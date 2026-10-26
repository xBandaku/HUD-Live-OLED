using System.Drawing;
using System.Runtime.InteropServices;

namespace HudWall;

internal sealed record TaskbarWindowState(Rectangle Bounds, bool OwnProcess, string WindowClass,
    bool MaximizedWithCaption, bool Minimized = false);

internal static class TaskbarLayout
{
    internal static int HeightForDpi(int dpi) => Math.Max(54, (int)Math.Round(62 * dpi / 96d));

    internal static Rectangle BottomBounds(Rectangle monitor, int height) =>
        new(monitor.Left, monitor.Bottom - height, monitor.Width, height);
}

internal static class TaskbarVisibility
{
    // GetWindowRect includes invisible resize borders, which can spill a
    // maximized app a few pixels onto the adjacent monitor.
    internal static Rectangle VisibleBounds(Rectangle bounds, Rectangle monitor, bool maximizedWithCaption) =>
        maximizedWithCaption ? Rectangle.Intersect(bounds, monitor) : bounds;

    internal static bool HasFullscreenOnMonitor(Rectangle monitor, IEnumerable<TaskbarWindowState> windows)
    {
        // Native snapshots are ordered from top to bottom. A normal app covering
        // a fullscreen window on this monitor takes precedence; focus elsewhere does not.
        foreach (var window in windows)
        {
            if (window.Minimized || window.OwnProcess || window.WindowClass is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") continue;
            if (!monitor.IntersectsWith(window.Bounds)) continue;
            return IsFullscreen(monitor, window.Bounds, false, window.WindowClass, window.MaximizedWithCaption);
        }
        return false;
    }

    internal static bool IsFullscreen(Rectangle monitor, Rectangle window, bool ownProcess,
        string windowClass, bool maximizedWithCaption)
    {
        if (ownProcess || maximizedWithCaption || windowClass is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        return window.Left <= monitor.Left + 1 && window.Top <= monitor.Top + 1 &&
            window.Right >= monitor.Right - 1 && window.Bottom >= monitor.Bottom - 1;
    }

    // Unknown membership must not destroy otherwise working taskbars.
    internal static bool NeedsDesktopRebuild(IEnumerable<bool?> membership) => membership.Any(current => current == false);
}

// Use the supported desktop-membership API only. New windows are created on the
// active desktop; never switch the user's desktop or move their application windows.
internal sealed class TaskbarDesktopMembership : IDisposable
{
    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr window, [MarshalAs(UnmanagedType.Bool)] out bool current);
        [PreserveSig] int GetWindowDesktopId(IntPtr window, out Guid desktop);
        [PreserveSig] int MoveWindowToDesktop(IntPtr window, ref Guid desktop);
    }

    private IVirtualDesktopManager _manager;
    private DateTime _retryAfter;

    internal bool? IsCurrent(IntPtr window)
    {
        if (window == IntPtr.Zero || DateTime.UtcNow < _retryAfter) return null;
        try
        {
            _manager ??= (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
            int result = _manager.IsWindowOnCurrentVirtualDesktop(window, out bool current);
            if (result < 0) Marshal.ThrowExceptionForHR(result);
            return current;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or PlatformNotSupportedException)
        {
            Dispose();
            _retryAfter = DateTime.UtcNow.AddSeconds(5);
            return null;
        }
    }

    public void Dispose()
    {
        if (_manager != null) { Marshal.ReleaseComObject(_manager); _manager = null; }
    }
}
