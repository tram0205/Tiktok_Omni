using System;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Cài đặt render Showcase theo từng dòng video — truyền qua job payload.</summary>
    public sealed class ShowcasePerVideoRenderSettings
    {
        public string SubtitleFontName { get; set; } = string.Empty;

        public int SubtitleFontSize { get; set; } = 72;

        public string SubtitlePosition { get; set; } = "Bottom";

        public string SubtitleAnimation { get; set; } = "Pop";

        public bool SubtitleBold { get; set; } = true;

        public bool SubtitleItalic { get; set; }

        public int SubtitleWordsPerLine { get; set; } = 6;

        public bool SubtitleEnabled { get; set; }

        public bool HookSubtitleEnabled { get; set; }

        public string HookSubtitleAnimation { get; set; } = "PopStrong";

        public string HookSubtitleFontName { get; set; } = string.Empty;

        public int HookSubtitleFontSize { get; set; }

        public string BackgroundMusicFile { get; set; } = string.Empty;

        public int MusicVolume { get; set; } = 14;

        /// <summary>0 = tự khớp clip; 50–200 = % tốc độ thoại (chỉnh tay).</summary>
        public int NarrationSpeedPercent { get; set; }

        public string TtsEngine { get; set; } = string.Empty;

        public string VoicePresetId { get; set; } = string.Empty;

        public string VoiceAgeId { get; set; } = string.Empty;

        public string VoiceLanguageId { get; set; } = string.Empty;

        public string HookElevenPersona { get; set; } = string.Empty;

        public string VoiceToneId { get; set; } = string.Empty;

        public int ElevenCustomStabilityPercent { get; set; }

        public int ElevenCustomSimilarityPercent { get; set; }

        public int ElevenCustomStylePercent { get; set; }

        public string BodyTtsEngine { get; set; } = string.Empty;

        public string HookTtsEngine { get; set; } = string.Empty;

        public string HookStyleKey { get; set; } = string.Empty;

        public int EdgeRateOffsetPercent { get; set; }

        public int EdgePitchOffsetHz { get; set; }

        public string BodyVoicePresetId { get; set; } = string.Empty;

        public string BodyVoiceAgeId { get; set; } = string.Empty;

        public string BodyVoiceLanguageId { get; set; } = string.Empty;

        public string BodyElevenPersona { get; set; } = string.Empty;

        public string BodyVoiceToneId { get; set; } = string.Empty;

        public int BodyElevenCustomStabilityPercent { get; set; }

        public int BodyElevenCustomSimilarityPercent { get; set; }

        public int BodyElevenCustomStylePercent { get; set; }

        public string BodyStyleKey { get; set; } = string.Empty;

        public int BodyEdgeRateOffsetPercent { get; set; }

        public int BodyEdgePitchOffsetHz { get; set; }

        public double TransitionSeconds { get; set; } = 0.6;

        public string OutputAspectId { get; set; } = ShowcaseOutputAspectPresets.DefaultId;

        public ShowcaseSubtitleDisplayPlan SubtitleDisplay { get; set; }

        public bool SfxMasterEnabled { get; set; } = true;

        public string CtaSfxFile { get; set; } = string.Empty;

        public bool CtaSfxEnabled { get; set; }

        public double CtaSfxOffsetSeconds { get; set; }

        public int CtaSfxVolumePercent { get; set; }

        public string HookSfxFile { get; set; } = string.Empty;

        public bool HookSfxEnabled { get; set; }

        public double HookSfxOffsetSeconds { get; set; }

        public int HookSfxVolumePercent { get; set; }

        public string ProfileName { get; set; } = string.Empty;

        public bool BrandLogoEnabled { get; set; }

        public string BrandLogoFile { get; set; } = string.Empty;

        public string BrandLogoResolvedPath { get; set; } = string.Empty;

        public string BrandLogoPositionId { get; set; } = ShowcaseBrandLogoPositionCatalog.BottomRight;

        public int BrandLogoScaleWidthPercent { get; set; } = ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent;

        public int BrandLogoMarginX { get; set; } = ShowcaseBrandOverlayHelper.DefaultMargin;

        public int BrandLogoMarginY { get; set; } = ShowcaseBrandOverlayHelper.DefaultMargin;

        public int BrandLogoOpacityPercent { get; set; } = ShowcaseBrandOverlayHelper.DefaultOpacityPercent;

        public static ShowcasePerVideoRenderSettings FromVideo(ShowcaseVideoItem video, AppSettings appSettings = null)
        {
            if (video == null)
            {
                return new ShowcasePerVideoRenderSettings
                {
                    TransitionSeconds = ShowcaseTransitionHelper.ClampSeconds(appSettings?.VideoTransitionDurationSeconds ?? 0.6d),
                    OutputAspectId = ShowcaseOutputAspectPresets.ResolveId(null, appSettings?.ShowcaseOutputAspectDefault)
                };
            }

            return new ShowcasePerVideoRenderSettings
            {
                SubtitleFontName = video.ShowcaseSubtitleFontName ?? string.Empty,
                SubtitleFontSize = video.ShowcaseSubtitleFontSize > 0 ? video.ShowcaseSubtitleFontSize : 72,
                SubtitlePosition = video.ShowcaseSubtitlePosition ?? "Bottom",
                SubtitleAnimation = video.ShowcaseSubtitleAnimation ?? "Pop",
                SubtitleBold = video.ShowcaseSubtitleBold,
                SubtitleItalic = video.ShowcaseSubtitleItalic,
                SubtitleWordsPerLine = video.ShowcaseSubtitleWordsPerLine > 0 ? video.ShowcaseSubtitleWordsPerLine : 6,
                SubtitleEnabled = video.ShowcaseSubtitleEnabled,
                HookSubtitleEnabled = video.ShowcaseHookSubtitleEnabled,
                HookSubtitleAnimation = video.ShowcaseHookSubtitleAnimation ?? "PopStrong",
                HookSubtitleFontName = video.ShowcaseHookSubtitleFontName ?? string.Empty,
                HookSubtitleFontSize = video.ShowcaseHookSubtitleFontSize,
                BackgroundMusicFile = video.ShowcaseBackgroundMusicFile ?? string.Empty,
                MusicVolume = video.ShowcaseMusicVolume >= 0 ? video.ShowcaseMusicVolume : 14,
                NarrationSpeedPercent = ShowcaseNarrationSpeedHelper.ResolveRenderSpeedPercent(video),
                TtsEngine = video.ShowcaseTtsEngine ?? string.Empty,
                HookTtsEngine = video.ShowcaseHookTtsEngine ?? string.Empty,
                BodyTtsEngine = video.ShowcaseBodyTtsEngine ?? string.Empty,
                VoicePresetId = video.ShowcaseVoicePresetId ?? string.Empty,
                VoiceAgeId = video.ShowcaseVoiceAgeId ?? string.Empty,
                VoiceLanguageId = video.ShowcaseVoiceLanguageId ?? string.Empty,
                HookElevenPersona = video.ShowcaseHookElevenPersona ?? string.Empty,
                VoiceToneId = video.ShowcaseVoiceToneId ?? string.Empty,
                ElevenCustomStabilityPercent = video.ShowcaseElevenCustomStabilityPercent,
                ElevenCustomSimilarityPercent = video.ShowcaseElevenCustomSimilarityPercent,
                ElevenCustomStylePercent = video.ShowcaseElevenCustomStylePercent,
                HookStyleKey = video.ShowcaseHookStyleKey ?? string.Empty,
                EdgeRateOffsetPercent = video.ShowcaseEdgeRateOffsetPercent,
                EdgePitchOffsetHz = video.ShowcaseEdgePitchOffsetHz,
                BodyVoicePresetId = video.ShowcaseBodyVoicePresetId ?? string.Empty,
                BodyVoiceAgeId = video.ShowcaseBodyVoiceAgeId ?? string.Empty,
                BodyVoiceLanguageId = video.ShowcaseBodyVoiceLanguageId ?? string.Empty,
                BodyElevenPersona = video.ShowcaseBodyElevenPersona ?? string.Empty,
                BodyVoiceToneId = video.ShowcaseBodyVoiceToneId ?? string.Empty,
                BodyElevenCustomStabilityPercent = video.ShowcaseBodyElevenCustomStabilityPercent,
                BodyElevenCustomSimilarityPercent = video.ShowcaseBodyElevenCustomSimilarityPercent,
                BodyElevenCustomStylePercent = video.ShowcaseBodyElevenCustomStylePercent,
                BodyStyleKey = video.ShowcaseBodyStyleKey ?? string.Empty,
                BodyEdgeRateOffsetPercent = video.ShowcaseBodyEdgeRateOffsetPercent,
                BodyEdgePitchOffsetHz = video.ShowcaseBodyEdgePitchOffsetHz,
                TransitionSeconds = ShowcaseTransitionHelper.ClampSeconds(appSettings?.VideoTransitionDurationSeconds ?? 0.6d),
                OutputAspectId = ShowcaseOutputAspectPresets.ResolveId(
                    video.ShowcaseOutputAspectId,
                    appSettings?.ShowcaseOutputAspectDefault),
                SubtitleDisplay = ShowcaseSubtitleDisplayHelper.FromVideo(video),
                SfxMasterEnabled = video.ShowcaseSfxMasterEnabled,
                CtaSfxFile = video.ShowcaseCtaSfxFile ?? string.Empty,
                CtaSfxEnabled = video.ShowcaseCtaSfxEnabled,
                CtaSfxOffsetSeconds = video.ShowcaseCtaSfxOffsetSeconds,
                CtaSfxVolumePercent = video.ShowcaseCtaSfxVolumePercent > 0
                    ? video.ShowcaseCtaSfxVolumePercent
                    : ShowcaseSfxCatalog.DefaultVolumePercent,
                HookSfxFile = video.ShowcaseHookSfxFile ?? string.Empty,
                HookSfxEnabled = video.ShowcaseHookSfxEnabled,
                HookSfxOffsetSeconds = video.ShowcaseHookSfxOffsetSeconds,
                HookSfxVolumePercent = video.ShowcaseHookSfxVolumePercent > 0
                    ? video.ShowcaseHookSfxVolumePercent
                    : ShowcaseSfxCatalog.DefaultVolumePercent,
                ProfileName = video.ProfileName ?? string.Empty,
                BrandLogoEnabled = video.ShowcaseBrandLogoEnabled,
                BrandLogoFile = video.ShowcaseBrandLogoFile ?? string.Empty,
                BrandLogoResolvedPath = ShowcaseBrandOverlayHelper.ResolveEffectiveLogoPath(
                    video.ShowcaseBrandLogoFile,
                    video.ProfileName,
                    appSettings),
                BrandLogoPositionId = ShowcaseBrandLogoPositionCatalog.ResolveId(video.ShowcaseBrandLogoPositionId),
                BrandLogoScaleWidthPercent = ShowcaseBrandOverlayHelper.ClampScaleWidthPercent(
                    video.ShowcaseBrandLogoScaleWidthPercent > 0
                        ? video.ShowcaseBrandLogoScaleWidthPercent
                        : ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent),
                BrandLogoMarginX = ShowcaseBrandOverlayHelper.ClampMargin(video.ShowcaseBrandLogoMarginX),
                BrandLogoMarginY = ShowcaseBrandOverlayHelper.ClampMargin(video.ShowcaseBrandLogoMarginY),
                BrandLogoOpacityPercent = ShowcaseBrandOverlayHelper.ClampOpacityPercent(
                    video.ShowcaseBrandLogoOpacityPercent > 0
                        ? video.ShowcaseBrandLogoOpacityPercent
                        : ShowcaseBrandOverlayHelper.DefaultOpacityPercent)
            };
        }

        public static ShowcaseTtsRenderOptions ToTtsOptions(ShowcasePerVideoRenderSettings renderSettings, AppSettings appSettings)
        {
            var video = new ShowcaseVideoItem
            {
                ShowcaseTtsEngine = renderSettings?.TtsEngine ?? string.Empty,
                ShowcaseHookTtsEngine = renderSettings?.HookTtsEngine ?? string.Empty,
                ShowcaseBodyTtsEngine = renderSettings?.BodyTtsEngine ?? string.Empty,
                ShowcaseVoicePresetId = renderSettings?.VoicePresetId ?? string.Empty,
                ShowcaseVoiceAgeId = renderSettings?.VoiceAgeId ?? string.Empty,
                ShowcaseVoiceLanguageId = renderSettings?.VoiceLanguageId ?? string.Empty,
                ShowcaseHookElevenPersona = renderSettings?.HookElevenPersona ?? string.Empty,
                ShowcaseVoiceToneId = renderSettings?.VoiceToneId ?? string.Empty,
                ShowcaseElevenCustomStabilityPercent = renderSettings?.ElevenCustomStabilityPercent ?? 0,
                ShowcaseElevenCustomSimilarityPercent = renderSettings?.ElevenCustomSimilarityPercent ?? 0,
                ShowcaseElevenCustomStylePercent = renderSettings?.ElevenCustomStylePercent ?? 0,
                ShowcaseHookStyleKey = renderSettings?.HookStyleKey ?? string.Empty,
                ShowcaseEdgeRateOffsetPercent = renderSettings?.EdgeRateOffsetPercent ?? 0,
                ShowcaseEdgePitchOffsetHz = renderSettings?.EdgePitchOffsetHz ?? 0,
                ShowcaseBodyVoicePresetId = renderSettings?.BodyVoicePresetId ?? string.Empty,
                ShowcaseBodyVoiceAgeId = renderSettings?.BodyVoiceAgeId ?? string.Empty,
                ShowcaseBodyVoiceLanguageId = renderSettings?.BodyVoiceLanguageId ?? string.Empty,
                ShowcaseBodyElevenPersona = renderSettings?.BodyElevenPersona ?? string.Empty,
                ShowcaseBodyVoiceToneId = renderSettings?.BodyVoiceToneId ?? string.Empty,
                ShowcaseBodyElevenCustomStabilityPercent = renderSettings?.BodyElevenCustomStabilityPercent ?? 0,
                ShowcaseBodyElevenCustomSimilarityPercent = renderSettings?.BodyElevenCustomSimilarityPercent ?? 0,
                ShowcaseBodyElevenCustomStylePercent = renderSettings?.BodyElevenCustomStylePercent ?? 0,
                ShowcaseBodyStyleKey = renderSettings?.BodyStyleKey ?? string.Empty,
                ShowcaseBodyEdgeRateOffsetPercent = renderSettings?.BodyEdgeRateOffsetPercent ?? 0,
                ShowcaseBodyEdgePitchOffsetHz = renderSettings?.BodyEdgePitchOffsetHz ?? 0
            };
            return ShowcaseTtsHelper.ResolveFromVideo(video, appSettings);
        }
    }
}
