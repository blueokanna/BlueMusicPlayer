using BlueMusicPlayer.Models.NetEase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Media;
using Windows.Storage;

namespace BlueMusicPlayer.Services
{
    public class NetEaseMusicService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string OpenApiBase = "http://openapi.music.163.com";
        private const string PublicApiBase = "https://music.163.com/api";
        private const string AppId = "a301010000000000aadb4e5a28b45a67";

        private string? _cachedAccessToken;
        private bool _disposed = false;

        public NetEaseMusicService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            // 为公开推荐接口设置必备头（对官方 OpenAPI 无影响）
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");
        }

        /// <summary>
        /// 使用官方平台颁发的 accessToken 初始化，并验证其有效性。
        /// </summary>
        public async Task InitializeWithTokenAsync(string accessToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetEaseMusicService));
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token 不能为空", nameof(accessToken));

            _cachedAccessToken = accessToken;
            var ok = await ValidateTokenAsync(accessToken);
            if (!ok)
            {
                _cachedAccessToken = null;
                throw new InvalidOperationException("provided accessToken 无效或已过期");
            }
        }

        /// <summary>
        /// 调用官方轻量级接口验证 accessToken 是否生效。
        /// </summary>
        private async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                var biz = JsonSerializer.Serialize(new { limit = 1 });
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var device = JsonSerializer.Serialize(new
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
                var url = BuildOpenApiUrl("/openapi/music/basic/recommend/songlist/get/v2", biz, accessToken, ts, device);

                using var rsp = await _httpClient.GetAsync(url);
                if (!rsp.IsSuccessStatusCode) return false;
                var txt = await rsp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(txt);
                return doc.RootElement.GetProperty("code").GetInt32() == 200;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<NetEaseSong>> GetDailyRecommendationsAsync(int limit = 30)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetEaseMusicService));

            // 先尝试个性化
            try
            {
                var token = await GetAccessTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    System.Diagnostics.Debug.WriteLine($"API返回歌曲token: {token}");
                    var personalizedSongs = await GetPersonalizedAsync(token, limit);
                    if (personalizedSongs != null && personalizedSongs.Count > 0)
                    {
                        return personalizedSongs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"个性化推荐失败: {ex.Message}");
                // 个性化拿歌失败，继续执行回退逻辑
            }

            try
            {
                var publicSongs = await GetPublicRecommendationsAsync(limit);
                if (publicSongs != null && publicSongs.Count > 0)
                {
                    return publicSongs;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"公开推荐失败: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("所有推荐方式都失败了，返回空列表");
            return new List<NetEaseSong>();
        }

        private async Task<List<NetEaseSong>> GetPersonalizedAsync(string token, int limit)
        {
            try
            {
                var req = new NetEaseRecommendRequest
                {
                    Limit = Math.Min(limit, 40),
                    QualityFlag = true
                };

                var biz = JsonSerializer.Serialize(req, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var device = JsonSerializer.Serialize(new
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

                var url = BuildOpenApiUrl("/openapi/music/basic/recommend/songlist/get/v2", biz, token, ts, device);

                using var rsp = await _httpClient.GetAsync(url);
                var txt = await rsp.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"个性化API响应状态: {rsp.StatusCode}");
                if (!string.IsNullOrEmpty(txt) && txt.Length < 1000)
                {
                    System.Diagnostics.Debug.WriteLine($"个性化API响应内容: {txt}");
                }

                if (!rsp.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"个性化 API HTTP 失败: {rsp.StatusCode}");
                    return new List<NetEaseSong>();
                }

                if (string.IsNullOrEmpty(txt))
                {
                    System.Diagnostics.Debug.WriteLine("个性化 API 返回空内容");
                    return new List<NetEaseSong>();
                }

                var apiResp = JsonSerializer.Deserialize<NetEaseApiResponse<List<NetEaseSong>>>(txt,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                System.Diagnostics.Debug.WriteLine($"个性化 API 返回内容：{apiResp}");
                System.Diagnostics.Debug.WriteLine($"个性化 API 返回解析后的内容：{txt}");

                if (apiResp == null)
                {
                    System.Diagnostics.Debug.WriteLine("个性化 API 返回格式异常");
                    return new List<NetEaseSong>();
                }

                if (apiResp.Code != 200)
                {
                    System.Diagnostics.Debug.WriteLine($"个性化 API 返回错误码: {apiResp.Code}, 消息: {apiResp.Message}");
                    return new List<NetEaseSong>();
                }

                var songs = apiResp.Data ?? new List<NetEaseSong>();
                System.Diagnostics.Debug.WriteLine($"API返回歌曲数量: {songs.Count}");

                // Filter out invalid songs
                return songs.Where(s => !string.IsNullOrEmpty(s.Id) && !string.IsNullOrEmpty(s.Name)).ToList();
            }
            catch (JsonException jsonEx)
            {
                System.Diagnostics.Debug.WriteLine($"个性化推荐JSON解析异常: {jsonEx.Message}");
                return new List<NetEaseSong>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"个性化推荐异常: {ex.Message}");
                return new List<NetEaseSong>();
            }
        }

        private async Task<List<NetEaseSong>> GetPublicRecommendationsAsync(int limit)
        {
            var endpoints = new[]
            {
                ("/personalized/newsong", "新歌推荐"),
                ("/top/song", "排行榜"),
                ("/recommend/songs", "推荐歌曲"),
                ("/personalized", "个性化推荐")
            };

            foreach (var (endpoint, description) in endpoints)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"尝试获取 {description}: {endpoint}");
                    var list = await TryGetSongsFromEndpoint(endpoint, limit);
                    if (list != null && list.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"成功从 {description} 获取 {list.Count} 首歌曲");
                        return list;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"从 {description} 获取歌曲失败: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine("所有推荐端点失败，尝试搜索热门歌曲");
            return await GetSongsFromSearchAsync(limit);
        }

        private async Task<List<NetEaseSong>?> TryGetSongsFromEndpoint(string endpoint, int limit)
        {
            // 构造 URL 和查询参数
            var url = PublicApiBase + endpoint;
            if (endpoint == "/top/song") url += "?type=0";
            else if (endpoint == "/personalized/newsong") url += $"?limit={limit}";

            using var rsp = await _httpClient.GetAsync(url);
            if (!rsp.IsSuccessStatusCode) return null;

            var txt = await rsp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;
            if (root.GetProperty("code").GetInt32() != 200) return null;

            // 取 songs 数组
            JsonElement arr;
            if (root.TryGetProperty("result", out var res) &&
                (res.TryGetProperty("songs", out arr) || res.ValueKind == JsonValueKind.Array))
            {
                arr = (res.ValueKind == JsonValueKind.Array) ? res : res.GetProperty("songs");
            }
            else if (root.TryGetProperty("data", out arr))
            {
                // 有些接口直接在 data 数组里
            }
            else
            {
                return null;
            }

            var list = new List<NetEaseSong>();
            var cnt = 0;
            foreach (var el in arr.EnumerateArray())
            {
                if (cnt++ >= limit) break;
                var songEl = el.TryGetProperty("song", out var nested) ? nested : el;
                var song = ParseNetEaseSong(songEl);
                if (song != null) list.Add(song);
            }
            return list;
        }

        private async Task<List<NetEaseSong>> GetSongsFromSearchAsync(int limit)
        {
            var terms = new[] { "流行", "热门", "推荐", "新歌", "经典" };
            var term = terms[new Random().Next(terms.Length)];
            var url = PublicApiBase + "/search/get";
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("s", term),
                new KeyValuePair<string,string>("type","1"),
                new KeyValuePair<string,string>("limit", limit.ToString()),
                new KeyValuePair<string,string>("offset","0")
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
            using var rsp = await _httpClient.SendAsync(req);
            if (!rsp.IsSuccessStatusCode) return new();

            var txt = await rsp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;
            if (root.GetProperty("code").GetInt32() != 200) return new();

            var list = new List<NetEaseSong>();
            if (root.TryGetProperty("result", out var res) &&
                res.TryGetProperty("songs", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var song = ParseNetEaseSong(el);
                    if (song != null) list.Add(song);
                }
            }
            return list;
        }

        /// <summary>
        /// 通用的 Json->NetEaseSong 解析，兼容多种字段名。
        /// </summary>
        private NetEaseSong? ParseNetEaseSong(JsonElement el)
        {
            try
            {
                var s = new NetEaseSong();
                if (el.TryGetProperty("id", out var idEl)) s.Id = idEl.GetString() ?? "";
                if (el.TryGetProperty("name", out var nameEl)) s.Name = nameEl.GetString() ?? "";

                if (el.TryGetProperty("dt", out var dt) ||
                    el.TryGetProperty("duration", out dt) ||
                    el.TryGetProperty("dur", out dt))
                    s.Duration = dt.GetInt64();

                if (el.TryGetProperty("ar", out var arts) ||
                    el.TryGetProperty("artists", out arts))
                {
                    s.Artists = new List<NetEaseArtist>();
                    foreach (var a in arts.EnumerateArray())
                    {
                        var art = new NetEaseArtist();
                        if (a.TryGetProperty("id", out var aid)) art.Id = aid.GetString() ?? "";
                        if (a.TryGetProperty("name", out var an)) art.Name = an.GetString() ?? "";
                        s.Artists.Add(art);
                    }
                }

                if (el.TryGetProperty("al", out var al) ||
                    el.TryGetProperty("album", out al))
                {
                    s.Album = new NetEaseAlbum();
                    if (al.TryGetProperty("id", out var aid)) s.Album.Id = aid.GetString() ?? "";
                    if (al.TryGetProperty("name", out var an)) s.Album.Name = an.GetString() ?? "";
                    if (al.TryGetProperty("picUrl", out var pic)) s.CoverImgUrl = pic.GetString() ?? "";
                }

                if (el.TryGetProperty("fee", out var fee))
                {
                    var f = fee.GetInt32();
                    s.ParsedVip = f == 1 || f == 4;
                }
                if (el.TryGetProperty("privilege", out var p) &&
                    p.TryGetProperty("st", out var st) &&
                    st.GetInt32() < 0)
                {
                    s.ParsedVip = true;
                }

                // id/name 至少要有
                if (string.IsNullOrEmpty(s.Id) || string.IsNullOrEmpty(s.Name))
                    return null;

                return s;
            }
            catch
            {
                return null;
            }
        }

        private string BuildOpenApiUrl(string path, string biz, string token, long ts, string device)
        {
            var uri = new UriBuilder(OpenApiBase + path);
            var q = HttpUtility.ParseQueryString(string.Empty);
            q["appId"] = AppId;
            q["bizContent"] = biz;
            q["signType"] = "RSA_SHA256";
            q["accessToken"] = token;
            q["device"] = device;
            q["timestamp"] = ts.ToString();
            uri.Query = q.ToString();
            return uri.ToString();
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            if (_disposed) return null;
            if (!string.IsNullOrEmpty(_cachedAccessToken))
                return _cachedAccessToken;

            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "auth_tokens.json");
            if (!File.Exists(path)) return null;

            var txt = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(txt);
            if (!doc.RootElement.TryGetProperty("ExpiresAt", out var exp) ||
                !doc.RootElement.TryGetProperty("AccessToken", out var tok))
                return null;

            var expires = exp.GetInt64();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var at = tok.GetString();
            if (at != null && expires > now)
            {
                _cachedAccessToken = at;
                return at;
            }
            return null;
        }

        public async Task<bool> IsLoggedInAsync()
        {
            if (_disposed) return false;
            return !string.IsNullOrEmpty(await GetAccessTokenAsync());
        }

        public void ClearCachedToken()
        {
            _cachedAccessToken = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _httpClient.Dispose();
            _cachedAccessToken = null;
            _disposed = true;
        }
    }
}
