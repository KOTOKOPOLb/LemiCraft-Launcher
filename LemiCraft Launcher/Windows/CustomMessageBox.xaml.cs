using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;

namespace LemiCraft_Launcher.Windows
{
    public partial class CustomMessageBox : Window
    {
        public enum MessageBoxResult
        {
            None,
            Yes,
            No,
            OK
        }

        public enum MessageBoxType
        {
            Information,
            Question,
            Success,
            Error,
            Warning
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private CustomMessageBox(string message, string title, MessageBoxType type, bool showYesNo)
        {
            InitializeComponent();

            ShowInTaskbar = true;
            Topmost = true;

            Title = title;
            MessageText.Text = message;
            TitleText.Text = title;

            switch (type)
            {
                case MessageBoxType.Information:
                    IconText.Text = "ℹ️";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    break;
                case MessageBoxType.Question:
                    IconText.Text = "❓";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                    break;
                case MessageBoxType.Success:
                    IconText.Text = "✅";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    break;
                case MessageBoxType.Error:
                    IconText.Text = "❌";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    break;
                case MessageBoxType.Warning:
                    IconText.Text = "⚠️";
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                    break;
            }

            if (showYesNo)
            {
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                OKButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
                OKButton.Visibility = Visibility.Visible;
            }

            Opacity = 0;
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);
            Loaded += CustomMessageBox_Loaded;
        }

        private void CustomMessageBox_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            var scale = (ScaleTransform)RootBorder.RenderTransform;
            var scaleAnim = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            CloseWithAnimation();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            CloseWithAnimation();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            CloseWithAnimation();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.None;
            CloseWithAnimation();
        }

        private void CloseWithAnimation()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);

            if (RootBorder.RenderTransform is ScaleTransform st)
            {
                var scaleAnim = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }

        public static void ShowInformation(string message, string title = "Информация")
        {
            var msgBox = new CustomMessageBox(message, title, MessageBoxType.Information, false);
            msgBox.ShowDialog();
        }

        public static void ShowSuccess(string message, string title = "Успех")
        {
            var msgBox = new CustomMessageBox(message, title, MessageBoxType.Success, false);
            msgBox.ShowDialog();
        }

        public static void ShowError(string message, string title = "Ошибка")
        {
            var msgBox = new CustomMessageBox(message, title, MessageBoxType.Error, false);
            msgBox.ShowDialog();
        }

        public static void ShowWarning(string message, string title = "Внимание")
        {
            var msgBox = new CustomMessageBox(message, title, MessageBoxType.Warning, false);
            msgBox.ShowDialog();
        }

        public static MessageBoxResult ShowQuestion(string message, string title = "Вопрос")
        {
            var msgBox = new CustomMessageBox(message, title, MessageBoxType.Question, true);
            msgBox.ShowDialog();
            return msgBox.Result;
        }
    }
}