using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Lựa chọn nền B-Roll trên lưới Video Triết lý.</summary>
    public static class PhilosophyBRollSelection
    {
        public const string RandomToken = "@random";

        private static readonly Random RandomPicker = new Random();

        public static bool IsRandomToken(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), RandomToken, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetDisplayLabel(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return "Chọn nền…";
            }

            if (IsRandomToken(trimmed))
            {
                return "Ngẫu nhiên";
            }

            if (File.Exists(trimmed))
            {
                return Path.GetFileName(trimmed);
            }

            trimmed = trimmed.TrimEnd('\\', '/');
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }

        public static string DescribeTooltip(string value, string profileName)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return "Bấm để chọn: Ngẫu nhiên hoặc duyệt video trong Assets\\Backgrounds / Assets\\" +
                       ProfileScopedPaths.ResolveProfileName(profileName) + "\\broll";
            }

            if (IsRandomToken(trimmed))
            {
                return "Render sẽ chọn ngẫu nhiên một video .mp4 từ kho Assets (Backgrounds + broll profile).";
            }

            if (File.Exists(trimmed))
            {
                return trimmed;
            }

            if (Directory.Exists(trimmed))
            {
                return "Thư mục: " + trimmed + " — render chọn ngẫu nhiên một file trong thư mục.";
            }

            return trimmed;
        }

        public static string GetBrowseInitialDirectory(string profileName)
        {
            foreach (var dir in EnumerateVideoPoolDirectories(profileName))
            {
                if (Directory.Exists(dir) && DirectoryContainsVideo(dir))
                {
                    return dir;
                }
            }

            var fallback = Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(profileName), "broll");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        public static string PickRandomVideoPath(string profileName)
        {
            var files = CollectVideoFiles(profileName);
            if (files.Count == 0)
            {
                return string.Empty;
            }

            lock (RandomPicker)
            {
                return files[RandomPicker.Next(files.Count)];
            }
        }

        public static bool TryValidateSelection(string value, string profileName, out string error)
        {
            error = string.Empty;
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "Chưa chọn nền — bấm cột «Nền» → «Ngẫu nhiên» hoặc «Chọn video…».";
                return false;
            }

            if (IsRandomToken(trimmed))
            {
                if (CollectVideoFiles(profileName).Count == 0)
                {
                    error = "Không có video nền — copy file .mp4 vào Assets\\Backgrounds hoặc Assets\\" +
                            ProfileScopedPaths.ResolveProfileName(profileName) + "\\broll.";
                    return false;
                }

                return true;
            }

            if (File.Exists(trimmed))
            {
                if (IsValidVideoFile(trimmed))
                {
                    return true;
                }

                error = "File video không hợp lệ hoặc quá nhỏ: " + trimmed;
                return false;
            }

            if (Directory.Exists(trimmed))
            {
                if (DirectoryContainsVideo(trimmed))
                {
                    return true;
                }

                error = "Thư mục không có file .mp4/.mov: " + trimmed;
                return false;
            }

            error = "Nền không tồn tại: " + trimmed;
            return false;
        }

        private static List<string> CollectVideoFiles(string profileName)
        {
            var files = new List<string>();
            foreach (var dir in EnumerateVideoPoolDirectories(profileName))
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                files.AddRange(Directory.GetFiles(dir, "*.mp4", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.GetFiles(dir, "*.mov", SearchOption.TopDirectoryOnly));
            }

            return files
                .Where(IsValidVideoFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public sealed class BrollCatalogEntry
        {
            public string FileName { get; set; } = string.Empty;

            public string FullPath { get; set; } = string.Empty;

            public string Category { get; set; } = string.Empty;
        }

        /// <summary>Liệt kê toàn bộ video B-roll kèm tên file cho Gemini gợi ý.</summary>
        public static List<BrollCatalogEntry> EnumerateVideoCatalog(string profileName)
        {
            return CollectVideoFiles(profileName)
                .Select(path => new BrollCatalogEntry
                {
                    FileName = Path.GetFileName(path) ?? string.Empty,
                    FullPath = path,
                    Category = DescribeVideoCategory(path, profileName)
                })
                .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Ánh xạ tên file Gemini gợi ý → đường dẫn video hợp lệ.</summary>
        public static string ResolveGeminiBrollFileName(string profileName, string suggestedFileName)
        {
            var suggestion = (suggestedFileName ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(suggestion) || IsRandomToken(suggestion))
            {
                return RandomToken;
            }

            suggestion = Path.GetFileName(suggestion);
            var catalog = EnumerateVideoCatalog(profileName);
            if (catalog.Count == 0)
            {
                return RandomToken;
            }

            var exact = catalog.FirstOrDefault(e =>
                string.Equals(e.FileName, suggestion, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact.FullPath;
            }

            var stem = Path.GetFileNameWithoutExtension(suggestion) ?? string.Empty;
            exact = catalog.FirstOrDefault(e =>
                string.Equals(Path.GetFileNameWithoutExtension(e.FileName), stem, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact.FullPath;
            }

            var partial = catalog.FirstOrDefault(e =>
                e.FileName.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrEmpty(stem)
                    && stem.IndexOf(Path.GetFileNameWithoutExtension(e.FileName) ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase) >= 0));
            if (partial != null)
            {
                return partial.FullPath;
            }

            return RandomToken;
        }

        private static string DescribeVideoCategory(string fullPath, string profileName)
        {
            var path = (fullPath ?? string.Empty).Replace('/', '\\');
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var profileBroll = Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), "broll");
            if (path.IndexOf(profileBroll, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var rel = path.Substring(profileBroll.Length).TrimStart('\\');
                var folder = Path.GetDirectoryName(rel);
                return string.IsNullOrEmpty(folder) ? "profile/broll" : "profile/broll/" + folder;
            }

            var shared = ProfileScopedPaths.GetSharedBackgroundsDirectory();
            if (path.IndexOf(shared, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var rel = path.Substring(shared.Length).TrimStart('\\');
                var folder = Path.GetDirectoryName(rel);
                return string.IsNullOrEmpty(folder) ? "shared" : "shared/" + folder;
            }

            return "assets";
        }

        private static IEnumerable<string> EnumerateVideoPoolDirectories(string profileName)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var sharedRoot = ProfileScopedPaths.GetSharedBackgroundsDirectory();
            yield return Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), "broll");
            yield return Path.Combine(sharedRoot, "Nature");
            yield return Path.Combine(sharedRoot, "Minimal");
            yield return sharedRoot;
            yield return PhilosophyProfileAssets.GetAssetsRoot(nick);
        }

        private static bool DirectoryContainsVideo(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }

            return Directory.GetFiles(directory, "*.mp4", SearchOption.TopDirectoryOnly)
                       .Concat(Directory.GetFiles(directory, "*.mov", SearchOption.TopDirectoryOnly))
                       .Any(IsValidVideoFile);
        }

        private static bool IsValidVideoFile(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 10_000L;
            }
            catch
            {
                return false;
            }
        }

        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

        public static bool IsImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ImageExtensions.Contains(ext);
        }

        /// <summary>Thư mục mặc định khi chọn ảnh nền (mode 3).</summary>
        public static string GetImageBrowseInitialDirectory(string profileName)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            foreach (var dir in new[]
                     {
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), "background-images"),
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), "images"),
                         PhilosophyProfileAssets.GetSharedBackgroundsRoot(),
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), "broll")
                     })
            {
                if (Directory.Exists(dir) && DirectoryContainsImage(dir))
                {
                    return dir;
                }
            }

            var fallback = Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(nick), "background-images");
            try
            {
                Directory.CreateDirectory(fallback);
            }
            catch
            {
                // ignored
            }

            return fallback;
        }

        public static string GetBackgroundDisplayLabel(string value, int visualMode)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return visualMode == PhilosophyVisualModes.PreRendered ? "Chọn ảnh…" : "Chọn nền…";
            }

            if (IsRandomToken(trimmed))
            {
                return "Ngẫu nhiên";
            }

            if (File.Exists(trimmed))
            {
                return Path.GetFileName(trimmed);
            }

            trimmed = trimmed.TrimEnd('\\', '/');
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }

        public static string DescribeBackgroundTooltip(string value, string profileName, int visualMode)
        {
            if (visualMode == PhilosophyVisualModes.PreRendered)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    return "Chế độ 3: chọn ảnh nền tham chiếu — Gemini dùng ảnh + quote để viết prompt phân cảnh.";
                }

                if (File.Exists(trimmed) && IsImageFile(trimmed))
                {
                    return "Ảnh nền: " + trimmed;
                }

                return trimmed;
            }

            return DescribeTooltip(value, profileName);
        }

        private static bool DirectoryContainsImage(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }

            return Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Any(IsImageFile);
        }
    }
}
