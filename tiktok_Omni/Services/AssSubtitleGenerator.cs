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
        private const string PopTagTemplate = @"{\fscx150\fscy150\t({0},{1},\fscx100\fscy100)}";

        /// <summary>Ghi file ASS karaoke pop-up (UTF-8).</summary>
        public static void GenerateAssFile(
            IReadOnlyList<WordTimestamp> words,
            string outputPath,
            AssSubtitleGeneratorOptions options = null)
        {
            WriteAssFile(outputPath, words, options);
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
            var wordsPerLine = Math.Max(5, Math.Min(7, opt.WordsPerLine));
            var body = BuildAssContent(wordTimestamps, opt, wordsPerLine);
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
            var wordsPerLine = wordsPerLineOverride ?? Math.Max(5, Math.Min(7, opt.WordsPerLine));
            var sb = new StringBuilder();
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
            sb.AppendLine(
                "Style: " + PopStyleName + "," + opt.FontName + "," + opt.FontSize.ToString(CultureInfo.InvariantCulture) +
                ",&H00FFFFFF,&H000000FF,&H00000000,&H80000000,1,0,0,0,100,100,0,0,1,10,2,2,60,60," +
                opt.MarginV.ToString(CultureInfo.InvariantCulture) + ",1");
            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            for (var chunkStart = 0; chunkStart < wordTimestamps.Count; chunkStart += wordsPerLine)
            {
                var chunkCount = Math.Min(wordsPerLine, wordTimestamps.Count - chunkStart);
                var chunk = new List<WordTimestamp>(chunkCount);
                for (var i = 0; i < chunkCount; i++)
                {
                    chunk.Add(wordTimestamps[chunkStart + i]);
                }

                if (chunk.Count == 0)
                {
                    continue;
                }

                var lineStartMs = chunk[0].StartTimeMs;
                var lineEndMs = chunk[chunk.Count - 1].EndTimeMs;
                var text = BuildChunkDialogueText(chunk, lineStartMs);
                sb.AppendLine(
                    "Dialogue: 0," +
                    FormatAssTime(lineStartMs) + "," +
                    FormatAssTime(lineEndMs) +
                    "," + PopStyleName + ",,0,0,0,," + text);
            }

            return sb.ToString();
        }

        private static string BuildChunkDialogueText(IReadOnlyList<WordTimestamp> chunk, double lineStartMs)
        {
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

        public int PlayResX { get; set; } = 1080;

        public int PlayResY { get; set; } = 1920;

        public string FontName { get; set; } = "Segoe UI Bold";

        public int FontSize { get; set; } = 72;

        public int MarginV { get; set; } = 120;
    }
}
