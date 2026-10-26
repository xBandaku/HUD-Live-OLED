#nullable enable
using System.Windows;
using System.Windows.Media;
using DeskNest.Models;

namespace DeskNest.Services;

/// <summary>
/// Turns a HUD palette into the brushes frames, tiles and the launcher are drawn with, matching the JARVIS panels:
/// square corners, a 35% accent border and a diagonal accent-tinted gradient. Applied to the application resources
/// (global theme) and to each frame's own resources (the theme of the monitor it sits on).
/// </summary>
public static class HudTheme
{
    /// <summary>Frame color value meaning "follow the HUD theme" instead of a fixed #AARRGGBB.</summary>
    public const string FollowHud = "HUD";

    public static void Apply(ResourceDictionary resources, HudPalette palette)
    {
        var accent = Parse(palette.Accent, HudPalette.Default.Accent);
        var accent2 = Parse(palette.Accent2, HudPalette.Default.Accent2);
        var background = Parse(palette.Background, HudPalette.Default.Background);
        var text = Parse(palette.Text, HudPalette.Default.Text);

        resources["HudAccentColor"] = accent;
        resources["HudAccentBrush"] = Solid(accent, 0xFF);
        resources["HudAccent2Brush"] = Solid(accent2, 0xFF);
        resources["HudRuleBrush"] = Solid(accent, 0x59);
        resources["HudPanelBrush"] = Gradient(Fade(accent, 0x2E), Fade(background, 0xC8), Fade(background, 0xD8));
        resources["HudLauncherBrush"] = Gradient(Blend(background, accent, 0x30), background, background);
        resources["TextBrush"] = Solid(text, 0xF2);
        resources["SubtleTextBrush"] = Solid(text, 0xA6);
        resources["HoverBrush"] = Solid(accent, 0x22);
        resources["SelectedBrush"] = Solid(accent, 0x3D);
        resources["SelectedBorderBrush"] = Solid(accent, 0x99);
        resources["FrameBorderBrush"] = Solid(accent, 0x59);
        resources["InputBrush"] = Solid(accent, 0x1A);
    }

    private static Color Parse(string hex, string fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }

    private static Color Fade(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>Opaque mix of <paramref name="tint"/> over <paramref name="baseColor"/>, for surfaces that must not show through.</summary>
    private static Color Blend(Color baseColor, Color tint, byte amount)
    {
        byte Mix(byte b, byte t) => (byte)(b + (t - b) * amount / 255);
        return Color.FromRgb(Mix(baseColor.R, tint.R), Mix(baseColor.G, tint.G), Mix(baseColor.B, tint.B));
    }

    private static SolidColorBrush Solid(Color color, byte alpha)
    {
        var brush = new SolidColorBrush(Fade(color, alpha));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(Color start, Color middle, Color end)
    {
        var brush = new LinearGradientBrush(new GradientStopCollection
        {
            new(start, 0), new(middle, 0.55), new(end, 1),
        }, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}
