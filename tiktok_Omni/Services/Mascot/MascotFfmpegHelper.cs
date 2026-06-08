using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Mascot
{
    internal static class MascotFfmpegHelper
    {
        public static string ResolveFfmpegExecutable(AppSettings settings)
        {
            var configured = (settings?.FfmpegPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                return configured;
            }

            var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe");
            return File.Exists(bundled) ? bundled : "ffmpeg";
        }

        public static async Task ConcatClipsWithCrossfadeAsync(
            IList<string> clipFiles,
            string outputFile,
            double transitionSeconds,
            Action<string> log,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (clipFiles == null || clipFiles.Count == 0)
            {
                throw new InvalidOperationException("Không có clip để ghép.");
            }

            var ffmpeg = ResolveFfmpegExecutable(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile)) ?? ".");

            if (clipFiles.Count == 1)
            {
                var singleArgs = "-y -i \"" + clipFiles[0] + "\" -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k \"" + outputFile + "\"";
                await RunFfmpegAsync(ffmpeg, singleArgs, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            var durations = new List<double>();
            foreach (var clip in clipFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                durations.Add(Math.Max(2d, await ProbeDurationAsync(ffmpeg, clip, cancellationToken).ConfigureAwait(false)));
            }

            var inputBuilder = new StringBuilder();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                inputBuilder.Append("-i \"").Append(clipFiles[i]).Append("\" ");
            }

            var td = transitionSeconds.ToString("0.00", CultureInfo.InvariantCulture);
            var filter = new StringBuilder();
            var currentVideo = "[0:v]";
            var currentAudio = "[0:a]";
            var timeline = durations[0];
            for (var i = 1; i < clipFiles.Count; i++)
            {
                var outV = i == clipFiles.Count - 1 ? "[vout]" : "[vx" + i + "]";
                var outA = i == clipFiles.Count - 1 ? "[aout]" : "[ax" + i + "]";
                var offset = Math.Max(0.1d, timeline - transitionSeconds)
                    .ToString("0.00", CultureInfo.InvariantCulture);
                filter.Append(currentVideo).Append("[").Append(i).Append(":v]xfade=transition=fade:duration=")
                    .Append(td).Append(":offset=").Append(offset).Append(outV).Append(";");
                filter.Append(currentAudio).Append("[").Append(i).Append(":a]acrossfade=d=").Append(td).Append(outA).Append(";");
                currentVideo = outV;
                currentAudio = outA;
                timeline += durations[i] - transitionSeconds;
            }

            var args = "-y " + inputBuilder + "-filter_complex \"" + filter + "\" -map \"[vout]\" -map \"[aout]\" -r 30 -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k \"" + outputFile + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<double> ProbeDurationAsync(string ffmpeg, string path, CancellationToken cancellationToken)
        {
            var ffprobe = ffmpeg.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                ? ffmpeg.Replace("ffmpeg.exe", "ffprobe.exe")
                : "ffprobe";
            if (!File.Exists(ffprobe))
            {
                return 8d;
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    return 8d;
                }

                var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await Task.Run(() => proc.WaitForExit(), cancellationToken).ConfigureAwait(false);
                if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
                {
                    return sec;
                }
            }

            return 8d;
        }

        private static async Task RunFfmpegAsync(string ffmpeg, string args, Action<string> log, CancellationToken cancellationToken)
        {
            log?.Invoke("[FFmpeg] " + args);
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    throw new InvalidOperationException("Không khởi chạy được FFmpeg.");
                }

                while (!proc.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }

                if (proc.ExitCode != 0)
                {
                    var err = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new InvalidOperationException("FFmpeg failed: " + err);
                }
            }
        }
    }
}
