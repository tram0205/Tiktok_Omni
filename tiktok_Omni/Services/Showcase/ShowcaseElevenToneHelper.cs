using System;
using System.Collections.Generic;
using System.Globalization;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>
    /// Map Tone giọng (<see cref="ShowcaseVoicePresetDimensions.Tone"/>) → voice_settings (stability/similarity/style)
    /// + audio tag ElevenLabs v3 (eleven_v3 hỗ trợ chèn [tag] cảm xúc đầu câu).
    /// «Tự nhiên» = không override — dùng mặc định cũ theo vai trò Hook/Thân/Narration.
    /// «Tùy chỉnh» = người dùng tự kéo 3 số (không có audio tag riêng).
    /// </summary>
    public static class ShowcaseElevenToneHelper
    {
        public const int MinPercent = 0;
        public const int MaxPercent = 100;

        /// <summary>
        /// Sentinel lưu trong ShowcaseHookStyleKey — mục «⚙ Tùy chỉnh giọng» riêng cho Hook + ElevenLabs.
        /// Khác với <see cref="ShowcaseEdgeProsodyHelper.StyleCustom"/> (Edge Rate/Pitch). Khi chọn mục này,
        /// Hook không chèn audio tag cảm xúc — chỉ áp 3 số stability/similarity/style tự chỉnh.
        /// </summary>
        public const string HookStyleCustomVoiceKey = "eleven_tuy_chinh_giong";

        public static bool IsHookCustomVoiceStyle(string hookStyleKey) =>
            string.Equals((hookStyleKey ?? string.Empty).Trim(), HookStyleCustomVoiceKey, StringComparison.OrdinalIgnoreCase);

        public static int ClampPercent(int value) => Math.Max(MinPercent, Math.Min(MaxPercent, value));

        public static bool IsCustomTone(string toneId) =>
            string.Equals(NormalizeToneId(toneId), ShowcaseVoicePresetDimensions.Tone.Custom, StringComparison.OrdinalIgnoreCase);

        public static bool IsNaturalTone(string toneId) =>
            string.Equals(NormalizeToneId(toneId), ShowcaseVoicePresetDimensions.Tone.Natural, StringComparison.OrdinalIgnoreCase);

        /// <summary>True nếu Tone này cần ghi đè voice_settings mặc định theo vai trò (mọi tone trừ «Tự nhiên»).</summary>
        public static bool HasVoiceSettingsOverride(string toneId) => !IsNaturalTone(toneId);

        /// <summary>Chuẩn hoá — «tuy_chinh» giữ nguyên; rỗng/không nhận diện → Natural.</summary>
        public static string NormalizeToneId(string toneId)
        {
            var key = (toneId ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                return ShowcaseVoicePresetDimensions.Tone.Natural;
            }

            if (string.Equals(key, ShowcaseVoicePresetDimensions.Tone.Custom, StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseVoicePresetDimensions.Tone.Custom;
            }

            foreach (var opt in ShowcaseVoicePresetDimensions.ListToneOptions())
            {
                if (string.Equals(opt.Id, key, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }

            return ShowcaseVoicePresetDimensions.Tone.Natural;
        }

        private sealed class ToneProfile
        {
            public ToneProfile(int stability, int similarity, int style, string audioTag)
            {
                Stability = stability;
                Similarity = similarity;
                Style = style;
                AudioTag = audioTag;
            }

            public int Stability { get; }
            public int Similarity { get; }
            public int Style { get; }
            public string AudioTag { get; }
        }

        /// <summary>Bộ số khởi điểm theo từng Tone — % (0-100), quy sang 0.00-1.00 khi build payload.</summary>
        private static readonly Dictionary<string, ToneProfile> Profiles =
            new Dictionary<string, ToneProfile>(StringComparer.OrdinalIgnoreCase)
            {
                [ShowcaseVoicePresetDimensions.Tone.Playful] = new ToneProfile(35, 75, 40, "[playfully]"),
                [ShowcaseVoicePresetDimensions.Tone.WarmDeep] = new ToneProfile(65, 78, 15, "[warmly]"),
                [ShowcaseVoicePresetDimensions.Tone.Ethereal] = new ToneProfile(55, 70, 25, "[softly]"),
                [ShowcaseVoicePresetDimensions.Tone.Cheerful] = new ToneProfile(35, 78, 45, "[excitedly]"),
                [ShowcaseVoicePresetDimensions.Tone.Sweet] = new ToneProfile(45, 78, 30, "[sweetly]"),
                [ShowcaseVoicePresetDimensions.Tone.Philosophy] = new ToneProfile(70, 72, 8, "[thoughtfully]"),
                [ShowcaseVoicePresetDimensions.Tone.Narrator] = new ToneProfile(55, 75, 15, "[calmly]")
            };

        /// <summary>Lấy bộ số preset của Tone — false với «Tự nhiên» (không override) và «Tùy chỉnh» (dùng số người dùng nhập).</summary>
        public static bool TryGetPresetVoiceSettings(string toneId, out int stabilityPercent, out int similarityPercent, out int stylePercent)
        {
            stabilityPercent = 0;
            similarityPercent = 0;
            stylePercent = 0;
            var key = NormalizeToneId(toneId);
            if (!Profiles.TryGetValue(key, out var profile))
            {
                return false;
            }

            stabilityPercent = profile.Stability;
            similarityPercent = profile.Similarity;
            stylePercent = profile.Style;
            return true;
        }

        /// <summary>Số hiệu lực cho slider/preview — Tone có preset thì dùng preset, còn lại (Tùy chỉnh) dùng số người dùng nhập.</summary>
        public static void ResolveEffectiveVoiceSettings(
            string toneId,
            int customStabilityPercent,
            int customSimilarityPercent,
            int customStylePercent,
            out int stabilityPercent,
            out int similarityPercent,
            out int stylePercent)
        {
            if (TryGetPresetVoiceSettings(toneId, out stabilityPercent, out similarityPercent, out stylePercent))
            {
                return;
            }

            stabilityPercent = ClampPercent(customStabilityPercent);
            similarityPercent = ClampPercent(customSimilarityPercent);
            stylePercent = ClampPercent(customStylePercent);
        }

        /// <summary>Audio tag chèn đầu câu cho model eleven_v3 — null nếu Tự nhiên/Tùy chỉnh (không có tag riêng).</summary>
        public static string ResolveAudioTag(string toneId)
        {
            var key = NormalizeToneId(toneId);
            return Profiles.TryGetValue(key, out var profile) ? profile.AudioTag : null;
        }

        public static string GetToneLabel(string toneId)
        {
            if (IsCustomTone(toneId))
            {
                return "Tùy chỉnh";
            }

            var key = NormalizeToneId(toneId);
            foreach (var opt in ShowcaseVoicePresetDimensions.ListToneOptions())
            {
                if (string.Equals(opt.Id, key, StringComparison.OrdinalIgnoreCase))
                {
                    return opt.Label;
                }
            }

            return key;
        }

        /// <summary>Dòng tóm tắt cho summary label — rỗng nếu Tự nhiên (không có gì để hiển thị thêm).</summary>
        public static string FormatToneSummary(
            string toneId,
            int customStabilityPercent,
            int customSimilarityPercent,
            int customStylePercent)
        {
            if (IsNaturalTone(toneId))
            {
                return string.Empty;
            }

            ResolveEffectiveVoiceSettings(
                toneId,
                customStabilityPercent,
                customSimilarityPercent,
                customStylePercent,
                out var stability,
                out var similarity,
                out var style);
            return GetToneLabel(toneId) + " (" +
                   ClampPercent(stability) + "/" + ClampPercent(similarity) + "/" + ClampPercent(style) + "%)";
        }

        private static string FormatUnit(int percent) =>
            (ClampPercent(percent) / 100.0).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Dòng tóm tắt cho Hook khi «Phong cách hook» = «⚙ Tùy chỉnh giọng».</summary>
        public static string FormatCustomVoiceSummary(int stabilityPercent, int similarityPercent, int stylePercent) =>
            "Tùy chỉnh giọng (" + FormatUnit(stabilityPercent) + "/" + FormatUnit(similarityPercent) + "/" + FormatUnit(stylePercent) + ")";

        /// <summary>Chỉ bộ ba % — dùng sau «⚙ Tùy chỉnh giọng» trên dòng Kết quả.</summary>
        public static string FormatCustomVoicePercentTriplet(int stabilityPercent, int similarityPercent, int stylePercent) =>
            ClampPercent(stabilityPercent) + "/" + ClampPercent(similarityPercent) + "/" + ClampPercent(stylePercent) + "%";
    }
}
