using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Client RapidAPI tiktok-api23 — săn video theo từ khoá (không cần mở Chrome).
    /// </summary>
    public sealed class TikTokRapidApiService
    {
        private const string RapidApiHost = "tiktok-api23.p.rapidapi.com";
        private const string SearchVideoUrl = "https://tiktok-api23.p.rapidapi.com/api/search/video";

        public async Task<bool> TestConnectionAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            var results = await SearchVideosAsync("tiktok", 1, apiKey, null, cancellationToken).ConfigureAwait(false);
            return results != null && results.Count > 0;
        }

        public async Task<List<AffiliateCandidate>> SearchVideosAsync(
            string keyword,
            int maxResults,
            string apiKey,
            Action<string> logAction,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Chưa cấu hình TikTok RapidAPI Key trong tab Cài đặt.");
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                throw new ArgumentException("Từ khoá không được để trống.", nameof(keyword));
            }

            var limit = Math.Max(1, Math.Min(maxResults, 50));
            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();

            long cursor = 0;
            string searchId = "0";
            var hasMore = true;
            var pageGuard = 0;

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) })
            {
                while (results.Count < limit && hasMore && pageGuard < 6)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pageGuard++;

                    var url = SearchVideoUrl
                        + "?keyword=" + Uri.EscapeDataString(keyword.Trim())
                        + "&cursor=" + cursor
                        + "&search_id=" + Uri.EscapeDataString(searchId ?? "0");

                    logAction?.Invoke("[RapidAPI] GET " + url);

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey.Trim());
                        request.Headers.TryAddWithoutValidation("x-rapidapi-host", RapidApiHost);

                        using (var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new InvalidOperationException(
                                    "RapidAPI HTTP " + (int)response.StatusCode + ": " + Truncate(body, 400));
                            }

                            var root = JObject.Parse(body);
                            var items = root["item_list"] as JArray
                                        ?? root["data"]?["item_list"] as JArray
                                        ?? root["data"]?["videos"] as JArray
                                        ?? new JArray();

                            if (items.Count == 0)
                            {
                                logAction?.Invoke("[RapidAPI] Trang " + pageGuard + ": không có video nào trong phản hồi.");
                                break;
                            }

                            foreach (var token in items)
                            {
                                if (results.Count >= limit)
                                {
                                    break;
                                }

                                var candidate = MapItemToCandidate(keyword, token as JObject);
                                if (candidate == null)
                                {
                                    continue;
                                }

                                var videoUrl = (candidate.VideoUrl ?? string.Empty).Trim();
                                if (!string.IsNullOrWhiteSpace(videoUrl) && !seen.Add(videoUrl))
                                {
                                    continue;
                                }

                                var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                                candidate.SafetyScore = safetyResult.Score;
                                candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                                    ? "Low risk"
                                    : string.Join("; ", safetyResult.Reasons.Take(3));
                                candidate.SourcePlatform = Affiliate.AffiliateSourceIds.TikTok;
                                results.Add(candidate);

                                logAction?.Invoke(
                                    "[RapidAPI] " + results.Count + "/" + limit + ": "
                                    + candidate.ProductName + " | views " + candidate.PlayCount);
                            }

                            hasMore = root["hasMore"]?.Value<bool?>() ?? root["has_more"]?.Value<bool?>() ?? false;
                            if (root["hasMore"]?.Type == JTokenType.Integer)
                            {
                                hasMore = root["hasMore"].Value<int>() != 0;
                            }

                            cursor = root["cursor"]?.Value<long?>() ?? cursor;
                            searchId = root["log_pb"]?["impr_id"]?.ToString()
                                         ?? root["search_id"]?.ToString()
                                         ?? searchId;

                            if (!hasMore || items.Count == 0)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            logAction?.Invoke("[RapidAPI] Hoàn tất — " + results.Count + " video cho «" + keyword + "».");
            return results;
        }

        private static AffiliateCandidate MapItemToCandidate(string keyword, JObject item)
        {
            if (item == null)
            {
                return null;
            }

            var videoId = (item["id"] ?? item["aweme_id"] ?? item["video_id"])?.ToString()?.Trim();
            var author = item["author"] as JObject;
            var uniqueId = (author?["uniqueId"] ?? author?["unique_id"])?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                uniqueId = ExtractHandleFromShareUrl(item);
            }

            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(uniqueId))
            {
                return null;
            }

            var desc = (item["desc"] ?? item["title"] ?? item["description"])?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(desc))
            {
                desc = "Video TikTok";
            }

            var stats = item["stats"] as JObject ?? item["statistics"] as JObject;
            var hashtags = ExtractHashtags(item, desc);
            var cover = item["video"]?["cover"]?.ToString()
                        ?? item["video"]?["originCover"]?.ToString()
                        ?? item["video"]?["dynamicCover"]?.ToString()
                        ?? string.Empty;

            var nickname = (author?["nickname"] ?? author?["nickName"])?.ToString()?.Trim();
            var creatorDisplay = !string.IsNullOrWhiteSpace(nickname)
                ? nickname
                : "@" + uniqueId.TrimStart('@');

            return new AffiliateCandidate
            {
                SourceKeyword = keyword,
                ProductName = desc,
                Creator = creatorDisplay,
                VideoUrl = "https://www.tiktok.com/@" + uniqueId.TrimStart('@') + "/video/" + videoId,
                ProfileUrl = "https://www.tiktok.com/@" + uniqueId.TrimStart('@'),
                Hashtags = hashtags,
                ImageUrl = cover,
                LinkedProduct = "Chưa rõ",
                PlayCount = ReadLongStat(stats, "playCount", "play_count"),
                LikeCount = ReadLongStat(stats, "diggCount", "digg_count", "likeCount", "like_count"),
                CommentCount = ReadLongStat(stats, "commentCount", "comment_count"),
                ShareCount = ReadLongStat(stats, "shareCount", "share_count"),
                CollectCount = ReadLongStat(stats, "collectCount", "collect_count"),
                DurationSeconds = item["video"]?["duration"]?.Value<int?>() ?? 0,
                MetricsCapturedAtUtc = DateTime.UtcNow
            };
        }

        private static string ExtractHandleFromShareUrl(JObject item)
        {
            var share = (item["share_url"] ?? item["shareUrl"])?.ToString() ?? string.Empty;
            var marker = "/@";
            var idx = share.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return string.Empty;
            }

            var tail = share.Substring(idx + marker.Length);
            var slash = tail.IndexOf('/');
            return slash >= 0 ? tail.Substring(0, slash) : tail;
        }

        private static string ExtractHashtags(JObject item, string desc)
        {
            var tags = new List<string>();
            var challenges = item["challenges"] as JArray;
            if (challenges != null)
            {
                foreach (var c in challenges)
                {
                    var title = c?["title"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        tags.Add(title.StartsWith("#", StringComparison.Ordinal) ? title : "#" + title);
                    }
                }
            }

            if (tags.Count == 0 && !string.IsNullOrWhiteSpace(desc))
            {
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(desc, @"#[\p{L}\d_]+"))
                {
                    tags.Add(m.Value);
                }
            }

            return string.Join(" ", tags.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static long ReadLongStat(JObject stats, params string[] names)
        {
            if (stats == null)
            {
                return 0;
            }

            foreach (var name in names)
            {
                var token = stats[name];
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                if (token.Type == JTokenType.Integer)
                {
                    return token.Value<long>();
                }

                if (long.TryParse(token.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }

        private static string Truncate(string text, int max)
        {
            var s = (text ?? string.Empty).Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
