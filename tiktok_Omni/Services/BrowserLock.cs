using System;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Delegate tới <see cref="Form1._browserSemaphore"/> — một phiên browser tại một thời điểm.</summary>
    internal static class BrowserLock
    {
        public static Task WithLockAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken) =>
            Form1.WithBrowserLockAsync(action, cancellationToken);

        public static Task<T> WithLockAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
            Form1.WithBrowserLockAsync(action, cancellationToken);
    }
}
