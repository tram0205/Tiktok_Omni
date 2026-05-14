using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace tiktok_Omni.Services
{
    public class VideoProcessingService
    {
        private readonly VideoService _videoService = new VideoService();
        private readonly GeminiService _geminiService = new GeminiService();
        private readonly Random _random = new Random();

        public async Task<List<string>> GenerateProductVideosAsync(
            IList<AiVideoGenInputItem> items,
            string script,
            IList<string> perItemScripts,
            AppSettings settings,
            Action<string> logAction,
            Action<VideoRenderProgress> progressAction,
            CancellationToken cancellationToken)
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

                        var output = await GenerateProductVideoAsync(
                            single,
                            itemScript,
                            settings,
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
                            }).ConfigureAwait(false);

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
                logAction,
                progressAction,
                cancellationToken);
        }

        public async Task<AffiliateVideoPipelineResult> GenerateAffiliateProductVideoAsync(
            IList<AiVideoGenInputItem> items,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null)
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

            var baseDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "generated_videos",
                "affiliate_pipeline",
                DateTime.Now.ToString("yyyyMMdd"),
                DateTime.Now.ToString("HHmmss"));
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
            await GenerateNarrationWithLyriaStrictAsync(narrationText, settings, narrationFile, logAction, cancellationToken).ConfigureAwait(false);
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
                var shotHint = ResolveShotHint(i);
                logAction?.Invoke($"Affiliate Pipeline: processing scene {i + 1}/4 ({shotHint})...");
                var imageProgress = 5 + (int)Math.Round(((i + 1) / 4d) * 35d);
                progressCallback?.Invoke(imageProgress, "Tạo ảnh");

                var redrawPrompt = BuildAffiliateRedrawPrompt(row.ProductName, row.Price, shotHint);
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
                    shotHint,
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
            await RenderVeoVerticalVideoAsync(
                sceneAssets.Select(x => x.SceneVideoPath).ToList(),
                finalOutput,
                narrationFile,
                narrationText,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
            progressCallback?.Invoke(100, "Render");

            return new AffiliateVideoPipelineResult
            {
                FinalVideoPath = finalOutput,
                SceneVideos = sceneAssets
            };
        }

        public async Task<MascotChannelVideoPipelineResult> GenerateMascotChannelVideoAsync(
            string mascotImagePath,
            string channelTheme,
            IList<string> identityImagePaths,
            int sceneCount,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null)
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

            var baseDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "generated_videos",
                "mascot_pipeline",
                DateTime.Now.ToString("yyyyMMdd"),
                DateTime.Now.ToString("HHmmss"));
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
            var sceneScripts = await BuildMascotSceneScriptsAsync(channelTheme, sceneCount, settings, cancellationToken).ConfigureAwait(false);
            var narrationFile = Path.Combine(audioDir, "narration.mp3");
            var narrationText = string.Join(" ", sceneScripts);
            await GenerateNarrationWithLyriaStrictAsync(
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

            var sceneAssets = new List<MascotSceneAsset>();
            progressCallback?.Invoke(5, "Tạo ảnh");
            for (var i = 0; i < sceneScripts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sceneText = sceneScripts[i];
                logAction?.Invoke($"Mascot Pipeline: creating scene {i + 1}/{sceneScripts.Count}...");
                var imageProgress = 5 + (int)Math.Round(((i + 1) / (double)sceneScripts.Count) * 35d);
                progressCallback?.Invoke(imageProgress, "Tạo ảnh");

                var variantPrompt = BuildMascotVariantImagePrompt(channelTheme, sceneText);
                var variantImageUrl = await _videoService.GenerateContextImageAsync(
                    mascotDataUrl,
                    variantPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    identityDataUrls,
                    cancellationToken).ConfigureAwait(false);
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
                    cancellationToken).ConfigureAwait(false);
                var clipUrl = await _videoService.GenerateVideoFromImageAsync(
                    variantImageUrl,
                    motionPrompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    sceneDurations[i],
                    cancellationToken).ConfigureAwait(false);
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

            var finalOutput = Path.Combine(baseDir, $"mascot_story_{DateTime.Now:HHmmss}.mp4");
            progressCallback?.Invoke(85, "Render");
            await RenderVeoVerticalVideoAsync(
                sceneAssets.Select(x => x.SceneVideoPath).ToList(),
                finalOutput,
                narrationFile,
                narrationText,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
            progressCallback?.Invoke(100, "Render");

            return new MascotChannelVideoPipelineResult
            {
                FinalVideoPath = finalOutput,
                Scenes = sceneAssets
            };
        }

        public async Task<MascotVariantPreviewResult> GenerateMascotVariantPreviewAsync(
            string mascotImagePath,
            string channelTheme,
            IList<string> identityImagePaths,
            int sceneCount,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
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
            var sceneScripts = await BuildMascotSceneScriptsAsync(channelTheme, sceneCount, settings, cancellationToken).ConfigureAwait(false);

            var previews = new List<string>();
            var previewCount = Math.Min(4, sceneScripts.Count);
            for (var i = 0; i < previewCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var variantPrompt = BuildMascotVariantImagePrompt(channelTheme, sceneScripts[i]);
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
            CancellationToken cancellationToken)
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

            var mascotDataUrl = BuildImageDataUrl(mascotImagePath);
            var identityDataUrls = validIdentityImages.Select(BuildImageDataUrl).ToList();
            var variantPrompt = BuildMascotVariantImagePrompt(channelTheme, sceneScript);
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
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<int, string> progressCallback = null)
        {
            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("No product items found for rendering.");
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                throw new InvalidOperationException("Script is empty. Please generate script first.");
            }

            var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "generated_videos", DateTime.Now.ToString("yyyyMMdd"), DateTime.Now.ToString("HHmmss"));
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
            await GenerateNarrationAudioAsync(script, settings, baseDir, audioFile, logAction, cancellationToken).ConfigureAwait(false);
            logAction?.Invoke("AI Video Gen: narration saved -> " + audioFile);
            var audioDuration = await GetAudioDurationSecondsAsync(audioFile, logAction, cancellationToken).ConfigureAwait(false);
            var perImageDuration = ResolvePerImageDuration(audioDuration, downloadedImages.Count);
            var scriptOverlay = BuildScriptOverlayText(script);
            var transitionDuration = ResolveTransitionDuration(settings);
            var textSize = ResolveTextSize(settings);
            var musicVolume = ResolveMusicVolume(settings);
            var visualVariant = BuildVisualVariant();
            logAction?.Invoke($"AI Video Gen: target image duration ~{perImageDuration:0.00}s per product.");
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
                await RenderImageClipAsync(
                    item.ImagePath,
                    clipPath,
                    item?.ProductName ?? string.Empty,
                    item?.Price ?? string.Empty,
                    scriptOverlay,
                    perImageDuration,
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
            await BuildTransitionVideoAsync(clipFiles, slideshowFile, perImageDuration, transitionDuration, visualVariant.Transition, logAction, cancellationToken).ConfigureAwait(false);

            var outputFile = Path.Combine(baseDir, $"product_video_{DateTime.Now:HHmmss}.mp4");
            var backgroundMusic = ResolveBackgroundMusicFile();
            string preparedBackgroundMusic = null;
            if (!string.IsNullOrWhiteSpace(backgroundMusic))
            {
                logAction?.Invoke("AI Video Gen: adding royalty-free background music -> " + Path.GetFileName(backgroundMusic));
                preparedBackgroundMusic = Path.Combine(baseDir, "music_prepared.mp3");
                progressCallback?.Invoke(88, "Preparing background music");
                await PrepareBackgroundMusicTrackAsync(backgroundMusic, preparedBackgroundMusic, audioDuration, logAction, cancellationToken).ConfigureAwait(false);
            }

            logAction?.Invoke("AI Video Gen: merging clips + voice + music (1080x1920)...");
            progressCallback?.Invoke(95, "Final merge");
            var concatArgs = BuildFinalRenderArgs(slideshowFile, audioFile, preparedBackgroundMusic, outputFile, musicVolume);
            await RunFfmpegAsync(concatArgs, logAction, cancellationToken).ConfigureAwait(false);
            progressCallback?.Invoke(100, "Completed");

            return outputFile;
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
            var vf =
                "scale=1080:1920:force_original_aspect_ratio=decrease," +
                "pad=1080:1920:(ow-iw)/2:(oh-ih)/2," +
                "eq=brightness=" + brightnessDelta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ":contrast=" + contrast.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                "zoompan=z='min(zoom+0.0009,1.16)':d=" + Math.Max(1, (int)Math.Round(clipDurationSeconds * 30d)) + ":s=1080x1920:fps=30," +
                "fade=t=in:st=0:d=0.35," +
                "fade=t=out:st=" + Math.Max(0d, clipDurationSeconds - 0.45d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ":d=0.45," +
                textBar + "," +
                textMain +
                textSub;
            var clipDurationText = clipDurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var args = $"-y -loop 1 -i \"{imagePath}\" -t {clipDurationText} -vf \"{vf}\" -r 30 -map_metadata -1 -c:v libx264 -preset medium -pix_fmt yuv420p \"{clipPath}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task BuildTransitionVideoAsync(
            IList<string> clipFiles,
            string outputFile,
            double clipDurationSeconds,
            double transitionDurationSeconds,
            string transitionName,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (clipFiles == null || clipFiles.Count == 0)
            {
                throw new InvalidOperationException("No clip files to merge.");
            }

            if (clipFiles.Count == 1)
            {
                var singleArgs = $"-y -i \"{clipFiles[0]}\" -c:v libx264 -preset medium -pix_fmt yuv420p \"{outputFile}\"";
                await RunFfmpegAsync(singleArgs, logAction, cancellationToken).ConfigureAwait(false);
                return;
            }

            var inputBuilder = new StringBuilder();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                inputBuilder.Append("-i \"").Append(clipFiles[i]).Append("\" ");
            }

            var filter = new StringBuilder();
            var currentLabel = "[0:v]";
            var timeline = clipDurationSeconds;
            for (var i = 1; i < clipFiles.Count; i++)
            {
                var outputLabel = i == clipFiles.Count - 1 ? "[vout]" : $"[vx{i}]";
                var offset = timeline - transitionDurationSeconds;
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
                timeline += clipDurationSeconds - transitionDurationSeconds;
            }

            var args = $"-y {inputBuilder}-filter_complex \"{filter}\" -map \"[vout]\" -r 30 -c:v libx264 -preset medium -pix_fmt yuv420p \"{outputFile}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task SanitizeImageAsync(string inputPath, string outputPath, Action<string> logAction, CancellationToken cancellationToken)
        {
            var args = $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -pix_fmt yuv420p \"{outputPath}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task PrepareBackgroundMusicTrackAsync(
            string sourceMusicPath,
            string outputPath,
            double targetDurationSeconds,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var safeTarget = Math.Max(8d, targetDurationSeconds);
            var safeTargetText = safeTarget.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var fadeStart = Math.Max(0d, safeTarget - 1.2d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var args = $"-y -stream_loop -1 -i \"{sourceMusicPath}\" -t {safeTargetText} -af \"afade=t=in:st=0:d=0.7,afade=t=out:st={fadeStart}:d=1.2\" -map_metadata -1 -c:a libmp3lame -q:a 2 \"{outputPath}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task RunFfmpegAsync(string args, Action<string> logAction, CancellationToken cancellationToken)
        {
            var ffmpegExecutable = ResolveFfmpegExecutablePath();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var stdOut = process.StandardOutput.ReadToEndAsync();
                var stdErr = process.StandardError.ReadToEndAsync();

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }

                var outText = await stdOut.ConfigureAwait(false);
                var errText = await stdErr.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("FFmpeg failed: " + (string.IsNullOrWhiteSpace(errText) ? outText : errText));
                }

                if (!string.IsNullOrWhiteSpace(errText))
                {
                    var line = errText.Split('\n').LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logAction?.Invoke("FFmpeg: " + line.Trim());
                    }
                }
            }
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

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                }

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
            CancellationToken cancellationToken)
        {
            Exception lastError = null;

            if (!string.IsNullOrWhiteSpace(settings?.LyriaApiKey) && !string.IsNullOrWhiteSpace(settings?.LyriaEndpoint))
            {
                try
                {
                    logAction?.Invoke("AI Video Gen: generating narration via Lyria...");
                    var audioUrl = await _videoService.GenerateAudioAsync(
                        script,
                        settings.LyriaApiKey,
                        settings.LyriaEndpoint,
                        cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(audioUrl))
                    {
                        await DownloadFileAsync(audioUrl, outputAudioFile, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    throw new InvalidOperationException("Lyria returned empty audioUrl.");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    logAction?.Invoke("AI Video Gen: Lyria failed, fallback to Google TTS. Reason: " + ex.Message);
                }
            }

            await GenerateGoogleTtsAudioAsync(script, baseDir, outputAudioFile, logAction, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(outputAudioFile))
            {
                throw new InvalidOperationException("Could not generate narration audio.", lastError);
            }
        }

        private async Task GenerateGoogleTtsAudioAsync(
            string script,
            string baseDir,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var segmentDir = Path.Combine(baseDir, "tts_segments");
            Directory.CreateDirectory(segmentDir);
            var chunks = SplitText(script, 180);
            var mp3Segments = new List<string>();

            for (var i = 0; i < chunks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = chunks[i];
                var ttsUrl =
                    "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=vi&q=" +
                    HttpUtility.UrlEncode(chunk);
                var segmentPath = Path.Combine(segmentDir, $"seg_{i + 1:D3}.mp3");
                await DownloadFileAsync(ttsUrl, segmentPath, cancellationToken).ConfigureAwait(false);
                mp3Segments.Add(segmentPath);
            }

            if (mp3Segments.Count == 0)
            {
                throw new InvalidOperationException("Google TTS fallback returned no audio segments.");
            }

            var listPath = Path.Combine(segmentDir, "segments.txt");
            var sb = new StringBuilder();
            foreach (var seg in mp3Segments)
            {
                sb.AppendLine("file '" + seg.Replace("'", "'\\''") + "'");
            }
            File.WriteAllText(listPath, sb.ToString());

            logAction?.Invoke("AI Video Gen: stitching Google TTS segments...");
            var args = $"-y -f concat -safe 0 -i \"{listPath}\" -c:a libmp3lame -q:a 2 \"{outputAudioFile}\"";
            await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
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
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background_music")
            };

            foreach (var dir in candidates)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                var files = Directory.GetFiles(dir, "*.mp3");
                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            return string.Empty;
        }

        private static string BuildFinalRenderArgs(string concatFile, string narrationFile, string backgroundMusicFile, string outputFile, double musicVolume)
        {
            if (string.IsNullOrWhiteSpace(backgroundMusicFile))
            {
                return $"-y -i \"{concatFile}\" -i \"{narrationFile}\" -map 0:v:0 -map 1:a:0 -vf scale=1080:1920:flags=lanczos -r 30 -c:v libx264 -preset medium -pix_fmt yuv420p -c:a aac -shortest \"{outputFile}\"";
            }

            var musicVolumeFilter = musicVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return $"-y -i \"{concatFile}\" -i \"{narrationFile}\" -stream_loop -1 -i \"{backgroundMusicFile}\" -filter_complex \"[1:a]volume=1.0[voice];[2:a]volume={musicVolumeFilter}[music];[voice][music]amix=inputs=2:duration=first:dropout_transition=2[aout]\" -map 0:v:0 -map \"[aout]\" -vf scale=1080:1920:flags=lanczos -r 30 -c:v libx264 -preset medium -pix_fmt yuv420p -c:a aac -shortest \"{outputFile}\"";
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

        private static double ResolveMusicVolume(AppSettings settings)
        {
            var percent = settings?.VideoMusicVolume ?? 14;
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
            string shotHint,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm" : productName.Trim();
            var priceText = string.IsNullOrWhiteSpace(price) ? "không nêu giá" : price.Trim();
            var prompt = "Viết một video prompt tiếng Việt ngắn (1-2 câu) cho Veo 3. " +
                         "Mô tả chuyển động vật lý chân thực của cảnh quay sản phẩm gồm: xoay nhẹ sản phẩm, ánh sáng lướt qua bề mặt, " +
                         "camera dolly chậm hoặc pan nhẹ, giữ chất liệu thật rõ nét. " +
                         "Không markdown, không số thứ tự. " +
                         $"Sản phẩm: {name}. Giá: {priceText}. Góc chụp: {shotHint}.";

            var generated = await _geminiService.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(generated)
                ? $"Quay cận cảnh {name}, camera pan nhẹ, ánh sáng studio lướt qua bề mặt sản phẩm, giữ chất liệu chân thực."
                : Regex.Replace(generated.Trim(), "\\s+", " ");
        }

        private static string ResolveShotHint(int index)
        {
            var hints = new[]
            {
                "hero shot chính diện",
                "góc 3/4 nhấn chất liệu",
                "cận cảnh chi tiết sản phẩm",
                "góc lifestyle sang trọng"
            };

            if (index < 0 || index >= hints.Length)
            {
                return "góc đa dụng";
            }

            return hints[index];
        }

        private static string BuildAffiliateRedrawPrompt(string productName, string price, string shotHint)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm gia dụng" : productName.Trim();
            var priceText = string.IsNullOrWhiteSpace(price) ? string.Empty : (" giá " + price.Trim());
            return "Phân tích sản phẩm, giữ nguyên form dáng và chất liệu. " +
                   "Vẽ lại ảnh mới theo phong cách thương mại cao cấp: nếu có người mẫu cũ thì thay bằng người mẫu xinh đẹp, tự nhiên; " +
                   "đặt sản phẩm vào bối cảnh sang trọng, hợp lý (studio premium, bàn đá cẩm thạch hoặc không gian hiện đại), " +
                   "đa dạng góc chụp theo chỉ dẫn. " +
                   "YÊU CẦU BẮT BUỘC: không làm sai tỷ lệ, không biến dạng, giữ đúng nhận diện thật của sản phẩm. " +
                   $"Sản phẩm: {name}{priceText}. Góc chụp: {shotHint}.";
        }

        private async Task<List<string>> BuildMascotSceneScriptsAsync(string channelTheme, int sceneCount, AppSettings settings, CancellationToken cancellationToken)
        {
            var safeSceneCount = sceneCount == 6 || sceneCount == 8 ? sceneCount : 4;
            var prompt = $"Viết kịch bản video storytelling gồm đúng {safeSceneCount} phân cảnh cho kênh TikTok. " +
                         "Chủ đề kênh: " + channelTheme.Trim() + ". " +
                         $"Trả về đúng {safeSceneCount} dòng, mỗi dòng bắt đầu bằng 'Cảnh i:'. " +
                         "Mỗi cảnh 1-2 câu ngắn, rõ hành động, bối cảnh, cảm xúc.";
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
                    "Cảnh 1: Mở đầu giới thiệu linh vật/người mẫu trong bối cảnh đời thường gần gũi.",
                    "Cảnh 2: Nhân vật chuyển sang bối cảnh nổi bật thể hiện cá tính của kênh.",
                    "Cảnh 3: Cận cảnh biểu cảm nhân vật, nhấn thông điệp chính của nội dung.",
                    "Cảnh 4: Kết thúc với góc quay ấn tượng và lời kêu gọi theo dõi kênh."
                };
                while (scenes.Count < safeSceneCount)
                {
                    var idx = scenes.Count + 1;
                    scenes.Add($"Cảnh {idx}: Nhân vật tiếp tục câu chuyện theo chủ đề kênh với góc quay mới.");
                }
            }

            return scenes;
        }

        private static string BuildMascotVariantImagePrompt(string channelTheme, string sceneScript)
        {
            return "Dựa trên ảnh gốc linh vật/người mẫu, tạo ảnh biến thể theo phân cảnh. " +
                   "Giữ NGUYÊN khuôn mặt/nhân vật nhận diện chính, không đổi đặc trưng gương mặt, không đổi danh tính. " +
                   "Chỉ thay đổi bối cảnh, tư thế, góc chụp theo nội dung cảnh. " +
                   "Phong cách điện ảnh, ánh sáng đẹp, bố cục rõ chủ thể. " +
                   "Chủ đề kênh: " + channelTheme.Trim() + ". " +
                   "Nội dung cảnh: " + sceneScript.Trim();
        }

        private async Task<string> BuildMascotMotionPromptAsync(
            string channelTheme,
            string sceneScript,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var prompt = "Viết một video prompt ngắn cho Veo 3 dựa trên cảnh sau, mô tả chuyển động camera và chuyển động vật lý tự nhiên. " +
                         "Không markdown, không giải thích. Chủ đề: " + channelTheme.Trim() + ". Cảnh: " + sceneScript.Trim();
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
            CancellationToken cancellationToken)
        {
            if (clipFiles == null || clipFiles.Count == 0)
            {
                throw new InvalidOperationException("No Veo clips found to render.");
            }

            var renderDir = Path.Combine(Path.GetDirectoryName(outputFile) ?? AppDomain.CurrentDomain.BaseDirectory, "render_work");
            Directory.CreateDirectory(renderDir);
            var normalizedClips = new List<string>();
            var hookText = ResolveOpeningHookText();
            for (var i = 0; i < clipFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = Path.Combine(renderDir, $"normalized_{i + 1:D2}.mp4");
                var sourceDuration = await GetVideoDurationSecondsAsync(clipFiles[i], logAction, cancellationToken).ConfigureAwait(false);
                var kenBurnsVf = BuildKenBurnsFilter(sourceDuration, i, hookText);
                var args = $"-y -i \"{clipFiles[i]}\" -vf \"{kenBurnsVf}\" -r 30 -c:v libx264 -preset slow -crf 14 -b:v 18M -maxrate 24M -bufsize 48M -pix_fmt yuv420p -c:a aac -b:a 320k -ar 48000 \"{normalized}\"";
                await RunFfmpegAsync(args, logAction, cancellationToken).ConfigureAwait(false);
                normalizedClips.Add(normalized);
            }

            var stitched = Path.Combine(renderDir, "stitched.mp4");
            await BuildSmoothTransitionVideoVariableAsync(
                normalizedClips,
                stitched,
                0.45d,
                logAction,
                cancellationToken).ConfigureAwait(false);

            var stitchedDuration = await GetVideoDurationSecondsAsync(stitched, logAction, cancellationToken).ConfigureAwait(false);
            var trendMusic = ResolveBackgroundMusicFile(settings, logAction);
            var preparedTrendMusic = await PrepareBackgroundMusicForRenderAsync(
                trendMusic,
                stitchedDuration,
                settings,
                renderDir,
                logAction,
                cancellationToken).ConfigureAwait(false);
            var hasAmbientAudio = await HasAudioStreamAsync(stitched, logAction, cancellationToken).ConfigureAwait(false);
            var fingerprintVariant = BuildRenderFingerprintVariant();
            var captionVideoFilter = await BuildDynamicCaptionFilterAsync(
                narrationScript,
                narrationTrackPath,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(preparedTrendMusic))
            {
                var noMusicArgs = BuildMetadataRenderArgs(stitched, outputFile, narrationTrackPath, null, null, hasAmbientAudio, captionVideoFilter, fingerprintVariant);
                await RunFfmpegAsync(noMusicArgs, logAction, cancellationToken).ConfigureAwait(false);
                return;
            }

            var musicVolume = (_random.Next(10, 16) / 100d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var withMusicArgs = BuildMetadataRenderArgs(stitched, outputFile, narrationTrackPath, preparedTrendMusic, musicVolume, hasAmbientAudio, captionVideoFilter, fingerprintVariant);
            await RunFfmpegAsync(withMusicArgs, logAction, cancellationToken).ConfigureAwait(false);
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
            if (string.IsNullOrWhiteSpace(musicPath))
            {
                if (hasNarration)
                {
                    if (hasAmbientAudio)
                    {
                        return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -filter_complex \"[0:a]volume=0.20[amb];[1:a]volume=1.0[narr];[amb][narr]amix=inputs=2:duration=first:dropout_transition=2[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
                    }

                    return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -filter_complex \"[1:a]volume=1.0[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
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
                    return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -stream_loop -1 -i \"{musicPath}\" -filter_complex \"[0:a]volume=0.20[amb];[1:a]volume=1.0[narr];[2:a]volume={musicVolume}[music];[amb][narr][music]amix=inputs=3:duration=first:dropout_transition=2[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
                }

                return $"-y -i \"{stitchedInput}\" -i \"{narrationPath}\" -stream_loop -1 -i \"{musicPath}\" -filter_complex \"[1:a]volume=1.0[narr];[2:a]volume={musicVolume}[music];[narr][music]amix=inputs=2:duration=first:dropout_transition=2[aout]\" -map 0:v:0 -map \"[aout]\"{vfArg} -map_metadata -1 -metadata title=\"{tagTitle}\" -metadata comment=\"{tagComment}\" -metadata artist=\"Omni Studio\" -metadata encoder=\"ffmpeg\" {qualityArgs} -shortest \"{outputFile}\"";
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
            var sampleRatePool = new[] { 44100, 48000, 47952 };
            var colorPool = new[] { "0xF7EFE1", "0xEEF7FF", "0xFFF3E8", "0xF1F0FF" };
            return new RenderFingerprintVariant
            {
                FrameRateText = fpsPool[_random.Next(fpsPool.Length)],
                AudioSampleRate = sampleRatePool[_random.Next(sampleRatePool.Length)],
                ColorHex = colorPool[_random.Next(colorPool.Length)]
            };
        }

        private string ResolveBackgroundMusicFile(AppSettings settings, Action<string> logAction)
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "music");
            if (!Directory.Exists(dir))
            {
                return string.Empty;
            }

            var files = Directory.GetFiles(dir, "*.mp3");
            if (files.Length == 0)
            {
                return string.Empty;
            }

            var selectedName = (settings?.VideoBackgroundMusicFileName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(selectedName))
            {
                var exact = files.FirstOrDefault(x => string.Equals(Path.GetFileName(x), selectedName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    logAction?.Invoke("AI Video Gen: selected background music -> " + Path.GetFileName(exact));
                    return exact;
                }
            }

            var picked = files[_random.Next(files.Length)];
            logAction?.Invoke("AI Video Gen: random background music -> " + Path.GetFileName(picked));
            return picked;
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

        private static string BuildKenBurnsFilter(double clipDurationSeconds, int clipIndex, string openingHookText)
        {
            var safeDuration = Math.Max(2.0d, clipDurationSeconds);
            var durationText = safeDuration.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var delta = 0.05d + ((clipIndex % 3) * 0.02d); // 5%, 7%, 9%
            var deltaText = delta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var zoomIn = clipIndex % 2 == 0;
            var scaleExpr = zoomIn
                ? $"1080*(1+{deltaText}*min(1\\,max(0\\,t/{durationText})))"
                : $"1080*(1+{deltaText}*(1-min(1\\,max(0\\,t/{durationText}))))";
            var panX = $"(in_w-out_w)/2 + ((in_w-out_w)/8)*sin(2*PI*t/{durationText})";
            var panY = $"(in_h-out_h)/2 + ((in_h-out_h)/10)*cos(2*PI*t/{durationText})";
            var baseFilter = "scale=1080:1920:force_original_aspect_ratio=increase," +
                   $"scale='{scaleExpr}':'{scaleExpr.Replace("1080", "1920")}':eval=frame," +
                   $"crop=1080:1920:x='{panX}':y='{panY}',setsar=1";

            if (clipIndex != 0)
            {
                return baseFilter;
            }

            var safeHook = EscapeDrawText(string.IsNullOrWhiteSpace(openingHookText)
                ? "Bi mat ma shop khong muon ban biet..."
                : openingHookText.Trim());
            return baseFilter +
                   ",eq=saturation=1.10" +
                   ",drawbox=x=40:y=(h/2)-130:w=(w-80):h=260:color=black@0.35:t=fill:enable='between(t,0,3)'" +
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

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                }

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

        private async Task<string> BuildDynamicCaptionFilterAsync(
            string narrationScript,
            string narrationTrackPath,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(narrationScript) ||
                string.IsNullOrWhiteSpace(narrationTrackPath) ||
                !File.Exists(narrationTrackPath))
            {
                return string.Empty;
            }

            var audioDuration = await GetAudioDurationSecondsAsync(narrationTrackPath, logAction, cancellationToken).ConfigureAwait(false);
            if (audioDuration <= 0.1d)
            {
                return string.Empty;
            }

            var captions = await BuildCaptionTimelineWithGeminiAsync(
                narrationScript,
                audioDuration,
                settings,
                logAction,
                cancellationToken).ConfigureAwait(false);
            if (captions.Count == 0)
            {
                return string.Empty;
            }

            var blocks = new List<string>();
            for (var i = 0; i < captions.Count; i++)
            {
                var row = captions[i];
                var color = i % 2 == 0 ? "yellow" : "white";
                var text = EscapeDrawText(row.Text);
                blocks.Add(
                    "drawtext=font='Segoe UI Bold':text='" + text +
                    "':x=(w-text_w)/2:y=(h*0.75):fontsize=54:fontcolor=" + color +
                    ":borderw=4:bordercolor=black:line_spacing=8:enable='between(t," +
                    row.Start.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," +
                    row.End.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")'");
            }

            logAction?.Invoke("AI Caption: generated " + captions.Count + " timed caption segments.");
            return string.Join(",", blocks);
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

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                }

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
            var prompt = "Viết một đoạn voice-over tiếng Việt 45-60 giây cho video affiliate bán hàng. " +
                         "Giọng kể tự nhiên, truyền cảm, có mở vấn đề - giải pháp - chốt CTA mềm. " +
                         $"Sản phẩm: {name}. Giá: {price}. " +
                         "Trả về đúng một đoạn văn, không markdown.";
            var text = await _geminiService.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);
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

        private async Task GenerateNarrationWithLyriaStrictAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.LyriaApiKey))
            {
                logAction?.Invoke("Lyria Error: thiếu LyriaApiKey trong AppSettings. Không thể tạo giọng đọc thuyết minh.");
                throw new InvalidOperationException("Missing Lyria API key.");
            }

            if (string.IsNullOrWhiteSpace(settings?.LyriaEndpoint))
            {
                logAction?.Invoke("Lyria Error: thiếu LyriaEndpoint trong AppSettings.");
                throw new InvalidOperationException("Missing Lyria endpoint.");
            }

            try
            {
                logAction?.Invoke("AI Voice: generating Vietnamese narration via Lyria...");
                var audioUrl = await _videoService.GenerateAudioAsync(
                    text,
                    settings.LyriaApiKey,
                    settings.LyriaEndpoint,
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    logAction?.Invoke("Lyria Error: API trả về audioUrl rỗng.");
                    throw new InvalidOperationException("Lyria returned empty audioUrl.");
                }

                await DownloadFileAsync(audioUrl, outputAudioFile, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(outputAudioFile))
                {
                    logAction?.Invoke("Lyria Error: tải file mp3 từ audioUrl thất bại.");
                    throw new InvalidOperationException("Failed to download Lyria audio.");
                }

                logAction?.Invoke("AI Voice: Lyria narration saved -> " + outputAudioFile);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("Lyria Error: gọi API thất bại -> " + ex.Message);
                throw;
            }
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
