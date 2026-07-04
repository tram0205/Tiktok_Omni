using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font ReupPrimaryActionFont = new Font("Segoe UI", 12F, FontStyle.Bold);
        private static readonly Font ReupJellyButtonFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        private const int ReupPrimaryActionHeight = 52;
        private const int ReupPrimaryActionMinWidth = 300;
        private const int ReupCommandButtonHeight = 44;
        private const int ReupCommandHorizontalPad = 30;
        private static readonly Color ReupTintAddRow = Color.FromArgb(55, 95, 160);
        private static readonly Color ReupTintAffiliateImport = Color.FromArgb(76, 110, 245);
        private static readonly Color ReupTintRender = Color.FromArgb(56, 158, 88);
        private static readonly Color ReupTintRenderBatch = Color.FromArgb(48, 138, 78);
        private static readonly Color ReupTintStop = Color.FromArgb(195, 72, 72);
        private static readonly Color ReupTintOutput = Color.FromArgb(88, 94, 112);
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

        private Panel pnlReupCommandBar;
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

                if (pnlVideoReupProgress != null)
                {
                    pnlVideoReupProgress.Dock = DockStyle.Bottom;
                }

                pnlVideoReupStatus.Dock = DockStyle.Bottom;
                pnlVideoReupStatus.Height = 88;
                pnlVideoReupStatus.MinimumSize = new Size(0, 72);

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
            return TextRenderer.MeasureText(
                text,
                ReupJellyButtonFont,
                new Size(int.MaxValue, ReupCommandButtonHeight),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width;
        }

        private static JellyButton CreateReupJellyButton(string name, string text, Color tint, int minWidth = 96)
        {
            var width = Math.Max(minWidth, MeasureReupButtonTextWidth(text) + ReupCommandHorizontalPad);
            return new JellyButton
            {
                Name = name,
                Text = text,
                Font = ReupJellyButtonFont,
                JellyTint = tint,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Width = width,
                Height = ReupCommandButtonHeight,
                MinimumSize = new Size(width, ReupCommandButtonHeight),
                MaximumSize = new Size(width, ReupCommandButtonHeight),
                Margin = ReupFlowItemMargin,
                Tag = JellyButton.ChromeTag,
                AccessibleName = JellyButton.ChromeTag
            };
        }

        private static void ApplyReupCommandBarButtonMetrics(Button btn)
        {
            if (btn == null)
            {
                return;
            }

            var width = Math.Max(
                btn.MinimumSize.Width > 0 ? btn.MinimumSize.Width : 96,
                MeasureReupButtonTextWidth(btn.Text) + ReupCommandHorizontalPad);
            btn.Font = ReupJellyButtonFont;
            btn.AutoSize = false;
            btn.Height = ReupCommandButtonHeight;
            btn.Width = width;
            btn.MinimumSize = new Size(width, ReupCommandButtonHeight);
            btn.MaximumSize = new Size(width, ReupCommandButtonHeight);
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
                lblVideoReupReadiness.AutoSize = false;
                lblVideoReupReadiness.Height = 28;
                lblVideoReupReadiness.Padding = new Padding(4, 4, 4, 2);
            }

            dgvVideoReupInput.Margin = Padding.Empty;
            dgvVideoReupInput.MinimumSize = new Size(120, 80);

            ApplyTopFillBottomDockLayout(
                pnlReupGridWrap,
                dgvVideoReupInput,
                bottom: null,
                lblVideoReupReadiness);
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
            BuildReupCommandBar();
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
            pnlReupCommandBar.Dock = DockStyle.Top;
            pnlReupTopChrome.Controls.Add(pnlReupHookToolbar);
            pnlReupTopChrome.Controls.Add(pnlReupCommandBar);
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

            var grpHook = CreateReupToolGroup("Hook:", barColor);
            AddHookBtn(grpHook, btnVideoReupHookGemini, ReupTintAffiliateImport);
            AddHookBtn(grpHook, btnVideoReupHookRegen, Color.FromArgb(60, 100, 200));
            if (btnVideoReupLyriaHook != null)
            {
                btnVideoReupLyriaHook.Visible = false;
            }

            var grpScript = CreateReupToolGroup("Script:", barColor);
            AddHookBtn(grpScript, btnVideoReupNarrationScriptGemini, Color.FromArgb(72, 130, 185));
            AddHookBtn(grpScript, btnVideoReupNarrationScriptRegen, Color.FromArgb(58, 108, 168));

            var grpSfx = CreateReupToolGroup("SFX mặc định:", barColor);
            ReparentReupControl(cbReupVisualHookPreset, grpSfx);
            ReparentReupControl(txtReupVisualHookSfx, grpSfx);
            ReparentReupControl(btnBrowseReupVisualHookSfx, grpSfx);
            if (cbReupVisualHookPreset != null)
            {
                cbReupVisualHookPreset.Width = 120;
                ApplyReupFlowControlMargin(cbReupVisualHookPreset, top: 6);
            }

            if (txtReupVisualHookSfx != null)
            {
                txtReupVisualHookSfx.Width = 140;
                ApplyReupFlowControlMargin(txtReupVisualHookSfx, top: 6);
            }

            if (btnBrowseReupVisualHookSfx != null)
            {
                btnBrowseReupVisualHookSfx.Height = 32;
                ApplyReupFlowControlMargin(btnBrowseReupVisualHookSfx, top: 6);
            }

            var grpMusic = CreateReupToolGroup("Nhạc:", barColor);
            ReparentReupControl(btnVideoReupRefreshMusicList, grpMusic);
            ReparentReupControl(btnVideoReupOpenMusicFolder, grpMusic);
            if (btnVideoReupRefreshMusicList != null)
            {
                ApplyReupCommandBarButtonMetrics(btnVideoReupRefreshMusicList);
                btnVideoReupRefreshMusicList.Height = 32;
            }

            if (btnVideoReupOpenMusicFolder != null)
            {
                ApplyReupCommandBarButtonMetrics(btnVideoReupOpenMusicFolder);
                btnVideoReupOpenMusicFolder.Height = 32;
            }

            flpHookRoot.Controls.Add(grpHook);
            flpHookRoot.Controls.Add(grpScript);
            flpHookRoot.Controls.Add(grpSfx);
            flpHookRoot.Controls.Add(grpMusic);

            HideDetachedReupEditorControls();

            pnlReupHookToolbar.Controls.Add(flpHookRoot);
        }

        private void BuildReupSubtitleToolbar()
        {
            var barColor = Color.FromArgb(30, 33, 40);
            pnlReupSubtitleToolbar = CreateReupAutoSizeBar("pnlReupSubtitleToolbar", barColor);

            var flp = CreateReupWrapFlowPanel("flpReupSubtitleStyle", barColor);

            Label MkLbl(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 158, 172),
                Margin = new Padding(0, 10, 4, 4)
            };

            cbReupSubtitlePosition = CreateReupSubtitleCombo("cbReupSubtitlePosition", 88);
            cbReupSubtitlePosition.Items.AddRange(new object[] { "Dưới", "Giữa", "Trên" });
            cbReupSubtitlePosition.SelectedIndex = 0;

            cbReupSubtitleFont = CreateReupSubtitleCombo("cbReupSubtitleFont", 130);
            cbReupSubtitleFont.Items.AddRange(ReupSubtitleStyleHelper.FontChoices.Cast<object>().ToArray());
            cbReupSubtitleFont.SelectedIndex = 0;

            numReupSubtitleFontSize = new NumericUpDown
            {
                Name = "numReupSubtitleFontSize",
                Width = 54,
                Height = 24,
                Minimum = 32,
                Maximum = 160,
                Value = 88,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 6, 0)
            };

            cbReupSubtitleAnimation = CreateReupSubtitleCombo("cbReupSubtitleAnimation", 130);
            cbReupSubtitleAnimation.Items.AddRange(new object[]
            {
                "Pop (phóng to)",
                "Karaoke (tô màu)",
                "Hiện dần",
                "Cả dòng"
            });
            cbReupSubtitleAnimation.SelectedIndex = 0;

            chkReupSubtitleBold = new CheckBox
            {
                Name = "chkReupSubtitleBold",
                Text = "Đậm",
                AutoSize = true,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(4, 8, 4, 0)
            };

            chkReupSubtitleItalic = new CheckBox
            {
                Name = "chkReupSubtitleItalic",
                Text = "Nghiêng",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 8, 6, 0)
            };

            numReupSubtitleWordsPerLine = new NumericUpDown
            {
                Name = "numReupSubtitleWordsPerLine",
                Width = 44,
                Height = 24,
                Minimum = 4,
                Maximum = 8,
                Value = 6,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 0, 0)
            };

            flp.Controls.Add(MkLbl("Phụ đề:"));
            flp.Controls.Add(MkLbl("Vị trí"));
            flp.Controls.Add(cbReupSubtitlePosition);
            flp.Controls.Add(MkLbl("Font"));
            flp.Controls.Add(cbReupSubtitleFont);
            flp.Controls.Add(MkLbl("Cỡ"));
            flp.Controls.Add(numReupSubtitleFontSize);
            flp.Controls.Add(MkLbl("Chạy chữ"));
            flp.Controls.Add(cbReupSubtitleAnimation);
            flp.Controls.Add(chkReupSubtitleBold);
            flp.Controls.Add(chkReupSubtitleItalic);
            flp.Controls.Add(MkLbl("Từ/dòng"));
            flp.Controls.Add(numReupSubtitleWordsPerLine);

            pnlReupSubtitleToolbar.Controls.Add(flp);
        }

        private static ComboBox CreateReupSubtitleCombo(string name, int width)
        {
            return new ComboBox
            {
                Name = name,
                Width = width,
                Height = 24,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(4, 6, 6, 4)
            };
        }

        /// <summary>Ẩn control đã chuyển sang lưới — tránh trùng trên toolbar.</summary>
        private void HideDetachedReupEditorControls()
        {
            if (chkReupUseVisualHookSfx != null)
            {
                chkReupUseVisualHookSfx.Visible = false;
            }

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

        private void BuildReupCommandBar()
        {
            EnsureReupPlaybackButtons();

            var barColor = Color.FromArgb(36, 39, 48);
            pnlReupCommandBar = CreateReupAutoSizeBar("pnlReupCommandBar", barColor);

            var flpReupActions = CreateReupWrapFlowPanel("flpReupActions", barColor);

            void AddBtn(Button btn)
            {
                if (btn == null)
                {
                    return;
                }

                btn.Parent?.Controls.Remove(btn);
                ApplyReupCommandBarButtonMetrics(btn);
                flpReupActions.Controls.Add(btn);
            }

            AddBtn(btnVideoReupAddManualRow);
            AddBtn(btnPushSelectionToVideoReup);

            btnVideoReupStop = CreateReupJellyButton("btnVideoReupStop", "D\u1EEBng h\u00E0ng \u0111\u1EE3i", ReupTintStop, 124);
            btnVideoReupStop.Click += btnVideoReupStop_Click;
            AddBtn(btnVideoReupStop);

            btnVideoReupPushToAutoPost = CreateReupJellyButton(
                "btnVideoReupPushToAutoPost",
                "Đẩy sang Đăng tự động",
                ReupTintAffiliateImport,
                196);
            btnVideoReupPushToAutoPost.Click += btnVideoReupPushToAutoPost_Click;
            AddBtn(btnVideoReupPushToAutoPost);

            pnlReupCommandBar.Controls.Add(flpReupActions);
        }

        private void BuildReupRenderActionBar()
        {
            EnsureReupPlaybackButtons();

            if (btnVideoReupProcessVideo == null || btnVideoReupProcessVideo.IsDisposed)
            {
                btnVideoReupProcessVideo = CreateAffiliateJellyButton(
                    "btnVideoReupProcessVideo",
                    "Render & Đóng gói",
                    AffiliateTintHunt);
                PrepareAffiliateToolbarButtonForFlow(btnVideoReupProcessVideo);
                btnVideoReupProcessVideo.Click += btnProcessVideo_Click;
            }
            else
            {
                btnVideoReupProcessVideo.Parent?.Controls.Remove(btnVideoReupProcessVideo);
            }

            btnPlaySource.Parent?.Controls.Remove(btnPlaySource);
            btnPlayOutput.Parent?.Controls.Remove(btnPlayOutput);
            btnPlaySource.Text = "Video nguồn";
            btnPlayOutput.Text = "Video thành phẩm";
            ApplyReupCommandBarButtonMetrics(btnPlaySource);
            ApplyReupCommandBarButtonMetrics(btnPlayOutput);

            var renderWidth = Math.Max(
                ReupPrimaryActionMinWidth + 40,
                TextRenderer.MeasureText(
                    btnVideoReupProcessVideo.Text,
                    ReupPrimaryActionFont,
                    new Size(int.MaxValue, ReupPrimaryActionHeight),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width
                + ReupCommandHorizontalPad + 32);
            btnVideoReupProcessVideo.Font = ReupPrimaryActionFont;
            btnVideoReupProcessVideo.AutoSize = false;
            btnVideoReupProcessVideo.Height = ReupPrimaryActionHeight;
            btnVideoReupProcessVideo.Width = renderWidth;
            btnVideoReupProcessVideo.MinimumSize = new Size(renderWidth, ReupPrimaryActionHeight);
            btnVideoReupProcessVideo.MaximumSize = new Size(renderWidth, ReupPrimaryActionHeight);
            btnVideoReupProcessVideo.Margin = new Padding(4, 4, 12, 6);
            btnVideoReupProcessVideo.Anchor = AnchorStyles.None;
            if (btnVideoReupProcessVideo is JellyButton renderJelly)
            {
                renderJelly.JellyFillOpacity = 1f - JellyButton.DefaultTransparency;
                renderJelly.JellyTint = ReupTintRender;
                renderJelly.ForeColor = Color.FromArgb(245, 247, 250);
            }

            btnVideoReupProcessVideo.Enabled = true;

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
            flpRender.Controls.Add(btnPlaySource);
            flpRender.Controls.Add(btnVideoReupProcessVideo);
            flpRender.Controls.Add(btnPlayOutput);

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
        private void RefreshVideoOcrService()
        {
            if (_videoReupRemixService == null)
            {
                return;
            }

            try
            {
                var settings = _configManager.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
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
                    return;
                }

                var tessdataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                _videoReupRemixService.OcrService = new Services.VideoOcrService(ffmpegPath, tessdataPath);
            }
            catch (Exception ex)
            {
                LogVideoReup("[OCR] RefreshVideoOcrService lỗi (bỏ qua): " + ex.Message);
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

        private void btnVideoReupStop_Click(object sender, System.EventArgs e)
        {
            CancelAllVideoReupBatchJobs();
            _globalJobQueue?.ClearAll();
            SetVideoReupProgress("Đã dừng hàng đợi render", 0);
            LogVideoReup("Đã dừng các job Video reup đang chờ/chạy.");
        }
    }
}
