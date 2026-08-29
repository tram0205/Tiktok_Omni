using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Color AutoPostAiAccent = Color.FromArgb(138, 99, 210);
        private static readonly Color AutoPostPrimaryGreen = Color.FromArgb(76, 175, 80);
        private static readonly Color AutoPostWarningOrange = Color.FromArgb(230, 126, 34);
        private static readonly Color AutoPostValidationErrorBack = Color.FromArgb(100, 45, 45);
        private static readonly Color AutoPostAffiliateStatusAi = Color.FromArgb(120, 200, 140);
        private static readonly Color AutoPostAffiliateStatusManual = Color.FromArgb(180, 190, 210);

        private static readonly Color AutoPostTikTokSurface = Color.FromArgb(26, 42, 46);
        private static readonly Color AutoPostTikTokGroup = Color.FromArgb(34, 54, 58);
        private static readonly Color AutoPostTikTokAccent = Color.FromArgb(0, 210, 200);

        private static readonly Color AutoPostFacebookSurface = Color.FromArgb(28, 38, 56);
        private static readonly Color AutoPostFacebookGroup = Color.FromArgb(36, 48, 70);
        private static readonly Color AutoPostFacebookAccent = Color.FromArgb(120, 168, 255);

        private static readonly Color AutoPostYouTubeSurface = Color.FromArgb(50, 34, 36);
        private static readonly Color AutoPostYouTubeGroup = Color.FromArgb(62, 42, 44);
        private static readonly Color AutoPostYouTubeAccent = Color.FromArgb(255, 120, 120);

        private static readonly Font AutoPostJellyButtonFont = AppJellyButtonFont;
        private const int AutoPostActionButtonHeight = AppJellyButtonHeight;
        private static readonly Color AutoPostTintStart = Color.FromArgb(56, 158, 88);
        private static readonly Color AutoPostTintValidate = Color.FromArgb(168, 128, 52);
        private static readonly Color AutoPostTintCloseBrowser = Color.FromArgb(195, 72, 72);
        private static readonly Color AutoPostTintOpenApproval = Color.FromArgb(88, 118, 178);

        private Label lblAffiliateStatus;
        private Button btnValidatePost;
        private readonly System.Windows.Forms.Timer _autoPostTabRefreshTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _autoPostFolderDebounceTimer = new System.Windows.Forms.Timer();

        // ── AutoPost log panels ───────────────────────────────────────────────
        private RichTextBox rtbAutoPostLog;         // shared bottom log strip
        private RichTextBox rtbLogTikTok;           // per-platform inline logs
        private RichTextBox rtbLogFb;
        private RichTextBox rtbLogYt;
        private const int AutoPostLogMaxLines = 600;

        private void BuildAutoPostUi()
        {
            tabAutoPost.SuspendLayout();
            var darkTab = Color.FromArgb(31, 34, 42);

            BuildAutoPostSharedControls();
            pnlAutoPostShared = BuildAutoPostSharedPanel();

            tabTikTokPost   = new TabPage("TikTok")   { Name = "tabTikTokPost" };
            tabFacebookPost = new TabPage("Facebook") { Name = "tabFacebookPost" };
            tabYouTubePost  = new TabPage("YouTube")  { Name = "tabYouTubePost" };
            ConfigureAutoPostPlatformTab(tabTikTokPost,   AutoPostTikTokSurface);
            ConfigureAutoPostPlatformTab(tabFacebookPost, AutoPostFacebookSurface);
            ConfigureAutoPostPlatformTab(tabYouTubePost,  AutoPostYouTubeSurface);

            tabMultiPlatformPost = new TabControl
            {
                Name = "tabMultiPlatformPost",
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(220, 80),
                Padding = new Point(0, 0),
                BackColor = darkTab,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(10, 4, 10, 4)
            };
            tabMultiPlatformPost.TabPages.Add(tabTikTokPost);
            tabMultiPlatformPost.TabPages.Add(tabFacebookPost);
            tabMultiPlatformPost.TabPages.Add(tabYouTubePost);
            WireAutoPostPlatformTabDraw();

            // Each tab now contains its own toolbar + DataGridView (inline editing)
            MountAutoPostPlatformTab(tabTikTokPost,
                BuildPlatformScheduleContent("TikTok",   AutoPostTikTokAccent,   AutoPostTikTokSurface));
            MountAutoPostPlatformTab(tabFacebookPost,
                BuildPlatformScheduleContent("Facebook", AutoPostFacebookAccent, AutoPostFacebookSurface));
            MountAutoPostPlatformTab(tabYouTubePost,
                BuildPlatformScheduleContent("YouTube",  AutoPostYouTubeAccent,  AutoPostYouTubeSurface));

            pnlAutoPostActions = BuildAutoPostActionsPanel();

            var pnlAutoPostBanner = CreateBannerHostPanel();
            CreateStepBanner(pnlAutoPostBanner,
                "① Duyệt thư mục  →  ② Thêm vào lịch đăng  →  ③ Sửa Caption / Link / Giờ trực tiếp trên lưới  →  ④ BẮT ĐẦU ĐĂNG");

            // 4-row layout: Banner | Shared panel | Platform tabs (grids) | Actions
            var tblAutoPostMain = new TableLayoutPanel
            {
                Name = "tblAutoPostMain",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = darkTab,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblAutoPostMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));   // row 0: banner (đủ cao cho font 12pt)
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // row 1: shared panel
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));     // row 2: platform tabs + grids
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // row 3: actions bar

            pnlAutoPostBanner.Dock    = DockStyle.Fill;
            pnlAutoPostBanner.MinimumSize = new Size(0, 48);
            // Shared panel: Top + AutoSize — không Fill để tránh bị ép chiều cao.
            pnlAutoPostShared.Dock    = DockStyle.Top;
            tabMultiPlatformPost.Dock = DockStyle.Fill;
            pnlAutoPostActions.Dock   = DockStyle.Fill;

            tblAutoPostMain.Controls.Add(pnlAutoPostBanner,    0, 0);
            tblAutoPostMain.Controls.Add(pnlAutoPostShared,    0, 1);
            tblAutoPostMain.Controls.Add(tabMultiPlatformPost, 0, 2);
            tblAutoPostMain.Controls.Add(pnlAutoPostActions,   0, 3);

            tabAutoPost.Controls.Clear();
            tabAutoPost.Controls.Add(tblAutoPostMain);
            tabAutoPost.AutoScroll = false;
            tabAutoPost.AutoScrollMinSize = Size.Empty;

            WireAutoPostComboDropDowns();
            WireAutoPostUxEvents();
            InitializeAutoPostTabAutoRefresh();
            RefreshAutoPostTabOnEnter();
            tabAutoPost.ResumeLayout(true);
            tabAutoPost.PerformLayout();
        }

        private void BuildAutoPostSharedControls()
        {
            txtAutoPostFolder = CreateAutoPostTextBox("txtAutoPostFolder");

            btnBrowseAutoPostFolder = CreateAutoPostButton(
                "btnBrowseAutoPostFolder",
                "Duyệt thư mục",
                AutoPostButtonBack,
                btnBrowseAutoPostFolder_Click);
            btnBrowseAutoPostFolder.AutoSize = false;
            btnBrowseAutoPostFolder.Font = AppInputFont;
            btnBrowseAutoPostFolder.Height = AppDefaultInputHeight;
            // Rộng đủ chữ «Duyệt thư mục» — không để cột Percent của ô path ép hẹp.
            var browseWidth = Math.Max(
                140,
                TextRenderer.MeasureText("Duyệt thư mục", AppInputFont).Width + 36);
            btnBrowseAutoPostFolder.Width = browseWidth;
            btnBrowseAutoPostFolder.MinimumSize = new Size(browseWidth, AppDefaultInputHeight);
            btnBrowseAutoPostFolder.MaximumSize = new Size(browseWidth, AppDefaultInputHeight);
            btnBrowseAutoPostFolder.Padding = new Padding(10, 6, 10, 6);

            cbAutoPostVideoFile = new ComboBox
            {
                Name = "cbAutoPostVideoFile",
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                IntegralHeight = false
            };

            cbAutoPostVideoType = new ComboBox
            {
                Name = "cbAutoPostVideoType",
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                IntegralHeight = false
            };
            foreach (VideoStorageType t in Enum.GetValues(typeof(VideoStorageType)))
            {
                cbAutoPostVideoType.Items.Add(t);
            }

            cbAutoPostVideoType.SelectedItem = VideoStorageType.Reup;
            cbAutoPostVideoType.SelectedIndexChanged += cbAutoPostVideoType_SelectedIndexChanged;

            cbAutoPostProfile = new ComboBox
            {
                Name = "cbAutoPostProfile",
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                IntegralHeight = false
            };
            cbAutoPostProfile.Items.Add("default");
            cbAutoPostProfile.SelectedIndex = 0;
            cbAutoPostProfile.SelectedIndexChanged += cbAutoPostProfile_SelectedIndexChanged;

            txtAutoPostHashtags = CreateAutoPostTextBox("txtAutoPostHashtags");
            txtAutoPostHashtags.Dock = DockStyle.Fill;

            cbCaptionStyle = new ComboBox
            {
                Name = "cbCaptionStyle",
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                IntegralHeight = false
            };
            cbCaptionStyle.Items.AddRange(new object[]
            {
                "Kiến thức",
                "Quote",
                "Hài hước",
                "Câu hỏi / Tranh cãi"
            });
            cbCaptionStyle.SelectedIndex = 0;

            chkEnableAffiliateLink = new CheckBox
            {
                Name = "chkEnableAffiliateLink",
                Text = "Gắn link Affiliate (tắt = video nuôi kênh, bỏ qua link dù đã có trong dữ liệu)",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 6, 0, 4)
            };
        }

        private Panel BuildAutoPostSharedPanel()
        {
            var root = new TableLayoutPanel
            {
                Name = "pnlAutoPostShared",
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = AutoPostPanelBack,
                // Padding + khoảng cách dòng tăng ~25% cho thoáng hơn.
                Padding = new Padding(12),
                Margin = Padding.Empty,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Hàng 1: label + nút cố định độ rộng; ô path rút ngắn để chữ không bị che.
            const int folderLabelWidth = 280;
            var pnlFolder = new FlowLayoutPanel
            {
                Name = "flpAutoPostFolderRow",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AutoPostPanelBack,
                Margin = new Padding(0, 0, 0, 14),
                Padding = Padding.Empty
            };
            var lblFolder = CreateAutoPostLabel("Thư mục video");
            lblFolder.AutoSize = false;
            lblFolder.Width = folderLabelWidth;
            lblFolder.Height = AppDefaultInputHeight;
            lblFolder.TextAlign = ContentAlignment.MiddleLeft;
            lblFolder.Font = AppLabelFont;
            lblFolder.Margin = new Padding(0, 0, 8, 0);

            btnBrowseAutoPostFolder.Dock = DockStyle.None;
            btnBrowseAutoPostFolder.Anchor = AnchorStyles.Left;
            btnBrowseAutoPostFolder.Margin = new Padding(0, 0, 8, 0);

            // Ô path vừa đủ — không kéo full hàng (để label/nút luôn hiện đủ chữ).
            const int folderPathWidth = 1000;
            txtAutoPostFolder.Dock = DockStyle.None;
            txtAutoPostFolder.Anchor = AnchorStyles.Left;
            txtAutoPostFolder.Width = folderPathWidth;
            txtAutoPostFolder.Height = AppDefaultInputHeight;
            txtAutoPostFolder.MinimumSize = new Size(folderPathWidth, AppDefaultInputHeight);
            txtAutoPostFolder.MaximumSize = new Size(folderPathWidth, AppDefaultInputHeight);
            txtAutoPostFolder.Margin = Padding.Empty;
            ApplyAppInputChrome(txtAutoPostFolder);
            txtAutoPostFolder.Width = folderPathWidth;
            txtAutoPostFolder.Height = AppDefaultInputHeight;

            pnlFolder.Controls.Add(lblFolder);
            pnlFolder.Controls.Add(btnBrowseAutoPostFolder);
            pnlFolder.Controls.Add(txtAutoPostFolder);

            // Hàng 2: 4 cột đều — Profile | Loại video | Video file | Caption Style
            var tblFour = new TableLayoutPanel
            {
                Name = "tblAutoPostSharedFourCols",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = AutoPostPanelBack,
                Margin = new Padding(0, 0, 0, 10),
                Padding = Padding.Empty
            };
            for (var i = 0; i < 4; i++)
            {
                tblFour.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            tblFour.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblFour.Controls.Add(CreateAutoPostLabeledComboColumn("Profile", cbAutoPostProfile, isLast: false), 0, 0);
            tblFour.Controls.Add(CreateAutoPostLabeledComboColumn("Loại video", cbAutoPostVideoType, isLast: false), 1, 0);
            tblFour.Controls.Add(CreateAutoPostLabeledComboColumn("Video file", cbAutoPostVideoFile, isLast: false), 2, 0);
            tblFour.Controls.Add(CreateAutoPostLabeledComboColumn("Caption Style", cbCaptionStyle, isLast: true), 3, 0);

            chkEnableAffiliateLink.Margin = new Padding(0, 8, 0, 4);
            chkEnableAffiliateLink.Dock = DockStyle.Top;

            root.Controls.Add(pnlFolder, 0, 0);
            root.Controls.Add(tblFour, 0, 1);
            root.Controls.Add(chkEnableAffiliateLink, 0, 2);
            return root;
        }

        private static Control CreateAutoPostLabeledComboColumn(string caption, ComboBox combo, bool isLast = false)
        {
            // Label rộng cố định (đủ chữ, 1 dòng) + combo co ngắn; khoảng cách giữa các cột.
            var labelWidth = Math.Max(
                72,
                TextRenderer.MeasureText(caption, AppLabelFont).Width + 8);

            var col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, isLast ? 0 : 16, 0),
                Padding = Padding.Empty,
                BackColor = AutoPostPanelBack,
                Height = AppDefaultInputHeight
            };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, AppDefaultInputHeight));

            var lbl = CreateAutoPostLabel(caption);
            lbl.AutoSize = false;
            lbl.Dock = DockStyle.Fill;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Font = AppLabelFont;
            lbl.Margin = new Padding(0, 0, 6, 0);
            lbl.MinimumSize = new Size(labelWidth, AppDefaultInputHeight);
            // Không wrap / không cắt chữ.
            lbl.AutoEllipsis = false;

            combo.Dock = DockStyle.Fill;
            combo.Margin = Padding.Empty;
            combo.Height = AppDefaultInputHeight;
            // Min thấp để ô co trước, chừa chỗ label đầy đủ.
            combo.MinimumSize = new Size(48, AppDefaultInputHeight);
            combo.MaximumSize = Size.Empty;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            ApplyAppInputChrome(combo);
            combo.MinimumSize = new Size(48, AppDefaultInputHeight);

            col.Controls.Add(lbl, 0, 0);
            col.Controls.Add(combo, 1, 0);
            return col;
        }

        // These three methods are no longer called — each tab now hosts a
        // DataGridView built by BuildPlatformScheduleContent() in
        // Form1.AutoPostScheduleQueue.cs. Kept as empty stubs so any surviving
        // call sites compile without error.
        private Control BuildAutoPostTikTokPanel()
        {
            var root = CreateAutoPostPlatformRoot(AutoPostTikTokSurface);
            root.Dock = DockStyle.Top;
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.Padding = new Padding(8, 8, 8, 12);

            chkAutoPostEnableTikTok = CreateAutoPostChannelCheckBox(
                "chkAutoPostEnableTikTok",
                "Bật đăng lên kênh này");
            AddAutoPostRootRow(root, chkAutoPostEnableTikTok, SizeType.AutoSize);

            var pnlSchedule = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                BackColor = root.BackColor,
                Margin = new Padding(0, 0, 0, 2),
                Padding = new Padding(0, 0, 0, 4)
            };
            pnlSchedule.Controls.Add(CreateAutoPostLabel("Giờ đăng TikTok:"));
            dtpAutoPostTikTok = CreateAutoPostSchedulePicker("dtpAutoPostTikTok");
            dtpAutoPostTikTok.Margin = new Padding(8, 4, 0, 4);
            pnlSchedule.Controls.Add(dtpAutoPostTikTok);
            AddAutoPostRootRow(root, pnlSchedule, SizeType.AutoSize);

            var tblAffiliate = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = root.BackColor,
                Margin = new Padding(0, 2, 0, 4)
            };
            tblAffiliate.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            tblAffiliate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            txtAutoPostAffiliateLink = CreateAutoPostTextBox("txtAutoPostAffiliateLink");
            txtAutoPostAffiliateLink.Dock = DockStyle.Fill;
            txtAutoPostAffiliateLink.Margin = new Padding(0, 4, 0, 4);
            txtAutoPostAffiliateLink.MinimumSize = new Size(0, 32);
            AddAutoPostTableRow(tblAffiliate, 0, "Link Affiliate", txtAutoPostAffiliateLink, compactField: true);
            AddAutoPostRootRow(root, tblAffiliate, SizeType.AutoSize);

            lblAffiliateStatus = new Label
            {
                Name = "lblAffiliateStatus",
                AutoSize = true,
                AutoEllipsis = true,
                Dock = DockStyle.Top,
                ForeColor = Color.Gainsboro,
                Font = AppLabelItalicFont,
                Padding = new Padding(112, 0, 0, 4),
                Margin = new Padding(0, 0, 0, 4),
                MaximumSize = new Size(900, 40)
            };
            AddAutoPostRootRow(root, lblAffiliateStatus, SizeType.AutoSize);

            var grpTikTokCaption = CreateAutoPostPlatformGroupBox(
                "grpTikTokAi",
                "Caption TikTok",
                AutoPostTikTokGroup,
                AutoPostTikTokAccent);
            var tblTikTokCaption = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = grpTikTokCaption.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblTikTokCaption.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblTikTokCaption.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblTikTokCaption.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var flpTikTokAi = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = grpTikTokCaption.BackColor,
                Padding = new Padding(0, 0, 0, 4),
                Margin = Padding.Empty
            };
            btnGenerateGeminiCaption = CreateAutoPostButton(
                "btnGenerateGeminiCaption",
                "Tạo caption (Gemini)",
                AutoPostAiAccent,
                btnGenerateGeminiCaption_Click);
            btnGenerateGeminiCaption.ForeColor = Color.White;
            btnGenerateGeminiCaption.AutoSize = true;
            btnGenerateGeminiCaption.Padding = new Padding(10, 4, 10, 4);
            btnGenerateGeminiCaption.Margin = new Padding(0, 0, 8, 0);

            btnPreviewAutoPostVideo = CreateAutoPostButton(
                "btnPreviewAutoPostVideo",
                "Xem video",
                AutoPostButtonBack,
                btnPreviewAutoPostVideo_Click);
            btnPreviewAutoPostVideo.AutoSize = true;
            btnPreviewAutoPostVideo.Padding = new Padding(10, 4, 10, 4);
            btnPreviewAutoPostVideo.Margin = new Padding(0, 0, 0, 0);
            flpTikTokAi.Controls.Add(btnGenerateGeminiCaption);
            flpTikTokAi.Controls.Add(btnPreviewAutoPostVideo);

            txtAutoPostCaption = CreateAutoPostTextBox("txtAutoPostCaption", multiline: true);
            txtAutoPostCaption.Dock = DockStyle.Fill;
            txtAutoPostCaption.MinimumSize = new Size(0, 88);
            txtAutoPostCaption.ScrollBars = ScrollBars.Vertical;

            var pnlTikTokCaptionHost = new Panel
            {
                Name = "pnlTikTokCaptionHost",
                Height = 88,
                MinimumSize = new Size(0, 88),
                AutoSize = false,
                Dock = DockStyle.Top,
                BackColor = grpTikTokCaption.BackColor,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            pnlTikTokCaptionHost.Controls.Add(txtAutoPostCaption);

            var pnlTikTokPostOptions = new Panel
            {
                Name = "pnlTikTokPostOptions",
                Height = 44,
                MinimumSize = new Size(0, 44),
                AutoSize = false,
                Dock = DockStyle.Top,
                BackColor = grpTikTokCaption.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var flpTikTokPostOptions = new FlowLayoutPanel
            {
                Name = "flpTikTokPostOptions",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = false,
                BackColor = grpTikTokCaption.BackColor,
                Padding = new Padding(0, 2, 0, 0)
            };

            chkAutoPostVideoApproved = CreateAutoPostOptionCheckBox(
                "chkAutoPostVideoApproved",
                "Đã duyệt video trước khi đăng",
                rightMarginPx: 16);
            chkAutoPostUploadOnly = CreateAutoPostOptionCheckBox(
                "chkAutoPostUploadOnly",
                "Chỉ upload — không tự bấm Đăng (để gắn link / chỉnh tay)",
                rightMarginPx: 0);
            flpTikTokPostOptions.Controls.Add(chkAutoPostVideoApproved);
            flpTikTokPostOptions.Controls.Add(chkAutoPostUploadOnly);
            pnlTikTokPostOptions.Controls.Add(flpTikTokPostOptions);

            tblTikTokCaption.Controls.Add(flpTikTokAi, 0, 0);
            tblTikTokCaption.Controls.Add(pnlTikTokCaptionHost, 0, 1);
            tblTikTokCaption.Controls.Add(pnlTikTokPostOptions, 0, 2);
            grpTikTokCaption.Controls.Add(tblTikTokCaption);
            AddAutoPostRootRow(root, grpTikTokCaption, SizeType.AutoSize);

            return root;
        }

        private Control BuildAutoPostFacebookPanel()
        {
            var root = CreateAutoPostPlatformRoot(AutoPostFacebookSurface);
            root.Dock = DockStyle.Top;
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.Padding = new Padding(8, 8, 8, 12);

            chkAutoPostEnableFacebook = CreateAutoPostChannelCheckBox(
                "chkAutoPostEnableFacebook",
                "Bật đăng lên kênh này");
            AddAutoPostRootRow(root, chkAutoPostEnableFacebook, SizeType.AutoSize);

            var pnlSchedule = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = root.BackColor
            };
            pnlSchedule.Controls.Add(CreateAutoPostLabel("Giờ đăng Facebook:"));
            dtpAutoPostFacebook = CreateAutoPostSchedulePicker("dtpAutoPostFacebook");
            dtpAutoPostFacebook.Margin = new Padding(8, 4, 0, 4);
            pnlSchedule.Controls.Add(dtpAutoPostFacebook);
            AddAutoPostRootRow(root, pnlSchedule, SizeType.AutoSize);

            chkAutoPostFbAttachShopee = new CheckBox
            {
                Name = "chkAutoPostFbAttachShopee",
                Text = "Gắn link sản phẩm Shopee khi đăng Facebook Reels",
                AutoSize = true,
                Font = WarmupFieldFont,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 4, 0, 2)
            };
            AddAutoPostRootRow(root, chkAutoPostFbAttachShopee, SizeType.AutoSize);

            var tblFbShopee = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = root.BackColor,
                Margin = new Padding(0, 2, 0, 4)
            };
            tblFbShopee.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
            tblFbShopee.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            txtAutoPostFbShopeeLink = CreateAutoPostTextBox("txtAutoPostFbShopeeLink");
            txtAutoPostFbShopeeLink.Dock = DockStyle.Fill;
            txtAutoPostFbShopeeLink.Margin = new Padding(0, 4, 0, 4);
            txtAutoPostFbShopeeLink.MinimumSize = new Size(0, 32);
            AddAutoPostTableRow(tblFbShopee, 0, "Link Shopee", txtAutoPostFbShopeeLink, compactField: true);
            AddAutoPostRootRow(root, tblFbShopee, SizeType.AutoSize);

            var flpFbShopeeActions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = root.BackColor,
                Margin = new Padding(0, 0, 0, 4)
            };
            btnFetchFbShopeeInfo = CreateAutoPostButton(
                "btnFetchFbShopeeInfo",
                "Kiểm tra link Shopee",
                AutoPostFacebookAccent);
            btnFetchFbShopeeInfo.ForeColor = Color.White;
            btnFetchFbShopeeInfo.AutoSize = true;
            btnFetchFbShopeeInfo.Padding = new Padding(10, 4, 10, 4);
            btnFetchFbShopeeInfo.Margin = new Padding(0, 2, 8, 2);
            btnFetchFbShopeeInfo.Click += btnFetchFbShopeeInfo_Click;
            flpFbShopeeActions.Controls.Add(btnFetchFbShopeeInfo);
            lblFbShopeeStatus = new Label
            {
                Name = "lblFbShopeeStatus",
                AutoSize = true,
                ForeColor = Color.FromArgb(170, 178, 195),
                Font = WarmupLabelFont,
                Margin = new Padding(0, 6, 0, 2),
                Text = "Dán link shopee.vn hoặc shp.ee — hệ thống thử gắn trên Reels, không được sẽ chèn vào caption."
            };
            flpFbShopeeActions.Controls.Add(lblFbShopeeStatus);
            AddAutoPostRootRow(root, flpFbShopeeActions, SizeType.AutoSize);

            var grpFacebookAi = CreateAutoPostPlatformGroupBox(
                "grpFacebookAi",
                "Facebook — Tạo caption (Gemini)",
                AutoPostFacebookGroup,
                AutoPostFacebookAccent);
            grpFacebookAi.AutoSize = false;
            grpFacebookAi.MinimumSize = new Size(0, 112);

            var tblFacebookAi = new TableLayoutPanel
            {
                Name = "tblFacebookGeminiCaption",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = grpFacebookAi.BackColor,
                Padding = new Padding(6, 10, 6, 8)
            };
            tblFacebookAi.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168F));
            tblFacebookAi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblFacebookAi.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            btnGenerateGeminiCaptionFb = CreateAutoPostButton(
                "btnGenerateGeminiCaptionFb",
                "Tạo caption\n(Gemini)",
                AutoPostAiAccent,
                btnGenerateGeminiCaptionFb_Click);
            btnGenerateGeminiCaptionFb.ForeColor = Color.White;
            btnGenerateGeminiCaptionFb.AutoSize = false;
            btnGenerateGeminiCaptionFb.Size = new Size(152, 52);
            btnGenerateGeminiCaptionFb.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnGenerateGeminiCaptionFb.Margin = new Padding(0, 2, 8, 0);

            txtAutoPostFbCaption = CreateAutoPostTextBox("txtAutoPostFbCaption", multiline: true);
            txtAutoPostFbCaption.Dock = DockStyle.Fill;
            txtAutoPostFbCaption.MinimumSize = new Size(160, 88);
            txtAutoPostFbCaption.ScrollBars = ScrollBars.Vertical;
            txtAutoPostFbCaption.Margin = Padding.Empty;

            tblFacebookAi.Controls.Add(btnGenerateGeminiCaptionFb, 0, 0);
            tblFacebookAi.Controls.Add(txtAutoPostFbCaption, 1, 0);
            grpFacebookAi.Controls.Add(tblFacebookAi);
            AddAutoPostRootRow(root, grpFacebookAi, SizeType.AutoSize);

            var tblHashtags = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = root.BackColor,
                Margin = new Padding(0, 6, 0, 4)
            };
            tblHashtags.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
            tblHashtags.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            txtAutoPostFbHashtags = CreateAutoPostTextBox("txtAutoPostFbHashtags");
            txtAutoPostFbHashtags.Dock = DockStyle.Fill;
            AddAutoPostTableRow(tblHashtags, 0, "Hashtag Facebook", txtAutoPostFbHashtags);
            AddAutoPostRootRow(root, tblHashtags, SizeType.AutoSize);

            return root;
        }

        private Control BuildAutoPostYouTubePanel()
        {
            var root = CreateAutoPostPlatformRoot(AutoPostYouTubeSurface);

            chkAutoPostEnableYouTube = CreateAutoPostChannelCheckBox(
                "chkAutoPostEnableYouTube",
                "Bật đăng lên kênh này");
            AddAutoPostRootRow(root, chkAutoPostEnableYouTube, SizeType.AutoSize);

            var pnlSchedule = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = root.BackColor
            };
            pnlSchedule.Controls.Add(CreateAutoPostLabel("Giờ đăng YouTube Shorts:"));
            dtpAutoPostYouTube = CreateAutoPostSchedulePicker("dtpAutoPostYouTube");
            dtpAutoPostYouTube.Margin = new Padding(8, 4, 0, 4);
            pnlSchedule.Controls.Add(dtpAutoPostYouTube);
            AddAutoPostRootRow(root, pnlSchedule, SizeType.AutoSize);

            var tblTitle = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = root.BackColor
            };
            tblTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
            tblTitle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            txtAutoPostYtTitle = CreateAutoPostTextBox("txtAutoPostYtTitle");
            txtAutoPostYtTitle.Dock = DockStyle.Fill;
            txtAutoPostYtTitle.MaxLength = 60;
            AddAutoPostTableRow(tblTitle, 0, "Tiêu đề ngắn (< 60 ký tự)", txtAutoPostYtTitle, compactField: true);
            AddAutoPostRootRow(root, tblTitle, SizeType.AutoSize);

            var grpYoutubeAi = CreateAutoPostPlatformGroupBox(
                "grpYoutubeAi",
                "YouTube — Tạo caption (Gemini)",
                AutoPostYouTubeGroup,
                AutoPostYouTubeAccent);
            var flpYoutubeAi = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = grpYoutubeAi.BackColor,
                Padding = new Padding(4, 2, 4, 4)
            };
            btnGenerateGeminiCaptionYoutube = CreateAutoPostButton(
                "btnGenerateGeminiCaptionYoutube",
                "Tạo tiêu đề SEO (Gemini)",
                AutoPostAiAccent,
                btnGenerateGeminiCaptionYoutube_Click);
            btnGenerateGeminiCaptionYoutube.ForeColor = Color.White;
            btnGenerateGeminiCaptionYoutube.AutoSize = true;
            btnGenerateGeminiCaptionYoutube.Padding = new Padding(10, 4, 10, 4);
            btnGenerateGeminiCaptionYoutube.Margin = new Padding(8, 2, 8, 2);
            flpYoutubeAi.Controls.Add(btnGenerateGeminiCaptionYoutube);
            grpYoutubeAi.Controls.Add(flpYoutubeAi);
            AddAutoPostRootRow(root, grpYoutubeAi, SizeType.AutoSize);

            var lblYtDesc = CreateAutoPostLabel("Mô tả dài (SEO, CTA, hashtag):");
            lblYtDesc.Margin = new Padding(0, 4, 0, 2);
            AddAutoPostRootRow(root, lblYtDesc, SizeType.AutoSize);

            txtAutoPostYtDescription = CreateAutoPostTextBox("txtAutoPostYtDescription", multiline: true);
            txtAutoPostYtDescription.Dock = DockStyle.Fill;
            txtAutoPostYtDescription.MinimumSize = new Size(0, 120);
            AddAutoPostRootRow(root, txtAutoPostYtDescription, SizeType.Percent, 100F);

            return root;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  LOG PANEL  (real-time progress for the schedule queue)
        // ─────────────────────────────────────────────────────────────────────────

        private Panel BuildAutoPostLogPanel()
        {
            var pnl = new Panel
            {
                Name      = "pnlAutoPostLog",
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                Padding   = Padding.Empty,
                Margin    = Padding.Empty
            };

            // Header bar: title label + clear button
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 26,
                BackColor = Color.FromArgb(30, 32, 40),
                Padding   = Padding.Empty
            };

            var lblTitle = new Label
            {
                Text      = "  LOG — tiến trình đăng bài",
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(160, 180, 220),
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding   = new Padding(4, 0, 0, 0)
            };

            var btnClear = new Button
            {
                Text      = "Xóa",
                Dock      = DockStyle.Right,
                Width     = 52,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 48, 58),
                ForeColor = Color.FromArgb(160, 160, 160),
                Font      = new Font("Segoe UI", 8F),
                Cursor    = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) =>
            {
                if (rtbAutoPostLog != null && !rtbAutoPostLog.IsDisposed)
                    rtbAutoPostLog.Clear();
            };

            header.Controls.Add(lblTitle);
            header.Controls.Add(btnClear);

            rtbAutoPostLog = new RichTextBox
            {
                Name        = "rtbAutoPostLog",
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                BackColor   = Color.FromArgb(18, 20, 26),
                ForeColor   = Color.FromArgb(200, 205, 215),
                Font        = new Font("Consolas", 8.5F),
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                WordWrap    = true,
                Padding     = new Padding(4, 2, 4, 2)
            };

            pnl.Controls.Add(rtbAutoPostLog);
            pnl.Controls.Add(header);
            return pnl;
        }

        /// <summary>
        /// Thread-safe, color-coded append to any <see cref="RichTextBox"/>.
        /// Trims to <see cref="AutoPostLogMaxLines"/> lines to prevent unbounded growth.
        /// </summary>
        internal void AppendToRtb(RichTextBox rtb, string msg)
        {
            if (rtb == null || rtb.IsDisposed) return;

            void Append()
            {
                var ts   = DateTime.Now.ToString("HH:mm:ss");
                var line = $"[{ts}] {msg}";

                Color col;
                if (msg.Contains("✓") || msg.Contains("Hoàn tất") || msg.Contains("Done") ||
                    msg.Contains("xong") || msg.Contains("đã gửi"))
                    col = Color.FromArgb(100, 220, 100);
                else if (msg.Contains("✗") || msg.Contains("lỗi") || msg.Contains("Lỗi") ||
                         msg.Contains("Failed") || msg.Contains("thất bại") || msg.Contains("error"))
                    col = Color.FromArgb(255, 90, 90);
                else if (msg.Contains("⊗") || msg.Contains("Hủy") || msg.Contains("Bỏ qua") ||
                         msg.Contains("không bấm được"))
                    col = Color.FromArgb(255, 185, 50);
                else if (msg.Contains("[Human]") || msg.Contains("Lướt trang") || msg.Contains("Jitter"))
                    col = Color.FromArgb(100, 180, 255);
                else if (msg.Contains("[Lịch đăng]") || msg.Contains("Xếp hàng") || msg.Contains("Queued"))
                    col = Color.FromArgb(180, 160, 255);
                else if (msg.Contains("[Caption AI]") || msg.Contains("Gemini") || msg.Contains("hashtag"))
                    col = Color.FromArgb(200, 160, 240);
                else if (msg.Contains("Auto Post") || msg.Contains("TikTok") ||
                         msg.Contains("Facebook") || msg.Contains("YouTube"))
                    col = Color.FromArgb(220, 220, 180);
                else
                    col = Color.FromArgb(190, 195, 205);

                rtb.SelectionStart  = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.SelectionColor  = col;
                rtb.AppendText(line + Environment.NewLine);
                rtb.ScrollToCaret();

                if (rtb.Lines.Length > AutoPostLogMaxLines)
                {
                    var trimmed = string.Join(
                        Environment.NewLine,
                        rtb.Lines.Skip(AutoPostLogMaxLines / 5));
                    rtb.Clear();
                    rtb.AppendText(trimmed + Environment.NewLine);
                    rtb.SelectionStart = rtb.TextLength;
                    rtb.ScrollToCaret();
                }
            }

            if (rtb.InvokeRequired)
                rtb.BeginInvoke((Action)Append);
            else
                Append();
        }

        /// <summary>Appends to the shared AutoPost log strip at the bottom of the tab.</summary>
        internal void AppendToAutoPostLog(string msg) => AppendToRtb(rtbAutoPostLog, msg);

        /// <summary>
        /// Writes to: main app log (+ session file) + shared AutoPost strip.
        /// Use for schedule-queue / caption-AI messages not tied to a specific platform.
        /// </summary>
        internal void LogAutoPost(string msg)
        {
            Log(msg);
            AppendToAutoPostLog(msg);
        }

        // ── Per-platform log helpers ──────────────────────────────────────────
        // Each method writes to: main log (file), shared strip, AND the inline
        // per-platform RichTextBox inside the platform's own tab.

        internal void LogTikTok(string msg)
        {
            Log($"[TikTok] {msg}");
            AppendToAutoPostLog($"[TikTok] {msg}");
            AppendToRtb(rtbLogTikTok, msg);
        }

        internal void LogFacebook(string msg)
        {
            Log($"[Facebook] {msg}");
            AppendToAutoPostLog($"[Facebook] {msg}");
            AppendToRtb(rtbLogFb, msg);
        }

        internal void LogYouTube(string msg)
        {
            Log($"[YouTube] {msg}");
            AppendToAutoPostLog($"[YouTube] {msg}");
            AppendToRtb(rtbLogYt, msg);
        }

        private Panel BuildAutoPostActionsPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlAutoPostActions",
                Dock = DockStyle.Fill,
                AutoSize = false,
                MinimumSize = new Size(0, AutoPostActionButtonHeight + 16),
                BackColor = AutoPostPanelBack,
                Padding = new Padding(0)
            };

            var flp = new FlowLayoutPanel
            {
                Name = "flpAutoPostActions",
                Dock = DockStyle.Fill,
                AutoSize = false,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                AutoScroll = false,
                Padding = new Padding(12, 8, 16, 8),
                BackColor = AutoPostPanelBack
            };

            btnStartAutoPost = CreateAutoPostJellyButton(
                "btnStartAutoPost",
                "BẮT ĐẦU ĐĂNG",
                AutoPostTintStart,
                btnStartAutoPost_Click);

            btnValidatePost = CreateAutoPostJellyButton(
                "btnValidatePost",
                "Kiểm tra trước khi đăng",
                AutoPostTintValidate,
                btnValidatePost_Click);

            btnCloseAutoPostBrowser = CreateAutoPostJellyButton(
                "btnCloseAutoPostBrowser",
                "Đóng trình duyệt Auto Post",
                AutoPostTintCloseBrowser,
                btnCloseAutoPostBrowser_Click);

            var btnAutoPostOpenApproval = CreateAutoPostJellyButton(
                "btnAutoPostOpenApproval",
                "Mở Hàng duyệt",
                AutoPostTintOpenApproval);
            btnAutoPostOpenApproval.Click += btnOpenApprovalQueue_Click;

            flp.Controls.Add(btnStartAutoPost);
            flp.Controls.Add(btnValidatePost);
            flp.Controls.Add(btnCloseAutoPostBrowser);
            flp.Controls.Add(btnAutoPostOpenApproval);
            pnl.Controls.Add(flp);
            return pnl;
        }

        private static CheckBox CreateAutoPostOptionCheckBox(string name, string text, int rightMarginPx)
        {
            const int checkHeight = 28;
            const int checkPad = 22;
            var font = SystemFonts.MessageBoxFont;
            var textWidth = TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, checkHeight),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width;
            var width = Math.Max(120, textWidth + checkPad);

            return new CheckBox
            {
                Name = name,
                Text = text,
                Font = font,
                AutoSize = false,
                Size = new Size(width, checkHeight),
                MinimumSize = new Size(width, checkHeight),
                MaximumSize = new Size(width, checkHeight),
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 4, rightMarginPx, 4),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static JellyButton CreateAutoPostJellyButton(string name, string text, Color tint, EventHandler click = null)
        {
            var btn = CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: AutoPostActionButtonHeight,
                margin: new Padding(8, 4, 0, 4));

            if (click != null)
            {
                btn.Click += click;
            }

            return btn;
        }

        private static int MeasureAutoPostJellyButtonTextWidth(string text)
        {
            return MeasureAppJellyButtonTextWidth(text, AutoPostJellyButtonFont, AutoPostActionButtonHeight);
        }

        private static GroupBox CreateAutoPostPlatformGroupBox(string name, string title, Color backColor, Color titleColor)
        {
            return new GroupBox
            {
                Name = name,
                Text = title,
                Dock = DockStyle.Fill,
                AutoSize = true,
                ForeColor = titleColor,
                BackColor = backColor,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 4, 0, 6)
            };
        }

        private static TableLayoutPanel CreateAutoPostPlatformRoot(Color surfaceBack)
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = surfaceBack,
                Padding = new Padding(8)
            };
        }

        private static void ConfigureAutoPostPlatformTab(TabPage page, Color surfaceBack)
        {
            if (page == null)
            {
                return;
            }

            page.BackColor = surfaceBack;
            page.ForeColor = Color.Gainsboro;
            page.UseVisualStyleBackColor = false;
        }

        private void WireAutoPostPlatformTabDraw()
        {
            if (tabMultiPlatformPost == null)
            {
                return;
            }

            tabMultiPlatformPost.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabMultiPlatformPost.DrawItem += AutoPostPlatformTab_DrawItem;
            // Vùng trống bên phải các tab (Windows mặc định xám/trắng) → tô đen.
            tabMultiPlatformPost.Paint += AutoPostPlatformTab_PaintStripBackground;
        }

        private void AutoPostPlatformTab_PaintStripBackground(object sender, PaintEventArgs e)
        {
            if (tabMultiPlatformPost == null || tabMultiPlatformPost.TabCount == 0)
            {
                return;
            }

            // Chỉ tô khoảng trống bên phải tab cuối (vùng Windows hay để xám/trắng).
            var stripHeight = Math.Max(tabMultiPlatformPost.ItemSize.Height, tabMultiPlatformPost.DisplayRectangle.Top);
            if (stripHeight <= 0)
            {
                return;
            }

            var last = tabMultiPlatformPost.GetTabRect(tabMultiPlatformPost.TabCount - 1);
            var fillLeft = Math.Max(0, last.Right);
            var fillWidth = tabMultiPlatformPost.ClientSize.Width - fillLeft;
            if (fillWidth <= 0)
            {
                return;
            }

            using (var brush = new SolidBrush(Color.FromArgb(20, 22, 28)))
            {
                e.Graphics.FillRectangle(brush, fillLeft, 0, fillWidth, stripHeight);
            }
        }

        private void AutoPostPlatformTab_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (tabMultiPlatformPost == null || e.Index < 0 || e.Index >= tabMultiPlatformPost.TabPages.Count)
            {
                return;
            }

            var page = tabMultiPlatformPost.TabPages[e.Index];
            var surface = ResolveAutoPostPlatformSurface(page);
            var accent = ResolveAutoPostPlatformAccent(page);
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = e.Bounds;

            using (var backBrush = new SolidBrush(selected ? surface : Color.FromArgb(20, 22, 28)))
            {
                e.Graphics.FillRectangle(backBrush, bounds);
            }

            if (selected)
            {
                using (var accentBrush = new SolidBrush(accent))
                {
                    e.Graphics.FillRectangle(accentBrush, bounds.Left + 2, bounds.Bottom - 3, bounds.Width - 4, 3);
                }
            }

            // Mỗi tab 1 màu chữ (accent nền tảng); in đậm + tăng 2pt so với font tab.
            var baseSize = tabMultiPlatformPost.Font?.Size ?? AppLabelFontSize;
            using (var tabFont = new Font(
                tabMultiPlatformPost.Font?.FontFamily ?? AppLabelFont.FontFamily,
                baseSize + 2F,
                FontStyle.Bold,
                GraphicsUnit.Point))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    page.Text,
                    tabFont,
                    bounds,
                    accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            e.DrawFocusRectangle();
        }

        private static Color ResolveAutoPostPlatformSurface(TabPage page)
        {
            if (page == null)
            {
                return Color.FromArgb(31, 34, 42);
            }

            if (string.Equals(page.Name, "tabTikTokPost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.Text, "TikTok", StringComparison.OrdinalIgnoreCase))
            {
                return AutoPostTikTokSurface;
            }

            if (string.Equals(page.Name, "tabFacebookPost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.Text, "Facebook", StringComparison.OrdinalIgnoreCase))
            {
                return AutoPostFacebookSurface;
            }

            if (string.Equals(page.Name, "tabYouTubePost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.Text, "YouTube", StringComparison.OrdinalIgnoreCase))
            {
                return AutoPostYouTubeSurface;
            }

            return Color.FromArgb(31, 34, 42);
        }

        private static Color ResolveAutoPostPlatformAccent(TabPage page)
        {
            if (page == null)
            {
                return Color.Gainsboro;
            }

            if (string.Equals(page.Name, "tabTikTokPost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.Text, "TikTok", StringComparison.OrdinalIgnoreCase))
            {
                return AutoPostTikTokAccent;
            }

            if (string.Equals(page.Name, "tabFacebookPost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.Text, "Facebook", StringComparison.OrdinalIgnoreCase))
            {
                return AutoPostFacebookAccent;
            }

            if (string.Equals(page.Name, "tabYouTubePost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.Text, "YouTube", StringComparison.OrdinalIgnoreCase))
            {
                return AutoPostYouTubeAccent;
            }

            return Color.Gainsboro;
        }

        private static void MountAutoPostPlatformTab(TabPage tab, Control content, bool scrollContent = false)
        {
            tab.AutoScroll = scrollContent;
            tab.AutoScrollMinSize = Size.Empty;
            tab.Padding = new Padding(4, 4, 4, 4);
            tab.Controls.Clear();

            var host = new Panel
            {
                Name = tab.Name + "_Host",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = tab.BackColor,
                AutoScroll = scrollContent
            };
            content.Dock = scrollContent ? DockStyle.Top : DockStyle.Fill;
            content.Margin = Padding.Empty;
            host.Controls.Add(content);
            tab.Controls.Add(host);
        }

        private static Panel CreateAutoPostComboCell(ComboBox combo)
        {
            combo.Dock = DockStyle.None;
            combo.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            combo.Margin = Padding.Empty;
            combo.Height = AppDefaultInputHeight;
            combo.Width = 240;
            combo.MinimumSize = new Size(120, AppDefaultInputHeight);
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            ApplyAppInputChrome(combo);

            var cell = new Panel
            {
                Name = combo.Name + "_Cell",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 10, 10, 10),
                BackColor = AutoPostPanelBack,
                Margin = Padding.Empty
            };
            cell.Controls.Add(combo);
            cell.Resize += (_, __) =>
            {
                combo.Width = Math.Max(120, cell.ClientSize.Width - cell.Padding.Horizontal);
            };
            return cell;
        }

        private static Control WrapAutoPostFieldControl(Control control)
        {
            if (control is ComboBox combo)
            {
                return CreateAutoPostComboCell(combo);
            }

            if (control is TableLayoutPanel || control is FlowLayoutPanel)
            {
                var compositeHost = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(10, 10, 10, 10),
                    BackColor = AutoPostPanelBack,
                    MinimumSize = new Size(0, 44)
                };
                control.Dock = DockStyle.Fill;
                control.Margin = Padding.Empty;
                compositeHost.Controls.Add(control);
                return compositeHost;
            }

            if (control.Dock == DockStyle.None)
            {
                control.Dock = DockStyle.Fill;
            }

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 10, 10, 10),
                BackColor = AutoPostPanelBack,
                MinimumSize = control is TextBox textBox && textBox.Multiline
                    ? new Size(0, 72)
                    : new Size(0, 40)
            };
            control.Margin = Padding.Empty;
            host.Controls.Add(control);
            return host;
        }

        private void WireAutoPostComboDropDown(ComboBox combo)
        {
            if (combo == null)
            {
                return;
            }

            void BringComboAbove(object sender, EventArgs e)
            {
                var target = sender as Control ?? combo;
                target.BringToFront();
                var parent = target.Parent;
                while (parent != null && !ReferenceEquals(parent, tabAutoPost))
                {
                    parent.BringToFront();
                    parent = parent.Parent;
                }

                tabAutoPost?.BringToFront();
            }

            combo.DropDown += BringComboAbove;
            combo.GotFocus += BringComboAbove;
            combo.MouseDown += (_, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    BringComboAbove(combo, EventArgs.Empty);
                }
            };
        }

        private void WireAutoPostComboDropDowns()
        {
            WireAutoPostComboDropDown(cbAutoPostProfile);
            WireAutoPostComboDropDown(cbAutoPostVideoType);
            WireAutoPostComboDropDown(cbAutoPostVideoFile);
            WireAutoPostComboDropDown(cbCaptionStyle);
        }

        private static void AddAutoPostTableRow(
            TableLayoutPanel tbl,
            int row,
            string caption,
            Control control,
            bool addRowStyle = true,
            bool compactField = false)
        {
            if (addRowStyle)
            {
                tbl.RowStyles.Add(compactField
                    ? new RowStyle(SizeType.Absolute, 40F)
                    : new RowStyle(SizeType.AutoSize));
            }

            var lbl = CreateAutoPostLabel(caption);
            lbl.AutoSize = false;
            lbl.Dock = DockStyle.Fill;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Margin = new Padding(0, 0, 10, 0);

            Control field;
            if (compactField && control is TextBox compactTextBox)
            {
                compactTextBox.Multiline = false;
                compactTextBox.Height = AppDefaultInputHeight;
                compactTextBox.Dock = DockStyle.Fill;
                compactTextBox.Margin = Padding.Empty;
                ApplyAppInputChrome(compactTextBox);
                field = compactTextBox;
            }
            else if (control is ComboBox comboBox)
            {
                field = CreateAutoPostComboCell(comboBox);
            }
            else
            {
                field = WrapAutoPostFieldControl(control);
            }

            field.Dock = DockStyle.Fill;
            field.Margin = Padding.Empty;
            control.MinimumSize = new Size(0, 40);
            if (field.MinimumSize.Height < 40)
            {
                field.MinimumSize = new Size(field.MinimumSize.Width, 40);
            }

            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(field, 1, row);
        }

        private static void AddAutoPostRootRow(TableLayoutPanel tbl, Control control, SizeType sizeType, float percent = 100F)
        {
            tbl.RowStyles.Add(sizeType == SizeType.Percent
                ? new RowStyle(SizeType.Percent, percent)
                : new RowStyle(SizeType.AutoSize));
            if (sizeType == SizeType.AutoSize &&
                (control.AutoSize ||
                 control is TableLayoutPanel ||
                 control is FlowLayoutPanel ||
                 control.MinimumSize.Height > 0))
            {
                control.Dock = DockStyle.Top;
            }
            else
            {
                control.Dock = DockStyle.Fill;
            }

            control.Margin = new Padding(0, 2, 0, 4);
            tbl.Controls.Add(control, 0, tbl.RowCount);
            tbl.RowCount++;
        }

        private void WireAutoPostUxEvents()
        {
            if (txtAutoPostAffiliateLink != null)
            {
                txtAutoPostAffiliateLink.TextChanged += (_, __) => RefreshAutoPostAffiliateUx();
            }

            if (txtAutoPostCaption != null)
            {
                txtAutoPostCaption.TextChanged += (_, __) => RefreshAutoPostStartButtonState();
            }

            if (chkEnableAffiliateLink != null)
            {
                chkEnableAffiliateLink.CheckedChanged += (_, __) => RefreshAutoPostAffiliateUx();
            }

            void OnChannelToggle(object s, EventArgs e) => RefreshAutoPostStartButtonState();
            if (chkAutoPostEnableTikTok != null)
            {
                chkAutoPostEnableTikTok.CheckedChanged += OnChannelToggle;
            }

            if (chkAutoPostEnableFacebook != null)
            {
                chkAutoPostEnableFacebook.CheckedChanged += OnChannelToggle;
            }

            if (chkAutoPostEnableYouTube != null)
            {
                chkAutoPostEnableYouTube.CheckedChanged += OnChannelToggle;
            }

            if (txtAutoPostFbShopeeLink != null)
            {
                txtAutoPostFbShopeeLink.TextChanged += (_, __) => RefreshAutoPostStartButtonState();
            }

            if (chkAutoPostFbAttachShopee != null)
            {
                chkAutoPostFbAttachShopee.CheckedChanged += (_, __) => RefreshAutoPostStartButtonState();
            }

            if (chkAutoPostVideoApproved != null)
            {
                chkAutoPostVideoApproved.CheckedChanged += (_, __) =>
                {
                    if (chkAutoPostVideoApproved.Checked)
                    {
                        ResetAutoPostControlHighlight(chkAutoPostVideoApproved);
                    }
                };
            }

            RefreshAutoPostAffiliateUx();
        }

        private void InitializeAutoPostTabAutoRefresh()
        {
            _autoPostFolderDebounceTimer.Interval = 400;
            _autoPostFolderDebounceTimer.Tick += (_, __) =>
            {
                _autoPostFolderDebounceTimer.Stop();
                RefreshAutoPostVideoCombo(GetSelectedAutoPostVideoFullPath());
            };

            if (txtAutoPostFolder != null)
            {
                txtAutoPostFolder.TextChanged += (_, __) =>
                {
                    _autoPostFolderDebounceTimer.Stop();
                    _autoPostFolderDebounceTimer.Start();
                };
                txtAutoPostFolder.Leave += (_, __) =>
                {
                    _autoPostFolderDebounceTimer.Stop();
                    RefreshAutoPostVideoCombo(GetSelectedAutoPostVideoFullPath());
                };
            }

            _autoPostTabRefreshTimer.Interval = 10000;
            _autoPostTabRefreshTimer.Tick += AutoPostTabRefreshTimer_Tick;
            _autoPostTabRefreshTimer.Start();
        }

        private void AutoPostTabRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (tabMain?.SelectedTab == null || !ReferenceEquals(tabMain.SelectedTab, tabAutoPost))
            {
                return;
            }

            RefreshAutoPostVideoCombo(GetSelectedAutoPostVideoFullPath());
        }

        private void RefreshAutoPostTabOnEnter()
        {
            SyncAutoPostFolderFromProfileAndType();
        }

        private void RefreshAutoPostAffiliateUx()
        {
            RefreshAutoPostAffiliateStatusLabel();
            RefreshAutoPostStartButtonState();
        }

        private void RefreshAutoPostAffiliateStatusLabel()
        {
            if (lblAffiliateStatus == null || lblAffiliateStatus.IsDisposed)
            {
                return;
            }

            var aiLink = OmnichannelAutoPostFields.NormalizeLink(_autoPostInboxJob?.AffiliateLink);
            if (!string.IsNullOrWhiteSpace(aiLink))
            {
                lblAffiliateStatus.Text = "Link affiliate từ AI: " + TruncateAutoPostDisplayLink(aiLink, 72);
                lblAffiliateStatus.ForeColor = AutoPostAffiliateStatusAi;
            }
            else
            {
                lblAffiliateStatus.Text = "Nhập link Affiliate ở trên nếu đăng thủ công";
                lblAffiliateStatus.ForeColor = AutoPostAffiliateStatusManual;
            }
        }

        private static string TruncateAutoPostDisplayLink(string link, int maxLen)
        {
            if (string.IsNullOrEmpty(link) || link.Length <= maxLen)
            {
                return link ?? string.Empty;
            }

            return link.Substring(0, maxLen - 3) + "...";
        }

        private bool HasResolvableAutoPostAffiliateLink()
        {
            var manual = OmnichannelAutoPostFields.NormalizeLink(txtAutoPostAffiliateLink?.Text);
            if (!string.IsNullOrWhiteSpace(manual))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(OmnichannelAutoPostFields.NormalizeLink(_autoPostInboxJob?.AffiliateLink));
        }

        private bool ShouldShowAutoPostAffiliateWarning()
        {
            if (chkEnableAffiliateLink == null || !chkEnableAffiliateLink.Checked)
            {
                return false;
            }

            return !HasResolvableAutoPostAffiliateLink();
        }

        private void RefreshAutoPostStartButtonState()
        {
            if (btnStartAutoPost == null || btnStartAutoPost.IsDisposed)
            {
                return;
            }

            if (ShouldShowAutoPostAffiliateWarning())
            {
                btnStartAutoPost.BackColor = AutoPostWarningOrange;
                btnStartAutoPost.Text = "CẢNH BÁO: Không có link Affiliate";
            }
            else
            {
                btnStartAutoPost.BackColor = AutoPostPrimaryGreen;
                btnStartAutoPost.Text = "BẮT ĐẦU ĐĂNG";
            }
        }

        private bool ValidateAutoPostContext(out string error)
        {
            error = string.Empty;

            var postTikTok = chkAutoPostEnableTikTok?.Checked ?? false;
            var postFacebook = chkAutoPostEnableFacebook?.Checked ?? false;
            var postYouTube = chkAutoPostEnableYouTube?.Checked ?? false;
            if (!postTikTok && !postFacebook && !postYouTube)
            {
                error = "Chưa bật kênh nào — tick «Bật đăng lên kênh này» ở TikTok, Facebook hoặc YouTube.";
                return false;
            }

            if (chkEnableAffiliateLink != null && chkEnableAffiliateLink.Checked && !HasResolvableAutoPostAffiliateLink())
            {
                error = "Affiliate Link đang bật nhưng trống!";
                return false;
            }

            if (postTikTok)
            {
                var tikTokCaption = BuildFinalAutoPostCaptionBody(
                    txtAutoPostCaption?.Text ?? string.Empty,
                    txtAutoPostHashtags?.Text ?? string.Empty);
                if (string.IsNullOrWhiteSpace(tikTokCaption))
                {
                    error = "Thiếu Caption cho TikTok!";
                    return false;
                }
            }

            if (postFacebook)
            {
                var facebookCaption = BuildFinalAutoPostCaptionBody(
                    txtAutoPostFbCaption?.Text ?? string.Empty,
                    txtAutoPostFbHashtags?.Text ?? string.Empty);
                if (string.IsNullOrWhiteSpace(facebookCaption))
                {
                    error = "Thiếu Caption cho Facebook!";
                    return false;
                }

                if (chkAutoPostFbAttachShopee?.Checked == true
                    && string.IsNullOrWhiteSpace(ResolveAutoPostFacebookShopeeLinkForPlan()))
                {
                    error = "Đã bật gắn link Shopee cho Facebook nhưng chưa có link Shopee hợp lệ (shopee.vn / shp.ee).";
                    return false;
                }
            }

            if (postYouTube && string.IsNullOrWhiteSpace(txtAutoPostYtTitle?.Text))
            {
                error = "Thiếu tiêu đề YouTube!";
                return false;
            }

            var videoFolder = (txtAutoPostFolder?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            {
                error = "Thư mục video không hợp lệ.";
                return false;
            }

            var explicitVideoPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(explicitVideoPath) &&
                !EnumerateVideoFilesInFolder(videoFolder).Any())
            {
                error = "Không có file video trong thư mục.";
                return false;
            }

            if (chkAutoPostVideoApproved != null && !chkAutoPostVideoApproved.Checked)
            {
                error = "Hãy xem video và tick «Tôi đã xem và duyệt video» trước khi đăng.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void btnValidatePost_Click(object sender, EventArgs e)
        {
            var tikPending   = _tikTokList?.Count(x => string.Equals(x?.Status, "Pending",   StringComparison.OrdinalIgnoreCase)) ?? 0;
            var tikScheduled = _tikTokList?.Count(x => string.Equals(x?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var fbPending    = _facebookList?.Count(x => string.Equals(x?.Status, "Pending",   StringComparison.OrdinalIgnoreCase)) ?? 0;
            var fbScheduled  = _facebookList?.Count(x => string.Equals(x?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var ytPending    = _youTubeList?.Count(x => string.Equals(x?.Status, "Pending",   StringComparison.OrdinalIgnoreCase)) ?? 0;
            var ytScheduled  = _youTubeList?.Count(x => string.Equals(x?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)) ?? 0;

            var total = (_tikTokList?.Count ?? 0) + (_facebookList?.Count ?? 0) + (_youTubeList?.Count ?? 0);
            if (total == 0)
            {
                MessageBox.Show(this,
                    "3 lưới đang trống — hãy duyệt thư mục và nhấn \"Thêm vào lịch đăng\" để thêm video.",
                    "Kiểm tra lịch đăng", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var msg = $"Trạng thái lịch đăng:{Environment.NewLine}" +
                      $"  TikTok  : {_tikTokList?.Count ?? 0} dòng  (Pending {tikPending} · Scheduled {tikScheduled}){Environment.NewLine}" +
                      $"  Facebook: {_facebookList?.Count ?? 0} dòng  (Pending {fbPending} · Scheduled {fbScheduled}){Environment.NewLine}" +
                      $"  YouTube : {_youTubeList?.Count ?? 0} dòng  (Pending {ytPending} · Scheduled {ytScheduled}){Environment.NewLine}" +
                      $"{Environment.NewLine}Các dòng Pending sẽ được đăng ngay khi bấm «BẮT ĐẦU ĐĂNG».{Environment.NewLine}" +
                      $"Các dòng Scheduled sẽ tự động chạy khi đến giờ hẹn (timer 15 giây).";

            MessageBox.Show(this, msg, "Kiểm tra lịch đăng", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private sealed class AutoPostValidationIssue
        {
            public string Message { get; set; }
            public Control Control { get; set; }
        }

        private List<AutoPostValidationIssue> CollectAutoPostValidationIssues()
        {
            var issues = new List<AutoPostValidationIssue>();
            var postTikTok = chkAutoPostEnableTikTok?.Checked ?? false;
            var postFacebook = chkAutoPostEnableFacebook?.Checked ?? false;
            var postYouTube = chkAutoPostEnableYouTube?.Checked ?? false;

            if (!postTikTok && !postFacebook && !postYouTube)
            {
                issues.Add(new AutoPostValidationIssue
                {
                    Message = "Chưa bật kênh TikTok, Facebook hoặc YouTube.",
                    Control = chkAutoPostEnableTikTok
                });
            }

            if (chkEnableAffiliateLink != null && chkEnableAffiliateLink.Checked && !HasResolvableAutoPostAffiliateLink())
            {
                issues.Add(new AutoPostValidationIssue
                {
                    Message = "Affiliate Link đang bật nhưng trống.",
                    Control = txtAutoPostAffiliateLink
                });
            }

            if (postTikTok)
            {
                var tikTokCaption = BuildFinalAutoPostCaptionBody(
                    txtAutoPostCaption?.Text ?? string.Empty,
                    txtAutoPostHashtags?.Text ?? string.Empty);
                if (string.IsNullOrWhiteSpace(tikTokCaption))
                {
                    issues.Add(new AutoPostValidationIssue
                    {
                        Message = "Thiếu Caption cho TikTok.",
                        Control = txtAutoPostCaption
                    });
                }
            }

            if (postFacebook)
            {
                var facebookCaption = BuildFinalAutoPostCaptionBody(
                    txtAutoPostFbCaption?.Text ?? string.Empty,
                    txtAutoPostFbHashtags?.Text ?? string.Empty);
                if (string.IsNullOrWhiteSpace(facebookCaption))
                {
                    issues.Add(new AutoPostValidationIssue
                    {
                        Message = "Thiếu Caption cho Facebook.",
                        Control = txtAutoPostFbCaption
                    });
                }

                if (chkAutoPostFbAttachShopee?.Checked == true
                    && string.IsNullOrWhiteSpace(ResolveAutoPostFacebookShopeeLinkForPlan()))
                {
                    issues.Add(new AutoPostValidationIssue
                    {
                        Message = "Bật gắn Shopee nhưng link Shopee trống hoặc không hợp lệ.",
                        Control = txtAutoPostFbShopeeLink
                    });
                }
            }

            if (postYouTube && string.IsNullOrWhiteSpace(txtAutoPostYtTitle?.Text))
            {
                issues.Add(new AutoPostValidationIssue
                {
                    Message = "Thiếu tiêu đề YouTube.",
                    Control = txtAutoPostYtTitle
                });
            }

            var videoFolder = (txtAutoPostFolder?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            {
                issues.Add(new AutoPostValidationIssue
                {
                    Message = "Thư mục video không hợp lệ.",
                    Control = txtAutoPostFolder
                });
            }
            else
            {
                var explicitVideoPath = GetSelectedAutoPostVideoFullPath();
                if (string.IsNullOrWhiteSpace(explicitVideoPath) &&
                    !EnumerateVideoFilesInFolder(videoFolder).Any())
                {
                    issues.Add(new AutoPostValidationIssue
                    {
                        Message = "Chưa chọn hoặc không có file video.",
                        Control = cbAutoPostVideoFile
                    });
                }
            }

            if (chkAutoPostVideoApproved != null && !chkAutoPostVideoApproved.Checked)
            {
                issues.Add(new AutoPostValidationIssue
                {
                    Message = "Chưa tick duyệt video trước khi đăng.",
                    Control = chkAutoPostVideoApproved
                });
            }

            return issues;
        }

        private void ApplyAutoPostValidationHighlights(IEnumerable<AutoPostValidationIssue> issues)
        {
            foreach (var issue in issues)
            {
                HighlightAutoPostControl(issue.Control);
            }
        }

        private void ClearAutoPostValidationHighlights()
        {
            ResetAutoPostControlHighlight(txtAutoPostAffiliateLink);
            ResetAutoPostControlHighlight(txtAutoPostCaption);
            ResetAutoPostControlHighlight(txtAutoPostFbCaption);
            ResetAutoPostControlHighlight(txtAutoPostFbShopeeLink);
            ResetAutoPostControlHighlight(txtAutoPostYtTitle);
            ResetAutoPostControlHighlight(txtAutoPostFolder);
            ResetAutoPostControlHighlight(cbAutoPostVideoFile);
            ResetAutoPostControlHighlight(chkAutoPostVideoApproved);
        }

        private static void HighlightAutoPostControl(Control control)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            if (control is TextBox textBox)
            {
                textBox.BackColor = AutoPostValidationErrorBack;
                return;
            }

            if (control is ComboBox comboBox)
            {
                comboBox.BackColor = AutoPostValidationErrorBack;
                return;
            }

            if (control is CheckBox checkBox)
            {
                checkBox.ForeColor = Color.FromArgb(255, 140, 140);
            }
        }

        private static void ResetAutoPostControlHighlight(Control control)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            if (control is TextBox textBox)
            {
                textBox.BackColor = AutoPostFieldBack;
                return;
            }

            if (control is ComboBox comboBox)
            {
                comboBox.BackColor = AutoPostFieldBack;
                return;
            }

            if (control is CheckBox checkBox)
            {
                checkBox.ForeColor = Color.Gainsboro;
            }
        }

        private async void btnFetchFbShopeeInfo_Click(object sender, EventArgs e)
        {
            var url = OmnichannelAutoPostFields.NormalizeLink(txtAutoPostFbShopeeLink?.Text);
            if (!OmnichannelAutoPostFields.IsShopeeProductUrl(url))
            {
                MessageBox.Show(
                    this,
                    "Nhập link Shopee hợp lệ (ví dụ shopee.vn hoặc shp.ee).",
                    "Link Shopee",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (btnFetchFbShopeeInfo != null)
            {
                btnFetchFbShopeeInfo.Enabled = false;
            }

            try
            {
                await WithBrowserLockAsync(
                    GetRunningProfileName(),
                    async cancellationToken =>
                    {
                        var fetch = await _affiliateHunter
                            .FetchProductInfoFromUrlAsync(url, Log, cancellationToken)
                            .ConfigureAwait(true);
                        if (fetch == null || string.IsNullOrWhiteSpace(fetch.ProductName))
                        {
                            if (lblFbShopeeStatus != null)
                            {
                                lblFbShopeeStatus.Text = "Không lấy được thông tin sản phẩm từ link.";
                                lblFbShopeeStatus.ForeColor = AutoPostWarningOrange;
                            }

                            return;
                        }

                        var canonicalLink = OmnichannelAutoPostFields.NormalizeLink(
                            string.IsNullOrWhiteSpace(fetch.AffiliateLink) ? url : fetch.AffiliateLink);
                        if (txtAutoPostFbShopeeLink != null)
                        {
                            txtAutoPostFbShopeeLink.Text = canonicalLink;
                        }

                        if (lblFbShopeeStatus != null)
                        {
                            var pricePart = string.IsNullOrWhiteSpace(fetch.Price) ? string.Empty : " — " + fetch.Price;
                            lblFbShopeeStatus.Text = "OK: " + fetch.ProductName + pricePart;
                            lblFbShopeeStatus.ForeColor = AutoPostAffiliateStatusAi;
                        }

                        Log("[Facebook Shopee] Đã xác minh: " + fetch.ProductName);
                    },
                    CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("[Facebook Shopee] Lỗi kiểm tra link: " + ex.Message);
                if (lblFbShopeeStatus != null)
                {
                    lblFbShopeeStatus.Text = "Lỗi: " + ex.Message;
                    lblFbShopeeStatus.ForeColor = AutoPostWarningOrange;
                }
            }
            finally
            {
                if (btnFetchFbShopeeInfo != null)
                {
                    btnFetchFbShopeeInfo.Enabled = true;
                }
            }
        }
    }
}
