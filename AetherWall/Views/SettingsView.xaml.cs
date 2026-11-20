using AetherWall.Models;
using AetherWall.ViewModels;
using System.Windows.Controls;

namespace AetherWall.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView(
            AppSettings settings,
            LibraryViewModel libraryViewModel)
        {
            InitializeComponent();

            DataContext = new SettingsViewModel(
                settings,
                libraryViewModel);
        }
    }
}
