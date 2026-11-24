#nullable enable
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using DeskNest.Models;
using DeskNest.Services;
using Microsoft.Win32;

namespace DeskNest.Views;

/// <summary>Editor for auto-sort rules. Edits a copy and writes it back on Save.</summary>
public partial class RulesWindow : Window
{
    private const string SortedRoot = @"%USERPROFILE%\Documents\Desktop sorted";

    private static readonly (string Name, string Extensions, string Folder)[] Presets =
    {
        ("Images", ".png .jpg .jpeg .gif .webp .bmp .heic .svg", "Images"),
        ("Screenshots", "", "Screenshots"),
        ("Documents", ".pdf .doc .docx .xls .xlsx .ppt .pptx .txt .md .rtf .odt .csv", "Documents"),
        ("Archives", ".zip .7z .rar .tar .gz", "Archives"),
        // No .exe: portable apps often live on the desktop and would stop working if moved.
        ("Installers", ".msi .msix .msixbundle", "Installers"),
        ("Videos", ".mp4 .mov .mkv .avi .webm", "Videos"),
        ("Audio", ".mp3 .wav .flac .m4a .ogg", "Audio"),
    };

    private readonly ConfigStore _store;
    private readonly AutoSorter _sorter;
    private readonly FrameManager _frames;
    private readonly ObservableCollection<SortRule> _rules;

    public RulesWindow(ConfigStore store, AutoSorter sorter, FrameManager frames)
    {
        _store = store;
        _sorter = sorter;
        _frames = frames;
        InitializeComponent();
        Icon=DeskNest.Services.HudWindowIcon.Image;

        MatchColumn.ItemsSource = Enum.GetValues<RuleMatch>();
        _rules = new ObservableCollection<SortRule>(store.Config.AutoSort.Rules.Select(Clone));
        Grid.ItemsSource = _rules;
        EnabledBox.IsChecked = store.Config.AutoSort.Enabled;
        SettleBox.Text = store.Config.AutoSort.SettleSeconds.ToString();
    }

    private static SortRule Clone(SortRule r) => new()
    {
        Enabled = r.Enabled, Name = r.Name, Match = r.Match, Pattern = r.Pattern,
        TargetFolder = r.TargetFolder, IncludeFolders = r.IncludeFolders,
    };

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var rule = new SortRule { Name = $"Rule {_rules.Count + 1}", Pattern = ".png .jpg" };
        _rules.Add(rule);
        Grid.SelectedItem = rule;
        Grid.ScrollIntoView(rule);
    }

    private void OnAddPresets(object sender, RoutedEventArgs e)
    {
        foreach (var (name, extensions, folder) in Presets)
        {
            if (_rules.Any(r => r.Name == name))
                continue;
            var rule = name == "Screenshots"
                // Windows and most capture tools name files "Screenshot ...". Listed before Images so it wins.
                ? new SortRule { Name = name, Match = RuleMatch.NameContains, Pattern = "Screenshot", TargetFolder = Path.Combine(SortedRoot, folder) }
                : new SortRule { Name = name, Match = RuleMatch.Extension, Pattern = extensions, TargetFolder = Path.Combine(SortedRoot, folder) };
            if (name == "Screenshots")
                _rules.Insert(0, rule);
            else
                _rules.Add(rule);
        }
        Status.Text = "";
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is SortRule rule)
            _rules.Remove(rule);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);
    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        if (Grid.SelectedItem is not SortRule rule)
            return;
        var i = _rules.IndexOf(rule);
        var j = i + delta;
        if (j < 0 || j >= _rules.Count)
            return;
        _rules.Move(i, j);
        Grid.SelectedItem = rule;
    }

    private void OnBrowseTarget(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not SortRule rule)
            return;
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender };
        foreach (var (title, folder) in _frames.PortalFolders())
        {
            var item = new MenuItem { Header = $"Portal: {title}", ToolTip = folder };
            var f = folder;
            item.Click += (_, _) => { rule.TargetFolder = f; Grid.Items.Refresh(); };
            menu.Items.Add(item);
        }
        if (menu.Items.Count > 0)
            menu.Items.Add(new Separator());
        var browse = new MenuItem { Header = "Browse…" };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { Title = $"Folder for \"{rule.Name}\"" };
            if (dialog.ShowDialog(this) == true)
            {
                rule.TargetFolder = dialog.FolderName;
                Grid.Items.Refresh();
            }
        };
        menu.Items.Add(browse);
        menu.IsOpen = true;
    }

    private bool TrySave()
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var rule in _rules.Where(r => r.Enabled))
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern))
                return Fail($"\"{rule.Name}\" has no pattern.");
            if (string.IsNullOrWhiteSpace(rule.TargetFolder))
                return Fail($"\"{rule.Name}\" has no target folder.");
            if (rule.Match == RuleMatch.Regex)
            {
                try { _ = new Regex(rule.Pattern); }
                catch (ArgumentException ex) { return Fail($"\"{rule.Name}\": invalid regular expression ({ex.Message})."); }
            }
            var target = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rule.TargetFolder)).TrimEnd('\\');
            if (string.Equals(target, AutoSorter.DesktopFolder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return Fail($"\"{rule.Name}\" points at the desktop itself.");
        }
        if (!int.TryParse(SettleBox.Text, out var settle) || settle < 1 || settle > 600)
            return Fail("The wait must be between 1 and 600 seconds.");

        var config = _store.Config.AutoSort;
        config.Rules = _rules.Select(Clone).ToList();
        config.Enabled = EnabledBox.IsChecked == true;
        config.SettleSeconds = settle;
        _store.Save();
        return true;
    }

    private bool Fail(string message)
    {
        Status.Text = message;
        return false;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (TrySave())
            DialogResult = true;
    }

    private void OnRunNow(object sender, RoutedEventArgs e)
    {
        if (!TrySave())
            return;
        var moved = _sorter.RunNow();
        Status.Text = "";
        MessageBox.Show(this, moved == 0 ? "Nothing on the desktop matched a rule." : $"Moved {moved} item{(moved == 1 ? "" : "s")}.",
            "DeskNest", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
