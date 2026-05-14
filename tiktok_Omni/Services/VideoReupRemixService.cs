using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Pipeline «reup» tách bước: Gemini hook → chỉnh tay → Lyria âm thanh → chọn nhạc → FFmpeg ghép.</summary>
    public sealed class VideoReupRemixService
    {
        private const string StageSourceMp4 = "source.mp4";
        private const string StageHookRawMp3 = "hook_raw.mp3";

        /// <summary>URL có vẻ là link http(s) tuyệt đối — dùng trước khi gọi TikWM tải video.</summary>
        public static bool LooksLikeHttpVideoUrl(string url)
        {
            var s = (url ?? string.Empty).Trim();
            if (s.Length < 12)
            {
                return false;
            }

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Xóa cache stage / file đã tải / WAV hook khi đổi URL hoặc làm mới nguồn.</summary>
        public static void ClearReupCachedMediaPaths(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            row.ReupStageFolder = string.Empty;
            row.ReupDownloadedVideoPath = string.Empty;
            row.ReupHookAudioPath = string.Empty;
            row.LastSourceVideoDurationSec = null;
            row.LastHookDurationUsedSec = null;
        }

        private static string GetOrCreateStageFolder(VideoReupRowItem row, string baseDir)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (!string.IsNullOrWhiteSpace(row.ReupStageFolder) && Directory.Exists(row.ReupStageFolder))
            {
                return row.ReupStageFolder;
            }

            var safe = VideoReupCaptionService.SanitizeFileNameFragment(row.ProductName);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = "video";
            }

            var h = (row.VideoUrl ?? string.Empty).GetHashCode().ToString("X8", CultureInfo.InvariantCulture);
            var dir = Path.Combine(baseDir, "VideoReup", "Stages", safe + "_" + h);
            Directory.CreateDirectory(dir);
            row.ReupStageFolder = dir;
            return dir;
        }

        /// <summary>Gemini viết hook (và gợi ý nhạc) — chỉ cần AI Key.</summary>
        public static bool TryValidateHookGeminiStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                errorMessage = "Cần AI API Key (Cài đặt) để Gemini viết hook.";
                return false;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var musicDir = Path.Combine(baseDir, "VideoReup", "Music");
            if (!Directory.Exists(musicDir) || Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly).Length == 0)
            {
                errorMessage = "Cần ít nhất một file .mp3 trong VideoReup\\Music (để Gemini gợi ý tên file nhạc).";
                return false;
            }

            return true;
        }

        /// <summary>Lyria đọc hook — cần Lyria, FFmpeg, URL video, câu hook không rỗng.</summary>
        public static bool TryValidateLyriaAudioStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.VideoUrl))
            {
                errorMessage = "Dòng chưa có URL video.";
                return false;
            }

            if (string.IsNullOrWhiteSpace((row.ReupHookDraft ?? string.Empty).Trim()))
            {
                errorMessage = "Chưa có câu hook — hãy tạo/chỉnh hook trước.";
                return false;
            }

            if (!HasLyriaCredentials(settings))
            {
                errorMessage = "Cần Lyria (API Key + Endpoint trong Cài đặt).";
                return false;
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            if (string.IsNullOrWhiteSpace(ffmpeg))
            {
                errorMessage = "Chưa cấu hình FFmpeg Path trong Cài đặt (hoặc đặt ffmpeg.exe trên PATH).";
                return false;
            }

            if (!string.Equals(ffmpeg, "ffmpeg", StringComparison.OrdinalIgnoreCase) && !File.Exists(ffmpeg))
            {
                errorMessage = "Không tìm thấy ffmpeg.exe — hãy cấu hình FFmpeg Path trong Cài đặt.";
                return false;
            }

            return true;
        }

        /// <summary>Ghép video cuối — cần video nguồn đã tải, WAV hook, nhạc đã chọn, FFmpeg.</summary>
        public static bool TryValidateFinalRenderStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.VideoUrl))
            {
                errorMessage = "Dòng chưa có URL video.";
                return false;
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            if (string.IsNullOrWhiteSpace(ffmpeg))
            {
                errorMessage = "Chưa cấu hình FFmpeg Path trong Cài đặt (hoặc đặt ffmpeg.exe trên PATH).";
                return false;
            }

            if (!string.Equals(ffmpeg, "ffmpeg", StringComparison.OrdinalIgnoreCase) && !File.Exists(ffmpeg))
            {
                errorMessage = "Không tìm thấy ffmpeg.exe — hãy cấu hình FFmpeg Path trong Cài đặt.";
                return false;
            }

            if (string.IsNullOrWhiteSpace((row.ReupDownloadedVideoPath ?? string.Empty).Trim()) || !File.Exists(row.ReupDownloadedVideoPath))
            {
                errorMessage = "Chưa có video nguồn đã tải — nhập URL (rời ô để tải), hoặc đợi tải xong sau «Nhập từ Săn Affiliate», hoặc chạy «Lyria đọc hook» (cũng tải video).";
                return false;
            }

            if (string.IsNullOrWhiteSpace((row.ReupHookAudioPath ?? string.Empty).Trim()) || !File.Exists(row.ReupHookAudioPath))
            {
                errorMessage = "Chưa có file âm thanh hook (WAV) — chạy «Lyria đọc hook» trước.";
                return false;
            }

            if (string.IsNullOrWhiteSpace((row.ReupSelectedMusicFile ?? string.Empty).Trim()))
            {
                errorMessage = "Hãy chọn file nhạc nền (.mp3) trong danh sách.";
                return false;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var musicDir = Path.Combine(baseDir, "VideoReup", "Music");
            var musicFiles = Directory.Exists(musicDir)
                ? Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            if (musicFiles.Length == 0)
            {
                errorMessage = "Cần ít nhất một file .mp3 trong VideoReup\\Music.";
                return false;
            }

            var names = musicFiles.Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var pick = PickMusicPath(musicFiles, row.ReupSelectedMusicFile, names, _ => { });
            if (string.IsNullOrWhiteSpace(pick) || !File.Exists(pick))
            {
                errorMessage = "File nhạc đã chọn không tồn tại trong VideoReup\\Music.";
                return false;
            }

            return true;
        }

        /// <summary>Kiểm tra trước khi render cuối (tương thích tên cũ).</summary>
        public static bool TryValidateRemixPrerequisites(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            return TryValidateFinalRenderStep(row, settings, out errorMessage);
        }
        private static readonly object RngLock = new object();
        private static readonly Random Rng = new Random();

        private static int NextCrf()
        {
            lock (RngLock)
            {
                return Rng.Next(19, 24);
            }
        }

        private static string RandomMetaToken(string prefix)
        {
            lock (RngLock)
            {
                return prefix + Guid.NewGuid().ToString("N");
            }
        }

        /// <summary>Tham số video cố định theo yêu cầu sản phẩm.</summary>
        private static class ReupVideoSpec
        {
            public const double TrimHeadSeconds = 1d;
            public const double TrimTailSeconds = 1d;
            public const double HookMinSec = 4d;
            public const double HookMaxSec = 7d;
            public const double MusicBedVolume = 0.22d;
            public const double MinMusicTailSec = 0.5d;

            /// <summary>Bật tốc độ ~+5% (setpts). Mặc định tắt để khớp mô tả «chỉ cắt/lật/màu/tiếng».</summary>
            public const bool ApplySpeedChange = false;

            public const double SpeedFactor = 1.05d;
        }

        private readonly VideoService _videoService = new VideoService();

        public async Task GenerateHookAndSuggestMusicAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!TryValidateHookGeminiStep(row, settings, out var err))
            {
                throw new InvalidOperationException(err);
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var musicDir = Path.Combine(baseDir, "VideoReup", "Music");
            var musicFiles = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly);
            var musicNames = musicFiles.Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var pick = await ResolveHookAndMusicAsync(
                row,
                musicNames,
                settings,
                gemini,
                log,
                cancellationToken).ConfigureAwait(false);
            row.ReupHookDraft = (pick.HookLine ?? string.Empty).Trim();
            row.ReupSuggestedMusicFile = (pick.MusicFile ?? string.Empty).Trim();
        }

        /// <summary>Tải video nguồn về thư mục stage (source.mp4) và cập nhật độ dài.</summary>
        public async Task EnsureVideoDownloadedAsync(
            VideoReupRowItem row,
            AppSettings settings,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (string.IsNullOrWhiteSpace(row.VideoUrl))
            {
                throw new InvalidOperationException("Dòng chưa có URL video.");
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var stage = GetOrCreateStageFolder(row, baseDir);
            var target = Path.Combine(stage, StageSourceMp4);
            var ffmpeg = ResolveFfmpegPath(settings);

            if (File.Exists(target) && new FileInfo(target).Length > 10_000L)
            {
                row.ReupDownloadedVideoPath = target;
                row.LastSourceVideoDurationSec = await ProbeMediaDurationSecondsAsync(ffmpeg, target, cancellationToken).ConfigureAwait(false);
                log?.Invoke("[VideoReup] Đã có video nguồn trong stage — dùng lại.");
                return;
            }

            log?.Invoke("[VideoReup] Đang tải video nguồn (TikWM)...");
            var dl = await affiliateHunter.DownloadAffiliateVideoAsync(row.VideoUrl, stage, log, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(dl, target, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(target))
                {
                    try
                    {
                        File.Delete(target);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                File.Copy(dl, target, true);
                try
                {
                    File.Delete(dl);
                }
                catch
                {
                    // ignored
                }
            }

            row.ReupDownloadedVideoPath = target;
            var totalDur = await ProbeMediaDurationSecondsAsync(ffmpeg, target, cancellationToken).ConfigureAwait(false);
            if (totalDur < ReupVideoSpec.TrimHeadSeconds + ReupVideoSpec.TrimTailSeconds + 1.5d)
            {
                throw new InvalidOperationException($"Video quá ngắn ({totalDur:0.0}s) — không đủ cắt đầu/cuối 1s mỗi bên.");
            }

            row.LastSourceVideoDurationSec = totalDur;
        }

        /// <summary>Lyria đọc câu hook trong <see cref="VideoReupRowItem.ReupHookDraft"/> → WAV chuẩn hóa (ghi <see cref="VideoReupRowItem.ReupHookAudioPath"/>).</summary>
        public async Task BuildLyriaHookAudioAsync(
            VideoReupRowItem row,
            AppSettings settings,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!TryValidateLyriaAudioStep(row, settings, out var err))
            {
                throw new InvalidOperationException(err);
            }

            await EnsureVideoDownloadedAsync(row, settings, affiliateHunter, log, cancellationToken).ConfigureAwait(false);

            var ffmpeg = ResolveFfmpegPath(settings);
            var stage = row.ReupStageFolder;
            var hookRaw = Path.Combine(stage, StageHookRawMp3);
            await BuildHookAudioAsync(
                row.ReupHookDraft,
                hookRaw,
                settings,
                log,
                cancellationToken).ConfigureAwait(false);

            var totalDur = row.LastSourceVideoDurationSec ?? 0d;
            var trimContentSeconds = totalDur - ReupVideoSpec.TrimHeadSeconds - ReupVideoSpec.TrimTailSeconds;
            var videoOutSeconds = ReupVideoSpec.ApplySpeedChange
                ? trimContentSeconds / ReupVideoSpec.SpeedFactor
                : trimContentSeconds;

            var hookNormWav = await NormalizeHookAudioToWavAsync(
                ffmpeg,
                hookRaw,
                stage,
                videoOutSeconds,
                log,
                cancellationToken).ConfigureAwait(false);

            var hookUsed = await ProbeMediaDurationSecondsAsync(ffmpeg, hookNormWav, cancellationToken).ConfigureAwait(false);
            if (hookUsed < ReupVideoSpec.HookMinSec - 0.15d)
            {
                throw new InvalidOperationException($"Hook sau chuẩn hóa quá ngắn ({hookUsed:0.##}s).");
            }

            row.ReupHookAudioPath = hookNormWav;
            row.LastHookDurationUsedSec = hookUsed;
            log?.Invoke("[VideoReup] Hook âm thanh (WAV) sẵn sàng cho bước render.");
        }

        /// <summary>Ghép nhạc + video re-encode + mux MP4 cuối (dùng file đã chuẩn bị trong stage).</summary>
        public async Task<VideoReupRemixResult> RenderFinalVideoAsync(
            VideoReupRowItem row,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!TryValidateFinalRenderStep(row, settings, out var preErr))
            {
                throw new InvalidOperationException(preErr);
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var musicDir = Path.Combine(baseDir, "VideoReup", "Music");
            var musicFiles = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly);
            var musicNames = musicFiles.Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var downloadedPath = row.ReupDownloadedVideoPath;
            var hookNormWav = row.ReupHookAudioPath;

            var totalDur = await ProbeMediaDurationSecondsAsync(ffmpeg, downloadedPath, cancellationToken).ConfigureAwait(false);
            if (totalDur < ReupVideoSpec.TrimHeadSeconds + ReupVideoSpec.TrimTailSeconds + 1.5d)
            {
                throw new InvalidOperationException($"Video quá ngắn ({totalDur:0.0}s) — không đủ cắt đầu/cuối 1s mỗi bên.");
            }

            var trimContentSeconds = totalDur - ReupVideoSpec.TrimHeadSeconds - ReupVideoSpec.TrimTailSeconds;
            var videoOutSeconds = ReupVideoSpec.ApplySpeedChange
                ? trimContentSeconds / ReupVideoSpec.SpeedFactor
                : trimContentSeconds;
            log?.Invoke(
                ReupVideoSpec.ApplySpeedChange
                    ? $"[VideoReup] Thời lượng gốc {totalDur:0.##}s → sau cắt {trimContentSeconds:0.##}s → sau tốc độ x{ReupVideoSpec.SpeedFactor:0.##} ≈ {videoOutSeconds:0.##}s."
                    : $"[VideoReup] Thời lượng gốc {totalDur:0.##}s → sau cắt đầu/đuôi 1s ≈ {videoOutSeconds:0.##}s (không đổi tốc độ).");

            var hookUsed = await ProbeMediaDurationSecondsAsync(ffmpeg, hookNormWav, cancellationToken).ConfigureAwait(false);
            if (hookUsed < ReupVideoSpec.HookMinSec - 0.15d)
            {
                throw new InvalidOperationException($"Hook WAV quá ngắn ({hookUsed:0.##}s).");
            }

            var musicPath = PickMusicPath(musicFiles, row.ReupSelectedMusicFile, musicNames, log);
            var musicSec = Math.Max(ReupVideoSpec.MinMusicTailSec, videoOutSeconds - hookUsed);
            log?.Invoke($"[VideoReup] Ghép audio: hook {hookUsed:0.##}s + nhạc {musicSec:0.##}s (video ≈{videoOutSeconds:0.##}s).");

            var stage = row.ReupStageFolder;
            var fullAudioWav = Path.Combine(stage, "full_audio.wav");
            await BuildCompositeAudioAsync(
                ffmpeg,
                hookNormWav,
                musicPath,
                hookUsed,
                musicSec,
                fullAudioWav,
                log,
                cancellationToken).ConfigureAwait(false);

            var compositeDur = await ProbeMediaDurationSecondsAsync(ffmpeg, fullAudioWav, cancellationToken).ConfigureAwait(false);
            var drift = Math.Abs(compositeDur - videoOutSeconds);
            if (drift > 0.35d)
            {
                log?.Invoke($"[VideoReup] Cảnh báo: độ dài track audio {compositeDur:0.##}s lệch video {videoOutSeconds:0.##}s khoảng {drift:0.##}s — mux dùng -shortest.");
            }

            var videoNoAudio = Path.Combine(stage, "video_noaudio.mp4");
            var vf = ReupVideoSpec.ApplySpeedChange
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "setpts=PTS/{0},hflip,eq=brightness=0.02:contrast=1.03:saturation=0.97",
                    ReupVideoSpec.SpeedFactor.ToString("0.#####", CultureInfo.InvariantCulture))
                : "hflip,eq=brightness=0.02:contrast=1.03:saturation=0.97";

            var crf = NextCrf();
            var metaComment = RandomMetaToken("reup");
            var trimT = trimContentSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var vArgs = "-y -ss " + ReupVideoSpec.TrimHeadSeconds.ToString("0.#####", CultureInfo.InvariantCulture) +
                        " -i \"" + downloadedPath + "\" -t " + trimT +
                        " -vf \"" + vf + "\" -an -c:v libx264 -preset medium -crf " + crf.ToString(CultureInfo.InvariantCulture) +
                        " -pix_fmt yuv420p -metadata comment=" + metaComment +
                        " \"" + videoNoAudio + "\"";

            log?.Invoke("[VideoReup] FFmpeg: cắt 1s đầu/đuôi, lật ngang, chỉnh màu nhẹ, bỏ audio gốc (re-encode)...");
            await RunFfmpegAsync(ffmpeg, vArgs, log, cancellationToken).ConfigureAwait(false);

            var outDir = Path.Combine(baseDir, "VideoReup", "Output");
            Directory.CreateDirectory(outDir);
            var safeName = VideoReupCaptionService.SanitizeFileNameFragment(row.ProductName);
            var finalPath = Path.Combine(outDir, "reup_" + safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".mp4");

            var muxMeta = RandomMetaToken("mux");
            var muxArgs = "-y -i \"" + videoNoAudio + "\" -i \"" + fullAudioWav + "\"" +
                          " -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -shortest" +
                          " -metadata title=reup -metadata comment=" + muxMeta +
                          " \"" + finalPath + "\"";

            log?.Invoke("[VideoReup] FFmpeg: ghép video + audio (hook + nhạc)...");
            await RunFfmpegAsync(ffmpeg, muxArgs, log, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(finalPath))
            {
                throw new InvalidOperationException("Không tạo được file output.");
            }

            var outFileDur = await ProbeMediaDurationSecondsAsync(ffmpeg, finalPath, cancellationToken).ConfigureAwait(false);
            log?.Invoke("[VideoReup] Xong: " + finalPath + $" (≈{outFileDur:0.##}s)");
            return new VideoReupRemixResult
            {
                OutputPath = finalPath,
                SourceDurationSeconds = totalDur,
                ProcessedVideoDurationSeconds = videoOutSeconds,
                HookDurationSecondsUsed = hookUsed,
                OutputFileDurationSeconds = outFileDur
            };
        }

        private static async Task<string> NormalizeHookAudioToWavAsync(
            string ffmpegExe,
            string inputPath,
            string workRoot,
            double videoOutSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var rawDur = await ProbeMediaDurationSecondsAsync(ffmpegExe, inputPath, cancellationToken).ConfigureAwait(false);
            if (rawDur < 0.15d)
            {
                throw new InvalidOperationException("Không đọc được thời lượng hook audio (Lyria).");
            }

            var maxHook = Math.Min(ReupVideoSpec.HookMaxSec, videoOutSeconds - ReupVideoSpec.MinMusicTailSec);
            if (maxHook < ReupVideoSpec.HookMinSec - 0.02d)
            {
                throw new InvalidOperationException(
                    $"Video sau xử lý ≈{videoOutSeconds:0.##}s — không đủ chỗ cho hook tối thiểu {ReupVideoSpec.HookMinSec}s và nhạc.");
            }

            var trimTarget = Math.Min(rawDur, maxHook);
            var trimmedWav = Path.Combine(workRoot, "hook_trim.wav");
            var trimStr = trimTarget.ToString("0.#####", CultureInfo.InvariantCulture);
            var trimArgs = "-y -i \"" + inputPath + "\" -af \"atrim=0:" + trimStr + ",asetpts=PTS-STARTPTS,aresample=48000\" \"" + trimmedWav + "\"";
            log?.Invoke($"[VideoReup] Chuẩn hóa hook: Lyria raw {rawDur:0.##}s → cắt tối đa {trimTarget:0.##}s (trần {maxHook:0.##}s).");
            await RunFfmpegAsync(ffmpegExe, trimArgs, log, cancellationToken).ConfigureAwait(false);

            var durAfterTrim = await ProbeMediaDurationSecondsAsync(ffmpegExe, trimmedWav, cancellationToken).ConfigureAwait(false);
            var outWav = Path.Combine(workRoot, "hook_norm.wav");
            if (File.Exists(outWav))
            {
                try
                {
                    File.Delete(outWav);
                }
                catch
                {
                    // ignored
                }
            }

            if (durAfterTrim < ReupVideoSpec.HookMinSec - 0.08d)
            {
                var wholeSamples = (int)Math.Round(ReupVideoSpec.HookMinSec * 48000d, MidpointRounding.AwayFromZero);
                var padArgs = "-y -i \"" + trimmedWav + "\" -af \"apad=whole_len=" + wholeSamples.ToString(CultureInfo.InvariantCulture) + ",aresample=48000\" \"" + outWav + "\"";
                log?.Invoke($"[VideoReup] Pad hook im lặng lên tối thiểu {ReupVideoSpec.HookMinSec:0.#}s.");
                await RunFfmpegAsync(ffmpegExe, padArgs, log, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                File.Copy(trimmedWav, outWav, true);
            }

            return outWav;
        }

        private static string PickMusicPath(
            IReadOnlyList<string> musicFiles,
            string requestedName,
            IReadOnlyList<string> musicNames,
            Action<string> log)
        {
            var want = (requestedName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(want))
            {
                foreach (var f in musicFiles)
                {
                    if (string.Equals(Path.GetFileName(f), want, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                }

                log?.Invoke("[VideoReup] Gemini chọn nhạc «" + want + "» không khớp file — dùng file đầu tiên.");
            }

            return musicFiles[0];
        }

        private sealed class HookMusicPick
        {
            public string HookLine { get; set; } = string.Empty;
            public string MusicFile { get; set; } = string.Empty;
        }

        private static async Task<HookMusicPick> ResolveHookAndMusicAsync(
            VideoReupRowItem row,
            List<string> musicFileNames,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var ctx = new StringBuilder();
            ctx.AppendLine("Tên sản phẩm / chủ đề: " + (row.ProductName ?? string.Empty).Trim());
            ctx.AppendLine("Hashtag: " + (row.Hashtags ?? string.Empty).Trim());
            ctx.AppendLine("Script / mô tả: " + Trim((row.VideoScript ?? string.Empty).Trim(), 1500));

            var list = string.Join(" | ", musicFileNames);
            var prompt =
                "Bạn là đạo diễn âm thanh TikTok affiliate. Dựa vào mô tả sau, hãy:\r\n" +
                "1) Viết MỘT câu mở đầu (hook) tiếng Việt, giọng nữ nhẹ nhàng, êm ái, thu hút, đọc trong khoảng 4 đến 7 giây (khoảng 18–36 từ, một dòng, không markdown, không xuống dòng).\r\n" +
                "2) Chọn ĐÚNG MỘT file nhạc nền từ danh sách tên file sau (phải trùng ký tự, gồm cả .mp3):\r\n" +
                list + "\r\n\r\n" +
                "Trả về DUY NHẤT một JSON, không markdown, không giải thích. Định dạng:\r\n" +
                "{\"hook\":\"...\",\"musicFile\":\"tên-file.mp3\"}\r\n\r\n" +
                "Mô tả video / sản phẩm:\r\n" + ctx;

            log?.Invoke("[VideoReup] Gemini: đang chọn hook + nhạc...");
            var raw = await gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            var json = ExtractJsonObject(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Gemini không trả JSON hợp lệ cho hook/nhạc.");
            }

            JObject o;
            try
            {
                o = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không parse được JSON hook/nhạc: " + ex.Message);
            }

            var hook = (o["hook"] ?? o["Hook"])?.ToString()?.Trim() ?? string.Empty;
            var music = (o["musicFile"] ?? o["musicfile"] ?? o["music"])?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(hook))
            {
                hook = "Cùng mình khám phá ngay nhé, bạn sẽ thích đấy.";
            }

            log?.Invoke("[VideoReup] Hook: " + hook);
            log?.Invoke("[VideoReup] Nhạc (theo Gemini): " + (string.IsNullOrEmpty(music) ? "(trống)" : music));
            return new HookMusicPick { HookLine = hook, MusicFile = music };
        }

        private static string ExtractJsonObject(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
                if (end > fence)
                {
                    s = s.Substring(fence + 3, end - fence - 3).Trim();
                    if (s.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    {
                        s = s.Substring(4).TrimStart();
                    }
                }
            }

            var i0 = s.IndexOf('{');
            var i1 = s.LastIndexOf('}');
            if (i0 >= 0 && i1 > i0)
            {
                return s.Substring(i0, i1 - i0 + 1);
            }

            return s;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
            {
                return s ?? string.Empty;
            }

            return s.Substring(0, max) + "...";
        }

        private async Task BuildHookAudioAsync(
            string hookLine,
            string outputAudioFile,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!HasLyriaCredentials(settings))
            {
                throw new InvalidOperationException("Cần Lyria (API Key + Endpoint) để tạo hook — luồng remix không dùng TTS Google.");
            }

            try
            {
                log?.Invoke("[VideoReup] Hook: Lyria (giọng nữ nhẹ, êm) — 4–7s mục tiêu.");
                await BuildLyriaHookMp3Async(
                    hookLine,
                    outputAudioFile,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
                if (!File.Exists(outputAudioFile) || new FileInfo(outputAudioFile).Length < 800)
                {
                    throw new InvalidOperationException("Lyria trả file hook quá nhỏ hoặc rỗng.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không tạo được hook bằng Lyria: " + ex.Message, ex);
            }
        }

        private static bool HasLyriaCredentials(AppSettings settings)
        {
            return !string.IsNullOrWhiteSpace(settings?.LyriaApiKey) &&
                   !string.IsNullOrWhiteSpace(settings?.LyriaEndpoint);
        }

        private async Task BuildLyriaHookMp3Async(
            string hookLine,
            string outputAudioFile,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var line = (hookLine ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                line = "Cùng mình khám phá ngay nhé!";
            }

            var lyriaScript =
                "Giọng đọc tiếng Việt nữ, nhẹ nhàng êm ái, ấm và rõ chữ, phong cách host TikTok mở đầu video. " +
                "Đọc với tốc độ tự nhiên để câu thoại kéo dài khoảng 4 đến 7 giây khi đọc (không quá vội). " +
                "Chỉ đọc một lần, không thêm lời ngoài câu sau: " + line;

            var audioUrl = await _videoService.GenerateAudioAsync(
                lyriaScript,
                settings.LyriaApiKey.Trim(),
                settings.LyriaEndpoint.Trim(),
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(audioUrl))
            {
                throw new InvalidOperationException("Lyria trả audioUrl rỗng.");
            }

            if (File.Exists(outputAudioFile))
            {
                File.Delete(outputAudioFile);
            }

            await DownloadUrlToFileAsync(audioUrl, outputAudioFile, cancellationToken).ConfigureAwait(false);
            log?.Invoke("[VideoReup] Hook Lyria OK → " + Path.GetFileName(outputAudioFile));
        }

        private static async Task DownloadUrlToFileAsync(string url, string outputPath, CancellationToken cancellationToken)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0");
                cancellationToken.Register(() => webClient.CancelAsync());
                await webClient.DownloadFileTaskAsync(new Uri(url), outputPath).ConfigureAwait(false);
            }
        }

        private static async Task BuildCompositeAudioAsync(
            string ffmpegExe,
            string hookMp3Path,
            string musicMp3Path,
            double hookTargetSec,
            double musicSec,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (musicSec <= 0.05d)
            {
                var onlyHook = "-y -i \"" + hookMp3Path + "\" -af \"atrim=0:" +
                               hookTargetSec.ToString("0.#####", CultureInfo.InvariantCulture) +
                               ",asetpts=PTS-STARTPTS,aresample=48000\" \"" + outputWav + "\"";
                await RunFfmpegAsync(ffmpegExe, onlyHook, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            var fadeDur = Math.Min(2d, Math.Max(0.25d, musicSec * 0.45d));
            var fadeOutStart = Math.Max(0d, musicSec - fadeDur);
            var fadeOutStartStr = fadeOutStart.ToString("0.#####", CultureInfo.InvariantCulture);
            var fadeDurStr = fadeDur.ToString("0.#####", CultureInfo.InvariantCulture);
            var musicSecStr = musicSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var hookStr = hookTargetSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var volStr = ReupVideoSpec.MusicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);

            var filter =
                "[0:a]atrim=0:" + hookStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=1[vo];" +
                "[1:a]atrim=0:" + musicSecStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + volStr +
                ",afade=t=out:st=" + fadeOutStartStr + ":d=" + fadeDurStr + "[bg];" +
                "[vo][bg]concat=n=2:v=0:a=1[aout]";

            var args = "-y -i \"" + hookMp3Path + "\" -i \"" + musicMp3Path + "\"" +
                       " -filter_complex \"" + filter + "\"" +
                       " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";

            log?.Invoke("[VideoReup] FFmpeg: tạo track audio (hook + nhạc)...");
            await RunFfmpegAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static string ResolveFfmpegPath(AppSettings settings)
        {
            var p = (settings?.FfmpegPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                return p;
            }

            return "ffmpeg";
        }

        private static string ResolveFfprobePath(string ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                return "ffprobe";
            }

            try
            {
                if (ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var probe = ffmpegPath.Substring(0, ffmpegPath.Length - "ffmpeg.exe".Length) + "ffprobe.exe";
                    if (File.Exists(probe))
                    {
                        return probe;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return "ffprobe";
        }

        private static async Task<double> ProbeMediaDurationSecondsAsync(string ffmpegPath, string mediaPath, CancellationToken cancellationToken)
        {
            var ffprobe = ResolveFfprobePath(ffmpegPath);
            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + mediaPath + "\"";
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
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                }

                var output = (await outputTask.ConfigureAwait(false) ?? string.Empty).Trim();
                if (double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    return Math.Max(0d, seconds);
                }
            }

            return 0d;
        }

        private static async Task RunFfmpegAsync(string ffmpegExecutable, string args, Action<string> log, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var stdOut = process.StandardOutput.ReadToEndAsync();
                var stdErr = process.StandardError.ReadToEndAsync();

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }

                var outText = await stdOut.ConfigureAwait(false);
                var errText = await stdErr.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "FFmpeg lỗi: " + (string.IsNullOrWhiteSpace(errText) ? outText : errText));
                }

                if (!string.IsNullOrWhiteSpace(errText))
                {
                    var line = errText.Split('\n').LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        log?.Invoke("FFmpeg: " + line.Trim());
                    }
                }
            }
        }
    }
}
