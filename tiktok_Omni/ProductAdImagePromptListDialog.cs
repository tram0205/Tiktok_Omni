using System;
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

        public ProductAdImagePromptListDialog(ProductAdImageBatchItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _bindingList = new BindingList<ProductAdImageShotPlan>(
                (_item.GeneratedShots ?? new System.Collections.Generic.List<ProductAdImageShotPlan>())
                    .Where(s => s != null)
                    .ToList());

            var product = string.IsNullOrWhiteSpace(_item.ProductName) ? "SP" : _item.ProductName.Trim();
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
            FormClosing += (_, __) => CommitShots();
        }

        private void BuildUi()
        {
            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(180, 186, 198),
                Text = BuildHint()
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            var btnCopy = CreateButton("📋 Copy tất cả", Color.FromArgb(88, 94, 112));
            btnCopy.Click += (_, __) => CopyAll();
            var btnExcel = CreateButton("📊 Xuất Excel", Color.FromArgb(52, 92, 158));
            btnExcel.Click += (_, __) => ExportExcel();
            var btnClose = CreateButton("Đóng", Color.FromArgb(68, 72, 86));
            btnClose.Click += (_, __) =>
            {
                CommitShots();
                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(btnCopy);
            buttons.Controls.Add(btnExcel);
            buttons.Controls.Add(btnClose);

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
                Tag = "SkipSttColumn"
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(31, 34, 42);
            _grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.RowTemplate.Height = 64;

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
                MinimumWidth = 100
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

            Controls.Add(_grid);
            Controls.Add(buttons);
            Controls.Add(_lblHint);
        }

        private string BuildHint()
        {
            return _bindingList.Count + " prompt · sửa trực tiếp cột Prompt · copy hoặc xuất Excel · sinh ảnh ngoài app.";
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
            if (_item.GeneratedShots.Count > 0 && string.Equals(_item.Status, "Chờ", StringComparison.Ordinal))
            {
                _item.Status = "Xong";
            }
        }

        private void CopyAll()
        {
            var sb = new StringBuilder();
            foreach (var shot in _bindingList)
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
                MessageBox.Show(this, "Chưa có prompt để copy.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(sb.ToString());
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
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(140, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 10, 0)
            };
        }
    }
}
