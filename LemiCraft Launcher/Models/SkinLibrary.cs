namespace LemiCraft_Launcher.Models
{
    public class SkinLibraryItem
    {
        public int Id { get; set; }
        public int? ElybyId { get; set; }
        public string Name { get; set; } = "";
        public string Model { get; set; } = "steve";
        public string FileUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class UploadSkinResponse
    {
        public bool Success { get; set; }
        public SkinLibraryItem? Skin { get; set; }
    }

    public class ApplySkinResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public class UserSkinsResponse
    {
        public bool Success { get; set; }
        public string Username { get; set; } = "";
        public List<SkinLibraryItem> Skins { get; set; } = new();
    }
}
