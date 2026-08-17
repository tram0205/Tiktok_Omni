using System;

using System.Collections.Generic;

using System.Globalization;

using System.Linq;



namespace tiktok_Omni.Services.Showcase

{

    /// <summary>Prompt Gemini xem clip phân cảnh → viết voiceover khớp từng clip (không slot hook/CTA audio riêng).</summary>

    public static class ShowcaseVoiceoverPromptBuilder

    {

        public static string Build(

            string productName,

            string userTheme,

            int sceneCount,

            IReadOnlyList<double> sceneDurationsSeconds = null,

            string userVideoFormatId = null)

        {

            var themeInstruction = string.IsNullOrWhiteSpace(userTheme)

                ? "Chủ đề quảng cáo: tự suy từ clip — giữ góc TikTok affiliate tự nhiên, không quảng cáo quá lố."

                : "Chủ đề quảng cáo BẮT BUỘC tôn trọng: \"" + userTheme.Trim() + "\".";



            var durationBlock = BuildSceneDurationBlock(sceneCount, sceneDurationsSeconds);

            var formatParts = ShowcaseVideoFormatPromptHelper.Resolve(userVideoFormatId);
            var ctaSection = formatParts.CtaSectionOverride ?? ShowcaseVoiceoverLanguageStyle.GeminiCtaPromptSection;

            var lastSceneCtaLine = formatParts.SuppressCta
                ? "   - Cảnh " + sceneCount + ": KHÔNG lồng CTA/lời mời liên hệ — kết bằng câu chốt tự nhiên (cảm xúc/insight về sản phẩm).\r\n"
                : "   - Cảnh " + sceneCount + ": lồng CTA (câu hỏi / mời tương tác — theo quy tắc CTA) vào cuối đoạn thân — KHÔNG tách thành câu hô hào riêng.\r\n";

            var ctaTextInstruction = formatParts.SuppressCta
                ? "3) cta_text: để chuỗi rỗng \"\" — video này KHÔNG có CTA/lời mời liên hệ.\r\n"
                : "3) cta_text: cụm ngắn cho chữ overlay cuối video (≤18 từ) — PHẢI trích từ đuôi voiceover cảnh " + sceneCount + ", KHÔNG viết câu thoại thứ hai.\r\n";



            return

                "Bạn là biên kịch voiceover TikTok affiliate tiếng Việt.\r\n\r\n" +

                "NHIỆM VỤ: Xem từng clip video phân cảnh (theo thứ tự CẢNH 1 → CẢNH " + sceneCount +

                ") và viết lời thoại KHỚP đúng những gì ĐANG THẤY trong clip — không bịa chi tiết không có trong video.\r\n" +

                "Video CHỈ có đúng " + sceneCount + " clip phân cảnh — KHÔNG có clip hook hay CTA riêng. Mỗi clip = 1 slot thoại duy nhất.\r\n" +

                "Clip gửi kèm đã nén 144p nhưng GIỮ NGUYÊN tốc độ và thời lượng thật — căn độ dài thoại theo thời gian clip.\r\n" +
                "Danh sách clip có thể gồm cảnh AI (Zoom/Veo/Kling) và clip quay tay thật — viết thoại khớp đúng hình từng clip theo thứ tự timeline (cảnh 1→" + sceneCount + "), không phân biệt nguồn clip.\r\n\r\n" +

                "Sản phẩm: \"" + (productName ?? string.Empty).Trim() + "\".\r\n" +

                themeInstruction + "\r\n\r\n" +

                durationBlock +

                "QUY TẮC VIẾT:\r\n" +

                "1) hook_text: tóm tắt 1 câu mở đầu (metadata cho editor) — nội dung mở phải nằm TRONG voiceover cảnh 1, KHÔNG đọc riêng.\r\n" +

                "2) Với MỖI cảnh (order 1.." + sceneCount + "):\r\n" +

                "   - Cảnh 1: CHỈ viết hook (1 câu ngắn gây tò mò) — sẽ được TTS đọc RIÊNG, nhấn mạnh.\r\n" +

                "   - Cảnh 2 → " + sceneCount + ": viết thoại thân bài + CTA cuối sao cho KHI GHÉP LIỀN các câu lại vẫn đọc trôi một mạch (app sẽ TTS 1 lần duy nhất).\r\n" +
                ShowcaseVoiceoverLanguageStyle.GeminiPromptSection +
                ShowcaseVoiceoverLanguageStyle.GeminiEdgeTtsWritingSection +
                ShowcaseVoiceoverLanguageStyle.GeminiTikTokComplianceSection +
                ctaSection +
                lastSceneCtaLine +

                "   - Cảnh giữa: mô tả đúng hình, vừa đủ đọc trong thời lượng clip.\r\n" +

                "   - Tối đa 1 cảnh silent (silent=true, voiceover=\"\") — thường 0 hoặc 1 cảnh flatlay/zoom đẹp để khách nhìn sản phẩm + nhạc. KHÔNG im nhiều cảnh.\r\n" +

                "   - KHÔNG lặp ý giữa các cảnh.\r\n" +

                ctaTextInstruction +

                "4) theme: tóm tắt 3-6 từ chủ đề thực tế của video.\r\n\r\n" +

                "CHỈ trả về JSON hợp lệ (không markdown):\r\n" +

                "{\"theme\":\"...\",\"hook_text\":\"...\",\"cta_text\":\"...\",\"scenes\":[{\"order\":1,\"clip_index\":1,\"voiceover\":\"...\",\"silent\":false}]}";

        }

