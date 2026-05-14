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
    /// <summary>Affiliate Hunter: lưới kết quả, Deep Dive (đơn + hàng loạt), quét anchor.</summary>
    public partial class Form1
    {
        private static readonly TimeSpan AffiliateBulkDeepDiveDelay = TimeSpan.FromSeconds(2.5);
        private static readonly TimeSpan AffiliateAutoEnrichRebindThrottle = TimeSpan.FromMilliseconds(800);

        private async void btnAffiliateDeepDive_Click(object sender, EventArgs e)
        {
            if (_affiliateDeepDiveRunning && _affiliateBulkDeepDiveCancelCts != null)
            {
                try
                {
                    _affiliateBulkDeepDiveCancelCts.Cancel();
                    Log("[DeepDive] Đã yêu cầu dừng hàng loạt.");
                }
                catch
                {
                    // ignore
                }

                return;
            }

            if (_affiliateDeepDiveRunning)
            {
                return;
            }

            if (dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                MessageBox.Show(this,
                    "Hãy chọn ít nhất một dòng trong bảng affiliate trước khi phân tích.",
                    "Phân tích Deep Dive",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedCandidates = new List<AffiliateCandidate>();
            foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
            {
                if (row?.DataBoundItem is AffiliateCandidate c)
                {
                    selectedCandidates.Add(c);
                }
            }

            if (selectedCandidates.Count == 0)
            {
                return;
            }

            if (selectedCandidates.Count > 1)
            {
                var ok = MessageBox.Show(this,
                    $"Bạn đang chọn {selectedCandidates.Count} dòng.\n\n" +
                    "Chế độ hàng loạt: phân tích TUẦN TỰ từng video, nghỉ ~2,5 giây giữa các lần gọi Gemini để giảm 429.\n" +
                    "Trong lúc chạy, bấm lại nút «Dừng hàng loạt» để hủy.\n\nTiếp tục?",
                    "Deep Dive hàng loạt",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (ok != DialogResult.Yes)
                {
                    return;
                }
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
                    "Chưa cấu hình AI API Key. Vào tab «Cài đặt» để nhập trước khi phân tích.",
                    "Phân tích Deep Dive",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selectedCandidates.Count == 1)
            {
                await RunAffiliateDeepDiveSingleAsync(selectedCandidates[0], settings).ConfigureAwait(true);
            }
            else
            {
                await RunAffiliateDeepDiveBulkAsync(selectedCandidates, settings).ConfigureAwait(true);
            }
        }

        private async Task RunAffiliateDeepDiveSingleAsync(AffiliateCandidate candidate, AppSettings settings)
        {
            var videoUrl = (candidate.VideoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show(this, "Dòng này không có VideoUrl.", "Phân tích Deep Dive",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _affiliateDeepDiveRunning = true;
            _affiliateBulkDeepDiveCancelCts = null;
            btnAffiliateDeepDive.Enabled = false;
            var originalText = btnAffiliateDeepDive.Text;
            btnAffiliateDeepDive.Text = "⏳ Đang phân tích...";
            Log($"[DeepDive] Bắt đầu phân tích video: {videoUrl}");
            GeminiUsageTracker.Instance.LogPrewarnIfNeeded(Log, settings.AiModel);

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8)))
                {
                    candidate.LastDeepDiveError = string.Empty;
                    var result = await _affiliateHunter
                        .AnalyzeVideoContentAsync(videoUrl, settings, Log, cts.Token)
                        .ConfigureAwait(true);

                    ApplyDeepDiveResultToCandidate(candidate, result);
                    _affiliateBindingList?.ResetBindings();

                    var moneyShot = string.IsNullOrWhiteSpace(result.MoneyShotSummary)
                        ? "(Gemini không xác định được đoạn ăn tiền nổi bật.)"
                        : result.MoneyShotSummary.Trim();
                    var transcriptPreview = string.IsNullOrWhiteSpace(result.VoiceoverTranscript)
                        ? "(không bóc tách được lời thoại)"
                        : TruncateForPreview(result.VoiceoverTranscript, 350);
                    var scriptPreview = string.IsNullOrWhiteSpace(result.VideoScript)
                        ? "(không có tóm tắt kịch bản)"
                        : TruncateForPreview(result.VideoScript, 600);

                    var popup =
                        "💰 ĐOẠN ĂN TIỀN:\r\n" + moneyShot + "\r\n\r\n" +
                        "🎤 Voiceover (rút gọn):\r\n" + transcriptPreview + "\r\n\r\n" +
                        "🎬 Kịch bản (rút gọn):\r\n" + scriptPreview + "\r\n\r\n" +
                        "Nội dung đầy đủ đã lưu vào trường VideoScript & VoiceoverTranscript của dòng này. " +
                        "Khi xuất CSV sẽ kèm theo.";

                    MessageBox.Show(this, popup, "✅ Phân tích Deep Dive xong",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                candidate.LastDeepDiveError = "Đã hủy / timeout.";
                Log("[DeepDive] Đã huỷ phân tích (timeout 8 phút).");
            }
            catch (Exception ex)
            {
                candidate.LastDeepDiveError = ex.Message;
                Log("[DeepDive] Lỗi: " + ex.Message);
                ShowDeepDiveErrorDialog(ex);
            }
            finally
            {
                _affiliateDeepDiveRunning = false;
                btnAffiliateDeepDive.Text = originalText;
                RefreshAffiliateDeepDiveButtonState();
            }
        }

        private async Task RunAffiliateDeepDiveBulkAsync(IList<AffiliateCandidate> candidates, AppSettings settings)
        {
            _affiliateDeepDiveRunning = true;
            _affiliateBulkDeepDiveCancelCts?.Dispose();
            _affiliateBulkDeepDiveCancelCts = new CancellationTokenSource();
            var userToken = _affiliateBulkDeepDiveCancelCts.Token;
            var originalText = btnAffiliateDeepDive.Text;
            btnAffiliateDeepDive.Text = "⏹ Dừng hàng loạt";
            btnAffiliateDeepDive.Enabled = true;

            var total = candidates.Count;
            var index = 0;
            var ok = 0;
            var fail = 0;

            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromHours(2)))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, timeout.Token))
                {
                    var token = linked.Token;
                    foreach (var candidate in candidates)
                    {
                        token.ThrowIfCancellationRequested();
                        index++;
                        var videoUrl = (candidate?.VideoUrl ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(videoUrl))
                        {
                            if (candidate != null)
                            {
                                candidate.LastDeepDiveError = "Thiếu VideoUrl.";
                            }

                            fail++;
                            Log($"[DeepDive] [{index}/{total}] Bỏ qua — không có VideoUrl.");
                            continue;
                        }

                        Log($"[DeepDive] [{index}/{total}] Bắt đầu: {videoUrl}");
                        GeminiUsageTracker.Instance.LogPrewarnIfNeeded(Log, settings.AiModel);
                        candidate.LastDeepDiveError = string.Empty;
                        _affiliateBindingList?.ResetBindings();

                        try
                        {
                            var result = await _affiliateHunter
                                .AnalyzeVideoContentAsync(videoUrl, settings, Log, token)
                                .ConfigureAwait(true);
                            ApplyDeepDiveResultToCandidate(candidate, result);
                            candidate.LastDeepDiveError = string.Empty;
                            ok++;
                            Log($"[DeepDive] [{index}/{total}] Xong.");
                        }
                        catch (OperationCanceledException)
                        {
                            if (candidate != null)
                            {
                                candidate.LastDeepDiveError = "Đã hủy.";
                            }

                            fail++;
                            Log($"[DeepDive] [{index}/{total}] Đã hủy.");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (candidate != null)
                            {
                                candidate.LastDeepDiveError = ex.Message;
                            }

                            fail++;
                            Log($"[DeepDive] [{index}/{total}] Lỗi: " + ex.Message);
                        }

                        _affiliateBindingList?.ResetBindings();

                        if (index < total)
                        {
                            try
                            {
                                await Task.Delay(AffiliateBulkDeepDiveDelay, token).ConfigureAwait(true);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                        }
                    }
                }

                MessageBox.Show(this,
                    $"Hoàn tất Deep Dive hàng loạt.\nThành công: {ok}\nLỗi / bỏ qua: {fail}\nChi tiết lỗi từng dòng: cột lưu trong log + CSV (LastDeepDiveError).",
                    "Deep Dive hàng loạt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                Log("[DeepDive] Hàng loạt dừng (hủy hoặc timeout).");
                MessageBox.Show(this,
                    $"Đã dừng hàng loạt sau {index}/{total} video.\nThành công: {ok}\nLỗi: {fail}",
                    "Deep Dive hàng loạt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                _affiliateBulkDeepDiveCancelCts?.Dispose();
                _affiliateBulkDeepDiveCancelCts = null;
                _affiliateDeepDiveRunning = false;
                btnAffiliateDeepDive.Text = originalText;
                RefreshAffiliateDeepDiveButtonState();
                _affiliateBindingList?.ResetBindings();
            }
        }

        private static void ApplyDeepDiveResultToCandidate(AffiliateCandidate candidate, VideoDeepAnalysisResult result)
        {
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

        private void ShowDeepDiveErrorDialog(Exception ex)
        {
            if (ex.Message.Contains("0 Frames found")
                || ex.Message.Contains("INVALID_ARGUMENT")
                || ex.Message.IndexOf("Slideshow", StringComparison.OrdinalIgnoreCase) >= 0
                || ex.Message.IndexOf("Ảnh trượt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(this,
                    "TikTok đã chặn tải phần hình ảnh của video này (chỉ tải được âm thanh), hoặc đây là video dạng Ảnh trượt (Slideshow).\n\nCon AI Gemini không có khung hình để phân tích. Bạn hãy thử phân tích video khác nhé!",
                    "Cảnh báo từ AI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (ex.Message.Contains("429")
                     || ex.Message.Contains("RESOURCE_EXHAUSTED")
                     || ex.Message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var retryHint = string.Empty;
                var m = System.Text.RegularExpressions.Regex.Match(
                    ex.Message,
                    "\"retryDelay\"\\s*:\\s*\"(\\d+)s\"");
                if (m.Success)
                {
                    retryHint = $"\n\nHãy chờ {m.Groups[1].Value} giây rồi thử lại.";
                }

                MessageBox.Show(this,
                    "Bạn đã hết hạn ngạch (quota) miễn phí của Gemini API hôm nay.\n\n" +
                    "Lý do: model gemini-2.5-flash free tier chỉ cho 20 request/ngày." + retryHint + "\n\n" +
                    "Cách khắc phục:\n" +
                    "1) Đợi sang ngày mới (theo giờ Mỹ — PST).\n" +
                    "2) Hoặc vào tab «Cài đặt» → đổi model sang «gemini-1.5-flash» (quota cao hơn nhiều).\n" +
                    "3) Hoặc bật billing trên Google AI Studio để được quota cao hơn.",
                    "Hết quota Gemini",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (ex.Message.IndexOf("Unable to extract", StringComparison.OrdinalIgnoreCase) >= 0
                     || ex.Message.IndexOf("universal data for rehydration", StringComparison.OrdinalIgnoreCase) >= 0
                     || ex.Message.IndexOf("yt-dlp -U", StringComparison.OrdinalIgnoreCase) >= 0
                     || ex.Message.IndexOf("on the latest version", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(this,
                    "Phiên bản yt-dlp.exe đang dùng đã quá cũ — TikTok thay đổi web nên extractor không còn lấy được dữ liệu video.\n\n" +
                    "Cách khắc phục:\n" +
                    "1) Vào tab «Cài đặt» → bấm «⬇ Tải yt-dlp» để tải bản mới nhất từ GitHub.\n" +
                    "2) Hoặc mở Command Prompt tại thư mục chứa yt-dlp.exe và chạy: yt-dlp.exe -U\n\n" +
                    "Sau đó thử Phân tích Deep Dive lại.",
                    "Cần cập nhật yt-dlp",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, "Lỗi khi phân tích video:\r\n" + ex.Message,
                    "Phân tích Deep Dive", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshAffiliateDeepDiveButtonState()
        {
            if (btnAffiliateDeepDive == null || dgvAffiliateResults == null)
            {
                return;
            }

            var hasSelection = dgvAffiliateResults.SelectedRows != null && dgvAffiliateResults.SelectedRows.Count > 0;
            var canCancelBulk = _affiliateDeepDiveRunning && _affiliateBulkDeepDiveCancelCts != null;
            btnAffiliateDeepDive.Enabled = (!_affiliateDeepDiveRunning && hasSelection) || canCancelBulk;
        }

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
                var sa = a?.SafetyScore ?? 0;
                var sb = b?.SafetyScore ?? 0;
                if (sb != sa)
                {
                    return sb.CompareTo(sa);
                }

                var pa = a?.PlayCount ?? 0;
                var pb = b?.PlayCount ?? 0;
                if (pb != pa)
                {
                    return pb.CompareTo(pa);
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
                        await Task.Delay(350, cancellationToken).ConfigureAwait(false);
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

                        await _configManager.SaveAsync(settings).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Log("[Affiliate] Không lưu được cài đặt xếp hạng: " + ex.Message);
                    }
                };
            }

            _affiliateHuntPrefsSaveTimer.Stop();
            _affiliateHuntPrefsSaveTimer.Start();
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
                        try { await Task.Delay(350, token).ConfigureAwait(false); }
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
