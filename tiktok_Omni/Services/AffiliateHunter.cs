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
using tiktok_Omni.Helpers;
using tiktok_Omni.Services.Affiliate;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace tiktok_Omni.Services
{
    public class AffiliateCandidate
    {
        /// <summary>Từ khoá lần quét tạo ra dòng này (có thể gộp nhiều khoá nếu trùng VideoUrl).</summary>
        public string SourceKeyword { get; set; } = string.Empty;

        /// <summary>Nền tảng gốc: TikTok, Facebook, YouTube, …</summary>
        public string SourcePlatform { get; set; } = string.Empty;

        /// <summary>Profile Chrome khi săn / tải — phân tách dữ liệu đa nick.</summary>
        public string ProfileName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string CommissionRate { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public string Hashtags { get; set; } = string.Empty;
        public string LinkedProduct { get; set; } = "Chưa rõ";
        /// <summary>Ngách do Gemini gán sau Hunt (Gia dụng, Làm đẹp, …).</summary>
        public string Category { get; set; } = string.Empty;
        public int SafetyScore { get; set; }
        public string SafetyRiskSummary { get; set; } = string.Empty;
        public string VideoScript { get; set; } = string.Empty;
        public string VoiceoverTranscript { get; set; } = string.Empty;
        /// <summary>Feedback khách hàng (chuỗi hoặc nối |||) — dùng cho kịch bản Slideshow / Deep.</summary>
        public string CustomerReviews { get; set; } = string.Empty;
        /// <summary>Ghi nhận lỗi gần nhất khi Deep Dive hàng loạt (để user xem trên lưới / CSV).</summary>
        public string LastDeepDiveError { get; set; } = string.Empty;
        /// <summary>Slideshow đã render xong từ tab Affiliate (job queue).</summary>
        public bool IsSlideshowRendered { get; set; }
        public string LastRenderOutputPath { get; set; } = string.Empty;
        public string LastRenderError { get; set; } = string.Empty;

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
        private const int DefaultMaxConcurrentSourceHunts = 2;
        private const string SeleniumBrowserProfileFolder = "browser_profile";

        private readonly IReadOnlyList<IAffiliateSource> _affiliateSources;
        private readonly SemaphoreSlim _sourceHuntThrottle;

        public AffiliateHunter(int maxConcurrentSourceHunts = DefaultMaxConcurrentSourceHunts)
        {
            var max = Math.Max(1, Math.Min(maxConcurrentSourceHunts, 4));
            _sourceHuntThrottle = new SemaphoreSlim(max, max);
            _affiliateSources = new IAffiliateSource[]
            {
                new TikTokHuntStrategy(this),
                new FacebookHuntStrategy(new FacebookReelsHuntService()),
                new YouTubeHuntStrategy(new YouTubeShortsHuntService())
            };
        }

        public IReadOnlyList<IAffiliateSource> RegisteredSources => _affiliateSources;

        private static readonly string[] ShopAffiliateMarketplaceUrls =
        {
            "https://affiliate.tiktok.com/connection/creator/marketplace",
            "https://affiliate.tiktok.com/connection/creator/product/marketplace",
            "https://affiliate-us.tiktok.com/connection/creator/marketplace"
        };

        private static readonly HashSet<string> SeleniumTikTokSessionCookieNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sessionid",
            "sessionid_ss",
            "sid_tt",
            "sid_guard",
            "sid_ucp_v1",
            "ssid_ucp_v1",
            "odin_tt",
            "multi_sids",
            "d_ticket",
            "passport_auth_status"
        };

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
            return HuntAsync(
                keywords,
                maxResults,
                new[] { AffiliateSourceIds.TikTok },
                searchMode,
                cancellationToken,
                logAction,
                configManager,
                runningProfileName);
        }

        public Task<List<AffiliateCandidate>> HuntAsync(
            string keywords,
            int maxResults,
            IList<string> platformIds,
            AffiliateSearchMode tikTokSearchMode,
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

            return HuntPlatformsAsync(
                keywords,
                maxResults,
                platformIds,
                tikTokSearchMode,
                cancellationToken,
                logAction,
                configManager,
                runningProfileName);
        }

        internal Task<List<AffiliateCandidate>> HuntTikTokPlatformAsync(
            string keywords,
            int maxResults,
            AffiliateSearchMode searchMode,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager,
            string runningProfileName)
        {
            return Task.Run(
                () => HuntCoreAsync(
                    keywords, maxResults, searchMode,
                    cancellationToken, logAction, configManager, runningProfileName),
                cancellationToken);
        }

        /// <summary>Tìm sản phẩm TikTok Shop như người mua (www.tiktok.com) — không cần Affiliate Creator.</summary>
        public Task<List<AffiliateCandidate>> HuntTikTokConsumerShopAsync(
            string keywords,
            int maxResults,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager,
            string runningProfileName)
        {
            return BrowserLock.WithLockAsync(
                ProfileScopedPaths.ResolveProfileName(runningProfileName),
                ct => HuntConsumerShopCoreAsync(
                    keywords,
                    maxResults,
                    ct,
                    logAction,
                    configManager,
                    runningProfileName),
                cancellationToken);
        }

        public async Task<List<AffiliateCandidate>> HuntWithCookieAsync(string keyword, string cookie)
        {
            var service = new TikTokApiService();
            try
            {
                return await service.SearchProductsAsync(keyword, cookie).ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }

        private const string MobileShopUserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

        private async Task<List<AffiliateCandidate>> HuntConsumerShopCoreAsync(
            string keywords,
            int maxResults,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager,
            string runningProfileName)
        {
            if (configManager == null)
            {
                throw new InvalidOperationException(
                    "Quét TikTok Shop cần profile Chrome trong tab Cài đặt (đăng nhập TikTok thường, không bắt buộc Affiliate).");
            }

            var settings = await configManager.LoadAsync().ConfigureAwait(false);
            var profile = ResolveRunningProfile(settings, runningProfileName, logAction);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Chưa có profile Chrome. Thêm profile trong Cài đặt và đăng nhập TikTok thủ công.");
            }

            profile = await configManager
                .EnsureProfileFingerprintAsync(settings, profile?.Name, logAction)
                .ConfigureAwait(false);

            var effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                ? (profile?.Name ?? "default")
                : runningProfileName.Trim();

            BrowserAutomation.EnsureLegacySessionMigratedForSharedProfile(profile, effectiveProfileName, logAction);

            var browser = new BrowserAutomation();
            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();
            var hasMobileGrid = false;
            var clickHarvestAttempted = false;

            try
            {
                logAction?.Invoke(
                    $"[Shop/TikTok] Tìm sản phẩm bán trên TikTok Shop (người mua) — profile «{effectiveProfileName}», từ khoá «{keywords}».");
                logAction?.Invoke("[Shop/TikTok] Mở www.tiktok.com (mobile UA + Chrome hiển thị, giống app điện thoại)…");

                await browser.LaunchAsync(
                        cancellationToken,
                        logAction,
                        effectiveProfileName,
                        profile,
                        headless: false,
                        BrowserPlatform.TikTok,
                        MobileShopUserAgent)
                    .ConfigureAwait(false);

                await browser.GotoMobileShopSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);
                await WaitForShopProductsAsync(browser, cancellationToken).ConfigureAwait(false);
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                await LogShopDiagnosticsAsync(browser, logAction).ConfigureAwait(false);

                hasMobileGrid = await browser.HasMobileShopGridAsync().ConfigureAwait(false);

                var deadline = DateTime.UtcNow.AddMinutes(3);
                var emptyRounds = 0;

                while (results.Count < maxResults && DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var snapshots = await ExtractShopProductsAsync(browser).ConfigureAwait(false);
                    if (snapshots.Count == 0)
                    {
                        snapshots = ExtractShopProductsFromCapturedJson(browser.GetShopSearchCapturedJson());
                    }

                    if (snapshots.Count == 0)
                    {
                        var html = await browser.GetPageContentSafeAsync().ConfigureAwait(false);
                        snapshots = ExtractShopProductsFromHtmlSource(html);
                    }

                    if (snapshots.Count == 0)
                    {
                        snapshots = await FetchConsumerShopWithSessionCookiesAsync(
                                browser,
                                keywords,
                                cancellationToken,
                                logAction)
                            .ConfigureAwait(false);
                    }

                    var domLinkCount = await browser.CountShopProductLinksAsync().ConfigureAwait(false);

                    if (snapshots.Count == 0)
                    {
                        emptyRounds++;
                        logAction?.Invoke(
                            $"[Shop/TikTok] Vòng {emptyRounds}: snapshot=0, link DOM={domLinkCount} — cuộn thêm…");

                        if (!clickHarvestAttempted && emptyRounds >= 2 && (hasMobileGrid || domLinkCount > 0))
                        {
                            clickHarvestAttempted = true;
                            logAction?.Invoke("[Shop/TikTok] Thử bấm từng thẻ sản phẩm (mobile) để lấy link…");
                            snapshots = await HarvestShopLinksByClickAsync(
                                    browser,
                                    Math.Min(maxResults - results.Count, 12),
                                    cancellationToken,
                                    logAction)
                                .ConfigureAwait(false);
                        }

                        if (snapshots.Count == 0 && emptyRounds >= 10)
                        {
                            logAction?.Invoke(
                                "[Shop/TikTok] Không đọc được sản phẩm. Thử đăng nhập TikTok thường cho profile hoặc đổi từ khoá.");
                            break;
                        }

                        await ScrollShopFeedAsync(browser, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    emptyRounds = 0;
                    foreach (var snap in snapshots)
                    {
                        if (results.Count >= maxResults)
                        {
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        var snapItem = snap ?? new CandidateSnapshot();
                        var productUrl = ToAbsoluteTikTokUrl(snapItem.VideoUrl);
                        if (string.IsNullOrWhiteSpace(productUrl))
                        {
                            continue;
                        }

                        if (!IsTikTokProductUrl(productUrl) &&
                            productUrl.IndexOf("/shop/", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        var dedupeKey = !string.IsNullOrWhiteSpace(snapItem.VideoId)
                            ? snapItem.VideoId.Trim()
                            : productUrl;
                        if (!seen.Add(dedupeKey))
                        {
                            continue;
                        }

                        var sellerName = !string.IsNullOrWhiteSpace(snapItem.Creator) && !IsIdLike(snapItem.Creator)
                            ? snapItem.Creator.Trim()
                            : string.Empty;
                        var productName = !string.IsNullOrWhiteSpace(snapItem.ProductName) && !IsIdLike(snapItem.ProductName)
                            ? snapItem.ProductName.Trim()
                            : (string.IsNullOrWhiteSpace(sellerName)
                                ? "TikTok Shop product"
                                : "TikTok Shop — " + sellerName);

                        var candidate = new AffiliateCandidate
                        {
                            SourceKeyword = keywords,
                            ProductName = productName,
                            Price = NormalizePrice(snapItem.PriceText),
                            ImageUrl = NormalizeImageUrl(snapItem.ImageUrl),
                            CommissionRate = NormalizeCommissionRate(snapItem.CommissionText),
                            Creator = sellerName,
                            VideoUrl = productUrl,
                            ProfileUrl = string.Empty,
                            Hashtags = string.Empty,
                            LinkedProduct = productName
                        };

                        var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                        candidate.SafetyScore = safetyResult.Score;
                        candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                            ? "Low risk"
                            : string.Join("; ", safetyResult.Reasons.Take(3));
                        results.Add(candidate);

                        logAction?.Invoke(
                            $"[Shop/TikTok] {results.Count}/{maxResults}: {candidate.ProductName} | {candidate.Price}");
                    }

                    if (results.Count < maxResults)
                    {
                        await ScrollShopFeedAsync(browser, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (results.Count == 0)
                {
                    logAction?.Invoke("[Shop/TikTok] Thử fetch HTML mobile (HTTP)…");
                    var httpSnapshots = await FetchConsumerShopViaMobileHttpAsync(
                            keywords,
                            cancellationToken,
                            logAction)
                        .ConfigureAwait(false);
                    foreach (var snap in httpSnapshots)
                    {
                        if (results.Count >= maxResults)
                        {
                            break;
                        }

                        var snapItem = snap ?? new CandidateSnapshot();
                        var productUrl = ToAbsoluteTikTokUrl(snapItem.VideoUrl);
                        if (string.IsNullOrWhiteSpace(productUrl) || !IsTikTokProductUrl(productUrl))
                        {
                            continue;
                        }

                        var dedupeKey = !string.IsNullOrWhiteSpace(snapItem.VideoId)
                            ? snapItem.VideoId.Trim()
                            : productUrl;
                        if (!seen.Add(dedupeKey))
                        {
                            continue;
                        }

                        results.Add(BuildConsumerShopCandidate(keywords, snapItem, safety));
                        logAction?.Invoke(
                            $"[Shop/TikTok/HTTP] {results.Count}/{maxResults}: {snapItem.ProductName}");
                    }
                }

                logAction?.Invoke($"[Shop/TikTok] Hoàn tất: {results.Count} sản phẩm.");
                return results;
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }

        private static async Task TrySetMobileShopViewportAsync(BrowserAutomation browser, Action<string> logAction)
        {
            var page = browser?.Page;
            if (page == null)
            {
                return;
            }

            try
            {
                await page.SetViewportSizeAsync(412, 915).ConfigureAwait(false);
                logAction?.Invoke("[Shop/TikTok] Viewport mobile 412×915.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop/TikTok] Không đổi viewport mobile: " + ex.Message);
            }
        }

        private async Task<List<AffiliateCandidate>> HuntPlatformsAsync(
            string keywords,
            int maxResultsPerSource,
            IList<string> platformIds,
            AffiliateSearchMode tikTokSearchMode,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager,
            string runningProfileName)
        {
            var wanted = NormalizePlatformIds(platformIds);
            if (wanted.Count == 0)
            {
                throw new InvalidOperationException("Chọn ít nhất một nền tảng (TikTok / Facebook / YouTube).");
            }

            var sources = _affiliateSources
                .Where(s => wanted.Contains(s.PlatformId, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (sources.Count == 0)
            {
                throw new InvalidOperationException("Không có strategy nào khớp nền tảng đã chọn.");
            }

            string ytDlpPath = null;
            if (wanted.Any(id => string.Equals(id, AffiliateSourceIds.YouTube, StringComparison.OrdinalIgnoreCase))
                && configManager != null)
            {
                try
                {
                    var settings = await configManager.LoadAsync().ConfigureAwait(false);
                    ytDlpPath = YtDlpToolResolver.Resolve(settings, logAction);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[YouTube] " + ex.Message);
                    sources = sources
                        .Where(s => !string.Equals(s.PlatformId, AffiliateSourceIds.YouTube, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            var context = new AffiliateHuntContext
            {
                TikTokSearchMode = tikTokSearchMode,
                ConfigManager = configManager,
                RunningProfileName = runningProfileName,
                Log = logAction,
                YtDlpPath = ytDlpPath
            };

            var merged = new List<AffiliateCandidate>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tasks = sources.Select(source => RunSourceHuntThrottledAsync(
                source, keywords, maxResultsPerSource, context, cancellationToken, merged, seenUrls));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(runningProfileName);
            var kwLabel = (keywords ?? string.Empty).Trim();
            foreach (var c in merged)
            {
                if (c == null)
                {
                    continue;
                }

                c.ProfileName = resolvedProfile;
                if (string.IsNullOrWhiteSpace(c.SourceKeyword) && !string.IsNullOrWhiteSpace(kwLabel))
                {
                    c.SourceKeyword = kwLabel;
                }
            }

            logAction?.Invoke($"[Affiliate] Tổng hợp {merged.Count} video từ {sources.Count} nền tảng cho «{keywords}» (nick «{resolvedProfile}»).");
            return merged;
        }

        private async Task RunSourceHuntThrottledAsync(
            IAffiliateSource source,
            string keyword,
            int limit,
            AffiliateHuntContext context,
            CancellationToken cancellationToken,
            List<AffiliateCandidate> merged,
            HashSet<string> seenUrls)
        {
            await _sourceHuntThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                context?.Log?.Invoke($"[{source.DisplayName}] Bắt đầu quét «{keyword}» (tối đa {limit})…");
                var batch = await source.HuntAsync(keyword, limit, context, cancellationToken).ConfigureAwait(false)
                            ?? new List<AffiliateCandidate>();

                lock (merged)
                {
                    foreach (var c in batch)
                    {
                        if (c == null)
                        {
                            continue;
                        }

                        var url = (c.VideoUrl ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            merged.Add(c);
                            continue;
                        }

                        if (seenUrls.Add(url))
                        {
                            if (string.IsNullOrWhiteSpace(c.ProfileName))
                            {
                                c.ProfileName = ProfileScopedPaths.ResolveProfileName(context?.RunningProfileName);
                            }

                            if (string.IsNullOrWhiteSpace(c.SourceKeyword) && !string.IsNullOrWhiteSpace(keyword))
                            {
                                c.SourceKeyword = keyword.Trim();
                            }

                            merged.Add(c);
                        }
                    }
                }

                context?.Log?.Invoke($"[{source.DisplayName}] Xong — {batch.Count} dòng (gộp hiện {merged.Count}).");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                context?.Log?.Invoke($"[{source.DisplayName}] Lỗi: {ex.Message}");
            }
            finally
            {
                _sourceHuntThrottle.Release();
            }
        }

        private static List<string> NormalizePlatformIds(IList<string> platformIds)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (platformIds == null)
            {
                return ordered;
            }

            foreach (var raw in platformIds)
            {
                var id = (raw ?? string.Empty).Trim();
                if (id.Length == 0 || !seen.Add(id))
                {
                    continue;
                }

                ordered.Add(id);
            }

            return ordered;
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
            if (searchMode == AffiliateSearchMode.Shop)
            {
                return await HuntShopViaSeleniumAsync(
                    keywords,
                    maxResults,
                    cancellationToken,
                    logAction,
                    configManager,
                    runningProfileName).ConfigureAwait(false);
            }

            AppSettings huntSettings = null;
            if (configManager != null)
            {
                huntSettings = await configManager.LoadAsync().ConfigureAwait(false);
            }

            if (ShouldHuntTikTokVideoViaRapidApi(huntSettings))
            {
                try
                {
                    return await HuntVideoViaRapidApiAsync(
                        keywords,
                        maxResults,
                        huntSettings,
                        cancellationToken,
                        logAction,
                        runningProfileName).ConfigureAwait(false);
                }
                catch (Exception ex) when (huntSettings?.AffiliateTikTokApiFallbackBrowser ?? true)
                {
                    logAction?.Invoke($"[Affiliate] RapidAPI lỗi ({ex.Message}) — chuyển sang Playwright…");
                }
            }
            else if (IsRapidApiHuntMode(huntSettings) && string.IsNullOrWhiteSpace(huntSettings?.TikTokRapidApiKey))
            {
                logAction?.Invoke("[Affiliate] Chế độ RapidAPI nhưng chưa có key — dùng Playwright.");
            }

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
                    logAction?.Invoke($"[Affiliate] Mở trình duyệt với profile «{effectiveProfileName}» (Playwright — săn Video).");
                }

                await browser.LaunchAsync(cancellationToken, logAction, effectiveProfileName, automationProfile, headless: false).ConfigureAwait(false);
                await browser.GotoVideoSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);

                var page = browser.Page;
                if (page == null)
                {
                    throw new InvalidOperationException("Browser page is not available.");
                }

                var deadline = DateTime.UtcNow.AddMinutes(2);
                var lastResultCount = -1;
                var stagnantRounds = 0;

                await WaitForVideoSearchAsync(browser, cancellationToken).ConfigureAwait(false);

                var emptyRounds = 0;
                while (results.Count < maxResults && DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var beforeCount = results.Count;
                    var snapshots = await ExtractVideoSearchResultsAsync(browser).ConfigureAwait(false);

                    if (snapshots.Count == 0)
                    {
                        emptyRounds++;
                        logAction?.Invoke($"[Affiliate] Vòng {emptyRounds}: chưa quét được sản phẩm/video nào. Cuộn thêm và đợi...");
                        if (emptyRounds >= 4)
                        {
                            logAction?.Invoke($"⚠️ Không quét được dữ liệu từ màn hình cho từ khóa «{keywords}». Có thể TikTok đang đổi giao diện hoặc chưa load xong.");
                            break;
                        }
                        await ScrollVideoSearchFeedAsync(browser, cancellationToken).ConfigureAwait(false);
                    }

                    for (var i = 0; i < snapshots.Count && results.Count < maxResults; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var snap = snapshots[i] ?? new CandidateSnapshot();

                        AffiliateCandidate candidate;
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

                    if (results.Count == lastResultCount)
                    {
                        stagnantRounds++;
                        if (stagnantRounds >= 6)
                        {
                            logAction?.Invoke($"[Affiliate] Không thêm video mới sau {stagnantRounds} lần cuộn — dừng ở {results.Count}/{maxResults}.");
                            break;
                        }
                    }
                    else
                    {
                        stagnantRounds = 0;
                        lastResultCount = results.Count;
                    }

                    if (results.Count == beforeCount)
                    {
                        await ScrollVideoSearchFeedAsync(browser, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await PerformStealthInteractionAsync(browser, cancellationToken, random).ConfigureAwait(false);
                    }
                }

                logAction?.Invoke($"[Affiliate] Deep extraction finished: {results.Count} item(s). Safety pre-check applied.");
                return results;
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }

        private static bool IsRapidApiHuntMode(AppSettings settings) =>
            settings != null &&
            string.Equals(
                (settings.AffiliateTikTokVideoHuntMode ?? string.Empty).Trim(),
                TikTokVideoHuntModes.RapidApi,
                StringComparison.OrdinalIgnoreCase);

        private static bool ShouldHuntTikTokVideoViaRapidApi(AppSettings settings) =>
            IsRapidApiHuntMode(settings) && !string.IsNullOrWhiteSpace(settings.TikTokRapidApiKey);

        private async Task<List<AffiliateCandidate>> HuntVideoViaRapidApiAsync(
            string keywords,
            int maxResults,
            AppSettings settings,
            CancellationToken cancellationToken,
            Action<string> logAction,
            string runningProfileName)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(runningProfileName);
            logAction?.Invoke($"[Affiliate] RapidAPI «{keywords}» — thu tối đa {maxResults} video, giữ top view/trend…");

            var service = new TikTokApiService();
            var results = await service.SearchVideosAsync(
                keywords,
                maxResults,
                settings.TikTokRapidApiKey,
                settings.TikTokRapidApiHost,
                logAction,
                cancellationToken).ConfigureAwait(false);

            foreach (var c in results ?? new List<AffiliateCandidate>())
            {
                if (c == null)
                {
                    continue;
                }

                c.ProfileName = profile;
                if (string.IsNullOrWhiteSpace(c.SourcePlatform))
                {
                    c.SourcePlatform = AffiliateSourceIds.TikTok;
                }
            }

            logAction?.Invoke($"[Affiliate] RapidAPI hoàn tất: {results?.Count ?? 0} video cho «{keywords}» @ {profile}.");
            return results ?? new List<AffiliateCandidate>();
        }

        private async Task<List<AffiliateCandidate>> HuntShopViaSeleniumAsync(
            string keywords,
            int maxResults,
            CancellationToken cancellationToken,
            Action<string> logAction,
            ConfigManager configManager,
            string runningProfileName)
        {
            if (configManager == null)
            {
                throw new InvalidOperationException(
                    "Săn Shop cần profile Chrome đã đăng nhập TikTok Affiliate. Chọn «Profile chạy» trên tab chính và chạy lại từ giao diện ứng dụng.");
            }

            var settings = await configManager.LoadAsync().ConfigureAwait(false);
            var profile = ResolveRunningProfile(settings, runningProfileName, logAction);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Chưa có profile Chrome trong tab Cài đặt. Thêm profile, bấm «Đăng nhập TikTok thủ công», đăng nhập xong rồi săn Shop lại.");
            }

            profile = await configManager
                .EnsureProfileFingerprintAsync(settings, profile?.Name, logAction)
                .ConfigureAwait(false);

            var effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                ? (profile?.Name ?? "default")
                : runningProfileName.Trim();

            BrowserAutomation.EnsureLegacySessionMigratedForSharedProfile(profile, effectiveProfileName, logAction);

            logAction?.Invoke(
                $"[Shop/Selenium] Headless Chrome + profile «{effectiveProfileName}» — Chợ Affiliate (cần tỉ lệ hoa hồng trên DOM).");
            logAction?.Invoke("[Shop/Selenium] Đang chờ lượt Chrome (đóng Chrome khác nếu chờ quá 2 phút)…");

            return await BrowserLock.WithLockAsync(
                    ProfileScopedPaths.ResolveProfileName(effectiveProfileName),
                    ct => Task.Run(
                        () => HuntShopViaSeleniumSync(
                            keywords,
                            maxResults,
                            ct,
                            logAction,
                            profile,
                            effectiveProfileName),
                        ct),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private List<AffiliateCandidate> HuntShopViaSeleniumSync(
            string keywords,
            int maxResults,
            CancellationToken cancellationToken,
            Action<string> logAction,
            AutomationProfile profile,
            string effectiveProfileName)
        {
            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();

            using (var driver = CreateShopHeadlessChromeDriver(profile, effectiveProfileName, logAction))
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(25))
                {
                    PollingInterval = TimeSpan.FromMilliseconds(350)
                };

                if (!NavigateShopAffiliateMarketplace(driver, wait, logAction, cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Không mở được trang Chợ Sản Phẩm Affiliate TikTok. Kiểm tra mạng/proxy hoặc đăng nhập lại profile.");
                }

                SeleniumShopWaitForPageReady(driver, wait);
                EnsureShopAffiliateLoggedIn(driver, wait, effectiveProfileName, logAction);

                logAction?.Invoke($"[Shop/Selenium] Tìm kiếm từ khóa: «{keywords}»");
                SeleniumShopSubmitKeywordSearch(driver, wait, keywords, logAction, cancellationToken);
                SeleniumShopWaitForSearchResults(driver, wait, cancellationToken);

                var deadline = DateTime.UtcNow.AddMinutes(3);
                var emptyRounds = 0;
                var stagnantRounds = 0;
                var lastSnapshotCount = -1;

                while (results.Count < maxResults && DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var snapshots = SeleniumExtractShopProducts(driver, logAction);
                    if (snapshots.Count == 0)
                    {
                        emptyRounds++;
                        logAction?.Invoke($"[Shop/Selenium] Vòng {emptyRounds}: chưa thấy thẻ sản phẩm có hoa hồng. Cuộn thêm...");
                        SeleniumShopScrollFeed(driver);
                        Thread.Sleep(1200);
                        if (emptyRounds >= 8)
                        {
                            logAction?.Invoke(
                                "[Shop/Selenium] Không trích xuất được sản phẩm. Kiểm tra đăng nhập Affiliate và từ khóa tìm kiếm.");
                            break;
                        }

                        continue;
                    }

                    emptyRounds = 0;
                    var addedThisRound = 0;

                    foreach (var snap in snapshots)
                    {
                        if (results.Count >= maxResults)
                        {
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        var snapItem = snap ?? new CandidateSnapshot();

                        var commission = NormalizeCommissionRate(snapItem.CommissionText);
                        if (string.IsNullOrWhiteSpace(snapItem.CommissionText) ||
                            string.Equals(commission, "N/A", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var productUrl = ToAbsoluteTikTokUrl(snapItem.VideoUrl);
                        if (string.IsNullOrWhiteSpace(productUrl))
                        {
                            continue;
                        }

                        if (!IsTikTokProductUrl(productUrl) &&
                            productUrl.IndexOf("affiliate.tiktok", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        var dedupeKey = !string.IsNullOrWhiteSpace(snapItem.VideoId)
                            ? snapItem.VideoId.Trim()
                            : productUrl;
                        if (!seen.Add(dedupeKey))
                        {
                            continue;
                        }

                        var sellerName = !string.IsNullOrWhiteSpace(snapItem.Creator) && !IsIdLike(snapItem.Creator)
                            ? snapItem.Creator.Trim()
                            : string.Empty;
                        var productName = !string.IsNullOrWhiteSpace(snapItem.ProductName) && !IsIdLike(snapItem.ProductName)
                            ? snapItem.ProductName.Trim()
                            : (string.IsNullOrWhiteSpace(sellerName)
                                ? "TikTok Shop product"
                                : "TikTok Shop — " + sellerName);

                        var candidate = new AffiliateCandidate
                        {
                            SourceKeyword = keywords,
                            ProductName = productName,
                            Price = NormalizePrice(snapItem.PriceText),
                            ImageUrl = NormalizeImageUrl(snapItem.ImageUrl),
                            CommissionRate = commission,
                            Creator = sellerName,
                            VideoUrl = productUrl,
                            ProfileUrl = string.Empty,
                            Hashtags = string.Empty,
                            LinkedProduct = productName
                        };

                        var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                        candidate.SafetyScore = safetyResult.Score;
                        candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                            ? "Low risk"
                            : string.Join("; ", safetyResult.Reasons.Take(3));
                        results.Add(candidate);
                        addedThisRound++;

                        logAction?.Invoke(
                            $"[Shop/Selenium] {results.Count}/{maxResults}: {candidate.ProductName} | {candidate.Price} | HH: {candidate.CommissionRate}");
                    }

                    if (results.Count >= maxResults)
                    {
                        break;
                    }

                    if (addedThisRound == 0)
                    {
                        emptyRounds++;
                        if (emptyRounds >= 6)
                        {
                            logAction?.Invoke(
                                "[Shop/Selenium] Có thẻ sản phẩm nhưng chưa thấy tỉ lệ hoa hồng trên DOM — thử từ khóa khác hoặc đăng nhập đúng tài khoản Creator Affiliate.");
                            break;
                        }
                    }
                    else
                    {
                        emptyRounds = 0;
                    }

                    if (snapshots.Count == lastSnapshotCount)
                    {
                        stagnantRounds++;
                        if (stagnantRounds >= 4)
                        {
                            logAction?.Invoke("[Shop/Selenium] Không tải thêm sản phẩm. Dừng sớm.");
                            break;
                        }
                    }
                    else
                    {
                        stagnantRounds = 0;
                        lastSnapshotCount = snapshots.Count;
                    }

                    SeleniumShopScrollFeed(driver);
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(900);
                }

                logAction?.Invoke($"[Shop/Selenium] Hoàn tất: {results.Count} sản phẩm (có hoa hồng).");
                return results;
            }
        }

        private static ChromeDriver CreateShopHeadlessChromeDriver(
            AutomationProfile profile,
            string effectiveProfileName,
            Action<string> logAction)
        {
            var userDataDir = ResolveSeleniumUserDataDirectory(profile, effectiveProfileName);
            Directory.CreateDirectory(userDataDir);
            logAction?.Invoke("[Shop/Selenium] user-data-dir: " + userDataDir);

            var proxyServer = BuildSeleniumProxyServerArgument(profile);
            if (!string.IsNullOrWhiteSpace(proxyServer))
            {
                logAction?.Invoke("[Shop/Selenium] proxy-server: " + proxyServer);
            }

            SeleniumChromeLaunchHelper.TryClearStaleProfileLocks(userDataDir, logAction);

            var options = new ChromeOptions();
            SeleniumChromeLaunchHelper.ApplyStableLaunchArguments(options, headless: true);
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument($"--user-data-dir={userDataDir}");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--mute-audio");
            options.AddArgument("--lang=en-US");
            if (!string.IsNullOrWhiteSpace(proxyServer))
            {
                options.AddArgument("--proxy-server=" + proxyServer);
            }

            var service = ChromeDriverService.CreateDefaultService();

            SeleniumChromeLaunchHelper.GuardProfileLaunch(userDataDir, logAction);

            var driver = SeleniumChromeLaunchHelper.CreateDriver(
                service,
                options,
                logAction);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(90);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(45);
            return driver;
        }

        private static bool NavigateShopAffiliateMarketplace(
            IWebDriver driver,
            WebDriverWait wait,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            foreach (var url in ShopAffiliateMarketplaceUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    logAction?.Invoke("[Shop/Selenium] Mở: " + url);
                    driver.Navigate().GoToUrl(url);
                    SeleniumShopWaitForPageReady(driver, wait);

                    var current = driver.Url ?? string.Empty;
                    if (IsAffiliateMarketplaceLandingUrl(current))
                    {
                        return true;
                    }

                    logAction?.Invoke("[Shop/Selenium] Redirect sang trang không phải Chợ Affiliate: " + current);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[Shop/Selenium] Không tải được " + url + ": " + ex.Message);
                }
            }

            return false;
        }

        private static bool IsAffiliateMarketplaceLandingUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (url.IndexOf("seller.tiktok", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (url.IndexOf("affiliate.tiktok", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (url.IndexOf("marketplace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 url.IndexOf("/connection/creator", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        internal static bool IsConsumerShopLandingUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (url.IndexOf("seller.tiktok", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return url.IndexOf("/shop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/search", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/view/product/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("shop.tiktok.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SeleniumShopWaitForPageReady(IWebDriver driver, WebDriverWait wait)
        {
            try
            {
                wait.Until(d =>
                {
                    try
                    {
                        var js = (IJavaScriptExecutor)d;
                        var state = js.ExecuteScript("return document.readyState") as string;
                        return string.Equals(state, "complete", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(state, "interactive", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch (WebDriverTimeoutException)
            {
                // Continue — DOM may still be usable.
            }

            Thread.Sleep(800);
        }

        private static void EnsureShopAffiliateLoggedIn(
            IWebDriver driver,
            WebDriverWait wait,
            string profileName,
            Action<string> logAction)
        {
            var url = driver.Url ?? string.Empty;
            var hasSessionCookies = SeleniumHasTikTokSessionCookies(driver);

            if (url.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("passport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("account/login", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw CreateShopLoginRequiredException(profileName);
            }

            var loginGateDetails = DescribeLoginGateMatches(driver);
            if (loginGateDetails.ShowsGate)
            {
                if (hasSessionCookies)
                {
                    logAction?.Invoke(
                        "[Shop/Selenium] Bỏ qua login gate ảo — profile đã có cookie session TikTok.");
                }
                else
                {
                    throw CreateShopLoginRequiredException(profileName);
                }
            }

            if (!hasSessionCookies)
            {
                logAction?.Invoke("[Shop/Selenium] Cảnh báo: chưa thấy cookie sessionid — vẫn thử quét nếu trang không chặn.");
            }
        }

        private static Exception CreateShopLoginRequiredException(string profileName)
        {
            var name = string.IsNullOrWhiteSpace(profileName) ? "đang chọn" : profileName.Trim();
            return new Exception(
                $"Chưa đăng nhập TikTok Affiliate cho profile «{name}». " +
                "Vào tab Cài đặt → chọn profile này → bấm «Đăng nhập TikTok thủ công», đăng nhập xong (mở affiliate.tiktok.com nếu cần), rồi săn Shop lại.");
        }

        private static bool SeleniumPageShowsLoginGate(IWebDriver driver) =>
            DescribeLoginGateMatches(driver).ShowsGate;

        private sealed class LoginGateProbeResult
        {
            public bool ShowsGate { get; set; }
        }

        private static string SeleniumDomAttr(IWebElement element, string attributeName)
        {
            if (element == null || string.IsNullOrWhiteSpace(attributeName))
            {
                return string.Empty;
            }

            try
            {
                return element.GetDomAttribute(attributeName) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static LoginGateProbeResult DescribeLoginGateMatches(IWebDriver driver)
        {
            var result = new LoginGateProbeResult();
            try
            {
                var loginLinks = driver.FindElements(
                    By.CssSelector(
                        "a[href*='/login'], [data-e2e='top-login-button']"));
                foreach (var el in loginLinks)
                {
                    if (el == null || !el.Displayed)
                    {
                        continue;
                    }

                    result.ShowsGate = true;
                }

                var body = driver.FindElement(By.TagName("body")).Text ?? string.Empty;
                if (body.IndexOf("Log in to continue", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.ShowsGate = true;
                }

                if (body.IndexOf("Sign up for TikTok", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.ShowsGate = true;
                }
            }
            catch
            {
                // ignored
            }

            return result;
        }

        private static bool SeleniumHasTikTokSessionCookies(IWebDriver driver)
        {
            if (driver == null)
            {
                return false;
            }

            try
            {
                foreach (var c in driver.Manage().Cookies.AllCookies)
                {
                    if (c == null || string.IsNullOrWhiteSpace(c.Name))
                    {
                        continue;
                    }

                    var domain = c.Domain ?? string.Empty;
                    if (domain.IndexOf("tiktok", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (SeleniumTikTokSessionCookieNames.Contains(c.Name))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static void SeleniumShopSubmitKeywordSearch(
            IWebDriver driver,
            WebDriverWait wait,
            string keywords,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var term = (keywords ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("Từ khoá tìm kiếm không được để trống.");
            }

            SeleniumShopDismissBlockingOverlays(driver, logAction);
            Thread.Sleep(600);

            if (SeleniumShopTryFillSearchInput(driver, wait, term, logAction))
            {
                logAction?.Invoke("[Shop/Selenium] Đã gửi từ khoá + Enter.");
                Thread.Sleep(1500);
                return;
            }

            logAction?.Invoke("[Shop/Selenium] Không thấy ô tìm kiếm — thử mở URL tìm kiếm trực tiếp…");
            if (SeleniumShopTryNavigateKeywordSearch(driver, wait, term, logAction, cancellationToken))
            {
                logAction?.Invoke("[Shop/Selenium] Đã mở trang tìm kiếm qua URL.");
                Thread.Sleep(1500);
                return;
            }

            throw new InvalidOperationException(
                "Không tìm thấy ô tìm kiếm sản phẩm trên Chợ Affiliate. TikTok có thể đã đổi giao diện — thử đăng nhập lại profile và mở affiliate.tiktok.com thủ công.");
        }

        private static void SeleniumShopDismissBlockingOverlays(IWebDriver driver, Action<string> logAction)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript(@"
try {
  window.scrollTo(0, 0);
  const labels = ['accept', 'agree', 'got it', 'ok', 'continue', 'đồng ý', 'tiếp tục', 'đã hiểu'];
  const nodes = Array.from(document.querySelectorAll('button, [role=""button""], a'));
  for (const n of nodes) {
    const t = (n.innerText || n.textContent || '').trim().toLowerCase();
    if (!t || t.length > 40) continue;
    if (labels.some(x => t.includes(x))) { try { n.click(); } catch(e) {} }
  }
} catch(e) {}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop/Selenium] Bỏ qua overlay: " + ex.Message);
            }
        }

        private static bool SeleniumShopTryFillSearchInput(
            IWebDriver driver,
            WebDriverWait wait,
            string keywords,
            Action<string> logAction)
        {
            SeleniumShopTryRevealSearchUi(driver, logAction);

            var searchInput = SeleniumFindShopSearchInput(driver, wait);
            if (searchInput == null)
            {
                searchInput = SeleniumFindShopSearchInputViaJs(driver);
            }

            if (searchInput == null)
            {
                return false;
            }

            try
            {
                new Actions(driver).MoveToElement(searchInput).Click().Perform();
            }
            catch
            {
                try
                {
                    searchInput.Click();
                }
                catch
                {
                    // continue with SendKeys
                }
            }

            try
            {
                searchInput.Clear();
            }
            catch
            {
                // contenteditable may not support Clear()
            }

            searchInput.SendKeys(keywords);
            searchInput.SendKeys(Keys.Enter);
            return true;
        }

        private static void SeleniumShopTryRevealSearchUi(IWebDriver driver, Action<string> logAction)
        {
            var revealSelectors = new[]
            {
                "[data-e2e*='search']",
                "button[aria-label*='Search']",
                "button[aria-label*='search']",
                "button[aria-label*='Tìm']",
                "[class*='search'] button",
                "[class*='Search'] button",
                "svg[class*='search']"
            };

            foreach (var css in revealSelectors)
            {
                try
                {
                    foreach (var el in driver.FindElements(By.CssSelector(css)))
                    {
                        if (el == null || !el.Displayed)
                        {
                            continue;
                        }

                        try
                        {
                            el.Click();
                            logAction?.Invoke("[Shop/Selenium] Bấm nút mở tìm kiếm: " + css);
                            Thread.Sleep(500);
                            return;
                        }
                        catch
                        {
                            // try next element
                        }
                    }
                }
                catch
                {
                    // try next selector
                }
            }
        }

        private static IWebElement SeleniumFindShopSearchInput(IWebDriver driver, WebDriverWait wait)
        {
            var shortWait = new WebDriverWait(driver, TimeSpan.FromSeconds(8))
            {
                PollingInterval = TimeSpan.FromMilliseconds(350)
            };

            foreach (var frame in driver.FindElements(By.CssSelector("iframe")))
            {
                try
                {
                    driver.SwitchTo().Frame(frame);
                    var inFrame = SeleniumFindShopSearchInputInCurrentContext(driver, shortWait);
                    if (inFrame != null)
                    {
                        return inFrame;
                    }
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    try
                    {
                        driver.SwitchTo().DefaultContent();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return SeleniumFindShopSearchInputInCurrentContext(driver, wait);
        }

        private static IWebElement SeleniumFindShopSearchInputInCurrentContext(IWebDriver driver, WebDriverWait wait)
        {
            var selectors = new[]
            {
                "input[type='search']",
                "input[placeholder*='Search']",
                "input[placeholder*='search']",
                "input[placeholder*='Tìm']",
                "input[placeholder*='tìm']",
                "input[placeholder*='sản phẩm']",
                "input[placeholder*='Sản phẩm']",
                "input[placeholder*='product']",
                "input[placeholder*='Product']",
                "input[placeholder*='keyword']",
                "input[placeholder*='Keyword']",
                "input[aria-label*='Search']",
                "input[aria-label*='search']",
                "input[aria-label*='Tìm']",
                "input[aria-label*='tìm']",
                "input[role='searchbox']",
                "input[role='combobox']",
                "textarea[placeholder*='Search']",
                "textarea[placeholder*='Tìm']",
                "[data-e2e*='search'] input",
                "[class*='search'] input[type='text']",
                "[class*='Search'] input[type='text']",
                "input[type='text']"
            };

            foreach (var css in selectors)
            {
                try
                {
                    var el = wait.Until(d =>
                    {
                        try
                        {
                            foreach (var candidate in d.FindElements(By.CssSelector(css)))
                            {
                                if (candidate != null &&
                                    candidate.Displayed &&
                                    candidate.Enabled &&
                                    SeleniumLooksLikeSearchField(candidate))
                                {
                                    return candidate;
                                }
                            }
                        }
                        catch (StaleElementReferenceException)
                        {
                            return null;
                        }

                        return null;
                    });
                    if (el != null)
                    {
                        return el;
                    }
                }
                catch
                {
                    // try next selector
                }
            }

            try
            {
                return driver.FindElement(
                    By.XPath(
                        "//input[contains(@placeholder,'Search') or contains(@placeholder,'search') or contains(@placeholder,'Tìm') or contains(@placeholder,'tìm') or contains(@placeholder,'sản phẩm') or contains(@placeholder,'Product')]" +
                        " | //textarea[contains(@placeholder,'Search') or contains(@placeholder,'Tìm') or contains(@placeholder,'tìm')]"));
            }
            catch
            {
                return null;
            }
        }

        private static bool SeleniumLooksLikeSearchField(IWebElement candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            try
            {
                var tag = (candidate.TagName ?? string.Empty).Trim();
                if (string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase))
                {
                    var type = (SeleniumDomAttr(candidate, "type"));
                    if (string.IsNullOrWhiteSpace(type))
                    {
                        type = "text";
                    }

                    type = type.Trim();
                    if (string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "radio", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                var blob = string.Join(
                    " ",
                    SeleniumDomAttr(candidate, "placeholder"),
                    SeleniumDomAttr(candidate, "aria-label"),
                    SeleniumDomAttr(candidate, "name"),
                    SeleniumDomAttr(candidate, "id"),
                    SeleniumDomAttr(candidate, "class"));
                if (blob.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    blob.IndexOf("tìm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    blob.IndexOf("product", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    blob.IndexOf("keyword", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    blob.IndexOf("sản phẩm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (string.Equals(SeleniumDomAttr(candidate, "type"), "search", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var role = SeleniumDomAttr(candidate, "role");
                return string.Equals(role, "searchbox", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(role, "combobox", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static IWebElement SeleniumFindShopSearchInputViaJs(IWebDriver driver)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                var handle = js.ExecuteScript(@"
function score(el) {
  if (!el) return -1;
  const st = window.getComputedStyle(el);
  if (!st || st.display === 'none' || st.visibility === 'hidden' || st.opacity === '0') return -1;
  const r = el.getBoundingClientRect();
  if (!r || r.width < 40 || r.height < 10) return -1;
  const ph = (el.getAttribute('placeholder')||'').toLowerCase();
  const aria = (el.getAttribute('aria-label')||'').toLowerCase();
  const cls = (el.className||'').toString().toLowerCase();
  const type = (el.getAttribute('type')||'').toLowerCase();
  const role = (el.getAttribute('role')||'').toLowerCase();
  let s = 0;
  const blob = ph + ' ' + aria + ' ' + cls + ' ' + type + ' ' + role;
  if (blob.includes('search') || blob.includes('tìm') || blob.includes('product') || blob.includes('keyword') || blob.includes('sản phẩm')) s += 50;
  if (type === 'search') s += 40;
  if (role === 'searchbox' || role === 'combobox') s += 30;
  if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') s += 10;
  if (r.top < 260) s += 8;
  return s;
}
let best = null, bestScore = 0;
const nodes = document.querySelectorAll('input, textarea, [contenteditable=""true""], [role=""searchbox""], [role=""combobox""]');
for (const el of nodes) {
  const s = score(el);
  if (s > bestScore) { bestScore = s; best = el; }
}
return bestScore >= 20 ? best : null;") as IWebElement;
                return handle;
            }
            catch
            {
                return null;
            }
        }

        private static bool SeleniumShopTryNavigateKeywordSearch(
            IWebDriver driver,
            WebDriverWait wait,
            string keywords,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var encoded = Uri.EscapeDataString(keywords);
            var urls = new[]
            {
                "https://affiliate.tiktok.com/connection/creator/product/marketplace?keyword=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/marketplace?keyword=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/product/marketplace?search=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/marketplace?search=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/product/marketplace?q=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/marketplace?q=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/product/marketplace?query=" + encoded,
                "https://affiliate.tiktok.com/connection/creator/marketplace?query=" + encoded
            };

            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    logAction?.Invoke("[Shop/Selenium] Thử URL: " + url);
                    driver.Navigate().GoToUrl(url);
                    SeleniumShopWaitForPageReady(driver, wait);
                    Thread.Sleep(1200);

                    var current = driver.Url ?? string.Empty;
                    if (current.IndexOf("affiliate", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (SeleniumFindShopSearchInput(driver, wait) != null ||
                        SeleniumFindShopSearchInputViaJs(driver) != null ||
                        SeleniumShopPageShowsProductSignals(driver))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[Shop/Selenium] URL tìm kiếm thất bại: " + ex.Message);
                }
            }

            return false;
        }

        private static bool SeleniumShopPageShowsProductSignals(IWebDriver driver)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                var count = js.ExecuteScript(
                    @"return document.querySelectorAll(
                        'a[href*=""/view/product/""], a[href*=""/product/""], [data-e2e*=""product""], [class*=""product-card""], [class*=""ProductCard""], [class*=""commission""]'
                    ).length;") as long?;
                return count.HasValue && count.Value > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void SeleniumShopWaitForSearchResults(
            IWebDriver driver,
            WebDriverWait wait,
            CancellationToken cancellationToken)
        {
            var shortWait = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
            {
                PollingInterval = TimeSpan.FromMilliseconds(400)
            };

            try
            {
                shortWait.Until(d =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var js = (IJavaScriptExecutor)d;
                    var count = js.ExecuteScript(
                        @"return document.querySelectorAll(
                            'a[href*=""/view/product/""], a[href*=""/product/""], [data-e2e*=""product""], [class*=""product-card""], [class*=""ProductCard""]'
                        ).length;") as long?;
                    return count.HasValue && count.Value > 0;
                });
            }
            catch (WebDriverTimeoutException)
            {
                // Marketplace may render commission rows without classic product links — continue.
            }

            Thread.Sleep(1000);
        }

        private static void SeleniumShopScrollFeed(IWebDriver driver)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript(
                    "window.scrollBy(0, Math.max(700, Math.floor(window.innerHeight * 0.9)));");
            }
            catch
            {
                // ignored
            }
        }

        private static List<CandidateSnapshot> SeleniumExtractShopProducts(IWebDriver driver, Action<string> logAction)
        {
            try
            {
                var js = (IJavaScriptExecutor)driver;
                var raw = js.ExecuteScript(ShopAffiliateExtractProductsJs) as string;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return new List<CandidateSnapshot>();
                }

                return JsonConvert.DeserializeObject<List<CandidateSnapshot>>(raw) ?? new List<CandidateSnapshot>();
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop/Selenium] Lỗi trích xuất DOM: " + ex.Message);
                return new List<CandidateSnapshot>();
            }
        }

        private const string ShopAffiliateExtractProductsJs =
            @"(function() {
                const safeJsonParse = (s) => { try { return JSON.parse(s); } catch (e) { return null; } };
                const pickCommissionFromText = (text) => {
                    if (!text) return '';
                    const t = String(text);
                    const pct = t.match(/\b(\d{1,3}(?:\.\d+)?)\s*%/);
                    if (pct) return pct[0].trim();
                    const money = t.match(/(?:commission|hoa hồng|earn)[:\s]*\$?\s*([\d,.]+)/i);
                    if (money) return money[0].trim();
                    const earn = t.match(/\$\s*[\d,.]+\s*(?:commission|earn)/i);
                    if (earn) return earn[0].trim();
                    return '';
                };

                const isProductNode = (n) => {
                    if (!n || typeof n !== 'object' || Array.isArray(n)) return false;
                    if (Object.prototype.hasOwnProperty.call(n, 'desc') && n.author && typeof n.author === 'object') return false;
                    const pid = n.product_id || n.productId;
                    const title = (n.title || n.product_name || n.productName || n.name || '').trim();
                    if (!title || title.length < 2) return false;
                    if (pid) return true;
                    const gid = n.id;
                    if (gid && (n.product_price || n.formatted_price || n.commission_rate || n.commissionRate || n.seller || n.shop)) return true;
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
                        if (Array.isArray(n)) { for (const v of n) visit(v, depth + 1); return; }
                        for (const k of Object.keys(n)) {
                            const v = n[k];
                            if (v && typeof v === 'object') visit(v, depth + 1);
                        }
                    };
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__) visit(window.__UNIVERSAL_DATA_FOR_REHYDRATION__, 0);
                    if (window.SIGI_STATE) visit(window.SIGI_STATE, 0);
                    return Array.from(found.values());
                };

                const pickPrice = (p) => {
                    if (!p) return '';
                    if (typeof p === 'string') return p.trim();
                    if (typeof p === 'object') {
                        const order = ['formatted_price','formattedPrice','real_price','price','product_price'];
                        for (const k of order) {
                            const v = p[k];
                            if (typeof v === 'string' && v.trim()) return v.trim();
                            if (typeof v === 'number' && v > 0) return String(v);
                        }
                    }
                    return '';
                };

                const pickImage = (n) => {
                    const buckets = [n.images, n.image, n.cover, n.product_image, n.thumbnail];
                    for (const b of buckets) {
                        if (!b) continue;
                        if (typeof b === 'string' && b.trim()) return b.trim();
                        if (Array.isArray(b) && b.length > 0) {
                            const item = b[0];
                            if (typeof item === 'string') return item;
                            if (item && item.url_list && item.url_list[0]) return item.url_list[0];
                        }
                    }
                    return '';
                };

                const pickSeller = (n) => {
                    const buckets = [n.seller, n.shop, n.shop_info, n.shopInfo];
                    for (const b of buckets) {
                        if (!b || typeof b !== 'object') continue;
                        const candidates = [b.shop_name, b.shopName, b.name, b.nickname];
                        for (const c of candidates) {
                            if (typeof c === 'string' && c.trim()) return c.trim();
                        }
                    }
                    return '';
                };

                const pickCommission = (n) => {
                    const direct = n.commission_rate || n.commissionRate || n.commission || n.commission_value || n.earn_commission;
                    if (typeof direct === 'string' && direct.trim()) return direct.trim();
                    if (typeof direct === 'number' && direct > 0) return direct + '%';
                    return pickCommissionFromText(JSON.stringify(n).slice(0, 800));
                };

                const items = [];
                const seen = new Set();

                for (const n of collectProducts()) {
                    const id = String(n.product_id || n.productId || n.id || '');
                    if (!id || seen.has(id)) continue;
                    const commission = pickCommission(n);
                    if (!commission) continue;
                    seen.add(id);
                    const title = (n.title || n.product_name || n.productName || n.name || '').trim();
                    items.push({
                        Source: 'affiliate-json',
                        VideoId: id,
                        ProductName: title,
                        Creator: pickSeller(n),
                        AuthorUniqueId: '',
                        PriceText: pickPrice(n.price || n.formatted_price || n.product_price),
                        CommissionText: commission,
                        ImageUrl: pickImage(n),
                        VideoUrl: 'https://www.tiktok.com/view/product/' + id
                    });
                }

                const cardSelectors = [
                    '[data-e2e*=""product""]',
                    '[class*=""product-card""]',
                    '[class*=""ProductCard""]',
                    '[class*=""product-item""]',
                    '[class*=""ProductItem""]',
                    'a[href*=""/view/product/""]',
                    'a[href*=""/product/""]'
                ].join(', ');

                const cards = Array.from(document.querySelectorAll(cardSelectors));
                for (const c of cards) {
                    const card = c.matches('a') ? (c.closest('[class*=""card""], [data-e2e], li, div') || c.parentElement || c) : c;
                    const link = card.matches('a') ? card : card.querySelector('a[href*=""/view/product/""], a[href*=""/product/""], a[href*=""affiliate""]');
                    const href = link ? (link.href || '') : '';
                    const cardText = (card.innerText || card.textContent || '').trim();
                    const commission = pickCommissionFromText(cardText);
                    if (!commission) continue;

                    const titleEl = card.querySelector('[data-e2e*=""title""], h3, h4, [class*=""title""], [class*=""Title""]');
                    const priceEl = card.querySelector('[data-e2e*=""price""], [class*=""price""], [class*=""Price""]');
                    const sellerEl = card.querySelector('[data-e2e*=""seller""], [data-e2e*=""shop""], [class*=""seller""], [class*=""shop""]');
                    const commEl = card.querySelector('[class*=""commission""], [class*=""Commission""], [data-e2e*=""commission""]');
                    const img = card.querySelector('img');

                    let title = titleEl ? (titleEl.textContent || '').trim() : '';
                    if (!title) {
                        const lines = cardText.split('\n').map(s => s.trim()).filter(Boolean);
                        title = lines.length ? lines[0] : 'Affiliate product';
                    }

                    const commText = commEl ? (commEl.textContent || '').trim() : commission;
                    const idMatch = href.match(/(?:view\/)?product\/(\d{6,})/i) || href.match(/product_id=(\d{6,})/i);
                    const id = idMatch ? idMatch[1] : ('dom-' + items.length + '-' + title.slice(0, 24));
                    const dedupe = id + '|' + commText;
                    if (seen.has(dedupe)) continue;
                    seen.add(dedupe);

                    items.push({
                        Source: 'affiliate-dom',
                        VideoId: idMatch ? idMatch[1] : '',
                        ProductName: title,
                        Creator: sellerEl ? (sellerEl.textContent || '').trim() : '',
                        AuthorUniqueId: '',
                        PriceText: priceEl ? (priceEl.textContent || '').trim() : '',
                        CommissionText: commText,
                        ImageUrl: img && img.src ? img.src : '',
                        VideoUrl: href || ''
                    });
                }

                return JSON.stringify(items);
            })();";

        private static string ResolveSeleniumUserDataDirectory(AutomationProfile profile, string activeProfileName) =>
            BrowserAutomation.GetSharedProfilePath(profile, activeProfileName);

        private static string SanitizeProfileNameForSelenium(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "default";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            var sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
        }

        private static string BuildSeleniumProxyServerArgument(AutomationProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProxyHost))
            {
                return string.Empty;
            }

            var host = profile.ProxyHost.Trim();
            var hasScheme = host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            host.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase);

            if (profile.ProxyPort > 0 && host.IndexOf(':') < 0)
            {
                host = host + ":" + profile.ProxyPort;
            }

            return hasScheme ? host : "http://" + host;
        }

        // ===== Deep video analysis (download → compress → Gemini) =====
        public Task<VideoDeepAnalysisResult> AnalyzeVideoContentAsync(
            string videoUrl,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            AffiliateCandidate profileContext = null,
            string storageRoot = null)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new ArgumentException("Video URL is required.", nameof(videoUrl));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return Task.Run(
                () => AnalyzeVideoContentCoreAsync(
                    videoUrl,
                    settings,
                    logAction,
                    cancellationToken,
                    profileContext,
                    storageRoot),
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
        private const int TikWmMinRequestIntervalMs = 1100;

        private static async Task<JObject> FetchTikWmDataAsync(
            string videoUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            string cleanUrl = (videoUrl ?? string.Empty).Trim();
            int qi = cleanUrl.IndexOf('?');
            if (qi >= 0) cleanUrl = cleanUrl.Substring(0, qi);

            logAction?.Invoke($"[TikWM] Fetch: {cleanUrl}");

            Exception lastError = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0)
                {
                    logAction?.Invoke("[TikWM] Thử lại sau giới hạn tốc độ…");
                    await Task.Delay(TikWmMinRequestIntervalMs, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    var apiBody = await RequestTikWmApiBodyAsync(cleanUrl, cancellationToken).ConfigureAwait(false);
                    var data = ParseTikWmDataResponse(apiBody);
                    return data;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    var msg = ex.Message ?? string.Empty;
                    if (attempt == 0
                        && (msg.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0
                            || msg.IndexOf("rate", StringComparison.OrdinalIgnoreCase) >= 0
                            || msg.IndexOf("too many", StringComparison.OrdinalIgnoreCase) >= 0
                            || msg.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }

                    throw;
                }
            }

            throw lastError ?? new Exception("TikWM API không phản hồi.");
        }

        private static async Task<string> RequestTikWmApiBodyAsync(string cleanUrl, CancellationToken cancellationToken)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var getUrl = "https://www.tikwm.com/api/?url=" + Uri.EscapeDataString(cleanUrl);
                using (var getResp = await http.GetAsync(getUrl, cancellationToken).ConfigureAwait(false))
                {
                    var getBody = await getResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (getResp.IsSuccessStatusCode && TryParseTikWmRoot(getBody, out var getRoot) && (getRoot["code"]?.Value<int?>() ?? -1) == 0)
                    {
                        return getBody;
                    }
                }

                using (var postContent = new FormUrlEncodedContent(new Dictionary<string, string> { { "url", cleanUrl } }))
                using (var postResp = await http.PostAsync("https://www.tikwm.com/api/", postContent, cancellationToken).ConfigureAwait(false))
                {
                    var postBody = await postResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!postResp.IsSuccessStatusCode)
                    {
                        throw new Exception($"TikWM API trả về HTTP {(int)postResp.StatusCode}. Body: {postBody}");
                    }

                    return postBody;
                }
            }
        }

        private static bool TryParseTikWmRoot(string apiBody, out JObject root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(apiBody))
            {
                return false;
            }

            try
            {
                root = JObject.Parse(apiBody);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static JObject ParseTikWmDataResponse(string apiBody)
        {
            if (!TryParseTikWmRoot(apiBody, out var root))
            {
                throw new Exception("TikWM trả JSON không hợp lệ.");
            }

            var code = root["code"]?.Value<int?>() ?? -1;
            if (code != 0)
            {
                var msg = root["msg"]?.ToString() ?? "(no msg)";
                throw new Exception($"TikWM API báo lỗi (code={code}): {msg}");
            }

            return root["data"] as JObject ?? throw new Exception("TikWM API không trả về data.");
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

            // Ưu tiên HD → chất lượng gốc cao nhất; fallback về play chuẩn
            var play = data["hdplay"]?.ToString();
            if (string.IsNullOrWhiteSpace(play)) play = data["play"]?.ToString();
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
            CancellationToken cancellationToken,
            AffiliateCandidate profileContext = null,
            string storageRoot = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var ffmpegExe = string.IsNullOrWhiteSpace(settings.FfmpegPath)
                ? ResolveFfmpegExecutablePath(settings, logAction)
                : settings.FfmpegPath.Trim();
            if (!File.Exists(ffmpegExe))
                throw new Exception("Lỗi: Không tìm thấy file ffmpeg.exe tại " + ffmpegExe);

            string tempDir;
            if (profileContext != null)
            {
                tempDir = AffiliateDeepDiveStore.GetDeepDiveSessionFolder(profileContext, storageRoot, create: true);
            }
            else
            {
                tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_downloads");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
            }

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
                ProcessCancellationHelper.WaitForExit(p, cancellationToken);
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
                if (profileContext != null)
                {
                    AffiliateDeepDiveStore.SaveCandidateSnapshot(
                        profileContext,
                        result,
                        storageRoot,
                        compressedMp4Path);
                }

                return result;
            }
            finally
            {
                if (profileContext == null)
                {
                    try
                    {
                        File.Delete(compressedMp4Path);
                    }
                    catch
                    {
                        // ignored
                    }
                }
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
                "• Tab «Cài đặt» → nhóm Veo/TTS/FFmpeg → bấm «⬇ Tải yt-dlp» (tải về cùng thư mục với exe),\r\n" +
                "  hoặc Browse chọn file yt-dlp.exe rồi bấm «Lưu cài đặt» (không bắt buộc nếu ô đã đúng).\r\n" +
                "• Hoặc đặt sẵn yt-dlp.exe vào thư mục chạy app:\r\n  " + dirExe + "\r\n\r\n" +
                "Đã thử các đường dẫn:\r\n  - " + tried);
        }

        private static string ResolveFfmpegExecutablePath(AppSettings settings, Action<string> logAction)
        {
            if (FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
            {
                logAction?.Invoke("[DeepDive] Dùng FFmpeg: " + toolkit.FfmpegExe);
                return toolkit.FfmpegExe;
            }

            throw new InvalidOperationException(
                "Chưa tìm thấy ffmpeg.exe — app sẽ tự tải vào Tools\\ffmpeg khi chạy Video reup hoặc bấm «⬇ Tải FFmpeg» trong Cài đặt.\r\n" +
                "Dự kiến: " + FfmpegToolkitService.GetBundledFfmpegPath());
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
            sb.AppendLine("SourceKeyword,ProductName,Category,Price,ImageUrl,CommissionRate,Creator,VideoUrl,ProfileUrl,Hashtags,LinkedProduct,PlayCount,LikeCount,CommentCount,ShareCount,CollectCount,DurationSeconds,CreateTimeUtc,MetricsCapturedAtUtc,VideoScript,VoiceoverTranscript,EngagementScore,EngagementReasons,LastDeepDiveError,LastMetricsError");
            foreach (var c in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.Append(TextHelper.EscapeCsv(c.SourceKeyword)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.ProductName)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.Category)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.Price)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.ImageUrl)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.CommissionRate)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.Creator)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.VideoUrl)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.ProfileUrl)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.Hashtags)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.LinkedProduct)).Append(',');
                sb.Append(c.PlayCount).Append(',');
                sb.Append(c.LikeCount).Append(',');
                sb.Append(c.CommentCount).Append(',');
                sb.Append(c.ShareCount).Append(',');
                sb.Append(c.CollectCount).Append(',');
                sb.Append(c.DurationSeconds).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.CreateTimeUtc == DateTime.MinValue ? string.Empty : c.CreateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.MetricsCapturedAtUtc == DateTime.MinValue ? string.Empty : c.MetricsCapturedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.VideoScript)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.VoiceoverTranscript)).Append(',');
                sb.Append(c.SafetyScore).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.SafetyRiskSummary)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.LastDeepDiveError)).Append(',');
                sb.Append(TextHelper.EscapeCsv(c.LastMetricsError)).AppendLine();
            }

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, TextFileEncoding.Utf8NoBom))
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
            using (var writer = new StreamWriter(stream, TextFileEncoding.Utf8NoBom))
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

        /// <summary>Cuộn feed tìm video — nhiều bước hơn shop để load thêm kết quả trước khi xếp hạng view.</summary>
        private static async Task ScrollVideoSearchFeedAsync(BrowserAutomation browser, CancellationToken cancellationToken)
        {
            var page = browser.Page;
            if (page == null)
            {
                return;
            }

            for (var i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await page.EvaluateAsync("window.scrollBy(0, Math.max(800, Math.floor(window.innerHeight * 0.92)));").ConfigureAwait(false);
                    await Task.Delay(1100, cancellationToken).ConfigureAwait(false);
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
                   url.IndexOf("/shop/pdp/", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
                    productLinkCount: document.querySelectorAll('a[href*=""/view/product/""], a[href*=""/shop/pdp/""]').length,
                    pdpLinkCount: document.querySelectorAll('a[href*=""/shop/pdp/""]').length,
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
                    pdpLinkCount = 0,
                    productHrefLoose = 0,
                    productCardCount = 0,
                    videoLinkCount = 0,
                    needsLogin = false,
                    rawTextHead = string.Empty
                });

                logAction.Invoke("[Shop diag] URL: " + diag.url);
                logAction.Invoke("[Shop diag] Title: " + diag.title);
                logAction.Invoke($"[Shop diag] UNIVERSAL_DATA={diag.hasUniversal}, SIGI_STATE={diag.hasSigi}, productLinks={diag.productLinkCount}, pdpLinks={diag.pdpLinkCount}, productHref*={diag.productHrefLoose}, productCards={diag.productCardCount}, videoLinks={diag.videoLinkCount}, needsLogin={diag.needsLogin}");
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

        private static AffiliateCandidate BuildConsumerShopCandidate(
            string keywords,
            CandidateSnapshot snapItem,
            SafetyScoreService safety)
        {
            var sellerName = !string.IsNullOrWhiteSpace(snapItem.Creator) && !IsIdLike(snapItem.Creator)
                ? snapItem.Creator.Trim()
                : string.Empty;
            var productName = !string.IsNullOrWhiteSpace(snapItem.ProductName) && !IsIdLike(snapItem.ProductName)
                ? snapItem.ProductName.Trim()
                : (string.IsNullOrWhiteSpace(sellerName)
                    ? "TikTok Shop product"
                    : "TikTok Shop — " + sellerName);
            var productUrl = ToAbsoluteTikTokUrl(snapItem.VideoUrl);

            var candidate = new AffiliateCandidate
            {
                SourceKeyword = keywords,
                ProductName = productName,
                Price = NormalizePrice(snapItem.PriceText),
                ImageUrl = NormalizeImageUrl(snapItem.ImageUrl),
                CommissionRate = NormalizeCommissionRate(snapItem.CommissionText),
                Creator = sellerName,
                VideoUrl = productUrl,
                ProfileUrl = string.Empty,
                Hashtags = string.Empty,
                LinkedProduct = productName
            };

            var safetyResult = safety.ScoreAffiliateCandidate(candidate);
            candidate.SafetyScore = safetyResult.Score;
            candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                ? "Low risk"
                : string.Join("; ", safetyResult.Reasons.Take(3));
            return candidate;
        }

        private static List<CandidateSnapshot> ExtractShopProductsFromCapturedJson(IReadOnlyList<string> jsonBodies)
        {
            var results = new List<CandidateSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (jsonBodies == null || jsonBodies.Count == 0)
            {
                return results;
            }

            foreach (var body in jsonBodies)
            {
                if (string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                try
                {
                    WalkJsonForShopProducts(JToken.Parse(body), results, seen, 0);
                }
                catch
                {
                    // ignored
                }
            }

            return results;
        }

        private static void WalkJsonForShopProducts(
            JToken node,
            List<CandidateSnapshot> results,
            HashSet<string> seen,
            int depth)
        {
            if (node == null || depth > 18)
            {
                return;
            }

            if (node is JObject obj)
            {
                var id = (obj["product_id"] ?? obj["productId"] ?? obj["id"])?.ToString()?.Trim();
                var title = (obj["title"] ?? obj["product_name"] ?? obj["productName"] ?? obj["name"])?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(id) &&
                    id.Length >= 5 &&
                    long.TryParse(id, out _) &&
                    !string.IsNullOrWhiteSpace(title) &&
                    title.Length >= 2 &&
                    seen.Add(id))
                {
                    var priceToken = obj["price"] ?? obj["formatted_price"] ?? obj["formattedPrice"] ?? obj["product_price"];
                    var price = priceToken?.Type == JTokenType.String
                        ? priceToken.ToString()
                        : priceToken?.ToString();
                    var canonical = (obj["canonical_url"] ?? obj["canonicalUrl"] ?? obj["product_url"] ?? obj["productUrl"])?.ToString()?.Trim();
                    var url = !string.IsNullOrWhiteSpace(canonical) && canonical.IndexOf("http", StringComparison.OrdinalIgnoreCase) >= 0
                        ? canonical
                        : "https://www.tiktok.com/view/product/" + id;
                    results.Add(new CandidateSnapshot
                    {
                        Source = "shop-network",
                        VideoId = id,
                        ProductName = title,
                        PriceText = price ?? string.Empty,
                        VideoUrl = url
                    });
                }

                foreach (var prop in obj.Properties())
                {
                    WalkJsonForShopProducts(prop.Value, results, seen, depth + 1);
                }

                return;
            }

            if (node is JArray arr)
            {
                foreach (var item in arr)
                {
                    WalkJsonForShopProducts(item, results, seen, depth + 1);
                }
            }
        }

        private static async Task<List<CandidateSnapshot>> FetchConsumerShopViaMobileHttpAsync(
            string keywords,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var encoded = Uri.EscapeDataString((keywords ?? string.Empty).Trim());
            var urls = new[]
            {
                "https://www.tiktok.com/search?q=" + encoded + "&t=shop",
                "https://www.tiktok.com/shop/s/" + encoded,
                "https://m.tiktok.com/search?q=" + encoded
            };

            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    logAction?.Invoke("[Shop/TikTok/HTTP] GET " + url);
                    var html = await FetchPageWithUserAgentAsync(url, MobileShopUserAgent, cancellationToken)
                        .ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(html))
                    {
                        continue;
                    }

                    var fromScript = ExtractShopProductsFromPageHtml(html);
                    if (fromScript.Count > 0)
                    {
                        logAction?.Invoke("[Shop/TikTok/HTTP] Đọc được " + fromScript.Count + " SP từ JSON nhúng.");
                        return fromScript;
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[Shop/TikTok/HTTP] Lỗi: " + ex.Message);
                }
            }

            return new List<CandidateSnapshot>();
        }

        private static List<CandidateSnapshot> ExtractShopProductsFromPageHtml(string html)
        {
            var results = new List<CandidateSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            foreach (var pattern in new[]
            {
                @"<script[^>]*id=""__UNIVERSAL_DATA_FOR_REHYDRATION__""[^>]*>(?<j>.*?)</script>",
                @"<script[^>]*id=""SIGI_STATE""[^>]*>(?<j>.*?)</script>"
            })
            {
                var m = Regex.Match(html, pattern, RegexOptions.Singleline);
                if (!m.Success)
                {
                    continue;
                }

                try
                {
                    WalkJsonForShopProducts(JToken.Parse(m.Groups["j"].Value), results, seen, 0);
                }
                catch
                {
                    // ignored
                }
            }

            return results;
        }

        private static async Task<List<CandidateSnapshot>> FetchConsumerShopWithSessionCookiesAsync(
            BrowserAutomation browser,
            string keywords,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var results = new List<CandidateSnapshot>();
            var cookieHeader = await browser.ExportCookieHeaderAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(cookieHeader))
            {
                logAction?.Invoke("[Shop/TikTok/Session] Không có cookie session.");
                return results;
            }

            var encoded = Uri.EscapeDataString((keywords ?? string.Empty).Trim());
            var urls = new[]
            {
                "https://www.tiktok.com/search?q=" + encoded + "&t=shop",
                "https://www.tiktok.com/shop/s/" + encoded
            };

            using (var handler = new HttpClientHandler { AllowAutoRedirect = true, UseCookies = false })
            using (var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(35) })
            {
                http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
                http.DefaultRequestHeaders.UserAgent.ParseAdd(MobileShopUserAgent);
                http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "vi-VN,vi;q=0.9,en;q=0.8");
                http.DefaultRequestHeaders.Referrer = new Uri("https://www.tiktok.com/");

                foreach (var url in urls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        logAction?.Invoke("[Shop/TikTok/Session] GET " + url);
                        var html = await http.GetStringAsync(url).ConfigureAwait(false);
                        var fromHtml = ExtractShopProductsFromHtmlSource(html);
                        if (fromHtml.Count == 0)
                        {
                            fromHtml = ExtractShopProductsFromPageHtml(html);
                        }

                        if (fromHtml.Count > 0)
                        {
                            logAction?.Invoke("[Shop/TikTok/Session] Đọc được " + fromHtml.Count + " SP.");
                            return fromHtml;
                        }
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke("[Shop/TikTok/Session] Lỗi: " + ex.Message);
                    }
                }
            }

            return results;
        }

        private static List<CandidateSnapshot> ExtractShopProductsFromHtmlSource(string html)
        {
            var results = new List<CandidateSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            foreach (Match m in Regex.Matches(
                         html,
                         @"https?://(?:www\.)?tiktok\.com/shop/pdp/[^""'\s<>]+",
                         RegexOptions.IgnoreCase))
            {
                var href = m.Value.Trim().TrimEnd('"', '\'', ',', ';');
                var idMatch = Regex.Match(href, @"/(\d{10,})(?:\?|$|""|'|\s)");
                if (!idMatch.Success)
                {
                    continue;
                }

                var id = idMatch.Groups[1].Value;
                if (!seen.Add(id))
                {
                    continue;
                }

                results.Add(new CandidateSnapshot
                {
                    Source = "html-pdp-abs",
                    VideoId = id,
                    ProductName = "TikTok Shop #" + id,
                    VideoUrl = href
                });
            }

            foreach (Match m in Regex.Matches(
                         html,
                         @"/shop/pdp/[^""'\s<>]+/(\d{10,})",
                         RegexOptions.IgnoreCase))
            {
                var id = m.Groups[1].Value;
                if (!seen.Add(id))
                {
                    continue;
                }

                var slugPath = m.Value.Trim().TrimEnd('"', '\'', ',', ';');
                results.Add(new CandidateSnapshot
                {
                    Source = "html-pdp-rel",
                    VideoId = id,
                    ProductName = "TikTok Shop #" + id,
                    VideoUrl = "https://www.tiktok.com" + slugPath
                });
            }

            foreach (Match m in Regex.Matches(
                         html,
                         @"/view/product/(\d{10,})",
                         RegexOptions.IgnoreCase))
            {
                var id = m.Groups[1].Value;
                if (!seen.Add(id))
                {
                    continue;
                }

                results.Add(new CandidateSnapshot
                {
                    Source = "html-view-product",
                    VideoId = id,
                    ProductName = "TikTok Shop #" + id,
                    VideoUrl = "https://www.tiktok.com/view/product/" + id
                });
            }

            return results;
        }

        private sealed class ShopClickTarget
        {
            public double X { get; set; }
            public double Y { get; set; }
            public string Href { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
        }

        private static async Task<List<CandidateSnapshot>> HarvestShopLinksByClickAsync(
            BrowserAutomation browser,
            int maxItems,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var page = browser?.Page;
            var results = new List<CandidateSnapshot>();
            if (page == null || maxItems <= 0)
            {
                return results;
            }

            const string findTargetsJs = @"() => {
                const out = [];
                const seen = new Set();
                const push = (el) => {
                    if (!el) return;
                    const r = el.getBoundingClientRect();
                    if (r.width < 48 || r.height < 48) return;
                    if (r.bottom < 72 || r.top > window.innerHeight - 16) return;
                    const key = Math.round(r.top) + ':' + Math.round(r.left) + ':' + Math.round(r.width);
                    if (seen.has(key)) return;
                    seen.add(key);
                    const href = el.href || (el.closest('a[href]') || {}).href || '';
                    const label = (el.getAttribute && (el.getAttribute('aria-label') || el.getAttribute('title'))) ||
                        (el.innerText || el.textContent || '');
                    out.push({
                        x: r.left + r.width / 2,
                        y: r.top + Math.min(r.height * 0.35, 100),
                        href: href || '',
                        label: String(label || '').trim().slice(0, 160)
                    });
                };
                for (const a of document.querySelectorAll('a[href*=""/shop/pdp/""], a[href*=""/view/product/""]')) push(a);
                for (const el of document.querySelectorAll('[data-e2e*=""product""], [class*=""ProductCard""], [class*=""product-card""]')) {
                    push(el.matches('a[href]') ? el : (el.querySelector('a[href]') || el));
                }
                return JSON.stringify(out.slice(0, 24));
            }";

            string json;
            try
            {
                json = await page.EvaluateAsync<string>(findTargetsJs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop/TikTok/Click] Không đọc được thẻ SP: " + ex.Message);
                return results;
            }

            var targets = string.IsNullOrWhiteSpace(json)
                ? new List<ShopClickTarget>()
                : (JsonConvert.DeserializeObject<List<ShopClickTarget>>(json) ?? new List<ShopClickTarget>());
            if (targets.Count == 0)
            {
                logAction?.Invoke("[Shop/TikTok/Click] Không thấy thẻ sản phẩm để bấm.");
                return results;
            }

            var searchUrl = page.Url ?? string.Empty;
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (results.Count >= maxItems)
                {
                    break;
                }

                var href = (target.Href ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(href) && IsTikTokProductUrl(href))
                {
                    var idFromHref = Regex.Match(href, @"(\d{8,})").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(idFromHref) && !seenIds.Add(idFromHref))
                    {
                        continue;
                    }

                    results.Add(new CandidateSnapshot
                    {
                        Source = "shop-click-href",
                        VideoId = idFromHref,
                        ProductName = string.IsNullOrWhiteSpace(target.Label) ? "TikTok Shop product" : target.Label,
                        VideoUrl = href
                    });
                    continue;
                }

                try
                {
                    await page.EvaluateAsync(
                        @"([x, y]) => {
                            const el = document.elementFromPoint(x, y);
                            if (el && el.scrollIntoView) el.scrollIntoView({ block: 'center', inline: 'center' });
                        }",
                        new object[] { target.X, target.Y }).ConfigureAwait(false);
                    await Task.Delay(450, cancellationToken).ConfigureAwait(false);
                    await page.Mouse.ClickAsync((float)target.X, (float)target.Y).ConfigureAwait(false);
                    await Task.Delay(2600, cancellationToken).ConfigureAwait(false);
                    var landed = page.Url ?? string.Empty;
                    if (IsTikTokProductUrl(landed))
                    {
                        var idMatch = Regex.Match(landed, @"(\d{8,})");
                        var id = idMatch.Success ? idMatch.Groups[1].Value : string.Empty;
                        if (string.IsNullOrWhiteSpace(id) || seenIds.Add(id))
                        {
                            results.Add(new CandidateSnapshot
                            {
                                Source = "shop-click-nav",
                                VideoId = id,
                                ProductName = string.IsNullOrWhiteSpace(target.Label) ? "TikTok Shop product" : target.Label,
                                VideoUrl = landed
                            });
                            logAction?.Invoke("[Shop/TikTok/Click] Link: " + landed);
                        }
                    }

                    if (!string.Equals(landed, searchUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        await page.GoBackAsync().ConfigureAwait(false);
                        await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
                        searchUrl = page.Url ?? searchUrl;
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[Shop/TikTok/Click] Bấm thẻ lỗi: " + ex.Message);
                }
            }

            return results;
        }

        private static async Task WaitForShopProductsAsync(BrowserAutomation browserPageWrapper, CancellationToken cancellationToken)
        {
            var page = browserPageWrapper?.Page;
            if (page == null)
            {
                return;
            }

            const string probe = @"() => {
                try {
                    if (document.querySelector('a[href*=""/shop/pdp/""], a[href*=""/view/product/""], [data-e2e*=""product""], [class*=""ProductCard""]')) {
                        return true;
                    }
                    const text = document.body ? document.body.innerText : '';
                    if (text && /đã bán|₫|\d[\d.,]*đ|giảm \d|cửa hàng/i.test(text)) {
                        const links = document.querySelectorAll('a[href*=""shop""], a[href*=""product""], a[href*=""pdp""]');
                        if (links.length >= 2) return true;
                        const prices = Array.from(document.querySelectorAll('span, div, p')).filter(el => {
                            const t = (el.textContent || '').trim();
                            return /^\d[\d.,]*\s*(?:đ|₫)$/.test(t);
                        });
                        if (prices.length >= 4) return true;
                    }
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__ || window.SIGI_STATE) {
                        if (text && /shop|sản phẩm|product|₫|đ/i.test(text)) {
                            const links = document.querySelectorAll('a[href*=""shop""], a[href*=""product""], a[href*=""pdp""]');
                            if (links.length >= 3) return true;
                        }
                    }
                } catch (e) {}
                return false;
            }";

            var deadline = DateTime.UtcNow.AddSeconds(20);
            var minStop = DateTime.UtcNow.AddSeconds(4);
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
                    // ignored
                }

                await Task.Delay(800, cancellationToken).ConfigureAwait(false);
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
                    if (window.__UNIVERSAL_DATA_FOR_REHYDRATION__) {
                        try {
                            const scope = window.__UNIVERSAL_DATA_FOR_REHYDRATION__.__DEFAULT_SCOPE__ || {};
                            for (const k of Object.keys(scope)) {
                                if (/shop|product|search/i.test(k)) visit(scope[k], 0);
                            }
                        } catch (e) {}
                    }
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

                const pickProductUrl = (n, id) => {
                    const direct = [
                        n.canonical_url, n.canonicalUrl, n.product_url, n.productUrl,
                        n.detail_url, n.detailUrl, n.url, n.link
                    ];
                    for (const u of direct) {
                        if (typeof u === 'string' && u.trim() && /shop|product|pdp/i.test(u)) {
                            return u.trim();
                        }
                    }
                    if (id && /^\d{5,}$/.test(String(id))) {
                        return 'https://www.tiktok.com/view/product/' + id;
                    }
                    return '';
                };

                const extractProductId = (href) => {
                    if (!href) return '';
                    const m = href.match(/\/(?:view\/)?product\/(\d{6,})/i) ||
                        href.match(/\/shop\/pdp\/[^/?#]+\/(\d{6,})/i) ||
                        href.match(/\/shop\/pdp\/(\d{6,})/i) ||
                        href.match(/\/pdp\/(\d{6,})/i) ||
                        href.match(/[?&]product_id=(\d{6,})/i);
                    return m ? m[1] : '';
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
                    const productUrl = pickProductUrl(n, id);
                    items.push({
                        Source: 'shop-json',
                        VideoId: id,
                        ProductName: title,
                        Creator: seller,
                        AuthorUniqueId: '',
                        PriceText: price,
                        CommissionText: commission,
                        ImageUrl: image,
                        VideoUrl: productUrl || ('https://www.tiktok.com/view/product/' + id)
                    });
                }

                if (items.length === 0) {
                    const cards = Array.from(document.querySelectorAll(
                        '[data-e2e=""product-item""], ' +
                        '[data-e2e*=""product-card""], ' +
                        '[data-e2e*=""shop-product""], ' +
                        '[class*=""product-card""], ' +
                        '[class*=""ProductCard""], ' +
                        'a[href*=""/view/product/""], ' +
                        'a[href*=""/shop/pdp/""]'
                    ));
                    for (const c of cards) {
                        const card = c.matches('a') ? (c.closest('[data-e2e], [class*=""product-card""], [class*=""ProductCard""]') || c) : c;
                        const link = card.matches('a') ? card : card.querySelector('a[href*=""/view/product/""], a[href*=""/shop/pdp/""], a[href*=""/shop/""]');
                        const href = link ? link.href : '';
                        if (!href) continue;
                        const id = extractProductId(href);
                        if (id && seen.has(id)) continue;
                        if (id) seen.add(id);
                        const titleEl = card.querySelector('[data-e2e=""product-title""], [data-e2e*=""title""], h3, h4, [class*=""title""]');
                        const priceEl = card.querySelector('[data-e2e=""product-price""], [data-e2e*=""price""], [class*=""price""], [class*=""Price""]');
                        const sellerEl = card.querySelector('[data-e2e*=""seller""], [data-e2e*=""shop""], [class*=""seller""], [class*=""shop""]');
                        const img = card.querySelector('img');
                        const cardText = (card.innerText || '');
                        const commMatch = cardText.match(/\d{1,3}\s*%/);
                        items.push({
                            Source: 'shop-dom',
                            VideoId: id,
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
                        const id = extractProductId(h);
                        if (!id) continue;
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
                            VideoUrl: h.indexOf('/shop/pdp/') >= 0 ? h : ('https://www.tiktok.com/view/product/' + id)
                        });
                    }
                }

                if (items.length === 0) {
                    const priceEls = Array.from(document.querySelectorAll('span, div, p, strong')).filter(el => {
                        const t = (el.textContent || '').trim();
                        return /^\d[\d.,]*\s*(?:đ|₫|k|K)$/.test(t) || /^\d[\d.,]+đ$/.test(t);
                    });
                    for (const priceEl of priceEls.slice(0, 80)) {
                        let card = priceEl.closest(
                            'a[href], [data-e2e*=""product""], [class*=""product""], [class*=""Product""], [class*=""Card""]'
                        );
                        if (!card) card = priceEl.parentElement && priceEl.parentElement.parentElement;
                        if (!card) continue;
                        const link = card.matches('a[href]') ? card : card.querySelector('a[href]');
                        const href = link ? link.href : '';
                        const id = extractProductId(href);
                        if (id && seen.has(id)) continue;
                        if (id) seen.add(id);
                        if (!href && !id) continue;
                        const titleEl = card.querySelector('h3, h4, [class*=""title""], [class*=""Title""], img[alt]');
                        let title = titleEl
                            ? ((titleEl.getAttribute && titleEl.getAttribute('alt')) || titleEl.textContent || '').trim()
                            : '';
                        if (!title) {
                            const lines = (card.innerText || '').split('\n').map(s => s.trim()).filter(Boolean);
                            title = lines.find(l => l.length > 8 && !/^\d/.test(l)) || '';
                        }
                        items.push({
                            Source: 'shop-mobile-price',
                            VideoId: id || '',
                            ProductName: title || ('TikTok Shop product #' + (id || items.length + 1)),
                            Creator: '',
                            AuthorUniqueId: '',
                            PriceText: (priceEl.textContent || '').trim(),
                            CommissionText: '',
                            ImageUrl: (card.querySelector('img') || {}).src || '',
                            VideoUrl: href || ('https://www.tiktok.com/view/product/' + id)
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

        /// <summary>Lấy tên, giá và danh sách ảnh từ link TikTok Shop / Shopee / trang sản phẩm.</summary>
        public async Task<ManualProductFetchResult> FetchProductInfoFromUrlAsync(
            string url,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var normalized = NormalizeManualProductUrl(url);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Link không hợp lệ. Hỗ trợ TikTok Shop, Shopee, TikTok video có anchor.");
            }

            logAction?.Invoke("[Manual] Đang tải trang sản phẩm…");
            const string mobileUa =
                "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1";
            var html = await FetchPageWithUserAgentAsync(normalized, mobileUa, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidOperationException("Không tải được nội dung trang.");
            }

            var result = new ManualProductFetchResult
            {
                ProductName = PickManualProductTitle(html),
                Price = PickManualProductPrice(html)
            };

            var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var img in ExtractManualProductImageUrls(html))
            {
                var norm = NormalizeImageUrl(img);
                if (!string.IsNullOrWhiteSpace(norm) &&
                    norm.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    !norm.Contains("avatar", StringComparison.OrdinalIgnoreCase))
                {
                    images.Add(norm);
                }
            }

            result.ImageUrls = images.Take(12).ToList();
            result.CustomerReviews = ProductReviewExtractor.ExtractTopCustomerReviews(html, 3).ToList();
            result.AffiliateLink = normalized;
            result.ProductId = TryExtractProductIdFromUrl(normalized);
            if (string.IsNullOrWhiteSpace(result.ProductName))
            {
                throw new InvalidOperationException("Không đọc được tên sản phẩm từ link.");
            }

            if (result.ImageUrls.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy ảnh sản phẩm trên trang.");
            }

            logAction?.Invoke(
                $"[Manual] OK: {result.ProductName} — {result.ImageUrls.Count} ảnh, {result.CustomerReviews.Count} review.");
            return result;
        }

        private static string TryExtractProductIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var m = Regex.Match(url, @"/(\d{8,})(?:[/?#]|$)");
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        private static string NormalizeManualProductUrl(string url)
        {
            var text = (url ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                text = "https://" + text;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                return string.Empty;
            }

            var host = uri.Host ?? string.Empty;
            if (host.IndexOf("tiktok", StringComparison.OrdinalIgnoreCase) >= 0 ||
                host.IndexOf("shopee", StringComparison.OrdinalIgnoreCase) >= 0 ||
                host.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            }

            return string.Empty;
        }

        private static string PickManualProductTitle(string html)
        {
            return ExtractHtmlMetaProperty(html, "og:title")
                   ?? ExtractHtmlMetaProperty(html, "twitter:title")
                   ?? ExtractHtmlTitleTag(html)
                   ?? string.Empty;
        }

        private static string PickManualProductPrice(string html)
        {
            var raw = ExtractHtmlMetaProperty(html, "product:price:amount")
                      ?? ExtractHtmlMetaProperty(html, "og:price:amount");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }

            var m = Regex.Match(html, @"(?:₫|VND|đ)\s*[\d.,]+|[\d.,]+\s*(?:₫|VND|đ)", RegexOptions.IgnoreCase);
            return m.Success ? m.Value.Trim() : "N/A";
        }

        private static string ExtractHtmlMetaProperty(string html, string property)
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
                    return DecodeHtmlEntities(m.Groups[1].Value);
                }
            }

            return null;
        }

        private static string ExtractHtmlTitleTag(string html)
        {
            var m = Regex.Match(html, "<title[^>]*>(?<t>[^<]+)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? DecodeHtmlEntities(m.Groups["t"].Value).Trim() : null;
        }

        private static string DecodeHtmlEntities(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return System.Net.WebUtility.HtmlDecode(raw).Trim();
        }

        private static IEnumerable<string> ExtractManualProductImageUrls(string html)
        {
            var list = new List<string>();
            var og = ExtractHtmlMetaProperty(html, "og:image");
            if (!string.IsNullOrWhiteSpace(og))
            {
                list.Add(og);
            }

            foreach (var pattern in new[]
            {
                @"<script[^>]*id=""__UNIVERSAL_DATA_FOR_REHYDRATION__""[^>]*>(?<j>.*?)</script>",
                @"<script[^>]*id=""SIGI_STATE""[^>]*>(?<j>.*?)</script>"
            })
            {
                var m = Regex.Match(html, pattern, RegexOptions.Singleline);
                if (!m.Success)
                {
                    continue;
                }

                try
                {
                    var root = JObject.Parse(m.Groups["j"].Value);
                    WalkJsonCollectImageUrls(root, list, 0);
                }
                catch
                {
                }
            }

            foreach (Match img in Regex.Matches(html, @"https?://[^\s""']+\.(?:jpg|jpeg|png|webp)", RegexOptions.IgnoreCase))
            {
                list.Add(img.Value);
            }

            return list;
        }

        private static void WalkJsonCollectImageUrls(JToken token, ICollection<string> sink, int depth)
        {
            if (token == null || depth > 16 || sink.Count >= 24)
            {
                return;
            }

            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    var name = prop.Name ?? string.Empty;
                    if (name.Equals("images", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("image", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("product_image", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("cover", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("thumbnail", StringComparison.OrdinalIgnoreCase))
                    {
                        CollectImageUrlsFromToken(prop.Value, sink);
                    }

                    WalkJsonCollectImageUrls(prop.Value, sink, depth + 1);
                }

                return;
            }

            if (token is JArray arr)
            {
                foreach (var child in arr)
                {
                    WalkJsonCollectImageUrls(child, sink, depth + 1);
                }
            }
        }

        private static void CollectImageUrlsFromToken(JToken token, ICollection<string> sink)
        {
            if (token == null)
            {
                return;
            }

            if (token.Type == JTokenType.String)
            {
                var s = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(s) && s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    sink.Add(s);
                }

                return;
            }

            if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JObject o)
                    {
                        var url = o["url"]?.ToString()
                                  ?? o["url_list"]?.First?.ToString()
                                  ?? o["urlList"]?.First?.ToString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            sink.Add(url);
                        }
                    }
                    else
                    {
                        CollectImageUrlsFromToken(item, sink);
                    }
                }

                return;
            }

            if (token is JObject jo)
            {
                var url = jo["url"]?.ToString()
                          ?? jo["url_list"]?.First?.ToString()
                          ?? jo["urlList"]?.First?.ToString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    sink.Add(url);
                }
            }
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
