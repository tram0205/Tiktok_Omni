using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseZoomAspectFitHelper
    {
        /// <summary>Sai lệch tỉ lệ cạnh &gt; 3% so với khung xuất → coi là không khớp.</summary>
        public const double AspectToleranceRatio = 0.03d;

        public static bool TryGetImagePixelSize(string imagePath, out int width, out int height)
        {
            width = 0;
            height = 0;
            var path = (imagePath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using (var img = Image.FromFile(path))
                {
                    width = img.Width;
                    height = img.Height;
                    return width > 0 && height > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAspectMismatch(int imageWidth, int imageHeight, ShowcaseOutputAspectPreset canvas)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || canvas == null || canvas.Width <= 0 || canvas.Height <= 0)
            {
                return false;
            }

            var imageAspect = imageWidth / (double)imageHeight;
            var canvasAspect = canvas.Width / (double)canvas.Height;
            if (canvasAspect <= 0d)
            {
                return false;
            }

            return Math.Abs(imageAspect - canvasAspect) / canvasAspect > AspectToleranceRatio;
        }

        public static string FormatAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return "?";
            }

            var gcd = GreatestCommonDivisor(width, height);
            return (width / gcd).ToString(CultureInfo.InvariantCulture)
                   + ":" + (height / gcd).ToString(CultureInfo.InvariantCulture)
                   + " (" + width + "×" + height + ")";
        }

        public static string BuildBlurPadCompositePrefix(int width, int height)
        {
            var wText = width.ToString(CultureInfo.InvariantCulture);
            var hText = height.ToString(CultureInfo.InvariantCulture);
            return "split[zoombg][zoomfg];" +
                   "[zoombg]" + ShowcaseOutputAspectPresets.FormatScaleIncrease(width, height) +
                   ",boxblur=20:20," + ShowcaseOutputAspectPresets.FormatScaleCrop(width, height) + "[zoombg2];" +
                   "[zoomfg]scale=" + wText + ":" + hText + ":force_original_aspect_ratio=decrease[zoomfg2];" +
                   "[zoombg2][zoomfg2]overlay=(W-w)/2:(H-h)/2[zoombase];" +
                   "[zoombase]";
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0)
            {
                var t = b;
                b = a % b;
                a = t;
            }

            return Math.Max(1, a);
        }
    }
}
