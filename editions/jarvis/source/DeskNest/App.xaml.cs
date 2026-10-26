#nullable enable
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DeskNest.Interop;
using DeskNest.Models;
using DeskNest.Services;
using DeskNest.Views;
using Microsoft.Win32;

namespace DeskNest;

/// <summary>What the organizer needs from the HUD that hosts it.</summary>
public interface IOrganizerHost
{
    /// <summary>Shows a notification from the HUD's tray icon. Called on the organizer's thread.</summary>
    void Notify(string title, string text);
}

/// <summary>
/// The DeskNest desktop organizer, hosted inside HUD Live. It runs its own WPF dispatcher on a dedicated thread so
/// frames and the launcher stay responsive whatever the wallpaper is doing. HUD Live owns the tray icon, startup
/// entry and voice commands, and drives the organizer through the thread-safe members below.
/// </summary>
public partial class App : Application
{
    private const string InstanceName = @"Local\DeskNest-single-instance";
    private const string ShowLauncherEventName = @"Local\DeskNest-show-launcher";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IOrganizerHost _host;
    private readonly string? _snapshotFolder;
    private readonly TaskCompletionSource<App> _started;
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showLauncherSignal;
    private ConfigStore _store = null!;
    private FrameManager _frames = null!;
    private LauncherWindow _launcher = null!;
    private AutoSorter _sorter = null!;
    private HotkeyService _hotkey = null!;
    private HudPalette _palette;
    private IReadOnlyDictionary<string, HudPalette> _screenPalettes = new Dictionary<string, HudPalette>();
    private bool _cleanedUp;

    private App(IOrganizerHost host, HudPalette palette, string? snapshotFolder, TaskCompletionSource<App> started)
    {
        _host = host;
        _palette = palette;
        _snapshotFolder = snapshotFolder;
        _started = started;
    }

