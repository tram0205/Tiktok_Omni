using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public class WarmupQueueScheduler
    {
        private readonly object _sync = new object();
        private readonly Queue<WarmupQueueItem> _queue = new Queue<WarmupQueueItem>();
        private static readonly TimeSpan MinimumRetryCooldown = TimeSpan.FromMinutes(5);

        public int QueueCount
        {
            get
            {
                lock (_sync)
                {
                    return _queue.Count;
                }
            }
        }

        public void Enqueue(WarmupRunState state, int maxRetries = 2)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (_sync)
            {
                _queue.Enqueue(new WarmupQueueItem
                {
                    State = state,
                    MaxRetries = Math.Max(0, maxRetries)
                });
            }
        }

        public void EnqueueFront(WarmupRunState state, int maxRetries = 2)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (_sync)
            {
                var temp = new List<WarmupQueueItem>(_queue.Count + 1)
                {
                    new WarmupQueueItem
                    {
                        State = state,
                        MaxRetries = Math.Max(0, maxRetries)
                    }
                };

                while (_queue.Count > 0)
                {
                    temp.Add(_queue.Dequeue());
                }

                for (var i = 0; i < temp.Count; i++)
                {
                    _queue.Enqueue(temp[i]);
                }
            }
        }

        public List<WarmupQueueSnapshotItem> GetPendingSnapshots()
        {
            lock (_sync)
            {
                var output = new List<WarmupQueueSnapshotItem>();
                foreach (var item in _queue)
                {
                    output.Add(new WarmupQueueSnapshotItem
                    {
                        State = item.State,
                        MaxRetries = item.MaxRetries
                    });
                }
                return output;
            }
        }

        public void ReplacePending(IEnumerable<WarmupQueueSnapshotItem> snapshots)
        {
            lock (_sync)
            {
                _queue.Clear();
                if (snapshots == null)
                {
                    return;
                }

                foreach (var snapshot in snapshots)
                {
                    if (snapshot?.State == null)
                    {
                        continue;
                    }

                    _queue.Enqueue(new WarmupQueueItem
                    {
                        State = snapshot.State,
                        MaxRetries = Math.Max(0, snapshot.MaxRetries)
                    });
                }
            }
        }

        public bool RemoveFirstMatching(WarmupRunState state)
        {
            if (state == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (_queue.Count == 0)
                {
                    return false;
                }

                var temp = new List<WarmupQueueItem>(_queue.Count);
                var removed = false;
                while (_queue.Count > 0)
                {
                    var item = _queue.Dequeue();
                    if (!removed && IsSameState(item.State, state))
                    {
                        removed = true;
                        continue;
                    }

                    temp.Add(item);
                }

                for (var i = 0; i < temp.Count; i++)
                {
                    _queue.Enqueue(temp[i]);
                }

                return removed;
            }
        }

        public void ClearPending()
        {
            lock (_sync)
            {
                _queue.Clear();
            }
        }

        public async Task RunAsync(
            Func<WarmupRunState, CancellationToken, Task> executor,
            Action<string> logAction,
            CancellationToken cancellationToken,
            Action<WarmupQueueRunEvent> eventAction = null,
            Func<WarmupQueueRunEvent, Task> terminalEventAsync = null)
        {
            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                WarmupQueueItem item = null;
                lock (_sync)
                {
                    if (_queue.Count > 0)
                    {
                        item = _queue.Dequeue();
                    }
                }

                if (item == null)
                {
                    logAction?.Invoke("[QUEUE] No pending warm-up jobs.");
                    return;
                }

                var profileName = string.IsNullOrWhiteSpace(item.State.RunningProfileName)
                    ? "default"
                    : item.State.RunningProfileName.Trim();
                logAction?.Invoke($"[QUEUE] Running warm-up job for profile '{profileName}'.");
                eventAction?.Invoke(new WarmupQueueRunEvent
                {
                    EventType = WarmupQueueEventType.Started,
                    State = item.State,
                    Attempt = 1,
                    MaxRetries = item.MaxRetries
                });

                for (var attempt = 0; attempt <= item.MaxRetries; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await executor(item.State, cancellationToken).ConfigureAwait(false);
                        logAction?.Invoke($"[QUEUE] Job completed for profile '{profileName}'.");
                        eventAction?.Invoke(new WarmupQueueRunEvent
                        {
                            EventType = WarmupQueueEventType.Completed,
                            State = item.State,
                            Attempt = attempt + 1,
                            MaxRetries = item.MaxRetries
                        });
                        if (terminalEventAsync != null)
                        {
                            await terminalEventAsync(new WarmupQueueRunEvent
                            {
                                EventType = WarmupQueueEventType.Completed,
                                State = item.State,
                                Attempt = attempt + 1,
                                MaxRetries = item.MaxRetries
                            }).ConfigureAwait(false);
                        }
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (WarmupQueueRequeueException)
                    {
                        logAction?.Invoke($"[QUEUE] Job re-queued for profile '{profileName}'.");
                        eventAction?.Invoke(new WarmupQueueRunEvent
                        {
                            EventType = WarmupQueueEventType.Requeued,
                            State = item.State,
                            Attempt = attempt + 1,
                            MaxRetries = item.MaxRetries
                        });
                        break;
                    }
                    catch (Exception ex)
                    {
                        var hasRetry = attempt < item.MaxRetries;
                        if (!hasRetry)
                        {
                            logAction?.Invoke($"[QUEUE][WARN] Job skipped after max retries for '{profileName}': {ex.Message}");
                            eventAction?.Invoke(new WarmupQueueRunEvent
                            {
                                EventType = WarmupQueueEventType.Skipped,
                                State = item.State,
                                Attempt = attempt + 1,
                                MaxRetries = item.MaxRetries,
                                ErrorMessage = ex.Message
                            });
                            if (terminalEventAsync != null)
                            {
                                await terminalEventAsync(new WarmupQueueRunEvent
                                {
                                    EventType = WarmupQueueEventType.Skipped,
                                    State = item.State,
                                    Attempt = attempt + 1,
                                    MaxRetries = item.MaxRetries,
                                    ErrorMessage = ex.Message
                                }).ConfigureAwait(false);
                            }
                            break;
                        }

                        var retryAtUtc = DateTime.UtcNow.Add(TimeSpan.FromMinutes(Math.Max(5, (attempt + 1) * 5)));
                        var delay = retryAtUtc - DateTime.UtcNow;
                        if (delay < MinimumRetryCooldown)
                        {
                            delay = MinimumRetryCooldown;
                            retryAtUtc = DateTime.UtcNow.Add(delay);
                        }

                        var delayMinutes = Math.Max(5, (int)Math.Ceiling(delay.TotalMinutes));
                        logAction?.Invoke($"[QUEUE][WARN] Job failed for '{profileName}' (attempt {attempt + 1}/{item.MaxRetries + 1}). Cooldown {delayMinutes}m before retry...");
                        eventAction?.Invoke(new WarmupQueueRunEvent
                        {
                            EventType = WarmupQueueEventType.Retrying,
                            State = item.State,
                            Attempt = attempt + 1,
                            MaxRetries = item.MaxRetries,
                            ErrorMessage = ex.Message,
                            NextRetryAtUtc = retryAtUtc
                        });
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private class WarmupQueueItem
        {
            public WarmupRunState State { get; set; }
            public int MaxRetries { get; set; }
        }

        private static bool IsSameState(WarmupRunState left, WarmupRunState right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.RunningProfileName ?? string.Empty, right.RunningProfileName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.Keywords ?? string.Empty, right.Keywords ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   left.VideoCount == right.VideoCount &&
                   left.WatchSecondsMin == right.WatchSecondsMin &&
                   left.WatchSecondsMax == right.WatchSecondsMax &&
                   left.AutoComment == right.AutoComment &&
                   left.DryRun == right.DryRun;
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

    public enum WarmupQueueEventType
    {
        Started,
        Retrying,
        Requeued,
        Completed,
        FailedPermanent,
        Skipped
    }

    public class WarmupQueueRunEvent
    {
        public WarmupQueueEventType EventType { get; set; }
        public WarmupRunState State { get; set; }
        public int Attempt { get; set; }
        public int MaxRetries { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime? NextRetryAtUtc { get; set; }
    }

    public class WarmupQueueRequeueException : Exception
    {
        public WarmupQueueRequeueException(string message) : base(message)
        {
        }
    }
}
