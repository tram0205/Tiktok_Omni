using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Nhận diện giọng nói qua OpenAI Whisper API — timestamp từng từ.</summary>
    public sealed class WhisperTranscriptionService
    {
        private const string Endpoint = "https://api.openai.com/v1/audio/transcriptions";
        private const long MaxUploadBytes = 25L * 1024L * 1024L;

        public async Task<List<WordTimestamp>> GetWordTimestampsAsync(
            string audioFilePath,
            string openAiApiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Không tìm thấy file audio cho Whisper.", audioFilePath ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(openAiApiKey))
            {
                throw new ArgumentException("OpenAI API key trống.", nameof(openAiApiKey));
            }

            var fileInfo = new FileInfo(audioFilePath);
            if (fileInfo.Length > MaxUploadBytes)
            {
                throw new InvalidOperationException(
                    "File audio vượt 25MB — Whisper API không hỗ trợ. Hãy rút ngắn hoặc nén audio.");
            }

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
            using (var form = new MultipartFormDataContent())
            {
                var bytes = File.ReadAllBytes(audioFilePath);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetAudioContentType(audioFilePath));
                form.Add(fileContent, "file", Path.GetFileName(audioFilePath));
                form.Add(new StringContent("whisper-1"), "model");
                form.Add(new StringContent("verbose_json"), "response_format");
                form.Add(new StringContent("word"), "timestamp_granularities[]");

                using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey.Trim());
                    request.Content = form;

                    using (var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Whisper API HTTP " + (int)response.StatusCode + ": " + Truncate(body, 500));
                        }

                        return ParseWordTimestamps(body);
                    }
                }
            }
        }

        private static List<WordTimestamp> ParseWordTimestamps(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<WordTimestamp>();
            }

            var root = JObject.Parse(json);
            var words = root["words"] as JArray;
            if (words == null || words.Count == 0)
            {
                return new List<WordTimestamp>();
            }

            var result = new List<WordTimestamp>(words.Count);
            foreach (var token in words)
            {
                if (token == null || token.Type != JTokenType.Object)
                {
                    continue;
                }

                var obj = (JObject)token;
                var rawWord = (obj["word"] ?? obj["text"])?.ToString() ?? string.Empty;
                var text = rawWord.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var startSec = ReadSeconds(obj["start"]);
                var endSec = ReadSeconds(obj["end"]);
                if (endSec <= startSec)
                {
                    endSec = startSec + 0.05d;
                }

                result.Add(new WordTimestamp
                {
                    Text = text,
                    StartTimeMs = Math.Round(startSec * 1000d, 2),
                    EndTimeMs = Math.Round(endSec * 1000d, 2)
                });
            }

            return result;
        }

        private static double ReadSeconds(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return 0d;
            }

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                return token.Value<double>();
            }

            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : 0d;
        }

        private static string GetAudioContentType(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            switch (ext)
            {
                case ".wav":
                    return "audio/wav";
                case ".m4a":
                    return "audio/m4a";
                case ".mp4":
                    return "video/mp4";
                case ".webm":
                    return "audio/webm";
                case ".mpeg":
                case ".mpga":
                    return "audio/mpeg";
                default:
                    return "audio/mpeg";
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, max) + "...";
        }
    }
}
