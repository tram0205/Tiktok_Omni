using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveCharts;
using LiveCharts.WinForms;
using Axis = LiveCharts.Wpf.Axis;
using LineSeries = LiveCharts.Wpf.LineSeries;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private TabPage tabRevenueDashboard;
        private Button btnFetchRevenue;
        private Label lblRevenueSummary;
        private LiveCharts.WinForms.CartesianChart chartRevenueCommission;
        private DataGridView dgvRevenueReport;
        private bool _revenueFetchRunning;

        private void BuildRevenueDashboardTabUi()
        {
            tabRevenueDashboard = new TabPage("Doanh thu Affiliate")
            {
                Name = "tabRevenueDashboard",
                AutoScroll = true
            };
            ConfigureTabPage(tabRevenueDashboard);

            lblRevenueSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                ForeColor = Color.FromArgb(180, 220, 255),
                Text = "Báo cáo hoa hồng Affiliate — 14 ngày gần nhất. Bấm «Cập nhật số liệu» hoặc bật tự cào khi mở app bên dưới."
            };

            var pnlRevenueOptions = new Panel
            {
                Name = "pnlRevenueOptions",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(8, 2, 8, 0)
            };
            chkAutoFetchAffiliateRevenueOnStartup = new CheckBox
            {
                Name = "chkAutoFetchAffiliateRevenueOnStartup",
                Text = "T\u1ef1 c\u00e0o doanh thu Affiliate khi m\u1edf app (t\u1ed1i \u0111a 1 l\u1ea7n/ng\u00e0y)",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = false,
                Location = new Point(8, 4)
            };
            pnlRevenueOptions.Controls.Add(chkAutoFetchAffiliateRevenueOnStartup);

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 6, 8, 4)
            };

            btnFetchRevenue = new Button
            {
                Name = "btnFetchRevenue",
                Text = "Cập nhật số liệu",
                Size = new Size(160, 32),
                Location = new Point(8, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 110, 245),
                ForeColor = Color.White
            };
            btnFetchRevenue.FlatAppearance.BorderSize = 0;
            btnFetchRevenue.Click += btnFetchRevenue_Click;
            pnlTop.Controls.Add(btnFetchRevenue);

            chartRevenueCommission = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Top,
                Height = 280,
                BackColor = Color.FromArgb(20, 22, 28)
            };

            dgvRevenueReport = new DataGridView
            {
                Name = "dgvRevenueReport",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                DefaultCellStyle =
                {
                    BackColor = Color.FromArgb(31, 34, 42),
                    ForeColor = Color.Gainsboro,
                    SelectionBackColor = Color.FromArgb(76, 110, 245)
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    WrapMode = DataGridViewTriState.False
                }
            };
            dgvRevenueReport.Columns.Add("colDate", "Ngày");
            dgvRevenueReport.Columns.Add("colOrders", "Đơn");
            dgvRevenueReport.Columns.Add("colRevenue", "Doanh thu");
            dgvRevenueReport.Columns.Add("colCommission", "Hoa hồng");
            dgvRevenueReport.Columns.Add("colStatus", "Trạng thái");
            ApplyAppGridChrome(dgvRevenueReport);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300
            };
            split.Panel1.Controls.Add(chartRevenueCommission);
            split.Panel2.Controls.Add(dgvRevenueReport);
            ApplyAppGridChrome(dgvRevenueReport);

            tabRevenueDashboard.Controls.Add(split);
            tabRevenueDashboard.Controls.Add(pnlTop);
            tabRevenueDashboard.Controls.Add(pnlRevenueOptions);
            tabRevenueDashboard.Controls.Add(lblRevenueSummary);
        }

        private async void btnFetchRevenue_Click(object sender, EventArgs e)
        {
            await RunButtonActionAsync(
                btnFetchRevenue,
                ct => RunRevenueFetchAsync(manual: true, ct)).ConfigureAwait(true);
        }

        private async Task RunRevenueFetchAsync(bool manual, CancellationToken cancellationToken = default)
        {
            if (_revenueFetchRunning)
            {
                Log("[Revenue] Đang cào — bỏ qua yêu cầu trùng.");
                return;
            }

            _revenueFetchRunning = true;
            _revenueCancellation?.Dispose();
            _revenueCancellation = RegisterActiveJobCancellation();
            var token = _revenueCancellation.Token;
            if (cancellationToken.CanBeCanceled)
            {
                token = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken).Token;
            }

            try
            {
                var profile = GetRunningProfileName() ?? "default";
                await _affiliateRevenueService
                    .FetchRevenueReportAsync(profile, Log, token)
                    .ConfigureAwait(true);
                await RefreshRevenueDashboardUiAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                Log("[Revenue] Đã hủy cào báo cáo (Stop).");
            }
            catch (Exception ex)
            {
                Log("[Revenue][ERROR] " + ex.Message);
            }
            finally
            {
                _revenueFetchRunning = false;
                _revenueCancellation = null;
                DisposeActiveJobCancellation();
            }
        }

        private async Task RefreshRevenueDashboardUiAsync()
        {
            if (tabRevenueDashboard == null || tabRevenueDashboard.IsDisposed)
            {
                return;
            }

            var file = await _affiliateRevenueService.LoadStoredReportAsync().ConfigureAwait(true);
            var items = file?.Items ?? new System.Collections.Generic.List<AffiliateRevenueItem>();
            var series14 = AffiliateRevenueStore.GetLast14DaysSeries(items);

            if (lblRevenueSummary != null)
            {
                var last = file?.LastFetchedUtc.HasValue == true
                    ? file.LastFetchedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "chưa cào";
                lblRevenueSummary.Text =
                    $"Hoa hồng Affiliate (14 ngày) — {items.Count} bản ghi — cập nhật: {last} — profile: {file?.ProfileName ?? "—"}";
            }

            BindRevenueChart(series14);
            BindRevenueGrid(items);
        }

        private void BindRevenueChart(System.Collections.Generic.List<AffiliateRevenueItem> series14)
        {
            if (chartRevenueCommission == null)
            {
                return;
            }

            var values = new ChartValues<double>();
            var labels = new System.Collections.Generic.List<string>();
            foreach (var row in series14.OrderBy(x => x.Date))
            {
                values.Add(row.Commission);
                labels.Add(row.Date.ToString("dd/MM"));
            }

            chartRevenueCommission.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Hoa hồng",
                    Values = values,
                    PointGeometrySize = 8,
                    StrokeThickness = 2,
                    Fill = System.Windows.Media.Brushes.Transparent
                }
            };

            chartRevenueCommission.AxisX.Add(new Axis
            {
                Title = "Ngày",
                Labels = labels,
                Foreground = System.Windows.Media.Brushes.Gainsboro
            });
            chartRevenueCommission.AxisY.Add(new Axis
            {
                Title = "Commission (VND)",
                LabelFormatter = v => v.ToString("N0", CultureInfo.InvariantCulture),
                Foreground = System.Windows.Media.Brushes.Gainsboro
            });
        }

        private void BindRevenueGrid(System.Collections.Generic.List<AffiliateRevenueItem> items)
        {
            if (dgvRevenueReport == null)
            {
                return;
            }

            dgvRevenueReport.Rows.Clear();
            foreach (var item in items.OrderByDescending(x => x.Date))
            {
                dgvRevenueReport.Rows.Add(
                    item.Date.ToString("yyyy-MM-dd"),
                    item.OrderCount,
                    item.Revenue.ToString("N0", CultureInfo.InvariantCulture),
                    item.Commission.ToString("N0", CultureInfo.InvariantCulture),
                    item.Status ?? string.Empty);
            }

            if (dgvRevenueReport.Rows.Count == 0)
            {
                dgvRevenueReport.Rows.Add("(chưa có dữ liệu)", 0, 0, 0, "—");
            }

            EnsureAppGridRowHeights(dgvRevenueReport);
        }

        private async Task TryDailyAffiliateRevenueFetchOnStartupAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);

                if (!(settings.AutoFetchAffiliateRevenueOnStartup ?? false))
                {
                    Log("[Revenue] Bỏ qua auto-fetch lúc mở app (bật trên tab Doanh thu nếu cần).");
                    await RefreshRevenueDashboardUiAsync().ConfigureAwait(true);
                    return;
                }

                if (!AffiliateRevenueService.ShouldRunDailyFetch(settings.LastAffiliateRevenueFetchDate))
                {
                    Log("[Revenue] Đã cào báo cáo hôm nay — bỏ qua auto-fetch.");
                    await RefreshRevenueDashboardUiAsync().ConfigureAwait(true);
                    return;
                }

                Log("[Revenue] Auto-fetch 1 lần/ngày khi mở app — sẽ mở Chrome (affiliate.tiktok.com)…");
                await RunRevenueFetchAsync(manual: false, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("[Revenue][ERROR] Auto-fetch lỗi: " + ex.Message);
            }
        }
    }
}
