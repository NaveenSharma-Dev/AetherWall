using System;
using System.IO;

namespace AetherWall.Services
{
    public static class StartupLogService
    {
        public static string? WriteDiagnostic(string message)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "AetherWall");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, "startup.log");
                File.AppendAllText(
                    path,
                    $"AetherWall diagnostic - {DateTimeOffset.Now:O}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}");
                return path;
            }
            catch
            {
                return null;
            }
        }

        public static string? WriteFailure(Exception exception)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "AetherWall");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, "startup.log");
                WriteDiagnostic($"Startup failure:{Environment.NewLine}{exception}");
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
