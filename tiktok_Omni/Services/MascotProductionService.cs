using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Pipeline mascot cảm xúc: Gemini (kịch bản) → ElevenLabs (giọng) → Lip-sync (tùy chọn) → FFmpeg (hậu kỳ).
    /// </summary>
    public sealed class MascotProductionService : IDisposable
    {
        private const double TargetDurationSeconds = 45d;

        private readonly HttpClient _httpClient;
        private readonly VideoService _videoService = new VideoService();
        private readonly LipSyncService _lipSyncService = new LipSyncService();
        private bool _disposed;

        public MascotProductionService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<string> CreateEmotionalMascotVideoAsync(
            MascotStoryJobPayload payload,
            AppSettings settings,
            Action<string> logger,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(payload.MascotImagePath) || !File.Exists(payload.MascotImagePath))
            {
                throw new FileNotFoundException("Ảnh mascot không tồn tại.", payload.MascotImagePath);
            }

            logger = logger ?? (_ => { });
            var profile = string.IsNullOrWhiteSpace(payload.ProfileName) ? "default" : payload.ProfileName.Trim();
            var workDir = Path.Combine(
                payload.StorageRootPath,
                "Generated",
                profile,
                "Mascot",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(workDir);

            cancellationToken.ThrowIfCancellationRequested();

            logger("Director: Đang soạn kịch bản đánh vào tâm lý người xem...");
            var gemini = new GeminiService();
            var prompt = $@"Viết kịch bản video ngắn (45s) về chủ đề: {payload.ChannelTheme}.
Yêu cầu: Sử dụng cấu trúc 'The Hero's Journey' (Nỗi đau -> Sự thật -> Hy vọng).
Ngôn ngữ: Tiếng Việt sâu sắc, truyền cảm hứng.
Trả lời duy nhất 1 chuỗi kịch bản lời thoại.";

            var script = await gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(workDir, "script.txt"), script ?? string.Empty, TextFileEncoding.Utf8NoBom);

            cancellationToken.ThrowIfCancellationRequested();

            logger("Voice: Đang thổi hồn vào lời thoại (ElevenLabs)...");
            var audioPath = Path.Combine(workDir, "voiceover.mp3");
            await GenerateElevenLabsVoiceAsync(script, settings, audioPath, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            logger("Lip-Sync: Đang phân tích nhịp điệu giọng nói...");
            var baseVideoPath = Path.Combine(workDir, "mascot_base.mp4");
            await RenderMascotSlideshowVideoAsync(
                payload.MascotImagePath,
                audioPath,
                baseVideoPath,
                settings,
                logger,
                cancellationToken).ConfigureAwait(false);

            var finalVideoPath = Path.Combine(workDir, "final_mascot_video.mp4");
            if (payload.UseLipSync && HasMouthAssets(payload))
            {
                logger("Lip-Sync: overlay miệng theo biên độ âm thanh (FFmpeg)...");
                var lipRequest = new LipSyncService.LipSyncRenderRequest
                {
                    BaseImagePath = payload.MascotImagePath,
                    VideoPath = baseVideoPath,
                    AudioPath = audioPath,
                    MouthClosedPath = payload.MouthClosedPath,
                    MouthOpenSmallPath = payload.MouthOpenSmallPath,
                    MouthOpenPath = payload.MouthOpenPath,
                    OverlayX = payload.MouthOverlayX,
                    OverlayY = payload.MouthOverlayY,
                    OverlayScale = payload.MouthOverlayScale <= 0 ? 1d : payload.MouthOverlayScale,
                    OutputPath = finalVideoPath
                };

                try
                {
                    await _lipSyncService.ApplyToVideoAsync(lipRequest, settings, logger, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger("Lip-Sync bỏ qua (lỗi): " + ex.Message);
                    File.Copy(baseVideoPath, finalVideoPath, overwrite: true);
                }
            }
            else
            {
                if (payload.UseLipSync)
                {
                    logger("Lip-Sync: thiếu asset miệng — xuất video không overlay.");
                }

                File.Copy(baseVideoPath, finalVideoPath, overwrite: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var musicPath = ResolveBackgroundMusicPath(settings);
            if (!string.IsNullOrWhiteSpace(musicPath))
            {
                logger("Editor: Đang hòa âm phối khí (nhạc nền + ducking khi lời thoại)...");
                var mixedPath = Path.Combine(workDir, "final_mascot_mixed.mp4");
                await MixVoiceoverWithBackgroundMusicAsync(
                    finalVideoPath,
                    audioPath,
                    musicPath,
                    mixedPath,
                    settings,
                    logger,
                    cancellationToken).ConfigureAwait(false);
                finalVideoPath = mixedPath;
            }
            else
            {
                logger("Editor: Không có nhạc nền — giữ voiceover gốc.");
            }

            logger("✓ Hoàn tất! Video lưu tại: " + finalVideoPath);
            return finalVideoPath;
        }

        private async Task GenerateElevenLabsVoiceAsync(
            string text,
            AppSettings settings,
            string outputPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.TtsApiKey))
            {
                throw new InvalidOperationException("TTS API Key (ElevenLabs) chưa cấu hình trong Cài đặt.");
            }

            if (string.IsNullOrWhiteSpace(settings.TtsEndpoint))
            {
                throw new InvalidOperationException("TTS Endpoint chưa cấu hình trong Cài đặt.");
            }

            if (!ElevenLabsTtsHelper.EndpointIncludesVoiceId(settings.TtsEndpoint))
            {
                throw new InvalidOperationException(
                    "ElevenLabs: TTS Endpoint phải chứa Voice ID của voice bạn đã tạo (…/text-to-speech/{voice_id}).");
            }

            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                throw new InvalidOperationException("Kịch bản lời thoại trống.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

            var tempMp3 = await _videoService.GenerateAudioAsync(
                line,
                settings,
                cancellationToken,
                emphaticHook: false).ConfigureAwait(false);

            File.Copy(tempMp3, outputPath, overwrite: true);
        }

        private static async Task RenderMascotSlideshowVideoAsync(
            string mascotImagePath,
            string audioPath,
            string outputPath,
            AppSettings settings,
            Action<string> logger,
            CancellationToken cancellationToken)
        {
            var ffmpeg = ResolveFfmpegExecutable(settings);
            var duration = Math.Min(TargetDurationSeconds, await ProbeAudioDurationSecondsAsync(audioPath, settings, cancellationToken)
                .ConfigureAwait(false));
            if (duration < 1d)
            {
                duration = TargetDurationSeconds;
            }

            var args =
                "-y -loop 1 -i \"" + mascotImagePath + "\" -i \"" + audioPath + "\" " +
                "-filter_complex \"[0:v]scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2,setsar=1[v]\" " +
                "-map \"[v]\" -map 1:a -c:v libx264 -preset medium -crf 20 -c:a aac -b:a 192k " +
                "-t " + duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " " +
                "-pix_fmt yuv420p -shortest \"" + outputPath + "\"";

            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, logger, cancellationToken).ConfigureAwait(false);
        }

        private static async Task MixVoiceoverWithBackgroundMusicAsync(
            string videoPath,
            string voiceoverPath,
            string musicPath,
            string outputPath,
            AppSettings settings,
            Action<string> logger,
            CancellationToken cancellationToken)
        {
            var ffmpeg = ResolveFfmpegExecutable(settings);
            var musicVolume = Math.Max(0.05d, Math.Min(0.35d, (settings.VideoMusicVolume <= 0 ? 14 : settings.VideoMusicVolume) / 100d));

            var args =
                "-y -i \"" + videoPath + "\" -i \"" + voiceoverPath + "\" -stream_loop -1 -i \"" + musicPath + "\" " +
                "-filter_complex \"[1:a]volume=1.0[vo];[2:a]volume=" + musicVolume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "[bg];" +
                "[vo][bg]amix=inputs=2:duration=first:dropout_transition=2[aout]\" " +
                "-map 0:v:0 -map \"[aout]\" -c:v copy -c:a aac -b:a 192k -shortest \"" + outputPath + "\"";

            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, logger, cancellationToken).ConfigureAwait(false);
        }

        private static bool HasMouthAssets(MascotStoryJobPayload payload)
        {
            return payload != null
                && File.Exists(payload.MouthClosedPath ?? string.Empty)
                && File.Exists(payload.MouthOpenPath ?? string.Empty);
        }

        private static string ResolveBackgroundMusicPath(AppSettings settings)
        {
            var fileName = (settings?.VideoBackgroundMusicFileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(fileName))
            {
                return string.Empty;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidate = Path.Combine(baseDir, "Music", fileName);
            return File.Exists(candidate) ? candidate : string.Empty;
        }

        private static string ResolveFfmpegExecutable(AppSettings settings)
        {
            var configured = (settings?.FfmpegPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                return configured;
            }

            var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe");
            if (File.Exists(bundled))
            {
                return bundled;
            }

            return "ffmpeg";
        }

        private static async Task<double> ProbeAudioDurationSecondsAsync(
            string audioPath,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var ffmpeg = ResolveFfmpegExecutable(settings);
            var ffprobe = ffmpeg.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                ? ffmpeg.Substring(0, ffmpeg.Length - "ffmpeg.exe".Length) + "ffprobe.exe"
                : "ffprobe";

            if (!File.Exists(ffprobe))
            {
                return TargetDurationSeconds;
            }

            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + audioPath + "\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return TargetDurationSeconds;
                }

                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken).ConfigureAwait(false);
                var text = (await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
                if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sec)
                    && sec > 0.5d)
                {
                    return sec;
                }
            }

            return TargetDurationSeconds;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _httpClient.Dispose();
        }
    }
}