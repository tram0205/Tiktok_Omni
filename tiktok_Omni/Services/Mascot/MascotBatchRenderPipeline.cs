using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Mascot
{
    public sealed class MascotBatchRenderPipeline : IDisposable
    {
        private readonly MascotWorker _mascotWorker;
        private readonly LipSyncCloudService _lipSyncCloud = new LipSyncCloudService();
        private readonly VideoService _videoService = new VideoService();
        private bool _disposed;

        public MascotBatchRenderPipeline()
        {
            _mascotWorker = new MascotWorker(_videoService);
        }

        public async Task<MascotBatchRenderResult> RenderFromExcelAsync(
            string excelPath,
            string profile,
            IList<string> identityImagePaths,
            string primaryMascotImagePath,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings.VeoEndpoint))
            {
                throw new InvalidOperationException("Cần cấu hình Veo API key và endpoint trong Cài đặt.");
            }

            var scenes = MascotExcelHelper.ReadScenes(excelPath);
            var nick = ProfileScopedPaths.ResolveProfileName(profile);
            var workDir = Path.Combine(
                ProfileScopedPaths.CreateGeneratedSessionFolder(nick, "MascotBatch"),
                Path.GetFileNameWithoutExtension(excelPath) ?? "batch");
            Directory.CreateDirectory(workDir);

            var identity = (identityImagePaths ?? new List<string>())
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
            if (identity.Count < 3)
            {
                throw new InvalidOperationException("Avatar Identity Pack cần 3-5 ảnh hợp lệ.");
            }

            var hero = string.IsNullOrWhiteSpace(primaryMascotImagePath) || !File.Exists(primaryMascotImagePath)
                ? identity[0]
                : primaryMascotImagePath;
            var heroDataUrl = MascotMediaHelper.BuildImageDataUrl(hero);
            var refUrls = identity.Select(MascotMediaHelper.BuildImageDataUrl).ToList();

            var lipSyncedClips = new List<string>();
            for (var i = 0; i < scenes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scene = scenes[i];
                var sceneDir = Path.Combine(workDir, $"scene_{scene.Order:D2}");
                Directory.CreateDirectory(sceneDir);
                log?.Invoke($"[Mascot Render] Cảnh {scene.Order}/{scenes.Count}…");

                var audioPath = Path.Combine(sceneDir, "voiceover.mp3");
                var edge = new EdgeTtsService();
                await edge.SynthesizeVietnameseFemaleToMp3Async(
                    scene.Voiceover,
                    audioPath,
                    log,
                    cancellationToken).ConfigureAwait(false);

                var prompt = string.IsNullOrWhiteSpace(scene.ImagePrompt)
                    ? "cinematic mascot scene, vertical 9:16, subtle motion, no talking mouth"
                    : scene.ImagePrompt;

                log?.Invoke("[Mascot Render] Veo: ảnh tham chiếu cảnh " + scene.Order);
                var variantUrl = await _mascotWorker.GenerateContextImageWithPollingAsync(
                    heroDataUrl,
                    prompt,
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    refUrls,
                    cancellationToken,
                    log).ConfigureAwait(false);

                var variantPath = Path.Combine(sceneDir, "variant.jpg");
                await MascotMediaHelper.DownloadAsync(variantUrl, variantPath, cancellationToken).ConfigureAwait(false);

                log?.Invoke("[Mascot Render] Veo: clip câm cảnh " + scene.Order);
                var silentUrl = await _mascotWorker.GenerateVideoFromImageWithPollingAsync(
                    MascotMediaHelper.BuildImageDataUrl(variantPath),
                    prompt + ", silent clip, mascot not speaking to camera",
                    settings.VeoApiKey,
                    settings.VeoEndpoint,
                    8d,
                    cancellationToken,
                    log).ConfigureAwait(false);

                var silentPath = Path.Combine(sceneDir, "silent.mp4");
                await MascotMediaHelper.DownloadAsync(silentUrl, silentPath, cancellationToken).ConfigureAwait(false);

                var lipPath = Path.Combine(sceneDir, "lip_synced.mp4");
                await _lipSyncCloud.LipSyncAsync(silentPath, audioPath, lipPath, settings, log, cancellationToken)
                    .ConfigureAwait(false);
                lipSyncedClips.Add(lipPath);
            }

            var finalPath = Path.Combine(workDir, nick + "_MascotBatch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");
            log?.Invoke("[Mascot Render] FFmpeg: ghép " + lipSyncedClips.Count + " clip (crossfade 0.5s)…");
            await MascotFfmpegHelper.ConcatClipsWithCrossfadeAsync(
                lipSyncedClips,
                finalPath,
                0.5d,
                log,
                settings,
                cancellationToken).ConfigureAwait(false);

            return new MascotBatchRenderResult
            {
                Success = File.Exists(finalPath),
                FinalVideoPath = finalPath,
                SceneClipPaths = lipSyncedClips
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lipSyncCloud.Dispose();
        }
    }
}
