using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class ProductAdImagePromptListDialog : Form
    {
        private readonly ProductAdImageBatchItem _item;
        private readonly BindingList<ProductAdImageShotPlan> _bindingList;
        private DataGridView _grid;
        private Label _lblHint;
        private Panel _pnlEmpty;
        private Button _btnCopySelected;
        private Button _btnCopyAll;
        private Button _btnExcel;
        private ContextMenuStrip _cms;

        public ProductAdImagePromptListDialog(ProductAdImageBatchItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _bindingList = new BindingList<ProductAdImageShotPlan>(
                (_item.GeneratedShots ?? new List<ProductAdImageShotPlan>())
                    .Where(s => s != null)
                    .ToList());
            _bindingList.ListChanged += (_, __) => RefreshChrome();

            var product = string.IsNullOrWhiteSpace(_item.ProductName)
                ? "(chưa đặt tên)"
                : _item.ProductName.Trim();
            Text = "Prompt — «" + product + "»";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(1200, 720);
            ClientSize = new Size(1280, 780);
            Padding = new Padding(16);

            BuildUi();
            RefreshChrome();
            FormClosing += (_, __) => CommitShots();
        }

        private void BuildUi()
        {
            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 64,
                AutoSize = false,
                AutoEllipsis = false,
                Padding = new Padding(2, 8, 2, 12),
                ForeColor = Color.FromArgb(180, 186, 198),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true
            };

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(0, 12, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var flpLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = Padding.Empty
            };

            _btnCopySelected = CreateButton("Copy dòng", Color.FromArgb(88, 94, 112));
            _btnCopySelected.Click += (_, __) => CopySelected();
            _btnCopyAll = CreateButton("Copy tất cả", Color.FromArgb(56, 110, 148));
            _btnCopyAll.Click += (_, __) => CopyAll();
            _btnExcel = CreateButton("📊 Xuất Excel", Color.FromArgb(52, 92, 158));
            _btnExcel.Click += (_, __) => ExportExcel();
            var btnClose = CreateButton("Đóng", Color.FromArgb(68, 72, 86));
            btnClose.Click += (_, __) =>
            {
                CommitShots();
                DialogResult = DialogResult.OK;
                Close();
            };
            btnClose.Dock = DockStyle.Right;
            btnClose.Margin = new Padding(12, 0, 0, 0);

            flpLeft.Controls.Add(_btnCopySelected);
            flpLeft.Controls.Add(_btnCopyAll);
            flpLeft.Controls.Add(_btnExcel);
            pnlBottom.Controls.Add(flpLeft);
            pnlBottom.Controls.Add(btnClose);

            var pnlGridHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                Padding = Padding.Empty
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                DataSource = _bindingList,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 44,
                Tag = "SkipSttColumn"
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            _grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            _grid.RowTemplate.Height = 72;
            _grid.SelectionChanged += (_, __) => UpdateButtonState();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colShotIndex",
                HeaderText = "STT",
                DataPropertyName = nameof(ProductAdImageShotPlan.Index),
                ReadOnly = true,
                FillWeight = 6,
                MinimumWidth = 48
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colShotType",
                HeaderText = "Loại shot",
                DataPropertyName = nameof(ProductAdImageShotPlan.ShotTypeDisplay),
                ReadOnly = true,
                FillWeight = 12,
                MinimumWidth = 110
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colShotPrompt",
                HeaderText = "Prompt",
                DataPropertyName = nameof(ProductAdImageShotPlan.Prompt),
                ReadOnly = false,
                FillWeight = 52,
                MinimumWidth = 280
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colShotFile",
                HeaderText = "Tên ảnh",
                DataPropertyName = nameof(ProductAdImageShotPlan.OutputFileName),
                ReadOnly = false,
                FillWeight = 18,
                MinimumWidth = 140
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colShotAspect",
                HeaderText = "TL",
                DataPropertyName = nameof(ProductAdImageShotPlan.AspectRatio),
                ReadOnly = true,
                FillWeight = 8,
                MinimumWidth = 72
            });

            WireContextMenu();

            _pnlEmpty = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28)
            };
            var lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(150, 156, 170),
                Font = new Font("Segoe UI", 12F),
                Text = "Chưa có prompt." + Environment.NewLine + Environment.NewLine
                       + "Bấm ▶ Lập prompt trên lưới chính."
            };
            _pnlEmpty.Controls.Add(lblEmpty);

            pnlGridHost.Controls.Add(_pnlEmpty);
            pnlGridHost.Controls.Add(_grid);

            Controls.Add(pnlGridHost);
            Controls.Add(pnlBottom);
            Controls.Add(_lblHint);
            CancelButton = btnClose;
        }

        private void WireContextMenu()
        {
            _cms = new ContextMenuStrip();
            var miCopyPrompt = new ToolStripMenuItem("Copy prompt");
            miCopyPrompt.Click += (_, __) => CopySelected();
            var miCopyFile = new ToolStripMenuItem("Copy tên file");
            miCopyFile.Click += (_, __) => CopySelectedFileNames();
            _cms.Items.Add(miCopyPrompt);
            _cms.Items.Add(miCopyFile);
            _cms.Opening += Cms_Opening;
            _grid.ContextMenuStrip = _cms;
        }

        private void Cms_Opening(object sender, CancelEventArgs e)
        {
            var has = GetSelectedShots().Count > 0;
            foreach (ToolStripItem item in _cms.Items)
            {
                if (item is ToolStripMenuItem)
                {
                    item.Enabled = has;
                }
            }

            if (!has)
            {
                e.Cancel = true;
            }
        }

        private void RefreshChrome()
        {
            if (_lblHint != null)
            {
                _lblHint.Text = BuildHint();
            }

            var empty = _bindingList.Count == 0;
            if (_pnlEmpty != null)
            {
                _pnlEmpty.Visible = empty;
                if (empty)
                {
                    _pnlEmpty.BringToFront();
                }
                else
                {
                    _grid?.BringToFront();
                }
            }

            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            var hasRows = _bindingList.Count > 0;
            var hasSelection = GetSelectedShots().Count > 0;
            if (_btnCopySelected != null)
            {
                _btnCopySelected.Enabled = hasSelection;
            }

            if (_btnCopyAll != null)
            {
                _btnCopyAll.Enabled = hasRows;
            }

            if (_btnExcel != null)
            {
                _btnExcel.Enabled = hasRows;
            }
        }

        private string BuildHint()
        {
            return _bindingList.Count + " prompt · sửa cột Prompt · copy hoặc xuất Excel";
        }

        private List<ProductAdImageShotPlan> GetSelectedShots()
        {
            var list = new List<ProductAdImageShotPlan>();
            if (_grid?.SelectedRows == null || _grid.SelectedRows.Count == 0)
            {
                if (_grid?.CurrentRow?.DataBoundItem is ProductAdImageShotPlan current)
                {
                    list.Add(current);
                }

                return list;
            }

            foreach (var row in _grid.SelectedRows.Cast<DataGridViewRow>()
                         .Where(r => r?.DataBoundItem is ProductAdImageShotPlan)
                         .OrderBy(r => r.Index))
            {
                list.Add((ProductAdImageShotPlan)row.DataBoundItem);
            }

            return list;
        }

        private void CommitShots()
        {
            try
            {
                _grid?.EndEdit();
            }
            catch
            {
                // ignored
            }

            _item.ReplaceGeneratedShots(_bindingList);
            if (_item.GeneratedShots.Count == 0)
            {
                if (!string.Equals(_item.Status, "Đang lập…", StringComparison.Ordinal))
                {
                    _item.Status = "Chờ";
                }
            }
            else if (string.Equals(_item.Status, "Chờ", StringComparison.Ordinal)
                     || string.Equals(_item.Status, "Lỗi", StringComparison.Ordinal))
            {
                _item.Status = "Xong";
            }
        }

        private void CopySelected()
        {
            CopyShots(GetSelectedShots(), "Chọn ít nhất một dòng để copy.");
        }

        private void CopyAll()
        {
            CopyShots(_bindingList.Where(s => s != null).ToList(), "Chưa có prompt để copy.");
        }

        private void CopyShots(IList<ProductAdImageShotPlan> shots, string emptyMessage)
        {
            var sb = new StringBuilder();
            foreach (var shot in shots)
            {
                if (shot == null || string.IsNullOrWhiteSpace(shot.Prompt))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.AppendLine(shot.Index + ". " + ProductAdImageShotPlan.FormatShotType(shot.ShotType)
                              + (string.IsNullOrWhiteSpace(shot.Title) ? string.Empty : " — " + shot.Title));
                sb.Append(shot.Prompt);
            }

            if (sb.Length == 0)
            {
                MessageBox.Show(this, emptyMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(sb.ToString());
        }

        private void CopySelectedFileNames()
        {
            var names = GetSelectedShots()
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.OutputFileName))
                .Select(s => s.OutputFileName.Trim())
                .ToList();
            if (names.Count == 0)
            {
                MessageBox.Show(this, "Dòng đang chọn chưa có tên file.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(string.Join(Environment.NewLine, names));
        }

        private void ExportExcel()
        {
            if (_bindingList.Count == 0)
            {
                MessageBox.Show(this, "Chưa có prompt để xuất Excel.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var folder = ProductAdImagePromptExcelHelper.GetDefaultFolder(_item.ProfileName);
            using (var dlg = new SaveFileDialog
            {
                Title = "Xuất Excel prompt",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = ProductAdImagePromptExcelHelper.BuildExcelFileName(_item.ProfileName, _item.ProductName),
                InitialDirectory = folder
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    ProductAdImagePromptExcelHelper.ExportShots(dlg.FileName, _bindingList.ToList());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Xuất Excel lỗi: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private static Button CreateButton(string text, Color back)
        {
            var font = new Font("Segoe UI", 11F, FontStyle.Bold);
            var textWidth = TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, 52),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            return new Button
            {
                Text = text,
                AutoSize = false,
                Width = Math.Max(148, textWidth + 40),
                Height = 52,
                MinimumSize = new Size(148, 52),
                FlatStyle = FlatStyle.Flat,
                Font = font,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true
            };
        }
    }
}
