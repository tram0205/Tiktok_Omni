using System;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Map vùng giọng tiếng Việt → voice_id ElevenLabs (6 ô persona trong Cài đặt = miền Nam).</summary>
    internal static class ElevenVoiceRegionHelper
    {
        public static string ResolveVoiceId(string personaKey, string languageId, AppSettings settings)
        {
            personaKey = ElevenVoicePersonaCatalog.Normalize(personaKey);
            languageId = ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(languageId);
            if (settings == null || string.Equals(personaKey, ElevenVoicePersonaCatalog.None, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (ShowcaseVoicePresetDimensions.IsSouthernVietnamese(languageId))
            {
                return ElevenVoicePersonaCatalog.ResolveConfiguredVoiceId(personaKey, settings);
            }

            return null;
        }

        /// <summary>Persona «Mặc định» — suy persona từ preset + vùng miền, tránh fallback Calm/Intense (giọng Bắc).</summary>
        public static string ResolveInferredVoiceId(ShowcaseTtsRenderOptions segmentTts, AppSettings settings)
        {
            segmentTts = segmentTts ?? new ShowcaseTtsRenderOptions();
            var languageId = ShowcaseVoicePresetDimensions.NormalizeElevenLanguageId(segmentTts.VoiceLanguageId);
            if (!ShowcaseVoicePresetDimensions.IsVietnameseLanguage(languageId))
            {
                return null;
            }

            var personaKey = InferPersonaKey(segmentTts.VoicePresetId);
            if (!ShowcaseVoicePresetDimensions.IsSouthernVietnamese(languageId))
            {
                return null;
            }

            return ResolveVoiceId(personaKey, languageId, settings)
                   ?? ElevenVoicePersonaCatalog.ResolveConfiguredVoiceId(personaKey, settings);
        }

        private static string InferPersonaKey(string presetId)
        {
            var dims = ShowcaseVoicePresetDimensions.GetForPreset(presetId);
            var age = ShowcaseVoicePresetDimensions.NormalizeAgeId(dims.AgeId);
            var gender = ShowcaseVoicePresetDimensions.NormalizeGenderId(dims.GenderId);

            if (gender == ShowcaseVoicePresetDimensions.Gender.Male)
            {
                return IsMatureAge(age)
                    ? ElevenVoicePersonaCatalog.MaleMature
                    : ElevenVoicePersonaCatalog.MaleYoung;
            }

            if (IsChildAge(age))
            {
                return ElevenVoicePersonaCatalog.GirlChild;
            }

            return IsMatureAge(age)
                ? ElevenVoicePersonaCatalog.FemaleMature
                : ElevenVoicePersonaCatalog.FemaleYoung;
        }

        private static bool IsChildAge(string ageId)
        {
            return string.Equals(ageId, ShowcaseVoicePresetDimensions.Age.Child3_7, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(ageId, ShowcaseVoicePresetDimensions.Age.Child8_12, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMatureAge(string ageId)
        {
            return string.Equals(ageId, ShowcaseVoicePresetDimensions.Age.Middle46_55, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(ageId, ShowcaseVoicePresetDimensions.Age.Adult36_45, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(ageId, ShowcaseVoicePresetDimensions.Age.Senior56_65, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(ageId, ShowcaseVoicePresetDimensions.Age.Senior66Plus, StringComparison.OrdinalIgnoreCase);
        }
    }
}
