using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseContentDisplayHelper
    {
        /// <summary>Nhãn cột lưới gộp trạng thái pipeline + output video.</summary>
        public static string FormatPipelineGridLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chờ";
            }

            var status = string.IsNullOrWhiteSpace(video.PipelineStatus)
                ? "Chờ"
                : video.PipelineStatus.Trim();
            var outputPath = (video.OutputVideoPath ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                var fileName = Path.GetFileName(outputPath);
                var filePart = File.Exists(outputPath)
                    ? "▶ " + fileName
                    : fileName + " (thiếu file)";
                if (string.Equals(status, "Chờ", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(outputPath))
                {
                    return filePart;
                }

                return status + " · " + filePart;
            }

            if (string.Equals(status, "Xong", StringComparison.OrdinalIgnoreCase))
            {
                return "Xong · (chưa có file)";
            }

            return status;
        }

        public static string FormatPipelineGridToolTip(ShowcaseVideoItem video)
        {
            var label = FormatPipelineGridLabel(video);
            var outputPath = (video?.OutputVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return label + "\r\n" + outputPath;
            }

            return label;
        }

        public static string TryResolveFinishedVideoPath(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            var tracked = (video.OutputVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(tracked) && File.Exists(tracked))
            {
                return tracked;
            }

            var sessionBase = (video.ShowcaseSessionBaseDir ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(sessionBase))
            {
                var ctaTail = Path.Combine(sessionBase, ShowcaseSessionCleanupHelper.FinalVideoFileName);
                if (File.Exists(ctaTail))
                {
                    return ctaTail;
                }

                var outputDir = ShowcaseNarrationCacheHelper.GetOutputDirectory(sessionBase);
                if (Directory.Exists(outputDir))
                {
                    var newest = Directory.GetFiles(outputDir, "*.mp4")
                        .Select(path => new FileInfo(path))
                        .Where(info => info.Exists)
                        .OrderByDescending(info => info.LastWriteTimeUtc)
                        .Select(info => info.FullName)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(newest))
                    {
                        return newest;
                    }
                }
            }

            return tracked;
        }

        public static string FormatOutputGridLabel(ShowcaseVideoItem video, string resolvedPath = null)
        {
            var path = (resolvedPath ?? TryResolveFinishedVideoPath(video) ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return "▶ " + Path.GetFileName(path);
            }

            if (!string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path) + " (thiếu file)";
            }

            return "Chờ render";
        }

        public static string FormatOutputGridToolTip(ShowcaseVideoItem video, string resolvedPath = null)
        {
            var path = (resolvedPath ?? TryResolveFinishedVideoPath(video) ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return "Bấm để mở video thành phẩm.\r\n" + path;
            }

            if (!string.IsNullOrEmpty(path))
            {
                return "File output đã ghi nhưng không tìm thấy trên đĩa:\r\n" + path;
            }

            return "Chưa có video thành phẩm — render xong sẽ hiện tại đây.";
        }

        public static void RefreshContentLabels(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseScriptLabel = FormatScriptPromptGridLabel(video);
            video.ShowcaseScenePromptLabel = FormatScenePromptLabel(video);
            video.ShowcaseVoiceoverLabel = FormatVoiceoverGridLabel(video);
        }

        /// <summary>Nhãn lưới cột «Kịch bản» — tóm tắt prompt clip (thoại xem cột «Lời thoại»).</summary>
        public static string FormatScriptPromptGridLabel(ShowcaseVideoItem video)
        {
            return FormatScenePromptLabel(video);
        }

        public static string FormatScriptPromptGridToolTip(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            return "Prompt clip: " + FormatScenePromptLabel(video)
                   + "\r\n\r\nThoại từng cảnh → cột «Lời thoại»."
                   + "\r\n\r\nBấm ô → hub «Kịch bản · Prompt» (prompt + chủ đề).";
        }

        public static string FormatVoiceoverGridLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "—";
            }

            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            if (scenes.Count == 0)
            {
                var folderClips = CountRenderFolderClips(video);
                if (folderClips > 0)
                {
                    return folderClips + " clip · chưa gán storyboard";
                }

                return "0 cảnh";
            }

            var clipCount = CountScenesWithClip(scenes);
            var clipPart = clipCount + "/" + scenes.Count + " clip";

            if (ShowcaseVoiceoverHelper.HasClipAlignedVoiceover(video, scenes))
            {
                return "✓ Khớp clip · " + clipPart;
            }

            if (ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes))
            {
                return "⚠ Clip đổi · " + clipPart;
            }

            if (ShowcaseVoiceoverHelper.CountVoicedScenes(scenes) > 0
                || !string.IsNullOrWhiteSpace(video.ShowcaseHookText)
                || !string.IsNullOrWhiteSpace(video.ShowcaseCtaText))
            {
                return "Nháp · " + clipPart;
            }

            return "Chưa có · " + clipPart;
        }

        public static string FormatVoiceoverGridToolTip(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            var lines = new List<string>
            {
                "Trạng thái: " + FormatVoiceoverGridLabel(video),
                "Thoại nháp = «Tạo kịch bản» (theo ảnh).",
                "Thoại khớp clip = «Tạo lời thoại» trong dialog (Gemini xem clip).",
                "Đổi clip sau «Tạo lời thoại» → vẫn render; app cảnh báo lệch thời lượng từng cảnh trước khi render.",
                "Cần ít nhất 1 clip trong clips_render trước khi «Tạo lời thoại».",
                string.Empty,
                "Bấm ô → bảng lời thoại từng cảnh."
            };

            if (scenes.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(FormatScriptHubPreviewVoiceSection(video, scenes));
            }

            return string.Join("\r\n", lines.Where(l => l != null));
        }

        private static int CountRenderFolderClips(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return 0;
            }

            var clipsDir = (video.ShowcaseClipsDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clipsDir))
            {
                clipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(
                    video.ShowcaseSessionBaseDir,
                    createIfMissing: false,
                    migrateLegacy: true);
            }

            return string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir)
                ? 0
                : ShowcaseSessionService.CountClipFilesOnDisk(clipsDir);
        }

        private static int CountScenesWithClip(IList<AiVideoGenInputItem> scenes)
        {
            var count = 0;
            foreach (var scene in scenes)
            {
                var path = (scene?.ClipPath ?? string.Empty).Trim();
                if (path.Length > 0 && File.Exists(path))
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatScriptHubPreviewVoiceSection(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)
        {
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var voice = (scene?.SceneVoiceover ?? string.Empty).Trim();
                if (scene?.ShowcaseSceneSilent == true)
                {
                    voice = "(im lặng)";
                }
                else if (voice.Length == 0)
                {
                    voice = "(chưa có)";
                }

                if (voice.Length > 64)
                {
                    voice = voice.Substring(0, 61) + "…";
                }

                sb.AppendLine("Cảnh " + (i + 1) + ": " + voice);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Nhãn gộp loại SP + chủ đề (đầu vào Gemini trước «Tạo kịch bản»).</summary>
        public static string FormatProductTypeThemeGridLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return ShowcaseProductTypePresets.Auto.DisplayLabel;
            }

            var typePart = ShowcaseProductTypePresets.GetDisplayLabel(video.ShowcaseProductTypePrompt);
            var themePart = video.ShowcaseThemeGridDisplay;
            if (string.IsNullOrWhiteSpace(themePart))
            {
                themePart = ShowcaseThemePresets.GetDisplayLabel(video.ShowcaseThemePrompt);
            }

            if (string.IsNullOrWhiteSpace(themePart))
            {
                themePart = ShowcaseThemePresets.Auto.DisplayLabel;
            }

            return typePart + "  |  " + themePart;
        }

        public static string FormatProductTypeThemeGridToolTip(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            var typeLabel = TruncateGridToolTipLine(
                ShowcaseProductTypePresets.GetDisplayLabel(video.ShowcaseProductTypePrompt),
                80);
            var themeLabel = video.ShowcaseThemeGridDisplay;
            if (string.IsNullOrWhiteSpace(themeLabel))
            {
                themeLabel = ShowcaseThemePresets.GetDisplayLabel(video.ShowcaseThemePrompt);
            }

            if (string.IsNullOrWhiteSpace(themeLabel))
            {
                themeLabel = ShowcaseThemePresets.Auto.DisplayLabel;
            }

            themeLabel = TruncateGridToolTipLine(themeLabel, 80);

            return "Loại SP: " + typeLabel
                   + "\r\nChủ đề: " + themeLabel
                   + "\r\n\r\nBấm ô → chọn «Loại SP» hoặc «Chủ đề».";
        }

        public static string FormatClipModeBrollGridLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return ShowcaseClipModePresets.GetDisplayLabel(ShowcaseClipModePresets.DefaultId);
            }

            var mode = ShowcaseClipModePresets.GetDisplayLabel(
                ShowcaseClipModePresets.ResolveIdForGemini(video.ShowcaseClipModeId));
            var aspect = ShowcaseOutputAspectPresets.GetDisplayLabelForVideo(video);
            var realClipCount = CountRealClipScenes(video);
            var label = mode + "  |  " + aspect;
            return realClipCount > 0
                ? label + "  |  " + realClipCount + " clip quay tay"
                : label;
        }

        private static int CountRealClipScenes(ShowcaseVideoItem video) =>
            video?.Scenes == null
                ? 0
                : video.Scenes.Count(s => s != null
                    && string.Equals(s.ShowcaseClipTool, ShowcaseClipToolHelper.ToolReal, StringComparison.Ordinal));

        public static string FormatClipModeBrollGridToolTip(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            var mode = ShowcaseClipModePresets.GetDisplayLabel(
                ShowcaseClipModePresets.ResolveIdForGemini(video.ShowcaseClipModeId));
            var aspect = ShowcaseOutputAspectPresets.GetDisplayLabelForVideo(video);
            var libraryLabel = ShowcaseCtaBrollLibraryService.GetLibraryDisplayLabel(
                ShowcaseCtaBrollLibraryPaths.ResolveFullLibraryPath(video));
            var realClipCount = CountRealClipScenes(video);
            var lines = "Công cụ clip AI: " + mode
                        + "\r\nKhung video: " + aspect
                        + "\r\nClip quay tay: " + (realClipCount > 0 ? realClipCount + " cảnh" : "chưa có")
                        + "\r\nThư viện: " + libraryLabel;

            return lines + "\r\n\r\nBấm ô → mở bảng «Công cụ Video · Clip quay tay».";
        }

        private static string TruncateGridToolTipLine(string text, int maxChars)
        {
            var trimmed = (text ?? string.Empty).Trim().Replace('\r', ' ').Replace('\n', ' ');
            while (trimmed.Contains("  "))
            {
                trimmed = trimmed.Replace("  ", " ");
            }

            if (trimmed.Length <= maxChars)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxChars).TrimEnd() + "…";
        }

        public static string FormatScriptLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chưa có kịch bản";
            }

            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? Array.Empty<AiVideoGenInputItem>().ToList();
            ShowcaseVoiceoverHelper.SyncSilentFlagsFromVoiceover(scenes);
            var voiceCount = ShowcaseVoiceoverHelper.CountVoicedScenes(scenes);
            var silentCount = ShowcaseVoiceoverHelper.CountSilentScenes(scenes);
            var hasHook = !string.IsNullOrWhiteSpace(video.ShowcaseHookText);
            var hasCta = !string.IsNullOrWhiteSpace(video.ShowcaseCtaText);

            if (voiceCount == 0 && !hasHook && !hasCta)
            {
                return scenes.Count > 0 ? "Chưa có kịch bản" : "0 cảnh";
            }

            var parts = scenes.Count > 0
                ? voiceCount + "/" + scenes.Count + " voice"
                : "0 cảnh";
            if (silentCount > 0)
            {
                parts += " · " + silentCount + " im";
            }
            if (hasHook)
            {
                parts += " · Hook";
            }

            if (hasCta)
            {
                parts += " · CTA";
            }

            return parts;
        }

        public static string FormatScenePromptLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chưa có prompt";
            }

            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? Array.Empty<AiVideoGenInputItem>().ToList();
            if (scenes.Count == 0)
            {
                return "0 cảnh";
            }

            var filled = scenes.Count(ShowcaseClipToolHelper.SceneHasClipPrompt);
            if (filled == 0)
            {
                return "Chưa có prompt";
            }

            var toolCounts = scenes
                .Where(ShowcaseClipToolHelper.SceneHasClipPrompt)
                .GroupBy(s => ShowcaseClipToolHelper.GetToolDisplayLabel(s.ShowcaseClipTool))
                .Select(g => g.Count() + " " + g.Key)
                .ToList();
            var toolsSummary = toolCounts.Count > 0 ? string.Join(", ", toolCounts) : string.Empty;

            var sample = scenes.FirstOrDefault(ShowcaseClipToolHelper.SceneHasClipPrompt);
            var sampleText = ShowcaseClipToolHelper.ResolveScenePrompt(sample)?.Trim() ?? string.Empty;
            if (sampleText.Length > 32)
            {
                sampleText = sampleText.Substring(0, 29) + "…";
            }

            return filled + "/" + scenes.Count + " · " + toolsSummary + " · " + sampleText;
        }

        /// <summary>Nội dung đầy đủ cho hub «Kịch bản · Prompt» — cột kịch bản.</summary>
        public static string FormatScriptHubPreview(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chưa có kịch bản";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Tóm tắt: " + FormatScriptLabel(video));
            sb.AppendLine();

            var hook = (video.ShowcaseHookText ?? string.Empty).Trim();
            if (hook.Length > 0)
            {
                sb.AppendLine("Hook:");
                sb.AppendLine(hook);
                sb.AppendLine();
            }

            var cta = (video.ShowcaseCtaText ?? string.Empty).Trim();
            if (cta.Length > 0)
            {
                sb.AppendLine("CTA:");
                sb.AppendLine(cta);
                sb.AppendLine();
            }

            var scenes = video.Scenes?.Where(s => s != null).ToList()
                           ?? Array.Empty<AiVideoGenInputItem>().ToList();
            if (scenes.Count == 0)
            {
                sb.AppendLine("(Chưa có cảnh — bấm «Tạo kịch bản» hoặc thêm ảnh.)");
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine("Thoại từng cảnh:");
            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var slot = ShowcaseSceneNamingHelper.BuildSceneSlotId(i);
                if (scene.ShowcaseSceneSilent)
                {
                    sb.AppendLine("  " + slot + ": (im lặng / không thoại)");
                    continue;
                }

                var voice = (scene.SceneVoiceover ?? string.Empty).Trim();
                sb.AppendLine("  " + slot + ": "
                              + (voice.Length > 0 ? voice : "(chưa có thoại)"));
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Nội dung đầy đủ cho hub «Kịch bản · Prompt» — cột prompt clip.</summary>
        public static string FormatScenePromptHubPreview(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chưa có prompt";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Tóm tắt: " + FormatScenePromptLabel(video));
            sb.AppendLine();

            var scenes = video.Scenes?.Where(s => s != null).ToList()
                           ?? Array.Empty<AiVideoGenInputItem>().ToList();
            if (scenes.Count == 0)
            {
                sb.AppendLine("(Chưa có cảnh — thêm ảnh hoặc tạo kịch bản trước.)");
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine("Prompt từng cảnh:");
            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var slot = ShowcaseSceneNamingHelper.BuildSceneSlotId(i);
                var tool = ShowcaseClipToolHelper.GetToolDisplayLabel(scene.ShowcaseClipTool);
                var prompt = (ShowcaseClipToolHelper.ResolveScenePrompt(scene) ?? string.Empty).Trim();
                if (prompt.Length == 0)
                {
                    sb.AppendLine("  " + slot + " [" + tool + "]: (chưa có prompt)");
                }
                else
                {
                    sb.AppendLine("  " + slot + " [" + tool + "]:");
                    sb.AppendLine("    " + prompt.Replace("\r\n", "\r\n    "));
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
