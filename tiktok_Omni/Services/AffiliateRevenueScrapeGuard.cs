using System;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Anti-ban: tối đa 1 navigation/request mỗi phút + delay ngẫu nhiên giữa thao tác.</summary>
    public static class AffiliateRevenueScrapeGuard
    {
        private static readonly object Sync = new object();
        private static DateTime _lastRequestUtc = DateTime.MinValue;
        private static readonly Random Rng = new Random();

        public static void HumanDelayBetweenActions()
        {
            Thread.Sleep(Rng.Next(3000, 7000));
        }

        public static async Task WaitForRateLimitSlotAsync(
            Action<string> log,
            CancellationToken cancellationToken)
        {
            TimeSpan wait;
            lock (Sync)
            {
                var elapsed = DateTime.UtcNow - _lastRequestUtc;
                wait = elapsed >= TimeSpan.FromMinutes(1)
                    ? TimeSpan.Zero
                    : TimeSpan.FromMinutes(1) - elapsed;
            }

            if (wait > TimeSpan.Zero)
            {
                log?.Invoke($"[Revenue] Anti-ban: chờ {wait.TotalSeconds:0}s (tối đa 1 request/phút)…");
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }

        public static void MarkRequestCompleted()
        {
            lock (Sync)
            {
                _lastRequestUtc = DateTime.UtcNow;
            }
        }
    }
}
