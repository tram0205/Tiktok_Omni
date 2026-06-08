using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public static class MascotMouthPositionEstimator
    {
        public const int RefWidth = 1080;
        public const int RefHeight = 1920;
        public const int DefaultX = 420;
        public const int DefaultY = 1180;

        public static Task<Point> EstimateMouthOverlayAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => EstimateMouthOverlay(imagePath, cancellationToken), cancellationToken);
        }

        public static Point EstimateMouthOverlay(string imagePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new Point(DefaultX, DefaultY);
            }

            try
            {
                using (var src = Image.FromFile(imagePath))
                {
                    var scale = Math.Min(RefWidth / (double)src.Width, RefHeight / (double)src.Height);
                    var drawW = Math.Max(1, (int)Math.Round(src.Width * scale));
                    var drawH = Math.Max(1, (int)Math.Round(src.Height * scale));
                    var offsetX = (RefWidth - drawW) / 2;
                    var offsetY = (RefHeight - drawH) / 2;
                    var mouthY = offsetY + (int)(drawH * 0.68);
                    var mouthX = offsetX + drawW / 2;
                    return new Point(Clamp(mouthX, 80, RefWidth - 80), Clamp(mouthY, RefHeight / 2, RefHeight - 120));
                }
            }
            catch
            {
                return new Point(DefaultX, DefaultY);
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static string GetDlibIntegrationHint()
        {
            return "DlibDotNet: cai shape_predictor_68_face_landmarks.dat, landmark moi 48-54, map sang 1080x1920.";
        }
    }
}