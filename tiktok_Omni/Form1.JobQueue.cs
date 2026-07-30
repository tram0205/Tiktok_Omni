using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1 : IJobUiBridge
    {
        private GlobalJobQueue _globalJobQueue;
        private JobWorkerService _jobWorkerService;
        private OmniJobExecutor _jobExecutor;
        private OmniJob _activeHuntJob;
        private OmniJob _activeRenderJob;
        private OmniJob _activeAutoPostJob;
        private void InitializeJobQueueInfrastructure()
        {
            _globalJobQueue = new GlobalJobQueue();
            _globalJobQueue.JobStateChanged += GlobalJobQueue_JobStateChanged;

            _jobExecutor = new OmniJobExecutor(
                _affiliateHunter,
                _videoProcessingService,
                _socialAutomation,
                _configManager,
                _duplicateGuardManager,
                _philosophyVideoService,
                _globalJobQueue,
                _huntHistoryStore,
                RankAffiliateBatchForJobAsync,
                (job, payload, ui, ct) => ExecuteVideoReupJobForWorkerAsync(job, payload, ui, ct));

            _jobWorkerService = new JobWorkerService(
                _globalJobQueue,
                _jobExecutor,
                () =>
                {
                    var s = _configManager.LoadAsync().GetAwaiter().GetResult();
                    return s?.MaxConcurrentJobs ?? 2;
                },
                this);

            _jobWorkerService.Start();
        }

        private void GlobalJobQueue_JobStateChanged(object sender, OmniJob job)
        {
            if (job == null || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => GlobalJobQueue_JobStateChanged(sender, job)));
                return;
            }

            var pending = _globalJobQueue.PendingCount;
            var running = _globalJobQueue.RunningCount;
            if (tslStatusMain != null)
            {
                tslStatusMain.Text = $"Jobs: {running} chạy, {pending} chờ";
            }

            // ── Schedule-entry status sync ────────────────────────────────────────
            // Note: the InvokeRequired guard at the top of this method already
            // ensures we are on the UI thread (via BeginInvoke). The explicit
            // Invoke call below adds belt-and-suspenders safety for any future
            // code path that might call this method directly from a worker thread.
            if (_scheduleJobMap.TryGetValue(job.Id, out var scheduleSlot))
            {
                var (entry, dgv) = scheduleSlot;
                bool   isFinal   = false;
                string newStatus = entry.Status;
                string newError  = entry.LastError;

                switch (job.Status)
                {
                    case OmniJobStatus.Running:
                        newStatus = "Running";
                        newError  = string.Empty;
                        break;
                    case OmniJobStatus.RetryPending:
                        newStatus = "Đang thử lại…";
                        break;
                    case OmniJobStatus.Completed:
                        newStatus = "Done";
                        newError  = string.Empty;
                        _scheduleJobMap.TryRemove(job.Id, out _);
                        isFinal   = true;
                        break;
                    case OmniJobStatus.Failed:
                        newStatus = "Failed";
                        newError  = job.LastError;
                        _scheduleJobMap.TryRemove(job.Id, out _);
                        isFinal   = true;
                        break;
                    case OmniJobStatus.Cancelled:
                        newStatus = "Pending";
                        newError  = string.Empty;
                        _scheduleJobMap.TryRemove(job.Id, out _);
                        isFinal   = true;
                        break;
                }

                void ApplyToGrid()
                {
                    entry.Status    = newStatus;
                    entry.LastError = newError;
                    dgv?.Refresh();
                }
                if (dgv?.InvokeRequired ?? false)
                    dgv.Invoke((Action)ApplyToGrid);
                else
                    ApplyToGrid();

                if (isFinal)
                {
                    var remaining = Interlocked.Decrement(ref _pendingScheduleJobCount);
                    if (remaining <= 0 && _isAutoPostScheduleQueueRunning)
                    {
                        _isAutoPostScheduleQueueRunning = false;
                        RefreshAutoPostScheduleStatus();
                        LogAutoPost("[Lịch đăng] ✓ Tất cả job đã hoàn tất.");
                    }
                }
            }

            if (job.Status == OmniJobStatus.Failed || job.Status == OmniJobStatus.Cancelled)
            {
                if (ReferenceEquals(job, _activeHuntJob))
                {
                    FinishHuntJobUi(false, job.LastError);
                }
                else if (ReferenceEquals(job, _activeRenderJob))
                {
                    FinishRenderJobUi(false, job.LastError);
                }
                else if (ReferenceEquals(job, _activeAutoPostJob))
                {
                    FinishAutoPostJobUi(false, job.LastError);
                }
                else if (job.Kind == OmniJobKind.PhilosophyVideo)
                {
                    LogPhilosophy("[JobQueue] Triết lý job thất bại: " + job.LastError);
                    SetPhilosophyProgress("lỗi — xem log", 0);
                }
                else if (job.Kind == OmniJobKind.MascotStory)
                {
                    Log("[JobQueue] Mascot Story thất bại: " + job.LastError);
                }
                else if (job.Kind == OmniJobKind.AffiliateDeepDive)
                {
                    Log("[JobQueue] Deep Dive thất bại: " + job.LastError);
                    RefreshAffiliateToolbarButtons();
                }
                else if (job.Kind == OmniJobKind.VideoReup)
                {
                    LogVideoReup("[JobQueue] Video reup thất bại: " + job.LastError);
                    _videoReupBatchJobIds.Remove(job.Id);
                }
                else if (job.Kind == OmniJobKind.AffiliateDeepRender)
                {
                    LogShowcase("[JobQueue] Affiliate Deep render thất bại: " + job.LastError);
                    if (!HasActiveShowcaseRenderJobs())
                    {
                        _activeAffiliateDeepRenderJobId = null;
                    }

                    TryGetAffiliateDeepRenderContext(job.Id, out _, out var video);
                    var scenes = video != null
                        ? GetShowcaseVideoScenes(video)
                        : new List<AiVideoGenInputItem>();
                    ApplyShowcaseRenderFailure(scenes, video);
                    UpdateShowcaseRenderButtonState();
                    RecordProductionError(OneClickPipelineService.PipelineAffiliateDeep);
                    RecordApiFailureFromMessage(job.LastError);
                }
                else if (job.Kind == OmniJobKind.PhilosophyVideo)
                {
                    RecordProductionError(OneClickPipelineService.PipelinePhilosophy);
                    RecordApiFailureFromMessage(job.LastError);
                }
                else if (job.Kind == OmniJobKind.MascotStory)
                {
                    RecordProductionError(OneClickPipelineService.PipelineMascot);
                    RecordApiFailureFromMessage(job.LastError);
                }
                else if (job.Kind == OmniJobKind.VideoReup)
                {
                    RecordProductionError(OneClickPipelineService.PipelineVideoReup);
                    RecordApiFailureFromMessage(job.LastError);
                }
            }
        }

        public void OnVideoReupJobFinished(
            Guid jobId,
            bool success,
            string videoUrl,
            VideoReupRemixResult result,
            string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnVideoReupJobFinished(jobId, success, videoUrl, result, error)));
                return;
            }

            if (jobId != Guid.Empty)
            {
                _videoReupBatchJobIds.Remove(jobId);
            }

            var url = (videoUrl ?? string.Empty).Trim();
            if (success && result != null && !string.IsNullOrWhiteSpace(result.OutputPath))
            {
                LogVideoReup("[JobQueue] Video reup xong: " + url);
                LogVideoReup("→ " + result.OutputPath);
                var row = _videoReupBindingList?.FirstOrDefault(r =>
                    string.Equals((r?.VideoUrl ?? string.Empty).Trim(), url, StringComparison.OrdinalIgnoreCase));
                var profile = ProfileScopedPaths.ResolveProfileName(row?.ProfileName);
                _ = EnqueueProductionApprovalAsync(
                    ApprovalJobType.VideoReup,
                    profile,
                    row?.ProductName ?? "Video Reup",
                    result.OutputPath,
                    88,
                    string.Empty,
                    new { OutputPath = result.OutputPath, VideoUrl = url, ProfileName = profile },
                    row?.ReupHookDraft);
            }
            else if (!success && !string.IsNullOrWhiteSpace(error))
            {
                LogVideoReup("[JobQueue] Video reup lỗi: " + error);
                RecordProductionError(OneClickPipelineService.PipelineVideoReup);
                if (error.IndexOf("gemini", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    RecordApiHealthAlert("Gemini", error);
                }
                else if (error.IndexOf("tts", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    RecordApiHealthAlert("TTS", error);
                }
            }

            var pendingReup = _globalJobQueue?.AllJobs.Count(j =>
                j.Kind == OmniJobKind.VideoReup &&
                (j.Status == OmniJobStatus.Pending || j.Status == OmniJobStatus.Running ||
                 j.Status == OmniJobStatus.Processing || j.Status == OmniJobStatus.RetryPending)) ?? 0;

            if (_videoReupBatchJobIds.Count == 0 && pendingReup == 0)
            {
                if (!_affiliateAutoEnrichRunning && _huntCancellation == null && !_affiliateDownloadingBatch)
                {
                    btnStopHunt.Enabled = false;
                }
            }

            _videoReupBindingList?.ResetBindings();
        }

        private async Task<List<AffiliateCandidate>> RankAffiliateBatchForJobAsync(
            HuntAffiliateJobPayload payload,
            List<AffiliateCandidate> batch,
            CancellationToken cancellationToken)
        {
            return await RankAffiliateBatchByEngagementAsync(batch, payload.MaxResultsPerKeyword, cancellationToken)
                .ConfigureAwait(false);
        }

        void IJobUiBridge.Log(string message)
        {
            Log(message);
            if (IsShowcaseLogMessage(message))
            {
                AppendToShowcaseLogPanel(message);
            }

            // Mirror job-level messages (from TikTokAutomation, SocialAutomation, etc.) to the
            // AutoPost tab log panel whenever a schedule-originated job is actively running.
            if (_scheduleJobMap.Count > 0 || _isAutoPostScheduleQueueRunning)
                AppendToAutoPostLog(message);
        }

        public void OnHuntKeywordStarted(int index, int total, string keyword, string profileName)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnHuntKeywordStarted(index, total, keyword, profileName)));
                return;
            }

            if (btnHuntAffiliates != null)
            {
                var nick = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
                btnHuntAffiliates.Text = $"⏳ Quét {index + 1}/{total}: «{keyword}» @ {nick}…";
                ResizeAffiliateToolbarButton(btnHuntAffiliates);
            }
        }

        public void OnHuntKeywordCompleted(int index, int total, string keyword, string profileName, List<AffiliateCandidate> batch)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnHuntKeywordCompleted(index, total, keyword, profileName, batch)));
                return;
            }

            var huntBatch = batch ?? new List<AffiliateCandidate>();
            SortAffiliateCandidatesByViewsDescending(huntBatch);
            MergeAffiliateHuntResultsIntoAll(_affiliateAllResults, huntBatch, keyword);
            SortAffiliateCandidatesByViewsDescending(_affiliateAllResults);
            FlushAffiliateDraftToDisk();
            RefreshAffiliateGridByQualityFilter();
            _affiliateBindingList?.ResetBindings();
            dgvAffiliateResults?.Invalidate();
            RecomputeAffiliateDeepDiveErrorColumnVisibility();
            SaveAffiliateHuntResultsToDisk();

            var withMetrics = (batch ?? new List<AffiliateCandidate>())
                .Count(c => c != null && c.MetricsCapturedAtUtc != DateTime.MinValue);
            var batchCount = batch?.Count ?? 0;
            if (batchCount > 0 && withMetrics < batchCount)
            {
                Log($"[Affiliate] {withMetrics}/{batchCount} dòng đã có số liệu — đang lấy view/likes qua TikWM…");
                StartAffiliateAutoEnrichIfEnabled(CancellationToken.None);
            }
        }

        public void OnHuntFinished(bool success, string summary)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnHuntFinished(success, summary)));
                return;
            }

            FinishHuntJobUi(success, success ? string.Empty : summary);
            if (success)
            {
                if ((_affiliateBindingList == null || _affiliateBindingList.Count == 0)
                    && _affiliateAllResults != null
                    && _affiliateAllResults.Count > 0
                    && _lastHuntKeywordEntries != null
                    && _lastHuntKeywordEntries.Count > 0)
                {
                    ApplyProfileScope(_lastHuntKeywordEntries[0].ProfileName);
                }

                Log(summary);
                StartAffiliateAutoEnrichIfEnabled(CancellationToken.None);
                StartAffiliateAutoCategorizeIfEnabled(CancellationToken.None);
                _ = PruneRevenueDeadJobsAsync();
                if (_industrialBatchPhase == IndustrialBatchPhase.Hunt)
                {
                    _ = ContinueIndustrialBatchAfterHuntAsync();
                }
            }
            else if (_industrialBatchPhase == IndustrialBatchPhase.Hunt)
            {
                _industrialBatchPhase = IndustrialBatchPhase.None;
            }
        }

        private void StartAffiliateAutoCategorizeIfEnabled(CancellationToken parentToken)
        {
            if (_affiliateAllResults == null || _affiliateAllResults.Count == 0)
            {
                return;
            }

            _affiliateCategorizeCts?.Cancel();
            _affiliateCategorizeCts?.Dispose();
            _affiliateCategorizeCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            var token = _affiliateCategorizeCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var settings = await _configManager.LoadAsync().ConfigureAwait(false);
                    var snapshot = _affiliateAllResults.Where(c => c != null).ToList();
                    await _affiliateCategoryService
                        .CategorizeBatchAsync(snapshot, settings, Log, token)
                        .ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        BeginInvoke(new Action(OnAffiliateCategoriesUpdated));
                    }
                }
                catch (OperationCanceledException)
                {
                    Log("[Category] Đã hủy phân loại nền.");
                }
                catch (Exception ex)
                {
                    Log("[Category] Lỗi: " + ex.Message);
                }
            }, token);
        }

        public void OnAffiliateCategoriesUpdated()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnAffiliateCategoriesUpdated));
                return;
            }

            RefreshAffiliateGridByQualityFilter();
            _affiliateBindingList?.ResetBindings();
            SaveAffiliateHuntResultsToDisk();
            Log("[Category] Đã cập nhật cột Ngách trên lưới.");
        }

        public void OnAffiliateDeepDiveFinished(
            Guid jobId,
            bool success,
            AffiliateCandidate context,
            VideoDeepAnalysisResult result,
            string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnAffiliateDeepDiveFinished(jobId, success, context, result, error)));
                return;
            }

            var url = (context?.VideoUrl ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                _affiliateDeepDiveJobByVideoUrl.Remove(url);
            }

            var target = FindAffiliateCandidateByVideoUrl(url);
            if (target == null && context != null)
            {
                target = context;
            }

            if (success && target != null && result != null)
            {
                ApplyDeepDiveResultToCandidate(target, result);
                target.LastDeepDiveError = string.Empty;
                Log("[DeepDive] Job xong: " + url);
                _ = EnsureAffiliateCategoryAfterDeepDiveAsync(target);
            }
            else if (target != null)
            {
                target.LastDeepDiveError = error ?? "Lỗi";
                Log("[DeepDive] Job lỗi: " + error);
            }

            _affiliateBindingList?.ResetBindings();
            RecomputeAffiliateDeepDiveErrorColumnVisibility();
            RefreshAffiliateToolbarButtons();
            SaveAffiliateHuntResultsToDisk();
        }

        private AffiliateCandidate FindAffiliateCandidateByVideoUrl(string videoUrl)
        {
            var url = (videoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            return (_affiliateAllResults ?? new List<AffiliateCandidate>())
                .FirstOrDefault(c => string.Equals((c?.VideoUrl ?? string.Empty).Trim(), url, StringComparison.OrdinalIgnoreCase));
        }

        private void EnqueueAffiliateDeepDiveJob(AffiliateCandidate candidate, AppSettings settings, int minSafety, bool highPriority)
        {
            var url = (candidate?.VideoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (candidate.SafetyScore <= minSafety)
            {
                Log($"[DeepDive] Bỏ qua (score {candidate.SafetyScore} ≤ {minSafety}): {url}");
                return;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(candidate.ProfileName);
            var cts = new CancellationTokenSource();
            var job = new OmniJob
            {
                Kind = OmniJobKind.AffiliateDeepDive,
                Title = "Deep Dive — " + profile,
                ProfileName = profile,
                Priority = highPriority ? 50 : 0,
                PayloadJson = JsonConvert.SerializeObject(new AffiliateDeepDiveJobPayload
                {
                    VideoUrl = url,
                    ProfileName = profile,
                    SourceKeyword = candidate.SourceKeyword ?? string.Empty,
                    Category = candidate.Category ?? string.Empty,
                    ProductName = candidate.ProductName ?? string.Empty,
                    SafetyScore = candidate.SafetyScore,
                    MinSafetyThreshold = minSafety,
                    StorageRootPath = settings?.StorageRootPath ?? string.Empty,
                    YtDlpPath = settings?.YtDlpPath ?? string.Empty,
                    FfmpegPath = settings?.FfmpegPath ?? string.Empty
                }),
                MaxRetries = 1,
                Tag = cts
            };

            _affiliateDeepDiveJobByVideoUrl[url] = job.Id;
            _globalJobQueue.Enqueue(job);
            Log("[JobQueue] Deep Dive đã xếp hàng — " + url + (highPriority ? " (ưu tiên)" : string.Empty));
        }

        public void CancelAffiliateDeepDiveJobForUrl(string videoUrl)
        {
            var url = (videoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (_affiliateDeepDiveJobByVideoUrl.TryGetValue(url, out var id))
            {
                _globalJobQueue.TryCancel(id);
                _affiliateDeepDiveJobByVideoUrl.Remove(url);
                Log("[JobQueue] Đã hủy Deep Dive job: " + url);
            }
        }


        private void FinishHuntJobUi(bool success, string error)
        {
            _activeHuntJob = null;
            if (btnHuntAffiliates != null)
            {
                btnHuntAffiliates.Enabled = true;
                btnHuntAffiliates.Text = "Quét Affiliate";
                ResizeAffiliateToolbarButton(btnHuntAffiliates);
            }

            btnExportAffiliateCsv.Enabled = _affiliateBindingList != null && _affiliateBindingList.Count > 0;
            btnPushToAiVideoGen.Enabled = btnExportAffiliateCsv.Enabled;
            btnStopHunt.Enabled = false;
            _huntCancellation = null;
            DisposeActiveJobCancellation();
            if (!success && !string.IsNullOrWhiteSpace(error))
            {
                Log("[JobQueue] Hunt thất bại: " + error);
            }
        }

        public void OnRenderProgress(VideoRenderProgress progress)
        {
            UpdateAiRenderProgress(progress);
            if (progress != null && _activeRenderJob != null)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateAiVideoGenItemProgress(progress.VideoIndex - 1, progress)));
                }
                else
                {
                    UpdateAiVideoGenItemProgress(progress.VideoIndex - 1, progress);
                }
            }

            if (progress == null || !progress.IsCompleted || _activeRenderJob == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnRenderProgress(progress)));
                return;
            }

            TryMarkSlideshowItemProcessedFromRenderJob(progress.VideoIndex - 1);
        }

        public void OnRenderFinished(bool success, List<string> outputPaths, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnRenderFinished(success, outputPaths, error)));
                return;
            }

            if (success)
            {
                MarkAllSlideshowItemsProcessedFromActiveRenderJob();
            }

            FinishRenderJobUi(success, error);
            if (success && outputPaths != null && outputPaths.Count > 0)
            {
                Log($"[JobQueue] Render xong {outputPaths.Count} video.");
                foreach (var path in outputPaths)
                {
                    Log("AI Video Gen: output -> " + path);
                }

                var products = TryGetActiveRenderJobProducts();
                var script = txtAiVideoGenPrompt?.Text?.Trim() ?? string.Empty;
                _ = AfterRenderOutputsApprovalAsync(
                    products,
                    outputPaths,
                    script,
                    ApprovalJobType.Slideshow);

                FlushSlideshowDraftToDisk();
            }
            else if (!success)
            {
                Log("[JobQueue] Render thất bại: " + error);
            }
        }

        private void TryMarkSlideshowItemProcessedFromRenderJob(int productIndex)
        {
            var products = TryGetActiveRenderJobProducts();
            if (products == null || productIndex < 0 || productIndex >= products.Count)
            {
                return;
            }

            MarkAiVideoGenItemsProcessed(new[] { products[productIndex] });
        }

        private void MarkAllSlideshowItemsProcessedFromActiveRenderJob()
        {
            var products = TryGetActiveRenderJobProducts();
            if (products == null || products.Count == 0)
            {
                return;
            }

            MarkAiVideoGenItemsProcessed(products);
        }

        private List<AiVideoGenInputItem> TryGetActiveRenderJobProducts()
        {
            if (_activeRenderJob == null || string.IsNullOrWhiteSpace(_activeRenderJob.PayloadJson))
            {
                return null;
            }

            try
            {
                var payload = JsonConvert.DeserializeObject<RenderVideoJobPayload>(_activeRenderJob.PayloadJson)
                              ?? new RenderVideoJobPayload();
                return payload.Products;
            }
            catch
            {
                return null;
            }
        }

        private void FinishRenderJobUi(bool success, string error)
        {
            _activeRenderJob = null;
            btnProcessVideo.Enabled = true;
            btnGenerateGeminiPrompt.Enabled = true;
            _aiVideoGenCancellation?.Dispose();
            _aiVideoGenCancellation = null;
        }

        public void OnAutoPostFinished(bool success, string summary, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnAutoPostFinished(success, summary, error)));
                return;
            }

            FinishAutoPostJobUi(success, error);
            if (success)
            {
                Log("[JobQueue] Auto Post: " + summary);
                MessageBox.Show(this, summary, "Đăng đa kênh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("[JobQueue] Auto Post thất bại: " + error);
                MessageBox.Show(this, error, "Đăng đa kênh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void FinishAutoPostJobUi(bool success, string error)
        {
            _activeAutoPostJob = null;
            btnStartAutoPost.Enabled = true;
            _autoPostCancellation?.Dispose();
            _autoPostCancellation = null;
        }

        private void EnqueueHuntJob(HuntAffiliateJobPayload payload)
        {
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, payload.ProfileName);
            var job = new OmniJob
            {
                Kind = OmniJobKind.HuntAffiliate,
                Title = "Săn Affiliate (" + (payload.Keywords?.Count ?? 0) + " từ khoá)",
                ProfileName = payload.ProfileName,
                PayloadJson = JsonConvert.SerializeObject(payload),
                MaxRetries = 2
            };
            _activeHuntJob = job;
            _globalJobQueue.Enqueue(job);
            Log("[JobQueue] Đã xếp hàng săn Affiliate — " + _globalJobQueue.PendingCount + " job chờ.");
        }

        private void EnqueueRenderJob(RenderVideoJobPayload payload)
        {
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, payload.ProfileName);
            var job = new OmniJob
            {
                Kind = OmniJobKind.RenderVideo,
                Title = "Render AI (" + (payload.Products?.Count ?? 0) + " SP)",
                ProfileName = payload.ProfileName,
                PayloadJson = JsonConvert.SerializeObject(payload),
                MaxRetries = 1
            };
            OmniJobMetadataHelper.ApplyAffiliateFields(job, payload.AffiliateLink, payload.ProductId);
            _activeRenderJob = job;
            _globalJobQueue.Enqueue(job);
            Log("[JobQueue] Đã xếp hàng render — " + _globalJobQueue.PendingCount + " job chờ.");
        }

        private void EnqueueAutoPostJob(AutoPostJobPayload payload)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(payload.Profile);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, profile);
            var job = new OmniJob
            {
                Kind = OmniJobKind.AutoPost,
                Title = "Auto Post đa kênh",
                ProfileName = profile,
                PayloadJson = JsonConvert.SerializeObject(payload),
                MaxRetries = 2
            };
            OmniJobMetadataHelper.ApplyAffiliateFields(job, payload.AffiliateLink, payload.ProductId);
            _activeAutoPostJob = job;
            _globalJobQueue.Enqueue(job);
            Log("[JobQueue] Đã xếp hàng Auto Post — " + _globalJobQueue.PendingCount + " job chờ.");
        }

        public void OnPhilosophyProgress(string statusText, int percent)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnPhilosophyProgress(statusText, percent)));
                return;
            }

            SetPhilosophyProgress(statusText, percent);
            var status = ProductionPipeline.MapPhilosophyStatus(statusText, percent);
            foreach (var row in _philosophyJobRows.Values.Where(r => r.PipelineStatus != "Xong"))
            {
                row.PipelineStatus = status;
                row.ProgressPercent = percent;
            }

            _philosophyQueueBindingList?.ResetBindings();
        }

        public async void OnPhilosophyFinished(bool success, PhilosophyVideoResult result, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnPhilosophyFinished(success, result, error)));
                return;
            }

            if (success && result != null && !string.IsNullOrWhiteSpace(result.OutputPath))
            {
                var profile = ProfileScopedPaths.ResolveProfileName(result.ProfileName);
                LogPhilosophy($"Triết lý: xong → {result.OutputPath} (≈{result.DurationSeconds:0.##}s) @ {profile}.");
                LogPhilosophy("Quote: " + result.Quote);
                SetPhilosophyProgress("Xong — đã đưa vào hàng duyệt", 100);

                var preview = (result.Quote ?? string.Empty).Trim();
                if (preview.Length > 120)
                {
                    preview = preview.Substring(0, 120) + "…";
                }

                var quoteRisk = _safetyScoreService.ScoreRender(result.Quote ?? string.Empty, new List<AiVideoGenInputItem>(), profile);
                await EnqueueProductionApprovalAsync(
                    ApprovalJobType.PhilosophyVideo,
                    profile,
                    "Triết lý/Quote — " + profile,
                    result.OutputPath,
                    quoteRisk.Score,
                    string.Join("; ", quoteRisk.Reasons),
                    new
                    {
                        OutputPath = result.OutputPath,
                        Quote = result.Quote ?? string.Empty,
                        ProfileName = profile,
                        DurationSeconds = result.DurationSeconds,
                        Mood = result.Mood ?? string.Empty,
                        ScheduledPostUtc = DateTime.UtcNow.AddHours(2)
                    },
                    preview).ConfigureAwait(true);

                if (_philosophyJobRows.Values.FirstOrDefault(r =>
                        string.Equals(r.OutputVideoPath, result.OutputPath, StringComparison.OrdinalIgnoreCase)) is ProductionQueueRowItem phRow)
                {
                    phRow.PipelineStatus = "Xong";
                    phRow.SafetyScore = quoteRisk.Score;
                    phRow.OutputVideoPath = result.OutputPath;
                    phRow.ThumbnailPath = ProductionPipeline.ResolveThumbnailPath(result.OutputPath);
                    _philosophyQueueBindingList?.ResetBindings();
                }

                LogPhilosophy("[APPROVAL] Đã đưa video vào hàng duyệt (Pending) — nick «" + profile + "».");

                LogPhilosophy("[APPROVAL] Video trong hàng duyệt — AutoRun nếu score ≥ 85 và bật AutoRunApprovedQueue.");
            }
            else if (!success)
            {
                LogPhilosophy("[JobQueue] Triết lý thất bại: " + error);
                SetPhilosophyProgress("lỗi — xem log", 0);
            }
        }

        public void OnMascotSceneScriptsReady(List<string> sceneScripts)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMascotSceneScriptsReady(sceneScripts)));
                return;
            }

            _mascotPreviewSceneScripts = (sceneScripts ?? new List<string>()).ToList();
            Log("[Mascot] Đã tạo kịch bản " + _mascotPreviewSceneScripts.Count + " cảnh (Gemini).");
        }

        public void OnMascotProgress(int percent, string stage)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMascotProgress(percent, stage)));
                return;
            }

            UpdateSinglePipelineProgress(percent, stage ?? "Mascot");
            var status = ProductionPipeline.MapMascotStageToStatus(stage, percent);
            foreach (var row in _mascotJobRows.Values.Where(r => r.PipelineStatus != "Xong"))
            {
                row.PipelineStatus = status;
                row.ProgressPercent = percent;
            }

            _mascotQueueBindingList?.ResetBindings();
        }

        public async void OnMascotFinished(bool success, MascotChannelVideoPipelineResult result, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnMascotFinished(success, result, error)));
                return;
            }

            if (success && result != null && !string.IsNullOrWhiteSpace(result.FinalVideoPath))
            {
                var profile = ProfileScopedPaths.ResolveProfileName(result.ProfileName);
                Log("Mascot Story Pipeline final -> " + result.FinalVideoPath);
                foreach (var scene in result.Scenes ?? new List<MascotSceneAsset>())
                {
                    Log($"Scene {scene.Index}: {scene.SceneVideoPath}");
                }

                UpdateSinglePipelineProgress(100, "Xong — hàng duyệt");

                var themePreview = (result.ChannelTheme ?? string.Empty).Trim();
                if (themePreview.Length > 80)
                {
                    themePreview = themePreview.Substring(0, 80) + "…";
                }

                var mascotRisk = _safetyScoreService.ScoreRender(result.ChannelTheme ?? string.Empty, new List<AiVideoGenInputItem>(), profile);
                await EnqueueProductionApprovalAsync(
                    ApprovalJobType.Mascot,
                    profile,
                    "Mascot Story — " + profile,
                    result.FinalVideoPath,
                    mascotRisk.Score,
                    string.Join("; ", mascotRisk.Reasons),
                    new RenderApprovalPayload
                    {
                        Profile = profile,
                        ProfileName = profile,
                        Script = themePreview,
                        OutputVideoPath = result.FinalVideoPath,
                        ChannelTheme = result.ChannelTheme ?? string.Empty,
                        IsMascotStory = true
                    },
                    themePreview).ConfigureAwait(true);

                foreach (var row in _mascotJobRows.Values.Where(r => r.PipelineStatus != "Xong"))
                {
                    row.PipelineStatus = "Xong";
                    row.SafetyScore = mascotRisk.Score;
                    row.OutputVideoPath = result.FinalVideoPath;
                    row.ThumbnailPath = ProductionPipeline.ResolveThumbnailPath(result.FinalVideoPath);
                }

                _mascotQueueBindingList?.ResetBindings();
                Log("[APPROVAL] Mascot Story → hàng duyệt (Pending) nick «" + profile + "».");

                Log("[APPROVAL] Mascot trong hàng duyệt — AutoRun nếu score ≥ 85 và bật AutoRunApprovedQueue.");
            }
            else if (!success)
            {
                Log("[JobQueue] Mascot Story thất bại: " + error);
                UpdateSinglePipelineProgress(0, "Lỗi");
            }
        }

        private void EnqueueMascotStoryJob(MascotStoryJobPayload payload)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(payload.ProfileName);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(payload.StorageRootPath, profile);
            payload.ProfileName = profile;

            var job = new OmniJob
            {
                Kind = OmniJobKind.MascotStory,
                Title = "Mascot Story — " + profile,
                ProfileName = profile,
                PayloadJson = JsonConvert.SerializeObject(payload),
                MaxRetries = 1
            };

            _globalJobQueue.Enqueue(job);
            TrackMascotJob(job.Id, payload);
            Log("[JobQueue] Đã xếp hàng Mascot Story — " + _globalJobQueue.PendingCount + " job chờ.");
        }

        public void OnAffiliateDeepRenderFinished(Guid jobId, bool success, string outputPath, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnAffiliateDeepRenderFinished(jobId, success, outputPath, error)));
                return;
            }

            if (!HasActiveShowcaseRenderJobs())
            {
                _activeAffiliateDeepRenderJobId = null;
            }

            UpdateShowcaseRenderButtonState();
            if (!HasActiveShowcaseRenderJobs())
            {
                btnProcessVideo.Enabled = true;
                btnGenerateGeminiPrompt.Enabled = true;
                btnReviewScriptBeforeRender.Enabled = true;
            }

            TryGetAffiliateDeepRenderContext(jobId, out var payload, out var video);
            var scenes = video != null
                ? GetShowcaseVideoScenes(video)
                : payload?.Products ?? new List<AiVideoGenInputItem>();

            if (success && !string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
            {
                LogShowcase("[JobQueue] Showcase render xong → " + outputPath);
                MarkShowcaseRenderComplete(scenes, video, outputPath);

                var script = BuildShowcaseApprovalScriptText(video, scenes);
                if (string.IsNullOrWhiteSpace(script))
                {
                    script = txtAiVideoGenPrompt?.Text?.Trim() ?? string.Empty;
                }

                _ = AfterRenderOutputsApprovalAsync(
                    scenes,
                    new List<string> { outputPath },
                    script,
                    ApprovalJobType.AffiliateDeep);

                var active = GetActiveShowcaseVideo();
                if (video == null || active == null || active.VideoId == video.VideoId)
                {
                    LoadProductionVideoPreview(
                        outputPath,
                        ProductionPipeline.ResolveThumbnailPath(outputPath),
                        video?.ProductName ?? scenes.FirstOrDefault()?.ProductName);
                }

                SyncBuffersToGrids();
                NotifyShowcaseDraftDirty();
            }
            else
            {
                if (!success)
                {
                    LogShowcase("[JobQueue] Showcase render lỗi: " + error);
                }

                ApplyShowcaseRenderFailure(scenes, video);
            }
        }

        private bool TryGetAffiliateDeepRenderContext(
            Guid jobId,
            out AffiliateDeepRenderJobPayload payload,
            out ShowcaseVideoItem video)
        {
            payload = null;
            video = null;
            if (jobId == Guid.Empty || _globalJobQueue == null || !_globalJobQueue.TryGet(jobId, out var job) || job == null)
            {
                return false;
            }

            payload = JsonConvert.DeserializeObject<AffiliateDeepRenderJobPayload>(job.PayloadJson ?? "{}")
                      ?? new AffiliateDeepRenderJobPayload();
            if (payload.ShowcaseVideoId != Guid.Empty)
            {
                video = FindShowcaseVideoById(payload.ShowcaseVideoId);
            }

            if (video == null && !string.IsNullOrWhiteSpace(payload.ProductName))
            {
                var name = payload.ProductName.Trim();
                video = GetShowcaseVideoBuffer()
                    .FirstOrDefault(v => string.Equals(v?.ProductName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        private void MarkShowcaseRenderComplete(
            IList<AiVideoGenInputItem> scenes,
            ShowcaseVideoItem video,
            string outputPath)
        {
            var thumb = ProductionPipeline.ResolveThumbnailPath(outputPath);
            if (scenes != null)
            {
                foreach (var scene in scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }

                    scene.IsProcessed = true;
                    scene.PipelineStatus = "Xong";
                    scene.OutputVideoPath = outputPath;
                    scene.ThumbnailPath = thumb;
                }
            }

            MarkBufferItemsProcessed(GetDeepDiveBuffer(), scenes, null, null);

            if (video != null)
            {
                video.PipelineStatus = "Xong";
                video.OutputVideoPath = outputPath;
                video.RefreshDisplayFields();
            }

            SyncBuffersToGrids();
        }

        private void ApplyShowcaseRenderFailure(IList<AiVideoGenInputItem> scenes, ShowcaseVideoItem video)
        {
            if (scenes != null)
            {
                foreach (var scene in scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }

                    scene.PipelineStatus = "Lỗi";
                }
            }

            if (video != null)
            {
                video.PipelineStatus = "Lỗi";
                video.RefreshDisplayFields();
            }

            SyncBuffersToGrids();
        }
    }
}
