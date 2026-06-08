using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace tiktok_Omni.Services
{
    /// <summary>Tùy chọn Chrome giảm phát hiện automation (Undetected-style) cho Affiliate Center.</summary>
    public static class UndetectedChromeOptionsBuilder
    {
        public static ChromeOptions Build(string userDataDir, string proxyServer, string userAgent = null)
        {
            var options = new ChromeOptions();
            options.PageLoadStrategy = PageLoadStrategy.Eager;
            options.AddArgument("--user-data-dir=" + userDataDir);
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--start-maximized");
            options.AddArgument("--lang=vi-VN");
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                options.AddArgument("--user-agent=" + userAgent);
            }

            if (!string.IsNullOrWhiteSpace(proxyServer))
            {
                options.AddArgument("--proxy-server=" + proxyServer);
            }

            options.AddUserProfilePreference("credentials_enable_service", false);
            options.AddUserProfilePreference("profile.password_manager_enabled", false);
            SeleniumChromeLaunchHelper.ApplyStableLaunchArguments(options, headless: false);
            return options;
        }

        public static void ApplyStealthScripts(IWebDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            try
            {
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript(
                    "Object.defineProperty(navigator, 'webdriver', {get: () => undefined});");
                js.ExecuteScript(
                    "window.chrome = window.chrome || { runtime: {} };");
            }
            catch
            {
                // ignored
            }
        }
    }
}
