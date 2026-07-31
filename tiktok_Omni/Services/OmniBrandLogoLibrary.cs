using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Kho logo dùng chung toàn app: Assets\Logos\</summary>
    public static class OmniBrandLogoLibrary
    {
        public const string DisplayRelativePath = @"Assets\Logos";

        private static readonly string[] LogoPatterns = { "*.png", "*.jpg", "*.jpeg", "*.webp" };

        private static readonly string[] PreferredFileNames =
        {
            "logo.png", "logo.jpg", "logo.jpeg", "logo.webp",
            "brand.png", "brand.jpg", "overlay.png", "overlay.jpg"
        };

        public static string GetApplicationBaseDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory ?? ".";
        }

        public static string GetSharedLogosDirectory(AppSettings settings = null)
        {
            _ = settings;
            return Path.Combine(GetApplicationBaseDirectory(), "Assets", "Logos");
        }

        public static string EnsureSharedLogosDirectory(AppSettings settings = null)
        {
            var dir = GetSharedLogosDirectory(settings);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static List<string> ListLogoFileNames(AppSettings settings = null)
        {
            EnsureSharedLogosDirectory(settings);
            return ListLogoFiles(GetSharedLogosDirectory(settings))
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int CountLogoFiles(AppSettings settings = null)
        {
            return ListLogoFiles(GetSharedLogosDirectory(settings)).Count;
        }

        public static string ResolveLogoFilePath(string fileNameOrPath, AppSettings settings = null)
        {
            var want = (fileNameOrPath ?? string.Empty).Trim();
            if (want.Length == 0)
            {
                return string.Empty;
            }

            if (File.Exists(want))
            {
                return Path.GetFullPath(want);
            }

            var sharedDir = EnsureSharedLogosDirectory(settings);
            var fileName = Path.GetFileName(want);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var exact = Path.Combine(sharedDir, fileName);
                if (File.Exists(exact))
                {
                    return exact;
                }
            }

            return string.Empty;
        }

        public static string TryPickDefaultLogo(AppSettings settings = null)
        {
            var dir = EnsureSharedLogosDirectory(settings);
            foreach (var name in PreferredFileNames)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return ListLogoFiles(dir).FirstOrDefault() ?? string.Empty;
        }

        public static string NormalizeStoredReference(string selectedPath, AppSettings settings = null)
        {
            var selected = (selectedPath ?? string.Empty).Trim();
            if (selected.Length == 0)
            {
                return string.Empty;
            }

            var full = File.Exists(selected) ? Path.GetFullPath(selected) : selected;
            var sharedDir = Path.GetFullPath(EnsureSharedLogosDirectory(settings));
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(dir)
                && string.Equals(Path.GetFullPath(dir), sharedDir, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(full) ?? string.Empty;
            }

            return full;
        }

        public static void FormatLibraryPanelLines(AppSettings settings, out string directoryLine, out string hintLine)
        {
            directoryLine = EnsureSharedLogosDirectory(settings);
            var count = CountLogoFiles(settings);
            hintLine = count <= 0
                ? "Chưa có file — thêm .png / .jpg / .webp vào thư mục này"
                : count + " file logo · .png / .jpg / .webp";
        }

        public static string FormatLibraryPanelText(AppSettings settings = null)
        {
            FormatLibraryPanelLines(settings, out var dir, out var hint);
            return dir + Environment.NewLine + hint;
        }

        private static List<string> ListLogoFiles(string directory)
        {
            var files = new List<string>();
            if (!Directory.Exists(directory))
            {
                return files;
            }

            foreach (var pattern in LogoPatterns)
            {
                try
                {
                    files.AddRange(Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    // ignore unreadable folder entries
                }
            }

            return files
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
