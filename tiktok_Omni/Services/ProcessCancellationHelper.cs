using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Ensures external processes (ffmpeg, ffprobe, …) are killed when a <see cref="CancellationToken"/> fires.
    /// </summary>
    internal static class ProcessCancellationHelper
    {
        public static void WaitForExit(Process process, CancellationToken cancellationToken)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (cancellationToken.Register(() => TryKillProcess(process)))
            {
                process.WaitForExit();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public static bool WaitForExit(Process process, int milliseconds, CancellationToken cancellationToken)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (cancellationToken.Register(() => TryKillProcess(process)))
            {
                return process.WaitForExit(milliseconds);
            }
        }

        public static async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (cancellationToken.Register(() => TryKillProcess(process)))
            {
                await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public static async Task WaitUntilExitAsync(
            Process process,
            CancellationToken cancellationToken,
            int pollIntervalMs = 100)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            using (cancellationToken.Register(() => TryKillProcess(process)))
            {
                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // Process may already be gone or access denied during teardown.
            }
        }
    }
}
