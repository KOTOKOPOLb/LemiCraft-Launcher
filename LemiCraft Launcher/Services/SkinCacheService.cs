using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public static class SkinCacheService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly string _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LemiCraft",
            "Cache",
            "Skins"
        );

        private static readonly Dictionary<string, CachedSkinsData> _skinsCache = [];
        private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        static SkinCacheService()
        {
            Directory.CreateDirectory(_cacheDir);
            Directory.CreateDirectory(Path.Combine(_cacheDir, "Images"));
        }

        public static async Task<List<SkinLibraryItem>?> GetCachedSkinsAsync(string username)
        {
            if (_skinsCache.TryGetValue(username, out var cached))
            {
                if (DateTime.Now - cached.CachedAt < _cacheLifetime)
                {
                    Debug.WriteLine($"✅ Using cached skins for {username} ({cached.Skins.Count} items)");
                    return cached.Skins;
                }
            }

            var cacheFile = Path.Combine(_cacheDir, $"{username}_skins.json");
            if (File.Exists(cacheFile))
            {
                try
                {
                    var fileInfo = new FileInfo(cacheFile);
                    if (DateTime.Now - fileInfo.LastWriteTime < _cacheLifetime)
                    {
                        var json = await File.ReadAllTextAsync(cacheFile);
                        var skins = JsonSerializer.Deserialize<List<SkinLibraryItem>>(json);

                        if (skins != null)
                        {
                            _skinsCache[username] = new CachedSkinsData
                            {
                                Skins = skins,
                                CachedAt = fileInfo.LastWriteTime
                            };

                            Debug.WriteLine($"✅ Loaded skins from file cache for {username}");
                            return skins;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ Failed to load cache: {ex.Message}");
                }
            }

            return null;
        }

        public static async Task SaveSkinsToCache(string username, List<SkinLibraryItem> skins)
        {
            try
            {
                _skinsCache[username] = new CachedSkinsData
                {
                    Skins = skins,
                    CachedAt = DateTime.Now
                };

                var cacheFile = Path.Combine(_cacheDir, $"{username}_skins.json");
                var json = JsonSerializer.Serialize(skins, _writeOptions);
                await File.WriteAllTextAsync(cacheFile, json);

                Debug.WriteLine($"💾 Saved {skins.Count} skins to cache for {username}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to save cache: {ex.Message}");
            }
        }

        public static void InvalidateSkinsCache(string username)
        {
            _skinsCache.Remove(username);

            var cacheFile = Path.Combine(_cacheDir, $"{username}_skins.json");
            if (File.Exists(cacheFile))
            {
                try
                {
                    File.Delete(cacheFile);
                    Debug.WriteLine($"🗑️ Invalidated skins cache for {username}");
                }
                catch { }
            }
        }

        public static async Task<string?> GetCachedImageAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            try
            {
                var fileName = GetCacheFileName(imageUrl);
                var cachedPath = Path.Combine(_cacheDir, "Images", fileName);

                if (File.Exists(cachedPath))
                {
                    var fileInfo = new FileInfo(cachedPath);
                    if (DateTime.Now - fileInfo.LastWriteTime < TimeSpan.FromHours(1))
                    {
                        Debug.WriteLine($"✅ Using cached image: {fileName}");
                        return cachedPath;
                    }
                    else
                    {
                        try { File.Delete(cachedPath); } catch { }
                    }
                }

                return await DownloadAndCacheImageAsync(imageUrl, cachedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Image cache error: {ex.Message}");
            }

            return null;
        }

        private static async Task<string?> DownloadAndCacheImageAsync(string imageUrl, string cachedPath)
        {
            try
            {
                Debug.WriteLine($"⬇️ Downloading image: {imageUrl}");

                var response = await _httpClient.GetAsync(imageUrl);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(cachedPath, bytes);

                    Debug.WriteLine($"💾 Cached image: {Path.GetFileName(cachedPath)} ({bytes.Length} bytes)");
                    return cachedPath;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"⏳ Image not ready yet (404): {imageUrl}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Download error: {ex.Message}");
            }

            return null;
        }

        private static string GetCacheFileName(string url)
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);

            if (!Path.HasExtension(fileName))
                fileName += ".png";

            return fileName;
        }

        public static void ClearImageCache()
        {
            try
            {
                var imagesDir = Path.Combine(_cacheDir, "Images");
                if (Directory.Exists(imagesDir))
                {
                    var files = Directory.GetFiles(imagesDir);
                    foreach (var file in files)
                    {
                        try { File.Delete(file); } catch { }
                    }
                    Debug.WriteLine($"🗑️ Cleared {files.Length} cached images");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to clear cache: {ex.Message}");
            }
        }

        private class CachedSkinsData
        {
            public List<SkinLibraryItem> Skins { get; set; } = [];
            public DateTime CachedAt { get; set; }
        }
    }
}