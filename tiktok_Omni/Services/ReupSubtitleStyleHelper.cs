using System;

namespace tiktok_Omni.Services
{
    /// <summary>Vị trí dọc phụ đề ASS (alignment numpad).</summary>
    public enum ReupSubtitleVerticalPosition
    {
        Bottom = 2,
        Middle = 5,
        Top = 8
    }

    /// <summary>Kiểu chạy chữ karaoke hook Video reup.</summary>
    public enum ReupKaraokeAnimationMode
    {
        /// <summary>Phóng to từng từ (pop scale).</summary>
        Pop,

        /// <summary>Tô màu karaoke theo nhịp (\k).</summary>
        Highlight,

        /// <summary>Hiện dần từng từ (fade alpha).</summary>
        FadeIn,

        /// <summary>Hiện cả cụm — không hiệu ứng từng từ.</summary>
        Plain
    }

    /// <summary>Map cài đặt Video reup → <see cref="AssSubtitleGeneratorOptions"/>.</summary>
    public static class ReupSubtitleStyleHelper
    {
        public static readonly string[] FontChoices =
        {
            "Segoe UI Bold",
            "Arial Black",
            "Impact",
            "Tahoma Bold",
            "Arial",
            "Times New Roman",
            "Verdana Bold"
        };

        public static AssSubtitleGeneratorOptions BuildOptions(AppSettings settings)
        {
            return BuildOptions(null, settings);
        }

        public static AssSubtitleGeneratorOptions BuildOptions(VideoReupRowItem row, AppSettings fallbackSettings)
        {
            var s = fallbackSettings ?? new AppSettings();
            EnsureRowDefaults(row, s);

            var fontName = row != null
                ? (row.ReupSubtitleFontName ?? string.Empty).Trim()
                : (s.ReupSubtitleFontName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(fontName))
            {
                fontName = "Segoe UI Bold";
            }

            var fontSize = row != null && row.ReupSubtitleFontSize > 0
                ? row.ReupSubtitleFontSize
                : s.ReupSubtitleFontSize;
            fontSize = Math.Max(28, Math.Min(160, fontSize <= 0 ? 88 : fontSize));

            var positionRaw = row != null
                ? row.ReupSubtitlePosition
                : s.ReupSubtitlePosition;
            var position = ParsePosition(positionRaw);

            var animationRaw = row != null
                ? row.ReupSubtitleAnimation
                : s.ReupSubtitleAnimation;
            var animation = ParseAnimation(animationRaw);

            var wordsPerLine = row != null && row.ReupSubtitleWordsPerLine > 0
                ? row.ReupSubtitleWordsPerLine
                : s.ReupSubtitleWordsPerLine;
            wordsPerLine = Math.Max(4, Math.Min(8, wordsPerLine <= 0 ? 6 : wordsPerLine));

            var bold = row != null ? row.ReupSubtitleBold : s.ReupSubtitleBold;
            var italic = row != null ? row.ReupSubtitleItalic : s.ReupSubtitleItalic;

            return new AssSubtitleGeneratorOptions
            {
                FontName = fontName,
                FontSize = fontSize,
                Alignment = (int)position,
                MarginV = ResolveMarginV(position, s.ReupSubtitleMarginV),
                Animation = animation,
                WordsPerLine = wordsPerLine,
                Bold = bold,
                Italic = italic
            };
        }

        public static void EnsureRowDefaults(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return;
            }

            var s = settings ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(row.ReupSubtitleFontName))
            {
                row.ReupSubtitleFontName = s.ReupSubtitleFontName ?? "Segoe UI Bold";
            }

            if (row.ReupSubtitleFontSize <= 0)
            {
                row.ReupSubtitleFontSize = s.ReupSubtitleFontSize <= 0 ? 88 : s.ReupSubtitleFontSize;
            }

            if (string.IsNullOrWhiteSpace(row.ReupSubtitlePosition))
            {
                row.ReupSubtitlePosition = string.IsNullOrWhiteSpace(s.ReupSubtitlePosition)
                    ? "Bottom"
                    : s.ReupSubtitlePosition;
            }

            if (string.IsNullOrWhiteSpace(row.ReupSubtitleAnimation))
            {
                row.ReupSubtitleAnimation = string.IsNullOrWhiteSpace(s.ReupSubtitleAnimation)
                    ? "Pop"
                    : s.ReupSubtitleAnimation;
            }

            if (row.ReupSubtitleWordsPerLine <= 0)
            {
                row.ReupSubtitleWordsPerLine = s.ReupSubtitleWordsPerLine <= 0 ? 6 : s.ReupSubtitleWordsPerLine;
            }

