using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font ReupPrimaryActionFont = AppPrimaryActionFont;
        private static readonly Font ReupJellyButtonFont = AppJellyButtonFont;
        private const int ReupPrimaryActionHeight = AppPrimaryActionHeight;
        private const int ReupPrimaryActionMinWidth = AppPrimaryActionMinWidth;
        private const int ReupCommandButtonHeight = AppJellyButtonHeight;
        private const int ReupCommandHorizontalPad = AppJellyButtonHorizontalPad;
        private static readonly Color ReupTintAddRow = Color.FromArgb(55, 95, 160);
        private static readonly Color ReupTintAffiliateImport = Color.FromArgb(76, 110, 245);
        private static readonly Color ReupTintRender = Color.FromArgb(56, 158, 88);
        private static readonly Color ReupTintStop = Color.FromArgb(195, 72, 72);
        private static readonly Color ReupTintContinue = Color.FromArgb(220, 110, 50);
        private static readonly Color ReupTintOutput = Color.FromArgb(88, 94, 112);
        private static readonly Color ReupTintFolder = Color.FromArgb(55, 100, 140);
        private static readonly Padding ReupFlowItemMargin = new Padding(4, 4, 10, 6);
        private static readonly Padding ReupFlowSectionMargin = new Padding(0, 0, 18, 4);

        private static FlowLayoutPanel CreateReupWrapFlowPanel(string name, Color backColor)
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

        private static FlowLayoutPanel CreateReupToolGroup(string sectionLabel, Color backColor)
        {
            var group = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Margin = ReupFlowSectionMargin,
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

        private static void ApplyReupFlowControlMargin(Control control, int top = 4)
        {
            if (control == null)
            {
                return;
            }

            control.Margin = new Padding(ReupFlowItemMargin.Left, top, ReupFlowItemMargin.Right, ReupFlowItemMargin.Bottom);
        }

        private static Panel CreateReupAutoSizeBar(string name, Color backColor)
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

        private Panel pnlReupHookToolbar;
        private Panel pnlReupTopChrome;
        private Panel pnlReupGridWrap;
        private Panel pnlReupMainFill;
        private Panel pnlReupRenderHost;
        private Button btnVideoReupStop;
        private Button btnVideoReupProcessVideo;
        private Button btnPlaySource;
        private Button btnPlayOutput;
        private System.Windows.Forms.ToolTip _reupPathTip;

        private void WireVideoReupTabLayout()
        {
            if (pnlModeVideoReup == null || dgvVideoReupInput == null)
            {
                return;
            }

            pnlModeVideoReup.SuspendLayout();
            try
            {
                pnlModeVideoReup.Padding = new Padding(4);
                pnlModeVideoReup.AutoScroll = false;

                BuildReupTopChrome();
                BuildReupRenderActionBar();
                BuildReupMainSplit();
                DetachReupEditorPanelFromTab();

                pnlVideoReupStatus.Dock = DockStyle.Bottom;
                pnlVideoReupStatus.Height = 210;
                pnlVideoReupStatus.MinimumSize = new Size(0, 177);

                pnlReupTopChrome.Dock = DockStyle.Top;
                pnlReupRenderHost.Dock = DockStyle.Bottom;

                pnlReupMainFill = new Panel
                {
                    Name = "pnlReupMainFill",
                    BackColor = Color.FromArgb(31, 34, 42),
                    Padding = new Padding(0, 2, 0, 0)
                };

                ApplyTopFillBottomDockLayout(
                    pnlReupMainFill,
                    pnlReupGridWrap,
                    bottom: null,
                    top: null);

                pnlModeVideoReup.Controls.Clear();
                pnlReupMainFill.Dock = DockStyle.Fill;
                pnlReupMainFill.Margin = Padding.Empty;
                pnlVideoReupStatus.Margin = Padding.Empty;
                pnlReupRenderHost.Margin = Padding.Empty;
                pnlReupTopChrome.Margin = Padding.Empty;
                pnlModeVideoReup.Controls.Add(pnlReupMainFill);
                pnlModeVideoReup.Controls.Add(pnlReupRenderHost);
                pnlModeVideoReup.Controls.Add(pnlVideoReupStatus);
                pnlModeVideoReup.Controls.Add(pnlReupTopChrome);
            }
            finally
            {
                pnlModeVideoReup.ResumeLayout(true);
            }
        }

        private static int MeasureReupButtonTextWidth(string text)
        {
            return MeasureAppJellyButtonTextWidth(text, ReupJellyButtonFont, ReupCommandButtonHeight);
        }

        private static JellyButton CreateReupJellyButton(string name, string text, Color tint, int minWidth = 96)
        {
            return CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: ReupCommandButtonHeight,
                minWidth: minWidth,
                horizontalPad: ReupCommandHorizontalPad,
                margin: ReupFlowItemMargin);
        }

        private static void ApplyReupCommandBarButtonMetrics(Button btn)
        {
            if (btn == null)
            {
                return;
            }

            var minWidth = btn.MinimumSize.Width > 0 ? btn.MinimumSize.Width : 96;
            ResizeAppJellyButton(btn, ReupCommandButtonHeight, minWidth, ReupCommandHorizontalPad);
            btn.Margin = ReupFlowItemMargin;
            if (btn is JellyButton jelly)
            {
                jelly.JellyFillOpacity = 1f - JellyButton.DefaultTransparency;
                jelly.ForeColor = Color.FromArgb(245, 247, 250);
                jelly.Tag = JellyButton.ChromeTag;
                jelly.AccessibleName = JellyButton.ChromeTag;
            }
        }

        private static void ApplyReupHookToolbarButtonStyle(Button btn, Color tint)
        {
            ApplyReupCommandBarButtonMetrics(btn);
            if (btn is JellyButton jelly)
            {
                jelly.JellyTint = tint;
                return;
            }

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = tint;
            btn.ForeColor = Color.FromArgb(245, 247, 250);
        }

        private void BuildReupMainSplit()
        {
            pnlReupGridWrap = new Panel
            {
                Name = "pnlReupGridWrap",
                Padding = new Padding(4),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            if (lblVideoReupReadiness != null)
            {
                lblVideoReupReadiness.AutoSize = true;
                lblVideoReupReadiness.AutoEllipsis = false;
                lblVideoReupReadiness.Dock = DockStyle.Top;
                lblVideoReupReadiness.Padding = new Padding(4, 4, 4, 4);
                WireAffiliateDeepReadinessWrap(lblVideoReupReadiness, pnlReupGridWrap);
            }

            if (pnlReupGridToolbar != null)
            {
                pnlReupGridToolbar.Dock = DockStyle.Top;
                pnlReupGridToolbar.Margin = new Padding(0, 0, 0, 4);
            }

            dgvVideoReupInput.Margin = Padding.Empty;
            dgvVideoReupInput.MinimumSize = new Size(120, 80);
            dgvVideoReupInput.Dock = DockStyle.Fill;

            pnlReupGridWrap.Controls.Add(dgvVideoReupInput);
            if (pnlReupGridToolbar != null)
            {
                pnlReupGridWrap.Controls.Add(pnlReupGridToolbar);
            }

            if (lblVideoReupReadiness != null)
            {
                pnlReupGridWrap.Controls.Add(lblVideoReupReadiness);
            }

            dgvVideoReupInput.BringToFront();
        }

        /// <summary>Gỡ panel editor cũ khỏi tab — các control hook/nhạc đã reparent sang thanh công cụ.</summary>
        private void DetachReupEditorPanelFromTab()
        {
            if (pnlReupEditor == null)
            {
                return;
            }

            pnlReupEditor.Parent?.Controls.Remove(pnlReupEditor);
            pnlReupEditor.Visible = false;
        }

        private void BuildReupTopChrome()
        {
            BuildReupHookToolbar();

            pnlReupTopChrome = new Panel
            {
                Name = "pnlReupTopChrome",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(36, 39, 48)
            };

            pnlReupHookToolbar.Dock = DockStyle.Top;
            pnlReupTopChrome.Controls.Add(pnlReupHookToolbar);
        }

        private static void ReparentReupControl(Control control, Control parent)
        {
            if (control == null || parent == null)
            {
                return;
            }

            control.Parent?.Controls.Remove(control);
            control.Visible = true;
            parent.Controls.Add(control);
        }

        private void BuildReupHookToolbar()
        {
            var barColor = Color.FromArgb(33, 36, 44);
            pnlReupHookToolbar = CreateReupAutoSizeBar("pnlReupHookToolbar", barColor);

            var flpHookRoot = CreateReupWrapFlowPanel("flpReupHookRoot", barColor);

            void AddHookBtn(FlowLayoutPanel group, Button btn, Color tint)
            {
                if (btn == null || group == null)
                {
                    return;
                }

                btn.Parent?.Controls.Remove(btn);
                btn.Visible = true;
                ApplyReupHookToolbarButtonStyle(btn, tint);
                group.Controls.Add(btn);
            }

            var grpGemini = CreateReupToolGroup(string.Empty, barColor);
            AddHookBtn(grpGemini, btnPushSelectionToVideoReup, ReupTintAffiliateImport);
            AddHookBtn(grpGemini, btnVideoReupHookGemini, ReupTintAffiliateImport);
            if (btnVideoReupHookGemini != null)
            {
                btnVideoReupHookGemini.Text = "Tạo Hook+Script+Hashtag";
            }
            if (btnVideoReupLyriaHook != null)
            {
                btnVideoReupLyriaHook.Visible = false;
            }

            flpHookRoot.Controls.Add(grpGemini);

            EnsureReupNarrationScriptButtons();
            var grpNarration = CreateReupToolGroup(string.Empty, barColor);
            AddHookBtn(grpNarration, btnVideoReupNarrationScriptGemini, ReupTintAffiliateImport);
            AddHookBtn(grpNarration, btnVideoReupNarrationScriptRegen, Color.FromArgb(120, 70, 160));

            var grpSfx = CreateReupToolGroup(string.Empty, barColor);
            ReparentReupControl(btnVideoReupOpenHookSfxFolder, grpSfx);
            if (btnVideoReupOpenHookSfxFolder != null)
            {
                ApplyReupCommandBarButtonMetrics(btnVideoReupOpenHookSfxFolder);
                ApplyReupFlowControlMargin(btnVideoReupOpenHookSfxFolder, top: 6);
            }

            var grpMusic = CreateReupToolGroup(string.Empty, barColor);
            ReparentReupControl(btnVideoReupOpenMusicFolder, grpMusic);
            if (btnVideoReupOpenMusicFolder != null)
            {
                ApplyReupCommandBarButtonMetrics(btnVideoReupOpenMusicFolder);
            }

            var grpLogo = CreateReupToolGroup(string.Empty, barColor);
            ReparentReupControl(btnVideoReupOpenLogoLibrary, grpLogo);
            if (btnVideoReupOpenLogoLibrary != null)
            {
                ApplyReupCommandBarButtonMetrics(btnVideoReupOpenLogoLibrary);
            }

            flpHookRoot.Controls.Add(grpNarration);
            flpHookRoot.Controls.Add(grpSfx);
            flpHookRoot.Controls.Add(grpMusic);
            flpHookRoot.Controls.Add(grpLogo);

            EnsureReupGeminiButtonTooltips();
            EnsureReupNarrationScriptTooltips();

            if (btnVideoReupHookGemini != null)
            {
                btnVideoReupHookGemini.Enabled = true;
            }

            HideDetachedReupEditorControls();

            pnlReupHookToolbar.Controls.Add(flpHookRoot);
        }

        /// <summary>Ẩn control đã chuyển sang lưới — tránh trùng trên toolbar.</summary>
        private void HideDetachedReupEditorControls()
        {
            if (cbVideoReupMusic != null)
            {
                cbVideoReupMusic.Visible = false;
            }

            if (lblVideoReupMusicPick != null)
            {
                lblVideoReupMusicPick.Visible = false;
            }

            if (rbVideoReupAudioAffiliate != null)
            {
                rbVideoReupAudioAffiliate.Visible = false;
            }

            if (rbVideoReupAudioFilm != null)
            {
                rbVideoReupAudioFilm.Visible = false;
            }

            if (grpVideoReupAudioMode != null)
            {
                grpVideoReupAudioMode.Visible = false;
            }
        }

        private void BuildReupRenderActionBar()
        {
            EnsureReupPlaybackButtons();

            if (btnVideoReupProcessVideo == null || btnVideoReupProcessVideo.IsDisposed)
            {
                btnVideoReupProcessVideo = CreateAppPrimaryJellyButton(
                    "btnVideoReupProcessVideo",
                    "Render & Đóng gói",
                    ReupTintRender,
                    minWidth: ReupPrimaryActionMinWidth);
                btnVideoReupProcessVideo.Click += btnProcessVideo_Click;
            }
            else
            {
                btnVideoReupProcessVideo.Parent?.Controls.Remove(btnVideoReupProcessVideo);
            }

            btnPlaySource.Parent?.Controls.Remove(btnPlaySource);
            btnPlayOutput.Parent?.Controls.Remove(btnPlayOutput);
            btnVideoReupAddManualRow?.Parent?.Controls.Remove(btnVideoReupAddManualRow);
            btnVideoReupPushToAutoPost?.Parent?.Controls.Remove(btnVideoReupPushToAutoPost);
            btnPlaySource.Text = "Video nguồn";
            btnPlayOutput.Text = "Video thành phẩm";
            ApplyReupCommandBarButtonMetrics(btnPlaySource);
            ApplyReupCommandBarButtonMetrics(btnPlayOutput);
            if (btnVideoReupAddManualRow != null)
            {
                ApplyReupCommandBarButtonMetrics(btnVideoReupAddManualRow);
            }

            if (btnVideoReupPushToAutoPost == null || btnVideoReupPushToAutoPost.IsDisposed)
            {
                btnVideoReupPushToAutoPost = CreateReupJellyButton(
                    "btnVideoReupPushToAutoPost",
                    "Đẩy sang Đăng tự động",
                    ReupTintAffiliateImport,
                    196);
                btnVideoReupPushToAutoPost.Click += btnVideoReupPushToAutoPost_Click;
            }

            ApplyReupCommandBarButtonMetrics(btnVideoReupPushToAutoPost);

            ResizeAppJellyButton(
                btnVideoReupProcessVideo,
                AppPrimaryActionHeight,
                ReupPrimaryActionMinWidth,
                AppPrimaryActionHorizontalPad);
            btnVideoReupProcessVideo.Font = ReupPrimaryActionFont;
            btnVideoReupProcessVideo.Margin = new Padding(4, 4, 12, 6);
            btnVideoReupProcessVideo.Anchor = AnchorStyles.None;
            if (btnVideoReupProcessVideo is JellyButton renderJelly)
            {
                renderJelly.JellyTint = ReupTintRender;
            }

            btnVideoReupProcessVideo.Enabled = ResolveVideoReupRenderReady();

            if (btnVideoReupStop == null || btnVideoReupStop.IsDisposed)
            {
                btnVideoReupStop = CreateReupJellyButton("btnVideoReupStop", "D\u1EEBng render", ReupTintStop, 108);
                btnVideoReupStop.Click += btnVideoReupStop_Click;
            }
            else
            {
                btnVideoReupStop.Parent?.Controls.Remove(btnVideoReupStop);
            }

            ApplyReupCommandBarButtonMetrics(btnVideoReupStop);
            ApplyVideoReupRenderStopButtonUi(continueMode: false, enabled: false);

            var renderBarColor = Color.FromArgb(28, 30, 38);
            pnlReupRenderHost = new Panel
            {
                Name = "pnlReupRenderHost",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Bottom,
                Padding = new Padding(8, 10, 8, 8),
                BackColor = renderBarColor
            };

            var flpRender = CreateReupWrapFlowPanel("flpReupRenderActions", renderBarColor);
            flpRender.Padding = new Padding(8, 4, 8, 4);
            if (btnVideoReupAddManualRow != null)
            {
                flpRender.Controls.Add(btnVideoReupAddManualRow);
            }

            flpRender.Controls.Add(btnPlaySource);
            flpRender.Controls.Add(btnVideoReupProcessVideo);
            flpRender.Controls.Add(btnVideoReupStop);
            flpRender.Controls.Add(btnPlayOutput);
            if (btnVideoReupPushToAutoPost != null)
            {
                flpRender.Controls.Add(btnVideoReupPushToAutoPost);
            }

            pnlReupRenderHost.Controls.Add(flpRender);
        }

        /// <summary>Khởi tạo <see cref="Services.VideoOcrService"/> — dùng ffmpeg bundled, tessdata cạnh exe.</summary>
        private Services.VideoOcrService BuildVideoOcrService()
        {
            try
            {
                var ffmpegPath = Services.FfmpegToolkitService.GetBundledFfmpegPath();
                if (!System.IO.File.Exists(ffmpegPath))
                {
                    ffmpegPath = "ffmpeg";
                }

                var tessdataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                return new Services.VideoOcrService(ffmpegPath, tessdataPath);
            }
            catch (Exception ex)
            {
                LogVideoReup("[OCR] Không khởi tạo được VideoOcrService: " + ex.Message);
                return null;
            }
        }

        /// <summary>Cập nhật ffmpeg path trong OcrService sau khi Settings được load (có thể khác bundled).</summary>
        private void RefreshVideoOcrService(AppSettings settings = null)
        {
            if (_videoReupRemixService == null)
            {
                return;
            }

            try
            {
                settings = settings ?? _configManager.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                string ffmpegPath = null;
                if (Services.FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
                {
                    ffmpegPath = toolkit.FfmpegExe;
                }

                if (string.IsNullOrWhiteSpace(ffmpegPath) || !System.IO.File.Exists(ffmpegPath))
                {
                    ffmpegPath = Services.FfmpegToolkitService.GetBundledFfmpegPath();
                }

                if (string.IsNullOrWhiteSpace(ffmpegPath) || !System.IO.File.Exists(ffmpegPath))
                {
                    _videoReupRemixService.OcrService = null;
                    LogVideoReup("[OCR] Chưa có FFmpeg — bỏ qua OCR (body vẫn lật ngang mặc định).");
                    return;
                }

                var tessdataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                if (!System.IO.Directory.Exists(tessdataPath))
                {
                    _videoReupRemixService.OcrService = null;
                    LogVideoReup("[OCR] Không tìm thấy tessdata tại «" + tessdataPath + "» — bỏ qua OCR (body vẫn lật ngang).");
                    return;
                }

                _videoReupRemixService.OcrService = new Services.VideoOcrService(ffmpegPath, tessdataPath);
                LogVideoReup("[OCR] Sẵn sàng — kiểm tra chữ trước khi lật ngang body.");
            }
            catch (Exception ex)
            {
                _videoReupRemixService.OcrService = null;
                LogVideoReup("[OCR] RefreshVideoOcrService lỗi (bỏ qua): " + ex.Message);
            }
        }

        private void EnsureReupNarrationScriptButtons()
        {
            if (btnVideoReupNarrationScriptGemini == null || btnVideoReupNarrationScriptGemini.IsDisposed)
            {
                btnVideoReupNarrationScriptGemini = CreateReupJellyButton(
                    "btnVideoReupNarrationScriptGemini",
                    "Tạo script",
                    ReupTintAffiliateImport,
                    108);
                btnVideoReupNarrationScriptGemini.Click += btnVideoReupNarrationScriptGemini_Click;
            }

            if (btnVideoReupNarrationScriptRegen == null || btnVideoReupNarrationScriptRegen.IsDisposed)
            {
                btnVideoReupNarrationScriptRegen = CreateReupJellyButton(
                    "btnVideoReupNarrationScriptRegen",
                    "Tạo lại script",
                    Color.FromArgb(120, 70, 160),
                    124);
                btnVideoReupNarrationScriptRegen.Click += btnVideoReupNarrationScriptRegen_Click;
            }
        }

        private void EnsureReupNarrationScriptTooltips()
        {
            if (_reupPathTip == null)
            {
                return;
            }

            if (btnVideoReupNarrationScriptGemini != null)
            {
                _reupPathTip.SetToolTip(
                    btnVideoReupNarrationScriptGemini,
                    "Sinh script thuyết minh bằng Gemini — chỉ dòng có cột «Chế độ» = «Hook + Thuyết minh».");
            }

            if (btnVideoReupNarrationScriptRegen != null)
            {
                _reupPathTip.SetToolTip(
                    btnVideoReupNarrationScriptRegen,
                    "Tạo lại script thuyết minh (bỏ qua script đã có).");
            }
        }

        private void EnsureReupGeminiButtonTooltips()
        {
            if (_reupPathTip == null)
            {
                _reupPathTip = new System.Windows.Forms.ToolTip
                {
                    AutoPopDelay = 12000,
                    InitialDelay = 400,
                    ReshowDelay = 200,
                    ShowAlways = true
                };
            }

            const string createTip = "Gọi Gemini sinh hook, script và hashtag cho dòng đã chọn.";

            if (btnVideoReupHookGemini != null)
            {
                _reupPathTip.SetToolTip(btnVideoReupHookGemini, createTip);
            }

            if (btnVideoReupStop != null)
            {
                _reupPathTip.SetToolTip(
                    btnVideoReupStop,
                    "Dừng render lô đang chạy. Sau khi dừng, bấm «Tiếp tục» để render các dòng còn lại.");
            }
        }

        private void EnsureReupPlaybackButtons()
        {
            // Shared ToolTip for path display on hover
            if (_reupPathTip == null)
            {
                _reupPathTip = new System.Windows.Forms.ToolTip
                {
                    AutoPopDelay = 10000,
                    InitialDelay = 400,
                    ReshowDelay  = 200,
                    ShowAlways   = true
                };
            }

            if (btnPlaySource == null)
            {
                btnPlaySource = CreateReupJellyButton("btnPlaySource", "Phát nguồn", ReupTintOutput, 108);
                btnPlaySource.Click      += (_, __) => PlayReupPreviewVideo(_reupPreviewSourcePath);
                btnPlaySource.MouseEnter += (_, __) =>
                {
                    var p = _reupPreviewSourcePath;
                    _reupPathTip.SetToolTip(btnPlaySource,
                        string.IsNullOrWhiteSpace(p) ? "(chưa tải video nguồn)" : p);
                };
            }

            if (btnPlayOutput == null)
            {
                btnPlayOutput = CreateReupJellyButton("btnPlayOutput", "Phát thành phẩm", ReupTintOutput, 124);
                btnPlayOutput.Click      += (_, __) => PlayReupPreviewVideo(_reupPreviewOutputPath);
                btnPlayOutput.MouseEnter += (_, __) =>
                {
                    var p = _reupPreviewOutputPath;
                    _reupPathTip.SetToolTip(btnPlayOutput,
                        string.IsNullOrWhiteSpace(p) ? "(chưa render video thành phẩm)" : p);
                };
            }
        }

        private async void btnVideoReupStop_Click(object sender, System.EventArgs e)
        {
            await HandleVideoReupRenderStopButtonClickAsync().ConfigureAwait(true);
        }
    }
}
