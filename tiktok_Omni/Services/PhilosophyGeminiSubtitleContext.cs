using System;
using System.Linq;
using System.Text;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Prompt + resolve gợi ý phụ đề Gemini cho từng quote Triết lý.</summary>
    public static class PhilosophyGeminiSubtitleContext
    {
        public static string BuildSubtitlePromptSection()
        {
            var looks = string.Join(", ",
                ShowcaseSubtitleLookPresetCatalog.AllForKind(ShowcaseDisplayLineEffectKind.Body)
                    .Select(p => p.Storage));
            var effects = string.Join(", ", ShowcaseHookAnimationCatalog.All.Select(e => e.Storage));
            var lineBgs = string.Join(", ",
                ShowcaseSubtitleHighlightColourCatalog.All.Select(p => p.Label));

            var sb = new StringBuilder();
            sb.AppendLine("PHỤ ĐỀ TRÊN VIDEO — BẮT BUỘC mỗi phần tử có các trường sau (khớp mood + cảm xúc câu):");
            sb.AppendLine("  • subtitle_look — kiểu chữ, CHỈ một trong: " + looks + ".");
            sb.AppendLine("    Gợi ý mood: melancholic/reflective→StoryItalic hoặc SoftWhite; hopeful→TikTokWhite; intense→YellowAccent hoặc ReviewBold; calm→TikTokClean.");
            sb.AppendLine("  • subtitle_size — cỡ chữ số nguyên 56–96 (mặc định 76; câu ngắn/intense có thể 80–88).");
            sb.AppendLine("  • subtitle_position — bottom | middle | top (quote triết lý thường middle hoặc bottom).");
            sb.AppendLine("  • subtitle_effect — hiệu ứng karaoke, CHỈ một trong: " + effects + ".");
            sb.AppendLine("    Mặc định Highlight; câu nhẹ/calm→FadeIn; câu mạnh→PopStrong hoặc LineZoom.");
            sb.AppendLine("  • subtitle_line_bg — nền dòng phía sau chữ, CHỈ một trong: " + lineBgs + ".");
            sb.AppendLine("    Mặc định «Không nền»; chỉ dùng nền màu khi cần tương phản (cảnh sáng→Đen · mờ 75%, cảnh tối→Vàng · mờ 35%).");
            return sb.ToString().TrimEnd();
        }

        public static void ApplyGeminiHints(
            PhilosophyScriptItem row,
            string lookRaw,
            string sizeRaw,
            string positionRaw,
            string effectRaw,
            string lineBgRaw)
        {
            if (row == null)
            {
                return;
            }

            row.SubtitleLookPreset = ResolveLookStorage(lookRaw);
            row.SubtitleFontSize = ResolveFontSize(sizeRaw);
            row.SubtitlePosition = ResolvePosition(positionRaw);
            row.SubtitleDisplayAnimation = ResolveEffectStorage(effectRaw);
            row.SubtitleHighlightColourAss = ResolveLineBackgroundAss(lineBgRaw);
            row.SubtitleEnabled = true;

            if (string.IsNullOrWhiteSpace(row.SubtitleLookPreset))
            {
                row.SubtitleLookPreset = ResolveLookStorageForMood(row.Mood);
            }

            if (row.SubtitleFontSize <= 0)
            {
                row.SubtitleFontSize = 76;
            }

            if (string.IsNullOrWhiteSpace(row.SubtitlePosition))
            {
                row.SubtitlePosition = "Middle";
            }

            if (string.IsNullOrWhiteSpace(row.SubtitleDisplayAnimation))
            {
                row.SubtitleDisplayAnimation = "Highlight";
            }

            row.SubtitleAnimation = row.SubtitleDisplayAnimation;
        }

        public static string ResolveLookStorage(string raw)
        {
            var key = (raw ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return string.Empty;
            }

            if (ShowcaseSubtitleLookPresetCatalog.FindByStorage(key, ShowcaseDisplayLineEffectKind.Body) != null)
            {
                return key;
            }

            return ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(key, ShowcaseDisplayLineEffectKind.Body);
        }

        public static int ResolveFontSize(string raw)
        {
            if (int.TryParse((raw ?? string.Empty).Trim(), out var size))
            {
                return System.Math.Max(56, System.Math.Min(96, size));
            }

            return 0;
        }

        public static string ResolvePosition(string raw)
        {
            var v = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Length == 0)
            {
                return string.Empty;
            }

            if (v is "top" or "tren" or "trên")
            {
                return "Top";
            }

            if (v is "middle" or "center" or "giua" or "giữa")
            {
                return "Middle";
            }

            return "Bottom";
        }

        public static string ResolveEffectStorage(string raw)
        {
            var key = (raw ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return string.Empty;
            }

            if (ShowcaseHookAnimationCatalog.All.Any(e =>
                    string.Equals(e.Storage, key, StringComparison.OrdinalIgnoreCase)))
            {
                return ShowcaseHookAnimationCatalog.All.First(e =>
                    string.Equals(e.Storage, key, StringComparison.OrdinalIgnoreCase)).Storage;
            }

            foreach (var entry in ShowcaseHookAnimationCatalog.All)
            {
                if (string.Equals(entry.ComboLabel, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.ShortLabel, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Storage;
                }
            }

            return "Highlight";
        }

        public static string ResolveLineBackgroundAss(string raw)
        {
            var key = (raw ?? string.Empty).Trim();
            if (key.Length == 0
                || string.Equals(key, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "không nền", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, ShowcaseSubtitleHighlightColourCatalog.FollowLookLabel, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return ShowcaseSubtitleHighlightColourCatalog.SecondaryAssFromLabel(key);
        }

        private static string ResolveLookStorageForMood(string mood)
        {
            switch ((mood ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "melancholic":
                    return "StoryItalic";
                case "hopeful":
                    return "TikTokWhite";
                case "intense":
                    return "ReviewBold";
                case "calm":
                    return "TikTokClean";
                default:
                    return ShowcaseSubtitleLookPresetCatalog.BodyDefaultStorage;
            }
        }
    }
}
