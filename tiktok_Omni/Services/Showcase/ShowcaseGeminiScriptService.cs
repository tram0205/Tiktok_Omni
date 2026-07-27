using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Text.RegularExpressions;

using System.Threading;

using System.Threading.Tasks;

using Newtonsoft.Json;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase

{

    /// <summary>Gemini vision: xem ảnh sản phẩm (≥1 cảnh), tự suy chủ đề, sắp cảnh, viết voiceover + prompt clip cho từng cảnh.</summary>

    public sealed class ShowcaseGeminiScriptService

    {

        private readonly GeminiService _gemini = new GeminiService();



        public async Task<ShowcaseScriptResult> GenerateAsync(

            IList<AiVideoGenInputItem> scenesWithLocalImages,

            string userTheme,

            string userProductType,

            string userClipModeId,

            string userOutputAspectId,

            AppSettings settings,

            CancellationToken cancellationToken)

        {

            if (scenesWithLocalImages == null || !ShowcaseWorkflowConstants.HasEnoughScenes(scenesWithLocalImages.Count))

            {

                throw new InvalidOperationException("Showcase cần ít nhất 1 ảnh trên storyboard để sinh kịch bản.");

            }



            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))

            {

                throw new InvalidOperationException("Cần cấu hình AI API Key trong tab Cài đặt.");

            }



            var imagePaths = scenesWithLocalImages.Select(s => s?.ThumbnailPath).ToList();

            var missingIndex = imagePaths.FindIndex(p => string.IsNullOrWhiteSpace(p) || !File.Exists(p));

            if (missingIndex >= 0)

            {

                throw new InvalidOperationException(

                    "Ảnh cảnh " + (missingIndex + 1) + " chưa được tải về máy — hãy thử lại (app tự tải ảnh trước khi gửi Gemini).");

            }



            var productName = scenesWithLocalImages[0]?.ProductName ?? string.Empty;

            var clipModeId = ShowcaseClipModePresets.ResolveIdForGemini(userClipModeId);

            var outputAspectId = ShowcaseOutputAspectPresets.ResolveId(userOutputAspectId, settings?.ShowcaseOutputAspectDefault);

            var prompt = ShowcaseScriptPromptBuilder.Build(

                productName,

                userTheme,

                userProductType,

                clipModeId,

                outputAspectId,

                scenesWithLocalImages.Count,

                settings);



            var raw = await _gemini.GenerateScriptWithImagesAsync(

                prompt,

                imagePaths,

                settings.AiProvider,

                settings.AiApiKey,

                settings.AiModel,

                cancellationToken).ConfigureAwait(false);



            var json = ExtractJsonObject(raw);

            var dto = JsonConvert.DeserializeObject<ShowcaseScriptDto>(json);

            if (dto?.scenes == null || dto.scenes.Count == 0)

            {

                throw new InvalidOperationException("Gemini không trả về kịch bản Showcase hợp lệ.");

            }



            return MapToResult(dto, scenesWithLocalImages, clipModeId, settings);

        }



        private static ShowcaseScriptResult MapToResult(

            ShowcaseScriptDto dto,

            IList<AiVideoGenInputItem> source,

            string clipModeId,

            AppSettings settings)

        {

            var ordered = new List<AiVideoGenInputItem>();

            var used = new HashSet<int>();

            foreach (var scene in dto.scenes.OrderBy(s => s.order))

            {

                var idx = scene.image_index - 1;

                if (idx < 0 || idx >= source.Count || used.Contains(idx))

                {

                    continue;

                }



                used.Add(idx);

                var item = source[idx];

                item.SceneRole = (scene.role ?? string.Empty).Trim();

                item.SceneTitle = (scene.scene_title ?? string.Empty).Trim();

                item.SceneVoiceover = (scene.voiceover ?? string.Empty).Trim();

                item.ShowcaseSceneSilent = scene.silent;

                item.ShowcaseClipDurationSeconds = scene.clip_duration_seconds > 0
                    ? ShowcaseSceneDurationHelper.Clamp(scene.clip_duration_seconds)
                    : ShowcaseSceneDurationHelper.EstimateFromVoiceover(item.SceneVoiceover, scene.silent);

                var imageKind = ShowcaseClipToolHelper.NormalizeImageKind(scene.image_kind, scene.veo_prompt);

                var clipTool = ShowcaseClipToolHelper.EnforceToolForMode(clipModeId, imageKind, scene.clip_tool);

                item.ShowcaseImageKind = imageKind;

                item.ShowcaseClipTool = clipTool;



                item.VeoPrompt = string.Empty;

                item.KlingPrompt = string.Empty;

                item.ZoomHint = string.Empty;
                item.ShowcaseZoomStyleId = string.Empty;



                if (string.Equals(clipTool, ShowcaseClipToolHelper.ToolVeo, StringComparison.Ordinal))

                {

                    item.VeoPrompt = ShowcaseVeoPromptSanitizer.Sanitize(

                        (scene.veo_prompt ?? string.Empty).Trim(),

                        ordered.Count);

                }

                else if (string.Equals(clipTool, ShowcaseClipToolHelper.ToolKling, StringComparison.Ordinal))

                {

                    var klingRaw = (scene.kling_prompt ?? string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(klingRaw))

                    {

                        klingRaw = (scene.veo_prompt ?? string.Empty).Trim();

                    }



                    item.KlingPrompt = ShowcaseKlingPromptSanitizer.Sanitize(klingRaw, ordered.Count);

                }

                else

                {

                    var zoomRaw = (scene.zoom_hint ?? string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(zoomRaw))

                    {

                        zoomRaw = InferDefaultZoomHint(imageKind, scene.veo_prompt);

                    }



                    item.ZoomHint = zoomRaw;
                    item.ShowcaseZoomStyleId = ShowcaseZoomStyleCatalog.ResolveStyleId(
                        scene.zoom_style,
                        zoomRaw,
                        imageKind,
                        ordered.Count);
                }

                ApplyGeminiSfxToScene(item, scene, settings);

                ordered.Add(item);

            }



            for (var i = 0; i < source.Count; i++)

            {

                if (!used.Contains(i))

                {

                    ordered.Add(source[i]);

                }

            }



            return new ShowcaseScriptResult

            {

                Theme = (dto.theme ?? string.Empty).Trim(),

                HookText = (dto.hook_text ?? string.Empty).Trim(),

                CtaText = (dto.cta_text ?? string.Empty).Trim(),

                OrderedScenes = ordered,

                CtaSfxFile = ResolveGeminiSfxFile(settings, dto.cta_sfx_id),

                CtaSfxGeminiHint = (dto.cta_sfx_hint ?? string.Empty).Trim(),

                HookSfxFile = ResolveGeminiSfxFile(settings, dto.hook_sfx_id),

                HookSfxGeminiHint = (dto.hook_sfx_hint ?? string.Empty).Trim(),

                BackgroundMusicFile = ResolveGeminiMusicFile(settings, dto.background_music_id),

                BackgroundMusicGeminiHint = (dto.background_music_hint ?? string.Empty).Trim()

            };

        }

        private static void ApplyGeminiSfxToScene(AiVideoGenInputItem item, ShowcaseSceneDto scene, AppSettings settings)
        {
            if (item == null || scene == null)
            {
                return;
            }

            var file = ResolveGeminiSfxFile(settings, scene.sfx_id);
            item.ShowcaseSfxFile = file;
            item.ShowcaseSfxEnabled = !string.IsNullOrWhiteSpace(file);
            item.ShowcaseSfxPlacement = ShowcaseSfxCatalog.NormalizePlacement(scene.sfx_placement);
            item.ShowcaseSfxOffsetSeconds = 0d;
            item.ShowcaseSfxVolumePercent = ShowcaseSfxCatalog.DefaultVolumePercent;
            item.ShowcaseSfxGeminiHint = (scene.sfx_hint ?? string.Empty).Trim();
        }

        private static string ResolveGeminiSfxFile(AppSettings settings, string sfxId)
        {
            if (ShowcaseSfxCatalog.IsNoneId(sfxId))
            {
                return string.Empty;
            }

            return ShowcaseSfxCatalog.ResolveIdToFileName(settings, sfxId);
        }

        private static string ResolveGeminiMusicFile(AppSettings settings, string musicId)
        {
            if (OmniAudioLibrary.IsNoneId(musicId))
            {
                return string.Empty;
            }

            return OmniAudioLibrary.ResolveMusicIdToFileName(settings, musicId);
        }



        private static string InferDefaultZoomHint(string imageKind, string veoPrompt)

        {

            if (string.Equals(imageKind, ShowcaseClipToolHelper.KindOnModel, StringComparison.Ordinal))

            {

                return "slow cinematic push-in on outfit detail, gentle pan";

            }



            return "slow push-in on product center, subtle parallax";

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


