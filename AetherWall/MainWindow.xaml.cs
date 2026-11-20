using AetherWall.ViewModels;
using System;
using System.Windows;

namespace AetherWall
{
    public partial class MainWindow : Window
    {
        private readonly AetherWall.Models.AppSettings _settings;
        private AetherWall.Services.SystemTrayService? _systemTrayService;
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
                System.Diagnostics.Debug.WriteLine(
                    $"[AetherWall] Startup failed: {ex}");
                StartupStatusText.Text =
                    "AetherWall could not finish starting. Restart the app to try again.";
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
