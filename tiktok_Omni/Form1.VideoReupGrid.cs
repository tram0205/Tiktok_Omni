using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private const string VideoReupRenderActionTitle = "Render & Đóng gói";

        private CancellationTokenSource _videoReupBatchCts;
        private bool _videoReupBatchRunning;
        private bool _videoReupRenderPaused;
        private List<VideoReupRowItem> _videoReupRenderPausedRows;
        private AppSettings _videoReupRenderActiveSettings;

        private bool TryGetVideoReupSelectedRowsOrdered(out List<VideoReupRowItem> rows, string emptySelectionLogMessage)
        {
            rows = GetVideoReupSelectedRowsOrdered();
            if (rows.Count == 0
                && dgvVideoReupInput?.CurrentRow?.DataBoundItem is VideoReupRowItem currentRow)
            {
                rows.Add(currentRow);
                dgvVideoReupInput.CurrentRow.Selected = true;
            }

            if (rows.Count == 0)
            {
                LogVideoReup(emptySelectionLogMessage);
                MessageBox.Show(
                    this,
                    emptySelectionLogMessage,
                    "Video reup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private void FocusVideoReupGridRow(VideoReupRowItem row)
        {
            if (row == null || dgvVideoReupInput == null || dgvVideoReupInput.IsDisposed)
            {
                return;
            }

            for (var i = 0; i < dgvVideoReupInput.Rows.Count; i++)
            {
                var gridRow = dgvVideoReupInput.Rows[i];
                if (gridRow.IsNewRow || !ReferenceEquals(gridRow.DataBoundItem, row))
                {
                    continue;
                }

                dgvVideoReupInput.ClearSelection();
                gridRow.Selected = true;
                if (gridRow.Cells.Count > 0)
                {
                    dgvVideoReupInput.CurrentCell = gridRow.Cells[0];
                }

                try
                {
                    dgvVideoReupInput.FirstDisplayedScrollingRowIndex = Math.Max(0, i);
                }
                catch
                {
                    // ignored
                }

                return;
            }
        }

        private bool TryGetVideoReupRowIndex(VideoReupRowItem row, out int gridRowIndex)
        {
            gridRowIndex = -1;
            if (row == null || dgvVideoReupInput == null || dgvVideoReupInput.IsDisposed)
            {
                return false;
            }

            for (var i = 0; i < dgvVideoReupInput.Rows.Count; i++)
            {
                var gridRow = dgvVideoReupInput.Rows[i];
                if (gridRow.IsNewRow || !ReferenceEquals(gridRow.DataBoundItem, row))
                {
                    continue;
                }

                gridRowIndex = gridRow.Index;
                return true;
            }

            return false;
        }

        private void ShowVideoReupBatchDoneMessage(string actionTitle, int successCount, int failCount, int total)
        {
            if (total <= 0)
            {
                return;
            }

            if (total == 1)
            {
                if (failCount == 0)
                {
                    MessageBox.Show(
                        this,
                        actionTitle + " — xong.",
                        "Video reup",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        this,
                        actionTitle + " — lỗi (xem cột «Trạng thái» / log phía dưới).",
                        "Video reup",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return;
            }

            var body =
                "Đã xử lý " + total + " dòng theo thứ tự từ trên xuống dưới.\n\n" +
                "Thành công: " + successCount + "\n" +
                "Lỗi: " + failCount;
            MessageBox.Show(
                this,
                body,
                actionTitle + " — xong",
                MessageBoxButtons.OK,
                failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async Task RunVideoReupBatchSequentialAsync(
            string actionTitle,
            IReadOnlyList<VideoReupRowItem> rows,
            Func<VideoReupRowItem, int, int, Task<bool>> processRowAsync)
        {
            if (rows == null || rows.Count == 0 || processRowAsync == null)
            {
                return;
            }

            FlushVideoReupHookDraftFromEditor();
            FlushVideoReupVideoUrlFromEditor();
            SetVideoReupCaptionButtonsEnabled(false);
            if (btnVideoReupProcessVideo != null && !btnVideoReupProcessVideo.IsDisposed)
            {
                btnVideoReupProcessVideo.Enabled = false;
            }

            var isRenderBatch = IsVideoReupRenderBatch(actionTitle);
            _videoReupBatchCts?.Cancel();
            _videoReupBatchCts?.Dispose();
            _videoReupBatchCts = new CancellationTokenSource();
            var batchToken = _videoReupBatchCts.Token;
            _videoReupBatchRunning = true;
            if (isRenderBatch)
            {
                ApplyVideoReupRenderStopButtonUi(continueMode: false, enabled: true);
            }

            var total = rows.Count;
            var ok = 0;
            var fail = 0;
            var cancelledAtIndex = -1;
            var batchCompleted = false;

            try
            {
                for (var i = 0; i < total; i++)
                {
                    if (batchToken.IsCancellationRequested)
                    {
                        cancelledAtIndex = i;
                        break;
                    }

                    var row = rows[i];
                    if (row == null)
                    {
                        fail++;
                        continue;
                    }

                    var index = i + 1;
                    FocusVideoReupGridRow(row);
                    var pct = total > 1 ? (int)Math.Round((i * 100.0) / total) : 0;
                    SetVideoReupProgress(actionTitle + " [" + index + "/" + total + "]: «" + row.ProductName + "»", pct, indeterminate: true);

                    try
                    {
                        if (await processRowAsync(row, index, total).ConfigureAwait(true))
                        {
                            ok++;
                        }
                        else
                        {
                            fail++;
                        }
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        row.IsProcessed = false;
                        row.RemixStatus = "Lỗi";
                        row.RemixLastError = ex.Message;
                        LogVideoReup(actionTitle + " lỗi («" + row.ProductName + "»): " + ex.Message);
                        InvalidateVideoReupGridRow(row);
                    }

                    if (batchToken.IsCancellationRequested)
                    {
                        cancelledAtIndex = i + 1;
                        break;
                    }
                }

                if (cancelledAtIndex >= 0)
                {
                    throw new OperationCanceledException(batchToken);
                }

                batchCompleted = true;
                _videoReupBindingList?.ResetBindings();
                dgvVideoReupInput?.Refresh();
                SetVideoReupProgress(actionTitle + ": xong " + ok + "/" + total, 100);
                LogVideoReup(actionTitle + " — hoàn tất " + ok + "/" + total + " dòng (lỗi: " + fail + ").");
                ShowVideoReupBatchDoneMessage(actionTitle, ok, fail, total);
                if (isRenderBatch)
                {
                    ResetVideoReupRenderPauseState();
                }
            }
            catch (OperationCanceledException)
            {
                if (isRenderBatch && cancelledAtIndex >= 0 && cancelledAtIndex < total)
                {
                    _videoReupRenderPausedRows = rows
                        .Skip(cancelledAtIndex)
                        .Where(r => r != null)
                        .ToList();
                    _videoReupRenderPaused = _videoReupRenderPausedRows.Count > 0;
                    if (_videoReupRenderPaused)
                    {
                        LogVideoReup("Đã dừng render — còn " + _videoReupRenderPausedRows.Count + " dòng. Bấm «Tiếp tục».");
                        SetVideoReupProgress("Đã dừng render — bấm «Tiếp tục»", 0);
                        ApplyVideoReupRenderStopButtonUi(continueMode: true, enabled: true);
                    }
                    else
                    {
                        ResetVideoReupRenderPauseState();
                        ApplyVideoReupRenderStopButtonUi(continueMode: false, enabled: false);
                    }
                }
                else
                {
                    LogVideoReup(actionTitle + " — đã dừng.");
                    SetVideoReupProgress("Đã dừng", 0);
                }
            }
            finally
            {
                _videoReupBatchRunning = false;
                _videoReupBatchCts?.Dispose();
                _videoReupBatchCts = null;
                SetVideoReupCaptionButtonsEnabled(true);
                if (btnVideoReupProcessVideo != null && !btnVideoReupProcessVideo.IsDisposed)
                {
                    btnVideoReupProcessVideo.Enabled = ResolveVideoReupRenderReady();
                }

                if (isRenderBatch && (batchCompleted || !_videoReupRenderPaused))
                {
                    ApplyVideoReupRenderStopButtonUi(continueMode: false, enabled: false);
                }
            }
        }

        private static bool IsVideoReupRenderBatch(string actionTitle)
        {
            return string.Equals(actionTitle, VideoReupRenderActionTitle, StringComparison.Ordinal);
        }

        private void ResetVideoReupRenderPauseState()
        {
            _videoReupRenderPaused = false;
            _videoReupRenderPausedRows = null;
        }

        private void ApplyVideoReupRenderStopButtonUi(bool continueMode, bool enabled)
        {
            if (btnVideoReupStop == null || btnVideoReupStop.IsDisposed)
            {
                return;
            }

            btnVideoReupStop.Text = continueMode ? "Tiếp tục" : "Dừng render";
            btnVideoReupStop.Enabled = enabled;
            var tint = continueMode ? ReupTintContinue : ReupTintStop;
            if (btnVideoReupStop is JellyButton jelly)
            {
                jelly.JellyTint = tint;
                jelly.Invalidate();
            }
        }

        private async Task HandleVideoReupRenderStopButtonClickAsync()
        {
            if (_videoReupRenderPaused && _videoReupRenderPausedRows != null && _videoReupRenderPausedRows.Count > 0)
            {
                var rows = _videoReupRenderPausedRows;
                var settingsSnap = _videoReupRenderActiveSettings ?? await _configManager.LoadAsync().ConfigureAwait(true);
                _videoReupRenderPaused = false;
                _videoReupRenderPausedRows = null;
                ApplyVideoReupRenderStopButtonUi(continueMode: false, enabled: true);
                LogVideoReup("Tiếp tục render — " + rows.Count + " dòng còn lại…");
                await RunVideoReupBatchSequentialAsync(
                    VideoReupRenderActionTitle,
                    rows,
                    async (row, index, total) =>
                        await RunVideoReupRenderPipelineForRowAsync(row, settingsSnap).ConfigureAwait(true)).ConfigureAwait(true);
                return;
            }

            if (!_videoReupBatchRunning)
            {
                return;
            }

            _videoReupBatchCts?.Cancel();
            CancelAllVideoReupBatchJobs();
            LogVideoReup("Đang dừng render…");
        }

        private void CancelRunningVideoReupWorkForEmergencyStop()
        {
            if (_videoReupBatchRunning)
            {
                TryCancel(_videoReupBatchCts);
                LogVideoReup("[Emergency] Đang dừng batch render đang chạy…");
            }
        }

        private void ConfigureVideoReupInputGrid()
        {
            if (dgvVideoReupInput == null)
            {
                return;
            }

            dgvVideoReupInput.AutoGenerateColumns = false;
            dgvVideoReupInput.Columns.Clear();

            dgvVideoReupInput.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "colReupProfile",
                HeaderText = "Profile",
                DataPropertyName = nameof(VideoReupRowItem.ProfileName),
                DisplayMember = nameof(ProfileComboEntry.Name),
                ValueMember = nameof(ProfileComboEntry.Name),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 24,
                MinimumWidth = 96,
                ReadOnly = false
            });
            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupProduct",
                HeaderText = "Sản phẩm",
                DataPropertyName = nameof(VideoReupRowItem.ProductName),
                FillWeight = 68,
                MinimumWidth = 64,
                ReadOnly = true
            });
            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupVideoUrl",
                HeaderText = "URL video",
                DataPropertyName = nameof(VideoReupRowItem.VideoUrl),
                FillWeight = 88,
                MinimumWidth = 72
            });
            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupHook",
                HeaderText = "Hook (4–7s)",
                DataPropertyName = nameof(VideoReupRowItem.ReupHookDraft),
                FillWeight = 90,
                MinimumWidth = 100,
                ReadOnly = true,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White,
                    WrapMode = DataGridViewTriState.False
                }
            });
            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupNarration",
                HeaderText = "Script",
                DataPropertyName = nameof(VideoReupRowItem.ReupNarrationScript),
                FillWeight = 72,
                MinimumWidth = 88,
                ReadOnly = true,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });
            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupHashtags",
                HeaderText = "Hashtag",
                DataPropertyName = nameof(VideoReupRowItem.Hashtags),
                FillWeight = 42,
                MinimumWidth = 72,
                ReadOnly = true,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });

            var combo = new DataGridViewComboBoxColumn
            {
                Name = "colReupMode",
                HeaderText = "Chế độ",
                DataPropertyName = nameof(VideoReupRowItem.ReupMode),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 52,
                MinimumWidth = 168
            };
            combo.Items.AddRange(
                VideoReupRowItem.ReupRemixModeAffiliateBedLabel,
                VideoReupRowItem.ReupRemixModeNarrationLabel,
                VideoReupRowItem.ReupRemixModeFilmLabel);
            dgvVideoReupInput.Columns.Add(combo);

            var musicCol = new DataGridViewComboBoxColumn
            {
                Name = "colReupMusic",
                HeaderText = "Nhạc nền",
                DataPropertyName = nameof(VideoReupRowItem.ReupSelectedMusicFile),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 48,
                MinimumWidth = 100
            };
            dgvVideoReupInput.Columns.Add(musicCol);

            var hookSfxCol = new DataGridViewComboBoxColumn
            {
                Name = "colReupHookSfx",
                HeaderText = "SFX Hook",
                DataPropertyName = nameof(VideoReupRowItem.ReupSelectedHookSfxFile),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Flat,
                FillWeight = 44,
                MinimumWidth = 96
            };
            dgvVideoReupInput.Columns.Add(hookSfxCol);

            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupSubtitle",
                HeaderText = "Phụ đề",
                DataPropertyName = nameof(VideoReupRowItem.ReupSubtitleStyleLabel),
                FillWeight = 58,
                MinimumWidth = 120,
                ReadOnly = true,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });

            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupColorGrade",
                HeaderText = "Màu",
                DataPropertyName = nameof(VideoReupRowItem.ReupColorGradeLabel),
                FillWeight = 46,
                MinimumWidth = 96,
                ReadOnly = true,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });

            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupStatus",
                HeaderText = "Trạng thái",
                DataPropertyName = nameof(VideoReupRowItem.RemixStatus),
                FillWeight = 56,
                MinimumWidth = 120,
                ReadOnly = true
            });
        }

        private void DgvVideoReupInput_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (dgvVideoReupInput?.Columns == null || dgvVideoReupInput.Columns.Count == 0)
            {
                return;
            }

            dgvVideoReupInput.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // Video reup cần dòng cao hơn chuẩn vì có ComboBox trong ô.
            ApplyAppComboGridRowHeight(dgvVideoReupInput);
            ApplyAppGridChrome(dgvVideoReupInput);
            EnsureAppGridRowHeights(dgvVideoReupInput);
            ApplyVideoReupProfileComboColumn();
            ApplyVideoReupComboColumnDropDownWidths();

            foreach (DataGridViewColumn col in dgvVideoReupInput.Columns)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

                if (col is DataGridViewComboBoxColumn comboCol)
                {
                    comboCol.DefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
                    comboCol.DefaultCellStyle.ForeColor = Color.Gainsboro;
                    comboCol.ToolTipText = "Hook + Nhạc nền: voiceover + nhạc affiliate. Hook + Phim: giữ tiếng gốc video.";
                    continue;
                }

                if (col.DefaultCellStyle.WrapMode == DataGridViewTriState.NotSet)
                {
                    col.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                }

                var name = col.DataPropertyName ?? string.Empty;
                if (string.Equals(name, nameof(VideoReupRowItem.VideoUrl), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "F2 hoặc double-click để sửa; Enter/rời ô để tải qua TikWM.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupHookDraft), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Bấm để mở bảng 5 phong cách hook/script — chọn, sửa, rồi render.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.Hashtags), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Bấm để mở bảng sửa hashtag.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ProfileName), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Chọn profile TikTok đã đăng nhập.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupMode), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Hook + Nhạc nền | Hook + Thuyết minh (Gemini script + ElevenLabs) | Hook + Phim.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupNarrationScript), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Script thuyết minh — bấm để mở bảng 5 phong cách, chỉnh sửa từng script. Dùng khi chế độ «Hook + Thuyết minh».";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupSelectedMusicFile), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Nhạc nền (.mp3) hoặc «(Không có nhạc)». Thuyết minh: tùy chọn (~10% volume).";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupSelectedHookSfxFile), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Hiệu ứng overlay cùng giọng hook (~45% volume). Kho: Assets\\Audio\\Sfx (.mp3/.wav).";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupSubtitleStyleLabel), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Bấm để chỉnh vị trí, font, cỡ chữ, kiểu chạy chữ phụ đề hook.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupColorGradeLabel), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Chỉnh màu body video (sáng/tương phản/bão hòa) — preset hoặc tùy chỉnh qua FFmpeg eq.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.RemixStatus), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Tiến trình remix và thông báo lỗi (nếu có).";
                }
            }
        }

        private void DgvVideoReupInput_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (dgvVideoReupInput == null || e.RowIndex < 0 || e.RowIndex >= dgvVideoReupInput.Rows.Count)
            {
                return;
            }

            var row = dgvVideoReupInput.Rows[e.RowIndex];
            if (row?.DataBoundItem is VideoReupRowItem item && item.IsProcessed)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(32, 58, 44);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(210, 235, 218);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(56, 120, 82);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            else if (row != null)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(20, 22, 28);
                row.DefaultCellStyle.ForeColor = Color.Gainsboro;
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }

            ApplyVideoReupComboCellChrome(row);
        }

        private void DgvVideoReupInput_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (!(e.Control is ComboBox combo) || dgvVideoReupInput?.CurrentCell == null)
            {
                return;
            }

            combo.FlatStyle = FlatStyle.Flat;
            if (dgvVideoReupInput.Columns[dgvVideoReupInput.CurrentCell.ColumnIndex] is DataGridViewComboBoxColumn comboCol)
            {
                combo.DisplayMember = comboCol.DisplayMember ?? string.Empty;
                combo.ValueMember = comboCol.ValueMember ?? string.Empty;
                combo.FormattingEnabled = !string.IsNullOrEmpty(combo.DisplayMember);
            }

            ApplyVideoReupComboDropDownWidth(combo);
        }

        private void ApplyVideoReupComboColumnDropDownWidths()
        {
            if (dgvVideoReupInput?.Columns == null)
            {
                return;
            }

            foreach (DataGridViewColumn col in dgvVideoReupInput.Columns)
            {
                if (col is DataGridViewComboBoxColumn comboCol)
                {
                    comboCol.MinimumWidth = Math.Max(
                        comboCol.MinimumWidth,
                        MeasureVideoReupComboColumnMinWidth(comboCol));
                }
            }
        }

        private static int MeasureVideoReupComboColumnMinWidth(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return 0;
            }

            var font = column.DataGridView?.Font ?? SystemFonts.DefaultFont;
            var max = TextRenderer.MeasureText(
                column.HeaderText ?? string.Empty,
                font,
                new Size(int.MaxValue, 0),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

            foreach (var text in EnumerateVideoReupComboColumnTexts(column))
            {
                max = Math.Max(
                    max,
                    TextRenderer.MeasureText(
                        text,
                        font,
                        new Size(int.MaxValue, 0),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
            }

            return max + 28;
        }

        private static IEnumerable<string> EnumerateVideoReupComboColumnTexts(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                yield break;
            }

            if (column.DataSource is IEnumerable dataSource)
            {
                foreach (var item in dataSource)
                {
                    yield return GetVideoReupComboItemText(column, item);
                }

                yield break;
            }

            foreach (var item in column.Items)
            {
                yield return GetVideoReupComboItemText(column, item);
            }
        }

        private static string GetVideoReupComboItemText(DataGridViewComboBoxColumn column, object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item is ProfileComboEntry entry)
            {
                return entry.Name ?? string.Empty;
            }

            var displayMember = column?.DisplayMember;
            if (!string.IsNullOrEmpty(displayMember))
            {
                var prop = item.GetType().GetProperty(displayMember);
                if (prop != null)
                {
                    return prop.GetValue(item)?.ToString() ?? string.Empty;
                }
            }

            return item.ToString() ?? string.Empty;
        }

        private static string GetVideoReupComboItemText(ComboBox combo, object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item is ProfileComboEntry entry)
            {
                return entry.Name ?? string.Empty;
            }

            try
            {
                if (!string.IsNullOrEmpty(combo.DisplayMember))
                {
                    return combo.GetItemText(item) ?? string.Empty;
                }
            }
            catch
            {
                // DisplayMember có thể không khớp kiểu item khi DGV tái sử dụng editing control.
            }

            return item.ToString() ?? string.Empty;
        }

        private static void ApplyVideoReupComboDropDownWidth(ComboBox combo)
        {
            if (combo == null)
            {
                return;
            }

            try
            {
                var font = combo.Font ?? SystemFonts.DefaultFont;
                var max = Math.Max(combo.Width, 120);
                IEnumerable items = combo.DataSource as IEnumerable ?? combo.Items;
                if (items == null)
                {
                    return;
                }

                foreach (var item in items)
                {
                    var text = GetVideoReupComboItemText(combo, item);
                    max = Math.Max(
                        max,
                        TextRenderer.MeasureText(
                            text,
                            font,
                            new Size(int.MaxValue, 0),
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
                }

                combo.DropDownWidth = max + SystemInformation.VerticalScrollBarWidth + 12;
            }
            catch
            {
                // Không chặn chỉnh sửa lưới nếu đo text dropdown thất bại.
            }
        }

        private static void ApplyVideoReupComboCellChrome(DataGridViewRow row)
        {
            if (row == null)
            {
                return;
            }

            foreach (DataGridViewCell cell in row.Cells)
            {
                if (!(cell is DataGridViewComboBoxCell))
                {
                    continue;
                }

                cell.Style.BackColor = Color.FromArgb(45, 49, 60);
                cell.Style.ForeColor = Color.Gainsboro;
                cell.Style.SelectionBackColor = Color.FromArgb(76, 110, 245);
                cell.Style.SelectionForeColor = Color.White;
            }
        }

        private void VideoReupInputGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || !grid.IsCurrentCellDirty)
            {
                return;
            }

            if (grid.CurrentCell is DataGridViewComboBoxCell)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvVideoReupInput_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvVideoReupInput == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = dgvVideoReupInput.Columns[e.ColumnIndex];
            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.RemixStatus), StringComparison.OrdinalIgnoreCase))
            {
                if (dgvVideoReupInput.Rows[e.RowIndex].DataBoundItem is VideoReupRowItem statusRow)
                {
                    e.Value = statusRow.GetReupStatusDisplayLabel();
                    var error = (statusRow.RemixLastError ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(error))
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(255, 145, 145);
                    }
                    else if (statusRow.IsProcessed
                             || string.Equals((statusRow.RemixStatus ?? string.Empty).Trim(), "Xong", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(170, 220, 180);
                    }
                }

                return;
            }

            if (!string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupSelectedMusicFile), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupSubtitleStyleLabel), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupHookDraft), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.Hashtags), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupNarrationScript), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!(dgvVideoReupInput.Rows[e.RowIndex].DataBoundItem is VideoReupRowItem row))
            {
                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupHookDraft), StringComparison.OrdinalIgnoreCase))
            {
                e.Value = FormatReupGridTextPreview(VideoReupStyleVariants.GetHookGridPreview(row));
                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.Hashtags), StringComparison.OrdinalIgnoreCase))
            {
                e.Value = FormatReupGridTextPreview(row.Hashtags, "Bấm để thêm hashtag…");
                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupNarrationScript), StringComparison.OrdinalIgnoreCase))
            {
                e.Value = FormatReupGridTextPreview(
                    VideoReupStyleVariants.GetScriptGridPreview(row),
                    "Bấm để sửa script…");
                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupSubtitleStyleLabel), StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(row.ReupSubtitleStyleLabel))
                {
                    e.Value = "Bấm để chỉnh…";
                }

                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupColorGradeLabel), StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(row.ReupColorGradeLabel))
                {
                    e.Value = "Chuẩn reup…";
                }

                return;
            }

            if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
            {
                e.CellStyle.ForeColor = Color.FromArgb(100, 105, 118);
                e.CellStyle.BackColor = Color.FromArgb(28, 30, 36);
            }
            else if (VideoReupRowItem.IsNoMusicSelection(row.ReupSelectedMusicFile))
            {
                e.CellStyle.ForeColor = Color.FromArgb(140, 145, 158);
            }
            else if (VideoReupRowItem.IsNoHookSfxSelection(row.ReupSelectedHookSfxFile)
                     || string.IsNullOrWhiteSpace(row.ReupSelectedHookSfxFile))
            {
                if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupSelectedHookSfxFile), StringComparison.OrdinalIgnoreCase))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(140, 145, 158);
                }
            }
            else if (row.ReupAudioMode == VideoReupAudioMode.NarrationScript
                     && string.IsNullOrWhiteSpace(row.ReupSelectedMusicFile))
            {
                e.CellStyle.ForeColor = Color.FromArgb(140, 145, 158);
            }
        }

        private void DgvVideoReupInput_EditorCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvVideoReupInput == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = dgvVideoReupInput.Columns[e.ColumnIndex];
            if (!(dgvVideoReupInput.Rows[e.RowIndex].DataBoundItem is VideoReupRowItem row))
            {
                return;
            }

            if (string.Equals(col?.Name, "colReupSubtitle", StringComparison.OrdinalIgnoreCase))
            {
                ShowReupSubtitleStyleEditor(row, e.RowIndex);
                return;
            }

            if (string.Equals(col?.Name, "colReupColorGrade", StringComparison.OrdinalIgnoreCase))
            {
                ShowReupColorGradeEditor(row, e.RowIndex);
                return;
            }

            if (string.Equals(col?.Name, "colReupHook", StringComparison.OrdinalIgnoreCase))
            {
                ShowReupHookEditor(row, e.RowIndex);
                return;
            }

            if (string.Equals(col?.Name, "colReupHashtags", StringComparison.OrdinalIgnoreCase))
            {
                ShowReupHashtagsEditor(row, e.RowIndex);
                return;
            }

            if (string.Equals(col?.Name, "colReupNarration", StringComparison.OrdinalIgnoreCase))
            {
                ShowReupNarrationScriptEditor(row, e.RowIndex);
                return;
            }
        }

        private void VideoReupInputGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.ColumnIndex < 0)
            {
                return;
            }

            var grid = sender as DataGridView;
            var col = grid?.Columns[e.ColumnIndex];
            if (col?.Name == "colReupProfile" || col?.Name == "colReupMode" || col?.Name == "colReupMusic"
                || col?.Name == "colReupHookSfx")
            {
                e.ThrowException = false;
            }
        }

        private void PopulateVideoReupMusicGridColumnItems(IReadOnlyList<string> musicFileNames)
        {
            if (dgvVideoReupInput?.Columns == null)
            {
                return;
            }

            if (!(dgvVideoReupInput.Columns["colReupMusic"] is DataGridViewComboBoxColumn musicCol))
            {
                return;
            }

            musicCol.Items.Clear();
            musicCol.Items.Add(VideoReupRowItem.NoMusicSelectionLabel);
            if (musicFileNames == null)
            {
                return;
            }

            foreach (var name in musicFileNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    musicCol.Items.Add(name);
                }
            }
        }

        private void PopulateVideoReupHookSfxGridColumnItems(IReadOnlyList<string> sfxFileNames)
        {
            if (dgvVideoReupInput?.Columns == null)
            {
                return;
            }

            if (!(dgvVideoReupInput.Columns["colReupHookSfx"] is DataGridViewComboBoxColumn sfxCol))
            {
                return;
            }

            sfxCol.Items.Clear();
            sfxCol.Items.Add(VideoReupRowItem.NoHookSfxSelectionLabel);
            if (sfxFileNames == null)
            {
                return;
            }

            foreach (var name in sfxFileNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    sfxCol.Items.Add(name);
                }
            }
        }

        private async Task<bool> RunVideoReupRenderPipelineForRowAsync(VideoReupRowItem row, AppSettings settingsSnap)
        {
            ApplyReupVisualHookSettingsToRow(row);
            SyncVideoReupAudioModeFromUiToRow(row);
            EnsureVideoReupRowMusicDefault(row);
            EnsureVideoReupRowHookSfxDefault(row);

            var result = await RunVideoReupFullPipelineAsync(row, settingsSnap).ConfigureAwait(true);
            _renderHistoryStore.AddSuccess(row.VideoUrl);
            row.LastRemixOutputPath = result.OutputPath ?? string.Empty;
            row.LastSourceVideoDurationSec = result.SourceDurationSeconds;
            row.LastRemixOutputVideoDurationSec = result.OutputFileDurationSeconds;
            row.LastHookDurationUsedSec = result.HookDurationSecondsUsed;
            row.IsProcessed = true;
            row.RemixStatus = "Xong";
            row.RemixLastError = string.Empty;
            LogVideoReup("Video reup xong («" + row.ProductName + "»): " + (result.OutputPath ?? string.Empty));
            InvalidateVideoReupGridRow(row);
            return true;
        }

        private async Task RunVideoReupRenderSelectedRowsAsync(string emptySelectionMessage)
        {
            if (!TryGetVideoReupSelectedRowsOrdered(out var selectedRows, emptySelectionMessage))
            {
                return;
            }

            if (btnVideoReupProcessVideo != null && !btnVideoReupProcessVideo.Enabled)
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                bool Ok(string key) => _systemHealth != null && _systemHealth.TryGetValue(key, out var v) && v;
                if (!VideoReupRemixService.IsTabRenderReady(settings, Ok("ffmpeg"), Ok("storage"), out var msg))
                {
                    MessageBox.Show(this, msg, "Chưa render được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            LogVideoReup("Bắt đầu Pipeline Render & Đóng gói (tải → hook → voiceover → remix) — " + selectedRows.Count + " dòng…");
            var settingsSnap = await _configManager.LoadAsync().ConfigureAwait(true);
            _videoReupRenderActiveSettings = settingsSnap;
            ResetVideoReupRenderPauseState();
            await RunVideoReupBatchSequentialAsync(
                VideoReupRenderActionTitle,
                selectedRows,
                async (row, index, total) =>
                    await RunVideoReupRenderPipelineForRowAsync(row, settingsSnap).ConfigureAwait(true)).ConfigureAwait(true);
        }

        private async void btnProcessVideo_Click(object sender, EventArgs e)
        {
            await RunVideoReupRenderSelectedRowsAsync(
                "Chọn ít nhất một dòng trong bảng Video reup (Ctrl+click nhiều dòng) rồi bấm «Render & Đóng gói».").ConfigureAwait(true);
        }
    }
}
