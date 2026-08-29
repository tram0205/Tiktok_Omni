using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>Sec-MS-GEC token (Microsoft Edge Read Aloud) — port từ edge-tts drm.py.</summary>
    internal static class EdgeTtsDrm
    {
        internal const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        internal const string SecMsGecVersion = "1-143.0.3650.75";
        private const double WinEpochSeconds = 11644473600d;
        private const double SecondsToNsOver100 = 1e7; // 1e9 / 100

        private static double _clockSkewSeconds;

        public static void AdjustClockSkewSeconds(double skewSeconds) =>
            _clockSkewSeconds += skewSeconds;

        public static void AdjustClockSkewFromServerDate(string rfc2616Date)
        {
            if (string.IsNullOrWhiteSpace(rfc2616Date))
            {
                return;
            }

            if (!DateTime.TryParseExact(
                    rfc2616Date.Trim(),
                    "R",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var serverUtc))
            {
                return;
            }

            var serverUnix = new DateTimeOffset(serverUtc).ToUnixTimeSeconds();
            var clientUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            AdjustClockSkewSeconds(serverUnix - clientUnix);
        }

        private static double GetUnixTimestamp() =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _clockSkewSeconds;

        public static string GenerateSecMsGec()
        {
            var ticks = (long)GetUnixTimestamp();
            ticks += (long)WinEpochSeconds;
            ticks -= ticks % 300;
            var winTicks = ticks * SecondsToNsOver100;
            var strToHash = winTicks.ToString("F0", CultureInfo.InvariantCulture) + TrustedClientToken;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(strToHash));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        public static string GenerateMuidCookieHeader()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var sb = new StringBuilder(32);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return "muid=" + sb.ToString().ToUpperInvariant() + ";";
        }
    }
}
