using LemiCraft_Launcher.Services;
using LemiCraft_Launcher.Windows;
using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

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
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            VersionText.Text = $"v{AppVersion.Current}";
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
            {
                if (MainFrame.Content is LoginPage)
                    HideOverlay();
                MainFrame.Navigate(new HomePage());
            }
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

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_logsWindow != null)
            {
                _logsWindow.Close();
                _logsWindow = null;
            }

            var fadeOut = (Storyboard)Resources["WindowFadeOut"];
            fadeOut.Completed += (s, a) => Close();
            fadeOut.Begin(this);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void ShowProgress(string message, double progress)
        {
            FooterProgressPanel.Visibility = Visibility.Visible;
            FooterProgressText.Text = message;
            FooterProgressBar.Value = progress;
            FooterFilesText.Text = "";
            FooterPercentText.Text = $"{progress:F0}%";
        }

        private void HideProgress()
        {
            FooterProgressPanel.Visibility = Visibility.Collapsed;
        }

        public async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = AuthService.LoadProfile();
            if (profile == null)
            {
                MainFrame.Navigate(new LoginPage());
                return;
            }

            AccountInfoPanel.IsEnabled = false;
            SettingsButton.IsEnabled = false;
            PlayButton.IsEnabled = false;

            var isInstalled = await MinecraftLauncherService.IsInstalledAsync();

            if (!isInstalled)
                await InstallGameAsync();
            else
            {
                ShowProgress("Подготовка к запуску...", 0);

                try
                {
                    await LaunchMinecraftAsync();
                }
                catch (Exception ex)
                {
                    HideProgress();
                    CustomMessageBox.ShowError($"Ошибка запуска: {ex.Message}");
                    PlayButton.IsEnabled = true;
                    AccountInfoPanel.IsEnabled = true;
                    SettingsButton.IsEnabled = true;
                }
            }
        }

        private async Task LaunchMinecraftAsync()
        {
            var config = ConfigService.Load();
            var profile = AuthService.LoadProfile();
            if (profile == null)
            {
                MainFrame.Navigate(new LoginPage());
                return;
            }

            AccountInfoPanel.IsEnabled = false;
            SettingsButton.IsEnabled = false;
            PlayButton.IsEnabled = false;

            var process = await MinecraftLauncherService.LaunchAsync(profile, config);

            HideProgress();

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

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
                if (config.LauncherBehavior == 0)
                    Dispatcher.Invoke(() => CloseButton_Click(null, new RoutedEventArgs()));
                else if (config.LauncherBehavior == 1)
                    Dispatcher.Invoke(() => WindowState = WindowState.Minimized);

                process.WaitForExit();

                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    FooterPlayButtonIcon.Text = "▶️";
                    FooterPlayButtonText.Text = "Играть";
                    PlayButton.IsEnabled = true;
                    AccountInfoPanel.IsEnabled = true;
                    SettingsButton.IsEnabled = true;
                    _logsWindow?.SetStatus("Игра закрыта", "#EF4444");
                });
            });
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
                CustomMessageBox.ShowSuccess("Установка завершена!");

                FooterPlayButtonIcon.Text = "▶️";
                FooterPlayButtonText.Text = "Играть";
                PlayButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ошибка установки:\n{ex.Message}");
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

        public void AccountInfo_Click(object sender, MouseButtonEventArgs e)
        {
            var profile = AuthService.LoadProfile();
            if (profile != null && PlayButton.IsEnabled)
                ShowAccountMenu();
            else
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
            var result = CustomMessageBox.ShowQuestion(
                "Вы уверены, что хотите выйти из аккаунта?",
                "Выход"
            );

            if (result == CustomMessageBox.MessageBoxResult.Yes)
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

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync();

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var result = await UpdateService.CheckForUpdatesAsync();

                if (result.LauncherUpdateAvailable && result.LauncherVersion != null)
                {
                    var updateWindow = new UpdateWindow(result.LauncherVersion)
                    {
                        Owner = this
                    };
                    updateWindow.ShowDialog();
                }

                if (result.ModpackUpdateAvailable && result.ModpackVersion != null)
                {
                    FooterPlayButtonText.Text = "Обновить сборку";
                    FooterPlayButtonIcon.Text = "↓";
                    PlayButton.Tag = result.ModpackVersion;
                    PlayButton.Click -= PlayButton_Click;
                    PlayButton.Click += UpdateModpack_Click;
                    if (MainFrame.Content is HomePage home)
                    {
                        var latest = result.ModpackVersion.Version;
                        Dispatcher.Invoke(() => home.ShowModpackUpdateAvailable(latest));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
            }
        }

        private void UpdateModpack_Click(object sender, RoutedEventArgs e)
        {
            if (PlayButton.Tag is ModpackVersion version)
            {
                var updateWindow = new ModpackUpdateWindow(version)
                {
                    Owner = this
                };

                updateWindow.ShowDialog();

                if (updateWindow.UpdateSuccessful)
                {
                    FooterPlayButtonText.Text = "Играть";
                    FooterPlayButtonIcon.Text = "▶️";
                    PlayButton.Tag = null;
                    PlayButton.Click -= UpdateModpack_Click;
                    PlayButton.Click += PlayButton_Click;
                    if (MainFrame.Content is HomePage home)
                        _ = home.UpdateModpackDisplayAsync();
                }
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_logsWindow != null && _logsWindow.IsVisible)
            {
                _logsWindow.Close();
                _logsWindow = null;
            }

            foreach (Window window in Application.Current.Windows)
            {
                if (window != this && window.IsVisible)
                    window.Close();
            }

            Application.Current.Shutdown();
        }

        public void ShowOverlay(bool withLoader = false)
        {
            Overlay.Visibility = Visibility.Visible;
            Overlay.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            Overlay.BeginAnimation(OpacityProperty, fadeIn);

            LoaderCanvas.Visibility = withLoader ? Visibility.Visible : Visibility.Collapsed;

            if (withLoader)
            {
                var rotate = new RotateTransform();
                LoaderCanvas.RenderTransform = rotate;
                LoaderCanvas.RenderTransformOrigin = new Point(0.5, 0.5);

                var animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                rotate.BeginAnimation(RotateTransform.AngleProperty, animation);
            }
        }

        public void HideOverlay()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, _) =>
            {
                Overlay.Visibility = Visibility.Collapsed;
                LoaderCanvas.Visibility = Visibility.Collapsed;
            };
            Overlay.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}