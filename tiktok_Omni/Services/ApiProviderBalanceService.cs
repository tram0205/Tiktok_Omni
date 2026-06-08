using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    public sealed class ApiProviderBalanceService
    {
        private readonly HttpClient _httpClient;

        public ApiProviderBalanceService(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        }

        public async Task<ProviderBalanceSnapshot> CheckAllAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            settings = settings ?? new AppSettings();
            var eleven = await CheckElevenLabsAsync(settings.TtsApiKey, cancellationToken).ConfigureAwait(false);
            var gemini = await CheckGeminiAsync(settings.AiApiKey, cancellationToken).ConfigureAwait(false);
            return new ProviderBalanceSnapshot { ElevenLabs = eleven, Gemini = gemini };
        }

        public async Task<ElevenLabsBalanceInfo> CheckElevenLabsAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new ElevenLabsBalanceInfo { Ok = false, Summary = "ElevenLabs: chua cau hinh API key." };
            }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/user/subscription"))
                {
                    request.Headers.TryAddWithoutValidation("xi-api-key", apiKey.Trim());
                    using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            return new ElevenLabsBalanceInfo { Ok = false, Summary = "ElevenLabs: loi HTTP " + (int)response.StatusCode };
                        }

                        var json = JObject.Parse(body);
                        var used = json["character_count"]?.Value<long?>() ?? 0;
                        var limit = json["character_limit"]?.Value<long?>() ?? 0;
                        var remaining = Math.Max(0, limit - used);
                        var tier = json["tier"]?.ToString() ?? "-";
                        return new ElevenLabsBalanceInfo
                        {
                            Ok = true,
                            CharactersUsed = used,
                            CharacterLimit = limit,
                            CharactersRemaining = remaining,
                            Tier = tier,
                            Summary = "ElevenLabs: con ~" + remaining.ToString("N0") + " ky tu / " + limit.ToString("N0") + " (" + tier + ")"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ElevenLabsBalanceInfo { Ok = false, Summary = "ElevenLabs: " + ex.Message };
            }
        }

        public async Task<GeminiBalanceInfo> CheckGeminiAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new GeminiBalanceInfo { Ok = false, Summary = "Gemini: chua cau hinh API key." };
            }

            try
            {
                var url = "https://generativelanguage.googleapis.com/v1beta/models?key=" + Uri.EscapeDataString(apiKey.Trim());
                using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return new GeminiBalanceInfo { Ok = true, Summary = "Gemini: key hop le - xem billing tai Google AI Studio" };
                    }

                    return new GeminiBalanceInfo { Ok = false, Summary = "Gemini: key loi HTTP " + (int)response.StatusCode };
                }
            }
            catch (Exception ex)
            {
                return new GeminiBalanceInfo { Ok = false, Summary = "Gemini: " + ex.Message };
            }
        }
    }

    public sealed class ProviderBalanceSnapshot
    {
        public ElevenLabsBalanceInfo ElevenLabs { get; set; } = new ElevenLabsBalanceInfo();
        public GeminiBalanceInfo Gemini { get; set; } = new GeminiBalanceInfo();
    }

    public sealed class ElevenLabsBalanceInfo
    {
        public bool Ok { get; set; }
        public long CharactersUsed { get; set; }
        public long CharacterLimit { get; set; }
        public long CharactersRemaining { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class GeminiBalanceInfo
    {
        public bool Ok { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}