namespace tiktok_Omni.Models
{
    /// <summary>Một phân cảnh được tách ra từ câu triết lý.</summary>
    public sealed class PhilosophySceneItem
    {
        /// <summary>Số thứ tự phân cảnh (1-based).</summary>
        public int SceneIndex { get; set; }

        /// <summary>Đoạn văn bản câu triết lý của phân cảnh này.</summary>
        public string SceneText { get; set; } = string.Empty;

        /// <summary>Ước lượng thời lượng giọng đọc (giây, 1 từ = 0.4s).</summary>
        public double EstimatedDurationSeconds { get; set; }

        /// <summary>Prompt hình ảnh tiếng Anh do Gemini sinh để tạo video AI.</summary>
        public string ImagePromptEn { get; set; } = string.Empty;

        /// <summary>Tên file video quy ước: [5-tu-dau-viet-lien-khong-dau]-[stt padded].mp4</summary>
        public string ConventionFileName { get; set; } = string.Empty;

        /// <summary>Câu triết lý đầy đủ (để xuất Excel theo nhóm).</summary>
        public string ParentQuote { get; set; } = string.Empty;
    }
}
