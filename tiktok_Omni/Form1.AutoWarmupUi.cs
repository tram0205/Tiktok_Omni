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
        private static readonly Font WarmupJellyButtonFont = new Font("Segoe UI", 10.25F, FontStyle.Bold);
        private static readonly Font WarmupFieldFont = AppInputFont;
        private static readonly Font WarmupLabelFont = AppInputFont;
        private const int WarmupFieldHeight = AppInputMinHeight;
        private const int WarmupToolbarButtonHeight = 44;
        private const int WarmupToolbarRowHeight = 48;
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
            tabAutoWarmup.AutoScroll = false;
            tabAutoWarmup.Padding = new Padding(8);

            var tblWarmupMain = new TableLayoutPanel
            {
                Name = "tblWarmupMain",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = WarmupPanelBack
            };
            tblWarmupMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblWarmupMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblWarmupMain.Controls.Add(BuildWarmupConfigPanel(), 0, 0);
            tblWarmupMain.Controls.Add(BuildWarmupPrimaryActionsPanel(), 0, 1);
            var pnlWarmupGrid = BuildWarmupGridPanel();
            pnlWarmupGrid.Margin = new Padding(0, WarmupSectionGap, 0, 0);
            tblWarmupMain.Controls.Add(pnlWarmupGrid, 0, 2);
            tblWarmupMain.Controls.Add(BuildWarmupBottomActionsPanel(), 0, 3);

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
                AutoSize = false,
                Padding = new Padding(4, 4, 4, 8),
                BackColor = WarmupPanelBack
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = WarmupPanelBack
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
                Value = 20,
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
                Minimum = 3,
                Maximum = 600,
                Value = 7,
                Font = WarmupFieldFont,
                Width = 64,
                MinimumSize = new Size(64, WarmupFieldHeight),
                BackColor = WarmupFieldBack,
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 4, 0)
            };
            numWatchMax = new NumericUpDown
            {
                Name = "numWatchMax",
                Minimum = 3,
                Maximum = 600,
                Value = 18,
                Font = WarmupFieldFont,
                Width = 64,
                MinimumSize = new Size(64, WarmupFieldHeight),
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
            tblTopRow.Controls.Add(CreateWarmupInlineLabel("Tổng xem (giây)"), 6, 0);
            tblTopRow.Controls.Add(flpWatchInline, 7, 0);

            tbl.Controls.Add(tblTopRow, 0, 0);

            chkAutoComment = new CheckBox
            {
                Name = "chkAutoComment",
                Text = "Bật bình luận AI tự sinh",
                AutoSize = true,
                Font = WarmupFieldFont,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 2, 16, 2)
            };

            var flpRunMode = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = WarmupPanelBack
            };
            flpRunMode.Controls.Add(chkAutoComment);
            flpRunMode.Controls.Add(new Label { Text = "Chế độ chạy:", AutoSize = true, Font = WarmupLabelFont, ForeColor = Color.Gainsboro, Margin = new Padding(0, 4, 8, 4) });
            rbDryRun = new RadioButton
            {
                Name = "rbDryRun",
                Text = "Dry Run (mô phỏng an toàn)",
                AutoSize = true,
                Font = WarmupFieldFont,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 4, 16, 4)
            };
            rbLiveRun = new RadioButton
            {
                Name = "rbLiveRun",
                Text = "Live Run (hành động thật)",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 4, 0, 4)
            };
            flpRunMode.Controls.Add(rbDryRun);
            flpRunMode.Controls.Add(rbLiveRun);
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.Controls.Add(flpRunMode, 0, 1);

            var flpSchedule = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = WarmupPanelBack
            };
            chkEnableWarmupSchedule = new CheckBox
            {
                Name = "chkEnableWarmupSchedule",
                Text = "Lập lịch warm-up",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 6, 12, 4)
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
                Width = 180,
                MinimumSize = new Size(180, WarmupFieldHeight),
                Margin = new Padding(0, 4, 0, 4)
            };
            flpSchedule.Controls.Add(chkEnableWarmupSchedule);
            flpSchedule.Controls.Add(dtpWarmupSchedule);
            chkAutoResumeQueueOnStartup = new CheckBox
            {
                Name = "chkAutoResumeQueueOnStartup",
                Text = "T\u1ef1 kh\u00f4i ph\u1ee5c h\u00e0ng \u0111\u1ee3i khi m\u1edf app",
                AutoSize = true,
                Font = WarmupFieldFont,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(12, 6, 0, 4)
            };
            flpSchedule.Controls.Add(chkAutoResumeQueueOnStartup);
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.Controls.Add(flpSchedule, 0, 2);

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
                Padding = new Padding(0, WarmupSectionGap, 0, WarmupSectionGap),
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
            return pnl;
        }

        private Control BuildWarmupPrimaryActionsPanel()
        {
            var tblPrimary = new TableLayoutPanel
            {
                Name = "tblWarmupPrimaryActions",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = WarmupPanelBack,
                Padding = new Padding(2, 2, 2, 2),
                Margin = Padding.Empty
            };
            tblPrimary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblPrimary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblPrimary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblPrimary.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblQueue = CreateWarmupSectionLabel("Thêm profile vào hàng đợi, chạy hàng đợi hoặc chạy ngay một lần");
            btnQueueWarmup = CreateWarmupActionButton("btnQueueWarmup", "Thêm vào hàng đợi", WarmupTintAdd);
            btnQueueWarmup.Click += btnQueueWarmup_Click;
            btnStartWarmupQueue = CreateWarmupActionButton("btnStartWarmupQueue", "Chạy hàng đợi", WarmupTintRun);
            btnStartWarmupQueue.Click += btnStartWarmupQueue_Click;
            btnStartWarmup = CreateWarmupActionButton("btnStartWarmup", "Chạy ngay", WarmupTintManual);
            btnStartWarmup.Click += btnStartWarmup_Click;
            btnPauseWarmupQueue = CreateWarmupActionButton("btnPauseWarmupQueue", "Tạm dừng", WarmupTintPause);
            btnPauseWarmupQueue.Enabled = false;
            btnPauseWarmupQueue.Click += btnPauseWarmupQueue_Click;
            btnStopWarmupQueue = CreateWarmupActionButton("btnStopWarmupQueue", "Dừng", WarmupTintStop);
            btnStopWarmupQueue.Enabled = false;
            btnStopWarmupQueue.Click += WarmupUnifiedStop_Click;
            btnRemoveQueueJob = CreateWarmupActionButton("btnRemoveQueueJob", "Xóa dòng", WarmupTintNeutral);
            btnRemoveQueueJob.Click += btnRemoveQueueJob_Click;

            var flpQueueToolbar = CreateWarmupToolbarFlow(
                "flpWarmupQueueToolbar",
                btnQueueWarmup,
                btnStartWarmupQueue,
                btnStartWarmup,
                btnPauseWarmupQueue,
                btnStopWarmupQueue,
                btnRemoveQueueJob);

            tblPrimary.Controls.Add(lblQueue, 0, 0);
            tblPrimary.Controls.Add(flpQueueToolbar, 0, 1);
            tblPrimary.Controls.Add(BuildWarmupMonitorStrip(), 0, 2);
            return tblPrimary;
        }

        private Control BuildWarmupBottomActionsPanel()
        {
            var tblBottom = new TableLayoutPanel
            {
                Name = "tblWarmupBottomActions",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = WarmupPanelBack,
                Padding = new Padding(2, 4, 2, 4),
                Margin = Padding.Empty
            };
            tblBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblBottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var flpStatsFilter = new FlowLayoutPanel
            {
                Name = "flpWarmupStatsFilter",
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 0),
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
                Width = 132,
                MinimumSize = new Size(132, WarmupFieldHeight),
                Margin = new Padding(5, 5, 5, 5)
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
                Width = 124,
                MinimumSize = new Size(124, WarmupFieldHeight),
                Margin = new Padding(5, 5, 5, 5)
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
                Width = 124,
                MinimumSize = new Size(124, WarmupFieldHeight),
                Margin = new Padding(5, 5, 5, 5)
            };
            dtQueueStatsTo.ValueChanged += (_, __) => RefreshQueueStatsSummary();

            flpStatsFilter.Controls.AddRange(new Control[]
            {
                cbQueueStatsRange, dtQueueStatsFrom, dtQueueStatsTo
            });

            tblBottom.Controls.Add(flpStatsFilter, 0, 0);
            return tblBottom;
        }

        private void WarmupUnifiedStop_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                btnStopWarmupQueue_Click(sender, e);
                return;
            }

            if (_warmupCancellation != null)
            {
                btnStopWarmup_Click(sender, e);
            }
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
                MultiSelect = false,
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
                Font = AppGridHeaderFont,
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 8, 6, 8),
                WrapMode = DataGridViewTriState.False
            };
            dgvWarmupQueue.ColumnHeadersHeight = AppGridHeaderHeight;
            dgvWarmupQueue.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvWarmupQueue.RowTemplate.Height = WarmupFieldHeight + 4;
            dgvWarmupQueue.EnableHeadersVisualStyles = false;
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Profile", HeaderText = "Profile", FillWeight = 11, MinimumWidth = 84, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Keywords", HeaderText = "Từ khóa", FillWeight = 19, MinimumWidth = 102, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Videos", HeaderText = "Số video", FillWeight = 11, MinimumWidth = 90, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WatchRange", HeaderText = "Tổng xem (s)", FillWeight = 21, MinimumWidth = 120, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusDisplay", HeaderText = "Trạng thái", FillWeight = 11, MinimumWidth = 102, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ScheduledAtLabel", HeaderText = "Lịch chạy", FillWeight = 12, MinimumWidth = 108, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MaxRetries", HeaderText = "Thử lại tối đa", FillWeight = 13, MinimumWidth = 148 });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RetryLabel", HeaderText = "Lần thử", FillWeight = 9, MinimumWidth = 78, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAtLabel", HeaderText = "Tạo lúc", FillWeight = 13, MinimumWidth = 126, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NextRetryEtaLabel", HeaderText = "Thử lại lúc", FillWeight = 16, MinimumWidth = 168, ReadOnly = true });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastError", HeaderText = "Lỗi cuối", FillWeight = 15, MinimumWidth = 118, ReadOnly = true });
            dgvWarmupQueue.CellEndEdit += dgvWarmupQueue_CellEndEdit;
        }

        private void WireWarmupTooltips()
        {
            var warmupToolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ReshowDelay = 150, ShowAlways = true };
            warmupToolTip.SetToolTip(btnQueueWarmup, "Thêm cấu hình hiện tại vào danh sách bên dưới.");
            warmupToolTip.SetToolTip(btnStartWarmupQueue, "Chạy toàn bộ hàng đợi theo thứ tự.");
            warmupToolTip.SetToolTip(btnStartWarmup, "Chạy một lần với cấu hình trên. Có phiên dở dang thì đổi thành Tiếp tục.");
            warmupToolTip.SetToolTip(btnPauseWarmupQueue, "Tạm dừng hoặc tiếp tục hàng đợi sau khi xong job hiện tại.");
            warmupToolTip.SetToolTip(btnStopWarmupQueue, "Dừng hàng đợi hoặc phiên chạy ngay.");
            warmupToolTip.SetToolTip(btnRemoveQueueJob, "Xóa dòng đang chọn (dừng hàng đợi trước).");
            warmupToolTip.SetToolTip(chkEnableWarmupSchedule, "Bật để hẹn giờ khi thêm vào hàng đợi. Tắt = chạy khi bấm Chạy hàng đợi.");
            warmupToolTip.SetToolTip(dtpWarmupSchedule, "Thời điểm chạy (giờ máy). Đến giờ app tự bắt đầu hàng đợi nếu chưa chạy.");
            warmupToolTip.SetToolTip(numWatchMin, "Tổng thời gian xem tối thiểu cho cả phiên (cộng tất cả video).");
            warmupToolTip.SetToolTip(numWatchMax, "Tổng thời gian xem tối đa cho cả phiên (cộng tất cả video).");
            if (chkAutoResumeQueueOnStartup != null)
            {
                warmupToolTip.SetToolTip(chkAutoResumeQueueOnStartup, "Nếu lần trước đóng app khi hàng đợi đang tạm dừng, tự resume khi mở lại.");
            }
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
            if (btnPauseWarmupQueue == null)
            {
                return;
            }

            var running = _isWarmupQueueRunning;
            btnPauseWarmupQueue.Enabled = running;
            if (!running)
            {
                btnPauseWarmupQueue.Text = "Tạm dừng";
                SetWarmupJellyTint(btnPauseWarmupQueue, WarmupTintPause);
            }
            else if (_isWarmupQueuePaused)
            {
                btnPauseWarmupQueue.Text = "Tiếp tục";
                SetWarmupJellyTint(btnPauseWarmupQueue, WarmupTintResume);
            }
            else
            {
                btnPauseWarmupQueue.Text = "Tạm dừng";
                SetWarmupJellyTint(btnPauseWarmupQueue, WarmupTintPause);
            }

            if (btnStopWarmupQueue != null)
            {
                btnStopWarmupQueue.Enabled = running || _warmupCancellation != null;
            }
        }

        internal static void ApplyWarmupStartButtonResumeState(Button startButton, bool canResume)
        {
            if (startButton == null || startButton.IsDisposed)
            {
                return;
            }

            startButton.Text = canResume ? "Tiếp tục" : "Chạy ngay";
            SetWarmupJellyTint(startButton, canResume ? WarmupTintResume : WarmupTintManual);
            var width = MeasureWarmupButtonTextWidth(startButton.Text, WarmupJellyButtonFont) + 22;
            startButton.Width = width;
            startButton.MinimumSize = new Size(width, WarmupToolbarRowHeight);
            startButton.MaximumSize = new Size(width, WarmupToolbarRowHeight);
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
            const int horizontalPad = 22;
            var width = MeasureWarmupButtonTextWidth(text, WarmupJellyButtonFont) + horizontalPad;

            return new JellyButton
            {
                Name = name,
                Text = text,
                Font = WarmupJellyButtonFont,
                JellyTint = tint,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Width = width,
                Height = WarmupToolbarRowHeight,
                MinimumSize = new Size(width, WarmupToolbarRowHeight),
                MaximumSize = new Size(width, WarmupToolbarRowHeight),
                Margin = new Padding(0, 0, 6, 0)
            };
        }

        private static int MeasureWarmupButtonTextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, WarmupToolbarButtonHeight),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding).Width;
        }

        private static FlowLayoutPanel CreateWarmupToolbarFlow(string name, params Control[] buttons)
        {
            var flp = new FlowLayoutPanel
            {
                Name = name,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                WrapContents = true,
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
