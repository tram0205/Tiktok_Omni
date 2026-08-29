using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;

namespace tiktok_Omni.Services
{
    public class ApiClient
    {
        private readonly RestClient _client;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;

        public ApiClient(string baseUrl, TimeSpan? timeout = null, int maxRetries = 2)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            }

            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            var options = new RestClientOptions(baseUrl)
            {
                Timeout = _timeout
            };
            _client = new RestClient(options);
            _maxRetries = Math.Max(0, maxRetries);
        }

        public async Task<RestResponse> SendAsync(RestRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RestResponse lastResponse = null;

            for (var attempt = 0; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lastResponse = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

                    if (lastResponse.IsSuccessful)
                    {
                        return lastResponse;
                    }

                    if (!IsTransientStatusCode(lastResponse.StatusCode) || attempt == _maxRetries)
                    {
                        if ((int)lastResponse.StatusCode == 429)
                        {
                            throw new InvalidOperationException(BuildQuotaErrorMessage(lastResponse));
                        }

                        var message = BuildErrorMessage(lastResponse, attempt);
                        throw new InvalidOperationException(message);
                    }
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (attempt == _maxRetries)
                    {
                        throw new TimeoutException($"Request timed out after {_timeout.TotalSeconds:F0} seconds.");
                    }
                }
                catch (Exception) when (attempt < _maxRetries)
                {
                    // retry on unexpected network exceptions
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException(BuildErrorMessage(lastResponse, _maxRetries));
        }

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout ||
                   (int)statusCode >= 500;
        }

        private static string BuildErrorMessage(RestResponse response, int attempt)
        {
            if (response == null)
            {
                return $"API request failed after {attempt + 1} attempts with no response.";
            }

            var status = (int)response.StatusCode;
            var statusLine = status == 0
                ? "No HTTP status code returned."
                : $"{status} {response.StatusCode}";

            var detail = string.IsNullOrWhiteSpace(response.ErrorMessage)
                ? response.Content
                : response.ErrorMessage;

            return $"API request failed after {attempt + 1} attempts. {statusLine} Details: {detail}";
        }

        private static string BuildQuotaErrorMessage(RestResponse response)
        {
            var detail = string.IsNullOrWhiteSpace(response?.Content)
                ? (response?.ErrorMessage ?? string.Empty)
                : response.Content;
            if (detail.IndexOf("GenerateRequestsPerDay", StringComparison.OrdinalIgnoreCase) >= 0
                || detail.IndexOf("free_tier", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Gemini hết quota free tier (429). Đợi reset ngày, đổi model, hoặc bật billing. "
                       + VideoReupRemixService.FormatGeminiQuotaShortMessage();
            }

            return "Gemini rate limit (429). Thử lại sau vài giây. "
                   + TrimApiDetail(detail);
        }

        private static string TrimApiDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return string.Empty;
            }

            return detail.Length <= 240 ? detail : detail.Substring(0, 237) + "…";
        }
    }
}
