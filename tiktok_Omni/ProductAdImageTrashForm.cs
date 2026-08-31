using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class ProductAdImageTrashForm : Form
    {
        private readonly ProductAdImageTrashStore _store;
        private readonly List<ProductAdImageBatchItem> _restoredItems = new List<ProductAdImageBatchItem>();
        private readonly DataGridView _grid;
        private readonly Label _lblHint;
        private readonly Button _btnRestore;
        private readonly Button _btnDeleteForever;

        public IReadOnlyList<ProductAdImageBatchItem> RestoredItems => _restoredItems;

        public ProductAdImageTrashForm(ProductAdImageTrashStore store)
        {
            _store = store ?? new ProductAdImageTrashStore();

            Text = "Thùng rác Tạo ảnh AI";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(920, 520);
            ClientSize = new Size(1100, 640);
            Font = new Font("Segoe UI", 11F);

            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 56,
                Padding = new Padding(12, 12, 12, 0),
                ForeColor = Color.FromArgb(180, 186, 198),
                Text = "Khôi phục dòng đã xoá hoặc xoá vĩnh viễn."
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                Tag = "SkipSttColumn"
            };
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(44, 48, 58);
            _grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 98, 148);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 31, 38);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(190, 196, 208);
            _grid.Columns.Add("colName", "Tên SP");
            _grid.Columns.Add("colProfile", "Profile");
            _grid.Columns.Add("colShots", "Prompt");
            _grid.Columns.Add("colDeleted", "Xóa lúc");
            _grid.SelectionChanged += (_, __) => UpdateButtonState();
            _grid.CellDoubleClick += (_, __) => RestoreSelected();

            _btnRestore = CreateButton("Khôi phục", Color.FromArgb(56, 110, 78));
            _btnRestore.Click += (_, __) => RestoreSelected();
            _btnDeleteForever = CreateButton("Xoá vĩnh viễn", Color.FromArgb(148, 58, 58));
            _btnDeleteForever.Click += (_, __) => DeleteSelectedForever();
            var btnClose = CreateButton("Đóng", Color.FromArgb(68, 72, 86));
            btnClose.Click += (_, __) => Close();

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(28, 31, 38)
            };
            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            flp.Controls.Add(_btnRestore);
            flp.Controls.Add(_btnDeleteForever);
            flp.Controls.Add(btnClose);
            pnlBottom.Controls.Add(flp);

            Controls.Add(_grid);
            Controls.Add(_lblHint);
            Controls.Add(pnlBottom);
            CancelButton = btnClose;
            Load += (_, __) => ReloadGrid();
        }

        private void ReloadGrid()
        {
            _grid.Rows.Clear();
            foreach (var entry in _store.LoadActiveEntries())
            {
                var item = entry.Item;
                var row = new DataGridViewRow();
                row.CreateCells(
                    _grid,
                    string.IsNullOrWhiteSpace(item?.ProductName) ? "(Không tên)" : item.ProductName,
                    item?.ProfileName ?? string.Empty,
                    (item?.GeneratedShots?.Count ?? 0).ToString(),
                    entry.DeletedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                row.Tag = entry.TrashId;
                _grid.Rows.Add(row);
            }

            _lblHint.Text = _grid.Rows.Count == 0
                ? "Thùng rác trống."
                : "Chọn dòng rồi bấm «Khôi phục» hoặc double-click.";
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            var hasSelection = _grid.CurrentRow?.Tag is Guid;
            _btnRestore.Enabled = hasSelection;
            _btnDeleteForever.Enabled = hasSelection;
        }

        private void RestoreSelected()
        {
            if (!(_grid.CurrentRow?.Tag is Guid trashId))
            {
                return;
            }

            var item = _store.TryTakeItemForRestore(trashId);
            if (item == null)
            {
                MessageBox.Show(this, "Không khôi phục được dòng này.", "Thùng rác",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ReloadGrid();
                return;
            }

            if (item.RowId == Guid.Empty)
            {
                item.RowId = Guid.NewGuid();
            }

            _restoredItems.Add(item);
            ReloadGrid();
        }

        private void DeleteSelectedForever()
        {
            if (!(_grid.CurrentRow?.Tag is Guid trashId))
            {
                return;
            }

            var name = _grid.CurrentRow.Cells[0].Value?.ToString() ?? "dòng này";
            if (MessageBox.Show(
                    this,
                    "Xóa vĩnh viễn «" + name + "» khỏi thùng rác?\nKhông thể hoàn tác.",
                    "Xóa vĩnh viễn",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            _store.TryRemove(trashId);
            ReloadGrid();
        }

        private static Button CreateButton(string text, Color back)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.FromArgb(245, 247, 250),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
