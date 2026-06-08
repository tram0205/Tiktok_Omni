using System;
using System.IO;

namespace tiktok_Omni.Services
{
    internal static class ChromeLoginVerifyRunner
    {
        public static int Run(string userDataDir)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
            {
                return 2;
            }

            try
            {
                using (var driver = SeleniumChromeLaunchHelper.CreateLoginChromeDriver(
                    userDataDir,
                    string.Empty,
                    null))
                {
                    driver.Navigate().GoToUrl("https://www.tiktok.com/login");
                }

                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}