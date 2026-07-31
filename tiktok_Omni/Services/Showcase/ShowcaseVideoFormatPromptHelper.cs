namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Mảnh prompt Gemini theo <see cref="ShowcaseVideoFormatPresets"/> — null field = giữ nguyên hành vi mặc định hiện có.</summary>
    public sealed class ShowcaseVideoFormatPromptParts
    {
        /// <summary>Khung mạch kịch bản (thay dòng AIDA/PAS mặc định) — null = giữ mặc định theo sceneCount.</summary>
        public string FrameworkOverride { get; set; }

        /// <summary>Danh sách role hợp lệ cho từng cảnh — null = giữ mặc định theo sceneCount.</summary>
        public string RoleListOverride { get; set; }

        /// <summary>Câu mục tiêu mở đầu prompt kịch bản — null = giữ câu mặc định.</summary>
        public string GoalSentenceOverride { get; set; }

        /// <summary>Khối quy tắc CTA (thay <see cref="ShowcaseVoiceoverLanguageStyle.GeminiCtaPromptSection"/>) — null = giữ mặc định.</summary>
        public string CtaSectionOverride { get; set; }

        /// <summary>True = KHÔNG chèn CTA/lời mời liên hệ vào kịch bản hay thoại.</summary>
        public bool SuppressCta { get; set; }
    }

    public static class ShowcaseVideoFormatPromptHelper
    {
        public static ShowcaseVideoFormatPromptParts Resolve(string formatId)
        {
            return ResolveForFormat(formatId);
        }

        private static ShowcaseVideoFormatPromptParts ResolveForFormat(string formatId)
        {
            var id = ShowcaseVideoFormatPresets.ResolveId(formatId);

            if (id == ShowcaseVideoFormatPresets.StorytellingId)
            {
                return new ShowcaseVideoFormatPromptParts
                {
                    FrameworkOverride = "Kể chuyện (Hook tò mò – Diễn biến – Cao trào/trải nghiệm thật – Kết cảm xúc nhẹ)",
                    RoleListOverride = "hook, buildup, climax, resolution",
                    GoalSentenceOverride = "mục tiêu kể một câu chuyện thật gần gũi khiến người xem đồng cảm và nhớ tới sản phẩm",
                    CtaSectionOverride =
                        "CTA (BẮT BUỘC — cta_text + lồng nhẹ vào voiceover cảnh cuối):\r\n" +
                        "- Ưu tiên câu hỏi / góc tranh luận nhẹ / mời bình luận — KHÔNG bán hàng cứng.\r\n" +
                        "- Gợi ý: «Theo bạn câu chuyện này hay phần nào?», «Follow mình coi tiếp phần sau nha».\r\n" +
                        "- Lồng tự nhiên vào cuối thoại cảnh cuối; tuân thủ mục TUÂN THỦ CHÍNH SÁCH TIKTOK.\r\n",
                    SuppressCta = false
                };
            }

            if (id == ShowcaseVideoFormatPresets.TutorialId)
            {
                return new ShowcaseVideoFormatPromptParts
                {
                    FrameworkOverride = "Tutorial/hướng dẫn (Nêu vấn đề – Bước 1 – Bước 2 (có thể thêm bước) – Mẹo nhỏ/kết)",
                    RoleListOverride = "problem, step, step, tip",
                    GoalSentenceOverride = "mục tiêu hướng dẫn người xem cách làm/cách dùng thật rõ ràng, dễ áp dụng theo",
                    CtaSectionOverride =
                        "CTA (BẮT BUỘC — cta_text + lồng nhẹ vào voiceover cảnh cuối):\r\n" +
                        "- Ưu tiên câu hỏi thực hành / mời bình luận / lưu video — KHÔNG bán hàng cứng.\r\n" +
                        "- Gợi ý: «Bạn hay phối kiểu nào? Comment nè», «Lưu lại làm theo khi cần nha».\r\n" +
                        "- Lồng tự nhiên vào cuối thoại cảnh cuối; tuân thủ mục TUÂN THỦ CHÍNH SÁCH TIKTOK.\r\n",
                    SuppressCta = false
                };
            }

            if (id == ShowcaseVideoFormatPresets.NoCtaId)
            {
                return new ShowcaseVideoFormatPromptParts
                {
                    FrameworkOverride = null,
                    RoleListOverride = null,
                    GoalSentenceOverride = "mục tiêu khiến khách thích và nhớ sản phẩm",
                    CtaSectionOverride =
                        "CTA: KHÔNG chèn CTA hay lời mời liên hệ/nhắn tin nào — 'cta_text' để chuỗi rỗng \"\".\r\n" +
                        "- Cảnh cuối kết bằng câu chốt tự nhiên (cảm xúc/insight về sản phẩm), KHÔNG hô hào hành động.\r\n",
                    SuppressCta = true
                };
            }

            return new ShowcaseVideoFormatPromptParts
            {
                FrameworkOverride = null,
                RoleListOverride = null,
                GoalSentenceOverride = null,
                CtaSectionOverride = null,
                SuppressCta = false
            };
        }
    }
}
