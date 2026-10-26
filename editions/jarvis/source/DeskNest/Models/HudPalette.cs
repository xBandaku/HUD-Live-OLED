#nullable enable
namespace DeskNest.Models;

/// <summary>A HUD Live theme as #RRGGBB strings: the same accent / accent2 / background / text keys as colors.json.</summary>
public sealed record HudPalette(string Accent, string Accent2, string Background, string Text)
{
    /// <summary>JARVIS - Arc Reactor, used until the HUD sends its theme.</summary>
    public static HudPalette Default { get; } = new("#38DBFF", "#A9F4FF", "#020A12", "#EFFBFF");

    public static HudPalette From(IReadOnlyDictionary<string, string> colors) => new(
        colors.TryGetValue("accent", out var accent) ? accent : Default.Accent,
        colors.TryGetValue("accent2", out var accent2) ? accent2 : Default.Accent2,
        colors.TryGetValue("background", out var background) ? background : Default.Background,
        colors.TryGetValue("text", out var text) ? text : Default.Text);
}
