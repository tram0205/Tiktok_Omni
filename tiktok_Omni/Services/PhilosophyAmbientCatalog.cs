using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Loại tiếng đệm (SFX môi trường ~5%) cho tab Video Triết lý.</summary>
    public static class PhilosophyAmbientCatalog
    {
        public const string NoneKey = "none";

        private static readonly IReadOnlyList<(string Key, string Label)> Options = new[]
        {
            (NoneKey, "Không tiếng đệm"),
            ("rain", "Mưa nhẹ"),
            ("wind", "Gió nhẹ"),
            ("forest", "Rừng / chim"),
            ("ocean", "Sóng biển"),
            ("city", "Thành phố"),
            ("fire", "Lửa ấm"),
            ("night", "Đêm yên"),
            ("thunder", "Sấm xa"),
            ("piano", "Piano pad")
        };

        public static IReadOnlyList<string> AllKeys { get; } = Options.Select(o => o.Key).ToList();

        public static string GetLabel(string key)
        {
            var normalized = NormalizeKey(key);
            foreach (var opt in Options)
            {
                if (string.Equals(opt.Key, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return opt.Label;
                }
            }

            if (IsMediaFileName(normalized))
            {
                return "SFX: " + normalized;
            }

            return string.IsNullOrEmpty(normalized) || normalized == NoneKey
                ? "Không tiếng đệm"
                : normalized;
        }

        public static bool IsMediaFileName(string value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(v))
            {
                return false;
            }

            var ext = Path.GetExtension(v);
            return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNoneKey(string key)
        {
            return OmniAudioLibrary.IsNoneId(key);
        }

        public static string NormalizeKey(string raw)
        {
            var trimmed = (raw ?? string.Empty).Trim();
            if (IsMediaFileName(trimmed))
            {
                return Path.GetFileName(trimmed);
            }

            var v = trimmed.ToLowerInvariant();
            if (string.IsNullOrEmpty(v) || v == "khong" || v == "no" || v == "off")
            {
                return NoneKey;
            }

            foreach (var opt in Options)
            {
                if (string.Equals(opt.Key, v, StringComparison.OrdinalIgnoreCase))
                {
                    return opt.Key;
                }
            }

            if (v.Contains("mua") || v.Contains("rain"))
            {
                return "rain";
            }

            if (v.Contains("gio") || v.Contains("wind"))
            {
                return "wind";
            }

            if (v.Contains("rung") || v.Contains("forest") || v.Contains("bird"))
            {
                return "forest";
            }

            if (v.Contains("bien") || v.Contains("ocean") || v.Contains("wave"))
            {
                return "ocean";
            }

            if (v.Contains("thanh pho") || v.Contains("city") || v.Contains("urban"))
            {
                return "city";
            }

            if (v.Contains("lua") || v.Contains("fire"))
            {
                return "fire";
            }

            if (v.Contains("dem") || v.Contains("night"))
            {
                return "night";
            }

            if (v.Contains("sam") || v.Contains("thunder"))
            {
                return "thunder";
            }

            if (v.Contains("piano"))
            {
                return "piano";
            }

            return NoneKey;
        }

        /// <summary>Gợi ý tiếng đệm khi Gemini không trả field ambient.</summary>
        public static string SuggestForMood(string mood)
        {
            switch (NormalizePhilosophyMood(mood))
            {
                case "melancholic":
                    return "rain";
                case "calm":
                    return "wind";
                case "hopeful":
                    return "forest";
                case "intense":
                    return "thunder";
                case "reflective":
                    return "night";
                default:
                    return "wind";
            }
        }

        public static void ApplyGeminiAmbient(PhilosophyScriptItem item, string ambientFromGemini, AppSettings settings = null)
        {
            if (item == null)
            {
                return;
            }

            var suggestion = (ambientFromGemini ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                item.AmbientKey = NoneKey;
                return;
            }

            if (IsNoneKey(suggestion))
            {
                item.AmbientKey = NoneKey;
                return;
            }

            if (settings != null)
            {
                var sfxFile = PhilosophyBatchHelper.ResolveGeminiSfxFileName(settings, suggestion, item.Mood);
                if (!string.IsNullOrEmpty(sfxFile))
                {
                    item.AmbientKey = sfxFile;
                    return;
                }
            }

            item.AmbientKey = NormalizeKey(suggestion);
        }

        public static void EnsureRowDefault(PhilosophyScriptItem item)
        {
            if (item == null)
            {
                return;
            }

            item.AmbientKey = string.IsNullOrWhiteSpace(item.AmbientKey)
                ? SuggestForMood(item.Mood)
                : NormalizeKey(item.AmbientKey);
        }

        /// <summary>Thư mục chứa file ambient: Assets\{profile}\ambient\{key} hoặc Assets\Ambient\{key}.</summary>
        public static string ResolveAmbientFolder(string ambientKey, string profileName)
        {
            var key = NormalizeKey(ambientKey);
            if (key == NoneKey)
            {
                return string.Empty;
            }

            var profileRoot = PhilosophyProfileAssets.GetAssetsRoot(profileName);
            var candidates = new[]
            {
                Path.Combine(profileRoot, "ambient", key),
                Path.Combine(profileRoot, "Ambient", key),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "Assets", "Ambient", key),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "Assets", "ambient", key)
            };

            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir))
                {
                    return dir;
                }
            }

            return candidates[2];
        }

        /// <summary>Đường dẫn file tiếng đệm: ưu tiên SFX dùng chung, sau đó thư mục legacy theo key.</summary>
        public static string ResolveAmbientMediaPath(string ambientKey, string profileName, AppSettings settings, string mood = null)
        {
            var key = (ambientKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key) || IsNoneKey(key))
            {
                return string.Empty;
            }

            if (IsMediaFileName(key) && settings != null)
            {
                var sfxPath = OmniAudioLibrary.ResolveSfxFilePath(key, settings);
                if (!string.IsNullOrWhiteSpace(sfxPath) && File.Exists(sfxPath))
                {
                    return sfxPath;
                }
            }

            var folder = ResolveAmbientFolder(key, profileName);
            return PickAmbientFileFromFolder(folder, mood);
        }

        private static string PickAmbientFileFromFolder(string folder, string mood)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return string.Empty;
            }

            var files = new List<string>();
            foreach (var pattern in new[] { "*.mp3", "*.wav", "*.m4a", "*.ogg" })
            {
                files.AddRange(Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly));
            }

            if (files.Count == 0)
            {
                return string.Empty;
            }

            var key = (mood ?? string.Empty).ToLowerInvariant();
            var match = files.FirstOrDefault(f =>
                Path.GetFileName(f).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            return match ?? files[0];
        }

        private static string NormalizePhilosophyMood(string mood)
        {
            var m = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var valid = new[] { "calm", "hopeful", "melancholic", "intense", "reflective", "sad" };
            return valid.Contains(m) ? m : "reflective";
        }
    }
}
