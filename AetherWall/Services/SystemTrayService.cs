using AetherWall.ViewModels;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Input;
using System.Windows.Forms;

namespace AetherWall.Services
{
    public sealed class SystemTrayService : IDisposable
    {
        private readonly MainViewModel _viewModel;
        private readonly Action _showWindow;
        private readonly Action _exitApplication;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly Icon _icon;
        private readonly ToolStripMenuItem _pauseItem;
        private readonly ToolStripMenuItem _stopWallpaperItem;
        private bool _disposed;

        public SystemTrayService(
            MainViewModel viewModel,
            Action showWindow,
            Action exitApplication)
        {
            _viewModel = viewModel;
            _showWindow = showWindow;
            _exitApplication = exitApplication;
            _icon = LoadIcon();

            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add(
                "Open AetherWall",
                null,
                (_, _) => _showWindow());
            _contextMenu.Items.Add(new ToolStripSeparator());

            _pauseItem = new ToolStripMenuItem();
            _pauseItem.Click += (_, _) =>
                ExecuteCommand(_viewModel.ToggleManualPauseCommand);
            _contextMenu.Items.Add(_pauseItem);

            _stopWallpaperItem = new ToolStripMenuItem(
                "Stop wallpaper",
                null,
                (_, _) => ExecuteCommand(_viewModel.StopWallpaperCommand));
            _contextMenu.Items.Add(_stopWallpaperItem);

            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(
                "Exit",
                null,
                (_, _) => _exitApplication());
            _contextMenu.Opening += (_, _) => RefreshMenuState();

            _notifyIcon = new NotifyIcon
            {
                Icon = _icon,
                Text = "AetherWall",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };
            _notifyIcon.MouseClick += OnTrayIconMouseClick;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RefreshMenuState();
        }

        private static Icon LoadIcon()
        {
            Uri iconUri = new(
                "pack://application:,,,/Assets/aetherwall.ico",
                UriKind.Absolute);

            var resource =
                System.Windows.Application.GetResourceStream(iconUri);

            if (resource != null)
            {
                using Stream stream = resource.Stream;
                using var sourceIcon = new Icon(stream);
                return (Icon)sourceIcon.Clone();
            }

            return (Icon)SystemIcons.Application.Clone();
        }

        private void OnTrayIconMouseClick(
            object? sender,
            System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _showWindow();
        }

        private void OnViewModelPropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.PauseResumeLabel) ||
                e.PropertyName == nameof(MainViewModel.CurrentWallpaper))
            {
                RefreshMenuState();
            }
        }

        private void RefreshMenuState()
        {
            _pauseItem.Text = _viewModel.PauseResumeLabel;
            _pauseItem.Enabled =
                _viewModel.ToggleManualPauseCommand.CanExecute(null);
            _stopWallpaperItem.Enabled =
                _viewModel.CurrentWallpaper != null;
        }

        private static void ExecuteCommand(ICommand command)
        {
            if (command.CanExecute(null))
                command.Execute(null);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _notifyIcon.MouseClick -= OnTrayIconMouseClick;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _icon.Dispose();
        }
    }
}
