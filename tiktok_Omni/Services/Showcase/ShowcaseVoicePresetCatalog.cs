using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseVoicePresetDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string PiperOnnxFileName { get; set; } = string.Empty;
        public string PiperOnnxDownloadUrl { get; set; } = string.Empty;
        public string PiperJsonDownloadUrl { get; set; } = string.Empty;
        public float PiperLengthScale { get; set; } = 1f;
        public float PiperNoiseScale { get; set; } = 0.667f;
        public float PiperNoiseW { get; set; } = 0.8f;
        /// <summary>Piper multi-speaker (vivos). Null nếu model 1 speaker (vais, 25hours).</summary>
        public int? PiperSpeakerId { get; set; }
        /// <summary>calm | intense | default</summary>
        public string ElevenLabsVoiceMood { get; set; } = "default";
        public bool ElevenLabsEmphaticHook { get; set; } = true;
        public bool ElevenLabsExpressiveBody { get; set; } = true;
        /// <summary>vi | en | zh | ja | ko | th — override ElevenLabs language_code theo video.</summary>
        public string ElevenLabsLanguageCode { get; set; } = string.Empty;
    }

    public static class ShowcaseVoicePresetCatalog
    {
        public const string DefaultPresetId = "female_south_young";

        private static readonly IReadOnlyList<ShowcaseVoicePresetDefinition> All = BuildAll();

        public static IReadOnlyList<ShowcaseVoicePresetDefinition> ListAll() => All;

        public static ShowcaseVoicePresetDefinition GetById(string id)
        {
            id = (id ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
            {
                return GetById(DefaultPresetId);
            }

            return All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                   ?? GetById(DefaultPresetId);
        }

        public static string FormatShortLabel(string presetId, string voiceAgeId = null, string voiceLanguageId = null)
        {
            return ShowcaseVoicePresetDimensions.FormatCompactSummary(presetId, voiceAgeId, voiceLanguageId);
        }

        private static string HfOnnx(string path) =>
            "https://huggingface.co/rhasspy/piper-voices/resolve/main/" + path;

        private static ShowcaseVoicePresetDefinition P(
            string id,
            string label,
            string onnxFile,
            string hfRelOnnx,
            float lengthScale,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            string elevenMood = "default",
            bool emphaticHook = true,
            bool expressiveBody = true,
            string elevenLanguage = "",
            int? piperSpeakerId = null)
        {
            var jsonRel = hfRelOnnx + ".json";
            return new ShowcaseVoicePresetDefinition
            {
                Id = id,
                Label = label,
                PiperOnnxFileName = onnxFile,
                PiperOnnxDownloadUrl = HfOnnx(hfRelOnnx),
                PiperJsonDownloadUrl = HfOnnx(jsonRel),
                PiperLengthScale = lengthScale,
                PiperNoiseScale = noiseScale,
                PiperNoiseW = noiseW,
                PiperSpeakerId = piperSpeakerId,
                ElevenLabsVoiceMood = elevenMood,
                ElevenLabsEmphaticHook = emphaticHook,
                ElevenLabsExpressiveBody = expressiveBody,
                ElevenLabsLanguageCode = (elevenLanguage ?? string.Empty).Trim().ToLowerInvariant()
            };
        }

        private static IReadOnlyList<ShowcaseVoicePresetDefinition> BuildAll()
        {
            const string vais = "vi/vi_VN/vais1000/medium/vi_VN-vais1000-medium.onnx";
            const string vivos = "vi/vi_VN/vivos/x_low/vi_VN-vivos-x_low.onnx";
            const string h25 = "vi/vi_VN/25hours_single/low/vi_VN-25hours_single-low.onnx";
            const string enUs = "en/en_US/lessac/medium/en_US-lessac-medium.onnx";
            const string enGb = "en/en_GB/alan/medium/en_GB-alan-medium.onnx";
            const string zhCn = "zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx";
            const string jaJp = "ja/ja_JP/natsumi/medium/ja_JP-natsumi-medium.onnx";

            return new List<ShowcaseVoicePresetDefinition>
            {
                P("female_south_young", "Nữ miền Nam — trẻ", "vi_VN-vais1000-medium.onnx", vais, 1.0f, 0.68f, 0.78f, "calm"),
                P("female_south_mature", "Nữ miền Nam — trung niên", "vi_VN-vais1000-medium.onnx", vais, 1.08f, 0.58f, 0.72f, "calm"),
                P("female_north_young", "Nữ miền Bắc — trẻ", "vi_VN-vais1000-medium.onnx", vais, 0.96f, 0.62f, 0.74f, "calm"),
                P("female_north_mature", "Nữ miền Bắc — trung niên", "vi_VN-vais1000-medium.onnx", vais, 1.06f, 0.55f, 0.7f, "calm"),
                P("female_central_young", "Nữ miền Trung — trẻ", "vi_VN-vais1000-medium.onnx", vais, 1.02f, 0.64f, 0.76f, "calm"),
                P("female_central_mature", "Nữ miền Trung — trung niên", "vi_VN-vais1000-medium.onnx", vais, 1.08f, 0.56f, 0.72f, "calm"),
                P("female_general_young", "Nữ — trẻ", "vi_VN-vais1000-medium.onnx", vais, 1.02f, 0.66f, 0.76f, "calm"),
                P("female_general_mature", "Nữ — trung niên", "vi_VN-vais1000-medium.onnx", vais, 1.08f, 0.55f, 0.72f, "calm"),
                P("female_south_child", "Nữ trẻ em — miền Nam", "vi_VN-vais1000-medium.onnx", vais, 1.16f, 0.72f, 0.82f, "calm"),
                P("female_north_child", "Nữ trẻ em — miền Bắc", "vi_VN-vais1000-medium.onnx", vais, 1.14f, 0.7f, 0.8f, "calm"),
                P("male_south_young", "Nam miền Nam — trẻ", "vi_VN-vivos-x_low.onnx", vivos, 0.94f, 0.65f, 0.75f, "intense"),
                P("male_south_mature", "Nam miền Nam — trung niên", "vi_VN-vivos-x_low.onnx", vivos, 1.05f, 0.55f, 0.7f, "intense"),
                P("male_north_young", "Nam miền Bắc — trẻ", "vi_VN-vivos-x_low.onnx", vivos, 0.9f, 0.58f, 0.72f, "intense"),
                P("male_central_young", "Nam miền Trung — trẻ", "vi_VN-vivos-x_low.onnx", vivos, 0.93f, 0.62f, 0.74f, "intense"),
                P("male_general_young", "Nam — trẻ", "vi_VN-vivos-x_low.onnx", vivos, 0.96f, 0.63f, 0.74f, "intense"),
                P("baby_boy", "Em bé — nam", "vi_VN-vais1000-medium.onnx", vais, 1.22f, 0.85f, 0.9f, "intense", true, false),
                P("baby_girl_south", "Em bé — nữ miền Nam", "vi_VN-vais1000-medium.onnx", vais, 1.18f, 0.78f, 0.86f, "calm", true, false),
                P("warm_deep", "Trầm — ấm", "vi_VN-vivos-x_low.onnx", vivos, 1.1f, 0.48f, 0.65f, "calm", false, true),
                P("ethereal", "Thanh — thoát", "vi_VN-vais1000-medium.onnx", vais, 0.98f, 0.66f, 0.82f, "calm", false, true),
                P("cheerful", "Vui vẻ — nhanh", "vi_VN-vais1000-medium.onnx", vais, 0.92f, 0.7f, 0.8f, "intense"),
                P("philosophy", "Quote — chậm rãi", "vi_VN-25hours_single-low.onnx", h25, 1.07f, 0.52f, 0.68f, "calm", false, true),
                P("sweet", "Ngọt ngào", "vi_VN-vais1000-medium.onnx", vais, 0.96f, 0.68f, 0.8f, "calm"),
                P("neutral_narrator", "Kể chuyện — trung tính", "vi_VN-25hours_single-low.onnx", h25, 1.0f, 0.62f, 0.75f, "default", false, false),
                P("en_us_narrator", "English (US)", "en_US-lessac-medium.onnx", enUs, 1.0f, 0.667f, 0.8f, "default", false, false, "en"),
                P("en_gb_narrator", "English (UK)", "en_GB-alan-medium.onnx", enGb, 1.02f, 0.62f, 0.78f, "default", false, false, "en"),
                P("zh_cn_narrator", "中文 (简体)", "zh_CN-huayan-medium.onnx", zhCn, 1.0f, 0.65f, 0.8f, "calm", false, true, "zh"),
                P("ja_jp_narrator", "日本語", "ja_JP-natsumi-medium.onnx", jaJp, 1.0f, 0.68f, 0.82f, "calm", false, true, "ja"),
                P("ko_kr_narrator", "한국어 (ElevenLabs)", string.Empty, enUs, 1.0f, 0.65f, 0.8f, "calm", false, true, "ko"),
                P("th_th_narrator", "ภาษาไทย (ElevenLabs)", string.Empty, enUs, 1.0f, 0.65f, 0.8f, "calm", false, true, "th")
            };
        }
    }
}
