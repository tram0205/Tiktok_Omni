using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace tiktok_Omni.Services
{
    public class CaptchaSolveRequest
    {
        public string SiteKey { get; set; } = string.Empty;
        public string PageUrl { get; set; } = string.Empty;
    }

    public class CaptchaService
    {
        private const string TwoCaptchaBaseUrl = "https://2captcha.com";

        public async Task<decimal> GetBalanceAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("2Captcha API key is missing.");
            }

            var client = new RestClient(TwoCaptchaBaseUrl);
            var request = new RestRequest("/res.php", Method.Get);
            request.AddParameter("key", apiKey);
            request.AddParameter("action", "getbalance");
            request.AddParameter("json", "1");

            var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            var json = ParseJson(response.Content);
            if ((int?)json["status"] != 1)
            {
                throw new InvalidOperationException("2Captcha key check failed: " + (string)json["request"]);
            }

            var balanceText = ((string)json["request"] ?? string.Empty).Trim();
            if (!decimal.TryParse(balanceText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var balance))
            {
                throw new InvalidOperationException("2Captcha returned invalid balance: " + balanceText);
            }

            return balance;
        }

        public async Task<string> SolveCaptchaAsync(
            string apiKey,
            CaptchaSolveRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("2Captcha API key is missing.");
            }

            if (request == null ||
                string.IsNullOrWhiteSpace(request.SiteKey) ||
                string.IsNullOrWhiteSpace(request.PageUrl))
            {
                throw new InvalidOperationException("Captcha request is missing SiteKey or PageUrl.");
            }

            var client = new RestClient(TwoCaptchaBaseUrl);
            var createRequest = new RestRequest("/in.php", Method.Post);
            createRequest.AddParameter("key", apiKey);
            createRequest.AddParameter("method", "userrecaptcha");
            createRequest.AddParameter("googlekey", request.SiteKey);
            createRequest.AddParameter("pageurl", request.PageUrl);
            createRequest.AddParameter("json", "1");

            var createResponse = await client.ExecuteAsync(createRequest, cancellationToken).ConfigureAwait(false);
            var createJson = ParseJson(createResponse.Content);
            if ((int?)createJson["status"] != 1)
            {
                throw new InvalidOperationException("2Captcha create task failed: " + (string)createJson["request"]);
            }

            var captchaId = (string)createJson["request"];
            if (string.IsNullOrWhiteSpace(captchaId))
            {
                throw new InvalidOperationException("2Captcha returned empty captcha id.");
            }

            for (var i = 0; i < 24; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                var resultRequest = new RestRequest("/res.php", Method.Get);
                resultRequest.AddParameter("key", apiKey);
                resultRequest.AddParameter("action", "get");
                resultRequest.AddParameter("id", captchaId);
                resultRequest.AddParameter("json", "1");

                var resultResponse = await client.ExecuteAsync(resultRequest, cancellationToken).ConfigureAwait(false);
                var resultJson = ParseJson(resultResponse.Content);
                var status = (int?)resultJson["status"] ?? 0;
                var requestText = ((string)resultJson["request"] ?? string.Empty).Trim();

                if (status == 1 && !string.IsNullOrWhiteSpace(requestText))
                {
                    return requestText;
                }

                if (!string.Equals(requestText, "CAPCHA_NOT_READY", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("2Captcha solve failed: " + requestText);
                }
            }

            throw new TimeoutException("2Captcha timed out waiting for solution.");
        }

        private static JObject ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("2Captcha returned empty response.");
            }

            return JObject.Parse(json);
        }
    }
}
