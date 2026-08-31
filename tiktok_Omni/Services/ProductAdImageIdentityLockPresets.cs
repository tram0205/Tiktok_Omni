using System;
using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    public sealed class ProductAdImageIdentityLockPreset
    {
        public ProductAdImageIdentityLockPreset(string id, string displayLabel, string promptHint)
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

    public static class ProductAdImageIdentityLockPresets
    {
        public static ProductAdImageIdentityLockPreset Auto { get; } =
            new ProductAdImageIdentityLockPreset("auto", "— Tự động —", string.Empty);

        public static IReadOnlyList<ProductAdImageIdentityLockPreset> All { get; } = new[]
        {
            Auto,
            new ProductAdImageIdentityLockPreset(
                "face-and-product",
                "Giữ mặt + SP",
                "Lock both the model's face/identity AND the garment exactly as in the reference. Do not beautify, age-shift, or restyle the person. Do not redesign the product."),
            new ProductAdImageIdentityLockPreset(
                "product-first",
                "Ưu tiên SP",
                "Product lock is highest priority. Model identity should stay recognizable but framing may favor the garment. Never invent new product details."),
            new ProductAdImageIdentityLockPreset(
                "product-only",
                "Chỉ SP (flatlay/macro)",
                "Product-only shots (flatlay/macro): no model face required. Keep garment color, print, logo, and form identical to the reference.")
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
