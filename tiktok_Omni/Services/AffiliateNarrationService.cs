using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>TTS đơn giản, đa đoạn (Slideshow) hoặc Showcase hook/thân/CTA (ghép segment bằng FFmpeg).</summary>
    public sealed class AffiliateNarrationService
    {
        private enum ShowcaseNarrationSegmentKind
        {
            Hook,
            Body,
            Cta
        }

        private readonly VideoService _videoService;

        public AffiliateNarrationService(VideoService videoService = null)
        {
            _videoService = videoService ?? new VideoService();
        }

        public async Task GenerateNarrationAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            bool useMultiVoice,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            TtsEngineKind engine = TtsEngineKind.ElevenLabs)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Narration text is required.", nameof(text));
            }

            if (useMultiVoice)
            {
                await GenerateMultiVoiceAsync(text, settings, outputAudioFile, workDirectory, log, cancellationToken, engine)
                    .ConfigureAwait(false);
                return;
            }

            await GenerateVoiceSegmentAsync(text, emphaticHook: false, settings, outputAudioFile, log, cancellationToken, engine)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Showcase legacy: hook riêng + thân/CTA gom một đoạn đọc trôi.
        /// </summary>
        public async Task GenerateShowcaseNarrationAsync(
            string hookText,
            IList<string> bodyVoiceovers,
            string ctaText,
            AppSettings settings,
            string outputAudioFile,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            TtsEngineKind engine = TtsEngineKind.ElevenLabs)
        {
            var hasHook = !string.IsNullOrWhiteSpace(hookText);
            var bodyParts = new List<string>();
            foreach (var body in bodyVoiceovers ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(body))
                {
                    bodyParts.Add(body.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(ctaText)
                && ShowcaseCtaDedupHelper.ShouldAppendCtaToBody(bodyParts, ctaText))
            {
                bodyParts.Add(ctaText.Trim());
            }

            var mergedBody = ShowcaseVoiceoverFitHelper.MergePassage(bodyParts);
            if (!hasHook && string.IsNullOrWhiteSpace(mergedBody))
            {
                throw new InvalidOperationException("Showcase cần hook, voiceover cảnh hoặc CTA để tạo giọng đọc.");
            }

            if (hasHook && string.IsNullOrWhiteSpace(mergedBody))
            {
                var preparedHook = PrepareShowcaseSegmentText(hookText.Trim(), ShowcaseNarrationSegmentKind.Hook, settings, engine);
                await GenerateVoiceSegmentAsync(
                        preparedHook,
                        emphaticHook: true,
                        settings,
                        outputAudioFile,
                        log,
                        cancellationToken,
                        engine,
                        showcaseExpressiveBody: false)
                    .ConfigureAwait(false);
                return;
            }

            if (!hasHook && !string.IsNullOrWhiteSpace(mergedBody))
            {
                var preparedBody = PrepareShowcaseSegmentText(mergedBody, ShowcaseNarrationSegmentKind.Body, settings, engine);
                await GenerateVoiceSegmentAsync(
                        preparedBody,
                        emphaticHook: false,
                        settings,
                        outputAudioFile,
                        log,
                        cancellationToken,
                        engine,
                        showcaseExpressiveBody: true)
                    .ConfigureAwait(false);
                return;
            }

            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".");
            var partFiles = new List<string>();
            log?.Invoke("[TTS] Showcase: 2 đoạn — hook nhấn riêng, thân + CTA đọc liền một lần…");

            var hookPath = Path.Combine(workDirectory, "showcase_hook.mp3");
            var preparedHookOnly = PrepareShowcaseSegmentText(hookText.Trim(), ShowcaseNarrationSegmentKind.Hook, settings, engine);
            await GenerateVoiceSegmentAsync(
                    preparedHookOnly,
                    emphaticHook: true,
                    settings,
                    hookPath,
                    log,
                    cancellationToken,
                    engine,
                    showcaseExpressiveBody: false)
                .ConfigureAwait(false);
            partFiles.Add(hookPath);

            var bodyPath = Path.Combine(workDirectory, "showcase_body_passage.mp3");
            var preparedBodyPassage = PrepareShowcaseSegmentText(mergedBody, ShowcaseNarrationSegmentKind.Body, settings, engine);
            await GenerateVoiceSegmentAsync(
                    preparedBodyPassage,
                    emphaticHook: false,
                    settings,
                    bodyPath,
                    log,
                    cancellationToken,
                    engine,
                    showcaseExpressiveBody: true)
                .ConfigureAwait(false);
            partFiles.Add(bodyPath);

            var ffmpeg = ResolveFfmpegPath(settings);
            await ConcatAudioPartsAsync(partFiles, outputAudioFile, ffmpeg, log, cancellationToken).ConfigureAwait(false);
            CleanupTempFiles(partFiles);
        }

        /// <summary>Showcase: hook riêng (cảnh đầu) + thân/CTA gom một đoạn TTS trôi chảy.</summary>
        public async Task GenerateShowcaseTimelineNarrationAsync(
            string hookText,
            IList<AiVideoGenInputItem> orderedScenes,
            string ctaText,
            double hookBrollDurationSeconds,
            AppSettings settings,
            string outputAudioFile,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions showcaseTts)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                throw new InvalidOperationException("Showcase cần danh sách cảnh để dựng timeline thoại.");
            }

            showcaseTts = showcaseTts ?? new ShowcaseTtsRenderOptions();
            TtsAvailabilityHelper.ValidateEngine(settings, showcaseTts.HookEngine);
            TtsAvailabilityHelper.ValidateEngine(settings, showcaseTts.BodyEngine);
            if (showcaseTts.HookEngine == TtsEngineKind.EdgeTts)
            {
                var styleName = ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey);
                var edgeHook = ShowcaseEdgeTtsVoiceResolver.Resolve(showcaseTts, emphaticHook: true, showcaseExpressiveBody: false);
                log?.Invoke("[TTS] Showcase hook: Edge · «" + styleName + "» · " + edgeHook.VoiceShortName);
            }
            else
            {
                log?.Invoke("[TTS] Showcase hook: ElevenLabs · «"
                            + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey) + "».");
            }

            if (showcaseTts.BodyEngine == TtsEngineKind.EdgeTts)
            {
                var styleName = ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey);
                var edgeBody = ShowcaseEdgeTtsVoiceResolver.Resolve(showcaseTts, emphaticHook: false, showcaseExpressiveBody: true);
                log?.Invoke("[TTS] Showcase thân: Edge từng cảnh · «" + styleName + "» · " + edgeBody.VoiceShortName
                            + " · " + showcaseTts.VoiceLanguageId);
            }
            else
            {
                log?.Invoke("[TTS] Showcase thân: ElevenLabs · «"
                            + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.BodyStyleKey)
                            + "» · từng cảnh.");
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".");
            var sceneCount = orderedScenes.Count;
            var sceneDurations = new double[sceneCount];
            for (var di = 0; di < sceneCount; di++)
            {
                sceneDurations[di] = 6d;
                var clipPath = orderedScenes[di]?.ClipPath;
                if (!string.IsNullOrWhiteSpace(clipPath) && File.Exists(clipPath))
                {
                    var probed = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(ffprobe, clipPath, cancellationToken)
                        .ConfigureAwait(false);
                    if (probed > 0.1d)
                    {
                        sceneDurations[di] = probed;
                    }
                }
            }

            var voicedSceneIndices = new List<int>();
            for (var vi = 0; vi < sceneCount; vi++)
            {
                if (orderedScenes[vi] != null && !orderedScenes[vi].ShowcaseSceneSilent)
                {
                    voicedSceneIndices.Add(vi);
                }
            }

            if (voicedSceneIndices.Count == 0)
            {
                throw new InvalidOperationException("Showcase không có cảnh thoại để tạo giọng đọc.");
            }

            var firstVoicedSceneIndex = voicedSceneIndices[0];
            var preHookDuration = 0d;
            for (var i = 0; i < firstVoicedSceneIndex; i++)
            {
                preHookDuration += sceneDurations[i];
            }

            var postHookDuration = 0d;
            for (var i = firstVoicedSceneIndex + 1; i < sceneCount; i++)
            {
                postHookDuration += sceneDurations[i];
            }

            var totalClipDuration = 0d;
            for (var i = 0; i < sceneCount; i++)
            {
                totalClipDuration += sceneDurations[i];
            }

            var segmentFiles = new List<string>();
            var tempFiles = new List<string>();
            string hookPreparedText = null;
            string bodyPreparedText = null;
            string hookRawPath = null;
            string bodyRawPath = null;

            try
            {
                if (preHookDuration > 0.05d)
                {
                    var preHookSilence = Path.Combine(workDirectory, "pre_hook_silence.mp3");
                    await ShowcaseFfmpegAudioHelper.CreateSilenceMp3Async(
                            ffmpeg,
                            preHookDuration,
                            preHookSilence,
                            log,
                            cancellationToken)
                        .ConfigureAwait(false);
                    segmentFiles.Add(preHookSilence);
                    tempFiles.Add(preHookSilence);
                }

                var hookSource = (orderedScenes[firstVoicedSceneIndex]?.SceneVoiceover ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(hookSource) && !string.IsNullOrWhiteSpace(hookText))
                {
                    hookSource = hookText.Trim();
                }

                if (string.IsNullOrWhiteSpace(hookSource))
                {
                    throw new InvalidOperationException("Showcase cần thoại hook ở cảnh đầu tiên có lời.");
                }

                var hookNormalized = NormalizeShowcaseTextLocal(hookSource);
                var hookPrepared = PrepareShowcaseSegmentText(
                    hookNormalized,
                    ShowcaseNarrationSegmentKind.Hook,
                    settings,
                    showcaseTts.HookEngine,
                    showcaseTts,
                    log);
                hookPreparedText = hookPrepared;
                var hookRaw = Path.Combine(workDirectory, "hook_raw.mp3");
                hookRawPath = hookRaw;
                tempFiles.Add(hookRaw);
                await GenerateVoiceSegmentAsync(
                        hookPrepared,
                        emphaticHook: true,
                        settings,
                        hookRaw,
                        log,
                        cancellationToken,
                        showcaseTts,
                        showcaseTts.HookEngine,
                        showcaseExpressiveBody: false)
                    .ConfigureAwait(false);
                segmentFiles.Add(hookRaw);
                log?.Invoke("[TTS] Showcase hook: cảnh " + (firstVoicedSceneIndex + 1) +
                            " — TTS nguyên câu (ghép với thân, fit tốc độ lúc render ~" +
                            totalClipDuration.ToString("0.#", CultureInfo.InvariantCulture) + "s)");

                if (postHookDuration > 0.05d)
                {
                    var bodyParts = new List<string>();
                    for (var i = firstVoicedSceneIndex + 1; i < sceneCount; i++)
                    {
                        var scene = orderedScenes[i];
                        if (scene != null && !scene.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                        {
                            bodyParts.Add(scene.SceneVoiceover.Trim());
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(ctaText)
                        && ShowcaseCtaDedupHelper.ShouldAppendCtaToBody(bodyParts, ctaText))
                    {
                        bodyParts.Add(ctaText.Trim());
                    }

                    var bodyMerged = ShowcaseVoiceoverFitHelper.MergePassage(bodyParts);
                    bodyPreparedText = bodyMerged;
                    if (!string.IsNullOrWhiteSpace(bodyMerged))
                    {
                        var bodySceneFiles = new List<string>();
                        var bodyEngine = showcaseTts.BodyEngine;
                        for (var bi = 0; bi < bodyParts.Count; bi++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var partText = NormalizeShowcaseTextLocal(bodyParts[bi]);
                            if (string.IsNullOrWhiteSpace(partText))
                            {
                                continue;
                            }

                            var isCtaPart = bi == bodyParts.Count - 1
                                            && !string.IsNullOrWhiteSpace(ctaText)
                                            && string.Equals(partText, ctaText.Trim(), StringComparison.OrdinalIgnoreCase);
                            var segmentKind = isCtaPart
                                ? ShowcaseNarrationSegmentKind.Cta
                                : ShowcaseNarrationSegmentKind.Body;
                            var prepared = PrepareShowcaseSegmentText(
                                partText,
                                segmentKind,
                                settings,
                                bodyEngine,
                                showcaseTts,
                                log);
                            var scenePath = Path.Combine(
                                workDirectory,
                                "body_scene_" + (bi + 1).ToString("D2", CultureInfo.InvariantCulture) + ".mp3");
                            tempFiles.Add(scenePath);
                            log?.Invoke("[TTS] Showcase thân cảnh " + (bi + 1) + "/" + bodyParts.Count
                                        + " (" + (bodyEngine == TtsEngineKind.EdgeTts ? "Edge" : "ElevenLabs") + ")…");
                            await GenerateVoiceSegmentAsync(
                                    prepared,
                                    emphaticHook: false,
                                    settings,
                                    scenePath,
                                    log,
                                    cancellationToken,
                                    showcaseTts,
                                    bodyEngine,
                                    showcaseExpressiveBody: !isCtaPart,
                                    emphaticCta: isCtaPart)
                                .ConfigureAwait(false);
                            bodySceneFiles.Add(scenePath);
                        }

                        if (bodySceneFiles.Count == 0)
                        {
                            throw new InvalidOperationException("Showcase thân không có đoạn thoại hợp lệ.");
                        }

                        var bodyRaw = Path.Combine(workDirectory, "body_passage_raw.mp3");
                        bodyRawPath = bodyRaw;
                        tempFiles.Add(bodyRaw);
                        if (bodySceneFiles.Count == 1)
                        {
                            File.Copy(bodySceneFiles[0], bodyRaw, true);
                        }
                        else
                        {
                            log?.Invoke("[TTS] Showcase: ghép " + bodySceneFiles.Count + " cảnh thân (FFmpeg)…");
                            await ConcatAudioPartsAsync(bodySceneFiles, bodyRaw, ffmpeg, log, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        segmentFiles.Add(bodyRaw);
                    }
                    else
                    {
                        var postHookSilence = Path.Combine(workDirectory, "post_hook_silence.mp3");
                        await ShowcaseFfmpegAudioHelper.CreateSilenceMp3Async(
                                ffmpeg,
                                postHookDuration,
                                postHookSilence,
                                log,
                                cancellationToken)
                            .ConfigureAwait(false);
                        segmentFiles.Add(postHookSilence);
                        tempFiles.Add(postHookSilence);
                    }
                }

                if (segmentFiles.Count == 0)
                {
                    throw new InvalidOperationException("Showcase không có đoạn thoại để ghép.");
                }

                log?.Invoke("[TTS] Showcase: ghép hook + thân/CTA (không cắt slot) — " + segmentFiles.Count + " phần…");
                await ConcatAudioPartsAsync(segmentFiles, outputAudioFile, ffmpeg, log, cancellationToken).ConfigureAwait(false);

                var hookAudioSeconds = 0d;
                if (!string.IsNullOrEmpty(hookRawPath) && File.Exists(hookRawPath))
                {
                    hookAudioSeconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                            ffprobe,
                            hookRawPath,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                var bodyAudioSeconds = 0d;
                if (!string.IsNullOrEmpty(bodyRawPath) && File.Exists(bodyRawPath))
                {
                    bodyAudioSeconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                            ffprobe,
                            bodyRawPath,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                var timingManifest = new ShowcaseNarrationTimingManifest
                {
                    SpeechStartSeconds = preHookDuration,
                    HookAudioSeconds = hookAudioSeconds,
                    BodyAudioSeconds = bodyAudioSeconds,
                    HookText = hookPreparedText ?? string.Empty,
                    BodyText = bodyPreparedText ?? string.Empty,
                    FullText = ShowcaseVoiceoverFitHelper.MergePassage(
                        new[] { hookPreparedText, bodyPreparedText })
                };
                ShowcaseNarrationTimingManifest.Save(
                    Path.GetDirectoryName(outputAudioFile) ?? workDirectory,
                    timingManifest);
            }
            finally
            {
                CleanupTempFiles(tempFiles.Where(p => !string.Equals(p, outputAudioFile, StringComparison.OrdinalIgnoreCase)));
            }
        }

        /// <summary>TTS riêng hook (preview nghe thử) — lưu thẳng vào <paramref name="hookPreviewOutputPath"/>.</summary>
        public async Task GenerateShowcaseHookPreviewAsync(
            string hookText,
            IList<AiVideoGenInputItem> orderedScenes,
            AppSettings settings,
            string hookPreviewOutputPath,
            Action<string> log,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions showcaseTts)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                throw new InvalidOperationException("Showcase cần danh sách cảnh để tạo hook.");
            }

            if (string.IsNullOrWhiteSpace(hookPreviewOutputPath))
            {
                throw new ArgumentException("Hook preview path is required.", nameof(hookPreviewOutputPath));
            }

            showcaseTts = showcaseTts ?? new ShowcaseTtsRenderOptions();
            TtsAvailabilityHelper.ValidateEngine(settings, showcaseTts.HookEngine);

            var firstVoiced = FindFirstVoicedSceneIndex(orderedScenes);
            if (firstVoiced < 0)
            {
                throw new InvalidOperationException("Showcase không có cảnh thoại cho hook.");
            }

            var hookSource = (orderedScenes[firstVoiced]?.SceneVoiceover ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(hookSource) && !string.IsNullOrWhiteSpace(hookText))
            {
                hookSource = hookText.Trim();
            }

            if (string.IsNullOrWhiteSpace(hookSource))
            {
                throw new InvalidOperationException("Showcase cần thoại hook ở cảnh đầu tiên có lời.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(hookPreviewOutputPath) ?? ".");
            var hookPrepared = PrepareShowcaseSegmentText(
                NormalizeShowcaseTextLocal(hookSource),
                ShowcaseNarrationSegmentKind.Hook,
                settings,
                showcaseTts.HookEngine,
                showcaseTts,
                log);
            log?.Invoke("[TTS] Showcase hook preview: cảnh " + (firstVoiced + 1) + "…");
            await GenerateVoiceSegmentAsync(
                    hookPrepared,
                    emphaticHook: true,
                    settings,
                    hookPreviewOutputPath,
                    log,
                    cancellationToken,
                    showcaseTts,
                    showcaseTts.HookEngine,
                    showcaseExpressiveBody: false)
                .ConfigureAwait(false);
        }

        /// <summary>TTS riêng thân + CTA (preview nghe thử).</summary>
        public async Task GenerateShowcaseBodyPreviewAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string ctaText,
            AppSettings settings,
            string bodyPreviewOutputPath,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions showcaseTts)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                throw new InvalidOperationException("Showcase cần danh sách cảnh để tạo thân.");
            }

            if (string.IsNullOrWhiteSpace(bodyPreviewOutputPath))
            {
                throw new ArgumentException("Body preview path is required.", nameof(bodyPreviewOutputPath));
            }

            showcaseTts = showcaseTts ?? new ShowcaseTtsRenderOptions();
            TtsAvailabilityHelper.ValidateEngine(settings, showcaseTts.BodyEngine);

            var firstVoiced = FindFirstVoicedSceneIndex(orderedScenes);
            if (firstVoiced < 0)
            {
                throw new InvalidOperationException("Showcase không có cảnh thoại cho thân.");
            }

            var bodyParts = CollectBodyVoiceParts(orderedScenes, firstVoiced, ctaText);
            if (bodyParts.Count == 0)
            {
                throw new InvalidOperationException("Showcase thân không có đoạn thoại (sau hook).");
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(bodyPreviewOutputPath) ?? ".");
            var tempFiles = new List<string>();
            var bodySceneFiles = new List<string>();
            var bodyEngine = showcaseTts.BodyEngine;
            var previewComplete = false;

            try
            {
                for (var bi = 0; bi < bodyParts.Count; bi++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var partText = NormalizeShowcaseTextLocal(bodyParts[bi]);
                    if (string.IsNullOrWhiteSpace(partText))
                    {
                        continue;
                    }

                    var isCtaPart = bi == bodyParts.Count - 1
                                    && !string.IsNullOrWhiteSpace(ctaText)
                                    && string.Equals(partText, ctaText.Trim(), StringComparison.OrdinalIgnoreCase);
                    var segmentKind = isCtaPart
                        ? ShowcaseNarrationSegmentKind.Cta
                        : ShowcaseNarrationSegmentKind.Body;
                    var prepared = PrepareShowcaseSegmentText(
                        partText,
                        segmentKind,
                        settings,
                        bodyEngine,
                        showcaseTts,
                        log);
                    var scenePath = Path.Combine(
                        workDirectory,
                        "body_preview_" + (bi + 1).ToString("D2", CultureInfo.InvariantCulture) + ".mp3");
                    tempFiles.Add(scenePath);

                    if (TryReuseExistingAudioPart(scenePath))
                    {
                        log?.Invoke("[TTS] Showcase thân preview " + (bi + 1) + "/" + bodyParts.Count + " — dùng file đã tạo.");
                        bodySceneFiles.Add(scenePath);
                    }
                    else
                    {
                        log?.Invoke("[TTS] Showcase thân preview " + (bi + 1) + "/" + bodyParts.Count + "…");
                        await GenerateVoiceSegmentAsync(
                                prepared,
                                emphaticHook: false,
                                settings,
                                scenePath,
                                log,
                                cancellationToken,
                                showcaseTts,
                                bodyEngine,
                                showcaseExpressiveBody: !isCtaPart,
                                emphaticCta: isCtaPart)
                            .ConfigureAwait(false);
                        bodySceneFiles.Add(scenePath);
                    }

                    if (bi < bodyParts.Count - 1)
                    {
                        await Task.Delay(650, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (bodySceneFiles.Count == 0)
                {
                    throw new InvalidOperationException("Showcase thân không có đoạn thoại hợp lệ.");
                }

                if (bodySceneFiles.Count == 1)
                {
                    File.Copy(bodySceneFiles[0], bodyPreviewOutputPath, true);
                }
                else
                {
                    log?.Invoke("[TTS] Showcase: ghép " + bodySceneFiles.Count + " đoạn thân preview…");
                    await ConcatAudioPartsAsync(bodySceneFiles, bodyPreviewOutputPath, ffmpeg, log, cancellationToken)
                        .ConfigureAwait(false);
                }

                previewComplete = true;
            }
            finally
            {
                if (previewComplete)
                {
                    CleanupTempFiles(tempFiles.Where(p =>
                        !string.Equals(p, bodyPreviewOutputPath, StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        /// <summary>TTS quote Triết lý — body engine, đọc tự nhiên (không hook nhấn / ngắt nghỉ sâu).</summary>
        public async Task GeneratePhilosophyQuoteVoicePreviewAsync(
            string quoteText,
            AppSettings settings,
            string outputPath,
            Action<string> log,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions showcaseTts)
        {
            var quote = (quoteText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(quote))
            {
                throw new InvalidOperationException("Quote trống — không gọi TTS.");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Quote preview path is required.", nameof(outputPath));
            }

            showcaseTts = showcaseTts ?? new ShowcaseTtsRenderOptions();
            var engine = showcaseTts.BodyEngine;
            TtsAvailabilityHelper.ValidateEngine(settings, engine);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            var prepared = PhilosophyGeminiTtsContext.NormalizeQuoteContentForTts(quote);
            if (engine == TtsEngineKind.ElevenLabs)
            {
                prepared = ShowcaseElevenLabsTextHelper.PreparePhilosophyQuoteText(
                    prepared,
                    settings,
                    showcaseTts,
                    log);
            }

            log?.Invoke("[TTS] Quote triết lý · "
                         + (engine == TtsEngineKind.ElevenLabs ? "ElevenLabs" : "Edge TTS")
                         + " · «"
                         + TrimQuoteTtsLog(prepared)
                         + "»…");
            await GenerateVoiceSegmentAsync(
                    prepared,
                    emphaticHook: false,
                    settings,
                    outputPath,
                    log,
                    cancellationToken,
                    showcaseTts,
                    engine,
                    showcaseExpressiveBody: false,
                    emphaticCta: false,
                    philosophyQuote: true)
                .ConfigureAwait(false);
        }

        private static bool TryReuseExistingAudioPart(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return false;
                }

                return new FileInfo(path).Length > 256;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Ghép hook + thân preview (và im lặng theo clip) thành narration.mp3 timeline.</summary>
        public async Task AssembleShowcaseNarrationFromPreviewsAsync(
            string hookPreviewPath,
            string bodyPreviewPath,
            IList<AiVideoGenInputItem> orderedScenes,
            string hookText,
            string ctaText,
            AppSettings settings,
            string outputAudioFile,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                throw new InvalidOperationException("Showcase cần danh sách cảnh để ghép narration.");
            }

            if (string.IsNullOrWhiteSpace(hookPreviewPath) || !File.Exists(hookPreviewPath))
            {
                throw new InvalidOperationException("Chưa có hook_preview.mp3 — bấm «Tạo audio hook» trước.");
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".");

            var sceneCount = orderedScenes.Count;
            var sceneDurations = await ProbeShowcaseSceneDurationsAsync(
                    orderedScenes,
                    ffprobe,
                    cancellationToken)
                .ConfigureAwait(false);
            var firstVoiced = FindFirstVoicedSceneIndex(orderedScenes);
            if (firstVoiced < 0)
            {
                throw new InvalidOperationException("Showcase không có cảnh thoại.");
            }

            var preHookDuration = 0d;
            for (var i = 0; i < firstVoiced; i++)
            {
                preHookDuration += sceneDurations[i];
            }

            var postHookDuration = 0d;
            for (var i = firstVoiced + 1; i < sceneCount; i++)
            {
                postHookDuration += sceneDurations[i];
            }

            var segmentFiles = new List<string>();
            var tempFiles = new List<string>();

            try
            {
                if (preHookDuration > 0.05d)
                {
                    var preHookSilence = Path.Combine(workDirectory, "pre_hook_silence_assemble.mp3");
                    await ShowcaseFfmpegAudioHelper.CreateSilenceMp3Async(
                            ffmpeg,
                            preHookDuration,
                            preHookSilence,
                            log,
                            cancellationToken)
                        .ConfigureAwait(false);
                    segmentFiles.Add(preHookSilence);
                    tempFiles.Add(preHookSilence);
                }

                segmentFiles.Add(hookPreviewPath);

                var hasBodyPreview = !string.IsNullOrWhiteSpace(bodyPreviewPath) && File.Exists(bodyPreviewPath);
                if (hasBodyPreview)
                {
                    segmentFiles.Add(bodyPreviewPath);
                }
                else if (postHookDuration > 0.05d)
                {
                    var postHookSilence = Path.Combine(workDirectory, "post_hook_silence_assemble.mp3");
                    await ShowcaseFfmpegAudioHelper.CreateSilenceMp3Async(
                            ffmpeg,
                            postHookDuration,
                            postHookSilence,
                            log,
                            cancellationToken)
                        .ConfigureAwait(false);
                    segmentFiles.Add(postHookSilence);
                    tempFiles.Add(postHookSilence);
                }

                log?.Invoke("[TTS] Showcase: ghép preview hook + thân → narration.mp3…");
                await ConcatAudioPartsAsync(segmentFiles, outputAudioFile, ffmpeg, log, cancellationToken).ConfigureAwait(false);

                var hookAudioSeconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                        ffprobe,
                        hookPreviewPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                var bodyAudioSeconds = 0d;
                if (hasBodyPreview)
                {
                    bodyAudioSeconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                            ffprobe,
                            bodyPreviewPath,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                var hookSource = (orderedScenes[firstVoiced]?.SceneVoiceover ?? hookText ?? string.Empty).Trim();
                var bodyMerged = ShowcaseVoiceoverFitHelper.MergePassage(
                    CollectBodyVoiceParts(orderedScenes, firstVoiced, ctaText));
                var timingManifest = new ShowcaseNarrationTimingManifest
                {
                    SpeechStartSeconds = preHookDuration,
                    HookAudioSeconds = hookAudioSeconds,
                    BodyAudioSeconds = bodyAudioSeconds,
                    HookText = hookSource,
                    BodyText = bodyMerged,
                    FullText = ShowcaseVoiceoverFitHelper.MergePassage(new[] { hookSource, bodyMerged })
                };
                ShowcaseNarrationTimingManifest.Save(
                    Path.GetDirectoryName(outputAudioFile) ?? workDirectory,
                    timingManifest);
            }
            finally
            {
                CleanupTempFiles(tempFiles);
            }
        }

        private static int FindFirstVoicedSceneIndex(IList<AiVideoGenInputItem> orderedScenes)
        {
            if (orderedScenes == null)
            {
                return -1;
            }

            for (var i = 0; i < orderedScenes.Count; i++)
            {
                if (orderedScenes[i] != null && !orderedScenes[i].ShowcaseSceneSilent)
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<string> CollectBodyVoiceParts(
            IList<AiVideoGenInputItem> orderedScenes,
            int firstVoicedSceneIndex,
            string ctaText)
        {
            var bodyParts = new List<string>();
            for (var i = firstVoicedSceneIndex + 1; i < orderedScenes.Count; i++)
            {
                var scene = orderedScenes[i];
                if (scene != null && !scene.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    bodyParts.Add(scene.SceneVoiceover.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(ctaText)
                && ShowcaseCtaDedupHelper.ShouldAppendCtaToBody(bodyParts, ctaText))
            {
                bodyParts.Add(ctaText.Trim());
            }

            return bodyParts;
        }

        private static async Task<double[]> ProbeShowcaseSceneDurationsAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string ffprobe,
            CancellationToken cancellationToken)
        {
            var sceneCount = orderedScenes.Count;
            var sceneDurations = new double[sceneCount];
            for (var di = 0; di < sceneCount; di++)
            {
                sceneDurations[di] = 6d;
                var clipPath = orderedScenes[di]?.ClipPath;
                if (!string.IsNullOrWhiteSpace(clipPath) && File.Exists(clipPath))
                {
                    var probed = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(ffprobe, clipPath, cancellationToken)
                        .ConfigureAwait(false);
                    if (probed > 0.1d)
                    {
                        sceneDurations[di] = probed;
                    }
                }
            }

            return sceneDurations;
        }

        private static string NormalizeShowcaseTextLocal(string text)
        {
            return VietnameseTtsTextNormalizer.SanitizeForElevenLabsRequest(text);
        }

        private static string TrimQuoteTtsLog(string text)
        {
            var t = (text ?? string.Empty).Trim();
            return t.Length <= 120 ? t : t.Substring(0, 119) + "…";
        }

        private async Task<string> NormalizeShowcaseTextAsync(
            string text,
            AppSettings settings,
            TtsEngineKind engine,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            if (engine == TtsEngineKind.EdgeTts
                || engine == TtsEngineKind.ElevenLabs
                || VideoService.IsElevenLabsEndpoint(settings?.TtsEndpoint))
            {
                return await VietnameseTtsTextNormalizer.PrepareForElevenLabsAsync(
                    line,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }

            return VietnameseTtsTextNormalizer.SanitizeForElevenLabsRequest(line);
        }

        private static string PrepareShowcaseSegmentText(
            string text,
            ShowcaseNarrationSegmentKind kind,
            AppSettings settings,
            TtsEngineKind engine,
            ShowcaseTtsRenderOptions showcaseTts = null,
            Action<string> log = null)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            if (engine == TtsEngineKind.ElevenLabs)
            {
                var elevenKind = kind == ShowcaseNarrationSegmentKind.Hook
                    ? ShowcaseElevenLabsTextHelper.SegmentKind.Hook
                    : kind == ShowcaseNarrationSegmentKind.Cta
                        ? ShowcaseElevenLabsTextHelper.SegmentKind.Cta
                        : ShowcaseElevenLabsTextHelper.SegmentKind.Body;
                return ShowcaseElevenLabsTextHelper.PrepareSegmentText(
                    line,
                    elevenKind,
                    settings,
                    showcaseTts,
                    log);
            }

            if (engine == TtsEngineKind.EdgeTts)
            {
                return line;
            }

            return line;
        }

        private async Task GenerateVoiceSegmentAsync(
            string text,
            bool emphaticHook,
            AppSettings settings,
            string outputAudioFile,
            Action<string> log,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions showcaseTts,
            TtsEngineKind engine,
            bool showcaseExpressiveBody = false,
            bool emphaticCta = false,
            bool philosophyQuote = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Narration text is required.", nameof(text));
            }

            showcaseTts = showcaseTts ?? new ShowcaseTtsRenderOptions();
            TtsAvailabilityHelper.ValidateEngine(settings, engine);

            var mode = engine == TtsEngineKind.EdgeTts
                ? emphaticHook
                    ? "Edge nhấn · " + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey)
                    : emphaticCta
                        ? "Edge CTA · " + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey)
                        : showcaseExpressiveBody
                            ? "Edge thân êm · " + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey)
                            : philosophyQuote
                                ? "Edge quote · " + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.BodyStyleKey)
                                : "Edge · " + showcaseTts.Preset.Label
                : emphaticHook
                    ? "ElevenLabs nhấn · «" + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey) + "»"
                    : emphaticCta
                        ? "ElevenLabs CTA · «" + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.HookStyleKey) + "»"
                        : showcaseExpressiveBody
                            ? "ElevenLabs thân · «" + ShowcaseEdgeProsodyHelper.GetHookStyleDisplayName(showcaseTts.BodyStyleKey) + "»"
                            : philosophyQuote
                                ? "ElevenLabs quote · tự nhiên"
                                : "ElevenLabs kể chuyện";
            log?.Invoke("[TTS] Sinh giọng — " + mode + "…");
            var audioRef = await _videoService.GenerateAudioAsync(
                text,
                settings,
                cancellationToken,
                engine,
                showcaseTts,
                emphaticHook: emphaticHook,
                showcaseExpressiveBody: showcaseExpressiveBody,
                logAction: log,
                emphaticCta: emphaticCta,
                philosophyQuote: philosophyQuote).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(audioRef))
            {
                throw new InvalidOperationException("TTS returned empty audio.");
            }

            await PersistAudioResultAsync(audioRef, outputAudioFile, cancellationToken).ConfigureAwait(false);
        }

        private async Task GenerateVoiceSegmentAsync(
            string text,
            bool emphaticHook,
            AppSettings settings,
            string outputAudioFile,
            Action<string> log,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions showcaseTts,
            bool showcaseExpressiveBody = false,
            bool emphaticCta = false)
        {
            var engine = emphaticHook ? showcaseTts.HookEngine : showcaseTts.BodyEngine;
            await GenerateVoiceSegmentAsync(
                text,
                emphaticHook,
                settings,
                outputAudioFile,
                log,
                cancellationToken,
                showcaseTts,
                engine,
                showcaseExpressiveBody,
                emphaticCta).ConfigureAwait(false);
        }

        private async Task GenerateVoiceSegmentAsync(
            string text,
            bool emphaticHook,
            AppSettings settings,
            string outputAudioFile,
            Action<string> log,
            CancellationToken cancellationToken,
            TtsEngineKind engine,
            bool showcaseExpressiveBody = false)
        {
            var opts = new ShowcaseTtsRenderOptions { Engine = engine };

            await GenerateVoiceSegmentAsync(
                text,
                emphaticHook,
                settings,
                outputAudioFile,
                log,
                cancellationToken,
                opts,
                engine,
                showcaseExpressiveBody).ConfigureAwait(false);
        }

        private async Task GenerateMultiVoiceAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            TtsEngineKind engine)
        {
            var segments = SplitNarrationSegments(text);
            if (segments.Count <= 1)
            {
                await GenerateVoiceSegmentAsync(text, emphaticHook: false, settings, outputAudioFile, log, cancellationToken, engine)
                    .ConfigureAwait(false);
                return;
            }

            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".");
            var partFiles = new List<string>();
            log?.Invoke("[TTS] Đa giọng: " + segments.Count + " đoạn…");
            for (var i = 0; i < segments.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var partPath = Path.Combine(workDirectory, "voice_part_" + (i + 1).ToString("D2", CultureInfo.InvariantCulture) + ".mp3");
                await GenerateVoiceSegmentAsync(
                        segments[i],
                        emphaticHook: false,
                        settings,
                        partPath,
                        log,
                        cancellationToken,
                        engine)
                    .ConfigureAwait(false);
                partFiles.Add(partPath);
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            await ConcatAudioPartsAsync(partFiles, outputAudioFile, ffmpeg, log, cancellationToken).ConfigureAwait(false);
            CleanupTempFiles(partFiles);
        }

        private static List<string> SplitNarrationSegments(string text)
        {
            var parts = Regex.Split(text.Trim(), @"(?<=[\.\!\?…])\s+")
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => x.Length >= 8)
                .ToList();
            if (parts.Count <= 1)
            {
                return new List<string> { text.Trim() };
            }

            return parts.Take(12).ToList();
        }

        private static async Task PersistAudioResultAsync(string audioUrlOrLocalPath, string outputPath, CancellationToken cancellationToken)
        {
            var src = (audioUrlOrLocalPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(src))
            {
                throw new InvalidOperationException("TTS returned empty audio reference.");
            }

            var targetDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(src))
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Copy(src, outputPath);
                return;
            }

            await DownloadToFileAsync(src, outputPath, cancellationToken).ConfigureAwait(false);
        }

        private static async Task ConcatAudioPartsAsync(
            IList<string> partFiles,
            string outputFile,
            string ffmpegPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (partFiles == null || partFiles.Count == 0)
            {
                throw new InvalidOperationException("Không có file MP3 để ghép.");
            }

            if (partFiles.Count == 1)
            {
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }

                File.Copy(partFiles[0], outputFile);
                return;
            }

            var listFile = Path.Combine(Path.GetDirectoryName(outputFile) ?? ".", "narration_concat_" + Guid.NewGuid().ToString("N") + ".txt");
            var lines = partFiles.Select(p => "file '" + p.Replace("'", "'\\''") + "'");
            File.WriteAllLines(listFile, lines, TextFileEncoding.Utf8NoBom);
            var ffmpeg = string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath) ? "ffmpeg" : ffmpegPath;
            var args = "-y -f concat -safe 0 -i \"" + listFile + "\" -c copy \"" + outputFile + "\"";
            log?.Invoke("[TTS] FFmpeg: ghép " + partFiles.Count + " đoạn MP3 → narration hoàn chỉnh…");
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(listFile))
                {
                    File.Delete(listFile);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static string ResolveFfmpegPath(AppSettings settings)
        {
            if (FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
            {
                return toolkit.FfmpegExe;
            }

            var bundled = FfmpegToolkitService.GetBundledFfmpegPath();
            if (File.Exists(bundled))
            {
                return bundled;
            }

            var p = (settings?.FfmpegPath ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p) ? p : bundled;
        }

        private static void CleanupTempFiles(IEnumerable<string> paths)
        {
            foreach (var p in paths)
            {
                try
                {
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static async Task RunFfmpegAsync(string ffmpeg, string args, Action<string> log, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 120).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    log?.Invoke("[TTS] FFmpeg concat warning: " + err);
                    throw new InvalidOperationException("FFmpeg concat failed (exit " + process.ExitCode + ").");
                }
            }
        }

        private static async Task DownloadToFileAsync(string url, string path, CancellationToken cancellationToken)
        {
            using (var client = new System.Net.WebClient())
            {
                client.Headers.Add("User-Agent", "Mozilla/5.0");
                cancellationToken.Register(() => client.CancelAsync());
                await client.DownloadFileTaskAsync(new Uri(url), path).ConfigureAwait(false);
            }
        }
    }
}
