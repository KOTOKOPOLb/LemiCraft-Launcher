using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace LemiCraft_Launcher
{
    public partial class GameLogsWindow : Window
    {
        private int _lineCount = 0;
        private readonly Stopwatch _timer = new();

        public GameLogsWindow()
        {
            InitializeComponent();
            _timer.Start();
            
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
                LogTextBox.AppendText(line + Environment.NewLine);
                _lineCount++;
                LineCountText.Text = $"Строк: {_lineCount}";
                
                LogScrollViewer.ScrollToEnd();
            });
        }

        public void SetStatus(string status, string color = "#22C55E")
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"• {status}";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)
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
            if (LogScrollViewer != null) LogScrollViewer.ScrollToEnd();
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(LogTextBox.Text);
                MessageBox.Show("Логи скопированы в буфер обмена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка копирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            _lineCount = 0;
            LineCountText.Text = "Строк: 0";
        }
    }
}
