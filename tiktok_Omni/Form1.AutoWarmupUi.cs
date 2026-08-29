using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Color WarmupPanelBack = Color.FromArgb(32, 34, 44);
        private static readonly Color WarmupFieldBack = Color.FromArgb(45, 49, 60);
        private static readonly Font WarmupFieldFont = AppInputFont;
        private static readonly Font WarmupLabelFont = AppLabelFont;
        private const int WarmupFieldHeight = AppDefaultInputHeight;
        private const int WarmupToolbarButtonHeight = AppJellyButtonHeight;
        private const int WarmupToolbarRowHeight = AppJellyButtonHeight;
        private const int WarmupSectionGap = 6;

        private static readonly Color WarmupTintAdd = Color.FromArgb(48, 158, 102);
        private static readonly Color WarmupTintRun = Color.FromArgb(56, 128, 88);
        private static readonly Color WarmupTintStop = Color.FromArgb(195, 72, 72);
        private static readonly Color WarmupTintPause = Color.FromArgb(168, 128, 52);
        private static readonly Color WarmupTintResume = Color.FromArgb(68, 118, 178);
        private static readonly Color WarmupTintPauseNow = Color.FromArgb(128, 92, 168);
        private static readonly Color WarmupTintManual = Color.FromArgb(52, 142, 158);
        private static readonly Color WarmupTintNeutral = Color.FromArgb(88, 94, 112);
        private static readonly Color WarmupTintInfo = Color.FromArgb(72, 110, 150);
        private static readonly Color WarmupTintTrend = Color.FromArgb(118, 88, 158);

        private void BuildAutoWarmupZeroScrollUi()
        {
            if (tabAutoWarmup == null)
            {
                return;
            }

            tabAutoWarmup.SuspendLayout();
            tabAutoWarmup.Controls.Clear();
            tabAutoWarmup.AutoScroll = true;
            tabAutoWarmup.Padding = new Padding(6, 4, 6, 4);

            var tblWarmupMain = new TableLayoutPanel
            {
                Name = "tblWarmupMain",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = WarmupPanelBack,
                AutoSize = false,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            tblWarmupMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            // row 0: config
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // row 1: toolbar (hàng đợi / chạy)
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // row 2: lưới (fill)
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            // row 3: stats (thống kê)
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // row 4: monitor strip (status + progress)
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // row 5: log
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 315F));

            tblWarmupMain.Controls.Add(BuildWarmupConfigPanel(), 0, 0);
            tblWarmupMain.Controls.Add(BuildWarmupPrimaryActionsPanel(), 0, 1);
            var pnlWarmupGrid = BuildWarmupGridPanel();
            pnlWarmupGrid.Margin = new Padding(0, 2, 0, 0);
            tblWarmupMain.Controls.Add(pnlWarmupGrid, 0, 2);
            var pnlWarmupStats = BuildWarmupBottomActionsPanel();
            pnlWarmupStats.Margin = new Padding(0, 2, 0, 0);
            tblWarmupMain.Controls.Add(pnlWarmupStats, 0, 3);
            var pnlWarmupMonitor = BuildWarmupMonitorStrip();
            pnlWarmupMonitor.Margin = new Padding(0, 2, 0, 0);
            tblWarmupMain.Controls.Add(pnlWarmupMonitor, 0, 4);
            var pnlWarmupLog = BuildWarmupLogPanel();
            pnlWarmupLog.Margin = Padding.Empty;
            tblWarmupMain.Controls.Add(pnlWarmupLog, 0, 5);

            tabAutoWarmup.Controls.Add(tblWarmupMain);
            tabAutoWarmup.AutoScrollMinSize = Size.Empty;
            tabAutoWarmup.ResumeLayout(true);
            tabAutoWarmup.PerformLayout();
            WireWarmupTooltips();
        }

        private Panel BuildWarmupConfigPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlWarmupConfig",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(4, 4, 4, 0),
                BackColor = WarmupPanelBack
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = WarmupPanelBack,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            cbRunningProfile = new ComboBox
            {
                Name = "cbRunningProfile",
                Dock = DockStyle.Fill,
                Font = WarmupFieldFont,
                MinimumSize = new Size(100, WarmupFieldHeight),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                IntegralHeight = false,
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 10, 6)
            };
            cbRunningProfile.Items.Add("default");
            cbRunningProfile.SelectedIndex = 0;
            cbRunningProfile.SelectedIndexChanged += cbRunningProfile_SelectedIndexChanged;

            txtKeywords = new TextBox
            {
                Name = "txtKeywords",
                Dock = DockStyle.Fill,
                Font = WarmupFieldFont,
                MinimumSize = new Size(120, WarmupFieldHeight),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 10, 6)
            };

            numVideoCount = new NumericUpDown
            {
                Name = "numVideoCount",
                Minimum = 1,
                Maximum = 1000,
                Value = 5,
                Dock = DockStyle.Fill,
                Font = WarmupFieldFont,
                MinimumSize = new Size(56, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 6, 8, 6)
            };

            numWatchMin = new NumericUpDown
            {
                Name = "numWatchMin",
                Minimum = 30,
                Maximum = 300,
                Value = 80,
                Font = WarmupFieldFont,
                Width = 110,
                MinimumSize = new Size(110, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 4, 0)
            };
            numWatchMax = new NumericUpDown
            {
                Name = "numWatchMax",
                Minimum = 30,
                Maximum = 300,
                Value = 150,
                Font = WarmupFieldFont,
                Width = 110,
                MinimumSize = new Size(110, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(4, 0, 0, 0)
            };
            var flpWatchInline = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = WarmupPanelBack,
                Margin = new Padding(0, 6, 0, 6),
                Padding = Padding.Empty
            };
            flpWatchInline.Controls.Add(numWatchMin);
            flpWatchInline.Controls.Add(new Label
            {
                Text = "–",
                AutoSize = true,
                Font = WarmupLabelFont,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(2, 8, 2, 0)
            });
            flpWatchInline.Controls.Add(numWatchMax);

            var tblTopRow = new TableLayoutPanel
            {
                Name = "tblWarmupTopConfig",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 8,
                RowCount = 1,
                BackColor = WarmupPanelBack,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8F));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            tblTopRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblTopRow.Controls.Add(CreateWarmupInlineLabel("Profile chạy"), 0, 0);
            tblTopRow.Controls.Add(cbRunningProfile, 1, 0);
            tblTopRow.Controls.Add(CreateWarmupInlineLabel("Từ khóa ngách"), 2, 0);
            tblTopRow.Controls.Add(txtKeywords, 3, 0);
            tblTopRow.Controls.Add(CreateWarmupInlineLabel("Số video"), 4, 0);
            tblTopRow.Controls.Add(numVideoCount, 5, 0);
            tblTopRow.Controls.Add(CreateWarmupInlineLabel("Giữ chân (%)"), 6, 0);
            tblTopRow.Controls.Add(flpWatchInline, 7, 0);

            tbl.Controls.Add(tblTopRow, 0, 0);

            numLikeProbability = new NumericUpDown
            {
                Name = "numLikeProbability",
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Font = WarmupFieldFont,
                Width = 64,
                MinimumSize = new Size(64, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 4, 0)
            };
            numShareProbability = new NumericUpDown
            {
                Name = "numShareProbability",
                Minimum = 0,
                Maximum = 100,
                Value = 20,
                Font = WarmupFieldFont,
                Width = 64,
                MinimumSize = new Size(64, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 4, 0)
            };
            chkAutoComment = new CheckBox
            {
                Name = "chkAutoComment",
                Text = "Bật bình luận AI",
                AutoSize = true,
                Font = WarmupFieldFont,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 8, 0)
            };
            numCommentProbability = new NumericUpDown
            {
                Name = "numCommentProbability",
                Minimum = 0,
                Maximum = 100,
                Value = 10,
                Font = WarmupFieldFont,
                Width = 64,
                MinimumSize = new Size(64, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 4, 0)
            };
            chkAutoComment.CheckedChanged += (_, __) =>
            {
                if (numCommentProbability != null)
                {
                    numCommentProbability.Enabled = chkAutoComment.Checked;
                }
            };

            var flpRunMode = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = WarmupPanelBack,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            flpRunMode.Controls.Add(new Label
            {
                Text = "Like %",
                AutoSize = true,
                Font = WarmupLabelFont,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 6, 6, 0)
            });
            flpRunMode.Controls.Add(numLikeProbability);
            flpRunMode.Controls.Add(new Label
            {
                Text = "Share %",
                AutoSize = true,
                Font = WarmupLabelFont,
                ForeColor = Color.LightGray,
                Margin = new Padding(12, 6, 6, 0)
            });
            flpRunMode.Controls.Add(numShareProbability);
            flpRunMode.Controls.Add(chkAutoComment);
            flpRunMode.Controls.Add(new Label
            {
                Text = "Comment %",
                AutoSize = true,
                Font = WarmupLabelFont,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 6, 6, 0)
            });
            flpRunMode.Controls.Add(numCommentProbability);
            flpRunMode.Controls.Add(new Label { Text = "Chế độ chạy:", AutoSize = true, Font = WarmupLabelFont, ForeColor = Color.Gainsboro, Margin = new Padding(12, 4, 8, 0) });
            rbDryRun = new RadioButton
            {
                Name = "rbDryRun",
                Text = "Dry Run (mô phỏng an toàn)",
                AutoSize = true,
                Font = WarmupFieldFont,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 16, 0)
            };
            rbLiveRun = new RadioButton
            {
                Name = "rbLiveRun",
                Text = "Live Run (hành động thật)",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 20, 0)
            };
            flpRunMode.Controls.Add(rbDryRun);
            flpRunMode.Controls.Add(rbLiveRun);

            chkEnableWarmupSchedule = new CheckBox
            {
                Name = "chkEnableWarmupSchedule",
                Text = "Lập lịch warm-up",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 12, 0)
            };
            chkEnableWarmupSchedule.CheckedChanged += (_, __) =>
            {
                if (dtpWarmupSchedule != null)
                {
                    dtpWarmupSchedule.Enabled = chkEnableWarmupSchedule.Checked;
                }
            };
            dtpWarmupSchedule = new DateTimePicker
            {
                Name = "dtpWarmupSchedule",
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy HH:mm",
                ShowUpDown = false,
                Enabled = false,
                Font = WarmupFieldFont,
                CalendarForeColor = Color.WhiteSmoke,
                CalendarMonthBackground = WarmupFieldBack,
                CalendarTitleBackColor = WarmupFieldBack,
                CalendarTitleForeColor = Color.WhiteSmoke,
                CalendarTrailingForeColor = Color.DimGray,
                Value = DateTime.Now.AddHours(1),
                Width = 320,
                MinimumSize = new Size(300, WarmupFieldHeight),
                Margin = new Padding(0, 0, 0, 0)
            };
            flpRunMode.Controls.Add(chkEnableWarmupSchedule);
            flpRunMode.Controls.Add(dtpWarmupSchedule);
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.Controls.Add(flpRunMode, 0, 1);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        private static Label CreateWarmupInlineLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = WarmupLabelFont,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 10, 8, 6),
                MinimumSize = new Size(0, WarmupFieldHeight)
            };
        }

        private Panel BuildWarmupMonitorStrip()
        {
            var pnlMonitorTop = new Panel
            {
                Name = "pnlWarmupMonitorTop",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 3, 0, 2),
                BackColor = WarmupPanelBack,
                Margin = Padding.Empty
            };
            var tblTop = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                BackColor = WarmupPanelBack,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblWarmupQueueStatus = new Label
            {
                Name = "lblWarmupQueueStatus",
                Text = "Hàng đợi: 0 mục",
                AutoSize = true,
                Font = WarmupFieldFont,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 180, 180),
                Margin = new Padding(0, 0, 8, 2)
            };
            lblWarmupProgress = new Label
            {
                Name = "lblWarmupProgress",
                Text = "Tiến độ: 0/0 (0%)",
                AutoSize = true,
                Font = WarmupFieldFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 0, 2)
            };
            tblTop.Controls.Add(lblWarmupQueueStatus, 0, 0);
            tblTop.Controls.Add(lblWarmupProgress, 1, 0);

            pbWarmupProgress = new ProgressBar
            {
                Name = "pbWarmupProgress",
                Dock = DockStyle.Top,
                Height = 14,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 2, 0, 0)
            };
            tblTop.Controls.Add(pbWarmupProgress, 0, 1);
            tblTop.SetColumnSpan(pbWarmupProgress, 2);
            pnlMonitorTop.Controls.Add(tblTop);
            return pnlMonitorTop;
        }

        private Panel BuildWarmupGridPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlWarmupGrid",
                Dock = DockStyle.Fill,
                BackColor = WarmupPanelBack,
                Padding = new Padding(4, 0, 4, 4)
            };

            ConfigureWarmupQueueGrid();
            dgvWarmupQueue.Dock = DockStyle.Fill;
            dgvWarmupQueue.ScrollBars = ScrollBars.Both;

            var pnlStats = new Panel
            {
                Name = "pnlWarmupQueueStats",
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(0, WarmupSectionGap, 0, 0),
                BackColor = WarmupPanelBack
            };
            var flpStats = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = WarmupPanelBack
            };
            lblQueueStatsSummary = new Label
            {
                Name = "lblQueueStatsSummary",
                Text = "Hôm nay: 0 mục",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.FromArgb(180, 180, 180),
                Margin = new Padding(0, 2, 16, 2)
            };
            lblQueueStatsSuccessRate = new Label
            {
                Name = "lblQueueStatsSuccessRate",
                Text = "Tỉ lệ thành công: -",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.FromArgb(180, 180, 180),
                Margin = new Padding(0, 2, 16, 2)
            };
            lblQueueStatsAvgRetry = new Label
            {
                Name = "lblQueueStatsAvgRetry",
                Text = "Số lần thử TB: -",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.FromArgb(180, 180, 180),
                Margin = new Padding(0, 2, 16, 2)
            };
            lblQueueStatsTopFailedProfile = new Label
            {
                Name = "lblQueueStatsTopFailedProfile",
                Text = "Profile lỗi nhiều nhất: -",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.FromArgb(180, 180, 180),
                Margin = new Padding(0, 2, 0, 2)
            };
            flpStats.Controls.AddRange(new Control[]
            {
                lblQueueStatsSummary, lblQueueStatsSuccessRate,
                lblQueueStatsAvgRetry, lblQueueStatsTopFailedProfile
            });
            pnlStats.Controls.Add(flpStats);

            pnl.Controls.Add(dgvWarmupQueue);
            pnl.Controls.Add(pnlStats);
            ApplyAppGridChrome(dgvWarmupQueue);
            return pnl;
        }

        private Panel BuildWarmupLogPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlWarmupLog",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                MinimumSize = new Size(0, 52),
                BackColor = Color.FromArgb(30, 32, 40),
                Padding = new Padding(6, 8, 6, 8)
            };

            var lblTitle = new Label
            {
                Text = "  LOG — tiến trình Warmup",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(160, 180, 220),
                Font = AppCaptionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                AutoSize = false,
                Padding = new Padding(4, 0, 8, 0)
            };

            var btnClear = new Button
            {
                Name = "btnClearWarmupLog",
                Text = "Xóa",
                Dock = DockStyle.Right,
                Width = 88,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 48, 58),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = AppLabelFont,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Tag = "GlobalLogChrome",
                AccessibleName = "GlobalLogChrome"
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (_, __) =>
            {
                if (rtbWarmupLog != null && !rtbWarmupLog.IsDisposed)
                {
                    rtbWarmupLog.Clear();
                }
            };

            // Dock Right trước, Fill sau — tránh chữ tiêu đề / nút bị che.
            header.Controls.Add(btnClear);
            header.Controls.Add(lblTitle);

            rtbWarmupLog = new RichTextBox
            {
                Name = "rtbWarmupLog",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(18, 20, 26),
                ForeColor = Color.FromArgb(200, 205, 215),
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                DetectUrls = false
            };

            pnl.Controls.Add(rtbWarmupLog);
            pnl.Controls.Add(header);
            return pnl;
        }

        private Control BuildWarmupPrimaryActionsPanel()
        {
            // Panel Top + AutoSize — tránh FlowLayout WrapContents tính PreferredHeight theo
            // chiều hẹp (nút xếp nhiều hàng) rồi để trống lớn dưới nút khi layout rộng.
            var pnl = new Panel
            {
                Name = "pnlWarmupPrimaryActions",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = WarmupPanelBack,
                Padding = new Padding(2, 0, 2, 2),
                Margin = Padding.Empty
            };

            btnQueueWarmup = CreateWarmupActionButton("btnQueueWarmup", "Thêm vào hàng đợi", WarmupTintAdd);
            btnQueueWarmup.Click += btnQueueWarmup_Click;
            btnStartWarmupQueue = CreateWarmupActionButton("btnStartWarmupQueue", "Chạy", WarmupTintRun);
            btnStartWarmupQueue.Click += btnStartWarmupQueue_Click;
            btnPauseWarmupQueue = CreateWarmupActionButton("btnPauseWarmupQueue", "Tạm dừng", WarmupTintPause);
            btnPauseWarmupQueue.Visible = false;
            btnPauseWarmupQueue.Enabled = false;
            btnPauseWarmupQueue.Click += btnPauseWarmupQueue_Click;
            btnStopWarmupQueue = CreateWarmupActionButton("btnStopWarmupQueue", "Dừng hẳn", WarmupTintStop);
            btnStopWarmupQueue.Visible = false;
            btnStopWarmupQueue.Enabled = false;
            btnStopWarmupQueue.Click += btnStopWarmupQueue_Click;
            btnMoveQueueJobUp = CreateWarmupActionButton("btnMoveQueueJobUp", "Lên", WarmupTintNeutral);
            btnMoveQueueJobUp.Click += btnMoveQueueJobUp_Click;
            btnMoveQueueJobDown = CreateWarmupActionButton("btnMoveQueueJobDown", "Xuống", WarmupTintNeutral);
            btnMoveQueueJobDown.Click += btnMoveQueueJobDown_Click;
            btnRemoveQueueJob = CreateWarmupActionButton("btnRemoveQueueJob", "Xóa dòng", WarmupTintNeutral);
            btnRemoveQueueJob.Click += btnRemoveQueueJob_Click;

            var flpQueueToolbar = CreateWarmupToolbarFlow(
                "flpWarmupQueueToolbar",
                btnQueueWarmup,
                btnStartWarmupQueue,
                btnPauseWarmupQueue,
                btnStopWarmupQueue,
                btnMoveQueueJobUp,
                btnMoveQueueJobDown,
                btnRemoveQueueJob);
            pnl.Controls.Add(flpQueueToolbar);
            return pnl;
        }

        private Control BuildWarmupBottomActionsPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlWarmupStatsFilterHost",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = WarmupPanelBack,
                Padding = new Padding(4, 2, 4, 2),
                Margin = Padding.Empty
            };

            var flpStatsFilter = new FlowLayoutPanel
            {
                Name = "flpWarmupStatsFilter",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 2, 0, 2),
                Margin = Padding.Empty,
                BackColor = WarmupPanelBack
            };

            cbQueueStatsRange = new ComboBox
            {
                Name = "cbQueueStatsRange",
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                IntegralHeight = false,
                Font = WarmupFieldFont,
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Width = 240,
                Height = WarmupFieldHeight,
                MinimumSize = new Size(220, WarmupFieldHeight),
                Margin = new Padding(0, 2, 12, 2)
            };
            cbQueueStatsRange.Items.AddRange(new object[] { "Hôm nay", "7 ngày", "30 ngày", "Tùy chỉnh" });
            cbQueueStatsRange.SelectedIndex = 0;
            cbQueueStatsRange.SelectedIndexChanged += (_, __) =>
            {
                var isCustom = string.Equals(cbQueueStatsRange.SelectedItem?.ToString(), "Tùy chỉnh", StringComparison.OrdinalIgnoreCase);
                if (dtQueueStatsFrom != null)
                {
                    dtQueueStatsFrom.Enabled = isCustom;
                }

                if (dtQueueStatsTo != null)
                {
                    dtQueueStatsTo.Enabled = isCustom;
                }

                RefreshQueueStatsSummary();
            };

            dtQueueStatsFrom = new DateTimePicker
            {
                Name = "dtQueueStatsFrom",
                Format = DateTimePickerFormat.Short,
                Font = WarmupFieldFont,
                CalendarForeColor = Color.WhiteSmoke,
                CalendarMonthBackground = WarmupFieldBack,
                CalendarTitleBackColor = WarmupFieldBack,
                CalendarTitleForeColor = Color.WhiteSmoke,
                CalendarTrailingForeColor = Color.DimGray,
                Enabled = false,
                Value = DateTime.Today.AddDays(-7),
                Width = 220,
                Height = WarmupFieldHeight,
                MinimumSize = new Size(200, WarmupFieldHeight),
                Margin = new Padding(0, 2, 12, 2)
            };
            dtQueueStatsFrom.ValueChanged += (_, __) => RefreshQueueStatsSummary();
            dtQueueStatsTo = new DateTimePicker
            {
                Name = "dtQueueStatsTo",
                Format = DateTimePickerFormat.Short,
                Font = WarmupFieldFont,
                CalendarForeColor = Color.WhiteSmoke,
                CalendarMonthBackground = WarmupFieldBack,
                CalendarTitleBackColor = WarmupFieldBack,
                CalendarTitleForeColor = Color.WhiteSmoke,
                CalendarTrailingForeColor = Color.DimGray,
                Enabled = false,
                Value = DateTime.Today,
                Width = 220,
                Height = WarmupFieldHeight,
                MinimumSize = new Size(200, WarmupFieldHeight),
                Margin = new Padding(0, 2, 0, 2)
            };
            dtQueueStatsTo.ValueChanged += (_, __) => RefreshQueueStatsSummary();

            flpStatsFilter.Controls.AddRange(new Control[]
            {
                cbQueueStatsRange, dtQueueStatsFrom, dtQueueStatsTo
            });

            pnl.Controls.Add(flpStatsFilter);
            return pnl;
        }

        private void ConfigureWarmupQueueGrid()
        {
            _warmupQueueBindingList = new BindingList<WarmupQueueUiItem>();
            dgvWarmupQueue = new DataGridView
            {
                Name = "dgvWarmupQueue",
                MinimumSize = new Size(240, 100),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DataSource = _warmupQueueBindingList
            };
            dgvWarmupQueue.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = WarmupFieldFont,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            dgvWarmupQueue.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };
            dgvWarmupQueue.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "colWarmupQueueProfile",
                DataPropertyName = "Profile",
                HeaderText = "Profile",
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 10,
                MinimumWidth = 84,
                ToolTipText = "Chọn profile cho job warm-up."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Keywords",
                HeaderText = "Từ khóa",
                FillWeight = 16,
                MinimumWidth = 102,
                ToolTipText = "Từ khóa ngách — sửa trực tiếp trên lưới."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Videos",
                HeaderText = "Số video",
                FillWeight = 8,
                MinimumWidth = 72,
                ToolTipText = "Số video warm-up (1–1000)."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "colWarmupRunMode",
                DataPropertyName = "RunModeLabel",
                HeaderText = "Dry/Live",
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 8,
                MinimumWidth = 72,
                ToolTipText = "Dry = mô phỏng, Live = hành động thật."
            });
            ((DataGridViewComboBoxColumn)dgvWarmupQueue.Columns["colWarmupRunMode"]).Items.AddRange("Dry", "Live");
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "WatchRange",
                HeaderText = "Giữ chân (%)",
                FillWeight = 12,
                MinimumWidth = 96,
                ToolTipText = "Dạng 80-150 hoặc 80-150%."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LikeProbability",
                HeaderText = "Like %",
                FillWeight = 7,
                MinimumWidth = 58,
                ToolTipText = "Số video tim = % × số video (0–100)."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ShareProbability",
                HeaderText = "Share %",
                FillWeight = 7,
                MinimumWidth = 62,
                ToolTipText = "Số video share = % × số video (0–100)."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CommentProbability",
                HeaderText = "Comment %",
                FillWeight = 8,
                MinimumWidth = 72,
                ToolTipText = "Số video comment = % × số video (0 = tắt, 1–100)."
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusDisplay", HeaderText = "Trạng thái", FillWeight = 9, MinimumWidth = 88, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colWarmupScheduledAt",
                DataPropertyName = "ScheduledAtLabel",
                HeaderText = "Lịch chạy",
                FillWeight = 12,
                MinimumWidth = 118,
                ReadOnly = true,
                ToolTipText = "Bấm để chọn ngày/giờ chạy hoặc Đăng ngay"
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RetrySummary",
                HeaderText = "Retry",
                FillWeight = 11,
                MinimumWidth = 96,
                ReadOnly = true,
                ToolTipText = "Lần thử / tối đa · giờ tạo hoặc giờ thử lại (vd. 1/3 · 17:35)"
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastError", HeaderText = "Lỗi cuối", FillWeight = 14, MinimumWidth = 100, ReadOnly = true });
            ApplyAppGridChrome(dgvWarmupQueue);
            dgvWarmupQueue.CellBeginEdit += dgvWarmupQueue_CellBeginEdit;
            dgvWarmupQueue.CellEndEdit += dgvWarmupQueue_CellEndEdit;
            dgvWarmupQueue.DataError += dgvWarmupQueue_DataError;
            dgvWarmupQueue.CellClick += dgvWarmupQueue_CellClick;
            dgvWarmupQueue.KeyDown += dgvWarmupQueue_KeyDown;
            ApplyWarmupQueueProfileComboColumn();
        }

        private void ApplyWarmupQueueProfileComboColumn()
        {
            ApplyGridProfileComboColumn(dgvWarmupQueue, "colWarmupQueueProfile");
        }

        private void WireWarmupTooltips()
        {
            var warmupToolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ReshowDelay = 150, ShowAlways = true };
            warmupToolTip.SetToolTip(btnQueueWarmup, "Thêm cấu hình hiện tại vào danh sách bên dưới.");
            warmupToolTip.SetToolTip(btnStartWarmupQueue, "Chọn chạy dòng đã chọn hoặc cả bảng hàng đợi.");
            warmupToolTip.SetToolTip(btnPauseWarmupQueue, "Tạm dừng ngay job đang chạy. Bấm «Tiếp tục» để chạy lại — hàng đợi không tự chạy khi mở app.");
            warmupToolTip.SetToolTip(btnStopWarmupQueue, "Dừng hẳn job hoặc hàng đợi đang chạy.");
            warmupToolTip.SetToolTip(btnMoveQueueJobUp, "Di chuyển dòng đã chọn lên trên (ưu tiên chạy trước).");
            warmupToolTip.SetToolTip(btnMoveQueueJobDown, "Di chuyển dòng đã chọn xuống dưới.");
            warmupToolTip.SetToolTip(btnRemoveQueueJob, "Xóa một hoặc nhiều dòng đang chọn (dừng hàng đợi trước).");
            warmupToolTip.SetToolTip(chkEnableWarmupSchedule, "Bật để hẹn giờ khi thêm vào hàng đợi. Tắt = chạy khi bấm Chạy.");
            warmupToolTip.SetToolTip(dtpWarmupSchedule, "Thời điểm chạy (giờ máy). Đến giờ app tự bắt đầu hàng đợi nếu chưa chạy.");
            warmupToolTip.SetToolTip(numWatchMin, "Tỷ lệ giữ chân tối thiểu (% thời lượng video, ví dụ 80).");
            warmupToolTip.SetToolTip(numWatchMax, "Tỷ lệ giữ chân tối đa (% — trên 100 = xem lặp một phần).");
            warmupToolTip.SetToolTip(numLikeProbability, "Số video sẽ tim trong phiên = % × số video. Ví dụ 50% × 5 video → tim khoảng 2–3 video (slot ngẫu nhiên).");
            warmupToolTip.SetToolTip(numShareProbability, "Số video sẽ share trong phiên = % × số video. Ví dụ 20% × 5 video → share khoảng 1 video (slot ngẫu nhiên).");
            warmupToolTip.SetToolTip(chkAutoComment, "Bật để bot có thể bình luận AI (cần API key).");
            warmupToolTip.SetToolTip(numCommentProbability, "Số video sẽ comment trong phiên = % × số video. Ví dụ 50% × 5 video → comment khoảng 2–3 video (slot ngẫu nhiên).");
        }

        private static void AddWarmupConfigRow(TableLayoutPanel tbl, int row, string caption, Func<Control> createControl)
        {
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.LightGray,
                Margin = new Padding(0, 10, 12, 6),
                MinimumSize = new Size(118, 0)
            };
            var ctrl = createControl();
            ctrl.Margin = new Padding(0, 6, 0, 6);
            if (ctrl.Dock == DockStyle.None)
            {
                ctrl.Dock = DockStyle.Fill;
            }

            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(ctrl, 1, row);
        }

        private static Label CreateWarmupSectionLabel(string text, int topMargin = 2)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(158, 166, 182),
                Font = WarmupLabelFont,
                Margin = new Padding(2, topMargin, 0, 2),
                BackColor = WarmupPanelBack
            };
        }

        private void ApplyWarmupQueueToolbarState()
        {
            if (btnPauseWarmupQueue == null || btnStopWarmupQueue == null)
            {
                return;
            }

            var queueRunning = _isWarmupQueueRunning;
            var manualRunning = _warmupCancellation != null;
            var active = queueRunning || manualRunning;
            var idle = !active;

            btnPauseWarmupQueue.Visible = queueRunning;
            btnPauseWarmupQueue.Enabled = queueRunning;
            if (queueRunning && _isWarmupQueuePaused)
            {
                btnPauseWarmupQueue.Text = "Tiếp tục";
                SetWarmupJellyTint(btnPauseWarmupQueue, WarmupTintResume);
            }
            else
            {
                btnPauseWarmupQueue.Text = "Tạm dừng";
                SetWarmupJellyTint(btnPauseWarmupQueue, WarmupTintPause);
            }

            btnStopWarmupQueue.Visible = active;
            btnStopWarmupQueue.Enabled = active;
            btnStopWarmupQueue.Text = "Dừng hẳn";
            SetWarmupJellyTint(btnStopWarmupQueue, WarmupTintStop);

            if (btnStartWarmupQueue != null)
            {
                btnStartWarmupQueue.Enabled = idle;
            }

            if (btnQueueWarmup != null)
            {
                btnQueueWarmup.Enabled = idle;
            }

            if (btnMoveQueueJobUp != null)
            {
                btnMoveQueueJobUp.Enabled = idle;
            }

            if (btnMoveQueueJobDown != null)
            {
                btnMoveQueueJobDown.Enabled = idle;
            }

            ResizeAppJellyButton(btnPauseWarmupQueue, WarmupToolbarRowHeight);
            ResizeAppJellyButton(btnStopWarmupQueue, WarmupToolbarRowHeight);
        }

        private static void SetWarmupJellyTint(Button button, Color tint)
        {
            if (button is JellyButton jelly)
            {
                jelly.JellyTint = tint;
                jelly.Invalidate();
            }
        }

        private static JellyButton CreateWarmupActionButton(string name, string text, Color tint)
        {
            return CreateAppJellyButton(name, text, tint, heightOverride: WarmupToolbarRowHeight);
        }

        private static FlowLayoutPanel CreateWarmupToolbarFlow(string name, params Control[] buttons)
        {
            var flp = new FlowLayoutPanel
            {
                Name = name,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                // false: PreferredHeight = 1 hàng nút — WrapContents=true dễ tính cao theo
                // chiều hẹp rồi để khoảng trống lớn dưới nút khi panel rộng.
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = WarmupPanelBack
            };
            flp.Controls.AddRange(buttons);
            return flp;
        }
    }
}
