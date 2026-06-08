using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public enum ApprovalJobType
    {
        RenderVideo = 1,
        AutoPost = 2,
        PhilosophyVideo = 3,
        Slideshow = 4,
        AffiliateDeep = 5,
        Mascot = 6,
        VideoReup = 7
    }

    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Completed = 4
    }

    public class ApprovalQueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ApprovalJobType JobType { get; set; }
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public string Profile { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public int SafetyScore { get; set; }
        public bool RequiresManualApproval { get; set; }
        public string RiskReasons { get; set; } = string.Empty;
        public string OriginalPreview { get; set; } = string.Empty;
        public string EditedPreview { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public int RiskScore { get; set; }
        public string ReviewerNotes { get; set; } = string.Empty;
        public string ReviewedBy { get; set; } = string.Empty;
        public List<string> ReviewActionLog { get; set; } = new List<string>();
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string LastError { get; set; } = string.Empty;
        public string AffiliateLink { get; set; }
        public string ProductId { get; set; }

        /// <summary>Reviewer bật khi duyệt — mới được gắn link lúc Auto Post.</summary>
        public bool CanAttachAffiliate { get; set; }

        /// <summary>Link dự kiến (từ sản phẩm / payload); chỉ dùng khi <see cref="CanAttachAffiliate"/>.</summary>
        public string TargetAffiliateLink { get; set; }

        public string TargetProductId { get; set; }

        public string ReviewedAtLabel => ReviewedAtUtc.HasValue ? ReviewedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "-";
        public string LastAuditAction => ReviewActionLog != null && ReviewActionLog.Count > 0
            ? ReviewActionLog[ReviewActionLog.Count - 1]
            : "-";
    }

    public class ApprovalQueueManager
    {
        private const string QueueFileName = "approval_queue.json";
        private const int MaxItems = 1000;

        public async Task<List<ApprovalQueueItem>> LoadAsync()
        {
            var path = GetQueuePath();
            if (!File.Exists(path))
            {
                return new List<ApprovalQueueItem>();
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<ApprovalQueueItem>();
                }

                var items = JsonConvert.DeserializeObject<List<ApprovalQueueItem>>(json) ?? new List<ApprovalQueueItem>();
                return items
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Take(MaxItems)
                    .ToList();
            }
            catch
            {
                return new List<ApprovalQueueItem>();
            }
        }

        public async Task SaveAsync(IList<ApprovalQueueItem> items)
        {
            var normalized = (items ?? new List<ApprovalQueueItem>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(MaxItems)
                .ToList();

            var json = JsonConvert.SerializeObject(normalized, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(GetQueuePath(), json, TextFileEncoding.Utf8NoBom)).ConfigureAwait(false);
        }

        private static string GetQueuePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, QueueFileName);
        }
    }
}
