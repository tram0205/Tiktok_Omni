using System;
using System.Diagnostics;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly TimeSpan ZombieBrowserMaxAge = TimeSpan.FromMinutes(60);

        /// <summary>Dọn chromedriver và Chrome automation còn sót (browser_profile). Không đụng Chrome người dùng.</summary>
        private static void KillZombieBrowserProcesses()
        {
            var currentPid = Process.GetCurrentProcess().Id;
            var cutoffUtc = DateTime.UtcNow - ZombieBrowserMaxAge;

            KillProcessesByName("chromedriver", currentPid, cutoffUtc, killRegardlessOfAge: true);
            SeleniumChromeLaunchHelper.TryTerminateOrphanedAppProfileChrome(
                AppDomain.CurrentDomain.BaseDirectory,
                null);
        }

        private static void KillProcessesByName(
            string processName,
            int currentPid,
            DateTime cutoffUtc,
            bool killRegardlessOfAge)
        {
            Process[] processes = null;
            try
            {
                processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    if (process == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (process.Id == currentPid)
                        {
                            continue;
                        }

                        var shouldKill = killRegardlessOfAge;
                        if (!shouldKill)
                        {
                            try
                            {
                                shouldKill = process.StartTime.ToUniversalTime() < cutoffUtc;
                            }
                            catch
                            {
                                // Không đọc được StartTime (quyền hạn) — không kill, tránh tắt Chrome người dùng.
                                shouldKill = false;
                            }
                        }

                        if (!shouldKill)
                        {
                            continue;
                        }

                        process.Kill();
                    }
                    catch
                    {
                        // ignored — process may have exited
                    }
                    finally
                    {
                        try
                        {
                            process.Dispose();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
            finally
            {
                if (processes != null)
                {
                    foreach (var p in processes)
                    {
                        try
                        {
                            p?.Dispose();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
        }
    }
}
