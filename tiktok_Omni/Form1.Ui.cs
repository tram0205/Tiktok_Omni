using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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
            var btnAffiliateKeywordsCaptionFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            const int affiliateKeywordRowHeight = 40;
            var btnAffiliateProfileCaptionWidth = Math.Max(
                72,
                MeasureAffiliateButtonTextWidth("Profile", btnAffiliateKeywordsCaptionFont, affiliateKeywordRowHeight) + 16);
            const int affiliateProfileComboWidth = 180;
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
                Size = new Size(88, 28),
                Margin = new Padding(0, 0, 12, 0),
                Minimum = 1,
                Maximum = 500,
                Value = 30,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblAffiliateMinSafety = new Label
            {
                Text = "Điểm tương tác tối thiểu",
                AutoSize = true,
                Margin = new Padding(0, 2, 6, 0),
                ForeColor = Color.Gainsboro
            };

            numAffiliateMinSafety = new NumericUpDown
            {
                Name = "numAffiliateMinSafety",
                Size = new Size(68, 28),
                Margin = new Padding(0, 0, 12, 0),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            numAffiliateMinSafety.ValueChanged += (s, e) => RefreshAffiliateGridByQualityFilter();

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
                Size = new Size(64, 28),
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
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Color.Transparent,
                Dock = DockStyle.None
            };
            flpAffiliateCompactFilters.Controls.Add(lblMaxResults);
            flpAffiliateCompactFilters.Controls.Add(numAffiliateMaxResults);
            flpAffiliateCompactFilters.Controls.Add(lblAffiliateMinSafety);
            flpAffiliateCompactFilters.Controls.Add(numAffiliateMinSafety);
            flpAffiliateCompactFilters.Controls.Add(chkAffiliateOnlyHighQuality);
            flpAffiliateCompactFilters.Controls.Add(chkAffiliateRankByEngagement);
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
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
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
            affiliateActionTip.SetToolTip(btnPushToAiVideoGen, "Đẩy dòng đang hiển thị sang Slideshow, Affiliate chuyên sâu hoặc Video Reup.");
            affiliateActionTip.SetToolTip(btnAffiliateDeepDive,
                "Chọn dòng trên lưới — tải video, gửi Gemini phân tích voiceover/kịch bản (cần AI API Key).");
            affiliateActionTip.SetToolTip(btnDownloadSelectedAffiliate, "Cần FFmpeg + lưu trữ OK và có dòng được chọn.");
            affiliateActionTip.SetToolTip(
                chkAffiliateAutoEnrich,
                "Sau Hunt: tự gọi TikWM (views/likes/…) và quét link affiliate. Chạy ngầm — bấm Dừng để hủy.");

            var flpAffiliateActions = CreateAffiliateActionsFlowPanel("flpAffiliateActions");
            flpAffiliateActions.Margin = new Padding(0, 2, 0, 0);
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = AppGridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
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
                Font = AppGridHeaderFont,
                Padding = new Padding(6, 8, 6, 8),
                WrapMode = DataGridViewTriState.False
            };
            dgvAffiliateResults.EnableHeadersVisualStyles = false;
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

            dgvAffiliateResults.ContextMenuStrip = affiliateContextMenu;

            var affiliateGridTip = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
            affiliateGridTip.SetToolTip(dgvAffiliateResults,
                "Bấm đúp dòng để xem video MP4 native. Bấm ô Caption / Từ khoá / Hashtag / Link aff / Lời thoại để xem đầy đủ và sao chép. " +
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
            affiliateGridTip.SetToolTip(numAffiliateMinSafety, scoringGuide);
            affiliateGridTip.SetToolTip(lblAffiliateMinSafety, scoringGuide);
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
                Margin = Padding.Empty
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
                Margin = new Padding(6, 0, 6, 0),
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
                Margin = new Padding(0, 0, 0, 2),
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
                Margin = new Padding(6, 2, 6, 0),
                BackColor = Color.FromArgb(35, 38, 48)
            };
            tblAffiliateFilters.Controls.Add(flpAffiliatePlatformsRow);
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

        private static readonly Font AffiliateJellyButtonFont = new Font("Segoe UI", 10.25F, FontStyle.Bold);
        private static readonly Font AffiliateKeywordsInputFont = AppKeywordInputFont;
        private const int AffiliateJellyButtonMinHeight = 32;
        private const int AffiliateButtonHorizontalPad = 22;
        private const int AffiliateButtonMinWidth = 72;

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
            return MeasureAffiliateButtonTextWidth(text, AffiliateJellyButtonFont, AffiliateJellyButtonMinHeight);
        }

        private static int MeasureAffiliateButtonTextWidth(string text, Font font, int buttonHeight)
        {
            return TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, buttonHeight),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;
        }

        internal static void ResizeAffiliateToolbarButton(Button button)
        {
            if (button == null || button.IsDisposed)
            {
                return;
            }

            var width = Math.Max(
                AffiliateButtonMinWidth,
                MeasureAffiliateButtonTextWidth(button.Text) + AffiliateButtonHorizontalPad);
            button.Width = width;
            button.MinimumSize = new Size(width, AffiliateJellyButtonMinHeight);
        }

        private static void PrepareAffiliateToolbarButtonForFlow(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.Dock = DockStyle.None;
            button.AutoSize = false;
            button.Height = AffiliateJellyButtonMinHeight;
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
            return new JellyButton
            {
                Name = name,
                Text = text,
                Font = AffiliateJellyButtonFont,
                JellyTint = tint,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = Color.FromArgb(245, 247, 250),
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 10, 6),
                MinimumSize = new Size(88, AffiliateJellyButtonMinHeight)
            };
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
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
            };

            flpSlideshowData = CreateAiVideoGenActionFlowPanel();
            flpSlideshowExecute = CreateAiVideoGenActionFlowPanel();

            btnGenerateGeminiPrompt = CreateToolbarButton("Tạo kịch bản AI", executeStyle: false, minWidth: 140);
            btnGenerateGeminiPrompt.Name = "btnGenerateGeminiPrompt";
            btnGenerateGeminiPrompt.BackColor = Color.FromArgb(76, 110, 245);
            btnGenerateGeminiPrompt.Click += btnGenerateGeminiPrompt_Click;

            btnReviewScriptBeforeRender = CreateToolbarButton("Duyệt kịch bản trước render", executeStyle: false, minWidth: 180);
            btnReviewScriptBeforeRender.Name = "btnReviewScriptBeforeRender";
            btnReviewScriptBeforeRender.BackColor = Color.FromArgb(88, 101, 242);
            btnReviewScriptBeforeRender.Click += btnReviewScriptBeforeRender_Click;

            btnAffiliateGenerateScript = CreateToolbarButton("Sinh Script", executeStyle: false, minWidth: 110);
            btnAffiliateGenerateScript.Name = "btnAffiliateGenerateScript";
            btnAffiliateGenerateScript.BackColor = Color.FromArgb(78, 120, 166);
            btnAffiliateGenerateScript.Click += btnAffiliateGenerateScript_Click;

            btnAffiliateEditScript = CreateToolbarButton("Sửa Script", executeStyle: false, minWidth: 100);
            btnAffiliateEditScript.Name = "btnAffiliateEditScript";
            btnAffiliateEditScript.BackColor = Color.FromArgb(60, 64, 77);
            btnAffiliateEditScript.Click += btnAffiliateEditScript_Click;

            btnAffiliateBatchPipeline = CreateToolbarButton("Pipeline hàng loạt", executeStyle: false, minWidth: 150);
            btnAffiliateBatchPipeline.Name = "btnAffiliateBatchPipeline";
            btnAffiliateBatchPipeline.BackColor = Color.FromArgb(100, 70, 160);
            btnAffiliateBatchPipeline.Click += btnAffiliateBatchPipeline_Click;

            var slideshowActionTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ShowAlways = true };
            slideshowActionTip.SetToolTip(btnAffiliateGenerateScript,
                "Chọn dòng trên lưới sản phẩm — sinh lời thoại preview bằng Gemini (cần AI API Key).");
            slideshowActionTip.SetToolTip(btnAffiliateEditScript,
                "Chọn đúng một dòng sản phẩm — mở hộp thoại sửa script.");
            slideshowActionTip.SetToolTip(btnAffiliateBatchPipeline,
                "Hunt (tab Săn Video) → lọc HQ → đẩy Slideshow → sinh script → render. Cần từ khóa + AI API Key.");

            btnCopyAiVideoPrompt = CreateToolbarButton("Sao chép", executeStyle: false, minWidth: 90);
            btnCopyAiVideoPrompt.Name = "btnCopyAiVideoPrompt";
            btnCopyAiVideoPrompt.Click += btnCopyAiVideoPrompt_Click;

            btnSaveAiVideoPrompt = CreateToolbarButton("Lưu .txt", executeStyle: false, minWidth: 90);
            btnSaveAiVideoPrompt.Name = "btnSaveAiVideoPrompt";
            btnSaveAiVideoPrompt.Click += btnSaveAiVideoPrompt_Click;

            var lblGeminiStyle = new Label
            {
                Text = "Mẫu Gemini:",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(0, 8, 4, 0)
            };
            cbGeminiStyleTemplate = new ComboBox
            {
                Name = "cbGeminiStyleTemplate",
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 4, 8, 0),
                MinimumSize = new Size(160, 28)
            };
            foreach (GeminiStyleTemplate t in Enum.GetValues(typeof(GeminiStyleTemplate)))
            {
                cbGeminiStyleTemplate.Items.Add(t);
            }

            cbGeminiStyleTemplate.SelectedItem = GeminiStyleTemplate.Storytelling;
            cbGeminiStyleTemplate.SelectedIndexChanged += async (_, __) =>
            {
                try
                {
                    var s = await _configManager.LoadAsync().ConfigureAwait(true);
                    s.GeminiStyleTemplate = GetSelectedGeminiStyleTemplate().ToString();
                    await _configManager.SaveAsync(s).ConfigureAwait(true);
                }
                catch
                {
                }
            };

            flpSlideshowData.Controls.Add(lblGeminiStyle);
            flpSlideshowData.Controls.Add(cbGeminiStyleTemplate);
            flpSlideshowData.Controls.Add(btnGenerateGeminiPrompt);
            flpSlideshowData.Controls.Add(btnAffiliateGenerateScript);
            flpSlideshowData.Controls.Add(btnAffiliateEditScript);
            flpSlideshowData.Controls.Add(btnReviewScriptBeforeRender);
            flpSlideshowData.Controls.Add(btnCopyAiVideoPrompt);
            flpSlideshowData.Controls.Add(btnSaveAiVideoPrompt);
            btnClearAiGenGrid = CreateClearGridButton();
            flpSlideshowData.Controls.Add(btnClearAiGenGrid);

            btnRenderAiVideo = CreateToolbarButton("Render video sản phẩm", executeStyle: true, minWidth: 160);
            btnRenderAiVideo.Name = "btnRenderAiVideo";
            btnRenderAiVideo.Click += btnRenderAiVideo_Click;
            btnOpenOutputFolder = CreateOpenOutputFolderButton();
            btnSlideshowOpenApproval = CreateSecondaryNavButton("btnSlideshowOpenApproval", "Mở Hàng duyệt");
            btnSlideshowOpenApproval.Click += btnOpenApprovalQueue_Click;
            flpSlideshowExecute.Controls.Add(btnAffiliateBatchPipeline);
            flpSlideshowExecute.Controls.Add(btnRenderAiVideo);
            flpSlideshowExecute.Controls.Add(btnOpenOutputFolder);
            flpSlideshowExecute.Controls.Add(btnSlideshowOpenApproval);

            if (chkUseMultiVoiceNarration == null)
            {
                chkUseMultiVoiceNarration = new CheckBox
                {
                    Name = "chkUseMultiVoiceNarration",
                    Text = "Đa giọng đọc (Multi-voice)",
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 204, 214),
                    Margin = new Padding(8, 8, 0, 0)
                };
            }

            chkUseMultiVoiceNarration.Visible = true;
            flpSlideshowData.Controls.Add(chkUseMultiVoiceNarration);

            numAiTextSize = new NumericUpDown
            {
                Name = "numAiTextSize",
                Minimum = 24,
                Maximum = 96,
                Value = 50,
                Width = 56,
                Height = 24,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 8, 0)
            };
            numAiMusicVolume = new NumericUpDown
            {
                Name = "numAiMusicVolume",
                Minimum = 0,
                Maximum = 100,
                Value = 14,
                Width = 56,
                Height = 24,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 0, 0)
            };
            flpSlideshowExecute.Controls.Add(new Label
            {
                Text = "Cỡ chữ",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(0, 8, 4, 0)
            });
            flpSlideshowExecute.Controls.Add(numAiTextSize);
            flpSlideshowExecute.Controls.Add(new Label
            {
                Text = "Âm lượng nhạc",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(8, 8, 4, 0)
            });
            flpSlideshowExecute.Controls.Add(numAiMusicVolume);

            numAiTransitionDuration = new NumericUpDown
            {
                Name = "numAiTransitionDuration",
                DecimalPlaces = 1,
                Increment = 0.1M,
                Minimum = 0.2M,
                Maximum = 2.0M,
                Value = 0.6M,
                Width = 56,
                Height = 24,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 0, 0)
            };
            flpSlideshowExecute.Controls.Add(new Label
            {
                Text = "Chuyển cảnh (s)",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(8, 8, 4, 0)
            });
            flpSlideshowExecute.Controls.Add(numAiTransitionDuration);

            pnlSlideshowActionBar = BuildAiVideoGenModeActionPanel(flpSlideshowData, flpSlideshowExecute);

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
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
            };

            btnRunAffiliateDeepVideo = CreateToolbarButton("Render Affiliate chuyên sâu", executeStyle: true, minWidth: 200);
            btnRunAffiliateDeepVideo.Name = "btnRunAffiliateDeepVideo";
            btnRunAffiliateDeepVideo.Click += btnRunAffiliateDeepVideo_Click;
            BuildAiModeToolbarOnlyLayout(pnlModeAffiliateDeep, out _);
            var pnlPhilosophyProgress = CreateAiModeProgressBand("pnlPhilosophyProgress");
            BuildAiModeFillLayoutPercent(pnlModePhilosophy, out var pnlPhilosophyInput, out var pnlPhilosophyLog, 42F, pnlPhilosophyProgress);

            var lblPhilosophyTitle = new Label
            {
                Text = "Video Triết lý / Quote",
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point)
            };
            lblPhilosophyPrereq = new Label
            {
                Name = "lblPhilosophyPrereq",
                Text = "Đang kiểm tra cấu hình…",
                Location = new Point(0, 26),
                Size = new Size(900, 44),
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(255, 180, 120),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
            };
            var lblPhilosophyProfile = new Label
            {
                Text = "Bước 1 — Profile kênh:",
                Location = new Point(0, 76),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 205, 218),
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold, GraphicsUnit.Point)
            };
            cbPhilosophyProfile = new ComboBox
            {
                Name = "cbPhilosophyProfile",
                Location = new Point(148, 72),
                Size = new Size(200, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false
            };
            cbPhilosophyProfile.Items.Add("default");
            cbPhilosophyProfile.SelectedIndex = 0;
            cbPhilosophyProfile.SelectedIndexChanged += (_, __) => RefreshPhilosophyPrereqLabel(null);

            btnPhilosophyOpenAssets = new Button
            {
                Name = "btnPhilosophyOpenAssets",
                Text = "Mở thư mục Assets",
                Location = new Point(358, 70),
                Size = new Size(160, 30),
                BackColor = Color.FromArgb(55, 60, 72),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnPhilosophyOpenAssets.FlatAppearance.BorderSize = 0;
            btnPhilosophyOpenAssets.Click += btnPhilosophyOpenAssets_Click;

            lblPhilosophyQuoteLabel = new Label
            {
                Name = "lblPhilosophyQuoteLabel",
                Text = "Bước 2 — Nội dung (1 quote, hoặc link bài để AI trích quote):",
                Location = new Point(0, 108),
                Size = new Size(900, 20),
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(200, 205, 218),
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold, GraphicsUnit.Point)
            };

            txtPhilosophyInput = new TextBox
            {
                Name = "txtPhilosophyInput",
                Location = new Point(0, 132),
                Size = new Size(900, 64),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
            };

            btnPhilosophyImportFromFile = new Button
            {
                Name = "btnPhilosophyImportFromFile",
                Text = "Nạp nhiều quote (.txt)",
                AutoSize = true,
                MinimumSize = new Size(180, 36),
                Margin = new Padding(0, 0, 8, 6),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point)
            };
            btnPhilosophyImportFromFile.FlatAppearance.BorderSize = 0;
            btnPhilosophyImportFromFile.Click += btnPhilosophyImportFromFile_Click;

            btnRunPhilosophyVideo = new Button
            {
                Name = "btnRunPhilosophyVideo",
                Text = "Tạo video (1 quote)",
                AutoSize = true,
                MinimumSize = new Size(180, 36),
                Margin = new Padding(0, 0, 8, 6),
                BackColor = Color.FromArgb(56, 142, 96),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
            };
            btnRunPhilosophyVideo.FlatAppearance.BorderSize = 0;
            btnRunPhilosophyVideo.Click += btnRunPhilosophyVideo_Click;

            btnPhilosophyOpenApproval = CreateSecondaryNavButton("btnPhilosophyOpenApproval", "Mở Hàng duyệt");
            btnPhilosophyOpenApproval.AutoSize = true;
            btnPhilosophyOpenApproval.MinimumSize = new Size(140, 36);
            btnPhilosophyOpenApproval.Margin = new Padding(0, 0, 8, 6);
            btnPhilosophyOpenApproval.Click += btnOpenApprovalQueue_Click;

            var flpPhilosophyActions = new FlowLayoutPanel
            {
                Name = "flpPhilosophyActions",
                Location = new Point(0, 206),
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = pnlPhilosophyInput.BackColor,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            flpPhilosophyActions.Controls.Add(btnPhilosophyImportFromFile);
            flpPhilosophyActions.Controls.Add(btnRunPhilosophyVideo);
            flpPhilosophyActions.Controls.Add(btnPhilosophyOpenApproval);

            var lblPhilosophyFlowHint = new Label
            {
                Name = "lblPhilosophyFlowHint",
                Text = "Sau khi tạo: xem tiến trình bên dưới → video vào Hàng duyệt (Pending) → duyệt rồi Đăng tự động.",
                Location = new Point(0, 248),
                Size = new Size(900, 32),
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(140, 148, 165),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic, GraphicsUnit.Point)
            };

            pnlPhilosophyInput.Controls.Add(lblPhilosophyTitle);
            pnlPhilosophyInput.Controls.Add(lblPhilosophyPrereq);
            pnlPhilosophyInput.Controls.Add(lblPhilosophyProfile);
            pnlPhilosophyInput.Controls.Add(cbPhilosophyProfile);
            pnlPhilosophyInput.Controls.Add(btnPhilosophyOpenAssets);
            pnlPhilosophyInput.Controls.Add(lblPhilosophyQuoteLabel);
            pnlPhilosophyInput.Controls.Add(txtPhilosophyInput);
            pnlPhilosophyInput.Controls.Add(flpPhilosophyActions);
            pnlPhilosophyInput.Controls.Add(lblPhilosophyFlowHint);
            pnlPhilosophyInput.Resize += (_, __) =>
            {
                var w = Math.Max(320, pnlPhilosophyInput.ClientSize.Width - pnlPhilosophyInput.Padding.Horizontal);
                if (lblPhilosophyPrereq != null)
                {
                    lblPhilosophyPrereq.Width = w;
                }

                if (lblPhilosophyQuoteLabel != null)
                {
                    lblPhilosophyQuoteLabel.Width = w;
                }

                if (txtPhilosophyInput != null)
                {
                    txtPhilosophyInput.Width = w;
                }

                LayoutPhilosophyStep2Row(pnlPhilosophyInput, flpPhilosophyActions, lblPhilosophyFlowHint);
            };

            var tblPhilosophyLog = new TableLayoutPanel
            {
                Name = "tblPhilosophyLog",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = pnlPhilosophyLog.BackColor,
                Margin = new Padding(0)
            };
            tblPhilosophyLog.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            tblPhilosophyLog.RowStyles.Add(new RowStyle(SizeType.Absolute, 152F));
            tblPhilosophyLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblPhilosophyQueueHeader = new Label
            {
                Text = "Bước 3 — Hàng đợi render & kết quả",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(200, 205, 218),
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold, GraphicsUnit.Point),
                Padding = new Padding(0, 4, 0, 0)
            };

            _philosophyQueueBindingList = new BindingList<ProductionQueueRowItem>();
            dgvPhilosophyQueue = new DataGridView
            {
                Name = "dgvPhilosophyQueue",
                Dock = DockStyle.Fill,
                DataSource = _philosophyQueueBindingList,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            ConfigureProductionQueueGrid(dgvPhilosophyQueue);
            dgvPhilosophyQueue.SelectionChanged += DgvPhilosophyQueue_SelectionChanged;

            rtbPhilosophyLog = CreateAiModeLogTextBox("rtbPhilosophyLog");
            tblPhilosophyLog.Controls.Add(lblPhilosophyQueueHeader, 0, 0);
            tblPhilosophyLog.Controls.Add(dgvPhilosophyQueue, 0, 1);
            tblPhilosophyLog.Controls.Add(rtbPhilosophyLog, 0, 2);
            pnlPhilosophyLog.Controls.Add(tblPhilosophyLog);
            ApplyAiModeLogLineSpacing(rtbPhilosophyLog);
            LayoutPhilosophyStep2Row(pnlPhilosophyInput, flpPhilosophyActions, lblPhilosophyFlowHint);

            lblPhilosophyProgress = new Label
            {
                Name = "lblPhilosophyProgress",
                Text = "Tiến trình: sẵn sàng"
            };
            ConfigureAiModeProgressLabel(lblPhilosophyProgress);
            pbPhilosophyProgress = new ProgressBar { Name = "pbPhilosophyProgress" };
            ConfigureAiModeProgressBar(pbPhilosophyProgress);
            btnPhilosophyClearLog = new Button
            {
                Name = "btnPhilosophyClearLog",
                Text = "Xóa log",
                Size = new Size(72, 24),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnPhilosophyClearLog.FlatAppearance.BorderSize = 0;
            btnPhilosophyClearLog.Click += btnPhilosophyClearLog_Click;
            pnlPhilosophyProgress.Controls.Add(pbPhilosophyProgress);
            pnlPhilosophyProgress.Controls.Add(lblPhilosophyProgress);
            pnlPhilosophyProgress.Controls.Add(btnPhilosophyClearLog);
            LayoutAiModeClearLogButton(btnPhilosophyClearLog, pnlPhilosophyProgress);

            lblVideoReupReadiness = new Label
            {
                Name = "lblVideoReupReadiness",
                Text = "Đang kiểm tra FFmpeg / API…",
                Dock = DockStyle.Top,
                Height = 28,
                AutoSize = false,
                ForeColor = Color.FromArgb(255, 180, 120),
                Padding = new Padding(4, 4, 4, 2),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
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
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 148),
                BackColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };
            lblVideoReupProgress = new Label
            {
                Name = "lblVideoReupProgress",
                Text = "Tiến trình: sẵn sàng",
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.FromArgb(200, 204, 214),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pbVideoReupProgress = new ProgressBar
            {
                Name = "pbVideoReupProgress",
                Dock = DockStyle.Top,
                Height = 12,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            btnVideoReupClearLog = new Button
            {
                Name = "btnVideoReupClearLog",
                Text = "Xóa log",
                Size = new Size(72, 24),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnVideoReupClearLog.FlatAppearance.BorderSize = 0;
            btnVideoReupClearLog.Click += btnVideoReupClearLog_Click;
            rtbVideoReupLog = new RichTextBox
            {
                Name = "rtbVideoReupLog",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 8.25F, FontStyle.Regular, GraphicsUnit.Point)
            };
            var tblReupStatus = new TableLayoutPanel
            {
                Name = "tblReupStatus",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            tblReupStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tblReupStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            tblReupStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var pnlReupStatusHeader = new Panel
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 28),
                Padding = new Padding(0, 0, 4, 0)
            };
            lblVideoReupProgress.Dock = DockStyle.Fill;
            btnVideoReupClearLog.Dock = DockStyle.Right;
            btnVideoReupClearLog.Width = 80;
            btnVideoReupClearLog.MinimumSize = new Size(80, 26);
            btnVideoReupClearLog.Margin = new Padding(4, 0, 0, 0);
            pnlReupStatusHeader.Controls.Add(lblVideoReupProgress);
            pnlReupStatusHeader.Controls.Add(btnVideoReupClearLog);
            tblReupStatus.Controls.Add(pnlReupStatusHeader, 0, 0);
            tblReupStatus.Controls.Add(pbVideoReupProgress, 0, 1);
            tblReupStatus.Controls.Add(rtbVideoReupLog, 0, 2);
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

            var pnlReupGridToolbar = CreateDualToolbarHost(88);
            pnlReupGridToolbar.Name = "pnlReupGridToolbar";
            var flpReupToolbar = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupToolbar.Dock = DockStyle.Fill;
            flpReupToolbar.Controls.Add(CreateClearGridButton());

            btnVideoReupRenderVideo = CreateReupJellyButton(
                "btnVideoReupRenderVideo",
                "Tạo video thành phẩm",
                ReupTintRender,
                188);
            btnVideoReupRenderVideo.Click += btnVideoReupRenderVideo_Click;
            btnVideoReupRenderBatch = CreateReupJellyButton(
                "btnVideoReupRenderBatch",
                "Tạo video thành phẩm (lô)",
                ReupTintRenderBatch,
                208);
            btnVideoReupRenderBatch.Click += btnVideoReupRenderBatch_Click;

            _videoReupBindingList = new BindingList<VideoReupRowItem>();
            dgvVideoReupInput = new DataGridView
            {
                Name = "dgvVideoReupInput",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(180, 100),
                AutoGenerateColumns = true,
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
                ScrollBars = ScrollBars.Both,
                DataSource = _videoReupBindingList
            };

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
            dgvVideoReupInput.ColumnHeadersHeight = AppGridHeaderHeight;
            dgvVideoReupInput.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.Font = AppGridHeaderFont;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 8, 6, 8);
            dgvVideoReupInput.RowTemplate.Height = 28;
            dgvVideoReupInput.EnableHeadersVisualStyles = false;
            dgvVideoReupInput.DataBindingComplete += DgvVideoReupInput_DataBindingComplete;
            dgvVideoReupInput.CellBeginEdit += DgvVideoReupInput_CellBeginEdit;
            dgvVideoReupInput.CellEndEdit += DgvVideoReupInput_CellEndEdit;
            dgvVideoReupInput.KeyDown += dgvVideoReupInput_KeyDown;
            dgvVideoReupInput.SelectionChanged += dgvVideoReupInput_SelectionChanged;

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

            chkReupUseVisualHookSfx = new CheckBox
            {
                Name = "chkReupUseVisualHookSfx",
                Text = "Hook SFX 3s",
                AutoSize = true,
                Margin = new Padding(0, 2, 8, 2),
                ForeColor = Color.Gainsboro
            };
            cbReupVisualHookPreset = new ComboBox
            {
                Name = "cbReupVisualHookPreset",
                Width = 110,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 2, 6, 2)
            };
            cbReupVisualHookPreset.Items.AddRange(new object[] { "(Chọn preset)", "Tiếng cười", "Giật mình" });
            cbReupVisualHookPreset.SelectedIndex = 0;
            cbReupVisualHookPreset.SelectedIndexChanged += (_, __) =>
            {
                if (cbReupVisualHookPreset.SelectedIndex <= 0)
                {
                    return;
                }

                VisualHookService.EnsureHooksDirectoryExists();
                var dir = VisualHookService.GetDefaultHooksDirectory();
                var file = cbReupVisualHookPreset.SelectedIndex == 1 ? "laugh.mp3" : "jumpscare.mp3";
                var path = Path.Combine(dir, file);
                if (txtReupVisualHookSfx != null)
                {
                    txtReupVisualHookSfx.Text = File.Exists(path) ? path : string.Empty;
                }
            };
            txtReupVisualHookSfx = new TextBox
            {
                Name = "txtReupVisualHookSfx",
                Width = 120,
                Height = 24,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 6, 2)
            };
            btnBrowseReupVisualHookSfx = new Button
            {
                Name = "btnBrowseReupVisualHookSfx",
                Text = "Duyệt…",
                AutoSize = true,
                MinimumSize = new Size(64, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(55, 100, 140),
                ForeColor = Color.White,
                Margin = new Padding(0, 2, 0, 2)
            };
            btnBrowseReupVisualHookSfx.FlatAppearance.BorderSize = 0;
            btnBrowseReupVisualHookSfx.Click += (_, __) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Audio|*.mp3;*.wav;*.m4a;*.aac";
                    dlg.Title = "Chọn file âm thanh Hook 3s";
                    if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        txtReupVisualHookSfx.Text = dlg.FileName;
                    }
                }
            };
            var flpHookSfxCompact = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpHookSfxCompact.Dock = DockStyle.Fill;
            flpHookSfxCompact.Controls.Add(chkReupUseVisualHookSfx);
            flpHookSfxCompact.Controls.Add(cbReupVisualHookPreset);
            flpHookSfxCompact.Controls.Add(txtReupVisualHookSfx);
            flpHookSfxCompact.Controls.Add(btnBrowseReupVisualHookSfx);

            lblVideoReupHook = new Label
            {
                Name = "lblVideoReupHook",
                Text = string.Empty,
                Visible = false
            };
            btnVideoReupHookGemini = CreateToolbarButton("Gemini: tạo hook", executeStyle: false, minWidth: 118);
            btnVideoReupHookGemini.Name = "btnVideoReupHookGemini";
            btnVideoReupHookGemini.BackColor = Color.FromArgb(76, 110, 245);
            btnVideoReupHookGemini.Height = 28;
            btnVideoReupHookGemini.Click += btnVideoReupHookGemini_Click;
            btnVideoReupHookRegen = CreateToolbarButton("Tạo lại hook", executeStyle: false, minWidth: 100);
            btnVideoReupHookRegen.Name = "btnVideoReupHookRegen";
            btnVideoReupHookRegen.BackColor = Color.FromArgb(60, 100, 200);
            btnVideoReupHookRegen.Height = 28;
            btnVideoReupHookRegen.Click += btnVideoReupHookRegen_Click;
            btnVideoReupLyriaHook = CreateToolbarButton("Voiceover hook", executeStyle: false, minWidth: 118);
            btnVideoReupLyriaHook.Name = "btnVideoReupLyriaHook";
            btnVideoReupLyriaHook.BackColor = Color.FromArgb(120, 70, 160);
            btnVideoReupLyriaHook.Height = 28;
            btnVideoReupLyriaHook.Click += btnVideoReupLyriaHook_Click;
            var lblReupHookPrefix = new Label
            {
                Text = "Hook:",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 6, 6, 0)
            };
            var flpReupHookActions = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupHookActions.Dock = DockStyle.Fill;
            flpReupHookActions.Controls.Add(lblReupHookPrefix);
            flpReupHookActions.Controls.Add(btnVideoReupHookGemini);
            flpReupHookActions.Controls.Add(btnVideoReupHookRegen);
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
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false,
                Margin = new Padding(0, 2, 6, 2)
            };
            cbVideoReupMusic.SelectedIndexChanged += cbVideoReupMusic_SelectedIndexChanged;
            btnVideoReupOpenMusicFolder = CreateToolbarButton("Thư mục", executeStyle: false, minWidth: 72);
            btnVideoReupOpenMusicFolder.Name = "btnVideoReupOpenMusicFolder";
            btnVideoReupOpenMusicFolder.BackColor = Color.FromArgb(55, 100, 140);
            btnVideoReupOpenMusicFolder.Height = 28;
            btnVideoReupOpenMusicFolder.Click += btnVideoReupOpenMusicFolder_Click;
            btnVideoReupRefreshMusicList = CreateToolbarButton("Làm mới", executeStyle: false, minWidth: 72);
            btnVideoReupRefreshMusicList.Name = "btnVideoReupRefreshMusicList";
            btnVideoReupRefreshMusicList.BackColor = Color.FromArgb(50, 120, 90);
            btnVideoReupRefreshMusicList.Height = 28;
            btnVideoReupRefreshMusicList.Click += btnVideoReupRefreshMusicList_Click;
            lblVideoReupMusicPathHint = new Label
            {
                Name = "lblVideoReupMusicPathHint",
                Text = string.Empty,
                Visible = false
            };

            var flpReupMusicRow = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupMusicRow.Dock = DockStyle.Fill;
            flpReupMusicRow.Controls.Add(lblVideoReupMusicPick);
            flpReupMusicRow.Controls.Add(cbVideoReupMusic);
            flpReupMusicRow.Controls.Add(btnVideoReupRefreshMusicList);
            flpReupMusicRow.Controls.Add(btnVideoReupOpenMusicFolder);

            pnlReupMusicLibraryPath = CreateSettingsCompactPathRow(
                out txtVideoReupMusicLibraryPath,
                out btnBrowseVideoReupMusicLibrary,
                "Thư mục nhạc",
                "txtVideoReupMusicLibraryPath",
                btnBrowseVideoReupMusicLibrary_Click,
                "Duyệt",
                "btnBrowseVideoReupMusicLibrary");
            pnlReupMusicLibraryPath.Name = "pnlReupMusicLibraryPath";
            pnlReupMusicLibraryPath.Dock = DockStyle.Fill;
            pnlReupMusicLibraryPath.Margin = new Padding(0, 2, 0, 0);

            var tblReupEditor = new TableLayoutPanel
            {
                Name = "tblReupEditor",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 5,
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
            tblReupEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tblReupEditor.Controls.Add(pnlReupUrlSection, 0, 0);
            tblReupEditor.SetColumnSpan(pnlReupUrlSection, 2);
            tblReupEditor.Controls.Add(grpVideoReupAudioMode, 0, 1);
            tblReupEditor.SetColumnSpan(grpVideoReupAudioMode, 2);
            tblReupEditor.Controls.Add(flpReupHookActions, 0, 2);
            tblReupEditor.Controls.Add(flpReupMusicRow, 1, 2);
            tblReupEditor.Controls.Add(pnlReupMusicLibraryPath, 0, 3);
            tblReupEditor.SetColumnSpan(pnlReupMusicLibraryPath, 2);
            tblReupEditor.Controls.Add(flpHookSfxCompact, 0, 4);
            tblReupEditor.SetColumnSpan(flpHookSfxCompact, 2);
            pnlReupEditor.Controls.Add(tblReupEditor);
            pnlReupEditor.AutoScroll = false;
            pnlReupEditor.AutoScrollMinSize = Size.Empty;

            InitializeReupPreviewUi();

            pnlModeVideoReup.AutoScroll = false;
            pnlModeVideoReup.Padding = new Padding(4);
            WireVideoReupTabLayout();
            RefreshVideoReupReadinessLabel(null);
            InitializeVideoReupDraftAutoSave();

            tabMascotStory = pnlModeMascot;
            BuildMascotStoryTabUi();
            WireMascotLipSyncEvents();
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = AppGridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            dgvAiVideoScriptReview.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            dgvAiVideoScriptReview.DefaultCellStyle.ForeColor = Color.Gainsboro;
            dgvAiVideoScriptReview.DefaultCellStyle.Font = AppInputFont;
            dgvAiVideoScriptReview.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            dgvAiVideoScriptReview.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvAiVideoScriptReview.RowTemplate.Height = 30;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.Font = AppGridHeaderFont;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 8, 6, 8);
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvAiVideoScriptReview.EnableHeadersVisualStyles = false;
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
                    RowCount = 4,
                    BackColor = tabAiVideoGen.BackColor,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                tblAiVideoGenRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 7F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 51F));
                tblAiVideoGenRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                flpAiVideoGenHeader.Dock = DockStyle.Fill;
                pnlAiVideoGenProductArea.Dock = DockStyle.Fill;
                pnlAiVideoGenScriptHost.Dock = DockStyle.Fill;
                pnlAiVideoGenStickyHost.Dock = DockStyle.Fill;
                pnlAiVideoGenActionBar.Dock = DockStyle.Fill;
                if (pnlAiVideoGenTopHeader != null)
                {
                    pnlAiVideoGenTopHeader.Dock = DockStyle.Fill;
                }

                var headerHost = pnlAiVideoGenTopHeader ?? (Control)flpAiVideoGenHeader;
                tblAiVideoGenRoot.Controls.Add(headerHost, 0, 0);
                tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenProductArea, 0, 1);
                tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenScriptHost, 0, 2);
                tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenActionBar, 0, 3);

                WireAiVideoGenTabShell();

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
            if (tabAiVideoGen == null || tblAiVideoGenRoot == null)
            {
                return;
            }

            tblAiVideoGenRoot.Dock = DockStyle.Fill;

            if (pnlAiVideoGenRenderStatusHost != null)
            {
                if (tabAiVideoGen.Controls.Contains(pnlAiVideoGenRenderStatusHost))
                {
                    tabAiVideoGen.Controls.Remove(pnlAiVideoGenRenderStatusHost);
                }

                pnlAiVideoGenRenderStatusHost.Visible = false;
            }

            if (grpAiRenderProgress != null)
            {
                grpAiRenderProgress.Visible = false;
            }

            foreach (var c in tabAiVideoGen.Controls.Cast<Control>().ToArray())
            {
                if (c != tblAiVideoGenRoot)
                {
                    tabAiVideoGen.Controls.Remove(c);
                }
            }

            if (!tabAiVideoGen.Controls.Contains(tblAiVideoGenRoot))
            {
                tabAiVideoGen.Controls.Add(tblAiVideoGenRoot);
            }
        }

        private void ApplyAiVideoGenTableRowHeights(bool showProductGrid, int modeTabIndex)
        {
            if (tblAiVideoGenRoot == null || tblAiVideoGenRoot.RowStyles.Count < 4)
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
                    SetRow(0, SizeType.AutoSize, 0F);
                    SetRow(1, SizeType.Percent, 100F);
                    SetRow(2, SizeType.Absolute, 0F);
                    SetRow(3, SizeType.Absolute, 0F);
                }
                else
                {
                    SetRow(0, SizeType.Percent, 7F);
                    SetRow(1, SizeType.Percent, 42F);
                    SetRow(2, SizeType.Percent, 51F);
                    SetRow(3, SizeType.AutoSize, 0F);
                }

                if (flpAiVideoGenHeader != null)
                {
                    flpAiVideoGenHeader.Visible = true;
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
                SetRow(3, SizeType.AutoSize, 0F);

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
                    if (!tblAiVideoGenRoot.Controls.Contains(pnlAiVideoGenStickyHost))
                    {
                        tblAiVideoGenRoot.Controls.Add(pnlAiVideoGenStickyHost, 0, 1);
                    }
                }

                if (pnlAiVideoGenActionBar != null)
                {
                    pnlAiVideoGenActionBar.Visible = true;
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

            lblAffiliateDeepSectionTitle = new Label
            {
                Name = "lblAffiliateDeepSectionTitle",
                Text = "Affiliate chuyên sâu — storyboard & kịch bản",
                AutoSize = true,
                Margin = new Padding(0, 6, 12, 4),
                ForeColor = Color.FromArgb(210, 214, 224),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
            };

            flpAffiliateDeepHeaderActions = CreateAiVideoGenActionFlowPanel();
            flpAffiliateDeepHeaderActions.Controls.Add(lblAffiliateDeepSectionTitle);

            btnDeepGenerateScript = CreateToolbarButton("Sinh Script", executeStyle: false, minWidth: 110);
            btnDeepGenerateScript.Name = "btnDeepGenerateScript";
            btnDeepGenerateScript.BackColor = Color.FromArgb(78, 120, 166);
            btnDeepGenerateScript.Click += btnAffiliateGenerateScript_Click;

            btnDeepEditScript = CreateToolbarButton("Sửa Script", executeStyle: false, minWidth: 100);
            btnDeepEditScript.Name = "btnDeepEditScript";
            btnDeepEditScript.BackColor = Color.FromArgb(60, 64, 77);
            btnDeepEditScript.Click += btnAffiliateEditScript_Click;

            flpAffiliateDeepHeaderActions.Controls.Add(btnDeepGenerateScript);
            flpAffiliateDeepHeaderActions.Controls.Add(btnDeepEditScript);

            if (btnRunAffiliateDeepVideo != null)
            {
                flpAffiliateDeepHeaderActions.Controls.Add(btnRunAffiliateDeepVideo);
            }

            btnAffiliateDeepOpenOutput = CreateOpenOutputFolderButton();
            btnAffiliateDeepOpenOutput.Name = "btnAffiliateDeepOpenOutput";
            flpAffiliateDeepHeaderActions.Controls.Add(btnAffiliateDeepOpenOutput);
            flpAffiliateDeepHeaderActions.Controls.Add(CreateClearGridButton());

            pnlAffiliateDeepReadinessHost = new Panel
            {
                Name = "pnlAffiliateDeepReadinessHost",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
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
                AutoSize = true,
                Padding = new Padding(4, 4, 8, 2),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var tblHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAffiliateDeepHeaderHost.BackColor
            };
            tblHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblHeader.Controls.Add(flpAffiliateDeepHeaderActions, 0, 0);
            tblHeader.Controls.Add(pnlAffiliateDeepReadinessHost, 0, 1);
            pnlAffiliateDeepHeaderHost.Controls.Add(tblHeader);

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

            pnlAffiliateDeepScriptReviewHost = new Panel
            {
                Name = "pnlAffiliateDeepScriptReviewHost",
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 2),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAffiliateDeepPreviewPromptHost = new Panel
            {
                Name = "pnlAffiliateDeepPreviewPromptHost",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAffiliateDeepPreviewProgressHost = new Panel
            {
                Name = "pnlAffiliateDeepPreviewProgressHost",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAffiliateDeepPreviewHost = new Panel
            {
                Name = "pnlAffiliateDeepPreviewHost",
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var tblPreview = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlAffiliateDeepPreviewHost.BackColor
            };
            tblPreview.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblPreview.Controls.Add(pnlAffiliateDeepPreviewPromptHost, 0, 0);
            pnlAffiliateDeepPreviewHost.Controls.Add(tblPreview);

            tblAffiliateDeepRoot = new TableLayoutPanel
            {
                Name = "tblAffiliateDeepRoot",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            tblAffiliateDeepRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            tblAffiliateDeepRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepHeaderHost, 0, 0);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepProductHost, 0, 1);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepScriptReviewHost, 0, 2);
            tblAffiliateDeepRoot.Controls.Add(pnlAffiliateDeepPreviewHost, 0, 3);

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
            }
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

            MountAiVideoGenControl(dgvDeepDiveInput, pnlAffiliateDeepProductGridHost);
            MountAiVideoGenControl(pnlAffiliateDeepStoryboardHost, pnlAffiliateDeepStoryboardSlot);
            MountAiVideoGenControl(dgvAiVideoScriptReview, pnlAffiliateDeepScriptReviewHost);
            MountAiVideoGenControl(txtAiVideoGenPrompt, pnlAffiliateDeepPreviewPromptHost);

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

            MountAiVideoGenControl(dgvDeepDiveInput, pnlDeepDiveGridHost);
            RestoreSlideshowScriptPanelLayout();

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
            var navFont = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point);

            pnlSidebar = new Panel
            {
                Name = "pnlSidebar",
                Dock = DockStyle.Left,
                Width = 220,
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
            btnNavWarmup = CreateSidebarNavButton("btnNavWarmup", "🔥  Làm ấm tài khoản", navFont, tabAutoWarmup);

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
                Width = 220,
                Height = 44,
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
            btnNavSettings.Height = 50;

            btnEmergencyStop = new Button
            {
                Name = "btnEmergencyStop",
                Text = "🛑 DỪNG TOÀN BỘ",
                Dock = DockStyle.Bottom,
                Height = 52,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
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
                Width = 220,
                Height = 50,
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
            btn.Click += (sender, e) =>
            {
                SwitchToMainTab(targetTab);
                HighlightSidebarButton((Button)sender);
            };
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
