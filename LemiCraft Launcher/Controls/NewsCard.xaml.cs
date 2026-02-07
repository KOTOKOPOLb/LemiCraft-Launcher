using LemiCraft_Launcher.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using UserControl = System.Windows.Controls.UserControl;

namespace LemiCraft_Launcher.Controls
{
    public partial class NewsCard : UserControl
    {
        public static readonly DependencyProperty NewsItemProperty =
            DependencyProperty.Register(nameof(NewsItem), typeof(NewsItem), typeof(NewsCard),
                new PropertyMetadata(null, OnNewsItemChanged));

        public NewsItem? NewsItem
        {
            get => (NewsItem?)GetValue(NewsItemProperty);
            set => SetValue(NewsItemProperty, value);
        }

        public event EventHandler<NewsItem>? NewsClicked;

        public NewsCard()
        {
            InitializeComponent();
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private static void OnNewsItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NewsCard card && e.NewValue is NewsItem news)
                card.UpdateUI(news);
        }

        private void UpdateUI(NewsItem news)
        {
            TitleText.Text = news.Title;
            PreviewText.Text = Services.NewsService.GetPreview(news.Content);

            DateText.Text = FormatDate(news.PublishedAt);

            SetCategory(news.Category);

            AuthorText.Text = news.AuthorName;

            if (!string.IsNullOrEmpty(news.AuthorAvatarUrl))
            {
                AuthorAvatar.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri(news.AuthorAvatarUrl));
            }

            if (!string.IsNullOrEmpty(news.ImageUrl))
            {
                ImageContainer.Visibility = Visibility.Visible;
                NewsImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri(news.ImageUrl));
            }
            else
                ImageContainer.Visibility = Visibility.Collapsed;
        }

        private void SetCategory(NewsCategory category)
        {
            switch (category)
            {
                case NewsCategory.Update:
                    CategoryText.Text = "Обновление";
                    CategoryBadge.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    break;

                case NewsCategory.Event:
                    CategoryText.Text = "Ивент";
                    CategoryBadge.Background = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                    break;

                case NewsCategory.Announcement:
                    CategoryText.Text = "Объявление";
                    CategoryBadge.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    break;

                case NewsCategory.Maintenance:
                    CategoryText.Text = "Тех. работы";
                    CategoryBadge.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    break;

                case NewsCategory.General:
                default:
                    CategoryText.Text = "Общее";
                    CategoryBadge.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                    break;
            }
        }

        private string FormatDate(DateTime date)
        {
            var now = DateTime.Now;
            var diff = now - date;

            if (diff.TotalMinutes < 1)
                return "Только что";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} мин назад";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} ч назад";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} дн назад";

            return date.ToString("dd.MM.yyyy");
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (NewsItem != null)
                NewsClicked?.Invoke(this, NewsItem);
        }

        private const double ImageTopCornerRadius = 12.0;

        private void ImageContainer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                UpdateTopCornersClip(fe, ImageTopCornerRadius);
        }

        private void ImageContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                UpdateTopCornersClip(fe, ImageTopCornerRadius);
        }

        private void UpdateTopCornersClip(FrameworkElement element, double radius)
        {
            double w = Math.Max(0, element.ActualWidth);
            double h = Math.Max(0, element.ActualHeight);
            if (w == 0 || h == 0)
                return;

            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(0, radius), true, true);
                ctx.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(w - radius, 0), true, false);
                ctx.ArcTo(new Point(w, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(w, h), true, false);
                ctx.LineTo(new Point(0, h), true, false);
            }
            g.Freeze();

            element.Clip = g;
        }
    }
}
