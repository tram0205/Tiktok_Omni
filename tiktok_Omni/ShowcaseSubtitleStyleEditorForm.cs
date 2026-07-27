using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseSubtitleStyleEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;
        private readonly AppSettings _defaults;

        private CheckBox _chkHookEnabled;
        private ComboBox _cbHookAnimation;
        private ComboBox _cbHookFont;
        private NumericUpDown _numHookFontSize;

        private CheckBox _chkBodyEnabled;
        private ComboBox _cbBodyPosition;
        private ComboBox _cbBodyFont;
        private NumericUpDown _numBodyFontSize;
        private ComboBox _cbBodyAnimation;
        private CheckBox _chkBodyBold;
        private CheckBox _chkBodyItalic;

        public ShowcaseSubtitleStyleEditorForm(ShowcaseVideoItem video, AppSettings defaults)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _defaults = defaults ?? new AppSettings();
            ShowcaseSubtitleStyleHelper.EnsureVideoDefaults(_video, _defaults);

            Text = "Phụ đề — " + ((_video.ProductName ?? string.Empty).Trim().Length > 0
                ? _video.ProductName.Trim()
                : "Showcase");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(1540, 984);
            Padding = new Padding(28, 12, 28, 8);
            MinimumSize = new Size(1480, 960);
            BuildUi();
            BuildDisplayTabUi();
            LoadFromVideo();
            LoadDisplayFromVideo();
        }

        private void BuildUi()
        {
            const int rowH = 64;
            const int gapH = 16;
            const int hintH = 36;
            const int labelW = 176;
            const int colGap = 24;

            Label MkFieldLbl(string text) => new Label
            {
                Text = text,
                AutoSize = false,
                Height = rowH,
                Width = labelW,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(0, 0, colGap, 0)
            };

            _chkHookEnabled = CreateCheck("Bật phụ đề hook", false);
            _cbHookAnimation = CreateCombo();
            _cbHookAnimation.DropDownHeight = 320;
            _cbHookAnimation.Items.AddRange(
                ShowcaseHookAnimationCatalog.All
                    .Select(e => (object)e.ComboLabel)
                    .ToArray());
            _cbHookFont = CreateCombo();
            _cbHookFont.Items.AddRange(ReupSubtitleStyleHelper.FontChoices.Cast<object>().ToArray());
            _numHookFontSize = CreateNumeric(72, 132, 94);

            _chkBodyEnabled = CreateCheck("Bật phụ đề thân", false);
            _cbBodyPosition = CreateCombo();
            _cbBodyPosition.Items.AddRange(new object[] { "Dưới", "Giữa", "Trên" });
            _cbBodyFont = CreateCombo();
            _cbBodyFont.Items.AddRange(ReupSubtitleStyleHelper.FontChoices.Cast<object>().ToArray());
            _numBodyFontSize = CreateNumeric(32, 160, 72);
            _cbBodyAnimation = CreateCombo();
            _cbBodyAnimation.Items.AddRange(new object[]
            {
                "Pop (phóng to)",
                "Karaoke (tô màu)",
                "Hiện dần",
                "Cả dòng"
            });
            _chkBodyBold = CreateCheck("Đậm", true);
            _chkBodyItalic = CreateCheck("Nghiêng", false);

            var hookBox = CreateSectionGroup("Phụ đề HOOK (giữa màn, thu hút)");
            var hookTbl = CreateFieldTable(labelW, rowH, 2);
            AddFieldRow(hookTbl, MkFieldLbl("Bật hook"), WrapCheck(_chkHookEnabled), MkFieldLbl("Hiệu ứng"), _cbHookAnimation, 0);
            AddFieldRow(hookTbl, MkFieldLbl("Font hook"), _cbHookFont, MkFieldLbl("Cỡ chữ hook"), _numHookFontSize, 1);
            hookBox.Controls.Add(hookTbl);

            var bodyBox = CreateSectionGroup("Phụ đề THÂN (theo câu, 2–6 từ/dòng)");
            var bodyTbl = CreateFieldTable(labelW, rowH, 3);
            AddFieldRow(bodyTbl, MkFieldLbl("Bật thân"), WrapCheck(_chkBodyEnabled), MkFieldLbl("Vị trí thân"), _cbBodyPosition, 0);
            AddFieldRow(bodyTbl, MkFieldLbl("Font thân"), _cbBodyFont, MkFieldLbl("Cỡ chữ thân"), _numBodyFontSize, 1);
            var flpBodyTraits = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = hookBox.BackColor,
                Padding = new Padding(0, 18, 0, 0)
            };
            flpBodyTraits.Controls.Add(_chkBodyBold);
            flpBodyTraits.Controls.Add(_chkBodyItalic);
            AddFieldRow(bodyTbl, MkFieldLbl("Chạy chữ thân"), _cbBodyAnimation, MkFieldLbl("Kiểu thân"), flpBodyTraits, 2);
            bodyBox.Controls.Add(bodyTbl);

            var hint = new Label
            {
                Text = "Hook và thân bật/tắt độc lập. Chỉ hook = thu hút mở đầu; chỉ thân = phụ đề nội dung chính.",
                Dock = DockStyle.Top,
                Height = hintH,
                AutoSize = false,
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                Padding = new Padding(4, 4, 4, 0)
            };

            var spacer = new Panel { Height = gapH, Dock = DockStyle.Top, BackColor = BackColor };

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 8, 0),
                Margin = new Padding(0)
            };
            content.Controls.Add(hint);
            content.Controls.Add(bodyBox);
            content.Controls.Add(spacer);
            content.Controls.Add(hookBox);

            var btnDefaults = CreateButton("Mặc định app", Color.FromArgb(70, 78, 96));
            btnDefaults.Click += (_, __) =>
            {
                ShowcaseSubtitleStyleHelper.ApplySettingsDefaultsToVideo(_video, _defaults);
                LoadFromVideo();
            };
            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnDefaults);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 0, 8)
            };
            footer.Controls.Add(flpButtons);

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5F),
                Padding = new Point(12, 6)
            };
            var tabStyle = new TabPage("Kiểu chữ") { BackColor = BackColor, Padding = new Padding(8) };
            tabStyle.Controls.Add(content);
            var tabDisplay = new TabPage("Chữ hiển thị") { BackColor = BackColor, Padding = new Padding(4) };
            _displayTabHost = tabDisplay;

            tabs.TabPages.Add(tabStyle);
            tabs.TabPages.Add(tabDisplay);

            Controls.Add(tabs);
            Controls.Add(footer);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };

            static GroupBox CreateSectionGroup(string title)
            {
                var box = new GroupBox
                {
                    Text = title,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ForeColor = Color.FromArgb(200, 208, 222),
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    BackColor = Color.FromArgb(31, 34, 42),
                    Padding = new Padding(18, 34, 18, 20),
                    Margin = new Padding(0, 0, 0, 8)
                };
                return box;
            }

            static TableLayoutPanel CreateFieldTable(int labelColumnWidth, int rowHeight, int rowCount)
            {
                var tbl = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 4,
                    RowCount = rowCount,
                    BackColor = Color.FromArgb(31, 34, 42),
                    Padding = new Padding(0, 6, 0, 10)
                };
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColumnWidth));
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColumnWidth));
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                for (var i = 0; i < rowCount; i++)
                {
                    tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
                }

                return tbl;
            }

            static void AddFieldRow(TableLayoutPanel tbl, Control c0, Control c1, Control c2, Control c3, int row)
            {
                if (c1 is ComboBox || c1 is NumericUpDown || c1 is FlowLayoutPanel || c1 is Panel)
                {
                    c1.Dock = DockStyle.Fill;
                }

                if (c3 is ComboBox || c3 is NumericUpDown || c3 is FlowLayoutPanel || c3 is Panel)
                {
                    c3.Dock = DockStyle.Fill;
                }

                tbl.Controls.Add(c0, 0, row);
                tbl.Controls.Add(c1, 1, row);
                tbl.Controls.Add(c2, 2, row);
                tbl.Controls.Add(c3, 3, row);
            }

            static Panel WrapCheck(CheckBox chk)
            {
                var panel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(31, 34, 42),
                    Padding = new Padding(0, 18, 0, 0)
                };
                chk.AutoSize = true;
                chk.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                panel.Controls.Add(chk);
                return panel;
            }
        }

        private void LoadFromVideo()
        {
            _chkHookEnabled.Checked = _video.ShowcaseHookSubtitleEnabled;
            _cbHookAnimation.SelectedIndex = ShowcaseSubtitleStyleHelper.HookAnimationToSelectedIndex(
                _video.ShowcaseHookSubtitleAnimation);
            SelectFont(_cbHookFont, _video.ShowcaseHookSubtitleFontName, _video.ShowcaseSubtitleFontName);
            _numHookFontSize.Value = Math.Max(_numHookFontSize.Minimum,
                Math.Min(_numHookFontSize.Maximum,
                    _video.ShowcaseHookSubtitleFontSize > 0
                        ? _video.ShowcaseHookSubtitleFontSize
                        : Math.Max(80, _video.ShowcaseSubtitleFontSize + 22)));

            _chkBodyEnabled.Checked = _video.ShowcaseSubtitleEnabled;
            _cbBodyPosition.SelectedIndex = ReupSubtitleStyleHelper.ParsePosition(_video.ShowcaseSubtitlePosition) switch
            {
                ReupSubtitleVerticalPosition.Top => 2,
                ReupSubtitleVerticalPosition.Middle => 1,
                _ => 0
            };
            SelectFont(_cbBodyFont, _video.ShowcaseSubtitleFontName, "Segoe UI Bold");
            _numBodyFontSize.Value = Math.Max(_numBodyFontSize.Minimum,
                Math.Min(_numBodyFontSize.Maximum, _video.ShowcaseSubtitleFontSize <= 0 ? 72 : _video.ShowcaseSubtitleFontSize));
            _cbBodyAnimation.SelectedIndex = ReupSubtitleStyleHelper.ParseAnimation(_video.ShowcaseSubtitleAnimation) switch
            {
                ReupKaraokeAnimationMode.Highlight => 1,
                ReupKaraokeAnimationMode.FadeIn => 2,
                ReupKaraokeAnimationMode.Plain => 3,
                _ => 0
            };
            _chkBodyBold.Checked = _video.ShowcaseSubtitleBold;
            _chkBodyItalic.Checked = _video.ShowcaseSubtitleItalic;
        }

        private static void SelectFont(ComboBox combo, string primary, string fallback)
        {
            var font = (primary ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(font))
            {
                font = (fallback ?? string.Empty).Trim();
            }

            var idx = combo.Items.IndexOf(font);
            combo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private bool ValidateAndSave()
        {
            _video.ShowcaseHookSubtitleEnabled = _chkHookEnabled.Checked;
            _video.ShowcaseHookSubtitleAnimation = ShowcaseSubtitleStyleHelper.HookAnimationToStorage(
                _cbHookAnimation.SelectedIndex);
            _video.ShowcaseHookSubtitleFontName = _cbHookFont.Text?.Trim() ?? string.Empty;
            _video.ShowcaseHookSubtitleFontSize = (int)_numHookFontSize.Value;

            _video.ShowcaseSubtitleEnabled = _chkBodyEnabled.Checked;
            _video.ShowcaseSubtitlePosition = _cbBodyPosition.SelectedIndex switch
            {
                2 => "Top",
                1 => "Middle",
                _ => "Bottom"
            };
            _video.ShowcaseSubtitleFontName = _cbBodyFont.Text?.Trim() ?? "Segoe UI Bold";
            _video.ShowcaseSubtitleFontSize = (int)_numBodyFontSize.Value;
            _video.ShowcaseTextSize = _video.ShowcaseSubtitleFontSize;
            _video.ShowcaseSubtitleAnimation = _cbBodyAnimation.SelectedIndex switch
            {
                1 => "Highlight",
                2 => "FadeIn",
                3 => "Plain",
                _ => "Pop"
            };
            _video.ShowcaseSubtitleBold = _chkBodyBold.Checked;
            _video.ShowcaseSubtitleItalic = _chkBodyItalic.Checked;
            SaveDisplayToVideo();
            ShowcaseSubtitleStyleHelper.RefreshStyleLabel(_video);
            return true;
        }

        private static ComboBox CreateCombo() => new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 49, 60),
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(0, 10, 12, 10)
        };

        private static NumericUpDown CreateNumeric(int min, int max, int value) => new NumericUpDown
        {
            Width = 120,
            Minimum = min,
            Maximum = max,
            Value = value,
            BackColor = Color.FromArgb(45, 49, 60),
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(0, 10, 0, 10)
        };

        private static CheckBox CreateCheck(string text, bool check) => new CheckBox
        {
            Text = text,
            AutoSize = true,
            Checked = check,
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(0, 0, 24, 0)
        };

        private static Button CreateButton(string text, Color back) => new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(140, 48),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(12, 0, 0, 0)
        };
    }
}
