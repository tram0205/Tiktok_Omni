using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseZoomAspectMismatchInfo
    {
        public ShowcaseZoomAspectMismatchInfo(int sceneOrder, int imageWidth, int imageHeight)
        {
            SceneOrder = sceneOrder;
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
        }

        public int SceneOrder { get; }

        public int ImageWidth { get; }

        public int ImageHeight { get; }
    }

    internal sealed class ShowcaseZoomAspectFitDialog : Form
    {
        private readonly RadioButton _rbCrop;
        private readonly RadioButton _rbBlur;

        public ShowcaseZoomAspectFitMode SelectedMode { get; private set; } = ShowcaseZoomAspectFitMode.Crop;

        public ShowcaseZoomAspectFitDialog(
            string productName,
            ShowcaseOutputAspectPreset canvas,
            IReadOnlyList<ShowcaseZoomAspectMismatchInfo> mismatches)
        {
            canvas = canvas ?? ShowcaseOutputAspectPresets.Vertical9x16;
            mismatches = mismatches ?? Array.Empty<ShowcaseZoomAspectMismatchInfo>();

            var product = (productName ?? string.Empty).Trim();
            Text = "Ảnh không khớp khung video";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(1120, 640);
            BackColor = Color.FromArgb(28, 31, 38);
            ForeColor = Color.FromArgb(210, 215, 225);
            Padding = new Padding(48, 36, 48, 36);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));

            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
                Text = product.Length > 0
                    ? "«" + product + "» — " + mismatches.Count + " ảnh Zoom lệch khung " + canvas.DisplayLabel
                    : mismatches.Count + " ảnh Zoom lệch khung " + canvas.DisplayLabel,
                Margin = new Padding(0, 0, 0, 16),
                MaximumSize = new Size(1160, 0)
            };

            var details = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(22, 25, 32),
                ForeColor = Color.FromArgb(170, 178, 192),
                Font = new Font("Segoe UI", 9.5F),
                Dock = DockStyle.Fill,
                Text = BuildDetailsText(canvas, mismatches)
            };

            _rbCrop = CreateOption(
                "Cắt (cover) — phóng to ảnh cho kín khung, có thể mất mép trái/phải hoặc trên/dưới.",
                true);
            _rbBlur = CreateOption(
                "Blur nền — giữ toàn bộ ảnh ở giữa, viền thừa là nền mờ từ chính ảnh.",
                false);

            var options = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 8, 0, 8)
            };
            options.Controls.Add(_rbCrop);
            options.Controls.Add(_rbBlur);

            var btnOk = new Button
            {
                Text = "Tiếp tục",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(240, 72),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 120, 82),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 10, 0)
            };
            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(200, 72),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(90, 96, 110),
                ForeColor = Color.White
            };
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            btnOk.Click += (_, __) =>
            {
                SelectedMode = _rbBlur.Checked
                    ? ShowcaseZoomAspectFitMode.BlurPad
                    : ShowcaseZoomAspectFitMode.Crop;
            };

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            footer.Controls.Add(btnCancel);
            footer.Controls.Add(btnOk);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(details, 0, 1);
            layout.Controls.Add(options, 0, 2);
            layout.Controls.Add(footer, 0, 3);
            Controls.Add(layout);
        }

        private static string BuildDetailsText(
            ShowcaseOutputAspectPreset canvas,
            IReadOnlyList<ShowcaseZoomAspectMismatchInfo> mismatches)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Khung xuất: " + canvas.DisplayLabel + " (" + canvas.Width + "×" + canvas.Height + ").");
            sb.AppendLine("Chọn cách xử lý cho tất cả ảnh lệch trong lần tạo clip này:");
            sb.AppendLine();

            for (var i = 0; i < mismatches.Count; i++)
            {
                var item = mismatches[i];
                sb.Append("• Cảnh ");
                sb.Append(item.SceneOrder);
                sb.Append(": ");
                sb.Append(ShowcaseZoomAspectFitHelper.FormatAspectRatio(item.ImageWidth, item.ImageHeight));
                if (i < mismatches.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static RadioButton CreateOption(string text, bool selected)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                Checked = selected,
                ForeColor = Color.FromArgb(210, 215, 225),
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(0, 0, 0, 20),
                MaximumSize = new Size(1160, 0)
            };
        }
    }
}
