using AetherWall.ViewModels;
using System.Windows;

namespace AetherWall
{
    public partial class App : Application
    {
        protected override void OnExit(
            ExitEventArgs e)
        {
            if (Current.MainWindow?.DataContext
                is MainViewModel mainViewModel)
            {
                mainViewModel.Dispose();
            }

            base.OnExit(e);
        }
    }
}