using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private CheckBox chkUseMultiVoiceNarration;

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
                Category = source.Category ?? string.Empty
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
            if (ordered.Count >= 4)
            {
                return ordered.Take(4).Select(CloneAiVideoGenItem).Where(x => x != null).ToList();
            }

            return GetDeepDiveBuffer()
                .Where(ShouldShowAiVideoGenItem)
                .Take(4)
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

        private void MarkAiVideoGenItemsProcessed(IEnumerable<AiVideoGenInputItem> renderedItems)
        {
            MarkSlideshowItemsProcessed(renderedItems);
        }
    }
}
