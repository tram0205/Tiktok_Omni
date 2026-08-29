namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Quy tắc giọng viết thoại tiếng Việt cho Gemini (kịch bản + lời thoại).</summary>
    public static class ShowcaseVoiceoverLanguageStyle
    {
        /// <summary>Đoạn prompt bắt buộc — thân thiện, gần gũi, ngôn ngữ miền Nam / Sài Gòn.</summary>
        public const string GeminiPromptSection =
            "GIỌNG VIẾT THOẠI (BẮT BUỘC cho hook_text, cta_text, voiceover mọi cảnh):\r\n" +
            "- Thân thiện, dễ gần — như bạn bè Sài Gòn chia sẻ trên TikTok, không giọng quảng cáo sáo hay thuyết minh truyền hình.\r\n" +
            "- Ngôn ngữ miền Nam tự nhiên: xưng «mình» hoặc «tui» (chọn một xuyên suốt); có thể dùng nhẹ «nè», «hen», «nghen», «đỉnh», «xịn», «ổn áp», «hợp lý», «đáng thử» khi hợp ngữ cảnh.\r\n" +
            "- Tránh văn phong cứng kiểu miền Bắc («các bạn», «quý vị», «sản phẩm này sẽ giúp bạn…»).\r\n" +
            "- Câu ngắn, nhịp nhanh, dễ đọc TTS — như người miền Nam kể trải nghiệm thật.\r\n" +
            "- Không lạm dụng từ lóng quá trẻ — giữ dễ hiểu cho đa tuổi.\r\n";

        /// <summary>
        /// Quy tắc viết thoại tối ưu Edge TTS — dấu câu = nhịp thở, phẩy = nhấn mạnh, câu ngắn = ít bị tua nhanh khi render.
        /// </summary>
        public const string GeminiEdgeTtsWritingSection =
            "EDGE TTS — VIẾT THOẠI TTS-FRIENDLY (BẮT BUỘC cho hook_text, voiceover mọi cảnh, cta_text):\r\n" +
            "Mỗi voiceover là text gửi thẳng Edge TTS — dấu câu điều khiển nhịp thở & nhấn mạnh (KHÔNG dùng SSML, KHÔNG ghi chú [pause]).\r\n" +
            "1) DẤU CÂU LÀ NHẠC CỤ:\r\n" +
            "   - Dấu phẩy (,): thở ngắn giữa các ý — đặt chỗ người nói tự nhiên thở.\r\n" +
            "   - Dấu chấm (.): kết thúc ý, dừng rõ — chia câu dài thành 2–3 câu ngắn, KHÔNG viết block dài không dấu.\r\n" +
            "   - Ba chấm (...): dừng dài trước reveal/bất ngờ — vd. «Mà chất vải thì... mềm lắm nè.»\r\n" +
            "   - Dấu hỏi (?): lên giọng cuối — dùng cho CTA dạng hỏi.\r\n" +
            "   - Dấu chấm than (!): nhiệt tình vừa phải — tối đa 1 lần/video, tránh spam.\r\n" +
            "2) NGẮT TỪ NHẤN MẠNH (micro-pause bằng phẩy):\r\n" +
            "   - Chèn phẩy trước hoặc giữa cụm cần nhấn: tên SP, chất liệu, form/điểm đẹp, ưu đãi.\r\n" +
            "   - Vd.: «Form áo này, cực kỳ tôn dáng.» / «Chất liệu, lụa cao cấp, mát tay.» / «Mức giá, hợp lý lắm nè.»\r\n" +
            "   - Tối đa 2–3 phẩy/câu — đủ thở, không rối.\r\n" +
            "3) NHỊP & ĐỘ DÀI (giọng tự nhiên ~110% — app có thể tua nhanh nếu thoại dài hơn clip):\r\n" +
            "   - Mỗi cảnh ~5–8 giây đọc: 1–2 câu ngắn, tổng ~12–22 từ tiếng Việt (trừ cảnh silent).\r\n" +
            "   - Viết như LỜI NÓI có nhịp thở — tránh văn viết sách/báo hay thuyết minh liền một mạch.\r\n" +
            "   - Hook (cảnh 1): 1 câu, ≤12 từ, có ít nhất 1 dấu phẩy hoặc chấm/ba chấm để nhấn.\r\n" +
            "VÍ DỤ SAI → ĐÚNG:\r\n" +
            "   - SAI: «Cái áo này mặc lên rất tôn dáng và chất vải mềm mát»\r\n" +
            "   - ĐÚNG: «Cái áo này, mặc lên, cực kỳ tôn dáng. Chất vải thì... mềm mát nè.»\r\n" +
            "   - SAI: «Theo bạn mẫu nào hợp gu hơn comment cho mình biết»\r\n" +
            "   - ĐÚNG: «Theo bạn, mẫu nào hợp gu hơn? Comment cho mình nha.»\r\n";

        /// <summary>Tuân thủ chính sách TikTok — tránh từ ngữ bị hạn chế phân phối.</summary>
        public const string GeminiTikTokComplianceSection =
            "TUÂN THỦ CHÍNH SÁCH CỘNG ĐỒNG TIKTOK (tránh bóp reach / vi phạm):\r\n" +
            "- Viết như review & trải nghiệm thật — KHÔNG spam bán hàng, không hô hào, không viết HOA toàn bộ.\r\n" +
            "- TUYỆT ĐỐI TRÁNH các từ/cụm dễ bị TikTok hạn chế tương tác: «giá rẻ», «giá sốc», «giá cực rẻ», «mua ngay», «chốt đơn», «chốt liền», «order», «liên hệ ngay», «sale sập sàn», «freeship», «giảm sốc», «inbox ngay», «đặt hàng ngay», URL http/https.\r\n" +
            "- Thay bằng ngôn ngữ tự nhiên: «mình thử thấy ổn», «form này xinh lắm», «ai cần hỏi thêm cứ nhắn mình».\r\n" +
            "- Không cam kết quá đà (100% chính hàng, chữa bệnh, làm giàu…), không gây shock tiêu cực, không nhắc shop đối thủ.\r\n" +
            "- Không đọc số điện thoại hay link trực tiếp trong thoại — mời nhắn tin/chat trên app là đủ.\r\n";

        /// <summary>CTA cuối video — kích hoạt tương tác, không nhất thiết kêu mua hàng.</summary>
        public const string GeminiCtaPromptSection =
            "CTA (BẮT BUỘC — cta_text + lồng vào voiceover cảnh cuối):\r\n" +
            "- QUAN TRỌNG — KHÔNG TRÙNG: CTA chỉ được đọc MỘT LẦN trong voiceover cảnh cuối. cta_text KHÔNG phải câu thoại thứ hai — chỉ trích cụm ngắn từ đuôi voiceover cảnh cuối cho chữ overlay (≤18 từ), phải là đoạn con của voiceover đó.\r\n" +
            "- CTA = câu kết khiến người xem MUỐN phản hồi — KHÔNG nhất thiết kêu mua hàng, chốt đơn hay nhắn tin tư vấn.\r\n" +
            "- Chọn 1 kiểu phù hợp chủ đề & kiểu video (có thể kết hợp nhẹ):\r\n" +
            "  • Câu hỏi mở: «Theo bạn mẫu nào hợp gu hơn?», «Nên phối với giày hay sandal ta?»\r\n" +
            "  • Góc tranh luận / tò mò nhẹ: «Mặc kiểu này đi chơi hay đi làm hợp hơn?», «Form này có hơi dài quá không?»\r\n" +
            "  • Mời bình luận: «Các bạn chọn màu nào nè?», «Ai hay mặc áo dài comment cho tui biết nha»\r\n" +
            "  • Mời nhắn tin tư vấn (khi hợp QC): «Thích mẫu nào nhắn tui gợi ý nha», «Chưa chắc size cứ nhắn mình hen»\r\n" +
            "  • Mời follow / lưu: «Lưu lại làm theo khi cần nha», «Follow mình coi thêm mẹo hay»\r\n" +
            "- Giọng thân thiện miền Nam, tự nhiên — như hỏi bạn bè, không hô hào.\r\n" +
            "- TUYỆT ĐỐI TRÁNH: mua ngay, chốt đơn, thêm giỏ hàng, giá sốc, inbox ngay, liên hệ ngay (tuân mục TUÂN THỦ TIKTOK).\r\n" +
            "- Lồng CTA tự nhiên vào cuối thoại cảnh cuối — tối đa 18 từ cho cta_text overlay.\r\n";
    }
}
