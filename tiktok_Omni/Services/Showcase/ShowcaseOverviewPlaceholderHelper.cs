using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Clip/audio tạm cho «Tổng quan» khi thiếu asset render thật.</summary>
    internal static class ShowcaseOverviewPlaceholderHelper
    {
        public static async Task<string> ResolveSceneClipForOverviewAsync(
            AiVideoGenInputItem scene,
            int sceneIndex,
            string workDirectory,
            string ffmpegExecutable,
            ShowcaseOutputAspectPreset outputCanvas,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var clip = (scene?.ClipPath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(clip) && File.Exists(clip))
            {
                return clip;
            }

            Directory.CreateDirectory(workDirectory ?? ".");
            var duration = ShowcaseSceneDurationHelper.ResolveZoomClipDuration(scene);
            var imagePath = (scene?.ThumbnailPath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                var zoomOut = Path.Combine(workDirectory, "overview_zoom_scene_" + (sceneIndex + 1) + ".mp4");
                if (!File.Exists(zoomOut))
                {
                    await ShowcaseZoomClipService.GenerateAsync(
                        imagePath,
                        zoomOut,
                        sceneIndex,
                        scene?.ShowcaseZoomStyleId,
                        ffmpegExecutable,
                        logAction,
                        cancellationToken,
                        duration,
                        outputCanvas,
                        scene?.ShowcaseZoomSpeedId).ConfigureAwait(false);
                }

                return zoomOut;
            }

            var placeholderOut = Path.Combine(workDirectory, "overview_missing_scene_" + (sceneIndex + 1) + ".mp4");
            if (!File.Exists(placeholderOut))
            {
                await CreateLabeledPlaceholderClipAsync(
                    ffmpegExecutable,
                    placeholderOut,
                    outputCanvas,
                    "Thiếu clip · Cảnh " + (sceneIndex + 1),
                    duration,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }

            return placeholderOut;
        }

        public static async Task CreateLabeledPlaceholderClipAsync(
            string ffmpegExecutable,
            string outputPath,
            ShowcaseOutputAspectPreset canvas,
            string label,
            double durationSeconds,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            canvas = canvas ?? ShowcaseOutputAspectPresets.Vertical9x16;
            var dur = Math.Max(2.0d, durationSeconds).ToString("0.##", CultureInfo.InvariantCulture);
            var safe = EscapeDrawText(string.IsNullOrWhiteSpace(label) ? "Thieu" : label.Trim());
            var vf = "scale=" + canvas.Width + ":" + canvas.Height + ":force_original_aspect_ratio=increase," +
                     "crop=" + canvas.Width + ":" + canvas.Height + ",format=yuv420p," +
                     "drawtext=font='Segoe UI':text='" + safe + "':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=42:fontcolor=white@0.92:borderw=2:bordercolor=black@0.6";
            var args = "-y -f lavfi -i color=c=0x1a1d26:s=" + canvas.Width + "x" + canvas.Height + ":d=" + dur +
                       " -vf \"" + vf + "\" -c:v libx264 -preset fast -crf 22 -pix_fmt yuv420p -an \"" + outputPath + "\"";
            logAction?.Invoke("[Showcase] Tổng quan — placeholder cảnh: " + Path.GetFileName(outputPath));
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static string EscapeDrawText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace(":", "\\:")
                .Replace("'", "\\'")
                .Replace(",", "\\,")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("%", "\\%")
                .Replace("\r", " ")
                .Replace("\n", " ");
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
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Không khởi chạy được ffmpeg.exe.");
                }

                var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var tail = stderr.Length > 400 ? stderr.Substring(stderr.Length - 400) : stderr;
                    logAction?.Invoke("[Showcase] FFmpeg placeholder: " + tail.Trim());
                    throw new InvalidOperationException("FFmpeg không tạo được clip placeholder (exit " + process.ExitCode + ").");
                }
            }
        }
    }
}
