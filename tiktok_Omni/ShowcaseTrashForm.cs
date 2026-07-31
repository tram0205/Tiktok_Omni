using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseTrashForm : Form
    {
        private const int GridRowHeight = 72;
        private const int GridHeaderHeight = 72;
        private const int DefaultClientWidth = 1800;
        private const int DefaultClientHeight = 960;
        private const int MinimumClientWidth = 1520;
        private const int MinimumClientHeight = 840;

        private readonly ShowcaseTrashStore _store;
        private readonly Func<AiVideoGenInputItem, AiVideoGenInputItem> _cloneScene;
        private readonly List<ShowcaseVideoItem> _restoredVideos = new List<ShowcaseVideoItem>();
        private readonly DataGridView _grid;
        private readonly Label _lblHint;
        private readonly Button _btnRestore;
        private readonly Button _btnDeleteForever;

        public IReadOnlyList<ShowcaseVideoItem> RestoredVideos => _restoredVideos;

        public ShowcaseTrashForm(
            ShowcaseTrashStore store,
            Func<AiVideoGenInputItem, AiVideoGenInputItem> cloneScene)
        {
            _store = store ?? new ShowcaseTrashStore();
            _cloneScene = cloneScene ?? throw new ArgumentNullException(nameof(cloneScene));

            Text = "Thùng rác Showcase";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(MinimumClientWidth, MinimumClientHeight);
            ClientSize = new Size(DefaultClientWidth, DefaultClientHeight);
            Font = new Font("Segoe UI", 11F);

            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 88,
                Padding = new Padding(12, 20, 12, 0),
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
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                EnableHeadersVisualStyles = false
            };
            _grid.DefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(44, 48, 58);
            _grid.DefaultCellStyle.ForeColor = Color.FromArgb(230, 234, 242);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 98, 156);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 56, 68);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(220, 224, 232);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProduct",
                HeaderText = "Sản phẩm / video",
                FillWeight = 42
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colScenes",
                HeaderText = "Cảnh",
                FillWeight = 10
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDeletedAt",
                HeaderText = "Xóa lúc",
                FillWeight = 22
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRemaining",
                HeaderText = "Còn lại",
                FillWeight = 16
            });
            _grid.CellDoubleClick += (_, __) => RestoreSelected();
            _grid.SelectionChanged += (_, __) => UpdateButtonState();

            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(12, 20, 12, 24),
                BackColor = BackColor
            };

            var btnClose = CreateButton("Đóng", Color.FromArgb(68, 72, 86));
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Click += (_, __) => Close();

            _btnDeleteForever = CreateButton("Xóa vĩnh viễn", Color.FromArgb(168, 52, 52));
            _btnDeleteForever.Click += (_, __) => DeleteSelectedForever();

            _btnRestore = CreateButton("Khôi phục", Color.FromArgb(52, 120, 82));
            _btnRestore.Click += (_, __) => RestoreSelected();

            pnlBottom.Controls.Add(btnClose);
            pnlBottom.Controls.Add(_btnDeleteForever);
            pnlBottom.Controls.Add(_btnRestore);

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = BackColor
            };
            host.Controls.Add(_grid);
            host.Controls.Add(_lblHint);

            Controls.Add(host);
            Controls.Add(pnlBottom);

            CancelButton = btnClose;
            Load += (_, __) => ReloadGrid();
        }

        private void ReloadGrid()
        {
            _grid.Rows.Clear();
            foreach (var entry in _store.LoadActiveEntries())
            {
                var video = entry.Video;
                var sceneCount = video?.Scenes?.Count ?? 0;
                var deletedLocal = entry.DeletedAtUtc.ToLocalTime();
                var expiresUtc = entry.DeletedAtUtc.AddHours(ShowcaseTrashStore.RetentionHours);
                var remaining = expiresUtc - DateTime.UtcNow;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                var row = new DataGridViewRow { Height = GridRowHeight };
                row.CreateCells(_grid,
                    video?.ProductName ?? "(Không tên)",
                    sceneCount.ToString(),
                    deletedLocal.ToString("dd/MM/yyyy HH:mm"),
                    FormatRemaining(remaining));
                row.Tag = entry.TrashId;
                row.Resizable = DataGridViewTriState.False;
                _grid.Rows.Add(row);
            }

            _lblHint.Text = _grid.Rows.Count == 0
                ? "Thùng rác trống — dòng xóa được giữ tối đa 24 giờ."
                : "Dòng xóa được giữ tối đa 24 giờ — chọn dòng rồi bấm «Khôi phục» hoặc double-click.";
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

            var video = _store.TryTakeVideoForRestore(trashId, _cloneScene);
            if (video == null)
            {
                MessageBox.Show(this,
                    "Không khôi phục được — có thể dòng đã hết hạn 24 giờ.",
                    "Thùng rác",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ReloadGrid();
                return;
            }

            video.RefreshDisplayFields();
            _restoredVideos.Add(video);
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
                MinimumSize = new Size(120, 68),
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
