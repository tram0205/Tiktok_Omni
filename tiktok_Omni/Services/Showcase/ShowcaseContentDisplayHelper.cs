using System;
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
                return label + "\r\n" + outputPath + "\r\n\r\nBấm để mở thư mục output.";
            }

            return label + "\r\n\r\nBấm để mở thư mục output (nếu đã có phiên render).";
        }

        public static void RefreshContentLabels(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseScriptLabel = FormatScriptPromptGridLabel(video);
            video.ShowcaseScenePromptLabel = FormatScenePromptLabel(video);
        }

        /// <summary>Nhãn lưới gộp tóm tắt kịch bản (thoại) + prompt clip.</summary>
        public static string FormatScriptPromptGridLabel(ShowcaseVideoItem video)
        {
            var script = FormatScriptLabel(video);
            var prompt = FormatScenePromptLabel(video);
            if (string.Equals(script, prompt, StringComparison.Ordinal))
            {
                return script;
            }

            return script + "  |  " + prompt;
        }

        public static string FormatScriptPromptGridToolTip(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            return "Kịch bản (thoại): " + FormatScriptLabel(video)
                   + "\r\nPrompt clip: " + FormatScenePromptLabel(video)
                   + "\r\n\r\nThoại khớp clip chỉ sau «Tạo lời thoại». «Tạo kịch bản» = nháp theo ảnh."
                   + "\r\n\r\nBấm ô → hub «Kịch bản · Prompt».";
        }

        /// <summary>Nhãn lưới gộp loại SP + chủ đề (đầu vào Gemini trước «Tạo kịch bản»).</summary>
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

            return "Loại SP: " + ShowcaseProductTypePresets.GetDisplayLabel(video.ShowcaseProductTypePrompt)
                   + "\r\nChủ đề: " + (video.ShowcaseThemeGridDisplay ?? string.Empty)
                   + "\r\n\r\nBấm ô → chọn «Loại SP» hoặc «Chủ đề».";
        }

        public static string FormatScriptLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chưa có kịch bản";
            }

            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? Array.Empty<AiVideoGenInputItem>().ToList();
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
                var n = i + 1;
                if (scene.ShowcaseSceneSilent)
                {
                    sb.AppendLine("  Cảnh " + n + ": (im lặng / không thoại)");
                    continue;
                }

                var voice = (scene.SceneVoiceover ?? string.Empty).Trim();
                sb.AppendLine("  Cảnh " + n + ": "
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
                var n = i + 1;
                var tool = ShowcaseClipToolHelper.GetToolDisplayLabel(scene.ShowcaseClipTool);
                var prompt = (ShowcaseClipToolHelper.ResolveScenePrompt(scene) ?? string.Empty).Trim();
                if (prompt.Length == 0)
                {
                    sb.AppendLine("  Cảnh " + n + " [" + tool + "]: (chưa có prompt)");
                }
                else
                {
                    sb.AppendLine("  Cảnh " + n + " [" + tool + "]:");
                    sb.AppendLine("    " + prompt.Replace("\r\n", "\r\n    "));
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
