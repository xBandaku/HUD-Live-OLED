#nullable enable
namespace DeskNest.Models;

public enum FrameKind
{
    /// <summary>Live view of a folder on disk (DeskFrame / Nimi Places / Portals style).</summary>
    Folder,
    /// <summary>A list of pinned paths; files stay where they are (NoFences / Palisades style).</summary>
    Shortcuts,
}

public enum SortMode
{
    Name,
    Type,
    DateModified,
    Manual,
}

public enum RuleMatch
{
    Extension,
    NameContains,
    Regex,
}

public sealed class AppConfig
{
    /// <summary>2 since frames follow the HUD theme (see App.MigrateToHudColors).</summary>
    public int Version { get; set; } = 2;
    public List<FrameConfig> Frames { get; set; } = new();
    public LauncherConfig Launcher { get; set; } = new();
    public AutoSortConfig AutoSort { get; set; } = new();
    public double GridSnap { get; set; } = 8;
}

public sealed class FrameConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New frame";
    /// <summary>Recovered fence storage remains at its original physical location; no content is moved during recovery.</summary>
    public string? StoragePath { get; set; }
    public FrameKind Kind { get; set; } = FrameKind.Shortcuts;

    /// <summary>Folder shown by a <see cref="FrameKind.Folder"/> frame.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Pinned paths of a <see cref="FrameKind.Shortcuts"/> frame, in display order.</summary>
    public List<string> Items { get; set; } = new();

    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 260;

    /// <summary>Background as #AARRGGBB, or "HUD" to follow the HUD theme of the monitor the frame is on.</summary>
    public string Color { get; set; } = "HUD";
    public int IconSize { get; set; } = 40;
    public SortMode Sort { get; set; } = SortMode.Name;
    public bool RolledUp { get; set; }
    public bool Locked { get; set; }
    public bool ShowHidden { get; set; }
}

public sealed class LauncherConfig
{
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";
    /// <summary>When false the pad hides as soon as it loses focus.</summary>
    public bool KeepOpen { get; set; }
    public int SelectedTab { get; set; }
    public List<LauncherTab> Tabs { get; set; } = new();
}

public sealed class LauncherTab
{
    public string Name { get; set; } = "Favorites";
    public List<string> Items { get; set; } = new();
}

public sealed class AutoSortConfig
{
    public bool Enabled { get; set; }
    /// <summary>Seconds to wait after a new desktop item appears before filing it, so downloads and saves can finish.</summary>
    public int SettleSeconds { get; set; } = 3;
    public List<SortRule> Rules { get; set; } = new();
}

public sealed class SortRule
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "New rule";
    public RuleMatch Match { get; set; } = RuleMatch.Extension;
    /// <summary>Extension list (".png .jpg"), a substring, or a regular expression, depending on <see cref="Match"/>.</summary>
    public string Pattern { get; set; } = "";
    public string TargetFolder { get; set; } = "";
    public bool IncludeFolders { get; set; }
}
