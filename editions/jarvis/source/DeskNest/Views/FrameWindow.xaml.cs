#nullable enable
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeskNest.Interop;
using DeskNest.Models;
using DeskNest.Services;
using Microsoft.Win32;

namespace DeskNest.Views;

/// <summary>A frame on the desktop: either a live folder portal or a fence of pinned shortcuts.</summary>
public partial class FrameWindow : Window
{
    private const double TitleHeight = 32; // 30px title row + 1px border top and bottom

    public static readonly (string Name, string Color)[] ColorPresets =
    {
        ("HUD theme", HudTheme.FollowHud),
        ("Graphite", "#C01E1E24"),
        ("Midnight", "#C0141B2D"),
        ("Ocean", "#B0124A6B"),
        ("Forest", "#B0184D33"),
        ("Plum", "#B04A1E5A"),
        ("Ember", "#B06B2A12"),
        ("Glass", "#40FFFFFF"),
        ("Clear", "#01000000"),
    };

    private static readonly (string Name, int Size)[] IconSizes =
    {
        ("Small", 32), ("Medium", 40), ("Large", 56), ("Extra large", 72),
    };

    private readonly FrameManager _manager;
    private readonly TileList _tiles;
    private readonly DispatcherTimer _refreshTimer;
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _peekTimer;
    private bool _rolledVisual;
    private bool _peeking;
    private bool _dragInside;
    private int _menusOpen;
    private bool _loadingBounds;
    private string? _themedScreen;

    public FrameWindow(FrameConfig config, FrameManager manager)
    {
        Config = config;
        _manager = manager;
        InitializeComponent();
        Icon=DeskNest.Services.HudWindowIcon.Image;
        Icon=DeskNest.Services.HudWindowIcon.Image;

        _tiles = new TileList(Items, config.Id);
        _tiles.Activated += item => OpenItems(new[] { item });
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            Refresh();
        };
        // Backstop for a missed MouseLeave (fast flicks, a window popping up over the frame).
        _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _peekTimer.Tick += (_, _) => CollapseIfIdle();

        _loadingBounds = true;
        Left = config.Left;
        Top = config.Top;
        Width = config.Width;
        Height = config.Height;
        _loadingBounds = false;

