using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public class InstallProgress
    {
        public string Task { get; set; } = "";
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long DownloadedBytes { get; set; }
        public double Percent => TotalFiles > 0 ? (CompletedFiles * 100.0 / TotalFiles) : 0;
    }

    public static class MinecraftLauncherService
    {
        private static readonly HttpClient _httpClient = new();

        private static readonly string GameDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");

        private static readonly string AuthlibPath =
            Path.Combine(GameDir, "authlib-injector.jar");

        private const string MC_VERSION = "1.21.10";
        private const string FABRIC_LOADER = "0.18.4";

        public static event Action<InstallProgress>? ProgressChanged;

        public static async Task<bool> IsInstalledAsync()
        {
            try
            {
                var path = new MinecraftPath(GameDir);
                var launcher = new MinecraftLauncher(path);

                var fabricVersion = FabricInstaller.GetVersionName(MC_VERSION, FABRIC_LOADER);
                var versions = await launcher.GetAllVersionsAsync();

                return versions.Any(v => v.Name == fabricVersion) && File.Exists(AuthlibPath);
            }
            catch
            {
                return false;
            }
        }

        public static async Task InstallAsync()
        {
            var progress = new InstallProgress();

            try
            {
                var path = new MinecraftPath(GameDir);
                var launcher = new MinecraftLauncher(path);

                progress.Task = "Проверка Java...";
                ProgressChanged?.Invoke(progress);

                progress.Task = "Загрузка Minecraft...";
                ProgressChanged?.Invoke(progress);

                launcher.FileProgressChanged += (s, e) =>
                {
                    progress.Task = $"Загрузка: {e.Name}";
                    progress.TotalFiles = e.TotalTasks;
                    progress.CompletedFiles = e.ProgressedTasks;
                    ProgressChanged?.Invoke(progress);
                };

                launcher.ByteProgressChanged += (s, e) =>
                {
                    progress.TotalBytes = e.TotalBytes;
                    progress.DownloadedBytes = e.ProgressedBytes;
                    ProgressChanged?.Invoke(progress);
                };

                await launcher.InstallAsync(MC_VERSION);

                progress.Task = "Установка Fabric...";
                ProgressChanged?.Invoke(progress);

                var fabricInstaller = new FabricInstaller(_httpClient);
                await fabricInstaller.Install(MC_VERSION, FABRIC_LOADER, path);

                Directory.CreateDirectory(Path.Combine(GameDir, "mods"));
                Directory.CreateDirectory(Path.Combine(GameDir, "config"));

                progress.Task = "Загрузка authlib-injector...";
                ProgressChanged?.Invoke(progress);

                await DownloadAuthlibAsync();

                progress.Task = "Установка завершена!";
                progress.CompletedFiles = progress.TotalFiles;
                ProgressChanged?.Invoke(progress);
            }
            catch (Exception ex)
            {
                progress.Task = $"Ошибка: {ex.Message}";
                ProgressChanged?.Invoke(progress);
                throw;
            }
        }

    private static async Task DownloadAuthlibAsync()
    {
        string api = "https://api.github.com/repos/yushijinhun/authlib-injector/releases/latest";
        using var req = new HttpRequestMessage(HttpMethod.Get, api);
        req.Headers.UserAgent.ParseAdd("LemiCraftLauncher/1.0");

        using var resp = await _httpClient.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub API error: {resp.StatusCode}");

        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        if (!doc.RootElement.TryGetProperty("tag_name", out var tagElem))
            throw new InvalidOperationException("Не удалось получить tag_name последнего релиза authlib-injector.");

        var tag = tagElem.GetString() ?? throw new InvalidOperationException("Пустой tag_name.");
        var shortTag = tag.StartsWith("v") ? tag.Substring(1) : tag;

        var candidateUrl = $"https://github.com/yushijinhun/authlib-injector/releases/download/{tag}/authlib-injector-{shortTag}.jar";

        using var tryResp = await _httpClient.GetAsync(candidateUrl);
        if (tryResp.IsSuccessStatusCode)
        {
            var bytes = await tryResp.Content.ReadAsByteArrayAsync();
            Directory.CreateDirectory(Path.GetDirectoryName(AuthlibPath)!);
            await File.WriteAllBytesAsync(AuthlibPath, bytes);
            return;
        }

        if (doc.RootElement.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var nameElem)
                    && asset.TryGetProperty("browser_download_url", out var urlElem))
                {
                    var name = nameElem.GetString() ?? "";
                    var url = urlElem.GetString() ?? "";
                    if (name.Contains("authlib-injector") && name.EndsWith(".jar") && !string.IsNullOrEmpty(url))
                    {
                        using var aResp = await _httpClient.GetAsync(url);
                        if (aResp.IsSuccessStatusCode)
                        {
                            var bytes = await aResp.Content.ReadAsByteArrayAsync();
                            Directory.CreateDirectory(Path.GetDirectoryName(AuthlibPath)!);
                            await File.WriteAllBytesAsync(AuthlibPath, bytes);
                            return;
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException($"Не удалось скачать authlib-injector ни по {candidateUrl}, ни из ассетов последнего релиза.");
    }

    public static async Task<Process> LaunchAsync(UserProfile profile, LauncherConfig config)
        {
            var path = new MinecraftPath(GameDir);
            var launcher = new MinecraftLauncher(path);

            var fabricVersion = FabricInstaller.GetVersionName(MC_VERSION, FABRIC_LOADER);

            var session = new MSession
            {
                Username = profile.Username,
                AccessToken = profile.AccessToken,
                UUID = profile.Uuid
            };

            var options = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = config.RamGb * 1024
            };

            var jvmArgs = new List<string>();

            if (!string.IsNullOrWhiteSpace(config.JvmArgs))
                jvmArgs.AddRange(config.JvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (profile.Provider == "Ely.by" && File.Exists(AuthlibPath))
                jvmArgs.Add($"-javaagent:{AuthlibPath}=https://authserver.ely.by/api/authlib-injector");

            if (jvmArgs.Any())
                options.ExtraJvmArguments = jvmArgs.Select(a => new MArgument(a)).ToArray();

            if (!string.IsNullOrWhiteSpace(config.JavaPath) && config.JavaPath != "Автоопределение")
                options.JavaPath = config.JavaPath;

            if (config.AutoConnect)
            {
                options.ServerIp = "lemicraft.ru";
                options.ServerPort = 25565;
            }

            var process = await launcher.InstallAndBuildProcessAsync(fabricVersion, options);

            return process;
        }

        public static async Task<string> GetVersionAsync()
        {
            try
            {
                if (await IsInstalledAsync())
                    return $"Fabric {FABRIC_LOADER} (MC {MC_VERSION})";
                return "Не установлена";
            }
            catch
            {
                return "Ошибка проверки";
            }
        }

        public static string GetFabricVersionName() => $"fabric-loader-{FABRIC_LOADER}-{MC_VERSION}";
    }

}