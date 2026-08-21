using System;
using System.Globalization;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chỉnh Rate/Pitch Edge (offset trên preset giọng) — preset theo phong cách hook hoặc tùy chỉnh.</summary>
    public static class ShowcaseEdgeProsodyHelper
    {
        public const string StyleCustom = "tuy_chinh";

        public const int MinRateOffsetPercent = -25;
        public const int MaxRateOffsetPercent = 25;
        public const int MinPitchOffsetHz = -12;
        public const int MaxPitchOffsetHz = 12;

        public static int ClampRateOffset(int value) =>
            Math.Max(MinRateOffsetPercent, Math.Min(MaxRateOffsetPercent, value));

        public static int ClampPitchOffset(int value) =>
            Math.Max(MinPitchOffsetHz, Math.Min(MaxPitchOffsetHz, value));

        public static bool IsCustomStyle(string hookStyleKey) =>
            string.Equals(NormalizeStoredHookStyleKey(hookStyleKey), StyleCustom, StringComparison.OrdinalIgnoreCase);

        public static string GetHookStyleDisplayName(string hookStyleKey)
        {
            if (IsCustomStyle(hookStyleKey))
            {
                return "Tùy chỉnh";
            }

            return HookStyleCatalog.GetDisplayName(hookStyleKey);
        }

        /// <summary>Lưu trên video / draft — giữ <see cref="StyleCustom"/>.</summary>
        public static string NormalizeStoredHookStyleKey(string hookStyleKey)
        {
            var key = (hookStyleKey ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                return HookStyleCatalog.StyleHuongdan;
            }

            if (string.Equals(key, StyleCustom, StringComparison.OrdinalIgnoreCase))
            {
                return StyleCustom;
            }

            if (ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(key))
            {
                return ShowcaseElevenToneHelper.HookStyleCustomVoiceKey;
            }

            foreach (var catalogKey in HookStyleCatalog.AllStyleKeys)
            {
                if (string.Equals(catalogKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return catalogKey;
                }
            }

            return HookStyleCatalog.StyleHuongdan;
        }

        public static bool TryGetPresetOffsets(string hookStyleKey, out int rateOffsetPercent, out int pitchOffsetHz) =>
            TryGetPresetOffsets(hookStyleKey, philosophyQuote: false, out rateOffsetPercent, out pitchOffsetHz);

        public static bool TryGetPresetOffsets(
            string hookStyleKey,
            bool philosophyQuote,
            out int rateOffsetPercent,
            out int pitchOffsetHz)
        {
            rateOffsetPercent = 0;
            pitchOffsetHz = 0;
            var key = NormalizeStoredHookStyleKey(hookStyleKey);
            if (string.Equals(key, StyleCustom, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (philosophyQuote)
            {
                GetPresetForPhilosophyQuoteStyle(key, out rateOffsetPercent, out pitchOffsetHz);
            }
            else
            {
                GetPresetForCatalogStyle(key, out rateOffsetPercent, out pitchOffsetHz);
            }

            return true;
        }

        public static void ResolveEffectiveOffsets(
            string hookStyleKey,
            int customRateOffsetPercent,
            int customPitchOffsetHz,
            out int rateOffsetPercent,
            out int pitchOffsetHz) =>
            ResolveEffectiveOffsets(
                hookStyleKey,
                customRateOffsetPercent,
                customPitchOffsetHz,
                philosophyQuote: false,
                out rateOffsetPercent,
                out pitchOffsetHz);

        public static void ResolveEffectiveOffsets(
            string hookStyleKey,
            int customRateOffsetPercent,
            int customPitchOffsetHz,
            bool philosophyQuote,
            out int rateOffsetPercent,
            out int pitchOffsetHz)
        {
            if (TryGetPresetOffsets(hookStyleKey, philosophyQuote, out rateOffsetPercent, out pitchOffsetHz))
            {
                return;
            }

            rateOffsetPercent = ClampRateOffset(customRateOffsetPercent);
            pitchOffsetHz = ClampPitchOffset(customPitchOffsetHz);
        }

        public static void EnsureVideoDefaults(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseEdgeRateOffsetPercent = ClampRateOffset(video.ShowcaseEdgeRateOffsetPercent);
            video.ShowcaseEdgePitchOffsetHz = ClampPitchOffset(video.ShowcaseEdgePitchOffsetHz);
        }

        public static EdgeTtsSynthesisOptions ApplyUserOffsets(
            EdgeTtsSynthesisOptions voiceBase,
            string hookStyleKey,
            int customRateOffsetPercent,
            int customPitchOffsetHz) =>
            ApplyUserOffsets(voiceBase, hookStyleKey, customRateOffsetPercent, customPitchOffsetHz, philosophyQuote: false);

        public static EdgeTtsSynthesisOptions ApplyUserOffsets(
            EdgeTtsSynthesisOptions voiceBase,
            string hookStyleKey,
            int customRateOffsetPercent,
            int customPitchOffsetHz,
            bool philosophyQuote)
        {
            ResolveEffectiveOffsets(
                hookStyleKey,
                customRateOffsetPercent,
                customPitchOffsetHz,
                philosophyQuote,
                out var rateOffsetPercent,
                out var pitchOffsetHz);
            return ApplyUserOffsets(voiceBase, rateOffsetPercent, pitchOffsetHz);
        }

        public static EdgeTtsSynthesisOptions ApplyUserOffsets(
            EdgeTtsSynthesisOptions voiceBase,
            int rateOffsetPercent,
            int pitchOffsetHz)
        {
            voiceBase = voiceBase ?? new EdgeTtsSynthesisOptions();
            rateOffsetPercent = ClampRateOffset(rateOffsetPercent);
            pitchOffsetHz = ClampPitchOffset(pitchOffsetHz);
            if (rateOffsetPercent == 0 && pitchOffsetHz == 0)
            {
                return voiceBase;
            }

            return new EdgeTtsSynthesisOptions
            {
                VoiceShortName = voiceBase.VoiceShortName,
                Rate = ShowcaseEdgeHookStyleProsody.MergeRatePercent(voiceBase.Rate, rateOffsetPercent),
                Pitch = ShowcaseEdgeHookStyleProsody.MergePitchHz(voiceBase.Pitch, pitchOffsetHz),
                Volume = voiceBase.Volume
            };
        }

        public static string FormatOffsetSummary(string hookStyleKey, int customRateOffset, int customPitchOffset)
        {
            ResolveEffectiveOffsets(hookStyleKey, customRateOffset, customPitchOffset, out var rate, out var pitch);
            if (IsCustomStyle(hookStyleKey))
            {
                return "tùy chỉnh · " + FormatOffsetPair(rate, pitch);
            }

            return HookStyleCatalog.GetDisplayName(hookStyleKey) + " · " + FormatOffsetPair(rate, pitch);
        }

        /// <summary>Phần rate/pitch trên dòng Kết quả — sau «phong cách», không lặp tên phong cách.</summary>
        public static string FormatSegmentProsodySuffix(string hookStyleKey, int customRateOffset, int customPitchOffset)
        {
            ResolveEffectiveOffsets(hookStyleKey, customRateOffset, customPitchOffset, out var rate, out var pitch);
            if (!IsCustomStyle(hookStyleKey) && rate == 0 && pitch == 0)
            {
                return string.Empty;
            }

            var pair = FormatOffsetPair(rate, pitch);
            return " · " + pair;
        }

        public static string FormatOffsetPair(int rateOffset, int pitchOffset)
        {
            rateOffset = ClampRateOffset(rateOffset);
            pitchOffset = ClampPitchOffset(pitchOffset);
            if (rateOffset == 0 && pitchOffset == 0)
            {
                return "rate ±0 · pitch ±0";
            }

            var rateStr = rateOffset == 0
                ? "rate ±0"
                : (rateOffset > 0 ? "rate +" : "rate ") + rateOffset.ToString(CultureInfo.InvariantCulture) + "%";
            var pitchStr = pitchOffset == 0
                ? "pitch ±0"
                : (pitchOffset > 0 ? "pitch +" : "pitch ") + pitchOffset.ToString(CultureInfo.InvariantCulture) + "Hz";
            return rateStr + " · " + pitchStr;
        }

        /// <summary>Offset slider gắn sẵn — Showcase/Reup (cân nhịp, không quá chậm).</summary>
        private static void GetPresetForCatalogStyle(string catalogStyleKey, out int rateOffsetPercent, out int pitchOffsetHz)
        {
            switch (NormalizeStoredHookStyleKey(catalogStyleKey))
            {
                case HookStyleCatalog.StyleFomo:
                    rateOffsetPercent = 14;
                    pitchOffsetHz = 7;
                    return;
                case HookStyleCatalog.StyleBocphot:
                    rateOffsetPercent = 11;
                    pitchOffsetHz = 6;
                    return;
                case HookStyleCatalog.StyleHuongdan:
                    rateOffsetPercent = 6;
                    pitchOffsetHz = 3;
                    return;
                case HookStyleCatalog.StyleKechuyen:
                    rateOffsetPercent = -5;
                    pitchOffsetHz = -3;
                    return;
                case HookStyleCatalog.StyleNoidau:
                    rateOffsetPercent = -8;
                    pitchOffsetHz = -5;
                    return;
                default:
                    rateOffsetPercent = 6;
                    pitchOffsetHz = 3;
                    return;
            }
        }

        /// <summary>Quote triết lý — trầm/cảm xúc nhưng ~100% tốc độ batch, có nhấn nhá.</summary>
        private static void GetPresetForPhilosophyQuoteStyle(string catalogStyleKey, out int rateOffsetPercent, out int pitchOffsetHz)
        {
            switch (NormalizeStoredHookStyleKey(catalogStyleKey))
            {
                case HookStyleCatalog.StyleNoidau:
                    rateOffsetPercent = -5;
                    pitchOffsetHz = -3;
                    return;
                case HookStyleCatalog.StyleKechuyen:
                    rateOffsetPercent = -2;
                    pitchOffsetHz = -2;
                    return;
                case HookStyleCatalog.StyleHuongdan:
                    rateOffsetPercent = 3;
                    pitchOffsetHz = 1;
                    return;
                case HookStyleCatalog.StyleFomo:
                    rateOffsetPercent = 8;
                    pitchOffsetHz = 4;
                    return;
                case HookStyleCatalog.StyleBocphot:
                    rateOffsetPercent = 6;
                    pitchOffsetHz = 3;
                    return;
                default:
                    rateOffsetPercent = 0;
                    pitchOffsetHz = 0;
                    return;
            }
        }
    }
}
