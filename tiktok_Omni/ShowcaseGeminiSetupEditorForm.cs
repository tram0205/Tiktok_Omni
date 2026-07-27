using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Bảng 2 cột: Loại SP + Chủ đề (thay cho 2 dialog / menu riêng).</summary>
    internal sealed class ShowcaseGeminiSetupEditorForm : Form
    {
        private static readonly Color Bg = ShowcasePastelTheme.ShellBg;
        private static readonly Color AccentProduct = ShowcasePastelTheme.ProductHeader;
        private static readonly Color AccentTheme = ShowcasePastelTheme.ThemeHeader;
        private static readonly Color ProductPanelBg = ShowcasePastelTheme.ProductFrame;
        private static readonly Color ThemePanelBg = ShowcasePastelTheme.ThemeFrame;

        private readonly ShowcaseVideoItem _video;
        private ListBox _lstProductType;
        private ListBox _lstTheme;
        private TextBox _txtProductHint;
        private TextBox _txtThemeHint;
        private TextBox _txtCustomTheme;
        private TableLayoutPanel _productColumnLayout;
        private TableLayoutPanel _themeColumnLayout;

        private const int ListRowHeight = 48;
        private const float ListFontSize = 11.5F;
        private const int HeaderRowHeight = 104;
        private const int HintMinHeight = 44;
        private const int HintTextVerticalPadding = 8;
        /// <summary>Ít hơn 6 dòng: co theo nội dung, không cuộn. Từ 6 dòng: cố định 5 dòng + cuộn.</summary>
        private const int HintLinesBeforeScroll = 6;
        private const int HintVisibleLinesWhenScrolling = 5;

        private const int EmGetLineCount = 0x00BA;
        private const int CustomCaptionRowHeight = 34;
        private const int CustomThemeRowHeight = 140;
        private const int ButtonBarHeight = 108;

        public ShowcaseGeminiSetupEditorForm(ShowcaseVideoItem video)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Loại SP · Chủ đề" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Bg;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(1600, 900);
            ClientSize = new Size(2130, 1100);

            BuildUi();
            LoadFromVideo();
            Shown += (_, __) =>
            {
                FitPresetListRowHeights();
                RefitHintRows();
            };
            Resize += (_, __) => RefitHintRows();
        }

        private void BuildUi()
        {
            var shell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(42, 36, 42, 30)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonBarHeight));

            root.Controls.Add(MakeHeader("Loại SP", "Trang phục / ngành hàng → Gemini", AccentProduct), 0, 0);
            root.Controls.Add(MakeHeader("Chủ đề", "Preset + tùy chỉnh → Gemini", AccentTheme), 1, 0);

            root.Controls.Add(BuildProductColumn(), 0, 1);
            root.Controls.Add(BuildThemeColumn(), 1, 1);

            var btnOk = MakeButton("Lưu", ShowcasePastelTheme.ButtonSave);
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = MakeButton("Hủy", ShowcasePastelTheme.ButtonCancel);
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Bg,
                Padding = new Padding(0, 10, 0, 0)
            };
            flp.Controls.Add(btnCancel);
            flp.Controls.Add(btnOk);

            root.Controls.Add(flp, 0, 2);
            root.SetColumnSpan(flp, 2);

            shell.Controls.Add(root);
            Controls.Add(shell);

            btnOk.Click += (_, __) =>
            {
                if (!SaveToVideo())
                {
                    DialogResult = DialogResult.None;
                }
            };
        }

        private Control BuildProductColumn()
        {
            _productColumnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = ProductPanelBg,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 6, 8, 8)
            };

            _lstProductType = MakeListBox(ShowcasePastelTheme.ProductListSelection);
            foreach (var p in ShowcaseProductTypePresets.All)
            {
                _lstProductType.Items.Add(p);
            }

            var listHeight = ComputePresetListHeight(_lstProductType);
            _productColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, listHeight));
            _productColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, HintMinHeight));
            _productColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lstProductType.DisplayMember = nameof(ShowcaseProductTypePreset.DisplayLabel);
            _lstProductType.SelectedIndexChanged += (_, __) =>
            {
                UpdateProductHint();
                RefitHintRows();
            };

            _txtProductHint = MakeHintBox(ProductPanelBg);

            _productColumnLayout.Controls.Add(_lstProductType, 0, 0);
            _productColumnLayout.Controls.Add(_txtProductHint, 0, 1);
            _productColumnLayout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = ProductPanelBg }, 0, 2);

            return _productColumnLayout;
        }

        private Control BuildThemeColumn()
        {
            _themeColumnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = ThemePanelBg,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 6, 0, 8)
            };

            _lstTheme = MakeListBox(ShowcasePastelTheme.ThemeListSelection);
            foreach (var t in ShowcaseThemePresets.All)
            {
                _lstTheme.Items.Add(t);
            }

            var listHeight = ComputePresetListHeight(_lstTheme);
            _themeColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, listHeight));
            _themeColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, HintMinHeight));
            _themeColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, CustomCaptionRowHeight));
            _themeColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lstTheme.DisplayMember = nameof(ShowcaseThemePreset.DisplayLabel);
            _lstTheme.SelectedIndexChanged += (_, __) =>
            {
                UpdateThemeHint();
                RefitHintRows();
            };

            _txtThemeHint = MakeHintBox(ThemePanelBg);

            var lblCustom = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chủ đề tùy chỉnh (ưu tiên hơn preset)",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ShowcasePastelTheme.TextMuted,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                UseCompatibleTextRendering = true
            };

            _txtCustomTheme = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = ShowcasePastelTheme.FieldBg,
                ForeColor = ShowcasePastelTheme.TextBody,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11F),
                WordWrap = true
            };

            _themeColumnLayout.Controls.Add(_lstTheme, 0, 0);
            _themeColumnLayout.Controls.Add(_txtThemeHint, 0, 1);
            _themeColumnLayout.Controls.Add(lblCustom, 0, 2);
            _themeColumnLayout.Controls.Add(_txtCustomTheme, 0, 3);

            return _themeColumnLayout;
        }

        private void LoadFromVideo()
        {
            var hint = _video.ShowcaseProductTypePrompt ?? string.Empty;
            for (var i = 0; i < _lstProductType.Items.Count; i++)
            {
                if (_lstProductType.Items[i] is ShowcaseProductTypePreset p
                    && string.Equals(p.PromptHint ?? string.Empty, hint, StringComparison.Ordinal))
                {
                    _lstProductType.SelectedIndex = i;
                    break;
                }
            }

            if (_lstProductType.SelectedIndex < 0 && _lstProductType.Items.Count > 0)
            {
                _lstProductType.SelectedIndex = 0;
            }

            _txtCustomTheme.Text = (_video.ShowcaseUserTheme ?? string.Empty).Trim();
            var presetIndex = ShowcaseThemePresets.FindIndexByPrompt(_video.ShowcaseThemePrompt);
            if (string.IsNullOrWhiteSpace(_txtCustomTheme.Text)
                && presetIndex >= 0
                && presetIndex < _lstTheme.Items.Count)
            {
                _lstTheme.SelectedIndex = presetIndex;
            }
            else if (_lstTheme.Items.Count > 0 && _lstTheme.SelectedIndex < 0)
            {
                _lstTheme.SelectedIndex = 0;
            }

            UpdateProductHint();
            UpdateThemeHint();
        }

        private void UpdateProductHint()
        {
            if (_lstProductType.SelectedItem is ShowcaseProductTypePreset p)
            {
                var h = (p.PromptHint ?? string.Empty).Trim();
                _txtProductHint.Text = string.IsNullOrEmpty(h)
                    ? p.DisplayLabel + " — Gemini tự suy từ ảnh."
                    : h;
            }
        }

        private void UpdateThemeHint()
        {
            if (_lstTheme.SelectedItem is ShowcaseThemePreset t)
            {
                var h = (t.PromptHint ?? string.Empty).Trim();
                _txtThemeHint.Text = string.IsNullOrEmpty(h)
                    ? t.DisplayLabel + " — Gemini tự suy chủ đề."
                    : h;
            }
        }

        private bool SaveToVideo()
        {
            if (_lstProductType.SelectedItem is ShowcaseProductTypePreset productPreset)
            {
                _video.ShowcaseProductTypePrompt = productPreset.PromptHint ?? string.Empty;
            }

            var custom = _txtCustomTheme.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(custom))
            {
                ShowcaseThemePresets.ApplyCombinedInput(_video, custom);
            }
            else if (_lstTheme.SelectedItem is ShowcaseThemePreset themePreset)
            {
                ShowcaseThemePresets.ApplyCombinedInput(_video, themePreset.DisplayLabel);
            }
            else
            {
                ShowcaseThemePresets.ApplyCombinedInput(_video, string.Empty);
            }

            return true;
        }

        private void RefitHintRows()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            RefitHintRow(_productColumnLayout, _txtProductHint, 1);
            RefitHintRow(_themeColumnLayout, _txtThemeHint, 1);
        }

        private void FitPresetListRowHeights()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            SetPresetListRowHeight(_productColumnLayout, _lstProductType, 0);
            SetPresetListRowHeight(_themeColumnLayout, _lstTheme, 0);
        }

        private static void SetPresetListRowHeight(TableLayoutPanel col, ListBox list, int rowIndex)
        {
            if (col == null || list == null || col.IsDisposed || list.IsDisposed)
            {
                return;
            }

            if (rowIndex < 0 || rowIndex >= col.RowStyles.Count)
            {
                return;
            }

            var height = ComputePresetListHeight(list);
            col.RowStyles[rowIndex].SizeType = SizeType.Absolute;
            col.RowStyles[rowIndex].Height = height;
        }

        private static int ComputePresetListHeight(ListBox list)
        {
            var count = list?.Items.Count ?? 0;
            if (count <= 0)
            {
                return ListRowHeight + 4;
            }

            return count * list.ItemHeight + 8;
        }

        private static void RefitHintRow(TableLayoutPanel col, TextBox hint, int rowIndex)
        {
            if (col == null || hint == null || col.IsDisposed || hint.IsDisposed || col.Width <= 0)
            {
                return;
            }

            var width = col.ClientSize.Width - col.Padding.Horizontal - 8;
            if (width < 80)
            {
                return;
            }

            var layout = MeasureHintTextLayout(hint.Text, hint.Font, width);
            var lineCount = layout.LineCount;
            var lineHeight = layout.LineHeight;
            var needsScroll = lineCount >= HintLinesBeforeScroll;

            int rowHeight;
            if (lineCount <= 0)
            {
                rowHeight = HintMinHeight;
                hint.ScrollBars = ScrollBars.None;
            }
            else if (needsScroll)
            {
                rowHeight = HintVisibleLinesWhenScrolling * lineHeight + HintTextVerticalPadding;
                hint.ScrollBars = ScrollBars.Vertical;
            }
            else
            {
                rowHeight = lineCount * lineHeight + HintTextVerticalPadding;
                hint.ScrollBars = ScrollBars.None;
            }

            col.RowStyles[rowIndex].SizeType = SizeType.Absolute;
            col.RowStyles[rowIndex].Height = Math.Max(HintMinHeight, rowHeight);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private readonly struct HintTextLayout
        {
            public HintTextLayout(int lineCount, int lineHeight)
            {
                LineCount = lineCount;
                LineHeight = lineHeight;
            }

            public int LineCount { get; }
            public int LineHeight { get; }
        }

        private static HintTextLayout MeasureHintTextLayout(string text, Font font, int width)
        {
            var content = text ?? string.Empty;
            if (content.Length == 0)
            {
                return new HintTextLayout(0, MeasureHintLineHeight(font, width));
            }

            var measureWidth = Math.Max(80, width);
            var lineCount = MeasureTextBoxLineCount(content, font, measureWidth);
            var lineHeight = MeasureTextBoxLineHeight(content, font, measureWidth, lineCount);

            if (lineCount >= HintLinesBeforeScroll)
            {
                var scrollWidth = measureWidth - SystemInformation.VerticalScrollBarWidth;
                if (scrollWidth >= 80)
                {
                    var withScroll = MeasureTextBoxLineCount(content, font, scrollWidth);
                    if (withScroll > lineCount)
                    {
                        lineCount = withScroll;
                        lineHeight = MeasureTextBoxLineHeight(content, font, scrollWidth, lineCount);
                    }
                }
            }

            return new HintTextLayout(lineCount, lineHeight);
        }

        private static int MeasureTextBoxLineCount(string text, Font font, int width)
        {
            using (var probe = CreateHintMeasureBox(font, width))
            {
                probe.Text = text;
                probe.CreateControl();
                return Math.Max(0, SendMessage(probe.Handle, EmGetLineCount, 0, 0));
            }
        }

        private static int MeasureTextBoxLineHeight(string text, Font font, int width, int lineCount)
        {
            if (lineCount <= 1)
            {
                return MeasureHintLineHeight(font, width);
            }

            using (var probe = CreateHintMeasureBox(font, width))
            {
                probe.Text = text;
                probe.CreateControl();

                var idx = probe.GetFirstCharIndexFromLine(1);
                if (idx < 0)
                {
                    return MeasureHintLineHeight(font, width);
                }

                var y0 = probe.GetPositionFromCharIndex(0).Y;
                var y1 = probe.GetPositionFromCharIndex(idx).Y;
                var delta = y1 - y0;
                return delta > 0 ? delta : MeasureHintLineHeight(font, width);
            }
        }

        private static TextBox CreateHintMeasureBox(Font font, int width) => new TextBox
        {
            Multiline = true,
            WordWrap = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.None,
            Font = font,
            Width = width
        };

        private static int MeasureHintLineHeight(Font font, int width)
        {
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl;
            return TextRenderer.MeasureText("Áy", font, new Size(Math.Max(40, width), int.MaxValue), flags).Height;
        }

        private ListBox MakeListBox(Color selectionBackground)
        {
            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = ShowcasePastelTheme.ListBg,
                ForeColor = ShowcasePastelTheme.TextBody,
                BorderStyle = BorderStyle.None,
                IntegralHeight = true,
                ItemHeight = ListRowHeight,
                Font = new Font("Segoe UI", ListFontSize),
                HorizontalScrollbar = false,
                DrawMode = DrawMode.OwnerDrawFixed
            };

            list.DrawItem += (_, e) => DrawPresetListItem(list, e, selectionBackground);
            return list;
        }

        private static void DrawPresetListItem(ListBox list, DrawItemEventArgs e, Color selectionBackground)
        {
            if (e.Index < 0)
            {
                return;
            }

            var selected = (e.State & DrawItemState.Selected) != 0;
            var back = selected ? selectionBackground : list.BackColor;
            var text = list.Items[e.Index]?.ToString() ?? string.Empty;

            using (var brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            var textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(
                e.Graphics,
                text,
                list.Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                ShowcasePastelTheme.TextPrimary,
                textFlags);

            if (selected)
            {
                e.DrawFocusRectangle();
            }
        }

        private static Panel MakeHeader(string title, string subtitle, Color accent)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = accent,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(16, 10, 12, 10)
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = accent
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            inner.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = ShowcasePastelTheme.TextPrimary,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 2, 0, 0)
            }, 0, 0);

            inner.Controls.Add(new Label
            {
                Text = subtitle,
                Dock = DockStyle.Fill,
                ForeColor = ShowcasePastelTheme.TextSubheader,
                Font = new Font("Segoe UI", 10.5F),
                TextAlign = ContentAlignment.TopLeft,
                UseCompatibleTextRendering = true
            }, 0, 1);

            p.Controls.Add(inner);
            return p;
        }

        private static TextBox MakeHintBox(Color back) => new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.None,
            WordWrap = true,
            BorderStyle = BorderStyle.None,
            BackColor = back,
            ForeColor = ShowcasePastelTheme.HintText,
            Font = new Font("Segoe UI", 10.5F),
            TabStop = false
        };

        private static Button MakeButton(string text, Color back) => new Button
        {
            Text = text,
            MinimumSize = new Size(148, 48),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(8, 0, 0, 0)
        };
    }
}
