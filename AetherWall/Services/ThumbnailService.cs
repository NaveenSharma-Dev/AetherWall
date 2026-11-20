using AetherWall.Native;
using AetherWall.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AetherWall.Services
{
    public class ThumbnailService
    {
        private readonly string _thumbnailFolder;
        private readonly SemaphoreSlim _generationGate = new(2, 2);

        public ThumbnailService()
        {
            _thumbnailFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "AetherWall",
                "Thumbnails");

            Directory.CreateDirectory(_thumbnailFolder);
        }

        public async Task<string?> GetOrCreateThumbnailAsync(
            WallpaperItem wallpaper)
        {
            string cachePath = GetCachePath(wallpaper);

            if (File.Exists(cachePath))
                return cachePath;

            await _generationGate.WaitAsync();
            try
            {
                if (File.Exists(cachePath))
                    return cachePath;

                return await Task.Run(() => GenerateThumbnail(
                    wallpaper.FilePath,
                    cachePath));
            }
            finally
            {
                _generationGate.Release();
            }
        }

        private string GetCachePath(WallpaperItem wallpaper)
        {
            string sourceKey =
                $"{Path.GetFullPath(wallpaper.FilePath)}|" +
                $"{wallpaper.FileSize}|" +
                $"{wallpaper.LastModified.Ticks}";

            byte[] hash =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(sourceKey));

            string fileName =
                Convert.ToHexString(hash).ToLowerInvariant();

            return Path.Combine(
                _thumbnailFolder,
                $"{fileName}.jpg");
        }

        private string? GenerateThumbnail(
            string videoPath,
            string outputPath)
        {
            try
            {
                Guid factoryGuid =
                    typeof(Shell32.IShellItemImageFactory).GUID;

                int result =
                    Shell32.SHCreateItemFromParsingName(
                        videoPath,
                        IntPtr.Zero,
                        ref factoryGuid,
                        out var factory);

                if (result != 0 || factory == null)
                    return null;

                IntPtr hBitmap = IntPtr.Zero;

                try
                {
                    result = factory.GetImage(
                        new Shell32.SIZE(480, 270),
                        Shell32.SIIGBF.THUMBNAILONLY |
                        Shell32.SIIGBF.BIGGERSIZEOK,
                        out hBitmap);

                    if (result != 0 || hBitmap == IntPtr.Zero)
                        return null;

                    BitmapSource bitmap =
                        Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                    bitmap.Freeze();

                    var encoder = new JpegBitmapEncoder();

                    encoder.Frames.Add(
                        BitmapFrame.Create(bitmap));

                    using var stream =
                        new FileStream(
                            outputPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None);

                    encoder.Save(stream);

                    return outputPath;
                }
                finally
                {
                    if (hBitmap != IntPtr.Zero)
                        Shell32.DeleteObject(hBitmap);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
