using System.Windows;

namespace AetherWall.Views
{
    public partial class RenameWallpaperDialog : Window
    {
        public string DisplayName => DisplayNameTextBox.Text.Trim();

        public RenameWallpaperDialog(string currentDisplayName)
        {
            InitializeComponent();

            DisplayNameTextBox.Text = currentDisplayName;
            Loaded += (_, _) =>
            {
                DisplayNameTextBox.Focus();
                DisplayNameTextBox.SelectAll();
            };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
