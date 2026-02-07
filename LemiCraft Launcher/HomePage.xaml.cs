using LemiCraft_Launcher.Services;
using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LemiCraft_Launcher.Windows;
using Brushes = System.Windows.Media.Brushes;

namespace LemiCraft_Launcher
{
    public partial class HomePage : Page
    {
        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.Register(nameof(AnimatedVerticalOffset), typeof(double), typeof(HomePage),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        public double AnimatedVerticalOffset
        {
            get => (double)GetValue(AnimatedVerticalOffsetProperty);
            set => SetValue(AnimatedVerticalOffsetProperty, value);
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HomePage page && page.NewsScrollViewer != null)
                page.NewsScrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }

        public HomePage()
        {
            InitializeComponent();
            _ = UpdateServerStatus();
            _ = LoadNewsAsync();
            Loaded += (s, e) => UpdateFades();
        }

        private void NewsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;
            var sv = NewsScrollViewer;
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

        private void NewsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateFades();

        private void UpdateFades()
        {
            var sv = NewsScrollViewer;
            if (sv == null) return;

            double topOpacity = Math.Min(1.0, sv.VerticalOffset / 30.0);
            double bottomOpacity = Math.Min(1.0, (sv.ScrollableHeight - sv.VerticalOffset) / 30.0);

            var topAnim = new DoubleAnimation(topOpacity, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase() };
            var bottomAnim = new DoubleAnimation(bottomOpacity, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase() };

            if (TopFade != null) TopFade.BeginAnimation(OpacityProperty, topAnim);
            if (BottomFade != null) BottomFade.BeginAnimation(OpacityProperty, bottomAnim);
        }

        private async Task UpdateServerStatus()
        {
            var status = await MineStatClient.PingAsync("lemicraft.ru", 25565, 4000);

            if (status == null)
            {
                ServerStatusDot.Foreground = Brushes.Red;
                OnlineText.Text = "0/0";
                PlayersProgressBar.Value = 0;
                return;
            }

            OnlineText.Text = $"{status.OnlinePlayers}/{status.MaxPlayers}";
            PlayersProgressBar.Maximum = Math.Max(1, status.MaxPlayers);
            PlayersProgressBar.Value = status.OnlinePlayers;
            ServerStatusDot.Foreground = Brushes.LimeGreen;
        }

        private void RulesButton_Click(object sender, RoutedEventArgs e) => OpenUrl("https://lemicraft.ru/rules");

        private void WikiButton_Click(object sender, RoutedEventArgs e) => OpenUrl("https://wiki.lemicraft.ru");

        private void DiscordButton_Click(object sender, RoutedEventArgs e) => OpenUrl("https://discord.gg/ybC6QM8WTM");

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Не удалось открыть ссылку: {ex.Message}");
            }
        }

        private async Task LoadNewsAsync(bool forceRefresh = false)
        {
            try
            {
                NewsLoadingPanel.Visibility = Visibility.Visible;
                EmptyNewsPanel.Visibility = Visibility.Collapsed;
                NewsItemsControl.ItemsSource = null;

                var news = await NewsService.GetNewsAsync(
                    filter: new NewsFilter { Limit = 10 },
                    forceRefresh: forceRefresh
                );

                if (news.Count > 0)
                {
                    NewsItemsControl.ItemsSource = news;
                    EmptyNewsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyNewsPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки новостей: {ex.Message}");
                EmptyNewsPanel.Visibility = Visibility.Visible;
            }
            finally
            {
                NewsLoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void RefreshNews_Click(object sender, RoutedEventArgs e) => await LoadNewsAsync(forceRefresh: true);

        private void NewsCard_Clicked(object sender, NewsItem news)
        {
            var detailWindow = new NewsDetailWindow(news);
            detailWindow.Owner = Window.GetWindow(this);
            detailWindow.ShowDialog();
        }

    }
}
