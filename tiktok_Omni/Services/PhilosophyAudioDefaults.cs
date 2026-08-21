using System;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Mặc định âm thanh tab Quote — tách khỏi <see cref="AppSettings.VideoMusicVolume"/> (Showcase/Reup).</summary>
    public static class PhilosophyAudioDefaults
    {
        public const int DefaultMusicVolumePercent = 100;
        public const int DefaultAmbientVolumePercent = 70;
        public const int DefaultNarrationSpeedPercent = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;

        public sealed class ResolvedDefaults
        {
            public int MusicVolumePercent { get; set; } = DefaultMusicVolumePercent;

            public string TtsEngine { get; set; } = ShowcaseTtsHelper.EngineEdgeTts;

            public string BodyStyleKey { get; set; } = HookStyleCatalog.StyleKechuyen;

            public string AmbientKey { get; set; } = PhilosophyAmbientCatalog.NoneKey;

            public int NarrationSpeedPercent { get; set; } = DefaultNarrationSpeedPercent;
        }

        public static ResolvedDefaults Resolve(AppSettings settings)
        {
            var resolved = new ResolvedDefaults();
            if (settings == null)
            {
                return resolved;
            }

            resolved.MusicVolumePercent = ClampVolume(settings.PhilosophyDefaultMusicVolumePercent);
            resolved.NarrationSpeedPercent = ShowcaseNarrationSpeedHelper.ClampManualPercent(
                settings.PhilosophyDefaultNarrationSpeedPercent);
            if (resolved.NarrationSpeedPercent <= 0)
            {
                resolved.NarrationSpeedPercent = DefaultNarrationSpeedPercent;
            }

            resolved.TtsEngine = NormalizeEngine(settings.PhilosophyDefaultTtsEngine);
            resolved.BodyStyleKey = NormalizeBodyStyleKey(settings.PhilosophyDefaultBodyStyleKey);
            resolved.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(settings.PhilosophyDefaultAmbientKey);
            return resolved;
        }

        public static void ApplyToBatch(PhilosophyBatchItem batch, AppSettings settings)
        {
            if (batch == null)
            {
                return;
            }

            var defaults = Resolve(settings);
            if (batch.MusicVolumePercent <= 0)
            {
                batch.MusicVolumePercent = defaults.MusicVolumePercent;
            }

            if (batch.BodyNarrationSpeedPercent <= 0)
            {
                batch.BodyNarrationSpeedPercent = defaults.NarrationSpeedPercent;
            }

            if (string.IsNullOrWhiteSpace(batch.BodyTtsEngine))
            {
                batch.BodyTtsEngine = defaults.TtsEngine;
            }

            if (string.IsNullOrWhiteSpace(batch.BodyStyleKey))
            {
                batch.BodyStyleKey = defaults.BodyStyleKey;
            }

            if (string.IsNullOrWhiteSpace(batch.AmbientKey))
            {
                batch.AmbientKey = defaults.AmbientKey;
            }

            if (string.IsNullOrWhiteSpace(batch.BodyVoiceLanguageId))
            {
                batch.BodyVoiceLanguageId = ShowcaseVoicePresetDimensions.Language.ViSouth;
            }
        }

        /// <summary>Chuẩn hoá ShowcaseVideoItem cho popup Âm thanh Quote — không dùng VideoMusicVolume Showcase.</summary>
        public static void EnsureVideoDefaults(ShowcaseVideoItem video, AppSettings settings)
        {
            if (video == null)
            {
                return;
            }

            var defaults = Resolve(settings);
            if (video.ShowcaseMusicVolume < 0)
            {
                video.ShowcaseMusicVolume = defaults.MusicVolumePercent;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyTtsEngine))
            {
                video.ShowcaseBodyTtsEngine = defaults.TtsEngine;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseBodyStyleKey))
            {
                video.ShowcaseBodyStyleKey = defaults.BodyStyleKey;
            }

            if (video.ShowcaseBodyNarrationSpeedPercent <= 0)
            {
                video.ShowcaseBodyNarrationSpeedPercent = defaults.NarrationSpeedPercent;
            }

            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
            ShowcaseMusicHelper.RefreshMusicLabel(video);
        }

        public static int ClampVolume(int volumePercent) =>
            PhilosophyBatchHelper.ClampMusicVolumePercent(
                volumePercent > 0 ? volumePercent : DefaultMusicVolumePercent);

        private static string NormalizeEngine(string raw)
        {
            var engine = (raw ?? string.Empty).Trim();
            if (string.Equals(engine, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseTtsHelper.EngineElevenLabs;
            }

            if (string.Equals(engine, ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                || string.Equals(engine, ShowcaseTtsHelper.EnginePiperOfflineLegacy, StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseTtsHelper.EngineEdgeTts;
            }

            return ShowcaseTtsHelper.EngineEdgeTts;
        }

        private static string NormalizeBodyStyleKey(string raw)
        {
            var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                return HookStyleCatalog.StyleKechuyen;
            }

            foreach (var allowed in HookStyleCatalog.AllStyleKeys)
            {
                if (string.Equals(allowed, key, StringComparison.OrdinalIgnoreCase))
                {
                    return allowed;
                }
            }

            return HookStyleCatalog.StyleKechuyen;
        }
    }
}
