using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services
{
    public class AiVideoGenInputItem
    {
        public string ProfileName { get; set; } = string.Empty;
        public string SourceKeyword { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string AffiliateLink { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Top reviews — lưu dạng chuỗi nối bằng ||| hoặc set qua <see cref="SetCustomerReviews"/>.</summary>
        public string CustomerReviews { get; set; } = string.Empty;

        public bool IsProcessed { get; set; }
        public string PipelineStatus { get; set; } = "Chờ";
        public int SafetyScore { get; set; } = 100;
        public int ProgressPercent { get; set; }
        public string ThumbnailPath { get; set; } = string.Empty;
        public string OutputVideoPath { get; set; } = string.Empty;
        public string ScriptPreview { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public void SetCustomerReviews(IEnumerable<string> reviews)
        {
            var list = (reviews ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Take(3)
                .ToList();
            CustomerReviews = list.Count == 0 ? string.Empty : string.Join("|||", list);
        }

        public List<string> GetCustomerReviewsList()
        {
            if (string.IsNullOrWhiteSpace(CustomerReviews))
            {
                return new List<string>();
            }

            return CustomerReviews
                .Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Take(3)
                .ToList();
        }
    }
}
