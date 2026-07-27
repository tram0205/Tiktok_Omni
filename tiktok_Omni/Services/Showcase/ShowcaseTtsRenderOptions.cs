using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    public sealed class ShowcaseTtsRenderOptions
    {
        public TtsEngineKind Engine { get; set; } = TtsEngineKind.EdgeTts;
        public string VoicePresetId { get; set; } = ShowcaseVoicePresetCatalog.DefaultPresetId;

        public string VoiceAgeId { get; set; } = ShowcaseVoicePresetDimensions.Age.Adult26_35;

        public string VoiceLanguageId { get; set; } = ShowcaseVoicePresetDimensions.Language.ViSouth;

        public ShowcaseVoicePresetDefinition Preset =>
            ShowcaseVoicePresetCatalog.GetById(VoicePresetId);

        public static ShowcaseTtsRenderOptions FromVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            return ShowcaseTtsHelper.ResolveFromVideo(video, settings);
        }
    }
}
