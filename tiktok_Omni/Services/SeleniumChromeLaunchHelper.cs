using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Chrome;

namespace tiktok_Omni.Services
{
    internal static class SeleniumChromeLaunchHelper
    {
        private static readonly string[] ProfileLockFiles = { "SingletonLock", "lockfile", "SingletonCookie" };

        public static void ApplyStableLaunchArguments(ChromeOptions options, bool headless)
        {
            if (options == null)
            {
                return;
            }

            if (headless)
            {
                options.AddArgument("--headless");
            }

            ApplyGpuSafeLaunchArguments(options);
            ApplyChromeUserDataArguments(options);
        }

        /// <summary>Tránh lỗi GPU virtualization trên Windows/VM (ChromeDriver console: kFatalFailure shared context).</summary>
        private static void ApplyGpuSafeLaunchArguments(ChromeOptions options)
        {
            if (options == null)
            {
                return;
            }

            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-gpu-compositing");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--disable-accelerated-2d-canvas");
            options.AddArgument("--disable-accelerated-video-decode");
            options.AddArgument("--use-angle=swiftshader-webgl");
            options.AddArgument("--disable-features=VizDisplayCompositor");
        }

        public static void ApplyChromeUserDataArguments(ChromeOptions options)
        {
            if (options == null)
            {
                return;
            }

            var userDataArg = options.Arguments.FirstOrDefault(a =>
                a != null && a.StartsWith("--user-data-dir=", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(userDataArg))
            {
                return;
            }

            var userDataDir = userDataArg.Substring("--user-data-dir=".Length).Trim().Trim('"');
            if (userDataDir.EndsWith("User Data", StringComparison.OrdinalIgnoreCase) &&
                !options.Arguments.Any(a =>
                    a != null && a.StartsWith("--profile-directory=", StringComparison.OrdinalIgnoreCase)))
            {
                options.AddArgument("--profile-directory=Default");
            }
        }

        public static void PrepareChromeDriverService(ChromeDriverService service)
        {
            if (service == null)
            {
                return;
            }

            // Ẩn console chromedriver — stderr GPU (kFatalFailure virtualization) là vô hại nhưng gây hiểu nhầm crash.
            service.HideCommandPromptWindow = true;
        }

        public static void TryClearStaleProfileLocks(string userDataDir, Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
            {
                return;
            }

            foreach (var lockName in ProfileLockFiles)
            {
                var lockPath = Path.Combine(userDataDir, lockName);
                if (!File.Exists(lockPath))
                {
                    continue;
                }

                if (IsLockFileHeldByAnotherProcess(lockPath))
                {
                    continue;
                }

                try
                {
                    File.Delete(lockPath);
                    logAction?.Invoke("[SELENIUM] Đã xóa lock profile cũ: " + lockName);
                }
                catch
                {
                }
            }
        }

        private static bool IsLockFileHeldByAnotherProcess(string lockPath)
        {
            try
            {
                using (new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void TryTerminateChromeProcessesUsingUserDataDir(string userDataDir, Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(userDataDir))
            {
                return;
            }

            var normalizedTarget = NormalizeProfilePath(userDataDir);
            var terminated = new List<int>();
            var matchedPids = GetChromeProcessIdsUsingUserDataDir(normalizedTarget).ToList();

            foreach (var pid in matchedPids)
            {
                try
                {
                    using (var process = Process.GetProcessById(pid))
                    {
                        process.Kill();
                        terminated.Add(pid);
                    }
                }
                catch
                {
                }
            }

            if (terminated.Count > 0)
            {
                logAction?.Invoke(
                    "[SELENIUM] Đã đóng " + terminated.Count +
                    " tiến trình Chrome automation còn sót cho profile này (PID: " +
                    string.Join(", ", terminated) + ").");
            }

        }

        public static void TryTerminateOrphanedAppProfileChrome(string appBaseDir, Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(appBaseDir))
            {
                return;
            }

            var profilesRoot = NormalizeProfilePath(Path.Combine(appBaseDir, "browser_profile"));
            if (!Directory.Exists(profilesRoot))
            {
                return;
            }

            var terminated = new List<int>();
            foreach (var pid in GetChromeProcessIdsUsingAppProfilesRoot(profilesRoot))
            {
                try
                {
                    using (var process = Process.GetProcessById(pid))
                    {
                        process.Kill();
                        terminated.Add(pid);
                    }
                }
                catch
                {
                }
            }

            if (terminated.Count > 0)
            {
                logAction?.Invoke(
                    "[SELENIUM] Đã đóng " + terminated.Count +
                    " tiến trình Chrome automation còn sót trong browser_profile.");
            }

        }

        private static string NormalizeProfilePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim().Trim('"'))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim().Trim('"')
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static IEnumerable<int> GetChromeProcessIdsUsingUserDataDir(string normalizedUserDataDir)
        {
            var pids = new HashSet<int>();
            foreach (var entry in EnumerateChromeCommandLines())
            {
                var dir = TryExtractUserDataDirFromCommandLine(entry.CommandLine);
                if (dir == null)
                {
                    continue;
                }

                if (ChromeProcessUsesUserDataDir(dir, normalizedUserDataDir))
                {
                    pids.Add(entry.ProcessId);
                }
            }

            return pids;
        }

        private static bool ChromeProcessUsesUserDataDir(string extractedUserDataDir, string normalizedTarget)
        {
            var normalized = NormalizeProfilePath(extractedUserDataDir);
            if (string.Equals(normalized, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var sep = Path.DirectorySeparatorChar;
            if (normalized.StartsWith(normalizedTarget + sep, StringComparison.OrdinalIgnoreCase) ||
                normalizedTarget.StartsWith(normalized + sep, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var targetInfo = new DirectoryInfo(normalizedTarget);
            if (!targetInfo.Exists)
            {
                return false;
            }

            try
            {
                var extractedInfo = new DirectoryInfo(normalized);
                if (extractedInfo.Exists &&
                    string.Equals(extractedInfo.FullName, targetInfo.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
            }

            var profilesRoot = targetInfo.Parent?.FullName;
            if (string.IsNullOrEmpty(profilesRoot) ||
                !normalized.StartsWith(profilesRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var relExtracted = normalized.Substring(profilesRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var relTarget = targetInfo.Name;
            if (string.IsNullOrEmpty(relExtracted))
            {
                return false;
            }

            return string.Equals(relExtracted, relTarget, StringComparison.OrdinalIgnoreCase)
                || relTarget.StartsWith(relExtracted, StringComparison.OrdinalIgnoreCase)
                || relExtracted.StartsWith(relTarget, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<int> GetChromeProcessIdsUsingAppProfilesRoot(string normalizedProfilesRoot)
        {
            var prefix = normalizedProfilesRoot + Path.DirectorySeparatorChar;
            var pids = new HashSet<int>();
            foreach (var cmd in EnumerateChromeCommandLines())
            {
                var dir = TryExtractUserDataDirFromCommandLine(cmd.CommandLine);
                if (dir == null)
                {
                    continue;
                }

                var normalized = NormalizeProfilePath(dir);
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, normalizedProfilesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    pids.Add(cmd.ProcessId);
                }
            }

            return pids;
        }

        private static List<(int ProcessId, string CommandLine)> EnumerateChromeCommandLines()
        {
            var results = new List<(int ProcessId, string CommandLine)>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='chrome.exe'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            var pid = Convert.ToInt32(mo["ProcessId"]);
                            var cmd = mo["CommandLine"] as string;
                            if (!string.IsNullOrWhiteSpace(cmd))
                            {
                                results.Add((pid, cmd));
                            }
                        }
                        finally
                        {
                            mo.Dispose();
                        }
                    }
                }
            }
            catch
            {
            }

            return results;
        }

        private static string TryExtractUserDataDirFromCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return null;
            }

            var match = Regex.Match(
                commandLine,
                @"--user-data-dir=(?:""([^""]+)""|(\S+))",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        private static List<string> GetHeldProfileLocks(string userDataDir)
        {
            return ProfileLockFiles
                .Where(f =>
                {
                    var path = Path.Combine(userDataDir, f);
                    return File.Exists(path) && IsLockFileHeldByAnotherProcess(path);
                })
                .ToList();
        }

        public static void GuardProfileLaunch(string userDataDir, Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
            {
                return;
            }

            TryTerminateChromeProcessesUsingUserDataDir(userDataDir, logAction);
            TryTerminateOrphanedAppProfileChrome(AppDomain.CurrentDomain.BaseDirectory, logAction);
            TryClearStaleProfileLocks(userDataDir, logAction);

            var heldLocks = GetHeldProfileLocks(userDataDir);

            if (heldLocks.Count > 0)
            {
                TryTerminateChromeProcessesUsingUserDataDir(userDataDir, logAction);
                TryTerminateOrphanedAppProfileChrome(AppDomain.CurrentDomain.BaseDirectory, logAction);
                TryClearStaleProfileLocks(userDataDir, logAction);
                heldLocks = GetHeldProfileLocks(userDataDir);
            }

            if (heldLocks.Count == 0)
            {
                return;
            }

            var profileLabel = Path.GetFileName(userDataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var msg =
                "Profile «" + profileLabel + "» đang được một phiên Chrome khác giữ (file " +
                string.Join(", ", heldLocks) +
                "). Bạn không cần tắt toàn bộ Chrome trên máy — chỉ cần đóng cửa sổ Chrome do app này mở trước đó (hoặc cửa sổ automation cùng profile), rồi bấm Tiktok lại.";
            logAction?.Invoke("[SELENIUM] " + msg);
            throw new InvalidOperationException(msg);
        }

        public static void ConfigureLoginDriverTimeouts(ChromeDriver driver)
        {
            if (driver == null)
            {
                return;
            }

            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(45);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);
        }

        public static ChromeDriver CreateLoginChromeDriver(
            string userDataDir,
            string proxyServer,
            Action<string> logAction)
        {
            GuardProfileLaunch(userDataDir, logAction);
            var options = UndetectedChromeOptionsBuilder.Build(userDataDir, proxyServer ?? string.Empty);
            var service = ChromeDriverService.CreateDefaultService();
            var driver = CreateDriver(service, options, logAction);
            ConfigureLoginDriverTimeouts(driver);
            return driver;
        }

        public static ChromeDriver CreateDriver(
            ChromeDriverService service,
            ChromeOptions options,
            Action<string> logAction)
        {
            PrepareChromeDriverService(service);

            var driver = new ChromeDriver(service, options);
            var argList = options?.Arguments;
            var isHeadless = argList != null &&
                argList.Any(a => a != null && a.StartsWith("--headless", StringComparison.OrdinalIgnoreCase));
            if (isHeadless)
            {
                logAction?.Invoke(
                    "[SELENIUM] Chrome headless đã khởi động (quét nền — không có cửa sổ hiển thị).");
            }
            else
            {
                logAction?.Invoke("[SELENIUM] Chrome đã mở thành công.");
            }

            return driver;
        }
    }
}