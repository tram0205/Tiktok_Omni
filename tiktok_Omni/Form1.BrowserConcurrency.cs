using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly AsyncLocal<int> BrowserLockDepth = new AsyncLocal<int>();

        /// <summary>Chỉ cho phép một phiên Chrome/Playwright tại một thời điểm (tránh đè profile / crash).</summary>
        internal static async Task WithBrowserLockAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (BrowserLockDepth.Value > 0)
            {
                await action(cancellationToken).ConfigureAwait(false);
                return;
            }

            KillZombieBrowserProcesses();
            await _browserSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                BrowserLockDepth.Value++;
                await action(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                BrowserLockDepth.Value = Math.Max(0, BrowserLockDepth.Value - 1);
                _browserSemaphore.Release();
            }
        }

        internal static async Task<T> WithBrowserLockAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (BrowserLockDepth.Value > 0)
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }

            KillZombieBrowserProcesses();
            await _browserSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                BrowserLockDepth.Value++;
                return await action(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                BrowserLockDepth.Value = Math.Max(0, BrowserLockDepth.Value - 1);
                _browserSemaphore.Release();
            }
        }

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
