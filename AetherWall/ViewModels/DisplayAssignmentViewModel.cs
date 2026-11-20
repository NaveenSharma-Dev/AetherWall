using AetherWall.Models;
using AetherWall.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AetherWall.ViewModels
{
    public sealed class DisplayAssignmentViewModel : INotifyPropertyChanged
    {
        private WallpaperItem? _selectedWallpaper;
        private bool _isSelected;
        private WallpaperDisplayMode _selectedDisplayMode;
        private bool _isWallpaperActive;
        private bool _isWallpaperPaused;
        private bool _canControlPlayback = true;
        private bool _audioEnabled;
        private readonly Action<WallpaperItem> _selectionChanged;
        private readonly Action<WallpaperDisplayMode> _displayModeChanged;
        private readonly Action<WallpaperItem> _togglePlayback;

        public string Number { get; }
        public string Title { get; }
        public string Resolution { get; }
        public string DeviceName { get; }
        public double LayoutLeft { get; }
        public double LayoutTop { get; }
        public double TileWidth { get; }
        public double TileHeight { get; }
        public IEnumerable<WallpaperItem> WallpaperChoices { get; }
        public IReadOnlyList<WallpaperDisplayMode> DisplayModes { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public WallpaperItem? SelectedWallpaper
        {
            get => _selectedWallpaper;
            set
            {
                if (ReferenceEquals(_selectedWallpaper, value))
                    return;

                _selectedWallpaper = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AssignedWallpaperName));
                OnPropertyChanged(nameof(WallpaperMetadata));
                OnPropertyChanged(nameof(PauseResumeLabel));
                OnPropertyChanged(nameof(PlaybackToggleIcon));
                OnPropertyChanged(nameof(IsWallpaperPlaying));
                RefreshPlaybackCommand();

                if (value != null)
                    _selectionChanged(value);
            }
        }

        public string AssignedWallpaperName =>
            SelectedWallpaper?.DisplayName ?? "No wallpaper assigned";

        public bool IsWallpaperPlaying =>
            _isWallpaperActive && !_isWallpaperPaused;

        public string WallpaperMetadata
        {
            get
            {
                if (SelectedWallpaper == null)
                    return "Choose a wallpaper in Library";

                var details = new List<string>();
                if (SelectedWallpaper.Resolution != "Unknown")
                    details.Add(SelectedWallpaper.Resolution);
                if (!string.IsNullOrWhiteSpace(SelectedWallpaper.Format))
                    details.Add(SelectedWallpaper.Format.ToUpperInvariant());

                details.Add(_audioEnabled ? "Audio Active" : "Audio Muted");
                return string.Join("  •  ", details);
            }
        }

        public string PauseResumeLabel =>
            SelectedWallpaper == null
                ? "No wallpaper assigned"
                : !_isWallpaperActive
                    ? "Play on this display"
                    : _isWallpaperPaused
                        ? "Resume on this display"
                        : "Pause this display";

        public string PlaybackToggleIcon =>
            _isWallpaperActive && !_isWallpaperPaused
                ? "\uE769"
                : "\uE768";

        public bool CanTogglePlayback =>
            SelectedWallpaper != null && _canControlPlayback;

        public ICommand TogglePlaybackCommand { get; }

        public WallpaperDisplayMode SelectedDisplayMode
        {
            get => _selectedDisplayMode;
            set
            {
                if (_selectedDisplayMode == value)
                    return;

                _selectedDisplayMode = value;
                OnPropertyChanged();
                _displayModeChanged(value);
            }
        }

        public DisplayAssignmentViewModel(
            string number,
            string title,
            string resolution,
            string deviceName,
            double layoutLeft,
            double layoutTop,
            double tileWidth,
            double tileHeight,
            IEnumerable<WallpaperItem> wallpaperChoices,
            WallpaperItem? selectedWallpaper,
            WallpaperDisplayMode selectedDisplayMode,
            IReadOnlyList<WallpaperDisplayMode> displayModes,
            bool isSelected,
            Action<WallpaperItem> selectionChanged,
            Action<WallpaperDisplayMode> displayModeChanged,
            Action<WallpaperItem> togglePlayback)
        {
            Number = number;
            Title = title;
            Resolution = resolution;
            DeviceName = deviceName;
            LayoutLeft = layoutLeft;
            LayoutTop = layoutTop;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            WallpaperChoices = wallpaperChoices;
            _selectedWallpaper = selectedWallpaper;
            _selectedDisplayMode = selectedDisplayMode;
            DisplayModes = displayModes;
            _isSelected = isSelected;
            _selectionChanged = selectionChanged;
            _displayModeChanged = displayModeChanged;
            _togglePlayback = togglePlayback;
            TogglePlaybackCommand = new RelayCommand(
                ToggleSelectedWallpaperPlayback,
                () => CanTogglePlayback);
        }

        public void SetSelectedWallpaperFromSettings(WallpaperItem? wallpaper)
        {
            if (ReferenceEquals(_selectedWallpaper, wallpaper))
                return;

            _selectedWallpaper = wallpaper;
            OnPropertyChanged(nameof(SelectedWallpaper));
            OnPropertyChanged(nameof(AssignedWallpaperName));
            OnPropertyChanged(nameof(WallpaperMetadata));
            OnPropertyChanged(nameof(PauseResumeLabel));
            OnPropertyChanged(nameof(PlaybackToggleIcon));
            OnPropertyChanged(nameof(IsWallpaperPlaying));
            RefreshPlaybackCommand();
        }

        public void SetPlaybackState(
            bool isActive,
            bool isPaused,
            bool canControlPlayback,
            bool audioEnabled)
        {
            bool paused = isActive && isPaused;
            bool changed = _isWallpaperActive != isActive ||
                _isWallpaperPaused != paused ||
                _canControlPlayback != canControlPlayback ||
                _audioEnabled != audioEnabled;

            _isWallpaperActive = isActive;
            _isWallpaperPaused = paused;
            _canControlPlayback = canControlPlayback;
            _audioEnabled = audioEnabled;

            if (!changed)
                return;

            OnPropertyChanged(nameof(WallpaperMetadata));
            OnPropertyChanged(nameof(PauseResumeLabel));
            OnPropertyChanged(nameof(PlaybackToggleIcon));
            OnPropertyChanged(nameof(IsWallpaperPlaying));
            RefreshPlaybackCommand();
        }

        private void ToggleSelectedWallpaperPlayback()
        {
            if (SelectedWallpaper is WallpaperItem wallpaper)
                _togglePlayback(wallpaper);
        }

        private void RefreshPlaybackCommand()
        {
            OnPropertyChanged(nameof(CanTogglePlayback));
            if (TogglePlaybackCommand is RelayCommand command)
                command.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
