using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Sinh file ASS karaoke với hiệu ứng pop từng từ.</summary>
    public static class AssSubtitleGenerator
    {
        private const string PopStyleName = "PopStyle";
        // ASS override tags use {…}; double braces so string.Format treats them as literals.
        private const string PopTagTemplate = @"{{\fscx150\fscy150\t({0},{1},\fscx100\fscy100)}}";

        /// <summary>Ghi file ASS karaoke pop-up (UTF-8).</summary>
        public static void GenerateAssFile(
            IReadOnlyList<WordTimestamp> words,
            string outputPath,
            AssSubtitleGeneratorOptions options = null)
        {
            WriteAssFile(outputPath, words, options);
        }

        /// <summary>Ghi file ASS từ các segment transcript (tương thích FFmpeg <c>subtitles</c> filter).</summary>
        public static void GenerateAssFileFromSegments(
            IReadOnlyList<TranscriptSegment> segments,
            string outputPath,
            AssSubtitleGeneratorOptions options = null)
        {
            WriteAssFileFromSegments(outputPath, segments, options);
        }

        public static void WriteAssFileFromSegments(
            string outputPath,
            IReadOnlyList<TranscriptSegment> segments,
            AssSubtitleGeneratorOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Đường dẫn ASS không hợp lệ.", nameof(outputPath));
            }

            if (segments == null || segments.Count == 0)
            {
                throw new ArgumentException("Danh sách TranscriptSegment trống.", nameof(segments));
            }

            var body = BuildAssContentFromSegments(segments, options);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, body, TextFileEncoding.Utf8NoBom);
        }

        public static string BuildAssContentFromSegments(
            IReadOnlyList<TranscriptSegment> segments,
            AssSubtitleGeneratorOptions options = null)
        {
            if (segments == null || segments.Count == 0)
            {
                throw new ArgumentException("Danh sách TranscriptSegment trống.", nameof(segments));
            }

            var opt = options ?? new AssSubtitleGeneratorOptions();
            var sb = new StringBuilder();
            AppendAssHeader(sb, opt);

            foreach (var segment in segments)
            {
                if (segment == null)
                {
                    continue;
                }

                var text = (segment.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var startMs = Math.Max(0d, segment.StartTimeMs);
                var endMs = segment.EndTimeMs > startMs ? segment.EndTimeMs : startMs + 500d;

                sb.AppendLine(
                    "Dialogue: 0," +
                    FormatAssTime(startMs) + "," +
                    FormatAssTime(endMs) +
                    "," + PopStyleName + ",,0,0,0,," + EscapeAssText(text));
            }

            return sb.ToString();
        }

        /// <summary>Chuyển segment thành <see cref="WordTimestamp"/> (một từ/block mỗi segment).</summary>
        public static List<WordTimestamp> ToWordTimestamps(IReadOnlyList<TranscriptSegment> segments)
        {
            var result = new List<WordTimestamp>();
            if (segments == null)
            {
                return result;
            }

            foreach (var segment in segments)
            {
                if (segment == null)
                {
                    continue;
                }

                var text = (segment.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var startMs = Math.Max(0d, segment.StartTimeMs);
                var endMs = segment.EndTimeMs > startMs ? segment.EndTimeMs : startMs + 500d;
                result.Add(new WordTimestamp
                {
                    Text = text,
                    StartTimeMs = startMs,
                    EndTimeMs = endMs
                });
            }

            return result;
        }

        public static void WriteAssFile(
            string outputPath,
            IReadOnlyList<WordTimestamp> wordTimestamps,
            AssSubtitleGeneratorOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Đường dẫn ASS không hợp lệ.", nameof(outputPath));
            }

            if (wordTimestamps == null || wordTimestamps.Count == 0)
            {
                throw new ArgumentException("Danh sách WordTimestamp trống.", nameof(wordTimestamps));
            }

            var opt = options ?? new AssSubtitleGeneratorOptions();
            var body = BuildAssContent(wordTimestamps, opt);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, body, TextFileEncoding.Utf8NoBom);
        }

        public static string BuildAssContent(
            IReadOnlyList<WordTimestamp> wordTimestamps,
            AssSubtitleGeneratorOptions options = null,
            int? wordsPerLineOverride = null)
        {
            var opt = options ?? new AssSubtitleGeneratorOptions();
            var sb = new StringBuilder();
            AppendAssHeader(sb, opt);

            foreach (var chunk in GroupIntoLines(wordTimestamps, opt, wordsPerLineOverride))
            {
                if (chunk.Count == 0)
                {
                    continue;
                }

                var lineStartMs = chunk[0].StartTimeMs;
                var lineEndMs = chunk[chunk.Count - 1].EndTimeMs;
                var text = BuildChunkDialogueText(chunk, lineStartMs, opt.Animation);
                sb.AppendLine(
                    "Dialogue: 0," +
                    FormatAssTime(lineStartMs) + "," +
                    FormatAssTime(lineEndMs) +
                    "," + PopStyleName + ",,0,0,0,," + text);
            }

            return sb.ToString();
        }

        private static IReadOnlyList<List<WordTimestamp>> GroupIntoLines(
            IReadOnlyList<WordTimestamp> wordTimestamps,
            AssSubtitleGeneratorOptions opt,
            int? wordsPerLineOverride = null)
        {
            if (wordTimestamps == null || wordTimestamps.Count == 0)
            {
                return Array.Empty<List<WordTimestamp>>();
            }

            var maxWords = wordsPerLineOverride ?? Math.Max(4, Math.Min(12, opt.WordsPerLine));
            if (opt.RhythmicLineBreaks)
            {
                return GroupIntoRhythmicLines(wordTimestamps, maxWords);
            }

            var lines = new List<List<WordTimestamp>>();
            for (var chunkStart = 0; chunkStart < wordTimestamps.Count; chunkStart += maxWords)
            {
                var chunkCount = Math.Min(maxWords, wordTimestamps.Count - chunkStart);
                var chunk = new List<WordTimestamp>(chunkCount);
                for (var i = 0; i < chunkCount; i++)
                {
                    chunk.Add(wordTimestamps[chunkStart + i]);
                }

                if (chunk.Count > 0)
                {
                    lines.Add(chunk);
                }
            }

            return lines;
        }

        private const double RhythmPauseGapMs = 350d;

        private static List<List<WordTimestamp>> GroupIntoRhythmicLines(
            IReadOnlyList<WordTimestamp> words,
            int maxWordsPerLine)
        {
            var lines = new List<List<WordTimestamp>>();
            var current = new List<WordTimestamp>();

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (current.Count > 0)
                {
                    var prev = current[current.Count - 1];
                    if (word.StartTimeMs - prev.EndTimeMs >= RhythmPauseGapMs)
                    {
                        lines.Add(current);
                        current = new List<WordTimestamp>();
                    }
                }

                current.Add(word);
                if (ShouldBreakLineAfterWord(GetWordText(word), current.Count, maxWordsPerLine))
                {
                    lines.Add(current);
                    current = new List<WordTimestamp>();
                }
            }

            if (current.Count > 0)
            {
                lines.Add(current);
            }

            return lines;
        }

        private static bool ShouldBreakLineAfterWord(string wordText, int wordsInLine, int maxWordsPerLine)
        {
            if (wordsInLine <= 0)
            {
                return false;
            }

            if (wordsInLine >= maxWordsPerLine)
            {
                return true;
            }

            var w = (wordText ?? string.Empty).TrimEnd();
            if (w.EndsWith("...", StringComparison.Ordinal) || w.EndsWith("…", StringComparison.Ordinal))
            {
                return true;
            }

            if (w.Length == 0)
            {
                return false;
            }

            var last = w[w.Length - 1];
            if (last == '.' || last == '!' || last == '?' || last == ';' || last == ':')
            {
                return true;
            }

            return last == ',' && wordsInLine >= 3;
        }

        private static void AppendAssHeader(StringBuilder sb, AssSubtitleGeneratorOptions opt)
        {
            sb.AppendLine("[Script Info]");
            sb.AppendLine("Title: Karaoke ASS");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine("PlayResX: " + opt.PlayResX.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("PlayResY: " + opt.PlayResY.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("WrapStyle: 0");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            var primary = string.IsNullOrWhiteSpace(opt.PrimaryColourAss) ? "&H00FFFFFF" : opt.PrimaryColourAss.Trim();
            var secondary = string.IsNullOrWhiteSpace(opt.SecondaryColourAss) ? "&H000000FF" : opt.SecondaryColourAss.Trim();
            sb.AppendLine(
                "Style: " + PopStyleName + "," + opt.FontName + "," + opt.FontSize.ToString(CultureInfo.InvariantCulture) +
                "," + primary + "," + secondary + ",&H00000000,&H80000000," +
                (opt.Bold ? "1" : "0") + "," + (opt.Italic ? "1" : "0") + ",0,0,100,100,0,0,1,10,2," +
                opt.Alignment.ToString(CultureInfo.InvariantCulture) + ",60,60," +
                opt.MarginV.ToString(CultureInfo.InvariantCulture) + ",1");
            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        }

        private static string BuildChunkDialogueText(
            IReadOnlyList<WordTimestamp> chunk,
            double lineStartMs,
            ReupKaraokeAnimationMode animation)
        {
            if (animation == ReupKaraokeAnimationMode.Plain)
            {
                var plainParts = new List<string>(chunk.Count);
                for (var i = 0; i < chunk.Count; i++)
                {
                    plainParts.Add(EscapeAssText(GetWordText(chunk[i])));
                }

                return string.Join(" ", plainParts);
            }

            if (animation == ReupKaraokeAnimationMode.Highlight)
            {
                var highlightParts = new List<string>(chunk.Count);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var word = chunk[i];
                    var durCs = (int)Math.Max(1, Math.Round((word.EndTimeMs - word.StartTimeMs) / 10d));
                    highlightParts.Add("{\\k" + durCs.ToString(CultureInfo.InvariantCulture) + "}" + EscapeAssText(GetWordText(word)));
                }

                return string.Join(" ", highlightParts);
            }

            if (animation == ReupKaraokeAnimationMode.FadeIn)
            {
                var fadeParts = new List<string>(chunk.Count);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var word = chunk[i];
                    var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                    var fadeEndMs = offsetMs + 180;
                    var fadeTag = string.Format(
                        CultureInfo.InvariantCulture,
                        @"{{\alpha&HFF&\t({0},{1},\alpha&H00&)}}",
                        offsetMs,
                        fadeEndMs);
                    fadeParts.Add(fadeTag + EscapeAssText(GetWordText(word)));
                }

                return string.Join(" ", fadeParts);
            }

            var parts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                var popEndMs = offsetMs + 100;
                var popTag = string.Format(CultureInfo.InvariantCulture, PopTagTemplate, offsetMs, popEndMs);
                parts.Add(popTag + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", parts);
        }

        private static string FormatAssTime(double milliseconds)
        {
            var ms = Math.Max(0d, milliseconds);
            var hours = (int)(ms / 3600000d);
            ms -= hours * 3600000d;
            var minutes = (int)(ms / 60000d);
            ms -= minutes * 60000d;
            var seconds = (int)(ms / 1000d);
            ms -= seconds * 1000d;
            var centis = (int)Math.Round(ms / 10d);
            if (centis >= 100)
            {
                centis = 99;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:D2}:{2:D2}.{3:D2}",
                hours,
                minutes,
                seconds,
                centis);
        }

        private static string GetWordText(WordTimestamp word)
        {
            if (word == null)
            {
                return string.Empty;
            }

            return (word.Text ?? word.Word ?? string.Empty).Trim();
        }

        private static string EscapeAssText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }

    public sealed class AssSubtitleGeneratorOptions
    {
        public int WordsPerLine { get; set; } = 6;

        /// <summary>Ngắt dòng theo dấu câu / khoảng dừng thay vì cố định N từ.</summary>
        public bool RhythmicLineBreaks { get; set; }

        public int PlayResX { get; set; } = 1080;

        public int PlayResY { get; set; } = 1920;

        public string FontName { get; set; } = "Segoe UI Bold";

        public int FontSize { get; set; } = 72;

        public int MarginV { get; set; } = 120;

        /// <summary>ASS alignment (2=dưới giữa, 5=giữa màn, 8=trên giữa).</summary>
        public int Alignment { get; set; } = 2;

        public bool Bold { get; set; } = true;

        public bool Italic { get; set; }

        public ReupKaraokeAnimationMode Animation { get; set; } = ReupKaraokeAnimationMode.Pop;

        /// <summary>ASS PrimaryColour tùy chỉnh (ví dụ &amp;H00FFFFFF).</summary>
        public string PrimaryColourAss { get; set; }

        /// <summary>ASS SecondaryColour — màu tô khi karaoke highlight.</summary>
        public string SecondaryColourAss { get; set; }
    }
}
