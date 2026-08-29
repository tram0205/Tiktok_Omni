using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni.Services.Showcase
{
    public static class ShowcaseSubtitleColourPresetCatalog
    {
        public const string BodyDefaultLabel = "Trắng";
        public const string HookDefaultLabel = "Vàng";

        public sealed class Preset
        {
            public Preset(string label, string primaryAss, string secondaryAss)
            {
                Label = label;
                PrimaryAss = primaryAss;
                SecondaryAss = secondaryAss;
            }

            public string Label { get; }

            public string PrimaryAss { get; }

            public string SecondaryAss { get; }
        }

        public static readonly Preset[] All =
        {
            new Preset("Trắng", "&H00FFFFFF", "&H00D7FF00"),
            new Preset("Vàng", "&H0000FFFF", "&H00FFFF00"),
            new Preset("Cyan", "&H00FFFF00", "&H00FFFFFF"),
            new Preset("Hồng", "&H00C080FF", "&H00FFFFFF"),
            new Preset("Xanh lá", "&H0000FF00", "&H00FFFFFF")
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

        public static string PrimaryAssFromLabel(string label)
        {
            var match = FindByLabel(label);
            return match?.PrimaryAss ?? All[0].PrimaryAss;
        }

        public static string SecondaryAssFromLabel(string label)
        {
            var match = FindByLabel(label);
            return match?.SecondaryAss ?? All[0].SecondaryAss;
        }

        public static string SecondaryAssFromPrimaryAss(string primaryAss)
        {
            var p = (primaryAss ?? string.Empty).Trim();
            foreach (var preset in All)
            {
                if (string.Equals(preset.PrimaryAss, p, StringComparison.OrdinalIgnoreCase))
                {
                    return preset.SecondaryAss;
                }
            }

            return All[0].SecondaryAss;
        }

        public static string LabelFromPrimaryAss(string primaryAss, string whenEmptyLabel = null)
        {
            var p = (primaryAss ?? string.Empty).Trim();
            if (p.Length == 0)
            {
                return whenEmptyLabel ?? BodyDefaultLabel;
            }

            foreach (var preset in All)
            {
                if (string.Equals(preset.PrimaryAss, p, StringComparison.OrdinalIgnoreCase))
                {
                    return preset.Label;
                }
            }

            return whenEmptyLabel ?? BodyDefaultLabel;
        }

        public static void SelectLabel(ComboBox combo, string primaryAss, string whenEmptyLabel = null)
        {
            if (combo == null)
            {
                return;
            }

            PopulateCombo(combo);
            var text = LabelFromPrimaryAss(primaryAss, whenEmptyLabel ?? BodyDefaultLabel);
            combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(i => string.Equals(i?.ToString(), text, StringComparison.Ordinal))
                               ?? (combo.Items.Count > 0 ? combo.Items[0] : null);
        }

        public static void SelectLabelCell(DataGridViewComboBoxCell cell, string primaryAss, string whenEmptyLabel)
        {
            if (cell == null)
            {
                return;
            }

            PopulateComboColumn(cell.OwningColumn as DataGridViewComboBoxColumn);
            var label = LabelFromPrimaryAss(primaryAss, whenEmptyLabel);
            if (cell.Items.Contains(label))
            {
                cell.Value = label;
            }
            else if (cell.Items.Count > 0)
            {
                cell.Value = cell.Items[0];
            }
        }

        public static Color ToDrawingColor(string primaryAss)
        {
            return TryParseAssColour(primaryAss, out var c) ? c : Color.White;
        }

        public static Color ContrastTextColor(Color back)
        {
            var luminance = (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) / 255d;
            return luminance > 0.62 ? Color.FromArgb(28, 32, 40) : Color.White;
        }

        public static bool TryParseAssColour(string ass, out Color color)
        {
            color = Color.White;
            var s = (ass ?? string.Empty).Trim();
            if (s.Length < 8 || !s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var hex = s.Substring(2).TrimEnd('&');
            if (hex.Length == 8)
            {
                hex = hex.Substring(2);
            }

            if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var bgr))
            {
                return false;
            }

            var b = (bgr >> 16) & 0xFF;
            var g = (bgr >> 8) & 0xFF;
            var r = bgr & 0xFF;
            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        private static Preset FindByLabel(string label)
        {
            var t = (label ?? string.Empty).Trim();
            return All.FirstOrDefault(p => string.Equals(p.Label, t, StringComparison.Ordinal));
        }
    }
}
