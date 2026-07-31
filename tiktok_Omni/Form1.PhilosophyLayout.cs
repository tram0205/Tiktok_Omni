using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Helpers;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font PhilosophyUiFont = AppLabelFont;
        private static readonly Font PhilosophyPrimaryActionFont = AppPrimaryActionFont;
        private static readonly Font PhilosophyCommandFont = AppJellyButtonFont;
        private static readonly Color PhilosophyPanelBack = Color.FromArgb(31, 34, 42);
        private static readonly Color PhilosophyChromeBack = Color.FromArgb(36, 39, 48);
        private static readonly Color PhilosophyConfigBarBack = Color.FromArgb(33, 36, 44);
        private static readonly Color PhilosophyTintGenerate = Color.FromArgb(76, 110, 245);
        private static readonly Color PhilosophyTintRender = Color.FromArgb(56, 158, 88);
        private static readonly Color PhilosophyTintSecondary = Color.FromArgb(88, 94, 112);
        private static readonly Color PhilosophyTintStop = Color.FromArgb(170, 72, 72);
        private static readonly Color PhilosophyTintResume = Color.FromArgb(68, 118, 178);
        private static readonly Padding PhilosophyFlowItemMargin = new Padding(4, 4, 10, 6);
        private static readonly Padding PhilosophyFlowSectionMargin = new Padding(0, 0, 18, 4);
        private const int PhilosophyCommandButtonHeight = AppJellyButtonHeight;
        private const int PhilosophyPrimaryActionHeight = AppPrimaryActionHeight;
        private const int PhilosophyCommandHorizontalPad = AppJellyButtonHorizontalPad;

        private Panel pnlPhilosophyTopChrome;
        private Panel pnlPhilosophyCommandBar;
        private Panel pnlPhilosophyConfigToolbar;
        private Panel pnlPhilosophyMainFill;
        private Panel pnlPhilosophyGridWrap;
        private Panel pnlPhilosophyRenderHost;
        private Panel pnlPhilosophyStatus;
        private RadioButton rbPhilosophyModeQuotes;
        private RadioButton rbPhilosophyModeStory;
        private TextBox txtPhilosophyTopic;
        private NumericUpDown numPhilosophyCount;
        private Button btnPhilosophyGenerateScript;
        private Button btnPhilosophyAddRow;
        private Button btnPhilosophyDeleteRow;
        private DataGridView dgvPhilosophyScripts;
        private BindingList<PhilosophyScriptItem> _philosophyScriptBindingList;
        private DataGridViewComboBoxColumn _colPhilosophyMusic;
        private DataGridViewComboBoxColumn _colPhilosophyAmbient;
        private DataGridViewComboBoxColumn _colPhilosophyProfile;
        private DataGridViewComboBoxColumn _colPhilosophyVisualMode;
        private DataGridViewButtonColumn _colPhilosophyMusicBrowse;
        private DataGridViewButtonColumn _colPhilosophySceneVideoBrowse;
        private Button btnPhilosophyStartRender;
        private Button btnPhilosophyStopRender;
        private Button btnGenerateScenePrompts;
        private Button btnExportExcelPrompts;
        private Button btnPhilosophyStopAll;
        private TextBox txtPhilosophyVideoInputFolder;
        private Button btnBrowseVideoInput;
        private Button btnOpenPhilosophyVideoFolder;
        private CancellationTokenSource _philosophyScriptGenCts;
        private CancellationTokenSource _philosophyScenePromptCts;
        private bool _philosophyScriptGenRunning;
        private bool _philosophyScenePromptRunning;
        private NumericUpDown numPhilosophyDurationMin;
        private NumericUpDown numPhilosophyDurationMax;
        private CancellationTokenSource _philosophyRenderCts;
        private bool _philosophyRenderRunning;
        private bool _philosophyRenderPaused;
        private List<PhilosophyScriptItem> _philosophyRenderPending;
        private ContextMenuStrip _cmsPhilosophyGrid;
        private ContextMenuStrip _cmsPhilosophyBRollPicker;
        private PhilosophyScriptItem _philosophyBRollPickerItem;
        private int _philosophyBRollPickerRowIndex = -1;
        private ToolStripMenuItem _miPhilosophyBRollRandom;
        private ToolStripMenuItem _miPhilosophyBRollBrowse;

        /// <summary>Tab Video Triết lý — layout giống Video reup (Top toolbar / Fill grid / Bottom actions + log).</summary>
        public void InitializePhilosophyControls(Panel modePage, Panel progressBand)
        {
            if (modePage == null)
            {
                return;
            }

            EnsurePhilosophyGridCreated();
            BuildPhilosophyTopChrome();
            BuildPhilosophyRenderActionBar();
            BuildPhilosophyMainGrid();
            BuildPhilosophyStatusPanel();
            WirePhilosophyTabLayout(modePage, progressBand);
            InitializePhilosophyDraftAutoSave();
            RefreshPhilosophyFolderOptions();
            EnsurePhilosophyVideoInputFolderDefault();
        }

        private void WirePhilosophyTabLayout(Panel modePage, Panel progressBand)
        {
            modePage.SuspendLayout();
            try
            {
                modePage.Padding = new Padding(4);
                modePage.AutoScroll = false;

                if (progressBand != null)
                {
                    progressBand.Visible = false;
                }

                pnlPhilosophyMainFill = new Panel
                {
                    Name = "pnlPhilosophyMainFill",
                    BackColor = PhilosophyPanelBack,
                    Padding = new Padding(0, 2, 0, 0)
                };

                ApplyTopFillBottomDockLayout(pnlPhilosophyMainFill, pnlPhilosophyGridWrap, bottom: null, top: null);

                pnlPhilosophyRenderHost.Dock = DockStyle.Bottom;
                pnlPhilosophyStatus.Dock = DockStyle.Bottom;
                pnlPhilosophyTopChrome.Dock = DockStyle.Top;

                pnlPhilosophyMainFill.Dock = DockStyle.Fill;
                pnlPhilosophyMainFill.Margin = Padding.Empty;
                pnlPhilosophyRenderHost.Margin = Padding.Empty;
                pnlPhilosophyStatus.Margin = Padding.Empty;
                pnlPhilosophyTopChrome.Margin = Padding.Empty;

                modePage.Controls.Clear();
                modePage.Controls.Add(pnlPhilosophyMainFill);
                modePage.Controls.Add(pnlPhilosophyRenderHost);
                modePage.Controls.Add(pnlPhilosophyStatus);
                modePage.Controls.Add(pnlPhilosophyTopChrome);
            }
            finally
            {
                modePage.ResumeLayout(true);
            }
        }

        private void EnsurePhilosophyGridCreated()
        {
            if (dgvPhilosophyScripts != null && !dgvPhilosophyScripts.IsDisposed)
            {
                return;
            }

            _philosophyScriptBindingList ??= new BindingList<PhilosophyScriptItem>();
            dgvPhilosophyScripts = new DataGridView
            {
                Name = "dgvPhilosophyScripts",
                DataSource = _philosophyScriptBindingList,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                ScrollBars = ScrollBars.Vertical,
                BackgroundColor = PhilosophyPanelBack,
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                Font = AppGridBodyFont,
                DefaultCellStyle =
                {
                    BackColor = PhilosophyPanelBack,
                    ForeColor = Color.Gainsboro,
                    SelectionBackColor = Color.FromArgb(76, 110, 245),
                    SelectionForeColor = Color.White,
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            ConfigurePhilosophyScriptGrid();
            WirePhilosophyGridEvents();
            ApplyGridProfileComboColumn(dgvPhilosophyScripts, "colPhilosophyProfile");
            // Có nhiều ComboBox trong ô — dùng chiều cao combo chuẩn.
            ApplyAppComboGridRowHeight(dgvPhilosophyScripts);
            ApplyAppGridChrome(dgvPhilosophyScripts);
        }

        private void BuildPhilosophyMainGrid()
        {
            pnlPhilosophyGridWrap = new Panel
            {
                Name = "pnlPhilosophyGridWrap",
                Padding = new Padding(4),
                BackColor = PhilosophyPanelBack
            };

            if (lblPhilosophyPrereq == null || lblPhilosophyPrereq.IsDisposed)
            {
                lblPhilosophyPrereq = new Label
                {
                    Name = "lblPhilosophyPrereq",
                    Text = "Đang kiểm tra cấu hình…",
                    AutoSize = false,
                    Height = 28,
                    ForeColor = Color.FromArgb(255, 180, 120),
                    Font = PhilosophyUiFont,
                    Padding = new Padding(4, 4, 4, 2)
                };
            }

            lblPhilosophyPrereq.AutoSize = false;
            lblPhilosophyPrereq.Height = 28;
            dgvPhilosophyScripts.Margin = Padding.Empty;
            dgvPhilosophyScripts.MinimumSize = new Size(120, 80);
            dgvPhilosophyScripts.Dock = DockStyle.Fill;

            ApplyTopFillBottomDockLayout(pnlPhilosophyGridWrap, dgvPhilosophyScripts, bottom: null, lblPhilosophyPrereq);
            ApplyAppGridChrome(dgvPhilosophyScripts);
        }

        private void BuildPhilosophyTopChrome()
        {
            BuildPhilosophyCommandBar();
            BuildPhilosophyConfigToolbar();

            pnlPhilosophyTopChrome = new Panel
            {
                Name = "pnlPhilosophyTopChrome",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = PhilosophyChromeBack
            };

            pnlPhilosophyConfigToolbar.Dock = DockStyle.Top;
            pnlPhilosophyCommandBar.Dock = DockStyle.Top;
            pnlPhilosophyTopChrome.Controls.Add(pnlPhilosophyConfigToolbar);
            pnlPhilosophyTopChrome.Controls.Add(pnlPhilosophyCommandBar);
        }

        private void BuildPhilosophyCommandBar()
        {
            pnlPhilosophyCommandBar = CreatePhilosophyAutoSizeBar("pnlPhilosophyCommandBar", PhilosophyChromeBack);
            var flp = CreatePhilosophyWrapFlowPanel("flpPhilosophyCommand", PhilosophyChromeBack);

            btnPhilosophyAddRow = CreatePhilosophyCommandButton("btnPhilosophyAddRow", "+ Thêm hàng", Color.FromArgb(55, 95, 160));
            btnPhilosophyAddRow.Click -= btnPhilosophyAddRow_Click;
            btnPhilosophyAddRow.Click += btnPhilosophyAddRow_Click;
            btnPhilosophyDeleteRow = CreatePhilosophyCommandButton("btnPhilosophyDeleteRow", "Xóa hàng", PhilosophyTintSecondary);
            btnPhilosophyDeleteRow.Click -= btnPhilosophyDeleteRow_Click;
            btnPhilosophyDeleteRow.Click += btnPhilosophyDeleteRow_Click;

            // Nút Tạo Prompt Phân Cảnh
            if (btnGenerateScenePrompts == null || btnGenerateScenePrompts.IsDisposed)
            {
                btnGenerateScenePrompts = CreatePhilosophyCommandButton(
                    "btnGenerateScenePrompts", "Tạo Prompt Phân Cảnh", Color.FromArgb(100, 72, 190));
            }

            btnGenerateScenePrompts.Click -= BtnGenerateScenePrompts_Click;
            btnGenerateScenePrompts.Click += BtnGenerateScenePrompts_Click;

            // Nút Xuất Excel / CSV
            if (btnExportExcelPrompts == null || btnExportExcelPrompts.IsDisposed)
            {
                btnExportExcelPrompts = CreatePhilosophyCommandButton(
                    "btnExportExcelPrompts", "Tải Excel Prompt", PhilosophyTintSecondary);
            }

            btnExportExcelPrompts.Click -= BtnExportExcelPrompts_Click;
            btnExportExcelPrompts.Click += BtnExportExcelPrompts_Click;

            if (btnPhilosophyStopAll == null || btnPhilosophyStopAll.IsDisposed)
            {
                btnPhilosophyStopAll = CreatePhilosophyCommandButton(
                    "btnPhilosophyStopAll", "Dừng", PhilosophyTintStop);
                btnPhilosophyStopAll.Visible = false;
            }

            btnPhilosophyStopAll.Click -= BtnPhilosophyStopAll_Click;
            btnPhilosophyStopAll.Click += BtnPhilosophyStopAll_Click;

            flp.Controls.Add(btnPhilosophyAddRow);
            flp.Controls.Add(btnPhilosophyDeleteRow);

            // Separator
            flp.Controls.Add(new Label
            {
                Text = "|",
                AutoSize = true,
                ForeColor = Color.FromArgb(70, 75, 90),
                Margin = new Padding(4, 10, 4, 0)
            });

            flp.Controls.Add(btnGenerateScenePrompts);
            flp.Controls.Add(btnExportExcelPrompts);
            flp.Controls.Add(btnPhilosophyStopAll);

            // Thư mục video phân cảnh tự làm (mode 3)
            flp.Controls.Add(new Label
            {
                Text = "Thư mục video:",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(12, 10, 4, 0)
            });

            if (txtPhilosophyVideoInputFolder == null || txtPhilosophyVideoInputFolder.IsDisposed)
            {
                txtPhilosophyVideoInputFolder = new TextBox
                {
                    Name = "txtPhilosophyVideoInputFolder",
                    Text = string.Empty,
                    Width = 200,
                    Height = 26,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Font = PhilosophyUiFont
                };
            }

            ApplyPhilosophyFlowMargin(txtPhilosophyVideoInputFolder, top: 8);

            if (btnBrowseVideoInput == null || btnBrowseVideoInput.IsDisposed)
            {
                btnBrowseVideoInput = CreatePhilosophyCommandButton(
                    "btnBrowseVideoInput", "Chọn thư mục Video", PhilosophyTintSecondary);
                btnBrowseVideoInput.Width = 140;
            }

            btnBrowseVideoInput.Click -= BtnBrowseVideoInput_Click;
            btnBrowseVideoInput.Click += BtnBrowseVideoInput_Click;

            if (btnOpenPhilosophyVideoFolder == null || btnOpenPhilosophyVideoFolder.IsDisposed)
            {
                btnOpenPhilosophyVideoFolder = CreatePhilosophyCommandButton(
                    "btnOpenPhilosophyVideoFolder", "Mở thư mục", Color.FromArgb(55, 95, 160));
                btnOpenPhilosophyVideoFolder.Width = 110;
            }

            btnOpenPhilosophyVideoFolder.Click -= BtnOpenPhilosophyVideoFolder_Click;
            btnOpenPhilosophyVideoFolder.Click += BtnOpenPhilosophyVideoFolder_Click;

            flp.Controls.Add(txtPhilosophyVideoInputFolder);
            flp.Controls.Add(btnBrowseVideoInput);
            flp.Controls.Add(btnOpenPhilosophyVideoFolder);

            pnlPhilosophyCommandBar.Controls.Add(flp);
        }

        private void BtnBrowseVideoInput_Click(object sender, EventArgs e)
        {
            var defaultDir = ResolvePhilosophyPreRenderedScenesDirectory();
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Chọn thư mục chứa video phân cảnh (mode 3).\r\nMặc định: Assets\\{profile}\\philosophy-scenes\\",
                SelectedPath = Directory.Exists(defaultDir) ? defaultDir : string.Empty,
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (txtPhilosophyVideoInputFolder != null && !txtPhilosophyVideoInputFolder.IsDisposed)
                    {
                        txtPhilosophyVideoInputFolder.Text = dlg.SelectedPath;
                        NotifyPhilosophyDraftDirty();
                    }
                }
            }
        }

        private void BtnOpenPhilosophyVideoFolder_Click(object sender, EventArgs e)
        {
            var dir = ResolvePhilosophyPreRenderedScenesDirectory();
            try
            {
                Directory.CreateDirectory(dir);
                if (txtPhilosophyVideoInputFolder != null && !txtPhilosophyVideoInputFolder.IsDisposed)
                {
                    txtPhilosophyVideoInputFolder.Text = dir;
                    NotifyPhilosophyDraftDirty();
                }

                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                LogPhilosophy("Thư mục video phân cảnh: " + dir);
                LogPhilosophy("Copy file .mp4 vào đây — tên theo cột «Prompt video» (vd: tam-bat-bien-giua-dong-01.mp4).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không mở được thư mục:\r\n" + ex.Message, "Video Triết lý",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Assets\{profile}\philosophy-scenes\ — tạo nếu chưa có.</summary>
        private string ResolvePhilosophyPreRenderedScenesDirectory()
        {
            var profile = GetSelectedPhilosophyProfileName();
            return PhilosophyProfileAssets.EnsurePreRenderedScenesDirectory(profile);
        }

        private void EnsurePhilosophyVideoInputFolderDefault()
        {
            if (txtPhilosophyVideoInputFolder == null || txtPhilosophyVideoInputFolder.IsDisposed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(txtPhilosophyVideoInputFolder.Text))
            {
                return;
            }

            txtPhilosophyVideoInputFolder.Text = ResolvePhilosophyPreRenderedScenesDirectory();
        }

        private void UpdatePhilosophyGenerateScriptButtonState()
        {
            if (btnPhilosophyGenerateScript == null || btnPhilosophyGenerateScript.IsDisposed)
            {
                return;
            }

            btnPhilosophyGenerateScript.Enabled = IsPhilosophyScriptGenerationReady();
        }

        private void BuildPhilosophyConfigToolbar()
        {
            pnlPhilosophyConfigToolbar = CreatePhilosophyAutoSizeBar("pnlPhilosophyConfigToolbar", PhilosophyConfigBarBack);
            var flpRoot = CreatePhilosophyWrapFlowPanel("flpPhilosophyConfigRoot", PhilosophyConfigBarBack);

            var grpTopic = CreatePhilosophyToolGroup("Chủ đề:", PhilosophyConfigBarBack);
            txtPhilosophyTopic = new TextBox
            {
                Name = "txtPhilosophyTopic",
                Text = "Sự cố gắng",
                Width = 168,
                Height = 26,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = PhilosophyUiFont
            };
            ApplyPhilosophyFlowMargin(txtPhilosophyTopic, top: 8);
            grpTopic.Controls.Add(txtPhilosophyTopic);

            rbPhilosophyModeQuotes = CreatePhilosophyRadio("Quotes", true);
            rbPhilosophyModeStory = CreatePhilosophyRadio("Story", false);
            rbPhilosophyModeQuotes.CheckedChanged += PhilosophyModeRadio_CheckedChanged;
            rbPhilosophyModeStory.CheckedChanged += PhilosophyModeRadio_CheckedChanged;
            ApplyPhilosophyFlowMargin(rbPhilosophyModeQuotes, top: 10);
            ApplyPhilosophyFlowMargin(rbPhilosophyModeStory, top: 10);
            grpTopic.Controls.Add(rbPhilosophyModeQuotes);
            grpTopic.Controls.Add(rbPhilosophyModeStory);

            numPhilosophyCount = new NumericUpDown
            {
                Name = "numPhilosophyCount",
                Minimum = 1,
                Maximum = 20,
                Value = 5,
                Width = 48,
                Height = 26,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = PhilosophyUiFont
            };
            ApplyPhilosophyFlowMargin(numPhilosophyCount, top: 8);
            grpTopic.Controls.Add(new Label
            {
                Text = "SL:",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 10, 4, 0)
            });
            grpTopic.Controls.Add(numPhilosophyCount);

            var grpProfile = CreatePhilosophyToolGroup("Profile:", PhilosophyConfigBarBack);
            if (cbPhilosophyProfile == null || cbPhilosophyProfile.IsDisposed)
            {
                cbPhilosophyProfile = new ComboBox
                {
                    Name = "cbPhilosophyProfile",
                    Width = 120,
                    Height = 26,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Font = PhilosophyUiFont
                };
                cbPhilosophyProfile.Items.Add("default");
                cbPhilosophyProfile.SelectedIndex = 0;
            }

            cbPhilosophyProfile.SelectedIndexChanged -= PhilosophyProfile_SelectedIndexChanged;
            cbPhilosophyProfile.SelectedIndexChanged += PhilosophyProfile_SelectedIndexChanged;
            ApplyPhilosophyFlowMargin(cbPhilosophyProfile, top: 8);
            grpProfile.Controls.Add(cbPhilosophyProfile);

            if (numPhilosophyDurationMin == null || numPhilosophyDurationMin.IsDisposed)
            {
                numPhilosophyDurationMin = CreatePhilosophyDurationSpinner("numPhilosophyDurationMin", 15);
            }

            if (numPhilosophyDurationMax == null || numPhilosophyDurationMax.IsDisposed)
            {
                numPhilosophyDurationMax = CreatePhilosophyDurationSpinner("numPhilosophyDurationMax", 60);
            }

            grpProfile.Controls.Add(new Label
            {
                Name = "lblPhilosophyDuration",
                Text = "Thời lượng Gemini:",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(12, 10, 4, 0)
            });
            ApplyPhilosophyFlowMargin(numPhilosophyDurationMin, top: 8);
            grpProfile.Controls.Add(numPhilosophyDurationMin);
            grpProfile.Controls.Add(new Label
            {
                Name = "lblPhilosophyDurationTo",
                Text = "đến",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 10, 4, 0)
            });
            ApplyPhilosophyFlowMargin(numPhilosophyDurationMax, top: 8);
            grpProfile.Controls.Add(numPhilosophyDurationMax);
            grpProfile.Controls.Add(new Label
            {
                Name = "lblPhilosophyDurationUnit",
                Text = "giây",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 10, 12, 0)
            });

            if (btnPhilosophyGenerateScript == null || btnPhilosophyGenerateScript.IsDisposed)
            {
                btnPhilosophyGenerateScript = CreatePhilosophyCommandButton(
                    "btnPhilosophyGenerateScript",
                    "Tạo câu triết lý",
                    PhilosophyTintGenerate);
            }

            btnPhilosophyGenerateScript.Text = "Tạo câu triết lý";
            btnPhilosophyGenerateScript.Click -= btnPhilosophyGenerateScript_Click;
            btnPhilosophyGenerateScript.Click += btnPhilosophyGenerateScript_Click;
            ApplyPhilosophyCommandButtonMetrics(btnPhilosophyGenerateScript);
            ApplyPhilosophyFlowMargin(btnPhilosophyGenerateScript, top: 8);
            grpProfile.Controls.Add(btnPhilosophyGenerateScript);
            UpdatePhilosophyGenerateScriptButtonState();

            flpRoot.Controls.Add(grpTopic);
            flpRoot.Controls.Add(grpProfile);
            pnlPhilosophyConfigToolbar.Controls.Add(flpRoot);
        }

        private void BuildPhilosophyRenderActionBar()
        {
            if (btnPhilosophyOpenAssets == null || btnPhilosophyOpenAssets.IsDisposed)
            {
                btnPhilosophyOpenAssets = CreatePhilosophyCommandButton("btnPhilosophyOpenAssets", "Mở Assets", PhilosophyTintSecondary);
            }

            btnPhilosophyOpenAssets.Click -= btnPhilosophyOpenAssets_Click;
            btnPhilosophyOpenAssets.Click += btnPhilosophyOpenAssets_Click;

            if (btnPhilosophyStartRender == null || btnPhilosophyStartRender.IsDisposed)
            {
                btnPhilosophyStartRender = CreateAppPrimaryJellyButton(
                    "btnPhilosophyStartRender",
                    "Bắt đầu Render",
                    PhilosophyTintRender);
            }

            btnPhilosophyStartRender.Click -= btnPhilosophyStartRender_Click;
            btnPhilosophyStartRender.Click += btnPhilosophyStartRender_Click;

            if (btnPhilosophyStopRender == null || btnPhilosophyStopRender.IsDisposed)
            {
                btnPhilosophyStopRender = CreatePhilosophyCommandButton("btnPhilosophyStopRender", "Dừng render", PhilosophyTintStop);
            }

            btnPhilosophyStopRender.Click -= btnPhilosophyStopRender_Click;
            btnPhilosophyStopRender.Click += btnPhilosophyStopRender_Click;

            if (btnPhilosophyPushToAutoPost == null || btnPhilosophyPushToAutoPost.IsDisposed)
            {
                btnPhilosophyPushToAutoPost = CreatePhilosophyCommandButton(
                    "btnPhilosophyPushToAutoPost",
                    "Đẩy sang Đăng tự động",
                    Color.FromArgb(68, 130, 105));
            }

            btnPhilosophyPushToAutoPost.Click -= btnPhilosophyPushToAutoPost_Click;
            btnPhilosophyPushToAutoPost.Click += btnPhilosophyPushToAutoPost_Click;

            ApplyPhilosophyCommandButtonMetrics(btnPhilosophyOpenAssets);
            ApplyPhilosophyCommandButtonMetrics(btnPhilosophyStopRender);
            ApplyPhilosophyCommandButtonMetrics(btnPhilosophyPushToAutoPost);
            btnPhilosophyPushToAutoPost.Width = 196;
            btnPhilosophyPushToAutoPost.MaximumSize = new Size(196, PhilosophyCommandButtonHeight);

            ResizeAppJellyButton(
                btnPhilosophyStartRender,
                PhilosophyPrimaryActionHeight,
                AppPrimaryActionMinWidth,
                AppPrimaryActionHorizontalPad);
            btnPhilosophyStartRender.Font = PhilosophyPrimaryActionFont;
            btnPhilosophyStartRender.Margin = new Padding(0, 8, 6, 0);

            btnPhilosophyOpenAssets.Margin = new Padding(0, 8, 10, 0);

            btnPhilosophyStopRender.Text = "Dừng";
            ResizeAppJellyButton(
                btnPhilosophyStopRender,
                PhilosophyPrimaryActionHeight,
                140,
                AppPrimaryActionHorizontalPad);
            btnPhilosophyStopRender.Font = PhilosophyPrimaryActionFont;
            btnPhilosophyStopRender.Margin = new Padding(0, 8, 0, 0);
            btnPhilosophyStopRender.Visible = true;
            btnPhilosophyStopRender.Enabled = false;

            var barColor = Color.FromArgb(28, 30, 38);
            pnlPhilosophyRenderHost = new Panel
            {
                Name = "pnlPhilosophyRenderHost",
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8),
                BackColor = barColor
            };

            var flpPhilosophyRenderActions = new FlowLayoutPanel
            {
                Name = "flpPhilosophyRenderActions",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = barColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            flpPhilosophyRenderActions.Controls.Add(btnPhilosophyOpenAssets);
            flpPhilosophyRenderActions.Controls.Add(btnPhilosophyStartRender);
            flpPhilosophyRenderActions.Controls.Add(btnPhilosophyStopRender);
            flpPhilosophyRenderActions.Controls.Add(btnPhilosophyPushToAutoPost);

            pnlPhilosophyRenderHost.Controls.Add(flpPhilosophyRenderActions);
            UpdatePhilosophyRenderControlStates();
        }

        private static NumericUpDown CreatePhilosophyDurationSpinner(string name, int defaultValue)
        {
            return new NumericUpDown
            {
                Name = name,
                Minimum = 5,
                Maximum = 180,
                Value = Math.Max(5, Math.Min(180, defaultValue)),
                Width = 52,
                Height = 26,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = PhilosophyUiFont,
                Margin = new Padding(0, 8, 4, 0)
            };
        }

        private void BuildPhilosophyStatusPanel()
        {
            if (btnPhilosophyClearLog == null || btnPhilosophyClearLog.IsDisposed)
            {
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
            }

            btnPhilosophyClearLog.Click -= btnPhilosophyClearLog_Click;
            btnPhilosophyClearLog.Click += btnPhilosophyClearLog_Click;

            if (rtbPhilosophyLog == null || rtbPhilosophyLog.IsDisposed)
            {
                rtbPhilosophyLog = CreateAiModeLogTextBox("rtbPhilosophyLog");
                ApplyAiModeLogLineSpacing(rtbPhilosophyLog);
            }

            rtbPhilosophyLog.Dock = DockStyle.Fill;
            rtbPhilosophyLog.Font = new Font("Consolas", 8.25F);

            EnsurePhilosophyProgressControls();

            pnlPhilosophyStatus = new Panel
            {
                Name = "pnlPhilosophyStatus",
                Dock = DockStyle.Bottom,
                Height = 140,
                MinimumSize = new Size(0, 120),
                BackColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };

            var pnlPhilosophyProgressHost = new Panel
            {
                Name = "pnlPhilosophyProgressHost",
                Dock = DockStyle.Bottom,
                Height = 36,
                MinimumSize = new Size(0, 30),
                Padding = new Padding(0, 2, 0, 0),
                BackColor = pnlPhilosophyStatus.BackColor
            };

            var tblProgress = new TableLayoutPanel
            {
                Name = "tblPhilosophyProgress",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblProgress.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tblProgress.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));

            lblPhilosophyProgress.Dock = DockStyle.Fill;
            lblPhilosophyProgress.Margin = Padding.Empty;
            lblPhilosophyProgress.Height = 20;
            lblPhilosophyProgress.AutoSize = false;
            lblPhilosophyProgress.TextAlign = ContentAlignment.MiddleLeft;
            lblPhilosophyProgress.ForeColor = Color.FromArgb(200, 204, 214);

            pbPhilosophyProgress.Dock = DockStyle.Fill;
            pbPhilosophyProgress.Margin = new Padding(0, 2, 0, 0);
            pbPhilosophyProgress.Height = 14;

            tblProgress.Controls.Add(lblPhilosophyProgress, 0, 0);
            tblProgress.Controls.Add(pbPhilosophyProgress, 0, 1);
            pnlPhilosophyProgressHost.Controls.Add(tblProgress);

            var tblLog = new TableLayoutPanel
            {
                Name = "tblPhilosophyLog",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            tblLog.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            tblLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 4, 0) };
            var lblLog = new Label
            {
                Text = "Nhật ký",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(200, 204, 214),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = PhilosophyUiFont
            };
            btnPhilosophyClearLog.Dock = DockStyle.Right;
            btnPhilosophyClearLog.Width = 80;
            btnPhilosophyClearLog.MinimumSize = new Size(80, 26);
            btnPhilosophyClearLog.Margin = new Padding(4, 0, 0, 0);
            header.Controls.Add(lblLog);
            header.Controls.Add(btnPhilosophyClearLog);

            tblLog.Controls.Add(header, 0, 0);
            tblLog.Controls.Add(rtbPhilosophyLog, 0, 1);

            pnlPhilosophyStatus.Controls.Add(pnlPhilosophyProgressHost);
            pnlPhilosophyStatus.Controls.Add(tblLog);
        }

        private void EnsurePhilosophyProgressControls()
        {
            if (lblPhilosophyProgress == null || lblPhilosophyProgress.IsDisposed)
            {
                lblPhilosophyProgress = new Label
                {
                    Name = "lblPhilosophyProgress",
                    Text = "Tiến trình: sẵn sàng"
                };
            }

            if (pbPhilosophyProgress == null || pbPhilosophyProgress.IsDisposed)
            {
                pbPhilosophyProgress = new ProgressBar { Name = "pbPhilosophyProgress" };
            }

            if (lblPhilosophyProgress.Parent != null)
            {
                lblPhilosophyProgress.Parent.Controls.Remove(lblPhilosophyProgress);
            }

            if (pbPhilosophyProgress.Parent != null)
            {
                pbPhilosophyProgress.Parent.Controls.Remove(pbPhilosophyProgress);
            }
        }

        private static Panel CreatePhilosophyAutoSizeBar(string name, Color backColor)
        {
            return new Panel
            {
                Name = name,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = backColor
            };
        }

        private static FlowLayoutPanel CreatePhilosophyWrapFlowPanel(string name, Color backColor)
        {
            return new FlowLayoutPanel
            {
                Name = name,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Padding = new Padding(8, 6, 8, 4),
                Margin = Padding.Empty,
                BackColor = backColor
            };
        }

        private static FlowLayoutPanel CreatePhilosophyToolGroup(string sectionLabel, Color backColor)
        {
            var group = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Margin = PhilosophyFlowSectionMargin,
                Padding = new Padding(0, 2, 0, 2),
                BackColor = backColor
            };

            if (!string.IsNullOrWhiteSpace(sectionLabel))
            {
                group.Controls.Add(new Label
                {
                    Text = sectionLabel,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(160, 168, 182),
                    Margin = new Padding(0, 10, 6, 4)
                });
            }

            return group;
        }

        private static void ApplyPhilosophyFlowMargin(Control control, int top = 4)
        {
            if (control == null)
            {
                return;
            }

            control.Margin = new Padding(PhilosophyFlowItemMargin.Left, top, PhilosophyFlowItemMargin.Right, PhilosophyFlowItemMargin.Bottom);
        }

        private static Button CreatePhilosophyCommandButton(string name, string text, Color tint)
        {
            return CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: PhilosophyCommandButtonHeight,
                minWidth: 96,
                horizontalPad: PhilosophyCommandHorizontalPad,
                margin: PhilosophyFlowItemMargin);
        }

        private static void ApplyPhilosophyCommandButtonMetrics(Button btn)
        {
            if (btn == null)
            {
                return;
            }

            ResizeAppJellyButton(btn, PhilosophyCommandButtonHeight, 96, PhilosophyCommandHorizontalPad);
            btn.Margin = PhilosophyFlowItemMargin;
        }

        private static RadioButton CreatePhilosophyRadio(string text, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                ForeColor = Color.Gainsboro,
                Font = PhilosophyUiFont,
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        private void PhilosophyProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshPhilosophyPrereqLabel(null);
            RefreshPhilosophyFolderOptions();
        }

        private void WirePhilosophyGridEvents()
        {
            dgvPhilosophyScripts.CellFormatting += DgvPhilosophyScripts_CellFormatting;
            dgvPhilosophyScripts.CellContentClick += DgvPhilosophyScripts_CellContentClick;
            dgvPhilosophyScripts.CellClick += DgvPhilosophyScripts_CellClick;
            dgvPhilosophyScripts.CellDoubleClick += DgvPhilosophyScripts_CellDoubleClick;
            dgvPhilosophyScripts.CellToolTipTextNeeded += DgvPhilosophyScripts_CellToolTipTextNeeded;
            dgvPhilosophyScripts.DataError += DgvPhilosophyScripts_DataError;
            dgvPhilosophyScripts.CellValueChanged += DgvPhilosophyScripts_CellValueChanged;

            _cmsPhilosophyGrid = new ContextMenuStrip { Font = PhilosophyUiFont };
            var miAdd = new ToolStripMenuItem("Thêm hàng");
            miAdd.Click += (_, __) => PhilosophyAddRow();
            var miDelete = new ToolStripMenuItem("Xóa hàng đã chọn");
            miDelete.Click += (_, __) => PhilosophyDeleteSelectedRows();
            _cmsPhilosophyGrid.Items.AddRange(new ToolStripItem[] { miAdd, miDelete });
            dgvPhilosophyScripts.ContextMenuStrip = _cmsPhilosophyGrid;
            EnsurePhilosophyBRollPickerMenu();
        }

        private void EnsurePhilosophyBRollPickerMenu()
        {
            if (_cmsPhilosophyBRollPicker != null && !_cmsPhilosophyBRollPicker.IsDisposed)
            {
                return;
            }

            _cmsPhilosophyBRollPicker = new ContextMenuStrip
            {
                Font = PhilosophyUiFont,
                ShowCheckMargin = false,
                ShowImageMargin = false
            };

            var miRandom = new ToolStripMenuItem("Ngẫu nhiên")
            {
                ToolTipText = "App tự chọn video ngẫu nhiên từ kho Assets"
            };
            _miPhilosophyBRollRandom = miRandom;
            miRandom.Click += (_, __) =>
            {
                var item = _philosophyBRollPickerItem;
                var row = _philosophyBRollPickerRowIndex;
                if (item == null)
                {
                    return;
                }

                item.BRollFolder = PhilosophyBRollSelection.RandomToken;
                _philosophyScriptBindingList?.ResetBindings();
                if (dgvPhilosophyScripts != null && row >= 0 && row < dgvPhilosophyScripts.Rows.Count)
                {
                    dgvPhilosophyScripts.InvalidateRow(row);
                }

                NotifyPhilosophyDraftDirty();
            };

            var miBrowse = new ToolStripMenuItem("Duyệt thư mục")
            {
                ToolTipText = "Mở thư mục chứa video nền trong Assets để chọn file"
            };
            _miPhilosophyBRollBrowse = miBrowse;
            miBrowse.Click += (_, __) =>
            {
                var item = _philosophyBRollPickerItem;
                var row = _philosophyBRollPickerRowIndex;
                if (item != null)
                {
                    PhilosophyPickBackgroundFile(row, item);
                }
            };

            _cmsPhilosophyBRollPicker.Items.Add(miRandom);
            _cmsPhilosophyBRollPicker.Items.Add(new ToolStripSeparator());
            _cmsPhilosophyBRollPicker.Items.Add(miBrowse);
        }

        private void DgvPhilosophyScripts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvPhilosophyScripts != null && e.RowIndex >= 0 && e.ColumnIndex >= 0
                && dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyVisualMode")
            {
                dgvPhilosophyScripts.InvalidateRow(e.RowIndex);
            }

            NotifyPhilosophyDraftDirty();
        }

        private static int GetDefaultPhilosophyVisualModeForNewRow(BindingList<PhilosophyScriptItem> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return PhilosophyVisualModes.Broll;
            }

            return PhilosophyVisualModes.Normalize(rows[rows.Count - 1].VisualMode);
        }

        private void ConfigurePhilosophyScriptGrid()
        {
            dgvPhilosophyScripts.Columns.Clear();

            _colPhilosophyProfile = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyProfile",
                HeaderText = "Profile",
                DataPropertyName = nameof(PhilosophyScriptItem.ProfileName),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 12,
                MinimumWidth = 72,
                ToolTipText = "Profile cho dòng này — dòng Gemini lấy từ combo Profile trên thanh công cụ."
            };
            dgvPhilosophyScripts.Columns.Add(_colPhilosophyProfile);

            dgvPhilosophyScripts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyContent",
                HeaderText = "Content",
                DataPropertyName = nameof(PhilosophyScriptItem.Content),
                FillWeight = 36,
                MinimumWidth = 100
            });

            _colPhilosophyVisualMode = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyVisualMode",
                HeaderText = "Chế độ nền",
                DataPropertyName = nameof(PhilosophyScriptItem.VisualModeLabel),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 16,
                MinimumWidth = 108,
                ToolTipText = "B-Roll = bấm cột «Nền» để chọn video. AI = cần Veo API trong Cài đặt."
            };
            foreach (var label in PhilosophyVisualModes.ComboLabels)
            {
                _colPhilosophyVisualMode.Items.Add(label);
            }

            dgvPhilosophyScripts.Columns.Add(_colPhilosophyVisualMode);

            dgvPhilosophyScripts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPhilosophyBRoll",
                HeaderText = "Nền",
                Text = "Chọn nền…",
                UseColumnTextForButtonValue = false,
                FillWeight = 14,
                MinimumWidth = 96,
                ToolTipText = "B-Roll/AI: chọn video. Chế độ 3: chọn ảnh nền tham chiếu cho Gemini.",
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                }
            });

            dgvPhilosophyScripts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPhilosophyScenePrompt",
                HeaderText = "Prompt video",
                Text = "Xem prompt…",
                UseColumnTextForButtonValue = false,
                FillWeight = 14,
                MinimumWidth = 96,
                ToolTipText = "Bấm để xem toàn bộ kịch bản phân cảnh và prompt AI.",
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                }
            });

            dgvPhilosophyScripts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPhilosophySceneVideo",
                HeaderText = "Thư mục video",
                Text = "Chọn thư mục…",
                UseColumnTextForButtonValue = false,
                FillWeight = 16,
                MinimumWidth = 108,
                ToolTipText = "Chế độ 3: thư mục chứa file .mp4 phân cảnh. Trống = dùng «Thư mục video» trên thanh công cụ.",
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                }
            });
            _colPhilosophySceneVideoBrowse = new DataGridViewButtonColumn
            {
                Name = "colPhilosophySceneVideoBrowse",
                HeaderText = string.Empty,
                Text = "…",
                ToolTipText = "Duyệt thư mục video phân cảnh (mode 3)",
                UseColumnTextForButtonValue = true,
                FillWeight = 4,
                MinimumWidth = 30
            };
            dgvPhilosophyScripts.Columns.Add(_colPhilosophySceneVideoBrowse);

            var moodCol = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyMood",
                HeaderText = "Mood",
                DataPropertyName = nameof(PhilosophyScriptItem.Mood),
                FlatStyle = FlatStyle.Flat,
                FillWeight = 12,
                MinimumWidth = 72
            };
            moodCol.Items.AddRange("calm", "melancholic", "hopeful", "intense", "reflective");
            dgvPhilosophyScripts.Columns.Add(moodCol);

            _colPhilosophyAmbient = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyAmbient",
                HeaderText = "Tiếng đệm",
                DataPropertyName = nameof(PhilosophyScriptItem.AmbientKey),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 14,
                MinimumWidth = 96
            };
            foreach (var key in PhilosophyAmbientCatalog.AllKeys)
            {
                _colPhilosophyAmbient.Items.Add(key);
            }

            dgvPhilosophyScripts.Columns.Add(_colPhilosophyAmbient);

            _colPhilosophyMusic = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyMusic",
                HeaderText = "Nhạc",
                DataPropertyName = nameof(PhilosophyScriptItem.MusicFolder),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 16,
                MinimumWidth = 88
            };
            dgvPhilosophyScripts.Columns.Add(_colPhilosophyMusic);
            _colPhilosophyMusicBrowse = new DataGridViewButtonColumn
            {
                Name = "colPhilosophyMusicBrowse",
                HeaderText = string.Empty,
                Text = "…",
                ToolTipText = "Duyệt file nhạc (.mp3/.wav/.m4a)",
                UseColumnTextForButtonValue = true,
                FillWeight = 4,
                MinimumWidth = 30
            };
            dgvPhilosophyScripts.Columns.Add(_colPhilosophyMusicBrowse);

            dgvPhilosophyScripts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophySubtitle",
                HeaderText = "Phụ đề",
                DataPropertyName = nameof(PhilosophyScriptItem.SubtitleStyleLabel),
                FillWeight = 22,
                MinimumWidth = 120,
                ReadOnly = true,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });

            dgvPhilosophyScripts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyStatus",
                HeaderText = "Status",
                DataPropertyName = nameof(PhilosophyScriptItem.Status),
                FillWeight = 12,
                MinimumWidth = 72,
                ReadOnly = true
            });

            dgvPhilosophyScripts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPhilosophyOutput",
                HeaderText = "Kết quả",
                Text = "▶ Xem",
                ToolTipText = "Bấm để mở video sau khi render xong",
                UseColumnTextForButtonValue = true,
                FillWeight = 10,
                MinimumWidth = 64
            });
        }

        private void DgvPhilosophyScripts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0)
            {
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyStatus")
            {
                var status = (e.Value?.ToString() ?? "Nháp").Trim();
                Color fore;
                string glyph;
                if (status.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(255, 110, 110);
                    glyph = "✕ ";
                }
                else if (status.IndexOf("xong", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(100, 210, 130);
                    glyph = "✓ ";
                }
                else if (status.IndexOf("render", StringComparison.OrdinalIgnoreCase) >= 0
                         || status.IndexOf("đang", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(255, 196, 90);
                    glyph = "◐ ";
                }
                else if (status.IndexOf("dừng", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(255, 150, 110);
                    glyph = "■ ";
                }
                else
                {
                    fore = Color.FromArgb(150, 158, 172);
                    glyph = "● ";
                }

                e.Value = glyph + status;
                e.CellStyle.ForeColor = fore;
                e.FormattingApplied = true;
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyBRoll"
                || dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyMusic"
                || dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyScenePrompt"
                || dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySceneVideo")
            {
                if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyBRoll"
                    && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem brollRow)
                {
                    e.Value = PhilosophyBRollSelection.GetBackgroundDisplayLabel(
                        brollRow.BRollFolder,
                        PhilosophyVisualModes.Normalize(brollRow.VisualMode));
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyScenePrompt"
                         && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem sceneRow)
                {
                    e.Value = PhilosophySceneHelper.GetScenePromptDisplayLabel(sceneRow);
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySceneVideo"
                         && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem videoRow)
                {
                    e.Value = FormatPhilosophySceneVideoFolderCell(videoRow);
                    var isPreRendered = PhilosophyVisualModes.Normalize(videoRow.VisualMode) == PhilosophyVisualModes.PreRendered;
                    e.CellStyle.ForeColor = isPreRendered
                        ? Color.FromArgb(130, 175, 255)
                        : Color.FromArgb(120, 126, 138);
                }
                else
                {
                    e.Value = FormatPhilosophyFolderCell(e.Value?.ToString());
                }

                e.FormattingApplied = true;
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyOutput"
                && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem outputItem)
            {
                var hasOutput = !string.IsNullOrWhiteSpace(outputItem.OutputPath)
                                && System.IO.File.Exists(outputItem.OutputPath);
                e.Value = hasOutput ? "▶ Xem" : "—";
                e.CellStyle.ForeColor = hasOutput
                    ? Color.FromArgb(130, 175, 255)
                    : Color.FromArgb(120, 126, 138);
                e.FormattingApplied = true;
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyAmbient")
            {
                e.Value = PhilosophyAmbientCatalog.GetLabel(e.Value?.ToString());
                e.FormattingApplied = true;
            }
        }

        private void DgvPhilosophyScripts_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0)
            {
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyContent"
                && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem item)
            {
                e.ToolTipText = item.Content ?? string.Empty;
                return;
            }

            if (dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem rowItem)
            {
                if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyBRoll")
                {
                    var profile = ResolvePhilosophyRowProfile(rowItem);
                    e.ToolTipText = PhilosophyBRollSelection.DescribeBackgroundTooltip(
                        rowItem.BRollFolder,
                        profile,
                        PhilosophyVisualModes.Normalize(rowItem.VisualMode));
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyScenePrompt")
                {
                    var count = rowItem.Scenes?.Count ?? 0;
                    e.ToolTipText = count > 0
                        ? "Bấm để xem " + count + " phân cảnh và prompt AI"
                        : "Chưa có prompt — bấm «Tạo Prompt Phân Cảnh»";
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySceneVideo"
                         || dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySceneVideoBrowse")
                {
                    e.ToolTipText = DescribePhilosophySceneVideoFolderTooltip(rowItem);
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyMusic")
                {
                    e.ToolTipText = DescribePhilosophyFolderTooltip(rowItem.MusicFolder, "nhạc");
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyVisualMode")
                {
                    e.ToolTipText = rowItem.VisualModeLabel;
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyOutput")
                {
                    e.ToolTipText = string.IsNullOrWhiteSpace(rowItem.OutputPath)
                        ? "Chưa có video — render trước"
                        : rowItem.OutputPath;
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyAmbient")
                {
                    var folder = PhilosophyAmbientCatalog.ResolveAmbientFolder(
                        rowItem.AmbientKey,
                        GetSelectedPhilosophyProfileName());
                    e.ToolTipText = PhilosophyAmbientCatalog.GetLabel(rowItem.AmbientKey) +
                                    (string.IsNullOrEmpty(folder) || rowItem.AmbientKey == PhilosophyAmbientCatalog.NoneKey
                                        ? string.Empty
                                        : "\r\n" + folder);
                }
                else if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySubtitle")
                {
                    e.ToolTipText = string.IsNullOrWhiteSpace(rowItem.SubtitleStyleLabel)
                        ? "Bấm để chỉnh phụ đề (vị trí, font, karaoke…)"
                        : rowItem.SubtitleStyleLabel;
                }
            }
        }

        private void DgvPhilosophyScripts_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            if (dgvPhilosophyScripts == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = dgvPhilosophyScripts.Columns[e.ColumnIndex].Name;
            if (colName != "colPhilosophyMusic"
                && colName != "colPhilosophyAmbient" && colName != "colPhilosophyVisualMode"
                && colName != "colPhilosophyProfile")
            {
                return;
            }

            if (dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem item)
            {
                if (colName == "colPhilosophyMusic")
                {
                    EnsurePhilosophyMusicInCombo(item.MusicFolder);
                }
                else if (colName == "colPhilosophyVisualMode")
                {
                    item.VisualMode = PhilosophyVisualModes.FromLabel(item.VisualModeLabel);
                }
                else if (colName == "colPhilosophyProfile")
                {
                    item.ProfileName = ProfileScopedPaths.ResolveProfileName(item.ProfileName);
                }
                else
                {
                    item.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(item.AmbientKey);
                    EnsurePhilosophyAmbientInCombo(item.AmbientKey);
                }
            }
        }

        private void DgvPhilosophyScripts_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name != "colPhilosophyContent")
            {
                return;
            }

            if (!(dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem item))
            {
                return;
            }

            ShowAffiliateLongTextPeekDialog("Content", item.Content ?? string.Empty);
        }

        private void DgvPhilosophyScripts_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0)
            {
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyMusicBrowse")
            {
                PhilosophyBrowseRowFolder(e.RowIndex, isMusic: true);
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySceneVideoBrowse"
                || dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySceneVideo")
            {
                if (dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem videoFolderItem)
                {
                    PhilosophyBrowseRowSceneVideoFolder(e.RowIndex, videoFolderItem);
                }

                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyBRoll"
                && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem brollItem)
            {
                ShowPhilosophyBRollPicker(brollItem, e.RowIndex);
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyScenePrompt"
                && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem sceneItem)
            {
                ShowPhilosophyScenePromptViewer(sceneItem, e.RowIndex);
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyOutput"
                && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem outputRow)
            {
                OpenPhilosophyOutputVideo(outputRow);
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophySubtitle"
                && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem subtitleItem)
            {
                ShowPhilosophySubtitleStyleEditor(subtitleItem, e.RowIndex);
            }
        }

        private void DgvPhilosophyScripts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name != "colPhilosophySubtitle")
            {
                return;
            }

            if (dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem item)
            {
                ShowPhilosophySubtitleStyleEditor(item, e.RowIndex);
            }
        }

        private void ShowPhilosophySubtitleStyleEditor(PhilosophyScriptItem item, int gridRowIndex)
        {
            if (item == null)
            {
                return;
            }

            PhilosophySubtitleStyleHelper.EnsureDefaults(item);
            using (var dlg = new PhilosophySubtitleStyleEditorForm(item))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            _philosophyScriptBindingList?.ResetBindings();
            if (dgvPhilosophyScripts != null && gridRowIndex >= 0 && gridRowIndex < dgvPhilosophyScripts.Rows.Count)
            {
                dgvPhilosophyScripts.InvalidateRow(gridRowIndex);
            }
        }

        private void btnPhilosophyAddRow_Click(object sender, EventArgs e)
        {
            PhilosophyAddRow();
        }

        private void btnPhilosophyDeleteRow_Click(object sender, EventArgs e)
        {
            PhilosophyDeleteSelectedRows();
        }

        private void PhilosophyAddRow()
        {
            var row = new PhilosophyScriptItem
            {
                Content = string.Empty,
                Mood = "reflective",
                ProfileName = GetSelectedPhilosophyProfileName(),
                VisualMode = GetDefaultPhilosophyVisualModeForNewRow(_philosophyScriptBindingList),
                BRollFolder = PhilosophyBRollSelection.RandomToken,
                Status = "Nháp"
            };
            PhilosophySubtitleStyleHelper.ApplyPhilosophyDefaults(row);
            PhilosophyAmbientCatalog.EnsureRowDefault(row);
            _philosophyScriptBindingList?.Add(row);
            NotifyPhilosophyDraftDirty();
        }

        private List<PhilosophyScriptItem> GetPhilosophyTargetRowsFromGrid()
        {
            if (dgvPhilosophyScripts == null)
            {
                return new List<PhilosophyScriptItem>();
            }

            var rows = dgvPhilosophyScripts.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => r.DataBoundItem is PhilosophyScriptItem)
                .Select(r => (PhilosophyScriptItem)r.DataBoundItem)
                .ToList();

            if (rows.Count == 0
                && dgvPhilosophyScripts.CurrentRow?.DataBoundItem is PhilosophyScriptItem current)
            {
                rows.Add(current);
            }

            if (_philosophyScriptBindingList == null || _philosophyScriptBindingList.Count == 0)
            {
                return rows;
            }

            return rows
                .OrderBy(item => _philosophyScriptBindingList.IndexOf(item))
                .ThenBy(item => item?.Content ?? string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private void PhilosophyDeleteSelectedRows()
        {
            if (_philosophyScriptBindingList == null || dgvPhilosophyScripts == null)
            {
                return;
            }

            var toRemove = GetPhilosophyTargetRowsFromGrid();
            if (toRemove.Count == 0)
            {
                return;
            }

            if (!UiConfirmHelper.ConfirmDeleteRows(this, toRemove.Count))
            {
                return;
            }

            foreach (var item in toRemove)
            {
                _philosophyScriptBindingList.Remove(item);
            }

            if (toRemove.Count > 0)
            {
                NotifyPhilosophyDraftDirty();
            }
        }

        private void PhilosophyModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (numPhilosophyCount == null)
            {
                return;
            }

            var story = rbPhilosophyModeStory != null && rbPhilosophyModeStory.Checked;
            numPhilosophyCount.Enabled = !story;
            if (story)
            {
                numPhilosophyCount.Value = 1;
            }
        }

        private void ShowPhilosophyBRollPicker(PhilosophyScriptItem item, int gridRowIndex)
        {
            if (item == null || dgvPhilosophyScripts == null)
            {
                return;
            }

            EnsurePhilosophyBRollPickerMenu();
            _philosophyBRollPickerItem = item;
            _philosophyBRollPickerRowIndex = gridRowIndex;

            var isPreRendered = PhilosophyVisualModes.Normalize(item.VisualMode) == PhilosophyVisualModes.PreRendered;
            if (_miPhilosophyBRollRandom != null)
            {
                _miPhilosophyBRollRandom.Visible = !isPreRendered;
            }

            if (_miPhilosophyBRollBrowse != null)
            {
                _miPhilosophyBRollBrowse.Text = isPreRendered ? "Chọn ảnh nền…" : "Duyệt thư mục";
                _miPhilosophyBRollBrowse.ToolTipText = isPreRendered
                    ? "Chọn ảnh tham chiếu — Gemini dùng ảnh + câu triết lý để viết prompt phân cảnh"
                    : "Mở thư mục chứa video nền trong Assets để chọn file";
            }

            var col = dgvPhilosophyScripts.Columns["colPhilosophyBRoll"];
            if (col == null)
            {
                return;
            }

            var rect = dgvPhilosophyScripts.GetCellDisplayRectangle(col.Index, gridRowIndex, false);
            _cmsPhilosophyBRollPicker.Show(dgvPhilosophyScripts, new Point(rect.Left, rect.Bottom));
        }

        private void PhilosophyPickBackgroundFile(int rowIndex, PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            if (PhilosophyVisualModes.Normalize(item.VisualMode) == PhilosophyVisualModes.PreRendered)
            {
                PhilosophyPickReferenceImageFile(rowIndex, item);
                return;
            }

            PhilosophyPickBRollVideoFile(rowIndex, item);
        }

        private void PhilosophyPickReferenceImageFile(int rowIndex, PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            var profile = ResolvePhilosophyRowProfile(item);
            var initialDir = PhilosophyBRollSelection.GetImageBrowseInitialDirectory(profile);
            try
            {
                System.IO.Directory.CreateDirectory(initialDir);
            }
            catch
            {
                // ignored
            }

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh nền tham chiếu (mode 3)",
                Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Tất cả|*.*",
                InitialDirectory = System.IO.Directory.Exists(initialDir) ? initialDir : string.Empty
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var path = dlg.FileName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(path) || !PhilosophyBRollSelection.IsImageFile(path))
                {
                    MessageBox.Show(this, "File ảnh không hợp lệ.", "Video Triết lý",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                item.BRollFolder = path;
                if (dgvPhilosophyScripts != null && rowIndex >= 0 && rowIndex < dgvPhilosophyScripts.Rows.Count)
                {
                    dgvPhilosophyScripts.InvalidateRow(rowIndex);
                }

                NotifyPhilosophyDraftDirty();
                LogPhilosophy("Ảnh nền mode 3: " + System.IO.Path.GetFileName(path));
            }
        }

        private void ShowPhilosophyScenePromptViewer(PhilosophyScriptItem item, int gridRowIndex)
        {
            if (item == null)
            {
                return;
            }

            using (var dlg = new PhilosophyScenePromptViewerForm(item))
            {
                dlg.ShowDialog(this);
            }

            if (dgvPhilosophyScripts != null && gridRowIndex >= 0 && gridRowIndex < dgvPhilosophyScripts.Rows.Count)
            {
                dgvPhilosophyScripts.InvalidateRow(gridRowIndex);
            }
        }

        private void PhilosophyPickBRollVideoFile(int rowIndex, PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            var profile = ResolvePhilosophyRowProfile(item);
            var initialDir = PhilosophyBRollSelection.GetBrowseInitialDirectory(profile);
            try
            {
                System.IO.Directory.CreateDirectory(initialDir);
            }
            catch
            {
                // ignore — OpenFileDialog vẫn mở được
            }

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn video nền B-Roll",
                Filter = "Video (*.mp4;*.mov)|*.mp4;*.mov|Tất cả|*.*",
                InitialDirectory = System.IO.Directory.Exists(initialDir) ? initialDir : string.Empty
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var path = dlg.FileName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                item.BRollFolder = path;
                _philosophyScriptBindingList?.ResetBindings();
                if (dgvPhilosophyScripts != null && rowIndex >= 0 && rowIndex < dgvPhilosophyScripts.Rows.Count)
                {
                    dgvPhilosophyScripts.InvalidateRow(rowIndex);
                }

                NotifyPhilosophyDraftDirty();
            }
        }

        private void PhilosophyBrowseRowFolder(int rowIndex, bool isMusic)
        {
            if (dgvPhilosophyScripts == null || rowIndex < 0 || rowIndex >= dgvPhilosophyScripts.Rows.Count)
            {
                return;
            }

            if (!(dgvPhilosophyScripts.Rows[rowIndex].DataBoundItem is PhilosophyScriptItem item))
            {
                return;
            }

            if (isMusic)
            {
                PhilosophyBrowseRowMusicFile(rowIndex, item);
                return;
            }
        }

        private void PhilosophyBrowseRowMusicFile(int rowIndex, PhilosophyScriptItem item)
        {
            var current = item.MusicFolder?.Trim() ?? string.Empty;
            var initialDir = string.Empty;
            if (System.IO.File.Exists(current))
            {
                initialDir = System.IO.Path.GetDirectoryName(current) ?? string.Empty;
            }
            else if (System.IO.Directory.Exists(current))
            {
                initialDir = current;
            }
            else
            {
                initialDir = PhilosophyProfileAssets.GetAssetsRoot(GetSelectedPhilosophyProfileName());
            }

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn bài nhạc nền",
                Filter = "Audio (*.mp3;*.wav;*.m4a)|*.mp3;*.wav;*.m4a|Tất cả|*.*",
                InitialDirectory = System.IO.Directory.Exists(initialDir) ? initialDir : string.Empty
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var path = dlg.FileName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                item.MusicFolder = System.IO.Path.GetFileName(path);
                EnsurePhilosophyMusicInCombo(item.MusicFolder);
                _philosophyScriptBindingList?.ResetBindings();
                NotifyPhilosophyDraftDirty();
            }
        }

        private void RefreshPhilosophyFolderOptions()
        {
            if (_colPhilosophyMusic == null)
            {
                return;
            }

            AppSettings settings;
            try
            {
                settings = _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult()
                           ?? new AppSettings();
            }
            catch
            {
                settings = new AppSettings();
            }

            var profile = GetSelectedPhilosophyProfileName();
            var musicFiles = PhilosophyProfileAssets.EnumerateMusicFileNames(profile, settings);

            if (_philosophyScriptBindingList != null)
            {
                foreach (var item in _philosophyScriptBindingList)
                {
                    AddPhilosophyMusicFileOption(musicFiles, item.MusicFolder);
                }
            }

            ApplyPhilosophyMusicComboItems(_colPhilosophyMusic, musicFiles);
        }

        private static void AddPhilosophyMusicFileOption(List<string> fileNames, string selection)
        {
            var sel = (selection ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(sel))
            {
                return;
            }

            if (System.IO.File.Exists(sel))
            {
                AddPhilosophyFolderOption(fileNames, System.IO.Path.GetFileName(sel));
                return;
            }

            if (!System.IO.Directory.Exists(sel))
            {
                AddPhilosophyFolderOption(fileNames, sel);
            }
        }

        private static void ApplyPhilosophyMusicComboItems(DataGridViewComboBoxColumn column, List<string> fileNames)
        {
            if (column == null)
            {
                return;
            }

            column.Items.Clear();
            column.Items.Add(string.Empty);
            foreach (var name in fileNames)
            {
                column.Items.Add(name);
            }
        }

        private void EnsurePhilosophyMusicInCombo(string fileName)
        {
            if (_colPhilosophyMusic == null)
            {
                return;
            }

            var trimmed = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            foreach (var item in _colPhilosophyMusic.Items)
            {
                if (string.Equals(item?.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _colPhilosophyMusic.Items.Add(trimmed);
        }

        private static void AddPhilosophyFolderOption(List<string> paths, string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            if (paths.Any(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            paths.Add(trimmed);
        }

        private void EnsurePhilosophyAmbientInCombo(string ambientKey)
        {
            if (_colPhilosophyAmbient == null)
            {
                return;
            }

            var key = PhilosophyAmbientCatalog.NormalizeKey(ambientKey);
            foreach (var item in _colPhilosophyAmbient.Items)
            {
                if (string.Equals(item?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _colPhilosophyAmbient.Items.Add(key);
        }

        private static string FormatPhilosophyFolderCell(string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return "— mặc định —";
            }

            trimmed = trimmed.TrimEnd('\\', '/');
            var name = System.IO.Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }

        private static string DescribePhilosophyFolderTooltip(string path, string kind)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return kind == "nhạc"
                    ? "Dùng nhạc mặc định theo mood / profile"
                    : "Dùng " + kind + " mặc định của profile / mood";
            }

            if (kind == "nhạc" && System.IO.File.Exists(trimmed))
            {
                return trimmed;
            }

            return trimmed;
        }

        private async void btnPhilosophyGenerateScript_Click(object sender, EventArgs e)
        {
            if (IsPhilosophyTabBusy(out var busyReason))
            {
                MessageBox.Show(busyReason, "Video Triết lý", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var topic = txtPhilosophyTopic?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(topic))
            {
                MessageBox.Show(
                    "Nhập chủ đề trước khi bấm «Tạo câu triết lý».",
                    "Video Triết lý",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _philosophyScriptGenCts?.Dispose();
            _philosophyScriptGenCts = new CancellationTokenSource();
            _philosophyScriptGenRunning = true;
            UpdatePhilosophyBusyControlStates();
            SetPhilosophyProgress("Gemini: đang sinh kịch bản…", 0, indeterminate: true);
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    MessageBox.Show(
                        "Cần AI API Key (Gemini) trong tab Cài đặt.",
                        "Video Triết lý",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    LogPhilosophy("Triết lý: thiếu AI API Key.");
                    return;
                }

                var mode = rbPhilosophyModeStory != null && rbPhilosophyModeStory.Checked ? "Story" : "Quotes";
                var count = (int)(numPhilosophyCount?.Value ?? 5);
                if (!TryNormalizePhilosophyDurationRange(
                        (int)(numPhilosophyDurationMin?.Value ?? 15),
                        (int)(numPhilosophyDurationMax?.Value ?? 60),
                        out var minDuration,
                        out var maxDuration,
                        out var durationErr))
                {
                    MessageBox.Show(durationErr, "Video Triết lý", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var (minWords, maxWords) = PhilosophyRenderOptions.EstimateSpeechWordCount(minDuration, maxDuration);
                LogPhilosophy("Gemini: mục tiêu " + minDuration + "–" + maxDuration + " giây (~" +
                              minWords + "–" + maxWords + " từ/" + (mode == "Story" ? "câu chuyện" : "quote") + ").");
                var scripts = await _videoProcessingService.GeneratePhilosophyScriptsAsync(
                    topic,
                    mode,
                    count,
                    settings,
                    minDuration,
                    maxDuration,
                    _philosophyScriptGenCts.Token).ConfigureAwait(true);

                _philosophyScriptGenCts.Token.ThrowIfCancellationRequested();

                void ApplyGrid()
                {
                    _philosophyScriptBindingList ??= new BindingList<PhilosophyScriptItem>();
                    var defaultProfile = GetSelectedPhilosophyProfileName();
                    var defaultVisual = GetDefaultPhilosophyVisualModeForNewRow(_philosophyScriptBindingList);
                    foreach (var script in scripts)
                    {
                        PhilosophySubtitleStyleHelper.EnsureDefaults(script);
                        PhilosophyAmbientCatalog.EnsureRowDefault(script);
                        script.ProfileName = defaultProfile;
                        script.VisualMode = defaultVisual;
                        _philosophyScriptBindingList.Add(script);
                    }

                    if (dgvPhilosophyScripts != null)
                    {
                        dgvPhilosophyScripts.DataSource = _philosophyScriptBindingList;
                    }

                    RefreshPhilosophyFolderOptions();
                }

                if (dgvPhilosophyScripts != null && dgvPhilosophyScripts.InvokeRequired)
                {
                    dgvPhilosophyScripts.Invoke(new Action(ApplyGrid));
                }
                else
                {
                    ApplyGrid();
                }

                foreach (var script in scripts)
                {
                    var wordCount = CountPhilosophyWords(script?.Content);
                    if (wordCount < minWords || wordCount > maxWords)
                    {
                        LogPhilosophy("Cảnh báo: một dòng có " + wordCount + " từ (mục tiêu " +
                                      minWords + "–" + maxWords + ") — nên chỉnh tay trước khi render.");
                    }
                }

                LogPhilosophy("Triết lý: Gemini trả " + scripts.Count + " dòng — duyệt/sửa trước khi render.");
                SetPhilosophyProgress("Đã sinh " + scripts.Count + " kịch bản", 100);
                NotifyPhilosophyDraftDirty();
            }
            catch (OperationCanceledException)
            {
                LogPhilosophy("Triết lý: đã dừng sinh kịch bản.");
                SetPhilosophyProgress("Đã dừng", 0);
            }
            catch (Exception ex)
            {
                LogPhilosophy("Triết lý lỗi Gemini: " + ex.Message);
                SetPhilosophyProgress("lỗi sinh kịch bản", 0);
            }
            finally
            {
                _philosophyScriptGenRunning = false;
                _philosophyScriptGenCts?.Dispose();
                _philosophyScriptGenCts = null;
                UpdatePhilosophyBusyControlStates();
            }
        }

        private bool IsPhilosophyScriptGenerationReady()
        {
            return !IsPhilosophyTabBusy(out _);
        }

        private bool IsPhilosophyTabBusy(out string reason)
        {
            if (_philosophyRenderRunning)
            {
                reason = "Đang render — bấm «Dừng» để hủy.";
                return true;
            }

            if (_philosophyScriptGenRunning)
            {
                reason = "Đang sinh kịch bản — bấm «Dừng» để hủy.";
                return true;
            }

            if (_philosophyScenePromptRunning)
            {
                reason = "Đang tạo prompt phân cảnh — bấm «Dừng» để hủy.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private void BtnPhilosophyStopAll_Click(object sender, EventArgs e)
        {
            CancelAllPhilosophyWorkForEmergencyStop(logPrefix: "Triết lý");
        }

        private void CancelAllPhilosophyWorkForEmergencyStop(string logPrefix = null)
        {
            var prefix = string.IsNullOrWhiteSpace(logPrefix) ? "[Emergency]" : logPrefix + ":";
            var cancelled = false;

            if (_philosophyScriptGenRunning)
            {
                LogPhilosophy(prefix + " đang dừng sinh kịch bản…");
                TryCancel(_philosophyScriptGenCts);
                cancelled = true;
            }

            if (_philosophyScenePromptRunning)
            {
                LogPhilosophy(prefix + " đang dừng tạo prompt phân cảnh…");
                TryCancel(_philosophyScenePromptCts);
                cancelled = true;
            }

            if (_philosophyRenderRunning)
            {
                _philosophyRenderPaused = true;
                LogPhilosophy(prefix + " đang dừng render… (chờ bước hiện tại kết thúc)");
                SetPhilosophyProgress("Đang dừng…", 0, indeterminate: true);
                TryCancel(_philosophyRenderCts);
                cancelled = true;
            }

            if (cancelled)
            {
                UpdatePhilosophyBusyControlStates();
            }
        }

        private void UpdatePhilosophyBusyControlStates()
        {
            var tabBusy = _philosophyScriptGenRunning || _philosophyScenePromptRunning || _philosophyRenderRunning;

            if (btnPhilosophyStopAll != null)
            {
                btnPhilosophyStopAll.Visible = tabBusy;
                btnPhilosophyStopAll.Enabled = tabBusy;
            }

            if (btnPhilosophyGenerateScript != null)
            {
                btnPhilosophyGenerateScript.Enabled = !tabBusy;
            }

            if (btnGenerateScenePrompts != null)
            {
                btnGenerateScenePrompts.Enabled = !tabBusy;
            }

            if (btnExportExcelPrompts != null)
            {
                btnExportExcelPrompts.Enabled = !tabBusy;
            }

            if (btnPhilosophyPushToAutoPost != null)
            {
                btnPhilosophyPushToAutoPost.Enabled = !tabBusy;
            }

            UpdatePhilosophyRenderControlStates();
        }

        private async void btnPhilosophyStartRender_Click(object sender, EventArgs e)
        {
            await StartPhilosophyRenderBatchAsync(resume: false).ConfigureAwait(true);
        }

        private async void btnPhilosophyStopRender_Click(object sender, EventArgs e)
        {
            if (_philosophyRenderRunning)
            {
                _philosophyRenderPaused = true;
                LogPhilosophy("Triết lý: đang dừng render… (chờ bước hiện tại kết thúc)");
                SetPhilosophyProgress("Đang dừng…", 0, indeterminate: true);
                _philosophyRenderCts?.Cancel();
                return;
            }

            if (_philosophyRenderPaused && _philosophyRenderPending != null && _philosophyRenderPending.Count > 0)
            {
                await StartPhilosophyRenderBatchAsync(resume: true).ConfigureAwait(true);
            }
        }

        private async Task StartPhilosophyRenderBatchAsync(bool resume)
        {
            if (_philosophyRenderRunning)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (!resume)
            {
                var selected = GetPhilosophyTargetRowsFromGrid();
                if (!TryValidatePhilosophyRenderRequest(selected, settings, out var blockMessage))
                {
                    NotifyPhilosophyRenderBlocked(blockMessage);
                    return;
                }

                if (_philosophyRenderPaused && _philosophyRenderPending != null && _philosophyRenderPending.Count > 0)
                {
                    LogPhilosophy("Bắt đầu batch mới — bỏ " + _philosophyRenderPending.Count +
                                  " dòng chờ từ lần trước (dùng «Tiếp tục» nếu muốn render tiếp).");
                }

                _philosophyRenderPending = selected.ToList();
                _philosophyRenderPaused = false;
                LogPhilosophy("Render " + _philosophyRenderPending.Count + " dòng đã chọn (trên → dưới).");
                for (var i = 0; i < _philosophyRenderPending.Count; i++)
                {
                    var rowNum = GetPhilosophyRowDisplayNumber(_philosophyRenderPending[i]);
                    LogPhilosophy("  " + (i + 1) + ". Dòng " + rowNum + ": " +
                                  TrimPhilosophyPreview(_philosophyRenderPending[i]?.Content));
                }
            }
            else
            {
                if (_philosophyRenderPending == null || _philosophyRenderPending.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "Không còn dòng chờ render.\r\nBôi đen dòng trong bảng rồi bấm «Bắt đầu Render».",
                        "Tiếp tục render",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (!PhilosophyVideoPipelineService.TryValidatePrerequisites(settings, out var preErr))
                {
                    NotifyPhilosophyRenderBlocked(preErr + "\r\n\r\nMở tab Cài đặt để bổ sung.");
                    return;
                }

                _philosophyRenderPaused = false;
                LogPhilosophy("Triết lý: tiếp tục render " + _philosophyRenderPending.Count + " dòng còn lại…");
            }

            await RunPhilosophyRenderLoopAsync(settings).ConfigureAwait(true);
        }

        private async Task RunPhilosophyRenderLoopAsync(AppSettings settings)
        {
            if (_philosophyRenderPending == null || _philosophyRenderPending.Count == 0)
            {
                return;
            }

            _philosophyRenderRunning = true;
            _philosophyRenderCts?.Dispose();
            _philosophyRenderCts = new CancellationTokenSource();
            UpdatePhilosophyBusyControlStates();

            var profileName = GetSelectedPhilosophyProfileName();
            var batchTotal = _philosophyRenderPending.Count;
            var completedInBatch = 0;

            try
            {
                while (_philosophyRenderPending.Count > 0)
                {
                    _philosophyRenderCts.Token.ThrowIfCancellationRequested();

                    var item = _philosophyRenderPending[0];
                    var rowProfile = ResolvePhilosophyRowProfile(item);
                    var profile = PhilosophyProfileAssets.ResolveProfile(settings, rowProfile);
                    var ordinal = completedInBatch + 1;
                    item.Status = "Đang render…";
                    item.LastError = string.Empty;
                    _philosophyScriptBindingList?.ResetBindings();
                    SetPhilosophyProgress(
                        "Render " + ordinal + "/" + batchTotal + "…",
                        (int)Math.Round(completedInBatch * 100d / Math.Max(1, batchTotal)),
                        indeterminate: true);
                    LogPhilosophy("Triết lý [" + ordinal + "/" + batchTotal + "]: " + TrimPhilosophyPreview(item.Content));

                    var renderOptions = BuildPhilosophyRenderOptions(rowProfile, item);
                    LogPhilosophy("Profile: «" + rowProfile + "»");
                    LogPhilosophy("Chế độ nền dòng: " + item.VisualModeLabel);
                    LogPhilosophy("Thời lượng xuất: đọc hết quote + "
                                  + PhilosophyRenderOptions.OutroPadMinSeconds.ToString("0")
                                  + "–"
                                  + PhilosophyRenderOptions.OutroPadMaxSeconds.ToString("0")
                                  + "s thở (tùy phân cảnh cuối)");
                    LogPhilosophy("Nền: " + PhilosophyBRollSelection.DescribeBackgroundTooltip(
                        item.BRollFolder, rowProfile, PhilosophyVisualModes.Normalize(item.VisualMode)));
                    LogPhilosophy("Nhạc: " + DescribePhilosophyFolderTooltip(item.MusicFolder, "nhạc"));
                    LogPhilosophy("Tiếng đệm: " + PhilosophyAmbientCatalog.GetLabel(item.AmbientKey));

                    try
                    {
                        ApplyMoodToProfileVoice(item, profile);
                        var voiceId = PhilosophyVideoPipelineService.ResolveVoiceIdByMood(item.Mood, settings, profile.VoiceId);
                        LogPhilosophy("Giọng (mood «" + (item.Mood ?? "reflective") + "»): " +
                                      (string.IsNullOrWhiteSpace(voiceId) ? "mặc định" : voiceId));
                        if (!string.IsNullOrWhiteSpace(item.OutputPath))
                        {
                            PhilosophyVideoPipelineService.TryDeletePreviousOutput(item.OutputPath, LogPhilosophy);
                            item.OutputPath = string.Empty;
                            _philosophyScriptBindingList?.ResetBindings();
                        }

                        var result = await _philosophyVideoService.GenerateFromScriptAsync(
                            item,
                            renderOptions,
                            settings,
                            profile,
                            LogPhilosophy,
                            (status, pct) => SetPhilosophyProgress(status, pct),
                            _philosophyRenderCts.Token).ConfigureAwait(true);

                        item.Status = "Xong";
                        item.OutputPath = result?.OutputPath ?? string.Empty;
                        item.LastError = string.Empty;
                        _philosophyRenderPending.RemoveAt(0);
                        completedInBatch++;
                        EnqueuePhilosophyJobFromResult(item, result, rowProfile, settings);
                        LogPhilosophy("Xong: " + result.OutputPath);
                        NotifyPhilosophyDraftDirty();
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = "Dừng";
                        _philosophyRenderPaused = true;
                        LogPhilosophy("Triết lý: đã dừng — còn " + _philosophyRenderPending.Count +
                                      " dòng. Bấm «Tiếp tục» để render tiếp.");
                        SetPhilosophyProgress("Đã dừng — " + _philosophyRenderPending.Count + " dòng chờ", 0);
                        break;
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Lỗi";
                        item.LastError = ex.Message;
                        item.OutputPath = string.Empty;
                        _philosophyRenderPaused = true;
                        LogPhilosophy("Lỗi render: " + ex.Message);
                        SetPhilosophyProgress("Dừng tại lỗi", 0);
                        MessageBox.Show(
                            this,
                            ex.Message,
                            "Video Triết lý — lỗi render",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        NotifyPhilosophyDraftDirty();
                        break;
                    }

                    _philosophyScriptBindingList?.ResetBindings();
                }

                if (!_philosophyRenderPaused && (_philosophyRenderPending == null || _philosophyRenderPending.Count == 0))
                {
                    SetPhilosophyProgress("Hoàn tất " + completedInBatch + " video", 100);
                    LogPhilosophy("Triết lý: hoàn tất batch render.");
                }
            }
            finally
            {
                _philosophyRenderRunning = false;
                _philosophyRenderCts?.Dispose();
                _philosophyRenderCts = null;
                if (!_philosophyRenderPaused)
                {
                    _philosophyRenderPending?.Clear();
                }

                UpdatePhilosophyBusyControlStates();
            }
        }

        private void UpdatePhilosophyRenderControlStates(bool? pipelineReadyOverride = null)
        {
            var pipelineReady = pipelineReadyOverride ?? IsPhilosophyPipelineReady();
            var running = _philosophyRenderRunning;
            var hasPending = _philosophyRenderPending != null && _philosophyRenderPending.Count > 0;
            var canResume = _philosophyRenderPaused && hasPending && !running;

            if (btnPhilosophyStartRender != null)
            {
                btnPhilosophyStartRender.Enabled = pipelineReady && !running
                    && !_philosophyScriptGenRunning && !_philosophyScenePromptRunning;
            }

            if (btnPhilosophyStopRender != null)
            {
                btnPhilosophyStopRender.Visible = true;
                if (running)
                {
                    SetPhilosophyStopResumeButton(resumeMode: false, enabled: true);
                }
                else if (canResume)
                {
                    SetPhilosophyStopResumeButton(resumeMode: true, enabled: pipelineReady
                        && !_philosophyScriptGenRunning && !_philosophyScenePromptRunning);
                }
                else
                {
                    SetPhilosophyStopResumeButton(resumeMode: false, enabled: false);
                }
            }

            if (numPhilosophyDurationMin != null)
            {
                numPhilosophyDurationMin.Enabled = !running && !_philosophyScriptGenRunning && !_philosophyScenePromptRunning;
            }

            if (numPhilosophyDurationMax != null)
            {
                numPhilosophyDurationMax.Enabled = !running && !_philosophyScriptGenRunning && !_philosophyScenePromptRunning;
            }

            UpdatePhilosophyGenerateScriptButtonState();
        }

        private void SetPhilosophyStopResumeButton(bool resumeMode, bool enabled)
        {
            if (btnPhilosophyStopRender == null)
            {
                return;
            }

            btnPhilosophyStopRender.Text = resumeMode ? "Tiếp tục" : "Dừng";
            btnPhilosophyStopRender.Font = PhilosophyPrimaryActionFont;
            if (btnPhilosophyStopRender is JellyButton stopJelly)
            {
                stopJelly.JellyTint = resumeMode ? PhilosophyTintResume : PhilosophyTintStop;
            }
            else
            {
                btnPhilosophyStopRender.BackColor = resumeMode ? PhilosophyTintResume : PhilosophyTintStop;
            }

            ResizeAppJellyButton(
                btnPhilosophyStopRender,
                PhilosophyPrimaryActionHeight,
                140,
                AppPrimaryActionHorizontalPad);
            btnPhilosophyStopRender.Enabled = enabled;
        }

        private bool IsPhilosophyPipelineReady()
        {
            if (_systemHealth == null)
            {
                return true;
            }

            bool Ok(string key) => _systemHealth.TryGetValue(key, out var v) && v;
            // Render cần FFmpeg + lưu trữ; TTS/Gemini/Veo kiểm tra lúc bấm «Bắt đầu Render».
            return Ok("ffmpeg") && Ok("storage");
        }

        private PhilosophyRenderOptions BuildPhilosophyRenderOptions(string profileName, PhilosophyScriptItem item)
        {
            var visualMode = PhilosophyVisualModes.Normalize(item?.VisualMode ?? 0);

            return new PhilosophyRenderOptions
            {
                ProfileName = profileName,
                BRollFolder = item?.BRollFolder?.Trim() ?? string.Empty,
                MusicFolder = item?.MusicFolder?.Trim() ?? string.Empty,
                AmbientFolder = PhilosophyAmbientCatalog.ResolveAmbientFolder(item?.AmbientKey, profileName),
                SubtitleOptions = PhilosophySubtitleStyleHelper.BuildOptions(item),
                VisualMode = visualMode,
                MinDurationSeconds = (int)(numPhilosophyDurationMin?.Value ?? 15),
                MaxDurationSeconds = (int)(numPhilosophyDurationMax?.Value ?? 60),
                PreRenderedFolder = ResolveRowSceneVideoFolder(item),
                QuoteForSceneMatch = (item?.Content ?? string.Empty).Trim()
            };
        }

        private string ResolveRowSceneVideoFolder(PhilosophyScriptItem item)
        {
            var rowFolder = (item?.SceneVideoFolder ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(rowFolder))
            {
                return rowFolder;
            }

            return GetPhilosophyVideoInputFolder();
        }

        private string DescribePhilosophySceneVideoFolderTooltip(PhilosophyScriptItem item)
        {
            if (PhilosophyVisualModes.Normalize(item?.VisualMode ?? 0) != PhilosophyVisualModes.PreRendered)
            {
                return "Chỉ dùng với chế độ «3. Video nhân vật tự làm sẵn»";
            }

            var resolved = ResolveRowSceneVideoFolder(item);
            var rowFolder = (item?.SceneVideoFolder ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(rowFolder))
            {
                return "Dùng thư mục mặc định (thanh công cụ hoặc Assets\\{profile}\\philosophy-scenes\\)\r\n"
                       + resolved;
            }

            return resolved;
        }

        private static string FormatPhilosophySceneVideoFolderCell(PhilosophyScriptItem item)
        {
            if (PhilosophyVisualModes.Normalize(item?.VisualMode ?? 0) != PhilosophyVisualModes.PreRendered)
            {
                return "—";
            }

            var rowFolder = (item?.SceneVideoFolder ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(rowFolder))
            {
                return "Chọn thư mục…";
            }

            return FormatPhilosophyFolderCell(rowFolder);
        }

        private void PhilosophyBrowseRowSceneVideoFolder(int rowIndex, PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            if (PhilosophyVisualModes.Normalize(item.VisualMode) != PhilosophyVisualModes.PreRendered)
            {
                MessageBox.Show(
                    this,
                    "Cột «Thư mục video» chỉ dùng với chế độ «3. Video nhân vật tự làm sẵn».",
                    "Video Triết lý",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var profile = ResolvePhilosophyRowProfile(item);
            var defaultDir = PhilosophyProfileAssets.EnsurePreRenderedScenesDirectory(profile);
            var current = (item.SceneVideoFolder ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(current))
            {
                current = ResolveRowSceneVideoFolder(item);
            }

            var initialDir = Directory.Exists(current) ? current : defaultDir;
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Chọn thư mục chứa video phân cảnh (.mp4) cho dòng này.\r\n"
                              + "Tên file theo cột «Prompt video» (vd: tam-bat-bien-giua-dong-01.mp4).",
                SelectedPath = initialDir,
                ShowNewFolderButton = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                item.SceneVideoFolder = dlg.SelectedPath?.Trim() ?? string.Empty;
                _philosophyScriptBindingList?.ResetBindings();
                if (dgvPhilosophyScripts != null && rowIndex >= 0 && rowIndex < dgvPhilosophyScripts.Rows.Count)
                {
                    dgvPhilosophyScripts.InvalidateRow(rowIndex);
                }

                NotifyPhilosophyDraftDirty();
                LogPhilosophy("Thư mục video dòng " + GetPhilosophyRowDisplayNumber(item) + ": " + item.SceneVideoFolder);
            }
        }

        private string ResolvePhilosophyRowProfile(PhilosophyScriptItem item)
        {
            var row = (item?.ProfileName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(row))
            {
                return ProfileScopedPaths.ResolveProfileName(row);
            }

            return GetSelectedPhilosophyProfileName();
        }

        private void OpenPhilosophyOutputVideo(PhilosophyScriptItem item)
        {
            var path = (item?.OutputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                MessageBox.Show(
                    this,
                    string.IsNullOrEmpty(item?.LastError)
                        ? "Chưa có file video — render dòng này trước."
                        : "Render lỗi:\r\n" + item.LastError,
                    "Video Triết lý",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                LogPhilosophy("Mở video: " + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không mở được file:\r\n" + ex.Message, "Video Triết lý", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool TryNormalizePhilosophyDurationRange(int minSeconds, int maxSeconds, out int min, out int max, out string error)
        {
            min = Math.Max(5, Math.Min(180, minSeconds));
            max = Math.Max(5, Math.Min(180, maxSeconds));
            if (min > max)
            {
                var swap = min;
                min = max;
                max = swap;
            }

            error = string.Empty;
            if (max < min)
            {
                error = "Thời lượng tối đa phải ≥ thời lượng tối thiểu.";
                return false;
            }

            return true;
        }

        private static int CountPhilosophyWords(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            return content.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private bool TryValidatePhilosophyRenderRequest(
            List<PhilosophyScriptItem> selected,
            AppSettings settings,
            out string message)
        {
            message = string.Empty;
            if (_philosophyScriptBindingList == null || _philosophyScriptBindingList.Count == 0)
            {
                message = "Chưa có kịch bản — bấm «Tạo câu triết lý» hoặc «+ Thêm hàng» trước.";
                return false;
            }

            if (selected == null || selected.Count == 0)
            {
                message = "Chọn ít nhất một dòng trong bảng (click dòng hoặc Ctrl+click nhiều dòng) rồi bấm «Bắt đầu Render».";
                return false;
            }

            if (!PhilosophyVideoPipelineService.TryValidatePrerequisites(settings, out var preErr))
            {
                message = preErr + "\r\n\r\nMở tab Cài đặt để bổ sung.";
                return false;
            }

            if (_systemHealth != null && _systemHealth.TryGetValue("storage", out var storageOk) && !storageOk)
            {
                message = "Không ghi được thư mục lưu trữ — kiểm tra Storage trong Cài đặt.";
                return false;
            }

            if (!TryNormalizePhilosophyDurationRange(
                    (int)(numPhilosophyDurationMin?.Value ?? 15),
                    (int)(numPhilosophyDurationMax?.Value ?? 60),
                    out _,
                    out _,
                    out var durationErr))
            {
                message = durationErr;
                return false;
            }

            var issues = new List<string>();
            foreach (var item in selected)
            {
                var rowNum = GetPhilosophyRowDisplayNumber(item);
                var rowLabel = rowNum > 0 ? "Dòng " + rowNum : "Một dòng đã chọn";
                var rowProfile = ResolvePhilosophyRowProfile(item);
                var visualMode = PhilosophyVisualModes.Normalize(item?.VisualMode ?? 0);

                if (string.IsNullOrWhiteSpace(item?.Content))
                {
                    issues.Add(rowLabel + ": thiếu nội dung kịch bản (Content).");
                }

                if (visualMode == PhilosophyVisualModes.Broll)
                {
                    if (!PhilosophyBRollSelection.TryValidateSelection(item?.BRollFolder, rowProfile, out var brollErr))
                    {
                        issues.Add(rowLabel + ": " + brollErr);
                    }
                }
                else if (visualMode == PhilosophyVisualModes.PreRendered)
                {
                    var videoFolder = ResolveRowSceneVideoFolder(item);
                    if (string.IsNullOrEmpty(videoFolder) || !Directory.Exists(videoFolder))
                    {
                        issues.Add(rowLabel + ": chế độ 3 cần chọn «Thư mục video» trên dòng hoặc thanh công cụ.");
                    }
                }
                else if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
                {
                    issues.Add(rowLabel + ": chế độ «" + item.VisualModeLabel + "» cần Veo API Key + Endpoint trong Cài đặt.");
                }

                var music = item?.MusicFolder?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(music))
                {
                    var resolved = PhilosophyProfileAssets.ResolveMusicPath(music, rowProfile, settings, item?.Mood);
                    if (string.IsNullOrEmpty(resolved))
                    {
                        issues.Add(rowLabel + ": bài nhạc không tồn tại hoặc không tìm thấy: " + music);
                    }
                }
            }

            if (issues.Count > 0)
            {
                message = string.Join("\r\n\r\n", issues);
                return false;
            }

            return true;
        }

        private int GetPhilosophyRowDisplayNumber(PhilosophyScriptItem item)
        {
            if (_philosophyScriptBindingList == null || item == null)
            {
                return 0;
            }

            var idx = _philosophyScriptBindingList.IndexOf(item);
            return idx >= 0 ? idx + 1 : 0;
        }

        private void NotifyPhilosophyRenderBlocked(string message)
        {
            var text = (message ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                text = "Không đủ điều kiện để render.";
            }

            LogPhilosophy("Triết lý: " + text.Replace("\r\n", " | "));
            MessageBox.Show(this, text, "Không render được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static void ApplyMoodToProfileVoice(PhilosophyScriptItem item, AutomationProfile profile)
        {
            if (item == null || profile == null)
            {
                return;
            }

            profile.VideoStyle = (item.Mood ?? "reflective").Trim().ToLowerInvariant();
        }

        private static string TrimPhilosophyPreview(string text)
        {
            var t = (text ?? string.Empty).Replace("\r\n", " ").Trim();
            return t.Length <= 64 ? t : t.Substring(0, 61) + "…";
        }

        private void EnqueuePhilosophyJobFromResult(
            PhilosophyScriptItem item,
            PhilosophyVideoResult result,
            string profileName,
            AppSettings settings)
        {
            var payload = new PhilosophyVideoJobPayload
            {
                Content = item.Content,
                QuoteText = item.Content,
                Mood = item.Mood,
                ProfileName = profileName,
                BRollFolder = item.BRollFolder?.Trim() ?? string.Empty,
                MusicFolder = item.MusicFolder?.Trim() ?? string.Empty,
                AmbientFolder = PhilosophyAmbientCatalog.ResolveAmbientFolder(item.AmbientKey, profileName),
                StorageRootPath = settings?.StorageRootPath ?? string.Empty
            };
            var title = TrimPhilosophyPreview(item.Content);
            EnqueuePhilosophyJob(payload, "Triết lý: " + title);
        }

        // ─── Scene Prompts & Export ───────────────────────────────────────────

        private async void BtnGenerateScenePrompts_Click(object sender, EventArgs e)
        {
            if (IsPhilosophyTabBusy(out var busyReason))
            {
                MessageBox.Show(busyReason, "Tạo Prompt Phân Cảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var targets = GetPhilosophyTargetRowsFromGrid();
            if (targets.Count == 0)
            {
                MessageBox.Show(
                    "Chọn ít nhất một dòng trên lưới (hoặc bấm vào dòng cần tạo prompt).",
                    "Tạo Prompt Phân Cảnh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _philosophyScenePromptCts?.Dispose();
            _philosophyScenePromptCts = new CancellationTokenSource();
            var token = _philosophyScenePromptCts.Token;
            _philosophyScenePromptRunning = true;
            UpdatePhilosophyBusyControlStates();
            SetPhilosophyProgress("Gemini: đang sinh prompt phân cảnh…", 0, indeterminate: true);
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    MessageBox.Show("Cần AI API Key (Gemini) trong tab Cài đặt.", "Tạo Prompt Phân Cảnh",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var total = targets.Count;
                var totalScenes = 0;
                for (var rowIdx = 0; rowIdx < total; rowIdx++)
                {
                    token.ThrowIfCancellationRequested();

                    var item = targets[rowIdx];
                    var quote = (item.Content ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(quote))
                    {
                        continue;
                    }

                    // Xóa prompt cũ — tạo mới hoàn toàn
                    item.Scenes = new List<PhilosophySceneItem>();

                    SetPhilosophyProgress(
                        "Gemini phân cảnh " + (rowIdx + 1) + "/" + total + "…",
                        (int)Math.Round((rowIdx / (double)total) * 90),
                        indeterminate: false);

                    var scenes = PhilosophySceneHelper.SplitIntoScenes(quote);
                    var visualMode = PhilosophyVisualModes.Normalize(item.VisualMode);
                    var imagePath = visualMode == PhilosophyVisualModes.PreRendered
                        ? (item.BRollFolder ?? string.Empty).Trim()
                        : string.Empty;

                    LogPhilosophy("Dòng " + GetPhilosophyRowDisplayNumber(item) + ": " + scenes.Count +
                                  " phân cảnh (~8s/clip, đọc ~" +
                                  PhilosophySceneHelper.EstimateDuration(quote).ToString("0.0") + "s).");

                    if (visualMode == PhilosophyVisualModes.PreRendered
                        && (string.IsNullOrEmpty(imagePath) || !PhilosophyBRollSelection.IsImageFile(imagePath)))
                    {
                        LogPhilosophy("Dòng " + GetPhilosophyRowDisplayNumber(item) +
                                      ": mode 3 cần chọn ảnh nền ở cột «Nền» trước khi sinh prompt.");
                        continue;
                    }

                    try
                    {
                        scenes = await PhilosophySceneHelper.GenerateScenePromptsAsync(
                            quote,
                            scenes,
                            settings,
                            token,
                            imagePath,
                            item.Mood).ConfigureAwait(true);
                        item.Scenes = scenes;
                        totalScenes += scenes.Count;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogPhilosophy("Gemini phân cảnh lỗi dòng " + GetPhilosophyRowDisplayNumber(item) + ": " + ex.Message);
                    }
                }

                if (dgvPhilosophyScripts != null)
                {
                    dgvPhilosophyScripts.Refresh();
                }

                NotifyPhilosophyDraftDirty();
                FlushPhilosophyDraftToDisk();
                LogPhilosophy("Phân cảnh: " + totalScenes + " cảnh mới từ " + targets.Count + " dòng đã chọn.");
                SetPhilosophyProgress("Đã sinh " + totalScenes + " phân cảnh", 100);
            }
            catch (OperationCanceledException)
            {
                LogPhilosophy("Triết lý: đã dừng tạo prompt phân cảnh.");
                SetPhilosophyProgress("Đã dừng", 0);
                NotifyPhilosophyDraftDirty();
            }
            catch (Exception ex)
            {
                LogPhilosophy("Lỗi tạo prompt phân cảnh: " + ex.Message);
                SetPhilosophyProgress("lỗi", 0);
            }
            finally
            {
                _philosophyScenePromptRunning = false;
                _philosophyScenePromptCts?.Dispose();
                _philosophyScenePromptCts = null;
                UpdatePhilosophyBusyControlStates();
            }
        }

        private void BtnExportExcelPrompts_Click(object sender, EventArgs e)
        {
            var selectedItems = GetPhilosophyTargetRowsFromGrid();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Chọn ít nhất một dòng trên lưới để xuất prompt.",
                    "Tải Excel Prompt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var scenes = new List<PhilosophySceneItem>();
            foreach (var item in selectedItems)
            {
                if (item?.Scenes != null && item.Scenes.Count > 0)
                {
                    scenes.AddRange(item.Scenes);
                    continue;
                }

                var quote = (item?.Content ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(quote))
                {
                    scenes.AddRange(PhilosophySceneHelper.SplitIntoScenes(quote));
                }
            }

            if (scenes.Count == 0)
            {
                MessageBox.Show(
                    "Các dòng đã chọn chưa có phân cảnh. Bấm «Tạo Prompt Phân Cảnh» trước.",
                    "Tải Excel Prompt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = "Lưu Excel Prompt",
                Filter = "CSV UTF-8|*.csv|Tất cả|*.*",
                FileName = "philosophy_scene_prompts_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv",
                DefaultExt = "csv"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var savedPath = PhilosophySceneHelper.ExportToCsv(dlg.FileName, scenes);
                    LogPhilosophy("Đã xuất CSV (" + selectedItems.Count + " dòng, " + scenes.Count + " cảnh): " + savedPath);
                    MessageBox.Show(
                        "Đã lưu file CSV:\r\n" + savedPath +
                        "\r\n\r\n(" + selectedItems.Count + " dòng, " + scenes.Count + " phân cảnh)\r\n" +
                        "Mở bằng Excel (Data → From Text/CSV, chọn UTF-8).",
                        "Tải Excel Prompt",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + savedPath + "\""); }
                    catch { /* ignore */ }
                }
                catch (Exception ex)
                {
                    LogPhilosophy("Lỗi xuất CSV: " + ex.Message);
                    MessageBox.Show("Lỗi lưu file:\r\n" + ex.Message, "Tải Excel Prompt",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetPhilosophyVideoInputFolder()
        {
            var text = (txtPhilosophyVideoInputFolder?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            return ResolvePhilosophyPreRenderedScenesDirectory();
        }

        private static string TrimPhilosophyPreviewN(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var t = text.Trim();
            return t.Length <= maxLen ? t : t.Substring(0, maxLen) + "…";
        }
    }
}
