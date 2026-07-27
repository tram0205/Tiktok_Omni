using System;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    public static class ShowcaseTtsHelper
    {
        public const string EngineAskOnCreate = "AskOnCreate";
        public const string EngineElevenLabs = "ElevenLabs";
        public const string EngineEdgeTts = "EdgeTts";
        /// <summary>Legacy — map sang EdgeTts.</summary>
        public const string EnginePiperOfflineLegacy = "PiperOffline";

        public static void EnsureVideoDefaults(ShowcaseVideoItem video, AppSettings settings)
        {
            if (video == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseTtsEngine))
            {
                video.ShowcaseTtsEngine = EngineEdgeTts;
            }
            else if (string.Equals(video.ShowcaseTtsEngine, EnginePiperOfflineLegacy, StringComparison.OrdinalIgnoreCase))
            {
                video.ShowcaseTtsEngine = EngineEdgeTts;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseVoicePresetId))
            {
                video.ShowcaseVoicePresetId = ShowcaseVoicePresetCatalog.DefaultPresetId;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseVoiceAgeId))
            {
                video.ShowcaseVoiceAgeId = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseVoicePresetId).AgeId;
            }
            else
            {
                video.ShowcaseVoiceAgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(video.ShowcaseVoiceAgeId);
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseVoiceLanguageId))
            {
                video.ShowcaseVoiceLanguageId = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseVoicePresetId).LanguageId;
            }
            else
            {
                video.ShowcaseVoiceLanguageId = ShowcaseVoicePresetDimensions.NormalizeLanguageId(video.ShowcaseVoiceLanguageId);
            }
        }

        public static ShowcaseTtsRenderOptions ResolveFromVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            EnsureVideoDefaults(video, settings);
            var dims = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseVoicePresetId);
            dims.AgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(video.ShowcaseVoiceAgeId);
            dims.LanguageId = ShowcaseVoicePresetDimensions.NormalizeLanguageId(video.ShowcaseVoiceLanguageId);
            var resolvedPresetId = ShowcaseVoicePresetDimensions.ResolvePresetId(dims);
            var engine = ParseEngine(video.ShowcaseTtsEngine, settings);
            return new ShowcaseTtsRenderOptions
            {
                Engine = engine,
                VoicePresetId = resolvedPresetId,
                VoiceAgeId = dims.AgeId,
                VoiceLanguageId = dims.LanguageId
            };
        }

        public static string ResolveElevenLabsLanguageCode(ShowcaseTtsRenderOptions options)
        {
            options = options ?? new ShowcaseTtsRenderOptions();
            return ShowcaseVoicePresetDimensions.ResolveElevenLabsLanguageCode(
                options.VoiceLanguageId,
                options.Preset);
        }

        public static TtsEngineKind ResolveEngineForCreate(ShowcaseVideoItem video, AppSettings settings)
        {
            EnsureVideoDefaults(video, settings);
            var raw = (video.ShowcaseTtsEngine ?? string.Empty).Trim();
            if (string.Equals(raw, EngineAskOnCreate, StringComparison.OrdinalIgnoreCase))
            {
                return TtsEngineChoiceHelper.ParseStoredChoice(settings?.ShowcaseLastTtsEngine);
            }

            return ParseEngine(raw, settings);
        }

        public static bool UsesAskOnCreateEngine(ShowcaseVideoItem video)
        {
            return string.Equals(
                (video?.ShowcaseTtsEngine ?? string.Empty).Trim(),
                EngineAskOnCreate,
                StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveElevenLabsVoiceId(ShowcaseVoicePresetDefinition preset, AppSettings settings)
        {
            preset = preset ?? ShowcaseVoicePresetCatalog.GetById(ShowcaseVoicePresetCatalog.DefaultPresetId);
            var mood = (preset.ElevenLabsVoiceMood ?? string.Empty).Trim().ToLowerInvariant();
            if (mood.Contains("intense"))
            {
                if (!string.IsNullOrWhiteSpace(settings?.VoiceId_Intense))
                {
                    return settings.VoiceId_Intense.Trim();
                }
            }
            else if (mood.Contains("calm") || mood.Contains("melanch"))
            {
                if (!string.IsNullOrWhiteSpace(settings?.VoiceId_Calm))
                {
                    return settings.VoiceId_Calm.Trim();
                }
            }

            return null;
        }

        private static TtsEngineKind ParseEngine(string raw, AppSettings settings)
        {
            raw = (raw ?? string.Empty).Trim();
            if (string.Equals(raw, EnginePiperOfflineLegacy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "PiperOffline", StringComparison.OrdinalIgnoreCase))
            {
                return TtsEngineKind.EdgeTts;
            }

            if (string.Equals(raw, EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, TtsEngineKind.EdgeTts.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return TtsEngineKind.EdgeTts;
            }

            if (string.Equals(raw, EngineElevenLabs, StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, TtsEngineKind.ElevenLabs.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return TtsEngineKind.ElevenLabs;
            }

            return TtsEngineKind.EdgeTts;
        }
    }
}
