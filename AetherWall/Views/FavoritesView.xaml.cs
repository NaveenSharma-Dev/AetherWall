using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AetherWall.Views
{
    /// <summary>
    /// Interaction logic for FavoritesView.xaml
    /// </summary>
    public partial class FavoritesView : UserControl
    {
        public FavoritesView()
        {
            InitializeComponent();
        }

        private void OpenWallpaperMenu_Click(
            object sender,
            RoutedEventArgs e)
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
