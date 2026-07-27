using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>ElevenLabs cho Video reup: giọng hook vs thuyết minh, audio tags v3, fallback voice_id.</summary>
    public static class ReupElevenLabsVoiceHelper
    {
        public static IReadOnlyList<string> BuildVoiceIdFallbackChain(bool emphaticHook, AppSettings settings)
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

            if (emphaticHook)
            {
                Add(settings?.VoiceId_ReupHook);
                Add(settings?.VoiceId_Intense);
            }
            else
            {
                Add(settings?.VoiceId_ReupNarration);
                Add(settings?.VoiceId_Calm);
            }

            Add(ElevenLabsTtsHelper.ExtractVoiceIdFromEndpoint(settings?.TtsEndpoint));
            Add(settings?.VoiceId_Calm);
            Add(settings?.VoiceId_Intense);
            Add(settings?.VoiceId_Melancholic);

            return ordered;
        }

        public static string PrepareTextForSynthesis(
            string text,
            AppSettings settings,
            bool emphaticHook,
            string hookStyleKey,
            Action<string> log)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            if (!VideoReupRemixService.IsElevenLabsConfigured(settings))
            {
                return line;
            }

            var model = ElevenLabsTtsHelper.ResolveModelId(settings);
            if (string.Equals(model, ElevenLabsTtsHelper.DefaultVietnameseModel, StringComparison.OrdinalIgnoreCase))
            {
                if (emphaticHook)
                {
                    line = ApplyHookStyleAudioTag(line, hookStyleKey);
                    line = ElevenLabsTtsHelper.ApplyHookDeliveryPauses(line);
                    log?.Invoke("[VideoReup] Eleven v3: hook + audio tag phong cách «"
                                + HookStyleCatalog.GetDisplayName(hookStyleKey) + "».");
                }
                else
                {
                    if (!line.StartsWith("[", StringComparison.Ordinal))
                    {
                        line = "[calm] " + line;
                    }

                    line = ElevenLabsTtsHelper.ApplyDeepPauses(line);
                    log?.Invoke("[VideoReup] Eleven v3: thuyết minh [calm] + ngắt câu sâu.");
                }
            }
            else if (emphaticHook)
            {
                line = ElevenLabsTtsHelper.ApplyHookDeliveryPauses(line);
            }
            else
            {
                line = ElevenLabsTtsHelper.ApplyDeepPauses(line);
            }

            return line;
        }

        public static string ApplyHookStyleAudioTag(string text, string hookStyleKey)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("[", StringComparison.Ordinal))
            {
                return line;
            }

            var tag = ResolveStyleAudioTag(hookStyleKey);
            return string.IsNullOrEmpty(tag) ? line : tag + " " + line;
        }

        public static string ResolveStyleAudioTag(string hookStyleKey)
        {
            switch ((hookStyleKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case HookStyleCatalog.StyleNoidau:
                    return "[concerned]";
                case HookStyleCatalog.StyleBocphot:
                    return "[mischievously]";
                case HookStyleCatalog.StyleHuongdan:
                    return "[curious]";
                case HookStyleCatalog.StyleFomo:
                    return "[excited]";
                case HookStyleCatalog.StyleKechuyen:
                    return "[thoughtful]";
                default:
                    return "[excited]";
            }
        }

        public static string DescribeVoiceSettings(bool emphaticHook)
        {
            if (emphaticHook)
            {
                return "stability=0.38, style=0.32 (hook nhấn)";
            }

            return "stability=0.58, style=0.08 (kể chuyện)";
        }

        public static async Task<string> GenerateAudioWithFallbackAsync(
            VideoService videoService,
            string text,
            AppSettings settings,
            bool emphaticHook,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (videoService == null)
            {
                throw new ArgumentNullException(nameof(videoService));
            }

            var chain = BuildVoiceIdFallbackChain(emphaticHook, settings);
            if (chain.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không có voice_id ElevenLabs — cấu hình Voice ID Reup hook/thuyết minh hoặc TTS Endpoint.");
            }

            Exception lastError = null;
            for (var i = 0; i < chain.Count; i++)
            {
                var voiceId = chain[i];
                try
                {
                    if (i == 0)
                    {
                        log?.Invoke("[VideoReup] ElevenLabs voice_id «" + voiceId + "», "
                                    + DescribeVoiceSettings(emphaticHook) + ".");
                    }
                    else
                    {
                        log?.Invoke("[VideoReup] ElevenLabs: thử voice_id dự phòng «" + voiceId + "».");
                    }

                    return await videoService.GenerateAudioAsync(
                        text,
                        settings,
                        cancellationToken,
                        emphaticHook,
                        voiceId).ConfigureAwait(false);
                }
                catch (Exception ex) when (ElevenLabsTtsService.IsNotFoundError(ex) && i < chain.Count - 1)
                {
                    lastError = ex;
                    log?.Invoke("[VideoReup] ElevenLabs 404 voice «" + voiceId + "».");
                }
            }

            throw lastError ?? new InvalidOperationException("ElevenLabs TTS thất bại — không còn voice_id dự phòng.");
        }
    }
}
