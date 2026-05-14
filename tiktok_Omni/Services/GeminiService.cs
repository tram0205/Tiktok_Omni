using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace tiktok_Omni.Services
{
    public class GeminiService
    {
        /// <summary>
        /// Inline video payload limit for Gemini REST (conservative; larger files use text-only fallback).
        /// </summary>
        private const long MaxInlineVideoBytes = 12 * 1024 * 1024;

        public async Task<string> GenerateScriptAsync(
            string prompt,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                return await SendClaudeRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
            }

            return await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates a TikTok caption (Vietnamese, with hashtags) from video content when possible (Gemini multimodal),
        /// or from filename when video is too large or provider is not Gemini.
        /// </summary>
        public async Task<string> GenerateTikTokCaptionFromVideoAsync(
            string videoPath,
            string captionStyleKey,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                throw new FileNotFoundException("Video file not found.", videoPath);
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                return await GenerateTikTokCaptionTextOnlyAsync(
                    videoPath,
                    captionStyleKey,
                    model,
                    apiKey,
                    cancellationToken,
                    useAnthropic: true).ConfigureAwait(false);
            }

            var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
            var fileInfo = new FileInfo(videoPath);
            if (fileInfo.Length > MaxInlineVideoBytes)
            {
                return await GenerateTikTokCaptionTextOnlyAsync(
                    videoPath,
                    captionStyleKey,
                    safeModel,
                    apiKey,
                    cancellationToken,
                    useAnthropic: false).ConfigureAwait(false);
            }

            var mime = GuessVideoMime(videoPath);
            var bytes = await ReadAllBytesAsync(videoPath, cancellationToken).ConfigureAwait(false);
            var b64 = Convert.ToBase64String(bytes);

            var prompt = BuildTikTokCaptionPrompt(captionStyleKey, Path.GetFileName(videoPath), multimodal: true);
            var body = new JObject
            {
                ["contents"] = new JArray(
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray(
                            new JObject { ["text"] = prompt },
                            new JObject
                            {
                                ["inline_data"] = new JObject
                                {
                                    ["mime_type"] = mime,
                                    ["data"] = b64
                                }
                            })
                    })
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
            var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(6));

            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddQueryParameter("key", apiKey);
            request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            var text = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return SanitizeCaptionOutput(text);
            }

            var err = json["error"]?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(err))
            {
                // Fallback if API refused inline video
                return await GenerateTikTokCaptionTextOnlyAsync(
                    videoPath,
                    captionStyleKey,
                    safeModel,
                    apiKey,
                    cancellationToken,
                    useAnthropic: false).ConfigureAwait(false);
            }

            return await GenerateTikTokCaptionTextOnlyAsync(
                videoPath,
                captionStyleKey,
                safeModel,
                apiKey,
                cancellationToken,
                useAnthropic: false).ConfigureAwait(false);
        }

        public async Task<JObject> AnalyzeAffiliateVideoAsync(
            string videoPath,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                throw new FileNotFoundException("Video file not found.", videoPath);
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                throw new InvalidOperationException("Phân tích video chuyên sâu hiện chỉ hỗ trợ Gemini (provider 'gemini'). Hãy đổi AI Provider trong Cài đặt.");
            }

            var fileInfo = new FileInfo(videoPath);
            if (fileInfo.Length > MaxInlineVideoBytes)
            {
                throw new InvalidOperationException(
                    $"File video ({fileInfo.Length / 1024d / 1024d:0.0} MB) lớn hơn giới hạn inline {MaxInlineVideoBytes / 1024d / 1024d:0.0} MB của Gemini REST. Hãy nén nhỏ hơn nữa.");
            }

            var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
            var mime = GuessVideoMime(videoPath);
            var bytes = await ReadAllBytesAsync(videoPath, cancellationToken).ConfigureAwait(false);
            var b64 = Convert.ToBase64String(bytes);

            const string prompt =
                "Bạn là chuyên gia Marketing TikTok. Hãy xem kỹ video TikTok này và:\n" +
                "1) Trích xuất CHÍNH XÁC lời thoại / voiceover (chỉ phần audio do người nói, không kèm thuyết minh phụ).\n" +
                "2) Tóm tắt kịch bản từng cảnh theo timestamp (giây), súc tích.\n" +
                "3) QUAN TRỌNG NHẤT: chỉ rõ (theo số giây, ví dụ 7-12s) đoạn 'ăn tiền' — đoạn mẫu mặc đẹp nhất / hiệu quả sử dụng sản phẩm rõ nhất / cú twist gây chú ý — giúp chốt đơn cao.\n\n" +
                "Trả về DUY NHẤT một JSON hợp lệ (không markdown, không backticks), schema:\n" +
                "{\n" +
                "  \"voiceover\": \"<toàn bộ lời thoại tiếng Việt>\",\n" +
                "  \"script_summary\": \"<tóm tắt kịch bản theo từng cảnh, mỗi cảnh bắt đầu bằng [mm:ss-mm:ss]>\",\n" +
                "  \"money_shot\": \"<mô tả ngắn (1-3 câu) đoạn ăn tiền + khoảng thời gian chính xác>\"\n" +
                "}\n" +
                "Tất cả nội dung viết bằng tiếng Việt tự nhiên.";

            var body = new JObject
            {
                ["contents"] = new JArray(
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray(
                            new JObject { ["text"] = prompt },
                            new JObject
                            {
                                ["inline_data"] = new JObject
                                {
                                    ["mime_type"] = mime,
                                    ["data"] = b64
                                }
                            })
                    }),
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.25,
                    ["responseMimeType"] = "application/json"
                }
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
            var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(6));
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddQueryParameter("key", apiKey);
            request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

            RestResponse response;
            try
            {
                response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (IsLikelyGeminiQuotaOrRateLimit(ex.Message))
            {
                GeminiUsageTracker.Instance.RecordRateLimit429(ex.Message);
                throw;
            }

            var json = JObject.Parse(response.Content ?? "{}");
            var err = json["error"]?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(err))
            {
                if (IsLikelyGeminiQuotaOrRateLimit(err))
                {
                    GeminiUsageTracker.Instance.RecordRateLimit429(err);
                }

                throw new InvalidOperationException("Gemini từ chối phân tích video: " + err);
            }

            var text = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Gemini không trả về nội dung phân tích.");
            }

            var cleaned = SanitizeCaptionOutput(text);
            try
            {
                var parsed = JObject.Parse(cleaned);
                parsed["_raw"] = text;
                GeminiUsageTracker.Instance.RecordVideoAnalysisSuccess();
                return parsed;
            }
            catch
            {
                GeminiUsageTracker.Instance.RecordVideoAnalysisSuccess();
                return new JObject
                {
                    ["voiceover"] = string.Empty,
                    ["script_summary"] = cleaned,
                    ["money_shot"] = string.Empty,
                    ["_raw"] = text
                };
            }
        }

        private static bool IsLikelyGeminiQuotaOrRateLimit(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.IndexOf("429", StringComparison.Ordinal) >= 0
                   || message.IndexOf("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.ReadAllBytes(path);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static string GuessVideoMime(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
            switch (ext)
            {
                case ".webm":
                    return "video/webm";
                case ".mov":
                    return "video/quicktime";
                case ".mkv":
                    return "video/x-matroska";
                default:
                    return "video/mp4";
            }
        }

        private async Task<string> GenerateTikTokCaptionTextOnlyAsync(
            string videoPath,
            string captionStyleKey,
            string model,
            string apiKey,
            CancellationToken cancellationToken,
            bool useAnthropic)
        {
            var fileName = Path.GetFileName(videoPath) ?? "video.mp4";
            var prompt = BuildTikTokCaptionPrompt(captionStyleKey, fileName, multimodal: false);
            var raw = await GenerateScriptAsync(prompt, useAnthropic ? "claude" : "gemini", apiKey, model, cancellationToken)
                .ConfigureAwait(false);
            return SanitizeCaptionOutput(raw);
        }

        private static string BuildTikTokCaptionPrompt(string captionStyleKey, string fileLabel, bool multimodal)
        {
            var styleVi = DescribeCaptionStyle(captionStyleKey);
            var mediaHint = multimodal
                ? "Bạn có thể xem video đính kèm."
                : $"Không có video — chỉ dựa trên tên file: \"{fileLabel}\". Hãy suy đoán chủ đề hợp lý từ tên file.";

            return "Bạn là copywriter TikTok tiếng Việt.\n" +
                   mediaHint + "\n" +
                   "Phong cách caption: " + styleVi + "\n\n" +
                   "Yêu cầu:\n" +
                   "- Viết MỘT caption duy nhất cho TikTok, tiếng Việt tự nhiên.\n" +
                   "- Thêm 3–8 hashtag phù hợp (#...), có thể có emoji nhẹ (0–3), không spam.\n" +
                   "- Giới hạn thực tế ~2200 ký tự; ưu tiên ngắn gọn, mạch lạc.\n" +
                   "Trả về DUY NHẤT nội dung caption và hashtag, không giải thích, không markdown, không bọc dấu ngoặc kép.";
        }

        private static string DescribeCaptionStyle(string key)
        {
            var k = (key ?? string.Empty).Trim().ToLowerInvariant();
            if (k == "philosophy" || k.Contains("triết") || k.Contains("phil")) return "Triết lý — insight, ẩn dụ nhẹ, tone suy ngẫm, không sến.";
            if (k == "humor" || k.Contains("hài") || k.Contains("humor")) return "Hài hước — twist, chơi chữ vừa phải, phù hợp TikTok, không toxic.";
            if (k == "debate" || k.Contains("tranh") || k.Contains("hỏi")) return "Câu hỏi / gây tranh luận — mở đầu bằng câu hỏi hoặc ý kiến gây tương tác, không thù hận.";
            if (k == "knowledge") return "Kiến thức — fact/tip hữu ích, giọng chia sẻ chuyên gia nhẹ nhàng.";
            return "Kiến thức — fact/tip hữu ích, giọng chia sẻ chuyên gia nhẹ nhàng.";
        }

        private static string SanitizeCaptionOutput(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var text = raw.Trim();
            text = text.Trim('"', '\'', '`');
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var idx = text.LastIndexOf("```", StringComparison.Ordinal);
                if (idx > 3)
                {
                    text = text.Substring(3, idx - 3).Trim();
                }
            }

            return text;
        }

        private static async Task<string> SendGeminiRequestAsync(string prompt, string model, string apiKey, CancellationToken cancellationToken)
        {
            var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
            var apiClient = new ApiClient(endpoint);

            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddQueryParameter("key", apiKey);
            request.AddJsonBody(new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            });

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                   ?? string.Empty;
        }

        private static async Task<string> SendClaudeRequestAsync(string prompt, string model, string apiKey, CancellationToken cancellationToken)
        {
            var safeModel = string.IsNullOrWhiteSpace(model) ? "claude-3-7-sonnet-latest" : model.Trim();
            var apiClient = new ApiClient("https://api.anthropic.com/v1/messages");

            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("x-api-key", apiKey);
            request.AddHeader("anthropic-version", "2023-06-01");
            request.AddJsonBody(new
            {
                model = safeModel,
                max_tokens = 900,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            });

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["content"]?[0]?["text"]?.ToString()
                   ?? string.Empty;
        }
    }
}
