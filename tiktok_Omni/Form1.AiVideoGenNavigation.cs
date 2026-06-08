using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private enum AiVideoGenMode
        {
            Slideshow = 0,
            AffiliateDeep = 1,
            Mascot = 2,
            Philosophy = 3,
            VideoReup = 4
        }

        private AiVideoGenMode _selectedAiVideoGenMode = AiVideoGenMode.Slideshow;
        private bool _aiVideoGenSubNavExpanded;

        private static readonly Color AiVideoGenSubNavActiveColor = Color.FromArgb(56, 142, 88);
        private static readonly Color AiVideoGenSubNavInactiveColor = Color.FromArgb(24, 26, 32);
        private static readonly Color AiVideoGenSectionHeaderColor = Color.FromArgb(38, 42, 52);

        private Panel pnlAiVideoGenModeHost;
        private Panel pnlModeSlideshow;
        private Panel pnlModeAffiliateDeep;
        private Panel pnlModeMascot;
        private Panel pnlModePhilosophy;
        private Panel pnlModeVideoReup;
        private Panel pnlSlideshowGridHost;
        private Panel pnlDeepDiveGridHost;
        private Button btnModeSlideshow;
        private Button btnModeAffiliateDeep;
        private Button btnModeMascot;
        private Button btnModePhilosophy;
        private Button btnModeVideoReup;

        private Panel pnlAiVideoGenTopHeader;
        private Panel pnlAiVideoGenModeIndicator;
        private Panel pnlAiVideoGenModeAccentStripe;
        private Label lblAiVideoGenModeTitle;
        private Label lblAiVideoGenModeHint;

        private int GetSelectedAiVideoGenModeIndex() => (int)_selectedAiVideoGenMode;

        private void SelectAiVideoGenMode(AiVideoGenMode mode)
        {
            _selectedAiVideoGenMode = mode;
            if (ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen))
            {
                SetAiVideoGenSubNavExpanded(true);
            }
            HighlightAiVideoGenSidebar(mode);
            ShowActiveAiVideoGenModePanel();
            ApplyAiVideoGenModeUiVisibility();
            RefreshAiVideoGenModeIndicator();
            AttachAiRenderProgressPanelToSelectedModePanel();
            RefreshAiVideoGenModeReadinessLabels();
            if (mode == AiVideoGenMode.VideoReup)
            {
                _ = RefreshVideoReupMusicComboAsync();
                BeginInvoke(new Action(() => LayoutVideoReupShell()));
            }
            else if (mode == AiVideoGenMode.Philosophy)
            {
                BeginInvoke(new Action(() => EnsureProductionQueueGridBandHeight(dgvPhilosophyQueue)));
            }
            else if (mode == AiVideoGenMode.Mascot)
            {
                BeginInvoke(new Action(() => EnsureProductionQueueGridBandHeight(dgvMascotQueue)));
            }
        }

        private void BuildAiVideoGenModeNavigation()
        {
            pnlAiVideoGenModeHost = new Panel
            {
                Name = "pnlAiVideoGenModeHost",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42),
                Padding = new Padding(4, 4, 8, 4)
            };

            pnlModeSlideshow = CreateAiVideoGenModePanel(nameof(pnlModeSlideshow));
            pnlModeAffiliateDeep = CreateAiVideoGenModePanel(nameof(pnlModeAffiliateDeep));
            pnlModeMascot = CreateAiVideoGenModePanel(nameof(pnlModeMascot));
            pnlModePhilosophy = CreateAiVideoGenModePanel(nameof(pnlModePhilosophy));
            pnlModeVideoReup = CreateAiVideoGenModePanel(nameof(pnlModeVideoReup));
            pnlModeVideoReup.AutoScroll = false;

            pnlAiVideoGenModeHost.Controls.Add(pnlModeSlideshow);
            pnlAiVideoGenModeHost.Controls.Add(pnlModeAffiliateDeep);
            pnlAiVideoGenModeHost.Controls.Add(pnlModeMascot);
            pnlAiVideoGenModeHost.Controls.Add(pnlModePhilosophy);
            pnlAiVideoGenModeHost.Controls.Add(pnlModeVideoReup);

            btnModeSlideshow = CreateAiVideoGenModeNavButton("Slideshow", AiVideoGenMode.Slideshow);
            btnModeAffiliateDeep = CreateAiVideoGenModeNavButton("Affiliate chuyên", AiVideoGenMode.AffiliateDeep);
            btnModeMascot = CreateAiVideoGenModeNavButton("Mascot", AiVideoGenMode.Mascot);
            btnModePhilosophy = CreateAiVideoGenModeNavButton("Triết lý", AiVideoGenMode.Philosophy);
            btnModeVideoReup = CreateAiVideoGenModeNavButton("Reup", AiVideoGenMode.VideoReup);
        }

        /// <summary>Thanh tiêu đề + vạch màu trái — biết đang ở loại video nào.</summary>
        internal void BuildAiVideoGenModeIndicatorUi()
        {
            if (pnlAiVideoGenModeIndicator != null)
            {
                return;
            }

            pnlAiVideoGenModeAccentStripe = new Panel
            {
                Name = "pnlAiVideoGenModeAccentStripe",
                Dock = DockStyle.Left,
                Width = 6,
                BackColor = AiVideoGenSubNavActiveColor
            };

            lblAiVideoGenModeTitle = new Label
            {
                Name = "lblAiVideoGenModeTitle",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 12, 0)
            };

            lblAiVideoGenModeHint = new Label
            {
                Name = "lblAiVideoGenModeHint",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(165, 172, 188),
                Margin = new Padding(0, 2, 0, 0)
            };

            var tblModeText = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(28, 30, 38),
                Padding = new Padding(10, 6, 8, 6)
            };
            tblModeText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblModeText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblModeText.Controls.Add(lblAiVideoGenModeTitle, 0, 0);
            tblModeText.Controls.Add(lblAiVideoGenModeHint, 0, 1);

            pnlAiVideoGenModeIndicator = new Panel
            {
                Name = "pnlAiVideoGenModeIndicator",
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(28, 30, 38),
                Padding = new Padding(0)
            };
            pnlAiVideoGenModeIndicator.Controls.Add(pnlAiVideoGenModeAccentStripe);
            pnlAiVideoGenModeIndicator.Controls.Add(tblModeText);

            pnlAiVideoGenTopHeader = new Panel
            {
                Name = "pnlAiVideoGenTopHeader",
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            if (flpAiVideoGenHeader != null)
            {
                flpAiVideoGenHeader.Dock = DockStyle.Top;
                pnlAiVideoGenTopHeader.Controls.Add(flpAiVideoGenHeader);
            }

            pnlAiVideoGenTopHeader.Controls.Add(pnlAiVideoGenModeIndicator);
            RefreshAiVideoGenModeIndicator();
        }

        private void RefreshAiVideoGenModeIndicator()
        {
            if (lblAiVideoGenModeTitle == null || lblAiVideoGenModeHint == null || pnlAiVideoGenModeAccentStripe == null)
            {
                return;
            }

            GetAiVideoGenModeDisplay(_selectedAiVideoGenMode, out var title, out var hint, out var accent);
            lblAiVideoGenModeTitle.Text = title;
            lblAiVideoGenModeHint.Text = hint;
            pnlAiVideoGenModeAccentStripe.BackColor = accent;
            UpdateAiVideoGenNavHeaderChevron();
        }

        private static void GetAiVideoGenModeDisplay(AiVideoGenMode mode, out string title, out string hint, out Color accent)
        {
            switch (mode)
            {
                case AiVideoGenMode.AffiliateDeep:
                    title = "Affiliate chuyên sâu";
                    hint = "4+ ảnh / 1 sản phẩm — storyboard & render";
                    accent = Color.FromArgb(56, 142, 88);
                    return;
                case AiVideoGenMode.Mascot:
                    title = "Mascot";
                    hint = "Kênh mascot — lip-sync & sản xuất";
                    accent = Color.FromArgb(160, 90, 210);
                    return;
                case AiVideoGenMode.Philosophy:
                    title = "Triết lý";
                    hint = "Video quote / triết lý sống";
                    accent = Color.FromArgb(210, 170, 80);
                    return;
                case AiVideoGenMode.VideoReup:
                    title = "Video Reup";
                    hint = "Remix TikTok — hook, voiceover, nhạc/phim";
                    accent = Color.FromArgb(220, 110, 50);
                    return;
                default:
                    title = "Slideshow";
                    hint = "Nhiều ảnh / nhiều sản phẩm — AI render";
                    accent = Color.FromArgb(76, 110, 245);
                    return;
            }
        }

        private static Color GetAiVideoGenModeAccentColor(AiVideoGenMode mode)
        {
            GetAiVideoGenModeDisplay(mode, out _, out _, out var accent);
            return accent;
        }

        /// <summary>Gắn 5 nút chế độ video vào sidebar chính, ngay dưới «AI Video Gen».</summary>
        private void WireAiVideoGenModeButtonsToMainSidebar()
        {
            if (pnlSidebarNav == null || btnNavAiVideo == null)
            {
                return;
            }

            var modeButtons = new[]
            {
                btnModeSlideshow,
                btnModeAffiliateDeep,
                btnModeMascot,
                btnModePhilosophy,
                btnModeVideoReup
            };

            foreach (var btn in modeButtons.Where(b => b != null))
            {
                if (btn.Parent != null)
                {
                    btn.Parent.Controls.Remove(btn);
                }
            }

            var insertAt = pnlSidebarNav.Controls.IndexOf(btnNavAiVideo);
            if (insertAt < 0)
            {
                insertAt = pnlSidebarNav.Controls.Count - 1;
            }

            insertAt++;
            for (var i = modeButtons.Length - 1; i >= 0; i--)
            {
                var btn = modeButtons[i];
                if (btn == null)
                {
                    continue;
                }

                pnlSidebarNav.Controls.Add(btn);
                pnlSidebarNav.Controls.SetChildIndex(btn, insertAt);
            }

            SetAiVideoGenSubNavExpanded(ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen));
        }

        private void ToggleAiVideoGenSubNavFromNav()
        {
            SetAiVideoGenSubNavExpanded(!_aiVideoGenSubNavExpanded);
            HighlightAiVideoGenSidebar(_selectedAiVideoGenMode);
        }

        private void SetAiVideoGenSubNavExpanded(bool expanded)
        {
            _aiVideoGenSubNavExpanded = expanded;
            foreach (var btn in GetAiVideoGenModeSidebarButtons())
            {
                if (btn != null)
                {
                    btn.Visible = expanded;
                }
            }

            UpdateAiVideoGenNavHeaderChevron();
            pnlSidebarNav?.PerformLayout();
        }

        private void UpdateAiVideoGenNavHeaderChevron()
        {
            if (btnNavAiVideo == null)
            {
                return;
            }

            var onAiTab = ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen);
            GetAiVideoGenModeDisplay(_selectedAiVideoGenMode, out var modeTitle, out _, out _);
            var chevron = _aiVideoGenSubNavExpanded ? "▼" : "▶";
            btnNavAiVideo.Text = onAiTab
                ? chevron + "  ✨  AI tạo video · " + modeTitle
                : chevron + "  ✨  AI tạo video";
        }

        private Button[] GetAiVideoGenModeSidebarButtons()
        {
            return new[]
            {
                btnModeSlideshow,
                btnModeAffiliateDeep,
                btnModeMascot,
                btnModePhilosophy,
                btnModeVideoReup
            };
        }

        private static Panel CreateAiVideoGenModePanel(string name)
        {
            return new Panel
            {
                Name = name,
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(31, 34, 42),
                Padding = new Padding(4, 4, 4, 4)
            };
        }

        private Button CreateAiVideoGenModeNavButton(string text, AiVideoGenMode mode)
        {
            var btn = new Button
            {
                Text = text,
                Width = 196,
                Height = 40,
                Margin = new Padding(16, 0, 0, 4),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(26, 28, 35),
                Tag = mode,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 44, 54);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 110, 68);
            btn.Paint += AiVideoGenModeNavButton_Paint;
            btn.Click += (_, __) =>
            {
                SwitchToMainTab(tabAiVideoGen);
                SelectAiVideoGenMode(mode);
            };
            return btn;
        }

        private void AiVideoGenModeNavButton_Paint(object sender, PaintEventArgs e)
        {
            if (!(sender is Button btn) || !TryGetAiVideoGenModeFromButton(btn, out var mode))
            {
                return;
            }

            var onAiTab = ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen);
            if (!onAiTab || mode != _selectedAiVideoGenMode)
            {
                return;
            }

            var accent = GetAiVideoGenModeAccentColor(mode);
            using (var brush = new SolidBrush(accent))
            {
                e.Graphics.FillRectangle(brush, 0, 0, 5, btn.ClientSize.Height);
            }
        }

        private Button GetAiVideoGenModeButton(AiVideoGenMode mode)
        {
            switch (mode)
            {
                case AiVideoGenMode.Slideshow:
                    return btnModeSlideshow;
                case AiVideoGenMode.AffiliateDeep:
                    return btnModeAffiliateDeep;
                case AiVideoGenMode.Mascot:
                    return btnModeMascot;
                case AiVideoGenMode.Philosophy:
                    return btnModePhilosophy;
                case AiVideoGenMode.VideoReup:
                    return btnModeVideoReup;
                default:
                    return btnModeSlideshow;
            }
        }

        private static string GetAiVideoGenModeNavCaption(AiVideoGenMode mode)
        {
            switch (mode)
            {
                case AiVideoGenMode.Slideshow:
                    return "Slideshow";
                case AiVideoGenMode.AffiliateDeep:
                    return "Affiliate chuyên";
                case AiVideoGenMode.Mascot:
                    return "Mascot";
                case AiVideoGenMode.Philosophy:
                    return "Triết lý";
                case AiVideoGenMode.VideoReup:
                    return "Reup";
                default:
                    return mode.ToString();
            }
        }

        private static bool TryGetAiVideoGenModeFromButton(Button btn, out AiVideoGenMode mode)
        {
            mode = default;
            if (btn?.Tag == null)
            {
                return false;
            }

            if (btn.Tag is AiVideoGenMode enumMode)
            {
                mode = enumMode;
                return true;
            }

            if (btn.Tag is int intMode && Enum.IsDefined(typeof(AiVideoGenMode), intMode))
            {
                mode = (AiVideoGenMode)intMode;
                return true;
            }

            return false;
        }

        private void ApplyAiVideoGenSubNavButtonStyle(Button btn, AiVideoGenMode mode, bool isActive)
        {
            if (btn == null || btn.IsDisposed)
            {
                return;
            }

            var caption = GetAiVideoGenModeNavCaption(mode);
            btn.Text = isActive ? "▸ " + caption : "   " + caption;
            btn.Font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular);
            btn.BackColor = isActive ? Color.FromArgb(34, 38, 48) : AiVideoGenSubNavInactiveColor;
            btn.ForeColor = isActive ? Color.White : Color.FromArgb(175, 180, 192);
            btn.FlatAppearance.MouseOverBackColor = isActive
                ? Color.FromArgb(42, 46, 56)
                : Color.FromArgb(34, 38, 46);
            btn.FlatAppearance.MouseDownBackColor = isActive
                ? Color.FromArgb(48, 52, 62)
                : Color.FromArgb(50, 110, 68);
            btn.Invalidate();
        }

        private void ApplyAiVideoGenSectionHeaderStyle(Button btn, bool onAiVideoGenTab)
        {
            if (btn == null || btn.IsDisposed)
            {
                return;
            }

            if (onAiVideoGenTab)
            {
                btn.BackColor = AiVideoGenSectionHeaderColor;
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            }
            else
            {
                ApplySidebarButtonStyle(btn, false);
            }

            btn.Invalidate();
        }

        private void HighlightAiVideoGenSidebar(AiVideoGenMode activeMode)
        {
            if (pnlSidebar == null)
            {
                return;
            }

            var onAiVideoGenTab = ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen);

            if (btnNavSettings != null)
            {
                ApplySidebarButtonStyle(
                    btnNavSettings,
                    ReferenceEquals(tabMain?.SelectedTab, tabSetting));
            }

            if (pnlSidebarNav != null)
            {
                foreach (Control ctrl in pnlSidebarNav.Controls)
                {
                    if (!(ctrl is Button btn))
                    {
                        continue;
                    }

                    if (IsAiVideoGenModeSidebarButton(btn))
                    {
                        if (!btn.Visible)
                        {
                            continue;
                        }

                        if (!TryGetAiVideoGenModeFromButton(btn, out var mode))
                        {
                            continue;
                        }

                        ApplyAiVideoGenSubNavButtonStyle(btn, mode, mode == activeMode);
                    }
                    else if (ReferenceEquals(btn, btnNavAiVideo))
                    {
                        ApplyAiVideoGenSectionHeaderStyle(btn, onAiVideoGenTab);
                    }
                    else
                    {
                        ApplySidebarButtonStyle(btn, false);
                    }
                }
            }

        }

        private static bool IsAiVideoGenModeSidebarButton(Button btn)
        {
            return btn != null && btn.Tag is AiVideoGenMode;
        }

        private void ShowActiveAiVideoGenModePanel()
        {
            if (pnlAiVideoGenModeHost == null)
            {
                return;
            }

            var panels = new[]
            {
                pnlModeSlideshow,
                pnlModeAffiliateDeep,
                pnlModeMascot,
                pnlModePhilosophy,
                pnlModeVideoReup
            };

            foreach (var panel in panels)
            {
                if (panel == null)
                {
                    continue;
                }

                panel.Visible = false;
            }

            var active = GetActiveAiVideoGenModePanel();
            if (active != null)
            {
                active.Visible = true;
                active.BringToFront();
            }
        }

        private Panel GetActiveAiVideoGenModePanel()
        {
            switch (_selectedAiVideoGenMode)
            {
                case AiVideoGenMode.Slideshow:
                    return pnlModeSlideshow;
                case AiVideoGenMode.AffiliateDeep:
                    return pnlModeAffiliateDeep;
                case AiVideoGenMode.Mascot:
                    return pnlModeMascot;
                case AiVideoGenMode.Philosophy:
                    return pnlModePhilosophy;
                case AiVideoGenMode.VideoReup:
                    return pnlModeVideoReup;
                default:
                    return pnlModeSlideshow;
            }
        }

        private void AttachAiRenderProgressPanelToSelectedModePanel()
        {
            if (pnlAiVideoGenRenderStatusHost != null)
            {
                if (tabAiVideoGen?.Controls.Contains(pnlAiVideoGenRenderStatusHost) == true)
                {
                    tabAiVideoGen.Controls.Remove(pnlAiVideoGenRenderStatusHost);
                }

                pnlAiVideoGenRenderStatusHost.Visible = false;
            }

            if (grpAiRenderProgress != null)
            {
                grpAiRenderProgress.Visible = false;
            }
        }
    }
}
