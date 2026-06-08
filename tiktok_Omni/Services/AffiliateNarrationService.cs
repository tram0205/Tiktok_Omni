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
        private static readonly string[] MultiVoiceIds = { "vi-VN-female", "vi-VN-male", "vi-VN-female-alt" };
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

            if (!useMultiVoice)
            {
                await GenerateSingleVoiceAsync(text, settings, outputAudioFile, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            await GenerateMultiVoiceAsync(text, settings, outputAudioFile, workDirectory, log, cancellationToken).ConfigureAwait(false);
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

            log?.Invoke("[TTS] Sinh giọng đọc đơn…");
            var audioUrl = await _videoService.GenerateAudioAsync(
                text,
                settings.TtsApiKey,
                settings.TtsEndpoint,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(audioUrl))
            {
                throw new InvalidOperationException("TTS returned empty audioUrl.");
            }

            await DownloadToFileAsync(audioUrl, outputAudioFile, cancellationToken).ConfigureAwait(false);
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
                var voiceId = MultiVoiceIds[i % MultiVoiceIds.Length];
                var partPath = Path.Combine(workDirectory, "voice_part_" + (i + 1).ToString("D2", CultureInfo.InvariantCulture) + ".mp3");
                var audioUrl = await _videoService.GenerateAudioAsync(
                    segments[i],
                    settings.TtsApiKey,
                    settings.TtsEndpoint,
                    cancellationToken,
                    voiceId).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    throw new InvalidOperationException("TTS segment " + (i + 1) + " returned empty audioUrl.");
                }

                await DownloadToFileAsync(audioUrl, partPath, cancellationToken).ConfigureAwait(false);
                partFiles.Add(partPath);
            }

            await ConcatAudioPartsAsync(partFiles, outputAudioFile, settings.FfmpegPath, log, cancellationToken).ConfigureAwait(false);
            foreach (var p in partFiles)
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

        private static async Task ConcatAudioPartsAsync(
            IList<string> partFiles,
            string outputFile,
            string ffmpegPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var listFile = Path.Combine(Path.GetDirectoryName(outputFile) ?? ".", "voice_concat_list.txt");
            var lines = partFiles.Select(p => "file '" + p.Replace("'", "'\\''") + "'");
            File.WriteAllLines(listFile, lines, TextFileEncoding.Utf8NoBom);
            var ffmpeg = string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath) ? "ffmpeg" : ffmpegPath;
            var args = "-y -f concat -safe 0 -i \"" + listFile + "\" -c copy \"" + outputFile + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
            try
            {
                File.Delete(listFile);
            }
            catch
            {
                // ignored
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
