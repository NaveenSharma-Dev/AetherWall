using AetherWall.Models;
using AetherWall.Views;
using AetherWall.Native;
using System;
using System.IO;
using System.Linq;

namespace AetherWall.Services
{
    public class WallpaperService : IDisposable
    {
        private sealed class WallpaperOutput
        {
            public PlaybackService PlaybackService { get; }
            public WallpaperWindow Window { get; }
            public AetherWall.Native.MonitorBounds Monitor { get; }
            public bool IsPrimary { get; }
            public bool IsPaused { get; set; }
            public bool IsManuallyPaused { get; set; }

            public WallpaperOutput(
                PlaybackService playbackService,
                WallpaperWindow window,
                MonitorBounds monitor,
                bool isPrimary)
            {
                PlaybackService = playbackService;
                Window = window;
                Monitor = monitor;
                IsPrimary = isPrimary;
            }
        }

        private readonly PlaybackService _playbackService;
        private readonly PlaybackStateManager _stateManager;
        private readonly BatteryMonitor _batteryMonitor;
        private readonly ConfigService _configService;
        private readonly AppSettings _settings;

        private readonly System.Collections.Generic.List<WallpaperOutput>
            _wallpaperOutputs = new();

        private bool _disposed;
        private bool _closingOutputs;
        private bool _wasPrimaryPlaylist;

        public PlaybackStateManager StateManager =>
            _stateManager;

        public string? CurrentFilePath =>
            _playbackService.CurrentFilePath ??
            _wallpaperOutputs.FirstOrDefault(output =>
                output.PlaybackService.CurrentFilePath != null)?.PlaybackService.CurrentFilePath;

        public event EventHandler? PlaybackInfoChanged;

        public bool IsPlayingFile(string filePath) =>
            _wallpaperOutputs.Any(output =>
                output.PlaybackService.IsPlayingFile(filePath));

        public bool IsPlaylistPlayback =>
            _playbackService.IsPlaylistPlayback;

        public WallpaperDisplayMode DisplayMode =>
            _settings.WallpaperDisplayMode;

        public WallpaperService(
            AppSettings settings,
            PlaybackService playbackService)
        {
            _configService = new ConfigService();
            _settings = settings;
            _settings.MonitorDisplayModes ??= new();

            _playbackService = playbackService;
            _playbackService.PlaybackInfoChanged +=
                OnPlaybackInfoChanged;

            _stateManager = new PlaybackStateManager(
                _playbackService,
                _settings);

            _stateManager.Start();

            _batteryMonitor = new BatteryMonitor();

            _batteryMonitor.BatteryStateChanged +=
                OnBatteryStateChanged;
        }
        private void OnBatteryStateChanged(
            object? sender,
            bool isOnBattery)
        {
            _stateManager.SetBatteryPaused(
                isOnBattery);
        }

        private void OnPlaybackInfoChanged(
            object? sender,
            EventArgs e)
        {
            if (_closingOutputs)
                return;

            void UpdateOutputsAndNotify()
            {
                SynchronizeSecondaryOutputs();
                PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
            }

            var dispatcher =
                System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null || dispatcher.CheckAccess())
                UpdateOutputsAndNotify();
            else
                dispatcher.BeginInvoke(
                    new Action(UpdateOutputsAndNotify));
        }

