using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Affiliate
{
    /// <summary>Quét Facebook Reels — giải mã HTML công khai (og tags + URL reel).</summary>
    public sealed class FacebookReelsHuntService
    {
        private static readonly Regex ReelPathRegex = new Regex(
            @"(?:https?:\\?/\\?/)?(?:www\.)?facebook\.com/reel/(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ReelPathBareRegex = new Regex(
            @"/reel/(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<List<AffiliateCandidate>> HuntAsync(
            string keyword,
            int limit,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                throw new ArgumentException("Keyword is required.", nameof(keyword));
            }

            limit = Math.Max(1, Math.Min(limit, 40));
            var tag = keyword.Trim().Replace(" ", string.Empty);
            var searchUrls = new[]
            {
                "https://www.facebook.com/watch/search/?q=" + Uri.EscapeDataString(keyword.Trim()),
                "https://www.facebook.com/hashtag/" + Uri.EscapeDataString(tag)
            };

            var reelUrls = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var http = CreateHttpClient())
            {
                foreach (var pageUrl in searchUrls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reelUrls.Count >= limit)
                    {
                        break;
                    }

                    try
                    {
                        logAction?.Invoke("[Facebook] GET " + pageUrl);
                        var html = await http.GetStringAsync(pageUrl).ConfigureAwait(false);
                        CollectReelUrlsFromHtml(html, reelUrls, seenIds, limit);
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke("[Facebook] Không đọc được trang: " + ex.Message);
                    }
                }

                var results = new List<AffiliateCandidate>();
                var safety = new SafetyScoreService();

                foreach (var reelUrl in reelUrls.Take(limit))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                        var detailHtml = await http.GetStringAsync(reelUrl).ConfigureAwait(false);
                        var title = ExtractMeta(detailHtml, "og:title") ?? ExtractMeta(detailHtml, "twitter:title") ?? "Facebook Reel";
                        var desc = ExtractMeta(detailHtml, "og:description") ?? string.Empty;
                        var image = ExtractMeta(detailHtml, "og:image") ?? string.Empty;

                        var candidate = new AffiliateCandidate
                        {
                            SourceKeyword = keyword.Trim(),
                            SourcePlatform = AffiliateSourceIds.Facebook,
                            ProductName = title.Trim(),
                            VideoUrl = reelUrl,
                            ImageUrl = image.Trim(),
                            Hashtags = desc.Length > 0 && desc.Length <= 200 ? desc.Trim() : string.Empty,
                            Creator = ExtractCreatorFromTitle(title),
                            LinkedProduct = "Facebook Reels"
                        };

                        var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                        candidate.SafetyScore = safetyResult.Score;
                        candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                            ? "Low risk"
                            : string.Join("; ", safetyResult.Reasons.Take(3));

                        results.Add(candidate);
                        logAction?.Invoke($"[Facebook] {results.Count}/{limit}: {candidate.ProductName}");
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke("[Facebook] Bỏ qua reel: " + ex.Message);
                    }
                }

                if (results.Count == 0)
                {
                    logAction?.Invoke(
                        $"[Facebook] Không tìm thấy Reels công khai cho «{keyword}». Facebook có thể yêu cầu đăng nhập — thử từ khoá khác hoặc Graph API token (sắp tới).");
                }

                return results;
            }
        }

        private static void CollectReelUrlsFromHtml(
            string html,
            List<string> reelUrls,
            HashSet<string> seenIds,
            int limit)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return;
            }

            foreach (Match m in ReelPathRegex.Matches(html))
            {
                var id = m.Groups["id"].Value;
                if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                {
                    continue;
                }

                reelUrls.Add("https://www.facebook.com/reel/" + id);
                if (reelUrls.Count >= limit)
                {
                    return;
                }
            }

            foreach (Match m in ReelPathBareRegex.Matches(html))
            {
                var id = m.Groups["id"].Value;
                if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                {
                    continue;
                }

                reelUrls.Add("https://www.facebook.com/reel/" + id);
                if (reelUrls.Count >= limit)
                {
                    return;
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,vi;q=0.8");
            return http;
        }

        private static string ExtractMeta(string html, string property)
        {
            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(property))
            {
                return null;
            }

            var patterns = new[]
            {
                "<meta[^>]+property=[\"']" + Regex.Escape(property) + "[\"'][^>]+content=[\"']([^\"']+)[\"']",
                "<meta[^>]+content=[\"']([^\"']+)[\"'][^>]+property=[\"']" + Regex.Escape(property) + "[\"']",
                "<meta[^>]+name=[\"']" + Regex.Escape(property) + "[\"'][^>]+content=[\"']([^\"']+)[\"']"
            };

            foreach (var pattern in patterns)
            {
                var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (m.Success)
                {
                    return DecodeHtml(m.Groups[1].Value);
                }
            }

            return null;
        }

        private static string DecodeHtml(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return System.Net.WebUtility.HtmlDecode(raw)
                .Replace("\\u0026", "&")
                .Replace("\\/", "/")
                .Trim();
        }

        private static string ExtractCreatorFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var idx = title.IndexOf(" - ", StringComparison.Ordinal);
            if (idx > 0 && idx < title.Length - 3)
            {
                return title.Substring(0, idx).Trim();
            }

            return string.Empty;
        }
    }
}
