using AetherWall.Models;
using AetherWall.Native;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AetherWall.Views
{
    public partial class MonitorSelectionDialog : Window
    {
        public string WallpaperName { get; }

        public ObservableCollection<MonitorSelectionChoice> MonitorChoices { get; }

        public IReadOnlyList<MonitorBounds> SelectedMonitors =>
            MonitorChoices
                .Where(choice => choice.IsSelected)
                .Select(choice => choice.Monitor)
                .ToArray();

        public MonitorSelectionDialog(
            WallpaperItem wallpaper,
            IReadOnlyList<MonitorBounds> monitors)
        {
            InitializeComponent();

            WallpaperName = wallpaper.DisplayName;
            bool hasPrimary = monitors.Any(monitor => monitor.IsPrimary);

            MonitorChoices = new ObservableCollection<MonitorSelectionChoice>(
                monitors.Select((monitor, index) =>
                    new MonitorSelectionChoice(
                        monitor,
                        $"Display {index + 1}{(monitor.IsPrimary ? " (Primary)" : string.Empty)}",
                        $"{monitor.Width} × {monitor.Height}",
                        monitor.IsPrimary || (!hasPrimary && index == 0))));

            DataContext = this;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool selectAll = MonitorChoices.Any(choice => !choice.IsSelected);
            foreach (MonitorSelectionChoice choice in MonitorChoices)
                choice.IsSelected = selectAll;

            SelectAllButton.Content = selectAll ? "Clear selection" : "Select all";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!MonitorChoices.Any(choice => choice.IsSelected))
            {
                MessageBox.Show(
                    this,
                    "Select at least one display.",
                    "No display selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }
    }

    public sealed class MonitorSelectionChoice : INotifyPropertyChanged
    {
        private bool _isSelected;

        public MonitorBounds Monitor { get; }
        public string Title { get; }
        public string Resolution { get; }

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

        public MonitorSelectionChoice(
            MonitorBounds monitor,
            string title,
            string resolution,
            bool isSelected)
        {
            Monitor = monitor;
            Title = title;
            Resolution = resolution;
            _isSelected = isSelected;
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
