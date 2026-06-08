using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services.Mascot
{
    public sealed class MascotGeminiScriptService
    {
        private readonly GeminiService _gemini = new GeminiService();

        public async Task<List<MascotScene>> GenerateScenesAsync(string request, AppSettings settings, CancellationToken cancellationToken)
        {
            var topic = (request ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(topic)) throw new InvalidOperationException("Yeu cau trong.");
            if (string.IsNullOrWhiteSpace(settings?.AiApiKey)) throw new InvalidOperationException("Chua cau hinh AI API key.");

            var prompt =
                "Bạn là chuyên gia viết kịch bản video. Viết kịch bản 4-5 cảnh dựa trên chủ đề: " + topic +
                ". Trả về MẢNG JSON thuần túy, không có markdown. Mỗi object có: 'scene' (int), 'voiceover' (string - thoại tiếng Việt, đọc 5-8s), 'image_prompt' (string - tiếng Anh, tả góc máy và hành động của mascot, tuyệt đối không tả mascot đang nói chuyện).";

            var raw = await _gemini.GenerateScriptAsync(prompt, settings.AiProvider, settings.AiApiKey, settings.AiModel, cancellationToken).ConfigureAwait(false);
            var json = ExtractJsonArray(raw);
            var dtos = JsonConvert.DeserializeObject<List<MascotSceneGeminiDto>>(json);
            if (dtos == null || dtos.Count == 0) throw new InvalidOperationException("Gemini khong tra ve mang canh hop le.");

            return dtos.Select((d, i) => new MascotScene
            {
                Order = d.scene > 0 ? d.scene : i + 1,
                Voiceover = (d.voiceover ?? string.Empty).Trim(),
                ImagePrompt = (d.image_prompt ?? string.Empty).Trim()
            }).Where(s => !string.IsNullOrWhiteSpace(s.Voiceover) || !string.IsNullOrWhiteSpace(s.ImagePrompt)).OrderBy(s => s.Order).ToList();
        }

        private static string ExtractJsonArray(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                text = Regex.Replace(text, "^```[a-zA-Z]*\\s*", string.Empty, RegexOptions.Multiline);
                text = Regex.Replace(text, "```\\s*$", string.Empty, RegexOptions.Multiline).Trim();
            }
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start >= 0 && end > start) return text.Substring(start, end - start + 1);
            var wrapped = JObject.Parse(text);
            if (wrapped["scenes"] is JArray arr) return arr.ToString(Formatting.None);
            throw new InvalidOperationException("Khong tach duoc JSON array tu Gemini.");
        }
    }
}