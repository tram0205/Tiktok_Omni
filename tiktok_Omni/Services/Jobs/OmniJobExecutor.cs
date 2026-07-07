using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Affiliate;

namespace tiktok_Omni.Services.Jobs
{
    public sealed class OmniJobExecutor
    {
        private readonly AffiliateHunter _affiliateHunter;
        private readonly VideoProcessingService _videoProcessingService;
        private readonly SocialAutomation _socialAutomation;
        private readonly ConfigManager _configManager;
        private readonly DuplicateGuardManager _duplicateGuardManager;
        private readonly PhilosophyVideoService _philosophyVideoService;
        private readonly GlobalJobQueue _jobQueue;
        private readonly HuntHistoryStore _huntHistoryStore;
        private readonly Func<HuntAffiliateJobPayload, List<AffiliateCandidate>, CancellationToken, Task<List<AffiliateCandidate>>> _rankAffiliateBatch;
        private readonly Func<OmniJob, VideoReupJobPayload, IJobUiBridge, CancellationToken, Task> _executeVideoReup;
        private readonly RevenueDrivenService _revenueDrivenService = new RevenueDrivenService();

        public OmniJobExecutor(
            AffiliateHunter affiliateHunter,
            VideoProcessingService videoProcessingService,
            SocialAutomation socialAutomation,
            ConfigManager configManager,
            DuplicateGuardManager duplicateGuardManager,
            PhilosophyVideoService philosophyVideoService,
            GlobalJobQueue jobQueue,
            HuntHistoryStore huntHistoryStore,
            Func<HuntAffiliateJobPayload, List<AffiliateCandidate>, CancellationToken, Task<List<AffiliateCandidate>>> rankAffiliateBatch = null,
            Func<OmniJob, VideoReupJobPayload, IJobUiBridge, CancellationToken, Task> executeVideoReup = null)
        {
            _affiliateHunter = affiliateHunter ?? throw new ArgumentNullException(nameof(affiliateHunter));
            _videoProcessingService = videoProcessingService ?? throw new ArgumentNullException(nameof(videoProcessingService));
            _socialAutomation = socialAutomation ?? throw new ArgumentNullException(nameof(socialAutomation));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _duplicateGuardManager = duplicateGuardManager ?? throw new ArgumentNullException(nameof(duplicateGuardManager));
            _philosophyVideoService = philosophyVideoService ?? throw new ArgumentNullException(nameof(philosophyVideoService));
            _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
            _huntHistoryStore = huntHistoryStore ?? throw new ArgumentNullException(nameof(huntHistoryStore));
            _rankAffiliateBatch = rankAffiliateBatch;
            _executeVideoReup = executeVideoReup;
        }

        public async Task ExecuteAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            switch (job.Kind)
            {
                case OmniJobKind.HuntAffiliate:
                    await ExecuteHuntAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.RenderVideo:
                    await ExecuteRenderAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.AutoPost:
                    await ExecuteAutoPostAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.PhilosophyVideo:
                    await ExecutePhilosophyAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.MascotStory:
                    await ExecuteMascotStoryAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.AffiliateDeepDive:
                    await ExecuteAffiliateDeepDiveAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.VideoReup:
                    await ExecuteVideoReupAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                case OmniJobKind.AffiliateDeepRender:
                    await ExecuteAffiliateDeepRenderAsync(job, ui, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException("Unsupported job kind: " + job.Kind);
            }
        }

        private async Task ExecuteHuntAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<HuntAffiliateJobPayload>(job.PayloadJson ?? "{}")
                            ?? new HuntAffiliateJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var entries = ResolveHuntKeywordEntries(payload);
            foreach (var entry in entries)
            {
                ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, entry.ProfileName);
            }

            var mode = (payload.SearchMode ?? string.Empty).IndexOf("Shop", StringComparison.OrdinalIgnoreCase) >= 0
                ? AffiliateSearchMode.Shop
                : AffiliateSearchMode.Video;

