using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseBrandLogoEditorForm : Form
    {
        private const int DialogClientWidth = 1280;
        private const int SettingsColumnWidth = 760;
        private const int PreviewColumnWidth = PreviewFrameWidth + 32;
        private const int ColumnGap = 24;
        private const int PreviewFrameWidth = 300;
        private const int PreviewFrameHeight = 420;
        private const int LabelColumnWidth = 198;
        private const int OuterPaddingH = 18;
        private const int OuterPaddingTop = 18;
        private const int OuterPaddingBottom = 20;
        private const int FooterBarHeight = 76;
        private const int ControlHeight = 66;
        private const int ComboWidth = 560;
        private const float CompactFieldWidthRatio = 0.25f;
        private const int MinCompactFieldWidth = 108;
        private const int InlineButtonWidth = 188;
        private const int InlineIconButtonWidth = ControlHeight;

        private readonly ShowcaseVideoItem _video;
        private readonly AppSettings _settings;

        private CheckBox _chkEnabled;
        private TextBox _txtFile;
        private ComboBox _cbPosition;
        private NumericUpDown _numScale;
        private NumericUpDown _numMarginX;
        private NumericUpDown _numMarginY;
        private NumericUpDown _numOpacity;
        private Label _lblLibraryDir;
        private Label _lblLibraryHint;
        private Label _lblPreviewHint;
        private PictureBox _picPreview;
        private Button _btnRefreshPreview;
        private TableLayoutPanel _tbl;
        private TableLayoutPanel _root;
        private Control _previewPanel;

        public ShowcaseBrandLogoEditorForm(ShowcaseVideoItem video, AppSettings settings)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _settings = settings ?? new AppSettings();
            ShowcaseBrandOverlayHelper.EnsureVideoDefaults(_video);

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Logo thương hiệu" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScroll = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            Padding = new Padding(OuterPaddingH, OuterPaddingTop, OuterPaddingH, OuterPaddingBottom);
            BuildUi();
            LoadFromVideo();
            SyncDialogBounds();
        }

        private void BuildUi()
        {
            var btnBrowse = CreateInlineButton("Chọn…");
            btnBrowse.Click += (_, __) => BrowseLogoFile();

            var btnOpenLibrary = CreateFolderIconButton();
            btnOpenLibrary.Click += (_, __) => OpenLogoLibraryFolder();

            var btnOk = CreateFooterButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateFooterButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            _tbl = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 7,
                BackColor = BackColor,
                Dock = DockStyle.Top,
                Width = SettingsColumnWidth
            };
            _tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelColumnWidth));
            _tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
            _tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            _tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            _tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            _tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            _tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));

            Label MkLbl(string text, ContentAlignment align = ContentAlignment.MiddleLeft) => new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = align,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0),
                Font = new Font("Segoe UI", 10.5F)
            };

            _chkEnabled = new CheckBox
            {
                Text = "Chèn logo lên video",
                AutoSize = true,
                MaximumSize = new Size(720, 0),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5F)
            };
            _chkEnabled.CheckedChanged += (_, __) => UpdatePreview();

            _lblLibraryDir = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 6, 12, 4)
            };
            _lblLibraryHint = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 12, 0)
            };
            _lblPreviewHint = new Label
            {
                AutoSize = false,
                Height = 44,
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 0, 8),
                Text = "Khung 9:16 · cập nhật theo thiết lập hiện tại"
            };
            _picPreview = new PictureBox
            {
                BackColor = Color.FromArgb(18, 24, 38),
                BorderStyle = BorderStyle.FixedSingle,
                Height = PreviewFrameHeight,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = PreviewFrameWidth
            };
            _btnRefreshPreview = CreateInlineButton("Xem thử");
            _btnRefreshPreview.Width = PreviewFrameWidth;
            _btnRefreshPreview.Click += (_, __) => UpdateVisualPreview();

            _txtFile = new TextBox
            {
                Height = ControlHeight,
                MinimumSize = new Size(240, ControlHeight),
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(0, 0, 12, 0)
            };

            _cbPosition = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ComboWidth,
                Height = ControlHeight,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            _cbPosition.Items.AddRange(new object[]
            {
                new PositionItem(ShowcaseBrandLogoPositionCatalog.BottomRight, "Phải dưới"),
                new PositionItem(ShowcaseBrandLogoPositionCatalog.BottomLeft, "Trái dưới"),
                new PositionItem(ShowcaseBrandLogoPositionCatalog.TopRight, "Phải trên"),
                new PositionItem(ShowcaseBrandLogoPositionCatalog.TopLeft, "Trái trên"),
                new PositionItem(ShowcaseBrandLogoPositionCatalog.Center, "Giữa màn hình")
            });
            _cbPosition.SelectedIndexChanged += (_, __) => UpdatePreview();

            _numScale = CreatePercentNumeric(4, 40, ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent);
            _numMarginX = CreatePercentNumeric(0, 160, ShowcaseBrandOverlayHelper.DefaultMargin);
            _numMarginY = CreatePercentNumeric(0, 160, ShowcaseBrandOverlayHelper.DefaultMargin);
            _numOpacity = CreatePercentNumeric(20, 100, ShowcaseBrandOverlayHelper.DefaultOpacityPercent);
            _numScale.ValueChanged += (_, __) => UpdatePreview();
            _numMarginX.ValueChanged += (_, __) => UpdatePreview();
            _numMarginY.ValueChanged += (_, __) => UpdatePreview();
            _numOpacity.ValueChanged += (_, __) => UpdatePreview();

            _tbl.Controls.Add(MkLbl("Bật logo"), 0, 0);
            _tbl.Controls.Add(CreateFieldHost(_chkEnabled), 1, 0);
            _tbl.Controls.Add(MkLbl("Thư mục logo", ContentAlignment.TopLeft), 0, 1);
            _tbl.Controls.Add(CreateFieldHost(CreateInlineRow(CreateLibraryInfoStack(), btnOpenLibrary, centerActionWithPrimary: true, actionWidth: InlineIconButtonWidth), topAlign: true), 1, 1);
            _tbl.Controls.Add(MkLbl("File logo"), 0, 2);
            _tbl.Controls.Add(CreateFieldHost(CreateInlineRow(_txtFile, btnBrowse)), 1, 2);
            _tbl.Controls.Add(MkLbl("Vị trí"), 0, 3);
            _tbl.Controls.Add(CreateFieldHost(_cbPosition), 1, 3);
            _tbl.Controls.Add(MkLbl("Scale (% rộng)"), 0, 4);
            _tbl.Controls.Add(CreateFieldHost(_numScale), 1, 4);
            _tbl.Controls.Add(MkLbl("Lề X / Y (px)"), 0, 5);
            _tbl.Controls.Add(CreateFieldHost(CreateMarginRow()), 1, 5);
            _tbl.Controls.Add(MkLbl("Độ mờ (%)"), 0, 6);
            _tbl.Controls.Add(CreateFieldHost(CreateOpacityRow()), 1, 6);

            _root = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                RowCount = 1,
                BackColor = BackColor,
                Width = SettingsColumnWidth + ColumnGap + PreviewColumnWidth
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SettingsColumnWidth));
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PreviewColumnWidth));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _previewPanel = CreatePreviewPanel();
            _root.Controls.Add(_tbl, 0, 0);
            _root.Controls.Add(_previewPanel, 1, 0);

            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = FooterBarHeight,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 0)
            };
            var flpFooter = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor
            };
            flpFooter.Controls.Add(btnCancel);
            flpFooter.Controls.Add(btnOk);
            btnBar.Controls.Add(flpFooter);

            Controls.Add(_root);
            Controls.Add(btnBar);

            var tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };
            tips.SetToolTip(btnOpenLibrary, "Mở thư mục");

            Load += (_, __) => ApplyLayoutMetrics();
            FormClosed += (_, __) =>
            {
                var image = _picPreview.Image;
                _picPreview.Image = null;
                image?.Dispose();
            };
            btnOk.Click += (_, __) => SaveToVideo();
        }

        private Control CreatePreviewPanel()
        {
            var wrap = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor,
                Dock = DockStyle.Fill,
                Padding = new Padding(ColumnGap, 0, 0, 0),
                Width = PreviewColumnWidth
            };

            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 230, 245),
                Margin = new Padding(0, 0, 0, 6),
                Text = "Xem thử logo"
            };

            var stack = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = BackColor,
                Width = PreviewColumnWidth,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            stack.Controls.Add(title);
            stack.Controls.Add(_lblPreviewHint);
            _lblPreviewHint.Width = PreviewFrameWidth;
            stack.Controls.Add(_picPreview);
            stack.Controls.Add(_btnRefreshPreview);
            wrap.Controls.Add(stack);
            return wrap;
        }

        private void SyncDialogBounds()
        {
            var innerW = SettingsColumnWidth + ColumnGap + PreviewColumnWidth;
            _root.Width = innerW;
            var settingsHeight = _tbl.GetPreferredSize(new Size(SettingsColumnWidth, 0)).Height;
            var previewHeight = _previewPanel.GetPreferredSize(new Size(PreviewColumnWidth, 0)).Height;
            var contentHeight = Math.Max(settingsHeight, previewHeight);
            ClientSize = new Size(DialogClientWidth, Padding.Vertical + contentHeight + FooterBarHeight);
        }

        private void ApplyLayoutMetrics()
        {
            var fieldWidth = Math.Max(320, SettingsColumnWidth - LabelColumnWidth - 24);
            var compactWidth = GetCompactFieldWidth(SettingsColumnWidth);
            var fileWidth = Math.Max(280, fieldWidth - InlineButtonWidth - 16);

            _tbl.Width = SettingsColumnWidth;
            _lblLibraryDir.MaximumSize = new Size(Math.Max(240, fieldWidth - InlineIconButtonWidth - 16), 0);
            _txtFile.Width = fileWidth;
            _cbPosition.Width = Math.Min(ComboWidth, fieldWidth);
            ApplyCompactFieldWidths(compactWidth);
            UpdatePreview();
            SyncDialogBounds();
        }

        private int GetCompactFieldWidth(int contentWidth)
        {
            var available = Math.Max(MinCompactFieldWidth * 4, contentWidth - LabelColumnWidth - 24);
            return Math.Max(MinCompactFieldWidth, (int)Math.Round(available * CompactFieldWidthRatio));
        }

        private void ApplyCompactFieldWidths(int compactWidth)
        {
            _numScale.Width = compactWidth;
            _numMarginX.Width = compactWidth;
            _numMarginY.Width = compactWidth;
            _numOpacity.Width = compactWidth;
        }

        private static Control CreateInlineRow(Control primary, Button action, bool centerActionWithPrimary = false, int? actionWidth = null)
        {
            var width = actionWidth ?? InlineButtonWidth;
            action.Width = width;
            action.Height = ControlHeight;
            action.Margin = Padding.Empty;

            var row = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Width = SettingsColumnWidth - LabelColumnWidth - 24
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width + 12));
            primary.Margin = new Padding(0, 0, 12, 0);
            primary.Dock = DockStyle.Fill;
            action.Dock = DockStyle.Fill;
            row.RowStyles.Add(new RowStyle(centerActionWithPrimary ? SizeType.AutoSize : SizeType.Absolute, ControlHeight));

            if (centerActionWithPrimary)
            {
                row.Controls.Add(primary, 0, 0);

                var actionHost = new Panel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = row.BackColor,
                    Dock = DockStyle.Fill,
                    MinimumSize = new Size(action.Width, ControlHeight)
                };
                actionHost.Controls.Add(action);
                row.Controls.Add(actionHost, 1, 0);

                void CenterAction()
                {
                    action.Location = new Point(0, Math.Max(0, (actionHost.ClientSize.Height - action.Height) / 2));
                }

                actionHost.Layout += (_, __) => CenterAction();
                CenterAction();
            }
            else
            {
                row.Controls.Add(primary, 0, 0);
                row.Controls.Add(action, 1, 0);
            }

            return row;
        }

        private Panel CreateFieldHost(Control control, bool topAlign = false)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, topAlign ? 10 : 14, 0, 16)
            };
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.Location = new Point(0, topAlign ? 10 : 14);
            panel.Controls.Add(control);
            return panel;
        }

        private Control CreateMarginRow()
        {
            const int suffixMarginTop = 23;
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };
            row.Controls.Add(_numMarginX);
            row.Controls.Add(new Label
            {
                Text = " / ",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(6, suffixMarginTop, 6, 0)
            });
            row.Controls.Add(_numMarginY);
            return row;
        }

        private Control CreateOpacityRow()
        {
            const int suffixMarginTop = 23;
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };
            row.Controls.Add(_numOpacity);
            row.Controls.Add(new Label
            {
                Text = "%",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(8, suffixMarginTop, 0, 0),
                Font = new Font("Segoe UI", 10F)
            });
            return row;
        }

        private NumericUpDown CreatePercentNumeric(int min, int max, int defaultValue) => new NumericUpDown
        {
            Width = GetCompactFieldWidth(ClientSize.Width - Padding.Horizontal),
            Height = ControlHeight,
            Minimum = min,
            Maximum = max,
            Value = Math.Max(min, Math.Min(max, defaultValue)),
            BackColor = Color.FromArgb(45, 49, 60),
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10.5F)
        };

        private void LoadFromVideo()
        {
            _chkEnabled.Checked = _video.ShowcaseBrandLogoEnabled;
            _txtFile.Text = _video.ShowcaseBrandLogoFile ?? string.Empty;
            SelectPosition(_video.ShowcaseBrandLogoPositionId);
            _numScale.Value = ShowcaseBrandOverlayHelper.ClampScaleWidthPercent(
                _video.ShowcaseBrandLogoScaleWidthPercent > 0
                    ? _video.ShowcaseBrandLogoScaleWidthPercent
                    : ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent);
            _numMarginX.Value = ShowcaseBrandOverlayHelper.ClampMargin(_video.ShowcaseBrandLogoMarginX);
            _numMarginY.Value = ShowcaseBrandOverlayHelper.ClampMargin(_video.ShowcaseBrandLogoMarginY);
            _numOpacity.Value = ShowcaseBrandOverlayHelper.ClampOpacityPercent(
                _video.ShowcaseBrandLogoOpacityPercent > 0
                    ? _video.ShowcaseBrandLogoOpacityPercent
                    : ShowcaseBrandOverlayHelper.DefaultOpacityPercent);
            UpdatePreview();
        }

        private void SelectPosition(string positionId)
        {
            var resolved = ShowcaseBrandLogoPositionCatalog.ResolveId(positionId);
            for (var i = 0; i < _cbPosition.Items.Count; i++)
            {
                if (_cbPosition.Items[i] is PositionItem item
                    && string.Equals(item.Id, resolved, StringComparison.Ordinal))
                {
                    _cbPosition.SelectedIndex = i;
                    return;
                }
            }

            _cbPosition.SelectedIndex = 0;
        }

        private void BrowseLogoFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                var libraryDir = OmniBrandLogoLibrary.EnsureSharedLogosDirectory(_settings);
                dlg.Title = "Chọn logo từ " + OmniBrandLogoLibrary.DisplayRelativePath;
                dlg.Filter = "Ảnh logo|*.png;*.jpg;*.jpeg;*.webp|Tất cả|*.*";
                dlg.CheckFileExists = true;
                dlg.InitialDirectory = libraryDir;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtFile.Text = OmniBrandLogoLibrary.NormalizeStoredReference(dlg.FileName, _settings);
                    _chkEnabled.Checked = true;
                    UpdatePreview();
                }
            }
        }

        private void OpenLogoLibraryFolder()
        {
            try
            {
                var dir = OmniBrandLogoLibrary.EnsureSharedLogosDirectory(_settings);
                Process.Start("explorer.exe", "\"" + dir + "\"");
                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Logo thương hiệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdatePreview()
        {
            OmniBrandLogoLibrary.FormatLibraryPanelLines(_settings, out var dir, out var hint);
            _lblLibraryDir.Text = dir;
            _lblLibraryHint.Text = hint;
            UpdateVisualPreview();
        }

        private void UpdateVisualPreview()
        {
            var previous = _picPreview.Image;
            _picPreview.Image = null;
            previous?.Dispose();

            var plan = BuildCurrentPreviewPlan();
            string statusMessage;
            if (!_chkEnabled.Checked)
            {
                statusMessage = "Logo đang tắt";
            }
            else if (!plan.IsActive)
            {
                statusMessage = "Chưa chọn file logo";
            }
            else
            {
                statusMessage = string.Empty;
            }

            _lblPreviewHint.Text = plan.IsActive
                ? ShowcaseBrandLogoPositionCatalog.GetDisplayLabel(plan.PositionId)
                  + " · " + plan.ScaleWidthPercent + "% · mờ " + plan.OpacityPercent + "%"
                : "Khung 9:16 · cập nhật theo thiết lập hiện tại";

            try
            {
                _picPreview.Image = ShowcaseBrandOverlayHelper.RenderLogoPreviewBitmap(
                    plan,
                    statusMessage,
                    PreviewFrameWidth);
            }
            catch
            {
                _picPreview.Image = ShowcaseBrandOverlayHelper.RenderLogoPreviewBitmap(
                    new ShowcaseBrandLogoRenderPlan(),
                    "Không tạo được xem thử",
                    PreviewFrameWidth);
            }
        }

        private ShowcaseBrandLogoRenderPlan BuildCurrentPreviewPlan()
        {
            return ShowcaseBrandOverlayHelper.BuildPreviewPlan(
                _chkEnabled.Checked,
                _txtFile.Text ?? string.Empty,
                _video.ProfileName,
                _settings,
                (_cbPosition.SelectedItem as PositionItem)?.Id ?? ShowcaseBrandLogoPositionCatalog.BottomRight,
                (int)_numScale.Value,
                (int)_numMarginX.Value,
                (int)_numMarginY.Value,
                (int)_numOpacity.Value);
        }

        private Control CreateLibraryInfoStack()
        {
            var stack = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            stack.Controls.Add(_lblLibraryDir);
            stack.Controls.Add(_lblLibraryHint);
            return stack;
        }

        private void SaveToVideo()
        {
            _video.ShowcaseBrandLogoEnabled = _chkEnabled.Checked;
            _video.ShowcaseBrandLogoFile = OmniBrandLogoLibrary.NormalizeStoredReference(_txtFile.Text ?? string.Empty, _settings);
            _video.ShowcaseBrandLogoPositionId = (_cbPosition.SelectedItem as PositionItem)?.Id
                ?? ShowcaseBrandLogoPositionCatalog.BottomRight;
            _video.ShowcaseBrandLogoScaleWidthPercent = (int)_numScale.Value;
            _video.ShowcaseBrandLogoMarginX = (int)_numMarginX.Value;
            _video.ShowcaseBrandLogoMarginY = (int)_numMarginY.Value;
            _video.ShowcaseBrandLogoOpacityPercent = (int)_numOpacity.Value;
            ShowcaseBrandOverlayHelper.EnsureVideoDefaults(_video);
            ShowcaseBrandOverlayHelper.RefreshLabel(_video, _settings);
        }

        private static Button CreateFolderIconButton() => new Button
        {
            Text = "📂",
            AutoSize = false,
            Width = InlineIconButtonWidth,
            Height = ControlHeight,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(58, 66, 82),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Emoji", 16F),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = Padding.Empty
        };

        private static Button CreateInlineButton(string text) => new Button
        {
            Text = text,
            AutoSize = false,
            Width = InlineButtonWidth,
            Height = ControlHeight,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(58, 66, 82),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F),
            Padding = new Padding(8, 6, 8, 6),
            TextAlign = ContentAlignment.MiddleCenter
        };

        private static Button CreateFooterButton(string text, Color back) => new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(160, 58),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(12, 6, 12, 6),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };

        private sealed class PositionItem
        {
            public PositionItem(string id, string label)
            {
                Id = id;
                Label = label;
            }

            public string Id { get; }

            public string Label { get; }

            public override string ToString() => Label;
        }
    }
}
