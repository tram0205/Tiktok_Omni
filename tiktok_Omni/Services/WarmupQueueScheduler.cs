using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public class WarmupQueueScheduler
    {
        private readonly object _sync = new object();
        // Dùng LinkedList để EnqueueFront và Enqueue đạt tốc độ O(1), không bị giật lag khi danh sách dài
        private readonly LinkedList<WarmupQueueItem> _queue = new LinkedList<WarmupQueueItem>();
        private static readonly TimeSpan MinimumRetryCooldown = TimeSpan.FromMinutes(5);

        public int QueueCount
        {
            get { lock (_sync) { return _queue.Count; } }
        }

        public void Enqueue(WarmupRunState state, int maxRetries = 2)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            lock (_sync)
            {
                _queue.AddLast(new WarmupQueueItem
                {
                    State = state,
                    MaxRetries = Math.Max(0, maxRetries),
                    NextRetryAtUtc = null // Chạy ngay
                });
            }
        }

        public void EnqueueFront(WarmupRunState state, int maxRetries = 2)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            lock (_sync)
            {
                // Thêm vào đầu list cực nhanh, không cần copy tạo List tạm như code cũ
                _queue.AddFirst(new WarmupQueueItem
                {
                    State = state,
                    MaxRetries = Math.Max(0, maxRetries),
                    NextRetryAtUtc = null
                });
            }
        }

        public List<WarmupQueueSnapshotItem> GetPendingSnapshots()
        {
            lock (_sync)
            {
                return _queue.Select(item => new WarmupQueueSnapshotItem
                {
                    State = item.State,
                    MaxRetries = item.MaxRetries,
                    NextRetryAtUtc = item.NextRetryAtUtc
                }).ToList();
            }
        }

        public void ReplacePending(IEnumerable<WarmupQueueSnapshotItem> snapshots)
        {
            lock (_sync)
            {
                _queue.Clear();
                if (snapshots == null) return;

                foreach (var snapshot in snapshots)
                {
                    if (snapshot?.State == null) continue;
                    _queue.AddLast(new WarmupQueueItem
                    {
                        State = snapshot.State,
                        MaxRetries = Math.Max(0, snapshot.MaxRetries),
                        NextRetryAtUtc = snapshot.NextRetryAtUtc
                    });
                }
            }
        }

        public void ClearPending()
        {
            lock (_sync) { _queue.Clear(); }
        }

        public async Task RunAsync(
            Func<WarmupRunState, CancellationToken, Task> executor,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<WarmupQueueRunEvent> eventAction = null,
            Func<WarmupQueueRunEvent, Task> terminalEventAsync = null,
            Func<CancellationToken, Task> waitIfPausedAsync = null)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            while (!cancellationToken.IsCancellationRequested)
            {
                if (waitIfPausedAsync != null)
                {
                    await waitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                }

                WarmupQueueItem itemToRun = null;
                TimeSpan? shortestWaitTime = null;

                lock (_sync)
                {
                    if (_queue.Count > 0)
                    {
                        // Tìm job đầu tiên sẵn sàng chạy (không bị dính thời gian Cooldown)
                        var node = _queue.First;
                        while (node != null)
                        {
                            if (!node.Value.NextRetryAtUtc.HasValue || node.Value.NextRetryAtUtc.Value <= DateTime.UtcNow)
                            {
                                itemToRun = node.Value;
                                _queue.Remove(node);
                                break;
                            }
                            else
                            {
                                // Tính thời gian chờ của job có cooldown ngắn nhất
                                var delay = node.Value.NextRetryAtUtc.Value - DateTime.UtcNow;
                                if (!shortestWaitTime.HasValue || delay < shortestWaitTime.Value)
                                    shortestWaitTime = delay;
                            }
                            node = node.Next;
                        }
                    }
                }

                // Hàng đợi có job nhưng TẤT CẢ đều đang bị Cooldown chờ chạy lại
                if (itemToRun == null)
                {
                    if (QueueCount == 0)
                    {
                        logAction?.Invoke("[QUEUE] No pending warm-up jobs.");
                        return;
                    }

                    // Có job nhưng chưa tới giờ retry — đợi đúng shortestWaitTime thay vì poll 5s vô ích
                    var sleepMs = shortestWaitTime.HasValue
                        ? (int)shortestWaitTime.Value.TotalMilliseconds
                        : 5000;
                    sleepMs = Math.Max(1000, Math.Min(30000, sleepMs));
                    await Task.Delay(sleepMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var profileName = string.IsNullOrWhiteSpace(itemToRun.State.RunningProfileName) ? "default" : itemToRun.State.RunningProfileName.Trim();
                logAction?.Invoke($"[QUEUE] Running warm-up job for profile '{profileName}'.");
                eventAction?.Invoke(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.Started, State = itemToRun.State, Attempt = itemToRun.AttemptCount + 1, MaxRetries = itemToRun.MaxRetries });

                try
                {
                    // Chạy Job
                    await executor(itemToRun.State, cancellationToken).ConfigureAwait(false);

                    logAction?.Invoke($"[QUEUE] Job completed for profile '{profileName}'.");
                    eventAction?.Invoke(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.Completed, State = itemToRun.State, Attempt = itemToRun.AttemptCount + 1, MaxRetries = itemToRun.MaxRetries });
                    if (terminalEventAsync != null) await terminalEventAsync(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.Completed, State = itemToRun.State, Attempt = itemToRun.AttemptCount + 1, MaxRetries = itemToRun.MaxRetries }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (WarmupQueueRequeueException)
                {
                    logAction?.Invoke($"[QUEUE] Job re-queued for profile '{profileName}'.");
                    eventAction?.Invoke(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.Requeued, State = itemToRun.State, Attempt = itemToRun.AttemptCount + 1, MaxRetries = itemToRun.MaxRetries });
                    if (waitIfPausedAsync != null)
                    {
                        await waitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (ArgumentException ex)
                {
                    logAction?.Invoke($"[QUEUE][WARN] Job skipped (cấu hình lỗi) for '{profileName}': {ex.Message}");
                    eventAction?.Invoke(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.Skipped, State = itemToRun.State, Attempt = itemToRun.AttemptCount + 1, MaxRetries = itemToRun.MaxRetries, ErrorMessage = ex.Message });
                }
                catch (Exception ex)
                {
                    var hasRetry = itemToRun.AttemptCount < itemToRun.MaxRetries;
                    if (!hasRetry)
                    {
                        logAction?.Invoke($"[QUEUE][WARN] Job failed permanently after {itemToRun.AttemptCount + 1} attempts for '{profileName}': {ex.Message}");
                        eventAction?.Invoke(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.FailedPermanent, State = itemToRun.State, Attempt = itemToRun.AttemptCount + 1, MaxRetries = itemToRun.MaxRetries, ErrorMessage = ex.Message });
                    }
                    else
                    {
                        // SỬA LỖI BLOCKING: Tính toán thời gian Retry
                        itemToRun.AttemptCount++;
                        var retryAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, itemToRun.AttemptCount * 5));
                        itemToRun.NextRetryAtUtc = retryAtUtc;

                        logAction?.Invoke(
                            $"[QUEUE][WARN] Job failed for '{profileName}': {ex.Message}. Cooldown {(retryAtUtc - DateTime.UtcNow).TotalMinutes:0.#}m... Pushing back to queue.");
                        eventAction?.Invoke(new WarmupQueueRunEvent { EventType = WarmupQueueEventType.Retrying, State = itemToRun.State, Attempt = itemToRun.AttemptCount, MaxRetries = itemToRun.MaxRetries, ErrorMessage = ex.Message, NextRetryAtUtc = retryAtUtc });

                        // ĐẨY VÀO CUỐI HÀNG ĐỢI ĐỂ PROFILE KHÁC ĐƯỢC CHẠY
                        lock (_sync)
                        {
                            _queue.AddLast(itemToRun);
                        }

                        // Nghỉ 3 giây để hệ thống/IP không bị giật cục, sau đó vòng while sẽ tự bốc Job kế tiếp (của profile khác) lên chạy ngay.
                        await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private class WarmupQueueItem
        {
            public WarmupRunState State { get; set; }
            public int MaxRetries { get; set; }
            public int AttemptCount { get; set; } = 0;
            public DateTime? NextRetryAtUtc { get; set; }
        }
    }

    public class WarmupQueueSnapshotItem
    {
        public WarmupRunState State { get; set; }
        public int MaxRetries { get; set; } = 2;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public int RetryCount { get; set; }
        public string LastError { get; set; } = string.Empty;
        public DateTime? NextRetryAtUtc { get; set; }
    }

    public enum WarmupQueueEventType { Started, Retrying, Requeued, Completed, FailedPermanent, Skipped }

    public class WarmupQueueRunEvent
    {
        public WarmupQueueEventType EventType { get; set; }
        public WarmupRunState State { get; set; }
        public int Attempt { get; set; }
        public int MaxRetries { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime? NextRetryAtUtc { get; set; }
    }

    public class WarmupQueueRequeueException : Exception { public WarmupQueueRequeueException(string msg) : base(msg) { } }
}
