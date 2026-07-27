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
    }
}
