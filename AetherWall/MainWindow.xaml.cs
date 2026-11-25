using AetherWall.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AetherWall
{
    public partial class MainWindow : Window
    {
        private readonly AetherWall.Models.AppSettings _settings;
        private AetherWall.Services.SystemTrayService? _systemTrayService;
        private string? _startupLogPath;
        private bool _exitRequested;

        public MainWindow()
        {
            _settings = new AetherWall.Services.ConfigService().Load();
            AetherWall.Services.ThemeService.Apply(_settings.UseLightTheme);

            InitializeComponent();

            Closing += OnWindowClosing;
            Closed += OnWindowClosed;
            ContentRendered += OnContentRendered;

            SetResponsiveInitialSize();
        }

        private async void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;

            try
            {
                StartupStatusText.Text = "Preparing wallpaper playback…";
                MainViewModel viewModel =
                    await MainViewModel.CreateAsync(_settings);

                DataContext = viewModel;
                _systemTrayService =
                    new AetherWall.Services.SystemTrayService(
                        viewModel,
                        ShowFromTray,
                        ExitFromTray);

                StartupOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _startupLogPath =
                    AetherWall.Services.StartupLogService.WriteFailure(ex);

                StartupProgressBar.Visibility = Visibility.Collapsed;
                if (_startupLogPath is not null)
                {
                    StartupStatusText.Text =
                        "AetherWall couldn't start. Open the local startup log for details.";
                    OpenStartupLogButton.Visibility = Visibility.Visible;
                }
                else
                {
                    StartupStatusText.Text =
                        "AetherWall couldn't start, and its diagnostic log couldn't be saved.";
                }
            }
        }

        private void OpenStartupLogButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_startupLogPath is null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo(_startupLogPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StartupStatusText.Text =
                    $"Couldn't open the log. Find it here: {Path.GetDirectoryName(_startupLogPath)}";
                Debug.WriteLine($"[AetherWall] Could not open startup log: {ex}");
            }
        }

        private void ShowFromTray()
        {
            if (!IsVisible)
                Show();

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
        }

        private void ExitFromTray()
        {
            _exitRequested = true;
            Close();
        }

        private void OnWindowClosing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (_exitRequested)
                return;

            e.Cancel = true;
            Hide();
        }

        private void OnWindowClosed(
            object? sender,
            EventArgs e)
        {
            _systemTrayService?.Dispose();
        }

        private void SetResponsiveInitialSize()
        {
            var workArea = SystemParameters.WorkArea;

            // Start at approximately 80% of the available work area.
            double targetWidth = workArea.Width * 0.80;
            double targetHeight = workArea.Height * 0.80;

            // Keep the initial window within sensible bounds.
            Width = Math.Clamp(targetWidth, MinWidth, 1400);
            Height = Math.Clamp(targetHeight, MinHeight, 900);
        }
    }
}
