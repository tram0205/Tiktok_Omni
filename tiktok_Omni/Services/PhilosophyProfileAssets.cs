using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Branding theo nick: Assets\{ProfileName}\ (nhạc, nền, style).</summary>
    public static class PhilosophyProfileAssets
    {
        public static string GetAssetsRoot(string profileName)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "Assets", nick);
        }

        public static AutomationProfile ResolveProfile(AppSettings settings, string profileName)
        {
            var target = ProfileScopedPaths.ResolveProfileName(profileName);
            if (settings?.Profiles == null || settings.Profiles.Count == 0)
            {
                return new AutomationProfile { Name = target };
            }

            var found = settings.Profiles.FirstOrDefault(p =>
                p != null && string.Equals(p.Name?.Trim(), target, StringComparison.OrdinalIgnoreCase));
            return found ?? new AutomationProfile { Name = target };
        }

        public static IEnumerable<string> GetMusicSearchDirectories(string profileName, AppSettings settings)
        {
            var root = GetAssetsRoot(profileName);
            yield return Path.Combine(root, "music");
            yield return root;
            yield return VideoReupRemixService.GetMusicLibraryDirectory(settings);
        }

        /// <summary>Liệt kê tên file nhạc (.mp3/.wav/.m4a) từ profile + thư viện Video reup.</summary>
        public static List<string> EnumerateMusicFileNames(string profileName, AppSettings settings)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in GetMusicSearchDirectories(profileName, settings))
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var ext in new[] { "*.mp3", "*.wav", "*.m4a" })
                {
                    foreach (var path in Directory.GetFiles(dir, ext, SearchOption.TopDirectoryOnly))
                    {
                        var name = Path.GetFileName(path);
                        if (!string.IsNullOrWhiteSpace(name) && !names.ContainsKey(name))
                        {
                            names[name] = path;
                        }
                    }
                }
            }

            return names.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Giá trị ô Nhạc: tên file, đường dẫn đầy đủ, hoặc thư mục (legacy).</summary>
        public static string ResolveMusicPath(string selection, string profileName, AppSettings settings, string mood)
        {
            var sel = (selection ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(sel))
            {
                return string.Empty;
            }

            if (File.Exists(sel))
            {
                return sel;
            }

            foreach (var dir in GetMusicSearchDirectories(profileName, settings))
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                var path = Path.Combine(dir, sel);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            if (Directory.Exists(sel))
            {
                return TryPickMusicFromDirectory(sel, mood);
            }

            return string.Empty;
        }

        private static string TryPickMusicFromDirectory(string folder, string mood)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return string.Empty;
            }

            var files = new List<string>();
            foreach (var ext in new[] { "*.mp3", "*.wav", "*.m4a" })
            {
                files.AddRange(Directory.GetFiles(folder, ext, SearchOption.TopDirectoryOnly));
            }

            if (files.Count == 0)
            {
                return string.Empty;
            }

            var key = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var match = files.FirstOrDefault(f =>
                Path.GetFileName(f).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            return match ?? files[0];
        }

        public static string TryPickMusicFile(string profileName, string mood)
        {
            var root = GetAssetsRoot(profileName);
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            var moodKey = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var candidates = new List<string>();

            void Scan(string dir)
            {
                if (!Directory.Exists(dir))
                {
                    return;
                }

                foreach (var ext in new[] { "*.mp3", "*.wav", "*.m4a" })
                {
                    candidates.AddRange(Directory.GetFiles(dir, ext, SearchOption.TopDirectoryOnly));
                }
            }

            Scan(Path.Combine(root, "music"));
            Scan(root);

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            var moodMatch = candidates.FirstOrDefault(f =>
                Path.GetFileName(f).IndexOf(moodKey, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(moodMatch))
            {
                return moodMatch;
            }

            var generic = candidates.FirstOrDefault(f =>
                Path.GetFileName(f).IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                Path.GetFileName(f).IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0);
            return generic ?? candidates[0];
        }

        public static string GetSharedBackgroundsRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "Assets", "Backgrounds");
        }

        /// <summary>Chọn ngẫu nhiên video .mp4 từ <see cref="ProfileScopedPaths.SharedBackgroundsFolderName"/>.</summary>
        public static string TryPickDynamicBackgroundVideo()
        {
            var root = ProfileScopedPaths.GetSharedBackgroundsDirectory();
            var pools = new[]
            {
                Path.Combine(root, "Nature"),
                Path.Combine(root, "Minimal"),
                root
            };

            var files = new List<string>();
            foreach (var dir in pools)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                files.AddRange(Directory.GetFiles(dir, "*.mp4", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.GetFiles(dir, "*.mov", SearchOption.TopDirectoryOnly));
            }

            files = files.Where(f => new FileInfo(f).Length > 10_000L).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0)
            {
                return string.Empty;
            }

            return files[new Random().Next(files.Count)];
        }

        public static string TryPickBackgroundVideo(string profileName)
        {
            var root = GetAssetsRoot(profileName);
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            foreach (var name in new[] { "background.mp4", "bg.mp4", "video_style.mp4", "style.mp4" })
            {
                var path = Path.Combine(root, name);
                if (File.Exists(path) && new FileInfo(path).Length > 10_000L)
                {
                    return path;
                }
            }

            var any = Directory.GetFiles(root, "*.mp4", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => new FileInfo(f).Length > 10_000L);
            return any ?? string.Empty;
        }

        public static string ResolveGradientColor(string videoStyle, string mood)
        {
            var style = (videoStyle ?? string.Empty).Trim().ToLowerInvariant();
            if (style.Contains("warm") || style.Contains("vang") || style.Contains("gold"))
            {
                return "0x2d1f0a";
            }

            if (style.Contains("neon") || style.Contains("cyber"))
            {
                return "0x0a1028";
            }

            if (style.Contains("dark") || style.Contains("toi"))
            {
                return "0x0a0a12";
            }

            return mood == "hopeful" ? "0x2d1f0a" : mood == "melancholic" ? "0x0f1419" : "0x1a1a2e";
        }

        public static string EnhanceVisualPrompt(string basePrompt, AutomationProfile profile)
        {
            var prompt = (basePrompt ?? string.Empty).Trim();
            var style = (profile?.VideoStyle ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(style))
            {
                return prompt;
            }

            return prompt + ", brand style: " + style + ", vertical 9:16 cinematic";
        }

        public static string TryPickBrandOverlayImage(string profileName)
        {
            var root = GetAssetsRoot(profileName);
            if (!Directory.Exists(root))
            {
                return string.Empty;
            }

            foreach (var name in new[] { "logo.png", "logo.jpg", "cta.png", "overlay.png", "brand.png" })
            {
                var path = Path.Combine(root, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        /// <summary>Thư mục mặc định đặt video phân cảnh tự làm (mode 3): Assets\{profile}\philosophy-scenes\</summary>
        public static string GetPreRenderedScenesDirectory(string profileName)
        {
            return Path.Combine(GetAssetsRoot(profileName), "philosophy-scenes");
        }

        /// <summary>Tạo thư mục philosophy-scenes nếu chưa có và trả đường dẫn tuyệt đối.</summary>
        public static string EnsurePreRenderedScenesDirectory(string profileName)
        {
            var dir = GetPreRenderedScenesDirectory(profileName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
