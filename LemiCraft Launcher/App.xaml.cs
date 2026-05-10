using LemiCraft_Launcher.Services;

namespace LemiCraft_Launcher
{
    public partial class App
    {
        private async void App_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            try
            {
                StartupData.Preloader?.SetProgress(5, "Инициализация...");

                using var cts = new CancellationTokenSource();
                var fakeTask = AnimateFakeProgressAsync(cts.Token);

                StartupData.UpdateResult = await UpdateService.CheckForUpdatesAsync();

                cts.Cancel();
                await fakeTask;

                StartupData.Preloader?.SetProgress(100, "Запуск...");
            }
            catch { }

            var mainWindow = new MainWindow();

            mainWindow.ContentRendered += (_, _) =>
            {
                StartupData.Preloader?.Close();
                StartupData.Preloader = null;
            };

            mainWindow.Show();
        }

        private static async Task AnimateFakeProgressAsync(CancellationToken ct)
        {
            var stages = new (int To, int DelayMs, string Status)[]
            {
                (15, 120, "Инициализация..."),
                (28, 180, "Загрузка конфигурации..."),
                (42, 220, "Подготовка компонентов..."),
                (55, 260, "Проверка обновлений..."),
                (67, 300, "Проверка обновлений..."),
                (76, 350, "Проверка обновлений..."),
                (83, 400, "Почти готово..."),
                (88, 500, "Почти готово..."),
            };

            try
            {
                foreach (var (to, delay, status) in stages)
                {
                    if (ct.IsCancellationRequested) break;
                    StartupData.Preloader?.SetProgress(to, status);
                    await Task.Delay(delay, ct);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
