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

            var title = "Hook (4–7s) — " + ((row.ProductName ?? string.Empty).Trim().Length > 0
                ? row.ProductName.Trim()
                : "Video reup");
            using (var dlg = new VideoReupTextEditorForm(
                title,
                row.ReupHookDraft ?? string.Empty,
                "Câu hook voiceover ElevenLabs — nên ngắn, 4–7 giây khi đọc.",
                minHeight: 240))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                row.ReupHookDraft = dlg.EditedText;
            }

            MarkVideoReupRowDirty(gridRowIndex);
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
            bool Ok(string key) => _systemHealth != null && _systemHealth.TryGetValue(key, out var v) && v;
            var pipelineReady = pipelineReadyOverride ?? (Ok("ffmpeg") && Ok("api_keys") && Ok("storage"));

            if (btnVideoReupNarrationScriptGemini != null)
            {
                btnVideoReupNarrationScriptGemini.Enabled = pipelineReady;
            }

            if (btnVideoReupNarrationScriptRegen != null)
            {
                btnVideoReupNarrationScriptRegen.Enabled = pipelineReady;
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
    }
}
