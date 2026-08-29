using System;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni.Services.Showcase
{
    public enum ShowcaseDisplayLineEffectKind
    {
        Hook,
        Body
    }

    public static class ShowcaseDisplayLineAnimationHelper
    {
        public static readonly string DefaultStorage = string.Empty;

        public const string PlainComboLabel = "Cả dòng (plain)";

        public static void PopulateCombo(ComboBox combo, ShowcaseDisplayLineEffectKind kind)
        {
            if (combo == null)
            {
                return;
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var entry in ShowcaseHookAnimationCatalog.All)
            {
                combo.Items.Add(entry.ComboLabel);
            }

            if (kind == ShowcaseDisplayLineEffectKind.Body)
            {
                combo.Items.Add(PlainComboLabel);
            }
        }

        public static void PopulateEffectCellItems(DataGridViewComboBoxCell cell, ShowcaseDisplayLineEffectKind kind)
        {
            if (cell == null)
            {
                return;
            }

            cell.Items.Clear();
            foreach (var entry in ShowcaseHookAnimationCatalog.All)
            {
                cell.Items.Add(entry.ComboLabel);
            }

            if (kind == ShowcaseDisplayLineEffectKind.Body)
            {
                cell.Items.Add(PlainComboLabel);
            }
        }

        public static string DefaultEffectLabel(ShowcaseDisplayLineEffectKind kind)
        {
            var first = ShowcaseHookAnimationCatalog.All.FirstOrDefault();
            return first?.ComboLabel ?? string.Empty;
        }

        public static string DefaultEffectStorage(ShowcaseDisplayLineEffectKind kind)
        {
            if (kind == ShowcaseDisplayLineEffectKind.Hook)
            {
                return ShowcaseHookAnimationCatalog.StorageFromSelectedIndex(0);
            }

            return "Pop";
        }

        public static void SelectStorage(ComboBox combo, ShowcaseDisplayLineEffectKind kind, string storage)
        {
            if (combo == null)
            {
                return;
            }

            PopulateCombo(combo, kind);
            if (combo.Items.Count == 0)
            {
                return;
            }

            ApplyStorageToListControl(combo, kind, storage);
        }

        public static void SelectStorageCell(DataGridViewComboBoxCell cell, ShowcaseDisplayLineEffectKind kind, string storage)
        {
            if (cell == null)
            {
                return;
            }

            PopulateEffectCellItems(cell, kind);
            var value = (storage ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value) || string.Equals(value, "(Mặc định Kiểu chữ)", StringComparison.Ordinal))
            {
                if (cell.Items.Count > 0)
                {
                    cell.Value = cell.Items[0];
                }

                return;
            }

            if (kind == ShowcaseDisplayLineEffectKind.Body
                && string.Equals(value, "Plain", StringComparison.OrdinalIgnoreCase))
            {
                if (cell.Items.Count > 0)
                {
                    cell.Value = cell.Items[cell.Items.Count - 1];
                }

                return;
            }

            var label = ShowcaseHookAnimationCatalog.ComboLabel(value);
            if (cell.Items.Contains(label))
            {
                cell.Value = label;
            }
            else if (cell.Items.Count > 0)
            {
                cell.Value = cell.Items[0];
            }
        }

        public static string GetSelectedStorage(ComboBox combo, ShowcaseDisplayLineEffectKind kind)
        {
            if (combo == null || combo.Items.Count == 0)
            {
                return DefaultEffectStorage(kind);
            }

            return StorageFromSelectedIndex(combo.SelectedIndex, kind, combo.SelectedItem?.ToString());
        }

        public static string GetSelectedStorageFromCell(DataGridViewComboBoxCell cell, ShowcaseDisplayLineEffectKind kind)
        {
            if (cell == null || cell.Items.Count == 0)
            {
                return DefaultEffectStorage(kind);
            }

            var idx = cell.Value != null ? cell.Items.IndexOf(cell.Value) : -1;
            return StorageFromSelectedIndex(idx, kind, cell.Value?.ToString());
        }

        private static void ApplyStorageToListControl(ComboBox combo, ShowcaseDisplayLineEffectKind kind, string storage)
        {
            var value = (storage ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value) || string.Equals(value, "(Mặc định Kiểu chữ)", StringComparison.Ordinal))
            {
                combo.SelectedIndex = 0;
                return;
            }

            if (kind == ShowcaseDisplayLineEffectKind.Body
                && string.Equals(value, "Plain", StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = combo.Items.Count - 1;
                return;
            }

            var idx = ShowcaseHookAnimationCatalog.SelectedIndexFromStorage(value);
            if (idx >= 0 && idx < combo.Items.Count)
            {
                combo.SelectedIndex = idx;
                return;
            }

            combo.SelectedIndex = 0;
        }

        private static string StorageFromSelectedIndex(int selectedIndex, ShowcaseDisplayLineEffectKind kind, string selectedText)
        {
            if (selectedIndex < 0)
            {
                return DefaultEffectStorage(kind);
            }

            if (kind == ShowcaseDisplayLineEffectKind.Body)
            {
                if (string.Equals(selectedText, PlainComboLabel, StringComparison.Ordinal)
                    || selectedIndex >= ShowcaseHookAnimationCatalog.All.Count)
                {
                    return "Plain";
                }
            }

            if (selectedIndex >= 0 && selectedIndex < ShowcaseHookAnimationCatalog.All.Count)
            {
                return ShowcaseHookAnimationCatalog.StorageFromSelectedIndex(selectedIndex);
            }

            return DefaultEffectStorage(kind);
        }
    }
}
