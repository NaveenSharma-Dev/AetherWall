using AetherWall.Models;
using System.IO;
using TagLib;

namespace AetherWall.Services
{
    public class VideoMetadataService
    {
        public void ReadMetadata(WallpaperItem wallpaper)
        {
            try
            {
                var fileInfo = new FileInfo(wallpaper.FilePath);

                wallpaper.FileSize = fileInfo.Length;
                wallpaper.LastModified = fileInfo.LastWriteTime;

                wallpaper.Format =
                    Path.GetExtension(wallpaper.FilePath)
                        .TrimStart('.')
                        .ToUpperInvariant();

                using var mediaFile =
                    TagLib.File.Create(wallpaper.FilePath);

                wallpaper.Duration =
                    mediaFile.Properties.Duration;

                wallpaper.Width =
                    mediaFile.Properties.VideoWidth;

                wallpaper.Height =
                    mediaFile.Properties.VideoHeight;
            }
            catch
            {
                // Metadata failure must not prevent
                // the wallpaper from appearing in the library.
            }
        }
    }
}