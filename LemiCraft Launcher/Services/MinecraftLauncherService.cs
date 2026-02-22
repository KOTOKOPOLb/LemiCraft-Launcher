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

        private static string GetGameDir() => ConfigService.Load().GamePath;

        private static string GetAuthlibPath() => Path.Combine(GetGameDir(), "authlib-injector.jar");

        private const string MC_VERSION = "1.21.10";
        private const string FABRIC_LOADER = "0.18.4";

        public static event Action<InstallProgress>? ProgressChanged;

        private static MinecraftLauncher? _cachedLauncher = null;
        private static string? _cachedGameDir = null;

        private static MinecraftLauncher GetLauncher()
        {
            var currentGameDir = GetGameDir();

            if (_cachedLauncher == null || _cachedGameDir != currentGameDir)
            {
                var path = new MinecraftPath(currentGameDir);
                _cachedLauncher = new MinecraftLauncher(path);
                _cachedGameDir = currentGameDir;

                Debug.WriteLine($"Создан новый MinecraftLauncher для: {currentGameDir}");
            }

            return _cachedLauncher;
        }

        public static void ResetLauncherCache()
        {
            _cachedLauncher = null;
            _cachedGameDir = null;
            Debug.WriteLine("Кэш MinecraftLauncher сброшен");
        }

        public static async Task<bool> IsInstalledAsync()
        {
            try
            {
                var launcher = GetLauncher();
                var fabricVersion = FabricInstaller.GetVersionName(MC_VERSION, FABRIC_LOADER);
                var versions = await launcher.GetAllVersionsAsync();

                var isInstalled = versions.Any(v => v.Name == fabricVersion) && File.Exists(GetAuthlibPath());

                Debug.WriteLine($"Проверка установки: {isInstalled}");
                Debug.WriteLine($"Fabric версия: {fabricVersion}");
                Debug.WriteLine($"Authlib путь: {GetAuthlibPath()}");
                Debug.WriteLine($"Найдено версий: {versions.Count()}");

                return isInstalled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки установки: {ex.Message}");
                return false;
            }
        }

        public static async Task InstallAsync()
        {
            var progress = new InstallProgress();

            try
            {
                var gameDir = GetGameDir();
                Debug.WriteLine($"Установка Minecraft в: {gameDir}");

                Directory.CreateDirectory(gameDir);

                var launcher = GetLauncher();

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
                await fabricInstaller.Install(MC_VERSION, FABRIC_LOADER, new MinecraftPath(gameDir));

                Directory.CreateDirectory(Path.Combine(gameDir, "mods"));
                Directory.CreateDirectory(Path.Combine(gameDir, "config"));
                Directory.CreateDirectory(Path.Combine(gameDir, "resourcepacks"));
                Directory.CreateDirectory(Path.Combine(gameDir, "shaderpacks"));

                progress.Task = "Загрузка authlib-injector...";
                ProgressChanged?.Invoke(progress);

                var authlibSuccess = await DownloadAuthlibAsync();
                if (!authlibSuccess)
                    Debug.WriteLine("⚠️ authlib-injector не скачан — Ely.by авторизация не будет работать");

                progress.Task = "Установка завершена!";
                progress.CompletedFiles = progress.TotalFiles;
                ProgressChanged?.Invoke(progress);

                ResetLauncherCache();

                Debug.WriteLine("Установка Minecraft завершена успешно!");
            }
            catch (Exception ex)
            {
                progress.Task = $"Ошибка: {ex.Message}";
                ProgressChanged?.Invoke(progress);
                Debug.WriteLine($"Ошибка установки: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private static async Task<bool> DownloadAuthlibAsync()
        {
            try
            {
                string api = "https://api.github.com/repos/yushijinhun/authlib-injector/releases/latest";
                using var req = new HttpRequestMessage(HttpMethod.Get, api);
                req.Headers.UserAgent.ParseAdd("LemiCraftLauncher/1.0");

                using var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GitHub API error: {resp.StatusCode}");
                    return false;
                }

                using var stream = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                if (!doc.RootElement.TryGetProperty("tag_name", out var tagElem))
                {
                    Debug.WriteLine("Не удалось получить tag_name");
                    return false;
                }

                var tag = tagElem.GetString() ?? "";
                var shortTag = tag.StartsWith('v') ? tag[1..] : tag;

                var candidateUrl = $"https://github.com/yushijinhun/authlib-injector/releases/download/{tag}/authlib-injector-{shortTag}.jar";

                using var tryResp = await _httpClient.GetAsync(candidateUrl);
                if (tryResp.IsSuccessStatusCode)
                {
                    var bytes = await tryResp.Content.ReadAsByteArrayAsync();
                    var authlibPath = GetAuthlibPath();
                    Directory.CreateDirectory(Path.GetDirectoryName(authlibPath)!);
                    await File.WriteAllBytesAsync(authlibPath, bytes);
                    Debug.WriteLine($"Authlib скачан: {authlibPath}");
                    return true;
                }

                if (doc.RootElement.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var nameElem) &&
                            asset.TryGetProperty("browser_download_url", out var urlElem))
                        {
                            var name = nameElem.GetString() ?? "";
                            var url = urlElem.GetString() ?? "";

                            if (name.Contains("authlib-injector") && name.EndsWith(".jar") && !string.IsNullOrEmpty(url))
                            {
                                using var aResp = await _httpClient.GetAsync(url);
                                if (aResp.IsSuccessStatusCode)
                                {
                                    var bytes = await aResp.Content.ReadAsByteArrayAsync();
                                    var authlibPath = GetAuthlibPath();
                                    Directory.CreateDirectory(Path.GetDirectoryName(authlibPath)!);
                                    await File.WriteAllBytesAsync(authlibPath, bytes);
                                    Debug.WriteLine($"Authlib скачан через assets: {authlibPath}");
                                    return true;
                                }
                            }
                        }
                    }
                }

                Debug.WriteLine("Не удалось скачать authlib-injector");
                return false;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Authlib download failed: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error downloading authlib: {ex.Message}");
                return false;
            }
        }

        public static async Task<Process> LaunchAsync(UserProfile profile, LauncherConfig config)
        {
            try
            {
                var gameDir = GetGameDir();
                Debug.WriteLine($"Запуск Minecraft из: {gameDir}");

                var launcher = GetLauncher();

                await launcher.GetAllVersionsAsync();

                var fabricVersion = FabricInstaller.GetVersionName(MC_VERSION, FABRIC_LOADER);
                Debug.WriteLine($"Используем версию: {fabricVersion}");

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

                var authlibPath = GetAuthlibPath();
                if (profile.Provider == "Ely.by" && File.Exists(authlibPath))
                {
                    jvmArgs.Add($"-javaagent:{authlibPath}=https://authserver.ely.by/api/authlib-injector");
                    Debug.WriteLine("Используется Ely.by authlib");
                }

                if (jvmArgs.Count > 0)
                    options.ExtraJvmArguments = jvmArgs.Select(a => new MArgument(a)).ToArray();

                if (!string.IsNullOrWhiteSpace(config.JavaPath) && config.JavaPath != "Автоопределение")
                {
                    options.JavaPath = config.JavaPath;
                    Debug.WriteLine($"Используется Java: {config.JavaPath}");
                }

                if (config.AutoConnect)
                {
                    options.ServerIp = "lemicraft.ru";
                    options.ServerPort = 25565;
                    Debug.WriteLine("Автоподключение к серверу включено");
                }

                Debug.WriteLine($"RAM: {config.RamGb} GB");
                Debug.WriteLine($"JVM Args: {config.JvmArgs}");

                var process = await launcher.InstallAndBuildProcessAsync(fabricVersion, options);

                Debug.WriteLine("Minecraft процесс запущен успешно!");
                return process;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка запуска Minecraft: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }
}