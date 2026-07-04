using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// TTS ElevenLabs — payload qua <see cref="ElevenLabsTtsHelper.BuildPayload"/>:
    /// text (UTF-8 NFC), model_id eleven_v3, language_code vi, voice_settings Web defaults.
    /// </summary>
    public sealed class ElevenLabsTtsService
    {
        private readonly VideoService _videoService;

        public ElevenLabsTtsService(VideoService videoService = null)
        {
            _videoService = videoService ?? new VideoService();
        }

        /// <summary>Chọn ElevenLabs voice_id theo mood (tab Triết lý).</summary>
        public static string GetVoiceIdForMood(string mood, AppSettings settings)
        {
            var m = (mood ?? string.Empty).Trim().ToLowerInvariant();
            string moodVoice;
            if (m.Contains("melancholic") || m.Contains("sad"))
            {
                moodVoice = settings?.VoiceId_Melancholic;
            }
            else if (m.Contains("intense") || m.Contains("hopeful"))
            {
                moodVoice = settings?.VoiceId_Intense;
            }
            else
            {
                moodVoice = settings?.VoiceId_Calm;
            }

            if (!string.IsNullOrWhiteSpace(moodVoice))
            {
                return moodVoice.Trim();
            }

            return ElevenLabsTtsHelper.ExtractVoiceIdFromEndpoint(settings?.TtsEndpoint);
        }

        /// <summary>Danh sách voice_id thử lần lượt khi mood voice trả 404.</summary>
        public static IReadOnlyList<string> BuildVoiceIdFallbackChain(
            string mood,
            AppSettings settings,
            string profileFallbackVoiceId = null)
        {
            var ordered = new List<string>();
            void Add(string id)
            {
                id = (id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }

                if (ordered.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                ordered.Add(id);
            }

            Add(GetVoiceIdForMood(mood, settings));
            Add(ElevenLabsTtsHelper.ExtractVoiceIdFromEndpoint(settings?.TtsEndpoint));
            Add(settings?.VoiceId_Calm);
            Add(settings?.VoiceId_Melancholic);
            Add(settings?.VoiceId_Intense);
            Add(profileFallbackVoiceId);

            return ordered;
        }

        public static bool IsNotFoundError(Exception ex)
        {
            var msg = ex?.Message ?? string.Empty;
            return msg.IndexOf("NotFound", StringComparison.OrdinalIgnoreCase) >= 0
                   || msg.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Gọi ElevenLabs, ghi audio vào <paramref name="audioOutputPath"/> và trả transcript
        /// (hiện tại là bản chuẩn hóa của <paramref name="hookText"/> — API TTS không trả STT).
        /// </summary>
        public async Task<string> GenerateAndGetTranscriptAsync(
            string hookText,
            string audioOutputPath,
            CancellationToken cancellationToken)
        {
            return await GenerateAndGetTranscriptAsync(hookText, audioOutputPath, mood: null, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Gọi ElevenLabs với voice_id chọn theo <paramref name="mood"/> (nếu có).</summary>
        public async Task<string> GenerateAndGetTranscriptAsync(
            string hookText,
            string audioOutputPath,
            string mood,
            CancellationToken cancellationToken)
        {
            var transcript = (hookText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(transcript))
            {
                throw new InvalidOperationException("Hook text trống — không thể tạo voiceover ElevenLabs.");
            }

            if (string.IsNullOrWhiteSpace(audioOutputPath))
            {
                throw new ArgumentException("Cần đường dẫn output audio.", nameof(audioOutputPath));
            }

            var settings = await new ConfigManager().LoadAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(settings.TtsApiKey) || string.IsNullOrWhiteSpace(settings.TtsEndpoint))
            {
                throw new InvalidOperationException("Cần TTS API Key và Endpoint (ElevenLabs) trong Cài đặt.");
            }

            transcript = ElevenLabsTtsHelper.ApplyDeepPauses(transcript);
            transcript = await VietnameseTtsTextNormalizer.PrepareForElevenLabsAsync(
                transcript,
                settings,
                _ => { },
                cancellationToken).ConfigureAwait(false);

            var mp3Path = await GenerateAudioWithFallbackAsync(
                transcript,
                settings,
                mood,
                profileFallbackVoiceId: null,
                emphaticHook: true,
                log: null,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(mp3Path) || !File.Exists(mp3Path))
            {
                throw new InvalidOperationException("ElevenLabs không trả file audio hợp lệ.");
            }

            var target = Path.GetFullPath(audioOutputPath);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(target))
            {
                File.Delete(target);
            }

            if (!string.Equals(Path.GetFullPath(mp3Path), target, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(mp3Path, target);
            }
            else if (mp3Path != target)
            {
                File.Move(mp3Path, target);
            }

            return transcript;
        }

        /// <summary>Gọi ElevenLabs, thử voice_id khác nếu mood voice trả 404 Not Found.</summary>
        public async Task<string> GenerateAudioWithFallbackAsync(
            string text,
            AppSettings settings,
            string mood,
            string profileFallbackVoiceId,
            bool emphaticHook,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var chain = BuildVoiceIdFallbackChain(mood, settings, profileFallbackVoiceId);
            if (chain.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không có voice_id ElevenLabs — cấu hình Voice ID mood hoặc TTS Endpoint trong Cài đặt.");
            }

            Exception lastError = null;
            for (var i = 0; i < chain.Count; i++)
            {
                var voiceId = chain[i];
                try
                {
                    if (i > 0)
                    {
                        log?.Invoke("[ElevenLabs] voice_id «" + chain[i - 1] + "» không tồn tại — thử «" + voiceId + "».");
                    }

                    return await _videoService.GenerateAudioAsync(
                        text,
                        settings,
                        cancellationToken,
                        emphaticHook,
                        voiceIdOverride: voiceId).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsNotFoundError(ex) && i < chain.Count - 1)
                {
                    lastError = ex;
                    log?.Invoke("[ElevenLabs] 404 voice_id «" + voiceId + "»: " + ex.Message);
                }
            }

            throw lastError ?? new InvalidOperationException("ElevenLabs TTS thất bại — không còn voice_id dự phòng.");
        }
    }
}