        private void CreateWallpaperOutputs(
            IEnumerable<string>? deviceNames = null)
        {
            IReadOnlyList<MonitorBounds> monitors =
                WallpaperNative.GetMonitors();
            HashSet<string>? requestedDevices = deviceNames == null
                ? null
                : new HashSet<string>(
                    deviceNames,
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < monitors.Count; index++)
            {
                if (requestedDevices != null &&
                    !requestedDevices.Contains(monitors[index].DeviceName))
                {
                    continue;
                }

                bool isPrimary = monitors[index].IsPrimary ||
                    (index == 0 && !monitors.Any(monitor => monitor.IsPrimary));
                PlaybackService playbackService = isPrimary
                    ? _playbackService
                    : new PlaybackService();

                if (!isPrimary)
                {
                    playbackService.SetAudioEnabled(false);
                    playbackService.SetVolume(_settings.Volume);
                }

                WallpaperDisplayMode displayMode =
                    _settings.MonitorDisplayModes.TryGetValue(
                        monitors[index].DeviceName,
                        out WallpaperDisplayMode savedDisplayMode)
                        ? savedDisplayMode
                        : _settings.WallpaperDisplayMode;

                var window = new WallpaperWindow(
                    playbackService,
                    displayMode,
                    monitors[index]);

                _wallpaperOutputs.Add(new WallpaperOutput(
                    playbackService,
                    window,
                    monitors[index],
                    isPrimary));
                window.Show();
            }

            UpdateAudioRouting();
        }

        private void SynchronizeSecondaryOutputs()
        {
            string? currentFilePath = _playbackService.CurrentFilePath;
            bool isPrimaryPlaylist = _playbackService.IsPlaylistPlayback;
            bool shouldPauseGlobally =
                _stateManager.CurrentState != PlaybackState.Playing;

            foreach (WallpaperOutput output in _wallpaperOutputs)
            {
                if (!output.IsPrimary &&
                    string.IsNullOrWhiteSpace(currentFilePath))
                {
                    if (_wasPrimaryPlaylist &&
                        output.PlaybackService.CurrentFilePath != null)
                    {
                        output.PlaybackService.Stop();
                        output.IsPaused = false;
                        output.IsManuallyPaused = false;
                        continue;
                    }
                }

                if (!output.IsPrimary &&
                    !string.IsNullOrWhiteSpace(currentFilePath) &&
                    isPrimaryPlaylist)
                {
                    bool fileChanged = !string.Equals(
                        output.PlaybackService.CurrentFilePath,
                        currentFilePath,
                        StringComparison.OrdinalIgnoreCase);

                    if (fileChanged)
                    {
                        output.PlaybackService.Play(currentFilePath);
                        output.IsPaused = false;
                    }
                }

                bool shouldPause =
                    shouldPauseGlobally || output.IsManuallyPaused;
                SetOutputPaused(output, shouldPause);
            }

            _wasPrimaryPlaylist = isPrimaryPlaylist;
        }

        private static void SetOutputPaused(
            WallpaperOutput output,
            bool shouldPause)
        {
            if (output.IsPaused == shouldPause)
                return;

            output.IsPaused = shouldPause;
            if (shouldPause)
                output.PlaybackService.Pause();
            else
                output.PlaybackService.Resume();
        }
        public void Show(string videoPath)
        {
            if (_disposed)
                return;

            if (!File.Exists(videoPath))
                return;

            CloseWallpaperOutputs(refreshDesktop: false);
            CreateWallpaperOutputs();

            foreach (WallpaperOutput output in _wallpaperOutputs)
            {
                output.PlaybackService.Play(videoPath);
                output.IsPaused = false;
                output.IsManuallyPaused = false;
            }

            UpdateAudioRouting();
            _stateManager.ReapplyCurrentState();
        }

        public void ShowPlaylist(IReadOnlyList<string> filePaths)
        {
            if (_disposed)
                return;

            string[] availablePaths =
                filePaths.Where(File.Exists).ToArray();

            if (availablePaths.Length == 0)
                return;

            CloseWallpaperOutputs(refreshDesktop: false);
            CreateWallpaperOutputs();

            _playbackService.PlayPlaylist(availablePaths);
            _stateManager.ReapplyCurrentState();
        }

        public void ApplyMonitorWallpapers(
            IReadOnlyDictionary<string, string> filePathsByMonitor)
        {
            if (_disposed)
                return;

            var availableWallpapers = filePathsByMonitor
                .Where(entry => File.Exists(entry.Value))
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value,
                    StringComparer.OrdinalIgnoreCase);

