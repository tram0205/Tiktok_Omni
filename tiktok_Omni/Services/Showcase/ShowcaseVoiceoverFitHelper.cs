using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Căn thoại Showcase theo thời lượng clip — tránh TTS dài hơn slot rồi bị cắt giữa câu.</summary>
    internal static class ShowcaseVoiceoverFitHelper
    {
        /// <summary>Ước lượng tốc độ đọc TTS tiếng Việt (từ/giây) — hơi bảo thủ để không tràn slot.</summary>
        public const double WordsPerSecond = 2.35;

        /// <summary>Chừa đuôi im lặng ngắn trong slot.</summary>
        public const double SlotPaddingSeconds = 0.25;

        private static readonly Regex SentenceSplitRegex = new Regex(
            @"(?<=[\.!\?…])\s+",
            RegexOptions.Compiled);

        public static int EstimateMaxWords(double slotSeconds)
        {
            var usable = Math.Max(0.5d, slotSeconds - SlotPaddingSeconds);
            return Math.Max(3, (int)Math.Floor(usable * WordsPerSecond));
        }

        public static string MergePassage(IEnumerable<string> parts)
        {
            if (parts == null)
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                parts
                    .Select(p => (p ?? string.Empty).Trim())
                    .Where(p => p.Length > 0));
        }

        /// <summary>Hook — giữ 1 câu trong giới hạn slot.</summary>
        public static string TrimToSlotDuration(string text, double slotSeconds, out bool trimmed)
        {
            trimmed = false;
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            var maxWords = EstimateMaxWords(slotSeconds);
            var sentences = SplitSentences(line);
            var primary = sentences.Count > 0 ? sentences[0] : line;
            if (sentences.Count > 1)
            {
                trimmed = true;
            }

            var words = CountWords(primary);
            if (words > maxWords)
            {
                trimmed = true;
                primary = TruncateAtWordBoundary(primary, maxWords);
            }

            return primary.Trim();
        }

        /// <summary>Thân + CTA — giữ nhiều câu liền mạch, chỉ cắt đuôi nếu quá dài so với slot.</summary>
        public static string TrimPassageToDuration(string text, double slotSeconds, out bool trimmed)
        {
            trimmed = false;
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            var maxWords = EstimateMaxWords(slotSeconds);
            var sentences = SplitSentences(line);
            if (sentences.Count == 0)
            {
                sentences = new List<string> { line };
            }

            var kept = new List<string>();
            var wordCount = 0;
            foreach (var sentence in sentences)
            {
                var sentenceWords = CountWords(sentence);
                if (wordCount + sentenceWords <= maxWords)
                {
                    kept.Add(sentence);
                    wordCount += sentenceWords;
                    continue;
                }

                if (kept.Count == 0)
                {
                    kept.Add(TruncateAtWordBoundary(sentence, maxWords));
                    wordCount = CountWords(kept[0]);
                }

                trimmed = true;
                break;
            }

            if (kept.Count < sentences.Count)
            {
                trimmed = true;
            }

            return string.Join(" ", kept).Trim();
        }

        public static IReadOnlyList<string> SplitSentences(string text)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return Array.Empty<string>();
            }

            return SentenceSplitRegex.Split(line)
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        public static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return text.Trim()
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }

        private static string TruncateAtWordBoundary(string text, int maxWords)
        {
            if (maxWords <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var parts = text.Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= maxWords)
            {
                return text.Trim();
            }

            var slice = string.Join(" ", parts.Take(maxWords));
            if (!slice.EndsWith(".", StringComparison.Ordinal)
                && !slice.EndsWith("!", StringComparison.Ordinal)
                && !slice.EndsWith("?", StringComparison.Ordinal)
                && !slice.EndsWith("…", StringComparison.Ordinal))
            {
                slice += ".";
            }

            return slice;
        }

        public static string FormatMaxWordsHint(double slotSeconds)
        {
            return EstimateMaxWords(slotSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }
}
