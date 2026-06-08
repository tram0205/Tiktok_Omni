using System;

namespace tiktok_Omni.Services
{
    public sealed class AffiliateRevenueItem
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public double Revenue { get; set; }
        public double Commission { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
