using System;
using System.Globalization;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Tốc độ/biên độ Ken Burns — Gemini gợi ý <c>zoom_speed</c> từng cảnh zoom.</summary>
    public static class ShowcaseZoomSpeedCatalog
    {
        public const string Slow = "slow";
        public const string Medium = "medium";
        public const string Fast = "fast";

        public const string DefaultId = Medium;

        private static readonly string[] ValidIds = { Slow, Medium, Fast };

        /// <summary>Chuỗi liệt kê cho prompt Gemini.</summary>
        public static string BuildGeminiSpeedList() => "'slow', 'medium', 'fast'";

        public static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var s = raw.Trim().ToLowerInvariant()
                .Replace(' ', '_')
                .Replace('-', '_');

            if (s == "very_slow" || s == "veryslow" || s == "gentle" || s == "subtle")
            {
                return Slow;
            }

            if (s == "quick" || s == "snappy" || s == "brisk")
            {
                return Fast;
            }

            foreach (var id in ValidIds)
            {
                if (string.Equals(s, id, StringComparison.Ordinal))
                {
                    return id;
                }
            }

            return string.Empty;
        }

        /// <summary>Ưu tiên zoom_speed từ JSON; không có thì suy từ zoom_hint; mặc định medium.</summary>
        public static string ResolveSpeedId(string zoomSpeedFromGemini, string zoomHint)
        {
            var fromField = NormalizeId(zoomSpeedFromGemini);
            if (!string.IsNullOrEmpty(fromField))
            {
                return fromField;
            }

            var fromHint = InferFromZoomHint(zoomHint);
            return string.IsNullOrEmpty(fromHint) ? DefaultId : fromHint;
        }

        public static string InferFromZoomHint(string zoomHint)
        {
            var h = (zoomHint ?? string.Empty).Trim().ToLowerInvariant();
            if (h.Length == 0)
            {
                return string.Empty;
            }

            if (h.Contains("very slow") || h.Contains("ultra slow") || h.Contains("extremely slow")
                || h.Contains("creep") || h.Contains("gentle") || h.Contains("subtle")
                || h.Contains("delicate") || h.Contains("soft zoom"))
            {
                return Slow;
            }

            if (h.Contains("fast") || h.Contains("quick") || h.Contains("snappy") || h.Contains("brisk")
                || h.Contains("rapid") || h.Contains("dynamic zoom") || h.Contains("energetic"))
            {
                return Fast;
            }

            if (h.Contains("slow"))
            {
                return Slow;
            }

            return string.Empty;
        }

        public static string GetDisplayLabel(string speedId)
        {
            switch (NormalizeId(speedId))
            {
                case Slow:
                    return "Chậm";
                case Fast:
                    return "Nhanh";
                default:
                    return "Vừa";
            }
        }

        /// <summary>Biên độ scale/pan — delta = % zoom thêm; panTravel càng lớn pan càng chậm.</summary>
        public static (double Delta, string PanTravel) GetMotionParameters(string speedId, int clipIndex)
        {
            var speed = NormalizeId(speedId);
            if (string.IsNullOrEmpty(speed))
            {
                speed = DefaultId;
            }

            var idx = Math.Max(0, clipIndex) % 3;
            switch (speed)
            {
                case Slow:
                    return (0.08d + (idx * 0.025d), "2.85");
                case Fast:
                    return (0.20d + (idx * 0.04d), "1.55");
                default:
                    return (0.14d + (idx * 0.04d), "2.15");
            }
        }

        public static string FormatDeltaLog(string speedId, int clipIndex)
        {
            var (delta, panTravel) = GetMotionParameters(speedId, clipIndex);
            return NormalizeId(speedId) + " delta=" + delta.ToString("0.##", CultureInfo.InvariantCulture)
                   + " pan=" + panTravel;
        }
    }
}
