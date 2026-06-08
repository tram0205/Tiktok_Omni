using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services.Mascot
{
    /// <summary>
    /// Cloud lip-sync (SyncLabs/Hedra-style). Dummy endpoint until a real API URL is configured in settings.
    /// </summary>
    public sealed class LipSyncCloudService : IDisposable
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(2, 2);
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public LipSyncCloudService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        }

        /// <summary>
        /// Merges silent video + voice audio. If endpoint is placeholder, muxes locally with FFmpeg.
        /// </summary>
        public async Task<string> LipSyncAsync(
            string silentVideoPath,
            string audioPath,
            string outputPath,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(silentVideoPath) || !File.Exists(silentVideoPath))
            {
                throw new FileNotFoundException("Video câm không tồn tại.", silentVideoPath);
            }

            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                throw new FileNotFoundException("Audio TTS không tồn tại.", audioPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var endpoint = (settings?.TtsEndpoint ?? string.Empty).Trim();
                var apiKey = (settings?.TtsApiKey ?? string.Empty).Trim();
                var canCallCloud = !string.IsNullOrWhiteSpace(endpoint) &&
                                   endpoint.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) < 0 &&
                                   !string.IsNullOrWhiteSpace(apiKey);

                if (canCallCloud)
                {
                    try
                    {
                        return await TryCloudLipSyncAsync(
                            silentVideoPath,
                            audioPath,
                            outputPath,
                            endpoint,
                            apiKey,
                            log,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke("[LipSync Cloud] API lỗi, fallback FFmpeg mux: " + ex.Message);
                    }
                }
                else
                {
                    log?.Invoke("[LipSync Cloud] Chưa cấu hình API thật — dùng FFmpeg ghép video + audio.");
                }

                await MuxVideoAudioAsync(silentVideoPath, audioPath, outputPath, log, cancellationToken)
                    .ConfigureAwait(false);
                return outputPath;
            }
            finally
            {
                Gate.Release();
            }
        }

        private async Task<string> TryCloudLipSyncAsync(
            string silentVideoPath,
            string audioPath,
            string outputPath,
            string endpoint,
            string apiKey,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            log?.Invoke("[LipSync Cloud] POST " + endpoint);
            using (var form = new MultipartFormDataContent())
            {
                var videoBytes = File.ReadAllBytes(silentVideoPath);
                var audioBytes = File.ReadAllBytes(audioPath);
                form.Add(new ByteArrayContent(videoBytes), "video", Path.GetFileName(silentVideoPath));
                form.Add(new ByteArrayContent(audioBytes), "audio", Path.GetFileName(audioPath));

                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
                    request.Content = form;
                    using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Lip-sync HTTP " + (int)response.StatusCode + ": " + body);
                        }

                        if (response.Content.Headers.ContentType?.MediaType?.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var parsed = Newtonsoft.Json.Linq.JObject.Parse(body);
                            var url = (string)(parsed["output_url"] ?? parsed["video_url"] ?? parsed["url"]);
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                await MascotMediaHelper.DownloadAsync(url, outputPath, cancellationToken).ConfigureAwait(false);
                                return outputPath;
                            }
                        }
                        else
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            File.WriteAllBytes(outputPath, bytes);
                            return outputPath;
                        }
                    }
                }
            }

            throw new InvalidOperationException("Lip-sync cloud response không có URL/video.");
        }

        private static async Task MuxVideoAudioAsync(
            string videoPath,
            string audioPath,
            string outputPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var settings = await new ConfigManager().LoadAsync().ConfigureAwait(false);
            var ffmpeg = MascotFfmpegHelper.ResolveFfmpegExecutable(settings);
            var args = new StringBuilder()
                .Append("-y -i \"").Append(videoPath).Append("\" -i \"").Append(audioPath)
                .Append("\" -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -shortest \"")
                .Append(outputPath).Append("\"")
                .ToString();
            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _httpClient?.Dispose();
        }
    }
}
