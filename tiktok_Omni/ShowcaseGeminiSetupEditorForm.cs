using System;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Bước 1: Kiểu video · Bước 2: Loại SP + Chủ đề (chủ đề lọc theo kiểu video).</summary>
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
        private TableLayoutPanel _productColumnLayout;
        private TableLayoutPanel _themeColumnLayout;
        private Panel[] _formatCards;
        private Label _lblThemeHeaderSubtitle;
        private string _selectedFormatId = ShowcaseVideoFormatPresets.DefaultId;

        private const int VideoFormatStepHeight = 208;
        private const int StepCaptionRowHeight = 36;

        private const int ListRowHeight = 48;
        private const float ListFontSize = 11.5F;
        private const int HeaderRowHeight = 104;

        private const int ButtonBarHeight = 108;

        private const float DialogHeightScale = 1.3f;
        private const int DefaultClientWidth = 2130;
        private const int DefaultClientHeight = 1100;
        private const int DefaultMinClientHeight = 900;

        public ShowcaseGeminiSetupEditorForm(ShowcaseVideoItem video)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Kiểu video · Loại SP · Chủ đề" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Bg;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(1600, ScaledDialogHeight(DefaultMinClientHeight));
            ClientSize = new Size(DefaultClientWidth, ScaledDialogHeight(DefaultClientHeight));

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
                RowCount = 5,
                BackColor = Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, VideoFormatStepHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, StepCaptionRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonBarHeight));

            var videoFormatStep = BuildVideoFormatStep();
            root.Controls.Add(videoFormatStep, 0, 0);
            root.SetColumnSpan(videoFormatStep, 2);

            var lblStep2 = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bước 2 — Loại SP · Chủ đề",
                ForeColor = ShowcasePastelTheme.TextMuted,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(4, 0, 0, 4),
                UseCompatibleTextRendering = true
            };
            root.Controls.Add(lblStep2, 0, 1);
            root.SetColumnSpan(lblStep2, 2);

            var themeHeader = MakeHeader("Chủ đề", ShowcaseThemePresets.ThemeSubtitleForFormat(_selectedFormatId), AccentTheme);
            _lblThemeHeaderSubtitle = FindSubtitleLabel(themeHeader);
            root.Controls.Add(MakeHeader("Loại SP", "Trang phục / ngành hàng → Gemini", AccentProduct), 0, 2);
            root.Controls.Add(themeHeader, 1, 2);

            root.Controls.Add(BuildProductColumn(), 0, 3);
            root.Controls.Add(BuildThemeColumn(), 1, 3);

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

            root.Controls.Add(flp, 0, 4);
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
                RowCount = 2,
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

            return _productColumnLayout;
        }

        private Control BuildThemeColumn()
        {
            _themeColumnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = ThemePanelBg,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 6, 0, 8)
            };

            _lstTheme = MakeListBox(ShowcasePastelTheme.ThemeListSelection);

            var listHeight = ComputePresetListHeight(_lstTheme);
            _themeColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, listHeight));
            _themeColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lstTheme.DisplayMember = nameof(ShowcaseThemePreset.DisplayLabel);
            _lstTheme.SelectedIndexChanged += (_, __) =>
            {
                UpdateThemeHint();
                RefitHintRows();
            };

            _txtThemeHint = MakeHintBox(ThemePanelBg);

            _themeColumnLayout.Controls.Add(_lstTheme, 0, 0);
            _themeColumnLayout.Controls.Add(_txtThemeHint, 0, 1);

            return _themeColumnLayout;
        }

        private Control BuildVideoFormatStep()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Bg,
                Margin = new Padding(0, 0, 0, 8)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            shell.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bước 1 — Kiểu video",
                ForeColor = ShowcasePastelTheme.TextMuted,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(4, 0, 0, 2),
                UseCompatibleTextRendering = true
            }, 0, 0);

            var cardsHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = ShowcaseVideoFormatPresets.All.Count,
                RowCount = 1,
                BackColor = Bg,
                Padding = new Padding(0, 8, 0, 4)
            };
            for (var i = 0; i < ShowcaseVideoFormatPresets.All.Count; i++)
            {
                cardsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            _formatCards = new Panel[ShowcaseVideoFormatPresets.All.Count];
            for (var i = 0; i < ShowcaseVideoFormatPresets.All.Count; i++)
            {
                var preset = ShowcaseVideoFormatPresets.All[i];
                var card = BuildFormatCard(preset);
                _formatCards[i] = card;
                cardsHost.Controls.Add(card, i, 0);
            }

            shell.Controls.Add(cardsHost, 0, 1);
            return shell;
        }

        private Panel BuildFormatCard(ShowcaseVideoFormatPreset preset)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ShowcasePastelTheme.FormatFrame,
                Margin = new Padding(6, 4, 6, 4),
                Padding = new Padding(0),
                Cursor = Cursors.Hand,
                Tag = preset
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = ShowcasePastelTheme.FormatFrame,
                Padding = new Padding(14, 14, 14, 12)
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = preset.DisplayLabel,
                ForeColor = ShowcasePastelTheme.TextPrimary,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 4, 0, 10),
                Margin = new Padding(0),
                UseCompatibleTextRendering = true
            };

            var hint = new Label
            {
                Dock = DockStyle.Fill,
                Text = preset.Hint,
                ForeColor = ShowcasePastelTheme.HintText,
                Font = new Font("Segoe UI", 9.75F),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 2, 0, 0),
                Margin = new Padding(0),
                UseCompatibleTextRendering = true
            };

            inner.Controls.Add(title, 0, 0);
            inner.Controls.Add(hint, 0, 1);
            card.Controls.Add(inner);

            void OnCardActivate(object sender, EventArgs e)
            {
                SelectFormat(preset.Id);
            }

            card.Click += OnCardActivate;
            inner.Click += OnCardActivate;
            title.Click += OnCardActivate;
            hint.Click += OnCardActivate;

            return card;
        }

        private void SelectFormat(string formatId)
        {
            _selectedFormatId = ShowcaseVideoFormatPresets.ResolveId(formatId);
            UpdateFormatCardVisuals();

            if (_lblThemeHeaderSubtitle != null)
            {
                _lblThemeHeaderSubtitle.Text = ShowcaseThemePresets.ThemeSubtitleForFormat(_selectedFormatId);
            }

            ReloadThemeListForFormat(_selectedFormatId, preserveCustomTheme: true);
        }

        private void UpdateFormatCardVisuals()
        {
            if (_formatCards == null)
            {
                return;
            }

            foreach (var card in _formatCards)
            {
                if (!(card?.Tag is ShowcaseVideoFormatPreset preset))
                {
                    continue;
                }

                var selected = string.Equals(preset.Id, _selectedFormatId, StringComparison.Ordinal);
                ApplyFormatCardVisual(card, selected);
            }
        }

        private static void ApplyFormatCardVisual(Panel card, bool selected)
        {
            var back = selected ? ShowcasePastelTheme.FormatCap : ShowcasePastelTheme.FormatFrameInactive;
            card.BackColor = back;

            foreach (Control child in card.Controls)
            {
                if (child is TableLayoutPanel inner)
                {
                    inner.BackColor = back;
                    foreach (Control innerChild in inner.Controls)
                    {
                        if (innerChild is Label lbl)
                        {
                            var isTitle = inner.GetRow(innerChild) == 0;
                            lbl.ForeColor = selected
                                ? (isTitle ? ShowcasePastelTheme.TextPrimary : ShowcasePastelTheme.HintText)
                                : (isTitle ? ShowcasePastelTheme.FormatTitleInactive : ShowcasePastelTheme.FormatHintInactive);
                        }
                    }
                }
            }
        }

        private void ReloadThemeListForFormat(string formatId, bool preserveCustomTheme)
        {
            if (_lstTheme == null)
            {
                return;
            }

            var presets = ShowcaseThemePresets.ForFormat(formatId);
            var savedPrompt = (_video.ShowcaseThemePrompt ?? string.Empty).Trim();
            var hasCustom = preserveCustomTheme && !string.IsNullOrWhiteSpace(_video?.ShowcaseUserTheme);
            var promptToMatch = savedPrompt;
            if (!hasCustom && _lstTheme?.SelectedItem is ShowcaseThemePreset currentPreset)
            {
                var currentHint = (currentPreset.PromptHint ?? string.Empty).Trim();
                if (currentHint.Length > 0)
                {
                    promptToMatch = currentHint;
                }
            }

            _lstTheme.BeginUpdate();
            _lstTheme.Items.Clear();
            foreach (var t in presets)
            {
                _lstTheme.Items.Add(t);
            }
            _lstTheme.EndUpdate();

            if (_themeColumnLayout != null && _themeColumnLayout.RowStyles.Count > 0)
            {
                SetPresetListRowHeight(_themeColumnLayout, _lstTheme, 0);
            }

            if (hasCustom)
            {
                var customIndex = ShowcaseThemePresets.FindCustomIndexInList(presets);
                if (customIndex >= 0 && customIndex < _lstTheme.Items.Count)
                {
                    _lstTheme.SelectedIndex = customIndex;
                }
                else if (_lstTheme.Items.Count > 0)
                {
                    _lstTheme.ClearSelected();
                }

                SetThemeHintEditable(true);
                _txtThemeHint.Text = (_video.ShowcaseUserTheme ?? string.Empty).Trim();
            }
            else
            {
                var presetIndex = ShowcaseThemePresets.FindIndexByPromptInList(presets, promptToMatch);
                if (presetIndex >= 0 && presetIndex < _lstTheme.Items.Count)
                {
                    _lstTheme.SelectedIndex = presetIndex;
                }
                else if (_lstTheme.Items.Count > 0)
                {
                    _lstTheme.SelectedIndex = 0;
                }

                UpdateThemeHint();
            }

            RefitHintRows();
        }

        private static Label FindSubtitleLabel(Panel headerPanel)
        {
            if (headerPanel == null)
            {
                return null;
            }

            foreach (Control child in headerPanel.Controls)
            {
                if (child is TableLayoutPanel inner)
                {
                    foreach (Control innerChild in inner.Controls)
                    {
                        if (innerChild is Label lbl && inner.GetRow(innerChild) == 1)
                        {
                            return lbl;
                        }
                    }
                }
            }

            return null;
        }

        private void LoadFromVideo()
        {
            _selectedFormatId = ShowcaseVideoFormatPresets.ResolveId(_video.ShowcaseVideoFormatId);
            UpdateFormatCardVisuals();
            if (_lblThemeHeaderSubtitle != null)
            {
                _lblThemeHeaderSubtitle.Text = ShowcaseThemePresets.ThemeSubtitleForFormat(_selectedFormatId);
            }

            ReloadThemeListForFormat(_selectedFormatId, preserveCustomTheme: true);

            var productHint = (_video.ShowcaseProductTypePrompt ?? string.Empty).Trim();
            var productIndex = ShowcaseProductTypePresets.FindIndexByPromptHint(productHint);
            if (productIndex >= 0)
            {
                _lstProductType.SelectedIndex = productIndex;
            }
            else if (!string.IsNullOrEmpty(productHint))
            {
                SelectCustomProductType();
                _txtProductHint.Text = productHint;
            }
            else if (_lstProductType.Items.Count > 0)
            {
                _lstProductType.SelectedIndex = 0;
            }

            UpdateProductHint();
            if (_lstTheme.SelectedIndex < 0)
            {
                UpdateThemeHint();
            }
        }

        private void SelectCustomProductType()
        {
            for (var i = 0; i < _lstProductType.Items.Count; i++)
            {
                if (_lstProductType.Items[i] is ShowcaseProductTypePreset preset
                    && ShowcaseProductTypePresets.IsCustomPreset(preset))
                {
                    _lstProductType.SelectedIndex = i;
                    return;
                }
            }
        }

        private void UpdateProductHint()
        {
            if (_lstProductType.SelectedItem is ShowcaseProductTypePreset p
                && ShowcaseProductTypePresets.IsCustomPreset(p))
            {
                SetProductHintEditable(true);
                var saved = (_video.ShowcaseProductTypePrompt ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(saved) && ShowcaseProductTypePresets.FindIndexByPromptHint(saved) < 0)
                {
                    _txtProductHint.Text = saved;
                }
                else if (!_txtProductHint.Focused)
                {
                    _txtProductHint.Text = string.Empty;
                }

                return;
            }

            SetProductHintEditable(false);
            if (_lstProductType.SelectedItem is ShowcaseProductTypePreset preset)
            {
                var h = (preset.PromptHint ?? string.Empty).Trim();
                _txtProductHint.Text = string.IsNullOrEmpty(h)
                    ? preset.DisplayLabel + " — Gemini tự suy từ ảnh."
                    : h;
            }
        }

        private void UpdateThemeHint()
        {
            if (_lstTheme.SelectedItem is ShowcaseThemePreset t
                && ShowcaseThemePresets.IsCustomPreset(t))
            {
                SetThemeHintEditable(true);
                var saved = (_video.ShowcaseUserTheme ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(saved))
                {
                    _txtThemeHint.Text = saved;
                }
                else if (!_txtThemeHint.Focused)
                {
                    _txtThemeHint.Text = string.Empty;
                }

                return;
            }

            SetThemeHintEditable(false);
            if (_lstTheme.SelectedItem is ShowcaseThemePreset preset)
            {
                var h = (preset.PromptHint ?? string.Empty).Trim();
                _txtThemeHint.Text = string.IsNullOrEmpty(h)
                    ? preset.DisplayLabel + " — Gemini tự suy chủ đề."
                    : h;
            }
        }

        private void SetProductHintEditable(bool editable)
        {
            ApplyHintBoxEditableState(_txtProductHint, ProductPanelBg, editable);
        }

        private void SetThemeHintEditable(bool editable)
        {
            ApplyHintBoxEditableState(_txtThemeHint, ThemePanelBg, editable);
        }

        private static void ApplyHintBoxEditableState(TextBox box, Color readOnlyBack, bool editable)
        {
            if (box == null || box.IsDisposed)
            {
                return;
            }

            box.ReadOnly = !editable;
            box.TabStop = editable;
            box.BackColor = editable ? ShowcasePastelTheme.ListBg : readOnlyBack;
            box.ForeColor = editable ? ShowcasePastelTheme.TextPrimary : ShowcasePastelTheme.HintText;
        }

        private bool SaveToVideo()
        {
            if (_lstProductType.SelectedItem is ShowcaseProductTypePreset productPreset)
            {
                if (ShowcaseProductTypePresets.IsCustomPreset(productPreset))
                {
                    _video.ShowcaseProductTypePrompt = (_txtProductHint.Text ?? string.Empty).Trim();
                }
                else
                {
                    _video.ShowcaseProductTypePrompt = productPreset.PromptHint ?? string.Empty;
                }
            }

            if (_lstTheme.SelectedItem is ShowcaseThemePreset themePreset)
            {
                if (ShowcaseThemePresets.IsCustomPreset(themePreset))
                {
                    ShowcaseThemePresets.ApplyCombinedInput(_video, (_txtThemeHint.Text ?? string.Empty).Trim());
                }
                else
                {
                    ShowcaseThemePresets.ApplyCombinedInput(_video, themePreset.DisplayLabel);
                }
            }
            else
            {
                ShowcaseThemePresets.ApplyCombinedInput(_video, string.Empty);
            }

            _video.ShowcaseVideoFormatId = _selectedFormatId;

            return true;
        }

        private void RefitHintRows()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            RefitProductHintRow();
            RefitThemeHintRow();
        }

        private void RefitThemeHintRow()
        {
            if (_txtThemeHint == null || _txtThemeHint.IsDisposed)
            {
                return;
            }

            _txtThemeHint.ScrollBars = ScrollBars.None;
        }

        private void RefitProductHintRow()
        {
            if (_txtProductHint == null || _txtProductHint.IsDisposed)
            {
                return;
            }

            _txtProductHint.ScrollBars = ScrollBars.None;
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

        private static int ScaledDialogHeight(int baseClientHeight) =>
            (int)Math.Round(baseClientHeight * DialogHeightScale);

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
            var textRect = new Rectangle(
                e.Bounds.X + 6,
                e.Bounds.Y + 4,
                e.Bounds.Width - 12,
                e.Bounds.Height - 8);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                list.Font,
                textRect,
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
            BorderStyle = BorderStyle.FixedSingle,
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
