using LemiCraft_Launcher.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LemiCraft_Launcher.Windows
{
    public partial class ModpackImportWindow : Window
    {
        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.Register(nameof(AnimatedVerticalOffset), typeof(double), typeof(ModpackImportWindow),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        public double AnimatedVerticalOffset
        {
            get => (double)GetValue(AnimatedVerticalOffsetProperty);
            set => SetValue(AnimatedVerticalOffsetProperty, value);
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModpackImportWindow w && w.ModListScroll != null)
                w.ModListScroll.ScrollToVerticalOffset((double)e.NewValue);
        }

        private ImportData? _importData;
        private string? _resolvedCode;
        private CancellationTokenSource? _cts;
        private bool _isClosing;

        public bool ImportSuccessful { get; private set; }

        public ModpackImportWindow(string? prefillCode = null)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(prefillCode))
            {
                CodeInput.Text = prefillCode;
                Loaded += async (_, _) => await RunPreviewAsync(prefillCode);
            }
            else
            {
                Loaded += (_, _) =>
                {
                    UpdateFades();
                    CodeInput.Focus();
                };
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isClosing)
            {
                e.Cancel = true;
                AnimateClose();
            }
            else
                base.OnClosing(e);
        }

        private void AnimateClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            _cts?.Cancel();

            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var slideOut = new DoubleAnimation(10, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
            ((TranslateTransform)RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideOut);
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => AnimateClose();

        private void OpenSiteBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://lemicraft.ru/mods",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void CodeInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                PreviewBtn_Click(sender, new RoutedEventArgs());
        }

        private async void PreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            var code = CodeInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(code)) return;
            await RunPreviewAsync(code);
        }

        private async void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_importData == null || _resolvedCode == null) return;

            _cts = new CancellationTokenSource();
            SetState(State.Importing);
            ImportBtn.IsEnabled = false;
            PreviewBtn.IsEnabled = false;
            CancelButton.IsEnabled = false;

            var progress = new Progress<(string Status, int Percent)>(p =>
            {
                ProgressText.Text = p.Status;
                ImportProgress.Value = p.Percent;
            });

            try
            {
                await ModpackImportService.ImportAsync(
                    _resolvedCode,
                    _importData.Configs,
                    progress,
                    _cts.Token);

                ImportSuccessful = true;
                CustomMessageBox.ShowSuccess(
                    $"Моды успешно импортированы!\nУстановлено: {_importData.Mods.Count} модов");
                AnimateClose();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ошибка импорта:\n{ex.Message}");
                SetState(State.Preview);
            }
            finally
            {
                ImportBtn.IsEnabled = true;
                PreviewBtn.IsEnabled = true;
                CancelButton.IsEnabled = true;
                _cts = null;
            }
        }

        private async Task RunPreviewAsync(string code)
        {
            SetState(State.Loading);
            PreviewBtn.IsEnabled = false;
            CodeInput.IsReadOnly = true;
            _importData = null;
            _resolvedCode = null;

            try
            {
                var data = await ModpackImportService.GetImportDataAsync(code);

                if (data == null)
                {
                    ErrorText.Text = "Код не найден или истёк (действует 30 минут)";
                    SetState(State.Error);
                    return;
                }

                var (known, unknown) = await ModpackImportService.ResolveModsAsync(data);

                foreach (var id in unknown)
                    known.Add(new ModInfo { Id = id, Name = id, Version = "неизвестно" });

                ModList.ItemsSource = known;
                ModsHeader.Text = $"Список модов  •  {known.Count} шт.";
                ModsHeader.Visibility = Visibility.Visible;

                _importData = data;
                _resolvedCode = code;

                SetState(State.Preview);
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Ошибка: {ex.Message}";
                SetState(State.Error);
            }
            finally
            {
                PreviewBtn.IsEnabled = true;
                CodeInput.IsReadOnly = false;
            }
        }

        private void ModListScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;
            var sv = ModListScroll;
            if (sv == null) return;

            double target = Math.Max(0, Math.Min(sv.ScrollableHeight, sv.VerticalOffset + (-e.Delta * 0.5)));

            var anim = new DoubleAnimation
            {
                From = sv.VerticalOffset,
                To = target,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(AnimatedVerticalOffsetProperty, anim);
        }

        private void ModListScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
            => UpdateFades();

        private void UpdateFades()
        {
            var sv = ModListScroll;
            if (sv == null || sv.Visibility != Visibility.Visible) return;

            double topOpacity = Math.Min(1.0, sv.VerticalOffset / 6.0);
            double bottomOpacity = sv.ScrollableHeight > 0
                ? Math.Min(1.0, (sv.ScrollableHeight - sv.VerticalOffset) / 6.0)
                : 0.0;

            var ease = new QuadraticEase();
            TopFade.BeginAnimation(OpacityProperty,
                new DoubleAnimation(topOpacity, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
            BottomFade.BeginAnimation(OpacityProperty,
                new DoubleAnimation(bottomOpacity, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
        }

        private enum State { Idle, Loading, Preview, Error, Importing }

        private void SetState(State state)
        {
            IdlePanel.Visibility = state == State.Idle ? Visibility.Visible : Visibility.Collapsed;
            LoadingPanel.Visibility = state == State.Loading ? Visibility.Visible : Visibility.Collapsed;
            ErrorPanel.Visibility = state == State.Error ? Visibility.Visible : Visibility.Collapsed;
            ModListScroll.Visibility = state == State.Preview ? Visibility.Visible : Visibility.Collapsed;
            ProgressPanel.Visibility = state == State.Importing ? Visibility.Visible : Visibility.Collapsed;

            ImportBtn.IsEnabled = state == State.Preview;

            if (state != State.Preview)
            {
                ModsHeader.Visibility = Visibility.Collapsed;
                TopFade.Opacity = 0;
                BottomFade.Opacity = 0;
            }
            else
                Dispatcher.InvokeAsync(UpdateFades, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}
