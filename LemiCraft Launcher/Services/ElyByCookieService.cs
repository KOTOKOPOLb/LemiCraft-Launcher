using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LemiCraft_Launcher.Services
{
    public class ElyCookies
    {
        public string? PhpSessId { get; set; }
        public string? Identity { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(PhpSessId) || !string.IsNullOrEmpty(Identity);

        public string ToCookieHeader()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(PhpSessId)) parts.Add($"PHPSESSID={PhpSessId}");
            if (!string.IsNullOrEmpty(Identity)) parts.Add($"identity={Identity}");
            return string.Join("; ", parts);
        }
    }

    public class ElySkinItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("skin_url")]
        public string SkinUrl { get; set; } = "";

        [JsonPropertyName("is_slim")]
        public bool IsSlim { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("count_views_total")]
        public int CountViews { get; set; }
    }

    public class ElySkinsList
    {
        [JsonPropertyName("items")]
        public List<ElySkinItem> Items { get; set; } = new();

        [JsonPropertyName("total_items")]
        public int TotalItems { get; set; }
    }

    public static class ElyByCookieService
    {
        private static ElyCookies? _cachedCookies;

        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = false
        });

        public static void SetCookies(ElyCookies cookies)
        {
            _cachedCookies = cookies;
            Debug.WriteLine($"✅ Ely.by cookies saved (PHPSESSID={!string.IsNullOrEmpty(cookies.PhpSessId)}, identity={!string.IsNullOrEmpty(cookies.Identity)})");
        }

        public static void ClearCookies()
        {
            _cachedCookies = null;
            Debug.WriteLine("🗑️ Ely.by cookies cleared");
        }

        public static async Task<bool> ValidateCookiesAsync(ElyCookies cookies)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://ely.by/api/authserver/v1/profile");
                request.Headers.Add("Cookie", cookies.ToCookieHeader());
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Cookie validation error: {ex.Message}");
                return false;
            }
        }

        public static async Task<List<ElySkinItem>?> GetUserSkinsAsync(string username, ElyCookies cookies)
        {
            try
            {
                var url = $"https://ely.by/skins?uploader={Uri.EscapeDataString(username)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Cookie", cookies.ToCookieHeader());
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"❌ Failed to get skins page: {response.StatusCode}");
                    return null;
                }

                var html = await response.Content.ReadAsStringAsync();
                return ParseSkinsFromHtml(html);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetUserSkins error: {ex.Message}");
                return null;
            }
        }

        private static List<ElySkinItem>? ParseSkinsFromHtml(string html)
        {
            try
            {
                var match = Regex.Match(html,
                    @"alight\.service\.skins\s*=\s*(\{.+\})\s*<",
                    RegexOptions.Singleline);

                if (!match.Success)
                {
                    Debug.WriteLine("⚠️ Could not find 'alight.service.skins' in HTML");
                    return new List<ElySkinItem>();
                }

                var json = match.Groups[1].Value.Trim();
                Debug.WriteLine($"[Extracted JSON length]: {json.Length} chars");

                var skinsList = JsonSerializer.Deserialize<ElySkinsList>(json);
                Debug.WriteLine($"✅ Parsed {skinsList?.Items.Count ?? 0} skins from ely.by");
                return skinsList?.Items ?? new List<ElySkinItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ParseSkins error: {ex.Message}");
                return new List<ElySkinItem>();
            }
        }

        public static async Task<bool> WearSkinAsync(int skinId, ElyCookies cookies)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://ely.by/skins/wear");
                request.Headers.Add("Cookie", cookies.ToCookieHeader());
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.Add("Referer", "https://ely.by/skins");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");

                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("skinId", skinId.ToString())
                });

                var response = await _httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"📤 WearSkin response: {result}");

                return result.Contains("success_skin_change");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ WearSkin error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> TakeOffSkinAsync(ElyCookies cookies) => await WearSkinAsync(0, cookies);

        public static async Task<int?> UploadSkinAsync(
            string filePath,
            string name,
            bool isSlim,
            List<string>? tags,
            ElyCookies cookies)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                content.Add(new ByteArrayContent(fileBytes), "file", Path.GetFileName(filePath));

                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "https://ely.by/skins/upload");
                uploadRequest.Headers.Add("Cookie", cookies.ToCookieHeader());
                uploadRequest.Headers.Add("User-Agent", "Mozilla/5.0");
                uploadRequest.Headers.Add("Referer", "https://ely.by/skins/add");
                uploadRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
                uploadRequest.Content = content;

                var uploadResponse = await _httpClient.SendAsync(uploadRequest);
                var uploadResult = await uploadResponse.Content.ReadAsStringAsync();

                Debug.WriteLine($"📤 Upload response: {uploadResult}");

                var uploadJson = JsonSerializer.Deserialize<UploadResponse>(uploadResult);
                if (uploadJson?.Url == null)
                {
                    Debug.WriteLine("❌ Failed to parse skin ID from upload response");
                    return null;
                }

                var match = Regex.Match(uploadJson.Url, @"/skins/s(\d+)/");
                if (!match.Success)
                {
                    Debug.WriteLine("❌ Failed to extract skin ID from URL");
                    return null;
                }

                int skinId = int.Parse(match.Groups[1].Value);
                Debug.WriteLine($"✅ Uploaded skin ID: {skinId}");

                if (!string.IsNullOrEmpty(name) || tags?.Count > 0)
                    await SaveSkinMetadataAsync(skinId, name, isSlim, tags, cookies);

                return skinId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Upload error: {ex.Message}");
                return null;
            }
        }

        private static async Task<bool> SaveSkinMetadataAsync(
            int skinId,
            string name,
            bool isSlim,
            List<string>? tags,
            ElyCookies cookies)
        {
            try
            {
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("name", name),
                    new KeyValuePair<string, string>("description", ""),
                    new KeyValuePair<string, string>("kind", "6"),
                    new KeyValuePair<string, string>("color", ""),
                    new KeyValuePair<string, string>("tags", tags != null ? string.Join(",", tags) : ""),
                    new KeyValuePair<string, string>("isSlim", isSlim ? "1" : "0")
                });

                var request = new HttpRequestMessage(HttpMethod.Post, $"https://ely.by/skins/save/{skinId}");
                request.Headers.Add("Cookie", cookies.ToCookieHeader());
                request.Headers.Add("User-Agent", "Mozilla/5.0");
                request.Headers.Add("Referer", $"https://ely.by/skins/s{skinId}/edit");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Content = formData;

                var response = await _httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"💾 Save metadata response: {result}");

                return result.Contains("success_skin_edit");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Save metadata error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DeleteSkinAsync(int skinId, ElyCookies cookies)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://ely.by/skins/remove/{skinId}");
                request.Headers.Add("Cookie", cookies.ToCookieHeader());
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.Add("Referer", $"https://ely.by/skins/s{skinId}");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");

                var response = await _httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"🗑️ DeleteSkin response: {result}");

                return result.Contains("success_skin_delete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ DeleteSkin error: {ex.Message}");
                return false;
            }
        }

        private class UploadResponse
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }
        }
    }
}
