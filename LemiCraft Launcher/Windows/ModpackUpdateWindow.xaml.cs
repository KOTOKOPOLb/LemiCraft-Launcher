using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LemiCraft_Launcher.Windows
{
    public partial class ModpackUpdateWindow : Window
    {
        private readonly ModpackVersion _version;
        private bool _isUpdating;
        public bool UpdateSuccessful { get; private set; }

        public ModpackUpdateWindow(ModpackVersion version)
        {
            InitializeComponent();
            _version = version;

            Opacity = 0;
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);

            Loaded += ModpackUpdateWindow_Loaded;
            LoadVersionInfo();
        }

        private void ModpackUpdateWindow_Loaded(object sender, RoutedEventArgs e)
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
            var localVersion = UpdateService.LoadLocalVersion();
            VersionText.Text = $"{localVersion.ModpackVersion} → {_version.Version}";

            if (_version.FileSizes.TryGetValue("mods", out long modSize))
                ModsOnlySize.Text = FormatFileSize(modSize);
            else
                ModsOnlySize.Text = "N/A";

            if (_version.FileSizes.TryGetValue("mods_resources", out long modResSize))
                ModsAndResourcesSize.Text = FormatFileSize(modResSize);
            else
                ModsAndResourcesSize.Text = "N/A";

            if (_version.FileSizes.TryGetValue("full", out long fullSize))
                FullUpdateSize.Text = FormatFileSize(fullSize);
            else
                FullUpdateSize.Text = FormatFileSize(_version.FileSizes.Values.FirstOrDefault());
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "N/A";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.#} {sizes[order]}";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
                CloseWithAnimation();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
                CloseWithAnimation();
        }

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

        private void FullUpdateOption_Click(object sender, MouseButtonEventArgs e) => FullUpdateRadio.IsChecked = true;

        private void ModsAndResourcesOption_Click(object sender, MouseButtonEventArgs e) => ModsAndResourcesRadio.IsChecked = true;

        private void ModsOnlyOption_Click(object sender, MouseButtonEventArgs e) => ModsOnlyRadio.IsChecked = true;

        private void UpdateTypeChanged(object sender, RoutedEventArgs e)
        {
            if (FullUpdateCheckMark != null)
                FullUpdateCheckMark.Visibility = FullUpdateRadio?.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;

            if (ModsAndResourcesCheckMark != null)
                ModsAndResourcesCheckMark.Visibility = ModsAndResourcesRadio?.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;

            if (ModsOnlyCheckMark != null)
                ModsOnlyCheckMark.Visibility = ModsOnlyRadio?.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;
        }


        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            ModpackUpdateType updateType;
            if (ModsOnlyRadio.IsChecked == true)
                updateType = ModpackUpdateType.ModsOnly;
            else if (ModsAndResourcesRadio.IsChecked == true)
                updateType = ModpackUpdateType.ModsAndResources;
            else
                updateType = ModpackUpdateType.Full;

            _isUpdating = true;
            UpdateButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            CloseButtonTop.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            if (FullUpdateRadio.IsChecked != true)
                FadeOutAndCollapse(FullUpdateCard);

            if (ModsAndResourcesRadio.IsChecked != true)
                FadeOutAndCollapse(ModsAndResourcesCard);

            if (ModsOnlyRadio.IsChecked != true)
                FadeOutAndCollapse(ModsOnlyCard);

            ProgressPanel.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            ProgressPanel.BeginAnimation(OpacityProperty, fadeIn);

            var progress = new Progress<(string task, double progress)>(value =>
            {
                ProgressText.Text = value.task;
                UpdateProgress.Value = value.progress;
            });

            try
            {
                var result = await UpdateService.UpdateModpackAsync(_version, updateType, progress);

                if (result)
                {
                    UpdateSuccessful = true;
                    await Task.Delay(500);
                    CustomMessageBox.ShowSuccess("Обновление успешно установлено!");
                    CloseWithAnimation();
                }
                else
                {
                    CustomMessageBox.ShowError("Не удалось установить обновление.\nПроверьте подключение к интернету");
                    UpdateButton.IsEnabled = true;
                    CancelButton.IsEnabled = true;
                    CloseButtonTop.IsEnabled = true;
                    ProgressPanel.Visibility = Visibility.Collapsed;
                    _isUpdating = false;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ошибка обновления: {ex.Message}");

                UpdateButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                CloseButtonTop.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                _isUpdating = false;
            }
        }

        private void FadeOutAndCollapse(UIElement element, int durationMs = 200)
        {
            if (element == null) return;

            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs));
            anim.Completed += (_, __) =>
            {
                element.Visibility = Visibility.Collapsed;
                element.Opacity = 1;
            };

            element.BeginAnimation(OpacityProperty, anim);
        }
    }
}