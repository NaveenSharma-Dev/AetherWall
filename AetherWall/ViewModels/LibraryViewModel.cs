
using AetherWall.Core;
using AetherWall.Models;
using AetherWall.Services;
using AetherWall.Views;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace AetherWall.ViewModels
{
    public enum WallpaperSortOrder
    {
        NameAscending,
        NameDescending,
        DurationAscending,
        DurationDescending,
        ResolutionAscending,
        ResolutionDescending
    }

    public class LibraryViewModel : INotifyPropertyChanged
    {
        private readonly WallpaperLibraryService _libraryService;
        private readonly VideoMetadataService _metadataService;
        private readonly ThumbnailService _thumbnailService;
        private readonly WallpaperService _wallpaperService;
        private readonly string _wallpaperFolder;
        private readonly AppSettings _settings;
        private readonly ConfigService _configService;
        private readonly SemaphoreSlim _metadataLoadGate = new(2);

        private string _searchText = string.Empty;
        private WallpaperSortOrder _sortOrder = WallpaperSortOrder.NameAscending;
        private CancellationTokenSource? _wallpaperLoadCancellation;
        private bool _metadataLoaded;
        private bool _libraryThumbnailsRequested;
        private bool _favoriteThumbnailsRequested;

        public ObservableCollection<WallpaperItem> Wallpapers { get; }
        public ObservableCollection<WallpaperItem> FavoriteWallpapers { get; }
        public ObservableCollection<WallpaperItem> PlaylistWallpapers { get; }

        public ICollectionView FilteredWallpapers { get; }

        public WallpaperSortOrder SortOrder
        {
            get => _sortOrder;
            set
            {
                if (_sortOrder == value)
                    return;

                _sortOrder = value;
                OnPropertyChanged();
                ApplySort();
            }
        }

        public ICommand ClearSearchCommand { get; }
        public ICommand AddWallpaperCommand { get; }
        public ICommand RefreshLibraryCommand { get; }
        public ICommand ApplyWallpaperCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand DeleteWallpaperCommand { get; }
        public ICommand OpenFileLocationCommand { get; }
        public ICommand RenameWallpaperCommand { get; }
        public ICommand AddToPlaylistCommand { get; }
        public ICommand RemoveFromPlaylistCommand { get; }
        public ICommand MovePlaylistItemUpCommand { get; }
        public ICommand MovePlaylistItemDownCommand { get; }
        public ICommand StartPlaylistCommand { get; }
        public ICommand StopPlaylistCommand { get; }
        public ICommand StopWallpaperCommand { get; }
        public ICommand ToggleManualPauseCommand { get; }

        public WallpaperDisplayMode WallpaperDisplayMode
        {
            get => _wallpaperService.DisplayMode;
            set
            {
                if (_wallpaperService.DisplayMode == value)
                    return;

                _wallpaperService.SetDisplayMode(value);
                OnPropertyChanged();
            }
        }

        public string PauseResumeLabel =>
            PlaybackStateManager.IsManuallyPaused
                ? "Resume wallpaper"
                : "Pause wallpaper";

        public PlaybackStateManager PlaybackStateManager =>
            _wallpaperService.StateManager;

        public WallpaperItem? CurrentWallpaper
        {
            get
            {
                string? filePath = _wallpaperService.CurrentFilePath;
                if (string.IsNullOrWhiteSpace(filePath))
                    return null;

                return Wallpapers.FirstOrDefault(wallpaper =>
                    string.Equals(
                        wallpaper.FilePath,
                        filePath,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        public string PlaybackStatus
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_wallpaperService.CurrentFilePath))
                    return "No wallpaper active";

                return PlaybackStateManager.CurrentState switch
                {
                    PlaybackState.PausedFullscreen => "Paused while another app is fullscreen",
                    PlaybackState.PausedBattery => "Paused on battery",
                    PlaybackState.PausedManual => "Paused",
                    _ when _wallpaperService.IsPlaylistPlayback => "Playlist playing",
                    _ => "Wallpaper playing"
                };
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;

                _searchText = value;
                OnPropertyChanged();

                FilteredWallpapers.Refresh();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? MonitorAssignmentsChanged;

        public LibraryViewModel(
            AppSettings settings,
            PlaybackService playbackService)
        {
            _settings = settings;
            _settings.FavoriteWallpaperIds ??= new();
            _settings.CustomWallpaperNames ??= new();
            _settings.PlaylistWallpaperIds ??= new();
            _settings.MonitorWallpaperIds ??= new();
            _settings.MonitorDisplayModes ??= new();
            _configService = new ConfigService();

            _libraryService = new WallpaperLibraryService();
            _metadataService = new VideoMetadataService();
            _thumbnailService = new ThumbnailService();
            _wallpaperService = new WallpaperService(
                settings,
                playbackService);
            _wallpaperService.PlaybackInfoChanged +=
                OnPlaybackInfoChanged;

            _wallpaperFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "AetherWall",
                "Wallpapers");

            Directory.CreateDirectory(_wallpaperFolder);

            Wallpapers = new ObservableCollection<WallpaperItem>();
            FavoriteWallpapers = new ObservableCollection<WallpaperItem>();
            PlaylistWallpapers = new ObservableCollection<WallpaperItem>();

            FilteredWallpapers =
                CollectionViewSource.GetDefaultView(Wallpapers);

            FilteredWallpapers.Filter = FilterWallpaper;
            ApplySort();

            AddWallpaperCommand =
                new RelayCommand(AddWallpaper);
            RefreshLibraryCommand =
                new RelayCommand(RefreshLibrary);

            ApplyWallpaperCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    ApplyWallpaper);
            ToggleFavoriteCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    ToggleFavorite);
            DeleteWallpaperCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    DeleteWallpaper);
            OpenFileLocationCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    OpenFileLocation);
            RenameWallpaperCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    RenameWallpaper);
            AddToPlaylistCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    AddToPlaylist);
            RemoveFromPlaylistCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    RemoveFromPlaylist);
            MovePlaylistItemUpCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    MovePlaylistItemUp);
            MovePlaylistItemDownCommand =
                new ParameterizedRelayCommand<WallpaperItem>(
                    MovePlaylistItemDown);
            StartPlaylistCommand =
                new RelayCommand(StartPlaylist);
            StopPlaylistCommand =
                new RelayCommand(() => _wallpaperService.StopPlaylist());
            StopWallpaperCommand =
                new RelayCommand(_wallpaperService.Hide);
            ToggleManualPauseCommand =
                new RelayCommand(
                    _wallpaperService.StateManager.ToggleManualPause,
                    () => !string.IsNullOrWhiteSpace(
                        _wallpaperService.CurrentFilePath));
            ClearSearchCommand = 
                new RelayCommand(() => SearchText = string.Empty);
            LoadWallpapers(restoreAssignments: true);
        }

        private bool FilterWallpaper(object item)
        {
            if (item is not WallpaperItem wallpaper)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            string searchText = SearchText.Trim();

            return wallpaper.DisplayName.Contains(
                       searchText,
                       StringComparison.OrdinalIgnoreCase)
                   || wallpaper.Name.Contains(
                       searchText,
                       StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySort()
        {
            var sortDescriptions = FilteredWallpapers.SortDescriptions;
            sortDescriptions.Clear();

            switch (SortOrder)
            {
                case WallpaperSortOrder.NameAscending:
                    sortDescriptions.Add(new SortDescription(
                        nameof(WallpaperItem.DisplayName),
                        ListSortDirection.Ascending));
                    break;

                case WallpaperSortOrder.NameDescending:
                    sortDescriptions.Add(new SortDescription(
                        nameof(WallpaperItem.DisplayName),
                        ListSortDirection.Descending));
                    break;

                case WallpaperSortOrder.DurationAscending:
                    sortDescriptions.Add(new SortDescription(
                        nameof(WallpaperItem.Duration),
                        ListSortDirection.Ascending));
                    break;

                case WallpaperSortOrder.DurationDescending:
                    sortDescriptions.Add(new SortDescription(
                        nameof(WallpaperItem.Duration),
                        ListSortDirection.Descending));
                    break;

                case WallpaperSortOrder.ResolutionAscending:
                    sortDescriptions.Add(new SortDescription(
                        nameof(WallpaperItem.ResolutionPixels),
                        ListSortDirection.Ascending));
                    break;

                case WallpaperSortOrder.ResolutionDescending:
                    sortDescriptions.Add(new SortDescription(
                        nameof(WallpaperItem.ResolutionPixels),
                        ListSortDirection.Descending));
                    break;
            }
        }

        private void ApplyWallpaper(WallpaperItem wallpaper)
        {
            if (wallpaper == null)
                return;

            IReadOnlyList<AetherWall.Native.MonitorBounds> monitors =
                AetherWall.Native.WallpaperNative.GetMonitors();
            if (monitors.Count == 0)
                return;

            var dialog = new MonitorSelectionDialog(wallpaper, monitors)
            {
                Owner = Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() != true)
                return;

            ApplyWallpaperToMonitors(
                wallpaper,
                dialog.SelectedMonitors.Select(monitor => monitor.DeviceName));
        }

        private void ApplyWallpaperToMonitors(
            WallpaperItem wallpaper,
            IEnumerable<string> selectedDeviceNames)
        {
            string[] deviceNames = selectedDeviceNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (deviceNames.Length == 0)
                return;

            WallpaperItem? currentWallpaper = CurrentWallpaper;
            if (currentWallpaper != null)
            {
                foreach (var monitor in AetherWall.Native.WallpaperNative.GetMonitors())
                {
                    if (!_settings.MonitorWallpaperIds.ContainsKey(monitor.DeviceName))
                        _settings.MonitorWallpaperIds[monitor.DeviceName] = currentWallpaper.Id;
                }
            }

            foreach (string deviceName in deviceNames)
                _settings.MonitorWallpaperIds[deviceName] = wallpaper.Id;

            _configService.Save(_settings);
            MonitorAssignmentsChanged?.Invoke(this, EventArgs.Empty);

            foreach (string deviceName in deviceNames)
            {
                _wallpaperService.SetMonitorWallpaper(
                    deviceName,
                    wallpaper.FilePath);
            }
        }

        public void SetMonitorWallpaper(
            string deviceName,
            WallpaperItem wallpaper)
        {
            if (wallpaper == null)
                return;

            var monitors = AetherWall.Native.WallpaperNative.GetMonitors();
            WallpaperItem? currentWallpaper = CurrentWallpaper;
            if (currentWallpaper != null)
            {
                foreach (var monitor in monitors)
                {
                    if (!_settings.MonitorWallpaperIds.ContainsKey(monitor.DeviceName))
                        _settings.MonitorWallpaperIds[monitor.DeviceName] = currentWallpaper.Id;
                }
            }

            _settings.MonitorWallpaperIds[deviceName] = wallpaper.Id;
            _configService.Save(_settings);
            MonitorAssignmentsChanged?.Invoke(this, EventArgs.Empty);
            _wallpaperService.SetMonitorWallpaper(deviceName, wallpaper.FilePath);
        }

        public (bool IsActive, bool IsPaused) GetMonitorPlaybackState(
            string deviceName,
            WallpaperItem wallpaper) =>
            _wallpaperService.GetMonitorPlaybackState(
                deviceName,
                wallpaper.FilePath);

        public void ToggleMonitorPlayback(
            string deviceName,
            WallpaperItem wallpaper) =>
            _wallpaperService.ToggleMonitorPlayback(
                deviceName,
                wallpaper.FilePath);

        public void SetWallpaperAudioEnabled(bool enabled) =>
            _wallpaperService.SetAudioEnabled(enabled);

        public void SetWallpaperVolume(int volume) =>
            _wallpaperService.SetVolume(volume);

        public void SetMonitorDisplayMode(
            string deviceName,
            WallpaperDisplayMode displayMode)
        {
            _settings.MonitorDisplayModes[deviceName] = displayMode;
            _configService.Save(_settings);
            _wallpaperService.SetMonitorDisplayMode(deviceName, displayMode);
        }

        private void RestoreMonitorAssignments()
        {
            var wallpapersById = Wallpapers.ToDictionary(
                wallpaper => wallpaper.Id,
                wallpaper => wallpaper.FilePath,
                StringComparer.Ordinal);
            var availableAssignments = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            bool settingsChanged = false;

            foreach (var assignment in _settings.MonitorWallpaperIds.ToArray())
            {
                if (wallpapersById.TryGetValue(assignment.Value, out string? filePath) &&
                    File.Exists(filePath))
                {
                    availableAssignments[assignment.Key] = filePath;
                }
                else
                {
                    _settings.MonitorWallpaperIds.Remove(assignment.Key);
                    settingsChanged = true;
                }
            }

            if (settingsChanged)
                _configService.Save(_settings);

            if (availableAssignments.Count > 0)
            {
                void RestoreAssignments() =>
                    _wallpaperService.ApplyMonitorWallpapers(availableAssignments);

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    RestoreAssignments();
                else
                    dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(RestoreAssignments));
            }
        }

        private void ToggleFavorite(WallpaperItem wallpaper)
        {
            if (wallpaper == null)
                return;

            if (wallpaper.IsFavorite)
            {
                wallpaper.IsFavorite = false;
                _settings.FavoriteWallpaperIds.Remove(wallpaper.Id);
                FavoriteWallpapers.Remove(wallpaper);
            }
            else
            {
                wallpaper.IsFavorite = true;

                if (!_settings.FavoriteWallpaperIds.Contains(wallpaper.Id))
                    _settings.FavoriteWallpaperIds.Add(wallpaper.Id);

                if (!FavoriteWallpapers.Contains(wallpaper))
                    FavoriteWallpapers.Add(wallpaper);
            }

            _configService.Save(_settings);
        }

        private void DeleteWallpaper(WallpaperItem wallpaper)
        {
            if (wallpaper == null)
                return;

            MessageBoxResult result = MessageBox.Show(
                $"Permanently delete '{wallpaper.DisplayName}' from AetherWall?",
                "Delete wallpaper",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                string libraryRoot = Path.GetFullPath(_wallpaperFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string filePath = Path.GetFullPath(wallpaper.FilePath);

                if (!filePath.StartsWith(
                        libraryRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Only wallpapers stored in AetherWall's library folder can be deleted.",
                        "Delete unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (_wallpaperService.IsPlayingFile(filePath))
                    _wallpaperService.Hide();

                File.Delete(filePath);

                wallpaper.IsFavorite = false;
                Wallpapers.Remove(wallpaper);
                FavoriteWallpapers.Remove(wallpaper);

                bool settingsChanged =
                    _settings.FavoriteWallpaperIds.Remove(wallpaper.Id);
                settingsChanged |=
                    _settings.CustomWallpaperNames.Remove(wallpaper.Id);
                settingsChanged |=
                    _settings.PlaylistWallpaperIds.Remove(wallpaper.Id);
                foreach (string monitor in _settings.MonitorWallpaperIds
                             .Where(entry => entry.Value == wallpaper.Id)
                             .Select(entry => entry.Key)
                             .ToArray())
                {
                    settingsChanged |=
                        _settings.MonitorWallpaperIds.Remove(monitor);
                }

                PlaylistWallpapers.Remove(wallpaper);

                if (settingsChanged)
                {
                    _configService.Save(_settings);
                    MonitorAssignmentsChanged?.Invoke(this, EventArgs.Empty);
                }

                UpdateActivePlaylist();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not delete '{wallpaper.DisplayName}'.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Delete failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenFileLocation(WallpaperItem wallpaper)
        {
            if (wallpaper == null)
                return;

            try
            {
                string filePath = Path.GetFullPath(wallpaper.FilePath);

                if (!File.Exists(filePath))
                {
                    MessageBox.Show(
                        "The wallpaper file could not be found.",
                        "File unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo(
                    "explorer.exe",
                    $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open the wallpaper's file location.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Open file location failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RenameWallpaper(WallpaperItem wallpaper)
        {
            if (wallpaper == null)
                return;

            var dialog = new RenameWallpaperDialog(wallpaper.DisplayName)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
                return;

            string displayName = dialog.DisplayName;

            if (string.IsNullOrWhiteSpace(displayName) ||
                string.Equals(displayName, wallpaper.Name, StringComparison.Ordinal))
            {
                wallpaper.CustomName = null;
                _settings.CustomWallpaperNames.Remove(wallpaper.Id);
            }
            else
            {
                wallpaper.CustomName = displayName;
                _settings.CustomWallpaperNames[wallpaper.Id] = displayName;
            }

            _configService.Save(_settings);
            FilteredWallpapers.Refresh();
        }

        private void ApplySavedDisplayName(WallpaperItem wallpaper)
        {
            if (_settings.CustomWallpaperNames.TryGetValue(
                    wallpaper.Id,
                    out var displayName))
            {
                wallpaper.CustomName = displayName;
            }
        }

        private void AddToPlaylist(WallpaperItem wallpaper)
        {
            if (wallpaper == null || PlaylistWallpapers.Contains(wallpaper))
                return;

            PlaylistWallpapers.Add(wallpaper);
            SavePlaylistOrder();
        }

        private void RemoveFromPlaylist(WallpaperItem wallpaper)
        {
            if (wallpaper != null && PlaylistWallpapers.Remove(wallpaper))
                SavePlaylistOrder();
        }

        private void MovePlaylistItemUp(WallpaperItem wallpaper)
        {
            int index = PlaylistWallpapers.IndexOf(wallpaper);
            if (index <= 0)
                return;

            PlaylistWallpapers.Move(index, index - 1);
            SavePlaylistOrder();
        }

        private void MovePlaylistItemDown(WallpaperItem wallpaper)
        {
            int index = PlaylistWallpapers.IndexOf(wallpaper);
            if (index < 0 || index >= PlaylistWallpapers.Count - 1)
                return;

            PlaylistWallpapers.Move(index, index + 1);
            SavePlaylistOrder();
        }

        private void SavePlaylistOrder()
        {
            _settings.PlaylistWallpaperIds =
                PlaylistWallpapers.Select(wallpaper => wallpaper.Id).ToList();

            _configService.Save(_settings);
            UpdateActivePlaylist();
        }

        private void UpdateActivePlaylist()
        {
            if (_wallpaperService.IsPlaylistPlayback)
            {
                _wallpaperService.UpdatePlaylist(
                    PlaylistWallpapers
                        .Select(wallpaper => wallpaper.FilePath)
                        .ToArray());
            }
        }

        private void StartPlaylist()
        {
            string[] filePaths = PlaylistWallpapers
                .Select(wallpaper => wallpaper.FilePath)
                .Where(File.Exists)
                .ToArray();

            if (filePaths.Length == 0)
            {
                MessageBox.Show(
                    "Add at least one available wallpaper to the playlist first.",
                    "Playlist is empty",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _wallpaperService.ShowPlaylist(filePaths);
        }

        // =========================================================
        // LOAD EXISTING WALLPAPERS
        // =========================================================

        private void LoadWallpapers(bool restoreAssignments = false)
        {
            _wallpaperLoadCancellation?.Cancel();
            _metadataLoaded = false;
            var loadCancellation = new CancellationTokenSource();
            _wallpaperLoadCancellation = loadCancellation;

            _ = LoadWallpapersAsync(loadCancellation, restoreAssignments);
        }

        private async Task LoadWallpapersAsync(
            CancellationTokenSource loadCancellation,
            bool restoreAssignments)
        {
            bool detailsOwnCancellation = false;

            try
            {
                var wallpapers = await Task.Run(
                    () => _libraryService.ScanFolder(_wallpaperFolder),
                    loadCancellation.Token);

                if (loadCancellation.IsCancellationRequested)
                    return;

                Wallpapers.Clear();
                FavoriteWallpapers.Clear();

                foreach (var wallpaper in wallpapers)
                {
                    ApplySavedDisplayName(wallpaper);
                    wallpaper.IsFavorite =
                        _settings.FavoriteWallpaperIds.Contains(wallpaper.Id);

                    Wallpapers.Add(wallpaper);

                    if (wallpaper.IsFavorite)
                        FavoriteWallpapers.Add(wallpaper);
                }

                RestorePlaylist();
                FilteredWallpapers.Refresh();
                UpdateActivePlaylist();

                if (restoreAssignments)
                    RestoreMonitorAssignments();

                detailsOwnCancellation = true;
                _ = LoadWallpaperDetailsAsync(wallpapers, loadCancellation);
            }
            catch (OperationCanceledException)
            {
                // A newer library refresh superseded this scan.
            }
            finally
            {
                if (!detailsOwnCancellation)
                {
                    if (ReferenceEquals(
                            _wallpaperLoadCancellation,
                            loadCancellation))
                    {
                        _wallpaperLoadCancellation = null;
                    }

                    loadCancellation.Dispose();
                }
            }
        }

        private async Task LoadWallpaperDetailsAsync(
            IReadOnlyList<WallpaperItem> wallpapers,
            CancellationTokenSource loadCancellation)
        {
            try
            {
                await Task.WhenAll(wallpapers.Select(wallpaper =>
                    LoadWallpaperMetadataAsync(
                        wallpaper,
                        loadCancellation.Token)));

                if (loadCancellation.IsCancellationRequested)
                    return;

                _metadataLoaded = true;
                EnsureStartupThumbnails(wallpapers);

                if (_libraryThumbnailsRequested)
                    EnsureThumbnails(Wallpapers);

                if (_favoriteThumbnailsRequested)
                    EnsureThumbnails(FavoriteWallpapers);

                FilteredWallpapers.Refresh();
            }
            catch (OperationCanceledException)
            {
                // A newer library refresh superseded this scan.
            }
            finally
            {
                if (ReferenceEquals(
                        _wallpaperLoadCancellation,
                        loadCancellation))
                {
                    _wallpaperLoadCancellation = null;
                }

                loadCancellation.Dispose();
            }
        }

        private async Task LoadWallpaperMetadataAsync(
            WallpaperItem wallpaper,
            CancellationToken cancellationToken)
        {
            await _metadataLoadGate.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(
                    () => _metadataService.ReadMetadata(wallpaper),
                    cancellationToken);
            }
            finally
            {
                _metadataLoadGate.Release();
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                wallpaper.NotifyMetadataUpdated();
            else
                await dispatcher.InvokeAsync(wallpaper.NotifyMetadataUpdated);

            if (ReferenceEquals(CurrentWallpaper, wallpaper))
                OnPropertyChanged(nameof(CurrentWallpaper));
        }

        public void EnsureLibraryThumbnails()
        {
            _libraryThumbnailsRequested = true;
            if (_metadataLoaded)
                EnsureThumbnails(Wallpapers);
        }

        public void EnsureFavoriteThumbnails()
        {
            _favoriteThumbnailsRequested = true;
            if (_metadataLoaded)
                EnsureThumbnails(FavoriteWallpapers);
        }

        private void EnsureStartupThumbnails(
            IReadOnlyList<WallpaperItem> wallpapers)
        {
            var prioritizedIds = new HashSet<string>(
                _settings.FavoriteWallpaperIds.Concat(
                    _settings.MonitorWallpaperIds.Values),
                StringComparer.Ordinal);

            if (CurrentWallpaper is WallpaperItem currentWallpaper)
                prioritizedIds.Add(currentWallpaper.Id);

            EnsureThumbnails(wallpapers.Where(wallpaper =>
                prioritizedIds.Contains(wallpaper.Id)));
        }

        private void EnsureThumbnails(
            IEnumerable<WallpaperItem> wallpapers)
        {
            foreach (WallpaperItem wallpaper in wallpapers)
            {
                if (string.IsNullOrWhiteSpace(wallpaper.ThumbnailPath))
                    _ = GenerateThumbnailAsync(wallpaper);
            }
        }

        private void RestorePlaylist()
        {
            PlaylistWallpapers.Clear();
            var wallpapersById = Wallpapers.ToDictionary(
                wallpaper => wallpaper.Id,
                StringComparer.Ordinal);
            var restoredIds = new List<string>();

            foreach (string id in _settings.PlaylistWallpaperIds)
            {
                if (wallpapersById.TryGetValue(id, out var wallpaper) &&
                    !PlaylistWallpapers.Contains(wallpaper))
                {
                    PlaylistWallpapers.Add(wallpaper);
                    restoredIds.Add(id);
                }
            }

            if (restoredIds.Count != _settings.PlaylistWallpaperIds.Count)
            {
                _settings.PlaylistWallpaperIds = restoredIds;
                _configService.Save(_settings);
            }
        }

        private void RefreshLibrary()
        {
            LoadWallpapers();
        }

        // =========================================================
        // ADD WALLPAPER
        // =========================================================

        public async void AddWallpaper()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add Wallpapers",
                Multiselect = true,

                Filter =
                    "Video Files|" +
                    "*.mp4;*.mkv;*.webm;*.avi;*.mov;" +
                    "*.wmv;*.flv;*.m4v;*.mpeg;*.mpg;*.ts"
            };

            bool? result = dialog.ShowDialog();

            if (result != true)
                return;

            foreach (string sourceFile in dialog.FileNames)
            {
                await ImportWallpaperAsync(sourceFile);
            }
        }

        // =========================================================
        // IMPORT WALLPAPER
        // =========================================================

        private async Task ImportWallpaperAsync(string sourceFile)
        {
            try
            {
                string fileName = Path.GetFileName(sourceFile);

                string destination = Path.Combine(
                    _wallpaperFolder,
                    fileName);

                // Prevent overwriting an existing wallpaper.
                destination = GetUniqueFilePath(destination);

                // Copy without blocking the UI.
                await Task.Run(() =>
                {
                    File.Copy(sourceFile, destination, false);
                });

                var wallpaper = new WallpaperItem
                {
                    Id = WallpaperLibraryService.CreateId(destination),
                    FilePath = destination,
                    Name = Path.GetFileNameWithoutExtension(destination)
                };

                // Read video metadata.
                await Task.Run(() =>
                    _metadataService.ReadMetadata(wallpaper));
                ApplySavedDisplayName(wallpaper);
                wallpaper.IsFavorite =
                    _settings.FavoriteWallpaperIds.Contains(wallpaper.Id);

                // Add the card to the library.
                Wallpapers.Add(wallpaper);

                if (wallpaper.IsFavorite)
                    FavoriteWallpapers.Add(wallpaper);

                if (_settings.PlaylistWallpaperIds.Contains(wallpaper.Id))
                {
                    RestorePlaylist();
                    UpdateActivePlaylist();
                }

                // Generate the thumbnail.
                await GenerateThumbnailAsync(wallpaper);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AetherWall] Wallpaper import failed: {ex.Message}");
            }
        }

        // =========================================================
        // THUMBNAIL
        // =========================================================

        private async Task GenerateThumbnailAsync(WallpaperItem wallpaper)
        {
            try
            {
                string? thumbnail =
                    await _thumbnailService.GetOrCreateThumbnailAsync(wallpaper);

                if (thumbnail != null)
                {
                    wallpaper.ThumbnailPath = thumbnail;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AetherWall] Thumbnail generation failed: {ex.Message}");
            }
        }

        // =========================================================
        // UNIQUE FILE NAME
        // =========================================================

        private static string GetUniqueFilePath(string originalPath)
        {
            if (!File.Exists(originalPath))
                return originalPath;

            string directory = Path.GetDirectoryName(originalPath)!;

            string name =
                Path.GetFileNameWithoutExtension(originalPath);

            string extension = Path.GetExtension(originalPath);

            int counter = 2;

            while (true)
            {
                string candidate = Path.Combine(
                    directory,
                    $"{name} ({counter}){extension}");

                if (!File.Exists(candidate))
                    return candidate;

                counter++;
            }
        }

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }

        private void OnPlaybackInfoChanged(
            object? sender,
            EventArgs e)
        {
            void RaisePlaybackProperties()
            {
                OnPropertyChanged(nameof(CurrentWallpaper));
                OnPropertyChanged(nameof(PlaybackStatus));
                OnPropertyChanged(nameof(PauseResumeLabel));

                if (ToggleManualPauseCommand is RelayCommand command)
                    command.RaiseCanExecuteChanged();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                RaisePlaybackProperties();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(RaisePlaybackProperties));
            }
        }

        public void Dispose()
        {
            _wallpaperLoadCancellation?.Cancel();
            _wallpaperLoadCancellation = null;
            _wallpaperService.PlaybackInfoChanged -=
                OnPlaybackInfoChanged;
            _wallpaperService.Dispose();
        }
    }
}
