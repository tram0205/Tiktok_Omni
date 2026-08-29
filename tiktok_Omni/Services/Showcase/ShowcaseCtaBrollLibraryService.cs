using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Kho clip quay tay do người dùng tự chọn/duyệt — mỗi loại SP một thư mục con trong Assets\CtaBRolls\.</summary>
    public static class ShowcaseCtaBrollLibraryService
    {
        public const string SharedLibraryId = "shared";

        private static readonly string[] SupportedExtensions = { ".mp4", ".mov", ".webm", ".mkv" };

        public static string NormalizeLibrarySegment(string segment)
        {
            var id = ProfileScopedPaths.SanitizeSegment((segment ?? string.Empty).Trim());
            return id.Length == 0 ? string.Empty : id.ToLowerInvariant();
        }

        public static string NormalizeLibraryPath(string libraryPath)
        {
            var raw = (libraryPath ?? string.Empty).Trim().Replace('\\', '/');
            if (raw.Length == 0)
            {
                return SharedLibraryId;
            }

            var parts = raw.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeLibrarySegment)
                .Where(p => p.Length > 0)
                .ToArray();
            return parts.Length == 0 ? SharedLibraryId : string.Join("/", parts);
        }

        public static string NormalizeLibraryId(string libraryId) => NormalizeLibraryPath(libraryId);

        public static string CombineLibraryPath(string parentLibraryId, string subLibraryId)
        {
            var parent = NormalizeLibraryPath(parentLibraryId);
            var sub = NormalizeLibrarySegment(subLibraryId);
            if (sub.Length == 0)
            {
                return parent;
            }

            return parent + "/" + sub;
        }

        public static string GetParentLibraryPath(string libraryPath)
        {
            var path = NormalizeLibraryPath(libraryPath);
            var slash = path.IndexOf('/');
            return slash < 0 ? path : path.Substring(0, slash);
        }

        public static string GetSubLibraryPath(string libraryPath)
        {
            var path = NormalizeLibraryPath(libraryPath);
            var slash = path.IndexOf('/');
            return slash < 0 ? string.Empty : path.Substring(slash + 1);
        }

        public static string GetCtaBRollsRootDirectory(bool ensureExists = false)
        {
            var dir = Path.Combine(ProfileScopedPaths.AppRoot, ProfileScopedPaths.AssetsFolderName, ProfileScopedPaths.CtaBRollsFolderName);
            if (ensureExists)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        public static string GetLibraryDirectory(string libraryId, bool ensureExists = false)
        {
            var relative = NormalizeLibraryPath(libraryId);
            var dir = GetCtaBRollsRootDirectory(false);
            foreach (var part in relative.Split('/'))
            {
                if (part.Length == 0)
                {
                    continue;
                }

                dir = Path.Combine(dir, part);
            }

            if (ensureExists)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        public static string GetLibraryDisplayLabel(string libraryId)
        {
            var path = NormalizeLibraryPath(libraryId);
            var parent = GetParentLibraryPath(path);
            var sub = GetSubLibraryPath(path);
            var parentLabel = ShowcaseProductTypePresets.GetDisplayLabelForLibraryId(parent);
            if (sub.Length == 0)
            {
                return parentLabel;
            }

            var subLabel = ShowcaseCtaBrollSubLibraryPresets.GetDisplayLabel(parent, sub);
            return parentLabel + " · " + (subLabel.Length > 0 ? subLabel : sub);
        }

        public sealed class LibraryFolderEntry
        {
            public LibraryFolderEntry(string libraryId, string displayLabel, int clipCount)
            {
                LibraryId = libraryId ?? string.Empty;
                DisplayLabel = displayLabel ?? string.Empty;
                ClipCount = clipCount;
            }

            public string LibraryId { get; }

            public string DisplayLabel { get; }

            public int ClipCount { get; }

            public override string ToString() => DisplayLabel + " (" + ClipCount + " clip)";
        }

        public sealed class CopyClipsResult
        {
            public int Copied { get; set; }

            public int Skipped { get; set; }

            public List<string> Messages { get; } = new List<string>();
        }

        /// <summary>Danh sách thư mục thư viện — mỗi loại SP một folder (+ nhóm con nếu có).</summary>
        public static IReadOnlyList<LibraryFolderEntry> ListLibraryFolders(bool includeSubFolders = true)
        {
            var entries = new Dictionary<string, LibraryFolderEntry>(StringComparer.OrdinalIgnoreCase);

            void AddEntry(string libraryId, string displayLabel)
            {
                var id = NormalizeLibraryPath(libraryId);
                if (id.Length == 0)
                {
                    return;
                }

                if (entries.ContainsKey(id))
                {
                    return;
                }

                entries[id] = new LibraryFolderEntry(
                    id,
                    displayLabel,
                    EnumerateClipFiles(id).Count);
            }

            AddEntry(SharedLibraryId, ShowcaseProductTypePresets.GetDisplayLabelForLibraryId(SharedLibraryId));

            foreach (var preset in ShowcaseProductTypePresets.All)
            {
                if (string.Equals(preset.Id, ShowcaseProductTypePresets.Auto.Id, StringComparison.Ordinal)
                    || string.Equals(preset.Id, ShowcaseProductTypePresets.Custom.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                AddEntry(preset.Id, preset.DisplayLabel);
                if (!includeSubFolders)
                {
                    continue;
                }

                foreach (var sub in ShowcaseCtaBrollSubLibraryPresets.ForParent(preset.Id))
                {
                    if (sub.Id.Length == 0)
                    {
                        continue;
                    }

                    var fullId = CombineLibraryPath(preset.Id, sub.Id);
                    AddEntry(fullId, preset.DisplayLabel + " · " + sub.DisplayLabel);
                }
            }

            var root = GetCtaBRollsRootDirectory(false);
            if (Directory.Exists(root))
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var topId = NormalizeLibrarySegment(Path.GetFileName(dir));
                    if (topId.Length == 0 || entries.ContainsKey(topId))
                    {
                        continue;
                    }

                    AddEntry(topId, topId);

                    if (!includeSubFolders)
                    {
                        continue;
                    }

                    foreach (var subDir in Directory.EnumerateDirectories(dir))
                    {
                        var subId = NormalizeLibrarySegment(Path.GetFileName(subDir));
                        if (subId.Length == 0)
                        {
                            continue;
                        }

                        var fullId = CombineLibraryPath(topId, subId);
                        if (!entries.ContainsKey(fullId))
                        {
                            AddEntry(fullId, topId + " / " + subId);
                        }
                    }
                }
            }

            return entries.Values
                .OrderBy(e => e.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Copy clip vào thư viện — giữ nguyên tên file gốc (bỏ qua nếu trùng tên).</summary>
        public static CopyClipsResult CopyClipsPreserveName(string libraryId, IEnumerable<string> sourceFiles)
        {
            var result = new CopyClipsResult();
            if (sourceFiles == null)
            {
                return result;
            }

            var libraryDir = GetLibraryDirectory(libraryId, true);
            foreach (var sourceFile in sourceFiles)
            {
                if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                {
                    result.Skipped++;
                    continue;
                }

                var ext = Path.GetExtension(sourceFile);
                if (!SupportedExtensions.Any(s => string.Equals(s, ext, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Skipped++;
                    result.Messages.Add("Bỏ qua «" + Path.GetFileName(sourceFile) + "» — đuôi file không hỗ trợ.");
                    continue;
                }

                var fileName = Path.GetFileName(sourceFile);
                var dest = Path.Combine(libraryDir, fileName);
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

        /// <summary>Danh sách file clip thô trong thư viện (đã copy, giữ nguyên tên gốc) — dùng để duyệt/chọn thủ công.</summary>
        public static IReadOnlyList<string> EnumerateClipFiles(string libraryId = null)
        {
            var id = NormalizeLibraryPath(libraryId);
            var paths = new List<string>();
            paths.AddRange(EnumerateClipFilesInDirectory(GetLibraryDirectory(id, false)));
            if (UsesLegacyRootFallback(id))
            {
                paths.AddRange(EnumerateClipFilesInDirectory(GetCtaBRollsRootDirectory(false)));
            }

            return paths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static async System.Threading.Tasks.Task<double> ProbeDurationSecondsAsync(
            string clipPath,
            string ffprobeExecutable,
            System.Threading.CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
            {
                return 0d;
            }

            return await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobeExecutable,
                    clipPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static IReadOnlyList<string> EnumerateClipFilesInDirectory(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return Array.Empty<string>();
            }

            return Directory
                .EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                .Where(p => SupportedExtensions.Any(ext => string.Equals(ext, Path.GetExtension(p), StringComparison.OrdinalIgnoreCase)))
                .Where(p => IsDirectChildFile(dir, p))
                .Where(FileHasContent)
                .ToList();
        }

        private static bool UsesLegacyRootFallback(string libraryId) =>
            string.Equals(NormalizeLibraryPath(libraryId), SharedLibraryId, StringComparison.Ordinal);

        private static bool IsDirectChildFile(string parentDir, string filePath)
        {
            try
            {
                var parent = Path.GetFullPath(parentDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var fileDir = Path.GetFullPath(Path.GetDirectoryName(filePath) ?? string.Empty);
                return string.Equals(parent, fileDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool FileHasContent(string path)
        {
            try
            {
                return new FileInfo(path).Length > 2048L;
            }
            catch
            {
                return false;
            }
        }
    }
}
