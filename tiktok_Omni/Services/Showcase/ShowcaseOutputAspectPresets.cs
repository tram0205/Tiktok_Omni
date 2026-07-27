using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseOutputAspectPreset
    {
        public ShowcaseOutputAspectPreset(string id, string displayLabel, int width, int height, string geminiHint)
        {
            Id = id ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
            Width = width;
            Height = height;
            GeminiHint = geminiHint ?? string.Empty;
        }

        public string Id { get; }

        public string DisplayLabel { get; }

        public int Width { get; }

        public int Height { get; }

        public string GeminiHint { get; }

        public double AspectRatio => Width / (double)Math.Max(1, Height);

        public override string ToString() => DisplayLabel;
    }

    /// <summary>Khung xuất Showcase — lưới «Khung video» + FFmpeg/ASS.</summary>
    public static class ShowcaseOutputAspectPresets
    {
        public const string Id9x16 = "9x16";
        public const string Id16x9 = "16x9";
        public const string Id1x1 = "1x1";

        public const string DefaultId = Id9x16;

        public static ShowcaseOutputAspectPreset Vertical9x16 { get; } =
            new ShowcaseOutputAspectPreset(Id9x16, "Dọc 9:16", 1080, 1920,
                "Video xuất dọc 9:16 (1080×1920) — bố cục cảnh và voiceover phù hợp màn dọc, an toàn vùng chữ trên/dưới.");

        public static ShowcaseOutputAspectPreset Horizontal16x9 { get; } =
            new ShowcaseOutputAspectPreset(Id16x9, "Ngang 16:9", 1920, 1080,
                "Video xuất ngang 16:9 (1920×1080) — bố cục rộng, tránh chữ sát mép ngang.");

        public static ShowcaseOutputAspectPreset Square1x1 { get; } =
            new ShowcaseOutputAspectPreset(Id1x1, "Vuông 1:1", 1080, 1080,
                "Video xuất vuông 1:1 (1080×1080) — căn giữa chủ thể, chữ gọn giữa khung.");

        public static IReadOnlyList<ShowcaseOutputAspectPreset> All { get; } = new[]
        {
            Vertical9x16, Horizontal16x9, Square1x1
        };

        public static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var s = raw.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            if (s == "916" || s == "9_16" || s == "vertical" || s == "tiktok" || s == "dọc" || s == "doc")
            {
                return Id9x16;
            }

            if (s == "169" || s == "16_9" || s == "horizontal" || s == "youtube" || s == "ngang")
            {
                return Id16x9;
            }

            if (s == "11" || s == "1_1" || s == "square" || s == "vuong" || s == "vuông")
            {
                return Id1x1;
            }

            return All.Any(p => string.Equals(p.Id, s, StringComparison.Ordinal)) ? s : string.Empty;
        }

        public static string ResolveId(string videoId, string appDefaultId)
        {
            var fromVideo = NormalizeId(videoId);
            if (!string.IsNullOrEmpty(fromVideo))
            {
                return fromVideo;
            }

            var fromApp = NormalizeId(appDefaultId);
            return !string.IsNullOrEmpty(fromApp) ? fromApp : DefaultId;
        }

        public static ShowcaseOutputAspectPreset Resolve(string videoId, string appDefaultId = null)
        {
            var id = ResolveId(videoId, appDefaultId);
            return All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal)) ?? Vertical9x16;
        }

        public static string GetDisplayLabel(string id) => Resolve(id, null).DisplayLabel;

        public static string FormatScaleIncrease(int width, int height) =>
            "scale=" + width.ToString(CultureInfo.InvariantCulture) + ":" +
            height.ToString(CultureInfo.InvariantCulture) + ":force_original_aspect_ratio=increase";

        public static string FormatScaleCrop(int width, int height) =>
            "crop=" + width.ToString(CultureInfo.InvariantCulture) + ":" +
            height.ToString(CultureInfo.InvariantCulture);
    }
}
