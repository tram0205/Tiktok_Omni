using System;
using System.IO;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Kiểm tra phiên đăng nhập từ thư mục Chrome user-data (Cookies / Local State) — không đọc nội dung cookie ra log.
    /// </summary>
    public static class ProfileSessionProbe
    {
        public sealed class LoginProbeResult
        {
            public string UserDataDir { get; set; } = string.Empty;
            public bool HasLocalState { get; set; }
            public bool HasCookieDatabase { get; set; }
            public bool TikTokLoggedIn { get; set; }
            public bool FacebookLoggedIn { get; set; }
            public bool YouTubeLoggedIn { get; set; }
        }

        public static string ResolveUserDataDir(AutomationProfile profile, string profileName) =>
            BrowserAutomation.GetSharedProfilePath(profile, profileName);

        public static LoginProbeResult ProbeFromDisk(AutomationProfile profile, string profileName)
        {
            var dir = ResolveUserDataDir(profile, profileName);
            var result = ProbeFromDisk(dir);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.TikTokUniqueId))
            {
                result.TikTokLoggedIn = true;
            }

            return result;
        }

        public static LoginProbeResult ProbeFromDisk(string userDataDir)
        {
            var result = new LoginProbeResult
            {
                UserDataDir = userDataDir ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
            {
                return result;
            }

            result.HasLocalState = File.Exists(Path.Combine(userDataDir, "Local State"));
            var cookiesPath = FindCookieDatabasePath(userDataDir);
            result.HasCookieDatabase = !string.IsNullOrWhiteSpace(cookiesPath) && File.Exists(cookiesPath);
            if (!result.HasCookieDatabase)
            {
                return result;
            }

            try
            {
                var bytes = ReadCookieDatabaseSample(cookiesPath);
                result.TikTokLoggedIn = CookieDbSuggestsTikTok(bytes);
                result.FacebookLoggedIn = CookieDbSuggestsFacebook(bytes);
                result.YouTubeLoggedIn = CookieDbSuggestsYouTube(bytes);
            }
            catch
            {
                // Giữ mặc định false — không làm hỏng UI
            }

            return result;
        }

        public static void ApplyToProfile(AutomationProfile profile, LoginProbeResult probe)
        {
            if (profile == null || probe == null)
            {
                return;
            }

            profile.IsTTLoggedIn = probe.TikTokLoggedIn;
            profile.IsFBLoggedIn = probe.FacebookLoggedIn;
            profile.IsYTLoggedIn = probe.YouTubeLoggedIn;
        }

        public static string FindCookieDatabasePath(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir))
            {
                return string.Empty;
            }

            var candidates = new[]
            {
                Path.Combine(userDataDir, "Default", "Network", "Cookies"),
                Path.Combine(userDataDir, "Default", "Cookies"),
                Path.Combine(userDataDir, "Profile 1", "Network", "Cookies"),
                Path.Combine(userDataDir, "Profile 1", "Cookies")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static byte[] ReadCookieDatabaseSample(string cookiesPath)
        {
            const int maxBytes = 12 * 1024 * 1024;
            using (var fs = new FileStream(cookiesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var len = (int)Math.Min(maxBytes, fs.Length);
                var buffer = new byte[len];
                var read = fs.Read(buffer, 0, len);
                if (read < len)
                {
                    Array.Resize(ref buffer, read);
                }

                return buffer;
            }
        }

        private static bool CookieDbSuggestsTikTok(byte[] bytes) =>
            ContainsAscii(bytes, "sessionid") &&
            (ContainsAscii(bytes, "tiktok.com") || ContainsAscii(bytes, ".tiktok.com"));

        private static bool CookieDbSuggestsFacebook(byte[] bytes) =>
            ContainsAscii(bytes, "c_user") &&
            (ContainsAscii(bytes, "facebook.com") || ContainsAscii(bytes, ".facebook.com"));

        private static bool CookieDbSuggestsYouTube(byte[] bytes) =>
            (ContainsAscii(bytes, "SAPISID") || ContainsAscii(bytes, "SSID")) &&
            (ContainsAscii(bytes, "youtube.com") ||
             ContainsAscii(bytes, ".youtube.com") ||
             ContainsAscii(bytes, "google.com"));

        private static bool ContainsAscii(byte[] haystack, string needle)
        {
            if (haystack == null || haystack.Length == 0 || string.IsNullOrEmpty(needle))
            {
                return false;
            }

            var n = Encoding.ASCII.GetBytes(needle);
            if (n.Length == 0 || n.Length > haystack.Length)
            {
                return false;
            }

            for (var i = 0; i <= haystack.Length - n.Length; i++)
            {
                var match = true;
                for (var j = 0; j < n.Length; j++)
                {
                    if (haystack[i + j] != n[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
