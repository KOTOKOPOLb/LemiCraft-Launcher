using LemiCraft_Launcher.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace LemiCraft_Launcher.Windows
{
    public partial class UploadSkinWindow : Window
    {
        private readonly string _username;
        private string? _selectedFilePath;

        public UploadSkinWindow(string username)
        {
            InitializeComponent();
            _username = username;

            Opacity = 0;
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);
            RootBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            Loaded += UploadSkinWindow_Loaded;
        }

        private void UploadSkinWindow_Loaded(object sender, RoutedEventArgs e)
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

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void SelectFile_Click(object sender, MouseButtonEventArgs e) => SelectFile();

        private void SelectFile()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файл скина",
                Filter = "PNG изображения|*.png",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 1024 * 1024)
                {
                    CustomMessageBox.ShowError("Размер файла превышает 1 МБ");
                    return;
                }

                try
                {
                    var bitmap = new BitmapImage(new Uri(filePath));

                    if ((bitmap.PixelWidth == 64 && bitmap.PixelHeight == 64) || (bitmap.PixelWidth == 64 && bitmap.PixelHeight == 32))
                    {
                        _selectedFilePath = filePath;

                        var detectedModel = DetectSkinModel(filePath);
                        ModelComboBox.SelectedIndex = detectedModel == "alex" ? 1 : 0;

                        Debug.WriteLine($"🔍 Detected model: {detectedModel}");

                        PlaceholderPanel.Visibility = Visibility.Collapsed;
                        PreviewImage.Visibility = Visibility.Visible;
                        PreviewImage.Source = bitmap;

                        FileNameText.Text = $"📄 {Path.GetFileName(filePath)}";
                        FileNameText.Visibility = Visibility.Visible;

                        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
                            NameTextBox.Text = Path.GetFileNameWithoutExtension(filePath);
                    }
                    else
                    {
                        CustomMessageBox.ShowError(
                            $"Неверные размеры изображения: {bitmap.PixelWidth}x{bitmap.PixelHeight}\n\nТребуется: 64x64 или 64x32"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Error loading image: {ex.Message}");
                    CustomMessageBox.ShowError("Не удалось загрузить изображение");
                }
            }
        }

        private string DetectSkinModel(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(filePath));
                var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

                int width = source.PixelWidth;
                int height = source.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                source.CopyPixels(pixels, stride, 0);

                int transparentPixels = 0;
                int totalPixels = 0;

                for (int y = 20; y < 32 && y < height; y++)
                {
                    for (int x = 54; x < 56 && x < width; x++)
                    {
                        int offset = y * stride + x * 4;
                        if (offset + 3 < pixels.Length)
                        {
                            byte alpha = pixels[offset + 3];
                            if (alpha == 0)
                                transparentPixels++;
                            totalPixels++;
                        }
                    }
                }

                bool isAlex = totalPixels > 0 && ((double)transparentPixels / totalPixels) > 0.8;

                Debug.WriteLine($"🔍 Arm detection: {transparentPixels}/{totalPixels} transparent pixels = {(isAlex ? "Alex (slim/тонкие руки)" : "Steve (classic/толстые руки)")}");

                return isAlex ? "alex" : "steve";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Model detection failed: {ex.Message}");
                return "steve";
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedFilePath))
            {
                CustomMessageBox.ShowWarning("Выберите файл скина");
                return;
            }

            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                CustomMessageBox.ShowWarning("Введите название скина");
                NameTextBox.Focus();
                return;
            }

            var selectedModelItem = ModelComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var model = selectedModelItem?.Tag?.ToString() ?? "steve";

            UploadButton.IsEnabled = false;
            UploadButton.Content = "⏳ Загрузка...";
            CloseButtonTop.IsEnabled = false;

            try
            {
                var result = await SkinLibraryService.UploadSkinAsync(
                    _selectedFilePath,
                    name,
                    model,
                    _username
                );

                if (result?.Success == true)
                {
                    DialogResult = true;
                    CloseWithAnimation();
                }
                else
                    CustomMessageBox.ShowError("Не удалось загрузить скин. Проверьте подключение к интернету");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error uploading: {ex.Message}");
                CustomMessageBox.ShowError($"Произошла ошибка: {ex.Message}");
            }
            finally
            {
                UploadButton.IsEnabled = true;
                UploadButton.Content = "📤 Загрузить";
                CloseButtonTop.IsEnabled = true;
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