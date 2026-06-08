using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (TryRunChromeLoginVerify(args))
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        private static bool TryRunChromeLoginVerify(string[] args)
        {
            if (args == null || args.Length < 2 ||
                !string.Equals(args[0], "--verify-chrome-login", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var profileDir = string.Join(" ", args.Skip(1));
            profileDir = ResolveProfileDirectory(profileDir);

            Environment.ExitCode = ChromeLoginVerifyRunner.Run(profileDir);
            return true;
        }

        private static string ResolveProfileDirectory(string profileDir)
        {
            if (string.IsNullOrWhiteSpace(profileDir))
            {
                return profileDir;
            }

            if (!Path.IsPathRooted(profileDir))
            {
                profileDir = Path.GetFullPath(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, profileDir));
            }

            if (Directory.Exists(profileDir))
            {
                return profileDir;
            }

            var underProfiles = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "browser_profile",
                profileDir.Trim().Trim('"'));
            if (Directory.Exists(underProfiles))
            {
                return Path.GetFullPath(underProfiles);
            }

            return profileDir;
        }
    }
}
