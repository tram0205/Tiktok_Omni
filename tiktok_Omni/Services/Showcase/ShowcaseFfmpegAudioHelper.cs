using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseFfmpegAudioHelper
    {
        public static async Task CreateSilenceMp3Async(
            string ffmpegExecutable,
            double durationSeconds,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var duration = Math.Max(0.1d, durationSeconds);
            var durText = duration.ToString("0.###", CultureInfo.InvariantCulture);
            var output = (outputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(output))
            {
                throw new ArgumentException("Output path is required.", nameof(outputPath));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            var args = "-y -f lavfi -i anullsrc=r=44100:cl=stereo -t " + durText +
                       " -c:a libmp3lame -q:a 6 \"" + output + "\"";
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Tua nhanh (hoặc pad im lặng) khớp target — không cắt lời.</summary>
        public static async Task SpeedMp3ToFitDurationAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string inputPath,
            double targetDurationSeconds,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var target = Math.Max(0.1d, targetDurationSeconds);
            var targetText = target.ToString("0.###", CultureInfo.InvariantCulture);
            var input = (inputPath ?? string.Empty).Trim();
            var output = (outputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(input) || !File.Exists(input))
            {
                throw new FileNotFoundException("Audio input not found.", input);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            var inputDuration = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobeExecutable,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
            if (inputDuration <= 0.01d)
            {
                inputDuration = target;
            }

            var filterParts = new List<string>();
            if (inputDuration > target + 0.05d)
            {
                var tempo = inputDuration / target;
                filterParts.Add(BuildAtempoChain(tempo));
                logAction?.Invoke("[Showcase audio] Thoại ~" +
                                   inputDuration.ToString("0.#", CultureInfo.InvariantCulture) +
                                   "s > video " + target.ToString("0.#", CultureInfo.InvariantCulture) +
                                   "s — tua x" + tempo.ToString("0.##", CultureInfo.InvariantCulture) +
                                   " (giữ nguyên lời).");
            }
            else if (inputDuration < target - 0.05d)
            {
                logAction?.Invoke("[Showcase audio] Thoại ngắn hơn video — pad im lặng cuối.");
            }

            filterParts.Add("apad=whole_dur=" + targetText);
            filterParts.Add("asetpts=PTS-STARTPTS");

            var args = "-y -i \"" + input + "\" -af \"" + string.Join(",", filterParts) +
                       "\" -c:a libmp3lame -q:a 4 \"" + output + "\"";
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Trả path audio dùng lúc render — tua/pad khớp thời lượng video stitched.</summary>
        public static async Task<string> PrepareNarrationForVideoDurationAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string narrationPath,
            double videoDurationSeconds,
            string workDirectory,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var source = (narrationPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                return source;
            }

            var videoDuration = Math.Max(0.1d, videoDurationSeconds);
            var narrationDuration = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobeExecutable,
                    source,
                    cancellationToken)
                .ConfigureAwait(false);
            if (narrationDuration <= 0.01d)
            {
                return source;
            }

            if (Math.Abs(narrationDuration - videoDuration) <= 0.08d)
            {
                return source;
            }

            Directory.CreateDirectory(workDirectory ?? ".");
            var output = Path.Combine(workDirectory ?? ".", "narration_render_fit.mp3");
            await SpeedMp3ToFitDurationAsync(
                    ffmpegExecutable,
                    ffprobeExecutable,
                    source,
                    videoDuration,
                    output,
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);
            return output;
        }

        /// <summary>Pad im lặng nếu ngắn; tua nhanh nhẹ nếu dài — tránh cắt giữa câu khi có thể.</summary>
        public static async Task FitMp3ToDurationAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string inputPath,
            double durationSeconds,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var duration = Math.Max(0.1d, durationSeconds);
            var durText = duration.ToString("0.###", CultureInfo.InvariantCulture);
            var input = (inputPath ?? string.Empty).Trim();
            var output = (outputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(input) || !File.Exists(input))
            {
                throw new FileNotFoundException("Audio input not found.", input);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            var inputDuration = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobeExecutable,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
            if (inputDuration <= 0.01d)
            {
                inputDuration = duration;
            }

            var filterParts = new List<string>();
            if (inputDuration > duration + 0.08d)
            {
                var ratio = inputDuration / duration;
                var tempo = Math.Min(1.45d, ratio);
                if (tempo > 1.03d)
                {
                    filterParts.Add(BuildAtempoChain(tempo));
                    logAction?.Invoke("[Showcase audio] TTS ~" +
                                       inputDuration.ToString("0.#", CultureInfo.InvariantCulture) +
                                       "s > clip " + duration.ToString("0.#", CultureInfo.InvariantCulture) +
                                       "s — tua x" + tempo.ToString("0.##", CultureInfo.InvariantCulture) +
                                       " (không cắt giữa câu).");
                }
            }

            filterParts.Add("apad=whole_dur=" + durText);
            filterParts.Add("atrim=0:" + durText);
            filterParts.Add("asetpts=PTS-STARTPTS");

            var args = "-y -i \"" + input + "\" -af \"" + string.Join(",", filterParts) +
                       "\" -t " + durText + " -c:a libmp3lame -q:a 4 \"" + output + "\"";
            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        internal static string BuildAtempoChainPublic(double tempo) => BuildAtempoChain(tempo);

        private static string BuildAtempoChain(double tempo)
        {
            var remaining = Math.Max(1.0d, tempo);
            var filters = new List<string>();
            while (remaining > 1.001d)
            {
                var step = Math.Min(2.0d, remaining);
                filters.Add("atempo=" + step.ToString("0.###", CultureInfo.InvariantCulture));
                remaining /= step;
            }

            return filters.Count > 0 ? string.Join(",", filters) : "atempo=1";
        }

        private static Task RunFfmpegAsync(
            string ffmpegExecutable,
            string args,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            return FfmpegProcessRunner.RunAsync(
                ffmpegExecutable,
                args,
                logAction,
                cancellationToken,
                logPrefix: "[Showcase audio]",
                timeoutSeconds: 120);
        }
    }
}
