using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni.Services
{
    /// <summary>Polling worker cho Veo image/video async — cập nhật OmniJob sang Processing khi chờ API.</summary>
    public sealed class MascotWorker
    {
        private readonly VideoService _videoService;
        private readonly AsyncTasksRebootStore _rebootStore;
        private readonly TimeSpan _pollInterval;
        private readonly TimeSpan _maxWait;

        public MascotWorker(
            VideoService videoService,
            AsyncTasksRebootStore rebootStore = null,
            TimeSpan? pollInterval = null,
            TimeSpan? maxWait = null)
        {
            _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            _rebootStore = rebootStore;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(8);
            _maxWait = maxWait ?? TimeSpan.FromMinutes(25);
        }

        public async Task<string> GenerateContextImageWithPollingAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            System.Collections.Generic.IList<string> referenceImageUrls,
            CancellationToken cancellationToken,
            Action<string> logAction = null,
            Action<string> onProcessingPhase = null)
        {
            onProcessingPhase?.Invoke("Veo: tạo ảnh cảnh (Processing)…");
            var submit = await _videoService.SubmitContextImageAsync(
                sourceImageUrl,
                prompt,
                veoApiKey,
                veoEndpoint,
                referenceImageUrls,
                cancellationToken).ConfigureAwait(false);

            return await WaitForMediaUrlAsync(
                submit,
                veoApiKey,
                veoEndpoint,
                expectVideo: false,
                logAction,
                onProcessingPhase,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> GenerateVideoFromImageWithPollingAsync(
            string sourceImageUrl,
            string prompt,
            string veoApiKey,
            string veoEndpoint,
            double durationSeconds,
            CancellationToken cancellationToken,
            Action<string> logAction = null,
            Action<string> onProcessingPhase = null)
        {
            onProcessingPhase?.Invoke("Veo: tạo clip chuyển động (Processing)…");
            var submit = await _videoService.SubmitVideoFromImageAsync(
                sourceImageUrl,
                prompt,
                veoApiKey,
                veoEndpoint,
                durationSeconds,
                cancellationToken).ConfigureAwait(false);

            return await WaitForMediaUrlAsync(
                submit,
                veoApiKey,
                veoEndpoint,
                expectVideo: true,
                logAction,
                onProcessingPhase,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> WaitForMediaUrlAsync(
            VeoAsyncJobResult submit,
            string veoApiKey,
            string veoEndpoint,
            bool expectVideo,
            Action<string> logAction,
            Action<string> onProcessingPhase,
            CancellationToken cancellationToken)
        {
            if (submit == null)
            {
                throw new InvalidOperationException("Veo submit returned null.");
            }

            if (submit.IsTerminalFailure)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(submit.ErrorMessage) ? "Veo job failed." : submit.ErrorMessage);
            }

            var immediate = expectVideo ? submit.VideoUrl : submit.ImageUrl;
            if (!string.IsNullOrWhiteSpace(immediate))
            {
                return immediate;
            }

            if (string.IsNullOrWhiteSpace(submit.JobId))
            {
                throw new InvalidOperationException("Veo không trả media URL hoặc jobId.");
            }

            _rebootStore?.Upsert(new AsyncVeoTaskEntry
            {
                TaskId = submit.JobId,
                StatusPollUrl = submit.StatusPollUrl ?? string.Empty,
                Status = submit.Status ?? "Processing",
                Kind = expectVideo ? "MascotSceneVideo" : "MascotSceneImage",
                ExpectVideo = expectVideo
            });

            var deadline = DateTime.UtcNow.Add(_maxWait);
            var polls = 0;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                polls++;
                onProcessingPhase?.Invoke($"Veo poll #{polls} — job {submit.JobId} (Processing)…");
                logAction?.Invoke($"[MascotWorker] Poll #{polls} job={submit.JobId} status={submit.Status}");

                var status = await _videoService.PollVeoJobAsync(
                    submit.JobId,
                    submit.StatusPollUrl,
                    veoApiKey,
                    veoEndpoint,
                    cancellationToken).ConfigureAwait(false);

                if (status.IsTerminalFailure)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(status.ErrorMessage)
                            ? "Veo job failed during polling."
                            : status.ErrorMessage);
                }

                var url = expectVideo ? status.VideoUrl : status.ImageUrl;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    _rebootStore?.Remove(submit.JobId);
                    logAction?.Invoke("[MascotWorker] Veo job hoàn tất → media URL.");
                    return url;
                }

                if (!VeoAsyncJobResult.IsProcessingStatus(status.Status) &&
                    !status.HasMediaUrl &&
                    !string.IsNullOrWhiteSpace(status.Status))
                {
                    logAction?.Invoke("[MascotWorker] Trạng thái Veo: " + status.Status);
                }
            }

            throw new TimeoutException("Veo job polling timed out after " + _maxWait.TotalMinutes.ToString("0") + " minutes.");
        }

        /// <summary>Tiếp tục poll task đã lưu trong async_tasks_reboot.json (không submit mới).</summary>
        public Task<string> ResumeVeoTaskAsync(
            AsyncVeoTaskEntry entry,
            string veoApiKey,
            string veoEndpoint,
            CancellationToken cancellationToken,
            Action<string> logAction = null,
            Action<string> onProcessingPhase = null)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.TaskId))
            {
                throw new ArgumentException("Thiếu TaskId để resume.", nameof(entry));
            }

            var submit = new VeoAsyncJobResult
            {
                JobId = entry.TaskId,
                StatusPollUrl = entry.StatusPollUrl ?? string.Empty,
                Status = entry.Status ?? "Processing"
            };

            return WaitForMediaUrlAsync(
                submit,
                veoApiKey,
                veoEndpoint,
                entry.ExpectVideo,
                logAction,
                onProcessingPhase,
                cancellationToken);
        }

        public static void MarkOmniJobProcessing(GlobalJobQueue queue, OmniJob job, string phase)
        {
            if (queue == null || job == null)
            {
                return;
            }

            job.Status = OmniJobStatus.Processing;
            if (!string.IsNullOrWhiteSpace(phase))
            {
                job.Title = phase.Length > 120 ? phase.Substring(0, 120) : phase;
            }

            queue.UpdateJob(job);
        }
    }
}