            var platforms = payload.Platforms != null && payload.Platforms.Count > 0
                ? payload.Platforms
                : new List<string> { AffiliateSourceIds.TikTok };

            var allResults = new List<AffiliateCandidate>();
            var rankTikTokVideo = payload.RankByEngagement
                && mode == AffiliateSearchMode.Video
                && platforms.Any(p => string.Equals(p, AffiliateSourceIds.TikTok, StringComparison.OrdinalIgnoreCase));

            for (var i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = entries[i];
                var kw = entry.Keyword;
                var profile = entry.ProfileName;
                ui.Log($"Đã nhận diện từ khóa [{kw}] cho nick [{profile}]");
                ui.OnHuntKeywordStarted(i, entries.Count, kw, profile);

                await _revenueDrivenService.LoadAsync().ConfigureAwait(false);
                var revenueMult = _revenueDrivenService.GetHuntBufferMultiplier(kw);
                var collectLimit = ComputeCollectLimit(
                    payload.MaxResultsPerKeyword,
                    payload.BufferMultiplier * revenueMult,
                    rankTikTokVideo,
                    mode);

                var results = await _affiliateHunter.HuntAsync(
                    kw,
                    collectLimit,
                    platforms,
                    mode,
                    cancellationToken,
                    ui.Log,
                    _configManager,
                    profile).ConfigureAwait(false);

                var batch = results ?? new List<AffiliateCandidate>();
                foreach (var c in batch)
                {
                    if (c != null)
                    {
                        c.ProfileName = profile;
                        await _revenueDrivenService.RecordCandidateAsync(c).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(c.SourceKeyword))
                        {
                            c.SourceKeyword = kw;
                        }
                    }
                }

