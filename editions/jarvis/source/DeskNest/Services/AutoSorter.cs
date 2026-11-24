#nullable enable
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using DeskNest.Models;

namespace DeskNest.Services;

/// <summary>
/// Files new items that land on the user's desktop into folders according to <see cref="SortRule"/>s.
/// The first enabled rule that matches wins. Items are moved only after they stop changing for a few seconds.
/// </summary>
public sealed class AutoSorter : IDisposable
{
    private static readonly HashSet<string> InProgressExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".part", ".partial", ".download", ".tmp", ".opdownload",
    };

    private readonly ConfigStore _store;
    private readonly Dictionary<string, DateTime> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _timer;
    private FileSystemWatcher? _watcher;

    public AutoSorter(ConfigStore store)
    {
        _store = store;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => ProcessPending();
    }

    public static string DesktopFolder => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    /// <summary>Raised after an item was moved: (original path, new path, rule).</summary>
    public event Action<string, string, SortRule>? Filed;

    private AutoSortConfig Settings => _store.Config.AutoSort;

    public void Start()
    {
        if (_watcher is not null || !Directory.Exists(DesktopFolder))
            return;
        _watcher = new FileSystemWatcher(DesktopFolder)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Created += (_, e) => Enqueue(e.FullPath);
        _watcher.Renamed += (_, e) => Enqueue(e.FullPath);
        _watcher.Changed += (_, e) => Touch(e.FullPath);
        _watcher.EnableRaisingEvents = true;
        _timer.Start();
    }

    /// <summary>Applies the rules to everything currently on the desktop. Returns how many items were moved.</summary>
    public int RunNow()
    {
        var moved = 0;
        foreach (var path in Directory.EnumerateFileSystemEntries(DesktopFolder))
        {
            if (TryFile(path))
                moved++;
        }
        return moved;
    }

    public static bool Matches(SortRule rule, string path, bool isFolder)
    {
        if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Pattern) || string.IsNullOrWhiteSpace(rule.TargetFolder))
            return false;
        if (isFolder && !rule.IncludeFolders)
            return false;

        var name = Path.GetFileName(path);
        switch (rule.Match)
        {
            case RuleMatch.Extension:
                if (isFolder)
                    return false;
                var ext = Path.GetExtension(name);
                return rule.Pattern
                    .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.StartsWith('.') ? p : "." + p.TrimStart('*'))
                    .Any(p => string.Equals(p, ext, StringComparison.OrdinalIgnoreCase));
            case RuleMatch.NameContains:
                return name.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
            case RuleMatch.Regex:
                try
                {
                    return Regex.IsMatch(name, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
                }
                catch (ArgumentException)
                {
                    return false; // invalid pattern; the rules editor flags it
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            default:
                return false;
        }
    }

    private void Enqueue(string path)
    {
        if (!Settings.Enabled)
            return;
        lock (_pending)
            _pending[path] = DateTime.UtcNow;
    }

    private void Touch(string path)
    {
        lock (_pending)
        {
            if (_pending.ContainsKey(path))
                _pending[path] = DateTime.UtcNow;
        }
    }

    private void ProcessPending()
    {
        List<string> due;
        var settle = TimeSpan.FromSeconds(Math.Max(1, Settings.SettleSeconds));
        lock (_pending)
        {
            due = _pending.Where(p => DateTime.UtcNow - p.Value >= settle).Select(p => p.Key).ToList();
            foreach (var path in due)
                _pending.Remove(path);
        }

        foreach (var path in due)
        {
            if (!Settings.Enabled)
                return;
            if (IsLocked(path))
            {
                Enqueue(path); // still being written; try again later
                continue;
            }
            TryFile(path);
        }
    }

    private bool TryFile(string path)
    {
        var isFolder = Directory.Exists(path);
        if (!isFolder && !File.Exists(path))
            return false;

        var name = Path.GetFileName(path);
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || InProgressExtensions.Contains(Path.GetExtension(name)))
            return false;
        var attrs = File.GetAttributes(path);
        if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System))
            return false;

        var rule = Settings.Rules.FirstOrDefault(r => Matches(r, path, isFolder));
        if (rule is null)
            return false;

        var target = Environment.ExpandEnvironmentVariables(rule.TargetFolder);
        // Never move a folder into itself, or a portal's own folder into its target.
        if (string.Equals(Path.GetFullPath(target).TrimEnd('\\'), Path.GetFullPath(path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return false;
        if (isFolder && ShellOps.IsInside(target, path))
            return false;
        if (string.Equals(Path.GetFullPath(target).TrimEnd('\\'), DesktopFolder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var dest = ShellOps.MoveQuietly(path, target);
            if (dest is null)
                return false;
            Log.Info($"Auto-sort \"{rule.Name}\": {path} -> {dest}");
            Filed?.Invoke(path, dest, rule);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Auto-sort could not move {path}", ex);
            return false;
        }
    }

    private static bool IsLocked(string path)
    {
        if (!File.Exists(path))
            return false;
        try
        {
            using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _watcher?.Dispose();
    }
}
