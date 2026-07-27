using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseThemePreset
    {
        public ShowcaseThemePreset(string id, string displayLabel, string promptHint)
        {
            Id = id ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
            PromptHint = promptHint ?? string.Empty;
        }

        public string Id { get; }

        public string DisplayLabel { get; }

        public string PromptHint { get; }

        public override string ToString() => DisplayLabel;
    }

    /// <summary>Góc quảng cáo gợi ý cho Showcase — gửi <see cref="ShowcaseThemePreset.PromptHint"/> cho Gemini khi sinh kịch bản.</summary>
    public static class ShowcaseThemePresets
    {
        public static ShowcaseThemePreset Auto { get; } =
            new ShowcaseThemePreset("auto", "— Tự động (Gemini) —", string.Empty);

        public static IReadOnlyList<ShowcaseThemePreset> All { get; } = new[]
        {
            Auto,
            new ShowcaseThemePreset(
                "pain",
                "Giải quyết nỗi đau",
                "Góc quảng cáo PAS: nêu vấn đề/nỗi đau thực tế của khách, làm trầm hậu quả nếu không xử lý, rồi sản phẩm là giải pháp rõ ràng."),
            new ShowcaseThemePreset(
                "before-after",
                "Trước / sau",
                "Góc transformation: so sánh trước và sau khi dùng sản phẩm — thay đổi rõ ràng, cụ thể, dễ hình dung."),
            new ShowcaseThemePreset(
                "unboxing",
                "Unboxing / trải nghiệm",
                "Góc trải nghiệm thật: mở hộp, chạm chất liệu, thử ngay — giọng review chân thật, không quảng cáo quá lố."),
            new ShowcaseThemePreset(
                "compare",
                "So sánh đáng mua",
                "Góc so sánh: sản phẩm này khác/hơn cách làm cũ hoặc hàng phổ thông — nêu 2-3 điểm khác biệt cụ thể."),
            new ShowcaseThemePreset(
                "lifestyle",
                "Lifestyle / phối đồ",
                "Góc lifestyle: gắn sản phẩm vào khoảnh khắc sống mong muốn (đi làm, đi chơi, ở nhà gọn gàng…) — cảm xúc trước, tính năng sau."),
            new ShowcaseThemePreset(
                "tips",
                "Mẹo nhanh / hack",
                "Góc tips: 1 mẹo hoặc cách dùng ít người biết — mở đầu hữu ích, sản phẩm là công cụ thực hiện mẹo đó."),
            new ShowcaseThemePreset(
                "fomo",
                "FOMO / trend",
                "Góc FOMO nhẹ: trend đang hot, nhiều người dùng, sợ bỏ lỡ — không cần số liệu bịa, giữ chân bằng tò mò."),
            new ShowcaseThemePreset(
                "value",
                "Giá trị / tiết kiệm",
                "Góc value: đáng từng đồng — tiết kiệm thời gian/chi phí/dùng lâu, nêu lợi ích cụ thể thay vì chỉ nói rẻ."),
            new ShowcaseThemePreset(
                "gift",
                "Quà tặng / mùa lễ",
                "Góc quà tặng: phù hợp tặng người thân/dịp đặc biệt — gợi cảm xúc và lý do nên mua làm quà.")
        };

        public static int FindIndexByPrompt(string promptHint)
        {
            var normalized = Normalize(promptHint);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            for (var i = 0; i < All.Count; i++)
            {
                if (string.Equals(Normalize(All[i].PromptHint), normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        public static string GetDisplayLabel(string promptHint)
        {
            var normalized = Normalize(promptHint);
            if (string.IsNullOrEmpty(normalized))
            {
                return Auto.DisplayLabel;
            }

            var preset = All.FirstOrDefault(p =>
                string.Equals(Normalize(p.PromptHint), normalized, StringComparison.Ordinal));
            return preset?.DisplayLabel ?? promptHint.Trim();
        }

        public static string ResolvePromptForGemini(string promptHint) => Normalize(promptHint);

        /// <summary>Chủ đề tự nhập ưu tiên; không có thì dùng preset; cả hai trống → Gemini tự suy.</summary>
        public static string ResolveUserThemeForGemini(string presetPromptHint, string userThemeText)
        {
            var custom = Normalize(userThemeText);
            if (!string.IsNullOrEmpty(custom))
            {
                return custom;
            }

            return ResolvePromptForGemini(presetPromptHint);
        }

        /// <summary>Gán chủ đề từ ô lưới gộp: khớp preset → preset; còn lại → chủ đề tùy chỉnh.</summary>
        public static void ApplyCombinedInput(ShowcaseVideoItem video, string gridText)
        {
            if (video == null)
            {
                return;
            }

            var text = Normalize(gridText);
            if (string.IsNullOrEmpty(text))
            {
                video.ShowcaseThemePrompt = string.Empty;
                video.ShowcaseUserTheme = string.Empty;
                return;
            }

            foreach (var preset in All)
            {
                if (string.Equals(preset.DisplayLabel, text, StringComparison.OrdinalIgnoreCase))
                {
                    video.ShowcaseThemePrompt = preset.PromptHint ?? string.Empty;
                    video.ShowcaseUserTheme = string.Empty;
                    return;
                }
            }

            video.ShowcaseUserTheme = text;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim();
    }
}
