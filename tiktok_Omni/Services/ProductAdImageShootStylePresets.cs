using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services
{
    public sealed class ProductAdImageShootStylePreset
    {
        public ProductAdImageShootStylePreset(string id, string displayLabel, string promptHint)
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

    public static class ProductAdImageShootStylePresets
    {
        public static ProductAdImageShootStylePreset Auto { get; } =
            new ProductAdImageShootStylePreset("auto", "— Tự động —", string.Empty);

        public static IReadOnlyList<ProductAdImageShootStylePreset> All { get; } = new[]
        {
            Auto,
            new ProductAdImageShootStylePreset(
                "studio-clean",
                "Studio sạch",
                "Clean fashion studio: seamless paper or cyclorama, even softbox lighting, no clutter, catalog-ready."),
            new ProductAdImageShootStylePreset(
                "street",
                "Đường phố",
                "Urban street fashion: sidewalk/storefront, natural daylight or golden hour, candid editorial energy."),
            new ProductAdImageShootStylePreset(
                "cafe-indoor",
                "Cafe / indoor",
                "Indoor cafe / lifestyle: warm practical lights, wood/brick textures, intimate lifestyle fashion."),
            new ProductAdImageShootStylePreset(
                "shopee-catalog",
                "Catalog Shopee",
                "Shopee/marketplace catalog: bright even light, simple backdrop, product fully visible, selling-focused."),
            new ProductAdImageShootStylePreset(
                "editorial-dark",
                "Editorial tối",
                "Dark editorial: low-key lighting, rich shadows, magazine mood, dramatic but product still readable.")
        };

        public static int FindIndexByPromptHint(string promptHint)
        {
            var normalized = Normalize(promptHint);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
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
            var index = FindIndexByPromptHint(promptHint);
            if (index >= 0)
            {
                return All[index].DisplayLabel;
            }

            return string.IsNullOrWhiteSpace(promptHint) ? Auto.DisplayLabel : promptHint.Trim();
        }

        public static string ResolvePromptForGemini(string promptHint)
        {
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
