using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public static class PhilosophyBatchHelper
    {
        public const string BackgroundModeBroll = "B-roll";
        public const string BackgroundModeAi = "AI tạo";
        public const string BackgroundModeZoom = "Zoom ảnh";
        public const string BackgroundModeSlideshow = "Slideshow ảnh";
        public const string BackgroundModeAiStillZoom = "AI ảnh → zoom";
        public const string BackgroundModeZoomBrollHybrid = "Zoom + B-roll";

        public const int DefaultMusicVolumePercent = PhilosophyAudioDefaults.DefaultMusicVolumePercent;

        /// <summary>Linear gain cơ sở (~10% khi slider = 100%).</summary>
        public const double MusicBedBaseVolume = 0.50d;

        private static readonly string[] AllowedQuoteMoods =
        {
            "calm", "melancholic", "hopeful", "intense", "reflective"
        };

        public static string NormalizeQuoteMood(string mood)
        {
            var t = (mood ?? string.Empty).Trim().ToLowerInvariant();
            foreach (var allowed in AllowedQuoteMoods)
            {
                if (string.Equals(t, allowed, StringComparison.Ordinal))
                {
                    return allowed;
                }
            }

            return "reflective";
        }

        public static string TrimGridLabel(string text, int maxLen, string emptyFallback)
        {
            var t = (text ?? string.Empty).Trim();
            if (t.Length == 0)
            {
                return emptyFallback;
            }

            return t.Length <= maxLen ? t : t.Substring(0, maxLen - 1) + "…";
        }

        public static string ToSimpleBackgroundModeLabel(int visualMode)
        {
            switch (PhilosophyVisualModes.Normalize(visualMode))
            {
                case PhilosophyVisualModes.ImageSlideshow:
                    return BackgroundModeSlideshow;
                case PhilosophyVisualModes.AiStillZoom:
                    return BackgroundModeAiStillZoom;
                case PhilosophyVisualModes.ZoomBrollHybrid:
                    return BackgroundModeZoomBrollHybrid;
                case PhilosophyVisualModes.ImageZoom:
                    return BackgroundModeZoom;
                case PhilosophyVisualModes.Broll:
                    return BackgroundModeBroll;
                default:
                    return BackgroundModeAi;
            }
        }

        public static int FromSimpleBackgroundModeLabel(string label)
        {
            var t = (label ?? string.Empty).Trim();
            if (string.Equals(t, BackgroundModeZoomBrollHybrid, StringComparison.OrdinalIgnoreCase)
                || t.IndexOf("zoom + b-roll", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("hybrid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PhilosophyVisualModes.ZoomBrollHybrid;
            }

            if (string.Equals(t, BackgroundModeAiStillZoom, StringComparison.OrdinalIgnoreCase)
                || t.IndexOf("ai ảnh", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PhilosophyVisualModes.AiStillZoom;
            }

            if (string.Equals(t, BackgroundModeSlideshow, StringComparison.OrdinalIgnoreCase)
                || t.IndexOf("slideshow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PhilosophyVisualModes.ImageSlideshow;
            }

            if (string.Equals(t, BackgroundModeZoom, StringComparison.OrdinalIgnoreCase))
            {
                return PhilosophyVisualModes.ImageZoom;
            }

            if (string.Equals(t, BackgroundModeBroll, StringComparison.OrdinalIgnoreCase)
                || t.IndexOf("b-roll", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("broll", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PhilosophyVisualModes.Broll;
            }

            return PhilosophyVisualModes.VeoMascot;
        }

        public static string DescribeBatchBackgroundSummary(PhilosophyBatchItem batch)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return "Chưa có câu…";
            }

            var broll = batch.Quotes.Count(q => PhilosophyVisualModes.Normalize(q.VisualMode) == PhilosophyVisualModes.Broll);
            var zoom = batch.Quotes.Count(q => PhilosophyVisualModes.Normalize(q.VisualMode) == PhilosophyVisualModes.ImageZoom);
            var slideshow = batch.Quotes.Count(q => PhilosophyVisualModes.Normalize(q.VisualMode) == PhilosophyVisualModes.ImageSlideshow);
            var hybrid = batch.Quotes.Count(q => PhilosophyVisualModes.Normalize(q.VisualMode) == PhilosophyVisualModes.ZoomBrollHybrid);
            var aiStill = batch.Quotes.Count(q => PhilosophyVisualModes.Normalize(q.VisualMode) == PhilosophyVisualModes.AiStillZoom);
            var ai = batch.Quotes.Count - broll - zoom - slideshow - hybrid - aiStill;
            var summary = broll + " B-roll · " + zoom + " Zoom · " + slideshow + " Slideshow · "
                          + hybrid + " Hybrid · " + aiStill + " AI ảnh · " + ai + " AI";
            var refPath = (batch.ReferenceImagePath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(refPath) && File.Exists(refPath))
            {
                summary += " · ảnh ref";
            }

            return summary + " · bấm mở";
        }

        public static string DescribeBatchAudioSummary(PhilosophyBatchItem batch)
        {
            if (batch == null)
            {
                return "—";
            }

            var music = string.IsNullOrWhiteSpace(batch.MusicFolder)
                ? "nhạc mặc định"
                : Path.GetFileName(batch.MusicFolder.Trim());
            var ambient = PhilosophyAmbientCatalog.GetLabel(batch.AmbientKey);
            return music + " · " + ambient + " · " + ResolveBatchMusicVolumePercent(batch) + "%";
        }

        public static int ResolveBatchMusicVolumePercent(PhilosophyBatchItem batch) =>
            batch == null || batch.MusicVolumePercent <= 0
                ? DefaultMusicVolumePercent
                : Math.Min(100, batch.MusicVolumePercent);

        public static int ResolveQuoteMusicVolumePercent(PhilosophyScriptItem quote, PhilosophyBatchItem batch)
        {
            if (quote != null && quote.MusicVolumePercent > 0)
            {
                return Math.Min(100, quote.MusicVolumePercent);
            }

            return ResolveBatchMusicVolumePercent(batch);
        }

        public static double ResolveMusicBedLinearVolume(int volumePercent) =>
            MusicBedBaseVolume * Math.Max(0, Math.Min(100, volumePercent)) / 100d;

        public static int ResolveAmbientVolumePercent() => PhilosophyAudioDefaults.DefaultAmbientVolumePercent;

        public static double ResolveAmbientBedLinearVolume(int? volumePercent = null)
        {
            var pct = volumePercent ?? ResolveAmbientVolumePercent();
            return Math.Max(0, Math.Min(100, pct)) / 100d;
        }

        public static int ClampMusicVolumePercent(int volumePercent) =>
            Math.Max(0, Math.Min(100, volumePercent));

        public static int ResolveBatchNarrationSpeedPercent(PhilosophyBatchItem batch) =>
            batch == null || batch.BodyNarrationSpeedPercent <= 0
                ? ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent
                : ShowcaseNarrationSpeedHelper.ClampManualPercent(batch.BodyNarrationSpeedPercent);

        public static int ResolveQuoteNarrationSpeedPercent(PhilosophyScriptItem quote, PhilosophyBatchItem batch)
        {
            if (quote != null && quote.NarrationSpeedPercent > 0)
            {
                return ShowcaseNarrationSpeedHelper.ClampManualPercent(quote.NarrationSpeedPercent);
            }

            return ResolveBatchNarrationSpeedPercent(batch);
        }

        public static int ClampNarrationSpeedPercent(int speedPercent) =>
            ShowcaseNarrationSpeedHelper.ClampManualPercent(speedPercent) > 0
                ? ShowcaseNarrationSpeedHelper.ClampManualPercent(speedPercent)
                : ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;

        public static void EnsureBatchAudioDefaults(PhilosophyBatchItem batch, AppSettings settings = null)
        {
            if (batch == null)
            {
                return;
            }

            PhilosophyAudioDefaults.ApplyToBatch(batch, settings);

            if (batch.Quotes == null)
            {
                return;
            }

            EnsureQuoteAudioDefaults(batch);
        }

        public static string DescribeBatchOutputSummary(PhilosophyBatchItem batch)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return "—";
            }

            var done = batch.Quotes.Count(q =>
                string.Equals(q.Status, "Xong", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(q.OutputPath));
            return done + "/" + batch.Quotes.Count + " xong";
        }

        public static string ComputeBatchStatus(PhilosophyBatchItem batch)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return "Nháp";
            }

            if (batch.Quotes.Any(q => q.Status != null && q.Status.IndexOf("render", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Đang render…";
            }

            if (batch.Quotes.Any(q => q.Status != null && q.Status.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Lỗi";
            }

            if (batch.Quotes.Any(q => q.Status != null && q.Status.IndexOf("dừng", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Dừng";
            }

            var done = batch.Quotes.Count(q =>
                string.Equals(q.Status, "Xong", StringComparison.OrdinalIgnoreCase));
            if (done == batch.Quotes.Count)
            {
                return "Xong";
            }

            if (done > 0)
            {
                return done + "/" + batch.Quotes.Count + " xong";
            }

            return "Nháp";
        }

        public static void ApplyBatchAudioToQuotes(PhilosophyBatchItem batch, bool overwriteAll = true)
        {
            if (batch?.Quotes == null)
            {
                return;
            }

            var musicFolder = batch.MusicFolder ?? string.Empty;
            var ambientKey = PhilosophyAmbientCatalog.NormalizeKey(batch.AmbientKey);
            var volume = ResolveBatchMusicVolumePercent(batch);
            var speed = ResolveBatchNarrationSpeedPercent(batch);

            foreach (var quote in batch.Quotes)
            {
                if (quote == null)
                {
                    continue;
                }

                if (overwriteAll)
                {
                    quote.MusicFolder = musicFolder;
                    quote.AmbientKey = ambientKey;
                    quote.MusicVolumePercent = volume;
                    quote.NarrationSpeedPercent = speed;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(quote.MusicFolder))
                {
                    quote.MusicFolder = musicFolder;
                }

                if (string.IsNullOrWhiteSpace(quote.AmbientKey))
                {
                    quote.AmbientKey = ambientKey;
                }

                if (quote.MusicVolumePercent <= 0)
                {
                    quote.MusicVolumePercent = volume;
                }

                if (quote.NarrationSpeedPercent <= 0)
                {
                    quote.NarrationSpeedPercent = speed;
                }
            }
        }

        /// <summary>Điền quote thiếu field âm thanh từ batch (migrate / quote mới).</summary>
        public static void EnsureQuoteAudioDefaults(PhilosophyBatchItem batch) =>
            ApplyBatchAudioToQuotes(batch, overwriteAll: false);

        /// <summary>Cập nhật nhãn batch từ quote đầu tiên — lưới quote là nguồn nhạc/ambient per-row.</summary>
        public static void SyncBatchAudioSummaryFromQuotes(PhilosophyBatchItem batch)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return;
            }

            var first = batch.Quotes.FirstOrDefault(q => q != null);
            if (first == null)
            {
                return;
            }

            batch.MusicFolder = first.MusicFolder ?? string.Empty;
            batch.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(first.AmbientKey);
        }

        /// <summary>Profile + nhãn legacy — giữ styling phụ đề per-quote (Gemini / popup Phụ đề).</summary>
        public static void ClearQuoteBatchOwnedFields(PhilosophyScriptItem quote)
        {
            if (quote == null)
            {
                return;
            }

            quote.ProfileName = string.Empty;
            quote.SubtitleStyleLabel = string.Empty;
        }

        public static void NormalizeBatchQuoteOwnership(PhilosophyBatchItem batch)
        {
            if (batch?.Quotes == null)
            {
                return;
            }

            foreach (var quote in batch.Quotes)
            {
                ClearQuoteBatchOwnedFields(quote);
            }
        }

        [Obsolete("Phụ đề lấy từ batch lúc render — gọi ClearQuoteBatchOwnedFields / NormalizeBatchQuoteOwnership.")]
        public static void ApplyBatchSubtitleToQuotes(PhilosophyBatchItem batch) =>
            NormalizeBatchQuoteOwnership(batch);

        public static void CopySubtitleTemplateFromQuote(PhilosophyBatchItem batch, PhilosophyScriptItem template)
        {
            if (batch == null || template == null)
            {
                return;
            }

            PhilosophySubtitleStyleHelper.EnsureDefaults(template);
            batch.SubtitleStyleLabel = template.SubtitleStyleLabel ?? string.Empty;
            batch.SubtitlePosition = template.SubtitlePosition ?? string.Empty;
            batch.SubtitleFontName = template.SubtitleFontName ?? string.Empty;
            batch.SubtitleFontSize = template.SubtitleFontSize;
            batch.SubtitleAnimation = template.SubtitleAnimation ?? string.Empty;
            batch.SubtitleBold = template.SubtitleBold;
            batch.SubtitleItalic = template.SubtitleItalic;
            batch.SubtitleWordsPerLine = template.SubtitleWordsPerLine;
            batch.SubtitlePrimaryColourAss = template.SubtitlePrimaryColourAss ?? string.Empty;
            batch.SubtitleSecondaryColourAss = template.SubtitleSecondaryColourAss ?? string.Empty;
            batch.SubtitleEnabled = template.SubtitleEnabled;
            batch.SubtitleLookPreset = template.SubtitleLookPreset ?? string.Empty;
            batch.SubtitleDecorPreset = template.SubtitleDecorPreset ?? string.Empty;
            batch.SubtitleHighlightColourAss = template.SubtitleHighlightColourAss ?? string.Empty;
            batch.SubtitleDisplayQuote = template.SubtitleDisplayQuote ?? string.Empty;
            batch.SubtitleDisplayAnimation = template.SubtitleDisplayAnimation ?? string.Empty;
            if (string.IsNullOrWhiteSpace(batch.SubtitleAnimation)
                && !string.IsNullOrWhiteSpace(template.SubtitleDisplayAnimation))
            {
                batch.SubtitleAnimation = template.SubtitleDisplayAnimation;
            }
        }

        public static string SuggestBRollFolder(string profileName, string topic, string mood)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var moodKey = (mood ?? "reflective").Trim().ToLowerInvariant();
            var topicKey = Slugify(topic);

            var sharedRoot = ProfileScopedPaths.GetSharedBackgroundsDirectory();
            foreach (var candidate in new[]
                     {
                         Path.Combine(sharedRoot, topicKey),
                         Path.Combine(sharedRoot, moodKey),
                         Path.Combine(sharedRoot, "Nature", moodKey),
                         sharedRoot,
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(profile), "broll", topicKey),
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(profile), "broll", moodKey),
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(profile), "broll")
                     })
            {
                if (Directory.Exists(candidate) && DirectoryContainsVideo(candidate))
                {
                    return candidate;
                }
            }

            return PhilosophyBRollSelection.RandomToken;
        }

        /// <summary>Ánh xạ tên file nhạc Gemini gợi ý → tên file trong kho nhạc.</summary>
        public static string ResolveGeminiMusicFileName(AppSettings settings, string suggestedFileName, string mood)
        {
            var suggestion = (suggestedFileName ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(suggestion) || OmniAudioLibrary.IsNoneId(suggestion))
            {
                return string.Empty;
            }

            suggestion = Path.GetFileName(suggestion);
            var names = PhilosophyProfileAssets.EnumerateMusicFileNames(null, settings);
            if (names.Count == 0)
            {
                return string.Empty;
            }

            var exact = names.FirstOrDefault(n =>
                string.Equals(n, suggestion, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact))
            {
                return exact;
            }

            var stem = Path.GetFileNameWithoutExtension(suggestion) ?? string.Empty;
            exact = names.FirstOrDefault(n =>
                string.Equals(Path.GetFileNameWithoutExtension(n), stem, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact))
            {
                return exact;
            }

            var partial = names.FirstOrDefault(n =>
                n.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrEmpty(stem)
                    && stem.IndexOf(Path.GetFileNameWithoutExtension(n) ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase) >= 0));
            if (!string.IsNullOrEmpty(partial))
            {
                return partial;
            }

            var moodPath = PhilosophyProfileAssets.TryPickMusicFile(null, mood);
            if (!string.IsNullOrWhiteSpace(moodPath))
            {
                return Path.GetFileName(moodPath);
            }

            return names[0];
        }

        /// <summary>Ánh xạ edge_style Gemini → HookStyleCatalog key.</summary>
        public static string ResolveGeminiEdgeStyle(string suggestedStyleKey, string mood)
        {
            var raw = (suggestedStyleKey ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(raw))
            {
                var exact = HookStyleCatalog.AllStyleKeys.FirstOrDefault(k =>
                    string.Equals(k, raw, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exact))
                {
                    return ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(exact);
                }

                if (raw.Contains("chuyen") || raw.Contains("story") || raw.Contains("ke_chuyen"))
                {
                    return HookStyleCatalog.StyleKechuyen;
                }

                if (raw.Contains("noi") || raw.Contains("pain") || raw.Contains("dau"))
                {
                    return HookStyleCatalog.StyleNoidau;
                }

                if (raw.Contains("fomo"))
                {
                    return HookStyleCatalog.StyleFomo;
                }

                if (raw.Contains("huong") || raw.Contains("guide"))
                {
                    return HookStyleCatalog.StyleHuongdan;
                }

                if (raw.Contains("boc") || raw.Contains("phot"))
                {
                    return HookStyleCatalog.StyleBocphot;
                }
            }

            switch ((mood ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "melancholic":
                    return HookStyleCatalog.StyleNoidau;
                case "hopeful":
                    return HookStyleCatalog.StyleHuongdan;
                case "intense":
                    return HookStyleCatalog.StyleBocphot;
                case "calm":
                    return HookStyleCatalog.StyleKechuyen;
                default:
                    return HookStyleCatalog.StyleKechuyen;
            }
        }

        /// <summary>Ánh xạ tên file SFX Gemini gợi ý → tên file trong kho Assets\Audio\Sfx.</summary>
        public static string ResolveGeminiSfxFileName(AppSettings settings, string suggestedFileName, string mood)
        {
            var suggestion = (suggestedFileName ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(suggestion) || OmniAudioLibrary.IsNoneId(suggestion))
            {
                return string.Empty;
            }

            suggestion = Path.GetFileName(suggestion);
            var names = OmniAudioLibrary.ListSfxFileNames(settings);
            if (names.Count == 0)
            {
                return string.Empty;
            }

            var exact = names.FirstOrDefault(n =>
                string.Equals(n, suggestion, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact))
            {
                return exact;
            }

            var stem = Path.GetFileNameWithoutExtension(suggestion) ?? string.Empty;
            exact = names.FirstOrDefault(n =>
                string.Equals(Path.GetFileNameWithoutExtension(n), stem, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact))
            {
                return exact;
            }

            var partial = names.FirstOrDefault(n =>
                n.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrEmpty(stem)
                    && stem.IndexOf(Path.GetFileNameWithoutExtension(n) ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase) >= 0));
            if (!string.IsNullOrEmpty(partial))
            {
                return partial;
            }

            var moodHint = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var moodMatch = names.FirstOrDefault(n =>
                n.IndexOf(moodHint, StringComparison.OrdinalIgnoreCase) >= 0);
            return moodMatch ?? string.Empty;
        }

        public static string BuildAiVeoPrompt(PhilosophyScriptItem quote, string profileName, string topic, AppSettings settings = null)
        {
            var content = (quote?.Content ?? string.Empty).Trim();
            var mood = (quote?.Mood ?? "reflective").Trim();
            var motion = (quote?.MotionPrompt ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(motion))
            {
                return motion;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var mascot = PhilosophyGeminiBackgroundContext.BuildMascotContext(profile, settings);
            var prefix = PhilosophyGeminiBackgroundContext.BuildMascotVeoPromptPrefix(mascot);

            return prefix + "Topic: «"
                   + TrimGridLabel(topic, 80, "quote") + "». Mood: " + mood + ". Quote context: «"
                   + TrimGridLabel(content, 120, "") + "». Subtle character motion, no on-screen text, atmospheric lighting matching mood.";
        }

        public static List<PhilosophyBatchItem> MigrateLegacyScripts(
            IList<PhilosophyScriptItem> scripts,
            string topic,
            int minDuration,
            int maxDuration)
        {
            if (scripts == null || scripts.Count == 0)
            {
                return new List<PhilosophyBatchItem>();
            }

            var batch = new PhilosophyBatchItem
            {
                Topic = string.IsNullOrWhiteSpace(topic) ? "Draft cũ" : topic.Trim(),
                MinDurationSeconds = minDuration,
                MaxDurationSeconds = maxDuration,
                Quotes = scripts.Where(s => s != null).ToList()
            };

            if (batch.Quotes.Count > 0)
            {
                batch.ProfileName = batch.Quotes[0].ProfileName ?? string.Empty;
                CopySubtitleTemplateFromQuote(batch, batch.Quotes[0]);
                batch.MusicFolder = batch.Quotes[0].MusicFolder ?? string.Empty;
                batch.AmbientKey = batch.Quotes[0].AmbientKey ?? string.Empty;
            }

            batch.RefreshDerivedFields();
            NormalizeBatchQuoteOwnership(batch);
            return new List<PhilosophyBatchItem> { batch };
        }

        public static string ResolveBatchProfileName(
            PhilosophyBatchItem batch,
            string toolbarFallback = null)
        {
            var batchProfile = (batch?.ProfileName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(batchProfile))
            {
                return ProfileScopedPaths.ResolveProfileName(batchProfile);
            }

            var fallback = (toolbarFallback ?? string.Empty).Trim();
            return string.IsNullOrEmpty(fallback)
                ? ProfileScopedPaths.ResolveProfileName(string.Empty)
                : ProfileScopedPaths.ResolveProfileName(fallback);
        }

        /// <summary>Gán profile batch từ toolbar khi cột Profile trống.</summary>
        public static void EnsureBatchProfileName(PhilosophyBatchItem batch, string toolbarFallback)
        {
            if (batch == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(batch.ProfileName))
            {
                batch.ProfileName = ResolveBatchProfileName(null, toolbarFallback);
                return;
            }

            batch.ProfileName = ProfileScopedPaths.ResolveProfileName(batch.ProfileName);
        }

        public static string ResolveQuoteProfileName(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            string toolbarFallback = null)
        {
            if (batch != null)
            {
                return ResolveBatchProfileName(batch, toolbarFallback);
            }

            var legacy = (quote?.ProfileName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(legacy))
            {
                return ProfileScopedPaths.ResolveProfileName(legacy);
            }

            return ResolveBatchProfileName(null, toolbarFallback);
        }

        /// <summary>Ảnh tham chiếu batch (popup Nội dung) hoặc legacy trên quote (mode 3).</summary>
        public static string ResolveReferenceImagePath(PhilosophyBatchItem batch, PhilosophyScriptItem quote)
        {
            var batchRef = (batch?.ReferenceImagePath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(batchRef) && File.Exists(batchRef))
            {
                return batchRef;
            }

            if (quote == null)
            {
                return string.Empty;
            }

            var visualMode = PhilosophyVisualModes.Normalize(quote.VisualMode);
            if (visualMode != PhilosophyVisualModes.PreRendered)
            {
                return string.Empty;
            }

            var quoteRef = (quote.BRollFolder ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(quoteRef) || !File.Exists(quoteRef))
            {
                return string.Empty;
            }

            return PhilosophyBRollSelection.IsImageFile(quoteRef) ? quoteRef : string.Empty;
        }

        public static string ResolveBatchOutputFolder(PhilosophyBatchItem batch)
        {
            var paths = batch?.Quotes?
                .Select(q => q?.OutputPath)
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Select(Path.GetDirectoryName)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (paths.Count == 1)
            {
                return paths[0];
            }

            if (batch != null && !string.IsNullOrWhiteSpace(batch.ProfileName))
            {
                return PhilosophyVideoPipelineService.GetProfileOutputDirectory(batch.ProfileName);
            }

            return PhilosophyVideoPipelineService.GetOutputRootDirectory();
        }

        private static bool DirectoryContainsVideo(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return false;
            }

            return Directory.GetFiles(dir, "*.mp4", SearchOption.TopDirectoryOnly).Length > 0
                   || Directory.GetFiles(dir, "*.mov", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static string Slugify(string text)
        {
            var t = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (t.Length == 0)
            {
                return "general";
            }

            var chars = t.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            var slug = new string(chars);
            while (slug.Contains("--"))
            {
                slug = slug.Replace("--", "-");
            }

            return slug.Trim('-');
        }

        public static void EnsureBatchDefaults(PhilosophyBatchItem batch)
        {
            if (batch == null)
            {
                return;
            }

            MigrateLegacyLogoSettings(batch);
            batch.ContentTemplateId = PhilosophyContentTemplatePresets.NormalizeId(batch.ContentTemplateId);
            batch.BrandLogoPositionId = ShowcaseBrandLogoPositionCatalog.ResolveId(batch.BrandLogoPositionId);
            if (batch.BrandLogoScaleWidthPercent <= 0)
            {
                batch.BrandLogoScaleWidthPercent = ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent;
            }

            batch.BrandLogoMarginX = ShowcaseBrandOverlayHelper.ClampMargin(batch.BrandLogoMarginX);
            batch.BrandLogoMarginY = ShowcaseBrandOverlayHelper.ClampMargin(batch.BrandLogoMarginY);
            if (batch.BrandLogoOpacityPercent <= 0)
            {
                batch.BrandLogoOpacityPercent = ShowcaseBrandOverlayHelper.DefaultOpacityPercent;
            }
        }

        private static void MigrateLegacyLogoSettings(PhilosophyBatchItem batch)
        {
            if (batch == null)
            {
                return;
            }

            var unset = !batch.BrandLogoEnabled
                        && string.IsNullOrWhiteSpace(batch.BrandLogoFile)
                        && string.IsNullOrWhiteSpace(batch.BrandLogoPositionId)
                        && batch.BrandLogoScaleWidthPercent <= 0
                        && batch.BrandLogoMarginX == 0
                        && batch.BrandLogoMarginY == 0
                        && batch.BrandLogoOpacityPercent <= 0;
            if (!unset)
            {
                return;
            }

            batch.BrandLogoEnabled = true;
            batch.BrandLogoPositionId = ShowcaseBrandLogoPositionCatalog.BottomRight;
            batch.BrandLogoScaleWidthPercent = ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent;
            batch.BrandLogoMarginX = ShowcaseBrandOverlayHelper.DefaultMargin;
            batch.BrandLogoMarginY = ShowcaseBrandOverlayHelper.DefaultMargin;
            batch.BrandLogoOpacityPercent = ShowcaseBrandOverlayHelper.DefaultOpacityPercent;
        }

        public static string DescribeBatchLogoSummary(PhilosophyBatchItem batch)
        {
            if (batch == null)
            {
                return "—";
            }

            EnsureBatchDefaults(batch);
            return ShowcaseBrandOverlayHelper.FormatSummary(
                batch.BrandLogoEnabled,
                batch.BrandLogoFile,
                batch.ProfileName,
                batch.BrandLogoPositionId,
                batch.BrandLogoScaleWidthPercent);
        }

        public static void CopyLogoSettingsToRenderOptions(PhilosophyBatchItem batch, PhilosophyRenderOptions options)
        {
            if (batch == null || options == null)
            {
                return;
            }

            EnsureBatchDefaults(batch);
            options.BrandLogoEnabled = batch.BrandLogoEnabled;
            options.BrandLogoFile = batch.BrandLogoFile ?? string.Empty;
            options.BrandLogoPositionId = batch.BrandLogoPositionId ?? string.Empty;
            options.BrandLogoScaleWidthPercent = batch.BrandLogoScaleWidthPercent;
            options.BrandLogoMarginX = batch.BrandLogoMarginX;
            options.BrandLogoMarginY = batch.BrandLogoMarginY;
            options.BrandLogoOpacityPercent = batch.BrandLogoOpacityPercent;
        }

        public static ShowcaseVideoItem CreateLogoEditorStub(PhilosophyBatchItem batch)
        {
            if (batch == null)
            {
                return new ShowcaseVideoItem();
            }

            EnsureBatchDefaults(batch);
            return new ShowcaseVideoItem
            {
                ProfileName = batch.ProfileName ?? string.Empty,
                ProductName = batch.Topic ?? string.Empty,
                ShowcaseBrandLogoEnabled = batch.BrandLogoEnabled,
                ShowcaseBrandLogoFile = batch.BrandLogoFile ?? string.Empty,
                ShowcaseBrandLogoPositionId = batch.BrandLogoPositionId ?? string.Empty,
                ShowcaseBrandLogoScaleWidthPercent = batch.BrandLogoScaleWidthPercent,
                ShowcaseBrandLogoMarginX = batch.BrandLogoMarginX,
                ShowcaseBrandLogoMarginY = batch.BrandLogoMarginY,
                ShowcaseBrandLogoOpacityPercent = batch.BrandLogoOpacityPercent
            };
        }

        public static void ApplyLogoEditorStub(PhilosophyBatchItem batch, ShowcaseVideoItem stub)
        {
            if (batch == null || stub == null)
            {
                return;
            }

            batch.BrandLogoEnabled = stub.ShowcaseBrandLogoEnabled;
            batch.BrandLogoFile = stub.ShowcaseBrandLogoFile ?? string.Empty;
            batch.BrandLogoPositionId = stub.ShowcaseBrandLogoPositionId ?? string.Empty;
            batch.BrandLogoScaleWidthPercent = stub.ShowcaseBrandLogoScaleWidthPercent;
            batch.BrandLogoMarginX = stub.ShowcaseBrandLogoMarginX;
            batch.BrandLogoMarginY = stub.ShowcaseBrandLogoMarginY;
            batch.BrandLogoOpacityPercent = stub.ShowcaseBrandLogoOpacityPercent;
            EnsureBatchDefaults(batch);
        }

        internal static ShowcaseBrandLogoRenderPlan BuildLogoRenderPlan(
            PhilosophyRenderOptions renderOptions,
            string profileName,
            AppSettings settings,
            int canvasWidth = 1080)
        {
            if (renderOptions == null || !renderOptions.BrandLogoEnabled)
            {
                return new ShowcaseBrandLogoRenderPlan { CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080 };
            }

            var path = ShowcaseBrandOverlayHelper.ResolveEffectiveLogoPath(
                renderOptions.BrandLogoFile,
                profileName,
                settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                return new ShowcaseBrandLogoRenderPlan { CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080 };
            }

            return new ShowcaseBrandLogoRenderPlan
            {
                Enabled = true,
                LogoPath = path,
                PositionId = ShowcaseBrandLogoPositionCatalog.ResolveId(renderOptions.BrandLogoPositionId),
                ScaleWidthPercent = ShowcaseBrandOverlayHelper.ClampScaleWidthPercent(
                    renderOptions.BrandLogoScaleWidthPercent > 0
                        ? renderOptions.BrandLogoScaleWidthPercent
                        : ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent),
                MarginX = ShowcaseBrandOverlayHelper.ClampMargin(renderOptions.BrandLogoMarginX),
                MarginY = ShowcaseBrandOverlayHelper.ClampMargin(renderOptions.BrandLogoMarginY),
                OpacityPercent = ShowcaseBrandOverlayHelper.ClampOpacityPercent(
                    renderOptions.BrandLogoOpacityPercent > 0
                        ? renderOptions.BrandLogoOpacityPercent
                        : ShowcaseBrandOverlayHelper.DefaultOpacityPercent),
                CanvasWidth = canvasWidth > 0 ? canvasWidth : 1080
            };
        }
    }
}
