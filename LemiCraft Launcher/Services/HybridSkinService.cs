using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LemiCraft_Launcher.Services
{
    public static class HybridSkinService
    {
        public static async Task<List<SkinLibraryItem>?> GetUserSkinsAsync(
            UserProfile profile,
            bool forceRefresh = false)
        {
            if (profile.Provider == "Ely.by")
                return await GetElybySkinsWithMetadataAsync(profile, forceRefresh);
            else
                return await GetLicenseSkinsAsync(profile.Username, forceRefresh);
        }

        private static async Task<List<SkinLibraryItem>> GetLicenseSkinsAsync(string username, bool forceRefresh)
        {
            var allSkins = await SkinLibraryService.GetUserSkinsAsync(username, forceRefresh);
            return allSkins.Where(s => !s.ElybyId.HasValue || s.ElybyId == 0).ToList();
        }

        private static async Task<List<SkinLibraryItem>?> GetElybySkinsWithMetadataAsync(
            UserProfile profile,
            bool forceRefresh)
        {
            if (!profile.HasValidElybyCookies())
            {
                Debug.WriteLine("⚠️ Ely.by cookies expired or missing");
                return null;
            }

            var cookies = new ElyCookies
            {
                PhpSessId = profile.ElybyPhpSessId,
                Identity = profile.ElybyIdentity
            };

            var elySkinsRaw = await ElyByCookieService.GetUserSkinsAsync(profile.Username, cookies);

            if (elySkinsRaw == null)
            {
                Debug.WriteLine("❌ Failed to fetch from Ely.by");
                return null;
            }

            var dbSkins = await SkinLibraryService.GetUserSkinsAsync(profile.Username, forceRefresh);

            var result = new List<SkinLibraryItem>();

            foreach (var elySkin in elySkinsRaw)
            {
                var dbSkin = dbSkins.FirstOrDefault(s => s.ElybyId == elySkin.Id);

                result.Add(new SkinLibraryItem
                {
                    Id = dbSkin?.Id ?? 0,
                    ElybyId = elySkin.Id,
                    Name = dbSkin?.Name ?? $"Скин #{elySkin.Id}",
                    Model = elySkin.IsSlim ? "alex" : "steve",
                    FileUrl = dbSkin?.FileUrl ?? elySkin.SkinUrl,
                    ThumbnailUrl = dbSkin?.ThumbnailUrl ?? elySkin.SkinUrl,
                    IsActive = dbSkin?.IsActive ?? false,
                    CreatedAt = dbSkin?.CreatedAt ?? DateTime.Now
                });

                if (dbSkin == null)
                {
                    _ = Task.Run(() => SyncSkinToDatabaseAsync(
                        elySkin.Id,
                        profile.Username,
                        elySkin.SkinUrl,
                        elySkin.IsSlim ? "alex" : "steve"
                    ));
                }
            }

            await SkinCacheService.SaveSkinsToCache(profile.Username, result);

            return result;
        }

        private static async Task SyncSkinToDatabaseAsync(int elybyId, string username, string skinUrl, string model)
        {
            try
            {
                var config = ConfigService.Load();
                var url = $"{config.ApiBaseUrl}/launcher/skins/sync";

                var body = new { elybyId, username, skinUrl, model };

                var httpClient = new HttpClient();
                var response = await httpClient.PostAsJsonAsync(url, body);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"✅ Synced Ely.by skin #{elybyId} to database");
                    SkinCacheService.InvalidateSkinsCache(username);
                }
                else
                    Debug.WriteLine($"⚠️ Sync failed: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Sync error: {ex.Message}");
            }
        }

        public static async Task<bool> ApplySkinAsync(SkinLibraryItem skin, UserProfile profile)
        {
            if (profile.Provider == "Ely.by")
            {
                if (!profile.HasValidElybyCookies())
                    return false;

                var cookies = new ElyCookies
                {
                    PhpSessId = profile.ElybyPhpSessId,
                    Identity = profile.ElybyIdentity
                };

                return await ElyByCookieService.WearSkinAsync(skin.ElybyId ?? 0, cookies);
            }
            else
            {
                return await SkinLibraryService.ApplySkinAsync(
                    skin.Id,
                    profile.Username,
                    profile.AccessToken,
                    profile.Provider,
                    profile.Uuid
                );
            }
        }

        public static async Task<bool> TakeOffSkinAsync(UserProfile profile)
        {
            if (profile.Provider != "Ely.by")
                return false;

            if (!profile.HasValidElybyCookies())
                return false;

            var cookies = new ElyCookies
            {
                PhpSessId = profile.ElybyPhpSessId,
                Identity = profile.ElybyIdentity
            };

            return await ElyByCookieService.WearSkinAsync(0, cookies);
        }

        public static async Task<bool> UploadSkinAsync(string filePath, string name, string model, UserProfile profile)
        {
            if (profile.Provider == "Ely.by")
                return await UploadToElybyAsync(filePath, name, model, profile);
            else
            {
                var result = await SkinLibraryService.UploadSkinAsync(
                    filePath,
                    name,
                    model,
                    profile.Username
                );

                return result?.Success ?? false;
            }
        }

        private static async Task<bool> UploadToElybyAsync(string filePath, string name, string model, UserProfile profile)
        {
            try
            {
                if (!profile.HasValidElybyCookies())
                {
                    Debug.WriteLine("❌ Ely.by cookies invalid or expired");
                    return false;
                }

                var cookies = new ElyCookies
                {
                    PhpSessId = profile.ElybyPhpSessId,
                    Identity = profile.ElybyIdentity
                };

                var isSlim = model.ToLower() == "alex";

                Debug.WriteLine("📤 Step 1/3: Uploading file to our server (elyby folder)...");
                var ourFileUrl = await UploadFileToOurServerAsync(filePath, provider: "elyby");

                if (string.IsNullOrEmpty(ourFileUrl))
                {
                    Debug.WriteLine("❌ Failed to upload file to our server");
                    return false;
                }

                Debug.WriteLine($"✅ File uploaded to our server: {ourFileUrl}");

                Debug.WriteLine("📤 Step 2/3: Uploading file to Ely.by...");
                var elybyId = await ElyByCookieService.UploadSkinAsync(
                    filePath,
                    name,
                    isSlim,
                    null,
                    cookies
                );

                if (!elybyId.HasValue)
                {
                    Debug.WriteLine("❌ Failed to upload to Ely.by");
                    return false;
                }

                Debug.WriteLine($"✅ Uploaded to Ely.by with ID: {elybyId}");

                Debug.WriteLine("📤 Step 3/3: Syncing to database...");
                await SyncSkinWithOurUrlAsync(
                    elybyId.Value,
                    profile.Username,
                    name,
                    ourFileUrl,
                    model
                );

                SkinCacheService.InvalidateSkinsCache(profile.Username);

                Debug.WriteLine("✅ Upload completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Upload error: {ex.Message}");
                return false;
            }
        }

        private static async Task<string?> UploadFileToOurServerAsync(string filePath, string provider = "elyby")
        {
            try
            {
                var config = ConfigService.Load();
                var url = $"{config.ApiBaseUrl}/launcher/skins/upload-file?provider={provider}";

                Debug.WriteLine($"📡 Uploading to: {url}");

                var boundary = "----LemiBoundary" + Guid.NewGuid().ToString("N");

                var handler = new SocketsHttpHandler();
                using var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.ExpectContinue = false;
                httpClient.DefaultRequestVersion = HttpVersion.Version11;

                using var fileStream = File.OpenRead(filePath);
                Debug.WriteLine($"📦 File size: {fileStream.Length} bytes");

                using var content = new MultipartFormDataContent(boundary);

                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

                content.Add(streamContent, "file", Path.GetFileName(filePath));

                content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content,
                    Version = HttpVersion.Version11
                };
                request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

                Debug.WriteLine("📤 Sending multipart request...");
                var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"📥 Response status: {response.StatusCode}");
                Debug.WriteLine($"📥 Response body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"❌ File upload failed: {response.StatusCode}");
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
                return result?.FileUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Upload file error: {ex.Message}");
                Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private static async Task SyncSkinWithOurUrlAsync(int elybyId, string username, string name, string fileUrl, string model)
        {
            try
            {
                var config = ConfigService.Load();
                var url = $"{config.ApiBaseUrl}/launcher/skins/sync";

                var body = new
                {
                    elybyId,
                    username,
                    name,
                    skinUrl = fileUrl,
                    model
                };

                var httpClient = new HttpClient();
                var response = await httpClient.PostAsJsonAsync(url, body);

                if (response.IsSuccessStatusCode)
                    Debug.WriteLine($"✅ Synced Ely.by skin #{elybyId} to database with our URL");
                else
                    Debug.WriteLine($"⚠️ Sync failed: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Sync error: {ex.Message}");
            }
        }

        public static async Task<bool> DeleteSkinAsync(SkinLibraryItem skin, UserProfile profile)
        {
            if (profile.Provider == "Ely.by")
            {
                if (!profile.HasValidElybyCookies())
                    return false;

                var cookies = new ElyCookies
                {
                    PhpSessId = profile.ElybyPhpSessId,
                    Identity = profile.ElybyIdentity
                };

                var elybyDeleted = await ElyByCookieService.DeleteSkinAsync(skin.ElybyId ?? 0, cookies);

                if (!elybyDeleted)
                    Debug.WriteLine("⚠️ Failed to delete from Ely.by, but will still delete from our DB");

                var dbDeleted = await SkinLibraryService.DeleteSkinAsync(skin.Id, profile.Username);

                if (dbDeleted)
                    SkinCacheService.InvalidateSkinsCache(profile.Username);

                return dbDeleted;
            }
            else
                return await SkinLibraryService.DeleteSkinAsync(skin.Id, profile.Username);
        }

        private class UploadFileResponse
        {
            public bool Success { get; set; }
            public string? FileUrl { get; set; }
        }
    }
}