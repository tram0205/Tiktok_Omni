using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Kho khóa Chrome/Playwright theo từng profile — nhiều profile có thể chạy song song.</summary>
    public sealed class BrowserLockService
    {
        public static BrowserLockService Instance { get; } = new BrowserLockService();

        /// <summary>Gọi trước khi chờ khóa (vd. dọn zombie chromedriver).</summary>
        public static Action BeforeAcquire { get; set; }

        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private static readonly AsyncLocal<int> LockDepth = new AsyncLocal<int>();
        private static readonly AsyncLocal<string> LockedProfileKey = new AsyncLocal<string>();

        public SemaphoreSlim GetLock(string profileName)
        {
            var key = NormalizeProfileKey(profileName);
            return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        public static string NormalizeProfileKey(string profileName)
        {
            return ProfileScopedPaths.ResolveProfileName(profileName);
        }

        public async Task WithLockAsync(
            string profileName,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var key = NormalizeProfileKey(profileName);
            if (IsReentrant(key))
            {
                await action(cancellationToken).ConfigureAwait(false);
                return;
            }

            BeforeAcquire?.Invoke();
            var semaphore = GetLock(key);
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnterLock(key);
                await action(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ExitLock();
                semaphore.Release();
            }
        }

        public async Task<T> WithLockAsync<T>(
            string profileName,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var key = NormalizeProfileKey(profileName);
            if (IsReentrant(key))
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }

            BeforeAcquire?.Invoke();
            var semaphore = GetLock(key);
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnterLock(key);
                return await action(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ExitLock();
                semaphore.Release();
            }
        }

        private static bool IsReentrant(string key)
        {
            return LockDepth.Value > 0
                && !string.IsNullOrEmpty(LockedProfileKey.Value)
                && string.Equals(LockedProfileKey.Value, key, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnterLock(string key)
        {
            LockDepth.Value++;
            LockedProfileKey.Value = key;
        }

        private static void ExitLock()
        {
            LockDepth.Value = Math.Max(0, LockDepth.Value - 1);
            if (LockDepth.Value == 0)
            {
                LockedProfileKey.Value = null;
            }
        }
    }
}
