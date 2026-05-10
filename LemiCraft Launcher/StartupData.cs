using LemiCraft_Launcher.Models;

namespace LemiCraft_Launcher
{
    public static class StartupData
    {
        public static UpdateCheckResult? UpdateResult { get; set; }
        public static PreloaderForm? Preloader { get; set; }

        public static string? PendingImportCode { get; set; }

        public static event Action<string>? ImportCodeReceived;
        public static void RaiseImportCodeReceived(string code) => ImportCodeReceived?.Invoke(code);
    }
}
