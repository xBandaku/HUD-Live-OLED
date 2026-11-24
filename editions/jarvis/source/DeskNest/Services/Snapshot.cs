#nullable enable
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskNest.Views;

namespace DeskNest.Services;

/// <summary>
/// Developer aid: <c>HudWall.exe --organizer-snapshot &lt;folder&gt;</c> renders every frame and the launcher to PNGs,
/// so the UI can be checked without capturing the rest of the screen.
/// </summary>
public static class Snapshot
{
    public static void RenderAllAfterDelay(string folder, LauncherWindow launcher, Action done)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Directory.CreateDirectory(folder);
            var n = 0;
            foreach (var frame in Application.Current.Windows.OfType<FrameWindow>())
                Save(frame, Path.Combine(folder, $"frame-{++n}-{Safe(frame.Config.Title)}.png"));
            launcher.ShowPad();
            launcher.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                Save(launcher, Path.Combine(folder, "launcher.png"));
                Log.Info($"Snapshots written to {folder}");
                done();
            });
        };
        timer.Start();
    }

    private static void Save(Window window, string path)
    {
        if (window.Content is not FrameworkElement root || root.ActualWidth < 1)
            return;
        var dpi = VisualTreeHelper.GetDpi(root);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        // Paint a stand-in wallpaper under the translucent frame so the result looks like it does on a desktop.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x2B, 0x4C, 0x7E), Color.FromRgb(0x56, 0x2D, 0x6B), 45), null,
                new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string Safe(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
