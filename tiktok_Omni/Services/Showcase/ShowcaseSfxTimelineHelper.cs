using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseSfxTimelineHelper
    {
        public sealed class TimelinePlan
        {
            public double RawStitchedSeconds { get; set; }

            public double[] SceneStartSeconds { get; set; } = Array.Empty<double>();

            public double[] SceneDurationSeconds { get; set; } = Array.Empty<double>();
        }

        public static async Task<TimelinePlan> BuildAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            IList<string> clipPaths,
            double transitionSeconds,
            string ffprobeExecutable,
            CancellationToken cancellationToken)
        {
            var count = orderedScenes?.Count ?? 0;
            if (count <= 0)
            {
                return new TimelinePlan();
            }

            var durations = new double[count];
            for (var i = 0; i < count; i++)
            {
                durations[i] = ResolveFallbackDuration(orderedScenes[i]);
                var clip = i < (clipPaths?.Count ?? 0) ? clipPaths[i] : orderedScenes[i]?.ClipPath;
                if (!string.IsNullOrWhiteSpace(clip) && File.Exists(clip))
                {
                    var probed = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                            ffprobeExecutable,
                            clip,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (probed > 0.05d)
                    {
                        durations[i] = probed;
                    }
                }
            }

            var trans = Math.Max(0d, transitionSeconds);
            var starts = new double[count];
            var cursor = 0d;
            for (var i = 0; i < count; i++)
            {
                starts[i] = cursor;
                cursor += durations[i];
                if (i < count - 1)
                {
                    cursor -= trans;
                }
            }

            return new TimelinePlan
            {
                RawStitchedSeconds = Math.Max(0.1d, cursor),
                SceneStartSeconds = starts,
                SceneDurationSeconds = durations
            };
        }

        public static double ScaleTime(double rawSeconds, double rawTotal, double finalDuration)
        {
            if (rawTotal <= 0.01d || finalDuration <= 0.01d)
            {
                return Math.Max(0d, rawSeconds);
            }

            return Math.Max(0d, rawSeconds * (finalDuration / rawTotal));
        }

        private static double ResolveFallbackDuration(AiVideoGenInputItem scene)
        {
            if (scene != null && scene.ShowcaseClipDurationSeconds > 0.05d)
            {
                return scene.ShowcaseClipDurationSeconds;
            }

            return 6d;
        }
    }
}