    /// <summary>
    /// Starts the organizer on its own UI thread. The task completes once frames are up, or faults if the organizer
    /// could not start (for example while a standalone DeskNest.exe still owns the layout).
    /// </summary>
    /// <param name="snapshotFolder">Developer aid: render every frame and the launcher to PNGs there, then stop.</param>
    public static Task<App> Start(IOrganizerHost host, HudPalette palette, string? snapshotFolder = null)
    {
        var started = new TaskCompletionSource<App>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App(host, palette, snapshotFolder, started);
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Error("Desktop organizer stopped", ex);
                started.TrySetException(ex);
            }
        }) { IsBackground = true, Name = "DeskNest organizer" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return started.Task;
    }

    /// <summary>True while another organizer, such as the standalone DeskNest.exe, owns the desktop layout.</summary>
    public static bool IsOtherInstanceRunning()
    {
        if (!Mutex.TryOpenExisting(InstanceName, out var mutex))
            return false;
        mutex.Dispose();
        return true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            StartOrganizer();
        }
        catch (Exception ex)
        {
            // WPF allows one Application per process, so a failed start is final until HUD Live restarts.
            Log.Error("Desktop organizer could not start", ex);
            _started.TrySetException(ex);
            Shutdown();
            return;
        }
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void StartOrganizer()
    {
        HudTheme.Apply(Resources, _palette);

        var snapshot = _snapshotFolder is not null;
        if (!snapshot)
        {
            _instanceMutex = new Mutex(true, InstanceName, out var isFirst);
            if (!isFirst)
                throw new InvalidOperationException("Another DeskNest is running.");
            // Launching the old DeskNest.exe again signals this event: open the launcher like it used to.
            _showLauncherSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowLauncherEventName);
            var signal = _showLauncherSignal;
            new Thread(() =>
            {
                try
                {
                    while (signal.WaitOne())
                        Dispatcher.BeginInvoke(() => _launcher.ShowPad());
                }
                catch (ObjectDisposedException)
                {
                }
            }) { IsBackground = true, Name = "DeskNest instance signal" }.Start();
        }

        _store = new ConfigStore();
        if (_store.IsFirstRun)
            SeedFirstRun(_store.Config);
        MigrateToHudColors(_store);

        _frames = new FrameManager(_store) { PaletteForScreen = PaletteFor };
        _launcher = new LauncherWindow(_store) { PaletteForScreen = PaletteFor };
        _frames.PinToLauncherHandler = paths => _launcher.Pin(paths);

        _sorter = new AutoSorter(_store);
        _sorter.Filed += (from, to, rule) =>
            _host.Notify($"Auto-sorted by \"{rule.Name}\"", $"{Path.GetFileName(from)} → {Path.GetDirectoryName(to)}");
        _hotkey = new HotkeyService();
        _hotkey.Pressed += () => _launcher.Toggle();
        _frames.AdoptionDeferred += (fence, item, reason) => _host.Notify($"\"{item}\" can't move into {fence} yet", reason);

        if (snapshot)
        {
            _frames.ShowAll();
            Snapshot.RenderAllAfterDelay(_snapshotFolder!, _launcher, () =>
            {
                Cleanup();
                Shutdown();
            });
            _started.TrySetResult(this);
            return;
        }

        _sorter.Start();
        var requested = _store.Config.Launcher.Hotkey;
        var hotkeyOk = RegisterHotkeyWithFallback();
        _frames.ShowAll();
        RemoveStandaloneStartup();

        if (!hotkeyOk)
            _host.Notify("Launcher hotkey unavailable", $"{requested} is taken by another app. Set \"Hotkey\" in config.json, or left-click the tray icon.");
        else if (_store.IsFirstRun || _store.Config.Launcher.Hotkey != requested)
            _host.Notify("Desktop organizer is running", $"Press {_store.Config.Launcher.Hotkey} or left-click the tray icon for the launcher. Right-click a frame's title for options.");
        if (_store.IsFirstRun)
            _store.SaveNow();
        Log.Info("Started inside HUD Live");
        _started.TrySetResult(this);
    }

    // ----- Called by HUD Live from its own thread -----

    public Task Stopped => _stopped.Task;
    public string LauncherHotkey => _store.Config.Launcher.Hotkey;
    public bool AutoSortEnabled => _store.Config.AutoSort.Enabled;
    public bool HasSortRules => _store.Config.AutoSort.Rules.Any(r => r.Enabled);
    public bool FramesVisible => _frames.Visible;

    public Task ShowLauncher() => OnUi(() => _launcher.ShowPad());
    public Task HideLauncher() => OnUi(() => _launcher.Hide());
    public Task ToggleLauncher() => OnUi(() => _launcher.Toggle());

    /// <summary>Adds a portal for <paramref name="folder"/>, or asks for one when it is null. False if the user cancelled.</summary>
    public Task<bool> NewFolderPortal(string? folder) => OnUi(() =>
    {
        _frames.SetVisible(true);
        return _frames.AddFolderPortal(folder) is not null;
    });

    public Task NewShortcutFence() => OnUi(() =>
    {
        _frames.SetVisible(true);
        _frames.AddShortcutFence();
    });

    public Task EditRules() => OnUi(() =>
    {
        var window = Windows.OfType<RulesWindow>().FirstOrDefault() ?? new RulesWindow(_store, _sorter, _frames);
        window.Show();
        window.Activate();
    });

    /// <summary>Applies the auto-sort rules to everything on the desktop now. Returns how many items moved.</summary>
    public Task<int> SortDesktopNow() => OnUi(() => _sorter.RunNow());

    public Task SetAutoSort(bool enabled) => OnUi(() =>
    {
        _store.Config.AutoSort.Enabled = enabled;
        _store.Save();
    });

    public Task SetFramesVisible(bool visible) => OnUi(() => _frames.SetVisible(visible));

    /// <summary>Shows hidden frames and moves off-screen ones back. Returns how many were moved.</summary>
    public Task<int> BringFramesOnScreen() => OnUi(() =>
    {
        _frames.SetVisible(true);
        return _frames.BringOnScreen();
    });

    public Task OpenSettingsFolder() => OnUi(() => ShellOps.OpenInExplorer(ConfigStore.DataFolder));

    public static bool DesktopIconsVisible => DesktopHost.DesktopIconsVisible;
    public static void SetDesktopIconsVisible(bool visible) => DesktopHost.SetDesktopIconsVisible(visible);

    /// <summary>Recolors frames and the launcher. <paramref name="screens"/> maps Windows display names to their theme.</summary>
    public void ApplyTheme(HudPalette global, IReadOnlyDictionary<string, HudPalette> screens) => Dispatcher.BeginInvoke(() =>
    {
        _palette = global;
        _screenPalettes = screens;
        HudTheme.Apply(Resources, global);
        _frames.ApplyTheme();
    });

    /// <summary>Saves the layout, closes the frames and ends the organizer thread. Safe to call from any thread.</summary>
    public void Stop()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                Cleanup();
                Shutdown();
            }, TimeSpan.FromSeconds(3));
        }
        catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
        {
            Log.Error("Desktop organizer did not stop cleanly", ex);
        }
    }

    // Work queued by the HUD runs here; failures are reported like any other organizer error, then fault the task.
    private Task OnUi(Action action) => OnUi(() => { action(); return true; });

    private Task<T> OnUi<T>(Func<T> func) => Dispatcher.InvokeAsync(() =>
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            Report(ex);
            throw;
        }
    }).Task;

    private HudPalette PaletteFor(string? screen) =>
        screen is not null && _screenPalettes.TryGetValue(screen, out var palette) ? palette : _palette;

    // ----- Startup helpers -----

    /// <summary>
    /// Keeps the configured hotkey if it is free. If the default is taken (other launchers like it too), tries a few
    /// alternatives and remembers the first that works; a hotkey the user chose themselves is never replaced.
    /// </summary>
    private bool RegisterHotkeyWithFallback()
    {
        var launcher = _store.Config.Launcher;
        if (_hotkey.Register(launcher.Hotkey))
            return true;
        if (launcher.Hotkey != new LauncherConfig().Hotkey)
            return false;
        foreach (var candidate in new[] { "Ctrl+Shift+Space", "Ctrl+Alt+Q", "Win+Alt+Space" })
        {
            if (!_hotkey.Register(candidate))
                continue;
            launcher.Hotkey = candidate;
            _store.Save();
            Log.Info($"Default hotkey was taken; using {candidate}");
            return true;
        }
        return false;
    }

    /// <summary>HUD Live's startup entry starts the organizer now, so the standalone DeskNest's own entry goes.</summary>
    private void RemoveStandaloneStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue("DeskNest") is not string command)
                return;
            key.DeleteValue("DeskNest", throwOnMissingValue: false);
            Log.Info($"Removed the standalone startup entry {command}");
            _host.Notify("DeskNest now runs inside HUD Live", "Its separate startup entry was removed. HUD Live starts your frames and launcher with the wallpaper.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not remove the standalone DeskNest startup entry", ex);
        }
    }

    /// <summary>Layout version 2: frames still on DeskNest's stock colors follow the HUD theme instead.</summary>
    private static void MigrateToHudColors(ConfigStore store)
    {
        var config = store.Config;
        if (config.Version >= 2)
            return;
        foreach (var frame in config.Frames.Where(f => f.Color is "#C01E1E24" or "#C0141B2D"))
            frame.Color = HudTheme.FollowHud;
        config.Version = 2;
        store.Save();
    }

    /// <summary>A starter layout so the first launch shows what the organizer does.</summary>
    private static void SeedFirstRun(AppConfig config)
    {
        var area = SystemParameters.WorkArea;
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
        {
            config.Frames.Add(new FrameConfig
            {
                Kind = FrameKind.Folder, Title = "Downloads", FolderPath = downloads, Sort = SortMode.DateModified,
                Left = area.Right - 400, Top = area.Top + 40, Width = 360, Height = 300,
            });
        }
        config.Frames.Add(new FrameConfig
        {
            Kind = FrameKind.Shortcuts, Title = "Shortcuts",
            Left = area.Right - 400, Top = area.Top + 370, Width = 360, Height = 200,
        });

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var starters = new[]
        {
            Path.Combine(windows, "explorer.exe"),
            Path.Combine(windows, "System32", "notepad.exe"),
            Path.Combine(windows, "System32", "calc.exe"),
            Path.Combine(windows, "System32", "control.exe"),
            Path.Combine(windows, "System32", "cmd.exe"),
        };
        config.Launcher.Tabs.Add(new LauncherTab { Name = "Favorites", Items = starters.Where(File.Exists).ToList() });
        config.Launcher.Tabs.Add(new LauncherTab { Name = "Work" });
        config.Launcher.Tabs.Add(new LauncherTab { Name = "Games" });
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Report(e.Exception);
        e.Handled = true;
    }

    private static void Report(Exception ex)
    {
        Log.Error("Unhandled exception", ex);
        MessageBox.Show($"Something went wrong in the desktop organizer:\n\n{ex.Message}\n\nDetails are in {Log.LogPath}",
            "HUD Live desktop organizer", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void Cleanup()
    {
        if (_cleanedUp)
            return;
        _cleanedUp = true;
        if (_store is not null)
        {
            _store.SaveNow();
            _frames?.CloseAll();
            _sorter?.Dispose();
            _hotkey?.Dispose();
            Log.Info("Exited");
        }
        _showLauncherSignal?.Dispose();
        if (_instanceMutex is not null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch (ApplicationException) { }
            _instanceMutex.Dispose();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
        _stopped.TrySetResult();
    }
}
