using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font ProductAdImageUiFont = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        private static readonly Font ProductAdImageCommandFont = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        private static readonly Color ProductAdImagePanelBack = Color.FromArgb(31, 34, 42);
        private static readonly Color ProductAdImageChromeBack = Color.FromArgb(36, 39, 48);
        private static readonly Color ProductAdImageAccent = Color.FromArgb(210, 95, 130);
        private static readonly Color ProductAdImageTintPlan = Color.FromArgb(56, 142, 88);
        private static readonly Color ProductAdImageTintStop = Color.FromArgb(170, 72, 72);
        private static readonly Color ProductAdImageTintAddRow = Color.FromArgb(52, 92, 158);
        private static readonly Color ProductAdImageTintCopy = Color.FromArgb(88, 94, 112);
        private static readonly Color ProductAdImageTintDelete = Color.FromArgb(168, 52, 52);
        private static readonly Color ProductAdImageTintTrash = Color.FromArgb(88, 92, 72);
        private static readonly Color ProductAdImageGridCellBack = Color.FromArgb(20, 22, 28);
        private static readonly Color ProductAdImageGridSelectBack = Color.FromArgb(76, 110, 245);
        private static readonly Padding ProductAdImageSolidButtonMargin = new Padding(4, 2, 4, 2);
        private const int ProductAdImageCommandButtonHeight = AppJellyButtonHeight;
        private const int ProductAdImageCommandHorizontalPad = AppJellyButtonHorizontalPad;
        private const int ProductAdImageStatusPanelHeight = 315;
        private const int ProductAdImageStatusPanelMinHeight = 270;
        private const int ProductAdImageLogHeaderRowHeight = 64;
        private const int ProductAdImageClearLogButtonWidth = 120;
        private const int ProductAdImageReadinessHeight = 68;
        private const float ProductAdImageLogFontSize = 10F;
        private const int ProductAdImageLogLineSpacing = 4;

        private Panel pnlProductAdImageTopChrome;
        private Panel pnlProductAdImageTabTitleHost;
        private ShowcaseTabTitleLabel lblProductAdImageTabTitle;
        private Panel pnlProductAdImageCommandBar;
        private FlowLayoutPanel flpProductAdImagePlanCenter;
        private FlowLayoutPanel flpProductAdImageRowManage;
        private Panel pnlProductAdImageMainFill;
        private Panel pnlProductAdImageGridWrap;
        private Panel pnlProductAdImageStatus;
        private Label lblProductAdImageReadiness;
        private Button btnProductAdImagePlanPrompt;
        private Button btnProductAdImageStop;
        private Button btnProductAdImageAddRow;
        private Button btnProductAdImageCopyRow;
        private Button btnProductAdImageDeleteRow;
        private Button btnProductAdImageTrash;
        private Button btnProductAdImageStripWatermark;
        private Button btnProductAdImageClearLog;
        private RichTextBox rtbProductAdImageLog;
        private DataGridView dgvProductAdImage;
        private BindingList<ProductAdImageBatchItem> _productAdImageBindingList;
        private DataGridViewComboBoxColumn _colProductAdImageProfile;
        private readonly ToolTip _productAdImageTip = new ToolTip();

        public void InitializeProductAdImageControls(Panel modePage)
        {
            if (modePage == null)
            {
                return;
            }

            EnsureProductAdImageGridCreated();
            BuildProductAdImageTopChrome();
            BuildProductAdImageStatusPanel();
            BuildProductAdImageReadinessLabel();
            WireProductAdImageTabLayout(modePage);
            WireProductAdImageCommandButtons();
            InitializeProductAdImageLogFlush();
            InitializeProductAdImageDraftAutoSave();
            InitializeProductAdImageTrash();
            AttachProductAdImageBindingListEvents();
            LogProductAdImage("[Tạo ảnh AI] Sẵn sàng.");
            RefreshProductAdImageReadinessLabel();
        }

        private bool IsProductAdImageLayoutOk()
        {
            return pnlModeProductAdImage != null
                   && !pnlModeProductAdImage.IsDisposed
                   && pnlProductAdImageMainFill != null
                   && pnlProductAdImageMainFill.Parent == pnlModeProductAdImage
                   && pnlProductAdImageStatus != null
                   && pnlProductAdImageTopChrome != null
                   && pnlProductAdImageGridWrap != null
                   && dgvProductAdImage != null
                   && lblProductAdImageReadiness != null;
        }

        private void LayoutProductAdImageShell()
        {
            if (pnlModeProductAdImage == null || pnlModeProductAdImage.IsDisposed)
            {
                return;
            }

            if (IsProductAdImageLayoutOk())
            {
                ApplyProductAdImageReadinessLabelChrome();
                ApplyProductAdImageProfileComboColumn();
                LayoutProductAdImageCommandBar();
                RefreshProductAdImageReadinessLabel();
                return;
            }

            WireProductAdImageTabLayout(pnlModeProductAdImage);
            LayoutProductAdImageCommandBar();
            RefreshProductAdImageReadinessLabel();
        }

        private void WireProductAdImageTabLayout(Panel modePage)
        {
            if (modePage == null || IsProductAdImageLayoutOk())
            {
                return;
            }

            modePage.SuspendLayout();
            try
            {
                modePage.Padding = new Padding(4);
                modePage.AutoScroll = false;

                if (pnlProductAdImageGridWrap == null)
                {
                    pnlProductAdImageGridWrap = new Panel
                    {
                        Name = "pnlProductAdImageGridWrap",
                        Padding = new Padding(4),
                        BackColor = ProductAdImagePanelBack
                    };
                }

                if (dgvProductAdImage != null && dgvProductAdImage.Parent != pnlProductAdImageGridWrap)
                {
                    dgvProductAdImage.Dock = DockStyle.Fill;
                    dgvProductAdImage.Margin = Padding.Empty;
                    dgvProductAdImage.MinimumSize = new Size(120, 80);
                    pnlProductAdImageGridWrap.Controls.Add(dgvProductAdImage);
                }

                if (pnlProductAdImageMainFill == null)
                {
                    pnlProductAdImageMainFill = new Panel
                    {
                        Name = "pnlProductAdImageMainFill",
                        BackColor = ProductAdImagePanelBack,
                        Padding = new Padding(0, 2, 0, 0)
                    };
                }

                if (lblProductAdImageReadiness != null && !lblProductAdImageReadiness.IsDisposed)
                {
                    ApplyProductAdImageReadinessLabelChrome();
                }

                ApplyTopFillBottomDockLayout(
                    pnlProductAdImageMainFill,
                    pnlProductAdImageGridWrap,
                    bottom: null,
                    top: lblProductAdImageReadiness);

                if (pnlProductAdImageStatus != null)
                {
                    pnlProductAdImageStatus.Dock = DockStyle.Bottom;
                    pnlProductAdImageStatus.Height = ProductAdImageStatusPanelHeight;
                    pnlProductAdImageStatus.MinimumSize = new Size(0, ProductAdImageStatusPanelMinHeight);
                    pnlProductAdImageStatus.Margin = Padding.Empty;
                }

                if (pnlProductAdImageTopChrome != null)
                {
                    pnlProductAdImageTopChrome.Dock = DockStyle.Top;
                    pnlProductAdImageTopChrome.Margin = Padding.Empty;
                }

                pnlProductAdImageMainFill.Dock = DockStyle.Fill;
                pnlProductAdImageMainFill.Margin = Padding.Empty;

                if (pnlProductAdImageMainFill.Parent != modePage)
                {
                    modePage.Controls.Add(pnlProductAdImageMainFill);
                }

                if (pnlProductAdImageStatus != null && pnlProductAdImageStatus.Parent != modePage)
                {
                    modePage.Controls.Add(pnlProductAdImageStatus);
                }

                if (pnlProductAdImageTopChrome != null && pnlProductAdImageTopChrome.Parent != modePage)
                {
                    modePage.Controls.Add(pnlProductAdImageTopChrome);
                }
            }
            finally
            {
                modePage.ResumeLayout(false);
            }
        }

        private void EnsureProductAdImageGridCreated()
        {
            if (dgvProductAdImage != null && !dgvProductAdImage.IsDisposed)
            {
                ApplyProductAdImageProfileComboColumn();
                ApplyProductAdImageGridSelectionChrome();
                return;
            }

            _productAdImageBindingList = _productAdImageBindingList
                                        ?? new BindingList<ProductAdImageBatchItem>();
            AttachProductAdImageBindingListEvents();
            dgvProductAdImage = new DataGridView
            {
                Name = "dgvProductAdImage",
                Dock = DockStyle.Fill,
                DataSource = _productAdImageBindingList,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                EditMode = DataGridViewEditMode.EditOnEnter,
                BackgroundColor = ProductAdImageGridCellBack,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                GridColor = Color.FromArgb(72, 78, 94),
                EnableHeadersVisualStyles = false,
                Font = ProductAdImageUiFont,
                Tag = "SkipSttColumn"
            };
            ConfigureProductAdImageGridColumns();
            WireProductAdImageGridEvents();
            ApplyGridProfileComboColumn(dgvProductAdImage, "colProductAdImageProfile");
            ApplyProductAdImageProfileComboColumn();
            ApplyAppComboGridRowHeight(dgvProductAdImage);
            ApplyAppGridChrome(dgvProductAdImage);
            ApplyProductAdImageGridSelectionChrome();
        }

        private void ConfigureProductAdImageGridColumns()
        {
            dgvProductAdImage.Columns.Clear();

            dgvProductAdImage.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProductAdImageOrder",
                HeaderText = "STT",
                DataPropertyName = nameof(ProductAdImageBatchItem.Order),
                ReadOnly = true,
                FillWeight = 6,
                MinimumWidth = 48,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            _colProductAdImageProfile = new DataGridViewComboBoxColumn
            {
                Name = "colProductAdImageProfile",
                HeaderText = "Profile",
                DataPropertyName = nameof(ProductAdImageBatchItem.ProfileName),
                DisplayMember = nameof(ProfileComboEntry.Name),
                ValueMember = nameof(ProfileComboEntry.Name),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                DisplayStyleForCurrentCellOnly = false,
                FillWeight = 12,
                MinimumWidth = 96,
                ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dgvProductAdImage.Columns.Add(_colProductAdImageProfile);

            dgvProductAdImage.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProductAdImageName",
                HeaderText = "Tên SP",
                DataPropertyName = nameof(ProductAdImageBatchItem.ProductName),
                ReadOnly = false,
                FillWeight = 16,
                MinimumWidth = 120,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            AddProductAdImagePopupColumn(
                "colProductAdImagePromptSetup",
                "Thiết lập prompt",
                nameof(ProductAdImageBatchItem.PromptSetupSummary),
                16);
            AddProductAdImagePopupColumn(
                "colProductAdImageImages",
                "Ảnh mẫu",
                nameof(ProductAdImageBatchItem.ImagesSummary),
                14);
            AddProductAdImagePopupColumn(
                "colProductAdImagePrompts",
                "Prompt",
                nameof(ProductAdImageBatchItem.PromptGridLabel),
                12);

            dgvProductAdImage.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProductAdImageStatus",
                HeaderText = "Trạng thái",
                DataPropertyName = nameof(ProductAdImageBatchItem.Status),
                ReadOnly = true,
                FillWeight = 10,
                MinimumWidth = 88,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            dgvProductAdImage.Columns["colProductAdImageOrder"].ToolTipText = "Số thứ tự dòng.";
            dgvProductAdImage.Columns["colProductAdImageProfile"].ToolTipText = "Profile lưu ảnh mẫu và file Excel.";
            dgvProductAdImage.Columns["colProductAdImageName"].ToolTipText = "Tên sản phẩm — bắt buộc trước khi lập prompt.";
            dgvProductAdImage.Columns["colProductAdImagePromptSetup"].ToolTipText = "Bấm để mở loại SP, phong cách, khóa mẫu, số ảnh và tỉ lệ.";
            dgvProductAdImage.Columns["colProductAdImageImages"].ToolTipText = "➕ chọn ảnh mẫu · 📂 mở folder refs.";
            dgvProductAdImage.Columns["colProductAdImagePrompts"].ToolTipText = "Bấm để sửa prompt, copy hoặc xuất Excel.";
            dgvProductAdImage.Columns["colProductAdImageStatus"].ToolTipText = "Chờ / Đang lập… / Xong / Lỗi.";
        }

        private void AddProductAdImagePopupColumn(string name, string header, string dataProperty, int fillWeight)
        {
            dgvProductAdImage.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                DataPropertyName = dataProperty,
                ReadOnly = true,
                FillWeight = fillWeight,
                MinimumWidth = 96,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle =
                {
                    BackColor = ProductAdImageGridCellBack,
                    ForeColor = Color.FromArgb(186, 196, 214),
                    SelectionBackColor = ProductAdImageGridSelectBack,
                    SelectionForeColor = Color.White
                }
            });
        }

        private void WireProductAdImageGridEvents()
        {
            dgvProductAdImage.CellMouseClick -= DgvProductAdImage_CellMouseClick;
            dgvProductAdImage.CellMouseClick += DgvProductAdImage_CellMouseClick;
            dgvProductAdImage.CellPainting -= DgvProductAdImage_CellPainting;
            dgvProductAdImage.CellPainting += DgvProductAdImage_CellPainting;
            dgvProductAdImage.DataBindingComplete -= DgvProductAdImage_DataBindingComplete;
            dgvProductAdImage.DataBindingComplete += DgvProductAdImage_DataBindingComplete;
            dgvProductAdImage.RowPrePaint -= DgvProductAdImage_RowPrePaint;
            dgvProductAdImage.RowPrePaint += DgvProductAdImage_RowPrePaint;
            dgvProductAdImage.EditingControlShowing -= DgvProductAdImage_EditingControlShowing;
            dgvProductAdImage.EditingControlShowing += DgvProductAdImage_EditingControlShowing;
            dgvProductAdImage.DataError -= DgvProductAdImage_DataError;
            dgvProductAdImage.DataError += DgvProductAdImage_DataError;
            dgvProductAdImage.CurrentCellDirtyStateChanged -= DgvProductAdImage_CurrentCellDirtyStateChanged;
            dgvProductAdImage.CurrentCellDirtyStateChanged += DgvProductAdImage_CurrentCellDirtyStateChanged;
            dgvProductAdImage.CellBeginEdit -= DgvProductAdImage_CellBeginEdit;
            dgvProductAdImage.CellBeginEdit += DgvProductAdImage_CellBeginEdit;
            dgvProductAdImage.CellValueChanged -= DgvProductAdImage_CellValueChanged;
            dgvProductAdImage.CellValueChanged += DgvProductAdImage_CellValueChanged;
            WireProductAdImageGridContextMenu();
        }

        private void ApplyProductAdImageProfileComboColumn()
        {
            if (dgvProductAdImage == null || dgvProductAdImage.IsDisposed)
            {
                return;
            }

            if (_aiVideoGenProfileComboSource == null || _aiVideoGenProfileComboSource.Count == 0)
            {
                RefreshGridProfileComboSource(_productAdImageSettingsSnap);
            }

            if (_productAdImageBindingList != null)
            {
                foreach (var item in _productAdImageBindingList)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (!IsUsableProductAdImageProfileName(item.ProfileName))
                    {
                        item.ProfileName = GetDefaultProductAdImageProfileName();
                    }

                    EnsureProfileComboIncludes(item.ProfileName);
                }
            }

            ApplyGridProfileComboColumn(dgvProductAdImage, "colProductAdImageProfile");
        }

        private static bool IsUsableProductAdImageProfileName(string name)
        {
            var n = (name ?? string.Empty).Trim();
            if (n.Length == 0)
            {
                return false;
            }

            return n.IndexOf("ProfileComboEntry", StringComparison.OrdinalIgnoreCase) < 0
                   && n.IndexOf("tiktok_Omni.", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void ApplyProductAdImageGridSelectionChrome()
        {
            if (dgvProductAdImage == null || dgvProductAdImage.IsDisposed)
            {
                return;
            }

            ApplyProductAdImageCellStyle(dgvProductAdImage.DefaultCellStyle, Color.Gainsboro);
            ApplyProductAdImageCellStyle(dgvProductAdImage.RowsDefaultCellStyle, Color.Gainsboro);
            ApplyProductAdImageCellStyle(dgvProductAdImage.AlternatingRowsDefaultCellStyle, Color.Gainsboro);
            ApplyProductAdImageCellStyle(dgvProductAdImage.RowTemplate.DefaultCellStyle, Color.Gainsboro);

            dgvProductAdImage.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgvProductAdImage.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgvProductAdImage.GridColor = Color.FromArgb(72, 78, 94);

            dgvProductAdImage.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                dgvProductAdImage.ColumnHeadersDefaultCellStyle.BackColor;
            dgvProductAdImage.ColumnHeadersDefaultCellStyle.SelectionForeColor =
                dgvProductAdImage.ColumnHeadersDefaultCellStyle.ForeColor;

            foreach (DataGridViewColumn column in dgvProductAdImage.Columns)
            {
                if (column?.DefaultCellStyle == null)
                {
                    continue;
                }

                var fore = column.DefaultCellStyle.ForeColor.IsEmpty
                    ? Color.Gainsboro
                    : column.DefaultCellStyle.ForeColor;
                ApplyProductAdImageCellStyle(column.DefaultCellStyle, fore);
            }
        }

        private static void ApplyProductAdImageCellStyle(DataGridViewCellStyle style, Color foreColor)
        {
            if (style == null)
            {
                return;
            }

            style.BackColor = ProductAdImageGridCellBack;
            style.ForeColor = foreColor;
            style.SelectionBackColor = ProductAdImageGridSelectBack;
            style.SelectionForeColor = Color.White;
        }

        private void DgvProductAdImage_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (dgvProductAdImage == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!string.Equals(
                    dgvProductAdImage.Columns[e.ColumnIndex]?.Name,
                    "colProductAdImageImages",
                    StringComparison.Ordinal))
            {
                return;
            }

            var item = dgvProductAdImage.Rows[e.RowIndex].DataBoundItem as ProductAdImageBatchItem;
            e.Handled = true;
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Background
                | DataGridViewPaintParts.SelectionBackground
                | DataGridViewPaintParts.Border);

            GetShowcaseImagesCellLayout(e.CellBounds.Width, e.CellBounds.Height, out var addLocal, out var folderLocal);
            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            var addIcon = item != null && item.HasReferenceImages ? "✓" : "➕";

            var graphics = e.Graphics;
            graphics.SetClip(e.CellBounds);
            PaintShowcaseImagesActionButton(
                graphics,
                OffsetRect(e.CellBounds, addLocal),
                addIcon,
                Color.FromArgb(90, 235, 150),
                Color.FromArgb(32, 58, 46),
                Color.FromArgb(70, 130, 95),
                selected);
            PaintShowcaseImagesActionButton(
                graphics,
                OffsetRect(e.CellBounds, folderLocal),
                "📂",
                Color.FromArgb(255, 205, 90),
                Color.FromArgb(58, 50, 32),
                Color.FromArgb(140, 110, 55),
                selected);
            graphics.ResetClip();
        }

        private void DgvProductAdImage_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (dgvProductAdImage == null || e.RowIndex < 0 || e.RowIndex >= dgvProductAdImage.Rows.Count)
            {
                return;
            }

            var row = dgvProductAdImage.Rows[e.RowIndex];
            if (row == null)
            {
                return;
            }

            if (row.DefaultCellStyle.BackColor != ProductAdImageGridCellBack
                || row.DefaultCellStyle.SelectionBackColor != ProductAdImageGridSelectBack)
            {
                ApplyProductAdImageCellStyle(row.DefaultCellStyle, Color.Gainsboro);
            }

            foreach (DataGridViewCell cell in row.Cells)
            {
                if (!(cell is DataGridViewComboBoxCell))
                {
                    continue;
                }

                if (cell.Style.BackColor != ProductAdImageGridCellBack
                    || cell.Style.SelectionBackColor != ProductAdImageGridSelectBack)
                {
                    ApplyProductAdImageCellStyle(cell.Style, Color.Gainsboro);
                }
            }
        }

        private void DgvProductAdImage_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var combo = e.Control as ComboBox;
            if (combo == null)
            {
                return;
            }

            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = ProductAdImageGridSelectBack;
            combo.ForeColor = Color.White;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.IntegralHeight = false;

            if (dgvProductAdImage?.CurrentCell == null
                || !(dgvProductAdImage.Columns[dgvProductAdImage.CurrentCell.ColumnIndex] is DataGridViewComboBoxColumn col)
                || !string.Equals(col.Name, "colProductAdImageProfile", StringComparison.Ordinal))
            {
                return;
            }

            combo.FormattingEnabled = true;
            combo.DisplayMember = nameof(ProfileComboEntry.Name);
            combo.ValueMember = nameof(ProfileComboEntry.Name);
            var count = combo.Items.Count > 0
                ? combo.Items.Count
                : (_aiVideoGenProfileComboSource?.Count ?? 1);
            combo.MaxDropDownItems = Math.Max(8, Math.Min(20, Math.Max(1, count)));
            combo.DropDownHeight = Math.Min(
                520,
                Math.Max(160, (combo.ItemHeight <= 0 ? 28 : combo.ItemHeight) * Math.Max(count, 1) + 8));
        }

        private void DgvProductAdImage_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        private void DgvProductAdImage_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvProductAdImage == null || !dgvProductAdImage.IsCurrentCellDirty)
            {
                return;
            }

            if (dgvProductAdImage.CurrentCell is DataGridViewComboBoxCell)
            {
                dgvProductAdImage.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvProductAdImage_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            ApplyProductAdImageProfileComboColumn();
            ApplyProductAdImageGridSelectionChrome();
        }

        private void WireProductAdImageCommandButtons()
        {
            if (btnProductAdImageAddRow != null)
            {
                btnProductAdImageAddRow.Click -= BtnProductAdImageAddRow_Click;
                btnProductAdImageAddRow.Click += BtnProductAdImageAddRow_Click;
            }

            if (btnProductAdImageCopyRow != null)
            {
                btnProductAdImageCopyRow.Click -= BtnProductAdImageCopyRow_Click;
                btnProductAdImageCopyRow.Click += BtnProductAdImageCopyRow_Click;
            }

            if (btnProductAdImageDeleteRow != null)
            {
                btnProductAdImageDeleteRow.Click -= BtnProductAdImageDeleteRow_Click;
                btnProductAdImageDeleteRow.Click += BtnProductAdImageDeleteRow_Click;
            }

            if (btnProductAdImagePlanPrompt != null)
            {
                btnProductAdImagePlanPrompt.Click -= BtnProductAdImagePlanPrompt_Click;
                btnProductAdImagePlanPrompt.Click += BtnProductAdImagePlanPrompt_Click;
            }

            if (btnProductAdImageStop != null)
            {
                btnProductAdImageStop.Click -= BtnProductAdImageStop_Click;
                btnProductAdImageStop.Click += BtnProductAdImageStop_Click;
            }

            if (btnProductAdImageTrash != null)
            {
                btnProductAdImageTrash.Click -= BtnProductAdImageTrash_Click;
                btnProductAdImageTrash.Click += BtnProductAdImageTrash_Click;
            }

            if (btnProductAdImageStripWatermark != null)
            {
                btnProductAdImageStripWatermark.Click -= BtnProductAdImageStripWatermark_Click;
                btnProductAdImageStripWatermark.Click += BtnProductAdImageStripWatermark_Click;
            }
        }

        private void BuildProductAdImageReadinessLabel()
        {
            if (lblProductAdImageReadiness == null || lblProductAdImageReadiness.IsDisposed)
            {
                lblProductAdImageReadiness = new Label
                {
                    Name = "lblProductAdImageReadiness",
                    Text = "Đang kiểm tra Gemini API key…"
                };
            }

            ApplyProductAdImageReadinessLabelChrome();
        }

        private void ApplyProductAdImageReadinessLabelChrome()
        {
            if (lblProductAdImageReadiness == null || lblProductAdImageReadiness.IsDisposed)
            {
                return;
            }

            lblProductAdImageReadiness.AutoSize = false;
            lblProductAdImageReadiness.AutoEllipsis = true;
            lblProductAdImageReadiness.UseCompatibleTextRendering = true;
            lblProductAdImageReadiness.Dock = DockStyle.Top;
            lblProductAdImageReadiness.ForeColor = Color.FromArgb(165, 172, 188);
            lblProductAdImageReadiness.Padding = new Padding(8, 10, 8, 14);
            lblProductAdImageReadiness.Font = ProductAdImageUiFont;
            lblProductAdImageReadiness.BackColor = ProductAdImagePanelBack;
            lblProductAdImageReadiness.TextAlign = ContentAlignment.MiddleLeft;
            lblProductAdImageReadiness.MinimumSize = new Size(0, ProductAdImageReadinessHeight);
            lblProductAdImageReadiness.Height = ProductAdImageReadinessHeight;
        }

        private void BuildProductAdImageTopChrome()
        {
            if (pnlProductAdImageTopChrome != null && !pnlProductAdImageTopChrome.IsDisposed)
            {
                return;
            }

            BuildProductAdImageCommandBar();
            BuildProductAdImageTabTitleRow();

            pnlProductAdImageTopChrome = new Panel
            {
                Name = "pnlProductAdImageTopChrome",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = ProductAdImageChromeBack
            };

            pnlProductAdImageCommandBar.Dock = DockStyle.Top;
            pnlProductAdImageTabTitleHost.Dock = DockStyle.Top;
            pnlProductAdImageTopChrome.Controls.Add(pnlProductAdImageCommandBar);
            pnlProductAdImageTopChrome.Controls.Add(pnlProductAdImageTabTitleHost);
        }

        private void BuildProductAdImageTabTitleRow()
        {
            pnlProductAdImageTabTitleHost = new Panel
            {
                Name = "pnlProductAdImageTabTitleHost",
                AutoSize = false,
                Height = 64,
                Margin = new Padding(0, 0, 0, 8),
                Padding = Padding.Empty,
                BackColor = ProductAdImagePanelBack
            };

            var accent = new Panel
            {
                Name = "pnlProductAdImageTitleAccent",
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = ProductAdImageAccent
            };

            lblProductAdImageTabTitle = new ShowcaseTabTitleLabel
            {
                Name = "lblProductAdImageTabTitle",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                TitleText = "TẠO ẢNH AI"
            };

            pnlProductAdImageTabTitleHost.Controls.Add(accent);
            pnlProductAdImageTabTitleHost.Controls.Add(lblProductAdImageTabTitle);
        }

        private void BuildProductAdImageCommandBar()
        {
            pnlProductAdImageCommandBar = new Panel
            {
                Name = "pnlProductAdImageCommandBar",
                Dock = DockStyle.Top,
                AutoSize = false,
                MinimumSize = new Size(0, AppJellyButtonHeight * 2 + 18),
                Height = AppJellyButtonHeight * 2 + 18,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = ProductAdImageChromeBack
            };
            pnlProductAdImageCommandBar.Resize += (_, __) => LayoutProductAdImageCommandBar();

            flpProductAdImagePlanCenter = CreateProductAdImageToolbarFlowPanel("flpProductAdImagePlanCenter");
            flpProductAdImageRowManage = CreateProductAdImageToolbarFlowPanel("flpProductAdImageRowManage");

            btnProductAdImagePlanPrompt = CreateProductAdImageJellyButton(
                "btnProductAdImagePlanPrompt",
                "▶ Lập prompt",
                ProductAdImageTintPlan,
                180);
            btnProductAdImageStop = CreateProductAdImageJellyButton(
                "btnProductAdImageStop",
                "■ Dừng",
                ProductAdImageTintStop,
                120);
            btnProductAdImageStop.Visible = false;

            btnProductAdImageAddRow = CreateProductAdImageSolidRectButton(
                "btnProductAdImageAddRow", "+ Thêm dòng", ProductAdImageTintAddRow, 128);
            btnProductAdImageCopyRow = CreateProductAdImageSolidRectButton(
                "btnProductAdImageCopyRow", "📋 Copy dòng", ProductAdImageTintCopy, 140);
            btnProductAdImageDeleteRow = CreateProductAdImageSolidRectButton(
                "btnProductAdImageDeleteRow", "🗑 Xoá dòng", ProductAdImageTintDelete, 128);
            btnProductAdImageTrash = CreateProductAdImageSolidRectButton(
                "btnProductAdImageTrash", "♻ Thùng rác", ProductAdImageTintTrash, 128);
            btnProductAdImageStripWatermark = CreateProductAdImageJellyButton(
                "btnProductAdImageStripWatermark",
                "Xoá logo Gemini",
                Color.FromArgb(118, 72, 140),
                180);

            _productAdImageTip.SetToolTip(btnProductAdImagePlanPrompt, "Gemini lập prompt cho các dòng đang chọn (tuần tự).");
            _productAdImageTip.SetToolTip(btnProductAdImageStop, "Dừng lập prompt.");
            _productAdImageTip.SetToolTip(btnProductAdImageAddRow, "Thêm dòng batch mới.");
            _productAdImageTip.SetToolTip(btnProductAdImageCopyRow, "Clone dòng đã chọn và copy ảnh mẫu sang folder profile.");
            _productAdImageTip.SetToolTip(btnProductAdImageDeleteRow, "Chuyển dòng đã chọn vào thùng rác.");
            _productAdImageTip.SetToolTip(btnProductAdImageTrash, "Mở thùng rác — khôi phục hoặc xoá vĩnh viễn.");
            _productAdImageTip.SetToolTip(
                btnProductAdImageStripWatermark,
                "Xoá ngôi sao 4 cánh góc phải dưới trên ảnh đã tải từ Gemini. Lưu file mới, không ghi đè gốc.");

            flpProductAdImagePlanCenter.Controls.Add(btnProductAdImagePlanPrompt);
            flpProductAdImagePlanCenter.Controls.Add(btnProductAdImageStop);
            flpProductAdImagePlanCenter.Controls.Add(btnProductAdImageStripWatermark);

            flpProductAdImageRowManage.Controls.Add(btnProductAdImageAddRow);
            flpProductAdImageRowManage.Controls.Add(btnProductAdImageCopyRow);
            flpProductAdImageRowManage.Controls.Add(btnProductAdImageDeleteRow);
            flpProductAdImageRowManage.Controls.Add(btnProductAdImageTrash);

            pnlProductAdImageCommandBar.Controls.Add(flpProductAdImagePlanCenter);
            pnlProductAdImageCommandBar.Controls.Add(flpProductAdImageRowManage);
            LayoutProductAdImageCommandBar();
        }

        private void LayoutProductAdImageCommandBar()
        {
            if (pnlProductAdImageCommandBar == null
                || flpProductAdImagePlanCenter == null
                || flpProductAdImageRowManage == null)
            {
                return;
            }

            if (pnlProductAdImageCommandBar.Width <= 0 || pnlProductAdImageCommandBar.Height <= 0)
            {
                return;
            }

            var rowHeight = AppJellyButtonHeight + 6;
            flpProductAdImagePlanCenter.PerformLayout();
            var planX = Math.Max(0, (pnlProductAdImageCommandBar.ClientSize.Width - flpProductAdImagePlanCenter.Width) / 2);
            flpProductAdImagePlanCenter.Location = new Point(planX, 0);
            flpProductAdImagePlanCenter.BringToFront();

            flpProductAdImageRowManage.PerformLayout();
            flpProductAdImageRowManage.Location = new Point(0, rowHeight);
            flpProductAdImageRowManage.BringToFront();
        }

        private void BuildProductAdImageStatusPanel()
        {
            if (pnlProductAdImageStatus != null && !pnlProductAdImageStatus.IsDisposed)
            {
                return;
            }

            btnProductAdImageClearLog = new Button
            {
                Name = "btnProductAdImageClearLog",
                Text = "Xoá log",
                Size = new Size(ProductAdImageClearLogButtonWidth, 24),
                BackColor = Color.FromArgb(60, 64, 77),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.WhiteSmoke,
                Font = ProductAdImageUiFont
            };
            btnProductAdImageClearLog.FlatAppearance.BorderSize = 0;
            btnProductAdImageClearLog.Click += (_, __) => ClearProductAdImageLog();

            rtbProductAdImageLog = CreateAiModeLogTextBox("rtbProductAdImageLog");
            rtbProductAdImageLog.Font = new Font("Segoe UI", ProductAdImageLogFontSize, FontStyle.Regular, GraphicsUnit.Point);
            rtbProductAdImageLog.Dock = DockStyle.Fill;
            rtbProductAdImageLog.Margin = new Padding(0, 4, 0, 8);
            ApplyAiModeLogLineSpacing(rtbProductAdImageLog);

            pnlProductAdImageStatus = new Panel
            {
                Name = "pnlProductAdImageStatus",
                Dock = DockStyle.Bottom,
                Height = ProductAdImageStatusPanelHeight,
                MinimumSize = new Size(0, ProductAdImageStatusPanelMinHeight),
                BackColor = Color.FromArgb(24, 26, 32),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6)
            };

            var tblLog = new TableLayoutPanel
            {
                Name = "tblProductAdImageLog",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            tblLog.RowStyles.Add(new RowStyle(SizeType.Absolute, ProductAdImageLogHeaderRowHeight));
            tblLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 4, 8) };
            var lblLog = new Label
            {
                Text = "Nhật ký",
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(200, 204, 214),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ProductAdImageUiFont,
                UseCompatibleTextRendering = true,
                Padding = new Padding(0, 4, 0, 8)
            };
            btnProductAdImageClearLog.Dock = DockStyle.Right;
            btnProductAdImageClearLog.Width = ProductAdImageClearLogButtonWidth;
            btnProductAdImageClearLog.MinimumSize = new Size(ProductAdImageClearLogButtonWidth, 36);
            header.Controls.Add(lblLog);
            header.Controls.Add(btnProductAdImageClearLog);

            tblLog.Controls.Add(header, 0, 0);
            tblLog.Controls.Add(rtbProductAdImageLog, 0, 1);
            pnlProductAdImageStatus.Controls.Add(tblLog);
        }

        private static FlowLayoutPanel CreateProductAdImageToolbarFlowPanel(string name)
        {
            return new FlowLayoutPanel
            {
                Name = name,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.None,
                Padding = new Padding(4, 2, 4, 2),
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
        }

        private static Button CreateProductAdImageJellyButton(string name, string text, Color tint, int minWidth)
        {
            return CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: ProductAdImageCommandButtonHeight,
                minWidth: minWidth,
                horizontalPad: ProductAdImageCommandHorizontalPad,
                margin: ProductAdImageSolidButtonMargin,
                fontOverride: ProductAdImageCommandFont);
        }

        private static Button CreateProductAdImageSolidRectButton(string name, string text, Color back, int minWidth)
        {
            var height = ProductAdImageCommandButtonHeight;
            var textW = TextRenderer.MeasureText(
                text,
                ProductAdImageCommandFont,
                new Size(int.MaxValue, height),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            var width = Math.Max(minWidth, textW + 28);
            var border = ControlPaint.Dark(back);
            var btn = new ShowcaseSolidRectButton
            {
                Name = name,
                Text = text,
                Font = ProductAdImageCommandFont,
                NormalBackColor = back,
                ForeColor = Color.FromArgb(245, 247, 250),
                AutoSize = false,
                Height = height,
                Width = width,
                MinimumSize = new Size(width, height),
                MaximumSize = new Size(width, height),
                Margin = ProductAdImageSolidButtonMargin,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = border;
            return btn;
        }
    }
}
