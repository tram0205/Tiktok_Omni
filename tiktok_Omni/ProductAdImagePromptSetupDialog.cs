using System;
using System.Drawing;
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
        private ComboBox _cboProductType;
        private ComboBox _cboShootStyle;
        private ComboBox _cboIdentityLock;
        private TextBox _txtProductLock;

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
            ClientSize = new Size(1440, 720);
            Padding = new Padding(24);

            BuildUi();
            LoadFromItem();
        }

        private void BuildUi()
        {
            var hint = new Label
            {
                Text = "Gemini dùng các tuỳ chọn này khi «Lập prompt» — giữ đúng chi tiết sản phẩm, chỉ đổi pose/nền.",
                Dock = DockStyle.Top,
                Height = 48,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(180, 186, 198)
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(0, 12, 0, 0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            _cboProductType = CreateCombo();
            _cboProductType.DataSource = ShowcaseProductTypePresets.All.ToList();
            _cboProductType.DisplayMember = nameof(ShowcaseProductTypePreset.DisplayLabel);

            _cboShootStyle = CreateCombo();
            _cboShootStyle.DataSource = ProductAdImageShootStylePresets.All.ToList();
            _cboShootStyle.DisplayMember = nameof(ProductAdImageShootStylePreset.DisplayLabel);

            _cboIdentityLock = CreateCombo();
            _cboIdentityLock.DataSource = ProductAdImageIdentityLockPresets.All.ToList();
            _cboIdentityLock.DisplayMember = nameof(ProductAdImageIdentityLockPreset.DisplayLabel);

            _txtProductLock = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };

            tbl.Controls.Add(CreateFieldLabel("Loại SP"), 0, 0);
            tbl.Controls.Add(_cboProductType, 1, 0);
            tbl.Controls.Add(CreateFieldLabel("Phong cách"), 0, 1);
            tbl.Controls.Add(_cboShootStyle, 1, 1);
            tbl.Controls.Add(CreateFieldLabel("Giữ mẫu"), 0, 2);
            tbl.Controls.Add(_cboIdentityLock, 1, 2);
            tbl.Controls.Add(CreateFieldLabel("Khóa SP"), 0, 3);
            tbl.Controls.Add(_txtProductLock, 1, 3);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var btnCancel = CreateDialogButton("Huỷ", DialogResult.Cancel, Color.FromArgb(68, 72, 86));
            var btnOk = CreateDialogButton("OK", DialogResult.OK, Color.FromArgb(56, 142, 88));
            btnOk.Click += (_, __) => ApplyToItem();
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnOk);
            tbl.Controls.Add(buttons, 0, 4);
            tbl.SetColumnSpan(buttons, 2);

            Controls.Add(tbl);
            Controls.Add(hint);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void LoadFromItem()
        {
            SelectByIndex(_cboProductType, ShowcaseProductTypePresets.FindIndexByPromptHint(_item.ProductTypePrompt));
            SelectByIndex(_cboShootStyle, ProductAdImageShootStylePresets.FindIndexByPromptHint(_item.ShootStylePrompt));
            SelectByIndex(_cboIdentityLock, ProductAdImageIdentityLockPresets.FindIndexByPromptHint(_item.IdentityLockPrompt));
            _txtProductLock.Text = _item.ProductLockDescription ?? string.Empty;
        }

        private void ApplyToItem()
        {
            _item.ProductTypePrompt = (_cboProductType.SelectedItem as ShowcaseProductTypePreset)?.PromptHint ?? string.Empty;
            _item.ShootStylePrompt = (_cboShootStyle.SelectedItem as ProductAdImageShootStylePreset)?.PromptHint ?? string.Empty;
            _item.IdentityLockPrompt = (_cboIdentityLock.SelectedItem as ProductAdImageIdentityLockPreset)?.PromptHint ?? string.Empty;
            _item.ProductLockDescription = _txtProductLock.Text ?? string.Empty;
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
                Width = 140,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(8, 8, 0, 8)
            };
        }
    }
}
