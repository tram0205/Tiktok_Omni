using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Helpers;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private sealed class PhilosophyRenderQueueEntry
        {
            public PhilosophyBatchItem Batch { get; set; }

            public PhilosophyScriptItem Quote { get; set; }
        }

        private void ConfigurePhilosophyBatchGrid()
        {
            dgvPhilosophyScripts.Columns.Clear();

            _colPhilosophyProfile = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyProfile",
                HeaderText = "Profile",
                DataPropertyName = nameof(PhilosophyBatchItem.ProfileName),
                DisplayMember = nameof(ProfileComboEntry.Name),
                ValueMember = nameof(ProfileComboEntry.Name),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 12,
                MinimumWidth = 96,
                ReadOnly = false,
                ToolTipText = "Profile cho batch — render và Assets lấy theo cột này."
            };
            dgvPhilosophyScripts.Columns.Add(_colPhilosophyProfile);

            AddPhilosophyBatchPopupColumn("colPhilosophyTopic", "Chủ đề", nameof(PhilosophyBatchItem.TopicGridLabel), 18);
            AddPhilosophyBatchPopupColumn("colPhilosophyBackground", "Nền", nameof(PhilosophyBatchItem.BackgroundGridLabel), 18);
            AddPhilosophyBatchPopupColumn("colPhilosophyAudio", "Âm thanh", nameof(PhilosophyBatchItem.AudioGridLabel), 16);
            AddPhilosophyBatchPopupColumn("colPhilosophySubtitle", "Phụ đề", nameof(PhilosophyBatchItem.SubtitleGridLabel), 14);
            AddPhilosophyBatchPopupColumn("colPhilosophyLogo", "Logo", nameof(PhilosophyBatchItem.LogoGridLabel), 12);

            dgvPhilosophyScripts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyStatus",
                HeaderText = "Trạng thái",
                DataPropertyName = nameof(PhilosophyBatchItem.StatusGridLabel),
                ReadOnly = true,
                FillWeight = 12,
                MinimumWidth = 88
            });

            dgvPhilosophyScripts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyOutput",
                HeaderText = "Thành phẩm",
                DataPropertyName = nameof(PhilosophyBatchItem.OutputGridLabel),
                ReadOnly = true,
                FillWeight = 14,
                MinimumWidth = 88,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });
        }

        private void AddPhilosophyBatchPopupColumn(string name, string header, string dataProperty, int fillWeight)
        {
            dgvPhilosophyScripts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                DataPropertyName = dataProperty,
                ReadOnly = true,
                FillWeight = fillWeight,
                MinimumWidth = 88,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }
            });
        }

        private void WirePhilosophyBatchGridEvents()
        {
            dgvPhilosophyScripts.CellFormatting -= DgvPhilosophyBatchScripts_CellFormatting;
            dgvPhilosophyScripts.CellPainting -= DgvPhilosophyBatchScripts_OutputCellPainting;
            dgvPhilosophyScripts.CellMouseClick -= DgvPhilosophyBatchScripts_OutputCellMouseClick;
            dgvPhilosophyScripts.CellClick -= DgvPhilosophyBatchScripts_CellClick;
            dgvPhilosophyScripts.CellValueChanged -= DgvPhilosophyBatchScripts_CellValueChanged;
            dgvPhilosophyScripts.DataError -= DgvPhilosophyBatchScripts_DataError;
            dgvPhilosophyScripts.CurrentCellDirtyStateChanged -= DgvPhilosophyBatchScripts_CurrentCellDirtyStateChanged;
            dgvPhilosophyScripts.EditingControlShowing -= DgvPhilosophyBatchScripts_EditingControlShowing;

            dgvPhilosophyScripts.CellFormatting += DgvPhilosophyBatchScripts_CellFormatting;
            dgvPhilosophyScripts.CellPainting += DgvPhilosophyBatchScripts_OutputCellPainting;
            dgvPhilosophyScripts.CellMouseClick += DgvPhilosophyBatchScripts_OutputCellMouseClick;
            dgvPhilosophyScripts.CellClick += DgvPhilosophyBatchScripts_CellClick;
            dgvPhilosophyScripts.CellValueChanged += DgvPhilosophyBatchScripts_CellValueChanged;
            dgvPhilosophyScripts.DataError += DgvPhilosophyBatchScripts_DataError;
            dgvPhilosophyScripts.CurrentCellDirtyStateChanged += DgvPhilosophyBatchScripts_CurrentCellDirtyStateChanged;
            dgvPhilosophyScripts.EditingControlShowing += DgvPhilosophyBatchScripts_EditingControlShowing;

            if (_cmsPhilosophyGrid == null || _cmsPhilosophyGrid.IsDisposed)
            {
                _cmsPhilosophyGrid = new ContextMenuStrip { Font = PhilosophyUiFont };
            }

            _cmsPhilosophyGrid.Items.Clear();
            var miAdd = new ToolStripMenuItem("Thêm batch");
            miAdd.Click += (_, __) => PhilosophyAddBatch();
            var miDelete = new ToolStripMenuItem("Xóa batch đã chọn");
            miDelete.Click += (_, __) => PhilosophyDeleteSelectedBatches();
            _cmsPhilosophyGrid.Items.AddRange(new ToolStripItem[] { miAdd, miDelete });
            dgvPhilosophyScripts.ContextMenuStrip = _cmsPhilosophyGrid;
        }

        private void DgvPhilosophyBatchScripts_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.ColumnIndex < 0)
            {
                return;
            }

            var col = dgvPhilosophyScripts?.Columns[e.ColumnIndex];
            if (col?.Name == "colPhilosophyProfile")
            {
                e.ThrowException = false;
                if (e.RowIndex >= 0
                    && dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyBatchItem batch)
                {
                    batch.ProfileName = ProfileScopedPaths.ResolveProfileName(batch.ProfileName);
                }
            }
        }

        private void DgvPhilosophyBatchScripts_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvPhilosophyScripts == null || !dgvPhilosophyScripts.IsCurrentCellDirty)
            {
                return;
            }

            if (dgvPhilosophyScripts.CurrentCell is DataGridViewComboBoxCell)
            {
                dgvPhilosophyScripts.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvPhilosophyBatchScripts_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (!(e.Control is ComboBox combo) || dgvPhilosophyScripts?.CurrentCell == null)
            {
                return;
            }

            combo.FlatStyle = FlatStyle.Flat;
            if (dgvPhilosophyScripts.Columns[dgvPhilosophyScripts.CurrentCell.ColumnIndex] is DataGridViewComboBoxColumn comboCol)
            {
                combo.DisplayMember = comboCol.DisplayMember ?? string.Empty;
                combo.ValueMember = comboCol.ValueMember ?? string.Empty;
                combo.FormattingEnabled = !string.IsNullOrEmpty(combo.DisplayMember);
                if (comboCol.Name == "colPhilosophyProfile")
                {
                    combo.DroppedDown = true;
                }
            }
        }

        private void DgvPhilosophyBatchScripts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (AppGridSttColumn.IsSttColumn(dgvPhilosophyScripts.Columns[e.ColumnIndex]))
            {
                return;
            }

            var colName = dgvPhilosophyScripts.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyStatus")
            {
                var status = (e.Value?.ToString() ?? "Nháp").Trim();
                Color fore;
                string glyph;
                if (status.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(255, 110, 110);
                    glyph = "✕ ";
                }
                else if (status.IndexOf("xong", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(100, 210, 130);
                    glyph = "✓ ";
                }
                else if (status.IndexOf("render", StringComparison.OrdinalIgnoreCase) >= 0
                         || status.IndexOf("đang", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(255, 196, 90);
                    glyph = "◐ ";
                }
                else if (status.IndexOf("dừng", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fore = Color.FromArgb(255, 150, 110);
                    glyph = "■ ";
                }
                else
                {
                    fore = Color.FromArgb(150, 158, 172);
                    glyph = "● ";
                }

                e.Value = glyph + status;
                e.CellStyle.ForeColor = fore;
                e.FormattingApplied = true;
            }
        }

        private void DgvPhilosophyBatchScripts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (dgvPhilosophyScripts.Columns[e.ColumnIndex].Name == "colPhilosophyProfile")
            {
                RefreshPhilosophyPrereqLabel(null);
                NotifyPhilosophyDraftDirty();
                return;
            }

            NotifyPhilosophyDraftDirty();
        }

        private void DgvPhilosophyBatchScripts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            var colName = dgvPhilosophyScripts.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyOutput")
            {
                return;
            }

            if (colName == "colPhilosophyProfile")
            {
                dgvPhilosophyScripts.BeginEdit(true);
                return;
            }

            OpenPhilosophyBatchEditorForColumn(e.RowIndex, colName);
        }

        private void DgvPhilosophyBatchScripts_OutputCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!string.Equals(dgvPhilosophyScripts.Columns[e.ColumnIndex].Name, "colPhilosophyOutput", StringComparison.Ordinal))
            {
                return;
            }

            if (!(dgvPhilosophyScripts.Rows[e.RowIndex].DataBoundItem is PhilosophyBatchItem batch))
            {
                return;
            }

            e.Handled = true;
            var graphics = e.Graphics;
            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            var cellBounds = e.CellBounds;
            e.Paint(
                cellBounds,
                DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);

            GetShowcaseImagesCellLayout(cellBounds.Width, cellBounds.Height, out var viewLocal, out var folderLocal);
            var viewRect = OffsetRect(cellBounds, viewLocal);
            var folderRect = OffsetRect(cellBounds, folderLocal);

            graphics.SetClip(cellBounds);
            dgvPhilosophyScripts.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText =
                (batch.OutputGridLabel ?? "—") + " — ▶ xem video · 📂 mở thư mục";

            PaintShowcaseImagesActionButton(
                graphics,
                viewRect,
                "▶",
                Color.FromArgb(130, 175, 255),
                Color.FromArgb(32, 42, 58),
                Color.FromArgb(70, 95, 140),
                selected);

            PaintShowcaseImagesActionButton(
                graphics,
                folderRect,
                "📂",
                Color.FromArgb(255, 205, 90),
                Color.FromArgb(58, 50, 32),
                Color.FromArgb(140, 110, 55),
                selected);

            graphics.ResetClip();
        }

        private void DgvPhilosophyBatchScripts_OutputCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dgvPhilosophyScripts == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!string.Equals(dgvPhilosophyScripts.Columns[e.ColumnIndex].Name, "colPhilosophyOutput", StringComparison.Ordinal))
            {
                return;
            }

            var display = dgvPhilosophyScripts.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var action = HitTestShowcaseDualActionCell(display.Width, display.Height, new Point(e.X, e.Y));
            if (action == ShowcaseDualActionCellAction.Add)
            {
                OpenPhilosophyBatchFirstOutput(e.RowIndex);
            }
            else if (action == ShowcaseDualActionCellAction.OpenFolder)
            {
                OpenPhilosophyBatchOutputFolder(e.RowIndex);
            }

            dgvPhilosophyScripts.InvalidateCell(e.ColumnIndex, e.RowIndex);
        }

        private void OpenPhilosophyBatchEditorForColumn(int rowIndex, string colName)
        {
            if (!(dgvPhilosophyScripts.Rows[rowIndex].DataBoundItem is PhilosophyBatchItem batch))
            {
                return;
            }

            var changed = false;
            switch (colName)
            {
                case "colPhilosophyTopic":
                    using (var dlg = new PhilosophyTopicEditorForm(
                        batch,
                        GeneratePhilosophyScriptsForBatchAsync,
                        ApplyPhilosophyGeminiScriptsAndAutoAudioAsync))
                    {
                        changed = dlg.ShowDialog(this) == DialogResult.OK;
                    }

                    break;
                case "colPhilosophyBackground":
                    using (var dlg = new PhilosophyBackgroundEditorForm(batch, ResolvePhilosophyBatchProfile(batch)))
                    {
                        changed = dlg.ShowDialog(this) == DialogResult.OK;
                    }

                    break;
                case "colPhilosophyAudio":
                    ShowPhilosophyBatchBackgroundMusicEditor(batch, rowIndex);
                    return;
                case "colPhilosophySubtitle":
                    ShowPhilosophyBatchSubtitleStyleEditor(batch, rowIndex);
                    return;
                case "colPhilosophyLogo":
                    ShowPhilosophyBatchBrandLogoEditor(batch, rowIndex);
                    return;
            }

            if (changed)
            {
                batch.RefreshDerivedFields();
                _philosophyBatchBindingList?.ResetBindings();
                dgvPhilosophyScripts.InvalidateRow(rowIndex);
                NotifyPhilosophyDraftDirty();
            }
        }

        private void OpenPhilosophyBatchFirstOutput(int rowIndex)
        {
            if (!(dgvPhilosophyScripts.Rows[rowIndex].DataBoundItem is PhilosophyBatchItem batch))
            {
                return;
            }

            var done = batch.Quotes?
                .FirstOrDefault(q => string.Equals(q?.Status, "Xong", StringComparison.OrdinalIgnoreCase)
                                     && !string.IsNullOrWhiteSpace(q?.OutputPath));
            if (done == null)
            {
                MessageBox.Show(this, "Batch chưa có video hoàn thành.", "Video Quote", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenPhilosophyOutputVideo(done);
        }

        private void OpenPhilosophyBatchOutputFolder(int rowIndex)
        {
            if (!(dgvPhilosophyScripts.Rows[rowIndex].DataBoundItem is PhilosophyBatchItem batch))
            {
                return;
            }

            var folder = PhilosophyBatchHelper.ResolveBatchOutputFolder(batch);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(this, "Chưa có thư mục thành phẩm.", "Video Quote", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Video Quote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string ResolvePhilosophyBatchProfile(PhilosophyBatchItem batch) =>
            PhilosophyBatchHelper.ResolveBatchProfileName(batch, GetSelectedPhilosophyProfileName());

        private void PhilosophyAddBatch()
        {
            var batch = CreateEmptyPhilosophyBatch();
            _philosophyBatchBindingList?.Add(batch);
            NotifyPhilosophyDraftDirty();
        }

        private PhilosophyBatchItem FindPhilosophyBatchForQuote(PhilosophyScriptItem quote)
        {
            if (quote == null || _philosophyBatchBindingList == null)
            {
                return null;
            }

            foreach (var batch in _philosophyBatchBindingList)
            {
                if (batch?.Quotes != null && batch.Quotes.Contains(quote))
                {
                    return batch;
                }
            }

            return null;
        }

        private PhilosophyBatchItem CreateEmptyPhilosophyBatch()
        {
            var batch = new PhilosophyBatchItem
            {
                ProfileName = GetSelectedPhilosophyProfileName(),
                MusicFolder = string.Empty,
                AmbientKey = PhilosophyAmbientCatalog.NoneKey,
                Status = "Nháp"
            };
            PhilosophyBatchHelper.EnsureBatchProfileName(batch, GetSelectedPhilosophyProfileName());

            var proxy = new PhilosophyScriptItem();
            PhilosophySubtitleStyleHelper.ApplyPhilosophyDefaults(proxy);
            PhilosophyBatchHelper.CopySubtitleTemplateFromQuote(batch, proxy);
            PhilosophyBatchHelper.EnsureBatchDefaults(batch);
            PhilosophyBatchHelper.EnsureBatchAudioDefaults(batch, _philosophySettingsSnap);
            batch.RefreshDerivedFields();
            return batch;
        }

        private void PhilosophyDeleteSelectedBatches()
        {
            if (_philosophyBatchBindingList == null || dgvPhilosophyScripts == null)
            {
                return;
            }

            var toRemove = GetPhilosophyTargetBatchesFromGrid();
            if (toRemove.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Chọn ít nhất một batch trên lưới để xóa.",
                    "Video Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!UiConfirmHelper.ConfirmDeleteRows(this, toRemove.Count))
            {
                return;
            }

            MovePhilosophyBatchesToTrash(toRemove);
            foreach (var batch in toRemove)
            {
                _philosophyBatchBindingList.Remove(batch);
            }

            NotifyPhilosophyDraftDirty();
        }

        private void PhilosophyCopySelectedBatches()
        {
            if (_philosophyBatchBindingList == null)
            {
                return;
            }

            var sources = GetPhilosophyTargetBatchesFromGrid();
            if (sources.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Chọn ít nhất một batch trên lưới để sao chép.",
                    "Video Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            PhilosophyBatchItem lastClone = null;
            foreach (var source in sources)
            {
                var clone = PhilosophyBatchCloneHelper.CloneBatch(source, forCopy: true);
                if (clone == null)
                {
                    continue;
                }

                _philosophyBatchBindingList.Add(clone);
                lastClone = clone;
            }

            if (lastClone == null)
            {
                return;
            }

            SelectPhilosophyBatchGridRow(lastClone);
            NotifyPhilosophyDraftDirty();
            LogPhilosophy("Đã sao chép " + sources.Count + " dòng — bản sao nằm cuối lưới («" + lastClone.Topic + "»).");
        }

        private void PhilosophyMoveSelectedBatch(int direction)
        {
            if (direction == 0 || _philosophyBatchBindingList == null || _philosophyBatchBindingList.Count < 2)
            {
                return;
            }

            var selected = GetPhilosophyTargetBatchesFromGrid();
            if (selected.Count != 1)
            {
                MessageBox.Show(
                    this,
                    "Chọn một batch trên lưới để di chuyển.",
                    "Video Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var batch = selected[0];
            var index = _philosophyBatchBindingList.IndexOf(batch);
            if (index < 0)
            {
                return;
            }

            var targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= _philosophyBatchBindingList.Count)
            {
                return;
            }

            _philosophyBatchBindingList.RemoveAt(index);
            _philosophyBatchBindingList.Insert(targetIndex, batch);
            SelectPhilosophyBatchGridRow(batch);
            NotifyPhilosophyDraftDirty();
        }

        private async void PhilosophyRestoreSessionsFromDisk()
        {
            if (_philosophyBatchBindingList == null || _philosophyBatchBindingList.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Chưa có batch trên lưới.\r\nBấm «+ Thêm batch» và nhập chủ đề trước.",
                    "Gắn phiên Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            var summary = PhilosophyBatchSessionRestoreHelper.RestoreFromDisk(
                _philosophyBatchBindingList.ToList(),
                settings,
                LogPhilosophy);

            dgvPhilosophyScripts?.Invalidate();
            NotifyPhilosophyDraftDirty();

            if (summary.OutputsNewlyLinked > 0 || summary.AudioSessionsFound > 0)
            {
                MessageBox.Show(
                    this,
                    "Đã gắn " + summary.OutputsNewlyLinked + " thành phẩm video."
                    + (summary.AudioSessionsFound > 0
                        ? "\r\nTìm thấy " + summary.AudioSessionsFound + " phiên preview âm thanh trên đĩa."
                        : string.Empty)
                    + (summary.StillNeedLink > 0
                        ? "\r\nCòn " + summary.StillNeedLink + " câu chưa có file output."
                        : string.Empty),
                    "Gắn phiên Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (summary.OutputsAlreadyLinked > 0 && summary.StillNeedLink <= 0)
            {
                MessageBox.Show(
                    this,
                    "Tất cả câu đã gắn thành phẩm rồi.",
                    "Gắn phiên Quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(
                this,
                "Không gắn được thành phẩm mới."
                + (summary.OrphanOutputs > 0
                    ? "\r\nCó " + summary.OrphanOutputs + " file output chưa khớp câu trên lưới."
                    : string.Empty)
                + "\r\n\r\n• Kiểm tra thư mục PhilosophyVideo\\ThanhPham\\{profile}\r\n• Preview âm thanh gắn theo BatchId trên lưới",
                "Gắn phiên Quote",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void SelectPhilosophyBatchGridRow(PhilosophyBatchItem batch)
        {
            if (batch == null || dgvPhilosophyScripts == null || dgvPhilosophyScripts.IsDisposed)
            {
                return;
            }

            dgvPhilosophyScripts.ClearSelection();
            for (var i = 0; i < dgvPhilosophyScripts.Rows.Count; i++)
            {
                if (dgvPhilosophyScripts.Rows[i].DataBoundItem is PhilosophyBatchItem rowBatch
                    && ReferenceEquals(rowBatch, batch))
                {
                    dgvPhilosophyScripts.Rows[i].Selected = true;
                    dgvPhilosophyScripts.CurrentCell = dgvPhilosophyScripts.Rows[i].Cells[0];
                    break;
                }
            }
        }

        private List<PhilosophyBatchItem> GetPhilosophyTargetBatchesFromGrid()
        {
            if (dgvPhilosophyScripts == null)
            {
                return new List<PhilosophyBatchItem>();
            }

            var rows = dgvPhilosophyScripts.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => r.DataBoundItem is PhilosophyBatchItem)
                .Select(r => (PhilosophyBatchItem)r.DataBoundItem)
                .ToList();

            if (rows.Count == 0
                && dgvPhilosophyScripts.CurrentRow?.DataBoundItem is PhilosophyBatchItem current)
            {
                rows.Add(current);
            }

            if (_philosophyBatchBindingList == null || _philosophyBatchBindingList.Count == 0)
            {
                return rows;
            }

            return rows
                .OrderBy(item => _philosophyBatchBindingList.IndexOf(item))
                .ToList();
        }

        private List<PhilosophyScriptItem> GetPhilosophyTargetQuotesFromSelectedBatches()
        {
            var batches = GetPhilosophyTargetBatchesFromGrid();
            var quotes = new List<PhilosophyScriptItem>();
            foreach (var batch in batches)
            {
                if (batch?.Quotes == null)
                {
                    continue;
                }

                PhilosophyBatchHelper.EnsureBatchAudioDefaults(batch, _philosophySettingsSnap);
                quotes.AddRange(batch.Quotes.Where(q => q != null));
            }

            return quotes;
        }

        private async Task ApplyPhilosophyGeminiScriptsAndAutoAudioAsync(
            PhilosophyBatchItem batch,
            IReadOnlyList<PhilosophyScriptItem> scripts,
            CancellationToken cancellationToken)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ApplyPhilosophyGeminiScriptsForBatch(batch, scripts, settings);
            await TryAutoGeneratePhilosophyBatchAudioAsync(batch, settings, cancellationToken).ConfigureAwait(true);
        }

        private void ApplyPhilosophyGeminiScriptsForBatch(
            PhilosophyBatchItem batch,
            IReadOnlyList<PhilosophyScriptItem> scripts,
            AppSettings settings)
        {
            if (batch == null)
            {
                return;
            }

            var mode = string.Equals(batch.GenerationMode, "Story", StringComparison.OrdinalIgnoreCase) ? "Story" : "Quotes";
            var duration = PhilosophyRenderOptions.ResolveDurationBounds(
                mode,
                batch.MinDurationSeconds,
                batch.MaxDurationSeconds);
            if (!TryNormalizePhilosophyDurationRange(
                    duration.MinSeconds,
                    duration.MaxSeconds,
                    out var minDuration,
                    out var maxDuration,
                    out _))
            {
                duration = PhilosophyRenderOptions.GetDefaultDurationBounds(mode);
                minDuration = duration.MinSeconds;
                maxDuration = duration.MaxSeconds;
            }

            ApplyPhilosophyGeminiScriptsToBatch(
                batch,
                scripts,
                batch.Topic ?? string.Empty,
                mode,
                minDuration,
                maxDuration,
                settings);
            NotifyPhilosophyDraftDirty();
        }

        private async Task<int> GeneratePhilosophyContentForBatchesAsync(
            IReadOnlyList<PhilosophyBatchItem> batches,
            CancellationToken cancellationToken,
            Action<int, int, string> progress = null)
        {
            if (batches == null || batches.Count == 0)
            {
                return 0;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                throw new InvalidOperationException("Cần AI API Key (Gemini) trong tab Cài đặt.");
            }

            var totalQuotes = 0;
            var index = 0;
            foreach (var batch in batches)
            {
                if (batch == null)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                index++;

                var topic = batch.Topic?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(topic))
                {
                    LogPhilosophy("Bỏ qua batch " + index + "/" + batches.Count + ": chưa có chủ đề.");
                    continue;
                }

                batch.ContentTemplateId = PhilosophyContentTemplatePresets.NormalizeId(batch.ContentTemplateId);
                if (!PhilosophyContentTemplatePresets.Resolve(batch.ContentTemplateId).UsesGemini)
                {
                    LogPhilosophy("Bỏ qua batch " + index + "/" + batches.Count
                                  + ": loại «Nhập tay» — nhập câu trong popup «Chủ đề».");
                    continue;
                }

                progress?.Invoke(index, batches.Count, topic);
                var scripts = await GeneratePhilosophyScriptsForBatchAsync(batch, cancellationToken).ConfigureAwait(true);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyPhilosophyGeminiScriptsForBatch(batch, scripts, settings);
                totalQuotes += scripts?.Count ?? 0;

                try
                {
                    await TryAutoGeneratePhilosophyBatchAudioAsync(batch, settings, cancellationToken)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            return totalQuotes;
        }

        private async Task<IReadOnlyList<PhilosophyScriptItem>> GeneratePhilosophyScriptsForBatchAsync(
            PhilosophyBatchItem batch,
            CancellationToken cancellationToken)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            var topic = batch.Topic?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(topic))
            {
                throw new InvalidOperationException("Nhập chủ đề trước khi tạo nội dung.");
            }

            batch.ContentTemplateId = PhilosophyContentTemplatePresets.NormalizeId(batch.ContentTemplateId);
            var preset = PhilosophyContentTemplatePresets.Resolve(batch.ContentTemplateId);
            if (!preset.UsesGemini)
            {
                throw new InvalidOperationException(
                    "Loại «" + preset.DisplayLabel + "» không dùng Gemini — nhập câu trong popup «Chủ đề».");
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                throw new InvalidOperationException("Cần AI API Key (Gemini) trong tab Cài đặt.");
            }

            var mode = string.Equals(batch.GenerationMode, "Story", StringComparison.OrdinalIgnoreCase) ? "Story" : "Quotes";
            var count = mode == "Story"
                ? 1
                : Math.Max(1, Math.Min(20, batch.QuoteCount > 0 ? batch.QuoteCount : 5));
            var duration = PhilosophyRenderOptions.ResolveDurationBounds(
                mode,
                batch.MinDurationSeconds,
                batch.MaxDurationSeconds);
            if (!TryNormalizePhilosophyDurationRange(
                    duration.MinSeconds,
                    duration.MaxSeconds,
                    out var minDuration,
                    out var maxDuration,
                    out var durationErr))
            {
                throw new InvalidOperationException(durationErr);
            }

            LogPhilosophy("Gemini: đang sinh nội dung cho «" + topic + "»…");
            var profile = ResolvePhilosophyBatchProfile(batch);
            var scripts = await _videoProcessingService.GeneratePhilosophyScriptsAsync(
                topic,
                mode,
                count,
                settings,
                minDuration,
                maxDuration,
                profile,
                batch.ContentTemplateId,
                batch.ContentMetadata,
                cancellationToken).ConfigureAwait(true);

            LogPhilosophy("Gemini: trả " + scripts.Count + " câu cho batch «" + topic + "».");
            return scripts;
        }

        private void ApplyPhilosophyGeminiScriptsToBatch(
            PhilosophyBatchItem batch,
            IReadOnlyList<PhilosophyScriptItem> scripts,
            string topic,
            string mode,
            int minDuration,
            int maxDuration,
            AppSettings settings)
        {
            if (batch == null)
            {
                return;
            }

            var profile = ResolvePhilosophyBatchProfile(batch);
            batch.Topic = topic;
            batch.GenerationMode = mode;
            batch.MinDurationSeconds = minDuration;
            batch.MaxDurationSeconds = maxDuration;
            batch.Quotes.Clear();

            foreach (var script in scripts ?? Array.Empty<PhilosophyScriptItem>())
            {
                if (script == null)
                {
                    continue;
                }

                PhilosophyAmbientCatalog.EnsureRowDefault(script);
                PhilosophyBatchHelper.ClearQuoteBatchOwnedFields(script);
                if (script.ZoomImagePaths != null && script.ZoomImagePaths.Count > 0)
                {
                    script.VisualMode = PhilosophyVisualModes.ImageZoom;
                }
                else if (script.VisualMode != PhilosophyVisualModes.VeoMascot
                         && script.VisualMode != PhilosophyVisualModes.VeoScenery
                         && script.VisualMode != PhilosophyVisualModes.PreRendered)
                {
                    script.VisualMode = PhilosophyVisualModes.Broll;
                }

                if (script.VisualMode == PhilosophyVisualModes.Broll
                    && (string.IsNullOrWhiteSpace(script.BRollFolder)
                        || (!PhilosophyBRollSelection.IsRandomToken(script.BRollFolder)
                            && !File.Exists(script.BRollFolder)
                            && !Directory.Exists(script.BRollFolder))))
                {
                    script.BRollFolder = PhilosophyBatchHelper.SuggestBRollFolder(profile, topic, script.Mood);
                }

                if (string.IsNullOrWhiteSpace(script.MotionPrompt))
                {
                    script.MotionPrompt = PhilosophyBatchHelper.BuildAiVeoPrompt(script, profile, topic, settings);
                }

                if (string.IsNullOrWhiteSpace(script.MusicFolder) && settings != null)
                {
                    var musicPath = PhilosophyProfileAssets.TryPickMusicFile(profile, script.Mood);
                    if (!string.IsNullOrWhiteSpace(musicPath))
                    {
                        script.MusicFolder = Path.GetFileName(musicPath);
                    }
                }

                batch.Quotes.Add(script);
            }

            var suggestedEdge = scripts?
                .Select(s => s?.EdgeStyleKey)
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            var dominantMood = batch.Quotes.FirstOrDefault()?.Mood ?? "reflective";
            batch.BodyStyleKey = PhilosophyBatchHelper.ResolveGeminiEdgeStyle(suggestedEdge, dominantMood);

            var firstQuote = batch.Quotes.FirstOrDefault();
            if (firstQuote != null)
            {
                PhilosophyBatchHelper.CopySubtitleTemplateFromQuote(batch, firstQuote);
                batch.SubtitleEnabled = true;
            }

            PhilosophyBatchHelper.ApplyBatchAudioToQuotes(batch, overwriteAll: false);
            PhilosophyBatchHelper.SyncBatchAudioSummaryFromQuotes(batch);
            PhilosophyBatchHelper.EnsureBatchAudioDefaults(batch, settings);
            PhilosophyBatchHelper.NormalizeBatchQuoteOwnership(batch);
            batch.RefreshDerivedFields();
            _philosophyBatchBindingList?.ResetBindings();
        }

        private async Task TryAutoGeneratePhilosophyBatchAudioAsync(
            PhilosophyBatchItem batch,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return;
            }

            var profile = ResolvePhilosophyBatchProfile(batch);
            var topic = batch.Topic?.Trim() ?? "batch";
            SetPhilosophyProgress("Audio tự động (thoại + mix): «" + TrimPhilosophyPreview(topic) + "»…", 0, indeterminate: true);
            try
            {
                await PhilosophyBatchAutoAudioHelper.TryGenerateBatchAudioAsync(
                    batch,
                    settings,
                    profile,
                    _videoProcessingService,
                    LogPhilosophy,
                    cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogPhilosophy("Audio tự động batch «" + topic + "» lỗi: " + ex.Message);
            }
        }

        private void PromptPhilosophyBatchRenderReport(PhilosophyBatchItem batch)
        {
            if (!ShouldAllowInteractivePrompts())
            {
                return;
            }

            if (batch == null || batch.Quotes == null || batch.Quotes.Count == 0)
            {
                return;
            }

            batch.RefreshDerivedFields();
            using (var dlg = new PhilosophyBatchRenderReportDialog(batch))
            {
                dlg.ShowDialog(this);
            }
        }

        private void MaybeShowPhilosophyBatchRenderReport(PhilosophyBatchItem finishedBatch)
        {
            if (finishedBatch == null || _philosophyRenderPending == null)
            {
                return;
            }

            var stillPending = _philosophyRenderPending.Any(e => ReferenceEquals(e.Batch, finishedBatch));
            if (!stillPending)
            {
                PromptPhilosophyBatchRenderReport(finishedBatch);
            }
        }

        private List<PhilosophyRenderQueueEntry> BuildPhilosophyRenderQueueFromSelectedBatches()
        {
            var queue = new List<PhilosophyRenderQueueEntry>();
            foreach (var batch in GetPhilosophyTargetBatchesFromGrid())
            {
                if (batch?.Quotes == null)
                {
                    continue;
                }

                PhilosophyBatchHelper.EnsureBatchAudioDefaults(batch, _philosophySettingsSnap);
                PhilosophyBatchHelper.EnsureBatchProfileName(batch, GetSelectedPhilosophyProfileName());
                foreach (var quote in batch.Quotes.Where(q => q != null))
                {
                    queue.Add(new PhilosophyRenderQueueEntry { Batch = batch, Quote = quote });
                }
            }

            return queue;
        }

        private async void ShowPhilosophyBatchBrandLogoEditor(PhilosophyBatchItem batch, int rowIndex)
        {
            if (batch == null)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            var stub = PhilosophyBatchHelper.CreateLogoEditorStub(batch);
            ShowcaseBrandOverlayHelper.EnsureVideoDefaults(stub);
            using (var dlg = new ShowcaseBrandLogoEditorForm(stub, settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            PhilosophyBatchHelper.ApplyLogoEditorStub(batch, stub);
            batch.RefreshDerivedFields();
            _philosophyBatchBindingList?.ResetBindings();
            dgvPhilosophyScripts?.InvalidateRow(rowIndex);
            NotifyPhilosophyDraftDirty();
        }
    }
}
