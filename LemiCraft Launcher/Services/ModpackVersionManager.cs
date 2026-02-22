using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class ModpackVersionManager
    {
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        private static string GetVersionFilePath()
        {
            var config = ConfigService.Load();
            var gameDir = config.GamePath;
            return Path.Combine(gameDir, "modpack-version.json");
        }

        public class ModpackVersion
        {
            public string Version { get; set; } = "0.0.0";
            public string FileName { get; set; } = "";
            public DateTime InstalledAt { get; set; }
            public long FileSize { get; set; }
        }

        public static async Task<string> GetInstalledVersionAsync()
        {
            try
            {
                var versionFilePath = GetVersionFilePath();

                if (!File.Exists(versionFilePath))
                {
                    Debug.WriteLine($"Файл версии модпака не найден: {versionFilePath}");
                    return "0.0.0";
                }

                var json = await File.ReadAllTextAsync(versionFilePath);
                var version = JsonSerializer.Deserialize<ModpackVersion>(json);

                var versionString = version?.Version ?? "0.0.0";
                Debug.WriteLine($"Установленная версия модпака: {versionString}");

                return versionString;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка чтения версии модпака: {ex.Message}");
                return "0.0.0";
            }
        }

        public static async Task SaveInstalledVersionAsync(string version, string fileName, long fileSize)
        {
            try
            {
                var versionFilePath = GetVersionFilePath();
                var dir = Path.GetDirectoryName(versionFilePath);

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var versionInfo = new ModpackVersion
                {
                    Version = version,
                    FileName = fileName,
                    InstalledAt = DateTime.Now,
                    FileSize = fileSize
                };

                var json = JsonSerializer.Serialize(versionInfo, _writeOptions);

                await File.WriteAllTextAsync(versionFilePath, json);

                Debug.WriteLine($"Версия модпака сохранена: {version} в {versionFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения версии модпака: {ex.Message}");
            }
        }

        public static async Task<ModpackVersion?> GetInstalledModpackInfoAsync()
        {
            try
            {
                var versionFilePath = GetVersionFilePath();

                if (!File.Exists(versionFilePath))
                    return null;

                var json = await File.ReadAllTextAsync(versionFilePath);
                return JsonSerializer.Deserialize<ModpackVersion>(json);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> IsUpdateNeededAsync(string latestVersion)
        {
            var currentVersion = await GetInstalledVersionAsync();
            return CompareVersions(latestVersion, currentVersion) > 0;
        }

        private static int CompareVersions(string v1, string v2)
        {
            try
            {
                var parts1 = v1.Split('.');
                var parts2 = v2.Split('.');

                for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
                {
                    int num1 = i < parts1.Length ? int.Parse(parts1[i]) : 0;
                    int num2 = i < parts2.Length ? int.Parse(parts2[i]) : 0;

                    if (num1 > num2) return 1;
                    if (num1 < num2) return -1;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}