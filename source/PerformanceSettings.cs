using System.Text.Json;

namespace HudWall;

internal sealed class PerformanceSettings
{
    public bool AutoPause { get; set; } = true;
    public int Fps { get; set; } = 30;
    public string Animations { get; set; } = "Full";
    public double Speed { get; set; } = 1;
    public List<string> PauseApps { get; set; } = new();

    internal static PerformanceSettings Load(string assets)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<PerformanceSettings>(SettingsStore.ReadAllText(Path.Combine(assets, "performance.json"))) ?? new();
            if (!new[] { 15, 30, 60, 120 }.Contains(settings.Fps)) settings.Fps = 30;
            if (!new[] { "Full", "Reduced", "Off" }.Contains(settings.Animations)) settings.Animations = "Full";
            if (!new[] { .5, 1, 2 }.Contains(settings.Speed)) settings.Speed = 1;
            settings.PauseApps = (settings.PauseApps ?? new()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return settings;
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException) { return new(); }
    }

    internal void Save(string assets)
    {
        Directory.CreateDirectory(assets);
        SettingsStore.WriteAllText(Path.Combine(assets, "performance.json"), JsonSerializer.Serialize(this));
    }
}
