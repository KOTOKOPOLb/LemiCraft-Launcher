using System.IO;
using LemiCraft_Launcher.Services;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace LemiCraft_Launcher
{
    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(int dpiFlag);

        private const string MutexName = "LemiCraftLauncher_SingleInstance";
        private const string PipeName = "LemiCraftLauncherPipe";

        [STAThread]
        public static void Main(string[] args)
        {
            try { SetProcessDpiAwarenessContext(-4); } catch { }

            string? importCode = null;
            if (args.Length > 0)
            {
                const string prefix = "lemicraft://import/";
                if (args[0].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    importCode = args[0][prefix.Length..].Trim('/');
            }

            bool createdNew;
            using var mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                if (importCode != null)
                    SendToRunningInstance(importCode);
                return;
            }

            StartupData.PendingImportCode = importCode;

            Application.EnableVisualStyles();

            var preloader = new PreloaderForm();
            preloader.Show();
            preloader.Refresh();
            StartupData.Preloader = preloader;

            AuthService.MigrateIfNeeded();

            _ = StartPipeServerAsync();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static void SendToRunningInstance(string code)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                pipe.Connect(2000);
                using var writer = new StreamWriter(pipe);
                writer.WriteLine(code);
                writer.Flush();
            }
            catch { }
        }

        private static async Task StartPipeServerAsync()
        {
            while (true)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync();

                    using var reader = new StreamReader(pipe);
                    var code = await reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(code))
                        StartupData.RaiseImportCodeReceived(code.Trim());
                }
                catch { }
            }
        }
    }
}
