using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Affiliate Hunter: lưới kết quả, enrich metrics/anchor. Deep Dive queue: Form1.AffiliateDeepDiveQueue.</summary>
    public partial class Form1
    {
        private static readonly TimeSpan AffiliateAutoEnrichRebindThrottle = TimeSpan.FromMilliseconds(800);

        /// <summary>Gọi TikWM, gán metrics và tính lại engagement score (Score).</summary>
        private async Task ApplyAffiliateMetricsFromTikWmAsync(AffiliateCandidate candidate, CancellationToken cancellationToken)
        {
            if (candidate == null)
            {
                return;
            }

            var url = (candidate.VideoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            var metrics = await _affiliateHunter
                .EnrichVideoMetricsAsync(url, Log, cancellationToken)
                .ConfigureAwait(false);

            candidate.PlayCount = metrics.PlayCount;
            candidate.LikeCount = metrics.LikeCount;
            candidate.CommentCount = metrics.CommentCount;
            candidate.ShareCount = metrics.ShareCount;
            candidate.CollectCount = metrics.CollectCount;
            candidate.DurationSeconds = metrics.DurationSeconds;
            candidate.CreateTimeUtc = metrics.CreateTimeUtc;
            candidate.MetricsCapturedAtUtc = DateTime.UtcNow;
            candidate.LastMetricsError = string.Empty;

            var newScore = _safetyScoreService.ScoreAffiliateCandidate(candidate);
            candidate.SafetyScore = newScore.Score;
            candidate.SafetyRiskSummary = string.Join(" | ", newScore.Reasons);
        }

        private static void SortAffiliateCandidatesByEngagement(List<AffiliateCandidate> items)
        {
            if (items == null || items.Count <= 1)
            {
                return;
            }

            items.Sort((a, b) =>
            {
                var pa = a?.PlayCount ?? 0;
                var pb = b?.PlayCount ?? 0;
                if (pb != pa)
                {
                    return pb.CompareTo(pa);
                }

                var sa = a?.SafetyScore ?? 0;
                var sb = b?.SafetyScore ?? 0;
                if (sb != sa)
                {
                    return sb.CompareTo(sa);
                }

                var la = a?.LikeCount ?? 0;
                var lb = b?.LikeCount ?? 0;
                return lb.CompareTo(la);
            });
        }

        /// <summary>
        /// Sau khi Hunt buffer: enrich TikWM toàn bộ ứng viên, sort theo engagement, giữ top <paramref name="keepTop"/>.
        /// Dòng đã có metrics gần đây vẫn được fetch lại để đồng nhất khi xếp hạng.
        /// </summary>
        private async Task<List<AffiliateCandidate>> RankAffiliateBatchByEngagementAsync(
            IList<AffiliateCandidate> batch,
            int keepTop,
            CancellationToken cancellationToken)
        {
            if (batch == null || batch.Count == 0)
            {
                return new List<AffiliateCandidate>();
            }

            var working = batch.Where(c => c != null).ToList();
            var top = Math.Max(1, keepTop);

            if (working.Count == 0)
            {
                return new List<AffiliateCandidate>();
            }

            Log($"[Affiliate] Xếp hạng engagement (TikWM): {working.Count} ứng viên → giữ top {Math.Min(top, working.Count)} ...");

            for (var i = 0; i < working.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var c = working[i];
                var url = (c?.VideoUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                try
                {
                    await ApplyAffiliateMetricsFromTikWmAsync(c, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    c.LastMetricsError = ex.Message;
                }

                if (i < working.Count - 1)
                {
                    try
                    {
                        await Task.Delay(1100, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }

            SortAffiliateCandidatesByEngagement(working);

            if (working.Count <= top)
            {
                Log($"[Affiliate] Đã xếp hạng {working.Count} dòng (≤ Max Results).");
                return working;
            }

            var trimmed = working.Take(top).ToList();
            Log($"[Affiliate] Đã giữ top {trimmed.Count}/{working.Count} theo engagement score.");
            return trimmed;
        }

        private static string TruncateForPreview(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        // ===== Refresh manual cho 1 dòng (qua context menu chuột phải) =====
        private async Task RefreshSelectedAffiliateMetricsAsync()
        {
            await RunSingleRowEnrichAsync(refreshMetrics: true, refreshAnchor: false).ConfigureAwait(true);
        }

        private async Task RescanSelectedAffiliateAnchorAsync()
        {
            await RunSingleRowEnrichAsync(refreshMetrics: false, refreshAnchor: true).ConfigureAwait(true);
        }

        private async Task RunSingleRowEnrichAsync(bool refreshMetrics, bool refreshAnchor)
        {
            if (_affiliateRowEnrichRunning)
            {
                Log("[Enrich] Đang xử lý dòng khác, vui lòng đợi.");
                return;
            }

            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                Log("Hãy chọn một dòng trong bảng affiliate trước.");
                return;
            }

            if (_affiliateHunter == null)
            {
                Log("AffiliateHunter chưa sẵn sàng.");
                return;
            }

            var row = dgvAffiliateResults.SelectedRows[0];
            if (!(row?.DataBoundItem is AffiliateCandidate candidate))
            {
                return;
            }

            var url = (candidate.VideoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                Log("Dòng này không có VideoUrl.");
                return;
            }

            _affiliateRowEnrichRunning = true;
            _affiliateRowEnrichCts?.Dispose();
            _affiliateRowEnrichCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var token = _affiliateRowEnrichCts.Token;

            var labelParts = new List<string>();
            if (refreshMetrics) labelParts.Add("số liệu");
            if (refreshAnchor) labelParts.Add("link affiliate");
            var actionLabel = string.Join(" + ", labelParts);
            UpdateAffiliateEnrichStatus($"⏳ Refresh {actionLabel} cho dòng: {url} ...");

            try
            {
                if (refreshMetrics)
                {
                    try
                    {
                        await ApplyAffiliateMetricsFromTikWmAsync(candidate, token).ConfigureAwait(true);
                        Log($"[Enrich] ✅ Refresh số liệu OK (engagement={candidate.SafetyScore}/100): {url}");
                    }
                    catch (Exception ex)
                    {
                        candidate.LastMetricsError = ex.Message;
                        Log($"[Enrich] Refresh số liệu lỗi: {ex.Message}");
                    }
                }

                if (refreshAnchor && !token.IsCancellationRequested)
                {
                    try
                    {
                        var anchor = await _affiliateHunter
                            .EnrichLinkedProductAsync(url, Log, token)
                            .ConfigureAwait(true);
                        if (!string.IsNullOrWhiteSpace(anchor))
                        {
                            candidate.LinkedProduct = anchor.Trim();
                            Log($"[Enrich] ✅ Anchor cập nhật: {anchor}");
                        }
                        else
                        {
                            candidate.LinkedProduct = "Video không gắn anchor sản phẩm";
                            Log("[Enrich] Anchor không tìm thấy.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Enrich] Quét anchor lỗi: {ex.Message}");
                    }
                }

                _affiliateBindingList?.ResetBindings();
                UpdateAffiliateEnrichStatus($"✅ Đã refresh {actionLabel} cho dòng.");
            }
            finally
            {
                _affiliateRowEnrichRunning = false;
            }
        }

        // ===== Auto enrich (Metrics → Anchor) sau khi Hunt xong toàn bộ keyword =====
        private void UpdateAffiliateRankControlsEnabledState()
        {
            var rankOn = chkAffiliateRankByEngagement?.Checked ?? true;
            if (numAffiliateBufferMultiplier != null)
            {
                numAffiliateBufferMultiplier.Enabled = rankOn;
            }

            if (chkTikTokApiFallbackBrowser != null)
            {
                chkTikTokApiFallbackBrowser.Enabled = cbTikTokHuntMethod?.SelectedIndex == 0;
            }
        }

        private void ScheduleAffiliateHuntPrefsSave()
        {
            if (_affiliateHuntPrefsSaveTimer == null)
            {
                _affiliateHuntPrefsSaveTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _affiliateHuntPrefsSaveTimer.Tick += async (s, _) =>
                {
                    _affiliateHuntPrefsSaveTimer.Stop();
                    try
                    {
                        var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                        settings.AffiliateRankByEngagementEnabled = chkAffiliateRankByEngagement?.Checked ?? true;
                        if (numAffiliateBufferMultiplier != null)
                        {
                            settings.AffiliateHuntBufferMultiplier = (double)numAffiliateBufferMultiplier.Value;
                        }

                        if (cbTikTokHuntMethod != null && cbTikTokHuntMethod.SelectedIndex >= 0)
                        {
                            settings.TikTokHuntMethod = cbTikTokHuntMethod.SelectedIndex == 0
                                ? TikTokHuntMethods.RapidApi
                                : TikTokHuntMethods.Browser;
                        }

                        if (chkTikTokApiFallbackBrowser != null)
                        {
                            settings.TikTokRapidApiFallbackToBrowser = chkTikTokApiFallbackBrowser.Checked;
                        }

                        await _configManager.SaveAsync(settings).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Log("[Affiliate] Không lưu được cài đặt săn/xếp hạng: " + ex.Message);
                    }
                };
            }

            _affiliateHuntPrefsSaveTimer.Stop();
            _affiliateHuntPrefsSaveTimer.Start();
        }

        private void cbTikTokHuntMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateAffiliateRankControlsEnabledState();
            ScheduleAffiliateHuntPrefsSave();
        }

        private void chkAffiliateRankByEngagement_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAffiliateRankControlsEnabledState();
            ScheduleAffiliateHuntPrefsSave();
        }

        private void numAffiliateBufferMultiplier_ValueChanged(object sender, EventArgs e)
        {
            ScheduleAffiliateHuntPrefsSave();
        }

        private void chkAffiliateAutoEnrich_CheckedChanged(object sender, EventArgs e)
        {
            // Debounce save 500ms để tránh ghi file mỗi lần check/uncheck nhanh.
            if (_affiliateAutoEnrichSaveTimer == null)
            {
                _affiliateAutoEnrichSaveTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _affiliateAutoEnrichSaveTimer.Tick += async (s, _) =>
                {
                    _affiliateAutoEnrichSaveTimer.Stop();
                    try
                    {
                        var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                        settings.AffiliateAutoEnrichEnabled = chkAffiliateAutoEnrich?.Checked ?? true;
                        await _configManager.SaveAsync(settings).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Log("[Enrich] Không lưu được setting AutoEnrich: " + ex.Message);
                    }
                };
            }

            _affiliateAutoEnrichSaveTimer.Stop();
            _affiliateAutoEnrichSaveTimer.Start();
        }

        private void UpdateAffiliateEnrichStatus(string text, bool isError = false)
        {
            if (lblAffiliateEnrichStatus == null || lblAffiliateEnrichStatus.IsDisposed) return;

            void apply()
            {
                lblAffiliateEnrichStatus.Text = text ?? string.Empty;
                lblAffiliateEnrichStatus.ForeColor = isError
                    ? Color.FromArgb(220, 130, 130)
                    : Color.FromArgb(150, 170, 200);
            }

            if (lblAffiliateEnrichStatus.InvokeRequired)
            {
                try { lblAffiliateEnrichStatus.BeginInvoke((Action)apply); }
                catch { /* form đóng */ }
            }
            else
            {
                apply();
            }
        }

        private void StartAffiliateAutoEnrichIfEnabled(CancellationToken parentToken)
        {
            if (chkAffiliateAutoEnrich == null || !chkAffiliateAutoEnrich.Checked)
            {
                UpdateAffiliateEnrichStatus("⏸ Auto enrich đang TẮT — bấm phải dòng để refresh số liệu/link thủ công.");
                return;
            }

            if (_affiliateBindingList == null || _affiliateBindingList.Count == 0)
            {
                return;
            }

            if (_affiliateAutoEnrichRunning)
            {
                Log("[Enrich] Auto enrich đang chạy, bỏ qua trigger mới.");
                return;
            }

            _affiliateAutoEnrichCts?.Dispose();
            _affiliateAutoEnrichCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            var token = _affiliateAutoEnrichCts.Token;

            // Set flag SYNCHRONOUSLY trên UI thread để btnHuntAffiliates_Click.finally đọc được
            // và biết là cần giữ btnStopHunt enabled.
            _affiliateAutoEnrichRunning = true;
            if (btnStopHunt != null && !btnStopHunt.IsDisposed)
            {
                btnStopHunt.Enabled = true;
            }

            _ = Task.Run(() => RunAffiliateAutoEnrichAsync(token));
        }

        private async Task RunAffiliateAutoEnrichAsync(CancellationToken token)
        {
            _affiliateAutoEnrichRunning = true;

            // Chốt snapshot list ngay bây giờ để tránh bị mất sync khi user xoá/sửa.
            var snapshot = new List<AffiliateCandidate>();
            if (_affiliateBindingList != null)
            {
                foreach (var c in _affiliateBindingList)
                {
                    if (c != null) snapshot.Add(c);
                }
            }

            var total = snapshot.Count;
            if (total == 0)
            {
                _affiliateAutoEnrichRunning = false;
                return;
            }

            Log($"[Enrich] Bắt đầu auto enrich {total} dòng (metrics → anchor).");
            UpdateAffiliateEnrichStatus($"📊 Đang lấy số liệu 0/{total} ...");

            var lastRebind = DateTime.UtcNow;
            void MaybeRebindGrid(bool force = false)
            {
                if (!force && (DateTime.UtcNow - lastRebind) < AffiliateAutoEnrichRebindThrottle)
                {
                    return;
                }

                lastRebind = DateTime.UtcNow;
                if (_affiliateBindingList == null) return;
                if (dgvAffiliateResults == null || dgvAffiliateResults.IsDisposed) return;

                if (dgvAffiliateResults.InvokeRequired)
                {
                    try { dgvAffiliateResults.BeginInvoke((Action)(() => _affiliateBindingList?.ResetBindings())); }
                    catch { }
                }
                else
                {
                    _affiliateBindingList?.ResetBindings();
                }
            }

            var metricsOk = 0;
            var metricsFail = 0;
            var anchorUpdated = 0;
            var anchorNotFound = 0;
            var anchorFail = 0;

            try
            {
                // ===== PHASE 1: Metrics (TikWM, nhanh) =====
                for (var i = 0; i < total; i++)
                {
                    if (token.IsCancellationRequested) break;
                    var candidate = snapshot[i];
                    var url = (candidate?.VideoUrl ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    // Skip nếu metrics đã capture gần đây (vd. user vừa bấm manual).
                    if (candidate.MetricsCapturedAtUtc != DateTime.MinValue
                        && (DateTime.UtcNow - candidate.MetricsCapturedAtUtc).TotalMinutes < 10)
                    {
                        metricsOk++;
                        UpdateAffiliateEnrichStatus($"📊 Đang lấy số liệu {i + 1}/{total} (bỏ qua, đã có) ...");
                        continue;
                    }

                    try
                    {
                        await ApplyAffiliateMetricsFromTikWmAsync(candidate, token).ConfigureAwait(false);
                        metricsOk++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        metricsFail++;
                        candidate.LastMetricsError = ex.Message;
                    }

                    UpdateAffiliateEnrichStatus($"📊 Đang lấy số liệu {i + 1}/{total} ...");
                    MaybeRebindGrid();

                    if (i < total - 1)
                    {
                        try { await Task.Delay(1100, token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                    }
                }

                MaybeRebindGrid(force: true);

                // Sau khi Metrics xong → engagement score của các candidate trong _affiliateAllResults
                // đã được cập nhật. Refresh filter để dòng nào dưới ngưỡng "Min engagement score" bị ẩn.
                try
                {
                    if (dgvAffiliateResults != null && !dgvAffiliateResults.IsDisposed)
                    {
                        if (dgvAffiliateResults.InvokeRequired)
                        {
                            dgvAffiliateResults.BeginInvoke((Action)RefreshAffiliateGridByQualityFilter);
                        }
                        else
                        {
                            RefreshAffiliateGridByQualityFilter();
                        }
                    }
                }
                catch { /* form closed */ }

                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }

                // ===== PHASE 2: Anchor (mobile UA, chậm hơn) =====
                UpdateAffiliateEnrichStatus($"📊 ✅ {metricsOk}/{total} · 🛒 Đang quét link affiliate 0/{total} ...");

                for (var i = 0; i < total; i++)
                {
                    if (token.IsCancellationRequested) break;
                    var candidate = snapshot[i];
                    var url = (candidate?.VideoUrl ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    // Skip nếu LinkedProduct đã có giá trị thật (từ Hunt DOM hoặc manual scan).
                    var existing = (candidate.LinkedProduct ?? string.Empty).Trim();
                    var isPlaceholder = string.IsNullOrEmpty(existing)
                                        || string.Equals(existing, "Chưa rõ", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(existing, "Không hiện giỏ hàng", StringComparison.OrdinalIgnoreCase);
                    if (!isPlaceholder)
                    {
                        UpdateAffiliateEnrichStatus($"📊 ✅ {metricsOk}/{total} · 🛒 Quét {i + 1}/{total} (bỏ qua, đã có anchor) ...");
                        continue;
                    }

                    try
                    {
                        var anchor = await _affiliateHunter
                            .EnrichLinkedProductAsync(url, Log, token)
                            .ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(anchor))
                        {
                            candidate.LinkedProduct = anchor.Trim();
                            anchorUpdated++;
                        }
                        else
                        {
                            candidate.LinkedProduct = "Video không gắn anchor sản phẩm";
                            anchorNotFound++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        anchorFail++;
                        Log($"[Enrich] Anchor dòng lỗi ({url}): {ex.Message}");
                    }

                    UpdateAffiliateEnrichStatus($"📊 ✅ {metricsOk}/{total} · 🛒 Quét link affiliate {i + 1}/{total} ...");
                    MaybeRebindGrid();

                    if (i < total - 1)
                    {
                        try { await Task.Delay(450, token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                    }
                }

                MaybeRebindGrid(force: true);

                Log($"[Enrich] ✅ Hoàn tất. Metrics: {metricsOk} OK / {metricsFail} lỗi. Anchor: {anchorUpdated} cập nhật / {anchorNotFound} không thấy / {anchorFail} lỗi.");
                UpdateAffiliateEnrichStatus($"✅ Đã enrich xong {total} dòng. (Metrics OK: {metricsOk}, Anchor cập nhật: {anchorUpdated}, không thấy anchor: {anchorNotFound})");
            }
            catch (OperationCanceledException)
            {
                MaybeRebindGrid(force: true);
                Log($"[Enrich] ⏸ Đã hủy. Metrics: {metricsOk}/{total} OK, Anchor: {anchorUpdated} cập nhật.");
                UpdateAffiliateEnrichStatus($"⏸ Đã hủy auto enrich. (Metrics OK: {metricsOk}/{total}, Anchor cập nhật: {anchorUpdated})", isError: true);
            }
            catch (Exception ex)
            {
                MaybeRebindGrid(force: true);
                Log("[Enrich] Lỗi ngoài luồng: " + ex.Message);
                UpdateAffiliateEnrichStatus("⚠ Auto enrich lỗi: " + ex.Message, isError: true);
            }
            finally
            {
                _affiliateAutoEnrichRunning = false;

                // Tắt Stop button nếu không còn việc gì đang chạy (Hunt đã xong rồi).
                if (btnStopHunt != null && !btnStopHunt.IsDisposed)
                {
                    try
                    {
                        if (btnStopHunt.InvokeRequired)
                        {
                            btnStopHunt.BeginInvoke((Action)(() =>
                            {
                                if (!_affiliateAutoEnrichRunning && _huntCancellation == null && !_affiliateDownloadingBatch)
                                {
                                    btnStopHunt.Enabled = false;
                                }
                            }));
                        }
                        else
                        {
                            if (_huntCancellation == null && !_affiliateDownloadingBatch)
                            {
                                btnStopHunt.Enabled = false;
                            }
                        }
                    }
                    catch { /* form closed */ }
                }
            }
        }
    }
}
