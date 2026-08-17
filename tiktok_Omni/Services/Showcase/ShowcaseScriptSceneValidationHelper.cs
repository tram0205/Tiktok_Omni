using System;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Kiểm tra nhanh công cụ clip vs loại ảnh trong hub kịch bản.</summary>
    internal static class ShowcaseScriptSceneValidationHelper
    {
        public static bool HasHardToolImageKindConflict(AiVideoGenInputItem scene, string clipModeId)
        {
            if (scene == null)
            {
                return false;
            }

            if (string.Equals(
                    ShowcaseClipModePresets.ResolveIdForGemini(clipModeId),
                    ShowcaseClipModePresets.ZoomOnlyId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var kind = ShowcaseClipToolHelper.NormalizeImageKind(scene.ShowcaseImageKind, scene.VeoPrompt);
            var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
            if (string.IsNullOrEmpty(tool))
            {
                return false;
            }

            if (string.Equals(kind, ShowcaseClipToolHelper.KindFlatlay, StringComparison.Ordinal)
                && string.Equals(tool, ShowcaseClipToolHelper.ToolKling, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(kind, ShowcaseClipToolHelper.KindOnModel, StringComparison.Ordinal)
                   && string.Equals(tool, ShowcaseClipToolHelper.ToolVeo, StringComparison.Ordinal);
        }

        public static string DescribeToolIssue(AiVideoGenInputItem scene, string clipModeId)
        {
            if (!HasHardToolImageKindConflict(scene, clipModeId))
            {
                return string.Empty;
            }

            var kind = ShowcaseClipToolHelper.NormalizeImageKind(scene?.ShowcaseImageKind, scene?.VeoPrompt);
            if (string.Equals(kind, ShowcaseClipToolHelper.KindFlatlay, StringComparison.Ordinal))
            {
                return "Flatlay không dùng Kling — chọn Veo hoặc Zoom.";
            }

            return "On-model không dùng Veo — chọn Kling hoặc Zoom.";
        }
    }
}
