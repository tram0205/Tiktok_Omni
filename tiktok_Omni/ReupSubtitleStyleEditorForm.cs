using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Hộp thoại chỉnh phụ đề hook cho một dòng Video reup.</summary>
    internal sealed class ReupSubtitleStyleEditorForm : Form
    {
        private readonly VideoReupRowItem _row;
        private readonly AppSettings _defaults;

        private ComboBox _cbPosition;
        private ComboBox _cbFont;
        private NumericUpDown _numFontSize;
        private ComboBox _cbAnimation;
        private CheckBox _chkBold;
        private CheckBox _chkItalic;
        private NumericUpDown _numWordsPerLine;

        public ReupSubtitleStyleEditorForm(VideoReupRowItem row, AppSettings defaults)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _defaults = defaults ?? new AppSettings();
            ReupSubtitleStyleHelper.EnsureRowDefaults(_row, _defaults);

            Text = "Phụ đề hook — " + ((_row.ProductName ?? string.Empty).Trim().Length > 0
                ? _row.ProductName.Trim()
                : "Video reup");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(840, 496);
            Padding = new Padding(20);

            BuildUi();
            LoadFromRow();
        }

        private void BuildUi()
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5,
                BackColor = BackColor
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Label MkLbl(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 14, 8, 0),
                Font = new Font("Segoe UI", 10.5F)
            };

            _cbPosition = CreateCombo();
            _cbPosition.Items.AddRange(new object[] { "Dưới", "Giữa", "Trên" });

            _cbFont = CreateCombo();
            _cbFont.Items.AddRange(ReupSubtitleStyleHelper.FontChoices.Cast<object>().ToArray());

            _numFontSize = CreateNumeric(32, 160, 88);

            _cbAnimation = CreateCombo();
            _cbAnimation.Items.AddRange(new object[]
            {
                "Pop (phóng to)",
                "Karaoke (tô màu)",
                "Hiện dần",
                "Cả dòng"
            });

            _chkBold = CreateCheck("Đậm", true);
            _chkItalic = CreateCheck("Nghiêng", false);

            _numWordsPerLine = CreateNumeric(4, 8, 6);

            tbl.Controls.Add(MkLbl("Vị trí"), 0, 0);
            tbl.Controls.Add(_cbPosition, 1, 0);
            tbl.Controls.Add(MkLbl("Font"), 2, 0);
            tbl.Controls.Add(_cbFont, 3, 0);

            tbl.Controls.Add(MkLbl("Cỡ chữ"), 0, 1);
            tbl.Controls.Add(_numFontSize, 1, 1);
            tbl.Controls.Add(MkLbl("Chạy chữ"), 2, 1);
            tbl.Controls.Add(_cbAnimation, 3, 1);

            var flpTraits = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 4, 0, 0)
            };
            flpTraits.Controls.Add(_chkBold);
            flpTraits.Controls.Add(_chkItalic);

            tbl.Controls.Add(MkLbl("Kiểu"), 0, 2);
            tbl.Controls.Add(flpTraits, 1, 2);
            tbl.Controls.Add(MkLbl("Từ/dòng"), 2, 2);
            tbl.Controls.Add(_numWordsPerLine, 3, 2);

            var btnDefaults = CreateButton("Mặc định app", Color.FromArgb(70, 78, 96));
            btnDefaults.Click += (_, __) =>
            {
                ReupSubtitleStyleHelper.ApplySettingsDefaultsToRow(_row, _defaults);
                LoadFromRow();
            };

            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;

            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(0, 8, 0, 0)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnDefaults);

            tbl.Controls.Add(flpButtons, 0, 4);
            tbl.SetColumnSpan(flpButtons, 4);

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

            Controls.Add(tbl);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };
        }

        private void LoadFromRow()
        {
            _cbPosition.SelectedIndex = ReupSubtitleStyleHelper.ParsePosition(_row.ReupSubtitlePosition) switch
            {
                ReupSubtitleVerticalPosition.Top => 2,
                ReupSubtitleVerticalPosition.Middle => 1,
                _ => 0
            };

            var font = (_row.ReupSubtitleFontName ?? string.Empty).Trim();
            var fontIdx = _cbFont.Items.IndexOf(font);
            _cbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;

            _numFontSize.Value = Math.Max(_numFontSize.Minimum,
                Math.Min(_numFontSize.Maximum, _row.ReupSubtitleFontSize <= 0 ? 88 : _row.ReupSubtitleFontSize));

            _cbAnimation.SelectedIndex = ReupSubtitleStyleHelper.ParseAnimation(_row.ReupSubtitleAnimation) switch
            {
                ReupKaraokeAnimationMode.Highlight => 1,
                ReupKaraokeAnimationMode.FadeIn => 2,
                ReupKaraokeAnimationMode.Plain => 3,
                _ => 0
            };

            _chkBold.Checked = _row.ReupSubtitleBold;
            _chkItalic.Checked = _row.ReupSubtitleItalic;
            _numWordsPerLine.Value = Math.Max(_numWordsPerLine.Minimum,
                Math.Min(_numWordsPerLine.Maximum, _row.ReupSubtitleWordsPerLine <= 0 ? 6 : _row.ReupSubtitleWordsPerLine));
        }

        private bool ValidateAndSave()
        {
            _row.ReupSubtitlePosition = _cbPosition.SelectedIndex switch
            {
                2 => "Top",
                1 => "Middle",
                _ => "Bottom"
            };
            _row.ReupSubtitleFontName = _cbFont.Text?.Trim() ?? "Segoe UI Bold";
            _row.ReupSubtitleFontSize = (int)_numFontSize.Value;
            _row.ReupSubtitleAnimation = _cbAnimation.SelectedIndex switch
            {
                1 => "Highlight",
                2 => "FadeIn",
                3 => "Plain",
                _ => "Pop"
            };
            _row.ReupSubtitleBold = _chkBold.Checked;
            _row.ReupSubtitleItalic = _chkItalic.Checked;
            _row.ReupSubtitleWordsPerLine = (int)_numWordsPerLine.Value;
            _row.ReupSubtitleStyleLabel = ReupSubtitleStyleHelper.FormatStyleSummary(_row);
            return true;
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        private static NumericUpDown CreateNumeric(int min, int max, int value)
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 96,
                Minimum = min,
                Maximum = max,
                Value = value,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        private static CheckBox CreateCheck(string text, bool check)
        {
            return new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = check,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(0, 10, 20, 0)
            };
        }

        private static Button CreateButton(string text, Color back)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(10, 0, 0, 0)
            };
        }
    }
}
