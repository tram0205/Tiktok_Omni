using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;



namespace tiktok_Omni.Services.Showcase

{

    public static class ShowcaseVoiceoverHelper

    {

        public const int MaxSilentScenes = 1;



        public static bool HasCompleteVoiceover(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)

        {

            if (video == null || scenes == null || !ShowcaseWorkflowConstants.HasEnoughScenes(scenes.Count))

            {

                return false;

            }



            if (CountSilentScenes(scenes) > MaxSilentScenes)

            {

                return false;

            }



            if (CountVoicedScenes(scenes) < scenes.Count - MaxSilentScenes)

            {

                return false;

            }



            return scenes.All(s =>

                s != null &&

                (s.ShowcaseSceneSilent || !string.IsNullOrWhiteSpace(s.SceneVoiceover)));

        }

        /// <summary>Thoại đã sinh từ clip (Gemini «Tạo lời thoại») và clip chưa đổi kể từ lần sinh đó.</summary>
        public static bool IsVoiceoverSyncedToClips(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)
        {
            if (video == null || scenes == null || !ShowcaseWorkflowConstants.HasEnoughScenes(scenes.Count))
            {
                return false;
            }

            if (video.ShowcaseVoiceoverClipFingerprint == 0)
            {
                return false;
            }

            return video.ShowcaseVoiceoverClipFingerprint == ComputeClipFingerprint(scenes);
        }

        public static bool HasClipAlignedVoiceover(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)
        {
            return HasCompleteVoiceover(video, scenes) && IsVoiceoverSyncedToClips(video, scenes);
        }

        /// <summary>
        /// Chế độ Chỉ Zoom: clip Ken Burns được tạo lại (cùng ảnh, file mới) nhưng thoại vẫn hợp lệ —
        /// cập nhật fingerprint thay vì bắt chạy Gemini «Tạo lời thoại» lại.
        /// </summary>
        public static bool TryResyncFingerprintForZoomOnlyClipRefresh(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string clipModeId)
        {
            if (video == null || scenes == null || scenes.Count == 0)
            {
                return false;
            }

            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);
            if (!string.Equals(mode, ShowcaseClipModePresets.ZoomOnlyId, StringComparison.Ordinal))
            {
                return false;
            }

            if (video.ShowcaseVoiceoverClipFingerprint == 0)
            {
                return false;
            }

            if (!HasCompleteVoiceover(video, scenes))
            {
                return false;
            }

            if (IsVoiceoverSyncedToClips(video, scenes))
            {
                return false;
            }

            if (ShowcaseClipStatusHelper.CountScenesWithClip(scenes) == 0)
            {
                return false;
            }

            video.ShowcaseVoiceoverClipFingerprint = ComputeClipFingerprint(scenes);
            return true;
        }

        public static long ComputeClipFingerprint(IList<AiVideoGenInputItem> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return 0;
            }

                unchecked
                {
                    long hash = 17;
                    for (var i = 0; i < scenes.Count; i++)
                    {
                        var path = (scenes[i]?.ClipPath ?? string.Empty).Trim();
                        hash = (hash * 31) + (i + 1);
                        if (path.Length == 0 || !File.Exists(path))
                        {
                            hash = (hash * 31) + 0;
                            continue;
                        }

                        hash = (hash * 31) + path.ToUpperInvariant().GetHashCode();
                        hash = (hash * 31) + File.GetLastWriteTimeUtc(path).Ticks;
                    }

                    return hash;
                }
        }



        public static int CountVoicedScenes(IList<AiVideoGenInputItem> scenes)

        {

            SyncSilentFlagsFromVoiceover(scenes);

            return scenes?.Count(s => s != null && !s.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(s.SceneVoiceover)) ?? 0;

        }



        public static int CountSilentScenes(IList<AiVideoGenInputItem> scenes)

        {

            SyncSilentFlagsFromVoiceover(scenes);

            return scenes?.Count(s => s != null && s.ShowcaseSceneSilent) ?? 0;

        }



        /// <summary>Cảnh đã có lời thoại thì bỏ cờ im — tránh nhãn «4/5 voice · 1 im» khi hub vẫn hiện đủ thoại.</summary>

        public static void SyncSilentFlagsFromVoiceover(IList<AiVideoGenInputItem> scenes)

        {

            if (scenes == null || scenes.Count == 0)

            {

                return;

            }



            foreach (var scene in scenes)

            {

                if (scene == null)

                {

                    continue;

                }



                if (!string.IsNullOrWhiteSpace(scene.SceneVoiceover))

                {

                    scene.ShowcaseSceneSilent = false;

                }

            }



            EnforceMaxSilentScenes(scenes);

        }



        /// <summary>Giữ tối đa 1 cảnh im — các cảnh im thừa được bật lại thoại nếu Gemini trả nhiều hơn.</summary>

        public static void EnforceMaxSilentScenes(IList<AiVideoGenInputItem> scenes)

        {

            if (scenes == null || scenes.Count == 0)

            {

                return;

            }



            var silentIndexes = new List<int>();

            for (var i = 0; i < scenes.Count; i++)

            {

                if (scenes[i]?.ShowcaseSceneSilent == true)

                {

                    silentIndexes.Add(i);

                }

            }



            if (silentIndexes.Count <= MaxSilentScenes)

            {

                return;

            }



            for (var k = MaxSilentScenes; k < silentIndexes.Count; k++)

            {

                var scene = scenes[silentIndexes[k]];

                if (scene == null)

                {

                    continue;

                }



                scene.ShowcaseSceneSilent = false;

            }

        }

    }

}