        SourceInitialized += (_, _) =>
        {
            DesktopHost.Glue(this);
            ApplyTheme(force: true);
        };
        LocationChanged += (_, _) =>
        {
            SaveBounds();
            ApplyTheme(force: false);
        };
        SizeChanged += (_, _) => SaveBounds();
        MouseEnter += (_, _) => OnHover(true);
        MouseLeave += (_, _) => OnHover(false);
        Activated += (_, _) => { if (Config.Kind == FrameKind.Shortcuts) Refresh(); };
        PreviewKeyDown += OnPreviewKeyDown;
        // Frames are part of the desktop: "Show desktop", Win+D/Win+M and minimize-all must never hide them.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                Dispatcher.BeginInvoke(() => WindowState = WindowState.Normal);
        };
        FilterBox.LostKeyboardFocus += (_, _) => CollapseIfIdle();
        Root.DragEnter += (_, _) =>
        {
            _dragInside = true;
            Peek();
        };
        Root.DragLeave += (_, _) =>
        {
            // DragLeave also fires when moving between child elements; only count it when really outside.
            _dragInside = CursorInside();
            CollapseIfIdle();
        };
        Closed += (_, _) =>
        {
            _watcher?.Dispose();
            _peekTimer.Stop();
        };

        ApplyAppearance();
        ApplyRolled(config.RolledUp);
        StartWatching();
        Refresh();
    }

    public FrameConfig Config { get; }

    /// <summary>Set before a deliberate close so the manager doesn't treat it as Explorer tearing us down.</summary>
    public bool ClosingOnPurpose { get; set; }

    // ----- Appearance -----

    private void ApplyAppearance()
    {
        TitleText.Text = Config.Title.ToUpperInvariant();
        KindGlyph.Text = Config.Kind == FrameKind.Folder ? "" : ""; // folder / pin
        if (Config.Color == HudTheme.FollowHud)
            Root.SetResourceReference(Border.BackgroundProperty, "HudPanelBrush");
        else
            Root.Background = ParseBrush(Config.Color);
        Chrome.ResizeBorderThickness = new Thickness(Config.Locked ? 0 : 6);
    }

    /// <summary>
    /// Takes on the HUD theme of the monitor the frame is on. Only re-applies when it moved to another monitor,
    /// unless <paramref name="force"/> (the theme itself changed).
    /// </summary>
    public void ApplyTheme(bool force)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd).DeviceName;
        if (!force && screen == _themedScreen)
            return;
        _themedScreen = screen;
        HudTheme.Apply(Resources, _manager.PaletteForScreen(screen));
    }

    public static Brush ParseBrush(string color)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Color.FromArgb(0xC0, 0x1E, 0x1E, 0x24));
        }
    }

    private void ApplyRolled(bool rolled)
    {
        _rolledVisual = rolled;
        _loadingBounds = true;
        if (rolled)
        {
            Body.Visibility = Visibility.Collapsed;
            // Lower the minimum first; setting Height while MinHeight is still 80 leaves the window at 80.
            MinHeight = TitleHeight;
            MaxHeight = TitleHeight;
            Height = TitleHeight;
        }
        else
        {
            Body.Visibility = Visibility.Visible;
            MaxHeight = double.PositiveInfinity;
            MinHeight = 80;
            Height = Math.Max(Config.Height, 80);
        }
        _loadingBounds = false;
    }

    private void OnHover(bool inside)
    {
        TitleButtons.Opacity = inside || FilterBox.IsVisible ? 1 : 0;
        if (inside)
            Peek();
        else
            CollapseIfIdle();
    }

    // Rolled-up frames open while the pointer (or a dragged item) is over them, and roll back up the moment it
    // leaves. They only stay open while one of their menus is up or the filter box is being typed in.

    private void Peek()
    {
        if (!Config.RolledUp || !_rolledVisual)
            return;
        _peeking = true;
        ApplyRolled(false);
        _peekTimer.Start();
    }

    private void CollapseIfIdle()
    {
        if (!_peeking || _menusOpen > 0 || _dragInside || FilterBox.IsKeyboardFocused || CursorInside())
            return;
        _peeking = false;
        _peekTimer.Stop();
        if (Config.RolledUp)
            ApplyRolled(true);
    }

    /// <summary>Checks the real cursor position; WPF's IsMouseOver can be stale right after a drag or a menu.</summary>
    private bool CursorInside()
    {
        if (!IsVisible)
            return false;
        var cursor = System.Windows.Forms.Cursor.Position;
        var topLeft = PointToScreen(new Point(0, 0));
        var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        return cursor.X >= topLeft.X && cursor.X < bottomRight.X && cursor.Y >= topLeft.Y && cursor.Y < bottomRight.Y;
    }

    /// <summary>Counts open menus so a peeked frame doesn't roll up underneath one.</summary>
    private ContextMenu Track(ContextMenu menu)
    {
        menu.Opened += (_, _) => _menusOpen++;
        menu.Closed += (_, _) =>
        {
            _menusOpen = Math.Max(0, _menusOpen - 1);
            Dispatcher.BeginInvoke(CollapseIfIdle, DispatcherPriority.Input);
        };
        return menu;
    }

    private void SaveBounds()
    {
        // A minimized window reports Windows' parking spot, not where the frame lives.
        if (_loadingBounds || !IsLoaded || WindowState != WindowState.Normal)
            return;
        Config.Left = Left;
        Config.Top = Top;
        Config.Width = ActualWidth;
        if (!_rolledVisual)
            Config.Height = ActualHeight;
        _manager.Store.Save();
    }

    // ----- Content -----

    private void StartWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (Config.Kind != FrameKind.Folder || !Directory.Exists(Config.FolderPath))
            return;
        try
        {
            _watcher = new FileSystemWatcher(Config.FolderPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Attributes,
            };
            FileSystemEventHandler changed = (_, e) =>
            {
                if (e.ChangeType == WatcherChangeTypes.Changed)
                    ShellIcons.Forget(e.FullPath);
                Dispatcher.BeginInvoke(ScheduleRefresh);
            };
            _watcher.Created += changed;
            _watcher.Deleted += changed;
            _watcher.Changed += changed;
            _watcher.Renamed += (_, _) => Dispatcher.BeginInvoke(ScheduleRefresh);
            _watcher.Error += (_, _) => Dispatcher.BeginInvoke(() => { StartWatching(); ScheduleRefresh(); });
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Log.Error($"Watching {Config.FolderPath} failed", ex);
        }
    }

    private void ScheduleRefresh()
    {
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    public void Refresh()
    {
        var selected = _tiles.Selected.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filter = FilterBox.Text.Trim();
        var missingFolder = Config.Kind == FrameKind.Folder && !Directory.Exists(Config.FolderPath);

        var all = Config.Kind == FrameKind.Folder ? LoadFolder() : Config.Items.Select(p => new ItemView(p, Config.IconSize, friendlyNames: true)).ToList();
        var shown = filter.Length == 0 ? all : all.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        Items.ItemsSource = shown;
        foreach (var item in shown.Where(i => selected.Contains(i.Path)))
            Items.SelectedItems.Add(item);

        ChooseFolderButton.Visibility = missingFolder ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = missingFolder
            ? $"Folder not found\n{Config.FolderPath}"
            : all.Count > 0 ? "No matches"
            : Config.Kind == FrameKind.Folder ? "This folder is empty.\nDrop files here to move them in."
            : "Drop desktop icons here to tidy them away,\nor pin apps and files from anywhere.";
    }

    private List<ItemView> LoadFolder()
    {
        if (!Directory.Exists(Config.FolderPath))
            return new List<ItemView>();
        IEnumerable<ItemView> items;
        try
        {
            items = Directory.EnumerateFileSystemEntries(Config.FolderPath)
                .Where(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                .Take(2000)
                .Select(p => new ItemView(p, Config.IconSize))
                .Where(i => Config.ShowHidden || !i.IsHidden)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Listing {Config.FolderPath} failed", ex);
            return new List<ItemView>();
        }

        var folders = items.OrderByDescending(i => i.IsFolder);
        return (Config.Sort switch
        {
            SortMode.Type => folders.ThenBy(i => i.Extension).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            SortMode.DateModified => folders.ThenByDescending(i => i.Modified),
            _ => folders.ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
        }).ToList();
    }

    private static void OpenItems(IEnumerable<ItemView> items)
    {
        foreach (var item in items)
        {
            if (item.Exists)
                ShellOps.Open(item.Path);
            else
                MessageBox.Show($"{item.Path}\n\nThis item no longer exists.", "DeskNest", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ----- Title bar -----

    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleRolledUp();
            return;
        }
        if (Config.Locked)
            return;
        DragMove();
        SnapToGrid();
    }

    private void SnapToGrid()
    {
        var grid = _manager.Store.Config.GridSnap;
        if (grid < 2)
            return;
        Left = Math.Round(Left / grid) * grid;
        Top = Math.Round(Top / grid) * grid;
    }

    private void ToggleRolledUp()
    {
        Config.RolledUp = !Config.RolledUp;
        _peeking = false;
        ApplyRolled(Config.RolledUp);
        _manager.Store.Save();
    }

    private void OnTitleRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = BuildFrameMenu();
        menu.PlacementTarget = TitleBar;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnMenuButton(object sender, RoutedEventArgs e)
    {
        var menu = BuildFrameMenu();
        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnFilterButton(object sender, RoutedEventArgs e) => ShowFilter(!FilterBox.IsVisible);

    private void ShowFilter(bool show)
    {
        if (show)
        {
            FilterBox.Visibility = Visibility.Visible;
            TitleText.Visibility = Visibility.Hidden;
            Activate();
            FilterBox.Focus();
        }
        else
        {
            FilterBox.Text = "";
            FilterBox.Visibility = Visibility.Collapsed;
            TitleText.Visibility = Visibility.Visible;
        }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void OnFilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ShowFilter(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Items.Items.Count > 0)
        {
            OpenItems(new[] { (ItemView)Items.Items[0] });
            e.Handled = true;
        }
        else if (e.Key == Key.Down && Items.Items.Count > 0)
        {
            Items.SelectedIndex = 0;
            (Items.ItemContainerGenerator.ContainerFromIndex(0) as UIElement)?.Focus();
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowFilter(true);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            Refresh();
            e.Handled = true;
        }
    }

    // ----- Frame menu -----

    private ContextMenu BuildFrameMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Rename frame…", () =>
        {
            var name = InputDialog.Ask("Rename frame", "Frame title:", Config.Title);
            if (name is null) return;
            Config.Title = name;
            ApplyAppearance();
            _manager.Store.Save();
        }));

        var colors = new MenuItem { Header = "Color" };
        foreach (var (name, color) in ColorPresets)
        {
            var c = color;
            colors.Items.Add(Item(name, () => { Config.Color = c; ApplyAppearance(); _manager.Store.Save(); },
                isChecked: string.Equals(Config.Color, c, StringComparison.OrdinalIgnoreCase)));
        }
        menu.Items.Add(colors);

        var sizes = new MenuItem { Header = "Icon size" };
        foreach (var (name, size) in IconSizes)
        {
            var s = size;
            sizes.Items.Add(Item(name, () => { Config.IconSize = s; _manager.Store.Save(); Refresh(); }, isChecked: Config.IconSize == s));
        }
        menu.Items.Add(sizes);

        if (Config.Kind == FrameKind.Folder)
        {
            var sort = new MenuItem { Header = "Sort by" };
            foreach (var (name, mode) in new[] { ("Name", SortMode.Name), ("Type", SortMode.Type), ("Date modified", SortMode.DateModified) })
            {
                var m = mode;
                sort.Items.Add(Item(name, () => { Config.Sort = m; _manager.Store.Save(); Refresh(); }, isChecked: Config.Sort == m));
            }
            menu.Items.Add(sort);
            menu.Items.Add(Item("Show hidden files", () => { Config.ShowHidden = !Config.ShowHidden; _manager.Store.Save(); Refresh(); },
                isChecked: Config.ShowHidden));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("New folder…", CreateSubfolder, enabled: Directory.Exists(Config.FolderPath)));
            menu.Items.Add(Item("Open in Explorer", () => ShellOps.OpenInExplorer(Config.FolderPath!), enabled: Directory.Exists(Config.FolderPath)));
            menu.Items.Add(Item("Change folder…", () => OnChooseFolder(this, new RoutedEventArgs())));
        }
        else
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Remove missing items", () =>
            {
                Config.Items.RemoveAll(p => !File.Exists(p) && !Directory.Exists(p));
                _manager.Store.Save();
                Refresh();
            }));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Roll up", ToggleRolledUp, isChecked: Config.RolledUp));
        menu.Items.Add(Item("Lock position and size", () =>
        {
            Config.Locked = !Config.Locked;
            ApplyAppearance();
            _manager.Store.Save();
        }, isChecked: Config.Locked));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("New folder portal…", () => _manager.AddFolderPortal(null, NearMe())));
        menu.Items.Add(Item("New shortcut fence", () => _manager.AddShortcutFence(NearMe())));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Delete frame…", () => _manager.Remove(this)));
        return Track(menu);
    }

    private Point NearMe() => new(Left + 24, Top + 24);

    private static MenuItem Item(string header, Action action, bool isChecked = false, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsChecked = isChecked, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose the folder this portal shows" };
        if (Directory.Exists(Config.FolderPath))
            dialog.InitialDirectory = Config.FolderPath;
        if (dialog.ShowDialog() != true)
            return;
        var renameToo = Config.Title == FrameManager.DefaultTitleFor(Config.FolderPath);
        Config.FolderPath = dialog.FolderName;
        if (renameToo)
            Config.Title = FrameManager.DefaultTitleFor(dialog.FolderName);
        ApplyAppearance();
        _manager.Store.Save();
        StartWatching();
        Refresh();
    }

    private void CreateSubfolder()
    {
        var name = InputDialog.Ask("New folder", "Folder name:", "New folder");
        if (name is null) return;
        try
        {
            Directory.CreateDirectory(Path.Combine(Config.FolderPath!, name));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "DeskNest", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ----- Item menu and keys -----

    private void OnItemsContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selected = _tiles.Selected;
        if (selected.Count == 0)
        {
            // Empty space: the frame menu.
            Items.ContextMenu = BuildFrameMenu();
            return;
        }

        var menu = new ContextMenu();
        menu.Items.Add(Item(selected.Count > 1 ? $"Open {selected.Count} items" : "Open", () => OpenItems(selected)));
        menu.Items.Add(Item("Open file location", () => ShellOps.RevealInExplorer(selected[0].Path), enabled: selected[0].Exists));
        menu.Items.Add(new Separator());
        if (Config.Kind == FrameKind.Folder)
        {
            menu.Items.Add(Item("Rename", () => RenameItem(selected[0]), enabled: selected.Count == 1));
            menu.Items.Add(Item("Delete", () => DeleteSelected()));
        }
        else
        {
            menu.Items.Add(Item("Pin to launcher", () => _manager.PinToLauncher(selected.Select(i => i.Path))));
            var owned = selected.Any(i => ShellOps.IsInside(i.Path, FrameManager.FenceStorage(Config)));
            menu.Items.Add(Item(owned ? "Move back to desktop" : "Remove from fence", () => DeleteSelected()));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Properties", () => ShellOps.ShowProperties(selected[0].Path), enabled: selected[0].Exists));
        Items.ContextMenu = Track(menu);
    }

    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _tiles.Selected.Count > 0)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.F2 && Config.Kind == FrameKind.Folder && _tiles.Selected.Count == 1)
        {
            RenameItem(_tiles.Selected[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Items.SelectAll();
            e.Handled = true;
        }
    }

    private void RenameItem(ItemView item)
    {
        var name = InputDialog.Ask("Rename", "New name:", Path.GetFileName(item.Path));
        if (name is not null && name != Path.GetFileName(item.Path))
            ShellOps.Rename(item.Path, name);
    }

    private void DeleteSelected()
    {
        var selected = _tiles.Selected;
        if (Config.Kind == FrameKind.Folder)
        {
            foreach (var item in selected)
                ShellOps.Recycle(item.Path);
        }
        else
        {
            var remove = selected.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Config.Items.RemoveAll(remove.Contains);
            foreach (var path in remove)
                _manager.ReleaseFromFence(Config, path);
            _manager.Store.Save();
            Refresh();
        }
    }

    // ----- Drag and drop -----

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DropEffect(e);
        e.Handled = true;
    }

    private DragDropEffects DropEffect(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return DragDropEffects.None;
        var internalDrag = TileList.IsInternal(e.Data, Config.Id);

        if (Config.Kind == FrameKind.Shortcuts)
            return internalDrag ? DragDropEffects.Move : DragDropEffects.Link;

        if (internalDrag || !Directory.Exists(Config.FolderPath))
            return DragDropEffects.None;
        var keys = e.KeyStates;
        if (keys.HasFlag(DragDropKeyStates.ControlKey))
            return DragDropEffects.Copy;
        if (keys.HasFlag(DragDropKeyStates.ShiftKey))
            return DragDropEffects.Move;
        // Explorer's default: move within a drive, copy across drives.
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        var sameDrive = paths.All(p => string.Equals(Path.GetPathRoot(p), Path.GetPathRoot(Config.FolderPath), StringComparison.OrdinalIgnoreCase));
        return sameDrive ? DragDropEffects.Move : DragDropEffects.Copy;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _dragInside = false;
        // Roll back up after the drop finishes if the pointer has already gone.
        Dispatcher.BeginInvoke(CollapseIfIdle, DispatcherPriority.Background);
        var effect = DropEffect(e);
        e.Handled = true;
        if (effect == DragDropEffects.None)
            return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

        if (Config.Kind == FrameKind.Folder)
        {
            // We perform the transfer ourselves, so report "none" to keep the source from acting on it too.
            e.Effects = DragDropEffects.None;
            ShellOps.Transfer(paths, Config.FolderPath!, copy: effect == DragDropEffects.Copy);
            ScheduleRefresh();
            return;
        }

        var index = Math.Clamp(DropIndex(e), 0, Config.Items.Count);
        var incoming = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!TileList.IsInternal(e.Data, Config.Id))
            incoming = incoming.Select(p => _manager.AdoptIntoFence(Config, p)).ToList();
        // Reordering within the fence (or re-dropping a pinned path) moves it rather than duplicating it.
        foreach (var path in incoming)
        {
            var existing = Config.Items.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (existing < 0) continue;
            Config.Items.RemoveAt(existing);
            if (existing < index) index--;
        }
        Config.Items.InsertRange(index, incoming);
        e.Effects = DragDropEffects.Link;
        _manager.Store.Save();
        Refresh();
    }

    /// <summary>Maps the drop position in the (possibly filtered) list back to an index in <see cref="FrameConfig.Items"/>.</summary>
    private int DropIndex(DragEventArgs e)
    {
        var shownIndex = _tiles.DropIndex(e);
        if (shownIndex >= Items.Items.Count)
            return Config.Items.Count;
        var target = ((ItemView)Items.Items[shownIndex]).Path;
        var index = Config.Items.FindIndex(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? Config.Items.Count : index;
    }
}
