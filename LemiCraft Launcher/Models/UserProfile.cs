namespace LemiCraft_Launcher.Models
{
    public class UserProfile
    {
        public string Username { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string Provider { get; set; } = ""; 
        public string AvatarPath { get; set; } = ""; 
        public DateTime LastLogin { get; set; } = DateTime.Now;
        public bool RememberMe { get; set; }
    }
}
