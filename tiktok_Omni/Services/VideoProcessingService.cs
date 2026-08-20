using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public class VideoProcessingService
    {
        private readonly VideoService _videoService = new VideoService();
        private readonly GeminiService _geminiService = new GeminiService();
        private readonly MascotWorker _mascotWorker;
        private readonly LipSyncService _lipSyncService = new LipSyncService();
        private readonly EmotionEngine _emotionEngine = new EmotionEngine();
        private readonly AffiliateNarrationService _affiliateNarrationService = new AffiliateNarrationService();
        private readonly AffiliateVideoPostProcessingService _affiliatePostProcessing = new AffiliateVideoPostProcessingService();
        private readonly ElevenLabsTtsService _elevenLabsTtsService;
        private readonly AssSubtitleGeneratorService _assSubtitleGenerator = new AssSubtitleGeneratorService();
        private readonly Random _random = new Random();

        private AsyncTasksRebootStore _asyncTasksRebootStore;

        public VideoProcessingService(AsyncTasksRebootStore rebootStore = null)
        {
            _asyncTasksRebootStore = rebootStore;
            _mascotWorker = new MascotWorker(_videoService, rebootStore);
            _elevenLabsTtsService = new ElevenLabsTtsService(_videoService);
        }

        /// <summary>Gemini sinh danh sách kịch bản Triết lý (Quotes / Story).</summary>
        public Task<IReadOnlyList<PhilosophyScriptItem>> GeneratePhilosophyScriptsAsync(
            string topic,
            string mode,
            int count,
            AppSettings settings,
            int minDurationSeconds = 15,
            int maxDurationSeconds = 60,
            string profileName = null,
            string contentTemplateId = null,
            string contentMetadata = null,
            CancellationToken cancellationToken = default)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                throw new InvalidOperationException("Cần AI API Key trong Cài đặt.");
            }

            return _geminiService.GeneratePhilosophyScriptsAsync(
                topic,
                mode,
                count,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                minDurationSeconds,
                maxDurationSeconds,
                profileName,
                settings,
                contentTemplateId,
                contentMetadata,
                cancellationToken);
        }

        public async Task<List<string>> GenerateProductVideosAsync(
            IList<AiVideoGenInputItem> items,
            string script,
            IList<string> perItemScripts,
            AppSettings settings,
            string profileName,
            Action<string> logAction,
            Action<VideoRenderProgress> progressAction,
            CancellationToken cancellationToken,
            bool useMultiVoiceNarration = false)
        {
            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("No product items found for rendering.");
            }

            var outputs = new string[items.Count];
            var semaphore = new SemaphoreSlim(3, 3);
            var slotPool = new ConcurrentQueue<int>(new[] { 1, 2, 3 });
            var tasks = new List<Task>();

            for (var i = 0; i < items.Count; i++)
            {
                var itemIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var slot = 1;
                    if (!slotPool.TryDequeue(out slot))
                    {
                        slot = 1;
                    }

                    try
                    {
                        progressAction?.Invoke(new VideoRenderProgress
                        {
                            Slot = slot,
                            VideoIndex = itemIndex + 1,
                            TotalVideos = items.Count,
                            Percent = 0,
                            Stage = "Starting"
                        });

                        var single = new List<AiVideoGenInputItem> { items[itemIndex] };
                        var itemScript = script;
                        if (perItemScripts != null &&
                            perItemScripts.Count > itemIndex &&
                            !string.IsNullOrWhiteSpace(perItemScripts[itemIndex]))
                        {
                            itemScript = perItemScripts[itemIndex].Trim();
                        }

                        var itemProfile = string.IsNullOrWhiteSpace(items[itemIndex]?.ProfileName)
                            ? profileName
                            : items[itemIndex].ProfileName;
                        IList<string> singleProductScripts = null;
                        if (perItemScripts != null &&
                            perItemScripts.Count > itemIndex &&
                            !string.IsNullOrWhiteSpace(perItemScripts[itemIndex]))
                        {
                            singleProductScripts = new List<string> { perItemScripts[itemIndex].Trim() };
                        }

                        var output = await GenerateProductVideoAsync(
                            single,
                            itemScript,
                            settings,
                            itemProfile,
                            logAction,
                            cancellationToken,
                            (percent, stage) =>
                            {
                                progressAction?.Invoke(new VideoRenderProgress
                                {
                                    Slot = slot,
                                    VideoIndex = itemIndex + 1,
                                    TotalVideos = items.Count,
                                    Percent = percent,
                                    Stage = stage ?? string.Empty
                                });
                            },
                            useMultiVoiceNarration,
                            singleProductScripts).ConfigureAwait(false);

                        outputs[itemIndex] = output;
                        progressAction?.Invoke(new VideoRenderProgress
                        {
                            Slot = slot,
                            VideoIndex = itemIndex + 1,
                            TotalVideos = items.Count,
                            Percent = 100,
                            Stage = "Completed",
                            OutputPath = output,
                            IsCompleted = true
                        });
                    }
                    finally
                    {
                        slotPool.Enqueue(slot);
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return outputs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public Task<List<string>> GenerateProductVideosAsync(
            IList<AiVideoGenInputItem> items,
            string script,
            AppSettings settings,
            Action<string> logAction,
            Action<VideoRenderProgress> progressAction,
            CancellationToken cancellationToken)
        {
            return GenerateProductVideosAsync(
                items,
                script,
                null,
                settings,
                null,
                logAction,
                progressAction,
                cancellationToken);
        }

        public async Task<AffiliateVideoPipelineResult> GenerateAffiliateProductVideoAsync(
            IList<AiVideoGenInputItem> items,
            AppSettings settings,
            string profileName,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null,
            string category = null,
            string storageRoot = null,
            bool useMultiVoiceNarration = false)
        {
            if (items == null || items.Count < 4)
            {
                throw new InvalidOperationException("Affiliate pipeline requires at least 4 images of the same product.");
            }

            var selected = items.Take(4).ToList();
            var normalizedName = NormalizeName(selected[0]?.ProductName);
            if (selected.Any(x => NormalizeName(x?.ProductName) != normalizedName))
            {
                throw new InvalidOperationException("4 images must belong to the same product (same ProductName).");
            }

            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
            {
                throw new InvalidOperationException("Veo API key/endpoint is required for affiliate pipeline.");
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                throw new InvalidOperationException("AI API key is required to generate motion prompts.");
            }

            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profileName);
            ProfileScopedPaths.SetConfiguredStorageRoot(storageRoot);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(storageRoot, resolvedProfile);
            var cat = string.IsNullOrWhiteSpace(category) ? "AffiliateDeep" : category.Trim();
            var baseDir = ProfileScopedPaths.CreateGeneratedSessionFolder(resolvedProfile, cat);
            var sourceDir = Path.Combine(baseDir, "source_images");
            var aiImageDir = Path.Combine(baseDir, "ai_images");
            var clipsDir = Path.Combine(baseDir, "clips");
            var audioDir = Path.Combine(baseDir, "audio");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(aiImageDir);
            Directory.CreateDirectory(clipsDir);
            Directory.CreateDirectory(audioDir);

            var downloaded = await DownloadImagesAsync(selected, sourceDir, logAction, cancellationToken).ConfigureAwait(false);
            if (downloaded.Count < 4)
            {
                throw new InvalidOperationException("Could not download enough valid images for affiliate pipeline.");
            }

            var narrationFile = Path.Combine(audioDir, "narration.mp3");
            var narrationText = await BuildAffiliateNarrationScriptAsync(selected, settings, cancellationToken).ConfigureAwait(false);
            await GenerateNarrationWithTtsStrictAsync(
                narrationText,
                settings,
                narrationFile,
                logAction,
                cancellationToken,
                useMultiVoiceNarration,
                baseDir).ConfigureAwait(false);
            var narrationDuration = await GetAudioDurationSecondsAsync(narrationFile, logAction, cancellationToken).ConfigureAwait(false);
            var sceneDurations = AllocateSceneDurations(
                narrationDuration,
                Enumerable.Range(0, 4).Select(x => 1d).ToList());
            logAction?.Invoke("Affiliate Pipeline: scene durations -> " + string.Join(", ", sceneDurations.Select(x => x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s")));

            var sceneAssets = new List<AffiliateSceneAsset>();
            progressCallback?.Invoke(5, "Tạo ảnh");
            for (var i = 0; i < 4; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = downloaded[i];
                var shotHint = ResolvePasShotHint(i);
                logAction?.Invoke($"Affiliate Pipeline: PAS cảnh {i + 1}/4 ({shotHint})...");
                var imageProgress = 5 + (int)Math.Round(((i + 1) / 4d) * 35d);
                progressCallback?.Invoke(imageProgress, "Tạo ảnh");

                var redrawPrompt = BuildAffiliateRedrawPrompt(row.ProductName, row.Price, i);
                var aiImageUrl = await _videoService.GenerateContextImageAsync(
                    row.SourceImageUrl,
                    redrawPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    null,
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(aiImageUrl))
                {
                    throw new InvalidOperationException($"AI image generation failed for scene {i + 1}.");
                }

                var aiImagePath = Path.Combine(aiImageDir, $"scene_{i + 1:D2}.jpg");
                await DownloadFileAsync(aiImageUrl, aiImagePath, cancellationToken).ConfigureAwait(false);

                var motionPrompt = await BuildMotionPromptAsync(
                    row.ProductName,
                    row.Price,
                    i,
                    settings,
                    cancellationToken).ConfigureAwait(false);
                var videoUrl = await _videoService.GenerateVideoFromImageAsync(
                    aiImageUrl,
                    motionPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    sceneDurations[i],
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    throw new InvalidOperationException($"Veo scene generation failed for scene {i + 1}.");
                }

                var clipPath = Path.Combine(clipsDir, $"scene_{i + 1:D2}.mp4");
                await DownloadFileAsync(videoUrl, clipPath, cancellationToken).ConfigureAwait(false);

                sceneAssets.Add(new AffiliateSceneAsset
                {
                    Index = i + 1,
                    ProductName = row.ProductName,
                    SourceImagePath = row.ImagePath,
                    AiImagePath = aiImagePath,
                    MotionPrompt = motionPrompt,
                    SceneVideoPath = clipPath
                });

                var clipProgress = 40 + (int)Math.Round(((i + 1) / 4d) * 40d);
                progressCallback?.Invoke(clipProgress, "Tạo clip");
            }

            var finalOutput = Path.Combine(baseDir, $"affiliate_story_{DateTime.Now:HHmmss}.mp4");
            progressCallback?.Invoke(85, "Render");
            var veoClipPaths = sceneAssets.Select(x => x.SceneVideoPath).ToList();
            try
            {
                var hookBroll = await HookBRollLibraryService.GetRandomHookBRollAsync(_random, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(hookBroll) && File.Exists(hookBroll))
                {
                    veoClipPaths.Insert(0, hookBroll);
                    logAction?.Invoke("Đã chèn Hook B-Roll: " + Path.GetFileName(hookBroll));
                }
                else
                {
                    var hookDir = ProfileScopedPaths.GetHookBRollsDirectory(ensureExists: false);
                    logAction?.Invoke("Affiliate Deep: HookBRolls trống hoặc chưa có («" + hookDir +
                                        "») — ghép 4 cảnh Veo.");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("Affiliate Deep: Hook B-Roll bỏ qua — " + ex.Message);
            }

            await RenderVeoVerticalVideoAsync(
                veoClipPaths,
                finalOutput,
                narrationFile,
                narrationText,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
            var polished = await _affiliatePostProcessing.ApplyCtaTailOverlayOnlyAsync(
                finalOutput,
                settings,
                baseDir,
                logAction,
                cancellationToken).ConfigureAwait(false);
            progressCallback?.Invoke(100, "Render");
            logAction?.Invoke("Affiliate Deep: lưu tại Processed/" + resolvedProfile + "/" + cat + "/ → " + polished);

            return new AffiliateVideoPipelineResult
            {
                FinalVideoPath = polished,
                SceneVideos = sceneAssets
            };
        }

        /// <summary>
        /// Showcase sản phẩm: ghép clip Veo đã tạo TAY (theo Excel prompt) thành 1 video hoàn chỉnh —
        /// hook đầu + voice-over liên tục + CTA cuối. Không tự gọi Veo (khắc phục hạn chế của Affiliate Deep cũ).
        /// </summary>
        public async Task<AffiliateVideoPipelineResult> GenerateShowcaseVideoFromClipsAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string hookText,
            string ctaText,
            AppSettings settings,
            string profileName,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null,
            string category = null,
            string storageRoot = null,
            ShowcasePerVideoRenderSettings renderSettings = null,
            AssSubtitleGeneratorOptions subtitleOptions = null,
            bool overviewPreview = false)
        {
            if (orderedScenes == null || !ShowcaseWorkflowConstants.HasEnoughScenes(orderedScenes.Count))
            {
                throw new InvalidOperationException("Showcase render cần ít nhất 1 cảnh (ảnh + clip) cùng sản phẩm.");
            }

            var missingClips = orderedScenes
                .Select((s, i) => new { Order = i + 1, Path = s?.ClipPath })
                .Where(x => string.IsNullOrWhiteSpace(x.Path) || !File.Exists(x.Path))
                .Select(x => x.Order)
                .ToList();
            if (!overviewPreview && missingClips.Count > 0)
            {
                throw new InvalidOperationException(
                    "Thiếu clip Veo cho cảnh: " + string.Join(", ", missingClips) +
                    ". Hãy gán clip vào từng cảnh (clips_render) trước khi Render.");
            }

            var sessionBaseEarly = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(orderedScenes);
            var hasPreRenderedAudio = !overviewPreview
                && !string.IsNullOrWhiteSpace(sessionBaseEarly)
                && ShowcaseNarrationCacheHelper.HasFullMixPreviewFile(sessionBaseEarly);

            if (!overviewPreview && !hasPreRenderedAudio && string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                throw new InvalidOperationException("Cần AI API Key để tạo giọng đọc (voice-over).");
            }
            else if (overviewPreview && missingClips.Count > 0)
            {
                logAction?.Invoke("[Showcase] Tổng quan — thiếu clip cảnh " + string.Join(", ", missingClips) + " (dùng ảnh hoặc placeholder).");
            }

            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profileName);
            ProfileScopedPaths.SetConfiguredStorageRoot(storageRoot);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(storageRoot, resolvedProfile);
            var cat = string.IsNullOrWhiteSpace(category) ? "Showcase" : category.Trim();

            var sessionBase = sessionBaseEarly;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ProfileScopedPaths.CreateGeneratedSessionFolder(resolvedProfile, cat);
                logAction?.Invoke("[Showcase] Phiên render mới: " + sessionBase);
            }
            else
            {
                logAction?.Invoke("[Showcase] Render trong phiên làm việc: " + sessionBase);
            }

            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            var outputDir = ShowcaseNarrationCacheHelper.GetOutputDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            Directory.CreateDirectory(outputDir);

            var preRenderedAudioPath = hasPreRenderedAudio
                ? ShowcaseNarrationCacheHelper.GetFullMixPreviewPath(sessionBase)
                : null;

            var previousOutput = orderedScenes
                .Select(s => (s?.OutputVideoPath ?? string.Empty).Trim())
                .FirstOrDefault(p => !string.IsNullOrEmpty(p));
            if (!overviewPreview)
            {
                ShowcaseSessionCleanupHelper.PrepareForFreshRender(
                    sessionBase,
                    previousOutput,
                    logAction,
                    preserveSessionAudio: hasPreRenderedAudio);
            }
            else
            {
                logAction?.Invoke("[Showcase] Tổng quan — giữ audio/clip hiện có, ghi đè overview_preview.mp4.");
            }

            progressCallback?.Invoke(5, hasPreRenderedAudio ? "Chuẩn bị audio" : "Tạo giọng đọc");
            string narrationFile;
            string narrationPathForSubtitles = null;
            if (hasPreRenderedAudio)
            {
                narrationFile = preRenderedAudioPath;
                var bareNarration = Path.Combine(audioDir, ShowcaseNarrationCacheHelper.NarrationFileName);
                if (File.Exists(bareNarration))
                {
                    narrationPathForSubtitles = bareNarration;
                }

                logAction?.Invoke("[Showcase] Dùng audio thành phẩm tab Âm thanh — bỏ qua TTS/SFX/nhạc nền → "
                    + preRenderedAudioPath);
            }
            else if (overviewPreview)
            {
                narrationFile = await EnsureShowcaseOverviewNarrationAsync(
                    orderedScenes,
                    hookText,
                    ctaText,
                    settings,
                    sessionBase,
                    logAction,
                    cancellationToken,
                    renderSettings).ConfigureAwait(false);
            }
            else
            {
                var narrationBuild = await EnsureShowcaseNarrationAsync(
                    orderedScenes,
                    hookText,
                    ctaText,
                    settings,
                    sessionBase,
                    logAction,
                    cancellationToken,
                    ShowcasePerVideoRenderSettings.ToTtsOptions(renderSettings, settings)).ConfigureAwait(false);
                narrationFile = narrationBuild.NarrationFilePath;
            }

            var narrationText = ShowcaseNarrationCacheHelper.BuildNarrationScript(orderedScenes);

            progressCallback?.Invoke(30, "Ghép clip");
            List<string> veoClipPaths;
            if (overviewPreview)
            {
                var overviewWork = Path.Combine(sessionBase, "overview_clip_work");
                Directory.CreateDirectory(overviewWork);
                var ffmpegExe = ResolveFfmpegExecutablePath();
                var outputCanvas = renderSettings?.ResolveOutputCanvas(settings)
                                   ?? ShowcaseOutputAspectPresets.Vertical9x16;
                veoClipPaths = new List<string>();
                for (var i = 0; i < orderedScenes.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = await ShowcaseOverviewPlaceholderHelper.ResolveSceneClipForOverviewAsync(
                        orderedScenes[i],
                        i,
                        overviewWork,
                        ffmpegExe,
                        outputCanvas,
                        logAction,
                        cancellationToken).ConfigureAwait(false);
                    veoClipPaths.Add(path);
                }
            }
            else
            {
                veoClipPaths = orderedScenes.Select(s => s.ClipPath).ToList();
            }

            var finalOutput = overviewPreview
                ? Path.Combine(outputDir, "overview_preview.mp4")
                : Path.Combine(outputDir, $"showcase_{DateTime.Now:HHmmss}.mp4");
            progressCallback?.Invoke(50, "Render");
            var transitionSeconds = renderSettings?.TransitionSeconds > 0
                ? ShowcaseTransitionHelper.ClampSeconds(renderSettings.TransitionSeconds)
                : ResolveTransitionDuration(settings);
            await RenderVeoVerticalVideoAsync(
                veoClipPaths,
                finalOutput,
                narrationFile,
                narrationText,
                settings,
                logAction,
                cancellationToken,
                string.Empty,
                transitionSeconds,
                renderSettings,
                subtitleOptions,
                orderedScenes,
                ctaText,
                preRenderedAudioPath,
                narrationPathForSubtitles).ConfigureAwait(false);

            progressCallback?.Invoke(90, "Hoàn tất");
            var polished = finalOutput;
            logAction?.Invoke("[Showcase] Bỏ overlay chữ CTA cố định — dùng phụ đề burn-in.");

            progressCallback?.Invoke(100, "Xong");
            logAction?.Invoke("Showcase: lưu tại Processed/" + resolvedProfile + "/" + cat + "/ → " + polished);

            return new AffiliateVideoPipelineResult
            {
                FinalVideoPath = polished,
                SceneVideos = orderedScenes.Select((s, i) => new AffiliateSceneAsset
                {
                    Index = i + 1,
                    ProductName = s?.ProductName ?? string.Empty,
                    SourceImagePath = s?.ThumbnailPath ?? string.Empty,
                    MotionPrompt = s?.VeoPrompt ?? string.Empty,
                    SceneVideoPath = s?.ClipPath ?? string.Empty
                }).ToList()
            };
        }

        private static string BuildShowcaseNarrationScript(string hookText, IList<AiVideoGenInputItem> scenes, string ctaText)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(hookText))
            {
                parts.Add(hookText.Trim());
            }

            foreach (var scene in scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                if (!string.IsNullOrWhiteSpace(scene?.SceneVoiceover))
                {
                    parts.Add(scene.SceneVoiceover.Trim());
                }
            }

            if (!string.IsNullOrWhiteSpace(ctaText))
            {
                parts.Add(ctaText.Trim());
            }

            return string.Join(" ", parts);
        }

        /// <summary>Tổng quan: dùng cache narration nếu có; không gọi TTS mới — im lặng nếu chưa có audio.</summary>
        private async Task<string> EnsureShowcaseOverviewNarrationAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string hookText,
            string ctaText,
            AppSettings settings,
            string sessionBase,
            Action<string> logAction,
            CancellationToken cancellationToken,
            ShowcasePerVideoRenderSettings renderSettings)
        {
            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            var narrationFile = Path.Combine(audioDir, ShowcaseNarrationCacheHelper.NarrationFileName);
            var ttsOptions = ShowcasePerVideoRenderSettings.ToTtsOptions(renderSettings, settings);

            try
            {
                if (TtsAvailabilityHelper.IsAnyShowcaseTtsConfigured(settings))
                {
                    var fingerprint = ShowcaseNarrationCacheHelper.ComputeFingerprint(orderedScenes, settings, ttsOptions);
                    if (ShowcaseNarrationCacheHelper.TryReuseCachedNarration(audioDir, fingerprint, out narrationFile))
                    {
                        logAction?.Invoke("[Showcase] Tổng quan — dùng narration.mp3 đã có.");
                        return narrationFile;
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Showcase] Tổng quan — bỏ qua cache narration: " + ex.Message);
            }

            if (File.Exists(narrationFile))
            {
                logAction?.Invoke("[Showcase] Tổng quan — dùng narration.mp3 trong phiên (chưa khớp fingerprint).");
                return narrationFile;
            }

            var totalSeconds = 0d;
            for (var i = 0; i < orderedScenes.Count; i++)
            {
                totalSeconds += ShowcaseSceneDurationHelper.ResolveZoomClipDuration(orderedScenes[i]);
            }

            totalSeconds = Math.Max(ShowcaseSceneDurationHelper.MinClipSeconds, totalSeconds);
            var silencePath = Path.Combine(audioDir, "overview_silence.mp3");
            var ffmpegExe = ResolveFfmpegExecutablePath();
            logAction?.Invoke("[Showcase] Tổng quan — chưa có audio thoại, ghép im lặng ~" +
                              totalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s.");
            await ShowcaseFfmpegAudioHelper.CreateSilenceMp3Async(
                ffmpegExe,
                totalSeconds,
                silencePath,
                logAction,
                cancellationToken).ConfigureAwait(false);
            return silencePath;
        }

        /// <summary>Tạo hoặc dùng lại narration.mp3 timeline từng cảnh — dùng cho preview audio và render.</summary>
        public async Task<ShowcaseNarrationBuildResult> EnsureShowcaseNarrationAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string hookText,
            string ctaText,
            AppSettings settings,
            string sessionBase,
            Action<string> logAction,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions ttsOptions = null)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                throw new InvalidOperationException("Showcase cần danh sách cảnh để tạo audio.");
            }

            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            var narrationFile = Path.Combine(audioDir, ShowcaseNarrationCacheHelper.NarrationFileName);

            var narrationFingerprint = ShowcaseNarrationCacheHelper.ComputeFingerprint(
                orderedScenes,
                settings,
                ttsOptions);

            var reused = ShowcaseNarrationCacheHelper.TryReuseCachedNarration(audioDir, narrationFingerprint, out narrationFile);
            if (reused)
            {
                logAction?.Invoke("[TTS] Showcase: dùng lại narration.mp3 (kịch bản + giọng không đổi).");
            }
            else
            {
                if (ttsOptions == null)
                {
                    throw new InvalidOperationException(
                        "Chưa có narration.mp3 hoặc kịch bản/giọng đổi — mở «Âm thanh», chọn giọng rồi bấm «Tạo audio hook/thân».");
                }

                TtsAvailabilityHelper.ValidateEngine(settings, ttsOptions.Engine);

                var previewsLookFresh = NarrationPreviewsMatchFingerprint(audioDir, narrationFingerprint);
                if (!previewsLookFresh)
                {
                    if (ShowcaseNarrationCacheHelper.HasBodyPreviewFile(sessionBase))
                    {
                        logAction?.Invoke("[Showcase] Lời thoại thân đổi — xóa preview thân/narration cũ (giữ hook nếu có).");
                        ShowcaseNarrationCacheHelper.ClearBodyPreview(sessionBase);
                    }
                    else
                    {
                        ShowcaseNarrationCacheHelper.ClearCachedNarration(sessionBase);
                    }

                    ShowcaseNarrationCacheHelper.ClearFullMixPreview(sessionBase);
                }

                if (ShowcaseNarrationCacheHelper.HasHookPreviewFile(sessionBase)
                    && (ShowcaseNarrationCacheHelper.HasBodyPreviewFile(sessionBase)
                        || !HasShowcaseBodyVoiceAfterHook(orderedScenes)))
                {
                    var hookPath = ShowcaseNarrationCacheHelper.GetHookPreviewPath(sessionBase);
                    var bodyPath = ShowcaseNarrationCacheHelper.GetBodyPreviewPath(sessionBase);
                    await _affiliateNarrationService.AssembleShowcaseNarrationFromPreviewsAsync(
                        hookPath,
                        bodyPath,
                        orderedScenes,
                        hookText,
                        ctaText,
                        settings,
                        narrationFile,
                        audioDir,
                        logAction,
                        cancellationToken).ConfigureAwait(false);
                    ShowcaseNarrationCacheHelper.SaveFingerprint(audioDir, narrationFingerprint);
                }
                else if (ShowcaseNarrationCacheHelper.HasHookPreviewFile(sessionBase)
                         && !ShowcaseNarrationCacheHelper.HasBodyPreviewFile(sessionBase)
                         && HasShowcaseBodyVoiceAfterHook(orderedScenes))
                {
                    throw new InvalidOperationException(
                        "Chưa có body_preview.mp3 — bấm «Tạo audio thân» (giữ hook_preview hiện có).");
                }
                else
                {
                    await GenerateShowcaseNarrationWithTtsStrictAsync(
                        orderedScenes,
                        settings,
                        narrationFile,
                        logAction,
                        cancellationToken,
                        audioDir,
                        ttsOptions).ConfigureAwait(false);
                    ShowcaseNarrationCacheHelper.SaveFingerprint(audioDir, narrationFingerprint);
                }
            }

            FfmpegToolkitService.TryResolve(settings, out var resolvedToolkit, out _);
            var probe = resolvedToolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            var duration = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(probe, narrationFile, cancellationToken)
                .ConfigureAwait(false);

            return new ShowcaseNarrationBuildResult
            {
                NarrationFilePath = narrationFile,
                AudioDirectory = audioDir,
                ReusedFromCache = reused,
                DurationSeconds = duration,
                HookBrollPath = string.Empty
            };
        }

        /// <summary>Ghép timeline thoại + hiệu ứng + nhạc nền (preview, không cần render video).</summary>
        public async Task<string> BuildShowcaseFullAudioPreviewAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string narrationPath,
            AppSettings settings,
            ShowcasePerVideoRenderSettings renderSettings,
            string sessionBase,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var narrIn = (narrationPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(narrIn) || !File.Exists(narrIn))
            {
                throw new InvalidOperationException("Không tìm thấy file thoại để ghép audio.");
            }

            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffmpegExe = ResolveFfmpegExecutablePath();
            var ffprobeExe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();

            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            var workDir = Path.Combine(audioDir, "full_mix_preview_work");
            Directory.CreateDirectory(workDir);

            var speedPercent = renderSettings?.NarrationSpeedPercent ?? 0;
            var narrForMix = await ShowcaseNarrationAvSyncHelper.PrepareSpeedAdjustedMp3Async(
                    ffmpegExe,
                    ffprobeExe,
                    narrIn,
                    workDir,
                    speedPercent,
                    "narration_speed_for_mix.mp3",
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(narrForMix, narrIn, StringComparison.OrdinalIgnoreCase))
            {
                logAction?.Invoke("[Showcase] Tốc độ thoại: "
                    + ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(speedPercent).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "% — áp dụng trước khi ghép SFX/nhạc.");
            }

            var clipPaths = orderedScenes?.Select(s => (s?.ClipPath ?? string.Empty).Trim()).ToList()
                            ?? new List<string>();
            var transitionSeconds = renderSettings?.TransitionSeconds > 0
                ? ShowcaseTransitionHelper.ClampSeconds(renderSettings.TransitionSeconds)
                : ResolveTransitionDuration(settings);

            var narrationDuration = await GetAudioDurationSecondsAsync(narrForMix, logAction, cancellationToken)
                .ConfigureAwait(false);
            var timelineDuration = narrationDuration > 0.05d ? narrationDuration : 30d;

            logAction?.Invoke("[Showcase] Ghép hiệu ứng âm thanh lên timeline thoại…");
            var narrationWithSfx = await ShowcaseSfxMixHelper.MixIntoNarrationIfNeededAsync(
                    ffmpegExe,
                    ffprobeExe,
                    narrForMix,
                    orderedScenes,
                    clipPaths,
                    new ShowcaseSfxMixHelper.HookSfxOptions
                    {
                        Enabled = renderSettings?.HookSfxEnabled ?? false,
                        FileName = renderSettings?.HookSfxFile ?? string.Empty,
                        OffsetSeconds = renderSettings?.HookSfxOffsetSeconds ?? 0d,
                        VolumePercent = renderSettings?.HookSfxVolumePercent > 0
                            ? renderSettings.HookSfxVolumePercent
                            : ShowcaseSfxCatalog.DefaultVolumePercent
                    },
                    new ShowcaseSfxMixHelper.CtaSfxOptions
                    {
                        Enabled = renderSettings?.CtaSfxEnabled ?? false,
                        FileName = renderSettings?.CtaSfxFile ?? string.Empty,
                        OffsetSeconds = renderSettings?.CtaSfxOffsetSeconds ?? 0d,
                        VolumePercent = renderSettings?.CtaSfxVolumePercent > 0
                            ? renderSettings.CtaSfxVolumePercent
                            : ShowcaseSfxCatalog.DefaultVolumePercent
                    },
                    renderSettings?.SfxMasterEnabled ?? true,
                    transitionSeconds,
                    timelineDuration,
                    settings,
                    workDir,
                    logAction,
                    cancellationToken).ConfigureAwait(false);

            var trendMusic = ResolveBackgroundMusicFile(settings, renderSettings, logAction);
            var output = Path.Combine(audioDir, ShowcaseNarrationCacheHelper.FullMixPreviewFileName);
            if (string.IsNullOrWhiteSpace(trendMusic))
            {
                await WriteShowcaseFullMixPreviewOutputAsync(
                        narrationWithSfx,
                        output,
                        ffmpegExe,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
                logAction?.Invoke("[Showcase] Đã lưu thành phẩm audio (thoại + SFX) → " + output);
                return output;
            }

            var preparedTrendMusic = await PrepareBackgroundMusicForRenderAsync(
                trendMusic,
                timelineDuration,
                settings,
                workDir,
                logAction,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(preparedTrendMusic) || !File.Exists(preparedTrendMusic))
            {
                await WriteShowcaseFullMixPreviewOutputAsync(
                        narrationWithSfx,
                        output,
                        ffmpegExe,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
                logAction?.Invoke("[Showcase] Đã lưu thành phẩm audio (thoại + SFX) → " + output);
                return output;
            }

            var musicVolume = ResolveMusicVolume(settings, renderSettings);
            logAction?.Invoke("[Showcase] Trộn thoại + nhạc nền → thành phẩm audio…");
            await MixShowcaseNarrationWithBackgroundMusicAsync(
                    narrationWithSfx,
                    preparedTrendMusic,
                    musicVolume,
                    output,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            logAction?.Invoke("[Showcase] Đã lưu thành phẩm audio → " + output);
            return output;
        }

        private async Task WriteShowcaseFullMixPreviewOutputAsync(
            string sourcePath,
            string outputPath,
            string ffmpegExecutable,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var source = (sourcePath ?? string.Empty).Trim();
            var output = (outputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                throw new InvalidOperationException("Không có file nguồn để lưu thành phẩm audio.");
            }

            if (string.IsNullOrEmpty(output))
            {
                throw new InvalidOperationException("Thiếu đường dẫn file thành phẩm audio.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            if (string.Equals(
                    Path.GetFullPath(source),
                    Path.GetFullPath(output),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var ext = Path.GetExtension(source)?.ToLowerInvariant();
            if (ext == ".mp3")
            {
                File.Copy(source, output, true);
                return;
            }

            var args = "-y -i \"" + source + "\" -c:a libmp3lame -q:a 2 \"" + output + "\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private async Task MixShowcaseNarrationWithBackgroundMusicAsync(
            string narrationPath,
            string musicPath,
            double musicVolumeLinear,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var musicVolume = musicVolumeLinear.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var narrGain = ShowcaseAudioMixHelper.FormatGain(ShowcaseAudioMixHelper.NarrationPremixGain);
            var args = "-y -i \"" + narrationPath + "\" -stream_loop -1 -i \"" + musicPath
                       + "\" -filter_complex \"[0:a]volume=" + narrGain + "[narr];[1:a]volume=" + musicVolume
                       + "[music];[narr][music]amix=inputs=2" + ShowcaseAudioMixHelper.AmixWithMusicSuffix
                       + "[aout]\" -map \"[aout]\" -c:a libmp3lame -q:a 2 \""
                       + outputPath + "\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private async Task GenerateShowcaseNarrationWithTtsStrictAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            AppSettings settings,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string workDirectory,
            ShowcaseTtsRenderOptions showcaseTts)
        {
            logAction?.Invoke("[TTS] Showcase: dựng audio hook riêng + thân/CTA đọc liền…");
            await _affiliateNarrationService.GenerateShowcaseTimelineNarrationAsync(
                string.Empty,
                orderedScenes,
                string.Empty,
                0d,
                settings,
                outputAudioFile,
                workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".",
                logAction,
                cancellationToken,
                showcaseTts).ConfigureAwait(false);
        }

        private static bool NarrationPreviewsMatchFingerprint(string audioDir, string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(audioDir) || string.IsNullOrWhiteSpace(fingerprint))
            {
                return false;
            }

            var fingerprintPath = Path.Combine(audioDir, ShowcaseNarrationCacheHelper.FingerprintFileName);
            if (!File.Exists(fingerprintPath))
            {
                return false;
            }

            try
            {
                var saved = File.ReadAllText(fingerprintPath, TextFileEncoding.Utf8).Trim();
                return string.Equals(saved, fingerprint.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasShowcaseBodyVoiceAfterHook(IList<AiVideoGenInputItem> orderedScenes)
        {
            if (orderedScenes == null)
            {
                return false;
            }

            var foundHook = false;
            foreach (var scene in orderedScenes)
            {
                if (scene == null || scene.ShowcaseSceneSilent)
                {
                    continue;
                }

                if (!foundHook)
                {
                    foundHook = true;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task GenerateShowcaseHookPreviewAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string hookText,
            AppSettings settings,
            string sessionBase,
            Action<string> logAction,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions ttsOptions)
        {
            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            var path = ShowcaseNarrationCacheHelper.GetHookPreviewPath(sessionBase);
            ShowcaseNarrationCacheHelper.ClearHookPreview(sessionBase);
            await _affiliateNarrationService.GenerateShowcaseHookPreviewAsync(
                hookText,
                orderedScenes,
                settings,
                path,
                logAction,
                cancellationToken,
                ttsOptions).ConfigureAwait(false);
            await TryAssembleShowcaseNarrationFromPreviewsAsync(
                orderedScenes,
                hookText,
                string.Empty,
                settings,
                sessionBase,
                logAction,
                cancellationToken).ConfigureAwait(false);
            if (ttsOptions != null && ShowcaseNarrationCacheHelper.HasNarrationFile(sessionBase))
            {
                var fp = ShowcaseNarrationCacheHelper.ComputeFingerprint(orderedScenes, settings, ttsOptions);
                ShowcaseNarrationCacheHelper.SaveFingerprint(
                    ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase),
                    fp);
            }
        }

        public async Task GenerateShowcaseBodyPreviewAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string ctaText,
            AppSettings settings,
            string sessionBase,
            Action<string> logAction,
            CancellationToken cancellationToken,
            ShowcaseTtsRenderOptions ttsOptions)
        {
            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            var path = ShowcaseNarrationCacheHelper.GetBodyPreviewPath(sessionBase);
            ShowcaseNarrationCacheHelper.ClearBodyPreview(sessionBase);
            await _affiliateNarrationService.GenerateShowcaseBodyPreviewAsync(
                orderedScenes,
                ctaText,
                settings,
                path,
                audioDir,
                logAction,
                cancellationToken,
                ttsOptions).ConfigureAwait(false);
            await TryAssembleShowcaseNarrationFromPreviewsAsync(
                orderedScenes,
                string.Empty,
                ctaText,
                settings,
                sessionBase,
                logAction,
                cancellationToken).ConfigureAwait(false);
            if (ttsOptions != null && ShowcaseNarrationCacheHelper.HasNarrationFile(sessionBase))
            {
                var fp = ShowcaseNarrationCacheHelper.ComputeFingerprint(orderedScenes, settings, ttsOptions);
                ShowcaseNarrationCacheHelper.SaveFingerprint(
                    ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase),
                    fp);
            }
        }

        private async Task TryAssembleShowcaseNarrationFromPreviewsAsync(
            IList<AiVideoGenInputItem> orderedScenes,
            string hookText,
            string ctaText,
            AppSettings settings,
            string sessionBase,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (!ShowcaseNarrationCacheHelper.HasHookPreviewFile(sessionBase))
            {
                return;
            }

            var needsBody = HasShowcaseBodyVoiceAfterHook(orderedScenes);
            if (needsBody && !ShowcaseNarrationCacheHelper.HasBodyPreviewFile(sessionBase))
            {
                return;
            }

            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            var narrationFile = Path.Combine(audioDir, ShowcaseNarrationCacheHelper.NarrationFileName);
            await _affiliateNarrationService.AssembleShowcaseNarrationFromPreviewsAsync(
                ShowcaseNarrationCacheHelper.GetHookPreviewPath(sessionBase),
                ShowcaseNarrationCacheHelper.GetBodyPreviewPath(sessionBase),
                orderedScenes,
                hookText,
                ctaText,
                settings,
                narrationFile,
                audioDir,
                logAction,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<MascotChannelVideoPipelineResult> GenerateMascotChannelVideoAsync(
            string mascotImagePath,
            string channelTheme,
            IList<string> identityImagePaths,
            int sceneCount,
            AppSettings settings,
            string profileName,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null,
            string mascotStyle = null,
            Action<List<string>> onSceneScriptsReady = null,
            Action<string> onProcessingPhase = null,
            bool useLipSync = false,
            AvatarIdentityPackConfig lipSyncPack = null)
        {
            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath))
            {
                throw new InvalidOperationException("Mascot/model image is missing.");
            }

            if (string.IsNullOrWhiteSpace(channelTheme))
            {
                throw new InvalidOperationException("Channel theme is required.");
            }

            if (identityImagePaths == null || identityImagePaths.Count < 3 || identityImagePaths.Count > 5)
            {
                throw new InvalidOperationException("Identity pack must contain 3-5 images.");
            }
            if (sceneCount != 4 && sceneCount != 6 && sceneCount != 8)
            {
                sceneCount = 4;
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                throw new InvalidOperationException("AI API key is required.");
            }

            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
            {
                throw new InvalidOperationException("Veo API key/endpoint is required.");
            }

            var baseDir = ProfileScopedPaths.CreateGeneratedSessionFolder(profileName, "Mascot");
            var variantsDir = Path.Combine(baseDir, "variant_images");
            var clipsDir = Path.Combine(baseDir, "clips");
            var audioDir = Path.Combine(baseDir, "audio");
            Directory.CreateDirectory(variantsDir);
            Directory.CreateDirectory(clipsDir);
            Directory.CreateDirectory(audioDir);

            var validIdentityImages = identityImagePaths
                .Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
            if (validIdentityImages.Count < 3)
            {
                throw new InvalidOperationException("Identity pack does not contain enough valid images.");
            }

            var mascotDataUrl = BuildImageDataUrl(mascotImagePath);
            var identityDataUrls = validIdentityImages.Select(BuildImageDataUrl).ToList();
            var brain = MascotBrainStore.LoadOrCreate(profileName, mascotStyle);
            var personaStyle = brain.ToGeminiContextBlock();
            logAction?.Invoke("Mascot Pipeline: MascotBrain.json → " + MascotBrainStore.GetBrainPath(profileName));

            progressCallback?.Invoke(2, "Kịch bản");
            onProcessingPhase?.Invoke("Gemini: tạo kịch bản " + sceneCount + " cảnh…");
            var sceneScripts = await BuildMascotSceneScriptsAsync(
                channelTheme,
                sceneCount,
                settings,
                personaStyle,
                cancellationToken).ConfigureAwait(false);
            onSceneScriptsReady?.Invoke(new List<string>(sceneScripts));
            logAction?.Invoke("Mascot Pipeline: đã lưu " + sceneScripts.Count + " scene scripts.");

            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var emotionPack = AvatarIdentityPackStore.LoadOrCreate(nick);
            var sceneEmotions = await _emotionEngine.AnalyzeSceneEmotionsAsync(
                sceneScripts,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);

            var narrationFile = Path.Combine(audioDir, "narration.mp3");
            var narrationText = string.Join(" ", sceneScripts.Select(EmotionEngine.StripTagsForNarration));
            await GenerateNarrationWithTtsStrictAsync(
                AdaptNarrationForMascotGender(channelTheme, narrationText),
                settings,
                narrationFile,
                logAction,
                cancellationToken).ConfigureAwait(false);
            var narrationDuration = await GetAudioDurationSecondsAsync(narrationFile, logAction, cancellationToken).ConfigureAwait(false);
            var sceneDurations = AllocateSceneDurations(
                narrationDuration,
                sceneScripts.Select(x => Math.Max(1d, (x ?? string.Empty).Length)).ToList());
            logAction?.Invoke("Mascot Pipeline: scene durations -> " + string.Join(", ", sceneDurations.Select(x => x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s")));

            AvatarIdentityPackStore.EnsureVaultMouthAssets(nick, lipSyncPack);
            var lipSyncEnabled = useLipSync && lipSyncPack != null && AvatarIdentityPackStore.HasValidMouthAssets(lipSyncPack);
            if (useLipSync && !lipSyncEnabled)
            {
                logAction?.Invoke("Mascot LipSync: thiếu mouth_open.png trong AvatarVault — bỏ qua khớp miệng.");
            }

            if (lipSyncEnabled)
            {
                progressCallback?.Invoke(8, "Lipsync");
                onProcessingPhase?.Invoke("LipSync: phân tích biên độ âm thanh (chuẩn bị overlay)…");
                await _lipSyncService.AnalyzeTimelineAsync(narrationFile, settings, logAction, cancellationToken)
                    .ConfigureAwait(false);
                logAction?.Invoke("Mascot LipSync: timeline âm thanh sẵn sàng — overlay sau khi ghép clip.");
            }

            var sceneAssets = new List<MascotSceneAsset>();
            progressCallback?.Invoke(5, "Tạo ảnh");
            for (var i = 0; i < sceneScripts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sceneText = sceneScripts[i];
                var emotionTag = i < sceneEmotions.Count ? sceneEmotions[i].Tag : MascotEmotionTag.Neutral;
                var sceneMascotPath = EmotionEngine.ResolveEmotionImagePath(
                    nick,
                    emotionTag,
                    emotionPack,
                    mascotImagePath);
                if (string.IsNullOrWhiteSpace(sceneMascotPath) || !File.Exists(sceneMascotPath))
                {
                    sceneMascotPath = mascotImagePath;
                }

                logAction?.Invoke($"Mascot Pipeline: scene {i + 1} emotion [{emotionTag}] → {Path.GetFileName(sceneMascotPath)}");
                var sceneMascotDataUrl = BuildImageDataUrl(sceneMascotPath);
                logAction?.Invoke($"Mascot Pipeline: creating scene {i + 1}/{sceneScripts.Count}...");
                var imageProgress = 5 + (int)Math.Round(((i + 1) / (double)sceneScripts.Count) * 35d);
                progressCallback?.Invoke(imageProgress, "Tạo ảnh");

                var variantPrompt = BuildMascotVariantImagePrompt(
                    channelTheme,
                    sceneText + " [Emotion: " + emotionTag + "]",
                    personaStyle,
                    validIdentityImages.Count);
                onProcessingPhase?.Invoke($"Cảnh {i + 1}/{sceneScripts.Count}: tạo ảnh (Processing)…");
                var variantImageUrl = await _mascotWorker.GenerateContextImageWithPollingAsync(
                    sceneMascotDataUrl,
                    variantPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    identityDataUrls,
                    cancellationToken,
                    logAction,
                    onProcessingPhase).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(variantImageUrl))
                {
                    throw new InvalidOperationException($"Failed to create mascot variant image for scene {i + 1}.");
                }

                var variantImagePath = Path.Combine(variantsDir, $"scene_{i + 1:D2}.jpg");
                await DownloadFileAsync(variantImageUrl, variantImagePath, cancellationToken).ConfigureAwait(false);

                var motionPrompt = await BuildMascotMotionPromptAsync(
                    channelTheme,
                    sceneText,
                    settings,
                    personaStyle,
                    cancellationToken).ConfigureAwait(false);
                onProcessingPhase?.Invoke($"Cảnh {i + 1}/{sceneScripts.Count}: Veo clip (Processing)…");
                var clipUrl = await _mascotWorker.GenerateVideoFromImageWithPollingAsync(
                    variantImageUrl,
                    motionPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    sceneDurations[i],
                    cancellationToken,
                    logAction,
                    onProcessingPhase).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(clipUrl))
                {
                    throw new InvalidOperationException($"Failed to create video clip for scene {i + 1}.");
                }

                var clipPath = Path.Combine(clipsDir, $"scene_{i + 1:D2}.mp4");
                await DownloadFileAsync(clipUrl, clipPath, cancellationToken).ConfigureAwait(false);

                sceneAssets.Add(new MascotSceneAsset
                {
                    Index = i + 1,
                    SceneScript = sceneText,
                    VariantImagePath = variantImagePath,
                    MotionPrompt = motionPrompt,
                    SceneVideoPath = clipPath
                });

                var clipProgress = 40 + (int)Math.Round(((i + 1) / (double)sceneScripts.Count) * 40d);
                progressCallback?.Invoke(clipProgress, "Tạo clip");
            }

            var tempOutput = Path.Combine(baseDir, $"mascot_story_{DateTime.Now:HHmmss}.mp4");
            progressCallback?.Invoke(85, "Render");
            onProcessingPhase?.Invoke("FFmpeg: ghép video cuối…");
            await RenderVeoVerticalVideoAsync(
                sceneAssets.Select(x => x.SceneVideoPath).ToList(),
                tempOutput,
                narrationFile,
                narrationText,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);

            var finalOutput = Path.Combine(baseDir, BuildMascotStoryFileName(nick, channelTheme));
            if (File.Exists(finalOutput))
            {
                File.Delete(finalOutput);
            }

            if (lipSyncEnabled)
            {
                progressCallback?.Invoke(92, "Lipsync");
                onProcessingPhase?.Invoke("LipSync: overlay miệng theo volume (FFmpeg)…");
                var lipsyncOut = Path.Combine(baseDir, "mascot_lipsync_final.mp4");
                var lipRequest = new LipSyncService.LipSyncRenderRequest
                {
                    VideoPath = tempOutput,
                    AudioPath = narrationFile,
                    MouthClosedPath = lipSyncPack.MouthClosedPath,
                    MouthOpenSmallPath = lipSyncPack.MouthOpenSmallPath,
                    MouthOpenPath = lipSyncPack.MouthOpenPath,
                    OverlayX = lipSyncPack.MouthOverlayX,
                    OverlayY = lipSyncPack.MouthOverlayY,
                    OverlayScale = lipSyncPack.MouthOverlayScale <= 0 ? 1d : lipSyncPack.MouthOverlayScale,
                    OutputPath = lipsyncOut
                };
                try
                {
                    var applied = await _lipSyncService.TryApplyToVideoWithFallbackAsync(
                        lipRequest,
                        settings,
                        logAction,
                        cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(applied) && File.Exists(applied))
                    {
                        File.Copy(applied, finalOutput, true);
                    }
                    else
                    {
                        File.Move(tempOutput, finalOutput);
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("Mascot LipSync bỏ qua (lỗi FFmpeg): " + ex.Message);
                    File.Move(tempOutput, finalOutput);
                }
            }
            else
            {
                File.Move(tempOutput, finalOutput);
            }

            progressCallback?.Invoke(100, "Render");
            logAction?.Invoke("Mascot Pipeline final -> " + finalOutput);

            return new MascotChannelVideoPipelineResult
            {
                FinalVideoPath = finalOutput,
                Scenes = sceneAssets,
                ProfileName = nick,
                ChannelTheme = channelTheme
            };
        }

        public Task GenerateMascotPreviewNarrationAsync(
            string channelTheme,
            string narrationText,
            AppSettings settings,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var adapted = AdaptNarrationForMascotGender(channelTheme, narrationText);
            return GenerateNarrationWithTtsStrictAsync(adapted, settings, outputAudioFile, logAction, cancellationToken);
        }

        public static string BuildMascotStoryFileName(string profileName, string channelTheme)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var theme = (channelTheme ?? "Story").Trim();
            if (theme.Length > 48)
            {
                theme = theme.Substring(0, 48);
            }

            theme = Regex.Replace(theme, @"[^\w\-]+", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(theme))
            {
                theme = "Story";
            }

            return $"{nick}_MascotStory_{theme}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }

        public async Task<MascotVariantPreviewResult> GenerateMascotVariantPreviewAsync(
            string mascotImagePath,
            string channelTheme,
            IList<string> identityImagePaths,
            int sceneCount,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string mascotStyle = null)
        {
            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath))
            {
                throw new InvalidOperationException("Mascot/model image is missing.");
            }

            if (string.IsNullOrWhiteSpace(channelTheme))
            {
                throw new InvalidOperationException("Channel theme is required.");
            }

            if (identityImagePaths == null || identityImagePaths.Count < 3 || identityImagePaths.Count > 5)
            {
                throw new InvalidOperationException("Identity pack must contain 3-5 images.");
            }

            if (sceneCount != 4 && sceneCount != 6 && sceneCount != 8)
            {
                sceneCount = 4;
            }

            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
            {
                throw new InvalidOperationException("Veo API key/endpoint is required.");
            }

            var validIdentityImages = identityImagePaths
                .Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
            if (validIdentityImages.Count < 3)
            {
                throw new InvalidOperationException("Identity pack does not contain enough valid images.");
            }

            var baseDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "generated_videos",
                "mascot_preview",
                DateTime.Now.ToString("yyyyMMdd"),
                DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(baseDir);

            var mascotDataUrl = BuildImageDataUrl(mascotImagePath);
            var identityDataUrls = validIdentityImages.Select(BuildImageDataUrl).ToList();
            var personaStyle = (mascotStyle ?? string.Empty).Trim();
            var sceneScripts = await BuildMascotSceneScriptsAsync(
                channelTheme,
                sceneCount,
                settings,
                personaStyle,
                cancellationToken).ConfigureAwait(false);

            var previews = new List<string>();
            var previewCount = Math.Min(4, sceneScripts.Count);
            for (var i = 0; i < previewCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var variantPrompt = BuildMascotVariantImagePrompt(
                    channelTheme,
                    sceneScripts[i],
                    personaStyle,
                    validIdentityImages.Count);
                var variantImageUrl = await _videoService.GenerateContextImageAsync(
                    mascotDataUrl,
                    variantPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    identityDataUrls,
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(variantImageUrl))
                {
                    continue;
                }

                var previewPath = Path.Combine(baseDir, $"preview_{i + 1:D2}.jpg");
                await DownloadFileAsync(variantImageUrl, previewPath, cancellationToken).ConfigureAwait(false);
                previews.Add(previewPath);
            }

            return new MascotVariantPreviewResult
            {
                SceneScripts = sceneScripts,
                PreviewImagePaths = previews
            };
        }

        public async Task<MascotSceneRegenerateResult> RegenerateMascotSceneAsync(
            string mascotImagePath,
            string channelTheme,
            IList<string> identityImagePaths,
            string sceneScript,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string mascotStyle = null)
        {
            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath))
            {
                throw new InvalidOperationException("Mascot/model image is missing.");
            }

            if (string.IsNullOrWhiteSpace(channelTheme))
            {
                throw new InvalidOperationException("Channel theme is required.");
            }

            if (string.IsNullOrWhiteSpace(sceneScript))
            {
                throw new InvalidOperationException("Scene script is required.");
            }

            if (identityImagePaths == null || identityImagePaths.Count < 3 || identityImagePaths.Count > 5)
            {
                throw new InvalidOperationException("Identity pack must contain 3-5 images.");
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey) || string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
            {
                throw new InvalidOperationException("AI/Veo credentials are required.");
            }

            var validIdentityImages = identityImagePaths
                .Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
            if (validIdentityImages.Count < 3)
            {
                throw new InvalidOperationException("Identity pack does not contain enough valid images.");
            }

            var baseDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "generated_videos",
                "mascot_preview",
                DateTime.Now.ToString("yyyyMMdd"),
                DateTime.Now.ToString("HHmmssfff"));
            Directory.CreateDirectory(baseDir);

            var personaStyle = (mascotStyle ?? string.Empty).Trim();
            var mascotDataUrl = BuildImageDataUrl(mascotImagePath);
            var identityDataUrls = validIdentityImages.Select(BuildImageDataUrl).ToList();
            var variantPrompt = BuildMascotVariantImagePrompt(
                channelTheme,
                sceneScript,
                personaStyle,
                validIdentityImages.Count);
            var variantImageUrl = await _videoService.GenerateContextImageAsync(
                mascotDataUrl,
                variantPrompt,
                settings.VeoApiKey,
                settings.VeoEndpoint,
                identityDataUrls,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(variantImageUrl))
            {
                throw new InvalidOperationException("Failed to regenerate mascot scene image.");
            }

            var previewPath = Path.Combine(baseDir, "preview_regen.jpg");
            await DownloadFileAsync(variantImageUrl, previewPath, cancellationToken).ConfigureAwait(false);
            var motionPrompt = await BuildMascotMotionPromptAsync(
                channelTheme,
                sceneScript,
                settings,
                personaStyle,
                cancellationToken).ConfigureAwait(false);

            logAction?.Invoke("Mascot Scene Regenerate: image + motion prompt regenerated.");
            return new MascotSceneRegenerateResult
            {
                PreviewImagePath = previewPath,
                MotionPrompt = motionPrompt,
                SceneScript = sceneScript
            };
        }

        public async Task<string> GenerateProductVideoAsync(
            IList<AiVideoGenInputItem> items,
            string script,
            AppSettings settings,
            string profileName,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null,
            bool useMultiVoiceNarration = false,
            IList<string> perProductScripts = null)
        {
            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("No product items found for rendering.");
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                throw new InvalidOperationException("Script is empty. Please generate script first.");
            }

            var baseDir = ProfileScopedPaths.CreateGeneratedSessionFolder(profileName, "Slideshow");
            var imagesDir = Path.Combine(baseDir, "images");
            var clipsDir = Path.Combine(baseDir, "clips");
            Directory.CreateDirectory(imagesDir);
            Directory.CreateDirectory(clipsDir);

            progressCallback?.Invoke(5, "Downloading images");
            logAction?.Invoke("AI Video Gen: downloading product images...");
            var downloadedImages = await DownloadImagesAsync(items, imagesDir, logAction, cancellationToken).ConfigureAwait(false);
            if (downloadedImages.Count == 0)
            {
                throw new InvalidOperationException("No valid product images downloaded.");
            }

            var hasContextImageApi = !string.IsNullOrWhiteSpace(settings?.VeoApiKey) && !string.IsNullOrWhiteSpace(settings?.VeoEndpoint);
            if (hasContextImageApi)
            {
                progressCallback?.Invoke(15, "Generating AI luxury product images");
                var aiContextDir = Path.Combine(baseDir, "ai_context_images");
                Directory.CreateDirectory(aiContextDir);
                logAction?.Invoke("AI Video Gen: generating AI re-rendered product images (luxury modern context)...");
                for (var i = 0; i < downloadedImages.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = downloadedImages[i];
                    if (string.IsNullOrWhiteSpace(row.SourceImageUrl))
                    {
                        continue;
                    }

                    try
                    {
                        var prompt = BuildLuxuryModernProductPrompt(row.ProductName, row.Price);
                        var aiImageUrl = await _videoService.GenerateContextImageAsync(
                            row.SourceImageUrl,
                            prompt,
                            settings.VeoApiKey,
                            settings.VeoEndpoint,
                            null,
                            cancellationToken).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(aiImageUrl))
                        {
                            continue;
                        }

                        var aiPath = Path.Combine(aiContextDir, $"context_{i + 1:D3}.jpg");
                        await DownloadFileAsync(aiImageUrl, aiPath, cancellationToken).ConfigureAwait(false);
                        row.ImagePath = aiPath;
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke("AI Video Gen: context-image fallback to original -> " + ex.Message);
                    }
                }
            }

            var audioFile = Path.Combine(baseDir, "narration.mp3");
            progressCallback?.Invoke(28, "Generating narration");
            await GenerateNarrationAudioAsync(
                script,
                settings,
                baseDir,
                audioFile,
                logAction,
                cancellationToken,
                useMultiVoiceNarration).ConfigureAwait(false);
            logAction?.Invoke("AI Video Gen: narration saved -> " + audioFile);
            var audioDuration = await GetAudioDurationSecondsAsync(audioFile, logAction, cancellationToken).ConfigureAwait(false);
            var ffmpegExe = ResolveFfmpegExecutablePath();
            IReadOnlyList<WordTimestamp> whisperWords = Array.Empty<WordTimestamp>();
            try
            {
                whisperWords = await SlideshowSemanticTimingService.TryGetWordTimestampsAsync(
                    ffmpegExe,
                    audioFile,
                    settings?.AiApiKey,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Slideshow] Whisper/prepare timing lỗi: " + ex.Message);
            }

            if (whisperWords.Count == 0)
            {
                var estimated = SubtitleTimingHelper.EstimateWordTimestamps(
                    script,
                    Math.Max(1000d, audioDuration * 1000d));
                if (estimated.Count > 0)
                {
                    whisperWords = estimated;
                    logAction?.Invoke("[Slideshow] Ước lượng " + estimated.Count + " từ từ script (fallback timing).");
                }
            }

            var segmentScripts = SlideshowSemanticTimingService.BuildSegmentScriptsForImages(
                downloadedImages.Count,
                script,
                perProductScripts);
            var perImageDurations = SlideshowSemanticTimingService.ResolvePerImageDurationsSeconds(
                downloadedImages.Count,
                audioDuration,
                whisperWords,
                segmentScripts,
                logAction);
            var scriptOverlay = BuildScriptOverlayText(script);
            var transitionDuration = ResolveTransitionDuration(settings);
            var textSize = ResolveTextSize(settings);
            var musicVolume = ResolveMusicVolume(settings);
            var visualVariant = BuildVisualVariant();
            logAction?.Invoke("[Slideshow] Thời lượng từng ảnh (s): " + string.Join(", ",
                perImageDurations.Select(d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))));
            logAction?.Invoke($"AI Video Gen: unique variant -> transition={visualVariant.Transition}, text={visualVariant.TextPosition}, brightness={visualVariant.BrightnessDelta:+0.00;-0.00}, contrast={visualVariant.Contrast:0.00}");

            logAction?.Invoke("AI Video Gen: sanitizing images (strip metadata)...");
            progressCallback?.Invoke(40, "Sanitizing images");
            var sanitizedImagesDir = Path.Combine(baseDir, "sanitized_images");
            Directory.CreateDirectory(sanitizedImagesDir);
            for (var i = 0; i < downloadedImages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sanitizedImagePath = Path.Combine(sanitizedImagesDir, $"sanitized_{i + 1:D3}.png");
                await SanitizeImageAsync(downloadedImages[i].ImagePath, sanitizedImagePath, logAction, cancellationToken).ConfigureAwait(false);
                downloadedImages[i].ImagePath = sanitizedImagePath;
            }

            logAction?.Invoke("AI Video Gen: rendering image clips with zoom/fade...");
            progressCallback?.Invoke(58, "Rendering clips");
            var clipFiles = new List<string>();
            for (var i = 0; i < downloadedImages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var clipPath = Path.Combine(clipsDir, $"clip_{i + 1:D3}.mp4");
                var item = downloadedImages[i];
                var clipDuration = i < perImageDurations.Count
                    ? perImageDurations[i]
                    : ResolvePerImageDuration(audioDuration, downloadedImages.Count);
                await RenderImageClipAsync(
                    item.ImagePath,
                    clipPath,
                    item?.ProductName ?? string.Empty,
                    item?.Price ?? string.Empty,
                    scriptOverlay,
                    clipDuration,
                    textSize,
                    visualVariant.TextPosition,
                    visualVariant.BrightnessDelta,
                    visualVariant.Contrast,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
                clipFiles.Add(clipPath);
            }

            var slideshowFile = Path.Combine(baseDir, "slideshow.mp4");
            logAction?.Invoke("AI Video Gen: building smooth transitions (xfade)...");
            progressCallback?.Invoke(76, "Building transitions");
            await BuildTransitionVideoAsync(
                clipFiles,
                slideshowFile,
                perImageDurations,
                transitionDuration,
                visualVariant.Transition,
                logAction,
                cancellationToken).ConfigureAwait(false);

            var slideshowDuration = await GetVideoDurationSecondsAsync(slideshowFile, logAction, cancellationToken).ConfigureAwait(false);
            if (slideshowDuration < 1d)
            {
                slideshowDuration = Math.Max(8d, audioDuration);
            }

            var outputFile = Path.Combine(baseDir, $"product_video_{DateTime.Now:HHmmss}.mp4");
            var backgroundMusic = ResolveBackgroundMusicFile();
            string preparedBackgroundMusic = null;
            if (!string.IsNullOrWhiteSpace(backgroundMusic))
            {
                logAction?.Invoke("AI Video Gen: AffiliateBed — trim nhạc nền khớp độ dài video (~" +
                                  slideshowDuration.ToString("0.##") + "s) → " + Path.GetFileName(backgroundMusic));
                preparedBackgroundMusic = Path.Combine(baseDir, "music_prepared.mp3");
                progressCallback?.Invoke(88, "Preparing background music");
                await PrepareBackgroundMusicTrackAsync(
                    backgroundMusic,
                    preparedBackgroundMusic,
                    slideshowDuration,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }

            logAction?.Invoke("AI Video Gen: merging clips + voice + music (1080x1920)...");
            progressCallback?.Invoke(95, "Final merge");
            KaraokeAssSubtitleService.KaraokeAssBurnInResult karaokeBurnIn = null;
            try
            {
                karaokeBurnIn = await KaraokeAssSubtitleService.TryCreateBurnInAsync(
                    ffmpegExe,
                    script,
                    audioFile,
                    baseDir,
                    logAction,
                    cancellationToken,
                    settings?.AiApiKey,
                    precomputedWordTimestamps: whisperWords).ConfigureAwait(false);
                var concatArgs = BuildFinalRenderArgs(
                    slideshowFile,
                    audioFile,
                    preparedBackgroundMusic,
                    outputFile,
                    musicVolume,
                    karaokeBurnIn?.VideoFilterFragment);
                await RunFfmpegAsync(concatArgs, logAction, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(karaokeBurnIn?.AssFilePath);
            }

            try
            {
                outputFile = await _affiliatePostProcessing.ApplyCtaTailOverlayOnlyAsync(
                    outputFile,
                    settings,
                    baseDir,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("AI Video Gen: CTA overlay skipped -> " + ex.Message);
            }

            progressCallback?.Invoke(98, "Cover thumbnail");
            await SaveSlideshowCoverThumbnailAsync(
                outputFile,
                downloadedImages,
                logAction,
                cancellationToken).ConfigureAwait(false);
            progressCallback?.Invoke(100, "Completed");

            return outputFile;
        }

        public async Task<string> RunFullVideoPipelineAsync(
            IList<AiVideoGenInputItem> items,
            string script,
            AppSettings settings,
            string profileName,
            Action<string> logAction,
            Action<VideoRenderProgress> progressAction,
            CancellationToken cancellationToken)
        {
            logAction?.Invoke("[Pipeline] Đang chạy bước Render...");
            var renderedVideo = await GenerateProductVideoAsync(
                items,
                script,
                settings,
                profileName,
                logAction,
                cancellationToken,
                (p, s) => progressAction?.Invoke(new VideoRenderProgress { Percent = p, Stage = s })
            ).ConfigureAwait(false);

            logAction?.Invoke("[Pipeline] Đang chạy bước Đóng gói (Post-processing)...");
            var baseDir = Path.GetDirectoryName(renderedVideo);

            var finalPath = await _affiliatePostProcessing.ApplyCtaTailOverlayOnlyAsync(
                renderedVideo,
                settings,
                baseDir,
                logAction,
                cancellationToken
            ).ConfigureAwait(false);

            return finalPath;
        }

        /// <summary>
        /// Điểm tiếp nhận tab Video reup: ElevenLabs → ASS → FFmpeg burn-in.
        /// </summary>
        public async Task RunFullVideoPipelineAsync(
            VideoReupRowItem item,
            bool useElevenLabs,
            bool burnSubtitle,
            Action<string> logAction,
            CancellationToken ct)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            ct.ThrowIfCancellationRequested();
            EnsureReupStageFolder(item);
            ResolveReupPipelinePaths(item);

            if (useElevenLabs && string.IsNullOrWhiteSpace((item.HookText ?? string.Empty).Trim()))
            {
                throw new InvalidOperationException(
                    "Hook trống — gõ hook vào cột «Hook» hoặc bấm «Gemini: tạo hook».");
            }

            var sourceVideo = (item.VideoPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sourceVideo) || !File.Exists(sourceVideo))
            {
                throw new InvalidOperationException(
                    "Chưa có video nguồn — nhập URL (rời ô để tải), hoặc chạy «Voiceover đọc hook» / «Tạo video thành phẩm».");
            }

            if (useElevenLabs)
            {
                logAction?.Invoke("[Pipeline] ElevenLabs TTS...");
                var transcript = await _elevenLabsTtsService.GenerateAndGetTranscriptAsync(
                    item.HookText,
                    item.AudioPath,
                    ct).ConfigureAwait(false);
                item.Transcript = transcript;
            }

            string assPath = string.Empty;
            if (burnSubtitle && item.Transcript != null)
            {
                _assSubtitleGenerator.WorkDirectory = item.ReupStageFolder;
                var settings = await new ConfigManager().LoadAsync().ConfigureAwait(false);
                if (File.Exists(item.AudioPath) &&
                    VideoReupRemixService.TryValidateFfmpegToolkit(settings, out _))
                {
                    var ms = await SubtitleTimingHelper.GetAudioDurationMsAsync(
                        settings.FfmpegPath,
                        item.AudioPath,
                        ct).ConfigureAwait(false);
                    _assSubtitleGenerator.DurationMsOverride = ms;
                }

                assPath = _assSubtitleGenerator.Generate(
                    item.Transcript,
                    ReupSubtitleStyleHelper.BuildOptions(settings));
                logAction?.Invoke("[Pipeline] Phụ đề đã tạo tại: " + assPath);
            }

            logAction?.Invoke("[Pipeline] FFmpeg burn-in...");
            await ExecuteFfmpegBurnInAsync(item.VideoPath, assPath, item.OutputPath, logAction, ct)
                .ConfigureAwait(false);

            item.IsProcessed = true;
            item.RemixStatus = "Xong";
            logAction?.Invoke("[Pipeline] Hoàn tất → " + item.OutputPath);
        }

        private static void ResolveReupPipelinePaths(VideoReupRowItem item)
        {
            if (string.IsNullOrWhiteSpace(item.AudioPath))
            {
                var tempDir = ProfileScopedPaths.GetTempDownloadsRoot(item.ProfileName);
                Directory.CreateDirectory(tempDir);
                item.AudioPath = Path.Combine(tempDir, "hook_pipeline_" + Guid.NewGuid().ToString("N") + ".mp3");
            }

            if (string.IsNullOrWhiteSpace(item.OutputPath))
            {
                var outDir = ProfileScopedPaths.GetVideoReupOutputRoot(item.ProfileName);
                Directory.CreateDirectory(outDir);
                var safe = VideoReupCaptionService.SanitizeFileNameFragment(item.ProductName);
                if (string.IsNullOrWhiteSpace(safe))
                {
                    safe = "video";
                }

                item.OutputPath = Path.Combine(
                    outDir,
                    "reup_burnin_" + safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");
            }
        }

        private async Task ExecuteFfmpegBurnInAsync(
            string videoPath,
            string assPath,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var settings = await new ConfigManager().LoadAsync().ConfigureAwait(false);
            if (!VideoReupRemixService.TryValidateFfmpegToolkit(settings, out var ffmpegErr))
            {
                throw new InvalidOperationException(ffmpegErr);
            }

            await ExecuteFfmpegBurnInAsync(
                videoPath,
                assPath,
                outputPath,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task ExecuteFfmpegBurnInAsync(
            string videoPath,
            string assPath,
            string outputPath,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                throw new FileNotFoundException("Không tìm thấy video nguồn để burn-in.", videoPath ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("OutputPath trống.", nameof(outputPath));
            }

            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            var ffmpeg = (settings.FfmpegPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ffmpeg))
            {
                throw new InvalidOperationException("Chưa cấu hình FFmpeg Path.");
            }

            string args;
            if (!string.IsNullOrWhiteSpace(assPath) && File.Exists(assPath))
            {
                var esc = KaraokeAssSubtitleService.EscapePathForFfmpegSubtitleFilter(assPath);
                var vf = "subtitles='" + esc + "'";
                args = "-y -i \"" + videoPath + "\" -vf \"" + vf +
                       "\" -c:v libx264 -preset medium -crf 23 -c:a copy \"" + outputPath + "\"";
                logAction?.Invoke("[Pipeline] Burn-in ASS: " + assPath);
            }
            else
            {
                args = "-y -i \"" + videoPath + "\" -c copy \"" + outputPath + "\"";
                logAction?.Invoke("[Pipeline] Không có ASS — copy video.");
            }

            try
            {
                await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, logAction, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(assPath);
            }

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException("FFmpeg không tạo được file output.");
            }
        }

        private static void EnsureReupStageFolder(VideoReupRowItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.ReupStageFolder) && Directory.Exists(item.ReupStageFolder))
            {
                return;
            }

            var safe = VideoReupCaptionService.SanitizeFileNameFragment(item.ProductName);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = "video";
            }

            var hash = (item.VideoUrl ?? string.Empty).GetHashCode().ToString("X8");
            var stagesRoot = ProfileScopedPaths.GetVideoReupStagesRoot(item.ProfileName);
            var dir = Path.Combine(stagesRoot, safe + "_" + hash);
            Directory.CreateDirectory(dir);
            item.ReupStageFolder = dir;
        }

        private async Task<List<DownloadedProductImage>> DownloadImagesAsync(
            IList<AiVideoGenInputItem> items,
            string imagesDir,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var output = new List<DownloadedProductImage>();
            for (var i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = items[i];
                var url = row?.ImageUrl?.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var ext = Path.GetExtension(url);
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5)
                {
                    ext = ".jpg";
                }

                var filePath = Path.Combine(imagesDir, $"img_{i + 1:D3}{ext}");
                try
                {
                    await DownloadFileAsync(url, filePath, cancellationToken).ConfigureAwait(false);
                    output.Add(new DownloadedProductImage
                    {
                        ImagePath = filePath,
                        SourceImageUrl = url,
                        ProductName = (row?.ProductName ?? string.Empty).Trim(),
                        Price = (row?.Price ?? string.Empty).Trim()
                    });
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("AI Video Gen: skip image download failed -> " + ex.Message);
                }
            }

            return output;
        }

        private static async Task DownloadFileAsync(string url, string outputPath, CancellationToken cancellationToken)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0");
                cancellationToken.Register(() => webClient.CancelAsync());
                await webClient.DownloadFileTaskAsync(new Uri(url), outputPath).ConfigureAwait(false);
            }
        }

        private async Task RenderImageClipAsync(
            string imagePath,
            string clipPath,
            string productName,
            string price,
            string scriptOverlay,
            double clipDurationSeconds,
            int textSize,
            string textPosition,
            double brightnessDelta,
            double contrast,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var safeName = EscapeDrawText(string.IsNullOrWhiteSpace(productName) ? "San pham noi bat" : productName.Trim());
            var safePrice = EscapeDrawText(string.IsNullOrWhiteSpace(price) ? string.Empty : price.Trim());
            var label = string.IsNullOrWhiteSpace(safePrice)
                ? safeName
                : $"{safeName} - {safePrice}";

            var safeScriptOverlay = EscapeDrawText(scriptOverlay);
            var yMain = ResolveMainTextY(textPosition);
            var ySub = yMain + Math.Max(72, (int)Math.Round(textSize * 1.25d));
            var boxY = Math.Max(40, yMain - 70);
            var boxHeight = Math.Max(180, textSize + 150);
            var textBar = "drawbox=x=40:y=" + boxY + ":w=1000:h=" + boxHeight + ":color=black@0.42:t=fill";
            var textMain = "drawtext=text='" + label + "':x=70:y=" + yMain + ":fontsize=" + textSize + ":fontcolor=white:line_spacing=10";
            var textSub = string.IsNullOrWhiteSpace(safeScriptOverlay)
                ? string.Empty
                : ",drawtext=text='" + safeScriptOverlay + "':x=70:y=" + ySub + ":fontsize=" + Math.Max(20, (int)Math.Round(textSize * 0.6d)) + ":fontcolor=white@0.95:line_spacing=8";
            var clipDurationText = clipDurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var zoomFrames = Math.Max(1, (int)Math.Round(clipDurationSeconds * 30d));
            var fadeOutStart = Math.Max(0d, clipDurationSeconds - 0.45d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var eqFilter = "eq=brightness=" + brightnessDelta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                           ":contrast=" + contrast.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var postOverlay =
                eqFilter + "," +
                "zoompan=z='min(zoom+0.0009,1.16)':d=" + zoomFrames + ":s=1080x1920:fps=30," +
                "fade=t=in:st=0:d=0.35," +
                "fade=t=out:st=" + fadeOutStart + ":d=0.45," +
                textBar + "," +
                textMain +
                textSub;

            try
            {
                var filterComplex = BuildSmartBoxBlurPaddingFilterComplex(postOverlay);
                var args = "-y -loop 1 -i \"" + imagePath + "\" -t " + clipDurationText +
                           " -filter_complex \"" + filterComplex + "\" -map \"[vout]\" -r 30 -map_metadata -1" +
                           " -c:v libx264 -preset medium -pix_fmt yuv420p \"" + clipPath + "\"";
                logAction?.Invoke("[Slideshow FFmpeg] Smart Boxblur Padding → " + Path.GetFileName(clipPath));
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Slideshow FFmpeg] Boxblur lỗi, fallback pad đen: " + ex.Message);
                var vf =
                    "scale=1080:1920:force_original_aspect_ratio=decrease," +
                    "pad=1080:1920:(ow-iw)/2:(oh-ih)/2," +
                    postOverlay;
                var fallbackArgs = "-y -loop 1 -i \"" + imagePath + "\" -t " + clipDurationText +
                                   " -vf \"" + vf + "\" -r 30 -map_metadata -1 -c:v libx264 -preset medium -pix_fmt yuv420p \"" +
                                   clipPath + "\"";
                await RunFfmpegAsync(fallbackArgs, logAction, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Nền boxblur 1080x1920 + ảnh foreground căn giữa (TikTok 9:16).</summary>
        private static string BuildSmartBoxBlurPaddingFilterComplex(string postOverlayFilters)
        {
            return "[0:v]scale=1080:1920:force_original_aspect_ratio=increase,boxblur=20:20,crop=1080:1920[bg];" +
                   "[0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg];" +
                   "[bg][fg]overlay=(W-w)/2:(H-h)/2," +
                   (postOverlayFilters ?? string.Empty) +
                   "[vout]";
        }

        private static async Task BuildTransitionVideoAsync(
            IList<string> clipFiles,
            string outputFile,
            IList<double> clipDurationSeconds,
            double transitionDurationSeconds,
            string transitionName,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (clipFiles == null || clipFiles.Count == 0)
            {
                throw new InvalidOperationException("No clip files to merge.");
            }

            var durations = NormalizeClipDurations(clipFiles.Count, clipDurationSeconds);

            if (clipFiles.Count == 1)
            {
                var singleArgs = $"-y -i \"{clipFiles[0]}\" -c:v libx264 -preset medium -pix_fmt yuv420p \"{outputFile}\"";
                await RunFfmpegAsync(singleArgs, logAction, cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                var inputBuilder = new StringBuilder();
                for (var i = 0; i < clipFiles.Count; i++)
                {
                    inputBuilder.Append("-i \"").Append(clipFiles[i]).Append("\" ");
                }

                var filter = new StringBuilder();
                var currentLabel = "[0:v]";
                var timeline = durations[0];
                for (var i = 1; i < clipFiles.Count; i++)
                {
                    var outputLabel = i == clipFiles.Count - 1 ? "[vout]" : $"[vx{i}]";
                    var offset = Math.Max(0d, timeline - transitionDurationSeconds);
                    filter
                        .Append(currentLabel)
                        .Append("[")
                        .Append(i)
                        .Append(":v]xfade=transition=")
                        .Append(string.IsNullOrWhiteSpace(transitionName) ? "fade" : transitionName)
                        .Append(":duration=")
                        .Append(transitionDurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                        .Append(":offset=")
                        .Append(offset.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                        .Append(outputLabel)
                        .Append(";");
                    currentLabel = outputLabel;
                    timeline += durations[i] - transitionDurationSeconds;
                }

                var args = $"-y {inputBuilder}-filter_complex \"{filter}\" -map \"[vout]\" -r 30 -c:v libx264 -preset medium -pix_fmt yuv420p \"{outputFile}\"";
                logAction?.Invoke("[Slideshow FFmpeg] xfade " + clipFiles.Count + " clip (semantic durations).");
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Slideshow FFmpeg] xfade lỗi: " + ex.Message);
                throw;
            }
        }

        private static IList<double> NormalizeClipDurations(int clipCount, IList<double> clipDurationSeconds)
        {
            if (clipCount <= 0)
            {
                return Array.Empty<double>();
            }

            if (clipDurationSeconds == null || clipDurationSeconds.Count == 0)
            {
                return Enumerable.Repeat(4.2d, clipCount).ToList();
            }

            var list = new List<double>(clipCount);
            for (var i = 0; i < clipCount; i++)
            {
                var value = i < clipDurationSeconds.Count ? clipDurationSeconds[i] : clipDurationSeconds[clipDurationSeconds.Count - 1];
                list.Add(Math.Max(2.5d, value));
            }

            return list;
        }

        private static async Task SanitizeImageAsync(string inputPath, string outputPath, Action<string> logAction, CancellationToken cancellationToken)
        {
            try
            {
                var stillFrame = "scale=1080:1920:force_original_aspect_ratio=increase,boxblur=20:20,crop=1080:1920[bg];" +
                                 "[0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg];" +
                                 "[bg][fg]overlay=(W-w)/2:(H-h)/2[vout]";
                var args = "-y -i \"" + inputPath + "\" -frames:v 1 -filter_complex \"" + stillFrame +
                           "\" -map \"[vout]\" -map_metadata -1 -pix_fmt yuv420p \"" + outputPath + "\"";
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Slideshow FFmpeg] Sanitize boxblur lỗi, fallback: " + ex.Message);
                var args = $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -pix_fmt yuv420p \"{outputPath}\"";
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task PrepareBackgroundMusicTrackAsync(
            string sourceMusicPath,
            string outputPath,
            double targetDurationSeconds,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var safeTarget = Math.Max(8d, targetDurationSeconds);
            var durationText = safeTarget.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var fadeStart = Math.Max(0d, safeTarget - 1d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var filter =
                $"atrim=0:{durationText},asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.7,afade=t=out:st={fadeStart}:d=1";
            var args =
                $"-y -stream_loop -1 -i \"{sourceMusicPath}\" -af \"{filter}\" -map_metadata -1 -c:a libmp3lame -q:a 2 \"{outputPath}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task SaveSlideshowCoverThumbnailAsync(
            string outputVideoPath,
            IList<DownloadedProductImage> sourceImages,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(outputVideoPath) || !File.Exists(outputVideoPath))
            {
                return;
            }

            var videoName = Path.GetFileNameWithoutExtension(outputVideoPath);
            if (string.IsNullOrWhiteSpace(videoName))
            {
                videoName = "slideshow";
            }

            var coverPath = Path.Combine(Path.GetDirectoryName(outputVideoPath) ?? string.Empty, videoName + "_cover.jpg");
            var firstImage = (sourceImages ?? new List<DownloadedProductImage>())
                .FirstOrDefault(x => x != null && !string.IsNullOrWhiteSpace(x.ImagePath) && File.Exists(x.ImagePath));
            if (firstImage != null)
            {
                try
                {
                    File.Copy(firstImage.ImagePath, coverPath, overwrite: true);
                    logAction?.Invoke("AI Video Gen: cover from first product image -> " + coverPath);
                    return;
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("AI Video Gen: first image cover fallback -> " + ex.Message);
                }
            }

            try
            {
                var frameArgs =
                    $"-y -i \"{outputVideoPath}\" -ss 00:00:01.000 -vframes 1 -q:v 2 \"{coverPath}\"";
                await RunFfmpegAsync(frameArgs, logAction, cancellationToken).ConfigureAwait(false);
                if (File.Exists(coverPath) && new FileInfo(coverPath).Length > 2048)
                {
                    logAction?.Invoke("AI Video Gen: cover thumbnail (frame) -> " + coverPath);
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("AI Video Gen: frame extract failed -> " + ex.Message);
            }
        }

        private static async Task RunFfmpegAsync(string args, Action<string> logAction, CancellationToken cancellationToken)
        {
            var ffmpegExecutable = ResolveFfmpegExecutablePath();
            await FfmpegProcessRunner.RunAsync(
                    ffmpegExecutable,
                    args,
                    logAction,
                    cancellationToken,
                    logPrefix: "FFmpeg",
                    timeoutSeconds: FfmpegProcessRunner.DefaultTimeoutSeconds)
                .ConfigureAwait(false);
        }

        private static string ResolveFfmpegExecutablePath()
        {
            try
            {
                var settings = new ConfigManager().LoadAsync().GetAwaiter().GetResult();
                var configuredPath = (settings?.FfmpegPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
                {
                    return configuredPath;
                }
            }
            catch
            {
                // Fallback to PATH lookup below.
            }

            return "ffmpeg";
        }

        private static async Task<double> GetAudioDurationSecondsAsync(string audioPath, Action<string> logAction, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                return 0d;
            }

            var ffprobeExecutable = ResolveFfprobeExecutablePath();
            var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioPath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobeExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 80).ConfigureAwait(false);

                var output = (await outputTask.ConfigureAwait(false) ?? string.Empty).Trim();
                var errors = (await errorTask.ConfigureAwait(false) ?? string.Empty).Trim();
                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrWhiteSpace(errors))
                    {
                        logAction?.Invoke("AI Video Gen: ffprobe warning -> " + errors);
                    }
                    return 0d;
                }

                if (double.TryParse(output, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                {
                    return seconds > 0d ? seconds : 0d;
                }
            }

            return 0d;
        }

        private static string ResolveFfprobeExecutablePath()
        {
            var ffmpegExecutable = ResolveFfmpegExecutablePath();
            if (string.IsNullOrWhiteSpace(ffmpegExecutable))
            {
                return "ffprobe";
            }

            try
            {
                if (ffmpegExecutable.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var probeCandidate = ffmpegExecutable.Substring(0, ffmpegExecutable.Length - "ffmpeg.exe".Length) + "ffprobe.exe";
                    if (File.Exists(probeCandidate))
                    {
                        return probeCandidate;
                    }
                }
            }
            catch
            {
                // fallback below
            }

            return "ffprobe";
        }

        private async Task GenerateNarrationAudioAsync(
            string script,
            AppSettings settings,
            string baseDir,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken,
            bool useMultiVoiceNarration = false)
        {
            Exception lastError = null;

            if (!string.IsNullOrWhiteSpace(settings?.TtsApiKey) && !string.IsNullOrWhiteSpace(settings?.TtsEndpoint))
            {
                try
                {
                    await _affiliateNarrationService.GenerateNarrationAsync(
                        script,
                        settings,
                        outputAudioFile,
                        useMultiVoiceNarration,
                        baseDir,
                        logAction,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    logAction?.Invoke("AI Video Gen: TTS API failed, fallback to Edge TTS. Reason: " + ex.Message);
                }
            }

            await GenerateEdgeTtsAudioAsync(script, settings, baseDir, outputAudioFile, logAction, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(outputAudioFile))
            {
                throw new InvalidOperationException("Could not generate narration audio.", lastError);
            }
        }

        private async Task GenerateEdgeTtsAudioAsync(
            string script,
            AppSettings settings,
            string baseDir,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            logAction?.Invoke("AI Video Gen: Edge TTS (vi-VN)…");
            var edge = new EdgeTtsService();
            var synthesis = ShowcaseEdgeTtsVoiceResolver.ResolveFemaleSouthYoung();
            var temp = await edge.SynthesizeLongTextToTempMp3Async(
                script,
                synthesis,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
            var targetDir = Path.GetDirectoryName(outputAudioFile);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(outputAudioFile))
            {
                File.Delete(outputAudioFile);
            }

            File.Copy(temp, outputAudioFile, true);
        }

        private static List<string> SplitText(string text, int maxChunk)
        {
            var source = (text ?? string.Empty).Trim();
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(source))
            {
                return result;
            }

            var words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();
            foreach (var word in words)
            {
                if (current.Length + word.Length + 1 > maxChunk && current.Length > 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }

                current.Append(word).Append(' ');
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString().Trim());
            }

            return result;
        }

        private static string ResolveBackgroundMusicFile()
        {
            OmniAudioLibrary.EnsureSharedDirectoriesExist();
            var dir = OmniAudioLibrary.GetSharedMusicDirectory();
            if (!Directory.Exists(dir))
            {
                return string.Empty;
            }

            foreach (var pattern in new[] { "*.mp3", "*.wav", "*.m4a" })
            {
                var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            return string.Empty;
        }

        private static string BuildFinalRenderArgs(
            string concatFile,
            string narrationFile,
            string backgroundMusicFile,
            string outputFile,
            double musicVolume,
            string subtitleVideoFilter = null)
        {
            var vf = BuildSlideshowVideoFilterChain(subtitleVideoFilter);
            if (string.IsNullOrWhiteSpace(backgroundMusicFile))
            {
                return $"-y -i \"{concatFile}\" -i \"{narrationFile}\" -map 0:v:0 -map 1:a:0 -vf \"{vf}\" -r 30 -c:v libx264 -preset medium -pix_fmt yuv420p -c:a aac -shortest \"{outputFile}\"";
            }

            var musicVolumeFilter = musicVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var narrGain = ShowcaseAudioMixHelper.FormatGain(ShowcaseAudioMixHelper.NarrationPremixGain);
            var amix = ShowcaseAudioMixHelper.AmixWithMusicSuffix;
            return $"-y -i \"{concatFile}\" -i \"{narrationFile}\" -stream_loop -1 -i \"{backgroundMusicFile}\" -filter_complex \"[1:a]volume={narrGain}[voice];[2:a]volume={musicVolumeFilter}[music];[voice][music]amix=inputs=2{amix}[aout]\" -map 0:v:0 -map \"[aout]\" -vf \"{vf}\" -r 30 -c:v libx264 -preset medium -pix_fmt yuv420p -c:a aac -shortest \"{outputFile}\"";
        }

        private static string BuildSlideshowVideoFilterChain(string subtitleVideoFilter)
        {
            var scale = "scale=1080:1920:flags=lanczos";
            return KaraokeAssSubtitleService.MergeVideoFilters(scale, subtitleVideoFilter);
        }

        private static double ResolvePerImageDuration(double audioDurationSeconds, int imageCount)
        {
            if (imageCount <= 0)
            {
                return 4.2d;
            }

            if (audioDurationSeconds <= 0.1d)
            {
                return 4.2d;
            }

            var perImage = audioDurationSeconds / imageCount;
            perImage = Math.Max(3.2d, perImage);
            perImage = Math.Min(7.5d, perImage);
            return perImage;
        }

        private static string BuildScriptOverlayText(string script)
        {
            var value = (script ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(value, "\\s+", " ");
            if (normalized.Length > 72)
            {
                normalized = normalized.Substring(0, 72).TrimEnd() + "...";
            }

            return normalized;
        }

        private static double ResolveTransitionDuration(AppSettings settings)
        {
            var value = settings?.VideoTransitionDurationSeconds ?? 0.6d;
            if (value < 0.2d) return 0.2d;
            if (value > 2.0d) return 2.0d;
            return value;
        }

        private static int ResolveTextSize(AppSettings settings)
        {
            var value = settings?.VideoTextSize ?? 50;
            if (value < 24) return 24;
            if (value > 96) return 96;
            return value;
        }

        private static double ResolveMusicVolume(AppSettings settings, ShowcasePerVideoRenderSettings renderSettings = null)
        {
            var percent = renderSettings?.MusicVolume ?? settings?.VideoMusicVolume ?? 14;
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return percent / 100d;
        }

        private static string EscapeDrawText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace(":", "\\:")
                .Replace("'", "\\'")
                .Replace(",", "\\,")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("%", "\\%")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string BuildLuxuryModernProductPrompt(string productName, string price)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm gia dụng" : productName.Trim();
            var priceText = string.IsNullOrWhiteSpace(price) ? string.Empty : (" giá " + price.Trim());
            return "Dựa trên ảnh gốc, nhận diện đúng sản phẩm chính và vẽ lại thành ảnh quảng bá cao cấp theo phong cách hiện đại. " +
                   "Bối cảnh gợi ý: đặt sản phẩm trên bàn đá cẩm thạch hoặc nền studio tối giản sang trọng, ánh sáng studio mềm, " +
                   "màu sắc sạch và premium. " +
                   "Nếu ảnh có người mẫu, thay khuôn mặt bằng gương mặt người mẫu Châu Á dễ thương, thân thiện, tự nhiên, phù hợp khách hàng Việt Nam; " +
                   "không làm kỳ dị khuôn mặt và giữ tỉ lệ cơ thể hợp lý. " +
                   "YÊU CẦU QUAN TRỌNG: giữ đúng form dáng, tỷ lệ, chi tiết nhận diện và cấu trúc thật của sản phẩm, " +
                   "không được biến dạng, không đổi kiểu sản phẩm, không thêm logo/thương hiệu sai lệch. " +
                   "Sản phẩm phải rõ nét, là chủ thể trung tâm của khung hình: " + name + priceText + ".";
        }

        private async Task<string> BuildMotionPromptAsync(
            string productName,
            string price,
            int sceneIndex,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm" : productName.Trim();
            var priceText = string.IsNullOrWhiteSpace(price) ? "không nêu giá" : price.Trim();
            var pasRole = ResolvePasShotHint(sceneIndex);
            var prompt =
                "Bạn là chuyên gia prompt video quảng cáo TikTok theo mô hình PAS (Pain-Agitate-Solve-CTA). " +
                "Viết MỘT video prompt tiếng Việt ngắn gọn (2-3 câu) cho Veo 3. " +
                "BẮT BUỘC đúng vai trò cảnh hiện tại trong chuỗi 4 cảnh PAS — không lẫn cảnh khác. " +
                "Mô tả chuyển động camera và vật lý chân thực (dolly, pan, macro push, light sweep) phù hợp vai trò. " +
                "Không markdown, không số thứ tự, không giải thích. " +
                $"Cảnh {sceneIndex + 1}/4 — {pasRole}. Sản phẩm: {name}. Giá: {priceText}.";

            var generated = await _geminiService.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            var body = string.IsNullOrWhiteSpace(generated)
                ? BuildPasFallbackMotionPrompt(sceneIndex, name)
                : Regex.Replace(generated.Trim(), "\\s+", " ");

            return AppendPasCameraLightingTail(body, sceneIndex);
        }

        private static string ResolvePasShotHint(int index)
        {
            switch (index)
            {
                case 0:
                    return "PAS Pain — tối màu, u ám, nhấn vấn đề sản phẩm giải quyết";
                case 1:
                    return "PAS Agitate — macro cận cảnh, zoom xoáy vào chi tiết sản phẩm";
                case 2:
                    return "PAS Solve — bừng sáng cinematic, glowing, tính năng hoàn hảo";
                case 3:
                    return "PAS CTA — quét ánh sáng qua sản phẩm hoặc logo";
                default:
                    return "PAS — cảnh quảng cáo";
            }
        }

        private static string BuildPasFallbackMotionPrompt(int sceneIndex, string productName)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm" : productName.Trim();
            switch (sceneIndex)
            {
                case 0:
                    return $"Cảnh u ám tối màu, camera chậm lướt qua bối cảnh thể hiện nỗi đau mà {name} giải quyết.";
                case 1:
                    return $"Macro push-in xoáy sâu vào chi tiết bề mặt {name}, nhấn kết cấu và điểm yếu trước khi giải pháp.";
                case 2:
                    return $"Ánh sáng cinematic bừng sáng, {name} phát sáng premium, thể hiện tính năng hoàn hảo rõ nét.";
                case 3:
                    return $"Quét sáng mạnh lướt qua {name} hoặc logo, camera ổn định kết thúc ấn tượng kêu gọi mua.";
                default:
                    return $"Quay cận {name}, chuyển động mượt, ánh sáng studio chân thực.";
            }
        }

        private static string AppendPasCameraLightingTail(string promptBody, int sceneIndex)
        {
            var tail = GetPasCameraLightingDirective(sceneIndex);
            if (string.IsNullOrWhiteSpace(promptBody))
            {
                return tail;
            }

            return promptBody.Trim() + " Camera & lighting: " + tail;
        }

        private static string GetPasCameraLightingDirective(int sceneIndex)
        {
            switch (sceneIndex)
            {
                case 0:
                    return "dark moody low-key lighting, desaturated tones, soft shadows, slow handheld drift, focus on customer pain point before product hero.";
                case 1:
                    return "macro close-up, aggressive push-in zoom, shallow depth of field, dramatic side light, emphasize texture and tension on product details.";
                case 2:
                    return "cinematic high-key lighting, soft glow and rim light, vibrant clean highlights, premium hero product reveal, gentle dolly-in.";
                case 3:
                    return "dynamic light sweep across product or logo, lens flare accent, stable confident framing, call-to-action energy.";
                default:
                    return "smooth product motion, realistic studio lighting.";
            }
        }

        private static string BuildAffiliateRedrawPrompt(string productName, string price, int sceneIndex)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm gia dụng" : productName.Trim();
            var priceText = string.IsNullOrWhiteSpace(price) ? string.Empty : (" giá " + price.Trim());
            var pasVisual = sceneIndex switch
            {
                0 => "Bối cảnh tối, u ám, màu lạnh — gợi vấn đề/nỗi đau trước khi dùng sản phẩm.",
                1 => "Góc macro cận chi tiết, tương phản cao — nhấn chất liệu và điểm cần cải thiện.",
                2 => "Ánh sáng studio sáng, cinematic glow — sản phẩm là giải pháp hoàn hảo, premium.",
                3 => "Hero shot sáng, có vùng highlight quét sáng — sẵn sàng CTA mua hàng.",
                _ => "Phong cách thương mại cao cấp."
            };

            return "Phân tích sản phẩm, giữ nguyên form dáng và chất liệu. " +
                   "Vẽ lại ảnh mới theo phong cách thương mại PAS: " + pasVisual + " " +
                   "Nếu có người mẫu cũ thì thay bằng người mẫu xinh đẹp, tự nhiên. " +
                   "YÊU CẦU BẮT BUỘC: không làm sai tỷ lệ, không biến dạng, giữ đúng nhận diện thật của sản phẩm. " +
                   $"Sản phẩm: {name}{priceText}. Cảnh PAS {sceneIndex + 1}/4.";
        }

        private async Task<List<string>> BuildMascotSceneScriptsAsync(
            string channelTheme,
            int sceneCount,
            AppSettings settings,
            string mascotStyle,
            CancellationToken cancellationToken)
        {
            var safeSceneCount = sceneCount == 6 || sceneCount == 8 ? sceneCount : 4;
            var styleBlock = string.IsNullOrWhiteSpace(mascotStyle)
                ? string.Empty
                : " Phong cách nhân vật/kênh (MascotStyle): " + mascotStyle.Trim() + ".";
            var prompt = $"Viết kịch bản video storytelling gồm đúng {safeSceneCount} phân cảnh cho kênh TikTok. " +
                         "Chủ đề kênh: " + channelTheme.Trim() + "." + styleBlock + " " +
                         $"Trả về đúng {safeSceneCount} dòng. MỖI dòng BẮT BUỘC bắt đầu bằng ĐÚNG MỘT tag cảm xúc: [HAPPY], [SAD] hoặc [NEUTRAL] (chỉ 3 tag), " +
                         "sau đó 'Cảnh i:' và 1-2 câu ngắn. Ví dụ: [HAPPY] Cảnh 1: ... " +
                         "Mỗi cảnh rõ hành động, bối cảnh — đồng bộ MascotStyle.";
            var raw = await _geminiService.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            var lines = (raw ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var scenes = lines.Where(x => x.StartsWith("Cảnh", StringComparison.OrdinalIgnoreCase)).Take(safeSceneCount).ToList();
            if (scenes.Count < safeSceneCount)
            {
                scenes = new List<string>
                {
                    "[NEUTRAL] Cảnh 1: Mở đầu giới thiệu linh vật trong bối cảnh đời thường gần gũi.",
                    "[HAPPY] Cảnh 2: Nhân vật tươi sáng thể hiện cá tính kênh.",
                    "[SAD] Cảnh 3: Cận cảnh biểu cảm, nhấn thông điệp chính.",
                    "[HAPPY] Cảnh 4: Kết thúc ấn tượng và lời kêu gọi theo dõi kênh."
                };
                while (scenes.Count < safeSceneCount)
                {
                    var idx = scenes.Count + 1;
                    scenes.Add($"Cảnh {idx}: Nhân vật tiếp tục câu chuyện theo chủ đề kênh với góc quay mới.");
                }
            }

            return scenes;
        }

        private static string BuildMascotVariantImagePrompt(
            string channelTheme,
            string sceneScript,
            string mascotStyle,
            int identityPackCount)
        {
            var styleLine = string.IsNullOrWhiteSpace(mascotStyle)
                ? string.Empty
                : " MascotStyle bắt buộc: " + mascotStyle.Trim() + ".";
            return "Dựa trên ảnh gốc linh vật/người mẫu + " + identityPackCount +
                   " ảnh Identity Pack (giữ nhận diện khuôn mặt), tạo ảnh biến thể theo phân cảnh. " +
                   "Giữ NGUYÊN khuôn mặt/nhân vật nhận diện chính, không đổi đặc trưng gương mặt, không đổi danh tính. " +
                   "Chỉ thay đổi bối cảnh, tư thế, góc chụp theo SceneScript. " +
                   "Phong cách điện ảnh, ánh sáng đẹp, bố cục rõ chủ thể." + styleLine + " " +
                   "Chủ đề kênh: " + channelTheme.Trim() + ". " +
                   "SceneScript: " + sceneScript.Trim();
        }

        private async Task<string> BuildMascotMotionPromptAsync(
            string channelTheme,
            string sceneScript,
            AppSettings settings,
            string mascotStyle,
            CancellationToken cancellationToken)
        {
            var styleLine = string.IsNullOrWhiteSpace(mascotStyle)
                ? string.Empty
                : " MascotStyle: " + mascotStyle.Trim() + ".";
            var prompt = "Viết một video prompt ngắn cho Veo 3 dựa trên cảnh sau, mô tả chuyển động camera và chuyển động vật lý tự nhiên. " +
                         "Không markdown, không giải thích." + styleLine +
                         " Chủ đề: " + channelTheme.Trim() + ". SceneScript: " + sceneScript.Trim();
            var text = await _geminiService.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text)
                ? "Camera dolly chậm, chủ thể chuyển động tự nhiên, ánh sáng mềm thay đổi theo chiều sâu khung hình."
                : Regex.Replace(text.Trim(), "\\s+", " ");
        }

        private static string BuildImageDataUrl(string imagePath)
        {
            var bytes = File.ReadAllBytes(imagePath);
            var ext = Path.GetExtension(imagePath)?.ToLowerInvariant();
            var mime = "image/jpeg";
            if (ext == ".png") mime = "image/png";
            if (ext == ".webp") mime = "image/webp";
            if (ext == ".gif") mime = "image/gif";
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }

        private async Task RenderVeoVerticalVideoAsync(
            IList<string> clipFiles,
            string outputFile,
            string narrationTrackPath,
            string narrationScript,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string customHookText = null,
            double? transitionDurationOverride = null,
            ShowcasePerVideoRenderSettings renderSettings = null,
            AssSubtitleGeneratorOptions subtitleOptions = null,
            IList<AiVideoGenInputItem> showcaseOrderedScenes = null,
            string showcaseCtaText = null,
            string preRenderedFullMixPath = null,
            string narrationPathForSubtitles = null)
        {
            if (clipFiles == null || clipFiles.Count == 0)
            {
                throw new InvalidOperationException("No Veo clips found to render.");
            }

            var usePreRenderedAudio = !string.IsNullOrWhiteSpace(preRenderedFullMixPath)
                && File.Exists(preRenderedFullMixPath.Trim());
            var subtitleSourcePath = !string.IsNullOrWhiteSpace(narrationPathForSubtitles)
                ? narrationPathForSubtitles.Trim()
                : narrationTrackPath;

            var renderDir = Path.Combine(Path.GetDirectoryName(outputFile) ?? AppDomain.CurrentDomain.BaseDirectory, "render_work");
            Directory.CreateDirectory(renderDir);
            var normalizedClips = new List<string>();
            var hookText = customHookText != null
                ? (customHookText ?? string.Empty).Trim()
                : ResolveOpeningHookText();
            var outputCanvas = renderSettings?.ResolveOutputCanvas(settings)
                               ?? ShowcaseOutputAspectPresets.Vertical9x16;
            logAction?.Invoke("[Showcase] Khung video: " + outputCanvas.DisplayLabel + " ("
                + outputCanvas.Width + "×" + outputCanvas.Height + ").");
            for (var i = 0; i < clipFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = Path.Combine(renderDir, $"normalized_{i + 1:D2}.mp4");
                var sourceDuration = await GetVideoDurationSecondsAsync(clipFiles[i], logAction, cancellationToken).ConfigureAwait(false);
                var fitMode = ResolveClipAspectFitMode(showcaseOrderedScenes, i);
                var kenBurnsVf = BuildKenBurnsFilter(sourceDuration, i, hookText, outputCanvas, fitMode);
                string audioEncodeArgs;
                if (usePreRenderedAudio)
                {
                    audioEncodeArgs = "-an";
                }
                else
                {
                    var clipHasAudio = await HasAudioStreamAsync(clipFiles[i], logAction, cancellationToken).ConfigureAwait(false);
                    audioEncodeArgs = clipHasAudio
                        ? "-c:a aac -b:a 320k -ar 48000"
                        : "-an";
                }

                var args = $"-y -i \"{clipFiles[i]}\" -vf \"{kenBurnsVf}\" -r 30 -c:v libx264 -preset slow -crf 14 -b:v 18M -maxrate 24M -bufsize 48M -pix_fmt yuv420p {audioEncodeArgs} \"{normalized}\"";
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
                normalizedClips.Add(normalized);
            }

            var stitched = Path.Combine(renderDir, "stitched.mp4");
            var transitionDuration = transitionDurationOverride ?? ResolveTransitionDuration(settings);
            if (usePreRenderedAudio)
            {
                logAction?.Invoke("[Showcase] Ghép cảnh chỉ video — audio lấy từ full_mix_preview.mp3.");
                await BuildSmoothTransitionVideoOnlyAsync(
                        normalizedClips,
                        stitched,
                        transitionDuration,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await BuildSmoothTransitionVideoVariableAsync(
                        normalizedClips,
                        stitched,
                        transitionDuration,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var ffmpegExe = ResolveFfmpegExecutablePath();
            FfmpegToolkitService.TryResolve(settings, out var renderToolkit, out _);
            var ffprobeExe = renderToolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            var narrationSpeedPercent = renderSettings?.NarrationSpeedPercent ?? 0;

            var avSync = await ShowcaseNarrationAvSyncHelper.ApplyAtRenderAsync(
                    ffmpegExe,
                    ffprobeExe,
                    stitched,
                    narrationTrackPath,
                    renderDir,
                    narrationSpeedPercent,
                    logAction,
                    cancellationToken,
                    preserveAudioSource: usePreRenderedAudio)
                .ConfigureAwait(false);
            stitched = avSync.VideoPath;

            var narrationForRender = avSync.NarrationPath;
            var stitchedDuration = avSync.TargetDurationSeconds > 0.01d
                ? avSync.TargetDurationSeconds
                : await GetVideoDurationSecondsAsync(stitched, logAction, cancellationToken).ConfigureAwait(false);
            string preparedTrendMusic = null;
            if (!usePreRenderedAudio)
            {
                var transitionDurationForSfx = transitionDurationOverride ?? ResolveTransitionDuration(settings);
                narrationForRender = await ShowcaseSfxMixHelper.MixIntoNarrationIfNeededAsync(
                        ffmpegExe,
                        ffprobeExe,
                        narrationForRender,
                        showcaseOrderedScenes,
                        clipFiles,
                        new ShowcaseSfxMixHelper.HookSfxOptions
                        {
                            Enabled = renderSettings?.HookSfxEnabled ?? false,
                            FileName = renderSettings?.HookSfxFile ?? string.Empty,
                            OffsetSeconds = renderSettings?.HookSfxOffsetSeconds ?? 0d,
                            VolumePercent = renderSettings?.HookSfxVolumePercent > 0
                                ? renderSettings.HookSfxVolumePercent
                                : ShowcaseSfxCatalog.DefaultVolumePercent
                        },
                        new ShowcaseSfxMixHelper.CtaSfxOptions
                        {
                            Enabled = renderSettings?.CtaSfxEnabled ?? false,
                            FileName = renderSettings?.CtaSfxFile ?? string.Empty,
                            OffsetSeconds = renderSettings?.CtaSfxOffsetSeconds ?? 0d,
                            VolumePercent = renderSettings?.CtaSfxVolumePercent > 0
                                ? renderSettings.CtaSfxVolumePercent
                                : ShowcaseSfxCatalog.DefaultVolumePercent
                        },
                        renderSettings?.SfxMasterEnabled ?? true,
                        transitionDurationForSfx,
                        stitchedDuration,
                        settings,
                        renderDir,
                        logAction,
                        cancellationToken).ConfigureAwait(false);
                var trendMusic = ResolveBackgroundMusicFile(settings, renderSettings, logAction);
                preparedTrendMusic = await PrepareBackgroundMusicForRenderAsync(
                    trendMusic,
                    stitchedDuration,
                    settings,
                    renderDir,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logAction?.Invoke("[Showcase] Bỏ qua trộn SFX/nhạc nền — dùng full_mix_preview.mp3.");
            }

            var hasAmbientAudio = await HasAudioStreamAsync(stitched, logAction, cancellationToken).ConfigureAwait(false);
            var fingerprintVariant = BuildRenderFingerprintVariant();
            var showcaseTiming = ShowcaseNarrationTimingManifest.TryLoadFromNarrationPath(subtitleSourcePath);
            var karaokeScript = showcaseTiming != null
                ? ShowcaseKaraokeTimingHelper.ResolveDisplayScript(showcaseTiming, narrationScript)
                : narrationScript;
            var karaokeNarrationPath = narrationForRender;
            if (avSync.AppliedAudioTempo > 1.03d)
            {
                logAction?.Invoke("[Showcase] Phụ đề: scale timeline theo audio tua x"
                    + avSync.AppliedAudioTempo.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
            else if (avSync.AppliedVideoTempo > 1.03d)
            {
                logAction?.Invoke("[Showcase] Phụ đề: video tua x"
                    + avSync.AppliedVideoTempo.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " — timeline theo audio render (~"
                    + avSync.TargetDurationSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                    + "s).");
            }
            KaraokeAssSubtitleService.KaraokeAssBurnInResult karaokeBurnIn = null;
            try
            {
                var subtitlePlan = renderSettings != null
                    ? ShowcaseSubtitleStyleHelper.BuildRenderPlan(renderSettings, settings)
                    : null;
                if (subtitlePlan != null && subtitlePlan.HasAnyEnabled)
                {
                    karaokeBurnIn = await KaraokeAssSubtitleService.TryCreateShowcaseBurnInAsync(
                        ffmpegExe,
                        karaokeScript,
                        karaokeNarrationPath,
                        renderDir,
                        logAction,
                        cancellationToken,
                        settings?.AiApiKey,
                        subtitlePlan,
                        showcaseTiming,
                        renderSettings?.SubtitleDisplay,
                        showcaseOrderedScenes,
                        showcaseCtaText,
                        renderSettings,
                        settings,
                        assFileName: null,
                        appliedAudioTempo: avSync.AppliedAudioTempo).ConfigureAwait(false);
                }
                else
                {
                    logAction?.Invoke("[Showcase] Phụ đề tắt — không chèn chữ lên video.");
                }

                var captionVideoFilter = karaokeBurnIn?.VideoFilterFragment ?? string.Empty;
                var logoPlan = ShowcaseBrandOverlayHelper.BuildRenderPlan(renderSettings, outputCanvas.Width);
                if (logoPlan.IsActive)
                {
                    logAction?.Invoke("[Showcase] Logo thương hiệu: "
                        + ShowcaseBrandLogoPositionCatalog.GetDisplayLabel(logoPlan.PositionId)
                        + " · " + logoPlan.ScaleWidthPercent + "% — "
                        + Path.GetFileName(logoPlan.LogoPath));
                }

                var variantGradeForOverlay = string.Empty;
                stitched = await ShowcaseBrandOverlayHelper.ApplyVideoOverlaysIfNeededAsync(
                    stitched,
                    renderDir,
                    captionVideoFilter,
                    variantGradeForOverlay,
                    logoPlan,
                    (args, ct) => RunFfmpegAsync(args, logAction, ct),
                    cancellationToken).ConfigureAwait(false);
                captionVideoFilter = string.Empty;

                if (usePreRenderedAudio)
                {
                    var copyVideo = string.IsNullOrWhiteSpace(captionVideoFilter);
                    logAction?.Invoke(copyVideo
                        ? "[Showcase] Ghép video + audio thành phẩm (copy video, không encode lại)…"
                        : "[Showcase] Ghép video + audio thành phẩm (có phụ đề)…");
                    var preMuxArgs = BuildPreRenderedAudioMuxArgs(
                        stitched,
                        outputFile,
                        narrationForRender,
                        captionVideoFilter,
                        fingerprintVariant,
                        copyVideoStream: copyVideo);
                    await RunFfmpegAsync(preMuxArgs, logAction, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(preparedTrendMusic))
                {
                    var noMusicArgs = BuildMetadataRenderArgs(
                        stitched,
                        outputFile,
                        narrationForRender,
                        null,
                        null,
                        hasAmbientAudio,
                        captionVideoFilter,
                        fingerprintVariant);
                    await RunFfmpegAsync(noMusicArgs, logAction, cancellationToken).ConfigureAwait(false);
                    return;
                }

                var musicVolume = ResolveMusicVolume(settings, renderSettings).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var withMusicArgs = BuildMetadataRenderArgs(
                    stitched,
                    outputFile,
                    narrationForRender,
                    preparedTrendMusic,
                    musicVolume,
                    hasAmbientAudio,
                    captionVideoFilter,
                    fingerprintVariant);
                await RunFfmpegAsync(withMusicArgs, logAction, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(karaokeBurnIn?.AssFilePath);
            }
        }

        private static string BuildPreRenderedAudioMuxArgs(
            string stitchedInput,
            string outputFile,
            string preRenderedAudioPath,
            string captionVideoFilter,
            RenderFingerprintVariant fingerprintVariant,
            bool copyVideoStream = false)
        {
            var tagTitle = "tiktok_omni_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var tagComment = "rendered_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var safeVariant = fingerprintVariant ?? new RenderFingerprintVariant();
            var audioArgs = "-c:a aac -b:a 320k -ar 48000 -movflags +faststart";
            var metaArgs = "-map_metadata -1 -metadata title=\"" + tagTitle + "\" -metadata comment=\"" + tagComment
                + "\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\"";

            if (copyVideoStream && string.IsNullOrWhiteSpace(captionVideoFilter))
            {
                return "-y -i \"" + stitchedInput + "\" -i \"" + preRenderedAudioPath
                    + "\" -map 0:v:0 -map 1:a:0 " + metaArgs + " -c:v copy " + audioArgs + " -shortest \"" + outputFile + "\"";
            }

            var qualityArgs = "-c:v libx264 -preset veryfast -crf 20 -profile:v high -level 4.2 -pix_fmt yuv420p -movflags +faststart -c:a aac -b:a 320k" +
                              " -r " + safeVariant.FrameRateText +
                              " -ar " + safeVariant.AudioSampleRate;
            var variantGrade = "drawbox=x=0:y=0:w=iw:h=ih:color=" + safeVariant.ColorHex + "@0.01:t=fill";
            var mergedVf = string.IsNullOrWhiteSpace(captionVideoFilter)
                ? variantGrade
                : captionVideoFilter + "," + variantGrade;
            var vfArg = $" -vf \"{mergedVf}\"";
            return $"-y -i \"{stitchedInput}\" -i \"{preRenderedAudioPath}\" -map 0:v:0 -map 1:a:0{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} \"{outputFile}\"";
        }

        private static string BuildMetadataRenderArgs(string stitchedInput, string outputFile, string narrationPath, string musicPath, string musicVolume, bool hasAmbientAudio, string captionVideoFilter, RenderFingerprintVariant fingerprintVariant)
        {
            var tagTitle = "tiktok_omni_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var tagComment = "rendered_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var hasNarration = !string.IsNullOrWhiteSpace(narrationPath) && File.Exists(narrationPath);
            var safeVariant = fingerprintVariant ?? new RenderFingerprintVariant();
            var qualityArgs = "-c:v libx264 -preset slow -crf 18 -profile:v high -level 4.2 -pix_fmt yuv420p -movflags +faststart -c:a aac -b:a 320k" +
                              " -r " + safeVariant.FrameRateText +
                              " -ar " + safeVariant.AudioSampleRate;
            var variantGrade = "drawbox=x=0:y=0:w=iw:h=ih:color=" + safeVariant.ColorHex + "@0.01:t=fill";
            var mergedVf = string.IsNullOrWhiteSpace(captionVideoFilter)
                ? variantGrade
                : captionVideoFilter + "," + variantGrade;
            var vfArg = $" -vf \"{mergedVf}\"";
            var narrGain = ShowcaseAudioMixHelper.FormatGain(ShowcaseAudioMixHelper.NarrationPremixGain);
            var amixMusic = ShowcaseAudioMixHelper.AmixWithMusicSuffix;
            if (string.IsNullOrWhiteSpace(musicPath))
            {
                if (hasNarration)
                {
                    if (hasAmbientAudio)
                    {
                        return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -filter_complex \"[0:a]volume=0.20[amb];[1:a]volume={narrGain}[narr];[amb][narr]amix=inputs=2{amixMusic}[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
                    }

                    return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -filter_complex \"[1:a]volume={narrGain}[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
                }

                if (hasAmbientAudio)
                {
                    return $"-y -i \"{stitchedInput}\" -filter_complex \"[0:a]volume=0.20[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
                }

                return $"-y -i \"{stitchedInput}\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} \"{outputFile}\"";
            }

            if (hasNarration)
            {
                if (hasAmbientAudio)
                {
                    return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -stream_loop -1 -i \"{musicPath}\" -filter_complex \"[0:a]volume=0.20[amb];[1:a]volume={narrGain}[narr];[2:a]volume={musicVolume}[music];[amb][narr][music]amix=inputs=3{amixMusic}[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
                }

                return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -stream_loop -1 -i \"{musicPath}\" -filter_complex \"[1:a]volume={narrGain}[narr];[2:a]volume={musicVolume}[music];[narr][music]amix=inputs=2{amixMusic}[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
            }

            if (hasAmbientAudio)
            {
                return $"-y -i \"{stitchedInput}\" -stream_loop -1 -i \"{musicPath}\" -filter_complex \"[0:a]volume=0.20[amb];[1:a]volume={musicVolume}[music];[amb][music]amix=inputs=2:duration=first:dropout_transition=2[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
            }

            return $"-y -i \"{stitchedInput}\" -stream_loop -1 -i \"{musicPath}\" -filter_complex \"[1:a]volume={musicVolume}[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
        }

        private RenderFingerprintVariant BuildRenderFingerprintVariant()
        {
            var fpsPool = new[] { "29.97", "30.00", "30.03", "29.98" };
            var sampleRatePool = new[] { 44100, 48000 };
            var colorPool = new[] { "0xF7EFE1", "0xEEF7FF", "0xFFF3E8", "0xF1F0FF" };
            return new RenderFingerprintVariant
            {
                FrameRateText = fpsPool[_random.Next(fpsPool.Length)],
                AudioSampleRate = sampleRatePool[_random.Next(sampleRatePool.Length)],
                ColorHex = colorPool[_random.Next(colorPool.Length)]
            };
        }

        private string ResolveBackgroundMusicFile(
            AppSettings settings,
            ShowcasePerVideoRenderSettings renderSettings,
            Action<string> logAction)
        {
            var selectedName = (renderSettings?.BackgroundMusicFile ?? settings?.VideoBackgroundMusicFileName ?? string.Empty).Trim();
            if (VideoReupRowItem.IsNoMusicSelection(selectedName))
            {
                logAction?.Invoke("Showcase: không trộn nhạc nền.");
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(selectedName))
            {
                var exact = VideoReupRemixService.ResolveMusicFilePath(selectedName, settings);
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    logAction?.Invoke("Showcase: nhạc nền -> " + Path.GetFileName(exact));
                    return exact;
                }
            }

            var allNames = VideoReupRemixService.ListMusicFileNames(settings);
            if (allNames.Count > 0)
            {
                var pickName = allNames[_random.Next(allNames.Count)];
                var picked = VideoReupRemixService.ResolveMusicFilePath(pickName, settings);
                if (!string.IsNullOrWhiteSpace(picked))
                {
                    logAction?.Invoke("Showcase: nhạc nền ngẫu nhiên -> " + Path.GetFileName(picked));
                    return picked;
                }
            }

            logAction?.Invoke("Showcase: không tìm thấy nhạc trong Assets\\Audio\\Music.");
            return string.Empty;
        }


        private string ResolveBackgroundMusicFile(AppSettings settings, Action<string> logAction)
        {
            return ResolveBackgroundMusicFile(settings, null, logAction);
        }

        private async Task<string> PrepareBackgroundMusicForRenderAsync(
            string sourceMusicPath,
            double targetDurationSeconds,
            AppSettings settings,
            string renderDir,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceMusicPath) || !File.Exists(sourceMusicPath))
            {
                return string.Empty;
            }

            var randomizeStart = settings?.VideoMusicRandomizeStartTime ?? true;
            if (!randomizeStart)
            {
                return sourceMusicPath;
            }

            var musicDuration = await GetAudioDurationSecondsAsync(sourceMusicPath, logAction, cancellationToken).ConfigureAwait(false);
            if (musicDuration <= 1d)
            {
                return sourceMusicPath;
            }

            var safeTarget = targetDurationSeconds > 1d ? targetDurationSeconds : 30d;
            var maxStart = Math.Max(0d, musicDuration - Math.Max(4d, safeTarget + 0.5d));
            var startAt = maxStart > 0.2d
                ? _random.NextDouble() * maxStart
                : 0d;
            var startText = startAt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var targetText = safeTarget.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var outPath = Path.Combine(renderDir, "bg_music_prepared.mp3");
            var args = $"-y -ss {startText} -i \"{sourceMusicPath}\" -t {targetText} -map_metadata -1 -c:a libmp3lame -q:a 2 \"{outPath}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            logAction?.Invoke($"AI Video Gen: randomize music start -> {Path.GetFileName(sourceMusicPath)} @ {startText}s");
            return File.Exists(outPath) ? outPath : sourceMusicPath;
        }

        private static List<double> AllocateSceneDurations(double narrationDurationSeconds, IList<double> weights)
        {
            var output = new List<double>();
            var sceneCount = weights?.Count ?? 0;
            if (sceneCount <= 0)
            {
                return output;
            }

            var total = narrationDurationSeconds > 1d ? narrationDurationSeconds : 36d;
            if (total < 16d)
            {
                total = 16d;
            }

            var normalizedWeights = new List<double>();
            var weightSum = 0d;
            for (var i = 0; i < sceneCount; i++)
            {
                var w = weights[i];
                if (w <= 0d)
                {
                    w = 1d;
                }

                normalizedWeights.Add(w);
                weightSum += w;
            }

            var minScene = 3.5d;
            var remaining = total;
            for (var i = 0; i < sceneCount; i++)
            {
                var raw = total * (normalizedWeights[i] / Math.Max(0.0001d, weightSum));
                var allocated = Math.Max(minScene, raw);
                output.Add(allocated);
                remaining -= allocated;
            }

            if (Math.Abs(remaining) > 0.01d)
            {
                var delta = remaining / sceneCount;
                for (var i = 0; i < output.Count; i++)
                {
                    output[i] = Math.Max(minScene, output[i] + delta);
                }
            }

            var rounded = output.Select(x => Math.Round(x, 2)).ToList();
            var roundedSum = rounded.Sum();
            var correction = Math.Round(total - roundedSum, 2);
            if (Math.Abs(correction) >= 0.01d && rounded.Count > 0)
            {
                rounded[rounded.Count - 1] = Math.Max(minScene, Math.Round(rounded[rounded.Count - 1] + correction, 2));
            }

            return rounded;
        }

        private static ShowcaseZoomAspectFitMode ResolveClipAspectFitMode(
            IList<AiVideoGenInputItem> showcaseOrderedScenes,
            int clipIndex)
        {
            if (showcaseOrderedScenes == null || clipIndex < 0 || clipIndex >= showcaseOrderedScenes.Count)
            {
                return ShowcaseZoomAspectFitMode.Crop;
            }

            return showcaseOrderedScenes[clipIndex]?.ShowcaseClipAspectFitMode ?? ShowcaseZoomAspectFitMode.Crop;
        }

        private static string BuildKenBurnsFilter(
            double clipDurationSeconds,
            int clipIndex,
            string openingHookText,
            ShowcaseOutputAspectPreset canvas = null,
            ShowcaseZoomAspectFitMode aspectFitMode = ShowcaseZoomAspectFitMode.Crop)
        {
            canvas = canvas ?? ShowcaseOutputAspectPresets.Vertical9x16;
            var w = canvas.Width;
            var h = canvas.Height;
            var wText = w.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            var hText = h.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

            var safeDuration = Math.Max(2.0d, clipDurationSeconds);
            var durationText = safeDuration.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var delta = 0.05d + ((clipIndex % 3) * 0.02d);
            var deltaText = delta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var zoomIn = clipIndex % 2 == 0;
            var scaleExpr = zoomIn
                ? $"{wText}*(1+{deltaText}*min(1\\,max(0\\,t/{durationText})))"
                : $"{wText}*(1+{deltaText}*(1-min(1\\,max(0\\,t/{durationText}))))";
            var scaleExprY = zoomIn
                ? $"{hText}*(1+{deltaText}*min(1\\,max(0\\,t/{durationText})))"
                : $"{hText}*(1+{deltaText}*(1-min(1\\,max(0\\,t/{durationText}))))";
            var panX = $"(in_w-out_w)/2 + ((in_w-out_w)/8)*sin(2*PI*t/{durationText})";
            var panY = $"(in_h-out_h)/2 + ((in_h-out_h)/10)*cos(2*PI*t/{durationText})";
            var kenBurns = $"scale='{scaleExpr}':'{scaleExprY}':eval=frame," +
                           $"crop={wText}:{hText}:x='{panX}':y='{panY}',setsar=1";
            string baseFilter;
            if (aspectFitMode == ShowcaseZoomAspectFitMode.BlurPad)
            {
                baseFilter = ShowcaseZoomAspectFitHelper.BuildBlurPadCompositePrefix(w, h) + kenBurns;
            }
            else
            {
                baseFilter = ShowcaseOutputAspectPresets.FormatScaleIncrease(w, h) + "," +
                             ShowcaseOutputAspectPresets.FormatScaleCrop(w, h) + ",setsar=1," +
                             kenBurns;
            }

            if (clipIndex != 0)
            {
                return baseFilter;
            }

            if (string.IsNullOrWhiteSpace(openingHookText))
            {
                return baseFilter + ",eq=saturation=1.10";
            }

            var safeHook = EscapeDrawText(openingHookText.Trim());
            return baseFilter +
                   ",eq=saturation=1.10" +
                   ",drawbox=x=40:y=(ih/2)-130:w=iw-80:h=260:color=black@0.35:t=fill:enable='between(t,0,3)'" +
                   ",drawtext=font='Segoe UI Bold':text='" + safeHook + "':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=76:fontcolor=white:borderw=5:bordercolor=black:enable='between(t,0,3)'";
        }

        private string ResolveOpeningHookText()
        {
            var hooks = new[]
            {
                "Bi mat ma shop khong muon ban biet...",
                "Ban dang bo lo deal lon nhat hom nay!",
                "Xem 3 giay nay truoc khi mua bat ky mon nao!"
            };

            return hooks[_random.Next(hooks.Length)];
        }

        private static async Task BuildSmoothTransitionVideoVariableAsync(
            IList<string> clipFiles,
            string outputFile,
            double transitionDurationSeconds,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (clipFiles == null || clipFiles.Count == 0)
            {
                throw new InvalidOperationException("No clips to stitch.");
            }

            var clipsHaveAudio = true;
            for (var i = 0; i < clipFiles.Count; i++)
            {
                if (!await HasAudioStreamAsync(clipFiles[i], logAction, cancellationToken).ConfigureAwait(false))
                {
                    clipsHaveAudio = false;
                    break;
                }
            }

            if (!clipsHaveAudio)
            {
                logAction?.Invoke("[Showcase] Clip không đồng nhất audio — ghép cảnh chỉ video (audio gắn sau).");
                await BuildSmoothTransitionVideoOnlyAsync(
                        clipFiles,
                        outputFile,
                        transitionDurationSeconds,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (clipFiles.Count == 1)
            {
                var singleArgs = $"-y -i \"{clipFiles[0]}\" -c:v libx264 -preset slow -crf 14 -b:v 18M -maxrate 24M -bufsize 48M -pix_fmt yuv420p -c:a aac -b:a 320k \"{outputFile}\"";
                await RunFfmpegAsync(singleArgs, logAction, cancellationToken).ConfigureAwait(false);
                return;
            }

            var durations = new List<double>();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var d = await GetVideoDurationSecondsAsync(clipFiles[i], logAction, cancellationToken).ConfigureAwait(false);
                durations.Add(Math.Max(2.0d, d));
            }

            var inputBuilder = new StringBuilder();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                inputBuilder.Append("-i \"").Append(clipFiles[i]).Append("\" ");
            }

            var transitionName = new[] { "fade", "hblur" }[DateTime.Now.Millisecond % 2];
            var filter = new StringBuilder();
            var currentVideo = "[0:v]";
            var currentAudio = "[0:a]";
            var timeline = durations[0];
            for (var i = 1; i < clipFiles.Count; i++)
            {
                var outV = i == clipFiles.Count - 1 ? "[vout]" : $"[vx{i}]";
                var outA = i == clipFiles.Count - 1 ? "[aout]" : $"[ax{i}]";
                var offset = Math.Max(0.1d, timeline - transitionDurationSeconds);
                filter.Append(currentVideo)
                      .Append("[").Append(i).Append(":v]")
                      .Append("xfade=transition=").Append(transitionName)
                      .Append(":duration=").Append(transitionDurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(":offset=").Append(offset.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(outV).Append(";");
                filter.Append(currentAudio)
                      .Append("[").Append(i).Append(":a]")
                      .Append("acrossfade=d=").Append(transitionDurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(outA).Append(";");
                currentVideo = outV;
                currentAudio = outA;
                timeline += durations[i] - transitionDurationSeconds;
            }

            var args = $"-y {inputBuilder}-filter_complex \"{filter}\" -map \"[vout]\" -map \"[aout]\" -r 30 -c:v libx264 -preset slow -crf 14 -b:v 18M -maxrate 24M -bufsize 48M -pix_fmt yuv420p -c:a aac -b:a 320k \"{outputFile}\"";
            try
            {
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Fallback to fade if hblur is unavailable on current ffmpeg build.
                var fallbackArgs = args.Replace("transition=hblur", "transition=fade");
                await RunFfmpegAsync(fallbackArgs, logAction, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task BuildSmoothTransitionVideoOnlyAsync(
            IList<string> clipFiles,
            string outputFile,
            double transitionDurationSeconds,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (clipFiles.Count == 1)
            {
                var singleArgs = $"-y -i \"{clipFiles[0]}\" -c:v libx264 -preset slow -crf 14 -b:v 18M -maxrate 24M -bufsize 48M -pix_fmt yuv420p -an \"{outputFile}\"";
                await RunFfmpegAsync(singleArgs, logAction, cancellationToken).ConfigureAwait(false);
                return;
            }

            var durations = new List<double>();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var d = await GetVideoDurationSecondsAsync(clipFiles[i], logAction, cancellationToken).ConfigureAwait(false);
                durations.Add(Math.Max(2.0d, d));
            }

            var inputBuilder = new StringBuilder();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                inputBuilder.Append("-i \"").Append(clipFiles[i]).Append("\" ");
            }

            var transitionName = new[] { "fade", "hblur" }[DateTime.Now.Millisecond % 2];
            var filter = new StringBuilder();
            var currentVideo = "[0:v]";
            var timeline = durations[0];
            for (var i = 1; i < clipFiles.Count; i++)
            {
                var outV = i == clipFiles.Count - 1 ? "[vout]" : $"[vx{i}]";
                var offset = Math.Max(0.1d, timeline - transitionDurationSeconds);
                filter.Append(currentVideo)
                      .Append("[").Append(i).Append(":v]")
                      .Append("xfade=transition=").Append(transitionName)
                      .Append(":duration=").Append(transitionDurationSeconds.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append(":offset=").Append(offset.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append(outV).Append(";");
                currentVideo = outV;
                timeline += durations[i] - transitionDurationSeconds;
            }

            var args = $"-y {inputBuilder}-filter_complex \"{filter}\" -map \"[vout]\" -an -r 30 -c:v libx264 -preset slow -crf 14 -b:v 18M -maxrate 24M -bufsize 48M -pix_fmt yuv420p \"{outputFile}\"";
            try
            {
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                var fallbackArgs = args.Replace("transition=hblur", "transition=fade");
                await RunFfmpegAsync(fallbackArgs, logAction, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<double> GetVideoDurationSecondsAsync(string videoPath, Action<string> logAction, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return 0d;
            }

            var ffprobeExecutable = ResolveFfprobeExecutablePath();
            var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobeExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 80).ConfigureAwait(false);

                var output = (await outputTask.ConfigureAwait(false) ?? string.Empty).Trim();
                var errors = (await errorTask.ConfigureAwait(false) ?? string.Empty).Trim();
                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(errors))
                {
                    logAction?.Invoke("AI Video Gen: ffprobe video-duration warning -> " + errors);
                }

                if (double.TryParse(output, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                {
                    return Math.Max(0d, seconds);
                }
            }

            return 0d;
        }

        private async Task<List<CaptionSegment>> BuildCaptionTimelineWithGeminiAsync(
            string script,
            double audioDurationSeconds,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var prompt = "Trích xuất timestamp phụ đề cho voice-over tiếng Việt dưới đây. " +
                         "Tổng thời lượng audio là " + audioDurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " giây. " +
                         "Trả về tối đa 24 dòng, mỗi dòng đúng định dạng: start|end|text . " +
                         "start/end là giây dạng số thập phân, text là cụm ngắn 3-8 từ. " +
                         "Không markdown, không giải thích. Script: " + script;
            try
            {
                var raw = await _geminiService.GenerateScriptAsync(
                    prompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    cancellationToken).ConfigureAwait(false);
                var parsed = ParseCaptionSegments(raw, audioDurationSeconds);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("AI Caption: Gemini timeline failed, fallback heuristic -> " + ex.Message);
            }

            return BuildHeuristicCaptionSegments(script, audioDurationSeconds);
        }

        private static List<CaptionSegment> ParseCaptionSegments(string raw, double audioDurationSeconds)
        {
            var output = new List<CaptionSegment>();
            var lines = (raw ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(30)
                .ToList();
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var start))
                {
                    continue;
                }

                if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var end))
                {
                    continue;
                }

                var text = parts[2].Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                start = Math.Max(0d, Math.Min(audioDurationSeconds, start));
                end = Math.Max(start + 0.1d, Math.Min(audioDurationSeconds, end));
                if (end - start < 0.1d)
                {
                    continue;
                }

                output.Add(new CaptionSegment
                {
                    Start = Math.Round(start, 2),
                    End = Math.Round(end, 2),
                    Text = text
                });
            }

            return output.OrderBy(x => x.Start).ToList();
        }

        private static List<CaptionSegment> BuildHeuristicCaptionSegments(string script, double audioDurationSeconds)
        {
            var phrases = Regex.Split(script ?? string.Empty, @"(?<=[\.\!\?,;:])\s+")
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (phrases.Count == 0)
            {
                return new List<CaptionSegment>();
            }

            var capped = phrases.Take(24).ToList();
            var weightSum = capped.Sum(x => Math.Max(1, x.Length));
            var cursor = 0d;
            var result = new List<CaptionSegment>();
            for (var i = 0; i < capped.Count; i++)
            {
                var lenWeight = Math.Max(1, capped[i].Length);
                var dur = audioDurationSeconds * (lenWeight / Math.Max(1d, weightSum));
                dur = Math.Max(0.8d, dur);
                var start = cursor;
                var end = Math.Min(audioDurationSeconds, start + dur);
                if (i == capped.Count - 1)
                {
                    end = audioDurationSeconds;
                }

                result.Add(new CaptionSegment
                {
                    Start = Math.Round(start, 2),
                    End = Math.Round(Math.Max(start + 0.2d, end), 2),
                    Text = capped[i]
                });
                cursor = end;
            }

            return result;
        }

        private static async Task<bool> HasAudioStreamAsync(string mediaPath, Action<string> logAction, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
            {
                return false;
            }

            var ffprobeExecutable = ResolveFfprobeExecutablePath();
            var args = $"-v error -select_streams a -show_entries stream=codec_type -of default=noprint_wrappers=1:nokey=1 \"{mediaPath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobeExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 80).ConfigureAwait(false);

                var output = (await outputTask.ConfigureAwait(false) ?? string.Empty).Trim();
                var errors = (await errorTask.ConfigureAwait(false) ?? string.Empty).Trim();
                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(errors))
                {
                    logAction?.Invoke("AI Video Gen: ffprobe audio-check warning -> " + errors);
                }

                return output.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private async Task<string> BuildAffiliateNarrationScriptAsync(
            IList<AiVideoGenInputItem> selectedItems,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var first = selectedItems != null && selectedItems.Count > 0 ? selectedItems[0] : null;
            var name = first?.ProductName ?? "sản phẩm nổi bật";
            var price = first?.Price ?? string.Empty;
            var style = GeminiStyleTemplateExtensions.Parse(settings.GeminiStyleTemplate);
            var text = await _geminiService.GenerateAffiliateExperienceScriptAsync(
                "deep",
                selectedItems,
                first,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken,
                style).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text)
                ? $"Đây là {name}, lựa chọn đáng cân nhắc cho nhu cầu hằng ngày. Trải nghiệm thực tế cho thấy sản phẩm mang lại hiệu quả rõ rệt và tiết kiệm thời gian. Nếu bạn đang tìm một giải pháp tiện lợi, hãy thử ngay hôm nay."
                : Regex.Replace(text.Trim(), "\\s+", " ");
        }

        private static string AdaptNarrationForMascotGender(string channelTheme, string narration)
        {
            var theme = (channelTheme ?? string.Empty).ToLowerInvariant();
            var voiceHint = "Giọng đọc tiếng Việt tự nhiên, truyền cảm, trung tính.";
            if (theme.Contains("nam") || theme.Contains("boy") || theme.Contains("male"))
            {
                voiceHint = "Giọng đọc tiếng Việt nam, truyền cảm, ấm áp và rõ chữ.";
            }
            else if (theme.Contains("nữ") || theme.Contains("nu") || theme.Contains("girl") || theme.Contains("female"))
            {
                voiceHint = "Giọng đọc tiếng Việt nữ, truyền cảm, thân thiện và rõ chữ.";
            }

            return $"{voiceHint} {narration}".Trim();
        }

        private async Task GenerateNarrationWithTtsStrictAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken,
            bool useMultiVoiceNarration = false,
            string workDirectory = null)
        {
            var prepared = text;
            if (VideoService.IsElevenLabsEndpoint(settings?.TtsEndpoint))
            {
                prepared = await VietnameseTtsTextNormalizer.PrepareNarrationForElevenLabsAsync(
                    text,
                    settings,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
                if (!string.Equals(prepared, (text ?? string.Empty).Trim(), StringComparison.Ordinal))
                {
                    logAction?.Invoke("[TTS] Đã chỉnh dấu/câu script trước ElevenLabs.");
                }
            }

            await _affiliateNarrationService.GenerateNarrationAsync(
                prepared,
                settings,
                outputAudioFile,
                useMultiVoiceNarration,
                workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".",
                logAction,
                cancellationToken).ConfigureAwait(false);
        }

        private static string NormalizeName(string value)
        {
            return Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), "\\s+", " ");
        }

        private VisualVariant BuildVisualVariant()
        {
            var positions = new[] { "top", "middle", "bottom" };
            var transitions = new[] { "fade", "wipeleft", "wiperight", "slideleft", "slideright", "circleopen", "smoothleft" };
            return new VisualVariant
            {
                TextPosition = positions[_random.Next(positions.Length)],
                Transition = transitions[_random.Next(transitions.Length)],
                BrightnessDelta = (_random.Next(0, 5) - 2) * 0.01d, // -0.02..+0.02
                Contrast = 0.98d + (_random.Next(0, 8) * 0.01d) // 0.98..1.05
            };
        }

        private static int ResolveMainTextY(string textPosition)
        {
            if (string.Equals(textPosition, "top", StringComparison.OrdinalIgnoreCase))
            {
                return 280;
            }

            if (string.Equals(textPosition, "middle", StringComparison.OrdinalIgnoreCase))
            {
                return 910;
            }

            return 1570;
        }

        private class DownloadedProductImage
        {
            public string ImagePath { get; set; } = string.Empty;
            public string SourceImageUrl { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string Price { get; set; } = string.Empty;
        }

        private class VisualVariant
        {
            public string TextPosition { get; set; } = "bottom";
            public string Transition { get; set; } = "fade";
            public double BrightnessDelta { get; set; }
            public double Contrast { get; set; } = 1.0d;
        }

        private class CaptionSegment
        {
            public double Start { get; set; }
            public double End { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        private class RenderFingerprintVariant
        {
            public string FrameRateText { get; set; } = "30.00";
            public int AudioSampleRate { get; set; } = 48000;
            public string ColorHex { get; set; } = "0xF7EFE1";
        }

        private async Task<string> ApplyBasicLipsyncAsync(
            string videoPath,
            string narrationAudioPath,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)
                || string.IsNullOrWhiteSpace(narrationAudioPath) || !File.Exists(narrationAudioPath))
            {
                return videoPath;
            }

            if (!FfmpegToolkitService.TryResolve(settings, out _, out _))
            {
                return videoPath;
            }

            try
            {
                var dir = Path.GetDirectoryName(videoPath) ?? string.Empty;
                var outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(videoPath) + "_lipsync.mp4");
                var args =
                    $"-y -i \"{videoPath}\" -i \"{narrationAudioPath}\" -filter_complex \"[0:v]setpts=PTS-STARTPTS[v];[1:a]volume=1.0[a]\" -map \"[v]\" -map \"[a]\" -c:v libx264 -preset fast -pix_fmt yuv420p -c:a aac -shortest \"{outPath}\"";
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
                if (File.Exists(outPath) && new FileInfo(outPath).Length > 10_000L)
                {
                    logAction?.Invoke("Mascot: lipsync cơ bản (khớp voiceover) → " + outPath);
                    return outPath;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("Mascot: lipsync bỏ qua — " + ex.Message);
            }

            return videoPath;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AUTO-POST PRE-PROCESSING  (anti-duplicate fingerprint for TikTok)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-masters a video before uploading to TikTok to reduce the risk of
        /// "Unoriginal / Low-quality / QR code" policy rejection.
        ///
        /// Steps applied in a single FFmpeg pass:
        ///   1. Edge crop (default 3 %) + slight clockwise rotation (1.5°) via affine transform
        ///   2. Subtle brightness +2 % and saturation +5 % via eq/hue filters
        ///   3. All metadata stripped (-map_metadata -1)
        ///
        /// Returns the path of the remastered file (in the same folder, new random name).
        /// Throws if the source video is below 720 p (height).
        /// </summary>
        public async Task<string> RemasterForAutoPostAsync(
            string sourceVideoPath,
            Action<string> logAction,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceVideoPath) || !File.Exists(sourceVideoPath))
                throw new FileNotFoundException("Video file not found: " + sourceVideoPath);

            // ── 1. Probe resolution ──────────────────────────────────────────
            var (width, height) = await ProbeVideoResolutionAsync(sourceVideoPath, logAction, cancellationToken)
                .ConfigureAwait(false);

            if (height > 0 && height < 720)
                throw new InvalidOperationException(
                    $"Video độ phân giải quá thấp ({width}×{height}). TikTok yêu cầu tối thiểu 720p — không đăng để tránh bị đánh lỗi chất lượng.");

            logAction?.Invoke($"  Remaster: nguồn {width}×{height} — bắt đầu xử lý…");

            // ── 2. Build output path (random name, same folder, .mp4) ────────
            var dir      = Path.GetDirectoryName(sourceVideoPath) ?? ".";
            var outName  = Guid.NewGuid().ToString("N").Substring(0, 16) + ".mp4";
            var outPath  = Path.Combine(dir, outName);

            // ── 3. Randomise parameters slightly each run ────────────────────
            var rng        = new Random();
            double cropPct = 0.02 + rng.NextDouble() * 0.02;       // 2–4 %
            double rotDeg  = 0.8  + rng.NextDouble() * 1.4;        // 0.8–2.2 °
            double bright  = 0.01 + rng.NextDouble() * 0.02;       // +1–3 %
            double satMul  = 1.04 + rng.NextDouble() * 0.04;       // ×1.04–1.08

            // crop=iw*(1-2*c):ih*(1-2*c) then affine rotation (scale=1 to avoid black bands)
            // eq filter: brightness shift, saturation multiply
            var cropExpr  = $"crop=iw*(1-2*{cropPct:F4}):ih*(1-2*{cropPct:F4})";
            var rotExpr   = $"rotate={rotDeg:F4}*PI/180:c=black:ow=iw:oh=ih";
            var eqExpr    = $"eq=brightness={bright:F4}:saturation={satMul:F4}";
            var filterStr = $"{cropExpr},{rotExpr},{eqExpr},scale=trunc(iw/2)*2:trunc(ih/2)*2";

            // Minimal re-encode: libx264 fast, aac copy-through, strip all metadata
            var args = $"-y -i \"{sourceVideoPath}\" " +
                       $"-vf \"{filterStr}\" " +
                       $"-c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p " +
                       $"-c:a aac -b:a 128k " +
                       $"-map_metadata -1 " +
                       $"\"{outPath}\"";

            logAction?.Invoke($"  Remaster: crop={cropPct*100:F1}% rot={rotDeg:F2}° bright=+{bright*100:F1}% sat×{satMul:F2}");
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(outPath) || new FileInfo(outPath).Length < 50_000L)
                throw new InvalidOperationException("Remaster output file missing or too small.");

            logAction?.Invoke($"  Remaster: ✓ → {outName}");
            return outPath;
        }

        /// <summary>
        /// Uses ffprobe to get the (width, height) of a video in pixels.
        /// Returns (0, 0) on failure.
        /// </summary>
        private static async Task<(int width, int height)> ProbeVideoResolutionAsync(
            string videoPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            try
            {
                var ffprobe = ResolveFfprobeExecutablePath();
                var args    = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"{videoPath}\"";
                var psi     = new ProcessStartInfo
                {
                    FileName               = ffprobe,
                    Arguments              = args,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };
                using (var proc = new Process { StartInfo = psi })
                {
                    proc.Start();
                    var outTask = proc.StandardOutput.ReadToEndAsync();
                    var errTask = proc.StandardError.ReadToEndAsync();
                    await ProcessCancellationHelper.WaitUntilExitAsync(proc, cancellationToken, 30)
                        .ConfigureAwait(false);
                    var text = (await outTask.ConfigureAwait(false)).Trim();
                    var parts = text.Split(',');
                    if (parts.Length >= 2 &&
                        int.TryParse(parts[0], out var w) &&
                        int.TryParse(parts[1], out var h))
                        return (w, h);
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("  Remaster probe lỗi: " + ex.Message);
            }
            return (0, 0);
        }
    }

    public class VideoRenderProgress
    {
        public int Slot { get; set; }
        public int VideoIndex { get; set; }
        public int TotalVideos { get; set; }
        public int Percent { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }

    public class AffiliateVideoPipelineResult
    {
        public string FinalVideoPath { get; set; } = string.Empty;
        public List<AffiliateSceneAsset> SceneVideos { get; set; } = new List<AffiliateSceneAsset>();
    }

    public class AffiliateSceneAsset
    {
        public int Index { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SourceImagePath { get; set; } = string.Empty;
        public string AiImagePath { get; set; } = string.Empty;
        public string MotionPrompt { get; set; } = string.Empty;
        public string SceneVideoPath { get; set; } = string.Empty;
    }

    public class MascotChannelVideoPipelineResult
    {
        public string FinalVideoPath { get; set; } = string.Empty;
        public List<MascotSceneAsset> Scenes { get; set; } = new List<MascotSceneAsset>();
        public string ProfileName { get; set; } = string.Empty;
        public string ChannelTheme { get; set; } = string.Empty;
    }

    public class MascotSceneAsset
    {
        public int Index { get; set; }
        public string SceneScript { get; set; } = string.Empty;
        public string VariantImagePath { get; set; } = string.Empty;
        public string MotionPrompt { get; set; } = string.Empty;
        public string SceneVideoPath { get; set; } = string.Empty;
    }

    public class MascotVariantPreviewResult
    {
        public List<string> SceneScripts { get; set; } = new List<string>();
        public List<string> PreviewImagePaths { get; set; } = new List<string>();
    }

    public class MascotSceneRegenerateResult
    {
        public string PreviewImagePath { get; set; } = string.Empty;
        public string MotionPrompt { get; set; } = string.Empty;
        public string SceneScript { get; set; } = string.Empty;
    }
}
