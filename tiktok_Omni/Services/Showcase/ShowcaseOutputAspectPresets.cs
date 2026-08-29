using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

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
        public const string IdCustom = "custom";

        public const string DefaultId = Id9x16;

        public const int DefaultCustomWidth = 1080;
        public const int DefaultCustomHeight = 1350;
        public const int MinDimension = 480;
        public const int MaxDimension = 3840;

        public static ShowcaseOutputAspectPreset Vertical9x16 { get; } =
            new ShowcaseOutputAspectPreset(Id9x16, "Dọc 9:16", 1080, 1920,
                "Video xuất dọc 9:16 (1080×1920) — bố cục cảnh và voiceover phù hợp màn dọc, an toàn vùng chữ trên/dưới.");

        public static ShowcaseOutputAspectPreset Horizontal16x9 { get; } =
            new ShowcaseOutputAspectPreset(Id16x9, "Ngang 16:9", 1920, 1080,
                "Video xuất ngang 16:9 (1920×1080) — bố cục rộng, tránh chữ sát mép ngang.");

        public static ShowcaseOutputAspectPreset Square1x1 { get; } =
            new ShowcaseOutputAspectPreset(Id1x1, "Vuông 1:1", 1080, 1080,
                "Video xuất vuông 1:1 (1080×1080) — căn giữa chủ thể, chữ gọn giữa khung.");

        public static ShowcaseOutputAspectPreset CustomPlaceholder { get; } =
            new ShowcaseOutputAspectPreset(IdCustom, "Tùy chỉnh…", DefaultCustomWidth, DefaultCustomHeight,
                "Video xuất theo kích thước tùy chỉnh — căn chủ thể giữa khung, tránh chữ sát mép.");

        public static IReadOnlyList<ShowcaseOutputAspectPreset> All { get; } = new[]
        {
            Vertical9x16, Horizontal16x9, Square1x1, CustomPlaceholder
        };

        private static readonly Regex CustomSizePattern = new Regex(
            @"^\s*(\d{3,4})\s*[x×X*]\s*(\d{3,4})\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsCustomId(string id) =>
            string.Equals(NormalizeId(id), IdCustom, StringComparison.Ordinal);

        public static int ClampDimension(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return Math.Max(MinDimension, Math.Min(MaxDimension, value));
        }

        public static int NormalizeCustomWidth(int value) =>
            ClampDimension(value) > 0 ? ClampDimension(value) : DefaultCustomWidth;

        public static int NormalizeCustomHeight(int value) =>
            ClampDimension(value) > 0 ? ClampDimension(value) : DefaultCustomHeight;

        public static ShowcaseOutputAspectPreset BuildCustomPreset(int width, int height)
        {
            width = NormalizeCustomWidth(width);
            height = NormalizeCustomHeight(height);
            var label = "Tùy chỉnh " + width.ToString(CultureInfo.InvariantCulture)
                        + "×" + height.ToString(CultureInfo.InvariantCulture);
            var hint = "Video xuất tùy chỉnh (" + width.ToString(CultureInfo.InvariantCulture)
                       + "×" + height.ToString(CultureInfo.InvariantCulture)
                       + ") — căn chủ thể giữa khung, tránh chữ sát mép.";
            return new ShowcaseOutputAspectPreset(IdCustom, label, width, height, hint);
        }

        public static string FormatCustomSize(int width, int height) =>
            NormalizeCustomWidth(width).ToString(CultureInfo.InvariantCulture)
            + "×" + NormalizeCustomHeight(height).ToString(CultureInfo.InvariantCulture);

        public static bool TryParseCustomSize(string text, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = CustomSizePattern.Match(text.Trim());
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
            {
                width = 0;
                height = 0;
                return false;
            }

            width = NormalizeCustomWidth(width);
            height = NormalizeCustomHeight(height);
            return true;
        }

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

            if (s == "custom" || s == "tuy_chinh" || s == "tuỳ_chỉnh" || s == "tuy_chỉnh")
            {
                return IdCustom;
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

        public static ShowcaseOutputAspectPreset Resolve(
            string videoId,
            string appDefaultId = null,
            int customWidth = 0,
            int customHeight = 0)
        {
            var id = ResolveId(videoId, appDefaultId);
            if (IsCustomId(id))
            {
                return BuildCustomPreset(customWidth, customHeight);
            }

            return All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal) && !IsCustomId(p.Id))
                   ?? Vertical9x16;
        }

        public static ShowcaseOutputAspectPreset ResolveForVideo(ShowcaseVideoItem video, string appDefaultId = null)
        {
            if (video == null)
            {
                return Resolve(null, appDefaultId);
            }

            return Resolve(
                video.ShowcaseOutputAspectId,
                appDefaultId,
                video.ShowcaseOutputAspectCustomWidth,
                video.ShowcaseOutputAspectCustomHeight);
        }

        public static string GetDisplayLabel(string id, int customWidth = 0, int customHeight = 0) =>
            Resolve(id, null, customWidth, customHeight).DisplayLabel;

        public static string GetDisplayLabelForVideo(ShowcaseVideoItem video, string appDefaultId = null) =>
            ResolveForVideo(video, appDefaultId).DisplayLabel;

        public static string FormatScaleIncrease(int width, int height) =>
            "scale=" + width.ToString(CultureInfo.InvariantCulture) + ":" +
            height.ToString(CultureInfo.InvariantCulture) + ":force_original_aspect_ratio=increase";

        public static string FormatScaleCrop(int width, int height) =>
            "crop=" + width.ToString(CultureInfo.InvariantCulture) + ":" +
            height.ToString(CultureInfo.InvariantCulture);
    }
}
