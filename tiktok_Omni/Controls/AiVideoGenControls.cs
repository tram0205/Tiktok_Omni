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
        private static readonly Color ShowcaseTintVoiceover = Color.FromArgb(36, 128, 138);
        private static readonly Color ShowcaseTintAudio = Color.FromArgb(138, 58, 118);
        private static readonly Color ShowcaseTintGemini = Color.FromArgb(78, 120, 166);
        private static readonly Color ShowcaseTintExcel = Color.FromArgb(52, 158, 178);
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
        private FlowLayoutPanel _flpShowcaseWorkflow;
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
        private Button _btnDeepGenerateScript;
        private Button _btnRunAffiliateDeepVideo;
        private Button _btnShowcaseOverview;
        private FlowLayoutPanel _flpShowcaseExecute;
        private Button _btnDeepClearGrid;
        private Button _btnShowcaseExportExcel;
        private Button _btnShowcaseGenerateZoomClips;
        private Button _btnShowcaseGenerateVoiceover;
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

        public Button DeepGenerateScriptButton => _btnDeepGenerateScript;

        public Button DeepEditScriptButton => null;

        public Button AffiliateDeepOpenOutputButton => null;

        public Button ShowcaseExportExcelButton => _btnShowcaseExportExcel;

        public Button ShowcaseOpenClipsFolderButton => null;

        public Button ShowcaseRefreshClipsButton => null;

        public Button ShowcaseGenerateVoiceoverButton => _btnShowcaseGenerateVoiceover;

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
            SetButtonEnabled(_btnDeepClearGrid, enabled);
            SetButtonEnabled(_btnDeepGenerateScript, enabled);
            SetButtonEnabled(_btnShowcaseExportExcel, enabled);
            SetButtonEnabled(_btnShowcaseGenerateZoomClips, enabled);
            SetButtonEnabled(_btnShowcaseGenerateVoiceover, enabled);
            SetButtonEnabled(_btnShowcasePreviewNarration, enabled);
            SetButtonEnabled(_btnShowcaseListenNarration, enabled);
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
                MinimumSize = new Size(0, Form1.AppJellyButtonHeight + 8),
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

            _btnShowcaseAddVideoRow = CreateShowcaseSolidRectButton(
                "btnShowcaseAddVideoRow",
                "+ Thêm dòng",
                ShowcaseTintAddRow,
                118);
            _btnShowcaseAddVideoRow.Click += (_, __) => _host?.AddShowcaseVideoRow();

            _btnDeepClearGrid = CreateShowcaseSolidRectButton(
                "btnDeepClearAiGenGrid",
                "🗑 Xoá dòng",
                ShowcaseTintDanger,
                108);
            _btnDeepClearGrid.Click += (_, __) => _host?.ClearActiveGrid();

            _btnDeepGenerateScript = CreateShowcaseJellyButton("btnDeepGenerateScript", "📝 Tạo kịch bản", ShowcaseTintScript, 148);
            _btnDeepGenerateScript.Click += async (_, __) => await RunHostAsync(_btnDeepGenerateScript, h => h.GenerateShowcaseSceneScriptAsync()).ConfigureAwait(true);

            _btnShowcaseExportExcel = CreateShowcaseJellyButton("btnShowcaseExportExcel", "Tải excel prompt", ShowcaseTintExcel, 232);
            _btnShowcaseExportExcel.Click += async (_, __) => await RunHostAsync(_btnShowcaseExportExcel, h => h.ExportShowcaseExcelAsync()).ConfigureAwait(true);

            _btnShowcaseGenerateZoomClips = CreateShowcaseJellyButton("btnShowcaseGenerateZoomClips", "⚡ Tạo clip Zoom", ShowcaseTintZoom, 156);
            _btnShowcaseGenerateZoomClips.Click += async (_, __) => await RunHostAsync(_btnShowcaseGenerateZoomClips, h => h.GenerateShowcaseZoomClipsAsync()).ConfigureAwait(true);

            _btnShowcaseGenerateVoiceover = CreateShowcaseJellyButton(
                "btnShowcaseGenerateVoiceover",
                "💬 Tạo lời thoại",
                ShowcaseTintVoiceover,
                152);
            _btnShowcaseGenerateVoiceover.Click += async (_, __) =>
                await RunHostAsync(_btnShowcaseGenerateVoiceover, h => h.GenerateShowcaseVoiceoverAsync()).ConfigureAwait(true);

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
                    if (_btnShowcaseStop != null && !_btnShowcaseStop.IsDisposed)
                    {
                        _btnShowcaseStop.Enabled = true;
                    }
                }
            };

            _btnShowcaseOverview = CreateShowcaseJellyButton(
                "btnShowcaseOverview",
                "👁 Tổng quan",
                Color.FromArgb(72, 118, 198),
                168);

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
                        "Tổng quan",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                await _host.PreviewShowcaseOverviewAsync().ConfigureAwait(true);
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
            _showcaseToolTip.SetToolTip(_btnDeepGenerateScript,
                "Cần ít nhất 1 ảnh trên storyboard. Chọn «Công cụ Video» + «Loại SP · Chủ đề» — Gemini viết Hook/CTA, thoại nháp và prompt clip từ ảnh. "
                + "Thoại chính thức: sau khi có clip → «Tạo lời thoại».");
            _showcaseToolTip.SetToolTip(_btnShowcaseExportExcel,
                "Xuất Excel (sheet «Clip Prompts»: loại ảnh, công cụ, prompt Veo/Kling, zoom gợi ý) — dùng tạo clip bên ngoài app.");
            _showcaseToolTip.SetToolTip(_btnShowcaseGenerateZoomClips,
                "Tạo clip Ken Burns trong app — thời lượng từng cảnh theo clip_duration_seconds (Gemini) / thoại cảnh, không cố định 6s.");
            _showcaseToolTip.SetToolTip(_btnShowcaseGenerateVoiceover,
                "Cần ít nhất 1 clip trong veo_clips (không bắt đủ mọi cảnh). Gemini xem clip có sẵn → viết lại Hook/thoại/CTA khớp hình. "
                + "Bấm lại khi replace clip hoặc bổ sung cảnh thiếu.");
            SetShowcaseNarrationButtonMode(hasExistingNarration: false);
            _showcaseToolTip.SetToolTip(_btnShowcaseListenNarration,
                "Mở file narration.mp3 đã tạo — nghe thử trước khi render (không gọi ElevenLabs lại).");
            _showcaseToolTip.SetToolTip(_btnShowcaseAddVideoRow,
                "Thêm một dòng video mới trên lưới — thêm ảnh bằng cột «Ảnh» (➕ Thêm ảnh) trên từng dòng.");
            _showcaseToolTip.SetToolTip(_btnShowcaseOverview,
                "Bảng trực quan: pipeline, timeline từng cảnh (ảnh + clip + thoại), thoại/phụ đề/nhạc, checklist Render.");
            _showcaseToolTip.SetToolTip(_btnRunAffiliateDeepVideo,
                "Bấm để render — nếu thiếu điều kiện, app liệt kê cụ thể (clip, thoại, TTS, FFmpeg…).");
            _showcaseToolTip.SetToolTip(_btnShowcaseStop,
                "Dừng lại mọi thao tác tab (Gemini, Zoom, audio, render). Sau đó bấm «Tiếp tục» (cam) để mở khóa — không tự chạy lại job đã hủy.");

            // Một dòng: thêm/xoá trái; kịch bản → Zoom → lời thoại → audio căn giữa toolbar
            _flpShowcaseRowManage = CreateActionFlowPanel();
            _flpShowcaseRowManage.Dock = DockStyle.None;
            _flpShowcaseRowManage.WrapContents = false;
            _flpShowcaseRowManage.Controls.Add(_btnShowcaseAddVideoRow);
            _flpShowcaseRowManage.Controls.Add(_btnDeepClearGrid);

            _flpShowcaseWorkflow = CreateActionFlowPanel();
            _flpShowcaseWorkflow.Dock = DockStyle.None;
            _flpShowcaseWorkflow.WrapContents = false;
            _flpShowcaseWorkflow.Controls.Add(_btnDeepGenerateScript);
            _flpShowcaseWorkflow.Controls.Add(_btnShowcaseGenerateZoomClips);
            _flpShowcaseWorkflow.Controls.Add(_btnShowcaseGenerateVoiceover);
            _btnShowcasePreviewNarration.Visible = false;

            _pnlAffiliateDeepHeaderActions.Controls.Add(_flpShowcaseRowManage);
            _pnlAffiliateDeepHeaderActions.Controls.Add(_flpShowcaseWorkflow);
            LayoutShowcaseHeaderToolbar();

            // Hàng 2 — render (nút primary căn giữa)
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
            _flpShowcaseExecute.Controls.Add(_btnShowcaseOverview);
            _flpShowcaseExecute.Controls.Add(_btnRunAffiliateDeepVideo);
            _flpShowcaseExecute.Controls.Add(_btnShowcaseStop);

            tblRenderCenter.Controls.Add(_flpShowcaseExecute, 1, 0);
            _pnlAffiliateDeepExecuteActions.Controls.Add(tblRenderCenter);
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
                || _flpShowcaseRowManage == null
                || _flpShowcaseWorkflow == null)
            {
                return;
            }

            var host = _pnlAffiliateDeepHeaderActions;
            if (host.Width <= 0 || host.Height <= 0)
            {
                return;
            }

            _flpShowcaseRowManage.PerformLayout();
            _flpShowcaseWorkflow.PerformLayout();

            var manageY = Math.Max(0, (host.ClientSize.Height - _flpShowcaseRowManage.Height) / 2);
            _flpShowcaseRowManage.Location = new Point(0, manageY);

            var workflowY = Math.Max(0, (host.ClientSize.Height - _flpShowcaseWorkflow.Height) / 2);
            var workflowX = Math.Max(0, (host.ClientSize.Width - _flpShowcaseWorkflow.Width) / 2);
            _flpShowcaseWorkflow.Location = new Point(workflowX, workflowY);

            _flpShowcaseRowManage.BringToFront();
            _flpShowcaseWorkflow.BringToFront();
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

        /// <summary>Nút chữ nhật đặc (không jelly trong suốt) — dùng cho Thêm/Xoá dòng.</summary>
        private static Button CreateShowcaseSolidRectButton(string name, string text, Color back, int minWidth)
        {
            var height = Form1.AppJellyButtonHeight;
            var textW = TextRenderer.MeasureText(
                text,
                JellyFont,
                new Size(int.MaxValue, height),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            var width = Math.Max(minWidth, textW + 28);

            var btn = new Button
            {
                Name = name,
                Text = text,
                Font = JellyFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Height = height,
                Width = width,
                MinimumSize = new Size(width, height),
                MaximumSize = new Size(width, height),
                Margin = ShowcaseJellyMargin,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };

            var border = ControlPaint.Dark(back);
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
}
