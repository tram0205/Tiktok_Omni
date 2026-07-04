using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>
    /// Pop-up chọn ngày/giờ đăng cho một dòng lịch AutoPost.
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
            Text            = title;
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ClientSize      = new Size(360, 200);
            BackColor       = Color.FromArgb(28, 31, 38);
            ForeColor       = Color.FromArgb(210, 215, 225);

            var lblTitle = new Label
            {
                Text      = "Ngày / giờ đăng bài",
                Dock      = DockStyle.Top,
                Height    = 28,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
                Padding   = new Padding(12, 8, 0, 0),
            };

            _chkPostNow = new CheckBox
            {
                Text      = "Đăng ngay (không hẹn giờ)",
                AutoSize  = true,
                Checked   = !current.HasValue,
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(200, 205, 215),
                Location  = new Point(16, 44),
            };
            _chkPostNow.CheckedChanged += (_, __) => _dtp.Enabled = !_chkPostNow.Checked;

            _dtp = new DateTimePicker
            {
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy   HH:mm",
                ShowUpDown   = true,
                Location     = new Point(16, 76),
                Size         = new Size(328, 28),
                Font         = new Font("Segoe UI", 10f),
                BackColor    = Color.FromArgb(38, 42, 52),
                ForeColor    = Color.WhiteSmoke,
                CalendarForeColor = Color.WhiteSmoke,
                CalendarMonthBackground = Color.FromArgb(38, 42, 52),
                Value        = current ?? DateTime.Now.AddHours(1),
                Enabled      = current.HasValue,
            };

            _lblHint = new Label
            {
                Text      = "Dùng mũi tên ▲▼ để chỉnh ngày, tháng, năm, giờ, phút.",
                Location  = new Point(16, 112),
                Size      = new Size(328, 36),
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 150, 165),
            };

            var btnSave = new Button
            {
                Text      = "Lưu",
                DialogResult = DialogResult.OK,
                Location  = new Point(168, 156),
                Size      = new Size(84, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 130, 80),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 110);
            btnSave.Click += (_, __) => CommitAndClose();

            var btnCancel = new Button
            {
                Text         = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location     = new Point(260, 156),
                Size         = new Size(84, 32),
                FlatStyle    = FlatStyle.Flat,
                BackColor    = Color.FromArgb(70, 50, 55),
                ForeColor    = Color.FromArgb(220, 200, 200),
                Font         = new Font("Segoe UI", 9f),
                Cursor       = Cursors.Hand,
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(120, 70, 80);

            Controls.Add(lblTitle);
            Controls.Add(_chkPostNow);
            Controls.Add(_dtp);
            Controls.Add(_lblHint);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);

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
