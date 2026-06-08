using System;
using System.IO;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Luồng khép kín: Render → Approval → Publishing → AutoPost (chỉ thư mục Publishing).</summary>
    public static class OneClickPipelineService
    {
        public const string PipelineVideoReup = "VideoReup";
        public const string PipelineMascot = "Mascot";
        public const string PipelinePhilosophy = "Philosophy";
        public const string PipelineSlideshow = "Slideshow";
        public const string PipelineAffiliateDeep = "AffiliateDeep";

        public static string ResolvePublishingFolder(string storageRoot, string profileName, VideoStorageType videoType = VideoStorageType.Processed)
        {
            return ProfileScopedPaths.GetPublishingFolder(storageRoot, profileName, videoType, create: true);
        }

        public static async Task<string> CopyApprovedToPublishingAsync(
            string storageRoot,
            string profileName,
            string sourceVideoPath,
            VideoStorageType videoType = VideoStorageType.Processed)
        {
            if (string.IsNullOrWhiteSpace(sourceVideoPath) || !File.Exists(sourceVideoPath))
            {
                return string.Empty;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(storageRoot);
            return await Task.Run(() =>
                ProfileScopedPaths.CopyVideoToPublishing(storageRoot, profileName, sourceVideoPath, videoType))
                .ConfigureAwait(false);
        }

        /// <summary>Đường dẫn duy nhất AutoPost bot được phép đọc.</summary>
        public static string GetAutoPostInboxFolder(string storageRoot, string profileName, VideoStorageType videoType = VideoStorageType.Processed)
        {
            return ResolvePublishingFolder(storageRoot, profileName, videoType);
        }

        public static void ValidateAutoPostInboxOnly(string storageRoot, string profileName, string folderPath, string videoFilePath)
        {
            ProfileScopedPaths.ValidatePublishingOnlyAutoPost(storageRoot, profileName, folderPath, videoFilePath);
        }
    }
}
