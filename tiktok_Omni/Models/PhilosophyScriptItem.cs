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

        /// <summary>Tên file hoặc đường dẫn nhạc nền; trống = dùng nhạc profile / mood.</summary>
        public string MusicFolder { get; set; } = string.Empty;

        /// <summary>Loại tiếng đệm: none, rain, wind, forest, ocean, city, fire, night, thunder, piano.</summary>
        public string AmbientKey { get; set; } = string.Empty;

        /// <summary>Nhãn tóm tắt phụ đề hiển thị trên lưới.</summary>
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

        public string Status { get; set; } = "Nháp";

        /// <summary>Profile TikTok / branding cho dòng này.</summary>
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
    }
}
