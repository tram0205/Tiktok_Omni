using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public sealed class ShowcaseDraftDocument
    {
        public Guid? ActiveVideoId { get; set; }

        public List<ShowcaseVideoDraftEntry> Videos { get; set; } = new List<ShowcaseVideoDraftEntry>();

        public ShowcaseSessionDraftEntry Session { get; set; }
    }

    public sealed class ShowcaseVideoDraftEntry
    {
        public Guid VideoId { get; set; }

        public string ProfileName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string ShowcaseTheme { get; set; } = string.Empty;

        public string ShowcaseThemePrompt { get; set; } = string.Empty;

        public string ShowcaseUserTheme { get; set; } = string.Empty;

        public string ShowcaseProductTypePrompt { get; set; } = string.Empty;

        public string ShowcaseClipModeId { get; set; } = ShowcaseClipModePresets.DefaultId;

        public string ShowcaseOutputAspectId { get; set; } = ShowcaseOutputAspectPresets.DefaultId;

        public string ShowcaseHookText { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayHook { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayHookAnimation { get; set; } = string.Empty;

        public string ShowcaseCtaText { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayCta { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayCtaAnimation { get; set; } = string.Empty;

        public long ShowcaseVoiceoverClipFingerprint { get; set; }

        public bool ShowcaseMultiVoice { get; set; }

        public int ShowcaseTextSize { get; set; } = 72;

        public string ShowcaseSubtitleFontName { get; set; } = string.Empty;

        public int ShowcaseSubtitleFontSize { get; set; } = 72;

        public string ShowcaseSubtitlePosition { get; set; } = string.Empty;

        public string ShowcaseSubtitleAnimation { get; set; } = string.Empty;

        public bool ShowcaseSubtitleBold { get; set; } = true;

        public bool ShowcaseSubtitleItalic { get; set; }

        public int ShowcaseSubtitleWordsPerLine { get; set; } = 6;

        public bool ShowcaseSubtitleEnabled { get; set; }

        public bool ShowcaseHookSubtitleEnabled { get; set; }

        public string ShowcaseHookSubtitleAnimation { get; set; } = "PopStrong";

        public string ShowcaseHookSubtitleFontName { get; set; } = string.Empty;

        public int ShowcaseHookSubtitleFontSize { get; set; }

        public string ShowcaseBackgroundMusicFile { get; set; } = string.Empty;

        public int ShowcaseMusicVolume { get; set; } = 14;

        public int ShowcaseNarrationSpeedPercent { get; set; }

        public string ShowcaseTtsEngine { get; set; } = string.Empty;

        public string ShowcaseVoicePresetId { get; set; } = string.Empty;

        public string ShowcaseVoiceAgeId { get; set; } = string.Empty;

        public string ShowcaseVoiceLanguageId { get; set; } = string.Empty;

        public bool ShowcaseSfxMasterEnabled { get; set; } = true;

        public string ShowcaseHookSfxFile { get; set; } = string.Empty;

        public bool ShowcaseHookSfxEnabled { get; set; }

        public double ShowcaseHookSfxOffsetSeconds { get; set; }

        public int ShowcaseHookSfxVolumePercent { get; set; }

        public string ShowcaseHookSfxGeminiHint { get; set; } = string.Empty;

        public string ShowcaseCtaSfxFile { get; set; } = string.Empty;

        public bool ShowcaseCtaSfxEnabled { get; set; }

        public double ShowcaseCtaSfxOffsetSeconds { get; set; }

        public int ShowcaseCtaSfxVolumePercent { get; set; }

        public string ShowcaseCtaSfxGeminiHint { get; set; } = string.Empty;

        public double ShowcaseTransitionSeconds { get; set; } = 0.6;

        public string PipelineStatus { get; set; } = "Chờ";

        public string OutputVideoPath { get; set; } = string.Empty;

        public string ShowcaseSessionBaseDir { get; set; } = string.Empty;

        public string ShowcaseClipsDir { get; set; } = string.Empty;

        public List<AiVideoGenInputItem> Scenes { get; set; } = new List<AiVideoGenInputItem>();
    }

    public sealed class ShowcaseSessionDraftEntry
    {
        public string BaseDir { get; set; } = string.Empty;

        public string SourceImagesDir { get; set; } = string.Empty;

        public string ClipsDir { get; set; } = string.Empty;

        public string OutputDir { get; set; } = string.Empty;

        public string ExcelPath { get; set; } = string.Empty;

        public string ProfileName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string Theme { get; set; } = string.Empty;

        public string HookText { get; set; } = string.Empty;

        public string CtaText { get; set; } = string.Empty;
    }

    public sealed class ShowcaseDraftStore
    {
        private const string FileName = "draft_showcase.json";

        public ShowcaseDraftDocument Load()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new ShowcaseDraftDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<ShowcaseDraftDocument>(json);
                if (doc == null)
                {
                    return new ShowcaseDraftDocument();
                }

                doc.Videos = doc.Videos?
                    .Where(v => v != null)
                    .Select(NormalizeVideo)
                    .ToList() ?? new List<ShowcaseVideoDraftEntry>();

                if (migrateFromLegacy && doc.Videos.Count > 0)
                {
                    Save(doc);
                }

                return doc;
            }
            catch
            {
                return new ShowcaseDraftDocument();
            }
        }

        public void Save(ShowcaseDraftDocument document)
        {
            var doc = document ?? new ShowcaseDraftDocument();
            doc.Videos = (doc.Videos ?? new List<ShowcaseVideoDraftEntry>())
                .Where(v => v != null)
                .Select(NormalizeVideo)
                .ToList();

            try
            {
                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(doc, Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }

        public static ShowcaseVideoDraftEntry FromVideo(ShowcaseVideoItem video, Func<AiVideoGenInputItem, AiVideoGenInputItem> cloneScene)
        {
            if (video == null)
            {
                return new ShowcaseVideoDraftEntry();
            }

            return new ShowcaseVideoDraftEntry
            {
                VideoId = video.VideoId,
                ProfileName = video.ProfileName ?? string.Empty,
                ProductName = video.ProductName ?? string.Empty,
                ShowcaseTheme = video.ShowcaseTheme ?? string.Empty,
                ShowcaseThemePrompt = video.ShowcaseThemePrompt ?? string.Empty,
                ShowcaseUserTheme = video.ShowcaseUserTheme ?? string.Empty,
                ShowcaseProductTypePrompt = video.ShowcaseProductTypePrompt ?? string.Empty,
                ShowcaseClipModeId = ShowcaseClipModePresets.ResolveIdForGemini(video.ShowcaseClipModeId),
                ShowcaseOutputAspectId = ShowcaseOutputAspectPresets.ResolveId(
                    video.ShowcaseOutputAspectId,
                    null),
                ShowcaseHookText = video.ShowcaseHookText ?? string.Empty,
                ShowcaseSubtitleDisplayHook = video.ShowcaseSubtitleDisplayHook ?? string.Empty,
                ShowcaseSubtitleDisplayHookAnimation = video.ShowcaseSubtitleDisplayHookAnimation ?? string.Empty,
                ShowcaseCtaText = video.ShowcaseCtaText ?? string.Empty,
                ShowcaseSubtitleDisplayCta = video.ShowcaseSubtitleDisplayCta ?? string.Empty,
                ShowcaseSubtitleDisplayCtaAnimation = video.ShowcaseSubtitleDisplayCtaAnimation ?? string.Empty,
                ShowcaseVoiceoverClipFingerprint = video.ShowcaseVoiceoverClipFingerprint,
                ShowcaseMultiVoice = video.ShowcaseMultiVoice,
                ShowcaseTextSize = video.ShowcaseTextSize,
                ShowcaseSubtitleFontName = video.ShowcaseSubtitleFontName ?? string.Empty,
                ShowcaseSubtitleFontSize = video.ShowcaseSubtitleFontSize,
                ShowcaseSubtitlePosition = video.ShowcaseSubtitlePosition ?? string.Empty,
                ShowcaseSubtitleAnimation = video.ShowcaseSubtitleAnimation ?? string.Empty,
                ShowcaseSubtitleBold = video.ShowcaseSubtitleBold,
                ShowcaseSubtitleItalic = video.ShowcaseSubtitleItalic,
                ShowcaseSubtitleWordsPerLine = video.ShowcaseSubtitleWordsPerLine,
                ShowcaseSubtitleEnabled = video.ShowcaseSubtitleEnabled,
                ShowcaseHookSubtitleEnabled = video.ShowcaseHookSubtitleEnabled,
                ShowcaseHookSubtitleAnimation = video.ShowcaseHookSubtitleAnimation ?? "PopStrong",
                ShowcaseHookSubtitleFontName = video.ShowcaseHookSubtitleFontName ?? string.Empty,
                ShowcaseHookSubtitleFontSize = video.ShowcaseHookSubtitleFontSize,
                ShowcaseBackgroundMusicFile = video.ShowcaseBackgroundMusicFile ?? string.Empty,
                ShowcaseMusicVolume = video.ShowcaseMusicVolume,
                ShowcaseNarrationSpeedPercent = video.ShowcaseNarrationSpeedPercent,
                ShowcaseTtsEngine = video.ShowcaseTtsEngine ?? string.Empty,
                ShowcaseVoicePresetId = video.ShowcaseVoicePresetId ?? string.Empty,
                ShowcaseVoiceAgeId = video.ShowcaseVoiceAgeId ?? string.Empty,
                ShowcaseVoiceLanguageId = video.ShowcaseVoiceLanguageId ?? string.Empty,
                ShowcaseSfxMasterEnabled = video.ShowcaseSfxMasterEnabled,
                ShowcaseHookSfxFile = video.ShowcaseHookSfxFile ?? string.Empty,
                ShowcaseHookSfxEnabled = video.ShowcaseHookSfxEnabled,
                ShowcaseHookSfxOffsetSeconds = video.ShowcaseHookSfxOffsetSeconds,
                ShowcaseHookSfxVolumePercent = video.ShowcaseHookSfxVolumePercent,
                ShowcaseHookSfxGeminiHint = video.ShowcaseHookSfxGeminiHint ?? string.Empty,
                ShowcaseCtaSfxFile = video.ShowcaseCtaSfxFile ?? string.Empty,
                ShowcaseCtaSfxEnabled = video.ShowcaseCtaSfxEnabled,
                ShowcaseCtaSfxOffsetSeconds = video.ShowcaseCtaSfxOffsetSeconds,
                ShowcaseCtaSfxVolumePercent = video.ShowcaseCtaSfxVolumePercent,
                ShowcaseCtaSfxGeminiHint = video.ShowcaseCtaSfxGeminiHint ?? string.Empty,
                ShowcaseTransitionSeconds = video.ShowcaseTransitionSeconds,
                PipelineStatus = video.PipelineStatus ?? "Chờ",
                OutputVideoPath = video.OutputVideoPath ?? string.Empty,
                ShowcaseSessionBaseDir = video.ShowcaseSessionBaseDir ?? string.Empty,
                ShowcaseClipsDir = video.ShowcaseClipsDir ?? string.Empty,
                Scenes = (video.Scenes ?? new List<AiVideoGenInputItem>())
                    .Where(s => s != null)
                    .Select(s => cloneScene?.Invoke(s) ?? s)
                    .Where(s => s != null)
                    .ToList()
            };
        }

        public static ShowcaseVideoItem ToVideo(ShowcaseVideoDraftEntry entry, Func<AiVideoGenInputItem, AiVideoGenInputItem> cloneScene)
        {
            if (entry == null)
            {
                return new ShowcaseVideoItem();
            }

            var video = new ShowcaseVideoItem
            {
                VideoId = entry.VideoId != Guid.Empty ? entry.VideoId : Guid.NewGuid(),
                ProfileName = entry.ProfileName ?? string.Empty,
                ProductName = entry.ProductName ?? string.Empty,
                ShowcaseTheme = entry.ShowcaseTheme ?? string.Empty,
                ShowcaseThemePrompt = entry.ShowcaseThemePrompt ?? string.Empty,
                ShowcaseUserTheme = entry.ShowcaseUserTheme ?? string.Empty,
                ShowcaseProductTypePrompt = entry.ShowcaseProductTypePrompt ?? string.Empty,
                ShowcaseClipModeId = ShowcaseClipModePresets.ResolveIdForGemini(entry.ShowcaseClipModeId),
                ShowcaseOutputAspectId = ShowcaseOutputAspectPresets.ResolveId(entry.ShowcaseOutputAspectId, null),
                ShowcaseHookText = entry.ShowcaseHookText ?? string.Empty,
                ShowcaseSubtitleDisplayHook = entry.ShowcaseSubtitleDisplayHook ?? string.Empty,
                ShowcaseSubtitleDisplayHookAnimation = entry.ShowcaseSubtitleDisplayHookAnimation ?? string.Empty,
                ShowcaseCtaText = entry.ShowcaseCtaText ?? string.Empty,
                ShowcaseSubtitleDisplayCta = entry.ShowcaseSubtitleDisplayCta ?? string.Empty,
                ShowcaseSubtitleDisplayCtaAnimation = entry.ShowcaseSubtitleDisplayCtaAnimation ?? string.Empty,
                ShowcaseVoiceoverClipFingerprint = entry.ShowcaseVoiceoverClipFingerprint,
                ShowcaseMultiVoice = entry.ShowcaseMultiVoice,
                ShowcaseTextSize = entry.ShowcaseTextSize > 0 ? entry.ShowcaseTextSize : 72,
                ShowcaseSubtitleFontName = entry.ShowcaseSubtitleFontName ?? string.Empty,
                ShowcaseSubtitleFontSize = entry.ShowcaseSubtitleFontSize > 0 ? entry.ShowcaseSubtitleFontSize : 72,
                ShowcaseSubtitlePosition = entry.ShowcaseSubtitlePosition ?? string.Empty,
                ShowcaseSubtitleAnimation = entry.ShowcaseSubtitleAnimation ?? string.Empty,
                ShowcaseSubtitleBold = entry.ShowcaseSubtitleBold,
                ShowcaseSubtitleItalic = entry.ShowcaseSubtitleItalic,
                ShowcaseSubtitleWordsPerLine = entry.ShowcaseSubtitleWordsPerLine > 0 ? entry.ShowcaseSubtitleWordsPerLine : 6,
                ShowcaseSubtitleEnabled = entry.ShowcaseSubtitleEnabled,
                ShowcaseHookSubtitleEnabled = entry.ShowcaseHookSubtitleEnabled,
                ShowcaseHookSubtitleAnimation = string.IsNullOrWhiteSpace(entry.ShowcaseHookSubtitleAnimation)
                    ? "PopStrong"
                    : entry.ShowcaseHookSubtitleAnimation.Trim(),
                ShowcaseHookSubtitleFontName = entry.ShowcaseHookSubtitleFontName ?? string.Empty,
                ShowcaseHookSubtitleFontSize = entry.ShowcaseHookSubtitleFontSize,
                ShowcaseBackgroundMusicFile = entry.ShowcaseBackgroundMusicFile ?? string.Empty,
                ShowcaseMusicVolume = entry.ShowcaseMusicVolume >= 0 ? entry.ShowcaseMusicVolume : 14,
                ShowcaseNarrationSpeedPercent = entry.ShowcaseNarrationSpeedPercent,
                ShowcaseTtsEngine = entry.ShowcaseTtsEngine ?? string.Empty,
                ShowcaseVoicePresetId = entry.ShowcaseVoicePresetId ?? string.Empty,
                ShowcaseVoiceAgeId = entry.ShowcaseVoiceAgeId ?? string.Empty,
                ShowcaseVoiceLanguageId = entry.ShowcaseVoiceLanguageId ?? string.Empty,
                ShowcaseSfxMasterEnabled = entry.ShowcaseSfxMasterEnabled,
                ShowcaseHookSfxFile = entry.ShowcaseHookSfxFile ?? string.Empty,
                ShowcaseHookSfxEnabled = entry.ShowcaseHookSfxEnabled,
                ShowcaseHookSfxOffsetSeconds = entry.ShowcaseHookSfxOffsetSeconds,
                ShowcaseHookSfxVolumePercent = entry.ShowcaseHookSfxVolumePercent,
                ShowcaseHookSfxGeminiHint = entry.ShowcaseHookSfxGeminiHint ?? string.Empty,
                ShowcaseCtaSfxFile = entry.ShowcaseCtaSfxFile ?? string.Empty,
                ShowcaseCtaSfxEnabled = entry.ShowcaseCtaSfxEnabled,
                ShowcaseCtaSfxOffsetSeconds = entry.ShowcaseCtaSfxOffsetSeconds,
                ShowcaseCtaSfxVolumePercent = entry.ShowcaseCtaSfxVolumePercent,
                ShowcaseCtaSfxGeminiHint = entry.ShowcaseCtaSfxGeminiHint ?? string.Empty,
                ShowcaseTransitionSeconds = entry.ShowcaseTransitionSeconds > 0 ? entry.ShowcaseTransitionSeconds : 0.6,
                PipelineStatus = entry.PipelineStatus ?? "Chờ",
                OutputVideoPath = entry.OutputVideoPath ?? string.Empty,
                ShowcaseSessionBaseDir = entry.ShowcaseSessionBaseDir ?? string.Empty,
                ShowcaseClipsDir = entry.ShowcaseClipsDir ?? string.Empty
            };

            foreach (var scene in entry.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                if (scene == null)
                {
                    continue;
                }

                var clone = cloneScene?.Invoke(scene) ?? scene;
                if (clone != null)
                {
                    video.Scenes.Add(clone);
                }
            }

            video.ApplySettingsToScenes();
            video.RefreshDisplayFields();
            return video;
        }

        public static ShowcaseSessionDraftEntry FromSession(ShowcaseSessionState session)
        {
            if (session == null)
            {
                return null;
            }

            return new ShowcaseSessionDraftEntry
            {
                BaseDir = session.BaseDir ?? string.Empty,
                SourceImagesDir = session.SourceImagesDir ?? string.Empty,
                ClipsDir = session.ClipsDir ?? string.Empty,
                OutputDir = session.OutputDir ?? string.Empty,
                ExcelPath = session.ExcelPath ?? string.Empty,
                ProfileName = session.ProfileName ?? string.Empty,
                ProductName = session.ProductName ?? string.Empty,
                Theme = session.Theme ?? string.Empty,
                HookText = session.HookText ?? string.Empty,
                CtaText = session.CtaText ?? string.Empty
            };
        }

        public static ShowcaseSessionState ToSession(ShowcaseSessionDraftEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return new ShowcaseSessionState
            {
                BaseDir = entry.BaseDir ?? string.Empty,
                SourceImagesDir = entry.SourceImagesDir ?? string.Empty,
                ClipsDir = entry.ClipsDir ?? string.Empty,
                OutputDir = entry.OutputDir ?? string.Empty,
                ExcelPath = entry.ExcelPath ?? string.Empty,
                ProfileName = entry.ProfileName ?? string.Empty,
                ProductName = entry.ProductName ?? string.Empty,
                Theme = entry.Theme ?? string.Empty,
                HookText = entry.HookText ?? string.Empty,
                CtaText = entry.CtaText ?? string.Empty
            };
        }

        private static ShowcaseVideoDraftEntry NormalizeVideo(ShowcaseVideoDraftEntry entry)
        {
            entry.ProfileName = ProfileScopedPaths.ResolveProfileName(entry.ProfileName);
            entry.ProductName = (entry.ProductName ?? string.Empty).Trim();
            entry.Scenes = entry.Scenes?
                .Where(s => s != null)
                .Select(NormalizeScene)
                .ToList() ?? new List<AiVideoGenInputItem>();
            return entry;
        }

        private static AiVideoGenInputItem NormalizeScene(AiVideoGenInputItem scene)
        {
            scene.ProfileName = ProfileScopedPaths.ResolveProfileName(scene.ProfileName);
            scene.ProductName = (scene.ProductName ?? string.Empty).Trim();
            scene.ImageUrl = (scene.ImageUrl ?? string.Empty).Trim();
            scene.ClipPath = (scene.ClipPath ?? string.Empty).Trim();
            scene.SceneVoiceover = (scene.SceneVoiceover ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayVoiceover = (scene.ShowcaseSubtitleDisplayVoiceover ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayAnimation = (scene.ShowcaseSubtitleDisplayAnimation ?? string.Empty).Trim();
            scene.VeoPrompt = (scene.VeoPrompt ?? string.Empty).Trim();
            scene.KlingPrompt = (scene.KlingPrompt ?? string.Empty).Trim();
            scene.ZoomHint = (scene.ZoomHint ?? string.Empty).Trim();
            scene.ShowcaseZoomStyleId = ShowcaseZoomStyleCatalog.NormalizeId(scene.ShowcaseZoomStyleId);
            if (string.IsNullOrEmpty(scene.ShowcaseZoomStyleId)
                && !string.IsNullOrEmpty(scene.ZoomHint)
                && string.Equals(scene.ShowcaseClipTool, ShowcaseClipToolHelper.ToolZoom, StringComparison.Ordinal))
            {
                scene.ShowcaseZoomStyleId = ShowcaseZoomStyleCatalog.InferFromZoomHint(scene.ZoomHint);
            }
            if (scene.ShowcaseClipDurationSeconds > 0)
            {
                scene.ShowcaseClipDurationSeconds = ShowcaseSceneDurationHelper.Clamp(scene.ShowcaseClipDurationSeconds);
            }

            scene.ShowcaseImageKind = ShowcaseClipToolHelper.NormalizeImageKind(scene.ShowcaseImageKind, scene.VeoPrompt);
            scene.ShowcaseClipTool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
            scene.SceneTitle = (scene.SceneTitle ?? string.Empty).Trim();
            scene.SceneRole = (scene.SceneRole ?? string.Empty).Trim();
            scene.ShowcaseLocalPickPath = (scene.ShowcaseLocalPickPath ?? string.Empty).Trim();
            scene.ShowcaseSfxFile = (scene.ShowcaseSfxFile ?? string.Empty).Trim();
            scene.ShowcaseSfxPlacement = ShowcaseSfxCatalog.NormalizePlacement(scene.ShowcaseSfxPlacement);
            scene.ShowcaseSfxGeminiHint = (scene.ShowcaseSfxGeminiHint ?? string.Empty).Trim();
            if (scene.ShowcaseSfxVolumePercent <= 0)
            {
                scene.ShowcaseSfxVolumePercent = ShowcaseSfxCatalog.DefaultVolumePercent;
            }
            return scene;
        }
    }
}
