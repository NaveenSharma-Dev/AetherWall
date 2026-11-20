
using AetherWall.Models;
using AetherWall.Services;
using AetherWall.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AetherWall.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly AppSettings _settings;
        private readonly ConfigService _configService;
        private readonly LibraryViewModel _libraryViewModel;
        private readonly PlaybackStateManager _playbackStateManager;
        private readonly StartupRegistrationService _startupRegistrationService;
        private string _startupStatus;

        public ICommand ToggleThemeCommand { get; }

        public SettingsViewModel(
            AppSettings settings,
            LibraryViewModel libraryViewModel)
        {
            _configService = new ConfigService();
            _settings = settings;
            _libraryViewModel = libraryViewModel;
            _playbackStateManager = libraryViewModel.PlaybackStateManager;
            _startupRegistrationService = new StartupRegistrationService();
            _startupStatus = settings.StartWithWindows
                ? "AetherWall will open when you sign in to Windows."
                : "AetherWall will open only when you launch it.";
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
        }

        public string ThemeButtonContent =>
            _settings.UseLightTheme
                ? "Switch to dark theme"
                : "Switch to light theme";

        public string ThemeDescription =>
            _settings.UseLightTheme
                ? "Light theme is active."
                : "Dark theme is active.";

        private void ToggleTheme()
        {
            _settings.UseLightTheme = !_settings.UseLightTheme;
            ThemeService.Apply(_settings.UseLightTheme);
            _configService.Save(_settings);
            OnPropertyChanged(nameof(ThemeButtonContent));
            OnPropertyChanged(nameof(ThemeDescription));
        }

        public bool StartWithWindows
        {
            get => _settings.StartWithWindows;
            set
            {
                if (_settings.StartWithWindows == value)
                    return;

                try
                {
                    _startupRegistrationService.SetEnabled(value);
                }
                catch
                {
                    _startupStatus = "Could not update the Windows startup setting.";
                    OnPropertyChanged(nameof(StartupStatus));
                    OnPropertyChanged();
                    return;
                }

                _settings.StartWithWindows = value;
                _configService.Save(_settings);
                _startupStatus = value
                    ? "AetherWall will open when you sign in to Windows."
                    : "AetherWall will open only when you launch it.";
                OnPropertyChanged(nameof(StartupStatus));
                OnPropertyChanged();
            }
        }

        public string StartupStatus => _startupStatus;

        public bool PauseOnFullscreen
        {
            get => _settings.PauseOnFullscreen;
            set
            {
                if (_settings.PauseOnFullscreen == value)
                    return;

                _settings.PauseOnFullscreen = value;
                _playbackStateManager.SetPauseOnFullscreen(value);
                _configService.Save(_settings);
                OnPropertyChanged();
            }
        }
        
        public bool PauseOnBattery
        {
            get => _settings.PauseOnBattery;
            set
            {
                if (_settings.PauseOnBattery == value)
                    return;

                _settings.PauseOnBattery = value;

                _configService.Save(_settings);

                _playbackStateManager?.SetPauseOnBattery(value);

                OnPropertyChanged();
            }
        }

        public bool AudioEnabled
        {
            get => _settings.AudioEnabled;
            set
            {
                if (_settings.AudioEnabled == value)
                    return;

                _settings.AudioEnabled = value;
                _libraryViewModel.SetWallpaperAudioEnabled(value);
                _configService.Save(_settings);
                OnPropertyChanged();
            }
        }

        public int Volume
        {
            get => _settings.Volume;
            set
            {
                int clampedVolume = Math.Clamp(value, 0, 100);
                if (_settings.Volume == clampedVolume)
                    return;

                _settings.Volume = clampedVolume;
                _libraryViewModel.SetWallpaperVolume(clampedVolume);
                _configService.Save(_settings);
                OnPropertyChanged();
            }
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
