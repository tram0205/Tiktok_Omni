using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        // ── 3 independent platform grids ─────────────────────────────────────────
        private DataGridView dgvTikTokSchedule;
        private DataGridView dgvFacebookSchedule;
        private DataGridView dgvYouTubeSchedule;

        private BindingList<ScheduleEntry> _tikTokList;
        private BindingList<ScheduleEntry> _facebookList;
        private BindingList<ScheduleEntry> _youTubeList;

        private Label lblTikTokScheduleStatus;
        private Label lblFacebookScheduleStatus;
        private Label lblYouTubeScheduleStatus;

        private ScheduleManager _scheduleManager;

        // Shared timer (scans all 3 grids every 15 s)
        private readonly System.Windows.Forms.Timer _autoPostScheduleTimer = new System.Windows.Forms.Timer();
        private bool _isAutoPostScheduleQueueRunning;
        private bool _isAutoPostQueueActive = false;
        private bool _isAutoPostPaused = false;
        private CancellationTokenSource _autoPostScheduleCts;

        // Pause/Resume buttons (one per platform toolbar) — updated together
        private readonly System.Collections.Generic.List<Button> _pauseButtons
            = new System.Collections.Generic.List<Button>();

        /// <summary>Maps OmniJob.Id → (ScheduleEntry, DataGridView) for status sync-back.</summary>
        private readonly ConcurrentDictionary<Guid, (ScheduleEntry entry, DataGridView dgv)>
            _scheduleJobMap = new ConcurrentDictionary<Guid, (ScheduleEntry, DataGridView)>();

        /// <summary>Number of schedule-originated jobs still pending/running in the global queue.</summary>
        private int _pendingScheduleJobCount;

        internal void RefreshPlatformScheduleGrids()
        {
            dgvTikTokSchedule?.Refresh();
            dgvFacebookSchedule?.Refresh();
            dgvYouTubeSchedule?.Refresh();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  INIT
        // ─────────────────────────────────────────────────────────────────────────

        private void InitializeAutoPostScheduleQueue()
        {
            _scheduleManager = new ScheduleManager();
            _tikTokList   = new BindingList<ScheduleEntry>();
            _facebookList = new BindingList<ScheduleEntry>();
            _youTubeList  = new BindingList<ScheduleEntry>();

            // BuildAutoPostUi() ran before this method (inside InitializeComponent),
            // so the grids already exist but had DataSource = null.
            // Re-bind them now that the BindingLists are ready.
            if (dgvTikTokSchedule   != null) dgvTikTokSchedule.DataSource   = _tikTokList;
            if (dgvFacebookSchedule != null) dgvFacebookSchedule.DataSource = _facebookList;
            if (dgvYouTubeSchedule  != null) dgvYouTubeSchedule.DataSource  = _youTubeList;

            _autoPostScheduleTimer.Interval = 15_000;
            _autoPostScheduleTimer.Tick += AutoPostScheduleTimer_Tick;
            _autoPostScheduleTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  BUILD PLATFORM TAB CONTENT  (called from Form1.AutoPostUi.cs)
        // ─────────────────────────────────────────────────────────────────────────

        internal Panel BuildPlatformScheduleContent(string platform, Color accentColor, Color surfaceColor)
        {
            var list = GetPlatformList(platform);
            var dgv  = CreatePlatformGrid(platform, list, accentColor);
            StorePlatformGrid(platform, dgv);

            var toolbar = BuildPlatformToolbar(platform, accentColor, surfaceColor, dgv, list);
            toolbar.Dock = DockStyle.Top;

            // Per-platform inline log panel
            var rtbPlatLog = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.FromArgb(18, 20, 26),
                ForeColor   = Color.FromArgb(190, 195, 205),
                Font        = new System.Drawing.Font("Consolas", 8f),
                ReadOnly    = true,
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                Margin      = Padding.Empty,
            };
            StorePlatformLog(platform, rtbPlatLog);

            var btnClear = new Button
            {
                Text      = "✕ Clear",
                Dock      = DockStyle.Right,
                Width     = 72,
                Height    = 22,
                FlatStyle = FlatStyle.Flat,
                Font      = new System.Drawing.Font("Segoe UI", 7.5f),
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.FromArgb(160, 165, 175),
                Cursor    = Cursors.Hand,
            };
            btnClear.FlatAppearance.BorderColor = Color.FromArgb(60, 64, 76);
            btnClear.Click += (_, __) => { if (!rtbPlatLog.IsDisposed) rtbPlatLog.Clear(); };

            var logStrip = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                Padding   = Padding.Empty,
            };
            logStrip.Controls.Add(rtbPlatLog);
            logStrip.Controls.Add(btnClear);

            // 3-row layout: toolbar | grid | per-platform log
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = surfaceColor,
                Margin      = Padding.Empty,
                Padding     = new Padding(4),
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // toolbar
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));     // grid
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));     // per-platform log strip
            tbl.Controls.Add(toolbar,  0, 0);
            tbl.Controls.Add(dgv,      0, 1);
            tbl.Controls.Add(logStrip, 0, 2);

            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = surfaceColor, Padding = Padding.Empty };
            pnl.Controls.Add(tbl);
            return pnl;
        }

        /// <summary>Routes the per-platform RichTextBox reference to the matching field.</summary>
        private void StorePlatformLog(string platform, RichTextBox rtb)
        {
            switch (platform)
            {
                case "TikTok":   rtbLogTikTok = rtb; break;
                case "Facebook": rtbLogFb     = rtb; break;
                case "YouTube":  rtbLogYt     = rtb; break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  GRID CONFIGURATION
        // ─────────────────────────────────────────────────────────────────────────

        private DataGridView CreatePlatformGrid(
            string platform,
            BindingList<ScheduleEntry> list,
            Color accentColor)
        {
            var dgv = new DataGridView
            {
                Name = "dgv" + platform + "Schedule",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,          // bulk select
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(50, 54, 66),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DataSource = list,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = WarmupFieldFont,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = accentColor,
                SelectionForeColor = Color.White
            };
            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = AppGridHeaderFont,
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0),
                WrapMode = DataGridViewTriState.False
            };
            dgv.ColumnHeadersHeight = AppGridHeaderHeight;
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowTemplate.Height = 34;

            // Video — clickable to open player
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colVideoLabel",
                DataPropertyName = "VideoLabel",
                HeaderText       = "Video  (bấm để xem)",
                FillWeight       = 20,
                MinimumWidth     = 100,
                ReadOnly         = true
            });

            // Profile — dropdown populated by RefreshAllProfileSelectors()
            var profileCol = new DataGridViewComboBoxColumn
            {
                Name             = "colAutoPostProfile",
                DataPropertyName = "Profile",
                HeaderText       = "Profile",
                FillWeight       = 14,
                MinimumWidth     = 90,
                DisplayStyle     = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle        = FlatStyle.Flat,
                // Items populated later via ApplyGridProfileComboColumn
            };
            dgv.Columns.Add(profileCol);

            // Suppress DataError when a stored profile name isn't in the combo list yet
            dgv.DataError += (s, ex) =>
            {
                if (ex.ColumnIndex >= 0 &&
                    dgv.Columns[ex.ColumnIndex]?.Name == "colAutoPostProfile")
                    ex.ThrowException = false;
            };
            // Giờ đăng — bấm ô để mở form chọn ngày/giờ
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colScheduledAt",
                DataPropertyName = "ScheduledAtText",
                HeaderText       = "Giờ đăng",
                FillWeight       = 15,
                MinimumWidth     = 130,
                ReadOnly         = true,
                ToolTipText      = "Bấm để chọn ngày / giờ đăng (dd/MM/yyyy HH:mm) hoặc Đăng ngay"
            });
            // Link (editable)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Link",
                HeaderText = "Link (Affiliate/Shopee)",
                FillWeight = 18,
                MinimumWidth = 100,
                ReadOnly = false
            });

            var isYouTube = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase);

            // YouTube: title preview (clicking also opens style selector)
            if (isYouTube)
            {
                dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name             = "colYtTitlePreview",
                    DataPropertyName = "YtTitlePreview",
                    HeaderText       = "Tiêu đề  (bấm để chọn)",
                    FillWeight       = 14,
                    MinimumWidth     = 90,
                    ReadOnly         = true
                });
            }

            // Caption column — single-click opens FormCaptionSelector when empty;
            // double-click / F2 allows direct inline editing at any time
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colCaptionStatus",
                DataPropertyName = "Caption",
                HeaderText       = "Caption  (bấm chọn phong cách / F2 sửa trực tiếp)",
                FillWeight       = 20,
                MinimumWidth     = 140,
                ReadOnly         = false
            });

            // Hashtag column — clickable cell opens FormHashtagEditor
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colHashtagPreview",
                DataPropertyName = "HashtagPreview",
                HeaderText       = "Hashtag  (bấm để sửa)",
                FillWeight       = 16,
                MinimumWidth     = 100,
                ReadOnly         = true
            });


            // Status
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText       = "Trạng thái",
                FillWeight       = 10,
                MinimumWidth     = 80,
                ReadOnly         = true
            });

            // LastError — only visible when non-empty; shows inline in grid
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colLastError",
                DataPropertyName = "LastError",
                HeaderText       = "Chi tiết lỗi",
                FillWeight       = 18,
                MinimumWidth     = 100,
                ReadOnly         = true
            });

            // Commit profile combo changes immediately when user picks a value
            dgv.CurrentCellDirtyStateChanged += (s, _) =>
            {
                var g = s as DataGridView;
                if (g?.CurrentCell is DataGridViewComboBoxCell)
                    g.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // Persist profile change to disk after commit
            dgv.CellValueChanged += (s, e2) =>
            {
                if (e2.ColumnIndex >= 0 &&
                    (s as DataGridView)?.Columns[e2.ColumnIndex]?.Name == "colAutoPostProfile")
                {
                    try
                    {
                        _scheduleManager?.Save(new ScheduleData
                        {
                            TikTok   = new System.Collections.Generic.List<ScheduleEntry>(_tikTokList),
                            Facebook = new System.Collections.Generic.List<ScheduleEntry>(_facebookList),
                            YouTube  = new System.Collections.Generic.List<ScheduleEntry>(_youTubeList),
                        });
                    }
                    catch { /* non-critical */ }
                }
            };

            // Wire CellClick to open popup editor when clickable columns are clicked
            dgv.CellClick += PlatformGrid_CellClick;

            // Show hand cursor over clickable text columns
            var clickableCols = new HashSet<string>
                { "colVideoLabel", "colCaptionStatus", "colYtTitlePreview", "colHashtagPreview", "colScheduledAt" };
            dgv.CellMouseEnter += (s, ev) =>
            {
                if (ev.RowIndex >= 0 && ev.ColumnIndex >= 0 &&
                    clickableCols.Contains(dgv.Columns[ev.ColumnIndex].Name))
                    dgv.Cursor = Cursors.Hand;
            };
            dgv.CellMouseLeave += (s, ev) => dgv.Cursor = Cursors.Default;

            // Tô màu các cột có thể bấm để gợi ý tương tác
            dgv.CellFormatting += (s, ev) =>
            {
                if (ev.RowIndex < 0 || ev.ColumnIndex < 0) return;
                var colName = dgv.Columns[ev.ColumnIndex].Name;
                switch (colName)
                {
                    case "colScheduledAt":
                        ev.CellStyle.ForeColor = Color.FromArgb(120, 200, 230);
                        break;
                    case "colVideoLabel":
                        ev.CellStyle.ForeColor = Color.FromArgb(230, 200, 110);
                        ev.CellStyle.Font      = new Font(dgv.Font, FontStyle.Underline);
                        break;
                    case "colCaptionStatus":
                        // Editable caption cell — subtle highlight to show it's editable
                        ev.CellStyle.ForeColor = Color.FromArgb(220, 220, 200);
                        ev.CellStyle.BackColor = Color.FromArgb(30, 36, 30);
                        ev.CellStyle.SelectionBackColor = Color.FromArgb(50, 70, 50);
                        break;
                    case "colYtTitlePreview":
                    case "colHashtagPreview":
                        ev.CellStyle.ForeColor   = Color.FromArgb(180, 160, 240);
                        ev.CellStyle.Font        = new Font(dgv.Font, FontStyle.Underline);
                        ev.CellStyle.BackColor   = Color.FromArgb(30, 28, 50);
                        ev.CellStyle.SelectionBackColor = Color.FromArgb(56, 45, 80);
                        break;
                    case "colLastError":
                        if (ev.Value != null && !string.IsNullOrWhiteSpace(ev.Value.ToString()))
                        {
                            ev.CellStyle.ForeColor = Color.FromArgb(255, 110, 90);
                            ev.CellStyle.BackColor = Color.FromArgb(40, 20, 18);
                        }
                        break;
                }
            };

            // Delete key removes selected rows (with confirmation when > 1 row selected)
            dgv.KeyDown += PlatformGrid_KeyDown;

            return dgv;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  TOOLBAR
        // ─────────────────────────────────────────────────────────────────────────

        private FlowLayoutPanel BuildPlatformToolbar(
            string platform,
            Color accentColor,
            Color surfaceColor,
            DataGridView dgv,
            BindingList<ScheduleEntry> list)
        {
            var flp = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                BackColor = surfaceColor,
                Padding = new Padding(4, 4, 4, 4)
            };

            // AI button
            var aiLabel = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase)
                ? "Tạo tiêu đề SEO (Gemini)"
                : "Tạo caption (Gemini)";
            var btnAi = CreateAutoPostJellyButton("btnAi" + platform, aiLabel, AutoPostAiAccent);
            // Use GetPlatformList() at click-time to avoid capturing the null list from init
            btnAi.Click += (_, __) => BtnGenCaption_Click(platform, dgv, GetPlatformList(platform));

            // Chạy lịch đăng (this platform only)
            var btnRun = CreateAutoPostJellyButton("btnRun" + platform, "Chạy lịch đăng", AutoPostTintStart);
            btnRun.Click += (_, __) => _ = BtnRunScheduleAsync(platform, dgv);

            // ⏸ Tạm dừng / Tiếp tục (shared pause flag, shown per-platform)
            var btnPause = new Button
            {
                Name      = "btnPause" + platform,
                Text      = "⏸",
                Width     = 34,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                Font      = new System.Drawing.Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(60, 70, 90),
                ForeColor = Color.FromArgb(180, 190, 210),
                Cursor    = Cursors.Hand,
                Enabled   = false,
                Margin    = new Padding(0, 4, 0, 0),
            };
            btnPause.FlatAppearance.BorderColor = Color.FromArgb(80, 95, 120);
            btnPause.Click += (_, __) => TogglePauseAutoPost();
            _pauseButtons.Add(btnPause);

            // Status label
            var lblStatus = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(170, 178, 195),
                Font = WarmupLabelFont,
                Margin = new Padding(10, 12, 0, 0),
                Text = platform + ": 0 dòng"
            };
            SetPlatformStatusLabel(platform, lblStatus);

            flp.Controls.Add(btnAi);
            flp.Controls.Add(btnRun);
            flp.Controls.Add(btnPause);
            flp.Controls.Add(lblStatus);
            return flp;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  ADD VIDEO TO ALL 3 GRIDS
        // ─────────────────────────────────────────────────────────────────────────

        private void BtnAddToAllSchedules_Click(object sender, EventArgs e)
        {
            var videoPath = GetSelectedAutoPostVideoFullPath();
            if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
            {
                var folder = (txtAutoPostFolder?.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(folder) && System.IO.Directory.Exists(folder))
                {
                    var first = EnumerateVideoFilesInFolder(folder).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(first)) videoPath = first;
                }
            }

            if (string.IsNullOrWhiteSpace(videoPath) || !System.IO.File.Exists(videoPath))
            {
                LogAutoPost("[Lịch đăng] Chưa chọn video. Duyệt thư mục và chọn file trước.");
                MessageBox.Show(this,
                    "Chưa chọn video — hãy duyệt thư mục và chọn file trước.",
                    "Thêm vào lịch đăng",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profile = cbAutoPostProfile?.SelectedItem?.ToString() ?? "default";
            var label   = System.IO.Path.GetFileName(videoPath);

            AddVideoToAllGrids(videoPath, label, caption: string.Empty,
                link: string.Empty, profile: profile);

            RefreshAutoPostScheduleStatus();
            LogAutoPost($"[Lịch đăng] Đã thêm «{label}» vào cả 3 lưới. Hãy điền Caption / Link / Giờ đăng trực tiếp.");
        }

        /// <summary>
        /// Adds one row to ALL three platform grids simultaneously.
        /// Caption → TikTok & Facebook; ytTitle/ytDesc → YouTube.
        /// </summary>
        internal void AddVideoToAllGrids(
            string videoPath,
            string videoLabel,
            string caption,
            string link,
            string profile,
            string ytTitle = null,
            string ytDesc  = null,
            DateTime? scheduledAt = null)
        {
            var lbl    = string.IsNullOrWhiteSpace(videoLabel) ? System.IO.Path.GetFileName(videoPath ?? string.Empty) : videoLabel;
            var status = scheduledAt.HasValue && scheduledAt.Value > DateTime.Now.AddMinutes(1) ? "Scheduled" : "Pending";

            var tik = new ScheduleEntry
            {
                VideoFilePath = videoPath ?? string.Empty,
                VideoLabel    = lbl,
                Caption       = caption ?? string.Empty,
                Link          = link ?? string.Empty,
                Profile       = profile ?? "default",
                ScheduledAt   = scheduledAt,
                Status        = status,
                CreatedAtUtc  = DateTime.UtcNow
            };
            var fb  = CloneEntry(tik);
            var yt  = CloneEntry(tik);
            yt.Caption       = string.Empty;
            yt.YtTitle       = ytTitle ?? TruncateTitle(caption, 60);
            yt.YtDescription = ytDesc  ?? caption ?? string.Empty;

            _tikTokList?.Add(tik);
            _facebookList?.Add(fb);
            _youTubeList?.Add(yt);

            RefreshAutoPostScheduleStatus();
        }

        private static string TruncateTitle(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max).TrimEnd();
        }

        private static ScheduleEntry CloneEntry(ScheduleEntry src) => new ScheduleEntry
        {
            VideoFilePath = src.VideoFilePath,
            VideoLabel    = src.VideoLabel,
            Caption       = src.Caption,
            YtTitle       = src.YtTitle,
            YtDescription = src.YtDescription,
            Link          = src.Link,
            Profile       = src.Profile,
            ScheduledAt   = src.ScheduledAt,
            Status        = src.Status,
            CreatedAtUtc  = src.CreatedAtUtc
        };

        // ─────────────────────────────────────────────────────────────────────────
        //  BACKWARD COMPAT: TryAddAutoPostScheduleRow
        //  (called from Form1.VideoReupAutoPost.cs)
        // ─────────────────────────────────────────────────────────────────────────

        private bool TryAddAutoPostScheduleRow(
            OmnichannelAutoPostPlan plan,
            string platform,
            DateTime? schedulePickerValue,
            out string error)
        {
            error = string.Empty;
            if (plan == null) { error = "Thiếu dữ liệu kế hoạch đăng."; return false; }

            var list = GetPlatformList(platform);
            if (list == null) { error = "Nền tảng không hợp lệ: " + platform; return false; }

            DateTime? scheduledAt = null;
            if (schedulePickerValue.HasValue && schedulePickerValue.Value > DateTime.Now.AddMinutes(1))
                scheduledAt = schedulePickerValue.Value;

            var isYouTube  = string.Equals(platform, "YouTube",  StringComparison.OrdinalIgnoreCase);
            var isFacebook = string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase);

            var caption = isYouTube ? string.Empty
                : isFacebook ? (plan.FacebookCaption ?? string.Empty)
                : (plan.TikTokCaption ?? string.Empty);
            var link = OmnichannelAutoPostFields.NormalizeLink(
                isFacebook ? (plan.FacebookShopeeLink ?? plan.AffiliateLink ?? string.Empty)
                           : (plan.AffiliateLink ?? string.Empty));

            list.Add(new ScheduleEntry
            {
                VideoFilePath = plan.VideoFilePath ?? string.Empty,
                VideoLabel    = System.IO.Path.GetFileName(plan.VideoFilePath ?? string.Empty),
                Caption       = caption,
                YtTitle       = plan.YouTubeTitle ?? string.Empty,
                YtDescription = plan.YouTubeDescription ?? string.Empty,
                Link          = link,
                Profile       = plan.Profile ?? "default",
                ScheduledAt   = scheduledAt,
                Status        = scheduledAt.HasValue ? "Scheduled" : "Pending",
                CreatedAtUtc  = DateTime.UtcNow
            });
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  REMOVE ROWS  (bulk, sorted descending by index to avoid reindex)
        // ─────────────────────────────────────────────────────────────────────────

        private void BtnRemoveRow_Click(DataGridView dgv, BindingList<ScheduleEntry> list)
        {
            if (dgv?.SelectedRows == null || dgv.SelectedRows.Count == 0 || list == null) return;

            var toRemove = dgv.SelectedRows.Cast<DataGridViewRow>()
                .OrderByDescending(r => r.Index)
                .Select(r => r.DataBoundItem as ScheduleEntry)
                .Where(e => e != null && !string.Equals(e.Status, "Running", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var entry in toRemove) list.Remove(entry);
            RefreshAutoPostScheduleStatus();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  RUN SCHEDULE  (per platform)
        // ─────────────────────────────────────────────────────────────────────────

        private async Task BtnRunScheduleAsync(string platform, DataGridView dgv)
        {
            if (_isAutoPostScheduleQueueRunning)
            {
                LogPlatform(platform, "[Lịch đăng] Đang chạy hàng đợi, vui lòng chờ.");
                return;
            }

            // Always resolve the live list at click-time (fixes stale-capture bug)
            var list = GetPlatformList(platform);

            // Collect all rows that are ready to be queued (Scheduled OR Pending),
            // sorted so "Đăng ngay" rows run first, then ascending by scheduled time.
            var runnable = list?
                .Where(e => string.Equals(e.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(e.Status, "Pending",   StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.ScheduledAt ?? DateTime.MinValue)
                .ToList() ?? new System.Collections.Generic.List<ScheduleEntry>();

            if (runnable.Count == 0)
            {
                MessageBox.Show(this,
                    $"Không có dòng nào trong lưới {platform} để đưa vào hàng đợi.",
                    "Chạy lịch đăng", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isAutoPostScheduleQueueRunning = true;
            _isAutoPostPaused = false;
            _autoPostScheduleCts?.Dispose();
            _autoPostScheduleCts = new CancellationTokenSource();
            var ct = _autoPostScheduleCts.Token;

            SetPauseButtonsEnabled(true);
            UpdatePauseButtonState();

            try
            {
                LogPlatform(platform, $"[Lịch đăng] Đã xếp hàng {runnable.Count} dòng — sẽ đợi đúng giờ nếu cần.");
                foreach (var entry in runnable)
                {
                    if (ct.IsCancellationRequested) break;

                    // ── Wait while paused ─────────────────────────────────────────
                    while (_isAutoPostPaused && !ct.IsCancellationRequested)
                    {
                        LogPlatform(platform, "[Lịch đăng] ⏸ Đang tạm dừng…");
                        await Task.Delay(2000, ct).ConfigureAwait(true);
                    }
                    if (ct.IsCancellationRequested) break;

                    // ── Wait until the scheduled time ─────────────────────────────
                    if (entry.ScheduledAt.HasValue && entry.ScheduledAt.Value > DateTime.Now)
                    {
                        entry.Status = "Waiting";
                        dgv?.Refresh();
                        LogPlatform(platform, $"[Lịch đăng] ⏳ Chờ đến {entry.ScheduledAtText} để đăng «{entry.VideoLabel}»…");
                        while (entry.ScheduledAt.Value > DateTime.Now && !ct.IsCancellationRequested)
                        {
                            var remaining = entry.ScheduledAt.Value - DateTime.Now;
                            if (remaining.TotalSeconds > 60)
                                LogPlatform(platform, $"  ⏳ Còn {(int)remaining.TotalMinutes} phút {remaining.Seconds} giây…");
                            var sleepMs = (int)Math.Min(60_000, remaining.TotalMilliseconds + 500);
                            await Task.Delay(Math.Max(500, sleepMs), ct).ConfigureAwait(true);
                        }
                        if (ct.IsCancellationRequested) break;
                    }

                    await RunScheduleEntryAsync(entry, platform, dgv, ct).ConfigureAwait(true);
                }
                LogPlatform(platform, "[Lịch đăng] ✓ Hoàn tất lịch đăng.");
            }
            finally
            {
                _isAutoPostScheduleQueueRunning = false;
                _isAutoPostPaused = false;
                SetPauseButtonsEnabled(false);
                UpdatePauseButtonState();
                RefreshAutoPostScheduleStatus();
                dgv?.Refresh();
            }
        }

        private void TogglePauseAutoPost()
        {
            if (!_isAutoPostScheduleQueueRunning) return;
            _isAutoPostPaused = !_isAutoPostPaused;
            UpdatePauseButtonState();
            LogAutoPost(_isAutoPostPaused
                ? "[Lịch đăng] ⏸ Đã tạm dừng — bấm lại để tiếp tục."
                : "[Lịch đăng] ▶ Tiếp tục đăng…");
        }

        private void SetPauseButtonsEnabled(bool enabled)
        {
            foreach (var btn in _pauseButtons)
            {
                if (btn == null || btn.IsDisposed) continue;
                void Apply() { btn.Enabled = enabled; }
                if (btn.InvokeRequired) btn.BeginInvoke((Action)Apply); else Apply();
            }
        }

        private void UpdatePauseButtonState()
        {
            foreach (var btn in _pauseButtons)
            {
                if (btn == null || btn.IsDisposed) continue;
                void Apply()
                {
                    if (_isAutoPostPaused)
                    {
                        btn.Text      = "▶";
                        btn.BackColor = Color.FromArgb(40, 120, 60);
                        btn.ForeColor = Color.FromArgb(140, 240, 150);
                    }
                    else
                    {
                        btn.Text      = "⏸";
                        btn.BackColor = Color.FromArgb(60, 70, 90);
                        btn.ForeColor = Color.FromArgb(180, 190, 210);
                    }
                }
                if (btn.InvokeRequired) btn.BeginInvoke((Action)Apply); else Apply();
            }
        }

        /// <summary>
        /// Global: merges all 3 grids into one time-sorted queue and pushes every
        /// Pending row into the central <see cref="GlobalJobQueue"/> (enforces
        /// MaxConcurrentJobs, pre-post jitter, and browser-busy checks automatically).
        /// Wired to the "BẮT ĐẦU ĐĂNG" toggle button.
        /// </summary>
        internal async Task RunAllPendingScheduleRowsAsync()
        {
            if (_isAutoPostScheduleQueueRunning)
            {
                LogAutoPost("[Lịch đăng] Đang chạy hàng đợi."); return;
            }

            PromoteDueEntries(_tikTokList);
            PromoteDueEntries(_facebookList);
            PromoteDueEntries(_youTubeList);

            // ── Collect all queued rows from 3 grids, sort ascending by scheduled time ─
            var allPending = new List<(ScheduleEntry entry, string platform, DataGridView dgv)>();

            void Collect(BindingList<ScheduleEntry> list, string platform, DataGridView dgv)
            {
                if (list == null) return;
                foreach (var e in list.Where(e =>
                    string.Equals(e?.Status, "Pending",   StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)))
                    allPending.Add((e, platform, dgv));
            }
            Collect(_tikTokList,   "TikTok",   dgvTikTokSchedule);
            Collect(_facebookList, "Facebook", dgvFacebookSchedule);
            Collect(_youTubeList,  "YouTube",  dgvYouTubeSchedule);

            allPending.Sort((a, b) =>
                (a.entry.ScheduledAt ?? DateTime.MinValue)
                    .CompareTo(b.entry.ScheduledAt ?? DateTime.MinValue));

            if (allPending.Count == 0)
            {
                LogAutoPost("[Lịch đăng] Không có dòng nào để xếp hàng."); return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            var disableAffiliate = chkEnableAffiliateLink != null && !chkEnableAffiliateLink.Checked;

            _isAutoPostScheduleQueueRunning = true;
            Interlocked.Exchange(ref _pendingScheduleJobCount, 0);
            int actuallyQueued = 0;

            // ── Enqueue each entry as an AutoPost OmniJob ─────────────────────────
            foreach (var (entry, platform, dgv) in allPending)
            {
                // Duplicate-enqueue guard
                if (string.Equals(entry.Status, "Queued",   StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Status, "Running",  StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Status, "Waiting",  StringComparison.OrdinalIgnoreCase))
                {
                    LogAutoPost($"[Lịch đăng] Bỏ qua (đã xếp hàng): {platform} «{entry.VideoLabel}»");
                    continue;
                }

                var plan    = BuildPlanFromEntry(entry, platform);
                var payload = new AutoPostJobPayload
                {
                    VideoFolder    = plan.VideoFolder,
                    VideoFilePath  = plan.VideoFilePath,
                    Profile        = plan.Profile,
                    PostTikTok     = plan.PostTikTok,
                    PostFacebook   = plan.PostFacebook,
                    PostYouTube    = plan.PostYouTube,
                    TikTokCaption  = plan.TikTokCaption,
                    TikTokHashtags = plan.TikTokHashtags,
                    TikTokUploadOnly         = plan.TikTokUploadOnly,
                    FacebookCaption          = plan.FacebookCaption,
                    FacebookHashtags         = plan.FacebookHashtags,
                    FacebookAttachShopeeLink = plan.FacebookAttachShopeeLink,
                    FacebookShopeeLink       = OmnichannelAutoPostFields.NormalizeLink(plan.FacebookShopeeLink),
                    YouTubeTitle             = plan.YouTubeTitle,
                    YouTubeDescription       = plan.YouTubeDescription,
                    PostFingerprint          = plan.PostFingerprint,
                    CombinedCaptionPreview   = plan.CombinedCaptionPreview,
                    StorageRootPath          = settings.StorageRootPath ?? string.Empty,
                    AffiliateLink = disableAffiliate ? string.Empty : OmnichannelAutoPostFields.NormalizeLink(plan.AffiliateLink),
                    ProductId     = disableAffiliate ? string.Empty : OmnichannelAutoPostFields.NormalizeLink(plan.ProductId),
                    ApplyPrePostJitter = true,
                    CheckBrowserBusy   = true
                };

                var job = new OmniJob
                {
                    Kind        = OmniJobKind.AutoPost,
                    Title       = $"[Lịch] {platform} — {entry.VideoLabel}",
                    ProfileName = entry.Profile ?? "default",
                    PayloadJson = JsonConvert.SerializeObject(payload),
                    MaxRetries  = 1
                };

                _scheduleJobMap[job.Id] = (entry, dgv);
                entry.Status = "Queued";
                dgv?.Refresh();

                _globalJobQueue.Enqueue(job);
                actuallyQueued++;
                LogAutoPost($"[Lịch đăng] Xếp hàng: {platform} «{entry.VideoLabel}»");
            }

            Interlocked.Exchange(ref _pendingScheduleJobCount, actuallyQueued);

            if (actuallyQueued == 0)
            {
                _isAutoPostScheduleQueueRunning = false;
                LogAutoPost("[Lịch đăng] Không có job mới để xếp hàng (tất cả đã ở trạng thái Queued/Running).");
                RefreshAutoPostScheduleStatus();
                return;
            }

            LogAutoPost($"[Lịch đăng] {actuallyQueued} job đã vào GlobalJobQueue — JobWorkerService xử lý (tối đa {settings.MaxConcurrentJobs} đồng thời, jitter 30–90s, kiểm tra trình duyệt).");
            RefreshAutoPostScheduleStatus();
        }

        private async Task RunScheduleEntryAsync(
            ScheduleEntry entry,
            string platform,
            DataGridView dgv,
            CancellationToken ct)
        {
            if (entry == null) return;
            entry.Status    = "Running";
            entry.LastError = string.Empty;
            dgv?.Refresh();

            LogPlatform(platform, $"▶ Bắt đầu đăng: «{entry.VideoLabel}» | Profile: {entry.Profile}");

            try
            {
                var plan = BuildPlanFromEntry(entry, platform);

                LogPlatform(platform, $"  Caption: {(entry.Caption?.Length > 40 ? entry.Caption.Substring(0, 40) + "…" : entry.Caption)}");
                LogPlatform(platform, $"  Hashtag: {entry.Hashtag}");
                if (string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase))
                    LogPlatform(platform, $"  YT Tiêu đề: {entry.YtTitle}");

                var summary = await RunOmnichannelAutoPostSequenceAsync(plan, ct).ConfigureAwait(true);

                entry.Status    = "Done";
                entry.LastError = string.Empty;
                LogPlatform(platform, $"✓ Đăng thành công «{entry.VideoLabel}»: {summary}");
            }
            catch (OperationCanceledException)
            {
                entry.Status    = "Pending";
                entry.LastError = "Đã hủy";
                LogPlatform(platform, $"⊗ Đã hủy «{entry.VideoLabel}»");
            }
            catch (Exception ex)
            {
                entry.Status    = "Failed";
                entry.LastError = ex.Message;
                // Log full detail including inner exception if any
                var detail = ex.InnerException != null
                    ? $"{ex.Message} → {ex.InnerException.Message}"
                    : ex.Message;
                LogPlatform(platform, $"✗ THẤT BẠI «{entry.VideoLabel}»");
                LogPlatform(platform, $"  Lỗi: {detail}");
                LogPlatform(platform, $"  Loại: {ex.GetType().Name}");
            }

            dgv?.Refresh();
            SaveScheduleToDisk();
        }

        private OmnichannelAutoPostPlan BuildPlanFromEntry(ScheduleEntry entry, string platform)
        {
            // Ensure a style is chosen — auto-pick random if user skipped selector
            EnsureCaptionSelected(entry, platform);

            var hashtags   = entry.Hashtag ?? string.Empty;
            var isTikTok   = string.Equals(platform, "TikTok",   StringComparison.OrdinalIgnoreCase);
            var isFacebook = string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase);
            var isYouTube  = string.Equals(platform, "YouTube",  StringComparison.OrdinalIgnoreCase);

            // Strip hashtags from caption body first — they may already be embedded in
            // the AllCaptions value; entry.Hashtag holds the canonical hashtag string.
            var captionBody    = StripTrailingHashtags(entry.Caption);
            var caption        = BuildFinalAutoPostCaptionBody(captionBody, hashtags);
            var normalizedLink = OmnichannelAutoPostFields.NormalizeLink(entry.Link);
            var attachShopee   = isFacebook && OmnichannelAutoPostFields.IsShopeeProductUrl(normalizedLink);

            var ytTitle = (entry.YtTitle ?? string.Empty).Trim();
            if (ytTitle.Length > 60) ytTitle = ytTitle.Substring(0, 60).TrimEnd();

            var folder = System.IO.Path.GetDirectoryName(entry.VideoFilePath) ?? string.Empty;
            var fingerprint = BuildAutoPostFingerprintSource(
                folder, caption, hashtags, entry.Profile, entry.VideoFilePath) + "|" + platform;

            return new OmnichannelAutoPostPlan
            {
                VideoFolder      = folder,
                VideoFilePath    = entry.VideoFilePath,
                Profile          = entry.Profile ?? "default",
                PostTikTok       = isTikTok,
                PostFacebook     = isFacebook,
                PostYouTube      = isYouTube,
                TikTokCaption    = isTikTok  ? caption   : string.Empty,
                TikTokHashtags   = isTikTok  ? hashtags  : string.Empty,
                TikTokUploadOnly = false,
                FacebookCaption  = isFacebook ? caption  : string.Empty,
                FacebookHashtags = isFacebook ? hashtags : string.Empty,
                FacebookAttachShopeeLink = attachShopee,
                FacebookShopeeLink       = isFacebook ? normalizedLink : string.Empty,
                YouTubeTitle       = isYouTube ? ytTitle                      : string.Empty,
                YouTubeDescription = isYouTube ? (entry.YtDescription ?? string.Empty) : string.Empty,
                AffiliateLink      = isTikTok ? normalizedLink : string.Empty,
                ProductId          = string.Empty,
                PostFingerprint        = fingerprint,
                CombinedCaptionPreview = caption,
                SkipFolderScopeCheck   = true,   // manual schedule: allow any video path
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  AI CAPTION GENERATION  (bulk, top-to-bottom — all 3 platforms per call)
        // ─────────────────────────────────────────────────────────────────────────

        private async void BtnGenCaption_Click(
            string platform,
            DataGridView dgv,
            BindingList<ScheduleEntry> list)
        {
            if (dgv?.SelectedRows == null || dgv.SelectedRows.Count == 0)
            {
                LogPlatform(platform, "[Caption AI] Tô ít nhất 1 dòng trong lưới để tạo caption.");
                return;
            }

            var settings = await _configManager.LoadAsync();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                LogPlatform(platform, "[Caption AI] Chưa cấu hình AI API Key trong Cài đặt."); return;
            }

            var isYouTube  = string.Equals(platform, "YouTube",  StringComparison.OrdinalIgnoreCase);
            var isFacebook = string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase);

            // Sort by row index so we process top-to-bottom
            var selectedEntries = dgv.SelectedRows.Cast<DataGridViewRow>()
                .OrderBy(r => r.Index)
                .Select(r => r.DataBoundItem as ScheduleEntry)
                .Where(e => e != null)
                .ToList();

            LogPlatform(platform,
                $"[Caption AI] Tạo caption cho {selectedEntries.Count} dòng trong lưới {platform}…");

            // Build the recent-captions list for duplicate-avoidance (TikTok-specific pool)
            var recentCaptions = CollectRecentCaptions(platform, maxCount: 10);

            int ok = 0, failed = 0;

            foreach (var entry in selectedEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.VideoFilePath) || !System.IO.File.Exists(entry.VideoFilePath))
                {
                    LogPlatform(platform, $"[Caption AI] ⊗ Bỏ qua «{entry.VideoLabel}»: không tìm thấy file video.");
                    failed++;
                    continue;
                }

                entry.Status = "Processing...";
                dgv.Refresh();

                try
                {
                    var profileName = (entry.Profile    ?? string.Empty).Trim();
                    var productName = (entry.VideoLabel ?? string.Empty).Trim();

                    LogPlatform(platform, $"[Caption AI] Nén video & gửi Gemini cho «{entry.VideoLabel}»…");
                    var result = await _geminiService.GenerateAllChannelsCaptionAsync(
                        entry.VideoFilePath,
                        profileName,
                        productName,
                        settings.AiProvider,
                        settings.AiApiKey,
                        settings.AiModel,
                        CancellationToken.None,
                        recentCaptions).ConfigureAwait(true);

                    // ── Store 5-style variants into AllCaptions ───────────────
                    var styleDict = isYouTube  ? result.YouTubeStyles
                                  : isFacebook ? result.FacebookStyles
                                               : result.TikTokStyles;

                    if (styleDict != null && styleDict.Count > 0)
                    {
                        entry.AllCaptions = new System.Collections.Generic.Dictionary<string, string>(styleDict);

                        // Extract hashtag from any available variant to pre-fill the Hashtag field
                        var sampleFull = styleDict.ContainsKey("fomo")
                            ? styleDict["fomo"]
                            : styleDict.Values.First();
                        entry.Hashtag = ExtractHashtagsFromCaption(sampleFull);
                        if (string.IsNullOrWhiteSpace(entry.SelectedCaptionKey))
                            entry.SelectedCaptionKey = string.Empty;   // force user to choose
                    }
                    else
                    {
                        // Fallback: single-variant result from older API response
                        entry.AllCaptions = new System.Collections.Generic.Dictionary<string, string>();
                        if (isYouTube)
                        {
                            entry.YtTitle       = result.YouTubeTitle;
                            var ytFull          = result.YouTubeDescription ?? string.Empty;
                            entry.YtDescription = StripTrailingHashtags(ytFull);
                            entry.Hashtag       = ExtractHashtagsFromCaption(ytFull);
                        }
                        else if (isFacebook)
                        {
                            var full      = result.Facebook ?? string.Empty;
                            entry.Caption = StripTrailingHashtags(full);
                            entry.Hashtag = ExtractHashtagsFromCaption(full);
                        }
                        else
                        {
                            var full      = result.TikTok ?? string.Empty;
                            entry.Caption = StripTrailingHashtags(full);
                            entry.Hashtag = ExtractHashtagsFromCaption(full);
                        }
                    }

                    // ── Open style selector so user picks right away ──────────
                    if (styleDict != null && styleDict.Count > 0)
                    {
                        dgv.Refresh();
                        OpenCaptionSelector(entry, platform, dgv, dgv.SelectedRows
                            .Cast<DataGridViewRow>()
                            .Where(r => r.DataBoundItem == entry)
                            .Select(r => r.Index)
                            .FirstOrDefault());
                    }

                    var tagCount = (entry.Hashtag ?? string.Empty).Split(
                        new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    LogPlatform(platform,
                        $"[Caption AI] ✓ «{entry.VideoLabel}» — {tagCount} hashtag");
                    ok++;
                }
                catch (Exception ex)
                {
                    LogPlatform(platform, $"[Caption AI] ✗ Lỗi «{entry.VideoLabel}»: {ex.Message}");
                    failed++;
                }

                entry.Status = "Pending";
                dgv.Refresh();
            }

            // Save the updated captions/hashtags to disk
            try
            {
                _scheduleManager?.Save(new ScheduleData
                {
                    TikTok   = new System.Collections.Generic.List<ScheduleEntry>(_tikTokList),
                    Facebook = new System.Collections.Generic.List<ScheduleEntry>(_facebookList),
                    YouTube  = new System.Collections.Generic.List<ScheduleEntry>(_youTubeList),
                });
            }
            catch { /* non-critical */ }

            var summary = $"[Caption AI] ✓ Hoàn tất {platform}: {ok} thành công" +
                          (failed > 0 ? $", {failed} lỗi/bỏ qua." : ".");
            LogPlatform(platform, summary);

            // Visible completion notification when batch has more than 1 row
            if (selectedEntries.Count > 1)
            {
                var detail = failed > 0
                    ? $"{ok}/{selectedEntries.Count} dòng thành công, {failed} lỗi."
                    : $"Tất cả {ok} dòng đã có caption.";
                MessageBox.Show(
                    detail,
                    $"Tạo caption {platform} xong",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Routes a log message to the correct per-platform log panel.
        /// Falls back to the shared AutoPost log for unknown platforms.
        /// </summary>
        private void LogPlatform(string platform, string msg)
        {
            switch (platform)
            {
                case "TikTok":   LogTikTok(msg);   break;
                case "Facebook": LogFacebook(msg); break;
                case "YouTube":  LogYouTube(msg);  break;
                default:         LogAutoPost(msg); break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  TIMER  (15s — promotes due Scheduled→Pending; runs queue only when armed)
        // ─────────────────────────────────────────────────────────────────────────

        private void AutoPostScheduleTimer_Tick(object sender, EventArgs e)
        {
            // Refresh grid status display only — auto-posting is triggered exclusively
            // by the user pressing "Chạy lịch đăng" or "BẮT ĐẦU ĐĂNG".
            RefreshAutoPostScheduleStatus();
            dgvTikTokSchedule?.Refresh();
            dgvFacebookSchedule?.Refresh();
            dgvYouTubeSchedule?.Refresh();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  QUEUE ARMED / DISARMED  (toggle via btnStartAutoPost)
        // ─────────────────────────────────────────────────────────────────────────

        internal void SetAutoPostQueueActive(bool active)
        {
            _isAutoPostQueueActive = active;

            void UpdateUi()
            {
                if (btnStartAutoPost == null || btnStartAutoPost.IsDisposed) return;

                if (active)
                {
                    if (btnStartAutoPost is JellyButton jb) jb.JellyTint = AutoPostTintCloseBrowser;
                    btnStartAutoPost.Text = "DỪNG ĐĂNG";
                }
                else
                {
                    if (btnStartAutoPost is JellyButton jb) jb.JellyTint = AutoPostTintStart;
                    btnStartAutoPost.Text = "BẮT ĐẦU ĐĂNG";
                }

                var width = MeasureAutoPostJellyButtonTextWidth(btnStartAutoPost.Text) + 24;
                btnStartAutoPost.Width = width;
                btnStartAutoPost.MinimumSize = new Size(width, btnStartAutoPost.Height);
                btnStartAutoPost.MaximumSize = new Size(width, btnStartAutoPost.Height);
            }

            if (btnStartAutoPost?.InvokeRequired ?? false)
                btnStartAutoPost.BeginInvoke((Action)UpdateUi);
            else
                UpdateUi();
        }

        private static bool PromoteDueEntries(BindingList<ScheduleEntry> list)
        {
            if (list == null) return false;
            var now = DateTime.Now;
            var promoted = false;
            foreach (var e in list)
            {
                if (e == null ||
                    !string.Equals(e.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)) continue;

                // Promote if: "Đăng ngay" (no scheduled time) OR time has arrived
                var isDue = !e.ScheduledAt.HasValue || e.ScheduledAt.Value <= now;
                if (!isDue) continue;

                e.Status = "Pending";
                promoted = true;
            }
            return promoted;
        }

        private bool HasAnyPendingEntries()
        {
            static bool AnyPending(BindingList<ScheduleEntry> l) =>
                l?.Any(e => string.Equals(e?.Status, "Pending", StringComparison.OrdinalIgnoreCase)) ?? false;
            return AnyPending(_tikTokList) || AnyPending(_facebookList) || AnyPending(_youTubeList);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  PERSISTENCE  (JSON, called on Form_Shown / Form_Closing)
        // ─────────────────────────────────────────────────────────────────────────

        internal void LoadSchedule()
        {
            try
            {
                var data = _scheduleManager?.Load() ?? new ScheduleData();
                Repopulate(_tikTokList,   data.TikTok);
                Repopulate(_facebookList, data.Facebook);
                Repopulate(_youTubeList,  data.YouTube);
                RefreshAutoPostScheduleStatus();
                LogAutoPost("[Lịch đăng] Đã nạp lịch từ file.");
            }
            catch (Exception ex)
            {
                LogAutoPost("[Lịch đăng] Lỗi nạp lịch: " + ex.Message);
            }
        }

        internal void SaveSchedule()
        {
            try
            {
                if (_scheduleManager == null) return;
                _scheduleManager.Save(new ScheduleData
                {
                    TikTok   = _tikTokList?.ToList()   ?? new List<ScheduleEntry>(),
                    Facebook = _facebookList?.ToList() ?? new List<ScheduleEntry>(),
                    YouTube  = _youTubeList?.ToList()  ?? new List<ScheduleEntry>()
                });
            }
            catch (Exception ex)
            {
                LogAutoPost("[Lịch đăng] Lỗi lưu lịch: " + ex.Message);
            }
        }

        private static void Repopulate(BindingList<ScheduleEntry> target, List<ScheduleEntry> source)
        {
            if (target == null || source == null) return;
            target.Clear();
            foreach (var e in source) if (e != null) target.Add(e);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  STATUS LABELS
        // ─────────────────────────────────────────────────────────────────────────

        private void RefreshAutoPostScheduleStatus()
        {
            UpdateStatusLabel(lblTikTokScheduleStatus,   "TikTok",   _tikTokList);
            UpdateStatusLabel(lblFacebookScheduleStatus, "Facebook", _facebookList);
            UpdateStatusLabel(lblYouTubeScheduleStatus,  "YouTube",  _youTubeList);
        }

        private static void UpdateStatusLabel(Label lbl, string platform, BindingList<ScheduleEntry> list)
        {
            if (lbl == null || lbl.IsDisposed || list == null) return;
            void Do()
            {
                var total     = list.Count;
                var pending   = list.Count(e => string.Equals(e?.Status, "Pending",   StringComparison.OrdinalIgnoreCase));
                var scheduled = list.Count(e => string.Equals(e?.Status, "Scheduled", StringComparison.OrdinalIgnoreCase));
                lbl.Text = $"{platform}: {total} dòng · Pending {pending} · Scheduled {scheduled}";
            }
            if (lbl.InvokeRequired) lbl.Invoke((Action)Do);
            else Do();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────────

        private BindingList<ScheduleEntry> GetPlatformList(string platform)
        {
            if (string.Equals(platform, "TikTok",   StringComparison.OrdinalIgnoreCase)) return _tikTokList;
            if (string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase)) return _facebookList;
            if (string.Equals(platform, "YouTube",  StringComparison.OrdinalIgnoreCase)) return _youTubeList;
            return null;
        }

        private void StorePlatformGrid(string platform, DataGridView dgv)
        {
            if (string.Equals(platform, "TikTok",   StringComparison.OrdinalIgnoreCase)) dgvTikTokSchedule   = dgv;
            if (string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase)) dgvFacebookSchedule = dgv;
            if (string.Equals(platform, "YouTube",  StringComparison.OrdinalIgnoreCase)) dgvYouTubeSchedule  = dgv;
        }

        private void SetPlatformStatusLabel(string platform, Label lbl)
        {
            if (string.Equals(platform, "TikTok",   StringComparison.OrdinalIgnoreCase)) lblTikTokScheduleStatus   = lbl;
            if (string.Equals(platform, "Facebook", StringComparison.OrdinalIgnoreCase)) lblFacebookScheduleStatus = lbl;
            if (string.Equals(platform, "YouTube",  StringComparison.OrdinalIgnoreCase)) lblYouTubeScheduleStatus  = lbl;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  POPUP CAPTION EDITOR
        // ─────────────────────────────────────────────────────────────────────────

        private void PlatformGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dgv = sender as DataGridView;
            if (dgv == null) return;

            var col   = dgv.Columns[e.ColumnIndex];
            var entry = dgv.Rows[e.RowIndex].DataBoundItem as ScheduleEntry;
            if (entry == null) return;

            switch (col.Name)
            {
                    case "colVideoLabel":
                    case "colPlay":
                    OpenVideoInPlayer(entry);
                    return;

                case "colCaptionStatus":
                    // Only open the style picker when AI styles are available.
                    // If the cell was already in edit mode the user wants to type — let the grid handle it.
                    if (entry.AllCaptions != null && entry.AllCaptions.Count > 0 &&
                        dgv.CurrentCell?.RowIndex == e.RowIndex &&
                        dgv.CurrentCell?.ColumnIndex == e.ColumnIndex &&
                        !dgv.IsCurrentCellInEditMode)
                    {
                        OpenCaptionSelector(entry, GetPlatformForGrid(dgv), dgv, e.RowIndex);
                    }
                    return;

                case "colYtTitlePreview":
                    if (entry.AllCaptions != null && entry.AllCaptions.Count > 0 &&
                        !dgv.IsCurrentCellInEditMode)
                    {
                        OpenCaptionSelector(entry, GetPlatformForGrid(dgv), dgv, e.RowIndex);
                    }
                    return;

                case "colHashtagPreview":
                    OpenHashtagEditor(entry, GetPlatformForGrid(dgv), dgv, e.RowIndex);
                    return;

                case "colScheduledAt":
                    OpenSchedulePicker(entry, dgv, e.RowIndex);
                    return;
            }
        }

        private void OpenCaptionSelector(ScheduleEntry entry, string platform, DataGridView dgv, int rowIndex)
        {
            var isYouTube = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase);
            var styles    = entry.AllCaptions;

            if (styles == null || styles.Count == 0)
            {
                MessageBox.Show(
                    "Chưa có dữ liệu phong cách.\nHãy bấm \"Tạo caption (Gemini)\" trước.",
                    "Chưa có caption", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new FormCaptionSelector(platform, styles, entry.SelectedCaptionKey, entry.VideoLabel))
            {
                var result = dlg.ShowDialog(this);

                string chosenKey;
                if (result == DialogResult.OK)
                {
                    chosenKey = dlg.ChosenKey;
                }
                else
                {
                    // User cancelled → auto-pick random if nothing selected yet
                    if (!string.IsNullOrWhiteSpace(entry.SelectedCaptionKey)) return;
                    var keys = Services.OmnichannelCaptionResult.StyleKeys
                        .Where(k => styles.ContainsKey(k)).ToList();
                    if (keys.Count == 0) return;
                    chosenKey = keys[new Random().Next(keys.Count)];
                }

                entry.SelectedCaptionKey = chosenKey;
                if (styles.TryGetValue(chosenKey, out var fullText))
                {
                    if (isYouTube)
                    {
                        Services.GeminiService.SplitYouTubeStyleValue(fullText, out var t, out var d);
                        entry.YtTitle       = t;
                        entry.YtDescription = StripTrailingHashtags(d);
                        entry.Hashtag       = ExtractHashtagsFromCaption(fullText);
                    }
                    else
                    {
                        entry.Caption = StripTrailingHashtags(fullText);
                        entry.Hashtag = ExtractHashtagsFromCaption(fullText);
                    }
                }

                dgv.InvalidateRow(rowIndex);
                SaveScheduleToDisk();
            }
        }

        private void OpenHashtagEditor(ScheduleEntry entry, string platform, DataGridView dgv, int rowIndex)
        {
            using (var dlg = new FormHashtagEditor(entry.Hashtag, platform))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                entry.Hashtag = dlg.ResultHashtag;
                dgv.InvalidateRow(rowIndex);
                SaveScheduleToDisk();
            }
        }

        /// <summary>
        /// If AllCaptions has styles but SelectedCaptionKey is empty, pick a random one
        /// and populate Caption / YtTitle / YtDescription accordingly.
        /// </summary>
        private static void EnsureCaptionSelected(ScheduleEntry entry, string platform)
        {
            if (entry.AllCaptions == null || entry.AllCaptions.Count == 0) return;
            if (!string.IsNullOrWhiteSpace(entry.SelectedCaptionKey)) return;

            var keys = Services.OmnichannelCaptionResult.StyleKeys
                .Where(k => entry.AllCaptions.ContainsKey(k)).ToList();
            if (keys.Count == 0) return;

            var chosenKey = keys[new Random().Next(keys.Count)];
            entry.SelectedCaptionKey = chosenKey;

            if (!entry.AllCaptions.TryGetValue(chosenKey, out var fullText)) return;

            var isYouTube = string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase);
            if (isYouTube)
            {
                Services.GeminiService.SplitYouTubeStyleValue(fullText, out var t, out var d);
                if (string.IsNullOrWhiteSpace(entry.YtTitle))       entry.YtTitle       = t;
                if (string.IsNullOrWhiteSpace(entry.YtDescription)) entry.YtDescription = StripTrailingHashtags(d);
                if (string.IsNullOrWhiteSpace(entry.Hashtag))       entry.Hashtag       = ExtractHashtagsFromCaption(fullText);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(entry.Caption)) entry.Caption = StripTrailingHashtags(fullText);
                if (string.IsNullOrWhiteSpace(entry.Hashtag)) entry.Hashtag = ExtractHashtagsFromCaption(fullText);
            }
        }

        private void OpenSchedulePicker(ScheduleEntry entry, DataGridView dgv, int rowIndex)
        {
            using (var dlg = new FormSchedulePicker(entry.ScheduledAt, entry.VideoLabel))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                entry.ScheduledAt = dlg.ResultScheduledAt;
                ApplyScheduleStatusFromTime(entry);
                dgv.InvalidateRow(rowIndex);
                SaveScheduleToDisk();
                RefreshAutoPostScheduleStatus();
            }
        }

        /// <summary>Pending nếu đăng ngay hoặc đã quá giờ; Scheduled nếu hẹn tương lai.</summary>
        private static void ApplyScheduleStatusFromTime(ScheduleEntry entry)
        {
            if (entry == null) return;
            var s = entry.Status ?? string.Empty;
            if (string.Equals(s, "Running", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "Queued",  StringComparison.OrdinalIgnoreCase))
                return;

            if (!entry.ScheduledAt.HasValue ||
                entry.ScheduledAt.Value <= DateTime.Now.AddMinutes(1))
                entry.Status = "Pending";
            else
                entry.Status = "Scheduled";
        }

        private void SaveScheduleToDisk()
        {
            try
            {
                _scheduleManager?.Save(new ScheduleData
                {
                    TikTok   = new System.Collections.Generic.List<ScheduleEntry>(_tikTokList),
                    Facebook = new System.Collections.Generic.List<ScheduleEntry>(_facebookList),
                    YouTube  = new System.Collections.Generic.List<ScheduleEntry>(_youTubeList),
                });
            }
            catch { /* non-critical */ }
        }

        private void PlatformGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;
            var dgv = sender as DataGridView;
            if (dgv == null || dgv.SelectedRows.Count == 0) return;

            var platform = GetPlatformForGrid(dgv);
            var list     = GetPlatformList(platform);
            if (list == null) return;

            // Confirm only when deleting more than 1 row
            if (dgv.SelectedRows.Count > 1)
            {
                var ans = MessageBox.Show(
                    $"Xóa {dgv.SelectedRows.Count} dòng đã chọn?",
                    "Xác nhận xóa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (ans != DialogResult.Yes) return;
            }

            var toRemove = dgv.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as ScheduleEntry)
                .Where(en => en != null)
                .ToList();

            foreach (var en in toRemove) list.Remove(en);

            SaveScheduleToDisk();

            RefreshAutoPostScheduleStatus();
            e.Handled = true;
        }

        private void OpenVideoInPlayer(ScheduleEntry entry)
        {
            var path = entry?.VideoFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                LogAutoPost("▶ Không có đường dẫn video để mở.");
                return;
            }

            if (!System.IO.File.Exists(path))
            {
                LogAutoPost($"▶ Không tìm thấy file: {path}");
                MessageBox.Show(
                    $"File không tồn tại:\n{path}",
                    "Không tìm thấy video",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = path,
                    UseShellExecute = true,   // lets Windows pick the default video player
                });
                LogAutoPost($"▶ Đang mở: {System.IO.Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                LogAutoPost($"▶ Lỗi mở video: {ex.Message}");
            }
        }

        private string GetPlatformForGrid(DataGridView dgv)
        {
            if (dgv == dgvTikTokSchedule)   return "TikTok";
            if (dgv == dgvFacebookSchedule) return "Facebook";
            if (dgv == dgvYouTubeSchedule)  return "YouTube";
            return "TikTok";
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  HASHTAG HELPERS
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Extracts all #tag tokens from a caption string.</summary>
        private static string ExtractHashtagsFromCaption(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return string.Empty;
            var matches = Regex.Matches(full, @"#[^\s#]+");
            return matches.Count == 0 ? string.Empty
                : string.Join(" ", matches.Cast<Match>().Select(m => m.Value));
        }

        /// <summary>
        /// Removes the trailing hashtag block from a caption so the caption body
        /// and hashtags can be stored in separate fields.
        /// </summary>
        private static string StripTrailingHashtags(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return string.Empty;
            return Regex.Replace(full, @"(\s*#[^\s#]+)+\s*$", string.Empty).Trim();
        }

        /// <summary>
        /// Collects the most recent (up to <paramref name="maxCount"/>) used captions for a given
        /// platform from the schedule list, to feed as "already used" context to Gemini.
        /// </summary>
        private IReadOnlyList<string> CollectRecentCaptions(string platform, int maxCount = 10)
        {
            var list = GetPlatformList(platform);
            if (list == null || list.Count == 0)
                return System.Array.Empty<string>();

            var captions = new List<string>();
            foreach (var e in list)
            {
                if (e == null) continue;
                // Use the active/selected caption body (strip hashtags for the context)
                var text = StripTrailingHashtags(e.Caption ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    captions.Add(text);
                if (captions.Count >= maxCount) break;
            }
            return captions.AsReadOnly();
        }
    }
}

