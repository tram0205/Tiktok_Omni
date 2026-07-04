using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>TTS đơn giản hoặc đa giọng (ghép segment bằng FFmpeg).</summary>
    public sealed class AffiliateNarrationService
    {
        private readonly VideoService _videoService;

        public AffiliateNarrationService(VideoService videoService = null)
        {
            _videoService = videoService ?? new VideoService();
        }

        public async Task GenerateNarrationAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            bool useMultiVoice,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Narration text is required.", nameof(text));
            }

            if (useMultiVoice)
            {
                await GenerateMultiVoiceAsync(text, settings, outputAudioFile, workDirectory, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            await GenerateSingleVoiceAsync(text, settings, outputAudioFile, log, cancellationToken).ConfigureAwait(false);
        }

        private async Task GenerateSingleVoiceAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.TtsApiKey) || string.IsNullOrWhiteSpace(settings?.TtsEndpoint))
            {
                throw new InvalidOperationException("Missing TTS API key/endpoint.");
            }

            log?.Invoke("[TTS] Sinh giọng đọc (nguyên đoạn script)…");
            var audioRef = await _videoService.GenerateAudioAsync(
                text,
                settings,
                cancellationToken,
                emphaticHook: false).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(audioRef))
            {
                throw new InvalidOperationException("TTS returned empty audio.");
            }

            await PersistAudioResultAsync(audioRef, outputAudioFile, cancellationToken).ConfigureAwait(false);
        }

        private async Task GenerateMultiVoiceAsync(
            string text,
            AppSettings settings,
            string outputAudioFile,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var segments = SplitNarrationSegments(text);
            if (segments.Count <= 1)
            {
                await GenerateSingleVoiceAsync(text, settings, outputAudioFile, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            Directory.CreateDirectory(workDirectory ?? Path.GetDirectoryName(outputAudioFile) ?? ".");
            var partFiles = new List<string>();
            log?.Invoke("[TTS] Đa giọng: " + segments.Count + " đoạn…");
            for (var i = 0; i < segments.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var partPath = Path.Combine(workDirectory, "voice_part_" + (i + 1).ToString("D2", CultureInfo.InvariantCulture) + ".mp3");
                var audioRef = await _videoService.GenerateAudioAsync(
                    segments[i],
                    settings,
                    cancellationToken,
                    emphaticHook: false).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(audioRef))
                {
                    throw new InvalidOperationException("TTS segment " + (i + 1) + " returned empty audio.");
                }

                await PersistAudioResultAsync(audioRef, partPath, cancellationToken).ConfigureAwait(false);
                partFiles.Add(partPath);
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            await ConcatAudioPartsAsync(partFiles, outputAudioFile, ffmpeg, log, cancellationToken).ConfigureAwait(false);
            CleanupTempFiles(partFiles);
        }

        private static List<string> SplitNarrationSegments(string text)
        {
            var parts = Regex.Split(text.Trim(), @"(?<=[\.\!\?…])\s+")
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => x.Length >= 8)
                .ToList();
            if (parts.Count <= 1)
            {
                return new List<string> { text.Trim() };
            }

            return parts.Take(12).ToList();
        }

        private static async Task PersistAudioResultAsync(string audioUrlOrLocalPath, string outputPath, CancellationToken cancellationToken)
        {
            var src = (audioUrlOrLocalPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(src))
            {
                throw new InvalidOperationException("TTS returned empty audio reference.");
            }

            var targetDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(src))
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Copy(src, outputPath);
                return;
            }

            await DownloadToFileAsync(src, outputPath, cancellationToken).ConfigureAwait(false);
        }

        private static async Task ConcatAudioPartsAsync(
            IList<string> partFiles,
            string outputFile,
            string ffmpegPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (partFiles == null || partFiles.Count == 0)
            {
                throw new InvalidOperationException("Không có file MP3 để ghép.");
            }

            if (partFiles.Count == 1)
            {
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }

                File.Copy(partFiles[0], outputFile);
                return;
            }

            var listFile = Path.Combine(Path.GetDirectoryName(outputFile) ?? ".", "narration_concat_" + Guid.NewGuid().ToString("N") + ".txt");
            var lines = partFiles.Select(p => "file '" + p.Replace("'", "'\\''") + "'");
            File.WriteAllLines(listFile, lines, TextFileEncoding.Utf8NoBom);
            var ffmpeg = string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath) ? "ffmpeg" : ffmpegPath;
            var args = "-y -f concat -safe 0 -i \"" + listFile + "\" -c copy \"" + outputFile + "\"";
            log?.Invoke("[TTS] FFmpeg: ghép " + partFiles.Count + " đoạn MP3 → narration hoàn chỉnh…");
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(listFile))
                {
                    File.Delete(listFile);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static string ResolveFfmpegPath(AppSettings settings)
        {
            if (FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
            {
                return toolkit.FfmpegExe;
            }

            var bundled = FfmpegToolkitService.GetBundledFfmpegPath();
            if (File.Exists(bundled))
            {
                return bundled;
            }

            var p = (settings?.FfmpegPath ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p) ? p : bundled;
        }

        private static void CleanupTempFiles(IEnumerable<string> paths)
        {
            foreach (var p in paths)
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
                    // ignored
                }
            }
        }

        private static async Task RunFfmpegAsync(string ffmpeg, string args, Action<string> log, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 120).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    log?.Invoke("[TTS] FFmpeg concat warning: " + err);
                    throw new InvalidOperationException("FFmpeg concat failed (exit " + process.ExitCode + ").");
                }
            }
        }

        private static async Task DownloadToFileAsync(string url, string path, CancellationToken cancellationToken)
        {
            using (var client = new System.Net.WebClient())
            {
                client.Headers.Add("User-Agent", "Mozilla/5.0");
                cancellationToken.Register(() => client.CancelAsync());
                await client.DownloadFileTaskAsync(new Uri(url), path).ConfigureAwait(false);
            }
        }
    }
}
