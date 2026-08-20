using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
                || string.Equals(name, "btnClearWarmupLog", StringComparison.Ordinal)
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
                }
                else
                {
                    var headerRole = UIThemeManager.InferGridColumnRole(column.HeaderText);
                    if (headerRole != ButtonRole.Neutral
                        || (column.HeaderText ?? string.Empty).IndexOf("Sửa", StringComparison.OrdinalIgnoreCase) >= 0
                        || (column.HeaderText ?? string.Empty).IndexOf("Xóa", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UIThemeManager.ApplyGridColumnTheme(column, headerRole);
                    }
                }

                // Ép font tiêu đề (DPI-aware) sau khi ApplyGridColumnTheme.
                if (column.HeaderCell.Style == null)
                {
                    column.HeaderCell.Style = new DataGridViewCellStyle();
                }

                column.HeaderCell.Style.Font = GetScaledHeaderFont(grid);
                column.HeaderCell.Style.Padding = new Padding(6, 8, 6, 8);
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        /// <summary>Font size chuẩn cho ô nhập / ô lưới (Segoe UI).</summary>
        internal const float AppInputFontSize = 12;
        internal const float AppKeywordInputFontSize = 13F;
        internal const float AppGridHeaderFontSize = 12F;

        /// <summary>Font nhãn bên ngoài ô nhập (Label / CheckBox / caption) — dùng chung mọi tab.</summary>
        internal const float AppLabelFontSize = 12;

        /// <summary>Chiều cao header lưới — dùng chung mọi tab.</summary>
        internal const int AppGridHeaderHeight = 76;

        /// <summary>Padding ngang khi đo tiêu đề cột → MinimumWidth / FillWeight.</summary>
        internal const int AppGridHeaderColumnPad = 28;

        /// <summary>Chiều rộng tối thiểu tuyệt đối cho mọi cột (kể cả tiêu đề ngắn).</summary>
        internal const int AppGridColumnMinWidth = 44;

        /// <summary>Chiều cao dòng lưới — dùng chung mọi tab (đủ cho Segoe UI 12pt + padding).</summary>
        internal const int AppDefaultRowHeight = 58;

        /// <summary>
        /// Chiều cao dòng lưới có ComboBox trong ô (Video reup / Triết lý / Auto-post…).
        /// Cao hơn AppDefaultRowHeight để dropdown không bị cắt.
        /// </summary>
        internal const int AppComboGridRowHeight = 50;

        /// <summary>Chiều cao tối thiểu ô nhập đơn dòng — dùng chung mọi tab.</summary>
        internal const int AppDefaultInputHeight = 56;

        /// <summary>Alias tương thích code cũ; luôn bằng AppDefaultInputHeight.</summary>
        internal const int AppInputMinHeight = AppDefaultInputHeight;

        internal const int AppDefaultPadding = 10;
        internal const int AppDefaultMargin = 10;

        /// <summary>Font / chiều cao nút jelly toolbar — dùng chung mọi tab.</summary>
        internal const float AppJellyButtonFontSize = 13F;
        internal const int AppJellyButtonHeight = 66;
        internal const int AppJellyButtonHorizontalPad = 30;
        internal const int AppJellyButtonMinWidth = 72;

        /// <summary>Nút primary lớn (Render & Đóng gói, Bắt đầu Render…) — nổi hơn toolbar.</summary>
        internal const float AppPrimaryActionFontSize = 19F;
        internal const int AppPrimaryActionHeight = 80;
        internal const int AppPrimaryActionHorizontalPad = 40;
        internal const int AppPrimaryActionMinWidth = 450;

        internal static readonly Font AppInputFont = new Font("Segoe UI", AppInputFontSize, FontStyle.Regular, GraphicsUnit.Point);
        internal static readonly Font AppKeywordInputFont = new Font("Segoe UI", AppKeywordInputFontSize, FontStyle.Regular, GraphicsUnit.Point);
        internal static readonly Font AppGridHeaderFont = new Font("Segoe UI", AppGridHeaderFontSize, FontStyle.Bold, GraphicsUnit.Point);
        /// <summary>Font thân lưới — cùng cỡ với ô nhập để đồng bộ giữa các tab.</summary>
        internal static readonly Font AppGridBodyFont = AppInputFont;
        /// <summary>Nhãn thường (bên cạnh ô nhập, mô tả ngắn).</summary>
        internal static readonly Font AppLabelFont = new Font("Segoe UI", AppLabelFontSize, FontStyle.Regular, GraphicsUnit.Point);
        /// <summary>Tiêu đề section / caption — cùng cỡ nhãn, không đậm.</summary>
        internal static readonly Font AppCaptionFont = AppLabelFont;
        /// <summary>Nhãn nghiêng (status phụ).</summary>
        internal static readonly Font AppLabelItalicFont = new Font("Segoe UI", AppLabelFontSize, FontStyle.Italic, GraphicsUnit.Point);
        internal static readonly Font AppJellyButtonFont = new Font("Segoe UI", AppJellyButtonFontSize, FontStyle.Bold, GraphicsUnit.Point);
        internal static readonly Font AppPrimaryActionFont = new Font("Segoe UI", AppPrimaryActionFontSize, FontStyle.Bold, GraphicsUnit.Point);

        private static Font _cachedHeaderFont;
        private static float _lastDpiX;
        private static bool? _isProcessDpiAware;

        [DllImport("user32.dll")]
        private static extern bool IsProcessDPIAware();

        /// <summary>
        /// Kiểm tra process có DPI-aware (app.manifest dpiAware / PerMonitorV2).
        /// </summary>
        internal static bool IsAppDpiAware()
        {
            if (_isProcessDpiAware.HasValue)
            {
                return _isProcessDpiAware.Value;
            }

            try
            {
                _isProcessDpiAware = IsProcessDPIAware();
            }
            catch
            {
                _isProcessDpiAware = false;
            }

            System.Diagnostics.Debug.WriteLine(
                $"App DPI-aware (app.manifest): {_isProcessDpiAware.Value}");
            return _isProcessDpiAware.Value;
        }

        /// <summary>
        /// Font tiêu đề cột theo DPI hiện tại (cache 1 instance / DPI — tránh tạo Font mới mỗi lần gọi).
        /// </summary>
        internal static Font GetScaledHeaderFont(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return AppGridHeaderFont;
            }

            // Log một lần nếu thiếu DPI awareness (font Point sẽ bị OS bitmap-scale lệch).
            if (!_isProcessDpiAware.HasValue)
            {
                IsAppDpiAware();
            }

            float dpiX;
            try
            {
                if (grid.IsHandleCreated)
                {
                    using (var g = grid.CreateGraphics())
                    {
                        dpiX = g.DpiX;
                    }
                }
                else
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        dpiX = g.DpiX;
                    }
                }
            }
            catch
            {
                return AppGridHeaderFont;
            }

            if (_cachedHeaderFont == null || Math.Abs(_lastDpiX - dpiX) > 0.01f)
            {
                _lastDpiX = dpiX;
                var dpiAware = IsAppDpiAware();
                var dpiScale = dpiX / 96f;
                // Point unit: process DPI-aware thì GDI+ tự scale → giữ AppGridHeaderFontSize.
                // Chưa aware: nhân dpiScale (DpiX thường vẫn 96 → size không đổi; cần app.manifest).
                var scaledSize = dpiAware
                    ? AppGridHeaderFontSize
                    : AppGridHeaderFontSize * Math.Max(1f, dpiScale);
                var previous = _cachedHeaderFont;
                _cachedHeaderFont = new Font("Segoe UI", scaledSize, FontStyle.Bold, GraphicsUnit.Point);
                previous?.Dispose();

                System.Diagnostics.Debug.WriteLine(
                    $"GetScaledHeaderFont: size={scaledSize}, dpiX={dpiX}, dpiAware={dpiAware}");
            }

            return _cachedHeaderFont;
        }

        private static readonly Color AppJellyButtonForeColor = Color.FromArgb(245, 247, 250);

        internal static int MeasureAppJellyButtonTextWidth(string text, Font font = null, int buttonHeight = 0)
        {
            var measureFont = font ?? AppJellyButtonFont;
            var height = buttonHeight > 0 ? buttonHeight : AppJellyButtonHeight;
            return TextRenderer.MeasureText(
                text ?? string.Empty,
                measureFont,
                new Size(int.MaxValue, height),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;
        }

        /// <summary>
        /// Nút jelly toolbar chuẩn: font/height chung, width = chữ + pad.
        /// heightOverride: dùng khi cần thấp hơn (ô Settings cạnh input) hoặc cao hơn (profile toolbar).
        /// </summary>
        internal static JellyButton CreateAppJellyButton(
            string name,
            string text,
            Color tint,
            int? heightOverride = null,
            int minWidth = AppJellyButtonMinWidth,
            int? horizontalPad = null,
            Padding? margin = null,
            bool lockSize = true,
            Font fontOverride = null,
            float? fillOpacity = null)
        {
            var height = heightOverride ?? AppJellyButtonHeight;
            var font = fontOverride ?? AppJellyButtonFont;
            var pad = horizontalPad ?? AppJellyButtonHorizontalPad;
            var width = Math.Max(minWidth, MeasureAppJellyButtonTextWidth(text, font, height) + pad);

            var btn = new JellyButton
            {
                Name = name,
                Text = text,
                Font = font,
                JellyTint = tint,
                JellyFillOpacity = fillOpacity ?? (1f - JellyButton.DefaultTransparency),
                ForeColor = AppJellyButtonForeColor,
                AutoSize = false,
                Height = height,
                Width = width,
                MinimumSize = new Size(width, height),
                Margin = margin ?? new Padding(0, 0, 6, 0),
                Tag = JellyButton.ChromeTag,
                AccessibleName = JellyButton.ChromeTag
            };

            if (lockSize)
            {
                btn.MaximumSize = new Size(width, height);
            }

            return btn;
        }

        /// <summary>Nút primary lớn — cao hơn toolbar, font đậm hơn.</summary>
        internal static JellyButton CreateAppPrimaryJellyButton(
            string name,
            string text,
            Color tint,
            int minWidth = AppPrimaryActionMinWidth,
            Padding? margin = null)
        {
            return CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: AppPrimaryActionHeight,
                minWidth: minWidth,
                horizontalPad: AppPrimaryActionHorizontalPad,
                margin: margin ?? new Padding(4, 4, 12, 6),
                lockSize: true,
                fontOverride: AppPrimaryActionFont);
        }

        /// <summary>Đo lại width theo Text hiện tại (khi đổi nhãn lúc runtime).</summary>
        internal static void ResizeAppJellyButton(Button button, int? heightOverride = null, int minWidth = AppJellyButtonMinWidth, int? horizontalPad = null)
        {
            if (button == null || button.IsDisposed)
            {
                return;
            }

            var height = heightOverride
                ?? (button.Height > 0 ? button.Height : AppJellyButtonHeight);
            var font = button.Font ?? AppJellyButtonFont;
            var pad = horizontalPad ?? (height >= AppPrimaryActionHeight
                ? AppPrimaryActionHorizontalPad
                : AppJellyButtonHorizontalPad);
            var width = Math.Max(minWidth, MeasureAppJellyButtonTextWidth(button.Text, font, height) + pad);

            button.AutoSize = false;
            button.Font = font;
            button.Height = height;
            button.Width = width;
            button.MinimumSize = new Size(width, height);
            button.MaximumSize = new Size(width, height);
        }

        private void ApplyAppTypographyRecursive(Control root)
        {
            if (root == null)
            {
                return;
            }

            if (root is DataGridView grid)
            {
                ApplyAppGridChrome(grid);
            }
            else if (ShouldApplyInputTypography(root))
            {
                ApplyAppInputChrome(root);
            }
            else if (ShouldApplyLabelTypography(root))
            {
                ApplyAppLabelChrome(root);
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

        /// <summary>Nhãn / checkbox / GroupBox title — chữ ngoài khung nhập.</summary>
        private static bool ShouldApplyLabelTypography(Control control)
        {
            if (control == null)
            {
                return false;
            }

            if (!(control is Label || control is CheckBox || control is RadioButton || control is GroupBox))
            {
                return false;
            }

            // LinkLabel giữ style riêng.
            if (control is LinkLabel)
            {
                return false;
            }

            var name = control.Name ?? string.Empty;
            if (name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Rtb", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Nav", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(control.AccessibleName, "SidebarNav", StringComparison.Ordinal)
                || string.Equals(control.Tag as string, "SidebarNav", StringComparison.Ordinal)
                || string.Equals(control.Tag as string, "SkipAppLabelFont", StringComparison.Ordinal)
                || string.Equals(control.AccessibleName, "SkipAppLabelFont", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        internal static void ApplyAppLabelChrome(Control control)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            // Nhãn không đậm; chỉ giữ Italic cho status phụ.
            if (control.Font != null && control.Font.Italic && !control.Font.Bold)
            {
                control.Font = AppLabelItalicFont;
            }
            else
            {
                control.Font = AppLabelFont;
            }
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
                if (!textBox.ReadOnly && textBox.Height < AppDefaultInputHeight * 2)
                {
                    textBox.MinimumSize = new Size(
                        textBox.MinimumSize.Width,
                        Math.Max(textBox.MinimumSize.Height, AppDefaultInputHeight));
                }

                return;
            }

            control.MinimumSize = new Size(
                control.MinimumSize.Width,
                Math.Max(control.MinimumSize.Height, AppDefaultInputHeight));
            if (control.Height < AppDefaultInputHeight)
            {
                control.Height = AppDefaultInputHeight;
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

        /// <summary>Áp dụng header + chiều cao dòng + font thân lưới + độ rộng cột theo tiêu đề. An toàn gọi nhiều lần.</summary>
        internal static void ApplyAppGridChrome(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            ApplyAppGridHeaderChrome(grid);
            AppGridSttColumn.EnsureFirstColumn(grid);
            EnsureAppGridRowHeights(grid);
            ApplyAppGridColumnHeaderWidths(grid);
            EnsureAppGridHeaderFontSurviveParenting(grid);
        }

        private sealed class AppGridHeaderFontHookState
        {
            public bool Reapplying;
        }

        private static readonly ConditionalWeakTable<DataGridView, AppGridHeaderFontHookState> AppGridHeaderFontParentHooks =
            new ConditionalWeakTable<DataGridView, AppGridHeaderFontHookState>();

        /// <summary>
        /// net472: Controls.Add / OnFontChanged ghi đè ColumnHeadersDefaultCellStyle.Font về font ambient.
        /// FontChanged chạy sau OnFontChanged — đây là chỗ bắt buộc phải áp lại header.
        /// </summary>
        private static void EnsureAppGridHeaderFontSurviveParenting(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            if (AppGridHeaderFontParentHooks.TryGetValue(grid, out _))
            {
                return;
            }

            var state = new AppGridHeaderFontHookState();
            AppGridHeaderFontParentHooks.Add(grid, state);

            void Reapply(object sender, EventArgs e)
            {
                if (!(sender is DataGridView g) || g.IsDisposed || state.Reapplying)
                {
                    return;
                }

                var expectedHeaderFont = GetScaledHeaderFont(g);

                // Nếu đã đúng font tiêu đề (DPI) thì bỏ qua (tránh đo width lặp vô ích).
                if (ReferenceEquals(g.ColumnHeadersDefaultCellStyle?.Font, expectedHeaderFont)
                    && HeaderCellsUseAppGridHeaderFont(g))
                {
                    return;
                }

                void Do()
                {
                    if (g.IsDisposed || state.Reapplying)
                    {
                        return;
                    }

                    var expected = GetScaledHeaderFont(g);
                    if (ReferenceEquals(g.ColumnHeadersDefaultCellStyle?.Font, expected)
                        && HeaderCellsUseAppGridHeaderFont(g))
                    {
                        return;
                    }

                    state.Reapplying = true;
                    try
                    {
                        ApplyAppGridHeaderChrome(g);
                        ApplyAppGridColumnHeaderWidths(g);
                    }
                    finally
                    {
                        state.Reapplying = false;
                    }
                }

                // Gọi trực tiếp Do() để font đổi ngay (không BeginInvoke — tránh chậm hơn luồng vẽ).
                Do();
            }

            grid.ParentChanged += Reapply;
            grid.HandleCreated += Reapply;
            grid.FontChanged += Reapply;

            // Đã gắn parent trước khi hook: áp lại ngay.
            if (grid.Parent != null || grid.IsHandleCreated)
            {
                Reapply(grid, EventArgs.Empty);
            }
        }

        private static bool HeaderCellsUseAppGridHeaderFont(DataGridView grid)
        {
            if (grid?.Columns == null || grid.Columns.Count == 0)
            {
                return true;
            }

            var expected = GetScaledHeaderFont(grid);
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column?.HeaderCell?.Style?.Font != null
                    && !ReferenceEquals(column.HeaderCell.Style.Font, expected))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sau khi font tiêu đề đã set: đo độ dài chữ HeaderText → đặt MinimumWidth + FillWeight
        /// tỷ lệ theo tiêu đề (tiêu đề dài = cột rộng hơn). Gọi sau khi thêm cột / đổi HeaderText.
        /// </summary>
        internal static void ApplyAppGridColumnHeaderWidths(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed || grid.Columns == null || grid.Columns.Count == 0)
            {
                return;
            }

            // Dùng đúng font tiêu đề đã áp (DPI-scaled / ColumnHeadersDefaultCellStyle).
            var headerFont = grid.ColumnHeadersDefaultCellStyle?.Font ?? GetScaledHeaderFont(grid);
            var headerPadding = grid.ColumnHeadersDefaultCellStyle?.Padding ?? new Padding(6, 8, 6, 8);
            var useFill = grid.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.Fill;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column == null)
                {
                    continue;
                }

                if (AppGridSttColumn.IsSttColumn(column))
                {
                    AppGridSttColumn.ApplyColumnWidth(grid, column, useFill, headerFont, headerPadding);
                    continue;
                }

                if (string.Equals(column.Tag as string, "SkipHeaderWidth", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!column.Visible)
                {
                    continue;
                }

                var headerText = (column.HeaderText ?? string.Empty).Trim();
                if (headerText.Length == 0 && column is DataGridViewButtonColumn buttonCol)
                {
                    headerText = (buttonCol.Text ?? string.Empty).Trim();
                }

                if (headerText.Length == 0)
                {
                    headerText = " ";
                }

                var textWidth = TextRenderer.MeasureText(
                    headerText,
                    headerFont,
                    new Size(int.MaxValue, AppGridHeaderHeight),
                    TextFormatFlags.SingleLine
                        | TextFormatFlags.NoPadding
                        | TextFormatFlags.GlyphOverhangPadding).Width;

                var measured = Math.Max(
                    AppGridColumnMinWidth,
                    textWidth + AppGridHeaderColumnPad + headerPadding.Horizontal + 4);

                if (column is DataGridViewComboBoxColumn || column is DataGridViewButtonColumn)
                {
                    measured += 18;
                }

                // Luôn theo tiêu đề — ghi đè MinimumWidth / FillWeight cũ (không giữ weight theo nội dung).
                column.MinimumWidth = measured;

                if (useFill || column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    column.FillWeight = Math.Max(1f, measured);
                }
                else
                {
                    column.Width = measured;
                }
            }
        }

        /// <summary>
        /// Đặt chiều cao dòng tối thiểu cho lưới có ComboBox trong ô.
        /// Gọi trước hoặc sau ApplyAppGridChrome; EnsureAppGridRowHeights sẽ tôn trọng giá trị này.
        /// </summary>
        internal static void ApplyAppComboGridRowHeight(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            if (grid.RowTemplate.Height < AppComboGridRowHeight)
            {
                grid.RowTemplate.Height = AppComboGridRowHeight;
            }

            if (grid.RowTemplate.MinimumHeight < AppDefaultRowHeight)
            {
                grid.RowTemplate.MinimumHeight = AppDefaultRowHeight;
            }
        }

        private static bool GridHasComboBoxColumn(DataGridView grid)
        {
            if (grid?.Columns == null || grid.Columns.Count == 0)
            {
                return false;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column is DataGridViewComboBoxColumn)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void ApplyAppGridHeaderChrome(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed) return;

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, AppGridHeaderHeight);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.AllowUserToResizeRows = false;

            // 1. ÉP CỨNG FONT TỔNG (DPI-aware; tuyệt đối không xài Clone nữa)
            var newFont = GetScaledHeaderFont(grid);
            grid.ColumnHeadersDefaultCellStyle.Font = newFont;

            // Debug: xem font tiêu đề thực sự được gán
            System.Diagnostics.Debug.WriteLine(
                $"Header Font set to: {grid.ColumnHeadersDefaultCellStyle.Font?.Size} (grid={grid.Name})");

            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 8, 6, 8);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 44, 54);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;

            // 2. LƯỚT QUA TỪNG CỘT HIỆN TẠI ĐỂ ÉP CHẾT FONT
            if (grid.Columns != null && grid.Columns.Count > 0)
            {
                var scaledFont = GetScaledHeaderFont(grid);
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    // Chỉ ép HeaderCell — không đụng column.DefaultCellStyle (font thân ô).
                    column.HeaderCell.Style.Font = scaledFont;
                    column.HeaderCell.Style.Padding = new Padding(6, 8, 6, 8);
                    column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                    // Cưỡng bức inheritance (một số bản WinForms cho phép set trên InheritedStyle).
                    try
                    {
                        column.InheritedStyle.Font = scaledFont;
                    }
                    catch (NotSupportedException)
                    {
                        // InheritedStyle read-only trên một số runtime — bỏ qua.
                    }
                }

                // Ép vẽ lại header ngay sau vòng lặp.
                grid.Refresh();
            }

            // 3. FONT THÂN LƯỚI (Dòng nội dung) — tách biệt header, không ghi đè bằng scaledFont.
            if (grid.DefaultCellStyle.Font == null || grid.DefaultCellStyle.Font.Size < AppInputFontSize)
            {
                grid.DefaultCellStyle.Font = AppGridBodyFont;
            }
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            var minRow = GridHasComboBoxColumn(grid) ? AppComboGridRowHeight : AppDefaultRowHeight;
            grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, minRow);
            grid.RowTemplate.MinimumHeight = Math.Max(grid.RowTemplate.MinimumHeight, AppDefaultRowHeight);

            // 4. GÀI BẪY CHỐNG RESET
            grid.ColumnAdded -= Grid_ColumnAdded_ForceTheme;
            grid.ColumnAdded += Grid_ColumnAdded_ForceTheme;
            grid.DataBindingComplete -= Grid_DataBindingComplete_ForceTheme;
            grid.DataBindingComplete += Grid_DataBindingComplete_ForceTheme;
            grid.ColumnHeadersDefaultCellStyle.Font = GetScaledHeaderFont(grid);
            grid.Invalidate();
            grid.Refresh();
        }
        private static void Grid_ColumnAdded_ForceTheme(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column != null && sender is DataGridView grid)
            {
                if (!AppGridSttColumn.IsSttColumn(e.Column))
                {
                    AppGridSttColumn.EnsureFirstColumn(grid);
                }

                if (e.Column.HeaderCell.Style == null)
                {
                    e.Column.HeaderCell.Style = new DataGridViewCellStyle();
                }

                // Lấy Font động
                var scaledFont = GetScaledHeaderFont(grid);
                e.Column.HeaderCell.Style.Font = scaledFont;
                e.Column.HeaderCell.Style.Padding = new Padding(6, 8, 6, 8);
                e.Column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                if (AppGridSttColumn.IsSttColumn(e.Column))
                {
                    AppGridSttColumn.ApplyColumnWidth(
                        grid,
                        e.Column,
                        grid.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.Fill,
                        scaledFont,
                        e.Column.HeaderCell.Style.Padding);
                    return;
                }

                // Tự động nới rộng cột đo theo font mới
                var headerText = (e.Column.HeaderText ?? string.Empty).Trim();
                if (headerText.Length > 0)
                {
                    var textWidth = TextRenderer.MeasureText(
                        headerText,
                        scaledFont,
                        new Size(int.MaxValue, AppGridHeaderHeight),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width;

                    e.Column.MinimumWidth = Math.Max(AppGridColumnMinWidth, textWidth + AppGridHeaderColumnPad + 12);
                }
            }
        }
        private static void Grid_DataBindingComplete_ForceTheme(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (sender is DataGridView grid)
            {
                // Khi bảng vừa nạp dữ liệu xong và lén reset font -> Ép nó sơn lại font to ngay lập tức!
                ApplyAppGridHeaderChrome(grid);
                AppGridSttColumn.EnsureFirstColumn(grid);
                ApplyAppGridColumnHeaderWidths(grid);
            }
        }

        /// <summary>Clone style lưới; null → style trống (tránh mutate object ambient của DGV).</summary>
        private static DataGridViewCellStyle CloneGridCellStyle(DataGridViewCellStyle source)
        {
            return source == null ? new DataGridViewCellStyle() : source.Clone();
        }

        /// <summary>Ép chiều cao dòng hiện có ≥ max(AppDefaultRowHeight, RowTemplate.Height).</summary>
        internal static void EnsureAppGridRowHeights(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            var target = Math.Max(AppDefaultRowHeight, grid.RowTemplate.Height);

            if (grid.RowTemplate.Height < target)
            {
                grid.RowTemplate.Height = target;
            }

            if (grid.RowTemplate.MinimumHeight < AppDefaultRowHeight)
            {
                grid.RowTemplate.MinimumHeight = AppDefaultRowHeight;
            }

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row == null || row.IsNewRow)
                {
                    continue;
                }

                if (row.Height < target)
                {
                    row.Height = target;
                }

                if (row.MinimumHeight < AppDefaultRowHeight)
                {
                    row.MinimumHeight = AppDefaultRowHeight;
                }
            }
        }

        private void EnableUiFlickerReduction()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            UiPaintHelper.EnableDoubleBuffer(tabMain);
            UiPaintHelper.EnableDoubleBuffer(pnlMainWorkspace);
        }

        private void BeginUiLayoutBatch()
        {
            SuspendLayout();
            tabMain?.SuspendLayout();
            pnlMainWorkspace?.SuspendLayout();
        }

        private void EndUiLayoutBatch()
        {
            pnlMainWorkspace?.ResumeLayout(true);
            tabMain?.ResumeLayout(true);
            ResumeLayout(true);
        }
    }

    internal static class UiPaintHelper
    {
        public static void EnableDoubleBuffer(Control control)
        {
            if (control == null)
            {
                return;
            }

            typeof(Control).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                control,
                new object[] { true });
        }
    }
}
