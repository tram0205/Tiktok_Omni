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

        private IAiVideoGenControlsHost _host;

        private FlowLayoutPanel _flpSlideshowData;
        private FlowLayoutPanel _flpSlideshowExecute;
        private Panel _pnlSlideshowActionBar;
        private FlowLayoutPanel _flpAffiliateDeepHeaderActions;

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
        private Button _btnDeepEditScript;
        private Button _btnRunAffiliateDeepVideo;
        private Button _btnAffiliateDeepOpenOutput;
        private Button _btnDeepClearGrid;
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

        public FlowLayoutPanel AffiliateDeepHeaderActions => _flpAffiliateDeepHeaderActions;

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

        public Button DeepGenerateScriptButton => _btnDeepGenerateScript;

        public Button DeepEditScriptButton => _btnDeepEditScript;

        public Button AffiliateDeepOpenOutputButton => _btnAffiliateDeepOpenOutput;

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

            _btnClearSlideshowGrid = CreateThemedUtilityButton("btnClearAiGenGrid", "🗑 Làm sạch", ButtonRole.Danger);
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
                Margin = new Padding(8, 8, 0, 0)
            };
            _flpSlideshowData.Controls.Add(_chkUseMultiVoiceNarration);

            _numAiTextSize = CreateNumeric("numAiTextSize", 24, 96, 50);
            _numAiMusicVolume = CreateNumeric("numAiMusicVolume", 0, 100, 14);
            _numAiTransitionDuration = CreateNumericDecimal("numAiTransitionDuration", 0.2M, 2.0M, 0.6M);

            _flpSlideshowExecute.Controls.Add(_btnAffiliateBatchPipeline);
            _flpSlideshowExecute.Controls.Add(_btnProcessVideo);
            _flpSlideshowExecute.Controls.Add(_btnOpenOutputFolder);
            _flpSlideshowExecute.Controls.Add(_btnSlideshowOpenApproval);
            _flpSlideshowExecute.Controls.Add(CreateMetricLabel("Cỡ chữ"));
            _flpSlideshowExecute.Controls.Add(_numAiTextSize);
            _flpSlideshowExecute.Controls.Add(CreateMetricLabel("Âm lượng nhạc", leftPad: 8));
            _flpSlideshowExecute.Controls.Add(_numAiMusicVolume);
            _flpSlideshowExecute.Controls.Add(CreateMetricLabel("Chuyển cảnh (s)", leftPad: 8));
            _flpSlideshowExecute.Controls.Add(_numAiTransitionDuration);

            _pnlSlideshowActionBar = BuildModeActionPanel(_flpSlideshowData, _flpSlideshowExecute);

            _flpAffiliateDeepHeaderActions = CreateActionFlowPanel();
            _btnDeepGenerateScript = CreateToolbarButton("Sinh Script", false, 110);
            _btnDeepGenerateScript.Name = "btnDeepGenerateScript";
            _btnDeepGenerateScript.BackColor = Color.FromArgb(78, 120, 166);
            _btnDeepGenerateScript.Click += async (_, __) => await RunHostAsync(_btnDeepGenerateScript, h => h.GenerateAffiliateScriptAsync()).ConfigureAwait(true);

            _btnDeepEditScript = CreateToolbarButton("Sửa Script", false, 100);
            _btnDeepEditScript.Name = "btnDeepEditScript";
            _btnDeepEditScript.BackColor = Color.FromArgb(60, 64, 77);
            _btnDeepEditScript.Click += async (_, __) => await RunHostAsync(_btnDeepEditScript, h => h.EditAffiliateScriptAsync()).ConfigureAwait(true);

            _btnRunAffiliateDeepVideo = CreateToolbarButton("Render Affiliate chuyên sâu", true, 200);
            _btnRunAffiliateDeepVideo.Name = "btnRunAffiliateDeepVideo";
            _btnRunAffiliateDeepVideo.Click += async (_, __) =>
            {
                if (_host == null)
                {
                    return;
                }

                _btnRunAffiliateDeepVideo.Enabled = false;
                await _host.RunAffiliateDeepVideoAsync().ConfigureAwait(true);
            };

            _btnAffiliateDeepOpenOutput = CreateThemedUtilityButton("btnAffiliateDeepOpenOutput", "📂 Output", ButtonRole.Neutral);
            _btnAffiliateDeepOpenOutput.Click += async (_, __) => await RunHostAsync(_btnAffiliateDeepOpenOutput, h => h.OpenAffiliateDeepOutputFolderAsync()).ConfigureAwait(true);

            _btnDeepClearGrid = CreateThemedUtilityButton("btnDeepClearAiGenGrid", "🗑 Làm sạch", ButtonRole.Danger);
            _btnDeepClearGrid.Click += (_, __) => _host?.ClearActiveGrid();

            _flpAffiliateDeepHeaderActions.Controls.Add(_btnDeepGenerateScript);
            _flpAffiliateDeepHeaderActions.Controls.Add(_btnDeepEditScript);
            _flpAffiliateDeepHeaderActions.Controls.Add(_btnRunAffiliateDeepVideo);
            _flpAffiliateDeepHeaderActions.Controls.Add(_btnAffiliateDeepOpenOutput);
            _flpAffiliateDeepHeaderActions.Controls.Add(_btnDeepClearGrid);
        }

        private async Task RunHostAsync(Button button, Func<IAiVideoGenControlsHost, Task> action)
        {
            if (_host == null || button == null || action == null)
            {
                return;
            }

            button.Enabled = false;
            try
            {
                await action(_host).ConfigureAwait(true);
            }
            finally
            {
                if (button != null && !button.IsDisposed)
                {
                    button.Enabled = true;
                }
            }
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
