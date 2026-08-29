using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Tạo clip Zoom Ken Burns từ ảnh tĩnh — style theo <see cref="ShowcaseZoomStyleCatalog"/>.</summary>
    public static class ShowcaseZoomClipService
    {
        public const double DefaultDurationSeconds = ShowcaseSceneDurationHelper.DefaultClipSeconds;

        /// <summary>Giữ tương thích — tương đương <see cref="ShowcaseZoomStyleCatalog.Drift"/>.</summary>
        public static string BuildKenBurnsFilter(double clipDurationSeconds, int clipIndex) =>
            ShowcaseZoomStyleCatalog.BuildVideoFilter(clipDurationSeconds, clipIndex, ShowcaseZoomStyleCatalog.Drift);

        public static async Task GenerateAsync(
            string imagePath,
            string outputPath,
            int sceneIndex,
            string zoomStyleId,
            string ffmpegExecutable,
            Action<string> logAction,
            CancellationToken cancellationToken,
            double durationSeconds = ShowcaseSceneDurationHelper.DefaultClipSeconds,
            ShowcaseOutputAspectPreset outputCanvas = null,
            string zoomSpeedId = null,
            ShowcaseZoomAspectFitMode aspectFitMode = ShowcaseZoomAspectFitMode.Crop)
        {
            outputCanvas = outputCanvas ?? ShowcaseOutputAspectPresets.Vertical9x16;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("Không tìm thấy ảnh nguồn cho clip Zoom.", imagePath);
            }

            if (string.IsNullOrWhiteSpace(ffmpegExecutable) || !File.Exists(ffmpegExecutable))
            {
                throw new FileNotFoundException("Không tìm thấy ffmpeg.exe — bấm «⬇ Tải FFmpeg» trong Cài đặt.", ffmpegExecutable);
            }

            var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var resolvedStyle = ShowcaseZoomStyleCatalog.NormalizeId(zoomStyleId);
            if (string.IsNullOrEmpty(resolvedStyle))
            {
                resolvedStyle = ShowcaseZoomStyleCatalog.DefaultId;
            }

            var resolvedSpeed = ShowcaseZoomSpeedCatalog.ResolveSpeedId(zoomSpeedId, null);
            var vf = ShowcaseZoomStyleCatalog.BuildVideoFilter(
                durationSeconds,
                sceneIndex,
                resolvedStyle,
                outputCanvas,
                resolvedSpeed,
                aspectFitMode);
            var durationText = Math.Max(2.0d, durationSeconds).ToString("0.00", CultureInfo.InvariantCulture);
            var args =
                "-y -loop 1 -framerate 30 -t " + durationText +
                " -i \"" + imagePath + "\" -vf \"" + vf + "\" -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -an \"" + outputPath + "\"";

            logAction?.Invoke("[Showcase] Zoom scene " + (sceneIndex + 1) + " style=" + resolvedStyle
                + " speed=" + resolvedSpeed
                + " fit=" + (aspectFitMode == ShowcaseZoomAspectFitMode.BlurPad ? "blur" : "crop")
                + " dur=" + durationText + "s"
                + " " + ShowcaseZoomSpeedCatalog.FormatDeltaLog(resolvedSpeed, sceneIndex)
                + " canvas=" + outputCanvas.Width + "x" + outputCanvas.Height + ": " + Path.GetFileName(outputPath));

            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task RunFfmpegAsync(string ffmpegExecutable, string args, Action<string> logAction, CancellationToken cancellationToken)
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
                    logAction?.Invoke("[Showcase] FFmpeg lỗi: " + tail.Trim());
                    throw new InvalidOperationException("FFmpeg không tạo được clip Zoom (exit " + process.ExitCode + ").");
                }
            }
        }
    }
}
