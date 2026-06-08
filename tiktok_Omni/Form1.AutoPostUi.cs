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

        private static readonly Font AutoPostJellyButtonFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        private const int AutoPostActionButtonHeight = 46;
        private static readonly Color AutoPostTintStart = Color.FromArgb(56, 158, 88);
        private static readonly Color AutoPostTintValidate = Color.FromArgb(168, 128, 52);
        private static readonly Color AutoPostTintCloseBrowser = Color.FromArgb(195, 72, 72);
        private static readonly Color AutoPostTintOpenApproval = Color.FromArgb(88, 118, 178);

        private Label lblAffiliateStatus;
        private Button btnValidatePost;
        private readonly System.Windows.Forms.Timer _autoPostTabRefreshTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _autoPostFolderDebounceTimer = new System.Windows.Forms.Timer();

        private void BuildAutoPostUi()
        {
            tabAutoPost.SuspendLayout();
            var darkTab = Color.FromArgb(31, 34, 42);

            BuildAutoPostSharedControls();
            pnlAutoPostShared = BuildAutoPostSharedPanel();

            tabTikTokPost = new TabPage("TikTok") { Name = "tabTikTokPost" };
            tabFacebookPost = new TabPage("Facebook") { Name = "tabFacebookPost" };
            tabYouTubePost = new TabPage("YouTube") { Name = "tabYouTubePost" };
            ConfigureAutoPostPlatformTab(tabTikTokPost, AutoPostTikTokSurface);
            ConfigureAutoPostPlatformTab(tabFacebookPost, AutoPostFacebookSurface);
            ConfigureAutoPostPlatformTab(tabYouTubePost, AutoPostYouTubeSurface);

            tabMultiPlatformPost = new TabControl
            {
                Name = "tabMultiPlatformPost",
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(100, 28),
                Padding = new Point(8, 4),
                BackColor = darkTab,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(10, 4, 10, 4)
            };
            tabMultiPlatformPost.TabPages.Add(tabTikTokPost);
            tabMultiPlatformPost.TabPages.Add(tabFacebookPost);
            tabMultiPlatformPost.TabPages.Add(tabYouTubePost);
            WireAutoPostPlatformTabDraw();

            MountAutoPostPlatformTab(tabTikTokPost, BuildAutoPostTikTokPanel(), scrollContent: true);
            MountAutoPostPlatformTab(tabFacebookPost, BuildAutoPostFacebookPanel(), scrollContent: true);
            MountAutoPostPlatformTab(tabYouTubePost, BuildAutoPostYouTubePanel());

            pnlAutoPostActions = BuildAutoPostActionsPanel();

            var pnlAutoPostBanner = CreateBannerHostPanel();
            CreateStepBanner(pnlAutoPostBanner, "① Chọn video  →  ② Soạn caption (TikTok/FB/YT)  →  ③ Thêm lịch  →  ④ Đăng");

            var pnlAutoPostSchedule = BuildAutoPostSchedulePanel();

            var tblAutoPostMain = new TableLayoutPanel
            {
                Name = "tblAutoPostMain",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = darkTab,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblAutoPostMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            tblAutoPostMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            pnlAutoPostBanner.Dock = DockStyle.Fill;
            pnlAutoPostShared.Dock = DockStyle.Fill;
            pnlAutoPostSchedule.Dock = DockStyle.Fill;
            tabMultiPlatformPost.Dock = DockStyle.Fill;
            pnlAutoPostActions.Dock = DockStyle.Fill;

            tblAutoPostMain.Controls.Add(pnlAutoPostBanner, 0, 0);
            tblAutoPostMain.Controls.Add(pnlAutoPostShared, 0, 1);
            tblAutoPostMain.Controls.Add(tabMultiPlatformPost, 0, 2);
            tblAutoPostMain.Controls.Add(pnlAutoPostSchedule, 0, 3);
            tblAutoPostMain.Controls.Add(pnlAutoPostActions, 0, 4);

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
            btnBrowseAutoPostFolder.AutoSize = true;
            btnBrowseAutoPostFolder.Padding = new Padding(8, 4, 8, 4);

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
                "Triết lý",
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
            var tbl = new TableLayoutPanel
            {
                Name = "pnlAutoPostShared",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                BackColor = AutoPostPanelBack,
                Padding = new Padding(8, 8, 8, 4),
                Margin = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 7; i++)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            var pnlFolder = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                BackColor = AutoPostPanelBack,
                Margin = Padding.Empty
            };
            pnlFolder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlFolder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlFolder.Controls.Add(btnBrowseAutoPostFolder, 0, 0);
            pnlFolder.Controls.Add(txtAutoPostFolder, 1, 0);

            AddAutoPostTableRow(tbl, 0, "Thư mục video", pnlFolder, addRowStyle: false);
            AddAutoPostTableRow(tbl, 1, "Profile", cbAutoPostProfile, addRowStyle: false);
            AddAutoPostTableRow(tbl, 2, "Loại video", cbAutoPostVideoType, addRowStyle: false);
            AddAutoPostTableRow(tbl, 3, "Video file", cbAutoPostVideoFile, addRowStyle: false);
            AddAutoPostTableRow(tbl, 4, "Hashtag chung", txtAutoPostHashtags, addRowStyle: false);
            AddAutoPostTableRow(tbl, 5, "Caption Style", cbCaptionStyle, addRowStyle: false);

            chkEnableAffiliateLink.Margin = new Padding(10, 8, 10, 6);
            tbl.Controls.Add(chkEnableAffiliateLink, 0, 6);
            tbl.SetColumnSpan(chkEnableAffiliateLink, 2);

            return tbl;
        }

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
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
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

            var flpSafety = new FlowLayoutPanel
            {
                Name = "flpAutoPostSafety",
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 10, 8, 8),
                BackColor = AutoPostPanelBack
            };
            flpSafety.Controls.Add(new Label
            {
                Text = "Điểm an toàn tối thiểu",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 6, 6, 0)
            });
            numBlockPostingSafetyScoreBelow = new NumericUpDown
            {
                Name = "numBlockPostingSafetyScoreBelow",
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                Width = 56,
                BackColor = AutoPostFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 4, 0, 0)
            };
            flpSafety.Controls.Add(numBlockPostingSafetyScoreBelow);

            flp.Controls.Add(btnStartAutoPost);
            flp.Controls.Add(btnValidatePost);
            flp.Controls.Add(btnCloseAutoPostBrowser);
            flp.Controls.Add(btnAutoPostOpenApproval);
            pnl.Controls.Add(flp);
            pnl.Controls.Add(flpSafety);
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
            const int horizontalPad = 24;
            var width = MeasureAutoPostJellyButtonTextWidth(text) + horizontalPad;

            var btn = new JellyButton
            {
                Name = name,
                Text = text,
                Font = AutoPostJellyButtonFont,
                JellyTint = tint,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Width = width,
                Height = AutoPostActionButtonHeight,
                MinimumSize = new Size(width, AutoPostActionButtonHeight),
                MaximumSize = new Size(width, AutoPostActionButtonHeight),
                Margin = new Padding(8, 4, 0, 4)
            };

            if (click != null)
            {
                btn.Click += click;
            }

            return btn;
        }

        private static int MeasureAutoPostJellyButtonTextWidth(string text)
        {
            return TextRenderer.MeasureText(
                text,
                AutoPostJellyButtonFont,
                new Size(int.MaxValue, AutoPostActionButtonHeight),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width;
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

            using (var backBrush = new SolidBrush(selected ? surface : Color.FromArgb(36, 38, 48)))
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

            var textColor = selected ? accent : Color.FromArgb(175, 178, 188);
            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                tabMultiPlatformPost.Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

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
            combo.Height = 28;
            combo.Width = 240;
            combo.MinimumSize = new Size(120, 28);
            combo.DropDownStyle = ComboBoxStyle.DropDownList;

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
                    ? new RowStyle(SizeType.Absolute, 36F)
                    : new RowStyle(SizeType.AutoSize));
            }

            var lbl = CreateAutoPostLabel(caption);
            lbl.AutoSize = true;
            lbl.Dock = DockStyle.None;
            lbl.Anchor = compactField
                ? AnchorStyles.Left
                : AnchorStyles.Left | AnchorStyles.Top;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Margin = compactField
                ? new Padding(0, 0, 8, 0)
                : new Padding(0, 10, 8, 10);
            if (compactField)
            {
                lbl.Dock = DockStyle.Fill;
                lbl.TextAlign = ContentAlignment.MiddleLeft;
            }

            Control field;
            if (compactField && control is TextBox compactTextBox)
            {
                compactTextBox.Multiline = false;
                compactTextBox.Height = 28;
                compactTextBox.Dock = DockStyle.Fill;
                compactTextBox.Margin = Padding.Empty;
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

            if (field is Panel fieldPanel && !(control is ComboBox) && !compactField)
            {
                fieldPanel.Dock = DockStyle.Fill;
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
            ClearAutoPostValidationHighlights();
            var issues = CollectAutoPostValidationIssues();
            if (issues.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Đủ điều kiện: caption, link affiliate (nếu bật), video và duyệt.",
                    "Kiểm tra trước khi đăng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            ApplyAutoPostValidationHighlights(issues);
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, issues.Select(i => "• " + i.Message)),
                "Kiểm tra trước khi đăng",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
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
