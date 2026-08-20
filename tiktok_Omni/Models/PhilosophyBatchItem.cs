using System;
using System.Collections.Generic;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Models
{
    /// <summary>Một dòng lưới Video Triết lý = một chủ đề/batch gồm nhiều câu quote.</summary>
    public sealed class PhilosophyBatchItem
    {
        public Guid BatchId { get; set; } = Guid.NewGuid();

        public string ProfileName { get; set; } = string.Empty;

        /// <summary>Chủ đề do người dùng nhập (toolbar hoặc popup).</summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>Quotes | Story — mode Gemini lúc tạo batch.</summary>
        public string GenerationMode { get; set; } = "Quotes";

        /// <summary>Loại nội dung — id từ <see cref="PhilosophyContentTemplatePresets"/>.</summary>
        public string ContentTemplateId { get; set; } = PhilosophyContentTemplatePresets.DefaultId;

        /// <summary>Metadata tùy chọn (vd. tác giả sách) — dùng khi template hỗ trợ.</summary>
        public string ContentMetadata { get; set; } = string.Empty;

        /// <summary>Số quote Gemini sinh (Quotes mode).</summary>
        public int QuoteCount { get; set; } = 5;

        /// <summary>Ảnh tham chiếu (popup Nội dung).</summary>
        public string ReferenceImagePath { get; set; } = string.Empty;

        public List<PhilosophyScriptItem> Quotes { get; set; } = new List<PhilosophyScriptItem>();

        // Âm thanh cấp batch (áp dụng cho mọi quote khi render)
        public string MusicFolder { get; set; } = string.Empty;

        public string AmbientKey { get; set; } = string.Empty;

        public int MusicVolumePercent { get; set; } = 20;

        // Giọng đọc quote (mirror Showcase body voice — popup Âm thanh)
        public string BodyTtsEngine { get; set; } = string.Empty;

        public string BodyVoicePresetId { get; set; } = string.Empty;

        public string BodyVoiceGenderId { get; set; } = string.Empty;

        public string BodyVoiceLanguageId { get; set; } = string.Empty;

        public string BodyElevenPersona { get; set; } = string.Empty;

        public string BodyVoiceToneId { get; set; } = string.Empty;

        public int BodyElevenCustomStabilityPercent { get; set; } = 50;

        public int BodyElevenCustomSimilarityPercent { get; set; } = 75;

        public int BodyElevenCustomStylePercent { get; set; } = 15;

        public string BodyStyleKey { get; set; } = string.Empty;

        public int BodyEdgeRateOffsetPercent { get; set; }

        public int BodyEdgePitchOffsetHz { get; set; }

        public int BodyNarrationSpeedPercent { get; set; }

        // Phụ đề cấp batch
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

        /// <summary>Chữ hiển thị quote (rút gọn) — rỗng = full quote TTS.</summary>
        public string SubtitleDisplayQuote { get; set; } = string.Empty;

        public string SubtitleDisplayAnimation { get; set; } = string.Empty;

        // Logo thương hiệu cấp batch (mirror Showcase)
        public bool BrandLogoEnabled { get; set; } = true;

        public string BrandLogoFile { get; set; } = string.Empty;

        public string BrandLogoPositionId { get; set; } = string.Empty;

        public int BrandLogoScaleWidthPercent { get; set; }

        public int BrandLogoMarginX { get; set; }

        public int BrandLogoMarginY { get; set; }

        public int BrandLogoOpacityPercent { get; set; }

        public string Status { get; set; } = "Nháp";

        public string LastError { get; set; } = string.Empty;

        public int MinDurationSeconds { get; set; } = 15;

        public int MaxDurationSeconds { get; set; } = 60;

        // Grid display (không persist riêng)
        public string TopicGridLabel
        {
            get
            {
                var preset = PhilosophyContentTemplatePresets.Resolve(ContentTemplateId);
                var topic = PhilosophyBatchHelper.TrimGridLabel(Topic, 40, "—");
                if (preset == null || preset.Id == PhilosophyContentTemplatePresets.DefaultId)
                {
                    return topic;
                }

                var tag = PhilosophyBatchHelper.TrimGridLabel(preset.DisplayLabel, 18, preset.Id);
                return tag + " · " + topic;
            }
        }

        public string ContentGridLabel
        {
            get
            {
                var n = Quotes?.Count ?? 0;
                return n == 0 ? "Chưa có câu…" : n + " câu · bấm mở";
            }
        }

        public string BackgroundGridLabel => PhilosophyBatchHelper.DescribeBatchBackgroundSummary(this);

        public string AudioGridLabel => PhilosophyBatchHelper.DescribeBatchAudioSummary(this);

        public string SubtitleGridLabel =>
            string.IsNullOrWhiteSpace(SubtitleStyleLabel) ? "Mặc định · bấm mở" : SubtitleStyleLabel.Trim();

        public string LogoGridLabel => PhilosophyBatchHelper.DescribeBatchLogoSummary(this);

        public string StatusGridLabel => Status ?? "Nháp";

        public string OutputGridLabel => PhilosophyBatchHelper.DescribeBatchOutputSummary(this);

        public void RefreshDerivedFields()
        {
            Status = PhilosophyBatchHelper.ComputeBatchStatus(this);
        }

        public IEnumerable<PhilosophyScriptItem> EnumerateQuotes()
        {
            return Quotes ?? Enumerable.Empty<PhilosophyScriptItem>();
        }
    }
}
