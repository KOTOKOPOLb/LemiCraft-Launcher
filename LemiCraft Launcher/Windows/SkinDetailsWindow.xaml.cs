using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Services;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace LemiCraft_Launcher.Windows
{
    public partial class SkinDetailsWindow : Window
    {
        private readonly SkinLibraryItem _skin;
        private readonly string _username;
        private static readonly HttpClient _httpClient = new();

        public SkinDetailsWindow(SkinLibraryItem skin, string username)
        {
            InitializeComponent();
            _skin = skin;
            _username = username;

            Opacity = 0;
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);
            RootBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            Loaded += SkinDetailsWindow_Loaded;
            LoadSkinData();
        }

        private void SkinDetailsWindow_Loaded(object sender, RoutedEventArgs e)
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

        private async void LoadSkinData()
        {
            TitleText.Text = _skin.Name;
            SkinName.Text = _skin.Name;

            var originalModel = _skin.Model.ToLower();
            ModelBadge.Text = originalModel == "alex" || _skin.Model.Contains("тонкие")
                ? "тонкие руки"
                : "толстые руки";

            if (_skin.IsActive)
            {
                ActiveBadge.Visibility = Visibility.Visible;
                ApplyButton.IsEnabled = false;
                ApplyButton.Content = "✓ Применён";
            }

            await LoadThumbnailWithRetryAsync();

            DateText.Text = $"📅 Добавлен: {_skin.CreatedAt:dd.MM.yyyy HH:mm}";
        }

        private async Task LoadThumbnailWithRetryAsync()
        {
            const int maxRetries = 5;
            const int retryDelayMs = 1000;

            if (string.IsNullOrWhiteSpace(_skin.ThumbnailUrl))
            {
                Debug.WriteLine("⚠️ ThumbnailUrl is empty");
                ShowError();
                return;
            }

            ShowLoading();

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Debug.WriteLine($"🔄 Attempt {attempt}/{maxRetries}: Loading {_skin.ThumbnailUrl}");

                    var cachedPath = await SkinCacheService.GetCachedImageAsync(_skin.ThumbnailUrl);

                    if (cachedPath != null && File.Exists(cachedPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(cachedPath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelHeight = 512;
                        bitmap.EndInit();

                        SkinPreview.Source = bitmap;
                        ShowImage();

                        Debug.WriteLine($"✅ Loaded successfully on attempt {attempt}");
                        return;
                    }

                    Debug.WriteLine($"⏳ Attempt {attempt}: Render not ready yet");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ Attempt {attempt} error: {ex.Message}");
                }

                if (attempt < maxRetries)
                {
                    UpdateLoadingText($"Попытка {attempt + 1} из {maxRetries}...");
                    await Task.Delay(retryDelayMs);
                }
            }

            Debug.WriteLine($"❌ Failed to load thumbnail after {maxRetries} attempts");
            ShowError();
        }

        private void ShowLoading()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingPanel.Visibility = Visibility.Visible;
                SkinPreview.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowImage()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                SkinPreview.Visibility = Visibility.Visible;
                ErrorPanel.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowError()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                SkinPreview.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
            });
        }

        private void UpdateLoadingText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (LoadingSubText != null)
                    LoadingSubText.Text = text;
            });
        }

        private async void RetryLoadingButton_Click(object sender, RoutedEventArgs e) => await LoadThumbnailWithRetryAsync();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.ShowQuestion($"Применить скин \"{_skin.Name}\"?\n\nСкин будет установлен на ваш аккаунт");

            if (result != CustomMessageBox.MessageBoxResult.Yes)
                return;

            ApplyButton.IsEnabled = false;
            ApplyButton.Content = "⏳ Применение...";

            try
            {
                var profile = AuthService.LoadProfile();
                var success = await SkinLibraryService.ApplySkinAsync(
                    _skin.Id,
                    _username,
                    profile?.AccessToken,
                    profile?.Provider,
                    profile?.Uuid
                );

                if (success)
                {
                    CustomMessageBox.ShowSuccess($"Скин \"{_skin.Name}\" успешно применён!");
                    DialogResult = true;
                    CloseWithAnimation();
                }
                else
                {
                    CustomMessageBox.ShowError("Не удалось применить скин");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error: {ex.Message}");
                CustomMessageBox.ShowError("Произошла ошибка при применении скина");
            }
            finally
            {
                ApplyButton.IsEnabled = true;
                ApplyButton.Content = "✨ Применить";
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.ShowQuestion($"Удалить скин \"{_skin.Name}\"?\n\nЭто действие нельзя отменить");

            if (result != CustomMessageBox.MessageBoxResult.Yes)
                return;

            DeleteButton.IsEnabled = false;
            DeleteButton.Content = "⏳ Удаление...";

            try
            {
                var success = await SkinLibraryService.DeleteSkinAsync(_skin.Id, _username);

                if (success)
                {
                    CustomMessageBox.ShowSuccess("Скин успешно удалён из коллекции");
                    DialogResult = true;
                    CloseWithAnimation();
                }
                else
                {
                    CustomMessageBox.ShowError("Не удалось удалить скин");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error: {ex.Message}");
                CustomMessageBox.ShowError("Произошла ошибка при удалении скина");
            }
            finally
            {
                DeleteButton.IsEnabled = true;
                DeleteButton.Content = "🗑️ Удалить";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
    }
}