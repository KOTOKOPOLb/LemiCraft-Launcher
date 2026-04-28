namespace LemiCraft_Launcher.Utils
{
    public static class FileUtils
    {
        public static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "N/A";
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.#} {sizes[order]}";
        }
    }
}
