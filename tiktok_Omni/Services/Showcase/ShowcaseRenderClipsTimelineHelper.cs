using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Quét clips_render → timeline thoại Gemini → storyboard render.</summary>
    internal static class ShowcaseRenderClipsTimelineHelper
    {
        public sealed class ClipManifestEntry
        {
            public int InputIndexOneBased { get; set; }

            public string ClipPath { get; set; } = string.Empty;

            public string FileName { get; set; } = string.Empty;

            public bool IsAppSceneFile { get; set; }
        }

        public static IReadOnlyList<ClipManifestEntry> BuildManifest(string clipsDir)
        {
            var list = new List<ClipManifestEntry>();
            var index = 1;
            foreach (var clipPath in ShowcaseSessionService.EnumerateClipFiles(clipsDir))
            {
                if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(clipPath) ?? string.Empty;
                list.Add(new ClipManifestEntry
                {
                    InputIndexOneBased = index++,
                    ClipPath = clipPath,
                    FileName = fileName,
                    IsAppSceneFile = ShowcaseSessionService.TryParseSceneOrderFromFileName(fileName, out _)
                });
            }

            return list;
        }

        public static List<AiVideoGenInputItem> ApplyTimelineToVideo(
            ShowcaseVideoItem video,
            IReadOnlyList<ClipManifestEntry> manifest,
            IList<ShowcaseSceneDto> timelineScenes,
            string profileName)
        {
            if (video == null || manifest == null || manifest.Count == 0)
            {
                return new List<AiVideoGenInputItem>();
            }

            var orderedDtos = (timelineScenes ?? new List<ShowcaseSceneDto>())
                .Where(s => s != null)
                .OrderBy(s => s.order > 0 ? s.order : int.MaxValue)
                .ThenBy(s => s.clip_index > 0 ? s.clip_index : int.MaxValue)
                .ToList();

            if (orderedDtos.Count == 0)
            {
                orderedDtos = manifest
                    .Select((m, i) => new ShowcaseSceneDto { order = i + 1, clip_index = i + 1 })
                    .ToList();
            }

            var existingByClip = IndexExistingScenesByClipPath(video.Scenes);
            var usedClipIndexes = new HashSet<int>();
            var productName = (video.ProductName ?? string.Empty).Trim();
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var result = new List<AiVideoGenInputItem>();

            foreach (var sceneDto in orderedDtos)
            {
                var clipIndex = ResolveClipInputIndex(sceneDto, manifest.Count);
                if (clipIndex < 1 || clipIndex > manifest.Count || !usedClipIndexes.Add(clipIndex))
                {
                    continue;
                }

                var entry = manifest[clipIndex - 1];
                var scene = ResolveOrCreateScene(existingByClip, entry, productName, profile, sceneDto);
                ApplyVoiceoverFromDto(scene, sceneDto);
                result.Add(scene);
            }

            foreach (var entry in manifest)
            {
                if (usedClipIndexes.Contains(entry.InputIndexOneBased))
                {
                    continue;
                }

                var scene = ResolveOrCreateScene(existingByClip, entry, productName, profile, null);
                result.Add(scene);
                logUnused(entry);
            }

            return result;
        }

        private static void logUnused(ClipManifestEntry entry)
        {
            // appended without voiceover — Gemini did not include this clip in timeline
        }

        private static int ResolveClipInputIndex(ShowcaseSceneDto sceneDto, int manifestCount)
        {
            if (sceneDto == null)
            {
                return 0;
            }

            if (sceneDto.clip_index >= 1 && sceneDto.clip_index <= manifestCount)
            {
                return sceneDto.clip_index;
            }

            if (sceneDto.order >= 1 && sceneDto.order <= manifestCount)
            {
                return sceneDto.order;
            }

            return 0;
        }

        private static Dictionary<string, AiVideoGenInputItem> IndexExistingScenesByClipPath(
            IList<AiVideoGenInputItem> scenes)
        {
            var map = new Dictionary<string, AiVideoGenInputItem>(StringComparer.OrdinalIgnoreCase);
            if (scenes == null)
            {
                return map;
            }

            foreach (var scene in scenes)
            {
                if (scene == null)
                {
                    continue;
                }

                foreach (var path in new[] { scene.ClipPath, scene.ShowcaseRealClipSourcePath })
                {
                    if (TryNormalizeClipKey(path, out var key) && !map.ContainsKey(key))
                    {
                        map[key] = scene;
                    }
                }
            }

            return map;
        }

        private static AiVideoGenInputItem ResolveOrCreateScene(
            Dictionary<string, AiVideoGenInputItem> existingByClip,
            ClipManifestEntry entry,
            string productName,
            string profile,
            ShowcaseSceneDto sceneDto)
        {
            if (TryNormalizeClipKey(entry.ClipPath, out var key) && existingByClip.TryGetValue(key, out var existing))
            {
                existing.ClipPath = entry.ClipPath;
                if (!entry.IsAppSceneFile)
                {
                    existing.ShowcaseRealClipSourcePath = entry.ClipPath;
                    if (string.IsNullOrWhiteSpace(existing.ShowcaseClipTool)
                        || string.Equals(existing.ShowcaseClipTool, ShowcaseClipToolHelper.ToolReal, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.ShowcaseClipTool = ShowcaseClipToolHelper.ToolReal;
                    }
                }

                if (!string.IsNullOrWhiteSpace(sceneDto?.scene_title))
                {
                    existing.SceneTitle = sceneDto.scene_title.Trim();
                }

                return existing;
            }

            var isReal = !entry.IsAppSceneFile;
            return new AiVideoGenInputItem
            {
                ProfileName = profile,
                ProductName = productName,
                ClipPath = entry.ClipPath,
                ShowcaseRealClipSourcePath = isReal ? entry.ClipPath : string.Empty,
                ShowcaseClipTool = isReal ? ShowcaseClipToolHelper.ToolReal : string.Empty,
                SceneTitle = (sceneDto?.scene_title ?? Path.GetFileNameWithoutExtension(entry.FileName) ?? string.Empty).Trim(),
                PipelineStatus = "Chờ"
            };
        }

        private static void ApplyVoiceoverFromDto(AiVideoGenInputItem scene, ShowcaseSceneDto sceneDto)
        {
            if (scene == null)
            {
                return;
            }

            if (sceneDto == null)
            {
                return;
            }

            var silent = sceneDto.silent;
            var voice = (sceneDto.voiceover ?? string.Empty).Trim();
            if (silent && voice.Length > 0)
            {
                silent = false;
            }

            // Gemini «Tạo lời thoại» — phụ đề burn-in lấy lại theo thoại mới (không giữ rút gọn cũ).
            scene.ShowcaseSubtitleDisplayVoiceover = string.Empty;

            scene.ShowcaseSceneSilent = silent;
            scene.SceneVoiceover = silent ? string.Empty : voice;

            if (sceneDto.clip_duration_seconds > 0)
            {
                scene.ShowcaseClipDurationSeconds = ShowcaseSceneDurationHelper.Clamp(sceneDto.clip_duration_seconds);
            }
            else if (!silent && voice.Length > 0)
            {
                scene.ShowcaseClipDurationSeconds = ShowcaseSceneDurationHelper.EstimateFromVoiceover(voice, false);
            }
        }

        private static bool TryNormalizeClipKey(string path, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                key = Path.GetFullPath(path.Trim());
                return true;
            }
            catch
            {
                key = path.Trim();
                return key.Length > 0;
            }
        }
    }
}
