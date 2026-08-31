using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly Queue<string> _productAdImageLogBuffer = new Queue<string>();
        private readonly object _productAdImageLogSync = new object();
        private System.Windows.Forms.Timer _productAdImageLogFlushTimer;
        private AppSettings _productAdImageSettingsSnap;
        private CancellationTokenSource _productAdImagePlanCts;
        private bool _productAdImagePlanRunning;
        private ContextMenuStrip _cmsProductAdImageGrid;
        private string _productAdImageProfileBeforeEdit;

        private void InitializeProductAdImageLogFlush()
        {
            if (_productAdImageLogFlushTimer != null)
            {
                return;
            }

            _productAdImageLogFlushTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _productAdImageLogFlushTimer.Tick += (_, __) => FlushProductAdImageLogBuffer();
            _productAdImageLogFlushTimer.Start();
        }

        private void LogProductAdImage(string message)
        {
            Log(message);
            var line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + (message ?? string.Empty);
            lock (_productAdImageLogSync)
            {
                _productAdImageLogBuffer.Enqueue(line);
            }
        }

        private void FlushProductAdImageLogBuffer()
        {
            if (rtbProductAdImageLog == null || rtbProductAdImageLog.IsDisposed)
            {
                return;
            }

            List<string> pending = null;
            lock (_productAdImageLogSync)
            {
                if (_productAdImageLogBuffer.Count == 0)
                {
                    return;
                }

                pending = new List<string>(_productAdImageLogBuffer.Count);
                while (_productAdImageLogBuffer.Count > 0)
                {
                    pending.Add(_productAdImageLogBuffer.Dequeue());
                }
            }

            if (pending == null || pending.Count == 0)
            {
                return;
            }

            if (rtbProductAdImageLog.InvokeRequired)
            {
                rtbProductAdImageLog.BeginInvoke(new Action(() => AppendProductAdImageLogLines(pending)));
                return;
            }

            AppendProductAdImageLogLines(pending);
        }

        private void AppendProductAdImageLogLines(IList<string> lines)
        {
            if (rtbProductAdImageLog == null || rtbProductAdImageLog.IsDisposed || lines == null)
            {
                return;
            }

            rtbProductAdImageLog.SuspendLayout();
            try
            {
                foreach (var line in lines)
                {
                    var isError = line.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                                  || line.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;
                    rtbProductAdImageLog.SelectionStart = rtbProductAdImageLog.TextLength;
                    rtbProductAdImageLog.SelectionLength = 0;
                    rtbProductAdImageLog.SelectionColor = isError
                        ? Color.FromArgb(255, 120, 120)
                        : Color.FromArgb(190, 195, 205);
                    rtbProductAdImageLog.SelectionCharOffset = ProductAdImageLogLineSpacing;
                    rtbProductAdImageLog.AppendText(line + Environment.NewLine);
                }

                rtbProductAdImageLog.SelectionColor = Color.FromArgb(190, 195, 205);
                rtbProductAdImageLog.ScrollToCaret();
            }
            finally
            {
                rtbProductAdImageLog.ResumeLayout(false);
            }
        }

        private void ClearProductAdImageLog()
        {
            lock (_productAdImageLogSync)
            {
                _productAdImageLogBuffer.Clear();
            }

            if (rtbProductAdImageLog != null && !rtbProductAdImageLog.IsDisposed)
            {
                rtbProductAdImageLog.Clear();
            }

            LogProductAdImage("[Tạo ảnh AI] Sẵn sàng.");
        }

        private void RefreshProductAdImageReadinessLabel()
        {
            if (lblProductAdImageReadiness == null || lblProductAdImageReadiness.IsDisposed)
            {
                return;
            }

            void Apply()
            {
                var unsaved = (txtAiApiKey?.Text ?? string.Empty).Trim();
                var saved = (_productAdImageSettingsSnap?.AiApiKey ?? string.Empty).Trim();
                var hasSaved = saved.Length > 0;
                var hasUnsaved = unsaved.Length > 0;

                if (!hasSaved && !hasUnsaved)
                {
                    lblProductAdImageReadiness.ForeColor = Color.FromArgb(255, 160, 80);
                    lblProductAdImageReadiness.Text = "Chưa có Gemini API key — mở Cài đặt để nhập.";
                    return;
                }

                if (hasUnsaved && !string.Equals(saved, unsaved, StringComparison.Ordinal))
                {
                    lblProductAdImageReadiness.ForeColor = Color.FromArgb(230, 190, 70);
                    lblProductAdImageReadiness.Text = "API key chưa lưu — bấm Lưu trong Cài đặt.";
                    return;
                }

                lblProductAdImageReadiness.ForeColor = Color.FromArgb(165, 172, 188);
                lblProductAdImageReadiness.Text = "Sẵn sàng lập prompt · copy/Excel · sinh ảnh ngoài app.";
            }

            if (lblProductAdImageReadiness.InvokeRequired)
            {
                lblProductAdImageReadiness.BeginInvoke(new Action(Apply));
                return;
            }

            Apply();
        }

        private async Task RefreshProductAdImageSettingsSnapAsync()
        {
            if (_configManager == null)
            {
                return;
            }

            try
            {
                _productAdImageSettingsSnap = await _configManager.LoadAsync().ConfigureAwait(true);
            }
            catch
            {
                // giữ snap cũ
            }

            RefreshProductAdImageReadinessLabel();
            if (_productAdImageSettingsSnap != null)
            {
                RefreshGridProfileComboSource(_productAdImageSettingsSnap);
                ApplyProductAdImageProfileComboColumn();
            }
        }

        private void BtnProductAdImageAddRow_Click(object sender, EventArgs e)
        {
            EnsureProductAdImageBindingList();
            var item = CreateEmptyProductAdImageRow();
            _productAdImageBindingList.Add(item);
            RenumberProductAdImageRows();
            SelectProductAdImageItem(item);
            LogProductAdImage("Đã thêm dòng " + item.Order + ".");
        }

        private void BtnProductAdImageCopyRow_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProductAdImageItems();
            if (selected.Count == 0)
            {
                LogProductAdImage("Chọn ít nhất một dòng để copy.");
                return;
            }

            EnsureProductAdImageBindingList();
            ProductAdImageBatchItem last = null;
            foreach (var source in selected)
            {
                var clone = ProductAdImageBatchCloneHelper.CloneRow(source, copyReferenceImage: true);
                if (clone == null)
                {
                    continue;
                }

                _productAdImageBindingList.Add(clone);
                last = clone;
            }

            RenumberProductAdImageRows();
            if (last != null)
            {
                SelectProductAdImageItem(last);
            }

            LogProductAdImage("Đã copy " + selected.Count + " dòng.");
        }

        private void BtnProductAdImageDeleteRow_Click(object sender, EventArgs e)
        {
            var selected = GetSelectedProductAdImageItems();
            if (selected.Count == 0)
            {
                LogProductAdImage("Chọn ít nhất một dòng để xoá.");
                return;
            }

            foreach (var item in selected)
            {
                _productAdImageTrashStore.AddFromItem(item);
                _productAdImageBindingList.Remove(item);
            }

            RefreshProductAdImageTrashButtonLabel();
            RenumberProductAdImageRows();
            NotifyProductAdImageDraftDirty();
            LogProductAdImage("Đã chuyển " + selected.Count + " dòng vào thùng rác.");
        }

        private void DgvProductAdImage_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _productAdImageProfileBeforeEdit = null;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgvProductAdImage == null)
            {
                return;
            }

            if (!string.Equals(
                    dgvProductAdImage.Columns[e.ColumnIndex]?.Name,
                    "colProductAdImageProfile",
                    StringComparison.Ordinal))
            {
                return;
            }

            var item = dgvProductAdImage.Rows[e.RowIndex].DataBoundItem as ProductAdImageBatchItem;
            _productAdImageProfileBeforeEdit = item?.ProfileName;
        }

        private void DgvProductAdImage_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgvProductAdImage == null)
            {
                return;
            }

            if (!string.Equals(
                    dgvProductAdImage.Columns[e.ColumnIndex]?.Name,
                    "colProductAdImageProfile",
                    StringComparison.Ordinal))
            {
                return;
            }

            var item = dgvProductAdImage.Rows[e.RowIndex].DataBoundItem as ProductAdImageBatchItem;
            var oldProfile = _productAdImageProfileBeforeEdit;
            _productAdImageProfileBeforeEdit = null;
            if (oldProfile == null)
            {
                return;
            }

            RelocateProductAdImageAfterProfileChange(item, oldProfile);
        }

        private void RelocateProductAdImageAfterProfileChange(ProductAdImageBatchItem item, string oldProfile)
        {
            if (item == null)
            {
                return;
            }

            var newProfile = (item.ProfileName ?? string.Empty).Trim();
            var previous = (oldProfile ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newProfile)
                || string.Equals(previous, newProfile, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.ModelImagePath) || !System.IO.File.Exists(item.ModelImagePath))
            {
                return;
            }

            var oldFile = System.IO.Path.GetFileName(item.ModelImagePath);
            try
            {
                var dest = ProductAdImageReferenceHelper.RelocateMasterImage(item.ModelImagePath, newProfile);
                item.ModelImagePath = dest;
                NotifyProductAdImageDraftDirty();
                var destFolder = ProductAdImageReferenceHelper.GetRefsDirectory(newProfile);
                var msg = "Đã chuyển ảnh mẫu «" + oldFile + "» từ profile «"
                          + (string.IsNullOrEmpty(previous) ? "(trống)" : previous)
                          + "» sang «" + newProfile + "».";
                LogProductAdImage(msg);
                MessageBox.Show(
                    this,
                    msg + Environment.NewLine + Environment.NewLine + destFolder,
                    "Đã chuyển ảnh mẫu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogProductAdImage("Chuyển ảnh mẫu sang profile mới lỗi: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Không chuyển được ảnh mẫu sang profile «" + newProfile + "».\r\n" + ex.Message,
                    "Ảnh mẫu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void DgvProductAdImage_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgvProductAdImage == null)
            {
                return;
            }

            var column = dgvProductAdImage.Columns[e.ColumnIndex];
            var item = dgvProductAdImage.Rows[e.RowIndex].DataBoundItem as ProductAdImageBatchItem;
            if (column == null || item == null)
            {
                return;
            }

            HandleProductAdImageCellClick(column.Name, item, e);
        }

        private void HandleProductAdImageCellClick(string columnName, ProductAdImageBatchItem item, DataGridViewCellMouseEventArgs e)
        {
            if (string.Equals(columnName, "colProductAdImageImages", StringComparison.Ordinal))
            {
                HandleProductAdImageImagesClick(item, e);
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            switch (columnName)
            {
                case "colProductAdImagePromptSetup":
                    using (var dlg = new ProductAdImagePromptSetupDialog(item))
                    {
                        dlg.ShowDialog(this);
                    }

                    break;
                case "colProductAdImagePrompts":
                    using (var dlg = new ProductAdImagePromptListDialog(item))
                    {
                        dlg.ShowDialog(this);
                    }

                    break;
            }
        }

        private void HandleProductAdImageImagesClick(ProductAdImageBatchItem item, DataGridViewCellMouseEventArgs e)
        {
            if (item == null)
            {
                return;
            }

            var action = ShowcaseDualActionCellAction.None;
            if (dgvProductAdImage != null && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var display = dgvProductAdImage.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                action = HitTestShowcaseDualActionCell(display.Width, display.Height, new Point(e.X, e.Y));
            }

            if (e.Button == MouseButtons.Right || action == ShowcaseDualActionCellAction.OpenFolder)
            {
                try
                {
                    ProductAdImageReferenceHelper.OpenRefsFolder(item.ProfileName);
                }
                catch (Exception ex)
                {
                    LogProductAdImage("Không mở được folder ảnh mẫu: " + ex.Message);
                }

                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh mẫu (người + sản phẩm)",
                Filter = ProductAdImageReferenceHelper.OpenFileFilter,
                Multiselect = false,
                CheckFileExists = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    item.ModelImagePath = ProductAdImageReferenceHelper.CopyMasterImage(dlg.FileName, item.ProfileName);
                    LogProductAdImage("Đã chọn ảnh mẫu: " + System.IO.Path.GetFileName(item.ModelImagePath));
                }
                catch (Exception ex)
                {
                    LogProductAdImage("Chọn ảnh mẫu lỗi: " + ex.Message);
                }
            }
        }

        private ProductAdImageBatchItem CreateEmptyProductAdImageRow()
        {
            return new ProductAdImageBatchItem
            {
                ProfileName = GetDefaultProductAdImageProfileName(),
                ProductName = string.Empty,
                Status = "Chờ"
            };
        }

        private string GetDefaultProductAdImageProfileName()
        {
            if (_aiVideoGenProfileComboSource != null)
            {
                foreach (var entry in _aiVideoGenProfileComboSource)
                {
                    if (!string.IsNullOrWhiteSpace(entry?.Name))
                    {
                        return entry.Name.Trim();
                    }
                }
            }

            return "default";
        }

        private void EnsureProductAdImageBindingList()
        {
            if (_productAdImageBindingList != null)
            {
                return;
            }

            _productAdImageBindingList = new BindingList<ProductAdImageBatchItem>();
            AttachProductAdImageBindingListEvents();
            if (dgvProductAdImage != null && !dgvProductAdImage.IsDisposed)
            {
                dgvProductAdImage.DataSource = _productAdImageBindingList;
            }
        }

        private List<ProductAdImageBatchItem> GetSelectedProductAdImageItems()
        {
            var result = new List<ProductAdImageBatchItem>();
            if (dgvProductAdImage?.SelectedRows == null || dgvProductAdImage.SelectedRows.Count == 0)
            {
                if (dgvProductAdImage?.CurrentRow?.DataBoundItem is ProductAdImageBatchItem current)
                {
                    result.Add(current);
                }

                return result;
            }

            foreach (var gridRow in dgvProductAdImage.SelectedRows
                         .Cast<DataGridViewRow>()
                         .Where(r => r?.DataBoundItem is ProductAdImageBatchItem)
                         .OrderBy(r => r.Index))
            {
                result.Add((ProductAdImageBatchItem)gridRow.DataBoundItem);
            }

            return result;
        }

        private void SelectProductAdImageItem(ProductAdImageBatchItem item)
        {
            if (item == null || dgvProductAdImage == null)
            {
                return;
            }

            foreach (DataGridViewRow row in dgvProductAdImage.Rows)
            {
                if (!ReferenceEquals(row.DataBoundItem, item))
                {
                    continue;
                }

                dgvProductAdImage.ClearSelection();
                row.Selected = true;
                if (row.Cells.Count > 0)
                {
                    dgvProductAdImage.CurrentCell = row.Cells[0];
                }

                break;
            }
        }

        private void RenumberProductAdImageRows()
        {
            if (_productAdImageBindingList == null)
            {
                return;
            }

            var orderCol = dgvProductAdImage?.Columns["colProductAdImageOrder"];
            for (var i = 0; i < _productAdImageBindingList.Count; i++)
            {
                var item = _productAdImageBindingList[i];
                if (item == null)
                {
                    continue;
                }

                var next = i + 1;
                if (item.Order == next)
                {
                    continue;
                }

                item.Order = next;
                if (orderCol != null && i < dgvProductAdImage.Rows.Count)
                {
                    dgvProductAdImage.InvalidateCell(orderCol.Index, i);
                }
            }
        }

        private async void BtnProductAdImagePlanPrompt_Click(object sender, EventArgs e)
        {
            if (_productAdImagePlanRunning)
            {
                return;
            }

            var selected = GetSelectedProductAdImageItems();
            if (selected.Count == 0)
            {
                LogProductAdImage("Chọn ít nhất một dòng.");
                return;
            }

            await RefreshProductAdImageSettingsSnapAsync().ConfigureAwait(true);
            var apiKey = ResolveProductAdImageGeminiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                LogProductAdImage("Chưa có Gemini API key — mở Cài đặt để nhập.");
                RefreshProductAdImageReadinessLabel();
                return;
            }

            var runnable = selected.Where(CanPlanProductAdImageItem).ToList();
            foreach (var skipped in selected.Except(runnable))
            {
                LogProductAdImage("Bỏ qua dòng «" + (skipped?.ProductName ?? "") + "»: cần tên SP, ảnh mẫu và số ảnh > 0.");
            }

            if (runnable.Count == 0)
            {
                LogProductAdImage("Không có dòng đủ điều kiện để lập prompt.");
                return;
            }

            _productAdImagePlanCts?.Dispose();
            _productAdImagePlanCts = new CancellationTokenSource();
            var token = _productAdImagePlanCts.Token;
            SetProductAdImagePlanningUi(true);

            var succeeded = new List<ProductAdImageBatchItem>();
            try
            {
                var model = ProductAdImagePromptBuilder.ResolveGeminiModel(_productAdImageSettingsSnap?.AiModel);
                var index = 0;
                foreach (var item in runnable)
                {
                    token.ThrowIfCancellationRequested();
                    index++;
                    item.Status = "Đang lập…";
                    LogProductAdImage("Đang lập prompt " + index + "/" + runnable.Count
                                      + " — «" + (item.ProductName ?? string.Empty).Trim() + "» (" + model + ")…");
                    try
                    {
                        await PlanProductAdImagePromptsForItemAsync(item, apiKey, model, token).ConfigureAwait(true);
                        item.Status = "Xong";
                        succeeded.Add(item);
                        LogProductAdImage("Xong «" + item.ProductName + "»: " + item.GeneratedShots.Count + " prompt.");
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = "Chờ";
                        throw;
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Lỗi";
                        LogProductAdImage("Lỗi lập prompt «" + item.ProductName + "»: " + ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogProductAdImage("Đã dừng lập prompt.");
            }
            finally
            {
                SetProductAdImagePlanningUi(false);
            }

            if (succeeded.Count == 1)
            {
                using (var dlg = new ProductAdImagePromptListDialog(succeeded[0]))
                {
                    dlg.ShowDialog(this);
                }
            }
            else if (succeeded.Count > 1)
            {
                var ask = MessageBox.Show(
                    this,
                    "Đã lập prompt cho " + succeeded.Count + " dòng. Mở danh sách prompt?",
                    "Tạo ảnh AI",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (ask == DialogResult.Yes)
                {
                    foreach (var item in succeeded)
                    {
                        using (var dlg = new ProductAdImagePromptListDialog(item))
                        {
                            dlg.ShowDialog(this);
                        }
                    }
                }
            }
        }

        private void BtnProductAdImageStop_Click(object sender, EventArgs e)
        {
            try
            {
                _productAdImagePlanCts?.Cancel();
            }
            catch
            {
                // ignored
            }

            LogProductAdImage("Đang dừng lập prompt…");
        }

        private async Task PlanProductAdImagePromptsForItemAsync(
            ProductAdImageBatchItem item,
            string apiKey,
            string model,
            CancellationToken cancellationToken)
        {
            var result = await _geminiService.PlanProductAdImagePromptsAsync(
                item,
                apiKey,
                model,
                cancellationToken).ConfigureAwait(true);
            item.ReplaceGeneratedShots(result.Shots);
        }

        private static bool CanPlanProductAdImageItem(ProductAdImageBatchItem item)
        {
            return item != null && item.CanPlanPrompt;
        }

        private string ResolveProductAdImageGeminiApiKey()
        {
            var saved = (_productAdImageSettingsSnap?.AiApiKey ?? string.Empty).Trim();
            if (saved.Length > 0)
            {
                return saved;
            }

            return (txtAiApiKey?.Text ?? string.Empty).Trim();
        }

        private void SetProductAdImagePlanningUi(bool running)
        {
            _productAdImagePlanRunning = running;
            if (btnProductAdImagePlanPrompt != null)
            {
                btnProductAdImagePlanPrompt.Enabled = !running;
            }

            if (btnProductAdImageStop != null)
            {
                btnProductAdImageStop.Visible = running;
            }

            LayoutProductAdImageCommandBar();
        }

        private void WireProductAdImageGridContextMenu()
        {
            if (_cmsProductAdImageGrid != null)
            {
                dgvProductAdImage.ContextMenuStrip = _cmsProductAdImageGrid;
                return;
            }

            _cmsProductAdImageGrid = new ContextMenuStrip();
            var miExcel = new ToolStripMenuItem("Xuất Excel dòng này");
            miExcel.Click += (_, __) => ExportProductAdImageSelectedToExcel();
            var miCopy = new ToolStripMenuItem("Copy prompt");
            miCopy.Click += (_, __) => CopyProductAdImageSelectedPrompts();
            _cmsProductAdImageGrid.Items.Add(miExcel);
            _cmsProductAdImageGrid.Items.Add(miCopy);
            _cmsProductAdImageGrid.Opening += CmsProductAdImageGrid_Opening;
            dgvProductAdImage.ContextMenuStrip = _cmsProductAdImageGrid;
        }

        private void CmsProductAdImageGrid_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var item = GetSelectedProductAdImageItems().FirstOrDefault();
            var hasPrompts = item?.GeneratedShots != null && item.GeneratedShots.Count > 0;
            if (_cmsProductAdImageGrid.Items.Count >= 2)
            {
                _cmsProductAdImageGrid.Items[0].Enabled = hasPrompts;
                _cmsProductAdImageGrid.Items[1].Enabled = hasPrompts;
            }

            if (item == null)
            {
                e.Cancel = true;
            }
        }

        private void BtnProductAdImageStripWatermark_Click(object sender, EventArgs e)
        {
            using (var dlg = new ProductAdImageGeminiWatermarkDialog())
            {
                dlg.ShowDialog(this);
                if (dlg.ProcessedCount > 0)
                {
                    LogProductAdImage(
                        "Đã xoá logo Gemini trên " + dlg.ProcessedCount + " ảnh (file _nologo.png).");
                }
            }
        }

        private void ExportProductAdImageSelectedToExcel()
        {
            var item = GetSelectedProductAdImageItems().FirstOrDefault();
            if (item == null)
            {
                LogProductAdImage("Chọn một dòng để xuất Excel.");
                return;
            }

            if (item.GeneratedShots == null || item.GeneratedShots.Count == 0)
            {
                LogProductAdImage("Dòng «" + item.ProductName + "» chưa có prompt để xuất Excel.");
                return;
            }

            var folder = ProductAdImagePromptExcelHelper.GetDefaultFolder(item.ProfileName);
            using (var dlg = new SaveFileDialog
            {
                Title = "Xuất Excel prompt",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = ProductAdImagePromptExcelHelper.BuildExcelFileName(item.ProfileName, item.ProductName),
                InitialDirectory = folder
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    ProductAdImagePromptExcelHelper.ExportShots(dlg.FileName, item.GeneratedShots);
                    LogProductAdImage("Đã xuất Excel: " + System.IO.Path.GetFileName(dlg.FileName));
                }
                catch (Exception ex)
                {
                    LogProductAdImage("Xuất Excel lỗi: " + ex.Message);
                }
            }
        }

        private void CopyProductAdImageSelectedPrompts()
        {
            var item = GetSelectedProductAdImageItems().FirstOrDefault();
            if (item?.GeneratedShots == null || item.GeneratedShots.Count == 0)
            {
                LogProductAdImage("Chưa có prompt để copy.");
                return;
            }

            var lines = item.GeneratedShots
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Prompt))
                .Select(s => s.Index + ". " + s.Prompt);
            var text = string.Join(Environment.NewLine + Environment.NewLine, lines);
            if (string.IsNullOrWhiteSpace(text))
            {
                LogProductAdImage("Chưa có prompt để copy.");
                return;
            }

            Clipboard.SetText(text);
            LogProductAdImage("Đã copy " + item.GeneratedShots.Count + " prompt của «" + item.ProductName + "».");
        }
    }
}
