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

            SyncHookBodyEnginesFromLegacy(video);

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

            if (string.IsNullOrWhiteSpace(video.ShowcaseVoiceGenderId))
            {
                video.ShowcaseVoiceGenderId = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseVoicePresetId).GenderId;
            }
            else
            {
                video.ShowcaseVoiceGenderId = ShowcaseVoicePresetDimensions.NormalizeGenderId(video.ShowcaseVoiceGenderId);
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseVoiceLanguageId))
            {
                video.ShowcaseVoiceLanguageId = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseVoicePresetId).LanguageId;
            }
            else
            {
                video.ShowcaseVoiceLanguageId = ShowcaseVoicePresetDimensions.NormalizeLanguageId(video.ShowcaseVoiceLanguageId);
            }

            video.ShowcaseVoiceToneId = ShowcaseElevenToneHelper.NormalizeToneId(video.ShowcaseVoiceToneId);
            EnsureCustomToneDefaults(
                () => video.ShowcaseElevenCustomStabilityPercent,
                v => video.ShowcaseElevenCustomStabilityPercent = v,
                () => video.ShowcaseElevenCustomSimilarityPercent,
                v => video.ShowcaseElevenCustomSimilarityPercent = v,
                () => video.ShowcaseElevenCustomStylePercent,
                v => video.ShowcaseElevenCustomStylePercent = v);

            if (string.IsNullOrWhiteSpace(video.ShowcaseHookStyleKey))
            {
                video.ShowcaseHookStyleKey = HookStyleCatalog.StyleHuongdan;
            }
            else
            {
                video.ShowcaseHookStyleKey = ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(video.ShowcaseHookStyleKey);
            }

            SyncBodyVoiceFromHookWhenEmpty(video);

            ShowcaseEdgeProsodyHelper.EnsureVideoDefaults(video);
            ShowcaseNarrationSpeedHelper.EnsureSegmentSpeedDefaults(video);
            ShowcaseNarrationSpeedHelper.EnsureSegmentSpeedDefaults(video);
            ApplyElevenSouthernLanguageDefaults(video);
        }

        /// <summary>ElevenLabs tiếng Việt luôn vi_south — không còn chọn vùng trên UI.</summary>
        private static void ApplyElevenSouthernLanguageDefaults(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            if (string.Equals(NormalizeEngineStorageId(video.ShowcaseHookTtsEngine), EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                video.ShowcaseVoiceLanguageId = ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(
                    video.ShowcaseVoiceLanguageId);
            }

            if (string.Equals(NormalizeEngineStorageId(video.ShowcaseBodyTtsEngine), EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                video.ShowcaseBodyVoiceLanguageId = ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(
                    video.ShowcaseBodyVoiceLanguageId);
            }
        }

        public static void SyncBodyVoiceFromHookWhenEmpty(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyVoicePresetId))
            {
                video.ShowcaseBodyVoicePresetId = video.ShowcaseVoicePresetId;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyVoiceAgeId))
            {
                video.ShowcaseBodyVoiceAgeId = video.ShowcaseVoiceAgeId;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyVoiceGenderId))
            {
                video.ShowcaseBodyVoiceGenderId = video.ShowcaseVoiceGenderId;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyVoiceLanguageId))
            {
                video.ShowcaseBodyVoiceLanguageId = video.ShowcaseVoiceLanguageId;
            }

            video.ShowcaseBodyVoiceToneId = ShowcaseElevenToneHelper.NormalizeToneId(video.ShowcaseBodyVoiceToneId);
            EnsureCustomToneDefaults(
                () => video.ShowcaseBodyElevenCustomStabilityPercent,
                v => video.ShowcaseBodyElevenCustomStabilityPercent = v,
                () => video.ShowcaseBodyElevenCustomSimilarityPercent,
                v => video.ShowcaseBodyElevenCustomSimilarityPercent = v,
                () => video.ShowcaseBodyElevenCustomStylePercent,
                v => video.ShowcaseBodyElevenCustomStylePercent = v);

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyStyleKey))
            {
                video.ShowcaseBodyStyleKey = HookStyleCatalog.StyleKechuyen;
            }
            else
            {
                video.ShowcaseBodyStyleKey =
                    ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(video.ShowcaseBodyStyleKey);
            }

            video.ShowcaseBodyEdgeRateOffsetPercent =
                ShowcaseEdgeProsodyHelper.ClampRateOffset(video.ShowcaseBodyEdgeRateOffsetPercent);
            video.ShowcaseBodyEdgePitchOffsetHz =
                ShowcaseEdgeProsodyHelper.ClampPitchOffset(video.ShowcaseBodyEdgePitchOffsetHz);
        }

        /// <summary>Video cũ chưa từng chỉnh Tùy chỉnh → cả 3 số = 0 (mặc định int) → nạp 50/75/15 hợp lý thay vì 0.00/0.00/0.00.</summary>
        private static void EnsureCustomToneDefaults(
            Func<int> getStability,
            Action<int> setStability,
            Func<int> getSimilarity,
            Action<int> setSimilarity,
            Func<int> getStyle,
            Action<int> setStyle)
        {
            if (getStability() <= 0 && getSimilarity() <= 0 && getStyle() <= 0)
            {
                setStability(50);
                setSimilarity(75);
                setStyle(15);
                return;
            }

            setStability(ShowcaseElevenToneHelper.ClampPercent(getStability()));
            setSimilarity(ShowcaseElevenToneHelper.ClampPercent(getSimilarity()));
            setStyle(ShowcaseElevenToneHelper.ClampPercent(getStyle()));
        }

        public static void SyncHookBodyEnginesFromLegacy(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            var legacy = (video.ShowcaseTtsEngine ?? string.Empty).Trim();
            var hookRaw = (video.ShowcaseHookTtsEngine ?? string.Empty).Trim();
            var bodyRaw = (video.ShowcaseBodyTtsEngine ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(hookRaw) && string.IsNullOrEmpty(bodyRaw)
                && !string.IsNullOrEmpty(legacy)
                && !string.Equals(legacy, EngineAskOnCreate, StringComparison.OrdinalIgnoreCase))
            {
                video.ShowcaseHookTtsEngine = NormalizeEngineStorageId(legacy);
                video.ShowcaseBodyTtsEngine = NormalizeEngineStorageId(legacy);
                return;
            }

            if (string.IsNullOrEmpty(hookRaw))
            {
                video.ShowcaseHookTtsEngine = string.IsNullOrEmpty(bodyRaw)
                    ? EngineEdgeTts
                    : NormalizeEngineStorageId(bodyRaw);
            }
            else
            {
                video.ShowcaseHookTtsEngine = NormalizeEngineStorageId(hookRaw);
            }

            if (string.IsNullOrEmpty(bodyRaw))
            {
                video.ShowcaseBodyTtsEngine = NormalizeEngineStorageId(video.ShowcaseHookTtsEngine);
            }
            else
            {
                video.ShowcaseBodyTtsEngine = NormalizeEngineStorageId(bodyRaw);
            }
        }

        public static string NormalizeEngineStorageId(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            if (string.Equals(raw, EnginePiperOfflineLegacy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "PiperOffline", StringComparison.OrdinalIgnoreCase))
            {
                return EngineEdgeTts;
            }

            if (string.Equals(raw, EngineElevenLabs, StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, TtsEngineKind.ElevenLabs.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return EngineElevenLabs;
            }

            if (string.Equals(raw, EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, TtsEngineKind.EdgeTts.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return EngineEdgeTts;
            }

            return EngineEdgeTts;
        }

        public static ShowcaseTtsRenderOptions ResolveFromVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            EnsureVideoDefaults(video, settings);
            var dims = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseVoicePresetId);
            dims.AgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(video.ShowcaseVoiceAgeId);
            dims.GenderId = ShowcaseVoicePresetDimensions.NormalizeGenderId(video.ShowcaseVoiceGenderId);
            dims.LanguageId = ParseEngine(video.ShowcaseHookTtsEngine, settings) == TtsEngineKind.ElevenLabs
                ? ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(video.ShowcaseVoiceLanguageId)
                : ShowcaseVoicePresetDimensions.NormalizeLanguageId(video.ShowcaseVoiceLanguageId);
            var resolvedPresetId = ShowcaseVoicePresetDimensions.ResolvePresetId(dims);

            var bodyDims = ShowcaseVoicePresetDimensions.GetForPreset(video.ShowcaseBodyVoicePresetId);
            bodyDims.AgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(video.ShowcaseBodyVoiceAgeId);
            bodyDims.GenderId = ShowcaseVoicePresetDimensions.NormalizeGenderId(video.ShowcaseBodyVoiceGenderId);
            bodyDims.LanguageId = ParseEngine(video.ShowcaseBodyTtsEngine, settings) == TtsEngineKind.ElevenLabs
                ? ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(video.ShowcaseBodyVoiceLanguageId)
                : ShowcaseVoicePresetDimensions.NormalizeLanguageId(video.ShowcaseBodyVoiceLanguageId);
            var resolvedBodyPresetId = ShowcaseVoicePresetDimensions.ResolvePresetId(bodyDims);

            return new ShowcaseTtsRenderOptions
            {
                HookEngine = ParseEngine(video.ShowcaseHookTtsEngine, settings),
                BodyEngine = ParseEngine(video.ShowcaseBodyTtsEngine, settings),
                VoicePresetId = resolvedPresetId,
                VoiceAgeId = dims.AgeId,
                VoiceLanguageId = dims.LanguageId,
                HookElevenPersona = ElevenVoicePersonaCatalog.Normalize(video.ShowcaseHookElevenPersona),
                HookVoiceToneId = ShowcaseElevenToneHelper.NormalizeToneId(video.ShowcaseVoiceToneId),
                HookElevenCustomStabilityPercent = ShowcaseElevenToneHelper.ClampPercent(video.ShowcaseElevenCustomStabilityPercent),
                HookElevenCustomSimilarityPercent = ShowcaseElevenToneHelper.ClampPercent(video.ShowcaseElevenCustomSimilarityPercent),
                HookElevenCustomStylePercent = ShowcaseElevenToneHelper.ClampPercent(video.ShowcaseElevenCustomStylePercent),
                HookStyleKey = ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(video.ShowcaseHookStyleKey),
                EdgeRateOffsetPercent = video.ShowcaseEdgeRateOffsetPercent,
                EdgePitchOffsetHz = video.ShowcaseEdgePitchOffsetHz,
                BodyVoicePresetId = resolvedBodyPresetId,
                BodyVoiceAgeId = bodyDims.AgeId,
                BodyVoiceLanguageId = bodyDims.LanguageId,
                BodyElevenPersona = ElevenVoicePersonaCatalog.Normalize(video.ShowcaseBodyElevenPersona),
                BodyVoiceToneId = ShowcaseElevenToneHelper.NormalizeToneId(video.ShowcaseBodyVoiceToneId),
                BodyElevenCustomStabilityPercent = ShowcaseElevenToneHelper.ClampPercent(video.ShowcaseBodyElevenCustomStabilityPercent),
                BodyElevenCustomSimilarityPercent = ShowcaseElevenToneHelper.ClampPercent(video.ShowcaseBodyElevenCustomSimilarityPercent),
                BodyElevenCustomStylePercent = ShowcaseElevenToneHelper.ClampPercent(video.ShowcaseBodyElevenCustomStylePercent),
                BodyStyleKey = ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(video.ShowcaseBodyStyleKey),
                BodyEdgeRateOffsetPercent = video.ShowcaseBodyEdgeRateOffsetPercent,
                BodyEdgePitchOffsetHz = video.ShowcaseBodyEdgePitchOffsetHz
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

        /// <summary>Ưu tiên persona + vùng miền; rỗng thì suy từ preset (miền Nam) thay vì Calm/Intense.</summary>
        public static string ResolveElevenLabsVoiceId(ShowcaseTtsRenderOptions segmentTts, AppSettings settings)
        {
            segmentTts = segmentTts ?? new ShowcaseTtsRenderOptions();
            var languageId = ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(segmentTts.VoiceLanguageId);
            var personaVoice = ElevenVoicePersonaCatalog.ResolveVoiceId(
                segmentTts.ElevenPersona,
                settings,
                languageId);
            if (!string.IsNullOrWhiteSpace(personaVoice))
            {
                return personaVoice;
            }

            var inferred = ElevenVoiceRegionHelper.ResolveInferredVoiceId(segmentTts, settings);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                return inferred;
            }

            return ResolveElevenLabsVoiceId(segmentTts.Preset, settings);
        }

        /// <summary>Legacy — mood preset (calm/intense) → VoiceId_Intense/Calm. Dùng khi chưa chọn persona.</summary>
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

        public static TtsEngineKind ParseEngine(string raw, AppSettings settings)
        {
            raw = NormalizeEngineStorageId(raw);
            if (string.Equals(raw, EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                return TtsEngineKind.ElevenLabs;
            }

            return TtsEngineKind.EdgeTts;
        }
    }
}
