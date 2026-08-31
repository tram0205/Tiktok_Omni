using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services
{
    public sealed class ProductAdImageThemePreset
    {
        public ProductAdImageThemePreset(string id, string displayLabel, string promptHint)
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

    public static class ProductAdImageThemePresets
    {
        public const string CustomPromptSentinel = "__product_ad_theme_custom__";

        public static ProductAdImageThemePreset Auto { get; } =
            new ProductAdImageThemePreset("auto", "— Tự động (Gemini) —", string.Empty);

        public static ProductAdImageThemePreset Custom { get; } =
            new ProductAdImageThemePreset(
                "custom",
                "Tuỳ chỉnh: người dùng tự nhập",
                CustomPromptSentinel);

        public static IReadOnlyList<ProductAdImageThemePreset> All { get; } = new[]
        {
            Auto,
            new ProductAdImageThemePreset(
                "catalog-white",
                "Catalog trắng / Shopee",
                "Clean white/light seamless catalog set: even e-commerce lighting, product fully visible, marketplace-ready. Keep garment identical to the reference."),
            new ProductAdImageThemePreset(
                "studio-editorial",
                "Studio editorial",
                "Fashion studio editorial set: cyclorama or seamless, shaped lighting, magazine energy. Product lock stays highest priority."),
            new ProductAdImageThemePreset(
                "street-city",
                "Street / đô thị",
                "Urban street fashion set: sidewalk, storefront, city textures, daylight or golden hour. Do not restyle the garment."),
            new ProductAdImageThemePreset(
                "cafe-lifestyle",
                "Cafe / lifestyle",
                "Cafe lifestyle set: warm interior, wood/brick, practical lights, intimate posing. Keep product details exact."),
            new ProductAdImageThemePreset(
                "beach-resort",
                "Biển / resort",
                "Beach or resort set: sand, sea, pool deck, tropical light. Wind and water must not hide or wet-distort the garment identity."),
            new ProductAdImageThemePreset(
                "office-work",
                "Văn phòng / công sở",
                "Office / workwear set: lobby, desk, glass, commuter city. Professional mood; garment unchanged from reference."),
            new ProductAdImageThemePreset(
                "travel-airport",
                "Du lịch / sân bay",
                "Travel set: airport, platform, luggage, departure-hall light. Product remains the hero, identical to the photo."),
            new ProductAdImageThemePreset(
                "tet-festival",
                "Tết / lễ hội VN",
                "Vietnamese Tết or festival set: red-gold accents, lanterns, traditional street. Do not add embroidery or motifs that are not on the reference garment."),
            new ProductAdImageThemePreset(
                "wedding-event",
                "Wedding / sự kiện",
                "Wedding or evening-event set: banquet light, florals, formal venue. Keep the exact product; no invented embellishment."),
            new ProductAdImageThemePreset(
                "gym-sport",
                "Gym / sport",
                "Gym or sport lifestyle set: studio gym, track, daylight outdoor training. Fabric and cut stay identical to the reference."),
            new ProductAdImageThemePreset(
                "night-neon",
                "Night city / neon",
                "Night city set: neon, wet asphalt, bokeh. Moody light must still read true product color and print from the reference."),
            new ProductAdImageThemePreset(
                "luxury-hotel",
                "Luxury hotel",
                "Luxury hotel set: marble lobby, suite, warm practicals. Elegant but catalog-honest; no restyle of the garment."),
            new ProductAdImageThemePreset(
                "countryside",
                "Đồng quê / thiên nhiên",
                "Countryside / nature set: field, garden, dirt path, soft daylight. Natural backdrop; product lock unchanged."),
            new ProductAdImageThemePreset(
                "rain-mood",
                "Mưa / mood",
                "Rainy mood set: umbrella, wet street, overcast. Do not soak or darken the garment beyond what still matches the reference."),
            new ProductAdImageThemePreset(
                "cherry-spring",
                "Xuân / hoa",
                "Spring floral set: blossoms, park path, soft backlight. Flowers are background only — do not print them onto the product."),
            Custom
        };

        public static bool IsCustomPreset(ProductAdImageThemePreset preset) =>
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
                if (string.Equals(Normalize(All[i].PromptHint), normalized, StringComparison.Ordinal)
                    || string.Equals(All[i].Id, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(All[i].DisplayLabel, normalized, StringComparison.OrdinalIgnoreCase))
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

            var index = FindIndexByPromptHint(promptHint);
            if (index < 0)
            {
                return Normalize(promptHint);
            }

            return All[index].PromptHint;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim();
    }
}
