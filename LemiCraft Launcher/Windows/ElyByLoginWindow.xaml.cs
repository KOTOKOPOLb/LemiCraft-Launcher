using LemiCraft_Launcher.Services;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace LemiCraft_Launcher.Windows
{
    public partial class ElyByLoginWindow : Window
    {
        public ElyCookies? ExtractedCookies { get; private set; }

        private string? _capturedPhpSessId;
        private string? _capturedIdentity;

        public ElyByLoginWindow()
        {
            InitializeComponent();
            Loaded += ElyByLoginWindow_Loaded;
        }

        private async void ElyByLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LemiCraft", "WebView2Profile");

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await WebView.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 init error: {ex.Message}");
                CustomMessageBox.ShowError(
                    $"Не удалось загрузить встроенный браузер:\n{ex.Message}\n\n" +
                    "Убедитесь, что WebView2 Runtime установлен");
                Close();
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess) { CustomMessageBox.ShowError("Не удалось инициализировать WebView2"); Close(); return; }
            var wv = WebView.CoreWebView2;
            wv.WebResourceResponseReceived += WebView_WebResourceResponseReceived;
            wv.NavigationStarting += (s, ev) => Dispatcher.Invoke(() => PageUrlText.Text = ev.Uri);
            wv.Navigate("https://ely.by/authorization/login");
        }

        private void WebView_WebResourceResponseReceived(object? sender,
            CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                if (!e.Request.Uri.StartsWith("https://ely.by/")) return;

                string? setCookie = null;
                try { setCookie = e.Response.Headers.GetHeader("Set-Cookie"); }
                catch { return; }

                if (string.IsNullOrEmpty(setCookie)) return;

                Debug.WriteLine($"[Set-Cookie from ely.by]: {setCookie}");

                bool changed = false;
                foreach (var line in setCookie.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(';')[0].Trim().Split('=', 2);
                    if (parts.Length != 2) continue;

                    var name = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (string.IsNullOrEmpty(value)) continue;

                    switch (name)
                    {
                        case "identity":
                            if (_capturedIdentity != value) { _capturedIdentity = value; changed = true; Debug.WriteLine("✅ identity captured!"); }
                            break;
                        case "PHPSESSID":
                            if (_capturedPhpSessId != value) { _capturedPhpSessId = value; changed = true; Debug.WriteLine("✅ PHPSESSID captured!"); }
                            break;
                    }
                }

                if (changed) UpdateLoginState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebResourceResponseReceived error: {ex.Message}");
            }
        }

        private void UpdateLoginState()
        {
            bool loggedIn = !string.IsNullOrEmpty(_capturedIdentity) && !string.IsNullOrEmpty(_capturedPhpSessId);

            Dispatcher.Invoke(() =>
            {
                if (loggedIn)
                {
                    ExtractedCookies = new ElyCookies { Identity = _capturedIdentity, PhpSessId = _capturedPhpSessId };
                    StatusText.Text = "✅ Вы вошли в аккаунт";
                    ExtractButton.IsEnabled = true;
                }
                else
                {
                    ExtractedCookies = null;
                    StatusText.Text = "⏳ Ожидание входа...";
                    ExtractButton.IsEnabled = false;
                }
            });
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var uri = WebView.CoreWebView2?.Source ?? "";
            Dispatcher.Invoke(() => PageUrlText.Text = uri);

            if (uri.StartsWith("https://ely.by/") && !uri.Contains("/authorization/"))
                CheckIfAlreadyLoggedIn();
        }

        private void CheckIfAlreadyLoggedIn()
        {
            if (ExtractedCookies?.IsValid == true)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "✅ Вы уже вошли в аккаунт";
                    ExtractButton.IsEnabled = true;

                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ElyByCookieService.SetCookies(ExtractedCookies);
                            DialogResult = true;
                            Close();
                        });
                    });
                });
            }
        }

        private void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExtractedCookies?.IsValid == true)
            {
                var profile = AuthService.LoadProfile();
                if (profile != null)
                {
                    profile.ElybyPhpSessId = ExtractedCookies.PhpSessId;
                    profile.ElybyIdentity = ExtractedCookies.Identity;
                    profile.ElybyCookiesExpiry = DateTime.Now.AddDays(7);

                    AuthService.SaveProfile(profile);
                    Debug.WriteLine("💾 Ely.by cookies saved to profile");
                }

                DialogResult = true;
                Close();
            }
            else
                CustomMessageBox.ShowWarning("Войдите в аккаунт Ely.by");
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ExtractedCookies = null;
            DialogResult = false;
            Close();
        }
    }
}