using System;

using System.Collections.Generic;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using OpenQA.Selenium;



namespace tiktok_Omni.Services

{

    public sealed class AffiliateRevenueService

    {

        private readonly ConfigManager _configManager;

        private readonly SocialAutomation _socialAutomation;

        private readonly NotificationService _notificationService;



        public AffiliateRevenueService(

            ConfigManager configManager,

            SocialAutomation socialAutomation,

            NotificationService notificationService)

        {

            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));

            _socialAutomation = socialAutomation ?? throw new ArgumentNullException(nameof(socialAutomation));

            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        }



        public Task<List<AffiliateRevenueItem>> FetchRevenueReportAsync(

            string profileName,

            Action<string> logAction,

            CancellationToken cancellationToken = default)

        {

            return BrowserLock.WithLockAsync(
                ProfileScopedPaths.ResolveProfileName(profileName),
                ct => FetchRevenueReportCoreAsync(profileName, logAction, ct),

                cancellationToken);

        }



        private async Task<List<AffiliateRevenueItem>> FetchRevenueReportCoreAsync(

            string profileName,

            Action<string> logAction,

            CancellationToken cancellationToken)

        {

            try

            {

                logAction?.Invoke("[Revenue] Bắt đầu cập nhật báo cáo Affiliate…");

                var settings = await _configManager.LoadAsync().ConfigureAwait(false);

                settings.Profiles = settings.Profiles ?? new List<AutomationProfile>();

                var profiles = settings.Profiles.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).ToList();



                if (profiles.Count == 0)

                {

                    var msg = "Không có profile Chrome — thêm profile trong Cài đặt.";

                    logAction?.Invoke("[Revenue] " + msg);

                    await NotifyIssueAsync(settings, msg, logAction, cancellationToken).ConfigureAwait(false);

                    return new List<AffiliateRevenueItem>();

                }



                var ordered = OrderProfilesForScrape(profiles, profileName);

                Exception lastError = null;



                foreach (var profile in ordered)

                {

                    cancellationToken.ThrowIfCancellationRequested();

                    var effectiveProfile = profile.Name.Trim();



                    try

                    {

                        logAction?.Invoke("[Revenue] Đang đăng nhập vào Affiliate Center (profile «" + effectiveProfile + "»)…");

                        var scraped = await _socialAutomation

                            .ScrapeAffiliateRevenueReportAsync(effectiveProfile, profile, cancellationToken, logAction)

                            .ConfigureAwait(false);



                        var items = scraped?.ToList() ?? new List<AffiliateRevenueItem>();

                        if (items.Count == 0)

                        {

                            logAction?.Invoke("[Revenue] Profile «" + effectiveProfile + "»: không có dòng dữ liệu — thử profile khác.");

                            continue;

                        }



                        await AffiliateRevenueStore.SaveAsync(items, effectiveProfile, cancellationToken).ConfigureAwait(false);



                        settings.LastAffiliateRevenueFetchDate = DateTime.Today.ToString("yyyy-MM-dd");

                        await _configManager.SaveAsync(settings).ConfigureAwait(false);



                        logAction?.Invoke("[Revenue] Đã lưu " + items.Count + " dòng → Data/EarningsReport.json (profile «" + effectiveProfile + "»)");

                        return items;

                    }

                    catch (OperationCanceledException)

                    {

                        throw;

                    }

                    catch (WebDriverTimeoutException ex)

                    {

                        lastError = ex;

                        logAction?.Invoke("[Revenue] Timeout profile «" + effectiveProfile + "»: " + ex.Message + " — thử profile tiếp theo.");

                    }

                    catch (NoSuchElementException ex)

                    {

                        lastError = ex;

                        logAction?.Invoke("[Revenue] Không tìm thấy phần tử DOM (profile «" + effectiveProfile + "»): " + ex.Message + " — thử profile tiếp theo.");

                    }

                    catch (Exception ex)

                    {

                        lastError = ex;

                        logAction?.Invoke("[Revenue] Lỗi profile «" + effectiveProfile + "»: " + ex.Message + " — thử profile tiếp theo.");

                    }

                }



                if (lastError != null)

                {

                    await NotifyIssueAsync(settings, lastError.Message, logAction, cancellationToken).ConfigureAwait(false);

                }



                return new List<AffiliateRevenueItem>();

            }

            catch (OperationCanceledException)

            {

                logAction?.Invoke("[Revenue] Đã hủy cào báo cáo.");

                throw;

            }

            catch (Exception ex)

            {

                logAction?.Invoke("[Revenue] Lỗi cào báo cáo: " + ex.Message);

                try

                {

                    var settings = await _configManager.LoadAsync().ConfigureAwait(false);

                    await NotifyIssueAsync(settings, ex.Message, logAction, cancellationToken).ConfigureAwait(false);

                }

                catch

                {

                    // ignored

                }



                return new List<AffiliateRevenueItem>();

            }

        }



        public Task<AffiliateRevenueReportFile> LoadStoredReportAsync(CancellationToken cancellationToken = default)

        {

            return AffiliateRevenueStore.LoadAsync(cancellationToken);

        }



        public static bool ShouldRunDailyFetch(string lastFetchDate)

        {

            var today = DateTime.Today.ToString("yyyy-MM-dd");

            return !string.Equals((lastFetchDate ?? string.Empty).Trim(), today, StringComparison.Ordinal);

        }



        private static List<AutomationProfile> OrderProfilesForScrape(

            List<AutomationProfile> profiles,

            string preferredProfileName)

        {

            var preferred = (preferredProfileName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(preferred))

            {

                return profiles;

            }



            return profiles

                .OrderByDescending(p => string.Equals(p.Name, preferred, StringComparison.OrdinalIgnoreCase))

                .ToList();

        }



        private async Task NotifyIssueAsync(

            AppSettings settings,

            string detail,

            Action<string> logAction,

            CancellationToken cancellationToken)

        {

            var message = new NotificationMessage

            {

                EventType = "affiliate_revenue_scrape",

                Severity = "warning",

                Title = "Affiliate Revenue — cần xử lý",

                Body = detail,

                ProfileName = settings?.Profiles?.FirstOrDefault()?.Name ?? string.Empty

            };



            await _notificationService

                .SendAsync(settings, message, logAction, cancellationToken)

                .ConfigureAwait(false);

        }

    }

}


