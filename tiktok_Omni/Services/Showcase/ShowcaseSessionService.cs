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
    /// <summary>Quản lý thư mục phiên Showcase: source_images/, clips_render/, output/, showcase_prompts.xlsx.</summary>
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
                ClipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(baseDir, createIfMissing: true),
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

        /// <summary>Đường dẫn đích trong source_images — giữ tên file gốc (clip video vẫn dùng scene_XX.mp4 trong clips_render).</summary>
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

            var preserved = BuildSourceImageDestPathPreservingFileName(session, sourceFilePath);
            if (!string.IsNullOrWhiteSpace(preserved))
            {
                return preserved;
            }

            var ext = GuessImageExtension(sourceFilePath);
            return Path.Combine(session.SourceImagesDir, $"photo_{sceneIndexOneBased:D2}{ext}");
        }

        private static string BuildSourceImageDestPathPreservingFileName(ShowcaseSessionState session, string sourceFilePath)
        {
            if (session == null || string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return null;
            }

            var fileName = SanitizeSourceImageFileName(Path.GetFileName(sourceFilePath));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            return Path.Combine(session.SourceImagesDir, fileName);
        }

        private static string ResolveUniqueSourceImageDestPath(ShowcaseSessionState session, string sourceFilePath)
        {
            var dest = BuildSourceImageDestPathPreservingFileName(session, sourceFilePath);
            if (string.IsNullOrWhiteSpace(dest))
            {
                return GetSourceImageDestPath(session, 1, sourceFilePath);
            }

            if (PathsEqual(sourceFilePath, dest) || !File.Exists(dest))
            {
                return dest;
            }

            var dir = session.SourceImagesDir;
            var stem = Path.GetFileNameWithoutExtension(dest);
            var ext = Path.GetExtension(dest);
            for (var n = 2; n < 1000; n++)
            {
                var candidate = Path.Combine(dir, stem + " (" + n + ")" + ext);
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(dir, stem + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ext);
        }

        private static string SanitizeSourceImageFileName(string fileName)
        {
            var name = (fileName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return string.Empty;
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
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

        /// <summary>Copy ảnh chọn từ máy vào source_images (giữ tên gốc) — null nếu bỏ qua vì trùng và không ghi đè.</summary>
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
            var dest = ResolveUniqueSourceImageDestPath(session, sourceFilePath);
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

                if (!PathsEqual(existingAtSlot, dest) && File.Exists(existingAtSlot))
                {
                    try
                    {
                        File.Delete(existingAtSlot);
                    }
                    catch
                    {
                        // vẫn thử ghi file mới
                    }
                }
            }
            else if (File.Exists(dest) && !overwriteExisting && !PathsEqual(sourceFilePath, dest))
            {
                return null;
            }

            CopyFileWithSharedRead(sourceFilePath, dest);
            return dest;
        }

        public static string CopyLocalSceneImage(ShowcaseSessionState session, int sceneIndexOneBased, string sourceFilePath) =>
            CopyLocalSceneImage(session, sceneIndexOneBased, sourceFilePath, overwriteExisting: true);

        /// <summary>Copy clip chọn từ máy vào <c>clips_render/scene_XX.*</c> — null nếu bỏ qua vì trùng và không ghi đè.</summary>
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

        /// <summary>Copy clip vào clips_render — giữ tên file gốc (chưa gán cảnh / bảng chờ render).</summary>
        public static string CopyLocalClipPreserveName(
            ShowcaseSessionState session,
            string sourceFilePath,
            bool overwriteExisting)
        {
            if (session == null || string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return null;
            }

            Directory.CreateDirectory(session.ClipsDir);
            var fileName = Path.GetFileName(sourceFilePath);
            if (string.IsNullOrWhiteSpace(fileName) || !IsVideoExtension(Path.GetExtension(fileName)))
            {
                return null;
            }

            var dest = Path.Combine(session.ClipsDir, fileName);
            if (PathsEqual(sourceFilePath, dest))
            {
                return dest;
            }

            if (File.Exists(dest) && !overwriteExisting)
            {
                return null;
            }

            CopyFileWithSharedRead(sourceFilePath, dest);
            return dest;
        }

        /// <summary>Copy clip vào clips_render với tên file chỉ định (giữ tên gốc clip quay tay).</summary>
        public static string CopyLocalClipWithBaseName(
            ShowcaseSessionState session,
            string sourceFilePath,
            string destBaseFileName,
            bool overwriteExisting)
        {
            if (session == null || string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return null;
            }

            var fileName = Path.GetFileName((destBaseFileName ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(fileName) || !IsVideoExtension(Path.GetExtension(fileName)))
            {
                return null;
            }

            Directory.CreateDirectory(session.ClipsDir);
            var dest = Path.Combine(session.ClipsDir, fileName);
            if (PathsEqual(sourceFilePath, dest))
            {
                return dest;
            }

            if (File.Exists(dest) && !overwriteExisting)
            {
                return null;
            }

            CopyFileWithSharedRead(sourceFilePath, dest);
            return dest;
        }

        /// <summary>Liệt kê mọi clip video trong clips_render — scene_XX trước (theo số), rồi tên gốc A→Z.</summary>
        public static IReadOnlyList<string> EnumerateClipFiles(string clipsDir)
        {
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return Array.Empty<string>();
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(clipsDir);
            }
            catch
            {
                return Array.Empty<string>();
            }

            return files
                .Where(f => IsVideoExtension(Path.GetExtension(f)))
                .OrderBy(f =>
                {
                    return TryParseSceneOrderFromFileName(Path.GetFileName(f), out var order)
                        ? order
                        : 1000;
                })
                .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int CountClipFilesOnDisk(string clipsDir) => EnumerateClipFiles(clipsDir).Count;

        /// <summary>Storyboard trống nhưng clips_render còn file — tạo lại cảnh từ folder (không gọi Gemini).</summary>
        public static int EnsureScenesFromRenderFolder(
            ShowcaseVideoItem video,
            string clipsDir,
            string profileName,
            Action<string> log = null)
        {
            if (video?.Scenes == null || video.Scenes.Count > 0)
            {
                return 0;
            }

            var resolvedDir = (clipsDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedDir))
            {
                resolvedDir = ShowcaseRenderClipsPaths.ResolveDirectory(
                    video.ShowcaseSessionBaseDir,
                    createIfMissing: false,
                    migrateLegacy: true);
            }

            if (string.IsNullOrWhiteSpace(resolvedDir) || !Directory.Exists(resolvedDir))
            {
                return 0;
            }

            var productName = (video.ProductName ?? string.Empty).Trim();
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var sourceDir = string.IsNullOrWhiteSpace(video.ShowcaseSessionBaseDir)
                ? string.Empty
                : Path.Combine(video.ShowcaseSessionBaseDir.Trim(), "source_images");
            var added = 0;

            foreach (var clipPath in EnumerateClipFiles(resolvedDir))
            {
                if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(clipPath) ?? string.Empty;
                var isAppScene = TryParseSceneOrderFromFileName(fileName, out var sceneOrder);
                var isReal = !isAppScene;
                var imagePath = string.Empty;
                if (isAppScene && !string.IsNullOrWhiteSpace(sourceDir))
                {
                    imagePath = TryDetectSourceMediaForOrder(sourceDir, sceneOrder) ?? string.Empty;
                }

                video.Scenes.Add(new AiVideoGenInputItem
                {
                    ProfileName = profile,
                    ProductName = productName,
                    ClipPath = clipPath,
                    ShowcaseRealClipSourcePath = isReal ? clipPath : string.Empty,
                    ShowcaseClipTool = isReal ? ShowcaseClipToolHelper.ToolReal : string.Empty,
                    SceneTitle = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty,
                    ImageUrl = imagePath,
                    ThumbnailPath = imagePath,
                    PipelineStatus = "Chờ"
                });
                added++;
            }

            if (added <= 0)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(resolvedDir))
            {
                video.ShowcaseClipsDir = resolvedDir;
            }

            ShowcaseSceneNamingHelper.ApplyConventionSceneTitles(video.Scenes);
            RefreshClipStatus(resolvedDir, video.Scenes, log);
            log?.Invoke("[Showcase] Khôi phục " + added + " cảnh từ "
                        + ShowcaseRenderClipsPaths.FolderName + " (clip trên đĩa).");
            return added;
        }

        /// <summary>scene_01.mp4 → 1; tên khác → false.</summary>
        public static bool TryParseSceneOrderFromFileName(string fileName, out int order)
        {
            order = 0;
            var baseName = NormalizeSceneBaseName(fileName);
            if (string.IsNullOrEmpty(baseName)
                || !baseName.StartsWith("scene_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var suffix = baseName.Substring("scene_".Length);
            return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out order) && order >= 1;
        }

        public sealed class AssignPendingClipsResult
        {
            public int AssignedCount { get; set; }

            public int RemainingPendingCount { get; set; }
        }

        /// <summary>
        /// Sau khi có kịch bản — gán clip chờ (tên gốc) vào cảnh còn trống theo thứ tự,
        /// chỉ cập nhật <see cref="AiVideoGenInputItem.ClipPath"/> — không đổi tên file.
        /// </summary>
        public static AssignPendingClipsResult AssignPendingClipsToSceneSlots(
            string clipsDir,
            IList<AiVideoGenInputItem> orderedScenes,
            Action<string> log = null)
        {
            var result = new AssignPendingClipsResult();
            if (orderedScenes == null || orderedScenes.Count == 0
                || string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                result.RemainingPendingCount = EnumeratePendingClipFiles(clipsDir).Count;
                return result;
            }

            var pending = EnumeratePendingClipFiles(clipsDir).ToList();
            var pendingIndex = 0;
            for (var i = 0; i < orderedScenes.Count && pendingIndex < pending.Count; i++)
            {
                if (SceneSlotHasClip(orderedScenes[i], clipsDir, i + 1))
                {
                    continue;
                }

                var source = pending[pendingIndex++];
                var scene = orderedScenes[i];
                if (scene != null)
                {
                    scene.ClipPath = source;
                    scene.ShowcaseRealClipSourcePath = source;
                    if (string.IsNullOrWhiteSpace(scene.ShowcaseClipTool))
                    {
                        scene.ShowcaseClipTool = ShowcaseClipToolHelper.ToolReal;
                    }
                }

                result.AssignedCount++;
                log?.Invoke("[Showcase] Gán clip «" + Path.GetFileName(source) + "» → cảnh "
                            + (i + 1) + " (giữ tên gốc).");
            }

            result.RemainingPendingCount = Math.Max(0, pending.Count - pendingIndex);
            if (result.RemainingPendingCount > 0)
            {
                log?.Invoke("[Showcase] Còn " + result.RemainingPendingCount
                            + " clip chờ render chưa gán cảnh (thừa slot hoặc cảnh đã có clip).");
            }

            return result;
        }

        public sealed class AppendPendingClipsAsScenesResult
        {
            public int AppendedCount { get; set; }
        }

        /// <summary>
        /// Clip quay tay trong clips_render chưa gán cảnh — thêm cảnh storyboard (cuối timeline)
        /// để «Tạo lời thoại» và render gửi đủ clip cho Gemini.
        /// </summary>
        public static AppendPendingClipsAsScenesResult AppendUnassignedPendingClipsAsScenes(
            string clipsDir,
            IList<AiVideoGenInputItem> orderedScenes,
            string productName,
            string profileName,
            Action<string> log = null)
        {
            var result = new AppendPendingClipsAsScenesResult();
            if (orderedScenes == null || string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return result;
            }

            var assigned = CollectAssignedClipFullPaths(orderedScenes);
            foreach (var clipPath in EnumeratePendingClipFiles(clipsDir))
            {
                if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                if (!TryNormalizeFullPath(clipPath, out var fullPath))
                {
                    fullPath = clipPath.Trim();
                }

                if (assigned.Contains(fullPath))
                {
                    continue;
                }

                var scene = new AiVideoGenInputItem
                {
                    ProfileName = ProfileScopedPaths.ResolveProfileName(profileName),
                    ProductName = (productName ?? string.Empty).Trim(),
                    ClipPath = clipPath,
                    ShowcaseRealClipSourcePath = clipPath,
                    ShowcaseClipTool = ShowcaseClipToolHelper.ToolReal,
                    SceneTitle = Path.GetFileNameWithoutExtension(clipPath) ?? string.Empty,
                    PipelineStatus = "Chờ"
                };
                orderedScenes.Add(scene);
                assigned.Add(fullPath);
                result.AppendedCount++;
                log?.Invoke("[Showcase] Thêm cảnh quay tay «" + Path.GetFileName(clipPath)
                            + "» — tổng " + orderedScenes.Count + " cảnh (Gemini sẽ xem clip này).");
            }

            return result;
        }

        private static HashSet<string> CollectAssignedClipFullPaths(IEnumerable<AiVideoGenInputItem> orderedScenes)
        {
            var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (orderedScenes == null)
            {
                return assigned;
            }

            foreach (var scene in orderedScenes)
            {
                if (scene == null)
                {
                    continue;
                }

                foreach (var path in new[] { scene.ClipPath, scene.ShowcaseRealClipSourcePath })
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    {
                        continue;
                    }

                    if (TryNormalizeFullPath(path, out var fullPath))
                    {
                        assigned.Add(fullPath);
                    }
                    else
                    {
                        assigned.Add(path.Trim());
                    }
                }
            }

            return assigned;
        }

        private static bool TryNormalizeFullPath(string path, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                fullPath = Path.GetFullPath(path.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SceneSlotHasClip(AiVideoGenInputItem scene, string clipsDir, int orderOneBased)
        {
            if (scene != null)
            {
                var stored = (scene.ClipPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(stored) && File.Exists(stored))
                {
                    if (ShowcaseClipDisplayHelper.IsRealClipScene(scene)
                        && ShowcaseClipDisplayHelper.IsAppGeneratedSceneFileName(Path.GetFileName(stored)))
                    {
                        return false;
                    }

                    return true;
                }
            }

            if (ShowcaseClipDisplayHelper.IsRealClipScene(scene))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(DetectClipForOrder(clipsDir, orderOneBased));
        }

        /// <summary>Clip trong clips_render chưa theo dạng scene_XX — thứ tự thêm file (creation time).</summary>
        public static IReadOnlyList<string> EnumeratePendingClipFiles(string clipsDir)
        {
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return Array.Empty<string>();
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(clipsDir);
            }
            catch
            {
                return Array.Empty<string>();
            }

            return files
                .Where(f => IsVideoExtension(Path.GetExtension(f))
                            && !TryParseSceneOrderFromFileName(Path.GetFileName(f), out _))
                .OrderBy(GetClipFileCreationTimeUtc)
                .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static DateTime GetClipFileCreationTimeUtc(string path)
        {
            try
            {
                return File.GetCreationTimeUtc(path);
            }
            catch
            {
                return DateTime.MaxValue;
            }
        }

        /// <summary>Đường dẫn chuẩn clip cảnh trong clips_render — scene_01.mp4, scene_02.mp4, …</summary>
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

        /// <summary>Đưa mọi ảnh cảnh vào session.SourceImagesDir (giữ tên gốc) và cập nhật đường dẫn trên item.</summary>
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
                ClipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(resolvedBase, createIfMissing: true),
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

            var stored = (scene?.ThumbnailPath ?? scene?.ImageUrl ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(stored) && File.Exists(stored))
            {
                resolvedPath = stored;
                return true;
            }

            var byName = TryFindSourceImageByKnownFileName(sourceImagesDir, scene);
            if (!string.IsNullOrWhiteSpace(byName))
            {
                resolvedPath = byName;
                return true;
            }

            var pick = (scene?.ShowcaseLocalPickPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(pick) && File.Exists(pick))
            {
                resolvedPath = pick;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(sourceImagesDir))
            {
                var clipFile = (scene?.ClipPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(clipFile)
                    && TryParseSceneOrderFromFileName(Path.GetFileName(clipFile), out var clipOrder))
                {
                    resolvedPath = DetectSourceImageForOrder(sourceImagesDir, clipOrder);
                    if (!string.IsNullOrWhiteSpace(resolvedPath))
                    {
                        return true;
                    }
                }

                resolvedPath = DetectSourceImageForOrder(sourceImagesDir, orderOneBased);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static string TryFindSourceImageByKnownFileName(string sourceImagesDir, AiVideoGenInputItem scene)
        {
            if (scene == null || string.IsNullOrWhiteSpace(sourceImagesDir) || !Directory.Exists(sourceImagesDir))
            {
                return null;
            }

            var names = new[]
            {
                Path.GetFileName((scene.ThumbnailPath ?? string.Empty).Trim()),
                Path.GetFileName((scene.ImageUrl ?? string.Empty).Trim()),
                Path.GetFileName((scene.ShowcaseLocalPickPath ?? string.Empty).Trim())
            };

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var candidate = Path.Combine(sourceImagesDir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
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

                // Timeline clip-first: cảnh có clip render hợp lệ không bị coi là «thiếu ảnh» để gỡ.
                if (ShowcaseClipStatusHelper.SceneHasClipFile(scene))
                {
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
                log("[Showcase] Đã gỡ " + removed + " cảnh khỏi storyboard (ảnh không còn trong source_images, không có clip). Còn "
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

        /// <summary>Tìm file clip theo tên "scene_XX.*" trong thư mục clips_render — null nếu chưa có.</summary>
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

        /// <summary>Quét thư mục clip, gán <see cref="AiVideoGenInputItem.ClipPath"/> — ưu tiên đường dẫn đã lưu, rồi scene_XX (Zoom/app).</summary>
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
                var scene = orderedScenes[i];
                var clip = ResolveClipForScene(scene, clipsDir, order);
                if (scene != null)
                {
                    scene.ClipPath = clip ?? string.Empty;
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

        /// <summary>Clip đã gán cảnh (tên gốc) hoặc scene_XX do app tạo (Zoom).</summary>
        public static string ResolveClipForScene(AiVideoGenInputItem scene, string clipsDir, int orderOneBased)
        {
            var stored = (scene?.ClipPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(stored))
            {
                if (File.Exists(stored))
                {
                    return stored;
                }

                var byStoredName = TryResolveClipPathInDirectory(clipsDir, Path.GetFileName(stored));
                if (!string.IsNullOrWhiteSpace(byStoredName))
                {
                    return byStoredName;
                }
            }

            var realSource = (scene?.ShowcaseRealClipSourcePath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(realSource))
            {
                if (File.Exists(realSource))
                {
                    return realSource;
                }

                var byRealName = TryResolveClipPathInDirectory(clipsDir, Path.GetFileName(realSource));
                if (!string.IsNullOrWhiteSpace(byRealName))
                {
                    return byRealName;
                }
            }

            if (!ShowcaseClipDisplayHelper.IsRealClipScene(scene))
            {
                return DetectClipForOrder(clipsDir, orderOneBased);
            }

            return null;
        }

        /// <summary>Tìm clip trong clips_render theo tên file (không phân biệt hoa thường).</summary>
        public static string TryResolveClipPathInDirectory(string clipsDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return null;
            }

            var name = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || !IsVideoExtension(Path.GetExtension(name)))
            {
                return null;
            }

            var direct = Path.Combine(clipsDir, name);
            if (File.Exists(direct))
            {
                return direct;
            }

            try
            {
                foreach (var file in Directory.GetFiles(clipsDir))
                {
                    if (string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        /// <summary>Đếm clip thực sự có trên đĩa (ưu tiên quét clips_render, fallback ClipPath đã lưu).</summary>
        public static int CountValidClipsOnDisk(string clipsDir, IList<AiVideoGenInputItem> orderedScenes)
        {
            if (orderedScenes == null || orderedScenes.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < orderedScenes.Count; i++)
            {
                var clip = !string.IsNullOrWhiteSpace(clipsDir)
                    ? ResolveClipForScene(orderedScenes[i], clipsDir, i + 1)
                    : null;
                if (string.IsNullOrWhiteSpace(clip))
                {
                    var stored = (orderedScenes[i]?.ClipPath ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(stored) && File.Exists(stored))
                    {
                        clip = stored;
                    }
                }

                if (!string.IsNullOrWhiteSpace(clip) && File.Exists(clip))
                {
                    count++;
                }
            }

            return count;
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
                return "[Showcase] " + ShowcaseRenderClipsPaths.FolderName + " không tồn tại: " + (clipsDir ?? string.Empty);
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(clipsDir);
            }
            catch (Exception ex)
            {
                return "[Showcase] Không đọc được " + ShowcaseRenderClipsPaths.FolderName + ": " + ex.Message;
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
            return "[Showcase] " + ShowcaseRenderClipsPaths.FolderName + " có " + files.Length + " file [" + sample + "] — thiếu "
                   + missingOrders.Count + "/" + sceneCount + " cảnh ("
                   + string.Join(", ", missingOrders) + ")." + hint;
        }

        /// <summary>Số cảnh có clip trong clips_render (scene_01 …).</summary>
        public static int CountClipSlots(string clipsDir)
        {
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return 0;
            }

            var max = 0;
            for (var order = 1; order <= 30; order++)
            {
                if (!string.IsNullOrWhiteSpace(DetectClipForOrder(clipsDir, order)))
                {
                    max = order;
                }
            }

            return max;
        }

        /// <summary>Số file ảnh/video storyboard trong source_images (photo_XX / scene_XX).</summary>
        public static int CountSourceMediaSlots(string sourceImagesDir)
        {
            if (string.IsNullOrWhiteSpace(sourceImagesDir) || !Directory.Exists(sourceImagesDir))
            {
                return 0;
            }

            var max = 0;
            for (var order = 1; order <= 30; order++)
            {
                if (!string.IsNullOrWhiteSpace(TryDetectSourceMediaForOrder(sourceImagesDir, order)))
                {
                    max = order;
                }
            }

            return max;
        }

        /// <summary>Ảnh hoặc clip gốc trong source_images theo thứ tự cảnh.</summary>
        public static string TryDetectSourceMediaForOrder(string sourceImagesDir, int order)
        {
            var image = DetectSourceImageForOrder(sourceImagesDir, order);
            if (!string.IsNullOrWhiteSpace(image))
            {
                return image;
            }

            if (order < 1 || string.IsNullOrWhiteSpace(sourceImagesDir) || !Directory.Exists(sourceImagesDir))
            {
                return null;
            }

            foreach (var prefix in new[]
                     {
                         "photo_" + order.ToString("D2", CultureInfo.InvariantCulture),
                         "scene_" + order.ToString("D2", CultureInfo.InvariantCulture)
                     })
            {
                string[] matches;
                try
                {
                    matches = Directory.GetFiles(sourceImagesDir, prefix + ".*");
                }
                catch
                {
                    matches = Array.Empty<string>();
                }

                foreach (var file in matches)
                {
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }
            }

            return null;
        }

        /// <summary>Tìm phiên Showcase gần nhất (trong 7 ngày) có đủ clip scene_01… — dùng khi draft chưa lưu đường dẫn clips_render.</summary>
        public static ShowcaseSessionState TryFindRecentSessionWithClips(string profileName, string productName, int sceneCount)
        {
            if (sceneCount < ShowcaseWorkflowConstants.MinScenes)
            {
                sceneCount = 1;
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
                    var clipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(sessionDir, createIfMissing: false);
                    if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
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
