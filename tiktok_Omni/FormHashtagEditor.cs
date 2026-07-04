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
            ClientSize      = new Size(500, 220);
            BackColor       = Color.FromArgb(28, 31, 38);

            // Header
            Controls.Add(new Label
            {
                Text      = "Nhập hashtag, cách nhau bằng dấu cách. KHÔNG DẤU tiếng Việt.",
                Location  = new Point(14, 14),
                Size      = new Size(470, 20),
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
            });

            // Hint
            var hint = PlatformHints.TryGetValue(platform, out var h)
                ? h : "VD: #kênh #sảnphẩm #trend";
            Controls.Add(new Label
            {
                Text      = hint,
                Location  = new Point(14, 38),
                Size      = new Size(470, 18),
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(120, 130, 145),
            });

            // Textbox
            _tb = new TextBox
            {
                Location    = new Point(14, 62),
                Size        = new Size(470, 56),
                Multiline   = true,
                WordWrap    = true,
                ScrollBars  = ScrollBars.Vertical,
                Text        = current ?? string.Empty,
                BackColor   = Color.FromArgb(38, 42, 52),
                ForeColor   = Color.FromArgb(220, 225, 235),
                Font        = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
            };
            Controls.Add(_tb);

            // Char count hint
            var lblCount = new Label
            {
                Location  = new Point(14, 124),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(110, 120, 135),
                Text      = $"{_tb.Text.Length} ký tự",
            };
            _tb.TextChanged += (_, __) => lblCount.Text = $"{_tb.Text.Length} ký tự";
            Controls.Add(lblCount);

            // Buttons
            var btnSave = new Button
            {
                Text      = "💾 Lưu",
                Location  = new Point(296, 174),
                Size      = new Size(90, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 130, 80),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 110);
            btnSave.Click += (_, __) => { ResultHashtag = _tb.Text.Trim(); DialogResult = DialogResult.OK; Close(); };
            Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text      = "Hủy",
                Location  = new Point(396, 174),
                Size      = new Size(88, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 50, 55),
                ForeColor = Color.FromArgb(220, 200, 200),
                Font      = new Font("Segoe UI", 9f),
                Cursor    = Cursors.Hand,
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(120, 70, 80);
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }
    }
}
