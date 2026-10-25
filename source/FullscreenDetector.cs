using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace HudWall;

internal static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect rect, int length);

    internal static bool CoversMonitor(Rectangle window, Rectangle monitor) =>
        window.Width > 0 && window.Height > 0 && monitor.Width > 0 && monitor.Height > 0 &&
        window.Left <= monitor.Left + 2 && window.Top <= monitor.Top + 2 &&
        window.Right >= monitor.Right - 2 && window.Bottom >= monitor.Bottom - 2;

    internal static bool IsFullscreenWindow(IntPtr window, int wallpaperPid)
    {
        if (window == IntPtr.Zero || IsIconic(window)) return false;
        GetWindowThreadProcessId(window, out uint pid);
        if (pid == wallpaperPid) return false;
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        if (new[] { "Progman", "WorkerW", "SHELLDLL_DefView", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" }.Contains(name.ToString())) return false;
        if (DwmGetWindowAttribute(window, 9, out Rect rect, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(window, out rect)) return false;
        return CoversMonitor(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), Screen.FromHandle(window).Bounds);
    }

    internal static bool IsForegroundFullscreen() => IsFullscreenWindow(GetForegroundWindow(), Environment.ProcessId);

    internal static string[] ForegroundFullscreenDevices()
    {
        var window=GetForegroundWindow();
        if(!IsFullscreenWindow(window,Environment.ProcessId))return Array.Empty<string>();
        if(DwmGetWindowAttribute(window,9,out Rect rect,Marshal.SizeOf<Rect>())!=0 && !GetWindowRect(window,out rect))return Array.Empty<string>();
        var bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
        return Screen.AllScreens.Where(screen=>CoversMonitor(bounds,screen.Bounds)).Select(screen=>screen.DeviceName).ToArray();
    }
    internal static string ForegroundMonitor() => ForegroundApp()=="" ? "" : Screen.FromHandle(GetForegroundWindow()).DeviceName;

    internal static string ForegroundApp()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return "";
        try { using var process = System.Diagnostics.Process.GetProcessById((int)pid); return process.ProcessName; }
        catch { return ""; }
    }
}
