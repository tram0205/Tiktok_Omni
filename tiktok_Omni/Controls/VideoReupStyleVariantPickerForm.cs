using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni.Controls
{
    internal sealed class VideoReupStyleVariantPickerForm : Form
    {
        private readonly DataGridView _grid;
        private ComboBox _cboHookStyle;
        private ComboBox _cboScriptStyle;

        public Dictionary<string, string> HookByStyle { get; private set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ScriptByStyle { get; private set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string SelectedHookStyleKey { get; private set; } = string.Empty;
        public string SelectedScriptStyleKey { get; private set; } = string.Empty;

        public VideoReupStyleVariantPickerForm(
            string title,
            IReadOnlyDictionary<string, string> hooks,
            IReadOnlyDictionary<string, string> scripts,
            string selectedHookStyleKey,
            string selectedScriptStyleKey,
            bool focusScriptColumn = false,
            string rawProductName = null)
        {
            Text = title ?? "Chọn phong cách hook & script";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            Size = new Size(2040, 1320);
            MinimumSize = new Size(1640, 1040);
            BackColor = Color.FromArgb(24, 26, 34);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 12f);

            HookByStyle = VideoReupStyleVariants.CloneMap(hooks);
            ScriptByStyle = VideoReupStyleVariants.CloneMap(scripts);
            SelectedHookStyleKey = VideoReupStyleVariants.NormalizeStyleKey(selectedHookStyleKey);
            SelectedScriptStyleKey = VideoReupStyleVariants.NormalizeStyleKey(selectedScriptStyleKey);

            var shortLabel = VideoReupProductLabel.GetShortLabel(rawProductName ?? string.Empty);
            var header = BuildHeaderPanel(shortLabel);

            var lblHint = new Label
            {
                Text =
                    "✦ Gemini đã sinh 5 phong cách. Chọn hook/script để render — sửa trực tiếp trên bảng. "
                    + "Hook dùng tên sản phẩm ngắn, không gồm shop gốc.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(155, 162, 178),
                Font = new Font("Segoe UI", 11.5f),
                Margin = new Padding(0, 0, 0, 12),
                Height = 52
            };

            _grid = BuildGrid();
            PopulateGrid();
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.RowPrePaint += Grid_RowPrePaint;

            var selectors = BuildSelectorsPanel();

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = false
            };

            var btnCancel = MakeJellyButton("Hủy", Color.FromArgb(72, 78, 92), DialogResult.Cancel);
            var btnOk = MakeJellyButton("Lưu & áp dụng", Color.FromArgb(56, 120, 210), DialogResult.OK);
            btnOk.Click += (_, __) =>
            {
                if (!TryCommitFromGrid(out var err))
                {
                    MessageBox.Show(this, err, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }

                SelectedHookStyleKey = ReadHookStyleSelection();
                SelectedScriptStyleKey = ReadScriptStyleSelection();
            };

            bottom.Controls.Add(btnCancel);
            bottom.Controls.Add(btnOk);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(20, 16, 20, 16),
                BackColor = Color.FromArgb(24, 26, 34)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(lblHint, 0, 1);
            layout.Controls.Add(_grid, 0, 2);
            layout.Controls.Add(selectors, 0, 3);
            layout.Controls.Add(bottom, 0, 4);
            Controls.Add(layout);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Shown += (_, __) =>
            {
                var wa = Screen.FromControl(this).WorkingArea;
                if (Width > wa.Width - 24 || Height > wa.Height - 24)
                {
                    Width = Math.Min(Width, wa.Width - 24);
                    Height = Math.Min(Height, wa.Height - 24);
                }

                HighlightSelectedRows();
                if (focusScriptColumn && _grid.Columns.Count > 2)
                {
                    _grid.CurrentCell = _grid.Rows[0].Cells[2];
                }
                else if (_grid.Columns.Count > 1)
                {
                    _grid.CurrentCell = _grid.Rows[0].Cells[1];
                }

                _grid.Focus();
            };
        }

        private static Panel BuildHeaderPanel(string shortProductLabel)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 96,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(32, 36, 48),
                Padding = new Padding(20, 14, 20, 14)
            };
            panel.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(56, 120, 210), 2f))
                {
                    e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
                }
            };

            panel.Controls.Add(new Label
            {
                Text = "✨ 5 phong cách hook & script",
                AutoSize = true,
                ForeColor = Color.FromArgb(235, 238, 245),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(6, 4)
            });
            panel.Controls.Add(new Label
            {
                Text = "Sản phẩm (reup):  " + shortProductLabel,
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 200, 255),
                Font = new Font("Segoe UI", 12f, FontStyle.Italic),
                Location = new Point(8, 44)
            });
            return panel;
        }

        private FlowLayoutPanel BuildSelectorsPanel()
        {
            var selectors = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.FromArgb(28, 30, 38)
            };

            selectors.Controls.Add(MakeSelectorLabel("🎯 Hook render:"));
            _cboHookStyle = BuildStyleCombo(includeRandom: true);
            _cboHookStyle.SelectedIndexChanged += (_, __) => HighlightSelectedRows();
            selectors.Controls.Add(_cboHookStyle);

            selectors.Controls.Add(MakeSelectorLabel("   📝 Script render:"));
            _cboScriptStyle = BuildStyleCombo(includeRandom: false);
            _cboScriptStyle.SelectedIndexChanged += (_, __) => HighlightSelectedRows();
            selectors.Controls.Add(_cboScriptStyle);

            SelectComboValue(_cboHookStyle, SelectedHookStyleKey, randomLabel: VideoReupStyleVariants.RandomHookStyleLabel);
            SelectComboValue(_cboScriptStyle, SelectedScriptStyleKey, randomLabel: VideoReupStyleVariants.FollowHookScriptStyleLabel);
            return selectors;
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(18, 20, 26),
                GridColor = Color.FromArgb(48, 52, 64),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeight = 76,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowTemplate = { MinimumHeight = 88 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(18, 20, 26),
                    ForeColor = Color.FromArgb(220, 224, 232),
                    SelectionBackColor = Color.FromArgb(56, 110, 165),
                    SelectionForeColor = Color.White,
                    WrapMode = DataGridViewTriState.True,
                    Font = new Font("Segoe UI", 12f),
                    Padding = new Padding(10, 8, 10, 8)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(36, 40, 52),
                    ForeColor = Color.FromArgb(210, 214, 224),
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 6, 0)
                },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                grid,
                new object[] { true });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStyle",
                HeaderText = "  Phong cách",
                ReadOnly = true,
                Width = 260,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colHook",
                HeaderText = "  Hook (4–7s)",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 45f,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colScript",
                HeaderText = "  Script thuyết minh",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 55f,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStyleKey",
                Visible = false,
                ReadOnly = true
            });

            AppGridSttColumn.EnsureFirstColumn(grid);

            return grid;
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                HookByStyle.TryGetValue(key, out var hook);
                ScriptByStyle.TryGetValue(key, out var script);
                _grid.Rows.Add(
                    HookStyleCatalog.GetStyleIcon(key) + "  " + HookStyleCatalog.GetDisplayName(key),
                    hook ?? string.Empty,
                    script ?? string.Empty,
                    key);
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var key = _grid.Rows[e.RowIndex].Cells["colStyleKey"].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "colStyle")
            {
                e.CellStyle.ForeColor = HookStyleCatalog.GetStyleAccent(key);
                e.CellStyle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            }
            else if (_grid.Columns[e.ColumnIndex].Name == "colHook")
            {
                e.CellStyle.ForeColor = Color.FromArgb(130, 175, 255);
            }
            else if (_grid.Columns[e.ColumnIndex].Name == "colScript")
            {
                e.CellStyle.ForeColor = Color.FromArgb(170, 210, 170);
            }
        }

        private void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var row = _grid.Rows[e.RowIndex];
            var key = row.Cells["colStyleKey"].Value?.ToString() ?? string.Empty;
            var hookKey = ReadHookStyleSelection();
            var scriptKey = ReadScriptStyleSelection();
            if (string.IsNullOrEmpty(scriptKey))
            {
                scriptKey = hookKey;
            }

            var selected = string.Equals(key, hookKey, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(key, scriptKey, StringComparison.OrdinalIgnoreCase);
            row.DefaultCellStyle.BackColor = HookStyleCatalog.GetStyleRowSurface(key, selected);
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(56, 110, 165);

            using (var brush = new SolidBrush(HookStyleCatalog.GetStyleAccent(key)))
            {
                var stripe = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, 4, e.RowBounds.Height - 1);
                e.Graphics.FillRectangle(brush, stripe);
            }
        }

        private ComboBox BuildStyleCombo(bool includeRandom)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280,
                BackColor = Color.FromArgb(22, 24, 30),
                ForeColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 6, 24, 6),
                Font = new Font("Segoe UI", 11.5f)
            };

            combo.Items.Add(includeRandom
                ? "🎲 " + VideoReupStyleVariants.RandomHookStyleLabel
                : "↪ " + VideoReupStyleVariants.FollowHookScriptStyleLabel);
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                combo.Items.Add(HookStyleCatalog.GetStyleIcon(key) + "  " + HookStyleCatalog.GetDisplayName(key));
            }

            combo.SelectedIndex = 0;
            return combo;
        }

        private static Label MakeSelectorLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(185, 192, 206),
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                Margin = new Padding(0, 14, 8, 0)
            };
        }

        private static JellyButton MakeJellyButton(string text, Color tint, DialogResult result)
        {
            return new JellyButton
            {
                Text = text,
                DialogResult = result,
                JellyTint = tint,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Width = Math.Max(140, TextRenderer.MeasureText(text, new Font("Segoe UI", 12f, FontStyle.Bold)).Width + 36),
                Height = 48,
                Margin = new Padding(12, 6, 0, 6)
            };
        }

        private static void SelectComboValue(ComboBox combo, string styleKey, string randomLabel)
        {
            if (string.IsNullOrEmpty(styleKey))
            {
                combo.SelectedIndex = 0;
                return;
            }

            var display = HookStyleCatalog.GetStyleIcon(styleKey) + "  " + HookStyleCatalog.GetDisplayName(styleKey);
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i]?.ToString(), display, StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private string ReadHookStyleSelection()
        {
            var label = _cboHookStyle.SelectedItem?.ToString() ?? string.Empty;
            if (label.Contains(VideoReupStyleVariants.RandomHookStyleLabel))
            {
                return string.Empty;
            }

            return ResolveStyleKeyFromDisplay(label);
        }

        private string ReadScriptStyleSelection()
        {
            var label = _cboScriptStyle.SelectedItem?.ToString() ?? string.Empty;
            if (label.Contains(VideoReupStyleVariants.FollowHookScriptStyleLabel))
            {
                return string.Empty;
            }

            return ResolveStyleKeyFromDisplay(label);
        }

        private static string ResolveStyleKeyFromDisplay(string display)
        {
            var text = (display ?? string.Empty).Trim();
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                var expected = HookStyleCatalog.GetStyleIcon(key) + "  " + HookStyleCatalog.GetDisplayName(key);
                if (string.Equals(expected, text, StringComparison.Ordinal))
                {
                    return key;
                }

                if (string.Equals(HookStyleCatalog.GetDisplayName(key), text, StringComparison.Ordinal))
                {
                    return key;
                }
            }

            return string.Empty;
        }

        private void HighlightSelectedRows()
        {
            _grid.Invalidate();
        }

        private bool TryCommitFromGrid(out string error)
        {
            error = string.Empty;
            var hooks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var key = row.Cells["colStyleKey"].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var hook = (row.Cells["colHook"].Value?.ToString() ?? string.Empty).Trim();
                var script = (row.Cells["colScript"].Value?.ToString() ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(hook))
                {
                    hooks[key] = hook;
                }

                if (!string.IsNullOrEmpty(script))
                {
                    scripts[key] = script;
                }
            }

            if (hooks.Count == 0)
            {
                error = "Cần ít nhất một câu hook.";
                return false;
            }

            HookByStyle = hooks;
            ScriptByStyle = scripts;
            return true;
        }
    }
}
