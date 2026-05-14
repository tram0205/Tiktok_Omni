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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1 : Form
    {
        private readonly ConfigManager _configManager;
        private readonly TikTokAutomation _tikTokAutomation;
        private readonly WarmupStateManager _warmupStateManager;
        private readonly AffiliateHunter _affiliateHunter;
        private readonly NotificationService _notificationService;

        private TabControl tabMain;
        private TabPage tabAutoWarmup;
        private TabPage tabAffiliateHunter;
        private TabPage tabAiVideoGen;
        private TabPage tabAutoPost;
        private TabPage tabSetting;

        private StatusStrip statusStripMain;
        private ToolStripStatusLabel tslStatusMain;

        private TextBox txtKeywords;
        private ComboBox cbRunningProfile;
        private NumericUpDown numVideoCount;
        private NumericUpDown numWatchMin;
        private NumericUpDown numWatchMax;
        private CheckBox chkAutoComment;
        private RadioButton rbDryRun;
        private RadioButton rbLiveRun;
        private Button btnStartWarmup;
        private Button btnOpenTikTokManualBrowser;
        private Button btnResumeWarmup;
        private Button btnStopWarmup;
        private Button btnQueueWarmup;
        private Button btnStartWarmupQueue;
        private Button btnStopWarmupQueue;
        private Button btnPauseWarmupQueue;
        private Button btnResumeWarmupQueue;
        private Button btnPauseNowWarmupQueue;
        private Button btnRemoveQueueJob;
        private Button btnClearWarmupQueue;
        private Button btnMoveQueueJobUp;
        private Button btnMoveQueueJobDown;
        private Button btnOpenQueueHistory;
        private Button btnOpenQueueTrend;
        private Button btnOpenApprovalQueue;
        private Button btnRefreshQueueStats;
        private ComboBox cbQueueStatsRange;
        private DateTimePicker dtQueueStatsFrom;
        private DateTimePicker dtQueueStatsTo;
        private Label lblWarmupQueueStatus;
        private Label lblQueueStatsSummary;
        private Label lblQueueStatsSuccessRate;
        private Label lblQueueStatsAvgRetry;
        private Label lblQueueStatsTopFailedProfile;
        private DataGridView dgvWarmupQueue;
        private ProgressBar pbWarmupProgress;
        private Label lblWarmupProgress;
        private Button btnOpenLogsFolder;
        private Button btnExportCurrentLogs;
        private Button btnClearLogs;
        private RichTextBox rtbLogs;
        private TextBox txtAutoPostFolder;
        private Button btnBrowseAutoPostFolder;
        private ComboBox cbAutoPostProfile;
        private ComboBox cbAutoPostVideoFile;
        private Button btnRefreshAutoPostVideos;
        private TextBox txtAutoPostHashtags;
        private ComboBox cbCaptionStyle;
        private Button btnGenerateGeminiCaption;
        private Button btnPreviewAutoPostVideo;
        private TextBox txtAutoPostCaption;
        private CheckBox chkAutoPostVideoApproved;
        private CheckBox chkAutoPostUploadOnly;
        private Button btnStartAutoPost;
        private Button btnCloseAutoPostBrowser;

        private static readonly string[] AutoPostCaptionStyleKeys =
        {
            "knowledge",
            "philosophy",
            "humor",
            "debate"
        };

        private const string AffiliateKeywordsPlaceholder =
            "Mỗi dòng 1 từ khoá. Ví dụ:\r\náo dài trắng\r\nváy maxi hè";

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
        private TextBox txtVeoApiKey;
        private TextBox txtLyriaApiKey;
        private TextBox txtVeoEndpoint;
        private TextBox txtLyriaEndpoint;
        private TextBox txtFfmpegPath;
        private TextBox txtYtDlpPath;
        private Button btnToggleAiApiKey;
        private Button btnToggleTwoCaptchaApiKey;
        private Button btnToggleVeoApiKey;
        private Button btnToggleLyriaApiKey;
        private Button btnTestAi;
        private Button btnTestTwoCaptcha;
        private Button btnTestVeo;
        private Button btnTestLyria;
        private Button btnBrowseFfmpegPath;
        private Button btnBrowseYtDlpPath;
        private Button btnDownloadYtDlp;
        private Button btnSaveSettings;
        private CheckBox chkAutoResumeQueueOnStartup;
        private CheckBox chkScheduleWarmupStub;
        private CheckBox chkAlwaysRequirePrePostApproval;
        private CheckBox chkAlwaysRequirePreRenderApproval;
        private CheckBox chkAutoRunApprovedQueue;
        private NumericUpDown numBlockPostingSafetyScoreBelow;
        private Label lblSettingsValidation;
        private DataGridView dgvProxyProfiles;
        private BindingList<AutomationProfile> _proxyProfileBindingList;
        private Button btnCheckBrowserProfileHealth;
        private DataGridView dgvAiVideoGenInput;
        private BindingList<AiVideoGenInputItem> _aiVideoGenBindingList;
        private Label lblAiVideoGenProductsTitle;
        private Label lblAiVideoGenProductBlockHint;
        private TabControl tabAiVideoGenModes;
        private TabPage tabAiModeSlideshow;
        private TabPage tabAiModeAffiliateDeep;
        private TabPage tabAiModeMascot;
        private TabPage tabAiModePhilosophy;
        private TabPage tabAiModeVideoReup;
        private Button btnGenerateGeminiPrompt;
        private Button btnReviewScriptBeforeRender;
        private Button btnRunAffiliateDeepVideo;
        private Button btnRunMascotChannelPipeline;
        private Button btnRunPhilosophyVideo;
        private Button btnCopyAiVideoPrompt;
        private Button btnSaveAiVideoPrompt;
        private Button btnRenderAiVideo;
        private TextBox txtAiVideoGenPrompt;
        private TextBox txtMascotImagePath;
        private TextBox txtMascotChannelTheme;
        private Button btnBrowseMascotImage;
        private TextBox txtAvatarIdentityPack;
        private Button btnSelectAvatarIdentityPack;
        private Button btnPreviewMascotVariants;
        private ComboBox cbMascotSceneCount;
        private PictureBox pbMascotPreview1;
        private PictureBox pbMascotPreview2;
        private PictureBox pbMascotPreview3;
        private PictureBox pbMascotPreview4;
        private ContextMenuStrip cmsMascotPreview;
        private ToolStripMenuItem miRegenerateScene;
        private int _selectedMascotPreviewSceneIndex = -1;
        private List<string> _mascotPreviewSceneScripts = new List<string>();
        private List<string> _mascotPreviewImagePaths = new List<string>();
        private TextBox txtPhilosophyInput;
        private DataGridView dgvAiVideoScriptReview;
        private NumericUpDown numAiTransitionDuration;
        private NumericUpDown numAiTextSize;
        private NumericUpDown numAiMusicVolume;
        private Panel pnlAiRenderProgress;
        private Panel pnlAiVideoGenStickyHost;
        private Panel pnlAiVideoGenScrollHost;
        private ProgressBar pbAiRenderSlot1;
        private ProgressBar pbAiRenderSlot2;
        private ProgressBar pbAiRenderSlot3;
        private Label lblAiRenderSlot1;
        private Label lblAiRenderSlot2;
        private Label lblAiRenderSlot3;
        private CancellationTokenSource _warmupCancellation;
        private CancellationTokenSource _warmupQueueCancellation;
        private CancellationTokenSource _currentQueueJobCancellation;
        private CancellationTokenSource _autoPostCancellation;
        private readonly object _sessionLogSync = new object();
        private readonly string _sessionLogFilePath;
        private WarmupRunState _currentWarmupState;

        private TextBox txtAffiliateKeywords;
        private NumericUpDown numAffiliateMaxResults;
        private Button btnHuntAffiliates;
        private Button btnStopHunt;
        private Button btnExportAffiliateCsv;
        private Button btnPushToAiVideoGen;
        private Button btnPushHighQualityToAiVideoGen;
        private Button btnDownloadSelectedAffiliate;
        private Button btnAffiliateDeepDive;
        private CheckBox chkAffiliateAutoEnrich;
        private Label lblAffiliateEnrichStatus;
        private LinkLabel lnkAffiliateDownloadFolder;
        private bool _affiliateDeepDiveRunning;
        private CancellationTokenSource _affiliateBulkDeepDiveCancelCts;
        private bool _affiliateAutoEnrichRunning;
        private CancellationTokenSource _affiliateAutoEnrichCts;
        private System.Windows.Forms.Timer _affiliateAutoEnrichSaveTimer;
        private System.Windows.Forms.Timer _affiliateHuntPrefsSaveTimer;
        private bool _affiliateRowEnrichRunning;
        private CancellationTokenSource _affiliateRowEnrichCts;
        private ComboBox cbAffiliateSearchMode;
        private CheckBox chkAffiliateOnlyHighQuality;
        private CheckBox chkAffiliateRankByEngagement;
        private NumericUpDown numAffiliateBufferMultiplier;
        private NumericUpDown numAffiliateMinSafety;
        private DataGridView dgvAffiliateResults;
        private BindingList<AffiliateCandidate> _affiliateBindingList;
        private List<AffiliateCandidate> _affiliateAllResults = new List<AffiliateCandidate>();
        private string _lastHuntKeyword = string.Empty;
        private List<string> _lastHuntKeywords = new List<string>();
        private bool _affiliateKeywordsPlaceholderActive;
        private bool _affiliateDeepDiveErrorColumnVisible;
        private bool _affiliateDownloadingBatch;
        private CancellationTokenSource _affiliateDownloadBatchCts;
        private DataGridView dgvVideoReupInput;
        private BindingList<VideoReupRowItem> _videoReupBindingList;
        private Button btnPushSelectionToVideoReup;
        private Button btnVideoReupExportSrtHeuristic;
        private Button btnVideoReupExportSrtGemini;
        private TextBox txtVideoReupHookDraft;
        private Button btnVideoReupHookGemini;
        private Button btnVideoReupHookRegen;
        private Button btnVideoReupLyriaHook;
        private Label lblVideoReupMusicPick;
        private ComboBox cbVideoReupMusic;
        private Button btnVideoReupRenderVideo;
        private TextBox txtVideoReupVideoUrl;
        private Label lblVideoReupVideoUrl;
        private Button btnVideoReupAddManualRow;
        private bool _videoReupSuppressUrlEditorEvents;
        private readonly SemaphoreSlim _videoReupDownloadGate = new SemaphoreSlim(1, 1);

        private List<AiVideoGenInputItem> _aiVideoGenInputBuffer = new List<AiVideoGenInputItem>();
        private BindingList<AiVideoScriptReviewItem> _aiVideoScriptBindingList;
        private readonly VideoProcessingService _videoProcessingService;
        private CancellationTokenSource _huntCancellation;
        private CancellationTokenSource _aiVideoGenCancellation;
        private readonly WarmupQueueScheduler _warmupQueueScheduler;
        private readonly WarmupQueueStateManager _warmupQueueStateManager;
        private readonly WarmupQueueHistoryManager _warmupQueueHistoryManager;
        private readonly ApprovalQueueManager _approvalQueueManager;
        private readonly SafetyScoreService _safetyScoreService;

        private readonly DuplicateGuardManager _duplicateGuardManager;
        private readonly PhilosophyVideoService _philosophyVideoService;
        private readonly GeminiService _geminiService;
        private readonly VideoReupRemixService _videoReupRemixService;
        private BindingList<WarmupQueueUiItem> _warmupQueueBindingList;
        private List<WarmupQueueHistoryRecord> _warmupQueueHistory;
        private List<ApprovalQueueItem> _approvalQueueItems;
        private bool _isWarmupQueueRunning;
        private bool _isWarmupQueuePaused;
        private bool _pauseNowRequested;

        public Form1()
        {
            InitializeTheme();
            InitializeComponent();

            _configManager = new ConfigManager();
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
            _videoReupRemixService = new VideoReupRemixService();
            _tikTokAutomation = new TikTokAutomation(_geminiService, _configManager, new CaptchaService());
            _tikTokAutomation.CaptchaDetected += TikTokAutomation_CaptchaDetected;
            _tikTokAutomation.CheckpointDetected += TikTokAutomation_CheckpointDetected;
            _affiliateHunter = new AffiliateHunter();
            _notificationService = new NotificationService();
            _videoProcessingService = new VideoProcessingService();
            _sessionLogFilePath = InitializeSessionLogFile();
            Shown += Form1_Shown;
        }

        private void InitializeTheme()
        {
            Text = "tiktok_Omni";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 700);
            BackColor = Color.FromArgb(24, 26, 32);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void InitializeComponent()
        {
            tabMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal
            };
            tabMain.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabMain.DrawItem += TabMain_DrawItem;

            // Settings first (API keys, profiles); then workflow tabs.
            tabAffiliateHunter = new TabPage("Săn Affiliate");
            tabAiVideoGen = new TabPage("AI Video Gen");
            tabAutoPost = new TabPage("Đăng tự động");
            tabAutoWarmup = new TabPage("Làm ấm tài khoản");
            tabSetting = new TabPage("Cài đặt");

            ConfigureTabPage(tabSetting);
            ConfigureTabPage(tabAffiliateHunter);
            ConfigureTabPage(tabAiVideoGen);
            ConfigureTabPage(tabAutoPost);
            ConfigureTabPage(tabAutoWarmup);

            tabMain.TabPages.AddRange(new[]
            {
                tabSetting,
                tabAffiliateHunter,
                tabAiVideoGen,
                tabAutoPost,
                tabAutoWarmup
            });

            BuildAffiliateHunterUi();
            BuildAiVideoGenUi();
            BuildAutoPostUi();
            BuildAutoWarmupUi();
            BuildSettingUi();
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
                Text = "Sẵn sàng — chọn tab hoặc Cài đặt để nhập API key và profile."
            };
            statusStripMain.Items.Add(tslStatusMain);
            tabMain.SelectedIndexChanged += TabMain_SelectedIndexChanged;

            Controls.Add(tabMain);
            Controls.Add(statusStripMain);
        }

        private void ConfigureTabPage(TabPage page)
        {
            page.BackColor = Color.FromArgb(31, 34, 42);
            page.ForeColor = Color.Gainsboro;
        }

        private void PostAdjustUiLayout()
        {
            ApplyTabEnhancements(tabSetting);
            ApplyTabEnhancements(tabAffiliateHunter);
            ApplyTabEnhancements(tabAiVideoGen);
            ApplyTabEnhancements(tabAutoPost);
            ApplyTabEnhancements(tabAutoWarmup);
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
                if (pnlAiVideoGenScrollHost != null)
                {
                    var required = MeasureContentSize(pnlAiVideoGenScrollHost.Controls);
                    pnlAiVideoGenScrollHost.AutoScrollMinSize = new Size(
                        Math.Max(0, required.Width + 24),
                        Math.Max(pnlAiVideoGenScrollHost.ClientSize.Height + 20, required.Height + 24));
                }

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

        private void NormalizeButtonsRecursive(Control.ControlCollection controls, int containerWidth)
        {
            if (controls == null)
            {
                return;
            }

            var buttons = controls
                .OfType<Button>()
                .OrderBy(x => x.Top)
                .ThenBy(x => x.Left)
                .ToList();
            var rowGroups = buttons
                .GroupBy(x => Math.Max(0, x.Top / 26))
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var row in rowGroups)
            {
                var ordered = row.OrderBy(x => x.Left).ToList();
                var rowTop = ordered.Count > 0 ? ordered.Min(x => x.Top) : 0;
                var currentLeft = ordered.Count > 0 ? Math.Max(12, ordered[0].Left) : 12;
                foreach (var btn in ordered)
                {
                    var width = btn.Width < 120 ? 120 : btn.Width;
                    var height = btn.Height < 32 ? 32 : Math.Min(40, btn.Height);
                    if (currentLeft + width > Math.Max(220, containerWidth - 24))
                    {
                        rowTop += 40;
                        currentLeft = 12;
                    }

                    btn.SetBounds(currentLeft, rowTop, width, height);
                    currentLeft = btn.Right + 10;
                }
            }

            foreach (Control control in controls)
            {
                if (control.HasChildren)
                {
                    NormalizeButtonsRecursive(control.Controls, control.ClientSize.Width > 200 ? control.ClientSize.Width : containerWidth);
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
                ["Start Queue"] = "Chạy hàng đợi",
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
                ["Triết lý/Quote"] = "Triết lý/Quote",
                ["Product Slideshow"] = "Sản phẩm (Slideshow)",
                ["Affiliate Deep"] = "Affiliate chuyên sâu",
                ["Mascot Story"] = "Mascot Story",
                ["Review Script before Render"] = "Duyệt kịch bản trước render",
                ["Run Mascot Story Pipeline"] = "Render Mascot Story",
                ["Run Affiliate Deep Video (4 scenes)"] = "Render Affiliate chuyên sâu",
                ["Run Philosophy/Quote Video"] = "Chạy video Triết lý/Quote",
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
                ["Error"] = "Lỗi"
            };

            if (map.TryGetValue(text, out var localized))
            {
                return localized;
            }

            return text;
        }

        private async void btnHuntAffiliates_Click(object sender, EventArgs e)
        {
            var keywords = ParseAffiliateKeywordListFromUi();
            if (keywords.Count == 0)
            {
                MessageBox.Show(this, "Hãy nhập ít nhất 1 từ khoá.", "Săn Affiliate",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (keywords.Count >= 3)
            {
                var estMinutes = (int)Math.Ceiling(keywords.Count * 1.5);
                var confirm = MessageBox.Show(this,
                    $"Bạn sắp quét {keywords.Count} từ khoá, dự kiến mất ~{estMinutes} phút. Tiếp tục?",
                    "Săn Affiliate",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            btnHuntAffiliates.Enabled = false;
            btnExportAffiliateCsv.Enabled = false;
            btnStopHunt.Enabled = true;
            _affiliateBindingList.Clear();
            _affiliateAllResults = new List<AffiliateCandidate>();
            _huntCancellation?.Dispose();
            _huntCancellation = new CancellationTokenSource();
            var huntToken = _huntCancellation.Token;
            _lastHuntKeywords = new List<string>(keywords);
            _lastHuntKeyword = string.Join(",", keywords);
            RefreshAffiliateDownloadFolderHint();
            var originalHuntButtonText = btnHuntAffiliates.Text;
            var completedKeywordCount = 0;

            try
            {
                var modeText = (cbAffiliateSearchMode?.SelectedItem?.ToString() ?? "Video").Trim();
                var mode = modeText.IndexOf("Video", StringComparison.OrdinalIgnoreCase) >= 0
                    ? AffiliateSearchMode.Video
                    : AffiliateSearchMode.Shop;
                Log(mode == AffiliateSearchMode.Shop
                    ? "Affiliate hunt started — TikTok Shop products."
                    : "Affiliate hunt started — TikTok video search.");
                var runningProfile = (cbRunningProfile?.SelectedItem?.ToString() ?? "default").Trim();

                var huntSettings = await _configManager.LoadAsync().ConfigureAwait(true);
                var desiredPerKeyword = (int)numAffiliateMaxResults.Value;
                var rankByEngagement = chkAffiliateRankByEngagement?.Checked ?? huntSettings.AffiliateRankByEngagementEnabled;
                var bufferMult = huntSettings.AffiliateHuntBufferMultiplier;
                if (numAffiliateBufferMultiplier != null)
                {
                    bufferMult = (double)numAffiliateBufferMultiplier.Value;
                }

                if (rankByEngagement && mode == AffiliateSearchMode.Video)
                {
                    var collectPreview = ComputeAffiliateCollectLimit(desiredPerKeyword, bufferMult, true, mode);
                    if (collectPreview > desiredPerKeyword)
                    {
                        Log($"[Affiliate] Ưu tiên engagement: tối đa {collectPreview} video/từ khoá → TikWM → giữ top {desiredPerKeyword}.");
                    }
                }

                for (var i = 0; i < keywords.Count; i++)
                {
                    huntToken.ThrowIfCancellationRequested();
                    var kw = keywords[i];
                    btnHuntAffiliates.Text = $"⏳ Quét {i + 1}/{keywords.Count}: '{kw}'...";

                    var collectLimit = ComputeAffiliateCollectLimit(desiredPerKeyword, bufferMult, rankByEngagement, mode);

                    var results = await _affiliateHunter.HuntAsync(
                        kw,
                        collectLimit,
                        mode,
                        huntToken,
                        Log,
                        _configManager,
                        runningProfile).ConfigureAwait(true);

                    List<AffiliateCandidate> batch = results ?? new List<AffiliateCandidate>();
                    if (rankByEngagement && mode == AffiliateSearchMode.Video && batch.Count > 0)
                    {
                        batch = await RankAffiliateBatchByEngagementAsync(batch, desiredPerKeyword, huntToken)
                            .ConfigureAwait(true);
                    }

                    MergeAffiliateHuntResultsIntoAll(_affiliateAllResults, batch, kw);
                    completedKeywordCount++;
                    RefreshAffiliateGridByQualityFilter();
                    RecomputeAffiliateDeepDiveErrorColumnVisibility();
                }

                Log($"Affiliate hunt completed. {_affiliateAllResults.Count} dòng gộp (từ {keywords.Count} từ khoá).");
                btnExportAffiliateCsv.Enabled = _affiliateBindingList.Count > 0;
                btnPushToAiVideoGen.Enabled = _affiliateBindingList.Count > 0;
                btnPushHighQualityToAiVideoGen.Enabled = _affiliateBindingList.Count > 0;

                // Auto enrich (Metrics → Anchor) chạy ngầm sau khi Hunt xong toàn bộ keywords.
                // Dùng CancellationToken.None vì _huntCancellation sẽ bị dispose trong finally
                // → user bấm Stop sau đó không hủy được enrich; thay vào đó dùng _affiliateAutoEnrichCts.
                StartAffiliateAutoEnrichIfEnabled(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                Log($"[Affiliate] Đã dừng (đã hoàn tất {completedKeywordCount}/{keywords.Count} từ khoá).");
                btnExportAffiliateCsv.Enabled = _affiliateBindingList.Count > 0;
                btnPushToAiVideoGen.Enabled = _affiliateBindingList.Count > 0;
                btnPushHighQualityToAiVideoGen.Enabled = _affiliateBindingList.Count > 0;
            }
            catch (Exception ex)
            {
                Log("Affiliate hunt failed: " + ex.Message);
            }
            finally
            {
                btnHuntAffiliates.Text = originalHuntButtonText;
                btnHuntAffiliates.Enabled = true;
                // Giữ Stop enabled khi auto enrich đang chạy (StartAffiliateAutoEnrichIfEnabled đã set true sync).
                btnStopHunt.Enabled = _affiliateAutoEnrichRunning;
                _huntCancellation?.Dispose();
                _huntCancellation = null;
                RefreshAffiliateDownloadFolderHint();
            }
        }

        private List<string> ParseAffiliateKeywordListFromUi()
        {
            if (_affiliateKeywordsPlaceholderActive)
            {
                return new List<string>();
            }

            return SplitAffiliateKeywordSegments(txtAffiliateKeywords?.Text ?? string.Empty);
        }

        private static List<string> SplitAffiliateKeywordSegments(string raw)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return ordered;
            }

            foreach (var part in raw.Replace("\r", string.Empty).Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (t.Length == 0 || !seen.Add(t))
                {
                    continue;
                }

                ordered.Add(t);
            }

            return ordered;
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

        private static void MergeAffiliateHuntResultsIntoAll(
            List<AffiliateCandidate> all,
            IList<AffiliateCandidate> batch,
            string keyword)
        {
            if (all == null || batch == null)
            {
                return;
            }

            var kw = (keyword ?? string.Empty).Trim();
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
                }
                else
                {
                    dup.SourceKeyword = MergeDistinctKeywordLabels(dup.SourceKeyword, incoming.SourceKeyword);
                }
            }
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
                        col.Visible = false;
                        break;
                    case "SourceKeyword":
                        col.HeaderText = "Từ khoá";
                        col.ToolTipText = "Từ khoá nguồn — bấm ô để xem đầy đủ / sao chép";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.Visible = true;
                        col.FillWeight = 10;
                        col.MinimumWidth = 80;
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
                    case "LinkedProduct":
                        col.HeaderText = "Link aff";
                        col.ToolTipText = "Link / sản phẩm affiliate — bấm ô để xem đầy đủ / sao chép";
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        col.Visible = true;
                        col.FillWeight = 14;
                        col.MinimumWidth = 90;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        break;
                    case "VoiceoverTranscript":
                        col.HeaderText = "Lời thoại";
                        col.ToolTipText = "Lời thoại (Deep Dive) — bấm ô để xem đầy đủ / sao chép";
                        col.Visible = true;
                        col.FillWeight = 16;
                        col.MinimumWidth = 100;
                        col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                        col.DefaultCellStyle.ForeColor = Color.LightGray;
                        col.ReadOnly = true;
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
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
                }
            }

            var displayOrder = new[]
            {
                "ProductName", "SourceKeyword", "Hashtags", "LinkedProduct",
                "PlayCount", "LikeCount", "CommentCount", "ShareCount", "CollectCount", "DurationSeconds", "CreateTimeUtc",
                "VoiceoverTranscript", "VideoUrl", "LastDeepDiveError",
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
            dgvAffiliateResults.Invalidate();
        }

        private void btnStopHunt_Click(object sender, EventArgs e)
        {
            if (_huntCancellation == null
                && !(_affiliateDownloadingBatch && _affiliateDownloadBatchCts != null)
                && !_affiliateAutoEnrichRunning)
            {
                return;
            }

            btnStopHunt.Enabled = false;
            _huntCancellation?.Cancel();
            if (_huntCancellation != null)
            {
                Log("Stopping affiliate hunt...");
            }

            _affiliateDownloadBatchCts?.Cancel();
            if (_affiliateDownloadingBatch)
            {
                Log("Đang hủy tải video hàng loạt...");
            }

            if (_affiliateAutoEnrichRunning && _affiliateAutoEnrichCts != null)
            {
                try { _affiliateAutoEnrichCts.Cancel(); }
                catch { /* ignore */ }
                Log("Đang hủy auto enrich...");
            }
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
            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                Log("Không có dữ liệu affiliate để đẩy sang AI Video Gen.");
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

            _aiVideoGenInputBuffer = new List<AiVideoGenInputItem>();
            foreach (var item in selectedItems
                .Where(x => x != null)
                .GroupBy(x => (x.VideoUrl ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()))
            {
                var productName = (item.ProductName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(productName))
                {
                    productName = (item.Creator ?? string.Empty).Trim();
                }

                _aiVideoGenInputBuffer.Add(new AiVideoGenInputItem
                {
                    ProductName = productName,
                    Price = string.IsNullOrWhiteSpace(item.Price) ? "N/A" : item.Price.Trim(),
                    ImageUrl = (item.ImageUrl ?? string.Empty).Trim()
                });
            }

            var withImage = _aiVideoGenInputBuffer.Count(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
            Log($"Đã đẩy {_aiVideoGenInputBuffer.Count} sản phẩm sang bộ nhớ tạm của AI Video Gen (có ảnh: {withImage}).");
            RefreshAiVideoGenInputGrid();
            tabMain.SelectedTab = tabAiVideoGen;
        }

        private void btnPushSelectionToVideoReup_Click(object sender, EventArgs e)
        {
            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                Log("Video reup: không có dữ liệu affiliate — hãy săn hoặc tải kết quả trước.");
                return;
            }

            if (_videoReupBindingList == null)
            {
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

            _videoReupBindingList.Clear();
            var skippedNoUrl = 0;
            foreach (var item in selectedItems
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
                    SourceKeyword = (item.SourceKeyword ?? string.Empty).Trim(),
                    ProductName = productName,
                    Price = string.IsNullOrWhiteSpace(item.Price) ? "N/A" : item.Price.Trim(),
                    VideoUrl = videoUrl,
                    ImageUrl = (item.ImageUrl ?? string.Empty).Trim(),
                    Hashtags = (item.Hashtags ?? string.Empty).Trim(),
                    VideoScript = (item.VideoScript ?? string.Empty).Trim()
                });
            }

            Log($"Video reup: đã nhập {_videoReupBindingList.Count} dòng từ Săn Affiliate (bỏ qua không có URL video: {skippedNoUrl}).");
            if (_videoReupBindingList.Count > 0)
            {
                tabMain.SelectedTab = tabAiVideoGen;
                if (tabAiVideoGenModes != null && tabAiVideoGenModes.TabPages.Count > 4)
                {
                    tabAiVideoGenModes.SelectedIndex = 4;
                }

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

            foreach (var v in items)
            {
                _videoReupBindingList.Remove(v);
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            Log($"Video reup: đã xóa {items.Count} dòng (Delete).");
        }

        private void DgvVideoReupInput_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (dgvVideoReupInput?.Columns == null || dgvVideoReupInput.Columns.Count == 0)
            {
                return;
            }

            dgvVideoReupInput.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvVideoReupInput.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            foreach (DataGridViewColumn col in dgvVideoReupInput.Columns)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

                var name = col.DataPropertyName ?? string.Empty;
                if (string.Equals(name, "ProductName", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Sản phẩm";
                    col.ToolTipText = "Tên dòng — dùng đặt tên file MP4/SRT khi xuất.";
                    col.FillWeight = 68f;
                    col.MinimumWidth = 64;
                }
                else if (string.Equals(name, "VideoUrl", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "URL video";
                    col.ToolTipText = "Link TikTok — đồng bộ với ô «URL video» phía trên khi chọn dòng; rời ô để tải qua TikWM.";
                    col.FillWeight = 95f;
                    col.MinimumWidth = 72;
                }
                else if (string.Equals(name, "Hashtags", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Hashtag";
                    col.ToolTipText = "Hashtag affiliate — đưa vào ngữ cảnh Gemini (hook / nhạc).";
                    col.FillWeight = 42f;
                    col.MinimumWidth = 52;
                }
                else if (string.Equals(name, "VideoScript", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Phân tích";
                    col.ToolTipText = "Deep dive / script — ngữ cảnh nội dung cho Gemini khi remix.";
                    col.FillWeight = 88f;
                    col.MinimumWidth = 72;
                }
                else if (string.Equals(name, "LastRemixOutputPath", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "MP4 remix";
                    col.ToolTipText = "Đường dẫn file video reup đã xuất (cắt đầu/đuôi, lật, hook Lyria + nhạc).";
                    col.FillWeight = 78f;
                    col.MinimumWidth = 72;
                }
                else if (string.Equals(name, "RemixStatus", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Remix";
                    col.ToolTipText = "Trạng thái lần remix gần nhất: Đang xử lý, Xong hoặc Lỗi.";
                    col.FillWeight = 22f;
                    col.MinimumWidth = 48;
                }
                else if (string.Equals(name, "RemixLastError", StringComparison.OrdinalIgnoreCase))
                {
                    col.HeaderText = "Lỗi";
                    col.ToolTipText = "Thông báo lỗi remix (nếu có). Rê chuột lên ô để xem đầy đủ nếu bị cắt.";
                    col.FillWeight = 52f;
                    col.MinimumWidth = 56;
                }
            }
        }

        private static bool TryResolveVideoReupCaptionDurationSeconds(VideoReupRowItem row, out double durationSeconds)
        {
            durationSeconds = 0d;
            if (row == null)
            {
                return false;
            }

            if (row.LastRemixOutputVideoDurationSec.HasValue && row.LastRemixOutputVideoDurationSec.Value > 0.5d)
            {
                durationSeconds = row.LastRemixOutputVideoDurationSec.Value;
                return true;
            }

            if (row.LastSourceVideoDurationSec.HasValue && row.LastSourceVideoDurationSec.Value > 2.5d)
            {
                durationSeconds = Math.Max(1d, row.LastSourceVideoDurationSec.Value - 2d);
                return true;
            }

            return false;
        }

        private static string ResolveVideoReupCaptionSourceText(VideoReupRowItem row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var script = (row.VideoScript ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(script))
            {
                return script;
            }

            var product = (row.ProductName ?? string.Empty).Trim();
            var tags = (row.Hashtags ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(product) && !string.IsNullOrEmpty(tags))
            {
                return product + ". " + tags;
            }

            if (!string.IsNullOrEmpty(product))
            {
                return product;
            }

            return tags;
        }

        private async void btnVideoReupExportSrtHeuristic_Click(object sender, EventArgs e)
        {
            await ExportVideoReupCaptionSrtAsync(useGemini: false).ConfigureAwait(true);
        }

        private async void btnVideoReupExportSrtGemini_Click(object sender, EventArgs e)
        {
            await ExportVideoReupCaptionSrtAsync(useGemini: true).ConfigureAwait(true);
        }

        private async Task ExportVideoReupCaptionSrtAsync(bool useGemini)
        {
            if (dgvVideoReupInput?.SelectedRows == null || dgvVideoReupInput.SelectedRows.Count == 0)
            {
                Log("Video reup caption: chọn ít nhất một dòng trong bảng.");
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (useGemini && string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                Log("Video reup caption (Gemini): chưa cấu hình AI API Key trong Cài đặt.");
                return;
            }

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoReup", "Captions");
            try
            {
                Directory.CreateDirectory(outDir);
            }
            catch (Exception ex)
            {
                Log("Video reup caption: không tạo được thư mục xuất — " + ex.Message);
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var ok = 0;
            var skipped = 0;

            try
            {
                foreach (DataGridViewRow gridRow in dgvVideoReupInput.SelectedRows)
                {
                    if (!(gridRow?.DataBoundItem is VideoReupRowItem row))
                    {
                        skipped++;
                        continue;
                    }

                    if (!TryResolveVideoReupCaptionDurationSeconds(row, out var duration) || duration < 1d)
                    {
                        Log($"Video reup caption: bỏ qua «{row.ProductName}» — chưa có độ dài video (chạy «Remix reup» một lần để probe nguồn / MP4, rồi xuất SRT).");
                        skipped++;
                        continue;
                    }

                    var sourceText = ResolveVideoReupCaptionSourceText(row);
                    if (string.IsNullOrWhiteSpace(sourceText))
                    {
                        Log($"Video reup caption: bỏ qua «{row.ProductName}» — không có script / tên / hashtag để làm phụ đề.");
                        skipped++;
                        continue;
                    }

                    List<CaptionTiming> timings;
                    if (useGemini)
                    {
                        timings = await VideoReupCaptionService.BuildWithGeminiAsync(
                            _geminiService,
                            sourceText,
                            duration,
                            settings.AiProvider,
                            settings.AiApiKey,
                            settings.AiModel,
                            CancellationToken.None).ConfigureAwait(true);
                    }
                    else
                    {
                        timings = VideoReupCaptionService.BuildHeuristic(sourceText, duration);
                    }

                    var srt = VideoReupCaptionService.ToSrtContent(timings);
                    if (string.IsNullOrWhiteSpace(srt))
                    {
                        Log($"Video reup caption: không tạo được khung thời gian — «{row.ProductName}».");
                        skipped++;
                        continue;
                    }

                    var baseName = VideoReupCaptionService.SanitizeFileNameFragment(row.ProductName) + "_" + stamp + "_" + ok;
                    var path = Path.Combine(outDir, baseName + ".srt");
                    try
                    {
                        File.WriteAllText(path, srt, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    }
                    catch (Exception ex)
                    {
                        Log($"Video reup caption: ghi file lỗi ({row.ProductName}): " + ex.Message);
                        skipped++;
                        continue;
                    }

                    row.LastCaptionSrtPath = path;
                    ok++;
                    Log($"Video reup caption: đã xuất {(useGemini ? "Gemini + " : string.Empty)}SRT → {path}");
                }

                _videoReupBindingList?.ResetBindings();
                Log($"Video reup caption: xong — {ok} file, bỏ qua {skipped}. Thư mục: {outDir}");
            }
            catch (Exception ex)
            {
                Log("Video reup caption lỗi: " + ex.Message);
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
            }
        }

        private void SetVideoReupCaptionButtonsEnabled(bool enabled)
        {
            if (btnVideoReupExportSrtHeuristic != null)
            {
                btnVideoReupExportSrtHeuristic.Enabled = enabled;
            }

            if (btnVideoReupExportSrtGemini != null)
            {
                btnVideoReupExportSrtGemini.Enabled = enabled;
            }

            if (btnPushSelectionToVideoReup != null)
            {
                btnPushSelectionToVideoReup.Enabled = enabled;
            }

            if (btnVideoReupHookGemini != null)
            {
                btnVideoReupHookGemini.Enabled = enabled;
            }

            if (btnVideoReupHookRegen != null)
            {
                btnVideoReupHookRegen.Enabled = enabled;
            }

            if (btnVideoReupLyriaHook != null)
            {
                btnVideoReupLyriaHook.Enabled = enabled;
            }

            if (cbVideoReupMusic != null)
            {
                cbVideoReupMusic.Enabled = enabled;
            }

            if (btnVideoReupRenderVideo != null)
            {
                btnVideoReupRenderVideo.Enabled = enabled;
            }

            if (txtVideoReupVideoUrl != null)
            {
                txtVideoReupVideoUrl.ReadOnly = !enabled;
            }

            if (btnVideoReupAddManualRow != null)
            {
                btnVideoReupAddManualRow.Enabled = enabled;
            }

            if (txtVideoReupHookDraft != null)
            {
                txtVideoReupHookDraft.ReadOnly = !enabled;
            }
        }

        private bool TryGetVideoReupSelectedRow(out VideoReupRowItem row)
        {
            row = null;
            if (dgvVideoReupInput?.SelectedRows == null || dgvVideoReupInput.SelectedRows.Count == 0)
            {
                return false;
            }

            foreach (DataGridViewRow gridRow in dgvVideoReupInput.SelectedRows)
            {
                if (gridRow?.DataBoundItem is VideoReupRowItem r)
                {
                    row = r;
                    return true;
                }
            }

            return false;
        }

        private void FlushVideoReupHookDraftFromEditor()
        {
            if (!TryGetVideoReupSelectedRow(out var row) || txtVideoReupHookDraft == null)
            {
                return;
            }

            row.ReupHookDraft = (txtVideoReupHookDraft.Text ?? string.Empty).Trim();
        }

        private void RefreshVideoReupMusicCombo()
        {
            if (cbVideoReupMusic == null)
            {
                return;
            }

            var prev = (cbVideoReupMusic.SelectedItem ?? string.Empty).ToString();
            cbVideoReupMusic.Items.Clear();
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoReup", "Music");
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.mp3", SearchOption.TopDirectoryOnly))
                {
                    cbVideoReupMusic.Items.Add(Path.GetFileName(f));
                }
            }

            if (cbVideoReupMusic.Items.Count == 0)
            {
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

        private void BindVideoReupEditorFromRow(VideoReupRowItem row)
        {
            if (txtVideoReupHookDraft == null || cbVideoReupMusic == null)
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
                txtVideoReupHookDraft.Text = string.Empty;
                return;
            }

            txtVideoReupHookDraft.Text = row.ReupHookDraft ?? string.Empty;
            RefreshVideoReupMusicCombo();
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

        private void dgvVideoReupInput_SelectionChanged(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                BindVideoReupEditorFromRow(null);
                return;
            }

            BindVideoReupEditorFromRow(row);
        }

        private void txtVideoReupHookDraft_Leave(object sender, EventArgs e)
        {
            FlushVideoReupHookDraftFromEditor();
            _videoReupBindingList?.ResetBindings();
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

        private async Task VideoReupEnsureDownloadWithGateAsync(VideoReupRowItem row, AppSettings settings)
        {
            await _videoReupDownloadGate.WaitAsync().ConfigureAwait(true);
            try
            {
                await _videoReupRemixService.EnsureVideoDownloadedAsync(
                    row,
                    settings,
                    _affiliateHunter,
                    Log,
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
                    _videoReupBindingList?.ResetBindings();
                    try
                    {
                        await VideoReupEnsureDownloadWithGateAsync(row, settings).ConfigureAwait(true);
                        row.RemixStatus = "Đã tải video";
                        Log($"Video reup: đã tải video nguồn — «{row.ProductName}».");
                    }
                    catch (Exception ex)
                    {
                        row.RemixStatus = "Lỗi";
                        row.RemixLastError = ex.Message;
                        Log($"Video reup tải video «{row.ProductName}»: {ex.Message}");
                    }

                    _videoReupBindingList?.ResetBindings();
                }
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
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
                Log("Video reup (tải sau nhập): " + ex.Message);
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

            if (!VideoReupRemixService.LooksLikeHttpVideoUrl(row.VideoUrl))
            {
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            row.RemixStatus = "Đang tải video…";
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                await VideoReupEnsureDownloadWithGateAsync(row, settings).ConfigureAwait(true);
                row.RemixStatus = "Đã tải video";
                Log($"Video reup: đã tải video nguồn — «{row.ProductName}».");
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = ex.Message;
                Log("Video reup tải video: " + ex.Message);
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
                _videoReupBindingList?.ResetBindings();
            }
        }

        private void btnVideoReupAddManualRow_Click(object sender, EventArgs e)
        {
            if (_videoReupBindingList == null || dgvVideoReupInput == null)
            {
                return;
            }

            var n = _videoReupBindingList.Count + 1;
            _videoReupBindingList.Add(new VideoReupRowItem
            {
                ProductName = "Video " + n,
                Price = "N/A",
                VideoUrl = string.Empty
            });

            var idx = _videoReupBindingList.Count - 1;
            if (idx >= 0 && dgvVideoReupInput.Rows.Count > idx)
            {
                dgvVideoReupInput.ClearSelection();
                dgvVideoReupInput.Rows[idx].Selected = true;
                dgvVideoReupInput.FirstDisplayedScrollingRowIndex = idx;
            }

            txtVideoReupVideoUrl?.Focus();
            Log("Video reup: đã thêm dòng — dán URL TikTok vào ô «URL video», rời ô để tải.");
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
            await RunVideoReupHookGeminiAsync(regenerateLabel: false).ConfigureAwait(true);
        }

        private async void btnVideoReupHookRegen_Click(object sender, EventArgs e)
        {
            await RunVideoReupHookGeminiAsync(regenerateLabel: true).ConfigureAwait(true);
        }

        private async Task RunVideoReupHookGeminiAsync(bool regenerateLabel)
        {
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                Log("Video reup hook: chọn một dòng trong bảng.");
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (!VideoReupRemixService.TryValidateHookGeminiStep(row, settings, out var preflightError))
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = preflightError;
                Log("Video reup hook: " + preflightError);
                _videoReupBindingList?.ResetBindings();
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            row.RemixStatus = regenerateLabel ? "Gemini (lại)…" : "Gemini hook…";
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            try
            {
                await _videoReupRemixService.GenerateHookAndSuggestMusicAsync(
                    row,
                    settings,
                    _geminiService,
                    Log,
                    CancellationToken.None).ConfigureAwait(true);
                if (txtVideoReupHookDraft != null)
                {
                    txtVideoReupHookDraft.Text = row.ReupHookDraft ?? string.Empty;
                }

                RefreshVideoReupMusicCombo();
                if (!string.IsNullOrWhiteSpace(row.ReupSuggestedMusicFile) && cbVideoReupMusic?.Items.Contains(row.ReupSuggestedMusicFile) == true)
                {
                    cbVideoReupMusic.SelectedItem = row.ReupSuggestedMusicFile;
                    row.ReupSelectedMusicFile = row.ReupSuggestedMusicFile;
                }

                row.RemixStatus = "Hook OK";
                Log(regenerateLabel ? "Video reup hook: Gemini đã tạo lại hook + gợi ý nhạc." : "Video reup hook: Gemini đã tạo hook + gợi ý nhạc.");
                _videoReupBindingList?.ResetBindings();
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = ex.Message;
                Log("Video reup hook lỗi: " + ex.Message);
                _videoReupBindingList?.ResetBindings();
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
            }
        }

        private async void btnVideoReupLyriaHook_Click(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                Log("Video reup Lyria: chọn một dòng trong bảng.");
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (!VideoReupRemixService.TryValidateLyriaAudioStep(row, settings, out var preflightError))
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = preflightError;
                Log("Video reup Lyria: " + preflightError);
                _videoReupBindingList?.ResetBindings();
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            row.RemixStatus = "Lyria…";
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            try
            {
                await _videoReupRemixService.BuildLyriaHookAudioAsync(
                    row,
                    settings,
                    _affiliateHunter,
                    Log,
                    CancellationToken.None).ConfigureAwait(true);
                row.RemixStatus = "Âm thanh hook OK";
                Log("Video reup Lyria: đã tạo WAV hook — chọn nhạc rồi «Render video».");
                _videoReupBindingList?.ResetBindings();
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = ex.Message;
                Log("Video reup Lyria lỗi: " + ex.Message);
                _videoReupBindingList?.ResetBindings();
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
            }
        }

        private async void btnVideoReupRenderVideo_Click(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRow(out var row))
            {
                Log("Video reup render: chọn một dòng trong bảng.");
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (!VideoReupRemixService.TryValidateFinalRenderStep(row, settings, out var preflightError))
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = preflightError;
                Log("Video reup render: " + preflightError);
                _videoReupBindingList?.ResetBindings();
                return;
            }

            SetVideoReupCaptionButtonsEnabled(false);
            row.RemixStatus = "Đang render…";
            row.RemixLastError = string.Empty;
            _videoReupBindingList?.ResetBindings();
            try
            {
                var result = await _videoReupRemixService.RenderFinalVideoAsync(
                    row,
                    settings,
                    Log,
                    CancellationToken.None).ConfigureAwait(true);
                row.LastRemixOutputPath = result.OutputPath ?? string.Empty;
                row.LastSourceVideoDurationSec = result.SourceDurationSeconds;
                row.LastRemixOutputVideoDurationSec = result.OutputFileDurationSeconds;
                row.LastHookDurationUsedSec = result.HookDurationSecondsUsed;
                row.RemixStatus = "Xong";
                row.RemixLastError = string.Empty;
                Log($"Video reup render: xong → {row.LastRemixOutputPath} (≈{result.OutputFileDurationSeconds:0.##}s).");
                _videoReupBindingList?.ResetBindings();
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = ex.Message;
                Log("Video reup render lỗi: " + ex.Message);
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

            var keyword = string.IsNullOrWhiteSpace(_lastHuntKeyword)
                ? (_affiliateKeywordsPlaceholderActive ? "default" : (txtAffiliateKeywords?.Text ?? "default"))
                : _lastHuntKeyword;
            var safeKeyword = SanitizePathSegment(keyword);
            if (string.IsNullOrWhiteSpace(safeKeyword))
            {
                safeKeyword = "default";
            }

            var saveDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads", safeKeyword));
            Directory.CreateDirectory(saveDir);
            Log("[Download] Thư mục lưu file: " + saveDir);
            RefreshAffiliateDownloadFolderHint();
            _affiliateDownloadingBatch = true;
            _affiliateDownloadBatchCts?.Dispose();
            _affiliateDownloadBatchCts = new CancellationTokenSource();
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
                _affiliateDownloadBatchCts?.Dispose();
                _affiliateDownloadBatchCts = null;
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
            var keyword = string.IsNullOrWhiteSpace(_lastHuntKeyword)
                ? (_affiliateKeywordsPlaceholderActive ? "default" : (txtAffiliateKeywords?.Text ?? "default").Trim())
                : _lastHuntKeyword.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                keyword = "default";
            }

            var safeKeyword = SanitizePathSegment(keyword);
            if (string.IsNullOrWhiteSpace(safeKeyword))
            {
                safeKeyword = "default";
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads", safeKeyword));
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
                lnkAffiliateDownloadFolder.Tag = dir;
                var shortPath = EllipsisMiddlePath(dir, 105);
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
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = GetAffiliateVideoSaveDirectory();
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

            RefreshAffiliateDeepDiveButtonState();
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
                    e.Value = "(Chưa phân tích — bấm 🔍 Deep Dive)";
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
                if (n <= 0 && (candidate == null || candidate.MetricsCapturedAtUtc == DateTime.MinValue))
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = Color.DimGray;
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

        private void btnPushHighQualityToAiVideoGen_Click(object sender, EventArgs e)
        {
            var source = _affiliateAllResults ?? new List<AffiliateCandidate>();
            var threshold = (int)(numAffiliateMinSafety?.Value ?? 75);
            var highQuality = source
                .Where(x => x != null)
                .Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
                .Where(x => !string.IsNullOrWhiteSpace(x.Price) && !string.Equals(x.Price.Trim(), "N/A", StringComparison.OrdinalIgnoreCase))
                .Where(x => x.SafetyScore >= threshold)
                .ToList();

            if (highQuality.Count == 0)
            {
                Log($"Không có sản phẩm high-quality (score>={threshold}, có ảnh + có giá).");
                return;
            }

            _aiVideoGenInputBuffer = highQuality.Select(item => new AiVideoGenInputItem
            {
                ProductName = (item.ProductName ?? string.Empty).Trim(),
                Price = (item.Price ?? string.Empty).Trim(),
                ImageUrl = (item.ImageUrl ?? string.Empty).Trim()
            }).ToList();

            Log($"Đã push {highQuality.Count} sản phẩm high-quality sang AI Video Gen.");
            RefreshAiVideoGenInputGrid();
            tabMain.SelectedTab = tabAiVideoGen;
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
            var threshold = (int)(numAffiliateMinSafety?.Value ?? 75);
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

            SortAffiliateCandidatesByViewsDescending(rows);

            foreach (var item in rows)
            {
                _affiliateBindingList.Add(item);
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



        private void RefreshAiVideoGenInputGrid()
        {
            if (_aiVideoGenBindingList == null)
            {
                return;
            }

            _aiVideoGenBindingList.Clear();
            foreach (var item in _aiVideoGenInputBuffer)
            {
                _aiVideoGenBindingList.Add(new AiVideoGenInputItem
                {
                    ProductName = item.ProductName,
                    Price = item.Price,
                    ImageUrl = item.ImageUrl
                });
            }

            _aiVideoScriptBindingList?.Clear();
            txtAiVideoGenPrompt.Text = string.Empty;
        }

        private async void btnGenerateGeminiPrompt_Click(object sender, EventArgs e)
        {
            if (_aiVideoGenInputBuffer == null || _aiVideoGenInputBuffer.Count == 0)
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

                var lines = new List<string>();
                for (var i = 0; i < _aiVideoGenInputBuffer.Count; i++)
                {
                    var x = _aiVideoGenInputBuffer[i];
                    lines.Add($"{i + 1}. Tên: {x.ProductName}; Giá: {x.Price}; Ảnh: {x.ImageUrl}");
                }
                var productLines = string.Join(Environment.NewLine, lines);

                var prompt = "Bạn là biên kịch TikTok chuyên viết kịch bản bán hàng theo dạng kể chuyện. " +
                             "Hãy viết DUY NHẤT 1 kịch bản tiếng Việt dài khoảng 30-45 giây, dựa trên danh sách sản phẩm bên dưới. " +
                             "Yêu cầu bắt buộc: " +
                             "1) Mở đầu có tình huống đời thường gây tò mò trong 2-3 câu đầu; " +
                             "2) Có yếu tố hài hước HOẶC kịch tính nhẹ, tự nhiên, không lố; " +
                             "3) Lồng được lợi ích chính của sản phẩm vào mạch chuyện; " +
                             "4) Có câu chốt CTA mềm mại ở cuối; " +
                             "5) Trình bày thành đoạn thoại liền mạch, dễ đọc voice-over, không dùng markdown, không đánh số mục. " +
                             "Danh sách sản phẩm:" + Environment.NewLine + productLines;

                var gemini = new GeminiService();
                var script = await gemini.GenerateScriptAsync(
                    prompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel);

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
            if (string.IsNullOrWhiteSpace(txtAutoPostFolder.Text) || !Directory.Exists(txtAutoPostFolder.Text))
            {
                Log("Auto Post: thư mục video không hợp lệ.");
                return;
            }

            var explicitVideoPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(explicitVideoPath) &&
                !EnumerateVideoFilesInFolder(txtAutoPostFolder.Text.Trim()).Any())
            {
                Log("Auto Post: không có file video (.mp4, .mov, …) trong thư mục.");
                return;
            }

            if (chkAutoPostVideoApproved != null && !chkAutoPostVideoApproved.Checked)
            {
                Log("Auto Post: hãy xem video và tick \"Tôi đã xem và duyệt video\" trước khi đăng.");
                MessageBox.Show(
                    this,
                    "Vui lòng xem video (nút \"Xem video\") và tick xác nhận đã duyệt trước khi đăng.",
                    "Đăng tự động",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var captionDraft = txtAutoPostCaption?.Text ?? string.Empty;
            var finalCaption = BuildFinalAutoPostCaptionBody(captionDraft, txtAutoPostHashtags?.Text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(finalCaption))
            {
                Log("Auto Post: chưa có caption — hãy tạo bằng Gemini hoặc nhập tay.");
                return;
            }

            var uploadOnly = chkAutoPostUploadOnly?.Checked ?? false;

            var policySettings = await _configManager.LoadAsync();
            var profile = cbAutoPostProfile?.SelectedItem?.ToString() ?? string.Empty;
            var captionPreview = BuildAutoPostCaptionPreview(
                txtAutoPostFolder.Text.Trim(),
                txtAutoPostHashtags.Text.Trim(),
                explicitVideoPath,
                captionDraft.Trim());
            var videoFingerprintSource = BuildAutoPostFingerprintSource(
                txtAutoPostFolder.Text.Trim(),
                captionPreview,
                txtAutoPostHashtags.Text.Trim(),
                profile,
                explicitVideoPath);
            var postFingerprint = DuplicateGuardManager.ComputeSha256Fingerprint(videoFingerprintSource);
            var isDuplicatePost = await _duplicateGuardManager.ExistsRecentAsync("post", postFingerprint, TimeSpan.FromDays(7));
            if (isDuplicatePost)
            {
                Log("[SAFEGUARD] Phát hiện nội dung Auto Post trùng trong 7 ngày gần đây. Đã chặn để tránh spam.");
                return;
            }

            var postRisk = _safetyScoreService.ScoreAutoPost(captionPreview, txtAutoPostHashtags.Text.Trim(), txtAutoPostFolder.Text.Trim(), profile);
            var forcePrePostApproval = policySettings.AlwaysRequirePrePostApproval ?? true;
            var postScoreThreshold = policySettings.BlockPostingSafetyScoreBelow;
            var mustApprovePost = forcePrePostApproval || postRisk.Score < postScoreThreshold || postRisk.RequiresManualApproval;
            if (mustApprovePost)
            {
                await EnqueueApprovalItemAsync(new ApprovalQueueItem
                {
                    JobType = ApprovalJobType.AutoPost,
                    Status = ApprovalStatus.Pending,
                    Profile = profile,
                    Title = "Auto Post - Requires Approval",
                    PayloadJson = JsonConvert.SerializeObject(new AutoPostApprovalPayload
                    {
                        VideoFolder = txtAutoPostFolder.Text.Trim(),
                        Hashtags = txtAutoPostHashtags.Text.Trim(),
                        Profile = profile,
                        VideoFilePath = explicitVideoPath,
                        CaptionFull = finalCaption,
                        UploadOnlyNoPublish = uploadOnly
                    }),
                    SafetyScore = postRisk.Score,
                    RequiresManualApproval = true,
                    RiskReasons = string.Join("; ", postRisk.Reasons),
                    OriginalPreview = captionPreview
                });
                Log($"[APPROVAL] Auto Post đã đưa vào hàng duyệt (score {postRisk.Score}).");
                if (policySettings.AutoRunApprovedQueue ?? false)
                {
                    await AutoRunApprovedQueueItemsAsync();
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

            try
            {
                Log("Auto Post started...");
                await _tikTokAutomation.AutoPostUpToProductLinkAsync(
                    txtAutoPostFolder.Text.Trim(),
                    txtAutoPostHashtags.Text.Trim(),
                    profile,
                    _autoPostCancellation.Token,
                    Log,
                    explicitVideoPath,
                    finalCaption,
                    clickPublish: !uploadOnly);
                await _duplicateGuardManager.AddAsync(new DuplicateGuardRecord
                {
                    Type = "post",
                    Fingerprint = postFingerprint,
                    Profile = profile,
                    Summary = captionPreview
                });
                if (uploadOnly)
                {
                    Log("Đã upload xong, mời ông gắn link Affiliate tay và nhấn Đăng");
                    MessageBox.Show(
                        this,
                        "Đã upload xong, mời ông gắn link Affiliate tay và nhấn Đăng.",
                        "Đăng tự động",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    Log("Luồng đăng đã chạy — kiểm tra trạng thái video trên TikTok.");
                    MessageBox.Show(
                        this,
                        "Đã gửi lệnh đăng bài (nếu có captcha, hãy xử lý trong trình duyệt rồi bot sẽ tiếp tục). Kiểm tra kết quả trên TikTok.",
                        "Đăng tự động",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Auto Post stopped.");
            }
            catch (Exception ex)
            {
                Log("Auto Post failed: " + ex.Message);
            }
            finally
            {
                btnStartAutoPost.Enabled = true;
                _autoPostCancellation?.Dispose();
                _autoPostCancellation = null;
            }
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
                var text = await _geminiService.GenerateTikTokCaptionFromVideoAsync(
                    explicitPath,
                    styleKey,
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
                await _tikTokAutomation.CloseAutoPostBrowserAsync(Log);
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

                    File.WriteAllText(dialog.FileName, text);
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
            if (_aiVideoGenInputBuffer == null || _aiVideoGenInputBuffer.Count == 0)
            {
                Log("AI Video Gen: chưa có sản phẩm đầu vào.");
                return;
            }

            SyncSelectedScriptFromEditor();
            var script = txtAiVideoGenPrompt?.Text?.Trim() ?? string.Empty;
            var hasReviewScripts = _aiVideoScriptBindingList != null &&
                                   _aiVideoScriptBindingList.Count == _aiVideoGenInputBuffer.Count &&
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
            var profile = cbRunningProfile?.SelectedItem?.ToString() ?? string.Empty;
            var renderFingerprint = BuildRenderFingerprint(fingerprintScript, _aiVideoGenInputBuffer, profile);
            var isDuplicateRender = await _duplicateGuardManager.ExistsRecentAsync("render", renderFingerprint, TimeSpan.FromDays(7));
            if (isDuplicateRender)
            {
                Log("[SAFEGUARD] Kịch bản + bộ sản phẩm này đã render gần đây (7 ngày). Đã chặn để tránh video trùng.");
                return;
            }
            var renderRisk = _safetyScoreService.ScoreRender(safetyScript, _aiVideoGenInputBuffer, profile);
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
                        Profile = profile
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

            btnRenderAiVideo.Enabled = false;
            btnGenerateGeminiPrompt.Enabled = false;
            _aiVideoGenCancellation?.Dispose();
            _aiVideoGenCancellation = new CancellationTokenSource();

            try
            {
                Log("AI Video Gen: bắt đầu render video thực tế...");
                var settings = await _configManager.LoadAsync();
                settings.VideoTransitionDurationSeconds = (double)numAiTransitionDuration.Value;
                settings.VideoTextSize = (int)numAiTextSize.Value;
                settings.VideoMusicVolume = (int)numAiMusicVolume.Value;
                await _configManager.SaveAsync(settings);

                ResetAiRenderSlotProgress();
                var outputPaths = await _videoProcessingService.GenerateProductVideosAsync(
                    _aiVideoGenInputBuffer,
                    script,
                    perItemScripts,
                    settings,
                    Log,
                    UpdateAiRenderProgress,
                    _aiVideoGenCancellation.Token);
                await _duplicateGuardManager.AddAsync(new DuplicateGuardRecord
                {
                    Type = "render",
                    Fingerprint = renderFingerprint,
                    Profile = profile,
                    Summary = $"batch:{outputPaths.Count}"
                });

                if (outputPaths.Count > 0)
                {
                    Log($"AI Video Gen: render hoàn tất {outputPaths.Count} video.");
                    foreach (var path in outputPaths)
                    {
                        Log("AI Video Gen: output -> " + path);
                    }

                    _ = SendNotificationAsync(
                        "AI Video Render Completed",
                        $"Đã render xong {outputPaths.Count} video. Kiểm tra output trong thư mục generated_videos.",
                        "video_render_completed",
                        "info");
                }
                else
                {
                    Log("AI Video Gen: không có video output.");
                }
            }
            catch (OperationCanceledException)
            {
                Log("AI Video Gen: đã dừng render.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen render thất bại: " + ex.Message);
            }
            finally
            {
                btnRenderAiVideo.Enabled = true;
                btnGenerateGeminiPrompt.Enabled = true;
                _aiVideoGenCancellation?.Dispose();
                _aiVideoGenCancellation = null;
            }
        }

        private async void btnReviewScriptBeforeRender_Click(object sender, EventArgs e)
        {
            if (_aiVideoGenInputBuffer == null || _aiVideoGenInputBuffer.Count == 0)
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

                var selectedItems = _aiVideoGenInputBuffer.Take(10).ToList();
                if (_aiVideoGenInputBuffer.Count > 10)
                {
                    Log("AI Video Gen: chỉ tạo review script cho 10 sản phẩm đầu tiên mỗi lượt.");
                }

                var lines = new List<string>();
                for (var i = 0; i < selectedItems.Count; i++)
                {
                    var x = selectedItems[i];
                    lines.Add($"{i + 1}. Tên: {x.ProductName}; Giá: {x.Price}; Ảnh: {x.ImageUrl}");
                }

                var prompt = "Bạn là biên kịch TikTok kể chuyện bán hàng. Hãy tạo script storytelling tiếng Việt cho TỪNG sản phẩm riêng biệt. " +
                             "Mỗi script dài khoảng 30-45 giây, giọng tự nhiên, có mở cảnh đời thường, nêu lợi ích sản phẩm và CTA mềm. " +
                             "Trả về DUY NHẤT JSON ARRAY, không markdown, không giải thích. " +
                             "Mỗi phần tử gồm: index (số thứ tự sản phẩm), script (chuỗi). " +
                             "Danh sách sản phẩm:\n" + string.Join("\n", lines);

                var gemini = new GeminiService();
                var raw = await gemini.GenerateScriptAsync(
                    prompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel);

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
            if (_aiVideoGenInputBuffer == null || _aiVideoGenInputBuffer.Count < 4)
            {
                Log("Affiliate Deep Video: cần ít nhất 4 ảnh của cùng 1 sản phẩm.");
                return;
            }

            btnRunAffiliateDeepVideo.Enabled = false;
            btnRenderAiVideo.Enabled = false;
            btnGenerateGeminiPrompt.Enabled = false;
            btnReviewScriptBeforeRender.Enabled = false;

            try
            {
                var firstName = (_aiVideoGenInputBuffer[0]?.ProductName ?? string.Empty).Trim();
                var top4 = _aiVideoGenInputBuffer.Take(4).ToList();
                if (top4.Any(x => !string.Equals((x?.ProductName ?? string.Empty).Trim(), firstName, StringComparison.OrdinalIgnoreCase)))
                {
                    Log("Affiliate Deep Video: 4 ảnh đầu phải thuộc cùng một sản phẩm (ProductName giống nhau).");
                    return;
                }

                var settings = await _configManager.LoadAsync();
                Log("Affiliate Deep Video: bắt đầu pipeline 4 ảnh -> AI image -> motion prompt -> Veo 3...");
                ResetAiRenderSlotProgress();
                UpdateSlotProgress(1, 5, "Luồng 1: Tạo ảnh");
                var result = await _videoProcessingService.GenerateAffiliateProductVideoAsync(
                    top4,
                    settings,
                    Log,
                    CancellationToken.None,
                    (percent, stage) => UpdateSinglePipelineProgress(percent, stage));

                Log("Affiliate Deep Video: hoàn tất.");
                for (var i = 0; i < result.SceneVideos.Count; i++)
                {
                    var scene = result.SceneVideos[i];
                    Log($"Scene {scene.Index}: clip -> {scene.SceneVideoPath}");
                }
                Log("Affiliate Deep Video: final -> " + result.FinalVideoPath);
            }
            catch (Exception ex)
            {
                Log("Affiliate Deep Video thất bại: " + ex.Message);
            }
            finally
            {
                btnRunAffiliateDeepVideo.Enabled = true;
                btnRenderAiVideo.Enabled = true;
                btnGenerateGeminiPrompt.Enabled = true;
                btnReviewScriptBeforeRender.Enabled = true;
            }
        }

        private void btnBrowseMascotImage_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.gif";
                dialog.Title = "Chọn ảnh Linh vật/Người mẫu đại diện";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtMascotImagePath.Text = dialog.FileName;
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

                txtAvatarIdentityPack.Text = $"{profileDir} ({files.Count} ảnh)";
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

            btnRunMascotChannelPipeline.Enabled = false;
            btnRunAffiliateDeepVideo.Enabled = false;
            btnRenderAiVideo.Enabled = false;
            btnGenerateGeminiPrompt.Enabled = false;
            btnReviewScriptBeforeRender.Enabled = false;
            btnSelectAvatarIdentityPack.Enabled = false;
            btnPreviewMascotVariants.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync();
                var identityPack = LoadAvatarIdentityPackForProfile(cbRunningProfile?.SelectedItem?.ToString());
                if (identityPack.Count < 3 || identityPack.Count > 5)
                {
                    Log("Mascot Story Pipeline: Bộ nhận diện chưa hợp lệ. Hãy chọn 3-5 ảnh trong AvatarVault.");
                    return;
                }

                Log("Mascot Story Pipeline: bắt đầu tạo 4 phân cảnh từ ảnh đại diện...");
                ResetAiRenderSlotProgress();
                UpdateSlotProgress(1, 5, "Luồng 1: Tạo ảnh");
                var result = await _videoProcessingService.GenerateMascotChannelVideoAsync(
                    mascotImagePath,
                    channelTheme,
                    identityPack,
                    GetSelectedMascotSceneCount(),
                    settings,
                    Log,
                    CancellationToken.None,
                    (percent, stage) => UpdateSinglePipelineProgress(percent, stage));

                Log("Mascot Story Pipeline: hoàn tất.");
                foreach (var scene in result.Scenes)
                {
                    Log($"Scene {scene.Index}: {scene.SceneVideoPath}");
                }
                Log("Mascot Story Pipeline final -> " + result.FinalVideoPath);
            }
            catch (Exception ex)
            {
                Log("Mascot Story Pipeline thất bại: " + ex.Message);
            }
            finally
            {
                btnRunMascotChannelPipeline.Enabled = true;
                btnRunAffiliateDeepVideo.Enabled = true;
                btnRenderAiVideo.Enabled = true;
                btnGenerateGeminiPrompt.Enabled = true;
                btnReviewScriptBeforeRender.Enabled = true;
                btnSelectAvatarIdentityPack.Enabled = true;
                btnPreviewMascotVariants.Enabled = true;
            }
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
                var identityPack = LoadAvatarIdentityPackForProfile(cbRunningProfile?.SelectedItem?.ToString());
                if (identityPack.Count < 3 || identityPack.Count > 5)
                {
                    Log("Preview biến thể: Bộ nhận diện chưa hợp lệ. Hãy chọn 3-5 ảnh.");
                    return;
                }

                Log("Preview biến thể: đang tạo 4 ảnh tham chiếu trước khi chạy Veo...");
                var preview = await _videoProcessingService.GenerateMascotVariantPreviewAsync(
                    mascotImagePath,
                    channelTheme,
                    identityPack,
                    GetSelectedMascotSceneCount(),
                    settings,
                    Log,
                    CancellationToken.None);
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

        private async void btnRunPhilosophyVideo_Click(object sender, EventArgs e)
        {
            var input = txtPhilosophyInput?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                Log("Philosophy module: nhập quote hoặc link bài viết.");
                return;
            }

            btnRunPhilosophyVideo.Enabled = false;
            try
            {
                Log("Philosophy module: generating video from quote/link...");
                var outputPath = await _philosophyVideoService.GenerateAsync(input, Log, CancellationToken.None);
                Log("Philosophy module: done -> " + outputPath);
            }
            catch (Exception ex)
            {
                Log("Philosophy module failed: " + ex.Message);
            }
            finally
            {
                btnRunPhilosophyVideo.Enabled = true;
            }
        }

        private void tabAiVideoGenModes_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyAiVideoGenModeUiVisibility();
            AttachAiRenderProgressPanelToSelectedModeTab();
        }

        private void AttachAiRenderProgressPanelToSelectedModeTab()
        {
            if (pnlAiRenderProgress == null || tabAiVideoGenModes?.SelectedTab == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(AttachAiRenderProgressPanelToSelectedModeTab));
                return;
            }

            const int progLabelLeft = 8;
            const int progLabelWidth = 200;
            const int progBarMaxWidth = 260;

            var page = tabAiVideoGenModes.SelectedTab;
            pnlAiRenderProgress.SuspendLayout();
            pnlAiRenderProgress.Parent = page;
            pnlAiRenderProgress.Dock = DockStyle.Bottom;
            pnlAiRenderProgress.Height = 68;
            pnlAiRenderProgress.SendToBack();
            pnlAiRenderProgress.ResumeLayout();

            var hdr = pnlAiRenderProgress.Controls.Find("lblAiRenderProgressHeader", false);
            if (hdr != null && hdr.Length > 0)
            {
                hdr[0].Width = Math.Max(80, Math.Min(560, pnlAiRenderProgress.ClientSize.Width - 16));
            }

            var barW = Math.Min(progBarMaxWidth, Math.Max(96, pnlAiRenderProgress.ClientSize.Width - progLabelLeft - progLabelWidth - 18));
            if (pbAiRenderSlot1 != null)
            {
                pbAiRenderSlot1.Width = barW;
            }

            if (pbAiRenderSlot2 != null)
            {
                pbAiRenderSlot2.Width = barW;
            }

            if (pbAiRenderSlot3 != null)
            {
                pbAiRenderSlot3.Width = barW;
            }
        }

        private void ApplyAiVideoGenModeUiVisibility()
        {
            if (tabAiVideoGenModes == null)
            {
                return;
            }

            var idx = tabAiVideoGenModes.SelectedIndex;
            var showProductGrid = idx == 0 || idx == 1;

            if (lblAiVideoGenProductsTitle != null)
            {
                lblAiVideoGenProductsTitle.Visible = showProductGrid;
            }

            if (dgvAiVideoGenInput != null)
            {
                dgvAiVideoGenInput.Visible = showProductGrid;
            }

            if (lblAiVideoGenProductBlockHint != null)
            {
                if (showProductGrid)
                {
                    lblAiVideoGenProductBlockHint.Visible = false;
                }
                else if (idx == 4)
                {
                    lblAiVideoGenProductBlockHint.Visible = true;
                    lblAiVideoGenProductBlockHint.Text =
                        "Video reup: ô «URL video» + bảng. Có link http(s) → rời ô hoặc nhập từ Affiliate → tải nguồn. Bước: hook Gemini → Lyria → nhạc → Render. Xóa dòng: Delete. SRT sau render. Slideshow phía trên không dùng cho reup.";
                }
                else
                {
                    lblAiVideoGenProductBlockHint.Visible = true;
                    lblAiVideoGenProductBlockHint.Text =
                        "Tab Mascot Story và Triết lý/Quote không dùng bảng sản phẩm slideshow. Chọn tab «Slideshow sản phẩm», «Affiliate chuyên sâu» hoặc «Video reup» khi cần dữ liệu từ Săn Affiliate.";
                }
            }

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

        private string EnsureAvatarVaultRootDirectory()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AvatarVault");
            Directory.CreateDirectory(path);
            return path;
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
            var profileDir = EnsureAvatarVaultProfileDirectory(profileName);
            var files = Directory.GetFiles(profileDir)
                .Where(x =>
                {
                    var ext = (Path.GetExtension(x) ?? string.Empty).ToLowerInvariant();
                    return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif";
                })
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            txtAvatarIdentityPack.Text = $"{profileDir} ({files.Count} ảnh)";
            return files;
        }

        private int GetSelectedMascotSceneCount()
        {
            var text = cbMascotSceneCount?.SelectedItem?.ToString() ?? "4";
            if (int.TryParse(text, out var value) && (value == 4 || value == 6 || value == 8))
            {
                return value;
            }

            return 4;
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

            var identityPack = LoadAvatarIdentityPackForProfile(cbRunningProfile?.SelectedItem?.ToString());
            if (identityPack.Count < 3 || identityPack.Count > 5)
            {
                Log("Regenerate scene: Bộ nhận diện chưa hợp lệ (cần 3-5 ảnh).");
                return;
            }

            miRegenerateScene.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync();
                var sceneScript = _mascotPreviewSceneScripts[idx];
                Log($"Regenerate scene {idx + 1}: đang gọi Gemini tạo lại ảnh + prompt...");
                var regen = await _videoProcessingService.RegenerateMascotSceneAsync(
                    mascotImagePath,
                    channelTheme,
                    identityPack,
                    sceneScript,
                    settings,
                    Log,
                    CancellationToken.None);

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
        }



        private void ToggleSecretVisibility(TextBox textBox, Button toggleButton)
        {
            textBox.UseSystemPasswordChar = !textBox.UseSystemPasswordChar;
            toggleButton.Text = textBox.UseSystemPasswordChar ? "Show" : "Hide";
        }

        private void HookSettingValidationEvents()
        {
            txtAiProvider.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtAiModel.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtVeoEndpoint.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtLyriaEndpoint.TextChanged += (sender, e) => ValidateSettingsInputs();
            txtFfmpegPath.TextChanged += (sender, e) => ValidateSettingsInputs();
        }

        private bool ValidateSettingsInputs()
        {
            if (string.IsNullOrWhiteSpace(txtAiProvider.Text))
            {
                lblSettingsValidation.Text = "AI Provider is required.";
                btnSaveSettings.Enabled = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtAiModel.Text))
            {
                lblSettingsValidation.Text = "AI Model is required.";
                btnSaveSettings.Enabled = false;
                return false;
            }

            if (!IsValidHttpUrl(txtVeoEndpoint.Text))
            {
                lblSettingsValidation.Text = "Veo Endpoint must be a valid http/https URL.";
                btnSaveSettings.Enabled = false;
                return false;
            }

            if (!IsValidHttpUrl(txtLyriaEndpoint.Text))
            {
                lblSettingsValidation.Text = "Lyria Endpoint must be a valid http/https URL.";
                btnSaveSettings.Enabled = false;
                return false;
            }

            var ffmpegPath = (txtFfmpegPath.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(ffmpegPath) &&
                (!File.Exists(ffmpegPath) || !ffmpegPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                lblSettingsValidation.Text = "FFmpeg Path must point to ffmpeg.exe or be empty.";
                btnSaveSettings.Enabled = false;
                return false;
            }

            lblSettingsValidation.Text = string.Empty;
            btnSaveSettings.Enabled = true;
            return true;
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

        private const int WatchSecondsUiMax = 10800; // 180 minutes — must match NumericUpDown maximum (minutes) × 60

        private static string FormatWatchRangeForQueue(int secMin, int secMax)
        {
            var a = Math.Round(secMin / 60.0m, 2);
            var b = Math.Round(secMax / 60.0m, 2);
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}-{1:0.##} m", a, b);
        }

        private static void ParseWatchRangeToSeconds(string watchRange, out int watchMin, out int watchMax)
        {
            watchMin = 7;
            watchMax = 18;
            if (string.IsNullOrWhiteSpace(watchRange))
            {
                return;
            }

            var wr = watchRange.Trim();

            if (wr.EndsWith(" m", StringComparison.OrdinalIgnoreCase))
            {
                var core = wr.Substring(0, wr.Length - 2).Trim();
                var parts = core.Split('-');
                if (parts.Length == 2 &&
                    decimal.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var minM) &&
                    decimal.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var maxM))
                {
                    watchMin = Math.Max(3, (int)Math.Round((double)minM * 60));
                    watchMax = Math.Max(watchMin, (int)Math.Round((double)maxM * 60));
                }

                return;
            }

            var legacy = wr;
            if (legacy.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                legacy = legacy.Substring(0, legacy.Length - 1);
            }

            var legacyParts = legacy.Split('-');
            if (legacyParts.Length == 2 &&
                int.TryParse(legacyParts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var smin) &&
                int.TryParse(legacyParts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var smax))
            {
                watchMin = Math.Max(3, smin);
                watchMax = Math.Max(watchMin, smax);
            }
        }

        private static int ReadWatchSecondsFromUi(NumericUpDown ctrl)
        {
            if (ctrl == null)
            {
                return 7;
            }

            return Math.Max(3, (int)Math.Round((double)ctrl.Value * 60));
        }

        private static int ReadWatchSecondsFromUiOrDefault(NumericUpDown ctrl, int defaultSeconds)
        {
            return ctrl == null ? defaultSeconds : ReadWatchSecondsFromUi(ctrl);
        }

        private static void ApplyWatchSecondsToUi(NumericUpDown ctrl, int seconds)
        {
            if (ctrl == null || seconds <= 0)
            {
                return;
            }

            var minutes = Math.Round(seconds / 60.0m, 2);
            ctrl.Value = minutes < ctrl.Minimum ? ctrl.Minimum : (minutes > ctrl.Maximum ? ctrl.Maximum : minutes);
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
                    TikTokUserId = (proxy.TikTokUserId ?? string.Empty).Trim()
                };

                if (string.IsNullOrWhiteSpace(item.Name) &&
                    string.IsNullOrWhiteSpace(item.ChromeUserDataPath) &&
                    string.IsNullOrWhiteSpace(item.ProxyHost) &&
                    item.ProxyPort == 0 &&
                    string.IsNullOrWhiteSpace(item.ProxyUser) &&
                    string.IsNullOrWhiteSpace(item.ProxyPass) &&
                    string.IsNullOrWhiteSpace(item.TikTokUniqueId) &&
                    string.IsNullOrWhiteSpace(item.TikTokNickname) &&
                    string.IsNullOrWhiteSpace(item.TikTokUserId))
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
                TikTokUserId = proxy.TikTokUserId
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
            SelectRunningProfileInUi(previous);
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
            SetStatusStripText(tab == null ? "tiktok_Omni" : $"Đang xem: {tab.Text}");
        }

        private void SetStatusStripText(string text)
        {
            if (tslStatusMain == null)
            {
                return;
            }

            void Apply()
            {
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

        private void TabMain_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tab = tabMain.TabPages[e.Index];
            var background = e.State.HasFlag(DrawItemState.Selected)
                ? Color.FromArgb(76, 110, 245)
                : Color.FromArgb(40, 44, 54);
            using (var brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            TextRenderer.DrawText(
                e.Graphics,
                tab.Text,
                Font,
                e.Bounds,
                Color.WhiteSmoke,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private async void btnStartWarmup_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                Log("Queue is running. Stop queue before manual warm-up.");
                return;
            }

            var state = BuildWarmupStateFromUi();

            await RunWarmupAsync(state, false);
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
            if (!TryGetSelectedProfileFromSettingsGrid(out var profileName)) return;

            var gridRow = dgvProxyProfiles.SelectedRows[0];
            var profile = gridRow.DataBoundItem as AutomationProfile;
            if (profile == null) return;

            // Đặt con trỏ chuột quay rõ ràng trên UI Thread
            this.Cursor = Cursors.WaitCursor;
            Log($"[LOGIN] Bắt đầu mở Chrome cho nick: {profileName}...");
            if (tslStatusMain != null) tslStatusMain.Text = $"Đang chờ bạn đăng nhập TikTok cho nick: {profileName}...";

            // Dùng CHUNG đường dẫn profile với Playwright warm-up
            // ⇒ Login ở đây xong vào tab «Làm ấm tài khoản» dùng được luôn, không bắt login lại.
            var sharedUserDataDir = BrowserAutomation.GetSharedProfilePath(profile, profileName);
            BrowserAutomation.EnsureLegacySessionMigratedForSharedProfile(profile, profileName, Log);
            Log($"[LOGIN] user-data-dir: {sharedUserDataDir}");

            await Task.Run(() =>
            {
                ChromeOptions options = new ChromeOptions();
                options.AddArgument($"--user-data-dir={sharedUserDataDir}");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");

                if (!string.IsNullOrWhiteSpace(profile.ProxyHost) && profile.ProxyPort > 0)
                {
                    options.AddArgument($"--proxy-server={profile.ProxyHost}:{profile.ProxyPort}");
                }

                using (IWebDriver driver = new ChromeDriver(options))
                {
                    driver.Navigate().GoToUrl("https://www.tiktok.com/login");
                    bool isLoggedIn = false;

                    // Vòng lặp chờ đăng nhập (Check Cookie sessionid)
                    while (true)
                    {
                        try
                        {
                            var title = driver.Title; // Kích hoạt Exception nếu user tắt Chrome
                            var sessionCookie = driver.Manage().Cookies.GetCookieNamed("sessionid");
                            if (sessionCookie != null)
                            {
                                isLoggedIn = true;
                                break;
                            }
                            Thread.Sleep(2000);
                        }
                        catch
                        {
                            break; // Trình duyệt bị đóng thủ công
                        }
                    }

                    if (isLoggedIn)
                    {
                        this.Invoke(new Action(() => Log("[LOGIN] Đăng nhập thành công! Đang chuyển hướng lấy thông tin...")));

                        try
                        {
                            // Truy cập /profile, TikTok sẽ tự động redirect về /@username của bạn
                            driver.Navigate().GoToUrl("https://www.tiktok.com/profile");

                            // Chờ quá trình redirect hoàn tất (khi URL xuất hiện chữ /@)
                            for (int i = 0; i < 15; i++)
                            {
                                if (driver.Url.Contains("/@")) break;
                                Thread.Sleep(1000);
                            }

                            // Lấy Username từ URL (Tuyệt đối chính xác)
                            string currentUrl = driver.Url;
                            if (currentUrl.Contains("/@"))
                            {
                                string uid = currentUrl.Substring(currentUrl.IndexOf("/@") + 2).Split('?')[0].Split('/')[0];
                                profile.TikTokUniqueId = uid;
                            }

                            // Lấy Tên hiển thị từ tiêu đề kênh
                            Thread.Sleep(2000);
                            string jsCode = @"
                        try {
                            let nameEl = document.querySelector('h1[data-e2e=""user-title""]');
                            return nameEl ? nameEl.innerText : '';
                        } catch(e) { return ''; }
                    ";
                            var js = (IJavaScriptExecutor)driver;
                            string nickName = js.ExecuteScript(jsCode)?.ToString() ?? "";

                            profile.TikTokNickname = string.IsNullOrWhiteSpace(nickName) ? profile.TikTokUniqueId : nickName;
                            if (string.IsNullOrWhiteSpace(profile.TikTokUserId)) profile.TikTokUserId = "ID_" + DateTime.Now.Ticks;
                        }
                        catch { /* Bỏ qua lỗi ngầm nếu mạng chập chờn */ }
                    }

                    driver.Quit(); // Lấy xong TỰ TẮT CHROME
                }
            });

            // DÙNG INVOKE ĐỂ XỬ LÝ LẠI GIAO DIỆN TRÊN LUỒNG CHÍNH
            this.Invoke(new Action(async () =>
            {
                if (_proxyProfileBindingList != null) _proxyProfileBindingList.ResetItem(gridRow.Index);
                dgvProxyProfiles.Refresh();
                await SaveProfilesFromGridAsync();

                Log($"[LOGIN] Hoàn tất xử lý nick: {profileName}");
                if (tslStatusMain != null) tslStatusMain.Text = $"Đã xong nick: {profileName}";

                // TRẢ LẠI CON TRỎ CHUỘT BÌNH THƯỜNG
                this.Cursor = Cursors.Default;
                UseWaitCursor = false;

                // Tự động nhảy xuống dòng tiếp theo
                if (gridRow.Index + 1 < dgvProxyProfiles.Rows.Count)
                {
                    dgvProxyProfiles.ClearSelection();
                    dgvProxyProfiles.Rows[gridRow.Index + 1].Selected = true;
                }
            }));
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
                SelectRunningProfileInUi(profile.Name.Trim());
            }
        }

        private void btnQueueWarmup_Click(object sender, EventArgs e)
        {
            var state = BuildWarmupStateFromUi();
            const int defaultRetries = 2;
            _warmupQueueScheduler.Enqueue(state, maxRetries: defaultRetries);
            _warmupQueueBindingList.Add(new WarmupQueueUiItem
            {
                Profile = state.RunningProfileName,
                Keywords = state.Keywords,
                Videos = state.VideoCount,
                WatchRange = FormatWatchRangeForQueue(state.WatchSecondsMin, state.WatchSecondsMax),
                AutoComment = state.AutoComment,
                DryRun = state.DryRun,
                Status = "Pending",
                RetryCount = 0,
                MaxRetries = defaultRetries,
                LastError = string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                NextRetryAtUtc = null
            });
            RefreshWarmupQueueStatus();
            Log($"[QUEUE] Added warm-up job for profile '{state.RunningProfileName}'.");
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private async void btnStartWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                return;
            }

            if (_warmupQueueScheduler.QueueCount == 0)
            {
                Log("[QUEUE] No jobs to run.");
                return;
            }
            SyncSchedulerFromUi();

            _isWarmupQueueRunning = true;
            _pauseNowRequested = false;
            btnStartWarmupQueue.Enabled = false;
            btnStopWarmupQueue.Enabled = true;
            btnPauseWarmupQueue.Enabled = !_isWarmupQueuePaused;
            btnResumeWarmupQueue.Enabled = _isWarmupQueuePaused;
            btnPauseNowWarmupQueue.Enabled = true;
            btnStartWarmup.Enabled = false;
            btnQueueWarmup.Enabled = false;
            btnResumeWarmup.Enabled = false;
            _warmupQueueCancellation?.Dispose();
            _warmupQueueCancellation = new CancellationTokenSource();
            RefreshWarmupQueueStatus();

            try
            {
                await _warmupQueueScheduler.RunAsync(
                    RunWarmupQueueJobAsync,
                    Log,
                    _warmupQueueCancellation.Token,
                    HandleQueueEvent,
                    _ => SaveWarmupQueueSnapshotAsync());
                Log("[QUEUE] All jobs processed.");
            }
            catch (OperationCanceledException)
            {
                Log("[QUEUE] Queue stopped by user.");
            }
            finally
            {
                _isWarmupQueueRunning = false;
                _isWarmupQueuePaused = false;
                _pauseNowRequested = false;
                btnStartWarmupQueue.Enabled = true;
                btnStopWarmupQueue.Enabled = false;
                btnPauseWarmupQueue.Enabled = false;
                btnResumeWarmupQueue.Enabled = false;
                btnPauseNowWarmupQueue.Enabled = false;
                btnStartWarmup.Enabled = true;
                btnQueueWarmup.Enabled = true;
                _warmupQueueCancellation?.Dispose();
                _warmupQueueCancellation = null;
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = null;
                await _warmupQueueStateManager.SavePausedFlagAsync(false);
                await RefreshResumeStateAsync();
                RefreshWarmupQueueStatus();
                _ = SaveWarmupQueueSnapshotAsync();
            }
        }

        private void btnStopWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_warmupQueueCancellation == null)
            {
                return;
            }

            btnStopWarmupQueue.Enabled = false;
            _warmupQueueCancellation.Cancel();
            Log("[QUEUE] Stopping queue...");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
        }

        private void btnPauseWarmupQueue_Click(object sender, EventArgs e)
        {
            if (!_isWarmupQueueRunning || _isWarmupQueuePaused)
            {
                return;
            }

            _isWarmupQueuePaused = true;
            btnPauseWarmupQueue.Enabled = false;
            btnResumeWarmupQueue.Enabled = true;
            RefreshWarmupQueueStatus();
            Log("[QUEUE] Pause requested. Queue will pause after current job.");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(true);
        }

        private void btnResumeWarmupQueue_Click(object sender, EventArgs e)
        {
            if (!_isWarmupQueueRunning || !_isWarmupQueuePaused)
            {
                return;
            }

            _isWarmupQueuePaused = false;
            btnPauseWarmupQueue.Enabled = true;
            btnResumeWarmupQueue.Enabled = false;
            RefreshWarmupQueueStatus();
            Log("[QUEUE] Queue resumed.");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
        }

        private void btnPauseNowWarmupQueue_Click(object sender, EventArgs e)
        {
            if (!_isWarmupQueueRunning)
            {
                return;
            }

            _pauseNowRequested = true;
            _isWarmupQueuePaused = true;
            btnPauseWarmupQueue.Enabled = false;
            btnResumeWarmupQueue.Enabled = true;
            btnPauseNowWarmupQueue.Enabled = false;
            _currentQueueJobCancellation?.Cancel();
            RefreshWarmupQueueStatus();
            Log("[QUEUE] Pause-now requested. Current job will be re-queued from latest progress.");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(true);
        }

        private void btnRemoveQueueJob_Click(object sender, EventArgs e)
        {
            if (dgvWarmupQueue?.SelectedRows == null || dgvWarmupQueue.SelectedRows.Count == 0)
            {
                Log("[QUEUE] Please select a queue job to remove.");
                return;
            }

            if (_isWarmupQueueRunning)
            {
                Log("[QUEUE] Stop queue before removing jobs.");
                return;
            }

            var selected = dgvWarmupQueue.SelectedRows[0]?.DataBoundItem as WarmupQueueUiItem;
            if (selected == null)
            {
                return;
            }

            var state = BuildStateFromQueueRow(selected);
            var removed = _warmupQueueScheduler.RemoveFirstMatching(state);
            if (removed)
            {
                _warmupQueueBindingList.Remove(selected);
                SyncSchedulerFromUi();
                RefreshWarmupQueueStatus();
                Log($"[QUEUE] Removed job for profile '{selected.Profile}'.");
                _ = SaveWarmupQueueSnapshotAsync();
            }
        }

        private void btnClearWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                Log("[QUEUE] Stop queue before clearing.");
                return;
            }

            _warmupQueueScheduler.ClearPending();
            _warmupQueueBindingList?.Clear();
            RefreshWarmupQueueStatus();
            Log("[QUEUE] Cleared all pending jobs.");
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private void btnMoveQueueJobUp_Click(object sender, EventArgs e)
        {
            MoveSelectedQueueRow(-1);
        }

        private void btnMoveQueueJobDown_Click(object sender, EventArgs e)
        {
            MoveSelectedQueueRow(1);
        }

        private async void btnResumeWarmup_Click(object sender, EventArgs e)
        {
            var state = await _warmupStateManager.LoadAsync();
            if (state == null)
            {
                btnResumeWarmup.Enabled = false;
                Log("No resumable warm-up state found.");
                return;
            }

            ApplyWarmupStateToUi(state);
            await RunWarmupAsync(state, true);
        }

        private async Task RunWarmupAsync(WarmupRunState state, bool isResume)
        {
            btnStartWarmup.Enabled = false;
            btnResumeWarmup.Enabled = false;
            btnStopWarmup.Enabled = true;
            _warmupCancellation?.Dispose();
            _warmupCancellation = new CancellationTokenSource();
            _currentWarmupState = state;
            UpdateWarmupProgress(state.CompletedCount, state.VideoCount);
            await _warmupStateManager.SaveAsync(state);

            try
            {
                Log(isResume
                    ? $"Resuming warm-up from {state.CompletedCount}/{state.VideoCount}..."
                    : "Warm-up started...");
                await _tikTokAutomation.StartWarmupAsync(
                    state.Keywords,
                    state.VideoCount,
                    state.AutoComment,
                    state.DryRun,
                    state.CompletedCount,
                    state.WatchSecondsMin <= 0 ? ReadWatchSecondsFromUi(numWatchMin) : state.WatchSecondsMin,
                    state.WatchSecondsMax <= 0 ? ReadWatchSecondsFromUi(numWatchMax) : state.WatchSecondsMax,
                    _warmupCancellation.Token,
                    Log,
                    HandleWarmupProgressUpdate,
                    state.RunningProfileName);
                Log("Warm-up completed.");
                await _warmupStateManager.ClearAsync();
                _currentWarmupState = null;
            }
            catch (OperationCanceledException)
            {
                Log("Warm-up stopped by user.");
            }
            catch (Exception ex)
            {
                Log("Warm-up failed: " + ex.Message);
            }
            finally
            {
                btnStartWarmup.Enabled = true;
                btnStopWarmup.Enabled = false;
                _warmupCancellation?.Dispose();
                _warmupCancellation = null;
                await RefreshResumeStateAsync();
            }
        }

        private void btnStopWarmup_Click(object sender, EventArgs e)
        {
            if (_warmupCancellation == null)
            {
                return;
            }

            btnStopWarmup.Enabled = false;
            _warmupCancellation.Cancel();
            Log("Stopping warm-up...");
        }

        private async Task RunWarmupQueueJobAsync(WarmupRunState state, CancellationToken cancellationToken)
        {
            if (state == null)
            {
                return;
            }

            await WaitIfQueuePausedAsync(cancellationToken);

            _currentWarmupState = state;
            UpdateWarmupProgress(0, state.VideoCount);
            await _warmupStateManager.SaveAsync(state);
            RefreshWarmupQueueStatus();

            try
            {
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await _tikTokAutomation.StartWarmupAsync(
                    state.Keywords,
                    state.VideoCount,
                    state.AutoComment,
                    state.DryRun,
                    state.CompletedCount,
                    state.WatchSecondsMin <= 0 ? 7 : state.WatchSecondsMin,
                    state.WatchSecondsMax <= 0 ? Math.Max(7, state.WatchSecondsMin) : state.WatchSecondsMax,
                    _currentQueueJobCancellation.Token,
                    Log,
                    HandleWarmupProgressUpdate,
                    state.RunningProfileName);
                await _warmupStateManager.ClearAsync();
            }
            catch (OperationCanceledException) when (_pauseNowRequested)
            {
                _pauseNowRequested = false;
                if (state.CompletedCount < state.VideoCount)
                {
                    var resumeState = CloneWarmupState(state);
                    _warmupQueueScheduler.EnqueueFront(resumeState, 2);
                    _warmupQueueBindingList?.Insert(0, new WarmupQueueUiItem
                    {
                        Profile = resumeState.RunningProfileName,
                        Keywords = resumeState.Keywords,
                        Videos = resumeState.VideoCount,
                        WatchRange = FormatWatchRangeForQueue(resumeState.WatchSecondsMin, resumeState.WatchSecondsMax),
                        AutoComment = resumeState.AutoComment,
                        DryRun = resumeState.DryRun,
                        Status = "Pending",
                        RetryCount = 0,
                        MaxRetries = 2,
                        LastError = string.Empty,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    _ = SaveWarmupQueueSnapshotAsync();
                }

                throw new WarmupQueueRequeueException("Paused now and re-queued.");
            }
            finally
            {
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = null;
                _currentWarmupState = null;
                RefreshWarmupQueueStatus();
            }
        }

        private async Task WaitIfQueuePausedAsync(CancellationToken cancellationToken)
        {
            while (_isWarmupQueuePaused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken);
            }
        }

        private void HandleQueueEvent(WarmupQueueRunEvent eventInfo)
        {
            if (eventInfo == null || _warmupQueueBindingList == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<WarmupQueueRunEvent>(HandleQueueEvent), eventInfo);
                return;
            }

            var row = FindQueueRow(eventInfo.State);
            if (row == null)
            {
                return;
            }

            if (eventInfo.EventType == WarmupQueueEventType.Started)
            {
                row.Status = "Running";
            }
            else if (eventInfo.EventType == WarmupQueueEventType.Retrying)
            {
                row.Status = $"Retry {eventInfo.Attempt}/{eventInfo.MaxRetries + 1}";
                row.RetryCount = Math.Max(row.RetryCount, eventInfo.Attempt);
                row.LastError = eventInfo.ErrorMessage ?? string.Empty;
                row.NextRetryAtUtc = eventInfo.NextRetryAtUtc ?? DateTime.UtcNow.AddMinutes(5);
            }
            else if (eventInfo.EventType == WarmupQueueEventType.Completed ||
                     eventInfo.EventType == WarmupQueueEventType.FailedPermanent ||
                     eventInfo.EventType == WarmupQueueEventType.Skipped)
            {
                if (eventInfo.EventType == WarmupQueueEventType.FailedPermanent ||
                    eventInfo.EventType == WarmupQueueEventType.Skipped)
                {
                    row.LastError = eventInfo.ErrorMessage ?? string.Empty;
                }
                if (eventInfo.EventType == WarmupQueueEventType.Skipped)
                {
                    row.Status = "Skipped";
                }
                row.NextRetryAtUtc = null;
                _ = AppendQueueHistoryAsync(eventInfo, row);
                _warmupQueueBindingList.Remove(row);
                _ = SaveWarmupQueueSnapshotAsync();
            }

            dgvWarmupQueue?.Refresh();
            RefreshWarmupQueueStatus();
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            await LoadSettingsIntoUiAsync();
            await RefreshResumeStateAsync();
            await LoadWarmupQueueAsync();
            _warmupQueueHistory = await _warmupQueueHistoryManager.LoadAsync();
            _approvalQueueItems = await _approvalQueueManager.LoadAsync();
            RefreshQueueStatsSummary();
            var paused = await _warmupQueueStateManager.LoadPausedFlagAsync();
            _isWarmupQueuePaused = paused && (_warmupQueueScheduler?.QueueCount ?? 0) > 0;
            RefreshWarmupQueueStatus();
            SetStatusStripText(tabMain?.SelectedTab == null ? "tiktok_Omni" : $"Đang xem: {tabMain.SelectedTab.Text}");
            if (_isWarmupQueuePaused)
            {
                var autoResumeEnabled = chkAutoResumeQueueOnStartup?.Checked ?? true;
                if (autoResumeEnabled)
                {
                    _isWarmupQueuePaused = false;
                    await _warmupQueueStateManager.SavePausedFlagAsync(false);
                    RefreshWarmupQueueStatus();
                    Log("[QUEUE] Auto-resume enabled. Restarting paused queue...");
                    btnStartWarmupQueue_Click(this, EventArgs.Empty);
                }
                else
                {
                    Log("[QUEUE] Previous session ended in paused state. Press Start Queue then Resume Queue to continue.");
                }
            }
        }

        private async Task LoadSettingsIntoUiAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync();
                txtAiProvider.Text = settings.AiProvider;
                txtAiModel.Text = settings.AiModel;
                txtAiApiKey.Text = settings.AiApiKey;
                txtTwoCaptchaApiKey.Text = settings.TwoCaptchaApiKey;
                txtVeoApiKey.Text = settings.VeoApiKey;
                txtLyriaApiKey.Text = settings.LyriaApiKey;
                txtVeoEndpoint.Text = settings.VeoEndpoint;
                txtLyriaEndpoint.Text = settings.LyriaEndpoint;
                txtFfmpegPath.Text = settings.FfmpegPath;
                if (txtYtDlpPath != null) txtYtDlpPath.Text = settings.YtDlpPath ?? string.Empty;
                chkAutoResumeQueueOnStartup.Checked = settings.AutoResumeQueueOnStartup ?? true;
                chkAlwaysRequirePrePostApproval.Checked = settings.AlwaysRequirePrePostApproval ?? true;
                chkAlwaysRequirePreRenderApproval.Checked = settings.AlwaysRequirePreRenderApproval ?? false;
                chkAutoRunApprovedQueue.Checked = settings.AutoRunApprovedQueue ?? false;
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

                UpdateAffiliateRankControlsEnabledState();
                numBlockPostingSafetyScoreBelow.Value = ClampNumericValue(settings.BlockPostingSafetyScoreBelow, numBlockPostingSafetyScoreBelow);
                numWatchMin.Value = ClampNumericValue(Math.Round(settings.WatchSecondsMin / 60.0, 2), numWatchMin);
                numWatchMax.Value = ClampNumericValue(Math.Round(settings.WatchSecondsMax / 60.0, 2), numWatchMax);
                numAiTransitionDuration.Value = ClampNumericValue(settings.VideoTransitionDurationSeconds, numAiTransitionDuration);
                numAiTextSize.Value = ClampNumericValue(settings.VideoTextSize, numAiTextSize);
                numAiMusicVolume.Value = ClampNumericValue(settings.VideoMusicVolume, numAiMusicVolume);
                if (numWatchMax.Value < numWatchMin.Value)
                {
                    numWatchMax.Value = numWatchMin.Value;
                }
                PopulateProfileBindingListFromSettings(settings);
                RefreshRunningProfileOptions(settings);
                RefreshAutoPostVideoCombo();
                ValidateSettingsInputs();
                Log("Settings loaded.");
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
                settings.VeoApiKey = txtVeoApiKey.Text.Trim();
                settings.LyriaApiKey = txtLyriaApiKey.Text.Trim();
                settings.VeoEndpoint = txtVeoEndpoint.Text.Trim();
                settings.LyriaEndpoint = txtLyriaEndpoint.Text.Trim();
                settings.FfmpegPath = txtFfmpegPath.Text.Trim();
                settings.YtDlpPath = (txtYtDlpPath?.Text ?? string.Empty).Trim();
                settings.AutoResumeQueueOnStartup = chkAutoResumeQueueOnStartup?.Checked ?? true;
                settings.AlwaysRequirePrePostApproval = chkAlwaysRequirePrePostApproval?.Checked ?? true;
                settings.AlwaysRequirePreRenderApproval = chkAlwaysRequirePreRenderApproval?.Checked ?? false;
                settings.AutoRunApprovedQueue = chkAutoRunApprovedQueue?.Checked ?? false;
                settings.BlockPostingSafetyScoreBelow = (int)(numBlockPostingSafetyScoreBelow?.Value ?? 75);
                var wMin = ReadWatchSecondsFromUi(numWatchMin);
                var wMax = ReadWatchSecondsFromUi(numWatchMax);
                settings.WatchSecondsMin = Math.Min(WatchSecondsUiMax, wMin);
                settings.WatchSecondsMax = Math.Min(WatchSecondsUiMax, Math.Max(settings.WatchSecondsMin, wMax));
                settings.VideoTransitionDurationSeconds = numAiTransitionDuration == null ? settings.VideoTransitionDurationSeconds : (double)numAiTransitionDuration.Value;
                settings.VideoTextSize = numAiTextSize == null ? settings.VideoTextSize : (int)numAiTextSize.Value;
                settings.VideoMusicVolume = numAiMusicVolume == null ? settings.VideoMusicVolume : (int)numAiMusicVolume.Value;
                settings.Profiles = BuildProxyProfilesFromGrid();

                await _configManager.SaveAsync(settings);
                RefreshRunningProfileOptions(settings);
                Log("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Log("Failed to save settings: " + ex.Message);
            }
            finally
            {
                btnSaveSettings.Enabled = true;
            }
        }

        private async void btnTestAi_Click(object sender, EventArgs e)
        {
            btnTestAi.Enabled = false;
            try
            {
                if (string.IsNullOrWhiteSpace(txtAiApiKey.Text))
                {
                    Log("AI test failed: AI API key is empty.");
                    return;
                }

                var geminiService = new GeminiService();
                var text = await geminiService.GenerateScriptAsync(
                    "Return a short text: OK",
                    txtAiProvider.Text.Trim(),
                    txtAiApiKey.Text.Trim(),
                    txtAiModel.Text.Trim());

                Log(string.IsNullOrWhiteSpace(text)
                    ? "AI test warning: request succeeded but response is empty."
                    : "AI test passed.");
            }
            catch (Exception ex)
            {
                Log("AI test failed: " + ex.Message);
            }
            finally
            {
                btnTestAi.Enabled = true;
            }
        }

        private async void btnTestTwoCaptcha_Click(object sender, EventArgs e)
        {
            btnTestTwoCaptcha.Enabled = false;
            try
            {
                if (string.IsNullOrWhiteSpace(txtTwoCaptchaApiKey.Text))
                {
                    Log("2Captcha test failed: API key is empty.");
                    return;
                }

                var service = new CaptchaService();
                var balance = await service.GetBalanceAsync(txtTwoCaptchaApiKey.Text.Trim(), CancellationToken.None);
                Log($"2Captcha test passed. Balance: {balance:0.####} USD");
            }
            catch (Exception ex)
            {
                Log("2Captcha test failed: " + ex.Message);
            }
            finally
            {
                btnTestTwoCaptcha.Enabled = true;
            }
        }

        private async void btnTestVeo_Click(object sender, EventArgs e)
        {
            btnTestVeo.Enabled = false;
            try
            {
                var videoService = new VideoService();
                var url = await videoService.GenerateVideoAsync(
                    "Generate a 2-second test clip.",
                    txtVeoApiKey.Text.Trim(),
                    txtVeoEndpoint.Text.Trim());

                Log(string.IsNullOrWhiteSpace(url)
                    ? "Veo test warning: request succeeded but no video URL returned."
                    : "Veo test passed.");
            }
            catch (Exception ex)
            {
                Log("Veo test failed: " + ex.Message);
            }
            finally
            {
                btnTestVeo.Enabled = true;
            }
        }

        private async void btnTestLyria_Click(object sender, EventArgs e)
        {
            btnTestLyria.Enabled = false;
            try
            {
                var videoService = new VideoService();
                var url = await videoService.GenerateAudioAsync(
                    "This is a short connection test.",
                    txtLyriaApiKey.Text.Trim(),
                    txtLyriaEndpoint.Text.Trim());

                Log(string.IsNullOrWhiteSpace(url)
                    ? "Lyria test warning: request succeeded but no audio URL returned."
                    : "Lyria test passed.");
            }
            catch (Exception ex)
            {
                Log("Lyria test failed: " + ex.Message);
            }
            finally
            {
                btnTestLyria.Enabled = true;
            }
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
            if (rtbLogs.IsDisposed)
            {
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

        private void UpdateWarmupProgress(int completed, int total)
        {
            if (pbWarmupProgress.IsDisposed || lblWarmupProgress.IsDisposed)
            {
                return;
            }

            if (pbWarmupProgress.InvokeRequired || lblWarmupProgress.InvokeRequired)
            {
                pbWarmupProgress.Invoke(new Action<int, int>(UpdateWarmupProgress), completed, total);
                return;
            }

            if (total <= 0)
            {
                pbWarmupProgress.Value = 0;
                lblWarmupProgress.Text = "Tiến độ: 0/0 (0%)";
                return;
            }

            var safeCompleted = Math.Max(0, Math.Min(completed, total));
            var percent = (int)Math.Round((double)safeCompleted * 100 / total);
            pbWarmupProgress.Value = Math.Max(0, Math.Min(percent, 100));
            lblWarmupProgress.Text = $"Tiến độ: {safeCompleted}/{total} ({percent}%)";
        }

        private async void HandleWarmupProgressUpdate(int completed, int total)
        {
            UpdateWarmupProgress(completed, total);

            if (_currentWarmupState == null)
            {
                return;
            }

            _currentWarmupState.CompletedCount = completed;
            _currentWarmupState.VideoCount = total;
            _currentWarmupState.LastUpdatedUtc = DateTime.UtcNow;
            await _warmupStateManager.SaveAsync(_currentWarmupState);
        }

        private async Task RefreshResumeStateAsync()
        {
            var state = await _warmupStateManager.LoadAsync();
            btnResumeWarmup.Enabled = state != null && state.CompletedCount < state.VideoCount;
        }

        private void ApplyWarmupStateToUi(WarmupRunState state)
        {
            txtKeywords.Text = state.Keywords;
            SelectRunningProfileInUi(state.RunningProfileName);
            numVideoCount.Value = Math.Max(numVideoCount.Minimum, Math.Min(numVideoCount.Maximum, state.VideoCount));
            if (state.WatchSecondsMin > 0)
            {
                ApplyWatchSecondsToUi(numWatchMin, state.WatchSecondsMin);
            }

            if (state.WatchSecondsMax > 0)
            {
                ApplyWatchSecondsToUi(numWatchMax, state.WatchSecondsMax);
            }
            chkAutoComment.Checked = state.AutoComment;
            rbDryRun.Checked = state.DryRun;
            rbLiveRun.Checked = !state.DryRun;
            UpdateWarmupProgress(state.CompletedCount, state.VideoCount);
        }

        private WarmupRunState BuildWarmupStateFromUi()
        {
            return new WarmupRunState
            {
                Keywords = txtKeywords.Text.Trim(),
                RunningProfileName = cbRunningProfile?.SelectedItem?.ToString() ?? "default",
                VideoCount = (int)numVideoCount.Value,
                WatchSecondsMin = ReadWatchSecondsFromUi(numWatchMin),
                WatchSecondsMax = Math.Max(ReadWatchSecondsFromUi(numWatchMin), ReadWatchSecondsFromUi(numWatchMax)),
                AutoComment = chkAutoComment.Checked,
                DryRun = rbDryRun.Checked,
                CompletedCount = 0,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }

        private void RefreshWarmupQueueStatus()
        {
            if (lblWarmupQueueStatus == null || lblWarmupQueueStatus.IsDisposed)
            {
                return;
            }

            if (lblWarmupQueueStatus.InvokeRequired)
            {
                lblWarmupQueueStatus.Invoke(new Action(RefreshWarmupQueueStatus));
                return;
            }

            var queueCount = _warmupQueueScheduler?.QueueCount ?? 0;
            var runningText = _isWarmupQueueRunning ? (_isWarmupQueuePaused ? "paused" : "running") : "idle";
            lblWarmupQueueStatus.Text = $"Queue: {queueCount} job(s) - {runningText}";
        }

        private async Task LoadWarmupQueueAsync()
        {
            try
            {
                var snapshots = await _warmupQueueStateManager.LoadAsync();
                _warmupQueueScheduler.ReplacePending(snapshots);
                _warmupQueueBindingList?.Clear();
                foreach (var snapshot in snapshots)
                {
                    if (snapshot?.State == null)
                    {
                        continue;
                    }

                    _warmupQueueBindingList.Add(new WarmupQueueUiItem
                    {
                        Profile = snapshot.State.RunningProfileName,
                        Keywords = snapshot.State.Keywords,
                        Videos = snapshot.State.VideoCount,
                        WatchRange = FormatWatchRangeForQueue(snapshot.State.WatchSecondsMin, snapshot.State.WatchSecondsMax),
                        AutoComment = snapshot.State.AutoComment,
                        DryRun = snapshot.State.DryRun,
                        Status = "Pending",
                        RetryCount = snapshot.RetryCount,
                        MaxRetries = snapshot.MaxRetries,
                        LastError = snapshot.LastError ?? string.Empty,
                        CreatedAtUtc = snapshot.CreatedAtUtc == default(DateTime) ? DateTime.UtcNow : snapshot.CreatedAtUtc,
                        NextRetryAtUtc = snapshot.NextRetryAtUtc
                    });
                }
                RefreshWarmupQueueStatus();
            }
            catch (Exception ex)
            {
                Log("[QUEUE] Failed to load queue state: " + ex.Message);
            }
        }

        private async Task SaveWarmupQueueSnapshotAsync()
        {
            try
            {
                var snapshots = BuildSnapshotsFromUi();
                await _warmupQueueStateManager.SaveAsync(snapshots);
            }
            catch (Exception ex)
            {
                Log("[QUEUE] Failed to persist queue state: " + ex.Message);
            }
        }

        private void SyncSchedulerFromUi()
        {
            var snapshots = BuildSnapshotsFromUi();
            _warmupQueueScheduler.ReplacePending(snapshots);
            RefreshWarmupQueueStatus();
        }

        private List<WarmupQueueSnapshotItem> BuildSnapshotsFromUi()
        {
            var snapshots = new List<WarmupQueueSnapshotItem>();
            if (_warmupQueueBindingList == null)
            {
                return snapshots;
            }

            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                var row = _warmupQueueBindingList[i];
                if (row == null)
                {
                    continue;
                }

                snapshots.Add(new WarmupQueueSnapshotItem
                {
                    State = BuildStateFromQueueRow(row),
                    MaxRetries = row.MaxRetries < 0 ? 0 : row.MaxRetries,
                    CreatedAtUtc = row.CreatedAtUtc == default(DateTime) ? DateTime.UtcNow : row.CreatedAtUtc,
                    RetryCount = Math.Max(0, row.RetryCount),
                    LastError = row.LastError ?? string.Empty,
                    NextRetryAtUtc = row.NextRetryAtUtc
                });
            }

            return snapshots;
        }

        private WarmupQueueUiItem FindQueueRow(WarmupRunState state)
        {
            if (state == null || _warmupQueueBindingList == null)
            {
                return null;
            }

            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                var row = _warmupQueueBindingList[i];
                if (row == null)
                {
                    continue;
                }

                ParseWatchRangeToSeconds(row.WatchRange, out var rowWatchMin, out var rowWatchMax);
                if (string.Equals(row.Profile ?? string.Empty, state.RunningProfileName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(row.Keywords ?? string.Empty, state.Keywords ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    row.Videos == state.VideoCount &&
                    rowWatchMin == state.WatchSecondsMin &&
                    rowWatchMax == state.WatchSecondsMax)
                {
                    return row;
                }
            }

            return null;
        }

        private static WarmupRunState BuildStateFromQueueRow(WarmupQueueUiItem row)
        {
            ParseWatchRangeToSeconds(row?.WatchRange, out var watchMin, out var watchMax);

            return new WarmupRunState
            {
                RunningProfileName = row?.Profile ?? "default",
                Keywords = row?.Keywords ?? string.Empty,
                VideoCount = row?.Videos ?? 0,
                WatchSecondsMin = watchMin,
                WatchSecondsMax = watchMax,
                AutoComment = row?.AutoComment ?? true,
                DryRun = row?.DryRun ?? false
            };
        }

        private static WarmupRunState CloneWarmupState(WarmupRunState source)
        {
            if (source == null)
            {
                return new WarmupRunState();
            }

            return new WarmupRunState
            {
                Keywords = source.Keywords,
                RunningProfileName = source.RunningProfileName,
                VideoCount = source.VideoCount,
                WatchSecondsMin = source.WatchSecondsMin,
                WatchSecondsMax = source.WatchSecondsMax,
                AutoComment = source.AutoComment,
                DryRun = source.DryRun,
                CompletedCount = source.CompletedCount,
                LastUpdatedUtc = source.LastUpdatedUtc
            };
        }

        private void dgvWarmupQueue_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _isWarmupQueueRunning || _warmupQueueBindingList == null)
            {
                return;
            }

            var column = dgvWarmupQueue.Columns[e.ColumnIndex];
            if (!string.Equals(column?.DataPropertyName, "MaxRetries", StringComparison.Ordinal))
            {
                return;
            }

            var row = dgvWarmupQueue.Rows[e.RowIndex]?.DataBoundItem as WarmupQueueUiItem;
            if (row == null)
            {
                return;
            }

            if (row.MaxRetries < 0)
            {
                row.MaxRetries = 0;
            }
            else if (row.MaxRetries > 10)
            {
                row.MaxRetries = 10;
            }

            dgvWarmupQueue.Refresh();
            SyncSchedulerFromUi();
            _ = SaveWarmupQueueSnapshotAsync();
            Log($"[QUEUE] Updated MaxRetries for '{row.Profile}' to {row.MaxRetries}.");
        }

        private async Task AppendQueueHistoryAsync(WarmupQueueRunEvent eventInfo, WarmupQueueUiItem row)
        {
            try
            {
                var record = new WarmupQueueHistoryRecord
                {
                    FinishedAtUtc = DateTime.UtcNow,
                    Profile = row?.Profile ?? string.Empty,
                    Keywords = row?.Keywords ?? string.Empty,
                    Videos = row?.Videos ?? 0,
                    Result = eventInfo.EventType == WarmupQueueEventType.Completed
                        ? "Completed"
                        : (eventInfo.EventType == WarmupQueueEventType.Skipped ? "Skipped" : "Failed"),
                    Error = (eventInfo.EventType == WarmupQueueEventType.FailedPermanent || eventInfo.EventType == WarmupQueueEventType.Skipped)
                        ? (eventInfo.ErrorMessage ?? string.Empty)
                        : string.Empty,
                    Attempts = Math.Max(1, eventInfo.Attempt)
                };

                _warmupQueueHistory.Insert(0, record);
                if (_warmupQueueHistory.Count > 300)
                {
                    _warmupQueueHistory.RemoveRange(300, _warmupQueueHistory.Count - 300);
                }

                await _warmupQueueHistoryManager.AppendAsync(record);
                RefreshQueueStatsSummary();
            }
            catch (Exception ex)
            {
                Log("[QUEUE] Failed to append history: " + ex.Message);
            }
        }

        private async void btnRefreshQueueStats_Click(object sender, EventArgs e)
        {
            _warmupQueueHistory = await _warmupQueueHistoryManager.LoadAsync();
            RefreshQueueStatsSummary();
            Log("[QUEUE] Stats refreshed.");
        }

        private void btnOpenQueueHistory_Click(object sender, EventArgs e)
        {
            var records = _warmupQueueHistory ?? new List<WarmupQueueHistoryRecord>();
            var form = new Form
            {
                Text = "Warm-up Queue History",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1080, 520),
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro
            };

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            var lblSearch = new Label
            {
                Text = "Search",
                AutoSize = true,
                Location = new Point(12, 18),
                ForeColor = Color.Gainsboro
            };
            var txtSearch = new TextBox
            {
                Location = new Point(66, 14),
                Size = new Size(240, 28),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            var lblResult = new Label
            {
                Text = "Result",
                AutoSize = true,
                Location = new Point(320, 18),
                ForeColor = Color.Gainsboro
            };
            var cbResult = new ComboBox
            {
                Location = new Point(368, 14),
                Size = new Size(120, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbResult.Items.AddRange(new object[] { "All", "Completed", "Failed", "Skipped" });
            cbResult.SelectedIndex = 0;
            var cbRange = new ComboBox
            {
                Location = new Point(640, 14),
                Size = new Size(100, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbRange.Items.AddRange(new object[] { "Today", "7d", "30d", "Custom" });
            cbRange.SelectedIndex = 0;
            var dtFrom = new DateTimePicker
            {
                Location = new Point(748, 14),
                Size = new Size(120, 28),
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Value = DateTime.Today.AddDays(-7)
            };
            var dtTo = new DateTimePicker
            {
                Location = new Point(874, 14),
                Size = new Size(120, 28),
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Value = DateTime.Today
            };
            var btnExportHistory = new Button
            {
                Text = "Export CSV",
                Location = new Point(998, 13),
                Size = new Size(70, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnExportHistory.FlatAppearance.BorderSize = 0;
            var btnRetrySkipped = new Button
            {
                Text = "Retry Skipped",
                Location = new Point(898, 13),
                Size = new Size(96, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnRetrySkipped.FlatAppearance.BorderSize = 0;

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                DataSource = new BindingList<WarmupQueueHistoryRecord>(new List<WarmupQueueHistoryRecord>(records))
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke
            };
            grid.EnableHeadersVisualStyles = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FinishedAtUtc", HeaderText = "Finished (UTC)", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Profile", HeaderText = "Profile", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Keywords", HeaderText = "Keywords", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Videos", HeaderText = "Videos", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Attempts", HeaderText = "Attempts", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Result", HeaderText = "Result", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Error", HeaderText = "Error", FillWeight = 28 });

            Action refreshGrid = () =>
            {
                var keyword = (txtSearch.Text ?? string.Empty).Trim();
                var selectedResult = cbResult.SelectedItem?.ToString() ?? "All";
                var filteredRange = FilterHistoryByRange(records, cbRange.SelectedItem?.ToString(), dtFrom.Value, dtTo.Value);
                var filtered = filteredRange
                    .Where(r =>
                        (selectedResult == "All" || string.Equals(r.Result, selectedResult, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrWhiteSpace(keyword) ||
                         (r.Profile ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (r.Keywords ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (r.Error ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
                grid.DataSource = new BindingList<WarmupQueueHistoryRecord>(filtered);
            };

            txtSearch.TextChanged += (s, a) => refreshGrid();
            cbResult.SelectedIndexChanged += (s, a) => refreshGrid();
            cbRange.SelectedIndexChanged += (s, a) =>
            {
                var isCustom = string.Equals(cbRange.SelectedItem?.ToString(), "Custom", StringComparison.OrdinalIgnoreCase);
                dtFrom.Enabled = isCustom;
                dtTo.Enabled = isCustom;
                refreshGrid();
            };
            dtFrom.ValueChanged += (s, a) => refreshGrid();
            dtTo.ValueChanged += (s, a) => refreshGrid();
            btnExportHistory.Click += (s, a) =>
            {
                try
                {
                    var current = ((BindingList<WarmupQueueHistoryRecord>)grid.DataSource)?.ToList() ?? new List<WarmupQueueHistoryRecord>();
                    if (current.Count == 0)
                    {
                        MessageBox.Show(form, "Không có dữ liệu để export.", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    using (var dialog = new SaveFileDialog())
                    {
                        dialog.FileName = $"queue_history_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                        dialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                        dialog.Title = "Export Queue History";
                        if (dialog.ShowDialog(form) != DialogResult.OK)
                        {
                            return;
                        }

                        var lines = new List<string> { "FinishedAtUtc,Profile,Keywords,Videos,Attempts,Result,Error" };
                        foreach (var item in current)
                        {
                            lines.Add(string.Join(",",
                                CsvEscape(item.FinishedAtUtc.ToString("O")),
                                CsvEscape(item.Profile),
                                CsvEscape(item.Keywords),
                                item.Videos.ToString(),
                                item.Attempts.ToString(),
                                CsvEscape(item.Result),
                                CsvEscape(item.Error)));
                        }
                        File.WriteAllLines(dialog.FileName, lines);
                        MessageBox.Show(form, "Export CSV thành công.", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(form, "Export thất bại: " + ex.Message, "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            btnRetrySkipped.Click += (s, a) =>
            {
                try
                {
                    var skippedNetwork = records
                        .Where(x => string.Equals(x.Result, "Skipped", StringComparison.OrdinalIgnoreCase))
                        .Where(x => IsRetryableNetworkOrProxyError(x.Error))
                        .Take(20)
                        .ToList();
                    if (skippedNetwork.Count == 0)
                    {
                        MessageBox.Show(form, "Không có job Skipped do network/proxy để retry.", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var added = 0;
                    foreach (var item in skippedNetwork)
                    {
                        var state = new WarmupRunState
                        {
                            Keywords = item.Keywords,
                            VideoCount = Math.Max(1, item.Videos),
                            CompletedCount = 0,
                            AutoComment = chkAutoComment?.Checked ?? false,
                            DryRun = rbDryRun?.Checked ?? false,
                            RunningProfileName = item.Profile,
                            WatchSecondsMin = ReadWatchSecondsFromUiOrDefault(numWatchMin, 7),
                            WatchSecondsMax = Math.Max(
                                ReadWatchSecondsFromUiOrDefault(numWatchMin, 7),
                                ReadWatchSecondsFromUiOrDefault(numWatchMax, 18))
                        };
                        _warmupQueueScheduler.Enqueue(state, 2);
                        _warmupQueueBindingList.Add(new WarmupQueueUiItem
                        {
                            Profile = state.RunningProfileName,
                            Keywords = state.Keywords,
                            Videos = state.VideoCount,
                            WatchRange = FormatWatchRangeForQueue(state.WatchSecondsMin, state.WatchSecondsMax),
                            AutoComment = state.AutoComment,
                            DryRun = state.DryRun,
                            Status = "Pending",
                            RetryCount = 0,
                            MaxRetries = 2,
                            LastError = string.Empty,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        added++;
                    }

                    RefreshWarmupQueueStatus();
                    _ = SaveWarmupQueueSnapshotAsync();
                    MessageBox.Show(form, $"Đã re-queue {added} job skipped (network/proxy).", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(form, "Retry Skipped thất bại: " + ex.Message, "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            topPanel.Controls.Add(lblSearch);
            topPanel.Controls.Add(txtSearch);
            topPanel.Controls.Add(lblResult);
            topPanel.Controls.Add(cbResult);
            topPanel.Controls.Add(cbRange);
            topPanel.Controls.Add(dtFrom);
            topPanel.Controls.Add(dtTo);
            topPanel.Controls.Add(btnRetrySkipped);
            topPanel.Controls.Add(btnExportHistory);
            form.Controls.Add(topPanel);
            form.Controls.Add(grid);
            refreshGrid();
            form.ShowDialog(this);
        }

        private static string CsvEscape(string value)
        {
            var text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }

        private static bool IsRetryableNetworkOrProxyError(string error)
        {
            var text = (error ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var keywords = new[]
            {
                "proxy", "ip", "timeout", "timed out", "dns", "network", "connection", "socket", "reset", "temporarily unavailable"
            };
            return keywords.Any(k => text.Contains(k));
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
                    .Select(x => $"{(x.ProductName ?? string.Empty).Trim().ToLowerInvariant()}|{(x.Price ?? string.Empty).Trim().ToLowerInvariant()}|{(x.ImageUrl ?? string.Empty).Trim().ToLowerInvariant()}")
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
            _approvalQueueItems = _approvalQueueItems ?? await _approvalQueueManager.LoadAsync();
            var source = new BindingList<ApprovalQueueItem>(_approvalQueueItems
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList());

            var form = new Form
            {
                Text = "Approval Queue",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1120, 620),
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro
            };

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
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(40, 44, 54), ForeColor = Color.WhiteSmoke };
            grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(31, 34, 42), ForeColor = Color.Gainsboro };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "JobType", FillWeight = 12, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", FillWeight = 12, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Profile", DataPropertyName = "Profile", FillWeight = 14, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Score", DataPropertyName = "SafetyScore", FillWeight = 8, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Title", DataPropertyName = "Title", FillWeight = 22, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reasons", DataPropertyName = "RiskReasons", FillWeight = 32, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reviewed By", DataPropertyName = "ReviewedBy", FillWeight = 14, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reviewed At", DataPropertyName = "ReviewedAtLabel", FillWeight = 16, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Audit Action", DataPropertyName = "LastAuditAction", FillWeight = 28, ReadOnly = true });

            var preview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };

            var txtNotes = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var txtReviewedBy = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            txtReviewedBy.Text = "reviewer";

            var cbStatusFilter = new ComboBox
            {
                Location = new Point(660, 10),
                Size = new Size(120, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbStatusFilter.Items.AddRange(new object[] { "All", "Pending", "Approved", "Completed", "Rejected" });
            cbStatusFilter.SelectedIndex = 0;
            var txtApprovalKeyword = new TextBox
            {
                Location = new Point(786, 10),
                Size = new Size(150, 28),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            var cbApprovalProfile = new ComboBox
            {
                Location = new Point(942, 10),
                Size = new Size(120, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbApprovalProfile.Items.Add("All");
            foreach (var p in (_approvalQueueItems ?? new List<ApprovalQueueItem>()).Select(x => (x.Profile ?? string.Empty).Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                cbApprovalProfile.Items.Add(p);
            }
            cbApprovalProfile.SelectedIndex = 0;
            var dtApprovalFrom = new DateTimePicker
            {
                Location = new Point(660, 42),
                Size = new Size(120, 28),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(-7)
            };
            var dtApprovalTo = new DateTimePicker
            {
                Location = new Point(786, 42),
                Size = new Size(120, 28),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };

            var btnApprove = CreateApprovalButton("Approve", new Point(12, 10));
            var btnReject = CreateApprovalButton("Reject", new Point(106, 10));
            var btnApproveLowRisk = CreateApprovalButton("Approve All Low-Risk", new Point(200, 10), 172);
            var btnRunApproved = CreateApprovalButton("Run Approved", new Point(378, 10), 130);
            var btnRunAllApproved = CreateApprovalButton("Run All Approved", new Point(514, 10), 140);
            var btnViewAuditLog = CreateApprovalButton("View Full Audit Log", new Point(12, 10), 170);
            var btnApproveFiltered = CreateApprovalButton("Approve Filtered", new Point(912, 42), 120);
            var btnRunFilteredApproved = CreateApprovalButton("Run Filtered", new Point(1038, 42), 80);

            var actionPanel = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(31, 34, 42) };
            actionPanel.Controls.Add(btnApprove);
            actionPanel.Controls.Add(btnReject);
            actionPanel.Controls.Add(btnApproveLowRisk);
            actionPanel.Controls.Add(btnRunApproved);
            actionPanel.Controls.Add(btnRunAllApproved);
            actionPanel.Controls.Add(cbStatusFilter);
            actionPanel.Controls.Add(btnViewAuditLog);
            actionPanel.Controls.Add(txtApprovalKeyword);
            actionPanel.Controls.Add(cbApprovalProfile);
            actionPanel.Controls.Add(dtApprovalFrom);
            actionPanel.Controls.Add(dtApprovalTo);
            actionPanel.Controls.Add(btnApproveFiltered);
            actionPanel.Controls.Add(btnRunFilteredApproved);

            var bottom = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(31, 34, 42) };
            bottom.Controls.Add(preview);
            bottom.Controls.Add(txtReviewedBy);
            bottom.Controls.Add(txtNotes);
            bottom.Controls.Add(actionPanel);
            split.Panel1.Controls.Add(grid);
            split.Panel2.Controls.Add(bottom);
            form.Controls.Add(split);

            Action refreshPreview = () =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null)
                {
                    preview.Text = string.Empty;
                    txtNotes.Text = string.Empty;
                    txtReviewedBy.Text = string.Empty;
                    return;
                }

                preview.Text = string.IsNullOrWhiteSpace(selected.EditedPreview) ? selected.OriginalPreview : selected.EditedPreview;
                txtNotes.Text = selected.ReviewerNotes ?? string.Empty;
                txtReviewedBy.Text = selected.ReviewedBy ?? string.Empty;
            };

            grid.SelectionChanged += (s, args) => refreshPreview();
            refreshPreview();

            btnApprove.Click += async (s, args) =>
            {
                var selected = grid.CurrentRow?.DataBoundItem as ApprovalQueueItem;
                if (selected == null) return;
                selected.Status = ApprovalStatus.Approved;
                selected.ReviewerNotes = txtNotes.Text.Trim();
                selected.ReviewedBy = txtReviewedBy.Text.Trim();
                selected.EditedPreview = preview.Text;
                selected.ReviewedAtUtc = DateTime.UtcNow;
                AppendApprovalAudit(selected, "Approved");
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
                    row.Status = ApprovalStatus.Approved;
                    row.ReviewedAtUtc = DateTime.UtcNow;
                    row.ReviewedBy = string.IsNullOrWhiteSpace(txtReviewedBy.Text) ? "bulk-review" : txtReviewedBy.Text.Trim();
                    AppendApprovalAudit(row, "Approved low-risk");
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
                    item.Status = ApprovalStatus.Approved;
                    item.ReviewedAtUtc = DateTime.UtcNow;
                    item.ReviewedBy = reviewer;
                    AppendApprovalAudit(item, "Approved filtered");
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

        private static Button CreateApprovalButton(string text, Point point, int width = 88)
        {
            var button = new Button
            {
                Text = text,
                Location = point,
                Size = new Size(width, 28),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private async Task ExecuteApprovedItemAsync(ApprovalQueueItem item)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                if (item.JobType == ApprovalJobType.RenderVideo)
                {
                    var payload = JsonConvert.DeserializeObject<RenderApprovalPayload>(item.PayloadJson ?? string.Empty) ?? new RenderApprovalPayload();
                    var script = string.IsNullOrWhiteSpace(item.EditedPreview) ? payload.Script : item.EditedPreview;
                    var settings = await _configManager.LoadAsync();
                    settings.VideoTransitionDurationSeconds = (double)numAiTransitionDuration.Value;
                    settings.VideoTextSize = (int)numAiTextSize.Value;
                    settings.VideoMusicVolume = (int)numAiMusicVolume.Value;
                    await _configManager.SaveAsync(settings);
                    var outputPath = await _videoProcessingService.GenerateProductVideoAsync(
                        _aiVideoGenInputBuffer,
                        script,
                        settings,
                        Log,
                        CancellationToken.None);
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
                    var payload = JsonConvert.DeserializeObject<AutoPostApprovalPayload>(item.PayloadJson ?? string.Empty) ?? new AutoPostApprovalPayload();
                    var hashtags = payload.Hashtags ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(item.EditedPreview))
                    {
                        // Keep hashtags from payload; edited preview is for review visibility only.
                    }

                    var captionFull = (payload.CaptionFull ?? string.Empty).Trim();
                    var uploadOnly = payload.UploadOnlyNoPublish;

                    await _tikTokAutomation.AutoPostUpToProductLinkAsync(
                        payload.VideoFolder ?? string.Empty,
                        hashtags,
                        payload.Profile,
                        CancellationToken.None,
                        Log,
                        payload.VideoFilePath,
                        string.IsNullOrWhiteSpace(captionFull) ? null : captionFull,
                        clickPublish: !uploadOnly);
                    item.Status = ApprovalStatus.Completed;
                    item.CompletedAtUtc = DateTime.UtcNow;
                    item.LastError = string.Empty;
                    Log(uploadOnly
                        ? "[APPROVAL] Auto Post upload completed (chế độ chỉ upload)."
                        : "[APPROVAL] Auto Post đã chạy (đăng tự động hoặc đã gửi lệnh đăng).");
                }
            }
            catch (Exception ex)
            {
                item.LastError = ex.Message;
                Log("[APPROVAL] Execute failed: " + ex.Message);
            }
        }

        private void btnOpenQueueTrend_Click(object sender, EventArgs e)
        {
            var records = _warmupQueueHistory ?? new List<WarmupQueueHistoryRecord>();
            var now = DateTime.UtcNow.Date;
            var start = now.AddDays(-13); // last 14 days
            var bucket = records
                .Where(x => x.FinishedAtUtc.Date >= start && x.FinishedAtUtc.Date <= now)
                .GroupBy(x => x.FinishedAtUtc.Date)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Success = g.Count(x => string.Equals(x.Result, "Completed", StringComparison.OrdinalIgnoreCase)),
                        Failed = g.Count(x => string.Equals(x.Result, "Failed", StringComparison.OrdinalIgnoreCase))
                    });

            var form = new Form
            {
                Text = "Warm-up Queue Trend (14 days)",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(980, 560),
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro
            };

            var rows = new List<QueueTrendRow>();
            for (var i = 0; i < 14; i++)
            {
                var day = start.AddDays(i);
                var success = 0;
                var failed = 0;
                if (bucket.TryGetValue(day, out var row))
                {
                    success = row.Success;
                    failed = row.Failed;
                }

                var total = success + failed;
                var rate = total == 0 ? "-" : (success * 100d / total).ToString("0.#") + "%";
                rows.Add(new QueueTrendRow
                {
                    Date = day.ToLocalTime().ToString("yyyy-MM-dd"),
                    Success = success,
                    Failed = failed,
                    Total = total,
                    SuccessRate = rate
                });
            }

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(31, 34, 42),
                SplitterWidth = 6,
                FixedPanel = FixedPanel.None,
                SplitterDistance = 250
            };

            var pnlChart = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                Padding = new Padding(12)
            };
            pnlChart.Paint += (s, pe) => DrawQueueTrendMiniChart(pe.Graphics, pnlChart.ClientRectangle, rows);

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke
            };
            grid.EnableHeadersVisualStyles = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Date", HeaderText = "Date", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Success", HeaderText = "Success", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Failed", HeaderText = "Failed", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Total", HeaderText = "Total", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SuccessRate", HeaderText = "Success Rate", FillWeight = 20 });

            grid.DataSource = new BindingList<QueueTrendRow>(rows);
            split.Panel1.Controls.Add(pnlChart);
            split.Panel2.Controls.Add(grid);
            form.Controls.Add(split);
            form.ShowDialog(this);
        }

        private void DrawQueueTrendMiniChart(Graphics g, Rectangle clientRect, IList<QueueTrendRow> rows)
        {
            if (g == null || rows == null || rows.Count == 0)
            {
                return;
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 22, 28));

            var paddingLeft = 48;
            var paddingTop = 20;
            var paddingBottom = 42;
            var paddingRight = 16;
            var plot = new Rectangle(
                clientRect.X + paddingLeft,
                clientRect.Y + paddingTop,
                Math.Max(1, clientRect.Width - paddingLeft - paddingRight),
                Math.Max(1, clientRect.Height - paddingTop - paddingBottom));

            using (var axisPen = new Pen(Color.FromArgb(90, 95, 110)))
            using (var gridPen = new Pen(Color.FromArgb(52, 56, 70)))
            using (var successBrush = new SolidBrush(Color.FromArgb(82, 170, 96)))
            using (var failedBrush = new SolidBrush(Color.FromArgb(206, 86, 86)))
            using (var textBrush = new SolidBrush(Color.Gainsboro))
            using (var smallFont = new Font("Segoe UI", 8f))
            using (var titleFont = new Font("Segoe UI Semibold", 9f))
            {
                g.DrawRectangle(axisPen, plot);
                var maxValue = Math.Max(1, rows.Max(x => Math.Max(x.Success, x.Failed)));
                var ySteps = Math.Min(5, maxValue);
                ySteps = Math.Max(1, ySteps);
                for (var i = 0; i <= ySteps; i++)
                {
                    var ratio = i / (double)ySteps;
                    var y = plot.Bottom - (int)(plot.Height * ratio);
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    var label = (int)Math.Round(maxValue * ratio);
                    g.DrawString(label.ToString(), smallFont, textBrush, plot.Left - 34, y - 7);
                }

                var groupWidth = plot.Width / (double)rows.Count;
                var barWidth = Math.Max(4f, (float)(groupWidth * 0.32));
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var gx = (float)(plot.Left + i * groupWidth);
                    var center = (float)(gx + groupWidth / 2d);

                    var successHeight = (float)(row.Success * 1d / maxValue * plot.Height);
                    var failedHeight = (float)(row.Failed * 1d / maxValue * plot.Height);
                    var successRect = new RectangleF(center - barWidth - 1f, plot.Bottom - successHeight, barWidth, successHeight);
                    var failedRect = new RectangleF(center + 1f, plot.Bottom - failedHeight, barWidth, failedHeight);
                    g.FillRectangle(successBrush, successRect);
                    g.FillRectangle(failedBrush, failedRect);

                    if (i % 2 == 0 || rows.Count <= 8)
                    {
                        var label = row.Date.Length >= 5 ? row.Date.Substring(5) : row.Date;
                        var size = g.MeasureString(label, smallFont);
                        g.DrawString(label, smallFont, textBrush, center - size.Width / 2f, plot.Bottom + 6);
                    }
                }

                g.DrawString("Success vs Failed (14 days)", titleFont, textBrush, plot.Left, clientRect.Top + 2);
                g.FillRectangle(successBrush, plot.Right - 190, clientRect.Top + 3, 14, 10);
                g.DrawString("Success", smallFont, textBrush, plot.Right - 172, clientRect.Top + 1);
                g.FillRectangle(failedBrush, plot.Right - 105, clientRect.Top + 3, 14, 10);
                g.DrawString("Failed", smallFont, textBrush, plot.Right - 87, clientRect.Top + 1);
            }
        }

        private void RefreshQueueStatsSummary()
        {
            var history = _warmupQueueHistory ?? new List<WarmupQueueHistoryRecord>();
            var selectedRange = cbQueueStatsRange?.SelectedItem?.ToString() ?? "Hôm nay";
            var from = dtQueueStatsFrom?.Value ?? DateTime.Today.AddDays(-7);
            var to = dtQueueStatsTo?.Value ?? DateTime.Today;
            var rangeItems = FilterHistoryByRange(history, selectedRange, from, to);
            var total = rangeItems.Count;
            var completed = rangeItems.Count(x => string.Equals(x.Result, "Completed", StringComparison.OrdinalIgnoreCase));
            var failed = rangeItems.Count(x => string.Equals(x.Result, "Failed", StringComparison.OrdinalIgnoreCase));
            var successRate = total <= 0 ? 0d : (completed * 100d / total);
            var avgRetry = total <= 0 ? 0d : rangeItems.Average(x => Math.Max(0, x.Attempts - 1));

            var topFailed = rangeItems
                .Where(x => string.Equals(x.Result, "Failed", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => (x.Profile ?? string.Empty).Trim())
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (lblQueueStatsSummary != null)
            {
                lblQueueStatsSummary.Text = $"Hôm nay: {total} mục, lỗi: {failed}";
            }
            if (lblQueueStatsSuccessRate != null)
            {
                lblQueueStatsSuccessRate.Text = $"Tỉ lệ thành công: {successRate:0.#}%";
            }
            if (lblQueueStatsAvgRetry != null)
            {
                lblQueueStatsAvgRetry.Text = $"Số lần thử TB: {avgRetry:0.##}";
            }
            if (lblQueueStatsTopFailedProfile != null)
            {
                lblQueueStatsTopFailedProfile.Text = topFailed == null
                    ? "Profile lỗi nhiều nhất: -"
                    : $"Profile lỗi nhiều nhất: {topFailed.Key} ({topFailed.Count()})";
            }
        }

        private static List<WarmupQueueHistoryRecord> FilterHistoryByRange(
            IEnumerable<WarmupQueueHistoryRecord> source,
            string range,
            DateTime fromLocal,
            DateTime toLocal)
        {
            var items = source?.ToList() ?? new List<WarmupQueueHistoryRecord>();
            if (items.Count == 0)
            {
                return items;
            }

            var nowUtc = DateTime.UtcNow;
            DateTime startUtc;
            DateTime endUtc;
            if (string.Equals(range, "7d", StringComparison.OrdinalIgnoreCase))
            {
                startUtc = nowUtc.AddDays(-7);
                endUtc = nowUtc;
            }
            else if (string.Equals(range, "30d", StringComparison.OrdinalIgnoreCase))
            {
                startUtc = nowUtc.AddDays(-30);
                endUtc = nowUtc;
            }
            else if (string.Equals(range, "Custom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(range, "Tùy chỉnh", StringComparison.OrdinalIgnoreCase))
            {
                var localStart = fromLocal.Date;
                var localEndExclusive = toLocal.Date.AddDays(1);
                startUtc = localStart.ToUniversalTime();
                endUtc = localEndExclusive.ToUniversalTime();
            }
            else
            {
                startUtc = nowUtc.Date;
                endUtc = nowUtc;
            }

            return items.Where(x => x.FinishedAtUtc >= startUtc && x.FinishedAtUtc < endUtc).ToList();
        }

        private void MoveSelectedQueueRow(int direction)
        {
            if (_isWarmupQueueRunning)
            {
                Log("[QUEUE] Stop queue before reordering jobs.");
                return;
            }

            if (dgvWarmupQueue?.SelectedRows == null || dgvWarmupQueue.SelectedRows.Count == 0 || _warmupQueueBindingList == null)
            {
                return;
            }

            var selected = dgvWarmupQueue.SelectedRows[0]?.DataBoundItem as WarmupQueueUiItem;
            if (selected == null)
            {
                return;
            }

            var oldIndex = _warmupQueueBindingList.IndexOf(selected);
            if (oldIndex < 0)
            {
                return;
            }

            var newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= _warmupQueueBindingList.Count)
            {
                return;
            }

            _warmupQueueBindingList.RemoveAt(oldIndex);
            _warmupQueueBindingList.Insert(newIndex, selected);
            dgvWarmupQueue.ClearSelection();
            if (newIndex >= 0 && newIndex < dgvWarmupQueue.Rows.Count)
            {
                dgvWarmupQueue.Rows[newIndex].Selected = true;
            }

            SyncSchedulerFromUi();
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private class WarmupQueueUiItem
        {
            public string Profile { get; set; } = string.Empty;
            public string Keywords { get; set; } = string.Empty;
            public int Videos { get; set; }
            public string WatchRange { get; set; } = string.Empty;
            public bool AutoComment { get; set; }
            public bool DryRun { get; set; }
            public string Status { get; set; } = "Pending";
            public int RetryCount { get; set; }
            public int MaxRetries { get; set; } = 2;
            public string LastError { get; set; } = string.Empty;
            public string RetryLabel => $"{RetryCount}/{MaxRetries + 1}";
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
            public DateTime? NextRetryAtUtc { get; set; }
            public string CreatedAtLabel => CreatedAtUtc == default(DateTime) ? string.Empty : CreatedAtUtc.ToLocalTime().ToString("HH:mm:ss");
            public string NextRetryEtaLabel => NextRetryAtUtc.HasValue ? NextRetryAtUtc.Value.ToLocalTime().ToString("HH:mm:ss") : "-";
        }

        private class QueueTrendRow
        {
            public string Date { get; set; } = string.Empty;
            public int Success { get; set; }
            public int Failed { get; set; }
            public int Total { get; set; }
            public string SuccessRate { get; set; } = "-";
        }

        private class RenderApprovalPayload
        {
            public string Script { get; set; } = string.Empty;
            public string Profile { get; set; } = string.Empty;
        }

        private class AutoPostApprovalPayload
        {
            public string VideoFolder { get; set; } = string.Empty;
            public string Hashtags { get; set; } = string.Empty;
            public string Profile { get; set; } = string.Empty;
            public string VideoFilePath { get; set; } = string.Empty;
            public string CaptionFull { get; set; } = string.Empty;
            public bool UploadOnlyNoPublish { get; set; }
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
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
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

                    await Task.Run(() => File.WriteAllText(dialog.FileName, rtbLogs.Text));
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

    }
}