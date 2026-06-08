using System;

namespace tiktok_Omni.Services
{
    /// <summary>Mẫu phong cách kịch bản Gemini — tối ưu chuyển đổi theo thị hiếu TikTok.</summary>
    public enum GeminiStyleTemplate
    {
        Knowledge = 0,
        Review = 1,
        Storytelling = 2
    }

    public static class GeminiStyleTemplateExtensions
    {
        public static GeminiStyleTemplate Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GeminiStyleTemplate.Storytelling;
            }

            if (Enum.TryParse<GeminiStyleTemplate>(value.Trim(), true, out var parsed))
            {
                return parsed;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "knowledge":
                case "tri thức":
                case "tri thuc":
                    return GeminiStyleTemplate.Knowledge;
                case "review":
                case "đánh giá":
                case "danh gia":
                    return GeminiStyleTemplate.Review;
                default:
                    return GeminiStyleTemplate.Storytelling;
            }
        }

        public static string ToDisplayName(this GeminiStyleTemplate template)
        {
            switch (template)
            {
                case GeminiStyleTemplate.Knowledge:
                    return "Knowledge (tri thức)";
                case GeminiStyleTemplate.Review:
                    return "Review (bằng chứng)";
                default:
                    return "Storytelling (kể chuyện)";
            }
        }
    }
}
