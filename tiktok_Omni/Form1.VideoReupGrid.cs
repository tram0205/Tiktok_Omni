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
    public partial class Form1
    {
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
                        actionTitle + " — lỗi (xem cột «Lỗi» / log phía dưới).",
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

            var total = rows.Count;
            var ok = 0;
            var fail = 0;

            try
            {
                for (var i = 0; i < total; i++)
                {
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
                }

                _videoReupBindingList?.ResetBindings();
                dgvVideoReupInput?.Refresh();
                SetVideoReupProgress(actionTitle + ": xong " + ok + "/" + total, 100);
                LogVideoReup(actionTitle + " — hoàn tất " + ok + "/" + total + " dòng (lỗi: " + fail + ").");
                ShowVideoReupBatchDoneMessage(actionTitle, ok, fail, total);
            }
            finally
            {
                SetVideoReupCaptionButtonsEnabled(true);
                if (btnVideoReupProcessVideo != null && !btnVideoReupProcessVideo.IsDisposed)
                {
                    btnVideoReupProcessVideo.Enabled = ResolveVideoReupRenderReady();
                }
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
                FillWeight = 16,
                MinimumWidth = 64,
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
                MinimumWidth = 120
            };
            combo.Items.AddRange(
                VideoReupRowItem.ReupRemixModeAffiliateBedLabel,
                VideoReupRowItem.ReupRemixModeNarrationLabel,
                VideoReupRowItem.ReupRemixModeFilmLabel);
            dgvVideoReupInput.Columns.Add(combo);

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

            dgvVideoReupInput.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colReupUseSfx",
                HeaderText = "SFX 3s",
                DataPropertyName = nameof(VideoReupRowItem.UseVisualHookSfx),
                FillWeight = 18,
                MinimumWidth = 44
            });

            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupStatus",
                HeaderText = "Trạng thái remix",
                DataPropertyName = nameof(VideoReupRowItem.RemixStatus),
                FillWeight = 34,
                MinimumWidth = 96,
                ReadOnly = true
            });
            dgvVideoReupInput.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colReupError",
                HeaderText = "Lỗi",
                DataPropertyName = nameof(VideoReupRowItem.RemixLastError),
                FillWeight = 48,
                MinimumWidth = 56,
                ReadOnly = true
            });
        }

        private void DgvVideoReupInput_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (dgvVideoReupInput?.Columns == null || dgvVideoReupInput.Columns.Count == 0)
            {
                return;
            }

            dgvVideoReupInput.EnableHeadersVisualStyles = false;
            dgvVideoReupInput.ColumnHeadersHeight = AppGridHeaderHeight;
            dgvVideoReupInput.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvVideoReupInput.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvVideoReupInput.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvVideoReupInput.RowTemplate.Height = 30;
            dgvVideoReupInput.DefaultCellStyle.Font = AppInputFont;
            ApplyAppGridHeaderChrome(dgvVideoReupInput);

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
                    col.ToolTipText = "Bấm để mở bảng sửa hook (4–7s) hoặc «Gemini: tạo hook» (sinh kèm script vào cột Script).";
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
                    col.ToolTipText = "Script thuyết minh — dùng khi chế độ «Hook + Thuyết minh». «Gemini: tạo hook» sinh sẵn; hoặc «Tạo script» nếu chỉ cần script.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupSelectedMusicFile), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Nhạc nền (.mp3) hoặc «(Không có nhạc)». Thuyết minh: tùy chọn (~12% volume).";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.ReupSubtitleStyleLabel), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Bấm để chỉnh vị trí, font, cỡ chữ, kiểu chạy chữ phụ đề hook.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.UseVisualHookSfx), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Bật để dùng Hook SFX 3 giây (file cấu hình trên thanh hook) thay voiceover dài.";
                }
                else if (string.Equals(name, nameof(VideoReupRowItem.RemixLastError), StringComparison.OrdinalIgnoreCase))
                {
                    col.ToolTipText = "Thông báo lỗi remix (nếu có).";
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
                e.Value = FormatReupGridTextPreview(row.ReupHookDraft);
                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.Hashtags), StringComparison.OrdinalIgnoreCase))
            {
                e.Value = FormatReupGridTextPreview(row.Hashtags, "Bấm để thêm hashtag…");
                return;
            }

            if (string.Equals(col?.DataPropertyName, nameof(VideoReupRowItem.ReupNarrationScript), StringComparison.OrdinalIgnoreCase))
            {
                if (!row.IsNarrationScriptMode)
                {
                    e.Value = "—";
                    e.CellStyle.ForeColor = Color.FromArgb(100, 105, 118);
                }
                else
                {
                    e.Value = FormatReupGridTextPreview(row.ReupNarrationScript, "Bấm để sửa script…");
                }

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

            if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
            {
                e.CellStyle.ForeColor = Color.FromArgb(100, 105, 118);
                e.CellStyle.BackColor = Color.FromArgb(28, 30, 36);
            }
            else if (VideoReupRowItem.IsNoMusicSelection(row.ReupSelectedMusicFile))
            {
                e.CellStyle.ForeColor = Color.FromArgb(140, 145, 158);
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
                if (row.IsNarrationScriptMode)
                {
                    ShowReupNarrationScriptEditor(row, e.RowIndex);
                }
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
            if (col?.Name == "colReupProfile" || col?.Name == "colReupMode" || col?.Name == "colReupMusic")
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

        private async void btnProcessVideo_Click(object sender, EventArgs e)
        {
            if (!TryGetVideoReupSelectedRowsOrdered(
                    out var selectedRows,
                    "Chọn ít nhất một dòng trong bảng Video reup (Ctrl+click nhiều dòng) rồi bấm «Render & Đóng gói»."))
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
            await RunVideoReupBatchSequentialAsync(
                "Render & Đóng gói",
                selectedRows,
                async (row, index, total) =>
                {
                    ApplyReupVisualHookSettingsToRow(row);
                    SyncVideoReupAudioModeFromUiToRow(row);
                    EnsureVideoReupRowMusicDefault(row);

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
                }).ConfigureAwait(true);
        }
    }
}
