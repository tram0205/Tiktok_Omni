namespace tiktok_Omni.Services
{
    /// <summary>Hàng lưới sản xuất cho tab Triết lý / Mascot (batch queue).</summary>
    public sealed class ProductionQueueRowItem
    {
        public string ProfileName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PipelineStatus { get; set; } = "Chờ";
        public int SafetyScore { get; set; } = 100;
        public int ProgressPercent { get; set; }
        public string ThumbnailPath { get; set; } = string.Empty;
        public string OutputVideoPath { get; set; } = string.Empty;
        public string ScriptPreview { get; set; } = string.Empty;
        public string VoiceId { get; set; } = string.Empty;
        public string BackgroundVideo { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
    }
}
