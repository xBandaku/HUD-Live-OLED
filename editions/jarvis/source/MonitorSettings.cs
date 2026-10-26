using System.Drawing;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HudWall;

internal sealed class MonitorPreference
{
    public string Interface {get;set;}="Global";
    public string Theme { get; set; } = "Global";
    public bool Paused { get; set; }
    public bool AutoPause { get; set; } = true;
    public bool Transparent { get; set; }
    public bool Oled { get; set; } = true;
    public int OledBrightness { get; set; } = 60;
    public bool PixelShift { get; set; } = true;
    public bool OledFloat { get; set; } = true;
    public int OledScale { get; set; } = 65;
    public bool IdleDim { get; set; } = true;
}

internal static class MonitorSettings
{
    internal static Dictionary<string,MonitorPreference> Load(string assets)
    {
        var result = new Dictionary<string,MonitorPreference>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var saved=JsonSerializer.Deserialize<Dictionary<string,MonitorPreference>>(SettingsStorage.Read(Path.Combine(assets,"monitors.json")));
            if(saved!=null) foreach(var item in saved)
            {
                if(item.Value==null || string.IsNullOrWhiteSpace(item.Key))continue;
                if(item.Value.Theme!="Global" && item.Value.Theme!="Custom" && !ThemeSettings.Presets.ContainsKey(item.Value.Theme)) item.Value.Theme="Global";
                if(item.Value.Interface is not ("Global" or "JARVIS" or "SHIELD"))item.Value.Interface="Global";
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
        SettingsStorage.Write(Path.Combine(assets,"monitors.json"),JsonSerializer.Serialize(preferences));
    }
    internal static string ColorFolder(string assets,string device) => Path.Combine(assets,"monitor-colors",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device)))[..16]);

    internal static Dictionary<string,string> Colors(string assets,string device,MonitorPreference preference)
    {
        if(preference.Theme=="Custom") return ThemeSettings.Load(ColorFolder(assets,device));
        return ThemeSettings.Presets.TryGetValue(preference.Theme,out var palette) ? new(palette) : ThemeSettings.Load(assets);
    }
}

internal sealed record MonitorState(string id,string label,int x,int y,int width,int height,bool paused,string pauseReason,Dictionary<string,string> colors,bool oled,int oledBrightness,bool pixelShift,bool oledIdle,bool oledFloat,int oledScale,bool transparent){public string interfaceStyle {get;init;}="Global";public PhysicalMonitor[] screens {get;init;}=Array.Empty<PhysicalMonitor>();public bool bezelSafe {get;init;}=true;}
internal sealed record PhysicalMonitor(string id,int x,int y,int width,int height,bool primary);
