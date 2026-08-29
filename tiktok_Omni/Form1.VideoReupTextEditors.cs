using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static string FormatReupGridTextPreview(string text, string emptyLabel = "Bấm để sửa…")
        {
            var t = (text ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            while (t.Contains("  "))
            {
                t = t.Replace("  ", " ");
            }

            if (string.IsNullOrEmpty(t))
            {
                return emptyLabel;
            }

            return t.Length <= 64 ? t : t.Substring(0, 61) + "…";
        }

        private void ShowReupHookEditor(VideoReupRowItem row, int gridRowIndex)
        {
            if (row == null)
            {
                return;
            }

            if (VideoReupStyleVariants.HasHookVariants(row))
            {
                ShowReupStyleVariantPicker(row, gridRowIndex, focusScript: false);
                return;
            }

            var title = "Hook (4–7s) — " + ((row.ProductName ?? string.Empty).Trim().Length > 0
                ? row.ProductName.Trim()
                : "Video reup");
            using (var dlg = new VideoReupTextEditorForm(
                title,
                row.ReupHookDraft ?? string.Empty,
                "Câu hook voiceover ElevenLabs — nên ngắn, 4–7 giây khi đọc.",
                minHeight: 280))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                row.ReupHookDraft = dlg.EditedText;
            }

            MarkVideoReupRowDirty(gridRowIndex);
        }

        private void ShowReupStyleVariantPicker(VideoReupRowItem row, int gridRowIndex, bool focusScript)
        {
            var shortLabel = VideoReupProductLabel.GetShortLabel(row.ProductName ?? string.Empty);
            var title = (focusScript ? "Script" : "Hook") + " — "
                        + (shortLabel.Length > 0 ? shortLabel : "Video reup");
            using (var dlg = new VideoReupStyleVariantPickerForm(
                title,
                row.ReupHookByStyle,
                row.ReupScriptByStyle,
                row.SelectedHookStyleKey,
                row.SelectedScriptStyleKey,
                focusScriptColumn: focusScript,
                rawProductName: row.ProductName))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                row.ReupHookByStyle = dlg.HookByStyle;
                row.ReupScriptByStyle = dlg.ScriptByStyle;
                row.SelectedHookStyleKey = dlg.SelectedHookStyleKey;
                row.SelectedScriptStyleKey = dlg.SelectedScriptStyleKey;
                ApplyReupStyleSelectionsToRow(row);
            }

            MarkVideoReupRowDirty(gridRowIndex);
        }

        private static void ApplyReupStyleSelectionsToRow(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            var hookStyle = VideoReupStyleVariants.NormalizeStyleKey(row.SelectedHookStyleKey);
            if (!string.IsNullOrEmpty(hookStyle))
            {
                row.HookStyleKey = hookStyle;
                if (VideoReupStyleVariants.TryGetVariant(row.ReupHookByStyle, hookStyle, out var hookText))
                {
                    row.ReupHookDraft = hookText;
                }
            }
            else
            {
                row.ReupHookDraft = string.Empty;
            }

            var scriptStyle = VideoReupStyleVariants.NormalizeStyleKey(row.SelectedScriptStyleKey);
            if (string.IsNullOrEmpty(scriptStyle))
            {
                scriptStyle = hookStyle;
            }

            if (!string.IsNullOrEmpty(scriptStyle)
                && VideoReupStyleVariants.TryGetVariant(row.ReupScriptByStyle, scriptStyle, out var scriptText))
            {
                row.ReupNarrationScript = scriptText;
            }
            else if (string.IsNullOrEmpty(row.SelectedScriptStyleKey) && string.IsNullOrEmpty(hookStyle))
            {
                row.ReupNarrationScript = string.Empty;
            }
        }

        private void ShowReupHashtagsEditor(VideoReupRowItem row, int gridRowIndex)
        {
            if (row == null)
            {
                return;
            }

            var title = "Hashtag — " + ((row.ProductName ?? string.Empty).Trim().Length > 0
                ? row.ProductName.Trim()
                : "Video reup");
            using (var dlg = new VideoReupTextEditorForm(
                title,
                row.Hashtags ?? string.Empty,
                "Hashtag đăng TikTok — cách nhau bằng dấu cách hoặc xuống dòng.",
                minHeight: 200))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                row.Hashtags = dlg.EditedText;
            }

            MarkVideoReupRowDirty(gridRowIndex);
        }

        private void ShowReupNarrationScriptEditor(VideoReupRowItem row, int gridRowIndex)
        {
            if (row == null || !row.IsNarrationScriptMode)
            {
                return;
            }

            if (VideoReupStyleVariants.HasScriptVariants(row))
            {
                ShowReupStyleVariantPicker(row, gridRowIndex, focusScript: true);
                return;
            }

            var title = "Script — " + ((row.ProductName ?? string.Empty).Trim().Length > 0
                ? row.ProductName.Trim()
                : "Video reup");
            using (var dlg = new VideoReupTextEditorForm(
                title,
                row.ReupNarrationScript ?? string.Empty,
                "Kịch bản đọc sau hook — giọng kể chuyện (ElevenLabs), khác giọng hook. «Gemini: tạo hook» sinh kèm script; hoặc dùng «Tạo script» nếu chỉ cần script.",
                minHeight: 280))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                row.ReupNarrationScript = dlg.EditedText;
            }

            MarkVideoReupRowDirty(gridRowIndex);
        }

        private void MarkVideoReupRowDirty(int gridRowIndex)
        {
            _videoReupDraftDirty = true;
            _videoReupBindingList?.ResetBindings();
            if (dgvVideoReupInput != null && gridRowIndex >= 0 && gridRowIndex < dgvVideoReupInput.Rows.Count)
            {
                dgvVideoReupInput.InvalidateRow(gridRowIndex);
            }
        }

        private bool TryGetVideoReupSelectedRowWithIndex(out VideoReupRowItem row, out int gridRowIndex)
        {
            row = null;
            gridRowIndex = -1;
            if (dgvVideoReupInput?.SelectedRows == null || dgvVideoReupInput.SelectedRows.Count == 0)
            {
                return false;
            }

            var gridRow = dgvVideoReupInput.SelectedRows[0];
            if (gridRow?.DataBoundItem is VideoReupRowItem r)
            {
                row = r;
                gridRowIndex = gridRow.Index;
                return true;
            }

            return false;
        }

        private void RefreshVideoReupNarrationScriptButtons(bool? pipelineReadyOverride = null)
        {
            var geminiReady = pipelineReadyOverride ?? ResolveVideoReupGeminiReady();
            var hasNarrationRows = false;
            var rows = GetVideoReupSelectedRowsOrdered();
            if (rows.Count > 0)
            {
                hasNarrationRows = rows.Any(r => r != null && r.IsNarrationScriptMode);
            }

            var enabled = geminiReady && hasNarrationRows;
            if (btnVideoReupNarrationScriptGemini != null && !btnVideoReupNarrationScriptGemini.IsDisposed)
            {
                btnVideoReupNarrationScriptGemini.Enabled = enabled;
            }

            if (btnVideoReupNarrationScriptRegen != null && !btnVideoReupNarrationScriptRegen.IsDisposed)
            {
                btnVideoReupNarrationScriptRegen.Enabled = enabled;
            }
        }

        private async void btnVideoReupNarrationScriptGemini_Click(object sender, EventArgs e)
        {
            await RunVideoReupNarrationScriptBatchAsync(forceRegenerate: false, openEditorAfter: true).ConfigureAwait(true);
        }

        private async void btnVideoReupNarrationScriptRegen_Click(object sender, EventArgs e)
        {
            await RunVideoReupNarrationScriptBatchAsync(forceRegenerate: true, openEditorAfter: true).ConfigureAwait(true);
        }

        private async Task RunVideoReupNarrationScriptBatchAsync(bool forceRegenerate, bool openEditorAfter)
        {
            if (!TryGetVideoReupSelectedRowsOrdered(
                    out var rows,
                    "Video reup script: chọn ít nhất một dòng (Ctrl+click nhiều dòng)."))
            {
                return;
            }

            rows = rows.Where(r => r != null && r.IsNarrationScriptMode).ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show(
                    FindForm(),
                    "Script thuyết minh chỉ dùng với chế độ «Hook + Thuyết minh».\n\n"
                    + "Trên lưới, đổi cột «Chế độ» của dòng đã chọn sang «Hook + Thuyết minh» rồi bấm lại.",
                    "Video reup — Tạo script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                LogVideoReup("Video reup script: cần cột «Chế độ» = «Hook + Thuyết minh».");
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            var actionTitle = forceRegenerate ? "Tạo lại script" : "Tạo script";
            await RunVideoReupBatchSequentialAsync(
                actionTitle,
                rows,
                async (row, index, total) =>
                {
                    SyncVideoReupAudioModeFromUiToRow(row);
                    if (!forceRegenerate && !string.IsNullOrWhiteSpace((row.ReupNarrationScript ?? string.Empty).Trim()))
                    {
                        LogVideoReup("Video reup script («" + row.ProductName + "»): đã có script — bỏ qua (dùng «Tạo lại script»).");
                        return true;
                    }

                    return await RunVideoReupNarrationScriptForRowAsync(row, settings, forceRegenerate).ConfigureAwait(true);
                }).ConfigureAwait(true);

            if (openEditorAfter
                && rows.Count == 1
                && TryGetVideoReupSelectedRowWithIndex(out var singleRow, out var gridRowIndex))
            {
                ShowReupNarrationScriptEditor(singleRow, gridRowIndex);
            }
        }

        private async Task<bool> RunVideoReupNarrationScriptForRowAsync(
            VideoReupRowItem row,
            AppSettings settings,
            bool forceRegenerate)
        {
            if (!VideoReupRemixService.TryValidateNarrationScriptGeminiStep(row, settings, out var preflightError))
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = preflightError;
                LogVideoReup("Video reup script («" + row.ProductName + "»): " + preflightError);
                InvalidateVideoReupGridRow(row);
                return false;
            }

            row.RemixStatus = forceRegenerate ? "Gemini script (lại)…" : "Gemini script…";
            row.RemixLastError = string.Empty;
            InvalidateVideoReupGridRow(row);

            await _videoReupRemixService.GenerateNarrationScriptForRowAsync(
                row,
                settings,
                _geminiService,
                _affiliateHunter,
                LogVideoReup,
                forceRegenerate,
                CancellationToken.None).ConfigureAwait(true);

            row.RemixStatus = "Script OK";
            LogVideoReup(forceRegenerate
                ? "Video reup script: Gemini đã ghi lại script («" + row.ProductName + "»)."
                : "Video reup script: Gemini đã ghi script («" + row.ProductName + "»).");
            InvalidateVideoReupGridRow(row);
            return true;
        }

        private static bool VideoReupRowHasHookGeminiContent(VideoReupRowItem row)
        {
            // Hashtag có thể đã được điền sẵn từ lúc import sản phẩm (trước khi có Hook),
            // nên chỉ coi là "đã có nội dung" khi Hook thực sự đã được tạo.
            return VideoReupStyleVariants.HasResolvableHook(row);
        }

        /// <summary>Xác nhận tạo lại hook khi dòng đã có nội dung (chỉ dùng khi chọn 1 dòng).</summary>
        private bool TryConfirmVideoReupHookGeminiRow(VideoReupRowItem row, out bool forceRegenerate)
        {
            forceRegenerate = false;
            if (!VideoReupRowHasHookGeminiContent(row))
            {
                return true;
            }

            var confirm = MessageBox.Show(
                FindForm(),
                "Dòng này đã có nội dung (hook / hashtag).\n\n"
                + "Gemini free tier giới hạn khoảng 20 lần/ngày/model.\n"
                + "Bấm Có để tạo lại hook + script + hashtag.\n"
                + "Bấm Không để giữ nguyên — mở bảng chọn phong cách hook/script.",
                "Tạo Hook+Script+Hashtag",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (confirm == DialogResult.Cancel)
            {
                return false;
            }

            if (confirm == DialogResult.No)
            {
                if (VideoReupStyleVariants.HasHookVariants(row)
                    && TryGetVideoReupRowIndex(row, out var gridRowIndex))
                {
                    ShowReupStyleVariantPicker(row, gridRowIndex, focusScript: false);
                }

                return false;
            }

            forceRegenerate = true;
            return true;
        }

        private async Task<bool> RunVideoReupHookGeminiCoreAsync(
            VideoReupRowItem row,
            AppSettings settings,
            bool forceRegenerate,
            bool openPickerAfter)
        {
            if (row == null)
            {
                return false;
            }

            SyncVideoReupAudioModeFromUiToRow(row);
            LogVideoReup("[VideoReup] Chế độ lưới: «" + row.ReupMode + "» — «" + row.ProductName + "».");

            if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
            {
                LogVideoReup("Video reup hook: chế độ Phim — không cần thư viện nhạc .mp3.");
            }

            if (!VideoReupRemixService.TryValidateHookGeminiStep(row, settings, out var preflightError))
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = preflightError;
                LogVideoReup("Video reup hook («" + row.ProductName + "»): " + preflightError);
                InvalidateVideoReupGridRow(row);
                return false;
            }

            row.RemixStatus = forceRegenerate ? "Gemini (lại)…" : "Gemini…";
            row.RemixLastError = string.Empty;
            InvalidateVideoReupGridRow(row);

            try
            {
                await _videoReupRemixService.GenerateHookAndSuggestMusicAsync(
                    row,
                    settings,
                    _geminiService,
                    _affiliateHunter,
                    LogVideoReup,
                    CancellationToken.None,
                    forceRegenerate: forceRegenerate).ConfigureAwait(true);

                await _videoReupRemixService.GenerateHashtagsAsync(
                    row,
                    settings,
                    _geminiService,
                    LogVideoReup,
                    CancellationToken.None,
                    forceRegenerate: forceRegenerate).ConfigureAwait(true);

                await RefreshVideoReupMusicComboAsync().ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(row.ReupSuggestedMusicFile)
                    && cbVideoReupMusic?.Items.Contains(row.ReupSuggestedMusicFile) == true
                    && TryGetVideoReupSelectedRow(out var selectedRow)
                    && ReferenceEquals(selectedRow, row))
                {
                    cbVideoReupMusic.SelectedItem = row.ReupSuggestedMusicFile;
                    row.ReupSelectedMusicFile = row.ReupSuggestedMusicFile;
                }

                row.RemixStatus = "Gemini OK";
                if (row.ReupAudioMode == VideoReupAudioMode.NarrationScript)
                {
                    LogVideoReup(forceRegenerate
                        ? "Video reup: Gemini đã tạo lại hook + script + hashtag («" + row.ProductName + "»)."
                        : "Video reup: Gemini đã tạo hook + script + hashtag («" + row.ProductName + "»).");
                }
                else if (row.ReupAudioMode == VideoReupAudioMode.AffiliateBed)
                {
                    LogVideoReup(forceRegenerate
                        ? "Video reup: Gemini đã tạo lại hook + hashtag («" + row.ProductName + "»)."
                        : "Video reup: Gemini đã tạo hook + hashtag («" + row.ProductName + "»).");
                }
                else
                {
                    LogVideoReup(forceRegenerate
                        ? "Video reup: Gemini đã tạo lại hook + hashtag («" + row.ProductName + "»)."
                        : "Video reup: Gemini đã tạo hook + hashtag («" + row.ProductName + "»).");
                }

                InvalidateVideoReupGridRow(row);

                if (openPickerAfter && TryGetVideoReupRowIndex(row, out var gridRowIndex))
                {
                    ShowReupStyleVariantPicker(row, gridRowIndex, focusScript: false);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (VideoReupRemixService.IsGeminiQuotaError(ex) && VideoReupStyleVariants.HasHookVariants(row))
                {
                    row.RemixStatus = "Gemini OK";
                    row.RemixLastError = VideoReupRemixService.FormatGeminiQuotaShortMessage();
                    LogVideoReup("[VideoReup] Hết quota — dùng hook/script đã có («" + row.ProductName + "»).");
                    InvalidateVideoReupGridRow(row);
                    if (openPickerAfter && TryGetVideoReupRowIndex(row, out var gridRowIndex))
                    {
                        ShowReupStyleVariantPicker(row, gridRowIndex, focusScript: false);
                    }

                    return true;
                }

                row.RemixStatus = "Lỗi";
                row.RemixLastError = VideoReupRemixService.IsGeminiQuotaError(ex)
                    ? VideoReupRemixService.FormatGeminiQuotaUserMessage(ex)
                    : ex.Message;
                LogVideoReup("Video reup Gemini lỗi («" + row.ProductName + "»): "
                              + (VideoReupRemixService.IsGeminiQuotaError(ex)
                                  ? VideoReupRemixService.FormatGeminiQuotaShortMessage()
                                  : ex.Message));
                InvalidateVideoReupGridRow(row);
                return false;
            }
        }

        private async Task RunVideoReupHookGeminiAsync()
        {
            if (!TryGetVideoReupSelectedRowsOrdered(
                    out var rows,
                    "Video reup: chọn ít nhất một dòng (Ctrl+click nhiều dòng) rồi bấm «Tạo Hook+Script+Hashtag»."))
            {
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);

            if (rows.Count == 1)
            {
                var row = rows[0];
                if (!TryConfirmVideoReupHookGeminiRow(row, out var forceRegenerate))
                {
                    return;
                }

                SetVideoReupCaptionButtonsEnabled(false);
                try
                {
                    FocusVideoReupGridRow(row);
                    SetVideoReupProgress(
                        forceRegenerate ? "Gemini: tạo lại hook + script + hashtag…" : "Gemini: tạo hook + script + hashtag…",
                        0,
                        indeterminate: true);
                    if (await RunVideoReupHookGeminiCoreAsync(row, settings, forceRegenerate, openPickerAfter: true).ConfigureAwait(true))
                    {
                        SetVideoReupProgress("Gemini OK — bấm cột Hook hoặc Script để chọn phong cách", 100);
                    }
                    else
                    {
                        SetVideoReupProgress("lỗi Gemini", 0);
                    }

                    _videoReupBindingList?.ResetBindings();
                }
                finally
                {
                    SetVideoReupCaptionButtonsEnabled(true);
                }

                return;
            }

            await RunVideoReupBatchSequentialAsync(
                "Tạo Hook+Script+Hashtag",
                rows,
                async (row, index, total) =>
                {
                    if (VideoReupRowHasHookGeminiContent(row))
                    {
                        LogVideoReup("Video reup hook («" + row.ProductName + "»): đã có hook/hashtag — bỏ qua.");
                        return true;
                    }

                    return await RunVideoReupHookGeminiCoreAsync(
                        row,
                        settings,
                        forceRegenerate: false,
                        openPickerAfter: false).ConfigureAwait(true);
                }).ConfigureAwait(true);
        }

        private async Task<bool> RunVideoReupLyriaHookForRowAsync(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return false;
            }

            if (!VideoReupRemixService.TryValidateVoiceoverAudioStep(row, settings, out var preflightError))
            {
                row.RemixStatus = "Lỗi";
                row.RemixLastError = preflightError;
                LogVideoReup("Video reup voiceover («" + row.ProductName + "»): " + preflightError);
                InvalidateVideoReupGridRow(row);
                return false;
            }

            row.RemixStatus = "Voiceover…";
            row.RemixLastError = string.Empty;
            InvalidateVideoReupGridRow(row);

            await _videoReupRemixService.BuildVoiceoverHookAudioAsync(
                row,
                settings,
                _affiliateHunter,
                LogVideoReup,
                CancellationToken.None).ConfigureAwait(true);
            row.RemixStatus = "Âm thanh hook OK";
            LogVideoReup("Video reup voiceover: đã tạo hook («" + row.ProductName + "») — MP3: " + (row.HookAudioPath ?? string.Empty));
            InvalidateVideoReupGridRow(row);
            return true;
        }

        private async Task RunVideoReupLyriaHookAsync()
        {
            if (!TryGetVideoReupSelectedRowsOrdered(
                    out var rows,
                    "Video reup voiceover: chọn ít nhất một dòng (Ctrl+click nhiều dòng)."))
            {
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);

            await RunVideoReupBatchSequentialAsync(
                "Voiceover hook",
                rows,
                async (row, index, total) =>
                    await RunVideoReupLyriaHookForRowAsync(row, settings).ConfigureAwait(true)).ConfigureAwait(true);
        }
    }
}
