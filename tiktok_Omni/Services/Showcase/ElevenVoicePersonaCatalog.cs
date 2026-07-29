using System;
using System.Collections.Generic;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>
    /// 6 giọng ElevenLabs dùng chung mọi tab video (Showcase, Triết lý, Reup…).
    /// Mỗi giọng gắn một voice_id cố định nhập trong Cài đặt — chọn persona thay vì suy diễn
    /// giới tính/tuổi từ preset nội bộ.
    /// </summary>
    public static class ElevenVoicePersonaCatalog
    {
        /// <summary>Không chọn persona — dùng logic cũ (mood preset → VoiceId_Intense/Calm → endpoint).</summary>
        public const string None = "";

        public const string FemaleYoung = "female_young";
        public const string FemaleMature = "female_mature";
        public const string MaleYoung = "male_young";
        public const string MaleMature = "male_mature";
        public const string GirlChild = "girl_child";
        public const string BoyChild = "boy_child";

        private static readonly IReadOnlyList<ShowcaseVoicePresetDimensions.VoiceDimensionChoice> Options = new[]
        {
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(None, "Mặc định (theo Cài đặt / preset)"),
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(FemaleYoung, "Nữ trẻ"),
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(FemaleMature, "Nữ trung niên"),
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(MaleYoung, "Nam trẻ"),
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(MaleMature, "Nam trung niên"),
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(GirlChild, "Bé gái"),
            new ShowcaseVoicePresetDimensions.VoiceDimensionChoice(BoyChild, "Bé trai")
        };

        public static IReadOnlyList<ShowcaseVoicePresetDimensions.VoiceDimensionChoice> ListOptions() => Options;

        public static string Normalize(string personaKey)
        {
            personaKey = (personaKey ?? string.Empty).Trim().ToLowerInvariant();
            return Options.Any(o => string.Equals(o.Id, personaKey, StringComparison.OrdinalIgnoreCase))
                ? personaKey
                : None;
        }

        public static string GetDisplayName(string personaKey)
        {
            personaKey = Normalize(personaKey);
            return Options
                .FirstOrDefault(o => string.Equals(o.Id, personaKey, StringComparison.OrdinalIgnoreCase))
                ?.Label ?? "Mặc định";
        }

        /// <summary>voice_id đã cấu hình trong Cài đặt cho persona; null nếu chọn Mặc định hoặc ô đang trống.</summary>
        public static string ResolveVoiceId(string personaKey, AppSettings settings)
        {
            personaKey = Normalize(personaKey);
            if (settings == null || string.Equals(personaKey, None, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string raw;
            switch (personaKey)
            {
                case FemaleYoung:
                    raw = settings.VoiceId_FemaleYoung;
                    break;
                case FemaleMature:
                    raw = settings.VoiceId_FemaleMature;
                    break;
                case MaleYoung:
                    raw = settings.VoiceId_MaleYoung;
                    break;
                case MaleMature:
                    raw = settings.VoiceId_MaleMature;
                    break;
                case GirlChild:
                    raw = settings.VoiceId_GirlChild;
                    break;
                case BoyChild:
                    raw = settings.VoiceId_BoyChild;
                    break;
                default:
                    raw = null;
                    break;
            }

            raw = (raw ?? string.Empty).Trim();
            return string.IsNullOrEmpty(raw) ? null : raw;
        }
    }
}
