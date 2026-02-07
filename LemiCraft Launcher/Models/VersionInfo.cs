namespace LemiCraft_Launcher.Models
{
    public class LauncherVersion
    {
        public string Version { get; set; } = "1.0.0";
        public DateTime ReleaseDate { get; set; }
        public string DownloadUrl { get; set; } = "";
        public List<string> Changelog { get; set; } = new();
        public bool IsRequired { get; set; }
        public long FileSize { get; set; }
        public string Sha256Hash { get; set; } = "";
    }

    public class ModpackVersion
    {
        public string Version { get; set; } = "1.0.0";
        public DateTime ReleaseDate { get; set; }
        public string MinecraftVersion { get; set; } = "1.21.10";
        public string FabricVersion { get; set; } = "0.18.4";
        public List<string> Changelog { get; set; } = new();
        public ModpackUpdateType UpdateType { get; set; }
        public Dictionary<string, long> FileSizes { get; set; } = new();
        public string DownloadUrl { get; set; } = "";
        public string Sha256Hash { get; set; } = "";
    }

    public enum ModpackUpdateType
    {
        ModsOnly,
        ModsAndResources,
        Full
    }

    public class UpdateCheckResult
    {
        public bool LauncherUpdateAvailable { get; set; }
        public bool ModpackUpdateAvailable { get; set; }
        public LauncherVersion? LauncherVersion { get; set; }
        public ModpackVersion? ModpackVersion { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class LocalVersionInfo
    {
        public string LauncherVersion { get; set; } = "1.0.0";
        public string ModpackVersion { get; set; } = "0.0.0";
        public DateTime LastUpdateCheck { get; set; }
    }
}
