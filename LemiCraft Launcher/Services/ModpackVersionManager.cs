using System.IO;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class ModpackVersionManager
    {
        private static readonly string VersionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LemiCraft",
            "modpack-version.json"
        );

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
                if (!File.Exists(VersionFilePath))
                    return "0.0.0";

                var json = await File.ReadAllTextAsync(VersionFilePath);
                var version = JsonSerializer.Deserialize<ModpackVersion>(json);

                return version?.Version ?? "0.0.0";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения версии модпака: {ex.Message}");
                return "0.0.0";
            }
        }

        public static async Task SaveInstalledVersionAsync(string version, string fileName, long fileSize)
        {
            try
            {
                var dir = Path.GetDirectoryName(VersionFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var versionInfo = new ModpackVersion
                {
                    Version = version,
                    FileName = fileName,
                    InstalledAt = DateTime.Now,
                    FileSize = fileSize
                };

                var json = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(VersionFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Версия модпака сохранена: {version}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения версии модпака: {ex.Message}");
            }
        }

        public static async Task<ModpackVersion?> GetInstalledModpackInfoAsync()
        {
            try
            {
                if (!File.Exists(VersionFilePath))
                    return null;

                var json = await File.ReadAllTextAsync(VersionFilePath);
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