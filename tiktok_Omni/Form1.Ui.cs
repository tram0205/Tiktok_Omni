using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1 : Form
    {
        private void BuildAutoWarmupUi()
        {
            var lblKeywords = new Label
            {
                Text = "Từ khóa ngách",
                AutoSize = true,
                Location = new Point(24, 30)
            };

            txtKeywords = new TextBox
            {
                Name = "txtKeywords",
                Location = new Point(24, 55),
                Size = new Size(350, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblRunningProfile = new Label
            {
                Text = "Profile chạy",
                AutoSize = true,
                Location = new Point(395, 30)
            };

            cbRunningProfile = new ComboBox
            {
                Name = "cbRunningProfile",
                Location = new Point(395, 55),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbRunningProfile.Items.Add("default");
            cbRunningProfile.SelectedIndex = 0;

            var lblVideoCount = new Label
            {
                Text = "Số video tương tác",
                AutoSize = true,
                Location = new Point(24, 100)
            };

            numVideoCount = new NumericUpDown
            {
                Name = "numVideoCount",
                Location = new Point(24, 125),
                Size = new Size(120, 30),
                Minimum = 1,
                Maximum = 1000,
                Value = 20,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblWatchMin = new Label
            {
                Text = "Xem tối thiểu (phút)",
                AutoSize = true,
                Location = new Point(180, 100)
            };

            numWatchMin = new NumericUpDown
            {
                Name = "numWatchMin",
                Location = new Point(180, 125),
                Size = new Size(100, 30),
                DecimalPlaces = 2,
                Minimum = 0.05m,
                Maximum = 180m,
                Increment = 0.05m,
                Value = 0.12m,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblWatchMax = new Label
            {
                Text = "Xem tối đa (phút)",
                AutoSize = true,
                Location = new Point(395, 100)
            };

            numWatchMax = new NumericUpDown
            {
                Name = "numWatchMax",
                Location = new Point(395, 125),
                Size = new Size(100, 30),
                DecimalPlaces = 2,
                Minimum = 0.05m,
                Maximum = 180m,
                Increment = 0.05m,
                Value = 0.30m,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            chkAutoComment = new CheckBox
            {
                Name = "chkAutoComment",
                Text = "Bật bình luận AI tự sinh",
                AutoSize = true,
                Location = new Point(24, 175),
                Checked = true
            };

            var lblRunMode = new Label
            {
                Text = "Chế độ chạy",
                AutoSize = true,
                Location = new Point(24, 205)
            };

            rbDryRun = new RadioButton
            {
                Name = "rbDryRun",
                Text = "Dry Run (mô phỏng an toàn)",
                AutoSize = true,
                Location = new Point(24, 228),
                Checked = true
            };

            rbLiveRun = new RadioButton
            {
                Name = "rbLiveRun",
                Text = "Live Run (hành động thật)",
                AutoSize = true,
                Location = new Point(24, 252)
            };

            // Row 1: single-job control — start → queue → resume → stop (left to right).
            btnStartWarmup = new Button
            {
                Name = "btnStartWarmup",
                Text = "Bắt đầu làm ấm",
                Location = new Point(24, 286),
                Size = new Size(170, 38),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnStartWarmup.FlatAppearance.BorderSize = 0;
            btnStartWarmup.Click += btnStartWarmup_Click;

            btnQueueWarmup = new Button
            {
                Name = "btnQueueWarmup",
                Text = "Thêm vào hàng đợi",
                Location = new Point(204, 286),
                Size = new Size(132, 38),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnQueueWarmup.FlatAppearance.BorderSize = 0;
            btnQueueWarmup.Click += btnQueueWarmup_Click;

            btnResumeWarmup = new Button
            {
                Name = "btnResumeWarmup",
                Text = "Tiếp tục",
                Location = new Point(346, 286),
                Size = new Size(110, 38),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Enabled = false
            };
            btnResumeWarmup.FlatAppearance.BorderSize = 0;
            btnResumeWarmup.Click += btnResumeWarmup_Click;

            btnStopWarmup = new Button
            {
                Name = "btnStopWarmup",
                Text = "Dừng",
                Location = new Point(464, 286),
                Size = new Size(110, 38),
                BackColor = Color.FromArgb(180, 70, 70),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Enabled = false
            };
            btnStopWarmup.FlatAppearance.BorderSize = 0;
            btnStopWarmup.Click += btnStopWarmup_Click;

            // Row 2: queue runner controls.
            btnStartWarmupQueue = new Button
            {
                Name = "btnStartWarmupQueue",
                Text = "Chạy hàng đợi",
                Location = new Point(24, 334),
                Size = new Size(118, 34),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnStartWarmupQueue.FlatAppearance.BorderSize = 0;
            btnStartWarmupQueue.Click += btnStartWarmupQueue_Click;

            btnStopWarmupQueue = new Button
            {
                Name = "btnStopWarmupQueue",
                Text = "Dừng hàng đợi",
                Location = new Point(150, 334),
                Size = new Size(118, 34),
                BackColor = Color.FromArgb(180, 70, 70),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Enabled = false
            };
            btnStopWarmupQueue.FlatAppearance.BorderSize = 0;
            btnStopWarmupQueue.Click += btnStopWarmupQueue_Click;

            btnPauseWarmupQueue = new Button
            {
                Name = "btnPauseWarmupQueue",
                Text = "Tạm dừng hàng đợi",
                Location = new Point(276, 334),
                Size = new Size(128, 34),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false
            };
            btnPauseWarmupQueue.FlatAppearance.BorderSize = 0;
            btnPauseWarmupQueue.Click += btnPauseWarmupQueue_Click;

            btnResumeWarmupQueue = new Button
            {
                Name = "btnResumeWarmupQueue",
                Text = "Tiếp tục hàng đợi",
                Location = new Point(412, 334),
                Size = new Size(128, 34),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false
            };
            btnResumeWarmupQueue.FlatAppearance.BorderSize = 0;
            btnResumeWarmupQueue.Click += btnResumeWarmupQueue_Click;

            btnPauseNowWarmupQueue = new Button
            {
                Name = "btnPauseNowWarmupQueue",
                Text = "Tạm dừng ngay",
                Location = new Point(548, 334),
                Size = new Size(118, 34),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false
            };
            btnPauseNowWarmupQueue.FlatAppearance.BorderSize = 0;
            btnPauseNowWarmupQueue.Click += btnPauseNowWarmupQueue_Click;

            // Stub UI only — full scheduler needs Task Scheduler / single-instance guard / queue restore; not shipped half-baked.
            chkScheduleWarmupStub = new CheckBox
            {
                Name = "chkScheduleWarmupStub",
                Text = "Lập lịch warm-up tự động (chưa hỗ trợ)",
                Location = new Point(674, 334),
                Size = new Size(360, 28),
                Enabled = false,
                ForeColor = Color.FromArgb(160, 160, 170),
                AutoSize = false
            };

            lblWarmupQueueStatus = new Label
            {
                Name = "lblWarmupQueueStatus",
                Text = "Hàng đợi: 0 mục",
                AutoSize = true,
                Location = new Point(680, 398),
                ForeColor = Color.FromArgb(180, 180, 180)
            };

            btnRemoveQueueJob = new Button
            {
                Name = "btnRemoveQueueJob",
                Text = "Xóa mục chọn",
                Location = new Point(24, 604),
                Size = new Size(118, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnRemoveQueueJob.FlatAppearance.BorderSize = 0;
            btnRemoveQueueJob.Click += btnRemoveQueueJob_Click;

            btnClearWarmupQueue = new Button
            {
                Name = "btnClearWarmupQueue",
                Text = "Xóa toàn bộ hàng đợi",
                Location = new Point(148, 604),
                Size = new Size(154, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnClearWarmupQueue.FlatAppearance.BorderSize = 0;
            btnClearWarmupQueue.Click += btnClearWarmupQueue_Click;

            // Short labels so text is not clipped to identical "Di chuyển…" at narrow widths.
            btnMoveQueueJobUp = new Button
            {
                Name = "btnMoveQueueJobUp",
                Text = "↑ Lên",
                Location = new Point(308, 604),
                Size = new Size(76, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnMoveQueueJobUp.FlatAppearance.BorderSize = 0;
            btnMoveQueueJobUp.Click += btnMoveQueueJobUp_Click;

            btnMoveQueueJobDown = new Button
            {
                Name = "btnMoveQueueJobDown",
                Text = "↓ Xuống",
                Location = new Point(388, 604),
                Size = new Size(88, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnMoveQueueJobDown.FlatAppearance.BorderSize = 0;
            btnMoveQueueJobDown.Click += btnMoveQueueJobDown_Click;

            btnOpenQueueHistory = new Button
            {
                Name = "btnOpenQueueHistory",
                Text = "Lịch sử hàng đợi",
                Location = new Point(482, 604),
                Size = new Size(118, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnOpenQueueHistory.FlatAppearance.BorderSize = 0;
            btnOpenQueueHistory.Click += btnOpenQueueHistory_Click;

            btnOpenQueueTrend = new Button
            {
                Name = "btnOpenQueueTrend",
                Text = "Xu hướng hàng đợi",
                Location = new Point(606, 604),
                Size = new Size(118, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnOpenQueueTrend.FlatAppearance.BorderSize = 0;
            btnOpenQueueTrend.Click += btnOpenQueueTrend_Click;

            btnOpenApprovalQueue = new Button
            {
                Name = "btnOpenApprovalQueue",
                Text = "Hàng đợi duyệt",
                Location = new Point(730, 604),
                Size = new Size(118, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnOpenApprovalQueue.FlatAppearance.BorderSize = 0;
            btnOpenApprovalQueue.Click += btnOpenApprovalQueue_Click;

            btnRefreshQueueStats = new Button
            {
                Name = "btnRefreshQueueStats",
                Text = "Làm mới thống kê",
                Location = new Point(854, 604),
                Size = new Size(154, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnRefreshQueueStats.FlatAppearance.BorderSize = 0;
            btnRefreshQueueStats.Click += btnRefreshQueueStats_Click;

            cbQueueStatsRange = new ComboBox
            {
                Name = "cbQueueStatsRange",
                Location = new Point(24, 642),
                Size = new Size(130, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbQueueStatsRange.Items.AddRange(new object[] { "Hôm nay", "7 ngày", "30 ngày", "Tùy chỉnh" });
            cbQueueStatsRange.SelectedIndex = 0;
            cbQueueStatsRange.SelectedIndexChanged += (s, e) =>
            {
                var isCustom = string.Equals(cbQueueStatsRange.SelectedItem?.ToString(), "Tùy chỉnh", StringComparison.OrdinalIgnoreCase);
                dtQueueStatsFrom.Enabled = isCustom;
                dtQueueStatsTo.Enabled = isCustom;
                RefreshQueueStatsSummary();
            };

            dtQueueStatsFrom = new DateTimePicker
            {
                Name = "dtQueueStatsFrom",
                Location = new Point(162, 642),
                Size = new Size(130, 24),
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Value = DateTime.Today.AddDays(-7)
            };
            dtQueueStatsFrom.ValueChanged += (s, e) => RefreshQueueStatsSummary();

            dtQueueStatsTo = new DateTimePicker
            {
                Name = "dtQueueStatsTo",
                Location = new Point(300, 642),
                Size = new Size(130, 24),
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Value = DateTime.Today
            };
            dtQueueStatsTo.ValueChanged += (s, e) => RefreshQueueStatsSummary();

            lblQueueStatsSummary = new Label
            {
                Name = "lblQueueStatsSummary",
                Text = "Hôm nay: 0 mục",
                AutoSize = true,
                Location = new Point(24, 678),
                ForeColor = Color.FromArgb(180, 180, 180)
            };
            lblQueueStatsSuccessRate = new Label
            {
                Name = "lblQueueStatsSuccessRate",
                Text = "Tỉ lệ thành công: -",
                AutoSize = true,
                Location = new Point(220, 678),
                ForeColor = Color.FromArgb(180, 180, 180)
            };
            lblQueueStatsAvgRetry = new Label
            {
                Name = "lblQueueStatsAvgRetry",
                Text = "Số lần thử TB: -",
                AutoSize = true,
                Location = new Point(420, 678),
                ForeColor = Color.FromArgb(180, 180, 180)
            };
            lblQueueStatsTopFailedProfile = new Label
            {
                Name = "lblQueueStatsTopFailedProfile",
                Text = "Profile lỗi nhiều nhất: -",
                AutoSize = true,
                Location = new Point(620, 678),
                ForeColor = Color.FromArgb(180, 180, 180)
            };

            _warmupQueueBindingList = new BindingList<WarmupQueueUiItem>();
            dgvWarmupQueue = new DataGridView
            {
                Name = "dgvWarmupQueue",
                Location = new Point(24, 460),
                Size = new Size(1020, 132),
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
                SelectionForeColor = Color.WhiteSmoke
            };
            dgvWarmupQueue.EnableHeadersVisualStyles = false;
            // FillWeight + MinimumWidth sized for Vietnamese headers (LocalizeDisplayText).
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Profile",
                HeaderText = "Profile",
                FillWeight = 11,
                MinimumWidth = 84,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Keywords",
                HeaderText = "Keywords",
                FillWeight = 19,
                MinimumWidth = 102,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Videos",
                HeaderText = "Videos",
                FillWeight = 11,
                MinimumWidth = 90,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "WatchRange",
                HeaderText = "Xem (phút)",
                FillWeight = 21,
                MinimumWidth = 178,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText = "Status",
                FillWeight = 11,
                MinimumWidth = 102,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "MaxRetries",
                HeaderText = "Max Retries",
                FillWeight = 13,
                MinimumWidth = 148
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RetryLabel",
                HeaderText = "Retry",
                FillWeight = 9,
                MinimumWidth = 78,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedAtLabel",
                HeaderText = "Created",
                FillWeight = 13,
                MinimumWidth = 126,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "NextRetryEtaLabel",
                HeaderText = "Next Retry ETA",
                FillWeight = 16,
                MinimumWidth = 168,
                ReadOnly = true
            });
            dgvWarmupQueue.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LastError",
                HeaderText = "Last Error",
                FillWeight = 15,
                MinimumWidth = 118,
                ReadOnly = true
            });
            dgvWarmupQueue.CellEndEdit += dgvWarmupQueue_CellEndEdit;

            pbWarmupProgress = new ProgressBar
            {
                Name = "pbWarmupProgress",
                Location = new Point(24, 428),
                Size = new Size(852, 22),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            lblWarmupProgress = new Label
            {
                Name = "lblWarmupProgress",
                Text = "Tiến độ: 0/0 (0%)",
                AutoSize = true,
                Location = new Point(888, 428)
            };

            btnOpenLogsFolder = new Button
            {
                Name = "btnOpenLogsFolder",
                Text = "Open Logs Folder",
                Location = new Point(280, 706),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnOpenLogsFolder.FlatAppearance.BorderSize = 0;
            btnOpenLogsFolder.Click += btnOpenLogsFolder_Click;

            btnExportCurrentLogs = new Button
            {
                Name = "btnExportCurrentLogs",
                Text = "Export Current Logs",
                Location = new Point(132, 706),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnExportCurrentLogs.FlatAppearance.BorderSize = 0;
            btnExportCurrentLogs.Click += btnExportCurrentLogs_Click;

            btnClearLogs = new Button
            {
                Name = "btnClearLogs",
                Text = "Clear Logs",
                Location = new Point(24, 706),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnClearLogs.FlatAppearance.BorderSize = 0;
            btnClearLogs.Click += btnClearLogs_Click;

            rtbLogs = new RichTextBox
            {
                Name = "rtbLogs",
                Location = new Point(24, 744),
                Size = new Size(1020, 165),
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle
            };

            var warmupToolTip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 300,
                ReshowDelay = 150,
                ShowAlways = true
            };
            warmupToolTip.SetToolTip(btnQueueWarmup, "Thêm profile hiện tại vào danh sách chạy tự động.");
            warmupToolTip.SetToolTip(btnStartWarmupQueue, "Bắt đầu xử lý toàn bộ hàng đợi theo thứ tự.");
            warmupToolTip.SetToolTip(btnPauseWarmupQueue, "Tạm dừng hàng đợi sau khi hoàn thành tác vụ hiện tại.");
            warmupToolTip.SetToolTip(btnPauseNowWarmupQueue, "Tạm dừng ngay lập tức để xử lý thủ công.");
            warmupToolTip.SetToolTip(btnOpenQueueHistory, "Xem lịch sử đã chạy và kết quả từng profile.");
            warmupToolTip.SetToolTip(btnOpenQueueTrend, "Mở biểu đồ xu hướng thành công/thất bại gần đây.");
            warmupToolTip.SetToolTip(btnOpenApprovalQueue, "Mở danh sách chờ duyệt trước khi chạy thật.");
            warmupToolTip.SetToolTip(btnMoveQueueJobUp, "Di chuyển một dòng đã chọn lên trên trong hàng đợi.");
            warmupToolTip.SetToolTip(btnMoveQueueJobDown, "Di chuyển một dòng đã chọn xuống dưới trong hàng đợi.");
            warmupToolTip.SetToolTip(btnRefreshQueueStats, "Tải lại thống kê hàng đợi (theo khoảng thời gian bên dưới: hôm nay / 7 ngày / …).");
            warmupToolTip.SetToolTip(chkScheduleWarmupStub,
                "Dự kiến: tích hợp Windows Task Scheduler hoặc bộ hẹn giờ nội bộ + khôi phục hàng đợi khi mở app. " +
                "Cần mutex single-instance và chính sách khi máy sleep — chưa ship để tránh warm-up chạy nền sai.");

            tabAutoWarmup.Controls.Add(lblKeywords);
            tabAutoWarmup.Controls.Add(txtKeywords);
            tabAutoWarmup.Controls.Add(lblRunningProfile);
            tabAutoWarmup.Controls.Add(cbRunningProfile);
            tabAutoWarmup.Controls.Add(lblVideoCount);
            tabAutoWarmup.Controls.Add(numVideoCount);
            tabAutoWarmup.Controls.Add(lblWatchMin);
            tabAutoWarmup.Controls.Add(numWatchMin);
            tabAutoWarmup.Controls.Add(lblWatchMax);
            tabAutoWarmup.Controls.Add(numWatchMax);
            tabAutoWarmup.Controls.Add(chkAutoComment);
            tabAutoWarmup.Controls.Add(lblRunMode);
            tabAutoWarmup.Controls.Add(rbDryRun);
            tabAutoWarmup.Controls.Add(rbLiveRun);
            tabAutoWarmup.Controls.Add(btnStartWarmup);
            tabAutoWarmup.Controls.Add(btnQueueWarmup);
            tabAutoWarmup.Controls.Add(btnResumeWarmup);
            tabAutoWarmup.Controls.Add(btnStopWarmup);
            tabAutoWarmup.Controls.Add(btnStartWarmupQueue);
            tabAutoWarmup.Controls.Add(btnStopWarmupQueue);
            tabAutoWarmup.Controls.Add(btnPauseWarmupQueue);
            tabAutoWarmup.Controls.Add(btnResumeWarmupQueue);
            tabAutoWarmup.Controls.Add(btnPauseNowWarmupQueue);
            tabAutoWarmup.Controls.Add(chkScheduleWarmupStub);
            tabAutoWarmup.Controls.Add(lblWarmupQueueStatus);
            tabAutoWarmup.Controls.Add(btnRemoveQueueJob);
            tabAutoWarmup.Controls.Add(btnClearWarmupQueue);
            tabAutoWarmup.Controls.Add(btnMoveQueueJobUp);
            tabAutoWarmup.Controls.Add(btnMoveQueueJobDown);
            tabAutoWarmup.Controls.Add(btnOpenQueueHistory);
            tabAutoWarmup.Controls.Add(btnOpenQueueTrend);
            tabAutoWarmup.Controls.Add(btnOpenApprovalQueue);
            tabAutoWarmup.Controls.Add(btnRefreshQueueStats);
            tabAutoWarmup.Controls.Add(cbQueueStatsRange);
            tabAutoWarmup.Controls.Add(dtQueueStatsFrom);
            tabAutoWarmup.Controls.Add(dtQueueStatsTo);
            tabAutoWarmup.Controls.Add(lblQueueStatsSummary);
            tabAutoWarmup.Controls.Add(lblQueueStatsSuccessRate);
            tabAutoWarmup.Controls.Add(lblQueueStatsAvgRetry);
            tabAutoWarmup.Controls.Add(lblQueueStatsTopFailedProfile);
            tabAutoWarmup.Controls.Add(dgvWarmupQueue);
            tabAutoWarmup.Controls.Add(pbWarmupProgress);
            tabAutoWarmup.Controls.Add(lblWarmupProgress);
            tabAutoWarmup.Controls.Add(btnClearLogs);
            tabAutoWarmup.Controls.Add(btnExportCurrentLogs);
            tabAutoWarmup.Controls.Add(btnOpenLogsFolder);
            tabAutoWarmup.Controls.Add(rtbLogs);
        }

        private void BuildAffiliateHunterUi()
        {
            var lblKeywords = new Label
            {
                Text = "Keywords",
                AutoSize = true,
                Location = new Point(24, 30)
            };

            txtAffiliateKeywords = new TextBox
            {
                Name = "txtAffiliateKeywords",
                Location = new Point(24, 55),
                Size = new Size(350, 64),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.FromArgb(120, 125, 140),
                Text = AffiliateKeywordsPlaceholder
            };
            _affiliateKeywordsPlaceholderActive = true;
            txtAffiliateKeywords.Enter += TxtAffiliateKeywords_Enter;
            txtAffiliateKeywords.Leave += TxtAffiliateKeywords_Leave;
            txtAffiliateKeywords.TextChanged += (_, __) => RefreshAffiliateDownloadFolderHint();

            var lblAffiliateSearchMode = new Label
            {
                Text = "Nguồn quét",
                AutoSize = true,
                Location = new Point(396, 30),
                ForeColor = Color.Gainsboro
            };

            cbAffiliateSearchMode = new ComboBox
            {
                Name = "cbAffiliateSearchMode",
                Location = new Point(396, 55),
                Size = new Size(320, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            cbAffiliateSearchMode.Items.Add("Săn Sản phẩm qua Video (Khuyên dùng)");
            cbAffiliateSearchMode.Items.Add("Săn trực tiếp TikTok Shop (Dễ bị chặn)");
            cbAffiliateSearchMode.SelectedIndex = 0;

            var lblMaxResults = new Label
            {
                Text = "Max Results",
                AutoSize = true,
                Location = new Point(24, 128)
            };

            numAffiliateMaxResults = new NumericUpDown
            {
                Name = "numAffiliateMaxResults",
                Location = new Point(24, 153),
                Size = new Size(120, 30),
                Minimum = 1,
                Maximum = 500,
                Value = 30,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblAffiliateMinSafety = new Label
            {
                Text = "Min engagement score",
                AutoSize = true,
                Location = new Point(200, 128)
            };

            numAffiliateMinSafety = new NumericUpDown
            {
                Name = "numAffiliateMinSafety",
                Location = new Point(200, 153),
                Size = new Size(68, 28),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            numAffiliateMinSafety.ValueChanged += (s, e) => RefreshAffiliateGridByQualityFilter();

            chkAffiliateOnlyHighQuality = new CheckBox
            {
                Name = "chkAffiliateOnlyHighQuality",
                Text = "Only high-quality",
                AutoSize = true,
                Location = new Point(300, 158),
                ForeColor = Color.Gainsboro
            };
            chkAffiliateOnlyHighQuality.CheckedChanged += (s, e) => RefreshAffiliateGridByQualityFilter();

            chkAffiliateRankByEngagement = new CheckBox
            {
                Name = "chkAffiliateRankByEngagement",
                Text = "Ưu tiên engagement (săn buffer → TikWM → top Max)",
                AutoSize = true,
                Location = new Point(24, 180),
                ForeColor = Color.Gainsboro,
                Checked = true
            };
            chkAffiliateRankByEngagement.CheckedChanged += chkAffiliateRankByEngagement_CheckedChanged;

            var lblAffiliateBufferMult = new Label
            {
                Text = "Hệ số buffer ×",
                AutoSize = true,
                Location = new Point(420, 182),
                ForeColor = Color.Gainsboro
            };

            numAffiliateBufferMultiplier = new NumericUpDown
            {
                Name = "numAffiliateBufferMultiplier",
                Location = new Point(520, 178),
                Size = new Size(56, 28),
                Minimum = 1.5m,
                Maximum = 5.0m,
                Increment = 0.5m,
                DecimalPlaces = 1,
                Value = 2.5m,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            numAffiliateBufferMultiplier.ValueChanged += numAffiliateBufferMultiplier_ValueChanged;

            btnHuntAffiliates = new Button
            {
                Name = "btnHuntAffiliates",
                Text = "Hunt Affiliates",
                Location = new Point(24, 214),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnHuntAffiliates.FlatAppearance.BorderSize = 0;
            btnHuntAffiliates.Click += btnHuntAffiliates_Click;

            btnStopHunt = new Button
            {
                Name = "btnStopHunt",
                Text = "Stop",
                Location = new Point(182, 214),
                Size = new Size(90, 40),
                BackColor = Color.FromArgb(180, 70, 70),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Enabled = false
            };
            btnStopHunt.FlatAppearance.BorderSize = 0;
            btnStopHunt.Click += btnStopHunt_Click;

            btnExportAffiliateCsv = new Button
            {
                Name = "btnExportAffiliateCsv",
                Text = "Xuất CSV / HTML",
                Location = new Point(280, 214),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false
            };
            btnExportAffiliateCsv.FlatAppearance.BorderSize = 0;
            btnExportAffiliateCsv.Click += btnExportAffiliateCsv_Click;

            btnPushToAiVideoGen = new Button
            {
                Name = "btnPushToAiVideoGen",
                Text = "Đẩy sang AI Video Gen",
                Location = new Point(438, 214),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false
            };
            btnPushToAiVideoGen.FlatAppearance.BorderSize = 0;
            btnPushToAiVideoGen.Click += btnPushToAiVideoGen_Click;

            btnPushHighQualityToAiVideoGen = new Button
            {
                Name = "btnPushHighQualityToAiVideoGen",
                Text = "Push High-Quality",
                Location = new Point(646, 214),
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false
            };
            btnPushHighQualityToAiVideoGen.FlatAppearance.BorderSize = 0;
            btnPushHighQualityToAiVideoGen.Click += btnPushHighQualityToAiVideoGen_Click;

            btnAffiliateDeepDive = new Button
            {
                Name = "btnAffiliateDeepDive",
                Text = "🔍 Phân tích Deep Dive",
                Location = new Point(24, 270),
                Size = new Size(240, 40),
                BackColor = Color.FromArgb(20, 60, 140),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Enabled = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnAffiliateDeepDive.FlatAppearance.BorderSize = 0;
            btnAffiliateDeepDive.Click += btnAffiliateDeepDive_Click;

            btnDownloadSelectedAffiliate = new Button
            {
                Name = "btnDownloadSelectedAffiliate",
                Text = "⬇ Tải video",
                Location = new Point(272, 270),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(46, 100, 78),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Enabled = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnDownloadSelectedAffiliate.FlatAppearance.BorderSize = 0;
            btnDownloadSelectedAffiliate.Click += btnDownloadSelectedAffiliate_Click;

            chkAffiliateAutoEnrich = new CheckBox
            {
                Name = "chkAffiliateAutoEnrich",
                Text = "Tự động enrich sau Hunt (số liệu + link affiliate)",
                AutoSize = true,
                Checked = true,
                ForeColor = Color.Gainsboro,
                Location = new Point(440, 280),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            chkAffiliateAutoEnrich.CheckedChanged += chkAffiliateAutoEnrich_CheckedChanged;

            lblAffiliateEnrichStatus = new Label
            {
                Name = "lblAffiliateEnrichStatus",
                Text = string.Empty,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(24, 306),
                Size = new Size(1020, 18),
                ForeColor = Color.FromArgb(150, 170, 200),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            lnkAffiliateDownloadFolder = new LinkLabel
            {
                Name = "lnkAffiliateDownloadFolder",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(24, 326),
                Size = new Size(1020, 22),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Color.FromArgb(140, 185, 255),
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.FromArgb(140, 185, 255),
                DisabledLinkColor = Color.Gray,
                ForeColor = Color.FromArgb(180, 190, 210),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                UseMnemonic = false,
                Text = "📁 Thư mục tải video: …"
            };
            lnkAffiliateDownloadFolder.LinkClicked += LnkAffiliateDownloadFolder_LinkClicked;

            _affiliateBindingList = new BindingList<AffiliateCandidate>();
            _affiliateBindingList.ListChanged += AffiliateBindingList_ListChanged;

            dgvAffiliateResults = new DataGridView
            {
                Name = "dgvAffiliateResults",
                Location = new Point(24, 352),
                Size = new Size(1020, 294),
                AutoGenerateColumns = true,
                ShowCellToolTips = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgvAffiliateResults.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            dgvAffiliateResults.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            dgvAffiliateResults.EnableHeadersVisualStyles = false;
            dgvAffiliateResults.DataSource = _affiliateBindingList;
            dgvAffiliateResults.DataBindingComplete += dgvAffiliateResults_DataBindingComplete;
            dgvAffiliateResults.CellFormatting += dgvAffiliateResults_CellFormatting;
            dgvAffiliateResults.CellDoubleClick += dgvAffiliateResults_CellDoubleClick;
            dgvAffiliateResults.CellClick += dgvAffiliateResults_CellClick;
            dgvAffiliateResults.SelectionChanged += dgvAffiliateResults_SelectionChanged;
            dgvAffiliateResults.CellMouseDown += dgvAffiliateResults_CellMouseDown;

            var affiliateContextMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                ShowImageMargin = false
            };

            var miOpenVideo = new ToolStripMenuItem("🌐 Xem Video gốc");
            miOpenVideo.Click += (s, e) => OpenSelectedAffiliateField(c => c.VideoUrl, "VideoUrl");
            affiliateContextMenu.Items.Add(miOpenVideo);

            var miOpenProfile = new ToolStripMenuItem("👤 Xem Kênh tác giả");
            miOpenProfile.Click += (s, e) => OpenSelectedAffiliateField(c => c.ProfileUrl, "ProfileUrl");
            affiliateContextMenu.Items.Add(miOpenProfile);

            affiliateContextMenu.Items.Add(new ToolStripSeparator());

            var miPeekCaption = new ToolStripMenuItem("📋 Xem / copy Caption đầy đủ");
            miPeekCaption.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.ProductName, "Caption");
            affiliateContextMenu.Items.Add(miPeekCaption);

            var miPeekKeyword = new ToolStripMenuItem("📋 Xem / copy Từ khoá đầy đủ");
            miPeekKeyword.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.SourceKeyword, "Từ khoá");
            affiliateContextMenu.Items.Add(miPeekKeyword);

            var miPeekHashtags = new ToolStripMenuItem("📋 Xem / copy Hashtag đầy đủ");
            miPeekHashtags.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.Hashtags, "Hashtag");
            affiliateContextMenu.Items.Add(miPeekHashtags);

            var miPeekLinked = new ToolStripMenuItem("📋 Xem / copy Link aff đầy đủ");
            miPeekLinked.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.LinkedProduct, "Link aff");
            affiliateContextMenu.Items.Add(miPeekLinked);

            var miPeekVoice = new ToolStripMenuItem("📋 Xem / copy Lời thoại đầy đủ");
            miPeekVoice.Click += (s, ev) => ShowSelectedAffiliateLongTextPeek(c => c.VoiceoverTranscript, "Lời thoại");
            affiliateContextMenu.Items.Add(miPeekVoice);

            affiliateContextMenu.Items.Add(new ToolStripSeparator());

            var miRefreshMetrics = new ToolStripMenuItem("📊 Refresh số liệu dòng này");
            miRefreshMetrics.Click += async (s, e) => await RefreshSelectedAffiliateMetricsAsync().ConfigureAwait(true);
            affiliateContextMenu.Items.Add(miRefreshMetrics);

            var miRescanAnchor = new ToolStripMenuItem("🛒 Quét lại link affiliate");
            miRescanAnchor.Click += async (s, e) => await RescanSelectedAffiliateAnchorAsync().ConfigureAwait(true);
            affiliateContextMenu.Items.Add(miRescanAnchor);

            dgvAffiliateResults.ContextMenuStrip = affiliateContextMenu;

            var affiliateGridTip = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
            affiliateGridTip.SetToolTip(dgvAffiliateResults,
                "Bấm đúp dòng để xem video MP4 native. Bấm ô Caption / Từ khoá / Hashtag / Link aff / Lời thoại để xem đầy đủ và sao chép. " +
                "Bấm đơn ô «Link video» để copy URL. " +
                "Điểm an toàn: 100 = sạch, càng thấp càng nhiều rủi ro. «Hướng dẫn chấm điểm» xem tiêu chí.");
            var scoringGuide =
                "Engagement Score (0-100) — cộng từ 6 yếu tố:\r\n\r\n" +
                "  📊 Views (30 điểm, log scale)\r\n" +
                "       10M+ → 30 · 1M → 27 · 500K → 24 · 100K → 20\r\n" +
                "       50K → 16 · 10K → 12 · 1K → 6 · <1K → 2\r\n\r\n" +
                "  ❤ Like rate = likes/views × 100 (20 điểm)\r\n" +
                "       ≥15% → 20 · ≥10% → 16 · ≥5% → 12 · ≥3% → 8 · ≥1% → 4\r\n\r\n" +
                "  💬 Comment rate (15 điểm)\r\n" +
                "       ≥1% → 15 · ≥0.5% → 10 · ≥0.2% → 6 · ≥0.05% → 3\r\n\r\n" +
                "  ↗ Share rate (15 điểm)\r\n" +
                "       ≥1% → 15 · ≥0.5% → 10 · ≥0.2% → 6 · ≥0.05% → 3\r\n\r\n" +
                "  💾 Save rate (15 điểm) — nếu TikWM không trả saves thì\r\n" +
                "       chuyển sang total engagement rate (likes+cmt+share)/views.\r\n\r\n" +
                "  ⏱ Duration sweet spot (5 điểm — proxy cho «thời gian ở lại»)\r\n" +
                "       7-25s → 5 · 5-60s → 3 · 3-90s → 1 · ngoài → 0\r\n\r\n" +
                "Ngưỡng: ≥75 video viral mạnh · 50-74 ổn · <50 yếu.\r\n" +
                "Score = 0 nghĩa là chưa enrich metrics (chờ auto-enrich xong).";
            affiliateGridTip.SetToolTip(numAffiliateMinSafety, scoringGuide);
            affiliateGridTip.SetToolTip(lblAffiliateMinSafety, scoringGuide);
            affiliateGridTip.SetToolTip(chkAffiliateOnlyHighQuality, scoringGuide);
            affiliateGridTip.SetToolTip(btnAffiliateDeepDive,
                "Tải video về, nén nhỏ <5MB rồi gửi Gemini phân tích: voiceover, kịch bản, đoạn ăn tiền (số giây).");
            affiliateGridTip.SetToolTip(btnDownloadSelectedAffiliate,
                "Tải MP4 tuần tự cho các dòng đang chọn. File lưu dưới thư mục con «downloads» theo từ khoá (xem dòng link ngay trên bảng). Bấm «Dừng» để hủy giữa chừng.");
            affiliateGridTip.SetToolTip(chkAffiliateAutoEnrich,
                "Khi bật: sau khi Hunt xong sẽ TỰ ĐỘNG gọi TikWM API lấy số liệu (views/likes/comments/shares/dài/đăng), " +
                "rồi tự động quét link affiliate (anchor mobile UA) cho từng dòng. Chạy ngầm, có thể bấm Stop để hủy.");
            affiliateGridTip.SetToolTip(chkAffiliateRankByEngagement,
                "Chế độ Video: săn thêm video (buffer × hệ số), gọi TikWM để chấm engagement, rồi chỉ giữ top «Max Results». " +
                "Tắt nếu muốn giữ đúng thứ tự feed TikTok mà không xếp hạng.");
            affiliateGridTip.SetToolTip(numAffiliateBufferMultiplier,
                "Số video tối đa săn mỗi từ khoá ≈ Max Results × hệ số (tối đa 120). Ví dụ Max 20 × 2,5 → ~50 ứng viên trước khi cắt top 20.");

            tabAffiliateHunter.Controls.Add(lblKeywords);
            tabAffiliateHunter.Controls.Add(txtAffiliateKeywords);
            tabAffiliateHunter.Controls.Add(lblAffiliateSearchMode);
            tabAffiliateHunter.Controls.Add(cbAffiliateSearchMode);
            tabAffiliateHunter.Controls.Add(lblMaxResults);
            tabAffiliateHunter.Controls.Add(numAffiliateMaxResults);
            tabAffiliateHunter.Controls.Add(lblAffiliateMinSafety);
            tabAffiliateHunter.Controls.Add(numAffiliateMinSafety);
            tabAffiliateHunter.Controls.Add(chkAffiliateOnlyHighQuality);
            tabAffiliateHunter.Controls.Add(chkAffiliateRankByEngagement);
            tabAffiliateHunter.Controls.Add(lblAffiliateBufferMult);
            tabAffiliateHunter.Controls.Add(numAffiliateBufferMultiplier);
            tabAffiliateHunter.Controls.Add(btnHuntAffiliates);
            tabAffiliateHunter.Controls.Add(btnStopHunt);
            tabAffiliateHunter.Controls.Add(btnExportAffiliateCsv);
            tabAffiliateHunter.Controls.Add(btnPushToAiVideoGen);
            tabAffiliateHunter.Controls.Add(btnPushHighQualityToAiVideoGen);
            tabAffiliateHunter.Controls.Add(btnAffiliateDeepDive);
            tabAffiliateHunter.Controls.Add(btnDownloadSelectedAffiliate);
            tabAffiliateHunter.Controls.Add(chkAffiliateAutoEnrich);
            tabAffiliateHunter.Controls.Add(lblAffiliateEnrichStatus);
            tabAffiliateHunter.Controls.Add(lnkAffiliateDownloadFolder);
            tabAffiliateHunter.Controls.Add(dgvAffiliateResults);

            RefreshAffiliateDownloadFolderHint();
        }

        private void BuildAiVideoGenUi()
        {
            const int renderPanelHeight = 68;
            const int aiTabProgressReserve = 14;
            const int tabModeHeight = 540;
            const int tabModesY = 40;
            const int stickyHostHeight = tabModesY + tabModeHeight + 12;
            const int gridHeight = 201;
            const int bodyTop = 12;
            const int scriptTopInner = bodyTop + gridHeight + 16;

            pnlAiVideoGenScrollHost = new Panel
            {
                Name = "pnlAiVideoGenScrollHost",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(31, 34, 42)
            };

            pnlAiVideoGenStickyHost = new Panel
            {
                Name = "pnlAiVideoGenStickyHost",
                Dock = DockStyle.Top,
                Height = stickyHostHeight,
                BackColor = Color.FromArgb(31, 34, 42)
            };

            lblAiVideoGenProductsTitle = new Label
            {
                Text = "Dữ liệu sản phẩm từ Affiliate Hunter",
                AutoSize = true,
                Location = new Point(24, 10),
                Parent = pnlAiVideoGenStickyHost
            };

            pnlAiRenderProgress = new Panel
            {
                Name = "pnlAiRenderProgress",
                Size = new Size(1008, renderPanelHeight),
                BackColor = Color.FromArgb(28, 30, 38),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = true
            };

            var lblAiRenderProgressHeader = new Label
            {
                Name = "lblAiRenderProgressHeader",
                Text = "Tiến độ (tối đa 3 luồng)",
                AutoSize = false,
                Location = new Point(8, 2),
                Size = new Size(560, 16),
                ForeColor = Color.FromArgb(160, 165, 180),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var aiRenderProgressTip = new ToolTip();
            const string aiRenderProgressTipText =
                "Mỗi «luồng» là một video đang được xử lý. Render slideshow nhiều video có thể dùng đến 3 luồng cùng lúc. Affiliate chuyên sâu / Mascot thường chỉ dùng luồng 1; luồng 2–3 sẽ hiện «đang chờ».";
            aiRenderProgressTip.SetToolTip(lblAiRenderProgressHeader, aiRenderProgressTipText);
            aiRenderProgressTip.SetToolTip(pnlAiRenderProgress, aiRenderProgressTipText);

            const int progLabelLeft = 8;
            const int progLabelWidth = 200;
            const int progBarLeft = progLabelLeft + progLabelWidth + 8;
            const int progBarMaxWidth = 260;
            var barWidth = Math.Min(progBarMaxWidth, Math.Max(96, pnlAiRenderProgress.Width - progBarLeft - 10));

            lblAiRenderSlot1 = new Label
            {
                Text = "Luồng 1: đang chờ",
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(progLabelLeft, 20),
                Size = new Size(progLabelWidth, 14),
                ForeColor = Color.FromArgb(130, 135, 150),
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblAiRenderSlot2 = new Label
            {
                Text = "Luồng 2: đang chờ",
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(progLabelLeft, 36),
                Size = new Size(progLabelWidth, 14),
                ForeColor = Color.FromArgb(130, 135, 150),
                TextAlign = ContentAlignment.MiddleLeft
            };
            lblAiRenderSlot3 = new Label
            {
                Text = "Luồng 3: đang chờ",
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(progLabelLeft, 52),
                Size = new Size(progLabelWidth, 14),
                ForeColor = Color.FromArgb(130, 135, 150),
                TextAlign = ContentAlignment.MiddleLeft
            };

            pbAiRenderSlot1 = new ProgressBar
            {
                Location = new Point(progBarLeft, 19),
                Size = new Size(barWidth, 11),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            pbAiRenderSlot2 = new ProgressBar
            {
                Location = new Point(progBarLeft, 35),
                Size = new Size(barWidth, 11),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            pbAiRenderSlot3 = new ProgressBar
            {
                Location = new Point(progBarLeft, 51),
                Size = new Size(barWidth, 11),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            pnlAiRenderProgress.Controls.Add(lblAiRenderProgressHeader);
            pnlAiRenderProgress.Controls.Add(lblAiRenderSlot1);
            pnlAiRenderProgress.Controls.Add(lblAiRenderSlot2);
            pnlAiRenderProgress.Controls.Add(lblAiRenderSlot3);
            pnlAiRenderProgress.Controls.Add(pbAiRenderSlot1);
            pnlAiRenderProgress.Controls.Add(pbAiRenderSlot2);
            pnlAiRenderProgress.Controls.Add(pbAiRenderSlot3);

            _aiVideoGenBindingList = new BindingList<AiVideoGenInputItem>();
            dgvAiVideoGenInput = new DataGridView
            {
                Name = "dgvAiVideoGenInput",
                Location = new Point(24, bodyTop),
                Size = new Size(1020, gridHeight),
                AutoGenerateColumns = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DataSource = _aiVideoGenBindingList,
                Parent = pnlAiVideoGenScrollHost
            };

            lblAiVideoGenProductBlockHint = new Label
            {
                Name = "lblAiVideoGenProductBlockHint",
                Text = "Mascot / Triết lý không dùng bảng sản phẩm. Chuyển lại tab «Slideshow» hoặc «Affiliate» trên thanh tab phía trên (vùng cố định) khi cần dữ liệu từ Săn Affiliate.",
                Location = new Point(24, bodyTop),
                Size = new Size(1020, gridHeight),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(160, 165, 180),
                BackColor = Color.FromArgb(26, 28, 36),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(16, 14, 16, 14),
                Visible = false,
                Parent = pnlAiVideoGenScrollHost
            };

            tabAiVideoGenModes = new TabControl
            {
                Name = "tabAiVideoGenModes",
                Location = new Point(24, tabModesY),
                Size = new Size(1020, tabModeHeight),
                Padding = new Point(12, 8),
                Multiline = true,
                Parent = pnlAiVideoGenStickyHost
            };
            tabAiModeSlideshow = new TabPage("Slideshow sản phẩm") { AutoScroll = true, BackColor = tabAiVideoGen.BackColor };
            tabAiModeAffiliateDeep = new TabPage("Affiliate chuyên sâu") { AutoScroll = true, BackColor = tabAiVideoGen.BackColor };
            tabAiModeMascot = new TabPage("Mascot Story") { AutoScroll = true, AutoScrollMinSize = new Size(0, 420 + aiTabProgressReserve), BackColor = tabAiVideoGen.BackColor };
            tabAiModePhilosophy = new TabPage("Triết lý / Quote") { AutoScroll = true, BackColor = tabAiVideoGen.BackColor };
            tabAiModeVideoReup = new TabPage("Video reup") { AutoScroll = true, BackColor = tabAiVideoGen.BackColor };
            tabAiVideoGenModes.TabPages.Add(tabAiModeSlideshow);
            tabAiVideoGenModes.TabPages.Add(tabAiModeAffiliateDeep);
            tabAiVideoGenModes.TabPages.Add(tabAiModeMascot);
            tabAiVideoGenModes.TabPages.Add(tabAiModePhilosophy);
            tabAiVideoGenModes.TabPages.Add(tabAiModeVideoReup);
            tabAiVideoGenModes.SelectedIndexChanged += tabAiVideoGenModes_SelectedIndexChanged;

            var lblTransition = new Label
            {
                Text = "Transition (s)",
                AutoSize = true,
                Location = new Point(18, 16 + aiTabProgressReserve)
            };
            numAiTransitionDuration = new NumericUpDown
            {
                Name = "numAiTransitionDuration",
                Location = new Point(18, 40 + aiTabProgressReserve),
                Size = new Size(120, 30),
                DecimalPlaces = 1,
                Increment = 0.1M,
                Minimum = 0.2M,
                Maximum = 2.0M,
                Value = 0.6M,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblTextSize = new Label
            {
                Text = "Text Size",
                AutoSize = true,
                Location = new Point(200, 16 + aiTabProgressReserve)
            };
            numAiTextSize = new NumericUpDown
            {
                Name = "numAiTextSize",
                Location = new Point(200, 40 + aiTabProgressReserve),
                Size = new Size(120, 30),
                Minimum = 24,
                Maximum = 96,
                Value = 50,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblMusicVolume = new Label
            {
                Text = "Music Volume (%)",
                AutoSize = true,
                Location = new Point(382, 16 + aiTabProgressReserve)
            };
            numAiMusicVolume = new NumericUpDown
            {
                Name = "numAiMusicVolume",
                Location = new Point(382, 40 + aiTabProgressReserve),
                Size = new Size(120, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 14,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            btnGenerateGeminiPrompt = new Button
            {
                Name = "btnGenerateGeminiPrompt",
                Text = "Tạo kịch bản AI",
                Location = new Point(18, 88 + aiTabProgressReserve),
                Size = new Size(200, 34),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnGenerateGeminiPrompt.FlatAppearance.BorderSize = 0;
            btnGenerateGeminiPrompt.Click += btnGenerateGeminiPrompt_Click;

            btnReviewScriptBeforeRender = new Button
            {
                Name = "btnReviewScriptBeforeRender",
                Text = "Duyệt kịch bản trước render",
                Location = new Point(224, 88 + aiTabProgressReserve),
                Size = new Size(210, 34),
                BackColor = Color.FromArgb(88, 101, 242),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnReviewScriptBeforeRender.FlatAppearance.BorderSize = 0;
            btnReviewScriptBeforeRender.Click += btnReviewScriptBeforeRender_Click;

            btnCopyAiVideoPrompt = new Button
            {
                Name = "btnCopyAiVideoPrompt",
                Text = "Sao chép",
                Location = new Point(442, 88 + aiTabProgressReserve),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnCopyAiVideoPrompt.FlatAppearance.BorderSize = 0;
            btnCopyAiVideoPrompt.Click += btnCopyAiVideoPrompt_Click;

            btnSaveAiVideoPrompt = new Button
            {
                Name = "btnSaveAiVideoPrompt",
                Text = "Lưu .txt",
                Location = new Point(548, 88 + aiTabProgressReserve),
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnSaveAiVideoPrompt.FlatAppearance.BorderSize = 0;
            btnSaveAiVideoPrompt.Click += btnSaveAiVideoPrompt_Click;

            btnRenderAiVideo = new Button
            {
                Name = "btnRenderAiVideo",
                Text = "Render video sản phẩm",
                Location = new Point(654, 88 + aiTabProgressReserve),
                Size = new Size(180, 34),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnRenderAiVideo.FlatAppearance.BorderSize = 0;
            btnRenderAiVideo.Click += btnRenderAiVideo_Click;

            tabAiModeSlideshow.Controls.Add(lblTransition);
            tabAiModeSlideshow.Controls.Add(numAiTransitionDuration);
            tabAiModeSlideshow.Controls.Add(lblTextSize);
            tabAiModeSlideshow.Controls.Add(numAiTextSize);
            tabAiModeSlideshow.Controls.Add(lblMusicVolume);
            tabAiModeSlideshow.Controls.Add(numAiMusicVolume);
            tabAiModeSlideshow.Controls.Add(btnGenerateGeminiPrompt);
            tabAiModeSlideshow.Controls.Add(btnReviewScriptBeforeRender);
            tabAiModeSlideshow.Controls.Add(btnCopyAiVideoPrompt);
            tabAiModeSlideshow.Controls.Add(btnSaveAiVideoPrompt);
            tabAiModeSlideshow.Controls.Add(btnRenderAiVideo);

            var lblAffiliateDeepHint = new Label
            {
                Text = "Dùng dòng đã chọn trong bảng sản phẩm phía trên. Tab này chỉ chạy render Affiliate chuyên sâu (khác slideshow).",
                AutoSize = false,
                Location = new Point(18, 16 + aiTabProgressReserve),
                Size = new Size(980, 48),
                ForeColor = Color.Gainsboro
            };
            btnRunAffiliateDeepVideo = new Button
            {
                Name = "btnRunAffiliateDeepVideo",
                Text = "Render Affiliate chuyên sâu",
                Location = new Point(18, 76 + aiTabProgressReserve),
                Size = new Size(240, 40),
                BackColor = Color.FromArgb(131, 96, 195),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnRunAffiliateDeepVideo.FlatAppearance.BorderSize = 0;
            btnRunAffiliateDeepVideo.Click += btnRunAffiliateDeepVideo_Click;
            tabAiModeAffiliateDeep.Controls.Add(lblAffiliateDeepHint);
            tabAiModeAffiliateDeep.Controls.Add(btnRunAffiliateDeepVideo);

            var lblPhilosophyHint = new Label
            {
                Text = "Quote hoặc link bài / video",
                AutoSize = true,
                Location = new Point(18, 16 + aiTabProgressReserve)
            };
            txtPhilosophyInput = new TextBox
            {
                Name = "txtPhilosophyInput",
                Location = new Point(18, 40 + aiTabProgressReserve),
                Size = new Size(980, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            btnRunPhilosophyVideo = new Button
            {
                Name = "btnRunPhilosophyVideo",
                Text = "Chạy video Triết lý/Quote",
                Location = new Point(18, 82 + aiTabProgressReserve),
                Size = new Size(280, 36),
                BackColor = Color.FromArgb(92, 118, 204),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnRunPhilosophyVideo.FlatAppearance.BorderSize = 0;
            btnRunPhilosophyVideo.Click += btnRunPhilosophyVideo_Click;
            tabAiModePhilosophy.Controls.Add(lblPhilosophyHint);
            tabAiModePhilosophy.Controls.Add(txtPhilosophyInput);
            tabAiModePhilosophy.Controls.Add(btnRunPhilosophyVideo);

            const int vrTop = 12 + aiTabProgressReserve;
            var lblVideoReupTitle = new Label
            {
                Text = "Video reup — tạo phiên bản mới từ video / dữ liệu affiliate",
                AutoSize = true,
                Location = new Point(18, vrTop),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point)
            };
            var lblVideoReupHint = new Label
            {
                Text = "1) URL: chọn dòng → dán link TikTok vào ô «URL video» → Tab/click ra ngoài để tự tải; hoặc «Nhập từ Săn Affiliate» (cũng tự tải khi có URL).\r\n" +
                       "2) «Thêm dòng» nếu muốn nhập link tay không qua affiliate.\r\n" +
                       "3) Gemini hook → chỉnh câu → Lyria → chọn nhạc → «Render video». Xóa dòng: chọn → Delete.\r\n" +
                       "4) SRT sau khi render. Cần FFmpeg, Lyria, AI Key, .mp3 trong VideoReup\\Music; tải video qua TikWM.",
                Location = new Point(18, vrTop + 26),
                Size = new Size(980, 78),
                ForeColor = Color.FromArgb(180, 185, 198)
            };
            btnPushSelectionToVideoReup = new Button
            {
                Name = "btnPushSelectionToVideoReup",
                Text = "Nhập từ Săn Affiliate",
                Location = new Point(18, vrTop + 112),
                Size = new Size(280, 36),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnPushSelectionToVideoReup.FlatAppearance.BorderSize = 0;
            btnPushSelectionToVideoReup.Click += btnPushSelectionToVideoReup_Click;
            btnVideoReupAddManualRow = new Button
            {
                Name = "btnVideoReupAddManualRow",
                Text = "Thêm dòng (nhập URL)",
                Location = new Point(306, vrTop + 112),
                Size = new Size(220, 36),
                BackColor = Color.FromArgb(55, 95, 160),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupAddManualRow.FlatAppearance.BorderSize = 0;
            btnVideoReupAddManualRow.Click += btnVideoReupAddManualRow_Click;
            lblVideoReupVideoUrl = new Label
            {
                Text = "URL video (TikTok — rời ô để tải nguồn về máy)",
                AutoSize = true,
                Location = new Point(18, vrTop + 156),
                ForeColor = Color.FromArgb(200, 204, 214)
            };
            txtVideoReupVideoUrl = new TextBox
            {
                Name = "txtVideoReupVideoUrl",
                Location = new Point(18, vrTop + 176),
                Size = new Size(980, 28),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(28, 30, 38),
                ForeColor = Color.Gainsboro
            };
            txtVideoReupVideoUrl.Leave += txtVideoReupVideoUrl_Leave;
            var lblVideoReupHook = new Label
            {
                Text = "Câu hook (Gemini / chỉnh tay, 4–7s đọc)",
                AutoSize = true,
                Location = new Point(18, vrTop + 214),
                ForeColor = Color.FromArgb(200, 204, 214)
            };
            txtVideoReupHookDraft = new TextBox
            {
                Name = "txtVideoReupHookDraft",
                Location = new Point(18, vrTop + 234),
                Size = new Size(980, 44),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(28, 30, 38),
                ForeColor = Color.Gainsboro
            };
            txtVideoReupHookDraft.Leave += txtVideoReupHookDraft_Leave;
            btnVideoReupHookGemini = new Button
            {
                Name = "btnVideoReupHookGemini",
                Text = "Gemini: tạo hook",
                Location = new Point(18, vrTop + 288),
                Size = new Size(160, 32),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupHookGemini.FlatAppearance.BorderSize = 0;
            btnVideoReupHookGemini.Click += btnVideoReupHookGemini_Click;
            btnVideoReupHookRegen = new Button
            {
                Name = "btnVideoReupHookRegen",
                Text = "Tạo lại hook",
                Location = new Point(186, vrTop + 288),
                Size = new Size(140, 32),
                BackColor = Color.FromArgb(60, 100, 200),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupHookRegen.FlatAppearance.BorderSize = 0;
            btnVideoReupHookRegen.Click += btnVideoReupHookRegen_Click;
            btnVideoReupLyriaHook = new Button
            {
                Name = "btnVideoReupLyriaHook",
                Text = "Lyria: đọc hook → âm thanh",
                Location = new Point(334, vrTop + 288),
                Size = new Size(220, 32),
                BackColor = Color.FromArgb(120, 70, 160),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupLyriaHook.FlatAppearance.BorderSize = 0;
            btnVideoReupLyriaHook.Click += btnVideoReupLyriaHook_Click;
            lblVideoReupMusicPick = new Label
            {
                Text = "Nhạc nền (.mp3)",
                AutoSize = true,
                Location = new Point(18, vrTop + 328),
                ForeColor = Color.FromArgb(200, 204, 214)
            };
            cbVideoReupMusic = new ComboBox
            {
                Name = "cbVideoReupMusic",
                Location = new Point(140, vrTop + 324),
                Size = new Size(420, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbVideoReupMusic.SelectedIndexChanged += cbVideoReupMusic_SelectedIndexChanged;
            btnVideoReupRenderVideo = new Button
            {
                Name = "btnVideoReupRenderVideo",
                Text = "Render video (ghép + chỉnh)",
                Location = new Point(572, vrTop + 320),
                Size = new Size(220, 36),
                BackColor = Color.FromArgb(160, 90, 60),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupRenderVideo.FlatAppearance.BorderSize = 0;
            btnVideoReupRenderVideo.Click += btnVideoReupRenderVideo_Click;
            btnVideoReupExportSrtHeuristic = new Button
            {
                Name = "btnVideoReupExportSrtHeuristic",
                Text = "Xuất SRT (chia theo câu)",
                Location = new Point(18, vrTop + 368),
                Size = new Size(210, 34),
                BackColor = Color.FromArgb(60, 120, 90),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupExportSrtHeuristic.FlatAppearance.BorderSize = 0;
            btnVideoReupExportSrtHeuristic.Click += btnVideoReupExportSrtHeuristic_Click;
            btnVideoReupExportSrtGemini = new Button
            {
                Name = "btnVideoReupExportSrtGemini",
                Text = "Gemini → timeline → SRT",
                Location = new Point(236, vrTop + 368),
                Size = new Size(220, 34),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnVideoReupExportSrtGemini.FlatAppearance.BorderSize = 0;
            btnVideoReupExportSrtGemini.Click += btnVideoReupExportSrtGemini_Click;
            _videoReupBindingList = new BindingList<VideoReupRowItem>();
            const int vrReupGridTop = vrTop + 412;
            dgvVideoReupInput = new DataGridView
            {
                Name = "dgvVideoReupInput",
                Location = new Point(18, vrReupGridTop),
                Size = new Size(980, 300),
                AutoGenerateColumns = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ScrollBars = ScrollBars.Both,
                DataSource = _videoReupBindingList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            dgvVideoReupInput.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            dgvVideoReupInput.DefaultCellStyle.ForeColor = Color.Gainsboro;
            dgvVideoReupInput.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvVideoReupInput.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            dgvVideoReupInput.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            dgvVideoReupInput.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvVideoReupInput.EnableHeadersVisualStyles = false;
            dgvVideoReupInput.DataBindingComplete += DgvVideoReupInput_DataBindingComplete;
            dgvVideoReupInput.KeyDown += dgvVideoReupInput_KeyDown;
            dgvVideoReupInput.SelectionChanged += dgvVideoReupInput_SelectionChanged;
            tabAiModeVideoReup.Controls.Add(lblVideoReupTitle);
            tabAiModeVideoReup.Controls.Add(lblVideoReupHint);
            tabAiModeVideoReup.Controls.Add(btnPushSelectionToVideoReup);
            tabAiModeVideoReup.Controls.Add(btnVideoReupAddManualRow);
            tabAiModeVideoReup.Controls.Add(lblVideoReupVideoUrl);
            tabAiModeVideoReup.Controls.Add(txtVideoReupVideoUrl);
            tabAiModeVideoReup.Controls.Add(lblVideoReupHook);
            tabAiModeVideoReup.Controls.Add(txtVideoReupHookDraft);
            tabAiModeVideoReup.Controls.Add(btnVideoReupHookGemini);
            tabAiModeVideoReup.Controls.Add(btnVideoReupHookRegen);
            tabAiModeVideoReup.Controls.Add(btnVideoReupLyriaHook);
            tabAiModeVideoReup.Controls.Add(lblVideoReupMusicPick);
            tabAiModeVideoReup.Controls.Add(cbVideoReupMusic);
            tabAiModeVideoReup.Controls.Add(btnVideoReupRenderVideo);
            tabAiModeVideoReup.Controls.Add(btnVideoReupExportSrtHeuristic);
            tabAiModeVideoReup.Controls.Add(btnVideoReupExportSrtGemini);
            tabAiModeVideoReup.Controls.Add(dgvVideoReupInput);
            tabAiModeVideoReup.AutoScrollMinSize = new Size(0, vrReupGridTop + 280 + 36);
            RefreshVideoReupMusicCombo();

            var lblMascotTheme = new Label
            {
                Text = "Chủ đề kênh",
                AutoSize = true,
                Location = new Point(18, 12 + aiTabProgressReserve)
            };
            txtMascotChannelTheme = new TextBox
            {
                Name = "txtMascotChannelTheme",
                Location = new Point(18, 36 + aiTabProgressReserve),
                Size = new Size(440, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbMascotSceneCount = new ComboBox
            {
                Name = "cbMascotSceneCount",
                Location = new Point(468, 36 + aiTabProgressReserve),
                Size = new Size(72, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbMascotSceneCount.Items.AddRange(new object[] { "4", "6", "8" });
            cbMascotSceneCount.SelectedIndex = 0;

            var lblMascotImage = new Label
            {
                Text = "Ảnh linh vật/người mẫu",
                AutoSize = true,
                Location = new Point(18, 76 + aiTabProgressReserve)
            };
            txtMascotImagePath = new TextBox
            {
                Name = "txtMascotImagePath",
                Location = new Point(200, 72 + aiTabProgressReserve),
                Size = new Size(660, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            btnBrowseMascotImage = new Button
            {
                Name = "btnBrowseMascotImage",
                Text = "Chọn ảnh",
                Location = new Point(868, 72 + aiTabProgressReserve),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnBrowseMascotImage.FlatAppearance.BorderSize = 0;
            btnBrowseMascotImage.Click += btnBrowseMascotImage_Click;

            var lblIdentityPack = new Label
            {
                Text = "Avatar Identity Pack (3-5 ảnh)",
                AutoSize = true,
                Location = new Point(18, 112 + aiTabProgressReserve)
            };
            txtAvatarIdentityPack = new TextBox
            {
                Name = "txtAvatarIdentityPack",
                Location = new Point(18, 136 + aiTabProgressReserve),
                Size = new Size(820, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                ReadOnly = true
            };
            btnSelectAvatarIdentityPack = new Button
            {
                Name = "btnSelectAvatarIdentityPack",
                Text = "Chọn Bộ nhận diện",
                Location = new Point(848, 136 + aiTabProgressReserve),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnSelectAvatarIdentityPack.FlatAppearance.BorderSize = 0;
            btnSelectAvatarIdentityPack.Click += btnSelectAvatarIdentityPack_Click;

            pbMascotPreview1 = CreateMascotPreviewBox(new Point(18, 178 + aiTabProgressReserve));
            pbMascotPreview2 = CreateMascotPreviewBox(new Point(273, 178 + aiTabProgressReserve));
            pbMascotPreview3 = CreateMascotPreviewBox(new Point(528, 178 + aiTabProgressReserve));
            pbMascotPreview4 = CreateMascotPreviewBox(new Point(783, 178 + aiTabProgressReserve));
            pbMascotPreview1.Tag = 0;
            pbMascotPreview2.Tag = 1;
            pbMascotPreview3.Tag = 2;
            pbMascotPreview4.Tag = 3;

            cmsMascotPreview = new ContextMenuStrip();
            miRegenerateScene = new ToolStripMenuItem("Tạo lại phân cảnh này");
            miRegenerateScene.Click += miRegenerateScene_Click;
            cmsMascotPreview.Items.Add(miRegenerateScene);
            pbMascotPreview1.ContextMenuStrip = cmsMascotPreview;
            pbMascotPreview2.ContextMenuStrip = cmsMascotPreview;
            pbMascotPreview3.ContextMenuStrip = cmsMascotPreview;
            pbMascotPreview4.ContextMenuStrip = cmsMascotPreview;
            pbMascotPreview1.MouseUp += MascotPreview_MouseUp;
            pbMascotPreview2.MouseUp += MascotPreview_MouseUp;
            pbMascotPreview3.MouseUp += MascotPreview_MouseUp;
            pbMascotPreview4.MouseUp += MascotPreview_MouseUp;
            pbMascotPreview1.Click += MascotPreview_Click;
            pbMascotPreview2.Click += MascotPreview_Click;
            pbMascotPreview3.Click += MascotPreview_Click;
            pbMascotPreview4.Click += MascotPreview_Click;

            btnRunMascotChannelPipeline = new Button
            {
                Name = "btnRunMascotChannelPipeline",
                Text = "Render Mascot Story",
                Location = new Point(18, 330 + aiTabProgressReserve),
                Size = new Size(170, 34),
                BackColor = Color.FromArgb(70, 132, 170),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnRunMascotChannelPipeline.FlatAppearance.BorderSize = 0;
            btnRunMascotChannelPipeline.Click += btnRunMascotChannelPipeline_Click;

            btnPreviewMascotVariants = new Button
            {
                Name = "btnPreviewMascotVariants",
                Text = "Preview 4 ảnh biến thể",
                Location = new Point(200, 330 + aiTabProgressReserve),
                Size = new Size(180, 34),
                BackColor = Color.FromArgb(78, 120, 166),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnPreviewMascotVariants.FlatAppearance.BorderSize = 0;
            btnPreviewMascotVariants.Click += btnPreviewMascotVariants_Click;

            tabAiModeMascot.Controls.Add(lblMascotTheme);
            tabAiModeMascot.Controls.Add(txtMascotChannelTheme);
            tabAiModeMascot.Controls.Add(cbMascotSceneCount);
            tabAiModeMascot.Controls.Add(lblMascotImage);
            tabAiModeMascot.Controls.Add(txtMascotImagePath);
            tabAiModeMascot.Controls.Add(btnBrowseMascotImage);
            tabAiModeMascot.Controls.Add(lblIdentityPack);
            tabAiModeMascot.Controls.Add(txtAvatarIdentityPack);
            tabAiModeMascot.Controls.Add(btnSelectAvatarIdentityPack);
            tabAiModeMascot.Controls.Add(pbMascotPreview1);
            tabAiModeMascot.Controls.Add(pbMascotPreview2);
            tabAiModeMascot.Controls.Add(pbMascotPreview3);
            tabAiModeMascot.Controls.Add(pbMascotPreview4);
            tabAiModeMascot.Controls.Add(btnRunMascotChannelPipeline);
            tabAiModeMascot.Controls.Add(btnPreviewMascotVariants);

            _aiVideoScriptBindingList = new BindingList<AiVideoScriptReviewItem>();
            dgvAiVideoScriptReview = new DataGridView
            {
                Name = "dgvAiVideoScriptReview",
                Location = new Point(24, scriptTopInner),
                Size = new Size(1020, 155),
                AutoGenerateColumns = true,
                DataSource = _aiVideoScriptBindingList,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                GridColor = Color.FromArgb(64, 68, 82),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Parent = pnlAiVideoGenScrollHost
            };
            dgvAiVideoScriptReview.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            dgvAiVideoScriptReview.DefaultCellStyle.ForeColor = Color.Gainsboro;
            dgvAiVideoScriptReview.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            dgvAiVideoScriptReview.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            dgvAiVideoScriptReview.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvAiVideoScriptReview.EnableHeadersVisualStyles = false;
            dgvAiVideoScriptReview.SelectionChanged += dgvAiVideoScriptReview_SelectionChanged;

            txtAiVideoGenPrompt = new TextBox
            {
                Name = "txtAiVideoGenPrompt",
                Location = new Point(24, scriptTopInner + 155 + 12),
                Size = new Size(1020, 90),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Parent = pnlAiVideoGenScrollHost
            };
            txtAiVideoGenPrompt.TextChanged += txtAiVideoGenPrompt_TextChanged;

            void SyncAiVideoGenPanelWidths()
            {
                if (pnlAiVideoGenStickyHost == null || pnlAiVideoGenScrollHost == null)
                {
                    return;
                }

                var stickyW = Math.Max(480, pnlAiVideoGenStickyHost.ClientSize.Width - 48);
                tabAiVideoGenModes.Width = stickyW;

                const int progLabelLeft = 8;
                const int progLabelWidth = 200;
                const int progBarMaxWidth = 260;

                if (pnlAiRenderProgress?.Parent is TabPage)
                {
                    var barW = Math.Min(progBarMaxWidth, Math.Max(96, pnlAiRenderProgress.ClientSize.Width - progLabelLeft - progLabelWidth - 18));
                    var hdr = pnlAiRenderProgress.Controls.Find("lblAiRenderProgressHeader", false);
                    if (hdr != null && hdr.Length > 0)
                    {
                        hdr[0].Width = Math.Max(80, Math.Min(560, pnlAiRenderProgress.ClientSize.Width - 16));
                    }

                    if (pbAiRenderSlot1 != null)
                    {
                        pbAiRenderSlot1.Width = barW;
                    }

                    if (pbAiRenderSlot2 != null)
                    {
                        pbAiRenderSlot2.Width = barW;
                    }

                    if (pbAiRenderSlot3 != null)
                    {
                        pbAiRenderSlot3.Width = barW;
                    }
                }

                var scrollW = Math.Max(480, pnlAiVideoGenScrollHost.ClientSize.Width - 48);
                dgvAiVideoGenInput.Width = scrollW;
                if (lblAiVideoGenProductBlockHint != null)
                {
                    lblAiVideoGenProductBlockHint.Width = scrollW;
                }

                dgvAiVideoScriptReview.Width = scrollW;
                txtAiVideoGenPrompt.Width = scrollW;
            }

            pnlAiVideoGenStickyHost.Resize += (_, __) => SyncAiVideoGenPanelWidths();
            pnlAiVideoGenScrollHost.Resize += (_, __) => SyncAiVideoGenPanelWidths();
            tabAiVideoGenModes.Resize += (_, __) => AttachAiRenderProgressPanelToSelectedModeTab();
            SyncAiVideoGenPanelWidths();

            tabAiVideoGen.Controls.Add(pnlAiVideoGenStickyHost);
            tabAiVideoGen.Controls.Add(pnlAiVideoGenScrollHost);

            tabAiVideoGenModes.SelectedIndex = 0;
            ApplyAiVideoGenModeUiVisibility();
            AttachAiRenderProgressPanelToSelectedModeTab();
        }

        private void BuildAutoPostUi()
        {
            var lblFolder = new Label
            {
                Text = "Thư mục chứa video",
                AutoSize = true,
                Location = new Point(24, 30)
            };

            txtAutoPostFolder = new TextBox
            {
                Name = "txtAutoPostFolder",
                Location = new Point(24, 55),
                Size = new Size(780, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            txtAutoPostFolder.Leave += (_, __) => RefreshAutoPostVideoCombo();

            btnBrowseAutoPostFolder = new Button
            {
                Name = "btnBrowseAutoPostFolder",
                Text = "Chọn thư mục",
                Location = new Point(820, 55),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnBrowseAutoPostFolder.FlatAppearance.BorderSize = 0;
            btnBrowseAutoPostFolder.Click += btnBrowseAutoPostFolder_Click;

            var lblAutoPostProfile = new Label
            {
                Text = "Profile đăng bài (Chrome)",
                AutoSize = true,
                Location = new Point(24, 95)
            };

            cbAutoPostProfile = new ComboBox
            {
                Name = "cbAutoPostProfile",
                Location = new Point(24, 120),
                Size = new Size(420, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbAutoPostProfile.Items.Add("default");
            cbAutoPostProfile.SelectedIndex = 0;

            var lblVideoPick = new Label
            {
                Text = "Video đăng (trong thư mục)",
                AutoSize = true,
                Location = new Point(460, 95)
            };

            cbAutoPostVideoFile = new ComboBox
            {
                Name = "cbAutoPostVideoFile",
                Location = new Point(460, 120),
                Size = new Size(510, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            btnRefreshAutoPostVideos = new Button
            {
                Name = "btnRefreshAutoPostVideos",
                Text = "Làm mới danh sách",
                Location = new Point(820, 118),
                Size = new Size(150, 34),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnRefreshAutoPostVideos.FlatAppearance.BorderSize = 0;
            btnRefreshAutoPostVideos.Click += (_, __) => RefreshAutoPostVideoCombo(GetSelectedAutoPostVideoFullPath());

            var lblHashtags = new Label
            {
                Text = "Hashtag thêm (tuỳ chọn, nối vào cuối caption)",
                AutoSize = true,
                Location = new Point(24, 160)
            };

            txtAutoPostHashtags = new TextBox
            {
                Name = "txtAutoPostHashtags",
                Location = new Point(24, 185),
                Size = new Size(946, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var lblCaptionStyle = new Label
            {
                Text = "Phong cách caption (Gemini) & nội dung đăng",
                AutoSize = true,
                Location = new Point(24, 220)
            };

            cbCaptionStyle = new ComboBox
            {
                Name = "cbCaptionStyle",
                Location = new Point(24, 245),
                Size = new Size(300, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbCaptionStyle.Items.AddRange(new object[]
            {
                "Kiến thức",
                "Triết lý",
                "Hài hước",
                "Câu hỏi / Tranh cãi"
            });
            cbCaptionStyle.SelectedIndex = 0;

            btnGenerateGeminiCaption = new Button
            {
                Name = "btnGenerateGeminiCaption",
                Text = "Tạo caption (Gemini)",
                Location = new Point(340, 243),
                Size = new Size(180, 34),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnGenerateGeminiCaption.FlatAppearance.BorderSize = 0;
            btnGenerateGeminiCaption.Click += btnGenerateGeminiCaption_Click;

            btnPreviewAutoPostVideo = new Button
            {
                Name = "btnPreviewAutoPostVideo",
                Text = "Xem video (mở file)",
                Location = new Point(530, 243),
                Size = new Size(180, 34),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnPreviewAutoPostVideo.FlatAppearance.BorderSize = 0;
            btnPreviewAutoPostVideo.Click += btnPreviewAutoPostVideo_Click;

            txtAutoPostCaption = new TextBox
            {
                Name = "txtAutoPostCaption",
                Location = new Point(24, 280),
                Size = new Size(946, 100),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };

            chkAutoPostVideoApproved = new CheckBox
            {
                Name = "chkAutoPostVideoApproved",
                Text = "Tôi đã xem và duyệt video này trước khi đăng",
                AutoSize = true,
                Location = new Point(24, 388),
                ForeColor = Color.Gainsboro
            };

            chkAutoPostUploadOnly = new CheckBox
            {
                Name = "chkAutoPostUploadOnly",
                Text = "Chỉ upload — không tự bấm Đăng (để gắn link / chỉnh tay)",
                AutoSize = true,
                Location = new Point(24, 412),
                ForeColor = Color.Gainsboro
            };

            btnStartAutoPost = new Button
            {
                Name = "btnStartAutoPost",
                Text = "Đăng lên TikTok",
                Location = new Point(24, 448),
                Size = new Size(350, 40),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnStartAutoPost.FlatAppearance.BorderSize = 0;
            btnStartAutoPost.Click += btnStartAutoPost_Click;

            btnCloseAutoPostBrowser = new Button
            {
                Name = "btnCloseAutoPostBrowser",
                Text = "Đóng trình duyệt Auto Post",
                Location = new Point(390, 448),
                Size = new Size(250, 40),
                BackColor = Color.FromArgb(180, 70, 70),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnCloseAutoPostBrowser.FlatAppearance.BorderSize = 0;
            btnCloseAutoPostBrowser.Click += btnCloseAutoPostBrowser_Click;

            tabAutoPost.Controls.Add(lblFolder);
            tabAutoPost.Controls.Add(txtAutoPostFolder);
            tabAutoPost.Controls.Add(btnBrowseAutoPostFolder);
            tabAutoPost.Controls.Add(lblAutoPostProfile);
            tabAutoPost.Controls.Add(cbAutoPostProfile);
            tabAutoPost.Controls.Add(lblVideoPick);
            tabAutoPost.Controls.Add(cbAutoPostVideoFile);
            tabAutoPost.Controls.Add(btnRefreshAutoPostVideos);
            tabAutoPost.Controls.Add(lblHashtags);
            tabAutoPost.Controls.Add(txtAutoPostHashtags);
            tabAutoPost.Controls.Add(lblCaptionStyle);
            tabAutoPost.Controls.Add(cbCaptionStyle);
            tabAutoPost.Controls.Add(btnGenerateGeminiCaption);
            tabAutoPost.Controls.Add(btnPreviewAutoPostVideo);
            tabAutoPost.Controls.Add(txtAutoPostCaption);
            tabAutoPost.Controls.Add(chkAutoPostVideoApproved);
            tabAutoPost.Controls.Add(chkAutoPostUploadOnly);
            tabAutoPost.Controls.Add(btnStartAutoPost);
            tabAutoPost.Controls.Add(btnCloseAutoPostBrowser);
        }
        private void BuildSettingUi()
        {
            const int gx = 8;
            const int grpFullW = 1048;
            const int colW = 516;
            const int colRightX = 532;
            const int xField = 16;
            const int tbFull = 484;
            const int tbSecret = 300;
            const int xToggleSecret = 322;
            const int xTestSecret = 413;
            const int tbPath = 275;
            const int xBrowsePath = 296;
            const int xDownloadYt = 387;

            GroupBox CreateSettingsGroup(ref int stackY, int colX, int width, string title, int height)
            {
                var box = new GroupBox
                {
                    Text = title,
                    Location = new Point(colX, stackY),
                    Size = new Size(width, height),
                    ForeColor = Color.FromArgb(200, 205, 215),
                    BackColor = Color.FromArgb(36, 39, 48),
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                    FlatStyle = FlatStyle.Flat
                };
                stackY += height + 10;
                return box;
            }

            // --- Tài khoản / Chrome profile (trên cùng, full width) ---
            const int profileGrpH = 316;
            var grpProfiles = new GroupBox
            {
                Text = "Tài khoản (Chrome profile)",
                Location = new Point(gx, 8),
                Size = new Size(grpFullW, profileGrpH),
                ForeColor = Color.FromArgb(200, 205, 215),
                BackColor = Color.FromArgb(36, 39, 48),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                FlatStyle = FlatStyle.Flat
            };

            const int px = 16;
            const int hintY = 22;
            var lblChromeProfilesHint = new Label
            {
                Text =
                    "Một dòng = một nick. Cột «Tên» bắt buộc; «Thư mục Chrome» = user-data khi tách nhiều account. Đăng nhập QR trong trình duyệt khi chạy job — không nhập QR vào bảng.",
                Location = new Point(px, hintY),
                Size = new Size(grpFullW - 40, 38),
                ForeColor = Color.FromArgb(155, 160, 170),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Parent = grpProfiles
            };

            var yProfBtns = hintY + 42;
            btnOpenTikTokManualBrowser = new Button
            {
                Name = "btnOpenTikTokManualBrowser",
                Text = "Đăng nhập TikTok thủ công (1 lần)",
                Location = new Point(px, yProfBtns),
                Size = new Size(300, 30),
                BackColor = Color.FromArgb(46, 125, 168),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Parent = grpProfiles
            };
            btnOpenTikTokManualBrowser.FlatAppearance.BorderSize = 0;
            btnOpenTikTokManualBrowser.Click += btnOpenTikTokLoginBrowser_Click;

            btnCheckBrowserProfileHealth = new Button
            {
                Name = "btnCheckBrowserProfileHealth",
                Text = "Kiểm tra profile (Cookies)",
                Location = new Point(px + 310, yProfBtns),
                Size = new Size(220, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Parent = grpProfiles
            };
            btnCheckBrowserProfileHealth.FlatAppearance.BorderSize = 0;
            btnCheckBrowserProfileHealth.Click += btnCheckBrowserProfileHealth_Click;

            _proxyProfileBindingList = new BindingList<AutomationProfile>();
            var yGrid = yProfBtns + 38;
            dgvProxyProfiles = new DataGridView
            {
                Name = "dgvProxyProfiles",
                Location = new Point(px, yGrid),
                Size = new Size(grpFullW - 32, 200),
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Parent = grpProfiles
            };
            dgvProxyProfiles.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            dgvProxyProfiles.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            dgvProxyProfiles.EnableHeadersVisualStyles = false;

            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Tên profile (app)",
                FillWeight = 14,
                MinimumWidth = 108
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ChromeUserDataPath",
                HeaderText = "Thư mục Chrome (user-data)",
                FillWeight = 34,
                MinimumWidth = 240
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyHost",
                HeaderText = "Proxy host",
                FillWeight = 12,
                MinimumWidth = 95
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyPort",
                HeaderText = "Port",
                FillWeight = 8,
                MinimumWidth = 52
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyUser",
                HeaderText = "Proxy user",
                FillWeight = 12,
                MinimumWidth = 85
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyPass",
                HeaderText = "Proxy pass",
                FillWeight = 12,
                MinimumWidth = 85
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokUniqueId",
                HeaderText = "TikTok @nick",
                FillWeight = 14,
                MinimumWidth = 100
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokNickname",
                HeaderText = "Tên TikTok (hiển thị)",
                FillWeight = 14,
                MinimumWidth = 100
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokUserId",
                HeaderText = "User id (web)",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.DataSource = _proxyProfileBindingList;
            dgvProxyProfiles.SelectionChanged += dgvProxyProfiles_SelectionChanged;

            var profileGridTip = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
            var profileHintDetail =
                "Một dòng = một nick. Chỉ bắt buộc nhập «Tên». «Thư mục Chrome» = folder user-data nếu tách nhiều tài khoản; để trống vẫn chạy profile mặc định.\r\n" +
                "Đăng nhập TikTok bằng QR (hoặc mật khẩu) trong cửa sổ trình duyệt khi bấm Làm ấm / Đăng — không nhập mã QR vào bảng. Cột proxy chỉ khi dùng VPN/proxy.\r\n" +
                "Nút «Đăng nhập TikTok thủ công» mở đúng profile dòng đang chọn (--user-data-dir), vào /login; đóng trình duyệt khi xong.";
            profileGridTip.SetToolTip(grpProfiles, profileHintDetail);
            profileGridTip.SetToolTip(lblChromeProfilesHint, profileHintDetail);
            profileGridTip.SetToolTip(
                dgvProxyProfiles,
                "Cột «Tên profile (app)» dùng trong app (dropdown). «TikTok @nick» / «Tên TikTok» do đăng nhập điền. «Thư mục Chrome»: user-data (vd. ...\\\\User Data\\\\Profile 1).");
            profileGridTip.SetToolTip(
                btnOpenTikTokManualBrowser,
                "Chọn một dòng rồi bấm: mở đăng nhập TikTok đúng profile; sau khi đăng nhập thành công bot đóng trình duyệt và cập nhật @nick.");
            profileGridTip.SetToolTip(
                btnCheckBrowserProfileHealth,
                "Quét browser_profile: có file Cookies (session) hay không — không đọc nội dung cookie.");

            var yAfterProfiles = 8 + profileGrpH + 10;

            // --- Hai cột: trái AI + Captcha + Hành vi; phải Media ---
            var leftY = yAfterProfiles;
            var rightY = yAfterProfiles;

            var grpAi = CreateSettingsGroup(ref leftY, gx, colW, "AI — caption, bình luận, script", 198);
            var lblAiProvider = CreateSettingLabel("AI Provider", xField, 28);
            lblAiProvider.Parent = grpAi;
            txtAiProvider = CreateSettingTextBox("txtAiProvider", xField, 50, tbFull);
            txtAiProvider.Parent = grpAi;

            var lblAiModel = CreateSettingLabel("AI Model", xField, 84);
            lblAiModel.Parent = grpAi;
            txtAiModel = CreateSettingTextBox("txtAiModel", xField, 106, tbFull);
            txtAiModel.Parent = grpAi;

            var lblAiApiKey = CreateSettingLabel("AI API Key", xField, 140);
            lblAiApiKey.Parent = grpAi;
            txtAiApiKey = CreateSettingTextBox("txtAiApiKey", xField, 162, tbSecret, true);
            txtAiApiKey.Parent = grpAi;
            btnToggleAiApiKey = CreateToggleSecretButton(xToggleSecret, 162);
            btnToggleAiApiKey.Parent = grpAi;
            btnToggleAiApiKey.Click += (sender, e) => ToggleSecretVisibility(txtAiApiKey, btnToggleAiApiKey);
            btnTestAi = CreateTestButton(xTestSecret, 162);
            btnTestAi.Parent = grpAi;
            btnTestAi.Click += btnTestAi_Click;

            var grpCaptcha = CreateSettingsGroup(ref leftY, gx, colW, "2Captcha — khi TikTok bắt captcha", 88);
            var lblTwoCaptchaApiKey = CreateSettingLabel("2Captcha API Key", xField, 28);
            lblTwoCaptchaApiKey.Parent = grpCaptcha;
            txtTwoCaptchaApiKey = CreateSettingTextBox("txtTwoCaptchaApiKey", xField, 50, tbSecret, true);
            txtTwoCaptchaApiKey.Parent = grpCaptcha;
            btnToggleTwoCaptchaApiKey = CreateToggleSecretButton(xToggleSecret, 50);
            btnToggleTwoCaptchaApiKey.Parent = grpCaptcha;
            btnToggleTwoCaptchaApiKey.Click += (sender, e) => ToggleSecretVisibility(txtTwoCaptchaApiKey, btnToggleTwoCaptchaApiKey);
            btnTestTwoCaptcha = CreateTestButton(xTestSecret, 50);
            btnTestTwoCaptcha.Parent = grpCaptcha;
            btnTestTwoCaptcha.Click += btnTestTwoCaptcha_Click;

            var grpBehavior = CreateSettingsGroup(ref leftY, gx, colW, "Hành vi bot — hàng chờ & duyệt nội dung", 118);
            chkAutoResumeQueueOnStartup = new CheckBox
            {
                Name = "chkAutoResumeQueueOnStartup",
                Text = "Auto resume warm-up queue on startup",
                AutoSize = true,
                Location = new Point(xField, 26),
                ForeColor = Color.Gainsboro,
                Checked = true,
                Parent = grpBehavior
            };

            chkAlwaysRequirePrePostApproval = new CheckBox
            {
                Name = "chkAlwaysRequirePrePostApproval",
                Text = "Always require pre-post approval",
                AutoSize = true,
                Location = new Point(268, 26),
                ForeColor = Color.Gainsboro,
                Checked = true,
                Parent = grpBehavior
            };

            chkAlwaysRequirePreRenderApproval = new CheckBox
            {
                Name = "chkAlwaysRequirePreRenderApproval",
                Text = "Always require pre-render approval",
                AutoSize = true,
                Location = new Point(268, 52),
                ForeColor = Color.Gainsboro,
                Checked = false,
                Parent = grpBehavior
            };

            chkAutoRunApprovedQueue = new CheckBox
            {
                Name = "chkAutoRunApprovedQueue",
                Text = "Auto run approved items",
                AutoSize = true,
                Location = new Point(xField, 52),
                ForeColor = Color.Gainsboro,
                Checked = false,
                Parent = grpBehavior
            };

            var lblSafetyThreshold = CreateSettingLabel("Block posting if safety score below", xField, 82);
            lblSafetyThreshold.Parent = grpBehavior;
            numBlockPostingSafetyScoreBelow = new NumericUpDown
            {
                Name = "numBlockPostingSafetyScoreBelow",
                Location = new Point(262, 78),
                Size = new Size(88, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Parent = grpBehavior
            };

            var grpMedia = CreateSettingsGroup(ref rightY, colRightX, colW, "Veo / Lyria / FFmpeg / yt-dlp — video, nhạc, xử lý file", 384);
            var lblVeoApiKey = CreateSettingLabel("Veo API Key", xField, 26);
            lblVeoApiKey.Parent = grpMedia;
            txtVeoApiKey = CreateSettingTextBox("txtVeoApiKey", xField, 48, tbSecret, true);
            txtVeoApiKey.Parent = grpMedia;
            btnToggleVeoApiKey = CreateToggleSecretButton(xToggleSecret, 48);
            btnToggleVeoApiKey.Parent = grpMedia;
            btnToggleVeoApiKey.Click += (sender, e) => ToggleSecretVisibility(txtVeoApiKey, btnToggleVeoApiKey);
            btnTestVeo = CreateTestButton(xTestSecret, 48);
            btnTestVeo.Parent = grpMedia;
            btnTestVeo.Click += btnTestVeo_Click;

            var lblLyriaApiKey = CreateSettingLabel("Lyria API Key", xField, 82);
            lblLyriaApiKey.Parent = grpMedia;
            txtLyriaApiKey = CreateSettingTextBox("txtLyriaApiKey", xField, 104, tbSecret, true);
            txtLyriaApiKey.Parent = grpMedia;
            btnToggleLyriaApiKey = CreateToggleSecretButton(xToggleSecret, 104);
            btnToggleLyriaApiKey.Parent = grpMedia;
            btnToggleLyriaApiKey.Click += (sender, e) => ToggleSecretVisibility(txtLyriaApiKey, btnToggleLyriaApiKey);
            btnTestLyria = CreateTestButton(xTestSecret, 104);
            btnTestLyria.Parent = grpMedia;
            btnTestLyria.Click += btnTestLyria_Click;

            var lblVeoEndpoint = CreateSettingLabel("Veo Endpoint", xField, 138);
            lblVeoEndpoint.Parent = grpMedia;
            txtVeoEndpoint = CreateSettingTextBox("txtVeoEndpoint", xField, 160, tbFull);
            txtVeoEndpoint.Parent = grpMedia;

            var lblLyriaEndpoint = CreateSettingLabel("Lyria Endpoint", xField, 194);
            lblLyriaEndpoint.Parent = grpMedia;
            txtLyriaEndpoint = CreateSettingTextBox("txtLyriaEndpoint", xField, 216, tbFull);
            txtLyriaEndpoint.Parent = grpMedia;

            var lblFfmpegPath = CreateSettingLabel("FFmpeg Path (optional)", xField, 250);
            lblFfmpegPath.Parent = grpMedia;
            txtFfmpegPath = CreateSettingTextBox("txtFfmpegPath", xField, 272, tbPath);
            txtFfmpegPath.Parent = grpMedia;
            btnBrowseFfmpegPath = new Button
            {
                Name = "btnBrowseFfmpegPath",
                Text = "Browse",
                Location = new Point(xBrowsePath, 272),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Parent = grpMedia
            };
            btnBrowseFfmpegPath.FlatAppearance.BorderSize = 0;
            btnBrowseFfmpegPath.Click += btnBrowseFfmpegPath_Click;

            var lblYtDlpPath = CreateSettingLabel("yt-dlp Path (cho Deep Dive)", xField, 306);
            lblYtDlpPath.Parent = grpMedia;
            txtYtDlpPath = CreateSettingTextBox("txtYtDlpPath", xField, 328, tbPath);
            txtYtDlpPath.Parent = grpMedia;
            btnBrowseYtDlpPath = new Button
            {
                Name = "btnBrowseYtDlpPath",
                Text = "Browse",
                Location = new Point(xBrowsePath, 328),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Parent = grpMedia
            };
            btnBrowseYtDlpPath.FlatAppearance.BorderSize = 0;
            btnBrowseYtDlpPath.Click += btnBrowseYtDlpPath_Click;

            btnDownloadYtDlp = new Button
            {
                Name = "btnDownloadYtDlp",
                Text = "⬇ Tải yt-dlp",
                Location = new Point(xDownloadYt, 328),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(20, 60, 140),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Parent = grpMedia
            };
            btnDownloadYtDlp.FlatAppearance.BorderSize = 0;
            btnDownloadYtDlp.Click += btnDownloadYtDlp_Click;

            var yBottom = Math.Max(leftY, rightY) + 4;
            lblSettingsValidation = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 120, 120),
                Location = new Point(24, yBottom)
            };

            var ySave = yBottom + 26;
            btnSaveSettings = new Button
            {
                Name = "btnSaveSettings",
                Text = "Save Settings",
                Location = new Point(24, ySave),
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(76, 110, 245),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            btnSaveSettings.FlatAppearance.BorderSize = 0;
            btnSaveSettings.Click += btnSaveSettings_Click;
            HookSettingValidationEvents();

            tabSetting.Controls.Add(grpProfiles);
            tabSetting.Controls.Add(grpAi);
            tabSetting.Controls.Add(grpCaptcha);
            tabSetting.Controls.Add(grpBehavior);
            tabSetting.Controls.Add(grpMedia);
            tabSetting.Controls.Add(lblSettingsValidation);
            tabSetting.Controls.Add(btnSaveSettings);
        }
        private Label CreateSettingLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(x, y),
                ForeColor = Color.Gainsboro
            };
        }

        private TextBox CreateSettingTextBox(string name, int x, int y, int width, bool isSecret = false)
        {
            return new TextBox
            {
                Name = name,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                UseSystemPasswordChar = isSecret
            };
        }

        private Button CreateToggleSecretButton(int x, int y)
        {
            var button = new Button
            {
                Text = "Show",
                Location = new Point(x, y),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Button CreateTestButton(int x, int y)
        {
            var button = new Button
            {
                Text = "Test",
                Location = new Point(x, y),
                Size = new Size(70, 30),
                BackColor = Color.FromArgb(82, 128, 89),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}