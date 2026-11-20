using System.Windows.Controls;

namespace AetherWall.Views
{
    public partial class LibraryView : UserControl
    {
        public LibraryView()
        {
            InitializeComponent();
        }

        private void OpenWallpaperMenu_Click(
            object sender,
            System.Windows.RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.ContextMenu is not ContextMenu menu)
            {
                return;
            }

            menu.PlacementTarget = button;
            menu.Placement =
                System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
