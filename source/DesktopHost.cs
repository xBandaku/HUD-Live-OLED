using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace HudWall
{
    /// <summary>
    /// Parents a window into Explorer's WorkerW layer so it renders as the
    /// desktop wallpaper (behind icons, above the static wallpaper).
    /// </summary>
    internal static class DesktopHost
    {
        private const uint WM_SPAWN_WORKER = 0x052C;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        public static void HideFromAltTab(IntPtr hwnd)
        {
            try
            {
                var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));
            }
            catch { }
        }

        private static IntPtr FindWorkerW()
        {
            IntPtr worker = IntPtr.Zero;
            EnumWindows((top, _) =>
            {
                if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    var w = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    if (w != IntPtr.Zero) worker = w;
                }
                return true;
            }, IntPtr.Zero);
            return worker;
        }

        /// <summary>Returns the parent actually used, or IntPtr.Zero on failure.</summary>
        public static IntPtr Attach(IntPtr hwnd, Rectangle bounds, out string detail)
        {
            detail = "";
            try
            {
                var progman = FindWindow("Progman", null);
                if (progman == IntPtr.Zero && GetShellWindow() != IntPtr.Zero)
                {
                    // Explorer restarted: ask it to rebuild the wallpaper host
                    progman = FindWindow("Progman", null);
                }

                // 0x052C with 0xD spawns the WorkerW that hosts wallpapers
                SendMessageTimeout(progman, WM_SPAWN_WORKER, new IntPtr(0xD), IntPtr.Zero, 0, 1000, out _);
                SendMessageTimeout(progman, WM_SPAWN_WORKER, new IntPtr(0xD), new IntPtr(1), 0, 1000, out _);

                var worker = FindWorkerW();
                if (worker == IntPtr.Zero) worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
                var parent = worker != IntPtr.Zero ? worker : progman;
                detail = $"progman=0x{progman.ToInt64():X} workerw=0x{worker.ToInt64():X} parent=0x{parent.ToInt64():X}";

                if (parent == IntPtr.Zero) return IntPtr.Zero;

                SetParent(hwnd, parent);
                SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                    SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                ShowWindow(hwnd, 5 /* SW_SHOW */);
                return parent;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return IntPtr.Zero;
            }
        }

        public static void Detach(IntPtr hwnd)
        {
            try { SetParent(hwnd, IntPtr.Zero); } catch { }
        }
    }
}
