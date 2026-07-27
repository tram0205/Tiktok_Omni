using System;
using System.Collections.Generic;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chiều UI cho preset giọng Showcase — map tới ShowcaseVoicePresetId + Edge/ElevenLabs.</summary>
    public static class ShowcaseVoicePresetDimensions
    {
        public static class Gender
        {
            public const string Female = "female";
            public const string Male = "male";
            public const string ChildBoy = "child_boy";
            public const string ChildGirl = "child_girl";
            public const string Neutral = "neutral";
        }

        public static class Age
        {
            public const string Child3_7 = "child_3_7";
            public const string Child8_12 = "child_8_12";
            public const string Teen13_17 = "teen_13_17";
            public const string Young18_25 = "young_18_25";
            public const string Adult26_35 = "adult_26_35";
            public const string Adult36_45 = "adult_36_45";
            public const string Middle46_55 = "middle_46_55";
            public const string Senior56_65 = "senior_56_65";
            public const string Senior66Plus = "senior_66_plus";
            public const string Neutral = "neutral";
        }

        public static class Language
        {
            public const string ViSouth = "vi_south";
            public const string ViNorth = "vi_north";
            public const string ViCentral = "vi_central";
            public const string ViGeneral = "vi_general";
            public const string EnUs = "en_us";
            public const string EnGb = "en_gb";
            public const string ZhCn = "zh_cn";
            public const string JaJp = "ja_jp";
            public const string KoKr = "ko_kr";
            public const string ThTh = "th_th";
        }

        public static class Tone
        {
            public const string Natural = "natural";
            public const string WarmDeep = "warm_deep";
            public const string Ethereal = "ethereal";
            public const string Cheerful = "cheerful";
            public const string Sweet = "sweet";
            public const string Philosophy = "philosophy";
            public const string Narrator = "narrator";
            public const string Playful = "playful";
        }

        public sealed class VoiceDimensionChoice
        {
            public VoiceDimensionChoice(string id, string label)
            {
                Id = id;
                Label = label;
            }

            public string Id { get; }
            public string Label { get; }
            public override string ToString() => Label;
        }

        public sealed class VoiceDimensionSet
        {
            public string GenderId { get; set; } = Gender.Female;
            public string AgeId { get; set; } = Age.Adult26_35;
            public string LanguageId { get; set; } = Language.ViSouth;
            public string ToneId { get; set; } = Tone.Natural;
        }

        private static readonly IReadOnlyList<VoiceDimensionChoice> GenderOptions = new[]
        {
            new VoiceDimensionChoice(Gender.Female, "Nữ"),
            new VoiceDimensionChoice(Gender.Male, "Nam"),
            new VoiceDimensionChoice(Gender.Neutral, "Trung tính / kể chuyện")
        };

        private static readonly IReadOnlyList<VoiceDimensionChoice> AgeOptions = new[]
        {
            new VoiceDimensionChoice(Age.Child3_7, "Trẻ em 3–7 tuổi"),
            new VoiceDimensionChoice(Age.Child8_12, "Trẻ em 8–12 tuổi"),
            new VoiceDimensionChoice(Age.Teen13_17, "13–17 tuổi"),
            new VoiceDimensionChoice(Age.Young18_25, "18–25 tuổi"),
            new VoiceDimensionChoice(Age.Adult26_35, "26–35 tuổi"),
            new VoiceDimensionChoice(Age.Adult36_45, "36–45 tuổi"),
            new VoiceDimensionChoice(Age.Middle46_55, "46–55 tuổi"),
            new VoiceDimensionChoice(Age.Senior56_65, "Người lớn tuổi 56–65"),
            new VoiceDimensionChoice(Age.Senior66Plus, "Người lớn tuổi 66+"),
            new VoiceDimensionChoice(Age.Neutral, "Không nhấn tuổi")
        };

        private static readonly IReadOnlyList<VoiceDimensionChoice> LanguageOptions = new[]
        {
            new VoiceDimensionChoice(Language.ViSouth, "Tiếng Việt — miền Nam"),
            new VoiceDimensionChoice(Language.ViNorth, "Tiếng Việt — miền Bắc"),
            new VoiceDimensionChoice(Language.ViCentral, "Tiếng Việt — miền Trung"),
            new VoiceDimensionChoice(Language.ViGeneral, "Tiếng Việt — phát âm chung"),
            new VoiceDimensionChoice(Language.EnUs, "English — US (ElevenLabs)"),
            new VoiceDimensionChoice(Language.EnGb, "English — UK (ElevenLabs)"),
            new VoiceDimensionChoice(Language.ZhCn, "中文 — 简体 (ElevenLabs)"),
            new VoiceDimensionChoice(Language.JaJp, "日本語 (ElevenLabs)"),
            new VoiceDimensionChoice(Language.KoKr, "한국어 (ElevenLabs)"),
            new VoiceDimensionChoice(Language.ThTh, "ภาษาไทย (ElevenLabs)")
        };

        private static readonly Dictionary<string, string> ForeignLanguagePresetIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Language.EnUs] = "en_us_narrator",
                [Language.EnGb] = "en_gb_narrator",
                [Language.ZhCn] = "zh_cn_narrator",
                [Language.JaJp] = "ja_jp_narrator",
                [Language.KoKr] = "ko_kr_narrator",
                [Language.ThTh] = "th_th_narrator"
            };

        private static readonly IReadOnlyList<VoiceDimensionChoice> ToneOptions = new[]
        {
            new VoiceDimensionChoice(Tone.Natural, "Tự nhiên — đọc thường"),
            new VoiceDimensionChoice(Tone.Playful, "Vui / cao — kiểu trẻ con"),
            new VoiceDimensionChoice(Tone.WarmDeep, "Trầm — ấm"),
            new VoiceDimensionChoice(Tone.Ethereal, "Thanh — thoát"),
            new VoiceDimensionChoice(Tone.Cheerful, "Vui vẻ — nhanh nhịp"),
            new VoiceDimensionChoice(Tone.Sweet, "Ngọt ngào"),
            new VoiceDimensionChoice(Tone.Philosophy, "Triết lý — chậm rãi"),
            new VoiceDimensionChoice(Tone.Narrator, "Kể chuyện — trung tính")
        };

        private static readonly Dictionary<string, VoiceDimensionSet> ByPresetId = BuildMap();
        private static readonly Dictionary<string, float> AgeLengthMultiplier = BuildAgeLengthMultipliers();

        public static string NormalizeGenderId(string genderId)
        {
            genderId = Norm(genderId);
            if (string.IsNullOrEmpty(genderId))
            {
                return Gender.Female;
            }

            if (genderId == Gender.ChildBoy)
            {
                return Gender.Male;
            }

            if (genderId == Gender.ChildGirl)
            {
                return Gender.Female;
            }

            if (GenderOptions.Any(o => string.Equals(o.Id, genderId, StringComparison.OrdinalIgnoreCase)))
            {
                return genderId;
            }

            return Gender.Female;
        }

        public static IReadOnlyList<VoiceDimensionChoice> ListGenderOptions() => GenderOptions;
        public static IReadOnlyList<VoiceDimensionChoice> ListAgeOptions() => AgeOptions;
        public static IReadOnlyList<VoiceDimensionChoice> ListLanguageOptions() => LanguageOptions;
        public static IReadOnlyList<VoiceDimensionChoice> ListToneOptions() => ToneOptions;

        public static string NormalizeLanguageId(string languageId)
        {
            languageId = Norm(languageId);
            if (string.IsNullOrEmpty(languageId))
            {
                return Language.ViSouth;
            }

            if (LanguageOptions.Any(o => string.Equals(o.Id, languageId, StringComparison.OrdinalIgnoreCase))
                || ForeignLanguagePresetIds.ContainsKey(languageId))
            {
                return languageId;
            }

            return Language.ViSouth;
        }

        /// <summary>Edge TTS miễn phí — hiện map neural tiếng Việt (Hoài My / Nam Minh).</summary>
        public static bool SupportsEdgeTts(string languageId)
        {
            languageId = NormalizeLanguageId(languageId);
            return languageId.StartsWith("vi_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Preset Showcase cho Edge — nữ → Hoài My, nam → Nam Minh (miền Nam trẻ).</summary>
        public static string ResolveEdgePresetIdFromGender(string genderId)
        {
            genderId = NormalizeGenderId(genderId);
            if (genderId == Gender.Male || genderId == Gender.ChildBoy)
            {
                return "male_south_young";
            }

            return "female_south_young";
        }

        public static string ResolveElevenLabsLanguageCode(string languageId, ShowcaseVoicePresetDefinition preset)
        {
            preset = preset ?? ShowcaseVoicePresetCatalog.GetById(ShowcaseVoicePresetCatalog.DefaultPresetId);
            languageId = NormalizeLanguageId(languageId);
            if (ForeignLanguagePresetIds.TryGetValue(languageId, out var foreignId))
            {
                var foreign = ShowcaseVoicePresetCatalog.GetById(foreignId);
                if (!string.IsNullOrWhiteSpace(foreign.ElevenLabsLanguageCode))
                {
                    return foreign.ElevenLabsLanguageCode;
                }
            }

            if (!string.IsNullOrWhiteSpace(preset.ElevenLabsLanguageCode))
            {
                return preset.ElevenLabsLanguageCode;
            }

            return ElevenLabsTtsHelper.DefaultLanguageCode;
        }

        public static string NormalizeAgeId(string ageId)
        {
            ageId = Norm(ageId);
            if (string.IsNullOrEmpty(ageId))
            {
                return Age.Adult26_35;
            }

            if (AgeLengthMultiplier.ContainsKey(ageId))
            {
                return ageId;
            }

            switch (ageId)
            {
                case "young":
                    return Age.Adult26_35;
                case "mature":
                    return Age.Middle46_55;
                case "child":
                    return Age.Child8_12;
                case "neutral":
                    return Age.Neutral;
                default:
                    return Age.Adult26_35;
            }
        }

        public static VoiceDimensionSet GetForPreset(string presetId)
        {
            presetId = (presetId ?? string.Empty).Trim();
            if (ByPresetId.TryGetValue(presetId, out var set))
            {
                return Clone(set);
            }

            return Clone(ByPresetId[ShowcaseVoicePresetCatalog.DefaultPresetId]);
        }

        public static string ResolvePresetId(VoiceDimensionSet dims)
        {
            dims = dims ?? new VoiceDimensionSet();
            dims.AgeId = NormalizeAgeId(dims.AgeId);
            dims.LanguageId = NormalizeLanguageId(dims.LanguageId);
            dims.GenderId = NormalizeGenderId(dims.GenderId);

            if (ForeignLanguagePresetIds.TryGetValue(dims.LanguageId, out var foreignPreset))
            {
                return foreignPreset;
            }

            if (TryResolveExactPresetId(dims, out var exactId))
            {
                return exactId;
            }

            var g = Norm(dims.GenderId);
            var a = Norm(dims.AgeId);
            var l = Norm(dims.LanguageId);
            var t = Norm(dims.ToneId);

            var scored = ByPresetId
                .Select(kv => new { kv.Key, Score = Score(kv.Value, g, a, l, t) })
                .OrderByDescending(x => x.Score)
                .ToList();
            if (scored.Count > 0 && scored[0].Score > 0)
            {
                return scored[0].Key;
            }

            return ShowcaseVoicePresetCatalog.DefaultPresetId;
        }

        public static bool TryResolveExactPresetId(VoiceDimensionSet dims, out string presetId)
        {
            dims = dims ?? new VoiceDimensionSet();
            dims.AgeId = NormalizeAgeId(dims.AgeId);
            dims.LanguageId = NormalizeLanguageId(dims.LanguageId);
            dims.GenderId = NormalizeGenderId(dims.GenderId);
            var g = Norm(dims.GenderId);
            var a = Norm(dims.AgeId);
            var l = Norm(dims.LanguageId);
            var t = Norm(dims.ToneId);

            if (ForeignLanguagePresetIds.ContainsKey(l))
            {
                presetId = ForeignLanguagePresetIds[l];
                return true;
            }

            var exact = ByPresetId.FirstOrDefault(kv =>
                kv.Value.GenderId == g
                && kv.Value.LanguageId == l
                && kv.Value.ToneId == t
                && AgeCoarse(kv.Value.AgeId) == AgeCoarse(a));
            if (!string.IsNullOrEmpty(exact.Key))
            {
                presetId = exact.Key;
                return true;
            }

            presetId = ShowcaseVoicePresetCatalog.DefaultPresetId;
            return false;
        }

        public static string FormatCompactSummary(string presetId, string voiceAgeId = null, string voiceLanguageId = null)
        {
            var preset = ShowcaseVoicePresetCatalog.GetById(presetId);
            var dims = GetForPreset(preset.Id);
            if (!string.IsNullOrWhiteSpace(voiceAgeId))
            {
                dims.AgeId = NormalizeAgeId(voiceAgeId);
            }

            if (!string.IsNullOrWhiteSpace(voiceLanguageId))
            {
                dims.LanguageId = NormalizeLanguageId(voiceLanguageId);
            }

            var g = ShortGender(LabelOf(GenderOptions, dims.GenderId));
            var age = ShortAgeLabel(LabelOf(AgeOptions, dims.AgeId));
            var lang = ShortLanguage(LabelOf(LanguageOptions, dims.LanguageId));
            if (ForeignLanguagePresetIds.ContainsKey(dims.LanguageId))
            {
                return lang + " · " + ShortTone(LabelOf(ToneOptions, dims.ToneId));
            }

            if (dims.ToneId != Tone.Natural)
            {
                var t = ShortTone(LabelOf(ToneOptions, dims.ToneId));
                return g + " · " + lang + " · " + age + " · " + t;
            }

            return g + " · " + lang + " · " + age;
        }

        private static Dictionary<string, float> BuildAgeLengthMultipliers()
        {
            return new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [Age.Child3_7] = 1.18f,
                [Age.Child8_12] = 1.10f,
                [Age.Teen13_17] = 1.04f,
                [Age.Young18_25] = 0.97f,
                [Age.Adult26_35] = 1.00f,
                [Age.Adult36_45] = 1.04f,
                [Age.Middle46_55] = 1.08f,
                [Age.Senior56_65] = 1.12f,
                [Age.Senior66Plus] = 1.16f,
                [Age.Neutral] = 1.00f
            };
        }

        private static float GetLengthMultiplier(string ageId)
        {
            ageId = NormalizeAgeId(ageId);
            return AgeLengthMultiplier.TryGetValue(ageId, out var mult) ? mult : 1f;
        }

        private static string AgeCoarse(string ageId)
        {
            ageId = NormalizeAgeId(ageId);
            switch (ageId)
            {
                case Age.Child3_7:
                case Age.Child8_12:
                    return "child";
                case Age.Teen13_17:
                case Age.Young18_25:
                case Age.Adult26_35:
                    return "young";
                case Age.Adult36_45:
                case Age.Middle46_55:
                case Age.Senior56_65:
                case Age.Senior66Plus:
                    return "mature";
                default:
                    return "neutral";
            }
        }

        private static Dictionary<string, VoiceDimensionSet> BuildMap()
        {
            return new Dictionary<string, VoiceDimensionSet>(StringComparer.OrdinalIgnoreCase)
            {
                ["female_south_young"] = D(Gender.Female, Age.Adult26_35, Language.ViSouth, Tone.Natural),
                ["female_south_mature"] = D(Gender.Female, Age.Middle46_55, Language.ViSouth, Tone.Natural),
                ["female_north_young"] = D(Gender.Female, Age.Adult26_35, Language.ViNorth, Tone.Natural),
                ["female_north_mature"] = D(Gender.Female, Age.Middle46_55, Language.ViNorth, Tone.Natural),
                ["female_central_young"] = D(Gender.Female, Age.Adult26_35, Language.ViCentral, Tone.Natural),
                ["female_central_mature"] = D(Gender.Female, Age.Middle46_55, Language.ViCentral, Tone.Natural),
                ["female_general_young"] = D(Gender.Female, Age.Adult26_35, Language.ViGeneral, Tone.Natural),
                ["female_general_mature"] = D(Gender.Female, Age.Middle46_55, Language.ViGeneral, Tone.Natural),
                ["female_south_child"] = D(Gender.Female, Age.Child3_7, Language.ViSouth, Tone.Natural),
                ["female_north_child"] = D(Gender.Female, Age.Child3_7, Language.ViNorth, Tone.Natural),
                ["male_south_young"] = D(Gender.Male, Age.Adult26_35, Language.ViSouth, Tone.Natural),
                ["male_south_mature"] = D(Gender.Male, Age.Middle46_55, Language.ViSouth, Tone.Natural),
                ["male_north_young"] = D(Gender.Male, Age.Adult26_35, Language.ViNorth, Tone.Natural),
                ["male_central_young"] = D(Gender.Male, Age.Adult26_35, Language.ViCentral, Tone.Natural),
                ["male_general_young"] = D(Gender.Male, Age.Adult26_35, Language.ViGeneral, Tone.Natural),
                ["baby_boy"] = D(Gender.Male, Age.Child3_7, Language.ViGeneral, Tone.Playful),
                ["baby_girl_south"] = D(Gender.Female, Age.Child3_7, Language.ViSouth, Tone.Playful),
                ["warm_deep"] = D(Gender.Male, Age.Middle46_55, Language.ViSouth, Tone.WarmDeep),
                ["ethereal"] = D(Gender.Female, Age.Young18_25, Language.ViSouth, Tone.Ethereal),
                ["cheerful"] = D(Gender.Neutral, Age.Young18_25, Language.ViGeneral, Tone.Cheerful),
                ["philosophy"] = D(Gender.Neutral, Age.Middle46_55, Language.ViGeneral, Tone.Philosophy),
                ["sweet"] = D(Gender.Female, Age.Young18_25, Language.ViGeneral, Tone.Sweet),
                ["neutral_narrator"] = D(Gender.Neutral, Age.Neutral, Language.ViGeneral, Tone.Narrator)
            };
        }

        private static VoiceDimensionSet D(string g, string a, string l, string t) =>
            new VoiceDimensionSet { GenderId = g, AgeId = a, LanguageId = l, ToneId = t };

        private static VoiceDimensionSet Clone(VoiceDimensionSet s) =>
            new VoiceDimensionSet
            {
                GenderId = s.GenderId,
                AgeId = s.AgeId,
                LanguageId = s.LanguageId,
                ToneId = s.ToneId
            };

        private static int Score(VoiceDimensionSet preset, string g, string a, string l, string t)
        {
            var score = 0;
            if (preset.GenderId == g)
            {
                score += 4;
            }

            if (AgeCoarse(preset.AgeId) == AgeCoarse(a))
            {
                score += 3;
            }

            if (string.Equals(preset.AgeId, a, StringComparison.OrdinalIgnoreCase))
            {
                score += 2;
            }

            if (preset.LanguageId == l)
            {
                score += 4;
            }
            else if (LanguageCoarse(preset.LanguageId) == LanguageCoarse(l))
            {
                score += 2;
            }

            if (preset.ToneId == t)
            {
                score += 5;
            }

            score -= AgeDistanceRank(preset.AgeId, a);
            return score;
        }

        private static int AgeDistanceRank(string presetAge, string userAge)
        {
            var order = new[]
            {
                Age.Child3_7, Age.Child8_12, Age.Teen13_17, Age.Young18_25, Age.Adult26_35,
                Age.Adult36_45, Age.Middle46_55, Age.Senior56_65, Age.Senior66Plus, Age.Neutral
            };
            var pi = Array.FindIndex(order, x => string.Equals(x, presetAge, StringComparison.OrdinalIgnoreCase));
            var ui = Array.FindIndex(order, x => string.Equals(x, userAge, StringComparison.OrdinalIgnoreCase));
            if (pi < 0 || ui < 0)
            {
                return 0;
            }

            return Math.Abs(pi - ui);
        }

        private static string Norm(string id) => (id ?? string.Empty).Trim().ToLowerInvariant();

        private static string LabelOf(IReadOnlyList<VoiceDimensionChoice> options, string id)
        {
            return options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase))?.Label
                   ?? id;
        }

        private static string ShortGender(string label)
        {
            if (label.StartsWith("Trung tính", StringComparison.Ordinal))
            {
                return "TT";
            }

            return label;
        }

        private static string ShortAgeLabel(string label)
        {
            label = (label ?? string.Empty).Trim();
            if (label.StartsWith("Không", StringComparison.Ordinal))
            {
                return "mọi tuổi";
            }

            if (label.StartsWith("Người lớn tuổi", StringComparison.Ordinal))
            {
                return label.Replace("Người lớn tuổi ", "≥");
            }

            if (label.StartsWith("Trẻ em", StringComparison.Ordinal))
            {
                return label.Replace("Trẻ em ", "").Replace(" tuổi", "");
            }

            return label.Replace(" tuổi", "");
        }

        private static string LanguageCoarse(string languageId)
        {
            languageId = NormalizeLanguageId(languageId);
            if (languageId.StartsWith("vi_", StringComparison.Ordinal))
            {
                return "vi";
            }

            if (languageId.StartsWith("en_", StringComparison.Ordinal))
            {
                return "en";
            }

            return languageId;
        }

        private static string ShortLanguage(string label)
        {
            label = (label ?? string.Empty).Trim();
            if (label.StartsWith("Tiếng Việt — miền Nam", StringComparison.Ordinal))
            {
                return "MN";
            }

            if (label.StartsWith("Tiếng Việt — miền Bắc", StringComparison.Ordinal))
            {
                return "Bắc";
            }

            if (label.StartsWith("Tiếng Việt — miền Trung", StringComparison.Ordinal))
            {
                return "Trung";
            }

            if (label.StartsWith("Tiếng Việt", StringComparison.Ordinal))
            {
                return "VN chung";
            }

            if (label.Length <= 16)
            {
                return label;
            }

            return label.Substring(0, 14) + "…";
        }

        private static string ShortTone(string label)
        {
            if (label.Length <= 14)
            {
                return label;
            }

            return label.Substring(0, 12) + "…";
        }
    }
}
