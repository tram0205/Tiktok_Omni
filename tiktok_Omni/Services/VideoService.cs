using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;

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
            var body = new System.Collections.Generic.Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["sourceImageUrl"] = sourceImageUrl
            };
            if (durationSeconds > 0.1d)
            {
                // Send both keys to maximize compatibility across Veo-compatible gateways.
                body["duration"] = durationSeconds;
                body["durationSeconds"] = durationSeconds;
            }
            request.AddJsonBody(body);

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["videoUrl"]?.ToString()
                   ?? json["outputVideoUrl"]?.ToString()
                   ?? json["data"]?["videoUrl"]?.ToString()
                   ?? json["result"]?["videoUrl"]?.ToString()
                   ?? string.Empty;
        }

        public async Task<string> GenerateAudioAsync(
            string script,
            string lyriaApiKey,
            string lyriaEndpoint,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentException("Script is required.", nameof(script));
            }

            if (string.IsNullOrWhiteSpace(lyriaApiKey))
            {
                throw new InvalidOperationException("Lyria API key is required.");
            }

            if (string.IsNullOrWhiteSpace(lyriaEndpoint))
            {
                throw new InvalidOperationException("Lyria endpoint is required.");
            }

            var apiClient = new ApiClient(lyriaEndpoint);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {lyriaApiKey}");
            request.AddJsonBody(new { script });

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
            System.Collections.Generic.IList<string> referenceImageUrls = null,
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

            var imageEndpoint = ResolveImageEndpoint(veoEndpoint);
            var apiClient = new ApiClient(imageEndpoint);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {veoApiKey}");
            var body = new System.Collections.Generic.Dictionary<string, object>
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
            var json = JObject.Parse(response.Content ?? "{}");
            return json["imageUrl"]?.ToString()
                   ?? json["outputImageUrl"]?.ToString()
                   ?? json["data"]?["imageUrl"]?.ToString()
                   ?? json["result"]?["imageUrl"]?.ToString()
                   ?? string.Empty;
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
