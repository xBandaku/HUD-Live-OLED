#nullable enable
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DeskNest.Models;
using DeskNest.Services;
using Microsoft.Win32;

namespace DeskNest.Views;

/// <summary>Owns the frame windows: creates them from config, adds and removes frames, and revives them after Explorer restarts.</summary>
public sealed class FrameManager
{
    private readonly List<FrameWindow> _windows = new();
    private bool _shuttingDown;

    public FrameManager(ConfigStore store)
    {
        Store = store;
    }

    public ConfigStore Store { get; }

    /// <summary>Set by the app so fences can send items to the launcher.</summary>
    public Action<IEnumerable<string>>? PinToLauncherHandler { get; set; }

    /// <summary>HUD theme for a Windows display name (null: the global theme).</summary>
    public Func<string?, HudPalette> PaletteForScreen { get; set; } = _ => HudPalette.Default;

    /// <summary>False while all frames are hidden (tray or "Jarvis hide frames"); not saved across restarts.</summary>
    public bool Visible { get; private set; } = true;

    public void SetVisible(bool visible)
    {
        if (Visible == visible)
            return;
        Visible = visible;
        foreach (var window in _windows)
        {
            if (visible)
                window.Show();
            else
                window.Hide();
        }
    }

    /// <summary>Recolors every frame after the HUD theme changed.</summary>
    public void ApplyTheme()
    {
        foreach (var window in _windows)
            window.ApplyTheme(force: true);
    }

    public void ShowAll()
    {
        AdoptPendingDesktopItems();
        foreach (var frame in Store.Config.Frames)
            Open(frame);

        // Items that were in use when dropped (e.g. a folder an app is running from) are pinned for now and
        // moved into their fence as soon as they are free.
        _retryTimer.Tick += (_, _) => AdoptPendingDesktopItems();
        _retryTimer.Start();
    }

    private readonly DispatcherTimer _retryTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly HashSet<string> _reportedBusy = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when a fence couldn't take a desktop icon yet: (fence title, item name, reason).</summary>
    public event Action<string, string, string>? AdoptionDeferred;

    private void AdoptPendingDesktopItems()
    {
        var changed = false;
        foreach (var fence in Store.Config.Frames.Where(f => f.Kind == FrameKind.Shortcuts))
        {
            for (var i = 0; i < fence.Items.Count; i++)
            {
                if (!IsDirectlyOnDesktop(fence.Items[i]))
                    continue;
                var adopted = AdoptIntoFence(fence, fence.Items[i]);
                if (adopted == fence.Items[i])
                    continue;
                fence.Items[i] = adopted;
                changed = true;
            }
        }
        if (!changed)
            return;
        Store.Save();
        RefreshAll();
    }

    // ----- Fence storage -----
    // Like Fences, a fence owns the desktop icons dropped on it: they move into the fence's own folder so they no
    // longer clutter the desktop, and go back to the desktop when removed from the fence or when the fence is deleted.
    // Anything dropped from elsewhere (Start menu, other folders) is only pinned and stays where it is.

    public static string FenceStorage(FrameConfig fence) =>
        fence.StoragePath ?? Path.Combine(ConfigStore.DataFolder, "Fences", fence.Id);

    private static string FenceStorageRoot => Path.Combine(ConfigStore.DataFolder, "Fences");

