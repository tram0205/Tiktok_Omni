using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private string _reupPreviewSourcePath = string.Empty;
        private string _reupPreviewOutputPath = string.Empty;

        private void LayoutVideoReupShell()
        {
            if (dgvVideoReupInput == null)
            {
                return;
            }

            dgvVideoReupInput.EnableHeadersVisualStyles = false;
            if (dgvVideoReupInput.ColumnHeadersHeight < AppGridHeaderHeight)
            {
                dgvVideoReupInput.ColumnHeadersHeight = AppGridHeaderHeight;
            }

            dgvVideoReupInput.Dock = DockStyle.Fill;
        }

        private void SetReupPreviewStage(string statusText)
        {
            // Progress label is updated by SetVideoReupProgress; preview path refresh is row-driven.
            _ = statusText;
        }

        private async Task RefreshReupPreviewForRowAsync(VideoReupRowItem row, string stageHint = null)
        {
            if (row == null)
            {
                _reupPreviewSourcePath = string.Empty;
                _reupPreviewOutputPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(stageHint))
                {
                    SetVideoReupProgress(stageHint, 0);
                }

                return;
            }

            _reupPreviewSourcePath = (row.ReupDownloadedVideoPath ?? string.Empty).Trim();
            _reupPreviewOutputPath = (row.LastRemixOutputPath ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(stageHint))
            {
                SetVideoReupProgress(stageHint, pbVideoReupProgress?.Value ?? 0);
            }

            await Task.CompletedTask.ConfigureAwait(true);
        }

        private void PlayReupPreviewVideo(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                LogVideoReup("Xem video: chưa có file (tải nguồn hoặc render trước).");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LogVideoReup("Đã mở video: " + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                LogVideoReup("Không mở được video: " + ex.Message);
            }
        }
    }
}
