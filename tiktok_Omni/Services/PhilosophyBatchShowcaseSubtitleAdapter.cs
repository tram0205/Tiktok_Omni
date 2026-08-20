using System;
using System.Collections.Generic;
using System.Linq;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Map phụ đề batch Triết lý ↔ ShowcaseVideoItem (dialog Showcase).</summary>
    public static class PhilosophyBatchShowcaseSubtitleAdapter
    {
        public static ShowcaseVideoItem ToShowcaseVideo(PhilosophyBatchItem batch, string profileName)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var video = new ShowcaseVideoItem
            {
                VideoId = batch.BatchId,
                ProfileName = profile,
                ProductName = (batch.Topic ?? string.Empty).Trim(),
                ShowcaseSubtitleEnabled = batch.SubtitleEnabled,
                ShowcaseHookSubtitleEnabled = false,
                ShowcaseSubtitleStyleLabel = batch.SubtitleStyleLabel ?? string.Empty,
                ShowcaseSubtitleFontName = batch.SubtitleFontName ?? string.Empty,
                ShowcaseSubtitleFontSize = batch.SubtitleFontSize,
                ShowcaseSubtitlePosition = batch.SubtitlePosition ?? string.Empty,
                ShowcaseSubtitleAnimation = batch.SubtitleAnimation ?? string.Empty,
                ShowcaseSubtitleBold = batch.SubtitleBold,
                ShowcaseSubtitleItalic = batch.SubtitleItalic,
                ShowcaseSubtitleWordsPerLine = batch.SubtitleWordsPerLine,
                ShowcaseSubtitlePrimaryColourAss = batch.SubtitlePrimaryColourAss ?? string.Empty,
                ShowcaseSubtitleDecorPreset = batch.SubtitleDecorPreset ?? string.Empty,
                ShowcaseSubtitleLookPreset = batch.SubtitleLookPreset ?? string.Empty,
                ShowcaseSubtitleHighlightColourAss = batch.SubtitleHighlightColourAss ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(video.ShowcaseSubtitleHighlightColourAss)
                && !string.IsNullOrWhiteSpace(batch.SubtitleSecondaryColourAss))
            {
                video.ShowcaseSubtitleHighlightColourAss = batch.SubtitleSecondaryColourAss;
            }

            var quotes = (batch.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>())
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.Content))
                .ToList();

            for (var i = 0; i < quotes.Count; i++)
            {
                var quote = quotes[i];
                var displayQuote = (quote.SubtitleDisplayQuote ?? string.Empty).Trim();
                var displayAnim = (quote.SubtitleDisplayAnimation ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(displayQuote)
                    && i == 0
                    && !string.IsNullOrWhiteSpace(batch.SubtitleDisplayQuote))
                {
                    displayQuote = batch.SubtitleDisplayQuote.Trim();
                    displayAnim = (batch.SubtitleDisplayAnimation ?? string.Empty).Trim();
                }

                video.Scenes.Add(new AiVideoGenInputItem
                {
                    ProductName = video.ProductName,
                    SceneTitle = "Câu " + (i + 1),
                    SceneVoiceover = quote.Content.Trim(),
                    ShowcaseSceneSilent = false,
                    ShowcaseSubtitleDisplayVoiceover = displayQuote,
                    ShowcaseSubtitleDisplayAnimation = displayAnim,
                    ShowcaseSubtitleDisplayDisabled = !batch.SubtitleEnabled
                });
            }

            return video;
        }

        public static void ApplyFromShowcaseVideo(ShowcaseVideoItem video, PhilosophyBatchItem batch)
        {
            if (video == null || batch == null)
            {
                return;
            }

            batch.SubtitleEnabled = video.ShowcaseSubtitleEnabled;
            batch.SubtitleStyleLabel = video.ShowcaseSubtitleStyleLabel ?? string.Empty;
            batch.SubtitleFontName = video.ShowcaseSubtitleFontName ?? string.Empty;
            batch.SubtitleFontSize = video.ShowcaseSubtitleFontSize;
            batch.SubtitlePosition = video.ShowcaseSubtitlePosition ?? string.Empty;
            batch.SubtitleAnimation = video.ShowcaseSubtitleAnimation ?? string.Empty;
            batch.SubtitleBold = video.ShowcaseSubtitleBold;
            batch.SubtitleItalic = video.ShowcaseSubtitleItalic;
            batch.SubtitleWordsPerLine = video.ShowcaseSubtitleWordsPerLine;
            batch.SubtitlePrimaryColourAss = video.ShowcaseSubtitlePrimaryColourAss ?? string.Empty;
            batch.SubtitleDecorPreset = video.ShowcaseSubtitleDecorPreset ?? string.Empty;
            batch.SubtitleLookPreset = video.ShowcaseSubtitleLookPreset ?? string.Empty;
            batch.SubtitleHighlightColourAss = video.ShowcaseSubtitleHighlightColourAss ?? string.Empty;
            batch.SubtitleSecondaryColourAss = string.IsNullOrWhiteSpace(video.ShowcaseSubtitleHighlightColourAss)
                ? batch.SubtitleSecondaryColourAss
                : video.ShowcaseSubtitleHighlightColourAss;

            var quotes = (batch.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>())
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.Content))
                .ToList();
            var scenes = (video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
                .Where(s => s != null && !s.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(s.SceneVoiceover))
                .ToList();

            for (var i = 0; i < quotes.Count && i < scenes.Count; i++)
            {
                quotes[i].SubtitleDisplayQuote = scenes[i].ShowcaseSubtitleDisplayVoiceover ?? string.Empty;
                quotes[i].SubtitleDisplayAnimation = scenes[i].ShowcaseSubtitleDisplayAnimation ?? string.Empty;
            }

            var first = quotes.FirstOrDefault();
            batch.SubtitleDisplayQuote = first?.SubtitleDisplayQuote ?? string.Empty;
            batch.SubtitleDisplayAnimation = first?.SubtitleDisplayAnimation ?? string.Empty;

            PhilosophyBatchHelper.NormalizeBatchQuoteOwnership(batch);
        }

        public static ShowcaseVideoItem ToShowcaseVideoFromQuote(PhilosophyScriptItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var batch = new PhilosophyBatchItem
            {
                Topic = TrimPreview(item.Content),
                SubtitleEnabled = item.SubtitleEnabled,
                SubtitleStyleLabel = item.SubtitleStyleLabel ?? string.Empty,
                SubtitleFontName = item.SubtitleFontName ?? string.Empty,
                SubtitleFontSize = item.SubtitleFontSize,
                SubtitlePosition = item.SubtitlePosition ?? string.Empty,
                SubtitleAnimation = item.SubtitleAnimation ?? string.Empty,
                SubtitleBold = item.SubtitleBold,
                SubtitleItalic = item.SubtitleItalic,
                SubtitleWordsPerLine = item.SubtitleWordsPerLine,
                SubtitlePrimaryColourAss = item.SubtitlePrimaryColourAss ?? string.Empty,
                SubtitleSecondaryColourAss = item.SubtitleSecondaryColourAss ?? string.Empty,
                SubtitleDecorPreset = item.SubtitleDecorPreset ?? string.Empty,
                SubtitleLookPreset = item.SubtitleLookPreset ?? string.Empty,
                SubtitleHighlightColourAss = item.SubtitleHighlightColourAss ?? string.Empty,
                SubtitleDisplayQuote = item.SubtitleDisplayQuote ?? string.Empty,
                SubtitleDisplayAnimation = item.SubtitleDisplayAnimation ?? string.Empty,
                Quotes = new List<PhilosophyScriptItem> { item }
            };

            return ToShowcaseVideo(batch, item.ProfileName);
        }

        private static string TrimPreview(string content)
        {
            var t = (content ?? string.Empty).Trim();
            return t.Length <= 48 ? t : t.Substring(0, 47) + "…";
        }
    }
}
