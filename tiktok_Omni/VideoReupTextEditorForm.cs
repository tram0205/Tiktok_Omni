using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>Hộp thoại sửa văn bản dài (hook, hashtag) cho một dòng Video reup.</summary>
    internal sealed class VideoReupTextEditorForm : Form
    {
        private readonly TextBox _txt;

        public string EditedText => (_txt.Text ?? string.Empty).Trim();

        public VideoReupTextEditorForm(string title, string initialText, string hint, int minHeight = 220)
        {
            Text = title ?? "Sửa nội dung";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);

            const int footerHeight = 56;
            const int hintHeight = 40;
            const int padding = 12;
            var textAreaHeight = Math.Max(minHeight, 180);
            var clientHeight = padding * 2 + hintHeight + textAreaHeight + footerHeight;

            ClientSize = new Size(640, clientHeight);
            MinimumSize = new Size(
                520,
                clientHeight + SystemInformation.CaptionHeight + SystemInformation.FixedFrameBorderSize.Height * 2);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(padding)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, footerHeight));

            var lblHint = new Label
            {
                Text = hint ?? string.Empty,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(150, 158, 172),
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 0, 0, 8),
                Height = string.IsNullOrWhiteSpace(hint) ? 0 : hintHeight
            };

            _txt = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 30, 38),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Text = initialText ?? string.Empty,
                AcceptsReturn = true,
                AcceptsTab = false,
                Font = new Font("Segoe UI", 10.5F)
            };

            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;

            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;

            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 10, 0, 4)
            };
            flp.Controls.Add(btnCancel);
            flp.Controls.Add(btnOk);

            layout.Controls.Add(lblHint, 0, 0);
            layout.Controls.Add(_txt, 0, 1);
            layout.Controls.Add(flp, 0, 2);

            Controls.Add(layout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Shown += (_, __) => _txt.Focus();
        }

        private static Button CreateButton(string text, Color back)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(92, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(8, 0, 0, 0)
            };
        }
    }
}
