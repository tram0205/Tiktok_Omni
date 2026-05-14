using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    public class AffiliateCandidate
    {
        /// <summary>Từ khoá lần quét tạo ra dòng này (có thể gộp nhiều khoá nếu trùng VideoUrl).</summary>
        public string SourceKeyword { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string CommissionRate { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public string Hashtags { get; set; } = string.Empty;
        public string LinkedProduct { get; set; } = "Chưa rõ";
        public int SafetyScore { get; set; }
        public string SafetyRiskSummary { get; set; } = string.Empty;
        public string VideoScript { get; set; } = string.Empty;
        public string VoiceoverTranscript { get; set; } = string.Empty;
        /// <summary>Ghi nhận lỗi gần nhất khi Deep Dive hàng loạt (để user xem trên lưới / CSV).</summary>
        public string LastDeepDiveError { get; set; } = string.Empty;

        // ===== Engagement metrics (lấy từ TikWM API qua nút "Lấy số liệu") =====
        /// <summary>Tổng view của video (TikWM data.play_count).</summary>
        public long PlayCount { get; set; }
        /// <summary>Tổng like (TikWM data.digg_count).</summary>
        public long LikeCount { get; set; }
        /// <summary>Tổng comment (TikWM data.comment_count).</summary>
        public long CommentCount { get; set; }
        /// <summary>Tổng share (TikWM data.share_count).</summary>
        public long ShareCount { get; set; }
        /// <summary>Tổng lưu video (TikWM data.collect_count).</summary>
        public long CollectCount { get; set; }
        /// <summary>Độ dài video (giây). 0 nghĩa là chưa lấy.</summary>
        public int DurationSeconds { get; set; }
        /// <summary>Thời điểm video được đăng (UTC). MinValue nghĩa là chưa lấy.</summary>
        public DateTime CreateTimeUtc { get; set; } = DateTime.MinValue;
        /// <summary>Thời điểm app fetch metrics gần nhất (UTC). MinValue nghĩa là chưa fetch lần nào.</summary>
        public DateTime MetricsCapturedAtUtc { get; set; } = DateTime.MinValue;
        /// <summary>Lý do lần fetch metrics gần nhất thất bại (nếu có).</summary>
        public string LastMetricsError { get; set; } = string.Empty;
    }

    /// <summary>Kết quả enrich engagement metrics cho 1 video.</summary>
    public class VideoMetrics
    {
        public long PlayCount { get; set; }
        public long LikeCount { get; set; }
        public long CommentCount { get; set; }
        public long ShareCount { get; set; }
        public long CollectCount { get; set; }
        public int DurationSeconds { get; set; }
        public DateTime CreateTimeUtc { get; set; } = DateTime.MinValue;
    }

    public class VideoDeepAnalysisResult
    {
        public string VoiceoverTranscript { get; set; } = string.Empty;
        public string VideoScript { get; set; } = string.Empty;
        public string MoneyShotSummary { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public string CompressedVideoSizeInfo { get; set; } = string.Empty;
        // Sản phẩm gắn anchor được trích xuất từ TikWM (nếu có).
        public string LinkedProduct { get; set; } = string.Empty;
    }

    public enum AffiliateSearchMode
    {
        Shop = 0,
        Video = 1
    }

    public class AffiliateHunter
    {
        public Task<List<AffiliateCandidate>> HuntAsync(
            string keywords,
            int maxResults,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            return HuntAsync(keywords, maxResults, AffiliateSearchMode.Shop, cancellationToken, logAction, null, null);
        }

        public Task<List<AffiliateCandidate>> HuntAsync(
            string keywords,
            int maxResults,
            AffiliateSearchMode searchMode,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager = null,
            string runningProfileName = null)
        {
            if (string.IsNullOrWhiteSpace(keywords))
            {
                throw new ArgumentException("Keywords are required.", nameof(keywords));
            }

            if (maxResults <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxResults));
            }

            // Đẩy toàn bộ pipeline (launch + navigate + chờ + scrape JSON) sang threadpool
            // để vòng lặp message của WinForms không bị "Not Responding".
            return Task.Run(
                () => HuntCoreAsync(
                    keywords, maxResults, searchMode,
                    cancellationToken, logAction, configManager, runningProfileName),
                cancellationToken);
        }

        private async Task<List<AffiliateCandidate>> HuntCoreAsync(
            string keywords,
            int maxResults,
            AffiliateSearchMode searchMode,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager,
            string runningProfileName)
        {
            var browser = new BrowserAutomation();
            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();
            var random = new Random();

            try
            {
                AutomationProfile automationProfile = null;
                var effectiveProfileName = "default";
                if (configManager != null)
                {
                    var settings = await configManager.LoadAsync().ConfigureAwait(false);
                    automationProfile = ResolveRunningProfile(settings, runningProfileName, logAction);
                    automationProfile = await configManager
                        .EnsureProfileFingerprintAsync(settings, automationProfile?.Name, logAction)
                        .ConfigureAwait(false);
                    effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                        ? (automationProfile?.Name ?? "default")
                        : runningProfileName.Trim();
                    logAction?.Invoke($"[Affiliate] Mở trình duyệt với profile «{effectiveProfileName}» (cùng session đăng nhập TikTok Shop).");
                }
                else if (searchMode == AffiliateSearchMode.Shop)
                {
                    logAction?.Invoke("[Affiliate] Cảnh báo: chưa truyền ConfigManager/profile — đang dùng profile Playwright mặc định (thường chưa đăng nhập TikTok). Shop có thể không trả sản phẩm. Hãy chọn «Profile chạy» ở tab chính và build lại.");
                }

                // Chạy trình duyệt thật (KHÔNG headless, KHÔNG minimize) — TikTok phát hiện headless rất nhanh và sẽ chặn.
                // Cửa sổ hiện bình thường để user có thể tự tay giải CAPTCHA nếu TikTok yêu cầu.
                await browser.LaunchAsync(cancellationToken, logAction, effectiveProfileName, automationProfile, headless: false).ConfigureAwait(false);
                if (searchMode == AffiliateSearchMode.Shop)
                {
                    await browser.GotoShopSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);
                }
                else
                {
                    await browser.GotoVideoSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);
                }

                var page = browser.Page;
                if (page == null)
                {
                    throw new InvalidOperationException("Browser page is not available.");
                }

                var deadline = DateTime.UtcNow.AddMinutes(2);
                var lastCount = -1;
                var stagnantRounds = 0;

                if (searchMode == AffiliateSearchMode.Video)
                {
                    await WaitForVideoSearchAsync(browser, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await WaitForRehydrationAsync(browser, cancellationToken).ConfigureAwait(false);
                }

                if (searchMode == AffiliateSearchMode.Shop)
                {
                    await LogShopDiagnosticsAsync(browser, logAction).ConfigureAwait(false);
                }

                var emptyRounds = 0;
                while (results.Count < maxResults && DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var snapshots = searchMode == AffiliateSearchMode.Shop
                        ? await ExtractShopProductsAsync(browser).ConfigureAwait(false)
                        : await ExtractVideoSearchResultsAsync(browser).ConfigureAwait(false);

                    if (searchMode == AffiliateSearchMode.Video && snapshots.Count == 0)
                    {
                        emptyRounds++;
                        logAction?.Invoke($"[Affiliate] Vòng {emptyRounds}: chưa quét được sản phẩm/video nào. Cuộn thêm và đợi...");
                        if (emptyRounds >= 4)
                        {
                            logAction?.Invoke($"⚠️ Không quét được dữ liệu từ màn hình cho từ khóa «{keywords}». Có thể TikTok đang đổi giao diện hoặc chưa load xong.");
                            break;
                        }
                        await ScrollShopFeedAsync(browser, cancellationToken).ConfigureAwait(false);
                    }

                    if (searchMode == AffiliateSearchMode.Shop && snapshots.Count == 0)
                    {
                        emptyRounds++;
                        logAction?.Invoke($"[Shop] Vòng {emptyRounds}: chưa thấy sản phẩm nào. Cuộn thêm và đợi...");
                        await ScrollShopFeedAsync(browser, cancellationToken).ConfigureAwait(false);
                        if (emptyRounds >= 6)
                        {
                            logAction?.Invoke("[Shop] Vẫn không có sản phẩm. Kiểm tra: (1) «Profile chạy» đúng nick đã đăng nhập TikTok; (2) mở tiktok.com/shop một lần trong trình duyệt thủ công; (3) xem log [Shop diag] phía trên.");
                            break;
                        }
                    }
                    else if (searchMode == AffiliateSearchMode.Shop)
                    {
                        emptyRounds = 0;
                    }
                    for (var i = 0; i < snapshots.Count && results.Count < maxResults; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var snap = snapshots[i] ?? new CandidateSnapshot();

                        AffiliateCandidate candidate;
                        if (searchMode == AffiliateSearchMode.Shop)
                        {
                            var productUrl = ToAbsoluteTikTokUrl(snap.VideoUrl);
                            if (!IsTikTokProductUrl(productUrl) || !seen.Add(productUrl))
                            {
                                continue;
                            }

                            var sellerName = !string.IsNullOrWhiteSpace(snap.Creator) && !IsIdLike(snap.Creator)
                                ? snap.Creator.Trim()
                                : string.Empty;
                            var productName = !string.IsNullOrWhiteSpace(snap.ProductName) && !IsIdLike(snap.ProductName)
                                ? snap.ProductName.Trim()
                                : (string.IsNullOrWhiteSpace(sellerName) ? "TikTok Shop product" : "TikTok Shop — " + sellerName);

                            candidate = new AffiliateCandidate
                            {
                                SourceKeyword = keywords,
                                ProductName = productName,
                                Price = NormalizePrice(snap.PriceText),
                                ImageUrl = NormalizeImageUrl(snap.ImageUrl),
                                CommissionRate = NormalizeCommissionRate(snap.CommissionText),
                                Creator = sellerName,
                                VideoUrl = productUrl,
                                ProfileUrl = string.Empty,
                                Hashtags = string.Empty,
                                LinkedProduct = !string.IsNullOrWhiteSpace(productName) && !IsIdLike(productName)
                                    ? productName.Trim()
                                    : "Chưa rõ"
                            };
                        }
                        else
                        {
                            var videoUrl = ToAbsoluteTikTokUrl(snap.VideoUrl);
                            if (!IsTikTokVideoUrl(videoUrl) || !seen.Add(videoUrl))
                            {
                                continue;
                            }

                            var creatorHandle = (!string.IsNullOrWhiteSpace(snap.AuthorUniqueId)
                                ? snap.AuthorUniqueId
                                : ExtractCreatorFromUrl(videoUrl)).Trim().TrimStart('@');
                            var creatorDisplay = !string.IsNullOrWhiteSpace(snap.Creator) && !IsIdLike(snap.Creator)
                                ? snap.Creator.Trim()
                                : (string.IsNullOrWhiteSpace(creatorHandle) ? string.Empty : "@" + creatorHandle);
                            var productName = ResolveProductName(snap.ProductName, creatorDisplay);
                            var profileUrl = string.IsNullOrWhiteSpace(creatorHandle)
                                ? string.Empty
                                : "https://www.tiktok.com/@" + creatorHandle;

                            var hashtags = (snap.Hashtags ?? string.Empty).Trim();
                            var linkedProduct = string.IsNullOrWhiteSpace(snap.LinkedProduct)
                                ? "Chưa rõ"
                                : snap.LinkedProduct.Trim();

                            candidate = new AffiliateCandidate
                            {
                                SourceKeyword = keywords,
                                ProductName = productName,
                                Price = NormalizePrice(snap.PriceText),
                                ImageUrl = NormalizeImageUrl(snap.ImageUrl),
                                CommissionRate = NormalizeCommissionRate(snap.CommissionText),
                                Creator = creatorDisplay,
                                VideoUrl = videoUrl,
                                ProfileUrl = profileUrl,
                                Hashtags = hashtags,
                                LinkedProduct = linkedProduct
                            };
                        }

                        var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                        candidate.SafetyScore = safetyResult.Score;
                        candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                            ? "Low risk"
                            : string.Join("; ", safetyResult.Reasons.Take(3));
                        results.Add(candidate);

                        logAction?.Invoke($"[Affiliate] {results.Count}/{maxResults}: {candidate.ProductName} | {candidate.Price} | {candidate.CommissionRate} | safety {candidate.SafetyScore}");
                    }

                    if (results.Count >= maxResults)
                    {
                        break;
                    }

                    var count = snapshots.Count;
                    if (count == lastCount)
                    {
                        stagnantRounds++;
                        if (stagnantRounds >= 3)
                        {
                            logAction?.Invoke("[Affiliate] No more results loading. Stopping early.");
                            break;
                        }
                    }
                    else
                    {
                        stagnantRounds = 0;
                        lastCount = count;
                    }

                    await PerformStealthInteractionAsync(browser, cancellationToken, random).ConfigureAwait(false);
                }

                logAction?.Invoke($"[Affiliate] Deep extraction finished: {results.Count} item(s). Safety pre-check applied.");
                return results;
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }

        // ===== Deep video analysis (download → compress → Gemini) =====
        public Task<VideoDeepAnalysisResult> AnalyzeVideoContentAsync(
            string videoUrl,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("Video URL is required.", nameof(videoUrl));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // Đẩy ra threadpool để WinForms không bị đơ.
            return Task.Run(
                () => AnalyzeVideoContentCoreAsync(videoUrl, settings, logAction, cancellationToken),
                cancellationToken);
        }

        // ===== Download MP4 (TikWM, no compression) → trả về full path file đã tải =====
        public Task<string> DownloadAffiliateVideoAsync(
            string videoUrl,
            string saveDir,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) throw new ArgumentException("Video URL is required.", nameof(videoUrl));
            if (string.IsNullOrWhiteSpace(saveDir)) throw new ArgumentException("Save directory is required.", nameof(saveDir));
            return Task.Run(
                () => DownloadAffiliateVideoCoreAsync(videoUrl, saveDir, logAction, cancellationToken),
                cancellationToken);
        }

        // ===== Get direct MP4 URL (no download) — dùng để mở trực tiếp trong trình duyệt =====
        public Task<string> GetDirectMp4UrlAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) throw new ArgumentException("Video URL is required.", nameof(videoUrl));
            return Task.Run(async () =>
            {
                var resolved = await ResolveTikWmMp4UrlAsync(videoUrl, logAction, cancellationToken).ConfigureAwait(false);
                return resolved.PlayUrl;
            }, cancellationToken);
        }

        // ===== Quét anchor sản phẩm (Mobile UA + JSON page state) =====
        // TikTok desktop web ẨN anchor commerce khỏi card search. Bản mobile web (m.tiktok.com)
        // và trang chi tiết video lại nhúng JSON __UNIVERSAL_DATA_FOR_REHYDRATION__ với
        // ItemModule[id].anchors[] đầy đủ. Method này giả lập iPhone, fetch HTML, parse JSON, lấy anchor.
        public Task<string> EnrichLinkedProductAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl)) throw new ArgumentException("Video URL is required.", nameof(videoUrl));
            return Task.Run(
                () => EnrichLinkedProductCoreAsync(videoUrl, logAction, cancellationToken),
                cancellationToken);
        }

        private static async Task<string> EnrichLinkedProductCoreAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            string cleanUrl = (videoUrl ?? string.Empty).Trim();
            int qi = cleanUrl.IndexOf('?');
            if (qi >= 0) cleanUrl = cleanUrl.Substring(0, qi);
            if (string.IsNullOrWhiteSpace(cleanUrl)) return string.Empty;

            // 1) Thử TikWM trước — nhanh & ít bị block.
            try
            {
                var tikwm = await ResolveTikWmMp4UrlAsync(cleanUrl, logAction, cancellationToken).ConfigureAwait(false);
                var fromTikwm = ExtractLinkedProductFromTikWm(tikwm.Data);
                if (!string.IsNullOrWhiteSpace(fromTikwm))
                {
                    logAction?.Invoke($"[Anchor] TikWM → {fromTikwm}");
                    return fromTikwm;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Anchor] TikWM fail (sẽ fallback mobile UA): " + ex.Message);
            }

            // 2) Fetch trang chi tiết video với MOBILE User-Agent → parse JSON state.
            string html = await FetchPageWithUserAgentAsync(
                cleanUrl,
                "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1",
                cancellationToken).ConfigureAwait(false);

            string fromMobile = ExtractAnchorFromPageHtml(html);
            if (!string.IsNullOrWhiteSpace(fromMobile))
            {
                logAction?.Invoke($"[Anchor] Mobile UA → {fromMobile}");
                return fromMobile;
            }

            // 3) Fallback desktop UA — đôi khi desktop trả nhiều dữ liệu hơn ở Module khác.
            html = await FetchPageWithUserAgentAsync(
                cleanUrl,
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                cancellationToken).ConfigureAwait(false);
            string fromDesktop = ExtractAnchorFromPageHtml(html);
            if (!string.IsNullOrWhiteSpace(fromDesktop))
            {
                logAction?.Invoke($"[Anchor] Desktop UA → {fromDesktop}");
                return fromDesktop;
            }

            logAction?.Invoke("[Anchor] Không tìm thấy anchor sản phẩm cho video này.");
            return string.Empty;
        }

        private static async Task<string> FetchPageWithUserAgentAsync(
            string url,
            string userAgent,
            CancellationToken cancellationToken)
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = true })
            using (var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi-VN,vi;q=0.9,en;q=0.8");
                http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                http.DefaultRequestHeaders.Referrer = new Uri("https://www.tiktok.com/");
                using (var resp = await http.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        // Parse anchor info từ HTML trang chi tiết video. Quét cả 2 script JSON:
        //   <script id="__UNIVERSAL_DATA_FOR_REHYDRATION__">  (TikTok web mới)
        //   <script id="SIGI_STATE">                          (TikTok web cũ)
        private static string ExtractAnchorFromPageHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // Thử 2 schema. Regex non-greedy, single-line.
            foreach (var pattern in new[]
            {
                @"<script[^>]*id=""__UNIVERSAL_DATA_FOR_REHYDRATION__""[^>]*>(?<j>.*?)</script>",
                @"<script[^>]*id=""SIGI_STATE""[^>]*>(?<j>.*?)</script>"
            })
            {
                var m = Regex.Match(html, pattern, RegexOptions.Singleline);
                if (!m.Success) continue;
                var raw = m.Groups["j"].Value;
                try
                {
                    var root = JObject.Parse(raw);
                    var found = WalkJsonForAnchorName(root, depth: 0);
                    if (!string.IsNullOrWhiteSpace(found)) return found;
                }
                catch { /* JSON xấu, thử pattern khác */ }
            }
            return string.Empty;
        }

        // Đệ quy duyệt JSON tìm anchor commerce (TikTok lồng rất sâu, schema đổi liên tục).
        // Bắt mọi object có key "anchors" là array → đọc keyword/title/name; HOẶC object trông giống anchor
        // (có "extra" string JSON, hoặc các field product_*). Trả về string đầu tiên tìm được.
        private static string WalkJsonForAnchorName(JToken token, int depth)
        {
            if (token == null || depth > 12) return string.Empty;

            if (token is JObject obj)
            {
                // Trực tiếp: object có field "anchors"
                if (obj["anchors"] is JArray anchorsArr && anchorsArr.Count > 0)
                {
                    foreach (var a in anchorsArr)
                    {
                        if (!(a is JObject ao)) continue;
                        foreach (var key in new[] { "keyword", "title", "name", "description", "anchor_title" })
                        {
                            var v = ao[key]?.ToString();
                            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                        }
                        var extraTok = ao["extra"];
                        if (extraTok != null)
                        {
                            if (extraTok.Type == JTokenType.String)
                            {
                                try
                                {
                                    var inner = JObject.Parse(extraTok.ToString());
                                    var found = WalkJsonForAnchorName(inner, depth + 1);
                                    if (!string.IsNullOrWhiteSpace(found)) return found;
                                }
                                catch { /* bỏ qua */ }
                            }
                            else
                            {
                                var found = WalkJsonForAnchorName(extraTok, depth + 1);
                                if (!string.IsNullOrWhiteSpace(found)) return found;
                            }
                        }
                    }
                }

                // Object có vẻ là product_info / commerce_info
                foreach (var key in new[] { "product_name", "product_title", "title", "name", "keyword" })
                {
                    if (obj[key]?.Type == JTokenType.String)
                    {
                        var v = obj[key].ToString();
                        // Heuristic: parent có context commerce (có key product_id / sku_id / shop_id / product_seo)
                        if (obj["product_id"] != null
                            || obj["sku_id"] != null
                            || obj["shop_id"] != null
                            || obj["product_seo"] != null
                            || obj["product_url"] != null)
                        {
                            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                        }
                    }
                }

                // Đệ quy con
                foreach (var prop in obj.Properties())
                {
                    var found = WalkJsonForAnchorName(prop.Value, depth + 1);
                    if (!string.IsNullOrWhiteSpace(found)) return found;
                }
            }
            else if (token is JArray arr)
            {
                foreach (var t in arr)
                {
                    var found = WalkJsonForAnchorName(t, depth + 1);
                    if (!string.IsNullOrWhiteSpace(found)) return found;
                }
            }
            return string.Empty;
        }

        // ===== Helper chung: fetch + parse TikWM `data` JObject. KHÔNG throw cho Slideshow =====
        // Slideshow vẫn có view/like/comment, nên metrics enrichment cần đọc được.
        // Caller nào cần play URL thì gọi `ResolveTikWmMp4UrlAsync` (sẽ throw nếu là slideshow).
        private static async Task<JObject> FetchTikWmDataAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            string cleanUrl = (videoUrl ?? string.Empty).Trim();
            int qi = cleanUrl.IndexOf('?');
            if (qi >= 0) cleanUrl = cleanUrl.Substring(0, qi);

            logAction?.Invoke($"[TikWM] Fetch: {cleanUrl}");

            string apiEndpoint = "https://www.tikwm.com/api/?url=" + Uri.EscapeDataString(cleanUrl);
            string apiBody;
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                using (var resp = await http.GetAsync(apiEndpoint, cancellationToken).ConfigureAwait(false))
                {
                    apiBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        throw new Exception($"TikWM API trả về HTTP {(int)resp.StatusCode}. Body: {apiBody}");
                }
            }

            JObject root;
            try { root = JObject.Parse(apiBody); }
            catch (Exception ex) { throw new Exception("TikWM trả JSON không hợp lệ: " + ex.Message); }

            var code = root["code"]?.Value<int?>() ?? -1;
            if (code != 0)
            {
                var msg = root["msg"]?.ToString() ?? "(no msg)";
                throw new Exception($"TikWM API báo lỗi (code={code}): {msg}");
            }

            var data = root["data"] as JObject ?? throw new Exception("TikWM API không trả về data.");
            return data;
        }

        private static async Task<TikWmResolveResult> ResolveTikWmMp4UrlAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            string cleanUrl = (videoUrl ?? string.Empty).Trim();
            int qi = cleanUrl.IndexOf('?');
            if (qi >= 0) cleanUrl = cleanUrl.Substring(0, qi);

            var data = await FetchTikWmDataAsync(cleanUrl, logAction, cancellationToken).ConfigureAwait(false);

            var images = data["images"] as JArray;
            if (images != null && images.Count > 0)
                throw new Exception("Video này là định dạng Ảnh trượt (Slideshow). Không thể phát MP4. Vui lòng chọn video khác!");

            var play = data["play"]?.ToString();
            if (string.IsNullOrWhiteSpace(play)) play = data["hdplay"]?.ToString();
            if (string.IsNullOrWhiteSpace(play))
                throw new Exception("TikWM API không thể bóc tách được video này.");
            if (play.StartsWith("/", StringComparison.Ordinal))
                play = "https://www.tikwm.com" + play;

            string videoId = data["id"]?.ToString();
            string author = data["author"]?["unique_id"]?.ToString() ?? data["author"]?["nickname"]?.ToString();
            if (string.IsNullOrWhiteSpace(videoId)) videoId = Guid.NewGuid().ToString("N").Substring(0, 10);

            return new TikWmResolveResult
            {
                PlayUrl = play,
                VideoId = videoId,
                Author = author ?? string.Empty,
                CleanUrl = cleanUrl,
                Data = data
            };
        }

        // ===== Enrich engagement metrics cho 1 video (views/likes/comments/shares/duration/posted) =====
        // Dùng TikWM API. Hoạt động cả với video MP4 lẫn slideshow.
        public Task<VideoMetrics> EnrichVideoMetricsAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("Video URL is required.", nameof(videoUrl));
            }

            return Task.Run(async () =>
            {
                var data = await FetchTikWmDataAsync(videoUrl, logAction, cancellationToken).ConfigureAwait(false);
                return ParseVideoMetricsFromTikWmData(data);
            }, cancellationToken);
        }

        private static VideoMetrics ParseVideoMetricsFromTikWmData(JObject data)
        {
            if (data == null) return new VideoMetrics();

            long ReadLong(string key)
            {
                var tok = data[key];
                if (tok == null) return 0L;
                if (tok.Type == JTokenType.Integer) return tok.Value<long>();
                if (tok.Type == JTokenType.Float) return (long)tok.Value<double>();
                return long.TryParse(tok.ToString(), out var v) ? v : 0L;
            }

            int ReadInt(string key)
            {
                var tok = data[key];
                if (tok == null) return 0;
                if (tok.Type == JTokenType.Integer) return tok.Value<int>();
                if (tok.Type == JTokenType.Float) return (int)tok.Value<double>();
                return int.TryParse(tok.ToString(), out var v) ? v : 0;
            }

            var metrics = new VideoMetrics
            {
                PlayCount = ReadLong("play_count"),
                LikeCount = ReadLong("digg_count"),
                CommentCount = ReadLong("comment_count"),
                ShareCount = ReadLong("share_count"),
                CollectCount = ReadLong("collect_count"),
                DurationSeconds = ReadInt("duration")
            };

            var createTok = data["create_time"];
            if (createTok != null)
            {
                long ts = 0;
                if (createTok.Type == JTokenType.Integer) ts = createTok.Value<long>();
                else if (createTok.Type == JTokenType.Float) ts = (long)createTok.Value<double>();
                else long.TryParse(createTok.ToString(), out ts);

                if (ts > 0)
                {
                    // TikWM trả về UNIX seconds.
                    try { metrics.CreateTimeUtc = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime; }
                    catch { /* timestamp out of range — bỏ qua */ }
                }
            }

            return metrics;
        }

        private sealed class TikWmResolveResult
        {
            public string PlayUrl { get; set; }
            public string VideoId { get; set; }
            public string Author { get; set; }
            public string CleanUrl { get; set; }
            public JObject Data { get; set; }
        }

        private static async Task<string> DownloadAffiliateVideoCoreAsync(
            string videoUrl,
            string saveDir,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(saveDir);

            var resolved = await ResolveTikWmMp4UrlAsync(videoUrl, logAction, cancellationToken).ConfigureAwait(false);
            var play = resolved.PlayUrl;

            // Build tên file
            var safeAuthor = SanitizeForFileName(resolved.Author);
            string fileName = string.IsNullOrWhiteSpace(safeAuthor)
                ? $"tk_{resolved.VideoId}.mp4"
                : $"tk_{safeAuthor}_{resolved.VideoId}.mp4";
            string savePath = Path.Combine(saveDir, fileName);

            // Tải MP4 trực tiếp
            logAction?.Invoke($"[Download] Tải MP4 → {savePath}");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                http.DefaultRequestHeaders.Referrer = new Uri("https://www.tikwm.com/");
                using (var resp = await http.GetAsync(play, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                        throw new Exception($"Tải MP4 thất bại — HTTP {(int)resp.StatusCode} từ {play}.");
                    using (var inStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var outStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await inStream.CopyToAsync(outStream, 81920, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            var size = new FileInfo(savePath).Length;
            if (size < 10_000)
            {
                try { File.Delete(savePath); } catch { }
                throw new Exception($"File tải về quá nhỏ ({size} bytes) — URL không hợp lệ hoặc video bị chặn.");
            }
            logAction?.Invoke($"[Download] OK ({size / 1024d / 1024d:0.00} MB).");
            return savePath;
        }

        private static string SanitizeForFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }
            return sb.ToString().Trim().TrimEnd('.');
        }

        // Bóc tách thông tin "sản phẩm gắn anchor" từ JSON data của TikWM.
        // TikWM/TikTok không có schema cố định: anchor có thể nằm ở data.anchors[], data.commerce_info,
        // data.anchors_extras, hoặc đôi khi chỉ là field keyword/title/description. Parse defensive.
        private static string ExtractLinkedProductFromTikWm(JObject data)
        {
            if (data == null) return string.Empty;

            // 1) data.anchors[] — common
            var anchors = data["anchors"] as JArray;
            if (anchors != null)
            {
                foreach (var anchorTok in anchors)
                {
                    if (!(anchorTok is JObject anchor)) continue;
                    foreach (var key in new[] { "keyword", "title", "name", "description" })
                    {
                        var v = anchor[key]?.ToString();
                        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                    }
                    // anchor.extra có thể là chuỗi JSON-encoded
                    var extraTok = anchor["extra"];
                    if (extraTok != null)
                    {
                        if (extraTok.Type == JTokenType.String)
                        {
                            try
                            {
                                var inner = JObject.Parse(extraTok.ToString());
                                foreach (var key in new[] { "title", "name", "product_name", "product_title", "keyword" })
                                {
                                    var v = inner[key]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                                }
                            }
                            catch { /* extra không phải JSON, bỏ qua */ }
                        }
                        else if (extraTok is JObject extraObj)
                        {
                            foreach (var key in new[] { "title", "name", "product_name", "product_title", "keyword" })
                            {
                                var v = extraObj[key]?.ToString();
                                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                            }
                        }
                    }
                }
            }

            // 2) data.anchors_extras / data.anchor_info
            foreach (var fld in new[] { "anchors_extras", "anchor_info", "commerce_info" })
            {
                var node = data[fld];
                if (node == null) continue;
                if (node is JObject obj)
                {
                    foreach (var key in new[] { "title", "name", "product_name", "product_title", "keyword" })
                    {
                        var v = obj[key]?.ToString();
                        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                    }
                }
                else if (node is JArray arr)
                {
                    foreach (var t in arr)
                    {
                        if (!(t is JObject o)) continue;
                        foreach (var key in new[] { "title", "name", "product_name", "product_title", "keyword" })
                        {
                            var v = o[key]?.ToString();
                            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                        }
                    }
                }
            }

            return string.Empty;
        }

        private async Task<VideoDeepAnalysisResult> AnalyzeVideoContentCoreAsync(
            string videoUrl,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var ffmpegExe = string.IsNullOrWhiteSpace(settings.FfmpegPath)
                ? ResolveFfmpegExecutablePath(settings, logAction)
                : settings.FfmpegPath.Trim();
            if (!File.Exists(ffmpegExe))
                throw new Exception("Lỗi: Không tìm thấy file ffmpeg.exe tại " + ffmpegExe);

            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_downloads");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            // 1. LÀM SẠCH LINK — bỏ mọi thứ từ "?" trở đi.
            string cleanUrl = (videoUrl ?? string.Empty).Trim();
            int q = cleanUrl.IndexOf('?');
            if (q >= 0) cleanUrl = cleanUrl.Substring(0, q);

            logAction?.Invoke($"[DeepDive] Gọi TikWM API cho: {cleanUrl}");

            // 2. GỌI API TIKWM
            string apiEndpoint = "https://www.tikwm.com/api/?url=" + Uri.EscapeDataString(cleanUrl);
            string apiBody;
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                using (var resp = await http.GetAsync(apiEndpoint, cancellationToken).ConfigureAwait(false))
                {
                    apiBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new Exception($"TikWM API trả về HTTP {(int)resp.StatusCode}. Body: {apiBody}");
                    }
                }
            }

            // 3. ĐỌC JSON TRẢ VỀ (bắt lỗi Slideshow)
            JObject root;
            try { root = JObject.Parse(apiBody); }
            catch (Exception ex) { throw new Exception("TikWM trả về JSON không hợp lệ: " + ex.Message + "\n" + apiBody); }

            var apiCode = root["code"]?.Value<int?>() ?? -1;
            if (apiCode != 0)
            {
                var apiMsg = root["msg"]?.ToString() ?? "(no msg)";
                throw new Exception($"TikWM API báo lỗi (code={apiCode}): {apiMsg}");
            }

            var data = root["data"] as JObject;
            if (data == null)
                throw new Exception("TikWM API không trả về data.");

            // ===== Trích xuất sản phẩm gắn anchor từ TikWM (nếu có) =====
            // TikTok video có thể đính anchor commerce/shop trong data.anchors hoặc data.commerce_info.
            // Parse defensive vì schema TikWM không ổn định.
            string tikwmLinkedProduct = ExtractLinkedProductFromTikWm(data);
            if (!string.IsNullOrWhiteSpace(tikwmLinkedProduct))
            {
                logAction?.Invoke($"[DeepDive] Sản phẩm anchor: {tikwmLinkedProduct}");
            }

            // Slideshow → có mảng images
            var images = data["images"] as JArray;
            if (images != null && images.Count > 0)
            {
                throw new Exception("Video này là định dạng Ảnh trượt (Slideshow). AI không thể phân tích khung hình. Vui lòng chọn video khác!");
            }

            var play = data["play"]?.ToString();
            if (string.IsNullOrWhiteSpace(play))
            {
                // Một số response trả "hdplay" thay vì "play".
                play = data["hdplay"]?.ToString();
            }
            if (string.IsNullOrWhiteSpace(play))
            {
                throw new Exception("TikWM API không thể bóc tách được video này.");
            }

            // Chuẩn hoá URL nếu TikWM trả về dạng tương đối.
            if (play.StartsWith("/", StringComparison.Ordinal))
            {
                play = "https://www.tikwm.com" + play;
            }

            // 4. TẢI FILE MP4 GỐC TRỰC TIẾP
            string videoId = data["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(videoId)) videoId = Guid.NewGuid().ToString("N").Substring(0, 10);
            string rawVideoPath = Path.Combine(tempDir, $"dl_{DateTime.Now:yyyyMMddHHmmss}_{videoId}.mp4");

            logAction?.Invoke($"[DeepDive] Tải video MP4: {play}");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                http.DefaultRequestHeaders.Referrer = new Uri("https://www.tikwm.com/");
                using (var resp = await http.GetAsync(play, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new Exception($"Tải MP4 thất bại — HTTP {(int)resp.StatusCode} từ {play}.");
                    }
                    using (var inStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var outStream = new FileStream(rawVideoPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await inStream.CopyToAsync(outStream, 81920, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            var rawSize = new FileInfo(rawVideoPath).Length;
            if (rawSize < 10_000)
            {
                try { File.Delete(rawVideoPath); } catch { }
                throw new Exception($"File tải về quá nhỏ ({rawSize} bytes) — TikWM trả URL không hợp lệ hoặc video bị chặn.");
            }
            logAction?.Invoke($"[DeepDive] Tải thành công: {Path.GetFileName(rawVideoPath)} ({rawSize / 1024d / 1024d:0.00} MB). Đang nén...");

            // 5. NÉN BẰNG FFMPEG (giữ nguyên cmd cũ)
            string compressedMp4Path = Path.Combine(tempDir, $"compressed_{Guid.NewGuid().ToString("N").Substring(0, 6)}.mp4");
            string ffmpegArgs = $"-y -i \"{rawVideoPath}\" -c:v libx264 -preset fast -crf 28 -pix_fmt yuv420p -c:a aac -b:a 64k -movflags +faststart \"{compressedMp4Path}\"";

            var ffPsi = new ProcessStartInfo(ffmpegExe, ffmpegArgs)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            var ffErr = new StringBuilder();
            using (var p = new Process { StartInfo = ffPsi })
            {
                p.OutputDataReceived += (_, ev) => { /* drain */ };
                p.ErrorDataReceived += (_, ev) => { if (ev.Data != null) ffErr.AppendLine(ev.Data); };
                if (!p.Start()) throw new Exception("Không khởi chạy được ffmpeg.exe.");
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                if (p.ExitCode != 0) logAction?.Invoke("[DeepDive] FFmpeg stderr: " + ffErr);
            }

            if (!File.Exists(compressedMp4Path) || new FileInfo(compressedMp4Path).Length < 20_000)
            {
                // Giữ raw để debug.
                throw new Exception("Quá trình nén thất bại (file < 20KB). FFmpeg stderr: " + ffErr);
            }

            // Nén OK → xóa raw cho nhẹ máy.
            try { File.Delete(rawVideoPath); } catch { }

            var compressedSize = new FileInfo(compressedMp4Path).Length;
            logAction?.Invoke($"[DeepDive] Đã nén xong ({compressedSize / 1024d / 1024d:0.00} MB). Đang gửi cho Gemini phân tích...");

            // 6. GỬI LÊN GEMINI
            try
            {
                var geminiService = new GeminiService();
                var parsed = await geminiService.AnalyzeAffiliateVideoAsync(
                    compressedMp4Path,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    cancellationToken).ConfigureAwait(false);

                var result = new VideoDeepAnalysisResult
                {
                    VoiceoverTranscript = (parsed["voiceover"]?.ToString() ?? string.Empty).Trim(),
                    VideoScript = (parsed["script_summary"]?.ToString() ?? string.Empty).Trim(),
                    MoneyShotSummary = (parsed["money_shot"]?.ToString() ?? string.Empty).Trim(),
                    RawText = parsed["_raw"]?.ToString() ?? string.Empty,
                    CompressedVideoSizeInfo = $"{compressedSize / 1024d / 1024d:0.00} MB",
                    LinkedProduct = tikwmLinkedProduct ?? string.Empty
                };
                logAction?.Invoke("[DeepDive] Phân tích xong.");
                return result;
            }
            finally
            {
                try { File.Delete(compressedMp4Path); } catch { }
            }
        }

        private static string ResolveYtDlpExecutablePath(AppSettings settings, Action<string> logAction)
        {
            static void AddUnique(List<string> list, string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }
                var t = path.Trim();
                if (list.Exists(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
                list.Add(t);
            }

            var candidates = new List<string>();
            AddUnique(candidates, settings?.YtDlpPath);

            try
            {
                var main = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(main))
                {
                    var dir = Path.GetDirectoryName(main);
                    AddUnique(candidates, Path.Combine(dir ?? string.Empty, "yt-dlp.exe"));
                }
            }
            catch
            {
                // ignored
            }

            AddUnique(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "yt-dlp.exe"));

            try
            {
                var loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.GetDirectoryName(loc);
                    AddUnique(candidates, Path.Combine(dir ?? string.Empty, "yt-dlp.exe"));
                }
            }
            catch
            {
                // ignored
            }

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                {
                    logAction?.Invoke("[DeepDive] Dùng yt-dlp: " + c);
                    return c;
                }
            }

            string dirExe;
            try
            {
                dirExe = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            }
            catch
            {
                dirExe = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(dirExe))
            {
                dirExe = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            }

            var tried = string.Join("\r\n  - ", candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                "Chưa tìm thấy yt-dlp.exe.\r\n\r\n" +
                "Cách xử lý nhanh:\r\n" +
                "• Tab «Cài đặt» → nhóm Veo/Lyria/FFmpeg → bấm «⬇ Tải yt-dlp» (tải về cùng thư mục với exe),\r\n" +
                "  hoặc Browse chọn file yt-dlp.exe rồi bấm «Lưu cài đặt» (không bắt buộc nếu ô đã đúng).\r\n" +
                "• Hoặc đặt sẵn yt-dlp.exe vào thư mục chạy app:\r\n  " + dirExe + "\r\n\r\n" +
                "Đã thử các đường dẫn:\r\n  - " + tried);
        }

        private static string ResolveFfmpegExecutablePath(AppSettings settings, Action<string> logAction)
        {
            static void AddUnique(List<string> list, string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }
                var t = path.Trim();
                if (list.Exists(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
                list.Add(t);
            }

            var candidates = new List<string>();
            AddUnique(candidates, settings?.FfmpegPath);

            try
            {
                var main = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(main))
                {
                    var dir = Path.GetDirectoryName(main);
                    AddUnique(candidates, Path.Combine(dir ?? string.Empty, "ffmpeg.exe"));
                }
            }
            catch
            {
                // ignored
            }

            AddUnique(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "ffmpeg.exe"));

            try
            {
                var loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.GetDirectoryName(loc);
                    AddUnique(candidates, Path.Combine(dir ?? string.Empty, "ffmpeg.exe"));
                }
            }
            catch
            {
                // ignored
            }

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                {
                    logAction?.Invoke("[DeepDive] Dùng FFmpeg: " + c);
                    return c;
                }
            }

            string dirExe;
            try
            {
                dirExe = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            }
            catch
            {
                dirExe = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(dirExe))
            {
                dirExe = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            }

            var tried = string.Join("\r\n  - ", candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                "Chưa tìm thấy ffmpeg.exe.\r\n\r\n" +
                "Cách xử lý:\r\n" +
                "• Tab «Cài đặt» → ô «FFmpeg Path (optional)» → Browse chọn file ffmpeg.exe\r\n" +
                "  (thường nằm trong thư mục bin của bản build Windows).\r\n" +
                "• Hoặc đặt ffmpeg.exe vào thư mục chạy app:\r\n  " + dirExe + "\r\n\r\n" +
                "Gợi ý tải build sẵn (Windows): https://www.gyan.dev/ffmpeg/builds/\r\n" +
                "(tìm «ffmpeg-release-essentials.zip», giải nén → chọn …\\bin\\ffmpeg.exe).\r\n\r\n" +
                "Đã thử các đường dẫn:\r\n  - " + tried);
        }

        public async Task ExportCsvAsync(IList<AffiliateCandidate> candidates, string filePath, CancellationToken cancellationToken)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            var sb = new StringBuilder();
            sb.AppendLine("SourceKeyword,ProductName,Price,ImageUrl,CommissionRate,Creator,VideoUrl,ProfileUrl,Hashtags,LinkedProduct,PlayCount,LikeCount,CommentCount,ShareCount,CollectCount,DurationSeconds,CreateTimeUtc,MetricsCapturedAtUtc,VideoScript,VoiceoverTranscript,EngagementScore,EngagementReasons,LastDeepDiveError,LastMetricsError");
            foreach (var c in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.Append(EscapeCsv(c.SourceKeyword)).Append(',');
                sb.Append(EscapeCsv(c.ProductName)).Append(',');
                sb.Append(EscapeCsv(c.Price)).Append(',');
                sb.Append(EscapeCsv(c.ImageUrl)).Append(',');
                sb.Append(EscapeCsv(c.CommissionRate)).Append(',');
                sb.Append(EscapeCsv(c.Creator)).Append(',');
                sb.Append(EscapeCsv(c.VideoUrl)).Append(',');
                sb.Append(EscapeCsv(c.ProfileUrl)).Append(',');
                sb.Append(EscapeCsv(c.Hashtags)).Append(',');
                sb.Append(EscapeCsv(c.LinkedProduct)).Append(',');
                sb.Append(c.PlayCount).Append(',');
                sb.Append(c.LikeCount).Append(',');
                sb.Append(c.CommentCount).Append(',');
                sb.Append(c.ShareCount).Append(',');
                sb.Append(c.CollectCount).Append(',');
                sb.Append(c.DurationSeconds).Append(',');
                sb.Append(EscapeCsv(c.CreateTimeUtc == DateTime.MinValue ? string.Empty : c.CreateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
                sb.Append(EscapeCsv(c.MetricsCapturedAtUtc == DateTime.MinValue ? string.Empty : c.MetricsCapturedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
                sb.Append(EscapeCsv(c.VideoScript)).Append(',');
                sb.Append(EscapeCsv(c.VoiceoverTranscript)).Append(',');
                sb.Append(c.SafetyScore).Append(',');
                sb.Append(EscapeCsv(c.SafetyRiskSummary)).Append(',');
                sb.Append(EscapeCsv(c.LastDeepDiveError)).Append(',');
                sb.Append(EscapeCsv(c.LastMetricsError)).AppendLine();
            }

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                await writer.WriteAsync(sb.ToString()).ConfigureAwait(false);
            }
        }

        /// <summary>Báo cáo HTML tối giản: affiliate + trường Deep Dive (không chứa API key).</summary>
        public async Task ExportHtmlSummaryAsync(IList<AffiliateCandidate> candidates, string filePath, CancellationToken cancellationToken)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\"/><title>Affiliate + Deep Dive</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;background:#1a1d24;color:#e8e8e8;padding:16px}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;font-size:13px} th,td{border:1px solid #444;padding:8px;vertical-align:top}");
            sb.AppendLine("th{background:#2d3340} tr:nth-child(even){background:#22262e} .num{text-align:center}</style></head><body>");
            sb.AppendLine($"<h2>Báo cáo Affiliate — {DateTime.Now:yyyy-MM-dd HH:mm}</h2><p>Số dòng: {candidates.Count}</p>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>Từ khoá</th><th>Caption</th><th>Score</th><th>Sản phẩm đính kèm</th><th>Views</th><th>Likes</th><th>Cmt</th><th>Share</th><th>Saves</th><th>Dài</th><th>Đăng</th><th>Lời thoại (rút gọn)</th><th>Deep Dive lỗi</th><th>Video URL</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var c in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sk = EscapeHtml(Truncate(c?.SourceKeyword, 120));
                var cap = EscapeHtml(Truncate(c?.ProductName, 200));
                var lp = EscapeHtml(Truncate(c?.LinkedProduct, 120));
                var vo = EscapeHtml(Truncate(c?.VoiceoverTranscript, 180));
                var err = EscapeHtml(Truncate(c?.LastDeepDiveError, 160));
                var vu = EscapeHtml(c?.VideoUrl ?? string.Empty);
                var pc = FormatCountCompactExport(c?.PlayCount ?? 0);
                var lc = FormatCountCompactExport(c?.LikeCount ?? 0);
                var cc = FormatCountCompactExport(c?.CommentCount ?? 0);
                var sc = FormatCountCompactExport(c?.ShareCount ?? 0);
                var sv = FormatCountCompactExport(c?.CollectCount ?? 0);
                var du = FormatDurationShortExport(c?.DurationSeconds ?? 0);
                var posted = (c?.CreateTimeUtc ?? DateTime.MinValue) == DateTime.MinValue
                    ? "—"
                    : EscapeHtml(c.CreateTimeUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{sk}</td><td>{cap}</td><td class=\"num\">{c?.SafetyScore ?? 0}</td><td>{lp}</td><td class=\"num\">{pc}</td><td class=\"num\">{lc}</td><td class=\"num\">{cc}</td><td class=\"num\">{sc}</td><td class=\"num\">{sv}</td><td class=\"num\">{du}</td><td class=\"num\">{posted}</td><td>{vo}</td><td>{err}</td><td><a href=\"{vu}\" style=\"color:#8ab4ff\">link</a></td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                await writer.WriteAsync(sb.ToString()).ConfigureAwait(false);
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private static string FormatCountCompactExport(long n)
        {
            if (n <= 0) return "—";
            if (n < 1_000) return n.ToString("N0");
            if (n < 1_000_000) return (n / 1000.0).ToString("0.#") + "K";
            if (n < 1_000_000_000) return (n / 1_000_000.0).ToString("0.#") + "M";
            return (n / 1_000_000_000.0).ToString("0.#") + "B";
        }

        private static string FormatDurationShortExport(int totalSeconds)
        {
            if (totalSeconds <= 0) return "—";
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1
                ? string.Format("{0:D1}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds)
                : string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
        }

        private static string EscapeHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ToAbsoluteTikTokUrl(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return string.Empty;
            }

            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }

            if (href.StartsWith("/", StringComparison.Ordinal))
            {
                return "https://www.tiktok.com" + href;
            }

            return "https://www.tiktok.com/" + href;
        }

        private static AutomationProfile ResolveRunningProfile(AppSettings settings, string runningProfileName, Action<string> logAction)
        {
            var profiles = settings?.Profiles;
            if (profiles == null || profiles.Count == 0)
            {
                logAction?.Invoke("[Affiliate] Chưa cấu hình profile trong Cài đặt — dùng profile Playwright mặc định.");
                return null;
            }

            AutomationProfile selected = null;
            if (!string.IsNullOrWhiteSpace(runningProfileName) &&
                !string.Equals(runningProfileName.Trim(), "default", StringComparison.OrdinalIgnoreCase))
            {
                selected = profiles.FirstOrDefault(p =>
                    p != null &&
                    string.Equals(p.Name, runningProfileName.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (selected == null)
            {
                selected = profiles.FirstOrDefault(p => p != null);
                if (!string.IsNullOrWhiteSpace(runningProfileName) &&
                    !string.Equals(runningProfileName.Trim(), "default", StringComparison.OrdinalIgnoreCase))
                {
                    logAction?.Invoke($"[Affiliate] Không tìm thấy profile «{runningProfileName}». Dùng profile đầu tiên: «{selected?.Name}».");
                }
            }

            if (selected == null)
            {
                logAction?.Invoke("[Affiliate] Không có profile hợp lệ.");
                return null;
            }

            logAction?.Invoke($"[Affiliate] Dùng profile: «{selected.Name}»");
            return selected;
        }

        private static async Task ScrollShopFeedAsync(BrowserAutomation browser, CancellationToken cancellationToken)
        {
            var page = browser.Page;
            if (page == null)
            {
                return;
            }

            for (var i = 0; i < 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await page.EvaluateAsync("window.scrollBy(0, Math.max(600, Math.floor(window.innerHeight * 0.85)));").ConfigureAwait(false);
                    await Task.Delay(900, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch
                {
                    break;
                }
            }
        }

        private static bool IsTikTokProductUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return url.IndexOf("/view/product/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/product/detail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/shop/p/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   Regex.IsMatch(url, @"[?&]product_id=\d+", RegexOptions.IgnoreCase);
        }

        private static bool IsTikTokVideoUrl(string url)
        {
            return !string.IsNullOrEmpty(url) &&
                   url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractCreatorFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            var atIndex = url.IndexOf("/@", StringComparison.Ordinal);
            if (atIndex < 0)
            {
                return string.Empty;
            }

            var start = atIndex + 1;
            var end = url.IndexOf('/', start);
            if (end < 0)
            {
                end = url.Length;
            }

            return url.Substring(start, end - start);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            var escaped = value.Replace("\"", "\"\"");
            return needsQuotes ? "\"" + escaped + "\"" : escaped;
        }

        private static async Task LogShopDiagnosticsAsync(BrowserAutomation browserPageWrapper, Action<string> logAction)
        {
            var page = browserPageWrapper.Page;
            if (page == null || logAction == null)
            {
                return;
            }

            const string js = @"() => {
                const out = {
                    url: location.href,
                    title: document.title || '',
                    hasUniversal: !!window.__UNIVERSAL_DATA_FOR_REHYDRATION__,
                    hasSigi: !!window.SIGI_STATE,
                    productLinkCount: document.querySelectorAll('a[href*=""/view/product/""]').length,
                    productHrefLoose: document.querySelectorAll('a[href*=""product""]').length,
                    productCardCount: document.querySelectorAll('[data-e2e=""product-item""], [data-e2e*=""product-card""], [class*=""product-card""]').length,
                    videoLinkCount: document.querySelectorAll('a[href*=""/video/""]').length,
                    needsLogin: !!document.querySelector('a[href*=""/login""], [data-e2e=""top-login-button""]'),
                    rawTextHead: (document.body && document.body.innerText ? document.body.innerText.slice(0, 160) : '')
                };
                return JSON.stringify(out);
            }";

            try
            {
                var raw = await page.EvaluateAsync<string>(js).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return;
                }

                var diag = JsonConvert.DeserializeAnonymousType(raw, new
                {
                    url = string.Empty,
                    title = string.Empty,
                    hasUniversal = false,
                    hasSigi = false,
                    productLinkCount = 0,
                    productHrefLoose = 0,
                    productCardCount = 0,
                    videoLinkCount = 0,
                    needsLogin = false,
                    rawTextHead = string.Empty
                });

                logAction.Invoke("[Shop diag] URL: " + diag.url);
                logAction.Invoke("[Shop diag] Title: " + diag.title);
                logAction.Invoke($"[Shop diag] UNIVERSAL_DATA={diag.hasUniversal}, SIGI_STATE={diag.hasSigi}, productLinks={diag.productLinkCount}, productHref*={diag.productHrefLoose}, productCards={diag.productCardCount}, videoLinks={diag.videoLinkCount}, needsLogin={diag.needsLogin}");
                if (!string.IsNullOrWhiteSpace(diag.rawTextHead))
                {
                    logAction.Invoke("[Shop diag] Body head: " + diag.rawTextHead.Replace("\n", " ").Replace("\r", " "));
                }

                if (diag.needsLogin)
                {
                    logAction.Invoke("[Shop diag] Trang đang yêu cầu đăng nhập. Mở 'Đăng nhập TikTok thủ công' rồi quét lại.");
                }
            }
            catch (Exception ex)
            {
                logAction.Invoke("[Shop diag] Lỗi khi đọc trạng thái trang: " + ex.Message);
            }
        }

        private static async Task<List<CandidateSnapshot>> ExtractShopProductsAsync(BrowserAutomation browserPageWrapper)
        {
            var page = browserPageWrapper.Page;
            if (page == null)
            {
                return new List<CandidateSnapshot>();
            }

            // For shop pages we look for product nodes (title + price/product_id) inside the
            // hydration JSON. DOM fallback walks through known shop-product card selectors.
            const string js = @"() => {
                const safeJsonParse = (s) => { try { return JSON.parse(s); } catch (e) { return null; } };
                const isProductNode = (n) => {
                    if (!n || typeof n !== 'object' || Array.isArray(n)) return false;
                    if (Object.prototype.hasOwnProperty.call(n, 'desc') && n.author && typeof n.author === 'object') {
                        return false;
                    }
                    const pid = n.product_id || n.productId;
                    const title = (n.title || n.product_name || n.productName || n.name || '').trim();
                    if (!title || title.length < 2) return false;
                    if (pid) return true;
                    const gid = n.id;
                    if (gid && (n.product_price || n.formatted_price || n.formattedPrice || n.product_image || n.seller || n.shop)) {
                        return true;
                    }
                    return false;
                };

                const collectProducts = () => {
                    const found = new Map();
                    const seenObjs = new WeakSet();
                    const visit = (n, depth) => {
                        if (!n || typeof n !== 'object' || depth > 14) return;
                        if (seenObjs.has(n)) return;
                        seenObjs.add(n);
                        if (isProductNode(n)) {
                            const id = String(n.product_id || n.productId || n.id);
                            if (/^\d{5,}$/.test(id) && !found.has(id)) found.set(id, n);
                        }
                        if (Array.isArray(n)) {
                            for (const v of n) visit(v, depth + 1);
                            return;
                        }
                        for (const k of Object.keys(n)) {
                            const v = n[k];
                            if (v && typeof v === 'object') visit(v, depth + 1);
                        }
                    };
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__) visit(window.__UNIVERSAL_DATA_FOR_REHYDRATION__, 0);
                    if (window.SIGI_STATE) visit(window.SIGI_STATE, 0);
                    if (window.__pace_f) {
                        try { for (const e of window.__pace_f) visit(e, 0); } catch (e) {}
                    }
                    return Array.from(found.values());
                };

                const pickPrice = (p) => {
                    if (!p) return '';
                    if (typeof p === 'string') return p.trim();
                    if (typeof p === 'object') {
                        const order = ['formatted_price','formattedPrice','real_price','realPrice','price','priceVal','min_price','original_price','product_price'];
                        for (const k of order) {
                            const v = p[k];
                            if (typeof v === 'string' && v.trim()) return v.trim();
                            if (typeof v === 'number' && v > 0) return String(v);
                        }
                    }
                    return '';
                };

                const pickImage = (n) => {
                    const buckets = [n.images, n.image, n.cover, n.product_image, n.productImage, n.thumbnail];
                    for (const b of buckets) {
                        if (!b) continue;
                        if (typeof b === 'string' && b.trim()) return b.trim();
                        if (Array.isArray(b)) {
                            for (const item of b) {
                                if (!item) continue;
                                if (typeof item === 'string' && item.trim()) return item.trim();
                                if (item.url_list && Array.isArray(item.url_list) && item.url_list.length > 0) return item.url_list[0];
                                if (item.urlList && Array.isArray(item.urlList) && item.urlList.length > 0) return item.urlList[0];
                                if (typeof item.url === 'string') return item.url;
                            }
                        } else if (typeof b === 'object') {
                            if (b.url_list && b.url_list.length > 0) return b.url_list[0];
                            if (b.urlList && b.urlList.length > 0) return b.urlList[0];
                            if (typeof b.url === 'string') return b.url;
                        }
                    }
                    return '';
                };

                const pickSeller = (n) => {
                    const buckets = [n.seller, n.shop, n.shop_info, n.shopInfo, n.seller_info, n.sellerInfo, n.author];
                    for (const b of buckets) {
                        if (!b || typeof b !== 'object') continue;
                        const candidates = [b.shop_name, b.shopName, b.name, b.nickname, b.title, b.uniqueId, b.unique_id];
                        for (const c of candidates) {
                            if (typeof c === 'string' && c.trim()) return c.trim();
                        }
                    }
                    return '';
                };

                const pickCommission = (n) => {
                    const direct = n.commission_rate || n.commissionRate || n.commission || n.commission_value || n.commissionValue;
                    if (typeof direct === 'string' && direct.trim()) return direct.trim();
                    if (typeof direct === 'number' && direct > 0) return direct + '%';
                    return '';
                };

                const items = [];
                const seen = new Set();
                for (const n of collectProducts()) {
                    const id = String(n.product_id || n.productId || n.id || '');
                    if (!id || seen.has(id)) continue;
                    seen.add(id);
                    const title = (n.title || n.product_name || n.productName || n.name || '').trim();
                    const price = pickPrice(n.price || n.formatted_price || n.formattedPrice || n.product_price);
                    const image = pickImage(n);
                    const seller = pickSeller(n);
                    const commission = pickCommission(n);
                    items.push({
                        Source: 'shop-json',
                        VideoId: id,
                        ProductName: title,
                        Creator: seller,
                        AuthorUniqueId: '',
                        PriceText: price,
                        CommissionText: commission,
                        ImageUrl: image,
                        VideoUrl: 'https://www.tiktok.com/view/product/' + id
                    });
                }

                if (items.length === 0) {
                    const cards = Array.from(document.querySelectorAll(
                        '[data-e2e=""product-item""], ' +
                        '[data-e2e*=""product-card""], ' +
                        '[data-e2e*=""shop-product""], ' +
                        '[class*=""product-card""], ' +
                        '[class*=""ProductCard""], ' +
                        'a[href*=""/view/product/""]'
                    ));
                    for (const c of cards) {
                        const card = c.matches('a') ? (c.closest('[data-e2e], [class*=""product-card""], [class*=""ProductCard""]') || c) : c;
                        const link = card.matches('a') ? card : card.querySelector('a[href*=""/view/product/""]') || card.querySelector('a[href*=""/shop/""]');
                        const href = link ? link.href : '';
                        if (!href) continue;
                        const titleEl = card.querySelector('[data-e2e=""product-title""], [data-e2e*=""title""], h3, h4, [class*=""title""]');
                        const priceEl = card.querySelector('[data-e2e=""product-price""], [data-e2e*=""price""], [class*=""price""], [class*=""Price""]');
                        const sellerEl = card.querySelector('[data-e2e*=""seller""], [data-e2e*=""shop""], [class*=""seller""], [class*=""shop""]');
                        const img = card.querySelector('img');
                        const cardText = (card.innerText || '');
                        const commMatch = cardText.match(/\d{1,3}\s*%/);
                        items.push({
                            Source: 'shop-dom',
                            VideoId: '',
                            ProductName: titleEl ? (titleEl.textContent || '').trim() : '',
                            Creator: sellerEl ? (sellerEl.textContent || '').trim() : '',
                            AuthorUniqueId: '',
                            PriceText: priceEl ? (priceEl.textContent || '').trim() : '',
                            CommissionText: commMatch ? commMatch[0] : '',
                            ImageUrl: img && img.src ? img.src : '',
                            VideoUrl: href
                        });
                    }
                }

                if (items.length === 0) {
                    const anchors = Array.from(document.querySelectorAll('a[href]'));
                    for (const a of anchors) {
                        const h = a.href || '';
                        const m = h.match(/\/(?:view\/)?product\/(\d{6,})/i) ||
                            h.match(/product\/detail\/(\d{6,})/i) ||
                            h.match(/[?&]product_id=(\d{6,})/i);
                        if (!m) continue;
                        const id = m[1];
                        if (seen.has(id)) continue;
                        seen.add(id);
                        let title = (a.getAttribute('title') || a.innerText || a.getAttribute('aria-label') || '').trim();
                        title = title.split('\n')[0].slice(0, 200);
                        if (!title) title = 'TikTok Shop product #' + id;
                        items.push({
                            Source: 'shop-link',
                            VideoId: id,
                            ProductName: title,
                            Creator: '',
                            AuthorUniqueId: '',
                            PriceText: '',
                            CommissionText: '',
                            ImageUrl: '',
                            VideoUrl: 'https://www.tiktok.com/view/product/' + id
                        });
                    }
                }

                return JSON.stringify(items);
            }";

            var json = await page.EvaluateAsync<string>(js).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<CandidateSnapshot>();
            }

            return JsonConvert.DeserializeObject<List<CandidateSnapshot>>(json) ?? new List<CandidateSnapshot>();
        }

        private static async Task WaitForVideoSearchAsync(BrowserAutomation browserPageWrapper, CancellationToken cancellationToken)
        {
            var page = browserPageWrapper.Page;
            if (page == null)
            {
                return;
            }

            // Strict wait — only succeed when search-scoped data exists, otherwise we'd grab profile/feed
            // videos by mistake. Min 5s of polling, hard cap 12s.
            const string probe = @"() => {
                try {
                    if (document.querySelector('[data-e2e=""search_video-item""]')) return true;
                    if (window.SIGI_STATE && window.SIGI_STATE.ItemList) {
                        const il = window.SIGI_STATE.ItemList;
                        for (const k of Object.keys(il)) {
                            const lk = k.toLowerCase();
                            if ((lk.includes('search') || lk === 'general') && il[k] && Array.isArray(il[k].list) && il[k].list.length > 0) {
                                return true;
                            }
                        }
                    }
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__) {
                        try {
                            const root = window.__UNIVERSAL_DATA_FOR_REHYDRATION__;
                            const scope = root.__DEFAULT_SCOPE__ || {};
                            const sp = scope['webapp.search-page'] || scope['webapp.search-result-container'] || {};
                            const candidates = [sp.searchItemList, sp.itemList,
                                sp.data && sp.data.searchItemList, sp.data && sp.data.itemList,
                                sp.data && sp.data.searchItems];
                            for (const list of candidates) {
                                if (Array.isArray(list) && list.length > 0) return true;
                            }
                        } catch (e) {}
                    }
                } catch (e) {}
                return false;
            }";

            var deadline = DateTime.UtcNow.AddSeconds(12);
            var minStop = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var ready = await page.EvaluateAsync<bool>(probe).ConfigureAwait(false);
                    if (ready && DateTime.UtcNow >= minStop)
                    {
                        return;
                    }
                }
                catch
                {
                    // Page may navigate; ignore and retry.
                }

                try
                {
                    await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        private static async Task<List<CandidateSnapshot>> ExtractVideoSearchResultsAsync(BrowserAutomation browserPageWrapper)
        {
            var page = browserPageWrapper.Page;
            if (page == null)
            {
                return new List<CandidateSnapshot>();
            }

            // DOM-based extraction: quét trực tiếp các card kết quả search trên màn hình.
            // Lấy CAPTION video làm ProductName (innerText → textContent → img.alt → fallback chuỗi).
            const string js = @"() => {
                const results = [];
                try {
                    const items = document.querySelectorAll('div[data-e2e=""search_video-item""]');
                    for (const item of items) {
                        const videoLinkEl = item.querySelector('a[href*=""/video/""]');
                        if (!videoLinkEl) continue;
                        const authorEl = item.querySelector('a[data-e2e=""search-video-user-link""], p[data-e2e=""search-user-unique-id""]');

                        // Lấy nội dung Caption (raw), sau đó tách hashtag và làm sạch làm ProductName
                        const descEl = item.querySelector('div[data-e2e=""search-video-desc""]');
                        let desc = '';
                        if (descEl) {
                            desc = (descEl.innerText || '').trim() || (descEl.textContent || '').trim();
                        }
                        if (!desc || desc.startsWith('@')) {
                            const img = item.querySelector('img');
                            if (img && img.alt) desc = img.alt;
                            else desc = 'Sản phẩm TikTok (Video chưa có tiêu đề)';
                        }

                        const tagsMatch = desc.match(/#[\p{L}\d_]+/gu);
                        const hashtags = tagsMatch ? tagsMatch.join(' ') : '';
                        let cleanDesc = desc.replace(/#[\p{L}\d_]+/gu, '').replace(/\s+/g, ' ').trim();
                        if (!cleanDesc) cleanDesc = 'Sản phẩm TikTok (Video chưa có tiêu đề)';

                        // ===== Trích xuất sản phẩm gắn anchor =====
                        // TikTok đổi data-e2e/class liên tục → thử nhiều selector & fallback.
                        let linkedProduct = '';
                        const tryEls = [
                            item.querySelector('div[data-e2e=""video-anchor""]'),
                            item.querySelector('[data-e2e*=""anchor""]'),
                            item.querySelector('[data-e2e*=""shop-link""]'),
                            item.querySelector('[data-e2e*=""card-link""]'),
                            item.querySelector('[data-e2e*=""product""]'),
                            item.querySelector('[class*=""VideoAnchor""]'),
                            item.querySelector('[class*=""ShopAnchor""]'),
                            item.querySelector('[class*=""ProductAnchor""]'),
                            item.querySelector('[class*=""AnchorContainer""]'),
                            item.querySelector('a[href*=""/shop/""]'),
                            item.querySelector('a[href*=""/product/""]'),
                            item.querySelector('a[href*=""tiktokshop""]'),
                            item.querySelector('a[href*=""shop_link""]')
                        ];
                        for (const el of tryEls) {
                            if (!el) continue;
                            const t = ((el.innerText || el.textContent) || '').trim();
                            if (t && t.length >= 2) { linkedProduct = t; break; }
                        }
                        // Heuristic: nếu giỏ hàng/anchor không text nhưng có aria-label
                        if (!linkedProduct) {
                            const ariaEl = item.querySelector('[aria-label*=""shop""], [aria-label*=""Shop""], [aria-label*=""giỏ hàng""], [aria-label*=""sản phẩm""]');
                            if (ariaEl) {
                                const al = (ariaEl.getAttribute('aria-label') || '').trim();
                                if (al) linkedProduct = al;
                            }
                        }
                        if (linkedProduct.length > 200) linkedProduct = linkedProduct.substring(0, 200) + '…';
                        if (!linkedProduct) linkedProduct = 'Không hiện giỏ hàng';

                        const vUrl = videoLinkEl.href || '';
                        const author = authorEl ? (authorEl.textContent || '').trim() : 'Tác giả ẩn';
                        const coverEl = item.querySelector('img');
                        const cover = coverEl ? (coverEl.src || '') : '';

                        const uniqueId = author.startsWith('@') ? author.substring(1) : author;
                        const profileUrl = author.startsWith('@')
                            ? ('https://www.tiktok.com/' + author)
                            : ('https://www.tiktok.com/@' + author);

                        results.push({
                            Source: 'search-dom',
                            VideoId: '',
                            VideoUrl: vUrl,
                            ProductName: cleanDesc,
                            Creator: author,
                            AuthorUniqueId: uniqueId,
                            ProfileUrl: profileUrl,
                            PriceText: 'Vào link xem',
                            CommissionText: '',
                            ImageUrl: cover,
                            Hashtags: hashtags,
                            LinkedProduct: linkedProduct
                        });
                    }
                } catch (e) {}
                return JSON.stringify(results);
            }";

            string json;
            try
            {
                json = await page.EvaluateAsync<string>(js).ConfigureAwait(false);
            }
            catch
            {
                return new List<CandidateSnapshot>();
            }

            if (string.IsNullOrWhiteSpace(json) || json == "[]")
            {
                return new List<CandidateSnapshot>();
            }

            return JsonConvert.DeserializeObject<List<CandidateSnapshot>>(json) ?? new List<CandidateSnapshot>();
        }

        private static async Task WaitForRehydrationAsync(BrowserAutomation browserPageWrapper, CancellationToken cancellationToken)
        {
            var page = browserPageWrapper.Page;
            if (page == null)
            {
                return;
            }

            const string probe = @"() => {
                try {
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__) return true;
                    if (window.SIGI_STATE && window.SIGI_STATE.ItemModule &&
                        Object.keys(window.SIGI_STATE.ItemModule).length > 0) return true;
                    if (document.querySelector('[data-e2e=""search_top-item""], [data-e2e*=""search-card""]')) return true;
                    if (document.querySelector('[data-e2e=""product-item""], [data-e2e*=""product-card""], a[href*=""/view/product/""]')) return true;
                    if (document.querySelectorAll('a[href*=""/video/""]').length >= 3) return true;
                } catch (e) {}
                return false;
            }";

            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var ready = await page.EvaluateAsync<bool>(probe).ConfigureAwait(false);
                    if (ready)
                    {
                        return;
                    }
                }
                catch
                {
                    // Page may navigate; ignore and retry.
                }

                try
                {
                    await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        private static async Task<List<CandidateSnapshot>> ExtractVisibleCandidatesAsync(BrowserAutomation browserPageWrapper)
        {
            var page = browserPageWrapper.Page;
            if (page == null)
            {
                return new List<CandidateSnapshot>();
            }

            // Strategy order:
            //   1) window.__UNIVERSAL_DATA_FOR_REHYDRATION__ — newer TikTok shell
            //   2) window.SIGI_STATE.ItemModule          — legacy shell
            //   3) DOM cards with caption hunted from aria-label / img alt / data-e2e (skip ID-like text)
            //   4) Page <title> + <meta property=""og:description""> as last-resort label
            const string js = @"() => {
                const isIdLike = (s) => {
                    const t = (s == null ? '' : String(s)).trim();
                    if (!t) return true;
                    return /^@?\d{6,}$/.test(t) || /^[A-Za-z0-9_\-]{12,}$/.test(t) && !/\s/.test(t) && /\d{6,}/.test(t);
                };
                const safeJsonParse = (s) => { try { return JSON.parse(s); } catch (e) { return null; } };

                const collectVideoNodes = () => {
                    const found = new Map();
                    const pushNode = (n) => {
                        if (!n || typeof n !== 'object') return;
                        const id = n.id || n.itemId || n.aweme_id;
                        if (!id) return;
                        const hasAuthor = n.author && typeof n.author === 'object';
                        const hasDesc = Object.prototype.hasOwnProperty.call(n, 'desc');
                        if (!hasAuthor || !hasDesc) return;
                        if (!found.has(String(id))) found.set(String(id), n);
                    };
                    const seenObjs = new WeakSet();
                    const visit = (n, depth) => {
                        if (!n || typeof n !== 'object' || depth > 12) return;
                        if (seenObjs.has(n)) return;
                        seenObjs.add(n);
                        pushNode(n);
                        if (Array.isArray(n)) {
                            for (const v of n) visit(v, depth + 1);
                            return;
                        }
                        for (const k of Object.keys(n)) {
                            const v = n[k];
                            if (v && typeof v === 'object') visit(v, depth + 1);
                        }
                    };
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__) visit(window.__UNIVERSAL_DATA_FOR_REHYDRATION__, 0);
                    if (window.SIGI_STATE) {
                        const itemModule = window.SIGI_STATE.ItemModule;
                        if (itemModule && typeof itemModule === 'object') {
                            for (const v of Object.values(itemModule)) pushNode(v);
                        }
                        visit(window.SIGI_STATE, 0);
                    }
                    return Array.from(found.values());
                };

                const extractPrice = (n) => {
                    try {
                        const buckets = [];
                        if (n.anchors && Array.isArray(n.anchors)) buckets.push(...n.anchors);
                        if (n.product) buckets.push(n.product);
                        if (n.commerceInfo) buckets.push(n.commerceInfo);
                        for (const b of buckets) {
                            if (!b) continue;
                            if (b.price && typeof b.price === 'string' && b.price.trim()) return b.price.trim();
                            if (b.priceText && typeof b.priceText === 'string') return b.priceText.trim();
                            if (b.extraInfo && typeof b.extraInfo === 'string') {
                                const ex = safeJsonParse(b.extraInfo);
                                if (ex && (ex.price || ex.priceText)) return String(ex.price || ex.priceText).trim();
                            }
                        }
                    } catch (e) {}
                    return '';
                };

                const items = [];
                const seenIds = new Set();
                for (const n of collectVideoNodes()) {
                    const id = String(n.id || n.itemId || n.aweme_id || '');
                    if (!id || seenIds.has(id)) continue;
                    seenIds.add(id);
                    const author = n.author || {};
                    const uniqueId = author.uniqueId || author.unique_id || '';
                    const nickname = author.nickname || '';
                    const cover = (n.video && (n.video.cover || n.video.originCover || n.video.dynamicCover)) || '';
                    const desc = (n.desc || '').trim();
                    items.push({
                        Source: 'json',
                        VideoId: id,
                        ProductName: desc,
                        Creator: nickname || '',
                        AuthorUniqueId: uniqueId || '',
                        PriceText: extractPrice(n),
                        CommissionText: '',
                        ImageUrl: cover || '',
                        VideoUrl: uniqueId && id ? ('https://www.tiktok.com/@' + uniqueId + '/video/' + id) : ''
                    });
                }

                if (items.length === 0) {
                    const anchors = Array.from(document.querySelectorAll('a[href*=""/video/""]'));
                    for (const a of anchors) {
                        const href = a.href || '';
                        if (!href) continue;
                        const card = a.closest('[data-e2e=""search_top-item""], [data-e2e*=""search-card""], [data-e2e=""recommend-list-item-container""], div[data-e2e], article, div') || a.parentElement || a;
                        const img = card ? card.querySelector('img') : null;
                        const descEl = card && (
                            card.querySelector('[data-e2e=""search-card-desc""]') ||
                            card.querySelector('[data-e2e=""search-card-video-caption""]') ||
                            card.querySelector('[data-e2e*=""video-desc""]') ||
                            card.querySelector('[data-e2e*=""desc""]') ||
                            card.querySelector('[data-e2e*=""caption""]')
                        );
                        const captionSources = [
                            descEl ? descEl.textContent : null,
                            img ? img.getAttribute('alt') : null,
                            a.getAttribute('aria-label'),
                            card && card.querySelector('h3') ? card.querySelector('h3').textContent : null,
                            card && card.querySelector('strong') ? card.querySelector('strong').textContent : null
                        ];
                        let productName = '';
                        for (const c of captionSources) {
                            const t = (c == null ? '' : String(c)).trim();
                            if (!t) continue;
                            if (isIdLike(t)) continue;
                            productName = t;
                            break;
                        }
                        const userLink = card ? card.querySelector('a[href^=""/@""]') : null;
                        let authorUniqueId = '';
                        if (userLink) {
                            const m = (userLink.getAttribute('href') || '').match(/^\/@([^\/?#]+)/);
                            if (m) authorUniqueId = m[1];
                        }
                        if (!authorUniqueId) {
                            const m = href.match(/\/@([^\/?#]+)\/video\//);
                            if (m) authorUniqueId = m[1];
                        }
                        const nickEl = card ? card.querySelector(
                            '[data-e2e=""search-card-user-unique-id""], ' +
                            '[data-e2e*=""user-uniqueid""], ' +
                            '[data-e2e=""search-card-user-link""], ' +
                            '[data-e2e*=""nickname""]'
                        ) : null;
                        const creator = nickEl && nickEl.textContent ? nickEl.textContent.trim() : '';
                        const cardText = (card && card.innerText) || '';
                        const priceMatch = cardText.match(/(?:₫|\$|đ)\s?[\d.,kK]+|[\d.,]+\s?(?:₫|đ|\$)/);
                        const commMatch = cardText.match(/\d{1,3}\s*%/);
                        items.push({
                            Source: 'dom',
                            VideoId: '',
                            ProductName: productName,
                            Creator: isIdLike(creator) ? '' : creator,
                            AuthorUniqueId: authorUniqueId,
                            PriceText: priceMatch ? priceMatch[0] : '',
                            CommissionText: commMatch ? commMatch[0] : '',
                            ImageUrl: img && img.src ? img.src : '',
                            VideoUrl: href
                        });
                    }
                }

                if (items.length === 0) {
                    const titleEl = document.title || '';
                    const ogEl = document.querySelector('meta[property=""og:description""]');
                    const og = ogEl ? (ogEl.getAttribute('content') || '') : '';
                    const fallback = (og.trim() || titleEl.trim());
                    if (fallback) {
                        items.push({
                            Source: 'page',
                            VideoId: '',
                            ProductName: fallback,
                            Creator: '',
                            AuthorUniqueId: '',
                            PriceText: '',
                            CommissionText: '',
                            ImageUrl: '',
                            VideoUrl: location.href
                        });
                    }
                }

                return JSON.stringify(items);
            }";

            var json = await page.EvaluateAsync<string>(js).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<CandidateSnapshot>();
            }

            return JsonConvert.DeserializeObject<List<CandidateSnapshot>>(json) ?? new List<CandidateSnapshot>();
        }

        private static bool IsIdLike(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            if (Regex.IsMatch(text, @"^@?\d{6,}$"))
            {
                return true;
            }

            // Long token without spaces, dominated by digits — typical of TikTok numeric IDs.
            if (!text.Contains(" ") && text.Length >= 12 && Regex.IsMatch(text, @"\d{6,}"))
            {
                return true;
            }

            return false;
        }

        private static string ResolveProductName(string rawDesc, string creatorDisplay)
        {
            var desc = (rawDesc ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(desc) && !IsIdLike(desc))
            {
                return desc;
            }

            if (!string.IsNullOrWhiteSpace(creatorDisplay) && !IsIdLike(creatorDisplay))
            {
                return "TikTok video — " + creatorDisplay;
            }

            return "TikTok video";
        }

        private static async Task PerformStealthInteractionAsync(BrowserAutomation browser, CancellationToken cancellationToken, Random random)
        {
            var page = browser.Page;
            if (page == null)
            {
                return;
            }

            var scrollPx = random.Next(560, 1450);
            await page.Mouse.MoveAsync(random.Next(120, 920), random.Next(180, 760)).ConfigureAwait(false);
            if (random.NextDouble() < 0.28d)
            {
                await page.Mouse.ClickAsync(random.Next(140, 880), random.Next(220, 720), new Microsoft.Playwright.MouseClickOptions { Delay = random.Next(40, 130) }).ConfigureAwait(false);
            }

            await page.EvaluateAsync($"window.scrollBy(0, {scrollPx});").ConfigureAwait(false);
            if (random.NextDouble() < 0.35d)
            {
                await page.EvaluateAsync($"window.scrollBy(0, {-random.Next(80, 260)});").ConfigureAwait(false);
            }

            await browser.RandomDelayAsync(random.Next(700, 1300), random.Next(1400, 2400), cancellationToken).ConfigureAwait(false);
        }

        private static string NormalizePrice(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? "N/A" : text;
        }

        private static string NormalizeCommissionRate(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "N/A";
            }

            var match = Regex.Match(text, @"\d{1,3}\s*%");
            return match.Success ? match.Value.Replace(" ", string.Empty) : text;
        }

        private static string NormalizeImageUrl(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.StartsWith("//", StringComparison.Ordinal) ? "https:" + text : text;
        }

        private class CandidateSnapshot
        {
            public string Source { get; set; } = string.Empty;
            public string VideoId { get; set; } = string.Empty;
            public string VideoUrl { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string Creator { get; set; } = string.Empty;
            public string AuthorUniqueId { get; set; } = string.Empty;
            public string PriceText { get; set; } = string.Empty;
            public string CommissionText { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;
            public string Hashtags { get; set; } = string.Empty;
            public string LinkedProduct { get; set; } = string.Empty;
        }
    }
}
