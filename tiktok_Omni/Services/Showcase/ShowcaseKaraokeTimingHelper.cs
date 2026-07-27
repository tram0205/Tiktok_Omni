using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseKaraokeTimingHelper
    {
        /// <summary>Ước lượng timestamp theo segment hook/thân, scale theo audio render (sau tua).</summary>
        public static async Task<List<WordTimestamp>> EstimateWordTimestampsAsync(
            ShowcaseNarrationTimingManifest manifest,
            string renderAudioPath,
            string ffmpegExecutablePath,
            CancellationToken cancellationToken)
        {
            if (manifest == null)
            {
                return new List<WordTimestamp>();
            }

            var renderDurationMs = await SubtitleTimingHelper.GetAudioDurationMsAsync(
                    ffmpegExecutablePath,
                    renderAudioPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (renderDurationMs < 50d)
            {
                return new List<WordTimestamp>();
            }

            var originalTotalMs = Math.Max(1d, manifest.TotalSpeechSeconds * 1000d);
            var scale = renderDurationMs / originalTotalMs;
            if (scale <= 0.01d)
            {
                scale = 1d;
            }

            var speechStartMs = manifest.SpeechStartSeconds * 1000d * scale;
            var hookMs = Math.Max(0d, manifest.HookAudioSeconds * 1000d * scale);
            var bodyMs = Math.Max(0d, manifest.BodyAudioSeconds * 1000d * scale);

            var result = new List<WordTimestamp>();
            AppendSegment(result, manifest.HookText, speechStartMs, hookMs);
            AppendSegment(result, manifest.BodyText, speechStartMs + hookMs, bodyMs);
            return result;
        }

        private static void AppendSegment(
            List<WordTimestamp> target,
            string text,
            double startOffsetMs,
            double segmentDurationMs)
        {
            if (string.IsNullOrWhiteSpace(text) || segmentDurationMs <= 1d)
            {
                return;
            }

            var segmentWords = SubtitleTimingHelper.EstimateWordTimestamps(text, segmentDurationMs);
            foreach (var word in segmentWords)
            {
                target.Add(new WordTimestamp
                {
                    Text = word.Text,
                    StartTimeMs = Math.Round(startOffsetMs + word.StartTimeMs, 2),
                    EndTimeMs = Math.Round(startOffsetMs + word.EndTimeMs, 2)
                });
            }
        }

        public static string ResolveDisplayScript(
            ShowcaseNarrationTimingManifest manifest,
            string fallbackScript)
        {
            if (manifest == null)
            {
                return fallbackScript ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(manifest.FullText))
            {
                return manifest.FullText.Trim();
            }

            return ShowcaseVoiceoverFitHelper.MergePassage(
                new[] { manifest.HookText, manifest.BodyText });
        }

        /// <summary>Tách từ hook/thân theo manifest hoặc ranh giới thời gian.</summary>
        public static void SplitHookBodyWords(
            IReadOnlyList<WordTimestamp> allWords,
            ShowcaseNarrationTimingManifest manifest,
            List<WordTimestamp> hookWords,
            List<WordTimestamp> bodyWords)
        {
            hookWords?.Clear();
            bodyWords?.Clear();
            if (allWords == null || allWords.Count == 0)
            {
                return;
            }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.HookText))
            {
                foreach (var word in allWords)
                {
                    bodyWords?.Add(word);
                }

                return;
            }

            var hookTokenCount = SubtitleTimingHelper.SplitWords(manifest.HookText).Count;
            if (hookTokenCount <= 0)
            {
                foreach (var word in allWords)
                {
                    bodyWords?.Add(word);
                }

                return;
            }

            var boundaryMs = ResolveHookBoundaryMs(manifest, allWords);
            var usedTimeSplit = boundaryMs > 0d;
            for (var i = 0; i < allWords.Count; i++)
            {
                var word = allWords[i];
                var isHook = usedTimeSplit
                    ? word.StartTimeMs < boundaryMs - 0.5d
                    : i < hookTokenCount;

                if (isHook)
                {
                    hookWords?.Add(word);
                }
                else
                {
                    bodyWords?.Add(word);
                }
            }
        }

        private static double ResolveHookBoundaryMs(
            ShowcaseNarrationTimingManifest manifest,
            IReadOnlyList<WordTimestamp> allWords)
        {
            if (manifest == null || allWords == null || allWords.Count == 0)
            {
                return 0d;
            }

            var originalTotalMs = Math.Max(1d, manifest.TotalSpeechSeconds * 1000d);
            var lastMs = allWords[allWords.Count - 1].EndTimeMs;
            var scale = lastMs / originalTotalMs;
            if (scale <= 0.01d)
            {
                scale = 1d;
            }

            return (manifest.SpeechStartSeconds + manifest.HookAudioSeconds) * 1000d * scale;
        }
    }
}
