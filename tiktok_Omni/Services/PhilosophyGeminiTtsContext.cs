namespace tiktok_Omni.Services
{
    /// <summary>Prompt + chuẩn hóa quote Triết lý cho Edge TTS (dấu câu = nhịp thở).</summary>
    public static class PhilosophyGeminiTtsContext
    {
        /// <summary>Quy tắc viết content quote — Edge TTS đọc có ngắt nghỉ, cảm xúc.</summary>
        public const string GeminiEdgeTtsWritingSection =
            "EDGE TTS — VIẾT QUOTE ĐỌC CÓ CẢM XÚC (BẮT BUỘC cho trường \"content\"):\r\n" +
            "Mỗi quote là lời đọc trầm, có nhịp thở — dấu câu điều khiển ngắt nghỉ (KHÔNG SSML, KHÔNG [pause], KHÔNG ghi chú ngoài câu).\r\n" +
            "1) DẤU CÂU LÀ NHẠC CỤ:\r\n" +
            "   - Dấu phẩy (,): thở ngắn giữa các ý — mỗi quote BẮT BUỘC có ít nhất 1 phẩy (quote dài: 2–3 phẩy).\r\n" +
            "   - Dấu chấm (.): kết thúc ý, dừng rõ — chia câu dài thành 2–3 câu ngắn; mỗi chỗ ngắt nghỉ chỉ MỘT dấu chấm, KHÔNG viết «. .» hay «...».\r\n" +
            "   - Dấu hỏi/chấm than: chỉ khi câu thật sự hỏi hoặc nhấn mạnh (tối đa 1 lần/quote).\r\n" +
            "2) NHỊP TRIẾT LÝ:\r\n" +
            "   - Viết như lời nói có khoảng lặng — trầm, sâu, không thuyết minh liền một mạch.\r\n" +
            "   - Chèn phẩy trước cụm cần nhấn: «Thất bại, không phải là kết thúc.» / «Trong im lặng. Ta nghe được chính mình.»\r\n" +
            "   - Tối đa 2–3 phẩy/câu — đủ thở, không rối.\r\n" +
            "VÍ DỤ SAI → ĐÚNG:\r\n" +
            "   - SAI: «Đừng sợ thất bại vì thất bại dạy ta cách đứng dậy mạnh mẽ hơn»\r\n" +
            "   - ĐÚNG: «Đừng sợ thất bại. Vì thất bại, dạy ta cách đứng dậy, mạnh mẽ hơn.»\r\n" +
            "   - SAI: «Cuộc đời ngắn lắm đừng sống để hối tiếc những điều chưa làm»\r\n" +
            "   - ĐÚNG: «Cuộc đời ngắn lắm. Đừng sống, để hối tiếc những điều chưa làm.»\r\n";

        public static string NormalizeQuoteContent(string content)
        {
            var text = FixQuotePeriods(content);
            text = VietnameseTtsTextNormalizer.NormalizePunctuation(text);
            return FixQuotePeriods(text);
        }

        /// <summary>Chuẩn hóa quote trước TTS — tôn trọng dấu câu người dùng/Gemini đã sửa.</summary>
        public static string NormalizeQuoteContentForTts(string content)
        {
            var text = FixQuotePeriods(content);
            text = VietnameseTtsTextNormalizer.NormalizeQuotePunctuation(text);
            return FixQuotePeriods(text);
        }

        /// <summary>Gom «. .» / «...» về một dấu chấm cho quote triết lý.</summary>
        public static string FixQuotePeriods(string content) =>
            VietnameseTtsTextNormalizer.FixSpacedPeriods((content ?? string.Empty).Trim());
    }
}
