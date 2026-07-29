using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseTtsRenderOptions
    {
        public TtsEngineKind HookEngine { get; set; } = TtsEngineKind.EdgeTts;

        public TtsEngineKind BodyEngine { get; set; } = TtsEngineKind.EdgeTts;

        /// <summary>Legacy — đồng bộ thân; batch «Hỏi khi tạo» gán cả hook + thân.</summary>
        public TtsEngineKind Engine
        {
            get => BodyEngine;
            set
            {
                BodyEngine = value;
                HookEngine = value;
            }
        }

        public string VoicePresetId { get; set; } = ShowcaseVoicePresetCatalog.DefaultPresetId;

        public string VoiceAgeId { get; set; } = ShowcaseVoicePresetDimensions.Age.Adult26_35;

        public string VoiceLanguageId { get; set; } = ShowcaseVoicePresetDimensions.Language.ViGeneral;

        /// <summary>Giọng ElevenLabs cố định (ElevenVoicePersonaCatalog) — hook. Rỗng = dùng preset/mood cũ.</summary>
        public string HookElevenPersona { get; set; } = ElevenVoicePersonaCatalog.None;

        /// <summary>Tone giọng hook (ShowcaseVoicePresetDimensions.Tone.*) — Tự nhiên = không ghi đè voice_settings.</summary>
        public string HookVoiceToneId { get; set; } = ShowcaseVoicePresetDimensions.Tone.Natural;

        public int HookElevenCustomStabilityPercent { get; set; } = 50;

        public int HookElevenCustomSimilarityPercent { get; set; } = 75;

        public int HookElevenCustomStylePercent { get; set; } = 15;

        /// <summary>HookStyleCatalog key — Edge prosody; Eleven v3 audio tags (ShowcaseElevenLabsTextHelper).</summary>
        public string HookStyleKey { get; set; } = HookStyleCatalog.StyleHuongdan;

        /// <summary>±% cộng thêm lên rate preset Edge (trẻ/nhanh = +, chậm/già = −).</summary>
        public int EdgeRateOffsetPercent { get; set; }

        /// <summary>±Hz cộng thêm pitch (vui/trẻ = +, trầm/buồn = −).</summary>
        public int EdgePitchOffsetHz { get; set; }

        public string BodyVoicePresetId { get; set; } = ShowcaseVoicePresetCatalog.DefaultPresetId;

        public string BodyVoiceAgeId { get; set; } = ShowcaseVoicePresetDimensions.Age.Adult26_35;

        public string BodyVoiceLanguageId { get; set; } = ShowcaseVoicePresetDimensions.Language.ViGeneral;

        /// <summary>Giọng ElevenLabs cố định (ElevenVoicePersonaCatalog) — thân. Rỗng = dùng preset/mood cũ.</summary>
        public string BodyElevenPersona { get; set; } = ElevenVoicePersonaCatalog.None;

        /// <summary>Tone giọng thân (ShowcaseVoicePresetDimensions.Tone.*) — Tự nhiên = không ghi đè voice_settings.</summary>
        public string BodyVoiceToneId { get; set; } = ShowcaseVoicePresetDimensions.Tone.Natural;

        public int BodyElevenCustomStabilityPercent { get; set; } = 50;

        public int BodyElevenCustomSimilarityPercent { get; set; } = 75;

        public int BodyElevenCustomStylePercent { get; set; } = 15;

        public string BodyStyleKey { get; set; } = HookStyleCatalog.StyleHuongdan;

        public int BodyEdgeRateOffsetPercent { get; set; }

        public int BodyEdgePitchOffsetHz { get; set; }

        public ShowcaseVoicePresetDefinition Preset =>
            ShowcaseVoicePresetCatalog.GetById(VoicePresetId);

        /// <summary>Persona đã resolve cho segment hiện tại (đặt bởi <see cref="ForSegment"/>).</summary>
        public string ElevenPersona { get; set; } = ElevenVoicePersonaCatalog.None;

        /// <summary>Tone đã resolve cho segment hiện tại (đặt bởi <see cref="ForSegment"/>).</summary>
        public string ElevenToneId { get; set; } = ShowcaseVoicePresetDimensions.Tone.Natural;

        public int ElevenCustomStabilityPercent { get; set; } = 50;

        public int ElevenCustomSimilarityPercent { get; set; } = 75;

        public int ElevenCustomStylePercent { get; set; } = 15;

        public ShowcaseTtsRenderOptions ForSegment(bool hook)
        {
            if (hook)
            {
                return new ShowcaseTtsRenderOptions
                {
                    HookEngine = HookEngine,
                    BodyEngine = BodyEngine,
                    VoicePresetId = VoicePresetId,
                    VoiceAgeId = VoiceAgeId,
                    VoiceLanguageId = VoiceLanguageId,
                    HookStyleKey = HookStyleKey,
                    EdgeRateOffsetPercent = EdgeRateOffsetPercent,
                    EdgePitchOffsetHz = EdgePitchOffsetHz,
                    ElevenPersona = ElevenVoicePersonaCatalog.Normalize(HookElevenPersona),
                    ElevenToneId = ShowcaseElevenToneHelper.NormalizeToneId(HookVoiceToneId),
                    ElevenCustomStabilityPercent = ShowcaseElevenToneHelper.ClampPercent(HookElevenCustomStabilityPercent),
                    ElevenCustomSimilarityPercent = ShowcaseElevenToneHelper.ClampPercent(HookElevenCustomSimilarityPercent),
                    ElevenCustomStylePercent = ShowcaseElevenToneHelper.ClampPercent(HookElevenCustomStylePercent)
                };
            }

            return new ShowcaseTtsRenderOptions
            {
                HookEngine = HookEngine,
                BodyEngine = BodyEngine,
                VoicePresetId = BodyVoicePresetId,
                VoiceAgeId = BodyVoiceAgeId,
                VoiceLanguageId = BodyVoiceLanguageId,
                HookStyleKey = BodyStyleKey,
                EdgeRateOffsetPercent = BodyEdgeRateOffsetPercent,
                EdgePitchOffsetHz = BodyEdgePitchOffsetHz,
                ElevenPersona = ElevenVoicePersonaCatalog.Normalize(BodyElevenPersona),
                ElevenToneId = ShowcaseElevenToneHelper.NormalizeToneId(BodyVoiceToneId),
                ElevenCustomStabilityPercent = ShowcaseElevenToneHelper.ClampPercent(BodyElevenCustomStabilityPercent),
                ElevenCustomSimilarityPercent = ShowcaseElevenToneHelper.ClampPercent(BodyElevenCustomSimilarityPercent),
                ElevenCustomStylePercent = ShowcaseElevenToneHelper.ClampPercent(BodyElevenCustomStylePercent)
            };
        }

        public static ShowcaseTtsRenderOptions FromVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            return ShowcaseTtsHelper.ResolveFromVideo(video, settings);
        }
    }
}
