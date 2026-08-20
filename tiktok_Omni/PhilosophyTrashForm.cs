using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class PhilosophyTrashForm : Form
    {
        private const int GridRowHeight = 56;
        private const int GridHeaderHeight = 56;

        private readonly PhilosophyTrashStore _store;
        private readonly List<PhilosophyBatchItem> _restoredBatches = new List<PhilosophyBatchItem>();
        private readonly DataGridView _grid;
        private readonly Label _lblHint;
        private readonly Button _btnRestore;
        private readonly Button _btnDeleteForever;

        public IReadOnlyList<PhilosophyBatchItem> RestoredBatches => _restoredBatches;

        public PhilosophyTrashForm(PhilosophyTrashStore store)
        {
            _store = store ?? new PhilosophyTrashStore();

            Text = "Thùng rác Video Quote";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(920, 520);
            ClientSize = new Size(1100, 640);
            Font = new Font("Segoe UI", 10F);

            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 72,
                Padding = new Padding(12, 16, 12, 0),
                ForeColor = Color.FromArgb(180, 186, 198),
                Text = "Dòng xóa được giữ tối đa 24 giờ — hết hạn sẽ tự xóa vĩnh viễn."
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
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = GridHeaderHeight,
                RowTemplate = { Height = GridRowHeight },
                EnableHeadersVisualStyles = false
            };
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(44, 48, 58);
            _grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 98, 148);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 31, 38);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(190, 196, 208);
            _grid.Columns.Add("colTopic", "Chủ đề");
            _grid.Columns.Add("colQuotes", "Số câu");
            _grid.Columns.Add("colDeleted", "Xóa lúc");
            _grid.Columns.Add("colRemaining", "Còn lại");
            _grid.SelectionChanged += (_, __) => UpdateButtonState();
            _grid.CellDoubleClick += (_, __) => RestoreSelected();

            _btnRestore = CreateButton("Khôi phục", Color.FromArgb(56, 110, 78));
            _btnRestore.Click += (_, __) => RestoreSelected();
            _btnDeleteForever = CreateButton("Xóa vĩnh viễn", Color.FromArgb(148, 58, 58));
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
                var batch = entry.Batch;
                var deletedLocal = entry.DeletedAtUtc.ToLocalTime();
                var expiresUtc = entry.DeletedAtUtc.AddHours(PhilosophyTrashStore.RetentionHours);
                var remaining = expiresUtc - DateTime.UtcNow;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                var row = new DataGridViewRow { Height = GridRowHeight };
                row.CreateCells(_grid,
                    PhilosophyBatchHelper.TrimGridLabel(batch?.Topic, 64, "(Không chủ đề)"),
                    (batch?.Quotes?.Count ?? 0).ToString(),
                    deletedLocal.ToString("dd/MM/yyyy HH:mm"),
                    FormatRemaining(remaining));
                row.Tag = entry.TrashId;
                _grid.Rows.Add(row);
            }

            _lblHint.Text = _grid.Rows.Count == 0
                ? "Thùng rác trống — dòng xóa được giữ tối đa 24 giờ."
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

            var batch = _store.TryTakeBatchForRestore(trashId);
            if (batch == null)
            {
                MessageBox.Show(this,
                    "Không khôi phục được — có thể dòng đã hết hạn 24 giờ.",
                    "Thùng rác",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ReloadGrid();
                return;
            }

            batch.BatchId = Guid.NewGuid();
            batch.RefreshDerivedFields();
            _restoredBatches.Add(batch);
            ReloadGrid();
        }

        private void DeleteSelectedForever()
        {
            if (!(_grid.CurrentRow?.Tag is Guid trashId))
            {
                return;
            }

            var name = _grid.CurrentRow.Cells[0].Value?.ToString() ?? "dòng này";
            if (MessageBox.Show(this,
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

        private static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                return "Hết hạn";
            }

            if (remaining.TotalHours >= 1)
            {
                return Math.Floor(remaining.TotalHours) + " giờ";
            }

            return Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)) + " phút";
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
