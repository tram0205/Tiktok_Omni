using System;
using System.IO;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Thư mục clip chờ render/ghép video — AI (scene_XX) + quay tay (tên gốc).</summary>
    internal static class ShowcaseRenderClipsPaths
    {
        public const string FolderName = "clips_render";

        public const string LegacyFolderName = "veo_clips";

        public static string Combine(string sessionBaseDir) =>
            string.IsNullOrWhiteSpace(sessionBaseDir)
                ? string.Empty
                : Path.Combine(sessionBaseDir.Trim(), FolderName);

        public static string CombineLegacy(string sessionBaseDir) =>
            string.IsNullOrWhiteSpace(sessionBaseDir)
                ? string.Empty
                : Path.Combine(sessionBaseDir.Trim(), LegacyFolderName);

        /// <summary>Đường dẫn thư mục clip render — ưu tiên <see cref="FolderName"/>, fallback legacy, tùy chọn migrate.</summary>
        public static string ResolveDirectory(string sessionBaseDir, bool createIfMissing, bool migrateLegacy = true)
        {
            var baseDir = (sessionBaseDir ?? string.Empty).Trim();
            if (baseDir.Length == 0)
            {
                return string.Empty;
            }

            var primary = Combine(baseDir);
            var legacy = CombineLegacy(baseDir);

            if (migrateLegacy && !Directory.Exists(primary) && Directory.Exists(legacy))
            {
                try
                {
                    Directory.Move(legacy, primary);
                }
                catch
                {
                    return legacy;
                }
            }

            if (Directory.Exists(primary))
            {
                return primary;
            }

            if (Directory.Exists(legacy))
            {
                return legacy;
            }

            if (createIfMissing)
            {
                Directory.CreateDirectory(primary);
            }

            return primary;
        }

        public static bool IsRenderClipsDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            var name = Path.GetFileName(directoryPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(name, FolderName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, LegacyFolderName, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetDisplayLabel() => FolderName;
    }
}
