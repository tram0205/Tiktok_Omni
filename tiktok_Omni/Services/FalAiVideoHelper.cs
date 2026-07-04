using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Endpoint + payload helpers cho FAL.AI (Veo 3.1, Flux, …).</summary>
    public static class FalAiVideoHelper
    {
        public const string DefaultTextToVideoQueueUrl = "https://queue.fal.run/fal-ai/veo3.1/fast";

        public const string DefaultContextImageQueueUrl = "https://queue.fal.run/fal-ai/flux/dev";

        public static bool IsFalEndpoint(string endpoint)
        {
            var e = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(e))
            {
                return false;
            }

            if (e.StartsWith("fal-ai/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return e.IndexOf("fal.run", StringComparison.OrdinalIgnoreCase) >= 0
                   || e.IndexOf("fal.ai", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string NormalizeQueueEndpoint(string endpoint)
        {
            var e = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(e))
            {
                return DefaultTextToVideoQueueUrl;
            }

            if (e.StartsWith("fal-ai/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://queue.fal.run/" + e.TrimStart('/');
            }

            if (e.StartsWith("https://fal.run/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://queue.fal.run/" + e.Substring("https://fal.run/".Length);
            }

            if (e.StartsWith("http://fal.run/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://queue.fal.run/" + e.Substring("http://fal.run/".Length);
            }

            return e;
        }

        public static string ResolveImageToVideoEndpoint(string textToVideoEndpoint)
        {
            var url = NormalizeQueueEndpoint(textToVideoEndpoint);
            if (url.IndexOf("/image-to-video", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url;
            }

            return url.TrimEnd('/') + "/image-to-video";
        }

        public static string ResolveContextImageEndpoint(string textToVideoEndpoint)
        {
            return DefaultContextImageQueueUrl;
        }

        public static string FormatAuthorizationHeader(string apiKey)
        {
            return "Key " + (apiKey ?? string.Empty).Trim();
        }

        public static string MapDurationToken(double durationSeconds)
        {
            if (durationSeconds <= 4.5d)
            {
                return "4s";
            }

            if (durationSeconds <= 6.5d)
            {
                return "6s";
            }

            return "8s";
        }

        public static object BuildTextToVideoBody(string prompt, double durationSeconds = 0d, bool generateAudio = false)
        {
            return new
            {
                prompt = (prompt ?? string.Empty).Trim(),
                aspect_ratio = "9:16",
                duration = durationSeconds > 0.1d ? MapDurationToken(durationSeconds) : "8s",
                resolution = "720p",
                generate_audio = generateAudio
            };
        }

        public static object BuildImageToVideoBody(
            string prompt,
            string imageUrl,
            double durationSeconds = 0d,
            bool generateAudio = false)
        {
            return new
            {
                prompt = (prompt ?? string.Empty).Trim(),
                image_url = (imageUrl ?? string.Empty).Trim(),
                aspect_ratio = "9:16",
                duration = durationSeconds > 0.1d ? MapDurationToken(durationSeconds) : "8s",
                resolution = "720p",
                generate_audio = generateAudio
            };
        }

        public static object BuildContextImageBody(string prompt, string sourceImageUrl)
        {
            var body = new System.Collections.Generic.Dictionary<string, object>
            {
                ["prompt"] = (prompt ?? string.Empty).Trim(),
                ["image_size"] = "portrait_16_9"
            };

            var src = (sourceImageUrl ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(src))
            {
                body["image_url"] = src;
            }

            return body;
        }

        public static string ExtractVideoUrl(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var direct = json["video"]?["url"]?.ToString()
                         ?? json["data"]?["video"]?["url"]?.ToString()
                         ?? json["output"]?["video"]?["url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct.Trim();
            }

            return FirstString(json, "videoUrl", "outputVideoUrl", "url", "data.videoUrl", "result.videoUrl");
        }

        public static string ExtractImageUrl(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var images = json["images"] as JArray ?? json["data"]?["images"] as JArray;
            if (images != null && images.Count > 0)
            {
                var url = images[0]?["url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url.Trim();
                }
            }

            var image = json["image"]?["url"]?.ToString()
                        ?? json["data"]?["image"]?["url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(image))
            {
                return image.Trim();
            }

            return FirstString(json, "imageUrl", "outputImageUrl", "url", "data.imageUrl", "result.imageUrl");
        }

        public static string ExtractRequestId(JObject json)
        {
            return FirstString(json, "request_id", "requestId", "id");
        }

        public static string ExtractStatusUrl(JObject json)
        {
            return FirstString(json, "status_url", "statusUrl");
        }

        public static string ExtractResponseUrl(JObject json)
        {
            return FirstString(json, "response_url", "responseUrl");
        }

        public static string NormalizeStatus(string status)
        {
            return (status ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static bool IsFalProcessingStatus(string status)
        {
            var s = NormalizeStatus(status);
            return s == "in_queue" || s == "in_progress" || s == "processing" || s == "queued" || s == "pending";
        }

        public static bool IsFalCompletedStatus(string status)
        {
            var s = NormalizeStatus(status);
            return s == "completed" || s == "ok" || s == "success" || s == "done";
        }

        public static bool IsFalFailedStatus(string status)
        {
            var s = NormalizeStatus(status);
            return s == "failed" || s == "error" || s == "cancelled" || s == "canceled";
        }

        public static string BuildResponseUrlFromQueueEndpoint(string queueEndpoint, string requestId)
        {
            var baseUrl = NormalizeQueueEndpoint(queueEndpoint).TrimEnd('/');
            var modelPath = ExtractModelPath(baseUrl);
            if (string.IsNullOrEmpty(modelPath) || string.IsNullOrWhiteSpace(requestId))
            {
                return string.Empty;
            }

            return "https://queue.fal.run/" + modelPath + "/requests/" + Uri.EscapeDataString(requestId.Trim());
        }

        public static string BuildStatusUrlFromQueueEndpoint(string queueEndpoint, string requestId)
        {
            var responseUrl = BuildResponseUrlFromQueueEndpoint(queueEndpoint, requestId);
            return string.IsNullOrEmpty(responseUrl) ? string.Empty : responseUrl + "/status";
        }

        private static string ExtractModelPath(string queueUrl)
        {
            var url = (queueUrl ?? string.Empty).Trim();
            var match = Regex.Match(url, @"queue\.fal\.run/(.+)$", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim().TrimEnd('/') : string.Empty;
        }

        private static string FirstString(JObject json, params string[] paths)
        {
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

                var nested = json.SelectToken(path)?.ToString();
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested.Trim();
                }
            }

            return string.Empty;
        }
    }
}
