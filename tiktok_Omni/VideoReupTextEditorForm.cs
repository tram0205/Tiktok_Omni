using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>Hộp thoại sửa văn bản dài (hook, hashtag) cho một dòng Video reup.</summary>
    internal sealed class VideoReupTextEditorForm : Form
    {
        private readonly TextBox _txt;
        private readonly string _hint;

        public string EditedText => (_txt.Text ?? string.Empty).Trim();

        public VideoReupTextEditorForm(string title, string initialText, string hint, int minHeight = 220)
        {
            Text = title ?? "Sửa nội dung";
            _hint = hint ?? string.Empty;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);
            MinimumSize = new Size(480, minHeight);
            ClientSize = new Size(560, minHeight);

            var lblHint = new Label
            {
                Text = _hint,
                Dock = DockStyle.Top,
                Height = string.IsNullOrWhiteSpace(_hint) ? 0 : 36,
                ForeColor = Color.FromArgb(150, 158, 172),
                Padding = new Padding(0, 0, 0, 6)
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
            AcceptButton = btnOk;

            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 6, 0, 0)
            };
            flp.Controls.Add(btnCancel);
            flp.Controls.Add(btnOk);

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 4) };
            host.Controls.Add(_txt);
            if (!string.IsNullOrWhiteSpace(_hint))
            {
                host.Controls.Add(lblHint);
            }

            Controls.Add(host);
            Controls.Add(flp);

            Shown += (_, __) => _txt.Focus();
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
