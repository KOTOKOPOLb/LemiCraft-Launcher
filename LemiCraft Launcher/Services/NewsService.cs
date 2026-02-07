using LemiCraft_Launcher.Models;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LemiCraft_Launcher.Services
{
    public static class NewsService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");
        private static readonly string CacheFilePath = Path.Combine(DataDir, "news_cache.json");

        private static string NEWS_API_URL =>
            $"{ConfigService.Load().ApiBaseUrl}/launcher/news";

        private const int CACHE_LIFETIME_MINUTES = 30;

        public static async Task<List<NewsItem>> GetNewsAsync(NewsFilter? filter = null, bool forceRefresh = false)
        {
            try
            {
                if (!forceRefresh)
                {
                    var cached = LoadFromCache();
                    if (cached != null && IsCacheValid(cached))
                        return ApplyFilter(cached.Items, filter);
                }

                var news = await FetchNewsFromApiAsync(filter);

                SaveToCache(new NewsCacheData
                {
                    Items = news,
                    LastUpdate = DateTime.Now,
                    TotalCount = news.Count
                });

                return news;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки новостей: {ex.Message}");

                var cached = LoadFromCache();
                return cached?.Items ?? new List<NewsItem>();
            }
        }

        private static async Task<List<NewsItem>> FetchNewsFromApiAsync(NewsFilter? filter)
        {
            var url = BuildApiUrl(filter);
            var response = await _httpClient.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            var apiResponse = JsonSerializer.Deserialize<NewsApiResponse>(response, options);

            if (apiResponse == null || !apiResponse.Success)
            {
                System.Diagnostics.Debug.WriteLine("API вернул success=false");
                return new List<NewsItem>();
            }

            return apiResponse.Items ?? new List<NewsItem>();
        }

        private static string BuildApiUrl(NewsFilter? filter)
        {
            if (filter == null)
                return NEWS_API_URL;

            var queryParams = new List<string>();

            if (filter.Category.HasValue)
                queryParams.Add($"category={filter.Category.Value.ToString().ToLower()}");

            if (filter.Limit.HasValue)
                queryParams.Add($"limit={filter.Limit.Value}");

            if (filter.Offset.HasValue)
                queryParams.Add($"offset={filter.Offset.Value}");

            if (filter.FromDate.HasValue)
                queryParams.Add($"from={filter.FromDate.Value:yyyy-MM-dd}");

            if (filter.ToDate.HasValue)
                queryParams.Add($"to={filter.ToDate.Value:yyyy-MM-dd}");

            return queryParams.Count > 0
                ? $"{NEWS_API_URL}?{string.Join("&", queryParams)}"
                : NEWS_API_URL;
        }

        private static List<NewsItem> ApplyFilter(List<NewsItem> items, NewsFilter? filter)
        {
            if (filter == null)
                return items;

            var filtered = items.AsEnumerable();

            if (filter.Category.HasValue)
                filtered = filtered.Where(x => x.Category == filter.Category.Value);

            if (filter.FromDate.HasValue)
                filtered = filtered.Where(x => x.PublishedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                filtered = filtered.Where(x => x.PublishedAt <= filter.ToDate.Value);

            if (filter.Offset.HasValue)
                filtered = filtered.Skip(filter.Offset.Value);

            if (filter.Limit.HasValue)
                filtered = filtered.Take(filter.Limit.Value);

            return filtered.ToList();
        }

        private static bool IsCacheValid(NewsCacheData cache) =>
            (DateTime.Now - cache.LastUpdate).TotalMinutes < CACHE_LIFETIME_MINUTES;

        private static NewsCacheData? LoadFromCache()
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                    return null;

                var json = File.ReadAllText(CacheFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                return JsonSerializer.Deserialize<NewsCacheData>(json, options);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveToCache(NewsCacheData cache)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                var json = JsonSerializer.Serialize(cache, options);
                File.WriteAllText(CacheFilePath, json);
            }
            catch
            { }
        }

        public static void ClearCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                    File.Delete(CacheFilePath);
            }
            catch
            { }
        }

        public static async Task<NewsItem?> GetNewsByIdAsync(string id)
        {
            try
            {
                var cached = LoadFromCache();
                if (cached != null)
                {
                    var cachedItem = cached.Items.FirstOrDefault(x => x.Id == id);
                    if (cachedItem != null)
                        return cachedItem;
                }

                var url = $"{NEWS_API_URL}/{id}";
                var response = await _httpClient.GetStringAsync(url);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };
                return JsonSerializer.Deserialize<NewsItem>(response, options);
            }
            catch
            {
                return null;
            }
        }

        public static string GetPreview(string content, int maxLength = 150)
        {
            var preview = System.Text.RegularExpressions.Regex.Replace(content, @"\*\*|__|~~|`", "");

            if (preview.Length > maxLength)
                preview = preview.Substring(0, maxLength) + "...";

            return preview;
        }

        public static List<string> ExtractTags(string content)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"#(\w+)");
            var matches = regex.Matches(content);
            return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
        }
        private class NewsApiResponse
        {
            public bool Success { get; set; }
            public List<NewsItem> Items { get; set; } = new();
            public int Total { get; set; }
        }
    }
}