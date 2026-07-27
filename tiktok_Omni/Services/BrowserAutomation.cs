using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    public enum BrowserPlatform
    {
        TikTok,
        Facebook,
        YouTube
    }

    public class BrowserAutomation
    {
        private const string ProfileFolderName = "browser_profile";
        private const string TikTokHomeUrl = "https://www.tiktok.com/";
        private const string TikTokLoginUrl = "https://www.tiktok.com/login";
        private const string TikTokForyouUrl = "https://www.tiktok.com/foryou";
        private const string IpLeakStopMessage = "Lộ IP thật, đã dừng để bảo vệ nick!";
        private static readonly TimeSpan ShortQueryTimeout = TimeSpan.FromSeconds(5);
        private const int CommentPanelWaitMs = 8000;
        private const string CommentInputReadySelector =
            "div[data-e2e='comment-input'] textarea, " +
            "div[data-e2e='comment-input'] div[contenteditable='true'], " +
            "div[contenteditable='true'][data-e2e='comment-input'], " +
            "div[contenteditable='true'][data-e2e='comment-text'], " +
            "div[role='textbox']";

        private readonly Random _random = new Random();

        private IPlaywright _playwright;
        private IBrowserContext _context;
        private IPage _page;
        private string _activeProfileName;
        private AutomationProfile _activeProfile;
        private readonly List<string> _shopSearchJsonBodies = new List<string>();
        private readonly List<string> _shopSearchResponseUrls = new List<string>();
        private EventHandler<IResponse> _shopSearchResponseHandler;
        /// <summary>Video id đã xem/loại trong phiên hiện tại (sau khi xem hoặc bỏ vì lỗi).</summary>
        private readonly HashSet<string> _warmupSessionExcludeVideoIds = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Video id đã mở từ search — chỉ chặn chọn lại trên lưới, không chặn lướt ↓ feed.</summary>
        private readonly HashSet<string> _warmupSessionOpenedVideoIds = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Video id từ job trước — chỉ chặn khi chọn trên lưới search, không chặn lướt ↓ feed.</summary>
        private readonly HashSet<string> _warmupPersistedExcludeVideoIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _warmupExcludedFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _warmupLockedVideoUrl;
        private string _warmupLockedVideoId;
        private bool _warmupInForyouMode;
        private string _warmupLastAuthor;
        private string _warmupLastCaption;
        private string _warmupLastFingerprint;
        private string _foryouNetworkVideoId;
        private DateTime _foryouNetworkVideoIdUtc;
        private EventHandler<IResponse> _foryouFeedResponseHandler;
        private EventHandler<IResponse> _likeApiResponseHandler;
        private TaskCompletionSource<bool> _likeApiResultTcs;
        private volatile bool _likeApiSucceeded;
        private volatile bool _likeApiAcceptingResponses;
        private string _likeApiCaptureVideoId;
        private bool _preserveExistingSession;

        public IPage Page => _page;

        public string WarmupLockedVideoId => _warmupLockedVideoId;
        public string WarmupLastAuthor => _warmupLastAuthor;
        public string WarmupLastCaption => _warmupLastCaption;

        public void ResetWarmupSession()
        {
            _warmupSessionExcludeVideoIds.Clear();
            _warmupSessionOpenedVideoIds.Clear();
            _warmupPersistedExcludeVideoIds.Clear();
            _warmupExcludedFingerprints.Clear();
            _warmupLockedVideoUrl = null;
            _warmupLockedVideoId = null;
            _warmupInForyouMode = false;
            _warmupLastAuthor = null;
            _warmupLastCaption = null;
            _warmupLastFingerprint = null;
            _foryouNetworkVideoId = null;
            _foryouNetworkVideoIdUtc = DateTime.MinValue;
            EndForyouFeedCapture();
            EndLikeApiCapture();
            _preserveExistingSession = false;
        }

        public void SeedWarmupExcludedVideoIds(IEnumerable<string> videoIds)
        {
            _warmupPersistedExcludeVideoIds.Clear();
            if (videoIds == null)
            {
                return;
            }

            foreach (var raw in videoIds)
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    _warmupPersistedExcludeVideoIds.Add(raw.Trim());
                }
            }
        }

        public void SeedWarmupExcludedFingerprints(IEnumerable<string> fingerprints)
        {
            if (fingerprints == null)
            {
                return;
            }

            foreach (var raw in fingerprints)
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    _warmupExcludedFingerprints.Add(raw.Trim());
                }
            }
        }

        private static string BuildWarmupContentFingerprint(string author, string caption)
        {
            return WarmupEngagementStore.BuildContentFingerprint(author, caption);
        }

        private bool IsWarmupSearchPickVideoIdExcluded(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return false;
            }

            var id = videoId.Trim();
            return _warmupSessionOpenedVideoIds.Contains(id)
                || _warmupSessionExcludeVideoIds.Contains(id)
                || _warmupPersistedExcludeVideoIds.Contains(id);
        }

        private void MarkWarmupVideoOpened(string videoId)
        {
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                _warmupSessionOpenedVideoIds.Add(videoId.Trim());
            }
        }

        private void MarkWarmupVideoOpenedFromPage()
        {
            MarkWarmupVideoOpened(_warmupLockedVideoId);
            var id = ExtractVideoIdFromUrl(_page?.Url ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(id))
            {
                MarkWarmupVideoOpened(id);
            }
        }

        private bool IsWarmupFeedContentExcluded(
            string videoId,
            string author,
            string caption,
            bool checkPersistedVideoHistory = true)
        {
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                var id = videoId.Trim();
                if (_warmupSessionExcludeVideoIds.Contains(id))
                {
                    return true;
                }

                if (checkPersistedVideoHistory && _warmupPersistedExcludeVideoIds.Contains(id))
                {
                    return true;
                }
            }

            if (_warmupInForyouMode)
            {
                var fingerprint = BuildWarmupContentFingerprint(author, caption);
                if (!string.IsNullOrWhiteSpace(fingerprint) && _warmupExcludedFingerprints.Contains(fingerprint))
                {
                    return true;
                }
            }

            return false;
        }

        private void ExcludeWarmupContent(string videoId, string author, string caption)
        {
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                _warmupSessionExcludeVideoIds.Add(videoId.Trim());
            }

            var fingerprint = BuildWarmupContentFingerprint(author, caption);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                _warmupExcludedFingerprints.Add(fingerprint);
            }
        }

        public void ExcludeCurrentWarmupVideo()
        {
            ExcludeWarmupContent(_warmupLockedVideoId, _warmupLastAuthor, _warmupLastCaption);

            var id = ExtractVideoIdFromUrl(_page?.Url ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(id))
            {
                _warmupSessionExcludeVideoIds.Add(id);
            }
        }

        public void ExcludeWarmupVideoId(string videoId)
        {
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                _warmupSessionExcludeVideoIds.Add(videoId.Trim());
            }
        }

        public async Task LaunchAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            string runningProfileName = null,
            AutomationProfile profile = null,
            bool headless = false,
            BrowserPlatform platform = BrowserPlatform.TikTok,
            string userAgentOverride = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_context != null)
            {
                return;
            }

            logAction?.Invoke("Starting Playwright browser (persistent profile)...");

            try
            {
                var rawProfileName = string.IsNullOrWhiteSpace(runningProfileName) ? profile?.Name : runningProfileName;
                _activeProfileName = SanitizeProfileName(rawProfileName);
                _activeProfile = profile;
                var profileDir = GetPlatformUserDataPath(_activeProfile, _activeProfileName, platform);
                Directory.CreateDirectory(profileDir);
                var hasCookieStore = ProfileHasCookieStore(profileDir);
                logAction?.Invoke(
                    $"Launching profile '{_activeProfileName}' with user data path: {profileDir} (cookiesOnDisk={hasCookieStore})");

                // Nếu thư mục mới CHƯA có session (do user từng login bằng nút Selenium-login cũ ở folder khác),
                // copy toàn bộ data từ folder cũ qua để khỏi bắt đăng nhập lại.
                MigrateLegacyProfileFolderIfNeeded(profileDir, _activeProfileName, rawProfileName, logAction);
                hasCookieStore = ProfileHasCookieStore(profileDir);
                if (!hasCookieStore)
                {
                    logAction?.Invoke(
                        "[LIVE] Chưa thấy cookie TikTok trên disk — nếu đã login ở Cài đặt, chọn đúng profile «" +
                        _activeProfileName +
                        "» rồi đăng nhập lại (cùng User-Agent với Warmup).");
                }

                var isMobileShopUa = !string.IsNullOrWhiteSpace(userAgentOverride) &&
                    userAgentOverride.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0;

                // Session đã login ở Cài đặt (Selenium: UA Chrome mặc định + maximize).
                // Warmup ép User-Agent/viewport khác → TikTok hủy cookie và bắt login lại.
                var preserveExistingSession = hasCookieStore && !isMobileShopUa;

                var resolvedUserAgent = !string.IsNullOrWhiteSpace(userAgentOverride)
                    ? userAgentOverride
                    : (string.IsNullOrWhiteSpace(_activeProfile?.UserAgent) ? null : _activeProfile.UserAgent);

                if (preserveExistingSession)
                {
                    resolvedUserAgent = null;
                    logAction?.Invoke(
                        "[LIVE] Giữ session đã login (Cài đặt): không ghi đè User-Agent/viewport — tránh TikTok bắt đăng nhập lại.");
                }
                else if (!string.IsNullOrWhiteSpace(resolvedUserAgent))
                {
                    logAction?.Invoke($"Profile '{_activeProfileName}' using fixed User-Agent.");
                }
                else
                {
                    logAction?.Invoke($"Profile '{_activeProfileName}' has no User-Agent override. Browser default will be used.");
                }

                _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
                var viewport = ResolveViewportForProfile(_activeProfile, _activeProfileName);
                if (isMobileShopUa)
                {
                    viewport = (412, 915);
                }

                var languages = isMobileShopUa
                    ? (primary: "vi-VN", secondary: "vi")
                    : ResolveLanguagesForProfile(_activeProfileName);
                var fontSet = ResolveFontsForProfile(_activeProfileName);
                var webGl = ResolveWebGlProfile(_activeProfileName);
                var hardwareConcurrency = ResolveHardwareConcurrencyForProfile(_activeProfileName);
                var browserArgs = new List<string>
                {
                    "--disable-blink-features=AutomationControlled",
                    "--disable-infobars",
                    "--lang=vi-VN"
                };
                if (preserveExistingSession)
                {
                    browserArgs.Add("--start-maximized");
                }

                var launchOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = headless,
                    ChromiumSandbox = true,
                    // null = cửa sổ thật (khớp Selenium --start-maximized); có cookie thì không ép viewport.
                    ViewportSize = preserveExistingSession
                        ? null
                        : new ViewportSize { Width = viewport.width, Height = viewport.height },
                    Locale = "vi-VN",
                    IsMobile = isMobileShopUa ? true : (bool?)null,
                    HasTouch = isMobileShopUa ? true : (bool?)null,
                    DeviceScaleFactor = isMobileShopUa ? 3f : (float?)null,
                    UserAgent = resolvedUserAgent,
                    IgnoreDefaultArgs = new[] { "--enable-automation" },
                    Args = browserArgs.ToArray()
                };

                var proxyServer = BuildProxyServer(_activeProfile);
                if (!string.IsNullOrWhiteSpace(proxyServer))
                {
                    launchOptions.Proxy = new Proxy
                    {
                        Server = proxyServer,
                        Username = string.IsNullOrWhiteSpace(_activeProfile.ProxyUser) ? null : _activeProfile.ProxyUser,
                        Password = string.IsNullOrWhiteSpace(_activeProfile.ProxyPass) ? null : _activeProfile.ProxyPass
                    };
                    logAction?.Invoke($"Launching with proxy server: {proxyServer}");
                }

                // Mobile shop hunt: bundled Chromium renders mobile layout more reliably than desktop Chrome.
                var channelAttempts = isMobileShopUa
                    ? new[]
                    {
                        (null, "Playwright Chromium"),
                        ("chrome", "Google Chrome"),
                        ("msedge", "Microsoft Edge")
                    }
                    : new[]
                    {
                        ("chrome", "Google Chrome"),
                        ("msedge", "Microsoft Edge"),
                        (null, "Playwright Chromium")
                    };

                Exception lastChannelError = null;
                for (var ci = 0; ci < channelAttempts.Length; ci++)
                {
                    var channel = channelAttempts[ci].Item1;
                    var label = channelAttempts[ci].Item2;
                    launchOptions.Channel = channel;
                    try
                    {
                        logAction?.Invoke($"Starting browser: {label}...");
                        _context = await _playwright.Chromium.LaunchPersistentContextAsync(profileDir, launchOptions).ConfigureAwait(false);
                        lastChannelError = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastChannelError = ex;
                        logAction?.Invoke($"[LIVE] Could not start {label}: {ex.Message}");
                    }
                }

                if (_context == null)
                {
                    throw lastChannelError ?? new InvalidOperationException("No browser channel could be started.");
                }

                _page = _context.Pages.Count > 0
                    ? _context.Pages[0]
                    : await _context.NewPageAsync().ConfigureAwait(false);

                // Chỉ ẩn webdriver khi đã có session — không đổi languages/WebGL/viewport fingerprint.
                await _context.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });").ConfigureAwait(false);
                if (preserveExistingSession)
                {
                    logAction?.Invoke("[LIVE] Stealth nhẹ (giữ fingerprint trình duyệt thật vì đã có cookie login).");
                }
                else
                {
                    await _context.AddInitScriptAsync($"Object.defineProperty(navigator, 'languages', {{ get: () => ['{languages.primary}','{languages.secondary}'] }});").ConfigureAwait(false);
                    await _context.AddInitScriptAsync($"Object.defineProperty(navigator, 'hardwareConcurrency', {{ get: () => {hardwareConcurrency} }});").ConfigureAwait(false);
                    await _context.AddInitScriptAsync(BuildFontStealthScript(fontSet)).ConfigureAwait(false);
                    await _context.AddInitScriptAsync(BuildWebGlStealthScript(webGl.vendor, webGl.renderer)).ConfigureAwait(false);
                    await _context.AddInitScriptAsync(BuildPluginsScriptForProfile(_activeProfileName)).ConfigureAwait(false);
                    await _context.AddInitScriptAsync(
                        isMobileShopUa
                            ? "Object.defineProperty(navigator, 'platform', { get: () => 'iPhone' });"
                            : "Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });").ConfigureAwait(false);
                    logAction?.Invoke(isMobileShopUa
                        ? $"Stealth enabled (mobile shop). Viewport: {viewport.width}x{viewport.height}. Locale: vi-VN."
                        : $"Stealth enabled. Viewport: {viewport.width}x{viewport.height}. Languages: {languages.primary}, {languages.secondary}. Fonts: {fontSet.Length}. WebGL: {webGl.vendor}. HW threads: {hardwareConcurrency}");
                }

                _preserveExistingSession = preserveExistingSession;
                await VerifyProxyIpAsync(_activeProfile, cancellationToken, logAction).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await CloseAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Failed to start Playwright browser. If you haven't installed Playwright browser binaries yet, run the Playwright install step and try again. " +
                    "Original error: " + ex.Message,
                    ex);
            }

            logAction?.Invoke("Browser ready.");
        }

        /// <summary>
        /// Thu nhỏ cửa sổ trình duyệt xuống Taskbar qua DevTools Protocol.
        /// Người dùng vẫn có thể mở lên từ Taskbar để giải CAPTCHA bằng tay.
        /// </summary>
        public async Task MinimizeAsync(Action<string> logAction = null)
        {
            try
            {
                if (_context == null || _page == null)
                {
                    return;
                }

                var cdp = await _context.NewCDPSessionAsync(_page).ConfigureAwait(false);
                var resp = await cdp.SendAsync("Browser.getWindowForTarget").ConfigureAwait(false);
                if (resp == null || !resp.HasValue)
                {
                    return;
                }

                if (!resp.Value.TryGetProperty("windowId", out var idElement))
                {
                    return;
                }

                var windowId = idElement.GetInt32();
                var args = new Dictionary<string, object>
                {
                    ["windowId"] = windowId,
                    ["bounds"] = new Dictionary<string, object>
                    {
                        ["windowState"] = "minimized"
                    }
                };
                await cdp.SendAsync("Browser.setWindowBounds", args).ConfigureAwait(false);
                logAction?.Invoke("[Browser] Đã thu nhỏ cửa sổ xuống Taskbar.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Browser] Không thu nhỏ được cửa sổ: " + ex.Message);
            }
        }

        /// <summary>
        /// Đăng nhập TikTok tương tác bằng Playwright (cùng engine/profile với Warmup) —
        /// tránh cookie Selenium không dùng được khi Warmup mở lại.
        /// </summary>
        public async Task<TikTokAccountSnapshot> RunInteractiveTikTokLoginAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            string runningProfileName,
            AutomationProfile profile)
        {
            ResetWarmupSession();
            try
            {
                await LaunchAsync(
                    cancellationToken,
                    logAction,
                    runningProfileName,
                    profile).ConfigureAwait(false);

                await EnsureLoggedInAsync(cancellationToken, logAction, forceOpenLoginPage: true)
                    .ConfigureAwait(false);

                logAction?.Invoke("[LOGIN] Đăng nhập OK — đang đọc thông tin tài khoản...");
                var snapshot = await TryExtractTikTokAccountSnapshotAsync(cancellationToken, logAction)
                    .ConfigureAwait(false);

                // Cho Chrome kịp ghi cookie xuống disk trước khi đóng context.
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                return snapshot;
            }
            finally
            {
                await CloseAsync().ConfigureAwait(false);
            }
        }

        public async Task EnsureLoggedInAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            bool forceOpenLoginPage = false)
        {
            await LaunchAsync(cancellationToken, logAction).ConfigureAwait(false);

            if (forceOpenLoginPage)
            {
                logAction?.Invoke("[LOGIN] Mở thẳng trang đăng nhập TikTok...");
                await SafeGotoAsync(TikTokLoginUrl, cancellationToken).ConfigureAwait(false);
                await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Thử khôi phục session thật (UI), không tin cookie cũ trên disk.
                logAction?.Invoke("[LOGIN] Kiểm tra phiên TikTok (For You / UI)...");
                if (await TryRecoverSessionViaForyouAsync(cancellationToken, logAction).ConfigureAwait(false))
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (await IsLoggedInAsync(cancellationToken).ConfigureAwait(false))
                {
                    logAction?.Invoke("TikTok session detected. Already logged in.");
                    return;
                }

                logAction?.Invoke(
                    "[LOGIN] Chưa đăng nhập trên UI — mở trang login. " +
                    "(Cookie cũ trên disk không đủ; hãy đăng nhập trong cửa sổ này, cùng profile Warmup.)");
                await SafeGotoAsync(TikTokLoginUrl, cancellationToken).ConfigureAwait(false);
            }

            var deadline = DateTime.UtcNow.AddMinutes(8);
            // Chế độ nút Cài đặt: đừng nhảy For You quá sớm — người dùng cần thời gian trên /login.
            var nextForyouProbeUtc = DateTime.UtcNow.AddSeconds(forceOpenLoginPage ? 120 : 35);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

                SyncToPrimaryTikTokPage(logAction, preferTikTokLoginTab: forceOpenLoginPage);

                if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new CaptchaDetectedException();
                }

                if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new CheckpointDetectedException("TikTok checkpoint detected while waiting for login.");
                }

                if (await IsLoggedInAsync(cancellationToken).ConfigureAwait(false))
                {
                    logAction?.Invoke("Login detected. Continuing automation...");
                    return;
                }

                if (!forceOpenLoginPage &&
                    await IsLoggedInOnAnyOpenTiktokPageAsync(cancellationToken, logAction).ConfigureAwait(false))
                {
                    logAction?.Invoke("Login detected (tab TikTok khác). Continuing automation...");
                    return;
                }

                var urlNow = _page.Url ?? string.Empty;
                if (DateTime.UtcNow >= nextForyouProbeUtc &&
                    urlNow.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    urlNow.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nextForyouProbeUtc = DateTime.UtcNow.AddSeconds(40);
                    if (await TryRecoverSessionViaForyouAsync(cancellationToken, logAction).ConfigureAwait(false))
                    {
                        logAction?.Invoke("Đã phát hiện đăng nhập sau khi mở For You.");
                        return;
                    }
                }
            }

            throw new InvalidOperationException(
                "Hết thời gian chờ đăng nhập TikTok. Gợi ý: thử QR lại hoặc chuyển sang email/SĐT trên trang TikTok; cài Chrome/Edge; tắt proxy; thử profile mới.");
        }

        /// <summary>
        /// After mobile confirms QR, the desktop tab sometimes stays on /login. Opening For You forces a navigation that exposes cookies / feed UI.
        /// </summary>
        private async Task<bool> TryRecoverSessionViaForyouAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            try
            {
                logAction?.Invoke("[LOGIN] Thử mở For You để kiểm tra phiên...");
                if (!await TryGotoSoftAsync(TikTokForyouUrl, cancellationToken, logAction).ConfigureAwait(false) &&
                    !await TryGotoSoftAsync(TikTokHomeUrl, cancellationToken, logAction).ConfigureAwait(false))
                {
                    logAction?.Invoke("[LOGIN] For You/Home bị chặn HTTP — sẽ mở trang đăng nhập.");
                    return false;
                }

                await Task.Delay(2500, cancellationToken).ConfigureAwait(false);
                SyncToPrimaryTikTokPage(logAction);

                if (await IsLoggedInAsync(cancellationToken).ConfigureAwait(false))
                {
                    logAction?.Invoke("[LOGIN] UI xác nhận đã đăng nhập.");
                    return true;
                }

                logAction?.Invoke("[LOGIN] UI vẫn hiện khách / Đăng nhập — cookie cũ không còn hiệu lực.");
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LOGIN] For You probe failed: " + ex.Message);
            }

            return false;
        }

        public async Task GotoSearchAsync(string keywords, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (string.IsNullOrWhiteSpace(keywords))
            {
                throw new ArgumentException("Keywords are required.", nameof(keywords));
            }

            var url = "https://www.tiktok.com/search?q=" + Uri.EscapeDataString(keywords.Trim());
            logAction?.Invoke("Navigating to TikTok search: " + keywords.Trim());
            await SafeGotoAsync(url, cancellationToken).ConfigureAwait(false);

            await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);

            if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
            {
                await WaitForCaptchaResolvedAsync(cancellationToken, logAction).ConfigureAwait(false);
            }

            if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new CheckpointDetectedException("TikTok checkpoint detected on search page.");
            }
        }

        public async Task GotoVideoSearchAsync(string keywords, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (string.IsNullOrWhiteSpace(keywords))
            {
                throw new ArgumentException("Keywords are required.", nameof(keywords));
            }

            var encoded = Uri.EscapeDataString(keywords.Trim());
            var url = "https://www.tiktok.com/search/video?q=" + encoded;
            logAction?.Invoke("[Video] Mở: " + url);
            await SafeGotoAsync(url, cancellationToken).ConfigureAwait(false);
            await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);

            if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
            {
                await WaitForCaptchaResolvedAsync(cancellationToken, logAction).ConfigureAwait(false);
            }

            if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new CheckpointDetectedException("TikTok checkpoint detected on video search page.");
            }
        }

        /// <summary>
        /// Warm-up: open For You feed when no search keywords (casual browse).
        /// </summary>
        public async Task GotoWarmupForyouFeedAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            _warmupLockedVideoUrl = null;
            _warmupLockedVideoId = null;
            _warmupInForyouMode = true;
            logAction?.Invoke("[LIVE] Mở For You — lướt video dạo (không từ khóa).");
            BeginForyouFeedCapture();
            await SafeGotoAsync(TikTokForyouUrl, cancellationToken).ConfigureAwait(false);
            await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);

            if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
            {
                await WaitForCaptchaResolvedAsync(cancellationToken, logAction).ConfigureAwait(false);
            }

            await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);
            await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            logAction?.Invoke("[LIVE] For You feed sẵn sàng.");
        }

        /// <summary>
        /// Khóa video đầu tiên trên For You (bỏ qua video của mình / Shop gate nếu gặp).
        /// </summary>
        public async Task OpenFirstWarmupVideoFromForyouAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            string excludeOwnUniqueId = null)
        {
            logAction?.Invoke("[LIVE] Xem video trên For You, sau đó lướt sang clip kế (không tìm kiếm).");
            if (!await WaitForWarmupVideoIdAsync(cancellationToken, logAction, TimeSpan.FromSeconds(18)).ConfigureAwait(false))
            {
                logAction?.Invoke("[LIVE] For You: chưa thấy video id — thử cuộn 1 lần...");
                await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                await RandomDelayAsync(1200, 2000, cancellationToken).ConfigureAwait(false);
            }

            var entryIdentity = await TryResolveForyouFeedIdentityAsync().ConfigureAwait(false);
            if (IsWarmupFeedContentExcluded(entryIdentity.VideoId, entryIdentity.Author, entryIdentity.Caption))
            {
                logAction?.Invoke(
                    "[LIVE] For You: clip đầu đã xem trước đó (@"
                    + (entryIdentity.Author ?? "?") + ") — lướt ngay.");
                await QuickForyouFeedNudgeAsync(cancellationToken).ConfigureAwait(false);
                await RandomDelayAsync(900, 1500, cancellationToken).ConfigureAwait(false);
            }

            for (var pickTry = 0; pickTry < 8; pickTry++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!await WaitForWarmupVideoIdAsync(cancellationToken, logAction, TimeSpan.FromSeconds(8)).ConfigureAwait(false))
                    {
                        logAction?.Invoke("[LIVE] For You: chưa thấy video id — thử lướt...");
                        await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                        await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                        await RandomDelayAsync(900, 1600, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    await PrepareCurrentFeedVideoForWarmupAsync(cancellationToken, logAction, excludeOwnUniqueId)
                        .ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] Now on For You: " + (_warmupLockedVideoUrl ?? _page?.Url ?? string.Empty));
                    return;
                }
                catch (ShopVideoGateException ex)
                {
                    var reason = string.IsNullOrWhiteSpace(ex.Message) ? "Shop / lỗi tải" : ex.Message;
                    logAction?.Invoke("[LIVE] Bỏ video For You (" + reason + ") — thử clip kế.");
                    try
                    {
                        if (reason.IndexOf("đã xem", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            await QuickForyouFeedNudgeAsync(cancellationToken).ConfigureAwait(false);
                            await RandomDelayAsync(900, 1500, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await AdvanceToNextWarmupVideoInFeedAsync(cancellationToken, logAction, excludeOwnUniqueId)
                                .ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        await QuickForyouFeedNudgeAsync(cancellationToken).ConfigureAwait(false);
                        await RandomDelayAsync(900, 1600, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            throw new ShopVideoGateException("Không mở được video khả dụng trên For You.");
        }

        /// <summary>
        /// Warm-up: open the Video tab search page and wait until search-scoped results exist (not FYP/profile links).
        /// </summary>
        public async Task GotoWarmupVideoSearchAsync(string keywords, CancellationToken cancellationToken, Action<string> logAction)
        {
            // Hết khóa video slot trước — tránh Watch/Ensure kéo lại đúng 1 URL cũ.
            _warmupLockedVideoUrl = null;
            _warmupLockedVideoId = null;
            _warmupInForyouMode = false;
            EndForyouFeedCapture();
            await GotoVideoSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);
            await WaitForWarmupVideoSearchAsync(cancellationToken, logAction).ConfigureAwait(false);
        }

        public void BeginShopSearchCapture()
        {
            lock (_shopSearchJsonBodies)
            {
                _shopSearchJsonBodies.Clear();
            }

            lock (_shopSearchResponseUrls)
            {
                _shopSearchResponseUrls.Clear();
            }

            if (_page == null)
            {
                return;
            }

            if (_shopSearchResponseHandler != null)
            {
                _page.Response -= _shopSearchResponseHandler;
            }

            _shopSearchResponseHandler = (_, response) => { _ = CaptureShopSearchResponseAsync(response); };
            _page.Response += _shopSearchResponseHandler;
        }

        public IReadOnlyList<string> GetShopSearchCapturedJson()
        {
            lock (_shopSearchJsonBodies)
            {
                return _shopSearchJsonBodies.ToList();
            }
        }

        public IReadOnlyList<string> GetShopSearchCapturedUrls()
        {
            lock (_shopSearchResponseUrls)
            {
                return _shopSearchResponseUrls.ToList();
            }
        }

        public async Task<string> GetPageContentSafeAsync()
        {
            EnsurePageReady();
            if (_page == null)
            {
                return string.Empty;
            }

            try
            {
                return await _page.ContentAsync().ConfigureAwait(false) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task ScrollMobileShopFeedAsync(CancellationToken cancellationToken, int rounds = 3)
        {
            if (_page == null)
            {
                return;
            }

            for (var i = 0; i < rounds; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _page.EvaluateAsync(
                        "window.scrollBy(0, Math.max(500, Math.floor(window.innerHeight * 0.75)));").ConfigureAwait(false);
                    await RandomDelayAsync(700, 1100, cancellationToken).ConfigureAwait(false);
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

        public async Task<string> GetMobileShopNavDiagnosticsAsync()
        {
            EnsurePageReady();
            if (_page == null)
            {
                return string.Empty;
            }

            try
            {
                return await _page.EvaluateAsync<string>(
                    @"() => {
                        const tabs = Array.from(document.querySelectorAll('[role=""tab""], button, a, span, div'))
                            .map(el => (el.innerText || el.textContent || '').trim())
                            .filter(t => t && t.length <= 24)
                            .slice(0, 20);
                        const out = {
                            url: location.href,
                            title: document.title || '',
                            tabs: tabs,
                            pdpLinks: document.querySelectorAll('a[href*=""/shop/pdp/""]').length,
                            productLinks: document.querySelectorAll('a[href*=""product""]').length,
                            priceLike: Array.from(document.querySelectorAll('span, div, p')).filter(el => {
                                const t = (el.textContent || '').trim();
                                return /^\d[\d.,]*\s*(?:đ|₫)$/.test(t);
                            }).length,
                            bodyHead: (document.body && document.body.innerText ? document.body.innerText.slice(0, 120) : '')
                        };
                        return JSON.stringify(out);
                    }").ConfigureAwait(false) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task CaptureShopSearchResponseAsync(IResponse response)
        {
            try
            {
                if (response == null)
                {
                    return;
                }

                var url = response.Url ?? string.Empty;
                lock (_shopSearchResponseUrls)
                {
                    if (_shopSearchResponseUrls.Count < 80)
                    {
                        _shopSearchResponseUrls.Add(url);
                    }
                }

                if (response.Status != 200)
                {
                    return;
                }

                if (url.IndexOf("tiktok", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                if (url.IndexOf("search", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("shop", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("product", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("commerce", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("/api/", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("aweme", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var contentType = string.Empty;
                if (response.Headers != null && response.Headers.TryGetValue("content-type", out var ct))
                {
                    contentType = ct ?? string.Empty;
                }

                if (contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) < 0 &&
                    contentType.IndexOf("javascript", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var body = await response.TextAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(body) ||
                    body.Length < 80 ||
                    body.IndexOf("product", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                lock (_shopSearchJsonBodies)
                {
                    if (_shopSearchJsonBodies.Count < 40)
                    {
                        _shopSearchJsonBodies.Add(body);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        public async Task GotoMobileShopSearchAsync(string keywords, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (string.IsNullOrWhiteSpace(keywords))
            {
                throw new ArgumentException("Keywords are required.", nameof(keywords));
            }

            var encoded = Uri.EscapeDataString(keywords.Trim());
            var mobileSearchUrls = new[]
            {
                "https://www.tiktok.com/search?q=" + encoded + "&t=shop",
                "https://www.tiktok.com/search?q=" + encoded,
                "https://m.tiktok.com/search?q=" + encoded
            };

            BeginShopSearchCapture();

            foreach (var url in mobileSearchUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logAction?.Invoke("[Shop/Mobile] Mở tìm kiếm mobile: " + url);
                try
                {
                    await SafeGotoAsync(url, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(1800, 2800, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[Shop/Mobile] Goto thất bại: " + ex.Message);
                    continue;
                }

                if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                {
                    await WaitForCaptchaResolvedAsync(cancellationToken, logAction).ConfigureAwait(false);
                }

                if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new CheckpointDetectedException("TikTok checkpoint detected on mobile shop search.");
                }

                var landedUrl = _page.Url ?? string.Empty;
                logAction?.Invoke("[Shop/Mobile] Đã tới: " + landedUrl);

                if (landedUrl.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    logAction?.Invoke("[Shop/Mobile] TikTok yêu cầu đăng nhập — thử URL khác.");
                    continue;
                }

                var clickedTab = await TryClickMobileShopTabAsync(cancellationToken, logAction).ConfigureAwait(false);
                await RandomDelayAsync(2200, 3200, cancellationToken).ConfigureAwait(false);
                try
                {
                    await _page.WaitForLoadStateAsync(
                        LoadState.NetworkIdle,
                        new PageWaitForLoadStateOptions { Timeout = 15000 }).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                await RandomDelayAsync(2000, 3000, cancellationToken).ConfigureAwait(false);
                await ScrollMobileShopFeedAsync(cancellationToken, 4).ConfigureAwait(false);
                await WaitForShopProductLinksAsync(1, 25000, cancellationToken).ConfigureAwait(false);

                var linkCount = await CountShopProductLinksAsync().ConfigureAwait(false);
                var hasGrid = await HasMobileShopGridAsync().ConfigureAwait(false);
                var navDiag = await GetMobileShopNavDiagnosticsAsync().ConfigureAwait(false);
                logAction?.Invoke(
                    $"[Shop/Mobile] Tab='{clickedTab}', link={linkCount}, grid={hasGrid}, url={_page.Url ?? string.Empty}");
                if (!string.IsNullOrWhiteSpace(navDiag))
                {
                    logAction?.Invoke("[Shop/Mobile] Diag: " + navDiag.Replace("\n", " ").Replace("\r", " "));
                }

                if (!string.IsNullOrWhiteSpace(clickedTab))
                {
                    logAction?.Invoke("[Shop/Mobile] Giữ trang mobile search (tab Cửa hàng).");
                    return;
                }

                if (linkCount >= 2 || hasGrid)
                {
                    logAction?.Invoke("[Shop/Mobile] Đã thấy sản phẩm trên trang mobile search.");
                    return;
                }
            }

            logAction?.Invoke("[Shop/Mobile] Tab Cửa hàng chưa có sản phẩm — thử URL Shop trực tiếp.");
            await GotoShopSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);
        }

        public async Task GotoShopSearchAsync(string keywords, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (string.IsNullOrWhiteSpace(keywords))
            {
                throw new ArgumentException("Keywords are required.", nameof(keywords));
            }

            var encoded = Uri.EscapeDataString(keywords.Trim());
            var attempts = new[]
            {
                "https://www.tiktok.com/shop/s/" + encoded,
                "https://shop.tiktok.com/vn/s?q=" + encoded,
                "https://shop.tiktok.com/s?q=" + encoded,
                "https://www.tiktok.com/search/shop?q=" + encoded,
                "https://www.tiktok.com/search?q=" + encoded + "&t=shop",
                "https://www.tiktok.com/search?q=" + encoded
            };

            var landed = false;
            foreach (var url in attempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logAction?.Invoke("[Shop] Thử URL: " + url);
                try
                {
                    await SafeGotoAsync(url, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[Shop] Goto thất bại: " + ex.Message);
                    continue;
                }

                if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                {
                    await WaitForCaptchaResolvedAsync(cancellationToken, logAction).ConfigureAwait(false);
                }

                if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new CheckpointDetectedException("TikTok checkpoint detected on shop search page.");
                }

                var landedUrl = _page.Url ?? string.Empty;
                logAction?.Invoke("[Shop] Đã tới: " + landedUrl);

                if (landedUrl.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    logAction?.Invoke("[Shop] TikTok yêu cầu đăng nhập. Hãy đăng nhập thủ công cho profile bot rồi thử lại.");
                    continue;
                }

                if (landedUrl.IndexOf("seller.tiktok", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    logAction?.Invoke("[Shop] Bị redirect sang seller.tiktok.com — thử URL khác.");
                    continue;
                }

                if (!AffiliateHunter.IsConsumerShopLandingUrl(landedUrl))
                {
                    logAction?.Invoke("[Shop] URL chưa phải trang Shop người mua — thử tiếp.");
                    continue;
                }

                landed = true;
                break;
            }

            if (!landed)
            {
                logAction?.Invoke("[Shop] Không trang Shop nào load được — sẽ thử click tab Shopping nếu có.");
            }

            // Try to click the "Shopping" tab on the regular search page if products are not visible yet.
            await TryClickShoppingTabAsync(cancellationToken, logAction).ConfigureAwait(false);
        }

        public async Task<long> CountShopProductLinksAsync()
        {
            EnsurePageReady();
            if (_page == null)
            {
                return 0;
            }

            try
            {
                return await _page.EvaluateAsync<long>(
                    @"() => document.querySelectorAll(
                        'a[href*=""/view/product/""], a[href*=""/shop/pdp/""], a[href*=""/shop/p/""], a[href*=""/pdp/""], [data-e2e*=""product""]'
                    ).length").ConfigureAwait(false);
            }
            catch
            {
                return 0;
            }
        }

        public async Task WaitForShopProductLinksAsync(int minLinks, int timeoutMs, CancellationToken cancellationToken)
        {
            if (minLinks < 1)
            {
                minLinks = 1;
            }

            if (timeoutMs < 1000)
            {
                timeoutMs = 1000;
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = await CountShopProductLinksAsync().ConfigureAwait(false);
                if (count >= minLinks)
                {
                    return;
                }

                await Task.Delay(800, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<string> ExportCookieHeaderAsync()
        {
            if (_context == null)
            {
                return string.Empty;
            }

            try
            {
                var cookies = await _context.CookiesAsync(new[] { "https://www.tiktok.com/", "https://tiktok.com/" })
                    .ConfigureAwait(false);
                if (cookies == null || cookies.Count == 0)
                {
                    cookies = await _context.CookiesAsync((IEnumerable<string>)null).ConfigureAwait(false);
                }

                if (cookies == null || cookies.Count == 0)
                {
                    return string.Empty;
                }

                var pairs = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < cookies.Count; i++)
                {
                    var c = cookies[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.Name))
                    {
                        continue;
                    }

                    if ((c.Domain ?? string.Empty).IndexOf("tiktok", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var dedupeKey = c.Name + "\0" + (c.Domain ?? string.Empty);
                    if (!seen.Add(dedupeKey))
                    {
                        continue;
                    }

                    pairs.Add(c.Name + "=" + (c.Value ?? string.Empty));
                }

                return string.Join("; ", pairs);
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<string> GetPageTitleSafeAsync()
        {
            EnsurePageReady();
            if (_page == null)
            {
                return string.Empty;
            }

            try
            {
                return await _page.TitleAsync().ConfigureAwait(false) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<bool> HasMobileShopGridAsync()
        {
            EnsurePageReady();
            if (_page == null)
            {
                return false;
            }

            try
            {
                return await _page.EvaluateAsync<bool>(
                    @"() => {
                        try {
                            const text = document.body ? document.body.innerText : '';
                            if (/đã bán|₫|\d[\d.,]*đ|giảm \d/i.test(text)) return true;
                            if (document.querySelector('[data-e2e*=""search-shop""], [data-e2e*=""shop-product""], [class*=""ProductCard""]')) {
                                return true;
                            }
                            return document.querySelectorAll('a[href*=""pdp""], a[href*=""product""]').length >= 2;
                        } catch (e) { return false; }
                    }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> TryClickMobileShopTabAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            EnsurePageReady();

            const string clickJs = @"() => {
                const pick = (el) => (el.innerText || el.textContent || '').trim();
                const tabs = Array.from(document.querySelectorAll('[role=""tab""], [data-e2e*=""search-tab""], [data-e2e*=""tab-item""]'));
                for (const el of tabs) {
                    const raw = pick(el);
                    const t = raw.toLowerCase();
                    if (!t || t.length > 24) continue;
                    if (t.includes('cửa hàng') || t.includes('cua hang') || t === 'shop') {
                        try { el.click(); return raw; } catch (e) {}
                    }
                }
                const e2e = document.querySelector(
                    '[data-e2e=""search_shop-tab""], [data-e2e*=""search-shop-tab""], [data-e2e*=""shop-tab""]'
                );
                if (e2e) {
                    try { e2e.click(); return 'e2e-shop'; } catch (e) {}
                }
                return '';
            }";

            try
            {
                var clicked = await _page.EvaluateAsync<string>(clickJs).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(clicked))
                {
                    logAction?.Invoke("[Shop/Mobile] Đã bấm tab Cửa hàng: '" + clicked + "'.");
                }
                else
                {
                    logAction?.Invoke("[Shop/Mobile] Không thấy tab Cửa hàng — thử tab Shopping chung.");
                    clicked = await TryClickShoppingTabAsync(cancellationToken, logAction).ConfigureAwait(false);
                }

                return clicked ?? string.Empty;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop/Mobile] Không click được tab Cửa hàng: " + ex.Message);
                return string.Empty;
            }
        }

        private async Task<string> TryClickShoppingTabAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            EnsurePageReady();

            const string clickJs = @"() => {
                const want = ['cửa hàng','cua hang','shop','shopping','mua','sản phẩm','store'];
                const candidates = Array.from(document.querySelectorAll(
                    '[role=""tab""], button, a, div[class*=""tab""], span[class*=""tab""]'
                ));
                for (const el of candidates) {
                    const t = (el.innerText || el.textContent || '').trim().toLowerCase();
                    if (!t || t.length > 24) continue;
                    if (want.some(w => t === w || t.includes(w))) {
                        try { el.click(); return t; } catch (e) {}
                    }
                }
                return '';
            }";

            try
            {
                var clicked = await _page.EvaluateAsync<string>(clickJs).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(clicked))
                {
                    logAction?.Invoke("[Shop] Đã bấm tab: '" + clicked + "'.");
                    await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);
                }

                return clicked ?? string.Empty;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop] Không click được tab Shopping: " + ex.Message);
                return string.Empty;
            }
        }

        public Task OpenVideoAsync(
            int oneBasedIndex,
            CancellationToken cancellationToken,
            Action<string> logAction,
            string excludeOwnUniqueId = null,
            string searchKeywords = null) =>
            OpenWarmupVideoFromSearchAsync(oneBasedIndex, searchKeywords, cancellationToken, logAction, excludeOwnUniqueId);

        /// <summary>
        /// Opens the Nth video from TikTok video-search results (search_video-item), optionally skipping the logged-in account's videos.
        /// </summary>
        public async Task OpenWarmupVideoFromSearchAsync(
            int oneBasedIndex,
            string searchKeywords,
            CancellationToken cancellationToken,
            Action<string> logAction,
            string excludeOwnUniqueId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (oneBasedIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
            }

            await WaitForWarmupVideoSearchAsync(cancellationToken, logAction).ConfigureAwait(false);

            var exclude = (excludeOwnUniqueId ?? string.Empty).Trim().TrimStart('@');
            var eligibleCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
            var stagnantScrolls = 0;

            while (eligibleCount < oneBasedIndex && stagnantScrolls < 8)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ScrollWarmupVideoSearchFeedAsync(cancellationToken).ConfigureAwait(false);
                await RandomDelayAsync(800, 1500, cancellationToken).ConfigureAwait(false);

                var newCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
                stagnantScrolls = newCount == eligibleCount ? stagnantScrolls + 1 : 0;
                eligibleCount = newCount;
            }

            if (eligibleCount == 0)
            {
                throw new InvalidOperationException(
                    "No eligible video results on TikTok video search (tab Video). Try another keyword or check login/CAPTCHA.");
            }

            var maxPick = eligibleCount;
            var openAttempts = 0;
            const int maxOpenAttempts = 8;
            for (var pick = oneBasedIndex; pick <= maxPick; pick++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (openAttempts >= maxOpenAttempts)
                {
                    throw new ShopVideoGateException(
                        "Quá nhiều video skeleton/không tải khi mở từ search (tối đa " + maxOpenAttempts + ").");
                }

                var href = await GetWarmupSearchVideoHrefAsync(pick, exclude).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var directUrl = ToCanonicalVideoPageUrl(href);
                var authorOnUrl = ExtractAuthorFromVideoUrl(directUrl);
                if (!string.IsNullOrWhiteSpace(exclude) &&
                    !string.IsNullOrWhiteSpace(authorOnUrl) &&
                    string.Equals(authorOnUrl, exclude, StringComparison.OrdinalIgnoreCase))
                {
                    var ownId = ExtractVideoIdFromUrl(directUrl);
                    if (!string.IsNullOrWhiteSpace(ownId))
                    {
                        _warmupSessionExcludeVideoIds.Add(ownId);
                    }

                    logAction?.Invoke($"[LIVE] Bỏ qua video của chính mình (@{exclude}): {directUrl}");
                    // Re-count vì vừa loại thêm id — có thể cần scroll thêm.
                    continue;
                }

                // Giữ query (?q=…) nếu có — browse/feed carousel cần context; canonical chỉ dùng để nhận diện id.
                var openUrl = NormalizeTikTokUrl(href);
                logAction?.Invoke($"[LIVE] Try open search video #{pick}/{eligibleCount} (@{authorOnUrl}): {openUrl}");
                openAttempts++;
                _warmupLockedVideoUrl = openUrl;
                _warmupLockedVideoId = ExtractVideoIdFromUrl(directUrl);
                if (!string.IsNullOrWhiteSpace(_warmupLockedVideoId) &&
                    IsWarmupSearchPickVideoIdExcluded(_warmupLockedVideoId))
                {
                    logAction?.Invoke("[LIVE] Video đã xem/loại trước đó — bỏ: " + _warmupLockedVideoId);
                    continue;
                }

                // Ưu tiên click item trên lưới search → TikTok mở browse/feed (có thể lướt).
                // Fallback goto URL đầy đủ (không strip ?q=) nếu click không vào được trang video.
                var openedByClick = await TryClickWarmupSearchVideoItemAsync(pick, exclude, cancellationToken, logAction)
                    .ConfigureAwait(false);
                if (!openedByClick)
                {
                    logAction?.Invoke("[LIVE] Click search item không vào video — fallback mở URL (giữ query).");
                    await SafeGotoAsync(openUrl, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(1200, 2200, cancellationToken).ConfigureAwait(false);
                }

                await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
                // Khóa URL thực tế trên trang (có thể còn ?q=) để giữ ngữ cảnh feed khi restore.
                var landedUrl = _page.Url ?? openUrl;
                if ((landedUrl.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _warmupLockedVideoUrl = landedUrl;
                    var landedId = ExtractVideoIdFromUrl(landedUrl);
                    if (!string.IsNullOrWhiteSpace(landedId))
                    {
                        _warmupLockedVideoId = landedId;
                    }
                }

                // Sau khi mở: nếu TikTok redirect về video của mình thì bỏ và chọn cái khác.
                var landedAuthor = ExtractAuthorFromVideoUrl(_page.Url ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(exclude) &&
                    !string.IsNullOrWhiteSpace(landedAuthor) &&
                    string.Equals(landedAuthor, exclude, StringComparison.OrdinalIgnoreCase))
                {
                    ExcludeCurrentWarmupVideo();
                    logAction?.Invoke($"[LIVE] Đã mở nhầm video của @{exclude} — quay lại search, chọn video khác.");
                    if (!string.IsNullOrWhiteSpace(searchKeywords))
                    {
                        await GotoWarmupVideoSearchAsync(searchKeywords, cancellationToken, logAction).ConfigureAwait(false);
                        eligibleCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
                        maxPick = eligibleCount;
                        pick = Math.Max(0, oneBasedIndex - 1);
                    }

                    continue;
                }

                if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new CaptchaDetectedException();
                }

                if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new CheckpointDetectedException("TikTok checkpoint detected while opening video.");
                }

                if (await IsShopInAppGateBlockingAsync().ConfigureAwait(false))
                {
                    var blockedId = ExtractVideoIdFromUrl(directUrl);
                    if (!string.IsNullOrWhiteSpace(blockedId))
                    {
                        _warmupSessionExcludeVideoIds.Add(blockedId);
                    }

                    logAction?.Invoke("[LIVE] Video TikTok Shop (chỉ xem trong app) — thử kết quả search khác...");
                    // #region agent log
                    DebugAgentLog.Write("F", "BrowserAutomation.OpenWarmupVideoFromSearch", "shop on open skip pick", new { pick, directUrl }, "post-fix");
                    // #endregion
                    if (!string.IsNullOrWhiteSpace(searchKeywords))
                    {
                        await GotoWarmupVideoSearchAsync(searchKeywords, cancellationToken, logAction).ConfigureAwait(false);
                        eligibleCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
                        maxPick = eligibleCount;
                        pick = Math.Max(0, oneBasedIndex - 1);
                    }
                    else
                    {
                        try
                        {
                            await _page.GoBackAsync(new PageGoBackOptions { Timeout = 15000 }).ConfigureAwait(false);
                            await RandomDelayAsync(1200, 2200, cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Best-effort return to results.
                        }
                    }

                    continue;
                }

                // Không ForceCanonical (strip ?q=) — sẽ phá browse feed và làm lướt ArrowDown/wheel fail.
                var hydrated = await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                if (hydrated && !await IsVideoMediaPlayableAsync().ConfigureAwait(false))
                {
                    try
                    {
                        await TryRecoverBrowseMediaAsync(cancellationToken, logAction).ConfigureAwait(false);
                    }
                    catch (ShopVideoGateException)
                    {
                        var blockedId = ExtractVideoIdFromUrl(directUrl);
                        if (!string.IsNullOrWhiteSpace(blockedId))
                        {
                            _warmupSessionExcludeVideoIds.Add(blockedId);
                        }

                        logAction?.Invoke("[LIVE] Video TikTok Shop (chỉ xem trong app) — thử kết quả search khác...");
                        if (!string.IsNullOrWhiteSpace(searchKeywords))
                        {
                            await GotoWarmupVideoSearchAsync(searchKeywords, cancellationToken, logAction).ConfigureAwait(false);
                            eligibleCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
                            maxPick = eligibleCount;
                            pick = Math.Max(0, oneBasedIndex - 1);
                        }

                        continue;
                    }
                }

                if (await IsShopInAppGateBlockingAsync().ConfigureAwait(false))
                {
                    var blockedId = ExtractVideoIdFromUrl(directUrl);
                    if (!string.IsNullOrWhiteSpace(blockedId))
                    {
                        _warmupSessionExcludeVideoIds.Add(blockedId);
                    }

                    logAction?.Invoke("[LIVE] Video TikTok Shop (chỉ xem trong app) — thử kết quả search khác...");
                    if (!string.IsNullOrWhiteSpace(searchKeywords))
                    {
                        await GotoWarmupVideoSearchAsync(searchKeywords, cancellationToken, logAction).ConfigureAwait(false);
                        eligibleCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
                        maxPick = eligibleCount;
                        pick = Math.Max(0, oneBasedIndex - 1);
                    }

                    continue;
                }

                if (!await IsVideoMediaPlayableAsync().ConfigureAwait(false))
                {
                    var blockedId = ExtractVideoIdFromUrl(directUrl);
                    if (!string.IsNullOrWhiteSpace(blockedId))
                    {
                        _warmupSessionExcludeVideoIds.Add(blockedId);
                    }

                    logAction?.Invoke("[LIVE] Video không có media trên web — bỏ clip, thử kết quả khác.");
                    if (!string.IsNullOrWhiteSpace(searchKeywords))
                    {
                        await GotoWarmupVideoSearchAsync(searchKeywords, cancellationToken, logAction).ConfigureAwait(false);
                        eligibleCount = await CountEligibleWarmupSearchItemsAsync(exclude).ConfigureAwait(false);
                        maxPick = eligibleCount;
                        pick = Math.Max(0, oneBasedIndex - 1);
                    }

                    continue;
                }

                logAction?.Invoke("[LIVE] Now on: " + (_page.Url ?? string.Empty));
                // Chỉ đánh dấu «đã mở» — chưa xem. Tránh lướt ↓ feed coi clip hiện tại là «đã xem» khi media chưa phát.
                MarkWarmupVideoOpenedFromPage();
                // #region agent log
                DebugAgentLog.Write("A", "BrowserAutomation.OpenWarmupVideoFromSearch", "video opened", new
                {
                    probe = await ProbePageEngagementStateAsync().ConfigureAwait(false),
                    excludedCount = _warmupSessionExcludeVideoIds.Count + _warmupPersistedExcludeVideoIds.Count,
                    lockedId = _warmupLockedVideoId
                }, "post-fix");
                // #endregion
                return;
            }

            throw new ShopVideoGateException(
                "Không tìm thấy video của người khác để warm-up (search toàn video của mình / Shop). Thử từ khóa khác.");
        }

        /// <summary>
        /// Mở 1 video từ kết quả search (ưu tiên top đầu, chọn ngẫu nhiên trong 1..3) rồi xem feed.
        /// </summary>
        public Task OpenFirstWarmupVideoFromSearchAsync(
            string searchKeywords,
            CancellationToken cancellationToken,
            Action<string> logAction,
            string excludeOwnUniqueId = null)
        {
            var topPick = _random.Next(1, 4); // 1..3
            logAction?.Invoke("[LIVE] Mở video từ search (top #" + topPick + "), sau đó sẽ cuộn feed thay vì quay lại tìm kiếm.");
            return OpenWarmupVideoFromSearchAsync(topPick, searchKeywords, cancellationToken, logAction, excludeOwnUniqueId);
        }

        /// <summary>
        /// Khóa video đang hiển thị trên trang (sau khi ArrowDown / cuộn feed).
        /// </summary>
        public async Task PrepareCurrentFeedVideoForWarmupAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            string excludeOwnUniqueId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
            await WaitForWarmupVideoIdAsync(cancellationToken, logAction, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            var url = _page.Url ?? string.Empty;
            string id;
            string author;
            string caption;
            if (_warmupInForyouMode)
            {
                var identity = await TryResolveForyouFeedIdentityAsync().ConfigureAwait(false);
                id = identity.VideoId;
                author = identity.Author;
                caption = identity.Caption;
            }
            else
            {
                id = ExtractVideoIdFromUrl(url);
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                }

                author = ExtractAuthorFromVideoUrl(url);
                caption = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ShopVideoGateException("Không xác định được video trên feed.");
            }

            if (IsWarmupFeedContentExcluded(id, author, caption, checkPersistedVideoHistory: _warmupInForyouMode))
            {
                logAction?.Invoke(
                    "[LIVE] Clip @"
                    + (author ?? "?")
                    + " đã xem/comment trước đó — bỏ qua, lướt clip khác.");
                throw new ShopVideoGateException("Video đã xem trong phiên — bỏ qua.");
            }

            if (string.IsNullOrWhiteSpace(author))
            {
                author = await TryResolveCurrentWarmupAuthorAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(caption))
            {
                caption = await GetCurrentVideoCaptionAsync(cancellationToken).ConfigureAwait(false);
            }
            var exclude = (excludeOwnUniqueId ?? string.Empty).Trim().TrimStart('@');
            if (!string.IsNullOrWhiteSpace(exclude) &&
                !string.IsNullOrWhiteSpace(author) &&
                string.Equals(author, exclude, StringComparison.OrdinalIgnoreCase))
            {
                ExcludeWarmupVideoId(id);
                throw new ShopVideoGateException("Feed đưa về video của chính mình — bỏ qua.");
            }

            if (await IsShopInAppGateBlockingAsync().ConfigureAwait(false))
            {
                ExcludeWarmupVideoId(id);
                logAction?.Invoke("[LIVE] Video TikTok Shop trên feed — bỏ clip, lướt tiếp.");
                throw new ShopVideoGateException("TikTok Shop video is blocked on web; open in the mobile app.");
            }

            _warmupLockedVideoId = id;
            _warmupLastAuthor = author;
            _warmupLastCaption = caption;
            _warmupLastFingerprint = BuildWarmupContentFingerprint(author, caption);
            if (url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _warmupLockedVideoUrl = NormalizeTikTokUrl(url);
            }
            else if (!string.IsNullOrWhiteSpace(author))
            {
                _warmupLockedVideoUrl = NormalizeTikTokUrl("https://www.tiktok.com/@" + author + "/video/" + id);
            }
            else
            {
                _warmupLockedVideoUrl = NormalizeTikTokUrl(TikTokForyouUrl);
            }

            MarkWarmupVideoOpened(id);
            var authorLabel = string.IsNullOrWhiteSpace(author) ? "?" : author;
            var idLabel = string.IsNullOrWhiteSpace(id) ? "?" : id;
            logAction?.Invoke("[LIVE] Feed video hiện tại: @" + authorLabel + " / " + idLabel);
            await EnsureVideoPlayingForWatchAsync(cancellationToken, logAction).ConfigureAwait(false);
        }

        /// <summary>
        /// Chuyển sang video kế tiếp bằng lướt lên / cuộn (giống người xem TikTok), không quay lại search.
        /// </summary>
        public async Task AdvanceToNextWarmupVideoInFeedAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            string excludeOwnUniqueId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            var previousId = _warmupLockedVideoId;
            if (string.IsNullOrWhiteSpace(previousId))
            {
                previousId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
            }

            // Bỏ khóa URL cũ — trên For You URL /video/ có thể giữ id clip trước dù UI đã lướt.
            _warmupLockedVideoUrl = null;
            _warmupLockedVideoId = null;

            await ClearWarmupPlaybackGuardsAsync().ConfigureAwait(false);
            await EnsureSharePanelClosedAsync(cancellationToken, logAction).ConfigureAwait(false);
            await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);
            var inForyouFeed = _warmupInForyouMode || await IsForyouFeedPageAsync().ConfigureAwait(false);
            var inBrowseOverlay = !inForyouFeed && await IsBrowseVideoOverlayAsync().ConfigureAwait(false);
            if (!inBrowseOverlay)
            {
                await TryCloseCommentsUiForFeedAdvanceAsync(cancellationToken, logAction).ConfigureAwait(false);
            }

            logAction?.Invoke(inBrowseOverlay
                ? "[LIVE] Chuyển video kế (browse overlay — ưu tiên nút ↓)..."
                : inForyouFeed
                    ? "[LIVE] Chuyển video kế (For You — lướt lên / ArrowDown)..."
                    : "[LIVE] Lướt lên sang video tiếp theo (không về trang tìm kiếm)...");

            var previousFingerprint = _warmupLastFingerprint
                ?? BuildWarmupContentFingerprint(_warmupLastAuthor, _warmupLastCaption);

            string newId = null;
            var excludedStreak = 0;
            string lastExcludedFingerprint = null;
            string lastExcludedVideoId = null;
            var deadline = DateTime.UtcNow.AddSeconds(inForyouFeed ? 55 : inBrowseOverlay ? 45 : 25);
            var advanced = false;
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= deadline)
                {
                    logAction?.Invoke(inForyouFeed
                        ? "[LIVE] Hết thời gian chờ lướt feed — thử reset For You."
                        : inBrowseOverlay
                            ? "[LIVE] Hết thời gian chờ lướt feed browse — thử cách khác."
                            : "[LIVE] Hết thời gian chờ lướt feed.");
                    if (inForyouFeed)
                    {
                        await BreakForyouFeedStuckAsync(cancellationToken, logAction).ConfigureAwait(false);
                        var deadlineIdentity = await TryResolveForyouFeedIdentityAsync().ConfigureAwait(false);
                        newId = deadlineIdentity.VideoId;
                        if (!string.IsNullOrWhiteSpace(newId) &&
                            (!string.Equals(newId, previousId, StringComparison.Ordinal) ||
                             !string.Equals(deadlineIdentity.Fingerprint, previousFingerprint, StringComparison.OrdinalIgnoreCase)) &&
                            !IsWarmupFeedContentExcluded(
                                deadlineIdentity.VideoId,
                                deadlineIdentity.Author,
                                deadlineIdentity.Caption,
                                checkPersistedVideoHistory: true))
                        {
                            advanced = true;
                            break;
                        }
                    }

                    break;
                }

                try
                {
                    if (inForyouFeed && !inBrowseOverlay)
                    {
                        if (attempt <= 6)
                        {
                            await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                            if (attempt <= 4)
                            {
                                await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                            }
                        }
                        else if (attempt <= 9)
                        {
                            await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                            await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            var viewport = _page.ViewportSize;
                            var cx = (float)((viewport?.Width > 0 ? viewport.Width : 900) * 0.5);
                            var cy = (float)((viewport?.Height > 0 ? viewport.Height : 800) * 0.45);
                            await _page.Mouse.MoveAsync(cx, cy).ConfigureAwait(false);
                            await _page.Mouse.WheelAsync(0, _random.Next(900, 1500)).ConfigureAwait(false);
                        }
                    }
                    else if (attempt <= 5)
                    {
                        var clickedNav = await TryClickBrowseNextVideoButtonAsync(cancellationToken, logAction)
                            .ConfigureAwait(false);
                        if (!clickedNav && attempt <= 3)
                        {
                            await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                        }
                    }
                    else if (attempt <= 7)
                    {
                        await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                    }
                    else if (attempt <= 9)
                    {
                        var viewport = _page.ViewportSize;
                        var cx = (float)((viewport?.Width > 0 ? viewport.Width : 900) * 0.5);
                        var cy = (float)((viewport?.Height > 0 ? viewport.Height : 800) * 0.45);
                        await _page.Mouse.MoveAsync(cx, cy).ConfigureAwait(false);
                        await _page.Mouse.WheelAsync(0, _random.Next(780, 1400)).ConfigureAwait(false);
                    }
                    else
                    {
                        await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // best-effort
                }

                await RandomDelayAsync(inForyouFeed ? 900 : 700, inForyouFeed ? 1600 : 1400, cancellationToken).ConfigureAwait(false);
                WarmupFeedIdentity identity;
                if (inForyouFeed)
                {
                    identity = await TryResolveForyouFeedIdentityAsync().ConfigureAwait(false);
                }
                else
                {
                    var pageUrl = _page?.Url ?? string.Empty;
                    identity = new WarmupFeedIdentity
                    {
                        VideoId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false),
                        Author = ExtractAuthorFromVideoUrl(pageUrl)
                    };
                }

                newId = identity.VideoId;
                if (IsWarmupFeedContentExcluded(
                        identity.VideoId,
                        identity.Author,
                        identity.Caption,
                        checkPersistedVideoHistory: inForyouFeed))
                {
                    // Cùng clip = nút ↓ chưa chuyển được (player trắng/kẹt), KHÔNG phải «gặp lại clip cũ trên feed».
                    if (string.Equals(newId, previousId, StringComparison.Ordinal))
                    {
                        logAction?.Invoke(
                            "[LIVE] Vẫn kẹt clip @"
                            + (identity.Author ?? "?")
                            + " — thử chuyển video mạnh hơn (focus player / ↓ / wheel)...");
                        excludedStreak++;
                        if (inBrowseOverlay)
                        {
                            await ForceBrowseAdvanceFromStuckAsync(cancellationToken, logAction).ConfigureAwait(false);
                        }
                        else
                        {
                            try
                            {
                                await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                                await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                            }
                            catch
                            {
                            }
                        }

                        if (inBrowseOverlay && excludedStreak >= 4)
                        {
                            logAction?.Invoke("[LIVE] Browse không thoát được clip kẹt sau nhiều lần thử.");
                            throw new ShopVideoGateException("Browse feed stuck on excluded clip.");
                        }

                        continue;
                    }

                    logAction?.Invoke(
                        "[LIVE] Gặp lại clip đã xem (@"
                        + (identity.Author ?? "?")
                        + ") — lướt tiếp.");
                    var fp = identity.Fingerprint ?? string.Empty;
                    var excludeKey = !string.IsNullOrWhiteSpace(newId) ? newId : fp;
                    if ((!string.IsNullOrWhiteSpace(excludeKey) &&
                         string.Equals(excludeKey, lastExcludedVideoId ?? lastExcludedFingerprint ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        || (string.IsNullOrWhiteSpace(excludeKey) &&
                            string.Equals(fp, lastExcludedFingerprint, StringComparison.OrdinalIgnoreCase)))
                    {
                        excludedStreak++;
                    }
                    else
                    {
                        lastExcludedFingerprint = fp;
                        lastExcludedVideoId = newId;
                        excludedStreak = 1;
                    }

                    if (inForyouFeed && excludedStreak >= 4)
                    {
                        logAction?.Invoke("[LIVE] For You kẹt clip cũ liên tiếp — reset feed và lướt mạnh.");
                        await BreakForyouFeedStuckAsync(cancellationToken, logAction).ConfigureAwait(false);
                        excludedStreak = 0;
                        lastExcludedFingerprint = null;
                        lastExcludedVideoId = null;
                        previousId = null;
                        previousFingerprint = null;
                    }
                    else if (inBrowseOverlay && excludedStreak >= 3)
                    {
                        logAction?.Invoke("[LIVE] Browse gặp nhiều clip đã xem liên tiếp — thử lướt mạnh.");
                        await ForceBrowseAdvanceFromStuckAsync(cancellationToken, logAction).ConfigureAwait(false);
                        excludedStreak = 0;
                        lastExcludedFingerprint = null;
                        lastExcludedVideoId = null;
                    }
                    else
                    {
                        previousId = newId;
                    }

                    continue;
                }

                excludedStreak = 0;
                lastExcludedFingerprint = null;
                if (inForyouFeed &&
                    !string.IsNullOrWhiteSpace(identity.Fingerprint) &&
                    !string.IsNullOrWhiteSpace(previousFingerprint) &&
                    !string.Equals(identity.Fingerprint, previousFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    advanced = true;
                    break;
                }

                if (!string.IsNullOrWhiteSpace(newId) &&
                    !string.Equals(newId, previousId, StringComparison.Ordinal))
                {
                    advanced = true;
                    break;
                }
            }

            if (!advanced || string.IsNullOrWhiteSpace(newId))
            {
                if (!string.IsNullOrWhiteSpace(previousId))
                {
                    ExcludeWarmupVideoId(previousId);
                }

                throw new ShopVideoGateException("Không lướt được sang video tiếp theo trong feed.");
            }

            await PrepareCurrentFeedVideoForWarmupAsync(cancellationToken, logAction, excludeOwnUniqueId)
                .ConfigureAwait(false);
            logAction?.Invoke("[LIVE] Đã lướt sang video mới: " + newId);
        }

        /// <summary>Giao diện browse từ search: video trái + bình luận phải + nút lên/xuống.</summary>
        private async Task<bool> IsBrowseVideoOverlayAsync()
        {
            if (_warmupInForyouMode)
            {
                return false;
            }

            try
            {
                return await _page.EvaluateAsync<bool>(@"() => {
                    const url = location.href || '';
                    if (url.indexOf('/foryou') >= 0) return false;
                    const path = location.pathname || '';
                    if ((path === '/' || path === '') && url.indexOf('?q=') < 0 && url.indexOf('/video/') < 0) {
                        return false;
                    }
                    const hasBrowseUi = !!document.querySelector(
                        '[data-e2e=""browse-comment-list""], [data-e2e=""browse-comment""], [data-e2e=""browse-comment-icon""]'
                    );
                    return hasBrowseUi || (url.indexOf('/video/') >= 0 && url.indexOf('?q=') >= 0);
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsForyouFeedPageAsync()
        {
            try
            {
                var url = _page?.Url ?? string.Empty;
                if (_warmupInForyouMode)
                {
                    if (url.IndexOf("/search", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        url.IndexOf("?q=", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }

                    if (url.IndexOf("?q=", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }

                    return true;
                }

                if (url.IndexOf("/foryou", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                return await _page.EvaluateAsync<bool>(@"() => {
                    if (!document.querySelector('video')) return false;
                    const url = location.href || '';
                    if (url.indexOf('/foryou') >= 0) return true;
                    if (url.indexOf('/search') >= 0) return false;
                    const path = location.pathname || '/';
                    if (path === '/' || path === '') return true;
                    return !!document.querySelector('[data-e2e=""video-author-uniqueid""], [data-e2e=""browse-video""]');
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> WaitForWarmupVideoIdAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return true;
                }

                await Task.Delay(450, cancellationToken).ConfigureAwait(false);
            }

            var last = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(last))
            {
                logAction?.Invoke("[LIVE] For You: timeout chờ video id trên feed.");
            }

            return !string.IsNullOrWhiteSpace(last);
        }

        /// <summary>Bấm nút ↓ (video kế) trên browse overlay search.</summary>
        private async Task<bool> TryClickBrowseNextVideoButtonAsync(
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            await FocusBrowsePlayerAsync(cancellationToken).ConfigureAwait(false);

            foreach (var selector in TikTokSelectors.BrowseNextVideoButtons)
            {
                try
                {
                    var btn = _page.Locator(selector).First;
                    if (!await IsVisibleAsync(btn, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await HumanClickLocatorAsync(btn, cancellationToken).ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] Đã bấm nút ↓ chuyển video (browse).");
                    return true;
                }
                catch
                {
                }
            }

            try
            {
                var clicked = await _page.EvaluateAsync<bool>(@"() => {
                    const video = document.querySelector('video');
                    let vr = null;
                    if (video) {
                        const r = video.getBoundingClientRect();
                        if (r.width > 80 && r.height > 120) vr = r;
                    }
                    if (!vr) {
                        const player = document.querySelector('[data-e2e=""browse-video""], [class*=""BrowseVideo""], main');
                        if (player) {
                            const pr = player.getBoundingClientRect();
                            if (pr.width > 200 && pr.height > 200) {
                                vr = { left: pr.left, top: pr.top, right: pr.left + pr.width * 0.62, bottom: pr.bottom, width: pr.width * 0.62, height: pr.height };
                            }
                        }
                    }
                    if (!vr) {
                        const w = window.innerWidth;
                        const h = window.innerHeight;
                        vr = { left: 0, top: 0, right: w * 0.58, bottom: h, width: w * 0.58, height: h };
                    }
                    const isNavBtn = (el) => {
                        const r = el.getBoundingClientRect();
                        if (r.width < 18 || r.height < 18 || r.width > 96 || r.height > 96) return false;
                        const cx = r.left + r.width / 2;
                        return cx >= vr.right - 110 && cx <= vr.right + 48
                            && r.top >= vr.top + 36 && r.bottom <= vr.bottom - 36;
                    };
                    const btns = Array.from(document.querySelectorAll('button, [role=""button""]'))
                        .filter(isNavBtn)
                        .sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top);
                    if (btns.length >= 2) {
                        try { btns[btns.length - 1].click(); return true; } catch (e) {}
                    }
                    if (btns.length === 1) {
                        const r = btns[0].getBoundingClientRect();
                        if (r.top > vr.top + vr.height * 0.42) {
                            try { btns[0].click(); return true; } catch (e) {}
                        }
                    }
                    return false;
                }").ConfigureAwait(false);

                if (clicked)
                {
                    logAction?.Invoke("[LIVE] Đã bấm nút ↓ (DOM nav bên player).");
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private async Task FocusBrowsePlayerAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _page.EvaluateAsync(@"() => {
                    const targets = [
                        document.querySelector('[data-e2e=""browse-video""]'),
                        document.querySelector('video'),
                        document.querySelector('main')
                    ];
                    for (const el of targets) {
                        if (!el) continue;
                        try {
                            el.focus({ preventScroll: true });
                            el.click();
                            return;
                        } catch (e) {}
                    }
                }").ConfigureAwait(false);
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task ForceBrowseAdvanceFromStuckAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            await FocusBrowsePlayerAsync(cancellationToken).ConfigureAwait(false);
            if (await TryClickBrowseNextVideoButtonAsync(cancellationToken, logAction).ConfigureAwait(false))
            {
                await RandomDelayAsync(700, 1200, cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                await RandomDelayAsync(400, 700, cancellationToken).ConfigureAwait(false);
                await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
            }
            catch
            {
            }

            try
            {
                var viewport = _page.ViewportSize;
                var cx = (float)((viewport?.Width > 0 ? viewport.Width : 900) * 0.32);
                var cy = (float)((viewport?.Height > 0 ? viewport.Height : 800) * 0.5);
                await _page.Mouse.MoveAsync(cx, cy).ConfigureAwait(false);
                await _page.Mouse.WheelAsync(0, _random.Next(900, 1400)).ConfigureAwait(false);
            }
            catch
            {
            }

            await RandomDelayAsync(700, 1200, cancellationToken).ConfigureAwait(false);
        }

        private async Task ClickBrowsePlayerCenterAsync(CancellationToken cancellationToken)
        {
            try
            {
                var json = await _page.EvaluateAsync<string>(@"() => {
                    const video = document.querySelector('video');
                    if (video) {
                        const r = video.getBoundingClientRect();
                        if (r.width > 80 && r.height > 120) {
                            return JSON.stringify({ x: r.left, y: r.top, width: r.width, height: r.height });
                        }
                    }
                    const player = document.querySelector('[data-e2e=""browse-video""], [class*=""BrowseVideo""]');
                    if (player) {
                        const r = player.getBoundingClientRect();
                        if (r.width > 200 && r.height > 200) {
                            return JSON.stringify({
                                x: r.left + r.width * 0.08,
                                y: r.top + r.height * 0.12,
                                width: r.width * 0.55,
                                height: r.height * 0.76
                            });
                        }
                    }
                    const w = window.innerWidth;
                    const h = window.innerHeight;
                    return JSON.stringify({ x: w * 0.08, y: h * 0.12, width: w * 0.48, height: h * 0.76 });
                }").ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var box = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);
                if (box == null || !box.TryGetValue("width", out var width) || !box.TryGetValue("height", out var height)
                    || width <= 40 || height <= 40)
                {
                    return;
                }

                box.TryGetValue("x", out var bx);
                box.TryGetValue("y", out var by);
                var x = (float)(bx + width * (0.38 + _random.NextDouble() * 0.24));
                var y = (float)(by + height * (0.38 + _random.NextDouble() * 0.24));
                await _page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
                await Task.Delay(_random.Next(80, 180), cancellationToken).ConfigureAwait(false);
                await _page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        /// <summary>Khôi phục media trên browse overlay (player trắng: focus + play overlay + click vùng player).</summary>
        private async Task<bool> TryRecoverBrowseMediaAsync(
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (_warmupInForyouMode || !await IsBrowseVideoOverlayAsync().ConfigureAwait(false))
            {
                return false;
            }

            if (await IsVideoMediaPlayableAsync().ConfigureAwait(false))
            {
                return true;
            }

            await ThrowIfBrowseShopBlockedAsync(logAction, hoverPlayer: true).ConfigureAwait(false);

            logAction?.Invoke("[LIVE] Browse player chưa có frame — thử kích hoạt media (focus / play / click player)...");

            await FocusBrowsePlayerAsync(cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < 10 && !await IsVideoMediaPlayableAsync().ConfigureAwait(false); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            await TryEnsureVideoPlayingOnceAsync(cancellationToken, browseSafe: false, mediaRecovery: true)
                .ConfigureAwait(false);
            if (await WaitForVideoMediaReadyAsync(cancellationToken, TimeSpan.FromSeconds(6)).ConfigureAwait(false))
            {
                logAction?.Invoke("[LIVE] Media browse đã sẵn sàng sau khi bấm play.");
                return true;
            }

            await ClickBrowsePlayerCenterAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            await TryEnsureVideoPlayingOnceAsync(cancellationToken, browseSafe: false, mediaRecovery: true)
                .ConfigureAwait(false);
            if (await WaitForVideoMediaReadyAsync(cancellationToken, TimeSpan.FromSeconds(6)).ConfigureAwait(false))
            {
                logAction?.Invoke("[LIVE] Media browse đã sẵn sàng sau click vùng player.");
                return true;
            }

            try
            {
                var probe = await ProbeBrowseShopGateAsync(hoverPlayer: true).ConfigureAwait(false);
                if (probe.IsBlocked)
                {
                    logAction?.Invoke(
                        "[LIVE] Browse media vẫn chưa có frame — TikTok Shop ("
                        + (probe.Reason ?? "?")
                        + ") | hasVideo="
                        + probe.HasVideo
                        + ".");
                    await ThrowIfBrowseShopBlockedAsync(logAction, hoverPlayer: false).ConfigureAwait(false);
                }

                var pageProbe = await ProbePageEngagementStateAsync().ConfigureAwait(false);
                logAction?.Invoke(
                    "[LIVE] Browse media vẫn chưa có frame"
                    + (pageProbe.TryGetValue("hasVideo", out var hv) ? " | hasVideo=" + hv : string.Empty)
                    + (pageProbe.TryGetValue("videoReadyState", out var rs) ? " | readyState=" + rs : string.Empty)
                    + (pageProbe.TryGetValue("playOverlayVisible", out var po) ? " | playOverlay=" + po : string.Empty)
                    + ".");
            }
            catch (ShopVideoGateException)
            {
                throw;
            }
            catch
            {
            }

            return await IsVideoMediaPlayableAsync().ConfigureAwait(false);
        }

        /// <summary>Chỉ đóng modal/drawer che UI — không Escape (đóng browse player).</summary>
        private async Task TryCloseCommentsUiForFeedAdvanceAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (await IsBrowseVideoOverlayAsync().ConfigureAwait(false))
            {
                return;
            }

            try
            {
                var closed = await _page.EvaluateAsync<bool>(@"() => {
                    let hit = false;
                    const sels = [
                        '[data-e2e=""comment-close""]',
                        'button[aria-label*=""Close""]',
                        'button[aria-label*=""Đóng""]',
                        '[class*=""DivCloseWrapper""] button',
                        '[class*=""CloseContainer""] button'
                    ];
                    for (const s of sels) {
                        const el = document.querySelector(s);
                        if (!el) continue;
                        const style = window.getComputedStyle(el);
                        if (style && (style.display === 'none' || style.visibility === 'hidden')) continue;
                        try { el.click(); hit = true; } catch (e) {}
                    }
                    return hit;
                }").ConfigureAwait(false);

                if (closed)
                {
                    logAction?.Invoke("[LIVE] Đã đóng overlay che UI trước khi lướt.");
                    await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
            }
        }

        /// <summary>Id video đang xem: URL trước, fallback DOM (browse/feed).</summary>
        private async Task<string> ResolveCurrentWarmupVideoIdAsync()
        {
            if (_warmupInForyouMode)
            {
                var identity = await TryResolveForyouFeedIdentityAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(identity.VideoId))
                {
                    return identity.VideoId;
                }
            }

            var fromUrl = ExtractVideoIdFromUrl(_page?.Url ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(fromUrl) && !_warmupInForyouMode)
            {
                return fromUrl;
            }

            try
            {
                var fromDom = await _page.EvaluateAsync<string>(@"() => {
                    const path = location.pathname || '';
                    let m = path.match(/\/video\/(\d+)/);
                    if (m) return m[1];
                    const og = document.querySelector('meta[property=""og:url""]');
                    if (og) {
                        m = String(og.content || '').match(/\/video\/(\d+)/);
                        if (m) return m[1];
                    }
                    const extractId = (href) => {
                        if (!href) return '';
                        m = String(href).match(/\/video\/(\d+)/);
                        return m ? m[1] : '';
                    };
                    const vh = window.innerHeight;
                    const vw = window.innerWidth;
                    const cy = vh / 2;
                    const videos = Array.from(document.querySelectorAll('video'));
                    let bestVideo = null;
                    let bestScore = -1;
                    for (const v of videos) {
                        const r = v.getBoundingClientRect();
                        if (r.width < 80 || r.height < 120) continue;
                        const visTop = Math.max(0, r.top);
                        const visBot = Math.min(vh, r.bottom);
                        const visH = Math.max(0, visBot - visTop);
                        if (visH < r.height * 0.35) continue;
                        const vcy = r.top + r.height / 2;
                        const dist = Math.abs(vcy - cy);
                        const score = (r.width * visH) / (1 + dist * 0.02);
                        if (score > bestScore) { bestScore = score; bestVideo = v; }
                    }
                    const video = bestVideo || document.querySelector('video');
                    if (video) {
                        let n = video.parentElement;
                        for (let i = 0; i < 14 && n; i++, n = n.parentElement) {
                            const a = n.querySelector && n.querySelector('a[href*=""/video/""]');
                            const id = extractId(a && a.getAttribute('href'));
                            if (id) return id;
                        }
                        const vr = video.getBoundingClientRect();
                        const vcy = vr.top + vr.height / 2;
                        let best = '';
                        let bestDist = 1e9;
                        const links = Array.from(document.querySelectorAll('a[href*=""/video/""]'));
                        for (const a of links) {
                            const r = a.getBoundingClientRect();
                            if (r.width <= 0 || r.height <= 0) continue;
                            if (r.bottom < 0 || r.top > vh) continue;
                            const acy = r.top + r.height / 2;
                            const dist = Math.abs(acy - vcy);
                            if (dist < bestDist && dist < vr.height * 0.65) {
                                bestDist = dist;
                                best = extractId(a.getAttribute('href'));
                            }
                        }
                        if (best) return best;
                    }
                    const author = document.querySelector('[data-e2e=""video-author-uniqueid""] a, [data-e2e=""browse-username""] a');
                    if (author) {
                        const id = extractId(author.getAttribute('href'));
                        if (id) return id;
                    }
                    const links = Array.from(document.querySelectorAll('a[href*=""/video/""]'));
                    for (const a of links) {
                        const r = a.getBoundingClientRect();
                        if (r.width > 40 && r.height > 20 && r.top >= -20 && r.top < window.innerHeight * 0.9) {
                            const id = extractId(a.getAttribute('href'));
                            if (id) return id;
                        }
                    }
                    return '';
                }").ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(fromDom))
                {
                    return fromDom;
                }
            }
            catch
            {
            }

            return fromUrl ?? string.Empty;
        }

        private sealed class WarmupFeedIdentity
        {
            public string VideoId { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public string Caption { get; set; } = string.Empty;
            public string Fingerprint { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
        }

        private sealed class VideoProgressSnapshot
        {
            public double current { get; set; }
            public double duration { get; set; }
        }

        /// <summary>For You: @ + caption + id từ state SPA / slide giữa màn hình.</summary>
        private async Task<WarmupFeedIdentity> TryResolveForyouFeedIdentityAsync()
        {
            var result = new WarmupFeedIdentity();
            try
            {
                var json = await _page.EvaluateAsync<string>(@"() => {
                    const out = { id: '', author: '', caption: '', source: '' };
                    const extractId = (href) => {
                        if (!href) return '';
                        const m = String(href).match(/\/video\/(\d+)/);
                        return m ? m[1] : '';
                    };
                    const pickText = (el) => {
                        if (!el) return '';
                        const t = (el.innerText || el.textContent || '').trim();
                        return t.length > 1 ? t : '';
                    };
                    try {
                        const root = window.__UNIVERSAL_DATA_FOR_REHYDRATION__;
                        if (root) {
                            const scope = root.__DEFAULT_SCOPE__ || {};
                            const vd = scope['webapp.video-detail'];
                            if (vd && vd.itemInfo && vd.itemInfo.itemStruct) {
                                const it = vd.itemInfo.itemStruct;
                                out.id = String(it.id || it.aweme_id || '');
                                out.author = ((it.author && it.author.uniqueId) || '').replace(/^@/, '');
                                out.caption = it.desc || '';
                                out.source = 'universal-video-detail';
                                if (out.id) return JSON.stringify(out);
                            }
                        }
                    } catch (e) {}
                    const vh = window.innerHeight;
                    const cy = vh / 2;
                    let bestVideo = null;
                    let bestScore = -1;
                    for (const v of document.querySelectorAll('video')) {
                        const r = v.getBoundingClientRect();
                        if (r.width < 80 || r.height < 120) continue;
                        const visTop = Math.max(0, r.top);
                        const visBot = Math.min(vh, r.bottom);
                        const visH = Math.max(0, visBot - visTop);
                        if (visH < r.height * 0.35) continue;
                        const vcy = r.top + r.height / 2;
                        const dist = Math.abs(vcy - cy);
                        const score = (r.width * visH) / (1 + dist * 0.02);
                        if (score > bestScore) { bestScore = score; bestVideo = v; }
                    }
                    if (!bestVideo) return JSON.stringify(out);
                    const vr = bestVideo.getBoundingClientRect();
                    let slide = bestVideo.parentElement;
                    for (let i = 0; i < 22 && slide; i++, slide = slide.parentElement) {
                        if (!out.author) {
                            const authorEl = slide.querySelector('[data-e2e=""video-author-uniqueid""], [data-e2e=""browse-username""]');
                            if (authorEl) {
                                const href = authorEl.getAttribute && authorEl.getAttribute('href');
                                const hm = href && href.match(/\/@([^/?#]+)/);
                                out.author = hm ? hm[1] : pickText(authorEl).replace(/^@/, '');
                                const authorLink = slide.querySelector('a[href*=""/@""][href*=""/video/""]')
                                    || authorEl.closest && authorEl.closest('a[href*=""/video/""]')
                                    || (authorEl.tagName === 'A' ? authorEl : authorEl.querySelector && authorEl.querySelector('a[href*=""/video/""]'));
                                if (authorLink && !out.id) {
                                    out.id = extractId(authorLink.getAttribute('href'));
                                }
                            }
                        }
                        if (!out.caption) {
                            const descEl = slide.querySelector('[data-e2e=""video-desc""], [data-e2e=""browse-video-desc""], [data-e2e=""video-description""]');
                            out.caption = pickText(descEl);
                        }
                        if (out.id && out.author && out.caption) break;
                    }
                    if (!out.id) {
                        const um = (location.pathname || '').match(/\/video\/(\d+)/);
                        if (um) out.id = um[1];
                    }
                    out.source = out.source || 'centered-slide';
                    return JSON.stringify(out);
                }").ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var jo = JObject.Parse(json);
                    result.VideoId = (jo["id"]?.ToString() ?? string.Empty).Trim();
                    result.Author = (jo["author"]?.ToString() ?? string.Empty).Trim().TrimStart('@');
                    result.Caption = (jo["caption"]?.ToString() ?? string.Empty).Trim();
                    result.Source = (jo["source"]?.ToString() ?? string.Empty).Trim();
                }
            }
            catch
            {
            }

            if ((DateTime.UtcNow - _foryouNetworkVideoIdUtc).TotalSeconds <= 10 &&
                !string.IsNullOrWhiteSpace(_foryouNetworkVideoId) &&
                string.IsNullOrWhiteSpace(result.VideoId))
            {
                result.VideoId = _foryouNetworkVideoId;
                result.Source = string.IsNullOrWhiteSpace(result.Source) ? "network" : result.Source + "+network";
            }

            result.Fingerprint = BuildWarmupContentFingerprint(result.Author, result.Caption);
            if (string.IsNullOrWhiteSpace(result.VideoId) && !string.IsNullOrWhiteSpace(result.Fingerprint))
            {
                result.VideoId = "fp-" + Math.Abs(result.Fingerprint.GetHashCode()).ToString();
            }

            return result;
        }

        private void BeginForyouFeedCapture()
        {
            if (_page == null)
            {
                return;
            }

            EndForyouFeedCapture();
            _foryouFeedResponseHandler = (_, response) => { _ = CaptureForyouFeedResponseAsync(response); };
            _page.Response += _foryouFeedResponseHandler;
        }

        private void EndForyouFeedCapture()
        {
            if (_page != null && _foryouFeedResponseHandler != null)
            {
                try
                {
                    _page.Response -= _foryouFeedResponseHandler;
                }
                catch
                {
                }
            }

            _foryouFeedResponseHandler = null;
        }

        private async Task CaptureForyouFeedResponseAsync(IResponse response)
        {
            try
            {
                if (response == null || response.Status != 200)
                {
                    return;
                }

                var url = response.Url ?? string.Empty;
                if (url.IndexOf("recommend", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("item_list", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("aweme", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var body = await response.TextAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(body))
                {
                    return;
                }

                string lastId = null;
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(
                             body,
                             @"""aweme_id""\s*:\s*""(\d+)""|""itemId""\s*:\s*""(\d+)""|""id""\s*:\s*""(\d{15,})"""))
                {
                    var id = m.Groups[1].Success ? m.Groups[1].Value
                        : m.Groups[2].Success ? m.Groups[2].Value
                        : m.Groups[3].Value;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        lastId = id;
                    }
                }

                if (!string.IsNullOrWhiteSpace(lastId))
                {
                    _foryouNetworkVideoId = lastId;
                    _foryouNetworkVideoIdUtc = DateTime.UtcNow;
                }
            }
            catch
            {
            }
        }

        private async Task QuickForyouFeedNudgeAsync(CancellationToken cancellationToken)
        {
            await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
            await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
        }

        private async Task BreakForyouFeedStuckAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            _warmupLockedVideoUrl = null;
            _warmupLockedVideoId = null;
            await ClearWarmupPlaybackGuardsAsync().ConfigureAwait(false);
            await EnsureSharePanelClosedAsync(cancellationToken, logAction).ConfigureAwait(false);

            for (var burst = 0; burst < 3; burst++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SwipeUpOnVideoFeedAsync(cancellationToken).ConfigureAwait(false);
                await _page.Keyboard.PressAsync("ArrowDown").ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            var unstuck = await TryResolveForyouFeedIdentityAsync().ConfigureAwait(false);
            if (!IsWarmupFeedContentExcluded(unstuck.VideoId, unstuck.Author, unstuck.Caption))
            {
                return;
            }

            logAction?.Invoke("[LIVE] Reload For You để thoát vòng lặp video cũ.");
            await SafeGotoAsync(TikTokForyouUrl, cancellationToken).ConfigureAwait(false);
            await RandomDelayAsync(1800, 2800, cancellationToken).ConfigureAwait(false);
            await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
            await QuickForyouFeedNudgeAsync(cancellationToken).ConfigureAwait(false);
            await RandomDelayAsync(900, 1400, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> TryResolveCurrentWarmupAuthorAsync()
        {
            try
            {
                var fromDom = await _page.EvaluateAsync<string>(@"() => {
                    const pick = (el) => {
                        if (!el) return '';
                        const href = el.getAttribute('href') || '';
                        const m = href.match(/\/@([^/?#]+)/);
                        if (m) return m[1];
                        const t = (el.innerText || el.textContent || '').trim().replace(/^@/, '');
                        return t.length <= 32 ? t : '';
                    };
                    const selectors = [
                        '[data-e2e=""browse-username""]',
                        '[data-e2e=""video-author-uniqueid""]',
                        '[data-e2e=""browse-user-avatar""]',
                        'a[href*=""/@""][data-e2e]'
                    ];
                    for (const sel of selectors) {
                        const el = document.querySelector(sel);
                        const v = pick(el);
                        if (v) return v;
                    }
                    const video = document.querySelector('video');
                    if (video) {
                        let n = video.parentElement;
                        for (let i = 0; i < 12 && n; i++, n = n.parentElement) {
                            const a = n.querySelector && n.querySelector('a[href*=""/@""]');
                            const v = pick(a);
                            if (v) return v;
                        }
                    }
                    return '';
                }").ConfigureAwait(false);

                return (fromDom ?? string.Empty).Trim().TrimStart('@');
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Trả về true nếu video active giữa viewport đang thực sự phát.</summary>
        private async Task<bool> IsVideoActuallyPlayingAsync()
        {
            try
            {
                var total = await _page.Locator("video").CountAsync().ConfigureAwait(false);
                if (total <= 0)
                {
                    return false;
                }

                var index = await ResolveActiveVideoIndexAsync().ConfigureAwait(false);
                var useIdx = index >= 0 && index < total ? index : 0;
                return await _page.Locator("video").Nth(useIdx).EvaluateAsync<bool>(@"v => {
                    if (!v) return false;
                    return !v.paused && !v.ended && v.currentTime > 0 && v.readyState >= 2;
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Click item trên lưới search để mở browse/feed (giữ context), thay vì goto URL trần.</summary>
        private async Task<bool> TryClickWarmupSearchVideoItemAsync(
            int oneBasedEligibleIndex,
            string excludeOwnUniqueId,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            try
            {
                var items = _page.Locator("div[data-e2e='search_video-item']");
                var count = await items.CountAsync().ConfigureAwait(false);
                var pick = oneBasedEligibleIndex;
                ILocator targetLink = null;
                for (var i = 0; i < count; i++)
                {
                    var item = items.Nth(i);
                    if (!await IsWarmupSearchItemEligibleAsync(item, excludeOwnUniqueId).ConfigureAwait(false))
                    {
                        continue;
                    }

                    pick--;
                    if (pick == 0)
                    {
                        targetLink = item.Locator("a[href*='/video/']").First;
                        break;
                    }
                }

                if (targetLink == null || await targetLink.CountAsync().ConfigureAwait(false) == 0)
                {
                    return false;
                }

                var beforeId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                var beforeUrl = _page.Url ?? string.Empty;

                try
                {
                    await targetLink.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                }
                catch
                {
                }

                await RandomDelayAsync(200, 500, cancellationToken).ConfigureAwait(false);
                await targetLink.ClickAsync(new LocatorClickOptions { Timeout = 8000 }).ConfigureAwait(false);
                logAction?.Invoke("[LIVE] Đã click video trên lưới search (giữ browse/feed).");

                var deadline = DateTime.UtcNow.AddSeconds(12);
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var url = _page.Url ?? string.Empty;
                    var id = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                    var onVideo = url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0;
                    var changed = (!string.IsNullOrWhiteSpace(id) &&
                                   !string.Equals(id, beforeId, StringComparison.Ordinal)) ||
                                  (!string.Equals(url, beforeUrl, StringComparison.OrdinalIgnoreCase) && onVideo);
                    if (onVideo && (changed || !string.IsNullOrWhiteSpace(id)))
                    {
                        await RandomDelayAsync(600, 1200, cancellationToken).ConfigureAwait(false);
                        return true;
                    }

                    // Browse overlay trên search: có <video> lớn dù URL chưa đổi.
                    try
                    {
                        var hasPlayer = await _page.EvaluateAsync<bool>(@"() => {
                            const v = document.querySelector('video');
                            if (!v) return false;
                            const r = v.getBoundingClientRect();
                            return r.width > 120 && r.height > 180;
                        }").ConfigureAwait(false);
                        if (hasPlayer && !string.IsNullOrWhiteSpace(id))
                        {
                            await RandomDelayAsync(600, 1200, cancellationToken).ConfigureAwait(false);
                            return true;
                        }
                    }
                    catch
                    {
                    }

                    await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                }

                return (_page.Url ?? string.Empty).IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LIVE] Click search item lỗi: " + ex.Message);
                return false;
            }
        }

        /// <summary>Mô phỏng ngón tay lướt lên trên vùng video (next clip).</summary>
        private async Task SwipeUpOnVideoFeedAsync(CancellationToken cancellationToken)
        {
            var viewport = _page.ViewportSize;
            double width = viewport?.Width > 0 ? viewport.Width : 900;
            double height = viewport?.Height > 0 ? viewport.Height : 800;
            float startX = (float)(width * 0.5);
            float startY = (float)(height * 0.72);
            float endY = (float)(height * 0.22);

            try
            {
                var boxJson = await _page.EvaluateAsync<string>(@"() => {
                    const vh = window.innerHeight;
                    const cy = vh / 2;
                    let best = null;
                    let bestScore = -1;
                    for (const v of document.querySelectorAll('video')) {
                        const r = v.getBoundingClientRect();
                        if (r.width < 80 || r.height < 120) continue;
                        const visTop = Math.max(0, r.top);
                        const visBot = Math.min(vh, r.bottom);
                        const visH = Math.max(0, visBot - visTop);
                        if (visH < r.height * 0.35) continue;
                        const vcy = r.top + r.height / 2;
                        const dist = Math.abs(vcy - cy);
                        const score = (r.width * visH) / (1 + dist * 0.02);
                        if (score > bestScore) { bestScore = score; best = r; }
                    }
                    if (!best) return '';
                    return JSON.stringify({ x: best.x, y: best.y, w: best.width, h: best.height });
                }").ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(boxJson))
                {
                    var jo = JObject.Parse(boxJson);
                    var bx = jo.Value<double>("x");
                    var by = jo.Value<double>("y");
                    var bw = jo.Value<double>("w");
                    var bh = jo.Value<double>("h");
                    if (bw > 40 && bh > 80)
                    {
                        startX = (float)(bx + bw * (0.45 + _random.NextDouble() * 0.1));
                        startY = (float)(by + bh * 0.72);
                        endY = (float)(by + bh * 0.22);
                        await _page.Mouse.MoveAsync(startX, startY).ConfigureAwait(false);
                        await Task.Delay(_random.Next(40, 90), cancellationToken).ConfigureAwait(false);
                        await _page.Mouse.DownAsync().ConfigureAwait(false);
                        await _page.Mouse.MoveAsync(startX, endY, new MouseMoveOptions { Steps = _random.Next(12, 22) })
                            .ConfigureAwait(false);
                        await Task.Delay(_random.Next(30, 80), cancellationToken).ConfigureAwait(false);
                        await _page.Mouse.UpAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch
            {
            }

            await _page.Mouse.MoveAsync(startX, startY).ConfigureAwait(false);
            await _page.Mouse.DownAsync().ConfigureAwait(false);
            await _page.Mouse.MoveAsync(startX, endY, new MouseMoveOptions { Steps = 16 }).ConfigureAwait(false);
            await _page.Mouse.UpAsync().ConfigureAwait(false);
        }

        private async Task ClearWarmupPlaybackGuardsAsync()
        {
            try
            {
                await _page.EvaluateAsync(@"() => {
                    if (window.__warmupPlaybackCleanup) {
                        try { window.__warmupPlaybackCleanup(); } catch (e) {}
                        window.__warmupPlaybackCleanup = null;
                    }
                    window.__warmupSeenNearEnd = false;
                }").ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task ForceCanonicalWarmupVideoPageAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(_warmupLockedVideoUrl))
            {
                return;
            }

            var url = _page.Url ?? string.Empty;
            var onVideo = url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0;
            var id = ExtractVideoIdFromUrl(url);
            // Đúng video rồi thì KHÔNG reload / không strip ?q= — giữ ngữ cảnh feed để lướt clip kế.
            if (onVideo && string.Equals(id, _warmupLockedVideoId, StringComparison.Ordinal))
            {
                return;
            }

            logAction?.Invoke("[LIVE] Re-open canonical video URL: " + _warmupLockedVideoUrl);
            await SafeGotoAsync(_warmupLockedVideoUrl, cancellationToken).ConfigureAwait(false);
            await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);
            await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(12)).ConfigureAwait(false);
        }

        /// <summary>Chờ SPA TikTok thoát skeleton (có video hoặc nút tim/comment thật).</summary>
        private async Task<bool> WaitForVideoPageHydratedAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsVideoPageHydratedAsync().ConfigureAwait(false))
                {
                    return true;
                }

                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            }

            var ok = await IsVideoPageHydratedAsync().ConfigureAwait(false);
            if (!ok)
            {
                logAction?.Invoke("[LIVE] Trang vẫn skeleton sau " + (int)timeout.TotalSeconds + "s.");
            }

            return ok;
        }

        private async Task<bool> IsVideoPageHydratedAsync()
        {
            try
            {
                return await _page.EvaluateAsync<bool>(@"() => {
                    const vh = window.innerHeight;
                    const cy = vh / 2;
                    const videos = Array.from(document.querySelectorAll('video'));
                    for (const v of videos) {
                        const r = v.getBoundingClientRect();
                        if (r.width > 80 && r.height > 120) {
                            const visH = Math.max(0, Math.min(vh, r.bottom) - Math.max(0, r.top));
                            if (visH >= r.height * 0.35 && Math.abs(r.top + r.height / 2 - cy) < vh * 0.45) {
                                return true;
                            }
                        }
                    }
                    const like = document.querySelector('[data-e2e=""like-icon""], [data-e2e=""browse-like-icon""]');
                    if (like) {
                        const r = like.getBoundingClientRect();
                        if (r.width > 8 && r.height > 8) return true;
                    }
                    const author = document.querySelector('[data-e2e=""browse-username""], [data-e2e=""video-author-uniqueid""], a[href*=""@""][data-e2e]');
                    if (author) {
                        const t = (author.innerText || '').trim();
                        if (t.length > 1) return true;
                    }
                    return false;
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task TryDismissBlockingOverlaysAsync(CancellationToken cancellationToken, Action<string> logAction = null)
        {
            try
            {
                var dismissed = await _page.EvaluateAsync<bool>(@"() => {
                    let hit = false;
                    const overlays = document.querySelectorAll(
                        '.TUXModal-overlay, [class*=""Modal-overlay""], [data-e2e=""modal-close-inner-button""], [class*=""DivModalContainer""]'
                    );
                    for (const el of overlays) {
                        const style = window.getComputedStyle(el);
                        if (style && style.display === 'none') continue;
                        const close = el.querySelector(
                            'button[aria-label*=""Close""], button[aria-label*=""Đóng""], [data-e2e=""modal-close-inner-button""], [class*=""Close""]'
                        );
                        if (close) { try { close.click(); hit = true; } catch (e) {} }
                    }
                    // Login / cookie banners
                    const btns = Array.from(document.querySelectorAll('button, [role=""button""]'));
                    for (const b of btns) {
                        const t = (b.innerText || '').trim().toLowerCase();
                        if (t === 'đóng' || t === 'close' || t === 'not now' || t === 'để sau' || t === 'reject all') {
                            try { b.click(); hit = true; } catch (e) {}
                        }
                    }
                    return hit;
                }").ConfigureAwait(false);

                if (dismissed)
                {
                    logAction?.Invoke("[LIVE] Đã đóng overlay/modal che UI.");
                    await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                }

                await TryDismissSharePanelAsync(cancellationToken, logAction).ConfigureAwait(false);

                // KHÔNG bấm Escape ở đây — trên trang browse/video Escape đóng player và
                // mất URL /video/{id}, khiến logic drift tưởng lệch và goto lại trang đơn.
            }
            catch
            {
            }
        }

        private async Task<bool> IsSharePanelOpenAsync()
        {
            try
            {
                return await _page.EvaluateAsync<bool>(@"() => {
                    const body = document.body ? document.body.innerText : '';
                    if (body.indexOf('Chia sẻ đến') >= 0 || body.indexOf('Share to') >= 0) return true;
                    const dlg = document.querySelector('[role=""dialog""], [class*=""ShareLayout""], [class*=""share-layout""]');
                    if (!dlg) return false;
                    const t = (dlg.innerText || '').trim();
                    return t.indexOf('Chia sẻ') >= 0 || t.indexOf('Share') >= 0;
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Đóng panel «Chia sẻ đến» — Escape thường không đủ trên TikTok web.</summary>
        private async Task<bool> TryDismissSharePanelAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (!await IsSharePanelOpenAsync().ConfigureAwait(false))
            {
                return false;
            }

            try
            {
                var closed = await _page.EvaluateAsync<bool>(@"() => {
                    const isShareTitle = (t) => {
                        const s = (t || '').trim();
                        return s.indexOf('Chia sẻ đến') >= 0 || s.indexOf('Share to') >= 0 || s === 'Chia sẻ' || s === 'Share';
                    };
                    const tryClick = (el) => {
                        if (!el) return false;
                        try { el.click(); return true; } catch (e) { return false; }
                    };
                    // Nút X / Close trong header panel share
                    const dialogs = Array.from(document.querySelectorAll(
                        '[role=""dialog""], [class*=""Modal""], [class*=""Share""], section, div'
                    ));
                    for (const dlg of dialogs) {
                        const txt = dlg.innerText || '';
                        if (!isShareTitle(txt) && txt.indexOf('Chia sẻ đến') < 0 && txt.indexOf('Share to') < 0) continue;
                        const closeCandidates = dlg.querySelectorAll(
                            'button[aria-label*=""Close""], button[aria-label*=""Đóng""], ' +
                            '[data-e2e=""close-icon""], [data-e2e=""modal-close-inner-button""], ' +
                            '[data-e2e=""share-close""], [class*=""Close""] button, button[class*=""close""]'
                        );
                        for (const btn of closeCandidates) {
                            const r = btn.getBoundingClientRect();
                            if (r.width > 0 && r.height > 0 && tryClick(btn)) return true;
                        }
                        // Header row: nút cuối thường là X
                        const headerBtns = dlg.querySelectorAll('button');
                        if (headerBtns.length >= 2) {
                            const last = headerBtns[headerBtns.length - 1];
                            const lr = last.getBoundingClientRect();
                            if (lr.width > 0 && lr.width <= 56 && lr.height <= 56 && tryClick(last)) return true;
                        }
                    }
                    const globalClose = document.querySelector(
                        '[data-e2e=""modal-close-inner-button""], [data-e2e=""close-icon""]'
                    );
                    if (tryClick(globalClose)) return true;
                    const overlay = document.querySelector('.TUXModal-overlay, [class*=""Modal-overlay""]');
                    if (overlay) {
                        const st = window.getComputedStyle(overlay);
                        if (st && st.display !== 'none' && st.visibility !== 'hidden') {
                            tryClick(overlay);
                            return true;
                        }
                    }
                    return false;
                }").ConfigureAwait(false);

                if (closed)
                {
                    logAction?.Invoke("[LIVE] Đã đóng panel Chia sẻ đến.");
                    await Task.Delay(450, cancellationToken).ConfigureAwait(false);
                    return !await IsSharePanelOpenAsync().ConfigureAwait(false);
                }
            }
            catch
            {
            }

            // Toggle: bấm lại icon share để đóng (TikTok thường toggle panel).
            foreach (var selector in new[]
                     {
                         "[data-e2e='browse-share-icon']",
                         "[data-e2e='share-icon']"
                     })
            {
                try
                {
                    var btn = _page.Locator(selector).First;
                    if (await IsVisibleAsync(btn, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                    {
                        await btn.ClickAsync(new LocatorClickOptions { Timeout = 3000, ClickCount = 1 }).ConfigureAwait(false);
                        await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                        if (!await IsSharePanelOpenAsync().ConfigureAwait(false))
                        {
                            logAction?.Invoke("[LIVE] Đã đóng panel share (toggle icon).");
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private async Task EnsureSharePanelClosedAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            for (var i = 0; i < 4 && await IsSharePanelOpenAsync().ConfigureAwait(false); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await TryDismissSharePanelAsync(cancellationToken, logAction).ConfigureAwait(false);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }

            if (await IsSharePanelOpenAsync().ConfigureAwait(false))
            {
                logAction?.Invoke("[LIVE] WARN: Panel share vẫn mở — có thể che UI khi lướt video.");
            }
        }

        private async Task EnsureOnLockedWarmupVideoAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(_warmupLockedVideoId) || string.IsNullOrWhiteSpace(_warmupLockedVideoUrl))
            {
                return;
            }

            var currentId = ExtractVideoIdFromUrl(_page.Url ?? string.Empty);
            if (string.IsNullOrWhiteSpace(currentId))
            {
                currentId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
            }

            if (string.Equals(currentId, _warmupLockedVideoId, StringComparison.Ordinal) &&
                !await IsShopInAppGateBlockingAsync().ConfigureAwait(false))
            {
                return;
            }

            // For You: URL vẫn /foryou nhưng DOM id khớp — không goto (tránh mất feed).
            if (string.IsNullOrWhiteSpace(ExtractVideoIdFromUrl(_page.Url ?? string.Empty)) &&
                string.Equals(currentId, _warmupLockedVideoId, StringComparison.Ordinal))
            {
                return;
            }

            logAction?.Invoke(
                $"[LIVE] URL lệch hoặc Shop gate (đang {currentId ?? string.Empty}, cần {_warmupLockedVideoId}) — khôi phục video đã chọn.");
            // #region agent log
            DebugAgentLog.Write("F", "BrowserAutomation.EnsureOnLockedWarmupVideo", "restore", new { currentId, lockedId = _warmupLockedVideoId }, "post-fix");
            // #endregion

            if (await IsShopInAppGateBlockingAsync().ConfigureAwait(false))
            {
                throw new ShopVideoGateException();
            }

            await SafeGotoAsync(_warmupLockedVideoUrl, cancellationToken).ConfigureAwait(false);
            await RandomDelayAsync(1200, 2200, cancellationToken).ConfigureAwait(false);
            // Không strip ?q= — giữ browse/feed nếu URL khóa còn query.
            await StabilizeWarmupVideoPlaybackAsync(cancellationToken).ConfigureAwait(false);
            await EnsureVideoPlayingForWatchAsync(cancellationToken, logAction).ConfigureAwait(false);

            if (!string.Equals(ExtractVideoIdFromUrl(_page.Url ?? string.Empty), _warmupLockedVideoId, StringComparison.Ordinal))
            {
                var domId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                if (!string.Equals(domId, _warmupLockedVideoId, StringComparison.Ordinal))
                {
                    throw new ShopVideoGateException("Could not stay on the selected warmup video.");
                }
            }

            if (!await IsVideoMediaPlayableAsync().ConfigureAwait(false))
            {
                ExcludeCurrentWarmupVideo();
                throw new ShopVideoGateException("Video media failed to load after restore (black player).");
            }
        }

        public async Task<bool> IsShopInAppGateBlockingAsync()
        {
            var probe = await ProbeBrowseShopGateAsync().ConfigureAwait(false);
            return probe.IsBlocked;
        }

        private sealed class BrowseShopGateProbe
        {
            public bool IsBlocked { get; set; }
            public string Reason { get; set; }
            public bool HasVideo { get; set; }
        }

        /// <summary>Phát hiện TikTok Shop kể cả tooltip/title (không có trong body.innerText).</summary>
        private async Task<BrowseShopGateProbe> ProbeBrowseShopGateAsync(bool hoverPlayer = false)
        {
            if (hoverPlayer)
            {
                try
                {
                    await ClickBrowsePlayerCenterAsync(CancellationToken.None).ConfigureAwait(false);
                    await Task.Delay(350).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            try
            {
                var json = await _page.EvaluateAsync<string>(@"() => {
                    const lower = (s) => (s || '').toLowerCase();
                    const shopPatterns = [
                        'tiktok shop',
                        'xem video tiktok shop',
                        'xem trong ứng dụng',
                        'trong ứng dụng tiktok',
                        'ứng dụng tiktok',
                        'watch tiktok shop',
                        'open in app',
                        'open tiktok'
                    ];
                    const hasShopText = (text) => {
                        const t = lower(text);
                        for (let i = 0; i < shopPatterns.length; i++) {
                            if (t.indexOf(shopPatterns[i]) >= 0) return true;
                        }
                        return false;
                    };
                    const collectText = (el) => {
                        if (!el) return '';
                        return (el.getAttribute('title') || '') + ' '
                            + (el.getAttribute('aria-label') || '') + ' '
                            + (el.getAttribute('aria-description') || '') + ' '
                            + (el.getAttribute('data-e2e') || '') + ' '
                            + (el.innerText || '');
                    };

                    const bodyText = document.body ? document.body.innerText : '';
                    if (hasShopText(bodyText)) {
                        return JSON.stringify({ shop: true, reason: 'body', hasVideo: !!document.querySelector('video') });
                    }

                    const roots = [
                        document.querySelector('[data-e2e=""browse-video""]'),
                        document.querySelector('[class*=""BrowseVideo""]'),
                        document.querySelector('main'),
                        document.body
                    ].filter(Boolean);

                    for (const root of roots) {
                        const nodes = root.querySelectorAll('[title], [aria-label], [data-e2e], [role=""tooltip""], [class*=""Tooltip""], span, div, p, button, a');
                        for (const el of nodes) {
                            if (hasShopText(collectText(el))) {
                                return JSON.stringify({ shop: true, reason: 'dom-text', hasVideo: !!document.querySelector('video') });
                            }
                            const e2e = lower(el.getAttribute('data-e2e'));
                            if (e2e.indexOf('shop') >= 0) {
                                return JSON.stringify({ shop: true, reason: 'data-e2e', hasVideo: !!document.querySelector('video') });
                            }
                        }
                    }

                    const hasVideo = !!document.querySelector('video');
                    const browseUi = !!document.querySelector('[data-e2e=""browse-like-icon""], [data-e2e=""browse-comment-icon""]');
                    const playPlaceholder = !!document.querySelector(
                        '[data-e2e=""browse-play-icon""], [data-e2e=""play-icon""], [class*=""PlayIcon""], [class*=""play-icon""]'
                    );
                    const desc = document.querySelector('[data-e2e=""browse-video-desc""], [data-e2e=""video-desc""]');
                    const descText = desc ? (desc.innerText || '') : '';
                    const commerceHint = /shop|mua|giá|đặt hàng|tiktok shop/i.test(descText);

                    if (browseUi && !hasVideo && (playPlaceholder || commerceHint)) {
                        return JSON.stringify({ shop: true, reason: 'no-video-shop-placeholder', hasVideo: false });
                    }

                    return JSON.stringify({ shop: false, reason: '', hasVideo });
                }").ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new BrowseShopGateProbe();
                }

                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (obj == null)
                {
                    return new BrowseShopGateProbe();
                }

                return new BrowseShopGateProbe
                {
                    IsBlocked = obj.TryGetValue("shop", out var shop) && shop is bool blocked && blocked,
                    Reason = obj.TryGetValue("reason", out var reason) ? reason?.ToString() : null,
                    HasVideo = obj.TryGetValue("hasVideo", out var hv) && hv is bool has && has
                };
            }
            catch
            {
                return new BrowseShopGateProbe();
            }
        }

        private async Task ThrowIfBrowseShopBlockedAsync(Action<string> logAction, bool hoverPlayer = true)
        {
            var probe = await ProbeBrowseShopGateAsync(hoverPlayer).ConfigureAwait(false);
            if (!probe.IsBlocked)
            {
                return;
            }

            logAction?.Invoke(
                "[LIVE] Video TikTok Shop (chỉ xem trong app"
                + (string.IsNullOrWhiteSpace(probe.Reason) ? "" : " — " + probe.Reason)
                + ") — bỏ clip, chọn video khác.");
            ExcludeCurrentWarmupVideo();
            throw new ShopVideoGateException("TikTok Shop video is blocked on web; open in the mobile app.");
        }


        /// <summary>Search/browse warmup (v13): play once, wall-clock budget, không cập nhật lock khi carousel lướt.</summary>
        private async Task WatchWarmupSearchClipAsync(
            int seconds,
            int minSeconds,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            logAction?.Invoke(
                "[LIVE] Watching locked video "
                + _warmupLockedVideoId
                + " for ~"
                + seconds
                + "s (play once, pause at end — no loop).");

            var watchStartedAt = DateTime.UtcNow;
            var maxWallClockEnd = watchStartedAt.AddSeconds(Math.Max(seconds + 20, seconds * 1.35));

            while (DateTime.UtcNow < maxWallClockEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(_random.Next(900, 1100), cancellationToken).ConfigureAwait(false);

                var elapsedSec = (DateTime.UtcNow - watchStartedAt).TotalSeconds;
                if (elapsedSec >= seconds)
                {
                    logAction?.Invoke(
                        "[LIVE] Clip ended after ~"
                        + elapsedSec.ToString("0.#")
                        + "s — chuyển bước tiếp (không lặp, budget "
                        + seconds
                        + "s).");
                    break;
                }

                if (elapsedSec >= minSeconds && await IsWarmupVideoAtNaturalEndAsync().ConfigureAwait(false))
                {
                    logAction?.Invoke(
                        "[LIVE] Clip ended after ~"
                        + elapsedSec.ToString("0.#")
                        + "s — chuyển bước tiếp (không lặp, budget "
                        + seconds
                        + "s).");
                    break;
                }

                if (await IsShopInAppGateBlockingAsync().ConfigureAwait(false))
                {
                    logAction?.Invoke("[LIVE] Shop gate during watch — bỏ clip.");
                    throw new ShopVideoGateException();
                }

                var isPlaying = await IsVideoActuallyPlayingAsync().ConfigureAwait(false);
                if (!isPlaying)
                {
                    var currentId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(currentId)
                        || string.Equals(currentId, _warmupLockedVideoId, StringComparison.Ordinal))
                    {
                        await TryEnsureVideoPlayingOnceAsync(cancellationToken, browseSafe: true).ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task WatchAsync(
            int minSeconds,
            int maxSeconds,
            CancellationToken cancellationToken,
            Action<string> logAction = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (lo, hi) = NormalizeRange(minSeconds, maxSeconds, 5, 30);
            var seconds = _random.Next(lo, hi + 1);

            await EnsureVideoPlayingForWatchAsync(cancellationToken, logAction).ConfigureAwait(false);
            if (!await IsVideoMediaPlayableAsync().ConfigureAwait(false))
            {
                if (!_warmupInForyouMode && await IsBrowseVideoOverlayAsync().ConfigureAwait(false))
                {
                    if (await TryRecoverBrowseMediaAsync(cancellationToken, logAction).ConfigureAwait(false))
                    {
                        // recovered
                    }
                    else
                    {
                        ExcludeCurrentWarmupVideo();
                        logAction?.Invoke("[LIVE] Video đen / không tải được media — bỏ clip này, chọn video khác.");
                        throw new ShopVideoGateException("Video media failed to load (black player).");
                    }
                }
                else
                {
                    ExcludeCurrentWarmupVideo();
                    logAction?.Invoke("[LIVE] Video đen / không tải được media — bỏ clip này, chọn video khác.");
                    throw new ShopVideoGateException("Video media failed to load (black player).");
                }
            }

            // Chắc chắn không mở lại clip này ở slot sau (đã xem / đang xem).
            ExcludeCurrentWarmupVideo();
            await StabilizeWarmupVideoPlaybackAsync(cancellationToken).ConfigureAwait(false);

            if (!_warmupInForyouMode)
            {
                await WatchWarmupSearchClipAsync(seconds, lo, cancellationToken, logAction).ConfigureAwait(false);
                return;
            }

            logAction?.Invoke(
                $"[LIVE] Watching locked video {_warmupLockedVideoId} for ~{seconds}s playback (poll video.currentTime — no wall-clock drift).");
            // #region agent log
            DebugAgentLog.Write("A", "BrowserAutomation.WatchAsync", "after EnsureVideoPlaying", await ProbePageEngagementStateAsync().ConfigureAwait(false), "post-fix");
            // #endregion

            var targetPlaybackSec = (double)seconds;
            var minPlaybackSec = (double)lo;
            var accumulatedSec = 0.0;
            var lastCurrentTime = -1.0;
            var watchStartedAt = DateTime.UtcNow;
            var maxWallClockEnd = watchStartedAt.AddSeconds(Math.Max(targetPlaybackSec * 2.5, targetPlaybackSec + 45));

            while (DateTime.UtcNow < maxWallClockEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(_random.Next(900, 1100), cancellationToken).ConfigureAwait(false);

                var (currentTime, duration) = await GetVideoProgressAsync().ConfigureAwait(false);
                if (lastCurrentTime >= 0 && currentTime + 1.0 < lastCurrentTime)
                {
                    accumulatedSec += Math.Max(0, lastCurrentTime);
                }

                lastCurrentTime = currentTime;
                var playbackWatched = accumulatedSec + Math.Max(0, currentTime);

                if (playbackWatched >= targetPlaybackSec)
                {
                    logAction?.Invoke(
                        $"[LIVE] Đã xem ~{playbackWatched:0.#}s playback (mục tiêu {targetPlaybackSec:0.#}s).");
                    // #region agent log
                    DebugAgentLog.Write(
                        "A",
                        "BrowserAutomation.WatchAsync",
                        "target playback reached",
                        new { playbackWatched, targetPlaybackSec, lockedId = _warmupLockedVideoId },
                        "post-fix");
                    // #endregion
                    break;
                }

                if (playbackWatched >= minPlaybackSec && await IsWarmupVideoAtNaturalEndAsync().ConfigureAwait(false))
                {
                    // Retention > 100%: còn budget playback thì phát lại từ đầu.
                    if (targetPlaybackSec - playbackWatched > 2)
                    {
                        logAction?.Invoke(
                            $"[LIVE] Retention >100% — phát lại clip từ đầu (~{playbackWatched:0.#}/{targetPlaybackSec:0.#}s playback).");
                        accumulatedSec = playbackWatched;
                        await ReplayWarmupVideoFromStartAsync(cancellationToken).ConfigureAwait(false);
                        lastCurrentTime = -1;
                        continue;
                    }

                    logAction?.Invoke(
                        $"[LIVE] Clip ended after ~{playbackWatched:0.#}s playback — chuyển bước tiếp (mục tiêu {targetPlaybackSec:0.#}s).");
                    // #region agent log
                    DebugAgentLog.Write(
                        "A",
                        "BrowserAutomation.WatchAsync",
                        "early exit clip ended",
                        new { playbackWatched, targetPlaybackSec, duration, lockedId = _warmupLockedVideoId },
                        "post-fix");
                    // #endregion
                    break;
                }

                if (!string.IsNullOrWhiteSpace(_warmupLockedVideoId))
                {
                    // Dùng DOM-fallback để có id ngay cả khi browse overlay không đổi URL.
                    var currentId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
                    var shopGate = await IsShopInAppGateBlockingAsync().ConfigureAwait(false);

                    if (shopGate)
                    {
                        // Shop gate thật: restore.
                        logAction?.Invoke(
                            $"[LIVE] Shop gate during watch (current={currentId}, locked={_warmupLockedVideoId}) — restoring.");
                        await EnsureOnLockedWarmupVideoAsync(cancellationToken, logAction).ConfigureAwait(false);
                    }
                    else if (string.IsNullOrWhiteSpace(currentId))
                    {
                        // Id rỗng = browse overlay đang mở (URL về search) nhưng video vẫn phát —
                        // kiểm tra: nếu có <video> đang chạy thì KHÔNG goto lại.
                        var isPlaying = await IsVideoActuallyPlayingAsync().ConfigureAwait(false);
                        if (!isPlaying)
                        {
                            logAction?.Invoke("[LIVE] Mất video (id rỗng, không phát) — khôi phục.");
                            await EnsureOnLockedWarmupVideoAsync(cancellationToken, logAction).ConfigureAwait(false);
                        }
                    }
                    else if (!string.Equals(currentId, _warmupLockedVideoId, StringComparison.Ordinal))
                    {
                        // Feed tự chuyển sang clip khác (bình thường khi dùng browse/carousel).
                        // Cập nhật lock thay vì kéo ngược.
                        logAction?.Invoke(
                            $"[LIVE] Feed tự chuyển sang video khác ({currentId}) — cập nhật lock (không restore).");
                        _warmupLockedVideoId = currentId;
                        var landedUrl = _page.Url ?? string.Empty;
                        if (landedUrl.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _warmupLockedVideoUrl = landedUrl;
                        }
                        else
                        {
                            var feedAuthor = await TryResolveCurrentWarmupAuthorAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(feedAuthor))
                            {
                                _warmupLockedVideoUrl = NormalizeTikTokUrl(
                                    "https://www.tiktok.com/@" + feedAuthor + "/video/" + currentId);
                            }
                        }
                    }
                }

                try
                {
                    await _page.Mouse.MoveAsync(_random.Next(200, 1000), _random.Next(200, 700)).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            if (DateTime.UtcNow >= maxWallClockEnd)
            {
                var (finalTime, _) = await GetVideoProgressAsync().ConfigureAwait(false);
                var finalPlayback = accumulatedSec + Math.Max(0, finalTime);
                logAction?.Invoke(
                    $"[LIVE] Hết thời gian chờ playback (~{finalPlayback:0.#}s / {targetPlaybackSec:0.#}s) — chuyển bước tiếp.");
            }
        }

        public async Task<bool> LikeCurrentVideoAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();
            logAction?.Invoke("[LIVE] Page before like: " + (_page.Url ?? string.Empty));

            var inBrowseOverlay = !_warmupInForyouMode && await IsBrowseVideoOverlayAsync().ConfigureAwait(false);
            if (!inBrowseOverlay)
            {
                try
                {
                    await EnsureOnLockedWarmupVideoAsync(cancellationToken, logAction).ConfigureAwait(false);
                }
                catch (ShopVideoGateException)
                {
                    logAction?.Invoke("[LIVE] Shop gate at like — skip.");
                    throw;
                }
            }

            await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);

            var likeTargetId = ExtractVideoIdFromUrl(_page.Url ?? string.Empty);
            if (string.IsNullOrWhiteSpace(likeTargetId))
            {
                likeTargetId = await ResolveCurrentWarmupVideoIdAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(likeTargetId) ||
                !string.Equals(likeTargetId, _warmupLockedVideoId, StringComparison.Ordinal))
            {
                logAction?.Invoke(
                    "[LIVE] Wrong video at like time — skip like (url id="
                    + (likeTargetId ?? string.Empty) + ", locked=" + _warmupLockedVideoId + ").");
                // #region agent log
                DebugAgentLog.Write("B", "BrowserAutomation.LikeCurrentVideoAsync", "skip wrong video", new { likeTargetId, lockedId = _warmupLockedVideoId }, "post-fix");
                // #endregion
                return false;
            }

            // #region agent log
            var pageBeforeLike = await ProbePageEngagementStateAsync().ConfigureAwait(false);
            var likeProbeBefore = await ProbeLikeButtonStateAsync().ConfigureAwait(false);
            DebugAgentLog.Write("B", "BrowserAutomation.LikeCurrentVideoAsync", "before like click", likeProbeBefore);
            DebugAgentLog.Write("D", "BrowserAutomation.LikeCurrentVideoAsync", "page flags before like", pageBeforeLike);
            // #endregion

            var likedBefore = await IsVideoLikedOnPageAsync().ConfigureAwait(false);
            logAction?.Invoke("[LIVE] Tim: likedBefore=" + (likedBefore ? "yes" : "no") + ".");

            if (pageBeforeLike.TryGetValue("verifyGateText", out var verifyObj) && verifyObj is bool verifyOn && verifyOn)
            {
                logAction?.Invoke("[LIVE] TikTok yêu cầu 'Verify it's you' — chờ bạn xác minh trên trình duyệt (tối đa 5 phút)...");
                await WaitForVerifyGateClearedAsync(cancellationToken, logAction).ConfigureAwait(false);
            }

            if (likedBefore)
            {
                logAction?.Invoke("[LIVE] Tim: video đã liked — bỏ qua click (tránh bỏ tim).");
                // #region agent log
                DebugAgentLog.Write("B", "BrowserAutomation.LikeCurrentVideoAsync", "skipped page already liked", likeProbeBefore);
                // #endregion
                return false;
            }

            await RandomDelayAsync(400, 1200, cancellationToken).ConfigureAwait(false);
            BeginLikeApiCapture(_warmupLockedVideoId);
            try
            {
                _likeApiAcceptingResponses = true;
                logAction?.Invoke("[LIVE] Tim: double-click giữa video để tim...");
                var dblClicked = await DoubleClickToLikeAsync(cancellationToken, logAction).ConfigureAwait(false);
                if (!dblClicked)
                {
                    logAction?.Invoke("[LIVE] Tim: không lấy được vùng video — thử bấm icon tim...");
                    var likedViaIcon = await TryLikeViaBrowseIconAsync(cancellationToken, logAction).ConfigureAwait(false);
                    if (!likedViaIcon)
                    {
                        logAction?.Invoke("[LIVE] Tim: chưa giữ tim — bỏ qua.");
                        return false;
                    }
                }

                await WaitForLikeApiOrDomAsync(cancellationToken, 4500).ConfigureAwait(false);
                var likedAfter = await ConfirmLikeStableAsync(cancellationToken, logAction).ConfigureAwait(false);

                // #region agent log
                DebugAgentLog.Write("B", "BrowserAutomation.LikeCurrentVideoAsync", "after like click", new
                {
                    method = "double-click",
                    likedBefore,
                    likedAfter,
                    likeApiOk = _likeApiSucceeded,
                    likeProbeAfter = await ProbeLikeButtonStateAsync().ConfigureAwait(false)
                });
                // #endregion

                logAction?.Invoke(
                    "[LIVE] Tim: likedAfter=" + (likedAfter ? "yes" : "no")
                    + " | api=" + (_likeApiSucceeded ? "ok" : "no")
                    + " | method=double-click.");

                if (likedAfter)
                {
                    logAction?.Invoke("[LIVE] Đã tim video.");
                    return true;
                }

                var probeState = await ProbeLikeButtonStateAsync().ConfigureAwait(false);
                if (probeState.TryGetValue("buttons", out var buttonsObj))
                {
                    logAction?.Invoke("[LIVE] Tim: probe sau click — " + JsonConvert.SerializeObject(buttonsObj));
                }

                logAction?.Invoke("[LIVE] Tim: chưa giữ tim trên server/UI — bỏ qua.");
                return false;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LIVE] Tim: lỗi khi tim: " + ex.Message);
                return false;
            }
            finally
            {
                EndLikeApiCapture();
            }
        }

        /// <summary>Tim bằng double-click giữa player (gesture TikTok gốc).</summary>
        public async Task<bool> DoubleClickToLikeAsync(CancellationToken cancellationToken, Action<string> logAction = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ILocator videoLocator = null;
                try
                {
                    videoLocator = await GetActiveVideoLocatorAsync(cancellationToken, 5000).ConfigureAwait(false);
                }
                catch
                {
                    videoLocator = _page.Locator("video").First;
                }

                var box = await videoLocator.BoundingBoxAsync().ConfigureAwait(false);
                if (box == null || box.Width < 40 || box.Height < 40)
                {
                    return false;
                }

                var centerX = (float)(box.X + box.Width / 2);
                var centerY = (float)(box.Y + box.Height / 2);

                await _page.Mouse.MoveAsync(centerX, centerY, new MouseMoveOptions { Steps = 5 }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                await _page.Mouse.DblClickAsync(centerX, centerY).ConfigureAwait(false);
                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LIVE] Tim: lỗi double-click — " + ex.Message);
                return false;
            }
        }

        private async Task<bool> TryLikeViaBrowseIconAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            var likeSelectors = await GetLikeButtonSelectorsAsync().ConfigureAwait(false);
            ILocator likeLocator = null;
            foreach (var selector in likeSelectors)
            {
                var locator = _page.Locator(selector).First;
                if (!await IsVisibleAsync(locator, ShortQueryTimeout).ConfigureAwait(false))
                {
                    continue;
                }

                if (await IsLikeActiveAsync(locator).ConfigureAwait(false))
                {
                    return true;
                }

                likeLocator = locator;
                break;
            }

            if (likeLocator == null)
            {
                return false;
            }

            var clickTarget = await ResolveLikeClickLocatorAsync(likeLocator, "[data-e2e=\"browse-like-icon\"]").ConfigureAwait(false);
            await NativeClickLikeButtonAsync(clickTarget, cancellationToken).ConfigureAwait(false);
            return await IsVideoLikedOnPageAsync().ConfigureAwait(false);
        }

        private void BeginLikeApiCapture(string videoId)
        {
            EndLikeApiCapture();
            _likeApiCaptureVideoId = videoId?.Trim();
            _likeApiSucceeded = false;
            _likeApiAcceptingResponses = false;
            _likeApiResultTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_page == null)
            {
                return;
            }

            _likeApiResponseHandler = (_, response) => { _ = CaptureLikeApiResponseAsync(response); };
            _page.Response += _likeApiResponseHandler;
        }

        private void EndLikeApiCapture()
        {
            if (_page != null && _likeApiResponseHandler != null)
            {
                try
                {
                    _page.Response -= _likeApiResponseHandler;
                }
                catch
                {
                }
            }

            _likeApiResponseHandler = null;
            _likeApiResultTcs = null;
            _likeApiCaptureVideoId = null;
            _likeApiSucceeded = false;
            _likeApiAcceptingResponses = false;
        }

        private async Task<ILocator> ResolveLikeClickLocatorAsync(ILocator likeLocator, string usedSelector)
        {
            if (usedSelector.IndexOf("browse-like", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    var icon = _page.Locator("[data-e2e=\"browse-like-icon\"]").First;
                    if (await icon.CountAsync().ConfigureAwait(false) > 0
                        && await IsVisibleAsync(icon, ShortQueryTimeout).ConfigureAwait(false))
                    {
                        return icon;
                    }
                }
                catch
                {
                }
            }

            return likeLocator;
        }

        private async Task<bool> DispatchBrowseLikePointerClickAsync()
        {
            try
            {
                return await _page.EvaluateAsync<bool>(@"() => {
                    const icon = document.querySelector('[data-e2e=""browse-like-icon""]');
                    const btn = icon ? (icon.closest('button') || icon) : document.querySelector('button:has([data-e2e=""browse-like-icon""])');
                    if (!btn) return false;
                    try { btn.scrollIntoView({ block: 'center', inline: 'center' }); } catch (e) {}
                    const rect = btn.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, clientX: x, clientY: y, view: window, buttons: 1 };
                    const fire = (type) => btn.dispatchEvent(new MouseEvent(type, opts));
                    fire('pointerover'); fire('mouseover'); fire('pointerenter'); fire('mouseenter');
                    fire('pointerdown'); fire('mousedown'); fire('pointerup'); fire('mouseup'); fire('click');
                    return true;
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task CaptureLikeApiResponseAsync(IResponse response)
        {
            try
            {
                if (response == null || !_likeApiAcceptingResponses)
                {
                    return;
                }

                var request = response.Request;
                var url = response.Url ?? string.Empty;
                var reqUrl = request?.Url ?? url;
                if (reqUrl.IndexOf("digg", StringComparison.OrdinalIgnoreCase) < 0
                    && reqUrl.IndexOf("/like", StringComparison.OrdinalIgnoreCase) < 0
                    && url.IndexOf("digg", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                if (reqUrl.IndexOf("type=0", StringComparison.OrdinalIgnoreCase) >= 0
                    || reqUrl.IndexOf("action=0", StringComparison.OrdinalIgnoreCase) >= 0
                    || reqUrl.IndexOf("digg_type=0", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                var isCommitDigg = reqUrl.IndexOf("/item/digg", StringComparison.OrdinalIgnoreCase) >= 0
                    || reqUrl.IndexOf("commit/item/digg", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isCommitDigg)
                {
                    return;
                }

                if (request != null
                    && !string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(request.Method, "PUT", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_likeApiCaptureVideoId)
                    && reqUrl.IndexOf(_likeApiCaptureVideoId, StringComparison.Ordinal) < 0
                    && url.IndexOf(_likeApiCaptureVideoId, StringComparison.Ordinal) < 0)
                {
                    return;
                }

                if (!response.Ok)
                {
                    return;
                }

                var body = await response.TextAsync().ConfigureAwait(false);
                if (!IsTikTokLikeApiSuccess(body))
                {
                    return;
                }

                _likeApiSucceeded = true;
                _likeApiResultTcs?.TrySetResult(true);
            }
            catch
            {
            }
        }

        private static bool IsTikTokLikeApiSuccess(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            if (body.IndexOf("is_digg\":1", StringComparison.Ordinal) >= 0
                || body.IndexOf("\"is_digg\":1", StringComparison.Ordinal) >= 0
                || body.IndexOf("\"digg\":1", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            // Chỉ chấp nhận status_code:0 khi response có trường digg/is_digg (tránh prefetch GET giả).
            if ((body.IndexOf("\"status_code\":0", StringComparison.Ordinal) >= 0
                 || body.IndexOf("\"statusCode\":0", StringComparison.Ordinal) >= 0)
                && body.IndexOf("digg", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return body.IndexOf("is_digg\":0", StringComparison.Ordinal) < 0;
            }

            return false;
        }

        private async Task WaitForLikeApiOrDomAsync(CancellationToken cancellationToken, int maxWaitMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_likeApiSucceeded)
                {
                    return;
                }

                if (await IsVideoLikedOnPageAsync().ConfigureAwait(false))
                {
                    return;
                }

                if (_likeApiResultTcs != null && _likeApiResultTcs.Task.IsCompleted)
                {
                    return;
                }

                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task NativeClickLikeButtonAsync(ILocator locator, CancellationToken cancellationToken)
        {
            await locator.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            try
            {
                await locator.HoverAsync(new LocatorHoverOptions { Timeout = 3000 }).ConfigureAwait(false);
                await Task.Delay(_random.Next(120, 280), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }

            var box = await locator.BoundingBoxAsync().ConfigureAwait(false);
            if (box != null && box.Width > 4 && box.Height > 4)
            {
                await locator.ClickAsync(new LocatorClickOptions
                {
                    Timeout = 8000,
                    Position = new Position
                    {
                        X = (float)(box.Width * 0.5),
                        Y = (float)(box.Height * 0.5)
                    }
                }).ConfigureAwait(false);
                return;
            }

            await locator.ClickAsync(new LocatorClickOptions { Timeout = 8000 }).ConfigureAwait(false);
        }

        private static async Task<bool> IsUnlikeLabelLocatorAsync(ILocator locator)
        {
            try
            {
                var label = (await locator.GetAttributeAsync("aria-label").ConfigureAwait(false) ?? string.Empty).ToLowerInvariant();
                return label.Contains("unlike") || label.Contains("bỏ thích");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CommentCurrentVideoAsync(string text, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (string.IsNullOrWhiteSpace(text))
            {
                logAction?.Invoke("[LIVE] Skipping comment: empty text.");
                return false;
            }

            try
            {
                await EnsureOnLockedWarmupVideoAsync(cancellationToken, logAction).ConfigureAwait(false);
            }
            catch (ShopVideoGateException)
            {
                logAction?.Invoke("[LIVE] Shop gate at comment — skip.");
                throw;
            }

            var pageBeforeComment = await ProbePageEngagementStateAsync().ConfigureAwait(false);
            if (pageBeforeComment.TryGetValue("verifyGateText", out var verifyComment) && verifyComment is bool verifyOnComment && verifyOnComment)
            {
                logAction?.Invoke("[LIVE] Verify gate before comment — waiting for manual verification...");
                await WaitForVerifyGateClearedAsync(cancellationToken, logAction).ConfigureAwait(false);
            }

            await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);

            ILocator inputLocator = null;
            string matchedInputSelector = null;

            inputLocator = await WaitForCommentInputReadyAsync(
                    cancellationToken,
                    logAction,
                    preferBrowsePanel: true,
                    timeoutMs: 2500)
                .ConfigureAwait(false);
            if (inputLocator != null)
            {
                matchedInputSelector = "browse-panel-input (wait)";
            }

            if (inputLocator == null)
            {
                await TrySelectCommentsTabAsync(cancellationToken, logAction).ConfigureAwait(false);
                if (await WaitAndClickCommentPanelButtonAsync(cancellationToken, logAction).ConfigureAwait(false))
                {
                    logAction?.Invoke("[LIVE] Đã mở panel bình luận.");
                }

                await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);
                await TrySelectCommentsTabAsync(cancellationToken, logAction).ConfigureAwait(false);

                inputLocator = await WaitForCommentInputReadyAsync(
                        cancellationToken,
                        logAction,
                        preferBrowsePanel: false,
                        timeoutMs: CommentPanelWaitMs)
                    .ConfigureAwait(false);
                if (inputLocator != null)
                {
                    matchedInputSelector = "comment-input (after panel wait)";
                }
            }

            if (inputLocator == null)
            {
                inputLocator = await TryOpenCommentViaPlaceholderAsync(cancellationToken, logAction).ConfigureAwait(false);
                if (inputLocator != null)
                {
                    matchedInputSelector = "comment-input (placeholder wait)";
                }
            }

            // #region agent log
            DebugAgentLog.Write("C", "BrowserAutomation.CommentCurrentVideoAsync", "panel open attempt", new { hasInput = inputLocator != null, textLen = text.Length, matchedInputSelector });
            // #endregion

            if (inputLocator == null)
            {
                logAction?.Invoke("[LIVE] Comment input not found. Skipping.");
                // #region agent log
                DebugAgentLog.Write("C", "BrowserAutomation.CommentCurrentVideoAsync", "input not found", await ProbeCommentUiStateAsync().ConfigureAwait(false));
                // #endregion
                return false;
            }

            try
            {
                logAction?.Invoke("[LIVE] Đang gõ comment từng chữ như người thật (" + text.Length + " ký tự)...");
                var typed = await TypeCommentLikeHumanAsync(inputLocator, text, cancellationToken).ConfigureAwait(false);
                // #region agent log
                DebugAgentLog.Write("C", "BrowserAutomation.CommentCurrentVideoAsync", "after typing", new
                {
                    typed,
                    state = await ProbeCommentUiStateAsync().ConfigureAwait(false)
                });
                // #endregion
                if (!typed)
                {
                    logAction?.Invoke("[LIVE] Không gõ được vào ô comment.");
                    return false;
                }

                logAction?.Invoke("[LIVE] Đã gõ xong — đang bấm Đăng/Post...");
                await RandomDelayAsync(500, 1100, cancellationToken).ConfigureAwait(false);

                var posted = false;
                string submitSelectorUsed = null;
                foreach (var selector in TikTokSelectors.CommentSubmit)
                {
                    var btn = _page.Locator(selector).First;
                    if (await IsVisibleAsync(btn, ShortQueryTimeout).ConfigureAwait(false))
                    {
                        try
                        {
                            await HumanClickLocatorAsync(btn, cancellationToken).ConfigureAwait(false);
                            posted = true;
                            submitSelectorUsed = selector;
                            break;
                        }
                        catch
                        {
                            // try next
                        }
                    }
                }

                if (!posted)
                {
                    await _page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
                    posted = true;
                    submitSelectorUsed = "Enter";
                }

                await RandomDelayAsync(800, 1600, cancellationToken).ConfigureAwait(false);
                var commentUiAfter = await ProbeCommentUiStateAsync().ConfigureAwait(false);
                // #region agent log
                DebugAgentLog.Write("C", "BrowserAutomation.CommentCurrentVideoAsync", "after submit attempt", new
                {
                    posted,
                    submitSelectorUsed,
                    matchedInputSelector,
                    state = commentUiAfter
                });
                DebugAgentLog.Write("D", "BrowserAutomation.CommentCurrentVideoAsync", "page flags after comment", await ProbePageEngagementStateAsync().ConfigureAwait(false));
                // #endregion
                if (posted)
                {
                    logAction?.Invoke("[LIVE] Đã gửi comment.");
                    return true;
                }

                logAction?.Invoke("[LIVE] Đã gõ comment nhưng chưa xác nhận nút Đăng.");
                return false;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LIVE] Failed to submit comment: " + ex.Message);
                // #region agent log
                DebugAgentLog.Write("C", "BrowserAutomation.CommentCurrentVideoAsync", "exception", new { ex.Message });
                // #endregion
                return false;
            }
        }

        /// <summary>Chờ nút mở comment sẵn sàng (lazy-load) rồi mới click.</summary>
        private async Task<bool> WaitAndClickCommentPanelButtonAsync(
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var selector in TikTokSelectors.CommentPanelButtons)
            {
                var button = _page.Locator(selector).First;
                try
                {
                    await button.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = CommentPanelWaitMs
                    }).ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] Nút bình luận sẵn sàng — mở panel...");
                    await HumanClickLocatorAsync(button, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(500, 900, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>Chờ ô nhập comment visible sau khi panel lazy-load.</summary>
        private async Task<ILocator> WaitForCommentInputReadyAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            bool preferBrowsePanel,
            int timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selectors = preferBrowsePanel
                ? TikTokSelectors.BrowseCommentInputs
                : TikTokSelectors.CommentInputs;

            foreach (var selector in selectors)
            {
                try
                {
                    var loc = preferBrowsePanel ? _page.Locator(selector).Last : _page.Locator(selector).First;
                    await loc.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = timeoutMs
                    }).ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] Ô bình luận đã sẵn sàng (" + selector + ").");
                    return loc;
                }
                catch
                {
                }
            }

            try
            {
                var combined = _page.Locator(CommentInputReadySelector).First;
                await combined.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeoutMs
                }).ConfigureAwait(false);
                logAction?.Invoke("[LIVE] Ô bình luận đã sẵn sàng (lazy input).");
                return combined;
            }
            catch
            {
            }

            try
            {
                var scoped = _page.Locator(
                        "[data-e2e='browse-comment-list'], [data-e2e='comment-list'], section[class*='Comment']")
                    .Locator("div[contenteditable='true'], textarea, div[role='textbox']")
                    .First;
                await scoped.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = Math.Min(timeoutMs, 4000)
                }).ConfigureAwait(false);
                logAction?.Invoke("[LIVE] Ô bình luận trong danh sách comment đã sẵn sàng.");
                return scoped;
            }
            catch
            {
                return null;
            }
        }

        private async Task<ILocator> TryOpenCommentViaPlaceholderAsync(
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            foreach (var ph in TikTokSelectors.CommentPlaceholderClicks)
            {
                var phLoc = _page.Locator(ph).First;
                try
                {
                    await phLoc.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 4000
                    }).ConfigureAwait(false);
                    await HumanClickLocatorAsync(phLoc, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(400, 800, cancellationToken).ConfigureAwait(false);
                    var input = await WaitForCommentInputReadyAsync(
                            cancellationToken,
                            logAction,
                            preferBrowsePanel: true,
                            timeoutMs: CommentPanelWaitMs)
                        .ConfigureAwait(false);
                    if (input != null)
                    {
                        logAction?.Invoke("[LIVE] Mở ô bình luận qua placeholder.");
                        return input;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        /// <summary>Tìm ô nhập bình luận trên browse overlay (panel phải).</summary>
        private async Task<ILocator> TryFindBrowseCommentInputAsync(
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            return await WaitForCommentInputReadyAsync(
                    cancellationToken,
                    logAction,
                    preferBrowsePanel: true,
                    timeoutMs: 2500)
                .ConfigureAwait(false);
        }

        /// <summary>Legacy wrapper — mở panel comment bằng WaitFor + click.</summary>
        public async Task<bool> OpenCommentsPanelForInputAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (await TrySelectCommentsTabAsync(cancellationToken, logAction).ConfigureAwait(false))
            {
                return true;
            }

            if (await WaitAndClickCommentPanelButtonAsync(cancellationToken, logAction).ConfigureAwait(false))
            {
                await TrySelectCommentsTabAsync(cancellationToken, logAction).ConfigureAwait(false);
                logAction?.Invoke("[LIVE] Đã mở panel bình luận.");
                return true;
            }

            logAction?.Invoke("[LIVE] Không thấy nút bình luận — thử ô nhập trực tiếp.");
            return false;
        }

        /// <summary>Trên trang video desktop, tab phải là «Bình luận» (không phải «Bạn có thể thích»).</summary>
        private async Task<bool> TrySelectCommentsTabAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            foreach (var selector in TikTokSelectors.CommentsTabs)
            {
                try
                {
                    var tab = _page.Locator(selector).First;
                    if (!await IsVisibleAsync(tab, TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await HumanClickLocatorAsync(tab, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(500, 1000, cancellationToken).ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] Đã chọn tab Bình luận.");
                    return true;
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }

        public async Task OpenCommentsPanelBrieflyAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (await OpenCommentsPanelForInputAsync(cancellationToken, logAction).ConfigureAwait(false))
            {
                await RandomDelayAsync(500, 1200, cancellationToken).ConfigureAwait(false);
                await _page.Keyboard.PressAsync("Escape").ConfigureAwait(false);
                logAction?.Invoke("[LIVE] Opened comments briefly before like.");
            }
        }

        public async Task SimulateReadingScrollAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            var url = _page.Url ?? string.Empty;
            if (url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Cuộn dọc trên trang video = TikTok chuyển sang clip kế tiếp (thường là Shop gate).
                logAction?.Invoke("[LIVE] Skip vertical scroll on video page (avoids accidental next-video).");
                return;
            }

            var steps = _random.Next(2, 5);
            for (var i = 0; i < steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var deltaY = _random.Next(120, 360);
                if (_random.NextDouble() < 0.35d)
                {
                    deltaY = -_random.Next(80, 220); // occasional slight upward scroll
                }

                try
                {
                    await _page.Mouse.WheelAsync(0, deltaY).ConfigureAwait(false);
                }
                catch
                {
                    // best effort only
                }

                await Task.Delay(_random.Next(280, 900), cancellationToken).ConfigureAwait(false);
            }

            logAction?.Invoke("[LIVE] Simulated gentle reading scroll.");
        }

        public async Task WaitForVerifyGateClearedAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            int maxMinutes = 5)
        {
            var deadline = DateTime.UtcNow.AddMinutes(maxMinutes);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = await ProbePageEngagementStateAsync().ConfigureAwait(false);
                if (!state.TryGetValue("verifyGateText", out var v) || v is not bool on || !on)
                {
                    logAction?.Invoke("[LIVE] Verify gate cleared. Continuing...");
                    return;
                }

                await Task.Delay(2500, cancellationToken).ConfigureAwait(false);
            }

            logAction?.Invoke("[LIVE] WARN: Verify gate still visible after wait.");
        }

        public async Task WaitForCaptchaResolvedAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            int maxMinutes = 5)
        {
            logAction?.Invoke($"[CAPTCHA] TikTok đang yêu cầu CAPTCHA. HÃY GIẢI THỦ CÔNG trong cửa sổ trình duyệt. Mình sẽ đợi tối đa {maxMinutes} phút và tự tiếp tục khi xong.");
            var deadline = DateTime.UtcNow.AddMinutes(maxMinutes);
            var lastLog = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await Task.Delay(2500, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                try
                {
                    if (!await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                    {
                        logAction?.Invoke("[CAPTCHA] Đã giải xong, tiếp tục quét...");
                        await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                }
                catch
                {
                    // Detection should never throw.
                }

                if ((DateTime.UtcNow - lastLog).TotalSeconds >= 25)
                {
                    lastLog = DateTime.UtcNow;
                    logAction?.Invoke("[CAPTCHA] Vẫn đang chờ bạn giải captcha trong cửa sổ trình duyệt...");
                }
            }

            logAction?.Invoke("[CAPTCHA] Hết thời gian chờ — bỏ qua, có thể trang sẽ rỗng.");
        }

        public async Task<bool> DetectChallengeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_page == null)
            {
                return false;
            }

            try
            {
                var url = _page.Url ?? string.Empty;
                if (url.IndexOf("captcha", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                foreach (var selector in TikTokSelectors.ChallengeIndicators)
                {
                    var loc = _page.Locator(selector).First;
                    if (await IsVisibleAsync(loc, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Detection should never throw.
            }

            return false;
        }

        public async Task<string> CaptureScreenshotAsync(string outputDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();
            var dir = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "checkpoint_screenshots")
                : outputDirectory;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"checkpoint_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                FullPage = true
            }).ConfigureAwait(false);
            return path;
        }

        public async Task<bool> DetectCheckpointAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_page == null)
            {
                return false;
            }

            try
            {
                var url = _page.Url ?? string.Empty;
                if (url.IndexOf("checkpoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    url.IndexOf("account-status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    url.IndexOf("suspended", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                foreach (var selector in TikTokSelectors.CheckpointIndicators)
                {
                    var loc = _page.Locator(selector).First;
                    if (await IsVisibleAsync(loc, TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Detection should never throw.
            }

            return false;
        }

        public async Task<CaptchaSolveRequest> GetCaptchaSolveRequestAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            string siteKey = null;
            try
            {
                siteKey = await _page.GetAttributeAsync("[data-sitekey]", "data-sitekey").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(siteKey))
                {
                    siteKey = await _page.EvaluateAsync<string>(
                        "(() => document.querySelector('[data-sitekey]')?.getAttribute('data-sitekey') || '')()")
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                // best effort
            }

            return new CaptchaSolveRequest
            {
                SiteKey = (siteKey ?? string.Empty).Trim(),
                PageUrl = _page.Url ?? string.Empty
            };
        }

        public async Task ApplyCaptchaTokenAsync(string token, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Captcha token is empty.", nameof(token));
            }

            await _page.EvaluateAsync(
                @"(t) => {
                    const textarea = document.getElementById('g-recaptcha-response') || document.querySelector('textarea[name=""g-recaptcha-response""]');
                    if (textarea) {
                        textarea.value = t;
                        textarea.innerHTML = t;
                    }
                    const recaptcha = window.grecaptcha;
                    if (recaptcha && recaptcha.getResponse && recaptcha.getResponse().length === 0) {
                        // token injected into hidden textarea as fallback
                    }
                }",
                token).ConfigureAwait(false);

            logAction?.Invoke("[LIVE] Captcha token injected. Waiting challenge to clear...");
            await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);
        }

        public async Task RandomDelayAsync(int minMs, int maxMs, CancellationToken cancellationToken)
        {
            var (lo, hi) = NormalizeRange(minMs, maxMs, 100, 60_000);
            await Task.Delay(_random.Next(lo, hi + 1), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens TikTok login in the persistent profile and blocks until the user closes the browser
        /// (same session folder as <c>--user-data-dir</c> in Selenium).
        /// </summary>
        public async Task OpenTikTokLoginAndWaitUntilClosedAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (_page == null || _context == null)
            {
                throw new InvalidOperationException("Browser context is not ready.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _page.GotoAsync(TikTokLoginUrl, new PageGotoOptions
            {
                Timeout = 60000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).ConfigureAwait(false);

            logAction?.Invoke("[MANUAL] Đã mở tiktok.com/login — đăng nhập xong thì đóng hết cửa sổ trình duyệt (cookie lưu trong thư mục profile).");

            var tcs = new TaskCompletionSource<object>();
            void OnContextClosed(object sender, IBrowserContext ctx)
            {
                tcs.TrySetResult(null);
            }

            _context.Close += OnContextClosed;
            try
            {
                using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
                {
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    if (_context != null)
                    {
                        _context.Close -= OnContextClosed;
                    }
                }
                catch
                {
                    // Context may already be disposed after user closed the browser.
                }
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                if (_context != null)
                {
                    await _context.CloseAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Avoid throwing during dispose.
            }
            finally
            {
                if (_page != null && _shopSearchResponseHandler != null)
                {
                    try
                    {
                        _page.Response -= _shopSearchResponseHandler;
                    }
                    catch
                    {
                        // ignored
                    }
                }

                _shopSearchResponseHandler = null;
                EndForyouFeedCapture();
                EndLikeApiCapture();
                lock (_shopSearchJsonBodies)
                {
                    _shopSearchJsonBodies.Clear();
                }

                lock (_shopSearchResponseUrls)
                {
                    _shopSearchResponseUrls.Clear();
                }

                _page = null;
                _context = null;
                _playwright = null;
                _activeProfileName = null;
                _activeProfile = null;
                _preserveExistingSession = false;
            }
        }

        /// <summary>
        /// Reads embedded TikTok user state from the active page (after login). Returns null if not found.
        /// </summary>
        public async Task<TikTokAccountSnapshot> TryExtractTikTokAccountSnapshotAsync(
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (_page == null || _context == null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SyncToPrimaryTikTokPage(logAction);

            try
            {
                if (!await TryGotoSoftAsync(TikTokHomeUrl, cancellationToken, logAction).ConfigureAwait(false) &&
                    !await TryGotoSoftAsync(TikTokForyouUrl, cancellationToken, logAction).ConfigureAwait(false))
                {
                    logAction?.Invoke("[LOGIN] Không tải được trang chủ/For You để đọc tài khoản — bỏ qua bước này.");
                    return null;
                }

                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LOGIN] Không tải trang chủ TikTok để đọc tài khoản: " + ex.Message);
                return null;
            }

            try
            {
                var json = await _page.EvaluateAsync<string>(
                    @"async () => {
                        const normalizeUser = (u) => {
                            if (!u || typeof u !== 'object') return null;
                            const uid = u.uniqueId || u.unique_id || u.uniqueID || u.user_unique_id || u.username || '';
                            const uidText = String(uid || '').trim().replace(/^@+/, '');
                            if (!uidText) return null;
                            const nickname = u.nickname || u.nickName || u.displayName || u.name || '';
                            const userId = u.userId || u.user_id || u.uid || u.id || u.secUid || '';
                            return {
                                uniqueId: uidText,
                                nickname: String(nickname || '').trim(),
                                userId: String(userId || '').trim()
                            };
                        };

                        const addCandidate = (list, user, source, score) => {
                            const n = normalizeUser(user);
                            if (!n) return;
                            n.source = source;
                            n.score = score || 0;
                            list.push(n);
                        };

                        const pickBest = (list) => {
                            if (!list || list.length === 0) return null;
                            list.sort((a, b) => (b.score || 0) - (a.score || 0));
                            return list[0];
                        };

                        const collectCandidatesFromState = (obj, sourcePrefix) => {
                            const out = [];
                            if (!obj || typeof obj !== 'object') return out;

                            // Strong: user detail scope (self/profile entity in current session)
                            const detail = obj.__DEFAULT_SCOPE__ && obj.__DEFAULT_SCOPE__['webapp.user-detail'];
                            if (detail) {
                                addCandidate(out, detail.userInfo || detail.user || detail, sourcePrefix + '.webapp.user-detail', 100);
                            }

                            // Strong: explicitly marked current/login user
                            addCandidate(out, obj.currentUser, sourcePrefix + '.currentUser', 95);
                            addCandidate(out, obj.loginUser, sourcePrefix + '.loginUser', 95);
                            addCandidate(out, obj.userInfo, sourcePrefix + '.userInfo', 92);
                            addCandidate(out, obj.me, sourcePrefix + '.me', 92);
                            addCandidate(out, obj.accountInfo, sourcePrefix + '.accountInfo', 90);
                            addCandidate(out, obj.LoginContextModule && obj.LoginContextModule.user, sourcePrefix + '.LoginContextModule.user', 94);

                            const initialPropsUser = obj.value && obj.value.userState && obj.value.userState.user;
                            addCandidate(out, initialPropsUser, sourcePrefix + '.value.userState.user', 118);

                            // SIGI_STATE patterns
                            const users = obj.UserModule && obj.UserModule.users;
                            const userModule = obj.UserModule || {};
                            const loginContext = obj.LoginContextModule || {};
                            const loginKeys = [
                                userModule.uid,
                                userModule.currentUid,
                                userModule.loginUid,
                                loginContext.uid,
                                loginContext.userId,
                                obj.uid,
                                obj.userId
                            ]
                                .filter(x => x !== undefined && x !== null && String(x).trim().length > 0)
                                .map(x => String(x).trim());

                            if (users && typeof users === 'object') {
                                // Best: id matched from login context
                                for (let i = 0; i < loginKeys.length; i++) {
                                    const key = loginKeys[i];
                                    if (users[key]) {
                                        addCandidate(out, users[key], sourcePrefix + '.UserModule.users[' + key + ']', 98);
                                    }
                                }

                                // Next: objects with self flags
                                const keys = Object.keys(users);
                                for (let i = 0; i < keys.length; i++) {
                                    const u = users[keys[i]];
                                    if (!u || typeof u !== 'object') continue;
                                    if (u.isSelf === true || u.isLoginUser === true || u.isCurrentUser === true) {
                                        addCandidate(out, u, sourcePrefix + '.UserModule.users[self-flag]', 96);
                                    }
                                }
                            }

                            // Last resort inside state object (still better than random DOM link)
                            addCandidate(out, userModule.user, sourcePrefix + '.UserModule.user', 80);
                            addCandidate(out, userModule.currentUser, sourcePrefix + '.UserModule.currentUser', 88);
                            addCandidate(out, loginContext.userInfo, sourcePrefix + '.LoginContextModule.userInfo', 90);

                            return out;
                        };

                        const parseJsonAndCollect = (text, sourcePrefix) => {
                            if (!text || !text.trim()) return [];
                            try {
                                const parsed = JSON.parse(text);
                                return collectCandidatesFromState(parsed, sourcePrefix);
                            } catch (e) {
                                return [];
                            }
                        };

                        const candidates = [];

                        // 1) API first (most likely to return the current account)
                        const apiCandidates = [
                            '/api/me/?aid=1988&app_name=tiktok_web',
                            '/api/user/detail/?from_page=fyp'
                        ];
                        for (let i = 0; i < apiCandidates.length; i++) {
                            try {
                                const res = await fetch(apiCandidates[i], {
                                    credentials: 'include',
                                    headers: { 'accept': 'application/json, text/plain, */*' }
                                });
                                if (!res || !res.ok) continue;
                                const text = await res.text();
                                const found = parseJsonAndCollect(text, 'api:' + apiCandidates[i]);
                                for (let j = 0; j < found.length; j++) candidates.push(found[j]);
                            } catch (e) {}
                        }

                        // 2) Script hydration states
                        const scriptIds = ['__UNIVERSAL_DATA_FOR_REHYDRATION__', 'SIGI_STATE', '__NEXT_DATA__'];
                        for (let i = 0; i < scriptIds.length; i++) {
                            const id = scriptIds[i];
                            const el = document.getElementById(id);
                            const found = parseJsonAndCollect(el && el.textContent ? el.textContent : '', 'script:' + id);
                            for (let j = 0; j < found.length; j++) candidates.push(found[j]);
                        }

                        // 3) Window globals
                        try {
                            const globals = [
                                { n: 'SIGI_STATE', v: window.SIGI_STATE },
                                { n: '__UNIVERSAL_DATA_FOR_REHYDRATION__', v: window.__UNIVERSAL_DATA_FOR_REHYDRATION__ },
                                { n: '__NEXT_DATA__', v: window.__NEXT_DATA__ },
                                { n: '__INIT_PROPS__', v: window.__INIT_PROPS__ },
                                { n: '__INITIAL_PROPS__', v: window.__INITIAL_PROPS__ }
                            ];
                            for (let i = 0; i < globals.length; i++) {
                                const found = collectCandidatesFromState(globals[i].v, 'window:' + globals[i].n);
                                for (let j = 0; j < found.length; j++) candidates.push(found[j]);
                            }
                        } catch (e) {}

                        // 4) DOM fallback with strict nav/header selectors only
                        try {
                            const navSelectors = [
                                'header a[href^=""/@""]',
                                'nav a[href^=""/@""]',
                                'a[data-e2e*=""profile""][href^=""/@""]'
                            ];
                            for (let i = 0; i < navSelectors.length; i++) {
                                const a = document.querySelector(navSelectors[i]);
                                if (!a) continue;
                                const href = String(a.getAttribute('href') || '');
                                const m = href.match(/^\/@([^\/\?\#]+)/);
                                if (!m || !m[1]) continue;
                                const maybeNick = decodeURIComponent(m[1]).replace(/^@+/, '').trim();
                                if (!maybeNick) continue;
                                addCandidate(candidates, { uniqueId: maybeNick, nickname: '', userId: '' }, 'dom-nav', 50);
                                break;
                            }
                        } catch (e) {}

                        const best = pickBest(candidates);
                        if (!best) return '';
                        return JSON.stringify(best);
                    }").ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(json))
                {
                    logAction?.Invoke("[LOGIN] Không đọc được @nick từ hydration/DOM/API — TikTok có thể đã đổi layout.");
                    return null;
                }

                var jo = JObject.Parse(json);
                var uniqueId = (jo["uniqueId"] ?? string.Empty).ToString().Trim().TrimStart('@');
                if (string.IsNullOrWhiteSpace(uniqueId))
                {
                    return null;
                }
                var source = (jo["source"] ?? string.Empty).ToString().Trim();
                if (!string.IsNullOrWhiteSpace(source))
                {
                    logAction?.Invoke("[LOGIN] @nick source: " + source);
                }

                var rawNick = (jo["nickname"] ?? string.Empty).ToString().Trim();
                return new TikTokAccountSnapshot
                {
                    UniqueId = uniqueId,
                    Nickname = TikTokDisplayNicknameSanitizer.Sanitize(rawNick, uniqueId),
                    UserId = (jo["userId"] ?? string.Empty).ToString().Trim()
                };
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LOGIN] Lỗi phân tích tài khoản TikTok: " + ex.Message);
                return null;
            }
        }

        public async Task LaunchFromSettingsAsync(
            AppSettings settings,
            string runningProfileName,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var profile = ResolveProfileFromSettings(settings, runningProfileName);
            var effectiveName = string.IsNullOrWhiteSpace(runningProfileName)
                ? (profile?.Name ?? "default")
                : runningProfileName;

            await LaunchAsync(cancellationToken, logAction, effectiveName, profile).ConfigureAwait(false);
        }

        /// <summary>
        /// Google / SSO often opens another tab; keep the automation focused on a TikTok tab that left /login.
        /// </summary>
        private static bool IsLikelyPostAuthTikTokUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (url.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("/signup", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return url.IndexOf("/foryou", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/following", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/explore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/search", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   url.IndexOf("/@", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// When <paramref name="preferTikTokLoginTab"/> is true (đă nhập từ Cài đặt): ưu tiên tab đã rời /login (OAuth xong),
        /// nếu không có thì giữ tab /login — tránh nhảy sang tab TikTok phụ (feed) khi user chưa đăng nhập.
        /// </summary>
        private void SyncToPrimaryTikTokPage(Action<string> logAction, bool preferTikTokLoginTab = false)
        {
            if (_context?.Pages == null || _context.Pages.Count == 0)
            {
                return;
            }

            if (preferTikTokLoginTab)
            {
                IPage postAuth = null;
                IPage loginTab = null;
                for (var i = 0; i < _context.Pages.Count; i++)
                {
                    var p = _context.Pages[i];
                    var u = p.Url ?? string.Empty;
                    if (u.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (IsLikelyPostAuthTikTokUrl(u))
                    {
                        postAuth = p;
                        break;
                    }

                    if (loginTab == null && u.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        loginTab = p;
                    }
                }

                var picked = postAuth ?? loginTab;
                if (picked != null && !ReferenceEquals(picked, _page))
                {
                    logAction?.Invoke(postAuth != null
                        ? "[LOGIN] Chuyển sang tab TikTok đã vào feed (thường gặp sau Google/QR)."
                        : "[LOGIN] Giữ tab đăng nhập TikTok làm tab chính.");
                    _page = picked;
                }

                return;
            }

            IPage best = null;
            for (var i = 0; i < _context.Pages.Count; i++)
            {
                var p = _context.Pages[i];
                var u = p.Url ?? string.Empty;
                if (u.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var onLogin = u.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!onLogin)
                {
                    best = p;
                    break;
                }

                if (best == null)
                {
                    best = p;
                }
            }

            if (best != null && !ReferenceEquals(best, _page))
            {
                logAction?.Invoke("[LOGIN] Chuyển sang tab TikTok (thường gặp sau đăng nhập Google).");
                _page = best;
            }
        }

        private static async Task<bool> TryEmbeddedTikTokUserStateAsync(IPage page)
        {
            if (page == null)
            {
                return false;
            }

            try
            {
                return await page.EvaluateAsync<bool>(
                    @"() => {
                        const hasLikelyUnique = (obj) => {
                            if (!obj || typeof obj !== 'object') return false;
                            const uid = obj.uniqueId || obj.unique_id || obj.uniqueID || obj.user_unique_id || obj.username || '';
                            return String(uid || '').trim().replace(/^@+/, '').length > 0;
                        };
                        const deepHas = (root) => {
                            const seen = new WeakSet();
                            const stack = [root];
                            let guard = 0;
                            while (stack.length > 0 && guard < 3500) {
                                guard++;
                                const cur = stack.pop();
                                if (!cur || typeof cur !== 'object') continue;
                                if (seen.has(cur)) continue;
                                seen.add(cur);
                                if (hasLikelyUnique(cur)) return true;
                                const keys = Object.keys(cur);
                                for (let i = 0; i < keys.length; i++) {
                                    const v = cur[keys[i]];
                                    if (v && typeof v === 'object') stack.push(v);
                                }
                            }
                            return false;
                        };
                        const parseScript = (id) => {
                            try {
                                const el = document.getElementById(id);
                                if (!el || !el.textContent) return false;
                                return deepHas(JSON.parse(el.textContent));
                            } catch (e) {
                                return false;
                            }
                        };
                        if (parseScript('SIGI_STATE')) return true;
                        if (parseScript('__UNIVERSAL_DATA_FOR_REHYDRATION__')) return true;
                        if (parseScript('__NEXT_DATA__')) return true;
                        try {
                            if (deepHas(window.SIGI_STATE) || deepHas(window.__UNIVERSAL_DATA_FOR_REHYDRATION__) || deepHas(window.__NEXT_DATA__)) {
                                return true;
                            }
                        } catch (e) {}
                        try {
                            const a = document.querySelector('a[href^=""/@""]');
                            if (a && a.getAttribute) {
                                const href = String(a.getAttribute('href') || '');
                                if (/^\/@[^\/\?\#]+/.test(href)) return true;
                            }
                        } catch (e) {}
                        return false;
                    }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsPageShowingLoggedInTikTokUserAsync(IPage page, CancellationToken cancellationToken)
        {
            if (page == null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var url = page.Url ?? string.Empty;
                if (url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }

                // Ưu tiên UI «Đăng nhập» — cookie cũ trên disk không đồng nghĩa đã login.
                foreach (var selector in TikTokSelectors.LoggedOutIndicators)
                {
                    var locOut = page.Locator(selector).First;
                    if (await IsVisibleAsync(locOut, ShortQueryTimeout).ConfigureAwait(false))
                    {
                        return false;
                    }
                }

                if (await TryEmbeddedTikTokUserStateAsync(page).ConfigureAwait(false))
                {
                    return true;
                }

                foreach (var selector in TikTokSelectors.LoggedInIndicators)
                {
                    var loc = page.Locator(selector).First;
                    if (await IsVisibleAsync(loc, ShortQueryTimeout).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // best effort
            }

            return false;
        }

        private async Task<bool> IsLoggedInOnAnyOpenTiktokPageAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (_context?.Pages == null || _context.Pages.Count < 2)
            {
                return false;
            }

            for (var i = 0; i < _context.Pages.Count; i++)
            {
                var p = _context.Pages[i];
                if (ReferenceEquals(p, _page))
                {
                    continue;
                }

                var u = p.Url ?? string.Empty;
                if (u.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!await IsPageShowingLoggedInTikTokUserAsync(p, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                logAction?.Invoke("[LOGIN] Đã đăng nhập trên tab TikTok khác — dùng tab này làm chính.");
                _page = p;
                try
                {
                    await p.BringToFrontAsync().ConfigureAwait(false);
                }
                catch
                {
                    // best effort
                }

                return true;
            }

            return false;
        }

        private async Task<bool> IsLoggedInAsync(CancellationToken cancellationToken)
        {
            if (_page == null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var url = _page.Url ?? string.Empty;
                var onTikTok = url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) >= 0;
                var onLoginPath = onTikTok && url.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0;

                // Không tự nhảy khỏi /login chỉ vì đã có cookie — làm vậy khiến user chưa kịp đăng nhập đã bị kéo sang trang khác.
                // Trường hợp QR kẹt trên /login vẫn do vòng TryRecoverSessionViaForyouAsync trong EnsureLoggedInAsync xử lý.

                if (!onTikTok)
                {
                    var rawUrl = _page.Url ?? string.Empty;
                    if (rawUrl.IndexOf("google.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rawUrl.IndexOf("gstatic.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }

                    if (!await TryGotoSoftAsync(TikTokHomeUrl, cancellationToken).ConfigureAwait(false) &&
                        !await TryGotoSoftAsync(TikTokForyouUrl, cancellationToken).ConfigureAwait(false))
                    {
                        return await HasLikelyTikTokSessionCookiesAsync().ConfigureAwait(false);
                    }

                    await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                }

                return await IsPageShowingLoggedInTikTokUserAsync(_page, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // If detection fails, treat as not logged in to keep flow safe.
            }

            return false;
        }

        /// <summary>
        /// TikTok sets these when a real session exists; QR can complete in the background while the
        /// login page DOM still shows the QR panel.
        /// </summary>
        // Do not include uid_tt alone — guests may have it without a real login session.
        private static readonly string[] TikTokSessionCookieNames =
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

        private async Task<bool> HasLikelyTikTokSessionCookiesAsync()
        {
            if (_context == null)
            {
                return false;
            }

            try
            {
                // Null URL list = all cookies in the persistent context (login cookies may sit on .tiktok.com paths).
                var cookies = await _context.CookiesAsync((IEnumerable<string>)null).ConfigureAwait(false);
                for (var i = 0; i < cookies.Count; i++)
                {
                    var c = cookies[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Domain))
                    {
                        continue;
                    }

                    if (c.Domain.IndexOf("tiktok", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var name = c.Name.Trim();
                    var value = (c.Value ?? string.Empty).Trim();
                    if (value.Length < 6)
                    {
                        continue;
                    }

                    for (var n = 0; n < TikTokSessionCookieNames.Length; n++)
                    {
                        if (!string.Equals(name, TikTokSessionCookieNames[n], StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (string.Equals(name, "passport_auth_status", StringComparison.OrdinalIgnoreCase))
                        {
                            if (value.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }

                            continue;
                        }

                        if (value.Length >= 8)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // best effort
            }

            return false;
        }

        private async Task WaitForWarmupVideoSearchAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
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
                } catch (e) {}
                return false;
            }";

            var deadline = DateTime.UtcNow.AddSeconds(14);
            var minStop = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await _page.EvaluateAsync<bool>(probe).ConfigureAwait(false) && DateTime.UtcNow >= minStop)
                    {
                        logAction?.Invoke("[LIVE] Video search results ready.");
                        await RandomDelayAsync(600, 1200, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                }
                catch
                {
                    // Page may still be loading.
                }

                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            }

            logAction?.Invoke("[LIVE] WARN: Video search grid slow to load — continuing best-effort.");
        }

        private async Task ScrollWarmupVideoSearchFeedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _page.EvaluateAsync(@"() => {
                    const feed = document.querySelector('[data-e2e=""search-video-feed""], main, [id*=""search""]');
                    if (feed && feed.scrollHeight > feed.clientHeight + 40) {
                        feed.scrollBy(0, Math.max(320, Math.floor(feed.clientHeight * 0.85)));
                        return;
                    }
                    window.scrollBy(0, Math.max(400, Math.floor(window.innerHeight * 0.9)));
                }").ConfigureAwait(false);
            }
            catch
            {
                await _page.EvaluateAsync("window.scrollBy(0, window.innerHeight);").ConfigureAwait(false);
            }
        }

        private async Task<int> CountEligibleWarmupSearchItemsAsync(string excludeOwnUniqueId)
        {
            var items = _page.Locator("div[data-e2e='search_video-item']");
            var count = await items.CountAsync().ConfigureAwait(false);
            if (count == 0)
            {
                return 0;
            }

            var eligible = 0;
            for (var i = 0; i < count; i++)
            {
                if (await IsWarmupSearchItemEligibleAsync(items.Nth(i), excludeOwnUniqueId).ConfigureAwait(false))
                {
                    eligible++;
                }
            }

            return eligible;
        }

        private async Task<string> GetWarmupSearchVideoHrefAsync(int oneBasedEligibleIndex, string excludeOwnUniqueId)
        {
            var items = _page.Locator("div[data-e2e='search_video-item']");
            var count = await items.CountAsync().ConfigureAwait(false);
            var pick = oneBasedEligibleIndex;
            for (var i = 0; i < count; i++)
            {
                var item = items.Nth(i);
                if (!await IsWarmupSearchItemEligibleAsync(item, excludeOwnUniqueId).ConfigureAwait(false))
                {
                    continue;
                }

                pick--;
                if (pick == 0)
                {
                    var href = await item.Locator("a[href*='/video/']").First.GetAttributeAsync("href").ConfigureAwait(false);
                    return href ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private async Task<bool> IsWarmupSearchItemEligibleAsync(ILocator item, string excludeOwnUniqueId)
        {
            try
            {
                var videoHref = await item.Locator("a[href*='/video/']").First.GetAttributeAsync("href").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(videoHref))
                {
                    return false;
                }

                if (IsWarmupVideoIdExcluded(videoHref))
                {
                    return false;
                }

                var exclude = (excludeOwnUniqueId ?? string.Empty).Trim().TrimStart('@');
                if (!string.IsNullOrWhiteSpace(exclude))
                {
                    // URL video dạng /@username/video/123 — đáng tin hơn selector author (DOM hay đổi).
                    var authorFromVideo = ExtractAuthorFromVideoUrl(videoHref);
                    if (!string.IsNullOrWhiteSpace(authorFromVideo) &&
                        string.Equals(authorFromVideo, exclude, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var authorLink = item.Locator("a[data-e2e='search-video-user-link'], a[href*='/@']").First;
                    if (await authorLink.CountAsync().ConfigureAwait(false) > 0)
                    {
                        var href = (await authorLink.GetAttributeAsync("href").ConfigureAwait(false)) ?? string.Empty;
                        var author = ExtractAuthorFromProfileOrVideoHref(href);
                        if (!string.IsNullOrWhiteSpace(author) &&
                            string.Equals(author, exclude, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    var uidNode = item.Locator("[data-e2e='search-user-unique-id']").First;
                    if (await uidNode.CountAsync().ConfigureAwait(false) > 0)
                    {
                        var text = (await uidNode.InnerTextAsync().ConfigureAwait(false) ?? string.Empty).Trim().TrimStart('@');
                        if (string.Equals(text, exclude, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }

                if (await item.Locator(
                        "[data-e2e*='anchor'], [data-e2e*='product'], a[href*='/shop/'], [class*='VideoAnchor'], [class*='ShopAnchor']")
                    .CountAsync().ConfigureAwait(false) > 0)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractAuthorFromVideoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var m = System.Text.RegularExpressions.Regex.Match(
                url,
                @"/@([^/?#]+)/video/",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return string.Empty;
            }

            try
            {
                return Uri.UnescapeDataString(m.Groups[1].Value).Trim().TrimStart('@');
            }
            catch
            {
                return m.Groups[1].Value.Trim().TrimStart('@');
            }
        }

        private static string ExtractAuthorFromProfileOrVideoHref(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return string.Empty;
            }

            var fromVideo = ExtractAuthorFromVideoUrl(href);
            if (!string.IsNullOrWhiteSpace(fromVideo))
            {
                return fromVideo;
            }

            var m = System.Text.RegularExpressions.Regex.Match(
                href,
                @"/@([^/?#]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return string.Empty;
            }

            try
            {
                return Uri.UnescapeDataString(m.Groups[1].Value).Trim().TrimStart('@');
            }
            catch
            {
                return m.Groups[1].Value.Trim().TrimStart('@');
            }
        }

        private static string NormalizeTikTokUrl(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return TikTokHomeUrl;
            }

            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }

            return href.StartsWith("/", StringComparison.Ordinal) ? "https://www.tiktok.com" + href : TikTokHomeUrl + href;
        }

        private static string ExtractVideoIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var m = System.Text.RegularExpressions.Regex.Match(url, @"/video/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        /// <summary>Drop ?q= and other query params so TikTok opens a single video page, not search-feed carousel.</summary>
        private static string ToCanonicalVideoPageUrl(string href)
        {
            var normalized = NormalizeTikTokUrl(href);
            var idx = normalized.IndexOf("?", StringComparison.Ordinal);
            if (idx > 0 && normalized.IndexOf("/video/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                normalized = normalized.Substring(0, idx);
            }

            return normalized;
        }

        private bool IsWarmupVideoIdExcluded(string videoHref)
        {
            var id = ExtractVideoIdFromUrl(NormalizeTikTokUrl(videoHref));
            return !string.IsNullOrWhiteSpace(id) && IsWarmupSearchPickVideoIdExcluded(id);
        }

        private const string PickActiveVideoIndexJs = @"() => {
            const vh = window.innerHeight;
            const cy = vh / 2;
            const videos = Array.from(document.querySelectorAll('video'));
            let bestIdx = -1;
            let bestScore = -1;
            for (let i = 0; i < videos.length; i++) {
                const v = videos[i];
                const r = v.getBoundingClientRect();
                if (r.width < 80 || r.height < 120) continue;
                const visTop = Math.max(0, r.top);
                const visBot = Math.min(vh, r.bottom);
                const visH = Math.max(0, visBot - visTop);
                if (visH < r.height * 0.35) continue;
                const vcy = r.top + r.height / 2;
                const dist = Math.abs(vcy - cy);
                const score = (r.width * visH) / (1 + dist * 0.02);
                if (score > bestScore) { bestScore = score; bestIdx = i; }
            }
            return bestIdx;
        }";

        /// <summary>Video đang hiển thị giữa viewport — TikTok hay lazy-load src ngoài vùng này.</summary>
        private async Task<int> ResolveActiveVideoIndexAsync()
        {
            try
            {
                return await _page.EvaluateAsync<int>(PickActiveVideoIndexJs).ConfigureAwait(false);
            }
            catch
            {
                return -1;
            }
        }

        private async Task<ILocator> GetActiveVideoLocatorAsync(CancellationToken cancellationToken, int timeoutMs = 5000)
        {
            EnsurePageReady();
            cancellationToken.ThrowIfCancellationRequested();

            var videos = _page.Locator("video");
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(500, timeoutMs));
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await videos.CountAsync().ConfigureAwait(false) > 0)
                {
                    break;
                }

                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }

            var total = await videos.CountAsync().ConfigureAwait(false);
            if (total <= 0)
            {
                throw new TimeoutException("No video element attached.");
            }

            var index = await ResolveActiveVideoIndexAsync().ConfigureAwait(false);
            var useIdx = index >= 0 && index < total ? index : 0;
            var active = videos.Nth(useIdx);
            await active.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = Math.Max(500, timeoutMs)
            }).ConfigureAwait(false);
            return active;
        }

        /// <summary>Cuộn video active vào viewport; browse overlay không wheel (tránh nhảy clip).</summary>
        private async Task NudgeActiveVideoIntoViewAsync(ILocator video, CancellationToken cancellationToken, bool allowWheelNudge = true)
        {
            try
            {
                await video.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            if (!allowWheelNudge)
            {
                return;
            }

            await Task.Delay(_random.Next(150, 280), cancellationToken).ConfigureAwait(false);
            var box = await video.BoundingBoxAsync().ConfigureAwait(false);
            if (box == null || box.Width <= 20 || box.Height <= 20)
            {
                return;
            }

            var cx = (float)(box.X + box.Width * 0.5);
            var cy = (float)(box.Y + box.Height * 0.5);
            await _page.Mouse.MoveAsync(cx, cy).ConfigureAwait(false);
            await _page.Mouse.WheelAsync(0, _random.Next(100, 180)).ConfigureAwait(false);
            await Task.Delay(_random.Next(350, 550), cancellationToken).ConfigureAwait(false);
            await _page.Mouse.WheelAsync(0, -_random.Next(40, 90)).ConfigureAwait(false);
            await Task.Delay(_random.Next(200, 350), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Locator + scroll/wheel: đảm bảo video giữa màn hình có src và đang phát.
        /// Trả false để caller skip slot — không ném exception.
        /// </summary>
        public async Task<bool> EnsureVideoPlayingAsync(CancellationToken cancellationToken, int timeoutMs = 5000, Action<string> logAction = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inBrowse = !_warmupInForyouMode && await IsBrowseVideoOverlayAsync().ConfigureAwait(false);
                var video = await GetActiveVideoLocatorAsync(cancellationToken, timeoutMs).ConfigureAwait(false);
                await NudgeActiveVideoIntoViewAsync(video, cancellationToken, allowWheelNudge: !inBrowse).ConfigureAwait(false);

                await video.EvaluateAsync(@"v => {
                    if (!v) return;
                    try {
                        v.muted = true;
                        v.playsInline = true;
                        if (v.paused) v.play().catch(() => {});
                    } catch (e) {}
                }").ConfigureAwait(false);

                await Task.Delay(_random.Next(400, 650), cancellationToken).ConfigureAwait(false);
                if (await IsVideoPlayingAsync().ConfigureAwait(false))
                {
                    logAction?.Invoke("[LIVE] Video active đang phát (Locator + nudge).");
                    return true;
                }

                logAction?.Invoke("[LIVE] Video active chưa play sau nudge — thử thêm một lần.");
                if (!inBrowse)
                {
                    await NudgeActiveVideoIntoViewAsync(video, cancellationToken).ConfigureAwait(false);
                }

                await video.EvaluateAsync(@"v => {
                    if (!v) return;
                    try {
                        v.muted = true;
                        if (v.paused) v.play().catch(() => {});
                    } catch (e) {}
                }").ConfigureAwait(false);
                await Task.Delay(_random.Next(450, 700), cancellationToken).ConfigureAwait(false);
                return await IsVideoPlayingAsync().ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                logAction?.Invoke("[LIVE] Timeout chờ thẻ video active — thử cách khác.");
                return false;
            }
            catch (PlaywrightException ex) when (ex.Message.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                logAction?.Invoke("[LIVE] Timeout Playwright chờ video — thử cách khác.");
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private async Task EnsureVideoPlayingForWatchAsync(CancellationToken cancellationToken, Action<string> logAction = null)
        {
            // #region agent log
            DebugAgentLog.Write("A", "BrowserAutomation.EnsureVideoPlaying", "start", await ProbePageEngagementStateAsync().ConfigureAwait(false));
            // #endregion

            await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);
            await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(8)).ConfigureAwait(false);

            var inBrowse = !_warmupInForyouMode && await IsBrowseVideoOverlayAsync().ConfigureAwait(false);

            await ThrowIfBrowseShopBlockedAsync(logAction, hoverPlayer: inBrowse).ConfigureAwait(false);

            if (!await EnsureVideoPlayingAsync(cancellationToken, inBrowse ? 4000 : 6000, logAction).ConfigureAwait(false))
            {
                await TryEnsureVideoPlayingOnceAsync(
                        cancellationToken,
                        browseSafe: inBrowse,
                        mediaRecovery: inBrowse)
                    .ConfigureAwait(false);
            }

            logAction?.Invoke("[LIVE] Đợi video tải media...");
            var hydrated = await IsVideoPageHydratedAsync().ConfigureAwait(false);
            var firstWait = inBrowse
                ? TimeSpan.FromSeconds(10)
                : hydrated
                    ? TimeSpan.FromSeconds(12)
                    : TimeSpan.FromSeconds(5);
            var mediaReady = await WaitForVideoMediaReadyAsync(cancellationToken, firstWait).ConfigureAwait(false);
            var browseRecoveryUsed = false;
            if (!mediaReady && hydrated && inBrowse && !browseRecoveryUsed)
            {
                browseRecoveryUsed = true;
                try
                {
                    mediaReady = await TryRecoverBrowseMediaAsync(cancellationToken, logAction).ConfigureAwait(false);
                }
                catch (ShopVideoGateException)
                {
                    throw;
                }
            }

            if (!mediaReady && hydrated)
            {
                await TryEnsureVideoPlayingOnceAsync(
                        cancellationToken,
                        browseSafe: inBrowse,
                        mediaRecovery: inBrowse)
                    .ConfigureAwait(false);
                mediaReady = await WaitForVideoMediaReadyAsync(cancellationToken, TimeSpan.FromSeconds(inBrowse ? 6 : 6))
                    .ConfigureAwait(false);
            }

            if (!mediaReady)
            {
                if (inBrowse)
                {
                    await ThrowIfBrowseShopBlockedAsync(logAction, hoverPlayer: true).ConfigureAwait(false);
                    if (!browseRecoveryUsed)
                    {
                        browseRecoveryUsed = true;
                        try
                        {
                            mediaReady = await TryRecoverBrowseMediaAsync(cancellationToken, logAction).ConfigureAwait(false);
                        }
                        catch (ShopVideoGateException)
                        {
                            throw;
                        }
                    }
                }

                if (!mediaReady && !inBrowse)
                {
                    logAction?.Invoke("[LIVE] Media chưa sẵn sàng — reload một lần...");
                    if (!string.IsNullOrWhiteSpace(_warmupLockedVideoUrl))
                    {
                        await SafeGotoAsync(_warmupLockedVideoUrl, cancellationToken).ConfigureAwait(false);
                        await RandomDelayAsync(1500, 2500, cancellationToken).ConfigureAwait(false);
                        await WaitForVideoPageHydratedAsync(cancellationToken, logAction, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        mediaReady = await WaitForVideoMediaReadyAsync(cancellationToken, TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                    }
                }
            }

            if (!mediaReady)
            {
                await ThrowIfBrowseShopBlockedAsync(logAction, hoverPlayer: inBrowse).ConfigureAwait(false);
                // #region agent log
                DebugAgentLog.Write("A", "BrowserAutomation.EnsureVideoPlaying", "media never ready", await ProbePageEngagementStateAsync().ConfigureAwait(false));
                // #endregion
                logAction?.Invoke("[LIVE] Video vẫn đen / không có frame — bỏ clip.");
                ExcludeCurrentWarmupVideo();
                throw new ShopVideoGateException("Video media never became ready (black player).");
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (inBrowse && !await IsVideoMediaPlayableAsync().ConfigureAwait(false))
                {
                    await ThrowIfBrowseShopBlockedAsync(logAction, hoverPlayer: true).ConfigureAwait(false);
                    if (!browseRecoveryUsed)
                    {
                        browseRecoveryUsed = true;
                        try
                        {
                            if (await TryRecoverBrowseMediaAsync(cancellationToken, logAction).ConfigureAwait(false))
                            {
                                break;
                            }
                        }
                        catch (ShopVideoGateException)
                        {
                            throw;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                await TryDismissShopInAppOverlayAsync(cancellationToken).ConfigureAwait(false);
                await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);
                await TryEnsureVideoPlayingOnceAsync(cancellationToken, browseSafe: inBrowse).ConfigureAwait(false);
                if (await IsVideoPlayingAsync().ConfigureAwait(false))
                {
                    // #region agent log
                    DebugAgentLog.Write("A", "BrowserAutomation.EnsureVideoPlaying", "playing", new { attempt });
                    // #endregion
                    logAction?.Invoke("[LIVE] Video đang phát.");
                    return;
                }

                await Task.Delay(_random.Next(600, 1100), cancellationToken).ConfigureAwait(false);
            }

            // #region agent log
            DebugAgentLog.Write("A", "BrowserAutomation.EnsureVideoPlaying", "still not playing after retries", await ProbePageEngagementStateAsync().ConfigureAwait(false));
            // #endregion
            logAction?.Invoke("[LIVE] Có media nhưng chưa play được — vẫn tiếp tục xem (có thể đang pause).");
        }

        private async Task<bool> WaitForVideoMediaReadyAsync(CancellationToken cancellationToken, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsVideoMediaPlayableAsync().ConfigureAwait(false))
                {
                    return true;
                }

                await Task.Delay(450, cancellationToken).ConfigureAwait(false);
            }

            return await IsVideoMediaPlayableAsync().ConfigureAwait(false);
        }

        private async Task<bool> IsVideoMediaPlayableAsync()
        {
            try
            {
                return await _page.EvaluateAsync<bool>(@"() => {
                    const vh = window.innerHeight;
                    const cy = vh / 2;
                    const videos = Array.from(document.querySelectorAll('video'));
                    if (!videos.length) return false;
                    let bestIdx = -1;
                    let bestScore = -1;
                    for (let i = 0; i < videos.length; i++) {
                        const v = videos[i];
                        const r = v.getBoundingClientRect();
                        if (r.width < 80 || r.height < 120) continue;
                        const visTop = Math.max(0, r.top);
                        const visBot = Math.min(vh, r.bottom);
                        const visH = Math.max(0, visBot - visTop);
                        if (visH < r.height * 0.35) continue;
                        const vcy = r.top + r.height / 2;
                        const dist = Math.abs(vcy - cy);
                        const score = (r.width * visH) / (1 + dist * 0.02);
                        if (score > bestScore) { bestScore = score; bestIdx = i; }
                    }
                    const v = bestIdx >= 0 ? videos[bestIdx] : videos[0];
                    if (!v || v.error) return false;
                    const w = v.videoWidth || 0;
                    const h = v.videoHeight || 0;
                    const rs = v.readyState || 0;
                    return rs >= 2 || (w > 16 && h > 16);
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task TryEnsureVideoPlayingOnceAsync(
            CancellationToken cancellationToken,
            bool browseSafe = false,
            bool mediaRecovery = false)
        {
            try
            {
                if (await IsVideoPlayingAsync().ConfigureAwait(false))
                {
                    return;
                }

                ILocator activeVideo = null;
                try
                {
                    activeVideo = await GetActiveVideoLocatorAsync(cancellationToken, 4000).ConfigureAwait(false);
                    if (!browseSafe)
                    {
                        await NudgeActiveVideoIntoViewAsync(activeVideo, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                }

                if (activeVideo != null)
                {
                    await activeVideo.EvaluateAsync(@"v => {
                        if (!v) return;
                        try {
                            v.muted = true;
                            v.playsInline = true;
                            if (v.paused) v.play().catch(() => {});
                        } catch (e) {}
                    }").ConfigureAwait(false);
                }
                else
                {
                    await _page.EvaluateAsync(@"() => {
                        const vh = window.innerHeight;
                        const cy = vh / 2;
                        const videos = Array.from(document.querySelectorAll('video'));
                        let best = null;
                        let bestScore = -1;
                        for (const v of videos) {
                            const r = v.getBoundingClientRect();
                            if (r.width < 80 || r.height < 120) continue;
                            const visH = Math.max(0, Math.min(vh, r.bottom) - Math.max(0, r.top));
                            if (visH < r.height * 0.35) continue;
                            const dist = Math.abs(r.top + r.height / 2 - cy);
                            const score = (r.width * visH) / (1 + dist * 0.02);
                            if (score > bestScore) { bestScore = score; best = v; }
                        }
                        const target = best || videos[0];
                        if (!target) return;
                        try {
                            target.muted = true;
                            target.playsInline = true;
                            if (target.paused) target.play().catch(() => {});
                        } catch (e) {}
                    }").ConfigureAwait(false);
                }

                if (await IsVideoPlayingAsync().ConfigureAwait(false))
                {
                    return;
                }

                if (browseSafe && !mediaRecovery)
                {
                    return;
                }

                // 2) Nút Play lớn (không click giữa video nếu đang pause overlay — tránh toggle sai)
                var bigPlay = _page.Locator(
                    "[data-e2e='play-icon'], [data-e2e='browse-play-icon'], button[aria-label*='Play'], button[aria-label*='Phát'], [class*='PlayButton'], [class*='play-button']");
                if (await bigPlay.CountAsync().ConfigureAwait(false) > 0)
                {
                    var btn = bigPlay.First;
                    if (await IsVisibleAsync(btn, TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                    {
                        await HumanClickLocatorAsync(btn, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                        if (await IsVideoPlayingAsync().ConfigureAwait(false))
                        {
                            return;
                        }
                    }
                }

                // 3) Phím Space (gesture người dùng)
                try
                {
                    await _page.Keyboard.PressAsync("Space").ConfigureAwait(false);
                    await Task.Delay(350, cancellationToken).ConfigureAwait(false);
                    if (await IsVideoPlayingAsync().ConfigureAwait(false))
                    {
                        return;
                    }
                }
                catch
                {
                }

                // 4) Click giữa player chỉ khi vẫn pause
                if (!await IsVideoPlayingAsync().ConfigureAwait(false))
                {
                    try
                    {
                        var player = activeVideo ?? await GetActiveVideoLocatorAsync(cancellationToken, 3000).ConfigureAwait(false);
                        var box = await player.BoundingBoxAsync().ConfigureAwait(false);
                        if (box != null && box.Width > 40 && box.Height > 40)
                        {
                            await _page.Mouse.MoveAsync(
                                box.X + box.Width / 2 + _random.Next(-8, 9),
                                box.Y + box.Height / 2 + _random.Next(-8, 9)).ConfigureAwait(false);
                            await Task.Delay(_random.Next(80, 200), cancellationToken).ConfigureAwait(false);
                            await _page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                // Best-effort.
            }
        }

        private async Task HumanClickLocatorAsync(ILocator locator, CancellationToken cancellationToken)
        {
            try
            {
                var box = await locator.BoundingBoxAsync().ConfigureAwait(false);
                if (box != null && box.Width > 2 && box.Height > 2)
                {
                    var x = (float)(box.X + box.Width * (0.35 + _random.NextDouble() * 0.3));
                    var y = (float)(box.Y + box.Height * (0.35 + _random.NextDouble() * 0.3));
                    await _page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
                    await Task.Delay(_random.Next(90, 220), cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await _page.Mouse.ClickAsync(x, y).ConfigureAwait(false);
                        return;
                    }
                    catch
                    {
                        // Click có thể đã tới DOM nhưng Playwright ném lỗi — không fallback (tránh double-click toggle tim).
                        return;
                    }
                }
            }
            catch
            {
            }

            try
            {
                await locator.ClickAsync(new LocatorClickOptions { Timeout = 8000, ClickCount = 1 }).ConfigureAwait(false);
            }
            catch
            {
                await locator.ClickAsync(new LocatorClickOptions { Timeout = 5000, Force = true, ClickCount = 1 }).ConfigureAwait(false);
            }
        }

        private async Task TryDismissShopInAppOverlayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _page.EvaluateAsync(@"() => {
                    const body = document.body ? document.body.innerText : '';
                    if (body.indexOf('TikTok Shop') < 0 && body.indexOf('ứng dụng TikTok') < 0) return;
                    const close = document.querySelector('[data-e2e=""close-icon""], button[aria-label*=""Close""], button[aria-label*=""Đóng""]');
                    if (close) close.click();
                }").ConfigureAwait(false);
                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        /// <summary>Play the selected clip once; pause on end instead of looping, while blocking feed keyboard advance.</summary>
        private async Task StabilizeWarmupVideoPlaybackAsync(CancellationToken cancellationToken)
        {
            var browseLight = !_warmupInForyouMode;
            try
            {
                if (browseLight)
                {
                    await _page.EvaluateAsync(@"() => {
                        if (window.__warmupPlaybackCleanup) {
                            try { window.__warmupPlaybackCleanup(); } catch (e) {}
                        }
                        const vh = window.innerHeight;
                        const cy = vh / 2;
                        const videos = Array.from(document.querySelectorAll('video'));
                        let v = null;
                        let bestScore = -1;
                        for (const el of videos) {
                            const r = el.getBoundingClientRect();
                            if (r.width < 80 || r.height < 120) continue;
                            const visH = Math.max(0, Math.min(vh, r.bottom) - Math.max(0, r.top));
                            if (visH < el.height * 0.35) continue;
                            const dist = Math.abs(r.top + r.height / 2 - cy);
                            const score = (r.width * visH) / (1 + dist * 0.02);
                            if (score > bestScore) { bestScore = score; v = el; }
                        }
                        if (!v) v = videos[0];
                        if (!v) return;
                        v.loop = false;
                        v.muted = true;
                        const blockFeedKeys = (e) => {
                            const k = e.key || '';
                            if (k === 'ArrowDown' || k === 'ArrowUp' || k === 'PageDown' || k === 'PageUp') {
                                e.stopImmediatePropagation();
                                e.preventDefault();
                            }
                        };
                        document.addEventListener('keydown', blockFeedKeys, true);
                        window.__warmupPlaybackCleanup = () => {
                            document.removeEventListener('keydown', blockFeedKeys, true);
                        };
                        try { if (v.paused) v.play(); } catch (e) {}
                    }").ConfigureAwait(false);
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await _page.EvaluateAsync(@"() => {
                    if (window.__warmupPlaybackCleanup) {
                        try { window.__warmupPlaybackCleanup(); } catch (e) {}
                    }
                    const vh = window.innerHeight;
                    const cy = vh / 2;
                    const videos = Array.from(document.querySelectorAll('video'));
                    let v = null;
                    let bestScore = -1;
                    for (const el of videos) {
                        const r = el.getBoundingClientRect();
                        if (r.width < 80 || r.height < 120) continue;
                        const visH = Math.max(0, Math.min(vh, r.bottom) - Math.max(0, r.top));
                        if (visH < el.height * 0.35) continue;
                        const dist = Math.abs(r.top + r.height / 2 - cy);
                        const score = (r.width * visH) / (1 + dist * 0.02);
                        if (score > bestScore) { bestScore = score; v = el; }
                    }
                    if (!v) v = videos[0];
                    if (!v) return;
                    window.__warmupSeenNearEnd = false;
                    v.loop = false;
                    v.muted = true;
                    let endedNaturally = false;
                    let maxTimeSeen = 0;
                    const markEnded = () => {
                        endedNaturally = true;
                        try { v.loop = false; v.pause(); } catch (e) {}
                    };
                    const onEnded = () => { markEnded(); };
                    const onTimeUpdate = () => {
                        if (endedNaturally) {
                            if (!v.paused) { try { v.pause(); } catch (e) {} }
                            return;
                        }
                        const t = v.currentTime || 0;
                        const dur = v.duration;
                        if (t > maxTimeSeen) maxTimeSeen = t;
                        // TikTok tự tua về đầu = đang loop — chặn.
                        if (maxTimeSeen > 2.5 && t < 0.75 && (!isFinite(dur) || maxTimeSeen > Math.min(dur * 0.45, dur - 0.5))) {
                            markEnded();
                            return;
                        }
                        if (isFinite(dur) && dur > 0 && t >= dur - 0.35) {
                            markEnded();
                        }
                    };
                    const onPause = () => {
                        if (endedNaturally) return;
                        const dur = v.duration;
                        const t = v.currentTime || 0;
                        if (isFinite(dur) && dur > 0 && t >= dur - 0.45) {
                            markEnded();
                            return;
                        }
                        // Chỉ resume nếu pause giữa clip (buffer) — không resume khi đã gần hết / về đầu.
                        if (isFinite(dur) && dur > 0 && t > 0.4 && t < dur - 0.6) {
                            try { v.play(); } catch (e) {}
                        }
                    };
                    const blockFeedKeys = (e) => {
                        const k = e.key || '';
                        if (k === 'ArrowDown' || k === 'ArrowUp' || k === 'PageDown' || k === 'PageUp') {
                            e.stopImmediatePropagation();
                            e.preventDefault();
                        }
                    };
                    const keepNoLoop = setInterval(() => {
                        try {
                            v.loop = false;
                            if (endedNaturally && !v.paused) v.pause();
                        } catch (e) {}
                    }, 400);
                    v.addEventListener('ended', onEnded, true);
                    v.addEventListener('timeupdate', onTimeUpdate);
                    v.addEventListener('pause', onPause);
                    document.addEventListener('keydown', blockFeedKeys, true);
                    window.__warmupPlaybackCleanup = () => {
                        clearInterval(keepNoLoop);
                        v.removeEventListener('ended', onEnded, true);
                        v.removeEventListener('timeupdate', onTimeUpdate);
                        v.removeEventListener('pause', onPause);
                        document.removeEventListener('keydown', blockFeedKeys, true);
                    };
                    try { v.play(); } catch (e) {}
                }").ConfigureAwait(false);
                // #region agent log
                DebugAgentLog.Write(
                    "F",
                    "BrowserAutomation.StabilizeWarmupVideoPlayback",
                    "pause-at-end enabled",
                    new { lockedId = _warmupLockedVideoId, loop = false },
                    "post-fix");
                // #endregion
            }
            catch
            {
                // Best-effort.
            }

            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Lấy currentTime/duration từ thẻ video đang active (không dùng đồng hồ C#).</summary>
        public async Task<(double CurrentTime, double Duration)> GetVideoProgressAsync()
        {
            try
            {
                var total = await _page.Locator("video").CountAsync().ConfigureAwait(false);
                if (total <= 0)
                {
                    return (0, 0);
                }

                var index = await ResolveActiveVideoIndexAsync().ConfigureAwait(false);
                var useIdx = index >= 0 && index < total ? index : 0;
                var snapshot = await _page.Locator("video").Nth(useIdx).EvaluateAsync<VideoProgressSnapshot>(@"v => {
                    if (!v || !isFinite(v.duration) || v.duration <= 0) return null;
                    return { current: v.currentTime, duration: v.duration };
                }").ConfigureAwait(false);

                if (snapshot == null)
                {
                    return (0, 0);
                }

                return (snapshot.current, snapshot.duration);
            }
            catch
            {
                return (0, 0);
            }
        }

        public async Task<double> GetCurrentVideoDurationSecondsAsync()
        {
            var (_, duration) = await GetVideoProgressAsync().ConfigureAwait(false);
            return duration;
        }

        /// <summary>Tiêu đề / mô tả video đang xem (browse, For You, trang video).</summary>
        public async Task<string> GetCurrentVideoCaptionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            try
            {
                var caption = await _page.EvaluateAsync<string>(@"() => {
                    const pick = (el) => {
                        if (!el) return '';
                        const t = (el.innerText || el.textContent || '').trim();
                        return t.length > 2 ? t : '';
                    };
                    for (const sel of [
                        '[data-e2e=""browse-video-desc""]',
                        '[data-e2e=""video-desc""]',
                        '[data-e2e=""video-description""]',
                        '[data-e2e=""browse-video-title""]',
                        'h1[data-e2e=""video-title""]'
                    ]) {
                        const t = pick(document.querySelector(sel));
                        if (t) return t;
                    }
                    const spanParts = [];
                    document.querySelectorAll('[data-e2e=""new-desc-span""], [data-e2e=""video-desc-span""]')
                        .forEach(el => {
                            const t = pick(el);
                            if (t) spanParts.push(t);
                        });
                    if (spanParts.length) return spanParts.join(' ').trim();
                    const og = document.querySelector('meta[property=""og:description""]');
                    if (og && og.content) {
                        const t = String(og.content).trim();
                        if (t.length > 2) return t;
                    }
                    return '';
                }").ConfigureAwait(false);

                return (caption ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<bool> ShareCurrentVideoAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();
            await TryDismissBlockingOverlaysAsync(cancellationToken, logAction).ConfigureAwait(false);

            var selectors = new[]
            {
                "[data-e2e='share-icon']",
                "[data-e2e='browse-share-icon']",
                "button[aria-label*='Share']",
                "button[aria-label*='Chia sẻ']"
            };

            foreach (var selector in selectors)
            {
                var btn = _page.Locator(selector).First;
                if (!await IsVisibleAsync(btn, TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                {
                    continue;
                }

                try
                {
                    logAction?.Invoke("[LIVE] Đang mở share...");
                    await HumanClickLocatorAsync(btn, cancellationToken).ConfigureAwait(false);
                    await RandomDelayAsync(700, 1400, cancellationToken).ConfigureAwait(false);
                    await EnsureSharePanelClosedAsync(cancellationToken, logAction).ConfigureAwait(false);

                    logAction?.Invoke("[LIVE] Đã tương tác share.");
                    return true;
                }
                catch
                {
                    // try next
                }
            }

            logAction?.Invoke("[LIVE] Không tìm thấy nút share — bỏ qua.");
            return false;
        }

        private async Task ReplayWarmupVideoFromStartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _page.EvaluateAsync(@"() => {
                    window.__warmupSeenNearEnd = false;
                    const v = document.querySelector('video');
                    if (!v) return;
                    try {
                        v.loop = false;
                        v.currentTime = 0;
                        v.muted = true;
                        v.play();
                    } catch (e) {}
                }").ConfigureAwait(false);
                await StabilizeWarmupVideoPlaybackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task<bool> IsVideoPlayingAsync()
        {
            try
            {
                var total = await _page.Locator("video").CountAsync().ConfigureAwait(false);
                if (total <= 0)
                {
                    return false;
                }

                var index = await ResolveActiveVideoIndexAsync().ConfigureAwait(false);
                var useIdx = index >= 0 && index < total ? index : 0;
                return await _page.Locator("video").Nth(useIdx).EvaluateAsync<bool>(@"v => {
                    if (!v) return false;
                    return !v.paused && !v.ended && v.readyState >= 2 &&
                        (v.currentTime > 0 || v.readyState >= 3);
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsWarmupVideoAtNaturalEndAsync()
        {
            try
            {
                var total = await _page.Locator("video").CountAsync().ConfigureAwait(false);
                if (total <= 0)
                {
                    return false;
                }

                var index = await ResolveActiveVideoIndexAsync().ConfigureAwait(false);
                var useIdx = index >= 0 && index < total ? index : 0;
                return await _page.Locator("video").Nth(useIdx).EvaluateAsync<bool>(@"v => {
                    if (!v || !isFinite(v.duration) || v.duration <= 0) return false;
                    if (v.ended || (v.paused && v.currentTime >= v.duration - 0.4)) return true;
                    if (window.__warmupSeenNearEnd && (v.currentTime || 0) < 1.0) return true;
                    if ((v.currentTime || 0) >= v.duration - 0.6) {
                        window.__warmupSeenNearEnd = true;
                    }
                    return false;
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private const string LikeActiveProbeJs = @"() => {
            const isRedFill = (value) => {
                const v = (value || '').toLowerCase();
                return v.includes('fe2c55') || v.includes('254, 44, 85') || v.includes('rgb(254, 44, 85)') || v.includes('#fe2c');
            };
            const isLikeNodeActive = (el) => {
                if (!el) return false;
                let node = el;
                for (let depth = 0; depth < 5 && node; depth++) {
                    const pressed = node.getAttribute('aria-pressed');
                    if (pressed === 'true') return true;
                    const label = (node.getAttribute('aria-label') || '').toLowerCase();
                    if (label.includes('unlike') || label.includes('bỏ thích')) return true;
                    node = node.parentElement;
                }
                const root = el.closest('button') || el;
                const svg = root.querySelector('svg');
                if (svg) {
                    const parts = svg.querySelectorAll('path, circle, *');
                    for (const part of parts) {
                        const fill = window.getComputedStyle(part).fill || '';
                        if (isRedFill(fill)) return true;
                    }
                    if (isRedFill(window.getComputedStyle(svg).fill || '')) return true;
                }
                const cls = (root.className || '') + ' ' + (root.parentElement && root.parentElement.className || '');
                return /liked|active/i.test(cls);
            };
            const sels = [
                'button:has([data-e2e=""browse-like-icon""])',
                'button:has([data-e2e=""like-icon""])',
                '[data-e2e=""browse-like-icon""]',
                '[data-e2e=""like-icon""]'
            ];
            for (const sel of sels) {
                const el = document.querySelector(sel);
                if (el && isLikeNodeActive(el)) return true;
            }
            return false;
        }";

        private async Task<bool> IsVideoLikedOnPageAsync()
        {
            try
            {
                return await _page.EvaluateAsync<bool>(LikeActiveProbeJs).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> WaitForLikeRegisteredAsync(CancellationToken cancellationToken, int maxWaitMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsVideoLikedOnPageAsync().ConfigureAwait(false))
                {
                    return true;
                }

                await Task.Delay(280, cancellationToken).ConfigureAwait(false);
            }

            return await IsVideoLikedOnPageAsync().ConfigureAwait(false);
        }

        private async Task<bool> ConfirmLikeStableAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            if (!await IsVideoLikedOnPageAsync().ConfigureAwait(false))
            {
                return false;
            }

            await Task.Delay(800, cancellationToken).ConfigureAwait(false);
            var atFirst = await IsVideoLikedOnPageAsync().ConfigureAwait(false);
            await Task.Delay(1300, cancellationToken).ConfigureAwait(false);
            var atSecond = await IsVideoLikedOnPageAsync().ConfigureAwait(false);

            if (atFirst && !atSecond)
            {
                logAction?.Invoke(
                    "[LIVE] Tim: WARN — tim đỏ rồi về đen (~2.5s sau click). Không bấm lại (tránh toggle).");
                return false;
            }

            if (!atSecond)
            {
                return false;
            }

            // Chờ TikTok/React render xong trước share/comment — tránh đen↔đỏ nhấp nháy do UI chuyển panel.
            logAction?.Invoke("[LIVE] Tim: chờ UI ổn định (~1.5s) trước bước tiếp...");
            await RandomDelayAsync(1200, 1800, cancellationToken).ConfigureAwait(false);
            var afterSettle = await IsVideoLikedOnPageAsync().ConfigureAwait(false);
            logAction?.Invoke("[LIVE] Tim: trạng thái sau settle=" + (afterSettle ? "đỏ (liked)" : "đen (chưa liked)") + ".");
            if (!afterSettle)
            {
                logAction?.Invoke(
                    "[LIVE] Tim: tim không giữ — UI về đen sau settle (server chưa lưu hoặc TikTok rollback).");
                return false;
            }

            return true;
        }

        private async Task<bool> TryLikeViaKeyboardAsync(CancellationToken cancellationToken)
        {
            try
            {
                await FocusBrowsePlayerAsync(cancellationToken).ConfigureAwait(false);
                await _page.Keyboard.PressAsync("l").ConfigureAwait(false);
                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string[]> GetLikeButtonSelectorsAsync()
        {
            try
            {
                var inForyou = await IsForyouFeedPageAsync().ConfigureAwait(false);
                if (inForyou)
                {
                    return new[]
                    {
                        "button:has([data-e2e=\"like-icon\"])",
                        "[data-e2e=\"like-icon\"]",
                        "button:has([data-e2e=\"browse-like-icon\"])",
                        "[data-e2e=\"browse-like-icon\"]"
                    };
                }
            }
            catch
            {
                // fallback below
            }

            return TikTokSelectors.LikeButtons;
        }

        private async Task<bool> TryDispatchLikePointerClickAsync()
        {
            try
            {
                return await _page.EvaluateAsync<bool>(@"() => {
                    const sels = [
                        'button:has([data-e2e=""browse-like-icon""])',
                        'button:has([data-e2e=""like-icon""])',
                        '[data-e2e=""browse-like-icon""]',
                        '[data-e2e=""like-icon""]'
                    ];
                    let target = null;
                    for (const sel of sels) {
                        const el = document.querySelector(sel);
                        if (el) {
                            target = el.closest('button') || el;
                            break;
                        }
                    }
                    if (!target) return false;
                    target.scrollIntoView({ block: 'center', inline: 'center' });
                    const rect = target.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, clientX: x, clientY: y, view: window, buttons: 1 };
                    target.dispatchEvent(new PointerEvent('pointerdown', opts));
                    target.dispatchEvent(new MouseEvent('mousedown', opts));
                    target.dispatchEvent(new PointerEvent('pointerup', opts));
                    target.dispatchEvent(new MouseEvent('mouseup', opts));
                    target.dispatchEvent(new MouseEvent('click', opts));
                    return true;
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private async Task<System.Collections.Generic.List<(ILocator Locator, string Label)>> BuildLikeClickTargetsAsync(
            ILocator primaryLocator,
            string primarySelector)
        {
            var targets = new System.Collections.Generic.List<(ILocator Locator, string Label)>();
            if (primaryLocator == null)
            {
                return targets;
            }

            var isButtonSelector = primarySelector.IndexOf("button:", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isButtonSelector)
            {
                targets.Add((primaryLocator, primarySelector));
                return targets;
            }

            try
            {
                var buttonAncestor = primaryLocator.Locator("xpath=ancestor::button[1]");
                if (await buttonAncestor.CountAsync().ConfigureAwait(false) > 0)
                {
                    var btn = buttonAncestor.First;
                    if (await IsVisibleAsync(btn, ShortQueryTimeout).ConfigureAwait(false))
                    {
                        targets.Add((btn, primarySelector + " → button ancestor"));
                        return targets;
                    }
                }
            }
            catch
            {
                // best-effort
            }

            targets.Add((primaryLocator, primarySelector));
            return targets;
        }

        private async Task<bool> TypeCommentLikeHumanAsync(ILocator inputLocator, string text, CancellationToken cancellationToken)
        {
            await TryDismissBlockingOverlaysAsync(cancellationToken).ConfigureAwait(false);
            await inputLocator.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);

            try
            {
                await HumanClickLocatorAsync(inputLocator, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await inputLocator.ClickAsync(new LocatorClickOptions { Timeout = 5000, Force = true }).ConfigureAwait(false);
            }

            await RandomDelayAsync(400, 900, cancellationToken).ConfigureAwait(false);

            // Xóa nội dung cũ nếu có
            try
            {
                await _page.Keyboard.PressAsync("Control+A").ConfigureAwait(false);
                await Task.Delay(_random.Next(60, 140), cancellationToken).ConfigureAwait(false);
                await _page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
                await Task.Delay(_random.Next(120, 280), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }

            // Ưu tiên gõ từng ký tự (delay ~120–280ms) như người thật — không dump cả chuỗi.
            try
            {
                await inputLocator.PressSequentiallyAsync(
                    text,
                    new LocatorPressSequentiallyOptions { Delay = _random.Next(120, 280) }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // Draft.js / contenteditable fallback
            }

            try
            {
                await inputLocator.FillAsync(text).ConfigureAwait(false);
                return true;
            }
            catch
            {
            }

            try
            {
                await _page.Keyboard.TypeAsync(text, new KeyboardTypeOptions { Delay = _random.Next(110, 260) }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // Chỉ dùng dump text khi keyboard hoàn toàn fail (không phải đường chính).
                try
                {
                    var ok = await inputLocator.EvaluateAsync<bool>(@"(el, value) => {
                        if (!el) return false;
                        el.focus();
                        el.textContent = value;
                        el.dispatchEvent(new InputEvent('input', { bubbles: true }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                        return ((el.innerText || el.textContent || '').trim().length > 0);
                    }", text).ConfigureAwait(false);
                    return ok;
                }
                catch
                {
                    return false;
                }
            }
        }

        private async Task<System.Collections.Generic.Dictionary<string, object>> ProbePageEngagementStateAsync()
        {
            try
            {
                var json = await _page.EvaluateAsync<string>(@"() => {
                    const v = document.querySelector('video');
                    const body = (document.body && document.body.innerText) ? document.body.innerText : '';
                    const playOverlay = !!document.querySelector('[data-e2e=""play-icon""], [data-e2e=""browse-play-icon""]');
                    const bodyLower = body.toLowerCase();
                    const shopGate = bodyLower.indexOf('tiktok shop') >= 0
                        || bodyLower.indexOf('xem video tiktok shop') >= 0
                        || bodyLower.indexOf('xem trong ứng dụng') >= 0
                        || bodyLower.indexOf('ứng dụng tiktok') >= 0;
                    const verifyGate = body.indexOf('Verify it') >= 0 || body.indexOf('Xác minh') >= 0;
                    return JSON.stringify({
                        url: location.href,
                        hasVideo: !!v,
                        videoPaused: v ? !!v.paused : null,
                        videoReadyState: v ? v.readyState : -1,
                        playOverlayVisible: playOverlay,
                        shopGateText: shopGate,
                        verifyGateText: verifyGate
                    });
                }").ConfigureAwait(false);
                return JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json)
                       ?? new System.Collections.Generic.Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                return new System.Collections.Generic.Dictionary<string, object> { ["probeError"] = ex.Message };
            }
        }

        private async Task<System.Collections.Generic.Dictionary<string, object>> ProbeLikeButtonStateAsync()
        {
            try
            {
                var json = await _page.EvaluateAsync<string>(@"() => {
                    const sels = [
                        'button:has([data-e2e=""browse-like-icon""])',
                        'button:has([data-e2e=""like-icon""])',
                        '[data-e2e=""browse-like-icon""]',
                        '[data-e2e=""like-icon""]',
                        'button[aria-label*=""Like""]',
                        'button[aria-label*=""Thích""]'
                    ];
                    const out = [];
                    for (const sel of sels) {
                        const el = document.querySelector(sel);
                        if (!el) continue;
                        const btn = el.closest('button') || el;
                        const aria = btn.getAttribute('aria-pressed') || el.getAttribute('aria-pressed') || '';
                        const label = btn.getAttribute('aria-label') || el.getAttribute('aria-label') || '';
                        const cls = btn.className || '';
                        const rect = btn.getBoundingClientRect();
                        out.push({
                            sel,
                            ariaPressed: aria,
                            ariaLabel: label,
                            classHasLiked: /liked|active/i.test(cls),
                            visible: rect.width > 0 && rect.height > 0
                        });
                    }
                    return JSON.stringify({ buttons: out });
                }").ConfigureAwait(false);
                return JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json)
                       ?? new System.Collections.Generic.Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                return new System.Collections.Generic.Dictionary<string, object> { ["probeError"] = ex.Message };
            }
        }

        private async Task<System.Collections.Generic.Dictionary<string, object>> ProbeCommentUiStateAsync()
        {
            try
            {
                var json = await _page.EvaluateAsync<string>(@"() => {
                    const inputs = document.querySelectorAll('div[contenteditable=""true""]');
                    const visibleInputs = [];
                    inputs.forEach(el => {
                        const r = el.getBoundingClientRect();
                        if (r.width > 0 && r.height > 0) {
                            visibleInputs.push({
                                e2e: el.getAttribute('data-e2e') || '',
                                textLen: (el.innerText || '').trim().length,
                                placeholder: el.getAttribute('placeholder') || el.getAttribute('aria-placeholder') || ''
                            });
                        }
                    });
                    const postBtn = document.querySelector('[data-e2e=""comment-post""]');
                    return JSON.stringify({
                        visibleInputCount: visibleInputs.length,
                        inputs: visibleInputs.slice(0, 3),
                        postBtnVisible: !!(postBtn && postBtn.offsetParent !== null)
                    });
                }").ConfigureAwait(false);
                return JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(json)
                       ?? new System.Collections.Generic.Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                return new System.Collections.Generic.Dictionary<string, object> { ["probeError"] = ex.Message };
            }
        }

        private async Task EnsureSearchResultsLoadedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _page.WaitForSelectorAsync("div[data-e2e='search_video-item'], " + TikTokSelectors.VideoLink, new PageWaitForSelectorOptions
                {
                    Timeout = 15000,
                    State = WaitForSelectorState.Attached
                }).ConfigureAwait(false);
            }
            catch
            {
                // Some pages use lazy load; best-effort below scroll loop will pick it up.
            }

            await RandomDelayAsync(600, 1400, cancellationToken).ConfigureAwait(false);
        }

        private async Task SafeGotoAsync(string url, CancellationToken cancellationToken, int timeoutMs = 45000)
        {
            EnsurePageReady();
            cancellationToken.ThrowIfCancellationRequested();

            async Task NavigateOnceAsync(WaitUntilState waitUntil)
            {
                var gotoTask = _page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout = timeoutMs,
                    WaitUntil = waitUntil
                });
                var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
                var completed = await Task.WhenAny(gotoTask, cancelTask).ConfigureAwait(false);
                if (completed == cancelTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await gotoTask.ConfigureAwait(false);
            }

            try
            {
                await NavigateOnceAsync(WaitUntilState.DOMContentLoaded).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientNavigationFailure(ex))
            {
                await NavigateOnceAsync(WaitUntilState.Commit).ConfigureAwait(false);
            }
        }

        /// <summary>Goto không ném nếu TikTok trả HTTP lỗi; trả về false khi thất bại hẳn.</summary>
        private async Task<bool> TryGotoSoftAsync(string url, CancellationToken cancellationToken, Action<string> logAction = null)
        {
            try
            {
                await SafeGotoAsync(url, cancellationToken, 35000).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientNavigationFailure(ex))
            {
                logAction?.Invoke("[Browser] Goto soft-fail " + url + ": " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Browser] Goto failed " + url + ": " + ex.Message);
                return false;
            }
        }

        private static bool IsTransientNavigationFailure(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            var msg = ex.Message ?? string.Empty;
            return msg.IndexOf("ERR_HTTP_RESPONSE_CODE_FAILURE", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ERR_CONNECTION", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ERR_TIMED_OUT", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("net::ERR_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsurePageReady()
        {
            if (_page == null)
            {
                throw new InvalidOperationException("Browser is not initialized. Call LaunchAsync first.");
            }
        }

        private static async Task<bool> IsVisibleAsync(ILocator locator, TimeSpan timeout)
        {
            try
            {
                var deadline = DateTime.UtcNow.Add(timeout);
                while (DateTime.UtcNow < deadline)
                {
                    if (await locator.IsVisibleAsync().ConfigureAwait(false))
                    {
                        return true;
                    }

                    await Task.Delay(120).ConfigureAwait(false);
                }

                return await locator.IsVisibleAsync().ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> IsLikeActiveAsync(ILocator locator)
        {
            try
            {
                return await locator.EvaluateAsync<bool>(@"el => {
                    if (!el) return false;
                    const isRedFill = (value) => {
                        const v = (value || '').toLowerCase();
                        return v.includes('fe2c55') || v.includes('254, 44, 85') || v.includes('rgb(254, 44, 85)') || v.includes('#fe2c');
                    };
                    let node = el;
                    for (let depth = 0; depth < 5 && node; depth++) {
                        const pressed = node.getAttribute('aria-pressed');
                        if (pressed === 'true') return true;
                        const label = (node.getAttribute('aria-label') || '').toLowerCase();
                        if (label.includes('unlike') || label.includes('bỏ thích')) return true;
                        node = node.parentElement;
                    }
                    const root = el.closest('button') || el;
                    const svg = root.querySelector('svg');
                    if (svg) {
                        const parts = svg.querySelectorAll('path, circle, *');
                        for (const part of parts) {
                            if (isRedFill(window.getComputedStyle(part).fill || '')) return true;
                        }
                        if (isRedFill(window.getComputedStyle(svg).fill || '')) return true;
                    }
                    const cls = (root.className || '') + ' ' + (root.parentElement && root.parentElement.className || '');
                    return /liked|active/i.test(cls);
                }").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private static (int lo, int hi) NormalizeRange(int min, int max, int hardMin, int hardMax)
        {
            var lo = Math.Max(hardMin, Math.Min(min, max));
            var hi = Math.Min(hardMax, Math.Max(min, max));
            if (hi < lo)
            {
                hi = lo;
            }

            return (lo, hi);
        }

        private static string BuildProxyServer(AutomationProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProxyHost))
            {
                return null;
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

        private static string ResolveChromeProfilePath(AutomationProfile profile, string activeProfileName)
        {
            if (!string.IsNullOrWhiteSpace(profile?.ChromeUserDataPath))
            {
                var configured = profile.ChromeUserDataPath.Trim();
                if (Path.IsPathRooted(configured))
                {
                    return configured;
                }

                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured));
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ProfileFolderName, activeProfileName);
        }

        // Helper PUBLIC — TikTok dùng chung warm-up + đăng nhập thủ công.
        public static string GetSharedProfilePath(AutomationProfile profile, string profileName)
        {
            return GetPlatformUserDataPath(profile, profileName, BrowserPlatform.TikTok);
        }

        /// <summary>user-data-dir riêng theo nền tảng — tránh cookie TikTok/FB/YT chồng chéo.</summary>
        public static string GetPlatformUserDataPath(
            AutomationProfile profile,
            string profileName,
            BrowserPlatform platform)
        {
            var sanitized = SanitizeProfileName(string.IsNullOrWhiteSpace(profileName) ? profile?.Name : profileName);
            if (platform == BrowserPlatform.TikTok)
            {
                return ResolveChromeProfilePath(profile, sanitized);
            }

            var suffix = platform == BrowserPlatform.Facebook ? "_facebook" : "_youtube";
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                ProfileFolderName,
                sanitized + suffix);
        }

        /// <summary>
        /// Tạo thư mục profile dùng chung với Playwright + migrate từ folder legacy nếu cần (trước Selenium login).
        /// </summary>
        public static void EnsureLegacySessionMigratedForSharedProfile(
            AutomationProfile profile,
            string profileName,
            Action<string> logAction)
        {
            var dir = GetSharedProfilePath(profile, profileName);
            if (string.IsNullOrWhiteSpace(dir))
            {
                logAction?.Invoke("[WarmupProfile] event=prepare_login_profile outcome=skip reason=empty_path");
                return;
            }

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[WarmupProfile] event=prepare_login_profile outcome=fail_create_dir err=" + ex.Message);
                return;
            }

            var sanitized = SanitizeProfileName(string.IsNullOrWhiteSpace(profileName) ? profile?.Name : profileName);
            var cookiesPath = ResolveProfileCookiesPath(dir);
            logAction?.Invoke(
                $"[WarmupProfile] event=prepare_login_profile path=\"{dir}\" sanitizedName=\"{sanitized}\" targetCookiesExists={!string.IsNullOrEmpty(cookiesPath)}");
            MigrateLegacyProfileFolderIfNeeded(dir, sanitized, profileName, logAction);
        }

        /// <summary>Chrome mới lưu cookie ở Default/Network/Cookies; bản cũ ở Default/Cookies.</summary>
        private static bool ProfileHasCookieStore(string profileDir)
        {
            return !string.IsNullOrEmpty(ResolveProfileCookiesPath(profileDir));
        }

        private static string ResolveProfileCookiesPath(string profileDir)
        {
            if (string.IsNullOrWhiteSpace(profileDir))
            {
                return null;
            }

            var network = Path.Combine(profileDir, "Default", "Network", "Cookies");
            if (File.Exists(network))
            {
                return network;
            }

            var legacy = Path.Combine(profileDir, "Default", "Cookies");
            return File.Exists(legacy) ? legacy : null;
        }

        // Migration: nếu folder mới CHƯA có session nhưng folder LEGACY có → copy để khỏi bắt login lại.
        // Gọi MỘT LẦN trước khi Playwright LaunchPersistentContextAsync(profileDir, ...).
        private static void MigrateLegacyProfileFolderIfNeeded(
            string targetDir,
            string activeProfileName,
            string rawProfileName,
            Action<string> logAction)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetDir)) return;

                var hadCookiesBefore = ProfileHasCookieStore(targetDir);
                logAction?.Invoke(
                    $"[WarmupProfile] event=migration_scan targetDir=\"{targetDir}\" hadCookiesBefore={hadCookiesBefore}");
                if (hadCookiesBefore)
                {
                    logAction?.Invoke("[WarmupProfile] event=migration_skip reason=target_already_has_cookies");
                    return; // đã có session, không cần migrate.
                }

                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                var sanitized = SanitizeProfileName(activeProfileName ?? rawProfileName);
                // Các folder legacy có thể chứa session do code Selenium cũ tạo ra.
                var legacyCandidates = new List<string>
                {
                    Path.Combine(baseDir, "Profiles", rawProfileName ?? string.Empty),
                    Path.Combine(baseDir, "Profiles", sanitized ?? string.Empty),
                    Path.Combine(baseDir, "browser_profile", rawProfileName ?? string.Empty),
                    Path.Combine(baseDir, "browser_profile", sanitized ?? string.Empty)
                };

                foreach (var src in legacyCandidates
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // chính nó
                    }
                    if (!Directory.Exists(src)) continue;
                    if (!ProfileHasCookieStore(src)) continue;

                    logAction?.Invoke(
                        $"[WarmupProfile] event=migration_start src=\"{src}\" dst=\"{targetDir}\"");
                    logAction?.Invoke($"[Browser] Migrate session từ folder cũ → mới:\r\n  src: {src}\r\n  dst: {targetDir}");
                    try
                    {
                        CopyDirectoryRecursiveBestEffort(src, targetDir, logAction);
                        logAction?.Invoke("[WarmupProfile] event=migration_done outcome=success");
                        logAction?.Invoke("[Browser] ✅ Đã migrate xong. Warm-up nên không bị bắt đăng nhập lại.");
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke("[WarmupProfile] event=migration_done outcome=failed err=" + ex.Message);
                        logAction?.Invoke("[Browser] ⚠️ Migrate thất bại: " + ex.Message);
                    }
                    return; // chỉ migrate từ nguồn đầu tiên thấy.
                }

                logAction?.Invoke("[WarmupProfile] event=migration_skip reason=no_legacy_source_with_cookies");
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[WarmupProfile] event=migration_error " + ex.Message);
                logAction?.Invoke("[Browser] ⚠️ Lỗi khi kiểm tra migrate session: " + ex.Message);
            }
        }

        /// <summary>Quét thư mục browser_profile: có Cookies hay không (không log nội dung cookie).</summary>
        public static List<(string folderName, bool hasCookies, DateTime? cookiesLastWriteUtc)> ScanBrowserProfileHealth()
        {
            var list = new List<(string, bool, DateTime?)>();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var root = Path.Combine(baseDir, ProfileFolderName);
            if (!Directory.Exists(root))
            {
                return list;
            }

            foreach (var dir in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(dir) ?? string.Empty;
                var cookies = Path.Combine(dir, "Default", "Cookies");
                if (!File.Exists(cookies))
                {
                    list.Add((name, false, null));
                    continue;
                }

                DateTime? lw = null;
                try
                {
                    lw = File.GetLastWriteTimeUtc(cookies);
                }
                catch
                {
                    lw = null;
                }

                list.Add((name, true, lw));
            }

            return list;
        }

        private static void CopyDirectoryRecursiveBestEffort(string src, string dst, Action<string> logAction)
        {
            Directory.CreateDirectory(dst);
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                var rel = dir.Substring(src.Length).TrimStart('\\', '/');
                Directory.CreateDirectory(Path.Combine(dst, rel));
            }
            int copied = 0, skipped = 0;
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(src.Length).TrimStart('\\', '/');
                var target = Path.Combine(dst, rel);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target) ?? dst);
                    File.Copy(file, target, overwrite: true);
                    copied++;
                }
                catch
                {
                    // SingletonLock / file đang lock — bỏ qua.
                    skipped++;
                }
            }
            logAction?.Invoke($"[Browser] Migrate stats: copied={copied}, skipped={skipped}.");
        }

        private async Task VerifyProxyIpAsync(AutomationProfile profile, CancellationToken cancellationToken, Action<string> logAction)
        {
            await _page.GotoAsync("https://api.ipify.org", new PageGotoOptions
            {
                Timeout = 30000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).ConfigureAwait(false);

            await RandomDelayAsync(400, 800, cancellationToken).ConfigureAwait(false);
            var actualIp = (await _page.InnerTextAsync("body").ConfigureAwait(false) ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(actualIp))
            {
                throw new InvalidOperationException("Could not read current IP from api.ipify.org.");
            }

            logAction?.Invoke($"[LIVE] Current public IP (ipify): {actualIp}");

            if (profile == null || string.IsNullOrWhiteSpace(profile.ProxyHost))
            {
                logAction?.Invoke("[LIVE] No proxy configured for this profile. IP logged before entering TikTok.");
                return;
            }

            var expectedIp = profile.ProxyHost.Trim();
            var candidates = new List<string>();
            if (IPAddress.TryParse(expectedIp, out _))
            {
                candidates.Add(expectedIp);
            }
            else
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(expectedIp).ConfigureAwait(false);
                    candidates.AddRange(addresses
                        .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => a.ToString()));
                }
                catch
                {
                    throw new InvalidOperationException($"[ERROR] Failed to resolve proxy host '{expectedIp}' for IP shield.");
                }
            }

            if (!candidates.Any(ip => string.Equals(ip, actualIp, StringComparison.OrdinalIgnoreCase)))
            {
                logAction?.Invoke($"[ERROR] IP Shield mismatch. Proxy host: {expectedIp}; Current IP: {actualIp}");
                logAction?.Invoke("[ERROR] " + IpLeakStopMessage);
                await CloseAsync().ConfigureAwait(false);
                throw new InvalidOperationException(IpLeakStopMessage);
            }

            logAction?.Invoke($"[LIVE] Proxy IP verified at ipify: {actualIp}");
        }

        private (int width, int height) ResolveViewportForProfile(AutomationProfile profile, string profileName)
        {
            if (profile != null && profile.ViewportWidth >= 800 && profile.ViewportHeight >= 600)
            {
                return (profile.ViewportWidth, profile.ViewportHeight);
            }

            var pool = new[]
            {
                (1280, 720),
                (1366, 768),
                (1440, 900),
                (1536, 864),
                (1600, 900)
            };

            var seed = BuildStableSeed(profileName);
            var selected = pool[seed % pool.Length];
            var widthJitter = (seed % 5) * 4; // 0..16
            var heightJitter = ((seed / 11) % 5) * 3; // 0..12
            return (selected.Item1 + widthJitter, selected.Item2 + heightJitter);
        }

        private static (string primary, string secondary) ResolveLanguagesForProfile(string profileName)
        {
            // Ưu tiên vi-VN để khớp Selenium login; vẫn jitter nhẹ theo profile.
            var pool = new[]
            {
                ("vi-VN", "vi"),
                ("vi-VN", "en"),
                ("en-US", "vi"),
                ("vi-VN", "vi")
            };

            var seed = BuildStableSeed(profileName);
            return pool[seed % pool.Length];
        }

        private static int ResolveHardwareConcurrencyForProfile(string profileName)
        {
            var pool = new[] { 4, 6, 8, 12, 16 };
            var seed = BuildStableSeed(profileName);
            return pool[(seed / 5) % pool.Length];
        }

        private static int BuildStableSeed(string profileName)
        {
            var key = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim().ToLowerInvariant();
            unchecked
            {
                var hash = 2166136261;
                for (var i = 0; i < key.Length; i++)
                {
                    hash ^= key[i];
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static string BuildPluginsScriptForProfile(string profileName)
        {
            var seed = BuildStableSeed(profileName);
            var pluginSetA = "[{name:'Chrome PDF Plugin',filename:'internal-pdf-viewer',description:'Portable Document Format'},{name:'Chrome PDF Viewer',filename:'mhjfbmdgcfjbbpaeojofohoefgiehjai',description:''},{name:'Native Client',filename:'internal-nacl-plugin',description:''}]";
            var pluginSetB = "[{name:'Chrome PDF Plugin',filename:'internal-pdf-viewer',description:'Portable Document Format'},{name:'Chrome PDF Viewer',filename:'mhjfbmdgcfjbbpaeojofohoefgiehjai',description:''},{name:'Native Client',filename:'internal-nacl-plugin',description:''},{name:'Widevine Content Decryption Module',filename:'widevinecdmadapter.dll',description:'Widevine Content Decryption Module'}]";
            var pluginSetC = "[{name:'Chrome PDF Plugin',filename:'internal-pdf-viewer',description:'Portable Document Format'},{name:'Chrome PDF Viewer',filename:'mhjfbmdgcfjbbpaeojofohoefgiehjai',description:''}]";
            var pluginSet = (seed % 3) == 0 ? pluginSetA : ((seed % 3) == 1 ? pluginSetB : pluginSetC);

            return @"(() => {
                        const fakePlugins = " + pluginSet + @";
                        Object.defineProperty(navigator, 'plugins', { get: () => fakePlugins });
                        Object.defineProperty(navigator, 'mimeTypes', { get: () => fakePlugins });
                    })();";
        }

        private static string[] ResolveFontsForProfile(string profileName)
        {
            var pool = new[]
            {
                new[] { "Arial", "Segoe UI", "Tahoma", "Verdana", "Times New Roman" },
                new[] { "Arial", "Calibri", "Segoe UI", "Cambria", "Trebuchet MS" },
                new[] { "Segoe UI", "Arial", "Consolas", "Georgia", "Tahoma" }
            };

            var seed = BuildStableSeed(profileName);
            return pool[seed % pool.Length];
        }

        private static (string vendor, string renderer) ResolveWebGlProfile(string profileName)
        {
            var pool = new[]
            {
                ("Intel Inc.", "Intel Iris OpenGL Engine"),
                ("Google Inc. (NVIDIA)", "ANGLE (NVIDIA GeForce GTX 1650 Direct3D11 vs_5_0 ps_5_0)"),
                ("Google Inc. (AMD)", "ANGLE (AMD Radeon RX 580 Direct3D11 vs_5_0 ps_5_0)")
            };
            var seed = BuildStableSeed(profileName);
            return pool[seed % pool.Length];
        }

        private static string BuildFontStealthScript(string[] fonts)
        {
            var safeFonts = fonts ?? new[] { "Arial", "Segoe UI", "Tahoma" };
            var quoted = string.Join(",", safeFonts.Select(x => "'" + x.Replace("'", "\\'") + "'"));
            return @"(() => {
                        const fakeFonts = [" + quoted + @"];
                        const originalCheck = document.fonts && document.fonts.check ? document.fonts.check.bind(document.fonts) : null;
                        if (document.fonts && document.fonts.check) {
                            document.fonts.check = function(font) {
                                if (!font) return false;
                                return fakeFonts.some(f => font.indexOf(f) >= 0) || (originalCheck ? originalCheck(font) : false);
                            };
                        }
                        Object.defineProperty(navigator, 'fonts', { get: () => fakeFonts });
                    })();";
        }

        private static string BuildWebGlStealthScript(string vendor, string renderer)
        {
            var safeVendor = (vendor ?? "Intel Inc.").Replace("'", "\\'");
            var safeRenderer = (renderer ?? "Intel Iris OpenGL Engine").Replace("'", "\\'");
            return @"(() => {
                        const getParameter = WebGLRenderingContext.prototype.getParameter;
                        WebGLRenderingContext.prototype.getParameter = function(parameter){
                            if (parameter === 37445) return '" + safeVendor + @"';
                            if (parameter === 37446) return '" + safeRenderer + @"';
                            return getParameter.call(this, parameter);
                        };
                        if (window.WebGL2RenderingContext && WebGL2RenderingContext.prototype) {
                            const getParameter2 = WebGL2RenderingContext.prototype.getParameter;
                            WebGL2RenderingContext.prototype.getParameter = function(parameter){
                                if (parameter === 37445) return '" + safeVendor + @"';
                                if (parameter === 37446) return '" + safeRenderer + @"';
                                return getParameter2.call(this, parameter);
                            };
                        }
                    })();";
        }

        private static AutomationProfile ResolveProfileFromSettings(AppSettings settings, string runningProfileName)
        {
            var profiles = settings?.Profiles;
            if (profiles == null || profiles.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(runningProfileName))
            {
                for (var i = 0; i < profiles.Count; i++)
                {
                    var profile = profiles[i];
                    if (profile != null &&
                        string.Equals(profile.Name, runningProfileName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return profile;
                    }
                }
            }

            return profiles[0];
        }

        private static string SanitizeProfileName(string name)
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

        private static class TikTokSelectors
        {
            // Search results
            public const string VideoLink = "a[href*=\"/video/\"]";

            // Login state
            public static readonly string[] LoggedInIndicators = new[]
            {
                "[data-e2e=\"profile-icon\"]",
                "[data-e2e=\"nav-activity\"]",
                "[data-e2e=\"nav-menu\"]",
                "[data-e2e=\"upload-icon\"]",
                "a[href*=\"/profile\"]"
            };

            public static readonly string[] LoggedOutIndicators = new[]
            {
                "[data-e2e=\"top-login-button\"]",
                "button:has-text(\"Log in\")",
                "button:has-text(\"Đăng nhập\")",
                "a[href*=\"/login\"]:has-text(\"Log in\")"
            };

            // Ưu tiên button cha — icon SVG thường không nhận Playwright click trực tiếp.
            public static readonly string[] LikeButtons = new[]
            {
                "button:has([data-e2e=\"browse-like-icon\"])",
                "button:has([data-e2e=\"like-icon\"])",
                "[data-e2e=\"browse-like-icon\"]",
                "[data-e2e=\"like-icon\"]"
            };

            public static readonly string[] VideoCaptions = new[]
            {
                "[data-e2e=\"browse-video-desc\"]",
                "[data-e2e=\"video-desc\"]",
                "[data-e2e=\"video-description\"]",
                "[data-e2e=\"browse-video-title\"]",
                "h1[data-e2e=\"video-title\"]"
            };

            public static readonly string[] BrowseNextVideoButtons = new[]
            {
                "[data-e2e=\"browse-video-switcher-next\"]",
                "[data-e2e=\"video-switcher-next\"]",
                "[data-e2e=\"arrow-right\"]",
                "button[aria-label*=\"Next video\"]",
                "button[aria-label*=\"Video tiếp\"]",
                "button[aria-label*=\"video tiếp\"]",
                "button[aria-label*=\"Next\"]",
                "button[aria-label*=\"Tiếp theo\"]"
            };

            public static readonly string[] BrowseCommentInputs = new[]
            {
                "[data-e2e='browse-comment-list'] div[contenteditable='true']",
                "[data-e2e='comment-input'] textarea",
                "[data-e2e='comment-input'] div[contenteditable='true']",
                "div[class*='CommentInput'] div[contenteditable='true']",
                "[data-e2e='browse-comment-list'] ~ div div[contenteditable='true']",
                "div[contenteditable='true'][data-e2e='comment-input']",
                "div[contenteditable='true'][data-e2e='comment-text']"
            };

            public static readonly string[] CommentPanelButtons = new[]
            {
                "[data-e2e='comment-icon']",
                "[data-e2e='browse-comment-icon']",
                "button[aria-label*='Comment']",
                "button[aria-label*='Bình luận']"
            };

            public static readonly string[] CommentsTabs = new[]
            {
                "[data-e2e='browse-comment']:has-text('Bình luận')",
                "[role='tab']:has-text('Bình luận')",
                "div[class*='Tab']:has-text('Bình luận')",
                "span:has-text('Bình luận')",
                "p:has-text('Bình luận')",
                "[role='tab']:has-text('Comments')",
                "div[class*='Tab']:has-text('Comments')"
            };

            // Comment input
            public static readonly string[] CommentInputs = new[]
            {
                "div[data-e2e='comment-input'] textarea",
                "div[contenteditable=\"true\"][data-e2e=\"comment-input\"]",
                "div[contenteditable=\"true\"][data-e2e=\"comment-text\"]",
                "[data-e2e='comment-input'] div[contenteditable=\"true\"]",
                "div.public-DraftEditor-content[contenteditable=\"true\"]",
                "div[role='textbox']",
                "div[contenteditable=\"true\"]"
            };

            public static readonly string[] CommentPlaceholderClicks = new[]
            {
                "[data-e2e='comment-input']",
                "div[class*='CommentInput']",
                "p[class*='CommentInput']",
                "span:has-text(\"Thêm bình luận\")",
                "span:has-text(\"Add comment\")"
            };

            // Submit comment
            public static readonly string[] CommentSubmit = new[]
            {
                "[data-e2e=\"comment-post\"]",
                "button:has-text(\"Đăng\")",
                "div[role='button']:has-text(\"Đăng\")",
                "button:has-text(\"Post\")",
                "div[role='button']:has-text(\"Post\")"
            };

            // Captcha / challenge indicators
            public static readonly string[] ChallengeIndicators = new[]
            {
                "iframe[src*=\"captcha\"]",
                "div#captcha-verify-container",
                "div[id*=\"captcha\"]"
            };

            public static readonly string[] CheckpointIndicators = new[]
            {
                "div[data-e2e*=\"suspend\"]",
                "div[class*=\"suspend\"]",
                "div[class*=\"account-status\"]",
                "text=Account suspended",
                "text=Account status",
                "text=Your account was suspended"
            };
        }
    }

    internal static class DebugAgentLog
    {
        private const string SessionId = "4e0696";
        private static readonly object Sync = new object();
        private static readonly string[] LogPaths = BuildLogPaths();

        private static string[] BuildLogPaths()
        {
            var list = new List<string>();
            try
            {
                var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrWhiteSpace(asmDir))
                {
                    list.Add(Path.Combine(asmDir, "debug-4e0696.log"));
                }
            }
            catch
            {
            }

            list.AddRange(new[]
            {
                Path.Combine(Path.GetTempPath(), "debug-4e0696.log"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug-4e0696.log"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "tiktok_Omni",
                    "debug-4e0696.log")
            });

            try
            {
                var docs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "tiktok_Omni",
                    "debug-4e0696.log");
                list.Add(docs);
            }
            catch
            {
            }

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static void Write(string hypothesisId, string location, string message, object data = null, string runId = "pre-fix")
        {
            // #region agent log
            try
            {
                object safeData = null;
                if (data != null)
                {
                    safeData = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(data));
                }

                var payload = new
                {
                    sessionId = SessionId,
                    runId,
                    hypothesisId,
                    location,
                    message,
                    data = safeData,
                    timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds
                };
                var line = JsonConvert.SerializeObject(payload);
                lock (Sync)
                {
                    foreach (var path in LogPaths)
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.AppendAllText(path, line + Environment.NewLine);
                    }

                    var mirrorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warmup-live.log");
                    File.AppendAllText(
                        mirrorPath,
                        $"[{DateTime.Now:HH:mm:ss}] {hypothesisId} {location} {message}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var err = JsonConvert.SerializeObject(new
                    {
                        sessionId = SessionId,
                        runId,
                        hypothesisId,
                        location,
                        message = "log_write_failed",
                        data = new { originalMessage = message, ex.Message },
                        timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds
                    });
                    lock (Sync)
                    {
                        File.AppendAllText(LogPaths[0], err + Environment.NewLine);
                    }
                }
                catch
                {
                }
            }
            // #endregion
        }
    }
}
