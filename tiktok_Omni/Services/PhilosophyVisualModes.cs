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

        public static readonly string[] Labels =
        {
            "1. Kho B-Roll có sẵn",
            "2. AI tự sinh (Cảnh vật/Nhân vật)",
            "2. AI tự sinh (Cảnh vật/Nhân vật)",   // VeoMascot — hiển thị chung nhóm AI
            "3. Video nhân vật tự làm sẵn"
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
                default: return "1. Kho B-Roll có sẵn";
            }
        }

        public static int FromLabel(string label)
        {
            var text = (label ?? string.Empty).Trim();

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
            return Math.Max(0, Math.Min(2, mode));
        }
    }
}
