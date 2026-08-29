using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    public sealed class ChannelHealthMonitor
    {
        private static readonly Regex SigiStateRegex = new Regex(
            @"<script[^>]*id=""SIGI_STATE""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UniversalDataRegex = new Regex(
            @"<script[^>]*id=""__UNIVERSAL_DATA_FOR_REHYDRATION__""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ConfigManager _configManager;
        private readonly HttpClient _httpClient;

        public ChannelHealthMonitor(ConfigManager configManager, HttpClient httpClient = null)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _httpClient = httpClient ?? CreateDefaultHttpClient();
        }

        public async Task<ChannelHealthSnapshot> ScrapeChannelMetricsAsync(
            string profileName,
            Action<string> logAction = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveName = ProfileScopedPaths.ResolveProfileName(profileName);
            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var profile = settings?.Profiles?
                .FirstOrDefault(p =>
                    p != null &&
                    string.Equals((p.Name ?? string.Empty).Trim(), effectiveName, StringComparison.OrdinalIgnoreCase));

            if (profile == null)
            {
                throw new InvalidOperationException("Không tìm thấy profile «" + effectiveName + "» trong cấu hình.");
            }

            var uniqueId = (profile.TikTokUniqueId ?? string.Empty).Trim().TrimStart('@');
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                throw new InvalidOperationException(
                    "Profile «" + effectiveName + "» chưa có TikTok @nick — đăng nhập TikTok trong Cài đặt trước.");
            }

            logAction?.Invoke("[ChannelHealth] Đang lấy chỉ số kênh @" + uniqueId + " (profile «" + effectiveName + "»)…");

            var html = await FetchProfileHtmlAsync(uniqueId, cancellationToken).ConfigureAwait(false);
            var snapshot = ParseProfileHtml(html, effectiveName, uniqueId, settings);
            logAction?.Invoke("[ChannelHealth] @" + uniqueId + ": followers=" + snapshot.FollowerCount
                + ", avgViews=" + snapshot.RecentVideosAvgViews
                + " (" + snapshot.RecentVideosSampled + " video) — " + snapshot.Summary);
            return snapshot;
        }

        public async Task<IReadOnlyList<ChannelHealthSnapshot>> ScrapeAllLoggedInProfilesAsync(
            AppSettings settings,
            Action<string> logAction = null,
            CancellationToken cancellationToken = default)
        {
            settings = settings ?? await _configManager.LoadAsync().ConfigureAwait(false);
            var profiles = (settings.Profiles ?? new List<AutomationProfile>())
                .Where(p => p != null && p.IsTTLoggedIn && !string.IsNullOrWhiteSpace(p.TikTokUniqueId))
                .ToList();

            var results = new List<ChannelHealthSnapshot>();
            foreach (var profile in profiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var snapshot = await ScrapeChannelMetricsAsync(profile.Name, logAction, cancellationToken).ConfigureAwait(false);
                    results.Add(snapshot);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[ChannelHealth] Bỏ qua profile «" + profile.Name + "»: " + ex.Message);
                }
            }

            return results;
        }

        private ChannelHealthSnapshot ParseProfileHtml(
            string html,
            string profileName,
            string uniqueId,
            AppSettings settings)
        {
            var root = ExtractEmbeddedJsonRoot(html);
            if (root == null)
            {
                throw new InvalidOperationException("Không đọc được dữ liệu nhúng từ trang TikTok @" + uniqueId + ".");
            }

            var stats = ExtractUserStats(root, uniqueId);
            var recentViews = ExtractRecentVideoViews(root, settings?.ChannelHealthRecentVideoSampleCount ?? 5);
            var avgViews = recentViews.Count > 0 ? (long)Math.Round(recentViews.Average(v => (double)v)) : 0L;
            var minViews = recentViews.Count > 0 ? recentViews.Min() : 0L;
            var threshold = Math.Max(0L, settings?.ChannelHealthMinRecentVideoAvgViews ?? 100);
            var minSample = Math.Max(1, settings?.ChannelHealthRecentVideoSampleCount ?? 5);
            var suspectedShadowban = recentViews.Count >= minSample && avgViews < threshold;
            var summary = suspectedShadowban
                ? "Nghi ngờ shadowban — view TB " + avgViews + " < ngưỡng " + threshold
                : "Ổn định — view TB " + avgViews + " (ngưỡng " + threshold + ")";

            return new ChannelHealthSnapshot
            {
                ProfileName = profileName,
                TikTokUniqueId = uniqueId,
                FollowerCount = stats.FollowerCount,
                FollowingCount = stats.FollowingCount,
                HeartCount = stats.HeartCount,
                VideoCount = stats.VideoCount,
                RecentVideosAvgViews = avgViews,
                RecentVideosMinViews = minViews,
                RecentVideosSampled = recentViews.Count,
                MinViewsThreshold = threshold,
                IsBelowSafetyThreshold = suspectedShadowban,
                SuspectedShadowban = suspectedShadowban,
                Summary = summary,
                CollectedAtUtc = DateTime.UtcNow
            };
        }

        private static (long FollowerCount, long FollowingCount, long HeartCount, long VideoCount) ExtractUserStats(JObject root, string uniqueId)
        {
            long follower = 0;
            long following = 0;
            long heart = 0;
            long video = 0;

            var userModule = root["UserModule"] as JObject;
            if (userModule != null)
            {
                var users = userModule["users"] as JObject;
                var user = users?[uniqueId] as JObject ?? FindUserObject(users, uniqueId);
                var stats = user?["stats"] as JObject;
                if (stats != null)
                {
                    follower = ReadLong(stats, "followerCount");
                    following = ReadLong(stats, "followingCount");
                    heart = ReadLong(stats, "heartCount", "heart");
                    video = ReadLong(stats, "videoCount");
                }
            }

            if (follower <= 0)
            {
                var detail = root["__DEFAULT_SCOPE__"]?["webapp.user-detail"] as JObject;
                var userInfo = detail?["userInfo"]?["stats"] as JObject ?? detail?["userInfo"]?["user"]?["stats"] as JObject;
                if (userInfo != null)
                {
                    follower = ReadLong(userInfo, "followerCount");
                    following = ReadLong(userInfo, "followingCount");
                    heart = ReadLong(userInfo, "heartCount", "heart");
                    video = ReadLong(userInfo, "videoCount");
                }
            }

            return (follower, following, heart, video);
        }

        private static JObject FindUserObject(JObject users, string uniqueId)
        {
            if (users == null)
            {
                return null;
            }

            foreach (var prop in users.Properties())
            {
                if (prop.Value is JObject obj &&
                    string.Equals((obj["uniqueId"] ?? obj["unique_id"])?.ToString(), uniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    return obj;
                }
            }

            return null;
        }

        private static List<long> ExtractRecentVideoViews(JObject root, int sampleCount)
        {
            var max = Math.Max(1, sampleCount);
            var views = new List<long>();
            var itemModule = root["ItemModule"] as JObject;
            if (itemModule == null)
            {
                return views;
            }

            foreach (var prop in itemModule.Properties().OrderByDescending(p => p.Name))
            {
                if (views.Count >= max)
                {
                    break;
                }

                if (!(prop.Value is JObject item))
                {
                    continue;
                }

                var stats = item["stats"] as JObject;
                var play = ReadLong(stats, "playCount", "viewCount", "play_count");
                if (play >= 0)
                {
                    views.Add(play);
                }
            }

            return views;
        }

        private static JObject ExtractEmbeddedJsonRoot(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            foreach (var regex in new[] { SigiStateRegex, UniversalDataRegex })
            {
                var match = regex.Match(html);
                if (!match.Success || match.Groups.Count < 2)
                {
                    continue;
                }

                try
                {
                    return JObject.Parse(match.Groups[1].Value);
                }
                catch
                {
                    // try next block
                }
            }

            return null;
        }

        private async Task<string> FetchProfileHtmlAsync(string uniqueId, CancellationToken cancellationToken)
        {
            var url = "https://www.tiktok.com/@" + Uri.EscapeDataString(uniqueId.Trim().TrimStart('@'));
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,vi;q=0.8");
                using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        private static long ReadLong(JObject obj, params string[] keys)
        {
            if (obj == null || keys == null)
            {
                return 0L;
            }

            foreach (var key in keys)
            {
                var tok = obj[key];
                if (tok == null)
                {
                    continue;
                }

                if (tok.Type == JTokenType.Integer)
                {
                    return tok.Value<long>();
                }

                if (tok.Type == JTokenType.Float)
                {
                    return (long)tok.Value<double>();
                }

                if (long.TryParse(tok.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return 0L;
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }
    }

    public sealed class ChannelHealthSnapshot
    {
        public string ProfileName { get; set; } = string.Empty;
        public string TikTokUniqueId { get; set; } = string.Empty;
        public long FollowerCount { get; set; }
        public long FollowingCount { get; set; }
        public long HeartCount { get; set; }
        public long VideoCount { get; set; }
        public long RecentVideosAvgViews { get; set; }
        public long RecentVideosMinViews { get; set; }
        public int RecentVideosSampled { get; set; }
        public long MinViewsThreshold { get; set; }
        public bool IsBelowSafetyThreshold { get; set; }
        public bool SuspectedShadowban { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime CollectedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
