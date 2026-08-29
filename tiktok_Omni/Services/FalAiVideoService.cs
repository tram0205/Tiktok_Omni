using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace tiktok_Omni.Services
{
    /// <summary>Gọi FAL.AI queue API — Veo 3.1 text/image-to-video, Flux context image.</summary>
    public sealed class FalAiVideoService
    {
        private static readonly TimeSpan SubmitTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan DefaultGenerateTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(8);

        public Task<string> GenerateVideoAsync(
            string prompt,
            string falApiKey,
            string falEndpoint,
            CancellationToken cancellationToken = default)
        {
            return GenerateVideoAsync(prompt, falApiKey, falEndpoint, durationSeconds: 0d, cancellationToken);
        }

        public async Task<string> GenerateVideoAsync(
            string prompt,
            string falApiKey,
            string falEndpoint,
            double durationSeconds,
            CancellationToken cancellationToken = default)
        {
            var submit = await SubmitTextToVideoAsync(
                prompt,
                falApiKey,
                falEndpoint,
                durationSeconds,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(submit.VideoUrl))
            {
                return submit.VideoUrl;
            }

            return await WaitForMediaAsync(
                submit,
                falApiKey,
                expectVideo: true,
                DefaultGenerateTimeout,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<VeoAsyncJobResult> SubmitTextToVideoAsync(
            string prompt,
            string falApiKey,
            string falEndpoint,
            CancellationToken cancellationToken = default)
        {
            return SubmitTextToVideoAsync(prompt, falApiKey, falEndpoint, durationSeconds: 0d, cancellationToken);
        }

        public Task<VeoAsyncJobResult> SubmitTextToVideoAsync(
            string prompt,
            string falApiKey,
            string falEndpoint,
            double durationSeconds,
            CancellationToken cancellationToken = default)
        {
            var queueUrl = FalAiVideoHelper.NormalizeQueueEndpoint(falEndpoint);
            var body = FalAiVideoHelper.BuildTextToVideoBody(prompt, durationSeconds, generateAudio: false);
            return QueueSubmitAsync(queueUrl, falApiKey, body, cancellationToken);
        }

        public Task<VeoAsyncJobResult> SubmitVideoFromImageAsync(
            string sourceImageUrl,
            string prompt,
            string falApiKey,
            string falEndpoint,
            double durationSeconds = 0d,
            CancellationToken cancellationToken = default)
        {
            var queueUrl = FalAiVideoHelper.ResolveImageToVideoEndpoint(falEndpoint);
            var body = FalAiVideoHelper.BuildImageToVideoBody(prompt, sourceImageUrl, durationSeconds, generateAudio: false);
            return QueueSubmitAsync(queueUrl, falApiKey, body, cancellationToken);
        }

        public Task<VeoAsyncJobResult> SubmitContextImageAsync(
            string sourceImageUrl,
            string prompt,
            string falApiKey,
            string falEndpoint,
            IList<string> referenceImageUrls,
            CancellationToken cancellationToken = default)
        {
            var queueUrl = FalAiVideoHelper.ResolveContextImageEndpoint(falEndpoint);
            var body = FalAiVideoHelper.BuildContextImageBody(prompt, sourceImageUrl);
            return QueueSubmitAsync(queueUrl, falApiKey, body, cancellationToken);
        }

        public Task<VeoAsyncJobResult> PollJobAsync(
            string jobId,
            string statusPollUrl,
            string falApiKey,
            string falEndpoint,
            CancellationToken cancellationToken = default)
        {
            return PollQueueAsync(jobId, statusPollUrl, falApiKey, falEndpoint, cancellationToken);
        }

        private async Task<string> WaitForMediaAsync(
            VeoAsyncJobResult submit,
            string falApiKey,
            bool expectVideo,
            TimeSpan maxWait,
            CancellationToken cancellationToken)
        {
            if (submit == null)
            {
                throw new InvalidOperationException("FAL submit returned null.");
            }

            if (submit.IsTerminalFailure)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(submit.ErrorMessage) ? "FAL job failed." : submit.ErrorMessage);
            }

            var immediate = expectVideo ? submit.VideoUrl : submit.ImageUrl;
            if (!string.IsNullOrWhiteSpace(immediate))
            {
                return immediate;
            }

            if (string.IsNullOrWhiteSpace(submit.JobId))
            {
                throw new InvalidOperationException("FAL không trả media URL hoặc request_id.");
            }

            var deadline = DateTime.UtcNow.Add(maxWait);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

                var status = await PollQueueAsync(
                    submit.JobId,
                    submit.StatusPollUrl,
                    falApiKey,
                    submit.SourceEndpoint,
                    cancellationToken).ConfigureAwait(false);

                if (status.IsTerminalFailure)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(status.ErrorMessage)
                            ? "FAL job failed during polling."
                            : status.ErrorMessage);
                }

                var url = expectVideo ? status.VideoUrl : status.ImageUrl;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }

            throw new TimeoutException("FAL job polling timed out after " + maxWait.TotalMinutes.ToString("0") + " minutes.");
        }

        private async Task<VeoAsyncJobResult> QueueSubmitAsync(
            string queueUrl,
            string falApiKey,
            object body,
            CancellationToken cancellationToken)
        {
            ValidateCredentials(falApiKey, queueUrl);
            var client = new ApiClient(queueUrl, SubmitTimeout, maxRetries: 1);
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", FalAiVideoHelper.FormatAuthorizationHeader(falApiKey));
            request.AddJsonBody(body);

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = ParseJson(response.Content);
            var result = MapQueueSubmit(json, queueUrl);

            var inlineVideo = FalAiVideoHelper.ExtractVideoUrl(json);
            var inlineImage = FalAiVideoHelper.ExtractImageUrl(json);
            if (!string.IsNullOrWhiteSpace(inlineVideo))
            {
                result.VideoUrl = inlineVideo;
                result.Status = "completed";
            }
            else if (!string.IsNullOrWhiteSpace(inlineImage))
            {
                result.ImageUrl = inlineImage;
                result.Status = "completed";
            }

            return result;
        }

        private async Task<VeoAsyncJobResult> PollQueueAsync(
            string jobId,
            string statusPollUrl,
            string falApiKey,
            string falEndpoint,
            CancellationToken cancellationToken)
        {
            ValidateCredentials(falApiKey, falEndpoint);
            var statusUrl = (statusPollUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(statusUrl) && !string.IsNullOrWhiteSpace(jobId))
            {
                statusUrl = FalAiVideoHelper.BuildStatusUrlFromQueueEndpoint(falEndpoint, jobId);
            }

            if (string.IsNullOrEmpty(statusUrl))
            {
                throw new InvalidOperationException("FAL status URL is required for polling.");
            }

            var client = new ApiClient(statusUrl, PollTimeout, maxRetries: 1);
            var request = new RestRequest(string.Empty, Method.Get);
            request.AddHeader("Authorization", FalAiVideoHelper.FormatAuthorizationHeader(falApiKey));
            request.AddQueryParameter("logs", "1");

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = ParseJson(response.Content);
            var status = json["status"]?.ToString() ?? string.Empty;
            var result = new VeoAsyncJobResult
            {
                JobId = string.IsNullOrWhiteSpace(jobId) ? FalAiVideoHelper.ExtractRequestId(json) : jobId,
                Status = MapFalStatus(status),
                StatusPollUrl = statusUrl,
                SourceEndpoint = FalAiVideoHelper.NormalizeQueueEndpoint(falEndpoint),
                ErrorMessage = json["error"]?.ToString() ?? string.Empty
            };

            if (FalAiVideoHelper.IsFalFailedStatus(status))
            {
                result.Status = "failed";
                if (string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result.ErrorMessage = "FAL request failed (" + status + ").";
                }

                return result;
            }

            if (!FalAiVideoHelper.IsFalCompletedStatus(status))
            {
                return result;
            }

            var responseUrl = FalAiVideoHelper.ExtractResponseUrl(json);
            if (string.IsNullOrWhiteSpace(responseUrl) && !string.IsNullOrWhiteSpace(result.JobId))
            {
                responseUrl = FalAiVideoHelper.BuildResponseUrlFromQueueEndpoint(result.SourceEndpoint, result.JobId);
            }

            if (string.IsNullOrWhiteSpace(responseUrl))
            {
                result.Status = "failed";
                result.ErrorMessage = "FAL completed nhưng thiếu response_url.";
                return result;
            }

            var output = await FetchResultAsync(responseUrl, falApiKey, cancellationToken).ConfigureAwait(false);
            result.VideoUrl = output.VideoUrl;
            result.ImageUrl = output.ImageUrl;
            result.Status = "completed";
            if (string.IsNullOrWhiteSpace(result.VideoUrl) && string.IsNullOrWhiteSpace(result.ImageUrl))
            {
                result.Status = "failed";
                result.ErrorMessage = "FAL completed nhưng JSON không có video/image URL.";
            }

            return result;
        }

        private static async Task<VeoAsyncJobResult> FetchResultAsync(
            string responseUrl,
            string falApiKey,
            CancellationToken cancellationToken)
        {
            var client = new ApiClient(responseUrl.Trim(), PollTimeout, maxRetries: 1);
            var request = new RestRequest(string.Empty, Method.Get);
            request.AddHeader("Authorization", FalAiVideoHelper.FormatAuthorizationHeader(falApiKey));
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = ParseJson(response.Content);
            return new VeoAsyncJobResult
            {
                VideoUrl = FalAiVideoHelper.ExtractVideoUrl(json),
                ImageUrl = FalAiVideoHelper.ExtractImageUrl(json)
            };
        }

        private static VeoAsyncJobResult MapQueueSubmit(JObject json, string queueUrl)
        {
            var requestId = FalAiVideoHelper.ExtractRequestId(json);
            var statusUrl = FalAiVideoHelper.ExtractStatusUrl(json);
            if (string.IsNullOrWhiteSpace(statusUrl) && !string.IsNullOrWhiteSpace(requestId))
            {
                statusUrl = FalAiVideoHelper.BuildStatusUrlFromQueueEndpoint(queueUrl, requestId);
            }

            return new VeoAsyncJobResult
            {
                JobId = requestId,
                Status = MapFalStatus(json["status"]?.ToString() ?? "in_queue"),
                StatusPollUrl = statusUrl,
                SourceEndpoint = FalAiVideoHelper.NormalizeQueueEndpoint(queueUrl)
            };
        }

        private static string MapFalStatus(string falStatus)
        {
            if (FalAiVideoHelper.IsFalCompletedStatus(falStatus))
            {
                return "completed";
            }

            if (FalAiVideoHelper.IsFalFailedStatus(falStatus))
            {
                return "failed";
            }

            if (FalAiVideoHelper.IsFalProcessingStatus(falStatus))
            {
                return "processing";
            }

            return FalAiVideoHelper.NormalizeStatus(falStatus);
        }

        private static JObject ParseJson(string content)
        {
            try
            {
                return JObject.Parse(content ?? "{}");
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException("FAL API trả JSON không hợp lệ: " + ex.Message, ex);
            }
        }

        private static void ValidateCredentials(string falApiKey, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(falApiKey))
            {
                throw new InvalidOperationException("FAL.AI API key is required.");
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("FAL.AI endpoint is required.");
            }
        }
    }
}
