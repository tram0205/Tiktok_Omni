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

        public static ShowcaseThemePreset Custom { get; } =
            new ShowcaseThemePreset("custom", "Tuỳ chỉnh: người dùng tự nhập", string.Empty);

        public static bool IsCustomPreset(ShowcaseThemePreset preset) =>
            preset != null && string.Equals(preset.Id, Custom.Id, StringComparison.Ordinal);

        /// <summary>Chủ đề quảng cáo SP — dùng cho «Quảng cáo SP» và «Không CTA».</summary>
        public static IReadOnlyList<ShowcaseThemePreset> ProductAdAll { get; } = new[]
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
                "Góc quà tặng: phù hợp tặng người thân/dịp đặc biệt — gợi cảm xúc và lý do nên mua làm quà."),
            Custom
        };

        /// <summary>Chủ đề kể chuyện — dùng khi kiểu video «Kể chuyện».</summary>
        public static IReadOnlyList<ShowcaseThemePreset> StorytellingAll { get; } = new[]
        {
            Auto,
            new ShowcaseThemePreset(
                "story-journey",
                "Hành trình / kỷ niệm",
                "Góc kể chuyện: một hành trình hoặc kỷ niệm cá nhân gắn với sản phẩm — cảm xúc chân thật, không bán hàng cứng."),
            new ShowcaseThemePreset(
                "story-moment",
                "Khoảnh khắc đáng nhớ",
                "Góc khoảnh khắc: một scene đời thường đẹp (đi chơi, lễ, gặp bạn…) — sản phẩm xuất hiện tự nhiên trong câu chuyện."),
            new ShowcaseThemePreset(
                "story-experience",
                "Trải nghiệm thật",
                "Góc review nhẹ: chia sẻ cảm nhận thật sau khi dùng/mặc — giọng bạn bè kể, không quảng cáo sáo."),
            new ShowcaseThemePreset(
                "story-behind",
                "Behind the scenes",
                "Góc hậu trường: chuẩn bị, chọn đồ, may đo, buổi chụp — tò mò quá trình phía sau sản phẩm."),
            new ShowcaseThemePreset(
                "story-empathy",
                "Cảm xúc / đồng cảm",
                "Góc cảm xúc: nói về cảm giác, tự tin, kỷ niệm gia đình — khách đồng cảm trước, sản phẩm là nhân vật phụ."),
            Custom
        };

        /// <summary>Chủ đề tutorial — dùng khi kiểu video «Tutorial / hướng dẫn».</summary>
        public static IReadOnlyList<ShowcaseThemePreset> TutorialAll { get; } = new[]
        {
            Auto,
            new ShowcaseThemePreset(
                "tut-outfit",
                "Cách phối đồ",
                "Góc hướng dẫn phối: 2–3 combo cụ thể với sản phẩm — từng bước dễ làm theo, nói rõ vì sao hợp."),
            new ShowcaseThemePreset(
                "tut-size",
                "Mẹo chọn size / màu",
                "Góc chọn đúng: hướng dẫn chọn size, màu, form theo dáng người — giải quyết nỗi lo hay gặp."),
            new ShowcaseThemePreset(
                "tut-care",
                "Bảo quản / giặt",
                "Góc chăm sóc: cách giặt, phơi, cất sản phẩm để bền — hữu ích, tăng giá trị cảm nhận."),
            new ShowcaseThemePreset(
                "tut-compare",
                "So sánh nhanh",
                "Góc so sánh ngắn: 2–3 điểm khác biệt rõ (chất liệu, form, giá trị) — giúp khách quyết định nhanh."),
            new ShowcaseThemePreset(
                "tut-tips",
                "Tips ít người biết",
                "Góc mẹo nhỏ: 1–2 trick dùng/mặc ít người biết — mở đầu hữu ích, sản phẩm là công cụ thực hiện."),
            Custom
        };

        /// <summary>Tương thích cũ — alias của <see cref="ProductAdAll"/>.</summary>
        public static IReadOnlyList<ShowcaseThemePreset> All => ProductAdAll;

        /// <summary>Danh sách chủ đề theo kiểu video đã chọn.</summary>
        public static IReadOnlyList<ShowcaseThemePreset> ForFormat(string formatId)
        {
            var id = ShowcaseVideoFormatPresets.ResolveId(formatId);
            if (id == ShowcaseVideoFormatPresets.StorytellingId)
            {
                return StorytellingAll;
            }

            if (id == ShowcaseVideoFormatPresets.TutorialId)
            {
                return TutorialAll;
            }

            return ProductAdAll;
        }

        public static string ThemeSubtitleForFormat(string formatId)
        {
            var id = ShowcaseVideoFormatPresets.ResolveId(formatId);
            if (id == ShowcaseVideoFormatPresets.StorytellingId)
            {
                return "Preset kể chuyện + tùy chỉnh → Gemini";
            }

            if (id == ShowcaseVideoFormatPresets.TutorialId)
            {
                return "Preset hướng dẫn + tùy chỉnh → Gemini";
            }

            return "Preset quảng cáo + tùy chỉnh → Gemini";
        }

        public static int FindIndexByPrompt(string promptHint)
        {
            return FindIndexByPromptInList(ForFormat(ShowcaseVideoFormatPresets.DefaultId), promptHint);
        }

        public static int FindIndexByPromptInList(IReadOnlyList<ShowcaseThemePreset> presets, string promptHint)
        {
            var normalized = Normalize(promptHint);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            if (presets == null)
            {
                return 0;
            }

            for (var i = 0; i < presets.Count; i++)
            {
                if (string.Equals(Normalize(presets[i].PromptHint), normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int FindCustomIndexInList(IReadOnlyList<ShowcaseThemePreset> presets)
        {
            if (presets == null)
            {
                return -1;
            }

            for (var i = 0; i < presets.Count; i++)
            {
                if (IsCustomPreset(presets[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        public static string GetDisplayLabel(string promptHint)
        {
            var normalized = Normalize(promptHint);
            if (string.IsNullOrEmpty(normalized))
            {
                return Auto.DisplayLabel;
            }

            var preset = FindPresetByPromptHint(normalized);
            return preset?.DisplayLabel ?? promptHint.Trim();
        }

        private static ShowcaseThemePreset FindPresetByPromptHint(string normalizedPromptHint)
        {
            return ProductAdAll.Concat(StorytellingAll).Concat(TutorialAll)
                .FirstOrDefault(p => string.Equals(Normalize(p.PromptHint), normalizedPromptHint, StringComparison.Ordinal));
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

            foreach (var preset in ProductAdAll.Concat(StorytellingAll).Concat(TutorialAll))
            {
                if (IsCustomPreset(preset))
                {
                    continue;
                }

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
