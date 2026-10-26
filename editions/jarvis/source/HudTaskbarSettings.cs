using System.Text.Json;

namespace HudWall;

internal sealed class HudTaskbarSettings
{
    public bool Enabled { get; set; }
    public List<string> Pins { get; set; } = new();
    public bool PinsInitialized { get; set; }

    internal static HudTaskbarSettings Load(string assets)
    {
        try
        {
            string text = SettingsStorage.Read(Path.Combine(assets, "taskbar.json"));
            var settings = JsonSerializer.Deserialize<HudTaskbarSettings>(text) ?? new();
            settings.Pins ??= new();
            using var json = JsonDocument.Parse(text);
            // Older saved arrays, including [], represent the user's choice.
            // Fresh shipped defaults explicitly opt into first-run pin import.
            if (!json.RootElement.TryGetProperty(nameof(PinsInitialized), out _) &&
                json.RootElement.TryGetProperty(nameof(Pins), out var pins) && pins.ValueKind == JsonValueKind.Array)
                settings.PinsInitialized = true;
            return settings;
        }
        catch { return new(); }
    }

    internal bool InitializePins(Func<IEnumerable<string>> import, Func<IEnumerable<string>> defaults)
    {
        if (PinsInitialized) return false;
        if (Pins.Count == 0) Pins = import().ToList();
        if (Pins.Count == 0) Pins = defaults().ToList();
        PinsInitialized = true;
        return true;
    }

    internal void Save(string assets) => SettingsStorage.Write(Path.Combine(assets, "taskbar.json"), JsonSerializer.Serialize(this));
}
