using System;
using AetherWall.Models;

namespace AetherWall.Services
{
    public enum PlaybackState
    {
        Playing,
        PausedFullscreen,
        PausedBattery,
        PausedManual
    }

    public class PlaybackStateManager : IDisposable
    {
        private readonly PlaybackService _playbackService;
        private readonly FullscreenDetector _fullscreenDetector;
        private readonly AppSettings _settings;

        private bool _fullscreenPaused;
        private bool _batteryPaused;
        private bool _manualPaused;
      
        public PlaybackState CurrentState { get; private set; }
            = PlaybackState.Playing;

        public bool IsManuallyPaused =>
            _manualPaused;

        public PlaybackStateManager(
            PlaybackService playbackService,
            AppSettings settings)
        {
            _playbackService = playbackService;
            _settings = settings
                ?? throw new ArgumentNullException(nameof(settings));

            _playbackService.SetAudioEnabled(_settings.AudioEnabled);
            _playbackService.SetVolume(_settings.Volume);

            _fullscreenDetector = new FullscreenDetector();

            _fullscreenDetector.FullscreenChanged +=
                OnFullscreenChanged;
        }

        public void Start()
        {
            _fullscreenDetector.Start();
        }

        private void OnFullscreenChanged(
            object? sender,
            bool isFullscreen)
        {
            SetFullscreenPaused(
                isFullscreen);
        }

        public void SetFullscreenPaused(
            bool paused)
        {
            _fullscreenPaused = paused;
            UpdatePlaybackState();
        }

        public void SetBatteryPaused(
            bool paused)
        {
            _batteryPaused = paused;
            UpdatePlaybackState();
        }

        public void SetManualPaused(
            bool paused)
        {
            _manualPaused = paused;
            UpdatePlaybackState();
        }

        public void ToggleManualPause()
        {
            SetManualPaused(!_manualPaused);
        }

        public void ReapplyCurrentState()
        {
            ApplyPlaybackState();
        }

        public void SetPauseOnBattery(bool enabled)
        {
            _settings.PauseOnBattery = enabled;
            UpdatePlaybackState();
        }

        public void SetPauseOnFullscreen(bool enabled)
        {
            _settings.PauseOnFullscreen = enabled;
            UpdatePlaybackState();
        }

        public void SetAudioEnabled(bool enabled)
        {
            _playbackService.SetAudioEnabled(enabled);
        }

        public void SetVolume(int volume)
        {
            _playbackService.SetVolume(volume);
        }

        private void UpdatePlaybackState()
        {
            PlaybackState newState;

            if (_manualPaused)
            {
                newState =
                    PlaybackState.PausedManual;
            }
            else if (_fullscreenPaused && _settings.PauseOnFullscreen)
            {
                newState =
                    PlaybackState.PausedFullscreen;
            }
            else if (_batteryPaused &&
                 _settings.PauseOnBattery)
            {
                newState =
                    PlaybackState.PausedBattery;
            }
            else
            {
                newState =
                    PlaybackState.Playing;
            }

            if (CurrentState == newState)
                return;

            CurrentState = newState;

            System.Diagnostics.Debug.WriteLine(
                $"[AetherWall] Playback state → {CurrentState}");

            ApplyPlaybackState();
        }

        private void ApplyPlaybackState()
        {
            switch (CurrentState)
            {
                case PlaybackState.Playing:
                    _playbackService.Resume();
                    break;

                case PlaybackState.PausedFullscreen:
                case PlaybackState.PausedBattery:
                case PlaybackState.PausedManual:
                    _playbackService.Pause();
                    break;
            }
        }

        public void Dispose()
        {
            _fullscreenDetector.FullscreenChanged -=
                OnFullscreenChanged;

            _fullscreenDetector.Dispose();
        }
    }
}
