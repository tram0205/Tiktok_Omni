using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Hộp thoại chỉnh phụ đề karaoke cho một dòng Video Triết lý.</summary>
    internal sealed class PhilosophySubtitleStyleEditorForm : Form
    {
        private readonly PhilosophyScriptItem _item;

        private ComboBox _cbPosition;
        private ComboBox _cbFont;
        private NumericUpDown _numFontSize;
        private ComboBox _cbAnimation;
        private ComboBox _cbColourPreset;
        private CheckBox _chkBold;
        private CheckBox _chkItalic;
        private NumericUpDown _numWordsPerLine;

        public PhilosophySubtitleStyleEditorForm(PhilosophyScriptItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            PhilosophySubtitleStyleHelper.EnsureDefaults(_item);

            var preview = TrimPreview(_item.Content);
            Text = "Phụ đề — " + (string.IsNullOrEmpty(preview) ? "Video Triết lý" : preview);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(440, 286);
            Padding = new Padding(12);

            BuildUi();
            LoadFromItem();
        }

        private void BuildUi()
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 6,
                BackColor = BackColor
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Label MkLbl(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 4, 0)
            };

            _cbPosition = CreateCombo();
            _cbPosition.Items.AddRange(new object[] { "Dưới", "Giữa", "Trên" });

            _cbFont = CreateCombo();
            _cbFont.Items.AddRange(ReupSubtitleStyleHelper.FontChoices.Cast<object>().ToArray());
            if (_cbFont.Items.IndexOf("Times New Roman") < 0)
            {
                _cbFont.Items.Add("Times New Roman");
            }

            _numFontSize = CreateNumeric(32, 160, 76);

            _cbAnimation = CreateCombo();
            _cbAnimation.Items.AddRange(new object[]
            {
                "Pop (phóng to)",
                "Karaoke (tô màu)",
                "Hiện dần",
                "Cả dòng"
            });

            _cbColourPreset = CreateCombo();
            _cbColourPreset.Items.AddRange(new object[]
            {
                "Trắng + vàng karaoke",
                "Trắng + xanh lá",
                "Vàng + trắng",
                "Trắng + đỏ nhạt"
            });

            _chkBold = CreateCheck("Đậm", true);
            _chkItalic = CreateCheck("Nghiêng", false);

            _numWordsPerLine = CreateNumeric(4, 12, 8);

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
            tbl.Controls.Add(MkLbl("Tối đa từ/dòng"), 2, 2);
            tbl.Controls.Add(_numWordsPerLine, 3, 2);

            tbl.Controls.Add(MkLbl("Màu"), 0, 3);
            tbl.Controls.Add(_cbColourPreset, 1, 3);
            tbl.SetColumnSpan(_cbColourPreset, 3);

            var btnDefaults = CreateButton("Mặc định Triết lý", Color.FromArgb(70, 78, 96));
            btnDefaults.Click += (_, __) =>
            {
                PhilosophySubtitleStyleHelper.ApplyPhilosophyDefaults(_item);
                LoadFromItem();
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
                Margin = new Padding(0, 8, 0, 0)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnDefaults);

            tbl.Controls.Add(flpButtons, 0, 5);
            tbl.SetColumnSpan(flpButtons, 4);

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            Controls.Add(tbl);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };
        }

        private void LoadFromItem()
        {
            _cbPosition.SelectedIndex = ReupSubtitleStyleHelper.ParsePosition(_item.SubtitlePosition) switch
            {
                ReupSubtitleVerticalPosition.Top => 2,
                ReupSubtitleVerticalPosition.Middle => 1,
                _ => 0
            };

            var font = (_item.SubtitleFontName ?? string.Empty).Trim();
            var fontIdx = _cbFont.Items.IndexOf(font);
            _cbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : Math.Max(0, _cbFont.Items.IndexOf("Times New Roman"));

            _numFontSize.Value = Math.Max(_numFontSize.Minimum,
                Math.Min(_numFontSize.Maximum, _item.SubtitleFontSize <= 0 ? 76 : _item.SubtitleFontSize));

            _cbAnimation.SelectedIndex = ReupSubtitleStyleHelper.ParseAnimation(_item.SubtitleAnimation) switch
            {
                ReupKaraokeAnimationMode.Highlight => 1,
                ReupKaraokeAnimationMode.FadeIn => 2,
                ReupKaraokeAnimationMode.Plain => 3,
                _ => 0
            };

            _chkBold.Checked = _item.SubtitleBold;
            _chkItalic.Checked = _item.SubtitleItalic;
            _numWordsPerLine.Value = Math.Max(_numWordsPerLine.Minimum,
                Math.Min(_numWordsPerLine.Maximum, _item.SubtitleWordsPerLine <= 0 ? 8 : _item.SubtitleWordsPerLine));

            _cbColourPreset.SelectedIndex = ResolveColourPresetIndex(
                _item.SubtitlePrimaryColourAss,
                _item.SubtitleSecondaryColourAss);
        }

        private bool ValidateAndSave()
        {
            _item.SubtitlePosition = _cbPosition.SelectedIndex switch
            {
                2 => "Top",
                1 => "Middle",
                _ => "Bottom"
            };
            _item.SubtitleFontName = _cbFont.Text?.Trim() ?? "Times New Roman";
            _item.SubtitleFontSize = (int)_numFontSize.Value;
            _item.SubtitleAnimation = _cbAnimation.SelectedIndex switch
            {
                1 => "Highlight",
                2 => "FadeIn",
                3 => "Plain",
                _ => "Pop"
            };
            _item.SubtitleBold = _chkBold.Checked;
            _item.SubtitleItalic = _chkItalic.Checked;
            _item.SubtitleWordsPerLine = (int)_numWordsPerLine.Value;
            ApplyColourPreset(_cbColourPreset.SelectedIndex);
            _item.SubtitleStyleLabel = PhilosophySubtitleStyleHelper.FormatStyleSummary(_item);
            return true;
        }

        private static int ResolveColourPresetIndex(string primary, string secondary)
        {
            if (string.Equals(secondary, "&H0000FF00", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(primary, "&H0000FFFF", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(secondary, "&H006666FF", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return 0;
        }

        private void ApplyColourPreset(int index)
        {
            switch (index)
            {
                case 1:
                    _item.SubtitlePrimaryColourAss = "&H00FFFFFF";
                    _item.SubtitleSecondaryColourAss = "&H0000FF00";
                    break;
                case 2:
                    _item.SubtitlePrimaryColourAss = "&H0000FFFF";
                    _item.SubtitleSecondaryColourAss = "&H00FFFFFF";
                    break;
                case 3:
                    _item.SubtitlePrimaryColourAss = "&H00FFFFFF";
                    _item.SubtitleSecondaryColourAss = "&H006666FF";
                    break;
                default:
                    _item.SubtitlePrimaryColourAss = "&H00FFFFFF";
                    _item.SubtitleSecondaryColourAss = "&H00D7FF00";
                    break;
            }
        }

        private static string TrimPreview(string text)
        {
            var t = (text ?? string.Empty).Replace("\r\n", " ").Trim();
            return t.Length <= 48 ? t : t.Substring(0, 45) + "…";
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 4, 0, 4)
            };
        }

        private static NumericUpDown CreateNumeric(int min, int max, int value)
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 64,
                Minimum = min,
                Maximum = max,
                Value = value,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 4, 0, 4)
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
                Margin = new Padding(0, 6, 12, 0)
            };
        }

        private static Button CreateButton(string text, Color back)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(88, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(6, 0, 0, 0)
            };
        }
    }
}
