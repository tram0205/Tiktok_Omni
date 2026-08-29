using System;
using System.IO;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Cấu trúc lưu trữ: RootPath\{ProfileName}\{VideoType}\.
    /// RootPath do người dùng chọn (AppSettings.StorageRootPath); để trống dùng MediaStorage cạnh exe.
    /// </summary>
    public static class ProfileScopedPaths
    {
        public static readonly string[] StandardVideoTypeFolderNames =
        {
            nameof(VideoStorageType.Original),
            nameof(VideoStorageType.Processed),
            nameof(VideoStorageType.Reup),
            nameof(VideoStorageType.Failed)
        };

        private static string _configuredStorageRoot = string.Empty;

        public static string AppRoot =>
            AppDomain.CurrentDomain.BaseDirectory ?? ".";

        /// <summary>Thư mục gốc asset cạnh exe: <c>Assets\</c>.</summary>
        public const string AssetsFolderName = "Assets";

        /// <summary>Kho video hook B-Roll ngắn: <c>Assets\HookBRolls\</c>.</summary>
        public const string HookBRollsFolderName = "HookBRolls";

        /// <summary>Clip quay tay ghép cuối Showcase (CTA): <c>Assets\CtaBRolls\</c>.</summary>
        public const string CtaBRollsFolderName = "CtaBRolls";

        /// <summary>Video nền động dùng chung (Triết lý): <c>Assets\Backgrounds\</c>.</summary>
        public const string SharedBackgroundsFolderName = "Backgrounds";

        public static string GetSharedBackgroundsDirectory(bool ensureExists = false)
        {
            var dir = Path.Combine(AppRoot, AssetsFolderName, SharedBackgroundsFolderName);
            if (ensureExists)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        /// <summary>Đường dẫn đầy đủ tới <see cref="HookBRollsFolderName"/> (tạo thư mục nếu <paramref name="ensureExists"/>).</summary>
        public static string GetHookBRollsDirectory(bool ensureExists = false)
        {
            var dir = Path.Combine(AppRoot, AssetsFolderName, HookBRollsFolderName);
            if (ensureExists)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        /// <summary>Đường dẫn đầy đủ tới <see cref="CtaBRollsFolderName"/>.</summary>
        public static string GetCtaBRollsDirectory(bool ensureExists = false)
        {
            var dir = Path.Combine(AppRoot, AssetsFolderName, CtaBRollsFolderName);
            if (ensureExists)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        /// <summary>Cập nhật từ Cài đặt sau Load/Save (Form1).</summary>
        public static void SetConfiguredStorageRoot(string storageRootPath)
        {
            _configuredStorageRoot = storageRootPath ?? string.Empty;
        }

        public static string ResolveStorageRoot(string configuredRoot = null)
        {
            var raw = configuredRoot ?? _configuredStorageRoot;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var full = Path.GetFullPath(raw.Trim());
                Directory.CreateDirectory(full);
                return full;
            }

            var def = Path.Combine(AppRoot, "MediaStorage");
            Directory.CreateDirectory(def);
            return Path.GetFullPath(def);
        }

        public static string ResolveProfileName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return "default";
            }

            var sanitized = SanitizeSegment(profileName.Trim());
            return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
        }

        public static string SanitizeSegment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = raw.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            var result = new string(chars).Trim().TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(result) ? string.Empty : result;
        }

        public static string ToFolderSegment(VideoStorageType videoType) =>
            videoType.ToString();

        /// <summary>RootPath\{Profile}\</summary>
        public static string GetProfileRoot(string storageRoot, string profileName, bool create = true)
        {
            var root = ResolveStorageRoot(storageRoot);
            var dir = Path.Combine(root, ResolveProfileName(profileName));
            if (create)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        /// <summary>RootPath\{Profile}\{VideoType}\</summary>
        public static string GetVideoTypeFolder(
            string storageRoot,
            string profileName,
            VideoStorageType videoType,
            bool create = true)
        {
            var dir = Path.Combine(
                GetProfileRoot(storageRoot, profileName, create),
                ToFolderSegment(videoType));
            if (create)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        public static string ResolveProfileFromCandidate(AffiliateCandidate candidate)
        {
            if (candidate == null)
            {
                return "default";
            }

            return ResolveProfileName(candidate.ProfileName);
        }

        /// <summary>Lấy thư mục VideoType theo profile gắn trên candidate (không dùng UI).</summary>
        public static string GetVideoTypeFolder(
            AffiliateCandidate candidate,
            VideoStorageType videoType,
            string storageRoot = null,
            bool create = true)
        {
            return GetVideoTypeFolder(storageRoot, ResolveProfileFromCandidate(candidate), videoType, create);
        }

        public static string GetDownloadsKeywordFolder(AffiliateCandidate candidate, string keyword, string storageRoot = null)
        {
            return GetDownloadsKeywordFolder(storageRoot, ResolveProfileFromCandidate(candidate), keyword);
        }

        /// <summary>Tạo đủ Original / Processed / Reup / Failed khi chọn profile trên lưới.</summary>
        public static void EnsureProfileVideoTypeHierarchy(string storageRoot, string profileName)
        {
            GetProfileRoot(storageRoot, profileName, create: true);
            foreach (VideoStorageType t in Enum.GetValues(typeof(VideoStorageType)))
            {
                GetVideoTypeFolder(storageRoot, profileName, t, create: true);
            }
        }

        /// <summary>Original\{Keyword}\ — tải Hunt.</summary>
        public static string GetDownloadsKeywordFolder(string profileName, string keyword)
        {
            return GetDownloadsKeywordFolder((string)null, profileName, keyword);
        }

        public static string GetDownloadsKeywordFolder(string storageRoot, string profileName, string keyword)
        {
            var kw = SanitizeSegment(keyword);
            if (string.IsNullOrWhiteSpace(kw))
            {
                kw = "default";
            }

            var dir = Path.Combine(
                GetVideoTypeFolder(storageRoot, profileName, VideoStorageType.Original, create: true),
                kw);
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        public static string GetGeneratedRoot(string profileName)
        {
            return GetVideoTypeFolder(null, profileName, VideoStorageType.Processed, create: true);
        }

        /// <summary>Processed\{category}\{yyyyMMdd}\{HHmmss}\</summary>
        public static string CreateGeneratedSessionFolder(string profileName, string category)
        {
            var cat = SanitizeSegment(category);
            if (string.IsNullOrWhiteSpace(cat))
            {
                cat = "AI";
            }

            var dir = Path.Combine(
                GetVideoTypeFolder(null, profileName, VideoStorageType.Processed, create: true),
                cat,
                DateTime.Now.ToString("yyyyMMdd"),
                DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        public static string GetVideoReupStagesRoot(string profileName)
        {
            var dir = Path.Combine(
                GetVideoTypeFolder(null, profileName, VideoStorageType.Original, create: true),
                "VideoReup",
                "Stages");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        public static string GetVideoReupOutputRoot(string profileName)
        {
            return GetVideoTypeFolder(null, profileName, VideoStorageType.Reup, create: true);
        }

        public static string GetTempDownloadsRoot(string profileName)
        {
            var dir = Path.Combine(
                GetVideoTypeFolder(null, profileName, VideoStorageType.Original, create: true),
                "temp_downloads");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        /// <summary>Thư mục gốc media săn link sản phẩm: Root\{Profile}\Products\</summary>
        public const string ProductsFolderName = "Products";

        /// <summary>Root\{Profile}\Products\{keyword}\</summary>
        public static string GetProductHuntKeywordFolder(string profileName, string keyword, string storageRoot = null)
        {
            var kw = SanitizeSegment(keyword);
            if (string.IsNullOrWhiteSpace(kw))
            {
                kw = "default";
            }

            var dir = Path.Combine(GetProfileRoot(storageRoot, profileName, create: true), ProductsFolderName, kw);
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        /// <summary>Root\{Profile}\Products\{keyword}\{productKey}\images|videos\</summary>
        public static string GetProductAssetFolder(
            string profileName,
            string keyword,
            string productKey,
            bool images,
            string storageRoot = null)
        {
            var seg = SanitizeSegment(productKey);
            if (string.IsNullOrWhiteSpace(seg))
            {
                seg = "item";
            }

            var sub = images ? "images" : "videos";
            var dir = Path.Combine(GetProductHuntKeywordFolder(profileName, keyword, storageRoot), seg, sub);
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }

        /// <summary>Root\Publishing\{Profile}\{VideoType}\ — bot AutoPost bốc video đã duyệt.</summary>
        public static string GetPublishingFolder(
            string storageRoot,
            string profileName,
            VideoStorageType videoType = VideoStorageType.Processed,
            bool create = true)
        {
            var root = ResolveStorageRoot(storageRoot);
            var dir = Path.Combine(
                root,
                "Publishing",
                ResolveProfileName(profileName),
                ToFolderSegment(videoType));
            if (create)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        public static string CopyVideoToPublishing(
            string storageRoot,
            string profileName,
            string sourceVideoPath,
            VideoStorageType videoType = VideoStorageType.Processed)
        {
            if (string.IsNullOrWhiteSpace(sourceVideoPath) || !File.Exists(sourceVideoPath))
            {
                return string.Empty;
            }

            var destDir = GetPublishingFolder(storageRoot, profileName, videoType, create: true);
            var dest = Path.Combine(destDir, Path.GetFileName(sourceVideoPath));
            if (File.Exists(dest))
            {
                dest = Path.Combine(destDir, Path.GetFileNameWithoutExtension(sourceVideoPath) + "_" + DateTime.Now.ToString("HHmmss") + Path.GetExtension(sourceVideoPath));
            }

            File.Copy(sourceVideoPath, dest, overwrite: false);
            var cover = ProductionPipeline.ResolveThumbnailPath(sourceVideoPath);
            if (!string.IsNullOrWhiteSpace(cover) && File.Exists(cover))
            {
                var coverDest = Path.Combine(destDir, Path.GetFileName(cover));
                try
                {
                    File.Copy(cover, coverDest, overwrite: true);
                }
                catch
                {
                }
            }

            return Path.GetFullPath(dest);
        }

        public static string GetAutoPostFolder(string profileName, VideoStorageType videoType = VideoStorageType.Reup)
        {
            return GetVideoTypeFolder(null, profileName, videoType, create: true);
        }

        public static string GetAutoPostVideosFolder(string profileName, VideoStorageType preferredType = VideoStorageType.Reup)
        {
            var preferred = GetVideoTypeFolder(null, profileName, preferredType, create: true);
            if (Directory.Exists(preferred) && Directory.EnumerateFiles(preferred, "*.*", SearchOption.TopDirectoryOnly)
                    .Any(f => IsVideoExtension(Path.GetExtension(f))))
            {
                return preferred;
            }

            var processed = GetVideoTypeFolder(null, profileName, VideoStorageType.Processed, create: true);
            if (Directory.Exists(processed) && Directory.EnumerateFiles(processed, "*.*", SearchOption.AllDirectories)
                    .Any(f => IsVideoExtension(Path.GetExtension(f))))
            {
                return processed;
            }

            return preferred;
        }

        public static string GetProfileRootPath(string profileName)
        {
            return GetProfileRoot(null, profileName, create: true);
        }

        public static bool IsPathUnderProfile(string profileName, string fileOrDirectoryPath)
        {
            return IsPathUnderProfile(null, profileName, fileOrDirectoryPath);
        }

        public static bool IsPathUnderProfile(string storageRoot, string profileName, string fileOrDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(fileOrDirectoryPath))
            {
                return false;
            }

            try
            {
                var full = Path.GetFullPath(fileOrDirectoryPath);
                var profileRoot = GetProfileRoot(storageRoot, profileName, create: false);
                return full.StartsWith(profileRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPathUnderProfileVideoType(
            string storageRoot,
            string profileName,
            VideoStorageType videoType,
            string fileOrDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(fileOrDirectoryPath))
            {
                return false;
            }

            try
            {
                var full = Path.GetFullPath(fileOrDirectoryPath);
                var typeRoot = GetVideoTypeFolder(storageRoot, profileName, videoType, create: false);
                return full.StartsWith(typeRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static VideoStorageType? DetectVideoTypeFromPath(string storageRoot, string profileName, string fileOrDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(fileOrDirectoryPath) || !IsPathUnderProfile(storageRoot, profileName, fileOrDirectoryPath))
            {
                return null;
            }

            foreach (VideoStorageType t in Enum.GetValues(typeof(VideoStorageType)))
            {
                if (IsPathUnderProfileVideoType(storageRoot, profileName, t, fileOrDirectoryPath))
                {
                    return t;
                }
            }

            return null;
        }

        public static string ResolveAutoPostFolderForVideo(
            string profileName,
            string videoFullPath,
            VideoStorageType? preferredType = null)
        {
            var profile = ResolveProfileName(profileName);
            if (!string.IsNullOrWhiteSpace(videoFullPath) && File.Exists(videoFullPath))
            {
                var detected = DetectVideoTypeFromPath(null, profile, videoFullPath);
                if (detected.HasValue)
                {
                    return GetVideoTypeFolder(null, profile, detected.Value, create: true);
                }

                var dir = Path.GetDirectoryName(videoFullPath);
                if (!string.IsNullOrWhiteSpace(dir) && IsPathUnderProfile(profile, dir))
                {
                    return Path.GetFullPath(dir);
                }
            }

            return GetAutoPostVideosFolder(profile, preferredType ?? VideoStorageType.Reup);
        }

        public static bool IsUnderPublishingRoot(string storageRoot, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var root = ResolveStorageRoot(storageRoot);
                var publishingRoot = Path.Combine(root, "Publishing");
                var full = Path.GetFullPath(path);
                return full.StartsWith(Path.GetFullPath(publishingRoot), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>AutoPost bot chỉ được đọc video từ Publishing — không Processed/Original/Reup.</summary>
        public static void ValidatePublishingOnlyAutoPost(
            string storageRoot,
            string profileName,
            string videoFolder,
            string explicitVideoPath)
        {
            var profile = ResolveProfileName(profileName);
            if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            {
                throw new InvalidOperationException("Thư mục video không hợp lệ.");
            }

            if (!IsUnderPublishingRoot(storageRoot, videoFolder))
            {
                throw new InvalidOperationException(
                    "AutoPost chỉ nhận video từ thư mục «Publishing» đã duyệt. Không dùng Processed/Original/Reup.");
            }

            var expected = GetPublishingFolder(storageRoot, profile, VideoStorageType.Processed, create: false);
            if (!videoFolder.StartsWith(Path.GetDirectoryName(expected) ?? expected, StringComparison.OrdinalIgnoreCase)
                && !IsUnderPublishingRoot(storageRoot, videoFolder))
            {
                throw new InvalidOperationException("Thư mục không thuộc Publishing của profile «" + profile + "».");
            }

            if (!string.IsNullOrWhiteSpace(explicitVideoPath) && File.Exists(explicitVideoPath)
                && !IsUnderPublishingRoot(storageRoot, explicitVideoPath))
            {
                throw new InvalidOperationException(
                    "File video phải nằm trong «Publishing/{Profile}/…» — chưa qua bước duyệt.");
            }
        }

        public static void ValidateAutoPostPathsForProfile(
            string storageRoot,
            string profileName,
            string videoFolder,
            string explicitVideoPath,
            VideoStorageType? requiredVideoType = null)
        {
            var profile = ResolveProfileName(profileName);
            if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            {
                throw new InvalidOperationException("Thư mục video không hợp lệ.");
            }

            if (!IsPathUnderProfile(storageRoot, profile, videoFolder))
            {
                throw new InvalidOperationException(
                    "Thư mục video không thuộc profile «" + profile +
                    "». Không được dùng file của nick khác.");
            }

            if (requiredVideoType.HasValue &&
                !IsPathUnderProfileVideoType(storageRoot, profile, requiredVideoType.Value, videoFolder))
            {
                throw new InvalidOperationException(
                    "Thư mục phải nằm trong «" + ToFolderSegment(requiredVideoType.Value) +
                    "» của profile «" + profile + "».");
            }

            var videoPath = (explicitVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(videoPath))
            {
                if (!File.Exists(videoPath))
                {
                    throw new InvalidOperationException("File video đã chọn không tồn tại.");
                }

                if (!IsPathUnderProfile(storageRoot, profile, videoPath))
                {
                    throw new InvalidOperationException(
                        "File video không thuộc profile «" + profile + "» — từ chối đăng để tránh nhầm nick.");
                }

                if (requiredVideoType.HasValue &&
                    !IsPathUnderProfileVideoType(storageRoot, profile, requiredVideoType.Value, videoPath))
                {
                    throw new InvalidOperationException(
                        "File phải nằm trong thư mục «" + ToFolderSegment(requiredVideoType.Value) +
                        "» của profile «" + profile + "».");
                }
            }
        }

        private static bool IsVideoExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return false;
            }

            switch (ext.Trim().ToLowerInvariant())
            {
                case ".mp4":
                case ".mov":
                case ".mkv":
                case ".webm":
                case ".avi":
                    return true;
                default:
                    return false;
            }
        }
    }
}
