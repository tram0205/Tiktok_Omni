using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Tránh CTA bị lặp — cta_text chỉ là overlay trích từ đuôi voiceover cảnh cuối.</summary>
    internal static class ShowcaseCtaDedupHelper
    {
        public const int MaxOverlayWords = 18;

        private static readonly Regex CompareCleanupRegex = new Regex(
            @"[^\p{L}\p{N}\s?]",
            RegexOptions.Compiled);

        public static string NormalizeScriptCta(IList<AiVideoGenInputItem> orderedScenes, string ctaText)
        {
            ctaText = (ctaText ?? string.Empty).Trim();
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                return TrimToOverlayWords(ctaText);
            }

            var lastVoiceover = GetLastNonSilentVoiceover(orderedScenes);
            if (string.IsNullOrEmpty(lastVoiceover))
            {
                return TrimToOverlayWords(ctaText);
            }

            if (string.IsNullOrEmpty(ctaText))
            {
                return HasCtaLikeEnding(lastVoiceover)
                    ? TrimToOverlayWords(ExtractLastSentence(lastVoiceover))
                    : string.Empty;
            }

            if (PassageContainsCta(lastVoiceover, ctaText))
            {
                return TrimToOverlayWords(ctaText);
            }

            if (HasCtaLikeEnding(lastVoiceover))
            {
                return TrimToOverlayWords(ExtractLastSentence(lastVoiceover));
            }

            return TrimToOverlayWords(ctaText);
        }

        public static bool ShouldAppendCtaToBody(IEnumerable<string> bodyParts, string ctaText)
        {
            ctaText = (ctaText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ctaText))
            {
                return false;
            }

            var merged = ShowcaseVoiceoverFitHelper.MergePassage(bodyParts);
            return !PassageContainsCta(merged, ctaText);
        }

        public static bool PassageContainsCta(string passage, string ctaText)
        {
            ctaText = (ctaText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ctaText))
            {
                return true;
            }

            passage = (passage ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(passage))
            {
                return false;
            }

            var normPassage = NormalizeCompare(passage);
            var normCta = NormalizeCompare(ctaText);
            if (normCta.Length == 0)
            {
                return true;
            }

            if (normPassage.Contains(normCta))
            {
                return true;
            }

            var lastSentence = ExtractLastSentence(passage);
            var normLast = NormalizeCompare(lastSentence);
            if (normLast.Contains(normCta) || normCta.Contains(normLast))
            {
                return true;
            }

            return WordOverlapRatio(normLast, normCta) >= 0.65d;
        }

        private static string GetLastNonSilentVoiceover(IList<AiVideoGenInputItem> orderedScenes)
        {
            for (var i = orderedScenes.Count - 1; i >= 0; i--)
            {
                var scene = orderedScenes[i];
                if (scene == null || scene.ShowcaseSceneSilent)
                {
                    continue;
                }

                var voiceover = (scene.SceneVoiceover ?? string.Empty).Trim();
                if (voiceover.Length > 0)
                {
                    return voiceover;
                }
            }

            return string.Empty;
        }

        private static string ExtractLastSentence(string text)
        {
            var sentences = ShowcaseVoiceoverFitHelper.SplitSentences(text);
            if (sentences.Count == 0)
            {
                return (text ?? string.Empty).Trim();
            }

            return sentences[sentences.Count - 1].Trim();
        }

        private static bool HasCtaLikeEnding(string text)
        {
            var line = (text ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                return false;
            }

            if (line.EndsWith("?", StringComparison.Ordinal)
                || line.EndsWith("?", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var last = NormalizeCompare(ExtractLastSentence(line));
            if (last.Length == 0)
            {
                return false;
            }

            var markers = new[]
            {
                "nè", "nghen", "hen", "nha", "nha", "comment", "binh luan", "follow", "luu lai",
                "luu video", "nhan tin", "nhan minh", "nhan tui", "chon mau", "chon mau nao",
                "theo ban", "ban chon", "ai can", "thich mau nao"
            };

            return markers.Any(marker => last.IndexOf(marker, StringComparison.Ordinal) >= 0);
        }

        private static string NormalizeCompare(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var cleaned = CompareCleanupRegex.Replace(text.Trim().ToLowerInvariant(), " ");
            return Regex.Replace(cleaned, @"\s+", " ").Trim();
        }

        private static double WordOverlapRatio(string left, string right)
        {
            var leftWords = SplitWords(left);
            var rightWords = SplitWords(right);
            if (leftWords.Count == 0 || rightWords.Count == 0)
            {
                return 0d;
            }

            var overlap = leftWords.Intersect(rightWords).Count();
            var denominator = Math.Min(leftWords.Count, rightWords.Count);
            return denominator <= 0 ? 0d : overlap / (double)denominator;
        }

        private static HashSet<string> SplitWords(string text)
        {
            return new HashSet<string>(
                (text ?? string.Empty)
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 1));
        }

        private static string TrimToOverlayWords(string text)
        {
            var line = (text ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                return line;
            }

            var words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= MaxOverlayWords)
            {
                return line;
            }

            return string.Join(" ", words.Take(MaxOverlayWords)).Trim();
        }
    }
}
