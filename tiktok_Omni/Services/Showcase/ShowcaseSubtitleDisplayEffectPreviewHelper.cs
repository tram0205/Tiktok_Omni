using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseSubtitleDisplayEffectPreviewRequest
    {
        public string SubtitleText { get; set; } = string.Empty;

        public string SceneLabel { get; set; } = string.Empty;

        public bool IsHook { get; set; }

        public bool StyleEnabled { get; set; } = true;

        public string EffectStorage { get; set; } = string.Empty;

        public ShowcaseSubtitleLineRenderOverride LineStyle { get; set; }
    }

    /// <summary>Clip ngắn 1080×1920 burn-in ASS — cùng engine hiệu ứng render Showcase.</summary>
    public static class ShowcaseSubtitleDisplayEffectPreviewHelper
    {
        private const int PlayResX = 1080;
        private const int PlayResY = 1920;

        public static async Task<string> RenderPreviewClipAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            ShowcaseSubtitleDisplayEffectPreviewRequest request,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (video == null)
            {
                throw new ArgumentNullException(nameof(video));
            }

            request = request ?? new ShowcaseSubtitleDisplayEffectPreviewRequest();
            var text = (request.SubtitleText ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                throw new InvalidOperationException("Dòng này chưa có chữ phụ đề để xem thử.");
            }

            if (!request.StyleEnabled)
            {
                throw new InvalidOperationException("Phụ đề đang tắt (cột Bật) — bật lại rồi xem thử.");
            }

            if (!VideoReupRemixService.TryValidateFfmpegToolkit(settings, out var ffmpegErr))
            {
                throw new InvalidOperationException(ffmpegErr);
            }

            var ffmpeg = (settings.FfmpegPath ?? string.Empty).Trim();
            var words = SubtitleTimingHelper.SplitWords(text);
            if (words.Count == 0)
            {
                throw new InvalidOperationException("Không tách được từ trong phụ đề.");
            }

            var durationMs = Math.Max(3500d, Math.Min(14000d, words.Count * 420d + 1200d));
            var timestamps = SubtitleTimingHelper.EstimateWordTimestamps(text, durationMs);
            if (timestamps.Count == 0)
            {
                throw new InvalidOperationException("Không tạo được timing xem thử.");
            }

            var opts = request.IsHook
                ? ShowcaseSubtitleStyleHelper.BuildHookOptionsWithLineOverride(
                    video,
                    settings,
                    request.EffectStorage)
                : ShowcaseSubtitleStyleHelper.BuildBodyOptionsWithLineOverride(
                    video,
                    settings,
                    request.EffectStorage,
                    request.LineStyle);

            opts.PlayResX = PlayResX;
            opts.PlayResY = PlayResY;
            opts.WordsPerLine = Math.Max(4, Math.Min(12, video.ShowcaseSubtitleWordsPerLine > 0
                ? video.ShowcaseSubtitleWordsPerLine
                : 6));
            opts.RhythmicLineBreaks = true;

            var workDir = Path.Combine(
                Path.GetTempPath(),
                "tiktok_Omni",
                "subtitle_effect_preview",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            var assPath = Path.Combine(workDir, "preview.ass");
            var mp4Path = Path.Combine(workDir, "preview.mp4");

            AssSubtitleGenerator.WriteAssFile(assPath, timestamps, opts);

            var durationSec = (durationMs / 1000d + 0.35d).ToString("0.###", CultureInfo.InvariantCulture);
            var esc = KaraokeAssSubtitleService.EscapePathForFfmpegSubtitleFilter(assPath);
            var bg = "0x1f222a";
            var vf = "subtitles='" + esc + "'";
            var args = "-y -f lavfi -i color=c=" + bg + ":s=" + PlayResX + "x" + PlayResY + ":d=" + durationSec +
                       " -vf \"" + vf + "\" -t " + durationSec +
                       " -c:v libx264 -preset ultrafast -crf 22 -pix_fmt yuv420p -an \"" + mp4Path + "\"";

            log?.Invoke("[Xem thử phụ đề] FFmpeg…");
            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, log, cancellationToken)
                .ConfigureAwait(false);

            if (!File.Exists(mp4Path))
            {
                throw new InvalidOperationException("Không tạo được clip xem thử.");
            }

            return mp4Path;
        }

        public static void TryOpenInDefaultPlayer(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }
}
