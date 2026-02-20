public class UserProfile
{
    public string Username { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string Provider { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public DateTime LastLogin { get; set; } = DateTime.Now;
    public bool RememberMe { get; set; }
    public string? ElybyPhpSessId { get; set; }
    public string? ElybyIdentity { get; set; }
    public DateTime? ElybyCookiesExpiry { get; set; }
    public bool HasValidElybyCookies() => !string.IsNullOrEmpty(ElybyPhpSessId) && (!ElybyCookiesExpiry.HasValue || ElybyCookiesExpiry.Value > DateTime.Now);
}