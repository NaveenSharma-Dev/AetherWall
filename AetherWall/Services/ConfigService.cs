using AetherWall.Models;
using System.IO;
using System.Text.Json;

namespace AetherWall.Services
{
    public class ConfigService
    {
        private readonly string _settingsPath;

        public ConfigService()
        {
            string folder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "AetherWall");

            Directory.CreateDirectory(folder);

            _settingsPath =
                Path.Combine(
                    folder,
                    "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return new AppSettings();

                string json =
                    File.ReadAllText(
                        _settingsPath);

                return JsonSerializer.Deserialize<AppSettings>(
                           json)
                       ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(
            AppSettings settings)
        {
            try
            {
                string json =
                    JsonSerializer.Serialize(
                        settings,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                File.WriteAllText(
                    _settingsPath,
                    json);
            }
            catch
            {
                // Settings failure must not
                // crash AetherWall.
            }
        }
    }
}