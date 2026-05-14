using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    public class BrowserAutomation
    {
        private const string ProfileFolderName = "browser_profile";
        private const string TikTokHomeUrl = "https://www.tiktok.com/";
        private const string TikTokLoginUrl = "https://www.tiktok.com/login";
        private const string TikTokForyouUrl = "https://www.tiktok.com/foryou";
        private const string IpLeakStopMessage = "Lộ IP thật, đã dừng để bảo vệ nick!";
        private static readonly TimeSpan ShortQueryTimeout = TimeSpan.FromSeconds(5);

        private readonly Random _random = new Random();

        private IPlaywright _playwright;
        private IBrowserContext _context;
        private IPage _page;
        private string _activeProfileName;
        private AutomationProfile _activeProfile;

        public IPage Page => _page;

        public async Task LaunchAsync(
            CancellationToken cancellationToken,
            Action<string> logAction,
            string runningProfileName = null,
            AutomationProfile profile = null,
            bool headless = false)
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
                var profileDir = ResolveChromeProfilePath(_activeProfile, _activeProfileName);
                Directory.CreateDirectory(profileDir);
                logAction?.Invoke($"Launching profile '{_activeProfileName}' with user data path: {profileDir}");

                // Nếu thư mục mới CHƯA có session (do user từng login bằng nút Selenium-login cũ ở folder khác),
                // copy toàn bộ data từ folder cũ qua để khỏi bắt đăng nhập lại.
                MigrateLegacyProfileFolderIfNeeded(profileDir, _activeProfileName, rawProfileName, logAction);
                if (!string.IsNullOrWhiteSpace(_activeProfile?.UserAgent))
                {
                    logAction?.Invoke($"Profile '{_activeProfileName}' using fixed User-Agent.");
                }
                else
                {
                    logAction?.Invoke($"Profile '{_activeProfileName}' has no custom User-Agent. Browser default will be used.");
                }

                _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
                var viewport = ResolveViewportForProfile(_activeProfile, _activeProfileName);
                var languages = ResolveLanguagesForProfile(_activeProfileName);
                var fontSet = ResolveFontsForProfile(_activeProfileName);
                var webGl = ResolveWebGlProfile(_activeProfileName);
                var hardwareConcurrency = ResolveHardwareConcurrencyForProfile(_activeProfileName);
                var browserArgs = new List<string>
                {
                    "--disable-blink-features=AutomationControlled",
                    "--disable-infobars"
                };
                var launchOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = headless,
                    // Playwright defaults to false → passes --no-sandbox (yellow bar; TikTok login often breaks).
                    ChromiumSandbox = true,
                    ViewportSize = new ViewportSize { Width = viewport.width, Height = viewport.height },
                    Locale = "en-US",
                    UserAgent = string.IsNullOrWhiteSpace(_activeProfile?.UserAgent) ? null : _activeProfile.UserAgent,
                    // Drops the "controlled by automated test" switch so TikTok is more likely to finish login.
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

                // Prefer real Chrome / Edge; bundled Chromium often never receives TikTok QR/session.
                var channelAttempts = new[]
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

                await _context.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });").ConfigureAwait(false);
                await _context.AddInitScriptAsync($"Object.defineProperty(navigator, 'languages', {{ get: () => ['{languages.primary}','{languages.secondary}'] }});").ConfigureAwait(false);
                await _context.AddInitScriptAsync($"Object.defineProperty(navigator, 'hardwareConcurrency', {{ get: () => {hardwareConcurrency} }});").ConfigureAwait(false);
                await _context.AddInitScriptAsync(BuildFontStealthScript(fontSet)).ConfigureAwait(false);
                await _context.AddInitScriptAsync(BuildWebGlStealthScript(webGl.vendor, webGl.renderer)).ConfigureAwait(false);
                await _context.AddInitScriptAsync(BuildPluginsScriptForProfile(_activeProfileName)).ConfigureAwait(false);
                await _context.AddInitScriptAsync("Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });").ConfigureAwait(false);
                logAction?.Invoke($"Stealth enabled. Viewport: {viewport.width}x{viewport.height}. Languages: {languages.primary}, {languages.secondary}. Fonts: {fontSet.Length}. WebGL: {webGl.vendor}. HW threads: {hardwareConcurrency}");

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

            if (!forceOpenLoginPage && await IsLoggedInAsync(cancellationToken).ConfigureAwait(false))
            {
                logAction?.Invoke("TikTok session detected. Already logged in.");
                return;
            }

            if (!forceOpenLoginPage)
            {
                logAction?.Invoke("Đang mở trang đăng nhập TikTok (QR / Google / email — chọn trên web).");
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
                logAction?.Invoke("[LOGIN] Thử mở For You để kiểm tra phiên (hữu ích sau khi quét QR trên điện thoại)...");
                await SafeGotoAsync(TikTokForyouUrl, cancellationToken).ConfigureAwait(false);
                await Task.Delay(2500, cancellationToken).ConfigureAwait(false);
                SyncToPrimaryTikTokPage(logAction);

                if (await IsLoggedInAsync(cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                logAction?.Invoke("[LOGIN] Chưa có phiên — quay lại trang đăng nhập.");
                await SafeGotoAsync(TikTokLoginUrl, cancellationToken).ConfigureAwait(false);
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

        private async Task TryClickShoppingTabAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            EnsurePageReady();

            const string clickJs = @"() => {
                const want = ['shop','shopping','mua','sản phẩm'];
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
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Shop] Không click được tab Shopping: " + ex.Message);
            }
        }

        public async Task OpenVideoAsync(int oneBasedIndex, CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            if (oneBasedIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
            }

            await EnsureSearchResultsLoadedAsync(cancellationToken).ConfigureAwait(false);

            var locator = _page.Locator(TikTokSelectors.VideoLink);
            var count = await locator.CountAsync().ConfigureAwait(false);

            while (count < oneBasedIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _page.EvaluateAsync("window.scrollBy(0, window.innerHeight);").ConfigureAwait(false);
                await RandomDelayAsync(800, 1500, cancellationToken).ConfigureAwait(false);

                var newCount = await locator.CountAsync().ConfigureAwait(false);
                if (newCount == count)
                {
                    break;
                }
                count = newCount;
            }

            if (count == 0)
            {
                throw new InvalidOperationException("No video results found on TikTok search page.");
            }

            var safeIndex = Math.Min(oneBasedIndex, count) - 1;
            logAction?.Invoke($"[LIVE] Opening video #{safeIndex + 1}...");
            var target = locator.Nth(safeIndex);
            await target.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
            await RandomDelayAsync(400, 900, cancellationToken).ConfigureAwait(false);
            await target.ClickAsync(new LocatorClickOptions { Timeout = 15000 }).ConfigureAwait(false);
            await RandomDelayAsync(2000, 3500, cancellationToken).ConfigureAwait(false);

            if (await DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new CaptchaDetectedException();
            }

            if (await DetectCheckpointAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new CheckpointDetectedException("TikTok checkpoint detected while opening video.");
            }
        }

        public async Task WatchAsync(int minSeconds, int maxSeconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (lo, hi) = NormalizeRange(minSeconds, maxSeconds, 5, 30);
            var seconds = _random.Next(lo, hi + 1);

            // Tiny scroll/mouse-move during watch to look more human.
            var endAt = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < endAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(_random.Next(1200, 2200), cancellationToken).ConfigureAwait(false);
                try
                {
                    await _page.Mouse.MoveAsync(_random.Next(200, 1000), _random.Next(200, 700)).ConfigureAwait(false);
                }
                catch
                {
                    // Mouse moves are best-effort.
                }
            }
        }

        public async Task<bool> LikeCurrentVideoAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            foreach (var selector in TikTokSelectors.LikeButtons)
            {
                var locator = _page.Locator(selector).First;
                if (await IsVisibleAsync(locator, ShortQueryTimeout).ConfigureAwait(false))
                {
                    var alreadyLiked = await IsLikeActiveAsync(locator).ConfigureAwait(false);
                    if (alreadyLiked)
                    {
                        logAction?.Invoke("[LIVE] Already liked. Skipping.");
                        return false;
                    }

                    await RandomDelayAsync(400, 1200, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await locator.ClickAsync(new LocatorClickOptions { Timeout = 8000 }).ConfigureAwait(false);
                        logAction?.Invoke("[LIVE] Liked the video.");
                        return true;
                    }
                    catch
                    {
                        // try next selector
                    }
                }
            }

            logAction?.Invoke("[LIVE] Like button not found. Skipping like.");
            return false;
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

            ILocator inputLocator = null;
            foreach (var selector in TikTokSelectors.CommentInputs)
            {
                var loc = _page.Locator(selector).First;
                if (await IsVisibleAsync(loc, ShortQueryTimeout).ConfigureAwait(false))
                {
                    inputLocator = loc;
                    break;
                }
            }

            if (inputLocator == null)
            {
                logAction?.Invoke("[LIVE] Comment input not found. Skipping.");
                return false;
            }

            try
            {
                await inputLocator.ClickAsync(new LocatorClickOptions { Timeout = 8000 }).ConfigureAwait(false);
                await RandomDelayAsync(400, 900, cancellationToken).ConfigureAwait(false);

                foreach (var ch in text)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Human-like typing: send each key with random cadence.
                    await _page.Keyboard.TypeAsync(ch.ToString(), new KeyboardTypeOptions { Delay = _random.Next(50, 201) }).ConfigureAwait(false);
                }

                await RandomDelayAsync(500, 1100, cancellationToken).ConfigureAwait(false);

                var posted = false;
                foreach (var selector in TikTokSelectors.CommentSubmit)
                {
                    var btn = _page.Locator(selector).First;
                    if (await IsVisibleAsync(btn, ShortQueryTimeout).ConfigureAwait(false))
                    {
                        try
                        {
                            await btn.ClickAsync(new LocatorClickOptions { Timeout = 5000 }).ConfigureAwait(false);
                            posted = true;
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
                }

                await RandomDelayAsync(800, 1600, cancellationToken).ConfigureAwait(false);
                logAction?.Invoke("[LIVE] Comment submitted.");
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LIVE] Failed to submit comment: " + ex.Message);
                return false;
            }
        }

        public async Task OpenCommentsPanelBrieflyAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

            var commentButtonSelectors = new[]
            {
                "[data-e2e='comment-icon']",
                "[data-e2e='browse-comment-icon']",
                "button[aria-label*='Comment']"
            };

            foreach (var selector in commentButtonSelectors)
            {
                var button = _page.Locator(selector).First;
                if (!await IsVisibleAsync(button, TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                {
                    continue;
                }

                try
                {
                    await button.ClickAsync(new LocatorClickOptions { Timeout = 5000 }).ConfigureAwait(false);
                    await RandomDelayAsync(900, 2200, cancellationToken).ConfigureAwait(false);
                    await _page.Keyboard.PressAsync("Escape").ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] Opened comments briefly before like.");
                    return;
                }
                catch
                {
                    // try next selector
                }
            }

            logAction?.Invoke("[LIVE] Comment panel not found for random pre-like action.");
        }

        public async Task SimulateReadingScrollAsync(CancellationToken cancellationToken, Action<string> logAction)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePageReady();

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
                _page = null;
                _context = null;
                _playwright = null;
                _activeProfileName = null;
                _activeProfile = null;
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
                await SafeGotoAsync(TikTokHomeUrl, cancellationToken).ConfigureAwait(false);
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

                var onLoginPath = url.IndexOf("/login", StringComparison.OrdinalIgnoreCase) >= 0;

                if (await TryEmbeddedTikTokUserStateAsync(page).ConfigureAwait(false))
                {
                    return true;
                }

                if (onLoginPath && await HasLikelyTikTokSessionCookiesAsync().ConfigureAwait(false))
                {
                    return true;
                }

                if (url.IndexOf("/login", StringComparison.OrdinalIgnoreCase) < 0 &&
                    url.IndexOf("/signup", StringComparison.OrdinalIgnoreCase) < 0 &&
                    await HasLikelyTikTokSessionCookiesAsync().ConfigureAwait(false))
                {
                    foreach (var selector in TikTokSelectors.LoggedInIndicators)
                    {
                        var loc = page.Locator(selector).First;
                        if (await IsVisibleAsync(loc, ShortQueryTimeout).ConfigureAwait(false))
                        {
                            return true;
                        }
                    }

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

                var onAuthPath = onLoginPath || url.IndexOf("/signup", StringComparison.OrdinalIgnoreCase) >= 0;
                if (onAuthPath)
                {
                    foreach (var selector in TikTokSelectors.LoggedOutIndicators)
                    {
                        var loc = page.Locator(selector).First;
                        if (await IsVisibleAsync(loc, ShortQueryTimeout).ConfigureAwait(false))
                        {
                            return false;
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

                    await SafeGotoAsync(TikTokHomeUrl, cancellationToken).ConfigureAwait(false);
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

        private async Task EnsureSearchResultsLoadedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _page.WaitForSelectorAsync(TikTokSelectors.VideoLink, new PageWaitForSelectorOptions
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

        private async Task SafeGotoAsync(string url, CancellationToken cancellationToken)
        {
            EnsurePageReady();
            cancellationToken.ThrowIfCancellationRequested();
            await _page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = 60000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).ConfigureAwait(false);
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
                var aria = await locator.GetAttributeAsync("aria-pressed").ConfigureAwait(false);
                if (string.Equals(aria, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
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

        // Helper PUBLIC để Form1/Selenium login dùng CHUNG đường dẫn với Playwright warm-up.
        // ⇒ Đảm bảo session đăng nhập 1 nơi sẽ tái sử dụng được ở mọi nơi.
        public static string GetSharedProfilePath(AutomationProfile profile, string profileName)
        {
            var sanitized = SanitizeProfileName(string.IsNullOrWhiteSpace(profileName) ? profile?.Name : profileName);
            return ResolveChromeProfilePath(profile, sanitized);
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
            var cookiesPath = Path.Combine(dir, "Default", "Cookies");
            logAction?.Invoke(
                $"[WarmupProfile] event=prepare_login_profile path=\"{dir}\" sanitizedName=\"{sanitized}\" targetCookiesExists={File.Exists(cookiesPath)}");
            MigrateLegacyProfileFolderIfNeeded(dir, sanitized, profileName, logAction);
        }

        // Migration: nếu folder mới chưa có "Default/Cookies" (chưa có session) nhưng
        // folder LEGACY (vd. <basedir>/Profiles/<name>) có → copy toàn bộ qua để khỏi bắt login lại.
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

                var targetCookies = Path.Combine(targetDir, "Default", "Cookies");
                var hadCookiesBefore = File.Exists(targetCookies);
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
                    var srcCookies = Path.Combine(src, "Default", "Cookies");
                    if (!File.Exists(srcCookies)) continue;

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
            var pool = new[]
            {
                ("en-US", "en"),
                ("vi-VN", "vi"),
                ("en-GB", "en"),
                ("en-US", "vi")
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

            // Like button
            public static readonly string[] LikeButtons = new[]
            {
                "[data-e2e=\"browse-like-icon\"]",
                "[data-e2e=\"like-icon\"]",
                "button[aria-label*=\"Like\"]"
            };

            // Comment input
            public static readonly string[] CommentInputs = new[]
            {
                "div[contenteditable=\"true\"][data-e2e=\"comment-input\"]",
                "div[contenteditable=\"true\"]"
            };

            // Submit comment
            public static readonly string[] CommentSubmit = new[]
            {
                "[data-e2e=\"comment-post\"]",
                "button:has-text(\"Post\")"
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
}
