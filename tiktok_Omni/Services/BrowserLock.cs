using System;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Helper khóa trình duyệt theo profile — delegate tới <see cref="BrowserLockService"/>.</summary>
    internal static class BrowserLock
    {
        public static Task WithLockAsync(
            string profileName,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            BrowserLockService.Instance.WithLockAsync(profileName, action, cancellationToken);

        public static Task<T> WithLockAsync<T>(
            string profileName,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) =>
            BrowserLockService.Instance.WithLockAsync(profileName, action, cancellationToken);
    }
}
