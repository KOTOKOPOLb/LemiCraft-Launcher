using LemiCraft_Launcher.Models;
using System.IO;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class ConfigService
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");

        private static readonly string PathFile = Path.Combine(Dir, "config.json");

        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        private static LauncherConfig? _cache;

        public static LauncherConfig Load()
        {
            if (_cache != null)
                return _cache;

            try
            {
                if (!File.Exists(PathFile))
                {
                    _cache = new LauncherConfig();
                    return _cache;
                }

                string json = File.ReadAllText(PathFile);
                _cache = JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
                return _cache;
            }
            catch
            {
                _cache = new LauncherConfig();
                return _cache;
            }
        }

        public static void Save(LauncherConfig config)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(PathFile, JsonSerializer.Serialize(config, _writeOptions));
            _cache = config;
        }

        public static void InvalidateCache() => _cache = null;
    }
}