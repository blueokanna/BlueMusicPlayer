using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace BlueMusicPlayer.Models.NetEase
{
    public class NetEaseSong : INotifyPropertyChanged
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public long Duration { get; set; }

        [JsonPropertyName("artists")]
        public List<NetEaseArtist> Artists { get; set; } = new List<NetEaseArtist>();

        [JsonPropertyName("fullArtists")]
        public List<NetEaseArtist> FullArtists { get; set; } = new List<NetEaseArtist>();

        [JsonPropertyName("album")]
        public NetEaseAlbum Album { get; set; } = new NetEaseAlbum();

        [JsonPropertyName("playFlag")]
        public bool PlayFlag { get; set; }

        [JsonPropertyName("downloadFlag")]
        public bool DownloadFlag { get; set; }

        [JsonPropertyName("payPlayFlag")]
        public bool PayPlayFlag { get; set; }

        [JsonPropertyName("payDownloadFlag")]
        public bool PayDownloadFlag { get; set; }

        [JsonPropertyName("vipFlag")]
        public bool VipFlag { get; set; }

        [JsonPropertyName("liked")]
        public bool Liked { get; set; }

        [JsonPropertyName("coverImgUrl")]
        public string CoverImgUrl { get; set; } = string.Empty;

        [JsonPropertyName("vipPlayFlag")]
        public bool VipPlayFlag { get; set; }

        [JsonPropertyName("accompanyFlag")]
        public bool? AccompanyFlag { get; set; }

        [JsonPropertyName("songMaxBr")]
        public int SongMaxBr { get; set; }

        [JsonPropertyName("userMaxBr")]
        public int UserMaxBr { get; set; }

        [JsonPropertyName("maxBrLevel")]
        public string MaxBrLevel { get; set; } = string.Empty;

        [JsonPropertyName("plLevel")]
        public string PlLevel { get; set; } = string.Empty;

        [JsonPropertyName("dlLevel")]
        public string DlLevel { get; set; } = string.Empty;

        [JsonPropertyName("songTag")]
        public List<string> SongTag { get; set; } = new List<string>();

        [JsonPropertyName("alg")]
        public string Alg { get; set; } = string.Empty;

        [JsonPropertyName("privateCloudSong")]
        public bool PrivateCloudSong { get; set; }

        [JsonPropertyName("freeTrailFlag")]
        public bool FreeTrailFlag { get; set; }

        [JsonPropertyName("songFtFlag")]
        public bool SongFtFlag { get; set; }

        [JsonPropertyName("freeTrialPrivilege")]
        public FreeTrialPrivilege FreeTrialPrivilege { get; set; } = new FreeTrialPrivilege();

        [JsonPropertyName("songFee")]
        public int SongFee { get; set; }

        [JsonPropertyName("playMaxbr")]
        public int PlayMaxbr { get; set; }

        [JsonPropertyName("qualities")]
        public List<string> Qualities { get; set; } = new List<string>();

        [JsonPropertyName("emotionTag")]
        public string? EmotionTag { get; set; }

        [JsonPropertyName("vocalFlag")]
        public bool? VocalFlag { get; set; }

        [JsonPropertyName("payed")]
        public PayedInfo? Payed { get; set; }

        [JsonPropertyName("visible")]
        public bool Visible { get; set; }

        // Backing field for parsed VIP status (from fee/privilege)
        private bool _parsedVip;

        /// <summary>
        /// 通过解析 JSON fee 和 privilege 时设置
        /// </summary>
        [JsonIgnore]
        public bool ParsedVip
        {
            get => _parsedVip;
            set
            {
                if (_parsedVip != value)
                {
                    _parsedVip = value;
                    OnPropertyChanged(nameof(ParsedVip));
                    OnPropertyChanged(nameof(IsVip));
                }
            }
        }

        // Computed properties for UI binding
        public string ArtistsText => Artists.Any()
            ? string.Join(", ", Artists.Select(a => a.Name))
            : "Unknown Artist";

        public string DurationText
        {
            get
            {
                var timeSpan = TimeSpan.FromMilliseconds(Duration);
                return timeSpan.TotalHours >= 1
                    ? $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}"
                    : $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
            }
        }

        public string TagsText => SongTag.Any()
            ? string.Join(" · ", SongTag)
            : string.Empty;

        /// <summary>
        /// 最终 VIP 状态：接口字段 或 fee/privilege 解析
        /// </summary>
        [JsonIgnore]
        public bool IsVip => VipFlag || PayPlayFlag || VipPlayFlag || ParsedVip;

        public string PlayabilityText
        {
            get
            {
                if (VipFlag) return "VIP";
                if (PayPlayFlag) return "付费";
                if (VipPlayFlag) return "VIP播放";
                if (ParsedVip) return "不可播放";
                return string.Empty;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NetEaseArtist
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class NetEaseAlbum
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class FreeTrialPrivilege
    {
        [JsonPropertyName("cannotListenReason")]
        public string? CannotListenReason { get; set; }

        [JsonPropertyName("resConsumable")]
        public bool ResConsumable { get; set; }

        [JsonPropertyName("userConsumable")]
        public bool UserConsumable { get; set; }
    }

    public class PayedInfo
    {
        [JsonPropertyName("payed")]
        public int Payed { get; set; }

        [JsonPropertyName("vipPackagePayed")]
        public int VipPackagePayed { get; set; }

        [JsonPropertyName("singlePayed")]
        public int SinglePayed { get; set; }

        [JsonPropertyName("albumPayed")]
        public int AlbumPayed { get; set; }
    }

    public class NetEaseApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("subCode")]
        public string? SubCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    public class NetEaseRecommendRequest
    {
        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 30;

        [JsonPropertyName("qualityFlag")]
        public bool QualityFlag { get; set; } = true;
    }
}