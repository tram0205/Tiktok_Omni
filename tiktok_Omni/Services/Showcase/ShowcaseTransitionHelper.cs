using System;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseTransitionHelper
    {
        public static double ClampSeconds(double seconds)
        {
            if (seconds < 0.2d)
            {
                return 0.2d;
            }

            if (seconds > 2.0d)
            {
                return 2.0d;
            }

            return seconds;
        }

        public static void EnsureVideoDefaults(ShowcaseVideoItem video, AppSettings settings)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseTransitionSeconds = ClampSeconds(settings?.VideoTransitionDurationSeconds ?? 0.6d);
            RefreshTransitionLabel(video);
        }

        public static void RefreshTransitionLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseTransitionLabel = FormatTransitionSummary(video.ShowcaseTransitionSeconds);
        }

        public static string FormatTransitionSummary(double seconds)
        {
            var value = ClampSeconds(seconds);
            var preset = value <= 0.35d
                ? "Nhanh"
                : value >= 1.0d
                    ? "Chậm"
                    : "Vừa";
            return preset + " · " + value.ToString("0.0") + "s";
        }
    }
}
