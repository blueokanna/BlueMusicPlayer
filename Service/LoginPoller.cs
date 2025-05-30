using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http.Headers;
using System.IO;
using Windows.Storage;

namespace BlueMusicPlayer.Services
{
    public class LoginService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        // Configuration constants
        private const string AppId = "a301010000000000aadb4e5a28b45a67";
        private const string AccessTokenConst = "y8f3b107ed962c79ade975991c3cde622c77459eb28d2b14af";
        private const string AppSecret = "de6882f913d59560c9f37345f4cb0053";
        private const string SignType = "RSA_SHA256";
        private const string BaseUrl = "http://openapi.music.163.com";
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        private static readonly string DeviceJson = JsonSerializer.Serialize(new
        {
            deviceType = "andrwear",
            os = "otos",
            appVer = "0.1",
            channel = "hm",
            model = "kys",
            deviceId = "357",
            brand = "hm",
            osVer = "8.1.0"
        });

        public LoginService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BlueMusicPlayer/1.0");

            _cts = new CancellationTokenSource();
            _disposed = false;
        }

        public async Task<(string qrCodeUrl, string uniKey)> GetQrCodeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < MaxRetries)
            {
                try
                {
                    var timestamp = GetCurrentTimestamp();
                    var bizContent = JsonSerializer.Serialize(new { type = 2, expiredKey = "300" });

                    var queryString = BuildQueryString(bizContent, timestamp, includeAccessToken: false);
                    var url = $"/openapi/music/basic/user/oauth2/qrcodekey/get/v2?{queryString}";

                    System.Diagnostics.Debug.WriteLine($"QR Code request URL: {url}");

                    using var response = await _httpClient.PostAsync(url, null, linkedCts.Token);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine($"QR Code response: {responseContent}");

                    response.EnsureSuccessStatusCode();

                    var result = await ParseQrCodeResponse(responseContent);

                    return result;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is JsonException)
                {
                    lastException = ex;
                    attempt++;

                    System.Diagnostics.Debug.WriteLine($"QR Code request attempt {attempt} failed: {ex.Message}");

                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelayMs * attempt, linkedCts.Token);
                    }
                }
            }

            throw new LoginServiceException("Failed to get QR code after multiple attempts", lastException);
        }

        public async Task<LoginResult> PollForTokenAsync(string uniKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(uniKey))
                throw new ArgumentException("uniKey cannot be null or empty", nameof(uniKey));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            const int maxAttempts = 150; // 5 minutes with 2-second intervals
            int attempts = 0;

            System.Diagnostics.Debug.WriteLine($"Starting polling for uniKey: {uniKey}");

            while (attempts < maxAttempts)
            {
                try
                {
                    var timestamp = GetCurrentTimestamp();
                    var bizContent = JsonSerializer.Serialize(new { key = uniKey, clientId = AppId });
                    var queryString = BuildQueryString(bizContent, timestamp, includeAccessToken: true);
                    var url = $"/openapi/music/basic/oauth2/device/login/qrcode/get?{queryString}";

                    using var response = await _httpClient.PostAsync(url, null, linkedCts.Token);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine($"Poll attempt {attempts + 1}, response: {responseContent[..Math.Min(200, responseContent.Length)]}...");

                    response.EnsureSuccessStatusCode();

                    var (status, loginResult) = await ParsePollResponse(responseContent);

                    switch (status)
                    {
                        case 800:
                            throw new QrCodeExpiredException("QR code has expired");
                        case 801:
                            System.Diagnostics.Debug.WriteLine("Waiting for QR code scan...");
                            break;
                        case 802:
                            System.Diagnostics.Debug.WriteLine("QR code scanned, waiting for authorization...");
                            break;
                        case 803:
                            System.Diagnostics.Debug.WriteLine("Login successful!");
                            // Save the token to storage
                            await SaveTokenAsync(loginResult!);
                            return loginResult!;
                        case 804:
                            throw new LoginServiceException("Unknown error during login");
                        default:
                            throw new LoginServiceException($"Unexpected status code: {status}");
                    }

                    attempts++;
                    if (attempts < maxAttempts)
                    {
                        await Task.Delay(2000, linkedCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("Login polling was cancelled");
                    throw;
                }
                catch (Exception ex) when (ex is not LoginServiceException)
                {
                    attempts++;
                    System.Diagnostics.Debug.WriteLine($"Poll attempt {attempts} failed: {ex.Message}");

                    if (attempts >= maxAttempts)
                    {
                        throw new LoginServiceException("Login polling failed after maximum attempts", ex);
                    }
                    await Task.Delay(2000, linkedCts.Token);
                }
            }

            throw new TimeoutException("Login polling timed out");
        }

        private async Task SaveTokenAsync(LoginResult loginResult)
        {
            try
            {
                var tokenPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "auth_tokens.json");
                var tokenData = new
                {
                    AccessToken = loginResult.AccessToken,
                    RefreshToken = loginResult.RefreshToken,
                    ExpiresIn = loginResult.ExpireTime,
                    SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tokenPath, json);

                // 计算绝对过期时间
                var expiryDateTime = DateTimeOffset.UtcNow.AddSeconds(loginResult.ExpireTime);
                System.Diagnostics.Debug.WriteLine($"Token saved to: {tokenPath}");
                System.Diagnostics.Debug.WriteLine($"Expires in (seconds): {loginResult.ExpireTime}, so at: {expiryDateTime}");
                System.Diagnostics.Debug.WriteLine($"Token content: {json}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save token: {ex.Message}");
            }
        }

        private string BuildQueryString(string bizContent, long timestamp, bool includeAccessToken)
        {
            var query = $"bizContent={Uri.EscapeDataString(bizContent)}&" +
                       $"appId={AppId}&" +
                       $"signType={SignType}&" +
                       (includeAccessToken ? $"accessToken={AccessTokenConst}&" : "") +
                       $"appSecret={AppSecret}&" +
                       $"device={Uri.EscapeDataString(DeviceJson)}&" +
                       $"timestamp={timestamp}";

            return query;
        }

        private async Task<(string qrCodeUrl, string uniKey)> ParseQrCodeResponse(string responseContent)
        {
            using var doc = await JsonDocument.ParseAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseContent)));
            var root = doc.RootElement;

            var code = root.GetProperty("code").GetInt32();
            if (code != 200)
            {
                var message = root.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString()
                    : "Unknown error";
                throw new LoginServiceException($"API returned error code {code}: {message}");
            }

            var data = root.GetProperty("data");
            var qrUrl = data.GetProperty("qrCodeUrl").GetString()
                ?? throw new LoginServiceException("qrCodeUrl is missing in response");
            var uniKey = data.GetProperty("uniKey").GetString()
                ?? throw new LoginServiceException("uniKey is missing in response");

            System.Diagnostics.Debug.WriteLine($"Generated QR Code URL: {qrUrl}");
            System.Diagnostics.Debug.WriteLine($"UniKey: {uniKey}");

            return (qrUrl, uniKey);
        }

        private async Task<(int status, LoginResult? result)> ParsePollResponse(string responseContent)
        {
            using var doc = await JsonDocument.ParseAsync(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseContent))
            );
            var root = doc.RootElement;
            var code = root.GetProperty("code").GetInt32();
            if (code != 200) throw new LoginServiceException($"API error code {code}");

            var data = root.GetProperty("data");
            var status = data.GetProperty("status").GetInt32();
            if (status != 803) return (status, null);

            var token = data.GetProperty("accessToken");
            var accessToken = token.GetProperty("accessToken").GetString()!;
            var refreshToken = token.GetProperty("refreshToken").GetString()!;

            // 解析 expireTime 仍然保持之前的逻辑，只是拿到的值当作“秒”
            long expiresIn;
            var prop = token.GetProperty("expireTime");
            if (prop.ValueKind == JsonValueKind.String)
            {
                expiresIn = long.Parse(prop.GetString()!);
            }
            else
            {
                expiresIn = prop.GetInt64();
            }
            // 毫秒数也会被除以 1000（保留逻辑不变）
            if (expiresIn > 9999999999) expiresIn /= 1000;

            var absoluteExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            System.Diagnostics.Debug.WriteLine(
                $"Parsed expireIn: {expiresIn} sec → expires at: {absoluteExpiry}"
            );

            return (status, new LoginResult(accessToken, refreshToken, expiresIn));
        }

        // Helper method to get current timestamp in milliseconds
        private static long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LoginService));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cts.Cancel();
                _cts.Dispose();
                _httpClient.Dispose();
            }
        }
    }

    public record LoginResult(string AccessToken, string RefreshToken, long ExpireTime);

    public class LoginServiceException : Exception
    {
        public LoginServiceException(string message) : base(message) { }
        public LoginServiceException(string message, Exception inner) : base(message, inner) { }
    }

    public class QrCodeExpiredException : LoginServiceException
    {
        public QrCodeExpiredException(string message) : base(message) { }
    }
}