using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        /// <summary>Chỉ một phiên Chrome/Playwright trên mỗi profile tại một thời điểm.</summary>
        internal static Task WithBrowserLockAsync(
            string profileName,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            BrowserLockService.Instance.WithLockAsync(profileName, action, cancellationToken);

        internal static Task WithBrowserLockAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            BrowserLockService.Instance.WithLockAsync("Global", action, cancellationToken);

        internal static Task<T> WithBrowserLockAsync<T>(
            string profileName,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) =>
            BrowserLockService.Instance.WithLockAsync(profileName, action, cancellationToken);

        internal static Task<T> WithBrowserLockAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) =>
            BrowserLockService.Instance.WithLockAsync("Global", action, cancellationToken);

        private static void TryCancel(CancellationTokenSource cts)
        {
            if (cts == null)
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch
            {
                // ignored
            }
        }

        private void CancelWarmupBrowserWork()
        {
            TryCancel(_warmupCancellation);
            TryCancel(_warmupQueueCancellation);
            TryCancel(_currentQueueJobCancellation);
        }

        private CancellationTokenSource RegisterActiveJobCancellation()
        {
            DisposeActiveJobCancellation();
            _activeJobCancellation = new CancellationTokenSource();
            return _activeJobCancellation;
        }

        private void DisposeActiveJobCancellation()
        {
            if (_activeJobCancellation == null)
            {
                return;
            }

            try
            {
                _activeJobCancellation.Dispose();
            }
            catch
            {
                // ignored
            }

            _activeJobCancellation = null;
        }

        private void CancelAffiliateBrowserWork()
        {
            TryCancel(_huntCancellation);
            TryCancel(_revenueCancellation);
            TryCancel(_activeJobCancellation);
            TryCancel(_autoPostCancellation);
            _globalJobQueue?.CancelWhere(j =>
                j != null && (j.Kind == OmniJobKind.AutoPost || j.Kind == OmniJobKind.HuntAffiliate));
        }
    }
}
