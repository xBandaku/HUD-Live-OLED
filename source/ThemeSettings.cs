using System.Text.Json;
using System.Text.RegularExpressions;

namespace HudWall;

internal static class ThemeSettings
{
    internal static readonly Dictionary<string, Dictionary<string,string>> Presets = new()
    {
        ["JARVIS - Arc Reactor"] = Palette("#38DBFF", "#A9F4FF", "#020A12", "#EFFBFF"),
        ["Rose HUD"] = Palette("#A93F68", "#CE8FA3", "#050409", "#ECE2E7"),
        ["Cyber Cyan"] = Palette("#22D3EE", "#A78BFA", "#050B14", "#E6F7FF"),
        ["Matrix Green"] = Palette("#22C55E", "#A3E635", "#030B06", "#E8FFED"),
        ["Ember"] = Palette("#F97316", "#FBBF24", "#120805", "#FFF1E6"),
        ["Arctic Blue"] = Palette("#60A5FA", "#BAE6FD", "#050A16", "#EFF6FF"),
        ["Monochrome"] = Palette("#E5E7EB", "#9CA3AF", "#08090B", "#F9FAFB"),
        ["Cyberpunk - Night City"] = Palette("#FCEE0A", "#00E5FF", "#080B14", "#F8F8DE"),
        ["Halo - Spartan"] = Palette("#91C96F", "#69C8FF", "#07120D", "#E8F5E6"),
        ["Fallout - Vault Terminal"] = Palette("#60FF6A", "#E6C76A", "#020A03", "#C9FFC8"),
        ["DOOM - Hellwalker"] = Palette("#FF3B30", "#FF9D2E", "#150403", "#FFE4D7"),
        ["Mass Effect - N7"] = Palette("#E84545", "#7CCBFF", "#080D18", "#EFF5FF"),
        ["Portal - Test Chamber"] = Palette("#39BFFF", "#FF9A32", "#07121B", "#F2F8FA"),
        ["Elden Ring - Lands Between"] = Palette("#D7B65B", "#A2B68A", "#101108", "#F1E8C9"),
        ["Zelda - Hyrule"] = Palette("#36D5A6", "#E9C86D", "#06130F", "#E9F6DD"),
        ["Minecraft - Overworld"] = Palette("#80C84C", "#C79362", "#0C1408", "#ECF1DC"),
        ["Resident Evil - Survival Horror"] = Palette("#C53644", "#C0BBA9", "#0E0709", "#EBE7DE"),
        ["Destiny - Guardian"] = Palette("#A591FF", "#FFB9D6", "#0D0920", "#F3EDFF"),
        ["Metroid - Samus"] = Palette("#FFAE38", "#5AF5D5", "#100C19", "#FFF0D7"),
        ["Witcher - White Wolf"] = Palette("#C8494A", "#BCC8D5", "#100A0D", "#EEE9E3"),
        ["Skyrim - Northern Sky"] = Palette("#8ABBD4", "#BFCBD3", "#080E14", "#E7EDF2"),
        ["Diablo - Sanctuary"] = Palette("#C73F32", "#C99D54", "#130705", "#F1E3CC"),
        ["Final Fantasy - Crystal"] = Palette("#67B9FF", "#C7AAFF", "#060C21", "#EBF3FF"),
        ["Hollow Knight - Hallownest"] = Palette("#8BAAE8", "#D1DCED", "#050815", "#E6ECF8"),
        ["Hades - Underworld"] = Palette("#E84E48", "#65D9B1", "#10070C", "#F4E8D9"),
        ["Stardew Valley - Twilight Farm"] = Palette("#BE87DC", "#F5D374", "#120B20", "#F7EBD6"),
        ["No Mans Sky - Atlas"] = Palette("#F3836F", "#73DDD8", "#08131A", "#EAF5EC"),
        ["Warframe - Tenno"] = Palette("#62D5CD", "#D9C09A", "#071210", "#EAF6F0"),
        ["Overwatch - Payload"] = Palette("#F6AA38", "#78BFE8", "#10131D", "#F4F7FA"),
        ["Dark Souls - Bonfire"] = Palette("#E89143", "#B6B3A7", "#100B07", "#EBE2D2"),
        ["Sea of Thieves - Ghost Ship"] = Palette("#59DDBA", "#E3BF6E", "#031512", "#E5F5E9"),
        ["OLED - Midnight"] = Palette("#8795AC", "#B0B7C1", "#000000", "#AEB7C5"),
        ["OLED - Emerald"] = Palette("#4FA777", "#7FB79A", "#000000", "#A7BAAE"),
        ["OLED - Amber"] = Palette("#B58A4C", "#C1A17A", "#000000", "#C0B29D"),
        ["OLED - Ice"] = Palette("#679DB8", "#8CAFC3", "#000000", "#ABBAC6"),
        ["OLED - Crimson"] = Palette("#A95D68", "#BC8A92", "#000000", "#BBA9AE"),
        ["OLED - Violet"] = Palette("#8E78B5", "#AC9BC5", "#000000", "#B5ACC4")
    };
    private static Dictionary<string,string> Palette(string accent,string accent2,string background,string text) =>
        new() { ["accent"]=accent, ["accent2"]=accent2, ["background"]=background, ["text"]=text };

    internal static Dictionary<string,string> Load(string assets, string file = "colors.json")
    {
        var result = new Dictionary<string,string>(Presets["Rose HUD"]);
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string,string>>(SettingsStore.ReadAllText(Path.Combine(assets,file)));
            foreach(string key in result.Keys.ToArray())
                if(saved != null && saved.TryGetValue(key,out string value) && Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}$")) result[key]=value;
        }
        catch(Exception ex) when(ex is IOException || ex is JsonException) { }
        return result;
    }

    internal static string Match(Dictionary<string,string> palette) => Presets.FirstOrDefault(p =>
        p.Value.All(color => palette.TryGetValue(color.Key,out string value) && string.Equals(color.Value,value,StringComparison.OrdinalIgnoreCase))).Key;

    internal static void Save(string assets, Dictionary<string,string> palette, bool custom = false)
    {
        Directory.CreateDirectory(assets);
        string json = JsonSerializer.Serialize(palette);
        SettingsStore.WriteAllText(Path.Combine(assets,"colors.json"),json);
        SettingsStore.WriteAllText(Path.Combine(assets,"colors.js"),"window.HUD_COLORS = " + json + ";");
        if(custom) SettingsStore.WriteAllText(Path.Combine(assets,"custom-colors.json"),json);
    }

    internal static void ApplyPreset(string assets, string name)
    {
        var current=Load(assets);
        if(Match(current)==null) SettingsStore.WriteAllText(Path.Combine(assets,"custom-colors.json"),JsonSerializer.Serialize(current));
        if(name=="Custom") { Save(assets,Load(assets,"custom-colors.json")); return; }
        if(!Presets.TryGetValue(name,out var preset)) throw new ArgumentException("Unknown theme",nameof(name));
        Save(assets,preset);
    }
}
