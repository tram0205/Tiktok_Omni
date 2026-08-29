using System;

namespace tiktok_Omni.Services
{
    public static class PhilosophyVisualModes
    {
        public const int Broll = 0;

        public const int VeoScenery = 1;

        public const int VeoMascot = 2;

        /// <summary>Video phân cảnh do người dùng tự render sẵn, quét theo tên file quy ước.</summary>
        public const int PreRendered = 3;

        /// <summary>Ken Burns zoom từ ảnh tĩnh — thư viện zoom-images theo profile.</summary>
        public const int ImageZoom = 4;

        /// <summary>Slideshow ảnh — crossfade, không zoom.</summary>
        public const int ImageSlideshow = 5;

        /// <summary>Gemini sinh 1 ảnh tĩnh (mascot) rồi Ken Burns zoom.</summary>
        public const int AiStillZoom = 6;

        /// <summary>Zoom ảnh phần chính + B-roll outro cuối.</summary>
        public const int ZoomBrollHybrid = 7;

        public static bool UsesZoomImageLibrary(int mode)
        {
            switch (Normalize(mode))
            {
                case ImageZoom:
                case ImageSlideshow:
                case ZoomBrollHybrid:
                    return true;
                default:
                    return false;
            }
        }

        public static readonly string[] Labels =
        {
            "1. Kho B-Roll có sẵn",
            "2. AI tự sinh (Cảnh vật/Nhân vật)",
            "2. AI tự sinh (Cảnh vật/Nhân vật)",
            "3. Video nhân vật tự làm sẵn",
            "4. Zoom ảnh (Ken Burns)",
            "5. Slideshow ảnh (crossfade)",
            "6. AI ảnh → zoom",
            "7. Zoom + B-roll outro"
        };

        /// <summary>Nhãn hiển thị trong ComboBox cột lưới (3 tuỳ chọn thực sự).</summary>
        public static readonly string[] ComboLabels =
        {
            "1. Kho B-Roll có sẵn",
            "2. AI tự sinh (Cảnh vật/Nhân vật)",
            "3. Video nhân vật tự làm sẵn"
        };

        public static string ToLabel(int mode)
        {
            switch (mode)
            {
                case Broll: return "1. Kho B-Roll có sẵn";
                case VeoScenery: return "2. AI tự sinh (Cảnh vật/Nhân vật)";
                case VeoMascot: return "2. AI tự sinh (Cảnh vật/Nhân vật)";
                case PreRendered: return "3. Video nhân vật tự làm sẵn";
                case ImageZoom: return "4. Zoom ảnh (Ken Burns)";
                case ImageSlideshow: return "5. Slideshow ảnh (crossfade)";
                case AiStillZoom: return "6. AI ảnh → zoom";
                case ZoomBrollHybrid: return "7. Zoom + B-roll outro";
                default: return "1. Kho B-Roll có sẵn";
            }
        }

        public static int FromLabel(string label)
        {
            var text = (label ?? string.Empty).Trim();

            if (text.StartsWith("7.", StringComparison.Ordinal)
                || text.IndexOf("zoom + b-roll", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("hybrid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ZoomBrollHybrid;
            }

            if (text.StartsWith("6.", StringComparison.Ordinal)
                || text.IndexOf("ai ảnh", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("ai anh", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AiStillZoom;
            }

            if (text.StartsWith("5.", StringComparison.Ordinal)
                || text.IndexOf("slideshow", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("crossfade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ImageSlideshow;
            }

            if (text.StartsWith("4.", StringComparison.Ordinal)
                || (text.IndexOf("zoom", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("b-roll", StringComparison.OrdinalIgnoreCase) < 0
                    && text.IndexOf("slideshow", StringComparison.OrdinalIgnoreCase) < 0))
            {
                return ImageZoom;
            }

            if (text.StartsWith("3.", StringComparison.Ordinal)
                || text.IndexOf("tự làm", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("pre", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PreRendered;
            }

            if (text.StartsWith("2.", StringComparison.Ordinal)
                || text.IndexOf("ai tự sinh", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("nhân vật", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("mascot", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("cảnh vật", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("veo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VeoScenery;
            }

            if (text.StartsWith("1.", StringComparison.Ordinal)
                || text.IndexOf("b-roll", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("broll", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Broll;
            }

            return Broll;
        }

        public static int Normalize(int mode)
        {
            if (mode == PreRendered) return PreRendered;
            if (mode == ImageZoom) return ImageZoom;
            if (mode == ImageSlideshow) return ImageSlideshow;
            if (mode == AiStillZoom) return AiStillZoom;
            if (mode == ZoomBrollHybrid) return ZoomBrollHybrid;
            return Math.Max(0, Math.Min(2, mode));
        }
    }
}
