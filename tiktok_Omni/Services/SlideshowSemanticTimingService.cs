using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Khớp thời lượng hiển thị ảnh Slideshow với lời thoại (Whisper / ước lượng).</summary>
    public static class SlideshowSemanticTimingService
    {
        private static readonly Regex NormalizeRegex = new Regex(@"[^\p{L}\p{N}\s]+", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        public static async Task<IReadOnlyList<WordTimestamp>> TryGetWordTimestampsAsync(
            string ffmpegExecutablePath,
            string audioFilePath,
            string openAiApiKey,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
            {
                return Array.Empty<WordTimestamp>();
            }

            var apiKey = (openAiApiKey ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    log?.Invoke("[Slideshow] Đang bóc băng Whisper (semantic sync)...");
                    var whisper = new WhisperTranscriptionService();
                    var words = await whisper.GetWordTimestampsAsync(audioFilePath, apiKey, cancellationToken)
                        .ConfigureAwait(false);
                    if (words != null && words.Count > 0)
                    {
                        log?.Invoke("[Slideshow] Whisper semantic: " + words.Count + " từ.");
                        return words;
                    }

                    log?.Invoke("[Slideshow] Whisper không trả word timestamps — thử ước lượng từ script.");
                }
                catch (Exception ex)
                {
                    log?.Invoke("[Slideshow] Lỗi gọi API Whisper (semantic): " + ex.Message);
                }
            }

            return Array.Empty<WordTimestamp>();
        }

        /// <summary>
        /// Trả về thời lượng (giây) cho từng ảnh. Fallback chia đều nếu không khớp được mốc thời gian.
        /// </summary>
        public static IList<double> ResolvePerImageDurationsSeconds(
            int imageCount,
            double audioDurationSeconds,
            IReadOnlyList<WordTimestamp> wordTimestamps,
            IList<string> segmentScripts,
            Action<string> log)
        {
            if (imageCount <= 0)
            {
                return Array.Empty<double>();
            }

            var fallback = BuildEqualDurations(imageCount, audioDurationSeconds);
            if (wordTimestamps == null || wordTimestamps.Count == 0)
            {
                log?.Invoke("[Slideshow] Semantic sync: không có word timestamps → chia đều " +
                            fallback.Count + " clip.");
                return fallback;
            }

            var segments = NormalizeSegmentScripts(segmentScripts, imageCount);
            if (segments.Count != imageCount)
            {
                log?.Invoke("[Slideshow] Semantic sync: số đoạn script (" + segments.Count +
                            ") ≠ số ảnh (" + imageCount + ") → chia đều.");
                return fallback;
            }

            try
            {
                var matched = TryMatchSegmentDurations(wordTimestamps, segments, audioDurationSeconds, log);
                if (matched != null && matched.Count == imageCount && IsDurationSetValid(matched, audioDurationSeconds))
                {
                    log?.Invoke("[Slideshow] Semantic sync OK: " + string.Join(", ",
                        matched.Select(d => d.ToString("0.##", CultureInfo.InvariantCulture) + "s")));
                    return matched;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[Slideshow] Semantic sync lỗi khớp mốc: " + ex.Message);
            }

            log?.Invoke("[Slideshow] Semantic sync: fallback chia đều thời lượng.");
            return fallback;
        }

        public static List<string> BuildSegmentScriptsForImages(
            int imageCount,
            string fullScript,
            IList<string> perProductScripts)
        {
            if (imageCount <= 0)
            {
                return new List<string>();
            }

            if (perProductScripts != null &&
                perProductScripts.Count == imageCount &&
                perProductScripts.All(x => !string.IsNullOrWhiteSpace(x)))
            {
                return perProductScripts.Select(x => x.Trim()).ToList();
            }

            var text = (fullScript ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return Enumerable.Repeat(string.Empty, imageCount).ToList();
            }

            var byMarker = text.Split(new[] { "\n---\n", "---" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (byMarker.Count == imageCount)
            {
                return byMarker;
            }

            var byParagraph = text.Replace("\r\n", "\n")
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (byParagraph.Count == imageCount)
            {
                return byParagraph;
            }

            return SplitScriptIntoWordChunks(text, imageCount);
        }

        private static List<string> NormalizeSegmentScripts(IList<string> segmentScripts, int imageCount)
        {
            if (segmentScripts == null || segmentScripts.Count == 0)
            {
                return Enumerable.Repeat(string.Empty, imageCount).ToList();
            }

            var list = segmentScripts.Select(x => (x ?? string.Empty).Trim()).ToList();
            while (list.Count < imageCount)
            {
                list.Add(string.Empty);
            }

            if (list.Count > imageCount)
            {
                list = list.Take(imageCount).ToList();
            }

            return list;
        }

        private static List<double> TryMatchSegmentDurations(
            IReadOnlyList<WordTimestamp> words,
            IReadOnlyList<string> segments,
            double audioDurationSeconds,
            Action<string> log)
        {
            var tokens = words
                .Select(w => NormalizeToken(GetWordText(w)))
                .Where(t => t.Length > 0)
                .ToList();
            if (tokens.Count == 0)
            {
                return null;
            }

            var timings = words.Where(w => !string.IsNullOrWhiteSpace(GetWordText(w))).ToList();
            if (timings.Count == 0)
            {
                return null;
            }

            var durations = new List<double>(segments.Count);
            var cursor = 0;
            for (var segIndex = 0; segIndex < segments.Count; segIndex++)
            {
                var segTokens = Tokenize(segments[segIndex]);
                if (segTokens.Count == 0)
                {
                    durations.Add(0d);
                    continue;
                }

                var startIndex = FindBestSequenceStart(tokens, segTokens, cursor);
                if (startIndex < 0)
                {
                    log?.Invoke("[Slideshow] Semantic: không khớp đoạn " + (segIndex + 1) + " («" +
                                Truncate(segments[segIndex], 40) + "»).");
                    return null;
                }

                var endIndex = Math.Min(tokens.Count - 1, startIndex + Math.Max(segTokens.Count - 1, 0));
                if (segIndex + 1 < segments.Count)
                {
                    var nextTokens = Tokenize(segments[segIndex + 1]);
                    if (nextTokens.Count > 0)
                    {
                        var nextStart = FindBestSequenceStart(tokens, nextTokens, startIndex + 1);
                        if (nextStart > startIndex)
                        {
                            endIndex = nextStart - 1;
                        }
                    }
                }

                var startMs = timings[Math.Min(startIndex, timings.Count - 1)].StartTimeMs;
                var endMs = timings[Math.Min(endIndex, timings.Count - 1)].EndTimeMs;
                var durationSec = Math.Max(0.2d, (endMs - startMs) / 1000d);
                durations.Add(durationSec);
                cursor = endIndex + 1;
            }

            ApplyDurationBounds(durations);
            NormalizeDurationSum(durations, audioDurationSeconds);
            return durations;
        }

        private static int FindBestSequenceStart(IReadOnlyList<string> haystack, IReadOnlyList<string> needle, int fromIndex)
        {
            if (needle.Count == 0 || haystack.Count == 0)
            {
                return -1;
            }

            var maxStart = haystack.Count - needle.Count;
            if (maxStart < fromIndex)
            {
                return -1;
            }

            var bestIndex = -1;
            var bestScore = 0;
            for (var i = fromIndex; i <= maxStart; i++)
            {
                var score = 0;
                for (var j = 0; j < needle.Count; j++)
                {
                    if (string.Equals(haystack[i + j], needle[j], StringComparison.OrdinalIgnoreCase))
                    {
                        score += 2;
                    }
                    else if (haystack[i + j].IndexOf(needle[j], StringComparison.OrdinalIgnoreCase) >= 0 ||
                             needle[j].IndexOf(haystack[i + j], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += 1;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestScore >= Math.Max(2, needle.Count) ? bestIndex : -1;
        }

        private static void ApplyDurationBounds(IList<double> durations)
        {
            for (var i = 0; i < durations.Count; i++)
            {
                durations[i] = Math.Max(2.5d, Math.Min(12d, durations[i]));
            }
        }

        private static void NormalizeDurationSum(IList<double> durations, double audioDurationSeconds)
        {
            if (durations.Count == 0)
            {
                return;
            }

            var target = audioDurationSeconds > 0.5d ? audioDurationSeconds : durations.Sum();
            if (target < durations.Count * 2.5d)
            {
                target = durations.Count * 3.2d;
            }

            var sum = durations.Sum();
            if (sum <= 0.01d)
            {
                return;
            }

            var scale = target / sum;
            for (var i = 0; i < durations.Count; i++)
            {
                durations[i] = Math.Round(Math.Max(2.5d, durations[i] * scale), 2);
            }

            var delta = Math.Round(target - durations.Sum(), 2);
            if (Math.Abs(delta) >= 0.05d)
            {
                durations[durations.Count - 1] = Math.Max(2.5d, Math.Round(durations[durations.Count - 1] + delta, 2));
            }
        }

        private static bool IsDurationSetValid(IReadOnlyList<double> durations, double audioDurationSeconds)
        {
            if (durations == null || durations.Count == 0 || durations.Any(d => d < 2d || d > 15d))
            {
                return false;
            }

            var sum = durations.Sum();
            if (audioDurationSeconds > 1d)
            {
                var ratio = sum / audioDurationSeconds;
                return ratio >= 0.55d && ratio <= 1.45d;
            }

            return sum >= durations.Count * 2d;
        }

        private static IList<double> BuildEqualDurations(int imageCount, double audioDurationSeconds)
        {
            var per = ResolveEqualPerImageDuration(audioDurationSeconds, imageCount);
            return Enumerable.Repeat(per, imageCount).ToList();
        }

        private static double ResolveEqualPerImageDuration(double audioDurationSeconds, int imageCount)
        {
            if (imageCount <= 0)
            {
                return 4.2d;
            }

            if (audioDurationSeconds <= 0.1d)
            {
                return 4.2d;
            }

            var perImage = audioDurationSeconds / imageCount;
            perImage = Math.Max(3.2d, perImage);
            perImage = Math.Min(7.5d, perImage);
            return Math.Round(perImage, 2);
        }

        private static List<string> SplitScriptIntoWordChunks(string script, int chunkCount)
        {
            var words = Tokenize(script);
            if (words.Count == 0)
            {
                return Enumerable.Repeat(string.Empty, chunkCount).ToList();
            }

            var result = new List<string>(chunkCount);
            var index = 0;
            for (var c = 0; c < chunkCount; c++)
            {
                var remainingChunks = chunkCount - c;
                var remainingWords = words.Count - index;
                var take = Math.Max(1, (int)Math.Ceiling(remainingWords / (double)remainingChunks));
                take = Math.Min(take, remainingWords);
                result.Add(string.Join(" ", words.Skip(index).Take(take)));
                index += take;
            }

            return result;
        }

        private static IReadOnlyList<string> Tokenize(string text)
        {
            return WhitespaceRegex
                .Split(NormalizeForDisplay(text))
                .Where(t => t.Length > 0)
                .Select(NormalizeToken)
                .Where(t => t.Length > 0)
                .ToList();
        }

        private static string NormalizeToken(string token)
        {
            return NormalizeRegex.Replace((token ?? string.Empty).ToLowerInvariant(), string.Empty).Trim();
        }

        private static string NormalizeForDisplay(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        }

        private static string GetWordText(WordTimestamp word)
        {
            return (word?.Text ?? word?.Word ?? string.Empty).Trim();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, max) + "...";
        }
    }
}
