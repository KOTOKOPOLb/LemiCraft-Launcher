using LemiCraft_Launcher.Services;

namespace LemiCraft_Launcher
{
    public partial class App
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            SetProcessDpiAwareness();
            base.OnStartup(e);
            AuthService.MigrateIfNeeded();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(int dpiFlag);

        private void SetProcessDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(-4);
            }
            catch
            { }
        }
    }
}