using System;

namespace tiktok_Omni.Services.Jobs
{
    public sealed class OmniJob
    {
        public Guid Id { get; } = Guid.NewGuid();
        public OmniJobKind Kind { get; set; }
        public OmniJobStatus Status { get; set; } = OmniJobStatus.Pending;
        public string Title { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; } = 2;
        public string LastError { get; set; } = string.Empty;
        /// <summary>Ưu tiên cao hơn chạy trước (Deep Dive video tiềm năng).</summary>
        public int Priority { get; set; }
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public object Tag { get; set; }
        public string AffiliateLink { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
    }
}
