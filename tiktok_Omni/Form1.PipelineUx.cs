using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private Button btnEmergencyStop;
        private Button btnClearAiGenGrid;
        private Button btnOpenOutputFolder;

        private void btnEmergencyStop_Click(object sender, EventArgs e)
        {
            try
            {
                CancelAllApplicationWorkForEmergencyStop();
                KillZombieBrowserProcesses();
                KillOrphanFfmpegProcesses();
                Log("[EMERGENCY] Đã ép dừng mọi tiến trình (queue, job, browser, ffmpeg).");
                MessageBox.Show(
                    this,
                    "Đã ép dừng mọi tiến trình!",
                    "Dừng khẩn cấp",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Log("[EMERGENCY] Lỗi khi dừng khẩn cấp: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Dừng khẩn cấp gặp lỗi: " + ex.Message,
                    "Dừng khẩn cấp",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CancelAllApplicationWorkForEmergencyStop()
        {
            CancelWarmupBrowserWork();
            CancelAffiliateBrowserWork();
            TryCancel(_affiliateAutoEnrichCts);
            TryCancel(_affiliateRowEnrichCts);
            TryCancel(_affiliateCategorizeCts);
            TryCancel(_aiVideoGenCancellation);
            DisposeActiveJobCancellation();

            _globalJobQueue?.ClearAll();
        }

        private static void KillOrphanFfmpegProcesses()
        {
            var currentPid = Process.GetCurrentProcess().Id;
            Process[] processes = null;
            try
            {
                processes = Process.GetProcessesByName("ffmpeg");
                foreach (var process in processes)
                {
                    if (process == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (process.Id == currentPid)
                        {
                            continue;
                        }

                        process.Kill();
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        try
                        {
                            process.Dispose();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
            finally
            {
                if (processes != null)
                {
                    foreach (var p in processes)
                    {
                        try
                        {
                            p?.Dispose();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
        }

        private void btnClearAiGenGrid_Click(object sender, EventArgs e)
        {
            try
            {
                ClearActiveAiVideoGenModeBuffer();
            }
            catch (Exception ex)
            {
                Log("[Grid] Làm sạch buffer lỗi: " + ex.Message);
            }
        }

        private void ClearActiveAiVideoGenModeBuffer()
        {
            switch (_selectedAiVideoGenMode)
            {
                case AiVideoGenMode.Slideshow:
                    GetSlideshowBuffer().Clear();
                    _aiVideoScriptBindingList?.Clear();
                    if (txtAiVideoGenPrompt != null)
                    {
                        txtAiVideoGenPrompt.Text = string.Empty;
                    }

                    NotifySlideshowDraftDirty();
                    Log("[Grid] Đã làm sạch buffer Slideshow.");
                    break;
                case AiVideoGenMode.AffiliateDeep:
                    GetDeepDiveBuffer().Clear();
                    RefreshAffiliateDeepStoryboard();
                    Log("[Grid] Đã làm sạch storyboard Affiliate Deep.");
                    break;
                case AiVideoGenMode.Mascot:
                    _mascotPreviewSceneScripts?.Clear();
                    _mascotPreviewImagePaths?.Clear();
                    _selectedMascotPreviewSceneIndex = -1;
                    Log("[Grid] Đã làm sạch preview Mascot.");
                    break;
                case AiVideoGenMode.Philosophy:
                    if (txtPhilosophyInput != null)
                    {
                        txtPhilosophyInput.Clear();
                    }

                    Log("[Grid] Đã làm sạch nội dung Triết lý.");
                    break;
                case AiVideoGenMode.VideoReup:
                    _videoReupBindingList?.Clear();
                    _videoReupDraftDirty = true;
                    LogVideoReup("[Grid] Đã làm sạch bảng Video Reup.");
                    break;
            }

            SyncBuffersToGrids();
        }

        private async void btnOpenOutputFolder_Click(object sender, EventArgs e)
        {
            if (btnOpenOutputFolder != null)
            {
                btnOpenOutputFolder.Enabled = false;
            }

            try
            {
                await OpenProfileOutputFolderInExplorerAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("[Folder] Không mở được thư mục output: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Không mở được thư mục: " + ex.Message,
                    "Thư mục output",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                if (btnOpenOutputFolder != null && !btnOpenOutputFolder.IsDisposed)
                {
                    btnOpenOutputFolder.Enabled = true;
                }
            }
        }

        private async System.Threading.Tasks.Task OpenProfileOutputFolderInExplorerAsync()
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(settings.StorageRootPath, profile);
            var path = ProfileScopedPaths.GetGeneratedRoot(profile);
            Directory.CreateDirectory(path);

            try
            {
                Process.Start("explorer.exe", path);
                Log("[Folder] Đã mở: " + path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("explorer.exe: " + ex.Message, ex);
            }
        }

        /// <summary>Các dòng đang hiển thị trên lưới affiliate (đã áp filter HQ nếu bật).</summary>
        private List<AffiliateCandidate> GetVisibleAffiliateCandidatesForPush()
        {
            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                return new List<AffiliateCandidate>();
            }

            if (dgvAffiliateResults?.SelectedRows != null && dgvAffiliateResults.SelectedRows.Count > 0)
            {
                var selected = new List<AffiliateCandidate>();
                foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
                {
                    if (row?.DataBoundItem is AffiliateCandidate candidate)
                    {
                        selected.Add(candidate);
                    }
                }

                if (selected.Count > 0)
                {
                    return selected;
                }
            }

            return _affiliateBindingList.Where(x => x != null).ToList();
        }

        private void PushVisibleAffiliateRowsToAiVideoGen(bool targetDeepDive)
        {
            var visible = GetVisibleAffiliateCandidatesForPush();
            if (visible.Count == 0)
            {
                Log("Không có dòng affiliate hiển thị trên lưới để đẩy sang AI Video Gen.");
                return;
            }

            var mapped = visible
                .GroupBy(x => (x.VideoUrl ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(MapAffiliateToAiVideoInput)
                .Where(x => x != null)
                .ToList();

            PushMappedProductsToTargetBuffer(mapped, targetDeepDive);

            var withImage = mapped.Count(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
            var nicks = mapped
                .Select(x => ProfileScopedPaths.ResolveProfileName(x.ProfileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _aiVideoScriptBindingList?.Clear();
            if (txtAiVideoGenPrompt != null)
            {
                txtAiVideoGenPrompt.Text = string.Empty;
            }

            var onlyHigh = chkAffiliateOnlyHighQuality?.Checked ?? false;
            Log(
                $"Đã đẩy {mapped.Count} sản phẩm (lưới đang hiển thị{(onlyHigh ? ", lọc HQ" : string.Empty)}) sang " +
                $"{(targetDeepDive ? "Affiliate Deep" : "Slideshow")} (có ảnh: {withImage}, nick: {string.Join(", ", nicks)}).");

            if (!targetDeepDive)
            {
                NotifySlideshowDraftDirty();
            }
            else
            {
                RefreshAffiliateDeepStoryboard();
            }

            SyncBuffersToGrids();
            SwitchToMainTab(tabAiVideoGen);
        }

        private void PushVisibleAffiliateRowsToVideoReup()
        {
            var visible = GetVisibleAffiliateCandidatesForPush();
            if (visible.Count == 0)
            {
                LogVideoReup("Video reup: không có dòng affiliate trên lưới để đẩy.");
                return;
            }

            PushAffiliateCandidatesToVideoReup(visible);
        }

        private Button CreateClearGridButton()
        {
            var btn = new Button
            {
                Name = "btnClearAiGenGrid",
                Text = "🗑 Làm sạch",
                AutoSize = true,
                Height = 34,
                Margin = new Padding(6, 0, 0, 0)
            };
            btn.ApplyTheme(ButtonRole.Danger);
            btn.Click += btnClearAiGenGrid_Click;
            return btn;
        }

        private Button CreateOpenOutputFolderButton()
        {
            var btn = new Button
            {
                Name = "btnOpenOutputFolder",
                Text = "📂 Output",
                AutoSize = true,
                Height = 34,
                Margin = new Padding(6, 0, 0, 0)
            };
            btn.ApplyTheme(ButtonRole.Neutral);
            btn.Click += btnOpenOutputFolder_Click;
            return btn;
        }
    }
}
