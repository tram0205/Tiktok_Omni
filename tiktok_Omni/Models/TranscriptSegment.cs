namespace tiktok_Omni.Models
{
    /// <summary>Đoạn transcript có timing (ElevenLabs alignment, Whisper, v.v.).</summary>
    public sealed class TranscriptSegment
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>Thời điểm bắt đầu (ms).</summary>
        public double StartTimeMs { get; set; }

        /// <summary>Thời điểm kết thúc (ms).</summary>
        public double EndTimeMs { get; set; }
    }
}
