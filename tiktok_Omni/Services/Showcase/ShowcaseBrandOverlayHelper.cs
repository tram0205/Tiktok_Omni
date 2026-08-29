using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

            return BuildPreviewPlan(
                renderSettings.BrandLogoEnabled,
                path,
                renderSettings.BrandLogoPositionId,
                renderSettings.BrandLogoScaleWidthPercent,
                renderSettings.BrandLogoMarginX,
                renderSettings.BrandLogoMarginY,
                renderSettings.BrandLogoOpacityPercent,
                canvasWidth);
        }

        public static ShowcaseBrandLogoRenderPlan BuildPreviewPlan(
            bool enabled,
            string logoFile,
            string profileName,
            AppSettings settings,
            string positionId,
            int scaleWidthPercent,
            int marginX,
            int marginY,
            int opacityPercent,
            int canvasWidth = 1080)
        {
            if (!enabled)
            {
                return new ShowcaseBrandLogoRenderPlan { CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080 };
            }

            var path = ResolveEffectiveLogoPath(logoFile, profileName, settings);
            return BuildPreviewPlan(
                true,
                path,
                positionId,
                scaleWidthPercent,
                marginX,
                marginY,
                opacityPercent,
                canvasWidth);
        }

        public static ShowcaseBrandLogoRenderPlan BuildPreviewPlan(
            bool enabled,
            string resolvedLogoPath,
            string positionId,
            int scaleWidthPercent,
            int marginX,
            int marginY,
            int opacityPercent,
            int canvasWidth = 1080)
        {
            if (!enabled)
            {
                return new ShowcaseBrandLogoRenderPlan { CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080 };
            }

            return new ShowcaseBrandLogoRenderPlan
            {
                Enabled = true,
                LogoPath = resolvedLogoPath ?? string.Empty,
                PositionId = ShowcaseBrandLogoPositionCatalog.ResolveId(positionId),
                ScaleWidthPercent = ClampScaleWidthPercent(
                    scaleWidthPercent > 0 ? scaleWidthPercent : DefaultScaleWidthPercent),
                MarginX = ClampMargin(marginX),
                MarginY = ClampMargin(marginY),
                OpacityPercent = ClampOpacityPercent(
                    opacityPercent > 0 ? opacityPercent : DefaultOpacityPercent),
                CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080
            };
        }

        public static Rectangle ComputeLogoDrawRectangle(
            int canvasWidth,
            int canvasHeight,
            int logoSourceWidth,
            int logoSourceHeight,
            string positionId,
            int scaleWidthPercent,
            int marginX,
            int marginY)
        {
            var canvasW = Math.Max(1, canvasWidth);
            var canvasH = Math.Max(1, canvasHeight);
            var sourceW = Math.Max(1, logoSourceWidth);
            var sourceH = Math.Max(1, logoSourceHeight);
            var targetW = Math.Max(8, canvasW * ClampScaleWidthPercent(scaleWidthPercent) / 100);
            var targetH = Math.Max(8, (int)Math.Round(sourceH * (targetW / (double)sourceW)));
            var mx = ClampMargin(marginX);
            var my = ClampMargin(marginY);

            switch (ShowcaseBrandLogoPositionCatalog.ResolveId(positionId))
            {
                case ShowcaseBrandLogoPositionCatalog.BottomLeft:
                    return new Rectangle(mx, canvasH - targetH - my, targetW, targetH);
                case ShowcaseBrandLogoPositionCatalog.TopRight:
                    return new Rectangle(canvasW - targetW - mx, my, targetW, targetH);
                case ShowcaseBrandLogoPositionCatalog.TopLeft:
                    return new Rectangle(mx, my, targetW, targetH);
                case ShowcaseBrandLogoPositionCatalog.Center:
                    return new Rectangle((canvasW - targetW) / 2, (canvasH - targetH) / 2, targetW, targetH);
                default:
                    return new Rectangle(canvasW - targetW - mx, canvasH - targetH - my, targetW, targetH);
            }
        }

        public static Bitmap RenderLogoPreviewBitmap(
            ShowcaseBrandLogoRenderPlan plan,
            string statusMessage,
            int outputWidth = 288)
        {
            const int canvasW = 1080;
            const int canvasH = 1920;
            var outputHeight = Math.Max(120, (int)Math.Round(outputWidth * (canvasH / (double)canvasW)));

            var frame = new Bitmap(canvasW, canvasH);
            using (var graphics = Graphics.FromImage(frame))
            {
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawMockVideoBackground(graphics, canvasW, canvasH);

                if (plan != null && plan.IsActive)
                {
                    try
                    {
                        using (var logo = Image.FromFile(plan.LogoPath))
                        {
                            var rect = ComputeLogoDrawRectangle(
                                canvasW,
                                canvasH,
                                logo.Width,
                                logo.Height,
                                plan.PositionId,
                                plan.ScaleWidthPercent,
                                plan.MarginX,
                                plan.MarginY);
                            DrawLogoWithOpacity(graphics, logo, rect, ClampOpacityPercent(plan.OpacityPercent));
                        }
                    }
                    catch
                    {
                        DrawPreviewMessage(graphics, canvasW, canvasH, "Không mở được file logo");
                    }
                }
                else
                {
                    DrawPreviewMessage(
                        graphics,
                        canvasW,
                        canvasH,
                        string.IsNullOrWhiteSpace(statusMessage) ? "Logo tắt hoặc chưa chọn file" : statusMessage);
                }
            }

            var scaled = new Bitmap(outputWidth, outputHeight);
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(frame, new Rectangle(0, 0, outputWidth, outputHeight));
            }

            frame.Dispose();
            return scaled;
        }

        private static void DrawMockVideoBackground(Graphics graphics, int width, int height)
        {
            using (var brush = new LinearGradientBrush(
                       new Rectangle(0, 0, width, height),
                       Color.FromArgb(26, 26, 46),
                       Color.FromArgb(22, 33, 62),
                       LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(brush, 0, 0, width, height);
            }

            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 2f))
            {
                graphics.DrawRectangle(pen, 24, 24, width - 48, height - 48);
            }
        }

        private static void DrawPreviewMessage(Graphics graphics, int width, int height, string message)
        {
            var text = (message ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            using (var font = new Font("Segoe UI", 34f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(170, 220, 220, 230)))
            {
                var size = graphics.MeasureString(text, font, width - 120);
                graphics.DrawString(
                    text,
                    font,
                    brush,
                    (width - size.Width) / 2f,
                    (height - size.Height) / 2f);
            }
        }

        private static void DrawLogoWithOpacity(Graphics graphics, Image logo, Rectangle dest, int opacityPercent)
        {
            var alpha = ClampOpacityPercent(opacityPercent) / 100f;
            if (alpha >= 0.995f)
            {
                graphics.DrawImage(logo, dest);
                return;
            }

            var colorMatrix = new ColorMatrix
            {
                Matrix33 = alpha
            };
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                graphics.DrawImage(
                    logo,
                    dest,
                    0,
                    0,
                    logo.Width,
                    logo.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
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
