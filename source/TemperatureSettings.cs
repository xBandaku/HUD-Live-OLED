using System.Text.Json;

namespace HudWall;

internal static class TemperatureSettings
{
    internal static string Load(string assets)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                SettingsStore.ReadAllText(Path.Combine(assets, "temperature.json")));
            return settings != null && settings.TryGetValue("unit", out string unit) && unit == "C" ? "C" : "F";
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException) { return "F"; }
    }

    internal static void Save(string assets, string unit)
    {
        if (unit != "C" && unit != "F") throw new ArgumentException("Unknown temperature unit", nameof(unit));
        Directory.CreateDirectory(assets);
        SettingsStore.WriteAllText(Path.Combine(assets, "temperature.json"), JsonSerializer.Serialize(new { unit }));
        SettingsStore.WriteAllText(Path.Combine(assets, "temperature.js"), "window.HUD_TEMPERATURE_UNIT = " + JsonSerializer.Serialize(unit) + ";");
    }
}
