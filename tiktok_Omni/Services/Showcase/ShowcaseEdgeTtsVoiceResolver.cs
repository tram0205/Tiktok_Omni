using System;
using System.Globalization;

namespace tiktok_Omni.Services.Showcase
{
    public static class ShowcaseEdgeTtsVoiceResolver
    {
        public static EdgeTtsSynthesisOptions Resolve(
            ShowcaseTtsRenderOptions options,
            bool emphaticHook = false,
            bool showcaseExpressiveBody = true,
            bool emphaticCta = false,
            bool philosophyQuote = false)
        {
            var voiceBase = ResolveVoiceBase(options);
            voiceBase = ShowcaseEdgeProsodyHelper.ApplyUserOffsets(
                voiceBase,
                options?.HookStyleKey,
                options?.EdgeRateOffsetPercent ?? 0,
                options?.EdgePitchOffsetHz ?? 0,
                philosophyQuote);
            return ShowcaseEdgeHookStyleProsody.ApplySegmentProsody(
                voiceBase,
                options?.HookStyleKey,
                emphaticHook,
                showcaseExpressiveBody,
                emphaticCta,
                philosophyQuote);
        }

        public static EdgeTtsSynthesisOptions ResolveVoiceBase(ShowcaseTtsRenderOptions options)
        {
            options = options ?? new ShowcaseTtsRenderOptions();
            var presetId = (options.VoicePresetId ?? ShowcaseVoicePresetCatalog.DefaultPresetId).Trim();
            var preset = options.Preset ?? ShowcaseVoicePresetCatalog.GetById(presetId);

            if (string.Equals(presetId, "male_south_young", StringComparison.OrdinalIgnoreCase))
            {
                return new EdgeTtsSynthesisOptions
                {
                    VoiceShortName = EdgeTtsVoices.NamMinhNeural,
                    Rate = "+0%"
                };
            }

            if (string.Equals(presetId, "female_south_young", StringComparison.OrdinalIgnoreCase))
            {
                return new EdgeTtsSynthesisOptions
                {
                    VoiceShortName = EdgeTtsVoices.HoaiMyNeural,
                    Rate = "+8%"
                };
            }

            var isMale = IsMalePreset(presetId, preset);
            var rate = MapLengthScaleToRate(preset.PiperLengthScale);
            return new EdgeTtsSynthesisOptions
            {
                VoiceShortName = isMale ? EdgeTtsVoices.NamMinhNeural : EdgeTtsVoices.HoaiMyNeural,
                Rate = rate
            };
        }

        public static EdgeTtsSynthesisOptions ResolveFemaleSouthYoung() =>
            new EdgeTtsSynthesisOptions
            {
                VoiceShortName = EdgeTtsVoices.HoaiMyNeural,
                Rate = "+8%"
            };

        private static bool IsMalePreset(string presetId, ShowcaseVoicePresetDefinition preset)
        {
            return presetId.StartsWith("male_", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(presetId, "warm_deep", StringComparison.OrdinalIgnoreCase)
                   || presetId.StartsWith("baby_boy", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Catalog length_scale &lt; 1 = faster → Edge rate +%.</summary>
        private static string MapLengthScaleToRate(float lengthScale)
        {
            var delta = (int)Math.Round((1.0d - lengthScale) * 100d, MidpointRounding.AwayFromZero);
            delta = Math.Max(-25, Math.Min(25, delta));
            if (delta == 0)
            {
                return "+0%";
            }

            return delta > 0
                ? "+" + delta.ToString(CultureInfo.InvariantCulture) + "%"
                : delta.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
