using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Gemini xem mọi clip trong clips_render → sắp timeline + viết thoại khớp từng clip.</summary>
    public sealed class ShowcaseGeminiVoiceoverService
    {
        private readonly GeminiService _gemini = new GeminiService();

        /// <summary>
        /// Quét folder clips_render, gửi toàn bộ clip cho Gemini, nhận timeline (có thể đổi thứ tự) → cập nhật storyboard.
        /// </summary>
        public async Task<ShowcaseVoiceoverResult> GenerateFromRenderFolderAsync(
            ShowcaseVideoItem video,
            string clipsDir,
            string userTheme,
            string productTypePrompt,
            string profileName,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string userVideoFormatId = null)
        {
            if (video == null)
            {
                throw new ArgumentNullException(nameof(video));
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                throw new InvalidOperationException("Cần cấu hình AI API Key trong tab Cài đặt.");
            }

            var resolvedDir = (clipsDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedDir))
            {
                resolvedDir = ShowcaseRenderClipsPaths.ResolveDirectory(
                    video.ShowcaseSessionBaseDir,
                    createIfMissing: false);
            }
            else
            {
                var sessionBase = Path.GetDirectoryName(resolvedDir);
                if (!string.IsNullOrWhiteSpace(sessionBase))
                {
                    ShowcaseRenderClipsPaths.ResolveDirectory(sessionBase, createIfMissing: false, migrateLegacy: true);
                }

                if (!Directory.Exists(resolvedDir))
                {
                    resolvedDir = ShowcaseRenderClipsPaths.ResolveDirectory(sessionBase, createIfMissing: false);
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedDir) || !Directory.Exists(resolvedDir))
            {
                throw new InvalidOperationException(
                    "Chưa có thư mục " + ShowcaseRenderClipsPaths.FolderName
                    + " — bấm «Duyệt video vào bảng» để thêm clip trước.");
            }

            var manifest = ShowcaseRenderClipsTimelineHelper.BuildManifest(resolvedDir);
            if (manifest.Count == 0)
            {
                throw new InvalidOperationException(
                    "Chưa có clip nào trong " + ShowcaseRenderClipsPaths.FolderName
                    + " — thêm clip AI hoặc quay tay rồi bấm «Tạo lời thoại» lại.");
            }

            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();

            var clipPaths = new List<string>();
            var sceneDurations = new List<double>();
            foreach (var entry in manifest)
            {
                clipPaths.Add(entry.ClipPath);
                var duration = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                        ffprobe,
                        entry.ClipPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                sceneDurations.Add(duration > 0 ? duration : ShowcaseSceneDurationHelper.DefaultClipSeconds);
            }

            logAction?.Invoke("[Showcase] Gửi Gemini " + manifest.Count + " clip từ "
                              + ShowcaseRenderClipsPaths.FolderName
                              + " — có thể sắp lại thứ tự timeline cho hợp lý.");

            var productName = (video.ProductName ?? string.Empty).Trim();
            var productTypeLabel = ShowcaseProductTypePresets.GetDisplayLabel(productTypePrompt);
            var prompt = ShowcaseVoiceoverPromptBuilder.BuildFromRenderFolder(
                productName,
                productTypeLabel,
                userTheme,
                manifest,
                sceneDurations,
                userVideoFormatId);

            logAction?.Invoke("[Showcase] Gemini đang nén " + clipPaths.Count
                              + " clip (144p, giữ tốc độ) và dựng timeline thoại…");

            var raw = await _gemini.GenerateShowcaseVoiceoverFromClipsAsync(
                    prompt,
                    clipPaths,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);

            var json = ExtractJsonObject(raw);
            var dto = JsonConvert.DeserializeObject<ShowcaseScriptDto>(json);
            if (dto?.scenes == null || dto.scenes.Count == 0)
            {
                throw new InvalidOperationException("Gemini không trả về thoại Showcase hợp lệ.");
            }

            var timelineScenes = ShowcaseRenderClipsTimelineHelper.ApplyTimelineToVideo(
                video,
                manifest,
                dto.scenes,
                profileName);

            if (timelineScenes.Count == 0)
            {
                throw new InvalidOperationException("Không ghép được timeline clip — thử «Tạo lời thoại» lại.");
            }

            video.Scenes.Clear();
            foreach (var scene in timelineScenes)
            {
                video.Scenes.Add(scene);
            }

            ShowcaseSceneNamingHelper.ApplyConventionSceneTitles(video.Scenes);
            ShowcaseVoiceoverHelper.EnforceMaxSilentScenes(video.Scenes);

            var ctaText = ShowcaseCtaDedupHelper.NormalizeScriptCta(
                video.Scenes,
                (dto.cta_text ?? string.Empty).Trim());

            logAction?.Invoke("[Showcase] Timeline render: " + timelineScenes.Count + " cảnh — thứ tự theo Gemini.");

            return new ShowcaseVoiceoverResult
            {
                Theme = (dto.theme ?? string.Empty).Trim(),
                HookText = (dto.hook_text ?? string.Empty).Trim(),
                CtaText = ctaText,
                Scenes = video.Scenes.ToList()
            };
        }

        /// <summary>Giữ tương thích — ưu tiên gọi <see cref="GenerateFromRenderFolderAsync"/>.</summary>
        public Task<ShowcaseVoiceoverResult> GenerateAsync(
            IList<AiVideoGenInputItem> orderedScenesWithClips,
            string userTheme,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken,
            string userVideoFormatId = null)
        {
            var video = new ShowcaseVideoItem
            {
                ProductName = orderedScenesWithClips?.FirstOrDefault()?.ProductName ?? string.Empty
            };
            if (orderedScenesWithClips != null)
            {
                foreach (var scene in orderedScenesWithClips)
                {
                    if (scene != null)
                    {
                        video.Scenes.Add(scene);
                    }
                }
            }

            var clipsDir = orderedScenesWithClips?
                .Select(s => s?.ClipPath)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
            clipsDir = string.IsNullOrWhiteSpace(clipsDir) ? string.Empty : Path.GetDirectoryName(clipsDir);

            return GenerateFromRenderFolderAsync(
                video,
                clipsDir,
                userTheme,
                string.Empty,
                orderedScenesWithClips?.FirstOrDefault()?.ProfileName ?? string.Empty,
                settings,
                logAction,
                cancellationToken,
                userVideoFormatId);
        }

        private static string ExtractJsonObject(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                text = Regex.Replace(text, "^```[a-zA-Z]*\\s*", string.Empty, RegexOptions.Multiline);
                text = Regex.Replace(text, "```\\s*$", string.Empty, RegexOptions.Multiline).Trim();
            }

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text.Substring(start, end - start + 1);
            }

            throw new InvalidOperationException("Không tách được JSON object từ phản hồi Gemini.");
        }
    }
}
