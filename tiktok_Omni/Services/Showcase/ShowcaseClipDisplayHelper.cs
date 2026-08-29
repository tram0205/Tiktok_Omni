using System;
using System.IO;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Nhãn clip trên UI — clip quay tay giữ tên gốc; Zoom/Veo/Kling dùng scene_XX.</summary>
    public static class ShowcaseClipDisplayHelper
    {
        public static bool IsAppGeneratedSceneFileName(string fileName)
        {
            return ShowcaseSessionService.TryParseSceneOrderFromFileName(fileName, out _);
        }

        public static bool IsRealClipScene(AiVideoGenInputItem scene) =>
            scene != null
            && string.Equals(
                (scene.ShowcaseClipTool ?? string.Empty).Trim(),
                ShowcaseClipToolHelper.ToolReal,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Tên hiển thị trong bảng «Video chờ render» — ưu tiên tên gốc clip quay tay.</summary>
        public static string FormatRenderQueueFileName(AiVideoGenInputItem scene, string clipPath)
        {
            var diskName = string.IsNullOrWhiteSpace(clipPath)
                ? string.Empty
                : Path.GetFileName(clipPath);
            if (string.IsNullOrWhiteSpace(diskName))
            {
                return "—";
            }

            var original = ResolveOriginalFileName(scene, clipPath);
            var title = (scene?.SceneTitle ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(original)
                && !string.Equals(original, diskName, StringComparison.OrdinalIgnoreCase))
            {
                return original;
            }

            if (IsRealClipScene(scene) && !IsAppGeneratedSceneFileName(diskName))
            {
                return diskName;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                return diskName + " · " + title;
            }

            return diskName;
        }

        public static string FormatRenderQueueToolTip(AiVideoGenInputItem scene, string clipPath)
        {
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                return string.Empty;
            }

            var lines = "File: " + clipPath;
            var original = ResolveOriginalFileName(scene, clipPath);
            if (!string.IsNullOrWhiteSpace(original)
                && !string.Equals(original, Path.GetFileName(clipPath), StringComparison.OrdinalIgnoreCase))
            {
                lines += "\r\nTên gốc: " + original;
            }

            var title = (scene?.SceneTitle ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                lines += "\r\nCảnh: " + title;
            }

            var role = (scene?.SceneRole ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(role))
            {
                lines += " [" + role + "]";
            }

            return lines;
        }

        public static string ResolveOriginalFileName(AiVideoGenInputItem scene, string clipPath)
        {
            var source = (scene?.ShowcaseRealClipSourcePath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(source))
            {
                return Path.GetFileName(source);
            }

            var diskName = string.IsNullOrWhiteSpace(clipPath) ? string.Empty : Path.GetFileName(clipPath);
            if (IsRealClipScene(scene) && !IsAppGeneratedSceneFileName(diskName))
            {
                return diskName;
            }

            return string.Empty;
        }
    }
}
