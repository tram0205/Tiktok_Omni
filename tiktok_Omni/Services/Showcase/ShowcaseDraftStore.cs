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

        public string ShowcaseVideoFormatId { get; set; } = ShowcaseVideoFormatPresets.DefaultId;

        public string ShowcaseOutputAspectId { get; set; } = ShowcaseOutputAspectPresets.DefaultId;

        public int ShowcaseOutputAspectCustomWidth { get; set; } = ShowcaseOutputAspectPresets.DefaultCustomWidth;

        public int ShowcaseOutputAspectCustomHeight { get; set; } = ShowcaseOutputAspectPresets.DefaultCustomHeight;

        public string ShowcaseHookText { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayHook { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayHookAnimation { get; set; } = string.Empty;

        public string ShowcaseCtaText { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayCta { get; set; } = string.Empty;

        public string ShowcaseSubtitleDisplayCtaAnimation { get; set; } = string.Empty;

        public bool ShowcaseSubtitleDisplayCtaDisabled { get; set; }

        public long ShowcaseVoiceoverClipFingerprint { get; set; }

        public long ShowcaseVoiceoverClipPathFingerprint { get; set; }

        public string ShowcaseVoiceoverClipDurationSignature { get; set; } = string.Empty;

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

        public string ShowcaseSubtitlePrimaryColourAss { get; set; } = string.Empty;

        public string ShowcaseSubtitleDecorPreset { get; set; } = string.Empty;

        public string ShowcaseHookSubtitlePrimaryColourAss { get; set; } = string.Empty;

        public string ShowcaseHookSubtitleDecorPreset { get; set; } = string.Empty;

        public string ShowcaseSubtitleLookPreset { get; set; } = string.Empty;

        public string ShowcaseHookSubtitleLookPreset { get; set; } = string.Empty;

        public string ShowcaseSubtitleHighlightColourAss { get; set; } = string.Empty;

        public string ShowcaseHookSubtitleHighlightColourAss { get; set; } = string.Empty;

        public string ShowcaseBackgroundMusicFile { get; set; } = string.Empty;

        public int ShowcaseMusicVolume { get; set; } = 14;

        public int ShowcaseNarrationSpeedPercent { get; set; }

        public int ShowcaseHookNarrationSpeedPercent { get; set; }

        public int ShowcaseBodyNarrationSpeedPercent { get; set; }

        public string ShowcaseTtsEngine { get; set; } = string.Empty;

        public string ShowcaseHookTtsEngine { get; set; } = string.Empty;

        public string ShowcaseBodyTtsEngine { get; set; } = string.Empty;

        public string ShowcaseVoicePresetId { get; set; } = string.Empty;

        public string ShowcaseVoiceAgeId { get; set; } = string.Empty;

        public string ShowcaseVoiceGenderId { get; set; } = string.Empty;

        public string ShowcaseVoiceLanguageId { get; set; } = string.Empty;

        public string ShowcaseHookElevenPersona { get; set; } = string.Empty;

        public string ShowcaseVoiceToneId { get; set; } = string.Empty;

        public int ShowcaseElevenCustomStabilityPercent { get; set; }

        public int ShowcaseElevenCustomSimilarityPercent { get; set; }

        public int ShowcaseElevenCustomStylePercent { get; set; }

        public string ShowcaseHookStyleKey { get; set; } = string.Empty;

        public int ShowcaseEdgeRateOffsetPercent { get; set; }

        public int ShowcaseEdgePitchOffsetHz { get; set; }

        public string ShowcaseBodyVoicePresetId { get; set; } = string.Empty;

        public string ShowcaseBodyVoiceAgeId { get; set; } = string.Empty;

        public string ShowcaseBodyVoiceGenderId { get; set; } = string.Empty;

        public string ShowcaseBodyVoiceLanguageId { get; set; } = string.Empty;

        public string ShowcaseBodyElevenPersona { get; set; } = string.Empty;

        public string ShowcaseBodyVoiceToneId { get; set; } = string.Empty;

        public int ShowcaseBodyElevenCustomStabilityPercent { get; set; }

        public int ShowcaseBodyElevenCustomSimilarityPercent { get; set; }

        public int ShowcaseBodyElevenCustomStylePercent { get; set; }

        public string ShowcaseBodyStyleKey { get; set; } = string.Empty;

        public int ShowcaseBodyEdgeRateOffsetPercent { get; set; }

        public int ShowcaseBodyEdgePitchOffsetHz { get; set; }

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

        public string ShowcaseCtaBrollSubLibraryId { get; set; } = string.Empty;

        public string ShowcaseCtaBrollLibraryId { get; set; } = string.Empty;

        public double ShowcaseTransitionSeconds { get; set; } = 0.6;

        public bool ShowcaseBrandLogoEnabled { get; set; }

        public string ShowcaseBrandLogoFile { get; set; } = string.Empty;

        public string ShowcaseBrandLogoPositionId { get; set; } = ShowcaseBrandLogoPositionCatalog.BottomRight;

        public int ShowcaseBrandLogoScaleWidthPercent { get; set; } = ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent;

        public int ShowcaseBrandLogoMarginX { get; set; } = ShowcaseBrandOverlayHelper.DefaultMargin;

        public int ShowcaseBrandLogoMarginY { get; set; } = ShowcaseBrandOverlayHelper.DefaultMargin;

        public int ShowcaseBrandLogoOpacityPercent { get; set; } = ShowcaseBrandOverlayHelper.DefaultOpacityPercent;

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
        private const string BackupFileName = "draft_showcase.json.bak";

        public ShowcaseDraftDocument Load()
        {
            var primary = LoadFromPath(AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy));
            var backup = LoadFromPath(AppDataPaths.PersistentFile(BackupFileName), allowMissing: true);

            var doc = ChooseRicherDraft(primary, backup);
            if (doc == null)
            {
                return new ShowcaseDraftDocument();
            }

            if (migrateFromLegacy && doc.Videos.Count > 0)
            {
                Save(doc);
            }

            return doc;
        }

        /// <summary>Đọc bản sao lưu gần nhất — dùng khi file chính bị ghi đè rỗng / ít dòng hơn.</summary>
        public ShowcaseDraftDocument LoadBackup()
        {
            return LoadFromPath(AppDataPaths.PersistentFile(BackupFileName), allowMissing: true)
                ?? new ShowcaseDraftDocument();
        }

        /// <summary>Đọc file draft chính trên đĩa (không gộp .bak) — dùng trước khi ghi để tránh mất dòng.</summary>
        public ShowcaseDraftDocument LoadPrimaryFile()
        {
            return LoadFromPath(AppDataPaths.PersistentFile(FileName), allowMissing: true)
                ?? new ShowcaseDraftDocument();
        }

        private static ShowcaseDraftDocument ChooseRicherDraft(
            ShowcaseDraftDocument primary,
            ShowcaseDraftDocument backup)
        {
            primary = primary ?? new ShowcaseDraftDocument();
            backup = backup ?? new ShowcaseDraftDocument();
            var primaryCount = primary.Videos?.Count ?? 0;
            var backupCount = backup.Videos?.Count ?? 0;
            if (backupCount > primaryCount)
            {
                return backup;
            }

            return primary;
        }

        private static ShowcaseDraftDocument LoadFromPath(string path, bool allowMissing = false)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return allowMissing ? null : new ShowcaseDraftDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<ShowcaseDraftDocument>(json);
                if (doc == null)
                {
                    return allowMissing ? null : new ShowcaseDraftDocument();
                }

                doc.Videos = doc.Videos?
                    .Where(v => v != null)
                    .Select(NormalizeVideo)
                    .ToList() ?? new List<ShowcaseVideoDraftEntry>();
                return doc;
            }
            catch
            {
                return allowMissing ? null : new ShowcaseDraftDocument();
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
                var path = AppDataPaths.PersistentFile(FileName);
                var backupPath = AppDataPaths.PersistentFile(BackupFileName);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, backupPath, overwrite: true);
                    }
                    catch
                    {
                        // ignored — vẫn ghi file chính
                    }
                }

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
                ShowcaseVideoFormatId = ShowcaseVideoFormatPresets.ResolveId(video.ShowcaseVideoFormatId),
                ShowcaseOutputAspectId = ShowcaseOutputAspectPresets.ResolveId(
                    video.ShowcaseOutputAspectId,
                    null),
                ShowcaseOutputAspectCustomWidth = ShowcaseOutputAspectPresets.NormalizeCustomWidth(
                    video.ShowcaseOutputAspectCustomWidth),
                ShowcaseOutputAspectCustomHeight = ShowcaseOutputAspectPresets.NormalizeCustomHeight(
                    video.ShowcaseOutputAspectCustomHeight),
                ShowcaseHookText = video.ShowcaseHookText ?? string.Empty,
                ShowcaseSubtitleDisplayHook = video.ShowcaseSubtitleDisplayHook ?? string.Empty,
                ShowcaseSubtitleDisplayHookAnimation = video.ShowcaseSubtitleDisplayHookAnimation ?? string.Empty,
                ShowcaseCtaText = video.ShowcaseCtaText ?? string.Empty,
                ShowcaseSubtitleDisplayCta = video.ShowcaseSubtitleDisplayCta ?? string.Empty,
                ShowcaseSubtitleDisplayCtaAnimation = video.ShowcaseSubtitleDisplayCtaAnimation ?? string.Empty,
                ShowcaseSubtitleDisplayCtaDisabled = video.ShowcaseSubtitleDisplayCtaDisabled,
                ShowcaseVoiceoverClipFingerprint = video.ShowcaseVoiceoverClipFingerprint,
                ShowcaseVoiceoverClipPathFingerprint = video.ShowcaseVoiceoverClipPathFingerprint,
                ShowcaseVoiceoverClipDurationSignature = video.ShowcaseVoiceoverClipDurationSignature ?? string.Empty,
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
                ShowcaseSubtitlePrimaryColourAss = video.ShowcaseSubtitlePrimaryColourAss ?? string.Empty,
                ShowcaseSubtitleDecorPreset = video.ShowcaseSubtitleDecorPreset ?? string.Empty,
                ShowcaseHookSubtitlePrimaryColourAss = video.ShowcaseHookSubtitlePrimaryColourAss ?? string.Empty,
                ShowcaseHookSubtitleDecorPreset = video.ShowcaseHookSubtitleDecorPreset ?? string.Empty,
                ShowcaseSubtitleLookPreset = video.ShowcaseSubtitleLookPreset ?? string.Empty,
                ShowcaseHookSubtitleLookPreset = video.ShowcaseHookSubtitleLookPreset ?? string.Empty,
                ShowcaseSubtitleHighlightColourAss = video.ShowcaseSubtitleHighlightColourAss ?? string.Empty,
                ShowcaseHookSubtitleHighlightColourAss = video.ShowcaseHookSubtitleHighlightColourAss ?? string.Empty,
                ShowcaseBackgroundMusicFile = video.ShowcaseBackgroundMusicFile ?? string.Empty,
                ShowcaseMusicVolume = video.ShowcaseMusicVolume,
                ShowcaseNarrationSpeedPercent = video.ShowcaseNarrationSpeedPercent,
                ShowcaseHookNarrationSpeedPercent = video.ShowcaseHookNarrationSpeedPercent,
                ShowcaseBodyNarrationSpeedPercent = video.ShowcaseBodyNarrationSpeedPercent,
                ShowcaseTtsEngine = video.ShowcaseTtsEngine ?? string.Empty,
                ShowcaseHookTtsEngine = video.ShowcaseHookTtsEngine ?? string.Empty,
                ShowcaseBodyTtsEngine = video.ShowcaseBodyTtsEngine ?? string.Empty,
                ShowcaseVoicePresetId = video.ShowcaseVoicePresetId ?? string.Empty,
                ShowcaseVoiceAgeId = video.ShowcaseVoiceAgeId ?? string.Empty,
                ShowcaseVoiceGenderId = video.ShowcaseVoiceGenderId ?? string.Empty,
                ShowcaseVoiceLanguageId = video.ShowcaseVoiceLanguageId ?? string.Empty,
                ShowcaseHookElevenPersona = video.ShowcaseHookElevenPersona ?? string.Empty,
                ShowcaseVoiceToneId = video.ShowcaseVoiceToneId ?? string.Empty,
                ShowcaseElevenCustomStabilityPercent = video.ShowcaseElevenCustomStabilityPercent,
                ShowcaseElevenCustomSimilarityPercent = video.ShowcaseElevenCustomSimilarityPercent,
                ShowcaseElevenCustomStylePercent = video.ShowcaseElevenCustomStylePercent,
                ShowcaseHookStyleKey = video.ShowcaseHookStyleKey ?? string.Empty,
                ShowcaseEdgeRateOffsetPercent = video.ShowcaseEdgeRateOffsetPercent,
                ShowcaseEdgePitchOffsetHz = video.ShowcaseEdgePitchOffsetHz,
                ShowcaseBodyVoicePresetId = video.ShowcaseBodyVoicePresetId ?? string.Empty,
                ShowcaseBodyVoiceAgeId = video.ShowcaseBodyVoiceAgeId ?? string.Empty,
                ShowcaseBodyVoiceGenderId = video.ShowcaseBodyVoiceGenderId ?? string.Empty,
                ShowcaseBodyVoiceLanguageId = video.ShowcaseBodyVoiceLanguageId ?? string.Empty,
                ShowcaseBodyElevenPersona = video.ShowcaseBodyElevenPersona ?? string.Empty,
                ShowcaseBodyVoiceToneId = video.ShowcaseBodyVoiceToneId ?? string.Empty,
                ShowcaseBodyElevenCustomStabilityPercent = video.ShowcaseBodyElevenCustomStabilityPercent,
                ShowcaseBodyElevenCustomSimilarityPercent = video.ShowcaseBodyElevenCustomSimilarityPercent,
                ShowcaseBodyElevenCustomStylePercent = video.ShowcaseBodyElevenCustomStylePercent,
                ShowcaseBodyStyleKey = video.ShowcaseBodyStyleKey ?? string.Empty,
                ShowcaseBodyEdgeRateOffsetPercent = video.ShowcaseBodyEdgeRateOffsetPercent,
                ShowcaseBodyEdgePitchOffsetHz = video.ShowcaseBodyEdgePitchOffsetHz,
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
                ShowcaseCtaBrollSubLibraryId = video.ShowcaseCtaBrollSubLibraryId ?? string.Empty,
                ShowcaseCtaBrollLibraryId = video.ShowcaseCtaBrollLibraryId ?? string.Empty,
                ShowcaseTransitionSeconds = video.ShowcaseTransitionSeconds,
                ShowcaseBrandLogoEnabled = video.ShowcaseBrandLogoEnabled,
                ShowcaseBrandLogoFile = video.ShowcaseBrandLogoFile ?? string.Empty,
                ShowcaseBrandLogoPositionId = video.ShowcaseBrandLogoPositionId ?? ShowcaseBrandLogoPositionCatalog.BottomRight,
                ShowcaseBrandLogoScaleWidthPercent = video.ShowcaseBrandLogoScaleWidthPercent,
                ShowcaseBrandLogoMarginX = video.ShowcaseBrandLogoMarginX,
                ShowcaseBrandLogoMarginY = video.ShowcaseBrandLogoMarginY,
                ShowcaseBrandLogoOpacityPercent = video.ShowcaseBrandLogoOpacityPercent,
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
                ShowcaseVideoFormatId = ShowcaseVideoFormatPresets.ResolveId(entry.ShowcaseVideoFormatId),
                ShowcaseOutputAspectId = ShowcaseOutputAspectPresets.ResolveId(entry.ShowcaseOutputAspectId, null),
                ShowcaseOutputAspectCustomWidth = ShowcaseOutputAspectPresets.NormalizeCustomWidth(
                    entry.ShowcaseOutputAspectCustomWidth),
                ShowcaseOutputAspectCustomHeight = ShowcaseOutputAspectPresets.NormalizeCustomHeight(
                    entry.ShowcaseOutputAspectCustomHeight),
                ShowcaseHookText = entry.ShowcaseHookText ?? string.Empty,
                ShowcaseSubtitleDisplayHook = entry.ShowcaseSubtitleDisplayHook ?? string.Empty,
                ShowcaseSubtitleDisplayHookAnimation = entry.ShowcaseSubtitleDisplayHookAnimation ?? string.Empty,
                ShowcaseCtaText = entry.ShowcaseCtaText ?? string.Empty,
                ShowcaseSubtitleDisplayCta = entry.ShowcaseSubtitleDisplayCta ?? string.Empty,
                ShowcaseSubtitleDisplayCtaAnimation = entry.ShowcaseSubtitleDisplayCtaAnimation ?? string.Empty,
                ShowcaseSubtitleDisplayCtaDisabled = entry.ShowcaseSubtitleDisplayCtaDisabled,
                ShowcaseVoiceoverClipFingerprint = entry.ShowcaseVoiceoverClipFingerprint,
                ShowcaseVoiceoverClipPathFingerprint = entry.ShowcaseVoiceoverClipPathFingerprint,
                ShowcaseVoiceoverClipDurationSignature = entry.ShowcaseVoiceoverClipDurationSignature ?? string.Empty,
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
                ShowcaseSubtitlePrimaryColourAss = entry.ShowcaseSubtitlePrimaryColourAss ?? string.Empty,
                ShowcaseSubtitleDecorPreset = entry.ShowcaseSubtitleDecorPreset ?? string.Empty,
                ShowcaseHookSubtitlePrimaryColourAss = entry.ShowcaseHookSubtitlePrimaryColourAss ?? string.Empty,
                ShowcaseHookSubtitleDecorPreset = entry.ShowcaseHookSubtitleDecorPreset ?? string.Empty,
                ShowcaseSubtitleLookPreset = entry.ShowcaseSubtitleLookPreset ?? string.Empty,
                ShowcaseHookSubtitleLookPreset = entry.ShowcaseHookSubtitleLookPreset ?? string.Empty,
                ShowcaseSubtitleHighlightColourAss = entry.ShowcaseSubtitleHighlightColourAss ?? string.Empty,
                ShowcaseHookSubtitleHighlightColourAss = entry.ShowcaseHookSubtitleHighlightColourAss ?? string.Empty,
                ShowcaseBackgroundMusicFile = entry.ShowcaseBackgroundMusicFile ?? string.Empty,
                ShowcaseMusicVolume = entry.ShowcaseMusicVolume >= 0 ? entry.ShowcaseMusicVolume : 14,
                ShowcaseNarrationSpeedPercent = entry.ShowcaseNarrationSpeedPercent,
                ShowcaseHookNarrationSpeedPercent = entry.ShowcaseHookNarrationSpeedPercent,
                ShowcaseBodyNarrationSpeedPercent = entry.ShowcaseBodyNarrationSpeedPercent,
                ShowcaseTtsEngine = entry.ShowcaseTtsEngine ?? string.Empty,
                ShowcaseHookTtsEngine = entry.ShowcaseHookTtsEngine ?? string.Empty,
                ShowcaseBodyTtsEngine = entry.ShowcaseBodyTtsEngine ?? string.Empty,
                ShowcaseVoicePresetId = entry.ShowcaseVoicePresetId ?? string.Empty,
                ShowcaseVoiceAgeId = entry.ShowcaseVoiceAgeId ?? string.Empty,
                ShowcaseVoiceGenderId = entry.ShowcaseVoiceGenderId ?? string.Empty,
                ShowcaseVoiceLanguageId = entry.ShowcaseVoiceLanguageId ?? string.Empty,
                ShowcaseHookElevenPersona = entry.ShowcaseHookElevenPersona ?? string.Empty,
                ShowcaseVoiceToneId = entry.ShowcaseVoiceToneId ?? string.Empty,
                ShowcaseElevenCustomStabilityPercent = entry.ShowcaseElevenCustomStabilityPercent,
                ShowcaseElevenCustomSimilarityPercent = entry.ShowcaseElevenCustomSimilarityPercent,
                ShowcaseElevenCustomStylePercent = entry.ShowcaseElevenCustomStylePercent,
                ShowcaseHookStyleKey = entry.ShowcaseHookStyleKey ?? string.Empty,
                ShowcaseEdgeRateOffsetPercent = entry.ShowcaseEdgeRateOffsetPercent,
                ShowcaseEdgePitchOffsetHz = entry.ShowcaseEdgePitchOffsetHz,
                ShowcaseBodyVoicePresetId = entry.ShowcaseBodyVoicePresetId ?? string.Empty,
                ShowcaseBodyVoiceAgeId = entry.ShowcaseBodyVoiceAgeId ?? string.Empty,
                ShowcaseBodyVoiceGenderId = entry.ShowcaseBodyVoiceGenderId ?? string.Empty,
                ShowcaseBodyVoiceLanguageId = entry.ShowcaseBodyVoiceLanguageId ?? string.Empty,
                ShowcaseBodyElevenPersona = entry.ShowcaseBodyElevenPersona ?? string.Empty,
                ShowcaseBodyVoiceToneId = entry.ShowcaseBodyVoiceToneId ?? string.Empty,
                ShowcaseBodyElevenCustomStabilityPercent = entry.ShowcaseBodyElevenCustomStabilityPercent,
                ShowcaseBodyElevenCustomSimilarityPercent = entry.ShowcaseBodyElevenCustomSimilarityPercent,
                ShowcaseBodyElevenCustomStylePercent = entry.ShowcaseBodyElevenCustomStylePercent,
                ShowcaseBodyStyleKey = entry.ShowcaseBodyStyleKey ?? string.Empty,
                ShowcaseBodyEdgeRateOffsetPercent = entry.ShowcaseBodyEdgeRateOffsetPercent,
                ShowcaseBodyEdgePitchOffsetHz = entry.ShowcaseBodyEdgePitchOffsetHz,
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
                ShowcaseCtaBrollSubLibraryId = entry.ShowcaseCtaBrollSubLibraryId ?? string.Empty,
                ShowcaseCtaBrollLibraryId = entry.ShowcaseCtaBrollLibraryId ?? string.Empty,
                ShowcaseTransitionSeconds = entry.ShowcaseTransitionSeconds > 0 ? entry.ShowcaseTransitionSeconds : 0.6,
                ShowcaseBrandLogoEnabled = entry.ShowcaseBrandLogoEnabled,
                ShowcaseBrandLogoFile = entry.ShowcaseBrandLogoFile ?? string.Empty,
                ShowcaseBrandLogoPositionId = entry.ShowcaseBrandLogoPositionId ?? ShowcaseBrandLogoPositionCatalog.BottomRight,
                ShowcaseBrandLogoScaleWidthPercent = entry.ShowcaseBrandLogoScaleWidthPercent > 0
                    ? entry.ShowcaseBrandLogoScaleWidthPercent
                    : ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent,
                ShowcaseBrandLogoMarginX = entry.ShowcaseBrandLogoMarginX,
                ShowcaseBrandLogoMarginY = entry.ShowcaseBrandLogoMarginY,
                ShowcaseBrandLogoOpacityPercent = entry.ShowcaseBrandLogoOpacityPercent > 0
                    ? entry.ShowcaseBrandLogoOpacityPercent
                    : ShowcaseBrandOverlayHelper.DefaultOpacityPercent,
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
            scene.ShowcaseRealClipSourcePath = (scene.ShowcaseRealClipSourcePath ?? string.Empty).Trim();
            scene.SceneVoiceover = (scene.SceneVoiceover ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayVoiceover = (scene.ShowcaseSubtitleDisplayVoiceover ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayAnimation = (scene.ShowcaseSubtitleDisplayAnimation ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayFontName = (scene.ShowcaseSubtitleDisplayFontName ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayFontFace = (scene.ShowcaseSubtitleDisplayFontFace ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayPosition = (scene.ShowcaseSubtitleDisplayPosition ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayPrimaryColourAss = (scene.ShowcaseSubtitleDisplayPrimaryColourAss ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayDecorPreset = (scene.ShowcaseSubtitleDisplayDecorPreset ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayLookPreset = (scene.ShowcaseSubtitleDisplayLookPreset ?? string.Empty).Trim();
            scene.ShowcaseSubtitleDisplayHighlightColourAss = (scene.ShowcaseSubtitleDisplayHighlightColourAss ?? string.Empty).Trim();
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

            scene.ShowcaseZoomSpeedId = ShowcaseZoomSpeedCatalog.NormalizeId(scene.ShowcaseZoomSpeedId);
            if (string.IsNullOrEmpty(scene.ShowcaseZoomSpeedId)
                && string.Equals(scene.ShowcaseClipTool, ShowcaseClipToolHelper.ToolZoom, StringComparison.Ordinal))
            {
                scene.ShowcaseZoomSpeedId = ShowcaseZoomSpeedCatalog.ResolveSpeedId(null, scene.ZoomHint);
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
