using CmlLib.Core.Auth.Microsoft;
using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LemiCraft_Launcher.Windows;
using Button = System.Windows.Controls.Button;

namespace LemiCraft_Launcher
{
    public partial class LoginPage : Page
    {
        private string _currentProvider = "Microsoft";

        public LoginPage()
        {
            InitializeComponent();
        }

        private void MicrosoftSelect_Click(object sender, RoutedEventArgs e)
        {
            _currentProvider = "Microsoft";
            MicrosoftBtn.Style = (Style)FindResource("ModernButton");
            ElyBtn.Style = (Style)FindResource("SecondaryButton");
        }

        private void ElySelect_Click(object sender, RoutedEventArgs e)
        {
            _currentProvider = "Ely.by";
            ElyBtn.Style = (Style)FindResource("ModernButton");
            MicrosoftBtn.Style = (Style)FindResource("SecondaryButton");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToHome();
        }

        private void BackToSelection_Click(object sender, RoutedEventArgs e) => SwitchToSelection();

        //TODO Доделать
        private void ShowCredentialsForm_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBox.ShowInformation("Будет в будущем...");
            return;
            //var fadeOut = (Storyboard)Resources["FadeOutContent"];
            //fadeOut.Completed += (s, args) =>
            //{
            //    ProviderSelection.Visibility = Visibility.Collapsed;
            //    CredentialsForm.Visibility = Visibility.Visible;
            //    CredentialsProviderText.Text = _currentProvider;
            //    LoginTextBox.Text = "";
            //    PasswordBox.Password = "";
            //    ErrorPanel.Visibility = Visibility.Collapsed;
            //    var fadeIn = (Storyboard)Resources["FadeInContent"];
            //    fadeIn.Begin();
            //    LoginTextBox.Focus();
            //};
            //fadeOut.Begin();
        }

        private void SwitchToSelection()
        {
            var fadeOut = (Storyboard)Resources["FadeOutContent"];
            fadeOut.Completed += (s, args) =>
            {
                CredentialsForm.Visibility = Visibility.Collapsed;
                ProviderSelection.Visibility = Visibility.Visible;
                var fadeIn = (Storyboard)Resources["FadeInContent"];
                fadeIn.Begin();
            };
            fadeOut.Begin();
        }

        private async void BrowserLogin_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.ShowOverlay(true);

            try
            {
                if (_currentProvider == "Microsoft")
                {
                    var loginHandler = JELoginHandlerBuilder.BuildDefault();
                    var session = await loginHandler.AuthenticateInteractively();

                    if (session != null)
                    {
                        var profile = new UserProfile
                        {
                            Username = session.Username ?? "",
                            AccessToken = session.AccessToken ?? "",
                            Uuid = session.UUID ?? "",
                            Provider = "Microsoft",
                            LastLogin = DateTime.Now
                        };

                        AuthService.SaveProfile(profile);

                        if (mainWindow != null)
                        {
                            mainWindow.UpdateAccountInfo(session.Username ?? "Unknown", true);
                            await mainWindow.LoadUserAvatarAsync(session.Username ?? "Unknown");
                            mainWindow.NavigateToHome();
                        }
                    }
                }
                else if (_currentProvider == "Ely.by")
                {
                    var res = await AuthService.LoginElyByOAuthAsync(msg =>
                    {
                        Dispatcher.Invoke(() => CustomMessageBox.ShowSuccess(msg, "Ely.by"));
                    });

                    if (res.Success && res.Profile != null)
                    {
                        AuthService.SaveProfile(res.Profile);
                        if (mainWindow != null)
                        {
                            mainWindow.UpdateAccountInfo(res.Profile.Username, true);
                            await mainWindow.LoadUserAvatarAsync(res.Profile.Username);
                            mainWindow.NavigateToHome();
                        }
                    }
                    else
                        CustomMessageBox.ShowError(res.ErrorMessage ?? "Ошибка");
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ошибка: {ex.Message}");
            }
            finally
            {
                mainWindow?.HideOverlay();
            }
        }

        private async void CredentialsLogin_Click(object sender, RoutedEventArgs e)
        {
            var login = LoginTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Введите email или логин");
                LoginTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите пароль");
                PasswordBox.Focus();
                return;
            }

            ErrorPanel.Visibility = Visibility.Collapsed;
            LoadingIndicator.Visibility = Visibility.Visible;
            SetControlsEnabled(false);

            try
            {
                AuthResult result = await AuthService.LoginElyByAsync(login, password);

                if (!result.Success && result.ErrorMessage != null && result.ErrorMessage.Contains("two factor", StringComparison.OrdinalIgnoreCase) || result.ErrorMessage.Contains("Account protected", StringComparison.OrdinalIgnoreCase))
                {
                    var token = Microsoft.VisualBasic.Interaction.InputBox("У аккаунта включена двухфакторная аутентификация.\nВведите код (TOTP) и нажмите OK:", "Двухфакторная аутентификация", "");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        var passwordWithToken = password + ":" + token.Trim();
                        result = await AuthService.LoginElyByAsync(login, passwordWithToken);
                    }
                }

                if (result.Success && result.Profile != null)
                {
                    AuthService.SaveProfile(result.Profile);
                    _ = AvatarService.PreloadAvatarAsync(result.Profile.Username, use3D: true);
                    var mainWindow = Window.GetWindow(this) as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.UpdateAccountInfo(result.Profile.Username, true);
                        await mainWindow.LoadUserAvatarAsync(result.Profile.Username);
                        mainWindow.NavigateToHome();
                        Focus();
                    }
                }
                else
                    ShowError(result.ErrorMessage ?? "Неизвестная ошибка");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                SetControlsEnabled(true);
            }
        }
        
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void SetControlsEnabled(bool enabled)
        {
            LoginTextBox.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;

            foreach (var child in ((StackPanel)CredentialsForm).Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Button btn)
                            btn.IsEnabled = enabled;
                    }
                }
            }
        }

        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            string url = _currentProvider == "Ely.by"
                ? "https://account.ely.by/register"
                : "https://www.minecraft.net/store/minecraft-java-bedrock-edition-pc";

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowError($"Ошибка: {ex.Message}");
            }
        }
    }
}
