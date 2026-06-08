using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void ApplyGlobalButtonThemes()
        {
            ApplyButtonThemesRecursive(this);
            ApplyAppTypographyRecursive(this);
            HighlightSidebarForSelectedTab();
            ApplyGlobalLogChrome();
        }

        internal void ApplyButtonThemesRecursive(Control root)
        {
            if (root == null)
            {
                return;
            }

            if (root is Button btn && !ShouldSkipAutoTheme(btn))
            {
                var role = btn.Tag is ButtonRole tagged
                    ? tagged
                    : UIThemeManager.InferRole(btn.Name, btn.Text);
                btn.ApplyTheme(role);
            }
            else if (root is DataGridView grid)
            {
                ApplyDataGridViewColumnThemes(grid);
            }

            foreach (Control child in root.Controls)
            {
                ApplyButtonThemesRecursive(child);
            }
        }

        private static bool ShouldSkipAutoTheme(Button btn)
        {
            if (btn == null)
            {
                return false;
            }

            if (btn is JellyButton
                || string.Equals(btn.AccessibleName, JellyButton.ChromeTag, StringComparison.Ordinal)
                || string.Equals(btn.Tag as string, JellyButton.ChromeTag, StringComparison.Ordinal)
                || string.Equals(btn.AccessibleName, "SettingsActionChrome", StringComparison.Ordinal)
                || string.Equals(btn.Tag as string, "SettingsActionChrome", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(btn.AccessibleName, "SidebarNav", StringComparison.Ordinal)
                || string.Equals(btn.AccessibleName, "GlobalLogChrome", StringComparison.Ordinal)
                || string.Equals(btn.Tag as string, "SidebarNav", StringComparison.Ordinal)
                || string.Equals(btn.Tag as string, "GlobalLogChrome", StringComparison.Ordinal))
            {
                return true;
            }

            var name = btn.Name ?? string.Empty;
            return name.StartsWith("btnNav", StringComparison.Ordinal)
                || string.Equals(name, "btnAffiliateKeywordsCaption", StringComparison.Ordinal)
                || string.Equals(name, "btnAffiliateProfileCaption", StringComparison.Ordinal)
                || string.Equals(name, "btnEmergencyStop", StringComparison.Ordinal)
                || string.Equals(name, "btnToggleGlobalLog", StringComparison.Ordinal)
                || string.Equals(name, "btnClearLogs", StringComparison.Ordinal)
                || string.Equals(name, "btnExportCurrentLogs", StringComparison.Ordinal)
                || string.Equals(name, "btnOpenLogsFolder", StringComparison.Ordinal);
        }

        private static void ApplyDataGridViewColumnThemes(DataGridView grid)
        {
            if (grid?.Columns == null)
            {
                return;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column is DataGridViewButtonColumn)
                {
                    var role = UIThemeManager.InferGridColumnRole(column.HeaderText);
                    UIThemeManager.ApplyGridColumnTheme(column, role);
                    continue;
                }

                var headerRole = UIThemeManager.InferGridColumnRole(column.HeaderText);
                if (headerRole != ButtonRole.Neutral
                    || (column.HeaderText ?? string.Empty).IndexOf("Sửa", StringComparison.OrdinalIgnoreCase) >= 0
                    || (column.HeaderText ?? string.Empty).IndexOf("Xóa", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UIThemeManager.ApplyGridColumnTheme(column, headerRole);
                }
            }
        }

        internal const float AppInputFontSize = 12F;
        internal const float AppKeywordInputFontSize = 17F;
        internal const float AppGridHeaderFontSize = 11F;
        internal const int AppGridHeaderHeight = 46;
        internal const int AppInputMinHeight = 32;

        internal static readonly Font AppInputFont = new Font("Segoe UI", AppInputFontSize, FontStyle.Regular, GraphicsUnit.Point);
        internal static readonly Font AppKeywordInputFont = new Font("Segoe UI", AppKeywordInputFontSize, FontStyle.Regular, GraphicsUnit.Point);
        internal static readonly Font AppGridHeaderFont = new Font("Segoe UI", AppGridHeaderFontSize, FontStyle.Bold, GraphicsUnit.Point);

        private void ApplyAppTypographyRecursive(Control root)
        {
            if (root == null)
            {
                return;
            }

            if (root is DataGridView grid)
            {
                ApplyAppGridHeaderChrome(grid);
            }
            else if (ShouldApplyInputTypography(root))
            {
                ApplyAppInputChrome(root);
            }

            foreach (Control child in root.Controls)
            {
                ApplyAppTypographyRecursive(child);
            }
        }

        private static bool ShouldApplyInputTypography(Control control)
        {
            if (control == null)
            {
                return false;
            }

            if (control is RichTextBox)
            {
                return false;
            }

            if (!(control is TextBox || control is NumericUpDown || control is ComboBox || control is DateTimePicker))
            {
                return false;
            }

            var name = control.Name ?? string.Empty;
            if (name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Rtb", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        internal static void ApplyAppInputChrome(Control control, Font fontOverride = null)
        {
            if (control == null)
            {
                return;
            }

            var font = fontOverride ?? ResolveAppInputFont(control);
            control.Font = font;

            if (control is TextBox textBox && textBox.Multiline)
            {
                if (!textBox.ReadOnly && textBox.Height < AppInputMinHeight * 2)
                {
                    textBox.MinimumSize = new Size(textBox.MinimumSize.Width, Math.Max(textBox.MinimumSize.Height, AppInputMinHeight));
                }

                return;
            }

            control.MinimumSize = new Size(
                control.MinimumSize.Width,
                Math.Max(control.MinimumSize.Height, AppInputMinHeight));
            if (control.Height < AppInputMinHeight)
            {
                control.Height = AppInputMinHeight;
            }
        }

        private static Font ResolveAppInputFont(Control control)
        {
            var name = control.Name ?? string.Empty;
            if (string.Equals(name, "txtAffiliateKeywords", StringComparison.Ordinal))
            {
                return AppKeywordInputFont;
            }

            return AppInputFont;
        }

        internal static void ApplyAppGridHeaderChrome(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.EnableHeadersVisualStyles = false;
            if (grid.ColumnHeadersHeight < AppGridHeaderHeight)
            {
                grid.ColumnHeadersHeight = AppGridHeaderHeight;
            }

            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            var headerStyle = grid.ColumnHeadersDefaultCellStyle;
            headerStyle.Font = AppGridHeaderFont;
            headerStyle.Padding = new Padding(6, 8, 6, 8);
            headerStyle.WrapMode = DataGridViewTriState.False;
            if (headerStyle.Alignment == DataGridViewContentAlignment.NotSet
                || headerStyle.Alignment == DataGridViewContentAlignment.TopLeft
                || headerStyle.Alignment == DataGridViewContentAlignment.TopCenter
                || headerStyle.Alignment == DataGridViewContentAlignment.TopRight)
            {
                headerStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }

            if (grid.Columns != null && grid.Columns.Count > 0)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    column.HeaderCell.Style.Font = headerStyle.Font;
                    column.HeaderCell.Style.Padding = headerStyle.Padding;
                    column.HeaderCell.Style.WrapMode = headerStyle.WrapMode;
                    column.HeaderCell.Style.Alignment = headerStyle.Alignment;
                }
            }

            if (!grid.ReadOnly)
            {
                grid.DefaultCellStyle.Font = AppInputFont;
                if (grid.RowTemplate.Height < 30)
                {
                    grid.RowTemplate.Height = 30;
                }
            }
        }
    }
}
