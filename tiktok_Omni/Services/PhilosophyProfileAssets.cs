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
    }
}
