using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ProcessBuilder;
using LemiCraft_Launcher.Services;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace LemiCraft_Launcher
{
    public partial class MainWindow : Window
    {
        private Action<InstallProgress>? _installProgressHandler;
        private GameLogsWindow? _logsWindow;
        private Page? currentPage;

        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new HomePage());
            _ = TryAutoLoginAsync();
            AvatarService.CleanOldAvatars();
            var config = ConfigService.Load();
            if (config.ShowLogs)
            {
                _logsWindow = new GameLogsWindow();
                _logsWindow.Show();
                _logsWindow.SetStatus("Ожидание запуска игры...", "#9CA3AF");
            }
        }

        private async Task TryAutoLoginAsync()
        {
            var result = await AuthService.AutoLoginAsync();

            if (result.Success && result.Profile != null)
            {
                UpdateAccountInfo(result.Profile.Username, true);
                await LoadUserAvatarAsync(result.Profile.Username);
            }
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (currentPage != null)
            {
                var fadeOut = (Storyboard)Resources["PageFadeOut"];
                Storyboard.SetTarget(fadeOut, currentPage);
                fadeOut.Begin();
            }

            currentPage = e.Content as Page;

            if (currentPage != null)
            {
                currentPage.Opacity = 0;
                var fadeIn = (Storyboard)Resources["PageFadeIn"];
                Storyboard.SetTarget(fadeIn, currentPage);
                fadeIn.Begin();
            }

            UpdateFooterVisibility();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(MainFrame.Content is SettingsPage))
                MainFrame.Navigate(new SettingsPage());
        }

        public void NavigateToHome()
        {
            if (!(MainFrame.Content is HomePage))
                MainFrame.Navigate(new HomePage());
        }

        private void UpdateFooterVisibility()
        {
            if (MainFrame.Content is HomePage)
                Footer.Visibility = Visibility.Visible;
            else
                Footer.Visibility = Visibility.Collapsed;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_logsWindow != null)
            {
                _logsWindow.Close();
                _logsWindow = null;
            }

            var fadeOut = (Storyboard)Resources["WindowFadeOut"];
            fadeOut.Completed += (s, a) => System.Windows.Application.Current.Shutdown();
            fadeOut.Begin(this);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        public async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            var isInstalled = await MinecraftLauncherService.IsInstalledAsync();

            if (!isInstalled)
                await InstallGameAsync();
            else
                await LaunchGameAsync();
        }

        private async Task InstallGameAsync()
        {
            AccountInfoPanel.IsEnabled = false;
            SettingsButton.IsEnabled = false;
            PlayButton.IsEnabled = false;
            FooterProgressPanel.Visibility = Visibility.Visible;

            FooterPlayButtonIcon.Text = "⏳";
            FooterPlayButtonText.Text = "Установка...";

            _installProgressHandler = progress =>
            {
                Dispatcher.Invoke(() =>
                {
                    FooterProgressText.Text = progress.Task;
                    FooterProgressBar.Value = progress.Percent;
                    FooterFilesText.Text = $"Файлов: {progress.CompletedFiles} / {progress.TotalFiles}";
                    FooterPercentText.Text = $"{progress.Percent:F1}%";
                });
            };
            MinecraftLauncherService.ProgressChanged += _installProgressHandler;

            try
            {
                await MinecraftLauncherService.InstallAsync();
                MessageBox.Show("Установка завершена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                FooterPlayButtonIcon.Text = "▶️";
                FooterPlayButtonText.Text = "Играть";
                PlayButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка установки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                FooterPlayButtonIcon.Text = "📥";
                FooterPlayButtonText.Text = "Установить";
                PlayButton.IsEnabled = true;
            }
            finally
            {
                MinecraftLauncherService.ProgressChanged -= _installProgressHandler;
                FooterProgressPanel.Visibility = Visibility.Collapsed;
                AccountInfoPanel.IsEnabled = true;
                SettingsButton.IsEnabled = true;
            }
        }

        private async Task LaunchGameAsync()
        {
            var profile = AuthService.LoadProfile();
            if (profile == null)
            {
                MessageBox.Show("Сначала войдите в аккаунт!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                MainFrame.Navigate(new LoginPage());
                return;
            }

            AccountInfoPanel.IsEnabled = false;
            SettingsButton.IsEnabled = false;
            PlayButton.IsEnabled = false;

            FooterPlayButtonIcon.Text = "🎮";
            FooterPlayButtonText.Text = "Запуск...";

            try
            {
                var config = ConfigService.Load();
                var process = await MinecraftLauncherService.LaunchAsync(profile, config);

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                if (_logsWindow != null)
                    _logsWindow.SetStatus("Запуск игры...", "#FFA500");

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logsWindow?.AppendLog(e.Data);
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logsWindow?.AppendLog($"[ERROR] {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _logsWindow?.SetStatus("Игра запущена", "#22C55E");

                FooterPlayButtonIcon.Text = "⏸️";
                FooterPlayButtonText.Text = "Игра запущена";

                _ = Task.Run(() =>
                {
                    process.WaitForExit();

                    Dispatcher.Invoke(() =>
                    {
                        FooterPlayButtonIcon.Text = "▶️";
                        FooterPlayButtonText.Text = "Играть";
                        PlayButton.IsEnabled = true;
                        AccountInfoPanel.IsEnabled = true;
                        SettingsButton.IsEnabled = true;
                        _logsWindow?.SetStatus("Игра закрыта", "#EF4444");
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                FooterPlayButtonIcon.Text = "▶️";
                FooterPlayButtonText.Text = "Играть";
                PlayButton.IsEnabled = true;
                AccountInfoPanel.Visibility = Visibility.Visible;
                SettingsButton.IsEnabled = true;
            }
        }

        public void SetPlayButtonState(string icon, string text, bool enabled)
        {
            FooterPlayButtonIcon.Text = icon;
            FooterPlayButtonText.Text = text;
            PlayButton.IsEnabled = enabled;
        }

        private async Task<MSession> GetSessionForLaunchAsync()
        {
            var profile = AuthService.LoadProfile();
            if (profile == null)
                return MSession.CreateOfflineSession("Player");

            if (profile.Provider == "Microsoft")
            {
                try
                {
                    var handler = JELoginHandlerBuilder.BuildDefault();
                    var session = await handler.Authenticate();
                    if (session != null && !string.IsNullOrWhiteSpace(session.AccessToken))
                        return session;

                    return MSession.CreateOfflineSession(profile.Username);
                }
                catch
                {
                    return MSession.CreateOfflineSession(profile.Username);
                }
            }
            else if (profile.Provider == "Ely.by")
            {
                var session = new MSession(profile.Username, profile.AccessToken ?? "", profile.Uuid ?? "");
                return session;
            }

            return MSession.CreateOfflineSession(profile.Username ?? "Player");
        }

        private async Task<string> EnsureAuthlibInjectorAsync()
        {
            try
            {
                var cfg = ConfigService.Load();
                var localDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");
                Directory.CreateDirectory(localDir);
                var jarPath = Path.Combine(localDir, "authlib-injector.jar");

                if (!string.IsNullOrWhiteSpace(cfg.AuthlibInjectorPath) && File.Exists(cfg.AuthlibInjectorPath))
                    return cfg.AuthlibInjectorPath;

                if (File.Exists(jarPath))
                    return jarPath;

                if (string.IsNullOrWhiteSpace(cfg.AuthlibInjectorDownloadUrl))
                    return "";

                using var http = new HttpClient();
                var resp = await http.GetAsync(cfg.AuthlibInjectorDownloadUrl);
                if (!resp.IsSuccessStatusCode) return "";

                await using var fs = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await resp.Content.CopyToAsync(fs);
                return jarPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureAuthlibInjectorAsync error: " + ex);
                return "";
            }
        }

        private async Task InstallLocalBuildFromZipAsync(string zipPath, string versionName, MinecraftLauncher launcher)
        {
            try
            {
                var mcDir = new MinecraftPath().BasePath;
                var versionsDir = Path.Combine(mcDir, "versions");
                Directory.CreateDirectory(versionsDir);

                var temp = Path.Combine(Path.GetTempPath(), "LemiCraftBuild", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                ZipFile.ExtractToDirectory(zipPath, temp);

                var from = Path.Combine(temp, "versions", versionName);
                if (Directory.Exists(from))
                {
                    var to = Path.Combine(versionsDir, versionName);
                    if (Directory.Exists(to)) Directory.Delete(to, true);
                    Directory.Move(from, to);
                }
                else
                {
                    var to = Path.Combine(versionsDir, versionName);
                    Directory.CreateDirectory(to);
                    foreach (var f in Directory.GetFiles(temp, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(temp, f);
                        var dest = Path.Combine(to, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        File.Copy(f, dest, true);
                    }
                }

                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InstallLocalBuildFromZipAsync error: " + ex.Message);
            }
        }

        public void AccountInfo_Click(object sender, MouseButtonEventArgs e)
        {
            if (PlayButton.IsEnabled)
                ShowAccountMenu();
            else
                if (!(MainFrame.Content is LoginPage))
                MainFrame.Navigate(new LoginPage());
        }

        private void ShowAccountMenu()
        {
            var accountPanel = FindName("AccountInfoPanel") as FrameworkElement;
            if (accountPanel == null)
                return;

            var menu = new ContextMenu();

            var profile = AuthService.LoadProfile();
            if (profile != null)
            {
                var profileItem = new MenuItem
                {
                    Header = $"👤 {profile.Username}",
                    IsEnabled = false
                };
                menu.Items.Add(profileItem);

                var providerItem = new MenuItem
                {
                    Header = $"🔷 {profile.Provider}",
                    IsEnabled = false
                };
                menu.Items.Add(providerItem);
            }

            var logoutItem = new MenuItem
            {
                Header = "🚪 Выйти из аккаунта"
            };
            logoutItem.Click += (s, ev) => Logout();
            menu.Items.Add(logoutItem);

            menu.PlacementTarget = accountPanel;
            menu.IsOpen = true;
        }

        private void Logout()
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите выйти из аккаунта?",
                "Выход",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                AuthService.Logout();
                UpdateAccountInfo("", false);
                ResetAvatar();
            }
        }

        public void UpdateAccountInfo(string username, bool isAuthorized)
        {
            if (isAuthorized)
            {
                UsernameText.Text = username;
                var profile = AuthService.LoadProfile();
                AccountTypeText.Text = profile != null ? $"{profile.Provider} аккаунт" : "Авторизован";
                PlayButton.IsEnabled = true;
            }
            else
            {
                UsernameText.Text = "Не авторизован";
                AccountTypeText.Text = "Нажмите для входа";
                PlayButton.IsEnabled = false;
            }
        }
        public async Task LoadUserAvatarAsync(string username)
        {
            try
            {
                var avatarPath = await AvatarService.GetAvatarAsync(username, use3D: true);
                if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(avatarPath, UriKind.Absolute);
                    bitmap.EndInit();

                    var imageBrush = new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };

                    PlayerAvatar.Background = imageBrush;
                    PlayerAvatarEmoji.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки аватара: {ex.Message}");
            }
        }
        private void ResetAvatar()
        {
            PlayerAvatar.Background = new SolidColorBrush(Color.FromRgb(55, 65, 81));
            PlayerAvatarEmoji.Visibility = Visibility.Visible;
        }

        public void OpenLogsWindow()
        {
            if (_logsWindow == null)
            {
                _logsWindow = new GameLogsWindow();
                _logsWindow.SetStatus("Ожидание запуска игры...", "#9CA3AF");
            }

            if (!_logsWindow.IsVisible)
                _logsWindow.Show();
        }
    }
}