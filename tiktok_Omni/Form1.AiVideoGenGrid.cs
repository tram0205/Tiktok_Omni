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

        private static readonly Color AiVideoGenProcessedRowBack = Color.FromArgb(210, 240, 220);

        private static readonly Color AiVideoGenProcessedRowFore = Color.FromArgb(24, 48, 32);

        private List<ProfileComboEntry> _aiVideoGenProfileComboSource = new List<ProfileComboEntry>();

        private const string AiVideoGenDragDropFormat = "tiktok_Omni.AiVideoGenInputItem";

        private void ConfigureProductInputGrid(DataGridView grid)

        {

            if (grid == null)

            {

                return;

            }



            grid.AutoGenerateColumns = false;

            grid.Columns.Clear();

            grid.Columns.Add(new DataGridViewComboBoxColumn

            {

                Name = "colAiProfile",

                HeaderText = "Profile",

                DataPropertyName = nameof(AiVideoGenInputItem.ProfileName),

                DisplayMember = nameof(ProfileComboEntry.Name),

                ValueMember = nameof(ProfileComboEntry.Name),

                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,

                FlatStyle = FlatStyle.Flat,

                FillWeight = 14,

                MinimumWidth = 72,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiProduct",

                HeaderText = "Sản phẩm",

                DataPropertyName = nameof(AiVideoGenInputItem.ProductName),

                FillWeight = 32,

                MinimumWidth = 120,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiUrl",

                HeaderText = "URL video",

                DataPropertyName = nameof(AiVideoGenInputItem.VideoUrl),

                FillWeight = 22,

                MinimumWidth = 100,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiHook",

                HeaderText = "Hook",

                DataPropertyName = nameof(AiVideoGenInputItem.HookText),

                FillWeight = 20,

                MinimumWidth = 88,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiHashtag",

                HeaderText = "Hashtag",

                DataPropertyName = nameof(AiVideoGenInputItem.Hashtags),

                FillWeight = 18,

                MinimumWidth = 80,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiPrice",

                HeaderText = "Giá",

                DataPropertyName = nameof(AiVideoGenInputItem.Price),

                FillWeight = 10,

                MinimumWidth = 56,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiStatus",

                HeaderText = "Trạng thái",

                DataPropertyName = nameof(AiVideoGenInputItem.PipelineStatus),

                FillWeight = 14,

                MinimumWidth = 88,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiSafety",

                HeaderText = "SafetyScore",

                DataPropertyName = nameof(AiVideoGenInputItem.SafetyScore),

                FillWeight = 10,

                MinimumWidth = 64,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiThumb",

                HeaderText = "Thumbnail Preview",

                DataPropertyName = nameof(AiVideoGenInputItem.ThumbnailPath),

                FillWeight = 16,

                MinimumWidth = 100,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiKeyword",

                HeaderText = "Từ khóa",

                DataPropertyName = nameof(AiVideoGenInputItem.SourceKeyword),

                FillWeight = 18,

                MinimumWidth = 80,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiImage",

                HeaderText = "Ảnh URL",

                DataPropertyName = nameof(AiVideoGenInputItem.ImageUrl),

                FillWeight = 22,

                MinimumWidth = 100,

                ReadOnly = true

            });



            ApplyAiVideoGenProfileComboColumn(grid);

            grid.CellFormatting -= ProductInputGrid_CellFormatting;

            grid.CellFormatting += ProductInputGrid_CellFormatting;

            grid.RowPrePaint -= ProductInputGrid_RowPrePaint;

            grid.RowPrePaint += ProductInputGrid_RowPrePaint;

            grid.CurrentCellDirtyStateChanged -= ProductInputGrid_CurrentCellDirtyStateChanged;

            grid.CurrentCellDirtyStateChanged += ProductInputGrid_CurrentCellDirtyStateChanged;

            grid.DataError -= ProductInputGrid_DataError;

            grid.DataError += ProductInputGrid_DataError;

            if (IsSlideshowProductInputGrid(grid))
            {
                WireSlideshowProductGridDragDrop(grid);
            }

        }

        private static bool IsSlideshowProductInputGrid(DataGridView grid)
        {
            if (grid == null)
            {
                return false;
            }

            return string.Equals(grid.Name, "dgvSlideshow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(grid.Name, "dgvAiVideoGenInput", StringComparison.OrdinalIgnoreCase);
        }

        private void WireSlideshowProductGridDragDrop(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.AllowDrop = true;

            grid.MouseDown -= SlideshowProductGrid_MouseDown;
            grid.MouseDown += SlideshowProductGrid_MouseDown;
            grid.DragOver -= SlideshowProductGrid_DragOver;
            grid.DragOver += SlideshowProductGrid_DragOver;
            grid.DragDrop -= SlideshowProductGrid_DragDrop;
            grid.DragDrop += SlideshowProductGrid_DragDrop;
        }

        private void SlideshowProductGrid_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var grid = sender as DataGridView;
            if (grid == null || grid.IsCurrentCellInEditMode)
            {
                return;
            }

            var hit = grid.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0 || hit.RowIndex >= grid.Rows.Count || grid.Rows[hit.RowIndex].IsNewRow)
            {
                return;
            }

            if (!(grid.Rows[hit.RowIndex].DataBoundItem is AiVideoGenInputItem item))
            {
                return;
            }

            grid.ClearSelection();
            grid.Rows[hit.RowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[Math.Max(0, hit.ColumnIndex)];

            grid.DoDragDrop(item, DragDropEffects.Move);
        }

        private void SlideshowProductGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(AiVideoGenDragDropFormat)
                || e.Data.GetDataPresent(typeof(AiVideoGenInputItem)))
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void SlideshowProductGrid_DragDrop(object sender, DragEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
            {
                return;
            }

            var source = ExtractDraggedAiVideoGenItem(e.Data);
            if (source == null)
            {
                return;
            }

            var client = grid.PointToClient(new Point(e.X, e.Y));
            var visibleInsertIndex = ResolveSlideshowVisibleInsertIndex(grid, client, out var originalVisibleIndex, source);
            if (visibleInsertIndex < 0)
            {
                return;
            }

            if (originalVisibleIndex >= 0
                && (visibleInsertIndex == originalVisibleIndex || visibleInsertIndex == originalVisibleIndex + 1))
            {
                return;
            }

            if (!TryMoveSlideshowItemToVisibleIndex(source, visibleInsertIndex))
            {
                return;
            }

            SyncBuffersToGrids();
            _slideshowDraftDirty = true;
            SelectSlideshowGridItem(grid, source);
        }

        private static AiVideoGenInputItem ExtractDraggedAiVideoGenItem(IDataObject data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.GetDataPresent(typeof(AiVideoGenInputItem)))
            {
                return data.GetData(typeof(AiVideoGenInputItem)) as AiVideoGenInputItem;
            }

            if (data.GetDataPresent(AiVideoGenDragDropFormat))
            {
                return data.GetData(AiVideoGenDragDropFormat) as AiVideoGenInputItem;
            }

            return null;
        }

        private int ResolveSlideshowVisibleInsertIndex(
            DataGridView grid,
            Point clientPoint,
            out int sourceVisibleIndex,
            AiVideoGenInputItem source)
        {
            sourceVisibleIndex = -1;
            if (grid == null)
            {
                return -1;
            }

            var visibleBefore = GetSlideshowBuffer()
                .Where(ShouldShowAiVideoGenItem)
                .ToList();
            sourceVisibleIndex = FindBufferItemIndex(visibleBefore, source);

            var hit = grid.HitTest(clientPoint.X, clientPoint.Y);
            if (hit.RowIndex >= 0 && hit.RowIndex < grid.Rows.Count && !grid.Rows[hit.RowIndex].IsNewRow)
            {
                var insertAfter = false;
                var rowRect = grid.GetRowDisplayRectangle(hit.RowIndex, false);
                if (rowRect.Height > 0)
                {
                    insertAfter = clientPoint.Y > rowRect.Top + (rowRect.Height / 2);
                }

                return hit.RowIndex + (insertAfter ? 1 : 0);
            }

            return visibleBefore.Count;
        }

        private bool TryMoveSlideshowItemToVisibleIndex(AiVideoGenInputItem source, int visibleInsertIndex)
        {
            var buffer = GetSlideshowBuffer();
            var from = FindBufferItemIndex(buffer, source);
            if (from < 0)
            {
                return false;
            }

            var item = buffer[from];
            buffer.RemoveAt(from);

            var visible = buffer.Where(ShouldShowAiVideoGenItem).ToList();
            visibleInsertIndex = Math.Max(0, Math.Min(visibleInsertIndex, visible.Count));

            int bufferInsertIndex;
            if (visibleInsertIndex >= visible.Count)
            {
                bufferInsertIndex = buffer.Count;
            }
            else
            {
                bufferInsertIndex = FindBufferItemIndex(buffer, visible[visibleInsertIndex]);
                if (bufferInsertIndex < 0)
                {
                    bufferInsertIndex = buffer.Count;
                }
            }

            buffer.Insert(bufferInsertIndex, item);
            return true;
        }

        private static int FindBufferItemIndex(IList<AiVideoGenInputItem> list, AiVideoGenInputItem item)
        {
            if (list == null || item == null)
            {
                return -1;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    return i;
                }
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (AiVideoGenItemsMatch(list[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SelectSlideshowGridItem(DataGridView grid, AiVideoGenInputItem item)
        {
            if (grid == null || item == null)
            {
                return;
            }

            for (var i = 0; i < grid.Rows.Count; i++)
            {
                var row = grid.Rows[i];
                if (row?.DataBoundItem is AiVideoGenInputItem bound
                    && (ReferenceEquals(bound, item) || AiVideoGenItemsMatch(bound, item)))
                {
                    grid.ClearSelection();
                    row.Selected = true;
                    if (row.Cells.Count > 0)
                    {
                        grid.CurrentCell = row.Cells[0];
                    }

                    grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i);
                    break;
                }
            }
        }



        private void ApplyGridProfileComboColumn(DataGridView grid, string columnName)

        {

            if (grid == null)

            {

                return;

            }



            if (!(grid.Columns[columnName] is DataGridViewComboBoxColumn profileColumn))

            {

                return;

            }



            profileColumn.DisplayMember = nameof(ProfileComboEntry.Name);

            profileColumn.ValueMember = nameof(ProfileComboEntry.Name);

            profileColumn.DataSource = _aiVideoGenProfileComboSource;

        }



        private void ApplyAiVideoGenProfileComboColumn(DataGridView grid)

        {

            ApplyGridProfileComboColumn(grid, "colAiProfile");

        }




        private void ProductInputGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)

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



        private void ProductInputGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)

        {

            if (e.ColumnIndex < 0)

            {

                return;

            }



            var grid = sender as DataGridView;

            var col = grid?.Columns[e.ColumnIndex];

            if (col?.Name == "colAiProfile")

            {

                e.ThrowException = false;

            }

        }



        private void ProductInputGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)

        {

            var grid = sender as DataGridView;

            if (grid == null || e.RowIndex < 0)

            {

                return;

            }



            var col = grid.Columns[e.ColumnIndex];

            if (col?.Name != "colAiStatus")

            {

                return;

            }



            var row = grid.Rows[e.RowIndex];

            if (row?.DataBoundItem is AiVideoGenInputItem item)

            {

                if (!string.IsNullOrWhiteSpace(item.PipelineStatus) && item.PipelineStatus != "Chờ")

                {

                    e.Value = item.PipelineStatus;

                }

                else

                {

                    e.Value = item.IsProcessed ? "Xong" : "Chờ";

                }



                e.FormattingApplied = true;

            }

        }



        private void ProductInputGrid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)

        {

            var grid = sender as DataGridView;

            if (grid == null || e.RowIndex < 0)

            {

                return;

            }



            var row = grid.Rows[e.RowIndex];

            if (row?.DataBoundItem is AiVideoGenInputItem item && item.IsProcessed)

            {

                row.DefaultCellStyle.BackColor = AiVideoGenProcessedRowBack;

                row.DefaultCellStyle.ForeColor = AiVideoGenProcessedRowFore;

                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(120, 180, 140);

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



        private void ChkAiVideoGenCurrentProfileOnly_CheckedChanged(object sender, EventArgs e)

        {

            SyncBuffersToGrids();

        }



        private void MarkSlideshowItemsProcessed(IEnumerable<AiVideoGenInputItem> renderedItems)

        {

            MarkBufferItemsProcessed(GetSlideshowBuffer(), renderedItems, SyncBuffersToGrids, () => _slideshowDraftDirty = true);

        }



        private void MarkDeepDiveItemsProcessed(IEnumerable<AiVideoGenInputItem> renderedItems)

        {

            MarkBufferItemsProcessed(GetDeepDiveBuffer(), renderedItems, SyncBuffersToGrids, null);

        }



        private void MarkBufferItemsProcessed(

            List<AiVideoGenInputItem> buffer,

            IEnumerable<AiVideoGenInputItem> renderedItems,

            Action refreshGrid,

            Action onChanged)

        {

            if (renderedItems == null || buffer == null)

            {

                return;

            }



            var changed = false;

            foreach (var rendered in renderedItems)

            {

                if (rendered == null)

                {

                    continue;

                }



                var match = buffer.FirstOrDefault(x => AiVideoGenItemsMatch(x, rendered));

                if (match != null && !match.IsProcessed)

                {

                    match.IsProcessed = true;

                    changed = true;

                }

            }



            if (changed)

            {

                onChanged?.Invoke();

                refreshGrid?.Invoke();

            }

        }

        private List<AiVideoGenInputItem> GetSelectedAiVideoGenItemsForPipeline()
        {
            if (!TryGetSelectedAiVideoGenItems(out var selected))
            {
                return new List<AiVideoGenInputItem>();
            }

            return selected
                .Where(x => x != null)
                .ToList();
        }

        private List<AiVideoGenInputItem> CloneAiVideoGenItemsForPipeline(IEnumerable<AiVideoGenInputItem> items)
        {
            return (items ?? Enumerable.Empty<AiVideoGenInputItem>())
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList();
        }

        private void SetAiVideoGenPipelineProgress(VideoRenderProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            progress.Slot = progress.Slot <= 0 ? 1 : progress.Slot;
            progress.VideoIndex = progress.VideoIndex <= 0 ? 1 : progress.VideoIndex;
            progress.TotalVideos = progress.TotalVideos <= 0 ? 1 : progress.TotalVideos;
            UpdateAiRenderProgress(progress);
        }

    }

}


