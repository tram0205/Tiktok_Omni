using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>
    /// Pop-up chọn ngày/giờ đăng cho một dòng lịch AutoPost / warm-up queue.
    /// </summary>
    public sealed class FormSchedulePicker : Form
    {
        private readonly CheckBox _chkPostNow;
        private readonly DateTimePicker _dtp;
        private readonly Label _lblHint;

        public DateTime? ResultScheduledAt { get; private set; }

        public FormSchedulePicker(DateTime? current, string videoLabel = null)
        {
            var title = string.IsNullOrWhiteSpace(videoLabel)
                ? "Chọn giờ đăng"
                : $"Giờ đăng — {videoLabel}";
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 248);
            MinimumSize = new Size(480, 248);
            BackColor = Color.FromArgb(28, 31, 38);
            ForeColor = Color.FromArgb(210, 215, 225);
            Padding = new Padding(20, 16, 20, 16);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle = new Label
            {
                Text = "Ngày / giờ đăng bài",
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
                Margin = new Padding(0, 0, 0, 12)
            };

            _chkPostNow = new CheckBox
            {
                Text = "Đăng ngay (không hẹn giờ)",
                AutoSize = true,
                Checked = !current.HasValue,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(200, 205, 215),
                Margin = new Padding(0, 0, 0, 10)
            };
            _chkPostNow.CheckedChanged += (_, __) => _dtp.Enabled = !_chkPostNow.Checked;

            _dtp = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy   HH:mm",
                ShowUpDown = true,
                Dock = DockStyle.Fill,
                Height = 32,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.WhiteSmoke,
                CalendarForeColor = Color.WhiteSmoke,
                CalendarMonthBackground = Color.FromArgb(38, 42, 52),
                Value = current ?? DateTime.Now.AddHours(1),
                Enabled = current.HasValue,
                Margin = new Padding(0, 0, 0, 8)
            };

            _lblHint = new Label
            {
                Text = "Dùng mũi tên ▲▼ để chỉnh ngày, tháng, năm, giờ, phút.",
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(140, 150, 165),
                Margin = new Padding(0, 0, 0, 16)
            };

            var btnSave = new Button
            {
                Text = "Lưu",
                DialogResult = DialogResult.OK,
                Size = new Size(104, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 130, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 110);
            btnSave.Click += (_, __) => CommitAndClose();

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Size = new Size(104, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 50, 55),
                ForeColor = Color.FromArgb(220, 200, 200),
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(120, 70, 80);

            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnSave);

            layout.Controls.Add(lblTitle, 0, 0);
            layout.Controls.Add(_chkPostNow, 0, 1);
            layout.Controls.Add(_dtp, 0, 2);
            layout.Controls.Add(_lblHint, 0, 3);
            layout.Controls.Add(btnPanel, 0, 4);

            Controls.Add(layout);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void CommitAndClose()
        {
            ResultScheduledAt = _chkPostNow.Checked ? (DateTime?)null : _dtp.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
