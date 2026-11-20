using System.Windows;

namespace AetherWall.Services
{
    public static class ThemeService
    {
        private const string DarkThemeSource =
            "/AetherWall;component/Themes/Colors.xaml";
        private const string LightThemeSource =
            "/AetherWall;component/Themes/LightColors.xaml";

        public static void Apply(bool useLightTheme)
        {
            Application? application = Application.Current;
            if (application == null)
                return;

            ResourceDictionary colors = new()
            {
                Source = new Uri(
                    useLightTheme ? LightThemeSource : DarkThemeSource,
                    UriKind.Relative)
            };

            var dictionaries = application.Resources.MergedDictionaries;
            for (int index = 0; index < dictionaries.Count; index++)
            {
                string? source = dictionaries[index].Source?.OriginalString;
                if (source?.EndsWith(
                        "Colors.xaml",
                        StringComparison.OrdinalIgnoreCase) != true)
                {
                    continue;
                }

                dictionaries[index] = colors;
                return;
            }

            dictionaries.Insert(0, colors);
        }
    }
}
