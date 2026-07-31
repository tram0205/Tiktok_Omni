using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseVideoFormatPreset
    {
        public ShowcaseVideoFormatPreset(string id, string displayLabel, string hint, bool suppressCta)
        {
            Id = id ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
            Hint = hint ?? string.Empty;
            SuppressCta = suppressCta;
        }

        public string Id { get; }

        public string DisplayLabel { get; }

        /// <summary>Mô tả ngắn hiển thị trong dialog «Loại SP · Chủ đề».</summary>
        public string Hint { get; }

        /// <summary>True = video không kết CTA (kể chuyện thuần / hướng dẫn thuần).</summary>
        public bool SuppressCta { get; }

        public override string ToString() => DisplayLabel;
    }

    /// <summary>Định dạng video Showcase — mặc định "Quảng cáo SP" (CTA nhắn tin tư vấn); các preset khác đổi mạch kịch bản/CTA.</summary>
    public static class ShowcaseVideoFormatPresets
    {
        public const string DefaultId = "product_showcase";
        public const string StorytellingId = "storytelling";
        public const string TutorialId = "tutorial";
        public const string NoCtaId = "no_cta";

        public static ShowcaseVideoFormatPreset ProductShowcase { get; } =
            new ShowcaseVideoFormatPreset(
                DefaultId,
                "Quảng cáo SP (mặc định)",
                "Mạch quảng cáo AIDA/PAS\r\ncâu kết mời tương tác",
                suppressCta: false);

        public static ShowcaseVideoFormatPreset Storytelling { get; } =
            new ShowcaseVideoFormatPreset(
                StorytellingId,
                "Kể chuyện",
                "Kể chuyện cảm xúc\r\ncâu kết mời theo dõi hoặc bình luận",
                suppressCta: false);

        public static ShowcaseVideoFormatPreset Tutorial { get; } =
            new ShowcaseVideoFormatPreset(
                TutorialId,
                "Tutorial / hướng dẫn",
                "Hướng dẫn từng bước\r\ncâu kết mời lưu video hoặc hỏi thêm",
                suppressCta: false);

        public static ShowcaseVideoFormatPreset NoCta { get; } =
            new ShowcaseVideoFormatPreset(
                NoCtaId,
                "Không CTA",
                "Quảng cáo mềm\r\ncâu chốt tự nhiên, không kêu mua",
                suppressCta: true);

        public static IReadOnlyList<ShowcaseVideoFormatPreset> All { get; } = new[]
        {
            ProductShowcase,
            Storytelling,
            Tutorial,
            NoCta
        };

        public static string GetDisplayLabel(string formatId)
        {
            var preset = FindById(formatId);
            return preset?.DisplayLabel ?? ProductShowcase.DisplayLabel;
        }

        public static string ResolveId(string formatId)
        {
            var normalized = Normalize(formatId);
            return FindById(normalized) != null ? normalized : DefaultId;
        }

        public static ShowcaseVideoFormatPreset Resolve(string formatId)
        {
            return FindById(ResolveId(formatId)) ?? ProductShowcase;
        }

        private static ShowcaseVideoFormatPreset FindById(string formatId)
        {
            var normalized = Normalize(formatId);
            return All.FirstOrDefault(p => string.Equals(p.Id, normalized, StringComparison.Ordinal));
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
