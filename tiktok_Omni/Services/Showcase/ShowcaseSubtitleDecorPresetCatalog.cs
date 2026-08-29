using System;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Viền / bóng ASS — áp dụng Hook + thân + từng dòng.</summary>
    public static class ShowcaseSubtitleDecorPresetCatalog
    {
        public const string DefaultLabel = "Mặc định";

        public sealed class Preset
        {
            public Preset(string storage, string label, int outline, int shadow)
            {
                Storage = storage;
                Label = label;
                OutlineWidth = outline;
                ShadowDepth = shadow;
            }

            public string Storage { get; }

            public string Label { get; }

            public int OutlineWidth { get; }

            public int ShadowDepth { get; }
        }

        public static readonly Preset[] All =
        {
            new Preset(string.Empty, DefaultLabel, 10, 2),
            new Preset("ThickOutline", "Viền dày", 18, 3),
            new Preset("DeepShadow", "Bóng đậm", 8, 14),
            new Preset("SoftGlow", "Glow viền", 14, 1),
            new Preset("Cinema", "Điện ảnh", 16, 6)
        };

        public static void PopulateCombo(ComboBox combo)
        {
            if (combo == null)
            {
                return;
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var p in All)
            {
                combo.Items.Add(p.Label);
            }
        }

        public static void PopulateComboColumn(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.Items.Clear();
            foreach (var p in All)
            {
                column.Items.Add(p.Label);
            }
        }

        public static void Apply(AssSubtitleGeneratorOptions opts, string storage)
        {
            if (opts == null)
            {
                return;
            }

            var preset = FindByStorage(storage) ?? All[0];
            opts.OutlineWidth = preset.OutlineWidth;
            opts.ShadowDepth = preset.ShadowDepth;
        }

        public static string LabelFromStorage(string storage)
        {
            return (FindByStorage(storage) ?? All[0]).Label;
        }

        public static string StorageFromLabel(string label)
        {
            var match = All.FirstOrDefault(p => string.Equals(p.Label, (label ?? string.Empty).Trim(), StringComparison.Ordinal));
            return match?.Storage ?? string.Empty;
        }

        public static void SelectLabel(ComboBox combo, string storage)
        {
            if (combo == null)
            {
                return;
            }

            PopulateCombo(combo);
            var text = LabelFromStorage(storage);
            combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(i => string.Equals(i?.ToString(), text, StringComparison.Ordinal))
                               ?? (combo.Items.Count > 0 ? combo.Items[0] : null);
        }

        public static void SelectLabelCell(DataGridViewComboBoxCell cell, string storage)
        {
            if (cell == null)
            {
                return;
            }

            PopulateComboColumn(cell.OwningColumn as DataGridViewComboBoxColumn);
            var text = LabelFromStorage(storage);
            if (cell.Items.Contains(text))
            {
                cell.Value = text;
            }
            else if (cell.Items.Count > 0)
            {
                cell.Value = cell.Items[0];
            }
        }

        private static Preset FindByStorage(string storage)
        {
            var s = (storage ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return All[0];
            }

            return All.FirstOrDefault(p => string.Equals(p.Storage, s, StringComparison.OrdinalIgnoreCase));
        }
    }
}
