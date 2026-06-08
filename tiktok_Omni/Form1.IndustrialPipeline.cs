using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private enum IndustrialBatchPhase
        {
            None,
            Hunt,
            Script,
            Render
        }

        private readonly ProductionHealthStore _productionHealthStore = new ProductionHealthStore();
        private readonly SemaphoreSlim _healthDashboardRefreshGate = new SemaphoreSlim(1, 1);
        private bool _productionHealthLoaded;
        private IndustrialBatchPhase _industrialBatchPhase = IndustrialBatchPhase.None;

        private Panel pnlHealthDashboard;
        private Label lblHealthVideos;
        private Label lblHealthErrors;
        private Label lblHealthPending;
        private Button btnAffiliateBatchPipeline;

        private void BuildHealthDashboardUi()
        {
            pnlHealthDashboard = new Panel
            {
                Name = "pnlHealthDashboard",
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(38, 42, 54),
                Padding = new Padding(12, 6, 12, 6)
            };

            lblHealthVideos = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                ForeColor = Color.FromArgb(120, 200, 140),
                Text = "Video đã làm: 0"
            };
            lblHealthErrors = new Label
            {
                AutoSize = true,
                Location = new Point(200, 12),
                ForeColor = Color.FromArgb(255, 120, 120),
                Text = "Lỗi: 0"
            };
            lblHealthPending = new Label
            {
                AutoSize = true,
                Location = new Point(320, 12),
                ForeColor = Color.FromArgb(255, 200, 100),
                Text = "Chờ duyệt: 0"
            };

            pnlHealthDashboard.Controls.Add(lblHealthVideos);
            pnlHealthDashboard.Controls.Add(lblHealthErrors);
            pnlHealthDashboard.Controls.Add(lblHealthPending);
        }

        private async Task RefreshHealthDashboardAsync()
        {
            if (!await _healthDashboardRefreshGate.WaitAsync(0).ConfigureAwait(true))
            {
                return;
            }

            try
            {
                if (!_productionHealthLoaded)
                {
                    await _productionHealthStore.LoadAsync().ConfigureAwait(true);
                    _productionHealthLoaded = true;
                }

                var pending = (_approvalQueueItems ?? new List<ApprovalQueueItem>())
                    .Count(x => x != null && x.Status == ApprovalStatus.Pending);
                _productionHealthStore.SetPendingApprovals(pending);
                await _productionHealthStore.SaveAsync().ConfigureAwait(true);

                var s = _productionHealthStore.Snapshot;
            if (lblHealthVideos != null)
            {
                lblHealthVideos.Text = "Video đã làm: " + s.TotalVideosProduced;
            }

            if (lblHealthErrors != null)
            {
                lblHealthErrors.Text = "Lỗi: " + s.TotalErrors;
            }

            if (lblHealthPending != null)
            {
                lblHealthPending.Text = "Chờ duyệt: " + s.PendingApprovals;
            }

            BindHealthDashboardGrids();
            }
            catch (Exception)
            {
            }
            finally
            {
                _healthDashboardRefreshGate.Release();
            }
        }

        private void RecordProductionSuccess(string pipelineName = "Other")
        {
            _productionHealthStore.RecordVideoCompleted(pipelineName);
            _ = RefreshHealthDashboardAsync();
        }

        private void RecordProductionError(string pipelineName = null)
        {
            if (!string.IsNullOrWhiteSpace(pipelineName))
            {
                _productionHealthStore.RecordPipelineFailure(pipelineName);
            }
            else
            {
                _productionHealthStore.RecordError();
            }

            _ = RefreshHealthDashboardAsync();
        }

        public void RecordApiHealthAlert(string service, string message)
        {
            _productionHealthStore.RecordApiError(service, message);
            _ = RefreshHealthDashboardAsync();
        }

        private void RecordApiFailureFromMessage(string message)
        {
            var msg = message ?? string.Empty;
            if (msg.IndexOf("veo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RecordApiHealthAlert("Veo", msg);
            }
            else if (msg.IndexOf("gemini", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RecordApiHealthAlert("Gemini", msg);
            }
            else if (msg.IndexOf("tts", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RecordApiHealthAlert("TTS", msg);
            }
        }

        private async Task PruneRevenueDeadJobsAsync()
        {
            try
            {
                await _revenueDrivenService.LoadAsync().ConfigureAwait(true);
                await _revenueDrivenService.PruneDeadProductsFromQueueAsync(
                    _globalJobQueue,
                    _affiliateAllResults,
                    Log).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("[Revenue] Prune queue: " + ex.Message);
            }
        }

        private async Task TryAutoAdvanceApprovalAsync(ApprovalQueueItem item)
        {
            if (item == null || item.RequiresManualApproval)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (!(settings.AutoRunApprovedQueue ?? false))
            {
                return;
            }

            item.Status = ApprovalStatus.Approved;
            item.ReviewedBy = "AutoQueue";
            item.ReviewedAtUtc = DateTime.UtcNow;
            AppendApprovalAudit(item, "AutoQueue: Auto-approved (SafetyScore >= 85)");

            var published = await ForwardApprovedVideoToPublishingAsync(item).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(published))
            {
                item.Status = ApprovalStatus.Completed;
                item.CompletedAtUtc = DateTime.UtcNow;
                item.EditedPreview = published;
                Log("[APPROVAL] AutoRun → Publishing: " + published);
                if (!CheckAssetIntegrity(item, out var autoIntegrityError))
                {
                    Log("[APPROVAL] Asset integrity — AutoRun bỏ qua đăng: " + autoIntegrityError);
                    await _approvalQueueManager.SaveAsync(_approvalQueueItems).ConfigureAwait(true);
                    await RefreshHealthDashboardAsync().ConfigureAwait(true);
                    return;
                }

                ApprovalAffiliateTagging.ResolveLinksForAutoPost(item, out var affLink, out var prodId);
                var inboxJob = new OmniJob();
                OmniJobMetadataHelper.ApplyAffiliateFields(inboxJob, affLink, prodId);
                ForwardToAutoPost(
                    published,
                    item.OriginalPreview,
                    string.Empty,
                    item.Profile,
                    affLink,
                    prodId,
                    sourceJob: inboxJob);
            }

            await _approvalQueueManager.SaveAsync(_approvalQueueItems).ConfigureAwait(true);
            await RefreshHealthDashboardAsync().ConfigureAwait(true);
        }

        private async Task<string> ForwardApprovedVideoToPublishingAsync(ApprovalQueueItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var videoPath = ExtractVideoPathFromApprovalItem(item);
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return string.Empty;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = ProfileScopedPaths.ResolveProfileName(item.Profile);
            var videoType = item.JobType == ApprovalJobType.PhilosophyVideo
                ? VideoStorageType.Processed
                : VideoStorageType.Processed;

            return ProfileScopedPaths.CopyVideoToPublishing(
                settings.StorageRootPath,
                profile,
                videoPath,
                videoType);
        }

        private async Task CompleteApprovedProductionVideoAsync(
            ApprovalQueueItem item,
            string videoPath,
            string profileName,
            string affiliateLink = null,
            string productId = null)
        {
            var published = await ForwardApprovedVideoToPublishingAsync(item).ConfigureAwait(true);
            var finalPath = !string.IsNullOrWhiteSpace(published) ? published : videoPath;
            item.Status = ApprovalStatus.Completed;
            item.CompletedAtUtc = DateTime.UtcNow;
            item.LastError = string.Empty;
            item.EditedPreview = finalPath;
            Log("[APPROVAL] " + item.JobType + " → Publishing: " + finalPath);

            ApprovalAffiliateTagging.EnsureTargetFields(item, affiliateLink, productId);
            if (item != null && item.JobType == ApprovalJobType.PhilosophyVideo)
            {
                item.CanAttachAffiliate = false;
            }

            if (!CheckAssetIntegrity(item, out var integrityError))
            {
                item.LastError = integrityError;
                Log("[APPROVAL] Asset integrity — không chuyển Auto Post: " + integrityError);
                await _approvalQueueManager.SaveAsync(_approvalQueueItems).ConfigureAwait(true);
                return;
            }

            ApprovalAffiliateTagging.ResolveLinksForAutoPost(item, out var link, out var pid);

            var inboxJob = new OmniJob();
            OmniJobMetadataHelper.ApplyAffiliateFields(inboxJob, link, pid);

            ForwardToAutoPost(
                finalPath,
                item?.OriginalPreview ?? string.Empty,
                string.Empty,
                profileName,
                link,
                pid,
                sourceJob: inboxJob);
            await _approvalQueueManager.SaveAsync(_approvalQueueItems).ConfigureAwait(true);
            await RefreshHealthDashboardAsync().ConfigureAwait(true);
        }

        private static void ResolveAffiliateFieldsFromApprovalItem(
            ApprovalQueueItem item,
            out string affiliateLink,
            out string productId)
        {
            affiliateLink = OmnichannelAutoPostFields.NormalizeLink(item?.AffiliateLink);
            productId = OmnichannelAutoPostFields.NormalizeLink(item?.ProductId);
            if (item == null || string.IsNullOrWhiteSpace(item.PayloadJson))
            {
                return;
            }

            try
            {
                var jo = Newtonsoft.Json.Linq.JObject.Parse(item.PayloadJson);
                if (string.IsNullOrWhiteSpace(affiliateLink))
                {
                    affiliateLink = OmnichannelAutoPostFields.NormalizeLink(jo["AffiliateLink"]?.ToString());
                }

                if (string.IsNullOrWhiteSpace(productId))
                {
                    productId = OmnichannelAutoPostFields.NormalizeLink(jo["ProductId"]?.ToString());
                }

                var products = jo["Products"] as Newtonsoft.Json.Linq.JArray;
                if (products != null && products.Count > 0)
                {
                    var first = products[0];
                    if (string.IsNullOrWhiteSpace(affiliateLink))
                    {
                        affiliateLink = OmnichannelAutoPostFields.NormalizeLink(first["AffiliateLink"]?.ToString());
                    }

                    if (string.IsNullOrWhiteSpace(productId))
                    {
                        productId = OmnichannelAutoPostFields.NormalizeLink(first["ProductId"]?.ToString());
                    }
                }
            }
            catch
            {
                // Payload không phải JSON hợp lệ — giữ giá trị đã chuẩn hóa từ item.
            }
        }

        private static string ExtractVideoPathFromApprovalItem(ApprovalQueueItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.EditedPreview) && File.Exists(item.EditedPreview))
            {
                return item.EditedPreview;
            }

            try
            {
                var jo = Newtonsoft.Json.Linq.JObject.Parse(item.PayloadJson ?? "{}");
                return (jo["OutputPath"] ?? jo["OutputVideoPath"])?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void btnAffiliateBatchPipeline_Click(object sender, EventArgs e)
        {
            var keywords = (GetAffiliateKeywordsTextFromUi() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(keywords))
            {
                MessageBox.Show(this,
                    "Nhập từ khóa ở ô «từ khoá» (tab Săn Video) trước khi chạy Pipeline hàng loạt.",
                    "Pipeline hàng loạt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                txtAffiliateKeywords?.Focus();
                return;
            }

            _industrialBatchPhase = IndustrialBatchPhase.Hunt;
            Log("[Batch Pipeline] 1/5 — Hunt → Queue…");
            btnHuntAffiliates_Click(sender, e);
            await Task.Delay(500).ConfigureAwait(true);
            await ContinueIndustrialBatchAfterHuntAsync().ConfigureAwait(true);
        }

        private async Task ContinueIndustrialBatchAfterHuntAsync()
        {
            if (_industrialBatchPhase != IndustrialBatchPhase.Hunt)
            {
                return;
            }

            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                Log("[Batch Pipeline] Chờ Hunt xong — khi có dữ liệu, bấm lại «Batch Pipeline» để tiếp tục Script+Render.");
                return;
            }

            _industrialBatchPhase = IndustrialBatchPhase.Script;
            Log("[Batch Pipeline] 2/5 — Lọc high-quality + đẩy AI Gen…");
            if (chkAffiliateOnlyHighQuality != null)
            {
                chkAffiliateOnlyHighQuality.Checked = true;
            }

            RefreshAffiliateGridByQualityFilter();
            PushVisibleAffiliateRowsToAiVideoGen(targetDeepDive: false);

            if (GetSlideshowBuffer().Count == 0)
            {
                Log("[Batch Pipeline] Không có dòng đủ điểm — hạ ngưỡng Safety hoặc săn thêm.");
                _industrialBatchPhase = IndustrialBatchPhase.None;
                return;
            }

            Log("[Batch Pipeline] 3/5 — Gemini scripting…");
            btnReviewScriptBeforeRender_Click(this, EventArgs.Empty);
            await Task.Delay(1500).ConfigureAwait(true);

            _industrialBatchPhase = IndustrialBatchPhase.Render;
            Log("[Batch Pipeline] 4/5 — Render batch → JobQueue…");
            btnRenderAiVideo_Click(this, EventArgs.Empty);

            _industrialBatchPhase = IndustrialBatchPhase.None;
            Log("[Batch Pipeline] 5/5 — Kết quả sẽ vào Approval Queue sau render.");
            await RefreshHealthDashboardAsync().ConfigureAwait(true);
        }
    }
}
