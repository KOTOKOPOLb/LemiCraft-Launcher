using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Services;
using LemiCraft_Launcher.Windows;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LemiCraft_Launcher
{
    public partial class SettingsPage : Page
    {
        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.Register(nameof(AnimatedVerticalOffset), typeof(double), typeof(SettingsPage),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        public double AnimatedVerticalOffset
        {
            get => (double)GetValue(AnimatedVerticalOffsetProperty);
            set => SetValue(AnimatedVerticalOffsetProperty, value);
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SettingsPage page && page.MainScrollViewer != null)
                page.MainScrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            Loaded += (s, e) => UpdateFades();
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            var sv = MainScrollViewer;
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

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateFades();

        private void UpdateFades()
        {
            var sv = MainScrollViewer;
            if (sv == null) return;

            double topOpacity = Math.Min(1.0, sv.VerticalOffset / 30.0);
            double bottomOpacity = Math.Min(1.0, (sv.ScrollableHeight - sv.VerticalOffset) / 30.0);

            var topAnim = new DoubleAnimation(topOpacity, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase() };
            var bottomAnim = new DoubleAnimation(bottomOpacity, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase() };

            TopFade.BeginAnimation(OpacityProperty, topAnim);
            BottomFade.BeginAnimation(OpacityProperty, bottomAnim);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToHome();
        }

        private void LoadSettings()
        {
            var cfg = ConfigService.Load();

            int maxRam = GetMaxRam();

            RamSlider.Maximum = maxRam;
            RamSlider.Value = Math.Min(cfg.RamGb, maxRam);

            JvmArgsBox.Text = cfg.JvmArgs;
            JavaPathBox.Text = cfg.JavaPath;
            GamePathBox.Text = cfg.GamePath;

            ShowLogsCheckBox.IsChecked = cfg.ShowLogs;
            AutoConnectCheckBox.IsChecked = cfg.AutoConnect;

            LauncherBehaviorCombo.SelectedIndex = cfg.LauncherBehavior;
        }

        private int GetMaxRam()
        {
            try
            {
                ulong totalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
                return (int)(totalMemory / (1024 * 1024 * 1024));
            }
            catch
            {
                return 16;
            }
        }

        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamValueText != null)
                RamValueText.Text = $"{(int)RamSlider.Value} GB";
        }

        private void BrowseJavaButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите исполняемый файл Java",
                Filter = "Java executable (java.exe)|java.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
                JavaPathBox.Text = dialog.FileName;
        }

        private void ChangeGamePathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Выберите папку для установки игры",
                ShowNewFolderButton = true,
                SelectedPath = GamePathBox.Text
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var selectedPath = dialog.SelectedPath;

                var result = CustomMessageBox.ShowQuestion(
                    "Вы уверены что хотите изменить путь установки игры?\n\n" +
                    $"Новый путь: {selectedPath}\n\n" +
                    "ВНИМАНИЕ: Уже установленные файлы НЕ будут перенесены автоматически!"
                );

                if (result == CustomMessageBox.MessageBoxResult.Yes)
                {
                    GamePathBox.Text = selectedPath;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var oldConfig = ConfigService.Load();
            var oldShowLogs = oldConfig.ShowLogs;
            var oldGamePath = oldConfig.GamePath;

            SaveSettings();

            var newConfig = ConfigService.Load();
            var newGamePath = newConfig.GamePath;

            if (oldGamePath != newGamePath)
            {
                MinecraftLauncherService.ResetLauncherCache();

                CustomMessageBox.ShowInformation(
                    "Путь установки игры изменен!\n\n" +
                    $"Новый путь: {newGamePath}\n\n" +
                    "Если игра уже была установлена, её нужно установить заново."
                );
            }

            if (!oldShowLogs && newConfig.ShowLogs)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.OpenLogsWindow();
            }

            var mainWindow2 = Window.GetWindow(this) as MainWindow;
            mainWindow2?.NavigateToHome();
        }

        private void SaveSettings()
        {
            var cfg = new LauncherConfig
            {
                RamGb = (int)RamSlider.Value,
                JvmArgs = JvmArgsBox.Text,
                JavaPath = JavaPathBox.Text,
                GamePath = GamePathBox.Text,
                ShowLogs = ShowLogsCheckBox.IsChecked == true,
                AutoConnect = AutoConnectCheckBox.IsChecked == true,
                LauncherBehavior = LauncherBehaviorCombo.SelectedIndex
            };

            ConfigService.Save(cfg);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = CustomMessageBox.ShowQuestion(
                "Вы уверены, что хотите сбросить все настройки?"
            );

            if (result == CustomMessageBox.MessageBoxResult.Yes)
            {
                RamSlider.Value = 4;
                JvmArgsBox.Text = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions";
                JavaPathBox.Text = "Автоопределение";

                GamePathBox.Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LemiCraft"
                );

                LauncherBehaviorCombo.SelectedIndex = 2;
                ShowLogsCheckBox.IsChecked = false;
                AutoConnectCheckBox.IsChecked = false;

                CustomMessageBox.ShowSuccess("Настройки сброшены");
            }
        }
    }
}