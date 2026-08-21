using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>Thumbnail preview cho cột B-roll (ffmpeg frame + cache đĩa).</summary>
    public static class PhilosophyBrollThumbnailHelper
    {
        private static readonly ConcurrentDictionary<string, Image> MemoryCache =
            new ConcurrentDictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static string GetCacheRootDirectory()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "tiktok_Omni",
                "broll-thumbs");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string ResolvePreviewVideoPath(string selection, string profileName)
        {
            var trimmed = (selection ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed) || PhilosophyBRollSelection.IsRandomToken(trimmed))
            {
                return string.Empty;
            }

            if (File.Exists(trimmed) && IsVideoFile(trimmed))
            {
                return trimmed;
            }

            if (Directory.Exists(trimmed))
            {
                return Directory
                    .GetFiles(trimmed, "*.mp4", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(trimmed, "*.mov", SearchOption.TopDirectoryOnly))
                    .FirstOrDefault(IsVideoFile)
                    ?? string.Empty;
            }

            return string.Empty;
        }

        public static Image TryGetThumbnail(string selection, string profileName)
        {
            var videoPath = ResolvePreviewVideoPath(selection, profileName);
            if (string.IsNullOrEmpty(videoPath))
            {
                return null;
            }

            var cacheKey = videoPath;
            if (MemoryCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var diskPath = BuildDiskCachePath(videoPath);
            if (File.Exists(diskPath))
            {
                try
                {
                    using (var stream = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var img = Image.FromStream(stream);
                        MemoryCache[cacheKey] = img;
                        return img;
                    }
                }
                catch
                {
                    // fall through to extract
                }
            }

            var ffmpeg = ResolveFfmpegPath();
            if (string.IsNullOrEmpty(ffmpeg))
            {
                return null;
            }

            if (!ReupPreviewFrameService.TryExtractFrame(ffmpeg, videoPath, diskPath, 1.0))
            {
                return null;
            }

            try
            {
                using (var stream = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var img = Image.FromStream(stream);
                    MemoryCache[cacheKey] = img;
                    return img;
                }
            }
            catch
            {
                return null;
            }
        }

        public static Image ResizeToThumb(Image source, int maxWidth)
        {
            if (source == null || maxWidth <= 0)
            {
                return null;
            }

            var maxHeight = (int)Math.Round(maxWidth * 16.0 / 9.0);
            var bmp = new Bitmap(maxWidth, maxHeight);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(28, 32, 40));
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                var dest = FitRectPreserveAspect(source.Width, source.Height, new Rectangle(0, 0, maxWidth, maxHeight));
                g.DrawImage(source, dest);
            }

            return bmp;
        }

        public static void DrawThumbnail(Graphics graphics, Rectangle bounds, Image image, bool selected)
        {
            if (graphics == null || image == null || bounds.Width <= 4 || bounds.Height <= 4)
            {
                return;
            }

            var inner = Rectangle.Inflate(bounds, -3, -3);
            graphics.SetClip(bounds);
            using (var brush = new SolidBrush(selected ? Color.FromArgb(52, 58, 72) : Color.FromArgb(28, 32, 40)))
            {
                graphics.FillRectangle(brush, inner);
            }

            var dest = FitRectPreserveAspect(image.Width, image.Height, inner);
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(image, dest);
            using (var border = new Pen(Color.FromArgb(70, 78, 96)))
            {
                graphics.DrawRectangle(border, dest);
            }

            graphics.ResetClip();
        }

        public static void DrawPlaceholder(
            Graphics graphics,
            Rectangle bounds,
            string selection,
            bool selected)
        {
            if (graphics == null || bounds.Width <= 4 || bounds.Height <= 4)
            {
                return;
            }

            var inner = Rectangle.Inflate(bounds, -3, -3);
            var trimmed = (selection ?? string.Empty).Trim();
            var isRandom = PhilosophyBRollSelection.IsRandomToken(trimmed);
            var back = selected ? Color.FromArgb(52, 58, 72) : Color.FromArgb(28, 32, 40);
            using (var brush = new SolidBrush(back))
            {
                graphics.FillRectangle(brush, inner);
            }

            var label = string.IsNullOrEmpty(trimmed)
                ? "Chọn…"
                : isRandom
                    ? "Ngẫu nhiên"
                    : "Thiếu preview";
            var icon = isRandom ? "🎲" : string.Empty;
            using (var font = new Font("Segoe UI", 8.5F, FontStyle.Regular))
            using (var textBrush = new SolidBrush(Color.FromArgb(175, 182, 196)))
            {
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                var text = string.IsNullOrEmpty(icon) ? label : icon + "\r\n" + label;
                graphics.DrawString(text, font, textBrush, inner, format);
            }

            using (var border = new Pen(Color.FromArgb(70, 78, 96)))
            {
                graphics.DrawRectangle(border, inner);
            }
        }

        public static string BuildTooltip(string selection)
        {
            var trimmed = (selection ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return "Bấm để chọn B-roll";
            }

            return PhilosophyBRollSelection.DescribeTooltip(trimmed, string.Empty);
        }

        private static Rectangle FitRectPreserveAspect(int srcW, int srcH, Rectangle bounds)
        {
            if (srcW <= 0 || srcH <= 0)
            {
                return bounds;
            }

            var scale = Math.Min(
                bounds.Width / (double)srcW,
                bounds.Height / (double)srcH);
            var w = Math.Max(1, (int)Math.Round(srcW * scale));
            var h = Math.Max(1, (int)Math.Round(srcH * scale));
            var x = bounds.X + (bounds.Width - w) / 2;
            var y = bounds.Y + (bounds.Height - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        private static string BuildDiskCachePath(string videoPath)
        {
            var full = Path.GetFullPath(videoPath);
            var stamp = File.GetLastWriteTimeUtc(full).Ticks.ToString();
            var hash = ComputeHash(full + "|" + stamp);
            return Path.Combine(GetCacheRootDirectory(), hash + ".jpg");
        }

        private static string ComputeHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private static bool IsVideoFile(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 10_000L;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveFfmpegPath()
        {
            var bundled = FfmpegToolkitService.GetBundledFfmpegPath();
            return File.Exists(bundled) ? bundled : string.Empty;
        }
    }
}
