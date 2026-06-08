using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    public sealed class ManualProductFetchResult
    {
        public string ProductName { get; set; } = string.Empty;
        public string Price { get; set; } = "N/A";
        public List<string> ImageUrls { get; set; } = new List<string>();
        public List<string> CustomerReviews { get; set; } = new List<string>();
        public string AffiliateLink { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
    }
}
