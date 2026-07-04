using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>
    /// Displays 5 copywriting-style variants as radio-button panels.
    /// User selects one and clicks Lưu; chosen data is exposed via result properties.
    /// </summary>
    public sealed class FormCaptionSelector : Form
    {
        // ── Result ─────────────────────────────────────────────────────────────
        public string ChosenKey           { get; private set; } = string.Empty;
        public string ChosenCaption       { get; private set; } = string.Empty;
        public string ChosenHashtag       { get; private set; } = string.Empty;
        public string ChosenYtTitle       { get; private set; } = string.Empty;
        public string ChosenYtDescription { get; private set; } = string.Empty;

        // ── Internal ───────────────────────────────────────────────────────────
        private readonly string _platform;
        private readonly Dictionary<string, string> _styles;
        private readonly bool _isYouTube;
        private string _currentKey;

        private readonly Dictionary<string, RadioButton> _radios = new Dictionary<string, RadioButton>();
        private RichTextBox _rtbPreview;
        private Label _lblYtTitle;
        private Button _btnSave;

        // ── Style meta ─────────────────────────────────────────────────────────
        private static readonly Dictionary<string, (string Label, string Hint, Color Color)> StyleMeta =
            new Dictionary<string, (string, string, Color)>
        {
            { "noi_dau",   ("😔 Nỗi đau",   "Khai thác vấn đề / nỗi lo người xem đang gặp",           Color.FromArgb(220,110, 90)) },
            { "boc_phot",  ("🔥 Bóc phốt",  "Tiết lộ sự thật ẩn, gây shock nhẹ, kích thích tò mò",    Color.FromArgb(230,160, 50)) },
            { "huong_dan", ("📖 Hướng dẫn", "Thực tế, rõ ràng, cung cấp giá trị ngay",                Color.FromArgb( 70,170,130)) },
            { "fomo",      ("⚡ FOMO",       "Tạo sự cấp bách, sợ bỏ lỡ",                             Color.FromArgb( 80,160,240)) },
            { "ke_chuyen", ("💬 Kể chuyện", "Kể chuyện cá nhân, chân thực, gần gũi",                  Color.FromArgb(160,130,220)) },
        };

        private static readonly Color BgDark   = Color.FromArgb(20, 23, 30);
        private static readonly Color BgCard   = Color.FromArgb(26, 30, 40);
        private static readonly Color BgSel    = Color.FromArgb(32, 50, 75);
        private static readonly Color ColText  = Color.FromArgb(210, 215, 225);
        private static readonly Color ColMuted = Color.FromArgb(130, 140, 155);

        public FormCaptionSelector(
            string platform,
            Dictionary<string, string> styles,
            string currentKey,
            string videoLabel = null)
        {
            _platform   = platform;
            _styles     = styles ?? throw new ArgumentNullException(nameof(styles));
            _isYouTube  = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase);
            _currentKey = currentKey ?? string.Empty;

            Text            = $"Chọn phong cách caption — {platform}  {(videoLabel != null ? $"[{videoLabel}]" : "")}";
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(820, 580);
            MinimumSize     = new Size(680, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor       = BgDark;

            BuildUi();

            // Default: keep existing selection, else pick first available key
            var initKey = !string.IsNullOrWhiteSpace(_currentKey) && _styles.ContainsKey(_currentKey)
                ? _currentKey
                : OmnichannelCaptionResult.StyleKeys.FirstOrDefault(k => _styles.ContainsKey(k)) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(initKey))
                SetSelection(initKey);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI
        // ─────────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = BgDark,
                Padding     = new Padding(10),
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            // ── Left: radio list ─────────────────────────────────────────────
            var scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = BgDark,
            };

            var radioStack = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                ColumnCount = 1,
                BackColor   = BgDark,
                Padding     = new Padding(0, 0, 6, 0),
            };
            radioStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            foreach (var key in OmnichannelCaptionResult.StyleKeys.Where(k => _styles.ContainsKey(k)))
            {
                var card = BuildRadioCard(key);
                radioStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                radioStack.Controls.Add(card);
            }
            scroll.Controls.Add(radioStack);
            outer.Controls.Add(scroll, 0, 0);

            // ── Right: preview ───────────────────────────────────────────────
            var previewPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 26, 34),
                Padding   = new Padding(10, 8, 10, 8),
            };

            var previewStack = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = _isYouTube ? 5 : 3,
                BackColor   = Color.Transparent,
            };
            previewStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Label MkLbl(string t, Color c) => new Label
            {
                Text      = t,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = c,
                Margin    = new Padding(0, 6, 0, 2),
            };

            if (_isYouTube)
            {
                previewStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                previewStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
                previewStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                previewStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                previewStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                previewStack.Controls.Add(MkLbl("TIÊU ĐỀ:", Color.FromArgb(255, 220, 100)), 0, 0);
                _lblYtTitle = new Label
                {
                    Dock      = DockStyle.Fill,
                    Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(255, 230, 120),
                    AutoSize  = false,
                    Height    = 28,
                };
                previewStack.Controls.Add(_lblYtTitle, 0, 1);
                previewStack.Controls.Add(MkLbl("MÔ TẢ SEO:", ColMuted), 0, 2);
                _rtbPreview = MkRtb();
                previewStack.Controls.Add(_rtbPreview, 0, 3);
                previewStack.Controls.Add(MkLbl("(Hashtag nằm cuối mô tả)", ColMuted), 0, 4);
            }
            else
            {
                previewStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                previewStack.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
                previewStack.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

                previewStack.Controls.Add(MkLbl("CAPTION:", ColMuted), 0, 0);
                _rtbPreview = MkRtb();
                previewStack.Controls.Add(_rtbPreview, 0, 1);
                // Hashtag row
                _lblYtTitle = new Label
                {
                    Dock      = DockStyle.Fill,
                    Font      = new Font("Consolas", 8.5f),
                    ForeColor = Color.FromArgb(100, 200, 140),
                    AutoSize  = false,
                    Padding   = new Padding(0, 4, 0, 0),
                };
                previewStack.Controls.Add(_lblYtTitle, 0, 2);
            }

            previewPanel.Controls.Add(previewStack);
            outer.Controls.Add(previewPanel, 1, 0);

            // ── Bottom action bar ────────────────────────────────────────────
            var bar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor     = Color.Transparent,
                Padding       = new Padding(0, 8, 0, 0),
            };
            outer.SetColumnSpan(bar, 2);

            var btnCancel = MkBtn("✕ Hủy", Color.FromArgb(70, 50, 55), Color.FromArgb(220, 200, 200));
            btnCancel.Width = 88;
            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };
            bar.Controls.Add(btnCancel);

            _btnSave = MkBtn("💾 Dùng phong cách này", Color.FromArgb(40, 130, 80), Color.White);
            _btnSave.Width  = 190;
            _btnSave.Font   = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnSave.Margin = new Padding(0, 0, 8, 0);
            _btnSave.Click += (_, __) => CommitAndClose();
            bar.Controls.Add(_btnSave);

            outer.Controls.Add(bar, 0, 1);
            Controls.Add(outer);

            AcceptButton = _btnSave;
            CancelButton = btnCancel;
        }

        private Panel BuildRadioCard(string key)
        {
            StyleMeta.TryGetValue(key, out var m);
            var metaLabel = m.Label ?? key;
            var metaColor = m.Label != null ? m.Color : ColMuted;
            var snippet = ExtractSnippet(_styles[key], 90);

            var card = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = BgCard,
                Margin    = new Padding(0, 0, 0, 6),
                Padding   = new Padding(10, 6, 10, 6),
                Cursor    = Cursors.Hand,
                Tag       = key,
            };

            // Accent bar
            card.Controls.Add(new Panel
            {
                Width     = 4,
                Dock      = DockStyle.Left,
                BackColor = metaColor,
            });

            var inner = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 2,
                BackColor   = Color.Transparent,
                Padding     = new Padding(6, 0, 0, 0),
            };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22F));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var radio = new RadioButton
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Checked   = key == _currentKey,
            };
            radio.CheckedChanged += (_, __) => { if (radio.Checked) SetSelection(key); };
            _radios[key] = radio;
            inner.Controls.Add(radio, 0, 0);

            inner.Controls.Add(new Label
            {
                Text      = metaLabel,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = metaColor,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
            }, 1, 0);

            inner.Controls.Add(new Label
            {
                Text      = snippet,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = ColMuted,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
            }, 1, 1);
            inner.SetColumnSpan(inner.Controls[inner.Controls.Count - 1], 1);

            card.Controls.Add(inner);

            // Clicking anywhere on card → select
            void Sel(object s, EventArgs ev)
            {
                radio.Checked = true;
                SetSelection(key);
            }
            foreach (Control c in card.Controls.OfType<Control>())
            {
                c.Click += Sel;
                foreach (Control cc in c.Controls.OfType<Control>())
                    cc.Click += Sel;
            }
            card.Click += Sel;

            return card;
        }

        private void SetSelection(string key)
        {
            _currentKey = key;
            if (_radios.TryGetValue(key, out var r) && !r.Checked)
                r.Checked = true;

            if (!_styles.TryGetValue(key, out var full)) return;

            if (_isYouTube)
            {
                GeminiService.SplitYouTubeStyleValue(full, out var title, out var desc);
                _lblYtTitle.Text = title;
                _rtbPreview.Text = desc;
            }
            else
            {
                _rtbPreview.Text = StripHashtags(full);
                _lblYtTitle.Text = ExtractHashtagStr(full);
            }

            UpdateCardHighlights();
        }

        private void UpdateCardHighlights()
        {
            // Walk outer TableLayoutPanel → scroll → radioStack → cards
            foreach (Control outer in Controls)
            {
                foreach (Control col0 in outer.Controls.OfType<Panel>().Where(p => p.AutoScroll))
                foreach (Control stack in col0.Controls)
                foreach (Control card in stack.Controls.OfType<Panel>())
                    card.BackColor = card.Tag?.ToString() == _currentKey ? BgSel : BgCard;
            }
        }

        private void CommitAndClose()
        {
            if (string.IsNullOrWhiteSpace(_currentKey) || !_styles.ContainsKey(_currentKey))
            {
                MessageBox.Show("Hãy chọn một phong cách.", "Chưa chọn",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ChosenKey  = _currentKey;
            var full   = _styles[_currentKey];

            if (_isYouTube)
            {
                GeminiService.SplitYouTubeStyleValue(full, out var title, out var desc);
                ChosenYtTitle       = title;
                ChosenYtDescription = StripHashtags(desc);
                ChosenHashtag       = ExtractHashtagStr(full);
            }
            else
            {
                ChosenCaption = StripHashtags(full);
                ChosenHashtag = ExtractHashtagStr(full);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static RichTextBox MkRtb() => new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = Color.FromArgb(30, 34, 44),
            ForeColor   = ColText,
            Font        = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.None,
            ReadOnly    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            WordWrap    = true,
        };

        private static Button MkBtn(string text, Color back, Color fore)
        {
            var b = new Button
            {
                Text      = text,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font      = new Font("Segoe UI", 9f),
                Cursor    = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        private static string ExtractSnippet(string text, int max)
        {
            var body = StripHashtags(text);
            return body.Length > max ? body.Substring(0, max).TrimEnd() + "…" : body;
        }

        private static string StripHashtags(string text)
            => string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : Regex.Replace(text, @"(\s*#[^\s#]+)+\s*$", string.Empty).Trim();

        private static string ExtractHashtagStr(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var matches = Regex.Matches(text, @"#[^\s#]+");
            return matches.Count == 0
                ? string.Empty
                : string.Join(" ", matches.Cast<Match>().Select(m => m.Value));
        }
    }
}
