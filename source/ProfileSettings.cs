using System.Text.Json;
using System.Text.RegularExpressions;

namespace HudWall;

internal sealed class ProfileSettings
{
    public Dictionary<string, string> Colors { get; set; } = new();
    public Dictionary<string, string> CustomColors { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public Dictionary<string, MonitorPreference> Monitors { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> MonitorColors { get; set; } = new();
    public string Clock { get; set; } = "12";
    public string Temperature { get; set; } = "F";
    public int Audio { get; set; } = 100;

    internal static ProfileSettings Capture(string root) => new() {
        Colors = ThemeSettings.Load(root), CustomColors = ThemeSettings.Load(root, "custom-colors.json"),
        Performance = PerformanceSettings.Load(root), Monitors = MonitorSettings.Load(root),
        MonitorColors = MonitorSettings.Load(root).Where(p => p.Value.Theme == "Custom").ToDictionary(
            p => p.Key, p => ThemeSettings.Load(MonitorSettings.ColorFolder(root, p.Key))),
        Clock = ClockSettings.Load(root), Temperature = TemperatureSettings.Load(root), Audio = AudioSettings.Load(root)
    };

    internal ProfileSettings Clone() => JsonSerializer.Deserialize<ProfileSettings>(JsonSerializer.Serialize(this));

    internal void Validate()
    {
        static bool Palette(Dictionary<string, string> colors) => colors != null &&
            new[] { "accent", "accent2", "background", "text" }.All(key =>
                colors.TryGetValue(key, out var value) && Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}$"));
        if (!Palette(Colors) || !Palette(CustomColors) || Performance == null ||
            !new[] { 15, 30, 60, 120 }.Contains(Performance.Fps) ||
            !new[] { "Full", "Reduced", "Off" }.Contains(Performance.Animations) ||
            !new[] { .5, 1, 2 }.Contains(Performance.Speed) || Performance.PauseApps == null ||
            Performance.PauseApps.Any(string.IsNullOrWhiteSpace) ||
            Clock is not ("12" or "24") || Temperature is not ("C" or "F") || Audio < 0 || Audio > 300 ||
            Monitors == null || Monitors.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null ||
                (p.Value.Theme != "Global" && p.Value.Theme != "Custom" && !ThemeSettings.Presets.ContainsKey(p.Value.Theme ?? ""))) ||
            MonitorColors == null || MonitorColors.Any(p => string.IsNullOrWhiteSpace(p.Key) || !Palette(p.Value)) ||
            Monitors.Any(p => p.Value.Theme == "Custom" && !MonitorColors.ContainsKey(p.Key)))
            throw new InvalidDataException("The profile contains invalid or incomplete settings.");
    }

    internal void Apply(string root)
    {
        Validate();
        var previous = Capture(root);
        try { Write(root); }
        catch {
            try { previous.Write(root); }
            catch (Exception ex) { Program.Log("Profile rollback failed: " + ex); }
            throw;
        }
    }

    private void Write(string root)
    {
        ThemeSettings.Save(root, CustomColors, custom: true);
        ThemeSettings.Save(root, Colors);
        Performance.Save(root);
        ClockSettings.Save(root, Clock);
        TemperatureSettings.Save(root, Temperature);
        AudioSettings.Save(root, Audio);
        foreach (var entry in MonitorColors)
            ThemeSettings.Save(MonitorSettings.ColorFolder(root, entry.Key), entry.Value, custom: true);
        MonitorSettings.Save(root, Monitors);
    }
}

internal sealed class NamedProfile
{
    public string Name { get; set; }
    public ProfileSettings Settings { get; set; }
}

internal static class ProfileCatalog
{
    internal static List<NamedProfile> Load(string root, IEnumerable<string> displays = null)
    {
        var path = Path.Combine(root, "profiles.json");
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return Defaults(root, displays ?? Array.Empty<string>());
        var profiles = JsonSerializer.Deserialize<List<NamedProfile>>(SettingsStore.ReadAllText(path));
        if (profiles == null || profiles.Count > 100 || profiles.Any(p => p == null || p.Settings == null))
            throw new InvalidDataException("The saved profile list is invalid.");
        foreach (var profile in profiles) { ValidateName(profile.Name); profile.Settings.Validate(); }
        if (profiles.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Count)
            throw new InvalidDataException("The saved profile list contains duplicate names.");
        return profiles;
    }

    internal static void Save(string root, List<NamedProfile> profiles, string name, ProfileSettings settings)
    {
        name = name?.Trim(); ValidateName(name); settings.Validate();
        var next = profiles.Where(p => !string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (next.Count >= 100) throw new InvalidDataException("You can save up to 100 profiles.");
        next.Add(new() { Name = name, Settings = settings.Clone() });
        SettingsStore.WriteAllText(Path.Combine(root, "profiles.json"), JsonSerializer.Serialize(next));
        profiles.Clear(); profiles.AddRange(next);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            throw new InvalidDataException("Enter a profile name between 1 and 80 characters.");
    }

    private static List<NamedProfile> Defaults(string root, IEnumerable<string> displays)
    {
        var current = ProfileSettings.Capture(root);
        foreach (var display in displays) current.Monitors.TryAdd(display, new());
        var profiles = new List<NamedProfile>();
        foreach (var (name, theme, fps, animations, brightness, audio) in new[] {
            ("Gaming", "JARVIS - Arc Reactor", 30, "Reduced", 60, 100),
            ("Work", "Cyber Cyan", 30, "Reduced", 60, 0),
            ("Minimal", "Monochrome", 15, "Off", 40, 0),
            ("Night", "OLED - Midnight", 15, "Reduced", 25, 0) })
        {
            var settings = current.Clone();
            settings.Colors = new(ThemeSettings.Presets[theme]);
            settings.Performance.Fps = fps; settings.Performance.Animations = animations;
            settings.Performance.Speed = name == "Night" ? .5 : 1;
            settings.Performance.AutoPause = true; settings.Audio = audio;
            foreach (var preference in settings.Monitors.Values) {
                preference.Theme = "Global"; preference.OledBrightness = brightness;
                preference.AutoPause = true; preference.Paused = false;
            }
            profiles.Add(new() { Name = name, Settings = settings });
        }
        return profiles;
    }
}
