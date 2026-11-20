using AetherWall.Core;
using AetherWall.Views;
using AetherWall.Native;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using AetherWall.Models;
using AetherWall.Services;
using System.Collections.Specialized;

namespace AetherWall.ViewModels
{
    public class MainViewModel : IDisposable,
        INotifyPropertyChanged
    {
        private string _currentPage = "Home";
        private UserControl _currentView;

        private readonly LibraryViewModel _libraryViewModel;
        private readonly ConfigService _configService;
        private DisplayAssignmentViewModel? _selectedDisplay;
        public AppSettings Settings { get; }

        private const double DisplayCanvasWidth = 760;
        private const double DisplayCanvasHeight = 240;

        public ObservableCollection<DisplayAssignmentViewModel> Displays { get; } = new();

        public DisplayAssignmentViewModel? SelectedDisplay
        {
            get => _selectedDisplay;
            private set
            {
                if (ReferenceEquals(_selectedDisplay, value))
                    return;

                _selectedDisplay = value;
                OnPropertyChanged();
            }
        }


        public string CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (_currentPage == value)
                    return;

                _currentPage = value;
                OnPropertyChanged();
            }
        }

        public UserControl CurrentView
        {
            get => _currentView;
            private set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public ICommand ShowHomeCommand { get; }
        public ICommand ShowLibraryCommand { get; }
        public ICommand ShowFavoritesCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand AddWallpaperCommand { get; }
        public ICommand PreviousWallpaperCommand { get; }
        public ICommand NextWallpaperCommand { get; }
        public ICommand ToggleAudioCommand { get; }
        public ICommand SelectDisplayCommand { get; }
        public ICommand DetectDisplaysCommand { get; }
        public ICommand IdentifyDisplaysCommand { get; }
        public ICommand StopWallpaperCommand =>
            _libraryViewModel.StopWallpaperCommand;
        public ICommand ToggleManualPauseCommand =>
            _libraryViewModel.ToggleManualPauseCommand;

        public IReadOnlyList<WallpaperDisplayMode> WallpaperDisplayModes { get; }
            = Enum.GetValues<WallpaperDisplayMode>();

        public WallpaperDisplayMode WallpaperDisplayMode
        {
            get => _libraryViewModel.WallpaperDisplayMode;
            set => _libraryViewModel.WallpaperDisplayMode = value;
        }

        public int WallpaperCount =>
            _libraryViewModel.Wallpapers.Count;

        public int FavoriteCount =>
            _libraryViewModel.FavoriteWallpapers.Count;

        public WallpaperItem? CurrentWallpaper =>
            _libraryViewModel.CurrentWallpaper;

        public string PlaybackStatus =>
            _libraryViewModel.PlaybackStatus;

        public string PlaybackDetails
        {
            get
            {
                if (CurrentWallpaper == null)
                    return PlaybackStatus;

                var details = new List<string>();
                if (CurrentWallpaper.Resolution != "Unknown")
                    details.Add(CurrentWallpaper.Resolution);
                if (!string.IsNullOrWhiteSpace(CurrentWallpaper.Format))
                    details.Add(CurrentWallpaper.Format);

                details.Add(Settings.AudioEnabled
                    ? "Audio Active"
                    : "Audio Muted");
                details.Add(PlaybackStatus);
                return string.Join("  •  ", details);
            }
        }

        public string AudioToggleIcon =>
            Settings.AudioEnabled ? "\uE767" : "\uE74F";

        public string PauseResumeLabel =>
            _libraryViewModel.PauseResumeLabel;

        public string PlaybackToggleIcon =>
            PauseResumeLabel == "Pause wallpaper"
                ? "\uE769"
                : "\uE768";

        public static async Task<MainViewModel> CreateAsync(
            AppSettings settings)
        {
            PlaybackService playbackService =
                await Task.Run(() => new PlaybackService());

            try
            {
                return new MainViewModel(settings, playbackService);
            }
            catch
            {
                playbackService.Dispose();
                throw;
            }
        }

        private MainViewModel(
            AppSettings settings,
            PlaybackService playbackService)
        {
            _configService = new ConfigService();
            Settings = settings;

            _libraryViewModel = new LibraryViewModel(
                Settings,
                playbackService);
            _libraryViewModel.Wallpapers.CollectionChanged +=
                OnLibraryCollectionChanged;
            _libraryViewModel.FavoriteWallpapers.CollectionChanged +=
                OnLibraryCollectionChanged;
            _libraryViewModel.PropertyChanged +=
                OnLibraryViewModelPropertyChanged;
            _libraryViewModel.MonitorAssignmentsChanged +=
                OnMonitorAssignmentsChanged;

            ShowHomeCommand = new RelayCommand(ShowHome);
            ShowLibraryCommand = new RelayCommand(ShowLibrary);
            ShowFavoritesCommand = new RelayCommand(ShowFavorites);
            ShowSettingsCommand = new RelayCommand(ShowSettings);
            AddWallpaperCommand = new RelayCommand(AddWallpaper);
            PreviousWallpaperCommand =
                new RelayCommand(() => ChangeWallpaper(-1));
            NextWallpaperCommand =
                new RelayCommand(() => ChangeWallpaper(1));
            ToggleAudioCommand = new RelayCommand(ToggleAudio);
            SelectDisplayCommand =
                new ParameterizedRelayCommand<DisplayAssignmentViewModel>(SelectDisplay);
            DetectDisplaysCommand =
                new RelayCommand(RefreshDisplayAssignments);
            IdentifyDisplaysCommand =
                new RelayCommand(IdentifyDisplays);

            RefreshDisplayAssignments();

            _currentView = new HomeView
            {
                DataContext = this
            };
        }

        private void ShowHome()
        {
            CurrentPage = "Home";
            CurrentView = new HomeView
            {
                DataContext = this
            };
        }

        private void ShowLibrary()
        {
            CurrentPage = "Library";
            _libraryViewModel.EnsureLibraryThumbnails();

            CurrentView =
                new LibraryView
                {
                    DataContext = _libraryViewModel
                };
        }

        private void ShowFavorites()
        {
            CurrentPage = "Favorites";
            _libraryViewModel.EnsureFavoriteThumbnails();
            CurrentView = new FavoritesView
            {
                DataContext = _libraryViewModel
            };
        }

        private void ShowSettings()
        {
            CurrentPage = "Settings";

            CurrentView = new SettingsView(
                Settings,
                _libraryViewModel);
        }

        private void AddWallpaper()
        {
            _libraryViewModel.AddWallpaper();
        }

        private void ChangeWallpaper(int direction)
        {
            DisplayAssignmentViewModel? display = SelectedDisplay;
            WallpaperItem? assignedWallpaper = display?.SelectedWallpaper;
            WallpaperItem[] availableWallpapers =
                _libraryViewModel.Wallpapers
                    .Where(wallpaper => System.IO.File.Exists(wallpaper.FilePath))
                    .ToArray();

            if (display == null || availableWallpapers.Length == 0)
                return;

            int currentIndex = Array.FindIndex(
                availableWallpapers,
                wallpaper => string.Equals(
                    wallpaper.FilePath,
                    assignedWallpaper?.FilePath,
                    StringComparison.OrdinalIgnoreCase));

            int nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : availableWallpapers.Length - 1)
                : (currentIndex + direction + availableWallpapers.Length)
                    % availableWallpapers.Length;

            _libraryViewModel.SetMonitorWallpaper(
                display.DeviceName,
                availableWallpapers[nextIndex]);
        }

        private void ToggleAudio()
        {
            Settings.AudioEnabled = !Settings.AudioEnabled;
            _libraryViewModel.SetWallpaperAudioEnabled(Settings.AudioEnabled);
            _configService.Save(Settings);

            OnPropertyChanged(nameof(AudioToggleIcon));
            OnPropertyChanged(nameof(PlaybackDetails));
            RefreshDisplayPlaybackStates();
        }

        private void SelectDisplay(DisplayAssignmentViewModel display)
        {
            if (display == null)
                return;

            foreach (DisplayAssignmentViewModel item in Displays)
                item.IsSelected = ReferenceEquals(item, display);

            SelectedDisplay = display;
        }

        private void RefreshDisplayAssignments()
        {
            string? selectedDeviceName = SelectedDisplay?.DeviceName;
            IReadOnlyList<MonitorBounds> monitors = WallpaperNative.GetMonitors();
            Displays.Clear();

            if (monitors.Count == 0)
            {
                SelectedDisplay = null;
                return;
            }

            int minLeft = monitors.Min(monitor => monitor.Left);
            int minTop = monitors.Min(monitor => monitor.Top);
            int maxRight = monitors.Max(monitor => monitor.Left + monitor.Width);
            int maxBottom = monitors.Max(monitor => monitor.Top + monitor.Height);
            double totalWidth = Math.Max(1, maxRight - minLeft);
            double totalHeight = Math.Max(1, maxBottom - minTop);
            double scale = Math.Min(
                (DisplayCanvasWidth - 48) / totalWidth,
                (DisplayCanvasHeight - 48) / totalHeight);
            double offsetX = (DisplayCanvasWidth - totalWidth * scale) / 2;
            double offsetY = (DisplayCanvasHeight - totalHeight * scale) / 2;
            WallpaperItem? currentWallpaper = _libraryViewModel.CurrentWallpaper;

            for (int index = 0; index < monitors.Count; index++)
            {
                MonitorBounds monitor = monitors[index];
                WallpaperItem? assignedWallpaper = null;

                if (Settings.MonitorWallpaperIds.TryGetValue(
                        monitor.DeviceName,
                        out string? wallpaperId))
                {
                    assignedWallpaper = _libraryViewModel.Wallpapers
                        .FirstOrDefault(wallpaper => wallpaper.Id == wallpaperId);
                }
                else
                {
                    assignedWallpaper = currentWallpaper;
                }

                bool isSelected = string.Equals(
                    selectedDeviceName,
                    monitor.DeviceName,
                    StringComparison.OrdinalIgnoreCase);
                WallpaperDisplayMode selectedDisplayMode =
                    Settings.MonitorDisplayModes.TryGetValue(
                        monitor.DeviceName,
                        out WallpaperDisplayMode savedDisplayMode)
                        ? savedDisplayMode
                        : Settings.WallpaperDisplayMode;
                double tileWidth = monitor.Width * scale;
                double tileHeight = monitor.Height * scale;

                Displays.Add(new DisplayAssignmentViewModel(
                    (index + 1).ToString(),
                    monitor.IsPrimary
                        ? $"Display {index + 1} (Primary)"
                        : $"Display {index + 1}",
                    $"{monitor.Width} × {monitor.Height}",
                    monitor.DeviceName,
                    offsetX + (monitor.Left - minLeft) * scale,
                    offsetY + (monitor.Top - minTop) * scale,
                    tileWidth,
                    tileHeight,
                    _libraryViewModel.Wallpapers,
                    assignedWallpaper,
                    selectedDisplayMode,
                    WallpaperDisplayModes,
                    isSelected,
                    wallpaper => _libraryViewModel.SetMonitorWallpaper(
                        monitor.DeviceName,
                        wallpaper),
                    mode => _libraryViewModel.SetMonitorDisplayMode(
                        monitor.DeviceName,
                        mode),
                    wallpaper => _libraryViewModel.ToggleMonitorPlayback(
                        monitor.DeviceName,
                        wallpaper)));
            }

            SelectedDisplay = Displays.FirstOrDefault(display => display.IsSelected)
                ?? Displays.FirstOrDefault(display =>
                    monitors.Any(monitor => monitor.IsPrimary &&
                        string.Equals(
                            monitor.DeviceName,
                            display.DeviceName,
                            StringComparison.OrdinalIgnoreCase)))
                ?? Displays[0];

            if (!SelectedDisplay.IsSelected)
                SelectDisplay(SelectedDisplay);

            RefreshDisplayPlaybackStates();
        }

        private void IdentifyDisplays()
        {
            IReadOnlyList<MonitorBounds> monitors = WallpaperNative.GetMonitors();
            for (int index = 0; index < monitors.Count; index++)
                new DisplayIdentifyOverlay(monitors[index], index + 1).Show();
        }

        private void RefreshDisplaySelectionsFromSettings()
        {
            WallpaperItem? currentWallpaper = _libraryViewModel.CurrentWallpaper;

            foreach (DisplayAssignmentViewModel display in Displays)
            {
                WallpaperItem? wallpaper = currentWallpaper;
                if (Settings.MonitorWallpaperIds.TryGetValue(
                        display.DeviceName,
                        out string? wallpaperId))
                {
                    wallpaper = _libraryViewModel.Wallpapers
                        .FirstOrDefault(item => item.Id == wallpaperId);
                }

                display.SetSelectedWallpaperFromSettings(wallpaper);
            }

            RefreshDisplayPlaybackStates();
        }

        private void RefreshDisplayPlaybackStates()
        {
            bool canControlPlayback =
                _libraryViewModel.PlaybackStateManager.CurrentState ==
                PlaybackState.Playing;

            foreach (DisplayAssignmentViewModel display in Displays)
            {
                (bool IsActive, bool IsPaused) state =
                    display.SelectedWallpaper is WallpaperItem wallpaper
                        ? _libraryViewModel.GetMonitorPlaybackState(
                            display.DeviceName,
                            wallpaper)
                        : (false, false);

                display.SetPlaybackState(
                    state.IsActive,
                    state.IsPaused,
                    canControlPlayback,
                    Settings.AudioEnabled);
            }
        }

        private void OnMonitorAssignmentsChanged(
            object? sender,
            EventArgs e)
        {
            RefreshDisplaySelectionsFromSettings();
        }

        public void Dispose()
        {
            _libraryViewModel.Wallpapers.CollectionChanged -=
                OnLibraryCollectionChanged;
            _libraryViewModel.FavoriteWallpapers.CollectionChanged -=
                OnLibraryCollectionChanged;
            _libraryViewModel.PropertyChanged -=
                OnLibraryViewModelPropertyChanged;
            _libraryViewModel.MonitorAssignmentsChanged -=
                OnMonitorAssignmentsChanged;
            _libraryViewModel.Dispose();
        }

        private void OnLibraryCollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(WallpaperCount));
            OnPropertyChanged(nameof(FavoriteCount));
            OnPropertyChanged(nameof(CurrentWallpaper));
            OnPropertyChanged(nameof(PlaybackDetails));
            RefreshDisplaySelectionsFromSettings();
        }

        private void OnLibraryViewModelPropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryViewModel.CurrentWallpaper))
            {
                OnPropertyChanged(nameof(CurrentWallpaper));
                OnPropertyChanged(nameof(PlaybackDetails));
                RefreshDisplaySelectionsFromSettings();
            }

            if (e.PropertyName == nameof(LibraryViewModel.PlaybackStatus))
            {
                OnPropertyChanged(nameof(PlaybackStatus));
                OnPropertyChanged(nameof(PlaybackDetails));
                RefreshDisplayPlaybackStates();
            }

            if (e.PropertyName == nameof(LibraryViewModel.PauseResumeLabel))
            {
                OnPropertyChanged(nameof(PauseResumeLabel));
                OnPropertyChanged(nameof(PlaybackToggleIcon));
            }

            if (e.PropertyName == nameof(LibraryViewModel.WallpaperDisplayMode))
                OnPropertyChanged(nameof(WallpaperDisplayMode));
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
