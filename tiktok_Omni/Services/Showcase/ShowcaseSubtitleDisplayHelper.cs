using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseSceneSubtitleDisplayEntry
    {
        public int SceneOrder { get; set; }

        public string DisplayVoiceover { get; set; } = string.Empty;

        /// <summary>Hiệu ứng dòng — rỗng = tab Kiểu chữ (thân).</summary>
        public string DisplayAnimation { get; set; } = string.Empty;

        public string DisplayPrimaryColourAss { get; set; } = string.Empty;

        public string DisplayDecorPreset { get; set; } = string.Empty;

        public string DisplayFontName { get; set; } = string.Empty;

        public int DisplayFontSize { get; set; }

        public string DisplayFontFace { get; set; } = string.Empty;

        public string DisplayLookPreset { get; set; } = string.Empty;

        public string DisplayPosition { get; set; } = string.Empty;
    }

    public sealed class ShowcaseSubtitleLineRenderOverride
    {
        public string FontName { get; set; } = string.Empty;

        public int FontSize { get; set; }

        public string FontFace { get; set; } = string.Empty;

        public string Position { get; set; } = string.Empty;

        public string PrimaryColourAss { get; set; } = string.Empty;

        public string DecorPreset { get; set; } = string.Empty;

        public string LookPreset { get; set; } = string.Empty;

        public string LineBackgroundColourAss { get; set; } = string.Empty;

        public bool HasAny()
        {
            return !string.IsNullOrWhiteSpace(LookPreset)
                   || !string.IsNullOrWhiteSpace(LineBackgroundColourAss)
                   || !string.IsNullOrWhiteSpace(FontName)
                   || FontSize > 0
                   || !string.IsNullOrWhiteSpace(FontFace)
                   || !string.IsNullOrWhiteSpace(Position)
                   || !string.IsNullOrWhiteSpace(PrimaryColourAss)
                   || !string.IsNullOrWhiteSpace(DecorPreset);
        }
    }

    /// <summary>Chữ burn-in ASS — tách khỏi thoại TTS (xóa từ ở đây không đổi audio).</summary>
    public sealed class ShowcaseSubtitleDisplayPlan
    {
        public string Hook { get; set; } = string.Empty;

        public string HookAnimation { get; set; } = string.Empty;

        public string Cta { get; set; } = string.Empty;

        public string CtaAnimation { get; set; } = string.Empty;

        public List<ShowcaseSceneSubtitleDisplayEntry> Scenes { get; set; } = new List<ShowcaseSceneSubtitleDisplayEntry>();
    }

    public static class ShowcaseSubtitleDisplayHelper
    {
        private static readonly Regex TokenStripRegex = new Regex(@"[^\p{L}\p{N}]", RegexOptions.Compiled);

        public static ShowcaseSubtitleDisplayPlan FromVideo(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return new ShowcaseSubtitleDisplayPlan();
            }

            var plan = new ShowcaseSubtitleDisplayPlan
            {
                Hook = video.ShowcaseSubtitleDisplayHook ?? string.Empty,
                HookAnimation = video.ShowcaseSubtitleDisplayHookAnimation ?? string.Empty,
                Cta = video.ShowcaseSubtitleDisplayCta ?? string.Empty,
                CtaAnimation = video.ShowcaseSubtitleDisplayCtaAnimation ?? string.Empty
            };

            var order = 0;
            foreach (var scene in video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                if (scene == null)
                {
                    continue;
                }

                order++;
                plan.Scenes.Add(new ShowcaseSceneSubtitleDisplayEntry
                {
                    SceneOrder = order,
                    DisplayVoiceover = scene.ShowcaseSubtitleDisplayVoiceover ?? string.Empty,
                    DisplayAnimation = scene.ShowcaseSubtitleDisplayAnimation ?? string.Empty,
                    DisplayPrimaryColourAss = scene.ShowcaseSubtitleDisplayPrimaryColourAss ?? string.Empty,
                    DisplayDecorPreset = scene.ShowcaseSubtitleDisplayDecorPreset ?? string.Empty,
                    DisplayFontName = scene.ShowcaseSubtitleDisplayFontName ?? string.Empty,
                    DisplayFontSize = scene.ShowcaseSubtitleDisplayFontSize,
                    DisplayFontFace = scene.ShowcaseSubtitleDisplayFontFace ?? string.Empty,
                    DisplayLookPreset = scene.ShowcaseSubtitleDisplayLookPreset ?? string.Empty,
                    DisplayPosition = scene.ShowcaseSubtitleDisplayPosition ?? string.Empty
                });
            }

            return plan;
        }

        public static void ApplyPlanToVideo(ShowcaseVideoItem video, ShowcaseSubtitleDisplayPlan plan)
        {
            if (video == null)
            {
                return;
            }

            plan = plan ?? new ShowcaseSubtitleDisplayPlan();
            video.ShowcaseSubtitleDisplayHook = (plan.Hook ?? string.Empty).Trim();
            video.ShowcaseSubtitleDisplayHookAnimation = (plan.HookAnimation ?? string.Empty).Trim();
            video.ShowcaseSubtitleDisplayCta = (plan.Cta ?? string.Empty).Trim();
            video.ShowcaseSubtitleDisplayCtaAnimation = (plan.CtaAnimation ?? string.Empty).Trim();

            var byOrder = (plan.Scenes ?? new List<ShowcaseSceneSubtitleDisplayEntry>())
                .Where(s => s != null && s.SceneOrder > 0)
                .GroupBy(s => s.SceneOrder)
                .ToDictionary(g => g.Key, g => (g.First().DisplayVoiceover ?? string.Empty).Trim());

            var byAnim = (plan.Scenes ?? new List<ShowcaseSceneSubtitleDisplayEntry>())
                .Where(s => s != null && s.SceneOrder > 0)
                .GroupBy(s => s.SceneOrder)
                .ToDictionary(g => g.Key, g => (g.First().DisplayAnimation ?? string.Empty).Trim());

            var order = 0;
            foreach (var scene in video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                if (scene == null)
                {
                    continue;
                }

                order++;
                scene.ShowcaseSubtitleDisplayVoiceover = byOrder.TryGetValue(order, out var text)
                    ? text
                    : string.Empty;
                scene.ShowcaseSubtitleDisplayAnimation = byAnim.TryGetValue(order, out var anim)
                    ? anim
                    : string.Empty;
            }
        }

        public static string ResolveHookSpeechSource(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            foreach (var scene in video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                if (scene != null && !scene.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    return scene.SceneVoiceover.Trim();
                }
            }

            return (video.ShowcaseHookText ?? string.Empty).Trim();
        }

        public static string ResolveDisplayHook(ShowcaseVideoItem video)
        {
            var stored = (video?.ShowcaseSubtitleDisplayHook ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            return ResolveHookSpeechSource(video);
        }

        public static string ResolveDisplaySceneVoiceover(AiVideoGenInputItem scene)
        {
            if (scene == null)
            {
                return string.Empty;
            }

            var stored = (scene.ShowcaseSubtitleDisplayVoiceover ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            return (scene.SceneVoiceover ?? string.Empty).Trim();
        }

        public static string ResolveDisplayCta(ShowcaseVideoItem video)
        {
            var stored = (video?.ShowcaseSubtitleDisplayCta ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            return (video?.ShowcaseCtaText ?? string.Empty).Trim();
        }

        /// <summary>Text hiển thị trong editor — thoại gốc nếu chưa chỉnh.</summary>
        public static string GetEditorDisplayText(string storedOverride, string sourceSpeech)
        {
            var stored = (storedOverride ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            return (sourceSpeech ?? string.Empty).Trim();
        }

        /// <summary>Ảnh chụp thoại TTS trước khi sửa kịch bản — dùng đồng bộ cột phụ đề.</summary>
        public sealed class ScriptSpeechSnapshot
        {
            public string Hook { get; set; } = string.Empty;

            public string Cta { get; set; } = string.Empty;

            public List<string> SceneVoiceovers { get; set; } = new List<string>();

            public static ScriptSpeechSnapshot Capture(ShowcaseVideoItem video)
            {
                if (video == null)
                {
                    return new ScriptSpeechSnapshot();
                }

                var snap = new ScriptSpeechSnapshot
                {
                    Hook = (video.ShowcaseHookText ?? string.Empty).Trim(),
                    Cta = (video.ShowcaseCtaText ?? string.Empty).Trim()
                };

                foreach (var scene in video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
                {
                    snap.SceneVoiceovers.Add(scene == null
                        ? string.Empty
                        : (scene.SceneVoiceover ?? string.Empty).Trim());
                }

                return snap;
            }
        }

        /// <summary>Sau khi sửa thoại kịch bản — chỉ cập nhật phụ đề burn-in nếu đang bám thoại TTS; phụ đề rút gọn/tùy chỉnh giữ nguyên.</summary>
        public static void SyncDisplayTextFromSpeechEdits(ShowcaseVideoItem video, ScriptSpeechSnapshot before)
        {
            if (video == null || before == null)
            {
                return;
            }

            video.ShowcaseSubtitleDisplayHook = ReconcileSpeechLinkedOverride(
                video.ShowcaseSubtitleDisplayHook,
                before.Hook,
                (video.ShowcaseHookText ?? string.Empty).Trim());

            video.ShowcaseSubtitleDisplayCta = ReconcileSpeechLinkedOverride(
                video.ShowcaseSubtitleDisplayCta,
                before.Cta,
                (video.ShowcaseCtaText ?? string.Empty).Trim());

            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            for (var i = 0; i < scenes.Count && i < before.SceneVoiceovers.Count; i++)
            {
                var scene = scenes[i];
                scene.ShowcaseSubtitleDisplayVoiceover = ReconcileSpeechLinkedOverride(
                    scene.ShowcaseSubtitleDisplayVoiceover,
                    before.SceneVoiceovers[i],
                    (scene.SceneVoiceover ?? string.Empty).Trim());
            }
        }

        /// <summary>Phụ đề hiển thị khác thoại TTS (vd. rút gọn vài từ lên video) — không bị sửa khi đổi kịch bản.</summary>
        public static bool IsIndependentDisplayOverride(string storedOverride, string sourceSpeech)
        {
            var storedNorm = NormalizeDisplayLine(storedOverride);
            if (string.IsNullOrEmpty(storedNorm))
            {
                return false;
            }

            return !string.Equals(storedNorm, NormalizeDisplayLine(sourceSpeech), StringComparison.Ordinal);
        }

        private static string ReconcileSpeechLinkedOverride(string storedOverride, string oldSpeech, string newSpeech)
        {
            if (string.Equals(NormalizeDisplayLine(oldSpeech), NormalizeDisplayLine(newSpeech), StringComparison.Ordinal))
            {
                return storedOverride ?? string.Empty;
            }

            if (IsIndependentDisplayOverride(storedOverride, oldSpeech))
            {
                return storedOverride ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>Lưu override: giống hệt thoại gốc → rỗng (hiện đủ khi render).</summary>
        public static string CoalesceDisplayOverrideForSave(string editedText, string sourceSpeech)
        {
            var edited = (editedText ?? string.Empty).Trim();
            var source = (sourceSpeech ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(edited) || string.IsNullOrEmpty(source))
            {
                return edited;
            }

            if (string.Equals(NormalizeDisplayLine(edited), NormalizeDisplayLine(source), StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return edited;
        }

        /// <summary>Lưu hiệu ứng dòng: trùng tab Kiểu chữ → rỗng (render theo kiểu chữ).</summary>
        public static string CoalesceAnimationOverrideForSave(string lineStorage, string tabStorage)
        {
            var line = (lineStorage ?? string.Empty).Trim();
            var tab = (tabStorage ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            if (string.Equals(line, tab, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return line;
        }

        private static string NormalizeDisplayLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text.Trim(), @"\s+", " ");
        }

        public static int FindFirstVoicedSceneIndex(IList<AiVideoGenInputItem> orderedScenes)
        {
            if (orderedScenes == null)
            {
                return 0;
            }

            for (var i = 0; i < orderedScenes.Count; i++)
            {
                var scene = orderedScenes[i];
                if (scene != null && !scene.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    return i;
                }
            }

            return 0;
        }

        public static List<WordTimestamp> FilterForBurnIn(
            IReadOnlyList<WordTimestamp> words,
            ShowcaseNarrationTimingManifest manifest,
            IList<AiVideoGenInputItem> orderedScenes,
            string ctaText,
            ShowcaseSubtitleDisplayPlan plan)
        {
            if (words == null || words.Count == 0)
            {
                return new List<WordTimestamp>();
            }

            if (plan == null || !PlanHasAnyOverride(plan))
            {
                return words.ToList();
            }

            if (manifest == null || orderedScenes == null || orderedScenes.Count == 0)
            {
                return words.ToList();
            }

            var hookWords = new List<WordTimestamp>();
            var bodyWords = new List<WordTimestamp>();
            ShowcaseKaraokeTimingHelper.SplitHookBodyWords(words, manifest, hookWords, bodyWords);

            var hookSource = (manifest.HookText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(hookSource))
            {
                hookSource = ResolveHookSpeechSourceFromScenes(orderedScenes, ctaText);
            }

            var hookDisplay = string.IsNullOrWhiteSpace(plan.Hook)
                ? hookSource
                : plan.Hook.Trim();

            var filteredHook = FilterSegment(hookWords, hookSource, hookDisplay);

            var bodySegments = BuildBodySegments(orderedScenes, ctaText, plan, FindFirstVoicedSceneIndex(orderedScenes));
            var filteredBody = FilterBodySegments(bodyWords, bodySegments);

            var merged = new List<WordTimestamp>(filteredHook.Count + filteredBody.Count);
            merged.AddRange(filteredHook);
            merged.AddRange(filteredBody);
            return merged;
        }

        private static bool PlanHasAnyOverride(ShowcaseSubtitleDisplayPlan plan)
        {
            if (plan == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(plan.Hook) || !string.IsNullOrWhiteSpace(plan.Cta))
            {
                return true;
            }

            if (HasAnyLineAnimationOverride(plan))
            {
                return true;
            }

            return plan.Scenes != null &&
                   plan.Scenes.Any(s => s != null && !string.IsNullOrWhiteSpace(s.DisplayVoiceover));
        }

        public static bool NeedsPerLineAssProcessing(ShowcaseSubtitleDisplayPlan plan)
            => PlanHasAnyOverride(plan);

        private static string ResolveHookSpeechSourceFromScenes(IList<AiVideoGenInputItem> orderedScenes, string ctaText)
        {
            var idx = FindFirstVoicedSceneIndex(orderedScenes);
            var scene = orderedScenes[idx];
            return (scene?.SceneVoiceover ?? string.Empty).Trim();
        }

        public static string ResolveHookSpeechSourceFromScenesPublic(IList<AiVideoGenInputItem> orderedScenes, string ctaText)
            => ResolveHookSpeechSourceFromScenes(orderedScenes, ctaText);

        public static List<WordTimestamp> FilterSegmentPublic(
            IReadOnlyList<WordTimestamp> segmentWords,
            string sourceText,
            string displayText)
            => FilterSegment(segmentWords, sourceText, displayText);

        private sealed class BodySegment
        {
            public string Source { get; set; }

            public string Display { get; set; }

            public string Animation { get; set; }

            public ShowcaseSubtitleLineRenderOverride LineStyle { get; set; }
        }

        public sealed class ShowcaseDisplayLineWordSlice
        {
            public List<WordTimestamp> Words { get; set; } = new List<WordTimestamp>();

            public string AnimationStorage { get; set; } = string.Empty;

            public ShowcaseSubtitleLineRenderOverride LineStyle { get; set; }
        }

        public static bool HasAnyLineAnimationOverride(ShowcaseSubtitleDisplayPlan plan)
        {
            if (plan == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(plan.HookAnimation) || !string.IsNullOrWhiteSpace(plan.CtaAnimation))
            {
                return true;
            }

            return plan.Scenes != null &&
                   plan.Scenes.Any(s => s != null && !string.IsNullOrWhiteSpace(s.DisplayAnimation));
        }

        public static List<ShowcaseDisplayLineWordSlice> SliceBodyWordsForLineEffects(
            IReadOnlyList<WordTimestamp> bodyWords,
            IList<AiVideoGenInputItem> orderedScenes,
            string ctaText,
            ShowcaseSubtitleDisplayPlan plan)
        {
            if (bodyWords == null || bodyWords.Count == 0 || orderedScenes == null || orderedScenes.Count == 0)
            {
                return new List<ShowcaseDisplayLineWordSlice>();
            }

            var segments = BuildBodySegments(orderedScenes, ctaText, plan, FindFirstVoicedSceneIndex(orderedScenes));
            return FilterBodySegmentsToSlices(bodyWords, segments);
        }

        private static List<BodySegment> BuildBodySegments(
            IList<AiVideoGenInputItem> orderedScenes,
            string ctaText,
            ShowcaseSubtitleDisplayPlan plan,
            int firstVoicedIndex)
        {
            var segments = new List<BodySegment>();
            var sceneCount = orderedScenes.Count;

            for (var i = firstVoicedIndex + 1; i < sceneCount; i++)
            {
                var scene = orderedScenes[i];
                if (scene == null || scene.ShowcaseSceneSilent || string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    continue;
                }

                var source = scene.SceneVoiceover.Trim();
                var displayOverride = GetPlanSceneDisplay(plan, i + 1);
                segments.Add(new BodySegment
                {
                    Source = source,
                    Display = string.IsNullOrWhiteSpace(displayOverride) ? source : displayOverride.Trim(),
                    Animation = GetPlanSceneAnimation(plan, i + 1),
                    LineStyle = BuildLineStyleOverrideFromScene(scene)
                });
            }

            var cta = (ctaText ?? string.Empty).Trim();
            if (cta.Length > 0
                && ShowcaseCtaDedupHelper.ShouldAppendCtaToBody(segments.Select(s => s.Source), cta))
            {
                var ctaDisplay = string.IsNullOrWhiteSpace(plan.Cta) ? cta : plan.Cta.Trim();
                segments.Add(new BodySegment
                {
                    Source = cta,
                    Display = ctaDisplay,
                    Animation = plan?.CtaAnimation ?? string.Empty
                });
            }

            return segments;
        }

        private static string GetPlanSceneDisplay(ShowcaseSubtitleDisplayPlan plan, int sceneOrder)
        {
            return plan?.Scenes?
                .FirstOrDefault(s => s != null && s.SceneOrder == sceneOrder)?
                .DisplayVoiceover ?? string.Empty;
        }

        private static string GetPlanSceneAnimation(ShowcaseSubtitleDisplayPlan plan, int sceneOrder)
        {
            return plan?.Scenes?
                .FirstOrDefault(s => s != null && s.SceneOrder == sceneOrder)?
                .DisplayAnimation ?? string.Empty;
        }

        private static ShowcaseSubtitleLineRenderOverride BuildLineStyleOverrideFromScene(AiVideoGenInputItem scene)
        {
            if (scene == null)
            {
                return null;
            }

            var o = new ShowcaseSubtitleLineRenderOverride
            {
                LookPreset = (scene.ShowcaseSubtitleDisplayLookPreset ?? string.Empty).Trim(),
                FontName = (scene.ShowcaseSubtitleDisplayFontName ?? string.Empty).Trim(),
                FontSize = scene.ShowcaseSubtitleDisplayFontSize,
                FontFace = (scene.ShowcaseSubtitleDisplayFontFace ?? string.Empty).Trim(),
                Position = (scene.ShowcaseSubtitleDisplayPosition ?? string.Empty).Trim(),
                PrimaryColourAss = (scene.ShowcaseSubtitleDisplayPrimaryColourAss ?? string.Empty).Trim(),
                DecorPreset = (scene.ShowcaseSubtitleDisplayDecorPreset ?? string.Empty).Trim(),
                LineBackgroundColourAss = (scene.ShowcaseSubtitleDisplayHighlightColourAss ?? string.Empty).Trim()
            };

            return o.HasAny() ? o : null;
        }

        private static List<ShowcaseDisplayLineWordSlice> FilterBodySegmentsToSlices(
            IReadOnlyList<WordTimestamp> bodyWords,
            IReadOnlyList<BodySegment> segments)
        {
            var slices = new List<ShowcaseDisplayLineWordSlice>();
            if (bodyWords == null || bodyWords.Count == 0 || segments == null || segments.Count == 0)
            {
                return slices;
            }

            var wordIndex = 0;
            foreach (var segment in segments)
            {
                var sourceTokens = SubtitleTimingHelper.SplitWords(segment.Source ?? string.Empty);
                var segmentWordCount = Math.Min(sourceTokens.Count, Math.Max(0, bodyWords.Count - wordIndex));
                if (segmentWordCount <= 0)
                {
                    continue;
                }

                var slice = new List<WordTimestamp>();
                for (var j = 0; j < segmentWordCount && wordIndex < bodyWords.Count; j++, wordIndex++)
                {
                    slice.Add(bodyWords[wordIndex]);
                }

                var filtered = FilterSegment(slice, segment.Source, segment.Display);
                if (filtered.Count == 0)
                {
                    continue;
                }

                slices.Add(new ShowcaseDisplayLineWordSlice
                {
                    Words = filtered,
                    AnimationStorage = segment.Animation ?? string.Empty,
                    LineStyle = segment.LineStyle
                });
            }

            return slices;
        }

        private static List<WordTimestamp> FilterBodySegments(
            IReadOnlyList<WordTimestamp> bodyWords,
            IReadOnlyList<BodySegment> segments)
        {
            if (bodyWords == null || bodyWords.Count == 0 || segments == null || segments.Count == 0)
            {
                return new List<WordTimestamp>();
            }

            var result = new List<WordTimestamp>();
            foreach (var slice in FilterBodySegmentsToSlices(bodyWords, segments))
            {
                result.AddRange(slice.Words);
            }

            return result;
        }

        private static List<WordTimestamp> FilterSegment(
            IReadOnlyList<WordTimestamp> segmentWords,
            string sourceText,
            string displayText)
        {
            if (segmentWords == null || segmentWords.Count == 0)
            {
                return new List<WordTimestamp>();
            }

            var sourceTokens = SubtitleTimingHelper.SplitWords(sourceText ?? string.Empty);
            var displayTokens = SubtitleTimingHelper.SplitWords(
                string.IsNullOrWhiteSpace(displayText) ? sourceText : displayText);

            if (displayTokens.Count == 0)
            {
                return new List<WordTimestamp>();
            }

            if (TokensSequencesEqual(sourceTokens, displayTokens))
            {
                return segmentWords.Take(Math.Min(segmentWords.Count, sourceTokens.Count)).ToList();
            }

            var result = new List<WordTimestamp>();
            var displayIdx = 0;
            var limit = Math.Min(segmentWords.Count, sourceTokens.Count);
            for (var i = 0; i < limit; i++)
            {
                if (displayIdx >= displayTokens.Count)
                {
                    break;
                }

                if (!TokenMatch(segmentWords[i].Text, sourceTokens[i]))
                {
                    continue;
                }

                if (TokenMatch(segmentWords[i].Text, displayTokens[displayIdx]))
                {
                    result.Add(segmentWords[i]);
                    displayIdx++;
                }
            }

            return result;
        }

        private static bool TokensSequencesEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (!TokenMatch(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TokenMatch(string a, string b)
        {
            return string.Equals(NormalizeToken(a), NormalizeToken(b), StringComparison.Ordinal);
        }

        private static string NormalizeToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return TokenStripRegex.Replace(raw.Trim(), string.Empty).ToLowerInvariant();
        }
    }
}
