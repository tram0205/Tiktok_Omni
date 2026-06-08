using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Chèn hook âm thanh 3 giây đầu video Reup (SFX: cười, giật mình, …).</summary>
    public static class VisualHookService
    {
        public const double HookDurationSeconds = 3d;

        public static string GetDefaultHooksDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoReup", "Hooks");
        }

        public static void EnsureHooksDirectoryExists()
        {
            Directory.CreateDirectory(GetDefaultHooksDirectory());
        }

        /// <summary>Cắt/chuẩn hóa SFX thành WAV stereo 48kHz, độ dài cố định 3s.</summary>
        public static async Task<double> PrepareThreeSecondHookWavAsync(
            string sfxFilePath,
            string outputWavPath,
            string ffmpegPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sfxFilePath) || !File.Exists(sfxFilePath))
            {
                throw new FileNotFoundException("Không tìm thấy file âm thanh Hook.", sfxFilePath);
            }

            if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                throw new InvalidOperationException("FFmpeg chưa sẵn sàng — cần cho Hook SFX 3s.");
            }

            var outDir = Path.GetDirectoryName(outputWavPath);
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            var dur = HookDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            var args =
                "-y -hide_banner -loglevel error " +
                "-i \"" + sfxFilePath.Replace("\"", "\\\"") + "\" " +
                "-t " + dur + " -af \"afade=t=in:st=0:d=0.08,volume=1.15\" " +
                "-ar 48000 -ac 2 \"" + outputWavPath.Replace("\"", "\\\"") + "\"";

            log?.Invoke("[VisualHook] Tạo hook SFX 3s từ: " + Path.GetFileName(sfxFilePath));
            await RunFfmpegAsync(ffmpegPath, args, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(outputWavPath))
            {
                throw new InvalidOperationException("FFmpeg không tạo được file hook WAV 3s.");
            }

            return HookDurationSeconds;
        }

        private static Task RunFfmpegAsync(string ffmpegPath, string arguments, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        throw new InvalidOperationException("Không khởi chạy được FFmpeg.");
                    }

                    proc.WaitForExit();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (proc.ExitCode != 0)
                    {
                        var err = proc.StandardError.ReadToEnd();
                        throw new InvalidOperationException(
                            string.IsNullOrWhiteSpace(err) ? "FFmpeg hook SFX thất bại." : err.Trim());
                    }
                }
            }, cancellationToken);
        }
    }
}
