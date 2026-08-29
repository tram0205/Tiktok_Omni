using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace tiktok_Omni.Services
{
    /// <summary>RapidAPI Google Veo 3.1 — POST /generate, GET /status/{{task_id}}.</summary>
    public sealed class RapidApiGoogleVeoService
    {
        private static readonly TimeSpan SubmitTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan DefaultGenerateTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

        public Task<string> GenerateVideoAsync(
            string prompt,
            string rapidApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken = default)
        {
            return GenerateVideoAsync(prompt, rapidApiKey, veoEndpoint, durationSeconds: 0d, cancellationToken);
        }

        public async Task<string> GenerateVideoAsync(
            string prompt,
            string rapidApiKey,
            string veoEndpoint,
            double durationSeconds,
            CancellationToken cancellationToken = default)
        {
            var submit = await SubmitTextToVideoAsync(
                prompt,
                rapidApiKey,
                veoEndpoint,
                durationSeconds,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(submit.VideoUrl))
            {
                return submit.VideoUrl;
            }

            return await WaitForMediaAsync(submit, rapidApiKey, veoEndpoint, cancellationToken).ConfigureAwait(false);
        }

        public Task<VeoAsyncJobResult> SubmitTextToVideoAsync(
            string prompt,
            string rapidApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken = default)
        {
            return SubmitTextToVideoAsync(prompt, rapidApiKey, veoEndpoint, durationSeconds: 0d, cancellationToken);
        }

        public Task<VeoAsyncJobResult> SubmitTextToVideoAsync(
            string prompt,
            string rapidApiKey,
            string veoEndpoint,
            double durationSeconds,
            CancellationToken cancellationToken = default)
        {
            var body = RapidApiGoogleVeoHelper.BuildGenerateBody(prompt, imageUrls: null, aspectRatio: "9:16");
            return SubmitGenerateAsync(body, rapidApiKey, veoEndpoint, cancellationToken);
        }

        public Task<VeoAsyncJobResult> SubmitVideoFromImageAsync(
            string sourceImageUrl,
            string prompt,
            string rapidApiKey,
            string veoEndpoint,
            double durationSeconds = 0d,
            CancellationToken cancellationToken = default)
        {
            var imageUrls = new List<string> { sourceImageUrl };
            var body = RapidApiGoogleVeoHelper.BuildGenerateBody(prompt, imageUrls, aspectRatio: "9:16");
            return SubmitGenerateAsync(body, rapidApiKey, veoEndpoint, cancellationToken);
        }

        /// <summary>
        /// RapidAPI Veo không có Flux context-image — trả ảnh nguồn ngay để pipeline image-to-video tiếp tục.
        /// </summary>
        public Task<VeoAsyncJobResult> SubmitContextImageAsync(
            string sourceImageUrl,
            string prompt,
            string rapidApiKey,
            string veoEndpoint,
            IList<string> referenceImageUrls,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCredentials(rapidApiKey, veoEndpoint);

            var src = (sourceImageUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(src))
            {
                throw new ArgumentException("Source image URL is required.", nameof(sourceImageUrl));
            }

            return Task.FromResult(new VeoAsyncJobResult
            {
                Status = "completed",
                ImageUrl = src,
                SourceEndpoint = RapidApiGoogleVeoHelper.NormalizeBaseUrl(veoEndpoint)
            });
        }

        public Task<VeoAsyncJobResult> PollJobAsync(
            string jobId,
            string statusPollUrl,
            string rapidApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken = default)
        {
            return PollStatusAsync(jobId, statusPollUrl, rapidApiKey, veoEndpoint, cancellationToken);
        }

        private async Task<string> WaitForMediaAsync(
            VeoAsyncJobResult submit,
            string rapidApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken)
        {
            if (submit == null)
            {
                throw new InvalidOperationException("RapidAPI Veo submit returned null.");
            }

            if (submit.IsTerminalFailure)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(submit.ErrorMessage)
                        ? "RapidAPI Veo job failed."
                        : submit.ErrorMessage);
            }

            if (!string.IsNullOrWhiteSpace(submit.VideoUrl))
            {
                return submit.VideoUrl;
            }

            if (string.IsNullOrWhiteSpace(submit.JobId))
            {
                throw new InvalidOperationException("RapidAPI Veo không trả video URL hoặc task_id.");
            }

            var deadline = DateTime.UtcNow.Add(DefaultGenerateTimeout);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

                var status = await PollStatusAsync(
                    submit.JobId,
                    submit.StatusPollUrl,
                    rapidApiKey,
                    veoEndpoint,
                    cancellationToken).ConfigureAwait(false);

                if (status.IsTerminalFailure)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(status.ErrorMessage)
                            ? "RapidAPI Veo job failed during polling."
                            : status.ErrorMessage);
                }

                if (!string.IsNullOrWhiteSpace(status.VideoUrl))
                {
                    return status.VideoUrl;
                }
            }

            throw new TimeoutException(
                "RapidAPI Veo polling timed out after " + DefaultGenerateTimeout.TotalMinutes.ToString("0") + " minutes.");
        }

        private async Task<VeoAsyncJobResult> SubmitGenerateAsync(
            object body,
            string rapidApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken)
        {
            ValidateCredentials(rapidApiKey, veoEndpoint);
            var generateUrl = RapidApiGoogleVeoHelper.BuildGenerateUrl(veoEndpoint);
            var host = RapidApiGoogleVeoHelper.ResolveRapidApiHost(veoEndpoint);
            var client = new ApiClient(generateUrl, SubmitTimeout, maxRetries: 0);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("x-rapidapi-key", rapidApiKey.Trim());
            request.AddHeader("x-rapidapi-host", host);
            request.AddJsonBody(body);

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = ParseJson(response.Content);
            EnsureSuccessCode(json, response.Content);

            var taskId = RapidApiGoogleVeoHelper.ExtractTaskId(json);
            var status = RapidApiGoogleVeoHelper.MapStatus(RapidApiGoogleVeoHelper.ExtractStatus(json));
            var baseUrl = RapidApiGoogleVeoHelper.NormalizeBaseUrl(veoEndpoint);
            var result = new VeoAsyncJobResult
            {
                JobId = taskId,
                Status = string.IsNullOrWhiteSpace(status) ? "processing" : status,
                StatusPollUrl = RapidApiGoogleVeoHelper.BuildStatusUrl(baseUrl, taskId),
                SourceEndpoint = baseUrl,
                VideoUrl = RapidApiGoogleVeoHelper.ExtractVideoUrl(json),
                ErrorMessage = RapidApiGoogleVeoHelper.ExtractErrorMessage(json)
            };

            if (RapidApiGoogleVeoHelper.IsFailedStatus(result.Status))
            {
                result.Status = "failed";
                if (string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result.ErrorMessage = "RapidAPI Veo submit failed.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.VideoUrl))
            {
                result.Status = "completed";
            }
            else if (string.IsNullOrWhiteSpace(result.JobId))
            {
                result.Status = "failed";
                result.ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "RapidAPI Veo không trả task_id."
                    : result.ErrorMessage;
            }

            return result;
        }

        private async Task<VeoAsyncJobResult> PollStatusAsync(
            string jobId,
            string statusPollUrl,
            string rapidApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken)
        {
            ValidateCredentials(rapidApiKey, veoEndpoint);
            var statusUrl = (statusPollUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(statusUrl) && !string.IsNullOrWhiteSpace(jobId))
            {
                statusUrl = RapidApiGoogleVeoHelper.BuildStatusUrl(veoEndpoint, jobId);
            }

            if (string.IsNullOrEmpty(statusUrl))
            {
                throw new InvalidOperationException("RapidAPI Veo status URL is required for polling.");
            }

            var host = RapidApiGoogleVeoHelper.ResolveRapidApiHost(veoEndpoint);
            var client = new ApiClient(statusUrl, PollTimeout, maxRetries: 1);
            var request = new RestRequest(string.Empty, Method.Get);
            request.AddHeader("x-rapidapi-key", rapidApiKey.Trim());
            request.AddHeader("x-rapidapi-host", host);

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = ParseJson(response.Content);
            EnsureSuccessCode(json, response.Content);

            var rawStatus = RapidApiGoogleVeoHelper.ExtractStatus(json);
            var result = new VeoAsyncJobResult
            {
                JobId = string.IsNullOrWhiteSpace(jobId) ? RapidApiGoogleVeoHelper.ExtractTaskId(json) : jobId,
                Status = RapidApiGoogleVeoHelper.MapStatus(rawStatus),
                StatusPollUrl = statusUrl,
                SourceEndpoint = RapidApiGoogleVeoHelper.NormalizeBaseUrl(veoEndpoint),
                VideoUrl = RapidApiGoogleVeoHelper.ExtractVideoUrl(json),
                ErrorMessage = RapidApiGoogleVeoHelper.ExtractErrorMessage(json)
            };

            if (RapidApiGoogleVeoHelper.IsFailedStatus(rawStatus))
            {
                result.Status = "failed";
                if (string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result.ErrorMessage = "RapidAPI Veo request failed (" + rawStatus + ").";
                }
            }
            else if (RapidApiGoogleVeoHelper.IsCompletedStatus(rawStatus))
            {
                result.Status = "completed";
                if (string.IsNullOrWhiteSpace(result.VideoUrl))
                {
                    result.Status = "failed";
                    result.ErrorMessage = "RapidAPI Veo completed nhưng thiếu video URL.";
                }
            }
            else if (string.IsNullOrWhiteSpace(result.Status))
            {
                result.Status = "processing";
            }

            return result;
        }

        private static JObject ParseJson(string content)
        {
            try
            {
                return JObject.Parse(content ?? "{}");
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException("RapidAPI Veo trả JSON không hợp lệ: " + ex.Message, ex);
            }
        }

        private static void EnsureSuccessCode(JObject json, string rawContent)
        {
            var code = json?["code"]?.ToString();
            if (string.IsNullOrWhiteSpace(code) || code.Equals("200", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var message = RapidApiGoogleVeoHelper.ExtractErrorMessage(json);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "RapidAPI Veo HTTP JSON code " + code;
            }

            throw new InvalidOperationException(message + (string.IsNullOrWhiteSpace(rawContent) ? string.Empty : "\r\n" + rawContent));
        }

        private static void ValidateCredentials(string rapidApiKey, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(rapidApiKey))
            {
                throw new InvalidOperationException("RapidAPI key is required (tab Cài đặt → Khóa Veo).");
            }

            if (string.IsNullOrWhiteSpace(endpoint)
                && string.IsNullOrWhiteSpace(RapidApiGoogleVeoHelper.NormalizeBaseUrl(endpoint)))
            {
                throw new InvalidOperationException("RapidAPI Veo endpoint is required.");
            }
        }
    }
}
