using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ComponentModel;

namespace HudWall;

internal sealed class MonitorPreference
{
    private int _brightness = 60;
    [Category("Appearance"), DisplayName("Theme"), TypeConverter(typeof(MonitorThemeConverter))]
    public string Theme { get; set; } = "Global";
    [Category("Playback"), DisplayName("Pause this monitor")]
    public bool Paused { get; set; }
    [Category("Playback"), DisplayName("Pause for fullscreen apps")]
    public bool AutoPause { get; set; } = true;
    [Category("Appearance"), DisplayName("Transparent background")]
    public bool Transparent { get; set; }
    [Category("OLED care"), DisplayName("OLED mode")]
    public bool Oled { get; set; } = true;
    [Category("OLED care"), DisplayName("Brightness (%)"), Description("25–100%. Applies to HUD content only.")]
    public int OledBrightness { get => _brightness; set => _brightness = Math.Clamp(value, 25, 100); }
    [Category("OLED care"), DisplayName("Minute pixel shifting")]
    public bool PixelShift { get; set; } = true;
    [Category("OLED care"), DisplayName("Gentle drift")]
    public bool OledFloat { get; set; } = true;
    [Browsable(false)]
    public int OledScale { get; set; } = 65;
    [Category("OLED care"), DisplayName("Dim after 2 minutes idle")]
    public bool IdleDim { get; set; } = true;
}

internal sealed class MonitorThemeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => new(new[] { "Global", "Custom" }.Concat(ThemeSettings.Presets.Keys).ToArray());
}

internal static class MonitorSettings
{
    internal static Dictionary<string,MonitorPreference> Load(string assets)
    {
        var result = new Dictionary<string,MonitorPreference>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var saved=JsonSerializer.Deserialize<Dictionary<string,MonitorPreference>>(SettingsStore.ReadAllText(Path.Combine(assets,"monitors.json")));
            if(saved!=null) foreach(var item in saved)
            {
                if(item.Value==null || string.IsNullOrWhiteSpace(item.Key))continue;
                if(item.Value.Theme!="Global" && item.Value.Theme!="Custom" && !ThemeSettings.Presets.ContainsKey(item.Value.Theme)) item.Value.Theme="Global";
                item.Value.OledBrightness = Math.Clamp(item.Value.OledBrightness, 25, 100);
                item.Value.OledScale = Math.Clamp(item.Value.OledScale, 50, 80);
                result[item.Key]=item.Value;
            }
        }
        catch(Exception ex) when(ex is IOException || ex is JsonException) { }
        return result;
    }
    internal static void Save(string assets,Dictionary<string,MonitorPreference> preferences)
    {
        Directory.CreateDirectory(assets);
        SettingsStore.WriteAllText(Path.Combine(assets,"monitors.json"),JsonSerializer.Serialize(preferences));
    }
    internal static string ColorFolder(string assets,string device) => Path.Combine(assets,"monitor-colors",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device)))[..16]);

    internal static Dictionary<string,string> Colors(string assets,string device,MonitorPreference preference)
    {
        if(preference.Theme=="Custom") return ThemeSettings.Load(ColorFolder(assets,device));
        return ThemeSettings.Presets.TryGetValue(preference.Theme,out var palette) ? new(palette) : ThemeSettings.Load(assets);
    }
}

internal sealed record MonitorState(string id,string label,int x,int y,int width,int height,bool paused,string pauseReason,Dictionary<string,string> colors,bool oled,int oledBrightness,bool pixelShift,bool oledIdle,bool oledFloat,int oledScale,bool transparent);
