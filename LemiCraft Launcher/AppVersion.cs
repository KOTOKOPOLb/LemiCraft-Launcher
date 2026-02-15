using System.Reflection;

namespace LemiCraft_Launcher
{
    public static class AppVersion
    {
        public static string Current
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null
                    ? $"{version.Major}.{version.Minor}.{version.Build}"
                    : "1.0.0";
            }
        }

        public static string FullVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version?.ToString() ?? "1.0.0.0";
            }
        }
    }
}