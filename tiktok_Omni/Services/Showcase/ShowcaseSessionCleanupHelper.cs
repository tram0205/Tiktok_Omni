using System;
using System.IO;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Xóa video/audio thành phẩm cũ trong phiên Showcase trước khi render lại.</summary>
    public static class ShowcaseSessionCleanupHelper
    {
        public const string FinalVideoFileName = "with_cta_tail.mp4";

        public static void PrepareForFreshRender(
            string sessionBase,
            string previousOutputPath,
            Action<string> log,
            bool preserveSessionAudio = false)
        {
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                return;
            }

            string sessionFull;
            try
            {
                sessionFull = Path.GetFullPath(sessionBase);
            }
            catch
            {
                return;
            }

            if (!Directory.Exists(sessionFull))
            {
                return;
            }

            ClearPreviousVideoOutputs(sessionFull, previousOutputPath, log);
            if (!preserveSessionAudio)
            {
                ClearPreviousAudio(sessionFull, log);
            }
        }

        public static void ClearPreviousVideoOutputs(
            string sessionBase,
            string previousOutputPath,
            Action<string> log)
        {
            TryDeleteFile(Path.Combine(sessionBase, FinalVideoFileName), log);

            var tracked = (previousOutputPath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(tracked))
            {
                TryDeleteFileIfUnderSession(tracked, sessionBase, log);
            }

            var outputDir = ShowcaseNarrationCacheHelper.GetOutputDirectory(sessionBase);
            if (!Directory.Exists(outputDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(outputDir, "showcase_*.mp4"))
            {
                TryDeleteFileIfUnderSession(file, sessionBase, log);
            }

            var renderWork = Path.Combine(outputDir, "render_work");
            if (!Directory.Exists(renderWork))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(renderWork))
            {
                TryDeleteFileIfUnderSession(file, sessionBase, log);
            }
        }

        public static void ClearPreviousAudio(string sessionBase, Action<string> log)
        {
            ShowcaseNarrationCacheHelper.ClearCachedNarration(sessionBase);

            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            if (!Directory.Exists(audioDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(audioDir, "*.mp3"))
            {
                TryDeleteFileIfUnderSession(file, sessionBase, log);
            }
        }

        private static void TryDeleteFileIfUnderSession(string path, string sessionBase, Action<string> log)
        {
            var fullPath = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            try
            {
                fullPath = Path.GetFullPath(fullPath);
                if (!IsUnderDirectory(fullPath, sessionBase))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            TryDeleteFile(fullPath, log);
        }

        private static void TryDeleteFile(string path, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
                log?.Invoke("[Showcase] Đã xóa file cũ: " + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                log?.Invoke("[Showcase] Không xóa được file cũ (" + Path.GetFileName(path) + "): " + ex.Message);
            }
        }

        private static bool IsUnderDirectory(string filePath, string rootDir)
        {
            var full = Path.GetFullPath(filePath);
            var root = Path.GetFullPath(rootDir);
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
