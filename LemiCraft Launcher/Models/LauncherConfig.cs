namespace LemiCraft_Launcher.Models
{
    public class LauncherConfig
    {
        public int RamGb { get; set; } = 4;
        public string JvmArgs { get; set; } = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions";
        public string JavaPath { get; set; } = "Автоопределение";
        public string GamePath { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LemiCraft"
        );

        public int LauncherBehavior { get; set; } = 2;

        public bool ShowLogs { get; set; }
        public bool AutoConnect { get; set; }

        public string ApiBaseUrl { get; set; } = "https://lemicraft.ru/api";

        public string? AuthlibInjectorPath { get; set; }
        public string? AuthlibInjectorDownloadUrl { get; set; } = "https://authlib-injector.yushi.moe/artifact/latest.json";
    }
}