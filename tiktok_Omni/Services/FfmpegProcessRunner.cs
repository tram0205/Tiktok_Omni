using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Chạy FFmpeg an toàn: đọc stdout/stderr song song (tránh deadlock), timeout, log tiến độ.</summary>
    internal static class FfmpegProcessRunner
    {
        public const int DefaultTimeoutSeconds = 180;

        public static async Task RunAsync(
            string ffmpegExecutable,
            string arguments,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string logPrefix = "FFmpeg",
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            var ffmpeg = ResolveExecutable(ffmpegExecutable);
            var args = arguments ?? string.Empty;
            var prefix = string.IsNullOrWhiteSpace(logPrefix) ? "FFmpeg" : logPrefix.Trim();

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        prefix + " — không chạy được ffmpeg (" + ffmpeg + "). Kiểm tra FFmpeg trong Cài đặt.\r\n" + ex.Message,
                        ex);
                }

                logAction?.Invoke(prefix + " chạy (timeout " + timeoutSeconds + "s)…");

                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();

                try
                {
                    await ProcessCancellationHelper.WaitUntilExitWithTimeoutAsync(
                            process,
                            cancellationToken,
                            timeoutSeconds)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException ex)
                {
                    logAction?.Invoke(prefix + " quá thời gian (" + timeoutSeconds + "s) — đã dừng process.");
                    throw new InvalidOperationException(
                        prefix + " quá thời gian (" + timeoutSeconds + "s). Thử tắt bớt SFX/nhạc nền hoặc rút ngắn thoại.",
                        ex);
                }

                var errText = await stderrTask.ConfigureAwait(false);
                var outText = await stdoutTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    var detail = PickErrorDetail(errText, outText);
                    logAction?.Invoke(prefix + " lỗi (exit " + process.ExitCode + "): " + Truncate(detail, 600));
                    throw new InvalidOperationException(prefix + " lỗi: " + Truncate(detail, 320));
                }

                var progressLine = PickLastMeaningfulLine(errText);
                if (!string.IsNullOrWhiteSpace(progressLine))
                {
                    logAction?.Invoke(prefix + " xong — " + progressLine.Trim());
                }
                else
                {
                    logAction?.Invoke(prefix + " xong.");
                }
            }
        }

        private static string ResolveExecutable(string ffmpegExecutable)
        {
            var ffmpeg = (ffmpegExecutable ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ffmpeg) || !System.IO.File.Exists(ffmpeg))
            {
                ffmpeg = FfmpegToolkitService.GetBundledFfmpegPath();
            }

            return ffmpeg;
        }

        private static string PickErrorDetail(string stderr, string stdout)
        {
            var err = (stderr ?? string.Empty).Trim();
            if (err.Length > 0)
            {
                return PickTailErrorLines(err);
            }

            return PickTailErrorLines((stdout ?? string.Empty).Trim());
        }

        private static string PickTailErrorLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lines = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var keywords = new[] { "error", "invalid", "failed", "matches no streams", "does not contain", "not found", "unable to" };
            var hits = lines
                .Where(line => keywords.Any(k => line.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            if (hits.Count > 0)
            {
                var tailHits = hits.Skip(Math.Max(0, hits.Count - 4));
                return string.Join(" | ", tailHits);
            }

            return string.Join(" | ", lines.Skip(Math.Max(0, lines.Count - 3)));
        }

        private static string PickLastMeaningfulLine(string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr))
            {
                return string.Empty;
            }

            var lines = stderr
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var progress = lines.LastOrDefault(x =>
                x.IndexOf("time=", StringComparison.OrdinalIgnoreCase) >= 0
                || x.IndexOf("Lsize=", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(progress))
            {
                return progress;
            }

            return lines[lines.Count - 1];
        }

        private static string Truncate(string text, int maxLength)
        {
            var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "…";
        }
    }
}
