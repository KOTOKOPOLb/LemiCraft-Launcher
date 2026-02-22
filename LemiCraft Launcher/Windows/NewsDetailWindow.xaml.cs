using LemiCraft_Launcher.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace LemiCraft_Launcher.Windows
{
    public partial class NewsDetailWindow : Window
    {
        private readonly NewsItem _news;
        private AnimationClock? _verticalScrollClock;
        private double _scrollTargetOffset = 0;

        private static readonly Regex MentionTokenRegex = new(@"^@(?:\P{L}\P{N})*[\p{L}\p{N}_-]+(?=$|\s|\p{P})", RegexOptions.Compiled);

        public NewsDetailWindow(NewsItem news)
        {
            InitializeComponent();
            _news = news ?? throw new ArgumentNullException(nameof(news));

            Opacity = 0;
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);

            Loaded += NewsDetailWindow_Loaded;
            LoadNewsContent();
            _scrollTargetOffset = DetailScrollViewer?.VerticalOffset ?? 0;
        }

        private void NewsDetailWindow_Loaded(object? sender, RoutedEventArgs e)
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

            UpdateFades();
        }

        private void LoadNewsContent()
        {
            TitleText.Text = _news.Title;
            DateText.Text = _news.PublishedAt.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
            AuthorText.Text = _news.AuthorName;
            CategoryText.Text = GetCategoryText(_news.Category);
            SetCategoryColor(_news.Category);

            if (!string.IsNullOrEmpty(_news.AuthorAvatarUrl))
            {
                try
                {
                    AuthorAvatar.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_news.AuthorAvatarUrl));
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(_news.ImageUrl))
            {
                ImageContainer.Visibility = Visibility.Visible;
                try
                {
                    NewsImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_news.ImageUrl));
                }
                catch
                {
                    ImageContainer.Visibility = Visibility.Collapsed;
                }
            }
            else
                ImageContainer.Visibility = Visibility.Collapsed;

            RenderMarkdown(_news.Content);
        }

        private void RenderMarkdown(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return;

            try
            {
                MarkdownDocument.Blocks.Clear();

                var lines = markdown.Split('\n');
                var currentParagraph = new Paragraph();
                var isCodeBlock = false;
                var codeLines = new List<string>();

                var normalizedTitle = _news.Title.Trim().ToLowerInvariant();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    if (trimmed.StartsWith("```", StringComparison.Ordinal))
                    {
                        if (isCodeBlock)
                        {
                            AddCodeBlock(codeLines);
                            codeLines.Clear();
                            isCodeBlock = false;
                        }
                        else
                        {
                            if (currentParagraph.Inlines.Count > 0)
                            {
                                MarkdownDocument.Blocks.Add(currentParagraph);
                                currentParagraph = new Paragraph();
                            }
                            isCodeBlock = true;
                        }
                        continue;
                    }

                    if (isCodeBlock)
                    {
                        codeLines.Add(line);
                        continue;
                    }

                    if (trimmed.StartsWith("###", StringComparison.Ordinal))
                    {
                        var headText = trimmed[3..].Trim();
                        if (headText.ToLowerInvariant() == normalizedTitle) continue;
                        FlushParagraph(ref currentParagraph);
                        AddHeader(headText, 3);
                        continue;
                    }
                    if (trimmed.StartsWith("##", StringComparison.Ordinal))
                    {
                        var headText = trimmed[2..].Trim();
                        if (headText.ToLowerInvariant() == normalizedTitle) continue;
                        FlushParagraph(ref currentParagraph);
                        AddHeader(headText, 2);
                        continue;
                    }
                    if (trimmed.StartsWith('#') && !trimmed.StartsWith("##", StringComparison.Ordinal))
                    {
                        var headText = trimmed[1..].Trim();
                        if (headText.ToLowerInvariant() == normalizedTitle) continue;
                        FlushParagraph(ref currentParagraph);
                        AddHeader(headText, 1);
                        continue;
                    }

                    if (trimmed.StartsWith(">", StringComparison.Ordinal) || trimmed.StartsWith("> ", StringComparison.Ordinal))
                    {
                        FlushParagraph(ref currentParagraph);
                        var quoteText = trimmed.StartsWith("> ", StringComparison.Ordinal)
                            ? trimmed[2..]
                            : trimmed[1..];
                        AddBlockquote(quoteText.Trim());
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        FlushParagraph(ref currentParagraph);
                        continue;
                    }

                    AddFormattedText(currentParagraph, line);
                    currentParagraph.Inlines.Add(new LineBreak());
                }

                FlushParagraph(ref currentParagraph);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Markdown render error: {ex.Message}");

                var para = new Paragraph(new Run(_news.Content))
                {
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                    LineHeight = 24
                };
                MarkdownDocument.Blocks.Add(para);
            }
        }

        private void FlushParagraph(ref Paragraph paragraph)
        {
            if (paragraph.Inlines.Count > 0)
            {
                paragraph.FontSize = 14;
                paragraph.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                paragraph.LineHeight = 24;
                paragraph.Margin = new Thickness(0, 0, 0, 12);
                MarkdownDocument.Blocks.Add(paragraph);
                paragraph = new Paragraph();
            }
        }

        private void AddHeader(string text, int level)
        {
            var para = new Paragraph();

            switch (level)
            {
                case 1:
                    para.FontSize = 28;
                    para.FontWeight = FontWeights.Bold;
                    para.Margin = new Thickness(0, 24, 0, 16);
                    break;
                case 2:
                    para.FontSize = 22;
                    para.FontWeight = FontWeights.SemiBold;
                    para.Margin = new Thickness(0, 20, 0, 12);
                    break;
                case 3:
                    para.FontSize = 18;
                    para.FontWeight = FontWeights.SemiBold;
                    para.Margin = new Thickness(0, 16, 0, 8);
                    break;
            }

            para.Foreground = Brushes.White;
            AddFormattedText(para, text);
            MarkdownDocument.Blocks.Add(para);
        }

        private void AddCodeBlock(List<string> lines)
        {
            var code = string.Join("\n", lines);
            var para = new Paragraph(new Run(code))
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 238, 243)),
                Background = new SolidColorBrush(Color.FromRgb(7, 16, 32)),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 6, 0, 12)
            };
            MarkdownDocument.Blocks.Add(para);
        }

        private void AddBlockquote(string text)
        {
            var para = new Paragraph();
            AddFormattedText(para, text);

            para.FontSize = 14;
            para.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            para.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81));
            para.BorderThickness = new Thickness(3, 0, 0, 0);
            para.Padding = new Thickness(14, 6, 14, 6);
            para.Margin = new Thickness(0, 4, 0, 12);

            MarkdownDocument.Blocks.Add(para);
        }

        private void AddFormattedText(Paragraph paragraph, string text)
        {
            var parts = new List<(string text, TextStyle style)>();
            ProcessInlineFormatting(text, parts);

            foreach (var (partText, style) in parts)
            {
                if (style == TextStyle.None && partText.StartsWith('@') && partText.Length > 1)
                {
                    var m = MentionTokenRegex.Match(partText);
                    if (m.Success)
                    {
                        var mentionRun = new Run(m.Value)
                        {
                            Background = new SolidColorBrush(Color.FromArgb(50, 88, 101, 242)),
                            Foreground = new SolidColorBrush(Color.FromRgb(147, 157, 255))
                        };
                        paragraph.Inlines.Add(mentionRun);

                        if (m.Length < partText.Length)
                        {
                            var rest = partText.Substring(m.Length);
                            var restRun = new Run(rest)
                            {
                                Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235))
                            };
                            paragraph.Inlines.Add(restRun);
                        }

                        continue;
                    }
                }

                var run = new Run(partText);

                if (style.HasFlag(TextStyle.Bold))
                    run.FontWeight = FontWeights.Bold;

                if (style.HasFlag(TextStyle.Italic))
                    run.FontStyle = FontStyles.Italic;

                if (style.HasFlag(TextStyle.Strikethrough))
                    run.TextDecorations = TextDecorations.Strikethrough;

                if (style.HasFlag(TextStyle.Code))
                {
                    run.FontFamily = new FontFamily("Consolas, Courier New");
                    run.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                    run.Foreground = new SolidColorBrush(Color.FromRgb(230, 238, 243));
                }

                if (style.HasFlag(TextStyle.Spoiler))
                {
                    run.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                    run.Foreground = new SolidColorBrush(Colors.Transparent);
                }

                paragraph.Inlines.Add(run);
            }
        }

        private static void ProcessInlineFormatting(string text, List<(string text, TextStyle style)> parts)
        {
            var current = "";
            var i = 0;

            while (i < text.Length)
            {
                if (i < text.Length - 1 && text[i] == '|' && text[i + 1] == '|')
                {
                    if (current.Length > 0) { parts.Add((current, TextStyle.None)); current = ""; }
                    var end = text.IndexOf("||", i + 2, StringComparison.Ordinal);
                    if (end != -1)
                    {
                        parts.Add((text[(i + 2)..end], TextStyle.Spoiler));
                        i = end + 2;
                        continue;
                    }
                }

                if (i < text.Length - 1 && text[i] == '*' && text[i + 1] == '*')
                {
                    if (current.Length > 0) { parts.Add((current, TextStyle.None)); current = ""; }
                    var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end != -1)
                    {
                        parts.Add((text[(i + 2)..end], TextStyle.Bold));
                        i = end + 2;
                        continue;
                    }
                }

                if (text[i] == '*' && (i == 0 || text[i - 1] != '*') && (i + 1 >= text.Length || text[i + 1] != '*'))
                {
                    if (current.Length > 0) { parts.Add((current, TextStyle.None)); current = ""; }
                    var end = text.IndexOf('*', i + 1);
                    if (end != -1)
                    {
                        parts.Add((text[(i + 1)..end], TextStyle.Italic));
                        i = end + 1;
                        continue;
                    }
                }

                if (i < text.Length - 1 && text[i] == '~' && text[i + 1] == '~')
                {
                    if (current.Length > 0) { parts.Add((current, TextStyle.None)); current = ""; }
                    var end = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                    if (end != -1)
                    {
                        parts.Add((text[(i + 2)..end], TextStyle.Strikethrough));
                        i = end + 2;
                        continue;
                    }
                }

                if (text[i] == '`')
                {
                    if (current.Length > 0) { parts.Add((current, TextStyle.None)); current = ""; }
                    var end = text.IndexOf('`', i + 1);
                    if (end != -1)
                    {
                        parts.Add((text[(i + 1)..end], TextStyle.Code));
                        i = end + 1;
                        continue;
                    }
                }

                current += text[i];
                i++;
            }

            if (current.Length > 0)
                parts.Add((current, TextStyle.None));
        }

        [Flags]
        private enum TextStyle
        {
            None = 0,
            Bold = 1,
            Italic = 2,
            Strikethrough = 4,
            Code = 8,
            Spoiler = 16
        }

        private static string GetCategoryText(NewsCategory category) => category switch
        {
            NewsCategory.Update => "Обновление",
            NewsCategory.Event => "Ивент",
            NewsCategory.Announcement => "Объявление",
            NewsCategory.Maintenance => "Тех. работы",
            _ => "Общее"
        };

        private void SetCategoryColor(NewsCategory category)
        {
            var color = category switch
            {
                NewsCategory.Update => Color.FromRgb(34, 197, 94),
                NewsCategory.Event => Color.FromRgb(139, 92, 246),
                NewsCategory.Announcement => Color.FromRgb(59, 130, 246),
                NewsCategory.Maintenance => Color.FromRgb(239, 68, 68),
                _ => Color.FromRgb(107, 114, 128)
            };

            CategoryBadge.Background = new SolidColorBrush(color);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
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

        private void OpenDiscord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_news.Url))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _news.Url,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Не удалось открыть ссылку: {ex.Message}");
            }
        }

        private void DetailScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            UpdateFades();
            if (_verticalScrollClock == null)
                _scrollTargetOffset = DetailScrollViewer.VerticalOffset;
        }

        private void SmoothAnimatedScroll(double delta)
        {
            if (DetailScrollViewer == null) return;

            if (_scrollTargetOffset == 0)
                _scrollTargetOffset = DetailScrollViewer.VerticalOffset;

            double factor = 0.6;
            _scrollTargetOffset = Math.Max(0, Math.Min(DetailScrollViewer.ScrollableHeight, _scrollTargetOffset - delta * factor));

            double from = DetailScrollViewer.VerticalOffset;
            double to = _scrollTargetOffset;

            if (Math.Abs(to - from) < 0.5) return;

            try { _verticalScrollClock?.Controller?.Stop(); } catch { }

            double distance = Math.Abs(to - from);
            int durationMs = (int)Math.Clamp(160 + distance * 3.5, 180, 600);

            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            var clock = anim.CreateClock();
            _verticalScrollClock = clock;

            clock.CurrentTimeInvalidated += (s, _) =>
            {
                var progress = clock.CurrentProgress;
                if (progress.HasValue)
                {
                    double current = from + (to - from) * progress.Value;
                    DetailScrollViewer.ScrollToVerticalOffset(current);
                }
            };

            clock.Completed += (s, _) =>
            {
                DetailScrollViewer.ScrollToVerticalOffset(to);
                _verticalScrollClock = null;
            };

            clock.Controller?.Begin();
        }

        private void MarkdownViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            SmoothAnimatedScroll(e.Delta);
        }

        private void DetailScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            SmoothAnimatedScroll(e.Delta);
        }

        private void UpdateFades()
        {
            var sv = DetailScrollViewer;
            if (sv == null) return;

            double topOpacity = Math.Min(1.0, sv.VerticalOffset / 30.0);
            double bottomOpacity = Math.Min(1.0, (sv.ScrollableHeight - sv.VerticalOffset) / 30.0);

            var topAnim = new DoubleAnimation(topOpacity, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase()
            };
            var bottomAnim = new DoubleAnimation(bottomOpacity, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase()
            };

            TopFade?.BeginAnimation(OpacityProperty, topAnim);
            BottomFade?.BeginAnimation(OpacityProperty, bottomAnim);
        }
    }
}