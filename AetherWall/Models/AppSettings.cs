namespace AetherWall.Models
{
    public class AppSettings
    {
        public List<string> FavoriteWallpaperIds { get; set; } = new();

        public Dictionary<string, string> CustomWallpaperNames { get; set; } = new();

        public List<string> PlaylistWallpaperIds { get; set; } = new();

        public Dictionary<string, string> MonitorWallpaperIds { get; set; } = new();

        public Dictionary<string, WallpaperDisplayMode> MonitorDisplayModes { get; set; } = new();

        public bool PauseOnFullscreen { get; set; } = true;

        public bool PauseOnBattery { get; set; } = false;

        public bool AudioEnabled { get; set; } = false;

        public bool UseLightTheme { get; set; } = false;

        public int Volume { get; set; } = 50;

        public WallpaperDisplayMode WallpaperDisplayMode { get; set; }
            = WallpaperDisplayMode.Fit;

        public bool StartWithWindows { get; set; } = false;

        public bool StartMinimized { get; set; } = false;

        public bool RestoreLastWallpaper { get; set; } = true;
    }
}
