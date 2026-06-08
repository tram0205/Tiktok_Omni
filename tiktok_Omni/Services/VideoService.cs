using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    public class VideoService
    {
        public async Task<string> GenerateVideoAsync(
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken = default)
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

        public async Task<string> GenerateAudioAsync(
            string script,
            string ttsApiKey,
            string ttsEndpoint,
            CancellationToken cancellationToken = default,
            string voiceId = null)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentException("Script is required.", nameof(script));
            }

            if (string.IsNullOrWhiteSpace(ttsApiKey))
            {
                throw new InvalidOperationException("TTS API key is required.");
            }

            if (string.IsNullOrWhiteSpace(ttsEndpoint))
            {
                throw new InvalidOperationException("TTS endpoint is required.");
            }

            var apiClient = new ApiClient(ttsEndpoint);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {ttsApiKey}");
            var body = string.IsNullOrWhiteSpace(voiceId)
                ? (object)new { script }
                : new { script, voiceId = voiceId.Trim() };
            request.AddJsonBody(body);

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["audioUrl"]?.ToString()
                   ?? json["data"]?["audioUrl"]?.ToString()
                   ?? json["result"]?["audioUrl"]?.ToString()
                   ?? string.Empty;
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
                JobId = FirstString(json, "jobId", "id", "taskId", "operationId", "data.jobId", "result.jobId"),
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
