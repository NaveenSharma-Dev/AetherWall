using AetherWall.Native;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace AetherWall.Views
{
    public sealed class DisplayIdentifyOverlay : Window
    {
        private readonly MonitorBounds _monitor;
        private readonly DispatcherTimer _timer;

        public DisplayIdentifyOverlay(
            MonitorBounds monitor,
            int displayNumber)
        {
            _monitor = monitor;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            _timer.Tick += OnTimerTick;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Left = monitor.Left;
            Top = monitor.Top;
            Width = monitor.Width;
            Height = monitor.Height;

            Content = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(184, 10, 13, 19)),
                Children =
                {
                    new Border
                    {
                        Width = 180,
                        Height = 180,
                        CornerRadius = new CornerRadius(18),
                        Background = new SolidColorBrush(Color.FromRgb(8, 127, 215)),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(3),
                        Child = new TextBlock
                        {
                            Text = displayNumber.ToString(),
                            FontSize = 92,
                            FontWeight = FontWeights.Light,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Center
                        },
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };

            Loaded += OnLoaded;
            Closed += (_, _) => _timer.Stop();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WallpaperNative.PositionIdentifyOverlay(
                new WindowInteropHelper(this).Handle,
                _monitor);
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            Close();
        }
    }
}
