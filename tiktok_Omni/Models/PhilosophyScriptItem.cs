using tiktok_Omni.Services;

using System.Collections.Generic;

namespace tiktok_Omni.Models
{
    /// <summary>Một dòng kịch bản trên lưới tab Video Triết lý.</summary>
    public sealed class PhilosophyScriptItem
    {
        public string Content { get; set; } = string.Empty;

        /// <summary>calm | melancholic | hopeful | intense | reflective</summary>
        public string Mood { get; set; } = "reflective";

        /// <summary>Gợi ý chuyển động/camera cho Veo (Text-to-Video hoặc Image-to-Video).</summary>
        public string MotionPrompt { get; set; } = string.Empty;

        /// <summary>Nền B-Roll: @random, đường dẫn file .mp4, hoặc thư mục (legacy).</summary>
        public string BRollFolder { get; set; } = string.Empty;

        /// <summary>Tên file hoặc đường dẫn nhạc nền (per-quote — lưới Âm thanh).</summary>
        public string MusicFolder { get; set; } = string.Empty;

        /// <summary>Âm lượng nhạc nền per-quote (0–100%).</summary>
        public int MusicVolumePercent { get; set; } = PhilosophyAudioDefaults.DefaultMusicVolumePercent;

        /// <summary>Tốc độ thoại per-quote (50–200%).</summary>
        public int NarrationSpeedPercent { get; set; } = 100;

        /// <summary>Tiếng đệm per-quote (lưới Âm thanh).</summary>
        public string AmbientKey { get; set; } = string.Empty;

        /// <summary>Gemini gợi ý Edge style (ke_chuyen, noi_dau, …) — sync lên batch.</summary>
        public string EdgeStyleKey { get; set; } = string.Empty;

        /// <summary>Legacy — không dùng khi quote thuộc batch; đọc từ <see cref="PhilosophyBatchItem"/>.</summary>
        public string SubtitleStyleLabel { get; set; } = string.Empty;

        public string SubtitlePosition { get; set; } = string.Empty;

        public string SubtitleFontName { get; set; } = string.Empty;

        public int SubtitleFontSize { get; set; }

        public string SubtitleAnimation { get; set; } = string.Empty;

        public bool SubtitleBold { get; set; } = true;

        public bool SubtitleItalic { get; set; }

        public int SubtitleWordsPerLine { get; set; }

        public string SubtitlePrimaryColourAss { get; set; } = string.Empty;

        public string SubtitleSecondaryColourAss { get; set; } = string.Empty;

        public bool SubtitleEnabled { get; set; } = true;

        public string SubtitleLookPreset { get; set; } = string.Empty;

        public string SubtitleDecorPreset { get; set; } = string.Empty;

        public string SubtitleHighlightColourAss { get; set; } = string.Empty;

        public string SubtitleDisplayQuote { get; set; } = string.Empty;

        public string SubtitleDisplayAnimation { get; set; } = string.Empty;

        public string Status { get; set; } = "Nháp";

        /// <summary>Legacy — không persist; profile lấy từ <see cref="PhilosophyBatchItem.ProfileName"/>.</summary>
        public string ProfileName { get; set; } = string.Empty;

        /// <summary>0 = B-Roll, 1 = Veo T2V, 2 = Veo I2V.</summary>
        public int VisualMode { get; set; }

        /// <summary>Nhãn hiển thị trên lưới (map ↔ <see cref="VisualMode"/>).</summary>
        public string VisualModeLabel
        {
            get => PhilosophyVisualModes.ToLabel(VisualMode);
            set => VisualMode = PhilosophyVisualModes.FromLabel(value);
        }

        /// <summary>Đường dẫn MP4 sau render thành công.</summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>Lỗi render gần nhất.</summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>Danh sách phân cảnh + prompt AI (sinh bởi Gemini).</summary>
        public List<PhilosophySceneItem> Scenes { get; set; } = new List<PhilosophySceneItem>();

        /// <summary>Mode 3: thư mục chứa video phân cảnh (.mp4) cho dòng này; trống = dùng «Thư mục video» trên thanh công cụ.</summary>
        public string SceneVideoFolder { get; set; } = string.Empty;

        /// <summary>Mode 4: danh sách ảnh zoom Ken Burns (theo thứ tự ghép).</summary>
        public List<string> ZoomImagePaths { get; set; } = new List<string>();
    }
}
