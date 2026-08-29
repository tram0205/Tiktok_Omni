using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private CancellationTokenSource _showcaseTabCts = new CancellationTokenSource();
        private bool _showcaseTabPaused;
        private int _showcaseTabActivityDepth;

        private CancellationToken ShowcaseTabCancellationToken => _showcaseTabCts?.Token ?? CancellationToken.None;

        bool IAiVideoGenControlsHost.TryBeginShowcaseTabWork()
        {
            return TryBeginShowcaseTabWork();
        }

        void IAiVideoGenControlsHost.EndShowcaseTabWork()
        {
            EndShowcaseTabWork();
        }

        Task IAiVideoGenControlsHost.HandleShowcaseStopResumeAsync()
        {
            return HandleShowcaseStopResumeAsync();
        }

        void IAiVideoGenControlsHost.RefreshShowcaseStopButton()
        {
            RefreshShowcaseStopButtonState();
        }

        bool IAiVideoGenControlsHost.IsShowcaseTabPaused => _showcaseTabPaused;

        private async Task RunShowcaseTabScopedWorkAsync(Func<Task> work)
        {
            if (work == null || !TryBeginShowcaseTabWork())
            {
                return;
            }

            try
            {
                await work().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                EndShowcaseTabWork();
            }
        }

        private bool TryBeginShowcaseTabWork()
        {
            if (_showcaseTabPaused)
            {
                LogShowcase("[Showcase] Tab đang dừng — bấm «Tiếp tục» (cam) rồi chạy lại thao tác.");
                return false;
            }

            if (_showcaseTabActivityDepth == 0)
            {
                RefreshShowcaseStopButtonState();
            }

            _showcaseTabActivityDepth++;
            return true;
        }

        private void EndShowcaseTabWork()
        {
            if (_showcaseTabActivityDepth > 0)
            {
                _showcaseTabActivityDepth--;
            }

            RefreshShowcaseStopButtonState();

            if (!_showcaseTabPaused && _showcaseTabActivityDepth == 0)
            {
                UpdateShowcaseRenderButtonState();
            }
        }

        private void ThrowIfShowcaseTabCancelled()
        {
            ShowcaseTabCancellationToken.ThrowIfCancellationRequested();
        }

        private void RefreshShowcaseStopButtonState()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshShowcaseStopButtonState));
                return;
            }

            if (_showcaseTabPaused)
            {
                ApplyShowcaseStopButtonUi(continueMode: true, enabled: true);
                return;
            }

            var busy = _showcaseTabActivityDepth > 0 || HasActiveShowcaseRenderJobs();
            ApplyShowcaseStopButtonUi(continueMode: false, enabled: busy);
        }

        private async Task HandleShowcaseStopResumeAsync()
        {
            if (_showcaseTabPaused)
            {
                _showcaseTabPaused = false;
                _showcaseTabCts?.Dispose();
                _showcaseTabCts = new CancellationTokenSource();
                ApplyShowcaseStopButtonUi(continueMode: false, enabled: false);
                SetShowcaseTabWorkflowButtonsEnabled(true);
                UpdateShowcaseRenderButtonState();
                LogShowcase("[Showcase] Tiếp tục — có thể bấm các nút workflow (không tự chạy lại job đã dừng).");
                return;
            }

            if (_showcaseTabActivityDepth <= 0 && !HasActiveShowcaseRenderJobs())
            {
                LogShowcase("[Showcase] Không có thao tác đang chạy để dừng.");
                return;
            }

            _showcaseTabPaused = true;
            try
            {
                _showcaseTabCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            CancelAllShowcaseTabJobs();
            SetShowcaseTabWorkflowButtonsEnabled(false);
            RefreshShowcaseStopButtonState();
            LogShowcase("[Showcase] Đang dừng mọi thao tác tab (Gemini, Zoom, audio, render hàng đợi)…");
            await Task.Yield();
        }

        private void CancelRunningShowcaseTabWorkForEmergencyStop()
        {
            var tabBusy = _showcaseTabActivityDepth > 0;
            var renderBusy = HasRunningShowcaseRenderJobs();
            if (!tabBusy && !renderBusy)
            {
                return;
            }

            try
            {
                _showcaseTabCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _showcaseTabCts?.Dispose();
            _showcaseTabCts = new CancellationTokenSource();

            CancelRunningShowcaseTabJobs();
            RefreshShowcaseStopButtonState();
            LogShowcase("[Showcase] Emergency stop — dừng thao tác đang chạy (render chờ/hẹn giờ giữ nguyên).");
        }

        private bool HasRunningShowcaseRenderJobs()
        {
            if (_globalJobQueue == null)
            {
                return false;
            }

            return _globalJobQueue.AllJobs.Any(j =>
                j != null &&
                j.Kind == OmniJobKind.AffiliateDeepRender &&
                GlobalJobQueue.IsRunningStatus(j.Status));
        }

        private void CancelRunningShowcaseTabJobs()
        {
            if (_globalJobQueue == null)
            {
                return;
            }

            var cancelled = _globalJobQueue.CancelWhere(j =>
                j != null &&
                j.Kind == OmniJobKind.AffiliateDeepRender &&
                GlobalJobQueue.IsRunningStatus(j.Status));

            if (cancelled > 0)
            {
                LogShowcase("[Showcase] Đã dừng " + cancelled + " job render đang chạy.");
            }
        }

        private void CancelAllShowcaseTabJobs()
        {
            if (_globalJobQueue == null)
            {
                return;
            }

            var cancelled = _globalJobQueue.CancelWhere(j =>
                j != null &&
                j.Kind == OmniJobKind.AffiliateDeepRender &&
                (j.Status == OmniJobStatus.Pending ||
                 j.Status == OmniJobStatus.RetryPending ||
                 j.Status == OmniJobStatus.Running ||
                 j.Status == OmniJobStatus.Processing));

            if (cancelled > 0)
            {
                LogShowcase("[Showcase] Đã hủy " + cancelled + " job render trong hàng đợi.");
            }
        }

        private void ApplyShowcaseStopButtonUi(bool continueMode, bool enabled)
        {
            _aiVideoGenControls?.ApplyShowcaseStopButtonUi(continueMode, enabled);
        }

        private void SetShowcaseTabWorkflowButtonsEnabled(bool enabled)
        {
            _aiVideoGenControls?.SetShowcaseWorkflowButtonsEnabled(enabled);
            if (!enabled)
            {
                SetShowcaseExecuteButtonsEnabled(false, false);
                return;
            }

            UpdateShowcaseRenderButtonState();
        }

        private CancellationTokenSource CreateShowcaseLinkedJobCancellation()
        {
            return CancellationTokenSource.CreateLinkedTokenSource(ShowcaseTabCancellationToken);
        }
    }
}
