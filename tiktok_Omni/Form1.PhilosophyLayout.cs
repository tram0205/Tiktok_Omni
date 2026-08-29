using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Helpers;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font PhilosophyUiFont = AppLabelFont;
        private static readonly Font PhilosophyCommandFont = AppJellyButtonFont;
        private static readonly Color PhilosophyPanelBack = Color.FromArgb(31, 34, 42);
        private static readonly Color PhilosophyChromeBack = Color.FromArgb(36, 39, 48);
        private static readonly Color PhilosophyConfigBarBack = Color.FromArgb(33, 36, 44);
        private static readonly Color PhilosophyTintGenerate = Color.FromArgb(76, 110, 245);
        private static readonly Color PhilosophyTintRender = Color.FromArgb(56, 158, 88);
        private static readonly Color PhilosophyTintSecondary = Color.FromArgb(88, 94, 112);
        private static readonly Color PhilosophyTintStop = Color.FromArgb(170, 72, 72);
        private static readonly Color PhilosophyTintResume = Color.FromArgb(68, 118, 178);
        private static readonly Color PhilosophyTintAddRow = Color.FromArgb(52, 92, 158);
        private static readonly Color PhilosophyTintNeutral = Color.FromArgb(68, 72, 86);
        private static readonly Color PhilosophyTintLinkSession = Color.FromArgb(56, 96, 128);
        private static readonly Color PhilosophyTintDanger = Color.FromArgb(168, 52, 52);
        private static readonly Color PhilosophyTintTrash = Color.FromArgb(88, 92, 72);
        private static readonly Color PhilosophyTintBrollLibrary = Color.FromArgb(56, 108, 88);
        private static readonly Color PhilosophyTintMascotLibrary = Color.FromArgb(100, 72, 190);
        private static readonly Color PhilosophyTintZoomLibrary = Color.FromArgb(72, 138, 118);
        private static readonly Color PhilosophyTintMusicLibrary = Color.FromArgb(138, 58, 118);
        private static readonly Color PhilosophyTintSfxLibrary = Color.FromArgb(168, 118, 42);
        private static readonly Color PhilosophyTintLogoLibrary = Color.FromArgb(72, 118, 168);
        private static readonly Padding PhilosophySolidButtonMargin = new Padding(4, 2, 4, 2);
        private static readonly Padding PhilosophyFlowItemMargin = new Padding(4, 4, 10, 6);
        private static readonly Padding PhilosophyFlowSectionMargin = new Padding(0, 0, 18, 4);
        private const int PhilosophyCommandButtonHeight = AppJellyButtonHeight;
        private const int PhilosophyCommandHorizontalPad = AppJellyButtonHorizontalPad;
        private const int PhilosophyStatusPanelHeight = 276;
        private const int PhilosophyStatusPanelMinHeight = 236;
        private const float PhilosophyLogFontSize = 10F;
        private const int PhilosophyLogLineSpacing = 6;
        private const int PhilosophyLogHeaderRowHeight = 64;
        private const int PhilosophyClearLogButtonWidth = 160;
        private const int PhilosophyPrereqLabelRowHeight = 56;
        private const int PhilosophyRenderBarHeight = AppPrimaryActionHeight + 24;

        private Panel pnlPhilosophyTopChrome;
        private Panel pnlPhilosophyTabTitleHost;
        private ShowcaseTabTitleLabel lblPhilosophyTabTitle;
        private Panel pnlPhilosophyCommandBar;
        private FlowLayoutPanel flpPhilosophyLibraryCenter;
        private FlowLayoutPanel flpPhilosophyRowManage;
        private Panel pnlPhilosophyMainFill;
        private Panel pnlPhilosophyGridWrap;
        private Panel pnlPhilosophyRenderHost;
        private FlowLayoutPanel flpPhilosophyRenderCenter;
        private Panel pnlPhilosophyRenderStopHost;
        private FlowLayoutPanel flpPhilosophyRenderStopRight;
        private Panel pnlPhilosophyStatus;
        private Button btnPhilosophyAddRow;
        private Button btnPhilosophyCopyRow;
        private Button btnPhilosophyRestoreSessions;
        private Button btnPhilosophyDeleteRow;
        private Button btnPhilosophyTrash;
        private Button btnPhilosophyMoveRowUp;
        private Button btnPhilosophyMoveRowDown;
        private Button btnPhilosophyBrollLibrary;
        private Button btnPhilosophyMascotImageLibrary;
        private Button btnPhilosophyZoomImageLibrary;
        private Button btnPhilosophyMusicLibrary;
        private Button btnPhilosophySfxLibrary;
        private Button btnPhilosophyLogoLibrary;
        private DataGridView dgvPhilosophyScripts;
        private BindingList<PhilosophyBatchItem> _philosophyBatchBindingList;
        private DataGridViewComboBoxColumn _colPhilosophyProfile;
        private Button btnPhilosophyGenerateContent;
        private Button btnPhilosophyStartRender;
        private Button btnPhilosophyStopRender;
        private Button btnPhilosophyStopAll;
        private CancellationTokenSource _philosophyScriptGenCts;
        private CancellationTokenSource _philosophyScenePromptCts;
        private bool _philosophyScriptGenRunning;
        private bool _philosophyScenePromptRunning;
        private CancellationTokenSource _philosophyRenderCts;
        private bool _philosophyRenderRunning;
        private bool _philosophyRenderPaused;
        private List<PhilosophyRenderQueueEntry> _philosophyRenderPending;
        private ContextMenuStrip _cmsPhilosophyGrid;

        /// <summary>Tab Video Quote — layout giống Video reup (Top toolbar / Fill grid / Bottom actions + log).</summary>
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
            InitializePhilosophyTrashMaintenance();
            _ = LoadPhilosophyProfileComboAsync();
        }

        private async Task LoadPhilosophyProfileComboAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                _philosophySettingsSnap = settings;
                PhilosophyVideoPipelineService.EnsureFinishedProductLayout(settings);
                RefreshPhilosophyProfileCombo(settings);
            }
            catch
            {
                RefreshPhilosophyProfileCombo(new AppSettings());
            }
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

            _philosophyBatchBindingList ??= new BindingList<PhilosophyBatchItem>();
            dgvPhilosophyScripts = new DataGridView
            {
                Name = "dgvPhilosophyScripts",
                DataSource = _philosophyBatchBindingList,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnEnter,
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
            ConfigurePhilosophyBatchGrid();
            WirePhilosophyBatchGridEvents();
            ApplyGridProfileComboColumn(dgvPhilosophyScripts, "colPhilosophyProfile");
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
                    Height = PhilosophyPrereqLabelRowHeight,
                    ForeColor = Color.FromArgb(255, 180, 120),
                    Font = PhilosophyUiFont,
                    Padding = new Padding(4, 6, 4, 4),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseCompatibleTextRendering = true
                };
            }

            lblPhilosophyPrereq.AutoSize = false;
            lblPhilosophyPrereq.Height = PhilosophyPrereqLabelRowHeight;
            dgvPhilosophyScripts.Margin = Padding.Empty;
            dgvPhilosophyScripts.MinimumSize = new Size(120, 80);
            dgvPhilosophyScripts.Dock = DockStyle.Fill;

            ApplyTopFillBottomDockLayout(pnlPhilosophyGridWrap, dgvPhilosophyScripts, bottom: null, lblPhilosophyPrereq);
            ApplyAppGridChrome(dgvPhilosophyScripts);
        }

        private void BuildPhilosophyTopChrome()
        {
            BuildPhilosophyCommandBar();
            BuildPhilosophyTabTitleRow();

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

            pnlPhilosophyCommandBar.Dock = DockStyle.Top;
            pnlPhilosophyTabTitleHost.Dock = DockStyle.Top;
            pnlPhilosophyTopChrome.Controls.Add(pnlPhilosophyCommandBar);
            pnlPhilosophyTopChrome.Controls.Add(pnlPhilosophyTabTitleHost);
        }

        private void BuildPhilosophyTabTitleRow()
        {
            pnlPhilosophyTabTitleHost = new Panel
            {
                Name = "pnlPhilosophyTabTitleHost",
                AutoSize = false,
                Height = 64,
                Margin = new Padding(0, 0, 0, 8),
                Padding = Padding.Empty,
                BackColor = PhilosophyPanelBack
            };

            var accent = new Panel
            {
                Name = "pnlPhilosophyTitleAccent",
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = Color.FromArgb(210, 158, 32)
            };

            lblPhilosophyTabTitle = new ShowcaseTabTitleLabel
            {
                Name = "lblPhilosophyTabTitle",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                TitleText = "VIDEO QUOTE"
            };

            pnlPhilosophyTabTitleHost.Controls.Add(accent);
            pnlPhilosophyTabTitleHost.Controls.Add(lblPhilosophyTabTitle);
        }

        private void BuildPhilosophyCommandBar()
        {
            pnlPhilosophyCommandBar = new Panel
            {
                Name = "pnlPhilosophyCommandBar",
                Dock = DockStyle.Top,
                AutoSize = false,
                MinimumSize = new Size(0, AppJellyButtonHeight * 2 + 18),
                Height = AppJellyButtonHeight * 2 + 18,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = PhilosophyChromeBack
            };
            pnlPhilosophyCommandBar.Resize += (_, __) => LayoutPhilosophyCommandBar();

            flpPhilosophyLibraryCenter = CreatePhilosophyToolbarFlowPanel("flpPhilosophyLibraryCenter");
            flpPhilosophyRowManage = CreatePhilosophyToolbarFlowPanel("flpPhilosophyRowManage");

            btnPhilosophyAddRow = CreatePhilosophySolidRectButton("btnPhilosophyAddRow", "+ Thêm batch", PhilosophyTintAddRow, 128);
            btnPhilosophyAddRow.Click -= btnPhilosophyAddRow_Click;
            btnPhilosophyAddRow.Click += btnPhilosophyAddRow_Click;

            btnPhilosophyCopyRow = CreatePhilosophySolidRectButton("btnPhilosophyCopyRow", "📋 Copy batch", PhilosophyTintNeutral, 128);
            btnPhilosophyCopyRow.Click -= btnPhilosophyCopyRow_Click;
            btnPhilosophyCopyRow.Click += btnPhilosophyCopyRow_Click;

            btnPhilosophyRestoreSessions = CreatePhilosophySolidRectButton(
                "btnPhilosophyRestoreSessions", "🔗 Gắn phiên", PhilosophyTintLinkSession, 118);
            btnPhilosophyRestoreSessions.Click -= btnPhilosophyRestoreSessions_Click;
            btnPhilosophyRestoreSessions.Click += btnPhilosophyRestoreSessions_Click;

            btnPhilosophyDeleteRow = CreatePhilosophySolidRectButton("btnPhilosophyDeleteRow", "🗑 Xoá batch", PhilosophyTintDanger, 118);
            btnPhilosophyDeleteRow.Click -= btnPhilosophyDeleteRow_Click;
            btnPhilosophyDeleteRow.Click += btnPhilosophyDeleteRow_Click;

            btnPhilosophyTrash = CreatePhilosophySolidRectButton("btnPhilosophyTrash", "♻ Thùng rác", PhilosophyTintTrash, 118);
            btnPhilosophyTrash.Click -= btnPhilosophyTrash_Click;
            btnPhilosophyTrash.Click += btnPhilosophyTrash_Click;

            btnPhilosophyMoveRowUp = CreatePhilosophyArrowButton("btnPhilosophyMoveRowUp", "↑");
            btnPhilosophyMoveRowUp.Click -= btnPhilosophyMoveRowUp_Click;
            btnPhilosophyMoveRowUp.Click += btnPhilosophyMoveRowUp_Click;

            btnPhilosophyMoveRowDown = CreatePhilosophyArrowButton("btnPhilosophyMoveRowDown", "↓");
            btnPhilosophyMoveRowDown.Click -= btnPhilosophyMoveRowDown_Click;
            btnPhilosophyMoveRowDown.Click += btnPhilosophyMoveRowDown_Click;

            if (btnPhilosophyStopAll == null || btnPhilosophyStopAll.IsDisposed)
            {
                btnPhilosophyStopAll = CreatePhilosophyCommandButton(
                    "btnPhilosophyStopAll", "Dừng", PhilosophyTintStop);
                btnPhilosophyStopAll.Visible = false;
            }

            btnPhilosophyStopAll.Click -= BtnPhilosophyStopAll_Click;
            btnPhilosophyStopAll.Click += BtnPhilosophyStopAll_Click;

            btnPhilosophyBrollLibrary = CreatePhilosophyJellyButton(
                "btnPhilosophyBrollLibrary",
                "📁 Thư viện B-roll",
                PhilosophyTintBrollLibrary,
                200);
            btnPhilosophyBrollLibrary.Click -= btnPhilosophyBrollLibrary_Click;
            btnPhilosophyBrollLibrary.Click += btnPhilosophyBrollLibrary_Click;

            btnPhilosophyMascotImageLibrary = CreatePhilosophyJellyButton(
                "btnPhilosophyMascotImageLibrary",
                "🖼 Thư viện Ảnh mascot",
                PhilosophyTintMascotLibrary,
                220);
            btnPhilosophyMascotImageLibrary.Click -= btnPhilosophyMascotImageLibrary_Click;
            btnPhilosophyMascotImageLibrary.Click += btnPhilosophyMascotImageLibrary_Click;

            btnPhilosophyZoomImageLibrary = CreatePhilosophyJellyButton(
                "btnPhilosophyZoomImageLibrary",
                "🔍 Thư viện ảnh zoom",
                PhilosophyTintZoomLibrary,
                220);
            btnPhilosophyZoomImageLibrary.Click -= btnPhilosophyZoomImageLibrary_Click;
            btnPhilosophyZoomImageLibrary.Click += btnPhilosophyZoomImageLibrary_Click;

            btnPhilosophyMusicLibrary = CreatePhilosophyJellyButton(
                "btnPhilosophyMusicLibrary",
                "🎵 Thư viện nhạc nền",
                PhilosophyTintMusicLibrary,
                200);
            btnPhilosophyMusicLibrary.Click -= btnPhilosophyMusicLibrary_Click;
            btnPhilosophyMusicLibrary.Click += btnPhilosophyMusicLibrary_Click;

            btnPhilosophySfxLibrary = CreatePhilosophyJellyButton(
                "btnPhilosophySfxLibrary",
                "🔊 Hiệu ứng âm thanh",
                PhilosophyTintSfxLibrary,
                200);
            btnPhilosophySfxLibrary.Click -= btnPhilosophySfxLibrary_Click;
            btnPhilosophySfxLibrary.Click += btnPhilosophySfxLibrary_Click;

            btnPhilosophyLogoLibrary = CreatePhilosophyJellyButton(
                "btnPhilosophyLogoLibrary",
                "🏷 Thư viện logo",
                PhilosophyTintLogoLibrary,
                180);
            btnPhilosophyLogoLibrary.Click -= btnPhilosophyLogoLibrary_Click;
            btnPhilosophyLogoLibrary.Click += btnPhilosophyLogoLibrary_Click;

            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophyBrollLibrary);
            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophyMascotImageLibrary);
            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophyZoomImageLibrary);
            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophyMusicLibrary);
            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophySfxLibrary);
            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophyLogoLibrary);
            flpPhilosophyLibraryCenter.Controls.Add(btnPhilosophyStopAll);

            flpPhilosophyRowManage.Controls.Add(btnPhilosophyAddRow);
            flpPhilosophyRowManage.Controls.Add(btnPhilosophyCopyRow);
            flpPhilosophyRowManage.Controls.Add(btnPhilosophyRestoreSessions);
            flpPhilosophyRowManage.Controls.Add(btnPhilosophyDeleteRow);
            flpPhilosophyRowManage.Controls.Add(btnPhilosophyTrash);
            flpPhilosophyRowManage.Controls.Add(btnPhilosophyMoveRowUp);
            flpPhilosophyRowManage.Controls.Add(btnPhilosophyMoveRowDown);

            pnlPhilosophyCommandBar.Controls.Add(flpPhilosophyLibraryCenter);
            pnlPhilosophyCommandBar.Controls.Add(flpPhilosophyRowManage);
            LayoutPhilosophyCommandBar();
        }

        private void LayoutPhilosophyCommandBar()
        {
            if (pnlPhilosophyCommandBar == null
                || flpPhilosophyLibraryCenter == null
                || flpPhilosophyRowManage == null)
            {
                return;
            }

            if (pnlPhilosophyCommandBar.Width <= 0 || pnlPhilosophyCommandBar.Height <= 0)
            {
                return;
            }

            var rowHeight = AppJellyButtonHeight + 6;

            flpPhilosophyLibraryCenter.PerformLayout();
            var libraryX = Math.Max(0, (pnlPhilosophyCommandBar.ClientSize.Width - flpPhilosophyLibraryCenter.Width) / 2);
            flpPhilosophyLibraryCenter.Location = new Point(libraryX, 0);
            flpPhilosophyLibraryCenter.BringToFront();

            flpPhilosophyRowManage.PerformLayout();
            flpPhilosophyRowManage.Location = new Point(0, rowHeight);
            flpPhilosophyRowManage.BringToFront();
        }

        private static FlowLayoutPanel CreatePhilosophyToolbarFlowPanel(string name)
        {
            return new FlowLayoutPanel
            {
                Name = name,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.None,
                Padding = new Padding(4, 2, 4, 2),
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
        }

        private void BuildPhilosophyRenderActionBar()
        {
            if (btnPhilosophyGenerateContent == null || btnPhilosophyGenerateContent.IsDisposed)
            {
                btnPhilosophyGenerateContent = CreatePhilosophyJellyButton(
                    "btnPhilosophyGenerateContent",
                    "Tạo nội dung Gemini",
                    PhilosophyTintGenerate,
                    200);
            }

            btnPhilosophyGenerateContent.Click -= btnPhilosophyGenerateScript_Click;
            btnPhilosophyGenerateContent.Click += btnPhilosophyGenerateScript_Click;
            btnPhilosophyGenerateContent.Margin = new Padding(0, 8, 10, 0);

            if (btnPhilosophyStartRender == null || btnPhilosophyStartRender.IsDisposed)
            {
                btnPhilosophyStartRender = CreateAppPrimaryJellyButton(
                    "btnPhilosophyStartRender",
                    "Render video",
                    PhilosophyTintRender);
            }

            btnPhilosophyStartRender.Click -= btnPhilosophyStartRender_Click;
            btnPhilosophyStartRender.Click += btnPhilosophyStartRender_Click;

            if (btnPhilosophyStopRender == null || btnPhilosophyStopRender.IsDisposed)
            {
                btnPhilosophyStopRender = CreatePhilosophyCommandButton(
                    "btnPhilosophyStopRender",
                    "Dừng lại",
                    PhilosophyTintStop);
            }

            btnPhilosophyStopRender.Click -= btnPhilosophyStopRender_Click;
            btnPhilosophyStopRender.Click += btnPhilosophyStopRender_Click;

            if (btnPhilosophyPushToAutoPost == null || btnPhilosophyPushToAutoPost.IsDisposed)
            {
                btnPhilosophyPushToAutoPost = CreatePhilosophyCommandButton(
                    "btnPhilosophyPushToAutoPost",
                    "Đăng tự động",
                    Color.FromArgb(68, 130, 105));
            }

            btnPhilosophyPushToAutoPost.Click -= btnPhilosophyPushToAutoPost_Click;
            btnPhilosophyPushToAutoPost.Click += btnPhilosophyPushToAutoPost_Click;

            ApplyPhilosophyCommandButtonMetrics(btnPhilosophyPushToAutoPost);

            ApplyPhilosophyPrimaryRenderButtonMetrics(btnPhilosophyStartRender);
            if (btnPhilosophyStartRender is JellyButton startRenderJelly)
            {
                startRenderJelly.JellyTint = PhilosophyTintRender;
            }

            btnPhilosophyStartRender.Margin = new Padding(0, 8, 10, 0);
            btnPhilosophyPushToAutoPost.Margin = new Padding(0, 8, 0, 0);

            btnPhilosophyStopRender.Visible = true;
            ApplyPhilosophyStopRenderButtonUi(resumeMode: false, enabled: false);
            btnPhilosophyStopRender.Margin = new Padding(0, 8, 0, 0);

            var barColor = Color.FromArgb(28, 30, 38);
            pnlPhilosophyRenderHost = new Panel
            {
                Name = "pnlPhilosophyRenderHost",
                Dock = DockStyle.Bottom,
                Height = PhilosophyRenderBarHeight,
                MinimumSize = new Size(0, PhilosophyRenderBarHeight),
                Padding = new Padding(8),
                BackColor = barColor
            };
            pnlPhilosophyRenderHost.Resize += (_, __) => LayoutPhilosophyRenderRow();

            var tblRenderCenter = new TableLayoutPanel
            {
                Name = "tblPhilosophyRenderCenter",
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = barColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblRenderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblRenderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblRenderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblRenderCenter.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            flpPhilosophyRenderCenter = CreatePhilosophyToolbarFlowPanel("flpPhilosophyRenderCenter");
            flpPhilosophyRenderCenter.Dock = DockStyle.None;
            flpPhilosophyRenderCenter.WrapContents = false;
            flpPhilosophyRenderCenter.Controls.Add(btnPhilosophyGenerateContent);
            flpPhilosophyRenderCenter.Controls.Add(btnPhilosophyStartRender);
            flpPhilosophyRenderCenter.Controls.Add(btnPhilosophyPushToAutoPost);

            pnlPhilosophyRenderStopHost = new Panel
            {
                Name = "pnlPhilosophyRenderStopHost",
                Dock = DockStyle.Fill,
                BackColor = barColor,
                Margin = Padding.Empty
            };

            flpPhilosophyRenderStopRight = CreatePhilosophyToolbarFlowPanel("flpPhilosophyRenderStopRight");
            flpPhilosophyRenderStopRight.Dock = DockStyle.None;
            flpPhilosophyRenderStopRight.WrapContents = false;
            flpPhilosophyRenderStopRight.Controls.Add(btnPhilosophyStopRender);
            pnlPhilosophyRenderStopHost.Controls.Add(flpPhilosophyRenderStopRight);

            tblRenderCenter.Controls.Add(flpPhilosophyRenderCenter, 1, 0);
            tblRenderCenter.Controls.Add(pnlPhilosophyRenderStopHost, 2, 0);
            pnlPhilosophyRenderHost.Controls.Add(tblRenderCenter);
            LayoutPhilosophyRenderRow();
            UpdatePhilosophyRenderControlStates();
        }

        private void LayoutPhilosophyRenderRow()
        {
            if (pnlPhilosophyRenderStopHost == null || flpPhilosophyRenderStopRight == null)
            {
                return;
            }

            if (pnlPhilosophyRenderStopHost.Width <= 0 || pnlPhilosophyRenderStopHost.Height <= 0)
            {
                return;
            }

            flpPhilosophyRenderStopRight.PerformLayout();
            var stopX = Math.Max(0, pnlPhilosophyRenderStopHost.ClientSize.Width - flpPhilosophyRenderStopRight.Width);
            var stopY = Math.Max(0, (pnlPhilosophyRenderStopHost.ClientSize.Height - flpPhilosophyRenderStopRight.Height) / 2);
            flpPhilosophyRenderStopRight.Location = new Point(stopX, stopY);
            flpPhilosophyRenderStopRight.BringToFront();
        }

        private void BuildPhilosophyStatusPanel()
        {
            if (btnPhilosophyClearLog == null || btnPhilosophyClearLog.IsDisposed)
            {
                btnPhilosophyClearLog = new Button
                {
                    Name = "btnPhilosophyClearLog",
                    Text = "Xóa log",
                    Size = new Size(PhilosophyClearLogButtonWidth, 24),
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
            }

            ConfigurePhilosophyLogTextBox(rtbPhilosophyLog);
            rtbPhilosophyLog.Dock = DockStyle.Fill;

            pnlPhilosophyStatus = new Panel
            {
                Name = "pnlPhilosophyStatus",
                Dock = DockStyle.Bottom,
                Height = PhilosophyStatusPanelHeight,
                MinimumSize = new Size(0, PhilosophyStatusPanelMinHeight),
                BackColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };

            var tblLog = new TableLayoutPanel
            {
                Name = "tblPhilosophyLog",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            tblLog.RowStyles.Add(new RowStyle(SizeType.Absolute, PhilosophyLogHeaderRowHeight));
            tblLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 4, 4) };
            var lblLog = new Label
            {
                Text = "Nhật ký",
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = PhilosophyUiFont,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 2, 0, 2)
            };
            btnPhilosophyClearLog.Dock = DockStyle.Right;
            btnPhilosophyClearLog.Width = PhilosophyClearLogButtonWidth;
            btnPhilosophyClearLog.MinimumSize = new Size(PhilosophyClearLogButtonWidth, 36);
            btnPhilosophyClearLog.Margin = new Padding(4, 0, 0, 0);
            header.Controls.Add(lblLog);
            header.Controls.Add(btnPhilosophyClearLog);

            tblLog.Controls.Add(header, 0, 0);
            tblLog.Controls.Add(rtbPhilosophyLog, 0, 1);

            pnlPhilosophyStatus.Controls.Add(tblLog);
        }

        private static void ConfigurePhilosophyLogTextBox(RichTextBox rtb)
        {
            if (rtb == null || rtb.IsDisposed)
            {
                return;
            }

            rtb.Font = new Font("Segoe UI", PhilosophyLogFontSize, FontStyle.Regular, GraphicsUnit.Point);
            rtb.Margin = new Padding(0, 4, 0, 8);
            rtb.Padding = new Padding(2, 4, 2, 4);
            rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
            ApplyPhilosophyLogLineSpacing(rtb);
        }

        private static void ApplyPhilosophyLogLineSpacing(RichTextBox rtb)
        {
            if (rtb == null || rtb.IsDisposed)
            {
                return;
            }

            rtb.SelectAll();
            rtb.SelectionCharOffset = PhilosophyLogLineSpacing;
            rtb.SelectionLength = 0;
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

        private static Button CreatePhilosophyJellyButton(string name, string text, Color tint, int minWidth)
        {
            var btn = CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: PhilosophyCommandButtonHeight,
                minWidth: minWidth,
                horizontalPad: PhilosophyCommandHorizontalPad,
                margin: PhilosophySolidButtonMargin);
            ApplyPhilosophyJellyChrome(btn);
            return btn;
        }

        private static Button CreatePhilosophySolidRectButton(string name, string text, Color back, int minWidth)
        {
            var height = PhilosophyCommandButtonHeight;
            var textW = TextRenderer.MeasureText(
                text,
                PhilosophyCommandFont,
                new Size(int.MaxValue, height),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            var width = Math.Max(minWidth, textW + 28);
            var border = ControlPaint.Dark(back);
            var btn = new ShowcaseSolidRectButton
            {
                Name = name,
                Text = text,
                Font = PhilosophyCommandFont,
                NormalBackColor = back,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Height = height,
                Width = width,
                MinimumSize = new Size(width, height),
                MaximumSize = new Size(width, height),
                Margin = PhilosophySolidButtonMargin,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.MouseOverBackColor = BlendPhilosophyColor(back, Color.White, 0.12f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.DarkDark(back);
            return btn;
        }

        private static Button CreatePhilosophyArrowButton(string name, string arrow)
        {
            const int width = 34;
            var height = PhilosophyCommandButtonHeight;
            var btn = new Button
            {
                Name = name,
                Text = arrow,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
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

        private static Color BlendPhilosophyColor(Color baseColor, Color overlay, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            var r = (int)(baseColor.R + (overlay.R - baseColor.R) * amount);
            var g = (int)(baseColor.G + (overlay.G - baseColor.G) * amount);
            var b = (int)(baseColor.B + (overlay.B - baseColor.B) * amount);
            return Color.FromArgb(baseColor.A, r, g, b);
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

        private static void ApplyPhilosophyCommandButtonMetrics(Button btn, int minWidth = 96)
        {
            if (btn == null)
            {
                return;
            }

            ResizeAppJellyButton(btn, PhilosophyCommandButtonHeight, minWidth, PhilosophyCommandHorizontalPad);
            btn.Margin = PhilosophyFlowItemMargin;
            ApplyPhilosophyJellyChrome(btn);
        }

        private static void ApplyPhilosophyPrimaryRenderButtonMetrics(Button btn, int minWidth = AppPrimaryActionMinWidth)
        {
            if (btn == null)
            {
                return;
            }

            ResizeAppJellyButton(btn, AppPrimaryActionHeight, minWidth, AppPrimaryActionHorizontalPad);
            ApplyPhilosophyJellyChrome(btn);
        }

        private static void ApplyPhilosophyJellyChrome(Button btn)
        {
            if (btn is JellyButton jelly)
            {
                jelly.JellyFillOpacity = 1f - JellyButton.DefaultTransparency;
                jelly.ForeColor = AppJellyButtonForeColor;
                jelly.Tag = JellyButton.ChromeTag;
                jelly.AccessibleName = JellyButton.ChromeTag;
            }
        }

        private void btnPhilosophyAddRow_Click(object sender, EventArgs e)
        {
            PhilosophyAddRow();
        }

        private void btnPhilosophyCopyRow_Click(object sender, EventArgs e)
        {
            PhilosophyCopySelectedBatches();
        }

        private void btnPhilosophyRestoreSessions_Click(object sender, EventArgs e)
        {
            PhilosophyRestoreSessionsFromDisk();
        }

        private void btnPhilosophyDeleteRow_Click(object sender, EventArgs e)
        {
            PhilosophyDeleteSelectedRows();
        }

        private void btnPhilosophyTrash_Click(object sender, EventArgs e)
        {
            PhilosophyOpenTrash();
        }

        private void btnPhilosophyMoveRowUp_Click(object sender, EventArgs e)
        {
            PhilosophyMoveSelectedBatch(-1);
        }

        private void btnPhilosophyMoveRowDown_Click(object sender, EventArgs e)
        {
            PhilosophyMoveSelectedBatch(1);
        }

        private void btnPhilosophyBrollLibrary_Click(object sender, EventArgs e)
        {
            OpenPhilosophyAssetLibraryFolder(
                PhilosophyProfileAssets.EnsureBrollLibraryDirectory(),
                "Thư viện B-roll",
                "Kho chung Assets\\Backgrounds — mọi profile dùng chung. Copy file .mp4 vào đây, rồi chọn cột «Nền» hoặc «Ngẫu nhiên».");
        }

        private void btnPhilosophyMascotImageLibrary_Click(object sender, EventArgs e)
        {
            OpenPhilosophyAssetLibraryFolder(
                PhilosophyProfileAssets.EnsureMascotImageLibraryDirectory(GetSelectedPhilosophyProfileName()),
                "Thư viện Ảnh mascot",
                "Copy ảnh .jpg/.png/.webp — dùng cho chế độ AI I2V / ảnh tham chiếu phân cảnh.");
        }

        private void btnPhilosophyZoomImageLibrary_Click(object sender, EventArgs e)
        {
            OpenPhilosophyAssetLibraryFolder(
                PhilosophyProfileAssets.EnsureZoomImageLibraryDirectory(GetSelectedPhilosophyProfileName()),
                "Thư viện ảnh zoom",
                "Copy ảnh .jpg/.png/.webp vào đây — Gemini gợi ý zoom_images; chọn «Zoom ảnh» trong popup Nền.");
        }

        private void btnPhilosophyMusicLibrary_Click(object sender, EventArgs e)
        {
            OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind.BackgroundMusic);
        }

        private void btnPhilosophySfxLibrary_Click(object sender, EventArgs e)
        {
            OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind.SoundEffects);
        }

        private void btnPhilosophyLogoLibrary_Click(object sender, EventArgs e)
        {
            OpenShowcaseLogoLibraryAsync();
        }

        private void OpenPhilosophyAssetLibraryFolder(string folder, string title, string hint)
        {
            try
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder)
                {
                    UseShellExecute = true
                });
                LogPhilosophy("Đã mở " + title + ": " + folder);
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    LogPhilosophy(hint);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Không mở được thư mục:\r\n" + ex.Message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void PhilosophyAddRow()
        {
            PhilosophyAddBatch();
        }

        private List<PhilosophyScriptItem> GetPhilosophyTargetRowsFromGrid()
        {
            return GetPhilosophyTargetQuotesFromSelectedBatches();
        }

        private void PhilosophyDeleteSelectedRows()
        {
            PhilosophyDeleteSelectedBatches();
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
                MessageBox.Show(busyReason, "Video Quote", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var batches = GetPhilosophyTargetBatchesFromGrid();
            if (batches.Count == 0)
            {
                MessageBox.Show(
                    "Chọn ít nhất một batch trên lưới (hoặc bấm «+ Thêm batch») trước.",
                    "Tạo nội dung Gemini",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var batchesWithTopic = batches
                .Where(b => b != null && !string.IsNullOrWhiteSpace(b.Topic))
                .ToList();
            if (batchesWithTopic.Count == 0)
            {
                MessageBox.Show(
                    "Nhập chủ đề trong cột «Chủ đề» (bấm mở popup) cho batch đã chọn trước khi chạy Gemini.",
                    "Tạo nội dung Gemini",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (batchesWithTopic.Count < batches.Count)
            {
                LogPhilosophy("Gemini: bỏ qua "
                              + (batches.Count - batchesWithTopic.Count)
                              + " batch không có chủ đề.");
            }

            _philosophyScriptGenCts?.Dispose();
            _philosophyScriptGenCts = new CancellationTokenSource();
            _philosophyScriptGenRunning = true;
            UpdatePhilosophyBusyControlStates();
            SetPhilosophyProgress("Gemini: đang tạo nội dung…", 0, indeterminate: true);
            try
            {
                var totalQuotes = await GeneratePhilosophyContentForBatchesAsync(
                    batchesWithTopic,
                    _philosophyScriptGenCts.Token,
                    (index, total, topic) =>
                    {
                        SetPhilosophyProgress(
                            "Gemini [" + index + "/" + total + "]: «" + TrimPhilosophyPreview(topic) + "»…",
                            0,
                            indeterminate: true);
                    }).ConfigureAwait(true);

                _philosophyScriptGenCts.Token.ThrowIfCancellationRequested();

                void ApplyGrid()
                {
                    dgvPhilosophyScripts?.Invalidate();
                }

                if (dgvPhilosophyScripts != null && dgvPhilosophyScripts.InvokeRequired)
                {
                    dgvPhilosophyScripts.Invoke(new Action(ApplyGrid));
                }
                else
                {
                    ApplyGrid();
                }

                LogPhilosophy("Gemini xong: "
                              + totalQuotes
                              + " câu · "
                              + batchesWithTopic.Count
                              + " batch — đã gợi ý nhạc/tiếng đệm/Edge/phụ đề và render audio thành phẩm. Mở «Âm thanh» để nghe.");
                SetPhilosophyProgress(
                    "Đã tạo " + totalQuotes + " câu + audio thành phẩm · " + batchesWithTopic.Count + " batch",
                    100);
                NotifyPhilosophyDraftDirty();
            }
            catch (OperationCanceledException)
            {
                LogPhilosophy("Quote: đã dừng tạo nội dung.");
                SetPhilosophyProgress("Đã dừng tạo nội dung", 0);
            }
            catch (Exception ex)
            {
                LogPhilosophy("Quote: lỗi tạo nội dung — " + ex.Message);
                MessageBox.Show(ex.Message, "Tạo nội dung Gemini", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetPhilosophyProgress("Lỗi Gemini", 0);
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
                reason = "Đang tạo nội dung — bấm «Dừng» để hủy.";
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
            CancelAllPhilosophyWorkForEmergencyStop(logPrefix: "Quote");
        }

        private void CancelAllPhilosophyWorkForEmergencyStop(string logPrefix = null)
        {
            var prefix = string.IsNullOrWhiteSpace(logPrefix) ? "[Emergency]" : logPrefix + ":";
            var cancelled = false;

            if (_philosophyScriptGenRunning)
            {
                LogPhilosophy(prefix + " đang dừng tạo nội dung…");
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

            SetPhilosophySolidButtonEnabled(btnPhilosophyAddRow, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyCopyRow, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyRestoreSessions, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyDeleteRow, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyTrash, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyMoveRowUp, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyMoveRowDown, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyBrollLibrary, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyMascotImageLibrary, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyZoomImageLibrary, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyMusicLibrary, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophySfxLibrary, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyLogoLibrary, !tabBusy);
            SetPhilosophySolidButtonEnabled(btnPhilosophyGenerateContent, !tabBusy);

            if (btnPhilosophyPushToAutoPost != null)
            {
                btnPhilosophyPushToAutoPost.Enabled = !tabBusy;
            }

            UpdatePhilosophyRenderControlStates();
        }

        private static void SetPhilosophySolidButtonEnabled(Button btn, bool enabled)
        {
            if (btn != null && !btn.IsDisposed)
            {
                btn.Enabled = enabled;
            }
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
                LogPhilosophy("Quote: đang dừng render… (chờ bước hiện tại kết thúc)");
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
                var queue = BuildPhilosophyRenderQueueFromSelectedBatches();
                var selected = queue.Select(e => e.Quote).ToList();
                if (!TryValidatePhilosophyRenderRequest(selected, settings, out var blockMessage))
                {
                    NotifyPhilosophyRenderBlocked(blockMessage);
                    return;
                }

                if (_philosophyRenderPaused && _philosophyRenderPending != null && _philosophyRenderPending.Count > 0)
                {
                    LogPhilosophy("Bắt đầu batch mới — bỏ " + _philosophyRenderPending.Count +
                                  " câu chờ từ lần trước (dùng «Tiếp tục» nếu muốn render tiếp).");
                }

                _philosophyRenderPending = queue;
                _philosophyRenderPaused = false;
                LogPhilosophy("Render " + queue.Count + " câu trong " +
                              GetPhilosophyTargetBatchesFromGrid().Count + " batch đã chọn.");
                for (var i = 0; i < queue.Count; i++)
                {
                    LogPhilosophy("  " + (i + 1) + ". " + TrimPhilosophyPreview(queue[i].Quote?.Content));
                }
            }
            else
            {
                if (_philosophyRenderPending == null || _philosophyRenderPending.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "Không còn dòng chờ render.\r\nBôi đen dòng trong bảng rồi bấm «Render video».",
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
                LogPhilosophy("Quote: tiếp tục render " + _philosophyRenderPending.Count + " dòng còn lại…");
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

            var toolbarProfile = GetSelectedPhilosophyProfileName();
            var batchTotal = _philosophyRenderPending.Count;
            var completedInBatch = 0;

            try
            {
                while (_philosophyRenderPending.Count > 0)
                {
                    if (!ShouldAllowInteractivePrompts())
                    {
                        break;
                    }

                    _philosophyRenderCts.Token.ThrowIfCancellationRequested();

                    var entry = _philosophyRenderPending[0];
                    var item = entry.Quote;
                    var ownerBatch = entry.Batch;
                    if (ownerBatch != null)
                    {
                        PhilosophyBatchHelper.EnsureBatchAudioDefaults(ownerBatch, settings);
                        PhilosophyBatchHelper.EnsureBatchProfileName(ownerBatch, toolbarProfile);
                    }

                    var rowProfile = PhilosophyBatchHelper.ResolveBatchProfileName(ownerBatch, toolbarProfile);
                    var profile = PhilosophyProfileAssets.ResolveProfile(settings, rowProfile);
                    var ordinal = completedInBatch + 1;
                    item.Status = "Đang render…";
                    item.LastError = string.Empty;
                    _philosophyBatchBindingList?.ResetBindings();
                    SetPhilosophyProgress(
                        "Render " + ordinal + "/" + batchTotal + "…",
                        (int)Math.Round(completedInBatch * 100d / Math.Max(1, batchTotal)),
                        indeterminate: true);
                    LogPhilosophy("Quote [" + ordinal + "/" + batchTotal + "]: " + TrimPhilosophyPreview(item.Content));

                    var renderOptions = BuildPhilosophyRenderOptions(toolbarProfile, item, settings, ownerBatch);
                    LogPhilosophy("Profile batch: «" + rowProfile + "»");
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
                        if (renderOptions.TtsOptions != null)
                        {
                            LogPhilosophy("Giọng: popup Âm thanh ("
                                          + (renderOptions.TtsOptions.BodyEngine == TtsEngineKind.ElevenLabs
                                              ? "ElevenLabs"
                                              : "Edge TTS")
                                          + ", tốc độ "
                                          + renderOptions.NarrationSpeedPercent.ToString(CultureInfo.InvariantCulture)
                                          + "%)");
                        }
                        else
                        {
                            var voiceId = PhilosophyVideoPipelineService.ResolveVoiceIdByMood(item.Mood, settings, profile.VoiceId);
                            LogPhilosophy("Giọng (mood «" + (item.Mood ?? "reflective") + "»): "
                                          + (string.IsNullOrWhiteSpace(voiceId) ? "mặc định" : voiceId));
                        }
                        if (!string.IsNullOrWhiteSpace(item.OutputPath))
                        {
                            PhilosophyVideoPipelineService.TryDeletePreviousOutput(item.OutputPath, LogPhilosophy);
                            item.OutputPath = string.Empty;
                            _philosophyBatchBindingList?.ResetBindings();
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
                        ownerBatch?.RefreshDerivedFields();
                        MaybeShowPhilosophyBatchRenderReport(ownerBatch);
                        OnPhilosophyFinished(true, result, string.Empty);
                        LogPhilosophy("Xong: " + result.OutputPath);
                        FlushPhilosophyDraftToDisk();
                        NotifyPhilosophyDraftDirty();
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = "Dừng";
                        _philosophyRenderPaused = true;
                        LogPhilosophy("Quote: đã dừng — còn " + _philosophyRenderPending.Count +
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
                        if (ShouldAllowInteractivePrompts())
                        {
                            MessageBox.Show(
                                this,
                                ex.Message,
                                "Video Quote — lỗi render",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }

                        NotifyPhilosophyDraftDirty();
                        break;
                    }

                    _philosophyBatchBindingList?.ResetBindings();
                }

                if (!_philosophyRenderPaused && (_philosophyRenderPending == null || _philosophyRenderPending.Count == 0))
                {
                    SetPhilosophyProgress("Hoàn tất " + completedInBatch + " video", 100);
                    LogPhilosophy("Quote: hoàn tất batch render.");
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

            if (btnPhilosophyGenerateContent != null)
            {
                btnPhilosophyGenerateContent.Enabled = !running
                    && !_philosophyScriptGenRunning && !_philosophyScenePromptRunning;
            }

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
                    ApplyPhilosophyStopRenderButtonUi(resumeMode: false, enabled: true);
                }
                else if (canResume)
                {
                    ApplyPhilosophyStopRenderButtonUi(resumeMode: true, enabled: pipelineReady
                        && !_philosophyScriptGenRunning && !_philosophyScenePromptRunning);
                }
                else
                {
                    ApplyPhilosophyStopRenderButtonUi(resumeMode: false, enabled: false);
                }
            }

        }

        private void ApplyPhilosophyStopRenderButtonUi(bool resumeMode, bool enabled)
        {
            if (btnPhilosophyStopRender == null || btnPhilosophyStopRender.IsDisposed)
            {
                return;
            }

            btnPhilosophyStopRender.Text = resumeMode ? "Tiếp tục" : "Dừng lại";
            btnPhilosophyStopRender.Enabled = enabled;
            var tint = resumeMode ? PhilosophyTintResume : PhilosophyTintStop;
            if (btnPhilosophyStopRender is JellyButton stopJelly)
            {
                stopJelly.JellyTint = tint;
                stopJelly.Invalidate();
            }

            ApplyPhilosophyCommandButtonMetrics(
                btnPhilosophyStopRender,
                resumeMode ? 120 : 118);
        }

        private bool IsPhilosophyPipelineReady()
        {
            if (_systemHealth == null)
            {
                return true;
            }

            bool Ok(string key) => _systemHealth.TryGetValue(key, out var v) && v;
            // Render cần FFmpeg + lưu trữ; TTS/Gemini/Veo kiểm tra lúc bấm «Render video».
            return Ok("ffmpeg") && Ok("storage");
        }

        private PhilosophyRenderOptions BuildPhilosophyRenderOptions(
            string toolbarProfileFallback,
            PhilosophyScriptItem item,
            AppSettings settings,
            PhilosophyBatchItem ownerBatch = null)
        {
            if (ownerBatch != null)
            {
                PhilosophyBatchHelper.EnsureBatchProfileName(ownerBatch, toolbarProfileFallback);
            }

            var profileName = PhilosophyBatchHelper.ResolveBatchProfileName(ownerBatch, toolbarProfileFallback);
            var visualMode = PhilosophyVisualModes.Normalize(item?.VisualMode ?? 0);
            var mode = ownerBatch?.GenerationMode ?? "Quotes";
            var duration = PhilosophyRenderOptions.ResolveDurationBounds(
                mode,
                ownerBatch?.MinDurationSeconds ?? 0,
                ownerBatch?.MaxDurationSeconds ?? 0);
            var minDur = duration.MinSeconds;
            var maxDur = duration.MaxSeconds;

            var options = new PhilosophyRenderOptions
            {
                ProfileName = profileName,
                ReferenceImagePath = PhilosophyBatchHelper.ResolveReferenceImagePath(ownerBatch, item),
                BRollFolder = item?.BRollFolder?.Trim() ?? string.Empty,
                MusicFolder = item?.MusicFolder?.Trim() ?? string.Empty,
                MusicVolumePercent = PhilosophyBatchHelper.ResolveQuoteMusicVolumePercent(item, ownerBatch),
                NarrationSpeedPercent = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(item, ownerBatch),
                AmbientFolder = PhilosophyAmbientCatalog.ResolveAmbientMediaPath(
                    item?.AmbientKey,
                    profileName,
                    settings,
                    item?.Mood),
                SubtitleOptions = ownerBatch != null
                    ? PhilosophySubtitleStyleHelper.BuildOptionsForBatchQuote(ownerBatch, item, settings)
                    : PhilosophySubtitleStyleHelper.BuildOptions(item, settings),
                VisualMode = visualMode,
                MinDurationSeconds = minDur,
                MaxDurationSeconds = maxDur,
                ZoomImagePaths = item?.ZoomImagePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
                                 ?? new List<string>(),
                PreRenderedFolder = ResolveRowSceneVideoFolder(item),
                QuoteForSceneMatch = (item?.Content ?? string.Empty).Trim()
            };

            if (ownerBatch != null)
            {
                PhilosophyBatchHelper.CopyLogoSettingsToRenderOptions(ownerBatch, options);
                options.TtsOptions = PhilosophyBatchTtsHelper.BuildTtsRenderOptions(ownerBatch, profileName, settings);
            }

            return options;
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

        private string ResolvePhilosophyRowProfile(PhilosophyScriptItem item, PhilosophyBatchItem batch = null) =>
            PhilosophyBatchHelper.ResolveBatchProfileName(
                batch ?? FindPhilosophyBatchForQuote(item),
                GetSelectedPhilosophyProfileName());

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
                    "Video Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            OpenPhilosophyFinishedVideoFile(path, item?.Content);
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
            if (_philosophyBatchBindingList == null || _philosophyBatchBindingList.Count == 0)
            {
                message = "Chưa có batch — bấm «+ Thêm batch» trước.";
                return false;
            }

            if (selected == null || selected.Count == 0)
            {
                message = "Chọn ít nhất một batch trong bảng (click dòng hoặc Ctrl+click nhiều dòng) rồi bấm «Render video».";
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

            var issues = new List<string>();
            foreach (var item in selected)
            {
                var ownerBatch = FindPhilosophyBatchForQuote(item);
                PhilosophyBatchHelper.EnsureBatchProfileName(ownerBatch, GetSelectedPhilosophyProfileName());
                var rowNum = GetPhilosophyRowDisplayNumber(item);
                var rowLabel = rowNum > 0 ? "Dòng " + rowNum : "Một dòng đã chọn";
                var rowProfile = PhilosophyBatchHelper.ResolveBatchProfileName(
                    ownerBatch,
                    GetSelectedPhilosophyProfileName());
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
                else if (visualMode == PhilosophyVisualModes.ImageZoom
                         || visualMode == PhilosophyVisualModes.ImageSlideshow)
                {
                    if (!PhilosophyBRollSelection.TryValidateZoomImages(item?.ZoomImagePaths, rowProfile, out var zoomErr))
                    {
                        issues.Add(rowLabel + ": " + zoomErr);
                    }
                }
                else if (visualMode == PhilosophyVisualModes.ZoomBrollHybrid)
                {
                    if (!PhilosophyBRollSelection.TryValidateZoomImages(item?.ZoomImagePaths, rowProfile, out var zoomHyErr))
                    {
                        issues.Add(rowLabel + ": " + zoomHyErr);
                    }

                    if (!PhilosophyBRollSelection.TryValidateSelection(item?.BRollFolder, rowProfile, out var brollHyErr))
                    {
                        issues.Add(rowLabel + ": " + brollHyErr + " (cần B-roll cho outro).");
                    }
                }
                else if (visualMode == PhilosophyVisualModes.AiStillZoom)
                {
                    if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
                    {
                        issues.Add(rowLabel + ": chế độ «AI ảnh → zoom» cần AI API Key (Gemini) trong Cài đặt.");
                    }

                    var refPath = PhilosophyBatchHelper.ResolveReferenceImagePath(ownerBatch, item);
                    if (string.IsNullOrEmpty(refPath))
                    {
                        var mascot = PhilosophyGeminiBackgroundContext.BuildMascotContext(rowProfile, null);
                        if (!mascot.HasMascotImage)
                        {
                            issues.Add(rowLabel + ": cần ảnh ref batch hoặc ảnh mascot profile cho «AI ảnh → zoom».");
                        }
                    }
                }
                else if (visualMode == PhilosophyVisualModes.PreRendered)
                {
                    var videoFolder = ResolveRowSceneVideoFolder(item);
                    if (string.IsNullOrEmpty(videoFolder) || !Directory.Exists(videoFolder))
                    {
                        issues.Add(rowLabel + ": chế độ 3 cần chọn «Thư mục video» trên dòng.");
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
            if (_philosophyBatchBindingList == null || item == null)
            {
                return 0;
            }

            var batchIndex = 0;
            foreach (var batch in _philosophyBatchBindingList)
            {
                batchIndex++;
                if (batch?.Quotes == null)
                {
                    continue;
                }

                var quoteIndex = batch.Quotes.IndexOf(item);
                if (quoteIndex >= 0)
                {
                    return batchIndex * 1000 + quoteIndex + 1;
                }
            }

            return 0;
        }

        private void NotifyPhilosophyRenderBlocked(string message)
        {
            var text = (message ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                text = "Không đủ điều kiện để render.";
            }

            LogPhilosophy("Quote: " + text.Replace("\r\n", " | "));
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

        private void PromptPhilosophyRenderCompleteDialog(string quote, string outputPath)
        {
            if (!ShouldAllowInteractivePrompts())
            {
                return;
            }

            var path = (outputPath ?? string.Empty).Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                return;
            }

            var label = TrimPhilosophyPreviewN((quote ?? string.Empty).Trim(), 64);
            if (label.Length == 0)
            {
                label = "Video Quote";
            }

            using (var dlg = new ShowcaseRenderCompleteDialog(label, path))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    OpenPhilosophyFinishedVideoFile(path, quote);
                }
            }
        }

        private void OpenPhilosophyFinishedVideoFile(string path, string quote)
        {
            var videoPath = (path ?? string.Empty).Trim();
            if (videoPath.Length == 0 || !File.Exists(videoPath))
            {
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(videoPath)
                {
                    UseShellExecute = true
                });
                var caption = TrimPhilosophyPreviewN((quote ?? string.Empty).Trim(), 48);
                LoadProductionVideoPreview(
                    videoPath,
                    ProductionPipeline.ResolveThumbnailPath(videoPath),
                    caption.Length > 0 ? caption : Path.GetFileNameWithoutExtension(videoPath));
                LogPhilosophy("Xem video thành phẩm → " + videoPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Không mở được video:\r\n" + ex.Message,
                    "Video Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // ─── Scene Prompts & Export ───────────────────────────────────────────

        private async void BtnGenerateScenePrompts_Click(object sender, EventArgs e)
        {
            if (IsPhilosophyTabBusy(out var busyReason))
            {
                MessageBox.Show(busyReason, "Tạo Prompt Phân Cảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var targets = GetPhilosophyTargetBatchesFromGrid();
            if (targets.Count == 0)
            {
                MessageBox.Show(
                    "Chọn ít nhất một batch trên lưới (hoặc bấm vào batch cần tạo prompt).",
                    "Tạo Prompt Phân Cảnh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var quoteJobs = new List<(PhilosophyBatchItem Batch, PhilosophyScriptItem Quote)>();
            foreach (var batch in targets)
            {
                if (batch?.Quotes == null)
                {
                    continue;
                }

                foreach (var quote in batch.Quotes.Where(q => q != null))
                {
                    quoteJobs.Add((batch, quote));
                }
            }

            if (quoteJobs.Count == 0)
            {
                MessageBox.Show(
                    "Batch đã chọn chưa có câu nội dung.",
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

                var total = quoteJobs.Count;
                var totalScenes = 0;
                for (var rowIdx = 0; rowIdx < total; rowIdx++)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = quoteJobs[rowIdx].Batch;
                    var item = quoteJobs[rowIdx].Quote;
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
                    var imagePath = PhilosophyBatchHelper.ResolveReferenceImagePath(batch, item);

                    LogPhilosophy("Dòng " + GetPhilosophyRowDisplayNumber(item) + ": " + scenes.Count +
                                  " phân cảnh (~8s/clip, đọc ~" +
                                  PhilosophySceneHelper.EstimateDuration(quote).ToString("0.0") + "s).");

                    if (string.IsNullOrEmpty(imagePath))
                    {
                        var visualMode = PhilosophyVisualModes.Normalize(item.VisualMode);
                        if (visualMode == PhilosophyVisualModes.PreRendered)
                        {
                            LogPhilosophy("Dòng " + GetPhilosophyRowDisplayNumber(item) +
                                          ": mode 3 cần ảnh tham chiếu (popup «Nền»).");
                        }

                        LogPhilosophy("Dòng " + GetPhilosophyRowDisplayNumber(item) +
                                      ": không có ảnh tham chiếu — Gemini dùng style photorealistic mặc định.");
                    }
                    else
                    {
                        LogPhilosophy("Dòng " + GetPhilosophyRowDisplayNumber(item) +
                                      ": ảnh tham chiếu → " + System.IO.Path.GetFileName(imagePath));
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
                LogPhilosophy("Phân cảnh: " + totalScenes + " cảnh mới từ " + quoteJobs.Count + " câu đã chọn.");
                SetPhilosophyProgress("Đã sinh " + totalScenes + " phân cảnh", 100);
            }
            catch (OperationCanceledException)
            {
                LogPhilosophy("Quote: đã dừng tạo prompt phân cảnh.");
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
                    "Chọn ít nhất một batch trên lưới để xuất prompt.",
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

        private string ResolvePhilosophyPreRenderedScenesDirectory()
        {
            var profile = GetSelectedPhilosophyProfileName();
            return PhilosophyProfileAssets.EnsurePreRenderedScenesDirectory(profile);
        }

        private string GetPhilosophyVideoInputFolder()
        {
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
