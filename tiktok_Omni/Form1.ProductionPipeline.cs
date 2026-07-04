using System;
using System.ComponentModel;
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
        private BindingList<ProductionQueueRowItem> _philosophyQueueBindingList;
        private BindingList<ProductionQueueRowItem> _mascotQueueBindingList;
        private DataGridView dgvPhilosophyQueue;
        private DataGridView dgvMascotQueue;
        private PictureBox pbProductionVideoPreview;
        private Label lblProductionPreviewCaption;
        private readonly Dictionary<Guid, ProductionQueueRowItem> _philosophyJobRows = new Dictionary<Guid, ProductionQueueRowItem>();
        private readonly Dictionary<Guid, ProductionQueueRowItem> _mascotJobRows = new Dictionary<Guid, ProductionQueueRowItem>();
        private Guid? _activeAffiliateDeepRenderJobId;

        private async Task EnqueueProductionApprovalAsync(
            ApprovalJobType jobType,
            string profileName,
            string title,
            string videoPath,
            int safetyScore,
            string riskReasons,
            object payload,
            string scriptPreview = null)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var thumb = ProductionPipeline.ResolveThumbnailPath(videoPath);
            var requiresManual = ProductionPipeline.RequiresManualApproval(safetyScore)
                               || (riskReasons ?? string.Empty).IndexOf("bắt buộc", StringComparison.OrdinalIgnoreCase) >= 0;

            var item = new ApprovalQueueItem
            {
                JobType = jobType,
                Status = ApprovalStatus.Pending,
                Profile = profile,
                Title = title ?? string.Empty,
                PayloadJson = payload == null ? "{}" : JsonConvert.SerializeObject(payload),
                SafetyScore = safetyScore,
                RiskScore = Math.Max(0, 100 - safetyScore),
                RequiresManualApproval = requiresManual,
                RiskReasons = string.IsNullOrWhiteSpace(riskReasons)
                    ? (requiresManual ? $"SafetyScore {safetyScore} < {ProductionPipeline.ManualApprovalSafetyThreshold} — duyệt thủ công trước khi đăng." : string.Empty)
                    : riskReasons,
                OriginalPreview = (scriptPreview ?? title ?? string.Empty).Trim(),
                ThumbnailPath = thumb,
                EditedPreview = videoPath ?? string.Empty,
                AffiliateLink = jobType == ApprovalJobType.PhilosophyVideo
                    ? string.Empty
                    : ExtractAffiliateLinkFromApprovalPayload(payload),
                ProductId = jobType == ApprovalJobType.PhilosophyVideo
                    ? string.Empty
                    : ExtractProductIdFromApprovalPayload(payload),
                CanAttachAffiliate = false,
                TargetAffiliateLink = jobType == ApprovalJobType.PhilosophyVideo
                    ? string.Empty
                    : ExtractAffiliateLinkFromApprovalPayload(payload),
                TargetProductId = jobType == ApprovalJobType.PhilosophyVideo
                    ? string.Empty
                    : ExtractProductIdFromApprovalPayload(payload)
            };
            ApprovalAffiliateTagging.EnsureTargetFields(item, item.AffiliateLink, item.ProductId);

            await EnqueueApprovalItemAsync(item).ConfigureAwait(true);
            Log($"[APPROVAL] {jobType} → Pending (score {safetyScore}) nick «{profile}»" +
                (requiresManual ? " — bắt buộc duyệt trước Auto Post." : "."));

            RecordProductionSuccess(MapApprovalJobTypeToPipeline(jobType));
            await TryAutoAdvanceApprovalAsync(item).ConfigureAwait(true);
            await RefreshHealthDashboardAsync().ConfigureAwait(true);
        }

        private void UpdateAiVideoGenItemProgress(int productIndex, VideoRenderProgress progress)
        {
            var buffer = GetSlideshowBuffer();
            if (productIndex < 0 || productIndex >= buffer.Count)
            {
                return;
            }

            var item = buffer[productIndex];
            if (item == null)
            {
                return;
            }

            item.ProgressPercent = Math.Max(0, Math.Min(100, progress?.Percent ?? 0));
            item.PipelineStatus = ProductionPipeline.MapRenderStageToStatus(progress?.Stage, item.ProgressPercent);
            if (progress != null && progress.IsCompleted && !string.IsNullOrWhiteSpace(progress.OutputPath))
            {
                item.OutputVideoPath = progress.OutputPath;
                item.ThumbnailPath = ProductionPipeline.ResolveThumbnailPath(progress.OutputPath);
                item.IsProcessed = true;
                item.PipelineStatus = "Xong";
            }

            SyncBuffersToGrids();
        }

        private void ScoreAndApplyAiVideoGenSafety(IList<AiVideoGenInputItem> items, string script)
        {
            if (items == null)
            {
                return;
            }

            var profile = ResolvePrimaryProfileFromAiBuffer(items);
            var risk = _safetyScoreService.ScoreRender(script ?? string.Empty, items, profile);
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                item.SafetyScore = risk.Score;
                if (string.IsNullOrWhiteSpace(item.PipelineStatus) || item.PipelineStatus == "Chờ")
                {
                    item.PipelineStatus = "Chờ";
                }
            }
        }

        private void BindProductionPreviewForAiItem(AiVideoGenInputItem item)
        {
            if (item == null)
            {
                return;
            }

            if (txtAiVideoGenPrompt != null)
            {
                var script = item.ScriptPreview;
                if (string.IsNullOrWhiteSpace(script) && _aiVideoScriptBindingList != null)
                {
                    var match = _aiVideoScriptBindingList.FirstOrDefault(x =>
                        string.Equals((x?.ProductName ?? string.Empty).Trim(), (item.ProductName ?? string.Empty).Trim(),
                            StringComparison.OrdinalIgnoreCase));
                    script = match?.Script ?? string.Empty;
                }

                txtAiVideoGenPrompt.Text = script ?? string.Empty;
            }

            LoadProductionVideoPreview(item.OutputVideoPath, item.ThumbnailPath, item.ProductName);
        }

        private void LoadProductionVideoPreview(string videoPath, string thumbPath, string caption)
        {
            if (lblProductionPreviewCaption != null)
            {
                lblProductionPreviewCaption.Text = string.IsNullOrWhiteSpace(caption)
                    ? "Video Preview"
                    : "Video: " + caption;
            }

            if (pbProductionVideoPreview == null)
            {
                return;
            }

            var path = !string.IsNullOrWhiteSpace(thumbPath) && File.Exists(thumbPath)
                ? thumbPath
                : videoPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                pbProductionVideoPreview.Image = null;
                return;
            }

            try
            {
                using (var img = Image.FromFile(path))
                {
                    pbProductionVideoPreview.Image = new Bitmap(img);
                }
            }
            catch
            {
                pbProductionVideoPreview.Image = null;
            }
        }

        private const int ProductionQueueHeaderHeight = AppGridHeaderHeight;
        private const int ProductionQueueRowHeight = 26;
        private const int ProductionQueueBandMinHeight = 152;

        private void ConfigureProductionQueueGrid(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = ProductionQueueHeaderHeight;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(52, 56, 68),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(52, 56, 68),
                Font = AppGridHeaderFont,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(6, 8, 6, 8),
                WrapMode = DataGridViewTriState.False
            };
            grid.RowTemplate.Height = ProductionQueueRowHeight;
            grid.MinimumSize = new Size(0, ProductionQueueHeaderHeight + ProductionQueueRowHeight + 6);
            grid.ScrollBars = ScrollBars.Both;
            grid.AutoGenerateColumns = false;
            grid.Columns.Clear();
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Profile",
                DataPropertyName = nameof(ProductionQueueRowItem.ProfileName),
                FillWeight = 14
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Trạng thái",
                DataPropertyName = nameof(ProductionQueueRowItem.PipelineStatus),
                FillWeight = 14
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "SafetyScore",
                DataPropertyName = nameof(ProductionQueueRowItem.SafetyScore),
                FillWeight = 10
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Thumb",
                DataPropertyName = nameof(ProductionQueueRowItem.ThumbnailPath),
                FillWeight = 18
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Tiêu đề",
                DataPropertyName = nameof(ProductionQueueRowItem.Title),
                FillWeight = 28
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Script",
                DataPropertyName = nameof(ProductionQueueRowItem.ScriptPreview),
                FillWeight = 26
            });

            EnsureProductionQueueGridBandHeight(grid);
        }

        private void EnsureProductionQueueGridBandHeight(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            grid.EnableHeadersVisualStyles = false;
            if (grid.ColumnHeadersHeight < ProductionQueueHeaderHeight)
            {
                grid.ColumnHeadersHeight = ProductionQueueHeaderHeight;
            }

            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            var minGridH = grid.ColumnHeadersHeight + grid.RowTemplate.Height + 6;
            if (grid.MinimumSize.Height < minGridH)
            {
                grid.MinimumSize = new Size(grid.MinimumSize.Width, minGridH);
            }

            var host = grid.Parent as TableLayoutPanel;
            if (host == null)
            {
                return;
            }

            var row = host.GetRow(grid);
            if (row < 0 || row >= host.RowStyles.Count)
            {
                return;
            }

            var band = Math.Max(ProductionQueueBandMinHeight, minGridH + 20);
            var style = host.RowStyles[row];
            if (style.SizeType != SizeType.Absolute || Math.Abs(style.Height - band) > 1F)
            {
                host.RowStyles[row] = new RowStyle(SizeType.Absolute, band);
                host.PerformLayout();
            }
        }


        private void DgvPhilosophyQueue_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvPhilosophyQueue?.CurrentRow?.DataBoundItem is ProductionQueueRowItem row)
            {
                if (txtPhilosophyTopic != null)
                {
                    txtPhilosophyTopic.Text = row.ScriptPreview ?? row.Title ?? string.Empty;
                }

                LoadProductionVideoPreview(row.OutputVideoPath, row.ThumbnailPath, row.Title);
            }
        }

        private void DgvMascotQueue_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvMascotQueue?.CurrentRow?.DataBoundItem is ProductionQueueRowItem row)
            {
                if (txtMascotChannelTheme != null && !string.IsNullOrWhiteSpace(row.ScriptPreview))
                {
                    txtMascotChannelTheme.Text = row.ScriptPreview;
                }

                LoadProductionVideoPreview(row.OutputVideoPath, row.ThumbnailPath, row.Title);
            }
        }

        private ProductionQueueRowItem TrackPhilosophyJob(Guid jobId, PhilosophyVideoJobPayload payload, string title)
        {
            _philosophyQueueBindingList = _philosophyQueueBindingList ?? new BindingList<ProductionQueueRowItem>();
            var row = new ProductionQueueRowItem
            {
                ProfileName = ProfileScopedPaths.ResolveProfileName(payload?.ProfileName),
                Title = title,
                ScriptPreview = payload?.QuoteText ?? string.Empty,
                VoiceId = payload?.VoiceId ?? string.Empty,
                BackgroundVideo = payload?.VideoStyle ?? string.Empty,
                PipelineStatus = "Chờ",
                JobId = jobId.ToString("N")
            };
            _philosophyQueueBindingList.Add(row);
            _philosophyJobRows[jobId] = row;
            return row;
        }

        private ProductionQueueRowItem TrackMascotJob(Guid jobId, MascotStoryJobPayload payload)
        {
            _mascotQueueBindingList = _mascotQueueBindingList ?? new BindingList<ProductionQueueRowItem>();
            var row = new ProductionQueueRowItem
            {
                ProfileName = ProfileScopedPaths.ResolveProfileName(payload?.ProfileName),
                Title = "Mascot — " + (payload?.ChannelTheme ?? string.Empty),
                ScriptPreview = payload?.ChannelTheme ?? string.Empty,
                PipelineStatus = "Chờ",
                JobId = jobId.ToString("N")
            };
            _mascotQueueBindingList.Add(row);
            _mascotJobRows[jobId] = row;
            return row;
        }

        private async Task EnsureAffiliateCategoryAfterDeepDiveAsync(AffiliateCandidate candidate)
        {
            if (candidate == null || !string.IsNullOrWhiteSpace(candidate.Category))
            {
                return;
            }

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                await _affiliateCategoryService.CategorizeBatchAsync(
                    new List<AffiliateCandidate> { candidate },
                    settings,
                    Log,
                    CancellationToken.None,
                    1).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(candidate.Category))
                {
                    Log("[Category] Deep Dive → ngách: " + candidate.Category);
                    _affiliateBindingList?.ResetBindings();
                }
            }
            catch (Exception ex)
            {
                Log("[Category] Deep Dive phân loại lỗi: " + ex.Message);
            }
        }

        private async Task AfterRenderOutputsApprovalAsync(
            IList<AiVideoGenInputItem> products,
            List<string> outputPaths,
            string script,
            ApprovalJobType jobType)
        {
            if (outputPaths == null || outputPaths.Count == 0)
            {
                return;
            }

            var profile = ResolvePrimaryProfileFromAiBuffer(products);
            var risk = _safetyScoreService.ScoreRender(script ?? string.Empty, products, profile);
            for (var i = 0; i < outputPaths.Count; i++)
            {
                var path = outputPaths[i];
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                var title = products != null && i < products.Count
                    ? (products[i]?.ProductName ?? "Video " + (i + 1))
                    : Path.GetFileName(path);

                await EnqueueProductionApprovalAsync(
                    jobType,
                    profile,
                    title,
                    path,
                    risk.Score,
                    string.Join("; ", risk.Reasons),
                    new RenderApprovalPayload
                    {
                        Profile = profile,
                        ProfileName = profile,
                        Script = script,
                        OutputVideoPath = path,
                        AffiliateLink = products != null && i < products.Count
                            ? OmnichannelAutoPostFields.NormalizeLink(products[i]?.AffiliateLink)
                            : string.Empty,
                        ProductId = products != null && i < products.Count
                            ? OmnichannelAutoPostFields.NormalizeLink(products[i]?.ProductId)
                            : string.Empty
                    },
                    title).ConfigureAwait(true);
            }
        }

        private static string MapApprovalJobTypeToPipeline(ApprovalJobType jobType)
        {
            switch (jobType)
            {
                case ApprovalJobType.Slideshow:
                    return OneClickPipelineService.PipelineSlideshow;
                case ApprovalJobType.AffiliateDeep:
                    return OneClickPipelineService.PipelineAffiliateDeep;
                case ApprovalJobType.Mascot:
                    return OneClickPipelineService.PipelineMascot;
                case ApprovalJobType.PhilosophyVideo:
                    return OneClickPipelineService.PipelinePhilosophy;
                case ApprovalJobType.VideoReup:
                    return OneClickPipelineService.PipelineVideoReup;
                default:
                    return "Other";
            }
        }

        private static string ExtractAffiliateLinkFromApprovalPayload(object payload)
        {
            if (payload == null)
            {
                return string.Empty;
            }

            try
            {
                if (payload is RenderVideoJobPayload render && render.Products != null && render.Products.Count > 0)
                {
                    return OmnichannelAutoPostFields.NormalizeLink(render.Products[0]?.AffiliateLink ?? render.AffiliateLink);
                }

                if (payload is AiVideoGenInputItem single)
                {
                    return OmnichannelAutoPostFields.NormalizeLink(single.AffiliateLink);
                }

                var jo = Newtonsoft.Json.Linq.JObject.FromObject(payload);
                var fromRoot = OmnichannelAutoPostFields.NormalizeLink(jo["AffiliateLink"]?.ToString());
                if (!string.IsNullOrWhiteSpace(fromRoot))
                {
                    return fromRoot;
                }

                var products = jo["Products"] as Newtonsoft.Json.Linq.JArray;
                if (products != null && products.Count > 0)
                {
                    return OmnichannelAutoPostFields.NormalizeLink(products[0]["AffiliateLink"]?.ToString());
                }
            }
            catch
            {
                // ignored
            }

            return string.Empty;
        }

        private static string ExtractProductIdFromApprovalPayload(object payload)
        {
            if (payload == null)
            {
                return string.Empty;
            }

            try
            {
                if (payload is RenderVideoJobPayload render)
                {
                    return OmnichannelAutoPostFields.NormalizeLink(render.Products?.FirstOrDefault()?.ProductId ?? render.ProductId);
                }

                if (payload is AiVideoGenInputItem single)
                {
                    return OmnichannelAutoPostFields.NormalizeLink(single.ProductId);
                }

                var jo = Newtonsoft.Json.Linq.JObject.FromObject(payload);
                var fromRoot = OmnichannelAutoPostFields.NormalizeLink(jo["ProductId"]?.ToString());
                if (!string.IsNullOrWhiteSpace(fromRoot))
                {
                    return fromRoot;
                }

                var products = jo["Products"] as Newtonsoft.Json.Linq.JArray;
                if (products != null && products.Count > 0)
                {
                    return OmnichannelAutoPostFields.NormalizeLink(products[0]["ProductId"]?.ToString());
                }
            }
            catch
            {
                // ignored
            }

            return string.Empty;
        }
    }
}
