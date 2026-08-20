using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1 : Form
    {
        private void BuildAutoWarmupUi()
        {
            BuildAutoWarmupZeroScrollUi();
        }

        private void BuildAffiliateHunterUi()
        {
            var btnAffiliateKeywordsCaptionFont = AppCaptionFont;
            const int affiliateKeywordRowHeight = 70;
            var btnAffiliateProfileCaptionWidth = Math.Max(
                72,
                MeasureAffiliateButtonTextWidth("Profile", btnAffiliateKeywordsCaptionFont, affiliateKeywordRowHeight) + 16);
            const int affiliateProfileComboWidth = 280;
            var btnAffiliateKeywordsCaptionWidth = Math.Max(
                72,
                MeasureAffiliateButtonTextWidth("từ khoá", btnAffiliateKeywordsCaptionFont, affiliateKeywordRowHeight) + 16);

            var btnAffiliateKeywordsCaption = new Button
            {
                Name = "btnAffiliateKeywordsCaption",
                Text = "từ khoá",
                AutoSize = false,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = Color.FromArgb(52, 120, 220),
                ForeColor = Color.FromArgb(245, 247, 250),
                Font = btnAffiliateKeywordsCaptionFont,
                Cursor = Cursors.Hand,
                Margin = Padding.Empty,
                MinimumSize = new Size(btnAffiliateKeywordsCaptionWidth, affiliateKeywordRowHeight),
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnAffiliateKeywordsCaption.FlatAppearance.BorderSize = 0;
            btnAffiliateKeywordsCaption.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 140, 235);
            btnAffiliateKeywordsCaption.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 100, 200);

            txtAffiliateKeywords = new TextBox
            {
                Name = "txtAffiliateKeywords",
                Height = affiliateKeywordRowHeight,
                MinimumSize = new Size(0, affiliateKeywordRowHeight),
                Multiline = false,
                ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.FromArgb(120, 125, 140),
                Font = AppInputFont,
                Text = AffiliateKeywordsPlaceholder
            };
            _affiliateKeywordsPlaceholderActive = true;
            btnAffiliateKeywordsCaption.Click += (_, __) => txtAffiliateKeywords.Focus();
            txtAffiliateKeywords.Enter += TxtAffiliateKeywords_Enter;
            txtAffiliateKeywords.Leave += TxtAffiliateKeywords_Leave;
            txtAffiliateKeywords.TextChanged += TxtAffiliateKeywords_TextChanged;

            var lblAffiliatePlatforms = new Label
            {
                Text = "Nền tảng (chọn nhiều)",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 0, 2)
            };

            chkAffiliatePlatformTikTok = new CheckBox
            {
                Name = "chkAffiliatePlatformTikTok",
                Text = "TikTok",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 0, 12, 0)
            };

            chkAffiliatePlatformFacebook = new CheckBox
            {
                Name = "chkAffiliatePlatformFacebook",
                Text = "Facebook Reels",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 0, 12, 0)
            };

            chkAffiliatePlatformYouTube = new CheckBox
            {
                Name = "chkAffiliatePlatformYouTube",
                Text = "YouTube Shorts",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 0, 12, 0)
            };

            var lblMaxResults = new Label
            {
                Text = "Kết quả tối đa",
                AutoSize = true,
                Margin = new Padding(0, 2, 6, 0),
                ForeColor = Color.Gainsboro
            };

            numAffiliateMaxResults = new NumericUpDown
            {
                Name = "numAffiliateMaxResults",
                Size = new Size(118, 28),
                Margin = new Padding(0, 0, 12, 0),
                Minimum = 1,
                Maximum = 500,
                Value = 30,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            chkAffiliateOnlyHighQuality = new CheckBox
            {
                Name = "chkAffiliateOnlyHighQuality",
                Text = "Chỉ chất lượng cao",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 12, 0)
            };
            chkAffiliateOnlyHighQuality.CheckedChanged += (s, e) => RefreshAffiliateGridByQualityFilter();

            chkAffiliateRankByEngagement = new CheckBox
            {
                Name = "chkAffiliateRankByEngagement",
                Text = "Ưu tiên engagement",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 2, 12, 0)
            };
            chkAffiliateRankByEngagement.CheckedChanged += chkAffiliateRankByEngagement_CheckedChanged;

            var lblAffiliateTikTokHuntMode = new Label
            {
                Text = "Săn TikTok",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 6, 0)
            };

            cbAffiliateTikTokHuntMode = new ComboBox
            {
                Name = "cbAffiliateTikTokHuntMode",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(218, 28),
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Font = AppInputFont
            };
            cbAffiliateTikTokHuntMode.Items.AddRange(new object[] { "Browser", "RapidAPI" });
            cbAffiliateTikTokHuntMode.SelectedIndex = 0;
            cbAffiliateTikTokHuntMode.SelectedIndexChanged += cbAffiliateTikTokHuntMode_SelectedIndexChanged;

            chkAffiliateTikTokApiFallbackBrowser = new CheckBox
            {
                Name = "chkAffiliateTikTokApiFallbackBrowser",
                Text = "API lỗi → Browser",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 2, 12, 0)
            };
            chkAffiliateTikTokApiFallbackBrowser.CheckedChanged += (s, e) => ScheduleAffiliateHuntPrefsSave();

            var lblAffiliateBufferMult = new Label
            {
                Text = "Hệ số buffer ×",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 6, 0)
            };

            numAffiliateBufferMultiplier = new NumericUpDown
            {
                Name = "numAffiliateBufferMultiplier",
                Size = new Size(94, 28),
                Minimum = 1.5m,
                Maximum = 5.0m,
                Increment = 0.5m,
                DecimalPlaces = 1,
                Value = 2.5m,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 0, 0)
            };
            numAffiliateBufferMultiplier.ValueChanged += numAffiliateBufferMultiplier_ValueChanged;

            var flpAffiliateCompactFilters = new FlowLayoutPanel
            {
                Name = "flpAffiliateCompactFilters",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                AutoScroll = false,
                Padding = new Padding(0, 4, 0, 4),
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.Transparent,
                Dock = DockStyle.None
            };
            flpAffiliateCompactFilters.Controls.Add(lblMaxResults);
            flpAffiliateCompactFilters.Controls.Add(numAffiliateMaxResults);
            flpAffiliateCompactFilters.Controls.Add(chkAffiliateOnlyHighQuality);
            flpAffiliateCompactFilters.Controls.Add(chkAffiliateRankByEngagement);
            flpAffiliateCompactFilters.Controls.Add(lblAffiliateTikTokHuntMode);
            flpAffiliateCompactFilters.Controls.Add(cbAffiliateTikTokHuntMode);
            flpAffiliateCompactFilters.Controls.Add(chkAffiliateTikTokApiFallbackBrowser);
            flpAffiliateCompactFilters.Controls.Add(lblAffiliateBufferMult);
            flpAffiliateCompactFilters.Controls.Add(numAffiliateBufferMultiplier);

            chkAffiliateAutoEnrich = new CheckBox
            {
                Name = "chkAffiliateAutoEnrich",
                Text = "Tự động enrich sau Hunt",
                AutoSize = true,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(8, 2, 0, 0),
                Font = AppLabelFont
            };
            chkAffiliateAutoEnrich.CheckedChanged += chkAffiliateAutoEnrich_CheckedChanged;
            flpAffiliateCompactFilters.Controls.Add(chkAffiliateAutoEnrich);

            btnHuntAffiliates = CreateAffiliateJellyButton("btnHuntAffiliates", "Quét Affiliate", AffiliateTintHunt);
            btnHuntAffiliates.Click += btnHuntAffiliates_Click;

            btnStopHunt = CreateAffiliateJellyButton("btnStopHunt", "Dừng", AffiliateTintStop);
            btnStopHunt.Enabled = false;
            btnStopHunt.Click += btnStopHunt_Click;

            btnExportAffiliateCsv = CreateAffiliateJellyButton("btnExportAffiliateCsv", "Xuất CSV / HTML", AffiliateTintNeutral);
            btnExportAffiliateCsv.Enabled = false;
            btnExportAffiliateCsv.Click += btnExportAffiliateCsv_Click;

            btnPushToAiVideoGen = CreateAffiliateJellyButton("btnPushToAiVideoGen", "Đẩy sang AI Video Gen", AffiliateTintPush);
            btnPushToAiVideoGen.Enabled = false;
            btnPushToAiVideoGen.Click += btnPushToAiVideoGen_Click;

            btnAffiliateDeepDive = CreateAffiliateJellyButton("btnAffiliateDeepDive", "🔍 Phân tích chuyên sâu (hàng đợi)", AffiliateTintDeepDive);
            btnAffiliateDeepDive.Click += btnAffiliateDeepDive_Click;

            btnDownloadSelectedAffiliate = CreateAffiliateJellyButton("btnDownloadSelectedAffiliate", "⬇ Tải video", AffiliateTintDownload);
            btnDownloadSelectedAffiliate.Enabled = false;
            btnDownloadSelectedAffiliate.Click += btnDownloadSelectedAffiliate_Click;

            var affiliateActionTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ShowAlways = true };
            affiliateActionTip.SetToolTip(btnStopHunt, "Dừng quét đang chạy.");
            affiliateActionTip.SetToolTip(btnExportAffiliateCsv, "Xuất kết quả khi đã có dữ liệu trên lưới.");
            affiliateActionTip.SetToolTip(btnPushToAiVideoGen, "Tô nhiều dòng (Ctrl/Shift+click) rồi đẩy sang Slideshow, Affiliate chuyên sâu hoặc Video Reup. Không tô = đẩy toàn bộ lưới.");
            affiliateActionTip.SetToolTip(btnAffiliateDeepDive,
                "Chọn dòng trên lưới — tải video, gửi Gemini phân tích voiceover/kịch bản (cần AI API Key).");
            affiliateActionTip.SetToolTip(btnDownloadSelectedAffiliate, "Cần FFmpeg + lưu trữ OK và có dòng được chọn.");
            affiliateActionTip.SetToolTip(
                chkAffiliateAutoEnrich,
                "Sau Hunt: tự gọi TikWM (views/likes/…) và quét link affiliate. Chạy ngầm — bấm Dừng để hủy.");

            var flpAffiliateActions = CreateAffiliateActionsFlowPanel("flpAffiliateActions");
            flpAffiliateActions.Margin = new Padding(0, 4, 0, 8);
            foreach (var btn in new[]
                     {
                         btnHuntAffiliates, btnStopHunt, btnDownloadSelectedAffiliate, btnExportAffiliateCsv,
                         btnPushToAiVideoGen, btnAffiliateDeepDive
                     })
            {
                PrepareAffiliateToolbarButtonForFlow(btn);
                flpAffiliateActions.Controls.Add(btn);
            }

            lblAffiliateEnrichStatus = new Label
            {
                Name = "lblAffiliateEnrichStatus",
                Text = string.Empty,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(150, 170, 200),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic)
            };

            lnkAffiliateDownloadFolder = new LinkLabel
            {
                Name = "lnkAffiliateDownloadFolder",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Color.FromArgb(140, 185, 255),
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.FromArgb(140, 185, 255),
                DisabledLinkColor = Color.Gray,
                ForeColor = Color.FromArgb(180, 190, 210),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                UseMnemonic = false,
                Text = "📁 Thư mục tải video: …"
            };
            lnkAffiliateDownloadFolder.LinkClicked += LnkAffiliateDownloadFolder_LinkClicked;

            _affiliateBindingList = new BindingList<AffiliateCandidate>();
            _affiliateBindingList.ListChanged += AffiliateBindingList_ListChanged;

            dgvAffiliateResults = new DataGridView
            {
                Name = "dgvAffiliateResults",
                AutoGenerateColumns = true,
                ShowCellToolTips = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgvAffiliateResults.KeyDown += dgvAffiliateResults_KeyDown;
            dgvAffiliateResults.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            dgvAffiliateResults.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };
            ApplyAppGridChrome(dgvAffiliateResults);
            dgvAffiliateResults.DataSource = _affiliateBindingList;
            dgvAffiliateResults.DataBindingComplete += dgvAffiliateResults_DataBindingComplete;
            dgvAffiliateResults.CellFormatting += dgvAffiliateResults_CellFormatting;
            dgvAffiliateResults.CellDoubleClick += dgvAffiliateResults_CellDoubleClick;
            dgvAffiliateResults.CellClick += dgvAffiliateResults_CellClick;
            dgvAffiliateResults.SelectionChanged += dgvAffiliateResults_SelectionChanged;
            dgvAffiliateResults.CellMouseDown += dgvAffiliateResults_CellMouseDown;

            var affiliateContextMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                ShowImageMargin = false
            };

            var miOpenVideo = new ToolStripMenuItem("🌐 Xem Video gốc");
            miOpenVideo.Click += (s, e) => OpenSelectedAffiliateField(c => c.VideoUrl, "VideoUrl");
            affiliateContextMenu.Items.Add(miOpenVideo);

            var miOpenProfile = new ToolStripMenuItem("👤 Xem Kênh tác giả");
            miOpenProfile.Click += (s, e) => OpenSelectedAffiliateField(c => c.ProfileUrl, "ProfileUrl");
            affiliateContextMenu.Items.Add(miOpenProfile);

            affiliateContextMenu.Items.Add(new ToolStripSeparator());

            var miPeekCaption = new ToolStripMenuItem("📋 Xem / copy Caption đầy đủ");
            miPeekCaption.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.ProductName, "Caption");
            affiliateContextMenu.Items.Add(miPeekCaption);

            var miPeekKeyword = new ToolStripMenuItem("📋 Xem / copy Từ khoá đầy đủ");
            miPeekKeyword.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.SourceKeyword, "Từ khoá");
            affiliateContextMenu.Items.Add(miPeekKeyword);

            var miPeekHashtags = new ToolStripMenuItem("📋 Xem / copy Hashtag đầy đủ");
            miPeekHashtags.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.Hashtags, "Hashtag");
            affiliateContextMenu.Items.Add(miPeekHashtags);

            var miPeekLinked = new ToolStripMenuItem("📋 Xem / copy Link aff đầy đủ");
            miPeekLinked.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.LinkedProduct, "Link aff");
            affiliateContextMenu.Items.Add(miPeekLinked);

            var miPeekVoice = new ToolStripMenuItem("📋 Xem / copy Lời thoại đầy đủ");
            miPeekVoice.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.VoiceoverTranscript, "Lời thoại");
            affiliateContextMenu.Items.Add(miPeekVoice);

            affiliateContextMenu.Items.Add(new ToolStripSeparator());

            var miPriorityDeepDive = new ToolStripMenuItem("⚡ Ưu tiên Deep Dive (Queue)");
            miPriorityDeepDive.Click += miAffiliatePriorityDeepDive_Click;
            affiliateContextMenu.Items.Add(miPriorityDeepDive);

            var miCancelDeepDive = new ToolStripMenuItem("⏹ Hủy job Deep Dive");
            miCancelDeepDive.Click += miAffiliateCancelDeepDive_Click;
            affiliateContextMenu.Items.Add(miCancelDeepDive);

            affiliateContextMenu.Items.Add(new ToolStripSeparator());

            var miRefreshMetrics = new ToolStripMenuItem("📊 Refresh số liệu dòng này");
            miRefreshMetrics.Click += async (s, e) => await RefreshSelectedAffiliateMetricsAsync().ConfigureAwait(true);
            affiliateContextMenu.Items.Add(miRefreshMetrics);

            var miRescanAnchor = new ToolStripMenuItem("🛒 Quét lại link affiliate");
            miRescanAnchor.Click += async (s, e) => await RescanSelectedAffiliateAnchorAsync().ConfigureAwait(true);
            affiliateContextMenu.Items.Add(miRescanAnchor);

            affiliateContextMenu.Items.Add(new ToolStripSeparator());

            var miDeleteAffiliate = new ToolStripMenuItem("❌ Xóa dòng đang chọn");
            miDeleteAffiliate.Click += miDeleteAffiliate_Click;
            affiliateContextMenu.Items.Add(miDeleteAffiliate);

            dgvAffiliateResults.ContextMenuStrip = affiliateContextMenu;

            var affiliateGridTip = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
            affiliateGridTip.SetToolTip(dgvAffiliateResults,
                "Kết quả săn được lưu tự động (%LocalAppData%\\tiktok_Omni\\hunt_results.json) — build lại app không mất. " +
                "Quét thêm sẽ gộp video mới, không xóa dòng cũ (Delete để xóa). " +
                "Bấm đúp dòng để xem video MP4 native. Bấm ô Caption / Từ khoá / Hashtag để xem đầy đủ và sao chép. " +
                "Bấm đơn ô «Link video» để copy URL. " +
                "Điểm an toàn: 100 = sạch, càng thấp càng nhiều rủi ro. «Hướng dẫn chấm điểm» xem tiêu chí.");
            var scoringGuide =
                "Engagement Score (0-100) — cộng từ 6 yếu tố:\r\n\r\n" +
                "  📊 Views (30 điểm, log scale)\r\n" +
                "       10M+ → 30 · 1M → 27 · 500K → 24 · 100K → 20\r\n" +
                "       50K → 16 · 10K → 12 · 1K → 6 · <1K → 2\r\n\r\n" +
                "  ❤ Like rate = likes/views × 100 (20 điểm)\r\n" +
                "       ≥15% → 20 · ≥10% → 16 · ≥5% → 12 · ≥3% → 8 · ≥1% → 4\r\n\r\n" +
                "  💬 Comment rate (15 điểm)\r\n" +
                "       ≥1% → 15 · ≥0.5% → 10 · ≥0.2% → 6 · ≥0.05% → 3\r\n\r\n" +
                "  ↗ Share rate (15 điểm)\r\n" +
                "       ≥1% → 15 · ≥0.5% → 10 · ≥0.2% → 6 · ≥0.05% → 3\r\n\r\n" +
                "  💾 Save rate (15 điểm) — nếu TikWM không trả saves thì\r\n" +
                "       chuyển sang total engagement rate (likes+cmt+share)/views.\r\n\r\n" +
                "  ⏱ Duration sweet spot (5 điểm — proxy cho «thời gian ở lại»)\r\n" +
                "       7-25s → 5 · 5-60s → 3 · 3-90s → 1 · ngoài → 0\r\n\r\n" +
                "Ngưỡng: ≥75 video viral mạnh · 50-74 ổn · <50 yếu.\r\n" +
                "Score = 0 nghĩa là chưa enrich metrics (chờ auto-enrich xong).";
            affiliateGridTip.SetToolTip(chkAffiliateOnlyHighQuality, scoringGuide);
            affiliateGridTip.SetToolTip(btnAffiliateDeepDive,
                "Tải video về, nén nhỏ <5MB rồi gửi Gemini phân tích: voiceover, kịch bản, đoạn ăn tiền (số giây).");
            affiliateGridTip.SetToolTip(btnDownloadSelectedAffiliate,
                "Tải MP4 tuần tự cho các dòng đang chọn. File lưu dưới thư mục con «downloads» theo từ khoá (xem dòng link ngay trên bảng). Bấm «Dừng» để hủy giữa chừng.");
            affiliateGridTip.SetToolTip(chkAffiliateAutoEnrich,
                "Khi bật: sau khi Hunt xong sẽ TỰ ĐỘNG gọi TikWM API lấy số liệu (views/likes/comments/shares/dài/đăng), " +
                "rồi tự động quét link affiliate (anchor mobile UA) cho từng dòng. Chạy ngầm, có thể bấm Stop để hủy.");
            affiliateGridTip.SetToolTip(chkAffiliateRankByEngagement,
                "Bật: săn thêm video (buffer × hệ số), gọi TikWM lấy view/likes, giữ top «Max Results» theo view cao nhất. " +
                "Tắt: giữ đúng thứ tự feed TikTok (có thể view thấp).");
            affiliateGridTip.SetToolTip(numAffiliateBufferMultiplier,
                "Số video tối đa săn mỗi từ khoá ≈ Max Results × hệ số (tối đa 120). Ví dụ Max 20 × 2,5 → ~50 ứng viên trước khi cắt top 20.");
            affiliateGridTip.SetToolTip(cbAffiliateTikTokHuntMode,
                "Browser: mở Chrome quét tiktok.com/search/video.\r\n" +
                "RapidAPI: gọi tiktok-api23 (cần key tab Cài đặt) — nhanh hơn, có view ngay.");
            affiliateGridTip.SetToolTip(chkAffiliateTikTokApiFallbackBrowser,
                "Khi chọn RapidAPI: nếu API lỗi (key/rate limit) tự chuyển sang săn bằng browser.");

            btnAffiliateKeywordsCaption.Dock = DockStyle.Fill;
            btnAffiliateKeywordsCaption.Margin = Padding.Empty;

            txtAffiliateKeywords.Dock = DockStyle.Fill;
            txtAffiliateKeywords.Margin = Padding.Empty;
            txtAffiliateKeywords.MinimumSize = new Size(0, affiliateKeywordRowHeight);

            var btnAffiliateProfileCaption = new Button
            {
                Name = "btnAffiliateProfileCaption",
                Text = "Profile",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 120, 210),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = btnAffiliateKeywordsCaptionFont,
                Cursor = Cursors.Hand,
                Margin = Padding.Empty,
                MinimumSize = new Size(btnAffiliateProfileCaptionWidth, affiliateKeywordRowHeight),
            };
            btnAffiliateProfileCaption.FlatAppearance.BorderSize = 0;
            btnAffiliateProfileCaption.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 140, 235);
            btnAffiliateProfileCaption.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 100, 200);

            cbAffiliateHuntProfile = new ComboBox
            {
                Name = "cbAffiliateHuntProfile",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Font = AppInputFont,
                Margin = Padding.Empty,
                ItemHeight = Math.Max(20, affiliateKeywordRowHeight - 10)
            };
            cbAffiliateHuntProfile.Items.Add("default");
            cbAffiliateHuntProfile.SelectedIndex = 0;
            cbAffiliateHuntProfile.SelectedIndexChanged += (_, __) =>
            {
                if (cbAffiliateHuntProfile.SelectedItem != null)
                {
                    ApplyProfileScope(cbAffiliateHuntProfile.SelectedItem.ToString());
                }

                RefreshAffiliateDownloadFolderHint();
            };
            btnAffiliateProfileCaption.Click += (_, __) => cbAffiliateHuntProfile.Focus();

            var pnlAffiliateKeywordHost = new Panel
            {
                Name = "pnlAffiliateKeywordHost",
                Dock = DockStyle.Fill,
                Height = affiliateKeywordRowHeight,
                MinimumSize = new Size(0, affiliateKeywordRowHeight),
                Margin = new Padding(6, 6, 6, 6),
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };

            var pnlAffiliateKeywordLine = new TableLayoutPanel
            {
                Name = "pnlAffiliateKeywordLine",
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            pnlAffiliateKeywordLine.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            pnlAffiliateKeywordLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnAffiliateProfileCaptionWidth));
            pnlAffiliateKeywordLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, affiliateProfileComboWidth));
            pnlAffiliateKeywordLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnAffiliateKeywordsCaptionWidth));
            pnlAffiliateKeywordLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlAffiliateKeywordLine.Controls.Add(btnAffiliateProfileCaption, 0, 0);
            pnlAffiliateKeywordLine.Controls.Add(cbAffiliateHuntProfile, 1, 0);
            pnlAffiliateKeywordLine.Controls.Add(btnAffiliateKeywordsCaption, 2, 0);
            pnlAffiliateKeywordLine.Controls.Add(txtAffiliateKeywords, 3, 0);
            pnlAffiliateKeywordHost.Controls.Add(pnlAffiliateKeywordLine);

            affiliateGridTip.SetToolTip(cbAffiliateHuntProfile,
                "Nick lưu video tải xuống và kết quả săn — chọn trước khi bấm «Quét Affiliate».");
            affiliateGridTip.SetToolTip(btnAffiliateProfileCaption,
                "Nick lưu video tải xuống và kết quả săn — chọn trước khi bấm «Quét Affiliate».");

            var flpAffiliatePlatformsRow = new FlowLayoutPanel
            {
                Name = "flpAffiliatePlatformsRow",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(0),
                BackColor = Color.Transparent,
                Dock = DockStyle.None
            };
            lblAffiliatePlatforms.Margin = new Padding(0, 2, 8, 0);
            chkAffiliatePlatformTikTok.Margin = new Padding(0, 2, 12, 0);
            chkAffiliatePlatformFacebook.Margin = new Padding(0, 2, 12, 0);
            chkAffiliatePlatformYouTube.Margin = new Padding(0, 2, 8, 0);
            flpAffiliatePlatformsRow.Controls.Add(lblAffiliatePlatforms);
            flpAffiliatePlatformsRow.Controls.Add(chkAffiliatePlatformTikTok);
            flpAffiliatePlatformsRow.Controls.Add(chkAffiliatePlatformFacebook);
            flpAffiliatePlatformsRow.Controls.Add(chkAffiliatePlatformYouTube);

            var lblTikTokHuntMethod = new Label
            {
                Text = "Săn TikTok",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(12, 2, 6, 0)
            };

            cbTikTokHuntMethod = new ComboBox
            {
                Name = "cbTikTokHuntMethod",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 168,
                Height = 28,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            cbTikTokHuntMethod.Items.AddRange(new object[] { "RapidAPI", "Browser (Playwright)" });
            cbTikTokHuntMethod.SelectedIndex = 0;
            cbTikTokHuntMethod.SelectedIndexChanged += cbTikTokHuntMethod_SelectedIndexChanged;

            chkTikTokApiFallbackBrowser = new CheckBox
            {
                Name = "chkTikTokApiFallbackBrowser",
                Text = "API lỗi → Browser",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            chkTikTokApiFallbackBrowser.CheckedChanged += (s, e) => ScheduleAffiliateHuntPrefsSave();

            var flpAffiliateTikTokHuntRow = new FlowLayoutPanel
            {
                Name = "flpAffiliateTikTokHuntRow",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Margin = new Padding(0, 0, 0, 2),
                Padding = new Padding(0),
                BackColor = Color.Transparent,
                Dock = DockStyle.None
            };
            flpAffiliateTikTokHuntRow.Controls.Add(lblTikTokHuntMethod);
            flpAffiliateTikTokHuntRow.Controls.Add(cbTikTokHuntMethod);
            flpAffiliateTikTokHuntRow.Controls.Add(chkTikTokApiFallbackBrowser);
            affiliateGridTip.SetToolTip(cbTikTokHuntMethod,
                "RapidAPI: gọi tiktok-api23, không mở Chrome. Browser: Playwright như trước.");
            affiliateGridTip.SetToolTip(chkTikTokApiFallbackBrowser,
                "Khi RapidAPI lỗi (key, quota, mạng), tự chuyển sang săn bằng trình duyệt.");

            var tblAffiliateFilters = new FlowLayoutPanel
            {
                Name = "tblAffiliateFilters",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = Padding.Empty,
                Margin = new Padding(6, 4, 6, 4),
                BackColor = Color.FromArgb(35, 38, 48)
            };
            tblAffiliateFilters.Controls.Add(flpAffiliatePlatformsRow);
            tblAffiliateFilters.Controls.Add(flpAffiliateTikTokHuntRow);
            tblAffiliateFilters.Controls.Add(flpAffiliateCompactFilters);
            tblAffiliateFilters.Controls.Add(flpAffiliateActions);

            var pnlAffiliateGridHost = new Panel
            {
                Name = "pnlAffiliateGridHost",
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(20, 22, 28),
                MinimumSize = new Size(0, 80)
            };
            dgvAffiliateResults.Dock = DockStyle.Fill;
            dgvAffiliateResults.Margin = Padding.Empty;
            pnlAffiliateGridHost.Controls.Add(dgvAffiliateResults);
            ApplyAppGridChrome(dgvAffiliateResults);

            var flpAffiliateFooter = new FlowLayoutPanel
            {
                Name = "flpAffiliateFooter",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 2, 0, 0),
                BackColor = tabAffiliateHunter.BackColor
            };
            lblAffiliateEnrichStatus.AutoSize = false;
            lblAffiliateEnrichStatus.Dock = DockStyle.None;
            lblAffiliateEnrichStatus.Height = 18;
            lblAffiliateEnrichStatus.Width = 280;
            lblAffiliateEnrichStatus.Margin = new Padding(0, 0, 12, 0);
            lnkAffiliateDownloadFolder.AutoSize = false;
            lnkAffiliateDownloadFolder.Dock = DockStyle.None;
            lnkAffiliateDownloadFolder.Height = 18;
            lnkAffiliateDownloadFolder.Margin = Padding.Empty;
            flpAffiliateFooter.Controls.Add(lblAffiliateEnrichStatus);
            flpAffiliateFooter.Controls.Add(lnkAffiliateDownloadFolder);
            flpAffiliateFooter.Resize += (_, __) =>
            {
                if (lnkAffiliateDownloadFolder != null && !lnkAffiliateDownloadFolder.IsDisposed)
                {
                    lnkAffiliateDownloadFolder.Width = Math.Max(
                        120,
                        flpAffiliateFooter.ClientSize.Width - lnkAffiliateDownloadFolder.Left - 4);
                }
            };

            var pnlAffiliateFooter = new Panel
            {
                Name = "pnlAffiliateFooter",
                Dock = DockStyle.Fill,
                Height = 24,
                MinimumSize = new Size(0, 24),
                Padding = new Padding(6, 0, 6, 2),
                BackColor = tabAffiliateHunter.BackColor
            };
            pnlAffiliateFooter.Controls.Add(flpAffiliateFooter);

            var tblAffiliateShellLayout = new TableLayoutPanel
            {
                Name = "tblAffiliateShellLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(32, 34, 44)
            };
            tblAffiliateShellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblAffiliateShellLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAffiliateShellLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAffiliateShellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblAffiliateShellLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            tblAffiliateShellLayout.Controls.Add(tblAffiliateFilters, 0, 0);
            tblAffiliateShellLayout.Controls.Add(pnlAffiliateKeywordHost, 0, 1);
            tblAffiliateShellLayout.Controls.Add(pnlAffiliateGridHost, 0, 2);
            tblAffiliateShellLayout.Controls.Add(pnlAffiliateFooter, 0, 3);

            var pnlAffiliateShell = new Panel
            {
                Name = "pnlAffiliateShell",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 34, 44),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            pnlAffiliateShell.Controls.Add(tblAffiliateShellLayout);

            tabHuntVideo = new TabPage("Săn Video")
            {
                Name = "tabHuntVideo",
                BackColor = Color.FromArgb(32, 34, 44),
                Padding = Padding.Empty,
                AutoScroll = false
            };
            tabHuntVideo.Controls.Add(pnlAffiliateShell);

            tabHuntProduct = new TabPage("Săn Link Sản phẩm")
            {
                Name = "tabHuntProduct",
                BackColor = Color.FromArgb(32, 34, 44),
                Padding = new Padding(4),
                AutoScroll = false
            };
            BuildAffiliateHunterProductUi(tabHuntProduct);

            tabCtrlHunter = new TabControl
            {
                Name = "tabCtrlHunter",
                Dock = DockStyle.Fill,
                Padding = new Point(8, 4),
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(32, 34, 44),
                Appearance = TabAppearance.Normal
            };
            tabCtrlHunter.TabPages.Add(tabHuntVideo);
            tabCtrlHunter.TabPages.Add(tabHuntProduct);
            WireHuntProductTabLayout();

            tabAffiliateHunter.Controls.Clear();
            tabAffiliateHunter.Controls.Add(tabCtrlHunter);
            tabAffiliateHunter.AutoScroll = false;

            RefreshAffiliateDownloadFolderHint();
            pnlAffiliateShell.Resize += (_, __) => EnsureAffiliateFiltersLayout();
            DisableAutoScrollRecursive(tabAffiliateHunter);
            EnableHuntProductTabAutoScroll();
            tabAffiliateHunter.HandleCreated += (_, __) =>
            {
                BeginInvoke(new Action(EnsureAffiliateFiltersLayout));
            };
        }

        private static readonly Font AffiliateJellyButtonFont = AppJellyButtonFont;
        private static readonly Font AffiliateKeywordsInputFont = AppKeywordInputFont;
        private const int AffiliateJellyButtonMinHeight = AppJellyButtonHeight;
        private const int AffiliateButtonHorizontalPad = AppJellyButtonHorizontalPad;
        private const int AffiliateButtonMinWidth = AppJellyButtonMinWidth;

        private static FlowLayoutPanel CreateAffiliateActionsFlowPanel(string name)
        {
            return new FlowLayoutPanel
            {
                Name = name,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                AutoScroll = false,
                Padding = new Padding(0, 1, 0, 1),
                Margin = Padding.Empty,
                BackColor = Color.Transparent,
                Dock = DockStyle.None
            };
        }

        private static int MeasureAffiliateButtonTextWidth(string text)
        {
            return MeasureAppJellyButtonTextWidth(text, AffiliateJellyButtonFont, AffiliateJellyButtonMinHeight);
        }

        private static int MeasureAffiliateButtonTextWidth(string text, Font font, int buttonHeight)
        {
            return MeasureAppJellyButtonTextWidth(text, font, buttonHeight);
        }

        internal static void ResizeAffiliateToolbarButton(Button button)
        {
            ResizeAppJellyButton(button, AffiliateJellyButtonMinHeight, AffiliateButtonMinWidth, AffiliateButtonHorizontalPad);
        }

        private static void PrepareAffiliateToolbarButtonForFlow(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.Dock = DockStyle.None;
            button.AutoSize = false;
            button.Margin = new Padding(4, 3, 4, 3);
            ResizeAffiliateToolbarButton(button);
        }
        private static readonly Color AffiliateTintHunt = Color.FromArgb(76, 110, 245);
        private static readonly Color AffiliateTintStop = Color.FromArgb(195, 72, 72);
        private static readonly Color AffiliateTintNeutral = Color.FromArgb(60, 64, 77);
        private static readonly Color AffiliateTintPush = Color.FromArgb(56, 158, 88);
        private static readonly Color AffiliateTintPipeline = Color.FromArgb(131, 96, 195);
        private static readonly Color AffiliateTintDeepDive = Color.FromArgb(20, 60, 140);
        private static readonly Color AffiliateTintDownload = Color.FromArgb(46, 100, 78);
        private static readonly Color AffiliateTintScript = Color.FromArgb(78, 120, 166);

        private static JellyButton CreateAffiliateJellyButton(string name, string text, Color tint)
        {
            var btn = CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: AffiliateJellyButtonMinHeight,
                minWidth: 88,
                margin: new Padding(10, 6, 10, 6),
                lockSize: false);
            btn.Dock = DockStyle.Fill;
            return btn;
        }

        private void BuildAiVideoGenUi()
        {
            lblAiVideoGenProductsTitle = new Label
            {
                Text = "Dữ liệu sản phẩm từ Affiliate Hunter (Slideshow)",
                AutoSize = true,
                Margin = new Padding(0, 4, 12, 4),
                Padding = new Padding(4, 2, 0, 2),
                ForeColor = Color.FromArgb(210, 214, 224)
            };

            chkAiVideoGenCurrentProfileOnly = new CheckBox
            {
                Name = "chkAiVideoGenCurrentProfileOnly",
                Text = "Chỉ hiện sản phẩm của Profile hiện tại",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 4),
                Padding = new Padding(0, 0, 8, 0),
                ForeColor = Color.FromArgb(200, 204, 214)
            };
            chkAiVideoGenCurrentProfileOnly.CheckedChanged += ChkAiVideoGenCurrentProfileOnly_CheckedChanged;

            flpAiVideoGenHeader = new FlowLayoutPanel
            {
                Name = "flpAiVideoGenHeader",
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 6, 8, 4),
                Margin = new Padding(0),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            flpAiVideoGenHeader.Controls.Add(lblAiVideoGenProductsTitle);
            flpAiVideoGenHeader.Controls.Add(chkAiVideoGenCurrentProfileOnly);
            BuildAiVideoGenModeIndicatorUi();

            pnlAiVideoGenScriptHost = new Panel
            {
                Name = "pnlAiVideoGenScriptHost",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAiVideoGenStickyHost = new Panel
            {
                Name = "pnlAiVideoGenStickyHost",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAiVideoGenProductArea = new Panel
            {
                Name = "pnlAiVideoGenProductArea",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42),
                Padding = new Padding(8, 4, 8, 4)
            };

            grpAiRenderProgress = new GroupBox
            {
                Name = "grpAiRenderProgress",
                Text = "Trạng thái render các luồng",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 4, 0, 0),
                ForeColor = Color.FromArgb(200, 204, 214),
                BackColor = Color.FromArgb(28, 30, 38),
                FlatStyle = FlatStyle.Flat,
                Visible = true
            };

            var aiRenderProgressTip = new ToolTip();
            const string aiRenderProgressTipText =
                "Mỗi «luồng» là một video đang được xử lý. Render slideshow nhiều video có thể dùng đến 3 luồng cùng lúc. Affiliate chuyên sâu / Mascot thường chỉ dùng luồng 1; luồng 2–3 sẽ hiện «đang chờ».";
            aiRenderProgressTip.SetToolTip(grpAiRenderProgress, aiRenderProgressTipText);

            tblAiRenderSlots = new TableLayoutPanel
            {
                Name = "tblAiRenderSlots",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0),
                BackColor = grpAiRenderProgress.BackColor
            };
            tblAiRenderSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            tblAiRenderSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            tblAiRenderSlots.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tblAiRenderSlots.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tblAiRenderSlots.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            lblAiRenderSlot1 = new Label
            {
                Text = "Luồng 1: đang chờ",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(130, 135, 150),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 8, 2),
                Padding = new Padding(0, 2, 0, 0)
            };
            lblAiRenderSlot2 = new Label
            {
                Text = "Luồng 2: đang chờ",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(130, 135, 150),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 8, 2),
                Padding = new Padding(0, 2, 0, 0)
            };
            lblAiRenderSlot3 = new Label
            {
                Text = "Luồng 3: đang chờ",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(130, 135, 150),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 8, 2),
                Padding = new Padding(0, 2, 0, 0)
            };

            pbAiRenderSlot1 = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                MinimumSize = new Size(80, 18),
                Margin = new Padding(0, 4, 0, 2)
            };
            pbAiRenderSlot2 = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                MinimumSize = new Size(80, 18),
                Margin = new Padding(0, 4, 0, 2)
            };
            pbAiRenderSlot3 = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                MinimumSize = new Size(80, 18),
                Margin = new Padding(0, 4, 0, 2)
            };

            tblAiRenderSlots.Controls.Add(lblAiRenderSlot1, 0, 0);
            tblAiRenderSlots.Controls.Add(pbAiRenderSlot1, 1, 0);
            tblAiRenderSlots.Controls.Add(lblAiRenderSlot2, 0, 1);
            tblAiRenderSlots.Controls.Add(pbAiRenderSlot2, 1, 1);
            tblAiRenderSlots.Controls.Add(lblAiRenderSlot3, 0, 2);
            tblAiRenderSlots.Controls.Add(pbAiRenderSlot3, 1, 2);
            grpAiRenderProgress.Controls.Add(tblAiRenderSlots);

            pnlAiVideoGenRenderStatusHost = new Panel
            {
                Name = "pnlAiVideoGenRenderStatusHost",
                Visible = false,
                Size = Size.Empty,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            grpAiRenderProgress.Dock = DockStyle.Fill;
            pnlAiVideoGenRenderStatusHost.Controls.Add(grpAiRenderProgress);

            BuildAiVideoGenModeNavigation();

            SetupManualInputUi();

            pnlSlideshowGridHost = new Panel
            {
                Name = "pnlSlideshowGridHost",
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            pnlSlideshowGridHost.Controls.Add(pnlManualInput);

            pnlDeepDiveGridHost = new Panel
            {
                Name = "pnlDeepDiveGridHost",
                Dock = DockStyle.Fill,
                Visible = false,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAiVideoGenProductArea.Controls.Add(pnlDeepDiveGridHost);
            pnlAiVideoGenProductArea.Controls.Add(pnlSlideshowGridHost);
            InitializeAiVideoGenProductGrids(pnlSlideshowGridHost, pnlDeepDiveGridHost);

            var pnlAiVideoGenWorkspace = new Panel
            {
                Name = "pnlAiVideoGenWorkspace",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            pnlAiVideoGenWorkspace.Controls.Add(pnlAiVideoGenModeHost);
            pnlAiVideoGenStickyHost.Controls.Add(pnlAiVideoGenWorkspace);

            BuildAiModeToolbarOnlyLayout(pnlModeSlideshow, out _);

            lblSlideshowReadiness = new Label
            {
                Name = "lblSlideshowReadiness",
                Text = "Đang kiểm tra cấu hình…",
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 0, 4),
                ForeColor = Color.FromArgb(255, 180, 120),
                Font = AppLabelFont
            };

            InitializeAiVideoGenToolbarControls();

            BuildAiModeToolbarOnlyLayout(pnlModeAffiliateDeep, out _);

            lblAffiliateDeepReadiness = new Label
            {
                Name = "lblAffiliateDeepReadiness",
                Text = "Đang kiểm tra cấu hình…",
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 0, 4),
                ForeColor = Color.FromArgb(255, 180, 120),
                Font = AppLabelFont
            };

            var pnlPhilosophyProgress = CreateAiModeProgressBand("pnlPhilosophyProgress");

            _philosophyQueueBindingList = new BindingList<ProductionQueueRowItem>();
            dgvPhilosophyQueue = new DataGridView
            {
                Name = "dgvPhilosophyQueue",
                Visible = false,
                DataSource = _philosophyQueueBindingList
            };
            ConfigureProductionQueueGrid(dgvPhilosophyQueue);
            dgvPhilosophyQueue.SelectionChanged += DgvPhilosophyQueue_SelectionChanged;

            _mascotQueueBindingList = new BindingList<ProductionQueueRowItem>();
            dgvMascotQueue = new DataGridView
            {
                Name = "dgvMascotQueue",
                Visible = false,
                DataSource = _mascotQueueBindingList
            };
            ConfigureProductionQueueGrid(dgvMascotQueue);

            lblPhilosophyProgress = new Label
            {
                Name = "lblPhilosophyProgress",
                Text = "Tiến trình: sẵn sàng"
            };
            ConfigureAiModeProgressLabel(lblPhilosophyProgress);
            pbPhilosophyProgress = new ProgressBar { Name = "pbPhilosophyProgress" };
            ConfigureAiModeProgressBar(pbPhilosophyProgress);
            pnlPhilosophyProgress.Controls.Add(pbPhilosophyProgress);
            pnlPhilosophyProgress.Controls.Add(lblPhilosophyProgress);

            InitializePhilosophyControls(pnlModePhilosophy, pnlPhilosophyProgress);

            lblVideoReupReadiness = new Label
            {
                Name = "lblVideoReupReadiness",
                Text = "Đang kiểm tra FFmpeg / API…",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoEllipsis = false,
                ForeColor = Color.FromArgb(255, 180, 120),
                Padding = new Padding(4, 4, 4, 4),
                Font = AppLabelFont,
                MaximumSize = new Size(900, 0)
            };

            lblVideoReupHint = new Label
            {
                Name = "lblVideoReupHint",
                Text = string.Empty,
                Visible = false
            };
            pnlVideoReupStatus = new Panel
            {
                Name = "pnlVideoReupStatus",
                Dock = DockStyle.Bottom,
                MinimumSize = new Size(0, 177),
                BackColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6, 4, 6, 4)
            };
            lblVideoReupProgress = new Label
            {
                Name = "lblVideoReupProgress",
                Text = "Tiến trình: sẵn sàng",
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(200, 206, 218),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(AppLabelFont.FontFamily, 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 8, 0),
                UseCompatibleTextRendering = true
            };
            btnVideoReupClearLog = new Button
            {
                Name = "btnVideoReupClearLog",
                Text = "Xóa log",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Right,
                MinimumSize = new Size(84, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                UseCompatibleTextRendering = true,
                Font = new Font(AppLabelFont.FontFamily, 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                Margin = Padding.Empty,
                Padding = new Padding(10, 4, 10, 4)
            };
            btnVideoReupClearLog.ApplyTheme(ButtonRole.Danger);
            btnVideoReupClearLog.Click += btnVideoReupClearLog_Click;
            var pnlReupLogHeaderBar = new Panel
            {
                Name = "pnlReupLogHeaderBar",
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 4),
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            pnlReupLogHeaderBar.Controls.Add(btnVideoReupClearLog);
            pnlReupLogHeaderBar.Controls.Add(lblVideoReupProgress);
            rtbVideoReupLog = new RichTextBox
            {
                Name = "rtbVideoReupLog",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.FromArgb(215, 222, 235),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 8.25F, FontStyle.Regular, GraphicsUnit.Point),
                Margin = Padding.Empty
            };
            ApplyAiModeLogLineSpacing(rtbVideoReupLog);
            var tblReupStatus = new TableLayoutPanel
            {
                Name = "tblReupStatus",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false
            };
            tblReupStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            tblReupStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblReupStatus.Controls.Add(pnlReupLogHeaderBar, 0, 0);
            tblReupStatus.Controls.Add(rtbVideoReupLog, 0, 1);
            pnlVideoReupStatus.Controls.Add(tblReupStatus);
            btnPushSelectionToVideoReup = CreateReupJellyButton(
                "btnPushSelectionToVideoReup",
                "Nhập từ Săn Affiliate",
                ReupTintAffiliateImport,
                172);
            btnPushSelectionToVideoReup.Click += btnPushSelectionToVideoReup_Click;

            btnVideoReupAddManualRow = CreateReupJellyButton(
                "btnVideoReupAddManualRow",
                "Thêm dòng (nhập URL)",
                ReupTintAddRow,
                168);
            btnVideoReupAddManualRow.Click += btnVideoReupAddManualRow_Click;

            btnVideoReupOpenMusicFolder = CreateReupJellyButton(
                "btnVideoReupOpenMusicFolder",
                "🎵 Thư viện nhạc nền",
                ReupTintFolder,
                168);
            btnVideoReupOpenMusicFolder.Click += btnVideoReupOpenMusicFolder_Click;

            btnVideoReupOpenLogoLibrary = CreateReupJellyButton(
                "btnVideoReupOpenLogoLibrary",
                "🏷 Thư viện logo",
                Color.FromArgb(72, 118, 168),
                148);
            btnVideoReupOpenLogoLibrary.Click += btnVideoReupOpenLogoLibrary_Click;

            pnlReupGridToolbar = new Panel
            {
                Name = "pnlReupGridToolbar",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 2, 0, 6),
                BackColor = Color.Transparent
            };
            var flpReupToolbar = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupToolbar.Dock = DockStyle.Top;
            flpReupToolbar.AutoSize = true;
            flpReupToolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flpReupToolbar.Controls.Add(CreateClearGridButton());
            btnVideoReupOpenOutput = CreateOpenOutputFolderButton();
            btnVideoReupOpenOutput.Name = "btnVideoReupOpenOutput";
            btnVideoReupOpenOutput.Text = "📂 Output Reup";
            btnVideoReupOpenOutput.Click -= btnOpenOutputFolder_Click;
            btnVideoReupOpenOutput.Click += btnVideoReupOpenOutput_Click;
            flpReupToolbar.Controls.Add(btnVideoReupOpenOutput);
            pnlReupGridToolbar.Controls.Add(flpReupToolbar);

            _videoReupBindingList = new BindingList<VideoReupRowItem>();
            dgvVideoReupInput = new DataGridView
            {
                Name = "dgvVideoReupInput",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(180, 100),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ScrollBars = ScrollBars.Both
            };
            ConfigureVideoReupInputGrid();
            RefreshAllProfileSelectors();
            dgvVideoReupInput.DataSource = _videoReupBindingList;

            pnlReupEditor = new Panel
            {
                Name = "pnlReupEditor",
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = new Padding(4, 0, 4, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            dgvVideoReupInput.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            dgvVideoReupInput.DefaultCellStyle.ForeColor = Color.Gainsboro;
            dgvVideoReupInput.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvVideoReupInput.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            dgvVideoReupInput.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            ApplyAppComboGridRowHeight(dgvVideoReupInput);
            ApplyAppGridChrome(dgvVideoReupInput);
            dgvVideoReupInput.DataBindingComplete += DgvVideoReupInput_DataBindingComplete;
            dgvVideoReupInput.CurrentCellDirtyStateChanged += VideoReupInputGrid_CurrentCellDirtyStateChanged;
            dgvVideoReupInput.DataError += VideoReupInputGrid_DataError;
            dgvVideoReupInput.CellFormatting += DgvVideoReupInput_CellFormatting;
            dgvVideoReupInput.CellBeginEdit += DgvVideoReupInput_CellBeginEdit;
            dgvVideoReupInput.CellEndEdit += DgvVideoReupInput_CellEndEdit;
            dgvVideoReupInput.RowPrePaint += DgvVideoReupInput_RowPrePaint;
            dgvVideoReupInput.CellClick += DgvVideoReupInput_EditorCellClick;
            dgvVideoReupInput.KeyDown += dgvVideoReupInput_KeyDown;
            dgvVideoReupInput.SelectionChanged += dgvVideoReupInput_SelectionChanged;
            dgvVideoReupInput.EditingControlShowing += DgvVideoReupInput_EditingControlShowing;

            var pnlReupUrlSection = new Panel
            {
                Name = "pnlReupUrlSection",
                Dock = DockStyle.Fill,
                Height = 26,
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            lblVideoReupVideoUrl = new Label
            {
                Text = "URL video:",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = Color.FromArgb(200, 204, 214),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var pnlReupUrlTextHost = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 26,
                Padding = new Padding(0)
            };
            txtVideoReupVideoUrl = new TextBox
            {
                Name = "txtVideoReupVideoUrl",
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(28, 30, 38),
                ForeColor = Color.Gainsboro
            };
            txtVideoReupVideoUrl.Leave += txtVideoReupVideoUrl_Leave;
            pnlReupUrlTextHost.Controls.Add(txtVideoReupVideoUrl);
            var tblReupUrlRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Height = 26
            };
            tblReupUrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            tblReupUrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblReupUrlRow.Controls.Add(lblVideoReupVideoUrl, 0, 0);
            tblReupUrlRow.Controls.Add(pnlReupUrlTextHost, 1, 0);
            pnlReupUrlSection.Controls.Add(tblReupUrlRow);

            lblVideoReupHook = new Label
            {
                Name = "lblVideoReupHook",
                Text = string.Empty,
                Visible = false
            };
            btnVideoReupHookGemini = CreateReupJellyButton(
                "btnVideoReupGeminiAll",
                "Tạo Hook+Script+Hashtag",
                ReupTintAffiliateImport,
                168);
            btnVideoReupHookGemini.Click += btnVideoReupHookGemini_Click;
            btnVideoReupLyriaHook = CreateToolbarButton("Voiceover hook", executeStyle: false, minWidth: 118);
            btnVideoReupLyriaHook.Name = "btnVideoReupLyriaHook";
            btnVideoReupLyriaHook.BackColor = Color.FromArgb(120, 70, 160);
            btnVideoReupLyriaHook.Height = 28;
            btnVideoReupLyriaHook.Click += btnVideoReupLyriaHook_Click;
            var flpReupHookActions = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupHookActions.Dock = DockStyle.Fill;
            flpReupHookActions.Controls.Add(btnVideoReupHookGemini);
            flpReupHookActions.Controls.Add(btnVideoReupLyriaHook);

            grpVideoReupAudioMode = new GroupBox
            {
                Name = "grpVideoReupAudioMode",
                Text = "Loại video (sau hook)",
                Dock = DockStyle.Fill,
                AutoSize = false,
                MinimumSize = new Size(0, 62),
                ForeColor = Color.FromArgb(200, 204, 214),
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(8, 4, 8, 4)
            };
            rbVideoReupAudioAffiliate = new RadioButton
            {
                Name = "rbVideoReupAudioAffiliate",
                Text = "Hook + nhạc nền (Affiliate)",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 16, 0),
                ForeColor = Color.Gainsboro,
                Checked = true
            };
            rbVideoReupAudioFilm = new RadioButton
            {
                Name = "rbVideoReupAudioFilm",
                Text = "Hook + phim (giữ tiếng gốc video)",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 0, 0),
                ForeColor = Color.Gainsboro
            };
            rbVideoReupAudioAffiliate.CheckedChanged += VideoReupAudioModeRadio_CheckedChanged;
            rbVideoReupAudioFilm.CheckedChanged += VideoReupAudioModeRadio_CheckedChanged;
            var tblReupAudioMode = new TableLayoutPanel
            {
                Name = "tblReupAudioMode",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(6, 2, 4, 0),
                BackColor = Color.Transparent
            };
            tblReupAudioMode.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblReupAudioMode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblReupAudioMode.Controls.Add(rbVideoReupAudioAffiliate, 0, 0);
            tblReupAudioMode.Controls.Add(rbVideoReupAudioFilm, 1, 0);
            grpVideoReupAudioMode.Controls.Add(tblReupAudioMode);
            lblVideoReupMusicPick = new Label
            {
                Text = "Nhạc:",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(0, 4, 6, 0)
            };
            cbVideoReupMusic = new ComboBox
            {
                Name = "cbVideoReupMusic",
                Width = 180,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = AppInputFont,
                IntegralHeight = false,
                Margin = new Padding(0, 2, 6, 2)
            };
            cbVideoReupMusic.SelectedIndexChanged += cbVideoReupMusic_SelectedIndexChanged;
            btnVideoReupOpenHookSfxFolder = CreateReupJellyButton(
                "btnVideoReupOpenHookSfxFolder",
                "🔊 Hiệu ứng âm thanh",
                ReupTintFolder,
                168);
            btnVideoReupOpenHookSfxFolder.Click += btnVideoReupOpenHookSfxFolder_Click;
            lblVideoReupMusicPathHint = new Label
            {
                Name = "lblVideoReupMusicPathHint",
                Text = string.Empty,
                Visible = false
            };

            var flpReupMusicRow = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupMusicRow.Dock = DockStyle.Fill;
            flpReupMusicRow.Visible = false;

            pnlReupMusicLibraryPath = CreateSettingsCompactPathRow(
                out txtVideoReupMusicLibraryPath,
                out btnBrowseVideoReupMusicLibrary,
                "Thư viện nhạc",
                "txtVideoReupMusicLibraryPath",
                btnBrowseVideoReupMusicLibrary_Click,
                "Duyệt",
                "btnBrowseVideoReupMusicLibrary");
            pnlReupMusicLibraryPath.Name = "pnlReupMusicLibraryPath";
            pnlReupMusicLibraryPath.Dock = DockStyle.Fill;
            pnlReupMusicLibraryPath.Margin = new Padding(0, 2, 0, 0);
            pnlReupMusicLibraryPath.Visible = false;

            var tblReupEditor = new TableLayoutPanel
            {
                Name = "tblReupEditor",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = pnlReupEditor.BackColor,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            tblReupEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            tblReupEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            tblReupEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            tblReupEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            tblReupEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tblReupEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tblReupEditor.Controls.Add(pnlReupUrlSection, 0, 0);
            tblReupEditor.SetColumnSpan(pnlReupUrlSection, 2);
            tblReupEditor.Controls.Add(grpVideoReupAudioMode, 0, 1);
            tblReupEditor.SetColumnSpan(grpVideoReupAudioMode, 2);
            tblReupEditor.Controls.Add(flpReupHookActions, 0, 2);
            tblReupEditor.Controls.Add(flpReupMusicRow, 1, 2);
            tblReupEditor.Controls.Add(pnlReupMusicLibraryPath, 0, 3);
            tblReupEditor.SetColumnSpan(pnlReupMusicLibraryPath, 2);
            pnlReupEditor.Controls.Add(tblReupEditor);
            pnlReupEditor.AutoScroll = false;
            pnlReupEditor.AutoScrollMinSize = Size.Empty;

            pnlModeVideoReup.AutoScroll = false;
            pnlModeVideoReup.Padding = new Padding(4);
            WireVideoReupTabLayout();
            RefreshVideoReupReadinessLabel(null);
            InitializeVideoReupDraftAutoSave();

            tabMascotStory = pnlModeMascot;
            BuildMascotStoryTabUi();
            LoadMascotIdentityPackForSelectedProfile();

            _aiVideoScriptBindingList = new BindingList<AiVideoScriptReviewItem>();
            dgvAiVideoScriptReview = new DataGridView
            {
                Name = "dgvAiVideoScriptReview",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(180, AppGridHeaderHeight + 44),
                AutoGenerateColumns = true,
                DataSource = _aiVideoScriptBindingList,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                GridColor = Color.FromArgb(64, 68, 82),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgvAiVideoScriptReview.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            dgvAiVideoScriptReview.DefaultCellStyle.ForeColor = Color.Gainsboro;
            dgvAiVideoScriptReview.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            dgvAiVideoScriptReview.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            ApplyAppGridChrome(dgvAiVideoScriptReview);
            dgvAiVideoScriptReview.DataBindingComplete += DgvAiVideoScriptReview_DataBindingComplete;
            dgvAiVideoScriptReview.SelectionChanged += dgvAiVideoScriptReview_SelectionChanged;

            lblProductionPreviewCaption = new Label
            {
                Name = "lblProductionPreviewCaption",
                Text = "Script Preview | Video Preview",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Margin = new Padding(0, 0, 0, 2),
                ForeColor = Color.FromArgb(170, 175, 188)
            };
            pbProductionVideoPreview = new PictureBox
            {
                Name = "pbProductionVideoPreview",
                Dock = DockStyle.Fill,
                Visible = false,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(16, 18, 24),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4, 0, 0, 0)
            };
            pnlAffiliateDeepStoryboardHost = new Panel
            {
                Name = "pnlAffiliateDeepStoryboardHost",
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(24, 26, 34)
            };
            BuildAffiliateDeepStoryboardUi(pnlAffiliateDeepStoryboardHost);

            txtAiVideoGenPrompt = new TextBox
            {
                Name = "txtAiVideoGenPrompt",
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtAiVideoGenPrompt.TextChanged += txtAiVideoGenPrompt_TextChanged;
            txtAiVideoGenPrompt.TextChanged += (_, __) => NotifySlideshowDraftDirty();

            WireAiVideoGenScriptPanelLayout();
            WireAiVideoGenActionBar();
            WireAffiliateDeepRootLayout();
            tabAiVideoGen.SuspendLayout();
            WireAiVideoGenTabTableLayout();

            tabAiVideoGen.Resize += TabAiVideoGen_Resize;
            SelectAiVideoGenMode(AiVideoGenMode.Slideshow);
            InitializeSlideshowDraftAutoSave();
            RefreshPhilosophyPrereqLabel(null);
            RefreshAiVideoGenModeReadinessLabels(null);
            tabAiVideoGen.AutoScroll = false;
            tabAiVideoGen.AutoScrollMinSize = Size.Empty;
            tabAiVideoGen.ResumeLayout(true);
            tabAiVideoGen.PerformLayout();
        }

        private void TabAiVideoGen_Resize(object sender, EventArgs e)
        {
            if (tabAiVideoGen == null || !tabAiVideoGen.Visible)
            {
                return;
            }

            var idx = GetSelectedAiVideoGenModeIndex();
            ApplyAiVideoGenShellLayout(idx == 0 || idx == 1, idx);
        }

        private static FlowLayoutPanel CreateAiVideoGenActionFlowPanel()
        {
            return new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 2, 0, 2),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
        }

        private static Panel BuildAiVideoGenModeActionPanel(FlowLayoutPanel dataRow, FlowLayoutPanel executeRow)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.Controls.Add(dataRow, 0, 0);
            tbl.Controls.Add(executeRow, 0, 1);
            host.Controls.Add(tbl);
            return host;
        }

        private void WireAiVideoGenScriptPanelLayout()
        {
            if (pnlAiVideoGenScriptHost == null)
            {
                return;
            }

            pnlAiVideoGenScriptHost.SuspendLayout();
            pnlAiVideoGenScriptHost.Controls.Clear();

            tblAiVideoGenScriptInner = new TableLayoutPanel
            {
                Name = "tblAiVideoGenScriptInner",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAiVideoGenScriptHost.BackColor
            };
            tblAiVideoGenScriptInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAiVideoGenScriptInner.RowStyles.Add(new RowStyle(SizeType.Percent, 32F));
            tblAiVideoGenScriptInner.RowStyles.Add(new RowStyle(SizeType.Percent, 68F));
            tblAiVideoGenScriptInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblProductionPreviewCaption.Dock = DockStyle.Fill;
            tblAiVideoGenScriptInner.Controls.Add(lblProductionPreviewCaption, 0, 0);
            tblAiVideoGenScriptInner.Controls.Add(dgvAiVideoScriptReview, 0, 1);
            ApplyAppGridChrome(dgvAiVideoScriptReview);

            var pnlPromptPreview = new Panel
            {
                Name = "pnlAiVideoGenPromptPreview",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAiVideoGenScriptHost.BackColor
            };
            var tblPrompt = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlPromptPreview.BackColor
            };
            tblPrompt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblPrompt.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblPrompt.Controls.Add(txtAiVideoGenPrompt, 0, 0);
            tblPrompt.Controls.Add(pbProductionVideoPreview, 1, 0);
            pnlPromptPreview.Controls.Add(tblPrompt);
            tblAiVideoGenScriptInner.Controls.Add(pnlPromptPreview, 0, 2);
            tblAiVideoGenScriptInner.Controls.Add(pnlAffiliateDeepStoryboardHost, 0, 3);

            pnlAiVideoGenScriptHost.Controls.Add(tblAiVideoGenScriptInner);
            pnlAiVideoGenScriptHost.ResumeLayout(true);
        }

        private void WireAiVideoGenActionBar()
        {
            pnlAiVideoGenActionBar = new Panel
            {
                Name = "pnlAiVideoGenActionBar",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 6),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            tblAiVideoGenActionInner = new TableLayoutPanel
            {
                Name = "tblAiVideoGenActionInner",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAiVideoGenActionBar.BackColor
            };
            tblAiVideoGenActionInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAiVideoGenActionInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblSlideshowReadiness.Margin = new Padding(0);
            lblAffiliateDeepReadiness.Margin = new Padding(0);

            pnlAiVideoGenReadinessHost = new Panel
            {
                Name = "pnlAiVideoGenReadinessHost",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = pnlAiVideoGenActionBar.BackColor
            };
            lblSlideshowReadiness.Dock = DockStyle.Fill;
            pnlAiVideoGenReadinessHost.Controls.Add(lblSlideshowReadiness);

            tblAiVideoGenActionInner.Controls.Add(pnlAiVideoGenReadinessHost, 0, 0);
            tblAiVideoGenActionInner.Controls.Add(pnlSlideshowActionBar, 0, 1);
            pnlAiVideoGenActionBar.Controls.Add(tblAiVideoGenActionInner);
        }

        private void WireAiVideoGenTabTableLayout()
        {
            if (tabAiVideoGen == null)
            {
                return;
            }

            tabAiVideoGen.SuspendLayout();
            try
            {
                foreach (var c in tabAiVideoGen.Controls.Cast<Control>().ToArray())
                {
                    if (c != null && c != tblAiVideoGenRoot)
                    {
                        tabAiVideoGen.Controls.Remove(c);
                    }
                }

                if (tblAiVideoGenRoot != null)
                {
                    tblAiVideoGenRoot.Controls.Clear();
                    tblAiVideoGenRoot.Dispose();
                }

                tblAiVideoGenRoot = new TableLayoutPanel
                {
                    Name = "tblAiVideoGenRoot",
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = tabAiVideoGen.BackColor,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                tblAiVideoGenRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 7F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 51F));

                flpAiVideoGenHeader.Dock = DockStyle.Fill;
                pnlAiVideoGenProductArea.Dock = DockStyle.Fill;
                pnlAiVideoGenScriptHost.Dock = DockStyle.Fill;
                pnlAiVideoGenStickyHost.Dock = DockStyle.Fill;
                if (pnlAiVideoGenTopHeader != null)
                {
                    pnlAiVideoGenTopHeader.Dock = DockStyle.Fill;
                }

                var headerHost = pnlAiVideoGenTopHeader ?? (Control)flpAiVideoGenHeader;
                tblAiVideoGenRoot.Controls.Add(headerHost, 0, 0);
                tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenProductArea, 0, 1);
                tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenScriptHost, 0, 2);

                WireAiVideoGenLayout(tabAiVideoGen);

                tabAiVideoGen.AutoScroll = false;
                tabAiVideoGen.AutoScrollMinSize = Size.Empty;
            }
            finally
            {
                tabAiVideoGen.ResumeLayout(true);
                tabAiVideoGen.PerformLayout();
            }
        }

        private void WireAiVideoGenTabShell()
        {
            WireAiVideoGenLayout(tabAiVideoGen);
        }

        private void ApplyAiVideoGenTableRowHeights(bool showProductGrid, int modeTabIndex)
        {
            if (tblAiVideoGenRoot == null || tblAiVideoGenRoot.RowStyles.Count < 3)
            {
                return;
            }

            void SetRow(int index, SizeType sizeType, float height)
            {
                tblAiVideoGenRoot.RowStyles[index].SizeType = sizeType;
                tblAiVideoGenRoot.RowStyles[index].Height = height;
            }

            if (showProductGrid)
            {
                if (modeTabIndex == 1)
                {
                    // Showcase: sidebar đã có tên mode + toolbar riêng — ẩn hẳn khối tiêu đề trùng lặp phía trên lưới.
                    SetRow(0, SizeType.Absolute, 0F);
                    SetRow(1, SizeType.Percent, 100F);
                    SetRow(2, SizeType.Absolute, 0F);

                    if (pnlAiVideoGenTopHeader != null)
                    {
                        pnlAiVideoGenTopHeader.Visible = false;
                    }

                    if (flpAiVideoGenHeader != null)
                    {
                        flpAiVideoGenHeader.Visible = false;
                    }

                    if (pnlAiVideoGenModeIndicator != null)
                    {
                        pnlAiVideoGenModeIndicator.Visible = false;
                    }
                }
                else
                {
                    SetRow(0, SizeType.Percent, 7F);
                    SetRow(1, SizeType.Percent, 42F);
                    SetRow(2, SizeType.Percent, 51F);

                    if (pnlAiVideoGenTopHeader != null)
                    {
                        pnlAiVideoGenTopHeader.Visible = true;
                    }

                    if (flpAiVideoGenHeader != null)
                    {
                        flpAiVideoGenHeader.Visible = true;
                    }

                    if (pnlAiVideoGenModeIndicator != null)
                    {
                        pnlAiVideoGenModeIndicator.Visible = true;
                    }
                }

                if (pnlAiVideoGenStickyHost != null)
                {
                    pnlAiVideoGenStickyHost.Visible = false;
                }

                if (modeTabIndex == 1)
                {
                    if (pnlAiVideoGenProductArea != null)
                    {
                        pnlAiVideoGenProductArea.Visible = false;
                    }

                    if (pnlAiVideoGenScriptHost != null)
                    {
                        pnlAiVideoGenScriptHost.Visible = false;
                    }

                    if (pnlAiVideoGenActionBar != null)
                    {
                        pnlAiVideoGenActionBar.Visible = false;
                    }
                }
                else
                {
                    if (pnlAiVideoGenProductArea != null)
                    {
                        pnlAiVideoGenProductArea.Visible = true;
                        if (pnlAiVideoGenProductArea.Parent != tblAiVideoGenRoot)
                        {
                            tblAiVideoGenRoot.Controls.Remove(pnlAiVideoGenStickyHost);
                            tblAiVideoGenRoot.Controls.Remove(pnlAffiliateDeepRoot);
                            tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenProductArea, 0, 1);
                        }
                    }

                    if (pnlAiVideoGenScriptHost != null)
                    {
                        pnlAiVideoGenScriptHost.Visible = true;
                    }

                    if (pnlAiVideoGenActionBar != null)
                    {
                        pnlAiVideoGenActionBar.Visible = true;
                    }
                }

                if (grpAiRenderProgress != null)
                {
                    grpAiRenderProgress.Visible = false;
                }

                ApplyAffiliateDeepControlHosts(modeTabIndex);
                SyncAiVideoGenActionBarForMode(modeTabIndex);
            }
            else
            {
                SetRow(0, SizeType.Absolute, 0F);
                SetRow(1, SizeType.Percent, 100F);
                SetRow(2, SizeType.Absolute, 0F);

                if (flpAiVideoGenHeader != null)
                {
                    flpAiVideoGenHeader.Visible = false;
                }

                if (pnlAiVideoGenProductArea != null)
                {
                    pnlAiVideoGenProductArea.Visible = false;
                }

                if (pnlAiVideoGenScriptHost != null)
                {
                    pnlAiVideoGenScriptHost.Visible = false;
                }

                if (pnlAiVideoGenStickyHost != null)
                {
                    pnlAiVideoGenStickyHost.Visible = true;
                    tblAiVideoGenRoot.Controls.Remove(pnlAiVideoGenProductArea);
                    tblAiVideoGenRoot.Controls.Remove(pnlAffiliateDeepRoot);
                    if (!tblAiVideoGenRoot.Controls.Contains(pnlAiVideoGenStickyHost))
                    {
                        tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenStickyHost, 0, 1);
                    }
                }

                if (pnlAffiliateDeepRoot != null)
                {
                    pnlAffiliateDeepRoot.Visible = false;
                }

                if (pnlAiVideoGenActionBar != null)
                {
                    pnlAiVideoGenActionBar.Visible = false;
                }

                if (grpAiRenderProgress != null)
                {
                    grpAiRenderProgress.Visible = false;
                }

                ApplyAffiliateDeepControlHosts(modeTabIndex);
                SyncAiVideoGenActionBarForMode(modeTabIndex);
            }

            tblAiVideoGenRoot.PerformLayout();
        }

        private void WireAffiliateDeepRootLayout()
        {
            if (_affiliateDeepRootWired || tabAiVideoGen == null)
            {
                return;
            }

            _affiliateDeepRootWired = true;

            if (_aiVideoGenControls == null)
            {
                InitializeAiVideoGenToolbarControls();
            }

            flpAffiliateDeepHeaderActions = _aiVideoGenControls.AffiliateDeepHeaderActions;
            flpAffiliateDeepExecuteActions = _aiVideoGenControls.AffiliateDeepExecuteActions;

            pnlShowcaseTabTitleHost = new Panel
            {
                Name = "pnlShowcaseTabTitleHost",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 64,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(0, 0, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var pnlShowcaseTitleAccent = new Panel
            {
                Name = "pnlShowcaseTitleAccent",
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = Color.FromArgb(210, 158, 32)
            };

            lblShowcaseTabTitle = new ShowcaseTabTitleLabel
            {
                Name = "lblShowcaseTabTitle",
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            pnlShowcaseTabTitleHost.Controls.Add(pnlShowcaseTitleAccent);
            pnlShowcaseTabTitleHost.Controls.Add(lblShowcaseTabTitle);

            pnlAffiliateDeepReadinessHost = new Panel
            {
                Name = "pnlAffiliateDeepReadinessHost",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(4, 6, 8, 2),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            if (lblAffiliateDeepReadiness != null)
            {
                lblAffiliateDeepReadiness.AutoSize = true;
                lblAffiliateDeepReadiness.AutoEllipsis = false;
                lblAffiliateDeepReadiness.Dock = DockStyle.Top;
                lblAffiliateDeepReadiness.MaximumSize = new Size(900, 0);
                lblAffiliateDeepReadiness.Margin = new Padding(0);
                pnlAffiliateDeepReadinessHost.Controls.Add(lblAffiliateDeepReadiness);
                WireAffiliateDeepReadinessWrap(lblAffiliateDeepReadiness, pnlAffiliateDeepReadinessHost);
            }

            pnlAffiliateDeepHeaderHost = new Panel
            {
                Name = "pnlAffiliateDeepHeaderHost",
                Dock = DockStyle.Fill,
                AutoSize = false,
                MinimumSize = new Size(0, AppJellyButtonHeight * 2 + 18),
                Padding = new Padding(4, 2, 8, 2),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var tblHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAffiliateDeepHeaderHost.BackColor
            };
            tblHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            if (flpAffiliateDeepHeaderActions != null)
            {
                flpAffiliateDeepHeaderActions.Dock = DockStyle.Fill;
                flpAffiliateDeepHeaderActions.Margin = new Padding(0);
            }

            tblHeader.Controls.Add(flpAffiliateDeepHeaderActions, 0, 0);
            pnlAffiliateDeepHeaderHost.Controls.Add(tblHeader);

            pnlAffiliateDeepExecuteHost = new Panel
            {
                Name = "pnlAffiliateDeepExecuteHost",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(4, 4, 8, 6),
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            if (flpAffiliateDeepExecuteActions != null)
            {
                flpAffiliateDeepExecuteActions.Dock = DockStyle.Fill;
                flpAffiliateDeepExecuteActions.Margin = new Padding(0);
                pnlAffiliateDeepExecuteHost.Controls.Add(flpAffiliateDeepExecuteActions);
            }

            pnlAffiliateDeepProductGridHost = new Panel
            {
                Name = "pnlAffiliateDeepProductGridHost",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAffiliateDeepStoryboardSlot = new Panel
            {
                Name = "pnlAffiliateDeepStoryboardSlot",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAffiliateDeepProductHost = new Panel
            {
                Name = "pnlAffiliateDeepProductHost",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var tblProduct = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAffiliateDeepProductHost.BackColor
            };
            tblProduct.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblProduct.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblProduct.Controls.Add(pnlAffiliateDeepProductGridHost, 0, 0);
            tblProduct.Controls.Add(pnlAffiliateDeepStoryboardSlot, 0, 1);
            pnlAffiliateDeepProductHost.Controls.Add(tblProduct);

            BuildShowcaseLogPanel();

            // 6 hàng: tiêu đề tab | readiness | toolbar chuẩn bị | lưới/storyboard | nút clip/render | nhật ký.
            // Lưới "Duyệt script" + ô prompt lớn (dgvAiVideoScriptReview/txtAiVideoGenPrompt) là tàn tích của luồng
            // Veo tự động cũ — không còn dùng cho Showcase (luồng clip Veo thủ công) nên không mount vào đây nữa.
            tblAffiliateDeepRoot = new TableLayoutPanel
            {
                Name = "tblAffiliateDeepRoot",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0),
                Padding = new Padding(8, 0, 8, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            tblAffiliateDeepRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, AppJellyButtonHeight * 2 + 18F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 280F));
            pnlAffiliateDeepReadinessHost.Visible = false;
            tblAffiliateDeepRoot.Controls.Add(pnlShowcaseTabTitleHost, 0, 0);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepReadinessHost, 0, 1);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepHeaderHost, 0, 2);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepProductHost, 0, 3);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepExecuteHost, 0, 4);
            if (pnlShowcaseLogHost != null)
            {
                tblAffiliateDeepRoot.Controls.Add(pnlShowcaseLogHost, 0, 5);
            }

            pnlAffiliateDeepRoot = new Panel
            {
                Name = "pnlAffiliateDeepRoot",
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            pnlAffiliateDeepRoot.Controls.Add(tblAffiliateDeepRoot);

            if (pnlAffiliateDeepStoryboardHost != null)
            {
                pnlAffiliateDeepStoryboardHost.Dock = DockStyle.Fill;
            }
        }

        private void BuildShowcaseLogPanel()
        {
            if (pnlShowcaseLogHost != null && !pnlShowcaseLogHost.IsDisposed)
            {
                return;
            }

            if (btnShowcaseClearLog == null || btnShowcaseClearLog.IsDisposed)
            {
                btnShowcaseClearLog = new Button
                {
                    Name = "btnShowcaseClearLog",
                    Text = "Xóa log",
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.FromArgb(60, 64, 77),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.WhiteSmoke,
                    TextAlign = ContentAlignment.MiddleCenter,
                    UseCompatibleTextRendering = true,
                    Font = new Font(AppLabelFont.FontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
                    MinimumSize = new Size(0, 38),
                    Padding = new Padding(12, 6, 12, 6)
                };
                btnShowcaseClearLog.FlatAppearance.BorderSize = 0;
            }

            btnShowcaseClearLog.Click -= btnShowcaseClearLog_Click;
            btnShowcaseClearLog.Click += btnShowcaseClearLog_Click;

            if (rtbShowcaseLog == null || rtbShowcaseLog.IsDisposed)
            {
                rtbShowcaseLog = CreateAiModeLogTextBox("rtbShowcaseLog");
                ApplyAiModeLogLineSpacing(rtbShowcaseLog);
                rtbShowcaseLog.Font = new Font("Consolas", 8.25F);
            }

            rtbShowcaseLog.Dock = DockStyle.Fill;

            pnlShowcaseLogHost = new Panel
            {
                Name = "pnlShowcaseLogHost",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 240),
                BackColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };

            var tblLog = new TableLayoutPanel
            {
                Name = "tblShowcaseLog",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            tblLog.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            tblLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 6, 8),
                MinimumSize = new Size(0, 58)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var lblLog = new Label
            {
                Text = "Nhật ký tạo video",
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = false,
                ForeColor = Color.FromArgb(200, 204, 214),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(AppLabelFont.FontFamily, 12F, FontStyle.Regular, GraphicsUnit.Point),
                Padding = new Padding(0, 4, 0, 4),
                UseCompatibleTextRendering = true
            };
            btnShowcaseClearLog.Dock = DockStyle.None;
            btnShowcaseClearLog.AutoSize = true;
            btnShowcaseClearLog.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnShowcaseClearLog.MinimumSize = new Size(0, 38);
            btnShowcaseClearLog.Margin = new Padding(8, 0, 0, 0);
            header.Controls.Add(lblLog, 0, 0);
            header.Controls.Add(btnShowcaseClearLog, 1, 0);

            tblLog.Controls.Add(header, 0, 0);
            tblLog.Controls.Add(rtbShowcaseLog, 0, 1);
            pnlShowcaseLogHost.Controls.Add(tblLog);
        }

        private static void WireAffiliateDeepReadinessWrap(Label label, Control widthHost)
        {
            if (label == null || widthHost == null)
            {
                return;
            }

            void ApplyMaxWidth()
            {
                var w = Math.Max(200, widthHost.ClientSize.Width - widthHost.Padding.Horizontal);
                label.MaximumSize = new Size(w, 0);
            }

            ApplyMaxWidth();
            widthHost.Resize += (_, __) => ApplyMaxWidth();
            if (widthHost.Parent != null)
            {
                widthHost.Parent.Resize += (_, __) => ApplyMaxWidth();
            }
        }

        /// <summary>Chuyển panel tham số render dùng chung (đa giọng đọc/cỡ chữ/nhạc/chuyển cảnh) sang hàng Execute
        /// của Slideshow hoặc Showcase tuỳ theo tab đang chọn — không set Dock=Fill vì đích là FlowLayoutPanel.</summary>
        private void MountSharedRenderParamsPanel(FlowLayoutPanel targetRow)
        {
            var panel = _aiVideoGenControls?.SharedRenderParamsPanel;
            if (panel == null || targetRow == null || panel.Parent == targetRow)
            {
                return;
            }

            panel.Parent?.Controls.Remove(panel);
            targetRow.Controls.Add(panel);
        }

        private static void MountAiVideoGenControl(Control control, Control parent)
        {
            if (control == null || parent == null)
            {
                return;
            }

            if (control.Parent == parent)
            {
                control.Dock = DockStyle.Fill;
                return;
            }

            control.Parent?.Controls.Remove(control);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0);
            parent.Controls.Add(control);
            control.BringToFront();
        }

        private void UnmountAffiliateDeepControls()
        {
            if (pnlAffiliateDeepRoot != null)
            {
                pnlAffiliateDeepRoot.Visible = false;
                if (tblAiVideoGenRoot != null && pnlAffiliateDeepRoot.Parent == tblAiVideoGenRoot)
                {
                    tblAiVideoGenRoot.Controls.Remove(pnlAffiliateDeepRoot);
                }
            }

            if (pnlAffiliateDeepStoryboardHost != null)
            {
                pnlAffiliateDeepStoryboardHost.Visible = false;
            }
        }

        private void ApplyAffiliateDeepControlHosts(int modeTabIndex)
        {
            if (!_affiliateDeepRootWired)
            {
                WireAffiliateDeepRootLayout();
            }

            if (modeTabIndex == 1)
            {
                MountAffiliateDeepControls();
                return;
            }

            if (modeTabIndex == 0)
            {
                MountSlideshowAiVideoGenControls();
                return;
            }

            UnmountAffiliateDeepControls();
        }

        private void MountAffiliateDeepControls()
        {
            if (pnlAffiliateDeepRoot == null || tblAiVideoGenRoot == null)
            {
                return;
            }

            pnlAffiliateDeepRoot.Visible = true;
            if (pnlAffiliateDeepRoot.Parent != tblAiVideoGenRoot)
            {
                tblAiVideoGenRoot.Controls.Add(pnlAffiliateDeepRoot, 0, 1);
            }

            if (pnlAiVideoGenProductArea != null)
            {
                pnlAiVideoGenProductArea.Visible = false;
            }

            if (pnlAiVideoGenScriptHost != null)
            {
                pnlAiVideoGenScriptHost.Visible = false;
            }

            WireShowcaseProductGridLayout();
            MountAiVideoGenControl(pnlAffiliateDeepStoryboardHost, pnlAffiliateDeepStoryboardSlot);
            ApplyDeepDiveGridColumnVisibility(showcaseMode: true);
            SyncBuffersToGrids();

            if (lblAffiliateDeepReadiness != null && pnlAffiliateDeepReadinessHost != null
                && lblAffiliateDeepReadiness.Parent != pnlAffiliateDeepReadinessHost)
            {
                lblAffiliateDeepReadiness.Parent?.Controls.Remove(lblAffiliateDeepReadiness);
                lblAffiliateDeepReadiness.AutoSize = true;
                lblAffiliateDeepReadiness.AutoEllipsis = false;
                lblAffiliateDeepReadiness.Dock = DockStyle.Top;
                pnlAffiliateDeepReadinessHost.Controls.Add(lblAffiliateDeepReadiness);
                WireAffiliateDeepReadinessWrap(lblAffiliateDeepReadiness, pnlAffiliateDeepReadinessHost);
            }

            if (pnlAffiliateDeepStoryboardHost != null)
            {
                pnlAffiliateDeepStoryboardHost.Visible = true;
            }

            tblAffiliateDeepRoot?.PerformLayout();
        }

        private void MountSlideshowAiVideoGenControls()
        {
            if (pnlAffiliateDeepRoot != null)
            {
                pnlAffiliateDeepRoot.Visible = false;
                if (pnlAffiliateDeepRoot.Parent == tblAiVideoGenRoot)
                {
                    tblAiVideoGenRoot.Controls.Remove(pnlAffiliateDeepRoot);
                }
            }

            if (pnlAiVideoGenProductArea != null)
            {
                pnlAiVideoGenProductArea.Visible = true;
                if (tblAiVideoGenRoot != null && pnlAiVideoGenProductArea.Parent != tblAiVideoGenRoot)
                {
                    tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenProductArea, 0, 1);
                }
            }

            if (pnlAiVideoGenScriptHost != null)
            {
                pnlAiVideoGenScriptHost.Visible = true;
            }

            RestoreSlideshowScriptPanelLayout();
            WireSlideshowProductGridLayout();
            WireDeepDiveProductGridLayout();
            MountSharedRenderParamsPanel(flpSlideshowExecute);
            ApplyDeepDiveGridColumnVisibility(showcaseMode: false);

            if (lblAffiliateDeepReadiness != null && pnlAiVideoGenReadinessHost != null
                && lblAffiliateDeepReadiness.Parent != pnlAiVideoGenReadinessHost)
            {
                lblAffiliateDeepReadiness.Parent?.Controls.Remove(lblAffiliateDeepReadiness);
                lblAffiliateDeepReadiness.Dock = DockStyle.Fill;
                lblAffiliateDeepReadiness.AutoSize = false;
                lblAffiliateDeepReadiness.AutoEllipsis = true;
                pnlAiVideoGenReadinessHost.Controls.Add(lblAffiliateDeepReadiness);
            }

            if (pnlAffiliateDeepStoryboardHost != null)
            {
                pnlAffiliateDeepStoryboardHost.Visible = false;
            }
        }

        private void RestoreSlideshowScriptPanelLayout()
        {
            if (tblAiVideoGenScriptInner == null)
            {
                return;
            }

            if (dgvAiVideoScriptReview != null && dgvAiVideoScriptReview.Parent != tblAiVideoGenScriptInner)
            {
                dgvAiVideoScriptReview.Parent?.Controls.Remove(dgvAiVideoScriptReview);
                dgvAiVideoScriptReview.Dock = DockStyle.Fill;
                tblAiVideoGenScriptInner.Controls.Add(dgvAiVideoScriptReview, 0, 1);
                ApplyAppGridChrome(dgvAiVideoScriptReview);
            }

            var promptHosts = Controls.Find("pnlAiVideoGenPromptPreview", true);
            if (txtAiVideoGenPrompt != null && promptHosts.Length > 0)
            {
                MountAiVideoGenControl(txtAiVideoGenPrompt, promptHosts[0]);
            }

            if (pnlAffiliateDeepStoryboardHost != null
                && !tblAiVideoGenScriptInner.Controls.Contains(pnlAffiliateDeepStoryboardHost))
            {
                pnlAffiliateDeepStoryboardHost.Parent?.Controls.Remove(pnlAffiliateDeepStoryboardHost);
                pnlAffiliateDeepStoryboardHost.Dock = DockStyle.Fill;
                tblAiVideoGenScriptInner.Controls.Add(pnlAffiliateDeepStoryboardHost, 0, 3);
            }
        }

        // Mascot UI: see Form1.MascotStoryUi.cs
        private static Label CreateMascotFieldCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 4, 0, 2)
            };
        }

        private static TextBox CreateMascotConfigTextBox(string name, int width, int height)
        {
            var box = new TextBox
            {
                Name = name,
                Width = width,
                Height = Math.Max(height, AppInputMinHeight),
                Margin = new Padding(0, 0, 0, 8),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            ApplyAppInputChrome(box);
            return box;
        }

        private static void WireMascotLipSyncTableRow(
            TableLayoutPanel tbl,
            int row,
            string caption,
            out TextBox pathBox,
            out Button browseBtn,
            EventHandler browseClick)
        {
            var lbl = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(180, 185, 198),
                Margin = new Padding(0, 4, 4, 4)
            };
            pathBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, AppInputMinHeight),
                Margin = new Padding(0, 4, 4, 4),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            ApplyAppInputChrome(pathBox);
            browseBtn = new Button
            {
                Text = "…",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 26),
                Margin = new Padding(0, 4, 0, 4),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            browseBtn.FlatAppearance.BorderSize = 0;
            browseBtn.Click += browseClick;
            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(pathBox, 1, row);
            tbl.Controls.Add(browseBtn, 2, row);
        }

        private static Color AutoPostFieldBack => Color.FromArgb(45, 49, 60);
        private static Color AutoPostPanelBack => Color.FromArgb(35, 38, 48);
        private static Color AutoPostAccent => Color.FromArgb(76, 110, 245);
        private static Color AutoPostButtonBack => Color.FromArgb(60, 64, 77);

        private static TextBox CreateAutoPostTextBox(string name, bool multiline = false)
        {
            var box = new TextBox
            {
                Name = name,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                Multiline = multiline,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                AcceptsReturn = multiline
            };
            ApplyAppInputChrome(box);
            return box;
        }

        private static Label CreateAutoPostLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.Gainsboro
            };
        }

        private static Button CreateAutoPostButton(string name, string text, Color backColor, EventHandler click = null)
        {
            var btn = new Button
            {
                Name = name,
                Text = text,
                BackColor = backColor,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btn.FlatAppearance.BorderSize = 0;
            if (click != null)
            {
                btn.Click += click;
            }

            return btn;
        }

        private static CheckBox CreateAutoPostChannelCheckBox(string name, string text)
        {
            return new CheckBox
            {
                Name = name,
                Text = text,
                AutoSize = false,
                Size = new Size(520, 28),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 200, 255),
                Checked = true
            };
        }

        private static DateTimePicker CreateAutoPostSchedulePicker(string name)
        {
            return new DateTimePicker
            {
                Name = name,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy  HH:mm",
                ShowUpDown = false,
                Value = DateTime.Now.AddHours(1),
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                CalendarForeColor = Color.WhiteSmoke,
                CalendarMonthBackground = AutoPostFieldBack,
                Size = new Size(260, 28)
            };
        }


        private void BuildSidebarUi()
        {
            var navFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold, GraphicsUnit.Point);

            pnlSidebar = new Panel
            {
                Name = "pnlSidebar",
                Dock = DockStyle.Left,
                Width = 320,
                BackColor = Color.FromArgb(26, 28, 35),
                Padding = new Padding(0)
            };

            pnlSidebarNav = new FlowLayoutPanel
            {
                Name = "pnlSidebarNav",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 8, 0, 8),
                BackColor = Color.FromArgb(26, 28, 35)
            };

            btnNavHealth = CreateSidebarNavButton("btnNavHealth", "📊  Dashboard", navFont, tabHealthDashboard);
            btnNavRevenue = CreateSidebarNavButton("btnNavRevenue", "💰  Doanh thu", navFont, tabRevenueDashboard);
            btnNavAffiliate = CreateSidebarNavButton("btnNavAffiliate", "🔍  Săn Affiliate", navFont, tabAffiliateHunter);
            btnNavAiVideo = CreateSidebarNavButton("btnNavAiVideo", "▶  ✨  AI tạo video", navFont, tabAiVideoGen);
            btnNavAutoPost = CreateSidebarNavButton("btnNavAutoPost", "✈  Đăng tự động", navFont, tabAutoPost);
            btnNavWarmup = CreateSidebarNavButton("btnNavWarmup", "🔥 Warmup", navFont, tabAutoWarmup);

            pnlSidebarNav.Controls.Add(btnNavHealth);
            pnlSidebarNav.Controls.Add(btnNavRevenue);
            pnlSidebarNav.Controls.Add(btnNavAffiliate);
            pnlSidebarNav.Controls.Add(btnNavAiVideo);
            pnlSidebarNav.Controls.Add(btnNavAutoPost);
            pnlSidebarNav.Controls.Add(btnNavWarmup);

            btnNavApprovalQueue = new Button
            {
                Name = "btnNavApprovalQueue",
                Text = "✓  Hàng duyệt",
                Width = 320,
                Height = 72,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Font = navFont,
                BackColor = Color.FromArgb(26, 28, 35),
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0),
                UseVisualStyleBackColor = false,
                TabStop = false,
                Tag = "SidebarNav",
                AccessibleName = "SidebarNav"
            };
            btnNavApprovalQueue.FlatAppearance.BorderSize = 0;
            btnNavApprovalQueue.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 44, 54);
            btnNavApprovalQueue.Click += btnOpenApprovalQueue_Click;
            pnlSidebarNav.Controls.Add(btnNavApprovalQueue);

            btnNavSettings = CreateSidebarNavButton("btnNavSettings", "⚙  Cài đặt", navFont, tabSetting);
            btnNavSettings.Dock = DockStyle.Bottom;
            btnNavSettings.Height = 70;

            btnEmergencyStop = new Button
            {
                Name = "btnEmergencyStop",
                Text = "🛑 STOP ALL",
                Dock = DockStyle.Bottom,
                Height = 72,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                BackColor = Color.FromArgb(120, 28, 28),
                ForeColor = Color.White,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            btnEmergencyStop.FlatAppearance.BorderSize = 0;
            btnEmergencyStop.Click += btnEmergencyStop_Click;

            pnlSidebar.Controls.Add(pnlSidebarNav);
            pnlSidebar.Controls.Add(btnNavSettings);
            pnlSidebar.Controls.Add(btnEmergencyStop);

            WireAiVideoGenModeButtonsToMainSidebar();
            HighlightSidebarForSelectedTab();
        }

        private Button CreateSidebarNavButton(string name, string text, Font font, TabPage targetTab)
        {
            var btn = new Button
            {
                Name = name,
                Text = text,
                Width = 320,
                Height = 80,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Font = font,
                BackColor = Color.FromArgb(26, 28, 35),
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0),
                UseVisualStyleBackColor = false,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 44, 54);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 110, 68);
            btn.Tag = "SidebarNav";
            btn.AccessibleName = "SidebarNav";
            btn.Click += (sender, e) => SwitchToMainTab(targetTab);
            return btn;
        }

        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        private static void ApplyTextBoxPlaceholder(TextBox textBox, string placeholder)
        {
            if (textBox == null || string.IsNullOrEmpty(placeholder))
            {
                return;
            }

            void Apply()
            {
                if (textBox.IsHandleCreated)
                {
                    SendMessage(textBox.Handle, EmSetCueBanner, (IntPtr)1, placeholder);
                }
            }

            textBox.HandleCreated += (sender, e) => Apply();
            Apply();
        }
    }
}
