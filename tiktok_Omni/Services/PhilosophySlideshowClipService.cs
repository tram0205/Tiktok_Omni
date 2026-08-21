using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Slideshow ảnh tĩnh 9:16 — hold frame + crossfade (không Ken Burns).</summary>
    public static class PhilosophySlideshowClipService
    {
        public const double DefaultCrossfadeSeconds = 0.85;

        private const string PortraitFilter =
            "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,format=yuv420p";

        public static async Task GenerateHoldClipAsync(
            string imagePath,
            string outputPath,
            double durationSeconds,
            string ffmpegExecutable,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("Không tìm thấy ảnh slideshow.", imagePath);
            }

            if (string.IsNullOrWhiteSpace(ffmpegExecutable) || !File.Exists(ffmpegExecutable))
            {
                throw new FileNotFoundException("Không tìm thấy ffmpeg.exe.", ffmpegExecutable);
            }

            var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var durationText = Math.Max(2.0d, durationSeconds).ToString("0.00", CultureInfo.InvariantCulture);
            var args =
                "-y -loop 1 -framerate 30 -t " + durationText +
                " -i \"" + imagePath + "\" -vf \"" + PortraitFilter +
                "\" -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -an \"" + outputPath + "\"";

            logAction?.Invoke("[Quote] Slideshow hold " + durationText + "s: " + Path.GetFileName(imagePath));
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        public static async Task AssembleCrossfadeAsync(
            IList<string> clipPaths,
            IList<double> clipDurationSeconds,
            string outputPath,
            string ffmpegExecutable,
            double transitionDurationSeconds,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (clipPaths == null || clipPaths.Count == 0)
            {
                throw new InvalidOperationException("Không có clip slideshow để ghép.");
            }

            if (clipPaths.Count == 1)
            {
                File.Copy(clipPaths[0], outputPath, overwrite: true);
                return;
            }

            var durations = NormalizeClipDurations(clipPaths.Count, clipDurationSeconds);
            var transition = Math.Max(0.2d, Math.Min(transitionDurationSeconds, durations.Min() * 0.45d));

            var inputBuilder = new StringBuilder();
            for (var i = 0; i < clipPaths.Count; i++)
            {
                inputBuilder.Append("-i \"").Append(clipPaths[i]).Append("\" ");
            }

            var filter = new StringBuilder();
            var currentLabel = "[0:v]";
            var timeline = durations[0];
            for (var i = 1; i < clipPaths.Count; i++)
            {
                var outputLabel = i == clipPaths.Count - 1 ? "[vout]" : "[vx" + i.ToString(CultureInfo.InvariantCulture) + "]";
                var offset = Math.Max(0d, timeline - transition);
                filter
                    .Append(currentLabel)
                    .Append("[")
                    .Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append(":v]xfade=transition=fade:duration=")
                    .Append(transition.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(":offset=")
                    .Append(offset.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(outputLabel)
                    .Append(";");
                currentLabel = outputLabel;
                timeline += durations[i] - transition;
            }

            var args = "-y " + inputBuilder + "-filter_complex \"" + filter +
                       "\" -map \"[vout]\" -r 30 -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -an \"" +
                       outputPath + "\"";
            logAction?.Invoke("[Quote] Slideshow xfade " + clipPaths.Count + " clip (crossfade " +
                              transition.ToString("0.0", CultureInfo.InvariantCulture) + "s).");
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        public static async Task TrimVideoSegmentAsync(
            string inputVideoPath,
            string outputPath,
            double durationSeconds,
            string ffmpegExecutable,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputVideoPath) || !File.Exists(inputVideoPath))
            {
                throw new FileNotFoundException("Không tìm thấy video nguồn.", inputVideoPath);
            }

            var durationText = Math.Max(1.0d, durationSeconds).ToString("0.00", CultureInfo.InvariantCulture);
            var args =
                "-y -ss 0 -t " + durationText + " -i \"" + inputVideoPath + "\" -vf \"" + PortraitFilter +
                "\" -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -an \"" + outputPath + "\"";
            logAction?.Invoke("[Quote] Trim B-roll outro " + durationText + "s: " + Path.GetFileName(inputVideoPath));
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static IList<double> NormalizeClipDurations(int clipCount, IList<double> clipDurationSeconds)
        {
            if (clipCount <= 0)
            {
                return Array.Empty<double>();
            }

            if (clipDurationSeconds == null || clipDurationSeconds.Count == 0)
            {
                return Enumerable.Repeat(4.0d, clipCount).ToList();
            }

            var list = new List<double>(clipCount);
            for (var i = 0; i < clipCount; i++)
            {
                var value = i < clipDurationSeconds.Count ? clipDurationSeconds[i] : clipDurationSeconds.Last();
                list.Add(Math.Max(2.0d, value));
            }

            return list;
        }

        private static async Task RunFfmpegAsync(
            string ffmpegExecutable,
            string args,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Không khởi chạy được FFmpeg.");
                }

                await Task.Run(() =>
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        process.StandardError.ReadLine();
                    }
                }, cancellationToken).ConfigureAwait(false);

                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("FFmpeg slideshow thất bại (exit " + process.ExitCode + ").");
                }
            }
        }
    }
}
