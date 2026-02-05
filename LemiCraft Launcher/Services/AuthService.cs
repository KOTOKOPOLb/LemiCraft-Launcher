using CmlLib.Core.Auth.Microsoft;
using LemiCraft_Launcher.Models;
using LemiCraft_Launcher.Utils;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public UserProfile? Profile { get; set; }
    }

    public static class AuthService
    {
        private static readonly HttpClient _httpClient = new();

        private static readonly string UserDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LemiCraft");

        private static readonly string PlainProfilePath = Path.Combine(UserDataDir, "user.json");
        private static readonly string EncryptedProfilePath = Path.Combine(UserDataDir, "user.sec");

        public static async Task<AuthResult> LoginElyByAsync(string username, string password)
        {
            try
            {
                var payload = new
                {
                    username,
                    password,
                    clientToken = Guid.NewGuid().ToString("N"),
                    requestUser = true
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://authserver.ely.by/auth/authenticate",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorJson = await response.Content.ReadAsStringAsync();

                    try
                    {
                        using var errorDoc = JsonDocument.Parse(errorJson);
                        var errorRoot = errorDoc.RootElement;

                        if (errorRoot.TryGetProperty("errorMessage", out var errorMsg))
                        {
                            return new AuthResult
                            {
                                Success = false,
                                ErrorMessage = errorMsg.GetString() ?? "Ошибка авторизации"
                            };
                        }
                    }
                    catch { }

                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = $"Ошибка: {response.StatusCode}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var responseDoc = JsonDocument.Parse(responseJson);
                var root = responseDoc.RootElement;

                var accessToken = root.GetProperty("accessToken").GetString() ?? "";
                var selectedProfile = root.GetProperty("selectedProfile");
                var uuid = selectedProfile.GetProperty("id").GetString() ?? "";
                var name = selectedProfile.GetProperty("name").GetString() ?? "";

                var profile = new UserProfile
                {
                    Username = name,
                    AccessToken = accessToken,
                    Uuid = uuid,
                    Provider = "Ely.by",
                    LastLogin = DateTime.Now
                };

                return new AuthResult
                {
                    Success = true,
                    Profile = profile
                };
            }
            catch (HttpRequestException ex)
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Ошибка сети: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Неизвестная ошибка: " + ex.Message
                };
            }
        }

        public static async Task<AuthResult> LoginMicrosoftAsync(string username, string password)
        {
            // TODO: Реализовать авторизацию через Microsoft по логину\паролю
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Авторизация через Microsoft временно недоступна.\nИспользуйте Ely.by или вход через браузер"
            };
        }

        public static async Task<bool> ValidateElyByTokenAsync(string accessToken)
        {
            try
            {
                var payload = new
                {
                    accessToken
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://authserver.ely.by/auth/validate",
                    content
                );

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<AuthResult> RefreshElyByTokenAsync(string accessToken)
        {
            try
            {
                var payload = new
                {
                    accessToken,
                    requestUser = true
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://authserver.ely.by/auth/refresh",
                    content
                );

                if (!response.IsSuccessStatusCode)
                    return new AuthResult { Success = false, ErrorMessage = "Не удалось обновить токен" };

                var responseJson = await response.Content.ReadAsStringAsync();
                using var responseDoc = JsonDocument.Parse(responseJson);
                var root = responseDoc.RootElement;

                var newAccessToken = root.GetProperty("accessToken").GetString() ?? "";
                var selectedProfile = root.GetProperty("selectedProfile");
                var uuid = selectedProfile.GetProperty("id").GetString() ?? "";
                var name = selectedProfile.GetProperty("name").GetString() ?? "";

                var profile = new UserProfile
                {
                    Username = name,
                    AccessToken = newAccessToken,
                    Uuid = uuid,
                    Provider = "Ely.by",
                    LastLogin = DateTime.Now
                };

                return new AuthResult { Success = true, Profile = profile };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public static void MigrateIfNeeded()
        {
            try
            {
                if (!File.Exists(EncryptedProfilePath) && File.Exists(PlainProfilePath))
                {
                    var json = File.ReadAllText(PlainProfilePath);
                    CryptoUtils.SaveEncryptedStringToFile(EncryptedProfilePath, json);

                    try
                    {
                        File.Delete(PlainProfilePath);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MigrateIfNeeded error: {ex.Message}");
            }
        }

        public static void SaveProfile(UserProfile profile)
        {
            try
            {
                Directory.CreateDirectory(UserDataDir);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(profile, options);
                CryptoUtils.SaveEncryptedStringToFile(EncryptedProfilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения профиля: {ex.Message}");
            }
        }

        public static UserProfile? LoadProfile()
        {
            try
            {
                var json = CryptoUtils.LoadEncryptedStringFromFile(EncryptedProfilePath);
                if (json != null)
                    return JsonSerializer.Deserialize<UserProfile>(json);

                if (File.Exists(PlainProfilePath))
                {
                    var plainJson = File.ReadAllText(PlainProfilePath);
                    return JsonSerializer.Deserialize<UserProfile>(plainJson);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfile error: {ex.Message}");
                return null;
            }
        }

        public static void Logout()
        {
            try
            {
                if (File.Exists(EncryptedProfilePath)) File.Delete(EncryptedProfilePath);
                if (File.Exists(PlainProfilePath)) File.Delete(PlainProfilePath);
            }
            catch { }
        }

        public static async Task<AuthResult> AutoLoginAsync()
        {
            var profile = LoadProfile();

            if (profile == null)
                return new AuthResult { Success = false, ErrorMessage = "Нет сохраненного профиля" };

            if (profile.Provider == "Ely.by")
            {
                var isValid = await ValidateElyByTokenAsync(profile.AccessToken);

                if (isValid)
                {
                    profile.LastLogin = DateTime.Now;
                    SaveProfile(profile);
                    return new AuthResult { Success = true, Profile = profile };
                }

                var refreshResult = await RefreshElyByTokenAsync(profile.AccessToken);

                if (refreshResult.Success && refreshResult.Profile != null)
                {
                    SaveProfile(refreshResult.Profile);
                    return refreshResult;
                }
            }
            if (profile.Provider == "Microsoft")
            {
                try
                {
                    var loginHandler = JELoginHandlerBuilder.BuildDefault();
                    var session = await loginHandler.Authenticate();
                    if (session != null && !string.IsNullOrWhiteSpace(session.AccessToken))
                    {
                        profile.Username = session.Username;
                        profile.AccessToken = session.AccessToken;
                        profile.Uuid = session.UUID;
                        profile.LastLogin = DateTime.Now;
                        SaveProfile(profile);

                        return new AuthResult { Success = true, Profile = profile };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("AutoLogin Microsoft error: " + ex);
                }

                return new AuthResult { Success = false, ErrorMessage = "Microsoft: silent login не удался" };
            }

            return new AuthResult { Success = false, ErrorMessage = "Сессия устарела. Войдите заново" };
        }

        public static async Task<AuthResult> LoginElyByOAuthAsync(Action<string>? showMessageCallback = null)
        {
            var cfg = ConfigService.Load();

            var apiBaseUrl = cfg.ApiBaseUrl;

            try
            {
                var urlResponse = await _httpClient.GetAsync($"{apiBaseUrl}/auth/ely/url");
                if (!urlResponse.IsSuccessStatusCode)
                {
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось получить OAuth URL с сервера"
                    };
                }

                var urlJson = await urlResponse.Content.ReadAsStringAsync();
                using var urlDoc = JsonDocument.Parse(urlJson);
                var root = urlDoc.RootElement;

                var authUrl = root.GetProperty("authUrl").GetString() ?? "";
                var state = root.GetProperty("state").GetString() ?? "";
                var redirectUri = root.GetProperty("redirectUri").GetString() ?? "";

                if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirectUriParsed))
                {
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "Некорректный redirect URI"
                    };
                }

                int port = redirectUriParsed.Port;

                using var listener = new HttpListener();
                var prefix = $"http://localhost:{port}/";
                listener.Prefixes.Add(prefix);
                try
                {
                    listener.Start();
                }
                catch (Exception ex)
                {
                    return new AuthResult { Success = false, ErrorMessage = $"Не удалось запустить локальный сервер: {ex.Message}" };
                }

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
                }
                catch { }

                HttpListenerContext? context;
                var waitTask = Task.Run(() =>
                {
                    try
                    {
                        return listener.GetContext();
                    }
                    catch { return null; }
                });

                var timeout = Task.Delay(TimeSpan.FromMinutes(2));
                var finished = await Task.WhenAny(waitTask, timeout);
                if (finished == timeout)
                {
                    listener.Stop();
                    return new AuthResult { Success = false, ErrorMessage = "Таймаут ожидания авторизации" };
                }

                context = await waitTask;
                if (context == null)
                {
                    listener.Stop();
                    return new AuthResult { Success = false, ErrorMessage = "Ошибка получения ответа от сервера" };
                }

                var query = context.Request.QueryString;

                if (query["error"] != null)
                {
                    listener.Stop();
                    return new AuthResult { Success = false, ErrorMessage = query["error_message"] ?? "Ошибка авторизации" };
                }

                var returnedState = query["state"];
                var code = query["code"];

                if (returnedState != state)
                {
                    listener.Stop();
                    return new AuthResult { Success = false, ErrorMessage = "Некорректный state" };
                }

                var exchangePayload = new
                {
                    code,
                    state = returnedState
                };

                var exchangeJson = JsonSerializer.Serialize(exchangePayload);
                var exchangeContent = new StringContent(exchangeJson, Encoding.UTF8, "application/json");

                var exchangeResponse = await _httpClient.PostAsync($"{apiBaseUrl}/auth/ely/exchange", exchangeContent);

                if (!exchangeResponse.IsSuccessStatusCode)
                {
                    listener.Stop();
                    var errorText = await exchangeResponse.Content.ReadAsStringAsync();
                    return new AuthResult { Success = false, ErrorMessage = $"Ошибка обмена кода: {errorText}" };
                }

                var exchangeResponseJson = await exchangeResponse.Content.ReadAsStringAsync();
                using var exchangeDoc = JsonDocument.Parse(exchangeResponseJson);
                var exchangeRoot = exchangeDoc.RootElement;

                if (!exchangeRoot.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                {
                    listener.Stop();
                    return new AuthResult { Success = false, ErrorMessage = "Ошибка обмена кода" };
                }

                var accessToken = exchangeRoot.GetProperty("accessToken").GetString() ?? "";
                var username = exchangeRoot.GetProperty("username").GetString() ?? "";
                var uuid = exchangeRoot.GetProperty("uuid").GetString() ?? "";

                var profile = new UserProfile
                {
                    Username = username,
                    AccessToken = accessToken,
                    Uuid = uuid,
                    Provider = "Ely.by",
                    LastLogin = DateTime.Now
                };

                SaveProfile(profile);

                var pathHtml = Path.Combine(AppContext.BaseDirectory, "Resources", "auth-success.html");
                var html = File.ReadAllText(pathHtml, Encoding.UTF8)
                    .Replace("{{USERNAME}}", WebUtility.HtmlEncode(username));

                var buffer = Encoding.UTF8.GetBytes(html);

                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();

                listener.Stop();

                return new AuthResult { Success = true, Profile = profile };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = $"Ошибка OAuth: {ex.Message}" };
            }
        }

        public static async Task<AuthResult> ExchangeElyOAuthCodeAsync(string clientId, string clientSecret, string redirectUri, string code)
        {
            try
            {
                var form = new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code
                };

                using var content = new FormUrlEncodedContent(form);
                var resp = await _httpClient.PostAsync("https://account.ely.by/api/oauth2/v1/token", content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    try
                    {
                        using var errDoc = JsonDocument.Parse(body);
                        var root = errDoc.RootElement;
                        if (root.TryGetProperty("error", out var e))
                            return new AuthResult { Success = false, ErrorMessage = e.GetString() ?? "Ошибка обмена кода" };
                    }
                    catch { }

                    return new AuthResult { Success = false, ErrorMessage = $"Ошибка обмена кода: {resp.StatusCode}" };
                }

                using var doc = JsonDocument.Parse(body);
                var rootElem = doc.RootElement;
                var accessToken = rootElem.GetProperty("access_token").GetString() ?? "";
                var refreshToken = rootElem.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : "";
                var expiresIn = rootElem.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 0;

                var request = new HttpRequestMessage(HttpMethod.Get, "https://account.ely.by/api/account/v1/info");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var userResp = await _httpClient.SendAsync(request);
                if (!userResp.IsSuccessStatusCode)
                    return new AuthResult { Success = false, ErrorMessage = "Не удалось получить информацию о пользователе" };

                var userJson = await userResp.Content.ReadAsStringAsync();
                using var userDoc = JsonDocument.Parse(userJson);
                var userRoot = userDoc.RootElement;

                var username = userRoot.GetProperty("username").GetString() ?? "";
                var uuid = userRoot.TryGetProperty("uuid", out var uu) ? uu.GetString() ?? "" : "";

                var profile = new UserProfile
                {
                    Username = username,
                    Uuid = uuid,
                    Provider = "Ely.by",
                    AccessToken = accessToken,
                    LastLogin = DateTime.Now
                };

                return new AuthResult { Success = true, Profile = profile };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = "Ошибка OAuth обмена Ely.by: " + ex.Message };
            }
        }
    }
}