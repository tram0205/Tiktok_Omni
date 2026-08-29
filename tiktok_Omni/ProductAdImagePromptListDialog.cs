using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class ProductAdImagePromptListDialog : Form
    {
        private static readonly Color PanelBack = Color.FromArgb(31, 34, 42);
        private static readonly Color GridBack = Color.FromArgb(28, 30, 38);
        private static readonly Color GridAltBack = Color.FromArgb(32, 35, 44);
        private static readonly Color TintGenerateAll = Color.FromArgb(210, 95, 130);
        private static readonly Color TintGenerateRow = Color.FromArgb(56, 142, 88);
        private static readonly Color TintViewImage = Color.FromArgb(90, 155, 210);
        private static readonly Color TintNeutral = Color.FromArgb(70, 78, 96);
        private static readonly Color HeaderBack = Color.FromArgb(45, 49, 60);
        private static readonly Color HeaderFore = Color.WhiteSmoke;
        private static readonly Font PromptGridHeaderFont = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        private const int CommandBarHeight = 78;
        private const int PromptGridHeaderHeight = 88;
        private const string RowGenerateLabel = "▶ Sinh ảnh";
        private const string RowViewLabel = "Xem";

        private readonly ProductAdImageBatchItem _item;
        private readonly Func<IReadOnlyList<ProductAdImageShotPlan>, Task<ProductAdImageBatchResult>> _generateAsync;
        private readonly BindingList<ProductAdImageShotPlan> _bindingList;
        private readonly DataGridView _grid;
        private readonly Label _lblHint;
        private readonly JellyButton _btnGenerateRow;
        private readonly JellyButton _btnGenerateAll;
        private readonly Button _btnClose;
        private bool _busy;
        private string _editingPromptOriginal;

        public ProductAdImagePromptListDialog(
            ProductAdImageBatchItem item,
            Func<IReadOnlyList<ProductAdImageShotPlan>, Task<ProductAdImageBatchResult>> generateAsync)
        {
            _item = item;
            _generateAsync = generateAsync;
            _bindingList = new BindingList<ProductAdImageShotPlan>(
                (item?.GeneratedShots ?? new List<ProductAdImageShotPlan>())
                .Where(s => s != null)
                .ToList());

            var product = (item?.ProductName ?? string.Empty).Trim();
            Text = string.IsNullOrEmpty(product) ? "Danh sách prompt" : "Prompt — «" + product + "»";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            MinimumSize = new Size(1200, 720);
            ClientSize = new Size(2160, 1280);
            BackColor = PanelBack;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);

            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 96,
                Padding = new Padding(24, 18, 24, 14),
                ForeColor = Color.FromArgb(190, 195, 210),
                UseCompatibleTextRendering = true,
                Text = BuildHintText()
            };

            _grid = BuildGrid();
            _grid.DataSource = _bindingList;
            _grid.DataBindingComplete += (_, __) => ApplyPromptGridHeaderChrome(_grid);
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellBeginEdit += Grid_CellBeginEdit;
            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.SelectionChanged += (_, __) => UpdateCommandButtons();

            FormClosing += (_, __) => _item?.ReplaceGeneratedShots(_bindingList);

            var actionBar = new FlowLayoutPanel
            {
                Height = CommandBarHeight,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 8),
                BackColor = PanelBack
            };

            _btnGenerateRow = CreatePromptDialogJellyButton(
                "btnPromptGenerateRow",
                RowGenerateLabel,
                TintGenerateRow,
                148);
            _btnGenerateRow.Click += async (_, __) => await RunGenerateSelectedRowAsync();

            _btnGenerateAll = CreatePromptDialogJellyButton(
                "btnPromptGenerateAll",
                "▶ Sinh tất cả",
                TintGenerateAll,
                168);
            _btnGenerateAll.Click += async (_, __) => await RunGenerateAllAsync();

            actionBar.Controls.Add(_btnGenerateRow);
            actionBar.Controls.Add(_btnGenerateAll);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 96,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 16, 28, 24),
                WrapContents = false,
                BackColor = PanelBack
            };

            _btnClose = CreatePromptDialogPlainButton("btnPromptClose", "Đóng", TintNeutral, 120);
            _btnClose.DialogResult = DialogResult.OK;

            footer.Controls.Add(_btnClose);

            var gridLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PanelBack,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, CommandBarHeight));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            actionBar.Dock = DockStyle.Fill;
            actionBar.Margin = Padding.Empty;
            _grid.Dock = DockStyle.Fill;
            _grid.Margin = Padding.Empty;

            gridLayout.Controls.Add(actionBar, 0, 0);
            gridLayout.Controls.Add(_grid, 0, 1);

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 8, 20, 12),
                BackColor = PanelBack
            };
            gridHost.Controls.Add(gridLayout);

            Controls.Add(gridHost);
            Controls.Add(footer);
            Controls.Add(_lblHint);
            AcceptButton = _btnClose;
            CancelButton = _btnClose;
            UpdateCommandButtons();

            Load += (_, __) =>
            {
                ApplyPromptGridHeaderChrome(_grid);
                ApplyPromptGridColumnWidths(_grid);
                LayoutHintLabel();
                AutoSizePromptRows();
            };
            Shown += (_, __) =>
            {
                ApplyPromptGridHeaderChrome(_grid);
                _grid?.Invalidate();
            };
            Resize += (_, __) =>
            {
                LayoutHintLabel();
                AutoSizePromptRows();
            };
            _grid.SizeChanged += (_, __) => AutoSizePromptRows();
        }

        private void LayoutHintLabel()
        {
            if (_lblHint == null || _lblHint.IsDisposed)
            {
                return;
            }

            var width = Math.Max(480, ClientSize.Width - 56);
            var size = TextRenderer.MeasureText(
                _lblHint.Text ?? string.Empty,
                _lblHint.Font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.Left);
            _lblHint.Height = Math.Max(96, size.Height + 36);
        }

        private string BuildHintText()
        {
            if (_bindingList.Count > 0)
            {
                var pending = CountPendingShots();
                return _bindingList.Count + " prompt · sửa trực tiếp cột «Prompt» · «▶ Sinh ảnh» / «▶ Sinh tất cả» phía trên lưới · cột «Xem ảnh» mở thành phẩm ("
                    + pending + " ảnh chưa xong) · Lập prompt trên lưới chính.";
            }

            return "Chưa có prompt — bấm «▶ Lập prompt» trên lưới chính trước, rồi quay lại sinh ảnh tại đây.";
        }

        private bool CanGenerateImages() =>
            _generateAsync != null && _bindingList.Count > 0 && (_item?.CanGenerate ?? false);

        private int CountPendingShots()
        {
            return _bindingList.Count(p => !IsShotReady(p));
        }

        private static bool IsShotReady(ProductAdImageShotPlan plan)
        {
            if (plan == null)
            {
                return false;
            }

            var path = (plan.OutputPath ?? string.Empty).Trim();
            return plan.Success && !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = GridBack,
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                Font = Form1.AppGridBodyFont,
                ScrollBars = ScrollBars.Both,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                DefaultCellStyle =
                {
                    BackColor = GridBack,
                    ForeColor = Color.Gainsboro,
                    SelectionBackColor = Color.FromArgb(210, 95, 130),
                    SelectionForeColor = Color.White,
                    WrapMode = DataGridViewTriState.True,
                    Padding = new Padding(8, 10, 8, 10)
                },
                AlternatingRowsDefaultCellStyle =
                {
                    BackColor = GridAltBack
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = HeaderBack,
                    ForeColor = HeaderFore,
                    Font = PromptGridHeaderFont,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(8, 10, 8, 10),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersHeight = PromptGridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowTemplate = { Height = 80, MinimumHeight = 80 }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPromptOrder",
                HeaderText = "STT",
                DataPropertyName = nameof(ProductAdImageShotPlan.Index),
                Width = 80,
                MinimumWidth = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPromptText",
                HeaderText = "Prompt",
                DataPropertyName = nameof(ProductAdImageShotPlan.Prompt),
                FillWeight = 62,
                MinimumWidth = 360,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = false,
                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(200, 220, 255),
                    BackColor = Color.FromArgb(34, 38, 50)
                }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPromptImageName",
                HeaderText = "Tên ảnh",
                DataPropertyName = nameof(ProductAdImageShotPlan.OutputFileName),
                FillWeight = 24,
                MinimumWidth = 280,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = Color.FromArgb(255, 210, 120) }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPromptStatus",
                HeaderText = "Trạng thái",
                DataPropertyName = nameof(ProductAdImageShotPlan.StatusGridLabel),
                Width = 108,
                MinimumWidth = 108,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPromptView",
                HeaderText = "Xem ảnh",
                Text = RowViewLabel,
                UseColumnTextForButtonValue = true,
                Width = 108,
                MinimumWidth = 96,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    BackColor = TintViewImage,
                    ForeColor = Color.White,
                    SelectionBackColor = Color.FromArgb(110, 175, 225),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Regular)
                }
            });

            ApplyPromptGridHeaderChrome(grid);
            ApplyPromptGridColumnWidths(grid);

            return grid;
        }

        private static void ApplyPromptGridHeaderChrome(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed || grid.Columns == null || grid.Columns.Count == 0)
            {
                return;
            }

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersVisible = true;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = PromptGridHeaderHeight;
            grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = HeaderFore;
            grid.ColumnHeadersDefaultCellStyle.Font = PromptGridHeaderFont;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 10, 8, 10);

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column == null)
                {
                    continue;
                }

                column.HeaderCell.Style.BackColor = HeaderBack;
                column.HeaderCell.Style.ForeColor = HeaderFore;
                column.HeaderCell.Style.Font = PromptGridHeaderFont;
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                column.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
                column.HeaderCell.Style.Padding = new Padding(8, 10, 8, 10);
            }

            grid.Invalidate();
        }

        private static void ApplyPromptGridColumnWidths(DataGridView grid)
        {
            if (grid?.Columns == null || grid.Columns.Count == 0)
            {
                return;
            }

            ApplyPromptGridHeaderColumnWidth(grid, "colPromptOrder", 80);
            ApplyPromptGridHeaderColumnWidth(grid, "colPromptStatus", 108);
            ApplyPromptViewColumnWidth(grid);
        }

        private static void ApplyPromptGridHeaderColumnWidth(DataGridView grid, string columnName, int fallbackWidth)
        {
            if (grid?.Columns == null || !grid.Columns.Contains(columnName))
            {
                return;
            }

            var column = grid.Columns[columnName];
            var headerText = (column.HeaderText ?? string.Empty).Trim();
            if (headerText.Length == 0)
            {
                headerText = " ";
            }

            var measured = TextRenderer.MeasureText(
                headerText,
                PromptGridHeaderFont,
                new Size(int.MaxValue, PromptGridHeaderHeight),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;

            column.MinimumWidth = Math.Max(fallbackWidth, measured + 24);
            column.Width = column.MinimumWidth;
        }

        private static JellyButton CreatePromptDialogJellyButton(string name, string text, Color tint, int minWidth)
        {
            return Form1.CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: Form1.AppJellyButtonHeight,
                minWidth: minWidth,
                margin: new Padding(0, 0, 8, 0));
        }

        private static Button CreatePromptDialogPlainButton(string name, string text, Color backColor, int width)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                Width = width,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                Margin = new Padding(0, 0, 8, 0),
                UseCompatibleTextRendering = true
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static void ApplyPromptViewColumnWidth(DataGridView grid)
        {
            if (grid?.Columns == null || !grid.Columns.Contains("colPromptView"))
            {
                return;
            }

            var column = grid.Columns["colPromptView"];
            var headerWidth = TextRenderer.MeasureText(
                "Xem ảnh",
                PromptGridHeaderFont,
                new Size(int.MaxValue, PromptGridHeaderHeight),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;
            var buttonWidth = TextRenderer.MeasureText(
                RowViewLabel,
                new Font("Segoe UI", 10.5F),
                new Size(int.MaxValue, 44),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;

            var measured = Math.Max(
                column.MinimumWidth,
                Math.Max(headerWidth, buttonWidth) + 24);

            column.MinimumWidth = measured;
            column.Width = measured;
        }

        private void UpdateCommandButtons()
        {
            var canGenerate = CanGenerateImages();
            _btnGenerateAll.Enabled = !_busy && canGenerate;
            _btnGenerateRow.Enabled = !_busy && canGenerate && GetSelectedShotPlan() != null;
        }

        private ProductAdImageShotPlan GetSelectedShotPlan()
        {
            return _grid?.CurrentRow?.DataBoundItem as ProductAdImageShotPlan;
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (_grid == null || e.ColumnIndex < 0)
            {
                return;
            }

            if (e.RowIndex == -1)
            {
                PaintPromptGridHeaderCell(_grid, e);
                return;
            }

            if (e.RowIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name != "colPromptView")
            {
                return;
            }

            PaintPromptViewCell(e);
        }

        private static void PaintPromptGridHeaderCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
        {
            var headerText = (e.FormattedValue ?? e.Value ?? string.Empty).ToString();
            if (string.IsNullOrWhiteSpace(headerText) && e.ColumnIndex >= 0 && grid?.Columns != null && e.ColumnIndex < grid.Columns.Count)
            {
                headerText = grid.Columns[e.ColumnIndex].HeaderText ?? string.Empty;
            }

            e.Handled = true;
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            var bounds = e.CellBounds;
            using (var backBrush = new SolidBrush(HeaderBack))
            {
                e.Graphics.FillRectangle(backBrush, bounds);
            }

            var textRect = Rectangle.Inflate(bounds, -10, -8);
            TextRenderer.DrawText(
                e.Graphics,
                headerText,
                PromptGridHeaderFont,
                textRect,
                HeaderFore,
                TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.SingleLine);

            using (var borderPen = new Pen(Color.FromArgb(58, 62, 74)))
            {
                e.Graphics.DrawLine(borderPen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
                e.Graphics.DrawLine(borderPen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }
        }

        private void PaintPromptViewCell(DataGridViewCellPaintingEventArgs e)
        {
            var plan = _grid.Rows[e.RowIndex].DataBoundItem as ProductAdImageShotPlan;
            var ready = IsShotReady(plan);
            var surface = e.RowIndex % 2 == 1 ? GridAltBack : GridBack;
            var fill = ready ? TintViewImage : surface;
            var text = ready ? "Xem" : "—";
            var textColor = ready ? Color.White : Color.FromArgb(120, 125, 140);

            e.Handled = true;
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            var bounds = e.CellBounds;
            using (var backBrush = new SolidBrush(fill))
            {
                e.Graphics.FillRectangle(backBrush, bounds);
            }

            var textRect = Rectangle.Inflate(bounds, -6, -4);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                _grid.Font,
                textRect,
                textColor,
                TextFormatFlags.HorizontalCenter
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.SingleLine);
        }

        private void Grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name != "colPromptText")
            {
                e.Cancel = true;
                return;
            }

            var plan = _grid.Rows[e.RowIndex].DataBoundItem as ProductAdImageShotPlan;
            _editingPromptOriginal = plan?.Prompt ?? string.Empty;
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name != "colPromptText")
            {
                return;
            }

            var plan = _grid.Rows[e.RowIndex].DataBoundItem as ProductAdImageShotPlan;
            if (plan == null)
            {
                _editingPromptOriginal = null;
                return;
            }

            var newPrompt = (plan.Prompt ?? string.Empty).Trim();
            var oldPrompt = (_editingPromptOriginal ?? string.Empty).Trim();
            _editingPromptOriginal = null;

            if (string.Equals(newPrompt, oldPrompt, StringComparison.Ordinal))
            {
                return;
            }

            plan.Prompt = newPrompt;
            if (plan.Success || !string.IsNullOrWhiteSpace(plan.ErrorMessage))
            {
                plan.Success = false;
                plan.ErrorMessage = string.Empty;
            }

            _item?.ReplaceGeneratedShots(_bindingList);
            _grid.InvalidateRow(e.RowIndex);
            AutoSizePromptRows();
            _lblHint.Text = BuildHintText();
            UpdateCommandButtons();
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "colPromptView")
            {
                OpenShotImageAtRow(e.RowIndex);
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name != "colPromptView")
            {
                return;
            }

            var plan = _grid.Rows[e.RowIndex].DataBoundItem as ProductAdImageShotPlan;
            var ready = IsShotReady(plan);
            var surface = e.RowIndex % 2 == 1 ? GridAltBack : GridBack;

            if (!ready)
            {
                e.CellStyle.ForeColor = Color.FromArgb(120, 125, 140);
                e.CellStyle.BackColor = surface;
                e.CellStyle.SelectionBackColor = Color.FromArgb(210, 95, 130);
                e.CellStyle.SelectionForeColor = Color.FromArgb(220, 220, 225);
                return;
            }

            e.CellStyle.ForeColor = Color.White;
            e.CellStyle.BackColor = TintViewImage;
            e.CellStyle.SelectionBackColor = Color.FromArgb(110, 175, 225);
            e.CellStyle.SelectionForeColor = Color.White;
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name != "colPromptView")
            {
                return;
            }

            OpenShotImageAtRow(e.RowIndex);
        }

        private void OpenShotImageAtRow(int rowIndex)
        {
            if (_grid == null || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var plan = _grid.Rows[rowIndex].DataBoundItem as ProductAdImageShotPlan;
            if (!IsShotReady(plan))
            {
                MessageBox.Show(this, "Dòng này chưa có ảnh — bấm «▶ Sinh ảnh» phía trên lưới.", "Xem ảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TryOpenImage(plan.OutputPath);
        }

        private static void TryOpenImage(string path)
        {
            var file = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                MessageBox.Show("Không tìm thấy file ảnh.", "Xem ảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không mở được ảnh: " + ex.Message, "Xem ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task RunGenerateSelectedRowAsync()
        {
            var plan = GetSelectedShotPlan();
            if (plan == null)
            {
                MessageBox.Show(this, "Chọn một dòng prompt trước.", "Sinh ảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await RunGenerateAsync(new[] { plan }).ConfigureAwait(true);
        }

        private async Task RunGenerateAllAsync()
        {
            var pending = _bindingList.Where(p => p != null && !IsShotReady(p)).ToList();
            if (pending.Count == 0)
            {
                MessageBox.Show(this, "Tất cả prompt đã có ảnh.", "Sinh ảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await RunGenerateAsync(pending).ConfigureAwait(true);
        }

        private async Task RunGenerateAsync(IReadOnlyList<ProductAdImageShotPlan> shots)
        {
            if (_generateAsync == null || shots == null || shots.Count == 0)
            {
                return;
            }

            SetBusy(true);
            try
            {
                var result = await _generateAsync(shots).ConfigureAwait(true);
                _item?.ReplaceGeneratedShots(_bindingList);
                RefreshGrid();
                _lblHint.Text = BuildHintText();

                if (result?.StoppedByQuota == true)
                {
                    MessageBox.Show(this,
                        result.SuccessCount > 0
                            ? "Hết quota — đã lưu " + result.SuccessCount + "/" + shots.Count + " ảnh trong lần này."
                            : "Hết quota model sinh ảnh — chưa lưu được ảnh nào.",
                        "Sinh ảnh",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Sinh ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UpdateCommandButtons();
            _btnClose.Enabled = !_busy;
            _grid.Enabled = !_busy;
            UseWaitCursor = _busy;
        }

        private void RefreshGrid()
        {
            _grid?.Invalidate();
            UpdateCommandButtons();
            AutoSizePromptRows();
        }

        private void AutoSizePromptRows()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            const int minRow = 80;
            const int maxRow = 260;
            var promptCol = _grid.Columns["colPromptText"]?.Index ?? 1;

            _grid.SuspendLayout();
            try
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.IsNewRow)
                    {
                        continue;
                    }

                    var text = row.Cells[promptCol].Value?.ToString() ?? string.Empty;
                    var width = Math.Max(120, _grid.GetCellDisplayRectangle(promptCol, row.Index, true).Width - 16);
                    var size = TextRenderer.MeasureText(
                        text,
                        _grid.Font,
                        new Size(width, int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.Left);

                    row.Height = Math.Max(minRow, Math.Min(maxRow, size.Height + 32));
                }
            }
            finally
            {
                _grid.ResumeLayout(true);
                ApplyPromptGridHeaderChrome(_grid);
                _grid.Invalidate(_grid.DisplayRectangle, true);
            }
        }
    }
}
