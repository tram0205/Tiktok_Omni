using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

// AiVideoGenInputItem lives in tiktok_Omni.Services

namespace tiktok_Omni.Services
{
    /// <summary>Map cấu hình âm thanh batch Triết lý ↔ ShowcaseVideoItem (dialog Showcase).</summary>
    public static class PhilosophyBatchShowcaseAudioAdapter
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
                ShowcaseBackgroundMusicFile = NormalizeMusicFileName(batch.MusicFolder),
                ShowcaseMusicVolume = PhilosophyBatchHelper.ResolveBatchMusicVolumePercent(batch),
                ShowcaseBodyTtsEngine = batch.BodyTtsEngine ?? string.Empty,
                ShowcaseBodyVoicePresetId = batch.BodyVoicePresetId ?? string.Empty,
                ShowcaseBodyVoiceGenderId = batch.BodyVoiceGenderId ?? string.Empty,
                ShowcaseBodyVoiceLanguageId = batch.BodyVoiceLanguageId ?? string.Empty,
                ShowcaseBodyElevenPersona = batch.BodyElevenPersona ?? string.Empty,
                ShowcaseBodyVoiceToneId = batch.BodyVoiceToneId ?? string.Empty,
                ShowcaseBodyElevenCustomStabilityPercent = batch.BodyElevenCustomStabilityPercent,
                ShowcaseBodyElevenCustomSimilarityPercent = batch.BodyElevenCustomSimilarityPercent,
                ShowcaseBodyElevenCustomStylePercent = batch.BodyElevenCustomStylePercent,
                ShowcaseBodyStyleKey = batch.BodyStyleKey ?? string.Empty,
                ShowcaseBodyEdgeRateOffsetPercent = batch.BodyEdgeRateOffsetPercent,
                ShowcaseBodyEdgePitchOffsetHz = batch.BodyEdgePitchOffsetHz,
                ShowcaseBodyNarrationSpeedPercent = batch.BodyNarrationSpeedPercent
            };
            PhilosophyBatchTtsHelper.SyncBodyVoiceToHookTrack(video);

            var quote = GetPreviewQuote(batch);
            if (quote != null && !string.IsNullOrWhiteSpace(quote.Content))
            {
                video.Scenes.Add(new AiVideoGenInputItem
                {
                    ProductName = video.ProductName,
                    SceneVoiceover = quote.Content.Trim(),
                    ShowcaseSceneSilent = false
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

            var music = (video.ShowcaseBackgroundMusicFile ?? string.Empty).Trim();
            if (VideoReupRowItem.IsNoMusicSelection(music))
            {
                batch.MusicFolder = string.Empty;
            }
            else
            {
                batch.MusicFolder = music;
            }

            batch.MusicVolumePercent = PhilosophyBatchHelper.ClampMusicVolumePercent(
                video.ShowcaseMusicVolume >= 0 ? video.ShowcaseMusicVolume : PhilosophyBatchHelper.DefaultMusicVolumePercent);
            batch.BodyTtsEngine = video.ShowcaseBodyTtsEngine ?? string.Empty;
            batch.BodyVoicePresetId = video.ShowcaseBodyVoicePresetId ?? string.Empty;
            batch.BodyVoiceGenderId = video.ShowcaseBodyVoiceGenderId ?? string.Empty;
            batch.BodyVoiceLanguageId = video.ShowcaseBodyVoiceLanguageId ?? string.Empty;
            batch.BodyElevenPersona = video.ShowcaseBodyElevenPersona ?? string.Empty;
            batch.BodyVoiceToneId = video.ShowcaseBodyVoiceToneId ?? string.Empty;
            batch.BodyElevenCustomStabilityPercent = video.ShowcaseBodyElevenCustomStabilityPercent;
            batch.BodyElevenCustomSimilarityPercent = video.ShowcaseBodyElevenCustomSimilarityPercent;
            batch.BodyElevenCustomStylePercent = video.ShowcaseBodyElevenCustomStylePercent;
            batch.BodyStyleKey = video.ShowcaseBodyStyleKey ?? string.Empty;
            batch.BodyEdgeRateOffsetPercent = video.ShowcaseBodyEdgeRateOffsetPercent;
            batch.BodyEdgePitchOffsetHz = video.ShowcaseBodyEdgePitchOffsetHz;
            batch.BodyNarrationSpeedPercent = video.ShowcaseBodyNarrationSpeedPercent;
        }

        public static PhilosophyScriptItem GetPreviewQuote(PhilosophyBatchItem batch)
        {
            return batch?.Quotes?
                .FirstOrDefault(q => q != null && !string.IsNullOrWhiteSpace(q.Content));
        }

        public static IList<AiVideoGenInputItem> BuildPreviewScenes(PhilosophyBatchItem batch)
        {
            var quote = GetPreviewQuote(batch);
            if (quote == null || string.IsNullOrWhiteSpace(quote.Content))
            {
                return Array.Empty<AiVideoGenInputItem>();
            }

            return new List<AiVideoGenInputItem>
            {
                new AiVideoGenInputItem
                {
                    ProductName = (batch.Topic ?? string.Empty).Trim(),
                    SceneVoiceover = quote.Content.Trim(),
                    ShowcaseSceneSilent = false
                }
            };
        }

        private static string NormalizeMusicFileName(string musicFolder)
        {
            var raw = (musicFolder ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            if (VideoReupRowItem.IsNoMusicSelection(raw))
            {
                return VideoReupRowItem.NoMusicSelectionLabel;
            }

            return File.Exists(raw) ? Path.GetFileName(raw) : raw;
        }
    }

}
