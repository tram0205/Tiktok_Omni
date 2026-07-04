using System;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>
    /// Pop-up editor for Caption / Hashtag / YouTube fields of a single <see cref="ScheduleEntry"/>.
    /// Opens from the [✏ Sửa] button column in the AutoPost schedule grids.
    /// Saves back to the entry in-place; caller should call DataGridView.InvalidateRow() afterwards.
    /// </summary>
    public sealed class FormCaptionEditor : Form
    {
        // ── backing data ──────────────────────────────────────────────────────
        private readonly ScheduleEntry _entry;
        private readonly bool _isYouTube;

        // ── controls ──────────────────────────────────────────────────────────
        private RichTextBox _rtbCaption;   // TikTok/FB caption  OR  YT description
        private TextBox     _tbHashtag;    // TikTok/FB hashtag  OR  YT title
        private Label       _lblMain;
        private Label       _lblSub;

        // ── constructor ───────────────────────────────────────────────────────
        public FormCaptionEditor(ScheduleEntry entry, string platform)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _entry     = entry;
            _isYouTube = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase);

            Text            = $"Chỉnh sửa nội dung — {platform}";
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(620, 440);
            MinimumSize     = new Size(480, 360);
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor       = Color.FromArgb(28, 31, 38);
            ForeColor       = Color.FromArgb(200, 205, 215);

            BuildUi(platform);
            LoadData();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI CONSTRUCTION
        // ─────────────────────────────────────────────────────────────────────

        private static Label MakeLabel(string text) => new Label
        {
            Text      = text,
            AutoSize  = true,
            ForeColor = Color.FromArgb(160, 165, 180),
            Font      = new Font("Segoe UI", 9f, FontStyle.Regular),
            Margin    = new Padding(0, 0, 0, 2),
        };

        private static TextBox MakeTextBox() => new TextBox
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(38, 42, 52),
            ForeColor = Color.FromArgb(220, 225, 235),
            Font      = new Font("Segoe UI", 9.5f),
            BorderStyle = BorderStyle.FixedSingle,
        };

        private static RichTextBox MakeRtb() => new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = Color.FromArgb(38, 42, 52),
            ForeColor   = Color.FromArgb(220, 225, 235),
            Font        = new Font("Segoe UI", 9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
        };

        private static Button MakeToolBtn(string text, Color accent) => new Button
        {
            Text      = text,
            Height    = 28,
            AutoSize  = true,
            Padding   = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = accent,
            ForeColor = Color.White,
            Cursor    = Cursors.Hand,
        };

        private void BuildUi(string platform)
        {
            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 5,
                Padding     = new Padding(12),
                BackColor   = Color.FromArgb(28, 31, 38),
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // platform label
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // sub-label (caption or title)
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));  // main RTB
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // secondary label
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));  // hashtag / title textbox

            // Row 0 — platform tag + formatting toolbar
            var topBar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize      = true,
                WrapContents  = false,
                BackColor     = Color.Transparent,
                Margin        = new Padding(0, 0, 0, 8),
            };

            var lblPlatform = new Label
            {
                Text      = $"[ {platform.ToUpper()} ]",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = GetPlatformColor(platform),
                Margin    = new Padding(0, 4, 12, 0),
            };
            topBar.Controls.Add(lblPlatform);

            var btnBold = MakeToolBtn("B  In đậm", Color.FromArgb(70, 80, 105));
            btnBold.Click += (_, __) => ApplyBold();
            btnBold.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 130);
            topBar.Controls.Add(btnBold);

            var btnUpper = MakeToolBtn("⬆ Viết hoa", Color.FromArgb(60, 100, 90));
            btnUpper.Click += (_, __) => ApplyUpperCase();
            btnUpper.FlatAppearance.BorderColor = Color.FromArgb(80, 130, 110);
            topBar.Controls.Add(btnUpper);

            // Spacer
            var spacer = new Panel { Width = 12, Height = 1, BackColor = Color.Transparent };
            topBar.Controls.Add(spacer);

            var btnSave = MakeToolBtn("💾 Lưu", Color.FromArgb(40, 130, 80));
            btnSave.Click += (_, __) => SaveAndClose();
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 110);
            topBar.Controls.Add(btnSave);

            var btnCancel = MakeToolBtn("✕ Hủy", Color.FromArgb(90, 50, 60));
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(130, 70, 80);
            topBar.Controls.Add(btnCancel);

            outer.Controls.Add(topBar, 0, 0);

            // Row 1 — main-field label
            if (_isYouTube)
            {
                _lblMain = MakeLabel("Mô tả SEO (YouTube):");
                _lblSub  = MakeLabel("Tiêu đề (tối đa 60 ký tự):");
            }
            else
            {
                _lblMain = MakeLabel($"Caption ({platform}):");
                _lblSub  = MakeLabel("Hashtag (cách nhau bằng dấu cách):");
            }
            outer.Controls.Add(_lblMain, 0, 1);

            // Row 2 — main RTB (caption for TikTok/FB, yt_desc for YouTube)
            _rtbCaption = MakeRtb();
            outer.Controls.Add(_rtbCaption, 0, 2);

            // Row 3 — secondary label
            outer.Controls.Add(_lblSub, 0, 3);

            // Row 4 — hashtag textbox (TikTok/FB) or yt_title textbox (YouTube)
            _tbHashtag = MakeTextBox();
            if (_isYouTube)
            {
                _tbHashtag.MaxLength = 60;
                _tbHashtag.TextChanged += (_, __) =>
                {
                    if (_tbHashtag.Text.Length > 58)
                        _tbHashtag.ForeColor = Color.FromArgb(255, 130, 80);
                    else
                        _tbHashtag.ForeColor = Color.FromArgb(220, 225, 235);
                };
            }
            outer.Controls.Add(_tbHashtag, 0, 4);

            Controls.Add(outer);

            // Accept / Cancel keyboard shortcuts
            AcceptButton = null;  // prevent accidental Enter-save while editing
            KeyPreview   = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                if (e.Control && e.KeyCode == Keys.Enter) SaveAndClose();
            };
        }

        private static Color GetPlatformColor(string platform)
        {
            switch (platform)
            {
                case "TikTok":   return Color.FromArgb(100, 200, 220);
                case "Facebook": return Color.FromArgb(100, 140, 255);
                case "YouTube":  return Color.FromArgb(255, 90, 80);
                default:         return Color.FromArgb(200, 200, 200);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DATA LOAD / SAVE
        // ─────────────────────────────────────────────────────────────────────

        private void LoadData()
        {
            if (_isYouTube)
            {
                _rtbCaption.Text = _entry.YtDescription;
                _tbHashtag.Text  = _entry.YtTitle;
            }
            else
            {
                _rtbCaption.Text = _entry.Caption;
                _tbHashtag.Text  = _entry.Hashtag;
            }
        }

        private void SaveAndClose()
        {
            if (_isYouTube)
            {
                _entry.YtDescription = _rtbCaption.Text.Trim();
                _entry.YtTitle       = _tbHashtag.Text.Trim();
            }
            else
            {
                _entry.Caption  = _rtbCaption.Text.Trim();
                _entry.Hashtag  = _tbHashtag.Text.Trim();
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FORMATTING ACTIONS (act on the focused RTB first, then the TextBox)
        // ─────────────────────────────────────────────────────────────────────

        private RichTextBox ActiveRtb =>
            _rtbCaption.Focused ? _rtbCaption : _rtbCaption; // extend if more RTBs are added

        /// <summary>
        /// Wraps the selected text in **…** bold markers (common for social copy-paste tools).
        /// If nothing is selected, wraps the entire content.
        /// </summary>
        private void ApplyBold()
        {
            var rtb = _rtbCaption;
            if (rtb.SelectionLength > 0)
            {
                var sel = rtb.SelectedText;
                rtb.SelectedText = $"**{sel}**";
            }
            else
            {
                rtb.Text = $"**{rtb.Text}**";
                rtb.SelectionStart = rtb.TextLength;
            }
        }

        /// <summary>
        /// Uppercases the selected text in the currently focused control.
        /// If nothing is selected, uppercases the full content.
        /// </summary>
        private void ApplyUpperCase()
        {
            // Try the caption RTB first, fall back to the hashtag textbox
            if (_rtbCaption.Focused || _rtbCaption.SelectionLength > 0)
            {
                if (_rtbCaption.SelectionLength > 0)
                    _rtbCaption.SelectedText = _rtbCaption.SelectedText.ToUpperInvariant();
                else
                    _rtbCaption.Text = _rtbCaption.Text.ToUpperInvariant();
            }
            else if (_tbHashtag.Focused)
            {
                if (_tbHashtag.SelectionLength > 0)
                    _tbHashtag.SelectedText = _tbHashtag.SelectedText.ToUpperInvariant();
                else
                    _tbHashtag.Text = _tbHashtag.Text.ToUpperInvariant();
            }
            else
            {
                // Default: uppercase the caption
                _rtbCaption.Text = _rtbCaption.Text.ToUpperInvariant();
            }
        }
    }
}
