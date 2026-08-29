using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public static class ReupPreviewFrameService
    {
        public static bool TryExtractFrame(string ffmpegExecutable, string videoPath, string outputJpg, double seekSeconds = 1.0)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(ffmpegExecutable))
            {
                ffmpegExecutable = "ffmpeg";
            }

            try
            {
                var dir = Path.GetDirectoryName(outputJpg);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var seek = Math.Max(0, seekSeconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                var args = "-y -ss " + seek + " -i \"" + videoPath + "\" -frames:v 1 -q:v 2 \"" + outputJpg + "\"";
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExecutable,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        return false;
                    }

                    // ffmpeg in khá nhiều log phân tích stream ra stderr — nếu không đọc, buffer pipe
                    // đầy sẽ làm ffmpeg bị chặn khi ghi tiếp, còn WaitForExit() thì chờ mãi (deadlock).
                    // Đọc bất đồng bộ (Begin*ReadLine) để rút cạn 2 luồng song song trong lúc chờ thoát.
                    process.OutputDataReceived += (_, __) => { };
                    process.ErrorDataReceived += (_, __) => { };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit(120000);
                    return process.ExitCode == 0 && File.Exists(outputJpg);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}