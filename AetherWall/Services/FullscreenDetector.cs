
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace AetherWall.Services
{
    public class FullscreenDetector : IDisposable
    {
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly DispatcherTimer _fallbackTimer;
        private readonly WinEventDelegate _winEventDelegate;
        private IntPtr _foregroundHook;
        private IntPtr _moveSizeHook;
        private uint _cachedProcessId;
        private string _cachedProcessName = string.Empty;
        private bool _isFullscreen;
        private bool _disposed;

        public bool IsFullscreen => _isFullscreen;

        public event EventHandler<bool>? FullscreenChanged;

        public FullscreenDetector()
        {
            _fallbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            _fallbackTimer.Tick += CheckFullscreen;
            _winEventDelegate = OnForegroundWindowChanged;
        }

        public void Start()
        {
            if (_disposed)
                return;

            if (_foregroundHook != IntPtr.Zero ||
                _moveSizeHook != IntPtr.Zero ||
                _fallbackTimer.IsEnabled)
            {
                return;
            }

            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            _moveSizeHook = SetWinEventHook(
                EVENT_SYSTEM_MOVESIZEEND,
                EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            if (_foregroundHook == IntPtr.Zero ||
                _moveSizeHook == IntPtr.Zero)
            {
                _fallbackTimer.Start();
            }

            CheckFullscreen(null, EventArgs.Empty);
        }

        public void Stop()
        {
            _fallbackTimer.Stop();

            if (_foregroundHook == IntPtr.Zero)
            {
                if (_moveSizeHook != IntPtr.Zero)
                {
                    UnhookWinEvent(_moveSizeHook);
                    _moveSizeHook = IntPtr.Zero;
                }

                return;
            }

            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;

            if (_moveSizeHook != IntPtr.Zero)
            {
                UnhookWinEvent(_moveSizeHook);
                _moveSizeHook = IntPtr.Zero;
            }
        }

        private void OnForegroundWindowChanged(
            IntPtr hook,
            uint eventType,
            IntPtr window,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime)
        {
            if (_disposed ||
                (eventType != EVENT_SYSTEM_FOREGROUND &&
                 eventType != EVENT_SYSTEM_MOVESIZEEND))
            {
                return;
            }

            if (eventType == EVENT_SYSTEM_MOVESIZEEND &&
                (window == IntPtr.Zero || window != GetForegroundWindow()))
            {
                return;
            }

            if (_dispatcher.CheckAccess())
                CheckFullscreen(null, EventArgs.Empty);
            else if (!_dispatcher.HasShutdownStarted &&
                     !_dispatcher.HasShutdownFinished)
            {
                _dispatcher.BeginInvoke(
                    new Action(() => CheckFullscreen(null, EventArgs.Empty)));
            }
        }

        private void CheckFullscreen(object? sender, EventArgs e)
        {
            bool detected = DetectMaximizedOrFullscreenWindow();

            if (_isFullscreen == detected)
                return;

            _isFullscreen = detected;

            Debug.WriteLine(
                detected
                    ? "[AetherWall] Maximized/fullscreen window detected -> Pausing wallpaper"
                    : "[AetherWall] No maximized/fullscreen window detected -> Resuming wallpaper");

            FullscreenChanged?.Invoke(this, detected);
        }

        private bool DetectMaximizedOrFullscreenWindow()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero ||
                !IsWindowVisible(foregroundWindow) ||
                IsIconic(foregroundWindow) ||
                IsIgnoredWindow(foregroundWindow))
            {
                return false;
            }

            GetWindowThreadProcessId(
                foregroundWindow,
                out uint processId);

            if (processId == 0 || processId == (uint)Environment.ProcessId)
                return false;

            string processName = GetProcessName(processId);
            if (string.Equals(
                    processName,
                    "TextInputHost",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsZoomed(foregroundWindow) ||
                CoversMonitor(foregroundWindow);
        }

        private string GetProcessName(uint processId)
        {
            if (_cachedProcessId == processId)
                return _cachedProcessName;

            _cachedProcessId = processId;
            try
            {
                using Process process =
                    Process.GetProcessById((int)processId);

                _cachedProcessName = process.ProcessName;
            }
            catch
            {
                // The foreground process may exit while it is being checked.
                _cachedProcessName = "Unknown";
            }

            return _cachedProcessName;
        }

        private static bool CoversMonitor(IntPtr hWnd)
        {
            if (!GetWindowRect(hWnd, out RECT windowRect))
                return false;

            IntPtr monitor = MonitorFromWindow(
                hWnd,
                MONITOR_DEFAULTTONEAREST);

            if (monitor == IntPtr.Zero)
                return false;

            var info = new MONITORINFO
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };

            if (!GetMonitorInfo(monitor, ref info))
                return false;

            RECT bounds = info.rcMonitor;

            return windowRect.Left <= bounds.Left &&
                   windowRect.Top <= bounds.Top &&
                   windowRect.Right >= bounds.Right &&
                   windowRect.Bottom >= bounds.Bottom;
        }

        private static bool IsIgnoredWindow(IntPtr hWnd)
        {
            int style = GetWindowLong(hWnd, GWL_EXSTYLE);

            return (style & WS_EX_TOOLWINDOW) != 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
            _fallbackTimer.Tick -= CheckFullscreen;
        }

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private delegate void WinEventDelegate(
            IntPtr hook,
            uint eventType,
            IntPtr window,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr module,
            WinEventDelegate callback,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr hWnd,
            out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(
            IntPtr hWnd,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(
            IntPtr monitor,
            ref MONITORINFO info);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(
            IntPtr hWnd,
            int index);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
    }
}
