using System.Reflection;

namespace LemiCraft_Launcher
{
    public static class AppVersion
    {
        public static string Current
        {
            get
            {
                try
                {
                    var version = Assembly.GetExecutingAssembly()
                        .GetName()
                        .Version;

                    if (version != null)
                        return version.ToString(3);

                    return "1.0.0";
                }
                catch
                {
                    return "1.0.0";
                }
            }
        }
    }
}