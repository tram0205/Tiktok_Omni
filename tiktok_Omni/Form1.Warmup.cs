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
        private CheckBox chkAutoComment;
        private RadioButton rbDryRun;
        private RadioButton rbLiveRun;
        private Button btnStartWarmup;
        private Button btnQueueWarmup;
        private Button btnStartWarmupQueue;
        private Button btnStopWarmupQueue;
        private Button btnPauseWarmupQueue;
        private Button btnRemoveQueueJob;
        private CheckBox chkAutoResumeQueueOnStartup;
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

        private const int WatchSecondsUiMax = 600; // must match numWatchMin/numWatchMax Maximum (seconds)

        private static string FormatWatchRangeForQueue(int secMin, int secMax)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1} s (tổng)", secMin, secMax);
        }

        private static void ParseWatchRangeToSeconds(string watchRange, out int watchMin, out int watchMax)
        {
            watchMin = 7;
            watchMax = 18;
            if (string.IsNullOrWhiteSpace(watchRange))
            {
                return;
            }

            var wr = watchRange.Trim();
            const string totalSuffix = " (tổng)";
            if (wr.EndsWith(totalSuffix, StringComparison.OrdinalIgnoreCase))
            {
                wr = wr.Substring(0, wr.Length - totalSuffix.Length).Trim();
            }

            if (wr.EndsWith(" s", StringComparison.OrdinalIgnoreCase))
            {
                var core = wr.Substring(0, wr.Length - 2).Trim();
                var parts = core.Split('-');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sminS) &&
                    int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var smaxS))
                {
                    watchMin = Math.Max(3, sminS);
                    watchMax = Math.Max(watchMin, smaxS);
                }

                return;
            }

            if (wr.EndsWith(" m", StringComparison.OrdinalIgnoreCase))
            {
                var core = wr.Substring(0, wr.Length - 2).Trim();
                var parts = core.Split('-');
                if (parts.Length == 2 &&
                    decimal.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var minM) &&
                    decimal.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var maxM))
                {
                    watchMin = Math.Max(3, (int)Math.Round((double)minM * 60));
                    watchMax = Math.Max(watchMin, (int)Math.Round((double)maxM * 60));
                }

                return;
            }

            var legacy = wr;
            if (legacy.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                legacy = legacy.Substring(0, legacy.Length - 1);
            }

            var legacyParts = legacy.Split('-');
            if (legacyParts.Length == 2 &&
                int.TryParse(legacyParts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var smin) &&
                int.TryParse(legacyParts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var smax))
            {
                watchMin = Math.Max(3, smin);
                watchMax = Math.Max(watchMin, smax);
            }
        }

        private static int ReadWatchSecondsFromUi(NumericUpDown ctrl)
        {
            if (ctrl == null)
            {
                return 7;
            }

            return Math.Max(3, (int)ctrl.Value);
        }

        private static int ReadWatchSecondsFromUiOrDefault(NumericUpDown ctrl, int defaultSeconds)
        {
            return ctrl == null ? defaultSeconds : ReadWatchSecondsFromUi(ctrl);
        }

        private static void ApplyWatchSecondsToUi(NumericUpDown ctrl, int seconds)
        {
            if (ctrl == null || seconds <= 0)
            {
                return;
            }

            var sec = Math.Max((int)ctrl.Minimum, Math.Min((int)ctrl.Maximum, seconds));
            ctrl.Value = sec;
        }

        private async void btnStartWarmup_Click(object sender, EventArgs e)
        {
            if (btnStartWarmup != null)
            {
                btnStartWarmup.Enabled = false;
            }

            try
            {
                await RunStartWarmupFromUiAsync().ConfigureAwait(true);
            }
            finally
            {
                if (btnStartWarmup != null && !btnStartWarmup.IsDisposed)
                {
                    btnStartWarmup.Enabled = true;
                }
            }
        }

        private async Task RunStartWarmupFromUiAsync()
        {
            if (_isWarmupQueueRunning)
            {
                Log("Queue is running. Stop queue before manual warm-up.");
                return;
            }

            var saved = await _warmupStateManager.LoadAsync().ConfigureAwait(true);
            if (saved != null
                && saved.CompletedCount < saved.VideoCount
                && btnStartWarmup != null
                && string.Equals(btnStartWarmup.Text, "Tiếp tục", StringComparison.Ordinal))
            {
                ApplyWarmupStateToUi(saved);
                await RunWarmupAsync(saved, true).ConfigureAwait(true);
                return;
            }

            var state = BuildWarmupStateFromUi();

            await RunWarmupAsync(state, false).ConfigureAwait(true);
        }
        private void btnQueueWarmup_Click(object sender, EventArgs e)
        {
            var state = BuildWarmupStateFromUi();
            const int defaultRetries = 2;
            var isScheduled = state.ScheduledAtLocal.HasValue;
            if (!isScheduled)
            {
                _warmupQueueScheduler.Enqueue(state, maxRetries: defaultRetries);
            }

            _warmupQueueBindingList.Add(new WarmupQueueUiItem
            {
                Profile = state.RunningProfileName,
                Keywords = state.Keywords,
                Videos = state.VideoCount,
                WatchRange = FormatWatchRangeForQueue(state.WatchSecondsMin, state.WatchSecondsMax),
                AutoComment = state.AutoComment,
                DryRun = state.DryRun,
                Status = isScheduled ? WarmupStatus.Scheduled : WarmupStatus.Pending,
                ScheduledAtLocal = state.ScheduledAtLocal,
                RetryCount = 0,
                MaxRetries = defaultRetries,
                LastError = string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                NextRetryAtUtc = null
            });
            RefreshWarmupQueueStatus();
            Log(isScheduled
                ? $"[QUEUE] Đã lập lịch warm-up profile '{state.RunningProfileName}' lúc {state.ScheduledAtLocal:dd/MM/yyyy HH:mm}."
                : $"[QUEUE] Added warm-up job for profile '{state.RunningProfileName}'.");
            _ = SaveWarmupQueueSnapshotAsync();
        }

        private void WarmupScheduleTimer_Tick(object sender, EventArgs e)
        {
            if (_warmupQueueBindingList == null || _warmupQueueBindingList.Count == 0)
            {
                return;
            }

            var now = DateTime.Now;
            var promoted = false;
            for (var i = 0; i < _warmupQueueBindingList.Count; i++)
            {
                var item = _warmupQueueBindingList[i];
                if (item == null ||
                    item.Status != WarmupStatus.Scheduled ||
                    !item.ScheduledAtLocal.HasValue ||
                    item.ScheduledAtLocal.Value > now)
                {
                    continue;
                }

                item.Status = WarmupStatus.Pending;
                item.ScheduledAtLocal = null;
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
            Log("[QUEUE] Đã đến giờ — chuyển job lập lịch sang Pending.");

            if (!_isWarmupQueueRunning && (_warmupQueueScheduler?.QueueCount ?? 0) > 0)
            {
                btnStartWarmupQueue.PerformClick();
            }
        }

        private async void btnStartWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                return;
            }

            if (_warmupQueueScheduler.QueueCount == 0)
            {
                Log("[QUEUE] No jobs to run.");
                return;
            }
            SyncSchedulerFromUi();

            _isWarmupQueueRunning = true;
            _pauseNowRequested = false;
            btnStartWarmupQueue.Enabled = false;
            btnStopWarmupQueue.Enabled = true;
            ApplyWarmupQueueToolbarState();
            btnStartWarmup.Enabled = false;
            btnQueueWarmup.Enabled = false;
            _warmupQueueCancellation?.Dispose();
            _warmupQueueCancellation = new CancellationTokenSource();
            RefreshWarmupQueueStatus();

            try
            {
                await _warmupQueueScheduler.RunAsync(
                    RunWarmupQueueJobAsync,
                    Log,
                    _warmupQueueCancellation.Token,
                    HandleQueueEvent,
                    _ => SaveWarmupQueueSnapshotAsync());
                Log("[QUEUE] All jobs processed.");
            }
            catch (OperationCanceledException)
            {
                Log("[QUEUE] Queue stopped by user.");
            }
            finally
            {
                _isWarmupQueueRunning = false;
                _isWarmupQueuePaused = false;
                _pauseNowRequested = false;
                btnStartWarmupQueue.Enabled = true;
                btnStopWarmupQueue.Enabled = false;
                ApplyWarmupQueueToolbarState();
                btnStartWarmup.Enabled = true;
                btnQueueWarmup.Enabled = true;
                _warmupQueueCancellation?.Dispose();
                _warmupQueueCancellation = null;
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = null;
                await _warmupQueueStateManager.SavePausedFlagAsync(false);
                await RefreshResumeStateAsync();
                RefreshWarmupQueueStatus();
                _ = SaveWarmupQueueSnapshotAsync();
            }
        }

        private void btnStopWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_warmupQueueCancellation == null)
            {
                return;
            }

            btnStopWarmupQueue.Enabled = false;
            CancelWarmupBrowserWork();
            Log("[QUEUE] Stopping queue...");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
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
                Log("[QUEUE] Queue resumed.");
                _ = _warmupQueueStateManager.SavePausedFlagAsync(false);
                ApplyWarmupQueueToolbarState();
                return;
            }

            _isWarmupQueuePaused = true;
            RefreshWarmupQueueStatus();
            Log("[QUEUE] Pause requested. Queue will pause after current job.");
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
            Log("[QUEUE] Pause-now requested. Current job will be re-queued from latest progress.");
            _ = _warmupQueueStateManager.SavePausedFlagAsync(true);
            ApplyWarmupQueueToolbarState();
        }

        private void btnRemoveQueueJob_Click(object sender, EventArgs e)
        {
            if (dgvWarmupQueue?.SelectedRows == null || dgvWarmupQueue.SelectedRows.Count == 0)
            {
                Log("[QUEUE] Please select a queue job to remove.");
                return;
            }

            if (_isWarmupQueueRunning)
            {
                Log("[QUEUE] Stop queue before removing jobs.");
                return;
            }

            var selected = dgvWarmupQueue.SelectedRows[0]?.DataBoundItem as WarmupQueueUiItem;
            if (selected == null)
            {
                return;
            }

            var state = BuildStateFromQueueRow(selected);
            var removed = _warmupQueueScheduler.RemoveFirstMatching(state);
            if (removed)
            {
                _warmupQueueBindingList.Remove(selected);
                SyncSchedulerFromUi();
                RefreshWarmupQueueStatus();
                Log($"[QUEUE] Removed job for profile '{selected.Profile}'.");
                _ = SaveWarmupQueueSnapshotAsync();
            }
        }

        private void btnClearWarmupQueue_Click(object sender, EventArgs e)
        {
            if (_isWarmupQueueRunning)
            {
                Log("[QUEUE] Stop queue before clearing.");
                return;
            }

            _warmupQueueScheduler.ClearPending();
            _warmupQueueBindingList?.Clear();
            RefreshWarmupQueueStatus();
            Log("[QUEUE] Cleared all pending jobs.");
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
                Log("[LIVE] ERROR: " + staleMsg);
                MessageBox.Show(
                    staleMsg,
                    "Can restart app",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            btnStartWarmup.Enabled = false;
            btnStopWarmupQueue.Enabled = true;
            _warmupCancellation?.Dispose();
            _warmupCancellation = RegisterActiveJobCancellation();
            _currentWarmupState = state;
            UpdateWarmupProgress(state.CompletedCount, state.VideoCount);
            await _warmupStateManager.SaveAsync(state).ConfigureAwait(true);

            try
            {
                Log(isResume
                    ? $"Resuming warm-up from {state.CompletedCount}/{state.VideoCount}..."
                    : "Warm-up started...");
                await _tikTokAutomation.StartWarmupAsync(
                    state.Keywords,
                    state.VideoCount,
                    state.AutoComment,
                    state.DryRun,
                    state.CompletedCount,
                    state.WatchSecondsMin <= 0 ? ReadWatchSecondsFromUi(numWatchMin) : state.WatchSecondsMin,
                    state.WatchSecondsMax <= 0 ? ReadWatchSecondsFromUi(numWatchMax) : state.WatchSecondsMax,
                    _warmupCancellation.Token,
                    Log,
                    HandleWarmupProgressUpdate,
                    state.RunningProfileName);
                Log("Warm-up completed.");
                await _warmupStateManager.ClearAsync();
                _currentWarmupState = null;
            }
            catch (OperationCanceledException)
            {
                Log("Warm-up stopped by user.");
            }
            catch (Exception ex)
            {
                Log("Warm-up failed: " + ex.Message);
            }
            finally
            {
                if (!_isWarmupQueueRunning)
                {
                    btnStartWarmup.Enabled = true;
                }

                ApplyWarmupQueueToolbarState();
                _warmupCancellation = null;
                DisposeActiveJobCancellation();
                await RefreshResumeStateAsync().ConfigureAwait(true);
            }
        }

        private void btnStopWarmup_Click(object sender, EventArgs e)
        {
            if (_warmupCancellation == null)
            {
                return;
            }

            btnStopWarmupQueue.Enabled = false;
            CancelWarmupBrowserWork();
            Log("Stopping warm-up...");
        }

        private async Task RunWarmupQueueJobAsync(WarmupRunState state, CancellationToken cancellationToken)
        {
            if (state == null)
            {
                return;
            }

            if (!state.DryRun && Services.WarmupBuildInfo.IsRunningStaleBuild(out var staleQueueMsg))
            {
                Log("[LIVE] ERROR: " + staleQueueMsg);
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
                    state.Keywords,
                    state.VideoCount,
                    state.AutoComment,
                    state.DryRun,
                    state.CompletedCount,
                    state.WatchSecondsMin <= 0 ? 7 : state.WatchSecondsMin,
                    state.WatchSecondsMax <= 0 ? Math.Max(7, state.WatchSecondsMin) : state.WatchSecondsMax,
                    _currentQueueJobCancellation.Token,
                    Log,
                    HandleWarmupProgressUpdate,
                    state.RunningProfileName);
                await _warmupStateManager.ClearAsync();
            }
            catch (OperationCanceledException) when (_pauseNowRequested)
            {
                _pauseNowRequested = false;
                if (state.CompletedCount < state.VideoCount)
                {
                    var resumeState = CloneWarmupState(state);
                    _warmupQueueScheduler.EnqueueFront(resumeState, 2);
                    _warmupQueueBindingList?.Insert(0, new WarmupQueueUiItem
                    {
                        Profile = resumeState.RunningProfileName,
                        Keywords = resumeState.Keywords,
                        Videos = resumeState.VideoCount,
                        WatchRange = FormatWatchRangeForQueue(resumeState.WatchSecondsMin, resumeState.WatchSecondsMax),
                        AutoComment = resumeState.AutoComment,
                        DryRun = resumeState.DryRun,
                        Status = WarmupStatus.Pending,
                        RetryCount = 0,
                        MaxRetries = 2,
                        LastError = string.Empty,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    _ = SaveWarmupQueueSnapshotAsync();
                }

                throw new WarmupQueueRequeueException("Paused now and re-queued.");
            }
            finally
            {
                _currentQueueJobCancellation?.Dispose();
                _currentQueueJobCancellation = null;
                _currentWarmupState = null;
                RefreshWarmupQueueStatus();
            }
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
            else if (eventInfo.EventType == WarmupQueueEventType.Completed ||
                     eventInfo.EventType == WarmupQueueEventType.FailedPermanent ||
                     eventInfo.EventType == WarmupQueueEventType.Skipped)
            {
                if (eventInfo.EventType == WarmupQueueEventType.FailedPermanent ||
                    eventInfo.EventType == WarmupQueueEventType.Skipped)
                {
                    row.LastError = eventInfo.ErrorMessage ?? string.Empty;
                }
                if (eventInfo.EventType == WarmupQueueEventType.Skipped)
                {
                    row.Status = WarmupStatus.Skipped;
                }
                row.NextRetryAtUtc = null;
                _ = AppendQueueHistoryAsync(eventInfo, row);
                _warmupQueueBindingList.Remove(row);
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
            await _warmupStateManager.SaveAsync(_currentWarmupState);
        }

        private async Task RefreshResumeStateAsync()
        {
            var state = await _warmupStateManager.LoadAsync();
            var canResume = state != null && state.CompletedCount < state.VideoCount;
            if (!_isWarmupQueueRunning && _warmupCancellation == null)
            {
                ApplyWarmupStartButtonResumeState(btnStartWarmup, canResume);
            }
        }

        private void ApplyWarmupStateToUi(WarmupRunState state)
        {
            txtKeywords.Text = state.Keywords;
            SelectRunningProfileInUi(state.RunningProfileName);
            numVideoCount.Value = Math.Max(numVideoCount.Minimum, Math.Min(numVideoCount.Maximum, state.VideoCount));
            if (state.WatchSecondsMin > 0)
            {
                ApplyWatchSecondsToUi(numWatchMin, state.WatchSecondsMin);
            }

            if (state.WatchSecondsMax > 0)
            {
                ApplyWatchSecondsToUi(numWatchMax, state.WatchSecondsMax);
            }
            chkAutoComment.Checked = state.AutoComment;
            rbDryRun.Checked = state.DryRun;
            rbLiveRun.Checked = !state.DryRun;
            UpdateWarmupProgress(state.CompletedCount, state.VideoCount);
        }

        private WarmupRunState BuildWarmupStateFromUi()
        {
            DateTime? scheduledAt = null;
            if (chkEnableWarmupSchedule != null && chkEnableWarmupSchedule.Checked && dtpWarmupSchedule != null)
            {
                scheduledAt = dtpWarmupSchedule.Value;
            }

            return new WarmupRunState
            {
                Keywords = txtKeywords.Text.Trim(),
                RunningProfileName = cbRunningProfile?.SelectedItem?.ToString() ?? "default",
                VideoCount = (int)numVideoCount.Value,
                WatchSecondsMin = ReadWatchSecondsFromUi(numWatchMin),
                WatchSecondsMax = Math.Max(ReadWatchSecondsFromUi(numWatchMin), ReadWatchSecondsFromUi(numWatchMax)),
                AutoComment = chkAutoComment.Checked,
                DryRun = rbDryRun.Checked,
                CompletedCount = 0,
                LastUpdatedUtc = DateTime.UtcNow,
                ScheduledAtLocal = scheduledAt
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

                    var scheduledAt = snapshot.State.ScheduledAtLocal;
                    var isScheduled = scheduledAt.HasValue && scheduledAt.Value > DateTime.Now;
                    _warmupQueueBindingList.Add(new WarmupQueueUiItem
                    {
                        Profile = snapshot.State.RunningProfileName,
                        Keywords = snapshot.State.Keywords,
                        Videos = snapshot.State.VideoCount,
                        WatchRange = FormatWatchRangeForQueue(snapshot.State.WatchSecondsMin, snapshot.State.WatchSecondsMax),
                        AutoComment = snapshot.State.AutoComment,
                        DryRun = snapshot.State.DryRun,
                        Status = isScheduled ? WarmupStatus.Scheduled : WarmupStatus.Pending,
                        ScheduledAtLocal = isScheduled ? scheduledAt : null,
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
                Log("[QUEUE] Failed to load queue state: " + ex.Message);
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
                Log("[QUEUE] Failed to persist queue state: " + ex.Message);
            }
        }

        private void SyncSchedulerFromUi()
        {
            var snapshots = BuildSnapshotsFromUi(includeScheduledRows: false);
            _warmupQueueScheduler.ReplacePending(snapshots);
            RefreshWarmupQueueStatus();
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
                if (!includeScheduledRows && isScheduledRow)
                {
                    continue;
                }

                var state = BuildStateFromQueueRow(row);
                if (isScheduledRow)
                {
                    state.ScheduledAtLocal = row.ScheduledAtLocal;
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

                ParseWatchRangeToSeconds(row.WatchRange, out var rowWatchMin, out var rowWatchMax);
                if (string.Equals(row.Profile ?? string.Empty, state.RunningProfileName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(row.Keywords ?? string.Empty, state.Keywords ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    row.Videos == state.VideoCount &&
                    rowWatchMin == state.WatchSecondsMin &&
                    rowWatchMax == state.WatchSecondsMax)
                {
                    return row;
                }
            }

            return null;
        }

        private WarmupRunState BuildStateFromQueueRow(WarmupQueueUiItem row)
        {
            ParseWatchRangeToSeconds(row?.WatchRange, out var watchMin, out var watchMax);

            // Use current UI run mode / auto-comment when executing queue (avoids stale Dry Run on old rows).
            var autoComment = chkAutoComment?.Checked ?? (row?.AutoComment ?? true);
            var dryRun = rbDryRun?.Checked ?? (row?.DryRun ?? false);

            return new WarmupRunState
            {
                RunningProfileName = row?.Profile ?? "default",
                Keywords = row?.Keywords ?? string.Empty,
                VideoCount = row?.Videos ?? 0,
                WatchSecondsMin = watchMin,
                WatchSecondsMax = watchMax,
                AutoComment = autoComment,
                DryRun = dryRun,
                ScheduledAtLocal = row?.ScheduledAtLocal
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
                WatchSecondsMin = source.WatchSecondsMin,
                WatchSecondsMax = source.WatchSecondsMax,
                AutoComment = source.AutoComment,
                DryRun = source.DryRun,
                CompletedCount = source.CompletedCount,
                LastUpdatedUtc = source.LastUpdatedUtc,
                ScheduledAtLocal = source.ScheduledAtLocal
            };
        }

        private void dgvWarmupQueue_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _isWarmupQueueRunning || _warmupQueueBindingList == null)
            {
                return;
            }

            var column = dgvWarmupQueue.Columns[e.ColumnIndex];
            if (!string.Equals(column?.DataPropertyName, "MaxRetries", StringComparison.Ordinal))
            {
                return;
            }

            var row = dgvWarmupQueue.Rows[e.RowIndex]?.DataBoundItem as WarmupQueueUiItem;
            if (row == null)
            {
                return;
            }

            if (row.MaxRetries < 0)
            {
                row.MaxRetries = 0;
            }
            else if (row.MaxRetries > 10)
            {
                row.MaxRetries = 10;
            }

            dgvWarmupQueue.Refresh();
            SyncSchedulerFromUi();
            _ = SaveWarmupQueueSnapshotAsync();
            Log($"[QUEUE] Updated MaxRetries for '{row.Profile}' to {row.MaxRetries}.");
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
                Log("[QUEUE] Failed to append history: " + ex.Message);
            }
        }

        private async void btnRefreshQueueStats_Click(object sender, EventArgs e)
        {
            _warmupQueueHistory = await _warmupQueueHistoryManager.LoadAsync();
            RefreshQueueStatsSummary();
            Log("[QUEUE] Stats refreshed.");
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
            ApplyAppGridHeaderChrome(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FinishedAtUtc", HeaderText = "Finished (UTC)", FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Profile", HeaderText = "Profile", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Keywords", HeaderText = "Keywords", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Videos", HeaderText = "Videos", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Attempts", HeaderText = "Attempts", FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Result", HeaderText = "Result", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Error", HeaderText = "Error", FillWeight = 28 });

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
                            AutoComment = chkAutoComment?.Checked ?? false,
                            DryRun = rbDryRun?.Checked ?? false,
                            RunningProfileName = item.Profile,
                            WatchSecondsMin = ReadWatchSecondsFromUiOrDefault(numWatchMin, 7),
                            WatchSecondsMax = Math.Max(
                                ReadWatchSecondsFromUiOrDefault(numWatchMin, 7),
                                ReadWatchSecondsFromUiOrDefault(numWatchMax, 18))
                        };
                        _warmupQueueScheduler.Enqueue(state, 2);
                        _warmupQueueBindingList.Add(new WarmupQueueUiItem
                        {
                            Profile = state.RunningProfileName,
                            Keywords = state.Keywords,
                            Videos = state.VideoCount,
                            WatchRange = FormatWatchRangeForQueue(state.WatchSecondsMin, state.WatchSecondsMax),
                            AutoComment = state.AutoComment,
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
            ApplyAppGridHeaderChrome(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Date", HeaderText = "Date", FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Success", HeaderText = "Success", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Failed", HeaderText = "Failed", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Total", HeaderText = "Total", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SuccessRate", HeaderText = "Success Rate", FillWeight = 20 });

            grid.DataSource = new BindingList<QueueTrendRow>(rows);
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
                Log("[QUEUE] Stop queue before reordering jobs.");
                return;
            }

            if (dgvWarmupQueue?.SelectedRows == null || dgvWarmupQueue.SelectedRows.Count == 0 || _warmupQueueBindingList == null)
            {
                return;
            }

            var selected = dgvWarmupQueue.SelectedRows[0]?.DataBoundItem as WarmupQueueUiItem;
            if (selected == null)
            {
                return;
            }

            var oldIndex = _warmupQueueBindingList.IndexOf(selected);
            if (oldIndex < 0)
            {
                return;
            }

            var newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= _warmupQueueBindingList.Count)
            {
                return;
            }

            _warmupQueueBindingList.RemoveAt(oldIndex);
            _warmupQueueBindingList.Insert(newIndex, selected);
            dgvWarmupQueue.ClearSelection();
            if (newIndex >= 0 && newIndex < dgvWarmupQueue.Rows.Count)
            {
                dgvWarmupQueue.Rows[newIndex].Selected = true;
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
            public bool AutoComment { get; set; }
            public bool DryRun { get; set; }
            public WarmupStatus Status { get; set; } = WarmupStatus.Pending;
            public int RetryAttempt { get; set; }
            public string StatusDisplay =>
                Status == WarmupStatus.Running && RetryAttempt > 0
                    ? "Retry " + RetryAttempt + "/" + (MaxRetries + 1)
                    : Status.ToString();
            public DateTime? ScheduledAtLocal { get; set; }
            public string ScheduledAtLabel =>
                ScheduledAtLocal.HasValue ? ScheduledAtLocal.Value.ToString("dd/MM HH:mm") : "Đăng ngay";
            public int RetryCount { get; set; }
            public int MaxRetries { get; set; } = 2;
            public string LastError { get; set; } = string.Empty;
            public string RetryLabel => RetryCount + "/" + (MaxRetries + 1);
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
            public DateTime? NextRetryAtUtc { get; set; }
            public string CreatedAtLabel => CreatedAtUtc == default(DateTime) ? string.Empty : CreatedAtUtc.ToLocalTime().ToString("HH:mm:ss");
            public string NextRetryEtaLabel => NextRetryAtUtc.HasValue ? NextRetryAtUtc.Value.ToLocalTime().ToString("HH:mm:ss") : "-";
        }
    }
}