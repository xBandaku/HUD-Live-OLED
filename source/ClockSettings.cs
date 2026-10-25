using System.Text.Json;

namespace HudWall;

internal static class ClockSettings
{
    internal static string Load(string assets)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                SettingsStore.ReadAllText(Path.Combine(assets, "clock.json")));
            return settings != null && settings.TryGetValue("format", out string format) && format == "24" ? "24" : "12";
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException) { return "12"; }
    }

    internal static void Save(string assets, string format)
    {
        if (format != "12" && format != "24") throw new ArgumentException("Unknown clock format", nameof(format));
        Directory.CreateDirectory(assets);
        SettingsStore.WriteAllText(Path.Combine(assets, "clock.json"), JsonSerializer.Serialize(new { format }));
    }
}
