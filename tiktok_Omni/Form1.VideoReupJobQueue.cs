using System;
using System.Collections.Generic;
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
        private readonly HashSet<Guid> _videoReupBatchJobIds = new HashSet<Guid>();

        private bool EnqueueVideoReupJob(VideoReupRowItem row, AppSettings settings, int batchIndex, int batchTotal)
        {
            if (row == null || string.IsNullOrWhiteSpace((row.VideoUrl ?? string.Empty).Trim()))
            {
                return false;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(row.ProfileName);
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(settings.StorageRootPath, profile);
            var cts = new CancellationTokenSource();
            ApplyReupVisualHookSettingsToRow(row);
            VideoReupRemixService.SanitizeStaleReupCache(row);
            var payload = new VideoReupJobPayload
            {
                Row = CloneVideoReupRowForJob(row),
                StorageRootPath = settings.StorageRootPath ?? string.Empty,
                BatchIndex = batchIndex,
                BatchTotal = batchTotal,
                UseVisualHookSfx = row.UseVisualHookSfx,
                VisualHookSfxPath = row.VisualHookSfxPath ?? string.Empty,
                VisualHookDurationSec = VisualHookService.HookDurationSeconds
            };

            var title = "Video Reup — " + TruncateAffiliateJobTitle(row.ProductName, profile);
            var job = new OmniJob
            {
                Kind = OmniJobKind.VideoReup,
                Title = title,
                ProfileName = profile,
                PayloadJson = JsonConvert.SerializeObject(payload),
                MaxRetries = 1,
                Tag = cts
            };

            _videoReupBatchJobIds.Add(job.Id);
            _globalJobQueue.Enqueue(job);
            return true;
        }

        private static VideoReupRowItem CloneVideoReupRowForJob(VideoReupRowItem row)
        {
            return new VideoReupRowItem
            {
                SourceKeyword = row.SourceKeyword,
                ProfileName = row.ProfileName,
                ProductName = row.ProductName,
                Price = row.Price,
                VideoUrl = row.VideoUrl,
                ImageUrl = row.ImageUrl,
                Hashtags = row.Hashtags,
                VideoScript = row.VideoScript,
                ReupHookDraft = row.ReupHookDraft,
                HookAudioPath = row.HookAudioPath,
                ReupSuggestedMusicFile = row.ReupSuggestedMusicFile,
                ReupSelectedMusicFile = row.ReupSelectedMusicFile,
                ReupSelectedHookSfxFile = row.ReupSelectedHookSfxFile ?? string.Empty,
                ReupAudioMode = row.ReupAudioMode,
                ReupStageFolder = row.ReupStageFolder,
                ReupDownloadedVideoPath = row.ReupDownloadedVideoPath,
                ReupHookAudioPath = row.ReupHookAudioPath,
                UseVisualHookSfx = row.UseVisualHookSfx,
                VisualHookSfxPath = row.VisualHookSfxPath
            };
        }

        internal async Task ExecuteVideoReupJobForWorkerAsync(
            OmniJob job,
            VideoReupJobPayload payload,
            IJobUiBridge ui,
            CancellationToken cancellationToken)
        {
            if (payload?.Row == null)
            {
                throw new InvalidOperationException("Video reup payload thiếu dòng dữ liệu.");
            }

            var tcs = new TaskCompletionSource<bool>();
            Exception captured = null;
            BeginInvoke(new Action(async () =>
            {
                try
                {
                    await ExecuteVideoReupJobOnUiThreadAsync(job, payload, ui, cancellationToken).ConfigureAwait(true);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    captured = ex;
                    tcs.TrySetException(ex);
                }
            }));

            await tcs.Task.ConfigureAwait(false);
            if (captured != null)
            {
                throw captured;
            }
        }

        private async Task ExecuteVideoReupJobOnUiThreadAsync(
            OmniJob job,
            VideoReupJobPayload payload,
            IJobUiBridge ui,
            CancellationToken cancellationToken)
        {
            var url = (payload.Row.VideoUrl ?? string.Empty).Trim();
            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            var row = FindVideoReupRowByVideoUrl(url) ?? payload.Row;
            VideoReupRemixService.SanitizeStaleReupCache(row);
            if (ReferenceEquals(row, payload.Row) == false)
            {
                VideoReupRemixService.SanitizeStaleReupCache(payload.Row);
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(payload.StorageRootPath ?? settings.StorageRootPath);
            ApplyReupVisualHookSettingsToRow(row);

            if (payload.BatchTotal > 0)
            {
                var pct = (int)Math.Round((double)(payload.BatchIndex - 1) / payload.BatchTotal * 100);
                SetVideoReupProgress($"Queue [{payload.BatchIndex}/{payload.BatchTotal}]: «{row.ProductName}»", pct);
            }

            ui.Log("Video reup [Job] «" + row.ProductName + "»…");
            if (!VideoReupRemixService.LooksLikeHttpVideoUrl(row.VideoUrl))
            {
                ui.Log("Video reup [Job]: URL video không hợp lệ — cần link TikTok http(s).");
            }
            else if (string.IsNullOrWhiteSpace((row.ReupDownloadedVideoPath ?? string.Empty).Trim())
                     || !File.Exists(row.ReupDownloadedVideoPath))
            {
                ui.Log("Video reup [Job]: chưa có source.mp4 — pipeline sẽ tự tải ở bước 1/4.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await RunVideoReupFullPipelineAsync(row, settings).ConfigureAwait(true);
                _renderHistoryStore.AddSuccess(row.VideoUrl);
                row.LastRemixOutputPath = result.OutputPath ?? string.Empty;
                row.LastSourceVideoDurationSec = result.SourceDurationSeconds;
                row.LastRemixOutputVideoDurationSec = result.OutputFileDurationSeconds;
                row.LastHookDurationUsedSec = result.HookDurationSecondsUsed;
                row.RemixStatus = "Xong";
                row.RemixLastError = string.Empty;
                InvalidateVideoReupGridRow(row);
                ui.OnVideoReupJobFinished(job?.Id ?? Guid.Empty, true, url, result, string.Empty);
            }
            catch (OperationCanceledException)
            {
                row.RemixStatus = "Đã hủy";
                row.RemixLastError = "Cancelled";
                InvalidateVideoReupGridRow(row);
                ui.OnVideoReupJobFinished(job?.Id ?? Guid.Empty, false, url, null, "Đã hủy");
                throw;
            }
            catch (Exception ex)
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = ex.Message;
                InvalidateVideoReupGridRow(row);
                ui.OnVideoReupJobFinished(job?.Id ?? Guid.Empty, false, url, null, ex.Message);
                throw;
            }
        }

        private VideoReupRowItem FindVideoReupRowByVideoUrl(string videoUrl)
        {
            var url = (videoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url) || _videoReupBindingList == null)
            {
                return null;
            }

            return _videoReupBindingList.FirstOrDefault(r =>
                string.Equals((r?.VideoUrl ?? string.Empty).Trim(), url, StringComparison.OrdinalIgnoreCase));
        }

        private void CancelAllVideoReupBatchJobs()
        {
            foreach (var id in _videoReupBatchJobIds.ToList())
            {
                _globalJobQueue?.TryCancel(id);
            }

            _videoReupBatchJobIds.Clear();
            LogVideoReup("[JobQueue] Đã hủy các job Video reup đang chờ/chạy.");
        }

        private static string TruncateAffiliateJobTitle(string productName, string profile)
        {
            var name = (productName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = profile;
            }

            return name.Length <= 48 ? name : name.Substring(0, 45) + "…";
        }
    }
}
