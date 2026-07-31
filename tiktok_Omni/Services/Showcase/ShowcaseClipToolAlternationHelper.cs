using System;
using System.Collections.Generic;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Xen kẽ clip_tool motion (veo/kling) với zoom khi chế độ cho phép cả hai.</summary>
    internal static class ShowcaseClipToolAlternationHelper
    {
        internal sealed class MappedScene
        {
            public AiVideoGenInputItem Item { get; set; }

            public ShowcaseSceneDto Dto { get; set; }

            public string ImageKind { get; set; }

            public string ClipTool { get; set; }

            public int StoryIndex { get; set; }
        }

        public static bool AppliesToMode(string clipModeId)
        {
            return ShowcaseClipModePresets.IsPerSceneToolChoiceMode(clipModeId);
        }

        public static string BuildGeminiPromptSection(string clipModeId)
        {
            if (!AppliesToMode(clipModeId))
            {
                return string.Empty;
            }

            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);
            var motionZoomPair = string.Equals(mode, ShowcaseClipModePresets.DefaultId, StringComparison.Ordinal)
                ? "veo ↔ zoom (flatlay)"
                : "flatlay: veo ↔ zoom; on_model: kling ↔ zoom";

            return
                "9) XEN KẼ CÔNG CỤ CLIP (storyboard order — BẮT BUỘC khi có thể):\r\n" +
                "   - " + motionZoomPair + " — KHÔNG để 2 cảnh liền kề cùng clip_tool khi cả hai đều hợp lệ cho loại ảnh đó.\r\n" +
                "   - Ví dụ flatlay: veo → zoom → veo…; on_model (Kling+Zoom): kling → zoom → kling…\r\n" +
                "   - Chỉ gộp liên tiếp cùng công cụ khi BẮT BUỘC (vd. toàn on_model chỉ zoom trong chế độ Veo+Zoom; thiếu ảnh flatlay để xen zoom).\r\n" +
                "   - Vẫn ưu tiên mạch QC (hook → chi tiết → on-model → CTA) hơn công thức cứng — nhưng CỐ GẮNG xen kẽ trước khi phá vỡ quy tắc.\r\n\r\n";
        }

        public static void Apply(IList<MappedScene> scenes, string clipModeId)
        {
            if (scenes == null || scenes.Count == 0 || !AppliesToMode(clipModeId))
            {
                return;
            }

            string previousFlexibleTool = null;
            for (var i = 0; i < scenes.Count; i++)
            {
                var mapped = scenes[i];
                if (!TryGetFlexiblePair(clipModeId, mapped.ImageKind, out var motionTool, out var zoomTool))
                {
                    previousFlexibleTool = ShowcaseClipToolHelper.NormalizeClipTool(mapped.ClipTool);
                    continue;
                }

                var current = ShowcaseClipToolHelper.NormalizeClipTool(mapped.ClipTool);
                if (!string.Equals(current, motionTool, StringComparison.Ordinal)
                    && !string.Equals(current, zoomTool, StringComparison.Ordinal))
                {
                    current = motionTool;
                }

                if (!string.IsNullOrEmpty(previousFlexibleTool)
                    && string.Equals(previousFlexibleTool, current, StringComparison.Ordinal))
                {
                    current = string.Equals(current, motionTool, StringComparison.Ordinal) ? zoomTool : motionTool;
                }

                mapped.ClipTool = current;
                previousFlexibleTool = current;
            }
        }

        private static bool TryGetFlexiblePair(
            string clipModeId,
            string imageKind,
            out string motionTool,
            out string zoomTool)
        {
            motionTool = null;
            zoomTool = ShowcaseClipToolHelper.ToolZoom;
            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);
            var flatlay = string.Equals(
                ShowcaseClipToolHelper.NormalizeImageKind(imageKind),
                ShowcaseClipToolHelper.KindFlatlay,
                StringComparison.Ordinal);

            if (flatlay
                && (string.Equals(mode, ShowcaseClipModePresets.DefaultId, StringComparison.Ordinal)
                    || ShowcaseClipModePresets.IsGeminiSuggestMode(mode)))
            {
                motionTool = ShowcaseClipToolHelper.ToolVeo;
                return true;
            }

            if (!flatlay
                && (string.Equals(mode, ShowcaseClipModePresets.KlingVeoZoomId, StringComparison.Ordinal)
                    || ShowcaseClipModePresets.IsGeminiSuggestMode(mode)))
            {
                motionTool = ShowcaseClipToolHelper.ToolKling;
                return true;
            }

            return false;
        }
    }
}
