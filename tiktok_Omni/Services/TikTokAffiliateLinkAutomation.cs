using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace tiktok_Omni.Services
{
    /// <summary>Gắn liên kết sản phẩm TikTok Shop trước khi đăng video.</summary>
    public static class TikTokAffiliateLinkAutomation
    {
        public static async Task<bool> TryAttachAffiliateProductLinkAsync(
            IPage page,
            string affiliateLink,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            if (page == null || string.IsNullOrWhiteSpace(affiliateLink))
            {
                return false;
            }

            try
            {
                log?.Invoke("[TikTok Link] Tìm nút «Thêm liên kết» / Add link…");
                var addLinkSelectors = new[]
                {
                    "button:has-text('Thêm liên kết')",
                    "button:has-text('Add link')",
                    "div:has-text('Thêm liên kết')",
                    "[data-e2e='add-link']"
                };
                if (!await TryClickFirstVisibleAsync(page, addLinkSelectors, cancellationToken, log, "Add link").ConfigureAwait(false))
                {
                    log?.Invoke("[TikTok Link] Không thấy nút Thêm liên kết — bỏ qua.");
                    return false;
                }

                await Task.Delay(1200, cancellationToken).ConfigureAwait(false);

                var productTabSelectors = new[]
                {
                    "div:has-text('Sản phẩm')",
                    "button:has-text('Sản phẩm')",
                    "div:has-text('Product')",
                    "button:has-text('Product')"
                };
                await TryClickFirstVisibleAsync(page, productTabSelectors, cancellationToken, log, "Product tab").ConfigureAwait(false);
                await Task.Delay(800, cancellationToken).ConfigureAwait(false);

                var searchSelectors = new[]
                {
                    "input[placeholder*='Tìm']",
                    "input[placeholder*='Search']",
                    "input[type='search']",
                    "input[type='text']"
                };
                var filled = await TryFillFirstVisibleAsync(page, searchSelectors, affiliateLink.Trim(), cancellationToken, log)
                    .ConfigureAwait(false);
                if (!filled)
                {
                    log?.Invoke("[TikTok Link] Không điền được ô tìm kiếm sản phẩm.");
                    return false;
                }

                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

                var pickSelectors = new[]
                {
                    "[data-e2e='search-product-item']",
                    ".product-item",
                    "div[role='option']",
                    "li[role='option']"
                };
                if (await TryClickFirstVisibleAsync(page, pickSelectors, cancellationToken, log, "Pick product").ConfigureAwait(false))
                {
                    log?.Invoke("[TikTok Link] Đã chọn sản phẩm từ kết quả tìm kiếm.");
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                log?.Invoke("[TikTok Link] Đã dán link — chọn sản phẩm thủ công nếu cần.");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("[TikTok Link] Lỗi: " + ex.Message);
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
                        log?.Invoke("[TikTok Link] Click: " + label + " (" + selector + ")");
                        return true;
                    }
                }
                catch
                {
                    // try next selector
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
                        log?.Invoke("[TikTok Link] Đã dán link vào ô tìm kiếm.");
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
