using System.ComponentModel;

namespace tiktok_Omni.Services
{
    /// <summary>Hàng làm việc cho tab «Video reup» (nguồn Săn Affiliate; remix FFmpeg + Lyria; phụ đề hook trong render).</summary>
    public class VideoReupRowItem
    {
        [Browsable(false)]
        public string SourceKeyword { get; set; } = string.Empty;

        /// <summary>Chrome profile (nick) sở hữu dòng — tránh trộn file giữa các nick.</summary>
        public string ProfileName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        [Browsable(false)]
        public string Price { get; set; } = string.Empty;

        public string VideoUrl { get; set; } = string.Empty;

        [Browsable(false)]
        public string ImageUrl { get; set; } = string.Empty;

        public string Hashtags { get; set; } = string.Empty;
        public string VideoScript { get; set; } = string.Empty;

        /// <summary>Câu hook thoại (Gemini / chỉnh tay) trước khi voiceover — hiển thị/sửa trong bảng Video reup.</summary>
        public string ReupHookDraft { get; set; } = string.Empty;

        /// <summary>MP3 hook TTS (temp_downloads\hook_audio_*.mp3) trước khi chuẩn hóa WAV.</summary>
        [Browsable(false)]
        public string HookAudioPath { get; set; } = string.Empty;

        /// <summary>Gemini gợi ý tên file .mp3 (có thể chọn trong Combo).</summary>
        [Browsable(false)]
        public string ReupSuggestedMusicFile { get; set; } = string.Empty;

        /// <summary>File nhạc nền đã chọn (tên file trong VideoReup\Music).</summary>
        [Browsable(false)]
        public string ReupSelectedMusicFile { get; set; } = string.Empty;

        /// <summary>Sau hook: nhạc nền hay giữ tiếng gốc video (chế độ phim).</summary>
        [Browsable(false)]
        public VideoReupAudioMode ReupAudioMode { get; set; } = VideoReupAudioMode.AffiliateBed;

        /// <summary>Thư mục làm việc bền cho từng video (tải nguồn, hook Lyria, wav).</summary>
        [Browsable(false)]
        public string ReupStageFolder { get; set; } = string.Empty;

        /// <summary>Đường dẫn file video nguồn đã tải (thường …\source.mp4 trong stage).</summary>
        [Browsable(false)]
        public string ReupDownloadedVideoPath { get; set; } = string.Empty;

        /// <summary>File WAV hook đã chuẩn hóa (sau Lyria + FFmpeg), dùng khi render.</summary>
        [Browsable(false)]
        public string ReupHookAudioPath { get; set; } = string.Empty;

        /// <summary>File MP4 remix gần nhất (cắt, lật, hook Lyria + nhạc).</summary>
        public string LastRemixOutputPath { get; set; } = string.Empty;

        /// <summary>Thời lượng video nguồn đã tải (giây) — set sau remix thành công.</summary>
        [Browsable(false)]
        public double? LastSourceVideoDurationSec { get; set; }

        /// <summary>Thời lượng MP4 remix (giây).</summary>
        [Browsable(false)]
        public double? LastRemixOutputVideoDurationSec { get; set; }

        /// <summary>Độ dài hook thoại đã dùng (giây).</summary>
        [Browsable(false)]
        public double? LastHookDurationUsedSec { get; set; }

        /// <summary>Trạng thái remix gần nhất (ví dụ: Đang xử lý, Xong, Lỗi).</summary>
        public string RemixStatus { get; set; } = string.Empty;

        /// <summary>Thông báo lỗi remix gần nhất (nếu có).</summary>
        public string RemixLastError { get; set; } = string.Empty;

        /// <summary>Bật hook SFX 3s (file âm thanh) thay voiceover hook dài.</summary>
        [Browsable(false)]
        public bool UseVisualHookSfx { get; set; }

        /// <summary>Đường dẫn file SFX hook (mp3/wav) — tiếng cười, giật mình, …</summary>
        [Browsable(false)]
        public string VisualHookSfxPath { get; set; } = string.Empty;
    }
}
