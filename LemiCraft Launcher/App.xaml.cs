using LemiCraft_Launcher.Services;

namespace LemiCraft_Launcher
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);
            AuthService.MigrateIfNeeded();
        }
    }
}