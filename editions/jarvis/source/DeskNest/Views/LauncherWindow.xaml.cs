#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeskNest.Interop;
using DeskNest.Models;
using DeskNest.Services;

namespace DeskNest.Views;

/// <summary>ViPad-style launcher: a popup grid of apps organized in tabs, summoned by hotkey or the tray icon.</summary>
public partial class LauncherWindow : Window
{
    private const int IconSize = 56;
    private const string OwnerId = "launcher";

    private readonly ConfigStore _store;
    private readonly TileList _tiles;
    private bool _dialogOpen;

    public LauncherWindow(ConfigStore store)
    {
        _store = store;
        InitializeComponent();
        Icon=DeskNest.Services.HudWindowIcon.Image;
        _tiles = new TileList(Items, OwnerId);
        _tiles.Activated += Launch;

        if (Config.Tabs.Count == 0)
            Config.Tabs.Add(new LauncherTab());

        KeepOpenButton.IsChecked = Config.KeepOpen;
        Deactivated += (_, _) =>
        {
            if (!Config.KeepOpen && !_dialogOpen && Items.ContextMenu?.IsOpen != true)
                Hide();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        BuildTabs();
        RefreshItems();
    }

    private LauncherConfig Config => _store.Config.Launcher;

    /// <summary>HUD theme for a Windows display name; the pad takes the theme of the monitor it opens on.</summary>
    public Func<string?, HudPalette> PaletteForScreen { get; set; } = _ => HudPalette.Default;

    private LauncherTab CurrentTab
    {
        get
        {
            Config.SelectedTab = Math.Clamp(Config.SelectedTab, 0, Config.Tabs.Count - 1);
            return Config.Tabs[Config.SelectedTab];
        }
    }

    public void Toggle()
    {
        if (IsVisible && IsActive)
            Hide();
        else
            ShowPad();
    }

    public void ShowPad()
    {
        SearchBox.Text = "";
        RefreshItems();
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).DeviceName;
        HudTheme.Apply(Resources, PaletteForScreen(screen));
        Show();
        CenterOnCursorScreen();
        Activate();
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    /// <summary>Adds paths to the current tab (used by "Pin to launcher" in fences).</summary>
    public void Pin(IEnumerable<string> paths)
    {
        AddToTab(CurrentTab, paths, CurrentTab.Items.Count);
        RefreshItems();
    }

    private void CenterOnCursorScreen()
    {
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var w = ActualWidth * dpi.DpiScaleX;
        var h = ActualHeight * dpi.DpiScaleY;
        Left = (screen.Left + (screen.Width - w) / 2) / dpi.DpiScaleX;
        Top = (screen.Top + (screen.Height - h) / 2.4) / dpi.DpiScaleY;
    }

    // ----- Tabs -----

    private void BuildTabs()
    {
        Tabs.Children.Clear();
        for (var i = 0; i < Config.Tabs.Count; i++)
        {
            var index = i;
            var tab = Config.Tabs[i];
            var button = new RadioButton
            {
                Content = tab.Name,
                GroupName = "LauncherTabs",
                Style = (Style)FindResource("TabButton"),
                IsChecked = i == Config.SelectedTab,
                AllowDrop = true,
                Tag = tab,
            };
            button.Checked += (_, _) =>
            {
                Config.SelectedTab = index;
                _store.Save();
                SearchBox.Text = "";
                RefreshItems();
            };
            button.MouseRightButtonUp += (_, e) =>
            {
                ShowTabMenu(button, index);
                e.Handled = true;
            };
            button.DragOver += (_, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
                e.Handled = true;
            };
            button.Drop += (_, e) =>
            {
                e.Handled = true;
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return;
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                // Dragging tiles from the current tab onto another tab moves them there.
                if (TileList.IsInternal(e.Data, OwnerId) && tab != CurrentTab)
                    CurrentTab.Items.RemoveAll(p => paths.Contains(p, StringComparer.OrdinalIgnoreCase));
                AddToTab(tab, paths, tab.Items.Count);
                RefreshItems();
            };
            Tabs.Children.Add(button);
        }

        var add = new Button { Style = (Style)FindResource("GlyphButton"), Content = "", ToolTip = "New tab" };
        add.Click += (_, _) => AddTab();
        Tabs.Children.Add(add);
    }

    private void ShowTabMenu(FrameworkElement target, int index)
    {
        var menu = new ContextMenu { PlacementTarget = target };
        menu.Items.Add(MenuItem("Rename tab…", () =>
        {
            var name = AskName("Rename tab", Config.Tabs[index].Name);
            if (name is null) return;
            Config.Tabs[index].Name = name;
            Commit();
        }));
        menu.Items.Add(MenuItem("Move left", () => MoveTab(index, -1), enabled: index > 0));
        menu.Items.Add(MenuItem("Move right", () => MoveTab(index, 1), enabled: index < Config.Tabs.Count - 1));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Delete tab", () =>
        {
            var tab = Config.Tabs[index];
            if (tab.Items.Count > 0 && !Confirm($"Delete the tab \"{tab.Name}\" and its {tab.Items.Count} shortcuts?\nThe apps themselves are not affected."))
                return;
            Config.Tabs.RemoveAt(index);
            if (Config.Tabs.Count == 0)
                Config.Tabs.Add(new LauncherTab());
            Config.SelectedTab = Math.Min(Config.SelectedTab, Config.Tabs.Count - 1);
            Commit();
        }, enabled: Config.Tabs.Count > 1 || Config.Tabs[0].Items.Count > 0));
        menu.IsOpen = true;
    }

    private void AddTab()
    {
        var name = AskName("New tab", $"Tab {Config.Tabs.Count + 1}");
        if (name is null) return;
        Config.Tabs.Add(new LauncherTab { Name = name });
        Config.SelectedTab = Config.Tabs.Count - 1;
        Commit();
    }

    private void MoveTab(int index, int delta)
    {
        var tab = Config.Tabs[index];
        Config.Tabs.RemoveAt(index);
        Config.Tabs.Insert(index + delta, tab);
        if (Config.SelectedTab == index)
            Config.SelectedTab = index + delta;
        else if (Config.SelectedTab == index + delta)
            Config.SelectedTab = index;
        Commit();
    }

    private void OnTabsWheel(object sender, MouseWheelEventArgs e)
    {
        TabsScroller.ScrollToHorizontalOffset(TabsScroller.HorizontalOffset - e.Delta / 2.0);
        e.Handled = true;
    }

    private void Commit()
    {
        _store.Save();
        BuildTabs();
        RefreshItems();
    }

    // ----- Items -----

    private void RefreshItems()
    {
        var query = SearchBox.Text.Trim();
        SearchHint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        List<ItemView> items;
        if (query.Length == 0)
        {
            items = CurrentTab.Items.Select(p => new ItemView(p, IconSize, friendlyNames: true)).ToList();
        }
        else
        {
            items = Config.Tabs.SelectMany(t => t.Items)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new ItemView(p, IconSize, friendlyNames: true))
                .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            System.IO.Path.GetFileName(i.Path).Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        Items.ItemsSource = items;
        if (query.Length > 0 && items.Count > 0)
            Items.SelectedIndex = 0;

        EmptyText.Text = items.Count > 0 ? ""
            : query.Length > 0 ? "No matches"
            : "Drag apps, files or folders here.\nDrop onto a tab name to add them to that tab.";
        Footer.Text = $"{Config.Hotkey} to toggle  •  Enter to launch  •  Esc to close  •  right-click tabs and icons for options";
    }

    private void Launch(ItemView item)
    {
        if (!item.Exists)
        {
            Inform($"{item.Path}\n\nThis item no longer exists. Right-click it to remove it.");
            return;
        }
        Hide();
        ShellOps.Open(item.Path);
    }

    private void AddToTab(LauncherTab tab, IEnumerable<string> paths, int index)
    {
        var incoming = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        index = Math.Clamp(index, 0, tab.Items.Count);
        foreach (var path in incoming)
        {
            var existing = tab.Items.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (existing < 0) continue;
            tab.Items.RemoveAt(existing);
            if (existing < index) index--;
        }
        tab.Items.InsertRange(index, incoming);
        _store.Save();
    }

    private void RemoveSelected()
    {
        var remove = _tiles.Selected.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // While searching, results span tabs, so remove from every tab.
        var tabs = SearchBox.Text.Trim().Length > 0 ? Config.Tabs : new List<LauncherTab> { CurrentTab };
        foreach (var tab in tabs)
            tab.Items.RemoveAll(remove.Contains);
        _store.Save();
        RefreshItems();
    }

    private void OnItemsContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selected = _tiles.Selected;
        var menu = new ContextMenu();
        if (selected.Count == 0)
        {
            menu.Items.Add(MenuItem("Add apps or files…", BrowseToAdd, enabled: SearchBox.Text.Length == 0));
            menu.Items.Add(MenuItem("New tab…", AddTab));
            Items.ContextMenu = menu;
            return;
        }

        menu.Items.Add(MenuItem("Open", () => Launch(selected[0])));
        menu.Items.Add(MenuItem("Open file location", () => { Hide(); ShellOps.RevealInExplorer(selected[0].Path); }, enabled: selected[0].Exists));
        var moveTo = new MenuItem { Header = "Move to tab", IsEnabled = Config.Tabs.Count > 1 && SearchBox.Text.Length == 0 };
        foreach (var tab in Config.Tabs.Where(t => t != CurrentTab))
        {
            var target = tab;
            moveTo.Items.Add(MenuItem(tab.Name, () =>
            {
                var paths = selected.Select(i => i.Path).ToList();
                CurrentTab.Items.RemoveAll(p => paths.Contains(p, StringComparer.OrdinalIgnoreCase));
                AddToTab(target, paths, target.Items.Count);
                RefreshItems();
            }));
        }
        menu.Items.Add(moveTo);
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Remove from launcher", RemoveSelected));
        menu.Items.Add(MenuItem("Properties", () => ShellOps.ShowProperties(selected[0].Path), enabled: selected[0].Exists));
        Items.ContextMenu = menu;
    }

    private void OnKeepOpenChanged(object sender, RoutedEventArgs e)
    {
        Config.KeepOpen = KeepOpenButton.IsChecked == true;
        _store.Save();
    }

    /// <summary>Picks shortcuts from the Start menu (or anywhere) without needing to drag them in.</summary>
    private void BrowseToAdd()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add to launcher",
            Multiselect = true,
            DereferenceLinks = false, // keep .lnk files so arguments and icons survive
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Filter = "Apps and shortcuts|*.lnk;*.exe;*.url;*.appref-ms;*.bat;*.cmd|All files|*.*",
        };
        _dialogOpen = true;
        bool? ok;
        try { ok = dialog.ShowDialog(this); }
        finally { _dialogOpen = false; Activate(); }
        if (ok != true)
            return;
        AddToTab(CurrentTab, dialog.FileNames, CurrentTab.Items.Count);
        RefreshItems();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && SearchBox.Text.Length == 0
            ? TileList.IsInternal(e.Data, OwnerId) ? DragDropEffects.Move : DragDropEffects.Link
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || SearchBox.Text.Length > 0)
            return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        AddToTab(CurrentTab, paths, _tiles.DropIndex(e));
        RefreshItems();
        Activate();
    }

    // ----- Keyboard -----

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshItems();

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Items.Items.Count > 0)
        {
            Launch((ItemView)(Items.SelectedItem ?? Items.Items[0]));
            e.Handled = true;
        }
        else if (e.Key == Key.Down && Items.Items.Count > 0)
        {
            Items.SelectedIndex = Math.Max(0, Items.SelectedIndex);
            (Items.ItemContainerGenerator.ContainerFromIndex(Items.SelectedIndex) as UIElement)?.Focus();
            e.Handled = true;
        }
    }

    private void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _tiles.Selected.Count > 0)
        {
            RemoveSelected();
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (SearchBox.Text.Length > 0) SearchBox.Text = "";
            else Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
            Config.SelectedTab = (Config.SelectedTab + step + Config.Tabs.Count) % Config.Tabs.Count;
            SearchBox.Text = "";
            BuildTabs();
            RefreshItems();
            e.Handled = true;
        }
        else if (!SearchBox.IsKeyboardFocused && e.Key is >= Key.A and <= Key.Z && Keyboard.Modifiers == ModifierKeys.None)
        {
            // Type-to-search from anywhere in the pad.
            SearchBox.Focus();
        }
    }

    // ----- Dialog helpers (keep the pad from hiding while they are up) -----

    private string? AskName(string title, string initial)
    {
        _dialogOpen = true;
        try { return InputDialog.Ask(title, "Name:", initial); }
        finally { _dialogOpen = false; Activate(); }
    }

    private bool Confirm(string message)
    {
        _dialogOpen = true;
        try { return MessageBox.Show(this, message, "DeskNest", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK; }
        finally { _dialogOpen = false; Activate(); }
    }

    private void Inform(string message)
    {
        _dialogOpen = true;
        try { MessageBox.Show(this, message, "DeskNest", MessageBoxButton.OK, MessageBoxImage.Information); }
        finally { _dialogOpen = false; Activate(); }
    }

    private static MenuItem MenuItem(string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }
}
