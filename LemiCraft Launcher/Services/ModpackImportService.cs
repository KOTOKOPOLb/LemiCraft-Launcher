using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LemiCraft_Launcher.Services
{
    public class ModInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("modrinthId")]
        public string ModrinthId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("configs")]
        public List<string> Configs { get; set; } = new();
    }

    public class ImportData
    {
        [JsonPropertyName("mods")]
        public List<string> Mods { get; set; } = new();

        [JsonPropertyName("configs")]
        public bool Configs { get; set; }
    }

    public static class ModpackImportService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private static List<ModInfo>? _catalogCache;

        private static string ApiBase => ConfigService.Load().ApiBaseUrl;

        public static async Task<ImportData?> GetImportDataAsync(string code)
        {
            try
            {
                var resp = await _http.GetAsync($"{ApiBase}/mods/import/{code.Trim()}");
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ImportData>(json);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<List<ModInfo>> GetModCatalogAsync()
        {
            if (_catalogCache != null) return _catalogCache;
            try
            {
                var resp = await _http.GetAsync($"{ApiBase}/mods");
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                _catalogCache = JsonSerializer.Deserialize<List<ModInfo>>(json) ?? new();
                return _catalogCache;
            }
            catch
            {
                return _catalogCache ?? new();
            }
        }

        public static async Task<(List<ModInfo> Known, List<string> Unknown)> ResolveModsAsync(ImportData data)
        {
            var catalog = await GetModCatalogAsync();
            var map = catalog.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

            var known = new List<ModInfo>();
            var unknown = new List<string>();

            foreach (var id in data.Mods)
            {
                if (map.TryGetValue(id, out var mod))
                    known.Add(mod);
                else
                    unknown.Add(id);
            }

            return (known, unknown);
        }

        public static async Task ImportAsync(
            string code,
            bool includeConfigs,
            IProgress<(string Status, int Percent)> progress,
            CancellationToken ct = default)
        {
            var config = ConfigService.Load();
            var gameDir = config.GamePath;
            var modsDir = Path.Combine(gameDir, "mods");
            Directory.CreateDirectory(modsDir);

            progress.Report(("Скачивание архива...", 10));

            var zipUrl = $"{ApiBase}/mods/download/{code.Trim()}";
            var zipBytes = await _http.GetByteArrayAsync(zipUrl, ct);

            progress.Report(("Распаковка файлов...", 60));

            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            int total = zip.Entries.Count;
            int done = 0;

            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                done++;

                if (entry.FullName.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(entry.Name))
                {
                    var dest = Path.Combine(modsDir, entry.Name);
                    entry.ExtractToFile(dest, overwrite: true);
                }
                else if (includeConfigs
                         && entry.FullName.StartsWith("config/", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(entry.Name))
                {
                    var relative = entry.FullName["config/".Length..];
                    var dest = Path.Combine(gameDir, "config", relative);
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    entry.ExtractToFile(dest, overwrite: true);
                }

                int pct = 60 + (int)(35.0 * done / Math.Max(1, total));
                progress.Report(($"Распаковка... {done}/{total}", pct));
            }

            await ModpackVersionManager.SaveInstalledVersionAsync(
                "Custom",
                Path.GetFileName(zipUrl),
                zipBytes.LongLength
            );

            progress.Report(("Готово!", 100));
        }
    }
}
