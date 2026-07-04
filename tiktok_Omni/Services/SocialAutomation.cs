using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Tự động hóa đa nền tảng (TikTok / Facebook / YouTube) — user-data-dir tách theo ProfileName + nền tảng.
    /// </summary>
    public class SocialAutomation
    {
        private readonly TikTokAutomation _tikTokAutomation;
        private readonly ConfigManager _configManager;

        public SocialAutomation(TikTokAutomation tikTokAutomation, ConfigManager configManager)
        {
            _tikTokAutomation = tikTokAutomation ?? throw new ArgumentNullException(nameof(tikTokAutomation));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        }

        public Task<bool> LoginFacebookAsync(
            string profileName,
            AutomationProfile profile,
            CancellationToken cancellationToken,
            Action<string> logAction) =>
            RunPlatformManualLoginAsync(
                profileName,
                profile,
                BrowserPlatform.Facebook,
                "https://www.facebook.com/login",
                IsFacebookLoggedIn,
                (p, loggedIn) => p.IsFBLoggedIn = loggedIn,
                cancellationToken,
                logAction);

        public Task<bool> LoginYouTubeAsync(
            string profileName,
            AutomationProfile profile,
            CancellationToken cancellationToken,
            Action<string> logAction) =>
            RunPlatformManualLoginAsync(
                profileName,
                profile,
                BrowserPlatform.YouTube,
                "https://accounts.google.com/ServiceLogin?continue=https://studio.youtube.com/",
                IsYouTubeLoggedIn,
                (p, loggedIn) => p.IsYTLoggedIn = loggedIn,
                cancellationToken,
                logAction);

        public Task<string> RunOmnichannelAutoPostAsync(
            AutoPostJobPayload payload,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return BrowserLock.WithLockAsync(
                ProfileScopedPaths.ResolveProfileName(payload.Profile),
                ct => RunOmnichannelAutoPostCoreAsync(payload, ct, logAction),
                cancellationToken);
        }

        private async Task<string> RunOmnichannelAutoPostCoreAsync(
            AutoPostJobPayload payload,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var channels = new System.Collections.Generic.List<string>();
            if (payload.PostTikTok)
            {
                channels.Add("TikTok");
            }

            if (payload.PostFacebook)
            {
                channels.Add("Facebook");
            }

            if (payload.PostYouTube)
            {
                channels.Add("YouTube");
            }

            logAction?.Invoke(
                "Auto Post đa kênh: profile «" + payload.Profile + "» → " + string.Join(" → ", channels) + ".");

            var reuseBrowser = false;
            var moreAfterTikTok = payload.PostFacebook || payload.PostYouTube;
            var moreAfterFacebook = payload.PostYouTube;

            if (payload.PostTikTok)
            {
                logAction?.Invoke("Auto Post [1]: TikTok…");
                var affiliateLink = OmnichannelAutoPostFields.NormalizeLink(payload.AffiliateLink);
                var productId = OmnichannelAutoPostFields.NormalizeLink(payload.ProductId);
                string attachLink = null;
                string attachProductId = null;
                if (!string.IsNullOrWhiteSpace(affiliateLink))
                {
                    attachLink = affiliateLink;
                    attachProductId = string.IsNullOrWhiteSpace(productId) ? null : productId;
                    logAction?.Invoke(
                        "Smart Posting: có Affiliate link → Upload → Caption/Hashtag → [Thêm SP] → dán link → xác nhận → Post.");
                }
                else
                {
                    logAction?.Invoke(
                        "Smart Posting: video thuần (AffiliateLink rỗng) → Upload → Caption/Hashtag → Post.");
                }

                await _tikTokAutomation.AutoPostTikTokAsync(
                    payload.VideoFolder,
                    payload.TikTokHashtags,
                    payload.Profile,
                    cancellationToken,
                    logAction,
                    payload.VideoFilePath,
                    payload.TikTokCaption,
                    clickPublish: !payload.TikTokUploadOnly,
                    reuseExistingAutoPostBrowser: reuseBrowser,
                    keepBrowserOpenAfter: moreAfterTikTok,
                    affiliateLink: attachLink,
                    productId: attachProductId,
                    skipFolderScopeCheck: payload.SkipFolderScopeCheck).ConfigureAwait(false);
                reuseBrowser = moreAfterTikTok;
            }

            if (payload.PostFacebook)
            {
                logAction?.Invoke("Auto Post [2]: Facebook Reels…");
                if (OmnichannelAutoPostFields.ShouldAttachFacebookShopeeLink(
                        payload.FacebookAttachShopeeLink,
                        payload.FacebookShopeeLink))
                {
                    logAction?.Invoke(
                        "Facebook: có link Shopee → thử gắn trên Reels hoặc chèn vào caption.");
                }

                await _tikTokAutomation.AutoPostFacebookAsync(
                    payload.VideoFolder,
                    payload.FacebookHashtags,
                    payload.Profile,
                    cancellationToken,
                    logAction,
                    payload.VideoFilePath,
                    payload.FacebookCaption,
                    clickPublish: true,
                    reuseExistingAutoPostBrowser: reuseBrowser || payload.PostTikTok,
                    keepBrowserOpenAfter: moreAfterFacebook,
                    attachShopeeLink: payload.FacebookAttachShopeeLink,
                    facebookShopeeLink: payload.FacebookShopeeLink,
                    skipFolderScopeCheck: payload.SkipFolderScopeCheck).ConfigureAwait(false);
                reuseBrowser = moreAfterFacebook;
            }

            if (payload.PostYouTube)
            {
                logAction?.Invoke("Auto Post [3]: YouTube Shorts…");
                await _tikTokAutomation.AutoPostYouTubeShortsAsync(
                    payload.VideoFolder,
                    payload.YouTubeTitle,
                    payload.YouTubeDescription,
                    payload.Profile,
                    cancellationToken,
                    logAction,
                    payload.VideoFilePath,
                    clickPublish: true,
                    reuseExistingAutoPostBrowser: reuseBrowser || payload.PostTikTok || payload.PostFacebook,
                    keepBrowserOpenAfter: false,
                    skipFolderScopeCheck: payload.SkipFolderScopeCheck).ConfigureAwait(false);
            }

            await _tikTokAutomation.CloseAutoPostBrowserAsync(logAction).ConfigureAwait(false);
            return "Đã chạy xong trên: " + string.Join(", ", channels) + ".";
        }

        public Task CloseAutoPostBrowserAsync(Action<string> logAction) =>
            _tikTokAutomation.CloseAutoPostBrowserAsync(logAction);

        /// <summary>
        /// Mở một Chrome duy nhất (user-data-dir theo ProfileName), 3 tab TikTok / Facebook / YouTube Studio để đăng nhập thủ công.
        /// Sau khi đóng trình duyệt, quét Cookies trên đĩa và cập nhật trạng thái 3 nền tảng.
        /// </summary>
        public Task<ProfileSessionProbe.LoginProbeResult> LoginAllSocialAsync(
            string profileName,
            AutomationProfile profile,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Profile name is required.", nameof(profileName));
            }

            return BrowserLock.WithLockAsync(
                ProfileScopedPaths.ResolveProfileName(profileName),
                ct => LoginAllSocialCoreAsync(profileName, profile, ct, logAction),
                cancellationToken);
        }

        private async Task<ProfileSessionProbe.LoginProbeResult> LoginAllSocialCoreAsync(
            string profileName,
            AutomationProfile profile,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var effectiveName = profileName.Trim();
            var userDataDir = ProfileSessionProbe.ResolveUserDataDir(profile, effectiveName);
            Directory.CreateDirectory(userDataDir);
            BrowserAutomation.EnsureLegacySessionMigratedForSharedProfile(profile, effectiveName, logAction);
            logAction?.Invoke("[LOGIN/ALL] user-data-dir (chung 3 nền tảng): " + userDataDir);

            var proxyArg = BuildProxyServerArgument(profile);

            await Task.Run(
                    () =>
                    {
                        SeleniumChromeLaunchHelper.TryClearStaleProfileLocks(userDataDir, logAction);
                        var options = BuildChromeOptions(userDataDir, proxyArg);
                        var service = ChromeDriverService.CreateDefaultService();
                        SeleniumChromeLaunchHelper.GuardProfileLaunch(userDataDir, logAction);
                        using (var driver = SeleniumChromeLaunchHelper.CreateDriver(
                            service,
                            options,
                            logAction))
                        {
                            SeleniumChromeLaunchHelper.ConfigureLoginDriverTimeouts(driver);
                            driver.Navigate().GoToUrl("https://www.tiktok.com/");
                            logAction?.Invoke("[LOGIN/ALL] Tab 1: TikTok — đăng nhập nếu cần.");

                            Thread.Sleep(1500);
                            var js = (IJavaScriptExecutor)driver;
                            object fbOpenResult = null;
                            try
                            {
                                fbOpenResult = js.ExecuteScript("return window.open('https://www.facebook.com/','_blank');");
                            }
                            catch (Exception)
                            {
                            }

                            Thread.Sleep(800);
                            object ytOpenResult = null;
                            try
                            {
                                ytOpenResult = js.ExecuteScript("return window.open('https://studio.youtube.com/','_blank');");
                            }
                            catch (Exception)
                            {
                            }

                            logAction?.Invoke("[LOGIN/ALL] Tab 2: Facebook | Tab 3: YouTube Studio — đăng nhập từng tab.");
                            logAction?.Invoke("[LOGIN/ALL] Đóng cửa sổ Chrome khi xong — cookie sẽ lưu vào profile.");

                            var fbNameCaptured = false;
                            var ytNameCaptured = false;
                            var lastStatusLog = DateTime.UtcNow;
                            while (true)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                try
                                {
                                    _ = driver.Title;
                                    if ((DateTime.UtcNow - lastStatusLog).TotalSeconds >= 8)
                                    {
                                        lastStatusLog = DateTime.UtcNow;
                                        LogLiveLoginHints(driver, logAction);
                                    }

                                    if (profile != null)
                                    {
                                        if (!fbNameCaptured && IsFacebookLoggedIn(driver))
                                        {
                                            fbNameCaptured = TryCaptureFacebookDisplayName(driver, profile, logAction);
                                        }

                                        if (!ytNameCaptured && IsYouTubeLoggedIn(driver))
                                        {
                                            ytNameCaptured = TryCaptureYouTubeDisplayName(driver, profile, logAction);
                                        }
                                    }

                                    Thread.Sleep(2000);
                                }
                                catch
                                {
                                    break;
                                }
                            }
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(1500);
            var probe = ProfileSessionProbe.ProbeFromDisk(profile, effectiveName);
            if (profile != null)
            {
                ProfileSessionProbe.ApplyToProfile(profile, probe);
            }

            await _configManager
                .UpdateProfileSocialLoginStatusAsync(
                    effectiveName,
                    probe.TikTokLoggedIn,
                    probe.FacebookLoggedIn,
                    probe.YouTubeLoggedIn,
                    logAction)
                .ConfigureAwait(false);

            logAction?.Invoke(
                $"[LOGIN/ALL] Kết quả quét cookie — TT: {(probe.TikTokLoggedIn ? "Đã login" : "Chưa")}, " +
                $"FB: {(probe.FacebookLoggedIn ? "Đã login" : "Chưa")}, " +
                $"YT: {(probe.YouTubeLoggedIn ? "Đã login" : "Chưa")}" +
                (probe.HasCookieDatabase ? string.Empty : " (chưa có file Cookies)"));

            return probe;
        }

        /// <summary>Quét lại trạng thái đăng nhập từ đĩa (không mở trình duyệt).</summary>
        public async Task<ProfileSessionProbe.LoginProbeResult> RefreshLoginStatusFromDiskAsync(
            string profileName,
            AutomationProfile profile,
            Action<string> logAction = null)
        {
            var probe = ProfileSessionProbe.ProbeFromDisk(profile, profileName);
            if (profile != null)
            {
                ProfileSessionProbe.ApplyToProfile(profile, probe);
            }

            await _configManager
                .UpdateProfileSocialLoginStatusAsync(
                    profileName,
                    probe.TikTokLoggedIn,
                    probe.FacebookLoggedIn,
                    probe.YouTubeLoggedIn,
                    logAction)
                .ConfigureAwait(false);

            return probe;
        }

        private static void LogLiveLoginHints(IWebDriver driver, Action<string> logAction)
        {
            if (driver == null)
            {
                return;
            }

            var tt = SeleniumHasTikTokSession(driver);
            var fb = IsFacebookLoggedIn(driver);
            var yt = IsYouTubeLoggedIn(driver);
            logAction?.Invoke($"[LOGIN/ALL] Trực tiếp — TT:{(tt ? "✓" : "—")} FB:{(fb ? "✓" : "—")} YT:{(yt ? "✓" : "—")}");
        }

        private static bool SeleniumHasTikTokSession(IWebDriver driver)
        {
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

                    if (string.Equals(c.Name, "sessionid", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(c.Value))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static ChromeOptions BuildChromeOptions(string userDataDir, string proxyServer)
        {
            return UndetectedChromeOptionsBuilder.Build(userDataDir, proxyServer);
        }

        /// <summary>Cào báo cáo Affiliate TikTok bằng Chrome undetected-style (profile TikTok).</summary>
        public Task<IReadOnlyList<AffiliateRevenueItem>> ScrapeAffiliateRevenueReportAsync(
            string profileName,
            AutomationProfile profile,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Profile name is required.", nameof(profileName));
            }

            return BrowserLock.WithLockAsync(
                ProfileScopedPaths.ResolveProfileName(profileName),
                ct => ScrapeAffiliateRevenueReportCoreAsync(profileName, profile, ct, logAction),
                cancellationToken);
        }

        private async Task<IReadOnlyList<AffiliateRevenueItem>> ScrapeAffiliateRevenueReportCoreAsync(
            string profileName,
            AutomationProfile profile,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var effectiveName = profileName.Trim();
            var userDataDir = BrowserAutomation.GetPlatformUserDataPath(profile, effectiveName, BrowserPlatform.TikTok);
            Directory.CreateDirectory(userDataDir);
            var proxyArg = BuildProxyServerArgument(profile);
            logAction?.Invoke("[Revenue] Đang mở Chrome Undetected cho profile «" + effectiveName + "»…");

            return await Task.Run(
                () => ScrapeAffiliateRevenueOnWorkerThread(userDataDir, proxyArg, logAction, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        private static List<AffiliateRevenueItem> ScrapeAffiliateRevenueOnWorkerThread(
            string userDataDir,
            string proxyArg,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SeleniumChromeLaunchHelper.TryClearStaleProfileLocks(userDataDir, logAction);
                var options = UndetectedChromeOptionsBuilder.Build(userDataDir, proxyArg);
                var service = ChromeDriverService.CreateDefaultService();
                SeleniumChromeLaunchHelper.GuardProfileLaunch(userDataDir, logAction);
                using (var driver = SeleniumChromeLaunchHelper.CreateDriver(
                    service,
                    options,
                    logAction))
                {
                    UndetectedChromeOptionsBuilder.ApplyStealthScripts(driver);

                    NavigateAffiliateWithThrottle(driver, "https://affiliate.tiktok.com/home", logAction, cancellationToken);
                    AffiliateRevenueScrapeGuard.HumanDelayBetweenActions();

                    if (!TryEnsureAffiliateSessionReady(driver, logAction))
                    {
                        throw new InvalidOperationException(
                            "Affiliate Center: chưa đăng nhập hoặc gặp Captcha — cần đăng nhập TikTok trên profile này.");
                    }

                    logAction?.Invoke("[Revenue] Đang mở trang báo cáo Affiliate…");
                    NavigateAffiliateWithThrottle(
                        driver,
                        "https://affiliate.tiktok.com/portal/report/overview",
                        logAction,
                        cancellationToken);
                    AffiliateRevenueScrapeGuard.HumanDelayBetweenActions();

                    logAction?.Invoke("[Revenue] Đang bóc tách số liệu từ bảng báo cáo…");
                    var items = ParseAffiliateRevenueFromPage(driver, logAction, cancellationToken);
                    if (items.Count == 0)
                    {
                        logAction?.Invoke("[Revenue] Không đọc được dòng trong bảng — thử trang home.");
                        NavigateAffiliateWithThrottle(driver, "https://affiliate.tiktok.com/home", logAction, cancellationToken);
                        AffiliateRevenueScrapeGuard.HumanDelayBetweenActions();
                        items = ParseAffiliateRevenueFromPage(driver, logAction, cancellationToken);
                    }

                    logAction?.Invoke("[Revenue] Đã bóc tách " + items.Count + " bản ghi.");
                    return items;
                }
            }
            catch (WebDriverTimeoutException)
            {
                throw;
            }
            catch (NoSuchElementException)
            {
                throw;
            }
        }

        private static void NavigateAffiliateWithThrottle(
            IWebDriver driver,
            string url,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AffiliateRevenueScrapeGuard.WaitForRateLimitSlotAsync(logAction, cancellationToken)
                .GetAwaiter()
                .GetResult();
            logAction?.Invoke("[Revenue] Điều hướng: " + url);
            driver.Navigate().GoToUrl(url);
            AffiliateRevenueScrapeGuard.MarkRequestCompleted();
            AffiliateRevenueScrapeGuard.HumanDelayBetweenActions();
        }

        private static bool TryEnsureAffiliateSessionReady(IWebDriver driver, Action<string> logAction)
        {
            if (PageHasCaptchaOrChallenge(driver))
            {
                logAction?.Invoke("[Revenue] Phát hiện Captcha / xác minh — dừng cào.");
                return false;
            }

            if (!SeleniumHasTikTokSession(driver))
            {
                logAction?.Invoke("[Revenue] Chưa có session TikTok — đăng nhập Affiliate Center trước.");
                return false;
            }

            return true;
        }

        private static bool PageHasCaptchaOrChallenge(IWebDriver driver)
        {
            try
            {
                var url = (driver.Url ?? string.Empty).ToLowerInvariant();
                var body = (driver.PageSource ?? string.Empty).ToLowerInvariant();
                if (body.Contains("captcha") || body.Contains("verify you are human") || body.Contains("security check"))
                {
                    return true;
                }

                if (url.Contains("/login") || body.Contains("log in to tiktok") || body.Contains("đăng nhập"))
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static List<AffiliateRevenueItem> ParseAffiliateRevenueFromPage(
            IWebDriver driver,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var list = new List<AffiliateRevenueItem>();
            try
            {
                var js = (IJavaScriptExecutor)driver;
                var raw = js.ExecuteScript(@"
var rows = [];
var trs = document.querySelectorAll('table tr, [role=""row""]');
for (var i = 0; i < trs.length; i++) {
  var cells = trs[i].querySelectorAll('td, th, [role=""cell""], div');
  if (!cells || cells.length < 2) continue;
  var texts = [];
  for (var j = 0; j < cells.length; j++) {
    var t = (cells[j].innerText || '').trim();
    if (t) texts.push(t);
  }
  if (texts.length >= 2) rows.push(texts.join('|'));
}
return rows;") as System.Collections.ObjectModel.ReadOnlyCollection<object>;

                if (raw != null)
                {
                    foreach (var lineObj in raw)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var line = (lineObj ?? string.Empty).ToString();
                        if (TryParseRevenueRow(line, out var item))
                        {
                            list.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[Revenue] JS parse: " + ex.Message);
            }

            if (list.Count == 0)
            {
                list.AddRange(ParseRevenueFromPageText(driver.PageSource));
            }

            return list
                .GroupBy(x => x.Date.Date)
                .Select(g =>
                {
                    var first = g.First();
                    return new AffiliateRevenueItem
                    {
                        Date = g.Key,
                        OrderCount = g.Sum(x => x.OrderCount),
                        Revenue = g.Sum(x => x.Revenue),
                        Commission = g.Sum(x => x.Commission),
                        Status = first.Status
                    };
                })
                .OrderByDescending(x => x.Date)
                .ToList();
        }

        private static bool TryParseRevenueRow(string pipeLine, out AffiliateRevenueItem item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(pipeLine))
            {
                return false;
            }

            var parts = pipeLine.Split('|');
            DateTime? date = null;
            double revenue = 0;
            double commission = 0;
            int orders = 0;
            var status = "OK";

            foreach (var part in parts)
            {
                var t = part.Trim();
                if (string.IsNullOrWhiteSpace(t))
                {
                    continue;
                }

                if (!date.HasValue && TryParseFlexibleDate(t, out var d))
                {
                    date = d;
                    continue;
                }

                if (orders == 0 && Regex.IsMatch(t, @"^\d+$"))
                {
                    orders = int.Parse(t, CultureInfo.InvariantCulture);
                    continue;
                }

                if (TryParseMoney(t, out var money))
                {
                    if (commission <= 0)
                    {
                        commission = money;
                    }
                    else if (revenue <= 0)
                    {
                        revenue = money;
                    }
                }
            }

            if (!date.HasValue)
            {
                return false;
            }

            item = new AffiliateRevenueItem
            {
                Date = date.Value.Date,
                OrderCount = orders,
                Revenue = revenue,
                Commission = commission > 0 ? commission : revenue,
                Status = status
            };
            return true;
        }

        private static IEnumerable<AffiliateRevenueItem> ParseRevenueFromPageText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                yield break;
            }

            var text = Regex.Replace(html, "<[^>]+>", " ");
            var dateMatches = Regex.Matches(
                text,
                @"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})");
            foreach (Match m in dateMatches)
            {
                if (!TryParseFlexibleDate(m.Groups[1].Value, out var d))
                {
                    continue;
                }

                yield return new AffiliateRevenueItem
                {
                    Date = d.Date,
                    OrderCount = 0,
                    Revenue = 0,
                    Commission = 0,
                    Status = "Parsed"
                };
            }
        }

        private static bool TryParseFlexibleDate(string text, out DateTime date)
        {
            var formats = new[]
            {
                "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy"
            };
            if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            return DateTime.TryParse(text, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out date)
                   || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static bool TryParseMoney(string text, out double value)
        {
            value = 0;
            var t = (text ?? string.Empty).Trim().ToLowerInvariant()
                .Replace("đ", string.Empty)
                .Replace("vnd", string.Empty)
                .Replace("₫", string.Empty)
                .Replace(",", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty);
            if (string.IsNullOrWhiteSpace(t))
            {
                return false;
            }

            if (t.EndsWith("k"))
            {
                t = t.Substring(0, t.Length - 1);
                if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var k))
                {
                    value = k * 1000d;
                    return true;
                }
            }

            return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private Task<bool> RunPlatformManualLoginAsync(
            string profileName,
            AutomationProfile profile,
            BrowserPlatform platform,
            string loginUrl,
            Func<IWebDriver, bool> isLoggedIn,
            Action<AutomationProfile, bool> setFlag,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Profile name is required.", nameof(profileName));
            }

            return BrowserLock.WithLockAsync(
                ProfileScopedPaths.ResolveProfileName(profileName),
                ct => RunPlatformManualLoginCoreAsync(
                    profileName,
                    profile,
                    platform,
                    loginUrl,
                    isLoggedIn,
                    setFlag,
                    ct,
                    logAction),
                cancellationToken);
        }

        private async Task<bool> RunPlatformManualLoginCoreAsync(
            string profileName,
            AutomationProfile profile,
            BrowserPlatform platform,
            string loginUrl,
            Func<IWebDriver, bool> isLoggedIn,
            Action<AutomationProfile, bool> setFlag,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var effectiveName = profileName.Trim();
            var userDataDir = BrowserAutomation.GetPlatformUserDataPath(profile, effectiveName, platform);
            Directory.CreateDirectory(userDataDir);
            logAction?.Invoke($"[LOGIN/{platform}] user-data-dir: {userDataDir}");

            var proxyArg = BuildProxyServerArgument(profile);
            var loggedIn = false;

            await Task.Run(
                    () =>
                    {
                        SeleniumChromeLaunchHelper.TryClearStaleProfileLocks(userDataDir, logAction);
                        var options = BuildChromeOptions(userDataDir, proxyArg);
                        var service = ChromeDriverService.CreateDefaultService();
                        SeleniumChromeLaunchHelper.GuardProfileLaunch(userDataDir, logAction);
                        using (var driver = SeleniumChromeLaunchHelper.CreateDriver(
                            service,
                            options,
                            logAction))
                        {
                            driver.Navigate().GoToUrl(loginUrl);
                            logAction?.Invoke(
                                $"[LOGIN/{platform}] Đăng nhập thủ công trong Chrome — đóng khi xong.");

                            var waitUntil = DateTime.UtcNow.AddMinutes(20);
                            while (DateTime.UtcNow < waitUntil)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                try
                                {
                                    _ = driver.Title;
                                    if (isLoggedIn(driver))
                                    {
                                        loggedIn = true;
                                        if (profile != null)
                                        {
                                            if (platform == BrowserPlatform.Facebook)
                                            {
                                                TryCaptureFacebookDisplayName(driver, profile, logAction);
                                            }
                                            else if (platform == BrowserPlatform.YouTube)
                                            {
                                                TryCaptureYouTubeDisplayName(driver, profile, logAction);
                                            }
                                        }

                                        break;
                                    }

                                    Thread.Sleep(2000);
                                }
                                catch
                                {
                                    break;
                                }
                            }
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (profile != null)
            {
                setFlag(profile, loggedIn);
            }

            await _configManager
                .UpdateProfilePlatformLoginFlagsAsync(effectiveName, platform, loggedIn, logAction)
                .ConfigureAwait(false);

            return loggedIn;
        }

        private const string FacebookDisplayNameJs = @"
(function() {
  try {
    var h1 = document.querySelector('h1');
    if (h1 && h1.innerText && h1.innerText.trim()) return h1.innerText.trim();
    var meta = document.querySelector('meta[property=""og:title""]');
    if (meta && meta.content) {
      var t = meta.content.trim();
      var pipe = t.indexOf(' | Facebook');
      if (pipe > 0) t = t.substring(0, pipe).trim();
      if (t && t.toLowerCase() !== 'facebook') return t;
    }
    return '';
  } catch (e) { return ''; }
})();";

        private const string YouTubeDisplayNameJs = @"
(function() {
  try {
    var selectors = [
      '#entity-name',
      '#channel-title',
      'yt-formatted-string#channel-name',
      '#channel-name'
    ];
    for (var i = 0; i < selectors.length; i++) {
      var el = document.querySelector(selectors[i]);
      if (el && el.innerText && el.innerText.trim()) return el.innerText.trim();
    }
    var meta = document.querySelector('meta[property=""og:title""]');
    if (meta && meta.content) {
      var t = meta.content.trim();
      if (t.indexOf(' - YouTube') > 0) return t.replace(' - YouTube', '').trim();
      if (t.toLowerCase() !== 'youtube') return t;
    }
    return '';
  } catch (e) { return ''; }
})();";

        private static bool TryCaptureFacebookDisplayName(
            IWebDriver driver,
            AutomationProfile profile,
            Action<string> logAction)
        {
            if (driver == null || profile == null || !string.IsNullOrWhiteSpace(profile.FacebookName))
            {
                return !string.IsNullOrWhiteSpace(profile?.FacebookName);
            }

            try
            {
                SwitchToWindowContainingUrl(driver, "facebook.com");
                driver.Navigate().GoToUrl("https://www.facebook.com/me");
                Thread.Sleep(2500);
                var js = (IJavaScriptExecutor)driver;
                var name = SanitizePlatformDisplayName(js.ExecuteScript(FacebookDisplayNameJs)?.ToString());
                if (string.IsNullOrWhiteSpace(name))
                {
                    return false;
                }

                profile.FacebookName = name;
                logAction?.Invoke("[LOGIN/FB] Tên hiển thị: " + name);
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LOGIN/FB] Không lấy được tên: " + ex.Message);
                return false;
            }
        }

        private static bool TryCaptureYouTubeDisplayName(
            IWebDriver driver,
            AutomationProfile profile,
            Action<string> logAction)
        {
            if (driver == null || profile == null || !string.IsNullOrWhiteSpace(profile.YouTubeName))
            {
                return !string.IsNullOrWhiteSpace(profile?.YouTubeName);
            }

            try
            {
                SwitchToWindowContainingUrl(driver, "youtube.com");
                driver.Navigate().GoToUrl("https://studio.youtube.com/");
                Thread.Sleep(3000);
                var js = (IJavaScriptExecutor)driver;
                var name = SanitizePlatformDisplayName(js.ExecuteScript(YouTubeDisplayNameJs)?.ToString());
                if (string.IsNullOrWhiteSpace(name))
                {
                    return false;
                }

                profile.YouTubeName = name;
                logAction?.Invoke("[LOGIN/YT] Tên kênh: " + name);
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LOGIN/YT] Không lấy được tên: " + ex.Message);
                return false;
            }
        }

        private static void SwitchToWindowContainingUrl(IWebDriver driver, string urlPart)
        {
            if (driver == null || string.IsNullOrWhiteSpace(urlPart))
            {
                return;
            }

            foreach (var handle in driver.WindowHandles)
            {
                try
                {
                    driver.SwitchTo().Window(handle);
                    var url = driver.Url ?? string.Empty;
                    if (url.IndexOf(urlPart, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return;
                    }
                }
                catch
                {
                    // thử tab kế
                }
            }
        }

        private static string SanitizePlatformDisplayName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var name = raw.Trim();
            if (name.Length > 120)
            {
                name = name.Substring(0, 120).Trim();
            }

            return name;
        }

        private static bool IsFacebookLoggedIn(IWebDriver driver)
        {
            try
            {
                var cUser = driver.Manage().Cookies.GetCookieNamed("c_user");
                return cUser != null && !string.IsNullOrWhiteSpace(cUser.Value);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsYouTubeLoggedIn(IWebDriver driver)
        {
            try
            {
                foreach (var name in new[] { "SID", "SSID", "SAPISID", "__Secure-1PSID" })
                {
                    var cookie = driver.Manage().Cookies.GetCookieNamed(name);
                    if (cookie != null && !string.IsNullOrWhiteSpace(cookie.Value))
                    {
                        return true;
                    }
                }

                var url = driver.Url ?? string.Empty;
                return url.IndexOf("studio.youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 &&
                       url.IndexOf("accounts.google.com/signin", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildProxyServerArgument(AutomationProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProxyHost) || profile.ProxyPort <= 0)
            {
                return string.Empty;
            }

            return profile.ProxyHost.Trim() + ":" + profile.ProxyPort;
        }

    }
}
