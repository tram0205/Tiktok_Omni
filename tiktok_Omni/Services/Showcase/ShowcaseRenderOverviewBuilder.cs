using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseRenderOverviewBuilder
    {
        public static ShowcaseRenderOverviewSnapshot Build(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            ShowcaseSessionState session,
            AppSettings settings,
            IList<string> renderBlockers)
        {
            scenes = scenes ?? new List<AiVideoGenInputItem>();
            var snapshot = new ShowcaseRenderOverviewSnapshot
            {
                ProductTitle = (video?.ProductName ?? scenes.FirstOrDefault()?.ProductName ?? "Showcase").Trim(),
                RenderReady = renderBlockers == null || renderBlockers.Count == 0,
                RenderBlockers = renderBlockers ?? new List<string>()
            };

            if (video == null)
            {
                snapshot.HeroSummary = "Chưa chọn dòng video trên lưới.";
                snapshot.Pipeline.Add(Step("Storyboard", ShowcaseOverviewStepStatus.Missing, "Chọn dòng video"));
                return snapshot;
            }

            var renderSettings = ShowcasePerVideoRenderSettings.FromVideo(video, settings ?? new AppSettings());
            var aspect = ShowcaseOutputAspectPresets.Resolve(video.ShowcaseOutputAspectId, settings?.ShowcaseOutputAspectDefault);
            snapshot.Theme = TrimOrMissing(video.ShowcaseTheme);
            snapshot.AspectLabel = aspect.DisplayLabel + " (" + aspect.Width + "×" + aspect.Height + ")";
            snapshot.ClipsDir = session?.ClipsDir ?? string.Empty;
            snapshot.ScriptPreviewText = ShowcaseContentDisplayHelper.FormatScriptHubPreview(video);

            var withClip = scenes.Count(s => ClipExists(s?.ClipPath));
            var promptFilled = scenes.Count(ShowcaseClipToolHelper.SceneHasClipPrompt);
            var voiceAligned = ShowcaseVoiceoverHelper.HasClipAlignedVoiceover(video, scenes);
            var voiceSummary = ShowcaseContentDisplayHelper.FormatScriptLabel(video);
            var narrPath = ResolveNarrationPath(session);
            var hasNarr = !string.IsNullOrWhiteSpace(narrPath) && File.Exists(narrPath);
            var hook = (video.ShowcaseHookText ?? session?.HookText ?? string.Empty).Trim();
            var cta = (video.ShowcaseCtaText ?? session?.CtaText ?? string.Empty).Trim();

            BuildSceneCards(snapshot, scenes, video);
            BuildPipeline(snapshot, video, scenes, withClip, promptFilled, voiceAligned, hasNarr);
            BuildHero(snapshot, scenes.Count, withClip, voiceAligned, hasNarr);
            BuildAudioSubtitlePanel(snapshot, video, session, renderSettings, voiceAligned, voiceSummary, hasNarr, narrPath, hook, cta);

            // Legacy lines (debug/export) — partial clip = thiếu
            Add(snapshot, "Video", "Sản phẩm", snapshot.ProductTitle, string.IsNullOrWhiteSpace(snapshot.ProductTitle));
            Add(snapshot, "Video", "Chủ đề", snapshot.Theme, string.IsNullOrWhiteSpace(video.ShowcaseTheme));
            Add(snapshot, "Video", "Khung hình", snapshot.AspectLabel, false);
            if (scenes.Count == 0)
            {
                Add(snapshot, "Cảnh & clip", "Storyboard", "Thiếu — thêm ảnh", true);
            }
            else
            {
                var missingOrders = scenes
                    .Select((s, i) => new { Order = i + 1, Ok = ClipExists(s?.ClipPath) })
                    .Where(x => !x.Ok)
                    .Select(x => x.Order)
                    .ToList();
                var clipDetail = withClip + "/" + scenes.Count + " cảnh có clip";
                if (missingOrders.Count > 0)
                {
                    clipDetail += " — thiếu " + string.Join(", ", missingOrders);
                }

                Add(snapshot, "Cảnh & clip", "Clip", clipDetail, withClip < scenes.Count);
                Add(snapshot, "Cảnh & clip", "Prompt", promptFilled + "/" + scenes.Count, promptFilled == 0);
            }

            Add(snapshot, "Thoại", "Khớp clip", voiceAligned ? "OK" : "Thiếu — «Tạo lời thoại»", !voiceAligned);
            Add(snapshot, "Âm thanh", "narration.mp3", hasNarr ? "Có" : "Thiếu", !hasNarr);
            Add(snapshot, "Render", "Sẵn sàng", snapshot.RenderReady ? "OK" : "Chưa đủ", !snapshot.RenderReady);

            return snapshot;
        }

        private static void BuildSceneCards(
            ShowcaseRenderOverviewSnapshot snapshot,
            IList<AiVideoGenInputItem> scenes,
            ShowcaseVideoItem video)
        {
            var firstVoiced = ShowcaseSubtitleDisplayHelper.FindFirstVoicedSceneIndex(scenes);
            var sceneCount = scenes.Count;

            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var voice = (scene?.SceneVoiceover ?? string.Empty).Trim();
                if (scene != null && scene.ShowcaseSceneSilent)
                {
                    voice = "(im lặng)";
                }
                else if (voice.Length == 0)
                {
                    voice = "(chưa thoại)";
                }

                var voicePreview = voice;
                if (voicePreview.Length > 48)
                {
                    voicePreview = voicePreview.Substring(0, 45) + "…";
                }

                snapshot.SceneCards.Add(new ShowcaseOverviewSceneCard
                {
                    SceneIndex = i + 1,
                    ThumbnailPath = (scene?.ThumbnailPath ?? string.Empty).Trim(),
                    HasClip = ClipExists(scene?.ClipPath),
                    HasPrompt = ShowcaseClipToolHelper.SceneHasClipPrompt(scene),
                    IsSilent = scene?.ShowcaseSceneSilent ?? false,
                    VoiceText = voice,
                    VoicePreview = voicePreview,
                    SubtitleDisplayText = ResolveOverviewSubtitleDisplayText(video, scene, i, sceneCount, firstVoiced),
                    ClipToolLabel = ShowcaseClipToolHelper.GetToolDisplayLabel(scene?.ShowcaseClipTool)
                });
            }
        }

        private static string ResolveOverviewSubtitleDisplayText(
            ShowcaseVideoItem video,
            AiVideoGenInputItem scene,
            int sceneIndexZeroBased,
            int sceneCount,
            int firstVoicedIndex)
        {
            if (video == null)
            {
                return "—";
            }

            var bodyBurnIn = video.ShowcaseSubtitleEnabled;
            var hookBurnIn = video.ShowcaseHookSubtitleEnabled;
            if (!bodyBurnIn && !hookBurnIn)
            {
                return "Tắt burn-in";
            }

            if (scene == null || scene.ShowcaseSceneSilent)
            {
                return "— (im lặng)";
            }

            var lines = new List<string>();
            if (hookBurnIn && sceneIndexZeroBased == firstVoicedIndex)
            {
                var hook = ShowcaseSubtitleDisplayHelper.ResolveDisplayHook(video);
                if (!string.IsNullOrWhiteSpace(hook))
                {
                    lines.Add(hook.Trim());
                }
            }

            if (bodyBurnIn && sceneIndexZeroBased > firstVoicedIndex
                && !string.IsNullOrWhiteSpace(scene.SceneVoiceover))
            {
                var body = ShowcaseSubtitleDisplayHelper.ResolveDisplaySceneVoiceover(scene);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    lines.Add(body.Trim());
                }
            }

            if (bodyBurnIn && sceneCount > 0 && sceneIndexZeroBased == sceneCount - 1)
            {
                var cta = ShowcaseSubtitleDisplayHelper.ResolveDisplayCta(video);
                var sceneVoice = (scene.SceneVoiceover ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cta)
                    && sceneVoice.IndexOf(cta, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    lines.Add(cta.Trim());
                }
            }

            if (lines.Count == 0)
            {
                if (bodyBurnIn && sceneIndexZeroBased == firstVoicedIndex)
                {
                    var fallback = ShowcaseSubtitleDisplayHelper.ResolveDisplaySceneVoiceover(scene);
                    if (!string.IsNullOrWhiteSpace(fallback))
                    {
                        return fallback.Trim();
                    }
                }

                return "—";
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void BuildPipeline(
            ShowcaseRenderOverviewSnapshot snapshot,
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            int withClip,
            int promptFilled,
            bool voiceAligned,
            bool hasNarr)
        {
            var n = scenes.Count;
            if (n == 0)
            {
                snapshot.Pipeline.Add(Step("Storyboard", ShowcaseOverviewStepStatus.Missing, "Thêm ảnh cột «Ảnh»"));
                snapshot.Pipeline.Add(Step("Kịch bản", ShowcaseOverviewStepStatus.Missing, "«Tạo kịch bản»"));
                snapshot.Pipeline.Add(Step("Clip", ShowcaseOverviewStepStatus.Missing, "0 clip"));
                snapshot.Pipeline.Add(Step("Thoại", ShowcaseOverviewStepStatus.Missing, "«Tạo lời thoại»"));
                snapshot.Pipeline.Add(Step("Audio", ShowcaseOverviewStepStatus.Missing, "«Tạo audio»"));
                snapshot.Pipeline.Add(Step("Render", ShowcaseOverviewStepStatus.Missing, "Chưa đủ"));
                return;
            }

            snapshot.Pipeline.Add(Step("Storyboard", ShowcaseOverviewStepStatus.Complete, n + " cảnh"));

            if (promptFilled == 0)
            {
                snapshot.Pipeline.Add(Step("Kịch bản", ShowcaseOverviewStepStatus.Missing, "Chưa prompt clip"));
            }
            else if (promptFilled < n)
            {
                snapshot.Pipeline.Add(Step("Kịch bản", ShowcaseOverviewStepStatus.Partial, promptFilled + "/" + n + " prompt"));
            }
            else
            {
                snapshot.Pipeline.Add(Step("Kịch bản", ShowcaseOverviewStepStatus.Complete, n + "/" + n + " prompt"));
            }

            if (withClip == 0)
            {
                snapshot.Pipeline.Add(Step("Clip", ShowcaseOverviewStepStatus.Missing, "0/" + n));
            }
            else if (withClip < n)
            {
                snapshot.Pipeline.Add(Step("Clip", ShowcaseOverviewStepStatus.Partial, withClip + "/" + n + " clip"));
            }
            else
            {
                snapshot.Pipeline.Add(Step("Clip", ShowcaseOverviewStepStatus.Complete, n + "/" + n + " clip"));
            }

            if (!voiceAligned)
            {
                var partial = ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes);
                snapshot.Pipeline.Add(Step("Thoại", ShowcaseOverviewStepStatus.Missing,
                    partial ? "Thoại nháp — cần «Tạo lời thoại»" : "«Tạo lời thoại»"));
            }
            else
            {
                snapshot.Pipeline.Add(Step("Thoại", ShowcaseOverviewStepStatus.Complete, "Khớp clip"));
            }

            snapshot.Pipeline.Add(hasNarr
                ? Step("Audio", ShowcaseOverviewStepStatus.Complete, "narration.mp3")
                : Step("Audio", ShowcaseOverviewStepStatus.Missing, "Tab Âm thanh → Tạo audio"));

            snapshot.Pipeline.Add(snapshot.RenderReady
                ? Step("Render", ShowcaseOverviewStepStatus.Complete, "Sẵn sàng")
                : Step("Render", ShowcaseOverviewStepStatus.Missing, snapshot.RenderBlockers.Count + " việc còn thiếu"));
        }

        private static void BuildHero(
            ShowcaseRenderOverviewSnapshot snapshot,
            int sceneCount,
            int withClip,
            bool voiceAligned,
            bool hasNarr)
        {
            if (sceneCount == 0)
            {
                snapshot.HeroSummary = "Chưa có cảnh trên storyboard.";
                return;
            }

            var parts = new List<string>
            {
                sceneCount + " cảnh",
                withClip + "/" + sceneCount + " clip",
                voiceAligned ? "thoại khớp clip" : "thoại chưa khớp",
                hasNarr ? "có audio thoại" : "chưa audio thoại",
                snapshot.RenderReady ? "→ có thể Render" : "→ chưa Render được"
            };
            snapshot.HeroSummary = string.Join(" · ", parts);
        }

        private static void BuildAudioSubtitlePanel(
            ShowcaseRenderOverviewSnapshot snapshot,
            ShowcaseVideoItem video,
            ShowcaseSessionState session,
            ShowcasePerVideoRenderSettings renderSettings,
            bool voiceAligned,
            string voiceSummary,
            bool hasNarr,
            string narrPath,
            string hook,
            string cta)
        {
            var panel = snapshot.AudioSubtitle;
            panel.HookText = hook;
            panel.CtaText = cta;
            panel.VoiceAligned = voiceAligned;
            panel.VoiceSummary = voiceSummary;

            panel.SubtitleBurnIn = video.ShowcaseSubtitleEnabled;
            var look = string.IsNullOrWhiteSpace(video.ShowcaseSubtitleLookPreset)
                ? (video.ShowcaseSubtitleStyleLabel ?? string.Empty).Trim()
                : video.ShowcaseSubtitleLookPreset.Trim();
            panel.SubtitleLook = string.IsNullOrWhiteSpace(look)
                ? "Mặc định · " + video.ShowcaseSubtitleFontSize + "px"
                : look + " · " + video.ShowcaseSubtitleFontSize + "px";
            panel.SubtitleHookOnScreen = string.IsNullOrWhiteSpace(video.ShowcaseSubtitleDisplayHook) ? "—" : video.ShowcaseSubtitleDisplayHook.Trim();
            panel.SubtitleCtaOnScreen = string.IsNullOrWhiteSpace(video.ShowcaseSubtitleDisplayCta) ? "—" : video.ShowcaseSubtitleDisplayCta.Trim();

            var musicFile = (renderSettings.BackgroundMusicFile ?? video.ShowcaseBackgroundMusicFile ?? string.Empty).Trim();
            panel.MusicLabel = VideoReupRowItem.IsNoMusicSelection(musicFile) || string.IsNullOrWhiteSpace(musicFile)
                ? "Không nhạc nền"
                : Path.GetFileName(musicFile);
            panel.MusicVolume = renderSettings.MusicVolume.ToString("0.#", CultureInfo.InvariantCulture) + "%";

            panel.HasNarration = hasNarr;
            panel.NarrationLabel = hasNarr
                ? Path.GetFileName(narrPath)
                : "Thiếu — mở tab Âm thanh → «Tạo audio»";

            panel.SpeedLabel = "Hook " + PercentLabel(video.ShowcaseHookNarrationSpeedPercent)
                               + " · Thân " + PercentLabel(video.ShowcaseBodyNarrationSpeedPercent);

            var hookSfx = renderSettings.HookSfxEnabled && !string.IsNullOrWhiteSpace(renderSettings.HookSfxFile);
            var ctaSfx = renderSettings.CtaSfxEnabled && !string.IsNullOrWhiteSpace(renderSettings.CtaSfxFile);
            panel.SfxLabel = (hookSfx ? "Hook SFX" : "—") + " · " + (ctaSfx ? "CTA SFX" : "—");
            panel.TransitionLabel = renderSettings.TransitionSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }

        public static string FormatPlainText(ShowcaseRenderOverviewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine(snapshot.HeroSummary);
            sb.AppendLine();
            foreach (var step in snapshot.Pipeline ?? new List<ShowcaseOverviewPipelineStep>())
            {
                sb.AppendLine("[" + step.Status + "] " + step.Title + ": " + step.Detail);
            }

            if (snapshot.RenderBlockers != null && snapshot.RenderBlockers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Cần trước Render:");
                foreach (var b in snapshot.RenderBlockers)
                {
                    sb.AppendLine("• " + b);
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static ShowcaseOverviewPipelineStep Step(string title, ShowcaseOverviewStepStatus status, string detail)
        {
            return new ShowcaseOverviewPipelineStep
            {
                Title = title,
                Status = status,
                Detail = detail
            };
        }

        private static void Add(ShowcaseRenderOverviewSnapshot snapshot, string section, string label, string detail, bool missing)
        {
            snapshot.Lines.Add(new ShowcaseRenderOverviewLine
            {
                Section = section,
                Label = label,
                Detail = string.IsNullOrWhiteSpace(detail) ? "—" : detail.Trim(),
                Missing = missing
            });
        }

        private static bool ClipExists(string path)
        {
            var p = (path ?? string.Empty).Trim();
            return !string.IsNullOrEmpty(p) && File.Exists(p);
        }

        private static string ResolveNarrationPath(ShowcaseSessionState session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.BaseDir))
            {
                return string.Empty;
            }

            return Path.Combine(
                ShowcaseNarrationCacheHelper.GetAudioDirectory(session.BaseDir),
                ShowcaseNarrationCacheHelper.NarrationFileName);
        }

        private static string TrimOrMissing(string value)
        {
            var t = (value ?? string.Empty).Trim();
            return string.IsNullOrEmpty(t) ? "—" : t;
        }

        private static string PercentLabel(int percent)
        {
            return percent == 0 ? "100%" : percent + "%";
        }
    }
}
