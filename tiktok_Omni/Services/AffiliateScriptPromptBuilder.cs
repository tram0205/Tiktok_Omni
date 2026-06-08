using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>Prompt Gemini cho kịch bản affiliate — ưu tiên trải nghiệm thực tế từ review khách.</summary>
    public static class AffiliateScriptPromptBuilder
    {
        public static string BuildSlideshowNarrationPrompt(
            IList<AiVideoGenInputItem> items,
            GeminiStyleTemplate styleTemplate = GeminiStyleTemplate.Storytelling)
        {
            var lines = new List<string>();
            for (var i = 0; i < (items?.Count ?? 0); i++)
            {
                var x = items[i];
                lines.Add(FormatProductLine(i + 1, x));
            }

            var styleBlock = DescribeStyleTemplate(styleTemplate);
            return "Bạn là biên kịch TikTok chuyên bán hàng (nhịp nhanh, hook 3s đầu, edit hiện đại). " +
                   styleBlock + " " +
                   "Viết DUY NHẤT 1 kịch bản voice-over tiếng Việt 30-45 giây cho slideshow nhiều sản phẩm. " +
                   "CTA mềm cuối; không markdown. " +
                   "Danh sách sản phẩm:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
        }

        public static string BuildPerProductReviewScriptsPrompt(
            IList<AiVideoGenInputItem> items,
            GeminiStyleTemplate styleTemplate = GeminiStyleTemplate.Review)
        {
            var lines = new List<string>();
            for (var i = 0; i < (items?.Count ?? 0); i++)
            {
                lines.Add(FormatProductLine(i + 1, items[i]));
            }

            var styleBlock = DescribeStyleTemplate(styleTemplate);
            return "Bạn là biên kịch TikTok. " + styleBlock + " " +
                   "Tạo script voice-over tiếng Việt cho TỪNG sản phẩm (30-45 giây mỗi sp). " +
                   "Trả về DUY NHẤT JSON ARRAY: [{\"index\":1,\"script\":\"...\"}]. Không markdown." + Environment.NewLine +
                   string.Join(Environment.NewLine, lines);
        }

        public static string BuildAffiliateDeepNarrationPrompt(
            AiVideoGenInputItem primary,
            IList<AiVideoGenInputItem> scenes,
            GeminiStyleTemplate styleTemplate = GeminiStyleTemplate.Storytelling)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là biên kịch video affiliate dạng short (nhịp nhanh, hook mạnh, pop-text).");
            sb.AppendLine(DescribeStyleTemplate(styleTemplate));
            sb.AppendLine("Viết 1 đoạn voice-over tiếng Việt 45-60 giây cho 4 cảnh ảnh cùng sản phẩm.");
            sb.AppendLine("Chốt CTA mềm; không markdown.");
            sb.AppendLine(FormatProductLine(1, primary));
            if (scenes != null)
            {
                for (var i = 0; i < scenes.Count && i < 4; i++)
                {
                    sb.AppendLine("Cảnh " + (i + 1) + ": " + (scenes[i]?.ImageUrl ?? string.Empty));
                }
            }

            return sb.ToString();
        }

        private static string DescribeStyleTemplate(GeminiStyleTemplate template)
        {
            switch (template)
            {
                case GeminiStyleTemplate.Knowledge:
                    return "TEMPLATE Knowledge: giọng chuyên gia thân thiện; 2-3 insight/tip thực tế; so sánh nhanh; ít cảm xúc thái quá; ưu tiên giá trị học được trước khi nhắc mua.";
                case GeminiStyleTemplate.Review:
                    return "TEMPLATE Review: mở bằng nghi ngờ hoặc pain-point; 2-3 câu paraphrase review khách (bằng chứng xã hội); nêu rõ before/after; không quảng cáo suông.";
                default:
                    return "TEMPLATE Storytelling: mở bằng tình huống đời thường (giật gân); lồng lợi ích tự nhiên; 1-2 câu review xen kẽ; nhịp edit nhanh.";
            }
        }

        private static string FormatProductLine(int index, AiVideoGenInputItem x)
        {
            if (x == null)
            {
                return index + ". (trống)";
            }

            var reviews = x.GetCustomerReviewsList();
            var reviewBlock = reviews.Count == 0
                ? "Review: (chưa có)"
                : "Review khách: " + string.Join(" | ", reviews.Take(3));
            return index + ". Tên: " + (x.ProductName ?? string.Empty) +
                   "; Giá: " + (x.Price ?? string.Empty) +
                   "; " + reviewBlock;
        }
    }
}
