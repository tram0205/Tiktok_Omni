using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chỉnh nhẹ thoại TTS + rà/sửa prompt &amp; công cụ theo loại ảnh từng cảnh.</summary>
    internal static class ShowcaseScriptHubPolishHelper
    {
        public const int HookMaxWords = 12;
        public const int BodyMinWordsHint = 8;
        public const int BodyMaxWordsHint = 22;

        private static readonly Regex MultiSpaceRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex RunOnSplitRegex = new Regex(
            @"\s+(?:và|nhưng|mà|nên|còn|rồi)\s+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HardcodedFabricRegex = new Regex(
            @"\b(?:white|black|red|blue|green|pink|beige|navy|cream|ivory|gold|silver)\s+(?:silk|cotton|linen|fabric|satin|chiffon)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public sealed class VideoPolishResult
        {
            public int ScenesPolished { get; set; }

            public int VoiceAdjustedCount { get; set; }

            public int ToolAdjustedCount { get; set; }

            public int PromptAdjustedCount { get; set; }

            public IReadOnlyList<SceneReviewNote> Notes { get; set; } = Array.Empty<SceneReviewNote>();
        }

        public sealed class SceneReviewNote
        {
            public int SceneIndex { get; set; }

            public string SceneLabel { get; set; } = string.Empty;

            public bool IsHardIssue { get; set; }

            public string Message { get; set; } = string.Empty;
        }

        public static VideoPolishResult PolishVideo(ShowcaseVideoItem video, Action<string> log = null)
        {
            var result = new VideoPolishResult();
            if (video?.Scenes == null || video.Scenes.Count == 0)
            {
                return result;
            }

            var notes = new List<SceneReviewNote>();
            var scenes = video.Scenes.Where(s => s != null).ToList();
            var sceneCount = scenes.Count;

            for (var i = 0; i < sceneCount; i++)
            {
                var scene = scenes[i];
                var isHook = i == 0;
                var isClosing = i == sceneCount - 1;
                var sceneLabel = "Cảnh " + (i + 1).ToString(CultureInfo.InvariantCulture);
                var voiceChanged = false;
                var toolChanged = false;
                var promptChanged = false;

                var slotSeconds = ShowcaseSceneDurationHelper.ResolveZoomClipDuration(scene);
                if (isHook)
                {
                    slotSeconds = Math.Min(slotSeconds, 5.5d);
                }

                if (!scene.ShowcaseSceneSilent)
                {
                    var voice = ResolveSceneVoiceText(video, scene, isHook, isClosing);
                    var polished = PolishVoiceText(voice, isHook, isClosing, slotSeconds, out voiceChanged);
                    if (voiceChanged)
                    {
                        ApplySceneVoiceText(video, scene, isHook, isClosing, polished);
                        result.VoiceAdjustedCount++;
                    }
                }

                toolChanged = PolishSceneTool(scene, video.ShowcaseClipModeId);
                if (toolChanged)
                {
                    result.ToolAdjustedCount++;
                }

                promptChanged = PolishScenePrompt(scene, video.ShowcaseClipModeId, i);
                if (promptChanged)
                {
                    result.PromptAdjustedCount++;
                }

                if (voiceChanged || toolChanged || promptChanged)
                {
                    result.ScenesPolished++;
                }

                notes.AddRange(CollectSceneReviewNotes(
                    scene,
                    video.ShowcaseClipModeId,
                    i,
                    sceneLabel,
                    isHook,
                    isClosing,
                    ResolveSceneVoiceText(video, scene, isHook, isClosing)));
            }

            result.Notes = notes;
            if (result.ScenesPolished > 0)
            {
                log?.Invoke("[Showcase] Đã chỉnh nhẹ thoại/prompt cho "
                            + result.ScenesPolished.ToString(CultureInfo.InvariantCulture) + " cảnh.");
            }

            var warnCount = notes.Count(n => !n.IsHardIssue);
            var hardCount = notes.Count(n => n.IsHardIssue);
            if (hardCount > 0)
            {
                log?.Invoke("[Showcase] Còn " + hardCount.ToString(CultureInfo.InvariantCulture)
                            + " cảnh cần rà công cụ/prompt trong hub «Kịch bản · Prompt».");
            }
            else if (warnCount > 0)
            {
                log?.Invoke("[Showcase] Gợi ý rà thêm " + warnCount.ToString(CultureInfo.InvariantCulture)
                            + " cảnh (độ dài thoại / prompt) trong hub.");
            }

            return result;
        }

        public static IReadOnlyList<SceneReviewNote> ReviewScene(
            AiVideoGenInputItem scene,
            ShowcaseVideoItem video,
            int sceneIndex,
            int sceneCount)
        {
            if (scene == null || video == null)
            {
                return Array.Empty<SceneReviewNote>();
            }

            var isHook = sceneIndex == 0;
            var isClosing = sceneCount > 0 && sceneIndex == sceneCount - 1;
            var sceneLabel = "Cảnh " + (sceneIndex + 1).ToString(CultureInfo.InvariantCulture);
            var voice = ResolveSceneVoiceText(video, scene, isHook, isClosing);
            return CollectSceneReviewNotes(
                scene,
                video.ShowcaseClipModeId,
                sceneIndex,
                sceneLabel,
                isHook,
                isClosing,
                voice);
        }

        private static string ResolveSceneVoiceText(
            ShowcaseVideoItem video,
            AiVideoGenInputItem scene,
            bool isHook,
            bool isClosing)
        {
            if (isHook && isClosing)
            {
                var hook = (video.ShowcaseHookText ?? string.Empty).Trim();
                if (hook.Length > 0)
                {
                    return hook;
                }

                var cta = (video.ShowcaseCtaText ?? string.Empty).Trim();
                if (cta.Length > 0)
                {
                    return cta;
                }
            }

            if (isHook)
            {
                var hookOnly = (video.ShowcaseHookText ?? string.Empty).Trim();
                if (hookOnly.Length > 0)
                {
                    return hookOnly;
                }
            }

            if (isClosing)
            {
                var ctaOnly = (video.ShowcaseCtaText ?? string.Empty).Trim();
                if (ctaOnly.Length > 0)
                {
                    return ctaOnly;
                }
            }

            return (scene.SceneVoiceover ?? string.Empty).Trim();
        }

        private static void ApplySceneVoiceText(
            ShowcaseVideoItem video,
            AiVideoGenInputItem scene,
            bool isHook,
            bool isClosing,
            string voice)
        {
            scene.SceneVoiceover = voice ?? string.Empty;
            if (isHook)
            {
                video.ShowcaseHookText = voice ?? string.Empty;
            }

            if (isClosing)
            {
                video.ShowcaseCtaText = voice ?? string.Empty;
            }
        }

        public static string PolishVoiceText(
            string text,
            bool isHook,
            bool isClosing,
            double slotSeconds,
            out bool changed)
        {
            changed = false;
            var line = CollapseWhitespace(text);
            if (line.Length == 0)
            {
                return line;
            }

            var normalized = NormalizeTtsPunctuation(line, out var punctChanged);
            if (punctChanged)
            {
                line = normalized;
                changed = true;
            }

            string trimmed;
            if (isHook)
            {
                trimmed = ShowcaseVoiceoverFitHelper.TrimToSlotDuration(line, slotSeconds, out var hookTrimmed);
                if (hookTrimmed)
                {
                    changed = true;
                }
            }
            else
            {
                trimmed = ShowcaseVoiceoverFitHelper.TrimPassageToDuration(line, slotSeconds, out var bodyTrimmed);
                if (bodyTrimmed)
                {
                    changed = true;
                }
            }

            if (!string.Equals(trimmed, line, StringComparison.Ordinal))
            {
                line = trimmed;
                changed = true;
            }

            if (isClosing && ShowcaseVoiceoverFitHelper.CountWords(line) > 18)
            {
                var ctaTrim = ShowcaseVoiceoverFitHelper.TrimToSlotDuration(line, slotSeconds, out var ctaChanged);
                if (ctaChanged || !string.Equals(ctaTrim, line, StringComparison.Ordinal))
                {
                    line = ctaTrim;
                    changed = true;
                }
            }

            return line.Trim();
        }

        private static string NormalizeTtsPunctuation(string text, out bool changed)
        {
            changed = false;
            var line = text.Trim();
            if (line.Length == 0)
            {
                return line;
            }

            line = Regex.Replace(line, @"\s+,", ",", RegexOptions.CultureInvariant);
            line = Regex.Replace(line, @",\s*", ", ", RegexOptions.CultureInvariant);
            line = Regex.Replace(line, @"\s+\.", ".", RegexOptions.CultureInvariant);
            line = MultiSpaceRegex.Replace(line, " ").Trim();

            if (!HasSentencePunctuation(line) && ShowcaseVoiceoverFitHelper.CountWords(line) >= 14)
            {
                var split = TrySplitRunOnSentence(line);
                if (!string.Equals(split, line, StringComparison.Ordinal))
                {
                    line = split;
                    changed = true;
                }
            }

            if (!HasComma(line) && ShowcaseVoiceoverFitHelper.CountWords(line) >= 8)
            {
                var withComma = InsertLightCommas(line);
                if (!string.Equals(withComma, line, StringComparison.Ordinal))
                {
                    line = withComma;
                    changed = true;
                }
            }

            if (!EndsWithSentencePunctuation(line))
            {
                line += LooksLikeQuestion(line) ? "?" : ".";
                changed = true;
            }

            return line.Trim();
        }

        private static string TrySplitRunOnSentence(string text)
        {
            var match = RunOnSplitRegex.Match(text);
            if (!match.Success || match.Index < 12)
            {
                return text;
            }

            var left = text.Substring(0, match.Index).Trim();
            var right = text.Substring(match.Index + match.Length).Trim();
            if (left.Length < 8 || right.Length < 6)
            {
                return text;
            }

            if (!EndsWithSentencePunctuation(left))
            {
                left += ".";
            }

            if (char.IsLower(right[0]))
            {
                right = char.ToUpper(right[0], CultureInfo.CurrentCulture) + right.Substring(1);
            }

            return left + " " + right;
        }

        private static string InsertLightCommas(string text)
        {
            var result = text;
            result = Regex.Replace(
                result,
                @"(\S)\s+(thì)\s+",
                "$1, $2 ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"(\S)\s+(chất vải|chất liệu|form này|form áo|lụa|màu này)\b",
                "$1, $2",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return MultiSpaceRegex.Replace(result, " ").Trim();
        }

        private static bool PolishSceneTool(AiVideoGenInputItem scene, string clipModeId)
        {
            var before = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
            var corrected = ShowcaseClipToolHelper.ApplyMandatoryToolRules(
                clipModeId,
                scene.ShowcaseImageKind,
                before);
            if (string.Equals(before, corrected, StringComparison.Ordinal))
            {
                return false;
            }

            scene.ShowcaseClipTool = corrected;
            return true;
        }

        private static bool PolishScenePrompt(AiVideoGenInputItem scene, string clipModeId, int sceneIndex)
        {
            if (scene == null)
            {
                return false;
            }

            if (string.Equals(
                    ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool),
                    ShowcaseClipToolHelper.ToolReal,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
            if (string.IsNullOrEmpty(tool))
            {
                tool = ShowcaseClipToolHelper.ResolveDefaultTool(clipModeId, scene.ShowcaseImageKind);
                scene.ShowcaseClipTool = tool;
            }

            var changed = false;
            switch (tool)
            {
                case ShowcaseClipToolHelper.ToolKling:
                {
                    var before = scene.KlingPrompt ?? string.Empty;
                    var after = ShowcaseKlingPromptSanitizer.Sanitize(before, sceneIndex);
                    if (!string.Equals(before, after, StringComparison.Ordinal))
                    {
                        scene.KlingPrompt = after;
                        changed = true;
                    }

                    break;
                }

                case ShowcaseClipToolHelper.ToolZoom:
                {
                    var before = scene.ZoomHint ?? string.Empty;
                    var after = PolishZoomPrompt(scene, before);
                    if (!string.Equals(before, after, StringComparison.Ordinal))
                    {
                        scene.ZoomHint = after;
                        changed = true;
                    }

                    break;
                }

                default:
                {
                    var before = scene.VeoPrompt ?? string.Empty;
                    var after = PolishVeoPromptForImageKind(scene, before, sceneIndex);
                    if (!string.Equals(before, after, StringComparison.Ordinal))
                    {
                        scene.VeoPrompt = after;
                        changed = true;
                    }

                    break;
                }
            }

            return changed;
        }

        private static string PolishVeoPromptForImageKind(AiVideoGenInputItem scene, string prompt, int sceneIndex)
        {
            var kind = ShowcaseClipToolHelper.NormalizeImageKind(scene.ShowcaseImageKind, prompt);
            var text = (prompt ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return text;
            }

            if (string.Equals(kind, ShowcaseClipToolHelper.KindFlatlay, StringComparison.Ordinal)
                && Regex.IsMatch(text, @"\bON-MODEL\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                text = Regex.Replace(text, @"^\s*ON-MODEL fashion lookbook\s*—\s*", string.Empty, RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\bON-MODEL\b", string.Empty, RegexOptions.IgnoreCase);
                text = MultiSpaceRegex.Replace(text, " ").Trim(' ', '—', '-', '.', ',');
            }

            return ShowcaseVeoPromptSanitizer.Sanitize(text, sceneIndex);
        }

        private static string PolishZoomPrompt(AiVideoGenInputItem scene, string prompt)
        {
            var text = ShowcasePromptTextHelper.StripDurationClauses(prompt ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return text;
            }

            var kind = ShowcaseClipToolHelper.NormalizeImageKind(scene.ShowcaseImageKind, scene.VeoPrompt);
            if (string.Equals(kind, ShowcaseClipToolHelper.KindFlatlay, StringComparison.Ordinal)
                && Regex.IsMatch(text, @"\bON-MODEL\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                text = Regex.Replace(text, @"^\s*ON-MODEL fashion lookbook\s*—\s*", string.Empty, RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\bON-MODEL\b", string.Empty, RegexOptions.IgnoreCase);
                text = MultiSpaceRegex.Replace(text, " ").Trim(' ', '—', '-', '.', ',');
            }

            return text;
        }

        private static List<SceneReviewNote> CollectSceneReviewNotes(
            AiVideoGenInputItem scene,
            string clipModeId,
            int sceneIndex,
            string sceneLabel,
            bool isHook,
            bool isClosing,
            string voice)
        {
            var notes = new List<SceneReviewNote>();
            if (scene == null)
            {
                return notes;
            }

            if (ShowcaseScriptSceneValidationHelper.HasHardToolImageKindConflict(scene, clipModeId))
            {
                notes.Add(new SceneReviewNote
                {
                    SceneIndex = sceneIndex,
                    SceneLabel = sceneLabel,
                    IsHardIssue = true,
                    Message = ShowcaseScriptSceneValidationHelper.DescribeToolIssue(scene, clipModeId)
                });
            }

            if (!scene.ShowcaseSceneSilent)
            {
                notes.AddRange(CollectVoiceReviewNotes(voice, isHook, isClosing, sceneIndex, sceneLabel));
            }

            notes.AddRange(CollectPromptReviewNotes(scene, clipModeId, sceneIndex, sceneLabel));
            return notes;
        }

        private static IEnumerable<SceneReviewNote> CollectVoiceReviewNotes(
            string voice,
            bool isHook,
            bool isClosing,
            int sceneIndex,
            string sceneLabel)
        {
            var line = (voice ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                yield break;
            }

            var words = ShowcaseVoiceoverFitHelper.CountWords(line);
            if (isHook && words > HookMaxWords)
            {
                yield return Note(sceneIndex, sceneLabel, false,
                    "Hook dài " + words + " từ — nên ≤" + HookMaxWords + " từ.");
            }

            if (!isHook && !isClosing && words > BodyMaxWordsHint)
            {
                yield return Note(sceneIndex, sceneLabel, false,
                    "Thoại " + words + " từ — nên ~12–22 từ/cảnh.");
            }

            if (isHook && !HasComma(line))
            {
                yield return Note(sceneIndex, sceneLabel, false,
                    "Hook nên có phẩy/chấm để TTS thở tự nhiên.");
            }

            if (!HasSentencePunctuation(line) && words >= 10)
            {
                yield return Note(sceneIndex, sceneLabel, false,
                    "Thoại dài chưa có dấu chấm — nên chia câu cho Edge TTS.");
            }
        }

        private static IEnumerable<SceneReviewNote> CollectPromptReviewNotes(
            AiVideoGenInputItem scene,
            string clipModeId,
            int sceneIndex,
            string sceneLabel)
        {
            if (string.Equals(
                    ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool),
                    ShowcaseClipToolHelper.ToolReal,
                    StringComparison.Ordinal))
            {
                yield break;
            }

            var kind = ShowcaseClipToolHelper.NormalizeImageKind(scene.ShowcaseImageKind, scene.VeoPrompt);
            var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
            var prompt = (ShowcaseClipToolHelper.ResolveScenePrompt(scene) ?? string.Empty).Trim();

            if (prompt.Length == 0 && !scene.ShowcaseSceneSilent)
            {
                yield return Note(sceneIndex, sceneLabel, false, "Thiếu prompt clip.");
                yield break;
            }

            if (string.Equals(kind, ShowcaseClipToolHelper.KindFlatlay, StringComparison.Ordinal))
            {
                if (string.Equals(tool, ShowcaseClipToolHelper.ToolKling, StringComparison.Ordinal))
                {
                    yield return Note(sceneIndex, sceneLabel, true,
                        "Ảnh flatlay không dùng Kling — chọn Veo hoặc Zoom.");
                }

                if (string.Equals(tool, ShowcaseClipToolHelper.ToolVeo, StringComparison.Ordinal))
                {
                    if (prompt.IndexOf("stay exactly as shown", StringComparison.OrdinalIgnoreCase) < 0
                        && prompt.IndexOf("as shown", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        yield return Note(sceneIndex, sceneLabel, false,
                            "Veo flatlay nên có «as shown» / giữ mẫu đúng ảnh.");
                    }

                    if (prompt.IndexOf("FLATLAY", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        yield return Note(sceneIndex, sceneLabel, false,
                            "Prompt Veo flatlay nên bắt đầu FLATLAY product shot.");
                    }
                }

                if (Regex.IsMatch(prompt, @"\bON-MODEL\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    yield return Note(sceneIndex, sceneLabel, false,
                        "Ảnh flatlay nhưng prompt kiểu ON-MODEL — rà lại theo ảnh.");
                }
            }
            else
            {
                if (string.Equals(tool, ShowcaseClipToolHelper.ToolVeo, StringComparison.Ordinal))
                {
                    yield return Note(sceneIndex, sceneLabel, true,
                        "Ảnh on-model không dùng Veo — chọn Kling hoặc Zoom.");
                }

                if (string.Equals(tool, ShowcaseClipToolHelper.ToolKling, StringComparison.Ordinal)
                    && prompt.IndexOf("FLATLAY", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return Note(sceneIndex, sceneLabel, false,
                        "Kling on-model không nên copy prefix FLATLAY.");
                }
            }

            if (HardcodedFabricRegex.IsMatch(prompt)
                && prompt.IndexOf("as shown", StringComparison.OrdinalIgnoreCase) < 0)
            {
                yield return Note(sceneIndex, sceneLabel, false,
                    "Prompt gán cứng màu/chất — nên dùng «as shown» nếu ảnh không khớp.");
            }
        }

        private static SceneReviewNote Note(int sceneIndex, string sceneLabel, bool hard, string message) =>
            new SceneReviewNote
            {
                SceneIndex = sceneIndex,
                SceneLabel = sceneLabel,
                IsHardIssue = hard,
                Message = message
            };

        private static string CollapseWhitespace(string text) =>
            MultiSpaceRegex.Replace((text ?? string.Empty).Trim(), " ");

        private static bool HasSentencePunctuation(string text) =>
            text.IndexOf('.') >= 0
            || text.IndexOf('?') >= 0
            || text.IndexOf('!') >= 0
            || text.IndexOf('…') >= 0;

        private static bool EndsWithSentencePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var last = text[text.Length - 1];
            return last == '.' || last == '?' || last == '!' || last == '…';
        }

        private static bool HasComma(string text) => (text ?? string.Empty).IndexOf(',') >= 0;

        private static bool LooksLikeQuestion(string text)
        {
            var line = (text ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                return false;
            }

            if (line.IndexOf('?') >= 0)
            {
                return true;
            }

            return Regex.IsMatch(
                line,
                @"\b(?:không|nào|gì|sao|chưa|bao nhiêu|mấy|ai|đâu)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
