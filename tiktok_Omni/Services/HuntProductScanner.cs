using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services.Affiliate;

namespace tiktok_Omni.Services
{
    public sealed class HuntProductScanRequest
    {
        public string Keyword { get; set; } = string.Empty;
        public string ProfileName { get; set; } = "default";
        public int MaxResults { get; set; } = 30;
        public long MinSales { get; set; }
        public decimal MinRating { get; set; }
        public bool ScanTikTok { get; set; } = true;
        public bool ScanShopee { get; set; } = true;
    }

    public sealed class HuntProductScanner
    {
        private static readonly Regex ShopeeSoldRegex = new Regex(
            @"(?:sold|da\s*ban|lượt\s*bán)[^\d]{0,20}([\d.,]+)\s*([kKmM])?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ShopeeItemHrefRegex = new Regex(
            @"href=""([^""]*?/i\.[^.""]+?/(\d+)\.(\d+)[^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly AffiliateHunter _affiliateHunter;

        public HuntProductScanner(AffiliateHunter affiliateHunter)
        {
            _affiliateHunter = affiliateHunter ?? throw new ArgumentNullException(nameof(affiliateHunter));
        }

        public async Task<List<HuntProductCandidate>> ScanAsync(
            HuntProductScanRequest request,
            ConfigManager configManager,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var keyword = (request.Keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                throw new ArgumentException("Từ khoá không được để trống.");
            }

            if (!request.ScanTikTok && !request.ScanShopee)
            {
                throw new ArgumentException("Chọn ít nhất một nền tảng: TikTok hoặc Shopee.");
            }

            var profileFolder = ProfileScopedPaths.ResolveProfileName(request.ProfileName);
            var profileForBrowser = string.IsNullOrWhiteSpace(request.ProfileName)
                ? "default"
                : request.ProfileName.Trim();
            var merged = new List<HuntProductCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (request.ScanTikTok)
            {
                log?.Invoke("[Săn SP] TikTok Shop — từ khoá: «" + keyword + "» (www.tiktok.com, người mua — không cần Affiliate Creator)");
                var tikTokRows = await ScanTikTokShopAsync(
                        keyword,
                        request.MaxResults,
                        profileForBrowser,
                        configManager,
                        cancellationToken,
                        log)
                    .ConfigureAwait(false);
                foreach (var row in tikTokRows)
                {
                    if (seen.Add(row.ProductLink))
                    {
                        merged.Add(row);
                    }
                }
            }

            if (request.ScanShopee)
            {
                log?.Invoke("[Săn SP] Shopee — tải trang tìm kiếm…");
                var shopeeRows = await ScanShopeeAsync(keyword, request.MaxResults, profileFolder, cancellationToken, log)
                    .ConfigureAwait(false);
                foreach (var row in shopeeRows)
                {
                    if (seen.Add(row.ProductLink))
                    {
                        merged.Add(row);
                    }
                }
            }

            var filtered = ApplyFilters(merged, request.MinSales, request.MinRating);
            var sorted = SortBestSellersFirst(filtered);
            log?.Invoke("[Săn SP] Gộp xong: " + sorted.Count + " sản phẩm sau lọc.");
            return sorted;
        }

        private async Task<List<HuntProductCandidate>> ScanTikTokShopAsync(
            string keyword,
            int maxResults,
            string profile,
            ConfigManager configManager,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            // TikTok Shop công khai (www.tiktok.com) — giống tìm trên app điện thoại, không cần affiliate.tiktok.com.
            var affiliates = await _affiliateHunter.HuntTikTokConsumerShopAsync(
                    keyword,
                    maxResults,
                    cancellationToken,
                    log,
                    configManager,
                    profile)
                .ConfigureAwait(false);

            return affiliates
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.VideoUrl))
                .Select(a => new HuntProductCandidate
                {
                    ProfileName = profile,
                    SourcePlatform = "TikTok",
                    ProductName = string.IsNullOrWhiteSpace(a.ProductName) ? "TikTok Shop" : a.ProductName.Trim(),
                    ProductLink = a.VideoUrl.Trim(),
                    ImageUrl = a.ImageUrl ?? string.Empty,
                    SalesVolume = 0,
                    Rating = 0,
                    Price = a.Price ?? string.Empty,
                    Commission = a.CommissionRate ?? string.Empty
                })
                .ToList();
        }

