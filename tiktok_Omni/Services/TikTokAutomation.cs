using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace tiktok_Omni.Services
{
    public class TikTokAutomation
    {
        private readonly GeminiService _geminiService;
        private readonly ConfigManager _configManager;
        private readonly CaptchaService _captchaService;
        private readonly Random _random = new Random();
        private BrowserAutomation _autoPostBrowser;
        private const int MaxCaptchaSolveAttempts = 3;

        public event EventHandler<string> CaptchaDetected;
        public event EventHandler<CheckpointAlert> CheckpointDetected;

        public TikTokAutomation(
            GeminiService geminiService,
            ConfigManager configManager,
            CaptchaService captchaService = null)
        {
            _geminiService = geminiService;
            _configManager = configManager;
            _captchaService = captchaService ?? new CaptchaService();
        }

        public async Task StartWarmupAsync(
            string keywords,
            int videoCount,
            bool autoComment,
            bool dryRun,
            int startIndex,
            int watchSecondsMin,
            int watchSecondsMax,
            CancellationToken cancellationToken,
            Action<string> logAction,
            Action<int, int> progressAction,
            string runningProfileName = null)
        {
            if (videoCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(videoCount), "Video count must be greater than 0.");
            }

            logAction?.Invoke($"Loading settings for warm-up with keywords: {keywords}");
            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var safeStartIndex = Math.Max(0, Math.Min(startIndex, videoCount));

            if (videoCount > 50)
            {
                logAction?.Invoke("[WARN] High volume can trigger TikTok anti-bot. Consider lowering videoCount.");
            }

            if (!dryRun)
            {
                var browser = new BrowserAutomation();
                try
                {
                    var selectedProfile = ResolveRunningProfile(settings, runningProfileName, logAction);
                    selectedProfile = await _configManager
                        .EnsureProfileFingerprintAsync(settings, selectedProfile?.Name, logAction)
                        .ConfigureAwait(false);
                    var effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                        ? (selectedProfile?.Name ?? "default")
                        : runningProfileName.Trim();

                    await browser.LaunchAsync(
                        cancellationToken,
                        logAction,
                        effectiveProfileName,
                        selectedProfile).ConfigureAwait(false);
                    await browser.EnsureLoggedInAsync(cancellationToken, logAction).ConfigureAwait(false);
                    await browser.GotoSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);

                    for (var i = safeStartIndex + 1; i <= videoCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progressAction?.Invoke(i - 1, videoCount);

                        logAction?.Invoke($"[LIVE] Slot {i}/{videoCount}: warm-up step starting.");

                        try
                        {
                            await browser.OpenVideoAsync(i, cancellationToken, logAction).ConfigureAwait(false);

                            var watchRange = BuildHumanLikeWatchRange(watchSecondsMin, watchSecondsMax);
                            logAction?.Invoke($"[LIVE] Human-watch mode: {watchRange.mode} ({watchRange.min}s..{watchRange.max}s).");
                            await browser.WatchAsync(watchRange.min, watchRange.max, cancellationToken).ConfigureAwait(false);
                            await browser.SimulateReadingScrollAsync(cancellationToken, logAction).ConfigureAwait(false);

                            if (_random.NextDouble() <= 0.15d)
                            {
                                await browser.OpenCommentsPanelBrieflyAsync(cancellationToken, logAction).ConfigureAwait(false);
                            }

                            await browser.LikeCurrentVideoAsync(cancellationToken, logAction).ConfigureAwait(false);

                            if (autoComment)
                            {
                                if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                                {
                                    logAction?.Invoke("[LIVE] Auto comment enabled but AI API key is missing. Skipping comment.");
                                }
                                else
                                {
                                    var prompt = BuildCommentPrompt(keywords, settings.CommentStyle);
                                    var generated = await _geminiService.GenerateScriptAsync(
                                        prompt,
                                        settings.AiProvider,
                                        settings.AiApiKey,
                                        settings.AiModel,
                                        cancellationToken).ConfigureAwait(false);

                                    var commentText = SanitizeComment(generated);
                                    if (!string.IsNullOrWhiteSpace(commentText))
                                    {
                                        await browser.CommentCurrentVideoAsync(commentText, cancellationToken, logAction).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        logAction?.Invoke("[LIVE] AI returned empty text. Skipping comment.");
                                    }
                                }
                            }
                        }
                        catch (CheckpointDetectedException)
                        {
                            var notifyText = "Phát hiện CHECKPOINT khi đang chạy warm-up. Hệ thống sẽ dừng profile.";
                            await RaiseCheckpointAlertAsync(browser, effectiveProfileName, notifyText, logAction, cancellationToken).ConfigureAwait(false);
                            logAction?.Invoke("[LIVE] " + notifyText);
                            throw new OperationCanceledException("Checkpoint detected.");
                        }
                        catch (CaptchaDetectedException)
                        {
                            var solved = await HandleCaptchaAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
                            if (!solved)
                            {
                                logAction?.Invoke("[LIVE] CAPTCHA solve failed too many times. Stopping this profile.");
                                throw new OperationCanceledException();
                            }

                            i--;
                            continue;
                        }

                        progressAction?.Invoke(i, videoCount);

                        if (i < videoCount)
                        {
                            try
                            {
                                await browser.RandomDelayAsync(1500, 3500, cancellationToken).ConfigureAwait(false);
                                await browser.GotoSearchAsync(keywords, cancellationToken, logAction).ConfigureAwait(false);
                            }
                            catch (CheckpointDetectedException)
                            {
                                var notifyText = "Phát hiện CHECKPOINT khi đang điều hướng. Hệ thống sẽ dừng profile.";
                                await RaiseCheckpointAlertAsync(browser, effectiveProfileName, notifyText, logAction, cancellationToken).ConfigureAwait(false);
                                logAction?.Invoke("[LIVE] " + notifyText);
                                throw new OperationCanceledException("Checkpoint detected.");
                            }
                            catch (CaptchaDetectedException)
                            {
                                var solved = await HandleCaptchaAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
                                if (!solved)
                                {
                                    logAction?.Invoke("[LIVE] CAPTCHA solve failed too many times. Stopping this profile.");
                                    throw new OperationCanceledException();
                                }

                                i--;
                            }
                        }
                    }

                    logAction?.Invoke("LIVE warm-up completed.");
                    return;
                }
                catch (CheckpointDetectedException)
                {
                    var notifyText = "Phát hiện CHECKPOINT! Nick có thể bị giới hạn, cần xử lý ngay.";
                    await RaiseCheckpointAlertAsync(browser, runningProfileName, notifyText, logAction, cancellationToken).ConfigureAwait(false);
                    logAction?.Invoke("[LIVE] " + notifyText);
                    throw new OperationCanceledException("Checkpoint detected.");
                }
                finally
                {
                    await browser.CloseAsync().ConfigureAwait(false);
                }
            }

            for (var i = safeStartIndex + 1; i <= videoCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progressAction?.Invoke(i - 1, videoCount);

                logAction?.Invoke($"[DRY] Interacting with video {i}/{videoCount}...");
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);

                logAction?.Invoke($"[DRY] Simulated scroll/like/watch on video {i}.");

                if (!autoComment)
                {
                    progressAction?.Invoke(i, videoCount);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    logAction?.Invoke("Auto comment enabled but AI API key is missing. Skipping comment generation.");
                    progressAction?.Invoke(i, videoCount);
                    continue;
                }

                var dryPrompt = BuildCommentPrompt(keywords, settings.CommentStyle);
                var generatedDry = await _geminiService.GenerateScriptAsync(
                    dryPrompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    cancellationToken).ConfigureAwait(false);

                logAction?.Invoke($"[DRY] Generated AI comment preview: {SanitizeComment(generatedDry)}");
                progressAction?.Invoke(i, videoCount);
            }

            logAction?.Invoke("DRY warm-up simulation completed.");
        }

        /// <summary>
        /// Opens Playwright Chrome for the selected profile and waits until TikTok login succeeds (QR or password in that window).
        /// There is no login field inside the desktop app — authentication happens only in this browser window.
        /// </summary>
        public async Task OpenTikTokLoginBrowserAsync(
            string runningProfileName,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var profile = ResolveRunningProfile(settings, runningProfileName, logAction);
            profile = await _configManager
                .EnsureProfileFingerprintAsync(settings, profile?.Name, logAction)
                .ConfigureAwait(false);
            var effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                ? (profile?.Name ?? "default")
                : runningProfileName.Trim();

            logAction?.Invoke($"[LOGIN] Profile '{effectiveProfileName}' — mở tiktok.com/login; đăng nhập xong app sẽ lưu @nick vào bảng profile.");

            var browser = new BrowserAutomation();
            try
            {
                await browser.LaunchAsync(cancellationToken, logAction, effectiveProfileName, profile).ConfigureAwait(false);
                logAction?.Invoke("[LOGIN] Trình duyệt đã chạy — kiểm tra taskbar nếu không thấy cửa sổ.");

                const int maxCaptchaRounds = 8;
                for (var round = 1; round <= maxCaptchaRounds; round++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        await browser.EnsureLoggedInAsync(
                            cancellationToken,
                            logAction,
                            forceOpenLoginPage: true).ConfigureAwait(false);
                        logAction?.Invoke("[LOGIN] Đã xác nhận đăng nhập TikTok.");
                        var snapshot = await browser.TryExtractTikTokAccountSnapshotAsync(cancellationToken, logAction).ConfigureAwait(false);
                        if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.UniqueId))
                        {
                            await _configManager
                                .UpdateAutomationProfileTikTokAsync(effectiveProfileName, snapshot, logAction)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            logAction?.Invoke("[LOGIN] Không đọc được @nick tự động — bạn có thể nhập tay ở Cài đặt.");
                        }

                        return;
                    }
                    catch (CaptchaDetectedException)
                    {
                        logAction?.Invoke($"[LOGIN] Phát hiện captcha/challenge — đang xử lý (vòng {round}/{maxCaptchaRounds})...");
                        await EnsureNoCaptchaResolvedAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
                    }
                }

                throw new InvalidOperationException("Không thể hoàn tất đăng nhập sau khi xử lý captcha nhiều lần.");
            }
            finally
            {
                logAction?.Invoke("[LOGIN] Đóng cửa sổ trình duyệt đăng nhập...");
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Đăng nhập thủ công một lần: Chrome (Selenium) với đúng user-data-dir + proxy của profile,
        /// chờ cookie/localStorage báo đã login (vd. <c>sessionid</c>), rồi trích xuất tài khoản —
        /// ưu tiên <c>window.__INITIAL_PROPS__.value.userState.user</c>, sau đó hydration + <c>/api/me</c>, đóng Chrome, lưu appsettings.
        /// </summary>
        public async Task<TikTokAccountSnapshot> ManualLoginAsync(
            string profileName,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Profile name is required.", nameof(profileName));
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var profile = ResolveRunningProfile(settings, profileName, logAction);
            profile = await _configManager
                .EnsureProfileFingerprintAsync(settings, profile?.Name, logAction)
                .ConfigureAwait(false);
            var effectiveName = profileName.Trim();

            var userDataDir = ResolveSeleniumUserDataDirectory(profile, effectiveName);
            Directory.CreateDirectory(userDataDir);
            logAction?.Invoke($"[SELENIUM] user-data-dir: {userDataDir}");

            var proxyServer = BuildSeleniumProxyServerArgument(profile);
            if (!string.IsNullOrWhiteSpace(proxyServer))
            {
                logAction?.Invoke("[SELENIUM] proxy-server: " + proxyServer);
            }

            if (profile != null &&
                (!string.IsNullOrWhiteSpace(profile.ProxyUser) || !string.IsNullOrWhiteSpace(profile.ProxyPass)))
            {
                logAction?.Invoke("[SELENIUM] Proxy có user/pass — Chrome có thể hỏi xác thực một lần trong cửa sổ.");
            }

            TikTokAccountSnapshot snapshot = null;

            await Task.Run(
                    () =>
                    {
                        ChromeDriver driver = null;
                        try
                        {
                            var options = new ChromeOptions();
                            options.AddArgument($"--user-data-dir={userDataDir}");
                            options.AddArgument("--disable-blink-features=AutomationControlled");
                            options.AddExcludedArgument("enable-automation");
                            options.AddAdditionalOption("useAutomationExtension", false);
                            options.AddArgument("--disable-dev-shm-usage");
                            options.AddArgument("--mute-audio");
                            options.AddArgument("--lang=en-US");
                            if (!string.IsNullOrWhiteSpace(proxyServer))
                            {
                                options.AddArgument("--proxy-server=" + proxyServer);
                            }

                            var service = ChromeDriverService.CreateDefaultService();
                            service.HideCommandPromptWindow = true;

                            driver = new ChromeDriver(service, options);
                            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(90);
                            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(45);

                            driver.Navigate().GoToUrl("https://www.tiktok.com/");
                            logAction?.Invoke("[SELENIUM] Đã mở tiktok.com — vui lòng đăng nhập / giải captcha trong Chrome.");

                            var waitUntil = DateTime.UtcNow.AddMinutes(15);
                            while (DateTime.UtcNow < waitUntil)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                Thread.Sleep(1800);

                                if (SeleniumHasTikTokSessionCookies(driver))
                                {
                                    logAction?.Invoke("[SELENIUM] Phát hiện cookie phiên đăng nhập TikTok.");
                                    break;
                                }

                                if (SeleniumLocalStorageSuggestsLogin(driver))
                                {
                                    logAction?.Invoke("[SELENIUM] Phát hiện localStorage/sessionStorage có dấu hiệu đã đăng nhập.");
                                    break;
                                }
                            }

                            if (!SeleniumHasTikTokSessionCookies(driver) && !SeleniumLocalStorageSuggestsLogin(driver))
                            {
                                throw new TimeoutException(
                                    "Hết thời gian chờ — chưa thấy cookie phiên TikTok hoặc dấu hiệu localStorage sau khi đăng nhập.");
                            }

                            Thread.Sleep(3000);
                            snapshot = null;
                            foreach (var url in new[]
                                     {
                                         "https://www.tiktok.com/",
                                         "https://www.tiktok.com/@me",
                                         "https://www.tiktok.com/foryou",
                                         "https://www.tiktok.com/following"
                                     })
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                driver.Navigate().GoToUrl(url);
                                Thread.Sleep(2800);

                                var json = SeleniumExecuteMineAccountJson(driver, logAction);
                                snapshot = ParseTikTokMineJson(json);
                                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.UniqueId))
                                {
                                    break;
                                }
                            }

                            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.UniqueId))
                            {
                                snapshot = SeleniumExtractFromProfileAnchors(driver);
                            }

                            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.UniqueId))
                            {
                                throw new InvalidOperationException(
                                    "Không trích xuất được UniqueId từ trang TikTok (DOM/API). Thử đăng nhập lại hoặc kiểm tra proxy.");
                            }

                            logAction?.Invoke(
                                $"[SELENIUM] Đã đọc: @{snapshot.UniqueId.TrimStart('@')}" +
                                (string.IsNullOrWhiteSpace(snapshot.Nickname) ? string.Empty : $" — {snapshot.Nickname}"));

                            try
                            {
                                driver?.Quit();
                            }
                            catch
                            {
                                // ignore
                            }

                            driver = null;
                            Thread.Sleep(400);
                        }
                        finally
                        {
                            try
                            {
                                driver?.Quit();
                            }
                            catch
                            {
                                // ignore
                            }

                            driver = null;
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await _configManager
                .UpdateAutomationProfileTikTokAsync(effectiveName, snapshot, logAction)
                .ConfigureAwait(false);
            return snapshot;
        }

        /// <summary>
        /// Opens the profile browser on TikTok login and leaves it open until the user closes all windows.
        /// Does not wait for login detection or save @nick — use «Sign in to TikTok (QR)» for that, or enter @nick in Settings.
        /// </summary>
        public async Task OpenManualTikTokBrowserAsync(
            string runningProfileName,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var profile = ResolveRunningProfile(settings, runningProfileName, logAction);
            profile = await _configManager
                .EnsureProfileFingerprintAsync(settings, profile?.Name, logAction)
                .ConfigureAwait(false);
            var effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                ? (profile?.Name ?? "default")
                : runningProfileName.Trim();

            logAction?.Invoke($"[MANUAL] Profile '{effectiveProfileName}' — Playwright dùng cùng thư mục persistent như Chrome/Selenium user-data.");

            var browser = new BrowserAutomation();
            try
            {
                await browser.LaunchAsync(cancellationToken, logAction, effectiveProfileName, profile).ConfigureAwait(false);
                await browser.OpenTikTokLoginAndWaitUntilClosedAsync(cancellationToken, logAction).ConfigureAwait(false);
                logAction?.Invoke("[MANUAL] Trình duyệt đã đóng.");
            }
            finally
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }

        private const string SeleniumBrowserProfileFolder = "browser_profile";

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

        private static readonly string SeleniumMineAccountAsyncScript =
            "var cb = arguments[arguments.length - 1];" +
            "(function(){" +
            "function normalizeUser(u){" +
            "if(!u||typeof u!=='object')return null;" +
            "var uid=(u.uniqueId||u.unique_id||u.uniqueID||u.user_unique_id||u.username||'');" +
            "var uidText=String(uid||'').trim().replace(/^@+/,'');" +
            "if(!uidText)return null;" +
            "var nickname=u.nickname||u.nickName||u.displayName||u.name||'';" +
            "var userId=u.userId||u.user_id||u.uid||u.id||u.secUid||'';" +
            "return{uniqueId:uidText,nickname:String(nickname||'').trim(),userId:String(userId||'').trim()};" +
            "}" +
            "function addCandidate(list,u,src,score){" +
            "var n=normalizeUser(u);if(!n)return;n.source=src;n.score=score||0;list.push(n);" +
            "}" +
            "function pickBest(list){if(!list||!list.length)return null;list.sort(function(a,b){return(b.score||0)-(a.score||0);});return list[0];}" +
            "function collectFromState(obj,prefix){" +
            "var out=[];if(!obj||typeof obj!=='object')return out;" +
            "var detail=obj.__DEFAULT_SCOPE__&&obj.__DEFAULT_SCOPE__['webapp.user-detail'];" +
            "if(detail){addCandidate(out,detail.userInfo||detail.user||detail,prefix+'.webapp.user-detail',100);}" +
            "addCandidate(out,obj.currentUser,prefix+'.currentUser',95);" +
            "addCandidate(out,obj.loginUser,prefix+'.loginUser',95);" +
            "addCandidate(out,obj.userInfo,prefix+'.userInfo',92);" +
            "addCandidate(out,obj.me,prefix+'.me',92);" +
            "addCandidate(out,obj.data&&obj.data.user,prefix+'.data.user',91);" +
            "addCandidate(out,obj.user,prefix+'.user',87);" +
            "var initU=obj.value&&obj.value.userState&&obj.value.userState.user;" +
            "addCandidate(out,initU,prefix+'.value.userState.user',118);" +
            "var users=obj.UserModule&&obj.UserModule.users;" +
            "var um=obj.UserModule||{};var lcm=obj.LoginContextModule||{};" +
            "var loginKeys=[um.uid,um.currentUid,um.loginUid,lcm.uid,lcm.userId,obj.uid,obj.userId]" +
            ".filter(function(x){return x!=null&&String(x).trim().length>0;}).map(function(x){return String(x).trim();});" +
            "if(users&&typeof users==='object'){" +
            "for(var i=0;i<loginKeys.length;i++){var key=loginKeys[i];if(users[key])addCandidate(out,users[key],prefix+'.UserModule.users['+key+']',98);}" +
            "var keys=Object.keys(users);for(var j=0;j<keys.length;j++){var u=users[keys[j]];if(u&&typeof u==='object'&&(u.isSelf===true||u.isLoginUser===true||u.isCurrentUser===true))addCandidate(out,u,prefix+'.UserModule.users[self-flag]',96);}" +
            "}" +
            "addCandidate(out,um.user,prefix+'.UserModule.user',80);" +
            "addCandidate(out,um.currentUser,prefix+'.UserModule.currentUser',88);" +
            "addCandidate(out,lcm.userInfo,prefix+'.LoginContextModule.userInfo',90);" +
            "return out;" +
            "}" +
            "function parseJsonText(text,prefix){if(!text||!text.trim())return[];try{return collectFromState(JSON.parse(text),prefix);}catch(e){return [];}}" +
            "var candidates=[];" +
            "try{" +
            "var ids=['__UNIVERSAL_DATA_FOR_REHYDRATION__','SIGI_STATE','__NEXT_DATA__'];" +
            "for(var a=0;a<ids.length;a++){" +
            "var el=document.getElementById(ids[a]);" +
            "var found=parseJsonText(el&&el.textContent?el.textContent:'','script:'+ids[a]);" +
            "for(var b=0;b<found.length;b++)candidates.push(found[b]);" +
            "}" +
            "}catch(e1){}" +
            "try{" +
            "var g=[['SIGI_STATE',window.SIGI_STATE],['__UNIVERSAL_DATA_FOR_REHYDRATION__',window.__UNIVERSAL_DATA_FOR_REHYDRATION__]," +
            "['__NEXT_DATA__',window.__NEXT_DATA__],['__INITIAL_PROPS__',window.__INITIAL_PROPS__]];" +
            "for(var c=0;c<g.length;c++){var f=collectFromState(g[c][1],'window:'+g[c][0]);for(var d=0;d<f.length;d++)candidates.push(f[d]);}" +
            "}catch(e2){}" +
            "var origin=(typeof location!=='undefined'&&location.origin)?location.origin:'';" +
            "fetch(origin+'/api/me/?aid=1988&app_name=tiktok_web',{credentials:'include',headers:{'accept':'application/json, text/plain, */*'}})" +
            ".then(function(r){if(!r)return Promise.resolve({ok:false,t:''});return r.text().then(function(t){return{ok:!!r.ok,t:t||''};});})" +
            ".then(function(o){if(o.ok){var arr=parseJsonText(o.t,'api:/api/me');for(var e=0;e<arr.length;e++){var c=arr[e];if(c&&typeof c==='object'){c.score=120;c.source='api:/api/me';candidates.push(c);}}}var best=pickBest(candidates);if(!best){cb('{}');return;}cb(JSON.stringify({uniqueId:best.uniqueId,nickname:best.nickname,userId:best.userId}));})" +
            ".catch(function(){var best=pickBest(candidates);if(!best){cb('{}');return;}cb(JSON.stringify({uniqueId:best.uniqueId,nickname:best.nickname,userId:best.userId}));});" +
            "})();";

        /// <summary>
        /// Chỉ đọc link profile trong sidebar/nav — không quét feed (tránh lấy nhầm @ creator trên For You).
        /// </summary>
        private static readonly string SeleniumMineAccountNavSidebarScript =
            "return JSON.stringify((function(){function z(s){return String(s||'').trim().replace(/^@+/g,'');}" +
            "function fromHref(href){" +
            "if(!href)return null;var mm=href.match(/tiktok\\.com\\/@([^\\/\\?\\#]+)/i)||href.match(/^\\/@([^\\/\\?\\#]+)/);" +
            "if(!mm||!mm[1])return null;var uid=z(decodeURIComponent(mm[1]));if(!uid)return null;" +
            "return{uniqueId:uid,nickname:'',userId:''};" +
            "}" +
            "var a=document.querySelector('a[data-e2e=\"nav-profile\"]');" +
            "if(a){var h=a.getAttribute('href')||'',o=fromHref(h);if(o)return o;}" +
            "var av=document.querySelector('[data-e2e=\"user-avatar\"]');" +
            "if(av){var n=av;for(var i=0;i<10&&n;i++){" +
            "if(n.tagName==='A'){var h2=n.getAttribute('href')||'';if(h2.indexOf('/@')===0||h2.toLowerCase().indexOf('tiktok.com/@')>=0){var o2=fromHref(h2);if(o2)return o2;}}" +
            "n=n.parentElement;}}" +
            "var aside=document.querySelector('aside');" +
            "if(aside){var links=aside.querySelectorAll('a[href^=\"/@\"],a[href*=\"tiktok.com/@\"]');" +
            "for(var li=links.length-1;li>=0;li--){" +
            "var el=links[li],href=el.getAttribute('href')||'',o3=fromHref(href);if(o3)return o3;" +
            "}}" +
            "return{};})());";

        /// <summary>
        /// Đồng bộ đọc user đăng nhập từ <c>window.__INITIAL_PROPS__.value.userState.user</c> (pattern phổ biến sau khi có session).
        /// </summary>
        private static readonly string SeleniumMineInitialPropsUserScript =
            "return (function(){try{" +
            "var v=window.__INITIAL_PROPS__&&window.__INITIAL_PROPS__.value&&window.__INITIAL_PROPS__.value.userState&&window.__INITIAL_PROPS__.value.userState.user;" +
            "if(!v||typeof v!=='object')return'';" +
            "var uid=String(v.uniqueId||v.unique_id||v.uniqueID||v.username||'').trim().replace(/^@+/,'');" +
            "if(!uid)return'';" +
            "return JSON.stringify({uniqueId:uid,nickname:String(v.nickname||v.nickName||v.displayName||v.name||'').trim()," +
            "userId:String(v.userId||v.user_id||v.uid||v.id||v.secUid||'').trim()});" +
            "}catch(e){return'';}})();";

        private static string ResolveSeleniumUserDataDirectory(AutomationProfile profile, string activeProfileName)
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

            var sanitized = SanitizeProfileNameForSelenium(activeProfileName);
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SeleniumBrowserProfileFolder, sanitized);
        }

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

        private static bool SeleniumLocalStorageSuggestsLogin(IWebDriver driver)
        {
            if (driver == null)
            {
                return false;
            }

            try
            {
                var js = (IJavaScriptExecutor)driver;
                var result = js.ExecuteScript(
                    @"return (function(){
                        try {
                            for (var i = 0; i < localStorage.length; i++) {
                                var k = localStorage.key(i);
                                if (!k) continue;
                                if (k.indexOf('webapp.user-detail') >= 0) return true;
                                var v = localStorage.getItem(k) || '';
                                if (v.indexOf('""uniqueId""') >= 0 && v.indexOf('""nickname""') >= 0) return true;
                            }
                        } catch (e) {}
                        try {
                            for (var j = 0; j < sessionStorage.length; j++) {
                                var k2 = sessionStorage.key(j);
                                if (!k2) continue;
                                var v2 = sessionStorage.getItem(k2) || '';
                                if (v2.indexOf('""uniqueId""') >= 0) return true;
                            }
                        } catch (e2) {}
                        return false;
                    })();");
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static string SeleniumExecuteMineAccountJson(IWebDriver driver, Action<string> logAction = null)
        {
            if (driver == null)
            {
                return string.Empty;
            }

            var js = (IJavaScriptExecutor)driver;

            try
            {
                var initJson = js.ExecuteScript(SeleniumMineInitialPropsUserScript)?.ToString();
                if (!string.IsNullOrWhiteSpace(initJson))
                {
                    var initSnap = ParseTikTokMineJson(initJson);
                    if (initSnap != null && !string.IsNullOrWhiteSpace(initSnap.UniqueId))
                    {
                        return initJson;
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[SELENIUM] __INITIAL_PROPS__ mine (sync): " + ex.Message);
            }

            try
            {
                var asyncResult = js.ExecuteAsyncScript(SeleniumMineAccountAsyncScript);
                var asyncText = asyncResult?.ToString() ?? string.Empty;
                var asyncSnap = ParseTikTokMineJson(asyncText);
                if (asyncSnap != null && !string.IsNullOrWhiteSpace(asyncSnap.UniqueId))
                {
                    return asyncText;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[SELENIUM] Async mine: " + ex.Message);
            }

            try
            {
                var navJson = js.ExecuteScript(SeleniumMineAccountNavSidebarScript)?.ToString();
                if (!string.IsNullOrWhiteSpace(navJson))
                {
                    var navSnap = ParseTikTokMineJson(navJson);
                    if (navSnap != null && !string.IsNullOrWhiteSpace(navSnap.UniqueId))
                    {
                        return navJson;
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[SELENIUM] Nav sidebar mine (sync): " + ex.Message);
            }

            return string.Empty;
        }

        private static TikTokAccountSnapshot SeleniumExtractFromProfileAnchors(IWebDriver driver)
        {
            if (driver == null)
            {
                return null;
            }

            TikTokAccountSnapshot tryOne(IWebElement el)
            {
                if (el == null)
                {
                    return null;
                }

                string href;
                try
                {
                    href = el.GetDomAttribute("href");
                }
                catch
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(href))
                {
                    return null;
                }

                var m = Regex.Match(href, @"tiktok\.com/@([^\/\?\#]+)", RegexOptions.IgnoreCase);
                if (!m.Success)
                {
                    m = Regex.Match(href, @"^/@([^\/\?\#]+)", RegexOptions.IgnoreCase);
                }

                if (!m.Success)
                {
                    return null;
                }

                var handle = Uri.UnescapeDataString(m.Groups[1].Value).Trim().TrimStart('@');
                if (string.IsNullOrWhiteSpace(handle))
                {
                    return null;
                }

                return new TikTokAccountSnapshot
                {
                    UniqueId = handle,
                    Nickname = string.Empty,
                    UserId = string.Empty
                };
            }

            try
            {
                var nav = driver.FindElements(By.CssSelector("a[data-e2e=\"nav-profile\"]"));
                foreach (var el in nav)
                {
                    var s = tryOne(el);
                    if (s != null)
                    {
                        return s;
                    }
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                var aside = driver.FindElements(By.CssSelector("aside a[href^='/@'], aside a[href*='tiktok.com/@']"));
                for (var i = aside.Count - 1; i >= 0; i--)
                {
                    var s = tryOne(aside[i]);
                    if (s != null)
                    {
                        return s;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static TikTokAccountSnapshot ParseTikTokMineJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            JObject jo;
            try
            {
                jo = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }

            var uniqueId = (jo["uniqueId"] ?? string.Empty).ToString().Trim().TrimStart('@');
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                return null;
            }

            var rawNick = (jo["nickname"] ?? string.Empty).ToString().Trim();
            return new TikTokAccountSnapshot
            {
                UniqueId = uniqueId,
                Nickname = TikTokDisplayNicknameSanitizer.Sanitize(rawNick, uniqueId),
                UserId = (jo["userId"] ?? string.Empty).ToString().Trim()
            };
        }

        private static AutomationProfile ResolveRunningProfile(AppSettings settings, string runningProfileName, Action<string> logAction)
        {
            var profiles = settings?.Profiles;
            if (profiles == null || profiles.Count == 0)
            {
                logAction?.Invoke("[LIVE] No profiles configured. Launching Playwright with default profile.");
                return null;
            }

            AutomationProfile selected = null;
            if (!string.IsNullOrWhiteSpace(runningProfileName))
            {
                selected = profiles.FirstOrDefault(p =>
                    p != null &&
                    string.Equals(p.Name, runningProfileName.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (selected == null)
            {
                selected = profiles.FirstOrDefault(p => p != null);
                if (!string.IsNullOrWhiteSpace(runningProfileName))
                {
                    logAction?.Invoke($"[LIVE] Profile '{runningProfileName}' not found. Fallback to first profile.");
                }
            }

            if (selected == null)
            {
                logAction?.Invoke("[LIVE] No valid profile found. Launching with defaults.");
                return null;
            }

            logAction?.Invoke($"[LIVE] Using profile: {selected.Name}");
            return selected;
        }

        private (int min, int max, string mode) BuildHumanLikeWatchRange(int baseMin, int baseMax)
        {
            var min = Math.Max(2, Math.Min(baseMin, baseMax));
            var max = Math.Max(min, Math.Max(baseMin, baseMax));
            var roll = _random.NextDouble();

            // 30% skim quickly (~3-6s), 45% normal, 25% deep watch (can reach ~45s+).
            if (roll < 0.30d)
            {
                var skimMin = Math.Max(3, Math.Min(4, max));
                var skimMax = Math.Max(skimMin, Math.Min(6, max));
                return (skimMin, skimMax, "skim");
            }

            if (roll < 0.75d)
            {
                return (min, max, "normal");
            }

            var deepMin = Math.Max(min, Math.Max(22, max));
            var deepMax = Math.Min(65, Math.Max(deepMin, max + _random.Next(22, 33)));
            if (deepMax < deepMin)
            {
                deepMax = deepMin;
            }

            return (deepMin, deepMax, "deep");
        }

        private async Task<bool> HandleCaptchaAsync(
            BrowserAutomation browser,
            AppSettings settings,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            var notifyText = "Phát hiện Captcha! Vui lòng giải tay hoặc đợi AI giải";
            CaptchaDetected?.Invoke(this, notifyText);
            logAction?.Invoke("[LIVE] " + notifyText);

            for (var attempt = 1; attempt <= MaxCaptchaSolveAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logAction?.Invoke($"[LIVE] Trying to solve captcha with 2Captcha (attempt {attempt}/{MaxCaptchaSolveAttempts})...");

                try
                {
                    var request = await browser.GetCaptchaSolveRequestAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(request.SiteKey))
                    {
                        logAction?.Invoke("[LIVE] Could not extract captcha sitekey. Waiting for manual solve...");
                        await browser.RandomDelayAsync(5000, 7000, cancellationToken).ConfigureAwait(false);
                        if (!await browser.DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                        {
                            return true;
                        }

                        continue;
                    }

                    var token = await _captchaService.SolveCaptchaAsync(
                        settings.TwoCaptchaApiKey,
                        request,
                        cancellationToken).ConfigureAwait(false);

                    await browser.ApplyCaptchaTokenAsync(token, cancellationToken, logAction).ConfigureAwait(false);
                    if (!await browser.DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                    {
                        logAction?.Invoke("[LIVE] Captcha solved successfully. Continuing...");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("[LIVE] Captcha solve attempt failed: " + ex.Message);
                }
            }

            return false;
        }

        /// <summary>
        /// Blocks until captcha / challenge is gone (2Captcha first, then manual solve in browser).
        /// </summary>
        private async Task EnsureNoCaptchaResolvedAsync(
            BrowserAutomation browser,
            AppSettings settings,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            while (await browser.DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
            {
                var solved = await HandleCaptchaAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
                if (solved)
                {
                    continue;
                }

                logAction?.Invoke("[LIVE] Chờ bạn xử lý captcha thủ công trên trình duyệt (sẽ tự tiếp tục khi hết)...");
                while (await browser.DetectChallengeAsync(cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                }

                logAction?.Invoke("[LIVE] Captcha đã được xử lý. Tiếp tục.");
            }
        }

        private async Task RaiseCheckpointAlertAsync(
            BrowserAutomation browser,
            string profileName,
            string message,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var screenshotPath = string.Empty;
            try
            {
                var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "checkpoint_screenshots");
                screenshotPath = await browser.CaptureScreenshotAsync(outputDir, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[LIVE] Capture checkpoint screenshot failed: " + ex.Message);
            }

            CheckpointDetected?.Invoke(this, new CheckpointAlert
            {
                Message = message ?? string.Empty,
                ProfileName = (profileName ?? string.Empty).Trim(),
                DetectedAtLocal = DateTime.Now,
                ScreenshotPath = screenshotPath
            });
        }

        private static string BuildCommentPrompt(string keywords, string style)
        {
            var safeStyle = string.IsNullOrWhiteSpace(style)
                ? "ngắn gọn, tự nhiên, đúng ngữ cảnh, không spam emoji"
                : style.Trim();

            return $"Bạn là người dùng TikTok thật. Hãy viết MỘT comment {safeStyle} cho video thuộc niche: \"{keywords}\". " +
                   "Yêu cầu: tối đa 120 ký tự, không hashtag, không link, không trích dẫn, không xuống dòng, " +
                   "không bắt đầu bằng dấu nháy. Trả về duy nhất nội dung comment, không kèm giải thích.";
        }

        private static string SanitizeComment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var text = raw.Trim();
            text = text.Replace("\r", " ").Replace("\n", " ");
            text = text.Trim('"', '\'', '`', ' ');

            const int maxLen = 150;
            if (text.Length > maxLen)
            {
                text = text.Substring(0, maxLen).TrimEnd();
            }

            return text;
        }

        public async Task AutoPostUpToProductLinkAsync(
            string videoFolder,
            string hashtags,
            string runningProfileName,
            CancellationToken cancellationToken,
            Action<string> logAction,
            string explicitVideoPath = null,
            string userCaptionOverride = null,
            bool clickPublish = false)
        {
            if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            {
                throw new InvalidOperationException("Video folder does not exist.");
            }

            var videoPath = (explicitVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath))
            {
                logAction?.Invoke("Auto Post: using selected video file -> " + Path.GetFileName(videoPath));
            }
            else
            {
                videoPath = GetFirstVideoFile(videoFolder);
            }

            if (string.IsNullOrWhiteSpace(videoPath))
            {
                throw new InvalidOperationException("No video file found in selected folder.");
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var profile = ResolveRunningProfile(settings, runningProfileName, logAction);
            profile = await _configManager
                .EnsureProfileFingerprintAsync(settings, profile?.Name, logAction)
                .ConfigureAwait(false);
            var effectiveProfileName = string.IsNullOrWhiteSpace(runningProfileName)
                ? (profile?.Name ?? "default")
                : runningProfileName.Trim();

            if (_autoPostBrowser != null)
            {
                await _autoPostBrowser.CloseAsync().ConfigureAwait(false);
                _autoPostBrowser = null;
            }

            var browser = new BrowserAutomation();
            _autoPostBrowser = browser;
            await browser.LaunchAsync(cancellationToken, logAction, effectiveProfileName, profile).ConfigureAwait(false);
            await browser.EnsureLoggedInAsync(cancellationToken, logAction).ConfigureAwait(false);

            var page = browser.Page ?? throw new InvalidOperationException("Browser page is not available.");
            await page.GotoAsync("https://www.tiktok.com/creator-center/upload", new PageGotoOptions
            {
                Timeout = 60000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).ConfigureAwait(false);

            logAction?.Invoke("Auto Post: Creator Center upload page opened.");
            await EnsureNoCaptchaResolvedAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);

            await page.SetInputFilesAsync("input[type='file']", videoPath).ConfigureAwait(false);
            logAction?.Invoke("Auto Post: video file selected -> " + Path.GetFileName(videoPath));

            logAction?.Invoke("Auto Post: đang chờ TikTok xử lý video...");
            await WaitForCaptionEditorReadyWithCaptchaAsync(browser, settings, page, cancellationToken, logAction)
                .ConfigureAwait(false);

            var productName = ExtractProductNameFromVideoFile(videoPath);
            var caption = string.IsNullOrWhiteSpace(userCaptionOverride)
                ? BuildAutoPostCaption(productName, hashtags)
                : userCaptionOverride.Trim();
            await FillCaptionAsync(page, caption, cancellationToken).ConfigureAwait(false);
            logAction?.Invoke("Auto Post: caption filled.");

            await browser.RandomDelayAsync(5000, 7000, cancellationToken).ConfigureAwait(false);

            if (!clickPublish)
            {
                logAction?.Invoke("Đã upload xong, mời ông gắn link Affiliate tay và nhấn Đăng");
                return;
            }

            await EnsureNoCaptchaResolvedAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
            var posted = await TryClickPublishButtonAsync(page, cancellationToken, logAction).ConfigureAwait(false);
            if (!posted)
            {
                logAction?.Invoke("Auto Post: không bấm được nút Đăng — hãy kiểm tra giao diện TikTok và bấm tay.");
                return;
            }

            await Task.Delay(4000, cancellationToken).ConfigureAwait(false);
            await EnsureNoCaptchaResolvedAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
            logAction?.Invoke("Auto Post: đã gửi lệnh đăng bài (kiểm tra trạng thái trên TikTok).");
        }

        public async Task CloseAutoPostBrowserAsync(Action<string> logAction)
        {
            if (_autoPostBrowser == null)
            {
                logAction?.Invoke("Auto Post: không có trình duyệt nào đang mở.");
                return;
            }

            try
            {
                await _autoPostBrowser.CloseAsync().ConfigureAwait(false);
                logAction?.Invoke("Auto Post: đã đóng trình duyệt.");
            }
            finally
            {
                _autoPostBrowser = null;
            }
        }

        private static readonly string[] CaptionInputSelectors =
        {
            "div[contenteditable='true'][data-e2e='video-caption']",
            "div[contenteditable='true']",
            "textarea"
        };

        private static readonly string[] PublishButtonSelectors =
        {
            "button[data-e2e='post_video']",
            "button[data-e2e=\"post_video\"]",
            "button[aria-label*='Post']",
            "button:has-text(\"Post\")",
            "button:has-text(\"Đăng\")",
            "[data-e2e='post_video'] button"
        };

        private async Task WaitForCaptionEditorReadyWithCaptchaAsync(
            BrowserAutomation browser,
            AppSettings settings,
            IPage page,
            CancellationToken cancellationToken,
            Action<string> logAction,
            int timeoutSeconds = 300)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsureNoCaptchaResolvedAsync(browser, settings, cancellationToken, logAction).ConfigureAwait(false);
                foreach (var selector in CaptionInputSelectors)
                {
                    var locator = page.Locator(selector).First;
                    if (await locator.IsVisibleAsync().ConfigureAwait(false))
                    {
                        logAction?.Invoke("Auto Post: khung mô tả / caption đã sẵn sàng.");
                        return;
                    }
                }

                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timeout waiting for TikTok upload UI (caption editor).");
        }

        private static async Task<bool> TryClickPublishButtonAsync(
            IPage page,
            CancellationToken cancellationToken,
            Action<string> logAction)
        {
            foreach (var selector in PublishButtonSelectors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var locator = page.Locator(selector).First;
                if (!await locator.IsVisibleAsync().ConfigureAwait(false))
                {
                    continue;
                }

                try
                {
                    var disabled = await locator.GetAttributeAsync("disabled").ConfigureAwait(false);
                    var ariaDisabled = await locator.GetAttributeAsync("aria-disabled").ConfigureAwait(false);
                    if (string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ariaDisabled, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await locator.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
                    await locator.ClickAsync().ConfigureAwait(false);
                    return true;
                }
                catch (Exception ex)
                {
                    logAction?.Invoke("Auto Post: thử nhấn nút đăng lỗi (" + selector + "): " + ex.Message);
                }
            }

            return false;
        }

        private static async Task FillCaptionAsync(IPage page, string caption, CancellationToken cancellationToken)
        {
            foreach (var selector in CaptionInputSelectors)
            {
                var locator = page.Locator(selector).First;
                if (await locator.IsVisibleAsync().ConfigureAwait(false))
                {
                    await locator.ClickAsync().ConfigureAwait(false);
                    await page.Keyboard.PressAsync("Control+A").ConfigureAwait(false);
                    await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
                    await page.Keyboard.TypeAsync(caption).ConfigureAwait(false);
                    return;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Could not find caption input on TikTok upload page.");
        }

        private static string BuildAutoPostCaption(string productName, string hashtags)
        {
            var name = (productName ?? string.Empty).Trim();
            var tagText = (hashtags ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Sản phẩm nổi bật hôm nay";
            }

            if (!string.IsNullOrWhiteSpace(tagText))
            {
                return $"{name} {tagText}".Trim();
            }

            return name;
        }

        private static string ExtractProductNameFromVideoFile(string videoPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(videoPath) ?? string.Empty;
            fileName = fileName.Replace("_", " ").Replace("-", " ").Trim();
            return string.IsNullOrWhiteSpace(fileName) ? "Sản phẩm nổi bật" : fileName;
        }

        private static string GetFirstVideoFile(string folderPath)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp4", ".mov", ".avi", ".mkv", ".webm"
            };

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                if (allowed.Contains(Path.GetExtension(file)))
                {
                    return file;
                }
            }

            return string.Empty;
        }
    }

    public class CheckpointAlert
    {
        public string Message { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public DateTime DetectedAtLocal { get; set; } = DateTime.Now;
        public string ScreenshotPath { get; set; } = string.Empty;
    }
}
