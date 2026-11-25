using AetherWall.Native;
using AetherWall.Services;
using System;
using System.Windows;
using System.Windows.Interop;

namespace AetherWall.Views
{
    public partial class WallpaperWindow : Window
    {
        private readonly PlaybackService _playbackService;

        private IntPtr _windowHandle;
        private IntPtr _workerW;
        private IntPtr _originalParent;
        private IntPtr _originalStyle;
        private bool _isDetached;
        private AetherWall.Models.WallpaperDisplayMode _displayMode;
        private readonly MonitorBounds _monitor;
        private string? _targetAspectRatio;

        public WallpaperWindow(
            PlaybackService playbackService,
            AetherWall.Models.WallpaperDisplayMode displayMode,
            MonitorBounds monitor)
        {
            InitializeComponent();

            _playbackService = playbackService;
            _displayMode = displayMode;
            _monitor = monitor;

            VideoView.MediaPlayer =
                _playbackService.MediaPlayer;

            Loaded += WallpaperWindow_Loaded;
        }

        private void WallpaperWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _windowHandle =
                new WindowInteropHelper(this).Handle;

            if (_windowHandle == IntPtr.Zero)
                return;

            _originalParent =
                WallpaperNative.GetWindowParent(_windowHandle);
            _originalStyle =
                WallpaperNative.GetWindowStyle(_windowHandle);

            _workerW =
                WallpaperNative.AttachToDesktop(
                    _windowHandle,
                    _monitor);

            _isDetached = false;

            if (_workerW == IntPtr.Zero)
            {
                StartupLogService.WriteDiagnostic(
                    $"Wallpaper window failed to attach to the desktop for {_monitor.DeviceName} ({_monitor.Width}x{_monitor.Height}).");
                return;
            }

            WallpaperNative.SetBounds(
                _windowHandle,
                _workerW,
                _monitor);

            _targetAspectRatio =
                WallpaperNative.GetAspectRatio(_monitor);
            ApplyDisplayMode();
        }

        public void DetachFromDesktop()
        {
            if (_isDetached)
                return;

            if (IsVisible)
                Hide();

            if (_windowHandle != IntPtr.Zero && _workerW != IntPtr.Zero)
            {
                WallpaperNative.DetachFromDesktop(
                    _windowHandle,
                    _originalParent,
                    _originalStyle);
            }

            _isDetached = true;
        }

        public void RefreshDesktopHost() =>
            WallpaperNative.RefreshDesktopHost(_workerW);

        public void SetDisplayMode(
            AetherWall.Models.WallpaperDisplayMode displayMode)
        {
            _displayMode = displayMode;
            ApplyDisplayMode();
        }

        private void ApplyDisplayMode()
        {
            if (!string.IsNullOrWhiteSpace(_targetAspectRatio))
            {
                _playbackService.SetDisplayMode(
                    _displayMode,
                    _targetAspectRatio);
            }
        }
    }
}
