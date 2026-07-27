using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Dữ liệu người dùng (lưới tab, draft, cài đặt) — lưu ngoài bin\Debug, không mất khi rebuild.
    /// </summary>
    public static class AppDataPaths
    {
        public const string AppFolderName = "tiktok_Omni";

        public static string PersistentRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);

        public static string LegacyBinRoot =>
            AppDomain.CurrentDomain.BaseDirectory ?? ".";

        public static string EnsurePersistentRoot()
        {
            var dir = PersistentRoot;
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string PersistentFile(string fileName) =>
            Path.Combine(EnsurePersistentRoot(), fileName);

        public static string PersistentPath(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return EnsurePersistentRoot();
            }

            return Path.Combine(new[] { EnsurePersistentRoot() }.Concat(segments).ToArray());
        }

        public static string LegacyBinFile(string fileName) =>
            Path.Combine(LegacyBinRoot, fileName);

        /// <summary>Ưu tiên file persistent; nếu chưa có thì đọc từ exe/bin cũ (migrate).</summary>
        public static string ResolveReadableJsonPath(string fileName, out bool migrateFromLegacy)
        {
            migrateFromLegacy = false;
            var persistent = PersistentFile(fileName);
            if (File.Exists(persistent))
            {
                return persistent;
            }

            try
            {
                var exeLegacy = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
                    fileName);
                if (File.Exists(exeLegacy))
                {
                    migrateFromLegacy = true;
                    return exeLegacy;
                }
            }
            catch
            {
                // ignored
            }

            var binLegacy = LegacyBinFile(fileName);
            if (File.Exists(binLegacy))
            {
                migrateFromLegacy = true;
                return binLegacy;
            }

            return persistent;
        }

        public static void WriteJson(string fileName, string json)
        {
            var path = PersistentFile(fileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            WriteAllTextAtomic(path, json ?? string.Empty);
        }

        /// <summary>
        /// Ghi atomic + retry khi file đang bị khóa (progress save / ClearAsync chồng nhau).
        /// </summary>
        public static void WriteAllTextAtomic(string path, string contents)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path is required.", nameof(path));
            }

            var payload = contents ?? string.Empty;
            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            const int maxAttempts = 12;
            Exception lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.WriteAllText(tempPath, payload, TextFileEncoding.Utf8NoBom);
                    ReplaceFileWithRetry(tempPath, path);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    lastError = ex;
                    TryDeleteIfExists(tempPath);
                    if (attempt >= maxAttempts)
                    {
                        break;
                    }

                    System.Threading.Thread.Sleep(40 * attempt);
                }
            }

            throw new IOException(
                "Không ghi được file '" + path + "' sau " + maxAttempts + " lần thử.",
                lastError);
        }

        private static void ReplaceFileWithRetry(string sourceTempPath, string destinationPath)
        {
            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Replace(sourceTempPath, destinationPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(sourceTempPath, destinationPath);
                }

                return;
            }
            catch (IOException)
            {
                // Fallback: xóa đích rồi move (khi Replace bị khóa đọc).
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(sourceTempPath, destinationPath);
        }

        public static void TryDeleteLegacyJson(string fileName)
        {
            TryDeleteIfExists(LegacyBinFile(fileName));
            try
            {
                var exeLegacy = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
                    fileName);
                TryDeleteIfExists(exeLegacy);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>AvatarVault — ảnh mascot / identity pack theo profile.</summary>
        public static string ResolveAvatarVaultRoot()
        {
            var persistent = Path.Combine(EnsurePersistentRoot(), "AvatarVault");
            var legacy = Path.Combine(LegacyBinRoot, "AvatarVault");

            if (!DirectoryHasEntries(persistent) && DirectoryHasEntries(legacy))
            {
                CopyDirectoryMerge(legacy, persistent);
            }

            Directory.CreateDirectory(persistent);
            return persistent;
        }

        private static bool DirectoryHasEntries(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return false;
            }

            try
            {
                return Directory.EnumerateFileSystemEntries(dir).Any();
            }
            catch
            {
                return false;
            }
        }

        private static void CopyDirectoryMerge(string source, string dest)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(dest, relative);
                var targetDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                if (!File.Exists(target))
                {
                    File.Copy(file, target, overwrite: false);
                }
            }
        }

        private static void TryDeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
