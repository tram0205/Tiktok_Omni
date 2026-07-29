using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;

using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public class VideoService
    {
        private readonly FalAiVideoService _falAiVideoService = new FalAiVideoService();
        private readonly RapidApiGoogleVeoService _rapidApiGoogleVeoService = new RapidApiGoogleVeoService();
        private readonly EdgeTtsService _edgeTtsService = new EdgeTtsService();

        public async Task<string> GenerateVideoAsync(
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken = default)
        {
            if (FalAiVideoHelper.IsFalEndpoint(veoEndpoint))
            {
                return await _falAiVideoService.GenerateVideoAsync(
                    prompt,
                    veoApiKey,
                    veoEndpoint,
                    cancellationToken).ConfigureAwait(false);
            }

            if (RapidApiGoogleVeoHelper.IsRapidApiVeoEndpoint(veoEndpoint))
            {
                return await _rapidApiGoogleVeoService.GenerateVideoAsync(
                    prompt,
                    veoApiKey,
                    veoEndpoint,
                    cancellationToken).ConfigureAwait(false);
            }

            return await GenerateVideoLegacyAsync(prompt, veoApiKey, veoEndpoint, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> GenerateVideoLegacyAsync(
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(veoApiKey))
            {
                throw new InvalidOperationException("Veo API key is required.");
            }

            if (string.IsNullOrWhiteSpace(veoEndpoint))
            {
                throw new InvalidOperationException("Veo endpoint is required.");
            }

            var apiClient = new ApiClient(veoEndpoint);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {veoApiKey}");
            request.AddJsonBody(new { prompt });

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["videoUrl"]?.ToString()
                   ?? json["data"]?["videoUrl"]?.ToString()
                   ?? json["result"]?["videoUrl"]?.ToString()
                   ?? string.Empty;
        }

        public async Task<string> GenerateVideoFromImageAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            double durationSeconds = 0d,
            CancellationToken cancellationToken = default)
        {
            var parsed = await SubmitVideoFromImageAsync(
                sourceImageUrl,
                prompt,
                veoApiKey,
                veoEndpoint,
                durationSeconds,
                cancellationToken).ConfigureAwait(false);
            return parsed?.VideoUrl ?? string.Empty;
        }

        public async Task<VeoAsyncJobResult> SubmitVideoFromImageAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            double durationSeconds = 0d,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceImageUrl))
            {
                throw new ArgumentException("Source image URL is required.", nameof(sourceImageUrl));
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(veoApiKey))
            {
                throw new InvalidOperationException("Veo API key is required.");
            }

            if (string.IsNullOrWhiteSpace(veoEndpoint))
            {
                throw new InvalidOperationException("Veo endpoint is required.");
            }

            if (FalAiVideoHelper.IsFalEndpoint(veoEndpoint))
            {
                return await _falAiVideoService.SubmitVideoFromImageAsync(
                    sourceImageUrl,
                    prompt,
                    veoApiKey,
                    veoEndpoint,
                    durationSeconds,
                    cancellationToken).ConfigureAwait(false);
            }

            if (RapidApiGoogleVeoHelper.IsRapidApiVeoEndpoint(veoEndpoint))
            {
                return await _rapidApiGoogleVeoService.SubmitVideoFromImageAsync(
                    sourceImageUrl,
                    prompt,
                    veoApiKey,
                    veoEndpoint,
                    durationSeconds,
                    cancellationToken).ConfigureAwait(false);
            }

            var apiClient = new ApiClient(veoEndpoint);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {veoApiKey}");
            var body = new Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["sourceImageUrl"] = sourceImageUrl
            };
            if (durationSeconds > 0.1d)
            {
                body["duration"] = durationSeconds;
                body["durationSeconds"] = durationSeconds;
            }

            request.AddJsonBody(body);
            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return ParseVeoAsyncResponse(response.Content);
        }

        /// <summary>Giữ tương thích — dùng <see cref="ElevenLabsTtsHelper.CreateVietnameseHookVoiceSettings"/>.</summary>
        public static object CreateEmphaticHookVoiceSettings()
        {
            return ElevenLabsTtsHelper.CreateVietnameseHookVoiceSettings();
        }

        public static bool IsElevenLabsEndpoint(string endpoint)
        {
            return (endpoint ?? string.Empty).IndexOf("elevenlabs.io", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            string apiKey,
            string endpoint,
            CancellationToken cancellationToken = default,
            string voiceId = null)
        {
            return await GenerateAudioAsync(text, apiKey, endpoint, cancellationToken, emphaticHook: false, settings: null)
                .ConfigureAwait(false);
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            string apiKey,
            string endpoint,
            CancellationToken cancellationToken,
            bool emphaticHook)
        {
            return await GenerateAudioAsync(text, apiKey, endpoint, cancellationToken, emphaticHook, settings: null)
                .ConfigureAwait(false);
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            AppSettings settings,
            CancellationToken cancellationToken,
            bool emphaticHook = false,
            string voiceIdOverride = null,
            bool showcaseExpressiveBody = false)
        {
            return await GenerateAudioAsync(
                text,
                settings,
                cancellationToken,
                TtsEngineKind.ElevenLabs,
                emphaticHook,
                voiceIdOverride,
                showcaseExpressiveBody,
                logAction: null).ConfigureAwait(false);
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            AppSettings settings,
            CancellationToken cancellationToken,
            TtsEngineKind engine,
            ShowcaseTtsRenderOptions showcaseTts,
            bool emphaticHook = false,
            bool showcaseExpressiveBody = false,
            Action<string> logAction = null)
        {
            var segmentTts = showcaseTts?.ForSegment(emphaticHook) ?? new ShowcaseTtsRenderOptions();
            var preset = segmentTts.Preset;
            var voiceOverride = ShowcaseTtsHelper.ResolveElevenLabsVoiceId(segmentTts, settings);
            var hookFlag = emphaticHook && (preset?.ElevenLabsEmphaticHook ?? true);
            var bodyFlag = showcaseExpressiveBody && (preset?.ElevenLabsExpressiveBody ?? true);
            var lang = ShowcaseTtsHelper.ResolveElevenLabsLanguageCode(segmentTts);

            if (engine == TtsEngineKind.EdgeTts)
            {
                var edgeOpts = ShowcaseEdgeTtsVoiceResolver.Resolve(
                    segmentTts,
                    emphaticHook,
                    showcaseExpressiveBody);
                return await _edgeTtsService.SynthesizeToTempMp3Async(
                    text,
                    edgeOpts,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }

            return await GenerateAudioAsync(
                text,
                settings,
                cancellationToken,
                engine,
                hookFlag,
                voiceOverride,
                bodyFlag,
                logAction,
                lang,
                segmentTts).ConfigureAwait(false);
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            AppSettings settings,
            CancellationToken cancellationToken,
            TtsEngineKind engine,
            bool emphaticHook = false,
            string voiceIdOverride = null,
            bool showcaseExpressiveBody = false,
            Action<string> logAction = null,
            string elevenLabsLanguageCode = null,
            ShowcaseTtsRenderOptions segmentTts = null)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (engine == TtsEngineKind.EdgeTts)
            {
                var edgeOpts = ShowcaseEdgeTtsVoiceResolver.ResolveFemaleSouthYoung();
                return await _edgeTtsService.SynthesizeToTempMp3Async(
                    text,
                    edgeOpts,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }

            return await GenerateAudioAsync(
                text,
                settings.TtsApiKey.Trim(),
                settings.TtsEndpoint.Trim(),
                cancellationToken,
                emphaticHook,
                settings,
                voiceIdOverride,
                showcaseExpressiveBody,
                elevenLabsLanguageCode,
                segmentTts).ConfigureAwait(false);
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            string apiKey,
            string endpoint,
            CancellationToken cancellationToken,
            bool emphaticHook,
            AppSettings settings)
        {
            return await GenerateAudioAsync(
                text,
                apiKey,
                endpoint,
                cancellationToken,
                emphaticHook,
                settings,
                voiceIdOverride: null,
                showcaseExpressiveBody: false).ConfigureAwait(false);
        }

        public async Task<string> GenerateAudioAsync(
            string text,
            string apiKey,
            string endpoint,
            CancellationToken cancellationToken,
            bool emphaticHook,
            AppSettings settings,
            string voiceIdOverride,
            bool showcaseExpressiveBody = false,
            string elevenLabsLanguageCode = null,
            ShowcaseTtsRenderOptions segmentTts = null)
        {
            // 1. NHẬN DIỆN NẾU URL LÀ CỦA ELEVENLABS
            if (IsElevenLabsEndpoint(endpoint))
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("xi-api-key", apiKey.Trim());
                    client.DefaultRequestHeaders.Add("Accept", "audio/mpeg");

                    var voiceId = (voiceIdOverride ?? string.Empty).Trim();
                    var requestUrl = string.IsNullOrEmpty(voiceId)
                        ? endpoint
                        : ElevenLabsTtsHelper.ResolveEndpointWithVoiceId(endpoint, voiceId);
                    var payload = ElevenLabsTtsHelper.BuildPayload(
                        text,
                        settings,
                        emphaticHook,
                        voiceId,
                        showcaseExpressiveBody,
                        elevenLabsLanguageCode,
                        segmentTts);
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(requestUrl, content, cancellationToken);

                    // Bắt lỗi chi tiết nếu có
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        var voiceHint = string.IsNullOrEmpty(voiceId) ? "(từ URL endpoint)" : voiceId;
                        throw new System.Exception(
                            "Lỗi ElevenLabs (" + response.StatusCode + "): " + error +
                            " | voice_id=" + voiceHint +
                            " | url=" + requestUrl);
                    }

                    // ElevenLabs trả về trực tiếp file audio (binary stream)
                    var audioBytes = await response.Content.ReadAsByteArrayAsync();

                    // Lưu file audio ra thư mục Temp của máy tính
                    string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tiktok_Omni_tts");
                    System.IO.Directory.CreateDirectory(tempDir);
                    string tempFile = System.IO.Path.Combine(tempDir, $"tts_elevenlabs_{System.Guid.NewGuid():N}.mp3");

                    System.IO.File.WriteAllBytes(tempFile, audioBytes);

                    // Trả về ĐƯỜNG DẪN LOCAL. Các module khác (như FFmpeg) vẫn sẽ đọc và ghép video bình thường.
                    return tempFile;
                }
            }
            
            // 2. LOGIC CŨ DÀNH CHO API GATEWAY (Giữ lại dự phòng nếu sau này bạn dùng server riêng)
            using (var client = new System.Net.Http.HttpClient())
            {
                var payload = new { text = text };
                var content = new System.Net.Http.StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload), 
                    System.Text.Encoding.UTF8, 
                    "application/json"
                );

                // Tùy thuộc vào gateway cũ mà header có thể là Authorization Bearer hoặc x-api-key
                // client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await client.PostAsync(endpoint, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = Newtonsoft.Json.Linq.JObject.Parse(responseBody);

                return json["audioUrl"]?.ToString() ?? string.Empty;
            }
        }

        public async Task<string> GenerateContextImageAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            IList<string> referenceImageUrls = null,
            CancellationToken cancellationToken = default)
        {
            var parsed = await SubmitContextImageAsync(
                sourceImageUrl,
                prompt,
                veoApiKey,
                veoEndpoint,
                referenceImageUrls,
                cancellationToken).ConfigureAwait(false);
            return parsed?.ImageUrl ?? string.Empty;
        }

        public Task<VeoAsyncJobResult> SubmitContextImageAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            IList<string> referenceImageUrls = null,
            CancellationToken cancellationToken = default)
        {
            return SubmitContextImageInternalAsync(
                sourceImageUrl,
                prompt,
                veoApiKey,
                veoEndpoint,
                referenceImageUrls,
                cancellationToken);
        }

        private async Task<VeoAsyncJobResult> SubmitContextImageInternalAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            IList<string> referenceImageUrls,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceImageUrl))
            {
                throw new ArgumentException("Source image URL is required.", nameof(sourceImageUrl));
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(veoApiKey))
            {
                throw new InvalidOperationException("Veo API key is required.");
            }

            if (string.IsNullOrWhiteSpace(veoEndpoint))
            {
                throw new InvalidOperationException("Veo endpoint is required.");
            }

            if (FalAiVideoHelper.IsFalEndpoint(veoEndpoint))
            {
                return await _falAiVideoService.SubmitContextImageAsync(
                    sourceImageUrl,
                    prompt,
                    veoApiKey,
                    veoEndpoint,
                    referenceImageUrls,
                    cancellationToken).ConfigureAwait(false);
            }

            if (RapidApiGoogleVeoHelper.IsRapidApiVeoEndpoint(veoEndpoint))
            {
                return await _rapidApiGoogleVeoService.SubmitContextImageAsync(
                    sourceImageUrl,
                    prompt,
                    veoApiKey,
                    veoEndpoint,
                    referenceImageUrls,
                    cancellationToken).ConfigureAwait(false);
            }

            var imageEndpoint = ResolveImageEndpoint(veoEndpoint);
            var apiClient = new ApiClient(imageEndpoint);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {veoApiKey}");
            var body = new Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["sourceImageUrl"] = sourceImageUrl
            };
            if (referenceImageUrls != null && referenceImageUrls.Count > 0)
            {
                body["referenceImageUrls"] = referenceImageUrls;
                body["identityReferenceImages"] = referenceImageUrls;
            }

            request.AddJsonBody(body);
            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return ParseVeoAsyncResponse(response.Content);
        }

        public async Task<VeoAsyncJobResult> PollVeoJobAsync(
            string jobId,
            string statusPollUrl,
            string veoApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId) && string.IsNullOrWhiteSpace(statusPollUrl))
            {
                throw new ArgumentException("jobId or statusPollUrl is required.");
            }

            if (FalAiVideoHelper.IsFalEndpoint(veoEndpoint)
                || (!string.IsNullOrWhiteSpace(statusPollUrl)
                    && statusPollUrl.IndexOf("fal.run", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return await _falAiVideoService.PollJobAsync(
                    jobId,
                    statusPollUrl,
                    veoApiKey,
                    veoEndpoint,
                    cancellationToken).ConfigureAwait(false);
            }

            if (RapidApiGoogleVeoHelper.IsRapidApiVeoEndpoint(veoEndpoint)
                || (!string.IsNullOrWhiteSpace(statusPollUrl)
                    && statusPollUrl.IndexOf("google-veo-3-1", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return await _rapidApiGoogleVeoService.PollJobAsync(
                    jobId,
                    statusPollUrl,
                    veoApiKey,
                    veoEndpoint,
                    cancellationToken).ConfigureAwait(false);
            }

            var pollEndpoint = ResolvePollEndpoint(veoEndpoint, statusPollUrl, jobId);
            var apiClient = new ApiClient(pollEndpoint);
            var request = new RestRequest(string.Empty, Method.Get);
            request.AddHeader("Authorization", $"Bearer {veoApiKey}");
            if (!string.IsNullOrWhiteSpace(jobId) &&
                string.IsNullOrWhiteSpace(statusPollUrl))
            {
                request.AddQueryParameter("jobId", jobId);
            }

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return ParseVeoJobStatusResponse(response.Content);
        }

        private static VeoAsyncJobResult ParseVeoAsyncResponse(string jsonText)
        {
            var json = JObject.Parse(jsonText ?? "{}");
            var result = new VeoAsyncJobResult
            {
                JobId = FirstString(json, "jobId", "id", "taskId", "task_id", "operationId", "data.jobId", "data.task_id", "result.jobId"),
                Status = FirstString(json, "status", "state", "data.status", "result.status"),
                VideoUrl = FirstString(json, "videoUrl", "outputVideoUrl", "data.videoUrl", "result.videoUrl"),
                ImageUrl = FirstString(json, "imageUrl", "outputImageUrl", "data.imageUrl", "result.imageUrl"),
                ErrorMessage = FirstString(json, "error", "message", "errorMessage", "data.error"),
                StatusPollUrl = FirstString(json, "statusUrl", "pollUrl", "statusEndpoint", "data.statusUrl")
            };

            if (string.IsNullOrWhiteSpace(result.VideoUrl) && string.IsNullOrWhiteSpace(result.ImageUrl))
            {
                result.VideoUrl = FirstString(json, "url", "outputUrl", "data.url");
            }

            return result;
        }

        private static VeoAsyncJobResult ParseVeoJobStatusResponse(string jsonText)
        {
            return ParseVeoAsyncResponse(jsonText);
        }

        private static string FirstString(JObject json, params string[] paths)
        {
            if (json == null)
            {
                return string.Empty;
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (!path.Contains("."))
                {
                    var direct = json[path]?.ToString();
                    if (!string.IsNullOrWhiteSpace(direct))
                    {
                        return direct.Trim();
                    }

                    continue;
                }

                var token = json.SelectToken(path);
                var nested = token?.ToString();
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested.Trim();
                }
            }

            return string.Empty;
        }

        private static string ResolvePollEndpoint(string veoEndpoint, string statusPollUrl, string jobId)
        {
            if (!string.IsNullOrWhiteSpace(statusPollUrl))
            {
                return statusPollUrl.Trim();
            }

            var baseUrl = (veoEndpoint ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Veo endpoint is required for polling.");
            }

            if (baseUrl.IndexOf("/videos", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return baseUrl + "/" + Uri.EscapeDataString(jobId ?? string.Empty);
            }

            return baseUrl + "/status/" + Uri.EscapeDataString(jobId ?? string.Empty);
        }

        private static string ResolveImageEndpoint(string veoEndpoint)
        {
            var endpoint = (veoEndpoint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return endpoint;
            }

            if (endpoint.IndexOf("/videos", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return endpoint.Replace("/videos", "/images");
            }

            if (!endpoint.EndsWith("/images", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint.TrimEnd('/') + "/images";
            }

            return endpoint;
        }
    }
}
