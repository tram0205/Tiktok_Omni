using System;
using System.Globalization;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Edge TTS: 5 phong cách hook (HookStyleCatalog) + hook nhấn / thân êm qua rate/pitch/volume.</summary>
    public static class ShowcaseEdgeHookStyleProsody
    {
        public static string NormalizeStyleKey(string hookStyleKey)
        {
            var key = (hookStyleKey ?? string.Empty).Trim().ToLowerInvariant();
            if (HookStyleCatalog.AllStyleKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                return key;
            }

            return HookStyleCatalog.StyleHuongdan;
        }

        public static EdgeTtsSynthesisOptions ApplySegmentProsody(
            EdgeTtsSynthesisOptions voiceBase,
            string hookStyleKey,
            bool emphaticHook,
            bool showcaseExpressiveBody)
        {
            voiceBase = voiceBase ?? new EdgeTtsSynthesisOptions();
            var styleKey = NormalizeStyleKey(hookStyleKey);
            if (ShowcaseEdgeProsodyHelper.IsCustomStyle(hookStyleKey))
            {
                styleKey = HookStyleCatalog.StyleHuongdan;
            }

            var style = GetStyleProsody(styleKey);

            int rateDelta;
            int pitchHz;
            int volumePct;
            if (emphaticHook)
            {
                rateDelta = style.HookRateDelta + 3;
                pitchHz = style.HookPitchHz + 1;
                volumePct = style.HookVolumePct + 4;
            }
            else if (showcaseExpressiveBody)
            {
                rateDelta = style.BodyRateDelta - 2;
                pitchHz = style.BodyPitchHz - 1;
                volumePct = style.BodyVolumePct - 3;
            }
            else
            {
                rateDelta = style.BodyRateDelta - 5;
                pitchHz = style.BodyPitchHz - 2;
                volumePct = style.BodyVolumePct - 5;
            }

            return new EdgeTtsSynthesisOptions
            {
                VoiceShortName = voiceBase.VoiceShortName,
                Rate = MergeRatePercent(voiceBase.Rate, rateDelta),
                Pitch = MergePitchHz(voiceBase.Pitch, pitchHz),
                Volume = MergeVolumePercent(voiceBase.Volume, volumePct)
            };
        }

        private static StyleProsody GetStyleProsody(string styleKey)
        {
            switch (styleKey)
            {
                case HookStyleCatalog.StyleNoidau:
                    return new StyleProsody(-3, -3, 0, -9, -4, -6);
                case HookStyleCatalog.StyleBocphot:
                    return new StyleProsody(7, 4, 9, -3, 0, -1);
                case HookStyleCatalog.StyleHuongdan:
                    return new StyleProsody(3, 2, 4, -6, 0, -3);
                case HookStyleCatalog.StyleFomo:
                    return new StyleProsody(9, 5, 12, -2, 1, 1);
                case HookStyleCatalog.StyleKechuyen:
                    return new StyleProsody(-5, -4, -1, -11, -3, -7);
                default:
                    return GetStyleProsody(HookStyleCatalog.StyleHuongdan);
            }
        }

        private readonly struct StyleProsody
        {
            public StyleProsody(
                int hookRateDelta,
                int hookPitchHz,
                int hookVolumePct,
                int bodyRateDelta,
                int bodyPitchHz,
                int bodyVolumePct)
            {
                HookRateDelta = hookRateDelta;
                HookPitchHz = hookPitchHz;
                HookVolumePct = hookVolumePct;
                BodyRateDelta = bodyRateDelta;
                BodyPitchHz = bodyPitchHz;
                BodyVolumePct = bodyVolumePct;
            }

            public int HookRateDelta { get; }
            public int HookPitchHz { get; }
            public int HookVolumePct { get; }
            public int BodyRateDelta { get; }
            public int BodyPitchHz { get; }
            public int BodyVolumePct { get; }
        }

        internal static string MergeRatePercent(string baseRate, int deltaPercent)
        {
            var merged = ParseSignedPercent(baseRate, 0) + deltaPercent;
            merged = Math.Max(-30, Math.Min(35, merged));
            return FormatSignedPercent(merged);
        }

        internal static string MergePitchHz(string basePitch, int deltaHz)
        {
            var merged = ParseSignedHz(basePitch, 0) + deltaHz;
            merged = Math.Max(-12, Math.Min(12, merged));
            return FormatSignedHz(merged);
        }

        internal static string MergeVolumePercent(string baseVolume, int deltaPercent)
        {
            var merged = ParseSignedPercent(baseVolume, 0) + deltaPercent;
            merged = Math.Max(-20, Math.Min(25, merged));
            return FormatSignedPercent(merged);
        }

        private static int ParseSignedPercent(string value, int fallback)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return fallback;
            }

            if (s.EndsWith("%", StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - 1).Trim();
            }

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                return n;
            }

            return fallback;
        }

        private static int ParseSignedHz(string value, int fallback)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return fallback;
            }

            if (s.EndsWith("Hz", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 2).Trim();
            }

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                return n;
            }

            return fallback;
        }

        private static string FormatSignedPercent(int value)
        {
            if (value == 0)
            {
                return "+0%";
            }

            return value > 0
                ? "+" + value.ToString(CultureInfo.InvariantCulture) + "%"
                : value.ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatSignedHz(int value)
        {
            if (value == 0)
            {
                return "+0Hz";
            }

            return value > 0
                ? "+" + value.ToString(CultureInfo.InvariantCulture) + "Hz"
                : value.ToString(CultureInfo.InvariantCulture) + "Hz";
        }
    }
}
