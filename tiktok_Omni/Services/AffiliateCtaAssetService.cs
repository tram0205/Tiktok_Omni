using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace tiktok_Omni.Services
{
    /// <summary>Tạo / resolve PNG mũi tên giỏ hàng (transparent) cho lớp phủ CTA cuối video.</summary>
    public static class AffiliateCtaAssetService
    {
        public const string CtaFileName = "cta_cart_arrow.png";

        public static string EnsureCartArrowAsset(string storageRoot = null)
        {
            var assetsDir = ResolveAssetsDirectory(storageRoot);
            Directory.CreateDirectory(assetsDir);
            var path = Path.Combine(assetsDir, CtaFileName);
            if (File.Exists(path) && new FileInfo(path).Length > 32)
            {
                return path;
            }

            try
            {
                RenderCartArrowPng(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không tạo được asset CTA: " + ex.Message, ex);
            }

            return path;
        }

        public static string ResolveAssetsDirectory(string storageRoot)
        {
            if (!string.IsNullOrWhiteSpace(storageRoot))
            {
                var underRoot = Path.Combine(storageRoot.Trim(), "Assets");
                if (Directory.Exists(underRoot) || TryCreate(underRoot))
                {
                    return underRoot;
                }
            }

            var exeAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            Directory.CreateDirectory(exeAssets);
            return exeAssets;
        }

        private static bool TryCreate(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RenderCartArrowPng(string outputPath)
        {
            const int w = 280;
            const int h = 280;
            using (var bmp = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.FromArgb(240, 255, 80, 0)))
                using (var pen = new Pen(Color.FromArgb(255, 255, 255, 255), 6f))
                {
                    var arrow = new Point[]
                    {
                        new Point(40, 140),
                        new Point(170, 140),
                        new Point(150, 110),
                        new Point(220, 150),
                        new Point(150, 190),
                        new Point(170, 160),
                        new Point(40, 160)
                    };
                    g.FillPolygon(brush, arrow);
                    g.DrawPolygon(pen, arrow);
                    g.FillEllipse(brush, 70, 175, 50, 50);
                    g.DrawEllipse(pen, 70, 175, 50, 50);
                    g.FillEllipse(brush, 150, 175, 50, 50);
                    g.DrawEllipse(pen, 150, 175, 50, 50);
                }

                bmp.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
