using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Sinh file ASS karaoke với hiệu ứng pop từng từ.</summary>
    public static class AssSubtitleGenerator
    {
        private const string PopStyleName = "PopStyle";
        private const string HookStyleName = "HookStyle";
        private const string BodyStyleName = "BodyStyle";
        // ASS override tags use {…}; double braces so string.Format treats them as literals.
        private const string PopTagTemplate = @"{{\fscx{0}\fscy{0}\t({1},{2},\fscx100\fscy100)}}";

        /// <summary>Ghi file ASS karaoke pop-up (UTF-8).</summary>
        public static void GenerateAssFile(
            IReadOnlyList<WordTimestamp> words,
            string outputPath,
            AssSubtitleGeneratorOptions options = null)
        {
            WriteAssFile(outputPath, words, options);
        }

        /// <summary>Ghi file ASS từ các segment transcript (tương thích FFmpeg <c>subtitles</c> filter).</summary>
        public static void GenerateAssFileFromSegments(
            IReadOnlyList<TranscriptSegment> segments,
            string outputPath,
            AssSubtitleGeneratorOptions options = null)
        {
            WriteAssFileFromSegments(outputPath, segments, options);
        }

        public static void WriteAssFileFromSegments(
            string outputPath,
            IReadOnlyList<TranscriptSegment> segments,
            AssSubtitleGeneratorOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Đường dẫn ASS không hợp lệ.", nameof(outputPath));
            }

            if (segments == null || segments.Count == 0)
            {
                throw new ArgumentException("Danh sách TranscriptSegment trống.", nameof(segments));
            }

            var body = BuildAssContentFromSegments(segments, options);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, body, TextFileEncoding.Utf8NoBom);
        }

        public static string BuildAssContentFromSegments(
            IReadOnlyList<TranscriptSegment> segments,
            AssSubtitleGeneratorOptions options = null)
        {
            if (segments == null || segments.Count == 0)
            {
                throw new ArgumentException("Danh sách TranscriptSegment trống.", nameof(segments));
            }

            var opt = options ?? new AssSubtitleGeneratorOptions();
            var sb = new StringBuilder();
            AppendAssHeader(sb, opt);

            foreach (var segment in segments)
            {
                if (segment == null)
                {
                    continue;
                }

                var text = (segment.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var startMs = Math.Max(0d, segment.StartTimeMs);
                var endMs = segment.EndTimeMs > startMs ? segment.EndTimeMs : startMs + 500d;

                sb.AppendLine(
                    "Dialogue: 0," +
                    FormatAssTime(startMs) + "," +
                    FormatAssTime(endMs) +
                    "," + PopStyleName + ",,0,0,0,," + EscapeAssText(text));
            }

            return sb.ToString();
        }

        /// <summary>Chuyển segment thành <see cref="WordTimestamp"/> (một từ/block mỗi segment).</summary>
        public static List<WordTimestamp> ToWordTimestamps(IReadOnlyList<TranscriptSegment> segments)
        {
            var result = new List<WordTimestamp>();
            if (segments == null)
            {
                return result;
            }

            foreach (var segment in segments)
            {
                if (segment == null)
                {
                    continue;
                }

                var text = (segment.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var startMs = Math.Max(0d, segment.StartTimeMs);
                var endMs = segment.EndTimeMs > startMs ? segment.EndTimeMs : startMs + 500d;
                result.Add(new WordTimestamp
                {
                    Text = text,
                    StartTimeMs = startMs,
                    EndTimeMs = endMs
                });
            }

            return result;
        }

        public static void WriteAssFile(
            string outputPath,
            IReadOnlyList<WordTimestamp> wordTimestamps,
            AssSubtitleGeneratorOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Đường dẫn ASS không hợp lệ.", nameof(outputPath));
            }

            if (wordTimestamps == null || wordTimestamps.Count == 0)
            {
                throw new ArgumentException("Danh sách WordTimestamp trống.", nameof(wordTimestamps));
            }

            var opt = options ?? new AssSubtitleGeneratorOptions();
            var body = BuildAssContent(wordTimestamps, opt);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, body, TextFileEncoding.Utf8NoBom);
        }

        public static string BuildAssContent(
            IReadOnlyList<WordTimestamp> wordTimestamps,
            AssSubtitleGeneratorOptions options = null,
            int? wordsPerLineOverride = null)
        {
            var opt = options ?? new AssSubtitleGeneratorOptions();
            var sb = new StringBuilder();
            AppendAssHeader(sb, opt);

            AppendDialogueLines(sb, wordTimestamps, opt, PopStyleName);
            return sb.ToString();
        }

        /// <summary>ASS Showcase: hook + thân với style/hiệu ứng riêng.</summary>
        public static void WriteShowcaseAssFile(
            string outputPath,
            IReadOnlyList<WordTimestamp> words,
            ShowcaseNarrationTimingManifest manifest,
            ShowcaseSubtitleRenderPlan plan,
            ShowcaseSubtitleDisplayPlan displayPlan = null,
            IList<AiVideoGenInputItem> orderedScenes = null,
            string ctaText = null,
            ShowcaseVideoItem styleVideo = null,
            AppSettings appSettings = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Đường dẫn ASS không hợp lệ.", nameof(outputPath));
            }

            if (words == null || words.Count == 0 || plan == null || !plan.HasAnyEnabled)
            {
                throw new ArgumentException("Không có từ hoặc plan phụ đề Showcase trống.", nameof(words));
            }

            var body = BuildShowcaseAssContent(
                words,
                manifest,
                plan,
                displayPlan,
                orderedScenes,
                ctaText,
                styleVideo,
                appSettings);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, body, TextFileEncoding.Utf8NoBom);
        }

        private static string BuildShowcaseAssContent(
            IReadOnlyList<WordTimestamp> words,
            ShowcaseNarrationTimingManifest manifest,
            ShowcaseSubtitleRenderPlan plan,
            ShowcaseSubtitleDisplayPlan displayPlan = null,
            IList<AiVideoGenInputItem> orderedScenes = null,
            string ctaText = null,
            ShowcaseVideoItem styleVideo = null,
            AppSettings appSettings = null)
        {
            var settings = appSettings ?? new AppSettings();
            var style = styleVideo ?? new ShowcaseVideoItem();
            var useDisplayPlan = displayPlan != null
                                 && orderedScenes != null
                                 && orderedScenes.Count > 0
                                 && ShowcaseSubtitleDisplayHelper.NeedsPerLineAssProcessing(displayPlan);

            var hookWords = new List<WordTimestamp>();
            var bodyWords = new List<WordTimestamp>();
            ShowcaseKaraokeTimingHelper.SplitHookBodyWords(words, manifest, hookWords, bodyWords);

            var sb = new StringBuilder();
            AppendShowcaseAssHeader(sb, plan);

            if (plan.HookEnabled && hookWords.Count > 0)
            {
                List<WordTimestamp> hookBurnIn = hookWords;
                if (useDisplayPlan)
                {
                    var hookSource = (manifest?.HookText ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(hookSource))
                    {
                        hookSource = ShowcaseSubtitleDisplayHelper.ResolveHookSpeechSourceFromScenesPublic(
                            orderedScenes,
                            ctaText);
                    }

                    var hookDisplay = string.IsNullOrWhiteSpace(displayPlan.Hook)
                        ? hookSource
                        : displayPlan.Hook.Trim();
                    hookBurnIn = ShowcaseSubtitleDisplayHelper.FilterSegmentPublic(
                        hookWords,
                        hookSource,
                        hookDisplay);
                }

                if (hookBurnIn.Count > 0)
                {
                    var hookOpts = ShowcaseSubtitleStyleHelper.BuildHookOptionsWithLineOverride(
                        style,
                        settings,
                        useDisplayPlan ? displayPlan.HookAnimation : null);
                    AppendDialogueLines(sb, hookBurnIn, hookOpts ?? plan.HookOptions ?? new AssSubtitleGeneratorOptions(), HookStyleName);
                }
            }

            if (plan.BodyEnabled && bodyWords.Count > 0)
            {
                if (useDisplayPlan)
                {
                    var slices = ShowcaseSubtitleDisplayHelper.SliceBodyWordsForLineEffects(
                        bodyWords,
                        orderedScenes,
                        ctaText,
                        displayPlan);
                    foreach (var slice in slices)
                    {
                        if (slice?.Words == null || slice.Words.Count == 0)
                        {
                            continue;
                        }

                        var bodyOpts = ShowcaseSubtitleStyleHelper.BuildBodyOptionsWithLineOverride(
                            style,
                            settings,
                            slice.AnimationStorage,
                            slice.LineStyle);
                        AppendDialogueLines(
                            sb,
                            slice.Words,
                            bodyOpts ?? plan.BodyOptions ?? new AssSubtitleGeneratorOptions(),
                            BodyStyleName);
                    }
                }
                else
                {
                    AppendDialogueLines(sb, bodyWords, plan.BodyOptions ?? new AssSubtitleGeneratorOptions(), BodyStyleName);
                }
            }

            return sb.ToString();
        }

        private static void AppendShowcaseAssHeader(StringBuilder sb, ShowcaseSubtitleRenderPlan plan)
        {
            var sample = plan.BodyOptions ?? plan.HookOptions ?? new AssSubtitleGeneratorOptions();
            sb.AppendLine("[Script Info]");
            sb.AppendLine("Title: Showcase ASS");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine("PlayResX: " + sample.PlayResX.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("PlayResY: " + sample.PlayResY.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("WrapStyle: 0");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

            if (plan.HookEnabled && plan.HookOptions != null)
            {
                AppendAssStyleLine(sb, HookStyleName, plan.HookOptions);
            }

            if (plan.BodyEnabled && plan.BodyOptions != null)
            {
                AppendAssStyleLine(sb, BodyStyleName, plan.BodyOptions);
            }

            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        }

        private static void AppendAssStyleLine(StringBuilder sb, string styleName, AssSubtitleGeneratorOptions opt)
        {
            var primary = string.IsNullOrWhiteSpace(opt.PrimaryColourAss) ? "&H00FFFFFF" : opt.PrimaryColourAss.Trim();
            var secondary = string.IsNullOrWhiteSpace(opt.SecondaryColourAss) ? "&H000000FF" : opt.SecondaryColourAss.Trim();
            var outline = opt.OutlineWidth > 0 ? opt.OutlineWidth : 10;
            var shadow = opt.ShadowDepth >= 0 ? opt.ShadowDepth : 2;
            sb.AppendLine(
                "Style: " + styleName + "," + opt.FontName + "," + opt.FontSize.ToString(CultureInfo.InvariantCulture) +
                "," + primary + "," + secondary + ",&H00000000,&H96000000," +
                (opt.Bold ? "1" : "0") + "," + (opt.Italic ? "1" : "0") + ",0,0,100,100,0,0,1," +
                outline.ToString(CultureInfo.InvariantCulture) + "," +
                shadow.ToString(CultureInfo.InvariantCulture) + "," +
                opt.Alignment.ToString(CultureInfo.InvariantCulture) + ",60,60," +
                opt.MarginV.ToString(CultureInfo.InvariantCulture) + ",1");
        }

        private static void AppendDialogueLines(
            StringBuilder sb,
            IReadOnlyList<WordTimestamp> words,
            AssSubtitleGeneratorOptions opt,
            string styleName)
        {
            foreach (var chunk in GroupIntoLines(words, opt, null))
            {
                if (chunk.Count == 0)
                {
                    continue;
                }

                var lineStartMs = chunk[0].StartTimeMs;
                var lineEndMs = chunk[chunk.Count - 1].EndTimeMs;
                var text = BuildLineBackgroundPrefix(opt) + BuildChunkDialogueText(chunk, lineStartMs, opt);
                sb.AppendLine(
                    "Dialogue: 0," +
                    FormatAssTime(lineStartMs) + "," +
                    FormatAssTime(lineEndMs) +
                    "," + styleName + ",,0,0,0,," + text);
            }
        }

        private static string BuildLineBackgroundPrefix(AssSubtitleGeneratorOptions opt)
        {
            if (opt == null)
            {
                return string.Empty;
            }

            return ShowcaseSubtitleHighlightColourCatalog.BuildLineBackgroundAssTag(
                opt.LineBackgroundColourAss);
        }

        private static IReadOnlyList<List<WordTimestamp>> GroupIntoLines(
            IReadOnlyList<WordTimestamp> wordTimestamps,
            AssSubtitleGeneratorOptions opt,
            int? wordsPerLineOverride = null)
        {
            if (wordTimestamps == null || wordTimestamps.Count == 0)
            {
                return Array.Empty<List<WordTimestamp>>();
            }

            var maxWords = wordsPerLineOverride ?? Math.Max(4, Math.Min(12, opt.WordsPerLine));
            if (opt.RhythmicLineBreaks)
            {
                return GroupIntoRhythmicLines(wordTimestamps, maxWords, Math.Max(1, opt.MinWordsPerLine));
            }

            var lines = new List<List<WordTimestamp>>();
            for (var chunkStart = 0; chunkStart < wordTimestamps.Count; chunkStart += maxWords)
            {
                var chunkCount = Math.Min(maxWords, wordTimestamps.Count - chunkStart);
                var chunk = new List<WordTimestamp>(chunkCount);
                for (var i = 0; i < chunkCount; i++)
                {
                    chunk.Add(wordTimestamps[chunkStart + i]);
                }

                if (chunk.Count > 0)
                {
                    lines.Add(chunk);
                }
            }

            return lines;
        }

        private const double RhythmPauseGapMs = 350d;

        private static List<List<WordTimestamp>> GroupIntoRhythmicLines(
            IReadOnlyList<WordTimestamp> words,
            int maxWordsPerLine,
            int minWordsPerLine)
        {
            var lines = new List<List<WordTimestamp>>();
            var current = new List<WordTimestamp>();

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (current.Count > 0)
                {
                    var prev = current[current.Count - 1];
                    if (word.StartTimeMs - prev.EndTimeMs >= RhythmPauseGapMs)
                    {
                        lines.Add(current);
                        current = new List<WordTimestamp>();
                    }
                }

                current.Add(word);
                if (ShouldBreakLineAfterWord(GetWordText(word), current.Count, maxWordsPerLine, minWordsPerLine))
                {
                    lines.Add(current);
                    current = new List<WordTimestamp>();
                }
            }

            if (current.Count > 0)
            {
                lines.Add(current);
            }

            return lines;
        }

        private static bool ShouldBreakLineAfterWord(string wordText, int wordsInLine, int maxWordsPerLine, int minWordsPerLine = 2)
        {
            if (wordsInLine <= 0)
            {
                return false;
            }

            if (wordsInLine >= maxWordsPerLine)
            {
                return true;
            }

            var w = (wordText ?? string.Empty).TrimEnd();
            if (w.EndsWith("...", StringComparison.Ordinal) || w.EndsWith("…", StringComparison.Ordinal))
            {
                return wordsInLine >= minWordsPerLine;
            }

            if (w.Length == 0)
            {
                return false;
            }

            var last = w[w.Length - 1];
            if (last == '.' || last == '!' || last == '?' || last == ';' || last == ':')
            {
                return wordsInLine >= minWordsPerLine;
            }

            return last == ',' && wordsInLine >= Math.Max(minWordsPerLine, 3);
        }

        private static void AppendAssHeader(StringBuilder sb, AssSubtitleGeneratorOptions opt)
        {
            sb.AppendLine("[Script Info]");
            sb.AppendLine("Title: Karaoke ASS");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine("PlayResX: " + opt.PlayResX.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("PlayResY: " + opt.PlayResY.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("WrapStyle: 0");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            var primary = string.IsNullOrWhiteSpace(opt.PrimaryColourAss) ? "&H00FFFFFF" : opt.PrimaryColourAss.Trim();
            var secondary = string.IsNullOrWhiteSpace(opt.SecondaryColourAss) ? "&H000000FF" : opt.SecondaryColourAss.Trim();
            sb.AppendLine(
                "Style: " + PopStyleName + "," + opt.FontName + "," + opt.FontSize.ToString(CultureInfo.InvariantCulture) +
                "," + primary + "," + secondary + ",&H00000000,&H80000000," +
                (opt.Bold ? "1" : "0") + "," + (opt.Italic ? "1" : "0") + ",0,0,100,100,0,0,1,10,2," +
                opt.Alignment.ToString(CultureInfo.InvariantCulture) + ",60,60," +
                opt.MarginV.ToString(CultureInfo.InvariantCulture) + ",1");
            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        }

        private static string BuildChunkDialogueText(
            IReadOnlyList<WordTimestamp> chunk,
            double lineStartMs,
            AssSubtitleGeneratorOptions options)
        {
            var animation = options?.Animation ?? ReupKaraokeAnimationMode.Pop;
            var popScale = options?.PopScalePercent > 0 ? options.PopScalePercent : 150;
            var popDuration = options?.PopDurationMs > 0 ? options.PopDurationMs : 100;

            if (animation == ReupKaraokeAnimationMode.Plain)
            {
                return BuildPlainChunkText(chunk);
            }

            if (animation == ReupKaraokeAnimationMode.LineZoom)
            {
                return @"{\fscx52\fscy52\t(0,170,\fscx100\fscy100)}" + BuildPlainChunkText(chunk);
            }

            if (animation == ReupKaraokeAnimationMode.FlashColor)
            {
                return @"{\1c&H00FFFFFF&\t(0,90,\1c&H00FFFF00&)\t(90,180,\1c&H00FFFFFF&)}" + BuildPlainChunkText(chunk);
            }

            if (animation == ReupKaraokeAnimationMode.GlowPulse)
            {
                return @"{\bord4\blur1\t(0,140,\bord16)\t(140,260,\bord5)}" + BuildPlainChunkText(chunk);
            }

            if (animation == ReupKaraokeAnimationMode.Highlight)
            {
                return BuildHighlightChunkText(chunk);
            }

            if (animation == ReupKaraokeAnimationMode.FadeIn)
            {
                return BuildFadeInChunkText(chunk, lineStartMs);
            }

            if (animation == ReupKaraokeAnimationMode.Typewriter)
            {
                return BuildTypewriterChunkText(chunk, lineStartMs);
            }

            if (animation == ReupKaraokeAnimationMode.Bounce)
            {
                return BuildBounceChunkText(chunk, lineStartMs, popScale);
            }

            if (animation == ReupKaraokeAnimationMode.Shake)
            {
                return BuildShakeChunkText(chunk, lineStartMs, popScale, popDuration);
            }

            if (animation == ReupKaraokeAnimationMode.NeonSale)
            {
                return BuildPopChunkText(chunk, lineStartMs, popScale, popDuration);
            }

            return BuildPopChunkText(chunk, lineStartMs, popScale, popDuration);
        }

        private static string BuildPlainChunkText(IReadOnlyList<WordTimestamp> chunk)
        {
            var plainParts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                plainParts.Add(EscapeAssText(GetWordText(chunk[i])));
            }

            return string.Join(" ", plainParts);
        }

        private static string BuildHighlightChunkText(IReadOnlyList<WordTimestamp> chunk)
        {
            var highlightParts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var durCs = (int)Math.Max(1, Math.Round((word.EndTimeMs - word.StartTimeMs) / 10d));
                highlightParts.Add("{\\k" + durCs.ToString(CultureInfo.InvariantCulture) + "}" + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", highlightParts);
        }

        private static string BuildFadeInChunkText(IReadOnlyList<WordTimestamp> chunk, double lineStartMs)
        {
            var fadeParts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                var fadeEndMs = offsetMs + 180;
                var fadeTag = string.Format(
                    CultureInfo.InvariantCulture,
                    @"{{\alpha&HFF&\t({0},{1},\alpha&H00&)}}",
                    offsetMs,
                    fadeEndMs);
                fadeParts.Add(fadeTag + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", fadeParts);
        }

        private static string BuildTypewriterChunkText(IReadOnlyList<WordTimestamp> chunk, double lineStartMs)
        {
            var parts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                var revealMs = offsetMs + 1;
                var tag = string.Format(
                    CultureInfo.InvariantCulture,
                    @"{{\alpha&HFF&\t({0},{1},\alpha&H00&)}}",
                    offsetMs,
                    revealMs);
                parts.Add(tag + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", parts);
        }

        private static string BuildBounceChunkText(
            IReadOnlyList<WordTimestamp> chunk,
            double lineStartMs,
            int popScale)
        {
            var overshoot = Math.Max(108, Math.Min(130, popScale / 2));
            var parts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                var midMs = offsetMs + 70;
                var endMs = offsetMs + 130;
                var tag = string.Format(
                    CultureInfo.InvariantCulture,
                    @"{{\fscx{0}\fscy{0}\t({1},{2},\fscx{3}\fscy{3})\t({2},{4},\fscx100\fscy100)}}",
                    popScale,
                    offsetMs,
                    midMs,
                    overshoot,
                    endMs);
                parts.Add(tag + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", parts);
        }

        private static string BuildShakeChunkText(
            IReadOnlyList<WordTimestamp> chunk,
            double lineStartMs,
            int popScale,
            int popDuration)
        {
            var parts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                var popEndMs = offsetMs + popDuration;
                var shakeMid = offsetMs + 40;
                var shakeEnd = offsetMs + 80;
                var tag = string.Format(
                    CultureInfo.InvariantCulture,
                    @"{{\fscx{0}\fscy{0}\t({1},{2},\fscx100\fscy100\frz-4)\t({2},{3},\frz4)\t({3},{4},\frz0)}}",
                    popScale,
                    offsetMs,
                    popEndMs,
                    shakeMid,
                    shakeEnd);
                parts.Add(tag + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", parts);
        }

        private static string BuildPopChunkText(
            IReadOnlyList<WordTimestamp> chunk,
            double lineStartMs,
            int popScale,
            int popDuration)
        {
            var parts = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var word = chunk[i];
                var offsetMs = (int)Math.Round(Math.Max(0d, word.StartTimeMs - lineStartMs));
                var popEndMs = offsetMs + popDuration;
                var popTag = string.Format(
                    CultureInfo.InvariantCulture,
                    PopTagTemplate,
                    popScale.ToString(CultureInfo.InvariantCulture),
                    offsetMs,
                    popEndMs);
                parts.Add(popTag + EscapeAssText(GetWordText(word)));
            }

            return string.Join(" ", parts);
        }

        private static string FormatAssTime(double milliseconds)
        {
            var ms = Math.Max(0d, milliseconds);
            var hours = (int)(ms / 3600000d);
            ms -= hours * 3600000d;
            var minutes = (int)(ms / 60000d);
            ms -= minutes * 60000d;
            var seconds = (int)(ms / 1000d);
            ms -= seconds * 1000d;
            var centis = (int)Math.Round(ms / 10d);
            if (centis >= 100)
            {
                centis = 99;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:D2}:{2:D2}.{3:D2}",
                hours,
                minutes,
                seconds,
                centis);
        }

        private static string GetWordText(WordTimestamp word)
        {
            if (word == null)
            {
                return string.Empty;
            }

            return (word.Text ?? word.Word ?? string.Empty).Trim();
        }

        private static string EscapeAssText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }

    public sealed class AssSubtitleGeneratorOptions
    {
        public int WordsPerLine { get; set; } = 6;

        /// <summary>Tối thiểu từ trên một dòng trước khi ngắt theo dấu câu nhẹ (ví dụ phẩy).</summary>
        public int MinWordsPerLine { get; set; } = 2;

        /// <summary>Ngắt dòng theo dấu câu / khoảng dừng thay vì cố định N từ.</summary>
        public bool RhythmicLineBreaks { get; set; }

        public int PlayResX { get; set; } = 1080;

        public int PlayResY { get; set; } = 1920;

        public string FontName { get; set; } = "Segoe UI Bold";

        public int FontSize { get; set; } = 72;

        public int MarginV { get; set; } = 120;

        /// <summary>ASS alignment (2=dưới giữa, 5=giữa màn, 8=trên giữa).</summary>
        public int Alignment { get; set; } = 2;

        public bool Bold { get; set; } = true;

        public bool Italic { get; set; }

        public ReupKaraokeAnimationMode Animation { get; set; } = ReupKaraokeAnimationMode.Pop;

        /// <summary>Pop scale % (150 = phóng 1.5x). Hook thường 220–280.</summary>
        public int PopScalePercent { get; set; } = 150;

        /// <summary>Thời gian pop ms.</summary>
        public int PopDurationMs { get; set; } = 100;

        /// <summary>Độ dày viền ASS (bord).</summary>
        public int OutlineWidth { get; set; } = 10;

        /// <summary>Độ sâu bóng ASS (shadow).</summary>
        public int ShadowDepth { get; set; } = 2;

        /// <summary>ASS PrimaryColour tùy chỉnh (ví dụ &amp;H00FFFFFF).</summary>
        public string PrimaryColourAss { get; set; }

        /// <summary>ASS SecondaryColour — màu tô khi karaoke highlight.</summary>
        public string SecondaryColourAss { get; set; }

        /// <summary>Band nền phía sau cả dòng — ASS prefix tag khi render.</summary>
        public string LineBackgroundColourAss { get; set; }
    }
}
