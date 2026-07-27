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
        public const int ColumnWidth = 100;

        private static readonly ConditionalWeakTable<DataGridView, object> HookedGrids =
            new ConditionalWeakTable<DataGridView, object>();

        /// <summary>Thêm hoặc ghim cột STT ở vị trí đầu tiên. An toàn gọi nhiều lần.</summary>
        public static void EnsureFirstColumn(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            if (string.Equals(grid.Tag as string, "SkipSttColumn", StringComparison.Ordinal))
            {
                return;
            }

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
                ApplySttColumnChrome(existing, grid);
            }

            if (existing.DisplayIndex != 0)
            {
                existing.DisplayIndex = 0;
            }

            ApplyColumnWidth(existing);

            EnsureGridHooks(grid);
        }

        /// <summary>Cố định rộng 100px — số canh giữa.</summary>
        public static void ApplyColumnWidth(DataGridViewColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = ColumnWidth;
            column.MinimumWidth = ColumnWidth;
            column.FillWeight = ColumnWidth;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        /// <summary>Giữ tương thích gọi từ Theme (bỏ qua tham số đo).</summary>
        public static void ApplyColumnWidth(
            DataGridView grid,
            DataGridViewColumn column,
            bool useFill,
            Font headerFont,
            Padding headerPadding)
        {
            ApplyColumnWidth(column);
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
            var column = new DataGridViewTextBoxColumn
            {
                Name = ColumnName,
                HeaderText = "STT",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            return column;
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

            ApplyColumnWidth(column);
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
            if (!string.Equals(column?.Name, ColumnName, StringComparison.Ordinal))
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
            if (grid == null || grid.IsDisposed || !grid.Columns.Contains(ColumnName))
            {
                return;
            }

            grid.InvalidateColumn(grid.Columns[ColumnName].Index);
        }
    }
}
