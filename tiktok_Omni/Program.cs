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
            // Xác nhận DPI awareness từ app.manifest (Output → Debug).
            System.Diagnostics.Debug.WriteLine(
                "Startup DPI-aware: " + Form1.IsAppDpiAware());
            var mainForm = new Form1();
            TryAttachConsoleCancelHandler(mainForm);
            Application.Run(mainForm);
        }

        /// <summary>dotnet run gắn console — Ctrl+C cần đóng WinForms, không chỉ dừng prompt.</summary>
        private static void TryAttachConsoleCancelHandler(Form mainForm)
        {
            try
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    if (mainForm == null || mainForm.IsDisposed)
                    {
                        return;
                    }

                    try
                    {
                        mainForm.BeginInvoke(new Action(() =>
                        {
                            if (mainForm is Form1 app && !app.IsDisposed)
                            {
                                app.ShutdownAndClose();
                            }
                            else if (!mainForm.IsDisposed)
                            {
                                mainForm.Close();
                            }
                        }));
                    }
                    catch
                    {
                        try
                        {
                            Environment.Exit(0);
                        }
                        catch
                        {
                        }
                    }
                };
            }
            catch
            {
                // Không có console (chạy .exe trực tiếp) — bỏ qua.
            }
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
