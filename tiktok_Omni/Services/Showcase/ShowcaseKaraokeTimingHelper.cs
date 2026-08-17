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
        /// <summary>Ước lượng timestamp theo segment hook/thân — scale theo audio render thực tế (tốc độ thoại / AV sync).</summary>
        public static async Task<List<WordTimestamp>> EstimateWordTimestampsAsync(
            ShowcaseNarrationTimingManifest manifest,
            string renderAudioPath,
            string ffmpegExecutablePath,
            CancellationToken cancellationToken,
            double appliedAudioTempo = 1d)
        {
            if (manifest == null)
            {
                return new List<WordTimestamp>();
            }

            if (string.IsNullOrWhiteSpace(renderAudioPath))
            {
                return new List<WordTimestamp>();
            }

            cancellationToken.ThrowIfCancellationRequested();

            var scale = await ResolveManifestTimingScaleAsync(
                    manifest,
                    renderAudioPath,
                    ffmpegExecutablePath,
                    appliedAudioTempo,
                    cancellationToken)
                .ConfigureAwait(false);

            var speechStartMs = manifest.SpeechStartSeconds * 1000d * scale;
            var hookMs = Math.Max(0d, manifest.HookAudioSeconds * 1000d * scale);
            var bodyMs = Math.Max(0d, manifest.BodyAudioSeconds * 1000d * scale);

            var result = new List<WordTimestamp>();
            AppendSegment(result, manifest.HookText, speechStartMs, hookMs);
            AppendSegment(result, manifest.BodyText, speechStartMs + hookMs, bodyMs);
            return result;
        }

        /// <summary>Nén timestamp khi manifest gốc dài hơn audio render (tốc độ thoại / full_mix_preview).</summary>
        public static async Task<List<WordTimestamp>> NormalizeTimestampsToRenderAudioAsync(
            List<WordTimestamp> words,
            string renderAudioPath,
            string ffmpegExecutablePath,
            ShowcaseNarrationTimingManifest manifest,
            CancellationToken cancellationToken)
        {
            if (words == null || words.Count == 0)
            {
                return words ?? new List<WordTimestamp>();
            }

            var lastEnd = words[words.Count - 1].EndTimeMs;
            if (lastEnd <= 1d)
            {
                return words;
            }

            var audioMs = await SubtitleTimingHelper.GetAudioDurationMsAsync(
                    ffmpegExecutablePath,
                    renderAudioPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (audioMs <= 50d)
            {
                return words;
            }

            var targetMs = lastEnd;
            if (manifest != null && manifest.TotalSpeechSeconds > 0.05d)
            {
                var manifestMs = manifest.TotalSpeechSeconds * 1000d;
                if (manifestMs > audioMs * 1.03d)
                {
                    targetMs = audioMs;
                }
            }

            if (lastEnd <= targetMs * 1.03d)
            {
                return words;
            }

            var scale = targetMs / lastEnd;
            foreach (var word in words)
            {
                word.StartTimeMs = Math.Round(word.StartTimeMs * scale, 2);
                word.EndTimeMs = Math.Round(word.EndTimeMs * scale, 2);
            }

            return words;
        }

        private static async Task<double> ResolveManifestTimingScaleAsync(
            ShowcaseNarrationTimingManifest manifest,
            string renderAudioPath,
            string ffmpegExecutablePath,
            double appliedAudioTempo,
            CancellationToken cancellationToken)
        {
            var manifestTotalMs = Math.Max(1d, manifest.TotalSpeechSeconds * 1000d);
            var actualDurationMs = await SubtitleTimingHelper.GetAudioDurationMsAsync(
                    ffmpegExecutablePath,
                    renderAudioPath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (actualDurationMs > 50d && manifestTotalMs > actualDurationMs * 1.03d)
            {
                return actualDurationMs / manifestTotalMs;
            }

            if (appliedAudioTempo > 1.03d)
            {
                return 1d / appliedAudioTempo;
            }

            return 1d;
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
