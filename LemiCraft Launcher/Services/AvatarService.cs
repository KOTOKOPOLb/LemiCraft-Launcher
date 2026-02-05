using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace LemiCraft_Launcher.Services
{
    public static class AvatarService
    {
        private static readonly HttpClient _httpClient = new();
        
        private static readonly string CacheDir = 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft", "avatars");

        static AvatarService()
        {
            Directory.CreateDirectory(CacheDir);
        }
        public static async Task<string?> GetAvatarAsync(string nickname, bool use3D = true)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return null;

            try
            {
                var fileName = GetCachedFileName(nickname, use3D);
                var cachedPath = Path.Combine(CacheDir, fileName);

                if (File.Exists(cachedPath))
                {
                    var fileInfo = new FileInfo(cachedPath);
                    if (DateTime.Now - fileInfo.LastWriteTime < TimeSpan.FromHours(24))
                        return cachedPath;
                }

                var url = $"https://lemicraft.ru/api/avatar/{nickname}?3d={use3D.ToString().ToLower()}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(cachedPath, imageBytes);

                return cachedPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки аватара: {ex.Message}");
                return null;
            }
        }
        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDir))
                {
                    foreach (var file in Directory.GetFiles(CacheDir))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void CleanOldAvatars()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-7);

                foreach (var file in Directory.GetFiles(CacheDir))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                            File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }
        public static double GetCacheSizeMB()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                    return 0;

                long totalBytes = 0;
                foreach (var file in Directory.GetFiles(CacheDir))
                {
                    var fileInfo = new FileInfo(file);
                    totalBytes += fileInfo.Length;
                }

                return totalBytes / (1024.0 * 1024.0);
            }
            catch
            {
                return 0;
            }
        }

        private static string GetCachedFileName(string nickname, bool use3D)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(nickname.ToLower()));
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
            
            var type = use3D ? "3d" : "2d";
            return $"{hashString}_{type}.png";
        }

        public static async Task PreloadAvatarAsync(string nickname, bool use3D = true)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await GetAvatarAsync(nickname, use3D);
                }
                catch { }
            });
        }
    }
}
