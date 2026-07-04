using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Nhận diện văn bản trong video bằng Tesseract OCR + FFmpeg.
    /// Trích 3 khung hình (10% / 50% / 90% thời lượng) → OCR → quyết định có chữ hay không.
    /// Nếu có chữ → KHÔNG lật ngang (hflip) khi render reup.
    /// </summary>
    public sealed class VideoOcrService
    {
        private const int SampleCount = 3;
        private const int MinSignificantChars = 5;

        // Ngưỡng: tổng ký tự chữ-số từ tất cả frame > ngưỡng → "có chữ"
        private const int MinTotalAlphanumChars = 5;

        private static readonly Regex _noiseRegex = new Regex(
            @"[^\p{L}\p{N}\s]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly string _ffmpegExe;
        private readonly string _ffprobeExe;

        /// <summary>Đường dẫn thư mục tessdata tương đối hoặc tuyệt đối.</summary>
        private readonly string _tessdataPath;

        public VideoOcrService(string ffmpegExePath, string tessdataPath = null)
        {
            _ffmpegExe = ffmpegExePath ?? throw new ArgumentNullException(nameof(ffmpegExePath));
            _ffprobeExe = ResolveFfprobePath(_ffmpegExe);
            _tessdataPath = !string.IsNullOrWhiteSpace(tessdataPath)
                ? tessdataPath
                : ResolveDefaultTessdataPath();
        }

        // ─── API chính ───────────────────────────────────────────────────────

        /// <summary>
        /// Phân tích video và trả về <c>true</c> nếu video chứa văn bản đáng kể
        /// (tổng ký tự chữ-số nhận diện được từ 3 frame > <see cref="MinTotalAlphanumChars"/>).
        /// </summary>
        /// <param name="videoPath">Đường dẫn file video nguồn.</param>
        /// <param name="log">Callback ghi log (tuỳ chọn).</param>
        /// <param name="cancellationToken">Token huỷ.</param>
        /// <returns><c>true</c> = có chữ, <c>false</c> = không có chữ.</returns>
        public async Task<bool> DetectTextInVideoAsync(
            string videoPath,
            Action<string> log = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                log?.Invoke("[OCR] Không tìm thấy file video: " + videoPath);
                return false;
            }

            // A — Lấy thời lượng video
            double durationSec;
            try
            {
                durationSec = await GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
                if (durationSec < 0.5d)
                {
                    log?.Invoke("[OCR] Thời lượng video quá ngắn / không đọc được.");
                    return false;
                }

                log?.Invoke($"[OCR] Thời lượng video: {durationSec:0.##}s.");
            }
            catch (Exception ex)
            {
                log?.Invoke("[OCR] Lỗi đọc thời lượng: " + ex.Message);
                return false;
            }

            // B — Trích xuất 3 khung hình vào thư mục tạm
            var tempFrames = new List<string>(SampleCount);
            var tempDir = Path.Combine(Path.GetTempPath(), "tiktok_ocr_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                double[] timestamps = ComputeSampleTimestamps(durationSec);
                log?.Invoke($"[OCR] Trích {SampleCount} khung hình tại: {string.Join("s, ", Array.ConvertAll(timestamps, t => t.ToString("0.##", CultureInfo.InvariantCulture)))}s.");

                for (int i = 0; i < SampleCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var framePath = Path.Combine(tempDir, $"frame_{i}.jpg");
                    bool extracted = await ExtractFrameAsync(videoPath, timestamps[i], framePath, log, cancellationToken).ConfigureAwait(false);
                    if (extracted && File.Exists(framePath))
                    {
                        tempFrames.Add(framePath);
                    }
                }

                if (tempFrames.Count == 0)
                {
                    log?.Invoke("[OCR] Không trích xuất được khung hình nào.");
                    return false;
                }

                // C + D — OCR và quyết định
                return await RunOcrAndDecideAsync(tempFrames, log, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // E — Dọn dẹp
                CleanupTempFrames(tempDir, tempFrames, log);
            }
        }

        // ─── Bước A: thời lượng ──────────────────────────────────────────────

        private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken)
        {
            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + videoPath + "\"";
            var output = await RunProcessAsync(_ffprobeExe, args, cancellationToken, timeoutMs: 30_000).ConfigureAwait(false);
            if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dur))
            {
                return dur;
            }

            return -1d;
        }

        // ─── Bước B: trích khung hình ────────────────────────────────────────

        private static double[] ComputeSampleTimestamps(double durationSec)
        {
            return new[]
            {
                durationSec * 0.10d,
                durationSec * 0.50d,
                durationSec * 0.90d
            };
        }

        private async Task<bool> ExtractFrameAsync(
            string videoPath,
            double timestampSec,
            string outputPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            // -ss trước -i: seek nhanh (key-frame seek), sau đó -vframes 1 lấy 1 frame
            var tsStr = timestampSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var args = $"-y -ss {tsStr} -i \"{videoPath}\" -vframes 1 -q:v 2 \"{outputPath}\"";
            try
            {
                await RunProcessAsync(_ffmpegExe, args, cancellationToken, timeoutMs: 30_000).ConfigureAwait(false);
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 512;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[OCR] Cảnh báo: không trích được frame tại {timestampSec:0.##}s — {ex.Message}");
                return false;
            }
        }

        // ─── Bước C + D: OCR và quyết định ──────────────────────────────────

        private async Task<bool> RunOcrAndDecideAsync(
            IReadOnlyList<string> framePaths,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_tessdataPath))
            {
                log?.Invoke("[OCR] Không tìm thấy thư mục tessdata: " + _tessdataPath + " — bỏ qua OCR, coi như không có chữ.");
                return false;
            }

            return await Task.Run(() =>
            {
                int totalAlphanumChars = 0;
                try
                {
                    using (var engine = new TesseractEngine(_tessdataPath, "eng+vie", EngineMode.Default))
                    {
                        // Tắt các thành phần không cần thiết cho tốc độ
                        engine.SetVariable("tessedit_pageseg_mode", "6");   // PSM_SINGLE_BLOCK
                        engine.SetVariable("load_system_dawg", "0");
                        engine.SetVariable("load_freq_dawg", "0");

                        foreach (var framePath in framePaths)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                using (var img = Pix.LoadFromFile(framePath))
                                using (var page = engine.Process(img))
                                {
                                    var rawText = page.GetText() ?? string.Empty;
                                    var cleaned = CleanOcrText(rawText);
                                    var alphanumCount = CountAlphanumChars(cleaned);
                                    totalAlphanumChars += alphanumCount;

                                    log?.Invoke($"[OCR] Frame «{System.IO.Path.GetFileName(framePath)}»: {alphanumCount} ký tự hợp lệ.");
                                    if (cleaned.Length > 0)
                                    {
                                        log?.Invoke("[OCR] Nội dung: " + TruncatePreview(cleaned, 80));
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                log?.Invoke($"[OCR] Bỏ qua frame: {ex.Message}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log?.Invoke("[OCR] Lỗi khởi tạo Tesseract: " + ex.Message);
                    return false;
                }

                var hasText = totalAlphanumChars > MinTotalAlphanumChars;
                log?.Invoke($"[OCR] Tổng ký tự hợp lệ: {totalAlphanumChars} → {(hasText ? "CÓ CHỮ (giữ nguyên chiều video)" : "KHÔNG CÓ CHỮ (lật ngang được)")}.");
                return hasText;
            }, cancellationToken).ConfigureAwait(false);
        }

        // ─── Bước E: dọn dẹp ────────────────────────────────────────────────

        private static void CleanupTempFrames(string tempDir, IEnumerable<string> framePaths, Action<string> log)
        {
            foreach (var p in framePaths)
            {
                try
                {
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                    }
                }
                catch
                {
                    // ignored — file tạm, không ảnh hưởng chức năng
                }
            }

            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // ignored
            }
        }

        // ─── Xử lý văn bản ──────────────────────────────────────────────────

        /// <summary>Làm sạch output thô của Tesseract: bỏ ký tự nhiễu, chuẩn hoá khoảng trắng.</summary>
        private static string CleanOcrText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // Xoá ký tự đặc biệt không phải chữ/số/dấu cách
            var s = _noiseRegex.Replace(raw, " ");

            // Chuẩn hoá khoảng trắng nhiều dòng thành một dòng
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        private static int CountAlphanumChars(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 0;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    count++;
                }
            }

            return count;
        }

        private static string TruncatePreview(string s, int maxLen)
        {
            return s.Length <= maxLen ? s : s.Substring(0, maxLen - 1) + "…";
        }

        // ─── Tiện ích Process ────────────────────────────────────────────────

        /// <summary>Chạy process ngầm và trả về stdout (không chặn UI thread).</summary>
        private static async Task<string> RunProcessAsync(
            string executable,
            string arguments,
            CancellationToken cancellationToken,
            int timeoutMs = 60_000)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Chờ không chặn với hỗ trợ cancellation
                var exited = await WaitForExitAsync(process, timeoutMs, cancellationToken).ConfigureAwait(false);
                if (!exited)
                {
                    try { process.Kill(); } catch { /* ignored */ }
                    throw new TimeoutException($"Tiến trình {executable} không phản hồi sau {timeoutMs}ms.");
                }

                return await outputTask.ConfigureAwait(false) ?? string.Empty;
            }
        }

        private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs, CancellationToken cancellationToken)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(timeoutMs);
                try
                {
                    await Task.Run(() => process.WaitForExit(), cts.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    return false; // timeout
                }
            }
        }

        // ─── Resolve path ────────────────────────────────────────────────────

        private static string ResolveFfprobePath(string ffmpegPath)
        {
            if (!string.IsNullOrWhiteSpace(ffmpegPath) &&
                ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                var probe = ffmpegPath.Substring(0, ffmpegPath.Length - "ffmpeg.exe".Length) + "ffprobe.exe";
                if (File.Exists(probe))
                {
                    return probe;
                }
            }

            return "ffprobe";
        }

        private static string ResolveDefaultTessdataPath()
        {
            // 1. Cạnh assembly đang chạy (output dir sau build)
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidate = Path.Combine(appDir, "tessdata");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            // 2. Thư mục project (dev-time)
            candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tessdata");
            try
            {
                candidate = Path.GetFullPath(candidate);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // ignored
            }

            return Path.Combine(appDir, "tessdata");
        }
    }
}
