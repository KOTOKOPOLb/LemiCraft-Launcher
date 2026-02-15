using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Services;
using LemiCraft_Launcher.Windows;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LemiCraft_Launcher
{
    public partial class SkinLibraryPage : Page
    {
        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.Register(nameof(AnimatedVerticalOffset), typeof(double), typeof(SkinLibraryPage),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        public double AnimatedVerticalOffset
        {
            get => (double)GetValue(AnimatedVerticalOffsetProperty);
            set => SetValue(AnimatedVerticalOffsetProperty, value);
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkinLibraryPage page && page.SkinsScroller != null)
                page.SkinsScroller.ScrollToVerticalOffset((double)e.NewValue);
        }

        private List<SkinLibraryItem> _allSkins = new();
        private string _username = "";

        public SkinLibraryPage()
        {
            InitializeComponent();
            Loaded += SkinLibraryPage_Loaded;
        }

        private async void SkinLibraryPage_Loaded(object sender, RoutedEventArgs e)
        {
            _username = GetCurrentUsername();

            if (string.IsNullOrEmpty(_username))
            {
                CustomMessageBox.ShowWarning("Для использования библиотеки скинов необходимо войти в аккаунт");
                NavigationService?.GoBack();
                return;
            }

            await LoadSkinsAsync(forceRefresh: false);
            UpdateFades();
        }

        private string GetCurrentUsername()
        {
            try
            {
                var profile = AuthService.LoadProfile();
                return profile?.Username ?? "";
            }
            catch
            {
                return "";
            }
        }

        private async Task LoadSkinsAsync(bool forceRefresh = false)
        {
            try
            {
                ShowLoading();

                var skins = await SkinLibraryService.GetUserSkinsAsync(_username, forceRefresh);
                _allSkins = skins ?? new List<SkinLibraryItem>();

                var sorted = _allSkins.OrderByDescending(s => s.IsActive)
                                      .ThenByDescending(s => s.CreatedAt)
                                      .ToList();

                SkinsGrid.ItemsSource = sorted;
                CountText.Text = $"Всего скинов: {_allSkins.Count}";

                ShowContent(sorted.Any());

                _ = PreloadImagesAsync(sorted);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading skins: {ex.Message}");
                ShowEmpty();
                CustomMessageBox.ShowError("Не удалось загрузить скины. Проверьте подключение к интернету");
            }
        }

        private async Task PreloadImagesAsync(List<SkinLibraryItem> skins)
        {
            var toPreload = skins.Take(10).ToList();

            foreach (var skin in toPreload)
            {
                if (!string.IsNullOrWhiteSpace(skin.ThumbnailUrl))
                {
                    _ = SkinCacheService.GetCachedImageAsync(skin.ThumbnailUrl);
                    await Task.Delay(100);
                }
            }
        }

        private void ShowLoading()
        {
            LoadingPanel.Visibility = Visibility.Visible;
            EmptyPanel.Visibility = Visibility.Collapsed;
            SkinsGrid.Visibility = Visibility.Collapsed;
        }

        private void ShowEmpty()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            EmptyPanel.Visibility = Visibility.Visible;
            SkinsGrid.Visibility = Visibility.Collapsed;
        }

        private void ShowContent(bool hasContent)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;

            if (hasContent)
            {
                EmptyPanel.Visibility = Visibility.Collapsed;
                SkinsGrid.Visibility = Visibility.Visible;
            }
            else
                ShowEmpty();
        }

        private void SkinsScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            var sv = SkinsScroller;
            if (sv == null) return;

            double mouseDelta = -e.Delta;
            double step = mouseDelta * 0.5;
            double target = Math.Max(0, Math.Min(sv.ScrollableHeight, sv.VerticalOffset + step));

            var anim = new DoubleAnimation
            {
                From = sv.VerticalOffset,
                To = target,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(AnimatedVerticalOffsetProperty, anim);
        }

        private void SkinsScroller_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateFades();

        private void UpdateFades()
        {
            var sv = SkinsScroller;
            if (sv == null) return;

            double topOpacity = Math.Min(1.0, sv.VerticalOffset / 30.0);
            double bottomOpacity = Math.Min(1.0, (sv.ScrollableHeight - sv.VerticalOffset) / 30.0);

            var topAnim = new DoubleAnimation(topOpacity, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase()
            };
            var bottomAnim = new DoubleAnimation(bottomOpacity, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase()
            };

            if (TopFade != null) TopFade.BeginAnimation(OpacityProperty, topAnim);
            if (BottomFade != null) BottomFade.BeginAnimation(OpacityProperty, bottomAnim);
        }

        private void SkinCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is SkinLibraryItem skin)
                ShowSkinDetails(skin);
        }

        private void ShowSkinDetails(SkinLibraryItem skin)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.ShowOverlay();

            var detailsWindow = new SkinDetailsWindow(skin, _username);
            detailsWindow.Owner = mainWindow;

            var result = detailsWindow.ShowDialog();

            mainWindow?.HideOverlay();

            if (result == true)
                _ = LoadSkinsAsync(forceRefresh: true);
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.ShowOverlay();

            var uploadWindow = new UploadSkinWindow(_username);
            uploadWindow.Owner = mainWindow;

            var result = uploadWindow.ShowDialog();

            mainWindow?.HideOverlay();

            if (result == true)
                _ = LoadSkinsAsync(forceRefresh: true);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadSkinsAsync(forceRefresh: true);

        private void BackButton_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();
    }
}