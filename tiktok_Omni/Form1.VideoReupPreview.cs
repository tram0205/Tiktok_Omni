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

            ApplyAppGridChrome(dgvVideoReupInput);

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
                SetVideoReupProgress(stageHint, 0);
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
        private async void btnVideoReupOpenOutput_Click(object sender, EventArgs e)
        {
            if (btnVideoReupOpenOutput != null)
            {
                btnVideoReupOpenOutput.Enabled = false;
            }

            try
            {
                await OpenVideoReupOutputFolderInExplorerAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogVideoReup("[Folder] Không mở được thư mục output Reup: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Không mở được thư mục: " + ex.Message,
                    "Thư mục output Reup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                if (btnVideoReupOpenOutput != null && !btnVideoReupOpenOutput.IsDisposed)
                {
                    btnVideoReupOpenOutput.Enabled = true;
                }
            }
        }

        private async Task OpenVideoReupOutputFolderInExplorerAsync()
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            if (TryGetVideoReupSelectedRow(out var row) && !string.IsNullOrWhiteSpace(row?.ProfileName))
            {
                profile = ProfileScopedPaths.ResolveProfileName(row.ProfileName);
            }

            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(settings.StorageRootPath, profile);
            var path = ProfileScopedPaths.GetVideoReupOutputRoot(profile);
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path + "\"",
                UseShellExecute = true
            });
            LogVideoReup("[Folder] Đã mở output Reup: " + path);
        }
    }
}
