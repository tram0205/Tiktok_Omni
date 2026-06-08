using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    /// <summary>Cấu hình Avatar Identity Pack theo profile (AvatarVault/{profile}/identity_pack.json).</summary>
    public sealed class AvatarIdentityPackConfig
    {
        public List<string> IdentityImagePaths { get; set; } = new List<string>();

        public string MouthClosedPath { get; set; } = string.Empty;

        /// <summary>Miệng mở nhỏ (Open-Small).</summary>
        public string MouthOpenSmallPath { get; set; } = string.Empty;

        /// <summary>Miệng mở lớn (Open-Large). Alias cột UI: MouthOpenPath.</summary>
        public string MouthOpenPath { get; set; } = string.Empty;

        /// <summary>Tọa độ X (pixel) overlay miệng trên khung 1080×1920.</summary>
        public int MouthOverlayX { get; set; } = 420;

        /// <summary>Tọa độ Y (pixel) overlay miệng trên khung 1080×1920.</summary>
        public int MouthOverlayY { get; set; } = 1180;

        /// <summary>Tỷ lệ preview (0.1–1) so với kích thước ảnh miệng gốc.</summary>
        public double MouthOverlayScale { get; set; } = 1.0d;

        public string EmotionHappyPath { get; set; } = string.Empty;
        public string EmotionSadPath { get; set; } = string.Empty;
        public string EmotionSurprisedPath { get; set; } = string.Empty;
        public string EmotionNeutralPath { get; set; } = string.Empty;
    }
}
