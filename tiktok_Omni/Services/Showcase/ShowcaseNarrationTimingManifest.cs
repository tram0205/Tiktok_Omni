using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Metadata timing hook/thân — karaoke fallback khớp audio sau khi tua lúc render.</summary>
    public sealed class ShowcaseNarrationTimingManifest
    {
        public const string FileName = "narration.timing.json";

        public double SpeechStartSeconds { get; set; }

        public double HookAudioSeconds { get; set; }

        public double BodyAudioSeconds { get; set; }

        public string HookText { get; set; } = string.Empty;

        public string BodyText { get; set; } = string.Empty;

        public string FullText { get; set; } = string.Empty;

        public double TotalSpeechSeconds =>
            Math.Max(0d, SpeechStartSeconds) + Math.Max(0d, HookAudioSeconds) + Math.Max(0d, BodyAudioSeconds);

        public static string GetPath(string audioDirectory)
        {
            return Path.Combine(audioDirectory ?? string.Empty, FileName);
        }

        public static void Save(string audioDirectory, ShowcaseNarrationTimingManifest manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(audioDirectory))
            {
                return;
            }

            Directory.CreateDirectory(audioDirectory);
            var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(GetPath(audioDirectory), json, TextFileEncoding.Utf8NoBom);
        }

        public static ShowcaseNarrationTimingManifest TryLoad(string audioDirectory)
        {
            var path = GetPath(audioDirectory);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                return JsonConvert.DeserializeObject<ShowcaseNarrationTimingManifest>(json);
            }
            catch
            {
                return null;
            }
        }

        public static ShowcaseNarrationTimingManifest TryLoadFromNarrationPath(string narrationFilePath)
        {
            var dir = Path.GetDirectoryName(narrationFilePath);
            return TryLoad(dir);
        }
    }
}
