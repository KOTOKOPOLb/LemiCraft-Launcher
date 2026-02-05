using LemiCraft_Launcher.Models;
using System.IO;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class ConfigService
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");

        private static readonly string PathFile = System.IO.Path.Combine(Dir, "config.json");

        public static LauncherConfig Load()
        {
            try
            {
                if (!File.Exists(PathFile))
                    return new LauncherConfig();

                string json = File.ReadAllText(PathFile);
                return JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
            }
            catch
            {
                return new LauncherConfig();
            }
        }

        public static void Save(LauncherConfig config)
        {
            Directory.CreateDirectory(Dir);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(PathFile, JsonSerializer.Serialize(config, options));
        }
    }
}