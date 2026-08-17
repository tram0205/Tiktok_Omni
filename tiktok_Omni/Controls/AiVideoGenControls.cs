using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni.Controls
{
    /// <summary>Toolbar nút điều khiển Slideshow + Affiliate Deep trên tab AI Video Gen.</summary>
    public sealed class AiVideoGenControls : UserControl
    {
        private static readonly Color TintHunt = Color.FromArgb(76, 110, 245);
        private static readonly Font JellyFont = new Font("Segoe UI", 10.25F, FontStyle.Bold);
        private const int JellyMinHeight = 32;

        private static readonly Color ShowcaseTintNeutral = Color.FromArgb(68, 72, 86);
        private static readonly Color ShowcaseTintAddRow = Color.FromArgb(52, 92, 158);
        private static readonly Color ShowcaseTintScript = Color.FromArgb(108, 78, 192);
        private static readonly Color ShowcaseTintZoom = Color.FromArgb(168, 118, 42);
        private static readonly Color ShowcaseTintAudio = Color.FromArgb(138, 58, 118);
        private static readonly Color ShowcaseTintGemini = Color.FromArgb(78, 120, 166);
        private static readonly Color ShowcaseTintExcel = Color.FromArgb(52, 158, 178);
        private static readonly Color ShowcaseTintAutoPost = Color.FromArgb(68, 130, 105);
        private static readonly Color ShowcaseTintRender = Color.FromArgb(50, 110, 68);
        private static readonly Color ShowcaseTintDanger = Color.FromArgb(168, 52, 52);
        private static readonly Color ShowcaseTintStop = Color.FromArgb(192, 48, 48);
        private static readonly Color ShowcaseTintContinue = Color.FromArgb(210, 118, 38);
        private static readonly Padding ShowcaseJellyMargin = new Padding(4, 2, 4, 2);

        private IAiVideoGenControlsHost _host;

        private FlowLayoutPanel _flpSlideshowData;
        private FlowLayoutPanel _flpSlideshowExecute;
        private Panel _pnlSlideshowActionBar;
        private Panel _pnlAffiliateDeepHeaderActions;
        private FlowLayoutPanel _flpShowcaseRowManage;
        private FlowLayoutPanel _flpShowcaseRealClipLibraryCenter;
        private FlowLayoutPanel _flpShowcaseDeleteTrashRight;
        private Panel _pnlShowcaseRenderRight;
        private Panel _pnlAffiliateDeepExecuteActions;
        private FlowLayoutPanel _flpSharedRenderParams;

        private Button _btnGenerateGeminiPrompt;
        private Button _btnReviewScriptBeforeRender;
        private Button _btnAffiliateGenerateScript;
        private Button _btnAffiliateEditScript;
        private Button _btnAffiliateBatchPipeline;
        private Button _btnCopyAiVideoPrompt;
        private Button _btnSaveAiVideoPrompt;
        private Button _btnClearSlideshowGrid;
        private Button _btnProcessVideo;
        private Button _btnOpenOutputFolder;
        private Button _btnSlideshowOpenApproval;
        private Button _btnRunAffiliateDeepVideo;
        private Button _btnShowcaseOverview;
        private Button _btnShowcasePushToAutoPost;
        private FlowLayoutPanel _flpShowcaseExecute;
        private Button _btnDeepClearGrid;
        private Button _btnShowcaseCopyVideoRow;
        private Button _btnShowcaseRestoreSessions;
        private Button _btnShowcaseRealClipLibrary;
        private Button _btnShowcaseMusicLibrary;
        private Button _btnShowcaseSfxLibrary;
        private Button _btnShowcaseLogoLibrary;
        private Button _btnShowcaseMoveVideoRowUp;
        private Button _btnShowcaseMoveVideoRowDown;
        private Button _btnShowcaseTrash;
        private Button _btnShowcaseExportExcel;
        private Button _btnShowcaseGenerateScript;
        private Button _btnShowcasePreviewNarration;
        private Button _btnShowcaseListenNarration;
        private ToolTip _showcaseToolTip;
        private Button _btnShowcaseAddVideoRow;
        private Button _btnShowcaseStop;
        private ComboBox _cbGeminiStyleTemplate;
        private CheckBox _chkUseMultiVoiceNarration;
        private NumericUpDown _numAiTextSize;
        private NumericUpDown _numAiMusicVolume;
        private NumericUpDown _numAiTransitionDuration;

        public AiVideoGenControls()
        {
            Name = "aiVideoGenControls";
            Dock = DockStyle.Fill;
            AutoSize = true;
            BackColor = Color.FromArgb(31, 34, 42);
            InitializeControls();
        }

        public Panel SlideshowActionBar => _pnlSlideshowActionBar;

        public FlowLayoutPanel SlideshowDataFlow => _flpSlideshowData;

        public FlowLayoutPanel SlideshowExecuteFlow => _flpSlideshowExecute;

        public Panel AffiliateDeepHeaderActions => _pnlAffiliateDeepHeaderActions;

        public Panel AffiliateDeepExecuteActions => _pnlAffiliateDeepExecuteActions;

        public FlowLayoutPanel SharedRenderParamsPanel => _flpSharedRenderParams;

        public Button GenerateGeminiPromptButton => _btnGenerateGeminiPrompt;

        public Button ReviewScriptBeforeRenderButton => _btnReviewScriptBeforeRender;

        public Button AffiliateGenerateScriptButton => _btnAffiliateGenerateScript;

        public Button AffiliateEditScriptButton => _btnAffiliateEditScript;

        public Button AffiliateBatchPipelineButton => _btnAffiliateBatchPipeline;

        public Button CopyAiVideoPromptButton => _btnCopyAiVideoPrompt;

        public Button SaveAiVideoPromptButton => _btnSaveAiVideoPrompt;

        public Button ProcessVideoButton => _btnProcessVideo;

        public Button OpenOutputFolderButton => _btnOpenOutputFolder;

        public Button SlideshowOpenApprovalButton => _btnSlideshowOpenApproval;

        public Button RunAffiliateDeepVideoButton => _btnRunAffiliateDeepVideo;

        public Button ShowcaseOverviewButton => _btnShowcaseOverview;

        public Button ShowcasePushToAutoPostButton => _btnShowcasePushToAutoPost;

        public Button DeepGenerateScriptButton => _btnShowcaseGenerateScript;

        public Button DeepEditScriptButton => null;

        public Button AffiliateDeepOpenOutputButton => null;

        public Button ShowcaseExportExcelButton => _btnShowcaseExportExcel;

        public Button ShowcaseOpenClipsFolderButton => null;

        public Button ShowcaseRefreshClipsButton => null;


        public Button ShowcasePreviewNarrationButton => _btnShowcasePreviewNarration;

        public Button ShowcaseListenNarrationButton => _btnShowcaseListenNarration;

        public Button ShowcaseAddLocalImagesButton => null;

        public Button ShowcaseAddVideoRowButton => _btnShowcaseAddVideoRow;

        public Button ShowcaseStopButton => _btnShowcaseStop;

        public void ApplyShowcaseStopButtonUi(bool continueMode, bool enabled)
        {
            if (_btnShowcaseStop == null || _btnShowcaseStop.IsDisposed)
            {
                return;
            }

            if (_btnShowcaseStop.InvokeRequired)
            {
                _btnShowcaseStop.BeginInvoke(new Action(() => ApplyShowcaseStopButtonUi(continueMode, enabled)));
                return;
            }

            _btnShowcaseStop.Text = continueMode ? "Tiếp tục" : "⏹ Dừng lại";
            _btnShowcaseStop.Enabled = enabled;
            if (_btnShowcaseStop is JellyButton jelly)
            {
                jelly.JellyTint = continueMode ? ShowcaseTintContinue : ShowcaseTintStop;
                jelly.Invalidate();
            }
        }

        public void SetShowcaseWorkflowButtonsEnabled(bool enabled)
        {
            SetButtonEnabled(_btnShowcaseAddVideoRow, enabled);
            SetButtonEnabled(_btnShowcaseCopyVideoRow, enabled);
            SetButtonEnabled(_btnShowcaseRestoreSessions, enabled);
            SetButtonEnabled(_btnShowcaseRealClipLibrary, enabled);
            SetButtonEnabled(_btnShowcaseMusicLibrary, enabled);
            SetButtonEnabled(_btnShowcaseSfxLibrary, enabled);
            SetButtonEnabled(_btnShowcaseLogoLibrary, enabled);
            SetButtonEnabled(_btnShowcaseMoveVideoRowUp, enabled);
            SetButtonEnabled(_btnShowcaseMoveVideoRowDown, enabled);
            SetButtonEnabled(_btnDeepClearGrid, enabled);
            SetButtonEnabled(_btnShowcaseTrash, enabled);
            SetButtonEnabled(_btnShowcaseExportExcel, enabled);
            SetButtonEnabled(_btnShowcaseGenerateScript, enabled);
            SetButtonEnabled(_btnShowcasePreviewNarration, enabled);
            SetButtonEnabled(_btnShowcaseListenNarration, enabled);
        }

        public void SetShowcaseTrashButtonCount(int count)
        {
            if (_btnShowcaseTrash == null || _btnShowcaseTrash.IsDisposed)
            {
                return;
            }

            _btnShowcaseTrash.Text = "♻ Thùng rác";
        }

        private static void SetButtonEnabled(Button button, bool enabled)
        {
            if (button != null && !button.IsDisposed)
            {
                button.Enabled = enabled;
            }
        }

        public ComboBox GeminiStyleTemplateCombo => _cbGeminiStyleTemplate;

        public CheckBox UseMultiVoiceNarrationCheckBox => _chkUseMultiVoiceNarration;

        public NumericUpDown TextSizeNumeric => _numAiTextSize;

        public NumericUpDown MusicVolumeNumeric => _numAiMusicVolume;

        public NumericUpDown TransitionDurationNumeric => _numAiTransitionDuration;

        public void BindHost(IAiVideoGenControlsHost host)
        {
            _host = host;
        }

        public GeminiStyleTemplate GetSelectedGeminiStyleTemplate()
        {
            if (_cbGeminiStyleTemplate?.SelectedItem is GeminiStyleTemplate t)
            {
                return t;
            }

            return GeminiStyleTemplate.Storytelling;
        }

        public void SetSelectedGeminiStyleTemplate(GeminiStyleTemplate template)
        {
            if (_cbGeminiStyleTemplate == null)
            {
                return;
            }

            _cbGeminiStyleTemplate.SelectedItem = template;
        }

        public void SetShowcaseListenNarrationEnabled(bool enabled)
        {
            if (_btnShowcaseListenNarration == null || _btnShowcaseListenNarration.IsDisposed)
            {
                return;
            }

            _btnShowcaseListenNarration.Enabled = enabled;
        }

        public void SetShowcaseNarrationButtonMode(bool hasExistingNarration)
        {
            if (_btnShowcasePreviewNarration == null || _btnShowcasePreviewNarration.IsDisposed)
            {
                return;
            }

            if (hasExistingNarration)
            {
                _btnShowcasePreviewNarration.Text = "🔁 Tạo lại audio";
                _showcaseToolTip?.SetToolTip(_btnShowcasePreviewNarration,
                    "Xóa cache và gọi ElevenLabs lại từ kịch bản hiện tại — không qua Gemini. Dùng sau «✎ Sửa kịch bản» hoặc khi giọng đọc không ổn.");
            }
            else
            {
                _btnShowcasePreviewNarration.Text = "🎙 Tạo audio";
                _showcaseToolTip?.SetToolTip(_btnShowcasePreviewNarration,
                    "Tạo narration.mp3 — Edge TTS (miễn phí) hoặc ElevenLabs khi bấm.");
            }
        }

        private void InitializeControls()
        {
            _flpSlideshowData = CreateActionFlowPanel();
            _flpSlideshowExecute = CreateActionFlowPanel();

            _btnGenerateGeminiPrompt = CreateToolbarButton("Tạo kịch bản AI", false, 140);
            _btnGenerateGeminiPrompt.Name = "btnGenerateGeminiPrompt";
            _btnGenerateGeminiPrompt.BackColor = Color.FromArgb(76, 110, 245);
            _btnGenerateGeminiPrompt.Click += async (_, __) => await RunHostAsync(_btnGenerateGeminiPrompt, h => h.GenerateGeminiPromptAsync()).ConfigureAwait(true);

            _btnReviewScriptBeforeRender = CreateToolbarButton("Duyệt kịch bản trước render", false, 180);
            _btnReviewScriptBeforeRender.Name = "btnReviewScriptBeforeRender";
            _btnReviewScriptBeforeRender.BackColor = Color.FromArgb(88, 101, 242);
            _btnReviewScriptBeforeRender.Click += async (_, __) => await RunHostAsync(_btnReviewScriptBeforeRender, h => h.ReviewScriptBeforeRenderAsync()).ConfigureAwait(true);

            _btnAffiliateGenerateScript = CreateToolbarButton("Sinh Script", false, 110);
            _btnAffiliateGenerateScript.Name = "btnAffiliateGenerateScript";
            _btnAffiliateGenerateScript.BackColor = Color.FromArgb(78, 120, 166);
            _btnAffiliateGenerateScript.Click += async (_, __) => await RunHostAsync(_btnAffiliateGenerateScript, h => h.GenerateAffiliateScriptAsync()).ConfigureAwait(true);

            _btnAffiliateEditScript = CreateToolbarButton("Sửa Script", false, 100);
            _btnAffiliateEditScript.Name = "btnAffiliateEditScript";
            _btnAffiliateEditScript.BackColor = Color.FromArgb(60, 64, 77);
            _btnAffiliateEditScript.Click += async (_, __) => await RunHostAsync(_btnAffiliateEditScript, h => h.EditAffiliateScriptAsync()).ConfigureAwait(true);

            _btnAffiliateBatchPipeline = CreateToolbarButton("Pipeline hàng loạt", false, 150);
            _btnAffiliateBatchPipeline.Name = "btnAffiliateBatchPipeline";
            _btnAffiliateBatchPipeline.BackColor = Color.FromArgb(100, 70, 160);
            _btnAffiliateBatchPipeline.Click += async (_, __) => await RunHostAsync(_btnAffiliateBatchPipeline, h => h.RunBatchPipelineAsync()).ConfigureAwait(true);

            _btnCopyAiVideoPrompt = CreateToolbarButton("Sao chép", false, 90);
            _btnCopyAiVideoPrompt.Name = "btnCopyAiVideoPrompt";
            _btnCopyAiVideoPrompt.Click += (_, __) => _host?.CopyAiVideoPrompt();

            _btnSaveAiVideoPrompt = CreateToolbarButton("Lưu .txt", false, 90);
            _btnSaveAiVideoPrompt.Name = "btnSaveAiVideoPrompt";
            _btnSaveAiVideoPrompt.Click += (_, __) => _host?.SaveAiVideoPrompt();

            _btnClearSlideshowGrid = CreateThemedUtilityButton("btnClearAiGenGrid", "Xoá dòng", ButtonRole.Danger);
            _btnClearSlideshowGrid.Click += (_, __) => _host?.ClearActiveGrid();

            _btnProcessVideo = CreateProcessVideoButton();
            _btnProcessVideo.Click += async (_, __) => await RunHostAsync(_btnProcessVideo, h => h.ProcessSlideshowVideoAsync()).ConfigureAwait(true);

            _btnOpenOutputFolder = CreateThemedUtilityButton("btnOpenOutputFolder", "📂 Output", ButtonRole.Neutral);
            _btnOpenOutputFolder.Click += async (_, __) => await RunHostAsync(_btnOpenOutputFolder, h => h.OpenSlideshowOutputFolderAsync()).ConfigureAwait(true);

            _btnSlideshowOpenApproval = CreateSecondaryButton("btnSlideshowOpenApproval", "Mở Hàng duyệt");
            _btnSlideshowOpenApproval.Click += (_, __) => _host?.OpenApprovalQueue();

            var slideshowTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ShowAlways = true };
            slideshowTip.SetToolTip(_btnAffiliateGenerateScript,
                "Chọn dòng trên lưới sản phẩm — sinh lời thoại preview bằng Gemini (cần AI API Key).");
            slideshowTip.SetToolTip(_btnAffiliateEditScript,
                "Chọn đúng một dòng sản phẩm — mở hộp thoại sửa script.");
            slideshowTip.SetToolTip(_btnAffiliateBatchPipeline,
                "Hunt (tab Săn Video) → lọc HQ → đẩy Slideshow → sinh script → render. Cần từ khóa + AI API Key.");

            var lblGeminiStyle = new Label
            {
                Text = "Mẫu Gemini:",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(0, 8, 4, 0)
            };
            _cbGeminiStyleTemplate = new ComboBox
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
                _cbGeminiStyleTemplate.Items.Add(t);
            }

            _cbGeminiStyleTemplate.SelectedItem = GeminiStyleTemplate.Storytelling;
            _cbGeminiStyleTemplate.SelectedIndexChanged += async (_, __) =>
            {
                if (_host == null)
                {
                    return;
                }

                try
                {
                    await _host.SaveGeminiStyleTemplateAsync(GetSelectedGeminiStyleTemplate()).ConfigureAwait(true);
                }
                catch
                {
                }
            };

            _flpSlideshowData.Controls.Add(lblGeminiStyle);
            _flpSlideshowData.Controls.Add(_cbGeminiStyleTemplate);
            _flpSlideshowData.Controls.Add(_btnGenerateGeminiPrompt);
            _flpSlideshowData.Controls.Add(_btnAffiliateGenerateScript);
            _flpSlideshowData.Controls.Add(_btnAffiliateEditScript);
            _flpSlideshowData.Controls.Add(_btnReviewScriptBeforeRender);
            _flpSlideshowData.Controls.Add(_btnCopyAiVideoPrompt);
            _flpSlideshowData.Controls.Add(_btnSaveAiVideoPrompt);
            _flpSlideshowData.Controls.Add(_btnClearSlideshowGrid);

            _chkUseMultiVoiceNarration = new CheckBox
            {
                Name = "chkUseMultiVoiceNarration",
                Text = "Đa giọng đọc (Multi-voice)",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(0, 8, 0, 0)
            };

            _numAiTextSize = CreateNumeric("numAiTextSize", 24, 96, 50);
            _numAiMusicVolume = CreateNumeric("numAiMusicVolume", 0, 100, 14);
            _numAiTransitionDuration = CreateNumericDecimal("numAiTransitionDuration", 0.2M, 2.0M, 0.6M);

            // Nhóm tham số render dùng chung — được gắn (remount) vào hàng Execute của Slideshow hoặc Showcase
            // tuỳ theo tab đang chọn, để giá trị luôn hiển thị/chỉnh được ở cả hai nơi dùng chung 1 bộ settings.
            _flpSharedRenderParams = CreateActionFlowPanel();
            _flpSharedRenderParams.WrapContents = false;
            _flpSharedRenderParams.Margin = new Padding(0);
            _flpSharedRenderParams.Controls.Add(_chkUseMultiVoiceNarration);
            _flpSharedRenderParams.Controls.Add(CreateMetricLabel("Cỡ chữ", leftPad: 10));
            _flpSharedRenderParams.Controls.Add(_numAiTextSize);
            _flpSharedRenderParams.Controls.Add(CreateMetricLabel("Âm lượng nhạc", leftPad: 8));
            _flpSharedRenderParams.Controls.Add(_numAiMusicVolume);
            _flpSharedRenderParams.Controls.Add(CreateMetricLabel("Chuyển cảnh (s)", leftPad: 8));
            _flpSharedRenderParams.Controls.Add(_numAiTransitionDuration);

            _flpSlideshowExecute.Controls.Add(_btnAffiliateBatchPipeline);
            _flpSlideshowExecute.Controls.Add(_btnProcessVideo);
            _flpSlideshowExecute.Controls.Add(_btnOpenOutputFolder);
            _flpSlideshowExecute.Controls.Add(_btnSlideshowOpenApproval);
            _flpSlideshowExecute.Controls.Add(_flpSharedRenderParams);

            _pnlSlideshowActionBar = BuildModeActionPanel(_flpSlideshowData, _flpSlideshowExecute);

            _pnlAffiliateDeepHeaderActions = new Panel
            {
                Name = "pnlAffiliateDeepHeaderActions",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                MinimumSize = new Size(0, Form1.AppJellyButtonHeight * 2 + 14),
                BackColor = Color.Transparent
            };
            _pnlAffiliateDeepHeaderActions.Resize += (_, __) => LayoutShowcaseHeaderToolbar();
            _pnlAffiliateDeepExecuteActions = new Panel
            {
                Name = "pnlAffiliateDeepExecuteActions",
                AutoSize = true,
                MinimumSize = new Size(0, 104),
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 12)
            };
            _pnlAffiliateDeepExecuteActions.Resize += (_, __) => LayoutShowcaseRenderRow();

            _btnShowcaseAddVideoRow = CreateShowcaseSolidRectButton(
                "btnShowcaseAddVideoRow",
                "+ Thêm dòng",
                ShowcaseTintAddRow,
                118);
            _btnShowcaseAddVideoRow.Click += (_, __) => _host?.AddShowcaseVideoRow();

            _btnShowcaseCopyVideoRow = CreateShowcaseSolidRectButton(
                "btnShowcaseCopyVideoRow",
                "📋 Copy dòng",
                ShowcaseTintNeutral,
                118);
            _btnShowcaseCopyVideoRow.Click += (_, __) => _host?.CopyShowcaseVideoRow();

            _btnShowcaseRestoreSessions = CreateShowcaseSolidRectButton(
                "btnShowcaseRestoreSessions",
                "🔗 Gắn phiên",
                Color.FromArgb(56, 96, 128),
                118);
            _btnShowcaseRestoreSessions.Click += (_, __) => _host?.RestoreShowcaseVideosFromDisk();

            _btnShowcaseRealClipLibrary = CreateShowcaseJellyButton(
                "btnShowcaseRealClipLibrary",
                "📁 Thư viện clip thật",
                Color.FromArgb(56, 108, 88),
                220);
            _btnShowcaseRealClipLibrary.Click += (_, __) => _host?.OpenShowcaseRealClipLibrary();

            _btnShowcaseMusicLibrary = CreateShowcaseJellyButton(
                "btnShowcaseMusicLibrary",
                "🎵 Thư viện nhạc nền",
                Color.FromArgb(138, 58, 118),
                200);
            _btnShowcaseMusicLibrary.Click += (_, __) => _host?.OpenShowcaseMusicLibrary();

            _btnShowcaseSfxLibrary = CreateShowcaseJellyButton(
                "btnShowcaseSfxLibrary",
                "🔊 Hiệu ứng âm thanh",
                Color.FromArgb(168, 118, 42),
                200);
            _btnShowcaseSfxLibrary.Click += (_, __) => _host?.OpenShowcaseSfxLibrary();

            _btnShowcaseLogoLibrary = CreateShowcaseJellyButton(
                "btnShowcaseLogoLibrary",
                "🏷 Thư viện logo",
                Color.FromArgb(72, 118, 168),
                180);
            _btnShowcaseLogoLibrary.Click += (_, __) => _host?.OpenShowcaseLogoLibrary();

            _btnShowcaseMoveVideoRowUp = CreateShowcaseArrowButton(
                "btnShowcaseMoveVideoRowUp",
                "↑");
            _btnShowcaseMoveVideoRowUp.Click += (_, __) => _host?.MoveShowcaseVideoRowUp();

            _btnShowcaseMoveVideoRowDown = CreateShowcaseArrowButton(
                "btnShowcaseMoveVideoRowDown",
                "↓");
            _btnShowcaseMoveVideoRowDown.Click += (_, __) => _host?.MoveShowcaseVideoRowDown();

            _btnDeepClearGrid = CreateShowcaseSolidRectButton(
                "btnDeepClearAiGenGrid",
                "🗑 Xoá dòng",
                ShowcaseTintDanger,
                108);
            _btnDeepClearGrid.Click += (_, __) => _host?.ClearActiveGrid();

            _btnShowcaseTrash = CreateShowcaseSolidRectButton(
                "btnShowcaseTrash",
                "♻ Thùng rác",
                Color.FromArgb(88, 92, 72),
                118);
            _btnShowcaseTrash.Click += (_, __) => _host?.OpenShowcaseTrash();

            _btnShowcaseExportExcel = CreateShowcaseJellyButton("btnShowcaseExportExcel", "Tải excel prompt", ShowcaseTintExcel, 232);
            _btnShowcaseExportExcel.Click += async (_, __) => await RunHostAsync(_btnShowcaseExportExcel, h => h.ExportShowcaseExcelAsync()).ConfigureAwait(true);

            _btnShowcaseGenerateScript = CreateShowcaseJellyButton(
                "btnShowcaseGenerateScript",
                "📝 Tạo kịch bản",
                ShowcaseTintScript,
                168);
            _btnShowcaseGenerateScript.Click += async (_, __) =>
                await RunHostAsync(_btnShowcaseGenerateScript, h => h.GenerateShowcaseSceneScriptAsync()).ConfigureAwait(true);

            _btnShowcasePreviewNarration = CreateShowcaseJellyButton(
                "btnShowcasePreviewNarration",
                "🎙 Tạo audio",
                ShowcaseTintAudio,
                132);
            _btnShowcasePreviewNarration.Click += async (_, __) =>
                await RunHostAsync(_btnShowcasePreviewNarration, h => h.BuildShowcaseNarrationAsync()).ConfigureAwait(true);

            _btnShowcaseListenNarration = CreateShowcaseJellyButton(
                "btnShowcaseListenNarration",
                "🔊 Nghe audio",
                ShowcaseTintNeutral,
                132);
            _btnShowcaseListenNarration.Enabled = false;
            _btnShowcaseListenNarration.Click += async (_, __) =>
                await RunHostAsync(_btnShowcaseListenNarration, h => h.ListenShowcaseNarrationAsync()).ConfigureAwait(true);

            _btnShowcaseStop = CreateShowcaseJellyButton("btnShowcaseStop", "⏹ Dừng lại", ShowcaseTintStop, 128);
            _btnShowcaseStop.Enabled = false;
            _btnShowcaseStop.Click += async (_, __) =>
            {
                if (_host == null)
                {
                    return;
                }

                _btnShowcaseStop.Enabled = false;
                try
                {
                    await _host.HandleShowcaseStopResumeAsync().ConfigureAwait(true);
                }
                finally
                {
                    _host.RefreshShowcaseStopButton();
                }
            };

            _btnShowcaseOverview = CreateShowcaseJellyButton(
                "btnShowcaseOverview",
                "Xem tổng quan",
                Color.FromArgb(72, 118, 198),
                188);

            _btnShowcasePushToAutoPost = CreateShowcaseJellyButton(
                "btnShowcasePushToAutoPost",
                "Đăng tự động",
                ShowcaseTintAutoPost,
                160);

            _btnRunAffiliateDeepVideo = Form1.CreateAppPrimaryJellyButton(
                "btnRunAffiliateDeepVideo",
                "▶ Render video",
                Color.FromArgb(22, 168, 86),
                minWidth: 300,
                margin: new Padding(8, 0, 8, 0));
            if (_btnRunAffiliateDeepVideo is JellyButton renderJelly)
            {
                renderJelly.JellyFillOpacity = 0.92f;
                renderJelly.ForeColor = Color.FromArgb(255, 252, 240);
            }

            _btnShowcaseOverview.Click += async (_, __) =>
            {
                if (_host == null)
                {
                    return;
                }

                if (_host.IsShowcaseTabPaused)
                {
                    MessageBox.Show(
                        "Tab Showcase đang dừng — bấm «Tiếp tục» (nút cam) rồi thử lại.",
                        "Xem tổng quan",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                await _host.PreviewShowcaseOverviewAsync().ConfigureAwait(true);
            };

            _btnShowcasePushToAutoPost.Click += (_, __) =>
            {
                if (_host == null)
                {
                    return;
                }

                if (_host.IsShowcaseTabPaused)
                {
                    MessageBox.Show(
                        "Tab Showcase đang dừng — bấm «Tiếp tục» (nút cam) rồi thử lại.",
                        "Đăng tự động",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                _host.PushShowcaseSelectionToAutoPost();
            };

            _btnRunAffiliateDeepVideo.Click += async (_, __) =>
            {
                if (_host == null)
                {
                    return;
                }

                if (!_host.TryBeginShowcaseTabWork())
                {
                    return;
                }

                _btnRunAffiliateDeepVideo.Enabled = false;
                if (_btnShowcaseOverview != null && !_btnShowcaseOverview.IsDisposed)
                {
                    _btnShowcaseOverview.Enabled = false;
                }

                if (_btnShowcasePushToAutoPost != null && !_btnShowcasePushToAutoPost.IsDisposed)
                {
                    _btnShowcasePushToAutoPost.Enabled = false;
                }

                try
                {
                    await _host.RunAffiliateDeepVideoAsync().ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _host.EndShowcaseTabWork();
                }
            };

            _showcaseToolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ShowAlways = true };
            _showcaseToolTip.SetToolTip(_btnShowcaseExportExcel,
                "Xuất Excel (sheet «Clip Prompts»: loại ảnh, công cụ, prompt Veo/Kling, zoom gợi ý) — dùng tạo clip bên ngoài app.");
            _showcaseToolTip.SetToolTip(_btnShowcaseGenerateScript,
                "Gemini sinh kịch bản, thoại nháp và prompt clip theo ảnh storyboard (chọn dòng trên lưới).");
            SetShowcaseNarrationButtonMode(hasExistingNarration: false);
            _showcaseToolTip.SetToolTip(_btnShowcaseListenNarration,
                "Mở file narration.mp3 đã tạo — nghe thử trước khi render (không gọi ElevenLabs lại).");
            _showcaseToolTip.SetToolTip(_btnShowcaseAddVideoRow,
                "Thêm một dòng video mới trên lưới — thêm ảnh bằng cột «Ảnh» (➕ Thêm ảnh) trên từng dòng.");
            _showcaseToolTip.SetToolTip(_btnShowcaseCopyVideoRow,
                "Sao chép dòng đang chọn (kịch bản, cảnh, cài đặt) — bản sao được thêm ở cuối lưới.");
            _showcaseToolTip.SetToolTip(_btnShowcaseRestoreSessions,
                "Quét thư mục Showcase trên đĩa → gắn lại ảnh, clip, kịch bản Excel cho dòng đang có tên SP (khi draft mất liên kết).");
            _showcaseToolTip.SetToolTip(_btnShowcaseRealClipLibrary,
                "Quản lý kho clip quay tay theo loại SP — thêm clip một lần, dùng cho mọi dòng cùng loại.");
            _showcaseToolTip.SetToolTip(_btnShowcaseMusicLibrary,
                "Quản lý nhạc nền Assets\\Audio\\Music — Gemini chọn khi «Tạo kịch bản».");
            _showcaseToolTip.SetToolTip(_btnShowcaseSfxLibrary,
                "Quản lý hiệu ứng Assets\\Audio\\Sfx — hook / cảnh / CTA khi «Tạo kịch bản».");
            _showcaseToolTip.SetToolTip(_btnShowcaseLogoLibrary,
                "Quản lý logo Assets\\Logos — chọn trên cột «Logo» khi render video.");
            _showcaseToolTip.SetToolTip(_btnShowcaseMoveVideoRowUp,
                "Đưa dòng đang chọn lên một vị trí trên lưới.");
            _showcaseToolTip.SetToolTip(_btnShowcaseMoveVideoRowDown,
                "Đưa dòng đang chọn xuống một vị trí trên lưới.");
            _showcaseToolTip.SetToolTip(_btnDeepClearGrid,
                "Xóa dòng đang chọn — chuyển vào thùng rác (giữ 24 giờ, có thể khôi phục).");
            _showcaseToolTip.SetToolTip(_btnShowcaseTrash,
                "Mở thùng rác — khôi phục hoặc xóa vĩnh viễn dòng đã xóa (tự xóa sau 24 giờ).");
            _showcaseToolTip.SetToolTip(_btnShowcaseOverview,
                "Bảng trực quan: pipeline, timeline từng cảnh (ảnh + clip + thoại), thoại/phụ đề/nhạc, checklist Render.");
            _showcaseToolTip.SetToolTip(_btnShowcasePushToAutoPost,
                "Chọn dòng đã render xong → copy MP4 vào Publishing và thêm lịch TikTok + Facebook + YouTube trên tab Đăng tự động.");
            _showcaseToolTip.SetToolTip(_btnRunAffiliateDeepVideo,
                "Bấm để render — nếu thiếu điều kiện, app liệt kê cụ thể (clip, thoại, TTS, FFmpeg…).");
            _showcaseToolTip.SetToolTip(_btnShowcaseStop,
                "Dừng lại mọi thao tác tab (Gemini, Zoom, audio, render). Sau đó bấm «Tiếp tục» (cam) để mở khóa — không tự chạy lại job đã hủy.");

            // Hàng trên (trong header): thư viện dùng chung — canh giữa
            _flpShowcaseRealClipLibraryCenter = CreateActionFlowPanel();
            _flpShowcaseRealClipLibraryCenter.Dock = DockStyle.None;
            _flpShowcaseRealClipLibraryCenter.WrapContents = false;
            _flpShowcaseRealClipLibraryCenter.Controls.Add(_btnShowcaseRealClipLibrary);
            _flpShowcaseRealClipLibraryCenter.Controls.Add(_btnShowcaseMusicLibrary);
            _flpShowcaseRealClipLibraryCenter.Controls.Add(_btnShowcaseSfxLibrary);
            _flpShowcaseRealClipLibraryCenter.Controls.Add(_btnShowcaseLogoLibrary);

            // Hàng dưới (trong header): quản lý dòng — trái (vị trí cũ)
            _flpShowcaseRowManage = CreateActionFlowPanel();
            _flpShowcaseRowManage.Dock = DockStyle.None;
            _flpShowcaseRowManage.WrapContents = false;
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseAddVideoRow);
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseCopyVideoRow);
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseRestoreSessions);
            _flpShowcaseRowManage.Controls.Add(_btnDeepClearGrid);
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseTrash);
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseMoveVideoRowUp);
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseMoveVideoRowDown);

            _flpShowcaseDeleteTrashRight = CreateActionFlowPanel();
            _flpShowcaseDeleteTrashRight.Dock = DockStyle.None;
            _flpShowcaseDeleteTrashRight.WrapContents = false;
            _flpShowcaseDeleteTrashRight.Controls.Add(_btnShowcaseStop);
            _btnShowcasePreviewNarration.Visible = false;

            _pnlAffiliateDeepHeaderActions.Controls.Add(_flpShowcaseRealClipLibraryCenter);
            _pnlAffiliateDeepHeaderActions.Controls.Add(_flpShowcaseRowManage);
            LayoutShowcaseHeaderToolbar();

            // Hàng render — giữa; dừng lại phải
            var tblRenderCenter = new TableLayoutPanel
            {
                Name = "tblShowcaseRenderCenter",
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            tblRenderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblRenderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblRenderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblRenderCenter.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _flpShowcaseExecute = CreateActionFlowPanel();
            _flpShowcaseExecute.Dock = DockStyle.None;
            _flpShowcaseExecute.WrapContents = false;
            _flpShowcaseExecute.Controls.Add(_btnShowcaseGenerateScript);
            _flpShowcaseExecute.Controls.Add(_btnShowcaseOverview);
            _flpShowcaseExecute.Controls.Add(_btnRunAffiliateDeepVideo);
            _flpShowcaseExecute.Controls.Add(_btnShowcasePushToAutoPost);

            _pnlShowcaseRenderRight = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            _pnlShowcaseRenderRight.Controls.Add(_flpShowcaseDeleteTrashRight);

            tblRenderCenter.Controls.Add(_flpShowcaseExecute, 1, 0);
            tblRenderCenter.Controls.Add(_pnlShowcaseRenderRight, 2, 0);
            _pnlAffiliateDeepExecuteActions.Controls.Add(tblRenderCenter);
            LayoutShowcaseRenderRow();
        }

        private async Task RunHostAsync(Button button, Func<IAiVideoGenControlsHost, Task> action)
        {
            if (_host == null || button == null || action == null)
            {
                return;
            }

            if (!_host.TryBeginShowcaseTabWork())
            {
                return;
            }

            button.Enabled = false;
            try
            {
                await action(_host).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _host.EndShowcaseTabWork();
                if (button != null && !button.IsDisposed && !_host.IsShowcaseTabPaused)
                {
                    button.Enabled = true;
                }
            }
        }

        private void LayoutShowcaseHeaderToolbar()
        {
            if (_pnlAffiliateDeepHeaderActions == null
                || _flpShowcaseRealClipLibraryCenter == null
                || _flpShowcaseRowManage == null)
            {
                return;
            }

            var host = _pnlAffiliateDeepHeaderActions;
            if (host.Width <= 0 || host.Height <= 0)
            {
                return;
            }

            var rowHeight = Form1.AppJellyButtonHeight + 6;

            _flpShowcaseRealClipLibraryCenter.PerformLayout();
            var libraryX = Math.Max(0, (host.ClientSize.Width - _flpShowcaseRealClipLibraryCenter.Width) / 2);
            _flpShowcaseRealClipLibraryCenter.Location = new Point(libraryX, 0);
            _flpShowcaseRealClipLibraryCenter.BringToFront();

            _flpShowcaseRowManage.PerformLayout();
            _flpShowcaseRowManage.Location = new Point(0, rowHeight);
            _flpShowcaseRowManage.BringToFront();
        }

        private void LayoutShowcaseRenderRow()
        {
            if (_pnlShowcaseRenderRight == null || _flpShowcaseDeleteTrashRight == null)
            {
                return;
            }

            if (_pnlShowcaseRenderRight.Width <= 0 || _pnlShowcaseRenderRight.Height <= 0)
            {
                return;
            }

            _flpShowcaseDeleteTrashRight.PerformLayout();
            var x = Math.Max(0, _pnlShowcaseRenderRight.ClientSize.Width - _flpShowcaseDeleteTrashRight.Width);
            var y = Math.Max(0, (_pnlShowcaseRenderRight.ClientSize.Height - _flpShowcaseDeleteTrashRight.Height) / 2);
            _flpShowcaseDeleteTrashRight.Location = new Point(x, y);
            _flpShowcaseDeleteTrashRight.BringToFront();
        }

        private static FlowLayoutPanel CreateActionFlowPanel()
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

        private static Panel BuildModeActionPanel(FlowLayoutPanel dataRow, FlowLayoutPanel executeRow)
        {
            var host = new Panel
            {
                Name = "pnlSlideshowActionBar",
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

        private static JellyButton CreateShowcaseJellyButton(string name, string text, Color tint, int minWidth)
        {
            return Form1.CreateAppJellyButton(
                name,
                text,
                tint,
                minWidth: minWidth,
                margin: ShowcaseJellyMargin);
        }

        /// <summary>Nút mũi tên gọn (không nền hộp) — lên/xuống dòng lưới.</summary>
        private static Button CreateShowcaseArrowButton(string name, string arrow)
        {
            var height = Form1.AppJellyButtonHeight;
            const int width = 30;
            var font = new Font("Segoe UI", 14F, FontStyle.Regular);

            var btn = new Button
            {
                Name = name,
                Text = arrow,
                Font = font,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.FromArgb(210, 214, 222),
                AutoSize = false,
                Height = height,
                Width = width,
                MinimumSize = new Size(width, height),
                MaximumSize = new Size(width, height),
                Margin = new Padding(1, 2, 1, 2),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 56, 68);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(68, 72, 86);
            return btn;
        }

        /// <summary>Nút chữ nhật đặc (không jelly trong suốt) — dùng cho Thêm/Xoá dòng.</summary>
        private static ShowcaseSolidRectButton CreateShowcaseSolidRectButton(string name, string text, Color back, int minWidth)
        {
            var height = Form1.AppJellyButtonHeight;
            var textW = TextRenderer.MeasureText(
                text,
                JellyFont,
                new Size(int.MaxValue, height),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            var width = Math.Max(minWidth, textW + 28);

            var border = ControlPaint.Dark(back);
            var btn = new ShowcaseSolidRectButton
            {
                Name = name,
                Text = text,
                Font = JellyFont,
                NormalBackColor = back,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Height = height,
                Width = width,
                MinimumSize = new Size(width, height),
                MaximumSize = new Size(width, height),
                Margin = ShowcaseJellyMargin,
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.MouseOverBackColor = BlendColor(back, Color.White, 0.12f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.DarkDark(back);
            return btn;
        }

        private static Color BlendColor(Color baseColor, Color overlay, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            var r = (int)(baseColor.R + (overlay.R - baseColor.R) * amount);
            var g = (int)(baseColor.G + (overlay.G - baseColor.G) * amount);
            var b = (int)(baseColor.B + (overlay.B - baseColor.B) * amount);
            return Color.FromArgb(baseColor.A, r, g, b);
        }

        private static Button CreateToolbarButton(string text, bool executeStyle, int minWidth)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minWidth, 32),
                Margin = new Padding(4, 2, 4, 2),
                UseVisualStyleBackColor = false
            };
            var role = executeStyle ? ButtonRole.Primary : UIThemeManager.InferRole(null, text);
            btn.ApplyTheme(role);
            return btn;
        }

        private static Button CreateThemedUtilityButton(string name, string text, ButtonRole role)
        {
            var btn = new Button
            {
                Name = name,
                Text = text,
                AutoSize = true,
                Height = 34,
                Margin = new Padding(6, 0, 0, 0)
            };
            btn.ApplyTheme(role);
            return btn;
        }

        private static Button CreateSecondaryButton(string name, string text)
        {
            var btn = new Button
            {
                Name = name,
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(96, 32),
                Margin = new Padding(4, 2, 4, 2),
                UseVisualStyleBackColor = false
            };
            btn.ApplyTheme(ButtonRole.Neutral);
            return btn;
        }

        private Button CreateProcessVideoButton()
        {
            var width = Math.Max(120, MeasureTextWidth("Render & Đóng gói") + 22);
            var button = new JellyButton
            {
                Name = "btnProcessVideo",
                Text = "Render & Đóng gói",
                Font = JellyFont,
                JellyTint = TintHunt,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Width = width,
                Height = JellyMinHeight,
                MinimumSize = new Size(width, JellyMinHeight),
                MaximumSize = new Size(width, JellyMinHeight),
                Margin = new Padding(4, 3, 4, 3),
                Tag = JellyButton.ChromeTag,
                AccessibleName = JellyButton.ChromeTag
            };
            return button;
        }

        private static int MeasureTextWidth(string text)
        {
            return TextRenderer.MeasureText(
                text,
                JellyFont,
                new Size(int.MaxValue, JellyMinHeight),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width;
        }

        private static Label CreateMetricLabel(string text, int leftPad = 0)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Margin = new Padding(leftPad, 8, 4, 0)
            };
        }

        private static NumericUpDown CreateNumeric(string name, int min, int max, int value)
        {
            return new NumericUpDown
            {
                Name = name,
                Minimum = min,
                Maximum = max,
                Value = value,
                Width = 56,
                Height = 24,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 8, 0)
            };
        }

        private static NumericUpDown CreateNumericDecimal(string name, decimal min, decimal max, decimal value)
        {
            return new NumericUpDown
            {
                Name = name,
                DecimalPlaces = 1,
                Increment = 0.1M,
                Minimum = min,
                Maximum = max,
                Value = value,
                Width = 56,
                Height = 24,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 0, 0)
            };
        }
    }

    /// <summary>Nút đặc Showcase — vẽ chữ canh giữa ô (emoji + text).</summary>
    internal sealed class ShowcaseSolidRectButton : Button
    {
        private Color _normalBackColor;

        public ShowcaseSolidRectButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }

        public Color NormalBackColor
        {
            get => _normalBackColor;
            set
            {
                _normalBackColor = value;
                BackColor = value;
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            using (var brush = new SolidBrush(BackColor))
            {
                g.FillRectangle(brush, rect);
            }

            if (FlatAppearance.BorderSize > 0)
            {
                var borderRect = new Rectangle(0, 0, rect.Width - 1, rect.Height - 1);
                using (var pen = new Pen(FlatAppearance.BorderColor))
                {
                    g.DrawRectangle(pen, borderRect);
                }
            }

            if (string.IsNullOrEmpty(Text))
            {
                return;
            }

            var textColor = Enabled ? ForeColor : Color.FromArgb(140, ForeColor);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                rect,
                textColor,
                TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.SingleLine
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (FlatAppearance.MouseOverBackColor != Color.Empty)
            {
                BackColor = FlatAppearance.MouseOverBackColor;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            BackColor = _normalBackColor;
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (FlatAppearance.MouseDownBackColor != Color.Empty)
            {
                BackColor = FlatAppearance.MouseDownBackColor;
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            BackColor = ClientRectangle.Contains(mevent.Location)
                        && FlatAppearance.MouseOverBackColor != Color.Empty
                ? FlatAppearance.MouseOverBackColor
                : _normalBackColor;
        }
    }
}
