using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services.Mascot;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Quản lý thư mục phiên Showcase: source_images/, veo_clips/, output/, showcase_prompts.xlsx.</summary>
    public static class ShowcaseSessionService
    {
        private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".webm", ".mkv" };

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
        };

        public static ShowcaseSessionState CreateSession(string profileName, string productName, string storageRoot = null)
        {
            // Chỉ ghi đè storage root toàn cục khi có giá trị cụ thể — tránh xoá cấu hình đã nạp từ trước.
            if (!string.IsNullOrWhiteSpace(storageRoot))
            {
                ProfileScopedPaths.SetConfiguredStorageRoot(storageRoot);
            }

            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profileName);
            var baseDir = ProfileScopedPaths.CreateGeneratedSessionFolder(resolvedProfile, "Showcase");

            var state = new ShowcaseSessionState
            {
                BaseDir = baseDir,
                SourceImagesDir = Path.Combine(baseDir, "source_images"),
                ClipsDir = Path.Combine(baseDir, "veo_clips"),
                OutputDir = Path.Combine(baseDir, "output"),
                ProfileName = resolvedProfile,
                ProductName = (productName ?? string.Empty).Trim()
            };
            Directory.CreateDirectory(state.SourceImagesDir);
            Directory.CreateDirectory(state.ClipsDir);
            Directory.CreateDirectory(state.OutputDir);
            state.ExcelPath = Path.Combine(state.BaseDir, ShowcaseExcelHelper.BuildExcelFileName(resolvedProfile, state.ProductName));
            return state;
        }

        /// <summary>Khôi phục phiên đã lưu (draft hoặc trên dòng video) — null nếu thư mục clip không còn.</summary>
        public static ShowcaseSessionState RestoreSession(
            string profileName,
            string productName,
            string baseDir,
            string clipsDir)
        {
            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profileName);
            var trimmedProduct = (productName ?? string.Empty).Trim();
            var resolvedClips = (clipsDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedClips) || !Directory.Exists(resolvedClips))
            {
                return null;
            }

            var resolvedBase = (baseDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedBase))
            {
                resolvedBase = Directory.GetParent(resolvedClips)?.FullName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(resolvedBase) || !Directory.Exists(resolvedBase))
            {
                return null;
            }

            var state = new ShowcaseSessionState
            {
                BaseDir = resolvedBase,
                SourceImagesDir = Path.Combine(resolvedBase, "source_images"),
                ClipsDir = resolvedClips,
                OutputDir = Path.Combine(resolvedBase, "output"),
                ProfileName = resolvedProfile,
                ProductName = trimmedProduct,
                ExcelPath = Path.Combine(resolvedBase, ShowcaseExcelHelper.BuildExcelFileName(resolvedProfile, trimmedProduct))
            };
            Directory.CreateDirectory(state.SourceImagesDir);
            Directory.CreateDirectory(state.ClipsDir);
            Directory.CreateDirectory(state.OutputDir);
            return state;
        }

        private static bool TryNormalizePickPath(string pickPath, out string fullPath, out string fileName, out string directory)
        {
            fullPath = null;
            fileName = null;
            directory = null;
            if (string.IsNullOrWhiteSpace(pickPath))
            {
                return false;
            }

            try
            {
                fullPath = Path.GetFullPath(pickPath.Trim());
                fileName = Path.GetFileName(fullPath);
                directory = Path.GetDirectoryName(fullPath);
                return !string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(directory);
            }
            catch
            {
                return false;
            }
        }

        private static bool PathSameOriginAndFileName(string pathA, string pathB)
        {
            if (!TryNormalizePickPath(pathA, out var fullA, out var nameA, out var dirA)
                || !TryNormalizePickPath(pathB, out var fullB, out var nameB, out var dirB))
            {
                return false;
            }

            if (string.Equals(fullA, fullB, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(dirA, dirB, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Ảnh chọn từ máy trùng nguồn (thư mục) + tên file với cảnh đã có trên storyboard.</summary>
        public static bool SceneMatchesShowcasePickSource(AiVideoGenInputItem scene, string pickPath)
        {
            if (scene == null || string.IsNullOrWhiteSpace(pickPath))
            {
                return false;
            }

            var storedPick = (scene.ShowcaseLocalPickPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(storedPick) && PathSameOriginAndFileName(storedPick, pickPath))
            {
                return true;
            }

            var thumb = (scene.ThumbnailPath ?? scene.ImageUrl ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(thumb) && File.Exists(thumb) && PathSameOriginAndFileName(thumb, pickPath))
            {
                return true;
            }

            return false;
        }

        public static bool TryFindSceneIndexWithSameShowcasePickSource(
            IList<AiVideoGenInputItem> scenes,
            string pickPath,
            out int sceneIndexOneBased)
        {
            sceneIndexOneBased = 0;
            if (scenes == null || string.IsNullOrWhiteSpace(pickPath))
            {
                return false;
            }

            for (var i = 0; i < scenes.Count; i++)
            {
                if (SceneMatchesShowcasePickSource(scenes[i], pickPath))
                {
                    sceneIndexOneBased = i + 1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Đường dẫn ảnh gốc storyboard — <c>photo_XX</c> (clip video dùng <c>scene_XX.mp4</c> trong veo_clips).</summary>
        public static string GetSourceImageDestPath(ShowcaseSessionState session, int sceneIndexOneBased, string sourceFilePath)
        {
            if (session == null)
            {
                return null;
            }

            if (sceneIndexOneBased < 1)
            {
                sceneIndexOneBased = 1;
            }

            var ext = GuessImageExtension(sourceFilePath);
            return Path.Combine(session.SourceImagesDir, $"photo_{sceneIndexOneBased:D2}{ext}");
        }

        /// <summary>Đã có file ảnh gốc ở vị trí thứ tự này (photo_XX hoặc scene_XX cũ).</summary>
        public static bool TryGetExistingSourceImageAtIndex(ShowcaseSessionState session, int sceneIndexOneBased, out string existingPath)
        {
            existingPath = null;
            if (session == null || sceneIndexOneBased < 1)
            {
                return false;
            }

            if (!Directory.Exists(session.SourceImagesDir))
            {
                return false;
            }

            foreach (var pattern in new[] { $"photo_{sceneIndexOneBased:D2}.*", $"scene_{sceneIndexOneBased:D2}.*" })
            {
                var matches = Directory.GetFiles(session.SourceImagesDir, pattern);
                if (matches.Length > 0)
                {
                    existingPath = matches[0];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Copy ảnh chọn từ máy vào <c>source_images/photo_XX.*</c> — null nếu bỏ qua vì trùng và không ghi đè.</summary>
        public static string CopyLocalSceneImage(
            ShowcaseSessionState session,
            int sceneIndexOneBased,
            string sourceFilePath,
            bool overwriteExisting)
        {
            if (session == null || string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return sourceFilePath;
            }

            if (sceneIndexOneBased < 1)
            {
                sceneIndexOneBased = 1;
            }

            Directory.CreateDirectory(session.SourceImagesDir);
            var dest = GetSourceImageDestPath(session, sceneIndexOneBased, sourceFilePath);
            if (PathsEqual(sourceFilePath, dest))
            {
                return dest;
            }

            if (TryGetExistingSourceImageAtIndex(session, sceneIndexOneBased, out var existingAtSlot)
                && !string.IsNullOrWhiteSpace(existingAtSlot)
                && File.Exists(existingAtSlot))
            {
                if (!overwriteExisting)
                {
                    return null;
                }

                dest = GetSourceImageDestPath(session, sceneIndexOneBased, sourceFilePath);
                if (!PathsEqual(existingAtSlot, dest) && File.Exists(existingAtSlot))
                {
                    try
                    {
                        File.Delete(existingAtSlot);
                    }
                    catch
                    {
                        // vẫn thử ghi photo_XX
                    }
                }
            }
            else if (File.Exists(dest) && !overwriteExisting)
            {
                return null;
            }

            CopyFileWithSharedRead(sourceFilePath, dest);
            return dest;
        }

        public static string CopyLocalSceneImage(ShowcaseSessionState session, int sceneIndexOneBased, string sourceFilePath) =>
            CopyLocalSceneImage(session, sceneIndexOneBased, sourceFilePath, overwriteExisting: true);

        /// <summary>Copy clip chọn từ máy vào <c>veo_clips/scene_XX.*</c> — null nếu bỏ qua vì trùng và không ghi đè.</summary>
        public static string CopyLocalSceneClip(
            ShowcaseSessionState session,
            int sceneIndexOneBased,
            string sourceFilePath,
            bool overwriteExisting)
        {
            if (session == null || string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return null;
            }

            if (sceneIndexOneBased < 1)
            {
                sceneIndexOneBased = 1;
            }

            Directory.CreateDirectory(session.ClipsDir);
            var ext = Path.GetExtension(sourceFilePath);
            if (!IsVideoExtension(ext))
            {
                ext = ".mp4";
            }

            var dest = BuildSceneClipFilePath(session.ClipsDir, sceneIndexOneBased, ext);
            if (PathsEqual(sourceFilePath, dest))
            {
                return dest;
            }

            var existingAtSlot = DetectClipForOrder(session.ClipsDir, sceneIndexOneBased);
            if (!string.IsNullOrWhiteSpace(existingAtSlot) && File.Exists(existingAtSlot))
            {
                if (!overwriteExisting)
                {
                    return null;
                }

                if (!PathsEqual(existingAtSlot, dest))
                {
                    try
                    {
                        File.Delete(existingAtSlot);
                    }
                    catch
                    {
                        // vẫn thử ghi scene_XX
                    }
                }
            }
            else if (File.Exists(dest) && !overwriteExisting)
            {
                return null;
            }

            CopyFileWithSharedRead(sourceFilePath, dest);
            return dest;
        }

        public static string CopyLocalSceneClip(ShowcaseSessionState session, int sceneIndexOneBased, string sourceFilePath) =>
            CopyLocalSceneClip(session, sceneIndexOneBased, sourceFilePath, overwriteExisting: true);

        /// <summary>Đường dẫn chuẩn clip cảnh trong veo_clips — scene_01.mp4, scene_02.mp4, …</summary>
        public static string BuildSceneClipFilePath(string clipsDir, int sceneIndexOneBased, string extension = ".mp4")
        {
            if (sceneIndexOneBased < 1)
            {
                sceneIndexOneBased = 1;
            }

            var ext = (extension ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".mp4";
            }

            if (!ext.StartsWith(".", StringComparison.Ordinal))
            {
                ext = "." + ext;
            }

            return Path.Combine(clipsDir ?? string.Empty, "scene_" + sceneIndexOneBased.ToString("D2", CultureInfo.InvariantCulture) + ext);
        }

        private static bool PathsEqual(string pathA, string pathB)
        {
            if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(pathA),
                    Path.GetFullPath(pathB),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPathUnderDirectory(string filePath, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            try
            {
                var fileFull = Path.GetFullPath(filePath);
                var dirFull = Path.GetFullPath(directoryPath);
                if (!dirFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    dirFull += Path.DirectorySeparatorChar;
                }

                return fileFull.StartsWith(dirFull, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Copy file — đọc nguồn với FileShare.ReadWrite để tránh lỗi khi app/Explorer đang xem ảnh.</summary>
        private static void CopyFileWithSharedRead(string sourceFilePath, string destFilePath)
        {
            var destDir = Path.GetDirectoryName(destFilePath);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var tempPath = destFilePath + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                using (var src = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    src.CopyTo(dst);
                }

                if (File.Exists(destFilePath))
                {
                    File.Delete(destFilePath);
                }

                File.Move(tempPath, destFilePath);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                throw;
            }
        }

        /// <summary>Thư mục chứa file ảnh của storyboard — ưu tiên thư mục có nhiều cảnh nhất.</summary>
        public static string ResolveSceneImagesDirectory(IList<AiVideoGenInputItem> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return null;
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var scene in scenes)
            {
                var path = (scene?.ThumbnailPath ?? scene?.ImageUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    continue;
                }

                counts[dir] = counts.TryGetValue(dir, out var n) ? n + 1 : 1;
            }

            if (counts.Count == 0)
            {
                return null;
            }

            return counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>Đưa mọi ảnh cảnh vào <c>session.SourceImagesDir</c> (photo_01, …) và cập nhật đường dẫn trên item.</summary>
        public static void ImportScenesIntoSessionSourceImages(ShowcaseSessionState session, IList<AiVideoGenInputItem> scenes)
        {
            if (session == null || scenes == null || scenes.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(session.SourceImagesDir);
            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                if (scene == null)
                {
                    continue;
                }

                var path = (scene.ThumbnailPath ?? scene.ImageUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                if (IsPathUnderDirectory(path, session.SourceImagesDir))
                {
                    continue;
                }

                var dest = CopyLocalSceneImage(session, i + 1, path, overwriteExisting: true);
                scene.ImageUrl = dest;
                scene.ThumbnailPath = dest;
            }
        }

        /// <summary>Khôi phục phiên khi đã có <c>BaseDir</c> (không bắt buộc đã có clip).</summary>
        public static ShowcaseSessionState TryRestoreSessionFromBaseDir(string profileName, string productName, string baseDir)
        {
            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profileName);
            var trimmedProduct = (productName ?? string.Empty).Trim();
            var resolvedBase = (baseDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedBase) || !Directory.Exists(resolvedBase))
            {
                return null;
            }

            var state = new ShowcaseSessionState
            {
                BaseDir = resolvedBase,
                SourceImagesDir = Path.Combine(resolvedBase, "source_images"),
                ClipsDir = Path.Combine(resolvedBase, "veo_clips"),
                OutputDir = Path.Combine(resolvedBase, "output"),
                ProfileName = resolvedProfile,
                ProductName = trimmedProduct,
                ExcelPath = Path.Combine(resolvedBase, ShowcaseExcelHelper.BuildExcelFileName(resolvedProfile, trimmedProduct))
            };
            Directory.CreateDirectory(state.SourceImagesDir);
            Directory.CreateDirectory(state.ClipsDir);
            Directory.CreateDirectory(state.OutputDir);
            return state;
        }

        /// <summary>Suy ra phiên Showcase từ thư mục đang chứa file ảnh (thường là …/source_images).</summary>
        public static ShowcaseSessionState TryBuildSessionFromSceneImagesDirectory(
            string profileName,
            string productName,
            string imagesDirectory)
        {
            var dir = (imagesDirectory ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return null;
            }

            var fullDir = Path.GetFullPath(dir);
            string baseDir;
            if (fullDir.EndsWith("source_images", StringComparison.OrdinalIgnoreCase))
            {
                baseDir = Directory.GetParent(fullDir)?.FullName;
            }
            else
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
            {
                return null;
            }

            return TryRestoreSessionFromBaseDir(profileName, productName, baseDir);
        }

        public static bool DirectoryHasImageFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
            };

            return Directory.EnumerateFiles(directory)
                .Any(f => exts.Contains(Path.GetExtension(f)));
        }

        /// <summary>Tải/copy ảnh từng cảnh vào source_images/scene_XX.* — set lên <see cref="AiVideoGenInputItem.ThumbnailPath"/>.</summary>
        public static async Task DownloadSceneImagesAsync(
            ShowcaseSessionState session,
            IList<AiVideoGenInputItem> orderedScenes,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (session == null || orderedScenes == null)
            {
                return;
            }

            for (var i = 0; i < orderedScenes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scene = orderedScenes[i];
                if (scene == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(scene.ThumbnailPath) && File.Exists(scene.ThumbnailPath))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(scene.ImageUrl))
                {
                    continue;
                }

                var ext = GuessImageExtension(scene.ImageUrl);
                var localPath = GetSourceImageDestPath(session, i + 1, scene.ImageUrl);
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    localPath = Path.Combine(session.SourceImagesDir, $"photo_{i + 1:D2}{ext}");
                }
                try
                {
                    await MascotMediaHelper.DownloadAsync(scene.ImageUrl, localPath, cancellationToken).ConfigureAwait(false);
                    scene.ThumbnailPath = localPath;
                }
                catch (Exception ex)
                {
                    log?.Invoke("[Showcase] Không tải được ảnh cảnh " + (i + 1) + ": " + ex.Message);
                }
            }
        }

        /// <summary>Tìm file ảnh gốc theo thứ tự — <c>photo_XX</c> hoặc <c>scene_XX</c> trong source_images.</summary>
        public static string DetectSourceImageForOrder(string sourceImagesDir, int order)
        {
            if (order < 1 || string.IsNullOrWhiteSpace(sourceImagesDir) || !Directory.Exists(sourceImagesDir))
            {
                return null;
            }

            string resolvedDir;
            try
            {
                resolvedDir = Path.GetFullPath(sourceImagesDir.Trim());
            }
            catch
            {
                return null;
            }

            foreach (var prefix in new[]
                     {
                         "photo_" + order.ToString("D2", CultureInfo.InvariantCulture),
                         "scene_" + order.ToString("D2", CultureInfo.InvariantCulture)
                     })
            {
                foreach (var ext in ImageExtensions)
                {
                    var directPath = Path.Combine(resolvedDir, prefix + ext);
                    if (File.Exists(directPath))
                    {
                        return directPath;
                    }
                }

                string[] matches;
                try
                {
                    matches = Directory.GetFiles(resolvedDir, prefix + ".*");
                }
                catch
                {
                    matches = Array.Empty<string>();
                }

                foreach (var file in matches)
                {
                    if (ImageExtensions.Contains(Path.GetExtension(file)))
                    {
                        return file;
                    }
                }
            }

            return null;
        }

        private static bool TryResolveSceneSourceImagePath(
            string sourceImagesDir,
            int orderOneBased,
            AiVideoGenInputItem scene,
            out string resolvedPath)
        {
            resolvedPath = null;
            if (orderOneBased < 1)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(sourceImagesDir))
            {
                resolvedPath = DetectSourceImageForOrder(sourceImagesDir, orderOneBased);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    return true;
                }
            }

            var stored = (scene?.ThumbnailPath ?? scene?.ImageUrl ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(stored) && File.Exists(stored))
            {
                resolvedPath = stored;
                return true;
            }

            return false;
        }

        private static void ApplyResolvedSourceImagePath(AiVideoGenInputItem scene, string path)
        {
            if (scene == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            scene.ThumbnailPath = path;
            scene.ImageUrl = path;
        }

        /// <summary>Cập nhật đường dẫn ảnh từ source_images — trả về STT cảnh (1-based) không còn file ảnh.</summary>
        public static List<int> RefreshSourceImageStatus(
            string sourceImagesDir,
            IList<AiVideoGenInputItem> orderedScenes,
            Action<string> log = null)
        {
            var missing = new List<int>();
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                return missing;
            }

            for (var i = 0; i < orderedScenes.Count; i++)
            {
                var order = i + 1;
                var scene = orderedScenes[i];
                if (TryResolveSceneSourceImagePath(sourceImagesDir, order, scene, out var path))
                {
                    ApplyResolvedSourceImagePath(scene, path);
                    continue;
                }

                missing.Add(order);
            }

            if (missing.Count > 0 && log != null)
            {
                log(BuildSourceImageScanDiagnostic(sourceImagesDir, orderedScenes.Count, missing));
            }

            return missing;
        }

        /// <summary>Gỡ khỏi storyboard các cảnh không còn ảnh (vd. user xóa file trong Explorer) — trả về số cảnh đã gỡ.</summary>
        public static int PruneScenesMissingSourceImages(
            string sourceImagesDir,
            IList<AiVideoGenInputItem> orderedScenes,
            Action<string> log = null)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                return 0;
            }

            var missing = RefreshSourceImageStatus(sourceImagesDir, orderedScenes, log: null);
            if (missing.Count == 0)
            {
                return 0;
            }

            var missingSet = new HashSet<int>(missing);
            var removed = 0;
            for (var i = orderedScenes.Count - 1; i >= 0; i--)
            {
                if (!missingSet.Contains(i + 1))
                {
                    continue;
                }

                orderedScenes.RemoveAt(i);
                removed++;
            }

            if (removed > 0 && log != null)
            {
                log("[Showcase] Đã gỡ " + removed + " cảnh khỏi storyboard (ảnh không còn trong source_images). Còn "
                    + orderedScenes.Count + " cảnh.");
            }

            return removed;
        }

        private static string BuildSourceImageScanDiagnostic(string sourceImagesDir, int sceneCount, IList<int> missingOrders)
        {
            if (string.IsNullOrWhiteSpace(sourceImagesDir) || !Directory.Exists(sourceImagesDir))
            {
                return "[Showcase] source_images không tồn tại: " + (sourceImagesDir ?? string.Empty);
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(sourceImagesDir)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                    .ToArray();
            }
            catch (Exception ex)
            {
                return "[Showcase] Không đọc được source_images: " + ex.Message;
            }

            var names = files
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(8)
                .ToArray();
            var sample = names.Length == 0
                ? "(trống)"
                : string.Join(", ", names) + (files.Length > names.Length ? ", ..." : string.Empty);
            return "[Showcase] source_images có " + files.Length + " ảnh [" + sample + "] — storyboard thiếu ảnh "
                   + missingOrders.Count + "/" + sceneCount + " cảnh ("
                   + string.Join(", ", missingOrders) + ").";
        }

        /// <summary>Tìm file clip theo tên "scene_XX.*" trong thư mục veo_clips — null nếu chưa có.</summary>
        public static string DetectClipForOrder(string clipsDir, int order)
        {
            if (order < 1 || string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return null;
            }

            string resolvedClipsDir;
            try
            {
                resolvedClipsDir = Path.GetFullPath(clipsDir.Trim());
            }
            catch
            {
                return null;
            }

            var prefix = "scene_" + order.ToString("D2", CultureInfo.InvariantCulture);
            foreach (var ext in VideoExtensions)
            {
                var directPath = Path.Combine(resolvedClipsDir, prefix + ext);
                if (File.Exists(directPath))
                {
                    return directPath;
                }

                // Windows đôi khi lưu thành scene_01.mp4.mp4 khi copy/rename.
                var doubleExtPath = Path.Combine(resolvedClipsDir, prefix + ext + ext);
                if (File.Exists(doubleExtPath))
                {
                    return doubleExtPath;
                }
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(resolvedClipsDir);
            }
            catch
            {
                return null;
            }

            foreach (var file in files)
            {
                var baseName = NormalizeSceneBaseName(Path.GetFileName(file));
                if (!string.Equals(baseName, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsVideoExtension(Path.GetExtension(file)))
                {
                    continue;
                }

                return file;
            }

            return null;
        }

        /// <summary>scene_01.mp4.mp4 → scene_01 (bỏ lặp đuôi video).</summary>
        private static string NormalizeSceneBaseName(string fileName)
        {
            var current = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(current))
            {
                return string.Empty;
            }

            current = Path.GetFileNameWithoutExtension(current);
            while (!string.IsNullOrEmpty(current))
            {
                var trailingExt = Path.GetExtension(current);
                if (!IsVideoExtension(trailingExt))
                {
                    break;
                }

                current = Path.GetFileNameWithoutExtension(current);
            }

            return current ?? string.Empty;
        }

        /// <summary>Quét thư mục clip, gán <see cref="AiVideoGenInputItem.ClipPath"/> cho từng cảnh theo thứ tự — trả về danh sách STT còn thiếu clip.</summary>
        public static List<int> RefreshClipStatus(
            string clipsDir,
            IList<AiVideoGenInputItem> orderedScenes,
            Action<string> log = null)
        {
            var missing = new List<int>();
            if (orderedScenes == null)
            {
                return missing;
            }

            for (var i = 0; i < orderedScenes.Count; i++)
            {
                var order = i + 1;
                var clip = DetectClipForOrder(clipsDir, order);
                if (orderedScenes[i] != null)
                {
                    orderedScenes[i].ClipPath = clip ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(clip))
                {
                    missing.Add(order);
                }
            }

            if (missing.Count > 0 && log != null)
            {
                log(BuildClipScanDiagnostic(clipsDir, orderedScenes.Count, missing));
            }

            return missing;
        }

        private static bool IsVideoExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            foreach (var allowed in VideoExtensions)
            {
                if (string.Equals(extension, allowed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildClipScanDiagnostic(string clipsDir, int sceneCount, IList<int> missingOrders)
        {
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return "[Showcase] veo_clips không tồn tại: " + (clipsDir ?? string.Empty);
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(clipsDir);
            }
            catch (Exception ex)
            {
                return "[Showcase] Không đọc được veo_clips: " + ex.Message;
            }

            var names = files
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(8)
                .ToArray();
            var sample = names.Length == 0
                ? "(trống)"
                : string.Join(", ", names) + (files.Length > names.Length ? ", ..." : string.Empty);
            var hint = names.Any(n => n.EndsWith(".mp4.mp4", StringComparison.OrdinalIgnoreCase)
                                      || n.EndsWith(".mov.mov", StringComparison.OrdinalIgnoreCase))
                ? " Gợi ý: đổi tên bỏ đuôi lặp (vd. scene_01.mp4.mp4 → scene_01.mp4) — app đã hỗ trợ cả hai."
                : string.Empty;
            return "[Showcase] veo_clips có " + files.Length + " file [" + sample + "] — thiếu "
                   + missingOrders.Count + "/" + sceneCount + " cảnh ("
                   + string.Join(", ", missingOrders) + ")." + hint;
        }

        /// <summary>Tìm phiên Showcase gần nhất (trong 7 ngày) có đủ clip scene_01… — dùng khi draft chưa lưu đường dẫn veo_clips.</summary>
        public static ShowcaseSessionState TryFindRecentSessionWithClips(string profileName, string productName, int sceneCount)
        {
            if (sceneCount < ShowcaseWorkflowConstants.MinScenes)
            {
                return null;
            }

            var processedRoot = ProfileScopedPaths.GetVideoTypeFolder(null, profileName, VideoStorageType.Processed, create: false);
            var showcaseRoot = Path.Combine(processedRoot, "Showcase");
            if (!Directory.Exists(showcaseRoot))
            {
                return null;
            }

            ShowcaseSessionState best = null;
            var bestStamp = DateTime.MinValue;
            var cutoff = DateTime.Now.Date.AddDays(-7);

            foreach (var dateDir in Directory.EnumerateDirectories(showcaseRoot))
            {
                if (!DateTime.TryParseExact(Path.GetFileName(dateDir), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var folderDate)
                    || folderDate < cutoff)
                {
                    continue;
                }

                foreach (var sessionDir in Directory.EnumerateDirectories(dateDir))
                {
                    var clipsDir = Path.Combine(sessionDir, "veo_clips");
                    if (!Directory.Exists(clipsDir))
                    {
                        continue;
                    }

                    var found = 0;
                    for (var order = 1; order <= sceneCount; order++)
                    {
                        if (!string.IsNullOrWhiteSpace(DetectClipForOrder(clipsDir, order)))
                        {
                            found++;
                        }
                    }

                    if (found < sceneCount)
                    {
                        continue;
                    }

                    if (!DateTime.TryParseExact(
                            Path.GetFileName(dateDir) + Path.GetFileName(sessionDir),
                            "yyyyMMddHHmmss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var sessionStamp))
                    {
                        sessionStamp = Directory.GetLastWriteTime(sessionDir);
                    }

                    if (sessionStamp > bestStamp)
                    {
                        bestStamp = sessionStamp;
                        best = RestoreSession(profileName, productName, sessionDir, clipsDir);
                    }
                }
            }

            return best;
        }

        private static string GuessImageExtension(string url)
        {
            try
            {
                var ext = Path.GetExtension(new Uri(url, UriKind.RelativeOrAbsolute).IsAbsoluteUri
                    ? new Uri(url).AbsolutePath
                    : url);
                if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 5)
                {
                    return ext;
                }
            }
            catch
            {
                // ignored — fallback below
            }

            return ".jpg";
        }
    }
}
