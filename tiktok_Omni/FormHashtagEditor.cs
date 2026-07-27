using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>
    /// Lightweight form for manually editing the hashtag string for one row.
    /// </summary>
    public sealed class FormHashtagEditor : Form
    {
        public string ResultHashtag { get; private set; } = string.Empty;

        private readonly TextBox _tb;

        private static readonly Dictionary<string, string> PlatformHints = new Dictionary<string, string>
        {
            { "TikTok",   "VD: #BaGiao #aodaitrang #xuhuong #review #thoitrang" },
            { "Facebook", "VD: #BaGiao #aodaitrang #shopeehaul #meovat #fashion" },
            { "YouTube",  "VD: #BaGiao #aodaitrang #aotrend2025 #shorts" },
        };

        public FormHashtagEditor(string current, string platform)
        {
            Text            = $"Sửa Hashtag — {platform}";
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            ClientSize      = new Size(1000, 480);
            BackColor       = Color.FromArgb(28, 31, 38);
            Font            = new Font("Segoe UI", 10.5F);
            Padding         = new Padding(20);

            var hint = PlatformHints.TryGetValue(platform, out var h)
                ? h : "VD: #kênh #sảnphẩm #trend";

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = BackColor
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            tbl.Controls.Add(new Label
            {
                Text = "Nhập hashtag, cách nhau bằng dấu cách. KHÔNG DẤU tiếng Việt.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            tbl.Controls.Add(new Label
            {
                Text = hint,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(120, 130, 145),
                Margin = new Padding(0, 0, 0, 10)
            }, 0, 1);

            _tb = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Text = current ?? string.Empty,
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.FromArgb(220, 225, 235),
                Font = new Font("Segoe UI", 11F),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 8)
            };
            tbl.Controls.Add(_tb, 0, 2);

            var lblCount = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(110, 120, 135),
                Text = $"{_tb.Text.Length} ký tự",
                Margin = new Padding(0, 0, 0, 8)
            };
            _tb.TextChanged += (_, __) => lblCount.Text = $"{_tb.Text.Length} ký tự";
            tbl.Controls.Add(lblCount, 0, 3);

            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = BackColor
            };

            var btnCancel = new Button
            {
                Text = "Hủy",
                AutoSize = true,
                MinimumSize = new Size(112, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 50, 55),
                ForeColor = Color.FromArgb(220, 200, 200),
                Font = new Font("Segoe UI", 10.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(120, 70, 80);
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            var btnSave = new Button
            {
                Text = "💾 Lưu",
                AutoSize = true,
                MinimumSize = new Size(112, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 130, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = Padding.Empty
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 110);
            btnSave.Click += (_, __) => { ResultHashtag = _tb.Text.Trim(); DialogResult = DialogResult.OK; Close(); };

            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnSave);
            tbl.Controls.Add(flpButtons, 0, 4);

            Controls.Add(tbl);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }
    }
}
