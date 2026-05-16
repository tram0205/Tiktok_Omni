using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Phụ đề timing (SRT) — heuristic / Gemini; dùng cho karaoke hook trong Video reup render.</summary>
    public sealed class CaptionTiming
    {
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public static class VideoReupCaptionService
    {
        public static string SanitizeFileNameFragment(string name)
        {
            var s = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return "video";
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }

            if (s.Length > 80)
            {
                s = s.Substring(0, 80).TrimEnd('_', ' ');
            }

            return string.IsNullOrEmpty(s) ? "video" : s;
        }

        /// <summary>Chia script theo dấu câu, phân bổ thời gian theo độ dài cụm (tối đa 24 dòng).</summary>
        public static List<CaptionTiming> BuildHeuristic(string script, double totalDurationSeconds)
        {
            if (totalDurationSeconds <= 0.2d)
            {
                return new List<CaptionTiming>();
            }

            var phrases = Regex.Split(script ?? string.Empty, @"(?<=[\.\!\?,;:])\s+")
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (phrases.Count == 0)
            {
                return new List<CaptionTiming>();
            }

            var capped = phrases.Take(24).ToList();
            var weightSum = capped.Sum(x => Math.Max(1, x.Length));
            var cursor = 0d;
            var result = new List<CaptionTiming>();
            for (var i = 0; i < capped.Count; i++)
            {
                var lenWeight = Math.Max(1, capped[i].Length);
                var dur = totalDurationSeconds * (lenWeight / Math.Max(1d, weightSum));
                dur = Math.Max(0.8d, dur);
                var start = cursor;
                var end = Math.Min(totalDurationSeconds, start + dur);
                if (i == capped.Count - 1)
                {
                    end = totalDurationSeconds;
                }

                result.Add(new CaptionTiming
                {
                    StartSeconds = Math.Round(start, 2),
                    EndSeconds = Math.Round(Math.Max(start + 0.2d, end), 2),
                    Text = capped[i]
                });
                cursor = end;
            }

            return result;
        }

        /// <summary>Chia câu hook theo thời lượng WAV Lyria (0…hookDuration): Gemini nếu có API key, không thì heuristic.</summary>
        public static async Task<List<CaptionTiming>> BuildHookKaraokeTimingsAsync(
            GeminiService gemini,
            string hookText,
            double hookDurationSeconds,
            string provider,
            string apiKey,
            string model,
            CancellationToken cancellationToken)
        {
            var script = (hookText ?? string.Empty).Trim();
            if (hookDurationSeconds <= 0.25d || string.IsNullOrEmpty(script))
            {
                return new List<CaptionTiming>();
            }

            if (gemini != null && !string.IsNullOrWhiteSpace(apiKey))
            {
                var dur = hookDurationSeconds.ToString("0.##", CultureInfo.InvariantCulture);
                var prompt =
                    "Đoạn sau là câu hook tiếng Việt sẽ được đọc thành tiếng (TTS) trong khoảng " + dur +
                    " giây. Chia thành các cụm từ ngắn (karaoke, hiển thị lần lượt), tối đa 18 dòng.\r\n" +
                    "Mỗi dòng đúng định dạng: start|end|text — start/end là giây thập phân từ 0 đến " + dur +
                    ", text là phần tiếng Việt khớp câu gốc (không thêm lời).\r\n" +
                    "Căn tiến độ đọc tự nhiên, trải đều suốt " + dur + " giây (không dồn hết vào cuối). Không markdown, không giải thích.\r\n" +
                    "Câu hook:\r\n" + script;

                try
                {
                    var raw = await gemini.GenerateScriptAsync(
                        prompt,
                        provider,
                        apiKey,
                        model,
                        cancellationToken).ConfigureAwait(false);
                    var parsed = ParseGeminiLines(raw, hookDurationSeconds);
                    if (parsed.Count > 0)
                    {
                        return parsed;
                    }
                }
                catch
                {
                    // fallback heuristic
                }
            }

            return BuildHeuristic(script, hookDurationSeconds);
        }

        /// <summary>Gọi Gemini (hoặc Claude nếu provider) để lấy dòng dạng start|end|text; lỗi hoặc rỗng thì heuristic.</summary>
        public static async Task<List<CaptionTiming>> BuildWithGeminiAsync(
            GeminiService gemini,
            string script,
            double totalDurationSeconds,
            string provider,
            string apiKey,
            string model,
            CancellationToken cancellationToken)
        {
            if (totalDurationSeconds <= 0.2d || string.IsNullOrWhiteSpace(script))
            {
                return BuildHeuristic(script, totalDurationSeconds);
            }

            var prompt = "Trích xuất timestamp phụ đề cho voice-over tiếng Việt dưới đây. " +
                         "Tổng thời lượng video (ước lượng) là " +
                         totalDurationSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " giây. " +
                         "Trả về tối đa 24 dòng, mỗi dòng đúng định dạng: start|end|text . " +
                         "start/end là giây dạng số thập phân, text là cụm ngắn 3-8 từ. " +
                         "Không markdown, không giải thích. Script: " + script;

            try
            {
                var raw = await gemini.GenerateScriptAsync(
                    prompt,
                    provider,
                    apiKey,
                    model,
                    cancellationToken).ConfigureAwait(false);
                var parsed = ParseGeminiLines(raw, totalDurationSeconds);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
            catch
            {
                // caller may log; still return heuristic
            }

            return BuildHeuristic(script, totalDurationSeconds);
        }

        private static List<CaptionTiming> ParseGeminiLines(string raw, double totalDurationSeconds)
        {
            var output = new List<CaptionTiming>();
            var lines = (raw ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(30)
                .ToList();
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var start))
                {
                    continue;
                }

                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var end))
                {
                    continue;
                }

                var text = parts[2].Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                start = Math.Max(0d, Math.Min(totalDurationSeconds, start));
                end = Math.Max(start + 0.1d, Math.Min(totalDurationSeconds, end));
                if (end - start < 0.1d)
                {
                    continue;
                }

                output.Add(new CaptionTiming
                {
                    StartSeconds = Math.Round(start, 2),
                    EndSeconds = Math.Round(end, 2),
                    Text = text
                });
            }

            return output.OrderBy(x => x.StartSeconds).ToList();
        }

        public static string ToSrtContent(IReadOnlyList<CaptionTiming> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                sb.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
                sb.AppendLine(FormatSrtRange(s.StartSeconds, s.EndSeconds));
                sb.AppendLine((s.Text ?? string.Empty).Replace("\r\n", "\n").Trim());
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatSrtRange(double startSec, double endSec)
        {
            return FormatSrtTimestamp(startSec) + " --> " + FormatSrtTimestamp(endSec);
        }

        private static string FormatSrtTimestamp(double seconds)
        {
            if (seconds < 0d)
            {
                seconds = 0d;
            }

            var ts = TimeSpan.FromSeconds(seconds);
            var ms = (int)Math.Round(ts.TotalMilliseconds);
            var t = TimeSpan.FromMilliseconds(ms);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00},{3:000}",
                (int)t.TotalHours,
                t.Minutes,
                t.Seconds,
                t.Milliseconds);
        }
    }
}
