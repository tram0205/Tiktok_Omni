using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private TabPage tabHealthDashboard;
        private DataGridView dgvHealthToday;
        private DataGridView dgvHealthPipeline;
        private ListBox lstHealthApiAlerts;
        private Label lblHealthSummary;
        private Timer _healthDashboardTimer;

        private void BuildHealthDashboardTabUi()
        {
            tabHealthDashboard = new TabPage("Dashboard sức khỏe")
            {
                Name = "tabHealthDashboard",
                AutoScroll = true
            };
            ConfigureTabPage(tabHealthDashboard);

            lblHealthSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(8, 8, 8, 4),
                ForeColor = Color.FromArgb(180, 220, 255),
                Font = AppLabelFont,
                Text = "Đang tải số liệu…",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 220
            };

            dgvHealthToday = CreateHealthGrid();
            dgvHealthToday.Columns.Add("Pipeline", "Luồng");
            dgvHealthToday.Columns.Add("Count", "Video hôm nay");
            dgvHealthToday.Dock = DockStyle.Fill;
            ApplyAppGridChrome(dgvHealthToday);

            dgvHealthPipeline = CreateHealthGrid();
            dgvHealthPipeline.Columns.Add("Pipeline", "Luồng");
            dgvHealthPipeline.Columns.Add("Success", "Thành công");
            dgvHealthPipeline.Columns.Add("Failed", "Thất bại");
            dgvHealthPipeline.Columns.Add("Rate", "Tỉ lệ %");
            dgvHealthPipeline.Dock = DockStyle.Fill;
            ApplyAppGridChrome(dgvHealthPipeline);

            var lblToday = new Label
            {
                Text = "Video sản xuất hôm nay (theo luồng)",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 30,
                Margin = new Padding(0, 4, 0, 2),
                ForeColor = Color.FromArgb(200, 204, 214),
                Font = AppCaptionFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblPipe = new Label
            {
                Text = "Tỉ lệ thành công / thất bại",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 30,
                Margin = new Padding(0, 8, 0, 2),
                ForeColor = Color.FromArgb(200, 204, 214),
                Font = AppCaptionFont,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var tblHealthGrids = new TableLayoutPanel
            {
                Name = "tblHealthGrids",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0, 4, 0, 0)
            };
            tblHealthGrids.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblHealthGrids.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            tblHealthGrids.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblHealthGrids.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            tblHealthGrids.Controls.Add(lblToday, 0, 0);
            tblHealthGrids.Controls.Add(dgvHealthToday, 0, 1);
            tblHealthGrids.Controls.Add(lblPipe, 0, 2);
            tblHealthGrids.Controls.Add(dgvHealthPipeline, 0, 3);
            ApplyAppGridChrome(dgvHealthToday);
            ApplyAppGridChrome(dgvHealthPipeline);

            lstHealthApiAlerts = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.FromArgb(255, 180, 120),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                IntegralHeight = false
            };
            var lblAlerts = new Label
            {
                Text = "Cảnh báo API (Veo / Gemini / TTS)",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4, 4, 4, 2),
                ForeColor = Color.FromArgb(255, 200, 100),
                Font = AppCaptionFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            split.Panel1.Controls.Add(tblHealthGrids);
            split.Panel2.Controls.Add(lstHealthApiAlerts);
            split.Panel2.Controls.Add(lblAlerts);

            btnHealthOpenSettings = new Button
            {
                Name = "btnHealthOpenSettings",
                Text = "Mở Cài đặt (API Keys)",
                Dock = DockStyle.Top,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 110, 245),
                ForeColor = Color.White,
                Visible = false
            };
            btnHealthOpenSettings.FlatAppearance.BorderSize = 0;
            btnHealthOpenSettings.Click += btnHealthOpenSettings_Click;

            pnlHealthReadiness = new Panel
            {
                Name = "pnlHealthReadiness",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 8, 8, 10),
                BackColor = Color.FromArgb(28, 30, 38)
            };

            var lblReadinessTitle = new Label
            {
                Text = "Checklist sẵn sàng hệ thống",
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = Color.FromArgb(200, 204, 214),
                Font = AppCaptionFont,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblHealthReadiness = new Label
            {
                Name = "lblHealthReadiness",
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 180, 120),
                Font = AppLabelFont,
                Text = "Checklist s\u1eb5n s\u00e0ng: \u2026",
                Margin = new Padding(0, 6, 12, 4),
                MinimumSize = new Size(0, 28)
            };

            btnHealthRecheck = new Button
            {
                Name = "btnHealthRecheck",
                Text = "Ki\u1ec3m tra l\u1ea1i",
                AutoSize = true,
                MinimumSize = new Size(120, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 64, 77),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 4, 0, 4)
            };
            btnHealthRecheck.FlatAppearance.BorderSize = 0;
            btnHealthRecheck.Click += btnHealthRecheckHealth_Click;

            numMaxConcurrentJobs = new NumericUpDown
            {
                Name = "numMaxConcurrentJobs",
                Minimum = 1,
                Maximum = 16,
                Value = 2,
                Width = 56,
                MinimumSize = new Size(56, 28),
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = AppInputFont,
                Margin = new Padding(0, 4, 0, 4)
            };
            numMaxConcurrentJobs.ValueChanged += async (_, __) =>
            {
                try
                {
                    var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                    settings.MaxConcurrentJobs = (int)numMaxConcurrentJobs.Value;
                    await _configManager.SaveAsync(settings).ConfigureAwait(true);
                    _jobWorkerService?.RefreshConcurrencyLimit();
                }
                catch
                {
                    // ignored
                }
            };

            var flpJobConcurrency = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 4, 0, 8)
            };
            flpJobConcurrency.Controls.Add(new Label
            {
                Text = "Max jobs song song",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                Font = AppLabelFont,
                Margin = new Padding(0, 8, 8, 4),
                MinimumSize = new Size(0, 28)
            });
            flpJobConcurrency.Controls.Add(numMaxConcurrentJobs);

            var flpReadiness = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 4, 0, 2)
            };
            flpReadiness.Controls.Add(lblHealthReadiness);
            flpReadiness.Controls.Add(btnHealthRecheck);

            pnlHealthReadiness.Controls.Add(flpReadiness);
            pnlHealthReadiness.Controls.Add(flpJobConcurrency);
            pnlHealthReadiness.Controls.Add(lblReadinessTitle);

            tabHealthDashboard.Controls.Add(split);
            tabHealthDashboard.Controls.Add(btnHealthOpenSettings);
            tabHealthDashboard.Controls.Add(pnlHealthReadiness);
            tabHealthDashboard.Controls.Add(lblHealthSummary);

            _healthDashboardTimer = new Timer { Interval = 5000 };
            _healthDashboardTimer.Tick += async (s, e) => await RefreshHealthDashboardAsync().ConfigureAwait(true);
            _healthDashboardTimer.Start();
        }

        private static DataGridView CreateHealthGrid()
        {
            var grid = new DataGridView
            {
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                DefaultCellStyle =
                {
                    BackColor = Color.FromArgb(31, 34, 42),
                    ForeColor = Color.Gainsboro,
                    SelectionBackColor = Color.FromArgb(76, 110, 245),
                    SelectionForeColor = Color.White,
                    Padding = new Padding(8, 4, 8, 4),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = Color.FromArgb(36, 39, 48),
                    ForeColor = Color.Gainsboro,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    WrapMode = DataGridViewTriState.False
                }
            };
            ApplyAppGridChrome(grid);
            return grid;
        }

        private void BindHealthDashboardGrids()
        {
            var s = _productionHealthStore.Snapshot;
            if (lblHealthSummary != null)
            {
                lblHealthSummary.Text =
                    $"Tổng video: {s.TotalVideosProduced} | Lỗi: {s.TotalErrors} | Chờ duyệt: {s.PendingApprovals} | Cập nhật: {s.UpdatedAtUtc.ToLocalTime():HH:mm:ss}";
            }

            if (dgvHealthToday != null)
            {
                dgvHealthToday.Rows.Clear();
                foreach (var kv in (s.VideosTodayByPipeline ?? new System.Collections.Generic.Dictionary<string, int>())
                             .OrderByDescending(x => x.Value))
                {
                    dgvHealthToday.Rows.Add(kv.Key, kv.Value);
                }

                if (dgvHealthToday.Rows.Count == 0)
                {
                    dgvHealthToday.Rows.Add("(chưa có)", 0);
                }

                EnsureAppGridRowHeights(dgvHealthToday);
            }

            if (dgvHealthPipeline != null)
            {
                dgvHealthPipeline.Rows.Clear();
                foreach (var kv in (s.PipelineStats ?? new System.Collections.Generic.Dictionary<string, PipelineRunStats>())
                             .OrderBy(x => x.Key))
                {
                    var st = kv.Value;
                    dgvHealthPipeline.Rows.Add(
                        kv.Key,
                        st.Success,
                        st.Failed,
                        st.SuccessRatePercent.ToString("0.#") + "%");
                }

                EnsureAppGridRowHeights(dgvHealthPipeline);
            }

            if (lstHealthApiAlerts != null)
            {
                lstHealthApiAlerts.Items.Clear();
                var alerts = s.ApiAlerts ?? new System.Collections.Generic.List<ApiHealthAlert>();
                if (alerts.Count == 0)
                {
                    lstHealthApiAlerts.Items.Add("(Không có cảnh báo API gần đây)");
                }
                else
                {
                    foreach (var a in alerts.Take(20))
                    {
                        lstHealthApiAlerts.Items.Add(
                            $"[{a.AtUtc.ToLocalTime():HH:mm}] {a.Service}: {a.Message}");
                    }
                }
            }
        }
    }
}
