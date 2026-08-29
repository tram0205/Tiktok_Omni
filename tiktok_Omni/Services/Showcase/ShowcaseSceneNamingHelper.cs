using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Quy ước tên phân cảnh theo video — scene_01 (clip), photo_01 (ảnh gốc).</summary>
    internal static class ShowcaseSceneNamingHelper
    {
        private static readonly Regex SlotPrefixRegex = new Regex(
            @"^scene_\d{2}\s*[·\-\—\.\:]\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex EmbeddedRoleRegex = new Regex(
            @"^\[(?<role>[^\]]+)\]\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string BuildSceneSlotId(int sceneIndexZeroBased) =>
            "scene_" + (sceneIndexZeroBased + 1).ToString("D2", CultureInfo.InvariantCulture);

        public static string BuildPhotoSlotId(int sceneIndexZeroBased) =>
            "photo_" + (sceneIndexZeroBased + 1).ToString("D2", CultureInfo.InvariantCulture);

        public static string BuildClipFileName(int sceneIndexZeroBased, string extension = ".mp4")
        {
            var ext = (extension ?? string.Empty).Trim();
            if (ext.Length == 0)
            {
                ext = ".mp4";
            }

            if (!ext.StartsWith(".", StringComparison.Ordinal))
            {
                ext = "." + ext;
            }

            return BuildSceneSlotId(sceneIndexZeroBased) + ext;
        }

        public static bool HasSceneSlotPrefix(string text) =>
            !string.IsNullOrWhiteSpace(text)
            && SlotPrefixRegex.IsMatch(text.Trim());

        public static string StripSceneSlotPrefix(string text)
        {
            var line = (text ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                return line;
            }

            return SlotPrefixRegex.Replace(line, string.Empty).Trim();
        }

        /// <summary>scene_01 · [attention] Mở hộp — khớp clips_render/scene_01.mp4.</summary>
        public static string BuildSceneTitle(int sceneIndexZeroBased, string creativeTitle, string role)
        {
            var slot = BuildSceneSlotId(sceneIndexZeroBased);
            var core = StripSceneSlotPrefix(creativeTitle);
            core = StripEmbeddedRole(core);
            if (core.Length == 0)
            {
                core = "Cảnh " + (sceneIndexZeroBased + 1).ToString(CultureInfo.InvariantCulture);
            }

            var roleText = (role ?? string.Empty).Trim();
            if (roleText.Length > 0)
            {
                return slot + " · [" + roleText + "] " + core;
            }

            return slot + " · " + core;
        }

        public static string FormatDisplayLabel(AiVideoGenInputItem scene, int sceneIndexZeroBased)
        {
            if (scene == null)
            {
                return BuildSceneTitle(sceneIndexZeroBased, string.Empty, string.Empty);
            }

            var title = (scene.SceneTitle ?? string.Empty).Trim();
            if (title.Length > 0 && HasSceneSlotPrefix(title))
            {
                return title;
            }

            return BuildSceneTitle(sceneIndexZeroBased, title, scene.SceneRole);
        }

        public static void ApplyConventionSceneTitles(IList<AiVideoGenInputItem> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return;
            }

            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                if (scene == null)
                {
                    continue;
                }

                scene.SceneTitle = BuildSceneTitle(i, scene.SceneTitle, scene.SceneRole);
            }
        }

        private static string StripEmbeddedRole(string text)
        {
            var line = (text ?? string.Empty).Trim();
            if (line.Length == 0)
            {
                return line;
            }

            return EmbeddedRoleRegex.Replace(line, string.Empty).Trim();
        }
    }
}