        private static async Task<List<HuntProductCandidate>> ScanShopeeAsync(
            string keyword,
            int maxResults,
            string profile,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            var results = new List<HuntProductCandidate>();
            var encoded = Uri.EscapeDataString(keyword);
            var url = "https://shopee.vn/search?keyword=" + encoded;

            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(35) })
                {
                    http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                    http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "vi-VN,vi;q=0.9,en;q=0.8");

                    var html = await http.GetStringAsync(url).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(html))
                    {
                        log?.Invoke("[Săn SP/Shopee] Không tải được trang tìm kiếm.");
                        return results;
                    }

                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match m in ShopeeItemHrefRegex.Matches(html))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (results.Count >= maxResults)
                        {
                            break;
                        }

                        var href = m.Groups[1].Value;
                        var shopId = m.Groups[2].Value;
                        var itemId = m.Groups[3].Value;
                        if (string.IsNullOrWhiteSpace(shopId) || string.IsNullOrWhiteSpace(itemId))
                        {
                            continue;
                        }

                        var link = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? href
                            : "https://shopee.vn" + (href.StartsWith("/") ? href : "/" + href);
                        var key = shopId + "_" + itemId;
                        if (!seen.Add(key))
                        {
                            continue;
                        }

                        var sold = TryParseSoldNear(html, m.Index);
                        results.Add(new HuntProductCandidate
                        {
                            ProfileName = profile,
                            SourcePlatform = "Shopee",
                            ProductName = "Shopee " + itemId,
                            ProductLink = link.Split('?')[0],
                            ImageUrl = string.Empty,
                            SalesVolume = sold,
                            Rating = 0,
                            Price = string.Empty,
                            Commission = string.Empty
                        });
                    }

                    if (results.Count == 0)
                    {
                        log?.Invoke(
                            "[Săn SP/Shopee] Không trích xuất được link sản phẩm (Shopee có thể chặn bot — thử thêm thủ công hoặc chạy lại sau).");
                    }
                    else
                    {
                        log?.Invoke("[Săn SP/Shopee] Đã lấy " + results.Count + " link sản phẩm.");
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[Săn SP/Shopee] Lỗi: " + ex.Message);
            }

            return results;
        }

        private static long TryParseSoldNear(string html, int anchorIndex)
        {
            if (string.IsNullOrEmpty(html) || anchorIndex < 0)
            {
                return 0;
            }

            var start = Math.Max(0, anchorIndex - 400);
            var len = Math.Min(900, html.Length - start);
            var slice = html.Substring(start, len);
            var m = ShopeeSoldRegex.Match(slice);
            if (!m.Success)
            {
                return 0;
            }

            return ParseCountToken(m.Groups[1].Value, m.Groups[2].Value);
        }

        private static long ParseCountToken(string digits, string suffix)
        {
            if (string.IsNullOrWhiteSpace(digits))
            {
                return 0;
            }

            var normalized = digits.Replace(",", string.Empty).Replace(".", string.Empty);
            if (!long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                var s = suffix[0];
                if (s == 'k' || s == 'K')
                {
                    n *= 1000;
                }
                else if (s == 'm' || s == 'M')
                {
                    n *= 1000000;
                }
            }

            return n;
        }

        internal static List<HuntProductCandidate> ApplyFilters(
            IList<HuntProductCandidate> rows,
            long minSales,
            decimal minRating)
        {
            IEnumerable<HuntProductCandidate> q = rows ?? Array.Empty<HuntProductCandidate>();
            if (minSales > 0)
            {
                q = q.Where(r => r.SalesVolume >= minSales);
            }

            if (minRating > 0)
            {
                q = q.Where(r => r.Rating >= minRating);
            }

            return q.ToList();
        }

        internal static List<HuntProductCandidate> SortBestSellersFirst(IList<HuntProductCandidate> rows)
        {
            return (rows ?? Array.Empty<HuntProductCandidate>())
                .OrderByDescending(r => r.SalesVolume)
                .ThenByDescending(r => r.Rating)
                .ThenBy(r => r.ProductName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
