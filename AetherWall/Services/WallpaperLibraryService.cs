using AetherWall.Models;
using System.IO;

namespace AetherWall.Services
{
    public class WallpaperLibraryService
    {
        private static readonly string[] SupportedExtensions =
        {
            ".mp4",
            ".mkv",
            ".webm",
            ".avi",
            ".mov",
            ".wmv",
            ".flv",
            ".m4v",
            ".mpeg",
            ".mpg",
            ".ts"
        };

        public IReadOnlyList<WallpaperItem> ScanFolder(string folderPath)
        {
            var wallpapers = new List<WallpaperItem>();

            if (!Directory.Exists(folderPath))
                return wallpapers;

            foreach (string file in Directory.EnumerateFiles(
                         folderPath,
                         "*.*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!IsSupported(file))
                    continue;

                FileInfo info = new FileInfo(file);

                wallpapers.Add(new WallpaperItem
                {
                    Id = CreateId(file),
                    FilePath = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    LastModified = info.LastWriteTime
                });
            }

            return wallpapers;
        }

        public bool IsSupported(string filePath)
        {
            string extension =
                Path.GetExtension(filePath);

            return SupportedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase);
        }

        public static string CreateId(string filePath)
        {
            return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        Path.GetFullPath(filePath))));
        }
    }
}
