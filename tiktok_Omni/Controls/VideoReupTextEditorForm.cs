using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni.Controls
{
    internal sealed class VideoReupTextEditorForm : Form
    {
        private readonly TextBox _textBox;

        public string EditedText { get; private set; } = string.Empty;

        public VideoReupTextEditorForm(string title, string initialText, string hint, int minHeight = 240)
        {
            Text = title ?? "Sửa nội dung";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            Size = new Size(680, Math.Max(minHeight + 120, 420));
            MinimumSize = new Size(480, Math.Max(minHeight + 100, 320));
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

            var lblHint = new Label
            {
                Text = hint ?? string.Empty,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(150, 158, 172),
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 0, 0, 8),
                Height = 36
            };

            _textBox = new TextBox
            {
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Text = initialText ?? string.Empty,
                BackColor = Color.FromArgb(22, 24, 30),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                AcceptsReturn = true,
                AcceptsTab = false
            };

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0),
                WrapContents = false
            };

            var btnCancel = new Button
            {
                Text = "Hủy",
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(50, 54, 65),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 4, 0, 4)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 74, 88);

            var btnOk = new Button
            {
                Text = "Lưu",
                AutoSize = true,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(56, 110, 165),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 4, 0, 4)
            };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(70, 120, 175);
            btnOk.Click += (_, __) => EditedText = (_textBox.Text ?? string.Empty).Trim();

            bottom.Controls.Add(btnCancel);
            bottom.Controls.Add(btnOk);

            layout.Controls.Add(lblHint, 0, 0);
            layout.Controls.Add(_textBox, 0, 1);
            layout.Controls.Add(bottom, 0, 2);

            Controls.Add(layout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Shown += (_, __) => _textBox.Focus();
        }
    }
}
