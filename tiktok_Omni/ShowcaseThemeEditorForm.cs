using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseThemeEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;

        private ListBox _lstPresets;
        private Label _lblPresetHint;
        private TextBox _txtCustom;

        public ShowcaseThemeEditorForm(ShowcaseVideoItem video)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Chủ đề" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(1120, 640);
            Padding = new Padding(32, 28, 32, 24);
            BuildUi();
            LoadFromVideo();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(0, 8, 0, 0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblPresets = MkSectionLabel("Preset góc quảng cáo");
            var lblCustom = MkSectionLabel("Chủ đề tùy chỉnh (ưu tiên hơn preset)");

            _lstPresets = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11F),
                IntegralHeight = false,
                Margin = new Padding(0, 12, 16, 0)
            };
            foreach (var preset in ShowcaseThemePresets.All)
            {
                _lstPresets.Items.Add(preset);
            }

            _lstPresets.DisplayMember = nameof(ShowcaseThemePreset.DisplayLabel);
            _lstPresets.SelectedIndexChanged += (_, __) => UpdatePresetHint();

            _lblPresetHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 72,
                AutoSize = false,
                ForeColor = Color.FromArgb(150, 158, 172),
                Font = new Font("Segoe UI", 10F),
                Padding = new Padding(0, 4, 0, 8),
                Text = "Chọn preset để xem gợi ý gửi Gemini."
            };

            _txtCustom = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11F),
                AcceptsReturn = false,
                Margin = new Padding(0, 0, 0, 8)
            };

            var hintCustom = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                AutoSize = false,
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                Text = "Ví dụ: Back to school, Quà 8/3. Có text tùy chỉnh → dùng text; không thì dùng preset đang chọn."
            };

            var pnlRight = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(8, 12, 0, 0)
            };
            pnlRight.Controls.Add(_txtCustom);
            pnlRight.Controls.Add(hintCustom);
            pnlRight.Controls.Add(_lblPresetHint);

            root.Controls.Add(lblPresets, 0, 0);
            root.Controls.Add(lblCustom, 1, 0);
            root.Controls.Add(_lstPresets, 0, 1);
            root.Controls.Add(pnlRight, 1, 1);

            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 0)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);

            Controls.Add(flpButtons);
            Controls.Add(root);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };
        }

        private void LoadFromVideo()
        {
            var custom = (_video.ShowcaseUserTheme ?? string.Empty).Trim();
            _txtCustom.Text = custom;

            var presetIndex = ShowcaseThemePresets.FindIndexByPrompt(_video.ShowcaseThemePrompt);
            if (string.IsNullOrWhiteSpace(custom) && presetIndex >= 0 && presetIndex < _lstPresets.Items.Count)
            {
                _lstPresets.SelectedIndex = presetIndex;
            }
            else if (_lstPresets.Items.Count > 0 && _lstPresets.SelectedIndex < 0)
            {
                _lstPresets.SelectedIndex = 0;
            }

            UpdatePresetHint();
        }

        private void UpdatePresetHint()
        {
            if (_lstPresets.SelectedItem is ShowcaseThemePreset preset)
            {
                var hint = (preset.PromptHint ?? string.Empty).Trim();
                _lblPresetHint.Text = string.IsNullOrEmpty(hint)
                    ? preset.DisplayLabel + " — Gemini tự suy chủ đề."
                    : hint;
            }
        }

        private bool ValidateAndSave()
        {
            var custom = _txtCustom.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(custom))
            {
                ShowcaseThemePresets.ApplyCombinedInput(_video, custom);
                return true;
            }

            if (_lstPresets.SelectedItem is ShowcaseThemePreset preset)
            {
                ShowcaseThemePresets.ApplyCombinedInput(_video, preset.DisplayLabel);
                return true;
            }

            ShowcaseThemePresets.ApplyCombinedInput(_video, string.Empty);
            return true;
        }

        private static Label MkSectionLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(190, 198, 212),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Padding = new Padding(0, 6, 0, 0),
            Margin = new Padding(0, 0, 0, 12)
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
