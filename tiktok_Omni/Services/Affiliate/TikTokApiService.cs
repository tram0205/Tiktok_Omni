using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Affiliate
{
    /// <summary>
    /// TikTok RapidAPI (tiktok-api23 / Lundehund) — Search Video + Top Products trending.
    /// </summary>
    public sealed class TikTokApiService
    {
        public const string DefaultRapidApiHost = "tiktok-api23.p.rapidapi.com";

        /// <summary>RapidAPI «Get Top Products» (Ads Creative Center) — host tiktok-api23.</summary>
        public const string TopProductsEndpointPath = "/api/trending/top-products";

        /// <summary>Lọc mặc định: chỉ giữ sản phẩm có hoa hồng / CVR &gt; 5%.</summary>
        public const double DefaultMinAffiliateCommissionPercent = 5.0;

        private const int DefaultPageDelayMs = 700;
        private const int MaxPaginationRounds = 25;

        private static readonly HttpClient SharedHttp = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            return client;
        }

        /// <summary>
        /// Săn video theo từ khoá qua GET /api/search/video — phân trang cursor + search_id, sort view cao nhất.
        /// </summary>
        public async Task<List<AffiliateCandidate>> SearchVideosAsync(
            string keyword,
            int collectLimit,
            string rapidApiKey,
            string rapidApiHost,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var kw = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(kw))
            {
                throw new ArgumentException("Keyword is required.", nameof(keyword));
            }

            var apiKey = (rapidApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Chưa cấu hình TikTok RapidAPI key (tab Cài đặt).");
            }

            collectLimit = Math.Max(1, Math.Min(collectLimit, 120));
            var host = NormalizeHost(rapidApiHost);

            logAction?.Invoke($"[TikTok API] Săn video «{kw}» — mục tiêu {collectLimit} ứng viên (host: {host})…");

            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();

            var cursor = 0;
            var searchId = "0";
            var round = 0;
            var stagnantRounds = 0;

            while (results.Count < collectLimit && round < MaxPaginationRounds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                round++;

                var url =
                    $"https://{host}/api/search/video?keyword={Uri.EscapeDataString(kw)}&cursor={cursor}&search_id={Uri.EscapeDataString(searchId)}";

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("x-rapidapi-host", host);
                    request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey);

                    HttpResponseMessage response;
                    try
                    {
                        response = await SharedHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                    {
                        throw new InvalidOperationException("Không kết nối được TikTok RapidAPI: " + ex.Message, ex);
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var snippet = Truncate(body, 280);
                        if ((int)response.StatusCode == 429)
                        {
                            throw new InvalidOperationException("TikTok RapidAPI rate limit (429). Thử lại sau vài phút.");
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            throw new InvalidOperationException("TikTok RapidAPI key không hợp lệ hoặc chưa subscribe API (401/403).");
                        }

                        throw new InvalidOperationException(
                            $"TikTok RapidAPI HTTP {(int)response.StatusCode}: {snippet}");
                    }

                    JObject root;
                    try
                    {
                        root = JObject.Parse(body);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("TikTok RapidAPI trả JSON không hợp lệ: " + ex.Message);
                    }

                    var itemsOnPage = (root["item_list"] as JArray)?.Count ?? 0;
                    var batchAdded = ParseSearchPage(root, kw, seen, safety, results, collectLimit);
                    var hasMore = ReadHasMore(root, itemsOnPage, results.Count, collectLimit);
                    var nextCursor = ReadCursor(root);
                    var nextSearchId = ReadSearchId(root, searchId);

                    logAction?.Invoke(
                        $"[TikTok API] Trang {round}: +{batchAdded} video (tổng {results.Count}/{collectLimit}, cursor={cursor}→{nextCursor}, hasMore={hasMore}).");

                    if (results.Count >= collectLimit)
                    {
                        break;
                    }

                    if (batchAdded == 0)
                    {
                        stagnantRounds++;
                    }
                    else
                    {
                        stagnantRounds = 0;
                    }

                    if (!hasMore)
                    {
                        if (batchAdded == 0 || itemsOnPage == 0)
                        {
                            logAction?.Invoke("[TikTok API] API báo hết trang — dừng phân trang.");
                            break;
                        }
                    }

                    if (stagnantRounds >= 4)
                    {
                        logAction?.Invoke("[TikTok API] Không thêm video mới sau 4 lần — dừng phân trang.");
                        break;
                    }

                    if (nextCursor <= cursor)
                    {
                        nextCursor = cursor + Math.Max(1, itemsOnPage > 0 ? itemsOnPage : batchAdded);
                    }

                    cursor = nextCursor;
                    searchId = nextSearchId;

                    if (results.Count < collectLimit && round < MaxPaginationRounds)
                    {
                        await Task.Delay(DefaultPageDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            SortByTrendScoreDescending(results);
            if (results.Count > collectLimit)
            {
                results = results.Take(collectLimit).ToList();
            }
            logAction?.Invoke($"[TikTok API] Xong «{kw}» — {results.Count} video (view cao nhất: {FormatViews(results.FirstOrDefault()?.PlayCount ?? 0)}).");
            return results;
        }

        private const string FallbackTopProductsCountry = "US";

        /// <summary>
        /// Săn sản phẩm affiliate trending: GET TopProductsEndpointPath + lọc HH &gt; 5%.
        /// </summary>
        public Task<List<AffiliateCandidate>> SearchAffiliateTrendingProductsAsync(
            string keyword,
            int collectLimit,
            string rapidApiKey,
            string rapidApiHost,
            string countryCode,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            return SearchAffiliateTrendingProductsAsync(
                keyword,
                collectLimit,
                rapidApiKey,
                rapidApiHost,
                countryCode,
                DefaultMinAffiliateCommissionPercent,
                logAction,
                cancellationToken);
        }

        /// <summary>
        /// Săn sản phẩm affiliate trending qua RapidAPI Get Top Products (Ads Creative Center).
        /// </summary>
        public async Task<List<AffiliateCandidate>> SearchAffiliateTrendingProductsAsync(
            string keyword,
            int collectLimit,
            string rapidApiKey,
            string rapidApiHost,
            string countryCode,
            double minCommissionPercent,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            return await SearchTopProductsAsync(
                    keyword,
                    collectLimit,
                    rapidApiKey,
                    rapidApiHost,
                    countryCode,
                    logAction,
                    cancellationToken,
                    minCommissionPercent)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Săn sản phẩm TikTok Shop trending qua GET /api/trending/top-products (Ads Creative Center).
        /// </summary>
        public async Task<List<AffiliateCandidate>> SearchTopProductsAsync(
            string keyword,
            int collectLimit,
            string rapidApiKey,
            string rapidApiHost,
            string countryCode,
            Action<string> logAction,
            CancellationToken cancellationToken,
            double minCommissionPercent = 0)
        {
            var kw = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(kw))
            {
                throw new ArgumentException("Keyword is required.", nameof(keyword));
            }

            var apiKey = (rapidApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Chưa cấu hình TikTok RapidAPI key (tab Cài đặt).");
            }

            collectLimit = Math.Max(1, Math.Min(collectLimit, 120));
            var host = NormalizeHost(rapidApiHost);
            var country = NormalizeCountryCode(countryCode);

            if (minCommissionPercent > 0)
            {
                logAction?.Invoke(
                    $"[TikTok API] Săn sản phẩm «{kw}» — {TopProductsEndpointPath}, quốc gia {country}, "
                    + $"mục tiêu {collectLimit} SP (lọc HH > {minCommissionPercent.ToString("0.##", CultureInfo.InvariantCulture)}%)…");
            }
            else
            {
                logAction?.Invoke(
                    $"[TikTok API] Săn sản phẩm «{kw}» — {TopProductsEndpointPath}, quốc gia {country}, mục tiêu {collectLimit} SP…");
            }

            var results = await CollectTopProductsForCountryAsync(
                    kw,
                    collectLimit,
                    apiKey,
                    host,
                    country,
                    minCommissionPercent,
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);

            if (results.Count == 0
                && !string.Equals(country, FallbackTopProductsCountry, StringComparison.OrdinalIgnoreCase))
            {
                logAction?.Invoke(
                    $"[TikTok API] «{kw}» rỗng với {country} — thử lại country_code={FallbackTopProductsCountry}…");
                results = await CollectTopProductsForCountryAsync(
                        kw,
                        collectLimit,
                        apiKey,
                        host,
                        FallbackTopProductsCountry,
                        minCommissionPercent,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (results.Count > 0)
            {
                await EnrichTopProductsWithMetricsAsync(
                        results,
                        apiKey,
                        host,
                        logAction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            SortTopProductsDescending(results);
            if (results.Count > collectLimit)
            {
                results = results.Take(collectLimit).ToList();
            }

            logAction?.Invoke(
                $"[TikTok API] Xong «{kw}» — {results.Count} sản phẩm (post trend cao nhất: {results.FirstOrDefault()?.PlayCount ?? 0}).");
            return results;
        }

        private async Task<List<AffiliateCandidate>> CollectTopProductsForCountryAsync(
            string keyword,
            int collectLimit,
            string apiKey,
            string host,
            string countryCode,
            double minCommissionPercent,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();
            var page = 1;
            var stagnantPages = 0;
            var skippedLowCommission = 0;
            var country = NormalizeCountryCode(countryCode);
            var maxPages = minCommissionPercent > 0 ? 20 : 12;

            while (results.Count < collectLimit && page <= maxPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var url =
                    $"https://{host}{TopProductsEndpointPath}?keyword={Uri.EscapeDataString(keyword)}" +
                    $"&page={page}&country_code={Uri.EscapeDataString(country)}&order_by=post&last=7";

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("x-rapidapi-host", host);
                    request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey);

                    HttpResponseMessage response;
                    try
                    {
                        response = await SharedHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                    {
                        throw new InvalidOperationException("Không kết nối được TikTok RapidAPI: " + ex.Message, ex);
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        if ((int)response.StatusCode == 429)
                        {
                            throw new InvalidOperationException("TikTok RapidAPI rate limit (429). Thử lại sau vài phút.");
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            throw new InvalidOperationException("TikTok RapidAPI key không hợp lệ hoặc chưa subscribe API (401/403).");
                        }

                        throw new InvalidOperationException(
                            $"TikTok RapidAPI HTTP {(int)response.StatusCode}: {Truncate(body, 280)}");
                    }

                    JObject root;
                    try
                    {
                        root = JObject.Parse(body);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("TikTok RapidAPI trả JSON không hợp lệ: " + ex.Message);
                    }

                    var list = ReadTopProductsList(root);
                    if (list == null || list.Count == 0)
                    {
                        logAction?.Invoke($"[TikTok API] {country} trang {page}: không có sản phẩm — dừng.");
                        break;
                    }

                    var batchAdded = ParseTopProductsPage(
                        list,
                        keyword,
                        seen,
                        safety,
                        results,
                        collectLimit,
                        minCommissionPercent,
                        ref skippedLowCommission);
                    var hasMore = ReadTopProductsHasMore(root, list.Count, results.Count, collectLimit);

                    logAction?.Invoke(
                        minCommissionPercent > 0
                            ? $"[TikTok API] {country} trang {page}: +{batchAdded} SP (tổng {results.Count}/{collectLimit}, hasMore={hasMore}, bỏ HH≤{minCommissionPercent.ToString("0.##", CultureInfo.InvariantCulture)}%: {skippedLowCommission})."
                            : $"[TikTok API] {country} trang {page}: +{batchAdded} SP (tổng {results.Count}/{collectLimit}, hasMore={hasMore}).");

                    if (results.Count >= collectLimit)
                    {
                        break;
                    }

                    if (batchAdded == 0)
                    {
                        stagnantPages++;
                    }
                    else
                    {
                        stagnantPages = 0;
                    }

                    if (!hasMore || stagnantPages >= 2)
                    {
                        break;
                    }

                    page++;
                    await Task.Delay(DefaultPageDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            return results;
        }

        private async Task EnrichTopProductsWithMetricsAsync(
            List<AffiliateCandidate> results,
            string apiKey,
            string host,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (results == null || results.Count == 0)
            {
                return;
            }

            logAction?.Invoke($"[TikTok API] Lấy metrics (top-products/metrics) cho {results.Count} SP…");
            var enriched = 0;

            foreach (var candidate in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate == null)
                {
                    continue;
                }

                var productId = ExtractTrendingProductId(candidate.VideoUrl);
                if (string.IsNullOrWhiteSpace(productId))
                {
                    continue;
                }

                try
                {
                    var metricsRoot = await FetchTopProductMetricsAsync(
                            productId,
                            apiKey,
                            host,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (metricsRoot == null)
                    {
                        continue;
                    }

                    if (ApplyTopProductMetrics(candidate, metricsRoot))
                    {
                        enriched++;
                    }

                    await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"[TikTok API] Metrics SP {productId} lỗi: {ex.Message}");
                }
            }

            logAction?.Invoke($"[TikTok API] Metrics xong — cập nhật {enriched}/{results.Count} SP.");
        }

        private async Task<JObject> FetchTopProductMetricsAsync(
            string productId,
            string apiKey,
            string host,
            CancellationToken cancellationToken)
        {
            var id = (productId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var url = $"https://{host}/api/trending/top-products/metrics?product_id={Uri.EscapeDataString(id)}";
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("x-rapidapi-host", host);
                request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey);

                var response = await SharedHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                try
                {
                    return JObject.Parse(body);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static bool ApplyTopProductMetrics(AffiliateCandidate candidate, JObject metricsRoot)
        {
            if (candidate == null || metricsRoot == null)
            {
                return false;
            }

            var info = AsJObject(metricsRoot["info"]) ?? AsJObject(metricsRoot["data"]?["info"]) ?? metricsRoot;
            var changed = false;

            var cover = FirstNonEmpty(
                info?["cover_url"]?.ToString(),
                info?["coverUrl"]?.ToString());
            if (!string.IsNullOrWhiteSpace(cover) && string.IsNullOrWhiteSpace(candidate.ImageUrl))
            {
                candidate.ImageUrl = cover;
                changed = true;
            }

            var urlTitle = (info?["url_title"] ?? info?["urlTitle"])?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(urlTitle)
                && (string.IsNullOrWhiteSpace(candidate.ProductName)
                    || string.Equals(candidate.ProductName, "TikTok Shop product", StringComparison.OrdinalIgnoreCase)))
            {
                candidate.ProductName = HumanizeUrlTitle(urlTitle);
                candidate.LinkedProduct = candidate.ProductName;
                changed = true;
            }

            var postMetrics = info?["post_metrics"] as JArray
                ?? metricsRoot["post_metrics"] as JArray
                ?? metricsRoot["data"]?["post_metrics"] as JArray;
            var postTrend = ReadLatestMetricLong(postMetrics);
            if (postTrend > 0)
            {
                candidate.PlayCount = postTrend;
                candidate.MetricsCapturedAtUtc = DateTime.UtcNow;
                changed = true;
            }

            var ctrMetrics = info?["ctr_metrics"] as JArray
                ?? metricsRoot["ctr_metrics"] as JArray
                ?? metricsRoot["data"]?["ctr_metrics"] as JArray;
            var ctr = ReadLatestMetricDouble(ctrMetrics);
            if (ctr > 0 && ParseCommissionPercent(candidate.CommissionRate) <= 0)
            {
                candidate.CommissionRate = ctr.ToString("0.##", CultureInfo.InvariantCulture) + "% CTR";
                changed = true;
            }

            return changed;
        }

        private static long ReadLatestMetricLong(JArray series)
        {
            if (series == null || series.Count == 0)
            {
                return 0L;
            }

            long bestTime = long.MinValue;
            long bestValue = 0L;
            foreach (var token in series)
            {
                if (!(token is JObject point))
                {
                    continue;
                }

                var time = ReadStatLong(point, "time");
                var value = ReadStatLong(point, "value");
                if (time >= bestTime)
                {
                    bestTime = time;
                    bestValue = value;
                }
            }

            return bestValue;
        }

        private static double ReadLatestMetricDouble(JArray series)
        {
            if (series == null || series.Count == 0)
            {
                return 0d;
            }

            long bestTime = long.MinValue;
            double bestValue = 0d;
            foreach (var token in series)
            {
                if (!(token is JObject point))
                {
                    continue;
                }

                var time = ReadStatLong(point, "time");
                var tok = point["value"];
                double value = 0d;
                if (tok != null)
                {
                    if (tok.Type == JTokenType.Float || tok.Type == JTokenType.Integer)
                    {
                        value = tok.Value<double>();
                    }
                    else
                    {
                        double.TryParse(tok.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                    }
                }

                if (time >= bestTime)
                {
                    bestTime = time;
                    bestValue = value;
                }
            }

            return bestValue;
        }

        private static string ExtractTrendingProductId(string productLink)
        {
            var url = (productLink ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var marker = "/view/product/";
            var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return string.Empty;
            }

            var start = idx + marker.Length;
            var end = start;
            while (end < url.Length && char.IsDigit(url[end]))
            {
                end++;
            }

            return end > start ? url.Substring(start, end - start) : string.Empty;
        }

        /// <summary>Legacy — tìm sản phẩm affiliate TikTok Shop qua cookie (stub).</summary>
        public async Task<List<AffiliateCandidate>> SearchProductsAsync(string keyword, string cookieString)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Cookie", cookieString);
                client.DefaultRequestHeaders.Add("Referer", "https://affiliate.tiktok.com/");

                var apiUrl = $"https://affiliate.tiktok.com/api/v1/product/search?keyword={Uri.EscapeDataString(keyword)}";
                var response = await client.GetAsync(apiUrl).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Cookie hết hạn hoặc không hợp lệ (401 Unauthorized).");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseProductJsonResponse(json);
            }
        }

        /// <summary>Kiểm tra key bằng một request search nhẹ.</summary>
        public async Task<string> TestSearchVideoAsync(string rapidApiKey, string rapidApiHost, CancellationToken cancellationToken)
        {
            var apiKey = (rapidApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Chưa nhập RapidAPI key.";
            }

            var host = NormalizeHost(rapidApiHost);
            var url = $"https://{host}/api/search/video?keyword=cat&cursor=0&search_id=0";
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("x-rapidapi-host", host);
                request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey);

                var response = await SharedHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return $"HTTP {(int)response.StatusCode}: {Truncate(body, 400)}";
                }

                var root = JObject.Parse(body);
                var count = (root["item_list"] as JArray)?.Count ?? 0;
                return count > 0
                    ? $"OK — nhận {count} video mẫu (keyword=cat)."
                    : "OK — kết nối thành công nhưng item_list rỗng (thử từ khoá khác).";
            }
        }

        /// <summary>Kiểm tra key bằng GET /api/trending/top-products (+ metrics mẫu).</summary>
        public async Task<string> TestTopProductsAsync(
            string rapidApiKey,
            string rapidApiHost,
            string countryCode,
            CancellationToken cancellationToken)
        {
            var apiKey = (rapidApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Chưa nhập RapidAPI key.";
            }

            var host = NormalizeHost(rapidApiHost);
            var country = NormalizeCountryCode(countryCode);

            try
            {
                var primary = await ProbeTopProductsCountryAsync(apiKey, host, country, cancellationToken)
                    .ConfigureAwait(false);
                if (primary.ItemCount > 0)
                {
                    var metricsNote = await ProbeTopProductMetricsSampleAsync(
                            apiKey,
                            host,
                            primary.FirstProductId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return $"OK — top-products {primary.ItemCount} SP mẫu ({country}, keyword=phone). {metricsNote}";
                }

                if (!string.Equals(country, FallbackTopProductsCountry, StringComparison.OrdinalIgnoreCase))
                {
                    var fallback = await ProbeTopProductsCountryAsync(
                            apiKey,
                            host,
                            FallbackTopProductsCountry,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (fallback.ItemCount > 0)
                    {
                        var metricsNote = await ProbeTopProductMetricsSampleAsync(
                                apiKey,
                                host,
                                fallback.FirstProductId,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return $"OK — top-products {fallback.ItemCount} SP (fallback {FallbackTopProductsCountry}, {country} rỗng). {metricsNote}";
                    }

                    return "Top-products rỗng cả " + country + " lẫn " + FallbackTopProductsCountry
                        + " (data=" + primary.DataType + "). Thử từ khoá khác.";
                }

                return "OK — top-products kết nối được nhưng list rỗng "
                    + $"(data={primary.DataType}, thử từ khoá khác).";
            }
            catch (Exception ex)
            {
                return "Top-products parse lỗi: " + ex.Message;
            }
        }

        private async Task<(int ItemCount, string FirstProductId, string DataType)> ProbeTopProductsCountryAsync(
            string apiKey,
            string host,
            string countryCode,
            CancellationToken cancellationToken)
        {
            var country = NormalizeCountryCode(countryCode);
            var url =
                $"https://{host}/api/trending/top-products?keyword=phone&page=1&country_code={Uri.EscapeDataString(country)}&order_by=post&last=7";
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("x-rapidapi-host", host);
                request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey);

                var response = await SharedHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Top-products HTTP {(int)response.StatusCode}: {Truncate(body, 400)}");
                }

                var root = JObject.Parse(body);
                var apiMessage = root["message"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(apiMessage)
                    && !string.Equals(apiMessage, "success", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Top-products: " + Truncate(apiMessage, 300));
                }

                var list = ReadTopProductsList(root);
                var count = list?.Count ?? 0;
                var firstId = string.Empty;
                if (count > 0 && list[0] is JObject first)
                {
                    firstId = FirstNonEmpty(
                        first["product_id"]?.ToString(),
                        first["productId"]?.ToString(),
                        first["id"]?.ToString());
                }

                var dataType = root["data"]?.Type.ToString() ?? "null";
                return (count, firstId, dataType);
            }
        }

        private async Task<string> ProbeTopProductMetricsSampleAsync(
            string apiKey,
            string host,
            string productId,
            CancellationToken cancellationToken)
        {
            var id = (productId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Metrics: bỏ qua (không có product_id).";
            }

            var metricsRoot = await FetchTopProductMetricsAsync(id, apiKey, host, cancellationToken).ConfigureAwait(false);
            if (metricsRoot == null)
            {
                return "Metrics: không đọc được cho product_id=" + id + ".";
            }

            var info = AsJObject(metricsRoot["info"]) ?? AsJObject(metricsRoot["data"]?["info"]) ?? metricsRoot;
            var post = ReadLatestMetricLong(info?["post_metrics"] as JArray ?? metricsRoot["post_metrics"] as JArray);
            return post > 0
                ? "Metrics OK (post trend=" + post + ")."
                : "Metrics OK — kết nối được.";
        }

        private static int ParseSearchPage(
            JObject root,
            string keyword,
            HashSet<string> seen,
            SafetyScoreService safety,
            List<AffiliateCandidate> results,
            int collectLimit)
        {
            var added = 0;
            var items = root["item_list"] as JArray;
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            foreach (var token in items)
            {
                if (results.Count >= collectLimit)
                {
                    break;
                }

                if (!(token is JObject item))
                {
                    continue;
                }

                var candidate = MapItemToCandidate(item, keyword);
                if (candidate == null)
                {
                    continue;
                }

                var url = (candidate.VideoUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
                {
                    continue;
                }

                var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                candidate.SafetyScore = safetyResult.Score;
                candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                    ? "Low risk"
                    : string.Join("; ", safetyResult.Reasons.Take(3));

                results.Add(candidate);
                added++;
            }

            return added;
        }

        private static int ParseTopProductsPage(
            JArray list,
            string keyword,
            HashSet<string> seen,
            SafetyScoreService safety,
            List<AffiliateCandidate> results,
            int collectLimit,
            double minCommissionPercent,
            ref int skippedLowCommission)
        {
            var added = 0;
            if (list == null || list.Count == 0)
            {
                return 0;
            }

            foreach (var token in list)
            {
                if (results.Count >= collectLimit)
                {
                    break;
                }

                if (!(token is JObject item))
                {
                    continue;
                }

                if (minCommissionPercent > 0)
                {
                    var commissionPct = ReadCommissionPercentFromItem(item);
                    if (commissionPct <= minCommissionPercent)
                    {
                        skippedLowCommission++;
                        continue;
                    }
                }

                var candidate = MapTopProductToCandidate(item, keyword);
                if (candidate == null)
                {
                    continue;
                }

                var url = (candidate.VideoUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
                {
                    continue;
                }

                var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                candidate.SafetyScore = safetyResult.Score;
                candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                    ? "Low risk"
                    : string.Join("; ", safetyResult.Reasons.Take(3));

                results.Add(candidate);
                added++;
            }

            return added;
        }

        internal static AffiliateCandidate MapTopProductToCandidate(JObject item, string keyword)
        {
            if (item == null)
            {
                return null;
            }

            var productId = FirstNonEmpty(
                item["product_id"]?.ToString(),
                item["productId"]?.ToString(),
                item["id"]?.ToString());
            var urlTitle = (item["url_title"] ?? item["urlTitle"])?.ToString()?.Trim();
            var explicitUrl = FirstNonEmpty(
                item["product_url"]?.ToString(),
                item["productUrl"]?.ToString(),
                item["url"]?.ToString());
            var productLink = !string.IsNullOrWhiteSpace(explicitUrl)
                              && explicitUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? explicitUrl.Trim()
                : BuildTrendingProductLink(productId, urlTitle, keyword);
            if (string.IsNullOrWhiteSpace(productLink))
            {
                return null;
            }

            var categoryLabel = ReadEcomCategoryLabel(item);
            var productName = FirstNonEmpty(
                item["product_name"]?.ToString(),
                item["productName"]?.ToString());
            if (string.IsNullOrWhiteSpace(productName))
            {
                productName = !string.IsNullOrWhiteSpace(urlTitle)
                    ? HumanizeUrlTitle(urlTitle)
                    : (!string.IsNullOrWhiteSpace(categoryLabel) ? categoryLabel : "TikTok Shop product");
            }
            else
            {
                productName = productName.Trim();
            }

            var postCount = ReadProductSalesVolume(item);
            var likeCount = ReadStatLong(item, "like");
            var commentCount = ReadStatLong(item, "comment");
            var shareCount = ReadStatLong(item, "share");
            var coverUrl = FirstNonEmpty(
                item["cover_url"]?.ToString(),
                item["coverUrl"]?.ToString());

            return new AffiliateCandidate
            {
                SourceKeyword = keyword ?? string.Empty,
                SourcePlatform = AffiliateSourceIds.TikTok,
                ProductName = productName,
                Price = ReadProductPrice(item),
                ImageUrl = coverUrl ?? string.Empty,
                CommissionRate = FormatCommissionRate(item),
                Creator = categoryLabel ?? string.Empty,
                VideoUrl = productLink,
                ProfileUrl = string.Empty,
                Hashtags = string.Empty,
                LinkedProduct = productName,
                PlayCount = postCount,
                LikeCount = likeCount,
                CommentCount = commentCount,
                ShareCount = shareCount,
                MetricsCapturedAtUtc = postCount > 0 ? DateTime.UtcNow : DateTime.MinValue
            };
        }

        private static string BuildTrendingProductLink(string productId, string urlTitle, string keyword)
        {
            if (!string.IsNullOrWhiteSpace(productId) && long.TryParse(productId.Trim(), out _))
            {
                return "https://www.tiktok.com/view/product/" + productId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(urlTitle))
            {
                var searchTerm = urlTitle.Replace('-', ' ').Trim();
                return "https://www.tiktok.com/shop/s/" + Uri.EscapeDataString(searchTerm);
            }

            var kw = (keyword ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(kw)
                ? string.Empty
                : "https://www.tiktok.com/shop/s/" + Uri.EscapeDataString(kw);
        }

        private static string HumanizeUrlTitle(string urlTitle)
        {
            var text = (urlTitle ?? string.Empty).Trim().Replace('-', ' ').Replace('_', ' ');
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            return string.IsNullOrWhiteSpace(text) ? "TikTok Shop product" : text;
        }

        private static string FormatPercentMetric(JToken token)
        {
            if (token == null)
            {
                return "N/A";
            }

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                return token.Value<double>().ToString("0.##", CultureInfo.InvariantCulture) + "% CVR";
            }

            var raw = token.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(raw) ? "N/A" : raw + "% CVR";
        }

        private static string FormatCommissionRate(JObject item)
        {
            if (item == null)
            {
                return "N/A";
            }

            var explicitRate = FirstNonEmpty(
                item["commission_rate"]?.ToString(),
                item["commissionRate"]?.ToString());
            if (!string.IsNullOrWhiteSpace(explicitRate))
            {
                var trimmed = explicitRate.Trim();
                return trimmed.IndexOf('%') >= 0
                    ? trimmed
                    : trimmed + "%";
            }

            return FormatPercentMetric(item["cvr"]);
        }

        internal static double ReadCommissionPercentFromItem(JObject item)
        {
            if (item == null)
            {
                return 0;
            }

            foreach (var token in new[] { item["commission_rate"], item["commissionRate"], item["cvr"] })
            {
                var pct = ParsePercentToken(token);
                if (pct > 0)
                {
                    return pct;
                }
            }

            return 0;
        }

        internal static double ParseCommissionPercent(string commissionText)
        {
            if (string.IsNullOrWhiteSpace(commissionText))
            {
                return 0;
            }

            var text = commissionText.Trim();
            var idx = text.IndexOf('%');
            if (idx >= 0)
            {
                text = text.Substring(0, idx).Trim();
            }
            else
            {
                var spaceIdx = text.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    text = text.Substring(0, spaceIdx).Trim();
                }
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                ? value
                : 0;
        }

        private static double ParsePercentToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return 0;
            }

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                return token.Value<double>();
            }

            return ParseCommissionPercent(token.ToString());
        }

        private static long ReadProductSalesVolume(JObject item)
        {
            if (item == null)
            {
                return 0;
            }

            foreach (var key in new[] { "sales", "volume", "sold", "post" })
            {
                var value = ReadStatLong(item, key);
                if (value > 0)
                {
                    return value;
                }
            }

            return 0;
        }

        private static string ReadProductPrice(JObject item)
        {
            if (item == null)
            {
                return "N/A";
            }

            var price = FirstNonEmpty(
                item["price"]?.ToString(),
                item["min_price"]?.ToString(),
                item["max_price"]?.ToString(),
                item["avg_price"]?.ToString(),
                item["display_price"]?.ToString());
            return string.IsNullOrWhiteSpace(price) ? "N/A" : price.Trim();
        }

        private static bool ReadTopProductsHasMore(JObject root, int itemsOnPage, int collected, int collectLimit)
        {
            var dataObj = AsJObject(root?["data"]);
            var pagination = dataObj?["pagination"] as JObject ?? root?["pagination"] as JObject;
            if (pagination != null)
            {
                var hasMore = pagination["has_more"] ?? pagination["hasMore"];
                if (hasMore != null)
                {
                    if (hasMore.Type == JTokenType.Boolean)
                    {
                        return hasMore.Value<bool>();
                    }

                    if (string.Equals(hasMore.ToString(), "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(hasMore.ToString(), "1", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (string.Equals(hasMore.ToString(), "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(hasMore.ToString(), "0", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return itemsOnPage >= 8 && collected < collectLimit;
        }

        private static void SortTopProductsDescending(List<AffiliateCandidate> rows)
        {
            if (rows == null || rows.Count <= 1)
            {
                return;
            }

            rows.Sort((a, b) =>
            {
                var pa = a?.PlayCount ?? 0L;
                var pb = b?.PlayCount ?? 0L;
                if (pb != pa)
                {
                    return pb.CompareTo(pa);
                }

                var la = a?.LikeCount ?? 0L;
                var lb = b?.LikeCount ?? 0L;
                return lb.CompareTo(la);
            });
        }

        private static string NormalizeCountryCode(string countryCode)
        {
            var code = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(code) ? "VN" : code;
        }

        internal static AffiliateCandidate MapItemToCandidate(JObject item, string keyword)
        {
            if (item == null)
            {
                return null;
            }

            var videoId = (item["id"] ?? item["aweme_id"])?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            var author = item["author"] as JObject;
            var uniqueId = (author?["uniqueId"] ?? author?["unique_id"])?.ToString()?.Trim().TrimStart('@');
            var nickname = (author?["nickname"] ?? author?["nickName"])?.ToString()?.Trim();

            var videoUrl = !string.IsNullOrWhiteSpace(uniqueId)
                ? $"https://www.tiktok.com/@{uniqueId}/video/{videoId}"
                : $"https://www.tiktok.com/video/{videoId}";

            var desc = (item["desc"] ?? item["title"])?.ToString()?.Trim();
            var productName = string.IsNullOrWhiteSpace(desc)
                ? $"TikTok {videoId}"
                : (desc.Length > 120 ? desc.Substring(0, 117) + "…" : desc);

            var stats = item["statsV2"] as JObject ?? item["stats"] as JObject;
            var playCount = ReadStatLong(stats, "playCount");
            var likeCount = ReadStatLong(stats, "diggCount");
            var commentCount = ReadStatLong(stats, "commentCount");
            var shareCount = ReadStatLong(stats, "shareCount");
            var collectCount = ReadStatLong(stats, "collectCount");

            var video = item["video"] as JObject;
            var durationSec = ReadStatInt(video, "duration");
            if (durationSec <= 0)
            {
                durationSec = ReadStatInt(item, "duration");
            }

            var imageUrl = FirstNonEmpty(
                video?["cover"]?.ToString(),
                video?["originCover"]?.ToString(),
                video?["dynamicCover"]?.ToString(),
                item["video"]?["cover"]?.ToString());

            var createUnix = ReadStatLong(item, "createTime");
            DateTime createUtc = DateTime.MinValue;
            if (createUnix > 0)
            {
                createUtc = DateTimeOffset.FromUnixTimeSeconds(createUnix).UtcDateTime;
            }

            var hashtags = ExtractHashtags(item);
            var creatorDisplay = !string.IsNullOrWhiteSpace(nickname)
                ? nickname
                : (string.IsNullOrWhiteSpace(uniqueId) ? string.Empty : "@" + uniqueId);
            var profileUrl = string.IsNullOrWhiteSpace(uniqueId)
                ? string.Empty
                : "https://www.tiktok.com/@" + uniqueId;

            return new AffiliateCandidate
            {
                SourceKeyword = keyword ?? string.Empty,
                SourcePlatform = AffiliateSourceIds.TikTok,
                ProductName = productName,
                Price = "N/A",
                ImageUrl = imageUrl ?? string.Empty,
                CommissionRate = "N/A",
                Creator = creatorDisplay,
                VideoUrl = videoUrl,
                ProfileUrl = profileUrl,
                Hashtags = hashtags,
                LinkedProduct = "Chưa rõ",
                PlayCount = playCount,
                LikeCount = likeCount,
                CommentCount = commentCount,
                ShareCount = shareCount,
                CollectCount = collectCount,
                DurationSeconds = durationSec,
                CreateTimeUtc = createUtc,
                MetricsCapturedAtUtc = playCount > 0 ? DateTime.UtcNow : DateTime.MinValue
            };
        }

        private static string ExtractHashtags(JObject item)
        {
            var tags = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void addTag(string raw)
            {
                var t = (raw ?? string.Empty).Trim().TrimStart('#');
                if (string.IsNullOrWhiteSpace(t) || !seen.Add(t))
                {
                    return;
                }

                tags.Add("#" + t);
            }

            foreach (var ch in item["challenges"] as JArray ?? new JArray())
            {
                addTag(ch?["title"]?.ToString());
            }

            foreach (var extra in item["textExtra"] as JArray ?? new JArray())
            {
                if (string.Equals(extra?["type"]?.ToString(), "1", StringComparison.OrdinalIgnoreCase)
                    || extra?["hashtagName"] != null)
                {
                    addTag(extra?["hashtagName"]?.ToString());
                }
            }

            return tags.Count == 0 ? string.Empty : string.Join(" ", tags.Take(12));
        }

        private static bool ReadHasMore(JObject root, int itemsOnPage, int collected, int collectLimit)
        {
            if (root == null)
            {
                return false;
            }

            foreach (var key in new[] { "hasMore", "has_more", "HasMore" })
            {
                var hm = root[key];
                if (hm == null)
                {
                    continue;
                }

                if (hm.Type == JTokenType.Boolean)
                {
                    return hm.Value<bool>();
                }

                if (hm.Type == JTokenType.Integer)
                {
                    return hm.Value<int>() != 0;
                }

                if (string.Equals(hm.ToString(), "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(hm.ToString(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(hm.ToString(), "0", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(hm.ToString(), "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // API đôi khi không trả hasMore — nếu trang đủ dày và chưa đủ collectLimit thì thử trang tiếp.
            return itemsOnPage >= 8 && collected < collectLimit;
        }

        private static int ReadCursor(JObject root)
        {
            var tok = root?["cursor"];
            if (tok == null)
            {
                return 0;
            }

            if (tok.Type == JTokenType.Integer)
            {
                return tok.Value<int>();
            }

            return int.TryParse(tok.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        private static string ReadSearchId(JObject root, string previous)
        {
            var logPb = AsJObject(root?["log_pb"]);
            var impr = logPb?["impr_id"]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(impr))
            {
                return impr;
            }

            var sid = root?["search_id"]?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(sid) ? (previous ?? "0") : sid;
        }

        private static JObject AsJObject(JToken token) => token as JObject;

        private static JArray ReadTopProductsList(JObject root)
        {
            if (root == null)
            {
                return null;
            }

            var dataObj = AsJObject(root["data"]);
            var nested = dataObj?["list"] as JArray;
            if (nested != null)
            {
                return nested;
            }

            return root["list"] as JArray;
        }

        private static string ReadEcomCategoryLabel(JObject item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            foreach (var key in new[] { "third_ecom_category", "second_ecom_category", "first_ecom_category" })
            {
                var cat = AsJObject(item[key]);
                var value = cat?["value"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static long ReadStatLong(JObject obj, string key)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key))
            {
                return 0L;
            }

            var tok = obj[key];
            if (tok == null)
            {
                return 0L;
            }

            if (tok.Type == JTokenType.Integer)
            {
                return tok.Value<long>();
            }

            if (tok.Type == JTokenType.Float)
            {
                return (long)tok.Value<double>();
            }

            return long.TryParse(tok.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0L;
        }

        private static int ReadStatInt(JObject obj, string key) =>
            (int)Math.Min(int.MaxValue, ReadStatLong(obj, key));

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v.Trim();
                }
            }

            return string.Empty;
        }

        private static string NormalizeHost(string host)
        {
            var h = (host ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(h))
            {
                return DefaultRapidApiHost;
            }

            if (h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                h = h.Substring(8);
            }
            else if (h.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                h = h.Substring(7);
            }

            var slash = h.IndexOf('/');
            if (slash >= 0)
            {
                h = h.Substring(0, slash);
            }

            return h;
        }

        private static void SortByTrendScoreDescending(List<AffiliateCandidate> rows)
        {
            if (rows == null || rows.Count <= 1)
            {
                return;
            }

            rows.Sort(CompareTrendScore);
        }

        private static int CompareTrendScore(AffiliateCandidate a, AffiliateCandidate b)
        {
            var va = a?.PlayCount ?? 0L;
            var vb = b?.PlayCount ?? 0L;
            if (vb != va)
            {
                return vb.CompareTo(va);
            }

            var ta = a?.CreateTimeUtc ?? DateTime.MinValue;
            var tb = b?.CreateTimeUtc ?? DateTime.MinValue;
            if (tb != ta)
            {
                return tb.CompareTo(ta);
            }

            var la = a?.LikeCount ?? 0L;
            var lb = b?.LikeCount ?? 0L;
            return lb.CompareTo(la);
        }

        private static void SortByViewsDescending(List<AffiliateCandidate> rows) =>
            SortByTrendScoreDescending(rows);

        private static string FormatViews(long views)
        {
            if (views >= 1_000_000)
            {
                return (views / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            }

            if (views >= 1_000)
            {
                return (views / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            }

            return views.ToString(CultureInfo.InvariantCulture);
        }

        private static string Truncate(string text, int max)
        {
            var s = (text ?? string.Empty).Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private List<AffiliateCandidate> ParseProductJsonResponse(string json)
        {
            var list = new List<AffiliateCandidate>();
            var root = JObject.Parse(json);
            var dataObj = AsJObject(root["data"]);
            var products = dataObj?["products"] as JArray;
            if (products == null)
            {
                return list;
            }

            foreach (var item in products)
            {
                list.Add(new AffiliateCandidate
                {
                    ProductName = item["title"]?.ToString(),
                    Price = item["price"]?.ToString(),
                    CommissionRate = item["commission_rate"]?.ToString() + "%",
                    VideoUrl = item["product_url"]?.ToString()
                });
            }

            return list;
        }
    }
}
