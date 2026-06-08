using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Anti-Detection cho Video Reup: metadata giả, lật ngang, tone nhạc +2%, nhiễu frame nhẹ.</summary>
    public static class RemixScramblerService
    {
        public const double MusicPitchFactor = 1.02d;

        /// <summary>Filter FFmpeg cho nhạc nền (+2% pitch).</summary>
        public static string GetMusicPitchFilterChain()
        {
            var rate = (int)Math.Round(48000d * MusicPitchFactor);
            return "asetrate=" + rate.ToString(CultureInfo.InvariantCulture) + ",aresample=48000";
        }

        /// <summary>Bổ sung vào chuỗi video filter (sau hflip): nhiễu frame nhẹ chống fingerprint.</summary>
        public static string AppendVideoAntiDetectionFilters(string baseVideoFilter)
        {
            var noise = "noise=c0s=5:allf=t+u:c0f=t";
            if (string.IsNullOrWhiteSpace(baseVideoFilter))
            {
                return noise;
            }

            return baseVideoFilter + "," + noise;
        }

        public static string BuildFakeMetadataArgs()
        {
            var token = Guid.NewGuid().ToString("N").Substring(0, 12);
            var ts = DateTime.UtcNow.AddDays(-_rnd.Next(3, 90)).ToString("yyyy-MM-ddTHH:mm:ss.000000Z", CultureInfo.InvariantCulture);
            return " -metadata title=clip_" + token +
                   " -metadata artist=omni_render_" + _rnd.Next(1000, 9999) +
                   " -metadata comment=reup_" + token +
                   " -metadata creation_time=" + ts;
        }

        /// <summary>Tạo bản copy nguồn đã scramble (metadata + re-mux nhẹ) trước khi render chính.</summary>
        public static async Task<string> PrepareScrambledSourceAsync(
            string ffmpegExe,
            string sourceVideoPath,
            string stageFolder,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceVideoPath) || !File.Exists(sourceVideoPath))
            {
                return sourceVideoPath;
            }

            Directory.CreateDirectory(stageFolder ?? ".");
            var outPath = Path.Combine(stageFolder, "source_scrambled.mp4");
            var meta = BuildFakeMetadataArgs();
            var vf = AppendVideoAntiDetectionFilters("hflip,eq=brightness=0.01:contrast=1.02");
            var args = "-y -i \"" + sourceVideoPath + "\" -vf \"" + vf + "\" -an -c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p" +
                       meta + " \"" + outPath + "\"";
            log?.Invoke("[RemixScrambler] Chuẩn bị nguồn anti-detection (metadata giả + hflip + noise)…");
            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
            return File.Exists(outPath) && new FileInfo(outPath).Length > 4096 ? outPath : sourceVideoPath;
        }

        private static readonly Random _rnd = new Random();
    }
}
