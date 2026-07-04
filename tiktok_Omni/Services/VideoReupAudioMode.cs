namespace tiktok_Omni.Services
{
    /// <summary>Kiểu ghép âm thanh khi render Video reup (phần sau hook).</summary>
    public enum VideoReupAudioMode
    {
        /// <summary>Hook Lyria + nhạc nền MP3 (mặc định).</summary>
        AffiliateBed = 0,

        /// <summary>Hook Lyria + giữ tiếng gốc video (không nhạc nền).</summary>
        FilmKeepOriginal = 1,

        /// <summary>Hook (giọng nhấn) + thuyết minh Gemini/ElevenLabs (giọng kể chuyện); nhạc nền tùy chọn ~12%.</summary>
        NarrationScript = 2
    }
}
