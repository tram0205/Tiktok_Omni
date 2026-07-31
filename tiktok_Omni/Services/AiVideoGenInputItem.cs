using System;
using System.Collections.Generic;
using System.Linq;

using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public class AiVideoGenInputItem
    {
        public string ProfileName { get; set; } = string.Empty;
        public string SourceKeyword { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string HookText { get; set; } = string.Empty;
        public string Hashtags { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string AffiliateLink { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Top reviews — lưu dạng chuỗi nối bằng ||| hoặc set qua <see cref="SetCustomerReviews"/>.</summary>
        public string CustomerReviews { get; set; } = string.Empty;

        public bool IsProcessed { get; set; }
        public string PipelineStatus { get; set; } = "Chờ";
        public int SafetyScore { get; set; } = 100;
        public int ProgressPercent { get; set; }
        public string ThumbnailPath { get; set; } = string.Empty;
        public string OutputVideoPath { get; set; } = string.Empty;
        public string ScriptPreview { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        /// <summary>Showcase: vai trò phân cảnh do Gemini gán (pain/agitate/solve/cta hoặc attention/interest/desire/action).</summary>
        public string SceneRole { get; set; } = string.Empty;

        /// <summary>Showcase: tên ngắn của phân cảnh (vd. "Chất vải cận cảnh") hiển thị trên storyboard.</summary>
        public string SceneTitle { get; set; } = string.Empty;

        /// <summary>Showcase: lời thoại riêng cho phân cảnh này, do Gemini viết.</summary>
        public string SceneVoiceover { get; set; } = string.Empty;

        /// <summary>Chữ phụ đề cảnh — để trống = hiện đủ <see cref="SceneVoiceover"/> (TTS không đổi).</summary>
        public string ShowcaseSubtitleDisplayVoiceover { get; set; } = string.Empty;

        /// <summary>Hiệu ứng dòng trên tab Chữ hiển thị — rỗng = tab Kiểu chữ (thân).</summary>
        public string ShowcaseSubtitleDisplayAnimation { get; set; } = string.Empty;

        /// <summary>Font dòng phụ đề — rỗng = theo mẫu thân video.</summary>
        public string ShowcaseSubtitleDisplayFontName { get; set; } = string.Empty;

        /// <summary>0 = theo mẫu thân.</summary>
        public int ShowcaseSubtitleDisplayFontSize { get; set; }

        /// <summary>Bold | Italic | rỗng = theo mẫu.</summary>
        public string ShowcaseSubtitleDisplayFontFace { get; set; } = string.Empty;

        /// <summary>Top | Middle | Bottom | rỗng = theo mẫu.</summary>
        public string ShowcaseSubtitleDisplayPosition { get; set; } = string.Empty;

        /// <summary>ASS PrimaryColour — rỗng = theo mẫu / mặc định.</summary>
        public string ShowcaseSubtitleDisplayPrimaryColourAss { get; set; } = string.Empty;

        /// <summary>Viền/bóng — rỗng = theo mẫu.</summary>
        public string ShowcaseSubtitleDisplayDecorPreset { get; set; } = string.Empty;

        /// <summary>Preset kiểu chữ — rỗng = theo mẫu thân.</summary>
        public string ShowcaseSubtitleDisplayLookPreset { get; set; } = string.Empty;

        /// <summary>Màu tô dòng — rỗng = theo mẫu / kiểu chữ.</summary>
        public string ShowcaseSubtitleDisplayHighlightColourAss { get; set; } = string.Empty;

        /// <summary>Showcase: đường dẫn đầy đủ file ảnh khi user chọn từ máy (trước khi copy vào source_images).</summary>
        public string ShowcaseLocalPickPath { get; set; } = string.Empty;

        /// <summary>Showcase: cảnh im lặng — chỉ hình + nhạc, không TTS (Gemini gợi ý silent).</summary>
        public bool ShowcaseSceneSilent { get; set; }

        /// <summary>Showcase: prompt tiếng Anh dùng để tạo clip Veo (I2V) cho phân cảnh này.</summary>
        public string VeoPrompt { get; set; } = string.Empty;

        /// <summary>Showcase: loại ảnh cảnh — flatlay hoặc on_model (do Gemini gán).</summary>
        public string ShowcaseImageKind { get; set; } = string.Empty;

        /// <summary>Showcase: công cụ tạo clip — veo, zoom hoặc kling.</summary>
        public string ShowcaseClipTool { get; set; } = string.Empty;

        /// <summary>Showcase: prompt tiếng Anh cho Kling I2V (cảnh on-model).</summary>
        public string KlingPrompt { get; set; } = string.Empty;

        /// <summary>Showcase: gợi ý chuyển động Zoom Ken Burns (tiếng Anh ngắn).</summary>
        public string ZoomHint { get; set; } = string.Empty;

        /// <summary>Showcase: preset zoom FFmpeg — push_in, pan_left, drift, … (<see cref="ShowcaseZoomStyleCatalog"/>).</summary>
        public string ShowcaseZoomStyleId { get; set; } = string.Empty;

        /// <summary>Showcase: tốc độ Ken Burns — slow, medium, fast (<see cref="ShowcaseZoomSpeedCatalog"/>).</summary>
        public string ShowcaseZoomSpeedId { get; set; } = string.Empty;

        /// <summary>Showcase: thời lượng clip gợi ý (giây) — Gemini / ước từ thoại; dùng cho clip Zoom FFmpeg.</summary>
        public double ShowcaseClipDurationSeconds { get; set; }

        /// <summary>Showcase: đường dẫn clip Veo đã tạo tay, khớp phân cảnh này (vd. veo_clips\scene_02.mp4).</summary>
        public string ClipPath { get; set; } = string.Empty;

        /// <summary>Showcase: chủ đề tuỳ chọn (Gemini tự suy nếu để trống) — dùng chung cả phiên, lưu trên dòng đầu lưới.</summary>
        public string ShowcaseTheme { get; set; } = string.Empty;

        /// <summary>Showcase: bật đa giọng đọc khi render — dùng chung cả phiên.</summary>
        public bool ShowcaseMultiVoice { get; set; }

        /// <summary>Showcase: cỡ chữ overlay khi render.</summary>
        public int ShowcaseTextSize { get; set; } = 50;

        /// <summary>Showcase: âm lượng nhạc nền (0–100) khi render.</summary>
        public int ShowcaseMusicVolume { get; set; } = 14;

        /// <summary>Showcase: thời gian chuyển cảnh (giây) khi render.</summary>
        public double ShowcaseTransitionSeconds { get; set; } = 0.6;

        /// <summary>Showcase: file SFX (tên trong Showcase\Sfx hoặc id Gemini).</summary>
        public string ShowcaseSfxFile { get; set; } = string.Empty;

        public bool ShowcaseSfxEnabled { get; set; } = true;

        /// <summary>scene_start | scene_middle | scene_end</summary>
        public string ShowcaseSfxPlacement { get; set; } = ShowcaseSfxCatalog.PlacementSceneStart;

        public double ShowcaseSfxOffsetSeconds { get; set; }

        public int ShowcaseSfxVolumePercent { get; set; }

        /// <summary>Gợi ý ngắn từ Gemini — chỉ hiển thị UI.</summary>
        public string ShowcaseSfxGeminiHint { get; set; } = string.Empty;

        public void SetCustomerReviews(IEnumerable<string> reviews)
        {
            var list = (reviews ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Take(3)
                .ToList();
            CustomerReviews = list.Count == 0 ? string.Empty : string.Join("|||", list);
        }

        public List<string> GetCustomerReviewsList()
        {
            if (string.IsNullOrWhiteSpace(CustomerReviews))
            {
                return new List<string>();
            }

            return CustomerReviews
                .Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Take(3)
                .ToList();
        }
    }
}
