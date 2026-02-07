using System.Text.Json.Serialization;

namespace LemiCraft_Launcher.Models
{
    public class NewsItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Preview { get; set; }

        public string? ImageUrl { get; set; }
        public string AuthorName { get; set; } = "";
        public string? AuthorAvatarUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public List<string> Tags { get; set; } = new();

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public NewsCategory Category { get; set; }

        public string Url { get; set; } = "";
    }

    public enum NewsCategory
    {
        General,
        Update,
        Event,
        Announcement,
        Maintenance
    }

    public class NewsCacheData
    {
        public List<NewsItem> Items { get; set; } = new();
        public DateTime LastUpdate { get; set; }
        public int TotalCount { get; set; }
    }

    public class NewsFilter
    {
        public NewsCategory? Category { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? Limit { get; set; } = 10;
        public int? Offset { get; set; } = 0;
    }
}