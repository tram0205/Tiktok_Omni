using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Entry point popup Âm thanh tab Quote — tách khỏi Showcase grid editors.</summary>
    internal static class PhilosophyAudioEditorForm
    {
        public static ShowcaseBackgroundMusicEditorForm Create(
            ShowcaseVideoItem video,
            AppSettings settings,
            IReadOnlyList<string> musicFileNames,
            Func<Task> generateBodyNarrationAsync,
            Func<Task> listenBodyNarrationAsync,
            Func<bool> canListenBodyNarration,
            Func<Task> renderFullMixedAudioAsync,
            Func<Task> listenFullMixedAudioAsync,
            Func<bool> canRenderFullMixedAudio,
            Func<bool> canListenFullMixedAudio,
            string philosophyAmbientKey,
            string philosophyMusicLibrarySummary,
            PhilosophyBatchItem philosophyBatch) =>
            ShowcaseBackgroundMusicEditorForm.CreatePhilosophy(
                video,
                settings,
                musicFileNames,
                generateBodyNarrationAsync,
                listenBodyNarrationAsync,
                canListenBodyNarration,
                renderFullMixedAudioAsync,
                listenFullMixedAudioAsync,
                canRenderFullMixedAudio,
                canListenFullMixedAudio,
                philosophyAmbientKey,
                philosophyMusicLibrarySummary,
                philosophyBatch);
    }
}
