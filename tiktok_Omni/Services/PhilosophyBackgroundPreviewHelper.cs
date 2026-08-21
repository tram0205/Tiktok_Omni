using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Cache video nền (bước 1 pipeline) trước khi ghép âm thanh.</summary>
    public static class PhilosophyBackgroundPreviewHelper
    {
        public const string PreviewVideoFileName = "background.mp4";
        public const string PreviewStampFileName = "preview.stamp";

        public static string GetSessionBase(PhilosophyBatchItem batch, AppSettings settings)
        {
            ProfileScopedPaths.SetConfiguredStorageRoot(settings?.StorageRootPath);
            var root = ProfileScopedPaths.ResolveStorageRoot(settings?.StorageRootPath);
            var id = batch?.BatchId ?? Guid.Empty;
            return Path.Combine(root, "philosophy-background-preview", id.ToString("N"));
        }

        public static string GetQuotePreviewDirectory(string sessionBase, int quoteIndex) =>
            Path.Combine(sessionBase ?? string.Empty, "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture));

        public static string GetPreviewVideoPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetQuotePreviewDirectory(sessionBase, quoteIndex), PreviewVideoFileName);

        public static string GetPreviewStampPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetQuotePreviewDirectory(sessionBase, quoteIndex), PreviewStampFileName);

        public static string ComputePreviewStamp(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            string profileName)
        {
            var mode = PhilosophyVisualModes.Normalize(quote?.VisualMode ?? 0);
            var broll = (quote?.BRollFolder ?? string.Empty).Trim();
            var zoom = string.Join("|", (quote?.ZoomImagePaths ?? new System.Collections.Generic.List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim()));
            var motion = (quote?.MotionPrompt ?? string.Empty).Trim();
            var batchRef = (batch?.ReferenceImagePath ?? string.Empty).Trim();
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            return mode + "|" + profile + "|" + broll + "|" + zoom + "|" + batchRef + "|" + motion;
        }

        public static string ComputePreviewStampHash(string stamp)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(stamp ?? string.Empty));
                var sb = new StringBuilder(16);
                for (var i = 0; i < 8; i++)
                {
                    sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        public static bool IsPreviewCurrent(string sessionBase, int quoteIndex, string stamp)
        {
            var videoPath = GetPreviewVideoPath(sessionBase, quoteIndex);
            var stampPath = GetPreviewStampPath(sessionBase, quoteIndex);
            if (!File.Exists(videoPath) || new FileInfo(videoPath).Length < 10_000L)
            {
                return false;
            }

            if (!File.Exists(stampPath))
            {
                return false;
            }

            try
            {
                var saved = (File.ReadAllText(stampPath) ?? string.Empty).Trim();
                return string.Equals(saved, ComputePreviewStampHash(stamp), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void WritePreviewStamp(string sessionBase, int quoteIndex, string stamp)
        {
            var dir = GetQuotePreviewDirectory(sessionBase, quoteIndex);
            Directory.CreateDirectory(dir);
            File.WriteAllText(GetPreviewStampPath(sessionBase, quoteIndex), ComputePreviewStampHash(stamp));
        }

        public static void InvalidatePreview(string sessionBase, int quoteIndex)
        {
            TryDeleteFile(GetPreviewVideoPath(sessionBase, quoteIndex));
            TryDeleteFile(GetPreviewStampPath(sessionBase, quoteIndex));
        }

        public static void TryOpenPreviewVideo(string videoPath)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = videoPath,
                UseShellExecute = true
            });
        }

        public static PhilosophyRenderOptions BuildRenderOptions(
            PhilosophyBatchItem batch,
            PhilosophyScriptItem quote,
            string profileName,
            AppSettings settings)
        {
            var duration = PhilosophyRenderOptions.ResolveDurationBounds(
                batch?.GenerationMode ?? "Quotes",
                batch?.MinDurationSeconds ?? 0,
                batch?.MaxDurationSeconds ?? 0);
            var resolvedProfile = PhilosophyBatchHelper.ResolveBatchProfileName(batch, profileName);
            var visualMode = PhilosophyVisualModes.Normalize(quote?.VisualMode ?? 0);

            return new PhilosophyRenderOptions
            {
                ProfileName = resolvedProfile,
                ReferenceImagePath = PhilosophyBatchHelper.ResolveReferenceImagePath(batch, quote),
                BRollFolder = quote?.BRollFolder?.Trim() ?? string.Empty,
                MusicFolder = quote?.MusicFolder?.Trim() ?? string.Empty,
                MusicVolumePercent = PhilosophyBatchHelper.ResolveQuoteMusicVolumePercent(quote, batch),
                NarrationSpeedPercent = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(quote, batch),
                AmbientFolder = PhilosophyAmbientCatalog.ResolveAmbientMediaPath(
                    quote?.AmbientKey,
                    resolvedProfile,
                    settings,
                    quote?.Mood),
                VisualMode = visualMode,
                MinDurationSeconds = duration.MinSeconds,
                MaxDurationSeconds = duration.MaxSeconds,
                ZoomImagePaths = quote?.ZoomImagePaths?
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .ToList() ?? new System.Collections.Generic.List<string>(),
                PreRenderedFolder = (quote?.SceneVideoFolder ?? string.Empty).Trim(),
                QuoteForSceneMatch = (quote?.Content ?? string.Empty).Trim()
            };
        }

        public static AutomationProfile ResolveAutomationProfile(AppSettings settings, string profileName)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var match = settings?.Profiles?
                .FirstOrDefault(p => p != null
                                     && string.Equals(
                                         ProfileScopedPaths.ResolveProfileName(p.Name),
                                         nick,
                                         StringComparison.OrdinalIgnoreCase));
            return match ?? new AutomationProfile { Name = nick };
        }

        public static bool TryValidateBackgroundPreviewRequest(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            string profileName,
            AppSettings settings,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (quote == null)
            {
                errorMessage = "Thiếu dòng quote.";
                return false;
            }

            if (!VideoReupRemixService.TryValidateFfmpegToolkit(settings, out errorMessage))
            {
                return false;
            }

            var rowProfile = PhilosophyBatchHelper.ResolveBatchProfileName(batch, profileName);
            var visualMode = PhilosophyVisualModes.Normalize(quote.VisualMode);

            if (visualMode == PhilosophyVisualModes.Broll)
            {
                if (!PhilosophyBRollSelection.TryValidateSelection(quote.BRollFolder, rowProfile, out errorMessage))
                {
                    return false;
                }

                return true;
            }

            if (visualMode == PhilosophyVisualModes.ImageZoom
                || visualMode == PhilosophyVisualModes.ImageSlideshow)
            {
                if (!PhilosophyBRollSelection.TryValidateZoomImages(quote.ZoomImagePaths, rowProfile, out errorMessage))
                {
                    return false;
                }

                return true;
            }

            if (visualMode == PhilosophyVisualModes.ZoomBrollHybrid)
            {
                if (!PhilosophyBRollSelection.TryValidateZoomImages(quote.ZoomImagePaths, rowProfile, out errorMessage))
                {
                    return false;
                }

                if (!PhilosophyBRollSelection.TryValidateSelection(quote.BRollFolder, rowProfile, out var brollErr))
                {
                    errorMessage = brollErr + " (cần B-roll outro).";
                    return false;
                }

                return true;
            }

            if (visualMode == PhilosophyVisualModes.AiStillZoom)
            {
                if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
                {
                    errorMessage = "Cần AI API Key (Gemini) trong Cài đặt.";
                    return false;
                }

                var refPath = PhilosophyBatchHelper.ResolveReferenceImagePath(batch, quote);
                if (string.IsNullOrEmpty(refPath))
                {
                    var mascot = PhilosophyGeminiBackgroundContext.BuildMascotContext(rowProfile, null);
                    if (!mascot.HasMascotImage)
                    {
                        errorMessage = "Cần ảnh ref batch hoặc mascot profile.";
                        return false;
                    }
                }

                return true;
            }

            if (visualMode == PhilosophyVisualModes.PreRendered)
            {
                var folder = (quote.SceneVideoFolder ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    errorMessage = "Cần thư mục video phân cảnh.";
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
            {
                errorMessage = "Loại nền AI Veo cần Veo API Key + Endpoint trong Cài đặt.";
                return false;
            }

            return true;
        }

        public static string BuildPreviewCellTooltip(
            bool isGenerating,
            bool isCurrent,
            string videoPath,
            int visualMode)
        {
            if (isGenerating)
            {
                return "Đang ghép video nền (FFmpeg / AI)…";
            }

            if (isCurrent && !string.IsNullOrWhiteSpace(videoPath))
            {
                return "Bấm để xem video nền (chưa có thoại/phụ đề)\r\n" + videoPath;
            }

            var modeLabel = PhilosophyBatchHelper.ToSimpleBackgroundModeLabel(visualMode);
            return "Bấm để tạo preview video nền («" + modeLabel + "») trước khi ghép âm thanh.";
        }

        private static void TryDeleteFile(string path)
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
