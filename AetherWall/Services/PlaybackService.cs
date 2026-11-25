using LibVLCSharp.Shared;
using System.IO;
using System.Linq;
using System.Threading;

namespace AetherWall.Services
{
    public class PlaybackService : IDisposable
    {
        private readonly LibVLC _libVLC;
        private readonly MediaPlayer _mediaPlayer;

        private Media? _currentMedia;
        private string? _currentFilePath;
        private string[] _playlistPaths = Array.Empty<string>();

        private bool _isStopping;
        private volatile bool _isPlaylistPlayback;
        private volatile bool _pauseRequested;
        private int _looping;

        public MediaPlayer MediaPlayer =>
            _mediaPlayer;

        public string? CurrentFilePath =>
            _currentFilePath;

        public event EventHandler? PlaybackInfoChanged;

        public bool IsPlaying =>
            _mediaPlayer.IsPlaying;

        public bool IsPlayingFile(string filePath) =>
            string.Equals(
                _currentFilePath,
                filePath,
                StringComparison.OrdinalIgnoreCase);

        public bool IsPlaylistPlayback =>
            _isPlaylistPlayback;

        public void SetAudioEnabled(bool enabled)
        {
            _mediaPlayer.Mute = !enabled;
        }

        public void SetVolume(int volume)
        {
            _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
        }

        public void SetDisplayMode(
            AetherWall.Models.WallpaperDisplayMode displayMode,
            string targetAspectRatio)
        {
            switch (displayMode)
            {
                case AetherWall.Models.WallpaperDisplayMode.Fill:
                    _mediaPlayer.AspectRatio = null;
                    _mediaPlayer.CropGeometry = targetAspectRatio;
                    break;

                case AetherWall.Models.WallpaperDisplayMode.Stretch:
                    _mediaPlayer.CropGeometry = string.Empty;
                    _mediaPlayer.AspectRatio = targetAspectRatio;
                    break;

                default:
                    _mediaPlayer.CropGeometry = string.Empty;
                    _mediaPlayer.AspectRatio = null;
                    break;
            }
        }

        public PlaybackService()
        {
            LibVLCSharp.Shared.Core.Initialize();

            _libVLC = new LibVLC(
                "--no-video-title-show",
                "--quiet");

            _mediaPlayer =
                new MediaPlayer(_libVLC);
            _mediaPlayer.EnableHardwareDecoding = true;

            _mediaPlayer.EndReached +=
                OnMediaEnded;
            _mediaPlayer.Playing +=
                OnMediaPlaying;
        }

        public void Play(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            Stop();

            _isStopping = false;
            _looping = 0;
            _currentFilePath = filePath;

            StartMedia(filePath);
            OnPlaybackInfoChanged();
        }

        public void PlayPlaylist(IReadOnlyList<string> filePaths)
        {
            string[] availablePaths =
                filePaths.Where(File.Exists).ToArray();

            if (availablePaths.Length == 0)
                return;

            Stop();

            _playlistPaths = availablePaths;
            _isPlaylistPlayback = true;
            _isStopping = false;
            _looping = 0;
            _currentFilePath = availablePaths[0];

            StartMedia(_currentFilePath);
            OnPlaybackInfoChanged();
        }

        public void UpdatePlaylist(IReadOnlyList<string> filePaths)
        {
            if (!_isPlaylistPlayback)
                return;

            _playlistPaths = filePaths
                .Where(File.Exists)
                .ToArray();
        }

        private void StartMedia(string filePath)
        {
            var media =
                new Media(
                    _libVLC,
                    new Uri(filePath));

            _currentMedia = media;

            _mediaPlayer.Media = media;

            _mediaPlayer.Play();
        }

        public void Pause()
        {
            _pauseRequested = true;

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
            }

            OnPlaybackInfoChanged();
        }

        public void Resume()
        {
            _pauseRequested = false;

            if (!_mediaPlayer.IsPlaying &&
                _mediaPlayer.Media != null)
            {
                _mediaPlayer.Play();
            }

            OnPlaybackInfoChanged();
        }

        public void Stop()
        {
            _isStopping = true;
            _pauseRequested = false;
            _isPlaylistPlayback = false;
            _playlistPaths = Array.Empty<string>();

            _mediaPlayer.Stop();

            _mediaPlayer.Media = null;

            _currentMedia?.Dispose();
            _currentMedia = null;

            _currentFilePath = null;
            OnPlaybackInfoChanged();
        }

        private void OnMediaEnded(
            object? sender,
            EventArgs e)
        {
            if (_isStopping)
                return;

            if (string.IsNullOrEmpty(
                    _currentFilePath))
            {
                return;
            }

            // Prevent multiple EndReached events
            // from starting multiple loops.
            if (Interlocked.Exchange(
                    ref _looping,
                    1) != 0)
            {
                return;
            }

            string filePath =
                _currentFilePath;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (_isStopping)
                        return;

                    if (_isPlaylistPlayback)
                    {
                        string? nextFilePath =
                            GetNextPlaylistFile(filePath);

                        if (nextFilePath == null)
                        {
                            Stop();
                            return;
                        }

                        if (_isStopping || !_isPlaylistPlayback)
                            return;

                        var nextMedia =
                            new Media(
                                _libVLC,
                                new Uri(nextFilePath));

                        _currentFilePath = nextFilePath;
                        _currentMedia = nextMedia;
                        _mediaPlayer.Play(nextMedia);
                        OnPlaybackInfoChanged();
                        return;
                    }

                    // Move playback back to the beginning.
                    _mediaPlayer.SeekTo(
                        TimeSpan.Zero);

                    if (_isStopping)
                        return;

                    // Create a fresh Media object.
                    // LibVLCSharp owns/disposes it when
                    // Play(Media) is called.
                    var media =
                        new Media(
                            _libVLC,
                            new Uri(filePath));

                    _currentMedia = media;
                    _mediaPlayer.Play(media);
                }
                finally
                {
                    Interlocked.Exchange(
                        ref _looping,
                        0);
                }
            });
        }

        private void OnMediaPlaying(
            object? sender,
            EventArgs e)
        {
            if (!_pauseRequested || !_mediaPlayer.IsPlaying)
                return;

            _mediaPlayer.Pause();
            OnPlaybackInfoChanged();
        }

        private void OnPlaybackInfoChanged()
        {
            PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        private string? GetNextPlaylistFile(string currentFilePath)
        {
            string[] playlistPaths = _playlistPaths;
            if (playlistPaths.Length == 0)
                return null;

            int currentIndex = Array.FindIndex(
                playlistPaths,
                path => string.Equals(
                    path,
                    currentFilePath,
                    StringComparison.OrdinalIgnoreCase));

            int firstCandidate = currentIndex < 0
                ? 0
                : (currentIndex + 1) % playlistPaths.Length;

            for (int offset = 0; offset < playlistPaths.Length; offset++)
            {
                string candidate =
                    playlistPaths[(firstCandidate + offset) % playlistPaths.Length];

                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public void Dispose()
        {
            _isStopping = true;

            _mediaPlayer.EndReached -=
                OnMediaEnded;
            _mediaPlayer.Playing -=
                OnMediaPlaying;

            _mediaPlayer.Stop();

            _mediaPlayer.Media = null;

            _currentMedia?.Dispose();
            _currentMedia = null;

            _currentFilePath = null;

            _mediaPlayer.Dispose();
            _libVLC.Dispose();
        }
    }
}
