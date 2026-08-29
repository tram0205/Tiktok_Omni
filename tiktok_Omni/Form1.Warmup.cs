using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Helpers;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public class QueueTrendRow
    {
        public string Date { get; set; } = string.Empty;
        public int Success { get; set; }
        public int Failed { get; set; }
        public int Total { get; set; }
        public string SuccessRate { get; set; } = "-";
    }

    public partial class Form1
    {
        private TabPage tabAutoWarmup;
        private TextBox txtKeywords;
        private NumericUpDown numVideoCount;
        private NumericUpDown numWatchMin;
        private NumericUpDown numWatchMax;
        private NumericUpDown numLikeProbability;
        private NumericUpDown numShareProbability;
        private NumericUpDown numCommentProbability;
        private CheckBox chkAutoComment;
        private RadioButton rbDryRun;
        private RadioButton rbLiveRun;
        private Button btnQueueWarmup;
        private Button btnStartWarmupQueue;
        private Button btnPauseWarmupQueue;
        private Button btnStopWarmupQueue;
        private Button btnMoveQueueJobUp;
        private Button btnMoveQueueJobDown;
        private Button btnRemoveQueueJob;
        private ComboBox cbQueueStatsRange;
        private DateTimePicker dtQueueStatsFrom;
        private DateTimePicker dtQueueStatsTo;
        private Label lblWarmupQueueStatus;
        private Label lblQueueStatsSummary;
        private Label lblQueueStatsSuccessRate;
        private Label lblQueueStatsAvgRetry;
        private Label lblQueueStatsTopFailedProfile;
        private DataGridView dgvWarmupQueue;
        private ProgressBar pbWarmupProgress;
        private Label lblWarmupProgress;
        private RichTextBox rtbWarmupLog;
        private CheckBox chkEnableWarmupSchedule;
        private DateTimePicker dtpWarmupSchedule;
        private System.Windows.Forms.Timer _warmupScheduleTimer;
        private CancellationTokenSource _warmupCancellation;
        private CancellationTokenSource _warmupQueueCancellation;
        private CancellationTokenSource _currentQueueJobCancellation;
        private WarmupRunState _currentWarmupState;
        private readonly WarmupStateManager _warmupStateManager;
        private readonly WarmupQueueScheduler _warmupQueueScheduler;
        private readonly WarmupQueueStateManager _warmupQueueStateManager;
        private readonly WarmupQueueHistoryManager _warmupQueueHistoryManager;
        private BindingList<WarmupQueueUiItem> _warmupQueueBindingList;
        private List<WarmupQueueHistoryRecord> _warmupQueueHistory;
        private bool _isWarmupQueueRunning;
        private bool _isWarmupQueuePaused;
        private bool _pauseNowRequested;

        private const int WatchPercentageUiMax = 300;
        private const int DefaultWatchPercentageMin = 80;
        private const int DefaultWatchPercentageMax = 150;
        private const int DefaultLikeProbability = 50;
        private const int DefaultCommentProbability = 10;
        private const int DefaultShareProbability = 20;

        private static string FormatWatchRangeForQueue(int pctMin, int pctMax)
        {
            var lo = Math.Max(1, pctMin);
            var hi = Math.Max(lo, pctMax);
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}%", lo, hi);
        }

        private static void ParseWatchRangeToPercent(string watchRange, out int watchMin, out int watchMax)
        {
            watchMin = DefaultWatchPercentageMin;
            watchMax = DefaultWatchPercentageMax;
            if (string.IsNullOrWhiteSpace(watchRange))
            {
                return;
            }

            var wr = watchRange.Trim();
            if (wr.EndsWith("%", StringComparison.Ordinal))
            {
                wr = wr.Substring(0, wr.Length - 1).Trim();
            }

            const string totalSuffix = " (tổng)";
            if (wr.EndsWith(totalSuffix, StringComparison.OrdinalIgnoreCase))
            {
                wr = wr.Substring(0, wr.Length - totalSuffix.Length).Trim();
            }

            if (wr.EndsWith(" phút", StringComparison.OrdinalIgnoreCase) ||
                wr.EndsWith(" s", StringComparison.OrdinalIgnoreCase) ||
                wr.EndsWith(" m", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var parts = wr.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minPct) &&
                int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxPct))
            {
                // BẢO VỆ: Chống load nhầm dữ liệu cũ (5-7 phút)
                if (minPct < 30) minPct = DefaultWatchPercentageMin;
                if (maxPct < 30) maxPct = DefaultWatchPercentageMax;

                watchMin = Math.Max(30, Math.Min(WatchPercentageUiMax, minPct));
                watchMax = Math.Max(watchMin, Math.Min(WatchPercentageUiMax, maxPct));
            }
        }

        private static int ReadWatchPercentageFromUi(NumericUpDown ctrl, int fallback)
        {
            if (ctrl == null)
            {
                return fallback;
            }

            var val = (int)ctrl.Value;
            // BẢO VỆ TỪ UI: Nếu người dùng lỡ nhập bé hơn 30, tự reset về mặc định (80%) để tránh bot bị nhận diện là spam
            if (val < 30) return fallback;

            return Math.Max(30, Math.Min(WatchPercentageUiMax, val));
        }

        private static void ApplyWatchPercentageToUi(NumericUpDown ctrl, int percent)
        {
            if (ctrl == null)
            {
                return;
            }

            // BẢO VỆ: Không cho hiển thị số rác lên UI
            if (percent < 30) percent = DefaultWatchPercentageMin;

            var value = Math.Max((int)ctrl.Minimum, Math.Min((int)ctrl.Maximum, percent));
            ctrl.Value = value;
        }

        private static int ReadPercentFromUi(NumericUpDown ctrl, int fallback)
        {
            if (ctrl == null)
            {
                return fallback;
            }

            return Math.Max(0, Math.Min(100, (int)ctrl.Value));
        }

        private int ReadLikeProbabilityFromUi()
        {
            return ReadPercentFromUi(numLikeProbability, DefaultLikeProbability);
        }

        private int ReadShareProbabilityFromUi()
        {
            return ReadPercentFromUi(numShareProbability, DefaultShareProbability);
        }

        private int ReadCommentProbabilityFromUi()
        {
            if (chkAutoComment != null && !chkAutoComment.Checked)
            {
                return 0;
            }

            return ReadPercentFromUi(numCommentProbability, DefaultCommentProbability);
        }

        private static void ApplyPercentToUi(NumericUpDown ctrl, int percent, int fallbackWhenZero)
        {
            if (ctrl == null)
            {
                return;
            }

            var p = Math.Max(0, Math.Min(100, percent));
            if (p > 0)
            {
                ctrl.Value = p;
            }
            else if (ctrl.Value <= 0)
            {
                ctrl.Value = fallbackWhenZero;
            }
        }

        private void ApplyLikeProbabilityToUi(int percent)
        {
            ApplyPercentToUi(numLikeProbability, percent, DefaultLikeProbability);
        }

        private void ApplyShareProbabilityToUi(int percent)
        {
            ApplyPercentToUi(numShareProbability, percent, DefaultShareProbability);
        }

        private void ApplyCommentProbabilityToUi(int percent)
        {
            var p = Math.Max(0, Math.Min(100, percent));
            if (chkAutoComment != null)
            {
                chkAutoComment.Checked = p > 0;
            }

            if (numCommentProbability != null)
            {
                numCommentProbability.Enabled = chkAutoComment == null || chkAutoComment.Checked;
                // Nếu tắt comment (0) vẫn giữ giá trị ô để bật lại không mất % đã chọn.
                if (p > 0)
                {
                    numCommentProbability.Value = p;
                }
                else if (numCommentProbability.Value <= 0)
                {
                    numCommentProbability.Value = DefaultCommentProbability;
                }
            }
        }

        private static DateTime? ToUtcFromUiLocal(DateTime? local)
        {
            if (!local.HasValue)
            {
                return null;
            }

            var dt = local.Value;
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            }

            return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }

        private static DateTime? ToLocalFromUtc(DateTime? utc)
        {
            if (!utc.HasValue)
            {
                return null;
            }

            var dt = utc.Value;
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            return dt.ToLocalTime();
        }

        // Settings (AppSettings) vẫn có WatchSeconds* legacy — không còn bind vào numWatch* (% giữ chân).
        private const int DefaultWatchMinutesMin = 5;
        private const int DefaultWatchMinutesMax = 10;

        private async void btnStartWarmupQueue_Click(object sender, EventArgs e)
        {
            await ExecuteWarmupRunFlowAsync(WarmupRunChoice.Cancel, skipPrompt: false).ConfigureAwait(true);
        }

        private async Task ExecuteWarmupRunFlowAsync(WarmupRunChoice forcedChoice, bool skipPrompt)
        {
            if (_isWarmupQueueRunning)
            {
                return;
            }

            var saved = await _warmupStateManager.LoadAsync().ConfigureAwait(true);
            var canResume = saved != null && saved.CompletedCount < saved.VideoCount;
            var selectedRows = GetSelectedWarmupQueueItemsInDisplayOrder();
            var runnableSelectedCount = selectedRows.Count(IsWarmupQueueRowRunnable);
            var allRunnableCount = CountRunnableQueueRows();

            WarmupRunChoice choice;
            if (skipPrompt && forcedChoice != WarmupRunChoice.Cancel)
            {
                choice = forcedChoice;
            }
            else if (allRunnableCount == 0 && !canResume)
            {
                LogWarmup("[QUEUE] Không có dòng sẵn sàng — thêm vào hàng đợi trước khi chạy.");
                return;
            }
            else
            {
                using (var dlg = new FormWarmupRunChoice(
                    canResume,
                    saved?.CompletedCount ?? 0,
                    saved?.VideoCount ?? 0,
                    runnableSelectedCount,
                    allRunnableCount))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Choice == WarmupRunChoice.Cancel)
                    {
                        return;
                    }

                    choice = dlg.Choice;
                }
            }

            switch (choice)
            {
                case WarmupRunChoice.Resume:
                    ApplyWarmupStateToUi(saved);
                    await RunWarmupAsync(saved, true).ConfigureAwait(true);
                    break;
                case WarmupRunChoice.SelectedRows:
                    await StartWarmupQueueAsync(selectedRows.Where(IsWarmupQueueRowRunnable).ToList()).ConfigureAwait(true);
                    break;
                case WarmupRunChoice.AllQueue:
                    await StartWarmupQueueAsync(null).ConfigureAwait(true);
                    break;
            }
        }

        private async Task StartWarmupQueueAsync(System.Collections.Generic.List<WarmupQueueUiItem> selectedRowsOnly)
        {
            if (_isWarmupQueueRunning)
            {
                return;
            }

            if (selectedRowsOnly != null && selectedRowsOnly.Count > 0)
            {
                var skippedNoKeywords = new System.Collections.Generic.List<WarmupQueueUiItem>();
                var rowsWithKeywords = new System.Collections.Generic.List<WarmupQueueUiItem>();
                foreach (var row in selectedRowsOnly)
                {
                    if (QueueRowHasKeywords(row))
                    {
                        rowsWithKeywords.Add(row);
                    }
                    else if (row != null)
                    {
                        skippedNoKeywords.Add(row);
                    }
                }

                foreach (var row in skippedNoKeywords)
                {
                    LogWarmup($"[QUEUE] WARNING: Dòng «{row.Profile}» chưa có từ khóa — bỏ qua.");
                }

                if (skippedNoKeywords.Count > 0)
                {
                    var skippedProfiles = string.Join(", ", skippedNoKeywords.Select(r => r.Profile));
                    MessageBox.Show(
                        $"{skippedNoKeywords.Count} dòng không có từ khóa và đã bỏ qua: {skippedProfiles}.\nBấm ô Keywords trên lưới để thêm rồi chạy lại.",
                        "Warm-up Queue",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                if (rowsWithKeywords.Count == 0)
                {
                    LogWarmup("[QUEUE] Không có dòng đã chọn nào có từ khóa để chạy.");
                    return;
                }

                var selectedSnapshots = BuildSnapshotsFromQueueRows(rowsWithKeywords, includeScheduledAsRunnable: true);
                if (selectedSnapshots.Count == 0)
                {
                    LogWarmup("[QUEUE] Không có dòng đã chọn nào có thể chạy.");
                    return;
                }

                _warmupQueueScheduler.ReplacePending(selectedSnapshots);
                LogWarmup($"[QUEUE] Chạy {selectedSnapshots.Count} dòng đã chọn (trên → dưới).");
            }
            else
            {
                SyncSchedulerFromUi();
                if (_warmupQueueScheduler.QueueCount == 0)
                {
                    LogWarmup("[QUEUE] No jobs to run.");
                    return;
                }

                LogWarmup($"[QUEUE] Chạy cả bảng ({_warmupQueueScheduler.QueueCount} job).");
            }

            _isWarmupQueueRunning = true;
            _isWarmupQueuePaused = false;
            _pauseNowRequested = false;
            _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
            btnStartWarmupQueue.Enabled = false;
            btnQueueWarmup.Enabled = false;
            ApplyWarmupQueueToolbarState();
            _warmupQueueCancellation?.Dispose();
            _warmupQueueCancellation = new CancellationTokenSource();
            RefreshWarmupQueueStatus();

            try
            {
                await _warmupQueueScheduler.RunAsync(
                    RunWarmupQueueJobAsync,
                    LogWarmup,
                    _warmupQueueCancellation.Token,
                    HandleQueueEvent,
                    _ => SaveWarmupQueueSnapshotAsync(),
                    WaitIfQueuePausedAsync).ConfigureAwait(true);
                LogWarmup("[QUEUE] All jobs processed.");
            }
            catch (OperationCanceledException)
            {
                LogWarmup("[QUEUE] Queue stopped by user.");
            }
            finally
            {
                _isWarmupQueueRunning = false;
                _isWarmupQueuePaused = false;
                _pauseNowRequested = false;
                btnStartWarmupQueue.Enabled = true;
                btnQueueWarmup.Enabled = true;
                ApplyWarmupQueueToolbarState();
                _warmupQueueCancellation?.Dispose();
                _warmupQueueCancellation = null;
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = null;
                await _warmupQueueStateManager.SavePausedFlagAsync(false).ConfigureAwait(true);
                await RefreshResumeStateAsync().ConfigureAwait(true);
                SyncSchedulerFromUi();
                RefreshWarmupQueueStatus();
                _ = SaveWarmupQueueSnapshotAsync();
            }
        }

        private static bool QueueRowHasKeywords(WarmupQueueUiItem row) =>
            !string.IsNullOrWhiteSpace(row?.Keywords);

        private static bool IsWarmupQueueRowRunnable(WarmupQueueUiItem row)
        {
            if (row == null)
            {
                return false;
            }

            return row.Status == WarmupStatus.Pending
                || row.Status == WarmupStatus.Failed
                || row.Status == WarmupStatus.Skipped
                || row.Status == WarmupStatus.Scheduled;
        }

        private int CountRunnableQueueRows()
        {
            if (_warmupQueueBindingList == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                if (IsWarmupQueueRowRunnable(_warmupQueueBindingList[i]))
                {
                    count++;
                }
            }

            return count;
        }
        private void btnQueueWarmup_Click(object sender, EventArgs e)
        {
            var state = BuildWarmupStateFromUi();

            const int defaultRetries = 2;
            var isScheduled = state.ScheduledAtUtc.HasValue;
            if (!isScheduled)
            {
                _warmupQueueScheduler.Enqueue(state, maxRetries: defaultRetries);
            }

            _warmupQueueBindingList.Add(new WarmupQueueUiItem
            {
                Profile = state.RunningProfileName,
                Keywords = state.Keywords,
                Videos = state.VideoCount,
                WatchRange = FormatWatchRangeForQueue(state.WatchPercentageMin, state.WatchPercentageMax),
                LikeProbability = state.LikeProbability,
                ShareProbability = state.ShareProbability,
                CommentProbability = state.CommentProbability,
                DryRun = state.DryRun,
                Status = isScheduled ? WarmupStatus.Scheduled : WarmupStatus.Pending,
                ScheduledAtUtc = state.ScheduledAtUtc,
                RetryCount = 0,
                MaxRetries = defaultRetries,
                LastError = string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                NextRetryAtUtc = null
            });
            RefreshWarmupQueueStatus();
            LogWarmup(isScheduled
                ? $"[QUEUE] Đã lập lịch warm-up profile '{state.RunningProfileName}' lúc {ToLocalFromUtc(state.ScheduledAtUtc):dd/MM/yyyy HH:mm}."
                : string.IsNullOrWhiteSpace(state.Keywords)
                    ? $"[QUEUE] Added warm-up job for profile '{state.RunningProfileName}' (For You — chưa có từ khóa)."
                    : $"[QUEUE] Added warm-up job for profile '{state.RunningProfileName}'.");
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private void WarmupScheduleTimer_Tick(object sender, EventArgs e)
        {
            if (_warmupQueueBindingList == null || _warmupQueueBindingList.Count == 0)
            {
                return;
            }

            var promoted = false;
            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                var item = _warmupQueueBindingList[i];
                if (item == null ||
                    item.Status != WarmupStatus.Scheduled ||
                    !item.ScheduledAtUtc.HasValue ||
                    item.ScheduledAtUtc.Value > DateTime.UtcNow)
                {
                    continue;
                }

                item.Status = WarmupStatus.Pending;
                item.ScheduledAtUtc = null;
                promoted = true;
            }

            if (!promoted)
            {
                return;
            }

            SyncSchedulerFromUi();
            _ = SaveWarmupQueueSnapshotAsync();
            dgvWarmupQueue?.Refresh();
            RefreshWarmupQueueStatus();
            LogWarmup("[QUEUE] Đã đến giờ — chuyển job lập lịch sang Pending. Bấm «Chạy» để bắt đầu.");
        }

        private void btnStopWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_warmupQueueCancellation == null && _warmupCancellation == null)
            {
                return;
            }

            CancelWarmupBrowserWork();
            _pauseNowRequested = false;
            _isWarmupQueuePaused = false;
            LogWarmup(_isWarmupQueueRunning ? "[QUEUE] Dừng hẳn hàng đợi..." : "[QUEUE] Dừng hẳn warm-up.");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
            ApplyWarmupQueueToolbarState();
        }

        private void btnPauseWarmupQueue_Click(object sender, EventArgs e)
        {
            if (!_isWarmupQueueRunning)
            {
                return;
            }

            if (_isWarmupQueuePaused)
            {
                _isWarmupQueuePaused = false;
                RefreshWarmupQueueStatus();
                LogWarmup("[QUEUE] Queue resumed.");
                _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
                ApplyWarmupQueueToolbarState();
                return;
            }

            _isWarmupQueuePaused = true;
            _pauseNowRequested = true;
            _currentQueueJobCancellation?.Cancel();
            var pausedRow = FindQueueRow(_currentWarmupState);
            if (pausedRow != null)
            {
                pausedRow.Status = WarmupStatus.Pending;
            }

            dgvWarmupQueue?.Refresh();
            RefreshWarmupQueueStatus();
            LogWarmup("[QUEUE] Tạm dừng — dừng job hiện tại. Bấm «Tiếp tục» để chạy lại (job dở sẽ ở đầu hàng).");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(true);
            ApplyWarmupQueueToolbarState();
        }

        private void WarmupPauseQueueNow_Click(object sender, EventArgs e)
        {
            if (!_isWarmupQueueRunning)
            {
                return;
            }

            _pauseNowRequested = true;
            _isWarmupQueuePaused = true;
            _currentQueueJobCancellation?.Cancel();
            RefreshWarmupQueueStatus();
            LogWarmup("[QUEUE] Pause-now requested. Current job will be re-queued from latest progress.");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(true);
            ApplyWarmupQueueToolbarState();
        }

        private void btnRemoveQueueJob_Click(object sender, EventArgs e)
        {
            RemoveSelectedWarmupQueueRows();
        }

        private void dgvWarmupQueue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete || e.Alt || e.Control)
            {
                return;
            }

            RemoveSelectedWarmupQueueRows();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void RemoveSelectedWarmupQueueRows()
        {
            var selected = GetSelectedWarmupQueueItemsInDisplayOrder();
            if (selected.Count == 0)
            {
                LogWarmup("[QUEUE] Please select a queue job to remove.");
                return;
            }

            if (_isWarmupQueueRunning)
            {
                LogWarmup("[QUEUE] Stop queue before removing jobs.");
                return;
            }

            if (!UiConfirmHelper.ConfirmDeleteRows(this, selected.Count))
            {
                return;
            }

            foreach (var item in selected)
            {
                _warmupQueueBindingList.Remove(item);
            }

            SyncSchedulerFromUi();
            RefreshWarmupQueueStatus();
            LogWarmup($"[QUEUE] Removed {selected.Count} job(s).");
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private void btnClearWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                LogWarmup("[QUEUE] Stop queue before clearing.");
                return;
            }

            _warmupQueueScheduler.ClearPending();
            _warmupQueueBindingList?.Clear();
            RefreshWarmupQueueStatus();
            LogWarmup("[QUEUE] Cleared all pending jobs.");
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private void btnMoveQueueJobUp_Click(object sender, EventArgs e)
        {
            MoveSelectedQueueRow(-1);
        }

        private void btnMoveQueueJobDown_Click(object sender, EventArgs e)
        {
            MoveSelectedQueueRow(1);
        }

        private async Task RunWarmupAsync(WarmupRunState state, bool isResume)
        {
            if (!state.DryRun && Services.WarmupBuildInfo.IsRunningStaleBuild(out var staleMsg))
            {
                LogWarmup("[LIVE] ERROR: " + staleMsg);
                MessageBox.Show(
                    staleMsg,
                    "Can restart app",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            btnStartWarmupQueue.Enabled = false;
            btnQueueWarmup.Enabled = false;
            _warmupCancellation?.Dispose();
            _warmupCancellation = RegisterActiveJobCancellation();
            ApplyWarmupQueueToolbarState();
            _currentWarmupState = state;
            UpdateWarmupProgress(state.CompletedCount, state.VideoCount);
            await _warmupStateManager.SaveAsync(state).ConfigureAwait(true);

            try
            {
                LogWarmup(isResume
                    ? $"Resuming warm-up from {state.CompletedCount}/{state.VideoCount}..."
                    : "Warm-up started...");
                await _tikTokAutomation.StartWarmupAsync(
                    state,
                    _warmupCancellation.Token,
                    LogWarmup,
                    HandleWarmupProgressUpdate);
                LogWarmup("Warm-up completed.");
                await _warmupStateManager.ClearAsync();
                _currentWarmupState = null;
            }
            catch (OperationCanceledException)
            {
                LogWarmup("Warm-up stopped by user.");
            }
            catch (Exception ex)
            {
                LogWarmup("Warm-up failed: " + ex.Message);
            }
            finally
            {
                if (!_isWarmupQueueRunning)
                {
                    btnStartWarmupQueue.Enabled = true;
                    btnQueueWarmup.Enabled = true;
                }

                _warmupCancellation = null;
                DisposeActiveJobCancellation();
                ApplyWarmupQueueToolbarState();
                await RefreshResumeStateAsync().ConfigureAwait(true);
            }
        }

        private void btnStopWarmup_Click(object sender, EventArgs e)
        {
            if (_warmupCancellation == null)
            {
                return;
            }

            CancelWarmupBrowserWork();
            ApplyWarmupQueueToolbarState();
            LogWarmup("Stopping warm-up...");
        }

        private async Task RunWarmupQueueJobAsync(WarmupRunState state, CancellationToken cancellationToken)
        {
            if (state == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(state.Keywords))
            {
                var profile = string.IsNullOrWhiteSpace(state.RunningProfileName) ? "default" : state.RunningProfileName.Trim();
                var noKeywordsMessage = $"[QUEUE] Dòng «{profile}» chưa có từ khóa — bấm ô Keywords trên lưới để thêm rồi chạy lại.";
                LogWarmup(noKeywordsMessage);
                MessageBox.Show(
                    noKeywordsMessage,
                    "Warm-up Queue",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                throw new ArgumentException(noKeywordsMessage);
            }

            if (!state.DryRun && Services.WarmupBuildInfo.IsRunningStaleBuild(out var staleQueueMsg))
            {
                LogWarmup("[LIVE] ERROR: " + staleQueueMsg);
                MessageBox.Show(staleQueueMsg, "Can restart app", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var queueRow = FindQueueRow(state);
            if (queueRow != null &&
                queueRow.Status == WarmupStatus.Scheduled)
            {
                return;
            }

            await WaitIfQueuePausedAsync(cancellationToken);

            _currentWarmupState = state;
            UpdateWarmupProgress(0, state.VideoCount);
            await _warmupStateManager.SaveAsync(state);
            RefreshWarmupQueueStatus();

            try
            {
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await _tikTokAutomation.StartWarmupAsync(
                    state,
                    _currentQueueJobCancellation.Token,
                    LogWarmup,
                    HandleWarmupProgressUpdate);
                await _warmupStateManager.ClearAsync();
            }
            catch (OperationCanceledException) when (_pauseNowRequested && !IsWarmupQueueStopRequested())
            {
                _pauseNowRequested = false;
                if (state.CompletedCount < state.VideoCount)
                {
                    var resumeState = CloneWarmupState(state);
                    _warmupQueueScheduler.EnqueueFront(resumeState, 2);
                    var existingRow = FindQueueRow(state);
                    if (existingRow != null)
                    {
                        existingRow.Status = WarmupStatus.Pending;
                        existingRow.LastError = string.Empty;
                    }
                    else
                    {
                        _warmupQueueBindingList?.Insert(0, new WarmupQueueUiItem
                        {
                            Profile = resumeState.RunningProfileName,
                            Keywords = resumeState.Keywords,
                            Videos = resumeState.VideoCount,
                            WatchRange = FormatWatchRangeForQueue(resumeState.WatchPercentageMin, resumeState.WatchPercentageMax),
                            LikeProbability = resumeState.LikeProbability,
                            ShareProbability = resumeState.ShareProbability,
                            CommentProbability = resumeState.CommentProbability,
                            DryRun = resumeState.DryRun,
                            Status = WarmupStatus.Pending,
                            RetryCount = 0,
                            MaxRetries = 2,
                            LastError = string.Empty,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                    }

                    _ = SaveWarmupQueueSnapshotAsync();
                }

                throw new WarmupQueueRequeueException("Paused now and re-queued.");
            }
            finally
            {
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = null;
                if (!_isWarmupQueuePaused)
                {
                    _currentWarmupState = null;
                }

                RefreshWarmupQueueStatus();
            }
        }

        private bool IsWarmupQueueStopRequested()
        {
            return (_warmupQueueCancellation?.IsCancellationRequested ?? false)
                || (_warmupCancellation?.IsCancellationRequested ?? false);
        }

        private async Task WaitIfQueuePausedAsync(CancellationToken cancellationToken)
        {
            while (_isWarmupQueuePaused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken);
            }
        }

        private void HandleQueueEvent(WarmupQueueRunEvent eventInfo)
        {
            if (eventInfo == null || _warmupQueueBindingList == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<WarmupQueueRunEvent>(HandleQueueEvent), eventInfo);
                return;
            }

            var row = FindQueueRow(eventInfo.State);
            if (row == null)
            {
                return;
            }

            if (eventInfo.EventType == WarmupQueueEventType.Started)
            {
                row.Status = WarmupStatus.Running; row.RetryAttempt = 0;
            }
            else if (eventInfo.EventType == WarmupQueueEventType.Retrying)
            {
                row.Status = WarmupStatus.Running; row.RetryAttempt = eventInfo.Attempt;
                row.RetryCount = Math.Max(row.RetryCount, eventInfo.Attempt);
                row.LastError = eventInfo.ErrorMessage ?? string.Empty;
                row.NextRetryAtUtc = eventInfo.NextRetryAtUtc ?? DateTime.UtcNow.AddMinutes(5);
            }
            else if (eventInfo.EventType == WarmupQueueEventType.Requeued)
            {
                row.Status = WarmupStatus.Pending;
                row.LastError = string.Empty;
            }
            else if (eventInfo.EventType == WarmupQueueEventType.Completed ||
                     eventInfo.EventType == WarmupQueueEventType.FailedPermanent ||
                     eventInfo.EventType == WarmupQueueEventType.Skipped)
            {
                if (eventInfo.EventType == WarmupQueueEventType.FailedPermanent ||
                    eventInfo.EventType == WarmupQueueEventType.Skipped)
                {
                    row.LastError = eventInfo.ErrorMessage ?? string.Empty;
                }

                if (eventInfo.EventType == WarmupQueueEventType.Completed)
                {
                    row.Status = WarmupStatus.Completed;
                    row.LastError = string.Empty;
                }
                else if (eventInfo.EventType == WarmupQueueEventType.FailedPermanent)
                {
                    row.Status = WarmupStatus.Failed;
                }
                else
                {
                    row.Status = WarmupStatus.Skipped;
                }

                row.RetryAttempt = 0;
                row.NextRetryAtUtc = null;
                _ = AppendQueueHistoryAsync(eventInfo, row);
                _ = SaveWarmupQueueSnapshotAsync();
            }

            dgvWarmupQueue?.Refresh();
            RefreshWarmupQueueStatus();
        }
        private void UpdateWarmupProgress(int completed, int total)
        {
            if (pbWarmupProgress.IsDisposed || lblWarmupProgress.IsDisposed)
            {
                return;
            }

            if (pbWarmupProgress.InvokeRequired || lblWarmupProgress.InvokeRequired)
            {
                pbWarmupProgress.Invoke(new Action<int, int>(UpdateWarmupProgress), completed, total);
                return;
            }

            if (total <= 0)
            {
                pbWarmupProgress.Value = 0;
                lblWarmupProgress.Text = "Tiến độ: 0/0 (0%)";
                return;
            }

            var safeCompleted = Math.Max(0, Math.Min(completed, total));
            var percent = (int)Math.Round((double)safeCompleted * 100 / total);
            pbWarmupProgress.Value = Math.Max(0, Math.Min(percent, 100));
            lblWarmupProgress.Text = $"Tiến độ: {safeCompleted}/{total} ({percent}%)";
        }

        private async void HandleWarmupProgressUpdate(int completed, int total)
        {
            UpdateWarmupProgress(completed, total);

            if (_currentWarmupState == null)
            {
                return;
            }

            _currentWarmupState.CompletedCount = completed;
            _currentWarmupState.VideoCount = total;
            _currentWarmupState.LastUpdatedUtc = DateTime.UtcNow;
            try
            {
                await _warmupStateManager.SaveAsync(_currentWarmupState).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogWarmup("[WARN] Không lưu warmup_state: " + ex.Message);
            }
        }

        private async Task RefreshResumeStateAsync()
        {
            var state = await _warmupStateManager.LoadAsync();
            var canResume = state != null && state.CompletedCount < state.VideoCount;
            if (canResume && !_isWarmupQueueRunning && _warmupCancellation == null && lblWarmupQueueStatus != null)
            {
                RefreshWarmupQueueStatus();
            }
        }

        private void ApplyWarmupStateToUi(WarmupRunState state)
        {
            txtKeywords.Text = state.Keywords;
            SelectRunningProfileInUi(state.RunningProfileName);
            numVideoCount.Value = Math.Max(numVideoCount.Minimum, Math.Min(numVideoCount.Maximum, state.VideoCount));

            // SỬA CHỖ NÀY: Dùng hàm ApplyWatchPercentageToUi để ép UI hiện đúng số an toàn
            ApplyWatchPercentageToUi(numWatchMin, state.WatchPercentageMin);
            ApplyWatchPercentageToUi(numWatchMax, state.WatchPercentageMax);
            ApplyLikeProbabilityToUi(state.LikeProbability);
            ApplyShareProbabilityToUi(state.ShareProbability);
            ApplyCommentProbabilityToUi(state.CommentProbability);

            rbDryRun.Checked = state.DryRun;
            rbLiveRun.Checked = !state.DryRun;
            UpdateWarmupProgress(state.CompletedCount, state.VideoCount);
        }

        private WarmupRunState BuildWarmupStateFromUi()
        {
            DateTime? scheduledAt = null;
            if (chkEnableWarmupSchedule != null && chkEnableWarmupSchedule.Checked && dtpWarmupSchedule != null)
            {
                // CHUYỂN SANG UTC KHI LƯU VÀO STATE
                scheduledAt = dtpWarmupSchedule.Value.ToUniversalTime();
            }

            var watchMin = ReadWatchPercentageFromUi(numWatchMin, DefaultWatchPercentageMin);
            var watchMax = Math.Max(watchMin, ReadWatchPercentageFromUi(numWatchMax, DefaultWatchPercentageMax));

            return new WarmupRunState
            {
                Keywords = txtKeywords.Text.Trim(),
                RunningProfileName = cbRunningProfile?.SelectedItem?.ToString() ?? "default",
                VideoCount = (int)numVideoCount.Value,

                // Percent và Probability từ UI (numWatchMin/Max = giữ chân %)
                WatchPercentageMin = watchMin,
                WatchPercentageMax = watchMax,
                LikeProbability = ReadLikeProbabilityFromUi(),
                CommentProbability = ReadCommentProbabilityFromUi(),
                ShareProbability = ReadShareProbabilityFromUi(),

                DryRun = rbDryRun.Checked,
                CompletedCount = 0,
                LastUpdatedUtc = DateTime.UtcNow,
                ScheduledAtUtc = scheduledAt // Đã đổi tên
            };
        }
        private void RefreshWarmupQueueStatus()
        {
            if (lblWarmupQueueStatus == null || lblWarmupQueueStatus.IsDisposed)
            {
                return;
            }

            if (lblWarmupQueueStatus.InvokeRequired)
            {
                lblWarmupQueueStatus.Invoke(new Action(RefreshWarmupQueueStatus));
                return;
            }

            var queueCount = _warmupQueueScheduler?.QueueCount ?? 0;
            var scheduledCount = 0;
            if (_warmupQueueBindingList != null)
            {
                for (var i = 0; i < _warmupQueueBindingList.Count; i++)
                {
                    var row = _warmupQueueBindingList[i];
                    if (row != null && row.Status == WarmupStatus.Scheduled)
                    {
                        scheduledCount++;
                    }
                }
            }

            var runningText = _isWarmupQueueRunning ? (_isWarmupQueuePaused ? "paused" : "running") : "idle";
            lblWarmupQueueStatus.Text = scheduledCount > 0
                ? $"Queue: {queueCount} sẵn sàng, {scheduledCount} đã hẹn giờ — {runningText}"
                : $"Queue: {queueCount} job(s) - {runningText}";
            ApplyWarmupQueueToolbarState();
        }

        private void PersistWarmupQueueOnAppExit()
        {
            try
            {
                var saveTask = Task.Run(async () =>
                {
                    if ((_warmupQueueScheduler?.QueueCount ?? 0) > 0 || _isWarmupQueueRunning)
                    {
                        await _warmupQueueStateManager.SavePausedFlagAsync(true).ConfigureAwait(false);
                    }

                    await SaveWarmupQueueSnapshotAsync().ConfigureAwait(false);
                });
                if (!saveTask.Wait(TimeSpan.FromSeconds(3)))
                {
                    // Không chặn thoát app nếu ghi file chậm.
                }
            }
            catch
            {
                // Best-effort on exit.
            }
        }

        private async Task LoadWarmupQueueAsync()
        {
            try
            {
                var snapshots = await _warmupQueueStateManager.LoadAsync();
                _warmupQueueBindingList?.Clear();
                foreach (var snapshot in snapshots)
                {
                    if (snapshot?.State == null)
                    {
                        continue;
                    }

                    var scheduledAtUtc = snapshot.State.ScheduledAtUtc;
                    var isScheduled = scheduledAtUtc.HasValue && scheduledAtUtc.Value > DateTime.UtcNow;
                    _warmupQueueBindingList.Add(new WarmupQueueUiItem
                    {
                        Profile = snapshot.State.RunningProfileName,
                        Keywords = snapshot.State.Keywords,
                        Videos = snapshot.State.VideoCount,
                        WatchRange = FormatWatchRangeForQueue(snapshot.State.WatchPercentageMin, snapshot.State.WatchPercentageMax),
                        LikeProbability = snapshot.State.LikeProbability,
                        ShareProbability = snapshot.State.ShareProbability,
                        CommentProbability = snapshot.State.CommentProbability,
                        DryRun = snapshot.State.DryRun,
                        Status = isScheduled ? WarmupStatus.Scheduled : WarmupStatus.Pending,
                        ScheduledAtUtc = isScheduled ? scheduledAtUtc : null,
                        RetryCount = snapshot.RetryCount,
                        MaxRetries = snapshot.MaxRetries,
                        LastError = snapshot.LastError ?? string.Empty,
                        CreatedAtUtc = snapshot.CreatedAtUtc == default(DateTime) ? DateTime.UtcNow : snapshot.CreatedAtUtc,
                        NextRetryAtUtc = snapshot.NextRetryAtUtc
                    });
                }

                SyncSchedulerFromUi();
                RefreshWarmupQueueStatus();
            }
            catch (Exception ex)
            {
                LogWarmup("[QUEUE] Failed to load queue state: " + ex.Message);
            }
        }

        private async Task SaveWarmupQueueSnapshotAsync()
        {
            try
            {
                var snapshots = BuildSnapshotsFromUi(includeScheduledRows: true);
                await _warmupQueueStateManager.SaveAsync(snapshots);
            }
            catch (Exception ex)
            {
                LogWarmup("[QUEUE] Failed to persist queue state: " + ex.Message);
            }
        }

        private void SyncSchedulerFromUi()
        {
            var snapshots = BuildSnapshotsFromUi(includeScheduledRows: false);
            _warmupQueueScheduler.ReplacePending(snapshots);
            RefreshWarmupQueueStatus();
        }

        private List<WarmupQueueUiItem> GetSelectedWarmupQueueItemsInDisplayOrder()
        {
            var result = new List<WarmupQueueUiItem>();
            if (dgvWarmupQueue?.Rows == null || dgvWarmupQueue.SelectedRows == null || dgvWarmupQueue.SelectedRows.Count == 0)
            {
                return result;
            }

            var selected = new HashSet<WarmupQueueUiItem>();
            foreach (DataGridViewRow row in dgvWarmupQueue.SelectedRows)
            {
                if (row?.DataBoundItem is WarmupQueueUiItem item)
                {
                    selected.Add(item);
                }
            }

            if (selected.Count == 0)
            {
                return result;
            }

            foreach (DataGridViewRow row in dgvWarmupQueue.Rows)
            {
                if (row?.DataBoundItem is WarmupQueueUiItem item && selected.Contains(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private List<WarmupQueueSnapshotItem> BuildSnapshotsFromQueueRows(
            IEnumerable<WarmupQueueUiItem> rows,
            bool includeScheduledAsRunnable = false)
        {
            var snapshots = new List<WarmupQueueSnapshotItem>();
            if (rows == null)
            {
                return snapshots;
            }

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                if (row.Status == WarmupStatus.Completed || row.Status == WarmupStatus.Running)
                {
                    continue;
                }

                if (row.Status == WarmupStatus.Scheduled)
                {
                    if (!includeScheduledAsRunnable)
                    {
                        continue;
                    }

                    row.Status = WarmupStatus.Pending;
                    row.ScheduledAtUtc = null;
                }
                else if (!IsWarmupQueueRunnableStatus(row.Status))
                {
                    continue;
                }

                var state = BuildStateFromQueueRow(row);
                if (includeScheduledAsRunnable)
                {
                    state.ScheduledAtUtc = null;
                }

                snapshots.Add(new WarmupQueueSnapshotItem
                {
                    State = state,
                    MaxRetries = row.MaxRetries < 0 ? 0 : row.MaxRetries,
                    CreatedAtUtc = row.CreatedAtUtc == default(DateTime) ? DateTime.UtcNow : row.CreatedAtUtc,
                    RetryCount = Math.Max(0, row.RetryCount),
                    LastError = row.LastError ?? string.Empty,
                    NextRetryAtUtc = row.NextRetryAtUtc
                });
            }

            return snapshots;
        }

        private static bool IsWarmupQueueRunnableStatus(WarmupStatus status)
        {
            return status == WarmupStatus.Pending
                || status == WarmupStatus.Failed
                || status == WarmupStatus.Skipped;
        }

        private List<WarmupQueueSnapshotItem> BuildSnapshotsFromUi(bool includeScheduledRows = true)
        {
            var snapshots = new List<WarmupQueueSnapshotItem>();
            if (_warmupQueueBindingList == null)
            {
                return snapshots;
            }

            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                var row = _warmupQueueBindingList[i];
                if (row == null)
                {
                    continue;
                }

                var isScheduledRow = row.Status == WarmupStatus.Scheduled;
                if (!includeScheduledRows)
                {
                    if (isScheduledRow || !IsWarmupQueueRunnableStatus(row.Status))
                    {
                        continue;
                    }
                }

                var state = BuildStateFromQueueRow(row);
                if (isScheduledRow)
                {
                    state.ScheduledAtUtc = row.ScheduledAtUtc;
                }

                snapshots.Add(new WarmupQueueSnapshotItem
                {
                    State = state,
                    MaxRetries = row.MaxRetries < 0 ? 0 : row.MaxRetries,
                    CreatedAtUtc = row.CreatedAtUtc == default(DateTime) ? DateTime.UtcNow : row.CreatedAtUtc,
                    RetryCount = Math.Max(0, row.RetryCount),
                    LastError = row.LastError ?? string.Empty,
                    NextRetryAtUtc = row.NextRetryAtUtc
                });
            }

            return snapshots;
        }

        private WarmupQueueUiItem FindQueueRow(WarmupRunState state)
        {
            if (state == null || _warmupQueueBindingList == null)
            {
                return null;
            }

            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                var row = _warmupQueueBindingList[i];
                if (row == null)
                {
                    continue;
                }

                ParseWatchRangeToPercent(row.WatchRange, out var rowWatchMin, out var rowWatchMax);
                if (string.Equals(row.Profile ?? string.Empty, state.RunningProfileName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(row.Keywords ?? string.Empty, state.Keywords ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    row.Videos == state.VideoCount &&
                    rowWatchMin == state.WatchPercentageMin &&
                    rowWatchMax == state.WatchPercentageMax)
                {
                    return row;
                }
            }

            return null;
        }

        private WarmupRunState BuildStateFromQueueRow(WarmupQueueUiItem row)
        {
            ParseWatchRangeToPercent(row?.WatchRange, out var watchMin, out var watchMax);

            return new WarmupRunState
            {
                RunningProfileName = row?.Profile ?? "default",
                Keywords = row?.Keywords ?? string.Empty,
                VideoCount = row?.Videos ?? 0,
                WatchPercentageMin = watchMin,
                WatchPercentageMax = watchMax,
                LikeProbability = row?.LikeProbability ?? ReadLikeProbabilityFromUi(),
                CommentProbability = row?.CommentProbability ?? ReadCommentProbabilityFromUi(),
                ShareProbability = row?.ShareProbability ?? ReadShareProbabilityFromUi(),
                DryRun = row?.DryRun ?? false,
                ScheduledAtUtc = row?.ScheduledAtUtc
            };
        }

        private static WarmupRunState CloneWarmupState(WarmupRunState source)
        {
            if (source == null)
            {
                return new WarmupRunState();
            }

            return new WarmupRunState
            {
                Keywords = source.Keywords,
                RunningProfileName = source.RunningProfileName,
                VideoCount = source.VideoCount,
                WatchPercentageMin = source.WatchPercentageMin,
                WatchPercentageMax = source.WatchPercentageMax,
                LikeProbability = source.LikeProbability,
                CommentProbability = source.CommentProbability,
                ShareProbability = source.ShareProbability,
                DryRun = source.DryRun,
                CompletedCount = source.CompletedCount,
                LastUpdatedUtc = source.LastUpdatedUtc,
                ScheduledAtUtc = source.ScheduledAtUtc
            };
        }

        private void dgvWarmupQueue_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgvWarmupQueue == null)
            {
                return;
            }

            var column = dgvWarmupQueue.Columns[e.ColumnIndex];
            if (column == null ||
                (!string.Equals(column.Name, "colWarmupScheduledAt", StringComparison.Ordinal) &&
                 !string.Equals(column.DataPropertyName, "ScheduledAtLabel", StringComparison.Ordinal)))
            {
                return;
            }

            var row = dgvWarmupQueue.Rows[e.RowIndex]?.DataBoundItem as WarmupQueueUiItem;
            if (row == null)
            {
                return;
            }

            OpenWarmupSchedulePicker(row, e.RowIndex);
        }

        private void OpenWarmupSchedulePicker(WarmupQueueUiItem row, int rowIndex)
        {
            if (row == null)
            {
                return;
            }

            if (_isWarmupQueueRunning && row.Status == WarmupStatus.Running)
            {
                LogWarmup("[QUEUE] Không đổi lịch khi job đang chạy.");
                return;
            }

            using (var dlg = new FormSchedulePicker(ToLocalFromUtc(row.ScheduledAtUtc), row.Profile))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplyWarmupScheduleFromPicker(row, dlg.ResultScheduledAt);
                if (rowIndex >= 0 && rowIndex < dgvWarmupQueue.Rows.Count)
                {
                    dgvWarmupQueue.InvalidateRow(rowIndex);
                }
                else
                {
                    dgvWarmupQueue.Refresh();
                }

                var listIndex = _warmupQueueBindingList?.IndexOf(row) ?? -1;
                if (listIndex >= 0)
                {
                    _warmupQueueBindingList.ResetItem(listIndex);
                }

                SyncSchedulerFromUi();
                RefreshWarmupQueueStatus();
                _ = SaveWarmupQueueSnapshotAsync();
            }
        }

        private void ApplyWarmupScheduleFromPicker(WarmupQueueUiItem row, DateTime? scheduledAt)
        {
            if (row == null)
            {
                return;
            }

            if (row.Status == WarmupStatus.Running ||
                row.Status == WarmupStatus.Completed)
            {
                return;
            }

            if (!scheduledAt.HasValue || scheduledAt.Value <= DateTime.Now.AddMinutes(1))
            {
                row.ScheduledAtUtc = null;
                if (row.Status == WarmupStatus.Scheduled)
                {
                    row.Status = WarmupStatus.Pending;
                }

                LogWarmup($"[QUEUE] '{row.Profile}' → Đăng ngay.");
                return;
            }

            // Picker trả giờ local → lưu UTC
            row.ScheduledAtUtc = ToUtcFromUiLocal(scheduledAt);
            row.Status = WarmupStatus.Scheduled;
            LogWarmup($"[QUEUE] '{row.Profile}' lập lịch lúc {scheduledAt:dd/MM/yyyy HH:mm}.");
        }

        private void dgvWarmupQueue_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgvWarmupQueue == null)
            {
                return;
            }

            var column = dgvWarmupQueue.Columns[e.ColumnIndex];
            if (column?.ReadOnly == true)
            {
                e.Cancel = true;
                return;
            }

            var row = dgvWarmupQueue.Rows[e.RowIndex]?.DataBoundItem as WarmupQueueUiItem;
            if (row == null)
            {
                e.Cancel = true;
                return;
            }

            if (row.Status == WarmupStatus.Running || row.Status == WarmupStatus.Completed)
            {
                LogWarmup("[QUEUE] Không sửa job đang chạy hoặc đã xong.");
                e.Cancel = true;
            }
        }

        private void dgvWarmupQueue_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            var columnName = e.ColumnIndex >= 0 && dgvWarmupQueue != null
                ? dgvWarmupQueue.Columns[e.ColumnIndex]?.HeaderText ?? "?"
                : "?";
            LogWarmup("[QUEUE] Giá trị không hợp lệ ở cột «" + columnName + "».");
        }

        private void dgvWarmupQueue_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgvWarmupQueue == null || _warmupQueueBindingList == null)
            {
                return;
            }

            var column = dgvWarmupQueue.Columns[e.ColumnIndex];
            var row = dgvWarmupQueue.Rows[e.RowIndex]?.DataBoundItem as WarmupQueueUiItem;
            if (column == null || row == null || column.ReadOnly)
            {
                return;
            }

            var prop = column.DataPropertyName ?? string.Empty;
            NormalizeWarmupQueueRow(row, prop);

            var listIndex = _warmupQueueBindingList.IndexOf(row);
            if (listIndex >= 0)
            {
                _warmupQueueBindingList.ResetItem(listIndex);
            }

            dgvWarmupQueue.InvalidateRow(e.RowIndex);
            SyncSchedulerFromUi();
            RefreshWarmupQueueStatus();
            _ = SaveWarmupQueueSnapshotAsync();
            LogWarmup("[QUEUE] Đã cập nhật «" + row.Profile + "» — " + column.HeaderText + ".");
        }

        private static void NormalizeWarmupQueueRow(WarmupQueueUiItem row, string editedProperty)
        {
            if (row == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "Profile", StringComparison.Ordinal))
            {
                row.Profile = string.IsNullOrWhiteSpace(row.Profile) ? "default" : row.Profile.Trim();
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "Keywords", StringComparison.Ordinal))
            {
                row.Keywords = (row.Keywords ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "Videos", StringComparison.Ordinal))
            {
                row.Videos = Math.Max(1, Math.Min(1000, row.Videos));
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "WatchRange", StringComparison.Ordinal))
            {
                ParseWatchRangeToPercent(row.WatchRange, out var watchMin, out var watchMax);
                row.WatchRange = FormatWatchRangeForQueue(watchMin, watchMax);
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "LikeProbability", StringComparison.Ordinal))
            {
                row.LikeProbability = Math.Max(0, Math.Min(100, row.LikeProbability));
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "ShareProbability", StringComparison.Ordinal))
            {
                row.ShareProbability = Math.Max(0, Math.Min(100, row.ShareProbability));
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "CommentProbability", StringComparison.Ordinal))
            {
                row.CommentProbability = Math.Max(0, Math.Min(100, row.CommentProbability));
            }

            if (string.IsNullOrEmpty(editedProperty) ||
                string.Equals(editedProperty, "RunModeLabel", StringComparison.Ordinal))
            {
                row.RunModeLabel = string.IsNullOrWhiteSpace(row.RunModeLabel) ? "Live" : row.RunModeLabel.Trim();
            }
        }
        private async Task AppendQueueHistoryAsync(WarmupQueueRunEvent eventInfo, WarmupQueueUiItem row)
        {
            try
            {
                var record = new WarmupQueueHistoryRecord
                {
                    FinishedAtUtc = DateTime.UtcNow,
                    Profile = row?.Profile ?? string.Empty,
                    Keywords = row?.Keywords ?? string.Empty,
                    Videos = row?.Videos ?? 0,
                    Result = eventInfo.EventType == WarmupQueueEventType.Completed
                        ? "Completed"
                        : (eventInfo.EventType == WarmupQueueEventType.Skipped ? "Skipped" : "Failed"),
                    Error = (eventInfo.EventType == WarmupQueueEventType.FailedPermanent || eventInfo.EventType == WarmupQueueEventType.Skipped)
                        ? (eventInfo.ErrorMessage ?? string.Empty)
                        : string.Empty,
                    Attempts = Math.Max(1, eventInfo.Attempt)
                };

                _warmupQueueHistory.Insert(0, record);
                if (_warmupQueueHistory.Count > 300)
                {
                    _warmupQueueHistory.RemoveRange(300, _warmupQueueHistory.Count - 300);
                }

                await _warmupQueueHistoryManager.AppendAsync(record);
                RefreshQueueStatsSummary();
            }
            catch (Exception ex)
            {
                LogWarmup("[QUEUE] Failed to append history: " + ex.Message);
            }
        }

        private async void btnRefreshQueueStats_Click(object sender, EventArgs e)
        {
            _warmupQueueHistory = await _warmupQueueHistoryManager.LoadAsync();
            RefreshQueueStatsSummary();
            LogWarmup("[QUEUE] Stats refreshed.");
        }

        private void btnOpenQueueHistory_Click(object sender, EventArgs e)
        {
            var records = _warmupQueueHistory ?? new List<WarmupQueueHistoryRecord>();
            var form = new Form
            {
                Text = "Warm-up Queue History",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1080, 520),
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro
            };

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            var lblSearch = new Label
            {
                Text = "Search",
                AutoSize = true,
                Location = new Point(12, 18),
                ForeColor = Color.Gainsboro
            };
            var txtSearch = new TextBox
            {
                Location = new Point(66, 14),
                Size = new Size(240, 28),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            var lblResult = new Label
            {
                Text = "Result",
                AutoSize = true,
                Location = new Point(320, 18),
                ForeColor = Color.Gainsboro
            };
            var cbResult = new ComboBox
            {
                Location = new Point(368, 14),
                Size = new Size(120, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbResult.Items.AddRange(new object[] { "All", "Completed", "Failed", "Skipped" });
            cbResult.SelectedIndex = 0;
            var cbRange = new ComboBox
            {
                Location = new Point(640, 14),
                Size = new Size(100, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };
            cbRange.Items.AddRange(new object[] { "Today", "7d", "30d", "Custom" });
            cbRange.SelectedIndex = 0;
            var dtFrom = new DateTimePicker
            {
                Location = new Point(748, 14),
                Size = new Size(120, 28),
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Value = DateTime.Today.AddDays(-7)
            };
            var dtTo = new DateTimePicker
            {
                Location = new Point(874, 14),
                Size = new Size(120, 28),
                Format = DateTimePickerFormat.Short,
                Enabled = false,
                Value = DateTime.Today
            };
            var btnExportHistory = new Button
            {
                Text = "Export CSV",
                Location = new Point(998, 13),
                Size = new Size(70, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnExportHistory.FlatAppearance.BorderSize = 0;
            var btnRetrySkipped = new Button
            {
                Text = "Retry Skipped",
                Location = new Point(898, 13),
                Size = new Size(96, 30),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke
            };
            btnRetrySkipped.FlatAppearance.BorderSize = 0;

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                DataSource = new BindingList<WarmupQueueHistoryRecord>(new List<WarmupQueueHistoryRecord>(records))
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke
            };
            grid.EnableHeadersVisualStyles = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FinishedAtUtc", HeaderText = "Finished (UTC)", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Profile", HeaderText = "Profile", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Keywords", HeaderText = "Keywords", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Videos", HeaderText = "Videos", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Attempts", HeaderText = "Attempts", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Result", HeaderText = "Result", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Error", HeaderText = "Error", FillWeight = 28 });
            ApplyAppGridChrome(grid);
            EnsureAppGridRowHeights(grid);

            Action refreshGrid = () =>
            {
                var keyword = (txtSearch.Text ?? string.Empty).Trim();
                var selectedResult = cbResult.SelectedItem?.ToString() ?? "All";
                var filteredRange = FilterHistoryByRange(records, cbRange.SelectedItem?.ToString(), dtFrom.Value, dtTo.Value);
                var filtered = filteredRange
                    .Where(r =>
                        (selectedResult == "All" || string.Equals(r.Result, selectedResult, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrWhiteSpace(keyword) ||
                         (r.Profile ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (r.Keywords ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (r.Error ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
                grid.DataSource = new BindingList<WarmupQueueHistoryRecord>(filtered);
                EnsureAppGridRowHeights(grid);
            };

            txtSearch.TextChanged += (s, a) => refreshGrid();
            cbResult.SelectedIndexChanged += (s, a) => refreshGrid();
            cbRange.SelectedIndexChanged += (s, a) =>
            {
                var isCustom = string.Equals(cbRange.SelectedItem?.ToString(), "Custom", StringComparison.OrdinalIgnoreCase);
                dtFrom.Enabled = isCustom;
                dtTo.Enabled = isCustom;
                refreshGrid();
            };
            dtFrom.ValueChanged += (s, a) => refreshGrid();
            dtTo.ValueChanged += (s, a) => refreshGrid();
            btnExportHistory.Click += (s, a) =>
            {
                try
                {
                    var current = ((BindingList<WarmupQueueHistoryRecord>)grid.DataSource)?.ToList() ?? new List<WarmupQueueHistoryRecord>();
                    if (current.Count == 0)
                    {
                        MessageBox.Show(form, "Không có dữ liệu để export.", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    using (var dialog = new SaveFileDialog())
                    {
                        dialog.FileName = $"queue_history_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                        dialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                        dialog.Title = "Export Queue History";
                        if (dialog.ShowDialog(form) != DialogResult.OK)
                        {
                            return;
                        }

                        var lines = new List<string> { "FinishedAtUtc,Profile,Keywords,Videos,Attempts,Result,Error" };
                        foreach (var item in current)
                        {
                            lines.Add(string.Join(",",
                                CsvEscape(item.FinishedAtUtc.ToString("O")),
                                CsvEscape(item.Profile),
                                CsvEscape(item.Keywords),
                                item.Videos.ToString(),
                                item.Attempts.ToString(),
                                CsvEscape(item.Result),
                                CsvEscape(item.Error)));
                        }
                        File.WriteAllLines(dialog.FileName, lines, TextFileEncoding.Utf8NoBom);
                        MessageBox.Show(form, "Export CSV thành công.", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(form, "Export thất bại: " + ex.Message, "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            btnRetrySkipped.Click += (s, a) =>
            {
                try
                {
                    var skippedNetwork = records
                        .Where(x => string.Equals(x.Result, "Skipped", StringComparison.OrdinalIgnoreCase))
                        .Where(x => IsRetryableNetworkOrProxyError(x.Error))
                        .Take(20)
                        .ToList();
                    if (skippedNetwork.Count == 0)
                    {
                        MessageBox.Show(form, "Không có job Skipped do network/proxy để retry.", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var added = 0;
                    foreach (var item in skippedNetwork)
                    {
                        var state = new WarmupRunState
                        {
                            Keywords = item.Keywords,
                            VideoCount = Math.Max(1, item.Videos),
                            CompletedCount = 0,
                            CommentProbability = ReadCommentProbabilityFromUi(),
                            LikeProbability = ReadLikeProbabilityFromUi(),
                            ShareProbability = ReadShareProbabilityFromUi(),
                            DryRun = rbDryRun?.Checked ?? false,
                            RunningProfileName = item.Profile,
                            WatchPercentageMin = ReadWatchPercentageFromUi(numWatchMin, DefaultWatchPercentageMin),
                            WatchPercentageMax = Math.Max(
                                ReadWatchPercentageFromUi(numWatchMin, DefaultWatchPercentageMin),
                                ReadWatchPercentageFromUi(numWatchMax, DefaultWatchPercentageMax))
                        };
                        _warmupQueueScheduler.Enqueue(state, 2);
                        _warmupQueueBindingList.Add(new WarmupQueueUiItem
                        {
                            Profile = state.RunningProfileName,
                            Keywords = state.Keywords,
                            Videos = state.VideoCount,
                            WatchRange = FormatWatchRangeForQueue(state.WatchPercentageMin, state.WatchPercentageMax),
                            LikeProbability = state.LikeProbability,
                            ShareProbability = state.ShareProbability,
                            CommentProbability = state.CommentProbability,
                            DryRun = state.DryRun,
                            Status = WarmupStatus.Pending,
                            RetryCount = 0,
                            MaxRetries = 2,
                            LastError = string.Empty,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        added++;
                    }

                    RefreshWarmupQueueStatus();
                    _ = SaveWarmupQueueSnapshotAsync();
                    MessageBox.Show(form, $"Đã re-queue {added} job skipped (network/proxy).", "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(form, "Retry Skipped thất bại: " + ex.Message, "Queue History", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            topPanel.Controls.Add(lblSearch);
            topPanel.Controls.Add(txtSearch);
            topPanel.Controls.Add(lblResult);
            topPanel.Controls.Add(cbResult);
            topPanel.Controls.Add(cbRange);
            topPanel.Controls.Add(dtFrom);
            topPanel.Controls.Add(dtTo);
            topPanel.Controls.Add(btnRetrySkipped);
            topPanel.Controls.Add(btnExportHistory);
            form.Controls.Add(topPanel);
            form.Controls.Add(grid);
            refreshGrid();
            form.ShowDialog(this);
        }

        private static string CsvEscape(string value)
        {
            var text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }

        private static bool IsRetryableNetworkOrProxyError(string error)
        {
            var text = (error ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var keywords = new[]
            {
                "proxy", "ip", "timeout", "timed out", "dns", "network", "connection", "socket", "reset", "temporarily unavailable"
            };
            return keywords.Any(k => text.Contains(k));
        }
        private void btnOpenQueueTrend_Click(object sender, EventArgs e)
        {
            var records = _warmupQueueHistory ?? new List<WarmupQueueHistoryRecord>();
            var now = DateTime.UtcNow.Date;
            var start = now.AddDays(-13); // last 14 days
            var bucket = records
                .Where(x => x.FinishedAtUtc.Date >= start && x.FinishedAtUtc.Date <= now)
                .GroupBy(x => x.FinishedAtUtc.Date)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Success = g.Count(x => string.Equals(x.Result, "Completed", StringComparison.OrdinalIgnoreCase)),
                        Failed = g.Count(x => string.Equals(x.Result, "Failed", StringComparison.OrdinalIgnoreCase))
                    });

            var form = new Form
            {
                Text = "Warm-up Queue Trend (14 days)",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(980, 560),
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro
            };

            var rows = new List<QueueTrendRow>();
            for (var i = 0; i < 14; i++)
            {
                var day = start.AddDays(i);
                var success = 0;
                var failed = 0;
                if (bucket.TryGetValue(day, out var row))
                {
                    success = row.Success;
                    failed = row.Failed;
                }

                var total = success + failed;
                var rate = total == 0 ? "-" : (success * 100d / total).ToString("0.#") + "%";
                rows.Add(new QueueTrendRow
                {
                    Date = day.ToLocalTime().ToString("yyyy-MM-dd"),
                    Success = success,
                    Failed = failed,
                    Total = total,
                    SuccessRate = rate
                });
            }

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(31, 34, 42),
                SplitterWidth = 6,
                FixedPanel = FixedPanel.None,
                SplitterDistance = 250
            };

            var pnlChart = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                Padding = new Padding(12)
            };
            pnlChart.Paint += (s, pe) => ChartHelper.DrawQueueTrendMiniChart(pe.Graphics, pnlChart.ClientRectangle, rows);

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke
            };
            grid.EnableHeadersVisualStyles = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Date", HeaderText = "Date", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Success", HeaderText = "Success", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Failed", HeaderText = "Failed", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Total", HeaderText = "Total", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SuccessRate", HeaderText = "Success Rate", FillWeight = 20 });
            ApplyAppGridChrome(grid);

            grid.DataSource = new BindingList<QueueTrendRow>(rows);
            EnsureAppGridRowHeights(grid);
            split.Panel1.Controls.Add(pnlChart);
            split.Panel2.Controls.Add(grid);
            form.Controls.Add(split);
            form.ShowDialog(this);
        }
        private void RefreshQueueStatsSummary()
        {
            var history = _warmupQueueHistory ?? new List<WarmupQueueHistoryRecord>();
            var selectedRange = cbQueueStatsRange?.SelectedItem?.ToString() ?? "Hôm nay";
            var from = dtQueueStatsFrom?.Value ?? DateTime.Today.AddDays(-7);
            var to = dtQueueStatsTo?.Value ?? DateTime.Today;
            var rangeItems = FilterHistoryByRange(history, selectedRange, from, to);
            var total = rangeItems.Count;
            var completed = rangeItems.Count(x => string.Equals(x.Result, "Completed", StringComparison.OrdinalIgnoreCase));
            var failed = rangeItems.Count(x => string.Equals(x.Result, "Failed", StringComparison.OrdinalIgnoreCase));
            var successRate = total <= 0 ? 0d : (completed * 100d / total);
            var avgRetry = total <= 0 ? 0d : rangeItems.Average(x => Math.Max(0, x.Attempts - 1));

            var topFailed = rangeItems
                .Where(x => string.Equals(x.Result, "Failed", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => (x.Profile ?? string.Empty).Trim())
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (lblQueueStatsSummary != null)
            {
                lblQueueStatsSummary.Text = $"Hôm nay: {total} mục, lỗi: {failed}";
            }
            if (lblQueueStatsSuccessRate != null)
            {
                lblQueueStatsSuccessRate.Text = $"Tỉ lệ thành công: {successRate:0.#}%";
            }
            if (lblQueueStatsAvgRetry != null)
            {
                lblQueueStatsAvgRetry.Text = $"Số lần thử TB: {avgRetry:0.##}";
            }
            if (lblQueueStatsTopFailedProfile != null)
            {
                lblQueueStatsTopFailedProfile.Text = topFailed == null
                    ? "Profile lỗi nhiều nhất: -"
                    : $"Profile lỗi nhiều nhất: {topFailed.Key} ({topFailed.Count()})";
            }
        }

        private static List<WarmupQueueHistoryRecord> FilterHistoryByRange(
            IEnumerable<WarmupQueueHistoryRecord> source,
            string range,
            DateTime fromLocal,
            DateTime toLocal)
        {
            var items = source?.ToList() ?? new List<WarmupQueueHistoryRecord>();
            if (items.Count == 0)
            {
                return items;
            }

            var nowUtc = DateTime.UtcNow;
            DateTime startUtc;
            DateTime endUtc;
            if (string.Equals(range, "7d", StringComparison.OrdinalIgnoreCase))
            {
                startUtc = nowUtc.AddDays(-7);
                endUtc = nowUtc;
            }
            else if (string.Equals(range, "30d", StringComparison.OrdinalIgnoreCase))
            {
                startUtc = nowUtc.AddDays(-30);
                endUtc = nowUtc;
            }
            else if (string.Equals(range, "Custom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(range, "Tùy chỉnh", StringComparison.OrdinalIgnoreCase))
            {
                var localStart = fromLocal.Date;
                var localEndExclusive = toLocal.Date.AddDays(1);
                startUtc = localStart.ToUniversalTime();
                endUtc = localEndExclusive.ToUniversalTime();
            }
            else
            {
                startUtc = nowUtc.Date;
                endUtc = nowUtc;
            }

            return items.Where(x => x.FinishedAtUtc >= startUtc && x.FinishedAtUtc < endUtc).ToList();
        }

        private void MoveSelectedQueueRow(int direction)
        {
            if (_isWarmupQueueRunning)
            {
                LogWarmup("[QUEUE] Stop queue before reordering jobs.");
                return;
            }

            if (_warmupQueueBindingList == null || dgvWarmupQueue == null)
            {
                return;
            }

            var selected = GetSelectedWarmupQueueItemsInDisplayOrder();
            if (selected.Count == 0)
            {
                return;
            }

            var indices = new List<int>(selected.Count);
            for (var i = 0; i < selected.Count; i++)
            {
                var idx = _warmupQueueBindingList.IndexOf(selected[i]);
                if (idx >= 0)
                {
                    indices.Add(idx);
                }
            }

            if (indices.Count == 0)
            {
                return;
            }

            indices.Sort();
            if (direction < 0)
            {
                if (indices[0] <= 0)
                {
                    return;
                }

                for (var i = 0; i < indices.Count; i++)
                {
                    var idx = indices[i];
                    var item = _warmupQueueBindingList[idx];
                    _warmupQueueBindingList.RemoveAt(idx);
                    _warmupQueueBindingList.Insert(idx - 1, item);
                    indices[i] = idx - 1;
                }
            }
            else
            {
                if (indices[indices.Count - 1] >= _warmupQueueBindingList.Count - 1)
                {
                    return;
                }

                for (var i = indices.Count - 1; i >= 0; i--)
                {
                    var idx = indices[i];
                    var item = _warmupQueueBindingList[idx];
                    _warmupQueueBindingList.RemoveAt(idx);
                    _warmupQueueBindingList.Insert(idx + 1, item);
                    indices[i] = idx + 1;
                }
            }

            dgvWarmupQueue.ClearSelection();
            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (idx >= 0 && idx < dgvWarmupQueue.Rows.Count)
                {
                    dgvWarmupQueue.Rows[idx].Selected = true;
                }
            }

            SyncSchedulerFromUi();
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private class WarmupQueueUiItem
        {
            public string Profile { get; set; } = string.Empty;
            public string Keywords { get; set; } = string.Empty;
            public int Videos { get; set; }
            public string WatchRange { get; set; } = string.Empty;
            public int LikeProbability { get; set; } = DefaultLikeProbability;
            public int ShareProbability { get; set; } = DefaultShareProbability;
            public int CommentProbability { get; set; }
            public bool DryRun { get; set; }
            public string RunModeLabel
            {
                get => DryRun ? "Dry" : "Live";
                set => DryRun = string.Equals(value, "Dry", StringComparison.OrdinalIgnoreCase);
            }
            public WarmupStatus Status { get; set; } = WarmupStatus.Pending;
            public int RetryAttempt { get; set; }
            public string StatusDisplay => Status.ToString();
            public DateTime? ScheduledAtUtc { get; set; }
            public string ScheduledAtLabel =>
                ScheduledAtUtc.HasValue
                    ? ScheduledAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : "Đăng ngay";
            public int RetryCount { get; set; }
            public int MaxRetries { get; set; } = 2;
            public string LastError { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
            public DateTime? NextRetryAtUtc { get; set; }
            public string RetrySummary
            {
                get
                {
                    var attempt = Math.Max(RetryCount, RetryAttempt);
                    var progress = attempt + "/" + (MaxRetries + 1);
                    if (NextRetryAtUtc.HasValue)
                    {
                        return progress + " · " + NextRetryAtUtc.Value.ToLocalTime().ToString("HH:mm");
                    }

                    if (CreatedAtUtc != default(DateTime))
                    {
                        return progress + " · " + CreatedAtUtc.ToLocalTime().ToString("HH:mm");
                    }

                    return progress;
                }
            }
        }
    }
}