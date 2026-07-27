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

            IReadOnlyList<double> sceneDurationsSeconds = null)

        {

            var themeInstruction = string.IsNullOrWhiteSpace(userTheme)

                ? "Chủ đề quảng cáo: tự suy từ clip — giữ góc TikTok affiliate tự nhiên, không quảng cáo quá lố."

                : "Chủ đề quảng cáo BẮT BUỘC tôn trọng: \"" + userTheme.Trim() + "\".";



            var durationBlock = BuildSceneDurationBlock(sceneCount, sceneDurationsSeconds);



            return

                "Bạn là biên kịch voiceover TikTok affiliate tiếng Việt.\r\n\r\n" +

                "NHIỆM VỤ: Xem từng clip video phân cảnh (theo thứ tự CẢNH 1 → CẢNH " + sceneCount +

                ") và viết lời thoại KHỚP đúng những gì ĐANG THẤY trong clip — không bịa chi tiết không có trong video.\r\n" +

                "Video CHỈ có đúng " + sceneCount + " clip phân cảnh — KHÔNG có clip hook hay CTA riêng. Mỗi clip = 1 slot thoại duy nhất.\r\n" +

                "Clip gửi kèm đã nén 144p nhưng GIỮ NGUYÊN tốc độ và thời lượng thật — căn độ dài thoại theo thời gian clip.\r\n\r\n" +

                "Sản phẩm: \"" + (productName ?? string.Empty).Trim() + "\".\r\n" +

                themeInstruction + "\r\n\r\n" +

                durationBlock +

                "QUY TẮC VIẾT:\r\n" +

                "1) hook_text: tóm tắt 1 câu mở đầu (metadata cho editor) — nội dung mở phải nằm TRONG voiceover cảnh 1, KHÔNG đọc riêng.\r\n" +

                "2) Với MỖI cảnh (order 1.." + sceneCount + "):\r\n" +

                "   - Cảnh 1: CHỈ viết hook (1 câu ngắn gây tò mò) — sẽ được TTS đọc RIÊNG, nhấn mạnh.\r\n" +

                "   - Cảnh 2 → " + sceneCount + ": viết thoại thân bài + CTA cuối sao cho KHI GHÉP LIỀN các câu lại vẫn đọc trôi một mạch (app sẽ TTS 1 lần duy nhất).\r\n" +

                "   - Giọng miền Nam tự nhiên (xưng «mình», từ đời thường) — tránh văn phong thuyết minh/trần trụi kiểu miền Bắc.\r\n" +

                "   - Cảnh " + sceneCount + ": lồng câu kêu gọi nhẹ (Bio/link) vào cuối đoạn thân — KHÔNG tách CTA thành câu hô hào riêng.\r\n" +

                "   - Cảnh giữa: mô tả đúng hình, vừa đủ đọc trong thời lượng clip.\r\n" +

                "   - Tối đa 1 cảnh silent (silent=true, voiceover=\"\") — thường 0 hoặc 1 cảnh flatlay/zoom đẹp để khách nhìn sản phẩm + nhạc. KHÔNG im nhiều cảnh.\r\n" +

                "   - KHÔNG lặp ý giữa các cảnh.\r\n" +

                "3) cta_text: 1 câu CTA ngắn (metadata + chữ overlay cuối video) — lời kêu gọi phải nằm TRONG voiceover cảnh " + sceneCount + ".\r\n" +

                "4) theme: tóm tắt 3-6 từ chủ đề thực tế của video.\r\n\r\n" +

                "CHỈ trả về JSON hợp lệ (không markdown):\r\n" +

                "{\"theme\":\"...\",\"hook_text\":\"...\",\"cta_text\":\"...\",\"scenes\":[{\"order\":1,\"voiceover\":\"...\",\"silent\":false}]}";

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


