namespace tiktok_Omni.Services
{
    /// <summary>Kết quả pipeline remix Video reup (đường dẫn + thời lượng để đồng bộ SRT / UI).</summary>
    public sealed class VideoReupRemixResult
    {
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>Thời lượng file nguồn đã tải (giây).</summary>
        public double SourceDurationSeconds { get; set; }

        /// <summary>Độ dài video sau cắt đầu/đuôi (và tốc độ nếu bật), trước mux — khớp track hình.</summary>
        public double ProcessedVideoDurationSeconds { get; set; }

        /// <summary>Độ dài đoạn hook thoại đã ghép (sau chuẩn hóa 4–7s).</summary>
        public double HookDurationSecondsUsed { get; set; }

        /// <summary>Độ dài file MP4 xuất (probe).</summary>
        public double OutputFileDurationSeconds { get; set; }
    }
}
