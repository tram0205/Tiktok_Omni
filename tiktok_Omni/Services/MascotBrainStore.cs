using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>DNA nhân vật Mascot theo profile — Assets\{Profile}\MascotBrain.json</summary>
    public sealed class MascotBrainStore
    {
        public const string FileName = "MascotBrain.json";

        public string ProfileName { get; set; } = "default";
        public string Personality { get; set; } = string.Empty;
        public string Tone { get; set; } = "vui, gần gũi";
        public string VoiceGender { get; set; } = "nữ";
        public string VisualStyle { get; set; } = string.Empty;
        public string Catchphrase { get; set; } = string.Empty;
        public string Taboos { get; set; } = "không toxic, không chính trị";

        public static string GetBrainPath(string profileName)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            return Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), FileName);
        }

        public static MascotBrainStore LoadOrCreate(string profileName, string fallbackMascotStyle = null)
        {
            var path = GetBrainPath(profileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                    var brain = JsonConvert.DeserializeObject<MascotBrainStore>(json) ?? new MascotBrainStore();
                    brain.ProfileName = ProfileScopedPaths.ResolveProfileName(profileName);
                    if (string.IsNullOrWhiteSpace(brain.VisualStyle) && !string.IsNullOrWhiteSpace(fallbackMascotStyle))
                    {
                        brain.VisualStyle = fallbackMascotStyle.Trim();
                    }

                    return brain;
                }
                catch
                {
                }
            }

            var created = new MascotBrainStore
            {
                ProfileName = ProfileScopedPaths.ResolveProfileName(profileName),
                Personality = string.IsNullOrWhiteSpace(fallbackMascotStyle)
                    ? "Linh vật thân thiện, năng lượng tích cực"
                    : fallbackMascotStyle.Trim(),
                VisualStyle = (fallbackMascotStyle ?? string.Empty).Trim()
            };
            Save(created);
            return created;
        }

        public static void Save(MascotBrainStore brain)
        {
            if (brain == null)
            {
                return;
            }

            var path = GetBrainPath(brain.ProfileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(brain, Formatting.Indented), TextFileEncoding.Utf8NoBom);
        }

        public string ToGeminiContextBlock()
        {
            return "MascotBrain (DNA kênh — bắt buộc tuân thủ xuyên suốt các job): " +
                   "Personality=" + (Personality ?? string.Empty).Trim() + "; " +
                   "Tone=" + (Tone ?? string.Empty).Trim() + "; " +
                   "VoiceGender=" + (VoiceGender ?? string.Empty).Trim() + "; " +
                   "VisualStyle=" + (VisualStyle ?? string.Empty).Trim() + "; " +
                   "Catchphrase=" + (Catchphrase ?? string.Empty).Trim() + "; " +
                   "Taboos=" + (Taboos ?? string.Empty).Trim() + ".";
        }
    }
}