            if (availableWallpapers.Count == 0)
            {
                Hide();
                return;
            }

            CloseWallpaperOutputs(refreshDesktop: false);
            CreateWallpaperOutputs(availableWallpapers.Keys);

            foreach (WallpaperOutput output in _wallpaperOutputs)
            {
                if (!availableWallpapers.TryGetValue(
                        output.Monitor.DeviceName,
                        out string? filePath))
                {
                    continue;
                }

                output.PlaybackService.Play(filePath);
                output.IsPaused = false;
            }

            UpdateAudioRouting();
            _stateManager.ReapplyCurrentState();
            SynchronizeSecondaryOutputs();
            PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetMonitorWallpaper(
            string deviceName,
            string filePath)
        {
            if (_disposed || !File.Exists(filePath))
                return;

            WallpaperOutput? output = _wallpaperOutputs.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Monitor.DeviceName,
                    deviceName,
                    StringComparison.OrdinalIgnoreCase));

            if (output == null)
            {
                CreateWallpaperOutputs(new[] { deviceName });
                output = _wallpaperOutputs.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.Monitor.DeviceName,
                        deviceName,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (_playbackService.IsPlaylistPlayback &&
                !string.IsNullOrWhiteSpace(_playbackService.CurrentFilePath))
            {
                _playbackService.Play(_playbackService.CurrentFilePath);
            }

            if (output == null)
                return;

            output.PlaybackService.Play(filePath);
            output.IsPaused = false;
            output.IsManuallyPaused = false;

            if (output.IsPrimary)
                _stateManager.ReapplyCurrentState();
            else if (_stateManager.CurrentState != PlaybackState.Playing)
            {
                output.PlaybackService.Pause();
                output.IsPaused = true;
            }

            UpdateAudioRouting();
            PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        public (bool IsActive, bool IsPaused) GetMonitorPlaybackState(
            string deviceName,
            string filePath)
        {
            WallpaperOutput? output = _wallpaperOutputs.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Monitor.DeviceName,
                    deviceName,
                    StringComparison.OrdinalIgnoreCase));

            bool isActive = output != null &&
                output.PlaybackService.IsPlayingFile(filePath);
            return (isActive, isActive && output!.IsPaused);
        }

        public void ToggleMonitorPlayback(
            string deviceName,
            string filePath)
        {
            if (_disposed || !File.Exists(filePath))
                return;

            WallpaperOutput? output = _wallpaperOutputs.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Monitor.DeviceName,
                    deviceName,
                    StringComparison.OrdinalIgnoreCase));

            if (output == null)
            {
                CreateWallpaperOutputs(new[] { deviceName });
                output = _wallpaperOutputs.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.Monitor.DeviceName,
                        deviceName,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (output == null)
                return;

            bool sameWallpaper =
                output.PlaybackService.IsPlayingFile(filePath);
            bool globallyPaused =
                _stateManager.CurrentState != PlaybackState.Playing;

            if (!sameWallpaper)
            {
                output.IsManuallyPaused = false;
                output.IsPaused = false;
                output.PlaybackService.Play(filePath);
                SetOutputPaused(output, globallyPaused);
            }
            else if (!globallyPaused)
            {
                output.IsManuallyPaused = !output.IsManuallyPaused;
                SetOutputPaused(output, output.IsManuallyPaused);
            }

            UpdateAudioRouting();
            PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetAudioEnabled(bool enabled)
        {
            _settings.AudioEnabled = enabled;
            _stateManager.SetAudioEnabled(enabled);
            UpdateAudioRouting();
        }

        public void SetVolume(int volume)
        {
            int clampedVolume = Math.Clamp(volume, 0, 100);
            _settings.Volume = clampedVolume;
            _stateManager.SetVolume(clampedVolume);

            foreach (WallpaperOutput output in _wallpaperOutputs)
                output.PlaybackService.SetVolume(clampedVolume);

            UpdateAudioRouting();
        }

        private void UpdateAudioRouting()
        {
            if (_wallpaperOutputs.Count == 0)
                return;

            WallpaperOutput? audioOutput = _wallpaperOutputs
                .FirstOrDefault(output => output.IsPrimary &&
                    output.PlaybackService.CurrentFilePath != null)
                ?? _wallpaperOutputs.FirstOrDefault(output =>
                    output.PlaybackService.CurrentFilePath != null);

            foreach (WallpaperOutput output in _wallpaperOutputs)
            {
                output.PlaybackService.SetVolume(_settings.Volume);
                output.PlaybackService.SetAudioEnabled(
                    _settings.AudioEnabled && ReferenceEquals(output, audioOutput));
            }
        }

        public void SetDisplayMode(WallpaperDisplayMode displayMode)
        {
            bool changed = _settings.WallpaperDisplayMode != displayMode ||
                _settings.MonitorDisplayModes.Values.Any(mode => mode != displayMode) ||
                _wallpaperOutputs.Any(output =>
                    !_settings.MonitorDisplayModes.TryGetValue(
                        output.Monitor.DeviceName,
                        out WallpaperDisplayMode savedMode) ||
                    savedMode != displayMode);
            _settings.WallpaperDisplayMode = displayMode;

            foreach (WallpaperOutput output in _wallpaperOutputs)
                _settings.MonitorDisplayModes[output.Monitor.DeviceName] = displayMode;

            if (changed)
                _configService.Save(_settings);

            foreach (WallpaperOutput output in _wallpaperOutputs)
                output.Window.SetDisplayMode(displayMode);
        }

        public void SetMonitorDisplayMode(
            string deviceName,
            WallpaperDisplayMode displayMode)
        {
            WallpaperOutput? output = _wallpaperOutputs.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Monitor.DeviceName,
                    deviceName,
                    StringComparison.OrdinalIgnoreCase));

            output?.Window.SetDisplayMode(displayMode);
        }

        public void UpdatePlaylist(IReadOnlyList<string> filePaths)
        {
            if (_disposed || !_playbackService.IsPlaylistPlayback)
                return;

            string[] availablePaths =
                filePaths.Where(File.Exists).ToArray();

            if (availablePaths.Length == 0)
            {
                Hide();
                return;
            }

            _playbackService.UpdatePlaylist(availablePaths);
        }

        public void StopPlaylist()
        {
            if (_playbackService.IsPlaylistPlayback)
                Hide();
        }

        public void Hide()
        {
            if (_disposed)
                return;

            CloseWallpaperOutputs();
        }

        private void CloseWallpaperOutputs(bool refreshDesktop = true)
        {
            _closingOutputs = true;
            try
            {
                foreach (WallpaperOutput output in _wallpaperOutputs)
                    output.Window.DetachFromDesktop();

                foreach (WallpaperOutput output in _wallpaperOutputs)
                    output.Window.Close();

                foreach (WallpaperOutput output in _wallpaperOutputs)
                    output.PlaybackService.Stop();

                foreach (WallpaperOutput output in _wallpaperOutputs)
                {
                    if (!output.IsPrimary)
                        output.PlaybackService.Dispose();
                }

                if (refreshDesktop)
                {
                    foreach (WallpaperOutput output in _wallpaperOutputs)
                        output.Window.RefreshDesktopHost();
                }

                if (_wallpaperOutputs.Count == 0)
                    _playbackService.Stop();

                _wallpaperOutputs.Clear();
                _wasPrimaryPlaylist = false;
            }
            finally
            {
                _closingOutputs = false;
            }

            if (refreshDesktop)
                WallpaperNative.RefreshDesktop();

            if (!_disposed)
                PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _playbackService.PlaybackInfoChanged -=
                    OnPlaybackInfoChanged;

                CloseWallpaperOutputs();
            }
            finally
            {
                _stateManager.Dispose();
                _playbackService.Dispose();
                _batteryMonitor.Dispose();
            }
        }
    }
}
