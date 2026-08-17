using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        /// Clip đổi (file/path) — giữ thoại, cập nhật fingerprint file.
        /// </summary>
        public static bool TryResyncFingerprintForClipFileRefresh(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string currentDurationSignature)
        {
            if (video == null || scenes == null || scenes.Count == 0)
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

            video.ShowcaseVoiceoverClipPathFingerprint = ComputeClipPathFingerprint(scenes);
            video.ShowcaseVoiceoverClipFingerprint = ComputeClipFingerprint(scenes);
            return true;
        }

        public static string BuildClipDurationDriftSummary(
            ShowcaseVideoItem video,
            string currentDurationSignature)
        {
            if (video == null)
            {
                return string.Empty;
            }

            var baseline = (video.ShowcaseVoiceoverClipDurationSignature ?? string.Empty).Trim();
            if (baseline.Length == 0)
            {
                return string.Empty;
            }

            return ShowcaseClipDurationHelper.BuildDriftSummary(baseline, currentDurationSignature);
        }

        public static bool IsClipDurationAlignedWithVoiceover(
            ShowcaseVideoItem video,
            string currentDurationSignature)
        {
            if (video == null)
            {
                return false;
            }

            var baseline = (video.ShowcaseVoiceoverClipDurationSignature ?? string.Empty).Trim();
            if (baseline.Length == 0)
            {
                return false;
            }

            return ShowcaseClipDurationHelper.SignaturesMatch(baseline, currentDurationSignature);
        }

        public static bool CanRenderWithExistingVoiceover(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string currentDurationSignature)
        {
            return HasCompleteVoiceover(video, scenes);
        }

        public static async Task StampClipVoiceoverBaselineAsync(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string ffprobeExecutable,
            CancellationToken cancellationToken)
        {
            if (video == null || scenes == null || scenes.Count == 0)
            {
                return;
            }

            if (ShowcaseClipStatusHelper.CountScenesWithClip(scenes) == 0)
            {
                return;
            }

            video.ShowcaseVoiceoverClipPathFingerprint = ComputeClipPathFingerprint(scenes);
            video.ShowcaseVoiceoverClipFingerprint = ComputeClipFingerprint(scenes);
            video.ShowcaseVoiceoverClipDurationSignature = await ShowcaseClipDurationHelper.BuildSignatureAsync(
                    scenes,
                    ffprobeExecutable,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Đủ lời thoại — thời lượng clip lệch chỉ cảnh báo trước render.</summary>
        public static bool HasRenderableVoiceover(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string currentDurationSignature)
        {
            return CanRenderWithExistingVoiceover(video, scenes, currentDurationSignature);
        }

        /// <summary>
        /// Chế độ Chỉ Zoom: clip Ken Burns được tạo lại (cùng ảnh, file mới) nhưng thoại vẫn hợp lệ —
        /// cập nhật fingerprint thay vì bắt chạy Gemini «Tạo lời thoại» lại.
        /// </summary>
        public static bool TryResyncFingerprintForZoomOnlyClipRefresh(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string clipModeId,
            string currentDurationSignature)
        {
            return TryResyncFingerprintForClipFileRefresh(video, scenes, currentDurationSignature);
        }

        public static long ComputeClipPathFingerprint(IList<AiVideoGenInputItem> scenes)
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
                    if (path.Length == 0)
                    {
                        hash = (hash * 31) + 0;
                        continue;
                    }

                    hash = (hash * 31) + path.ToUpperInvariant().GetHashCode();
                }

                return hash;
            }
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
                        try
                        {
                            var info = new FileInfo(path);
                            hash = (hash * 31) + info.Length;
                            hash = (hash * 31) + info.LastWriteTimeUtc.Ticks;
                        }
                        catch
                        {
                            // ignored
                        }
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

        /// <summary>Ghi Hook/CTA cấp video vào SceneVoiceover cảnh đầu/cuối — draft lưu đủ khi mở lại.</summary>
        public static void PersistHookCtaIntoSceneVoiceovers(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)
        {
            if (video == null || scenes == null || scenes.Count == 0)
            {
                return;
            }

            var hook = (video.ShowcaseHookText ?? string.Empty).Trim();
            if (hook.Length > 0 && scenes[0] != null)
            {
                scenes[0].SceneVoiceover = hook;
                scenes[0].ShowcaseSceneSilent = false;
            }

            if (scenes.Count > 1)
            {
                var cta = (video.ShowcaseCtaText ?? string.Empty).Trim();
                var last = scenes[scenes.Count - 1];
                if (cta.Length > 0 && last != null)
                {
                    last.SceneVoiceover = cta;
                    last.ShowcaseSceneSilent = false;
                }
            }
        }

        public static void StampClipFingerprintIfVoiced(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)
        {
            if (video == null || scenes == null || scenes.Count == 0)
            {
                return;
            }

            SyncSilentFlagsFromVoiceover(scenes);
            if (CountVoicedScenes(scenes) == 0
                && string.IsNullOrWhiteSpace(video.ShowcaseHookText)
                && string.IsNullOrWhiteSpace(video.ShowcaseCtaText))
            {
                return;
            }

            if (ShowcaseClipStatusHelper.CountScenesWithClip(scenes) > 0)
            {
                video.ShowcaseVoiceoverClipPathFingerprint = ComputeClipPathFingerprint(scenes);
                video.ShowcaseVoiceoverClipFingerprint = ComputeClipFingerprint(scenes);
            }
        }

        public static void ClearClipVoiceoverBaseline(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseVoiceoverClipFingerprint = 0;
            video.ShowcaseVoiceoverClipPathFingerprint = 0;
            video.ShowcaseVoiceoverClipDurationSignature = string.Empty;
        }

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


