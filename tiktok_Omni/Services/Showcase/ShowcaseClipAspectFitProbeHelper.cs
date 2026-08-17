using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    internal sealed class ShowcaseClipAspectMismatchEntry
    {
        public int SceneOrderOneBased { get; set; }

        public int SceneIndexZeroBased { get; set; }

        public AiVideoGenInputItem Scene { get; set; }

        public string ClipPath { get; set; } = string.Empty;

        public string ClipFileName { get; set; } = string.Empty;

        public int ClipWidth { get; set; }

        public int ClipHeight { get; set; }

        public string ThumbnailPath { get; set; } = string.Empty;

        public ShowcaseZoomAspectFitMode SelectedMode { get; set; } = ShowcaseZoomAspectFitMode.Crop;
    }

    internal static class ShowcaseClipAspectFitProbeHelper
    {
        public static async Task<IReadOnlyList<ShowcaseClipAspectMismatchEntry>> FindMismatchesAsync(
            IList<AiVideoGenInputItem> scenes,
            ShowcaseOutputAspectPreset canvas,
            string ffprobeExecutable,
            string ffmpegExecutable,
            string thumbnailWorkDir,
            CancellationToken cancellationToken)
        {
            var list = new List<ShowcaseClipAspectMismatchEntry>();
            if (scenes == null || scenes.Count == 0 || canvas == null)
            {
                return list;
            }

            if (!string.IsNullOrWhiteSpace(thumbnailWorkDir))
            {
                Directory.CreateDirectory(thumbnailWorkDir);
            }

            for (var i = 0; i < scenes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scene = scenes[i];
                var clipPath = (scene?.ClipPath ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                var (width, height) = await ShowcaseMediaProbeHelper.ProbeVideoResolutionAsync(
                        ffprobeExecutable,
                        clipPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (width <= 0 || height <= 0
                    || !ShowcaseZoomAspectFitHelper.IsAspectMismatch(width, height, canvas))
                {
                    continue;
                }

                var thumbPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(thumbnailWorkDir))
                {
                    thumbPath = BuildThumbnailPath(thumbnailWorkDir, i + 1, clipPath);
                    if (NeedsThumbnailRefresh(clipPath, thumbPath))
                    {
                        TryDelete(thumbPath);
                        ReupPreviewFrameService.TryExtractFrame(ffmpegExecutable, clipPath, thumbPath, 0.35d);
                    }

                    if (!File.Exists(thumbPath))
                    {
                        thumbPath = string.Empty;
                    }
                }

                list.Add(new ShowcaseClipAspectMismatchEntry
                {
                    SceneOrderOneBased = i + 1,
                    SceneIndexZeroBased = i,
                    Scene = scene,
                    ClipPath = clipPath,
                    ClipFileName = Path.GetFileName(clipPath) ?? string.Empty,
                    ClipWidth = width,
                    ClipHeight = height,
                    ThumbnailPath = thumbPath,
                    SelectedMode = scene.ShowcaseClipAspectFitMode
                });
            }

            return list;
        }

        private static string BuildThumbnailPath(string thumbnailWorkDir, int sceneOrderOneBased, string clipPath)
        {
            var clipKey = BuildClipThumbKey(clipPath);
            return Path.Combine(
                thumbnailWorkDir,
                "aspect_thumb_s" + sceneOrderOneBased.ToString("D2") + "_" + clipKey + ".jpg");
        }

        private static string BuildClipThumbKey(string clipPath)
        {
            var full = Path.GetFullPath(clipPath ?? string.Empty).Trim();
            if (full.Length == 0)
            {
                return "empty";
            }

            long ticks = 0;
            try
            {
                ticks = File.GetLastWriteTimeUtc(full).Ticks;
            }
            catch
            {
                // ignored
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(full + "|" + ticks.ToString()));
                return BitConverter.ToString(bytes, 0, 6).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static bool NeedsThumbnailRefresh(string clipPath, string thumbPath)
        {
            if (string.IsNullOrWhiteSpace(thumbPath) || !File.Exists(thumbPath))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
            {
                return false;
            }

            try
            {
                return File.GetLastWriteTimeUtc(clipPath) > File.GetLastWriteTimeUtc(thumbPath);
            }
            catch
            {
                return true;
            }
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
