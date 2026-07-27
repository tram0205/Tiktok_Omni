using System;
using System.Collections.Generic;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Danh mục hiệu ứng phụ đề hook Showcase (storage ↔ nhãn UI ↔ ASS mode).</summary>
    public static class ShowcaseHookAnimationCatalog
    {
        public const string DefaultStorage = "PopStrong";

        private static readonly HookAnimationEntry[] Entries =
        {
            new HookAnimationEntry("PopStrong", "Pop mạnh (vàng, giữa)", "Pop mạnh", ReupKaraokeAnimationMode.Pop),
            new HookAnimationEntry("Highlight", "Karaoke (tô màu)", "Karaoke", ReupKaraokeAnimationMode.Highlight),
            new HookAnimationEntry("FadeIn", "Hiện dần", "Hiện dần", ReupKaraokeAnimationMode.FadeIn),
            new HookAnimationEntry("Slam", "Slam (pop cực mạnh)", "Slam", ReupKaraokeAnimationMode.Pop),
            new HookAnimationEntry("Bounce", "Bounce (nảy overshoot)", "Bounce", ReupKaraokeAnimationMode.Bounce),
            new HookAnimationEntry("LineZoom", "Zoom cả câu", "Zoom câu", ReupKaraokeAnimationMode.LineZoom),
            new HookAnimationEntry("Shake", "Rung nhẹ", "Rung", ReupKaraokeAnimationMode.Shake),
            new HookAnimationEntry("FlashColor", "Flash vàng ↔ trắng", "Flash", ReupKaraokeAnimationMode.FlashColor),
            new HookAnimationEntry("NeonSale", "Neon đỏ (sale/FOMO)", "Neon đỏ", ReupKaraokeAnimationMode.NeonSale),
            new HookAnimationEntry("Typewriter", "Máy chữ (từng từ)", "Máy chữ", ReupKaraokeAnimationMode.Typewriter),
            new HookAnimationEntry("GlowPulse", "Glow viền sáng", "Glow", ReupKaraokeAnimationMode.GlowPulse)
        };

        public static IReadOnlyList<HookAnimationEntry> All => Entries;

        public static string DisplayLabel(string storage)
        {
            var entry = FindByStorage(storage);
            return entry?.ShortLabel ?? "Pop mạnh";
        }

        public static string ComboLabel(string storage)
        {
            var entry = FindByStorage(storage);
            return entry?.ComboLabel ?? Entries[0].ComboLabel;
        }

        public static string StorageFromSelectedIndex(int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < Entries.Length)
            {
                return Entries[selectedIndex].Storage;
            }

            return DefaultStorage;
        }

        public static int SelectedIndexFromStorage(string storage)
        {
            var value = (storage ?? string.Empty).Trim();
            for (var i = 0; i < Entries.Length; i++)
            {
                if (string.Equals(Entries[i].Storage, value, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            if (string.Equals(value, "Pop", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return 0;
        }

        public static ReupKaraokeAnimationMode ParseMode(string storage)
        {
            var entry = FindByStorage(storage);
            return entry?.Mode ?? ReupKaraokeAnimationMode.Pop;
        }

        public static bool IsSlam(string storage)
            => string.Equals((storage ?? string.Empty).Trim(), "Slam", StringComparison.OrdinalIgnoreCase);

        public static bool IsNeonSale(string storage)
            => string.Equals((storage ?? string.Empty).Trim(), "NeonSale", StringComparison.OrdinalIgnoreCase);

        public static bool IsPopFamily(string storage)
        {
            var mode = ParseMode(storage);
            return mode == ReupKaraokeAnimationMode.Pop
                   || mode == ReupKaraokeAnimationMode.NeonSale;
        }

        private static HookAnimationEntry FindByStorage(string storage)
        {
            var value = (storage ?? string.Empty).Trim();
            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Storage, value, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            if (string.Equals(value, "Pop", StringComparison.OrdinalIgnoreCase))
            {
                return Entries[0];
            }

            return null;
        }

        public sealed class HookAnimationEntry
        {
            public HookAnimationEntry(string storage, string comboLabel, string shortLabel, ReupKaraokeAnimationMode mode)
            {
                Storage = storage;
                ComboLabel = comboLabel;
                ShortLabel = shortLabel;
                Mode = mode;
            }

            public string Storage { get; }

            public string ComboLabel { get; }

            public string ShortLabel { get; }

            public ReupKaraokeAnimationMode Mode { get; }
        }
    }
}
