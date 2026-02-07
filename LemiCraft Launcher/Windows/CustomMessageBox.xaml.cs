using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;

namespace LemiCraft_Launcher.Windows
{
    public partial class CustomMessageBox : Window
    {
        public enum MessageBoxResult
        {
            OK,
            Cancel,
            Yes,
            No
        }

        public enum MessageBoxIcon
        {
            None,
            Information,
            Warning,
            Error,
            Question,
            Success
        }

        public enum MessageBoxButtons
        {
            OK,
            OKCancel,
            YesNo,
            YesNoCancel
        }

        private MessageBoxResult _result = MessageBoxResult.Cancel;

        private bool _isClosingAnimated = false;

        private CustomMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            InitializeComponent();
            
            TitleText.Text = title;
            MessageText.Text = message;
            
            SetIcon(icon);
            SetButtons(buttons);

            Closing += CustomMessageBox_Closing;
        }

        private void CustomMessageBox_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingAnimated)
                return;

            e.Cancel = true;
            BeginCloseAnimation(null);
        }

        private void BeginCloseAnimation(bool? dialogResult)
        {
            if (_isClosingAnimated)
                return;

            _isClosingAnimated = true;

            var sb = (Storyboard)FindResource("WindowFadeOut");
            if (sb == null)
            {
                if (dialogResult.HasValue)
                    DialogResult = dialogResult.Value;
                else
                    Close();
                return;
            }

            void OnCompleted(object s, EventArgs ev)
            {
                sb.Completed -= OnCompleted;

                if (dialogResult.HasValue)
                    DialogResult = dialogResult.Value;
                else
                    Close();
            }

            sb.Completed += OnCompleted;
            sb.Begin(this);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch
                { }
            }
        }

        private void SetIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Information:
                    IconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    break;
                    
                case MessageBoxIcon.Warning:
                    IconPath.Data = Geometry.Parse("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                    break;
                    
                case MessageBoxIcon.Error:
                    IconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    break;
                    
                case MessageBoxIcon.Question:
                    IconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    break;
                    
                case MessageBoxIcon.Success:
                    IconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z");
                    IconPath.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    break;
                    
                case MessageBoxIcon.None:
                default:
                    IconPath.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SetButtons(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    PrimaryButton.Content = "ОК";
                    SecondaryButton.Visibility = Visibility.Collapsed;
                    ButtonsPanel.ColumnDefinitions[0].Width = new GridLength(0);
                    ButtonsPanel.ColumnDefinitions[1].Width = new GridLength(0);
                    Grid.SetColumn(PrimaryButton, 2);
                    break;
                    
                case MessageBoxButtons.OKCancel:
                    PrimaryButton.Content = "ОК";
                    SecondaryButton.Content = "Отмена";
                    SecondaryButton.Visibility = Visibility.Visible;
                    break;
                    
                case MessageBoxButtons.YesNo:
                    PrimaryButton.Content = "Да";
                    SecondaryButton.Content = "Нет";
                    SecondaryButton.Visibility = Visibility.Visible;
                    break;
                    
                case MessageBoxButtons.YesNoCancel:
                    PrimaryButton.Content = "Да";
                    SecondaryButton.Content = "Нет";
                    SecondaryButton.Visibility = Visibility.Visible;
                    // TODO: Добавить третью кнопку Cancel
                    break;
            }
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrimaryButton.Content.ToString() == "Да")
                _result = MessageBoxResult.Yes;
            else
                _result = MessageBoxResult.OK;

            BeginCloseAnimation(true);
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (SecondaryButton.Content.ToString() == "Нет")
                _result = MessageBoxResult.No;
            else
                _result = MessageBoxResult.Cancel;

            BeginCloseAnimation(false);
        }

        public static MessageBoxResult Show(
            string message, 
            string title = "Сообщение",
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None)
        {
            var msgBox = new CustomMessageBox(message, title, buttons, icon);
            msgBox.ShowDialog();
            return msgBox._result;
        }

        public static MessageBoxResult ShowInformation(string message, string title = "Информация") => Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static MessageBoxResult ShowWarning(string message, string title = "Предупреждение") => Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static MessageBoxResult ShowError(string message, string title = "Ошибка") => Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static MessageBoxResult ShowSuccess(string message, string title = "Успех") => Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Success);

        public static MessageBoxResult ShowQuestion(string message, string title = "Подтверждение") => Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        public static MessageBoxResult ShowConfirm(string message, string title = "Подтверждение") => Show(message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
    }
}
