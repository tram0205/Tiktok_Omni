using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseProductTypePreset
    {
        public ShowcaseProductTypePreset(string id, string displayLabel, string promptHint)
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

    /// <summary>Loại sản phẩm/trang phục Showcase — gửi <see cref="ShowcaseProductTypePreset.PromptHint"/> cho Gemini khi sinh kịch bản.</summary>
    public static class ShowcaseProductTypePresets
    {
        /// <summary>Giá trị lưu trên lưới khi chọn «Tuỳ chỉnh» (tránh trùng «Tự động» — PromptHint rỗng).</summary>
        public const string CustomPromptSentinel = "__showcase_product_custom__";

        public static ShowcaseProductTypePreset Auto { get; } =
            new ShowcaseProductTypePreset("auto", "— Tự động (Gemini) —", string.Empty);

        public static ShowcaseProductTypePreset Custom { get; } =
            new ShowcaseProductTypePreset(
                "custom",
                "Tuỳ chỉnh: người dùng tự nhập",
                CustomPromptSentinel);

        public static IReadOnlyList<ShowcaseProductTypePreset> All { get; } = new[]
        {
            Auto,
            new ShowcaseProductTypePreset(
                "ao-dai",
                "Áo dài / truyền thống",
                "Loại: áo dài / trang phục truyền thống Việt Nam. Voiceover có thể nhấn lụa, thêu, tà áo, xẻ tà, phom eo. Prompt clip (Veo/Kling): CHỈ mô tả chi tiết NHÌN THẤY trong từng ảnh — áo trơn → fabric as shown, KHÔNG embroidery/zipper nếu ảnh không có; có thêu rõ → mới nói embroidery; closure = tie cords/buttons/hooks as shown (KHÔNG đoán zipper). Veo flatlay: giữ mẫu + chuyển động camera hoặc tay nhẹ (no face); bắt buộc câu Garment color, silhouette and visible details stay exactly as shown. On-model Flow-safe: fabric panels/hem sway, mandarin collar. Kling: chuyển động vừa phải + khớp pose; preserve silhouette + visible fabric details from photo."),
            new ShowcaseProductTypePreset(
                "fashion-top",
                "Áo / top",
                "Loại: áo thời trang (sơ mi, áo kiểu, áo croptop…). Nhấn chất vải, form vai, cổ tay, phom mặc. Flatlay: chi tiết cổ/nút/vải. On-model: sleeve/collar adjustment, fabric drape."),
            new ShowcaseProductTypePreset(
                "dress-skirt",
                "Váy / đầm",
                "Loại: váy, đầm. Nhấn phom, độ rủ, xoè, chiều dài, chất liệu. Flatlay: silhouette đứng, xoè vải. On-model: hem sway, waist fit, gentle fabric movement."),
            new ShowcaseProductTypePreset(
                "outfit-set",
                "Set đồ / bộ",
                "Loại: set/bộ phối sẵn. Nhấn sự hài hòa bộ, phối phụ kiện, giá trị combo. Flatlay: layout cả bộ + phụ kiện. On-model: coordinated outfit drape."),
            new ShowcaseProductTypePreset(
                "pants",
                "Quần",
                "Loại: quần (jean, tây, jogger…). Nhấn form ống, co giãn, đường may, túi. Flatlay: gập phom, macro vải. On-model: fit at waist/hip, fabric stretch hint."),
            new ShowcaseProductTypePreset(
                "shoes",
                "Giày / dép",
                "Loại: giày/dép. Nhấn form, đế, chất liệu, êm, bám. Flatlay: đế, logo, góc nghiêng. On-model: foot placement on product if shown — ưu tiên sản phẩm."),
            new ShowcaseProductTypePreset(
                "accessory",
                "Phụ kiện",
                "Loại: phụ kiện (túi, kẹp tóc, trang sức, khăn…). Nhấn chi tiết nhỏ, chất liệu, cách phối. Flatlay: macro, tay cầm, styling props."),
            new ShowcaseProductTypePreset(
                "beauty",
                "Mỹ phẩm / skincare",
                "Loại: mỹ phẩm/skincare. Nhấn texture, màu, bao bì, cách dùng. Flatlay: swatch, mở nắp, tay swatch. Tránh on-model face animation."),
            new ShowcaseProductTypePreset(
                "home",
                "Gia dụng / đồ dùng",
                "Loại: gia dụng/đồ dùng. Nhấn công năng, chất liệu, tiện ích hàng ngày. Flatlay: tay demo dùng, so sánh kích thước."),
            new ShowcaseProductTypePreset(
                "general",
                "Thời trang / chung",
                "Loại: thời trang/sản phẩm phổ thông. Cân bằng chi tiết chất liệu + phom mặc + giá trị sử dụng. Áp dụng quy tắc flatlay/on-model Flow-safe chuẩn."),
            Custom
        };

        public static bool IsCustomPreset(ShowcaseProductTypePreset preset) =>
            preset != null && string.Equals(preset.Id, Custom.Id, StringComparison.Ordinal);

        public static bool IsCustomPrompt(string promptHint) =>
            string.Equals(Normalize(promptHint), CustomPromptSentinel, StringComparison.Ordinal);

        public static int FindIndexByPromptHint(string promptHint)
        {
            var normalized = Normalize(promptHint);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            if (IsCustomPrompt(normalized))
            {
                for (var i = 0; i < All.Count; i++)
                {
                    if (IsCustomPreset(All[i]))
                    {
                        return i;
                    }
                }

                return -1;
            }

            for (var i = 0; i < All.Count; i++)
            {
                if (string.Equals(Normalize(All[i].PromptHint), normalized, StringComparison.Ordinal))
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

            if (IsCustomPrompt(normalized))
            {
                return Custom.DisplayLabel;
            }

            var preset = All.FirstOrDefault(p =>
                string.Equals(Normalize(p.PromptHint), normalized, StringComparison.Ordinal));
            return preset?.DisplayLabel ?? promptHint.Trim();
        }

        public static string ResolvePromptForGemini(string promptHint)
        {
            if (IsCustomPrompt(promptHint))
            {
                return string.Empty;
            }

            return Normalize(promptHint);
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim();
    }
}
