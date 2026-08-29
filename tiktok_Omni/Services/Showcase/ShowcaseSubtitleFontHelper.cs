using System;
using System.Drawing;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chuẩn hóa tên font ASS/WinForms và thay font thiếu glyph tiếng Việt.</summary>
    public static class ShowcaseSubtitleFontHelper
    {
        /// <summary>Impact và một số display font không có đủ dấu tiếng Việt — dùng face nặng thay thế.</summary>
        public static string SubstituteIfWeakVietnamese(string fontFamily)
        {
            var n = (fontFamily ?? string.Empty).Trim();
            if (n.Length == 0)
            {
                return n;
            }

            if (string.Equals(n, "Impact", StringComparison.OrdinalIgnoreCase))
            {
                return "Arial Black";
            }

            return n;
        }

        /// <summary>Tách "Segoe UI Bold" → family + cờ Bold; Arial Black không bật faux-bold.</summary>
        public static void Normalize(ref string fontFamily, ref bool bold, ref bool italic)
        {
            var n = SubstituteIfWeakVietnamese(fontFamily);
            if (n.Length == 0)
            {
                fontFamily = "Segoe UI";
                bold = true;
                return;
            }

            if (n.EndsWith(" Bold", StringComparison.OrdinalIgnoreCase))
            {
                n = n.Substring(0, n.Length - 5).Trim();
                bold = true;
            }
            else if (n.EndsWith(" Black", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(n, "Arial Black", StringComparison.OrdinalIgnoreCase))
            {
                n = n.Substring(0, n.Length - 6).Trim();
                bold = true;
            }

            if (string.Equals(n, "Arial Black", StringComparison.OrdinalIgnoreCase))
            {
                bold = false;
            }

            fontFamily = n;
        }

        public static Font CreateDrawingFont(string fontFamily, bool bold, bool italic, float sizeInPoints, Font fallback)
        {
            var family = fontFamily;
            Normalize(ref family, ref bold, ref italic);

            var style = FontStyle.Regular;
            if (bold)
            {
                style |= FontStyle.Bold;
            }

            if (italic)
            {
                style |= FontStyle.Italic;
            }

            try
            {
                return new Font(family, sizeInPoints, style, GraphicsUnit.Point);
            }
            catch
            {
                try
                {
                    if (fallback != null)
                    {
                        return new Font(fallback.FontFamily, sizeInPoints, style, GraphicsUnit.Point);
                    }
                }
                catch
                {
                    // ignored
                }

                return fallback ?? SystemFonts.MessageBoxFont;
            }
        }

        public static void ApplyToOptions(AssSubtitleGeneratorOptions opts, string fontFamily, bool bold, bool italic)
        {
            if (opts == null)
            {
                return;
            }

            var family = fontFamily;
            Normalize(ref family, ref bold, ref italic);
            opts.FontName = family;
            opts.Bold = bold;
            opts.Italic = italic;
        }
    }
}
