using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services
{
    /// <summary>Kho âm thanh dùng chung: Assets\Audio\Music và Assets\Audio\Sfx.</summary>
    public static class OmniAudioLibrary
    {
        public const string NoneId = "none";

        private static readonly string[] MusicPatterns = { "*.mp3", "*.wav", "*.m4a" };
        private static readonly string[] SfxPatterns = { "*.mp3", "*.wav", "*.m4a", "*.ogg" };

        public static string GetApplicationBaseDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory ?? ".";
        }

        public static string GetSharedAudioRoot(AppSettings settings = null)
        {
            _ = settings;
            return Path.Combine(GetApplicationBaseDirectory(), "Assets", "Audio");
        }

        public static string GetSharedMusicDirectory(AppSettings settings = null)
        {
            return Path.Combine(GetSharedAudioRoot(settings), "Music");
        }

        public static string GetSharedSfxDirectory(AppSettings settings = null)
        {
            return Path.Combine(GetSharedAudioRoot(settings), "Sfx");
        }

        public static void EnsureSharedDirectoriesExist(AppSettings settings = null)
        {
            Directory.CreateDirectory(GetSharedMusicDirectory(settings));
            Directory.CreateDirectory(GetSharedSfxDirectory(settings));
        }

        /// <summary>Thư mục nhạc — mở Explorer / label UI.</summary>
        public static string GetPrimaryMusicDirectory(AppSettings settings) =>
            GetSharedMusicDirectory(settings);

        /// <summary>Thư mục SFX «chính» — dùng chung mọi tab.</summary>
        public static string GetPrimarySfxDirectory(AppSettings settings = null)
        {
            return GetSharedSfxDirectory(settings);
        }

        public static IReadOnlyList<string> GetMusicSearchDirectories(AppSettings settings)
        {
            EnsureSharedDirectoriesExist(settings);
            return new[] { GetSharedMusicDirectory(settings) };
        }

        public static IReadOnlyList<string> GetSfxSearchDirectories(AppSettings settings)
        {
            EnsureSharedDirectoriesExist(settings);
            return new[] { GetSharedSfxDirectory(settings) };
        }

        public static List<string> ListMusicFileNames(AppSettings settings)
        {
            return ListMediaFileNames(GetMusicSearchDirectories(settings), MusicPatterns);
        }

        public static List<string> ListSfxFileNames(AppSettings settings)
        {
            return ListMediaFileNames(GetSfxSearchDirectories(settings), SfxPatterns);
        }

        private static List<string> ListMediaFileNames(IReadOnlyList<string> directories, string[] patterns)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in directories ?? Array.Empty<string>())
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var pattern in patterns)
                {
                    string[] files;
                    try
                    {
                        files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var path in files)
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

        public static string ResolveMusicFilePath(string fileNameOrId, AppSettings settings)
        {
            return ResolveMediaFilePath(fileNameOrId, GetMusicSearchDirectories(settings), MusicPatterns);
        }

        public static string ResolveSfxFilePath(string fileNameOrId, AppSettings settings)
        {
            return ResolveMediaFilePath(fileNameOrId, GetSfxSearchDirectories(settings), SfxPatterns);
        }

        private static string ResolveMediaFilePath(string fileNameOrId, IReadOnlyList<string> directories, string[] patterns)
        {
            var want = (fileNameOrId ?? string.Empty).Trim();
            if (want.Length == 0 || IsNoneId(want))
            {
                return string.Empty;
            }

            if (want.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && want.Contains("."))
            {
                foreach (var dir in directories)
                {
                    var exact = Path.Combine(dir, want);
                    if (File.Exists(exact))
                    {
                        return exact;
                    }
                }
            }

            var id = NormalizeId(want);
            foreach (var name in ListMediaFileNames(directories, patterns))
            {
                if (string.Equals(NormalizeId(Path.GetFileNameWithoutExtension(name)), id, StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveMediaFilePath(name, directories, patterns);
                }
            }

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var ext in patterns.Select(p => p.TrimStart('*')))
                {
                    var candidate = Path.Combine(dir, want + ext);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return string.Empty;
        }

        public static string ResolveMusicIdToFileName(AppSettings settings, string musicId)
        {
            if (IsNoneId(musicId))
            {
                return string.Empty;
            }

            var path = ResolveMusicFilePath(musicId, settings);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path);
        }

        public static string BuildGeminiMusicIdList(AppSettings settings)
        {
            var names = ListMusicFileNames(settings);
            if (names.Count == 0)
            {
                return "none (chưa có file — đặt .mp3/.wav/.m4a vào Assets\\Audio\\Music)";
            }

            var lines = new List<string> { "none" };
            foreach (var name in names)
            {
                var id = NormalizeId(Path.GetFileNameWithoutExtension(name));
                lines.Add(id + " → " + name);
            }

            return string.Join("; ", lines);
        }

        public static bool IsNoneId(string value)
        {
            var v = (value ?? string.Empty).Trim();
            return v.Length == 0
                   || string.Equals(v, NoneId, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(v, "(none)", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(v, "không", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeId(string raw)
        {
            var s = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Length == 0)
            {
                return string.Empty;
            }

            s = Regex.Replace(s, @"[^a-z0-9]+", "_");
            s = Regex.Replace(s, @"_+", "_").Trim('_');
            return s;
        }

        public sealed class CopyAudioFilesResult
        {
            public int Copied { get; set; }

            public int Skipped { get; set; }

            public List<string> Messages { get; } = new List<string>();
        }

        /// <summary>Copy file vào thư mục nhạc/SFX — giữ nguyên tên (bỏ qua nếu trùng).</summary>
        public static CopyAudioFilesResult CopyFilesPreserveName(
            string targetDirectory,
            IEnumerable<string> sourceFiles,
            IReadOnlyList<string> allowedExtensions)
        {
            var result = new CopyAudioFilesResult();
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return result;
            }

            Directory.CreateDirectory(targetDirectory);
            var extensions = allowedExtensions ?? Array.Empty<string>();

            foreach (var sourceFile in sourceFiles ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                {
                    result.Skipped++;
                    continue;
                }

                var ext = Path.GetExtension(sourceFile);
                if (extensions.Count > 0
                    && !extensions.Any(s => string.Equals(s, ext, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Skipped++;
                    result.Messages.Add("Bỏ qua «" + Path.GetFileName(sourceFile) + "» — đuôi file không hỗ trợ.");
                    continue;
                }

                var fileName = Path.GetFileName(sourceFile);
                var dest = Path.Combine(targetDirectory, fileName);
                if (File.Exists(dest))
                {
                    if (FilesLikelyIdentical(sourceFile, dest))
                    {
                        result.Skipped++;
                        result.Messages.Add("«" + fileName + "» đã có trong thư viện (cùng kích thước).");
                    }
                    else
                    {
                        result.Skipped++;
                        result.Messages.Add("«" + fileName + "» đã tồn tại — giữ file cũ, không ghi đè.");
                    }

                    continue;
                }

                try
                {
                    File.Copy(sourceFile, dest, overwrite: false);
                    result.Copied++;
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Messages.Add("Lỗi copy «" + fileName + "»: " + ex.Message);
                }
            }

            return result;
        }

        public static CopyAudioFilesResult CopyMusicFilesPreserveName(AppSettings settings, IEnumerable<string> sourceFiles) =>
            CopyFilesPreserveName(GetSharedMusicDirectory(settings), sourceFiles, MusicPatterns.Select(p => p.TrimStart('*')).ToList());

        public static CopyAudioFilesResult CopySfxFilesPreserveName(AppSettings settings, IEnumerable<string> sourceFiles) =>
            CopyFilesPreserveName(GetSharedSfxDirectory(settings), sourceFiles, SfxPatterns.Select(p => p.TrimStart('*')).ToList());

        private static bool FilesLikelyIdentical(string a, string b)
        {
            try
            {
                var fa = new FileInfo(a);
                var fb = new FileInfo(b);
                return fa.Exists && fb.Exists && fa.Length == fb.Length;
            }
            catch
            {
                return false;
            }
        }
    }
}
