using BlueMusicPlayer.Models;
using BlueMusicPlayer.Models.NetEase;
using BlueMusicPlayer.Services;
using BlueMusicPlayer.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BlueMusicPlayer.ViewModels
{
    public partial class MusicPlayerViewModel : ObservableObject, IDisposable
    {
        private const string ConfigFileName = "config.json";
        private const string AuthTokensFileName = "auth_tokens.json";
        private readonly MusicRepository _repo;
        private readonly DispatcherQueue _dispatcher;
        private readonly IntPtr _hwnd;
        private readonly HttpClient _httpClient;
        private readonly LoginService _loginService;
        private readonly NetEaseMusicService _netEaseService;
        private readonly XamlRoot _xamlRoot;
        private bool _disposed;

        public MediaPlayer MediaPlayer { get; private set; }
        public ObservableCollection<Track> Tracks { get; private set; }
        public ObservableCollection<Track> FilteredTracks { get; private set; }
        public ObservableCollection<Track> SelectedTracks { get; } = new();

        // NetEase Music properties
        public ObservableCollection<NetEaseSong> NetEaseSongs { get; } = new();
        public ObservableCollection<NetEaseSong> FilteredNetEaseSongs { get; private set; } = new();

        [ObservableProperty]
        private bool togglePaneState;
        [ObservableProperty]
        private bool isPlaying;
        [ObservableProperty]
        private bool isLoopEnabled;
        [ObservableProperty]
        private bool isShuffleEnabled;
        [ObservableProperty]
        private bool isEditing;
        [ObservableProperty]
        private bool isLoggingIn;
        [ObservableProperty]
        private bool isLoading;
        [ObservableProperty]
        private string loadingMessage = string.Empty;
        [ObservableProperty]
        private string searchQuery = string.Empty;
        [ObservableProperty]
        private string isLoggedIn = "登录";
        [ObservableProperty]
        private string selectedNavItem = "local";
        [ObservableProperty]
        private bool isNetEaseLoading;

        // Count properties
        public int LocalMusicCount => Tracks?.Count ?? 0;
        public string LoginButtonText => IsLoggedIn ?? "登录";
        public int PlaylistCount => 0;
        public int NetEaseSongCount => NetEaseSongs?.Count ?? 0;

        // Login status property
        public bool IsNetEaseLoggedIn => IsLoggedIn == "已登录";

        // Properties to replace MultiBinding - these handle the complex visibility logic
        public bool ShowNoSongsMessage => IsNetEaseLoggedIn && FilteredNetEaseSongs.Count == 0 && !IsNetEaseLoading;
        public bool ShowLoginRequiredMessage => !IsNetEaseLoggedIn && !IsNetEaseLoading;
        public bool ShowHelpTextForLoggedIn => IsNetEaseLoggedIn;
        public bool ShowHelpTextForNotLoggedIn => !IsNetEaseLoggedIn;

        public bool ShowSongsList => !IsNetEaseLoading &&
                                     FilteredNetEaseSongs != null &&
                                     FilteredNetEaseSongs.Count > 0;

        public bool ShowEmptyState => !IsNetEaseLoading &&
                              (FilteredNetEaseSongs == null || FilteredNetEaseSongs.Count == 0) &&
                              !IsNetEaseLoggedIn;

        // Now Playing properties for the player bar
        public string NowPlayingTitle => LocalSelectedTrack?.Title ?? "未选择音乐";
        public string NowPlayingArtist => LocalSelectedTrack?.Artist ?? "未知艺术家";
        public string AlbumArtUrl => LocalSelectedTrack?.AlbumArtUrl ?? string.Empty;

        public string NowPlayingText => LocalSelectedTrack == null
            ? "未选择音乐"
            : $"{LocalSelectedTrack.Title} - {LocalSelectedTrack.Artist}";

        private Track? _localSelectedTrack;
        public Track? LocalSelectedTrack
        {
            get => _localSelectedTrack;
            set
            {
                if (SetProperty(ref _localSelectedTrack, value))
                {
                    OnPropertyChanged(nameof(NowPlayingText));
                    OnPropertyChanged(nameof(NowPlayingTitle));
                    OnPropertyChanged(nameof(NowPlayingArtist));
                    OnPropertyChanged(nameof(AlbumArtUrl));
                    PlayPauseCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public bool CanDeleteMultiple => SelectedTracks?.Any() ?? false;

        // Commands
        public IRelayCommand TogglePlaylistCommand { get; private set; }
        public IRelayCommand PreviousCommand { get; private set; }
        public IRelayCommand NextCommand { get; private set; }
        public IAsyncRelayCommand PlayPauseCommand { get; private set; }
        public IAsyncRelayCommand BatchAddCommand { get; private set; }
        public IRelayCommand ToggleLoopCommand { get; private set; }
        public IRelayCommand ToggleEditModeCommand { get; private set; }
        public IAsyncRelayCommand LoginCommand { get; private set; }
        public IRelayCommand<Track> DeleteTrackCommand { get; private set; }
        public IRelayCommand<Track> PlayTrackCommand { get; private set; }
        public IAsyncRelayCommand RefreshNetEaseCommand { get; private set; }
        public IRelayCommand<string> NavigateCommand { get; private set; }

        public MusicPlayerViewModel(
            MusicRepository repo,
            MediaPlayerElement playerElement,
            IntPtr hwnd,
            XamlRoot xamlRoot)
        {
            try
            {
                _repo = repo ?? throw new ArgumentNullException(nameof(repo));
                _dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("Cannot get dispatcher queue");
                _hwnd = hwnd;
                _httpClient = new HttpClient();
                _loginService = new LoginService();
                _netEaseService = new NetEaseMusicService();
                _xamlRoot = xamlRoot;

                // Initialize MediaPlayer safely
                if (playerElement?.MediaPlayer != null)
                {
                    MediaPlayer = playerElement.MediaPlayer;
                }
                else
                {
                    throw new ArgumentException("MediaPlayer element or its MediaPlayer is null", nameof(playerElement));
                }

                // Initialize collections safely
                Tracks = _repo.LoadAll() ?? new ObservableCollection<Track>();
                FilteredTracks = new ObservableCollection<Track>(Tracks);
                FilteredNetEaseSongs = new ObservableCollection<NetEaseSong>();

                InitializeCommands();
                SetupEventHandlers();
                LoadConfig();

                // Load auth tokens and initialize NetEase service
                _ = Task.Run(async () => await LoadAuthTokensAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing MusicPlayerViewModel: {ex.Message}");
                throw;
            }
        }

        private void InitializeCommands()
        {
            TogglePlaylistCommand = new RelayCommand(() => TogglePaneState = !TogglePaneState);
            PreviousCommand = new RelayCommand(Previous);
            NextCommand = new RelayCommand(Next);
            PlayPauseCommand = new AsyncRelayCommand(PlayOrPauseAsync, () => LocalSelectedTrack != null);
            BatchAddCommand = new AsyncRelayCommand(BatchAddAsync);
            DeleteTrackCommand = new RelayCommand<Track>(
                track => DeleteTrack(track),
                track => track != null
            );
            ToggleLoopCommand = new RelayCommand(() => IsLoopEnabled = !IsLoopEnabled);
            ToggleEditModeCommand = new RelayCommand(() =>
            {
                IsEditing = !IsEditing;
                if (!IsEditing) SelectedTracks.Clear();
            });
            LoginCommand = new AsyncRelayCommand(OnLoginAsync, () => !IsLoggingIn);
            PlayTrackCommand = new RelayCommand<Track>(async t => await SelectAndPlayAsync(t));
            RefreshNetEaseCommand = new AsyncRelayCommand(LoadNetEaseRecommendationsAsync);
            NavigateCommand = new RelayCommand<string>(OnNavigate);
        }

        private void SetupEventHandlers()
        {
            // Subscribe to collection changes to update counts
            if (Tracks != null)
            {
                Tracks.CollectionChanged += (_, __) =>
                {
                    OnPropertyChanged(nameof(LocalMusicCount));
                };

                foreach (var t in Tracks)
                {
                    if (t != null)
                        t.PropertyChanged += Track_PropertyChanged;
                }
            }

            if (NetEaseSongs != null)
            {
                NetEaseSongs.CollectionChanged += (_, __) =>
                {
                    OnPropertyChanged(nameof(NetEaseSongCount));
                    UpdateNetEaseVisibilityProperties();
                    FilterNetEaseSongs();
                };
            }

            if (MediaPlayer?.PlaybackSession != null)
            {
                MediaPlayer.PlaybackSession.PlaybackStateChanged += (_, __) =>
                    _dispatcher?.TryEnqueue(() =>
                        IsPlaying = MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing);

                MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            }
        }

        // Method to update all visibility-related properties
        private void UpdateNetEaseVisibilityProperties()
        {
            OnPropertyChanged(nameof(ShowNoSongsMessage));
            OnPropertyChanged(nameof(ShowLoginRequiredMessage));
            OnPropertyChanged(nameof(ShowHelpTextForLoggedIn));
            OnPropertyChanged(nameof(ShowHelpTextForNotLoggedIn));
            OnPropertyChanged(nameof(ShowSongsList));
            OnPropertyChanged(nameof(ShowEmptyState));
        }

        partial void OnSearchQueryChanged(string value)
        {
            try
            {
                if (SelectedNavItem == "local")
                {
                    FilterTracks();
                }
                else if (SelectedNavItem == "netease")
                {
                    FilterNetEaseSongs();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnSearchQueryChanged: {ex.Message}");
            }
        }

        partial void OnIsLoggedInChanged(string value)
        {
            OnPropertyChanged(nameof(LoginButtonText));
            OnPropertyChanged(nameof(IsNetEaseLoggedIn));
            UpdateNetEaseVisibilityProperties(); // Update all visibility properties when login status changes
        }

        partial void OnIsNetEaseLoadingChanged(bool value)
        {
            UpdateNetEaseVisibilityProperties(); // Update visibility when loading state changes
        }

        partial void OnSelectedNavItemChanged(string value)
        {
            try
            {
                if (value == "netease" && IsNetEaseLoggedIn && (NetEaseSongs?.Count ?? 0) == 0)
                {
                    _ = Task.Run(async () => await LoadNetEaseRecommendationsAsync());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnSelectedNavItemChanged: {ex.Message}");
            }
        }

        private void OnNavigate(string? tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                SelectedNavItem = tag;
            }
        }

        private async Task LoadNetEaseRecommendationsAsync()
        {
            if (_disposed) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("开始加载网易云推荐...");

                // 先在 UI 线程上显示 loading 状态
                _dispatcher.TryEnqueue(() =>
                {
                    IsNetEaseLoading = true;
                    SetLoading(true, "正在加载网易云音乐推荐...");
                });

                // 检查登录状态
                var isLoggedIn = await CheckLoginStatusAsync();
                System.Diagnostics.Debug.WriteLine($"登录状态检查: {isLoggedIn}");

                if (!isLoggedIn)
                {
                    System.Diagnostics.Debug.WriteLine("用户未登录，无法加载推荐");
                    _dispatcher.TryEnqueue(async () =>
                    {
                        IsNetEaseLoading = false;
                        SetLoading(false);
                        await ShowErrorDialogAsync("提示", "请先登录网易云音乐账号");
                    });
                    return;
                }

                System.Diagnostics.Debug.WriteLine("开始获取推荐歌曲...");
                var songs = await _netEaseService.GetDailyRecommendationsAsync(35);
                System.Diagnostics.Debug.WriteLine($"API返回歌曲数量: {songs?.Count ?? 0}");

                // 在 UI 线程上更新列表和状态
                _dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("开始更新UI中的歌曲列表...");
                        NetEaseSongs.Clear();

                        if (songs != null && songs.Count > 0)
                        {
                            foreach (var song in songs)
                                NetEaseSongs.Add(song);

                            System.Diagnostics.Debug.WriteLine($"成功添加 {songs.Count} 首歌曲到UI");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("没有获取到推荐歌曲");
                        }

                        FilterNetEaseSongs();
                        UpdateNetEaseVisibilityProperties();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"更新UI失败: {ex.Message}");
                    }
                    finally
                    {
                        IsNetEaseLoading = false;
                        SetLoading(false);
                    }
                });

                System.Diagnostics.Debug.WriteLine($"推荐加载完成，总共 {songs?.Count ?? 0} 首歌曲");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载网易云推荐失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常详情: {ex}");

                _dispatcher.TryEnqueue(async () =>
                {
                    IsNetEaseLoading = false;
                    SetLoading(false);
                    await ShowErrorDialogAsync("加载失败", $"无法加载网易云推荐: {ex.Message}");
                });
            }
        }

        private async Task<bool> CheckLoginStatusAsync()
        {
            try
            {
                var tokenPath = GetTokenFilePath();
                System.Diagnostics.Debug.WriteLine($"检查Token文件路径: {tokenPath}");

                if (!File.Exists(tokenPath))
                {
                    System.Diagnostics.Debug.WriteLine("Token文件不存在");
                    return false;
                }

                var tokenJson = await File.ReadAllTextAsync(tokenPath);
                System.Diagnostics.Debug.WriteLine($"读取Token内容: {tokenJson[..Math.Min(100, tokenJson.Length)]}...");

                var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

                if (tokenData.TryGetProperty("ExpiresAt", out var expiresProperty) &&
                    tokenData.TryGetProperty("AccessToken", out var tokenProperty))
                {
                    var expiresAt = expiresProperty.GetInt64();
                    var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var accessToken = tokenProperty.GetString();

                    System.Diagnostics.Debug.WriteLine($"Token过期时间: {DateTimeOffset.FromUnixTimeSeconds(currentTime + expiresAt /3600)}");
                    System.Diagnostics.Debug.WriteLine($"当前时间: {DateTimeOffset.UtcNow}");
                    System.Diagnostics.Debug.WriteLine($"Token是否有效: {currentTime + expiresAt/3600 > currentTime && !string.IsNullOrEmpty(accessToken)}");

                    if (currentTime + expiresAt / 3600 > currentTime && !string.IsNullOrEmpty(accessToken))
                    {
                        // Initialize NetEase service with the token
                        await _netEaseService.InitializeWithTokenAsync(accessToken);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查登录状态失败: {ex.Message}");
                return false;
            }
        }

        private void FilterTracks()
        {
            try
            {
                if (FilteredTracks == null || Tracks == null) return;

                FilteredTracks.Clear();

                var tracksToAdd = string.IsNullOrWhiteSpace(SearchQuery)
                    ? Tracks
                    : Tracks.Where(t => t != null && (
                        (t.Title?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (t.Artist?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (t.AlbumArtUrl?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)));

                foreach (var track in tracksToAdd)
                {
                    if (track != null)
                        FilteredTracks.Add(track);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtering tracks: {ex.Message}");
            }
        }

        private void FilterNetEaseSongs()
        {
            try
            {
                if (FilteredNetEaseSongs == null || NetEaseSongs == null) return;

                FilteredNetEaseSongs.Clear();

                var songsToAdd = string.IsNullOrWhiteSpace(SearchQuery)
                    ? NetEaseSongs
                    : NetEaseSongs.Where(s => s != null && (
                        (s.Name?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (s.ArtistsText?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (s.Album?.Name?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)));

                foreach (var song in songsToAdd)
                {
                    if (song != null)
                        FilteredNetEaseSongs.Add(song);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtering NetEase songs: {ex.Message}");
            }
        }

        private void Track_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(Track.IsSelected) && sender is Track t && SelectedTracks != null)
                {
                    if (t.IsSelected && !SelectedTracks.Contains(t))
                        SelectedTracks.Add(t);
                    else if (!t.IsSelected && SelectedTracks.Contains(t))
                        SelectedTracks.Remove(t);

                    OnPropertyChanged(nameof(CanDeleteMultiple));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Track property changed error: {ex.Message}");
            }
        }


        public async Task SelectAndPlayAsync(Track track)
        {
            if (_disposed || track == null) return;

            try
            {
                SetLoading(true, "正在加载音频文件...");

                LocalSelectedTrack = track;
                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                if (MediaPlayer != null)
                {
                    MediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
                    MediaPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("播放失败", $"无法播放文件: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task PlayOrPauseAsync()
        {
            if (_disposed || MediaPlayer == null) return;

            try
            {
                if (IsPlaying)
                {
                    MediaPlayer.Pause();
                }
                else
                {
                    var session = MediaPlayer.PlaybackSession;
                    if (MediaPlayer.Source != null
                        && session != null
                        && session.Position > TimeSpan.Zero
                        && session.Position < session.NaturalDuration)
                    {
                        MediaPlayer.Play();
                    }
                    else if (LocalSelectedTrack != null)
                    {
                        await SelectAndPlayAsync(LocalSelectedTrack);
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("播放错误", ex.Message);
            }
        }

        private void Previous()
        {
            try
            {
                if (Tracks == null || !Tracks.Any()) return;

                var trackList = IsShuffleEnabled ? Tracks.OrderBy(x => Guid.NewGuid()).ToList() : Tracks.ToList();
                int idx = LocalSelectedTrack != null ? trackList.IndexOf(LocalSelectedTrack) : 0;
                idx = (idx - 1 + trackList.Count) % trackList.Count;
                _ = SelectAndPlayAsync(trackList[idx]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Previous: {ex.Message}");
            }
        }

        private void Next()
        {
            try
            {
                if (Tracks == null || !Tracks.Any()) return;

                var trackList = IsShuffleEnabled ? Tracks.OrderBy(x => Guid.NewGuid()).ToList() : Tracks.ToList();
                int idx = LocalSelectedTrack != null ? trackList.IndexOf(LocalSelectedTrack) + 1 : 0;
                if (idx >= trackList.Count)
                {
                    if (IsLoopEnabled) idx = 0;
                    else return;
                }
                _ = SelectAndPlayAsync(trackList[idx]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Next: {ex.Message}");
            }
        }

        private async Task BatchAddAsync()
        {
            if (_disposed) return;

            try
            {
                SetLoading(true, "正在选择音频文件...");

                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, _hwnd);
                picker.ViewMode = PickerViewMode.Thumbnail;
                foreach (var ext in new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus" })
                    picker.FileTypeFilter.Add(ext);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Any())
                {
                    SetLoading(true, $"正在添加 {files.Count} 个文件...");

                    foreach (var f in files)
                    {
                        var t = new Track { FilePath = f.Path, Title = f.DisplayName, AlbumArtUrl = string.Empty };
                        t.PropertyChanged += Track_PropertyChanged;
                        _repo?.Add(t);
                        Tracks?.Add(t);
                    }

                    FilterTracks(); // Refresh filtered tracks
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("添加文件失败", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void DeleteTrack(Track track)
        {
            if (track == null || _disposed) return;

            try
            {
                SetLoading(true, "正在删除歌曲…");

                bool isCurrent = track == LocalSelectedTrack;
                int oldIndex = isCurrent && Tracks != null
                    ? Tracks.IndexOf(track)
                    : -1;

                _repo?.Delete(track);
                Tracks?.Remove(track);
                FilteredTracks?.Remove(track);

                if (isCurrent)
                {
                    if (Tracks?.Any() == true)
                    {
                        int nextIndex = oldIndex;
                        if (nextIndex >= Tracks.Count)
                            nextIndex = Tracks.Count - 1;

                        _ = SelectAndPlayAsync(Tracks[nextIndex]);
                    }
                    else
                    {
                        MediaPlayer?.Pause();
                        LocalSelectedTrack = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _ = ShowErrorDialogAsync("删除失败", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool isLoading, string message = "")
        {
            IsLoading = isLoading;
            LoadingMessage = message ?? string.Empty;
        }

        private class Config { public bool Loop { get; set; } }

        private void LoadConfig()
        {
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, ConfigFileName);
                if (!File.Exists(path)) return;

                var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(path));
                if (cfg is not null) IsLoopEnabled = cfg.Loop;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
            }
        }

        private string GetTokenFilePath()
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, AuthTokensFileName);
        }

        private async Task LoadAuthTokensAsync()
        {
            try
            {
                var tokenPath = GetTokenFilePath();
                System.Diagnostics.Debug.WriteLine($"加载Token文件路径: {tokenPath}");

                if (File.Exists(tokenPath))
                {
                    var tokenJson = await File.ReadAllTextAsync(tokenPath);
                    System.Diagnostics.Debug.WriteLine($"Token文件内容长度: {tokenJson.Length}");

                    var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

                    if (tokenData.TryGetProperty("ExpiresAt", out var expiresProperty) &&
                        tokenData.TryGetProperty("AccessToken", out var tokenProperty))
                    {
                        var expiresAt = expiresProperty.GetInt64();
                        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var accessToken = tokenProperty.GetString();

                        System.Diagnostics.Debug.WriteLine($"Token过期时间(小时): {expiresAt/3600}");
                        System.Diagnostics.Debug.WriteLine($"当前时间: {DateTimeOffset.UtcNow}");

                        if (expiresAt > currentTime && !string.IsNullOrEmpty(accessToken))
                        {
                            System.Diagnostics.Debug.WriteLine("发现有效的登录令牌，设置登录状态");

                            // Initialize NetEase service with token
                            await _netEaseService.InitializeWithTokenAsync(accessToken);

                            // Update UI on main thread
                            _dispatcher.TryEnqueue(() =>
                            {
                                IsLoggedIn = "已登录";
                                System.Diagnostics.Debug.WriteLine("UI登录状态已更新为: 已登录");
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("登录令牌已过期或无效");
                            // Clean up expired token
                            try
                            {
                                File.Delete(tokenPath);
                                System.Diagnostics.Debug.WriteLine("已删除过期的Token文件");
                            }
                            catch (Exception deleteEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"删除过期Token文件失败: {deleteEx.Message}");
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("未找到登录令牌文件");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load auth tokens: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常详情: {ex}");
            }
        }

        public void SaveConfig()
        {
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, ConfigFileName);
                File.WriteAllText(path, JsonSerializer.Serialize(new Config { Loop = IsLoopEnabled }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            if (!_disposed && _dispatcher != null)
            {
                _dispatcher.TryEnqueue(() => Next());
            }
        }


        private async Task OnLoginAsync()
        {
            if (IsLoggingIn || _disposed) return;

            IsLoggingIn = true;
            SetLoading(true, "正在获取登录二维码...");
            LoginCommand?.NotifyCanExecuteChanged();

            try
            {
                System.Diagnostics.Debug.WriteLine("开始登录流程...");
                var (qrUrl, uniKey) = await _loginService.GetQrCodeAsync();
                System.Diagnostics.Debug.WriteLine($"获取二维码成功: {qrUrl}");

                SetLoading(false);

                // Ensure we're on the UI thread when creating the window
                if (_dispatcher != null)
                {
                    if (!_dispatcher.HasThreadAccess)
                    {
                        _dispatcher.TryEnqueue(() => CreateAndShowLoginWindow(qrUrl, uniKey));
                    }
                    else
                    {
                        CreateAndShowLoginWindow(qrUrl, uniKey);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取二维码失败: {ex.Message}");
                await ShowErrorDialogAsync("登录失败", $"无法获取二维码: {ex.Message}");
                IsLoggingIn = false;
                SetLoading(false);
                LoginCommand?.NotifyCanExecuteChanged();
            }
        }

        private void CreateAndShowLoginWindow(string qrUrl, string uniKey)
        {
            try
            {
                var window = new QrLoginWindow(qrUrl, uniKey);

                // Subscribe to events
                window.TokenReceived += OnTokenReceived;
                window.Closed += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine("登录窗口已关闭");
                    _dispatcher?.TryEnqueue(() =>
                    {
                        IsLoggingIn = false;
                        SetLoading(false);
                        LoginCommand?.NotifyCanExecuteChanged();
                    });
                };

                // Activate the window to show it
                window.Activate();
                System.Diagnostics.Debug.WriteLine("登录窗口已创建并激活");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建登录窗口失败: {ex.Message}");
                _ = ShowErrorDialogAsync("错误", $"无法打开登录窗口: {ex.Message}");
                IsLoggingIn = false;
                SetLoading(false);
                LoginCommand?.NotifyCanExecuteChanged();
            }
        }

        private async void OnTokenReceived(string accessToken, string refreshToken, long expires)
        {
            if (_disposed) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("登录成功，开始处理Token...");

                _dispatcher.TryEnqueue(() => SetLoading(true, "正在保存登录信息..."));

                System.Diagnostics.Debug.WriteLine("登录成功，收到Token:");
                System.Diagnostics.Debug.WriteLine($"AccessToken: {accessToken[..Math.Min(30, accessToken.Length)]}...");
                System.Diagnostics.Debug.WriteLine($"RefreshToken: {refreshToken[..Math.Min(30, refreshToken.Length)]}...");
                System.Diagnostics.Debug.WriteLine($"Expires: {DateTimeOffset.FromUnixTimeSeconds(expires)}");

                // Save tokens securely
                SaveTokensToAppData(accessToken, refreshToken, expires);

                _dispatcher.TryEnqueue(() =>
                {
                    IsLoggedIn = "已登录";
                    IsLoggingIn = false;
                    SetLoading(false);
                    LoginCommand?.NotifyCanExecuteChanged();
                });

                // Show success message
                _dispatcher.TryEnqueue(async () =>
                {
                    await ShowErrorDialogAsync("登录成功", "已成功登录到网易云音乐！");
                });

                // If user is on NetEase tab, automatically load recommendations
                if (SelectedNavItem == "netease")
                {
                    System.Diagnostics.Debug.WriteLine("用户在网易云页面，自动加载推荐");
                    _ = Task.Run(async () => await LoadNetEaseRecommendationsAsync());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理Token失败: {ex.Message}");
                _dispatcher.TryEnqueue(async () =>
                {
                    IsLoggingIn = false;
                    SetLoading(false);
                    LoginCommand?.NotifyCanExecuteChanged();
                    await ShowErrorDialogAsync("警告", $"登录成功但保存凭据失败: {ex.Message}");
                });
            }
        }

        private void SaveTokensToAppData(string accessToken, string refreshToken, long expires)
        {
            try
            {
                var tokenData = new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expires,
                    SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var tokenJson = JsonSerializer.Serialize(tokenData);
                var tokenPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "auth_tokens.json");
                File.WriteAllText(tokenPath, tokenJson);

                System.Diagnostics.Debug.WriteLine("Token已保存到本地存储");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存Token到本地存储失败: {ex.Message}");
                throw;
            }
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            if (_disposed) return;

            try
            {
                if (_dispatcher?.HasThreadAccess == true)
                {
                    await ShowDialogOnUIThread(title, message);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _dispatcher?.TryEnqueue(async () =>
                    {
                        try
                        {
                            await ShowDialogOnUIThread(title, message);
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    });

                    try
                    {
                        await tcs.Task;
                    }
                    catch
                    {
                        // Ignore if we can't show the dialog
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing error dialog: {ex.Message}");
            }
        }

        private async Task ShowDialogOnUIThread(string title, string message)
        {
            try
            {
                if (_xamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = title ?? "提示",
                        Content = message ?? "发生了未知错误",
                        CloseButtonText = "确定",
                        XamlRoot = _xamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show dialog: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _httpClient?.Dispose();
                _loginService?.Dispose();
                _netEaseService?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
        }
    }
}