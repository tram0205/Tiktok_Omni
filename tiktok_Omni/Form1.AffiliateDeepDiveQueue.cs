using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    /// <summary>Deep Dive qua GlobalJobQueue + script preview / sửa script trên lưới Slideshow / Deep.</summary>
    public partial class Form1
    {
        private async void btnAffiliateDeepDive_Click(object sender, EventArgs e)
        {
            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                MessageBox.Show(this,
                    "Hãy chọn ít nhất một dòng trong bảng affiliate.",
                    "Deep Dive",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var selected = new List<AffiliateCandidate>();
            foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
            {
                if (row?.DataBoundItem is AffiliateCandidate c)
                {
                    selected.Add(c);
                }
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(this,
                    "Không đọc được dòng đã chọn — thử chọn lại trên lưới.",
                    "Deep Dive",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            AppSettings settings;
            try
            {
                settings = await _configManager.LoadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("Không nạp được cài đặt: " + ex.Message);
                return;
            }

            var ytFromUi = (txtYtDlpPath?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(ytFromUi))
            {
                settings.YtDlpPath = ytFromUi;
            }

            var ffFromUi = (txtFfmpegPath?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(ffFromUi))
            {
                settings.FfmpegPath = ffFromUi;
            }

            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                MessageBox.Show(this,
                    "Chưa cấu hình AI API Key trong tab Cài đặt.",
                    "Deep Dive",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var minSafety = (int)(numAffiliateMinSafety?.Value ?? 75);
            var enqueued = 0;
            var skippedScore = 0;
            foreach (var c in selected)
            {
                if (c.SafetyScore <= minSafety)
                {
                    skippedScore++;
                    continue;
                }

                EnqueueAffiliateDeepDiveJob(c, settings, minSafety, highPriority: false);
                enqueued++;
            }

            Log($"[JobQueue] Deep Dive: đã xếp {enqueued} job (bỏ {skippedScore} dòng score ≤ {minSafety}).");
            RefreshAffiliateToolbarButtons();
        }

        private void AfterAiVideoGenScriptEdited()
        {
            if (IsSlideshowModeTab())
            {
                NotifySlideshowDraftDirty();
            }

            SyncBuffersToGrids();
        }

        private void SetAiGenScriptButtonsEnabled(bool enabled)
        {
            if (btnAffiliateGenerateScript != null && !btnAffiliateGenerateScript.IsDisposed)
            {
                btnAffiliateGenerateScript.Enabled = enabled;
            }

            if (btnDeepGenerateScript != null && !btnDeepGenerateScript.IsDisposed)
            {
                btnDeepGenerateScript.Enabled = enabled;
            }

            if (btnAffiliateEditScript != null && !btnAffiliateEditScript.IsDisposed)
            {
                btnAffiliateEditScript.Enabled = enabled;
            }

            if (btnDeepEditScript != null && !btnDeepEditScript.IsDisposed)
            {
                btnDeepEditScript.Enabled = enabled;
            }
        }

        private bool TryGetSelectedAiVideoGenItems(out List<AiVideoGenInputItem> list)
        {
            list = new List<AiVideoGenInputItem>();
            var grid = GetActiveProductGrid();
            if (grid?.SelectedRows == null)
            {
                return false;
            }

            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                if (row?.DataBoundItem is AiVideoGenInputItem item)
                {
                    list.Add(item);
                }
            }

            return list.Count > 0;
        }

        private bool TryGetSingleSelectedAiVideoGenItem(out AiVideoGenInputItem item)
        {
            item = null;
            if (!TryGetSelectedAiVideoGenItems(out var list) || list.Count != 1)
            {
                return false;
            }

            item = list[0];
            return item != null;
        }

        private bool TryGetSelectedAffiliateCandidates(out List<AffiliateCandidate> list)
        {
            list = new List<AffiliateCandidate>();
            if (dgvAffiliateResults?.SelectedRows == null)
            {
                return false;
            }

            foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
            {
                if (row?.DataBoundItem is AffiliateCandidate c)
                {
                    list.Add(c);
                }
            }

            return list.Count > 0;
        }

        private void miAffiliatePriorityDeepDive_Click(object sender, EventArgs e)
        {
            if (!TryGetSingleSelectedAffiliateCandidate(out var candidate))
            {
                return;
            }

            var settings = _configManager.LoadAsync().GetAwaiter().GetResult();
            var minSafety = (int)(numAffiliateMinSafety?.Value ?? 75);
            var url = (candidate.VideoUrl ?? string.Empty).Trim();
            CancelAffiliateDeepDiveJobForUrl(url);
            EnqueueAffiliateDeepDiveJob(candidate, settings, minSafety, highPriority: true);
        }

        private void miAffiliateCancelDeepDive_Click(object sender, EventArgs e)
        {
            if (!TryGetSingleSelectedAffiliateCandidate(out var candidate))
            {
                return;
            }

            CancelAffiliateDeepDiveJobForUrl(candidate.VideoUrl);
        }

        private static void ApplyDeepDiveResultToCandidate(AffiliateCandidate candidate, VideoDeepAnalysisResult result)
        {
            if (candidate == null || result == null)
            {
                return;
            }

            candidate.VideoScript = result.VideoScript ?? string.Empty;
            candidate.VoiceoverTranscript = result.VoiceoverTranscript ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(result.LinkedProduct))
            {
                var existing = (candidate.LinkedProduct ?? string.Empty).Trim();
                var isPlaceholder = string.IsNullOrEmpty(existing)
                                    || string.Equals(existing, "Chưa rõ", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(existing, "Không hiện giỏ hàng", StringComparison.OrdinalIgnoreCase);
                if (isPlaceholder)
                {
                    candidate.LinkedProduct = result.LinkedProduct.Trim();
                }
            }
        }

        private void RefreshAffiliateToolbarButtons()
        {
            // Deep Dive luôn bấm được — handler tự báo khi thiếu chọn dòng.
        }
    }
}
