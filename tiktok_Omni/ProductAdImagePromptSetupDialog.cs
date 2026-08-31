using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ProductAdImagePromptSetupDialog : Form
    {
        private readonly ProductAdImageBatchItem _item;
        private TableLayoutPanel _tblPrompt;
        private ComboBox _cboProductType;
        private Label _lblProductTypeCustom;
        private TextBox _txtProductTypeCustom;
        private bool _productTypeCustomFocusEnabled;
        private ComboBox _cboShootStyle;
        private ComboBox _cboIdentityLock;
        private ComboBox _cboTheme;
        private Label _lblThemeCustom;
        private TextBox _txtThemeCustom;
        private bool _themeCustomFocusEnabled;
        private TextBox _txtProductLock;
        private Label _lblProductLockPlaceholder;
        private PictureBox _picRef;
        private Label _lblRefEmpty;
        private Image _refImage;
        private NumericUpDown _numSoloFemale;
        private NumericUpDown _numSoloMale;
        private NumericUpDown _numCouple;
        private NumericUpDown _numGroup;
        private NumericUpDown _numFlatlay;
        private NumericUpDown _numFabric;
        private NumericUpDown _numDetail;
        private ComboBox _cboAspect;
        private Label _lblTotal;

        public ProductAdImagePromptSetupDialog(ProductAdImageBatchItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            Text = "Thiết lập prompt";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            ClientSize = new Size(1872, 1020);
            Padding = new Padding(24, 20, 24, 16);

            BuildUi();
            LoadFromItem();
            LoadReferencePreview();
            UpdateTotal();
            FormClosed += (_, __) => DisposeRefImage();
        }

        private void BuildUi()
        {
            var hint = new Label
            {
                Text = "Gemini dùng các tuỳ chọn này khi «Lập prompt» — giữ đúng chi tiết sản phẩm, chỉ đổi pose/nền. Số ảnh tối đa 30/dòng.",
                Dock = DockStyle.Top,
                Height = 56,
                AutoEllipsis = true,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 4, 0, 8),
                ForeColor = Color.FromArgb(180, 186, 198)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0, 8, 0, 0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));

            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = Padding.Empty
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            split.Controls.Add(BuildPromptColumn(), 0, 0);
            split.Controls.Add(BuildShotCountColumn(), 1, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var btnCancel = CreateDialogButton("Huỷ", DialogResult.Cancel, Color.FromArgb(68, 72, 86));
            var btnOk = CreateDialogButton("OK", DialogResult.None, Color.FromArgb(56, 142, 88));
            btnOk.Click += (_, __) => TryAccept();
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnOk);

            root.Controls.Add(split, 0, 0);
            root.Controls.Add(buttons, 0, 1);

            Controls.Add(root);
            Controls.Add(hint);
            CancelButton = btnCancel;
        }

        private Control BuildPromptColumn()
        {
            _tblPrompt = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(0, 0, 18, 0)
            };
            _tblPrompt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            _tblPrompt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            _tblPrompt.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _cboProductType = CreateCombo();
            _cboProductType.DataSource = ShowcaseProductTypePresets.All.ToList();
            _cboProductType.DisplayMember = nameof(ShowcaseProductTypePreset.DisplayLabel);
            _cboProductType.SelectedIndexChanged += (_, __) => SyncProductTypeCustomBox();

            _lblProductTypeCustom = CreateFieldLabel("Nhập loại");
            _txtProductTypeCustom = new TextBox
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };

            _cboShootStyle = CreateCombo();
            _cboShootStyle.DataSource = ProductAdImageShootStylePresets.All.ToList();
            _cboShootStyle.DisplayMember = nameof(ProductAdImageShootStylePreset.DisplayLabel);

            _cboIdentityLock = CreateCombo();
            _cboIdentityLock.DataSource = ProductAdImageIdentityLockPresets.All.ToList();
            _cboIdentityLock.DisplayMember = nameof(ProductAdImageIdentityLockPreset.DisplayLabel);

            _cboTheme = CreateCombo();
            _cboTheme.DropDownHeight = 360;
            _cboTheme.DataSource = ProductAdImageThemePresets.All.ToList();
            _cboTheme.DisplayMember = nameof(ProductAdImageThemePreset.DisplayLabel);
            _cboTheme.SelectedIndexChanged += (_, __) => SyncThemeCustomBox();

            _lblThemeCustom = CreateFieldLabel("Nhập chủ đề");
            _txtThemeCustom = new TextBox
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };

            _txtProductLock = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtProductLock.TextChanged += (_, __) => SyncProductLockPlaceholder();
            _txtProductLock.GotFocus += (_, __) => SyncProductLockPlaceholder();
            _txtProductLock.LostFocus += (_, __) => SyncProductLockPlaceholder();

            var pnlLock = new Panel { Dock = DockStyle.Fill };
            var lblLockHint = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                UseCompatibleTextRendering = true,
                ForeColor = Color.FromArgb(168, 176, 190),
                Padding = new Padding(0, 2, 0, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Giữ đúng SP trên ảnh mẫu — chỉ đổi pose / nền / chủ đề."
            };
            _lblProductLockPlaceholder = new Label
            {
                Dock = DockStyle.Fill,
                UseCompatibleTextRendering = true,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.FromArgb(150, 158, 172),
                Padding = new Padding(8, 8, 8, 8),
                Cursor = Cursors.IBeam,
                Text = "Nên điền:\r\n"
                       + "• Màu vải, hoạ tiết / in, logo (đúng như ảnh)\r\n"
                       + "• Form dáng, nút / khoá, đường may\r\n"
                       + "• Không bịa zipper, thêu, logo nếu ảnh không có\r\n\r\n"
                       + "Vd: Áo dài lụa đỏ đô, thêu hoa đào ngực, nút tết, tà xẻ.\r\n"
                       + "Để trống = Gemini chỉ dựa vào ảnh mẫu."
            };
            _lblProductLockPlaceholder.Click += (_, __) => _txtProductLock.Focus();
            var pnlLockBox = new Panel { Dock = DockStyle.Fill };
            pnlLockBox.Controls.Add(_txtProductLock);
            pnlLockBox.Controls.Add(_lblProductLockPlaceholder);

            _picRef = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 22, 28)
            };
            _lblRefEmpty = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.FromArgb(150, 158, 172),
                Padding = new Padding(12),
                Text = "Chưa có ảnh mẫu.\r\nChọn ở cột Ảnh mẫu trên lưới."
            };
            var pnlRef = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                Margin = new Padding(12, 0, 0, 0),
                Padding = new Padding(1),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlRef.Controls.Add(_picRef);
            pnlRef.Controls.Add(_lblRefEmpty);

            var tblLock = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblLock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
            tblLock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            tblLock.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            tblLock.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblLock.Controls.Add(lblLockHint, 0, 0);
            tblLock.SetColumnSpan(lblLockHint, 2);
            tblLock.Controls.Add(pnlLockBox, 0, 1);
            tblLock.Controls.Add(pnlRef, 1, 1);
            pnlLock.Controls.Add(tblLock);

            _tblPrompt.Controls.Add(CreateFieldLabel("Loại SP"), 0, 0);
            _tblPrompt.Controls.Add(WrapField(_cboProductType), 1, 0);
            _tblPrompt.Controls.Add(_lblProductTypeCustom, 0, 1);
            _tblPrompt.Controls.Add(_txtProductTypeCustom, 1, 1);
            _tblPrompt.Controls.Add(CreateFieldLabel("Phong cách"), 0, 2);
            _tblPrompt.Controls.Add(WrapField(_cboShootStyle), 1, 2);
            _tblPrompt.Controls.Add(CreateFieldLabel("Giữ mẫu"), 0, 3);
            _tblPrompt.Controls.Add(WrapField(_cboIdentityLock), 1, 3);
            _tblPrompt.Controls.Add(CreateFieldLabel("Chủ đề"), 0, 4);
            _tblPrompt.Controls.Add(WrapField(_cboTheme), 1, 4);
            _tblPrompt.Controls.Add(_lblThemeCustom, 0, 5);
            _tblPrompt.Controls.Add(_txtThemeCustom, 1, 5);
            var lblLock = CreateFieldLabel("Khóa SP");
            lblLock.TextAlign = ContentAlignment.TopLeft;
            lblLock.Padding = new Padding(0, 8, 8, 2);
            _tblPrompt.Controls.Add(lblLock, 0, 6);
            _tblPrompt.Controls.Add(pnlLock, 1, 6);
            return _tblPrompt;
        }

        private Control BuildShotCountColumn()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 31, 38),
                Padding = new Padding(18, 18, 18, 16)
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 11,
                Padding = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            for (var i = 0; i < 8; i++)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            }

            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var title = new Label
            {
                Text = "Số ảnh & tỉ lệ",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.WhiteSmoke,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true
            };
            var sub = new Label
            {
                Text = "Tổng tối đa 30 ảnh/dòng.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 186, 198),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 2, 0, 4)
            };
            tbl.Controls.Add(title, 0, 0);
            tbl.SetColumnSpan(title, 3);
            tbl.Controls.Add(sub, 0, 1);
            tbl.SetColumnSpan(sub, 3);

            _numSoloFemale = CreateCountBox();
            _numSoloMale = CreateCountBox();
            _numCouple = CreateCountBox();
            _numGroup = CreateCountBox();
            _numFlatlay = CreateCountBox();
            _numFabric = CreateCountBox();
            _numDetail = CreateCountBox();

            AddCountRow(tbl, 2, "Đơn nữ", _numSoloFemale);
            AddCountRow(tbl, 3, "Đơn nam", _numSoloMale);
            AddCountRow(tbl, 4, "Cặp nam–nữ", _numCouple);
            AddCountRow(tbl, 5, "Hội nhóm", _numGroup);
            AddCountRow(tbl, 6, "Flatlay", _numFlatlay);
            AddCountRow(tbl, 7, "Cận vải", _numFabric);
            AddCountRow(tbl, 8, "Điểm nhấn", _numDetail);

            _cboAspect = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro,
                DropDownHeight = 280
            };
            foreach (var id in ProductAdImageAspectRatioHelper.AllIds)
            {
                _cboAspect.Items.Add(ProductAdImageAspectRatioHelper.GetDisplayLabel(id));
            }

            tbl.Controls.Add(CreateFieldLabel("Tỉ lệ khung hình"), 0, 9);
            tbl.Controls.Add(WrapField(_cboAspect), 1, 9);

            _lblTotal = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 8, 0, 0),
                ForeColor = Color.FromArgb(210, 95, 130),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            tbl.Controls.Add(_lblTotal, 0, 10);
            tbl.SetColumnSpan(_lblTotal, 3);

            host.Controls.Add(tbl);
            return host;
        }

        private void LoadFromItem()
        {
            _productTypeCustomFocusEnabled = false;
            _themeCustomFocusEnabled = false;
            LoadProductTypeFromItem();
            SelectByIndex(_cboShootStyle, ProductAdImageShootStylePresets.FindIndexByPromptHint(_item.ShootStylePrompt));
            SelectByIndex(_cboIdentityLock, ProductAdImageIdentityLockPresets.FindIndexByPromptHint(_item.IdentityLockPrompt));
            LoadThemeFromItem();
            _txtProductLock.Text = _item.ProductLockDescription ?? string.Empty;
            SyncProductLockPlaceholder();

            _numSoloFemale.Value = _item.SoloFemaleCount;
            _numSoloMale.Value = _item.SoloMaleCount;
            _numCouple.Value = _item.CoupleCount;
            _numGroup.Value = _item.GroupCount;
            _numFlatlay.Value = _item.FlatlayCount;
            _numFabric.Value = _item.FabricCloseupCount;
            _numDetail.Value = _item.DetailHighlightCount;

            var aspect = ProductAdImageAspectRatioHelper.Normalize(_item.AspectRatio);
            var aspectIndex = 0;
            for (var i = 0; i < ProductAdImageAspectRatioHelper.AllIds.Count; i++)
            {
                if (string.Equals(ProductAdImageAspectRatioHelper.AllIds[i], aspect, StringComparison.OrdinalIgnoreCase))
                {
                    aspectIndex = i;
                    break;
                }
            }

            if (_cboAspect.Items.Count > 0)
            {
                _cboAspect.SelectedIndex = aspectIndex;
            }

            _productTypeCustomFocusEnabled = true;
            _themeCustomFocusEnabled = true;
        }

        private void LoadReferencePreview()
        {
            DisposeRefImage();
            if (_picRef == null || _lblRefEmpty == null)
            {
                return;
            }

            var path = _item.ModelImagePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _lblRefEmpty.Text = "Chưa có ảnh mẫu.\r\nChọn ở cột Ảnh mẫu trên lưới.";
                _lblRefEmpty.Visible = true;
                _lblRefEmpty.BringToFront();
                return;
            }

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: true))
                {
                    _refImage = new Bitmap(img);
                }

                _picRef.Image = _refImage;
                _lblRefEmpty.Visible = false;
                _picRef.BringToFront();
            }
            catch
            {
                _lblRefEmpty.Text = "Không đọc được ảnh mẫu.";
                _lblRefEmpty.Visible = true;
                _lblRefEmpty.BringToFront();
            }
        }

        private void DisposeRefImage()
        {
            if (_picRef != null)
            {
                _picRef.Image = null;
            }

            if (_refImage != null)
            {
                _refImage.Dispose();
                _refImage = null;
            }
        }

        private void TryAccept()
        {
            var total = ReadTotal();
            if (total > ProductAdImageBatchItem.MaxImagesPerRow)
            {
                MessageBox.Show(
                    this,
                    "Tổng tối đa " + ProductAdImageBatchItem.MaxImagesPerRow + " ảnh/dòng.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _item.ProductTypePrompt = ReadProductTypePrompt();
            _item.ShootStylePrompt = (_cboShootStyle.SelectedItem as ProductAdImageShootStylePreset)?.PromptHint ?? string.Empty;
            _item.IdentityLockPrompt = (_cboIdentityLock.SelectedItem as ProductAdImageIdentityLockPreset)?.PromptHint ?? string.Empty;
            _item.ThemePrompt = ReadThemePrompt();
            _item.ProductLockDescription = _txtProductLock.Text ?? string.Empty;
            _item.ApplyShotCounts(
                (int)_numSoloFemale.Value,
                (int)_numSoloMale.Value,
                (int)_numCouple.Value,
                (int)_numGroup.Value,
                (int)_numFlatlay.Value,
                (int)_numFabric.Value,
                (int)_numDetail.Value);

            var aspectIndex = _cboAspect.SelectedIndex;
            if (aspectIndex >= 0 && aspectIndex < ProductAdImageAspectRatioHelper.AllIds.Count)
            {
                _item.AspectRatio = ProductAdImageAspectRatioHelper.AllIds[aspectIndex];
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void LoadProductTypeFromItem()
        {
            var hint = (_item.ProductTypePrompt ?? string.Empty).Trim();
            var index = ShowcaseProductTypePresets.FindIndexByPromptHint(hint);
            if (index >= 0)
            {
                SelectByIndex(_cboProductType, index);
                if (ShowcaseProductTypePresets.IsCustomPrompt(hint))
                {
                    _txtProductTypeCustom.Text = string.Empty;
                }
            }
            else if (!string.IsNullOrEmpty(hint))
            {
                SelectCustomProductType();
                _txtProductTypeCustom.Text = hint;
            }
            else
            {
                SelectByIndex(_cboProductType, 0);
            }

            SyncProductTypeCustomBox();
        }

        private void SelectCustomProductType()
        {
            for (var i = 0; i < _cboProductType.Items.Count; i++)
            {
                if (_cboProductType.Items[i] is ShowcaseProductTypePreset preset
                    && ShowcaseProductTypePresets.IsCustomPreset(preset))
                {
                    _cboProductType.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SyncProductTypeCustomBox()
        {
            var custom = _cboProductType.SelectedItem is ShowcaseProductTypePreset preset
                         && ShowcaseProductTypePresets.IsCustomPreset(preset);
            _txtProductTypeCustom.Visible = custom;
            if (_lblProductTypeCustom != null)
            {
                _lblProductTypeCustom.Visible = custom;
            }

            if (_tblPrompt != null && _tblPrompt.RowStyles.Count > 1)
            {
                _tblPrompt.RowStyles[1].Height = custom ? 96F : 0F;
            }

            if (custom && _productTypeCustomFocusEnabled && _txtProductTypeCustom.CanFocus)
            {
                _txtProductTypeCustom.Focus();
            }
        }

        private string ReadProductTypePrompt()
        {
            if (_cboProductType.SelectedItem is ShowcaseProductTypePreset preset
                && ShowcaseProductTypePresets.IsCustomPreset(preset))
            {
                var typed = (_txtProductTypeCustom.Text ?? string.Empty).Trim();
                return string.IsNullOrEmpty(typed)
                    ? ShowcaseProductTypePresets.CustomPromptSentinel
                    : typed;
            }

            return (_cboProductType.SelectedItem as ShowcaseProductTypePreset)?.PromptHint ?? string.Empty;
        }

        private void LoadThemeFromItem()
        {
            var hint = (_item.ThemePrompt ?? string.Empty).Trim();
            var index = ProductAdImageThemePresets.FindIndexByPromptHint(hint);
            if (index >= 0)
            {
                SelectByIndex(_cboTheme, index);
                if (ProductAdImageThemePresets.IsCustomPrompt(hint))
                {
                    _txtThemeCustom.Text = string.Empty;
                }
            }
            else if (!string.IsNullOrEmpty(hint))
            {
                SelectCustomTheme();
                _txtThemeCustom.Text = hint;
            }
            else
            {
                SelectByIndex(_cboTheme, 0);
            }

            SyncThemeCustomBox();
        }

        private void SelectCustomTheme()
        {
            for (var i = 0; i < _cboTheme.Items.Count; i++)
            {
                if (_cboTheme.Items[i] is ProductAdImageThemePreset preset
                    && ProductAdImageThemePresets.IsCustomPreset(preset))
                {
                    _cboTheme.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SyncThemeCustomBox()
        {
            var custom = _cboTheme.SelectedItem is ProductAdImageThemePreset preset
                         && ProductAdImageThemePresets.IsCustomPreset(preset);
            _txtThemeCustom.Visible = custom;
            if (_lblThemeCustom != null)
            {
                _lblThemeCustom.Visible = custom;
            }

            if (_tblPrompt != null && _tblPrompt.RowStyles.Count > 5)
            {
                _tblPrompt.RowStyles[5].Height = custom ? 96F : 0F;
            }

            if (custom && _themeCustomFocusEnabled && _txtThemeCustom.CanFocus)
            {
                _txtThemeCustom.Focus();
            }
        }

        private string ReadThemePrompt()
        {
            if (_cboTheme.SelectedItem is ProductAdImageThemePreset preset
                && ProductAdImageThemePresets.IsCustomPreset(preset))
            {
                var typed = (_txtThemeCustom.Text ?? string.Empty).Trim();
                return string.IsNullOrEmpty(typed)
                    ? ProductAdImageThemePresets.CustomPromptSentinel
                    : typed;
            }

            return (_cboTheme.SelectedItem as ProductAdImageThemePreset)?.PromptHint ?? string.Empty;
        }

        private void SyncProductLockPlaceholder()
        {
            if (_lblProductLockPlaceholder == null || _txtProductLock == null)
            {
                return;
            }

            var show = string.IsNullOrWhiteSpace(_txtProductLock.Text) && !_txtProductLock.Focused;
            _lblProductLockPlaceholder.Visible = show;
            if (show)
            {
                _lblProductLockPlaceholder.BringToFront();
            }
        }

        private int ReadTotal()
        {
            return (int)_numSoloFemale.Value
                   + (int)_numSoloMale.Value
                   + (int)_numCouple.Value
                   + (int)_numGroup.Value
                   + (int)_numFlatlay.Value
                   + (int)_numFabric.Value
                   + (int)_numDetail.Value;
        }

        private void UpdateTotal()
        {
            if (_lblTotal == null)
            {
                return;
            }

            var total = ReadTotal();
            _lblTotal.Text = "Tổng: " + total + " / " + ProductAdImageBatchItem.MaxImagesPerRow;
            _lblTotal.ForeColor = total > ProductAdImageBatchItem.MaxImagesPerRow
                ? Color.FromArgb(255, 120, 120)
                : Color.FromArgb(210, 95, 130);
        }

        private NumericUpDown CreateCountBox()
        {
            var num = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = ProductAdImageBatchItem.MaxImagesPerRow,
                DecimalPlaces = 0,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro
            };
            num.ValueChanged += (_, __) => UpdateTotal();
            return num;
        }

        private static void AddCountRow(TableLayoutPanel tbl, int row, string label, NumericUpDown num)
        {
            tbl.Controls.Add(CreateFieldLabel(label), 0, row);
            tbl.Controls.Add(WrapField(num), 1, row);
        }

        private static Control WrapField(Control field)
        {
            field.Dock = DockStyle.Fill;
            field.Margin = Padding.Empty;
            var wrap = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8)
            };
            wrap.Controls.Add(field);
            return wrap;
        }

        private static void SelectByIndex(ComboBox combo, int index)
        {
            if (combo.Items.Count == 0)
            {
                return;
            }

            combo.SelectedIndex = index >= 0 && index < combo.Items.Count ? index : 0;
        }

        private static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 2, 8, 2),
                ForeColor = Color.FromArgb(200, 206, 218)
            };
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro
            };
        }

        private static Button CreateDialogButton(string text, DialogResult result, Color back)
        {
            return new Button
            {
                Text = text,
                DialogResult = result,
                Width = 160,
                Height = 52,
                MinimumSize = new Size(160, 52),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(8, 10, 0, 10),
                Cursor = Cursors.Hand
            };
        }
    }
}
