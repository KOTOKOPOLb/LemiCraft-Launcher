using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LemiCraft_Launcher.Windows;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace LemiCraft_Launcher
{
    public partial class GameLogsWindow : Window
    {
        private int _lineCount = 0;
        private readonly Stopwatch _timer = new();
        private bool _inXmlBlock = false;
        private string _xmlBuffer = "";

        public GameLogsWindow()
        {
            InitializeComponent();
            _timer.Start();

            LogTextBox.Dispatcher.InvokeAsync(() =>
            {
                var fd = LogTextBox.Document;
                if (fd != null)
                    fd.ColumnWidth = Math.Max(100, LogTextBox.ActualWidth);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += (s, e) => UpdateTimer();
            dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            dispatcherTimer.Start();
        }

        private void UpdateTimer()
        {
            var elapsed = _timer.Elapsed;
            TimestampText.Text = $"{elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        public void AppendLog(string line)
        {
            Dispatcher.Invoke(() =>
            {
                var parsedLine = ParseLogLine(line);

                if (!string.IsNullOrEmpty(parsedLine))
                {
                    AppendColoredLog(parsedLine);
                    _lineCount++;
                    LineCountText.Text = $"Строк: {_lineCount}";
                }

                LogTextBox.CaretPosition = LogTextBox.Document.ContentEnd;
                LogTextBox.Focusable = false;
                LogTextBox.ScrollToEnd();
            });
        }

        private void AppendColoredLog(string line)
        {
            Color color = Colors.White;

            if (line.Contains("/INFO]") || line.Contains("/main/INFO") || line.Contains("/Datafixer Bootstrap/INFO"))
                color = Color.FromRgb(229, 231, 235);
            else if (line.Contains("/WARN]") || line.Contains("/WARN"))
                color = Color.FromRgb(251, 191, 36);
            else if (line.Contains("/ERROR]") || line.Contains("[ERROR]") || line.Contains("/ERROR"))
                color = Color.FromRgb(239, 68, 68);
            else if (line.Contains("/DEBUG]") || line.Contains("/DEBUG"))
                color = Color.FromRgb(156, 163, 175);

            var lines = line.Replace("\r\n", "\n").Split('\n');
            foreach (var l in lines)
            {
                var p = new Paragraph { Margin = new Thickness(0) };
                var run = new Run(l) { Foreground = new SolidColorBrush(color) };
                p.Inlines.Add(run);
                LogTextBox.Document.Blocks.Add(p);
            }
        }

        private string ParseLogLine(string line)
        {
            if (line.TrimStart().StartsWith("<log4j:Event"))
            {
                _inXmlBlock = true;
                _xmlBuffer = line;
                return "";
            }

            if (_inXmlBlock)
            {
                _xmlBuffer += "\n" + line;

                if (line.TrimStart().StartsWith("</log4j:Event>"))
                {
                    _inXmlBlock = false;
                    var parsed = ParseXmlLogEntry(_xmlBuffer);
                    _xmlBuffer = "";
                    return parsed;
                }

                return "";
            }

            return line;
        }

        private string ParseXmlLogEntry(string xml)
        {
            try
            {
                var timestampMatch = Regex.Match(xml, @"timestamp=""(\d+)""");
                var timestamp = "";
                if (timestampMatch.Success)
                {
                    var ms = long.Parse(timestampMatch.Groups[1].Value);
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
                    timestamp = dt.ToString("HH:mm:ss");
                }

                var threadMatch = Regex.Match(xml, @"thread=""([^""]*)""");
                var thread = threadMatch.Success ? threadMatch.Groups[1].Value : "Unknown";

                var levelMatch = Regex.Match(xml, @"level=""([^""]*)""");
                var level = levelMatch.Success ? levelMatch.Groups[1].Value : "INFO";

                var messageMatch = Regex.Match(xml, @"<!\[CDATA\[(.*?)\]\]>", RegexOptions.Singleline);
                var message = messageMatch.Success ? messageMatch.Groups[1].Value.TrimEnd() : "";

                return $"[{timestamp}] [{thread}/{level}]: {message}";
            }
            catch
            {
                return xml;
            }
        }

        public void SetStatus(string status, string color = "#22C55E")
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"● {status}";
                StatusText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(color)
                );
            });
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

        private void ScrollToBottom_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.CaretPosition = LogTextBox.Document.ContentEnd;
            LogTextBox.ScrollToEnd();
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textRange = new TextRange(LogTextBox.Document.ContentStart, LogTextBox.Document.ContentEnd);
                Clipboard.SetText(textRange.Text);
                CustomMessageBox.ShowInformation("Логи скопированы в буфер обмена!", "Успех");
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ошибка копирования: {ex.Message}");
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Document.Blocks.Clear();
            _lineCount = 0;
            LineCountText.Text = "Строк: 0";
            _inXmlBlock = false;
            _xmlBuffer = "";
        }
    }
}