        /// <summary>Prompt folder-first: mọi clip trong clips_render, Gemini chọn thứ tự timeline.</summary>
        internal static string BuildFromRenderFolder(
            string productName,
            string productTypeLabel,
            string userTheme,
            IReadOnlyList<ShowcaseRenderClipsTimelineHelper.ClipManifestEntry> manifest,
            IReadOnlyList<double> sceneDurationsSeconds,
            string userVideoFormatId = null)
        {
            var clipCount = manifest?.Count ?? 0;
            if (clipCount == 0)
            {
                return Build(productName, userTheme, 1, sceneDurationsSeconds, userVideoFormatId);
            }

            var themeInstruction = string.IsNullOrWhiteSpace(userTheme)
                ? "Chủ đề quảng cáo: tự suy từ clip — giữ góc TikTok affiliate tự nhiên, không quảng cáo quá lố."
                : "Chủ đề quảng cáo BẮT BUỘC tôn trọng: \"" + userTheme.Trim() + "\".";

            var typeLine = string.IsNullOrWhiteSpace(productTypeLabel)
                ? string.Empty
                : "Loại sản phẩm: " + productTypeLabel.Trim() + ".\r\n";

            var manifestBlock = BuildClipManifestBlock(manifest, sceneDurationsSeconds);
            var durationBlock = BuildSceneDurationBlock(clipCount, sceneDurationsSeconds);
            var formatParts = ShowcaseVideoFormatPromptHelper.Resolve(userVideoFormatId);
            var ctaSection = formatParts.CtaSectionOverride ?? ShowcaseVoiceoverLanguageStyle.GeminiCtaPromptSection;

            var lastSceneCtaLine = formatParts.SuppressCta
                ? "   - Cảnh cuối timeline: KHÔNG lồng CTA/lời mời liên hệ — kết bằng câu chốt tự nhiên.\r\n"
                : "   - Cảnh cuối timeline: lồng CTA (câu hỏi / mời tương tác) vào cuối đoạn thân — KHÔNG tách thành câu hô hào riêng.\r\n";

            var ctaTextInstruction = formatParts.SuppressCta
                ? "3) cta_text: để chuỗi rỗng \"\" — video này KHÔNG có CTA/lời mời liên hệ.\r\n"
                : "3) cta_text: cụm ngắn cho chữ overlay cuối video (≤18 từ) — PHẢI trích từ đuôi voiceover cảnh cuối timeline.\r\n";

            return
                "Bạn là biên kịch voiceover TikTok affiliate tiếng Việt.\r\n\r\n"
                + "NHIỆM VỤ: Xem TẤT CẢ clip đính kèm (INPUT 1.." + clipCount + ") từ thư mục clips_render. "
                + "Sắp xếp lại thứ tự clip cho thành MỘT video logic (hook → thân → CTA), "
                + "clip quay tay có thể đặt đầu/giữa/cuối tùy nội dung. "
                + "Viết lời thoại KHỚP đúng hình từng clip — không bịa chi tiết không có trong video.\r\n"
                + "Mỗi clip INPUT chỉ dùng ĐÚNG 1 lần trong timeline. Clip gửi kèm đã nén 144p nhưng GIỮ NGUYÊN tốc độ và thời lượng thật.\r\n\r\n"
                + "Sản phẩm: \"" + (productName ?? string.Empty).Trim() + "\".\r\n"
                + typeLine
                + themeInstruction + "\r\n\r\n"
                + manifestBlock
                + durationBlock
                + "QUY TẮC JSON scenes[]:\r\n"
                + "- Mỗi phần tử: order (1.." + clipCount + " thứ tự PHÁT trong video cuối), "
                + "clip_index (1.." + clipCount + " clip INPUT nào dùng), voiceover, silent.\r\n"
                + "- order có thể KHÁC clip_index — bạn được phép đổi thứ tự clip cho mạch kể chuyện tốt hơn.\r\n"
                + "- Cảnh order=1: hook ngắn (1 câu gây tò mò) — TTS đọc riêng.\r\n"
                + "- Cảnh order≥2: thân bài + CTA cuối đọc liền một mạch (TTS gom).\r\n"
                + ShowcaseVoiceoverLanguageStyle.GeminiPromptSection
                + ShowcaseVoiceoverLanguageStyle.GeminiEdgeTtsWritingSection
                + ShowcaseVoiceoverLanguageStyle.GeminiTikTokComplianceSection
                + ctaSection
                + lastSceneCtaLine
                + "   - Tối đa 1 cảnh silent. KHÔNG lặp ý giữa các cảnh.\r\n"
                + "1) hook_text: metadata tóm hook (trùng ý cảnh order=1).\r\n"
                + ctaTextInstruction
                + "4) theme: tóm tắt 3-6 từ chủ đề thực tế của video.\r\n\r\n"
                + "CHỈ trả về JSON hợp lệ (không markdown):\r\n"
                + "{\"theme\":\"...\",\"hook_text\":\"...\",\"cta_text\":\"...\",\"scenes\":[{\"order\":1,\"clip_index\":2,\"voiceover\":\"...\",\"silent\":false}]}";
        }

