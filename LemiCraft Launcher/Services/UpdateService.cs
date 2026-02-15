using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class UpdateService
    {
        private static readonly HttpClient _httpClient = new();
        private static string GetDataDir() => Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");

        private static string VersionFilePath => Path.Combine(GetDataDir(), "version.json");

        private static string GetApiUrl(string endpoint) =>
            $"{ConfigService.Load().ApiBaseUrl}/launcher/{endpoint}";

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var localVersion = AppVersion.Current;
                var installedModpackVersion = await ModpackVersionManager.GetInstalledVersionAsync();
                var launcherTask = CheckLauncherUpdateAsync(localVersion);
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
                var currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var currentExeDir = Path.GetDirectoryName(currentExePath) ?? "";
                var newExePath = Path.Combine(currentExeDir, "LemiCraft Launcher_new.exe");
                var oldExePath = Path.Combine(currentExeDir, "LemiCraft Launcher_old.exe");

                progress?.Report(0);

                using (var response = await _httpClient.GetAsync(version.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(newExePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                            progress?.Report((double)downloadedBytes / totalBytes * 100);
                    }
                }

                progress?.Report(100);

                var batchPath = Path.Combine(Path.GetTempPath(), "update_launcher.bat");
                var batchContent = $@"@echo off
timeout /t 1 /nobreak > nul
del ""{currentExePath.Replace(".dll", ".exe")}"" > nul 2>&1
move /y ""{newExePath}"" ""{currentExePath.Replace(".dll", ".exe")}"" > nul 2>&1
start """" ""{currentExePath.Replace(".dll", ".exe")}""
del ""{batchPath}""
";

                await File.WriteAllTextAsync(batchPath, batchContent);

                var psi = new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                System.Windows.Application.Current.Shutdown();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки обновления лаунчера: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> UpdateModpackAsync(
            ModpackVersion version,
            ModpackUpdateType updateType,
            IProgress<(string task, double progress)>? progress = null)
        {
            try
            {
                var config = ConfigService.Load();
                var gameDir = config.GamePath;

                Debug.WriteLine($"Обновление модпака в: {gameDir}");
                Debug.WriteLine($"Тип обновления: {updateType}");

                var driveInfo = new DriveInfo(Path.GetPathRoot(gameDir)!);
                var requiredSpace = version.FileSizes.Values.FirstOrDefault();

                if (driveInfo.AvailableFreeSpace < requiredSpace)
                {
                    progress?.Report(("Недостаточно места на диске!", 0));
                    Debug.WriteLine($"Недостаточно места: нужно {requiredSpace / (1024 * 1024)} MB, доступно {driveInfo.AvailableFreeSpace / (1024 * 1024)} MB");
                    return false;
                }

                progress?.Report(("Скачивание обновления...", 0));

                var downloadUrl = version.DownloadUrl.StartsWith("http")
                    ? version.DownloadUrl
                    : $"{config.ApiBaseUrl.Replace("/api", "")}{version.DownloadUrl}";

                Debug.WriteLine($"Скачивание с: {downloadUrl}");

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
                        {
                            var downloadProgress = (double)downloadedBytes / totalBytes * 50;
                            progress?.Report(("Скачивание...", downloadProgress));
                        }
                    }
                }

                progress?.Report(("Установка обновления...", 50));

                if (updateType == ModpackUpdateType.Full)
                {
                    Debug.WriteLine("Создание бэкапа...");
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
                        using var archive = ZipFile.OpenRead(tempZip);
                        archive.Dispose();
                    }
                    catch (InvalidDataException)
                    {
                        throw new Exception("Загруженный файл поврежден");
                    }
                });

                progress?.Report(("Распаковка файлов...", 60));

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(tempZip);
                    var totalEntries = archive.Entries.Count;
                    var processedEntries = 0;

                    foreach (var entry in archive.Entries)
                    {
                        var shouldExtract = ShouldExtractEntry(entry.FullName, updateType);

                        if (shouldExtract)
                        {
                            var destinationPath = Path.Combine(gameDir, entry.FullName);

                            if (string.IsNullOrEmpty(entry.Name))
                                Directory.CreateDirectory(destinationPath);
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                                entry.ExtractToFile(destinationPath, true);
                            }
                        }

                        processedEntries++;
                        var extractProgress = 60 + (processedEntries * 35.0 / totalEntries);
                        progress?.Report(($"Распаковка: {entry.Name}", extractProgress));
                    }
                });

                progress?.Report(("Обновление завершено!", 100));

                await ModpackVersionManager.SaveInstalledVersionAsync(
                    version.Version,
                    Path.GetFileName(downloadUrl),
                    requiredSpace
                );

                try { File.Delete(tempZip); } catch { }

                Debug.WriteLine("Модпак успешно обновлен!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления модпака: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                progress?.Report(($"Ошибка: {ex.Message}", 0));
                return false;
            }
        }

        private static bool ShouldExtractEntry(string entryPath, ModpackUpdateType updateType)
        {
            var normalizedPath = entryPath.Replace('\\', '/').ToLowerInvariant();

            switch (updateType)
            {
                case ModpackUpdateType.ModsOnly:
                    return normalizedPath.StartsWith("mods/");

                case ModpackUpdateType.ModsAndResources:
                    return normalizedPath.StartsWith("mods/") ||
                           normalizedPath.StartsWith("resourcepacks/") ||
                           normalizedPath.StartsWith("shaderpacks/");

                case ModpackUpdateType.Full:
                default:
                    return !normalizedPath.StartsWith("backups/") &&
                           !normalizedPath.Contains("/saves/") &&
                           normalizedPath != "options.txt" &&
                           normalizedPath != "servers.dat";
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

        public static void SaveLocalVersion(LocalVersionInfo version)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VersionFilePath)!);
                var json = JsonSerializer.Serialize(version, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VersionFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения версии: {ex.Message}");
            }
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

            public LauncherVersion ToLauncherVersion() => new()
            {
                Version = Version,
                ReleaseDate = ReleaseDate,
                DownloadUrl = DownloadUrl,
                Changelog = Changelog,
                IsRequired = IsRequired,
                FileSize = FileSize
            };
        }

        private class ModpackApiResponse
        {
            public bool Success { get; set; }
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public long FileSize { get; set; }
            public string Minecraft { get; set; } = "";
            public string Fabric { get; set; } = "";
            public List<string> Changelog { get; set; } = new();

            public ModpackVersion ToModpackVersion() => new()
            {
                Version = Version,
                DownloadUrl = DownloadUrl,
                MinecraftVersion = Minecraft,
                FabricVersion = Fabric,
                Changelog = Changelog,
                FileSizes = new Dictionary<string, long>
                {
                    { "full", FileSize },
                    { "mods", (long)(FileSize * 0.6) },
                    { "mods_resources", (long)(FileSize * 0.8) }
                }
            };
        }
    }
}