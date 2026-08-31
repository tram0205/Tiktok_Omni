using System;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class ProductAdImageShotCountDialog : Form
    {
        private readonly ProductAdImageBatchItem _item;
        private NumericUpDown _numSoloFemale;
        private NumericUpDown _numSoloMale;
        private NumericUpDown _numCouple;
        private NumericUpDown _numGroup;
        private NumericUpDown _numFlatlay;
        private NumericUpDown _numFabric;
        private NumericUpDown _numDetail;
        private ComboBox _cboAspect;
        private Label _lblTotal;

        public ProductAdImageShotCountDialog(ProductAdImageBatchItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            Text = "Số ảnh & tỉ lệ";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            ClientSize = new Size(720, 920);
            Padding = new Padding(24);

            BuildUi();
            LoadFromItem();
            UpdateTotal();
        }

        private void BuildUi()
        {
            var hint = new Label
            {
                Text = "Tổng tối đa 30 ảnh/dòng.",
                Dock = DockStyle.Top,
                Height = 40,
                ForeColor = Color.FromArgb(180, 186, 198)
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(0, 8, 0, 0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            for (var i = 0; i < 8; i++)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            }

            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            _numSoloFemale = CreateCountBox();
            _numSoloMale = CreateCountBox();
            _numCouple = CreateCountBox();
            _numGroup = CreateCountBox();
            _numFlatlay = CreateCountBox();
            _numFabric = CreateCountBox();
            _numDetail = CreateCountBox();

            AddCountRow(tbl, 0, "Đơn nữ", _numSoloFemale);
            AddCountRow(tbl, 1, "Đơn nam", _numSoloMale);
            AddCountRow(tbl, 2, "Cặp nam–nữ", _numCouple);
            AddCountRow(tbl, 3, "Hội nhóm", _numGroup);
            AddCountRow(tbl, 4, "Flatlay", _numFlatlay);
            AddCountRow(tbl, 5, "Cận vải", _numFabric);
            AddCountRow(tbl, 6, "Cận điểm nhấn", _numDetail);

            _cboAspect = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 26, 32),
                ForeColor = Color.Gainsboro
            };
            foreach (var id in ProductAdImageAspectRatioHelper.AllIds)
            {
                _cboAspect.Items.Add(ProductAdImageAspectRatioHelper.GetDisplayLabel(id));
            }

            tbl.Controls.Add(CreateFieldLabel("Tỉ lệ khung hình"), 0, 7);
            tbl.Controls.Add(_cboAspect, 1, 7);

            _lblTotal = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(210, 95, 130)
            };
            tbl.Controls.Add(_lblTotal, 0, 8);
            tbl.SetColumnSpan(_lblTotal, 2);

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
            tbl.Controls.Add(buttons, 0, 9);
            tbl.SetColumnSpan(buttons, 2);

            Controls.Add(tbl);
            Controls.Add(hint);
            CancelButton = btnCancel;
        }

        private void LoadFromItem()
        {
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
        }

        private void TryAccept()
        {
            var total = ReadTotal();
            if (total > ProductAdImageBatchItem.MaxImagesPerRow)
            {
                MessageBox.Show(
                    this,
                    "Tổng tối đa " + ProductAdImageBatchItem.MaxImagesPerRow + " ảnh/dòng.",
                    "Số ảnh & tỉ lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

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
            tbl.Controls.Add(num, 1, row);
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
