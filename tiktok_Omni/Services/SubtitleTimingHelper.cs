using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Đọc thời lượng audio (ffprobe) và ước lượng timestamp từng từ khi TTS không trả về alignment.</summary>
    public static class SubtitleTimingHelper
    {
        private static readonly Regex WordSplitRegex = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>Đọc tổng thời lượng file audio (ms) bằng ffprobe.</summary>
        public static async Task<double> GetAudioDurationMsAsync(
            string ffmpegExecutablePath,
            string audioFilePath,
            CancellationToken cancellationToken = default)
        {
            var seconds = await ProbeMediaDurationSecondsAsync(ffmpegExecutablePath, audioFilePath, cancellationToken)
                .ConfigureAwait(false);
            return Math.Max(0d, seconds * 1000d);
        }

        /// <summary>
        /// Chia đều <paramref name="totalAudioDurationMs"/> cho từng từ trong <paramref name="text"/>.
        /// </summary>
        public static List<WordTimestamp> EstimateWordTimestamps(string text, double totalAudioDurationMs)
        {
            var words = SplitWords(text);
            if (words.Count == 0)
            {
                return new List<WordTimestamp>();
            }

            var totalMs = Math.Max(1d, totalAudioDurationMs);
            var perWordMs = totalMs / words.Count;
            var result = new List<WordTimestamp>(words.Count);
            for (var i = 0; i < words.Count; i++)
            {
                var start = i * perWordMs;
                var end = i == words.Count - 1 ? totalMs : (i + 1) * perWordMs;
                result.Add(new WordTimestamp
                {
                    Text = words[i],
                    StartTimeMs = Math.Round(start, 2),
                    EndTimeMs = Math.Round(end, 2)
                });
            }

            return result;
        }

        public static IReadOnlyList<string> SplitWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return WordSplitRegex
                .Split(text.Trim())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToList();
        }

        public static async Task<double> ProbeMediaDurationSecondsAsync(
            string ffmpegExecutablePath,
            string mediaPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
            {
                throw new FileNotFoundException("Không tìm thấy file media để đo thời lượng.", mediaPath ?? string.Empty);
            }

            var ffprobe = ResolveFfprobePath(ffmpegExecutablePath);
            if (!string.Equals(ffprobe, "ffprobe", StringComparison.OrdinalIgnoreCase) && !File.Exists(ffprobe))
            {
                throw new InvalidOperationException(
                    "Không tìm thấy ffprobe.exe — cấu hình FFmpeg Path trỏ tới ffmpeg.exe (ffprobe.exe cùng thư mục).");
            }

            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + mediaPath + "\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 80).ConfigureAwait(false);
                var output = (await outputTask.ConfigureAwait(false) ?? string.Empty).Trim();
                if (double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    return Math.Max(0d, seconds);
                }
            }

            return 0d;
        }

        public static string ResolveFfprobePath(string ffmpegPath)
        {
            var trimmed = (ffmpegPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return "ffprobe";
            }

            if (File.Exists(trimmed))
            {
                var dir = Path.GetDirectoryName(trimmed);
                var name = Path.GetFileName(trimmed);
                if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name))
                {
                    var probeName = name.StartsWith("ffmpeg", StringComparison.OrdinalIgnoreCase)
                        ? "ffprobe" + name.Substring("ffmpeg".Length)
                        : "ffprobe.exe";
                    var candidate = Path.Combine(dir, probeName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return "ffprobe";
        }
    }
}