                var huntWindow = TimeSpan.FromDays(7);
                var freshBatch = new List<AffiliateCandidate>();
                var skipped = 0;
                foreach (var c in batch)
                {
                    if (c == null)
                    {
                        continue;
                    }

                    var url = (c.VideoUrl ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(url) && _huntHistoryStore.ContainsRecent(url, huntWindow))
                    {
                        skipped++;
                        continue;
                    }

                    freshBatch.Add(c);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        _huntHistoryStore.Record(url, profile, kw);
                    }
                }

                if (skipped > 0)
                {
                    ui.Log($"[Hunt] Bỏ qua {skipped} URL đã quét trong 7 ngày (hunt_history).");
                }

                batch = freshBatch;
                if (batch.Count == 0 && skipped > 0 && (results?.Count ?? 0) > 0)
                {
                    batch = results;
                    ui.Log($"[Hunt] Toàn bộ kết quả mới trùng lịch sử 7 ngày. Tự động hiển thị lại {batch.Count} video đã có trong lịch sử lên lưới để bạn sử dụng.");
                }

                if (rankTikTokVideo && batch.Count > 0 && _rankAffiliateBatch != null)
                {
                    var tikTokBatch = batch
                        .Where(c => c != null && string.Equals(c.SourcePlatform, AffiliateSourceIds.TikTok, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (tikTokBatch.Count > 0)
                    {
                        var ranked = await _rankAffiliateBatch(payload, tikTokBatch, cancellationToken).ConfigureAwait(false);
                        batch = MergeRankedTikTokIntoBatch(batch, ranked);
                        foreach (var c in batch.Where(x => x != null))
                        {
                            c.ProfileName = profile;
                        }

                        ui.Log($"[Hunt] Đã xếp hạng engagement — giữ {batch.Count} video view/tương tác cao nhất cho «{kw}».");
                    }
                }

                MergeHuntResults(allResults, batch, kw);
                ui.OnHuntKeywordCompleted(i, entries.Count, kw, profile, batch);
            }

            ui.OnHuntFinished(true, $"Hunt xong — {allResults.Count} dòng từ {entries.Count} từ khoá.");
        }

        private static List<HuntKeywordEntry> ResolveHuntKeywordEntries(HuntAffiliateJobPayload payload)
        {
            if (payload?.KeywordEntries != null && payload.KeywordEntries.Count > 0)
            {
                return payload.KeywordEntries;
            }

            var fallbackProfile = ProfileScopedPaths.ResolveProfileName(payload?.ProfileName);
            var list = new List<HuntKeywordEntry>();
            foreach (var raw in payload?.Keywords ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var keyword = raw.Trim();
                list.Add(new HuntKeywordEntry
                {
                    RawInput = keyword,
                    Keyword = keyword,
                    ProfileName = fallbackProfile
                });
            }

            if (list.Count == 0)
            {
                list.Add(new HuntKeywordEntry
                {
                    RawInput = string.Empty,
                    Keyword = string.Empty,
                    ProfileName = fallbackProfile
                });
            }

            return list;
        }

        private async Task ExecuteRenderAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<RenderVideoJobPayload>(job.PayloadJson ?? "{}")
                            ?? new RenderVideoJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var renderProfiles = (payload.Products ?? new List<AiVideoGenInputItem>())
                .Select(p => ProfileScopedPaths.ResolveProfileName(
                    string.IsNullOrWhiteSpace(p?.ProfileName) ? payload.ProfileName : p.ProfileName))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var rp in renderProfiles)
            {
                ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, rp);
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            settings.VideoTransitionDurationSeconds = payload.TransitionSeconds;
            settings.VideoTextSize = payload.TextSize;
            settings.VideoMusicVolume = payload.MusicVolume;
            await _configManager.SaveAsync(settings).ConfigureAwait(false);

            var perItemScripts = payload.PerItemScripts;
            var hasPerItem = perItemScripts != null &&
                               perItemScripts.Count == payload.Products?.Count &&
                               perItemScripts.All(x => !string.IsNullOrWhiteSpace(x));

            ui.Log("[Job] Render video — nick: «" + string.Join(", ", renderProfiles) + "»…");
            var outputPaths = await _videoProcessingService.GenerateProductVideosAsync(
                payload.Products ?? new List<AiVideoGenInputItem>(),
                payload.SharedScript ?? string.Empty,
                hasPerItem ? perItemScripts : null,
                settings,
                payload.ProfileName,
                ui.Log,
                ui.OnRenderProgress,
                cancellationToken,
                payload.UseMultiVoiceNarration).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(payload.RenderFingerprint))
            {
                await _duplicateGuardManager.AddAsync(new DuplicateGuardRecord
                {
                    Type = "render",
                    Fingerprint = payload.RenderFingerprint,
                    Profile = payload.ProfileName,
                    Summary = "batch:" + outputPaths.Count
                }).ConfigureAwait(false);
            }

            ui.OnRenderFinished(true, outputPaths, string.Empty);
        }

        private async Task ExecuteAutoPostAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<AutoPostJobPayload>(job.PayloadJson ?? "{}")
                            ?? new AutoPostJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var profile = ProfileScopedPaths.ResolveProfileName(payload.Profile);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, profile);

            if (!string.IsNullOrWhiteSpace(payload.VideoFolder))
            {
                Directory.CreateDirectory(payload.VideoFolder);
            }

            ProfileScopedPaths.ValidateAutoPostPathsForProfile(
                payload.StorageRootPath,
                profile,
                payload.VideoFolder,
                payload.VideoFilePath);

            var summary = await _socialAutomation.RunOmnichannelAutoPostAsync(
                payload,
                cancellationToken,
                ui.Log).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(payload.PostFingerprint))
            {
                await _duplicateGuardManager.AddAsync(new DuplicateGuardRecord
                {
                    Type = "post",
                    Fingerprint = payload.PostFingerprint,
                    Profile = profile,
                    Summary = payload.CombinedCaptionPreview
                }).ConfigureAwait(false);
            }

            ui.OnAutoPostFinished(true, summary, string.Empty);
        }

        private async Task ExecutePhilosophyAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<PhilosophyVideoJobPayload>(job.PayloadJson ?? "{}")
                            ?? new PhilosophyVideoJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var nick = ProfileScopedPaths.ResolveProfileName(
                string.IsNullOrWhiteSpace(payload.ProfileName) ? job.ProfileName : payload.ProfileName);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, nick);

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var profile = PhilosophyProfileAssets.ResolveProfile(settings, nick);
            if (!string.IsNullOrWhiteSpace(payload.VoiceId))
            {
                profile.VoiceId = payload.VoiceId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(payload.VideoStyle))
            {
                profile.VideoStyle = payload.VideoStyle.Trim();
            }

            var quoteInput = (payload.QuoteText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(quoteInput))
            {
                throw new InvalidOperationException("Quote trống.");
            }

            ui.Log("[Job] Triết lý/Quote — nick «" + nick + "»: " +
                   (quoteInput.Length > 60 ? quoteInput.Substring(0, 60) + "…" : quoteInput));

            try
            {
                var result = await _philosophyVideoService.GenerateAsync(
                    quoteInput,
                    settings,
                    profile,
                    ui.Log,
                    (status, pct) => ui.OnPhilosophyProgress(status, pct),
                    cancellationToken).ConfigureAwait(false);

                result.ProfileName = nick;
                ui.OnPhilosophyFinished(true, result, string.Empty);
            }
            catch (Exception ex)
            {
                ui.OnPhilosophyFinished(false, null, ex.Message);
                throw;
            }
        }

        private async Task ExecuteMascotStoryAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<MascotStoryJobPayload>(job.PayloadJson ?? "{}")
                            ?? new MascotStoryJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var nick = ProfileScopedPaths.ResolveProfileName(
                string.IsNullOrWhiteSpace(payload.ProfileName) ? job.ProfileName : payload.ProfileName);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, nick);

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var identityPaths = payload.IdentityImagePaths?
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .ToList() ?? new List<string>();

            ui.Log("[Job] Mascot Story — nick «" + nick + "» | theme: " + payload.ChannelTheme);

            Action<string> onProcessing = phase =>
            {
                MascotWorker.MarkOmniJobProcessing(_jobQueue, job, phase);
            };

            try
            {
                var lipPack = new AvatarIdentityPackConfig
                {
                    MouthClosedPath = payload.MouthClosedPath ?? string.Empty,
                    MouthOpenSmallPath = payload.MouthOpenSmallPath ?? string.Empty,
                    MouthOpenPath = payload.MouthOpenPath ?? string.Empty,
                    MouthOverlayX = payload.MouthOverlayX,
                    MouthOverlayY = payload.MouthOverlayY,
                    MouthOverlayScale = payload.MouthOverlayScale
                };

                MascotChannelVideoPipelineResult result;
                var useEmotional = payload.UseEmotionalRemix;
                ui.Log(useEmotional
                    ? "[Job] Pipeline: EmotionalRemix (Gemini → ElevenLabs WAV → Hook SFX → Lip-sync → Ducking)"
                    : "[Job] Pipeline: Mascot channel legacy (multi-scene)");

                if (useEmotional)
                {
                    onProcessing("EmotionalRemix");
                    ui.OnMascotProgress(5, "EmotionalRemix: bắt đầu");

                    Action<string> emotionalLog = msg =>
                    {
                        ui.Log(msg);
                        if (string.IsNullOrEmpty(msg))
                        {
                            return;
                        }

                        if (msg.IndexOf("1/6", StringComparison.OrdinalIgnoreCase) >= 0
                            || msg.IndexOf("Gemini", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ui.OnMascotProgress(15, "Gemini — kịch bản");
                        }
                        else if (msg.IndexOf("2/6", StringComparison.OrdinalIgnoreCase) >= 0
                                 || msg.IndexOf("ElevenLabs", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ui.OnMascotProgress(35, "ElevenLabs — giọng");
                        }
                        else if (msg.IndexOf("3/6", StringComparison.OrdinalIgnoreCase) >= 0
                                 || msg.IndexOf("Visual Hook", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ui.OnMascotProgress(50, "Hook SFX 3s");
                        }
                        else if (msg.IndexOf("Lip-Sync", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ui.OnMascotProgress(70, "Lip-sync");
                        }
                        else if (msg.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0
                                 || msg.IndexOf("Ducking", StringComparison.OrdinalIgnoreCase) >= 0
                                 || msg.IndexOf("[EmotionalRemix] Ducking", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ui.OnMascotProgress(85, "Hậu kỳ âm thanh");
                        }
                        else if (msg.IndexOf("Hoàn tất", StringComparison.OrdinalIgnoreCase) >= 0
                                 || msg.IndexOf("✓", StringComparison.Ordinal) >= 0)
                        {
                            ui.OnMascotProgress(95, "Hoàn tất");
                        }
                    };

                    using (var remix = new EmotionalRemixService())
                    {
                        var finalPath = await remix.RenderMascotStoryAsync(
                            payload,
                            settings,
                            emotionalLog,
                            cancellationToken).ConfigureAwait(false);
                        result = new MascotChannelVideoPipelineResult
                        {
                            FinalVideoPath = finalPath,
                            ChannelTheme = payload.ChannelTheme,
                            ProfileName = nick,
                            Scenes = new List<MascotSceneAsset>()
                        };
                    }
                }
                else
                {
                    result = await _videoProcessingService.GenerateMascotChannelVideoAsync(
                        payload.MascotImagePath,
                        payload.ChannelTheme,
                        identityPaths,
                        payload.SceneCount,
                        settings,
                        nick,
                        ui.Log,
                        cancellationToken,
                        (pct, stage) => ui.OnMascotProgress(pct, stage),
                        payload.MascotStyle,
                        scripts => ui.OnMascotSceneScriptsReady(scripts),
                        onProcessing,
                        payload.UseLipSync,
                        lipPack).ConfigureAwait(false);
                    result.ProfileName = nick;
                }

                ui.OnMascotProgress(100, "Xong");
                ui.OnMascotFinished(true, result, string.Empty);
            }
            catch (Exception ex)
            {
                ui.OnMascotFinished(false, null, ex.Message);
                throw;
            }
        }

        private async Task ExecuteAffiliateDeepRenderAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<AffiliateDeepRenderJobPayload>(job.PayloadJson ?? "{}")
                            ?? new AffiliateDeepRenderJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var profile = ProfileScopedPaths.ResolveProfileName(payload.ProfileName);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, profile);

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var products = payload.Products ?? new List<AiVideoGenInputItem>();
            if (products.Count < 4)
            {
                throw new InvalidOperationException("Affiliate Deep render cần ít nhất 4 ảnh cùng sản phẩm.");
            }

            ui.Log("[Job] Affiliate Deep render — «" + profile + "»: " + (payload.ProductName ?? string.Empty));

            try
            {
                var result = await _videoProcessingService.GenerateAffiliateProductVideoAsync(
                    products,
                    settings,
                    profile,
                    ui.Log,
                    cancellationToken,
                    (percent, stage) =>
                    {
                        var mapped = ProductionPipeline.MapRenderStageToStatus(stage, percent);
                        ui.Log("[Deep Render] " + mapped + " " + percent + "% — " + stage);
                    },
                    payload.Category,
                    payload.StorageRootPath,
                    payload.UseMultiVoiceNarration).ConfigureAwait(false);

                var output = result?.FinalVideoPath ?? string.Empty;
                ui.OnAffiliateDeepRenderFinished(job.Id, true, output, string.Empty);
            }
            catch (Exception ex)
            {
                ui.OnAffiliateDeepRenderFinished(job.Id, false, string.Empty, ex.Message);
                throw;
            }
        }

        private async Task ExecuteVideoReupAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            if (_executeVideoReup == null)
            {
                throw new InvalidOperationException("Video reup job handler chưa được gắn (Form1).");
            }

            var payload = JsonConvert.DeserializeObject<VideoReupJobPayload>(job.PayloadJson ?? "{}")
                            ?? new VideoReupJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            ui.Log("[Job] Video reup — «" + ProfileScopedPaths.ResolveProfileName(job.ProfileName) + "»…");
            await _executeVideoReup(job, payload, ui, cancellationToken).ConfigureAwait(false);
        }

        private async Task ExecuteAffiliateDeepDiveAsync(OmniJob job, IJobUiBridge ui, CancellationToken cancellationToken)
        {
            var payload = JsonConvert.DeserializeObject<AffiliateDeepDiveJobPayload>(job.PayloadJson ?? "{}")
                            ?? new AffiliateDeepDiveJobPayload();
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath);
            var nick = ProfileScopedPaths.ResolveProfileName(payload.ProfileName);

            if (payload.SafetyScore <= payload.MinSafetyThreshold)
            {
                throw new InvalidOperationException(
                    $"SafetyScore {payload.SafetyScore} không vượt ngưỡng {payload.MinSafetyThreshold}.");
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(payload.YtDlpPath))
            {
                settings.YtDlpPath = payload.YtDlpPath;
            }

            if (!string.IsNullOrWhiteSpace(payload.FfmpegPath))
            {
                settings.FfmpegPath = payload.FfmpegPath;
            }

            var context = new AffiliateCandidate
            {
                VideoUrl = payload.VideoUrl,
                ProfileName = nick,
                SourceKeyword = payload.SourceKeyword,
                Category = payload.Category,
                ProductName = payload.ProductName,
                SafetyScore = payload.SafetyScore
            };

            ui.Log("[Job] Deep Dive — «" + nick + "» score " + payload.SafetyScore + ": " + payload.VideoUrl);

            try
            {
                var result = await _affiliateHunter.AnalyzeVideoContentAsync(
                    payload.VideoUrl,
                    settings,
                    ui.Log,
                    cancellationToken,
                    context,
                    payload.StorageRootPath).ConfigureAwait(false);

                ui.OnAffiliateDeepDiveFinished(job.Id, true, context, result, string.Empty);
            }
            catch (Exception ex)
            {
                ui.OnAffiliateDeepDiveFinished(job.Id, false, context, null, ex.Message);
                throw;
            }
        }

        private static List<AffiliateCandidate> MergeRankedTikTokIntoBatch(
            List<AffiliateCandidate> batch,
            List<AffiliateCandidate> rankedTikTok)
        {
            if (batch == null || batch.Count == 0)
            {
                return rankedTikTok ?? new List<AffiliateCandidate>();
            }

            if (rankedTikTok == null || rankedTikTok.Count == 0)
            {
                return batch;
            }

            var others = batch
                .Where(c => c == null || !string.Equals(c.SourcePlatform, AffiliateSourceIds.TikTok, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var merged = new List<AffiliateCandidate>(rankedTikTok.Count + others.Count);
            merged.AddRange(rankedTikTok);
            merged.AddRange(others.Where(c => c != null));
            return merged;
        }

        private static int ComputeCollectLimit(int desired, double bufferMult, bool rankByEngagement, AffiliateSearchMode mode)
        {
            if (mode != AffiliateSearchMode.Video || !rankByEngagement)
            {
                return Math.Max(1, desired);
            }

            var mult = bufferMult < 1.5d ? 1.5d : bufferMult;
            if (mult > 5.0d)
            {
                mult = 5.0d;
            }

            return Math.Max(desired, (int)Math.Ceiling(desired * mult));
        }

        private static void MergeHuntResults(List<AffiliateCandidate> all, List<AffiliateCandidate> batch, string keyword)
        {
            if (batch == null || batch.Count == 0)
            {
                return;
            }

            foreach (var item in batch)
            {
                if (item == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.SourceKeyword))
                {
                    item.SourceKeyword = keyword;
                }

                all.Add(item);
            }
        }
    }
}
