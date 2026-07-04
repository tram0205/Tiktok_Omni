using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Anti-Detection cho Video Reup: metadata giả, lật ngang, tone nhạc +2%,
    /// crop bất đối xứng xóa QR code/watermark, vignette cinematic, nhiễu frame mạnh.
    /// </summary>
    public static class RemixScramblerService
    {
        public const double MusicPitchFactor = 1.02d;

        // ── Crop bất đối xứng ────────────────────────────────────────────────
        // Giữ 93% width và 93% height → duy trì tỉ lệ 9:16 gốc.
        // Left/Right mỗi bên 3.5%  → xóa logo/watermark cạnh trái-phải
        // Top 2%  → xóa username TikTok phía trên
        // Bottom = 1 - 0.93 - 0.02 = 5%  → xóa QR code phía dưới
        private const double CropKeep = 0.93d;       // 93% mỗi chiều
        private const double CropLeftFrac = 0.035d;  // x-offset 3.5%
        private const double CropTopFrac = 0.02d;    // y-offset 2% (bottom tự động = 5%)

        /// <summary>Filter FFmpeg cho nhạc nền (+2% pitch).</summary>
        public static string GetMusicPitchFilterChain()
        {
            var rate = (int)Math.Round(48000d * MusicPitchFactor);
            return "asetrate=" + rate.ToString(CultureInfo.InvariantCulture) + ",aresample=48000";
        }

        /// <summary>
        /// Crop bất đối xứng: cắt 5% dưới (QR), 2% trên (username), 3.5% mỗi bên,
        /// rồi scale về kích thước gốc. Tỉ lệ 9:16 được bảo toàn.
        /// </summary>
        public static string BuildCropZoomFilter()
        {
            var keepS  = CropKeep.ToString("0.####", CultureInfo.InvariantCulture);
            var leftS  = CropLeftFrac.ToString("0.######", CultureInfo.InvariantCulture);
            var topS   = CropTopFrac.ToString("0.######", CultureInfo.InvariantCulture);
            var invS   = (1d / CropKeep).ToString("0.######", CultureInfo.InvariantCulture);
            // trunc(x/2)*2 → số chẵn bắt buộc cho H.264
            return $"crop=iw*{keepS}:ih*{keepS}:iw*{leftS}:ih*{topS}," +
                   $"scale=trunc(iw*{invS}/2)*2:trunc(ih*{invS}/2)*2";
        }

        /// <summary>
        /// Bổ sung vào chuỗi video filter: crop bất đối xứng xóa QR/watermark,
        /// xoay nhẹ 0.5° (phá spatial hash), đổi fps→30 (phá temporal hash), nhiễu nhẹ.
        /// Thứ tự: cropZoom → [baseFilter] → rotate(0.5°) → fps=30 → noise.
        /// </summary>
        public static string AppendVideoAntiDetectionFilters(string baseVideoFilter)
        {
            // Xoay 0.5° — phá spatial/perceptual hash, góc bị che ~2-3px (không nhìn thấy)
            const string Rotate = "rotate=0.5*PI/180:fillcolor=black:ow=iw:oh=ih";
            // Đổi về 30fps — phá temporal fingerprint (nếu nguồn 60fps thì bỏ nửa frame)
            const string Fps = "fps=30";
            // Noise vừa đủ: c0s=7 (luminance) + c1s/c2s=3 (chroma) — phá hash mà không lộ ra mắt
            const string Noise = "noise=c0s=7:c1s=3:c2s=3:allf=t+u:c0f=t";
            var cropZoom = BuildCropZoomFilter();

            if (string.IsNullOrWhiteSpace(baseVideoFilter))
            {
                return cropZoom + "," + Rotate + "," + Fps + "," + Noise;
            }

            return cropZoom + "," + baseVideoFilter + "," + Rotate + "," + Fps + "," + Noise;
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

        /// <summary>
        /// Tạo filter_complex string để blend video nền mờ phía dưới video chính.
        /// Thứ tự: mainVfChain → [mv]; bg scale2ref → [bvscaled]; blend(3%) → [blended]; [subtitle?] → [vout].
        /// Trả về null nếu bgVideoPath không tồn tại.
        /// </summary>
        /// <param name="mainVfChain">Chuỗi filter cho video chính (không có label, không có subtitles).</param>
        /// <param name="bgVideoPath">Đường dẫn video nền (input [1:v], cần -stream_loop -1 ở ngoài).</param>
        /// <param name="subtitleFragment">Fragment subtitles='...' từ karaoke, hoặc null.</param>
        /// <param name="bgOpacity">Độ mờ video nền (0.03 = 3%).</param>
        /// <returns>Chuỗi -filter_complex kết thúc bằng label [vout], hoặc null nếu không có bg.</returns>
        public static string BuildBgBlendFilterComplex(
            string mainVfChain,
            string bgVideoPath,
            string subtitleFragment = null,
            double bgOpacity = 0.03d)
        {
            if (string.IsNullOrWhiteSpace(bgVideoPath) || !File.Exists(bgVideoPath))
            {
                return null;
            }

            var opStr = bgOpacity.ToString("0.##", CultureInfo.InvariantCulture);
            var sb = new System.Text.StringBuilder();

            // [0:v] = video chính → áp dụng toàn bộ anti-detection filters
            sb.Append("[0:v]").Append(mainVfChain).Append("[mv];");

            // [1:v] = video nền (stream_loop -1) → reset PTS, scale khớp kích thước [mv]
            sb.Append("[1:v]setpts=PTS-STARTPTS[bvraw];");
            sb.Append("[bvraw][mv]scale2ref=flags=lanczos[bvscaled][mv2];");

            // blend: bg (TOP, 3%) + main (BOTTOM, 97%) = subtle temporal noise
            sb.Append("[bvscaled][mv2]blend=all_mode=normal:all_opacity=").Append(opStr).Append("[blended]");

            // burn-in karaoke subtitle nếu có
            if (!string.IsNullOrWhiteSpace(subtitleFragment))
            {
                sb.Append(";[blended]").Append(subtitleFragment).Append("[vout]");
            }
            else
            {
                // null filter = pass-through, đặt tên output chuẩn [vout]
                sb.Append(";[blended]null[vout]");
            }

            return sb.ToString();
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
            // Pre-scramble: crop+zoom viền, chỉnh màu nhẹ, noise — lật ngang (hflip) thực hiện ở bước render cuối
            var vf = AppendVideoAntiDetectionFilters("eq=brightness=0.015:contrast=1.04:saturation=0.95:gamma=1.02");
            var args = "-y -i \"" + sourceVideoPath + "\" -vf \"" + vf + "\" -an -c:v libx264 -preset ultrafast -crf 22 -pix_fmt yuv420p" +
                       meta + " \"" + outPath + "\"";
            log?.Invoke("[RemixScrambler] Chuẩn bị nguồn anti-detection (crop+zoom viền, màu, noise, metadata giả — hflip ở render cuối)…");
            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
            return File.Exists(outPath) && new FileInfo(outPath).Length > 4096 ? outPath : sourceVideoPath;
        }

        private static readonly Random _rnd = new Random();
    }
}