            row.ReupSubtitleStyleLabel = FormatStyleSummary(row);
        }

        public static void ApplySettingsDefaultsToRow(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return;
            }

            var s = settings ?? new AppSettings();
            row.ReupSubtitleFontName = s.ReupSubtitleFontName ?? "Segoe UI Bold";
            row.ReupSubtitleFontSize = s.ReupSubtitleFontSize <= 0 ? 88 : s.ReupSubtitleFontSize;
            row.ReupSubtitlePosition = string.IsNullOrWhiteSpace(s.ReupSubtitlePosition) ? "Bottom" : s.ReupSubtitlePosition;
            row.ReupSubtitleAnimation = string.IsNullOrWhiteSpace(s.ReupSubtitleAnimation) ? "Pop" : s.ReupSubtitleAnimation;
            row.ReupSubtitleBold = s.ReupSubtitleBold;
            row.ReupSubtitleItalic = s.ReupSubtitleItalic;
            row.ReupSubtitleWordsPerLine = s.ReupSubtitleWordsPerLine <= 0 ? 6 : s.ReupSubtitleWordsPerLine;
            row.ReupSubtitleStyleLabel = FormatStyleSummary(row);
        }

        public static string FormatStyleSummary(VideoReupRowItem row)
        {
            if (row == null)
            {
                return "Chưa cấu hình";
            }

            var position = PositionDisplayLabel(ParsePosition(row.ReupSubtitlePosition));
            var font = ShortFontLabel(row.ReupSubtitleFontName);
            var size = row.ReupSubtitleFontSize > 0 ? row.ReupSubtitleFontSize : 88;
            var anim = AnimationDisplayLabel(ParseAnimation(row.ReupSubtitleAnimation));
            var traits = row.ReupSubtitleBold ? "Đậm" : "Thường";
            if (row.ReupSubtitleItalic)
            {
                traits += ", Nghiêng";
            }

            return position + " · " + font + " " + size + " · " + anim + " · " + traits;
        }

        public static string PositionDisplayLabel(ReupSubtitleVerticalPosition position)
        {
            switch (position)
            {
                case ReupSubtitleVerticalPosition.Top:
                    return "Trên";
                case ReupSubtitleVerticalPosition.Middle:
                    return "Giữa";
                default:
                    return "Dưới";
            }
        }

        public static string AnimationDisplayLabel(ReupKaraokeAnimationMode mode)
        {
            switch (mode)
            {
                case ReupKaraokeAnimationMode.Highlight:
                    return "Karaoke";
                case ReupKaraokeAnimationMode.FadeIn:
                    return "Hiện dần";
                case ReupKaraokeAnimationMode.Plain:
                    return "Cả dòng";
                default:
                    return "Pop";
            }
        }

        private static string ShortFontLabel(string fontName)
        {
            var font = (fontName ?? string.Empty).Trim();
            if (font.Length <= 14)
            {
                return string.IsNullOrEmpty(font) ? "Segoe UI" : font;
            }

            return font.Substring(0, 12) + "…";
        }

        public static string PositionToStorage(ReupSubtitleVerticalPosition position)
        {
            switch (position)
            {
                case ReupSubtitleVerticalPosition.Top:
                    return "Top";
                case ReupSubtitleVerticalPosition.Middle:
                    return "Middle";
                default:
                    return "Bottom";
            }
        }

        public static ReupSubtitleVerticalPosition ParsePosition(string value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.Equals(v, "Top", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Trên", StringComparison.OrdinalIgnoreCase))
            {
                return ReupSubtitleVerticalPosition.Top;
            }

            if (string.Equals(v, "Middle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Giữa", StringComparison.OrdinalIgnoreCase))
            {
                return ReupSubtitleVerticalPosition.Middle;
            }

            return ReupSubtitleVerticalPosition.Bottom;
        }

        public static string AnimationToStorage(ReupKaraokeAnimationMode mode)
        {
            switch (mode)
            {
                case ReupKaraokeAnimationMode.Highlight:
                    return "Highlight";
                case ReupKaraokeAnimationMode.FadeIn:
                    return "FadeIn";
                case ReupKaraokeAnimationMode.Plain:
                    return "Plain";
                default:
                    return "Pop";
            }
        }

        public static ReupKaraokeAnimationMode ParseAnimation(string value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.Equals(v, "Highlight", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Karaoke", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Tô màu", StringComparison.OrdinalIgnoreCase))
            {
                return ReupKaraokeAnimationMode.Highlight;
            }

            if (string.Equals(v, "FadeIn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Fade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Hiện dần", StringComparison.OrdinalIgnoreCase))
            {
                return ReupKaraokeAnimationMode.FadeIn;
            }

            if (string.Equals(v, "Plain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "Cả dòng", StringComparison.OrdinalIgnoreCase))
            {
                return ReupKaraokeAnimationMode.Plain;
            }

            return ReupKaraokeAnimationMode.Pop;
        }

        public static int ResolveMarginV(ReupSubtitleVerticalPosition position, int configuredMargin)
        {
            if (configuredMargin > 0)
            {
                return Math.Max(20, Math.Min(400, configuredMargin));
            }

            switch (position)
            {
                case ReupSubtitleVerticalPosition.Top:
                    return 100;
                case ReupSubtitleVerticalPosition.Middle:
                    return 0;
                default:
                    return 140;
            }
        }
    }
}
