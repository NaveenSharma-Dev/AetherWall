using System;
using System.Runtime.InteropServices;

namespace AetherWall.Native
{
    public readonly record struct MonitorBounds(
        int Left,
        int Top,
        int Width,
        int Height,
        bool IsPrimary,
        string DeviceName);

    internal static class WallpaperNative
    {
        private const uint WM_SPAWN_WORKER = 0x052C;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_NORMAL = 0x0000;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;

        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_HIDE = 0;
        private const int GWL_STYLE = -16;
        private const long WS_CHILD = 0x40000000L;
        private const long WS_POPUP = 0x80000000L;
        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint MONITORINFOF_PRIMARY = 1;

        private delegate bool EnumWindowsProc(
            IntPtr hWnd,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int Size;
            public RECT Monitor;
            public RECT Work;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        private delegate bool MonitorEnumProc(
            IntPtr monitorHandle,
            IntPtr deviceContext,
            ref RECT monitorRect,
            IntPtr data);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(
            string? lpClassName,
            string? lpWindowName);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(
            IntPtr hWndParent,
            IntPtr hWndChildAfter,
            string? lpszClass,
            string? lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(
            EnumWindowsProc lpEnumFunc,
            IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(
            IntPtr deviceContext,
            IntPtr clipRect,
            MonitorEnumProc callback,
            IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW", ExactSpelling = true)]
        private static extern bool GetMonitorInfo(
            IntPtr monitorHandle,
            ref MONITORINFOEX monitorInfo);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr hWnd,
            out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(
            IntPtr hWnd,
            System.Text.StringBuilder className,
            int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(
            IntPtr hWndChild,
            IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(
            IntPtr hWnd,
            int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(
            IntPtr hWnd,
            IntPtr updateRect,
            IntPtr updateRegion,
            uint flags);

        public static void RefreshDesktop()
        {
            IntPtr settingName =
                Marshal.StringToHGlobalUni("Control Panel\\Desktop");

            try
            {
                SendMessageTimeout(
                    new IntPtr(0xffff),
                    WM_SETTINGCHANGE,
                    new IntPtr(SPI_SETDESKWALLPAPER),
                    settingName,
                    SMTO_ABORTIFHUNG,
                    1000,
                    out _);
            }
            finally
            {
                Marshal.FreeHGlobal(settingName);
            }

            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                RedrawWindow(
                    progman,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    RDW_INVALIDATE |
                    RDW_ERASE |
                    RDW_ALLCHILDREN |
                    RDW_UPDATENOW);
            }

            RedrawWindow(
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_INVALIDATE |
                RDW_ERASE |
                RDW_ALLCHILDREN |
                RDW_UPDATENOW);
        }

        public static void RefreshDesktopHost(IntPtr hostHandle)
        {
            if (hostHandle == IntPtr.Zero)
                return;

            RedrawWindow(
                hostHandle,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_INVALIDATE |
                RDW_ERASE |
                RDW_ALLCHILDREN |
                RDW_UPDATENOW);
        }

        public static IntPtr GetWindowParent(IntPtr windowHandle) =>
            windowHandle == IntPtr.Zero
                ? IntPtr.Zero
                : GetParent(windowHandle);

        public static IntPtr GetWindowStyle(IntPtr windowHandle) =>
            windowHandle == IntPtr.Zero
                ? IntPtr.Zero
                : GetWindowLongPtr(windowHandle, GWL_STYLE);

        public static void DetachFromDesktop(
            IntPtr wallpaperHandle,
            IntPtr originalParent,
            IntPtr originalStyle)
        {
            if (wallpaperHandle == IntPtr.Zero)
                return;

            ShowWindow(wallpaperHandle, SW_HIDE);
            SetParent(wallpaperHandle, originalParent);
            SetWindowLongPtr(wallpaperHandle, GWL_STYLE, originalStyle);
            SetWindowPos(
                wallpaperHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static IntPtr AttachToDesktop(
            IntPtr wallpaperHandle,
            MonitorBounds monitor)
        {
            if (wallpaperHandle == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr progman =
                FindWindow("Progman", null);

            if (progman == IntPtr.Zero)
                return IntPtr.Zero;

            SendMessageTimeout(
                progman,
                WM_SPAWN_WORKER,
                IntPtr.Zero,
                IntPtr.Zero,
                SMTO_NORMAL,
                1000,
                out _);

            IntPtr workerW =
                FindWorkerWindow(monitor);

            if (workerW == IntPtr.Zero)
            {
                Services.StartupLogService.WriteDiagnostic(
                    $"Could not locate the Windows desktop host for {monitor.DeviceName} ({monitor.Width}x{monitor.Height}).");
                return IntPtr.Zero;
            }

            IntPtr originalStyle = GetWindowLongPtr(wallpaperHandle, GWL_STYLE);
            long childStyle = (originalStyle.ToInt64() & ~WS_POPUP) | WS_CHILD;
            SetWindowLongPtr(
                wallpaperHandle,
                GWL_STYLE,
                new IntPtr(childStyle));
            SetParent(
                wallpaperHandle,
                workerW);

            SetWindowPos(
                wallpaperHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_NOACTIVATE | SWP_FRAMECHANGED);

            if (GetParent(wallpaperHandle) != workerW)
            {
                Services.StartupLogService.WriteDiagnostic(
                    $"Windows rejected attaching the wallpaper window to the desktop host for {monitor.DeviceName}.");
                return IntPtr.Zero;
            }

            return workerW;
        }

        private static IntPtr FindPrimaryWorkerWindow()
        {
            IntPtr workerW = IntPtr.Zero;

            EnumWindows(
                (hWnd, lParam) =>
                {
                    IntPtr shellView =
                        FindWindowEx(
                            hWnd,
                            IntPtr.Zero,
                            "SHELLDLL_DefView",
                            null);

                    if (shellView != IntPtr.Zero)
                    {
                        workerW =
                            FindWindowEx(
                                IntPtr.Zero,
                                hWnd,
                                "WorkerW",
                                null);
                    }

                    return workerW == IntPtr.Zero;
                },
                IntPtr.Zero);

            return workerW;
        }

        private static IntPtr FindWorkerWindow(MonitorBounds monitor)
        {
            IntPtr preferredWorker = FindPrimaryWorkerWindow();
            if (WindowContainsMonitor(preferredWorker, monitor))
                return preferredWorker;

            IntPtr bestWorker = IntPtr.Zero;
            long bestOverlap = 0;

            EnumWindows(
                (hWnd, lParam) =>
                {
                    var className = new System.Text.StringBuilder(32);
                    GetClassName(hWnd, className, className.Capacity);
                    if (!string.Equals(
                            className.ToString(),
                            "WorkerW",
                            StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (FindWindowEx(
                            hWnd,
                            IntPtr.Zero,
                            "SHELLDLL_DefView",
                            null) != IntPtr.Zero ||
                        !GetWindowRect(hWnd, out RECT rect))
                    {
                        return true;
                    }

                    long overlap = GetMonitorOverlap(rect, monitor);
                    if (WindowContainsMonitor(rect, monitor))
                    {
                        bestWorker = hWnd;
                        bestOverlap = long.MaxValue;
                        return false;
                    }

                    if (overlap > bestOverlap)
                    {
                        bestWorker = hWnd;
                        bestOverlap = overlap;
                    }

                    return true;
                },
                IntPtr.Zero);

            return bestWorker != IntPtr.Zero
                ? bestWorker
                : preferredWorker;
        }

        private static bool WindowContainsMonitor(
            IntPtr windowHandle,
            MonitorBounds monitor)
        {
            return windowHandle != IntPtr.Zero &&
                   GetWindowRect(windowHandle, out RECT rect) &&
                   WindowContainsMonitor(rect, monitor);
        }

        private static bool WindowContainsMonitor(
            RECT rect,
            MonitorBounds monitor)
        {
            return rect.Left <= monitor.Left &&
                   rect.Top <= monitor.Top &&
                   rect.Right >= monitor.Left + monitor.Width &&
                   rect.Bottom >= monitor.Top + monitor.Height;
        }

        private static long GetMonitorOverlap(
            RECT rect,
            MonitorBounds monitor)
        {
            int left = Math.Max(rect.Left, monitor.Left);
            int top = Math.Max(rect.Top, monitor.Top);
            int right = Math.Min(rect.Right, monitor.Left + monitor.Width);
            int bottom = Math.Min(rect.Bottom, monitor.Top + monitor.Height);

            return right <= left || bottom <= top
                ? 0
                : (long)(right - left) * (bottom - top);
        }

        public static void SetBounds(
            IntPtr wallpaperHandle,
            IntPtr parentHandle,
            MonitorBounds monitor)
        {
            if (wallpaperHandle == IntPtr.Zero ||
                parentHandle == IntPtr.Zero)
            {
                return;
            }

            if (!GetWindowRect(parentHandle, out RECT parentRect))
            {
                return;
            }

            SetWindowPos(
                wallpaperHandle,
                IntPtr.Zero,
                monitor.Left - parentRect.Left,
                monitor.Top - parentRect.Top,
                monitor.Width,
                monitor.Height,
                SWP_NOACTIVATE |
                SWP_SHOWWINDOW);

            ShowWindow(
                wallpaperHandle,
                SW_SHOWNOACTIVATE);
        }

        public static void PositionIdentifyOverlay(
            IntPtr overlayHandle,
            MonitorBounds monitor)
        {
            if (overlayHandle == IntPtr.Zero)
                return;

            SetWindowPos(
                overlayHandle,
                new IntPtr(-1),
                monitor.Left,
                monitor.Top,
                monitor.Width,
                monitor.Height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);

            ShowWindow(overlayHandle, SW_SHOWNOACTIVATE);
        }

        public static string? GetAspectRatio(MonitorBounds monitor)
        {
            int width = monitor.Width;
            int height = monitor.Height;
            if (width <= 0 || height <= 0)
                return null;

            int divisor = GreatestCommonDivisor(width, height);
            return string.Concat(width / divisor, ":", height / divisor);
        }

        public static System.Collections.Generic.IReadOnlyList<MonitorBounds> GetMonitors()
        {
            var monitors =
                new System.Collections.Generic.List<MonitorBounds>();

            MonitorEnumProc callback =
                delegate(
                    IntPtr monitorHandle,
                    IntPtr deviceContext,
                    ref RECT monitorRect,
                    IntPtr data)
                {
                    var info = new MONITORINFOEX
                    {
                        Size = Marshal.SizeOf<MONITORINFOEX>(),
                        DeviceName = string.Empty
                    };

                    if (GetMonitorInfo(monitorHandle, ref info))
                    {
                        RECT rect = info.Monitor;
                        monitors.Add(new MonitorBounds(
                            rect.Left,
                            rect.Top,
                            rect.Right - rect.Left,
                            rect.Bottom - rect.Top,
                            (info.Flags & MONITORINFOF_PRIMARY) != 0,
                            info.DeviceName));
                    }

                    return true;
                };

            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                callback,
                IntPtr.Zero);

            if (monitors.Count == 0)
            {
                int width = GetSystemMetrics(SM_CXSCREEN);
                int height = GetSystemMetrics(SM_CYSCREEN);
                if (width > 0 && height > 0)
                    monitors.Add(new MonitorBounds(0, 0, width, height, true, "DISPLAY1"));
            }

            monitors.Sort((left, right) =>
            {
                int primaryOrder = right.IsPrimary.CompareTo(left.IsPrimary);
                if (primaryOrder != 0)
                    return primaryOrder;

                int topOrder = left.Top.CompareTo(right.Top);
                return topOrder != 0
                    ? topOrder
                    : left.Left.CompareTo(right.Left);
            });

            return monitors;
        }

        private static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                int remainder = left % right;
                left = right;
                right = remainder;
            }

            return left;
        }
    }
}
