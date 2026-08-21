using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    /// <summary>Kết quả Gemini gợi ý nền cho một quote (không đổi content).</summary>
    public sealed class PhilosophyBackgroundGeminiSuggestion
    {
        public string BRollFolder { get; set; } = string.Empty;

        public List<string> ZoomImagePaths { get; set; } = new List<string>();

        public string MotionPrompt { get; set; } = string.Empty;

        public bool PreferZoomMode { get; set; }
    }
}
