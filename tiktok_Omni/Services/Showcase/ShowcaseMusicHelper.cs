using System;

using System.Collections.Generic;

using System.IO;

using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase

{

    internal static class ShowcaseMusicHelper

    {

        public static List<string> ListMusicFileNames(AppSettings settings) =>

            VideoReupRemixService.ListMusicFileNames(settings);



        public static string GetMusicLibraryDirectory(AppSettings settings) =>
            OmniAudioLibrary.GetPrimaryMusicDirectory(settings);

        public static string GetSharedMusicDirectory(AppSettings settings) =>
            OmniAudioLibrary.GetSharedMusicDirectory(settings);

        public static string GetSharedSfxDirectory(AppSettings settings) =>
            OmniAudioLibrary.GetSharedSfxDirectory(settings);



        public static IReadOnlyList<string> GetMusicSearchDirectories(AppSettings settings) =>

            VideoReupRemixService.GetMusicSearchDirectories(settings);



        public static string ResolveMusicFilePath(string fileName, AppSettings settings) =>

            VideoReupRemixService.ResolveMusicFilePath(fileName, settings);



        public static void EnsureVideoDefaults(ShowcaseVideoItem video, AppSettings settings)

        {

            if (video == null)

            {

                return;

            }



            if (video.ShowcaseMusicVolume < 0)

            {

                video.ShowcaseMusicVolume = settings?.VideoMusicVolume ?? 14;

            }

            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);

            RefreshMusicLabel(video);

        }



        public static void RefreshMusicLabel(ShowcaseVideoItem video)

        {

            if (video == null)

            {

                return;

            }



            video.ShowcaseMusicLabel = FormatMusicSummary(
                video.ShowcaseBackgroundMusicFile,
                video.ShowcaseMusicVolume,
                video.ShowcaseNarrationSpeedPercent,
                video.ShowcaseTtsEngine,
                video.ShowcaseVoicePresetId,
                video.ShowcaseVoiceAgeId,
                video.ShowcaseVoiceLanguageId,
                video);
        }

        public static string FormatMusicSummary(
            string musicFile,
            int volumePercent,
            int narrationSpeedPercent = 0,
            string ttsEngine = null,
            string voicePresetId = null,
            string voiceAgeId = null,
            string voiceLanguageId = null,
            ShowcaseVideoItem sfxSource = null)
        {
            var pick = (musicFile ?? string.Empty).Trim();
            var vol = volumePercent < 0 ? 0 : Math.Min(100, volumePercent);
            var speedSuffix = FormatNarrationSpeedSummary(narrationSpeedPercent);
            var voiceSuffix = FormatVoiceSummary(ttsEngine, voicePresetId, voiceAgeId, voiceLanguageId);
            var sfxSuffix = FormatSfxSummary(sfxSource);



            if (VideoReupRowItem.IsNoMusicSelection(pick))

            {

                return VideoReupRowItem.NoMusicSelectionLabel + " · " + voiceSuffix + " · " + speedSuffix + sfxSuffix;

            }



            if (string.IsNullOrWhiteSpace(pick))

            {

                return vol + "% · " + voiceSuffix + " · " + speedSuffix + sfxSuffix;

            }



            var shortName = pick.Length > 22 ? pick.Substring(0, 20) + "…" : pick;

            return shortName + " · " + vol + "% · " + voiceSuffix + " · " + speedSuffix + sfxSuffix;

        }



        private static string FormatVoiceSummary(
            string ttsEngine,
            string voicePresetId,
            string voiceAgeId,
            string voiceLanguageId)
        {
            var engine = (ttsEngine ?? string.Empty).Trim();
            if (string.Equals(engine, ShowcaseTtsHelper.EngineAskOnCreate, StringComparison.OrdinalIgnoreCase))
            {
                return "giọng: hỏi lúc TTS";
            }

            var shortVoice = ShowcaseVoicePresetCatalog.FormatShortLabel(voicePresetId, voiceAgeId, voiceLanguageId);
            if (string.Equals(engine, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                return "EL · " + shortVoice;
            }

            if (string.Equals(engine, ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                || string.Equals(engine, TtsEngineKind.EdgeTts.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(engine, ShowcaseTtsHelper.EnginePiperOfflineLegacy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(engine, "PiperOffline", StringComparison.OrdinalIgnoreCase))
            {
                return "Edge · " + shortVoice;
            }

            return "Edge · " + shortVoice;
        }

        private static string FormatNarrationSpeedSummary(int narrationSpeedPercent)
        {
            return ShowcaseNarrationSpeedHelper.FormatGridLabel(narrationSpeedPercent);
        }

        private static string FormatSfxSummary(ShowcaseVideoItem video)
        {
            if (video == null || !video.ShowcaseSfxMasterEnabled)
            {
                return " · SFX: tắt";
            }

            var active = 0;
            foreach (var scene in video.Scenes)
            {
                if (scene != null && scene.ShowcaseSfxEnabled && !string.IsNullOrWhiteSpace(scene.ShowcaseSfxFile))
                {
                    active++;
                }
            }

            if (video.ShowcaseCtaSfxEnabled && !string.IsNullOrWhiteSpace(video.ShowcaseCtaSfxFile))
            {
                active++;
            }

            if (video.ShowcaseHookSfxEnabled && !string.IsNullOrWhiteSpace(video.ShowcaseHookSfxFile))
            {
                active++;
            }

            return active > 0 ? " · SFX: " + active : " · SFX: 0";
        }

    }
}