        private static string BuildClipManifestBlock(
            IReadOnlyList<ShowcaseRenderClipsTimelineHelper.ClipManifestEntry> manifest,
            IReadOnlyList<double> sceneDurationsSeconds)
        {
            if (manifest == null || manifest.Count == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>
            {
                "DANH SÁCH CLIP INPUT (thứ tự đính kèm — clip_index = số INPUT):"
            };

            for (var i = 0; i < manifest.Count; i++)
            {
                var entry = manifest[i];
                var dur = sceneDurationsSeconds != null && i < sceneDurationsSeconds.Count
                    ? " ~" + Math.Max(0.5d, sceneDurationsSeconds[i]).ToString("0.#", CultureInfo.InvariantCulture) + "s"
                    : string.Empty;
                var kind = entry.IsAppSceneFile ? "AI/app" : "quay tay";
                lines.Add("- INPUT " + entry.InputIndexOneBased + ": «" + entry.FileName + "» (" + kind + ")" + dur);
            }

            return string.Join("\r\n", lines) + "\r\n\r\n";
        }



        private static string BuildSceneDurationBlock(int sceneCount, IReadOnlyList<double> sceneDurationsSeconds)

        {

            if (sceneDurationsSeconds == null || sceneDurationsSeconds.Count == 0)

            {

                return string.Empty;

            }



            var lines = new List<string> { "THỜI LƯỢNG CLIP THẬT (giây — clip gửi kèm không bị tua nhanh):" };

            for (var i = 0; i < sceneCount; i++)

            {

                var seconds = i < sceneDurationsSeconds.Count

                    ? Math.Max(0.5d, sceneDurationsSeconds[i])

                    : 0d;

                if (seconds <= 0)

                {

                    continue;

                }



                lines.Add("- Cảnh " + (i + 1) + ": ~" +

                          seconds.ToString("0.#", CultureInfo.InvariantCulture) + " giây" +

                          (i == 0
                              ? " — hook (1 câu, TTS riêng)"
                              : " — phần thân/CTA (gom đọc liền sau hook)"));

            }



            if (lines.Count <= 1)

            {

                return string.Empty;

            }



            return string.Join("\r\n", lines) + "\r\n\r\n";

        }

    }

}


