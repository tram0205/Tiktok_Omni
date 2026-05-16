using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Tự tìm hoặc tải ffmpeg.exe + ffprobe.exe (Tools/ffmpeg cạnh exe) — không bắt người dùng Browse thủ công.</summary>
    public static class FfmpegToolkitService
    {
        private const string FfmpegDownloadZipUrl =
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        private static readonly SemaphoreSlim EnsureGate = new SemaphoreSlim(1, 1);
        private static string _cachedFfmpegPath = string.Empty;

        public sealed class ResolvedToolkit
        {
            public string FfmpegExe { get; set; } = string.Empty;
            public string FfprobeExe { get; set; } = string.Empty;
        }

        public static string GetBundledBinDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "Tools", "ffmpeg", "bin");
        }

        public static string GetBundledFfmpegPath()
        {
            return Path.Combine(GetBundledBinDirectory(), "ffmpeg.exe");
        }

        public static string GetBundledFfprobePath()
        {
            return Path.Combine(GetBundledBinDirectory(), "ffprobe.exe");
        }

        /// <summary>Thử tìm sẵn (không tải).</summary>
        public static bool TryResolve(AppSettings settings, out ResolvedToolkit toolkit, out string errorMessage)
        {
            toolkit = null;
            errorMessage = string.Empty;

            if (!string.IsNullOrWhiteSpace(_cachedFfmpegPath) &&
                File.Exists(_cachedFfmpegPath) &&
                TryPairFromFfmpeg(_cachedFfmpegPath, out toolkit))
            {
                return true;
            }

            foreach (var ffmpeg in EnumerateFfmpegCandidates(settings))
            {
                if (!TryPairFromFfmpeg(ffmpeg, out toolkit))
                {
                    continue;
                }

                if (!CanRunMediaTool(toolkit.FfmpegExe) || !CanRunMediaTool(toolkit.FfprobeExe))
                {
                    continue;
                }

                _cachedFfmpegPath = toolkit.FfmpegExe;
                return true;
            }

            errorMessage =
                "Chưa có ffmpeg/ffprobe. App sẽ tự tải vào thư mục Tools\\ffmpeg khi bạn tạo video hoặc khởi động (cần mạng lần đầu).";
            return false;
        }

        /// <summary>Tìm hoặc tải zip FFmpeg (Windows), trả về đường dẫn ffmpeg.exe.</summary>
        public static async Task<string> EnsureAvailableAsync(
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (TryResolve(settings, out var existing, out _))
            {
                return existing.FfmpegExe;
            }

            await EnsureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (TryResolve(settings, out existing, out _))
                {
                    return existing.FfmpegExe;
                }

                log?.Invoke("[FFmpeg] Chưa thấy ffmpeg/ffprobe — bắt đầu tải bản Windows (chỉ lần đầu, ~80–150 MB)...");
                var installed = await DownloadAndInstallWindowsBundleAsync(log, cancellationToken).ConfigureAwait(false);
                _cachedFfmpegPath = installed;
                log?.Invoke("[FFmpeg] Đã sẵn sàng: " + installed);
                return installed;
            }
            finally
            {
                EnsureGate.Release();
            }
        }

        public static IEnumerable<string> EnumerateFfmpegCandidates(AppSettings settings)
        {
            static void Add(List<string> list, string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                var t = path.Trim();
                if (list.Exists(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                list.Add(t);
            }

            var list = new List<string>();
            Add(list, settings?.FfmpegPath);
            Add(list, GetBundledFfmpegPath());

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            Add(list, Path.Combine(baseDir, "ffmpeg.exe"));

            try
            {
                var main = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(main))
                {
                    var dir = Path.GetDirectoryName(main);
                    Add(list, Path.Combine(dir ?? string.Empty, "ffmpeg.exe"));
                    Add(list, Path.Combine(dir ?? string.Empty, "Tools", "ffmpeg", "bin", "ffmpeg.exe"));
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                var loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.GetDirectoryName(loc);
                    Add(list, Path.Combine(dir ?? string.Empty, "ffmpeg.exe"));
                }
            }
            catch
            {
                // ignored
            }

            var fromPath = FindExecutableOnPath("ffmpeg.exe");
            if (!string.IsNullOrWhiteSpace(fromPath))
            {
                Add(list, fromPath);
            }

            return list;
        }

        private static async Task<string> DownloadAndInstallWindowsBundleAsync(
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var binDir = GetBundledBinDirectory();
            Directory.CreateDirectory(binDir);
            var ffmpegTarget = GetBundledFfmpegPath();
            var ffprobeTarget = GetBundledFfprobePath();

            if (File.Exists(ffmpegTarget) && File.Exists(ffprobeTarget) && CanRunMediaTool(ffmpegTarget) && CanRunMediaTool(ffprobeTarget))
            {
                return ffmpegTarget;
            }

            var tempZip = Path.Combine(Path.GetTempPath(), "tiktok_omni_ffmpeg_" + Guid.NewGuid().ToString("N") + ".zip");
            var tempExtract = Path.Combine(Path.GetTempPath(), "tiktok_omni_ffmpeg_ext_" + Guid.NewGuid().ToString("N"));

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(25) })
                using (var resp = await http.GetAsync(FfmpegDownloadZipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                           .ConfigureAwait(false))
                {
                    resp.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        await src.CopyToAsync(fs).ConfigureAwait(false);
                    }
                }

                log?.Invoke("[FFmpeg] Đang giải nén...");
                Directory.CreateDirectory(tempExtract);
                ZipFile.ExtractToDirectory(tempZip, tempExtract);

                var ffmpegSrc = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(ffmpegSrc) || !File.Exists(ffmpegSrc))
                {
                    throw new InvalidOperationException("File zip không chứa ffmpeg.exe.");
                }

                var srcDir = Path.GetDirectoryName(ffmpegSrc) ?? tempExtract;
                var ffprobeSrc = Path.Combine(srcDir, "ffprobe.exe");
                if (!File.Exists(ffprobeSrc))
                {
                    throw new InvalidOperationException("File zip thiếu ffprobe.exe cạnh ffmpeg.exe.");
                }

                Directory.CreateDirectory(binDir);
                File.Copy(ffmpegSrc, ffmpegTarget, true);
                File.Copy(ffprobeSrc, ffprobeTarget, true);

                if (!CanRunMediaTool(ffmpegTarget) || !CanRunMediaTool(ffprobeTarget))
                {
                    throw new InvalidOperationException(
                        "Đã giải nén nhưng không chạy được ffmpeg/ffprobe — thử tắt antivirus hoặc chạy app quyền Administrator.");
                }

                return ffmpegTarget;
            }
            finally
            {
                TryDeleteFile(tempZip);
                TryDeleteDirectory(tempExtract);
            }
        }

        private static bool TryPairFromFfmpeg(string ffmpegPath, out ResolvedToolkit toolkit)
        {
            toolkit = null;
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                return false;
            }

            if (string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            {
                var onPathFfmpeg = FindExecutableOnPath("ffmpeg.exe");
                var onPathFfprobe = FindExecutableOnPath("ffprobe.exe");
                if (string.IsNullOrWhiteSpace(onPathFfmpeg) || string.IsNullOrWhiteSpace(onPathFfprobe))
                {
                    return false;
                }

                toolkit = new ResolvedToolkit { FfmpegExe = onPathFfmpeg, FfprobeExe = onPathFfprobe };
                return true;
            }

            if (!File.Exists(ffmpegPath))
            {
                return false;
            }

            var probe = ResolveFfprobeSibling(ffmpegPath);
            if (!File.Exists(probe))
            {
                return false;
            }

            toolkit = new ResolvedToolkit { FfmpegExe = ffmpegPath, FfprobeExe = probe };
            return true;
        }

        private static string ResolveFfprobeSibling(string ffmpegPath)
        {
            if (ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                return ffmpegPath.Substring(0, ffmpegPath.Length - "ffmpeg.exe".Length) + "ffprobe.exe";
            }

            return Path.Combine(Path.GetDirectoryName(ffmpegPath) ?? string.Empty, "ffprobe.exe");
        }

        private static string FindExecutableOnPath(string exeName)
        {
            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where.exe",
                        Arguments = exeName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                })
                {
                    process.Start();
                    var stdout = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    if (process.ExitCode != 0)
                    {
                        return string.Empty;
                    }

                    var line = (stdout ?? string.Empty)
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .FirstOrDefault(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(x));
                    return line ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool CanRunMediaTool(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                })
                {
                    process.Start();
                    if (!process.WaitForExit(8000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // ignored
                        }

                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
