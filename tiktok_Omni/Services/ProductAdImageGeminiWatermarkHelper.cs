using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace tiktok_Omni.Services
{
    public enum GeminiWatermarkRegionKind
    {
        Auto = 0,
        Small = 1,
        Medium = 2,
        Large = 3
    }

    public sealed class GeminiWatermarkStripResult
    {
        public string SourcePath { get; set; }
        public string OutputPath { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public int MaskPixels { get; set; }
    }

    /// <summary>
    /// Xoá ngôi sao 4 cánh Gemini (logo hiện, góc phải dưới) bằng inpaint.
    /// Không gỡ được watermark ẩn SynthID.
    /// </summary>
    public static class ProductAdImageGeminiWatermarkHelper
    {
        public static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

        public static string OpenFileFilter =>
            "Ảnh Gemini (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tất cả tệp (*.*)|*.*";

        public static bool IsSupportedImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var ext = Path.GetExtension(path);
            for (var i = 0; i < SupportedExtensions.Length; i++)
            {
                if (string.Equals(SupportedExtensions[i], ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool LooksLikeAlreadyStripped(string path)
        {
            var stem = Path.GetFileNameWithoutExtension(path ?? string.Empty);
            return stem.EndsWith("_nologo", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildOutputPath(string sourcePath)
        {
            var dir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(sourcePath) ?? "image";
            if (stem.EndsWith("_nologo", StringComparison.OrdinalIgnoreCase))
            {
                stem = stem.Substring(0, stem.Length - "_nologo".Length);
            }

            var dest = Path.Combine(dir, stem + "_nologo.png");
            var n = 2;
            while (File.Exists(dest))
            {
                dest = Path.Combine(
                    dir,
                    stem + "_nologo_" + n.ToString(CultureInfo.InvariantCulture) + ".png");
                n++;
            }

            return dest;
        }

        public static GeminiWatermarkStripResult ProcessFile(
            string sourcePath,
            GeminiWatermarkRegionKind region = GeminiWatermarkRegionKind.Auto)
        {
            var result = new GeminiWatermarkStripResult { SourcePath = sourcePath };
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                result.Error = "Không tìm thấy file.";
                return result;
            }

            if (!IsSupportedImage(sourcePath))
            {
                result.Error = "Định dạng không hỗ trợ.";
                return result;
            }

            try
            {
                using (var bmp = LoadAs32Bpp(sourcePath))
                {
                    result.MaskPixels = StripBottomRightSparkle(bmp, region);
                    var dest = BuildOutputPath(sourcePath);
                    bmp.Save(dest, ImageFormat.Png);
                    result.OutputPath = dest;
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        public static int StripBottomRightSparkle(Bitmap bmp, GeminiWatermarkRegionKind region)
        {
            if (bmp == null)
            {
                throw new ArgumentNullException(nameof(bmp));
            }

            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new InvalidOperationException("Ảnh cần Format32bppArgb.");
            }

            var w = bmp.Width;
            var h = bmp.Height;
            if (w < 48 || h < 48)
            {
                throw new InvalidOperationException("Ảnh quá nhỏ để xoá logo góc.");
            }

            var searchRoi = GetCornerRoi(w, h, 0.12f, 40, 280);
            bool[] mask;
            if (region == GeminiWatermarkRegionKind.Auto)
            {
                mask = DetectSparkleMask(bmp, searchRoi);
                var maskCount = CountTrue(mask);
                var roiPixels = searchRoi.Width * searchRoi.Height;
                var tooFew = maskCount < 12;
                var tooMany = roiPixels > 0 && maskCount > roiPixels * 0.38;
                if (tooFew || tooMany)
                {
                    mask = BuildBoxMask(w, h, GetCornerRoi(w, h, 0.07f, 32, 120));
                }
            }
            else
            {
                var fraction = region == GeminiWatermarkRegionKind.Small
                    ? 0.055f
                    : region == GeminiWatermarkRegionKind.Large
                        ? 0.12f
                        : 0.08f;
                var maxBox = region == GeminiWatermarkRegionKind.Large ? 260 : 160;
                mask = BuildBoxMask(w, h, GetCornerRoi(w, h, fraction, 28, maxBox));
            }

            InpaintMasked(bmp, mask);
            return CountTrue(mask);
        }

        private static Bitmap LoadAs32Bpp(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: true))
            {
                var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(img, 0, 0, img.Width, img.Height);
                }

                return bmp;
            }
        }

        private static Rectangle GetCornerRoi(int w, int h, float fraction, int minBox, int maxBox)
        {
            var minSide = Math.Min(w, h);
            var box = Clamp((int)(minSide * fraction), minBox, maxBox);
            box = Math.Min(box, w);
            box = Math.Min(box, h);
            return new Rectangle(w - box, h - box, box, box);
        }

        private static bool[] DetectSparkleMask(Bitmap bmp, Rectangle roi)
        {
            var w = bmp.Width;
            var h = bmp.Height;
            var n = w * h;
            var mask = new bool[n];
            var data = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var pixels = new byte[stride * h];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                var ring = Math.Max(4, Math.Min(roi.Width, roi.Height) / 16);
                var inner = Math.Max(2, ring / 3);

                for (var y = roi.Top; y < roi.Bottom; y++)
                {
                    for (var x = roi.Left; x < roi.Right; x++)
                    {
                        var off = y * stride + x * 4;
                        var b = pixels[off];
                        var g = pixels[off + 1];
                        var r = pixels[off + 2];
                        var luma = (0.299 * r) + (0.587 * g) + (0.114 * b);
                        var maxc = Math.Max(r, Math.Max(g, b));
                        var minc = Math.Min(r, Math.Min(g, b));
                        var sat = maxc == 0 ? 0.0 : (maxc - minc) / (double)maxc;

                        double ringSum = 0;
                        var ringCount = 0;
                        for (var dy = -ring; dy <= ring; dy++)
                        {
                            var yy = y + dy;
                            if (yy < 0 || yy >= h)
                            {
                                continue;
                            }

                            for (var dx = -ring; dx <= ring; dx++)
                            {
                                if (Math.Abs(dx) <= inner && Math.Abs(dy) <= inner)
                                {
                                    continue;
                                }

                                var xx = x + dx;
                                if (xx < 0 || xx >= w)
                                {
                                    continue;
                                }

                                var no = yy * stride + xx * 4;
                                ringSum += (0.299 * pixels[no + 2]) + (0.587 * pixels[no + 1]) + (0.114 * pixels[no]);
                                ringCount++;
                            }
                        }

                        var ringMean = ringCount > 0 ? ringSum / ringCount : luma;
                        var contrast = luma - ringMean;
                        var sparkle = sat < 0.48
                            && ((luma > 150 && contrast > 18) || (luma > 210 && contrast > 8));
                        if (sparkle)
                        {
                            mask[y * w + x] = true;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            DilateMask(mask, w, h, roi, radius: 3);
            return mask;
        }

        private static bool[] BuildBoxMask(int w, int h, Rectangle roi)
        {
            var mask = new bool[w * h];
            for (var y = roi.Top; y < roi.Bottom; y++)
            {
                for (var x = roi.Left; x < roi.Right; x++)
                {
                    mask[y * w + x] = true;
                }
            }

            return mask;
        }

        private static void DilateMask(bool[] mask, int w, int h, Rectangle roi, int radius)
        {
            var copy = (bool[])mask.Clone();
            var left = Math.Max(0, roi.Left - radius);
            var top = Math.Max(0, roi.Top - radius);
            var right = Math.Min(w, roi.Right + radius);
            var bottom = Math.Min(h, roi.Bottom + radius);
            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    if (!copy[y * w + x])
                    {
                        continue;
                    }

                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        var yy = y + dy;
                        if (yy < 0 || yy >= h)
                        {
                            continue;
                        }

                        for (var dx = -radius; dx <= radius; dx++)
                        {
                            var xx = x + dx;
                            if (xx < 0 || xx >= w)
                            {
                                continue;
                            }

                            mask[yy * w + xx] = true;
                        }
                    }
                }
            }
        }

        private static void InpaintMasked(Bitmap bmp, bool[] mask)
        {
            var w = bmp.Width;
            var h = bmp.Height;
            var bounds = MaskBounds(mask, w, h);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var data = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var pixels = new byte[stride * h];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                var dist = BuildDistanceFromKnown(mask, w, h, bounds);
                var order = new List<int>(bounds.Width * bounds.Height);
                var left = Math.Max(0, bounds.Left);
                var top = Math.Max(0, bounds.Top);
                var right = Math.Min(w, bounds.Right);
                var bottom = Math.Min(h, bounds.Bottom);
                for (var y = top; y < bottom; y++)
                {
                    for (var x = left; x < right; x++)
                    {
                        var i = y * w + x;
                        if (mask[i] && dist[i] > 0)
                        {
                            order.Add(i);
                        }
                    }
                }

                order.Sort((a, b) => dist[a].CompareTo(dist[b]));

                int[] dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
                int[] dy = { 0, 0, -1, 1, -1, 1, -1, 1 };

                for (var k = 0; k < order.Count; k++)
                {
                    var i = order[k];
                    var x = i % w;
                    var y = i / w;
                    var sumB = 0;
                    var sumG = 0;
                    var sumR = 0;
                    var sumA = 0;
                    var count = 0;
                    var weightSum = 0;
                    for (var n = 0; n < 8; n++)
                    {
                        var xx = x + dx[n];
                        var yy = y + dy[n];
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h)
                        {
                            continue;
                        }

                        var ni = yy * w + xx;
                        if (dist[ni] < 0 || dist[ni] >= dist[i])
                        {
                            continue;
                        }

                        var off = yy * stride + xx * 4;
                        var weight = n < 4 ? 2 : 1;
                        if (dx[n] < 0 || dy[n] < 0)
                        {
                            weight += 1;
                        }

                        sumB += pixels[off] * weight;
                        sumG += pixels[off + 1] * weight;
                        sumR += pixels[off + 2] * weight;
                        sumA += pixels[off + 3] * weight;
                        weightSum += weight;
                        count++;
                    }

                    if (count == 0 || weightSum == 0)
                    {
                        continue;
                    }

                    var dst = y * stride + x * 4;
                    pixels[dst] = (byte)(sumB / weightSum);
                    pixels[dst + 1] = (byte)(sumG / weightSum);
                    pixels[dst + 2] = (byte)(sumR / weightSum);
                    pixels[dst + 3] = (byte)(sumA / weightSum);
                    dist[i] = 0;
                }

                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static int[] BuildDistanceFromKnown(bool[] mask, int w, int h, Rectangle bounds)
        {
            var dist = new int[w * h];
            for (var i = 0; i < dist.Length; i++)
            {
                dist[i] = -1;
            }

            var q = new Queue<int>();
            var left = Math.Max(0, bounds.Left - 1);
            var top = Math.Max(0, bounds.Top - 1);
            var right = Math.Min(w, bounds.Right + 1);
            var bottom = Math.Min(h, bounds.Bottom + 1);

            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    var i = y * w + x;
                    if (!mask[i])
                    {
                        dist[i] = 0;
                        q.Enqueue(i);
                    }
                }
            }

            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            while (q.Count > 0)
            {
                var i = q.Dequeue();
                var x = i % w;
                var y = i / w;
                for (var n = 0; n < 4; n++)
                {
                    var xx = x + dx[n];
                    var yy = y + dy[n];
                    if (xx < left || yy < top || xx >= right || yy >= bottom)
                    {
                        continue;
                    }

                    var ni = yy * w + xx;
                    if (!mask[ni] || dist[ni] >= 0)
                    {
                        continue;
                    }

                    dist[ni] = dist[i] + 1;
                    q.Enqueue(ni);
                }
            }

            return dist;
        }

        private static Rectangle MaskBounds(bool[] mask, int w, int h)
        {
            var minX = w;
            var minY = h;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!mask[y * w + x])
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX)
            {
                return Rectangle.Empty;
            }

            return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        private static int CountTrue(bool[] mask)
        {
            var c = 0;
            for (var i = 0; i < mask.Length; i++)
            {
                if (mask[i])
                {
                    c++;
                }
            }

            return c;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
