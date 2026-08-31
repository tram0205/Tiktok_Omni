using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Helpers;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Affiliate;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1 : Form
    {
        private readonly ConfigManager _configManager;
        private readonly TikTokAutomation _tikTokAutomation;
        private readonly SocialAutomation _socialAutomation;
        private NumericUpDown numMaxConcurrentJobs;
        private string _storageRootPathCache = string.Empty;
        private readonly AffiliateHunter _affiliateHunter;
        private readonly NotificationService _notificationService;
        private readonly AffiliateRevenueService _affiliateRevenueService;

        private Panel pnlMainWorkspace;
        private TabControl tabMain;
        private TabPage tabAffiliateHunter;
        private TabPage tabAiVideoGen;
        private TabPage tabAutoPost;
        private TabPage tabSetting;

        private StatusStrip statusStripMain;
        private ToolStripStatusLabel tslStatusMain;

        private ComboBox cbRunningProfile;
        private Button btnOpenTikTokManualBrowser;
        private Button btnNavApprovalQueue;
        private Label lblHealthReadiness;
        private Button btnHealthRecheck;
        private Panel pnlHealthReadiness;
        private Button btnHealthOpenSettings;
        private Button btnOpenLogsFolder;
        private Button btnExportCurrentLogs;
        private Button btnClearLogs;
        private RichTextBox rtbLogs;
        private TextBox txtAutoPostFolder;
        private Button btnBrowseAutoPostFolder;
        private ComboBox cbAutoPostProfile;
        private ComboBox cbAutoPostVideoType;
        private ComboBox cbAutoPostVideoFile;
        private Button btnLoginAllSocial;
        private Button btnRefreshProfileLoginStatus;
        private Button btnLoginFacebook;
        private Button btnLoginYouTube;
        private TextBox txtAutoPostHashtags;
        private ComboBox cbCaptionStyle;
        private Button btnGenerateGeminiCaption;
        private Button btnPreviewAutoPostVideo;
        private TextBox txtAutoPostCaption;
        private TextBox txtAutoPostAffiliateLink;
        private CheckBox chkAutoPostVideoApproved;
        private CheckBox chkAutoPostUploadOnly;
        private Button btnStartAutoPost;
        private Button btnCloseAutoPostBrowser;
        private Panel pnlAutoPostShared;
        private TabControl tabMultiPlatformPost;
        private TabPage tabTikTokPost;
        private TabPage tabFacebookPost;
        private TabPage tabYouTubePost;
        private Panel pnlAutoPostActions;
        private DateTimePicker dtpAutoPostTikTok;
        private CheckBox chkAutoPostEnableTikTok;
        private TextBox txtAutoPostFbCaption;
        private TextBox txtAutoPostFbHashtags;
        private TextBox txtAutoPostFbShopeeLink;
        private CheckBox chkAutoPostFbAttachShopee;
        private Button btnFetchFbShopeeInfo;
        private Label lblFbShopeeStatus;
        private Button btnGenerateGeminiCaptionFb;
        private DateTimePicker dtpAutoPostFacebook;
        private CheckBox chkAutoPostEnableFacebook;
        private TextBox txtAutoPostYtTitle;
        private TextBox txtAutoPostYtDescription;
        private Button btnGenerateGeminiCaptionYoutube;
        private DateTimePicker dtpAutoPostYouTube;
        private CheckBox chkAutoPostEnableYouTube;

        private static readonly string[] AutoPostCaptionStyleKeys =
        {
            "knowledge",
            "philosophy",
            "humor",
            "debate"
        };

        private const string AffiliateKeywordsPlaceholder =
            "Nhập từ khoá (nhiều từ khoá cách nhau bởi dấu phẩy)";

        /// <summary>Giới hạn app săn tối đa mỗi keyword khi bật buffer xếp hạng (tránh quá tải TikWM).</summary>
        private const int AffiliateHuntBufferHardCap = 120;
        /// <summary>Giới hạn ký tự hiển thị trong lưới (đủ rộng để đọc nhanh, tránh kéo cao hàng).</summary>
        private const int AffiliateGridCaptionPreviewChars = 120;
        private const int AffiliateGridKeywordPreviewChars = 96;
        private const int AffiliateGridHashtagPreviewChars = 88;
        private const int AffiliateGridLinkedProductPreviewChars = 72;
        private const int AffiliateGridVoiceoverPreviewChars = 100;
        private TextBox txtAiProvider;
        private TextBox txtAiModel;
        private TextBox txtAiApiKey;
        private TextBox txtTwoCaptchaApiKey;
        private TextBox txtTikTokRapidApiKey;
        private TextBox txtVeoApiKey;
        private TextBox txtTtsApiKey;
        private TextBox txtVeoEndpoint;
        private TextBox txtFfmpegPath;
        private TextBox txtYtDlpPath;
        private Button btnToggleAiApiKey;
        private Button btnToggleTwoCaptchaApiKey;
        private Button btnToggleTikTokRapidApiKey;
        private Button btnToggleVeoApiKey;
        private Button btnToggleTtsApiKey;
        private Button btnTestAi;
        private Button btnTestTwoCaptcha;
        private Button btnTestTikTokRapidApi;
        private Button btnTestVeo;
        private Button btnTestTts;
        private Button btnBrowseFfmpegPath;
        private Button btnDownloadFfmpeg;
        private Button btnBrowseYtDlpPath;
        private Control pnlReupMusicLibraryPath;
        private Button btnBrowseVideoReupMusicLibrary;
        private Button btnDownloadYtDlp;
        private TextBox txtVideoReupMusicLibraryPath;
        private Button btnSaveSettings;
        private CheckBox chkAutoFetchAffiliateRevenueOnStartup;
        private CheckBox chkAlwaysRequirePrePostApproval;
        private CheckBox chkAlwaysRequirePreRenderApproval;
        private CheckBox chkAutoRunApprovedQueue;
        private Label lblSettingsValidation;
        private DataGridView dgvProxyProfiles;
        private BindingList<AutomationProfile> _proxyProfileBindingList;
        private Button btnCheckBrowserProfileHealth;
        private CheckBox chkAiVideoGenCurrentProfileOnly;
        private List<AiVideoGenInputItem> _slideshowBuffer = new List<AiVideoGenInputItem>();
        private List<AiVideoGenInputItem> _deepDiveBuffer = new List<AiVideoGenInputItem>();
        private DataGridView dgvAiVideoGenInput;
        private DataGridView dgvDeepDiveInput;
        private Panel pnlAiVideoGenScriptHost;
        private Label lblAiVideoGenProductsTitle;
        private Panel pnlManualInput;
        private TextBox txtManualProductUrl;
        private Button btnAddManualProduct;
        private CheckBox chkEnableAffiliateLink;
        private Panel tabMascotStory;
        private TextBox txtAiVideoGenPrompt;
        private TextBox txtMascotImagePath;
        private TextBox txtMascotChannelTheme;
        private List<string> _mascotPreviewSceneScripts = new List<string>();
        private List<string> _mascotPreviewImagePaths = new List<string>();
        private ComboBox cbMascotProfile;
        private GroupBox grpAiRenderProgress;
        private TableLayoutPanel tblAiRenderSlots;
        private Label lblPhilosophyPrereq;
        private Label lblPhilosophyProgress;
        private ProgressBar pbPhilosophyProgress;
        private RichTextBox rtbPhilosophyLog;
        private Button btnPhilosophyClearLog;
        private RichTextBox rtbShowcaseLog;
        private Button btnShowcaseClearLog;
        private Panel pnlShowcaseLogHost;
        private DataGridView dgvAiVideoScriptReview;
        private Panel pnlAiVideoGenStickyHost;
        private Panel pnlAiVideoGenProductArea;
        private Panel pnlAffiliateDeepStoryboardHost;
        private TableLayoutPanel tblAffiliateDeepRoot;
        private Panel pnlAffiliateDeepRoot;
        private Panel flpAffiliateDeepHeaderActions;
        private Panel flpAffiliateDeepExecuteActions;
        private Panel pnlAffiliateDeepExecuteHost;
        private Panel pnlAffiliateDeepHeaderHost;
        private Panel pnlAffiliateDeepReadinessHost;
        private Panel pnlShowcaseTabTitleHost;
        private ShowcaseTabTitleLabel lblShowcaseTabTitle;
        private Panel pnlAffiliateDeepProductHost;
        private Panel pnlAffiliateDeepProductGridHost;
        private Panel pnlAffiliateDeepStoryboardSlot;
        private bool _affiliateDeepRootWired;
        private TableLayoutPanel tblAiVideoGenRoot;
        private Panel pnlAiVideoGenRenderStatusHost;
        private FlowLayoutPanel flpAiVideoGenHeader;
        private Panel pnlAiVideoGenActionBar;
        private TableLayoutPanel tblAiVideoGenActionInner;
        private TableLayoutPanel tblAiVideoGenScriptInner;
        private Panel pnlSlideshowActionBar;
        private Panel pnlAiVideoGenReadinessHost;
        private FlowLayoutPanel flpSlideshowData;
        private FlowLayoutPanel flpSlideshowExecute;
        private bool _aiVideoGenLayoutGuard;
        private ProgressBar pbAiRenderSlot1;
        private ProgressBar pbAiRenderSlot2;
        private ProgressBar pbAiRenderSlot3;
        private Label lblAiRenderSlot1;
        private Label lblAiRenderSlot2;
        private Label lblAiRenderSlot3;
        private CancellationTokenSource _autoPostCancellation;
        private const int AiVideoGenRenderStatusHeight = 132;
        private readonly object _sessionLogSync = new object();
        private readonly string _sessionLogFilePath;

        private TabControl tabCtrlHunter;
        private TabPage tabHuntVideo;
        private TabPage tabHuntProduct;

        private TextBox txtHuntProductKeyword;
        private CheckBox chkHuntProductTikTok;
        private CheckBox chkHuntProductShopee;
        private NumericUpDown numHuntProductMaxResults;
        private NumericUpDown numHuntProductMinSales;
        private NumericUpDown numHuntProductMinRating;
        private Button btnHuntProductAutoScan;
        private Label lblHuntProductStatus;
        private ProgressBar pbHuntProductScan;
        private LinkLabel lnkHuntProductDownloadFolder;
        private CancellationTokenSource _huntProductCancellation;
        private string _lastHuntProductKeyword = string.Empty;
        private TextBox txtHuntProductManualLink;
        private ComboBox cbHuntProductProfile;
        private Button btnHuntProductAddManual;
        private DataGridView dgvHuntProduct;
        private BindingList<HuntProductCandidate> _huntProductBindingList;
        private Button btnHuntProductDownloadMedia;
        private Button btnHuntProductPushDeep;
        private Button btnHuntProductDeleteRow;
        private Button btnHuntProductExportCsv;

        private TextBox txtAffiliateKeywords;
        private ComboBox cbAffiliateHuntProfile;
        private NumericUpDown numAffiliateMaxResults;
        private Button btnHuntAffiliates;
        private Button btnStopHunt;
        private Button btnExportAffiliateCsv;
        private Button btnPushToAiVideoGen;
        private Button btnDownloadSelectedAffiliate;
        private Button btnAffiliateDeepDive;
        private CheckBox chkAffiliateAutoEnrich;
        private Label lblAffiliateEnrichStatus;
        private LinkLabel lnkAffiliateDownloadFolder;
        private bool _affiliateAutoEnrichRunning;
        private CancellationTokenSource _affiliateAutoEnrichCts;
        private System.Windows.Forms.Timer _affiliateAutoEnrichSaveTimer;
        private System.Windows.Forms.Timer _affiliateHuntPrefsSaveTimer;
        private bool _affiliateRowEnrichRunning;
        private CancellationTokenSource _affiliateRowEnrichCts;
        private CheckBox chkAffiliatePlatformTikTok;
        private CheckBox chkAffiliatePlatformFacebook;
        private CheckBox chkAffiliatePlatformYouTube;
        private CheckBox chkAffiliateOnlyHighQuality;
        private CheckBox chkAffiliateRankByEngagement;
        private NumericUpDown numAffiliateBufferMultiplier;
        private ComboBox cbTikTokHuntMethod;
        private CheckBox chkTikTokApiFallbackBrowser;
        private DataGridView dgvAffiliateResults;
        private BindingList<AffiliateCandidate> _affiliateBindingList;
        private List<AffiliateCandidate> _affiliateAllResults = new List<AffiliateCandidate>();
        private List<HuntKeywordEntry> _lastHuntKeywordEntries = new List<HuntKeywordEntry>();

        /// <summary>Lọc lưới affiliate theo profile đang chọn (cbRunningProfile).</summary>
        private string _affiliateGridProfileScope = string.Empty;
        private string _lastHuntKeyword = string.Empty;
        private List<string> _lastHuntKeywords = new List<string>();
        private bool _affiliateKeywordsPlaceholderActive;
        private bool _affiliateDeepDiveErrorColumnVisible;
        private bool _affiliateDownloadingBatch;
        private CancellationTokenSource _affiliateDownloadBatchCts;
        private DataGridView dgvVideoReupInput;
        private BindingList<VideoReupRowItem> _videoReupBindingList;
        private Button btnPushSelectionToVideoReup;
        private Button btnVideoReupHookGemini;
        private Button btnVideoReupLyriaHook;
        private bool _videoReupPipelineReady;
        private bool _videoReupGeminiReady;
        private AppSettings _videoReupSettingsSnap;
        private AppSettings _philosophySettingsSnap;
        private Label lblVideoReupMusicPick;
        private ComboBox cbVideoReupMusic;
        private Button btnVideoReupOpenMusicFolder;
        private Button btnVideoReupOpenHookSfxFolder;
        private Button btnVideoReupOpenLogoLibrary;
        private Label lblVideoReupMusicPathHint;
        private ToolTip _tipVideoReupMusicPath;
        private DateTime _lastVideoReupMusicAutoRefreshUtc = DateTime.MinValue;
        private TextBox txtVideoReupVideoUrl;
        private Label lblVideoReupVideoUrl;
        private Label lblVideoReupHook;
        private Label lblVideoReupHint;
        private Label lblVideoReupReadiness;
        private Label lblSlideshowReadiness;
        private Label lblAffiliateDeepReadiness;
        private Panel pnlVideoReupStatus;
        private Panel pnlReupEditor;
        private Label lblVideoReupProgress;
        private RichTextBox rtbVideoReupLog;
        private Button btnVideoReupClearLog;
        private Button btnVideoReupAddManualRow;
        private GroupBox grpVideoReupAudioMode;
        private RadioButton rbVideoReupAudioAffiliate;
        private RadioButton rbVideoReupAudioFilm;
        private bool _videoReupSuppressAudioModeEvents;
        private bool _videoReupSuppressUrlEditorEvents;
        private string _videoReupVideoUrlBeforeCellEdit = string.Empty;
        private static readonly SemaphoreSlim _browserSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _videoReupDownloadGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _revenueCancellation;
        private CancellationTokenSource _activeJobCancellation;
        private readonly RenderHistoryStore _renderHistoryStore = new RenderHistoryStore();
        private readonly HuntHistoryStore _huntHistoryStore = new HuntHistoryStore();
        private readonly AffiliateCategoryService _affiliateCategoryService = new AffiliateCategoryService();
        private readonly AffiliateScriptPreviewService _affiliateScriptPreviewService = new AffiliateScriptPreviewService();
        private readonly Dictionary<string, Guid> _affiliateDeepDiveJobByVideoUrl =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _affiliateCategorizeCts;
        private readonly VideoReupDraftStore _videoReupDraftStore = new VideoReupDraftStore();
        private readonly AffiliateDraftStore _affiliateDraftStore = new AffiliateDraftStore();
        private readonly HuntProductDraftStore _huntProductDraftStore = new HuntProductDraftStore();
        private System.Windows.Forms.Timer _videoReupDraftTimer;
        private bool _videoReupDraftDirty;
        private Dictionary<string, bool> _systemHealth = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private SystemHealthCheckService _systemHealthCheckService;

        private BindingList<AiVideoScriptReviewItem> _aiVideoScriptBindingList;
        private readonly VideoProcessingService _videoProcessingService;
        private CancellationTokenSource _huntCancellation;
        private CancellationTokenSource _aiVideoGenCancellation;
        private readonly ApprovalQueueManager _approvalQueueManager;
        private readonly SafetyScoreService _safetyScoreService;

        private readonly DuplicateGuardManager _duplicateGuardManager;
        private readonly PhilosophyVideoService _philosophyVideoService;
        private readonly GeminiService _geminiService;
        private readonly VideoReupRemixService _videoReupRemixService;
        private readonly RevenueDrivenService _revenueDrivenService = new RevenueDrivenService();
        private readonly AsyncTasksRebootStore _asyncTasksRebootStore = new AsyncTasksRebootStore();
        private List<ApprovalQueueItem> _approvalQueueItems;

        public Form1()
        {
            InitializeTheme();
            _configManager = new ConfigManager();
            InitializeComponent();
            EnableUiFlickerReduction();
            ApplyGlobalButtonThemes();
            HighlightSidebarForSelectedTab();

            _warmupStateManager = new WarmupStateManager();
            _warmupQueueScheduler = new WarmupQueueScheduler();
            _warmupQueueStateManager = new WarmupQueueStateManager();
            _warmupQueueHistoryManager = new WarmupQueueHistoryManager();
            _approvalQueueManager = new ApprovalQueueManager();
            _safetyScoreService = new SafetyScoreService();
            _duplicateGuardManager = new DuplicateGuardManager();
            _philosophyVideoService = new PhilosophyVideoService();
            _warmupQueueHistory = new List<WarmupQueueHistoryRecord>();
            _approvalQueueItems = new List<ApprovalQueueItem>();
            _geminiService = new GeminiService();
            _systemHealthCheckService = new SystemHealthCheckService(_configManager, _geminiService);
            _renderHistoryStore.Load();
            _huntHistoryStore.Load();
            _videoReupRemixService = new VideoReupRemixService();
            _tikTokAutomation = new TikTokAutomation(_geminiService, _configManager, new CaptchaService());
            _socialAutomation = new SocialAutomation(_tikTokAutomation, _configManager);
            _tikTokAutomation.CaptchaDetected += TikTokAutomation_CaptchaDetected;
            _tikTokAutomation.CheckpointDetected += TikTokAutomation_CheckpointDetected;
            _affiliateHunter = new AffiliateHunter();
            _notificationService = new NotificationService();
            _affiliateRevenueService = new AffiliateRevenueService(_configManager, _socialAutomation, _notificationService);
            _videoProcessingService = new VideoProcessingService(_asyncTasksRebootStore);
            _sessionLogFilePath = InitializeSessionLogFile();
            _warmupScheduleTimer = new System.Windows.Forms.Timer { Interval = 15000 };
            _warmupScheduleTimer.Tick += WarmupScheduleTimer_Tick;
            _warmupScheduleTimer.Start();
            InitializeAutoPostScheduleQueue();
            Shown += Form1_Shown;
            Load += Form1_LoadSyncChrome;
            Activated += Form1_Activated;
            FormClosing += Form1_FormClosing;
        }

        private void Form1_LoadSyncChrome(object sender, EventArgs e)
        {
            if (tabMain != null && tabMain.TabPages.Count > 0 && tabMain.SelectedIndex < 0)
            {
                tabMain.SelectedIndex = 0;
            }

            BeginUiLayoutBatch();
            try
            {
                ApplyAppTypographyRecursive(this);
                HighlightSidebarForSelectedTab();
                ApplyGlobalLogChrome();
            }
            finally
            {
                EndUiLayoutBatch();
            }

            Services.WarmupBuildInfo.WriteStartupFingerprint(Log);
            Text = "tiktok_Omni — " + Services.WarmupBuildInfo.BuildId;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _applicationClosing = true;
            CloseAllAuxiliaryFormsOnExit();
            ShutdownAllAutomationWork();
            CancelAllApplicationWorkForEmergencyStop();

            try
            {
                FlushVideoReupDraftToDisk();
                FlushSlideshowDraftToDisk();
                FlushShowcaseDraftToDisk();
                FlushPhilosophyDraftToDisk();
                FlushProductAdImageDraftToDisk();
                FlushAffiliateDraftToDisk();
                FlushHuntProductDraftToDisk();
                FlushAffiliateHuntResultsOnExit();
                SaveSchedule();
                PersistWarmupQueueOnAppExit();
                if (!_consoleFastExit)
                {
                    SaveUiNavigationStateSync();
                }
            }
            catch
            {
                // ignored on exit
            }

            _slideshowDraftTimer?.Stop();
            _slideshowDraftTimer?.Dispose();
            _showcaseDraftTimer?.Stop();
            _showcaseDraftTimer?.Dispose();
            _philosophyDraftTimer?.Stop();
            _philosophyDraftTimer?.Dispose();
            _videoReupDraftTimer?.Stop();
            _videoReupDraftTimer?.Dispose();
            _jobWorkerService?.Dispose();
            _globalJobQueue?.Dispose();
        }

        private bool ShouldAllowInteractivePrompts()
        {
            return !_applicationClosing && !IsDisposed && !Disposing;
        }

        private void CloseAllAuxiliaryFormsOnExit()
        {
            Form[] openForms;
            try
            {
                openForms = Application.OpenForms.Cast<Form>().ToArray();
            }
            catch
            {
                return;
            }

            foreach (var form in openForms)
            {
                if (form == null || ReferenceEquals(form, this) || form.IsDisposed)
                {
                    continue;
                }

                try
                {
                    form.Close();
                }
                catch
                {
                    // ignored on exit
                }
            }
        }

        private void InitializeTheme()
        {
            Text = "tiktok_Omni";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 700);
            BackColor = Color.FromArgb(32, 34, 44);
            ForeColor = Color.Gainsboro;
            Font = AppInputFont;
            WindowState = FormWindowState.Maximized;
        }

        private void InitializeComponent()
        {
            MinimumSize = new Size(1024, 680);

            tabMain = new TabControl
            {
                Dock = DockStyle.Fill
            };
            ConfigureMainTabChrome();

            tabAffiliateHunter = new TabPage("Săn Affiliate");
            tabAiVideoGen = new TabPage("AI tạo video");
            tabAutoPost = new TabPage("Đăng tự động");
            tabAutoWarmup = new TabPage("Làm ấm tài khoản");
            tabSetting = new TabPage("Cài đặt");
            BuildHealthDashboardTabUi();
            BuildRevenueDashboardTabUi();

            ConfigureTabPage(tabAffiliateHunter);
            ConfigureTabPage(tabAiVideoGen);
            ConfigureTabPage(tabAutoPost);
            ConfigureTabPage(tabAutoWarmup);
            ConfigureTabPage(tabSetting);
            ConfigureTabPage(tabHealthDashboard);
            ConfigureTabPage(tabRevenueDashboard);

            tabMain.TabPages.AddRange(new[]
            {
                tabHealthDashboard,
                tabRevenueDashboard,
                tabAffiliateHunter,
                tabAiVideoGen,
                tabAutoPost,
                tabAutoWarmup,
                tabSetting
            });

            BuildGlobalLogShell();
            BuildAffiliateHunterUi();
            BuildAiVideoGenUi();
            BuildAutoPostUi();
            BuildAutoWarmupUi();
            BuildSettingUi();
            BuildApprovalQueueBehaviorControls();
            BuildSidebarUi();
            PostAdjustUiLayout();

            statusStripMain = new StatusStrip
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SizingGrip = false
            };
            tslStatusMain = new ToolStripStatusLabel
            {
                Spring = true,
                Text = "Sẵn sàng — chọn mục bên trái hoặc Cài đặt để nhập API key và profile."
            };
            statusStripMain.Items.Add(tslStatusMain);
            tabMain.SelectedIndexChanged += TabMain_SelectedIndexChanged;

            pnlMainWorkspace = new Panel
            {
                Name = "pnlMainWorkspace",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 34, 44),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            pnlMainWorkspace.Controls.Add(tabMain);
            pnlMainWorkspace.Controls.Add(pnlSidebar);

            Controls.Add(pnlMainWorkspace);
            Controls.Add(pnlGlobalLog);
            Controls.Add(statusStripMain);

            if (tabMain.TabPages.Count > 0)
            {
                tabMain.SelectedIndex = 0;
            }

            HighlightSidebarForSelectedTab();
            ApplyGlobalLogChrome();

            _ = RefreshHealthDashboardAsync();
        }

        private void ConfigureTabPage(TabPage page)
        {
            if (page == null)
            {
                return;
            }

            page.BackColor = Color.FromArgb(32, 34, 44);
            page.ForeColor = Color.Gainsboro;
        }

        private void PostAdjustUiLayout()
        {
            WrapSettingsTabResponsive();
            ApplyTabEnhancements(tabSetting);
            ApplyTabEnhancements(tabAffiliateHunter);
            EnsureAffiliateFiltersLayout();
            ApplyTabEnhancements(tabAiVideoGen);
            ApplyTabEnhancements(tabAutoPost);
            ApplyTabEnhancements(tabAutoWarmup);
            ApplyTabEnhancements(tabRevenueDashboard);
        }

        private static void DisableAutoScrollRecursive(Control parent)
        {
            if (parent == null)
            {
                return;
            }

            if (string.Equals(parent.Name, "tabHuntProduct", StringComparison.Ordinal)
                || string.Equals(parent.Name, "pnlHuntProductFiltersScroll", StringComparison.Ordinal)
                || string.Equals(parent.Name, "splitHuntProductMain", StringComparison.Ordinal))
            {
                return;
            }

            if (parent is ScrollableControl scrollable)
            {
                scrollable.AutoScroll = false;
                scrollable.AutoScrollMinSize = Size.Empty;
            }

            foreach (Control child in parent.Controls)
            {
                if (string.Equals(child.Name, "tabHuntProduct", StringComparison.Ordinal)
                    || string.Equals(child.Name, "pnlHuntProductFiltersScroll", StringComparison.Ordinal)
                    || string.Equals(child.Name, "splitHuntProductMain", StringComparison.Ordinal))
                {
                    continue;
                }

                DisableAutoScrollRecursive(child);
            }
        }

        private void ApplyTabEnhancements(TabPage tab)
        {
            if (tab == null)
            {
                return;
            }

            if (ReferenceEquals(tab, tabAiVideoGen))
            {
                tab.AutoScroll = false;
                tab.AutoScrollMinSize = new Size(0, 0);
                LocalizeControlTextsRecursive(tab.Controls);
                return;
            }

            if (ReferenceEquals(tab, tabAutoPost))
            {
                tab.AutoScroll = false;
                tab.AutoScrollMinSize = Size.Empty;
                DisableAutoScrollRecursive(tab);
                LocalizeControlTextsRecursive(tab.Controls);
                return;
            }

            if (ReferenceEquals(tab, tabSetting))
            {
                tab.AutoScroll = false;
                tab.AutoScrollMinSize = new Size(0, 0);
                LocalizeControlTextsRecursive(tab.Controls);
                return;
            }

            if (ReferenceEquals(tab, tabAutoWarmup))
            {
                tab.AutoScroll = true;
                tab.AutoScrollMinSize = Size.Empty;
                LocalizeControlTextsRecursive(tab.Controls);
                return;
            }

            if (ReferenceEquals(tab, tabAffiliateHunter))
            {
                tab.AutoScroll = false;
                tab.AutoScrollMinSize = Size.Empty;
                DisableAutoScrollRecursive(tab);
                EnableHuntProductTabAutoScroll();
                LocalizeControlTextsRecursive(tab.Controls);
                return;
            }

            tab.AutoScroll = true;
            LocalizeControlTextsRecursive(tab.Controls);
            var requiredOuter = MeasureContentSize(tab.Controls);
            tab.AutoScrollMinSize = new Size(
                Math.Max(0, requiredOuter.Width + 24),
                Math.Max(tab.ClientSize.Height + 20, requiredOuter.Height + 24));
        }

        private void LocalizeControlTextsRecursive(Control.ControlCollection controls)
        {
            if (controls == null)
            {
                return;
            }

            foreach (Control control in controls)
            {
                if (!string.IsNullOrWhiteSpace(control.Text))
                {
                    control.Text = LocalizeDisplayText(control.Text);
                }

                if (control is DataGridView grid && grid.Columns != null)
                {
                    foreach (DataGridViewColumn col in grid.Columns)
                    {
                        if (!string.IsNullOrWhiteSpace(col.HeaderText))
                        {
                            col.HeaderText = LocalizeDisplayText(col.HeaderText);
                        }
                    }
                }

                if (control.HasChildren)
                {
                    LocalizeControlTextsRecursive(control.Controls);
                }
            }
        }

        private static Size MeasureContentSize(Control.ControlCollection controls)
        {
            var maxRight = 0;
            var maxBottom = 0;
            if (controls == null)
            {
                return new Size(0, 0);
            }

            foreach (Control control in controls)
            {
                maxRight = Math.Max(maxRight, control.Right);
                maxBottom = Math.Max(maxBottom, control.Bottom);
                if (control.HasChildren)
                {
                    var child = MeasureContentSize(control.Controls);
                    maxRight = Math.Max(maxRight, control.Left + child.Width);
                    maxBottom = Math.Max(maxBottom, control.Top + child.Height);
                }
            }

            return new Size(maxRight, maxBottom);
        }

        private static string LocalizeDisplayText(string source)
        {
            var text = (source ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Auto Warm-up"] = "Làm ấm tài khoản",
                ["Affiliate Hunter"] = "Săn Affiliate",
                ["Auto Post"] = "Đăng tự động",
                ["Setting"] = "Cài đặt",
                ["Niche Keywords"] = "Từ khóa ngách",
                ["Running Profile"] = "Profile chạy",
                ["Videos To Interact With"] = "Số video tương tác",
                ["Watch Min (s)"] = "Xem tối thiểu (giây)",
                ["Watch Max (s)"] = "Xem tối đa (giây)",
                ["Enable AI-generated comments"] = "Bật bình luận AI",
                ["Execution Mode"] = "Chế độ chạy",
                ["Dry Run (safe simulation)"] = "Dry Run (mô phỏng an toàn)",
                ["Live Run (real actions)"] = "Live Run (hành động thật)",
                ["Live: first run opens a browser; please sign in to TikTok manually once."] = "Live: lần đầu sẽ mở trình duyệt, vui lòng đăng nhập TikTok thủ công một lần.",
                ["Start Warm-up"] = "Bắt đầu làm ấm",
                ["Resume"] = "Tiếp tục",
                ["Stop"] = "Dừng",
                ["Add To Queue"] = "Thêm vào hàng đợi",
                ["Start Queue"] = "Chạy",
                ["Stop Queue"] = "Dừng hàng đợi",
                ["Pause Queue"] = "Tạm dừng hàng đợi",
                ["Resume Queue"] = "Tiếp tục hàng đợi",
                ["Pause Now"] = "Tạm dừng ngay",
                ["Remove Selected"] = "Xóa mục chọn",
                ["Clear Queue"] = "Xóa toàn bộ hàng đợi",
                ["Move Up"] = "Di chuyển lên",
                ["Move Down"] = "Di chuyển xuống",
                ["Queue History"] = "Lịch sử hàng đợi",
                ["Queue Trend"] = "Xu hướng hàng đợi",
                ["Approval Queue"] = "Hàng đợi duyệt",
                ["Refresh Stats"] = "Làm mới thống kê",
                ["Queue: 0 job(s)"] = "Hàng đợi: 0 mục",
                ["Today"] = "Hôm nay",
                ["Custom"] = "Tùy chỉnh",
                ["Progress: 0/0 (0%)"] = "Tiến độ: 0/0 (0%)",
                ["Max Retries"] = "Số lần thử tối đa",
                ["Retry"] = "Thử lại",
                ["Created"] = "Thời gian tạo",
                ["Next Retry ETA"] = "Lần thử lại kế tiếp",
                ["Last Error"] = "Lỗi gần nhất",
                ["Keywords"] = "Từ khóa",
                ["Videos"] = "Số video",
                ["Watch Range"] = "Khoảng thời gian xem",
                ["Open Logs Folder"] = "Mở thư mục log",
                ["Export Current Logs"] = "Xuất log hiện tại",
                ["Clear Logs"] = "Xóa log",
                ["Keywords"] = "Từ khóa",
                ["Max Results"] = "Kết quả tối đa",
                ["Hunt Affiliates"] = "Quét Affiliate",
                ["Stop"] = "Dừng",
                ["Export CSV"] = "Xuất CSV",
                ["Push High-Quality"] = "Đẩy hàng chất lượng cao",
                ["Only high-quality"] = "Chỉ lấy chất lượng cao",
                ["Min safety score"] = "Điểm an toàn tối thiểu",
                ["Preview 4 ảnh biến thể"] = "Xem trước 4 ảnh biến thể",
                ["Transition (s)"] = "Chuyển cảnh (giây)",
                ["Text Size"] = "Cỡ chữ",
                ["Music Volume (%)"] = "Âm lượng nhạc (%)",
                ["Avatar Identity Pack (3-5 ảnh)"] = "Bộ nhận diện Avatar (3-5 ảnh)",
                ["Loại video"] = "Loại video",
                ["Quote"] = "Quote",
                ["Product Slideshow"] = "Sản phẩm (Slideshow)",
                ["Affiliate Deep"] = "Affiliate chuyên sâu",
                ["Mascot Story"] = "Mascot Story",
                ["Review Script before Render"] = "Duyệt kịch bản trước render",
                ["Run Mascot Story Pipeline"] = "Render Mascot Story",
                ["Run Affiliate Deep Video (4 scenes)"] = "Render Affiliate chuyên sâu",
                ["Run Philosophy/Quote Video"] = "Chạy video Quote",
                ["Copy Prompt"] = "Sao chép",
                ["Browse"] = "Duyệt...",
                ["Save Settings"] = "Lưu cài đặt",
                ["Chrome profile"] = "Profile Chrome",
                ["Sign in to TikTok (QR)"] = "Đăng nhập TikTok (QR)",
                ["Always require pre-post approval"] = "Luôn yêu cầu duyệt trước khi đăng",
                ["Always require pre-render approval"] = "Luôn yêu cầu duyệt trước khi render",
                ["Auto run approved items"] = "Tự chạy mục đã duyệt",
                ["Auto resume warm-up queue on startup"] = "Tự khôi phục hàng đợi khi mở app",
                ["Start"] = "Bắt đầu",
                ["Stop"] = "Dừng",
                ["Pause"] = "Tạm dừng",
                ["Resume"] = "Tiếp tục",
                ["Save"] = "Lưu",
                ["Type"] = "Loại",
                ["Status"] = "Trạng thái",
                ["Profile"] = "Profile",
                ["Score"] = "Điểm",
                ["Title"] = "Tiêu đề",
                ["Reasons"] = "Lý do",
                ["Reviewed By"] = "Người duyệt",
                ["Reviewed At"] = "Thời gian duyệt",
                ["Last Audit Action"] = "Hành động duyệt gần nhất",
                ["Date"] = "Ngày",
                ["Success"] = "Thành công",
                ["Failed"] = "Thất bại",
                ["Total"] = "Tổng",
                ["Success Rate"] = "Tỉ lệ thành công",
                ["Result"] = "Kết quả",
                ["Error"] = "Lỗi",
                ["AI Video Gen"] = "AI tạo video",
                ["Settings"] = "Cài đặt",
                ["Show"] = "Hiện",
                ["Hide"] = "Ẩn",
                ["Test"] = "Kiểm tra",
                ["Browse RootPath"] = "Duyệt RootPath",
                ["Min engagement score"] = "Điểm tương tác tối thiểu",
                ["Only high-quality"] = "Chỉ chất lượng cao",
                ["Batch Pipeline"] = "Pipeline hàng loạt",
                ["Deep Dive (Queue)"] = "Phân tích chuyên sâu (hàng đợi)",
                ["Auto-Detect Mouth"] = "Tự nhận diện miệng",
                ["Auto-Lipsync"] = "Tự động Lip-sync",
                ["Preview LipSync"] = "Xem thử Lip-sync",
                ["Visual Hook SFX (3s)"] = "Hook SFX hình ảnh (3s)",
                ["Profile"] = "Profile",
                ["Status"] = "Trạng thái",
                ["Scheduled At"] = "Lịch chạy",
                ["Remove"] = "Xóa",
                ["Clear"] = "Xóa",
                ["Delete"] = "Xóa",
                ["Cancel"] = "Hủy",
                ["OK"] = "Đồng ý",
                ["Yes"] = "Có",
                ["No"] = "Không",
                ["Render"] = "Render",
                ["Start"] = "Bắt đầu",
                ["Export"] = "Xuất",
                ["Import"] = "Nhập",
                ["Refresh"] = "Làm mới",
                ["Copy"] = "Sao chép",
                ["Paste"] = "Dán",
                ["Open"] = "Mở",
                ["Close"] = "Đóng",
                ["Upload"] = "Tải lên",
                ["Download"] = "Tải xuống",
                ["Enable"] = "Bật",
                ["Disable"] = "Tắt",
                ["Caption"] = "Caption",
                ["Description"] = "Mô tả",
                ["Title"] = "Tiêu đề",
                ["Queue"] = "Hàng đợi",
                ["History"] = "Lịch sử",
                ["Trend"] = "Xu hướng",
                ["Dashboard"] = "Bảng điều khiển",
                ["Health"] = "Sức khỏe hệ thống",
                ["Revenue"] = "Doanh thu",
                ["Approval"] = "Duyệt",
                ["Warm-up"] = "Làm ấm",
                ["Warmup"] = "Làm ấm",
                ["Affiliate"] = "Affiliate",
                ["Gemini"] = "Gemini",
                ["Chrome profile"] = "Profile Chrome",
                ["TTS API Key (Text-to-Speech)"] = "Khóa API TTS (Text-to-Speech)",
                ["Veo API Key (video AI, tùy chọn)"] = "Khóa API Veo (video AI, tùy chọn)",
                ["2Captcha API Key"] = "Khóa API 2Captcha",
                ["Gemini / AI API Key"] = "Khóa API Gemini / AI"
            };

            if (map.TryGetValue(text, out var localized))
            {
                return localized;
            }

            return text;
        }

        private async void btnHuntAffiliates_Click(object sender, EventArgs e)
        {
            var keywordEntries = ParseAffiliateKeywordEntriesFromUi();
            if (keywordEntries.Count == 0)
            {
                MessageBox.Show(this,
                    "Hãy nhập ít nhất 1 từ khoá vào ô «từ khoá» (bấm vào ô rồi gõ, không để text gợi ý xám).",
                    "Săn Affiliate",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                txtAffiliateKeywords?.Focus();
                return;
            }

            if (keywordEntries.Count >= 3)
            {
                var estMinutes = (int)Math.Ceiling(keywordEntries.Count * 1.5);
                var confirm = MessageBox.Show(this,
                    $"Bạn sắp quét {keywordEntries.Count} từ khoá, dự kiến mất ~{estMinutes} phút. Tiếp tục?",
                    "Săn Affiliate",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            ApplyProfileScope(keywordEntries[0].ProfileName);

            btnHuntAffiliates.Enabled = false;
            btnExportAffiliateCsv.Enabled = false;
            SaveAffiliateHuntResultsToDisk();
            _affiliateBindingList.Clear();
            _affiliateAllResults = new List<AffiliateCandidate>();
            _huntCancellation?.Dispose();
            _huntCancellation = RegisterActiveJobCancellation();
            _lastHuntKeywordEntries = new List<HuntKeywordEntry>(keywordEntries);
            _lastHuntKeywords = keywordEntries.Select(e => e.Keyword).ToList();
            _lastHuntKeyword = string.Join(",", keywordEntries.Select(e => $"{e.Keyword}@{e.ProfileName}"));
            foreach (var entry in keywordEntries)
            {
                Log($"Đã nhận diện từ khóa [{entry.Keyword}] cho nick [{entry.ProfileName}]");
            }

            RefreshAffiliateDownloadFolderHint();
            var platforms = GetSelectedAffiliatePlatformsFromUi();
            if (platforms.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn ít nhất một nền tảng: TikTok, Facebook hoặc YouTube.",
                    "Săn Affiliate",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                btnHuntAffiliates.Enabled = true;
                btnExportAffiliateCsv.Enabled = true;
                btnStopHunt.Enabled = false;
                return;
            }

            try
            {
                var huntSettings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(huntSettings.StorageRootPath);
                foreach (var entry in keywordEntries)
                {
                    ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(huntSettings.StorageRootPath, entry.ProfileName);
                }

                var payload = new HuntAffiliateJobPayload
                {
                    KeywordEntries = keywordEntries,
                    Keywords = keywordEntries.Select(e => e.Keyword).ToList(),
                    Platforms = platforms,
                    SearchMode = "Video",
                    ProfileName = keywordEntries[0].ProfileName,
                    MaxResultsPerKeyword = (int)numAffiliateMaxResults.Value,
                    RankByEngagement = chkAffiliateRankByEngagement?.Checked ?? huntSettings.AffiliateRankByEngagementEnabled,
                    BufferMultiplier = numAffiliateBufferMultiplier != null
                        ? (double)numAffiliateBufferMultiplier.Value
                        : huntSettings.AffiliateHuntBufferMultiplier,
                    StorageRootPath = huntSettings.StorageRootPath ?? string.Empty,
                    TikTokHuntMethod = cbTikTokHuntMethod != null && cbTikTokHuntMethod.SelectedIndex >= 0
                        ? (cbTikTokHuntMethod.SelectedIndex == 0 ? TikTokHuntMethods.RapidApi : TikTokHuntMethods.Browser)
                        : huntSettings.TikTokHuntMethod,
                    TikTokRapidApiFallbackToBrowser = chkTikTokApiFallbackBrowser != null
                        ? chkTikTokApiFallbackBrowser.Checked
                        : huntSettings.TikTokRapidApiFallbackToBrowser
                };

                var job = new OmniJob
                {
                    Kind = OmniJobKind.HuntAffiliate,
                    Title = "Săn Affiliate (" + keywordEntries.Count + " từ khoá)",
                    ProfileName = keywordEntries[0].ProfileName,
                    PayloadJson = JsonConvert.SerializeObject(payload),
                    MaxRetries = 2,
                    Tag = _huntCancellation
                };
                _activeHuntJob = job;
                _globalJobQueue.Enqueue(job);
                Log("[JobQueue] Hunt đã vào hàng đợi — tối đa " + huntSettings.MaxConcurrentJobs + " job chạy song song.");
            }
            catch (Exception ex)
            {
                Log("[Affiliate] Lỗi khi bắt đầu Hunt: " + ex.Message);
                MessageBox.Show(this, "Không bắt đầu được Hunt:\n" + ex.Message, "Săn Affiliate",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnHuntAffiliates.Enabled = true;
                btnExportAffiliateCsv.Enabled = true;
                btnStopHunt.Enabled = false;
                _activeHuntJob = null;
                _huntCancellation?.Dispose();
                _huntCancellation = null;
            }
        }

        private List<string> GetSelectedAffiliatePlatformsFromUi()
        {
            var list = new List<string>();
            if (chkAffiliatePlatformTikTok?.Checked == true)
            {
                list.Add(AffiliateSourceIds.TikTok);
            }

            if (chkAffiliatePlatformFacebook?.Checked == true)
            {
                list.Add(AffiliateSourceIds.Facebook);
            }

            if (chkAffiliatePlatformYouTube?.Checked == true)
            {
                list.Add(AffiliateSourceIds.YouTube);
            }

            return list;
        }

        private List<HuntKeywordEntry> ParseAffiliateKeywordEntriesFromUi()
        {
            var raw = GetAffiliateKeywordsTextFromUi();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<HuntKeywordEntry>();
            }

            _affiliateKeywordsPlaceholderActive = false;
            if (txtAffiliateKeywords != null)
            {
                txtAffiliateKeywords.ForeColor = Color.WhiteSmoke;
            }

            return ParseAffiliateKeywordEntries(raw, GetAffiliateHuntProfileFromUi());
        }

        private string GetAffiliateKeywordsTextFromUi()
        {
            var raw = txtAffiliateKeywords?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            if (string.Equals(raw.Trim(), AffiliateKeywordsPlaceholder, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return raw;
        }

        private string GetAffiliateHuntProfileFromUi()
        {
            var profile = cbAffiliateHuntProfile?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(profile))
            {
                profile = GetRunningProfileName();
            }

            return ProfileScopedPaths.ResolveProfileName(profile);
        }

        private static List<HuntKeywordEntry> ParseAffiliateKeywordEntries(string raw, string profileName)
        {
            return AffiliateKeywordParser.ParseKeywordLines(raw, profileName);
        }

        /// <summary>
        /// Chế độ Video + xếp hạng: săn thêm ứng viên rồi cắt top sau TikWM. Shop hoặc tắt rank → chỉ lấy đúng Max Results.
        /// </summary>
        private static int ComputeAffiliateCollectLimit(int desired, double multiplier, bool rankEnabled, AffiliateSearchMode mode)
        {
            desired = Math.Max(1, desired);
            if (!rankEnabled || mode != AffiliateSearchMode.Video)
            {
                return desired;
            }

            var m = multiplier;
            if (double.IsNaN(m) || m < 1.5d)
            {
                m = 1.5d;
            }

            if (m > 5.0d)
            {
                m = 5.0d;
            }

            var raw = Math.Max(desired, (int)Math.Ceiling(desired * m));
            return Math.Min(AffiliateHuntBufferHardCap, raw);
        }

        private void MergeAffiliateHuntResultsIntoAll(
            List<AffiliateCandidate> all,
            IList<AffiliateCandidate> batch,
            string keyword)
        {
            if (all == null || batch == null)
            {
                return;
            }

            var kw = (keyword ?? string.Empty).Trim();
            var addedCount = 0;
            foreach (var incoming in batch)
            {
                if (incoming == null)
                {
                    continue;
                }

                var url = (incoming.VideoUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    if (!string.IsNullOrWhiteSpace(kw))
                    {
                        incoming.SourceKeyword = kw;
                    }

                    all.Add(incoming);
                    addedCount++;
                    continue;
                }

                var dup = all.FirstOrDefault(x =>
                    string.Equals((x?.VideoUrl ?? string.Empty).Trim(), url, StringComparison.OrdinalIgnoreCase));
                if (dup == null)
                {
                    if (!string.IsNullOrWhiteSpace(kw))
                    {
                        incoming.SourceKeyword = string.IsNullOrWhiteSpace(incoming.SourceKeyword)
                            ? kw
                            : MergeDistinctKeywordLabels(incoming.SourceKeyword, kw);
                    }

                    all.Add(incoming);
                    addedCount++;
                    if (!string.IsNullOrWhiteSpace(url) &&
                        !_huntHistoryStore.ContainsRecent(url, TimeSpan.FromDays(7)))
                    {
                        _huntHistoryStore.Record(url, incoming.ProfileName, kw);
                    }
                }
                else
                {
                    dup.SourceKeyword = MergeDistinctKeywordLabels(dup.SourceKeyword, incoming.SourceKeyword);
                    if (!string.IsNullOrWhiteSpace(incoming.ProfileName))
                    {
                        dup.ProfileName = ProfileScopedPaths.ResolveProfileName(incoming.ProfileName);
                    }
                }
            }
        }

        private static string ResolvePrimaryProfileFromAiBuffer(IList<AiVideoGenInputItem> items)
        {
            if (items == null)
            {
                return "default";
            }

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item?.ProfileName))
                {
                    return ProfileScopedPaths.ResolveProfileName(item.ProfileName);
                }
            }

            return "default";
        }

        private static string SanitizeAffiliateKeywordForFolder(AffiliateCandidate candidate)
        {
            var kw = (candidate?.SourceKeyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(kw))
            {
                kw = "default";
            }

            var safe = SanitizePathSegment(kw);
            return string.IsNullOrWhiteSpace(safe) ? "default" : safe;
        }

        private static string MergeDistinctKeywordLabels(string existing, string append)
        {
            var parts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void ingest(string chunk)
            {
                foreach (var piece in (chunk ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = piece.Trim();
                    if (t.Length == 0 || !seen.Add(t))
                    {
                        continue;
                    }

                    parts.Add(t);
                }
            }

            ingest(existing);
            ingest(append);
            return string.Join(", ", parts);
        }

        private void TxtAffiliateKeywords_Enter(object sender, EventArgs e)
        {
            if (txtAffiliateKeywords == null)
            {
                return;
            }

            if (_affiliateKeywordsPlaceholderActive
                && string.Equals(txtAffiliateKeywords.Text, AffiliateKeywordsPlaceholder, StringComparison.Ordinal))
            {
                txtAffiliateKeywords.Text = string.Empty;
                txtAffiliateKeywords.ForeColor = Color.WhiteSmoke;
                _affiliateKeywordsPlaceholderActive = false;
            }

            RefreshAffiliateDownloadFolderHint();
        }

        private void TxtAffiliateKeywords_Leave(object sender, EventArgs e)
        {
            if (txtAffiliateKeywords == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(txtAffiliateKeywords.Text))
            {
                txtAffiliateKeywords.Text = AffiliateKeywordsPlaceholder;
                txtAffiliateKeywords.ForeColor = Color.FromArgb(120, 125, 140);
                _affiliateKeywordsPlaceholderActive = true;
            }

            RefreshAffiliateDownloadFolderHint();
        }

        private void TxtAffiliateKeywords_TextChanged(object sender, EventArgs e)
        {
            if (txtAffiliateKeywords == null)
            {
                return;
            }

            var text = txtAffiliateKeywords.Text ?? string.Empty;
            var isPlaceholder = string.Equals(text, AffiliateKeywordsPlaceholder, StringComparison.Ordinal);
            if (_affiliateKeywordsPlaceholderActive && !isPlaceholder && !string.IsNullOrWhiteSpace(text))
            {
                _affiliateKeywordsPlaceholderActive = false;
                txtAffiliateKeywords.ForeColor = Color.WhiteSmoke;
            }

            RefreshAffiliateDownloadFolderHint();
        }

        private void AffiliateBindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            RecomputeAffiliateDeepDiveErrorColumnVisibility();
        }

        private void RecomputeAffiliateDeepDiveErrorColumnVisibility()
        {
            if (_affiliateBindingList == null || dgvAffiliateResults == null || dgvAffiliateResults.IsDisposed)
            {
                return;
            }

            var any = false;
            foreach (var c in _affiliateBindingList)
            {
                if (!string.IsNullOrWhiteSpace(c?.LastDeepDiveError))
                {
                    any = true;
                    break;
                }
            }

            if (_affiliateDeepDiveErrorColumnVisible == any)
            {
                dgvAffiliateResults?.Invalidate();
                return;
            }

            _affiliateDeepDiveErrorColumnVisible = any;
            ApplyAffiliateGridColumnPresentation();
        }

        private void ApplyAffiliateGridColumnPresentation()
        {
            if (dgvAffiliateResults?.Columns == null || dgvAffiliateResults.Columns.Count == 0)
            {
                return;
            }

            foreach (DataGridViewColumn col in dgvAffiliateResults.Columns)
            {
                var name = col.DataPropertyName ?? string.Empty;
                switch (name)
                {
                    case "ImageUrl":
                    case "ProfileUrl":
                    case "Price":
                    case "CommissionRate":
                    case "VideoScript":
                    case "Creator":
                    case "SafetyRiskSummary":
                    case "SafetyScore":
                    case "MetricsCapturedAtUtc":
                    case "LastMetricsError":
                    case "IsSlideshowRendered":
                    case "LastRenderOutputPath":
                    case "LastRenderError":
                    case "SourcePlatform":
                    case "CustomerReviews":
                    case "Category":
                    case "LinkedProduct":
                    case "VoiceoverTranscript":
                        col.Visible = false;
                        break;
                    case "ProfileName":
                        col.HeaderText = "Profile";
                        col.ToolTipText = "Chrome profile / nick sở hữu dòng này";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.Visible = true;
                        col.FillWeight = 8;
                        col.MinimumWidth = 64;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        break;
                    case "SourceKeyword":
                        col.HeaderText = "Từ khoá";
                        col.ToolTipText = "Từ khoá nguồn — bấm ô để xem đầy đủ / sao chép";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.HeaderCell.Style.Padding = new Padding(4, 2, 4, 2);
                        col.Visible = true;
                        col.FillWeight = 10;
                        col.MinimumWidth = 92;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        break;
                    case "ProductName":
                        col.HeaderText = "Caption";
                        col.ToolTipText = "Caption / tiêu đề video — bấm ô để xem đầy đủ / sao chép";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.Visible = true;
                        col.FillWeight = 22;
                        col.MinimumWidth = 100;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        break;
                    case "Hashtags":
                        col.HeaderText = "Hashtag";
                        col.ToolTipText = "Hashtag — bấm ô để xem đầy đủ / sao chép";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.Visible = true;
                        col.FillWeight = 12;
                        col.MinimumWidth = 80;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        break;
                    case "VideoUrl":
                        col.HeaderText = "Link video";
                        col.ToolTipText = "URL video";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.Visible = true;
                        col.FillWeight = 10;
                        col.MinimumWidth = 110;
                        break;
                    case "PlayCount":
                        col.HeaderText = "👁";
                        col.ToolTipText = "Views (lượt xem)";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 8;
                        col.MinimumWidth = 58;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "LikeCount":
                        col.HeaderText = "❤";
                        col.ToolTipText = "Likes";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 8;
                        col.MinimumWidth = 58;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "CommentCount":
                        col.HeaderText = "💬";
                        col.ToolTipText = "Comments (bình luận)";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 7;
                        col.MinimumWidth = 54;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "ShareCount":
                        col.HeaderText = "↗";
                        col.ToolTipText = "Shares (chia sẻ)";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 7;
                        col.MinimumWidth = 54;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "CollectCount":
                        col.HeaderText = "💾";
                        col.ToolTipText = "Saves (lưu)";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 7;
                        col.MinimumWidth = 54;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "DurationSeconds":
                        col.HeaderText = "⏱";
                        col.ToolTipText = "Thời lượng (giây)";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 6;
                        col.MinimumWidth = 52;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        break;
                    case "CreateTimeUtc":
                        col.HeaderText = "📅";
                        col.ToolTipText = "Giờ đăng (UTC)";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 10;
                        col.MinimumWidth = 78;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        break;
                    case "LastDeepDiveError":
                        col.HeaderText = "Lỗi Deep Dive";
                        col.Visible = _affiliateDeepDiveErrorColumnVisible;
                        col.FillWeight = 12;
                        col.MinimumWidth = 90;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                        break;
                    case "OrderNumber":
                        col.HeaderText = "STT";
                        col.ToolTipText = "Số thứ tự dòng";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        col.Visible = true;
                        col.FillWeight = 4;
                        col.MinimumWidth = 36;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        break;
                }
            }

            var displayOrder = new[]
            {
                "ProfileName", "ProductName", "Category", "SourceKeyword", "Hashtags",
                "PlayCount", "LikeCount", "CommentCount", "ShareCount", "CollectCount", "DurationSeconds", "CreateTimeUtc",
                "VideoUrl", "LastDeepDiveError",
                "LinkedProduct", "VoiceoverTranscript",
                "ImageUrl", "ProfileUrl", "Price", "CommissionRate", "VideoScript", "Creator", "SafetyRiskSummary",
                "SafetyScore", "MetricsCapturedAtUtc", "LastMetricsError"
            };
            for (var i = 0; i < displayOrder.Length; i++)
            {
                var key = displayOrder[i];
                var col = dgvAffiliateResults.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, key, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                {
                    col.DisplayIndex = i;
                }
            }

            dgvAffiliateResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvAffiliateResults.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            // HeaderText / HeaderCell.Style vừa đổi — áp lại font tiêu đề + độ rộng (net472).
            ApplyAppGridChrome(dgvAffiliateResults);
            dgvAffiliateResults.Invalidate();
        }

        private void btnStopHunt_Click(object sender, EventArgs e)
        {
            var hasVideoReupJobs = _videoReupBatchJobIds != null && _videoReupBatchJobIds.Count > 0;
            if (_huntCancellation == null
                && !(_affiliateDownloadingBatch && _affiliateDownloadBatchCts != null)
                && !_affiliateAutoEnrichRunning
                && !hasVideoReupJobs
                && !(_revenueFetchRunning && _revenueCancellation != null))
            {
                return;
            }

            btnStopHunt.Enabled = false;
            if (hasVideoReupJobs)
            {
                CancelAllVideoReupBatchJobs();
            }

            TryCancel(_affiliateDownloadBatchCts);
            if (_affiliateDownloadingBatch)
            {
                Log("Đang hủy tải video hàng loạt...");
            }

            TryCancel(_affiliateAutoEnrichCts);
            if (_affiliateAutoEnrichRunning)
            {
                Log("Đang hủy auto enrich...");
            }

            CancelAffiliateBrowserWork();
            Log("Đang dừng săn Affiliate / cào báo cáo / Auto Post…");
        }

        private async void btnExportAffiliateCsv_Click(object sender, EventArgs e)
        {
            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                Log("No affiliate results to export.");
                return;
            }

            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.FileName = $"affiliates_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    dialog.Filter =
                        "CSV (*.csv)|*.csv|HTML báo cáo (*.html)|*.html|Tất cả|*.*";
                    dialog.Title = "Xuất CSV hoặc HTML";

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    var snapshot = new List<AffiliateCandidate>(_affiliateBindingList);
                    var path = dialog.FileName;
                    if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        await _affiliateHunter.ExportHtmlSummaryAsync(snapshot, path, CancellationToken.None)
                            .ConfigureAwait(true);
                        Log("Đã xuất báo cáo HTML: " + path);
                    }
                    else
                    {
                        await _affiliateHunter.ExportCsvAsync(snapshot, path, CancellationToken.None)
                            .ConfigureAwait(true);
                        Log("Affiliate results exported to: " + path);
                    }

                    Log("[GeminiUsage] " + GeminiUsageTracker.Instance.GetSessionSummaryForUi());
                }
            }
            catch (Exception ex)
            {
                Log("Failed to export affiliate: " + ex.Message);
            }
        }

        private void btnPushToAiVideoGen_Click(object sender, EventArgs e)
        {
            if (btnPushToAiVideoGen != null)
            {
                btnPushToAiVideoGen.Enabled = false;
            }

            try
            {
                var targetMode = PromptAffiliatePushTargetMode();
                if (targetMode == null)
                {
                    return;
                }

                switch (targetMode.Value)
                {
                    case AffiliatePushTargetMode.Slideshow:
                        PushVisibleAffiliateRowsToAiVideoGen(targetDeepDive: false);
                        break;
                    case AffiliatePushTargetMode.DeepDive:
                        PushVisibleAffiliateRowsToAiVideoGen(targetDeepDive: true);
                        break;
                    case AffiliatePushTargetMode.VideoReup:
                        PushVisibleAffiliateRowsToVideoReup();
                        break;
                }
            }
            finally
            {
                if (btnPushToAiVideoGen != null && !btnPushToAiVideoGen.IsDisposed)
                {
                    btnPushToAiVideoGen.Enabled = _affiliateBindingList != null && _affiliateBindingList.Count > 0;
                }
            }
        }

        private void btnPushSelectionToVideoReup_Click(object sender, EventArgs e)
        {
            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                LogVideoReup("Video reup: không có dữ liệu affiliate — hãy săn hoặc tải kết quả trước.");
                return;
            }

            var selectedItems = new List<AffiliateCandidate>();
            if (dgvAffiliateResults?.SelectedRows != null && dgvAffiliateResults.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
                {
                    if (row?.DataBoundItem is AffiliateCandidate candidate)
                    {
                        selectedItems.Add(candidate);
                    }
                }
            }

            if (selectedItems.Count == 0)
            {
                selectedItems.AddRange(_affiliateBindingList);
            }

            PushAffiliateCandidatesToVideoReup(selectedItems);
        }

        private void PushAffiliateCandidatesToVideoReup(IEnumerable<AffiliateCandidate> candidates)
        {
            if (_videoReupBindingList == null)
            {
                return;
            }

            _videoReupBindingList.Clear();
            var skippedNoUrl = 0;
            foreach (var item in (candidates ?? Enumerable.Empty<AffiliateCandidate>())
                .Where(x => x != null)
                .GroupBy(x => (x.VideoUrl ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()))
            {
                var videoUrl = (item.VideoUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    skippedNoUrl++;
                    continue;
                }

                var productName = (item.ProductName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(productName))
                {
                    productName = (item.Creator ?? string.Empty).Trim();
                }

                _videoReupBindingList.Add(new VideoReupRowItem
                {
                    ProfileName = ProfileScopedPaths.ResolveProfileFromCandidate(item),
                    SourceKeyword = (item.SourceKeyword ?? string.Empty).Trim(),
                    ProductName = productName,
                    Price = string.IsNullOrWhiteSpace(item.Price) ? "N/A" : item.Price.Trim(),
                    VideoUrl = videoUrl,
                    ImageUrl = (item.ImageUrl ?? string.Empty).Trim(),
                    Hashtags = string.Empty,
                    VideoScript = (item.VideoScript ?? string.Empty).Trim()
                });
            }

            LogVideoReup($"Video reup: đã nhập {_videoReupBindingList.Count} dòng từ Săn Affiliate (bỏ qua không có URL video: {skippedNoUrl}).");
            if (_videoReupBindingList.Count > 0)
            {
                SwitchToMainTab(tabAiVideoGen);
                SelectAiVideoGenMode(AiVideoGenMode.VideoReup);

                VideoReupDownloadImportedRowsFireAndForgetAsync();
            }
        }

        private void dgvVideoReupInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.Delete || e.Alt || e.Control)
            {
                return;
            }

            if (_videoReupBindingList == null || dgvVideoReupInput?.SelectedRows == null || dgvVideoReupInput.SelectedRows.Count == 0)
            {
                return;
            }

            var items = new List<VideoReupRowItem>();
            foreach (DataGridViewRow row in dgvVideoReupInput.SelectedRows)
            {
                if (row?.DataBoundItem is VideoReupRowItem v)
                {
                    items.Add(v);
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            if (!UiConfirmHelper.ConfirmDeleteRows(this, items.Count))
            {
                return;
            }

            foreach (var v in items)
            {
                _videoReupBindingList.Remove(v);
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            LogVideoReup($"Video reup: đã xóa {items.Count} dòng (Delete).");
        }

        private void dgvProxyProfiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.Delete || e.Alt || e.Control)
            {
                return;
            }

            if (_proxyProfileBindingList == null || dgvProxyProfiles?.SelectedRows == null || dgvProxyProfiles.SelectedRows.Count == 0)
            {
                return;
            }

            var items = new List<AutomationProfile>();
            foreach (DataGridViewRow row in dgvProxyProfiles.SelectedRows)
            {
                if (row?.DataBoundItem is AutomationProfile profile)
                {
                    items.Add(profile);
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            if (!UiConfirmHelper.ConfirmDeleteRows(this, items.Count))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            foreach (var profile in items)
            {
                _proxyProfileBindingList.Remove(profile);
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }


        private bool ResolveVideoReupGeminiReady(AppSettings settings = null)
        {
            settings = settings ?? _videoReupSettingsSnap;
            if (!string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                return true;
            }

            return _systemHealth != null
                   && _systemHealth.TryGetValue("api_keys", out var apiOk)
                   && apiOk;
        }

        /// <summary>Render — FFmpeg + API + storage; Gemini nội dung — chỉ cần API key.</summary>
        private void SetVideoReupPipelineControlsEnabled(bool pipelineReady, bool geminiReady)
        {
            if (btnVideoReupHookGemini != null)
            {
                btnVideoReupHookGemini.Enabled = geminiReady;
            }

            if (btnVideoReupLyriaHook != null)
            {
                btnVideoReupLyriaHook.Enabled = pipelineReady;
            }

            if (grpVideoReupAudioMode != null)
            {
                grpVideoReupAudioMode.Enabled = pipelineReady;
            }

            if (rbVideoReupAudioAffiliate != null)
            {
                rbVideoReupAudioAffiliate.Enabled = pipelineReady;
            }

            if (rbVideoReupAudioFilm != null)
            {
                rbVideoReupAudioFilm.Enabled = pipelineReady;
            }

            if (cbVideoReupMusic != null)
            {
                cbVideoReupMusic.Enabled = pipelineReady;
            }

            if (btnVideoReupOpenMusicFolder != null)
            {
                btnVideoReupOpenMusicFolder.Enabled = pipelineReady;
            }

            if (btnVideoReupOpenHookSfxFolder != null)
            {
                btnVideoReupOpenHookSfxFolder.Enabled = true;
            }

            if (btnVideoReupOpenLogoLibrary != null)
            {
                btnVideoReupOpenLogoLibrary.Enabled = true;
            }

            if (txtVideoReupVideoUrl != null)
            {
                txtVideoReupVideoUrl.ReadOnly = !pipelineReady;
            }

            var renderReady = pipelineReady && ResolveVideoReupRenderReady();
            if (btnVideoReupProcessVideo != null && !btnVideoReupProcessVideo.IsDisposed)
            {
                btnVideoReupProcessVideo.Enabled = renderReady;
            }

            RefreshVideoReupNarrationScriptButtons(geminiReady);
        }

        /// <summary>Nút thêm dòng / nhập affiliate — luôn bật để người dùng chuẩn bị dữ liệu.</summary>
        private void SetVideoReupDataEntryControlsEnabled(bool enabled = true)
        {
            if (btnPushSelectionToVideoReup != null)
            {
                btnPushSelectionToVideoReup.Enabled = enabled;
            }

            if (btnVideoReupAddManualRow != null)
            {
                btnVideoReupAddManualRow.Enabled = enabled;
            }
        }

        private void UpdateVideoReupControlReadiness(bool pipelineReady, bool geminiReady)
        {
            _videoReupPipelineReady = pipelineReady;
            _videoReupGeminiReady = geminiReady;
            SetVideoReupPipelineControlsEnabled(pipelineReady, geminiReady);
        }

        private void SetVideoReupCaptionButtonsEnabled(bool enabledDuringOperation)
        {
            if (!enabledDuringOperation)
            {
                SetVideoReupPipelineControlsEnabled(false, false);
            }
            else
            {
                var geminiReady = ResolveVideoReupGeminiReady();
                SetVideoReupPipelineControlsEnabled(_videoReupPipelineReady, geminiReady);
            }

            SetVideoReupDataEntryControlsEnabled(true);
        }

        private void RefreshVideoReupReadinessLabel(AppSettings settings = null)
        {
            if (lblVideoReupReadiness == null || lblVideoReupReadiness.IsDisposed)
            {
                return;
            }

            if (settings == null)
            {
                _ = RefreshVideoReupReadinessLabelAsync();
                return;
            }

            void Apply()
            {
                _videoReupSettingsSnap = settings;
                var row = new VideoReupRowItem();
                var blockers = VideoReupRemixService.DescribePipelineBlockers(row, settings);
                var geminiReady = ResolveVideoReupGeminiReady(settings);
                bool Ok(string key) => _systemHealth != null
                                       && _systemHealth.TryGetValue(key, out var v)
                                       && v;
                var pipelineReady = VideoReupRemixService.IsTabRenderReady(
                    settings,
                    Ok("ffmpeg"),
                    Ok("storage"),
                    out var renderBlocker);
                lblVideoReupReadiness.ForeColor = pipelineReady
                    ? Color.FromArgb(120, 220, 160)
                    : Color.FromArgb(255, 180, 120);
                lblVideoReupReadiness.Text = pipelineReady
                    ? "✓ Sẵn sàng — chọn dòng trên lưới (Ctrl+click nhiều dòng = xử lý tuần tự từ trên xuống), rồi bấm «Tạo Hook+Script+Hashtag» hoặc «Render & Đóng gói»."
                    : "⚠ Chưa render được (vẫn thêm dòng được): "
                      + (string.IsNullOrWhiteSpace(renderBlocker)
                          ? blockers.Replace("\r\n", "  •  ")
                          : renderBlocker);
                UpdateVideoReupControlReadiness(pipelineReady, geminiReady);
            }

            if (lblVideoReupReadiness.InvokeRequired)
            {
                lblVideoReupReadiness.Invoke(new Action(Apply));
                return;
            }

            Apply();
        }

        private async Task RefreshVideoReupReadinessLabelAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                RefreshVideoReupReadinessLabel(settings);
            }
            catch
            {
                RefreshVideoReupReadinessLabel(new AppSettings());
            }
        }

        private enum AffiliatePushTargetMode
        {
            Slideshow,
            DeepDive,
            VideoReup
        }

        private AffiliatePushTargetMode? PromptAffiliatePushTargetMode()
        {
            using (var dlg = new Form
            {
                Text = "Đẩy sang AI Video Gen",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(360, 178),
                BackColor = Color.FromArgb(32, 34, 44),
                ForeColor = Color.Gainsboro,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            })
            {
                var lbl = new Label
                {
                    Text = "Chọn chế độ đích:",
                    AutoSize = true,
                    Location = new Point(16, 16),
                    ForeColor = Color.Gainsboro
                };
                var rbSlideshow = new RadioButton
                {
                    Text = "Slideshow (nhiều ảnh / SP)",
                    AutoSize = true,
                    Location = new Point(20, 44),
                    Checked = true,
                    ForeColor = Color.Gainsboro
                };
                var rbDeep = new RadioButton
                {
                    Text = "Affiliate chuyên sâu (4+ ảnh / 1 SP)",
                    AutoSize = true,
                    Location = new Point(20, 72),
                    ForeColor = Color.Gainsboro
                };
                var rbReup = new RadioButton
                {
                    Text = "Video Reup (tải & render lại video)",
                    AutoSize = true,
                    Location = new Point(20, 100),
                    ForeColor = Color.Gainsboro
                };
                var btnOk = new Button
                {
                    Text = "Đẩy",
                    DialogResult = DialogResult.OK,
                    Location = new Point(168, 136),
                    Size = new Size(80, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(82, 128, 89),
                    ForeColor = Color.White
                };
                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(256, 136),
                    Size = new Size(80, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 64, 77),
                    ForeColor = Color.WhiteSmoke
                };
                dlg.Controls.AddRange(new Control[] { lbl, rbSlideshow, rbDeep, rbReup, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return null;
                }

                if (rbDeep.Checked)
                {
                    return AffiliatePushTargetMode.DeepDive;
                }

                if (rbReup.Checked)
                {
                    return AffiliatePushTargetMode.VideoReup;
                }

                return AffiliatePushTargetMode.Slideshow;
            }
        }

        private void RefreshAiVideoGenModeReadinessLabels(bool? ffmpegOk = null, bool? apiOk = null, bool? storageOk = null)
        {
            if (ffmpegOk == null || apiOk == null || storageOk == null)
            {
                bool Ok(string key) => _systemHealth != null
                                       && _systemHealth.TryGetValue(key, out var v)
                                       && v;
                ffmpegOk = Ok("ffmpeg");
                apiOk = Ok("api_keys");
                storageOk = Ok("storage");
            }

            var pipelineReady = ffmpegOk.Value && apiOk.Value && storageOk.Value;
            var blockers = new List<string>();
            if (!apiOk.Value)
            {
                blockers.Add("Gemini API Key (Cài đặt)");
            }

            if (!ffmpegOk.Value)
            {
                blockers.Add("FFmpeg (Cài đặt)");
            }

            if (!storageOk.Value)
            {
                blockers.Add("Thư mục lưu trữ (Cài đặt)");
            }

            var blockerText = blockers.Count > 0
                ? string.Join("  •  ", blockers)
                : string.Empty;

            void ApplySlideshow()
            {
                if (lblSlideshowReadiness == null || lblSlideshowReadiness.IsDisposed)
                {
                    return;
                }

                lblSlideshowReadiness.ForeColor = pipelineReady
                    ? Color.FromArgb(120, 220, 160)
                    : Color.FromArgb(255, 180, 120);
                lblSlideshowReadiness.Text = pipelineReady
                    ? "✓ Bảng SP phía trên → «Tạo kịch bản AI» → sửa script giữa màn hình → «Render video sản phẩm»."
                    : "⚠ Chưa render được: " + blockerText;
            }

            void ApplyAffiliateDeep()
            {
                if (lblAffiliateDeepReadiness != null && !lblAffiliateDeepReadiness.IsDisposed)
                {
                    lblAffiliateDeepReadiness.Visible = false;
                    lblAffiliateDeepReadiness.Text = string.Empty;
                }

                if (pnlAffiliateDeepReadinessHost != null && !pnlAffiliateDeepReadinessHost.IsDisposed)
                {
                    pnlAffiliateDeepReadinessHost.Visible = false;
                }
            }

            if (lblSlideshowReadiness?.InvokeRequired == true)
            {
                lblSlideshowReadiness.Invoke(new Action(ApplySlideshow));
            }
            else
            {
                ApplySlideshow();
            }

            if (lblAffiliateDeepReadiness?.InvokeRequired == true)
            {
                lblAffiliateDeepReadiness.Invoke(new Action(ApplyAffiliateDeep));
            }
            else
            {
                ApplyAffiliateDeep();
            }

            UpdateShowcaseRenderButtonState(ffmpegOk, storageOk);
            RefreshProductAdImageReadinessLabel();
        }

        private bool TryGetVideoReupSelectedRow(out VideoReupRowItem row)
        {
            row = null;
            var ordered = GetVideoReupSelectedRowsOrdered();
            if (ordered.Count > 0)
            {
                row = ordered[0];
                return true;
            }

            if (dgvVideoReupInput?.CurrentRow?.DataBoundItem is VideoReupRowItem currentRow)
            {
                row = currentRow;
                return true;
            }

            return false;
        }

        /// <summary>Tất cả dòng đang chọn, sắp theo chỉ số hàng trong lưới (ổn định cho render lô).</summary>
        private List<VideoReupRowItem> GetVideoReupSelectedRowsOrdered()
        {
            var result = new List<VideoReupRowItem>();
            if (dgvVideoReupInput?.SelectedRows == null || dgvVideoReupInput.SelectedRows.Count == 0)
            {
                return result;
            }

            var ordered = dgvVideoReupInput.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => r != null && r.DataBoundItem is VideoReupRowItem)
                .OrderBy(r => r.Index);
            foreach (var gridRow in ordered)
            {
                result.Add((VideoReupRowItem)gridRow.DataBoundItem);
            }

            return result;
        }

        private void FlushVideoReupHookDraftFromEditor()
        {
            try
            {
                dgvVideoReupInput?.EndEdit();
            }
            catch
            {
                // EndEdit có thể ném nếu lưới đang dispose — bỏ qua.
            }
        }

        /// <summary>Chỉ vẽ lại một dòng lưới Video reup (tránh ResetBindings cả bảng trong vòng lặp lô).</summary>
        private void InvalidateVideoReupGridRow(VideoReupRowItem row)
        {
            if (row == null || dgvVideoReupInput == null || dgvVideoReupInput.IsDisposed)
            {
                return;
            }

            void Apply()
            {
                for (var i = 0; i < dgvVideoReupInput.Rows.Count; i++)
                {
                    var gridRow = dgvVideoReupInput.Rows[i];
                    if (gridRow.IsNewRow)
                    {
                        continue;
                    }

                    if (ReferenceEquals(gridRow.DataBoundItem, row))
                    {
                        dgvVideoReupInput.InvalidateRow(i);
                        return;
                    }
                }
            }

            if (dgvVideoReupInput.InvokeRequired)
            {
                dgvVideoReupInput.BeginInvoke((Action)Apply);
            }
            else
            {
                Apply();
            }
        }

        private async Task<AppSettings> GetSettingsSnapshotForVideoReupMusicAsync()
        {
            var s = await _configManager.LoadAsync().ConfigureAwait(true);
            if (txtVideoReupMusicLibraryPath != null)
            {
                txtVideoReupMusicLibraryPath.Text = string.Empty;
            }

            s.VideoReupMusicLibraryPath = string.Empty;

            return s;
        }

        private async Task UpdateVideoReupMusicPathHintAsync()
        {
            if (lblVideoReupMusicPathHint == null)
            {
                return;
            }

            try
            {
                var s = await GetSettingsSnapshotForVideoReupMusicAsync().ConfigureAwait(true);
                var dir = VideoReupRemixService.GetMusicLibraryDirectory(s);
                lblVideoReupMusicPathHint.Text =
                    "Kho nhạc: " + dir +
                    "\r\nCopy file .mp3/.wav/.m4a vào thư mục — app tự cập nhật danh sách khi chọn dòng trên lưới.";
                _tipVideoReupMusicPath ??= new ToolTip { ShowAlways = true };
                var hintTarget = cbVideoReupMusic ?? lblVideoReupMusicPathHint as Control;
                if (hintTarget != null)
                {
                    _tipVideoReupMusicPath.SetToolTip(hintTarget, dir);
                }
            }
            catch
            {
                lblVideoReupMusicPathHint.Text = string.Empty;
            }
        }

        private async void Form1_Activated(object sender, EventArgs e)
        {
            if (!ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen)
                || _selectedAiVideoGenMode != AiVideoGenMode.VideoReup)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastVideoReupMusicAutoRefreshUtc).TotalSeconds < 3)
            {
                return;
            }

            _lastVideoReupMusicAutoRefreshUtc = now;
            await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
        }

        private async Task RefreshVideoReupMusicComboAsync()
        {
            if (cbVideoReupMusic == null)
            {
                return;
            }

            AppSettings settingsSnap;
            try
            {
                settingsSnap = await GetSettingsSnapshotForVideoReupMusicAsync().ConfigureAwait(true);
            }
            catch
            {
                settingsSnap = new AppSettings();
            }

            var dir = VideoReupRemixService.GetMusicLibraryDirectory(settingsSnap);
            VideoReupRemixService.EnsureMusicLibraryDirectoryExists(settingsSnap);
            var musicNames = VideoReupRemixService.ListMusicFileNames(settingsSnap);
            PopulateVideoReupMusicGridColumnItems(musicNames);

            var sfxNames = VideoReupRemixService.ListHookSfxFileNames(settingsSnap);
            PopulateVideoReupHookSfxGridColumnItems(sfxNames);

            var prev = (cbVideoReupMusic.SelectedItem ?? string.Empty).ToString();
            cbVideoReupMusic.Items.Clear();
            foreach (var name in musicNames)
            {
                cbVideoReupMusic.Items.Add(name);
            }

            await UpdateVideoReupMusicPathHintAsync().ConfigureAwait(true);

            if (cbVideoReupMusic.Items.Count == 0)
            {
                cbVideoReupMusic.SelectedIndex = -1;
                cbVideoReupMusic.Text = string.Empty;
                return;
            }

            var pick = !string.IsNullOrWhiteSpace(prev) && cbVideoReupMusic.Items.Contains(prev)
                ? prev
                : null;
            if (pick == null && TryGetVideoReupSelectedRow(out var row) && !string.IsNullOrWhiteSpace(row.ReupSelectedMusicFile) && cbVideoReupMusic.Items.Contains(row.ReupSelectedMusicFile))
            {
                pick = row.ReupSelectedMusicFile;
            }

            if (pick != null)
            {
                cbVideoReupMusic.SelectedItem = pick;
            }
            else if (!string.IsNullOrWhiteSpace(prev) && cbVideoReupMusic.Items.Contains(prev))
            {
                cbVideoReupMusic.SelectedItem = prev;
            }
            else
            {
                cbVideoReupMusic.SelectedIndex = 0;
            }
        }

        private async Task BindVideoReupEditorFromRowAsync(VideoReupRowItem row)
        {
            if (cbVideoReupMusic == null)
            {
                return;
            }

            if (txtVideoReupVideoUrl != null)
            {
                _videoReupSuppressUrlEditorEvents = true;
                try
                {
                    txtVideoReupVideoUrl.Text = row == null ? string.Empty : (row.VideoUrl ?? string.Empty);
                }
                finally
                {
                    _videoReupSuppressUrlEditorEvents = false;
                }
            }

            if (row == null)
            {
                await ApplyVideoReupAudioModeToUiAsync(null).ConfigureAwait(true);
                await RefreshReupPreviewForRowAsync(null).ConfigureAwait(true);
                return;
            }

            await ApplyVideoReupAudioModeToUiAsync(row).ConfigureAwait(true);
            if (row.ReupAudioMode == VideoReupAudioMode.AffiliateBed)
            {
                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(row.ReupSelectedMusicFile) && cbVideoReupMusic.Items.Contains(row.ReupSelectedMusicFile))
                {
                    cbVideoReupMusic.SelectedItem = row.ReupSelectedMusicFile;
                }
                else if (!string.IsNullOrWhiteSpace(row.ReupSuggestedMusicFile) && cbVideoReupMusic.Items.Contains(row.ReupSuggestedMusicFile))
                {
                    cbVideoReupMusic.SelectedItem = row.ReupSuggestedMusicFile;
                    row.ReupSelectedMusicFile = row.ReupSuggestedMusicFile;
                }
                else if (cbVideoReupMusic.Items.Count > 0)
                {
                    cbVideoReupMusic.SelectedIndex = 0;
                    row.ReupSelectedMusicFile = cbVideoReupMusic.SelectedItem?.ToString() ?? string.Empty;
                }
            }

            await RefreshReupPreviewForRowAsync(row).ConfigureAwait(true);
        }

        private async Task ApplyVideoReupAudioModeToUiAsync(VideoReupRowItem row)
        {
            if (grpVideoReupAudioMode == null || rbVideoReupAudioAffiliate == null || rbVideoReupAudioFilm == null)
            {
                return;
            }

            if (row == null)
            {
                grpVideoReupAudioMode.Enabled = false;
                _videoReupSuppressAudioModeEvents = true;
                try
                {
                    rbVideoReupAudioAffiliate.Checked = true;
                }
                finally
                {
                    _videoReupSuppressAudioModeEvents = false;
                }

                if (lblVideoReupMusicPick != null)
                {
                    lblVideoReupMusicPick.Visible = true;
                }

                if (cbVideoReupMusic != null)
                {
                    cbVideoReupMusic.Visible = true;
                }

                if (btnVideoReupOpenMusicFolder != null)
                {
                    btnVideoReupOpenMusicFolder.Visible = true;
                }

                if (lblVideoReupMusicPathHint != null)
                {
                    lblVideoReupMusicPathHint.Visible = true;
                }

                if (pnlReupMusicLibraryPath != null)
                {
                    pnlReupMusicLibraryPath.Visible = false;
                }

                await UpdateVideoReupMusicPathHintAsync().ConfigureAwait(true);
                return;
            }

            grpVideoReupAudioMode.Enabled = true;
            _videoReupSuppressAudioModeEvents = true;
            try
            {
                if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
                {
                    rbVideoReupAudioFilm.Checked = true;
                }
                else
                {
                    rbVideoReupAudioAffiliate.Checked = true;
                }
            }
            finally
            {
                _videoReupSuppressAudioModeEvents = false;
            }

            var film = row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal;
            if (lblVideoReupMusicPick != null)
            {
                lblVideoReupMusicPick.Visible = !film;
            }

            if (cbVideoReupMusic != null)
            {
                cbVideoReupMusic.Visible = !film;
            }

            if (btnVideoReupOpenMusicFolder != null)
            {
                btnVideoReupOpenMusicFolder.Visible = !film;
            }

            if (lblVideoReupMusicPathHint != null)
            {
                lblVideoReupMusicPathHint.Visible = !film;
            }

            if (pnlReupMusicLibraryPath != null)
            {
                pnlReupMusicLibraryPath.Visible = false;
            }

            if (!film)
            {
                await UpdateVideoReupMusicPathHintAsync().ConfigureAwait(true);
            }
        }

        private void SyncVideoReupAudioModeFromUiToRow(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            // Cột «Chế độ» trên lưới là nguồn chính (3 chế độ, gồm Thuyết minh).
            // Panel radio cũ chỉ có Affiliate/Phim — không ghi đè NarrationScript.
            if (row.ReupAudioMode == VideoReupAudioMode.NarrationScript)
            {
                return;
            }

            if (rbVideoReupAudioFilm != null && rbVideoReupAudioFilm.Checked)
            {
                row.ReupAudioMode = VideoReupAudioMode.FilmKeepOriginal;
            }
            else if (rbVideoReupAudioAffiliate != null && rbVideoReupAudioAffiliate.Checked)
            {
                row.ReupAudioMode = VideoReupAudioMode.AffiliateBed;
            }
        }

        private async void VideoReupAudioModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (_videoReupSuppressAudioModeEvents)
            {
                return;
            }

            if (!TryGetVideoReupSelectedRow(out var row))
            {
                return;
            }

            if (rbVideoReupAudioFilm != null && rbVideoReupAudioFilm.Checked)
            {
                row.ReupAudioMode = VideoReupAudioMode.FilmKeepOriginal;
            }
            else if (rbVideoReupAudioAffiliate != null && rbVideoReupAudioAffiliate.Checked)
            {
                row.ReupAudioMode = VideoReupAudioMode.AffiliateBed;
            }

            await ApplyVideoReupAudioModeToUiAsync(row).ConfigureAwait(true);
        }

        private async void dgvVideoReupInput_SelectionChanged(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                await BindVideoReupEditorFromRowAsync(null).ConfigureAwait(true);
                RefreshVideoReupNarrationScriptButtons();
                return;
            }

            await BindVideoReupEditorFromRowAsync(row).ConfigureAwait(true);
            RefreshVideoReupNarrationScriptButtons();
        }

        private void FlushVideoReupVideoUrlFromEditor()
        {
            if (_videoReupSuppressUrlEditorEvents || txtVideoReupVideoUrl == null || !TryGetVideoReupSelectedRow(out var row))
            {
                return;
            }

            var incoming = (txtVideoReupVideoUrl.Text ?? string.Empty).Trim();
            var prev = (row.VideoUrl ?? string.Empty).Trim();
            if (string.Equals(prev, incoming, StringComparison.Ordinal))
            {
                return;
            }

            row.VideoUrl = incoming;
            VideoReupRemixService.ClearReupCachedMediaPaths(row);
        }

        /// <summary>Tự tìm/tải ffmpeg+ffprobe, lưu vào cài đặt nếu thành công.</summary>
        private async Task<bool> TryEnsureVideoReupFfmpegAsync(AppSettings settings, bool saveSettings)
        {
            try
            {
                var path = await VideoReupRemixService.EnsureFfmpegToolkitAsync(
                    settings,
                    LogVideoReup,
                    CancellationToken.None).ConfigureAwait(true);
                if (saveSettings)
                {
                    await _configManager.SaveAsync(settings).ConfigureAwait(true);
                }

                if (!string.IsNullOrWhiteSpace(path) && txtFfmpegPath != null && !txtFfmpegPath.IsDisposed)
                {
                    if (txtFfmpegPath.InvokeRequired)
                    {
                        txtFfmpegPath.Invoke(new Action(() => txtFfmpegPath.Text = path));
                    }
                    else
                    {
                        txtFfmpegPath.Text = path;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogVideoReup("[FFmpeg] " + ex.Message);
                SetVideoReupProgress("lỗi FFmpeg — xem log", 0);
                return false;
            }
        }

        private async Task VideoReupEnsureDownloadWithGateAsync(VideoReupRowItem row, AppSettings settings)
        {
            if (!await TryEnsureVideoReupFfmpegAsync(settings, saveSettings: true).ConfigureAwait(true))
            {
                throw new InvalidOperationException(
                    "Chưa cài được FFmpeg/ffprobe. Cần mạng lần đầu — thử lại hoặc bấm «⬇ Tải FFmpeg» trong Cài đặt.");
            }

            await _videoReupDownloadGate.WaitAsync().ConfigureAwait(true);
            try
            {
                await _videoReupRemixService.EnsureVideoDownloadedAsync(
                    row,
                    settings,
                    _affiliateHunter,
                    LogVideoReup,
                    CancellationToken.None).ConfigureAwait(true);
            }
            finally
            {
                _videoReupDownloadGate.Release();
            }
        }

        private async Task VideoReupDownloadAllImportedRowsAsync()
        {
            var list = _videoReupBindingList?.ToList();
            if (list == null || list.Count == 0)
            {
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                foreach (var row in list)
                {
                    if (!VideoReupRemixService.LooksLikeHttpVideoUrl(row.VideoUrl))
                    {
                        continue;
                    }

                    row.RemixStatus = "Đang tải video…";
                    row.RemixLastError = string.Empty;
                    InvalidateVideoReupGridRow(row);
                    SetVideoReupProgress($"Đang tải «{row.ProductName}»…", 0, indeterminate: true);
                    try
                    {
                        await VideoReupEnsureDownloadWithGateAsync(row, settings).ConfigureAwait(true);
                        row.RemixStatus = "Đã tải video";
                        LogVideoReup($"Video reup: đã tải video nguồn — «{row.ProductName}».");
                    }
                    catch (Exception ex)
                    {
                        row.RemixStatus = "Lỗi";
                        row.RemixLastError = ex.Message;
                        LogVideoReup($"Video reup tải video «{row.ProductName}»: {ex.Message}");
                    }

                    InvalidateVideoReupGridRow(row);
                }
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
                _videoReupBindingList?.ResetBindings();
            }
        }

        private async void VideoReupDownloadImportedRowsFireAndForgetAsync()
        {
            try
            {
                await VideoReupDownloadAllImportedRowsAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogVideoReup("Video reup (tải sau nhập): " + ex.Message);
            }
        }

        private async Task VideoReupCommitUrlThenMaybeDownloadAsync(VideoReupRowItem row)
        {
            if (row == null || !VideoReupRemixService.LooksLikeHttpVideoUrl(row.VideoUrl))
            {
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            row.RemixStatus = "Đang tải video…";
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            SetVideoReupProgress("Đang tải video nguồn (TikWM)…", 0, indeterminate: true);
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                await VideoReupEnsureDownloadWithGateAsync(row, settings).ConfigureAwait(true);
                row.RemixStatus = "Đã tải video";
                LogVideoReup($"Video reup: đã tải video nguồn — «{row.ProductName}».");
                SetVideoReupProgress("Đã tải video nguồn", 100);
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi tải";
                row.RemixLastError = ex.Message;
                LogVideoReup("Video reup tải video: " + ex.Message);
                SetVideoReupProgress("lỗi tải video", 0);
                try
                {
                    var s = await _configManager.LoadAsync().ConfigureAwait(true);
                    LogVideoReup("Gợi ý:\r\n" + VideoReupRemixService.DescribePipelineBlockers(row, s));
                }
                catch
                {
                    // ignored
                }
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
                _videoReupBindingList?.ResetBindings();
            }
        }

        private void DgvVideoReupInput_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (dgvVideoReupInput == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = dgvVideoReupInput.Columns[e.ColumnIndex];
            if (!string.Equals(col.DataPropertyName, "VideoUrl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (dgvVideoReupInput.Rows[e.RowIndex].DataBoundItem is VideoReupRowItem row)
            {
                _videoReupVideoUrlBeforeCellEdit = row.VideoUrl ?? string.Empty;
            }
        }

        private async void DgvVideoReupInput_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvVideoReupInput == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = dgvVideoReupInput.Columns[e.ColumnIndex];
            var prop = col.DataPropertyName ?? string.Empty;
            if (!(dgvVideoReupInput.Rows[e.RowIndex].DataBoundItem is VideoReupRowItem row))
            {
                return;
            }

            if (string.Equals(prop, "ReupHookDraft", StringComparison.OrdinalIgnoreCase))
            {
                row.ReupHookDraft = (row.ReupHookDraft ?? string.Empty).Trim();
                _videoReupBindingList?.ResetBindings();
                return;
            }

            // Khi đổi style hook → xóa cache clip cũ để render lần sau chọn clip mới
            if (string.Equals(prop, nameof(VideoReupRowItem.HookStyleDisplay), StringComparison.OrdinalIgnoreCase))
            {
                row.HookStockClipPath = string.Empty;
                row.ReupHookIntroVideoPath = string.Empty;
                row.ReupHookIntroDurationSec = null;
                if (!string.IsNullOrWhiteSpace(row.HookStyleKey))
                {
                    row.SelectedHookStyleKey = row.HookStyleKey;
                    ApplyReupStyleSelectionsToRow(row);
                }

                _videoReupBindingList?.ResetBindings();
                return;
            }

            if (string.Equals(prop, nameof(VideoReupRowItem.ReupMode), StringComparison.OrdinalIgnoreCase))
            {
                _videoReupBindingList?.ResetBindings();
                RefreshVideoReupNarrationScriptButtons();
                return;
            }

            if (!string.Equals(prop, "VideoUrl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var newUrl = (row.VideoUrl ?? string.Empty).Trim();
            var prev = (_videoReupVideoUrlBeforeCellEdit ?? string.Empty).Trim();
            if (!string.Equals(prev, newUrl, StringComparison.Ordinal))
            {
                VideoReupRemixService.ClearReupCachedMediaPaths(row);
            }

            if (TryGetVideoReupSelectedRow(out var sel) && ReferenceEquals(sel, row) && txtVideoReupVideoUrl != null)
            {
                _videoReupSuppressUrlEditorEvents = true;
                try
                {
                    txtVideoReupVideoUrl.Text = row.VideoUrl ?? string.Empty;
                }
                finally
                {
                    _videoReupSuppressUrlEditorEvents = false;
                }
            }

            _videoReupBindingList?.ResetBindings();

            if (string.Equals(prev, newUrl, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                await VideoReupCommitUrlThenMaybeDownloadAsync(row).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogVideoReup("Video reup (sửa URL trong bảng): " + ex.Message);
            }
        }

        private async void txtVideoReupVideoUrl_Leave(object sender, EventArgs e)
        {
            FlushVideoReupVideoUrlFromEditor();
            _videoReupBindingList?.ResetBindings();
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                return;
            }

            try
            {
                await VideoReupCommitUrlThenMaybeDownloadAsync(row).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogVideoReup("Video reup tải video: " + ex.Message);
            }
        }

        private void btnVideoReupAddManualRow_Click(object sender, EventArgs e)
        {
            if (_videoReupBindingList == null || dgvVideoReupInput == null)
            {
                MessageBox.Show(
                    this,
                    "Bảng Video reup chưa khởi tạo. Hãy đóng app và mở lại.",
                    "Video reup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var n = _videoReupBindingList.Count + 1;
            _videoReupBindingList.Add(new VideoReupRowItem
            {
                ProfileName = GetRunningProfileName(),
                ProductName = "Video " + n,
                Price = "N/A",
                VideoUrl = string.Empty,
                ReupSelectedMusicFile = VideoReupRowItem.NoMusicSelectionLabel,
                ReupSelectedHookSfxFile = VideoReupRowItem.NoHookSfxSelectionLabel
            });

            var idx = _videoReupBindingList.Count - 1;
            if (idx >= 0 && dgvVideoReupInput.Rows.Count > idx)
            {
                dgvVideoReupInput.ClearSelection();
                dgvVideoReupInput.Rows[idx].Selected = true;
                dgvVideoReupInput.FirstDisplayedScrollingRowIndex = idx;
            }

            txtVideoReupVideoUrl?.Focus();
            LogVideoReup("Video reup: đã thêm dòng — nhập URL TikTok trong cột «URL video» (F2) hoặc ô «URL video» bên dưới, rời ô / Enter để tải.");
        }

        private void cbVideoReupMusic_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRow(out var row) || cbVideoReupMusic?.SelectedItem == null)
            {
                return;
            }

            row.ReupSelectedMusicFile = cbVideoReupMusic.SelectedItem.ToString() ?? string.Empty;
        }

        private async void btnVideoReupHookGemini_Click(object sender, EventArgs e)
        {
            await RunVideoReupHookGeminiAsync().ConfigureAwait(true);
        }

        private async void btnVideoReupLyriaHook_Click(object sender, EventArgs e)
        {
            await RunVideoReupLyriaHookAsync().ConfigureAwait(true);
        }

        private void SyncVideoReupMusicFromComboToRow(VideoReupRowItem row)
        {
            if (row == null || row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
            {
                return;
            }

            if (cbVideoReupMusic?.SelectedItem != null)
            {
                row.ReupSelectedMusicFile = cbVideoReupMusic.SelectedItem.ToString() ?? string.Empty;
            }
            else if (cbVideoReupMusic?.Items.Count > 0 && string.IsNullOrWhiteSpace(row.ReupSelectedMusicFile))
            {
                row.ReupSelectedMusicFile = cbVideoReupMusic.Items[0]?.ToString() ?? string.Empty;
            }
        }

        /// <summary>Render lại: xóa MP4 cũ + gỡ URL khỏi render_history (không chặn trùng).</summary>
        private void PrepareVideoReupReRender(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            var hadHistory = _renderHistoryStore.Contains(row.VideoUrl);
            var prevOutput = (row.LastRemixOutputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(prevOutput) && !hadHistory)
            {
                return;
            }

            if (!string.IsNullOrEmpty(prevOutput))
            {
                VideoReupRemixService.TryDeletePreviousOutput(prevOutput, row.ProfileName, LogVideoReup);
                row.LastRemixOutputPath = string.Empty;
                row.LastRemixOutputVideoDurationSec = null;
            }

            if (hadHistory)
            {
                _renderHistoryStore.Remove(row.VideoUrl);
            }

            row.IsProcessed = false;
            row.RemixStatus = string.Empty;
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
        }

        /// <summary>Tự chạy đủ bước: tải → Gemini (nếu thiếu hook) → voiceover → ghép MP4.</summary>
        private async Task<VideoReupRemixResult> RunVideoReupFullPipelineAsync(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            SyncVideoReupAudioModeFromUiToRow(row);

            if (!VideoReupRemixService.LooksLikeHttpVideoUrl(row.VideoUrl))
            {
                throw new InvalidOperationException("URL video không hợp lệ — cần link TikTok http(s).");
            }

            PrepareVideoReupReRender(row);

            if (!await TryEnsureVideoReupFfmpegAsync(settings, saveSettings: true).ConfigureAwait(true))
            {
                throw new InvalidOperationException(
                    "Chưa cài được FFmpeg/ffprobe. Cần mạng lần đầu — thử lại hoặc bấm «⬇ Tải FFmpeg» trong Cài đặt.");
            }

            RefreshVideoOcrService(settings);

            row.RemixStatus = "Bước 1/4: tải video…";
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            SetVideoReupProgress("Bước 1/4: tải video (TikWM)…", 12, indeterminate: true);
            await VideoReupEnsureDownloadWithGateAsync(row, settings).ConfigureAwait(true);
            LogVideoReup("Video reup [1/4]: đã tải video nguồn.");
            SetVideoReupProgress("Bước 1/4: xong", 25);
            await RefreshReupPreviewForRowAsync(row, "Đã tải video nguồn — xem panel bên phải").ConfigureAwait(true);

            ApplyReupVisualHookSettingsToRow(row);
            if (row.UseVisualHookSfx)
            {
                row.RemixStatus = "Bước 2-3/4: Hook SFX 3s…";
                _videoReupBindingList?.ResetBindings();
                SetVideoReupProgress("Bước 2-3/4: chèn Hook SFX 3 giây…", 55, indeterminate: true);
                var ffmpeg = settings.FfmpegPath;

                var stage = row.ReupStageFolder;
                if (string.IsNullOrWhiteSpace(stage))
                {
                    stage = Path.Combine(Path.GetTempPath(), "tiktok_Omni_reup_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(stage);
                    row.ReupStageFolder = stage;
                }

                var hookWav = Path.Combine(stage, "visual_hook_3s.wav");
                await VisualHookService.PrepareThreeSecondHookWavAsync(
                    row.VisualHookSfxPath,
                    hookWav,
                    ffmpeg,
                    LogVideoReup,
                    CancellationToken.None).ConfigureAwait(true);
                row.ReupHookAudioPath = hookWav;
                row.HookAudioPath = row.VisualHookSfxPath;
                row.LastHookDurationUsedSec = VisualHookService.HookDurationSeconds;
                row.ReupHookDraft = string.Empty;
                LogVideoReup("Video reup [2-3/4]: Hook SFX 3s đã sẵn sàng (bỏ qua Gemini + voiceover).");
                SetVideoReupProgress("Bước 2-3/4: xong (SFX)", 75);
            }
            else if (!VideoReupStyleVariants.HasResolvableHook(row))
            {
                if (!VideoReupRemixService.TryValidateHookGeminiStep(row, settings, out var geminiPre))
                {
                    throw new InvalidOperationException(
                        "Chưa có câu hook.\r\n" + geminiPre + "\r\nHoặc gõ hook tay vào cột «Hook» (4–7s) rồi thử lại.");
                }

                row.RemixStatus = "Bước 2/4: Gemini hook…";
                _videoReupBindingList?.ResetBindings();
                SetVideoReupProgress("Bước 2/4: Gemini tạo hook…", 40, indeterminate: true);
                await _videoReupRemixService.GenerateHookAndSuggestMusicAsync(
                    row,
                    settings,
                    _geminiService,
                    _affiliateHunter,
                    LogVideoReup,
                    CancellationToken.None).ConfigureAwait(true);
                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(row.ReupSuggestedMusicFile) && cbVideoReupMusic?.Items.Contains(row.ReupSuggestedMusicFile) == true)
                {
                    cbVideoReupMusic.SelectedItem = row.ReupSuggestedMusicFile;
                    row.ReupSelectedMusicFile = row.ReupSuggestedMusicFile;
                }

                LogVideoReup("Video reup [2/4]: Gemini đã ghi hook.");
                SetVideoReupProgress("Bước 2/4: xong", 50);
            }
            else
            {
                LogVideoReup("Video reup [2/4]: đã có hook — bỏ qua Gemini.");
                SetVideoReupProgress("Bước 2/4: bỏ qua (đã có hook)", 50);
            }

            if (!row.UseVisualHookSfx)
            {
                row.RemixStatus = "Bước 3/4: Voiceover hook…";
                _videoReupBindingList?.ResetBindings();
                SetVideoReupProgress("Bước 3/4: Voiceover đọc hook…", 65, indeterminate: true);
                await _videoReupRemixService.BuildVoiceoverHookAudioAsync(
                    row,
                    settings,
                    _affiliateHunter,
                    LogVideoReup,
                    CancellationToken.None).ConfigureAwait(true);
                LogVideoReup("Video reup [3/4]: Voiceover đã tạo âm thanh hook.");
                SetVideoReupProgress("Bước 3/4: xong", 75);
            }

            if (row.ReupAudioMode == VideoReupAudioMode.NarrationScript)
            {
                if (!VideoReupStyleVariants.HasResolvableScript(row))
                {
                    if (!VideoReupRemixService.TryValidateNarrationScriptGeminiStep(row, settings, out var scriptPre))
                    {
                        throw new InvalidOperationException(
                            "Chưa có script thuyết minh.\r\n" + scriptPre + "\r\nHoặc gõ script vào cột «Script» rồi thử lại.");
                    }

                    row.RemixStatus = "Bước 2b/4: Gemini script…";
                    _videoReupBindingList?.ResetBindings();
                    SetVideoReupProgress("Bước 2b/4: Gemini script thuyết minh…", 58, indeterminate: true);
                    await _videoReupRemixService.GenerateHookAndNarrationBundleAsync(
                        row,
                        settings,
                        _geminiService,
                        _affiliateHunter,
                        LogVideoReup,
                        forceRegenerate: false,
                        CancellationToken.None).ConfigureAwait(true);
                    VideoReupStyleVariants.ApplyActiveSelections(row, settings, LogVideoReup);
                    LogVideoReup("Video reup [2b/4]: đã có script thuyết minh.");
                }

                row.RemixStatus = "Bước 3b/4: Voiceover thuyết minh…";
                _videoReupBindingList?.ResetBindings();
                SetVideoReupProgress("Bước 3b/4: ElevenLabs đọc script…", 72, indeterminate: true);
                await _videoReupRemixService.BuildNarrationVoiceoverAsync(
                    row,
                    settings,
                    LogVideoReup,
                    CancellationToken.None).ConfigureAwait(true);
                LogVideoReup("Video reup [3b/4]: Voiceover thuyết minh xong.");
                SetVideoReupProgress("Bước 3b/4: xong", 78);
            }

            SyncVideoReupMusicFromComboToRow(row);
            if (!VideoReupRemixService.TryValidateFinalRenderStep(row, settings, out var renderPre))
            {
                throw new InvalidOperationException(renderPre);
            }

            row.RemixStatus = "Bước 4/4: render MP4…";
            _videoReupBindingList?.ResetBindings();
            SetVideoReupProgress("Bước 4/4: ghép MP4 (FFmpeg)…", 88, indeterminate: true);
            var result = await _videoReupRemixService.RenderFinalVideoAsync(
                row,
                settings,
                _geminiService,
                LogVideoReup,
                CancellationToken.None).ConfigureAwait(true);
            LogVideoReup("Video reup [4/4]: render xong.");
            SetVideoReupProgress("Hoàn tất", 100);
            row.LastRemixOutputPath = result.OutputPath ?? string.Empty;
            await RefreshReupPreviewForRowAsync(row, "Thành phẩm — bấm «Phát thành phẩm» hoặc double-click khung").ConfigureAwait(true);
            return result;
        }

        private async void btnVideoReupRenderVideo_Click(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                LogVideoReup("Video reup: chọn một dòng trong bảng.");
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            SyncVideoReupAudioModeFromUiToRow(row);
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            LogVideoReup("Video reup — kiểm tra trước khi tạo:\r\n" + VideoReupRemixService.DescribePipelineBlockers(row, settings));
            SetVideoReupProgress("Bắt đầu pipeline 4 bước…", 5);

            SetVideoReupCaptionButtonsEnabled(false);
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            VideoReupRemixService.EnsureMusicLibraryDirectoryExists(settings);
            try
            {
                var result = await RunVideoReupFullPipelineAsync(row, settings).ConfigureAwait(true);
                _renderHistoryStore.AddSuccess(row.VideoUrl);
                row.LastRemixOutputPath = result.OutputPath ?? string.Empty;
                row.LastSourceVideoDurationSec = result.SourceDurationSeconds;
                row.LastRemixOutputVideoDurationSec = result.OutputFileDurationSeconds;
                row.LastHookDurationUsedSec = result.HookDurationSecondsUsed;
                row.RemixStatus = "Xong";
                row.RemixLastError = string.Empty;
                LogVideoReup($"Video reup: thành phẩm → {row.LastRemixOutputPath} (≈{result.OutputFileDurationSeconds:0.##}s).");
                SetVideoReupProgress("Xong — xem cột MP4 remix", 100);
                await RefreshReupPreviewForRowAsync(row, "Render xong — khung hình thành phẩm").ConfigureAwait(true);
                _videoReupBindingList?.ResetBindings();

                var forwardDialog = MessageBox.Show(
                    this,
                    "Video đã Render xong xịn xò! Ông có muốn đẩy thẳng sang Tab Đăng Tự Động luôn không?",
                    "Chuyển tiếp cực mượt",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (forwardDialog == DialogResult.Yes)
                {
                    ForwardToAutoPost(
                        row.LastRemixOutputPath,
                        row.ProductName,
                        row.Hashtags,
                        cbRunningProfile?.SelectedItem?.ToString());
                }
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = ex.Message;
                LogVideoReup("Video reup lỗi: " + ex.Message);
                LogVideoReup("Gợi ý:\r\n" + VideoReupRemixService.DescribePipelineBlockers(row, settings));
                SetVideoReupProgress("lỗi — xem cột Trạng thái và log", 0);
                _videoReupBindingList?.ResetBindings();
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
            }
        }

        private void dgvAffiliateResults_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || dgvAffiliateResults == null)
            {
                return;
            }

            dgvAffiliateResults.ClearSelection();
            dgvAffiliateResults.Rows[e.RowIndex].Selected = true;
            dgvAffiliateResults.CurrentCell = dgvAffiliateResults.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        }

        private void OpenSelectedAffiliateField(Func<AffiliateCandidate, string> picker, string label)
        {
            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                Log("Hãy chọn một dòng trong bảng affiliate trước.");
                return;
            }

            var row = dgvAffiliateResults.SelectedRows[0];
            if (!(row?.DataBoundItem is AffiliateCandidate candidate))
            {
                Log("Không xác định được dòng đang chọn.");
                return;
            }

            var url = (picker(candidate) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                Log($"{label} của dòng này đang trống — không có gì để mở.");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Log("Đã mở: " + url);
            }
            catch (Exception ex)
            {
                Log("Không mở được link: " + ex.Message);
            }
        }

        private async void dgvAffiliateResults_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvAffiliateResults == null)
            {
                return;
            }

            var row = dgvAffiliateResults.Rows[e.RowIndex];
            var column = e.ColumnIndex >= 0 ? dgvAffiliateResults.Columns[e.ColumnIndex] : null;
            if (row?.DataBoundItem is AffiliateCandidate candidate
                && TryGetAffiliateLongTextField(column, candidate, out var title, out var longText))
            {
                ShowAffiliateLongTextPeekDialog(title, longText);
                return;
            }

            await OpenAffiliateRowAsync(row).ConfigureAwait(true);
        }

        private async void dgvAffiliateResults_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvAffiliateResults == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var column = dgvAffiliateResults.Columns[e.ColumnIndex];
            if (column == null)
            {
                return;
            }

            var row = dgvAffiliateResults.Rows[e.RowIndex];
            if (!(row?.DataBoundItem is AffiliateCandidate candidate))
            {
                return;
            }

            if (TryGetAffiliateLongTextField(column, candidate, out var peekTitle, out var peekText))
            {
                ShowAffiliateLongTextPeekDialog(peekTitle, peekText);
                return;
            }

            if (!string.Equals(column.DataPropertyName, "VideoUrl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fullUrl = (candidate.VideoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fullUrl))
            {
                return;
            }

            try
            {
                Clipboard.SetText(fullUrl);
            }
            catch (Exception ex)
            {
                Log("[Affiliate] Không copy được clipboard: " + ex.Message);
                return;
            }

            Log("[Affiliate] Đã copy link video: " + fullUrl);
            var cell = dgvAffiliateResults.Rows[e.RowIndex].Cells[e.ColumnIndex];
            await FlashAffiliateGridCellHighlightAsync(cell).ConfigureAwait(true);
        }

        private static async Task FlashAffiliateGridCellHighlightAsync(DataGridViewCell cell)
        {
            if (cell == null)
            {
                return;
            }

            var grid = cell.DataGridView;
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            var prevBack = cell.Style.BackColor;
            var prevSelBack = cell.Style.SelectionBackColor;
            void apply(Color back, Color sel)
            {
                if (grid.IsDisposed)
                {
                    return;
                }

                cell.Style.BackColor = back;
                cell.Style.SelectionBackColor = sel;
                grid.InvalidateCell(cell.ColumnIndex, cell.RowIndex);
            }

            try
            {
                apply(Color.FromArgb(90, 110, 70), Color.FromArgb(90, 110, 70));
                await Task.Delay(600).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            if (!grid.IsDisposed)
            {
                grid.BeginInvoke(new Action(() =>
                {
                    if (grid.IsDisposed)
                    {
                        return;
                    }

                    cell.Style.BackColor = prevBack;
                    cell.Style.SelectionBackColor = prevSelBack;
                    grid.InvalidateCell(cell.ColumnIndex, cell.RowIndex);
                }));
            }
        }

        private void ShowAffiliateLongTextPeekDialog(string fieldLabel, string content)
        {
            var body = content ?? string.Empty;
            using (var dlg = new Form())
            {
                dlg.Text = fieldLabel + " — xem / sao chép";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.Size = new Size(620, 460);
                dlg.MinimumSize = new Size(420, 300);
                dlg.BackColor = Color.FromArgb(31, 34, 42);
                dlg.ForeColor = Color.Gainsboro;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(12)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

                var tb = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    WordWrap = true,
                    ScrollBars = ScrollBars.Both,
                    Dock = DockStyle.Fill,
                    Text = body,
                    BackColor = Color.FromArgb(22, 24, 30),
                    ForeColor = Color.Gainsboro,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 9.25f)
                };

                var bottom = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(0, 8, 0, 0),
                    WrapContents = false
                };

                var btnClose = new Button
                {
                    Text = "Đóng",
                    AutoSize = true,
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(50, 54, 65),
                    ForeColor = Color.WhiteSmoke,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(8, 4, 0, 4)
                };
                btnClose.FlatAppearance.BorderColor = Color.FromArgb(70, 74, 88);

                var btnCopy = new Button
                {
                    Text = "Sao chép",
                    AutoSize = true,
                    BackColor = Color.FromArgb(50, 54, 65),
                    ForeColor = Color.WhiteSmoke,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(8, 4, 0, 4)
                };
                btnCopy.FlatAppearance.BorderColor = Color.FromArgb(70, 74, 88);

                btnCopy.Click += (_, __) =>
                {
                    try
                    {
                        Clipboard.SetText(tb.Text);
                        Log("[Affiliate] Đã copy «" + fieldLabel + "» vào clipboard.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(dlg, "Không copy được: " + ex.Message, "Clipboard",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };

                bottom.Controls.Add(btnClose);
                bottom.Controls.Add(btnCopy);

                layout.Controls.Add(tb, 0, 0);
                layout.Controls.Add(bottom, 0, 1);

                dlg.Controls.Add(layout);
                dlg.AcceptButton = btnClose;
                dlg.CancelButton = btnClose;

                dlg.Shown += (_, __) => tb.Select(0, 0);
                dlg.ShowDialog(this);
            }
        }

        private void ShowSelectedAffiliateLongTextPeek(Func<AffiliateCandidate, string> picker, string fieldLabel)
        {
            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                Log("Hãy chọn một dòng trong bảng affiliate trước.");
                return;
            }

            var row = dgvAffiliateResults.SelectedRows[0];
            if (!(row?.DataBoundItem is AffiliateCandidate candidate))
            {
                Log("Không xác định được dòng đang chọn.");
                return;
            }

            var text = picker(candidate) ?? string.Empty;
            ShowAffiliateLongTextPeekDialog(fieldLabel, text);
        }

        private static bool TryGetAffiliateLongTextField(DataGridViewColumn column, AffiliateCandidate candidate,
            out string title, out string text)
        {
            title = string.Empty;
            text = string.Empty;
            if (column == null || candidate == null)
            {
                return false;
            }

            var prop = column.DataPropertyName ?? string.Empty;
            if (string.Equals(prop, "SourceKeyword", StringComparison.OrdinalIgnoreCase))
            {
                title = "Từ khoá";
                text = candidate.SourceKeyword ?? string.Empty;
                return true;
            }

            if (string.Equals(prop, "ProductName", StringComparison.OrdinalIgnoreCase))
            {
                title = "Caption";
                text = candidate.ProductName ?? string.Empty;
                return true;
            }

            if (string.Equals(prop, "Hashtags", StringComparison.OrdinalIgnoreCase))
            {
                title = "Hashtag";
                text = candidate.Hashtags ?? string.Empty;
                return true;
            }

            if (string.Equals(prop, "LinkedProduct", StringComparison.OrdinalIgnoreCase))
            {
                title = "Link aff";
                text = candidate.LinkedProduct ?? string.Empty;
                return true;
            }

            if (string.Equals(prop, "VoiceoverTranscript", StringComparison.OrdinalIgnoreCase))
            {
                title = "Lời thoại";
                text = candidate.VoiceoverTranscript ?? string.Empty;
                return true;
            }

            return false;
        }

        private async void btnDownloadSelectedAffiliate_Click(object sender, EventArgs e)
        {
            if (_affiliateDownloadingBatch || dgvAffiliateResults == null || _affiliateHunter == null)
            {
                return;
            }

            var rows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
            {
                if (row?.DataBoundItem is AffiliateCandidate c
                    && !string.IsNullOrWhiteSpace((c.VideoUrl ?? string.Empty).Trim()))
                {
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
            {
                return;
            }

            RefreshAffiliateDownloadFolderHint();
            _affiliateDownloadingBatch = true;
            _affiliateDownloadBatchCts?.Dispose();
            _affiliateDownloadBatchCts = RegisterActiveJobCancellation();
            var token = _affiliateDownloadBatchCts.Token;
            var originalBtnText = btnDownloadSelectedAffiliate.Text;
            btnDownloadSelectedAffiliate.Enabled = true;
            btnHuntAffiliates.Enabled = false;
            btnStopHunt.Enabled = true;

            try
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    btnDownloadSelectedAffiliate.Text = $"⏳ Tải {i + 1}/{rows.Count}...";
                    if (!(rows[i].DataBoundItem is AffiliateCandidate candidate))
                    {
                        continue;
                    }

                    var url = (candidate.VideoUrl ?? string.Empty).Trim();
                    var nick = ProfileScopedPaths.ResolveProfileFromCandidate(candidate);
                    var saveDir = ProfileScopedPaths.GetDownloadsKeywordFolder(
                        candidate,
                        SanitizeAffiliateKeywordForFolder(candidate));
                    Log($"[Download] ({i + 1}/{rows.Count}) nick «{nick}» → {saveDir}");
                    using (var rowCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        rowCts.CancelAfter(TimeSpan.FromMinutes(5));
                        try
                        {
                            var savedPath = await _affiliateHunter
                                .DownloadAffiliateVideoAsync(url, saveDir, Log, rowCts.Token)
                                .ConfigureAwait(true);
                            Log($"[Download] Đã lưu ({i + 1}/{rows.Count}): {savedPath}");
                        }
                        catch (OperationCanceledException)
                        {
                            if (token.IsCancellationRequested)
                            {
                                throw;
                            }

                            Log($"[Download] Timeout dòng {i + 1}/{rows.Count}.");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Download] Lỗi dòng {i + 1}/{rows.Count}: " + ex.Message);
                        }
                    }

                    if (i < rows.Count - 1)
                    {
                        await Task.Delay(600, token).ConfigureAwait(true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log("[Download] Đã hủy tải hàng loạt.");
            }
            finally
            {
                btnDownloadSelectedAffiliate.Text = originalBtnText;
                _affiliateDownloadingBatch = false;
                _affiliateDownloadBatchCts = null;
                DisposeActiveJobCancellation();
                btnHuntAffiliates.Enabled = true;
                btnStopHunt.Enabled = false;
                dgvAffiliateResults_SelectionChanged(dgvAffiliateResults, EventArgs.Empty);
                RefreshAffiliateDownloadFolderHint();
            }
        }

        private static string EllipsisMiddlePath(string path, int maxLen)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLen)
            {
                return path;
            }

            const string ell = "…";
            if (maxLen <= ell.Length + 2)
            {
                return path.Substring(0, maxLen);
            }

            var keep = maxLen - ell.Length;
            var left = keep / 2;
            var right = keep - left;
            return path.Substring(0, left) + ell + path.Substring(path.Length - right);
        }

        /// <summary>Thư mục con của «downloads» theo từ khoá quét (giống nút Tải video).</summary>
        private string GetAffiliateVideoSaveDirectory()
        {
            if (TryGetSingleSelectedAffiliateCandidate(out var selected))
            {
                return ProfileScopedPaths.GetDownloadsKeywordFolder(
                    selected,
                    SanitizeAffiliateKeywordForFolder(selected));
            }

            if (_lastHuntKeywordEntries != null && _lastHuntKeywordEntries.Count == 1)
            {
                var entry = _lastHuntKeywordEntries[0];
                return ProfileScopedPaths.GetDownloadsKeywordFolder(
                    entry.ProfileName,
                    SanitizePathSegment(entry.Keyword) ?? "default");
            }

            var pendingEntries = ParseAffiliateKeywordEntriesFromUi();
            if (pendingEntries.Count == 1)
            {
                var entry = pendingEntries[0];
                return ProfileScopedPaths.GetDownloadsKeywordFolder(
                    entry.ProfileName,
                    SanitizePathSegment(entry.Keyword) ?? "default");
            }

            if (pendingEntries.Count > 1)
            {
                var profile = GetAffiliateHuntProfileFromUi();
                return $"(nhiều từ khoá — lưu dưới Downloads\\{profile}\\<từ_khoá> — chọn 1 hàng để xem đường dẫn cụ thể)";
            }

            return "(chọn Profile + từ khoá trước khi quét — hoặc chọn 1 hàng trên lưới để xem đường dẫn)";
        }

        private bool TryGetSingleSelectedAffiliateCandidate(out AffiliateCandidate candidate)
        {
            candidate = null;
            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count != 1)
            {
                return false;
            }

            if (dgvAffiliateResults.SelectedRows[0]?.DataBoundItem is AffiliateCandidate c)
            {
                candidate = c;
                return true;
            }

            return false;
        }

        private ToolTip _tipAffiliateDownloadPath;

        private void RefreshAffiliateDownloadFolderHint()
        {
            if (lnkAffiliateDownloadFolder == null || lnkAffiliateDownloadFolder.IsDisposed)
            {
                return;
            }

            try
            {
                var dir = GetAffiliateVideoSaveDirectory();
                lnkAffiliateDownloadFolder.Tag = Directory.Exists(dir) || dir.Contains(Path.DirectorySeparatorChar) || dir.Contains(':')
                    ? dir
                    : null;
                var shortPath = dir.StartsWith("(", StringComparison.Ordinal)
                    ? dir
                    : EllipsisMiddlePath(dir, 105);
                lnkAffiliateDownloadFolder.Text = "📁 Thư mục tải video (bấm để mở): " + shortPath;
                if (_tipAffiliateDownloadPath == null)
                {
                    _tipAffiliateDownloadPath = new ToolTip
                    {
                        InitialDelay = 250,
                        AutoPopDelay = 20000,
                        ReshowDelay = 100,
                        ShowAlways = true
                    };
                }

                _tipAffiliateDownloadPath.SetToolTip(lnkAffiliateDownloadFolder, dir);
            }
            catch
            {
                lnkAffiliateDownloadFolder.Text = "📁 Thư mục tải video: (không đọc được đường dẫn)";
                lnkAffiliateDownloadFolder.Tag = null;
            }
        }

        private void LnkAffiliateDownloadFolder_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var dir = (lnkAffiliateDownloadFolder?.Tag as string)?.Trim();
            if (string.IsNullOrWhiteSpace(dir) || dir.StartsWith("(", StringComparison.Ordinal))
            {
                if (!TryGetSingleSelectedAffiliateCandidate(out var selected))
                {
                    Log("[Affiliate] Chọn đúng 1 dòng trên lưới để mở thư mục tải theo nick của dòng đó.");
                    return;
                }

                dir = ProfileScopedPaths.GetDownloadsKeywordFolder(
                    selected,
                    SanitizeAffiliateKeywordForFolder(selected));
            }

            try
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log("[Affiliate] Không mở được thư mục tải: " + ex.Message);
            }
        }

        private static string SanitizePathSegment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }
            return sb.ToString().Trim().TrimEnd('.', ' ');
        }

        private void dgvAffiliateResults_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvAffiliateResults == null)
            {
                return;
            }

            var hasSelection = dgvAffiliateResults.SelectedRows != null
                && dgvAffiliateResults.SelectedRows.Count > 0;

            var videoSelected = 0;
            if (hasSelection)
            {
                foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
                {
                    if (row?.DataBoundItem is AffiliateCandidate c
                        && !string.IsNullOrWhiteSpace((c.VideoUrl ?? string.Empty).Trim()))
                    {
                        videoSelected++;
                    }
                }
            }

            if (btnDownloadSelectedAffiliate != null)
            {
                btnDownloadSelectedAffiliate.Enabled = videoSelected > 0 && !_affiliateDownloadingBatch;
            }

            RefreshAffiliateToolbarButtons();
        }

        private async Task<bool> OpenAffiliateRowAsync(DataGridViewRow row)
        {
            if (!(row?.DataBoundItem is AffiliateCandidate candidate))
            {
                return false;
            }

            var url = (candidate.VideoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Giải pháp 3: gọi TikWM lấy URL MP4 trực tiếp rồi mở trong trình duyệt → trình duyệt
            // phát video MP4 native (không bị TikTok chặn bằng popup "view in app").
            Log("[Open] Đang lấy URL MP4 trực tiếp qua TikWM cho: " + url);
            try
            {
                if (_affiliateHunter == null)
                {
                    Log("[Open] AffiliateHunter chưa sẵn sàng — mở link gốc thay thế.");
                    return OpenUrlInBrowser(url);
                }

                var mp4Url = await _affiliateHunter
                    .GetDirectMp4UrlAsync(url, Log, CancellationToken.None)
                    .ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(mp4Url))
                {
                    Log("[Open] TikWM không trả về URL MP4 — mở link gốc thay thế.");
                    return OpenUrlInBrowser(url);
                }

                if (!OpenUrlInBrowser(mp4Url))
                {
                    return false;
                }

                Log("[Open] Đã mở MP4 native: " + mp4Url);
                return true;
            }
            catch (Exception ex)
            {
                var msg = ex.Message ?? string.Empty;
                if (msg.IndexOf("Slideshow", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("Ảnh trượt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MessageBox.Show(this,
                        "Video này là dạng Ảnh trượt (Slideshow) — không có MP4 để phát trực tiếp.\r\nBạn có thể chọn video khác hoặc mở link gốc trên TikTok app.",
                        "Mở video",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    Log("[Open] Video là Slideshow, không phát được MP4.");
                    return false;
                }

                Log("[Open] TikWM lỗi: " + msg + " → fallback mở link gốc.");
                return OpenUrlInBrowser(url);
            }
        }

        private bool OpenUrlInBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Log("Không mở được link: " + ex.Message);
                return false;
            }
        }

        private void dgvAffiliateResults_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (dgvAffiliateResults?.Columns == null || dgvAffiliateResults.Columns.Count == 0)
            {
                return;
            }

            EnsureAppGridRowHeights(dgvAffiliateResults);

            var anyErr = false;
            if (_affiliateBindingList != null)
            {
                foreach (var c in _affiliateBindingList)
                {
                    if (!string.IsNullOrWhiteSpace(c?.LastDeepDiveError))
                    {
                        anyErr = true;
                        break;
                    }
                }
            }

            _affiliateDeepDiveErrorColumnVisible = anyErr;
            ApplyAffiliateGridColumnPresentation();
        }

        private void dgvAffiliateResults_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvAffiliateResults == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var column = dgvAffiliateResults.Columns[e.ColumnIndex];
            if (column == null)
            {
                return;
            }

            var prop = column.DataPropertyName ?? string.Empty;
            var row = dgvAffiliateResults.Rows[e.RowIndex];
            var candidate = row.DataBoundItem as AffiliateCandidate;
            var errorRow = _affiliateDeepDiveErrorColumnVisible
                && candidate != null
                && !string.IsNullOrWhiteSpace(candidate.LastDeepDiveError);
            if (string.Equals(prop, "ProductName", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "SourceKeyword", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "Hashtags", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "LinkedProduct", StringComparison.OrdinalIgnoreCase))
            {
                var full = e.Value?.ToString() ?? string.Empty;
                row.Cells[e.ColumnIndex].ToolTipText = full;

                int maxPreview;
                if (string.Equals(prop, "SourceKeyword", StringComparison.OrdinalIgnoreCase))
                {
                    maxPreview = AffiliateGridKeywordPreviewChars;
                }
                else if (string.Equals(prop, "Hashtags", StringComparison.OrdinalIgnoreCase))
                {
                    maxPreview = AffiliateGridHashtagPreviewChars;
                }
                else if (string.Equals(prop, "LinkedProduct", StringComparison.OrdinalIgnoreCase))
                {
                    maxPreview = AffiliateGridLinkedProductPreviewChars;
                }
                else
                {
                    maxPreview = AffiliateGridCaptionPreviewChars;
                }

                if (full.Length > maxPreview)
                {
                    e.Value = full.Substring(0, maxPreview) + "…";
                    e.FormattingApplied = true;
                }

                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                }

                return;
            }

            if (string.Equals(prop, "VoiceoverTranscript", StringComparison.OrdinalIgnoreCase))
            {
                var text = e.Value?.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    e.Value = "(Chưa có script — Sinh Script / Deep Dive)";
                    e.FormattingApplied = true;
                    e.CellStyle.ForeColor = Color.Gray;
                    e.CellStyle.Font = new Font(dgvAffiliateResults.Font, FontStyle.Italic);
                    row.Cells[e.ColumnIndex].ToolTipText = "Chưa có lời thoại — chạy Deep Dive để phân tích.";
                }
                else
                {
                    var full = text;
                    row.Cells[e.ColumnIndex].ToolTipText = full;
                    if (full.Length > AffiliateGridVoiceoverPreviewChars)
                    {
                        e.Value = full.Substring(0, AffiliateGridVoiceoverPreviewChars) + "…";
                        e.FormattingApplied = true;
                    }

                    e.CellStyle.Font = dgvAffiliateResults.Font;
                    e.CellStyle.ForeColor = Color.LightGray;
                }

                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                }

                return;
            }

            if (string.Equals(prop, "VideoUrl", StringComparison.OrdinalIgnoreCase))
            {
                var full = (e.Value as string ?? string.Empty).Trim();
                row.Cells[e.ColumnIndex].ToolTipText = full;
                if (full.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    const string marker = "/video/";
                    var idx = full.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length;
                    var id = idx >= 0 && idx <= full.Length
                        ? full.Substring(idx).Split('?')[0]
                        : full;
                    e.Value = id;
                    e.FormattingApplied = true;
                }

                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                }

                return;
            }

            if (string.Equals(prop, "SafetyScore", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (e.Value != null)
                {
                    int.TryParse(e.Value.ToString(), out score);
                }

                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                    return;
                }

                if (score >= 85)
                {
                    e.CellStyle.BackColor = Color.FromArgb(50, 110, 68);
                    e.CellStyle.ForeColor = Color.White;
                }
                else if (score >= 70)
                {
                    e.CellStyle.BackColor = Color.FromArgb(133, 106, 42);
                    e.CellStyle.ForeColor = Color.White;
                }
                else
                {
                    e.CellStyle.BackColor = Color.FromArgb(133, 64, 64);
                    e.CellStyle.ForeColor = Color.White;
                }

                return;
            }

            if (string.Equals(prop, "PlayCount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "LikeCount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "CommentCount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "ShareCount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop, "CollectCount", StringComparison.OrdinalIgnoreCase))
            {
                long n = 0;
                if (e.Value != null) long.TryParse(e.Value.ToString(), out n);
                var pendingMetrics = candidate != null
                                     && candidate.MetricsCapturedAtUtc == DateTime.MinValue
                                     && string.IsNullOrWhiteSpace(candidate.LastMetricsError);
                var metricsFailed = candidate != null && !string.IsNullOrWhiteSpace(candidate.LastMetricsError);

                if (n <= 0 && pendingMetrics && (_affiliateAutoEnrichRunning || _huntCancellation != null))
                {
                    e.Value = "⏳";
                    e.CellStyle.ForeColor = Color.FromArgb(150, 170, 210);
                    row.Cells[e.ColumnIndex].ToolTipText = "Đang lấy số liệu TikWM…";
                }
                else if (n <= 0 && metricsFailed)
                {
                    e.Value = "!";
                    e.CellStyle.ForeColor = Color.FromArgb(255, 170, 120);
                    row.Cells[e.ColumnIndex].ToolTipText = candidate.LastMetricsError;
                }
                else if (n <= 0 && (candidate == null || candidate.MetricsCapturedAtUtc == DateTime.MinValue))
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = Color.DimGray;
                    row.Cells[e.ColumnIndex].ToolTipText = "Chưa có số liệu — bật «Tự động enrich» hoặc chuột phải → Refresh số liệu.";
                }
                else
                {
                    e.Value = FormatCountCompact(n);
                }
                e.FormattingApplied = true;
                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                }
                return;
            }

            if (string.Equals(prop, "DurationSeconds", StringComparison.OrdinalIgnoreCase))
            {
                int sec = 0;
                if (e.Value != null) int.TryParse(e.Value.ToString(), out sec);
                if (sec <= 0)
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = Color.DimGray;
                }
                else
                {
                    e.Value = FormatDurationShort(sec);
                }
                e.FormattingApplied = true;
                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                }
                return;
            }

            if (string.Equals(prop, "CreateTimeUtc", StringComparison.OrdinalIgnoreCase))
            {
                DateTime dt = DateTime.MinValue;
                if (e.Value is DateTime d) dt = d;
                else if (e.Value != null) DateTime.TryParse(e.Value.ToString(), out dt);

                if (dt == DateTime.MinValue)
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = Color.DimGray;
                    if (candidate != null)
                    {
                        row.Cells[e.ColumnIndex].ToolTipText = "Bấm «Lấy số liệu» để fetch metrics.";
                    }
                }
                else
                {
                    var local = dt.ToLocalTime();
                    e.Value = FormatRelativeTimeVi(local);
                    row.Cells[e.ColumnIndex].ToolTipText = local.ToString("dd/MM/yyyy HH:mm");
                }
                e.FormattingApplied = true;
                if (errorRow)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                    e.CellStyle.ForeColor = Color.WhiteSmoke;
                }
                return;
            }

            if (errorRow)
            {
                e.CellStyle.BackColor = Color.FromArgb(80, 35, 35);
                e.CellStyle.ForeColor = Color.WhiteSmoke;
            }
        }

        private static string FormatCountCompact(long n)
        {
            if (n < 0) n = 0;
            if (n < 1_000) return n.ToString("N0");
            if (n < 1_000_000) return (n / 1000.0).ToString("0.#") + "K";
            if (n < 1_000_000_000) return (n / 1_000_000.0).ToString("0.#") + "M";
            return (n / 1_000_000_000.0).ToString("0.#") + "B";
        }

        private static string FormatDurationShort(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1
                ? string.Format("{0:D1}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds)
                : string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        private static string FormatRelativeTimeVi(DateTime localTime)
        {
            var diff = DateTime.Now - localTime;
            if (diff.TotalSeconds < 0) return localTime.ToString("dd/MM HH:mm");
            if (diff.TotalMinutes < 1) return "vừa xong";
            if (diff.TotalMinutes < 60) return ((int)diff.TotalMinutes) + " phút trước";
            if (diff.TotalHours < 24) return ((int)diff.TotalHours) + "h trước";
            if (diff.TotalDays < 7) return ((int)diff.TotalDays) + " ngày trước";
            if (diff.TotalDays < 30) return ((int)(diff.TotalDays / 7)) + " tuần trước";
            if (diff.TotalDays < 365) return ((int)(diff.TotalDays / 30)) + " tháng trước";
            return ((int)(diff.TotalDays / 365)) + " năm trước";
        }

        private void RefreshAffiliateGridByQualityFilter()
        {
            if (_affiliateBindingList == null)
            {
                return;
            }

            _affiliateBindingList.Clear();
            var source = _affiliateAllResults ?? new List<AffiliateCandidate>();
            var onlyHigh = chkAffiliateOnlyHighQuality?.Checked ?? false;
            const int threshold = 75;
            List<AffiliateCandidate> rows;
            if (onlyHigh)
            {
                rows = source
                    .Where(x =>
                        x != null &&
                        x.SafetyScore >= threshold &&
                        !string.IsNullOrWhiteSpace(x.ImageUrl) &&
                        !string.IsNullOrWhiteSpace(x.Price) &&
                        !string.Equals(x.Price.Trim(), "N/A", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                rows = source.Where(x => x != null).ToList();
            }

            var huntProfileScope = GetAffiliateHuntProfileFromUi();
            var gridProfileFilter = !string.IsNullOrWhiteSpace(huntProfileScope)
                ? huntProfileScope
                : _affiliateGridProfileScope;
            if (!string.IsNullOrWhiteSpace(gridProfileFilter))
            {
                rows = rows
                    .Where(x => string.Equals(
                        ProfileScopedPaths.ResolveProfileName(x.ProfileName),
                        gridProfileFilter,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            SortAffiliateCandidatesByViewsDescending(rows);

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                {
                    rows[i].OrderNumber = i + 1;
                }
            }

            foreach (var item in rows)
            {
                _affiliateBindingList.Add(item);
            }

            if (rows.Count == 0 && source.Count > 0 && !string.IsNullOrWhiteSpace(gridProfileFilter))
            {
                Log($"[Affiliate] Có {source.Count} dòng trong bộ nhớ nhưng không khớp profile lưới «{gridProfileFilter}» — chọn đúng Profile trên dòng trên cùng.");
            }
        }

        /// <summary>Lưới affiliate: view cao nhất lên trên; chưa có metrics (0 view) xuống dưới.</summary>
        private static void SortAffiliateCandidatesByViewsDescending(List<AffiliateCandidate> rows)
        {
            if (rows == null || rows.Count <= 1)
            {
                return;
            }

            rows.Sort((a, b) =>
            {
                var va = a?.PlayCount ?? 0L;
                var vb = b?.PlayCount ?? 0L;
                if (vb != va)
                {
                    return vb.CompareTo(va);
                }

                var sa = a?.SafetyScore ?? 0;
                var sb = b?.SafetyScore ?? 0;
                if (sb != sa)
                {
                    return sb.CompareTo(sa);
                }

                var la = a?.LikeCount ?? 0L;
                var lb = b?.LikeCount ?? 0L;
                return lb.CompareTo(la);
            });
        }



        private async void btnGenerateGeminiPrompt_Click(object sender, EventArgs e)
        {
            var slideshowItems = GetSlideshowItemsForRender();
            if (slideshowItems.Count == 0)
            {
                Log("AI Video Gen: chưa có dữ liệu sản phẩm. Hãy bấm 'Đẩy sang AI Video Gen' từ tab Affiliate Hunter.");
                return;
            }

            btnGenerateGeminiPrompt.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync();
                if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    Log("AI Video Gen: thiếu AI API key trong Setting.");
                    return;
                }

                var gemini = new GeminiService();
                var script = await gemini.GenerateAffiliateExperienceScriptAsync(
                    "slideshow",
                    slideshowItems,
                    slideshowItems[0],
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None,
                    GetSelectedGeminiStyleTemplate()).ConfigureAwait(true);

                txtAiVideoGenPrompt.Text = script ?? string.Empty;
                Log("AI Video Gen: đã tạo prompt/script bằng Gemini.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen thất bại: " + ex.Message);
            }
            finally
            {
                btnGenerateGeminiPrompt.Enabled = true;
            }
        }

        private void btnBrowseAutoPostFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Chọn thư mục chứa video để đăng TikTok";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtAutoPostFolder.Text = dialog.SelectedPath;
                    RefreshAutoPostVideoCombo();
                }
            }
        }

        private async void btnStartAutoPost_Click(object sender, EventArgs e)
        {
            ClearAutoPostValidationHighlights();
            if (!ValidateAutoPostContext(out var contextError))
            {
                Log("Auto Post: " + contextError);
                ApplyAutoPostValidationHighlights(CollectAutoPostValidationIssues());
                if (!string.IsNullOrWhiteSpace(contextError))
                {
                    MessageBox.Show(this, contextError, "Đăng đa kênh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            if (!TryBuildOmnichannelAutoPostPlan(out var plan, out var planError))
            {
                Log("Auto Post: " + planError);
                if (!string.IsNullOrWhiteSpace(planError))
                {
                    MessageBox.Show(this, planError, "Đăng đa kênh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return;
            }

            if (await _duplicateGuardManager.ExistsRecentAsync("post", plan.PostFingerprint, TimeSpan.FromDays(7)).ConfigureAwait(true))
            {
                Log("[SAFEGUARD] Phát hiện nội dung Auto Post trùng trong 7 ngày gần đây. Đã chặn để tránh spam.");
                return;
            }

            var policySettings = await _configManager.LoadAsync().ConfigureAwait(true);
            var postRisk = _safetyScoreService.ScoreAutoPost(
                plan.CombinedCaptionPreview,
                plan.TikTokHashtags,
                plan.VideoFolder,
                plan.Profile);
            var forcePrePostApproval = policySettings.AlwaysRequirePrePostApproval ?? true;
            var postScoreThreshold = policySettings.BlockPostingSafetyScoreBelow;
            var mustApprovePost = forcePrePostApproval || postRisk.Score < postScoreThreshold || postRisk.RequiresManualApproval;
            if (mustApprovePost)
            {
                await EnqueueApprovalItemAsync(new ApprovalQueueItem
                {
                    JobType = ApprovalJobType.AutoPost,
                    Status = ApprovalStatus.Pending,
                    Profile = plan.Profile,
                    Title = "Auto Post đa kênh - Requires Approval",
                    PayloadJson = JsonConvert.SerializeObject(new AutoPostApprovalPayload
                    {
                        VideoFolder = plan.VideoFolder,
                        Hashtags = plan.TikTokHashtags,
                        Profile = ProfileScopedPaths.ResolveProfileName(plan.Profile),
                        ProfileName = ProfileScopedPaths.ResolveProfileName(plan.Profile),
                        VideoFilePath = plan.VideoFilePath,
                        CaptionFull = plan.TikTokCaption,
                        UploadOnlyNoPublish = plan.TikTokUploadOnly,
                        PostTikTok = plan.PostTikTok,
                        PostFacebook = plan.PostFacebook,
                        PostYouTube = plan.PostYouTube,
                        FacebookCaption = plan.FacebookCaption,
                        FacebookHashtags = plan.FacebookHashtags,
                        FacebookAttachShopeeLink = plan.FacebookAttachShopeeLink,
                        FacebookShopeeLink = OmnichannelAutoPostFields.NormalizeLink(plan.FacebookShopeeLink),
                        YouTubeTitle = plan.YouTubeTitle,
                        YouTubeDescription = plan.YouTubeDescription,
                        AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                        ProductId = OmnichannelAutoPostFields.NormalizeLink(plan.ProductId)
                    }),
                    SafetyScore = postRisk.Score,
                    RequiresManualApproval = true,
                    RiskReasons = string.Join("; ", postRisk.Reasons),
                    OriginalPreview = plan.CombinedCaptionPreview,
                    CanAttachAffiliate = false,
                    TargetAffiliateLink = OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                    TargetProductId = OmnichannelAutoPostFields.NormalizeLink(plan.ProductId),
                    AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                    ProductId = OmnichannelAutoPostFields.NormalizeLink(plan.ProductId)
                }).ConfigureAwait(true);
                Log($"[APPROVAL] Auto Post đa kênh đã đưa vào hàng duyệt (score {postRisk.Score}).");
                if (policySettings.AutoRunApprovedQueue ?? false)
                {
                    await AutoRunApprovedQueueItemsAsync().ConfigureAwait(true);
                }
                else
                {
                    btnOpenApprovalQueue_Click(this, EventArgs.Empty);
                }

                return;
            }

            btnStartAutoPost.Enabled = false;
            _autoPostCancellation?.Dispose();
            _autoPostCancellation = new CancellationTokenSource();

            var policyForPost = await _configManager.LoadAsync().ConfigureAwait(true);
            var postPayload = new AutoPostJobPayload
            {
                VideoFolder = plan.VideoFolder,
                VideoFilePath = plan.VideoFilePath,
                Profile = plan.Profile,
                PostTikTok = plan.PostTikTok,
                PostFacebook = plan.PostFacebook,
                PostYouTube = plan.PostYouTube,
                TikTokCaption = plan.TikTokCaption,
                TikTokHashtags = plan.TikTokHashtags,
                TikTokUploadOnly = plan.TikTokUploadOnly,
                FacebookCaption = plan.FacebookCaption,
                FacebookHashtags = plan.FacebookHashtags,
                FacebookAttachShopeeLink = plan.FacebookAttachShopeeLink,
                FacebookShopeeLink = OmnichannelAutoPostFields.NormalizeLink(plan.FacebookShopeeLink),
                YouTubeTitle = plan.YouTubeTitle,
                YouTubeDescription = plan.YouTubeDescription,
                PostFingerprint = plan.PostFingerprint,
                CombinedCaptionPreview = plan.CombinedCaptionPreview,
                StorageRootPath = policyForPost.StorageRootPath ?? string.Empty,
                VideoTypeFolder = GetSelectedAutoPostVideoType().ToString(),
                AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                ProductId = OmnichannelAutoPostFields.NormalizeLink(plan.ProductId)
            };

            var postJob = new OmniJob
            {
                Kind = OmniJobKind.AutoPost,
                Title = "Auto Post đa kênh",
                ProfileName = ProfileScopedPaths.ResolveProfileName(plan.Profile),
                PayloadJson = JsonConvert.SerializeObject(postPayload),
                MaxRetries = 2,
                Tag = _autoPostCancellation
            };
            OmniJobMetadataHelper.ApplyAffiliateFields(postJob, plan.AffiliateLink, plan.ProductId);
            if (_autoPostInboxJob != null)
            {
                OmniJobMetadataHelper.CopyAffiliateFields(_autoPostInboxJob, postJob);
            }
            _activeAutoPostJob = postJob;
            _globalJobQueue.Enqueue(postJob);
            Log("[JobQueue] Auto Post đã vào hàng đợi.");
        }

        private bool TryBuildOmnichannelAutoPostPlan(out OmnichannelAutoPostPlan plan, out string errorMessage)
        {
            plan = null;
            errorMessage = string.Empty;

            var postTikTok = chkAutoPostEnableTikTok?.Checked ?? false;
            var postFacebook = chkAutoPostEnableFacebook?.Checked ?? false;
            var postYouTube = chkAutoPostEnableYouTube?.Checked ?? false;
            if (!postTikTok && !postFacebook && !postYouTube)
            {
                errorMessage = "Chưa bật kênh nào — tick «Bật đăng lên kênh này» ở TikTok, Facebook hoặc YouTube.";
                return false;
            }

            var videoFolder = (txtAutoPostFolder?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            {
                errorMessage = "Thư mục video không hợp lệ.";
                return false;
            }

            var explicitVideoPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(explicitVideoPath) &&
                !EnumerateVideoFilesInFolder(videoFolder).Any())
            {
                errorMessage = "Không có file video (.mp4, .mov, …) trong thư mục.";
                return false;
            }

            if (chkAutoPostVideoApproved != null && !chkAutoPostVideoApproved.Checked)
            {
                errorMessage = "Hãy xem video và tick «Tôi đã xem và duyệt video» trước khi đăng.";
                return false;
            }

            var tikTokCaption = BuildFinalAutoPostCaptionBody(
                txtAutoPostCaption?.Text ?? string.Empty,
                txtAutoPostHashtags?.Text ?? string.Empty);
            var facebookCaption = BuildFinalAutoPostCaptionBody(
                txtAutoPostFbCaption?.Text ?? string.Empty,
                txtAutoPostFbHashtags?.Text ?? string.Empty);
            var youtubeTitle = (txtAutoPostYtTitle?.Text ?? string.Empty).Trim();
            var youtubeDescription = (txtAutoPostYtDescription?.Text ?? string.Empty).Trim();

            if (postTikTok && string.IsNullOrWhiteSpace(tikTokCaption))
            {
                errorMessage = "TikTok: chưa có caption — tạo bằng Gemini hoặc nhập tay.";
                return false;
            }

            if (postFacebook && string.IsNullOrWhiteSpace(facebookCaption))
            {
                errorMessage = "Facebook: chưa có caption — nhập hoặc dùng AI viết caption FB.";
                return false;
            }

            if (postYouTube && string.IsNullOrWhiteSpace(youtubeTitle))
            {
                errorMessage = "YouTube: chưa có tiêu đề ngắn (< 60 ký tự).";
                return false;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(cbAutoPostProfile?.SelectedItem?.ToString());
            try
            {
                OneClickPipelineService.ValidateAutoPostInboxOnly(
                    _storageRootPathCache,
                    profile,
                    videoFolder,
                    explicitVideoPath);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }

            var tikTokHashtags = (txtAutoPostHashtags?.Text ?? string.Empty).Trim();
            var facebookHashtags = (txtAutoPostFbHashtags?.Text ?? string.Empty).Trim();
            var captionDraft = (txtAutoPostCaption?.Text ?? string.Empty).Trim();

            var previewParts = new List<string>();
            if (postTikTok)
            {
                previewParts.Add("[TikTok] " + BuildAutoPostCaptionPreview(videoFolder, tikTokHashtags, explicitVideoPath, captionDraft));
            }

            if (postFacebook)
            {
                previewParts.Add("[Facebook] " + facebookCaption);
            }

            if (postYouTube)
            {
                previewParts.Add("[YouTube] " + youtubeTitle + " | " + youtubeDescription);
            }

            var combinedPreview = string.Join(" || ", previewParts);
            var fingerprintSource = BuildAutoPostFingerprintSource(
                videoFolder,
                combinedPreview,
                tikTokHashtags + "|" + facebookHashtags + "|" + youtubeTitle,
                profile,
                explicitVideoPath) +
                "|tiktok=" + postTikTok +
                "|facebook=" + postFacebook +
                "|youtube=" + postYouTube;

            plan = new OmnichannelAutoPostPlan
            {
                VideoFolder = videoFolder,
                VideoFilePath = explicitVideoPath,
                Profile = profile,
                PostTikTok = postTikTok,
                PostFacebook = postFacebook,
                PostYouTube = postYouTube,
                TikTokCaption = tikTokCaption,
                TikTokHashtags = tikTokHashtags,
                TikTokUploadOnly = chkAutoPostUploadOnly?.Checked ?? false,
                FacebookCaption = facebookCaption,
                FacebookHashtags = facebookHashtags,
                FacebookAttachShopeeLink = ResolveAutoPostFacebookAttachShopeeForPlan(),
                FacebookShopeeLink = ResolveAutoPostFacebookShopeeLinkForPlan(),
                YouTubeTitle = youtubeTitle,
                YouTubeDescription = youtubeDescription,
                CombinedCaptionPreview = combinedPreview,
                PostFingerprint = DuplicateGuardManager.ComputeSha256Fingerprint(fingerprintSource),
                AffiliateLink = ResolveAutoPostAffiliateLinkForPlan(),
                ProductId = ResolveAutoPostProductIdForPlan()
            };

            return true;
        }

        private string ResolveAutoPostAffiliateLinkForPlan()
        {
            if (chkEnableAffiliateLink != null && !chkEnableAffiliateLink.Checked)
            {
                return string.Empty;
            }

            var manualLink = OmnichannelAutoPostFields.NormalizeLink(txtAutoPostAffiliateLink?.Text);
            if (!string.IsNullOrWhiteSpace(manualLink))
            {
                return manualLink;
            }

            return OmnichannelAutoPostFields.NormalizeLink(_autoPostInboxJob?.AffiliateLink);
        }

        private string ResolveAutoPostProductIdForPlan()
        {
            if (chkEnableAffiliateLink != null && !chkEnableAffiliateLink.Checked)
            {
                return string.Empty;
            }

            return OmnichannelAutoPostFields.NormalizeLink(_autoPostInboxJob?.ProductId);
        }

        private bool ResolveAutoPostFacebookAttachShopeeForPlan()
        {
            return chkAutoPostFbAttachShopee?.Checked ?? false;
        }

        private string ResolveAutoPostFacebookShopeeLinkForPlan()
        {
            if (chkAutoPostFbAttachShopee == null || !chkAutoPostFbAttachShopee.Checked)
            {
                return string.Empty;
            }

            var fbLink = OmnichannelAutoPostFields.NormalizeLink(txtAutoPostFbShopeeLink?.Text);
            if (OmnichannelAutoPostFields.IsShopeeProductUrl(fbLink))
            {
                return fbLink;
            }

            var tikTokLink = ResolveAutoPostAffiliateLinkForPlan();
            if (OmnichannelAutoPostFields.IsShopeeProductUrl(tikTokLink))
            {
                return tikTokLink;
            }

            return string.Empty;
        }

        private async Task<string> RunOmnichannelAutoPostSequenceAsync(OmnichannelAutoPostPlan plan, CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var payload = new AutoPostJobPayload
            {
                VideoFolder = plan.VideoFolder,
                VideoFilePath = plan.VideoFilePath,
                Profile = plan.Profile,
                PostTikTok = plan.PostTikTok,
                PostFacebook = plan.PostFacebook,
                PostYouTube = plan.PostYouTube,
                TikTokCaption = plan.TikTokCaption,
                TikTokHashtags = plan.TikTokHashtags,
                TikTokUploadOnly = plan.TikTokUploadOnly,
                FacebookCaption = plan.FacebookCaption,
                FacebookHashtags = plan.FacebookHashtags,
                FacebookAttachShopeeLink = plan.FacebookAttachShopeeLink,
                FacebookShopeeLink = OmnichannelAutoPostFields.NormalizeLink(plan.FacebookShopeeLink),
                YouTubeTitle = plan.YouTubeTitle,
                YouTubeDescription = plan.YouTubeDescription,
                PostFingerprint = plan.PostFingerprint,
                CombinedCaptionPreview = plan.CombinedCaptionPreview,
                StorageRootPath = settings.StorageRootPath ?? string.Empty,
                AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                ProductId = OmnichannelAutoPostFields.NormalizeLink(plan.ProductId)
            };

            if (chkEnableAffiliateLink != null && !chkEnableAffiliateLink.Checked)
            {
                payload.AffiliateLink = string.Empty;
                payload.ProductId = string.Empty;
            }

            return await _socialAutomation.RunOmnichannelAutoPostAsync(payload, cancellationToken, Log)
                .ConfigureAwait(false);
        }

        private async void btnGenerateGeminiCaption_Click(object sender, EventArgs e)
        {
            var explicitPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(explicitPath) || !File.Exists(explicitPath))
            {
                Log("Caption Gemini: chọn file video trong danh sách trước.");
                return;
            }

            var settings = await _configManager.LoadAsync();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                Log("Caption Gemini: chưa cấu hình AI API Key trong Cài đặt.");
                return;
            }

            btnGenerateGeminiCaption.Enabled = false;
            try
            {
                Log("Caption Gemini: đang tạo caption...");
                var styleKey = GetSelectedAutoPostCaptionStyleKey();
                var text = await _geminiService.GenerateOmnichannelCaptionAsync(
                    explicitPath,
                    styleKey,
                    "TikTok",
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None).ConfigureAwait(true);

                if (txtAutoPostCaption != null)
                {
                    txtAutoPostCaption.Text = text ?? string.Empty;
                }

                Log(string.IsNullOrWhiteSpace(text)
                    ? "Caption Gemini: kết quả trống."
                    : "Caption Gemini: đã điền vào ô caption — hãy chỉnh sửa nếu cần.");
            }
            catch (Exception ex)
            {
                Log("Caption Gemini lỗi: " + ex.Message);
            }
            finally
            {
                btnGenerateGeminiCaption.Enabled = true;
            }
        }

        private async void btnGenerateGeminiCaptionFb_Click(object sender, EventArgs e)
        {
            var explicitPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(explicitPath) || !File.Exists(explicitPath))
            {
                Log("Caption Facebook: chọn file video trong danh sách trước.");
                return;
            }

            var settings = await _configManager.LoadAsync();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                Log("Caption Facebook: chưa cấu hình AI API Key trong Cài đặt.");
                return;
            }

            btnGenerateGeminiCaptionFb.Enabled = false;
            try
            {
                Log("Caption Facebook (Gemini): đang tạo...");
                var styleKey = GetSelectedAutoPostCaptionStyleKey();
                var text = await _geminiService.GenerateOmnichannelCaptionAsync(
                    explicitPath,
                    styleKey,
                    "Facebook",
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None).ConfigureAwait(true);

                if (txtAutoPostFbCaption != null)
                {
                    txtAutoPostFbCaption.Text = text ?? string.Empty;
                }

                Log(string.IsNullOrWhiteSpace(text)
                    ? "Caption Facebook: kết quả trống."
                    : "Caption Facebook: đã điền — chỉnh lại cho phù hợp Reels/Page nếu cần.");
            }
            catch (Exception ex)
            {
                Log("Caption Facebook lỗi: " + ex.Message);
            }
            finally
            {
                btnGenerateGeminiCaptionFb.Enabled = true;
            }
        }

        private async void btnGenerateGeminiCaptionYoutube_Click(object sender, EventArgs e)
        {
            var explicitPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(explicitPath) || !File.Exists(explicitPath))
            {
                Log("YouTube SEO: chọn file video trong danh sách trước.");
                return;
            }

            var settings = await _configManager.LoadAsync();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                Log("YouTube SEO: chưa cấu hình AI API Key trong Cài đặt.");
                return;
            }

            btnGenerateGeminiCaptionYoutube.Enabled = false;
            try
            {
                Log("YouTube SEO (Gemini): đang tạo tiêu đề & mô tả...");
                var styleKey = GetSelectedAutoPostCaptionStyleKey();
                var text = await _geminiService.GenerateOmnichannelCaptionAsync(
                    explicitPath,
                    styleKey,
                    "YouTube",
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None).ConfigureAwait(true);

                ApplyYoutubeShortsMetadataFromGemini(text);
                Log(string.IsNullOrWhiteSpace(text)
                    ? "YouTube SEO: kết quả trống."
                    : "YouTube SEO: đã tách tiêu đề (<60 ký tự) và mô tả — hãy chỉnh sửa.");
            }
            catch (Exception ex)
            {
                Log("YouTube SEO lỗi: " + ex.Message);
            }
            finally
            {
                btnGenerateGeminiCaptionYoutube.Enabled = true;
            }
        }

        private void ApplyYoutubeShortsMetadataFromGemini(string raw)
        {
            if (txtAutoPostYtTitle == null || txtAutoPostYtDescription == null)
            {
                return;
            }

            var normalized = (raw ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                txtAutoPostYtTitle.Clear();
                txtAutoPostYtDescription.Clear();
                return;
            }

            var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var titleSource = lines.Length > 0 ? lines[0].Trim() : normalized;
            if (titleSource.Length > 60)
            {
                titleSource = titleSource.Substring(0, 60).TrimEnd();
            }

            txtAutoPostYtTitle.Text = titleSource;
            txtAutoPostYtDescription.Text = lines.Length > 1
                ? string.Join(Environment.NewLine, lines.Skip(1).Select(l => l.Trim()))
                : normalized;
        }

        private void btnPreviewAutoPostVideo_Click(object sender, EventArgs e)
        {
            var path = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Log("Xem video: chọn file video trong danh sách.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Log("Đã mở video bằng ứng dụng mặc định: " + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                Log("Không mở được video: " + ex.Message);
            }
        }

        private async void btnCloseAutoPostBrowser_Click(object sender, EventArgs e)
        {
            btnCloseAutoPostBrowser.Enabled = false;
            try
            {
                await WithBrowserLockAsync(
                    _ => _tikTokAutomation.CloseAutoPostBrowserAsync(Log),
                    CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("Đóng trình duyệt Auto Post thất bại: " + ex.Message);
            }
            finally
            {
                btnCloseAutoPostBrowser.Enabled = true;
            }
        }

        private void btnCopyAiVideoPrompt_Click(object sender, EventArgs e)
        {
            var text = txtAiVideoGenPrompt?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Log("AI Video Gen: chưa có prompt để copy.");
                return;
            }

            try
            {
                Clipboard.SetText(text);
                Log("AI Video Gen: đã copy prompt vào clipboard.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen copy thất bại: " + ex.Message);
            }
        }

        private void btnSaveAiVideoPrompt_Click(object sender, EventArgs e)
        {
            var text = txtAiVideoGenPrompt?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Log("AI Video Gen: chưa có nội dung để lưu file.");
                return;
            }

            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.FileName = $"ai_video_prompt_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    dialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                    dialog.Title = "Lưu AI Video Prompt";

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    File.WriteAllText(dialog.FileName, text, TextFileEncoding.Utf8NoBom);
                    Log("AI Video Gen: đã lưu prompt ra file " + dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log("AI Video Gen lưu file thất bại: " + ex.Message);
            }
        }

        private async void btnRenderAiVideo_Click(object sender, EventArgs e)
        {
            var renderItems = GetSlideshowItemsForRender();
            if (renderItems.Count == 0)
            {
                Log("AI Video Gen: chưa có sản phẩm đầu vào (Slideshow).");
                return;
            }

            SyncSelectedScriptFromEditor();
            var script = txtAiVideoGenPrompt?.Text?.Trim() ?? string.Empty;
            var hasReviewScripts = _aiVideoScriptBindingList != null &&
                                   _aiVideoScriptBindingList.Count == renderItems.Count &&
                                   _aiVideoScriptBindingList.All(x => !string.IsNullOrWhiteSpace(x.Script));
            var perItemScripts = hasReviewScripts
                ? _aiVideoScriptBindingList.Select(x => (x.Script ?? string.Empty).Trim()).ToList()
                : null;
            if (string.IsNullOrWhiteSpace(script))
            {
                if (!hasReviewScripts)
                {
                    Log("AI Video Gen: chưa có script. Hãy bấm tạo prompt Gemini hoặc Review Script trước.");
                    return;
                }
            }

            var fingerprintScript = hasReviewScripts
                ? string.Join("\n---\n", perItemScripts)
                : script;
            if (string.IsNullOrWhiteSpace(fingerprintScript))
            {
                Log("AI Video Gen: chưa có script hợp lệ để render.");
                return;
            }

            var safetyScript = hasReviewScripts ? fingerprintScript : script;
            var settingsPolicy = await _configManager.LoadAsync();
            var profile = ResolvePrimaryProfileFromAiBuffer(renderItems);
            var renderProfiles = renderItems
                .Select(x => ProfileScopedPaths.ResolveProfileName(x.ProfileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Log($"AI Video Gen render — nick DNA: {string.Join(", ", renderProfiles)}");
            var renderFingerprint = BuildRenderFingerprint(fingerprintScript, renderItems, profile);
            var isDuplicateRender = await _duplicateGuardManager.ExistsRecentAsync("render", renderFingerprint, TimeSpan.FromDays(7));
            if (isDuplicateRender)
            {
                Log("[SAFEGUARD] Kịch bản + bộ sản phẩm này đã render gần đây (7 ngày). Đã chặn để tránh video trùng.");
                return;
            }
            var renderRisk = _safetyScoreService.ScoreRender(safetyScript, renderItems, profile);
            var forcePreRenderApproval = settingsPolicy.AlwaysRequirePreRenderApproval ?? false;
            var mustApproveRender = forcePreRenderApproval || renderRisk.RequiresManualApproval;
            if (mustApproveRender)
            {
                await EnqueueApprovalItemAsync(new ApprovalQueueItem
                {
                    JobType = ApprovalJobType.RenderVideo,
                    Status = ApprovalStatus.Pending,
                    Profile = profile,
                    Title = "AI Render - Requires Approval",
                    PayloadJson = JsonConvert.SerializeObject(new RenderApprovalPayload
                    {
                        Script = safetyScript,
                        Profile = ProfileScopedPaths.ResolveProfileName(profile),
                        ProfileName = ProfileScopedPaths.ResolveProfileName(profile)
                    }),
                    SafetyScore = renderRisk.Score,
                    RequiresManualApproval = true,
                    RiskReasons = string.Join("; ", renderRisk.Reasons),
                    OriginalPreview = script
                });
                Log($"[APPROVAL] AI Render đã đưa vào hàng duyệt (score {renderRisk.Score}).");
                if (settingsPolicy.AutoRunApprovedQueue ?? false)
                {
                    await AutoRunApprovedQueueItemsAsync();
                }
                else
                {
                    btnOpenApprovalQueue_Click(this, EventArgs.Empty);
                }
                return;
            }

            if (btnProcessVideo != null)
            {
                btnProcessVideo.Enabled = false;
            }

            if (btnGenerateGeminiPrompt != null)
            {
                btnGenerateGeminiPrompt.Enabled = false;
            }

            _aiVideoGenCancellation?.Dispose();
            _aiVideoGenCancellation = new CancellationTokenSource();

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            settings.VideoTransitionDurationSeconds = (double)numAiTransitionDuration.Value;
            settings.VideoTextSize = (int)numAiTextSize.Value;
            settings.VideoMusicVolume = (int)numAiMusicVolume.Value;
            await _configManager.SaveAsync(settings).ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);

            ResetAiRenderSlotProgress();
            foreach (var rp in renderProfiles)
            {
                ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(settings.StorageRootPath, rp);
            }

            var renderPayload = new RenderVideoJobPayload
            {
                ProfileName = profile,
                SharedScript = script,
                PerItemScripts = hasReviewScripts ? perItemScripts : null,
                Products = renderItems,
                RenderFingerprint = renderFingerprint,
                TransitionSeconds = settings.VideoTransitionDurationSeconds,
                TextSize = settings.VideoTextSize,
                MusicVolume = settings.VideoMusicVolume,
                StorageRootPath = settings.StorageRootPath ?? string.Empty,
                UseMultiVoiceNarration = UseMultiVoiceNarrationEnabled(),
                AffiliateLink = renderItems.FirstOrDefault()?.AffiliateLink ?? string.Empty,
                ProductId = renderItems.FirstOrDefault()?.ProductId ?? string.Empty,
                StyleTemplate = GetSelectedGeminiStyleTemplate().ToString()
            };

            var renderJob = new OmniJob
            {
                Kind = OmniJobKind.RenderVideo,
                Title = "Render AI (" + renderPayload.Products.Count + " SP)",
                ProfileName = renderPayload.ProfileName,
                PayloadJson = JsonConvert.SerializeObject(renderPayload),
                MaxRetries = 1,
                Tag = _aiVideoGenCancellation,
                AffiliateLink = renderPayload.AffiliateLink,
                ProductId = renderPayload.ProductId
            };
            ScoreAndApplyAiVideoGenSafety(renderItems, safetyScript);
            foreach (var item in renderItems)
            {
                if (item != null)
                {
                    item.PipelineStatus = "Chờ";
                }
            }

            SyncBuffersToGrids();
            _activeRenderJob = renderJob;
            _globalJobQueue.Enqueue(renderJob);
            Log("[JobQueue] Render đã vào hàng đợi.");
        }

        private async void btnReviewScriptBeforeRender_Click(object sender, EventArgs e)
        {
            var slideshowItems = GetSlideshowItemsForRender();
            if (slideshowItems.Count == 0)
            {
                Log("AI Video Gen: chưa có dữ liệu sản phẩm để review script.");
                return;
            }

            btnReviewScriptBeforeRender.Enabled = false;
            btnGenerateGeminiPrompt.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync();
                if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    Log("AI Video Gen: thiếu AI API key trong Setting.");
                    return;
                }

                var selectedItems = slideshowItems.Take(10).ToList();
                if (slideshowItems.Count > 10)
                {
                    Log("AI Video Gen: chỉ tạo review script cho 10 sản phẩm đầu tiên mỗi lượt.");
                }

                var gemini = new GeminiService();
                var raw = await gemini.GenerateAffiliateExperienceScriptAsync(
                    "per-product",
                    selectedItems,
                    selectedItems[0],
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None,
                    GeminiStyleTemplate.Review).ConfigureAwait(true);

                var generated = ParseReviewScripts(raw, selectedItems);
                _aiVideoScriptBindingList.Clear();
                for (var i = 0; i < generated.Count; i++)
                {
                    _aiVideoScriptBindingList.Add(generated[i]);
                }

                if (_aiVideoScriptBindingList.Count > 0)
                {
                    dgvAiVideoScriptReview.ClearSelection();
                    dgvAiVideoScriptReview.Rows[0].Selected = true;
                    LoadSelectedReviewScriptToEditor();
                }

                Log($"AI Video Gen: đã tạo {generated.Count} script để bạn review trước khi render.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen review script thất bại: " + ex.Message);
            }
            finally
            {
                btnReviewScriptBeforeRender.Enabled = true;
                btnGenerateGeminiPrompt.Enabled = true;
            }
        }

        private async void btnRunAffiliateDeepVideo_Click(object sender, EventArgs e)
        {
            var top4 = GetDeepDiveOrderedScenesForRender();
            if (top4.Count < 4)
            {
                Log("Affiliate Deep Video: cần ít nhất 4 ảnh của cùng 1 sản phẩm.");
                return;
            }

            var firstName = (top4[0]?.ProductName ?? string.Empty).Trim();
            if (top4.Any(x => !string.Equals((x?.ProductName ?? string.Empty).Trim(), firstName, StringComparison.OrdinalIgnoreCase)))
            {
                Log("Affiliate Deep Video: 4 ảnh đầu phải thuộc cùng một sản phẩm (ProductName giống nhau).");
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            var category = (top4[0]?.Category ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "AffiliateDeep";
            }

            foreach (var item in top4)
            {
                if (item != null)
                {
                    item.PipelineStatus = "Chờ";
                    item.ProfileName = string.IsNullOrWhiteSpace(item.ProfileName) ? profile : item.ProfileName;
                }
            }

            ScoreAndApplyAiVideoGenSafety(top4, txtAiVideoGenPrompt?.Text?.Trim());
            SyncBuffersToGrids();

            if (btnRunAffiliateDeepVideo != null)
            {
                btnRunAffiliateDeepVideo.Enabled = false;
            }

            if (btnProcessVideo != null)
            {
                btnProcessVideo.Enabled = false;
            }

            if (btnGenerateGeminiPrompt != null)
            {
                btnGenerateGeminiPrompt.Enabled = false;
            }

            if (btnReviewScriptBeforeRender != null)
            {
                btnReviewScriptBeforeRender.Enabled = false;
            }
            ResetAiRenderSlotProgress();
            UpdateSinglePipelineProgress(2, "Đã xếp hàng Deep render…");

            var job = new OmniJob
            {
                Kind = OmniJobKind.AffiliateDeepRender,
                Title = "Affiliate Deep — " + firstName,
                ProfileName = profile,
                PayloadJson = JsonConvert.SerializeObject(new AffiliateDeepRenderJobPayload
                {
                    ProfileName = profile,
                    ProductName = firstName,
                    Category = category,
                    Products = top4,
                    StorageRootPath = settings.StorageRootPath ?? string.Empty,
                    SafetyScore = top4[0]?.SafetyScore ?? 100,
                    UseMultiVoiceNarration = UseMultiVoiceNarrationEnabled(),
                    AffiliateLink = top4[0]?.AffiliateLink ?? string.Empty,
                    ProductId = top4[0]?.ProductId ?? string.Empty
                }),
                MaxRetries = 1,
                AffiliateLink = top4[0]?.AffiliateLink ?? string.Empty,
                ProductId = top4[0]?.ProductId ?? string.Empty
            };

            _activeAffiliateDeepRenderJobId = job.Id;
            _globalJobQueue.Enqueue(job);
            Log("[JobQueue] Affiliate Deep render đã vào hàng đợi (không chạy trên UI).");
        }

        private async void btnBrowseMascotImage_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.gif";
                dialog.Title = "Chon anh Linh vat";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtMascotImagePath.Text = dialog.FileName;
                    await RunAutoDetectMouthAsync().ConfigureAwait(true);
                }
            }
        }

        private void btnSelectAvatarIdentityPack_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.gif";
                dialog.Title = "Chọn 3-5 ảnh cho Bộ nhận diện";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var files = (dialog.FileNames ?? new string[0])
                    .Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (files.Count < 3 || files.Count > 5)
                {
                    Log("AvatarVault: cần chọn đúng 3-5 ảnh cho Bộ nhận diện.");
                    MessageBox.Show(this, "Vui lòng chọn đúng 3-5 ảnh.", "Avatar Identity Pack", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var profile = (cbRunningProfile?.SelectedItem?.ToString() ?? "default").Trim();
                if (string.IsNullOrWhiteSpace(profile))
                {
                    profile = "default";
                }

                var profileDir = EnsureAvatarVaultProfileDirectory(profile);
                foreach (var old in Directory.GetFiles(profileDir))
                {
                    try { File.Delete(old); } catch { }
                }

                for (var i = 0; i < files.Count; i++)
                {
                    var ext = Path.GetExtension(files[i]);
                    if (string.IsNullOrWhiteSpace(ext))
                    {
                        ext = ".jpg";
                    }

                    var target = Path.Combine(profileDir, $"identity_{i + 1:D2}{ext.ToLowerInvariant()}");
                    File.Copy(files[i], target, true);
                }

                var pack = AvatarIdentityPackStore.LoadOrCreate(profile);
                pack.IdentityImagePaths = Directory.GetFiles(profileDir)
                    .Where(x =>
                    {
                        var name = Path.GetFileName(x) ?? string.Empty;
                        if (name.Equals("identity_pack.json", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        var ext = Path.GetExtension(x).ToLowerInvariant();
                        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif";
                    })
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                AvatarIdentityPackStore.Save(profile, pack);
                _mascotIdentityPackConfig = pack;
                SyncMascotIdentityPackUi(profile, pack.IdentityImagePaths);
                SyncMouthPathsToUi();
                Log($"AvatarVault: đã lưu Bộ nhận diện cho profile '{profile}' với {files.Count} ảnh.");
            }
        }

        private async void btnRunMascotChannelPipeline_Click(object sender, EventArgs e)
        {
            var mascotImagePath = txtMascotImagePath?.Text?.Trim() ?? string.Empty;
            var channelTheme = txtMascotChannelTheme?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath))
            {
                Log("Mascot Story Pipeline: ảnh linh vật/người mẫu không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(channelTheme))
            {
                Log("Mascot Story Pipeline: vui lòng nhập chủ đề kênh.");
                return;
            }

            var profileName = GetSelectedMascotProfileName();
            var identityPack = LoadAvatarIdentityPackForProfile(profileName);
            if (identityPack.Count < 3 || identityPack.Count > 5)
            {
                Log("Mascot Story Pipeline: Bộ nhận diện chưa hợp lệ. Hãy chọn 3-5 ảnh trong AvatarVault.");
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var mascotStyle = ResolveMascotStyleForProfile(profileName, settings);

            Log("Mascot Story Pipeline: xếp hàng Job Queue — nick «" + profileName + "»" +
                (string.IsNullOrWhiteSpace(mascotStyle) ? "" : " | style: " + mascotStyle));
            ResetAiRenderSlotProgress();
            UpdateSinglePipelineProgress(2, "Đã xếp hàng…");

            var lipPack = BuildMascotLipSyncPackForEnqueue();
            EnqueueMascotStoryJob(new MascotStoryJobPayload
            {
                MascotImagePath = mascotImagePath,
                ChannelTheme = channelTheme,
                ProfileName = profileName,
                MascotStyle = mascotStyle,
                IdentityImagePaths = identityPack,
                SceneCount = GetSelectedMascotSceneCount(),
                StorageRootPath = settings.StorageRootPath ?? string.Empty,
                UseLipSync = chkMascotUseLipSync?.Checked ?? false,
                MouthClosedPath = lipPack.MouthClosedPath ?? string.Empty,
                MouthOpenSmallPath = lipPack.MouthOpenSmallPath ?? string.Empty,
                MouthOpenPath = lipPack.MouthOpenPath ?? string.Empty,
                MouthOverlayX = lipPack.MouthOverlayX,
                MouthOverlayY = lipPack.MouthOverlayY,
                MouthOverlayScale = lipPack.MouthOverlayScale,
                UseEmotionalRemix = true,
                UseVisualHookSfx = chkMascotUseVisualHookSfx?.Checked == true,
                VisualHookSfxPath = (txtMascotVisualHookSfx?.Text ?? string.Empty).Trim()
            });
        }

        private string GetSelectedMascotProfileName()
        {
            var name = cbMascotProfile?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = cbRunningProfile?.SelectedItem?.ToString();
            }

            return ProfileScopedPaths.ResolveProfileName(name);
        }

        private static string ResolveMascotStyleForProfile(string profileName, AppSettings settings)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var fromSettings = settings?.Profiles?.FirstOrDefault(p =>
                p != null && string.Equals((p.Name ?? string.Empty).Trim(), nick, StringComparison.OrdinalIgnoreCase));
            var style = (fromSettings?.MascotPersonality ?? fromSettings?.MascotStyle ?? string.Empty).Trim();
            return style;
        }

        private void RefreshMascotProfileCombo(AppSettings settings)
        {
            if (cbMascotProfile == null)
            {
                return;
            }

            var previous = cbMascotProfile.SelectedItem?.ToString();
            cbMascotProfile.Items.Clear();
            cbMascotProfile.Items.Add("default");
            foreach (var profile in settings?.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || cbMascotProfile.Items.Contains(name))
                {
                    continue;
                }

                cbMascotProfile.Items.Add(name);
            }

            var target = string.IsNullOrWhiteSpace(previous) ? "default" : previous.Trim();
            var index = cbMascotProfile.Items.IndexOf(target);
            cbMascotProfile.SelectedIndex = index >= 0 ? index : 0;
        }

        private async void btnPreviewMascotVariants_Click(object sender, EventArgs e)
        {
            var mascotImagePath = txtMascotImagePath?.Text?.Trim() ?? string.Empty;
            var channelTheme = txtMascotChannelTheme?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath))
            {
                Log("Preview biến thể: ảnh linh vật/người mẫu không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(channelTheme))
            {
                Log("Preview biến thể: vui lòng nhập chủ đề kênh.");
                return;
            }

            btnPreviewMascotVariants.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync();
                var profileName = GetSelectedMascotProfileName();
                var identityPack = LoadAvatarIdentityPackForProfile(profileName);
                if (identityPack.Count < 3 || identityPack.Count > 5)
                {
                    Log("Preview biến thể: Bộ nhận diện chưa hợp lệ. Hãy chọn 3-5 ảnh.");
                    return;
                }

                var mascotStyle = ResolveMascotStyleForProfile(profileName, settings);
                Log("Preview biến thể: đang tạo ảnh tham chiếu (Gemini kịch bản + Veo)…");
                var preview = await _videoProcessingService.GenerateMascotVariantPreviewAsync(
                    mascotImagePath,
                    channelTheme,
                    identityPack,
                    GetSelectedMascotSceneCount(),
                    settings,
                    Log,
                    CancellationToken.None,
                    mascotStyle);
                BindMascotPreviewImages(preview.PreviewImagePaths, preview.SceneScripts);
                Log($"Preview biến thể: đã hiển thị {preview.PreviewImagePaths.Count} ảnh.");
            }
            catch (Exception ex)
            {
                Log("Preview biến thể thất bại: " + ex.Message);
            }
            finally
            {
                btnPreviewMascotVariants.Enabled = true;
            }
        }

        private string GetSelectedPhilosophyProfileName()
        {
            var batches = GetPhilosophyTargetBatchesFromGrid();
            foreach (var batch in batches)
            {
                var row = (batch?.ProfileName ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(row))
                {
                    return ProfileScopedPaths.ResolveProfileName(row);
                }
            }

            return "default";
        }

        private async Task<int> EnqueuePhilosophyJobsFromLinesAsync(string[] lines, string profileName, AppSettings settings)
        {
            var profile = PhilosophyProfileAssets.ResolveProfile(settings, profileName);
            var quotes = (lines ?? Array.Empty<string>())
                .Select(l => (l ?? string.Empty).Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                .ToList();

            if (quotes.Count == 0)
            {
                LogPhilosophy("Không có dòng quote hợp lệ (bỏ dòng trống và dòng # comment).");
                return 0;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var nick = ProfileScopedPaths.ResolveProfileName(profile.Name);
            var enqueued = 0;
            foreach (var quote in quotes)
            {
                var payload = new PhilosophyVideoJobPayload
                {
                    QuoteText = quote,
                    ProfileName = nick,
                    VoiceId = profile.VoiceId ?? string.Empty,
                    VideoStyle = profile.VideoStyle ?? string.Empty,
                    StorageRootPath = settings.StorageRootPath ?? string.Empty,
                    ScheduledPostUtc = DateTime.UtcNow.AddHours(2)
                };
                var shortTitle = quote.Length > 40 ? quote.Substring(0, 40) + "…" : quote;
                EnqueuePhilosophyJob(payload, "Quote: " + shortTitle);
                enqueued++;
            }

            await Task.CompletedTask.ConfigureAwait(false);
            return enqueued;
        }

        private void EnqueuePhilosophyJob(PhilosophyVideoJobPayload payload, string title)
        {
            if (payload == null)
            {
                return;
            }

            var job = new OmniJob
            {
                Kind = OmniJobKind.PhilosophyVideo,
                Title = title,
                ProfileName = payload.ProfileName,
                PayloadJson = JsonConvert.SerializeObject(payload),
                MaxRetries = 1,
                Tag = _aiVideoGenCancellation
            };

            _globalJobQueue.Enqueue(job);
            TrackPhilosophyJob(job.Id, payload, title);
            Log($"[JobQueue] Đã đẩy job Quote vào hàng đợi: {title}");
        }

        private void RefreshPhilosophyPrereqLabel(AppSettings settings = null)
        {
            if (lblPhilosophyPrereq == null || lblPhilosophyPrereq.IsDisposed)
            {
                return;
            }

            if (settings == null)
            {
                _ = RefreshPhilosophyPrereqLabelAsync();
                return;
            }

            void Apply()
            {
                var blockers = PhilosophyVideoService.DescribeBlockers(settings);
                var ready = PhilosophyVideoPipelineService.TryValidatePrerequisites(settings, out _);
                lblPhilosophyPrereq.ForeColor = ready
                    ? Color.FromArgb(120, 220, 160)
                    : Color.FromArgb(255, 180, 120);
                lblPhilosophyPrereq.Text = ready
                    ? "✓ Sẵn sàng tạo video — thêm batch / bấm «Tạo nội dung Gemini» rồi «Render video»."
                    : "⚠ Cần cấu hình trước (tab Cài đặt):\r\n" + blockers.Replace("\r\n", "  •  ");
            }

            if (lblPhilosophyPrereq.InvokeRequired)
            {
                lblPhilosophyPrereq.Invoke(new Action(Apply));
                return;
            }

            Apply();
        }

        private async Task RefreshPhilosophyPrereqLabelAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                RefreshPhilosophyPrereqLabel(settings);
            }
            catch
            {
                RefreshPhilosophyPrereqLabel(new AppSettings());
            }
        }

        private void RefreshPhilosophyProfileCombo(AppSettings settings)
        {
            if (_colPhilosophyProfile == null)
            {
                return;
            }

            RefreshGridProfileComboSource(settings);
            if (_philosophyBatchBindingList != null)
            {
                foreach (var batch in _philosophyBatchBindingList)
                {
                    EnsureProfileComboIncludes(batch?.ProfileName);
                }
            }

            ApplyGridProfileComboColumn(dgvPhilosophyScripts, "colPhilosophyProfile");
            dgvPhilosophyScripts?.Invalidate();
        }

        private void LogPhilosophy(string message)
        {
            Log(message);

            if (rtbPhilosophyLog == null || rtbPhilosophyLog.IsDisposed)
            {
                return;
            }

            if (rtbPhilosophyLog.InvokeRequired)
            {
                rtbPhilosophyLog.Invoke(new Action<string>(LogPhilosophy), message);
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var isError = message.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                          || message.IndexOf("429", StringComparison.Ordinal) >= 0
                          || message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;
            rtbPhilosophyLog.SelectionStart = rtbPhilosophyLog.TextLength;
            rtbPhilosophyLog.SelectionLength = 0;
            rtbPhilosophyLog.SelectionColor = isError ? Color.FromArgb(255, 120, 120) : Color.FromArgb(190, 195, 205);
            rtbPhilosophyLog.SelectionCharOffset = PhilosophyLogLineSpacing;
            rtbPhilosophyLog.AppendText(line + Environment.NewLine);
            rtbPhilosophyLog.SelectionColor = Color.FromArgb(190, 195, 205);
            rtbPhilosophyLog.ScrollToCaret();
        }

        private void SetPhilosophyProgress(string statusText, int percent, bool indeterminate = false)
        {
            if (lblPhilosophyProgress == null || pbPhilosophyProgress == null)
            {
                return;
            }

            void Apply()
            {
                lblPhilosophyProgress.Text = "Tiến trình: " + (statusText ?? string.Empty).Trim();
                if (indeterminate)
                {
                    pbPhilosophyProgress.Style = ProgressBarStyle.Marquee;
                    pbPhilosophyProgress.MarqueeAnimationSpeed = 28;
                }
                else
                {
                    pbPhilosophyProgress.Style = ProgressBarStyle.Continuous;
                    pbPhilosophyProgress.MarqueeAnimationSpeed = 0;
                    pbPhilosophyProgress.Value = Math.Max(0, Math.Min(100, percent));
                }
            }

            if (lblPhilosophyProgress.InvokeRequired)
            {
                lblPhilosophyProgress.Invoke(new Action(Apply));
                return;
            }

            Apply();
        }

        private void btnPhilosophyClearLog_Click(object sender, EventArgs e)
        {
            if (rtbPhilosophyLog == null || rtbPhilosophyLog.IsDisposed)
            {
                return;
            }

            rtbPhilosophyLog.Clear();
            SetPhilosophyProgress("sẵn sàng", 0);
        }

        private void btnShowcaseClearLog_Click(object sender, EventArgs e)
        {
            if (rtbShowcaseLog == null || rtbShowcaseLog.IsDisposed)
            {
                return;
            }

            rtbShowcaseLog.Clear();
        }

        private static bool IsShowcaseLogMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.StartsWith("[Showcase", StringComparison.Ordinal)
                   || message.StartsWith("Showcase:", StringComparison.Ordinal)
                   || message.IndexOf("[Showcase Render]", StringComparison.Ordinal) >= 0
                   || message.IndexOf("[JobQueue] Showcase", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("[JobQueue] Affiliate Deep render", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("output Showcase", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Log Showcase trên tab (và ghi file qua <see cref="Log"/>).</summary>
        private void LogShowcase(string message)
        {
            Log(message);
            AppendToShowcaseLogPanel(message);
            TryMirrorShowcaseLogToAudioDialog(message);
        }

        private void AppendToShowcaseLogPanel(string message)
        {
            if (rtbShowcaseLog == null || rtbShowcaseLog.IsDisposed)
            {
                return;
            }

            if (rtbShowcaseLog.InvokeRequired)
            {
                rtbShowcaseLog.Invoke(new Action<string>(AppendToShowcaseLogPanel), message);
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var isError = message.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                          || message.IndexOf("thất bại", StringComparison.OrdinalIgnoreCase) >= 0
                          || message.IndexOf("429", StringComparison.Ordinal) >= 0
                          || message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0
                          || message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;
            rtbShowcaseLog.SelectionStart = rtbShowcaseLog.TextLength;
            rtbShowcaseLog.SelectionLength = 0;
            rtbShowcaseLog.SelectionColor = isError ? Color.FromArgb(255, 130, 130) : Color.FromArgb(215, 222, 235);
            rtbShowcaseLog.SelectionCharOffset = 1;
            rtbShowcaseLog.AppendText(line + Environment.NewLine);
            rtbShowcaseLog.SelectionColor = Color.FromArgb(215, 222, 235);
            rtbShowcaseLog.ScrollToCaret();

            const int maxLines = 800;
            if (rtbShowcaseLog.Lines.Length > maxLines)
            {
                var keep = string.Join(Environment.NewLine, rtbShowcaseLog.Lines.Skip(maxLines / 4));
                rtbShowcaseLog.Clear();
                rtbShowcaseLog.AppendText(keep + Environment.NewLine);
                rtbShowcaseLog.SelectionStart = rtbShowcaseLog.TextLength;
                rtbShowcaseLog.ScrollToCaret();
            }
        }

        private void ApplyAiVideoGenModeUiVisibility()
        {
            var idx = GetSelectedAiVideoGenModeIndex();
            var showProductGrid = idx == 0 || idx == 1;

            // Text/Visible của lblAiVideoGenProductsTitle được set duy nhất trong SyncProductGridVisibilityForMode
            // (gọi ngay dưới) — tránh 2 nơi cùng ghi đè lên 1 label gây trùng lặp nội dung.
            if (chkAiVideoGenCurrentProfileOnly != null)
            {
                // Showcase không dùng bảng sản phẩm Hunter — checkbox lọc profile chỉ cần ở Slideshow.
                chkAiVideoGenCurrentProfileOnly.Visible = idx == 0;
            }

            if (pnlAiVideoGenProductArea != null)
            {
                pnlAiVideoGenProductArea.Visible = showProductGrid;
            }

            if (pnlSlideshowGridHost != null)
            {
                pnlSlideshowGridHost.Visible = idx == 0;
            }

            if (pnlDeepDiveGridHost != null)
            {
                pnlDeepDiveGridHost.Visible = idx == 1;
            }

            if (pnlManualInput != null)
            {
                // Khung dán link chỉ tab Slideshow — Showcase dùng «Chọn ảnh từ máy».
                pnlManualInput.Visible = idx == 0;
            }

            if (pnlAffiliateDeepStoryboardHost != null)
            {
                pnlAffiliateDeepStoryboardHost.Visible = idx == 1;
            }

            if (pnlAiVideoGenScriptHost != null)
            {
                pnlAiVideoGenScriptHost.Visible = showProductGrid;
            }

            SyncAiVideoGenActionBarForMode(idx);

            SyncProductGridVisibilityForMode(idx);

            ApplyAffiliateDeepControlHosts(idx);

            ApplyAiVideoGenShellLayout(showProductGrid, idx);

            RefreshAiVideoGenModeIndicator();

            var showScriptBlock = idx == 0 || idx == 1;
            if (dgvAiVideoScriptReview != null)
            {
                dgvAiVideoScriptReview.Visible = showScriptBlock;
            }

            if (txtAiVideoGenPrompt != null)
            {
                txtAiVideoGenPrompt.Visible = showScriptBlock;
            }
        }

        /// <summary>
        /// Slideshow/Affiliate: bảng sản phẩm Fill + khối mode Dock Bottom (~300px).
        /// Mascot/Triết lý/Reup: khối mode chiếm toàn bộ tab (tránh vùng trống ~60% màn hình).
        /// </summary>
        private void ApplyAiVideoGenShellLayout(bool showProductGrid, int modeTabIndex)
        {
            if (tabAiVideoGen == null || pnlAiVideoGenStickyHost == null || _aiVideoGenLayoutGuard)
            {
                return;
            }

            _aiVideoGenLayoutGuard = true;
            tabAiVideoGen.SuspendLayout();
            try
            {
                if (tblAiVideoGenRoot == null)
                {
                    WireAiVideoGenTabTableLayout();
                }

                ApplyAiVideoGenTableRowHeights(showProductGrid, modeTabIndex);
                AttachAiRenderProgressPanelToSelectedModePanel();
                if (modeTabIndex == (int)AiVideoGenMode.Philosophy)
                {
                    EnsureProductionQueueGridBandHeight(dgvPhilosophyQueue);
                }
                else if (modeTabIndex == (int)AiVideoGenMode.Mascot)
                {
                    EnsureProductionQueueGridBandHeight(dgvMascotQueue);
                }
                else if (modeTabIndex == (int)AiVideoGenMode.VideoReup)
                {
                    LayoutVideoReupShell();
                }
                else if (modeTabIndex == (int)AiVideoGenMode.ProductAdImage)
                {
                    LayoutProductAdImageShell();
                }
            }
            finally
            {
                tabAiVideoGen.ResumeLayout(true);
                _aiVideoGenLayoutGuard = false;
            }
        }

        private void SyncAiVideoGenActionBarForMode(int modeTabIndex)
        {
            if (tblAiVideoGenActionInner == null)
            {
                return;
            }

            var showSlideshowActions = modeTabIndex == 0;
            var showDeepActions = modeTabIndex == 1;

            if (pnlAiVideoGenReadinessHost != null)
            {
                pnlAiVideoGenReadinessHost.Visible = showSlideshowActions;
            }

            if (lblSlideshowReadiness != null)
            {
                lblSlideshowReadiness.Visible = showSlideshowActions;
            }

            if (lblAffiliateDeepReadiness != null)
            {
                lblAffiliateDeepReadiness.Visible = false;
            }

            if (pnlAffiliateDeepReadinessHost != null)
            {
                pnlAffiliateDeepReadinessHost.Visible = false;
            }

            if (pnlShowcaseTabTitleHost != null)
            {
                pnlShowcaseTabTitleHost.Visible = showDeepActions;
            }

            if (pnlSlideshowActionBar != null)
            {
                pnlSlideshowActionBar.Visible = showSlideshowActions;
            }

            if (pnlAiVideoGenActionBar != null)
            {
                pnlAiVideoGenActionBar.Visible = showSlideshowActions || showDeepActions;
            }
        }

        private string EnsureAvatarVaultRootDirectory()
        {
            return AvatarIdentityPackStore.GetVaultRoot();
        }

        private string EnsureAvatarVaultProfileDirectory(string profileName)
        {
            var root = EnsureAvatarVaultRootDirectory();
            var safeProfile = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                safeProfile = safeProfile.Replace(invalid, '_');
            }

            var path = Path.Combine(root, safeProfile);
            Directory.CreateDirectory(path);
            return path;
        }

        private List<string> LoadAvatarIdentityPackForProfile(string profileName)
        {
            _mascotIdentityPackConfig = AvatarIdentityPackStore.LoadOrCreate(profileName);
            var files = AvatarIdentityPackStore.SyncIdentityImagesFromVault(profileName);
            if (files.Count >= 3)
            {
                _mascotIdentityPackConfig.IdentityImagePaths = files;
            }

            SyncMascotIdentityPackUi(profileName, files);
            SyncMouthPathsToUi();
            return files.Count >= 3 ? files : (_mascotIdentityPackConfig.IdentityImagePaths ?? new List<string>());
        }

        private static int GetSelectedMascotSceneCount() => 4;

        private static PictureBox CreateMascotPreviewBoxDocked()
        {
            return new PictureBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.None
            };
        }

        private static PictureBox CreateMascotPreviewBox(Point location)
        {
            return new PictureBox
            {
                Location = location,
                Size = new Size(245, 138),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(20, 22, 28)
            };
        }

        private void BindMascotPreviewImages(IList<string> paths, IList<string> sceneScripts)
        {
            _mascotPreviewImagePaths = (paths ?? new List<string>()).ToList();
            _mascotPreviewSceneScripts = (sceneScripts ?? new List<string>()).ToList();
            var targets = new[] { pbMascotPreview1, pbMascotPreview2, pbMascotPreview3, pbMascotPreview4 };
            for (var i = 0; i < targets.Length; i++)
            {
                var box = targets[i];
                if (box == null)
                {
                    continue;
                }

                var old = box.Image;
                box.Image = null;
                old?.Dispose();

                if (paths == null || i >= paths.Count || string.IsNullOrWhiteSpace(paths[i]) || !File.Exists(paths[i]))
                {
                    continue;
                }

                using (var fs = new FileStream(paths[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                {
                    box.Image = new Bitmap(img);
                }
            }
        }

        private void MascotPreview_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var box = sender as PictureBox;
            if (box == null)
            {
                return;
            }

            var idx = box.Tag is int ? (int)box.Tag : -1;
            _selectedMascotPreviewSceneIndex = idx;
            var canRegen = idx >= 0 && idx < _mascotPreviewSceneScripts.Count;
            miRegenerateScene.Enabled = canRegen;
        }

        private async void miRegenerateScene_Click(object sender, EventArgs e)
        {
            var idx = _selectedMascotPreviewSceneIndex;
            if (idx < 0 || idx >= _mascotPreviewSceneScripts.Count)
            {
                Log("Regenerate scene: chưa chọn cảnh hợp lệ.");
                return;
            }

            var mascotImagePath = txtMascotImagePath?.Text?.Trim() ?? string.Empty;
            var channelTheme = txtMascotChannelTheme?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath) || string.IsNullOrWhiteSpace(channelTheme))
            {
                Log("Regenerate scene: thiếu ảnh mascot hoặc chủ đề kênh.");
                return;
            }

            var profileName = GetSelectedMascotProfileName();
            var identityPack = LoadAvatarIdentityPackForProfile(profileName);
            if (identityPack.Count < 3 || identityPack.Count > 5)
            {
                Log("Regenerate scene: Bộ nhận diện chưa hợp lệ (cần 3-5 ảnh).");
                return;
            }

            miRegenerateScene.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync();
                var mascotStyle = ResolveMascotStyleForProfile(profileName, settings);
                var sceneScript = _mascotPreviewSceneScripts[idx];
                Log($"Regenerate scene {idx + 1}: đang gọi Gemini tạo lại ảnh + prompt...");
                var regen = await _videoProcessingService.RegenerateMascotSceneAsync(
                    mascotImagePath,
                    channelTheme,
                    identityPack,
                    sceneScript,
                    settings,
                    Log,
                    CancellationToken.None,
                    mascotStyle);

                while (_mascotPreviewImagePaths.Count <= idx)
                {
                    _mascotPreviewImagePaths.Add(string.Empty);
                }
                _mascotPreviewImagePaths[idx] = regen.PreviewImagePath;
                BindMascotPreviewImages(_mascotPreviewImagePaths, _mascotPreviewSceneScripts);
                Log($"Regenerate scene {idx + 1}: done. Motion prompt -> {regen.MotionPrompt}");
            }
            catch (Exception ex)
            {
                Log($"Regenerate scene {idx + 1} thất bại: {ex.Message}");
            }
            finally
            {
                miRegenerateScene.Enabled = true;
            }
        }

        private void MascotPreview_Click(object sender, EventArgs e)
        {
            var box = sender as PictureBox;
            if (box == null)
            {
                return;
            }

            var idx = box.Tag is int ? (int)box.Tag : -1;
            if (idx < 0 || idx >= _mascotPreviewImagePaths.Count)
            {
                return;
            }

            var imagePath = _mascotPreviewImagePaths[idx];
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            OpenFullSizeImagePreview(imagePath, idx + 1);
        }

        private void OpenFullSizeImagePreview(string imagePath, int sceneNumber)
        {
            using (var form = new Form())
            {
                form.Text = $"Preview Scene {sceneNumber} - Full Size";
                form.StartPosition = FormStartPosition.CenterParent;
                form.Size = new Size(980, 760);
                form.BackColor = Color.FromArgb(18, 20, 26);
                form.ForeColor = Color.Gainsboro;
                form.KeyPreview = true;

                var picture = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(18, 20, 26)
                };
                using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                {
                    picture.Image = new Bitmap(img);
                }

                var hint = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 28,
                    Text = "Esc để đóng | Click phải để regenerate scene",
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.LightGray,
                    BackColor = Color.FromArgb(26, 29, 37)
                };

                form.Controls.Add(picture);
                form.Controls.Add(hint);
                form.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == System.Windows.Forms.Keys.Escape)
                    {
                        form.Close();
                    }
                };
                form.FormClosed += (s, e) =>
                {
                    var old = picture.Image;
                    picture.Image = null;
                    old?.Dispose();
                };
                form.ShowDialog(this);
            }
        }

        private List<AiVideoScriptReviewItem> ParseReviewScripts(string rawJson, IList<AiVideoGenInputItem> sourceItems)
        {
            var output = new List<AiVideoScriptReviewItem>();
            var source = sourceItems ?? new List<AiVideoGenInputItem>();
            try
            {
                var clean = (rawJson ?? string.Empty).Trim();
                var start = clean.IndexOf('[');
                var end = clean.LastIndexOf(']');
                if (start >= 0 && end > start)
                {
                    clean = clean.Substring(start, end - start + 1);
                }

                var arr = JArray.Parse(clean);
                for (var i = 0; i < source.Count; i++)
                {
                    var script = string.Empty;
                    var node = arr.Children<JObject>().FirstOrDefault(x => (int?)x["index"] == i + 1) ?? arr.Children<JObject>().ElementAtOrDefault(i);
                    if (node != null)
                    {
                        script = (node["script"]?.ToString() ?? string.Empty).Trim();
                    }

                    output.Add(new AiVideoScriptReviewItem
                    {
                        Index = i + 1,
                        ProductName = source[i].ProductName,
                        Price = source[i].Price,
                        Script = script
                    });
                }
            }
            catch
            {
                for (var i = 0; i < source.Count; i++)
                {
                    output.Add(new AiVideoScriptReviewItem
                    {
                        Index = i + 1,
                        ProductName = source[i].ProductName,
                        Price = source[i].Price,
                        Script = string.Empty
                    });
                }
            }

            return output;
        }

        private void DgvAiVideoScriptReview_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (dgvAiVideoScriptReview?.Columns == null || dgvAiVideoScriptReview.Columns.Count == 0)
            {
                return;
            }

            ApplyAppGridChrome(dgvAiVideoScriptReview);
            EnsureAppGridRowHeights(dgvAiVideoScriptReview);

            foreach (DataGridViewColumn col in dgvAiVideoScriptReview.Columns)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

                var name = col.DataPropertyName ?? string.Empty;
                if (string.Equals(name, "Index", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "#";
                    col.FillWeight = 8f;
                    col.MinimumWidth = 36;
                }
                else if (string.Equals(name, "ProductName", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Sản phẩm";
                    col.FillWeight = 40f;
                    col.MinimumWidth = 96;
                }
                else if (string.Equals(name, "Price", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Giá";
                    col.FillWeight = 16f;
                    col.MinimumWidth = 64;
                }
                else if (string.Equals(name, "Script", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Script";
                    col.FillWeight = 36f;
                    col.MinimumWidth = 120;
                }
            }

            ApplyAppGridChrome(dgvAiVideoScriptReview);
        }

        private void dgvAiVideoScriptReview_SelectionChanged(object sender, EventArgs e)
        {
            LoadSelectedReviewScriptToEditor();
        }

        private void txtAiVideoGenPrompt_TextChanged(object sender, EventArgs e)
        {
            SyncSelectedScriptFromEditor();
        }

        private void LoadSelectedReviewScriptToEditor()
        {
            if (dgvAiVideoScriptReview == null || dgvAiVideoScriptReview.CurrentRow == null)
            {
                return;
            }

            var row = dgvAiVideoScriptReview.CurrentRow.DataBoundItem as AiVideoScriptReviewItem;
            if (row == null)
            {
                return;
            }

            if (!string.Equals(txtAiVideoGenPrompt.Text, row.Script ?? string.Empty, StringComparison.Ordinal))
            {
                txtAiVideoGenPrompt.Text = row.Script ?? string.Empty;
            }
        }

        private void SyncSelectedScriptFromEditor()
        {
            if (dgvAiVideoScriptReview == null || dgvAiVideoScriptReview.CurrentRow == null)
            {
                return;
            }

            var row = dgvAiVideoScriptReview.CurrentRow.DataBoundItem as AiVideoScriptReviewItem;
            if (row == null)
            {
                return;
            }

            row.Script = txtAiVideoGenPrompt.Text ?? string.Empty;
            dgvAiVideoScriptReview.Refresh();
        }

        private void UpdateSinglePipelineProgress(int percent, string stage)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, string>(UpdateSinglePipelineProgress), percent, stage);
                return;
            }

            UpdateSlotProgress(1, percent, "Luồng 1: " + (stage ?? "Đang render"));
        }

        private void ResetAiRenderSlotProgress()
        {
            UpdateSlotProgress(1, 0, "Luồng 1: đang chờ");
            UpdateSlotProgress(2, 0, "Luồng 2: đang chờ");
            UpdateSlotProgress(3, 0, "Luồng 3: đang chờ");
        }

        private void UpdateAiRenderProgress(VideoRenderProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<VideoRenderProgress>(UpdateAiRenderProgress), progress);
                return;
            }

            var slot = progress.Slot <= 0 ? 1 : progress.Slot;
            var stage = string.IsNullOrWhiteSpace(progress.Stage) ? "đang xử lý" : progress.Stage;
            var label = $"Luồng {slot}: video {progress.VideoIndex}/{progress.TotalVideos} — {stage}";
            var percent = Math.Max(0, Math.Min(100, progress.Percent));
            if (progress.IsCompleted)
            {
                label = $"Luồng {slot}: xong video {progress.VideoIndex}/{progress.TotalVideos}";
                percent = 100;
            }

            UpdateSlotProgress(slot, percent, label);
        }

        private void UpdateSlotProgress(int slot, int percent, string labelText)
        {
            ProgressBar bar = null;
            Label label = null;
            if (slot == 2)
            {
                bar = pbAiRenderSlot2;
                label = lblAiRenderSlot2;
            }
            else if (slot == 3)
            {
                bar = pbAiRenderSlot3;
                label = lblAiRenderSlot3;
            }
            else
            {
                bar = pbAiRenderSlot1;
                label = lblAiRenderSlot1;
            }

            if (bar != null)
            {
                bar.Value = Math.Max(0, Math.Min(100, percent));
            }
            if (label != null)
            {
                label.Text = labelText ?? string.Empty;
                if (labelText != null && labelText.IndexOf("đang chờ", StringComparison.Ordinal) >= 0)
                {
                    label.ForeColor = Color.FromArgb(130, 135, 150);
                }
                else
                {
                    label.ForeColor = Color.Gainsboro;
                }
            }

            if (ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen)
                && !string.IsNullOrWhiteSpace(labelText)
                && labelText.IndexOf("đang chờ", StringComparison.Ordinal) < 0)
            {
                SetStatusStripText(labelText);
            }
        }



        private void ToggleSecretVisibility(TextBox textBox, Button toggleButton)
        {
            textBox.UseSystemPasswordChar = !textBox.UseSystemPasswordChar;
            toggleButton.Text = textBox.UseSystemPasswordChar
                ? LocalizeDisplayText("Show")
                : LocalizeDisplayText("Hide");
        }

        private void HookSettingValidationEvents()
        {
            txtAiProvider.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtAiModel.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtVeoEndpoint.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtFfmpegPath.TextChanged += (sender, e) => ValidateSettingsInputs();
        }

        private bool ValidateSettingsInputs()
        {
            if (btnSaveSettings == null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(txtAiProvider.Text))
            {
                lblSettingsValidation.Text = "Cần nhập AI Provider.";
                btnSaveSettings.Enabled = false;
                ApplySettingsSaveButtonState();
                EnsureSettingsValidationBarVisible();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtAiModel.Text))
            {
                lblSettingsValidation.Text = "Cần nhập AI Model.";
                btnSaveSettings.Enabled = false;
                ApplySettingsSaveButtonState();
                EnsureSettingsValidationBarVisible();
                return false;
            }

            if (!IsValidHttpUrl(txtVeoEndpoint.Text))
            {
                lblSettingsValidation.Text = "URL gateway Veo phải là URL http/https hợp lệ.";
                btnSaveSettings.Enabled = false;
                ApplySettingsSaveButtonState();
                EnsureSettingsValidationBarVisible();
                return false;
            }

            foreach (var voiceField in EnumerateVoicePersonaFields())
            {
                if (!ElevenLabsTtsHelper.IsValidSettingsVoiceField(voiceField.Text))
                {
                    lblSettingsValidation.Text =
                        "Voice ID ElevenLabs không hợp lệ — chỉ nhập ID (vd. B2sElSyvaOc1di2VskcS), không cần URL.";
                    btnSaveSettings.Enabled = false;
                    ApplySettingsSaveButtonState();
                    EnsureSettingsValidationBarVisible();
                    return false;
                }
            }

            var ffmpegPath = (txtFfmpegPath.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(ffmpegPath) &&
                (!File.Exists(ffmpegPath) || !ffmpegPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                lblSettingsValidation.Text =
                    "Cảnh báo: FFmpeg không hợp lệ — vẫn lưu được; đường dẫn sẽ được xóa nếu không sửa.";
            }
            else
            {
                lblSettingsValidation.Text = string.Empty;
            }

            btnSaveSettings.Enabled = true;
            ApplySettingsSaveButtonState();
            EnsureSettingsValidationBarVisible();
            return true;
        }

        private static string NormalizeSettingsFfmpegPathForSave(string rawPath)
        {
            var ffmpegPath = (rawPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                return string.Empty;
            }

            if (File.Exists(ffmpegPath) && ffmpegPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return ffmpegPath;
            }

            return string.Empty;
        }

        private static bool IsValidHttpUrl(string value)
        {
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static decimal ClampNumericValue(int value, NumericUpDown control)
        {
            if (control == null)
            {
                return value;
            }

            if (value < control.Minimum)
            {
                return control.Minimum;
            }

            if (value > control.Maximum)
            {
                return control.Maximum;
            }

            return value;
        }

        private static decimal ClampNumericValue(double value, NumericUpDown control)
        {
            if (control == null)
            {
                return (decimal)value;
            }

            var decimalValue = (decimal)value;
            if (decimalValue < control.Minimum)
            {
                return control.Minimum;
            }

            if (decimalValue > control.Maximum)
            {
                return control.Maximum;
            }

            return decimalValue;
        }

        private List<AutomationProfile> BuildProxyProfilesFromGrid()
        {
            var proxies = new List<AutomationProfile>();
            if (_proxyProfileBindingList == null)
            {
                return proxies;
            }

            foreach (var proxy in _proxyProfileBindingList)
            {
                if (proxy == null)
                {
                    continue;
                }

                var item = new AutomationProfile
                {
                    Name = (proxy.Name ?? string.Empty).Trim(),
                    ChromeUserDataPath = (proxy.ChromeUserDataPath ?? string.Empty).Trim(),
                    ProxyHost = (proxy.ProxyHost ?? string.Empty).Trim(),
                    ProxyUser = (proxy.ProxyUser ?? string.Empty).Trim(),
                    ProxyPass = (proxy.ProxyPass ?? string.Empty).Trim(),
                    ProxyPort = proxy.ProxyPort < 0 ? 0 : proxy.ProxyPort,
                    TikTokUniqueId = (proxy.TikTokUniqueId ?? string.Empty).Trim(),
                    TikTokNickname = (proxy.TikTokNickname ?? string.Empty).Trim(),
                    FacebookName = (proxy.FacebookName ?? string.Empty).Trim(),
                    YouTubeName = (proxy.YouTubeName ?? string.Empty).Trim(),
                    TikTokUserId = (proxy.TikTokUserId ?? string.Empty).Trim(),
                    VoiceId = (proxy.VoiceId ?? string.Empty).Trim(),
                    VideoStyle = (proxy.VideoStyle ?? string.Empty).Trim(),
                    MascotStyle = (proxy.MascotStyle ?? string.Empty).Trim()
                };

                if (string.IsNullOrWhiteSpace(item.Name) &&
                    string.IsNullOrWhiteSpace(item.ChromeUserDataPath) &&
                    string.IsNullOrWhiteSpace(item.ProxyHost) &&
                    item.ProxyPort == 0 &&
                    string.IsNullOrWhiteSpace(item.ProxyUser) &&
                    string.IsNullOrWhiteSpace(item.ProxyPass) &&
                    string.IsNullOrWhiteSpace(item.TikTokUniqueId) &&
                    string.IsNullOrWhiteSpace(item.TikTokNickname) &&
                    string.IsNullOrWhiteSpace(item.FacebookName) &&
                    string.IsNullOrWhiteSpace(item.YouTubeName) &&
                    string.IsNullOrWhiteSpace(item.TikTokUserId) &&
                    string.IsNullOrWhiteSpace(item.VoiceId) &&
                    string.IsNullOrWhiteSpace(item.VideoStyle))
                {
                    continue;
                }

                proxies.Add(item);
            }

            return proxies;
        }

        private static AutomationProfile CloneAutomationProfileForGrid(AutomationProfile proxy)
        {
            if (proxy == null)
            {
                return new AutomationProfile();
            }

            return new AutomationProfile
            {
                Name = proxy.Name,
                ChromeUserDataPath = proxy.ChromeUserDataPath,
                ProxyHost = proxy.ProxyHost,
                ProxyPort = proxy.ProxyPort,
                ProxyUser = proxy.ProxyUser,
                ProxyPass = proxy.ProxyPass,
                UserAgent = proxy.UserAgent,
                ViewportWidth = proxy.ViewportWidth,
                ViewportHeight = proxy.ViewportHeight,
                TikTokUniqueId = proxy.TikTokUniqueId,
                TikTokNickname = proxy.TikTokNickname,
                FacebookName = proxy.FacebookName,
                YouTubeName = proxy.YouTubeName,
                TikTokUserId = proxy.TikTokUserId,
                VoiceId = proxy.VoiceId ?? string.Empty,
                VideoStyle = proxy.VideoStyle ?? string.Empty,
                MascotStyle = proxy.MascotStyle ?? string.Empty
            };
        }

        private void PopulateProfileBindingListFromSettings(AppSettings settings)
        {
            if (_proxyProfileBindingList == null)
            {
                return;
            }

            _proxyProfileBindingList.Clear();
            foreach (var proxy in settings.Profiles ?? new List<AutomationProfile>())
            {
                _proxyProfileBindingList.Add(CloneAutomationProfileForGrid(proxy));
            }
        }

        private async Task RefreshProxyProfilesFromDiskAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                PopulateProfileBindingListFromSettings(settings);
                RefreshRunningProfileOptions(settings);
                dgvProxyProfiles?.Refresh();
            }
            catch (Exception ex)
            {
                Log("[LOGIN] Làm mới bảng profile: " + ex.Message);
            }
        }

        private void RefreshRunningProfileOptions(AppSettings settings)
        {
            if (cbRunningProfile == null)
            {
                return;
            }

            var previous = cbRunningProfile.SelectedItem?.ToString();
            cbRunningProfile.Items.Clear();
            cbRunningProfile.Items.Add("default");

            foreach (var profile in settings.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!cbRunningProfile.Items.Contains(name))
                {
                    cbRunningProfile.Items.Add(name);
                }
            }

            RefillAutoPostProfileItems(settings);
            RefreshGridProfileComboSource(settings);
            RefreshPhilosophyProfileCombo(settings);
            RefreshHuntProductProfileCombo(settings);
            RefreshAffiliateHuntProfileCombo(settings);
            RefreshPhilosophyPrereqLabel(settings);
            RefreshAiVideoGenModeReadinessLabels();
            RefreshMascotProfileCombo(settings);
            ApplyVideoReupProfileComboColumn();
            ApplyGridProfileComboColumn(dgvPhilosophyScripts, "colPhilosophyProfile");
            ApplyWarmupQueueProfileComboColumn();
            ApplyAutoPostScheduleProfileComboColumns();
            SelectRunningProfileInUi(previous);
            ApplyProfileScope(previous ?? GetRunningProfileName());
        }

        private void RefillAutoPostProfileItems(AppSettings settings)
        {
            if (cbAutoPostProfile == null)
            {
                return;
            }

            cbAutoPostProfile.Items.Clear();
            cbAutoPostProfile.Items.Add("default");

            foreach (var profile in settings?.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!cbAutoPostProfile.Items.Contains(name))
                {
                    cbAutoPostProfile.Items.Add(name);
                }
            }
        }

        private void SelectRunningProfileInUi(string profileName)
        {
            if (cbRunningProfile == null)
            {
                return;
            }

            var target = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
            var index = cbRunningProfile.Items.IndexOf(target);
            cbRunningProfile.SelectedIndex = index >= 0 ? index : 0;

            if (cbAutoPostProfile != null && cbAutoPostProfile.Items.Count > 0)
            {
                var i2 = cbAutoPostProfile.Items.IndexOf(target);
                cbAutoPostProfile.SelectedIndex = i2 >= 0 ? i2 : 0;
            }
        }

        private void RefreshAutoPostVideoCombo(string preferredFullPath = null)
        {
            if (cbAutoPostVideoFile == null)
            {
                return;
            }

            cbAutoPostVideoFile.Items.Clear();
            cbAutoPostVideoFile.Items.Add(new AutoPostVideoItem("(Chọn file video)", string.Empty));

            var folder = txtAutoPostFolder?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                cbAutoPostVideoFile.SelectedIndex = 0;
                return;
            }

            foreach (var path in EnumerateVideoFilesInFolder(folder))
            {
                cbAutoPostVideoFile.Items.Add(new AutoPostVideoItem(Path.GetFileName(path), path));
            }

            if (!string.IsNullOrWhiteSpace(preferredFullPath) && File.Exists(preferredFullPath))
            {
                var alreadyListed = false;
                for (var i = 1; i < cbAutoPostVideoFile.Items.Count; i++)
                {
                    if (cbAutoPostVideoFile.Items[i] is AutoPostVideoItem listed &&
                        string.Equals(listed.FullPath, preferredFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyListed = true;
                        break;
                    }
                }

                if (!alreadyListed)
                {
                    cbAutoPostVideoFile.Items.Add(
                        new AutoPostVideoItem(Path.GetFileName(preferredFullPath), preferredFullPath));
                }
            }

            if (cbAutoPostVideoFile.Items.Count <= 1)
            {
                cbAutoPostVideoFile.SelectedIndex = 0;
                return;
            }

            if (!string.IsNullOrWhiteSpace(preferredFullPath))
            {
                for (var i = 0; i < cbAutoPostVideoFile.Items.Count; i++)
                {
                    if (cbAutoPostVideoFile.Items[i] is AutoPostVideoItem item &&
                        string.Equals(item.FullPath, preferredFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        cbAutoPostVideoFile.SelectedIndex = i;
                        return;
                    }
                }
            }

            cbAutoPostVideoFile.SelectedIndex = 1;
        }

        private static IEnumerable<string> EnumerateVideoFilesInFolder(string folderPath)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp4", ".mov", ".avi", ".mkv", ".webm"
            };

            try
            {
                return Directory.GetFiles(folderPath ?? string.Empty)
                    .Where(p => allowed.Contains(Path.GetExtension(p)))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private string GetSelectedAutoPostVideoFullPath()
        {
            if (cbAutoPostVideoFile?.SelectedItem is AutoPostVideoItem item &&
                !string.IsNullOrWhiteSpace(item.FullPath))
            {
                return item.FullPath;
            }

            return string.Empty;
        }

        private void TabMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            var tab = tabMain?.SelectedTab;
            SetAiVideoGenSubNavExpanded(tab != null && ReferenceEquals(tab, tabAiVideoGen));
            HighlightSidebarForSelectedTab();
            SetStatusStripText(tab == null ? "tiktok_Omni" : $"Đang xem: {tab.Text}");
            if (tab != null && ReferenceEquals(tab, tabAiVideoGen))
            {
                BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen))
                    {
                        ApplyAiVideoGenModeUiVisibility();
                    }
                }));
            }

            if (tab != null && ReferenceEquals(tab, tabRevenueDashboard))
            {
                BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(tabMain?.SelectedTab, tabRevenueDashboard))
                    {
                        _ = RefreshRevenueDashboardUiAsync();
                    }
                }));
            }

            if (tab != null && ReferenceEquals(tab, tabAutoPost))
            {
                BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(tabMain?.SelectedTab, tabAutoPost))
                    {
                        RefreshAutoPostTabOnEnter();
                    }
                }));
            }

            if (tab != null && ReferenceEquals(tab, tabSetting))
            {
                BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(tabMain?.SelectedTab, tabSetting))
                    {
                        EnsureSettingsValidationBarVisible();
                    }
                }));
            }

            if (!_suppressUiNavigationPersist)
            {
                _ = SaveUiNavigationStateAsync();
            }
        }

        private void SetStatusStripText(string text)
        {
            if (tslStatusMain == null)
            {
                return;
            }

            void Apply()
            {
                tslStatusMain.ForeColor = Color.Gainsboro;
                tslStatusMain.Text = text ?? string.Empty;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)Apply);
            }
            else
            {
                Apply();
            }
        }

        private async Task<Dictionary<string, bool>> PerformSystemHealthCheckAsync()
        {
            return await _systemHealthCheckService.PerformSystemHealthCheckAsync().ConfigureAwait(false);
        }

        private async Task RunStartupSystemHealthCheckAsync()
        {
            try
            {
                SetStatusStripText("Đang kiểm tra hệ thống (FFmpeg, yt-dlp, API, lưu trữ)…");
                var health = await PerformSystemHealthCheckAsync().ConfigureAwait(true);
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => ApplySystemHealthToUi(health)));
                }
                else
                {
                    ApplySystemHealthToUi(health);
                }
            }
            catch (Exception ex)
            {
                Log("[Health] Kiểm tra hệ thống thất bại: " + ex.Message);
            }
        }

        private void ApplySystemHealthToUi(Dictionary<string, bool> health)
        {
            _systemHealth = health ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            bool Ok(string key) => _systemHealth.TryGetValue(key, out var v) && v;

            var ffmpegOk = Ok("ffmpeg");
            var ytdlpOk = Ok("ytdlp");
            var apiOk = Ok("api_keys");
            var storageOk = Ok("storage");

            var failures = new List<string>();
            if (!ffmpegOk)
            {
                failures.Add("FFmpeg");
            }

            if (!ytdlpOk)
            {
                failures.Add("yt-dlp");
            }

            if (!apiOk)
            {
                failures.Add("API Keys");
            }

            if (!storageOk)
            {
                failures.Add("Thư mục lưu");
            }

            if (failures.Count > 0 && tslStatusMain != null)
            {
                tslStatusMain.ForeColor = Color.FromArgb(255, 90, 90);
                tslStatusMain.Text = "⚠ Cảnh báo: " + string.Join(", ", failures) + " — một số chức năng đã tắt.";
            }
            else
            {
                SetStatusStripText("Hệ thống sẵn sàng — FFmpeg, yt-dlp, API, lưu trữ OK.");
            }

            if (btnHuntAffiliates != null)
            {
                btnHuntAffiliates.Enabled = storageOk;
            }

            if (btnDownloadSelectedAffiliate != null)
            {
                btnDownloadSelectedAffiliate.Enabled = ffmpegOk && storageOk;
            }

            if (btnGenerateGeminiPrompt != null)
            {
                btnGenerateGeminiPrompt.Enabled = apiOk;
            }

            if (btnReviewScriptBeforeRender != null)
            {
                btnReviewScriptBeforeRender.Enabled = apiOk;
            }

            if (btnProcessVideo != null)
            {
                btnProcessVideo.Enabled = ffmpegOk && apiOk && storageOk;
            }

            if (btnRunAffiliateDeepVideo != null)
            {
                UpdateShowcaseRenderButtonState(ffmpegOk, storageOk);
            }

            if (btnPhilosophyStartRender != null)
            {
                btnPhilosophyStartRender.Enabled = ffmpegOk && apiOk && storageOk;
            }

            if (btnRunMascotChannelPipeline != null)
            {
                btnRunMascotChannelPipeline.Enabled = ffmpegOk && apiOk && storageOk;
            }

            if (btnPreviewMascotVariants != null)
            {
                btnPreviewMascotVariants.Enabled = apiOk;
            }

            var pipelineReady = ffmpegOk && apiOk && storageOk;
            var geminiReady = apiOk || ResolveVideoReupGeminiReady();
            UpdateVideoReupControlReadiness(pipelineReady, geminiReady);
            SetVideoReupDataEntryControlsEnabled(true);
            RefreshVideoReupReadinessLabel(null);
            RefreshAiVideoGenModeReadinessLabels(ffmpegOk, apiOk, storageOk);
            RefreshHealthReadinessPanel(health);
            UpdateHealthOpenSettingsButton(health);

            if (btnStartAutoPost != null)
            {
                btnStartAutoPost.Enabled = storageOk;
                RefreshAutoPostStartButtonState();
            }

            Log("[Health] FFmpeg=" + (ffmpegOk ? "OK" : "FAIL") +
                " | yt-dlp=" + (ytdlpOk ? "OK" : "FAIL") +
                " | API=" + (apiOk ? "OK" : "FAIL") +
                " | Storage=" + (storageOk ? "OK" : "FAIL"));
        }

        private void InitializeVideoReupDraftAutoSave()
        {
            if (_videoReupBindingList == null)
            {
                return;
            }

            _videoReupBindingList.ListChanged += VideoReupBindingList_ListChanged;
            _videoReupDraftTimer?.Stop();
            _videoReupDraftTimer?.Dispose();
            _videoReupDraftTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _videoReupDraftTimer.Tick += VideoReupDraftTimer_Tick;
            _videoReupDraftTimer.Start();
        }

        private void VideoReupBindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.Reset && !_videoReupDraftDirty)
            {
                return;
            }

            _videoReupDraftDirty = true;
        }

        private void VideoReupDraftTimer_Tick(object sender, EventArgs e)
        {
            if (!_videoReupDraftDirty)
            {
                return;
            }

            FlushVideoReupDraftToDisk();
        }

        private void FlushVideoReupDraftToDisk()
        {
            if (_videoReupBindingList == null)
            {
                return;
            }

            _videoReupDraftStore.Save(_videoReupBindingList.ToList());
            _videoReupDraftDirty = false;
        }

        private void FlushAffiliateDraftToDisk()
        {
            if (_affiliateAllResults == null)
            {
                return;
            }

            _affiliateDraftStore.Save(_affiliateAllResults);
        }

        private void FlushHuntProductDraftToDisk()
        {
            if (_huntProductBindingList == null)
            {
                return;
            }

            _huntProductDraftStore.Save(_huntProductBindingList.ToList());
        }

        private void LoadAffiliateDraftIntoGrid()
        {
            if (_affiliateAllResults == null || _affiliateBindingList == null)
            {
                return;
            }

            var rows = _affiliateDraftStore.Load();
            if (rows.Count == 0)
            {
                return;
            }

            _affiliateAllResults.Clear();
            _affiliateAllResults.AddRange(rows);
            RefreshAffiliateGridByQualityFilter();
            Log($"[Affiliate] Đã khôi phục {rows.Count} dòng kết quả từ draft_affiliate.json.");
        }

        private void LoadHuntProductDraftIntoGrid()
        {
            if (_huntProductBindingList == null)
            {
                return;
            }

            var rows = _huntProductDraftStore.Load();
            if (rows.Count == 0)
            {
                return;
            }

            _huntProductBindingList.RaiseListChangedEvents = false;
            try
            {
                _huntProductBindingList.Clear();
                foreach (var row in rows)
                {
                    if (row != null)
                    {
                        _huntProductBindingList.Add(row);
                    }
                }
            }
            finally
            {
                _huntProductBindingList.RaiseListChangedEvents = true;
                _huntProductBindingList.ResetBindings();
            }

            Log($"[Săn SP] Đã khôi phục {rows.Count} dòng sản phẩm từ draft_hunt_product.json.");
        }

        private void LoadVideoReupDraftIntoGrid()
        {
            if (_videoReupBindingList == null)
            {
                return;
            }

            var rows = _videoReupDraftStore.Load();
            if (rows.Count == 0)
            {
                return;
            }

            _videoReupBindingList.RaiseListChangedEvents = false;
            try
            {
                _videoReupBindingList.Clear();
                foreach (var row in rows)
                {
                    if (row != null)
                    {
                        row.ProfileName = ProfileScopedPaths.ResolveProfileName(row.ProfileName);
                        EnsureVideoReupRowMusicDefault(row);
                        EnsureVideoReupRowHookSfxDefault(row);
                        ReupColorGradeHelper.EnsureRowDefaults(row, null);
                        _videoReupBindingList.Add(row);
                    }
                }
            }
            finally
            {
                _videoReupBindingList.RaiseListChangedEvents = true;
                _videoReupBindingList.ResetBindings();
            }

            _videoReupDraftDirty = false;
            LogVideoReup($"Video reup: đã khôi phục {rows.Count} dòng từ draft_reup.json.");
        }


        private bool TryGetSelectedProfileFromSettingsGrid(out string profileName)
        {
            profileName = string.Empty;
            if (dgvProxyProfiles == null || dgvProxyProfiles.SelectedRows.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Chọn một dòng trong bảng «Chrome profile» (cột Tên profile (app)).",
                    "Chú ý",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            var gridRow = dgvProxyProfiles.SelectedRows[0];
            if (gridRow.IsNewRow)
            {
                MessageBox.Show(
                    this,
                    "Dòng mới chưa lưu — nhập «Tên profile (app)» và Save Settings trước.",
                    "Chú ý",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            var profile = gridRow.DataBoundItem as AutomationProfile;
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
            {
                MessageBox.Show(
                    this,
                    "Dòng chưa có «Tên profile (app)» hợp lệ.",
                    "Chú ý",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            profileName = profile.Name.Trim();
            return true;
        }

        private async Task SaveProfilesFromGridAsync()
        {
            if (dgvProxyProfiles != null && dgvProxyProfiles.IsCurrentCellInEditMode)
            {
                dgvProxyProfiles.EndEdit();
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            settings.Profiles = BuildProxyProfilesFromGrid();
            await _configManager.SaveAsync(settings).ConfigureAwait(true);
            RefreshRunningProfileOptions(settings);
        }

        private async void btnOpenTikTokLoginBrowser_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedProfileFromSettingsGrid(out var profileName))
            {
                return;
            }

            var gridRow = dgvProxyProfiles.SelectedRows[0];
            var profile = gridRow.DataBoundItem as AutomationProfile;
            if (profile == null) return;

            this.Cursor = Cursors.WaitCursor;
            UseWaitCursor = true;
            try
            {
            Log($"[LOGIN] Bắt đầu mở Chrome (Playwright — cùng Warmup) cho nick: {profileName}...");
            if (tslStatusMain != null) tslStatusMain.Text = $"Đang chờ bạn đăng nhập TikTok cho nick: {profileName}...";

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            profile = await _configManager
                .EnsureProfileFingerprintAsync(settings, profileName, Log)
                .ConfigureAwait(true) ?? profile;

            var sharedUserDataDir = BrowserAutomation.GetSharedProfilePath(profile, profileName);
            BrowserAutomation.EnsureLegacySessionMigratedForSharedProfile(profile, profileName, Log);
            Log($"[LOGIN] user-data-dir: {sharedUserDataDir}");

            await WithBrowserLockAsync(
                async ct =>
                {
                    var browser = new BrowserAutomation();
                    var snapshot = await browser.RunInteractiveTikTokLoginAsync(
                        ct,
                        m =>
                        {
                            if (IsDisposed)
                            {
                                return;
                            }

                            if (InvokeRequired)
                            {
                                BeginInvoke(new Action(() => Log(m)));
                            }
                            else
                            {
                                Log(m);
                            }
                        },
                        profileName,
                        profile).ConfigureAwait(false);

                    if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.UniqueId))
                    {
                        profile.TikTokUniqueId = snapshot.UniqueId.Trim().TrimStart('@');
                        profile.TikTokNickname = string.IsNullOrWhiteSpace(snapshot.Nickname)
                            ? profile.TikTokUniqueId
                            : snapshot.Nickname.Trim();
                        if (string.IsNullOrWhiteSpace(profile.TikTokUserId))
                        {
                            profile.TikTokUserId = string.IsNullOrWhiteSpace(snapshot.UserId)
                                ? "ID_" + DateTime.Now.Ticks
                                : snapshot.UserId.Trim();
                        }
                    }
                },
                CancellationToken.None).ConfigureAwait(true);

            // DÙNG INVOKE ĐỂ XỬ LÝ LẠI GIAO DIỆN TRÊN LUỒNG CHÍNH
            this.Invoke(new Action(async () =>
            {
                profile.IsTTLoggedIn = !string.IsNullOrWhiteSpace(profile.TikTokUniqueId);
                await _socialAutomation.RefreshLoginStatusFromDiskAsync(profileName, profile, Log)
                    .ConfigureAwait(true);
                if (_proxyProfileBindingList != null) _proxyProfileBindingList.ResetItem(gridRow.Index);
                dgvProxyProfiles.Refresh();
                await SaveProfilesFromGridAsync();

                Log($"[LOGIN] Hoàn tất xử lý nick: {profileName}");
                if (tslStatusMain != null) tslStatusMain.Text = $"Đã xong nick: {profileName}";

                this.Cursor = Cursors.Default;
                UseWaitCursor = false;

                if (gridRow.Index + 1 < dgvProxyProfiles.Rows.Count)
                {
                    dgvProxyProfiles.ClearSelection();
                    dgvProxyProfiles.Rows[gridRow.Index + 1].Selected = true;
                }
            }));
            }
            catch (InvalidOperationException ex)
            {
                Invoke(new Action(() =>
                {
                    Log("[LOGIN] " + ex.Message);
                    MessageBox.Show(this, ex.Message, "Không mở được Chrome", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    Log("[LOGIN] Lỗi: " + ex.Message);
                    MessageBox.Show(
                        this,
                        "Không mở được Chrome:\n" + ex.Message,
                        "Lỗi đăng nhập TikTok",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }));
            }
            finally
            {
                Invoke(new Action(() =>
                {
                    Cursor = Cursors.Default;
                    UseWaitCursor = false;
                }));
            }
        }

        private void btnCheckBrowserProfileHealth_Click(object sender, EventArgs e)
        {
            try
            {
                var rows = BrowserAutomation.ScanBrowserProfileHealth();
                if (rows.Count == 0)
                {
                    Log("[ProfileHealth] Không thấy thư mục con trong «browser_profile» (hoặc thư mục chưa tồn tại).");
                    MessageBox.Show(this,
                        "Chưa có profile nào trong thư mục browser_profile cạnh file .exe.\nChạy warm-up hoặc đăng nhập thủ công ít nhất một lần để tạo profile.",
                        "Kiểm tra profile",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var sb = new StringBuilder();
                foreach (var row in rows.OrderBy(r => r.folderName, StringComparer.OrdinalIgnoreCase))
                {
                    var lw = row.cookiesLastWriteUtc.HasValue
                        ? row.cookiesLastWriteUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                        : "—";
                    var line =
                        $"• {row.folderName}: cookies={(row.hasCookies ? "có" : "KHÔNG")}, sửa Cookies lần cuối (UTC): {lw}";
                    Log("[ProfileHealth] " + line);
                    sb.AppendLine(line);
                }

                MessageBox.Show(this,
                    sb + "\nChi tiết đầy đủ đã ghi vào log — có thể copy dán khi báo lỗi.",
                    "Sức khỏe profile (browser_profile)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("[ProfileHealth] Lỗi quét: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Kiểm tra profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void dgvProxyProfiles_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvProxyProfiles == null || dgvProxyProfiles.SelectedRows.Count == 0)
            {
                return;
            }

            var row = dgvProxyProfiles.SelectedRows[0];
            if (row?.DataBoundItem is AutomationProfile profile && !string.IsNullOrWhiteSpace(profile.Name))
            {
                var name = profile.Name.Trim();
                SelectRunningProfileInUi(name);
                ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(_storageRootPathCache, name);
                Log("[Storage] Đã đảm bảo thư mục Original / Processed / Reup / Failed cho profile «" + name + "».");
                RefreshProfileLoginStateFromDisk(profile, persist: false);
                dgvProxyProfiles.InvalidateRow(row.Index);
            }
        }

        private void dgvProxyProfiles_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvProxyProfiles == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = dgvProxyProfiles.Columns[e.ColumnIndex];
            if (col == null)
            {
                return;
            }

            var row = dgvProxyProfiles.Rows[e.RowIndex];
            if (!(row?.DataBoundItem is AutomationProfile profile))
            {
                return;
            }

            if (col.Name == "colProfileMascotImage")
            {
                var profileName = (profile.Name ?? string.Empty).Trim();
                var hasMascot = !string.IsNullOrEmpty(profileName)
                    && AvatarIdentityPackStore.TryGetMascotImagePath(profileName, out _, out _);
                e.Value = hasMascot ? "✓ Có ảnh" : "📷 Chọn";
                e.FormattingApplied = true;
                e.CellStyle.BackColor = hasMascot ? Color.FromArgb(28, 92, 52) : Color.FromArgb(60, 64, 77);
                e.CellStyle.ForeColor = Color.White;
                e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;
                e.CellStyle.SelectionForeColor = Color.White;
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                return;
            }

            // Chỉ hiện tên nền tảng khi đã đăng nhập; chưa login → ô trống.
            switch (col.Name)
            {
                case "colProfileTikTokNick":
                    e.Value = profile.IsTTLoggedIn
                        ? (profile.TikTokUniqueId ?? string.Empty).Trim()
                        : string.Empty;
                    e.FormattingApplied = true;
                    return;
                case "colProfileTikTokName":
                    e.Value = profile.IsTTLoggedIn
                        ? (profile.TikTokNickname ?? string.Empty).Trim()
                        : string.Empty;
                    e.FormattingApplied = true;
                    return;
                case "colProfileFacebookName":
                    e.Value = profile.IsFBLoggedIn
                        ? (profile.FacebookName ?? string.Empty).Trim()
                        : string.Empty;
                    e.FormattingApplied = true;
                    return;
                case "colProfileYouTubeName":
                    e.Value = profile.IsYTLoggedIn
                        ? (profile.YouTubeName ?? string.Empty).Trim()
                        : string.Empty;
                    e.FormattingApplied = true;
                    return;
            }
        }

        private void dgvProxyProfiles_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (dgvProxyProfiles == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (dgvProxyProfiles.Columns[e.ColumnIndex]?.Name != "colProfileMascotImage")
            {
                return;
            }

            if (!(dgvProxyProfiles.Rows[e.RowIndex].DataBoundItem is AutomationProfile profile))
            {
                return;
            }

            var profileName = (profile.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(profileName))
            {
                e.ToolTipText = "Nhập «Tên profile» trước khi chọn ảnh.";
                return;
            }

            var dir = AvatarIdentityPackStore.GetProfileDirectory(profileName);
            if (AvatarIdentityPackStore.TryGetMascotImagePath(profileName, out var mascotPath, out _))
            {
                e.ToolTipText =
                    "✓ Đã có ảnh profile (hook intro Video reup)\r\n" +
                    "File: " + mascotPath + "\r\n" +
                    "Thư mục: " + dir + "\r\n" +
                    "Bấm nút để đổi ảnh khác.";
                return;
            }

            e.ToolTipText =
                "Chưa có ảnh — bấm để chọn\r\n" +
                "Lưu tại: " + dir + "\r\n" +
                "Tên file: mascot.png / .jpg / .webp";
        }

        private void dgvProxyProfiles_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            if (e.ColumnIndex < 0 || e.RowIndex < 0 || dgvProxyProfiles == null)
            {
                return;
            }

            var col = dgvProxyProfiles.Columns[e.ColumnIndex];
            if (col?.DataPropertyName == "ProxyPort" &&
                dgvProxyProfiles.Rows[e.RowIndex].DataBoundItem is AutomationProfile profile)
            {
                profile.ProxyPort = 0;
            }
        }

        private void RefreshProfileLoginStateFromDisk(AutomationProfile profile, bool persist)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
            {
                return;
            }

            var probe = ProfileSessionProbe.ProbeFromDisk(profile, profile.Name.Trim());
            ProfileSessionProbe.ApplyToProfile(profile, probe);
            if (persist)
            {
                _ = PersistProfileLoginFlagsAsync(profile);
            }
        }

        private void RefreshAllProfileLoginStatesFromDisk(bool persist)
        {
            if (_proxyProfileBindingList == null)
            {
                return;
            }

            foreach (var profile in _proxyProfileBindingList)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                {
                    continue;
                }

                RefreshProfileLoginStateFromDisk(profile, persist: false);
            }

            dgvProxyProfiles?.Refresh();
            if (persist)
            {
                _ = SaveProfilesFromGridAsync();
            }
        }

        private async Task PersistProfileLoginFlagsAsync(AutomationProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            await _configManager.UpdateProfileSocialLoginStatusAsync(
                profile.Name,
                profile.IsTTLoggedIn,
                profile.IsFBLoggedIn,
                profile.IsYTLoggedIn,
                Log).ConfigureAwait(true);
        }

        private async void btnLoginAllSocial_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedProfileFromSettingsGrid(out var profileName))
            {
                return;
            }

            var gridRow = dgvProxyProfiles.SelectedRows[0];
            var profile = gridRow.DataBoundItem as AutomationProfile;
            if (profile == null)
            {
                return;
            }

            if (btnLoginAllSocial != null)
            {
                btnLoginAllSocial.Enabled = false;
            }

            UseWaitCursor = true;
            Cursor = Cursors.WaitCursor;
            var loginCts = RegisterActiveJobCancellation();
            try
            {
                var probe = await _socialAutomation.LoginAllSocialAsync(
                    profileName,
                    profile,
                    loginCts.Token,
                    Log).ConfigureAwait(true);

                _proxyProfileBindingList?.ResetItem(gridRow.Index);
                dgvProxyProfiles?.Refresh();
                await SaveProfilesFromGridAsync().ConfigureAwait(true);

                MessageBox.Show(
                    this,
                    "TikTok: " + (probe.TikTokLoggedIn ? "Đã login" : "Chưa") + "\n" +
                    "Facebook: " + (probe.FacebookLoggedIn ? "Đã login" : "Chưa") + "\n" +
                    "YouTube: " + (probe.YouTubeLoggedIn ? "Đã login" : "Chưa") +
                    (probe.HasCookieDatabase ? string.Empty : "\n\n(Chưa thấy file Cookies — thử đăng nhập lại và đóng Chrome đúng cách.)"),
                    "Đăng nhập 3 nền tảng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("[LOGIN/ALL] " + ex.Message);
                MessageBox.Show(this, ex.Message, "Đăng nhập 3 nền tảng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                Cursor = Cursors.Default;
                DisposeActiveJobCancellation();
                if (btnLoginAllSocial != null && !btnLoginAllSocial.IsDisposed)
                {
                    btnLoginAllSocial.Enabled = true;
                }
            }
        }

        private async void btnRefreshProfileLoginStatus_Click(object sender, EventArgs e)
        {
            btnRefreshProfileLoginStatus.Enabled = false;
            try
            {
                RefreshAllProfileLoginStatesFromDisk(persist: true);
                Log("[LOGIN] Đã quét lại Cookies/Local State cho tất cả profile trên lưới.");
            }
            finally
            {
                btnRefreshProfileLoginStatus.Enabled = true;
            }
        }

        private async void btnLoginFacebook_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedProfileFromSettingsGrid(out var profileName)) return;
            var gridRow = dgvProxyProfiles.SelectedRows[0];
            var profile = gridRow.DataBoundItem as AutomationProfile;
            if (profile == null) return;

            UseWaitCursor = true;
            Cursor = Cursors.WaitCursor;
            try
            {
                var ok = await _socialAutomation.LoginFacebookAsync(
                    profileName,
                    profile,
                    CancellationToken.None,
                    Log).ConfigureAwait(true);
                await _socialAutomation.RefreshLoginStatusFromDiskAsync(profileName, profile, Log)
                    .ConfigureAwait(true);
                _proxyProfileBindingList?.ResetItem(gridRow.Index);
                dgvProxyProfiles?.Refresh();
                await SaveProfilesFromGridAsync().ConfigureAwait(true);
                MessageBox.Show(
                    this,
                    ok ? "Facebook: đã phát hiện phiên đăng nhập." : "Chưa xác nhận đăng nhập Facebook.",
                    "Đăng nhập Facebook",
                    MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Log("[LOGIN/FB] " + ex.Message);
                MessageBox.Show(this, ex.Message, "Đăng nhập Facebook", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                Cursor = Cursors.Default;
            }
        }

        private async void btnLoginYouTube_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedProfileFromSettingsGrid(out var profileName)) return;
            var gridRow = dgvProxyProfiles.SelectedRows[0];
            var profile = gridRow.DataBoundItem as AutomationProfile;
            if (profile == null) return;

            UseWaitCursor = true;
            Cursor = Cursors.WaitCursor;
            try
            {
                var ok = await _socialAutomation.LoginYouTubeAsync(
                    profileName,
                    profile,
                    CancellationToken.None,
                    Log).ConfigureAwait(true);
                await _socialAutomation.RefreshLoginStatusFromDiskAsync(profileName, profile, Log)
                    .ConfigureAwait(true);
                _proxyProfileBindingList?.ResetItem(gridRow.Index);
                dgvProxyProfiles?.Refresh();
                await SaveProfilesFromGridAsync().ConfigureAwait(true);
                MessageBox.Show(
                    this,
                    ok ? "YouTube: đã phát hiện phiên đăng nhập." : "Chưa xác nhận đăng nhập YouTube.",
                    "Đăng nhập YouTube",
                    MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Log("[LOGIN/YT] " + ex.Message);
                MessageBox.Show(this, ex.Message, "Đăng nhập YouTube", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                Cursor = Cursors.Default;
            }
        }

        private VideoStorageType GetSelectedAutoPostVideoType()
        {
            if (cbAutoPostVideoType?.SelectedItem is VideoStorageType t)
            {
                return t;
            }

            return VideoStorageType.Reup;
        }

        private void SyncAutoPostFolderFromProfileAndType()
        {
            var profile = ProfileScopedPaths.ResolveProfileName(
                cbAutoPostProfile?.SelectedItem?.ToString() ?? GetRunningProfileName());
            var folder = ProfileScopedPaths.GetVideoTypeFolder(
                _storageRootPathCache,
                profile,
                GetSelectedAutoPostVideoType(),
                create: true);
            if (txtAutoPostFolder != null)
            {
                txtAutoPostFolder.Text = folder;
            }

            RefreshAutoPostVideoCombo();
        }

        private void cbAutoPostProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncAutoPostFolderFromProfileAndType();
        }

        private void cbAutoPostVideoType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncAutoPostFolderFromProfileAndType();
        }


        private async void Form1_Shown(object sender, EventArgs e)
        {
            InitializeJobQueueInfrastructure();
            _ = ResumeAsyncVeoTasksOnStartupAsync();
            await LoadSettingsIntoUiAsync();
            var navSettings = await _configManager.LoadAsync().ConfigureAwait(true);
            RestoreUiNavigationFromSettings(navSettings);
            RefreshVideoOcrService();
            LoadAffiliateHuntResultsFromDisk();
            await RunStartupSystemHealthCheckAsync().ConfigureAwait(true);

            BeginUiLayoutBatch();
            try
            {
                LoadVideoReupDraftIntoGrid();
                LoadSlideshowDraftIntoBuffer();
                LoadShowcaseDraftIntoBuffer();
                LoadPhilosophyDraftIntoGrid();
                InitializeShowcaseTrashMaintenance();
                LoadAffiliateDraftIntoGrid();
                LoadHuntProductDraftIntoGrid();
                LoadSchedule();
                _jobWorkerService?.RefreshConcurrencyLimit();
            }
            finally
            {
                EndUiLayoutBatch();
            }

            EnsureShowcaseGridHydratedAfterStartup();

            _ = RefreshVideoReupMusicComboAsync();
            await RefreshResumeStateAsync();
            await LoadWarmupQueueAsync();
            _warmupQueueHistory = await _warmupQueueHistoryManager.LoadAsync();
            _approvalQueueItems = await _approvalQueueManager.LoadAsync();
            var paused = await _warmupQueueStateManager.LoadPausedFlagAsync();
            _isWarmupQueuePaused = paused && (_warmupQueueScheduler?.QueueCount ?? 0) > 0;

            BeginUiLayoutBatch();
            try
            {
                RefreshQueueStatsSummary();
                RefreshWarmupQueueStatus();
                SetStatusStripText(tabMain?.SelectedTab == null ? "tiktok_Omni" : $"Đang xem: {tabMain.SelectedTab.Text}");
                HighlightSidebarForSelectedTab();
                ApplyGlobalLogChrome();
                if (ReferenceEquals(tabMain?.SelectedTab, tabAffiliateHunter))
                {
                    EnsureAffiliateFiltersLayout();
                }
            }
            finally
            {
                EndUiLayoutBatch();
            }

            _ = WarmupBundledToolingInBackgroundAsync();
            _ = TryDailyAffiliateRevenueFetchOnStartupAsync();
            var runnableJobs = CountRunnableQueueRows();
            var queueJobs = _warmupQueueScheduler?.QueueCount ?? 0;
            if (queueJobs > 0 || runnableJobs > 0)
            {
                if (_isWarmupQueuePaused)
                {
                    Log("[QUEUE] Hàng đợi đang tạm dừng (lưu từ phiên trước). Bấm «Chạy» khi bạn sẵn sàng — không tự chạy khi mở app.");
                }
                else
                {
                    Log("[QUEUE] Có " + runnableJobs + " job trong hàng đợi — bấm «Chạy» khi bạn sẵn sàng (không tự chạy khi mở app).");
                }
            }
        }

        private const string DefaultVeoEndpointPlaceholder = "https://api.veo.example.com/v1/videos";

        /// <summary>Tự điền mặc định + phát hiện ffmpeg/yt-dlp/nhạc khi mở Cài đặt. Trả về true nếu cần lưu file.</summary>
        private static bool ApplySettingsAutoFillAndDetect(AppSettings settings, ICollection<string> notes)
        {
            if (settings == null)
            {
                return false;
            }

            var changed = false;
            void Note(string line)
            {
                notes?.Add(line);
            }

            var aiUnset = string.IsNullOrWhiteSpace(settings.AiApiKey);
            if (string.IsNullOrWhiteSpace(settings.AiProvider) ||
                (aiUnset && string.Equals(settings.AiProvider, "gemini", StringComparison.OrdinalIgnoreCase)))
            {
                settings.AiProvider = "Google";
                changed = true;
                Note("AI Provider → Google");
            }

            if (string.IsNullOrWhiteSpace(settings.AiModel) ||
                (aiUnset && string.Equals(settings.AiModel, "gemini-2.0-flash", StringComparison.OrdinalIgnoreCase)))
            {
                settings.AiModel = "gemini-2.5-flash";
                changed = true;
                Note("AI Model → gemini-2.5-flash");
            }

            if (string.IsNullOrWhiteSpace(settings.VeoEndpoint))
            {
                settings.VeoEndpoint = DefaultVeoEndpointPlaceholder;
                changed = true;
                Note("URL gateway Veo → placeholder (thay bằng URL API thật khi dùng Veo)");
            }

            if (settings.BlockPostingSafetyScoreBelow <= 0)
            {
                settings.BlockPostingSafetyScoreBelow = 75;
                changed = true;
                Note("Điểm an toàn đăng bài → 75");
            }

            if (settings.WatchSecondsMin <= 0)
            {
                settings.WatchSecondsMin = DefaultWatchMinutesMin * 60;
                changed = true;
                Note("Thời gian xem tối thiểu → 5 phút");
            }

            if (settings.WatchSecondsMax <= 0)
            {
                settings.WatchSecondsMax = DefaultWatchMinutesMax * 60;
                changed = true;
                Note("Thời gian xem tối đa → 10 phút");
            }

            if (settings.WatchSecondsMax < settings.WatchSecondsMin)
            {
                settings.WatchSecondsMax = Math.Max(settings.WatchSecondsMin, DefaultWatchMinutesMax * 60);
                changed = true;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            if (string.IsNullOrWhiteSpace(settings.FfmpegPath))
            {
                var ffmpeg = FindBundledToolExecutable(
                    "ffmpeg.exe",
                    "ffmpeg.exe",
                    Path.Combine("Tools", "ffmpeg", "bin", "ffmpeg.exe"),
                    Path.Combine("Tools", "ffmpeg", "ffmpeg.exe"));
                if (!string.IsNullOrWhiteSpace(ffmpeg))
                {
                    settings.FfmpegPath = ffmpeg;
                    changed = true;
                    Note("FFmpeg → " + ffmpeg);
                }
            }

            if (string.IsNullOrWhiteSpace(settings.YtDlpPath))
            {
                var ytdlp = FindBundledToolExecutable("yt-dlp.exe", "yt-dlp.exe");
                if (!string.IsNullOrWhiteSpace(ytdlp))
                {
                    settings.YtDlpPath = ytdlp;
                    changed = true;
                    Note("yt-dlp → " + ytdlp);
                }
            }

            if (string.IsNullOrWhiteSpace(settings.VideoReupMusicLibraryPath))
            {
                try
                {
                    OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);
                    Note("Kho âm thanh dùng chung: " + OmniAudioLibrary.GetSharedAudioRoot(settings));
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                settings.VideoReupMusicLibraryPath = string.Empty;
                changed = true;
                Note("Nhạc/SFX chỉ dùng Assets\\Audio\\Music và Sfx — đã bỏ đường dẫn nhạc tùy chỉnh cũ.");
                try
                {
                    OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);
                }
                catch
                {
                    // ignored
                }
            }

            return changed;
        }

        private static string FindBundledToolExecutable(string fileName, params string[] relativePaths)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<string>();
            void Add(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                try
                {
                    path = Path.GetFullPath(path);
                }
                catch
                {
                    return;
                }

                if (seen.Add(path))
                {
                    candidates.Add(path);
                }
            }

            Add(Path.Combine(baseDir, fileName));
            if (relativePaths != null)
            {
                foreach (var rel in relativePaths)
                {
                    Add(Path.Combine(baseDir, rel));
                }
            }

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private async Task LoadSettingsIntoUiAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync();
                var autoNotes = new List<string>();
                var autoFilled = ApplySettingsAutoFillAndDetect(settings, autoNotes);
                if (autoFilled)
                {
                    await _configManager.SaveAsync(settings).ConfigureAwait(true);
                }

                txtAiProvider.Text = settings.AiProvider;
                txtAiModel.Text = settings.AiModel;
                txtAiApiKey.Text = settings.AiApiKey;
                txtTwoCaptchaApiKey.Text = settings.TwoCaptchaApiKey;
                txtTikTokRapidApiKey.Text = settings.TikTokRapidApiKey;
                txtVeoApiKey.Text = settings.VeoApiKey;
                txtTtsApiKey.Text = settings.TtsApiKey;
                txtVeoEndpoint.Text = settings.VeoEndpoint;
                txtFfmpegPath.Text = settings.FfmpegPath;
                _storageRootPathCache = string.Empty;
                ProfileScopedPaths.SetConfiguredStorageRoot(string.Empty);
                settings.StorageRootPath = string.Empty;

                if (txtYtDlpPath != null)
                {
                    txtYtDlpPath.Text = settings.YtDlpPath ?? string.Empty;
                }

                if (txtVideoReupMusicLibraryPath != null)
                {
                    txtVideoReupMusicLibraryPath.Text = string.Empty;
                }

                if (txtVoiceIdFemaleYoung != null)
                {
                    txtVoiceIdFemaleYoung.Text = settings.VoiceId_FemaleYoung ?? string.Empty;
                }

                if (txtVoiceIdFemaleMature != null)
                {
                    txtVoiceIdFemaleMature.Text = settings.VoiceId_FemaleMature ?? string.Empty;
                }

                if (txtVoiceIdMaleYoung != null)
                {
                    txtVoiceIdMaleYoung.Text = settings.VoiceId_MaleYoung ?? string.Empty;
                }

                if (txtVoiceIdMaleMature != null)
                {
                    txtVoiceIdMaleMature.Text = settings.VoiceId_MaleMature ?? string.Empty;
                }

                if (txtVoiceIdGirlChild != null)
                {
                    txtVoiceIdGirlChild.Text = settings.VoiceId_GirlChild ?? string.Empty;
                }

                if (txtVoiceIdBoyChild != null)
                {
                    txtVoiceIdBoyChild.Text = settings.VoiceId_BoyChild ?? string.Empty;
                }

                if (cbGeminiStyleTemplate != null)
                {
                    var style = GeminiStyleTemplateExtensions.Parse(settings.GeminiStyleTemplate);
                    cbGeminiStyleTemplate.SelectedItem = style;
                }

                if (chkAutoFetchAffiliateRevenueOnStartup != null)
                {
                    chkAutoFetchAffiliateRevenueOnStartup.Checked = settings.AutoFetchAffiliateRevenueOnStartup ?? false;
                }
                _suppressApprovalBehaviorPersist = true;
                try
                {
                    if (chkAlwaysRequirePrePostApproval != null)
                    {
                        chkAlwaysRequirePrePostApproval.Checked = settings.AlwaysRequirePrePostApproval ?? true;
                    }

                    if (chkAlwaysRequirePreRenderApproval != null)
                    {
                        chkAlwaysRequirePreRenderApproval.Checked = settings.AlwaysRequirePreRenderApproval ?? false;
                    }

                    if (chkAutoRunApprovedQueue != null)
                    {
                        chkAutoRunApprovedQueue.Checked = settings.AutoRunApprovedQueue ?? false;
                    }
                }
                finally
                {
                    _suppressApprovalBehaviorPersist = false;
                }
                if (chkAffiliateAutoEnrich != null)
                {
                    chkAffiliateAutoEnrich.Checked = settings.AffiliateAutoEnrichEnabled;
                }

                if (chkAffiliateRankByEngagement != null)
                {
                    chkAffiliateRankByEngagement.Checked = settings.AffiliateRankByEngagementEnabled;
                }

                if (numAffiliateBufferMultiplier != null)
                {
                    var m = settings.AffiliateHuntBufferMultiplier;
                    if (double.IsNaN(m) || m < 1.5d)
                    {
                        m = 1.5d;
                    }

                    if (m > 5.0d)
                    {
                        m = 5.0d;
                    }

                    numAffiliateBufferMultiplier.Value = (decimal)m;
                }

                if (cbTikTokHuntMethod != null)
                {
                    var methodIndex = TikTokHuntMethods.IsRapidApi(settings.TikTokHuntMethod) ? 0 : 1;
                    if (methodIndex >= 0 && methodIndex < cbTikTokHuntMethod.Items.Count)
                    {
                        cbTikTokHuntMethod.SelectedIndex = methodIndex;
                    }
                }

                if (chkTikTokApiFallbackBrowser != null)
                {
                    chkTikTokApiFallbackBrowser.Checked = settings.TikTokRapidApiFallbackToBrowser;
                }

                UpdateAffiliateRankControlsEnabledState();
                if (numMaxConcurrentJobs != null)
                {
                    numMaxConcurrentJobs.Value = ClampNumericValue(settings.MaxConcurrentJobs, numMaxConcurrentJobs);
                }
                // numWatchMin/Max = % giữ chân warm-up — không map từ WatchSeconds* (phút cũ).
                ApplyWatchPercentageToUi(numWatchMin, DefaultWatchPercentageMin);
                ApplyWatchPercentageToUi(numWatchMax, DefaultWatchPercentageMax);
                numAiTransitionDuration.Value = ClampNumericValue(settings.VideoTransitionDurationSeconds, numAiTransitionDuration);
                numAiTextSize.Value = ClampNumericValue(settings.VideoTextSize, numAiTextSize);
                numAiMusicVolume.Value = ClampNumericValue(settings.VideoMusicVolume, numAiMusicVolume);
                if (numWatchMin != null && numWatchMax != null && numWatchMax.Value < numWatchMin.Value)
                {
                    numWatchMax.Value = numWatchMin.Value;
                }
                PopulateProfileBindingListFromSettings(settings);
                RefreshAllProfileLoginStatesFromDisk(persist: false);
                RefreshRunningProfileOptions(settings);
                RefreshAutoPostVideoCombo();
                SyncAutoPostFolderFromProfileAndType();
                ValidateSettingsInputs();
                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
                if (autoFilled)
                {
                    Log("Settings loaded — đã tự điền và lưu: " + string.Join("; ", autoNotes));
                }
                else
                {
                    Log("Settings loaded.");
                }
            }
            catch (Exception ex)
            {
                Log("Failed to load settings: " + ex.Message);
            }
        }

        private async void btnSaveSettings_Click(object sender, EventArgs e)
        {
            btnSaveSettings.Enabled = false;

            try
            {
                if (!ValidateSettingsInputs())
                {
                    Log("Cannot save settings: please fix validation errors.");
                    return;
                }

                if (dgvProxyProfiles.IsCurrentCellInEditMode)
                {
                    dgvProxyProfiles.EndEdit();
                }

                var settings = await _configManager.LoadAsync();
                settings.AiProvider = txtAiProvider.Text.Trim();
                settings.AiModel = txtAiModel.Text.Trim();
                settings.AiApiKey = txtAiApiKey.Text.Trim();
                settings.TwoCaptchaApiKey = txtTwoCaptchaApiKey.Text.Trim();
                settings.TikTokRapidApiKey = txtTikTokRapidApiKey.Text.Trim();
                settings.VeoApiKey = txtVeoApiKey.Text.Trim();
                settings.TtsApiKey = txtTtsApiKey.Text.Trim();
                settings.VeoEndpoint = txtVeoEndpoint.Text.Trim();
                // TtsEndpoint không còn ô nhập riêng — tự suy từ Voice ID persona đầu tiên có giá trị
                // (giữ cho IsElevenLabsConfigured/EndpointIncludesVoiceId hoạt động cho các tab TTS cũ).
                var firstVoiceId = ResolveFirstSettingsVoiceId();
                settings.TtsEndpoint = string.IsNullOrWhiteSpace(firstVoiceId)
                    ? string.Empty
                    : ElevenLabsTtsHelper.NormalizeSettingsEndpoint(firstVoiceId);
                settings.FfmpegPath = NormalizeSettingsFfmpegPathForSave(txtFfmpegPath.Text);
                txtFfmpegPath.Text = settings.FfmpegPath;
                settings.StorageRootPath = string.Empty;
                _storageRootPathCache = string.Empty;
                ProfileScopedPaths.SetConfiguredStorageRoot(string.Empty);
                settings.YtDlpPath = (txtYtDlpPath?.Text ?? string.Empty).Trim();
                settings.VideoReupMusicLibraryPath = string.Empty;
                // HookStockClipsRoot / VoiceId_Reup*: không còn trên UI — giữ nguyên giá trị đã load từ file.
                settings.VoiceId_FemaleYoung = (txtVoiceIdFemaleYoung?.Text ?? string.Empty).Trim();
                settings.VoiceId_FemaleMature = (txtVoiceIdFemaleMature?.Text ?? string.Empty).Trim();
                settings.VoiceId_MaleYoung = (txtVoiceIdMaleYoung?.Text ?? string.Empty).Trim();
                settings.VoiceId_MaleMature = (txtVoiceIdMaleMature?.Text ?? string.Empty).Trim();
                settings.VoiceId_GirlChild = (txtVoiceIdGirlChild?.Text ?? string.Empty).Trim();
                settings.VoiceId_BoyChild = (txtVoiceIdBoyChild?.Text ?? string.Empty).Trim();
                settings.GeminiStyleTemplate = GetSelectedGeminiStyleTemplate().ToString();
                // AutoResumeQueueOnStartup: giữ giá trị đã load từ file (mặc định false).
                settings.AutoFetchAffiliateRevenueOnStartup = chkAutoFetchAffiliateRevenueOnStartup?.Checked ?? false;
                settings.AlwaysRequirePrePostApproval = chkAlwaysRequirePrePostApproval?.Checked ?? true;
                settings.AlwaysRequirePreRenderApproval = chkAlwaysRequirePreRenderApproval?.Checked ?? false;
                settings.AutoRunApprovedQueue = chkAutoRunApprovedQueue?.Checked ?? false;
                // Điểm an toàn: không còn UI AutoPost — giữ giá trị đã load từ settings.
                settings.MaxConcurrentJobs = (int)(numMaxConcurrentJobs?.Value ?? 2);
                _jobWorkerService?.RefreshConcurrencyLimit();
                // Giữ WatchSeconds* trong file settings (legacy); UI warm-up dùng % giữ chân riêng.
                settings.VideoTransitionDurationSeconds = numAiTransitionDuration == null ? settings.VideoTransitionDurationSeconds : (double)numAiTransitionDuration.Value;
                settings.VideoTextSize = numAiTextSize == null ? settings.VideoTextSize : (int)numAiTextSize.Value;
                settings.VideoMusicVolume = numAiMusicVolume == null ? settings.VideoMusicVolume : (int)numAiMusicVolume.Value;
                settings.Profiles = BuildProxyProfilesFromGrid();

                if (chkAffiliateRankByEngagement != null)
                {
                    settings.AffiliateRankByEngagementEnabled = chkAffiliateRankByEngagement.Checked;
                }

                if (numAffiliateBufferMultiplier != null)
                {
                    settings.AffiliateHuntBufferMultiplier = (double)numAffiliateBufferMultiplier.Value;
                }

                if (chkAffiliateAutoEnrich != null)
                {
                    settings.AffiliateAutoEnrichEnabled = chkAffiliateAutoEnrich.Checked;
                }

                if (cbTikTokHuntMethod != null && cbTikTokHuntMethod.SelectedIndex >= 0)
                {
                    settings.TikTokHuntMethod = cbTikTokHuntMethod.SelectedIndex == 0
                        ? TikTokHuntMethods.RapidApi
                        : TikTokHuntMethods.Browser;
                }

                if (chkTikTokApiFallbackBrowser != null)
                {
                    settings.TikTokRapidApiFallbackToBrowser = chkTikTokApiFallbackBrowser.Checked;
                }

                await _configManager.SaveAsync(settings);
                RefreshRunningProfileOptions(settings);
                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
                Log("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Log("Failed to save settings: " + ex.Message);
            }
            finally
            {
                ValidateSettingsInputs();
            }
        }

        private static string TruncateSettingsTestMessage(string text, int maxLen = 900)
        {
            var s = (text ?? string.Empty).Trim();
            if (s.Length <= maxLen)
            {
                return s;
            }

            return s.Substring(0, maxLen) + "…";
        }

        private static bool LooksLikePlaceholderApiEndpoint(string endpoint)
        {
            var u = (endpoint ?? string.Empty).Trim();
            return u.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Thông báo ngay trên màn Cài đặt + ghi log Auto Warmup.</summary>
        private void NotifySettingsApiTest(string serviceName, bool success, bool warning, string detail)
        {
            var title = serviceName + (success ? " — Key hoạt động" : warning ? " — Cảnh báo" : " — Key không dùng được");
            var icon = success
                ? MessageBoxIcon.Information
                : warning
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Error;
            var body = string.IsNullOrWhiteSpace(detail)
                ? (success ? "Kết nối API thành công." : "Không kết nối được API.")
                : detail;
            Log((success ? "[Test] " : warning ? "[Test cảnh báo] " : "[Test lỗi] ") + serviceName + ": " + body.Replace("\r\n", " "));
            MessageBox.Show(this, TruncateSettingsTestMessage(body), title, MessageBoxButtons.OK, icon);
        }

        private async void btnTestAi_Click(object sender, EventArgs e)
        {
            btnTestAi.Enabled = false;
            var prevText = btnTestAi.Text;
            btnTestAi.Text = "…";
            try
            {
                if (string.IsNullOrWhiteSpace(txtAiApiKey.Text))
                {
                    NotifySettingsApiTest("AI (Gemini)", false, false, "Chưa nhập AI API Key.");
                    return;
                }

                var geminiService = new GeminiService();
                var text = await geminiService.GenerateScriptAsync(
                    "Trả lời đúng một từ: OK",
                    txtAiProvider.Text.Trim(),
                    txtAiApiKey.Text.Trim(),
                    txtAiModel.Text.Trim()).ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(text))
                {
                    NotifySettingsApiTest(
                        "AI (Gemini)",
                        false,
                        true,
                        "API phản hồi nhưng nội dung trống.\r\nKiểm tra model («" + txtAiModel.Text.Trim() + "») và quota.");
                    return;
                }

                NotifySettingsApiTest(
                    "AI (Gemini)",
                    true,
                    false,
                    "Key hoạt động.\r\nModel: " + txtAiModel.Text.Trim() + "\r\nPhản hồi mẫu: " + TruncateSettingsTestMessage(text, 120));
            }
            catch (Exception ex)
            {
                NotifySettingsApiTest("AI (Gemini)", false, false, FormatApiTestException(ex));
            }
            finally
            {
                btnTestAi.Text = prevText;
                btnTestAi.Enabled = true;
            }
        }

        private async void btnTestTwoCaptcha_Click(object sender, EventArgs e)
        {
            btnTestTwoCaptcha.Enabled = false;
            var prevText = btnTestTwoCaptcha.Text;
            btnTestTwoCaptcha.Text = "…";
            try
            {
                if (string.IsNullOrWhiteSpace(txtTwoCaptchaApiKey.Text))
                {
                    NotifySettingsApiTest("2Captcha", false, false, "Chưa nhập 2Captcha API Key.");
                    return;
                }

                var service = new CaptchaService();
                var balance = await service.GetBalanceAsync(txtTwoCaptchaApiKey.Text.Trim(), CancellationToken.None).ConfigureAwait(true);
                NotifySettingsApiTest(
                    "2Captcha",
                    true,
                    false,
                    "Key hoạt động.\r\nSố dư tài khoản: " + balance.ToString("0.####", CultureInfo.InvariantCulture) + " USD");
            }
            catch (Exception ex)
            {
                NotifySettingsApiTest("2Captcha", false, false, FormatApiTestException(ex));
            }
            finally
            {
                btnTestTwoCaptcha.Text = prevText;
                btnTestTwoCaptcha.Enabled = true;
            }
        }

        private async void btnTestTikTokRapidApi_Click(object sender, EventArgs e)
        {
            btnTestTikTokRapidApi.Enabled = false;
            var prevText = btnTestTikTokRapidApi.Text;
            btnTestTikTokRapidApi.Text = "…";
            try
            {
                if (string.IsNullOrWhiteSpace(txtTikTokRapidApiKey.Text))
                {
                    NotifySettingsApiTest("TikTok RapidAPI", false, false, "Chưa nhập TikTok RapidAPI Key.");
                    return;
                }

                var service = new TikTokRapidApiService();
                var ok = await service.TestConnectionAsync(txtTikTokRapidApiKey.Text.Trim(), CancellationToken.None)
                    .ConfigureAwait(true);
                if (!ok)
                {
                    NotifySettingsApiTest(
                        "TikTok RapidAPI",
                        false,
                        true,
                        "API phản hồi nhưng không trả về video mẫu. Kiểm tra quota hoặc thử lại sau.");
                    return;
                }

                NotifySettingsApiTest(
                    "TikTok RapidAPI (tiktok-api23)",
                    true,
                    false,
                    "Key hoạt động — đã lấy được video mẫu từ RapidAPI.");
            }
            catch (Exception ex)
            {
                NotifySettingsApiTest("TikTok RapidAPI", false, false, FormatApiTestException(ex));
            }
            finally
            {
                btnTestTikTokRapidApi.Text = prevText;
                btnTestTikTokRapidApi.Enabled = true;
            }
        }

        private async void btnTestVeo_Click(object sender, EventArgs e)
        {
            btnTestVeo.Enabled = false;
            var prevText = btnTestVeo.Text;
            btnTestVeo.Text = "…";
            try
            {
                if (string.IsNullOrWhiteSpace(txtVeoApiKey.Text))
                {
                    NotifySettingsApiTest("Veo", false, false, "Chưa nhập Veo API Key.");
                    return;
                }

                var endpoint = txtVeoEndpoint.Text.Trim();
                if (LooksLikePlaceholderApiEndpoint(endpoint))
                {
                    NotifySettingsApiTest(
                        "Veo",
                        false,
                        false,
                        "Endpoint vẫn là mẫu (example.com) — không phải API thật.\r\nHãy thay URL do nhà cung cấp Veo/gateway cung cấp.");
                    return;
                }

                if (!IsValidHttpUrl(endpoint))
                {
                    NotifySettingsApiTest("Veo", false, false, "Veo Endpoint không phải URL http/https hợp lệ.");
                    return;
                }

                var videoService = new VideoService();
                var url = await videoService.GenerateVideoAsync(
                    "Generate a 2-second test clip.",
                    txtVeoApiKey.Text.Trim(),
                    endpoint).ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(url))
                {
                    NotifySettingsApiTest(
                        "Veo",
                        false,
                        true,
                        "Kết nối được nhưng JSON không có trường videoUrl.\r\nKiểm tra endpoint và định dạng phản hồi API.");
                    return;
                }

                NotifySettingsApiTest(
                    "Veo",
                    true,
                    false,
                    "Key hoạt động.\r\nĐã nhận videoUrl (rút gọn):\r\n" + TruncateSettingsTestMessage(url, 200));
            }
            catch (Exception ex)
            {
                NotifySettingsApiTest("Veo", false, false, FormatApiTestException(ex));
            }
            finally
            {
                btnTestVeo.Text = prevText;
                btnTestVeo.Enabled = true;
            }
        }

        private async void btnTestTts_Click(object sender, EventArgs e)
        {
            btnTestTts.Enabled = false;
            var prevText = btnTestTts.Text;
            btnTestTts.Text = "…";
            try
            {
                if (string.IsNullOrWhiteSpace(txtTtsApiKey.Text))
                {
                    NotifySettingsApiTest("TTS", false, false, "Chưa nhập TTS API Key.");
                    return;
                }

                var firstVoiceId = ResolveFirstSettingsVoiceId();
                if (string.IsNullOrWhiteSpace(firstVoiceId))
                {
                    NotifySettingsApiTest(
                        "TTS",
                        false,
                        false,
                        "Chưa nhập Voice ID nào (Nữ trẻ, Nam trẻ, …) để test.");
                    return;
                }

                var endpoint = ElevenLabsTtsHelper.NormalizeSettingsEndpoint(firstVoiceId);
                if (string.IsNullOrWhiteSpace(endpoint) || !ElevenLabsTtsHelper.EndpointIncludesVoiceId(endpoint))
                {
                    NotifySettingsApiTest(
                        "TTS",
                        false,
                        false,
                        "Voice ID ElevenLabs không hợp lệ.");
                    return;
                }

                if (LooksLikePlaceholderApiEndpoint(endpoint))
                {
                    NotifySettingsApiTest(
                        "TTS",
                        false,
                        false,
                        "Voice ID / endpoint vẫn là mẫu — nhập ID giọng ElevenLabs thật.");
                    return;
                }

                var videoService = new VideoService();
                var url = await videoService.GenerateAudioAsync(
                    "Đây là câu test kết nối Text-to-Speech ngắn.",
                    txtTtsApiKey.Text.Trim(),
                    endpoint).ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(url))
                {
                    NotifySettingsApiTest(
                        "TTS",
                        false,
                        true,
                        "Kết nối được nhưng JSON không có trường audioUrl.\r\n" +
                        "Kiểm tra TTS Endpoint và định dạng phản hồi API (POST JSON → audioUrl).");
                    return;
                }

                NotifySettingsApiTest(
                    "TTS",
                    true,
                    false,
                    "TTS API hoạt động.\r\nĐã nhận audioUrl (rút gọn):\r\n" + TruncateSettingsTestMessage(url, 200));
            }
            catch (Exception ex)
            {
                NotifySettingsApiTest("TTS", false, false, FormatApiTestException(ex));
            }
            finally
            {
                btnTestTts.Text = prevText;
                btnTestTts.Enabled = true;
            }
        }

        private static string FormatApiTestException(Exception ex)
        {
            var msg = ex?.Message ?? "Lỗi không xác định.";
            if (msg.IndexOf("429", StringComparison.Ordinal) >= 0 ||
                msg.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Hết quota / giới hạn API (429).\r\n\r\n" + TruncateSettingsTestMessage(msg, 700) +
                       "\r\n\r\nGợi ý: đổi model (gemini-1.5-flash), đợi reset, hoặc bật billing trên Google AI Studio.";
            }

            return TruncateSettingsTestMessage(msg, 900);
        }

        private void btnBrowseFfmpegPath_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.Title = "Select ffmpeg.exe";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtFfmpegPath.Text = dialog.FileName;
                }
            }
        }

        private void btnBrowseYtDlpPath_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "yt-dlp executable (yt-dlp.exe)|yt-dlp.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.Title = "Select yt-dlp.exe";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtYtDlpPath.Text = dialog.FileName;
                }
            }
        }

        private async void btnBrowseVideoReupMusicLibrary_Click(object sender, EventArgs e)
        {
            btnVideoReupOpenMusicFolder_Click(sender, e);
        }

        private async void btnVideoReupOpenMusicFolder_Click(object sender, EventArgs e)
        {
            try
            {
                OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind.BackgroundMusic);
                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("Video reup nhạc: " + ex.Message);
            }
        }

        private async void btnVideoReupOpenHookSfxFolder_Click(object sender, EventArgs e)
        {
            try
            {
                OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind.SoundEffects);
                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("Video reup SFX: " + ex.Message);
            }
        }

        private void btnVideoReupOpenLogoLibrary_Click(object sender, EventArgs e)
        {
            OpenShowcaseLogoLibraryAsync();
        }

        private async Task WarmupBundledToolingInBackgroundAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                VideoReupRemixService.EnsureMusicLibraryDirectoryExists(settings);

                if (FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
                {
                    if (string.IsNullOrWhiteSpace(settings.FfmpegPath))
                    {
                        settings.FfmpegPath = toolkit.FfmpegExe;
                        await _configManager.SaveAsync(settings).ConfigureAwait(true);
                        if (txtFfmpegPath != null && !txtFfmpegPath.IsDisposed)
                        {
                            BeginInvoke(new Action(() => txtFfmpegPath.Text = toolkit.FfmpegExe));
                        }
                    }

                    Log("[FFmpeg] Sẵn sàng: " + toolkit.FfmpegExe);
                    return;
                }

                Log("[FFmpeg] Chưa có — sẽ tự tải khi cần (Video reup) hoặc bấm «⬇ Tải FFmpeg» trong Cài đặt.");
            }
            catch (Exception ex)
            {
                Log("[FFmpeg] Kiểm tra: " + ex.Message);
            }
        }

        private async void btnDownloadFfmpeg_Click(object sender, EventArgs e)
        {
            if (btnDownloadFfmpeg != null)
            {
                btnDownloadFfmpeg.Enabled = false;
                btnDownloadFfmpeg.Text = "⏳ Đang tải...";
            }

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                var path = await VideoReupRemixService.EnsureFfmpegToolkitAsync(
                    settings,
                    Log,
                    CancellationToken.None).ConfigureAwait(true);
                await _configManager.SaveAsync(settings).ConfigureAwait(true);
                if (txtFfmpegPath != null)
                {
                    txtFfmpegPath.Text = path;
                }

                Log("[FFmpeg] Đã cài: " + path);
                MessageBox.Show(this,
                    "Đã cài FFmpeg + ffprobe:\r\n" + path +
                    "\r\n\r\n(Thư mục Tools\\ffmpeg cạnh file exe — không cần Browse thủ công.)",
                    "Tải FFmpeg",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("[FFmpeg] Lỗi tải: " + ex.Message);
                MessageBox.Show(this,
                    "Không tải được FFmpeg.\r\n\r\nLỗi: " + ex.Message +
                    "\r\n\r\nKiểm tra mạng / firewall rồi thử lại.",
                    "Tải FFmpeg",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (btnDownloadFfmpeg != null)
                {
                    btnDownloadFfmpeg.Enabled = true;
                    btnDownloadFfmpeg.Text = "⬇ Tải FFmpeg";
                }
            }
        }

        private async void btnDownloadYtDlp_Click(object sender, EventArgs e)
        {
            const string downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");

            if (btnDownloadYtDlp != null)
            {
                btnDownloadYtDlp.Enabled = false;
                btnDownloadYtDlp.Text = "⏳ Đang tải...";
            }
            Log("[yt-dlp] Bắt đầu tải từ " + downloadUrl);

            try
            {
                await Task.Run(async () =>
                {
                    System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                    using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                    using (var resp = await http.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        resp.EnsureSuccessStatusCode();
                        using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            await src.CopyToAsync(fs).ConfigureAwait(false);
                        }
                    }
                }).ConfigureAwait(true);

                if (txtYtDlpPath != null)
                {
                    txtYtDlpPath.Text = targetPath;
                }
                var sizeMb = new FileInfo(targetPath).Length / 1024d / 1024d;
                Log($"[yt-dlp] Đã tải xong: {targetPath} ({sizeMb:0.00} MB)");
                MessageBox.Show(this,
                    "Đã tải yt-dlp.exe về:\r\n" + targetPath +
                    "\r\n\r\nBấm Lưu cài đặt để ghi lại đường dẫn.",
                    "Tải yt-dlp", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("[yt-dlp] Lỗi tải: " + ex.Message);
                MessageBox.Show(this,
                    "Không tải được yt-dlp.exe.\r\n\r\nLỗi: " + ex.Message +
                    "\r\n\r\nBạn có thể tải thủ công tại:\r\n" + downloadUrl,
                    "Tải yt-dlp", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (btnDownloadYtDlp != null)
                {
                    btnDownloadYtDlp.Enabled = true;
                    btnDownloadYtDlp.Text = "⬇ Tải yt-dlp";
                }
            }
        }

        private void Log(string message)
        {
            if (rtbLogs == null || rtbLogs.IsDisposed)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _ = AppendLogToFileAsync($"[{DateTime.Now:HH:mm:ss}] {message}");
                }
                return;
            }

            if (rtbLogs.InvokeRequired)
            {
                rtbLogs.Invoke(new Action<string>(Log), message);
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var isError = line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0;
            rtbLogs.SelectionStart = rtbLogs.TextLength;
            rtbLogs.SelectionLength = 0;
            rtbLogs.SelectionColor = isError ? Color.FromArgb(255, 95, 95) : Color.LightGray;
            rtbLogs.AppendText(line + Environment.NewLine);
            rtbLogs.SelectionColor = Color.LightGray;
            rtbLogs.ScrollToCaret();
            _ = AppendLogToFileAsync(line);
        }

        /// <summary>Log Warmup trên tab (và ghi file qua <see cref="Log"/>).</summary>
        private void LogWarmup(string message)
        {
            Log(message);

            if (rtbWarmupLog == null || rtbWarmupLog.IsDisposed)
            {
                return;
            }

            if (rtbWarmupLog.InvokeRequired)
            {
                rtbWarmupLog.BeginInvoke(new Action<string>(LogWarmup), message);
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var isError = message != null && (
                message.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("thất bại", StringComparison.OrdinalIgnoreCase) >= 0);
            rtbWarmupLog.SelectionStart = rtbWarmupLog.TextLength;
            rtbWarmupLog.SelectionLength = 0;
            rtbWarmupLog.SelectionColor = isError ? Color.FromArgb(255, 120, 120) : Color.FromArgb(190, 195, 205);
            rtbWarmupLog.AppendText(line + Environment.NewLine);
            rtbWarmupLog.SelectionColor = Color.LightGray;
            rtbWarmupLog.ScrollToCaret();

            const int maxLines = 800;
            if (rtbWarmupLog.Lines.Length > maxLines)
            {
                var keep = string.Join(Environment.NewLine, rtbWarmupLog.Lines.Skip(maxLines / 4));
                rtbWarmupLog.Clear();
                rtbWarmupLog.AppendText(keep + Environment.NewLine);
                rtbWarmupLog.SelectionStart = rtbWarmupLog.TextLength;
                rtbWarmupLog.ScrollToCaret();
            }
        }

        /// <summary>Log Video reup trên tab (và ghi file qua <see cref="Log"/>).</summary>
        private void LogVideoReup(string message)
        {
            Log(message);

            if (rtbVideoReupLog == null || rtbVideoReupLog.IsDisposed)
            {
                return;
            }

            if (rtbVideoReupLog.InvokeRequired)
            {
                rtbVideoReupLog.Invoke(new Action<string>(LogVideoReup), message);
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var isError = message.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                          || message.IndexOf("429", StringComparison.Ordinal) >= 0
                          || message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0;
            rtbVideoReupLog.SelectionStart = rtbVideoReupLog.TextLength;
            rtbVideoReupLog.SelectionLength = 0;
            rtbVideoReupLog.SelectionColor = isError ? Color.FromArgb(255, 130, 130) : Color.FromArgb(215, 222, 235);
            rtbVideoReupLog.SelectionCharOffset = 1;
            rtbVideoReupLog.AppendText(line + Environment.NewLine);
            rtbVideoReupLog.SelectionColor = Color.FromArgb(215, 222, 235);
            rtbVideoReupLog.ScrollToCaret();
        }

        private void SetVideoReupProgress(string statusText, int percent, bool indeterminate = false)
        {
            if (lblVideoReupProgress == null)
            {
                return;
            }

            void Apply()
            {
                var text = "Tiến trình: " + (statusText ?? string.Empty).Trim();
                if (!indeterminate && percent > 0 && percent < 100)
                {
                    text += " (" + percent + "%)";
                }

                lblVideoReupProgress.Text = text;
                SetReupPreviewStage(statusText);
            }

            if (lblVideoReupProgress.InvokeRequired)
            {
                lblVideoReupProgress.Invoke(new Action(Apply));
                return;
            }

            Apply();
        }

        private void btnVideoReupClearLog_Click(object sender, EventArgs e)
        {
            if (rtbVideoReupLog == null || rtbVideoReupLog.IsDisposed)
            {
                return;
            }

            rtbVideoReupLog.Clear();
            SetVideoReupProgress("sẵn sàng", 0);
        }




        private async Task EnqueueApprovalItemAsync(ApprovalQueueItem item)
        {
            if (item == null)
            {
                return;
            }

            _approvalQueueItems = _approvalQueueItems ?? new List<ApprovalQueueItem>();
            _approvalQueueItems.Insert(0, item);
            await _approvalQueueManager.SaveAsync(_approvalQueueItems);
        }

        private string BuildAutoPostCaptionPreview(string folderPath, string hashtags, string preferredVideoPath, string userCaptionDraft = null)
        {
            var draft = (userCaptionDraft ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(draft))
            {
                var tags = (hashtags ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(tags) ? draft : (draft + " " + tags).Trim();
            }

            var pick = (preferredVideoPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(pick) || !File.Exists(pick))
            {
                pick = EnumerateVideoFilesInFolder(folderPath ?? string.Empty).FirstOrDefault() ?? string.Empty;
            }

            var product = string.IsNullOrWhiteSpace(pick)
                ? "Sản phẩm nổi bật"
                : Path.GetFileNameWithoutExtension(pick).Replace("_", " ").Replace("-", " ").Trim();
            if (string.IsNullOrWhiteSpace(product))
            {
                product = "Sản phẩm nổi bật";
            }

            return string.IsNullOrWhiteSpace(hashtags) ? product : (product + " " + hashtags.Trim()).Trim();
        }

        private static string BuildFinalAutoPostCaptionBody(string captionBody, string extraHashtags)
        {
            var body = (captionBody ?? string.Empty).Trim();
            var tags = (extraHashtags ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(body))
            {
                return tags;
            }

            return string.IsNullOrWhiteSpace(tags) ? body : (body + " " + tags).Trim();
        }

        /// <summary>
        /// Chờ tiến trình ngoài (ffmpeg, ffprobe, …) và <see cref="Process.Kill"/> khi <paramref name="cancellationToken"/> hủy (Stop).
        /// Các lệnh Process.Start trong Form1 hiện chỉ mở file/explorer (UseShellExecute) — không dùng WaitForExit.
        /// </summary>
        private static void WaitForExternalToolProcessExit(Process process, CancellationToken cancellationToken)
        {
            ProcessCancellationHelper.WaitForExit(process, cancellationToken);
        }

        private string GetSelectedAutoPostCaptionStyleKey()
        {
            var idx = cbCaptionStyle?.SelectedIndex ?? 0;
            if (idx < 0 || idx >= AutoPostCaptionStyleKeys.Length)
            {
                return AutoPostCaptionStyleKeys[0];
            }

            return AutoPostCaptionStyleKeys[idx];
        }

        private static string BuildRenderFingerprint(string script, IList<AiVideoGenInputItem> items, string profile)
        {
            var normalizedScript = (script ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedProfile = (profile ?? string.Empty).Trim().ToLowerInvariant();
            var itemText = string.Empty;
            if (items != null && items.Count > 0)
            {
                var rows = items
                    .Where(x => x != null)
                    .Select(x =>
                        $"{ProfileScopedPaths.ResolveProfileName(x.ProfileName)}|" +
                        $"{(x.SourceKeyword ?? string.Empty).Trim().ToLowerInvariant()}|" +
                        $"{(x.ProductName ?? string.Empty).Trim().ToLowerInvariant()}|" +
                        $"{(x.Price ?? string.Empty).Trim().ToLowerInvariant()}|" +
                        $"{(x.ImageUrl ?? string.Empty).Trim().ToLowerInvariant()}")
                    .OrderBy(x => x)
                    .ToList();
                itemText = string.Join(";", rows);
            }

            return DuplicateGuardManager.ComputeSha256Fingerprint($"render::{normalizedProfile}::{normalizedScript}::{itemText}");
        }

        private static string BuildAutoPostFingerprintSource(
            string folderPath,
            string caption,
            string hashtags,
            string profile,
            string explicitVideoPath)
        {
            var normalizedProfile = (profile ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedCaption = (caption ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedTags = (hashtags ?? string.Empty).Trim().ToLowerInvariant();
            var videoName = string.Empty;
            var pick = (explicitVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(pick) && File.Exists(pick))
            {
                videoName = Path.GetFileName(pick).ToLowerInvariant();
            }
            else
            {
                try
                {
                    var files = Directory.GetFiles(folderPath ?? string.Empty)
                        .Where(x =>
                        {
                            var ext = Path.GetExtension(x);
                            return string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(ext, ".mov", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(ext, ".avi", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(ext, ".mkv", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(ext, ".webm", StringComparison.OrdinalIgnoreCase);
                        })
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (files.Count > 0)
                    {
                        videoName = Path.GetFileName(files[0]).ToLowerInvariant();
                    }
                }
                catch
                {
                    // best effort only
                }
            }

            return $"post::{normalizedProfile}::{videoName}::{normalizedCaption}::{normalizedTags}";
        }

        private async void btnOpenApprovalQueue_Click(object sender, EventArgs e)
        {
            HighlightSidebarForSelectedTab();
            _approvalQueueItems = _approvalQueueItems ?? await _approvalQueueManager.LoadAsync();
            foreach (var row in _approvalQueueItems)
            {
                ApprovalAffiliateTagging.EnsureTargetFields(row, row?.AffiliateLink, row?.ProductId);
            }

            var source = new BindingList<ApprovalQueueItem>(_approvalQueueItems
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList());

            var form = new Form
            {
                Text = "Hàng duyệt nội dung",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1180, 700),
                Font = ApprovalQueueBodyFont,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro
            };

            var pnlApprovalBehavior = BuildApprovalQueueBehaviorPanel();
            form.FormClosed += (_, __) => RestoreApprovalBehaviorControlsToHost();

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 320
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                DataSource = source
            };
            grid.EnableHeadersVisualStyles = false;
            // Font control trước khi thêm cột/chrome — tránh OnFontChanged đè header trên net472.
            grid.Font = ApprovalQueueBodyFont;
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                Font = ApprovalQueueBodyFont,
                Padding = new Padding(4, 2, 4, 2)
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "JobType", FillWeight = 12, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", FillWeight = 12, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Profile", DataPropertyName = "Profile", FillWeight = 14, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Score", DataPropertyName = "SafetyScore", FillWeight = 8, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Title", DataPropertyName = "Title", FillWeight = 22, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reasons", DataPropertyName = "RiskReasons", FillWeight = 32, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reviewed By", DataPropertyName = "ReviewedBy", FillWeight = 14, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reviewed At", DataPropertyName = "ReviewedAtLabel", FillWeight = 16, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Audit Action", DataPropertyName = "LastAuditAction", FillWeight = 28, ReadOnly = true });
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Gắn link Affiliate",
                DataPropertyName = nameof(ApprovalQueueItem.CanAttachAffiliate),
                FillWeight = 14,
                ReadOnly = false,
                ThreeState = false
            });
            ApplyAppGridChrome(grid);

            var preview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = ApprovalQueueBodyFont,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };

            var txtNotes = new TextBox
            {
                Dock = DockStyle.Top,
                Height = ApprovalQueueInputHeight,
                Font = ApprovalQueueBodyFont,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var txtReviewedBy = new TextBox
            {
                Dock = DockStyle.Top,
                Height = ApprovalQueueInputHeight,
                Font = ApprovalQueueBodyFont,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            txtReviewedBy.Text = "reviewer";

            var chkAttachAffiliate = new CheckBox
            {
                Text = "Gắn link Affiliate",
                Dock = DockStyle.Top,
                Height = ApprovalQueueInputHeight,
                AutoSize = false,
                Font = ApprovalQueueBodyFont,
                Padding = new Padding(4, 4, 0, 0),
                ForeColor = Color.FromArgb(200, 204, 214),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var lblAffiliateTarget = new Label
            {
                Dock = DockStyle.Top,
                Height = ApprovalQueueInputHeight - 4,
                Font = ApprovalQueueBodyFont,
                ForeColor = Color.FromArgb(150, 156, 172),
                Text = "Link dự kiến: (chưa chọn dòng)"
            };

            var cbStatusFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cbStatusFilter.Items.AddRange(new object[] { "All", "Pending", "Approved", "Completed", "Rejected" });
            cbStatusFilter.SelectedIndex = 0;
            var txtApprovalKeyword = new TextBox();
            var cbApprovalProfile = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cbApprovalProfile.Items.Add("All");
            foreach (var p in (_approvalQueueItems ?? new List<ApprovalQueueItem>()).Select(x => (x.Profile ?? string.Empty).Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                cbApprovalProfile.Items.Add(p);
            }
            cbApprovalProfile.SelectedIndex = 0;
            var dtApprovalFrom = new DateTimePicker
            {
                Value = DateTime.Today.AddDays(-7)
            };
            var dtApprovalTo = new DateTimePicker
            {
                Value = DateTime.Today
            };

            var btnApprove = CreateApprovalToolbarButton("Approve", 88);
            var btnApproveAndPublish = CreateApprovalToolbarButton("Duyệt & Đăng", 120);
            var btnReject = CreateApprovalToolbarButton("Reject", 88);
            var btnApproveLowRisk = CreateApprovalToolbarButton("Approve All Low-Risk", 150);
            var btnRunApproved = CreateApprovalToolbarButton("Run Approved", 120);
            var btnRunAllApproved = CreateApprovalToolbarButton("Run All Approved", 130);
            var btnViewAuditLog = CreateApprovalToolbarButton("View Full Audit Log", 150);
            var btnApproveFiltered = CreateApprovalToolbarButton("Approve Filtered", 120);
            var btnRunFilteredApproved = CreateApprovalToolbarButton("Run Filtered", 100);

            var actionPanel = BuildApprovalQueueActionPanel(
                btnApprove,
                btnApproveAndPublish,
                btnReject,
                btnViewAuditLog,
                btnApproveLowRisk,
                btnRunApproved,
                btnRunAllApproved,
                cbStatusFilter,
                txtApprovalKeyword,
                cbApprovalProfile,
                dtApprovalFrom,
                dtApprovalTo,
                btnApproveFiltered,
                btnRunFilteredApproved);

            var bottom = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(31, 34, 42) };
            bottom.Controls.Add(preview);
            bottom.Controls.Add(txtReviewedBy);
            bottom.Controls.Add(txtNotes);
            bottom.Controls.Add(chkAttachAffiliate);
            bottom.Controls.Add(lblAffiliateTarget);
            bottom.Controls.Add(actionPanel);
            split.Panel1.Controls.Add(grid);
            split.Panel2.Controls.Add(bottom);
            form.Controls.Add(split);
            form.Controls.Add(pnlApprovalBehavior);
            ApplyApprovalQueueDialogTypography(form);
            ApplyButtonThemesRecursive(form);
            ApplyAppTypographyRecursive(form);

            Action refreshPreview = () =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null)
                {
                    preview.Text = string.Empty;
                    txtNotes.Text = string.Empty;
                    txtReviewedBy.Text = string.Empty;
                    chkAttachAffiliate.Checked = false;
                    chkAttachAffiliate.Enabled = false;
                    lblAffiliateTarget.Text = "Link dự kiến: (chưa chọn dòng)";
                    return;
                }

                ApprovalAffiliateTagging.EnsureTargetFields(selected, selected.AffiliateLink, selected.ProductId);
                preview.Text = string.IsNullOrWhiteSpace(selected.EditedPreview) ? selected.OriginalPreview : selected.EditedPreview;
                txtNotes.Text = selected.ReviewerNotes ?? string.Empty;
                txtReviewedBy.Text = selected.ReviewedBy ?? string.Empty;
                chkAttachAffiliate.Checked = selected.CanAttachAffiliate;
                chkAttachAffiliate.Enabled = ApprovalAffiliateTagging.AllowsAffiliateCheckbox(selected);
                var targetLink = OmnichannelAutoPostFields.NormalizeLink(selected.TargetAffiliateLink);
                lblAffiliateTarget.Text = string.IsNullOrWhiteSpace(targetLink)
                    ? "Link dự kiến: (không có — chỉ đăng video thuần)"
                    : "Link dự kiến: " + targetLink;
            };

            chkAttachAffiliate.CheckedChanged += (s, args) =>
            {
                if (grid.CurrentRow?.DataBoundItem is ApprovalQueueItem selected)
                {
                    ApprovalAffiliateTagging.ApplyReviewerAffiliateChoice(selected, chkAttachAffiliate.Checked);
                    grid.Refresh();
                }
            };

            grid.CellContentClick += (s, args) =>
            {
                if (args.RowIndex < 0)
                {
                    return;
                }

                var col = grid.Columns[args.ColumnIndex];
                if (col is DataGridViewCheckBoxColumn && col.DataPropertyName == nameof(ApprovalQueueItem.CanAttachAffiliate))
                {
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    if (grid.Rows[args.RowIndex].DataBoundItem is ApprovalQueueItem row)
                    {
                        ApprovalAffiliateTagging.ApplyReviewerAffiliateChoice(row, row.CanAttachAffiliate);
                        refreshPreview();
                    }
                }
            };

            grid.SelectionChanged += (s, args) => refreshPreview();
            refreshPreview();

            btnApprove.Click += async (s, args) =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null) return;
                ApprovalAffiliateTagging.ApplyReviewerAffiliateChoice(selected, chkAttachAffiliate.Checked);
                selected.Status = ApprovalStatus.Approved;
                selected.ReviewerNotes = txtNotes.Text.Trim();
                selected.ReviewedBy = txtReviewedBy.Text.Trim();
                selected.EditedPreview = preview.Text;
                selected.ReviewedAtUtc = DateTime.UtcNow;
                AppendApprovalAudit(selected, selected.CanAttachAffiliate
                    ? "Approved (gắn link Affiliate)"
                    : "Approved (video thuần, không gắn link)");
                grid.Refresh();
                await _approvalQueueManager.SaveAsync(source.ToList());
                _approvalQueueItems = source.ToList();
            };

            btnApproveAndPublish.Click += async (s, args) =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null)
                {
                    Log("[APPROVAL] Chọn một dòng Pending để Duyệt & Đăng.");
                    return;
                }

                if (selected.Status != ApprovalStatus.Pending)
                {
                    Log("[APPROVAL] Chỉ duyệt & đăng item trạng thái Pending.");
                    return;
                }

                selected.ReviewerNotes = txtNotes.Text.Trim();
                selected.ReviewedBy = string.IsNullOrWhiteSpace(txtReviewedBy.Text)
                    ? "approve-publish"
                    : txtReviewedBy.Text.Trim();
                selected.ReviewedAtUtc = DateTime.UtcNow;
                selected.Status = ApprovalStatus.Approved;
                ApprovalAffiliateTagging.ApplyReviewerAffiliateChoice(selected, chkAttachAffiliate.Checked);
                AppendApprovalAudit(selected, selected.CanAttachAffiliate
                    ? "Approve & Publish (gắn link Affiliate)"
                    : "Approve & Publish (video thuần)");
                try
                {
                    var lockedProfile = ResolveLockedProfileFromApprovalItem(selected);
                    await CompleteApprovedProductionVideoAsync(selected, ExtractVideoPathFromApprovalItem(selected), lockedProfile)
                        .ConfigureAwait(true);
                    Log("[APPROVAL] Duyệt & Đăng xong — video → Publishing + AutoPost job.");
                }
                catch (Exception ex)
                {
                    selected.LastError = ex.Message;
                    Log("[APPROVAL] Duyệt & Đăng lỗi: " + ex.Message);
                }

                grid.Refresh();
                await _approvalQueueManager.SaveAsync(source.ToList());
                _approvalQueueItems = source.ToList();
            };

            btnReject.Click += async (s, args) =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null) return;
                selected.Status = ApprovalStatus.Rejected;
                selected.ReviewerNotes = txtNotes.Text.Trim();
                selected.ReviewedBy = txtReviewedBy.Text.Trim();
                selected.ReviewedAtUtc = DateTime.UtcNow;
                AppendApprovalAudit(selected, "Rejected");
                grid.Refresh();
                await _approvalQueueManager.SaveAsync(source.ToList());
                _approvalQueueItems = source.ToList();
            };

            btnApproveLowRisk.Click += async (s, args) =>
            {
                foreach (var row in source.Where(x => x.Status == ApprovalStatus.Pending && x.SafetyScore >= 85))
                {
                    ApprovalAffiliateTagging.ApplyReviewerAffiliateChoice(row, false);
                    row.Status = ApprovalStatus.Approved;
                    row.ReviewedAtUtc = DateTime.UtcNow;
                    row.ReviewedBy = string.IsNullOrWhiteSpace(txtReviewedBy.Text) ? "bulk-review" : txtReviewedBy.Text.Trim();
                    AppendApprovalAudit(row, "Approved low-risk (không gắn link)");
                }
                grid.Refresh();
                await _approvalQueueManager.SaveAsync(source.ToList());
                _approvalQueueItems = source.ToList();
            };

            btnRunApproved.Click += async (s, args) =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null || selected.Status != ApprovalStatus.Approved)
                {
                    Log("[APPROVAL] Chọn item đã Approved để chạy.");
                    return;
                }

                await ExecuteApprovedItemAsync(selected);
                AppendApprovalAudit(selected, "Executed approved item");
                grid.Refresh();
                await _approvalQueueManager.SaveAsync(source.ToList());
                _approvalQueueItems = source.ToList();
            };

            btnRunAllApproved.Click += async (s, args) =>
            {
                foreach (var approved in source.Where(x => x.Status == ApprovalStatus.Approved).ToList())
                {
                    await ExecuteApprovedItemAsync(approved);
                    AppendApprovalAudit(approved, "Executed in run-all");
                }

                grid.Refresh();
                await _approvalQueueManager.SaveAsync(source.ToList());
                _approvalQueueItems = source.ToList();
            };

            btnApproveFiltered.Click += async (s, args) =>
            {
                var filteredRows = ((BindingList<ApprovalQueueItem>)grid.DataSource)?.ToList() ?? new List<ApprovalQueueItem>();
                var pending = filteredRows.Where(x => x.Status == ApprovalStatus.Pending).ToList();
                if (pending.Count == 0)
                {
                    Log("[APPROVAL] Không có item Pending trong bộ lọc hiện tại.");
                    return;
                }

                var reviewer = string.IsNullOrWhiteSpace(txtReviewedBy.Text) ? "bulk-filter" : txtReviewedBy.Text.Trim();
                foreach (var item in pending)
                {
                    ApprovalAffiliateTagging.ApplyReviewerAffiliateChoice(item, false);
                    item.Status = ApprovalStatus.Approved;
                    item.ReviewedAtUtc = DateTime.UtcNow;
                    item.ReviewedBy = reviewer;
                    AppendApprovalAudit(item, "Approved filtered (không gắn link)");
                }

                grid.Refresh();
                await _approvalQueueManager.SaveAsync(_approvalQueueItems);
            };

            btnRunFilteredApproved.Click += async (s, args) =>
            {
                var filteredRows = ((BindingList<ApprovalQueueItem>)grid.DataSource)?.ToList() ?? new List<ApprovalQueueItem>();
                var approved = filteredRows.Where(x => x.Status == ApprovalStatus.Approved).ToList();
                if (approved.Count == 0)
                {
                    Log("[APPROVAL] Không có item Approved trong bộ lọc hiện tại.");
                    return;
                }

                foreach (var item in approved)
                {
                    await ExecuteApprovedItemAsync(item);
                    AppendApprovalAudit(item, "Executed filtered-approved");
                }

                grid.Refresh();
                await _approvalQueueManager.SaveAsync(_approvalQueueItems);
            };

            btnViewAuditLog.Click += (s, args) =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null)
                {
                    Log("[APPROVAL] Chọn item để xem audit log.");
                    return;
                }

                var lines = selected.ReviewActionLog ?? new List<string>();
                var viewer = new Form
                {
                    Text = "Approval Audit Log",
                    StartPosition = FormStartPosition.CenterParent,
                    Size = new Size(860, 420),
                    BackColor = Color.FromArgb(31, 34, 42),
                    ForeColor = Color.Gainsboro
                };
                var box = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    BackColor = Color.FromArgb(20, 22, 28),
                    ForeColor = Color.Gainsboro,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = lines.Count == 0 ? "No audit records yet." : string.Join(Environment.NewLine, lines)
                };
                viewer.Controls.Add(box);
                viewer.ShowDialog(form);
            };

            Action applyApprovalFilter = () =>
            {
                var selectedFilter = cbStatusFilter.SelectedItem?.ToString() ?? "All";
                var selectedProfile = cbApprovalProfile.SelectedItem?.ToString() ?? "All";
                var keyword = txtApprovalKeyword.Text ?? string.Empty;
                var filtered = ApplyApprovalFilter(_approvalQueueItems, selectedFilter, selectedProfile, keyword, dtApprovalFrom.Value, dtApprovalTo.Value);
                grid.DataSource = new BindingList<ApprovalQueueItem>(filtered);
                refreshPreview();
            };
            cbStatusFilter.SelectedIndexChanged += (s, args) => applyApprovalFilter();
            txtApprovalKeyword.TextChanged += (s, args) => applyApprovalFilter();
            cbApprovalProfile.SelectedIndexChanged += (s, args) => applyApprovalFilter();
            dtApprovalFrom.ValueChanged += (s, args) => applyApprovalFilter();
            dtApprovalTo.ValueChanged += (s, args) => applyApprovalFilter();

            form.ShowDialog(this);
        }

        private static List<ApprovalQueueItem> ApplyApprovalFilter(IEnumerable<ApprovalQueueItem> items, string filter, string profile, string keyword, DateTime from, DateTime to)
        {
            var source = (items ?? Enumerable.Empty<ApprovalQueueItem>()).OrderByDescending(x => x.CreatedAtUtc);
            var rangeFrom = from.Date;
            var rangeTo = to.Date.AddDays(1).AddTicks(-1);
            var query = source.Where(x => x.CreatedAtUtc >= rangeFrom.ToUniversalTime() && x.CreatedAtUtc <= rangeTo.ToUniversalTime());

            if (!string.Equals(profile, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals((x.Profile ?? string.Empty).Trim(), profile ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            var key = (keyword ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                query = query.Where(x =>
                    (x.Title ?? string.Empty).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (x.Profile ?? string.Empty).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (x.RiskReasons ?? string.Empty).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (x.ReviewerNotes ?? string.Empty).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (string.Equals(filter, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == ApprovalStatus.Pending);
            }
            else if (string.Equals(filter, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == ApprovalStatus.Approved);
            }
            else if (string.Equals(filter, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == ApprovalStatus.Completed);
            }
            else if (string.Equals(filter, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status == ApprovalStatus.Rejected);
            }

            return query.ToList();
        }

        private static void AppendApprovalAudit(ApprovalQueueItem item, string action)
        {
            if (item == null)
            {
                return;
            }

            item.ReviewActionLog = item.ReviewActionLog ?? new List<string>();
            var who = string.IsNullOrWhiteSpace(item.ReviewedBy) ? "unknown" : item.ReviewedBy.Trim();
            item.ReviewActionLog.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {who}: {action}");
            if (item.ReviewActionLog.Count > 200)
            {
                item.ReviewActionLog = item.ReviewActionLog.Skip(item.ReviewActionLog.Count - 200).ToList();
            }
        }

        private async Task AutoRunApprovedQueueItemsAsync()
        {
            _approvalQueueItems = _approvalQueueItems ?? await _approvalQueueManager.LoadAsync();
            var approved = _approvalQueueItems
                .Where(x => x.Status == ApprovalStatus.Approved)
                .OrderBy(x => x.CreatedAtUtc)
                .ToList();

            if (approved.Count == 0)
            {
                return;
            }

            foreach (var item in approved)
            {
                await ExecuteApprovedItemAsync(item);
            }

            await _approvalQueueManager.SaveAsync(_approvalQueueItems);
        }

        private async Task ExecuteApprovedItemAsync(ApprovalQueueItem item)
        {
            if (item == null)
            {
                return;
            }

            var lockedProfile = ResolveLockedProfileFromApprovalItem(item);
            SelectRunningProfileInUi(lockedProfile);
            ApplyProfileScope(lockedProfile);
            Log("[APPROVAL] Khóa profile thực thi: «" + lockedProfile + "» (bỏ qua profile đang chọn trên UI).");

            try
            {
                if (item.JobType == ApprovalJobType.RenderVideo)
                {
                    var payload = JsonConvert.DeserializeObject<RenderApprovalPayload>(item.PayloadJson ?? string.Empty) ?? new RenderApprovalPayload();
                    if (payload.IsMascotStory &&
                        !string.IsNullOrWhiteSpace(payload.OutputVideoPath) &&
                        File.Exists(payload.OutputVideoPath))
                    {
                        item.Status = ApprovalStatus.Completed;
                        item.CompletedAtUtc = DateTime.UtcNow;
                        item.LastError = string.Empty;
                        Log("[APPROVAL] Mascot Story đã duyệt — video: " + payload.OutputVideoPath);
                        return;
                    }

                    var script = string.IsNullOrWhiteSpace(item.EditedPreview) ? payload.Script : item.EditedPreview;
                    var approvalRenderItems = GetSlideshowItemsForRender();
                    IList<string> approvalPerScripts = null;
                    if (_aiVideoScriptBindingList != null &&
                        _aiVideoScriptBindingList.Count == approvalRenderItems.Count &&
                        _aiVideoScriptBindingList.All(x => !string.IsNullOrWhiteSpace(x?.Script)))
                    {
                        approvalPerScripts = _aiVideoScriptBindingList
                            .Select(x => (x.Script ?? string.Empty).Trim())
                            .ToList();
                    }

                    var settings = await _configManager.LoadAsync();
                    settings.VideoTransitionDurationSeconds = (double)numAiTransitionDuration.Value;
                    settings.VideoTextSize = (int)numAiTextSize.Value;
                    settings.VideoMusicVolume = (int)numAiMusicVolume.Value;
                    await _configManager.SaveAsync(settings);
                    var outputPath = await _videoProcessingService.GenerateProductVideoAsync(
                        approvalRenderItems,
                        script,
                        settings,
                        lockedProfile,
                        Log,
                        CancellationToken.None,
                        null,
                        UseMultiVoiceNarrationEnabled(),
                        approvalPerScripts);
                    item.Status = ApprovalStatus.Completed;
                    item.CompletedAtUtc = DateTime.UtcNow;
                    item.LastError = string.Empty;
                    Log("[APPROVAL] Render completed -> " + outputPath);
                    _ = SendNotificationAsync(
                        "Approval Render Completed",
                        "Render trong hàng duyệt đã hoàn tất. Video đã sẵn sàng để xử lý bước tiếp theo.",
                        "video_render_completed",
                        "info");
                }
                else if (item.JobType == ApprovalJobType.AutoPost)
                {
                    if (!CheckAssetIntegrity(item, out var autoPostIntegrityError))
                    {
                        item.LastError = autoPostIntegrityError;
                        Log("[APPROVAL] Asset integrity: " + autoPostIntegrityError);
                        return;
                    }

                    var payload = JsonConvert.DeserializeObject<AutoPostApprovalPayload>(item.PayloadJson ?? "{}")
                                  ?? new AutoPostApprovalPayload();
                    var plan = new OmnichannelAutoPostPlan
                    {
                        VideoFolder = payload.VideoFolder ?? string.Empty,
                        VideoFilePath = payload.VideoFilePath ?? string.Empty,
                        Profile = payload.Profile ?? payload.ProfileName ?? item.Profile ?? string.Empty,
                        PostTikTok = payload.PostTikTok,
                        PostFacebook = payload.PostFacebook,
                        PostYouTube = payload.PostYouTube,
                        TikTokCaption = payload.CaptionFull ?? string.Empty,
                        TikTokHashtags = payload.Hashtags ?? string.Empty,
                        TikTokUploadOnly = payload.UploadOnlyNoPublish,
                        FacebookCaption = payload.FacebookCaption ?? string.Empty,
                        FacebookHashtags = payload.FacebookHashtags ?? string.Empty,
                        FacebookAttachShopeeLink = payload.FacebookAttachShopeeLink,
                        FacebookShopeeLink = OmnichannelAutoPostFields.NormalizeLink(payload.FacebookShopeeLink),
                        YouTubeTitle = payload.YouTubeTitle ?? string.Empty,
                        YouTubeDescription = payload.YouTubeDescription ?? string.Empty,
                        AffiliateLink = item.CanAttachAffiliate
                            ? OmnichannelAutoPostFields.NormalizeLink(item.TargetAffiliateLink)
                            : string.Empty,
                        ProductId = item.CanAttachAffiliate
                            ? OmnichannelAutoPostFields.NormalizeLink(item.TargetProductId)
                            : string.Empty
                    };

                    if (!plan.PostTikTok && !plan.PostFacebook && !plan.PostYouTube)
                    {
                        plan.PostTikTok = true;
                    }

                    await RunOmnichannelAutoPostSequenceAsync(plan, CancellationToken.None).ConfigureAwait(true);
                    item.Status = ApprovalStatus.Completed;
                    item.CompletedAtUtc = DateTime.UtcNow;
                    item.LastError = string.Empty;
                    Log("[APPROVAL] Auto Post đa kênh đã chạy xong.");
                }
                else if (item.JobType == ApprovalJobType.PhilosophyVideo)
                {
                    var payload = JsonConvert.DeserializeObject<PhilosophyApprovalPayload>(item.PayloadJson ?? "{}")
                                  ?? new PhilosophyApprovalPayload();
                    if (!string.IsNullOrWhiteSpace(payload.OutputPath) && File.Exists(payload.OutputPath))
                    {
                        await CompleteApprovedProductionVideoAsync(
                            item,
                            payload.OutputPath,
                            lockedProfile,
                            affiliateLink: string.Empty,
                            productId: string.Empty).ConfigureAwait(true);
                    }
                    else
                    {
                        throw new FileNotFoundException("Không tìm thấy file video Quote.", payload.OutputPath);
                    }
                }
                else if (item.JobType == ApprovalJobType.VideoReup)
                {
                    var videoPath = ExtractVideoPathFromApprovalItem(item);
                    if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                    {
                        throw new FileNotFoundException("Không tìm thấy video Reup.", videoPath);
                    }

                    await CompleteApprovedProductionVideoAsync(item, videoPath, lockedProfile).ConfigureAwait(true);
                }
                else if (item.JobType == ApprovalJobType.Slideshow
                         || item.JobType == ApprovalJobType.AffiliateDeep
                         || item.JobType == ApprovalJobType.Mascot)
                {
                    var videoPath = ExtractVideoPathFromApprovalItem(item);
                    if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                    {
                        throw new FileNotFoundException("Không tìm thấy file video đã render.", videoPath);
                    }

                    await CompleteApprovedProductionVideoAsync(item, videoPath, lockedProfile).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message;
                Log("[APPROVAL] Execute failed: " + ex.Message);
            }
        }





        private class RenderApprovalPayload
        {
            public string Script { get; set; } = string.Empty;
            public string Profile { get; set; } = string.Empty;
            public string ProfileName { get; set; } = string.Empty;
            public string OutputVideoPath { get; set; } = string.Empty;
            public string ChannelTheme { get; set; } = string.Empty;
            public bool IsMascotStory { get; set; }
            public string AffiliateLink { get; set; }
            public string ProductId { get; set; }
        }

        private class PhilosophyApprovalPayload
        {
            public string OutputPath { get; set; } = string.Empty;
            public string Quote { get; set; } = string.Empty;
            public string ProfileName { get; set; } = string.Empty;
            public double DurationSeconds { get; set; }
            public string Mood { get; set; } = string.Empty;
        }

        private sealed class AutoPostVideoItem
        {
            public AutoPostVideoItem(string displayLabel, string fullPath)
            {
                DisplayLabel = displayLabel ?? string.Empty;
                FullPath = fullPath ?? string.Empty;
            }

            public string DisplayLabel { get; }
            public string FullPath { get; }

            public override string ToString() => DisplayLabel;
        }

        private string InitializeSessionLogFile()
        {
            var baseLogsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            var dayFolder = Path.Combine(baseLogsFolder, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayFolder);

            var pid = Process.GetCurrentProcess().Id;
            var fileName = $"session_{DateTime.Now:HHmmss}_{pid}.log";
            var logPath = Path.Combine(dayFolder, fileName);
            var preamble = $"Session started at {DateTime.Now:O}{Environment.NewLine}";
            lock (_sessionLogSync)
            {
                WriteSessionLogLinesUnlocked(logPath, FileMode.Create, preamble);
            }

            return logPath;
        }

        private static void WriteSessionLogLinesUnlocked(string path, FileMode mode, string text)
        {
            using (var fs = new FileStream(path, mode, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, TextFileEncoding.Utf8NoBom))
            {
                sw.Write(text);
            }
        }

        private async Task AppendLogToFileAsync(string line)
        {
            var payload = line + Environment.NewLine;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        lock (_sessionLogSync)
                        {
                            WriteSessionLogLinesUnlocked(_sessionLogFilePath, FileMode.Append, payload);
                        }
                    }).ConfigureAwait(false);
                    return;
                }
                catch (IOException)
                {
                    if (attempt == 4)
                    {
                        return;
                    }

                    await Task.Delay(40 * (attempt + 1)).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt == 4)
                    {
                        return;
                    }

                    await Task.Delay(40 * (attempt + 1)).ConfigureAwait(false);
                }
            }
        }

        private void TikTokAutomation_CaptchaDetected(object sender, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, string>(TikTokAutomation_CaptchaDetected), sender, message);
                return;
            }

            MessageBox.Show(
                this,
                message,
                "Captcha",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            _ = SendNotificationAsync(
                "TikTok Captcha Detected",
                message,
                "captcha_detected",
                "warning");
        }

        private void TikTokAutomation_CheckpointDetected(object sender, CheckpointAlert alert)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, CheckpointAlert>(TikTokAutomation_CheckpointDetected), sender, alert);
                return;
            }

            var payload = alert ?? new CheckpointAlert();
            var profile = string.IsNullOrWhiteSpace(payload.ProfileName)
                ? (cbRunningProfile?.SelectedItem?.ToString() ?? "default")
                : payload.ProfileName;
            var detectedAt = payload.DetectedAtLocal == default(DateTime) ? DateTime.Now : payload.DetectedAtLocal;
            var message = payload.Message ?? "Phát hiện CHECKPOINT.";
            var detail = $"{message}{Environment.NewLine}Profile: {profile}{Environment.NewLine}Thời gian: {detectedAt:yyyy-MM-dd HH:mm:ss}";

            MessageBox.Show(
                this,
                detail,
                "Checkpoint",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            _ = SendNotificationAsync(
                "TikTok Checkpoint Detected",
                detail,
                "checkpoint_detected",
                "critical",
                profile,
                string.IsNullOrWhiteSpace(payload.ScreenshotPath) ? null : new List<string> { payload.ScreenshotPath });
        }

        private async Task SendNotificationAsync(
            string title,
            string body,
            string eventType,
            string severity,
            string profileName = "",
            List<string> attachmentPaths = null)
        {
            try
            {
                var settings = await _configManager.LoadAsync();
                await _notificationService.SendAsync(
                    settings,
                    new NotificationMessage
                    {
                        Title = (title ?? string.Empty).Trim(),
                        Body = (body ?? string.Empty).Trim(),
                        EventType = (eventType ?? string.Empty).Trim(),
                        Severity = (severity ?? "info").Trim(),
                        ProfileName = (profileName ?? string.Empty).Trim(),
                        AttachmentPaths = attachmentPaths ?? new List<string>()
                    },
                    Log).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log("[NOTIFY] Failed to send notification: " + ex.Message);
            }
        }

        private void btnOpenLogsFolder_Click(object sender, EventArgs e)
        {
            try
            {
                var logsFolder = Path.GetDirectoryName(_sessionLogFilePath);
                if (string.IsNullOrWhiteSpace(logsFolder) || !Directory.Exists(logsFolder))
                {
                    Log("Logs folder not found.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = logsFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log("Failed to open logs folder: " + ex.Message);
            }
        }

        private async void btnExportCurrentLogs_Click(object sender, EventArgs e)
        {
            try
            {
                var logsFolder = Path.GetDirectoryName(_sessionLogFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(logsFolder);

                using (var dialog = new SaveFileDialog())
                {
                    dialog.InitialDirectory = logsFolder;
                    dialog.FileName = $"ui_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    dialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                    dialog.Title = "Export Current Logs";

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    var logText = rtbLogs.Text;
                    await Task.Run(() => File.WriteAllText(dialog.FileName, logText, TextFileEncoding.Utf8NoBom));
                    Log("Current logs exported to: " + dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log("Failed to export current logs: " + ex.Message);
            }
        }

        private void btnClearLogs_Click(object sender, EventArgs e)
        {
            rtbLogs.Clear();
            Log("UI logs cleared.");
        }

        private void cbRunningProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbRunningProfile?.SelectedItem != null)
            {
                var selectedProfile = cbRunningProfile.SelectedItem.ToString();
                SelectRunningProfileInUi(selectedProfile);
                ApplyProfileScope(selectedProfile);
            }

            SyncBuffersToGrids();
        }

        private string GetRunningProfileName()
        {
            return ProfileScopedPaths.ResolveProfileName(cbRunningProfile?.SelectedItem?.ToString());
        }

        /// <summary>Lọc lưới affiliate theo profile; đồng bộ scope lưu file.</summary>
        private void ApplyProfileScope(string profileName)
        {
            _affiliateGridProfileScope = ProfileScopedPaths.ResolveProfileName(profileName);
            RefreshAffiliateGridByQualityFilter();
            SyncBuffersToGrids();
            RefreshAffiliateDownloadFolderHint();
            Log($"[Profile] Phạm vi UI: «{_affiliateGridProfileScope}» — tải/render lưu dưới Downloads\\{_affiliateGridProfileScope} và Generated\\{_affiliateGridProfileScope}.");
        }

        private static string ResolveLockedProfileFromApprovalItem(ApprovalQueueItem item)
        {
            if (item == null)
            {
                return "default";
            }

            var fallback = ProfileScopedPaths.ResolveProfileName(item.Profile);
            try
            {
                if (item.JobType == ApprovalJobType.RenderVideo)
                {
                    var payload = JsonConvert.DeserializeObject<RenderApprovalPayload>(item.PayloadJson ?? "{}");
                    return ProfileScopedPaths.ResolveProfileName(
                        payload?.ProfileName ?? payload?.Profile ?? fallback);
                }

                if (item.JobType == ApprovalJobType.AutoPost)
                {
                    var payload = JsonConvert.DeserializeObject<AutoPostApprovalPayload>(item.PayloadJson ?? "{}");
                    return ProfileScopedPaths.ResolveProfileName(
                        payload?.ProfileName ?? payload?.Profile ?? fallback);
                }

                if (item.JobType == ApprovalJobType.PhilosophyVideo)
                {
                    var payload = JsonConvert.DeserializeObject<PhilosophyApprovalPayload>(item.PayloadJson ?? "{}");
                    return ProfileScopedPaths.ResolveProfileName(
                        payload?.ProfileName ?? fallback);
                }
            }
            catch
            {
                // ignored — dùng fallback từ item.Profile
            }

            return fallback;
        }

        private void ForwardToAutoPost(
            string videoPath,
            string caption,
            string hashtags,
            string profileName,
            string affiliateLink = null,
            string productId = null,
            OmniJob sourceJob = null)
        {
            if (!AssetIntegrityService.CheckVideoFile(videoPath, out var integrityError))
            {
                Log("[AssetIntegrity] Forward to Auto Post bị chặn: " + integrityError);
                return;
            }

            try
            {
                var profile = ProfileScopedPaths.ResolveProfileName(profileName);
                SelectRunningProfileInUi(profile);
                ApplyProfileScope(profile);

                var folder = OneClickPipelineService.GetAutoPostInboxFolder(_storageRootPathCache, profile);
                if (!ProfileScopedPaths.IsUnderPublishingRoot(_storageRootPathCache, videoPath))
                {
                    var published = ProfileScopedPaths.CopyVideoToPublishing(
                        _storageRootPathCache,
                        profile,
                        videoPath,
                        VideoStorageType.Processed);
                    if (!string.IsNullOrWhiteSpace(published) && File.Exists(published))
                    {
                        videoPath = published;
                    }
                }

                Directory.CreateDirectory(folder);
                txtAutoPostFolder.Text = folder;
                RefreshAutoPostVideoCombo(videoPath);

                if (txtAutoPostCaption != null)
                {
                    txtAutoPostCaption.Text = caption ?? string.Empty;
                }

                if (txtAutoPostHashtags != null)
                {
                    txtAutoPostHashtags.Text = hashtags ?? string.Empty;
                }

                if (OmnichannelAutoPostFields.IsShopeeProductUrl(affiliateLink))
                {
                    if (txtAutoPostFbShopeeLink != null)
                    {
                        txtAutoPostFbShopeeLink.Text = OmnichannelAutoPostFields.NormalizeLink(affiliateLink);
                    }

                    if (chkAutoPostFbAttachShopee != null)
                    {
                        chkAutoPostFbAttachShopee.Checked = true;
                    }
                }

                SwitchToMainTab(tabAutoPost);

                SetAutoPostInboxFromJob(sourceJob, affiliateLink, productId);

                Log($"[Luồng Khép Kín] Profile «{profile}» → thư mục «{folder}» — video '{Path.GetFileName(videoPath)}'.");
                if (OmnichannelAutoPostFields.ShouldAttachAffiliateProduct(
                        _autoPostInboxJob?.AffiliateLink,
                        _autoPostInboxJob?.ProductId))
                {
                    Log("[Luồng Khép Kín] Affiliate link sẽ gắn khi đăng TikTok: " + _autoPostInboxJob.AffiliateLink);
                }
                else
                {
                    Log("[Luồng Khép Kín] Nuôi kênh — Auto Post không gắn sản phẩm (AffiliateLink/ProductId trống).");
                }
            }
            catch (Exception ex)
            {
                Log("Forward to Auto Post lỗi: " + ex.Message);
            }
        }

    }
}
