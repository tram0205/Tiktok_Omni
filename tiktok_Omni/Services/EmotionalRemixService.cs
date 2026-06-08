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
    public sealed class EmotionalRemixService : IDisposable
    {
        private const string DefaultElevenLabsVoiceId = "pNInz6obpg8nEmeWscDJ";
        private const double TargetDurationSeconds = 45d;

        private readonly HttpClient _httpClient;
        private readonly LipSyncService _lipSyncService = new LipSyncService();
        private bool _disposed;

        public EmotionalRemixService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }


        public Task<string> RenderMascotStoryAsync(MascotStoryJobPayload payload, CancellationToken cancellationToken)
        {
            return RenderMascotStoryAsync(payload, null, null, cancellationToken);
        }
        public async Task<string> RenderMascotStoryAsync(
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
                "Mascot", "EmotionalRemix",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(workDir);

            cancellationToken.ThrowIfCancellationRequested();

            logger("Director: Đang soạn kịch bản đánh vào tâm lý người xem...");
            var gemini = new GeminiService();
            var style = (payload.MascotStyle ?? string.Empty).Trim();
            var prompt =
                "Viet kich ban mascot TikTok ~45s. Cau truc: Hook 3s, Pain 12s, Solution 25s, CTA 5s. Chu de: " +
                (payload.ChannelTheme ?? string.Empty).Trim() +
                (string.IsNullOrEmpty(style) ? string.Empty : ". Giong: " + style) +
                ". Chi tra loi loi thoai.";

            var script = await gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(workDir, "script.txt"), script ?? string.Empty, TextFileEncoding.Utf8NoBom);

            cancellationToken.ThrowIfCancellationRequested();

            logger("Voice: Đang thổi hồn vào lời thoại (ElevenLabs)...");
            var audioPath = Path.Combine(workDir, "voiceover.wav");
            await GenerateElevenLabsVoiceAsync(script, settings.TtsApiKey, audioPath, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var masterVoicePath = audioPath;
            if (payload.UseVisualHookSfx && !string.IsNullOrWhiteSpace(payload.VisualHookSfxPath) && File.Exists(payload.VisualHookSfxPath))
            {
                logger("[EmotionalRemix] Visual Hook SFX 3s...");
                var hookWav = Path.Combine(workDir, "hook_sfx_3s.wav");
                await VisualHookService.PrepareThreeSecondHookWavAsync(payload.VisualHookSfxPath, hookWav, ResolveFfmpegExecutable(settings), logger, cancellationToken).ConfigureAwait(false);
                masterVoicePath = Path.Combine(workDir, "voice_master.wav");
                await MergeHookWithVoiceoverAsync(hookWav, audioPath, masterVoicePath, settings, logger, cancellationToken).ConfigureAwait(false);
            }

            logger("Lip-Sync: Đang phân tích nhịp điệu giọng nói...");
            var baseVideoPath = Path.Combine(workDir, "mascot_base.mp4");
            await RenderMascotSlideshowVideoAsync(
                payload.MascotImagePath,
                masterVoicePath,
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
                    AudioPath = masterVoicePath,
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
                    masterVoicePath,
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
            string apiKey,
            string outputPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("TTS API Key (ElevenLabs) chưa cấu hình trong Cài đặt.");
            }

            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                throw new InvalidOperationException("Kịch bản lời thoại trống.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

            var voiceId = DefaultElevenLabsVoiceId;
            var url = "https://api.elevenlabs.io/v1/text-to-speech/" + voiceId;
            var body = JsonConvert.SerializeObject(new
            {
                text = line,
                model_id = "eleven_multilingual_v2",
                voice_settings = new { stability = 0.5, similarity_boost = 0.8 }
            });

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.TryAddWithoutValidation("xi-api-key", apiKey.Trim());
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Lỗi ElevenLabs: " + responseText);
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var tempMp3 = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".", "voiceover_elevenlabs.mp3");
                    File.WriteAllBytes(tempMp3, bytes);
                    var ffmWav = ResolveFfmpegExecutable(await new ConfigManager().LoadAsync().ConfigureAwait(false));
                    var wavArgs = "-y -i \"" + tempMp3 + "\" -ar 48000 -ac 2 \"" + outputPath + "\"";
                    await VideoReupRemixService.RunFfmpegPublicAsync(ffmWav, wavArgs, null, cancellationToken).ConfigureAwait(false);
                }
            }
        }


        private static async Task MergeHookWithVoiceoverAsync(
            string hookWav, string voiceWav, string outputWav, AppSettings settings, Action<string> logger, CancellationToken cancellationToken)
        {
            var ffmpeg = ResolveFfmpegExecutable(settings);
            var hookStr = VisualHookService.HookDurationSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var filter = "[0:a]atrim=0:" + hookStr + ",asetpts=PTS-STARTPTS,volume=1.0[hook];[1:a]asetpts=PTS-STARTPTS,volume=1.0[vo];[hook][vo]amix=inputs=2:duration=longest:dropout_transition=0[aout]";
            var args = "-y -i \"" + hookWav + "\" -i \"" + voiceWav + "\" -filter_complex \"" + filter + "\" -map \"[aout]\" \"" + outputWav + "\"";
            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, logger, cancellationToken).ConfigureAwait(false);
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
            var duckStr = "0.2";
            logger?.Invoke("[EmotionalRemix] Ducking nhac nen 20% + sidechain khi co giong.");
            var args =
                "-y -i \"" + videoPath + "\" -i \"" + voiceoverPath + "\" -stream_loop -1 -i \"" + musicPath + "\" " +
                "-filter_complex \"[1:a]asetpts=PTS-STARTPTS,volume=1.0[vo];[2:a]asetpts=PTS-STARTPTS,volume=" + duckStr + "[bg];" +
                "[vo][bg]sidechaincompress=threshold=0.02:ratio=8:attack=200:release=900:makeup=1[ducked];" +
                "[vo][ducked]amix=inputs=2:duration=first:dropout_transition=2[aout]\" " +
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