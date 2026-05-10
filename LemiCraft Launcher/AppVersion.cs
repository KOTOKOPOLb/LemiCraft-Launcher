using System.IO;
using System.Reflection;

namespace LemiCraft_Launcher
{
    public static class AppVersion
    {
        // Portable = одиночный .exe без рядом лежащей .dll
        public static bool IsPortable => !File.Exists(
            Path.ChangeExtension(Environment.ProcessPath ?? "", ".dll"));

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