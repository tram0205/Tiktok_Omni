using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseNarrationAvSyncHelper
    {
        public static async Task<ShowcaseAvSyncResult> ApplyAtRenderAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string stitchedVideoPath,
            string narrationPath,
            string workDirectory,
            int userSpeedPercent,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var videoIn = (stitchedVideoPath ?? string.Empty).Trim();
            var narrIn = (narrationPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(videoIn) || !File.Exists(videoIn))
            {
                throw new FileNotFoundException("Không tìm thấy video ghép.", videoIn);
            }

            if (string.IsNullOrEmpty(narrIn) || !File.Exists(narrIn))
            {
                return new ShowcaseAvSyncResult
                {
                    VideoPath = videoIn,
                    NarrationPath = narrIn,
                    TargetDurationSeconds = await ProbeSeconds(ffprobeExecutable, videoIn, cancellationToken).ConfigureAwait(false)
                };
            }

            Directory.CreateDirectory(workDirectory ?? ".");
            var videoDuration = await ProbeSeconds(ffprobeExecutable, videoIn, cancellationToken).ConfigureAwait(false);
            var narrationDuration = await ProbeSeconds(ffprobeExecutable, narrIn, cancellationToken).ConfigureAwait(false);
            var plan = ShowcaseNarrationSpeedHelper.BuildRenderPlan(
                narrationDuration,
                videoDuration,
                userSpeedPercent);
            var effectivePercent = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(userSpeedPercent);

            logAction?.Invoke("[Showcase] Tốc độ thoại: "
                + effectivePercent.ToString(CultureInfo.InvariantCulture)
                + "% (chỉnh tay) → tự khớp clip ~"
                + videoDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s / thoại ~"
                + narrationDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s → mục tiêu ~"
                + plan.TargetDurationSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s.");

            var videoOut = videoIn;
            if (plan.VideoTempo > 1.03d)
            {
                videoOut = Path.Combine(workDirectory, "stitched_av_sync.mp4");
                await SpeedVideoAsync(
                        ffmpegExecutable,
                        ffprobeExecutable,
                        videoIn,
                        plan.VideoTempo,
                        videoOut,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
                logAction?.Invoke("[Showcase] Tua video x"
                    + plan.VideoTempo.ToString("0.##", CultureInfo.InvariantCulture)
                    + " (clip dài hơn thoại).");
            }

            var targetDuration = plan.TargetDurationSeconds;
            if (plan.VideoTempo > 1.03d)
            {
                targetDuration = await ProbeSeconds(ffprobeExecutable, videoOut, cancellationToken).ConfigureAwait(false);
            }

            var narrOut = Path.Combine(workDirectory, "narration_av_sync.mp3");
            if (plan.AudioTempo > 1.03d)
            {
                await SpeedMp3ByTempoAsync(
                        ffmpegExecutable,
                        ffprobeExecutable,
                        narrIn,
                        plan.AudioTempo,
                        narrOut,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                narrOut = narrIn;
            }

            var finalNarration = await ShowcaseFfmpegAudioHelper.PrepareNarrationForVideoDurationAsync(
                    ffmpegExecutable,
                    ffprobeExecutable,
                    narrOut,
                    targetDuration,
                    workDirectory,
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);

            return new ShowcaseAvSyncResult
            {
                VideoPath = videoOut,
                NarrationPath = finalNarration,
                TargetDurationSeconds = targetDuration
            };
        }

        private static async Task SpeedMp3ByTempoAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string inputPath,
            double tempo,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var input = (inputPath ?? string.Empty).Trim();
            var output = (outputPath ?? string.Empty).Trim();
            if (tempo <= 1.03d)
            {
                if (!string.Equals(input, output, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(input, output, true);
                }

                return;
            }

            var filter = ShowcaseFfmpegAudioHelper.BuildAtempoChainPublic(tempo);
            var args = "-y -i \"" + input + "\" -af \"" + filter + ",asetpts=PTS-STARTPTS\" -c:a libmp3lame -q:a 4 \"" + output + "\"";
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task SpeedVideoAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string inputPath,
            double tempo,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var tempoText = tempo.ToString("0.######", CultureInfo.InvariantCulture);
            var hasAudio = await HasAudioStreamAsync(ffprobeExecutable, inputPath, cancellationToken).ConfigureAwait(false);
            string args;
            if (hasAudio)
            {
                var audioFilter = ShowcaseFfmpegAudioHelper.BuildAtempoChainPublic(tempo);
                args = "-y -i \"" + inputPath + "\" -filter_complex \"[0:v]setpts=PTS/" + tempoText + "[v];[0:a]" +
                       audioFilter + "[a]\" -map \"[v]\" -map \"[a]\" -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k \"" +
                       outputPath + "\"";
            }
            else
            {
                args = "-y -i \"" + inputPath + "\" -vf \"setpts=PTS/" + tempoText + "\" -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p \"" +
                       outputPath + "\"";
            }

            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static Task<bool> HasAudioStreamAsync(
            string ffprobeExecutable,
            string mediaPath,
            CancellationToken cancellationToken) =>
            ShowcaseMediaProbeHelper.HasAudioStreamAsync(ffprobeExecutable, mediaPath, cancellationToken);

        private static async Task<double> ProbeSeconds(
            string ffprobeExecutable,
            string path,
            CancellationToken cancellationToken)
        {
            return await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobeExecutable,
                    path,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task RunFfmpegAsync(
            string ffmpegExecutable,
            string args,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var ffmpeg = (ffmpegExecutable ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg))
            {
                ffmpeg = FfmpegToolkitService.GetBundledFfmpegPath();
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 300).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    logAction?.Invoke("[Showcase AV sync] FFmpeg: " + err);
                    throw new InvalidOperationException("FFmpeg AV sync failed (exit " + process.ExitCode + ").");
                }
            }
        }
    }
}
