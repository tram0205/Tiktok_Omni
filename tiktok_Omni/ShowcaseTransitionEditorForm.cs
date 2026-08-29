using System;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseTransitionEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;
        private readonly AppSettings _settings;

        private NumericUpDown _numSeconds;
        private Label _lblPreview;

        public ShowcaseTransitionEditorForm(ShowcaseVideoItem video, AppSettings settings)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _settings = settings ?? new AppSettings();
            ShowcaseTransitionHelper.EnsureVideoDefaults(_video, _settings);

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Chuyển cảnh" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(760, 348);
            Padding = new Padding(24, 20, 24, 16);
            BuildUi();
            LoadFromVideo();
        }

        private void BuildUi()
        {
            var btnDefaults = CreateButton("Mặc định app", Color.FromArgb(70, 78, 96));
            btnDefaults.Click += (_, __) =>
            {
                _numSeconds.Value = (decimal)ShowcaseTransitionHelper.ClampSeconds(_settings?.VideoTransitionDurationSeconds ?? 0.6d);
                UpdatePreview();
            };

            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 0)
            };
            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnDefaults);
            btnBar.Controls.Add(flpButtons);

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = BackColor,
                Width = ClientSize.Width - Padding.Horizontal
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));

            Label MkLbl(string text, ContentAlignment align = ContentAlignment.MiddleLeft) => new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = align,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 0, 16, 0),
                Font = new Font("Segoe UI", 10.5F)
            };

            _numSeconds = new NumericUpDown
            {
                Width = 112,
                Height = 34,
                DecimalPlaces = 1,
                Increment = 0.1M,
                Minimum = 0.2M,
                Maximum = 2.0M,
                Value = 0.6M,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5F),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _numSeconds.ValueChanged += (_, __) => UpdatePreview();

            var numHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, 10, 0, 10)
            };
            numHost.Controls.Add(_numSeconds);

            _lblPreview = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(tbl.Width - 152, 0),
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 6, 0, 0)
            };

            var flpPresets = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 10, 0, 10)
            };
            flpPresets.Controls.Add(CreatePresetButton("Nhanh (0.3s)", 0.3));
            flpPresets.Controls.Add(CreatePresetButton("Vừa (0.6s)", 0.6));
            flpPresets.Controls.Add(CreatePresetButton("Chậm (1.0s)", 1.0));

            var presetHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, 8, 0, 8)
            };
            presetHost.Controls.Add(flpPresets);
            flpPresets.Location = new Point(0, 0);

            var descHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, 8, 0, 0)
            };
            descHost.Controls.Add(_lblPreview);
            _lblPreview.Location = new Point(0, 0);
            _lblPreview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            tbl.Controls.Add(MkLbl("Thời gian"), 0, 0);
            tbl.Controls.Add(numHost, 1, 0);
            tbl.Controls.Add(MkLbl("Gợi ý"), 0, 1);
            tbl.Controls.Add(presetHost, 1, 1);
            tbl.Controls.Add(MkLbl("Mô tả", ContentAlignment.TopLeft), 0, 2);
            tbl.Controls.Add(descHost, 1, 2);

            Controls.Add(tbl);
            Controls.Add(btnBar);

            Load += (_, __) =>
            {
                var w = ClientSize.Width - Padding.Horizontal;
                tbl.Width = w;
                _lblPreview.MaximumSize = new Size(Math.Max(240, w - 152), 0);
            };

            btnOk.Click += (_, __) =>
            {
                _video.ShowcaseTransitionSeconds = ShowcaseTransitionHelper.ClampSeconds((double)_numSeconds.Value);
                ShowcaseTransitionHelper.RefreshTransitionLabel(_video);
            };
        }

        private Button CreatePresetButton(string text, double seconds)
        {
            var btn = CreateButton(text, Color.FromArgb(58, 66, 82));
            btn.AutoSize = false;
            btn.Size = new Size(148, 40);
            btn.Margin = new Padding(0, 0, 12, 0);
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.Padding = new Padding(8, 6, 8, 6);
            btn.Click += (_, __) =>
            {
                _numSeconds.Value = (decimal)ShowcaseTransitionHelper.ClampSeconds(seconds);
                UpdatePreview();
            };
            return btn;
        }

        private void LoadFromVideo()
        {
            _numSeconds.Value = (decimal)ShowcaseTransitionHelper.ClampSeconds(_video.ShowcaseTransitionSeconds);
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var seconds = ShowcaseTransitionHelper.ClampSeconds((double)_numSeconds.Value);
            _lblPreview.Text =
                "Thời gian crossfade khi ghép clip Veo.\r\n" +
                ShowcaseTransitionHelper.FormatTransitionSummary(seconds) +
                " — ngắn hơn = cắt nhanh, dài hơn = mượt hơn.";
        }

        private static Button CreateButton(string text, Color back) => new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(120, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(10, 0, 0, 0),
            Padding = new Padding(12, 6, 12, 6)
        };
    }
}
