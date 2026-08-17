using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static AiVideoGenInputItem CloneAiVideoGenItem(AiVideoGenInputItem source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new AiVideoGenInputItem
            {
                ProfileName = source.ProfileName ?? string.Empty,
                SourceKeyword = source.SourceKeyword ?? string.Empty,
                ProductName = source.ProductName ?? string.Empty,
                VideoUrl = source.VideoUrl ?? string.Empty,
                HookText = source.HookText ?? string.Empty,
                Hashtags = source.Hashtags ?? string.Empty,
                Price = source.Price ?? string.Empty,
                ImageUrl = source.ImageUrl ?? string.Empty,
                AffiliateLink = source.AffiliateLink ?? string.Empty,
                ProductId = source.ProductId ?? string.Empty,
                CustomerReviews = source.CustomerReviews ?? string.Empty,
                IsProcessed = source.IsProcessed,
                PipelineStatus = source.PipelineStatus ?? "Chờ",
                SafetyScore = source.SafetyScore,
                ProgressPercent = source.ProgressPercent,
                ThumbnailPath = source.ThumbnailPath ?? string.Empty,
                OutputVideoPath = source.OutputVideoPath ?? string.Empty,
                ScriptPreview = source.ScriptPreview ?? string.Empty,
                Category = source.Category ?? string.Empty,
                SceneRole = source.SceneRole ?? string.Empty,
                SceneTitle = source.SceneTitle ?? string.Empty,
                SceneVoiceover = source.SceneVoiceover ?? string.Empty,
                ShowcaseSubtitleDisplayVoiceover = source.ShowcaseSubtitleDisplayVoiceover ?? string.Empty,
                ShowcaseSubtitleDisplayAnimation = source.ShowcaseSubtitleDisplayAnimation ?? string.Empty,
                ShowcaseSubtitleDisplayDisabled = source.ShowcaseSubtitleDisplayDisabled,
                VeoPrompt = source.VeoPrompt ?? string.Empty,
                ShowcaseImageKind = source.ShowcaseImageKind ?? string.Empty,
                ShowcaseClipTool = source.ShowcaseClipTool ?? string.Empty,
                KlingPrompt = source.KlingPrompt ?? string.Empty,
                ZoomHint = source.ZoomHint ?? string.Empty,
                ShowcaseZoomStyleId = source.ShowcaseZoomStyleId ?? string.Empty,
                ShowcaseZoomSpeedId = source.ShowcaseZoomSpeedId ?? string.Empty,
                ShowcaseClipDurationSeconds = source.ShowcaseClipDurationSeconds,
                ClipPath = source.ClipPath ?? string.Empty,
                ShowcaseRealClipSourcePath = source.ShowcaseRealClipSourcePath ?? string.Empty,
                ShowcaseTheme = source.ShowcaseTheme ?? string.Empty,
                ShowcaseMultiVoice = source.ShowcaseMultiVoice,
                ShowcaseTextSize = source.ShowcaseTextSize > 0 ? source.ShowcaseTextSize : 50,
                ShowcaseMusicVolume = source.ShowcaseMusicVolume >= 0 ? source.ShowcaseMusicVolume : 14,
                ShowcaseTransitionSeconds = source.ShowcaseTransitionSeconds > 0 ? source.ShowcaseTransitionSeconds : 0.6,
                ShowcaseLocalPickPath = source.ShowcaseLocalPickPath ?? string.Empty,
                ShowcaseSceneSilent = source.ShowcaseSceneSilent,
                ShowcaseSfxFile = source.ShowcaseSfxFile ?? string.Empty,
                ShowcaseSfxEnabled = source.ShowcaseSfxEnabled,
                ShowcaseSfxPlacement = source.ShowcaseSfxPlacement ?? ShowcaseSfxCatalog.PlacementSceneStart,
                ShowcaseSfxOffsetSeconds = source.ShowcaseSfxOffsetSeconds,
                ShowcaseSfxVolumePercent = source.ShowcaseSfxVolumePercent > 0
                    ? source.ShowcaseSfxVolumePercent
                    : ShowcaseSfxCatalog.DefaultVolumePercent,
                ShowcaseSfxGeminiHint = source.ShowcaseSfxGeminiHint ?? string.Empty
            };
            return clone;
        }

        private static bool AiVideoGenItemsMatch(AiVideoGenInputItem a, AiVideoGenInputItem b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(a.ImageUrl ?? string.Empty, b.ImageUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.ProductName ?? string.Empty, b.ProductName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldShowAiVideoGenItem(AiVideoGenInputItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (chkAiVideoGenCurrentProfileOnly == null || !chkAiVideoGenCurrentProfileOnly.Checked)
            {
                return true;
            }

            var current = ProfileScopedPaths.ResolveProfileName(GetRunningProfileName());
            var itemProfile = ProfileScopedPaths.ResolveProfileName(item.ProfileName);
            return string.Equals(current, itemProfile, StringComparison.OrdinalIgnoreCase);
        }

        private List<AiVideoGenInputItem> GetSlideshowItemsForRender()
        {
            return GetSlideshowBuffer()
                .Where(ShouldShowAiVideoGenItem)
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList();
        }

        private List<AiVideoGenInputItem> GetDeepDiveOrderedScenesForRender()
        {
            var ordered = GetDeepDiveStoryboardOrderedBuffer();
            if (ordered.Count >= ShowcaseWorkflowConstants.MinScenes)
            {
                return ordered.Select(CloneAiVideoGenItem).Where(x => x != null).ToList();
            }

            return GetDeepDiveBuffer()
                .Where(ShouldShowAiVideoGenItem)
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList();
        }

        private static AiVideoGenInputItem MapAffiliateToAiVideoInput(AffiliateCandidate item)
        {
            var productName = (item?.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(productName))
            {
                productName = (item?.Creator ?? string.Empty).Trim();
            }

            var link = (item?.LinkedProduct ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(link) || string.Equals(link, "Chưa rõ", StringComparison.OrdinalIgnoreCase))
            {
                link = (item?.VideoUrl ?? string.Empty).Trim();
            }

            var scriptPreview = (item?.VoiceoverTranscript ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(scriptPreview))
            {
                scriptPreview = (item?.VideoScript ?? string.Empty).Trim();
            }

            return new AiVideoGenInputItem
            {
                ProfileName = ProfileScopedPaths.ResolveProfileFromCandidate(item),
                SourceKeyword = (item?.SourceKeyword ?? string.Empty).Trim(),
                ProductName = productName,
                VideoUrl = (item?.VideoUrl ?? string.Empty).Trim(),
                HookText = (item?.VoiceoverTranscript ?? string.Empty).Trim(),
                Hashtags = (item?.Hashtags ?? string.Empty).Trim(),
                Price = string.IsNullOrWhiteSpace(item?.Price) ? "N/A" : item.Price.Trim(),
                ImageUrl = (item?.ImageUrl ?? string.Empty).Trim(),
                AffiliateLink = link,
                ProductId = string.Empty,
                CustomerReviews = (item?.CustomerReviews ?? string.Empty).Trim(),
                Category = (item?.Category ?? string.Empty).Trim(),
                ScriptPreview = scriptPreview,
                IsProcessed = false
            };
        }

        private bool UseMultiVoiceNarrationEnabled()
        {
            return chkUseMultiVoiceNarration != null && chkUseMultiVoiceNarration.Checked;
        }

        private sealed class ShowcaseRenderSettings
        {
            public int TextSize { get; set; } = 50;
            public int MusicVolume { get; set; } = 14;
            public double TransitionSeconds { get; set; } = 0.6;
        }

        private AiVideoGenInputItem GetShowcaseSettingsSourceScene()
        {
            return GetActiveShowcaseVideo()?.Scenes.FirstOrDefault()
                   ?? GetDeepDiveStoryboardOrderedBuffer().FirstOrDefault();
        }

        private double GetAppTransitionSeconds()
        {
            if (numAiTransitionDuration != null)
            {
                return ShowcaseTransitionHelper.ClampSeconds((double)numAiTransitionDuration.Value);
            }

            return 0.6d;
        }

        private ShowcaseRenderSettings GetShowcaseRenderSettings()
        {
            var appTransition = GetAppTransitionSeconds();
            var video = GetActiveShowcaseVideo();
            if (video != null)
            {
                return new ShowcaseRenderSettings
                {
                    TextSize = video.ShowcaseSubtitleFontSize > 0 ? video.ShowcaseSubtitleFontSize : 72,
                    MusicVolume = video.ShowcaseMusicVolume >= 0 ? video.ShowcaseMusicVolume : 14,
                    TransitionSeconds = appTransition
                };
            }

            var source = GetShowcaseSettingsSourceScene();
            if (source == null)
            {
                return new ShowcaseRenderSettings { TransitionSeconds = appTransition };
            }

            return new ShowcaseRenderSettings
            {
                TextSize = source.ShowcaseTextSize > 0 ? source.ShowcaseTextSize : 50,
                MusicVolume = source.ShowcaseMusicVolume >= 0 ? source.ShowcaseMusicVolume : 14,
                TransitionSeconds = appTransition
            };
        }

        private void ApplyShowcaseSettingsTemplate(AiVideoGenInputItem target, AiVideoGenInputItem template)
        {
            if (target == null)
            {
                return;
            }

            if (template != null)
            {
                target.ShowcaseTheme = template.ShowcaseTheme ?? string.Empty;
                target.ShowcaseTextSize = template.ShowcaseTextSize > 0 ? template.ShowcaseTextSize : 50;
                target.ShowcaseMusicVolume = template.ShowcaseMusicVolume >= 0 ? template.ShowcaseMusicVolume : 14;
                target.ShowcaseTransitionSeconds = GetAppTransitionSeconds();
                return;
            }

            target.ShowcaseTextSize = 50;
            target.ShowcaseMusicVolume = 14;
            target.ShowcaseTransitionSeconds = GetAppTransitionSeconds();
        }

        private void SyncShowcaseSessionSettingsAcrossScenes(AiVideoGenInputItem source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var scene in GetDeepDiveStoryboardOrderedBuffer())
            {
                if (scene == null || ReferenceEquals(scene, source))
                {
                    continue;
                }

                scene.ShowcaseTheme = source.ShowcaseTheme ?? string.Empty;
                scene.ShowcaseTextSize = source.ShowcaseTextSize > 0 ? source.ShowcaseTextSize : 50;
                scene.ShowcaseMusicVolume = source.ShowcaseMusicVolume >= 0 ? source.ShowcaseMusicVolume : 14;
                scene.ShowcaseTransitionSeconds = GetAppTransitionSeconds();
            }

            dgvDeepDiveInput?.Refresh();
        }

        private static bool IsShowcaseSettingsGridColumn(string columnName)
        {
            return string.Equals(columnName, "colAiShowcaseTheme", StringComparison.Ordinal)
                   || string.Equals(columnName, "colAiShowcaseProductType", StringComparison.Ordinal)
                   || string.Equals(columnName, "colAiShowcaseClipMode", StringComparison.Ordinal)
                   || string.Equals(columnName, "colAiProduct", StringComparison.Ordinal);
        }

        private void MarkAiVideoGenItemsProcessed(IEnumerable<AiVideoGenInputItem> renderedItems)
        {
            MarkSlideshowItemsProcessed(renderedItems);
        }
    }
}
