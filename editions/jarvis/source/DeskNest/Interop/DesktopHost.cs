#nullable enable
using System.Windows;
using System.Windows.Interop;
using static DeskNest.Interop.NativeMethods;

namespace DeskNest.Interop;

/// <summary>
/// Keeps frame windows on the desktop layer: owned by the shell's desktop window, always at the bottom of the
/// z-order, out of Alt+Tab and the taskbar, and still visible after Win+D / "Show desktop".
/// </summary>
internal static class DesktopHost
{
    private const int ToggleDesktopIconsCommand = 0x7402;

    /// <summary>Call from <see cref="Window.SourceInitialized"/>.</summary>
    public static void Glue(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));

        var (host, _) = FindDesktop();
        if (host != IntPtr.Zero)
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, host);

        HwndSource.FromHwnd(hwnd)?.AddHook(KeepAtBottom);
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Shows or hides the regular desktop icons, like the "Show desktop icons" menu item.</summary>
    public static void ToggleDesktopIcons()
    {
        var (_, defView) = FindDesktop();
        if (defView != IntPtr.Zero)
            SendMessage(defView, WM_COMMAND, new IntPtr(ToggleDesktopIconsCommand), IntPtr.Zero);
    }

    /// <summary>Whether Explorer's desktop icon list is showing.</summary>
    public static bool DesktopIconsVisible
    {
        get
        {
            var (_, defView) = FindDesktop();
            var list = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            return list != IntPtr.Zero && IsWindowVisible(list);
        }
    }

    public static void SetDesktopIconsVisible(bool visible)
    {
        if (DesktopIconsVisible != visible)
            ToggleDesktopIcons();
    }

    private static IntPtr KeepAtBottom(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero)
        {
            var pos = System.Runtime.InteropServices.Marshal.PtrToStructure<WINDOWPOS>(lParam);
            pos.hwndInsertAfter = HWND_BOTTOM;
            pos.flags &= ~SWP_NOZORDER;
            System.Runtime.InteropServices.Marshal.StructureToPtr(pos, lParam, false);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Returns the top-level window that hosts the desktop list view, and that list view's parent (SHELLDLL_DefView).
    /// Depending on the Windows build and whether a wallpaper slideshow ran, it lives under Progman or a WorkerW.
    /// </summary>
    private static (IntPtr Host, IntPtr DefView) FindDesktop()
    {
        var progman = FindWindow("Progman", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero)
            return (progman, defView);

        IntPtr host = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            var view = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view == IntPtr.Zero)
                return true;
            host = top;
            defView = view;
            return false;
        }, IntPtr.Zero);
        return (host == IntPtr.Zero ? progman : host, defView);
    }
}
