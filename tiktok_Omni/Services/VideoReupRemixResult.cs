namespace tiktok_Omni.Services
{
    /// <summary>Kết quả pipeline remix Video reup (đường dẫn + thời lượng cho UI).</summary>
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

        /// <summary>Thời lượng clip hook intro AI (giây).</summary>
        public double HookIntroDurationSeconds { get; set; }

        /// <summary>Thời lượng phần body reup sau lách (giây).</summary>
        public double BodyDurationSeconds { get; set; }

        /// <summary>Tổng thời lượng = hook intro + body.</summary>
        public double TotalDurationSeconds { get; set; }
    }
}
