using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseBrandLogoPositionCatalog
    {
        public const string BottomRight = "bottom_right";
        public const string BottomLeft = "bottom_left";
        public const string TopRight = "top_right";
        public const string TopLeft = "top_left";
        public const string Center = "center";

        public static string ResolveId(string id)
        {
            var value = (id ?? string.Empty).Trim().ToLowerInvariant();
            switch (value)
            {
                case BottomLeft:
                case TopRight:
                case TopLeft:
                case Center:
                    return value;
                default:
                    return BottomRight;
            }
        }

        public static string GetDisplayLabel(string id)
        {
            switch (ResolveId(id))
            {
                case BottomLeft:
                    return "Trái dưới";
                case TopRight:
                    return "Phải trên";
                case TopLeft:
                    return "Trái trên";
                case Center:
                    return "Giữa";
                default:
                    return "Phải dưới";
            }
        }

        public static string BuildOverlayExpression(string id, int marginX, int marginY)
        {
            var mx = Math.Max(0, marginX);
            var my = Math.Max(0, marginY);
            switch (ResolveId(id))
            {
                case BottomLeft:
                    return mx + ":H-h-" + my;
                case TopRight:
                    return "W-w-" + mx + ":" + my;
                case TopLeft:
                    return mx + ":" + my;
                case Center:
                    return "(W-w)/2:(H-h)/2";
                default:
                    return "W-w-" + mx + ":H-h-" + my;
            }
        }
    }

    internal sealed class ShowcaseBrandLogoRenderPlan
    {
        public bool Enabled { get; set; }

        public string LogoPath { get; set; } = string.Empty;

        public string PositionId { get; set; } = ShowcaseBrandLogoPositionCatalog.BottomRight;

        public int ScaleWidthPercent { get; set; } = 12;

        public int MarginX { get; set; } = 24;

        public int MarginY { get; set; } = 24;

        public int OpacityPercent { get; set; } = 100;

        public int CanvasWidth { get; set; } = 1080;

        public bool IsActive =>
            Enabled
            && !string.IsNullOrWhiteSpace(LogoPath)
            && File.Exists(LogoPath.Trim());
    }

    internal static class ShowcaseBrandOverlayHelper
    {
        public const int DefaultScaleWidthPercent = 12;
        public const int DefaultMargin = 24;
        public const int DefaultOpacityPercent = 100;

        public static int ClampScaleWidthPercent(int value)
        {
            if (value < 4)
            {
                return 4;
            }

            if (value > 40)
            {
                return 40;
            }

            return value;
        }

        public static int ClampMargin(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 160)
            {
                return 160;
            }

            return value;
        }

        public static int ClampOpacityPercent(int value)
        {
            if (value < 20)
            {
                return 20;
            }

            if (value > 100)
            {
                return 100;
            }

            return value;
        }

        public static void EnsureVideoDefaults(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseBrandLogoPositionId = ShowcaseBrandLogoPositionCatalog.ResolveId(video.ShowcaseBrandLogoPositionId);
            video.ShowcaseBrandLogoScaleWidthPercent = ClampScaleWidthPercent(
                video.ShowcaseBrandLogoScaleWidthPercent > 0 ? video.ShowcaseBrandLogoScaleWidthPercent : DefaultScaleWidthPercent);
            video.ShowcaseBrandLogoMarginX = ClampMargin(video.ShowcaseBrandLogoMarginX);
            video.ShowcaseBrandLogoMarginY = ClampMargin(video.ShowcaseBrandLogoMarginY);
            video.ShowcaseBrandLogoOpacityPercent = ClampOpacityPercent(
                video.ShowcaseBrandLogoOpacityPercent > 0 ? video.ShowcaseBrandLogoOpacityPercent : DefaultOpacityPercent);
            RefreshLabel(video);
        }

        public static void RefreshLabel(ShowcaseVideoItem video, AppSettings settings = null)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseBrandLogoLabel = FormatSummary(
                video.ShowcaseBrandLogoEnabled,
                video.ShowcaseBrandLogoFile,
                video.ProfileName,
                video.ShowcaseBrandLogoPositionId,
                video.ShowcaseBrandLogoScaleWidthPercent,
                settings);
        }

        public static string FormatSummary(
            bool enabled,
            string customFile,
            string profileName,
            string positionId,
            int scaleWidthPercent,
            AppSettings settings = null)
        {
            if (!enabled)
            {
                return "Tắt";
            }

            var path = ResolveEffectiveLogoPath(customFile, profileName, settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                return "Bật · chưa có file";
            }

            var scale = ClampScaleWidthPercent(scaleWidthPercent);
            return ShowcaseBrandLogoPositionCatalog.GetDisplayLabel(positionId) + " · " + scale + "%";
        }

        public static string ResolveEffectiveLogoPath(string customFile, string profileName, AppSettings settings = null)
        {
            var custom = (customFile ?? string.Empty).Trim();
            if (custom.Length > 0)
            {
                var resolved = OmniBrandLogoLibrary.ResolveLogoFilePath(custom, settings);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            var shared = OmniBrandLogoLibrary.TryPickDefaultLogo(settings);
            if (!string.IsNullOrWhiteSpace(shared))
            {
                return shared;
            }

            return PhilosophyProfileAssets.TryPickBrandOverlayImage(profileName);
        }

        public static ShowcaseBrandLogoRenderPlan BuildRenderPlan(
            ShowcasePerVideoRenderSettings renderSettings,
            int canvasWidth)
        {
            if (renderSettings == null || !renderSettings.BrandLogoEnabled)
            {
                return new ShowcaseBrandLogoRenderPlan { CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080 };
            }

            var path = !string.IsNullOrWhiteSpace(renderSettings.BrandLogoResolvedPath)
                ? renderSettings.BrandLogoResolvedPath.Trim()
                : ResolveEffectiveLogoPath(renderSettings.BrandLogoFile, renderSettings.ProfileName, null);

            return new ShowcaseBrandLogoRenderPlan
            {
                Enabled = true,
                LogoPath = path ?? string.Empty,
                PositionId = ShowcaseBrandLogoPositionCatalog.ResolveId(renderSettings.BrandLogoPositionId),
                ScaleWidthPercent = ClampScaleWidthPercent(renderSettings.BrandLogoScaleWidthPercent),
                MarginX = ClampMargin(renderSettings.BrandLogoMarginX),
                MarginY = ClampMargin(renderSettings.BrandLogoMarginY),
                OpacityPercent = ClampOpacityPercent(renderSettings.BrandLogoOpacityPercent),
                CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080
            };
        }

        public static string BuildLogoFilterComplex(
            int videoInputIndex,
            int logoInputIndex,
            string captionVideoFilter,
            string variantGrade,
            ShowcaseBrandLogoRenderPlan plan)
        {
            if (plan == null || !plan.IsActive)
            {
                return string.Empty;
            }

            var chain = MergeCaptionAndGrade(captionVideoFilter, variantGrade);
            var parts = new System.Collections.Generic.List<string>();
            var current = "[" + videoInputIndex + ":v]";
            if (!string.IsNullOrWhiteSpace(chain))
            {
                parts.Add(current + chain + "[vbase]");
                current = "[vbase]";
            }
            else
            {
                parts.Add(current + "format=yuv420p[vbase]");
                current = "[vbase]";
            }

            var targetWidth = Math.Max(32, plan.CanvasWidth * ClampScaleWidthPercent(plan.ScaleWidthPercent) / 100);
            var alpha = ClampOpacityPercent(plan.OpacityPercent) / 100d;
            var overlay = ShowcaseBrandLogoPositionCatalog.BuildOverlayExpression(plan.PositionId, plan.MarginX, plan.MarginY);
            var logoChain = "[" + logoInputIndex + ":v]scale=w=" + targetWidth + ":h=-1";
            if (alpha < 0.995d)
            {
                var alphaText = alpha.ToString("0.##", CultureInfo.InvariantCulture);
                logoChain += ",format=rgba,colorchannelmixer=aa=" + alphaText;
            }

            logoChain += "[logo]";
            parts.Add(logoChain);
            parts.Add(current + "[logo]overlay=" + overlay + "[vout]");
            return string.Join(";", parts);
        }

        public static async Task<string> ApplyVideoOverlaysIfNeededAsync(
            string stitchedPath,
            string renderDir,
            string captionVideoFilter,
            string variantGrade,
            ShowcaseBrandLogoRenderPlan logoPlan,
            Func<string, CancellationToken, Task> runFfmpegAsync,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stitchedPath) || !File.Exists(stitchedPath))
            {
                return stitchedPath;
            }

            var needsLogo = logoPlan != null && logoPlan.IsActive;
            var needsSubs = !string.IsNullOrWhiteSpace(captionVideoFilter);
            if (!needsLogo && !needsSubs)
            {
                return stitchedPath;
            }

            Directory.CreateDirectory(renderDir ?? stitchedPath);
            var output = Path.Combine(renderDir, "video_composited.mp4");
            var grade = string.IsNullOrWhiteSpace(variantGrade)
                ? string.Empty
                : variantGrade;
            var qualityArgs = "-c:v libx264 -preset veryfast -crf 20 -profile:v high -level 4.2 -pix_fmt yuv420p -movflags +faststart -an";

            // Phụ đề ASS + logo trong một filter_complex dễ lỗi escape đường dẫn Windows — tách 2 bước.
            var videoInputPath = stitchedPath;
            if (needsLogo && needsSubs)
            {
                var withSubs = Path.Combine(renderDir, "video_with_subs.mp4");
                var mergedVf = MergeCaptionAndGrade(captionVideoFilter, grade);
                var subsArgs = "-y -i \"" + stitchedPath + "\" -vf \"" + mergedVf + "\" " + qualityArgs + " \"" + withSubs + "\"";
                await runFfmpegAsync(subsArgs, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(withSubs))
                {
                    return stitchedPath;
                }

                videoInputPath = withSubs;
                captionVideoFilter = string.Empty;
                grade = string.Empty;
            }

            string args;
            if (needsLogo)
            {
                var filterComplex = BuildLogoFilterComplex(0, 1, captionVideoFilter, grade, logoPlan);
                args = "-y -i \"" + videoInputPath + "\" -i \"" + logoPlan.LogoPath + "\" -filter_complex \""
                    + filterComplex + "\" -map \"[vout]\" " + qualityArgs + " \"" + output + "\"";
            }
            else
            {
                var mergedVf = MergeCaptionAndGrade(captionVideoFilter, grade);
                args = "-y -i \"" + videoInputPath + "\" -vf \"" + mergedVf + "\" " + qualityArgs + " \"" + output + "\"";
            }

            await runFfmpegAsync(args, cancellationToken).ConfigureAwait(false);
            return File.Exists(output) ? output : stitchedPath;
        }

        private static string MergeCaptionAndGrade(string captionVideoFilter, string variantGrade)
        {
            if (string.IsNullOrWhiteSpace(captionVideoFilter))
            {
                return variantGrade ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(variantGrade))
            {
                return captionVideoFilter;
            }

            return captionVideoFilter + "," + variantGrade;
        }
    }
}
