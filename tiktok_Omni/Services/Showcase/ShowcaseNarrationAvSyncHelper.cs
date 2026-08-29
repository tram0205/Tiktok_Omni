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
            CancellationToken cancellationToken,
            bool preserveAudioSource = false)
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
                    TargetDurationSeconds = await ProbeSeconds(ffprobeExecutable, videoIn, cancellationToken).ConfigureAwait(false),
                    AppliedAudioTempo = 1d,
                    AppliedVideoTempo = 1d
                };
            }

            Directory.CreateDirectory(workDirectory ?? ".");
            var videoDuration = await ProbeSeconds(ffprobeExecutable, videoIn, cancellationToken).ConfigureAwait(false);
            var narrationDuration = await ProbeSeconds(ffprobeExecutable, narrIn, cancellationToken).ConfigureAwait(false);

            if (preserveAudioSource)
            {
                const double syncThreshold = 1.03d;
                var lockedVideoOut = videoIn;
                var lockedAudioOut = narrIn;
                var appliedAudioTempo = 1d;
                var appliedVideoTempo = 1d;

                logAction?.Invoke("[Showcase] So thời lượng clip ghép (~"
                    + videoDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s) vs audio thành phẩm (~"
                    + narrationDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s) — tua nhanh phía dài, không cắt.");

                if (videoDuration > narrationDuration * syncThreshold && narrationDuration > 0.05d)
                {
                    var tempo = videoDuration / narrationDuration;
                    appliedVideoTempo = tempo;
                    lockedVideoOut = Path.Combine(workDirectory, "stitched_av_sync.mp4");
                    logAction?.Invoke("[Showcase AV sync] Đang tua video x"
                        + tempo.ToString("0.##", CultureInfo.InvariantCulture) + "…");
                    await SpeedVideoAsync(
                            ffmpegExecutable,
                            ffprobeExecutable,
                            videoIn,
                            tempo,
                            lockedVideoOut,
                            logAction,
                            cancellationToken)
                        .ConfigureAwait(false);
                    logAction?.Invoke("[Showcase] Tua video x"
                        + tempo.ToString("0.##", CultureInfo.InvariantCulture)
                        + " (clip ghép dài hơn audio).");
                }
                else if (narrationDuration > videoDuration * syncThreshold && videoDuration > 0.05d)
                {
                    var tempo = narrationDuration / videoDuration;
                    appliedAudioTempo = tempo;
                    lockedAudioOut = Path.Combine(workDirectory, "full_mix_av_sync.mp3");
                    logAction?.Invoke("[Showcase AV sync] Đang tua audio thành phẩm x"
                        + tempo.ToString("0.##", CultureInfo.InvariantCulture) + "…");
                    await SpeedMp3ByTempoAsync(
                            ffmpegExecutable,
                            ffprobeExecutable,
                            narrIn,
                            tempo,
                            lockedAudioOut,
                            logAction,
                            cancellationToken)
                        .ConfigureAwait(false);
                    logAction?.Invoke("[Showcase] Tua audio thành phẩm x"
                        + tempo.ToString("0.##", CultureInfo.InvariantCulture)
                        + " (audio dài hơn clip ghép).");
                }
                else
                {
                    logAction?.Invoke("[Showcase] Clip ghép và audio đã gần khớp — ghép trực tiếp.");
                }

                var finalVideoDuration = await ProbeSeconds(ffprobeExecutable, lockedVideoOut, cancellationToken)
                    .ConfigureAwait(false);
                var finalAudioDuration = await ProbeSeconds(ffprobeExecutable, lockedAudioOut, cancellationToken)
                    .ConfigureAwait(false);
                logAction?.Invoke("[Showcase] Sau khớp: video ~"
                    + finalVideoDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s, audio ~"
                    + finalAudioDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s.");

                return new ShowcaseAvSyncResult
                {
                    VideoPath = lockedVideoOut,
                    NarrationPath = lockedAudioOut,
                    TargetDurationSeconds = finalAudioDuration,
                    AppliedAudioTempo = appliedAudioTempo,
                    AppliedVideoTempo = appliedVideoTempo
                };
            }
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
                TargetDurationSeconds = targetDuration,
                AppliedAudioTempo = plan.AudioTempo,
                AppliedVideoTempo = plan.VideoTempo
            };
        }

        public static async Task<string> PrepareSpeedAdjustedMp3Async(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string inputPath,
            string workDirectory,
            int userSpeedPercent,
            string outputFileName,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var input = (inputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(input) || !File.Exists(input))
            {
                return input;
            }

            var tempo = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(userSpeedPercent) / 100d;
            if (Math.Abs(tempo - 1d) <= 0.03d)
            {
                return input;
            }

            Directory.CreateDirectory(workDirectory ?? ".");
            var output = Path.Combine(workDirectory, string.IsNullOrWhiteSpace(outputFileName)
                ? "speed_preview.mp3"
                : outputFileName.Trim());
            await SpeedMp3ByTempoAsync(
                    ffmpegExecutable,
                    ffprobeExecutable,
                    input,
                    tempo,
                    output,
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);
            return output;
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
            if (Math.Abs(tempo - 1d) <= 0.03d)
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
                       audioFilter + "[a]\" -map \"[v]\" -map \"[a]\" -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -c:a aac -b:a 192k \"" +
                       outputPath + "\"";
            }
            else
            {
                args = "-y -i \"" + inputPath + "\" -vf \"setpts=PTS/" + tempoText + "\" -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p \"" +
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
            await FfmpegProcessRunner.RunAsync(
                    ffmpegExecutable,
                    args,
                    logAction,
                    cancellationToken,
                    logPrefix: "[Showcase AV sync]",
                    timeoutSeconds: FfmpegProcessRunner.DefaultTimeoutSeconds)
                .ConfigureAwait(false);
        }
    }
}
