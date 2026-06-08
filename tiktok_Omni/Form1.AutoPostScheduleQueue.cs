using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private DataGridView dgvAutoPostSchedule;
        private BindingList<AutoPostScheduleUiItem> _autoPostScheduleBindingList;
        private Button btnAddAutoPostSchedule;
        private Button btnRunAutoPostScheduleQueue;
        private Button btnRemoveAutoPostScheduleRow;
        private Label lblAutoPostScheduleStatus;
        private readonly System.Windows.Forms.Timer _autoPostScheduleTimer = new System.Windows.Forms.Timer();
        private bool _isAutoPostScheduleQueueRunning;
        private CancellationTokenSource _autoPostScheduleCts;

        private void InitializeAutoPostScheduleQueue()
        {
            _autoPostScheduleTimer.Interval = 15000;
            _autoPostScheduleTimer.Tick += AutoPostScheduleTimer_Tick;
            _autoPostScheduleTimer.Start();
        }

        private Panel BuildAutoPostSchedulePanel()
        {
            var pnl = new Panel
            {
                Name = "pnlAutoPostSchedule",
                Dock = DockStyle.Fill,
                AutoSize = false,
                MinimumSize = new Size(0, 120),
                BackColor = Color.FromArgb(31, 34, 42),
                Padding = new Padding(8, 4, 8, 4)
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = pnl.BackColor
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var flpToolbar = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = pnl.BackColor,
                Padding = new Padding(0, 0, 0, 4)
            };

            btnAddAutoPostSchedule = CreateAutoPostJellyButton(
                "btnAddAutoPostSchedule",
                "Thêm vào lịch đăng",
                AutoPostTintStart);
            btnAddAutoPostSchedule.Click += btnAddAutoPostSchedule_Click;

            btnRunAutoPostScheduleQueue = CreateAutoPostJellyButton(
                "btnRunAutoPostScheduleQueue",
                "Chạy lịch đăng",
                AutoPostTintStart);
            btnRunAutoPostScheduleQueue.Click += btnRunAutoPostScheduleQueue_Click;

            btnRemoveAutoPostScheduleRow = CreateAutoPostJellyButton(
                "btnRemoveAutoPostScheduleRow",
                "Xóa dòng",
                AutoPostTintCloseBrowser);
            btnRemoveAutoPostScheduleRow.Click += btnRemoveAutoPostScheduleRow_Click;

            lblAutoPostScheduleStatus = new Label
            {
                Name = "lblAutoPostScheduleStatus",
                AutoSize = true,
                ForeColor = Color.FromArgb(170, 178, 195),
                Font = WarmupLabelFont,
                Margin = new Padding(12, 10, 0, 4),
                Text = "Lịch đăng: 0 dòng"
            };

            flpToolbar.Controls.Add(btnAddAutoPostSchedule);
            flpToolbar.Controls.Add(btnRunAutoPostScheduleQueue);
            flpToolbar.Controls.Add(btnRemoveAutoPostScheduleRow);
            flpToolbar.Controls.Add(lblAutoPostScheduleStatus);

            ConfigureAutoPostScheduleGrid();
            dgvAutoPostSchedule.Dock = DockStyle.Fill;

            tbl.Controls.Add(flpToolbar, 0, 0);
            tbl.Controls.Add(dgvAutoPostSchedule, 0, 1);
            pnl.Controls.Add(tbl);
            RefreshAutoPostScheduleStatus();
            return pnl;
        }

        private void ConfigureAutoPostScheduleGrid()
        {
            _autoPostScheduleBindingList = new BindingList<AutoPostScheduleUiItem>();
            dgvAutoPostSchedule = new DataGridView
            {
                Name = "dgvAutoPostSchedule",
                MinimumSize = new Size(240, 120),
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
                DataSource = _autoPostScheduleBindingList
            };
            dgvAutoPostSchedule.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = WarmupFieldFont,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            dgvAutoPostSchedule.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = AppGridHeaderFont,
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 8, 6, 8),
                WrapMode = DataGridViewTriState.False
            };
            dgvAutoPostSchedule.ColumnHeadersHeight = AppGridHeaderHeight;
            dgvAutoPostSchedule.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvAutoPostSchedule.EnableHeadersVisualStyles = false;
            dgvAutoPostSchedule.RowTemplate.Height = 34;

            dgvAutoPostSchedule.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Profile",
                HeaderText = "Profile",
                FillWeight = 14,
                MinimumWidth = 80,
                ReadOnly = true
            });
            dgvAutoPostSchedule.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Platform",
                HeaderText = "Nền tảng",
                FillWeight = 12,
                MinimumWidth = 72,
                ReadOnly = true
            });
            dgvAutoPostSchedule.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ScheduledAtLabel",
                HeaderText = "Giờ đăng",
                FillWeight = 16,
                MinimumWidth = 110,
                ReadOnly = false
            });
            dgvAutoPostSchedule.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "VideoLabel",
                HeaderText = "Video",
                FillWeight = 22,
                MinimumWidth = 120,
                ReadOnly = true
            });
            dgvAutoPostSchedule.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText = "Trạng thái",
                FillWeight = 12,
                MinimumWidth = 88,
                ReadOnly = true
            });
            dgvAutoPostSchedule.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CaptionPreview",
                HeaderText = "Caption / tiêu đề",
                FillWeight = 24,
                MinimumWidth = 140,
                ReadOnly = true
            });
            dgvAutoPostSchedule.CellEndEdit += dgvAutoPostSchedule_CellEndEdit;
        }

        private void btnAddAutoPostSchedule_Click(object sender, EventArgs e)
        {
            if (!TryBuildOmnichannelAutoPostPlan(out var plan, out var error))
            {
                Log("[Lịch đăng] " + error);
                MessageBox.Show(this, error, "Thêm vào lịch đăng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var added = 0;
            if (plan.PostTikTok)
            {
                if (TryAddAutoPostScheduleRow(plan, "TikTok", dtpAutoPostTikTok?.Value, out var err))
                {
                    added++;
                }
                else if (!string.IsNullOrWhiteSpace(err))
                {
                    Log("[Lịch đăng] TikTok: " + err);
                }
            }

            if (plan.PostFacebook)
            {
                if (TryAddAutoPostScheduleRow(plan, "Facebook", dtpAutoPostFacebook?.Value, out var err))
                {
                    added++;
                }
                else if (!string.IsNullOrWhiteSpace(err))
                {
                    Log("[Lịch đăng] Facebook: " + err);
                }
            }

            if (plan.PostYouTube)
            {
                if (TryAddAutoPostScheduleRow(plan, "YouTube", dtpAutoPostYouTube?.Value, out var err))
                {
                    added++;
                }
                else if (!string.IsNullOrWhiteSpace(err))
                {
                    Log("[Lịch đăng] YouTube: " + err);
                }
            }

            if (added == 0)
            {
                MessageBox.Show(
                    this,
                    "Không thêm được dòng nào. Kiểm tra kênh đã bật và giờ đăng.",
                    "Thêm vào lịch đăng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RefreshAutoPostScheduleStatus();
            dgvAutoPostSchedule?.Refresh();
            Log($"[Lịch đăng] Đã thêm {added} dòng (profile «{plan.Profile}»).");
        }

        private bool TryAddAutoPostScheduleRow(
            OmnichannelAutoPostPlan plan,
            string platform,
            DateTime? schedulePickerValue,
            out string error)
        {
            error = string.Empty;
            if (plan == null || string.IsNullOrWhiteSpace(platform))
            {
                error = "Thiếu dữ liệu kế hoạch đăng.";
                return false;
            }

            DateTime? scheduledAt = null;
            if (schedulePickerValue.HasValue)
            {
                var picked = schedulePickerValue.Value;
                if (picked > DateTime.Now.AddMinutes(1))
                {
                    scheduledAt = picked;
                }
            }

            var payload = BuildAutoPostJobPayloadFromPlan(plan, platform);
            if (payload == null)
            {
                error = "Không tạo được payload.";
                return false;
            }

            var videoLabel = string.IsNullOrWhiteSpace(payload.VideoFilePath)
                ? "(thư mục)"
                : System.IO.Path.GetFileName(payload.VideoFilePath);

            _autoPostScheduleBindingList.Add(new AutoPostScheduleUiItem
            {
                Profile = payload.Profile,
                Platform = platform,
                ScheduledAtLocal = scheduledAt,
                VideoLabel = videoLabel,
                Status = scheduledAt.HasValue ? "Scheduled" : "Pending",
                CaptionPreview = BuildAutoPostScheduleCaptionPreview(payload, platform),
                PayloadJson = JsonConvert.SerializeObject(payload),
                CreatedAtUtc = DateTime.UtcNow
            });

            return true;
        }

        private static string BuildAutoPostScheduleCaptionPreview(AutoPostJobPayload payload, string platform)
        {
            if (payload == null)
            {
                return string.Empty;
            }

            switch (platform)
            {
                case "TikTok":
                    var cap = (payload.TikTokCaption ?? string.Empty).Trim();
                    return cap.Length > 80 ? cap.Substring(0, 77) + "..." : cap;
                case "Facebook":
                    var fb = (payload.FacebookCaption ?? string.Empty).Trim();
                    return fb.Length > 80 ? fb.Substring(0, 77) + "..." : fb;
                case "YouTube":
                    return (payload.YouTubeTitle ?? string.Empty).Trim();
                default:
                    return string.Empty;
            }
        }

        private AutoPostJobPayload BuildAutoPostJobPayloadFromPlan(OmnichannelAutoPostPlan plan, string platform)
        {
            if (plan == null)
            {
                return null;
            }

            return new AutoPostJobPayload
            {
                VideoFolder = plan.VideoFolder,
                VideoFilePath = plan.VideoFilePath,
                Profile = plan.Profile,
                PostTikTok = string.Equals(platform, "TikTok", StringComparison.OrdinalIgnoreCase),
                PostFacebook = string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase),
                PostYouTube = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase),
                TikTokCaption = plan.TikTokCaption,
                TikTokHashtags = plan.TikTokHashtags,
                TikTokUploadOnly = plan.TikTokUploadOnly,
                FacebookCaption = plan.FacebookCaption,
                FacebookHashtags = plan.FacebookHashtags,
                FacebookAttachShopeeLink = plan.FacebookAttachShopeeLink,
                FacebookShopeeLink = plan.FacebookShopeeLink,
                YouTubeTitle = plan.YouTubeTitle,
                YouTubeDescription = plan.YouTubeDescription,
                PostFingerprint = plan.PostFingerprint + "|platform=" + platform,
                CombinedCaptionPreview = plan.CombinedCaptionPreview,
                StorageRootPath = _storageRootPathCache ?? string.Empty,
                AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                ProductId = OmnichannelAutoPostFields.NormalizeLink(plan.ProductId)
            };
        }

        private void btnRemoveAutoPostScheduleRow_Click(object sender, EventArgs e)
        {
            if (dgvAutoPostSchedule?.SelectedRows == null ||
                dgvAutoPostSchedule.SelectedRows.Count == 0 ||
                _autoPostScheduleBindingList == null)
            {
                return;
            }

            var selected = dgvAutoPostSchedule.SelectedRows[0]?.DataBoundItem as AutoPostScheduleUiItem;
            if (selected == null)
            {
                return;
            }

            if (string.Equals(selected.Status, "Running", StringComparison.OrdinalIgnoreCase))
            {
                Log("[Lịch đăng] Không xóa dòng đang chạy.");
                return;
            }

            _autoPostScheduleBindingList.Remove(selected);
            RefreshAutoPostScheduleStatus();
        }

        private async void btnRunAutoPostScheduleQueue_Click(object sender, EventArgs e)
        {
            if (_isAutoPostScheduleQueueRunning)
            {
                Log("[Lịch đăng] Đang chạy hàng đợi lịch đăng.");
                return;
            }

            PromoteDueAutoPostScheduleRows();
            var runnable = GetRunnableAutoPostScheduleRows().ToList();
            if (runnable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Không có dòng Pending hoặc đã đến giờ (Scheduled). Thêm lịch hoặc đợi đến giờ hẹn.",
                    "Chạy lịch đăng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _isAutoPostScheduleQueueRunning = true;
            _autoPostScheduleCts?.Dispose();
            _autoPostScheduleCts = new CancellationTokenSource();
            SetAutoPostScheduleButtonsEnabled(false);

            try
            {
                Log($"[Lịch đăng] Bắt đầu {runnable.Count} job đăng theo lịch…");
                foreach (var row in runnable)
                {
                    if (_autoPostScheduleCts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    await RunAutoPostScheduleRowAsync(row, _autoPostScheduleCts.Token).ConfigureAwait(true);
                }

                Log("[Lịch đăng] Hoàn tất lượt chạy hàng đợi.");
            }
            finally
            {
                _isAutoPostScheduleQueueRunning = false;
                SetAutoPostScheduleButtonsEnabled(true);
                RefreshAutoPostScheduleStatus();
                dgvAutoPostSchedule?.Refresh();
            }
        }

        private async Task RunAutoPostScheduleRowAsync(AutoPostScheduleUiItem row, CancellationToken cancellationToken)
        {
            if (row == null)
            {
                return;
            }

            row.Status = "Running";
            dgvAutoPostSchedule?.Refresh();

            try
            {
                var payload = JsonConvert.DeserializeObject<AutoPostJobPayload>(row.PayloadJson ?? "{}")
                              ?? new AutoPostJobPayload();
                payload.StorageRootPath = _storageRootPathCache ?? payload.StorageRootPath ?? string.Empty;

                var plan = OmnichannelAutoPostPlanFromPayload(payload);
                var summary = await RunOmnichannelAutoPostSequenceAsync(plan, cancellationToken).ConfigureAwait(true);
                row.Status = "Done";
                row.LastError = string.Empty;
                Log($"[Lịch đăng] ✓ {row.Platform} «{row.Profile}»: {summary}");
            }
            catch (OperationCanceledException)
            {
                row.Status = "Pending";
                row.LastError = "Đã hủy";
                Log($"[Lịch đăng] ⊗ Hủy {row.Platform} «{row.Profile}»");
            }
            catch (Exception ex)
            {
                row.Status = "Failed";
                row.LastError = ex.Message;
                Log($"[Lịch đăng] ✗ {row.Platform} «{row.Profile}»: {ex.Message}");
            }

            dgvAutoPostSchedule?.Refresh();
        }

        private static OmnichannelAutoPostPlan OmnichannelAutoPostPlanFromPayload(AutoPostJobPayload payload)
        {
            return new OmnichannelAutoPostPlan
            {
                VideoFolder = payload.VideoFolder ?? string.Empty,
                VideoFilePath = payload.VideoFilePath ?? string.Empty,
                Profile = payload.Profile ?? string.Empty,
                PostTikTok = payload.PostTikTok,
                PostFacebook = payload.PostFacebook,
                PostYouTube = payload.PostYouTube,
                TikTokCaption = payload.TikTokCaption ?? string.Empty,
                TikTokHashtags = payload.TikTokHashtags ?? string.Empty,
                TikTokUploadOnly = payload.TikTokUploadOnly,
                FacebookCaption = payload.FacebookCaption ?? string.Empty,
                FacebookHashtags = payload.FacebookHashtags ?? string.Empty,
                FacebookAttachShopeeLink = payload.FacebookAttachShopeeLink,
                FacebookShopeeLink = payload.FacebookShopeeLink ?? string.Empty,
                YouTubeTitle = payload.YouTubeTitle ?? string.Empty,
                YouTubeDescription = payload.YouTubeDescription ?? string.Empty,
                CombinedCaptionPreview = payload.CombinedCaptionPreview ?? string.Empty,
                PostFingerprint = payload.PostFingerprint ?? string.Empty,
                AffiliateLink = payload.AffiliateLink,
                ProductId = payload.ProductId
            };
        }

        private void AutoPostScheduleTimer_Tick(object sender, EventArgs e)
        {
            if (_autoPostScheduleBindingList == null || _autoPostScheduleBindingList.Count == 0)
            {
                return;
            }

            var promoted = PromoteDueAutoPostScheduleRows();
            if (!promoted)
            {
                return;
            }

            RefreshAutoPostScheduleStatus();
            dgvAutoPostSchedule?.Refresh();
            Log("[Lịch đăng] Đã đến giờ — chuyển Scheduled → Pending.");

            if (!_isAutoPostScheduleQueueRunning && GetRunnableAutoPostScheduleRows().Any())
            {
                btnRunAutoPostScheduleQueue?.PerformClick();
            }
        }

        private bool PromoteDueAutoPostScheduleRows()
        {
            if (_autoPostScheduleBindingList == null)
            {
                return false;
            }

            var now = DateTime.Now;
            var promoted = false;
            foreach (var item in _autoPostScheduleBindingList)
            {
                if (item == null ||
                    !string.Equals(item.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                    !item.ScheduledAtLocal.HasValue ||
                    item.ScheduledAtLocal.Value > now)
                {
                    continue;
                }

                item.Status = "Pending";
                promoted = true;
            }

            return promoted;
        }

        private IEnumerable<AutoPostScheduleUiItem> GetRunnableAutoPostScheduleRows()
        {
            if (_autoPostScheduleBindingList == null)
            {
                yield break;
            }

            foreach (var item in _autoPostScheduleBindingList)
            {
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    yield return item;
                }
            }
        }

        private void RefreshAutoPostScheduleStatus()
        {
            if (lblAutoPostScheduleStatus == null || lblAutoPostScheduleStatus.IsDisposed)
            {
                return;
            }

            if (lblAutoPostScheduleStatus.InvokeRequired)
            {
                lblAutoPostScheduleStatus.Invoke(new Action(RefreshAutoPostScheduleStatus));
                return;
            }

            var total = _autoPostScheduleBindingList?.Count ?? 0;
            var scheduled = 0;
            var pending = 0;
            if (_autoPostScheduleBindingList != null)
            {
                foreach (var row in _autoPostScheduleBindingList)
                {
                    if (row == null)
                    {
                        continue;
                    }

                    if (string.Equals(row.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
                    {
                        scheduled++;
                    }
                    else if (string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        pending++;
                    }
                }
            }

            lblAutoPostScheduleStatus.Text =
                $"Lịch đăng: {total} dòng · Scheduled {scheduled} · Pending {pending}";
        }

        private void SetAutoPostScheduleButtonsEnabled(bool enabled)
        {
            if (btnAddAutoPostSchedule != null)
            {
                btnAddAutoPostSchedule.Enabled = enabled;
            }

            if (btnRunAutoPostScheduleQueue != null)
            {
                btnRunAutoPostScheduleQueue.Enabled = enabled;
            }

            if (btnRemoveAutoPostScheduleRow != null)
            {
                btnRemoveAutoPostScheduleRow.Enabled = enabled;
            }
        }

        private void dgvAutoPostSchedule_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvAutoPostSchedule == null)
            {
                return;
            }

            var column = dgvAutoPostSchedule.Columns[e.ColumnIndex];
            if (column == null ||
                !string.Equals(column.DataPropertyName, "ScheduledAtLabel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var row = dgvAutoPostSchedule.Rows[e.RowIndex]?.DataBoundItem as AutoPostScheduleUiItem;
            if (row == null)
            {
                return;
            }

            var raw = dgvAutoPostSchedule.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw) ||
                string.Equals(raw, "Đăng ngay", StringComparison.OrdinalIgnoreCase))
            {
                row.ScheduledAtLocal = null;
                row.Status = string.Equals(row.Status, "Done", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(row.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(row.Status, "Running", StringComparison.OrdinalIgnoreCase)
                    ? row.Status
                    : "Pending";
            }
            else if (DateTime.TryParse(raw, out var parsed))
            {
                row.ScheduledAtLocal = parsed;
                row.Status = parsed > DateTime.Now.AddMinutes(1) ? "Scheduled" : "Pending";
            }

            dgvAutoPostSchedule.Refresh();
            RefreshAutoPostScheduleStatus();
        }

        private sealed class AutoPostScheduleUiItem
        {
            public string Profile { get; set; } = string.Empty;
            public string Platform { get; set; } = string.Empty;
            public DateTime? ScheduledAtLocal { get; set; }
            public string ScheduledAtLabel =>
                ScheduledAtLocal.HasValue
                    ? ScheduledAtLocal.Value.ToString("dd/MM/yyyy HH:mm")
                    : "Đăng ngay";
            public string VideoLabel { get; set; } = string.Empty;
            public string Status { get; set; } = "Pending";
            public string CaptionPreview { get; set; } = string.Empty;
            public string PayloadJson { get; set; } = string.Empty;
            public string LastError { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        }
    }
}
