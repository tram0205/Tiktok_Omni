using System;
using System.Globalization;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Thời lượng clip từng cảnh — Gemini gợi ý + fallback theo thoại.</summary>
    internal static class ShowcaseSceneDurationHelper
    {
        public const double MinClipSeconds = 3.0;
        public const double MaxClipSeconds = 12.0;
        public const double DefaultClipSeconds = 6.0;
        public const double SilentSceneSeconds = 4.5;

        public static double Clamp(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                return DefaultClipSeconds;
            }

            return Math.Max(MinClipSeconds, Math.Min(MaxClipSeconds, seconds));
        }

        public static double EstimateFromVoiceover(string voiceover, bool silent)
        {
            if (silent)
            {
                return SilentSceneSeconds;
            }

            var text = (voiceover ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return DefaultClipSeconds;
            }

            var words = ShowcaseVoiceoverFitHelper.CountWords(text);
            if (words <= 0)
            {
                return DefaultClipSeconds;
            }

            var seconds = words / ShowcaseVoiceoverFitHelper.WordsPerSecond
                          + ShowcaseVoiceoverFitHelper.SlotPaddingSeconds
                          + 0.35d;
            return Clamp(seconds);
        }

        /// <summary>Ưu tiên <see cref="AiVideoGenInputItem.ShowcaseClipDurationSeconds"/> từ Gemini; không có thì ước từ thoại.</summary>
        public static double ResolveZoomClipDuration(AiVideoGenInputItem scene)
        {
            if (scene == null)
            {
                return DefaultClipSeconds;
            }

            if (scene.ShowcaseClipDurationSeconds > 0)
            {
                return Clamp(scene.ShowcaseClipDurationSeconds);
            }

            return EstimateFromVoiceover(scene.SceneVoiceover, scene.ShowcaseSceneSilent);
        }

        public static string FormatSecondsLog(double seconds) =>
            Clamp(seconds).ToString("0.#", CultureInfo.InvariantCulture) + "s";
    }
}
