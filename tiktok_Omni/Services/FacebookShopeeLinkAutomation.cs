using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace tiktok_Omni.Services
{
    /// <summary>Gắn link sản phẩm Shopee khi đăng Facebook Reels (UI native hoặc fallback caption).</summary>
    public static class FacebookShopeeLinkAutomation
    {
        public static async Task<bool> TryAttachShopeeProductLinkAsync(
            IPage page,
            string shopeeLink,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            if (page == null || !OmnichannelAutoPostFields.IsShopeeProductUrl(shopeeLink))
            {
                return false;
            }

            var link = OmnichannelAutoPostFields.NormalizeLink(shopeeLink);

            try
            {
                log?.Invoke("[Facebook Shopee] Tìm nút thêm liên kết / Add link…");
                var addLinkSelectors = new[]
                {
                    "div[aria-label*='Thêm liên kết']",
                    "div[aria-label*='Add link']",
                    "span:has-text('Thêm liên kết')",
                    "span:has-text('Add link')",
                    "button:has-text('Thêm liên kết')",
                    "button:has-text('Add link')",
                    "div[role='button']:has-text('Thêm liên kết')",
                    "div[role='button']:has-text('Link')"
                };

                if (!await TryClickFirstVisibleAsync(page, addLinkSelectors, cancellationToken, log, "Add link").ConfigureAwait(false))
                {
                    log?.Invoke("[Facebook Shopee] Không thấy nút thêm liên kết trên Reels.");
                    return false;
                }

                await Task.Delay(1200, cancellationToken).ConfigureAwait(false);

                var urlTabSelectors = new[]
                {
                    "span:has-text('URL')",
                    "div:has-text('URL')",
                    "button:has-text('URL')",
                    "span:has-text('Liên kết')",
                    "div:has-text('Liên kết')"
                };
                await TryClickFirstVisibleAsync(page, urlTabSelectors, cancellationToken, log, "URL tab").ConfigureAwait(false);
                await Task.Delay(600, cancellationToken).ConfigureAwait(false);

                var urlFieldSelectors = new[]
                {
                    "input[placeholder*='URL']",
                    "input[placeholder*='url']",
                    "input[placeholder*='http']",
                    "input[type='url']",
                    "input[type='text']"
                };

                if (!await TryFillFirstVisibleAsync(page, urlFieldSelectors, link, cancellationToken, log).ConfigureAwait(false))
                {
                    log?.Invoke("[Facebook Shopee] Không điền được ô URL.");
                    return false;
                }

                await Task.Delay(800, cancellationToken).ConfigureAwait(false);

                var confirmSelectors = new[]
                {
                    "div[aria-label='Done']",
                    "div[aria-label='Xong']",
                    "button:has-text('Done')",
                    "button:has-text('Xong')",
                    "button:has-text('Lưu')",
                    "button:has-text('Save')",
                    "div[role='button']:has-text('Done')",
                    "div[role='button']:has-text('Xong')"
                };

                if (await TryClickFirstVisibleAsync(page, confirmSelectors, cancellationToken, log, "Confirm link").ConfigureAwait(false))
                {
                    log?.Invoke("[Facebook Shopee] Đã gắn link Shopee qua giao diện Reels.");
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                log?.Invoke("[Facebook Shopee] Đã dán URL — xác nhận thủ công nếu cần.");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Facebook Shopee] Lỗi: " + ex.Message);
                return false;
            }
        }

        private static async Task<bool> TryClickFirstVisibleAsync(
            IPage page,
            string[] selectors,
            CancellationToken cancellationToken,
            Action<string> log,
            string label)
        {
            foreach (var selector in selectors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loc = page.Locator(selector).First;
                try
                {
                    if (await loc.IsVisibleAsync().ConfigureAwait(false))
                    {
                        await loc.ClickAsync().ConfigureAwait(false);
                        log?.Invoke("[Facebook Shopee] Click: " + label);
                        return true;
                    }
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }

        private static async Task<bool> TryFillFirstVisibleAsync(
            IPage page,
            string[] selectors,
            string text,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            foreach (var selector in selectors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loc = page.Locator(selector).First;
                try
                {
                    if (await loc.IsVisibleAsync().ConfigureAwait(false))
                    {
                        await loc.FillAsync(text).ConfigureAwait(false);
                        log?.Invoke("[Facebook Shopee] Đã dán link Shopee.");
                        return true;
                    }
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }
    }
}
