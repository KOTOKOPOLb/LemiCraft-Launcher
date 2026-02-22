using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LemiCraft_Launcher.Windows
{
    public partial class UpdateWindow : Window
    {
        private readonly LauncherVersion _version;
        private bool _isUpdating = false;

        public UpdateWindow(LauncherVersion version)
        {
            InitializeComponent();
            _version = version;

            Opacity = 0;
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);

            Loaded += UpdateWindow_Loaded;
            LoadVersionInfo();
        }

        private void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            var scale = (ScaleTransform)RootBorder.RenderTransform;
            var scaleAnim = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void LoadVersionInfo()
        {
            VersionText.Text = $"{AppVersion.Current}  →  {_version.Version}";
            FileSizeText.Text = FormatFileSize(_version.FileSize);
            ReleaseDateText.Text = _version.ReleaseDate.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
            ChangelogList.ItemsSource = _version.Changelog;

            if (_version.IsRequired)
            {
                RequiredBadge.Visibility = Visibility.Visible;
                LaterButton.Visibility = Visibility.Collapsed;
                CloseButtonTop.IsEnabled = false;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "N/A";
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.#} {sizes[order]}";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_version.IsRequired && !_isUpdating)
                CloseWithAnimation();
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e) => CloseWithAnimation();

        private void CloseWithAnimation()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);

            if (RootBorder.RenderTransform is ScaleTransform st)
            {
                var scaleAnim = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            UpdateButton.IsEnabled = false;
            LaterButton.IsEnabled = false;
            CloseButtonTop.IsEnabled = false;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
            fadeOut.Completed += (_, _) =>
            {
                ChangelogSection.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressPanel.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                ProgressPanel.BeginAnimation(OpacityProperty, fadeIn);
            };
            ChangelogSection.BeginAnimation(OpacityProperty, fadeOut);

            var progress = new Progress<(double percent, long bytes)>(value =>
            {
                var (percent, bytes) = value;

                if (percent < 0)
                {
                    DownloadProgress.IsIndeterminate = true;
                    ProgressText.Text = $"Скачивание... {FormatFileSize(bytes)}";
                }
                else
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadProgress.Value = percent;
                    ProgressText.Text = percent < 100
                        ? $"Скачивание... {percent:0}%"
                        : "Запуск установщика...";
                }
            });

            try
            {
                var result = await UpdateService.DownloadLauncherUpdateAsync(_version, progress);

                if (result)
                {
                    ProgressText.Text = "Установка... Окно закроется автоматически";
                    DownloadProgress.IsIndeterminate = true;
                }
                else
                    ShowError("Не удалось скачать обновление.\nПроверьте подключение к интернету");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка обновления: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            CustomMessageBox.ShowError(message);

            ProgressPanel.Visibility = Visibility.Collapsed;
            ChangelogSection.Visibility = Visibility.Visible;
            ChangelogSection.Opacity = 1;

            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            CloseButtonTop.IsEnabled = !_version.IsRequired;
            _isUpdating = false;
        }
    }
}
