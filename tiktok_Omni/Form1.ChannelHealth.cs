using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private System.Windows.Forms.Timer _channelHealthTimer;
        private ChannelHealthMonitor _channelHealthMonitor;
        private ChannelTransitionService _channelTransitionService;
        private bool _channelHealthCycleRunning;
        private CancellationTokenSource _channelHealthCycleCts;

        private void InitializeChannelHealthMonitor()
        {
            _channelHealthMonitor = new ChannelHealthMonitor(_configManager);
            _channelTransitionService = new ChannelTransitionService(
                _warmupStateManager,
                _configManager,
                _notificationService);

            _channelHealthTimer = new System.Windows.Forms.Timer();
            _channelHealthTimer.Tick += ChannelHealthTimer_Tick;
            ApplyChannelHealthTimerInterval();
        }

        private void ApplyChannelHealthTimerInterval()
        {
            if (_channelHealthTimer == null)
            {
                return;
            }

            var hours = 6;
            try
            {
                var settings = _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult();
                hours = Math.Max(1, settings?.ChannelHealthCheckIntervalHours ?? 6);
            }
            catch
            {
                hours = 6;
            }

            _channelHealthTimer.Interval = hours * 60 * 60 * 1000;
        }

        private void StartChannelHealthMonitorIfEnabled()
        {
            if (_channelHealthTimer == null)
            {
                return;
            }

            try
            {
                var settings = _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult();
                if (settings == null || !settings.ChannelHealthMonitorEnabled)
                {
                    _channelHealthTimer.Stop();
                    Log("[ChannelHealth] Monitor tắt trong cài đặt.");
                    return;
                }

                ApplyChannelHealthTimerInterval();
                if (!_channelHealthTimer.Enabled)
                {
                    _channelHealthTimer.Start();
                }

                Log("[ChannelHealth] Monitor bật — chu kỳ " + (settings.ChannelHealthCheckIntervalHours) + " giờ.");
                _ = RunChannelHealthCycleAsync();
            }
            catch (Exception ex)
            {
                Log("[ChannelHealth] Không khởi động được monitor: " + ex.Message);
            }
        }

        private void ChannelHealthTimer_Tick(object sender, EventArgs e)
        {
            _ = RunChannelHealthCycleAsync();
        }

        private async Task RunChannelHealthCycleAsync()
        {
            if (_channelHealthCycleRunning)
            {
                return;
            }

            _channelHealthCycleRunning = true;
            _channelHealthCycleCts?.Cancel();
            _channelHealthCycleCts = new CancellationTokenSource();
            var ct = _channelHealthCycleCts.Token;

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                if (settings == null || !settings.ChannelHealthMonitorEnabled)
                {
                    return;
                }

                Log("[ChannelHealth] Bắt đầu chu kỳ quét sức khỏe kênh…");
                var snapshots = await _channelHealthMonitor
                    .ScrapeAllLoggedInProfilesAsync(settings, Log, ct)
                    .ConfigureAwait(true);

                if (snapshots == null || snapshots.Count == 0)
                {
                    Log("[ChannelHealth] Không có profile TikTok đã đăng nhập để quét.");
                    return;
                }

                var transitions = await _channelTransitionService
                    .ProcessSnapshotsAsync(snapshots, settings, Log, ct)
                    .ConfigureAwait(true);

                foreach (var transition in transitions.Where(t => t?.WarmupJobToEnqueue != null))
                {
                    EnqueueHighIntensityWarmupFromChannelHealth(transition);
                }

                Log("[ChannelHealth] Hoàn tất chu kỳ — đã quét " + snapshots.Count + " profile.");
            }
            catch (OperationCanceledException)
            {
                Log("[ChannelHealth] Chu kỳ bị hủy.");
            }
            catch (Exception ex)
            {
                Log("[ChannelHealth] Lỗi chu kỳ: " + ex.Message);
            }
            finally
            {
                _channelHealthCycleRunning = false;
            }
        }

        private void EnqueueHighIntensityWarmupFromChannelHealth(ChannelTransitionResult transition)
        {
            if (transition?.WarmupJobToEnqueue == null || _warmupQueueScheduler == null)
            {
                return;
            }

            void Apply()
            {
                const int defaultRetries = 2;
                var state = transition.WarmupJobToEnqueue;
                _warmupQueueScheduler.Enqueue(state, maxRetries: defaultRetries);
                if (_warmupQueueBindingList != null)
                {
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
                        ScheduledAtUtc = null,
                        RetryCount = 0,
                        MaxRetries = defaultRetries,
                        LastError = string.Empty,
                        CreatedAtUtc = DateTime.UtcNow,
                        NextRetryAtUtc = null
                    });
                }

                RefreshWarmupQueueStatus();
                _ = SaveWarmupQueueSnapshotAsync();
                Log("[ChannelHealth] Đã xếp hàng Warm-up cường độ cao cho «"
                    + transition.ProfileName + "» (" + state.VideoCount + " video).");
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)Apply);
            }
            else
            {
                Apply();
            }
        }

        private void StopChannelHealthMonitor()
        {
            _channelHealthCycleCts?.Cancel();
            _channelHealthTimer?.Stop();
        }
    }
}
