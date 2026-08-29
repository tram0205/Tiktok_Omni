using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseMediaProbeHelper
    {
        public static async Task<double> ProbeDurationSecondsAsync(
            string ffprobeExecutable,
            string mediaPath,
            CancellationToken cancellationToken)
        {
            var path = (mediaPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return 0d;
            }

            var ffprobe = (ffprobeExecutable ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ffprobe) || !File.Exists(ffprobe))
            {
                ffprobe = FfmpegToolkitService.GetBundledFfprobePath();
            }

            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"";
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
                var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 30).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    return 0d;
                }

                if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    return Math.Max(0d, seconds);
                }
            }

            return 0d;
        }

        public static async Task<(int width, int height)> ProbeVideoResolutionAsync(
            string ffprobeExecutable,
            string mediaPath,
            CancellationToken cancellationToken)
        {
            var path = (mediaPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return (0, 0);
            }

            var ffprobe = (ffprobeExecutable ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ffprobe) || !File.Exists(ffprobe))
            {
                ffprobe = FfmpegToolkitService.GetBundledFfprobePath();
            }

            var args = "-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"" + path + "\"";
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
                var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 30).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    return (0, 0);
                }

                var text = (stdout ?? string.Empty).Trim();
                var parts = text.Split(',');
                if (parts.Length >= 2
                    && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
                    && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
                {
                    return (w, h);
                }
            }

            return (0, 0);
        }

        public static async Task<bool> HasAudioStreamAsync(
            string ffprobeExecutable,
            string mediaPath,
            CancellationToken cancellationToken)
        {
            var path = (mediaPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            var ffprobe = (ffprobeExecutable ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ffprobe) || !File.Exists(ffprobe))
            {
                ffprobe = FfmpegToolkitService.GetBundledFfprobePath();
            }

            var args = "-v error -select_streams a:0 -show_entries stream=index -of csv=p=0 \"" + path + "\"";
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
                var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 30).ConfigureAwait(false);
                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
            }
        }
    }
}
