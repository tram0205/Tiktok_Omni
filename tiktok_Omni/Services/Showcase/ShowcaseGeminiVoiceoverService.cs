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

    /// <summary>Gemini xem clip phân cảnh (nén trước) → viết hook/voiceover/CTA khớp hình và chủ đề.</summary>

    public sealed class ShowcaseGeminiVoiceoverService

    {

        private readonly GeminiService _gemini = new GeminiService();



        public async Task<ShowcaseVoiceoverResult> GenerateAsync(

            IList<AiVideoGenInputItem> orderedScenesWithClips,

            string userTheme,

            AppSettings settings,

            Action<string> logAction,

            CancellationToken cancellationToken)

        {

            if (orderedScenesWithClips == null || !ShowcaseWorkflowConstants.HasEnoughScenes(orderedScenesWithClips.Count))

            {

                throw new InvalidOperationException("Showcase cần ít nhất 1 cảnh trên storyboard để sinh thoại.");

            }



            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))

            {

                throw new InvalidOperationException("Cần cấu hình AI API Key trong tab Cài đặt.");

            }



            var clipSceneIndexes = new List<int>();

            for (var i = 0; i < orderedScenesWithClips.Count; i++)

            {

                if (ShowcaseClipStatusHelper.SceneHasClipFile(orderedScenesWithClips[i]))

                {

                    clipSceneIndexes.Add(i);

                }

            }



            if (clipSceneIndexes.Count == 0)

            {

                throw new InvalidOperationException(

                    "Chưa có clip nào trong veo_clips — bỏ ít nhất scene_01.mp4 (…) rồi bấm «Tạo lời thoại» lại.");

            }



            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);

            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();



            var clipPaths = new List<string>();

            var sceneDurations = new List<double>();

            foreach (var index in clipSceneIndexes)

            {

                var clipPath = orderedScenesWithClips[index].ClipPath;

                clipPaths.Add(clipPath);

                var duration = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(ffprobe, clipPath, cancellationToken)

                    .ConfigureAwait(false);

                sceneDurations.Add(duration > 0 ? duration : ShowcaseSceneDurationHelper.DefaultClipSeconds);
                orderedScenesWithClips[index].ShowcaseClipDurationSeconds =
                    ShowcaseSceneDurationHelper.Clamp(sceneDurations[sceneDurations.Count - 1]);

            }



            if (clipSceneIndexes.Count < orderedScenesWithClips.Count)

            {

                logAction?.Invoke("[Showcase] Chỉ gửi Gemini " + clipSceneIndexes.Count + "/" + orderedScenesWithClips.Count

                    + " cảnh có clip — cảnh thiếu clip giữ thoại nháp (ảnh) cho đến khi bổ sung clip.");

            }



            var productName = orderedScenesWithClips[0]?.ProductName ?? string.Empty;

            var prompt = ShowcaseVoiceoverPromptBuilder.Build(

                productName,

                userTheme,

                clipSceneIndexes.Count,

                sceneDurations);



            logAction?.Invoke("[Showcase] Gemini đang nén " + clipPaths.Count + " clip (144p, giữ tốc độ) và phân tích thoại…");

            var raw = await _gemini.GenerateShowcaseVoiceoverFromClipsAsync(

                prompt,

                clipPaths,

                settings.AiProvider,

                settings.AiApiKey,

                settings.AiModel,

                logAction,

                cancellationToken).ConfigureAwait(false);



            var json = ExtractJsonObject(raw);

            var dto = JsonConvert.DeserializeObject<ShowcaseScriptDto>(json);

            if (dto?.scenes == null || dto.scenes.Count == 0)

            {

                throw new InvalidOperationException("Gemini không trả về thoại Showcase hợp lệ.");

            }



            return MapToResult(dto, orderedScenesWithClips, clipSceneIndexes);

        }



        private static ShowcaseVoiceoverResult MapToResult(

            ShowcaseScriptDto dto,

            IList<AiVideoGenInputItem> allScenes,

            IList<int> clipSceneIndexes)

        {

            var sceneDtoByOrder = dto.scenes

                .Where(s => s != null && s.order > 0)

                .GroupBy(s => s.order)

                .ToDictionary(g => g.Key, g => g.First());



            for (var k = 0; k < clipSceneIndexes.Count; k++)

            {

                var sceneIndex = clipSceneIndexes[k];

                if (sceneIndex < 0 || sceneIndex >= allScenes.Count)

                {

                    continue;

                }



                var scene = allScenes[sceneIndex];

                if (scene == null)

                {

                    continue;

                }



                var dtoOrder = k + 1;

                ShowcaseSceneDto sceneDto = null;

                if (!sceneDtoByOrder.TryGetValue(dtoOrder, out sceneDto))

                {

                    sceneDto = dto.scenes.ElementAtOrDefault(k);

                }



                if (sceneDto == null)

                {

                    scene.ShowcaseSceneSilent = false;

                    continue;

                }



                var silent = sceneDto.silent;

                if (silent && !string.IsNullOrWhiteSpace(sceneDto.voiceover))

                {

                    silent = false;

                }



                scene.ShowcaseSceneSilent = silent;

                scene.SceneVoiceover = silent ? string.Empty : (sceneDto.voiceover ?? string.Empty).Trim();

                if (sceneDto.clip_duration_seconds > 0)
                {
                    scene.ShowcaseClipDurationSeconds = ShowcaseSceneDurationHelper.Clamp(sceneDto.clip_duration_seconds);
                }
                else
                {
                    scene.ShowcaseClipDurationSeconds = ShowcaseSceneDurationHelper.EstimateFromVoiceover(
                        scene.SceneVoiceover,
                        scene.ShowcaseSceneSilent);
                }

            }

            for (var i = 0; i < allScenes.Count; i++)
            {
                var scene = allScenes[i];
                if (scene == null || scene.ShowcaseClipDurationSeconds > 0)
                {
                    continue;
                }

                scene.ShowcaseClipDurationSeconds = ShowcaseSceneDurationHelper.EstimateFromVoiceover(
                    scene.SceneVoiceover,
                    scene.ShowcaseSceneSilent);
            }



            ShowcaseVoiceoverHelper.EnforceMaxSilentScenes(allScenes);



            return new ShowcaseVoiceoverResult

            {

                Theme = (dto.theme ?? string.Empty).Trim(),

                HookText = (dto.hook_text ?? string.Empty).Trim(),

                CtaText = (dto.cta_text ?? string.Empty).Trim(),

                Scenes = allScenes.ToList()

            };

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


