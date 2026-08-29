using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>Cột STT (số thứ tự dòng) — cột đầu tiên trên mọi lưới DataGridView.</summary>
    public static class AppGridSttColumn
    {
        public const string ColumnName = "colAppGridStt";

        /// <summary>Rộng chuẩn trên lưới chính (tab, hub, v.v.).</summary>
        public const int ColumnWidth = 100;

        /// <summary>Rộng gọn trong popup/dialog có nhiều cột.</summary>
        public const int CompactColumnWidth = 56;

        private static readonly ConditionalWeakTable<DataGridView, SttGridOptions> GridOptions =
            new ConditionalWeakTable<DataGridView, SttGridOptions>();

        private static readonly ConditionalWeakTable<DataGridView, object> HookedGrids =
            new ConditionalWeakTable<DataGridView, object>();

        private sealed class SttGridOptions
        {
            public int Width { get; set; } = ColumnWidth;
        }

        /// <summary>Thêm hoặc ghim cột STT ở vị trí đầu tiên. An toàn gọi nhiều lần.</summary>
        public static void EnsureFirstColumn(DataGridView grid, bool compact = false, int? width = null)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            if (string.Equals(grid.Tag as string, "SkipSttColumn", StringComparison.Ordinal))
            {
                return;
            }

            SetGridWidth(grid, width ?? (compact ? CompactColumnWidth : ColumnWidth));

            var existing = FindExistingSttColumn(grid);
            if (existing == null)
            {
                existing = CreateSttColumn();
                if (grid.Columns.Count == 0)
                {
                    grid.Columns.Add(existing);
                }
                else
                {
                    grid.Columns.Insert(0, existing);
                }

                ApplySttColumnChrome(existing, grid);
            }
            else
            {
                existing.Name = ColumnName;
                ApplySttColumnChrome(existing, grid);
            }

            if (existing.DisplayIndex != 0)
            {
                existing.DisplayIndex = 0;
            }

            ApplyColumnWidth(grid, existing);
            EnsureGridHooks(grid);
        }

        /// <summary>Cố định rộng theo profile lưới — số canh giữa.</summary>
        public static void ApplyColumnWidth(DataGridViewColumn column)
        {
            ApplyColumnWidth(null, column);
        }

        public static void ApplyColumnWidth(DataGridView grid, DataGridViewColumn column)
        {
            if (column == null)
            {
                return;
            }

            var width = ResolveColumnWidth(grid);
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = width;
            column.FillWeight = width;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column.HeaderCell.Style.Padding = Padding.Empty;
        }

        /// <summary>Giữ tương thích gọi từ Theme (bỏ qua tham số đo).</summary>
        public static void ApplyColumnWidth(
            DataGridView grid,
            DataGridViewColumn column,
            bool useFill,
            Font headerFont,
            Padding headerPadding)
        {
            ApplyColumnWidth(grid, column);
        }

        public static bool IsSttColumn(DataGridViewColumn column)
        {
            if (column == null)
            {
                return false;
            }

            if (string.Equals(column.Name, ColumnName, StringComparison.Ordinal)
                || string.Equals(column.Name, "colStt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals((column.HeaderText ?? string.Empty).Trim(), "STT", StringComparison.OrdinalIgnoreCase);
        }

        public static int ResolveColumnWidth(DataGridView grid)
        {
            if (grid != null && GridOptions.TryGetValue(grid, out var opts) && opts != null)
            {
                return opts.Width;
            }

            return ColumnWidth;
        }

        private static void SetGridWidth(DataGridView grid, int width)
        {
            if (grid == null)
            {
                return;
            }

            if (GridOptions.TryGetValue(grid, out var opts) && opts != null)
            {
                opts.Width = width;
                return;
            }

            GridOptions.Add(grid, new SttGridOptions { Width = width });
        }

        private static DataGridViewColumn FindExistingSttColumn(DataGridView grid)
        {
            DataGridViewColumn named = null;
            DataGridViewColumn headerMatch = null;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column == null)
                {
                    continue;
                }

                if (string.Equals(column.Name, ColumnName, StringComparison.Ordinal)
                    || string.Equals(column.Name, "colStt", StringComparison.OrdinalIgnoreCase))
                {
                    named = column;
                    break;
                }

                if (headerMatch == null
                    && string.Equals((column.HeaderText ?? string.Empty).Trim(), "STT", StringComparison.OrdinalIgnoreCase))
                {
                    headerMatch = column;
                }
            }

            return named ?? headerMatch;
        }

        private static DataGridViewTextBoxColumn CreateSttColumn()
        {
            return new DataGridViewTextBoxColumn
            {
                Name = ColumnName,
                HeaderText = "STT",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private static void ApplySttColumnChrome(DataGridViewColumn column, DataGridView grid)
        {
            if (column == null)
            {
                return;
            }

            column.HeaderText = "STT";
            column.ReadOnly = true;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;

            if (grid != null && grid.DefaultCellStyle?.Font != null)
            {
                column.DefaultCellStyle.Font = grid.DefaultCellStyle.Font;
            }

            ApplyColumnWidth(grid, column);
        }

        private static void EnsureGridHooks(DataGridView grid)
        {
            if (HookedGrids.TryGetValue(grid, out _))
            {
                return;
            }

            HookedGrids.Add(grid, null);
            grid.CellFormatting += Grid_CellFormatting;
            grid.Sorted += Grid_RefreshStt;
            grid.RowsAdded += Grid_RefreshStt;
            grid.RowsRemoved += Grid_RefreshStt;
        }

        private static void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var column = grid.Columns[e.ColumnIndex];
            if (!IsSttColumn(column))
            {
                return;
            }

            var row = grid.Rows[e.RowIndex];
            if (row.IsNewRow)
            {
                e.Value = string.Empty;
            }
            else
            {
                e.Value = (e.RowIndex + 1).ToString();
            }

            e.FormattingApplied = true;
        }

        private static void Grid_RefreshStt(object sender, EventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            var sttColumn = FindExistingSttColumn(grid);
            if (sttColumn == null)
            {
                return;
            }

            grid.InvalidateColumn(sttColumn.Index);
        }
    }
}
