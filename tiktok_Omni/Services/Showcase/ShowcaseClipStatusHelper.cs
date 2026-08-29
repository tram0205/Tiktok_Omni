using System;
using System.Collections.Generic;
using System.IO;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseClipStatusHelper
    {
        public static bool SceneHasClipFile(AiVideoGenInputItem scene)
        {
            var path = (scene?.ClipPath ?? string.Empty).Trim();
            return path.Length > 0 && File.Exists(path);
        }

        public static int CountScenesWithClip(IList<AiVideoGenInputItem> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < scenes.Count; i++)
            {
                if (SceneHasClipFile(scenes[i]))
                {
                    count++;
                }
            }

            return count;
        }

        public static bool HasAnyClipAfterRefresh(string clipsDir, IList<AiVideoGenInputItem> scenes, Action<string> log = null)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return false;
            }

            ShowcaseSessionService.RefreshClipStatus(clipsDir, scenes, log);
            return CountScenesWithClip(scenes) > 0;
        }

        public static string BuildNoClipsForVoiceoverMessage(string productName, string clipModeId)
        {
            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);
            var name = string.IsNullOrWhiteSpace(productName) ? "Video" : "«" + productName.Trim() + "»";

            if (string.Equals(mode, ShowcaseClipModePresets.ZoomOnlyId, StringComparison.Ordinal))
            {
                return name + " chưa có clip trong clips_render.\r\n\r\n"
                       + "Chế độ Chỉ Zoom: bấm «Tạo clip Zoom» trước, rồi «Tạo lời thoại».\r\n\r\n"
                       + "Thoại từ «Tạo kịch bản» chỉ là bản nháp theo ảnh — chưa khớp clip.";
            }

            if (ShowcaseClipModePresets.ModeAllowsInAppZoom(mode))
            {
                return name + " chưa có clip trong clips_render.\r\n\r\n"
                       + "Tạo clip (Zoom / Veo / …) hoặc bỏ file scene_01.mp4, scene_02.mp4, … vào clips_render rồi bấm lại.\r\n\r\n"
                       + "Thoại từ «Tạo kịch bản» chỉ là bản nháp theo ảnh — chưa khớp clip.";
            }

            return name + " chưa có clip trong clips_render.\r\n\r\n"
                   + "Bỏ ít nhất scene_01.mp4 vào clips_render (hoặc tạo clip bằng công cụ video) rồi bấm «Tạo lời thoại» lại.\r\n\r\n"
                   + "Thoại từ «Tạo kịch bản» chỉ là bản nháp theo ảnh — chưa khớp clip.";
        }
    }
}
