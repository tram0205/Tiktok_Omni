using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font ReupJellyButtonFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        private const int ReupCommandButtonHeight = 44;
        private const int ReupCommandHorizontalPad = 30;
        private static readonly Color ReupTintAddRow = Color.FromArgb(55, 95, 160);
        private static readonly Color ReupTintAffiliateImport = Color.FromArgb(76, 110, 245);
        private static readonly Color ReupTintRender = Color.FromArgb(56, 158, 88);
        private static readonly Color ReupTintRenderBatch = Color.FromArgb(48, 138, 78);
        private static readonly Color ReupTintStop = Color.FromArgb(195, 72, 72);
        private static readonly Color ReupTintSaveDraft = Color.FromArgb(55, 95, 140);
        private static readonly Color ReupTintOutput = Color.FromArgb(88, 94, 112);

        private Panel pnlReupCommandBar;
        private Panel pnlReupConfigStrip;
        private Panel pnlReupGridWrap;
        private Button btnVideoReupStop;
        private Button btnVideoReupSaveDraft;

        private void WireVideoReupTabLayout()
        {
            if (pnlModeVideoReup == null || dgvVideoReupInput == null)
            {
                return;
            }

            pnlModeVideoReup.SuspendLayout();
            pnlModeVideoReup.Controls.Clear();
            pnlModeVideoReup.Padding = new Padding(4);
            pnlModeVideoReup.AutoScroll = false;

            BuildReupCommandBar();
            BuildReupInlineConfig();
            BuildReupMainSplit();

            pnlVideoReupStatus.Dock = DockStyle.Bottom;
            pnlVideoReupStatus.Height = 88;
            pnlVideoReupStatus.MinimumSize = new Size(0, 72);

            pnlModeVideoReup.Controls.Add(_tblReupMain);
            pnlModeVideoReup.Controls.Add(pnlReupConfigStrip);
            pnlModeVideoReup.Controls.Add(pnlVideoReupStatus);
            pnlModeVideoReup.Controls.Add(pnlReupCommandBar);

            pnlModeVideoReup.ResumeLayout(true);
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
                Margin = new Padding(0, 0, 10, 0),
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
            btn.Margin = new Padding(0, 0, 10, 0);
            if (btn is JellyButton jelly)
            {
                jelly.JellyFillOpacity = 1f - JellyButton.DefaultTransparency;
                jelly.ForeColor = Color.FromArgb(245, 247, 250);
                jelly.Tag = JellyButton.ChromeTag;
                jelly.AccessibleName = JellyButton.ChromeTag;
            }
        }

        private TableLayoutPanel _tblReupMain;

        private void BuildReupMainSplit()
        {
            pnlReupGridWrap = new Panel
            {
                Name = "pnlReupGridWrap",
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 2, 4, 2),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            if (lblVideoReupReadiness != null)
            {
                lblVideoReupReadiness.Dock = DockStyle.Top;
                lblVideoReupReadiness.Height = 28;
                lblVideoReupReadiness.AutoSize = false;
                lblVideoReupReadiness.Padding = new Padding(4, 4, 4, 2);
            }

            dgvVideoReupInput.Dock = DockStyle.Fill;
            dgvVideoReupInput.Margin = new Padding(0);
            dgvVideoReupInput.MinimumSize = new Size(120, 80);

            pnlReupGridWrap.Controls.Add(dgvVideoReupInput);
            if (lblVideoReupReadiness != null)
            {
                pnlReupGridWrap.Controls.Add(lblVideoReupReadiness);
            }

            if (pnlReupPreviewHost != null)
            {
                pnlReupPreviewHost.Dock = DockStyle.Fill;
                pnlReupPreviewHost.MinimumSize = new Size(200, 120);
            }

            _tblReupMain = new TableLayoutPanel
            {
                Name = "tblReupMain",
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = pnlModeVideoReup.BackColor
            };
            _tblReupMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _tblReupMain.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248F));
            _tblReupMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _tblReupMain.Controls.Add(pnlReupGridWrap, 0, 0);
            if (pnlReupPreviewHost != null)
            {
                _tblReupMain.Controls.Add(pnlReupPreviewHost, 1, 0);
            }
        }

        private void BuildReupInlineConfig()
        {
            pnlReupConfigStrip = new Panel
            {
                Name = "pnlReupConfigStrip",
                Dock = DockStyle.Bottom,
                Height = 174,
                MinimumSize = new Size(0, 166),
                BackColor = Color.FromArgb(31, 34, 42),
                Padding = new Padding(6, 4, 6, 4)
            };

            if (pnlReupEditor != null)
            {
                pnlReupEditor.Parent?.Controls.Remove(pnlReupEditor);
                pnlReupEditor.Dock = DockStyle.Fill;
                pnlReupEditor.AutoSize = false;
                pnlReupEditor.AutoScroll = false;
                pnlReupEditor.Padding = new Padding(2, 0, 2, 0);
                pnlReupConfigStrip.Controls.Add(pnlReupEditor);
            }
        }

        private void BuildReupCommandBar()
        {
            pnlReupCommandBar = new Panel
            {
                Name = "pnlReupCommandBar",
                Dock = DockStyle.Bottom,
                Height = 56,
                MinimumSize = new Size(0, 52),
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(36, 39, 48)
            };

            var flp = new FlowLayoutPanel
            {
                Name = "flpReupCommandBar",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = pnlReupCommandBar.BackColor,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            void AddBtn(Button btn)
            {
                if (btn == null)
                {
                    return;
                }

                btn.Parent?.Controls.Remove(btn);
                ApplyReupCommandBarButtonMetrics(btn);
                flp.Controls.Add(btn);
            }

            AddBtn(btnVideoReupAddManualRow);
            AddBtn(btnPushSelectionToVideoReup);

            if (btnVideoReupRenderVideo != null)
            {
                btnVideoReupRenderVideo.Text = "B\u1EAFt \u0111\u1EA7u render";
                ApplyReupCommandBarButtonMetrics(btnVideoReupRenderVideo);
            }

            AddBtn(btnVideoReupRenderVideo);
            AddBtn(btnVideoReupRenderBatch);

            btnVideoReupStop = CreateReupJellyButton("btnVideoReupStop", "D\u1EEBng h\u00E0ng \u0111\u1EE3i", ReupTintStop, 124);
            btnVideoReupStop.Click += btnVideoReupStop_Click;
            AddBtn(btnVideoReupStop);

            btnVideoReupSaveDraft = CreateReupJellyButton("btnVideoReupSaveDraft", "L\u01B0u nh\u00E1p", ReupTintSaveDraft, 108);
            btnVideoReupSaveDraft.Click += btnVideoReupSaveDraft_Click;
            AddBtn(btnVideoReupSaveDraft);

            var btnReupOutput = CreateReupJellyButton("btnVideoReupOpenOutput", "\U0001F4C2 Output", ReupTintOutput, 108);
            btnReupOutput.Click += btnOpenOutputFolder_Click;
            AddBtn(btnReupOutput);

            pnlReupCommandBar.Controls.Add(flp);
        }

        private void btnVideoReupStop_Click(object sender, System.EventArgs e)
        {
            CancelAllVideoReupBatchJobs();
            _globalJobQueue?.ClearAll();
            SetVideoReupProgress("Đã dừng hàng đợi render", 0);
            LogVideoReup("Đã dừng các job Video reup đang chờ/chạy.");
        }

        private void btnVideoReupSaveDraft_Click(object sender, System.EventArgs e)
        {
            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            FlushVideoReupDraftToDisk();
            LogVideoReup("Đã lưu nháp danh sách Video reup.");
        }
    }
}
