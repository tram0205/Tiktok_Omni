using System;
using System.Collections.Generic;

namespace tiktok_Omni.Services.Jobs
{
    public sealed class HuntKeywordEntry
    {
        public string RawInput { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public string ProfileName { get; set; } = "default";
    }

    public sealed class HuntAffiliateJobPayload
    {
        public List<HuntKeywordEntry> KeywordEntries { get; set; } = new List<HuntKeywordEntry>();

        /// <summary>Legacy — dùng khi KeywordEntries trống.</summary>
        public List<string> Keywords { get; set; } = new List<string>();
        public List<string> Platforms { get; set; } = new List<string> { "TikTok" };
        public string SearchMode { get; set; } = "Video";
        public string ProfileName { get; set; } = "default";
        public int MaxResultsPerKeyword { get; set; } = 20;
        public bool RankByEngagement { get; set; }
        public double BufferMultiplier { get; set; } = 2.5d;
        public string StorageRootPath { get; set; } = string.Empty;
    }

    public sealed class RenderVideoJobPayload
    {
        public string ProfileName { get; set; } = string.Empty;
        public string SharedScript { get; set; } = string.Empty;
        public List<string> PerItemScripts { get; set; }
        public List<AiVideoGenInputItem> Products { get; set; } = new List<AiVideoGenInputItem>();
        public string RenderFingerprint { get; set; } = string.Empty;
        public double TransitionSeconds { get; set; }
        public int TextSize { get; set; }
        public int MusicVolume { get; set; }
        public string StorageRootPath { get; set; } = string.Empty;
        public bool UseMultiVoiceNarration { get; set; }
        /// <summary>Null hoặc rỗng khi không gắn affiliate.</summary>
        public string AffiliateLink { get; set; }

        public string ProductId { get; set; }

        public string StyleTemplate { get; set; } = "Storytelling";
    }

    public sealed class AutoPostJobPayload
    {
        public string VideoFolder { get; set; } = string.Empty;
        public string VideoFilePath { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public bool PostTikTok { get; set; }
        public bool PostFacebook { get; set; }
        public bool PostYouTube { get; set; }
        public string TikTokCaption { get; set; } = string.Empty;
        public string TikTokHashtags { get; set; } = string.Empty;
        public bool TikTokUploadOnly { get; set; }
        public string FacebookCaption { get; set; } = string.Empty;
        public string FacebookHashtags { get; set; } = string.Empty;
        public bool FacebookAttachShopeeLink { get; set; }
        public string FacebookShopeeLink { get; set; } = string.Empty;
        public string YouTubeTitle { get; set; } = string.Empty;
        public string YouTubeDescription { get; set; } = string.Empty;
        public string PostFingerprint { get; set; } = string.Empty;
        public string CombinedCaptionPreview { get; set; } = string.Empty;
        public string StorageRootPath { get; set; } = string.Empty;
        public string VideoTypeFolder { get; set; } = "Reup";
        public string AffiliateLink { get; set; }
        public string ProductId { get; set; }
    }

    public sealed class PhilosophyVideoJobPayload
    {
        public string QuoteText { get; set; } = string.Empty;
        public string ProfileName { get; set; } = "default";
        public string VoiceId { get; set; } = string.Empty;
        public string VideoStyle { get; set; } = string.Empty;
        public string StorageRootPath { get; set; } = string.Empty;
        public DateTime? ScheduledPostUtc { get; set; }
    }

    public sealed class MascotStoryJobPayload
    {
        public string MascotImagePath { get; set; } = string.Empty;
        public string ChannelTheme { get; set; } = string.Empty;
        public string ProfileName { get; set; } = "default";
        public string MascotStyle { get; set; } = string.Empty;
        public List<string> IdentityImagePaths { get; set; } = new List<string>();
        public int SceneCount { get; set; } = 4;
        public string StorageRootPath { get; set; } = string.Empty;

        /// <summary>Bật Auto-Lipsync (phân tích volume → overlay miệng trước xuất video cuối).</summary>
        public bool UseLipSync { get; set; }

        public string MouthClosedPath { get; set; } = string.Empty;
        public string MouthOpenSmallPath { get; set; } = string.Empty;
        public string MouthOpenPath { get; set; } = string.Empty;
        public int MouthOverlayX { get; set; } = 420;
        public int MouthOverlayY { get; set; } = 1180;
        public double MouthOverlayScale { get; set; } = 1.0d;

        public bool UseEmotionalRemix { get; set; } = true;
        public bool UseVisualHookSfx { get; set; }
        public string VisualHookSfxPath { get; set; } = string.Empty;

    }

    public sealed class VideoReupJobPayload
    {
        public VideoReupRowItem Row { get; set; }
        public string StorageRootPath { get; set; } = string.Empty;
        public int BatchIndex { get; set; }
        public int BatchTotal { get; set; }
        public bool UseVisualHookSfx { get; set; }
        public string VisualHookSfxPath { get; set; } = string.Empty;
        public double VisualHookDurationSec { get; set; } = 3d;
    }

    public sealed class AffiliateDeepDiveJobPayload
    {
        public string VideoUrl { get; set; } = string.Empty;
        public string ProfileName { get; set; } = "default";
        public string SourceKeyword { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int SafetyScore { get; set; }
        public int MinSafetyThreshold { get; set; }
        public string StorageRootPath { get; set; } = string.Empty;
        public string YtDlpPath { get; set; } = string.Empty;
        public string FfmpegPath { get; set; } = string.Empty;
    }

    public sealed class AffiliateDeepRenderJobPayload
    {
        public string ProfileName { get; set; } = "default";
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<AiVideoGenInputItem> Products { get; set; } = new List<AiVideoGenInputItem>();
        public string StorageRootPath { get; set; } = string.Empty;
        public int SafetyScore { get; set; } = 100;
        public bool UseMultiVoiceNarration { get; set; }
        public string AffiliateLink { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
    }
}
