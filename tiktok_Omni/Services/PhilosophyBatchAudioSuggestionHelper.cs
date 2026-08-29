using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Gợi ý nhạc nền + tiếng đệm theo mood quote (không gọi Gemini).</summary>
    public static class PhilosophyBatchAudioSuggestionHelper
    {
        public static void ApplyMoodSuggestion(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            AppSettings settings,
            string profileName)
        {
            if (quote == null)
            {
                return;
            }

            var mood = quote.Mood ?? "reflective";
            var musicPath = PhilosophyProfileAssets.TryPickMusicFile(profileName, mood);
            if (!string.IsNullOrWhiteSpace(musicPath))
            {
                quote.MusicFolder = System.IO.Path.GetFileName(musicPath);
            }
            else if (settings != null)
            {
                var names = OmniAudioLibrary.ListMusicFileNames(settings);
                if (names.Count > 0)
                {
                    quote.MusicFolder = PhilosophyBatchHelper.ResolveGeminiMusicFileName(settings, string.Empty, mood);
                }
            }

            quote.AmbientKey = PhilosophyAmbientCatalog.SuggestForMood(mood);

            if (quote.MusicVolumePercent <= 0 && batch != null)
            {
                quote.MusicVolumePercent = PhilosophyBatchHelper.ResolveBatchMusicVolumePercent(batch);
            }
        }
    }
}
