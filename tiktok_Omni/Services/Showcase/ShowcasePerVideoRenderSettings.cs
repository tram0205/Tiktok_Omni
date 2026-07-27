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
                NarrationSpeedPercent = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(video.ShowcaseNarrationSpeedPercent),
                TtsEngine = video.ShowcaseTtsEngine ?? string.Empty,
                VoicePresetId = video.ShowcaseVoicePresetId ?? string.Empty,
                VoiceAgeId = video.ShowcaseVoiceAgeId ?? string.Empty,
                VoiceLanguageId = video.ShowcaseVoiceLanguageId ?? string.Empty,
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
                    : ShowcaseSfxCatalog.DefaultVolumePercent
            };
        }

        public static ShowcaseTtsRenderOptions ToTtsOptions(ShowcasePerVideoRenderSettings renderSettings, AppSettings appSettings)
        {
            var video = new ShowcaseVideoItem
            {
                ShowcaseTtsEngine = renderSettings?.TtsEngine ?? string.Empty,
                ShowcaseVoicePresetId = renderSettings?.VoicePresetId ?? string.Empty,
                ShowcaseVoiceAgeId = renderSettings?.VoiceAgeId ?? string.Empty,
                ShowcaseVoiceLanguageId = renderSettings?.VoiceLanguageId ?? string.Empty
            };
            return ShowcaseTtsHelper.ResolveFromVideo(video, appSettings);
        }
    }
}
