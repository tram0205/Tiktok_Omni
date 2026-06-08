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

        private async void btnAffiliateGenerateScript_Click(object sender, EventArgs e)
        {
            if (!IsProductPipelineModeTab())
            {
                MessageBox.Show(this,
                    "Chọn tab Slideshow hoặc Affiliate chuyên sâu trước khi sinh script.",
                    "Sinh Script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!TryGetSelectedAiVideoGenItems(out var selected))
            {
                MessageBox.Show(this,
                    "Hãy chọn ít nhất một dòng sản phẩm trên lưới trước khi sinh script.",
                    "Sinh Script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                MessageBox.Show(this, "Cần AI API Key.", "Sinh script", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetAiGenScriptButtonsEnabled(false);
            try
            {
                var modeLabel = IsDeepDiveModeTab() ? "Affiliate Deep" : "Slideshow";
                foreach (var item in selected)
                {
                    try
                    {
                        var script = await _affiliateScriptPreviewService
                            .GenerateVoiceoverPreviewAsync(item, settings, CancellationToken.None)
                            .ConfigureAwait(true);
                        item.ScriptPreview = (script ?? string.Empty).Trim();
                        Log($"[Script] ({modeLabel}) Đã sinh script: {item.ProductName}");
                    }
                    catch (Exception ex)
                    {
                        Log("[Script] Lỗi: " + ex.Message);
                    }
                }

                AfterAiVideoGenScriptEdited();
            }
            finally
            {
                SetAiGenScriptButtonsEnabled(true);
            }
        }

        private void btnAffiliateEditScript_Click(object sender, EventArgs e)
        {
            if (!IsProductPipelineModeTab())
            {
                MessageBox.Show(this,
                    "Chọn tab Slideshow hoặc Affiliate chuyên sâu trước khi sửa script.",
                    "Sửa Script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!TryGetSingleSelectedAiVideoGenItem(out var item))
            {
                MessageBox.Show(this, "Chọn một dòng sản phẩm để sửa script.", "Sửa Script",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new Form
            {
                Text = "Sửa Voiceover / Script",
                StartPosition = FormStartPosition.CenterParent,
                Size = new System.Drawing.Size(720, 480),
                BackColor = System.Drawing.Color.FromArgb(31, 34, 42),
                ForeColor = System.Drawing.Color.Gainsboro
            })
            {
                var box = new TextBox
                {
                    Multiline = true,
                    Dock = DockStyle.Fill,
                    ScrollBars = ScrollBars.Vertical,
                    Text = item.ScriptPreview ?? string.Empty,
                    BackColor = System.Drawing.Color.FromArgb(45, 49, 60),
                    ForeColor = System.Drawing.Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new System.Drawing.Font("Segoe UI", 10F)
                };
                var panel = new Panel { Dock = DockStyle.Bottom, Height = 44 };
                var btnOk = new Button
                {
                    Text = "Lưu",
                    DialogResult = DialogResult.OK,
                    Location = new System.Drawing.Point(12, 8),
                    Size = new System.Drawing.Size(100, 28)
                };
                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Location = new System.Drawing.Point(120, 8),
                    Size = new System.Drawing.Size(100, 28)
                };
                panel.Controls.Add(btnOk);
                panel.Controls.Add(btnCancel);
                dlg.Controls.Add(box);
                dlg.Controls.Add(panel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    item.ScriptPreview = box.Text?.Trim() ?? string.Empty;
                    AfterAiVideoGenScriptEdited();
                    Log("[Script] Đã lưu chỉnh sửa script.");
                }
            }
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
