using System;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseSubtitleStyleEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;
        private readonly AppSettings _defaults;
        private readonly bool _philosophyMode;

        public ShowcaseSubtitleStyleEditorForm(ShowcaseVideoItem video, AppSettings defaults, bool philosophyMode = false)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _defaults = defaults ?? new AppSettings();
            _philosophyMode = philosophyMode;
            ShowcaseSubtitleStyleHelper.EnsureVideoDefaults(_video, _defaults);
            if (_philosophyMode)
            {
                _video.ShowcaseHookSubtitleEnabled = false;
                if (!_video.ShowcaseSubtitleEnabled)
                {
                    _video.ShowcaseSubtitleEnabled = true;
                }
            }

            Text = _philosophyMode
                ? "Phụ đề — Quote"
                  + ((_video.ProductName ?? string.Empty).Trim().Length > 0 ? " · " + _video.ProductName.Trim() : string.Empty)
                : "Phụ đề — " + ((_video.ProductName ?? string.Empty).Trim().Length > 0
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
            ClientSize = new Size(2310, 1476);
            Padding = new Padding(28, 12, 28, 8);
            MinimumSize = new Size(2220, 1440);
            BuildUi();
            BuildDisplayTabUi();
            LoadDisplayFromVideo();
        }

        private void BuildUi()
        {
            var btnPreviewEffect = CreateButton("Xem thử hiệu ứng", Color.FromArgb(72, 98, 140));
            btnPreviewEffect.Click += (_, __) => PreviewSelectedRowDisplayEffect();
            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var footerInset = (int)Math.Max(8, Math.Round(Font.Size * 0.85));
            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnPreviewEffect);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor,
                Padding = new Padding(0, footerInset / 2, 0, footerInset)
            };
            footer.Controls.Add(flpButtons);

            _displayTabHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(4)
            };

            Controls.Add(_displayTabHost);
            Controls.Add(footer);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };
        }

        private bool ValidateAndSave()
        {
            if (!SaveStyleFromGridToVideo())
            {
                return false;
            }

            SaveDisplayToVideo();
            ShowcaseSubtitleStyleHelper.RefreshStyleLabel(_video);
            return true;
        }

        private Button CreateButton(string text, Color back)
        {
            var minH = MeasureSingleLineControlHeight(Font, 10);
            var padV = (int)Math.Max(8, Math.Round(Font.Size * 0.85));
            var padH = (int)Math.Max(14, Math.Round(Font.Size * 1.25));
            var minW = (int)Math.Max(140, Math.Round(Font.Size * 12.5));
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minW, minH),
                Padding = new Padding(padH, padV, padH, padV),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Regular, Font.Unit),
                Margin = new Padding(12, 0, 0, 0)
            };
        }

        private static int MeasureSingleLineControlHeight(Font font, int verticalPadEachSide)
        {
            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            var textH = TextRenderer.MeasureText("Áy", font, Size.Empty, flags).Height;
            return textH + verticalPadEachSide * 2 + 8;
        }
    }
}
