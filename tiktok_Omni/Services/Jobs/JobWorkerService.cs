using System;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Jobs
{
    /// <summary>Worker nền — bốc job từ <see cref="GlobalJobQueue"/> và giới hạn theo MaxConcurrentJobs.</summary>
    public sealed class JobWorkerService : IDisposable
    {
        private readonly GlobalJobQueue _queue;
        private readonly OmniJobExecutor _executor;
        private readonly Func<int> _getMaxConcurrentJobs;
        private readonly IJobUiBridge _uiBridge;

        private CancellationTokenSource _cts;
        private Task _dispatcherTask;
        private SemaphoreSlim _concurrency;
        private int _configuredSlots = 2;
        private bool _disposed;

        public JobWorkerService(
            GlobalJobQueue queue,
            OmniJobExecutor executor,
            Func<int> getMaxConcurrentJobs,
            IJobUiBridge uiBridge)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _getMaxConcurrentJobs = getMaxConcurrentJobs ?? throw new ArgumentNullException(nameof(getMaxConcurrentJobs));
            _uiBridge = uiBridge ?? throw new ArgumentNullException(nameof(uiBridge));
            _concurrency = new SemaphoreSlim(ClampSlots(_getMaxConcurrentJobs()), ClampSlots(_getMaxConcurrentJobs()));
        }

        public void Start()
        {
            if (_dispatcherTask != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _dispatcherTask = Task.Run(() => DispatchLoopAsync(_cts.Token));
            _uiBridge.Log("[JobQueue] WorkerService đã khởi động.");
        }

        public void RefreshConcurrencyLimit()
        {
            var slots = ClampSlots(_getMaxConcurrentJobs());
            if (slots == _configuredSlots)
            {
                return;
            }

            _configuredSlots = slots;
            var old = _concurrency;
            _concurrency = new SemaphoreSlim(slots, slots);
            old.Dispose();
            _uiBridge.Log("[JobQueue] MaxConcurrentJobs = " + slots);
        }

        private async Task DispatchLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_queue.TryTake(cancellationToken, out var job) || job == null)
                {
                    continue;
                }

                await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = RunJobWithRetryAsync(job, cancellationToken);
            }
        }

        private async Task RunJobWithRetryAsync(OmniJob job, CancellationToken dispatcherCancellation)
        {
            try
            {
                while (true)
                {
                    if (job.Status == OmniJobStatus.Cancelled)
                    {
                        return;
                    }

                    job.Status = job.RetryCount > 0 ? OmniJobStatus.RetryPending : OmniJobStatus.Running;
                    job.StartedAtUtc = DateTime.UtcNow;
                    job.LastError = string.Empty;
                    _queue.UpdateJob(job);
                    _uiBridge.Log($"[JobQueue] ▶ {job.Kind} «{job.Title}» (lần thử {job.RetryCount + 1}/{job.MaxRetries + 1})");

                    try
                    {
                        using (var jobCts = CreateJobCancellation(job, dispatcherCancellation))
                        {
                            await _executor.ExecuteAsync(job, _uiBridge, jobCts.Token).ConfigureAwait(false);
                        }

                        job.Status = OmniJobStatus.Completed;
                        job.FinishedAtUtc = DateTime.UtcNow;
                        _queue.UpdateJob(job);
                        _uiBridge.Log($"[JobQueue] ✓ Hoàn tất: {job.Title}");
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        job.Status = OmniJobStatus.Cancelled;
                        job.FinishedAtUtc = DateTime.UtcNow;
                        job.LastError = "Cancelled";
                        _queue.UpdateJob(job);
                        _uiBridge.Log($"[JobQueue] ⊗ Đã hủy: {job.Title}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        job.LastError = ex.Message;
                        if (job.RetryCount < job.MaxRetries)
                        {
                            job.RetryCount++;
                            job.Status = OmniJobStatus.RetryPending;
                            _queue.UpdateJob(job);
                            var delaySec = Math.Min(30, 3 * job.RetryCount);
                            _uiBridge.Log(
                                $"[JobQueue] ↻ Retry {job.RetryCount}/{job.MaxRetries} sau {delaySec}s — {ex.Message}");
                            await Task.Delay(TimeSpan.FromSeconds(delaySec), dispatcherCancellation).ConfigureAwait(false);
                            continue;
                        }

                        job.Status = OmniJobStatus.Failed;
                        job.FinishedAtUtc = DateTime.UtcNow;
                        _queue.UpdateJob(job);
                        _uiBridge.Log($"[JobQueue] ✗ Thất bại: {job.Title} — {ex.Message}");
                        return;
                    }
                }
            }
            finally
            {
                _concurrency.Release();
            }
        }

        private static CancellationTokenSource CreateJobCancellation(OmniJob job, CancellationToken dispatcherCancellation)
        {
            if (job?.Tag is CancellationTokenSource external && !external.IsCancellationRequested)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(dispatcherCancellation, external.Token);
            }

            return CancellationTokenSource.CreateLinkedTokenSource(dispatcherCancellation);
        }

        private static int ClampSlots(int value)
        {
            if (value < 1)
            {
                return 1;
            }

            if (value > 8)
            {
                return 8;
            }

            return value;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignored
            }

            _concurrency?.Dispose();
            _cts?.Dispose();
        }
    }
}
