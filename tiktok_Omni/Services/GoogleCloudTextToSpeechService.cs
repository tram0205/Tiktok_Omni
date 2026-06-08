using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Google Cloud Text-to-Speech REST + fallback công cộng (translate TTS) khi không có key Cloud TTS.</summary>
    public static class GoogleCloudTextToSpeechService
    {
        private const string CloudTtsUrl = "https://texttospeech.googleapis.com/v1/text:synthesize";
        private const string DefaultVoiceName = "vi-VN-Neural2-A";

        public static async Task SynthesizeVietnameseFemaleToMp3Async(
            string text,
            string outputMp3Path,
            string apiKey,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                throw new ArgumentException("Hook text is empty.", nameof(text));
            }

            if (string.IsNullOrWhiteSpace(outputMp3Path))
            {
                throw new ArgumentException("Output path is required.", nameof(outputMp3Path));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputMp3Path)) ?? ".");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    await SynthesizeViaCloudApiAsync(line, outputMp3Path, apiKey.Trim(), cancellationToken)
                        .ConfigureAwait(false);
                    if (File.Exists(outputMp3Path) && new FileInfo(outputMp3Path).Length > 400)
                    {
                        log?.Invoke("[Voiceover] Google Cloud TTS → " + Path.GetFileName(outputMp3Path));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke("[Voiceover] Cloud TTS lỗi, thử TTS công cộng: " + ex.Message);
                }
            }

            await SynthesizeViaPublicGoogleTtsAsync(line, outputMp3Path, log, cancellationToken).ConfigureAwait(false);
            log?.Invoke("[Voiceover] TTS công cộng (test) → " + Path.GetFileName(outputMp3Path));
        }

        private static async Task SynthesizeViaCloudApiAsync(
            string text,
            string outputMp3Path,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var body = new JObject
            {
                ["input"] = new JObject { ["text"] = text },
                ["voice"] = new JObject
                {
                    ["languageCode"] = "vi-VN",
                    ["name"] = DefaultVoiceName,
                    ["ssmlGender"] = "FEMALE"
                },
                ["audioConfig"] = new JObject
                {
                    ["audioEncoding"] = "MP3",
                    ["speakingRate"] = 0.95d,
                    ["pitch"] = -1.0d
                }
            };

            var url = CloudTtsUrl + "?key=" + Uri.EscapeDataString(apiKey);
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = 120000;
            var payload = body.ToString(Newtonsoft.Json.Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(payload);
            request.ContentLength = bytes.Length;

            using (var stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            }

            string responseText;
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, TextFileEncoding.Utf8))
            {
                responseText = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var json = JObject.Parse(responseText ?? "{}");
            var err = json["error"]?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(err))
            {
                throw new InvalidOperationException(err);
            }

            var b64 = json["audioContent"]?.ToString();
            if (string.IsNullOrWhiteSpace(b64))
            {
                throw new InvalidOperationException("Cloud TTS không trả audioContent.");
            }

            var audio = Convert.FromBase64String(b64);
            if (audio.Length < 400)
            {
                throw new InvalidOperationException("Cloud TTS trả file quá nhỏ.");
            }

            File.WriteAllBytes(outputMp3Path, audio);
        }

        private static async Task SynthesizeViaPublicGoogleTtsAsync(
            string text,
            string outputMp3Path,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var ttsUrl =
                "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=vi&q=" +
                HttpUtility.UrlEncode(text);

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "Mozilla/5.0");
                cancellationToken.Register(() => client.CancelAsync());
                var data = await client.DownloadDataTaskAsync(ttsUrl).ConfigureAwait(false);
                if (data == null || data.Length < 400)
                {
                    throw new InvalidOperationException("TTS công cộng trả dữ liệu quá nhỏ.");
                }

                File.WriteAllBytes(outputMp3Path, data);
            }
        }
    }
}
