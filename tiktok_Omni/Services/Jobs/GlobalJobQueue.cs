using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Jobs
{
    /// <summary>Hàng đợi job toàn cục — producer đẩy job, worker bốc ra xử lý.</summary>
    public sealed class GlobalJobQueue : IDisposable
    {
        private readonly BlockingCollection<OmniJob> _queue =
            new BlockingCollection<OmniJob>(new ConcurrentQueue<OmniJob>());

        private readonly ConcurrentQueue<OmniJob> _priorityQueue = new ConcurrentQueue<OmniJob>();

        private readonly ConcurrentDictionary<Guid, OmniJob> _registry =
            new ConcurrentDictionary<Guid, OmniJob>();

        private bool _disposed;

        public event EventHandler<OmniJob> JobStateChanged;

        public IReadOnlyCollection<OmniJob> AllJobs =>
            _registry.Values.OrderByDescending(j => j.CreatedAtUtc).ToList();

        public int PendingCount => _registry.Values.Count(j =>
            j.Status == OmniJobStatus.Pending || j.Status == OmniJobStatus.RetryPending);

        public int RunningCount => _registry.Values.Count(j =>
            j.Status == OmniJobStatus.Running || j.Status == OmniJobStatus.Processing);

        public void Enqueue(OmniJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalJobQueue));
            }

            job.Status = OmniJobStatus.Pending;
            _registry[job.Id] = job;
            Notify(job);
            if (job.Priority > 0)
            {
                _priorityQueue.Enqueue(job);
            }
            else
            {
                _queue.Add(job);
            }
        }

        public static bool IsRunningStatus(OmniJobStatus status) =>
            status == OmniJobStatus.Running || status == OmniJobStatus.Processing;

        /// <summary>Hủy job đang chạy — giữ job Pending/RetryPending (hàng đợi, lịch đăng).</summary>
        public int CancelRunningOnly() =>
            CancelWhere(j => j != null && IsRunningStatus(j.Status));

        /// <summary>Hủy mọi job đang chờ/chạy và xả hàng đợi nội bộ (Emergency Stop).</summary>
        public int ClearAll()
        {
            var cancelled = CancelWhere(_ => true);

            while (_priorityQueue.TryDequeue(out _))
            {
            }

            while (_queue.TryTake(out _, 0))
            {
            }

            return cancelled;
        }

        public int CancelWhere(Func<OmniJob, bool> predicate)
        {
            if (predicate == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var job in _registry.Values.ToList())
            {
                if (job == null || !predicate(job))
                {
                    continue;
                }

                if (TryCancel(job.Id))
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryCancel(Guid jobId)
        {
            if (!_registry.TryGetValue(jobId, out var job) || job == null)
            {
                return false;
            }

            if (job.Status != OmniJobStatus.Pending &&
                job.Status != OmniJobStatus.RetryPending &&
                job.Status != OmniJobStatus.Running &&
                job.Status != OmniJobStatus.Processing)
            {
                return false;
            }

            if (job.Tag is CancellationTokenSource cts)
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // ignored
                }
            }

            job.Status = OmniJobStatus.Cancelled;
            job.FinishedAtUtc = DateTime.UtcNow;
            job.LastError = "Cancelled by user";
            UpdateJob(job);
            return true;
        }

        public bool TryTake(CancellationToken cancellationToken, out OmniJob job)
        {
            job = null;
            if (_disposed)
            {
                return false;
            }

            try
            {
                if (_priorityQueue.TryDequeue(out job) && job != null)
                {
                    return true;
                }

                job = _queue.Take(cancellationToken);
                return job != null;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public void UpdateJob(OmniJob job)
        {
            if (job == null)
            {
                return;
            }

            _registry[job.Id] = job;
            Notify(job);
        }

        public bool TryGet(Guid id, out OmniJob job) => _registry.TryGetValue(id, out job);

        private void Notify(OmniJob job) => JobStateChanged?.Invoke(this, job);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.CompleteAdding();
            _queue.Dispose();
        }
    }
}
