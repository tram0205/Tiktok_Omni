using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Endpoint + payload helpers cho RapidAPI Google Veo 3.1 Text-to-Video.</summary>
    public static class RapidApiGoogleVeoHelper
    {
        public const string DefaultRapidApiHost = "google-veo-3-1-text-to-video-api.p.rapidapi.com";

        public const string DefaultBaseUrl = "https://" + DefaultRapidApiHost;

        public static bool IsRapidApiVeoEndpoint(string endpoint)
        {
            var e = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(e))
            {
                return true;
            }

            if (FalAiVideoHelper.IsFalEndpoint(e))
            {
                return false;
            }

            return e.IndexOf("google-veo-3-1", StringComparison.OrdinalIgnoreCase) >= 0
                   || (e.IndexOf("rapidapi.com", StringComparison.OrdinalIgnoreCase) >= 0
                       && e.IndexOf("veo", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static string NormalizeBaseUrl(string endpoint)
        {
            var e = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(e)
                || e.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0
                || FalAiVideoHelper.IsFalEndpoint(e))
            {
                return DefaultBaseUrl;
            }

            if (!e.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !e.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                e = "https://" + e.TrimStart('/');
            }

            if (Uri.TryCreate(e, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }

            return DefaultBaseUrl;
        }

        public static string ResolveRapidApiHost(string endpoint)
        {
            var baseUrl = NormalizeBaseUrl(endpoint);
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }

            return DefaultRapidApiHost;
        }

        public static string BuildGenerateUrl(string endpoint)
        {
            return NormalizeBaseUrl(endpoint).TrimEnd('/') + "/generate";
        }

        public static string BuildStatusUrl(string endpoint, string taskId)
        {
            var id = (taskId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            return NormalizeBaseUrl(endpoint).TrimEnd('/') + "/status/" + Uri.EscapeDataString(id);
        }

        public static object BuildGenerateBody(
            string prompt,
            IList<string> imageUrls = null,
            string aspectRatio = "9:16")
        {
            var body = new Dictionary<string, object>
            {
                ["prompt"] = (prompt ?? string.Empty).Trim(),
                ["aspect_ratio"] = NormalizeAspectRatio(aspectRatio)
            };

            var urls = (imageUrls ?? Array.Empty<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (urls.Count > 0)
            {
                body["image_urls"] = urls;
            }

            return body;
        }

        public static string NormalizeAspectRatio(string aspectRatio)
        {
            var ratio = (aspectRatio ?? string.Empty).Trim();
            if (ratio.Equals("16:9", StringComparison.OrdinalIgnoreCase))
            {
                return "16:9";
            }

            return "9:16";
        }

        public static string ExtractTaskId(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var data = json["data"];
            if (data is JArray array && array.Count > 0)
            {
                return FirstString(array[0] as JObject, "task_id", "taskId", "id");
            }

            if (data is JObject obj)
            {
                return FirstString(obj, "task_id", "taskId", "id");
            }

            return FirstString(json, "task_id", "taskId", "id", "data.task_id", "data.id");
        }

        public static string ExtractStatus(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var data = json["data"];
            if (data is JArray array && array.Count > 0)
            {
                return FirstString(array[0] as JObject, "status", "state");
            }

            if (data is JObject obj)
            {
                return FirstString(obj, "status", "state");
            }

            return FirstString(json, "status", "state", "data.status");
        }

        public static string ExtractVideoUrl(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var data = json["data"] as JObject;
            var videos = data?["result"]?["videos"] as JArray
                         ?? data?["videos"] as JArray
                         ?? json["result"]?["videos"] as JArray;

            if (videos != null && videos.Count > 0)
            {
                var urlToken = videos[0]?["url"];
                var fromArray = FirstUrlFromToken(urlToken);
                if (!string.IsNullOrWhiteSpace(fromArray))
                {
                    return fromArray;
                }

                var direct = videos[0]?["url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(direct))
                {
                    return direct.Trim();
                }
            }

            return FirstString(json, "videoUrl", "url", "data.url", "data.videoUrl", "result.videoUrl");
        }

        public static string ExtractErrorMessage(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var message = FirstString(json, "message", "error", "errorMessage", "data.message", "data.error");
            var code = json["code"]?.ToString();
            if (!string.IsNullOrWhiteSpace(code)
                && !code.Equals("200", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(message))
            {
                return "RapidAPI Veo error code " + code;
            }

            return message;
        }

        public static bool IsProcessingStatus(string status)
        {
            var s = NormalizeStatus(status);
            return s == "submitted" || s == "pending" || s == "processing" || s == "queued" || s == "running"
                   || s == "in_progress" || s == "in_queue";
        }

        public static bool IsCompletedStatus(string status)
        {
            var s = NormalizeStatus(status);
            return s == "completed" || s == "complete" || s == "succeeded" || s == "success" || s == "done";
        }

        public static bool IsFailedStatus(string status)
        {
            var s = NormalizeStatus(status);
            return s == "failed" || s == "error" || s == "cancelled" || s == "canceled";
        }

        public static string MapStatus(string status)
        {
            if (IsCompletedStatus(status))
            {
                return "completed";
            }

            if (IsFailedStatus(status))
            {
                return "failed";
            }

            if (IsProcessingStatus(status))
            {
                return "processing";
            }

            return NormalizeStatus(status);
        }

        private static string FirstUrlFromToken(JToken token)
        {
            if (token == null)
            {
                return string.Empty;
            }

            if (token is JArray array)
            {
                foreach (var item in array)
                {
                    var value = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }

                return string.Empty;
            }

            var direct = token.ToString();
            return string.IsNullOrWhiteSpace(direct) ? string.Empty : direct.Trim();
        }

        private static string NormalizeStatus(string status)
        {
            return (status ?? string.Empty).Trim().ToLowerInvariant();
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
