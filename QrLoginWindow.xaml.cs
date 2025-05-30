using BlueMusicPlayer.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace BlueMusicPlayer.Views
{
    public sealed partial class QrLoginWindow : Window
    {
        private readonly string _uniKey;
        private readonly LoginService _loginService;
        private readonly DispatcherQueue _dispatcher;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _hasStarted = false;
        private bool _isDisposed = false;

        public event Action<string, string, long>? TokenReceived;

        public QrLoginWindow(string qrData, string uniKey)
        {
            InitializeComponent();

            // Set window properties
            this.Title = "网易云音乐登录";
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            // Set window size
            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(450, 650));
            }

            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _uniKey = uniKey;
            _loginService = new LoginService();
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize UI - show loading state
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            StatusText.Text = "正在生成二维码...";
            QrImage.Visibility = Visibility.Collapsed;

            // Generate QR code
            _ = GenerateQrCodeAsync(qrData,uniKey);

            // Event handlers
            this.Activated += QrLoginWindow_Activated;
            this.Closed += QrLoginWindow_Closed;
        }

        private async Task GenerateQrCodeAsync(string qrData, string uniKey)
        {
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        // Generate QR code using QRCoder
                        var qrGenerator = new QRCodeGenerator();
                        var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
                        var qrCode = new PngByteQRCode(qrCodeData);
                        var qrCodeBytes = qrCode.GetGraphic(20); // 20 pixels per module

                        // Convert to BitmapImage on UI thread
                        _dispatcher.TryEnqueue(async () =>
                        {
                            if (_isDisposed) return;

                            try
                            {
                                using var stream = new InMemoryRandomAccessStream();
                                using var writer = new DataWriter(stream.GetOutputStreamAt(0));
                                writer.WriteBytes(qrCodeBytes);
                                await writer.StoreAsync();

                                var bitmap = new BitmapImage();
                                bitmap.SetSource(stream);
                                QrImage.Source = bitmap;

                                // Show QR code and hide loading
                                QrImage.Visibility = Visibility.Visible;
                                LoadingRing.IsActive = false;
                                LoadingRing.Visibility = Visibility.Collapsed;
                                StatusText.Text = "请使用手机扫描二维码";

                                System.Diagnostics.Debug.WriteLine($"QR Code generated successfully");
                                System.Diagnostics.Debug.WriteLine($"QR Data: {qrData}");
                                System.Diagnostics.Debug.WriteLine($"UniKey: {uniKey}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set QR image: {ex.Message}");
                                ShowQrError($"无法显示二维码: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to generate QR code: {ex.Message}");
                        _dispatcher.TryEnqueue(() =>
                        {
                            if (!_isDisposed)
                            {
                                ShowQrError($"无法生成二维码: {ex.Message}");
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QR generation task failed: {ex.Message}");
                ShowQrError($"二维码生成失败: {ex.Message}");
            }
        }

        private void ShowQrError(string message)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            QrImage.Visibility = Visibility.Collapsed;
            StatusText.Text = message;
        }

        private async void QrLoginWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (!_hasStarted && e.WindowActivationState != WindowActivationState.Deactivated)
            {
                _hasStarted = true;
                // Wait for QR code to be generated first
                await Task.Delay(1000);
                _ = StartLoginFlowAsync();
            }
        }

        private void QrLoginWindow_Closed(object sender, WindowEventArgs e)
        {
            _isDisposed = true;
            _cancellationTokenSource?.Cancel();
            _loginService?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        private async Task StartLoginFlowAsync()
        {
            try
            {
                if (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // Only update status if QR is successfully shown
                _dispatcher.TryEnqueue(() =>
                {
                    if (!_isDisposed && QrImage.Visibility == Visibility.Visible)
                    {
                        StatusText.Text = "等待扫码确认...";
                    }
                });

                var (accessToken, refreshToken, expires) = await _loginService.PollForTokenAsync(_uniKey);

                if (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // Update UI for success
                _dispatcher.TryEnqueue(() =>
                {
                    if (!_isDisposed)
                    {
                        StatusText.Text = "登录成功！";
                        TokenReceived?.Invoke(accessToken, refreshToken, expires);
                    }
                });

                await ShowResultAsync("登录成功", "已成功登录到网易云音乐！");
            }
            catch (TimeoutException)
            {
                if (!_isDisposed)
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        if (!_isDisposed)
                        {
                            StatusText.Text = "二维码已过期，请重新登录";
                        }
                    });
                    await ShowResultAsync("登录超时", "二维码已过期，请重新获取");
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Login operation was cancelled");
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        if (!_isDisposed)
                        {
                            StatusText.Text = "登录失败";
                        }
                    });
                    await ShowResultAsync("登录失败", $"登录过程中发生错误: {ex.Message}");
                }
            }
            finally
            {
                // Close window after showing result
                await Task.Delay(2000);
                if (!_isDisposed)
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            if (!_isDisposed)
                            {
                                this.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error closing window: {ex.Message}");
                        }
                    });
                }
            }
        }

        private async Task ShowResultAsync(string title, string message)
        {
            if (_isDisposed) return;

            try
            {
                if (_dispatcher.HasThreadAccess)
                {
                    await ShowDialogOnUIThread(title, message);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _dispatcher.TryEnqueue(async () =>
                    {
                        try
                        {
                            if (!_isDisposed)
                            {
                                await ShowDialogOnUIThread(title, message);
                            }
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    });

                    await tcs.Task.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show result dialog: {ex.Message}");
            }
        }

        private async Task ShowDialogOnUIThread(string title, string message)
        {
            if (_isDisposed) return;

            try
            {
                var xamlRoot = this.Content?.XamlRoot;

                if (xamlRoot == null)
                {
                    for (int i = 0; i < 50 && xamlRoot == null && !_isDisposed; i++)
                    {
                        await Task.Delay(100);
                        xamlRoot = this.Content?.XamlRoot;
                    }
                }

                if (xamlRoot != null && !_isDisposed)
                {
                    var dialog = new ContentDialog
                    {
                        Title = title,
                        Content = new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 300
                        },
                        CloseButtonText = "确定",
                        XamlRoot = xamlRoot
                    };

                    await dialog.ShowAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Could not show dialog - XamlRoot unavailable: {title} - {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show dialog: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cancel: {ex.Message}");
            }
        }
    }
}