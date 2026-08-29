using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tiktok_Omni
{
    internal sealed class ShowcaseSubtitleEffectPreviewForm : Form
    {
        private readonly Func<CancellationToken, Task<string>> _renderAsync;
        private readonly string _lineLabel;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Label _status;
        private Button _btnReplay;
        private Button _btnOpenFolder;
        private string _clipPath;

        public ShowcaseSubtitleEffectPreviewForm(string lineLabel, Func<CancellationToken, Task<string>> renderAsync)
        {
            _lineLabel = (lineLabel ?? string.Empty).Trim();
            _renderAsync = renderAsync ?? throw new ArgumentNullException(nameof(renderAsync));

            Text = "Xem thử hiệu ứng phụ đề";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            MinimumSize = new Size(560, 280);
            ClientSize = new Size(680, 320);
            Padding = new Padding(0, 0, 0, 4);
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);

            var edge = (int)Math.Max(12, Math.Round(Font.Size * 1.05));

            var intro = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = (int)Math.Max(64, Math.Round(Font.Size * 6.2)),
                Padding = new Padding(edge, edge, edge, 6),
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Regular, Font.Unit),
                Text = string.IsNullOrEmpty(_lineLabel)
                    ? "Đang tạo clip ~ vài giây (1080×1920, nền tối)…"
                    : "Dòng: " + _lineLabel + "\nClip ngắn — hiệu ứng giống khi render."
            };

            _status = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(edge, 10, edge, 10),
                ForeColor = Color.WhiteSmoke,
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Regular, Font.Unit),
                Text = "Đang dựng ASS và FFmpeg…"
            };

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor,
                Padding = new Padding(edge, edge / 2, edge, edge)
            };

            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0),
                BackColor = BackColor
            };

            var btnClose = MakeFooterButton("Đóng", Color.FromArgb(90, 96, 110));
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Click += (_, __) => Close();

            _btnReplay = MakeFooterButton("Phát lại", Color.FromArgb(56, 120, 82), bold: true);
            _btnReplay.Enabled = false;
            _btnReplay.Margin = new Padding(12, 0, 0, 0);
            _btnReplay.Click += (_, __) => Services.Showcase.ShowcaseSubtitleDisplayEffectPreviewHelper.TryOpenInDefaultPlayer(_clipPath);

            _btnOpenFolder = MakeFooterButton("Thư mục clip", Color.FromArgb(70, 78, 96));
            _btnOpenFolder.Enabled = false;
            _btnOpenFolder.Margin = new Padding(12, 0, 0, 0);
            _btnOpenFolder.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_clipPath))
                {
                    return;
                }

                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + _clipPath + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            flp.Controls.Add(btnClose);
            flp.Controls.Add(_btnReplay);
            flp.Controls.Add(_btnOpenFolder);
            footer.Controls.Add(flp);

            Controls.Add(_status);
            Controls.Add(footer);
            Controls.Add(intro);

            CancelButton = btnClose;
            Shown += async (_, __) => await RunRenderAsync().ConfigureAwait(true);
            FormClosed += (_, __) => _cts.Cancel();
        }

        private Button MakeFooterButton(string text, Color back, bool bold = false)
        {
            var padV = (int)Math.Max(10, Math.Round(Font.Size * 0.9));
            var padH = (int)Math.Max(18, Math.Round(Font.Size * 1.45));
            var minH = MeasureComfortButtonHeight(Font);
            var minW = (int)Math.Max(108, Math.Round(Font.Size * 12));
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minW, minH),
                Padding = new Padding(padH, padV, padH, padV),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, Font.Size, bold ? FontStyle.Bold : FontStyle.Regular, Font.Unit)
            };
        }

        private static int MeasureComfortButtonHeight(Font font)
        {
            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            var textH = TextRenderer.MeasureText("Áy", font, Size.Empty, flags).Height;
            return textH + 28;
        }

        private async Task RunRenderAsync()
        {
            try
            {
                _clipPath = await _renderAsync(_cts.Token).ConfigureAwait(true);
                _status.Text = "Xong. Clip đã mở trong trình phát mặc định.\n" + _clipPath;
                _btnReplay.Enabled = true;
                _btnOpenFolder.Enabled = true;
                Services.Showcase.ShowcaseSubtitleDisplayEffectPreviewHelper.TryOpenInDefaultPlayer(_clipPath);
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Đã hủy.";
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
            }
        }
    }
}
