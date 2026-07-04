using System;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Map cài đặt phụ đề từng dòng tab Video Triết lý → ASS karaoke.</summary>
    public static class PhilosophySubtitleStyleHelper
    {
        public static void ApplyPhilosophyDefaults(PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            var defaults = PhilosophyVideoPipelineService.CreatePhilosophyKaraokeOptions();
            item.SubtitlePosition = "Middle";
            item.SubtitleFontName = defaults.FontName ?? "Times New Roman";
            item.SubtitleFontSize = defaults.FontSize > 0 ? defaults.FontSize : 76;
            item.SubtitleAnimation = ReupSubtitleStyleHelper.AnimationToStorage(defaults.Animation);
            item.SubtitleBold = defaults.Bold;
            item.SubtitleItalic = defaults.Italic;
            item.SubtitleWordsPerLine = defaults.WordsPerLine > 0 ? defaults.WordsPerLine : 8;
            item.SubtitlePrimaryColourAss = defaults.PrimaryColourAss ?? "&H00FFFFFF";
            item.SubtitleSecondaryColourAss = defaults.SecondaryColourAss ?? "&H00D7FF00";
            item.SubtitleStyleLabel = FormatStyleSummary(item);
        }

        public static void EnsureDefaults(PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.SubtitleFontName))
            {
                ApplyPhilosophyDefaults(item);
                return;
            }

            if (item.SubtitleFontSize <= 0)
            {
                item.SubtitleFontSize = 76;
            }

            if (string.IsNullOrWhiteSpace(item.SubtitlePosition))
            {
                item.SubtitlePosition = "Middle";
            }

            if (string.IsNullOrWhiteSpace(item.SubtitleAnimation))
            {
                item.SubtitleAnimation = "Highlight";
            }

            if (item.SubtitleWordsPerLine <= 0)
            {
                item.SubtitleWordsPerLine = 8;
            }

            if (string.IsNullOrWhiteSpace(item.SubtitlePrimaryColourAss))
            {
                item.SubtitlePrimaryColourAss = "&H00FFFFFF";
            }

            if (string.IsNullOrWhiteSpace(item.SubtitleSecondaryColourAss))
            {
                item.SubtitleSecondaryColourAss = "&H00D7FF00";
            }

            item.SubtitleStyleLabel = FormatStyleSummary(item);
        }

        public static AssSubtitleGeneratorOptions BuildOptions(PhilosophyScriptItem item)
        {
            EnsureDefaults(item);
            var position = ReupSubtitleStyleHelper.ParsePosition(item.SubtitlePosition);
            var animation = ReupSubtitleStyleHelper.ParseAnimation(item.SubtitleAnimation);
            var fontSize = Math.Max(28, Math.Min(160, item.SubtitleFontSize <= 0 ? 76 : item.SubtitleFontSize));
            var wordsPerLine = Math.Max(4, Math.Min(12, item.SubtitleWordsPerLine <= 0 ? 8 : item.SubtitleWordsPerLine));

            return new AssSubtitleGeneratorOptions
            {
                FontName = (item.SubtitleFontName ?? "Times New Roman").Trim(),
                FontSize = fontSize,
                Alignment = (int)position,
                MarginV = ResolvePhilosophyMarginV(position),
                Animation = animation,
                WordsPerLine = wordsPerLine,
                RhythmicLineBreaks = true,
                Bold = item.SubtitleBold,
                Italic = item.SubtitleItalic,
                PrimaryColourAss = item.SubtitlePrimaryColourAss,
                SecondaryColourAss = item.SubtitleSecondaryColourAss
            };
        }

        public static string FormatStyleSummary(PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return "Chưa cấu hình";
            }

            var position = ReupSubtitleStyleHelper.PositionDisplayLabel(
                ReupSubtitleStyleHelper.ParsePosition(
                    string.IsNullOrWhiteSpace(item.SubtitlePosition) ? "Middle" : item.SubtitlePosition));
            var font = ShortFontLabel(
                string.IsNullOrWhiteSpace(item.SubtitleFontName) ? "Times New Roman" : item.SubtitleFontName);
            var size = item.SubtitleFontSize > 0 ? item.SubtitleFontSize : 76;
            var anim = ReupSubtitleStyleHelper.AnimationDisplayLabel(
                ReupSubtitleStyleHelper.ParseAnimation(
                    string.IsNullOrWhiteSpace(item.SubtitleAnimation) ? "Highlight" : item.SubtitleAnimation));
            var traits = item.SubtitleBold ? "Đậm" : "Thường";
            if (item.SubtitleItalic)
            {
                traits += ", Nghiêng";
            }

            return position + " · " + font + " " + size + " · " + anim + " · " + traits;
        }

        public static int ResolvePhilosophyMarginV(ReupSubtitleVerticalPosition position)
        {
            switch (position)
            {
                case ReupSubtitleVerticalPosition.Top:
                    return 120;
                case ReupSubtitleVerticalPosition.Middle:
                    return 300;
                default:
                    return 220;
            }
        }

        private static string ShortFontLabel(string fontName)
        {
            var font = (fontName ?? string.Empty).Trim();
            if (font.Length <= 14)
            {
                return string.IsNullOrEmpty(font) ? "Times New Roman" : font;
            }

            return font.Substring(0, 12) + "…";
        }
    }
}