    public static bool IsDirectlyOnDesktop(string path)
    {
        var parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar));
        return parent is not null &&
               (SamePath(parent, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)) ||
                SamePath(parent, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)));
    }

    /// <summary>
    /// Moves a desktop icon (or an icon owned by another fence) into <paramref name="fence"/>'s storage and returns
    /// its new path. Other paths, and items that can't be moved (e.g. all-users icons without admin rights), are
    /// returned unchanged and simply pinned.
    /// </summary>
    public string AdoptIntoFence(FrameConfig fence, string path)
    {
        var storage = FenceStorage(fence);
        var ownedElsewhere = Store.Config.Frames.Any(f=>ShellOps.IsInside(path,FenceStorage(f))) && !ShellOps.IsInside(path, storage);
        if (!IsDirectlyOnDesktop(path) && !ownedElsewhere)
            return path;
        if (!File.Exists(path) && !Directory.Exists(path))
            return path;
        try
        {
            var dest = ShellOps.MoveQuietly(path, storage) ?? path;
            Log.Info($"Fence \"{fence.Title}\" took {path} -> {dest}");
            if (ownedElsewhere)
            {
                foreach (var other in Store.Config.Frames.Where(f => f != fence))
                    other.Items.RemoveAll(p => SamePath(p, path));
                foreach (var window in _windows.Where(w => w.Config != fence))
                    window.Refresh();
            }
            Repoint(path, dest);
            _reportedBusy.Remove(path);
            return dest;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Retried every 30 seconds; only report it once.
            if (_reportedBusy.Add(path))
            {
                Log.Error($"Fence \"{fence.Title}\" could not take {path} yet; pinned it and will retry", ex);
                var reason = ShellOps.IsInside(path, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))
                    ? "It is on every user's desktop, which needs admin rights to change. It stays pinned."
                    : RunningFrom(path) is { } app
                        ? $"{app} is running from it. It will move into the fence once {app} is closed."
                        : "It is in use. It will move into the fence once it is free.";
                AdoptionDeferred?.Invoke(fence.Title, Path.GetFileName(path), reason);
            }
            return path;
        }
    }

    /// <summary>If the fence owns <paramref name="path"/>, moves it back to the desktop.</summary>
    public void ReleaseFromFence(FrameConfig fence, string path)
    {
        if (!ShellOps.IsInside(path, FenceStorage(fence)) || (!File.Exists(path) && !Directory.Exists(path)))
            return;
        try
        {
            var dest = ShellOps.MoveQuietly(path, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            Log.Info($"Fence \"{fence.Title}\" returned {path} -> {dest}");
            if (dest is not null)
                Repoint(path, dest);
        }
        catch (Exception ex)
        {
            Log.Error($"Could not return {path} to the desktop", ex);
            MessageBox.Show($"Could not move {Path.GetFileName(path)} back to the desktop.\n\n{ex.Message}\n\nIt is still in {FenceStorage(fence)}.",
                "DeskNest", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Name of a running program whose exe lives inside <paramref name="folder"/>, if any.</summary>
    private static string? RunningFrom(string folder)
    {
        if (!Directory.Exists(folder))
            return null;
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                var exe = process.MainModule?.FileName;
                if (exe is not null && ShellOps.IsInside(exe, folder))
                    return Path.GetFileNameWithoutExtension(exe);
            }
            catch (Exception)
            {
                // Access denied for elevated/system processes; skip them.
            }
            finally
            {
                process.Dispose();
            }
        }
        return null;
    }

    /// <summary>Keeps launcher pins (and plain pins in other fences) working after an icon moves.</summary>
    private void Repoint(string from, string to)
    {
        foreach (var list in Store.Config.Launcher.Tabs.Select(t => t.Items))
        {
            for (var i = 0; i < list.Count; i++)
                if (SamePath(list[i], from)) list[i] = to;
        }
        foreach (var frame in Store.Config.Frames.Where(f => f.Kind == FrameKind.Shortcuts))
        {
            for (var i = 0; i < frame.Items.Count; i++)
                if (SamePath(frame.Items[i], from)) frame.Items[i] = to;
        }
        Store.Save();
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    public void CloseAll()
    {
        _shuttingDown = true;
        foreach (var window in _windows.ToList())
        {
            window.ClosingOnPurpose = true;
            window.Close();
        }
        _windows.Clear();
    }

    public static string DefaultTitleFor(string? folder)
    {
        if (string.IsNullOrEmpty(folder))
            return "Folder";
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? folder : name;
    }

    /// <summary>Creates a portal; asks for the folder when <paramref name="folder"/> is null.</summary>
    public FrameConfig? AddFolderPortal(string? folder, Point? at = null)
    {
        if (folder is null)
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder to show on the desktop" };
            if (dialog.ShowDialog() != true)
                return null;
            folder = dialog.FolderName;
        }
        var frame = new FrameConfig
        {
            Kind = FrameKind.Folder,
            FolderPath = folder,
            Title = DefaultTitleFor(folder),
        };
        return Add(frame, at);
    }

    public FrameConfig AddShortcutFence(Point? at = null, string title = "Shortcuts") =>
        Add(new FrameConfig { Kind = FrameKind.Shortcuts, Title = title }, at);

    private FrameConfig Add(FrameConfig frame, Point? at)
    {
        var area = SystemParameters.WorkArea;
        var pos = at ?? new Point(area.Left + 80 + 32 * (Store.Config.Frames.Count % 8), area.Top + 80 + 32 * (Store.Config.Frames.Count % 8));
        frame.Left = Math.Min(pos.X, area.Right - frame.Width);
        frame.Top = Math.Min(pos.Y, area.Bottom - frame.Height);
        Store.Config.Frames.Add(frame);
        Store.Save();
        Open(frame);
        return frame;
    }

    public void Remove(FrameWindow window)
    {
        var what = window.Config.Kind == FrameKind.Folder
            ? "The folder and its files stay where they are."
            : "Desktop icons in it go back to the desktop; other pinned items are not touched.";
        if (MessageBox.Show($"Remove the frame \"{window.Config.Title}\"?\n\n{what}", "DeskNest",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        foreach (var path in window.Config.Items.ToList())
            ReleaseFromFence(window.Config, path);
        Store.Config.Frames.Remove(window.Config);
        Store.Save();
        _windows.Remove(window);
        window.ClosingOnPurpose = true;
        window.Close();
    }

    public void PinToLauncher(IEnumerable<string> paths) => PinToLauncherHandler?.Invoke(paths);

    /// <summary>Folder paths of all portals, for the auto-sort rule editor.</summary>
    public IEnumerable<(string Title, string Folder)> PortalFolders() =>
        Store.Config.Frames.Where(f => f.Kind == FrameKind.Folder && !string.IsNullOrEmpty(f.FolderPath))
            .Select(f => (f.Title, f.FolderPath!));

    public void RefreshAll()
    {
        foreach (var window in _windows)
            window.Refresh();
    }

    /// <summary>Moves frames that ended up off-screen (e.g. after unplugging a monitor) back onto the primary work area.</summary>
    public int BringOnScreen()
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var area = SystemParameters.WorkArea;
        var moved = 0;
        foreach (var window in _windows)
        {
            var title = new Rect(window.Left, window.Top, window.ActualWidth, 32);
            if (screen.Contains(title.TopLeft) && screen.Contains(title.BottomRight))
                continue;
            window.Left = area.Left + 40 + 24 * moved;
            window.Top = area.Top + 40 + 24 * moved;
            moved++;
        }
        return moved;
    }

    private void Open(FrameConfig frame)
    {
        var window = new FrameWindow(frame, this);
        window.Closed += (_, _) => OnClosed(window);
        _windows.Add(window);
        if (Visible)
            window.Show();
    }

    // Frames are owned by the shell's desktop window, so an Explorer restart destroys them. Bring them back.
    private void OnClosed(FrameWindow window)
    {
        if (_shuttingDown || window.ClosingOnPurpose)
            return;
        _windows.Remove(window);
        Log.Info($"Frame \"{window.Config.Title}\" was closed by the system; reopening.");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_shuttingDown && Store.Config.Frames.Contains(window.Config))
                Open(window.Config);
        };
        timer.Start();
    }
}
