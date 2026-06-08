using System;

using System.Collections.Generic;

using System.Drawing;

using System.Linq;

using System.Windows.Forms;

using tiktok_Omni.Services;



namespace tiktok_Omni

{

    public partial class Form1

    {

        private static readonly Color AiVideoGenProcessedRowBack = Color.FromArgb(210, 240, 220);

        private static readonly Color AiVideoGenProcessedRowFore = Color.FromArgb(24, 48, 32);



        private void ConfigureProductInputGrid(DataGridView grid)

        {

            if (grid == null)

            {

                return;

            }



            grid.AutoGenerateColumns = false;

            grid.Columns.Clear();

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiProfile",

                HeaderText = "Profile",

                DataPropertyName = nameof(AiVideoGenInputItem.ProfileName),

                FillWeight = 14,

                MinimumWidth = 72

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiProduct",

                HeaderText = "Sản phẩm",

                DataPropertyName = nameof(AiVideoGenInputItem.ProductName),

                FillWeight = 32,

                MinimumWidth = 120

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiPrice",

                HeaderText = "Giá",

                DataPropertyName = nameof(AiVideoGenInputItem.Price),

                FillWeight = 10,

                MinimumWidth = 56

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiStatus",

                HeaderText = "Trạng thái",

                DataPropertyName = nameof(AiVideoGenInputItem.PipelineStatus),

                FillWeight = 14,

                MinimumWidth = 88

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiSafety",

                HeaderText = "SafetyScore",

                DataPropertyName = nameof(AiVideoGenInputItem.SafetyScore),

                FillWeight = 10,

                MinimumWidth = 64

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiThumb",

                HeaderText = "Thumbnail Preview",

                DataPropertyName = nameof(AiVideoGenInputItem.ThumbnailPath),

                FillWeight = 16,

                MinimumWidth = 100

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiKeyword",

                HeaderText = "Từ khóa",

                DataPropertyName = nameof(AiVideoGenInputItem.SourceKeyword),

                FillWeight = 18,

                MinimumWidth = 80

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiImage",

                HeaderText = "Ảnh URL",

                DataPropertyName = nameof(AiVideoGenInputItem.ImageUrl),

                FillWeight = 22,

                MinimumWidth = 100

            });



            grid.CellFormatting -= ProductInputGrid_CellFormatting;

            grid.CellFormatting += ProductInputGrid_CellFormatting;

            grid.RowPrePaint -= ProductInputGrid_RowPrePaint;

            grid.RowPrePaint += ProductInputGrid_RowPrePaint;

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

    }

}


