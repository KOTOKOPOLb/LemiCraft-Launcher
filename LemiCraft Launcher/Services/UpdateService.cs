using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");
        private static readonly string VersionFilePath = Path.Combine(DataDir, "version.json");

        private static string GetApiUrl(string endpoint) =>
            $"{ConfigService.Load().ApiBaseUrl}/launcher/{endpoint}";

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var localVersion = LoadLocalVersion();
                var installedModpackVersion = await ModpackVersionManager.GetInstalledVersionAsync();
                var launcherTask = CheckLauncherUpdateAsync(localVersion.LauncherVersion);
                var modpackTask = CheckModpackUpdateAsync(installedModpackVersion);

                await Task.WhenAll(launcherTask, modpackTask);

                var launcherVersion = await launcherTask;
                var modpackVersion = await modpackTask;

                var result = new UpdateCheckResult
                {
                    LauncherVersion = launcherVersion,
                    ModpackVersion = modpackVersion,
                    LauncherUpdateAvailable = launcherVersion != null,
                    ModpackUpdateAvailable = modpackVersion != null
                };

                localVersion.LastUpdateCheck = DateTime.Now;
                SaveLocalVersion(localVersion);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
                return new UpdateCheckResult
                {
                    ErrorMessage = $"Ошибка проверки обновлений: {ex.Message}"
                };
            }
        }

        private static async Task<LauncherVersion?> CheckLauncherUpdateAsync(string currentVersion)
        {
            try
            {
                var url = GetApiUrl("version");
                var response = await _httpClient.GetStringAsync(url);

                var apiResponse = JsonSerializer.Deserialize<LauncherApiResponse>(response, JsonOptions);
                if (apiResponse == null || !apiResponse.Success)
                {
                    Debug.WriteLine("API launcher вернул success=false");
                    return null;
                }

                var latestVersion = apiResponse.ToLauncherVersion();

                if (latestVersion != null && IsNewerVersion(latestVersion.Version, currentVersion))
                    return latestVersion;

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки версии лаунчера: {ex.Message}");
                return null;
            }
        }

        private static async Task<ModpackVersion?> CheckModpackUpdateAsync(string currentVersion)
        {
            try
            {
                var url = GetApiUrl("modpack/version");
                var response = await _httpClient.GetStringAsync(url);

                Debug.WriteLine("Requesting: " + url);
                var apiResponse = JsonSerializer.Deserialize<ModpackApiResponse>(response, JsonOptions);
                Debug.WriteLine("ApiResponse: " + apiResponse);
                Debug.WriteLine("Response: " + response);
                if (apiResponse == null || !apiResponse.Success)
                {
                    Debug.WriteLine("API modpack вернул success=false");
                    return null;
                }

                var latestVersion = apiResponse.ToModpackVersion();

                if (latestVersion != null && IsNewerVersion(latestVersion.Version, currentVersion))
                    return latestVersion;

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки версии модпака: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> DownloadLauncherUpdateAsync(
            LauncherVersion version,
            IProgress<double>? progress = null)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "LemiCraft_Launcher_Update.exe");

                using var response = await _httpClient.GetAsync(version.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                        progress?.Report((double)downloadedBytes / totalBytes * 100);
                }

                if (!string.IsNullOrEmpty(version.Sha256Hash))
                {
                    var hash = await ComputeSha256Async(tempPath);
                    if (!hash.Equals(version.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempPath);
                        return false;
                    }
                }

                StartUpdate(tempPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StartUpdate(string updateFilePath)
        {
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var batchPath = Path.Combine(Path.GetTempPath(), "update.bat");

            var batchContent =
$@"@echo off
timeout /t 2 /nobreak > nul
del ""{currentExe}""
move /y ""{updateFilePath}"" ""{currentExe}""
start """" ""{currentExe}""
del ""{batchPath}""
";

            File.WriteAllText(batchPath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Environment.Exit(0);
        }

        public static async Task<bool> UpdateModpackAsync(
            ModpackVersion version, 
            ModpackUpdateType updateType, 
            IProgress<(string task, double progress)>? progress = null)
        {
            try
            {
                var gameDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LemiCraft"
                );

                var driveInfo = new DriveInfo(Path.GetPathRoot(gameDir));
                if (driveInfo.AvailableFreeSpace < version.FileSizes.Values.FirstOrDefault())
                {
                    progress?.Report(("Недостаточно места на диске!", 0));
                    return false;
                }

                progress?.Report(("Скачивание обновления...", 0));

                var downloadUrl = version.DownloadUrl.StartsWith("http")
                    ? version.DownloadUrl
                    : $"{ConfigService.Load().ApiBaseUrl.Replace("/api", "")}{version.DownloadUrl}";

                var tempZip = Path.Combine(Path.GetTempPath(), $"modpack_{DateTime.Now:yyyyMMddHHmmss}.zip");

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempZip, FileMode.Create);

                    var buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                            progress?.Report(("Скачивание...", (double)downloadedBytes / totalBytes * 50));
                    }
                }

                progress?.Report(("Установка обновления...", 50));

                if (updateType == ModpackUpdateType.Full)
                {
                    var backupDir = Path.Combine(gameDir, "backups", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                    Directory.CreateDirectory(backupDir);

                    var filesToBackup = new[] { "config", "options.txt" };
                    foreach (var item in filesToBackup)
                    {
                        var sourcePath = Path.Combine(gameDir, item);
                        if (Directory.Exists(sourcePath))
                            CopyDirectory(sourcePath, Path.Combine(backupDir, item));
                        else if (File.Exists(sourcePath))
                            File.Copy(sourcePath, Path.Combine(backupDir, item), true);
                    }
                }

                await Task.Run(() =>
                {
                    try
                    {
                        using var archive = System.IO.Compression.ZipFile.OpenRead(tempZip);
                        archive.Dispose();
                    }
                    catch (InvalidDataException)
                    {
                        throw new Exception("Загруженный файл поврежден");
                    }

                    System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, gameDir, true);
                });

                progress?.Report(("Обновление завершено!", 100));

                await ModpackVersionManager.SaveInstalledVersionAsync(
                    version.Version,
                    version.DownloadUrl,
                    version.FileSizes.Values.FirstOrDefault()
                );

                try { File.Delete(tempZip); } catch { }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления модпака: {ex.Message}");
                progress?.Report(($"Ошибка: {ex.Message}", 0));
                return false;
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var dest = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dest = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }

        private static async Task<string> ComputeSha256Async(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await Task.Run(() => sha256.ComputeHash(stream));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                var newParts = newVersion.Split('.').Select(int.Parse).ToArray();
                var currentParts = currentVersion.Split('.').Select(int.Parse).ToArray();

                for (int i = 0; i < Math.Min(newParts.Length, currentParts.Length); i++)
                {
                    if (newParts[i] > currentParts[i]) return true;
                    if (newParts[i] < currentParts[i]) return false;
                }

                return newParts.Length > currentParts.Length;
            }
            catch
            {
                return false;
            }
        }

        public static LocalVersionInfo LoadLocalVersion()
        {
            try
            {
                if (!File.Exists(VersionFilePath))
                    return new LocalVersionInfo
                    {
                        LauncherVersion = AppVersion.Current,
                        ModpackVersion = "0.0.0"
                    };

                var json = File.ReadAllText(VersionFilePath);
                return JsonSerializer.Deserialize<LocalVersionInfo>(json) ?? new LocalVersionInfo
                {
                    LauncherVersion = AppVersion.Current,
                    ModpackVersion = "0.0.0"
                };
            }
            catch
            {
                return new LocalVersionInfo
                {
                    LauncherVersion = AppVersion.Current,
                    ModpackVersion = "0.0.0"
                };
            }
        }

        private static void SaveLocalVersion(LocalVersionInfo version)
        {
            Directory.CreateDirectory(DataDir);
            var json = JsonSerializer.Serialize(version, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(VersionFilePath, json);
        }

        public static void SetModpackVersion(string version)
        {
            var localVersion = LoadLocalVersion();
            localVersion.ModpackVersion = version;
            SaveLocalVersion(localVersion);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private class LauncherApiResponse
        {
            public bool Success { get; set; }
            public string Version { get; set; } = "";
            public DateTime ReleaseDate { get; set; }
            public string DownloadUrl { get; set; } = "";
            public List<string> Changelog { get; set; } = new();
            public bool IsRequired { get; set; }
            public long FileSize { get; set; }
            public string FileName { get; set; } = "";

            public LauncherVersion ToLauncherVersion()
            {
                return new LauncherVersion
                {
                    Version = Version,
                    ReleaseDate = ReleaseDate,
                    DownloadUrl = DownloadUrl,
                    Changelog = Changelog,
                    IsRequired = IsRequired,
                    FileSize = FileSize,
                    Sha256Hash = ""
                };
            }
        }

        private class ModpackApiResponse
        {
            public bool Success { get; set; }
            public string Version { get; set; } = "";
            public string FileName { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public long FileSize { get; set; }
            public DateTime ReleaseDate { get; set; }
            public string Minecraft { get; set; } = "";
            public string Fabric { get; set; } = "";
            public List<string> Changelog { get; set; } = new();

            public ModpackVersion ToModpackVersion()
            {
                return new ModpackVersion
                {
                    Version = Version,
                    ReleaseDate = ReleaseDate,
                    MinecraftVersion = Minecraft,
                    FabricVersion = Fabric,
                    Changelog = Changelog,
                    DownloadUrl = DownloadUrl,
                    FileSizes = new Dictionary<string, long>
                    {
                        { "full", FileSize }
                    },
                    UpdateType = ModpackUpdateType.Full
                };
            }
        }
    }
}