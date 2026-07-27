using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Karaoke ASS + watermark CTA 3 giây cuối — bọc FFmpeg an toàn.</summary>
    public sealed class AffiliateVideoPostProcessingService
    {
        public const double CtaTailSeconds = 3d;

        public async Task<string> ApplyAffiliatePolishAsync(
            string inputVideoPath,
            string narrationText,
            string narrationAudioPath,
            AppSettings settings,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            IReadOnlyList<WordTimestamp> ttsWordTimestamps = null)
        {
            if (string.IsNullOrWhiteSpace(inputVideoPath) || !File.Exists(inputVideoPath))
            {
                throw new FileNotFoundException("Video đầu vào không tồn tại.", inputVideoPath ?? string.Empty);
            }

            var ffmpeg = ResolveFfmpeg(settings);
            var stage = string.IsNullOrWhiteSpace(workDirectory)
                ? Path.GetDirectoryName(inputVideoPath) ?? "."
                : workDirectory;
            Directory.CreateDirectory(stage);

            var current = inputVideoPath;
            KaraokeAssSubtitleService.KaraokeAssBurnInResult karaoke = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(narrationText) &&
                    !string.IsNullOrWhiteSpace(narrationAudioPath) &&
                    File.Exists(narrationAudioPath))
                {
                    karaoke = await BuildKaraokeBurnInAsync(
                        ffmpeg,
                        narrationText,
                        narrationAudioPath,
                        stage,
                        log,
                        cancellationToken,
                        settings,
                        ttsWordTimestamps).ConfigureAwait(false);
                    if (karaoke != null)
                    {
                        var withSubs = Path.Combine(stage, "polished_subs.mp4");
                        await BurnVideoFilterAsync(
                            ffmpeg,
                            current,
                            withSubs,
                            karaoke.VideoFilterFragment,
                            log,
                            cancellationToken).ConfigureAwait(false);
                        current = withSubs;
                    }
                }

                var ctaPath = AffiliateCtaAssetService.EnsureCartArrowAsset(settings?.StorageRootPath);
                var duration = await SubtitleTimingHelper.ProbeMediaDurationSecondsAsync(ffmpeg, current, cancellationToken)
                    .ConfigureAwait(false);
                if (duration > CtaTailSeconds + 0.2d)
                {
                    var withCta = Path.Combine(stage, "polished_cta.mp4");
                    var start = Math.Max(0d, duration - CtaTailSeconds);
                    var ctaFilter = BuildCtaOverlayFilter(ctaPath, start, duration);
                    await BurnVideoFilterAsync(ffmpeg, current, withCta, ctaFilter, log, cancellationToken).ConfigureAwait(false);
                    current = withCta;
                }

                return current;
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(karaoke?.AssFilePath);
            }
        }

        private static async Task<KaraokeAssSubtitleService.KaraokeAssBurnInResult> BuildKaraokeBurnInAsync(
            string ffmpeg,
            string narrationText,
            string narrationAudioPath,
            string stage,
            Action<string> log,
            CancellationToken cancellationToken,
            AppSettings settings,
            IReadOnlyList<WordTimestamp> ttsWordTimestamps)
        {
            try
            {
                if (ttsWordTimestamps != null && ttsWordTimestamps.Count > 0)
                {
                    var assPath = Path.Combine(stage, KaraokeAssSubtitleService.DefaultAssFileName);
                    AssSubtitleGenerator.WriteAssFile(
                        assPath,
                        ttsWordTimestamps,
                        CreateKaraokeStyleOptions());
                    var esc = KaraokeAssSubtitleService.EscapePathForFfmpegSubtitleFilter(assPath);
                    log?.Invoke("[Affiliate Polish] Karaoke ASS từ TTS timestamps (" + ttsWordTimestamps.Count + " từ).");
                    return new KaraokeAssSubtitleService.KaraokeAssBurnInResult
                    {
                        AssFilePath = assPath,
                        VideoFilterFragment = "subtitles='" + esc + "'"
                    };
                }

                return await KaraokeAssSubtitleService.TryCreateBurnInAsync(
                    ffmpeg,
                    narrationText,
                    narrationAudioPath,
                    stage,
                    log,
                    cancellationToken,
                    settings?.AiApiKey,
                    CreateKaraokeStyleOptions()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log?.Invoke("[Affiliate Polish] Karaoke bỏ qua: " + ex.Message);
                return null;
            }
        }

        public static AssSubtitleGeneratorOptions CreateKaraokeStyleOptions()
        {
            return new AssSubtitleGeneratorOptions
            {
                WordsPerLine = 6,
                FontSize = 88,
                MarginV = 140,
                PlayResX = 1080,
                PlayResY = 1920
            };
        }

        private static string BuildCtaOverlayFilter(string ctaPngPath, double startSec, double endSec)
        {
            var esc = KaraokeAssSubtitleService.EscapePathForFfmpegSubtitleFilter(ctaPngPath);
            var s = startSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var e = endSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return "movie='" + esc + "'[cta];[0:v][cta]overlay=x=48:y=H-h-120:enable='between(t," + s + "," + e + ")'";
        }

        private static async Task BurnVideoFilterAsync(
            string ffmpeg,
            string input,
            string output,
            string videoFilter,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoFilter))
            {
                File.Copy(input, output, true);
                return;
            }

            var args = "-y -i \"" + input + "\" -vf \"" + videoFilter +
                       "\" -c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p -c:a copy \"" + output + "\"";
            try
            {
                await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log?.Invoke("[Affiliate Polish] FFmpeg burn lỗi: " + ex.Message);
                throw;
            }
        }

        public async Task<string> ApplyCtaTailOverlayOnlyAsync(
            string inputVideoPath,
            AppSettings settings,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputVideoPath) || !File.Exists(inputVideoPath))
            {
                return inputVideoPath;
            }

            try
            {
                var ffmpeg = ResolveFfmpeg(settings);
                var stage = workDirectory ?? Path.GetDirectoryName(inputVideoPath) ?? ".";
                Directory.CreateDirectory(stage);
                var ctaPath = AffiliateCtaAssetService.EnsureCartArrowAsset(settings?.StorageRootPath);
                var duration = await SubtitleTimingHelper.ProbeMediaDurationSecondsAsync(ffmpeg, inputVideoPath, cancellationToken)
                    .ConfigureAwait(false);
                if (duration <= CtaTailSeconds + 0.2d)
                {
                    return inputVideoPath;
                }

                var output = Path.Combine(stage, "with_cta_tail.mp4");
                var start = Math.Max(0d, duration - CtaTailSeconds);
                var filter = BuildCtaOverlayFilter(ctaPath, start, duration);
                await BurnVideoFilterAsync(ffmpeg, inputVideoPath, output, filter, log, cancellationToken).ConfigureAwait(false);
                log?.Invoke("[Affiliate Polish] Đã chèn CTA mũi tên (3s cuối).");
                return output;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Affiliate Polish] CTA bỏ qua: " + ex.Message);
                return inputVideoPath;
            }
        }

        /// <summary>Showcase: CTA mũi tên (3s cuối) + chữ CTA tuỳ chỉnh (vd. "Link giỏ hàng ở bio — số lượng có hạn!").</summary>
        public async Task<string> ApplyCtaTailWithTextOverlayAsync(
            string inputVideoPath,
            string ctaText,
            AppSettings settings,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputVideoPath) || !File.Exists(inputVideoPath))
            {
                return inputVideoPath;
            }

            try
            {
                var ffmpeg = ResolveFfmpeg(settings);
                var stage = workDirectory ?? Path.GetDirectoryName(inputVideoPath) ?? ".";
                Directory.CreateDirectory(stage);
                var ctaPath = AffiliateCtaAssetService.EnsureCartArrowAsset(settings?.StorageRootPath);
                var duration = await SubtitleTimingHelper.ProbeMediaDurationSecondsAsync(ffmpeg, inputVideoPath, cancellationToken)
                    .ConfigureAwait(false);
                if (duration <= CtaTailSeconds + 0.2d)
                {
                    return inputVideoPath;
                }

                var output = Path.Combine(stage, "with_cta_tail.mp4");
                var start = Math.Max(0d, duration - CtaTailSeconds);
                var filter = BuildCtaOverlayFilter(ctaPath, start, duration);
                if (!string.IsNullOrWhiteSpace(ctaText))
                {
                    filter += "," + BuildCtaTextFilter(ctaText, start, duration);
                }

                await BurnVideoFilterAsync(ffmpeg, inputVideoPath, output, filter, log, cancellationToken).ConfigureAwait(false);
                log?.Invoke("[Showcase] Đã chèn CTA (mũi tên + chữ) 3s cuối.");
                return output;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Showcase] CTA bỏ qua: " + ex.Message);
                return inputVideoPath;
            }
        }

        private static string BuildCtaTextFilter(string ctaText, double startSec, double endSec)
        {
            var safe = EscapeCtaDrawText(ctaText);
            var s = startSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var e = endSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return "drawbox=x=0:y=ih-200:w=iw:h=140:color=black@0.45:t=fill:enable='between(t," + s + "," + e + ")'" +
                   ",drawtext=font='Segoe UI Bold':text='" + safe + "':x=(w-text_w)/2:y=h-160:fontsize=52:fontcolor=white:borderw=3:bordercolor=black:enable='between(t," + s + "," + e + ")'";
        }

        private static string EscapeCtaDrawText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim()
                .Replace("\\", "\\\\")
                .Replace(":", "\\:")
                .Replace("'", "\u2019")
                .Replace("%", "\\%");
        }

        private static string ResolveFfmpeg(AppSettings settings)
        {
            var path = (settings?.FfmpegPath ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : "ffmpeg";
        }
    }
}
