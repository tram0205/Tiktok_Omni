using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using tiktok_Omni.Helpers;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private Button btnEmergencyStop;

        private void btnEmergencyStop_Click(object sender, EventArgs e)
        {
            try
            {
                CancelAllApplicationWorkForEmergencyStop();
                KillZombieBrowserProcesses();
                KillOrphanFfmpegProcesses();
                Log("[EMERGENCY] Đã dừng mọi thao tác đang chạy (job chờ/hẹn giờ giữ nguyên).");
                MessageBox.Show(
                    this,
                    "Đã dừng mọi thao tác đang chạy.\r\n\r\n"
                    + "Job trong hàng đợi và lịch hẹn giữ nguyên — đến giờ vẫn thực hiện bình thường.",
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
            CancelRunningWarmupWorkForEmergencyStop();
            CancelRunningAffiliateBrowserWorkForEmergencyStop();

            if (_affiliateAutoEnrichRunning)
            {
                TryCancel(_affiliateAutoEnrichCts);
            }

            if (_affiliateRowEnrichRunning)
            {
                TryCancel(_affiliateRowEnrichCts);
            }

            if (_affiliateDownloadingBatch)
            {
                TryCancel(_affiliateDownloadBatchCts);
            }

            TryCancel(_huntProductCancellation);
            TryCancel(_aiVideoGenCancellation);
            CancelAllPhilosophyWorkForEmergencyStop();
            CancelRunningShowcaseTabWorkForEmergencyStop();
            CancelRunningVideoReupWorkForEmergencyStop();

            if (_mascotBatchCts != null)
            {
                TryCancel(_mascotBatchCts);
            }

            if (_channelHealthCycleRunning)
            {
                TryCancel(_channelHealthCycleCts);
            }

            TryCancel(_activeJobCancellation);

            var stoppedJobs = _globalJobQueue?.CancelRunningOnly() ?? 0;
            if (stoppedJobs > 0)
            {
                Log("[EMERGENCY] Đã dừng " + stoppedJobs + " job đang chạy trong hàng đợi.");
            }
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
                var count = GetActiveGridRowCountForClear();
                if (count <= 0)
                {
                    return;
                }

                if (!UiConfirmHelper.ConfirmDeleteRows(this, count))
                {
                    return;
                }

                ClearActiveAiVideoGenModeBuffer();
            }
            catch (Exception ex)
            {
                Log("[Grid] Làm sạch buffer lỗi: " + ex.Message);
            }
        }

        private int GetActiveGridRowCountForClear()
        {
            switch (_selectedAiVideoGenMode)
            {
                case AiVideoGenMode.Slideshow:
                    return GetSlideshowBuffer()?.Count ?? 0;
                case AiVideoGenMode.AffiliateDeep:
                    return GetShowcaseVideoBuffer()?.Count ?? 0;
                case AiVideoGenMode.Mascot:
                    return _mascotPreviewSceneScripts?.Count ?? 0;
                case AiVideoGenMode.Philosophy:
                    return string.IsNullOrWhiteSpace(txtPhilosophyTopic?.Text) ? 0 : 1;
                case AiVideoGenMode.VideoReup:
                    return _videoReupBindingList?.Count ?? 0;
                default:
                    return 0;
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
                    GetShowcaseVideoBuffer().Clear();
                    _activeShowcaseVideoId = null;
                    _showcaseSession = null;
                    NotifyShowcaseDraftDirty();
                    RefreshAffiliateDeepStoryboard();
                    Log("[Grid] Đã làm sạch lưới video Showcase.");
                    break;
                case AiVideoGenMode.Mascot:
                    _mascotPreviewSceneScripts?.Clear();
                    Log("[Grid] Đã làm sạch preview Mascot.");
                    break;
                case AiVideoGenMode.Philosophy:
                    if (txtPhilosophyTopic != null)
                    {
                        txtPhilosophyTopic.Clear();
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

        /// <summary>Các dòng đang hiển thị trên lưới affiliate (khi không tô dòng nào).</summary>
        private List<AffiliateCandidate> GetVisibleAffiliateCandidatesForPush()
        {
            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                return new List<AffiliateCandidate>();
            }

            return _affiliateBindingList.Where(x => x != null).ToList();
        }

        /// <summary>Chụp dòng đang tô trên lưới affiliate (trước dialog — tránh mất selection khi ShowDialog).</summary>
        private List<AffiliateCandidate> GetSelectedAffiliateCandidatesSnapshot()
        {
            var list = new List<AffiliateCandidate>();
            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                return list;
            }

            foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows
                         .Cast<DataGridViewRow>()
                         .Where(r => r != null && !r.IsNewRow)
                         .OrderBy(r => r.Index))
            {
                if (row.DataBoundItem is AffiliateCandidate candidate && candidate != null)
                {
                    list.Add(candidate);
                }
            }

            return list;
        }

        /// <summary>Các dòng để đẩy: ưu tiên snapshot đã chọn, không thì toàn bộ lưới đang hiển thị.</summary>
        private List<AffiliateCandidate> ResolveAffiliateCandidatesForPush(IList<AffiliateCandidate> selectedOverride)
        {
            if (selectedOverride != null && selectedOverride.Count > 0)
            {
                return selectedOverride
                    .Where(x => x != null)
                    .GroupBy(x => (x.VideoUrl ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }

            return GetVisibleAffiliateCandidatesForPush();
        }

        private void PushAffiliateRowsToAiVideoGen(bool targetDeepDive, IList<AffiliateCandidate> selectedOverride = null)
        {
            var visible = ResolveAffiliateCandidatesForPush(selectedOverride);
            if (visible.Count == 0)
            {
                Log("Không có dòng affiliate để đẩy sang AI Video Gen (hãy tô dòng hoặc săn trước).");
                return;
            }

            var mapped = visible
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

            var selectionNote = selectedOverride != null && selectedOverride.Count > 0
                ? $"{selectedOverride.Count} dòng đã tô"
                : "toàn bộ lưới đang hiển thị";
            var onlyHigh = chkAffiliateOnlyHighQuality?.Checked ?? false;
            Log(
                $"Đã đẩy {mapped.Count} sản phẩm ({selectionNote}{(onlyHigh && selectedOverride == null ? ", lọc HQ" : string.Empty)}) sang " +
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

        /// <summary>Giữ tên cũ cho pipeline công nghiệp — đẩy toàn bộ lưới hiển thị.</summary>
        private void PushVisibleAffiliateRowsToAiVideoGen(bool targetDeepDive) =>
            PushAffiliateRowsToAiVideoGen(targetDeepDive, selectedOverride: null);

        private void PushAffiliateRowsToVideoReup(IList<AffiliateCandidate> selectedOverride = null)
        {
            var visible = ResolveAffiliateCandidatesForPush(selectedOverride);
            if (visible.Count == 0)
            {
                LogVideoReup("Video reup: không có dòng affiliate để đẩy (hãy tô dòng hoặc săn trước).");
                return;
            }

            PushAffiliateCandidatesToVideoReup(visible);
        }

        private void PushVisibleAffiliateRowsToVideoReup() =>
            PushAffiliateRowsToVideoReup(selectedOverride: null);

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
