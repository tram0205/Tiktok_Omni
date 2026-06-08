using System;
using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    public sealed class AffiliateRevenueReportFile
    {
        public DateTime? LastFetchedUtc { get; set; }
        public string ProfileName { get; set; } = string.Empty;
        public List<AffiliateRevenueItem> Items { get; set; } = new List<AffiliateRevenueItem>();
    }
}
