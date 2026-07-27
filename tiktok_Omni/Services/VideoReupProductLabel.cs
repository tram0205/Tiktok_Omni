using System;
using System.Text;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Rút gọn tên sản phẩm / hook reup — bỏ branding shop gốc (created by, nhạc nền, tên kênh…).
    /// </summary>
    public static class VideoReupProductLabel
    {
        private static readonly string[] TitleCutMarkers =
        {
            " created by ",
            " Created by ",
            " CREATED BY ",
            " with ",
            " With ",
            " | ",
            " nhạc nền",
            " nhac nen",
            "'s nhạc",
            "'s nhac",
            " - Cô ",
            " - cô ",
            " - Shop ",
            " - shop ",
            " - Store ",
            " review ",
            " Review ",
        };

        private static readonly Regex CreatorTailRegex = new Regex(
            @"\s*(?:created\s+by|with)\s+.+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ShopTailRegex = new Regex(
            @"\s*[-–—|]\s*(?:cô|co|shop|store|review|channel|kênh|kenh)\b.+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>@shop hoặc « @Tên Shop » ở cuối tiêu đề TikTok.</summary>
        private static readonly Regex AtHandleTailRegex = new Regex(
            @"\s@.+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>@handle không dấu cách (ví dụ @aodailinh).</summary>
        private static readonly Regex AtHandleTokenRegex = new Regex(
            @"@\S+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Tên ngắn dùng trong hook/script/hashtag — không gồm shop gốc.</summary>
        public static string GetShortLabel(string rawProductName)
        {
            var s = StripEmojis((rawProductName ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(s))
            {
                return "sản phẩm này";
            }

            foreach (var marker in TitleCutMarkers)
            {
                var idx = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 12)
                {
                    s = s.Substring(0, idx).Trim();
                    break;
                }
            }

            s = CreatorTailRegex.Replace(s, string.Empty).Trim();
            s = ShopTailRegex.Replace(s, string.Empty).Trim();
            s = StripAtHandleMentions(s);
            s = StripEmojis(s);
            s = CollapseSpaces(s);
            s = TrimTrailingFluff(s);

            if (s.Length > 56)
            {
                s = TrimAtWordBoundary(s, 54);
            }

            return string.IsNullOrWhiteSpace(s) ? "sản phẩm này" : s;
        }

        /// <summary>Chuẩn hóa hook sau Gemini/fallback — thay tên dài bằng tên ngắn, cắt branding shop.</summary>
        public static string NormalizeHookText(string hook, string rawProductName, string shortLabel = null)
        {
            var text = StripEmojis((hook ?? string.Empty).Trim());
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var raw = (rawProductName ?? string.Empty).Trim();
            var label = shortLabel ?? GetShortLabel(raw);
            if (!string.IsNullOrEmpty(raw) && raw.Length > label.Length + 8)
            {
                text = ReplaceIgnoreCase(text, raw, label);
            }

            text = CreatorTailRegex.Replace(text, string.Empty).Trim();
            text = ShopTailRegex.Replace(text, string.Empty).Trim();
            text = StripAtHandleMentions(text);
            text = StripEmojis(text);
            text = CollapseSpaces(text);
            text = TrimTrailingFluff(text);

            return text;
        }

        public static string NormalizeScriptText(string script, string rawProductName, string shortLabel = null)
        {
            var text = (script ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var raw = (rawProductName ?? string.Empty).Trim();
            var label = shortLabel ?? GetShortLabel(raw);
            if (!string.IsNullOrEmpty(raw) && raw.Length > label.Length + 8)
            {
                text = ReplaceIgnoreCase(text, raw, label);
            }

            text = StripAtHandleMentions(text);
            return CollapseSpaces(text);
        }

        private static string StripAtHandleMentions(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var s = AtHandleTailRegex.Replace(text, string.Empty);
            s = AtHandleTokenRegex.Replace(s, string.Empty);
            return CollapseSpaces(s);
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
            {
                return source ?? string.Empty;
            }

            var idx = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return source;
            }

            return source.Substring(0, idx) + newValue + source.Substring(idx + oldValue.Length);
        }

        private static string StripEmojis(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (char.IsSurrogate(ch))
                {
                    continue;
                }

                var cat = char.GetUnicodeCategory(ch);
                if (cat == System.Globalization.UnicodeCategory.OtherSymbol)
                {
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        private static string CollapseSpaces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text.Trim(), @"\s{2,}", " ");
        }

        private static string TrimTrailingFluff(string text)
        {
            return (text ?? string.Empty).Trim(' ', ',', '.', '!', '…', '-', '–', '—', '|');
        }

        private static string TrimAtWordBoundary(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            {
                return text ?? string.Empty;
            }

            var cut = text.Substring(0, maxLen);
            var lastSpace = cut.LastIndexOf(' ');
            if (lastSpace > maxLen / 2)
            {
                cut = cut.Substring(0, lastSpace);
            }

            return TrimTrailingFluff(cut);
        }
    }
}
