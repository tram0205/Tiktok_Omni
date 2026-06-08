namespace tiktok_Omni.Services
{
    /// <summary>Kết quả submit/poll Veo — hỗ trợ trả URL ngay hoặc jobId + trạng thái Processing.</summary>
    public sealed class VeoAsyncJobResult
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string StatusPollUrl { get; set; } = string.Empty;

        public bool IsTerminalSuccess =>
            HasMediaUrl && IsSuccessStatus(Status);

        public bool IsTerminalFailure =>
            IsFailedStatus(Status) || (!string.IsNullOrWhiteSpace(ErrorMessage) && !HasMediaUrl && IsFailedStatus(Status));

        public bool HasMediaUrl =>
            !string.IsNullOrWhiteSpace(VideoUrl) || !string.IsNullOrWhiteSpace(ImageUrl);

        public static bool IsSuccessStatus(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s == "completed" || s == "complete" || s == "succeeded" || s == "success" || s == "done";
        }

        public static bool IsProcessingStatus(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s == "processing" || s == "pending" || s == "queued" || s == "running" || s == "in_progress";
        }

        public static bool IsFailedStatus(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            return s == "failed" || s == "error" || s == "cancelled" || s == "canceled";
        }
    }
}
