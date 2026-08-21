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
            _ = profileName;
            return OmniAudioLibrary.GetMusicSearchDirectories(settings);
        }

        /// <summary>Liệt kê tên file nhạc từ kho dùng chung Assets\Audio\Music.</summary>
        public static List<string> EnumerateMusicFileNames(string profileName, AppSettings settings)
        {
            _ = profileName;
            OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);
            return OmniAudioLibrary.ListMusicFileNames(settings);
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

            var shared = OmniAudioLibrary.ResolveMusicFilePath(sel, settings);
            if (!string.IsNullOrWhiteSpace(shared))
            {
                return shared;
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
            _ = profileName;
            var names = OmniAudioLibrary.ListMusicFileNames(null);
            if (names.Count == 0)
            {
                return string.Empty;
            }

            var moodKey = (mood ?? string.Empty).Trim().ToLowerInvariant();
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(moodKey)
                    && name.IndexOf(moodKey, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var path = OmniAudioLibrary.ResolveMusicFilePath(name, null);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }

            foreach (var name in names)
            {
                if (name.IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var path = OmniAudioLibrary.ResolveMusicFilePath(name, null);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }

            return OmniAudioLibrary.ResolveMusicFilePath(names[0], null);
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

        /// <summary>Kho B-roll dùng chung mọi profile Video Quote: <c>Assets\Backgrounds\</c>.</summary>
        public static string GetBrollLibraryDirectory(string profileName = null)
        {
            _ = profileName;
            return ProfileScopedPaths.GetSharedBackgroundsDirectory();
        }

        public static string EnsureBrollLibraryDirectory(string profileName = null)
        {
            return ProfileScopedPaths.GetSharedBackgroundsDirectory(ensureExists: true);
        }

        /// <summary>Assets\{profile}\background-images\ — ảnh tham chiếu Veo I2V / mascot.</summary>
        public static string GetMascotImageLibraryDirectory(string profileName)
        {
            return Path.Combine(GetAssetsRoot(profileName), "background-images");
        }

        public static string EnsureMascotImageLibraryDirectory(string profileName)
        {
            var dir = GetMascotImageLibraryDirectory(profileName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Assets\{profile}\zoom-images\ — thư viện ảnh Ken Burns zoom (per-quote).</summary>
        public static string GetZoomImageLibraryDirectory(string profileName)
        {
            return Path.Combine(GetAssetsRoot(profileName), "zoom-images");
        }

        public static string EnsureZoomImageLibraryDirectory(string profileName)
        {
            var dir = GetZoomImageLibraryDirectory(profileName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
