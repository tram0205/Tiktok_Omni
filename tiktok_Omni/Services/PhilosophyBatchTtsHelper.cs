using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>TTS quote Triết lý — cùng engine/giọng/tốc độ cho preview và render.</summary>
    public static class PhilosophyBatchTtsHelper
    {
        public static ShowcaseTtsRenderOptions BuildTtsRenderOptions(
            PhilosophyBatchItem batch,
            string profileName,
            AppSettings settings)
        {
            if (batch == null)
            {
                return null;
            }

            var video = PhilosophyBatchShowcaseAudioAdapter.ToShowcaseVideo(batch, profileName);
            SyncBodyVoiceToHookTrack(video);
            return ShowcaseTtsRenderOptions.FromVideo(video, settings);
        }

        /// <summary>Triết lý chỉ có một track thoại — map body voice sang hook để Showcase TTS dùng đúng preset.</summary>
        public static void SyncBodyVoiceToHookTrack(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseHookTtsEngine = video.ShowcaseBodyTtsEngine ?? string.Empty;
            video.ShowcaseHookElevenPersona = video.ShowcaseBodyElevenPersona ?? string.Empty;
            video.ShowcaseVoiceToneId = video.ShowcaseBodyVoiceToneId ?? string.Empty;
            video.ShowcaseElevenCustomStabilityPercent = video.ShowcaseBodyElevenCustomStabilityPercent;
            video.ShowcaseElevenCustomSimilarityPercent = video.ShowcaseBodyElevenCustomSimilarityPercent;
            video.ShowcaseElevenCustomStylePercent = video.ShowcaseBodyElevenCustomStylePercent;
            video.ShowcaseHookStyleKey = video.ShowcaseBodyStyleKey ?? string.Empty;
            video.ShowcaseEdgeRateOffsetPercent = video.ShowcaseBodyEdgeRateOffsetPercent;
            video.ShowcaseEdgePitchOffsetHz = video.ShowcaseBodyEdgePitchOffsetHz;
            video.ShowcaseVoicePresetId = video.ShowcaseBodyVoicePresetId ?? string.Empty;
            video.ShowcaseVoiceGenderId = video.ShowcaseBodyVoiceGenderId ?? string.Empty;
            video.ShowcaseVoiceLanguageId = video.ShowcaseBodyVoiceLanguageId ?? string.Empty;
        }

        public static async Task GenerateQuoteVoiceMp3Async(
            string quoteText,
            ShowcaseTtsRenderOptions ttsOptions,
            AppSettings settings,
            string outputMp3,
            string workDirectory,
            int narrationSpeedPercent,
            string ffmpeg,
            string ffprobe,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var quote = (quoteText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(quote))
            {
                throw new InvalidOperationException("Script trống — không gọi TTS.");
            }

            ttsOptions = ttsOptions ?? new ShowcaseTtsRenderOptions();
            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(outputMp3) ?? ".");
            var scenes = new List<AiVideoGenInputItem>
            {
                new AiVideoGenInputItem
                {
                    SceneVoiceover = quote,
                    ShowcaseSceneSilent = false
                }
            };

            var narration = new AffiliateNarrationService();
            log?.Invoke("[Quote] TTS theo cấu hình popup Âm thanh ("
                         + DescribeEngine(ttsOptions.BodyEngine) + ")…");
            await narration.GenerateShowcaseHookPreviewAsync(
                quote,
                scenes,
                settings,
                outputMp3,
                log,
                cancellationToken,
                ttsOptions).ConfigureAwait(false);

            if (!File.Exists(outputMp3))
            {
                throw new InvalidOperationException("TTS không tạo được file audio.");
            }

            var speedPct = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(narrationSpeedPercent);
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg) || speedPct == 100)
            {
                return;
            }

            var speedWorkDir = Path.Combine(workDirectory ?? ".", "speed_work");
            var adjusted = await ShowcaseNarrationAvSyncHelper.PrepareSpeedAdjustedMp3Async(
                ffmpeg,
                ffprobe,
                outputMp3,
                speedWorkDir,
                speedPct,
                "voice_speed.mp3",
                log,
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(adjusted, outputMp3, StringComparison.OrdinalIgnoreCase) && File.Exists(adjusted))
            {
                File.Copy(adjusted, outputMp3, overwrite: true);
                log?.Invoke("[Quote] Tốc độ thoại: "
                             + speedPct.ToString(CultureInfo.InvariantCulture) + "%");
            }
        }

        private static string DescribeEngine(TtsEngineKind engine) =>
            engine == TtsEngineKind.ElevenLabs ? "ElevenLabs" : "Edge TTS";
    }
}
