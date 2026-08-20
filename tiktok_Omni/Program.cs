using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal static class Program
    {
        private static int _consoleExitPass;

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
            Environment.Exit(0);
        }

        /// <summary>dotnet run gắn console — Ctrl+C cần đóng WinForms, không chỉ dừng prompt.</summary>
        private static void TryAttachConsoleCancelHandler(Form mainForm)
        {
            try
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    RequestConsoleExit(mainForm);
                };
            }
            catch
            {
                // Không có console (chạy .exe trực tiếp) — bỏ qua.
            }
        }

        private static void RequestConsoleExit(Form mainForm)
        {
            var pass = Interlocked.Increment(ref _consoleExitPass);
            if (pass > 1)
            {
                Environment.Exit(0);
                return;
            }

            // UI thread bị kẹt (GetResult/modal/render) → vẫn thoát sau vài giây.
            Task.Run(async () =>
            {
                await Task.Delay(2500).ConfigureAwait(false);
                Environment.Exit(0);
            });

            if (mainForm == null || mainForm.IsDisposed)
            {
                Environment.Exit(0);
                return;
            }

            try
            {
                if (mainForm.InvokeRequired)
                {
                    mainForm.BeginInvoke(new Action(() => TryGracefulConsoleExit(mainForm)));
                }
                else
                {
                    TryGracefulConsoleExit(mainForm);
                }
            }
            catch
            {
                Environment.Exit(0);
            }
        }

        private static void TryGracefulConsoleExit(Form mainForm)
        {
            try
            {
                if (mainForm is Form1 app && !app.IsDisposed)
                {
                    app.ShutdownAndClose(fromConsole: true);
                    return;
                }

                if (!mainForm.IsDisposed)
                {
                    mainForm.Close();
                }
            }
            catch
            {
                Environment.Exit(0);
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
