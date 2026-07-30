using System;

namespace tiktok_Omni.Services.Affiliate
{
    public sealed class AffiliateHuntContext
    {
        public AffiliateSearchMode TikTokSearchMode { get; set; } = AffiliateSearchMode.Video;
        public ConfigManager ConfigManager { get; set; }
        public string RunningProfileName { get; set; }
        public Action<string> Log { get; set; }
        public string YtDlpPath { get; set; }
        public string TikTokHuntMethod { get; set; } = string.Empty;
        public bool TikTokRapidApiFallbackToBrowser { get; set; } = true;
    }
}
