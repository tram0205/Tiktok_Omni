using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using RowValidationResult = tiktok_Omni.Services.PhilosophyBackgroundRowValidator.RowValidationResult;

namespace tiktok_Omni
{
    internal sealed class PhilosophyBackgroundEditorForm : Form
    {
        private const double UiScale = 1.3;

        private static int Sc(int value) => (int)Math.Round(value * UiScale);

        private static readonly int GridRowHeight = Sc(36);
        private static readonly int GridHeaderHeight = Sc(57);
        private static readonly int BrollThumbMinRowHeight = Sc(148);
        private static readonly int QuoteColumnMinWidth = Sc(240);
        private static readonly int PromptColumnMinWidth = Sc(200);
        private static readonly Color ActiveCellBack = Color.FromArgb(38, 42, 52);
        private static readonly Color HighlightHeaderBack = Color.FromArgb(56, 72, 98);
        private static readonly Color HighlightHeaderFore = Color.FromArgb(220, 228, 240);
        private static readonly string[] ModeScopedColumnNames =
        {
            "colBgBroll",
            "colBgZoomImages",
            "colBgPrompt",
            "colBgRefImage"
        };
        private bool _applyingGridColumnWidths;

        private readonly PhilosophyBatchItem _batch;
        private readonly string _profileName;
        private DataGridView _grid;
        private Label _lblModeHint;
        private readonly Dictionary<int, Image> _brollThumbsByRow = new Dictionary<int, Image>();
        private readonly Dictionary<int, Image> _zoomThumbsByRow = new Dictionary<int, Image>();
        private readonly Dictionary<int, Image> _previewThumbsByRow = new Dictionary<int, Image>();
        private readonly Dictionary<int, bool> _previewOwnsImageByRow = new Dictionary<int, bool>();
        private readonly Dictionary<int, string> _previewPlaceholdersByRow = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _previewTooltipsByRow = new Dictionary<int, string>();
        private readonly HashSet<int> _generatingPreviewRows = new HashSet<int>();
        private readonly ConfigManager _configManager = new ConfigManager();
        private readonly PhilosophyVideoPipelineService _previewPipeline = new PhilosophyVideoPipelineService();
        private readonly VideoProcessingService _videoProcessingService = new VideoProcessingService();
        private readonly Dictionary<int, RowValidationResult> _statusByRow = new Dictionary<int, RowValidationResult>();
        private AppSettings _cachedSettings;
        private bool _isBatchPreviewRunning;
        private bool _isGeminiSuggestRunning;
        private string _previewSessionBase = string.Empty;
        private Image _refImageThumb;

        public PhilosophyBackgroundEditorForm(PhilosophyBatchItem batch, string profileName)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
            _profileName = profileName ?? string.Empty;
            Text = "Nền — " + PhilosophyBatchHelper.TrimGridLabel(_batch.Topic, 40, "batch");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(Sc(1840), Sc(1040));
            MinimumSize = new Size(Sc(920), Sc(520));
            Padding = new Padding(20);
            BuildUi();
            Shown += PhilosophyBackgroundEditorForm_Shown;
        }

        private async void PhilosophyBackgroundEditorForm_Shown(object sender, EventArgs e)
        {
            try
            {
                _cachedSettings = await _configManager.LoadAsync().ConfigureAwait(true);
                RefreshAllRowStatuses(_cachedSettings);
            }
            catch
            {
                RefreshAllRowStatuses(null);
            }
        }

        private AppSettings TryLoadSettingsSync()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            try
            {
                _cachedSettings = _configManager.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                return _cachedSettings;
            }
            catch
            {
                return null;
            }
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(CreateHeaderPanel(), 0, 0);
            root.Controls.Add(CreateGrid(), 0, 1);

            Controls.Add(root);
            Controls.Add(CreateFooterBar());
        }

        private Control CreateHeaderPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = BackColor,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblModeHint = new Label
            {
                Text = BuildDefaultIntroText(),
                AutoSize = true,
                MaximumSize = new Size(Sc(1700), 0),
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 0, 0, Sc(10))
            };
            panel.Controls.Add(_lblModeHint, 0, 0);
            panel.Controls.Add(CreateBatchActionToolbar(), 0, 1);
            panel.Controls.Add(CreateLibraryToolbar(), 0, 2);
            return panel;
        }

        private Control CreateBatchActionToolbar()
        {
            var bar = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 0, Sc(8)),
                Margin = Padding.Empty,
                Dock = DockStyle.Top
            };

            bar.Controls.Add(CreateLibraryButton(
                "⎘ Copy loại nền → tất cả",
                Color.FromArgb(68, 88, 118),
                (_, __) => CopyBackgroundModeToAllRows()));
            bar.Controls.Add(CreateLibraryButton(
                "⎘ Copy tài nguyên → cùng loại",
                Color.FromArgb(72, 92, 108),
                (_, __) => CopyBackgroundResourcesToMatchingRows()));
            bar.Controls.Add(CreateLibraryButton(
                "✨ Gemini gợi ý nền",
                Color.FromArgb(92, 72, 128),
                (_, __) => _ = RunGeminiBackgroundSuggestionsAsync(selectedRowOnly: false)));
            bar.Controls.Add(CreateLibraryButton(
                "✨ Gemini (dòng đang chọn)",
                Color.FromArgb(82, 68, 118),
                (_, __) => _ = RunGeminiBackgroundSuggestionsAsync(selectedRowOnly: true)));
            bar.Controls.Add(CreateLibraryButton(
                "▶ Preview tất cả dòng",
                Color.FromArgb(56, 108, 88),
                (_, __) => _ = PreviewAllRowsAsync()));
            return bar;
        }

        private Control CreateLibraryToolbar()
        {
            var bar = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 0, Sc(12)),
                Margin = Padding.Empty,
                Dock = DockStyle.Top
            };

            bar.Controls.Add(CreateLibraryButton(
                "📁 Thư viện B-roll",
                Color.FromArgb(52, 92, 118),
                (_, __) => OpenAssetLibraryFolder(
                    PhilosophyProfileAssets.EnsureBrollLibraryDirectory(),
                    "Thư viện B-roll")));
            bar.Controls.Add(CreateLibraryButton(
                "🖼 Thư viện ảnh mascot",
                Color.FromArgb(72, 88, 118),
                (_, __) => OpenAssetLibraryFolder(
                    PhilosophyProfileAssets.EnsureMascotImageLibraryDirectory(_profileName),
                    "Thư viện ảnh mascot")));
            bar.Controls.Add(CreateLibraryButton(
                "🔍 Thư viện ảnh zoom",
                Color.FromArgb(68, 98, 88),
                (_, __) => OpenAssetLibraryFolder(
                    PhilosophyProfileAssets.EnsureZoomImageLibraryDirectory(_profileName),
                    "Thư viện ảnh zoom")));
            return bar;
        }

        private Button CreateLibraryButton(string text, Color backColor, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(Sc(160), Sc(36)),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, Sc(10), Sc(6)),
                Padding = new Padding(Sc(10), Sc(4), Sc(10), Sc(4)),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void OpenAssetLibraryFolder(string folder, string title)
        {
            try
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Không mở được thư mục:\r\n" + ex.Message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private Control CreateFooterBar()
        {
            var btnOk = CreateFooterButton("Lưu", Color.FromArgb(56, 120, 82), new Padding(10, 0, 0, 0));
            btnOk.DialogResult = DialogResult.OK;

            var btnCancel = CreateFooterButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            var flpButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 10, 0, 8),
                Margin = Padding.Empty,
                Dock = DockStyle.Fill
            };
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnCancel);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 0, 8),
                Margin = Padding.Empty
            };
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 1F));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            footer.Controls.Add(new Panel
            {
                Dock = DockStyle.Fill,
                Height = 1,
                BackColor = Color.FromArgb(55, 60, 72),
                Margin = Padding.Empty
            }, 0, 0);
            footer.Controls.Add(flpButtons, 0, 1);
            return footer;
        }

        private static Button CreateFooterButton(string text, Color back, Padding? margin = null)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(Sc(120), Sc(44)),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F),
                Margin = margin ?? Padding.Empty,
                Padding = new Padding(12, 6, 12, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static string BuildDefaultIntroText() =>
            "Cột B-roll / Zoom / Prompt / Ref chỉ hiện khi khớp «Loại nền» dòng đang chọn. Preview = video bước 1 (chưa thoại).";

        private DataGridView CreateGrid()
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AllowUserToResizeRows = true,
                ColumnHeadersHeight = GridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(60, 64, 77),
                MinimumSize = new Size(0, Sc(320)),
                DefaultCellStyle =
                {
                    BackColor = Color.FromArgb(38, 42, 52),
                    ForeColor = Color.Gainsboro,
                    SelectionBackColor = Color.FromArgb(76, 110, 245),
                    SelectionForeColor = Color.White,
                    Padding = new Padding(4, 4, 4, 4),
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = Padding.Empty,
                    WrapMode = DataGridViewTriState.True
                }
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgQuote",
                HeaderText = "Quote",
                ReadOnly = false,
                MinimumWidth = QuoteColumnMinWidth,
                ToolTipText = "Sửa quote tại đây — đồng bộ sang popup Nội dung khi Lưu",
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            });
            var modeCol = new DataGridViewComboBoxColumn
            {
                Name = "colBgMode",
                HeaderText = "Loại nền",
                MinimumWidth = Sc(72),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                HeaderCell =
                {
                    Style =
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        Padding = Padding.Empty
                    }
                }
            };
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeBroll);
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeAi);
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeZoom);
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeSlideshow);
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeAiStillZoom);
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeZoomBrollHybrid);
            _grid.Columns.Add(modeCol);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgStatus",
                HeaderText = "TT",
                ReadOnly = true,
                MinimumWidth = Sc(36),
                ToolTipText = "Trạng thái: ✓ đủ tài nguyên · ! thiếu — hover xem chi tiết",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold)
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgBroll",
                HeaderText = "B-roll",
                ReadOnly = true,
                MinimumWidth = MeasureBrollColumnWidth(),
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    Padding = new Padding(2)
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgZoomImages",
                HeaderText = "Zoom ảnh",
                ReadOnly = true,
                MinimumWidth = MeasureBrollColumnWidth(),
                ToolTipText = "Ảnh Ken Burns zoom — bấm để chọn nhiều ảnh",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    Padding = new Padding(2)
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgPrompt",
                HeaderText = "Prompt Veo (AI)",
                MinimumWidth = PromptColumnMinWidth,
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgRefImage",
                HeaderText = "Ảnh ref",
                ReadOnly = true,
                MinimumWidth = MeasureRefImageColumnWidth(),
                ToolTipText = "Ảnh tham chiếu batch (AI I2V) — bấm để chọn hoặc xóa",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    Padding = new Padding(2)
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgPreview",
                HeaderText = "Preview nền",
                ReadOnly = true,
                MinimumWidth = MeasureBrollColumnWidth(),
                ToolTipText = "Bấm để tạo hoặc xem video nền (chưa ghép âm thanh)",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    Padding = new Padding(2)
                }
            });

            AppGridSttColumn.EnsureFirstColumn(_grid, width: Sc((int)(AppGridSttColumn.CompactColumnWidth * 1.2)));
            _grid.DataError += Grid_DataError;
            PopulateRows();
            ApplyGridColumnWidths();
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
            _grid.SelectionChanged += (_, __) =>
            {
                UpdateColumnHeaderHighlights();
                UpdateModeHintLabel(GetCurrentRowIndex());
            };
            _grid.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.SizeChanged += (_, __) =>
            {
                ApplyGridColumnWidths();
                ResizeGridRows();
            };
            _grid.CellValueChanged += (_, e) =>
            {
                ApplyRowEdits();
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    var colName = _grid.Columns[e.ColumnIndex].Name;
                    if (colName == "colBgMode")
                    {
                        _grid.InvalidateRow(e.RowIndex);
                        UpdateColumnHeaderHighlights();
                        UpdateModeHintLabel(e.RowIndex);
                        InvalidateBackgroundPreviewForRow(e.RowIndex, _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem);
                        QueueBackgroundPreviewLoadForRow(e.RowIndex, _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem);
                        RefreshStatusForRow(e.RowIndex);
                    }

                    if (colName == "colBgQuote")
                    {
                        SyncQuoteContentFromGrid(e.RowIndex);
                    }

                    if (colName == "colBgPrompt"
                        || colName == "colBgMode")
                    {
                        ResizeGridRows();
                    }
                }
            };

            UpdateColumnHeaderHighlights();
            UpdateModeHintLabel(GetCurrentRowIndex());

            return _grid;
        }

        private string ResolvePreviewSessionBase()
        {
            if (!string.IsNullOrWhiteSpace(_previewSessionBase))
            {
                return _previewSessionBase;
            }

            try
            {
                var settings = TryLoadSettingsSync();
                _previewSessionBase = PhilosophyBackgroundPreviewHelper.GetSessionBase(_batch, settings);
            }
            catch
            {
                _previewSessionBase = PhilosophyBackgroundPreviewHelper.GetSessionBase(_batch, null);
            }

            return _previewSessionBase;
        }

        private void InvalidateBackgroundPreviewForRow(int rowIndex, PhilosophyScriptItem quote)
        {
            if (rowIndex < 0)
            {
                return;
            }

            var sessionBase = ResolvePreviewSessionBase();
            PhilosophyBackgroundPreviewHelper.InvalidatePreview(sessionBase, rowIndex);
            ClearPreviewThumbForRow(rowIndex);
            if (_grid?.Columns.Contains("colBgPreview") == true)
            {
                _grid.InvalidateCell(_grid.Columns["colBgPreview"].Index, rowIndex);
            }
        }

        private int GetCurrentRowIndex()
        {
            if (_grid?.CurrentCell == null)
            {
                return _grid != null && _grid.Rows.Count > 0 ? 0 : -1;
            }

            return _grid.CurrentCell.RowIndex;
        }

        private int GetRowVisualMode(int rowIndex)
        {
            if (_grid == null || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return PhilosophyVisualModes.Broll;
            }

            if (_grid.Rows[rowIndex].Tag is PhilosophyScriptItem quote)
            {
                return PhilosophyVisualModes.Normalize(quote.VisualMode);
            }

            var modeLabel = (_grid.Rows[rowIndex].Cells["colBgMode"].Value ?? string.Empty).ToString();
            return PhilosophyVisualModes.Normalize(
                PhilosophyBatchHelper.FromSimpleBackgroundModeLabel(modeLabel));
        }

        private static bool IsColumnActiveForMode(string columnName, int visualMode)
        {
            switch (columnName)
            {
                case "colBgBroll":
                    return visualMode == PhilosophyVisualModes.Broll
                           || visualMode == PhilosophyVisualModes.ZoomBrollHybrid;
                case "colBgZoomImages":
                    return PhilosophyVisualModes.UsesZoomImageLibrary(visualMode);
                case "colBgPrompt":
                    return visualMode == PhilosophyVisualModes.VeoScenery
                           || visualMode == PhilosophyVisualModes.VeoMascot
                           || visualMode == PhilosophyVisualModes.AiStillZoom;
                case "colBgRefImage":
                    return visualMode == PhilosophyVisualModes.VeoMascot
                           || visualMode == PhilosophyVisualModes.AiStillZoom;
                case "colBgPreview":
                    return true;
                default:
                    return true;
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;
            if (string.Equals(colName, "colBgStatus", StringComparison.Ordinal))
            {
                if (_statusByRow.TryGetValue(e.RowIndex, out var status))
                {
                    e.Value = status.Summary;
                    e.CellStyle.ForeColor = status.IsValid
                        ? Color.FromArgb(120, 210, 140)
                        : Color.FromArgb(240, 150, 110);
                }
                else
                {
                    e.Value = "?";
                }

                e.FormattingApplied = true;
                return;
            }

            if (!string.Equals(colName, "colBgPrompt", StringComparison.Ordinal))
            {
                return;
            }

            e.CellStyle.BackColor = ActiveCellBack;
            e.CellStyle.ForeColor = Color.Gainsboro;
            e.CellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            e.CellStyle.SelectionForeColor = Color.White;
        }

        private void UpdateColumnHeaderHighlights()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            var mode = GetRowVisualMode(GetCurrentRowIndex());
            var visibilityChanged = false;
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (col == null)
                {
                    continue;
                }

                if (IsModeScopedColumn(col.Name))
                {
                    var shouldShow = IsColumnActiveForMode(col.Name, mode);
                    if (col.Visible != shouldShow)
                    {
                        col.Visible = shouldShow;
                        visibilityChanged = true;
                    }

                    col.HeaderCell.Style.BackColor = HighlightHeaderBack;
                    col.HeaderCell.Style.ForeColor = HighlightHeaderFore;
                    continue;
                }

                if (string.Equals(col.Name, AppGridSttColumn.ColumnName, StringComparison.Ordinal)
                    || string.Equals(col.Name, "colBgQuote", StringComparison.Ordinal)
                    || string.Equals(col.Name, "colBgMode", StringComparison.Ordinal)
                    || string.Equals(col.Name, "colBgStatus", StringComparison.Ordinal)
                    || string.Equals(col.Name, "colBgPreview", StringComparison.Ordinal))
                {
                    if (!col.Visible)
                    {
                        col.Visible = true;
                        visibilityChanged = true;
                    }

                    col.HeaderCell.Style.BackColor = Color.FromArgb(45, 49, 60);
                    col.HeaderCell.Style.ForeColor = Color.WhiteSmoke;
                }
            }

            if (visibilityChanged)
            {
                ApplyGridColumnWidths();
            }

            _grid.Invalidate();
        }

        private static bool IsModeScopedColumn(string columnName)
        {
            foreach (var name in ModeScopedColumnNames)
            {
                if (string.Equals(name, columnName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateModeHintLabel(int rowIndex)
        {
            if (_lblModeHint == null || _lblModeHint.IsDisposed)
            {
                return;
            }

            if (rowIndex < 0)
            {
                _lblModeHint.Text = BuildDefaultIntroText();
                return;
            }

            var modeLabel = (_grid?.Rows[rowIndex].Cells["colBgMode"].Value ?? string.Empty).ToString().Trim();
            if (string.IsNullOrEmpty(modeLabel))
            {
                modeLabel = PhilosophyBatchHelper.BackgroundModeBroll;
            }

            _lblModeHint.Text = "Dòng " + (rowIndex + 1) + " · «" + modeLabel + "» — " + BuildModeActionHint(GetRowVisualMode(rowIndex));
        }

        private static string BuildModeActionHint(int visualMode)
        {
            switch (PhilosophyVisualModes.Normalize(visualMode))
            {
                case PhilosophyVisualModes.Broll:
                    return "bấm cột «B-roll» để chọn video nền.";
                case PhilosophyVisualModes.ImageZoom:
                    return "chọn 1–4 ảnh ở cột «Zoom ảnh» (Ken Burns).";
                case PhilosophyVisualModes.ImageSlideshow:
                    return "chọn 2+ ảnh ở cột «Zoom ảnh» (crossfade, không zoom).";
                case PhilosophyVisualModes.AiStillZoom:
                    return "điền «Prompt Veo» + có «Ảnh ref» batch hoặc mascot profile.";
                case PhilosophyVisualModes.ZoomBrollHybrid:
                    return "chọn ảnh «Zoom ảnh» (phần chính) + video «B-roll» (outro cuối).";
                default:
                    return "điền «Prompt Veo»; «Ảnh ref» nếu AI có nhân vật (I2V).";
            }
        }

        private static int MeasureRefImageColumnWidth() => MeasureBrollColumnWidth();

        private void QueueReferenceImageLoad()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            var path = (_batch.ReferenceImagePath ?? string.Empty).Trim();
            _refImageThumb?.Dispose();
            _refImageThumb = null;

            if (string.IsNullOrEmpty(path))
            {
                RefreshReferenceImageColumn();
                ResizeGridRows();
                return;
            }

            Task.Run(() =>
            {
                Image thumb = null;
                try
                {
                    if (File.Exists(path))
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            thumb = Image.FromStream(stream);
                        }
                    }
                }
                catch
                {
                    thumb?.Dispose();
                    thumb = null;
                }

                if (_grid.IsDisposed)
                {
                    thumb?.Dispose();
                    return;
                }

                try
                {
                    _grid.BeginInvoke(new Action(() =>
                    {
                        if (_grid.IsDisposed)
                        {
                            thumb?.Dispose();
                            return;
                        }

                        _refImageThumb?.Dispose();
                        _refImageThumb = thumb;
                        RefreshReferenceImageColumn();
                        ResizeGridRows();
                    }));
                }
                catch
                {
                    thumb?.Dispose();
                }
            });
        }

        private void RefreshReferenceImageColumn()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            var col = _grid.Columns["colBgRefImage"];
            if (col == null)
            {
                return;
            }

            _grid.InvalidateColumn(col.Index);
            ApplyGridColumnWidths();
        }

        private void BrowseReferenceImage()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Title = "Chọn ảnh tham chiếu cho batch"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _batch.ReferenceImagePath = dlg.FileName;
                QueueReferenceImageLoad();
                for (var i = 0; i < _grid.Rows.Count; i++)
                {
                    InvalidateBackgroundPreviewForRow(i, _grid.Rows[i].Tag as PhilosophyScriptItem);
                    QueueBackgroundPreviewLoadForRow(i, _grid.Rows[i].Tag as PhilosophyScriptItem);
                }
            }
        }

        private void ShowReferenceImageMenu()
        {
            var hasImage = !string.IsNullOrWhiteSpace(_batch.ReferenceImagePath);
            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };
            menu.Items.Add("Chọn ảnh…", null, (_, __) => BrowseReferenceImage());
            var clearItem = menu.Items.Add("Xóa ảnh", null, (_, __) =>
            {
                _batch.ReferenceImagePath = string.Empty;
                QueueReferenceImageLoad();
                for (var i = 0; i < _grid.Rows.Count; i++)
                {
                    InvalidateBackgroundPreviewForRow(i, _grid.Rows[i].Tag as PhilosophyScriptItem);
                    QueueBackgroundPreviewLoadForRow(i, _grid.Rows[i].Tag as PhilosophyScriptItem);
                }
            });
            clearItem.Enabled = hasImage;
            menu.Show(Cursor.Position);
        }

        private void ResizeGridRows()
        {
            if (_grid == null || _grid.IsDisposed || _grid.Rows.Count == 0)
            {
                return;
            }

            _grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Height < GridRowHeight)
                {
                    _grid.Rows[i].Height = GridRowHeight;
                }

                var quote = _grid.Rows[i].Tag as PhilosophyScriptItem;
                var mode = PhilosophyBatchHelper.ToSimpleBackgroundModeLabel(quote?.VisualMode ?? PhilosophyVisualModes.Broll);
                var hasZoomImages = quote?.ZoomImagePaths != null && quote.ZoomImagePaths.Count > 0;
                var needsThumbRow = _refImageThumb != null
                    || hasZoomImages
                    || _previewThumbsByRow.ContainsKey(i)
                    || (string.Equals(mode, PhilosophyBatchHelper.BackgroundModeBroll, StringComparison.OrdinalIgnoreCase)
                        && _brollThumbsByRow.ContainsKey(i));
                if (needsThumbRow && _grid.Rows[i].Height < BrollThumbMinRowHeight)
                {
                    _grid.Rows[i].Height = BrollThumbMinRowHeight;
                }
            }
        }

        private void PopulateRows()
        {
            _grid.Rows.Clear();
            var quotes = _batch.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>();
            foreach (var quote in quotes)
            {
                if (quote == null)
                {
                    continue;
                }

                var mode = PhilosophyBatchHelper.ToSimpleBackgroundModeLabel(quote.VisualMode);
                var prompt = PhilosophyBatchHelper.BuildAiVeoPrompt(quote, _profileName, _batch.Topic);
                if (string.IsNullOrWhiteSpace(quote.MotionPrompt))
                {
                    quote.MotionPrompt = prompt;
                }

                if (quote.ZoomImagePaths == null)
                {
                    quote.ZoomImagePaths = new List<string>();
                }

                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["colBgQuote"].Value = quote.Content ?? string.Empty;
                row.Cells["colBgMode"].Value = mode;
                row.Cells["colBgPrompt"].Value = quote.MotionPrompt ?? prompt;
                row.Tag = quote;
            }

            RefreshReferenceImageColumn();
            ResizeGridRows();
            QueueBrollThumbnailLoads();
            QueueZoomThumbnailLoads();
            QueueReferenceImageLoad();
            QueueBackgroundPreviewLoads();
            UpdateColumnHeaderHighlights();
            UpdateModeHintLabel(GetCurrentRowIndex());
        }

        private void QueueZoomThumbnailLoads()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                QueueZoomThumbnailLoadsForRow(i, _grid.Rows[i].Tag as PhilosophyScriptItem);
            }
        }

        private void QueueZoomThumbnailLoadsForRow(int rowIndex, PhilosophyScriptItem quote)
        {
            if (_grid == null || _grid.IsDisposed || quote == null)
            {
                return;
            }

            if (_zoomThumbsByRow.TryGetValue(rowIndex, out var oldThumb))
            {
                oldThumb?.Dispose();
                _zoomThumbsByRow.Remove(rowIndex);
            }

            var firstPath = quote.ZoomImagePaths?
                .FirstOrDefault(p => PhilosophyBRollSelection.IsImageFile(p));
            if (string.IsNullOrWhiteSpace(firstPath))
            {
                _grid.InvalidateCell(_grid.Columns["colBgZoomImages"].Index, rowIndex);
                return;
            }

            var path = firstPath.Trim();
            Task.Run(() =>
            {
                Image thumb = null;
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        thumb = Image.FromStream(stream);
                    }
                }
                catch
                {
                    thumb?.Dispose();
                    thumb = null;
                }

                if (thumb == null || _grid.IsDisposed)
                {
                    thumb?.Dispose();
                    return;
                }

                try
                {
                    _grid.BeginInvoke(new Action(() =>
                    {
                        if (_grid.IsDisposed || rowIndex >= _grid.Rows.Count)
                        {
                            thumb.Dispose();
                            return;
                        }

                        _zoomThumbsByRow[rowIndex] = thumb;
                        _grid.InvalidateCell(_grid.Columns["colBgZoomImages"].Index, rowIndex);
                    }));
                }
                catch
                {
                    thumb.Dispose();
                }
            });
        }

        private void QueueBackgroundPreviewLoads()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                QueueBackgroundPreviewLoadForRow(i, _grid.Rows[i].Tag as PhilosophyScriptItem);
            }
        }

        private void ClearPreviewThumbForRow(int rowIndex)
        {
            if (_previewThumbsByRow.TryGetValue(rowIndex, out var oldThumb))
            {
                if (_previewOwnsImageByRow.TryGetValue(rowIndex, out var owns) && owns)
                {
                    oldThumb?.Dispose();
                }

                _previewThumbsByRow.Remove(rowIndex);
            }

            _previewOwnsImageByRow.Remove(rowIndex);
            _previewPlaceholdersByRow.Remove(rowIndex);
            _previewTooltipsByRow.Remove(rowIndex);
        }

        private void QueueBackgroundPreviewLoadForRow(int rowIndex, PhilosophyScriptItem quote)
        {
            if (_grid == null || _grid.IsDisposed || quote == null)
            {
                return;
            }

            ClearPreviewThumbForRow(rowIndex);
            if (_grid.Columns.Contains("colBgPreview"))
            {
                _grid.InvalidateCell(_grid.Columns["colBgPreview"].Index, rowIndex);
            }

            if (_generatingPreviewRows.Contains(rowIndex))
            {
                _previewPlaceholdersByRow[rowIndex] = "Đang tạo…";
                return;
            }

            var sessionBase = ResolvePreviewSessionBase();
            var stamp = PhilosophyBackgroundPreviewHelper.ComputePreviewStamp(quote, _batch, _profileName);
            var videoPath = PhilosophyBackgroundPreviewHelper.GetPreviewVideoPath(sessionBase, rowIndex);
            var isCurrent = PhilosophyBackgroundPreviewHelper.IsPreviewCurrent(sessionBase, rowIndex, stamp);
            var visualMode = PhilosophyVisualModes.Normalize(quote.VisualMode);

            _previewPlaceholdersByRow[rowIndex] = isCurrent ? "▶ Xem" : "▶ Tạo preview";
            _previewTooltipsByRow[rowIndex] = PhilosophyBackgroundPreviewHelper.BuildPreviewCellTooltip(
                false,
                isCurrent,
                isCurrent ? videoPath : string.Empty,
                visualMode);

            if (!isCurrent)
            {
                return;
            }

            Task.Run(() =>
            {
                var thumb = PhilosophyBrollThumbnailHelper.TryGetThumbnail(videoPath, _profileName);
                if (_grid.IsDisposed)
                {
                    return;
                }

                try
                {
                    _grid.BeginInvoke(new Action(() =>
                    {
                        if (_grid.IsDisposed || rowIndex >= _grid.Rows.Count)
                        {
                            return;
                        }

                        if (thumb != null)
                        {
                            _previewThumbsByRow[rowIndex] = thumb;
                            _previewOwnsImageByRow[rowIndex] = false;
                        }

                        if (_grid.Columns.Contains("colBgPreview"))
                        {
                            _grid.InvalidateCell(_grid.Columns["colBgPreview"].Index, rowIndex);
                        }

                        ResizeGridRows();
                    }));
                }
                catch
                {
                    // ignored
                }
            });
        }

        private async Task GenerateBackgroundPreviewAsync(int rowIndex, PhilosophyScriptItem quote)
        {
            if (quote == null || _grid == null || _grid.IsDisposed)
            {
                return;
            }

            if (_generatingPreviewRows.Contains(rowIndex))
            {
                return;
            }

            AppSettings settings;
            try
            {
                settings = await _configManager.LoadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không đọc được Cài đặt:\r\n" + ex.Message, "Preview nền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!PhilosophyBackgroundPreviewHelper.TryValidateBackgroundPreviewRequest(
                    quote,
                    _batch,
                    _profileName,
                    settings,
                    out var validateErr))
            {
                MessageBox.Show(this, validateErr, "Preview nền", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _generatingPreviewRows.Add(rowIndex);
            _previewPlaceholdersByRow[rowIndex] = "Đang tạo…";
            _grid.InvalidateCell(_grid.Columns["colBgPreview"].Index, rowIndex);

            var sessionBase = PhilosophyBackgroundPreviewHelper.GetSessionBase(_batch, settings);
            _previewSessionBase = sessionBase;
            var videoPath = PhilosophyBackgroundPreviewHelper.GetPreviewVideoPath(sessionBase, rowIndex);
            var stamp = PhilosophyBackgroundPreviewHelper.ComputePreviewStamp(quote, _batch, _profileName);
            var renderOptions = PhilosophyBackgroundPreviewHelper.BuildRenderOptions(_batch, quote, _profileName, settings);
            var profile = PhilosophyBackgroundPreviewHelper.ResolveAutomationProfile(settings, _profileName);

            try
            {
                var (ok, err) = await _previewPipeline.GenerateBackgroundVideoAsync(
                    quote,
                    renderOptions,
                    settings,
                    profile,
                    videoPath,
                    _ => { },
                    CancellationToken.None).ConfigureAwait(true);

                if (!ok)
                {
                    MessageBox.Show(this, err, "Preview nền thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                PhilosophyBackgroundPreviewHelper.WritePreviewStamp(sessionBase, rowIndex, stamp);
                QueueBackgroundPreviewLoadForRow(rowIndex, quote);
                PhilosophyBackgroundPreviewHelper.TryOpenPreviewVideo(videoPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Preview nền", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _generatingPreviewRows.Remove(rowIndex);
                if (!_grid.IsDisposed && rowIndex < _grid.Rows.Count)
                {
                    _grid.InvalidateCell(_grid.Columns["colBgPreview"].Index, rowIndex);
                }
            }
        }

        private void HandlePreviewCellClick(int rowIndex, PhilosophyScriptItem quote)
        {
            if (quote == null || _generatingPreviewRows.Contains(rowIndex))
            {
                return;
            }

            var sessionBase = ResolvePreviewSessionBase();
            var stamp = PhilosophyBackgroundPreviewHelper.ComputePreviewStamp(quote, _batch, _profileName);
            var videoPath = PhilosophyBackgroundPreviewHelper.GetPreviewVideoPath(sessionBase, rowIndex);
            if (PhilosophyBackgroundPreviewHelper.IsPreviewCurrent(sessionBase, rowIndex, stamp))
            {
                PhilosophyBackgroundPreviewHelper.TryOpenPreviewVideo(videoPath);
                return;
            }

            _ = GenerateBackgroundPreviewAsync(rowIndex, quote);
        }

        private void QueueBrollThumbnailLoads()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                var rowIndex = i;
                var quote = _grid.Rows[rowIndex].Tag as PhilosophyScriptItem;
                if (quote == null)
                {
                    continue;
                }

                var selection = quote.BRollFolder ?? string.Empty;
                Task.Run(() =>
                {
                    var thumb = PhilosophyBrollThumbnailHelper.TryGetThumbnail(selection, _profileName);
                    if (thumb == null || _grid.IsDisposed)
                    {
                        return;
                    }

                    try
                    {
                        _grid.BeginInvoke(new Action(() =>
                        {
                            if (_grid.IsDisposed || rowIndex >= _grid.Rows.Count)
                            {
                                return;
                            }

                            _brollThumbsByRow[rowIndex] = thumb;
                            var col = _grid.Columns["colBgBroll"];
                            if (col != null)
                            {
                                _grid.InvalidateCell(col.Index, rowIndex);
                            }
                        }));
                    }
                    catch
                    {
                        // form closed
                    }
                });
            }
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "colBgMode")
            {
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = PhilosophyBatchHelper.BackgroundModeBroll;
                _grid.InvalidateRow(e.RowIndex);
                UpdateColumnHeaderHighlights();
                UpdateModeHintLabel(e.RowIndex);
            }
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName == "colBgPreview")
            {
                PaintBackgroundPreviewCell(e);
                return;
            }

            if (colName == "colBgRefImage")
            {
                PaintReferenceImageCell(e);
                return;
            }

            if (colName == "colBgZoomImages")
            {
                PaintZoomImagesCell(e);
                return;
            }

            if (colName != "colBgBroll")
            {
                return;
            }

            var quote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
            e.Handled = true;
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Background
                    | DataGridViewPaintParts.SelectionBackground
                    | DataGridViewPaintParts.Border);

            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            if (_brollThumbsByRow.TryGetValue(e.RowIndex, out var thumb) && thumb != null)
            {
                PhilosophyBrollThumbnailHelper.DrawThumbnail(e.Graphics, e.CellBounds, thumb, selected);
            }
            else
            {
                PhilosophyBrollThumbnailHelper.DrawPlaceholder(
                    e.Graphics,
                    e.CellBounds,
                    quote?.BRollFolder,
                    selected);
            }
        }

        private void PaintBackgroundPreviewCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Handled = true;
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Background
                    | DataGridViewPaintParts.SelectionBackground
                    | DataGridViewPaintParts.Border);

            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            if (_previewThumbsByRow.TryGetValue(e.RowIndex, out var thumb) && thumb != null)
            {
                PhilosophyBrollThumbnailHelper.DrawThumbnail(e.Graphics, e.CellBounds, thumb, selected);
                DrawPreviewPlayBadge(e.Graphics, e.CellBounds, selected);
                return;
            }

            var placeholder = _generatingPreviewRows.Contains(e.RowIndex)
                ? "Đang tạo…"
                : _previewPlaceholdersByRow.TryGetValue(e.RowIndex, out var label) && !string.IsNullOrWhiteSpace(label)
                    ? label
                    : "▶ Tạo preview";
            PhilosophyBrollThumbnailHelper.DrawPlaceholder(
                e.Graphics,
                e.CellBounds,
                placeholder,
                selected);
        }

        private static void DrawPreviewPlayBadge(Graphics graphics, Rectangle bounds, bool selected)
        {
            if (graphics == null)
            {
                return;
            }

            using (var font = new Font("Segoe UI", 9F, FontStyle.Bold))
            {
                var text = "▶";
                var size = TextRenderer.MeasureText(text, font);
                var pad = 4;
                var rect = new Rectangle(
                    bounds.X + bounds.Width - size.Width - pad * 2 - 4,
                    bounds.Y + bounds.Height - size.Height - pad * 2 - 4,
                    size.Width + pad * 2,
                    size.Height + pad);
                using (var brush = new SolidBrush(selected ? Color.FromArgb(220, 76, 110, 245) : Color.FromArgb(210, 24, 28, 36)))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    graphics.FillRectangle(brush, rect);
                    graphics.DrawString(text, font, textBrush, rect.X + pad, rect.Y + pad / 2);
                }
            }
        }

        private void PaintZoomImagesCell(DataGridViewCellPaintingEventArgs e)
        {
            var quote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
            e.Handled = true;
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Background
                    | DataGridViewPaintParts.SelectionBackground
                    | DataGridViewPaintParts.Border);

            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            if (_zoomThumbsByRow.TryGetValue(e.RowIndex, out var thumb) && thumb != null)
            {
                PhilosophyBrollThumbnailHelper.DrawThumbnail(e.Graphics, e.CellBounds, thumb, selected);
                var count = quote?.ZoomImagePaths?.Count ?? 0;
                if (count > 1)
                {
                    DrawZoomCountBadge(e.Graphics, e.CellBounds, count, selected);
                }
            }
            else
            {
                PhilosophyBrollThumbnailHelper.DrawPlaceholder(
                    e.Graphics,
                    e.CellBounds,
                    quote?.ZoomImagePaths?.Count > 0 ? "Thiếu preview" : string.Empty,
                    selected);
            }
        }

        private static void DrawZoomCountBadge(Graphics graphics, Rectangle bounds, int count, bool selected)
        {
            if (graphics == null || count <= 1)
            {
                return;
            }

            var badgeText = "+" + (count - 1);
            using (var font = new Font("Segoe UI", 8F, FontStyle.Bold))
            {
                var size = TextRenderer.MeasureText(badgeText, font);
                var pad = 4;
                var rect = new Rectangle(
                    bounds.Right - size.Width - pad * 2 - 2,
                    bounds.Bottom - size.Height - pad * 2 - 2,
                    size.Width + pad * 2,
                    size.Height + pad);
                using (var brush = new SolidBrush(selected ? Color.FromArgb(220, 76, 110, 245) : Color.FromArgb(220, 28, 32, 40)))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    graphics.FillRectangle(brush, rect);
                    graphics.DrawString(badgeText, font, textBrush, rect.X + pad, rect.Y + pad / 2);
                }
            }
        }

        private void PaintReferenceImageCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Handled = true;
            e.Paint(
                e.CellBounds,
                DataGridViewPaintParts.Background
                    | DataGridViewPaintParts.SelectionBackground
                    | DataGridViewPaintParts.Border);

            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            if (_refImageThumb != null)
            {
                PhilosophyBrollThumbnailHelper.DrawThumbnail(e.Graphics, e.CellBounds, _refImageThumb, selected);
            }
            else
            {
                var path = (_batch.ReferenceImagePath ?? string.Empty).Trim();
                var placeholder = string.IsNullOrEmpty(path)
                    ? string.Empty
                    : File.Exists(path)
                        ? path
                        : "Thiếu file";
                PhilosophyBrollThumbnailHelper.DrawPlaceholder(
                    e.Graphics,
                    e.CellBounds,
                    placeholder,
                    selected);
            }
        }

        private void Grid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;
            var visualMode = GetRowVisualMode(e.RowIndex);
            if (string.Equals(colName, "colBgStatus", StringComparison.Ordinal))
            {
                if (_statusByRow.TryGetValue(e.RowIndex, out var status))
                {
                    e.ToolTipText = status.Detail;
                }

                return;
            }

            if (string.Equals(colName, "colBgPreview", StringComparison.Ordinal))
            {
                var previewQuote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
                var sessionBase = ResolvePreviewSessionBase();
                var stamp = PhilosophyBackgroundPreviewHelper.ComputePreviewStamp(previewQuote, _batch, _profileName);
                var videoPath = PhilosophyBackgroundPreviewHelper.GetPreviewVideoPath(sessionBase, e.RowIndex);
                var isCurrent = previewQuote != null
                                && PhilosophyBackgroundPreviewHelper.IsPreviewCurrent(sessionBase, e.RowIndex, stamp);
                e.ToolTipText = PhilosophyBackgroundPreviewHelper.BuildPreviewCellTooltip(
                    _generatingPreviewRows.Contains(e.RowIndex),
                    isCurrent,
                    isCurrent ? videoPath : string.Empty,
                    GetRowVisualMode(e.RowIndex))
                    + "\r\nChuột phải: menu Tạo lại / Xem / Mở cache";
                return;
            }

            if (colName == "colBgRefImage")
            {
                var path = (_batch.ReferenceImagePath ?? string.Empty).Trim();
                e.ToolTipText = string.IsNullOrEmpty(path)
                    ? "Bấm để chọn ảnh ref batch (AI I2V)"
                    : File.Exists(path)
                        ? path
                        : "Thiếu file: " + path;
                return;
            }

            var quote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
            if (colName == "colBgZoomImages")
            {
                var paths = quote?.ZoomImagePaths ?? new List<string>();
                e.ToolTipText = paths.Count == 0
                    ? "Bấm để chọn ảnh zoom (có thể chọn nhiều)"
                    : string.Join("\r\n", paths);
                return;
            }

            if (colName != "colBgBroll")
            {
                return;
            }

            e.ToolTipText = PhilosophyBrollThumbnailHelper.BuildTooltip(quote?.BRollFolder);
        }

        private void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName == "colBgPreview")
            {
                if (e.Button == MouseButtons.Right)
                {
                    ShowPreviewMenu(e.RowIndex, _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem);
                }
                else
                {
                    HandlePreviewCellClick(e.RowIndex, _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem);
                }

                return;
            }

            if (colName == "colBgRefImage")
            {
                ShowReferenceImageMenu();
                return;
            }

            var quote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
            if (quote == null)
            {
                return;
            }

            if (colName == "colBgBroll")
            {
                ShowBRollMenu(quote, e.RowIndex);
                return;
            }

            if (colName == "colBgZoomImages")
            {
                ShowZoomImagesMenu(quote, e.RowIndex);
            }
        }

        private void ShowZoomImagesMenu(PhilosophyScriptItem quote, int rowIndex)
        {
            if (quote == null)
            {
                return;
            }

            var hasImages = quote.ZoomImagePaths != null && quote.ZoomImagePaths.Count > 0;
            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };
            menu.Items.Add("Chọn ảnh…", null, (_, __) => PickZoomImages(quote, rowIndex, append: false));
            menu.Items.Add("Thêm ảnh…", null, (_, __) => PickZoomImages(quote, rowIndex, append: true));
            menu.Items.Add("Thư viện trong app…", null, (_, __) => PickZoomImagesFromLibrary(quote, rowIndex, append: false));
            menu.Items.Add("Thêm từ thư viện…", null, (_, __) => PickZoomImagesFromLibrary(quote, rowIndex, append: true));
            var clearItem = menu.Items.Add("Xóa hết", null, (_, __) =>
            {
                quote.ZoomImagePaths = new List<string>();
                InvalidateBackgroundPreviewForRow(rowIndex, quote);
                QueueZoomThumbnailLoadsForRow(rowIndex, quote);
                QueueBackgroundPreviewLoadForRow(rowIndex, quote);
                ResizeGridRows();
            });
            clearItem.Enabled = hasImages;
            menu.Items.Add("Mở thư viện zoom", null, (_, __) =>
            {
                var dir = PhilosophyProfileAssets.EnsureZoomImageLibraryDirectory(_profileName);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
            });
            menu.Show(Cursor.Position);
        }

        private void PickZoomImages(PhilosophyScriptItem quote, int rowIndex, bool append)
        {
            if (quote == null)
            {
                return;
            }

            using (var dlg = new OpenFileDialog
            {
                Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Title = append ? "Thêm ảnh zoom" : "Chọn ảnh zoom",
                Multiselect = true,
                InitialDirectory = PhilosophyBRollSelection.GetZoomImageBrowseInitialDirectory(_profileName)
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var picked = dlg.FileNames
                    .Where(PhilosophyBRollSelection.IsImageFile)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (picked.Count == 0)
                {
                    return;
                }

                ApplyZoomImageSelection(quote, rowIndex, picked, append);
            }
        }

        private void PickBRoll(PhilosophyScriptItem quote, int rowIndex)
        {
            ShowBRollMenu(quote, rowIndex);
        }

        private void ShowBRollMenu(PhilosophyScriptItem quote, int rowIndex)
        {
            if (quote == null)
            {
                return;
            }

            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };
            menu.Items.Add("Ngẫu nhiên", null, (_, __) =>
                ApplyBRollSelection(quote, rowIndex, PhilosophyBRollSelection.RandomToken));
            menu.Items.Add("Chọn file .mp4…", null, (_, __) => BrowseBRollVideoFile(quote, rowIndex));
            menu.Items.Add("Chọn thư mục…", null, (_, __) => BrowseBRollFolder(quote, rowIndex));
            menu.Items.Add("Thư viện trong app…", null, (_, __) => PickBRollFromLibrary(quote, rowIndex));
            menu.Show(Cursor.Position);
        }

        private void BrowseBRollVideoFile(PhilosophyScriptItem quote, int rowIndex)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Video|*.mp4;*.mov",
                Title = "Chọn file B-roll",
                InitialDirectory = PhilosophyBRollSelection.GetBrowseInitialDirectory(_profileName)
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplyBRollSelection(quote, rowIndex, dlg.FileName?.Trim() ?? string.Empty);
            }
        }

        private void BrowseBRollFolder(PhilosophyScriptItem quote, int rowIndex)
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Chọn thư mục B-roll",
                ShowNewFolderButton = false
            })
            {
                var initial = PhilosophyBRollSelection.GetBrowseInitialDirectory(_profileName);
                if (!string.IsNullOrEmpty(initial))
                {
                    dlg.SelectedPath = initial;
                }

                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplyBRollSelection(quote, rowIndex, dlg.SelectedPath?.Trim() ?? string.Empty);
            }
        }

        private void PickBRollFromLibrary(PhilosophyScriptItem quote, int rowIndex)
        {
            using (var picker = new PhilosophyAssetLibraryPickerForm(
                PhilosophyAssetLibraryPickerForm.PickerMode.BrollVideo,
                _profileName,
                allowMultiSelect: false))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedPaths.Count == 0)
                {
                    return;
                }

                ApplyBRollSelection(quote, rowIndex, picker.SelectedPaths[0]);
            }
        }

        private void ApplyBRollSelection(PhilosophyScriptItem quote, int rowIndex, string selection)
        {
            if (quote == null)
            {
                return;
            }

            quote.BRollFolder = (selection ?? string.Empty).Trim();
            if (_brollThumbsByRow.TryGetValue(rowIndex, out _))
            {
                _brollThumbsByRow.Remove(rowIndex);
            }

            _grid.InvalidateCell(_grid.Columns["colBgBroll"].Index, rowIndex);
            ResizeGridRows();
            QueueBrollThumbnailLoadsForRow(rowIndex, quote);
            InvalidateBackgroundPreviewForRow(rowIndex, quote);
            QueueBackgroundPreviewLoadForRow(rowIndex, quote);
            RefreshStatusForRow(rowIndex);
        }

        private void PickZoomImagesFromLibrary(PhilosophyScriptItem quote, int rowIndex, bool append)
        {
            using (var picker = new PhilosophyAssetLibraryPickerForm(
                PhilosophyAssetLibraryPickerForm.PickerMode.ZoomImage,
                _profileName,
                allowMultiSelect: true))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedPaths.Count == 0)
                {
                    return;
                }

                ApplyZoomImageSelection(quote, rowIndex, picker.SelectedPaths, append);
            }
        }

        private void ApplyZoomImageSelection(
            PhilosophyScriptItem quote,
            int rowIndex,
            IReadOnlyList<string> picked,
            bool append)
        {
            if (quote == null || picked == null || picked.Count == 0)
            {
                return;
            }

            if (append && quote.ZoomImagePaths != null && quote.ZoomImagePaths.Count > 0)
            {
                quote.ZoomImagePaths = quote.ZoomImagePaths
                    .Concat(picked)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                quote.ZoomImagePaths = picked.ToList();
            }

            var modeLabel = (_grid.Rows[rowIndex].Cells["colBgMode"].Value ?? string.Empty).ToString();
            if (string.IsNullOrWhiteSpace(modeLabel)
                || string.Equals(modeLabel, PhilosophyBatchHelper.BackgroundModeBroll, StringComparison.OrdinalIgnoreCase))
            {
                quote.VisualMode = PhilosophyVisualModes.ImageZoom;
                _grid.Rows[rowIndex].Cells["colBgMode"].Value = PhilosophyBatchHelper.BackgroundModeZoom;
            }

            QueueZoomThumbnailLoadsForRow(rowIndex, quote);
            InvalidateBackgroundPreviewForRow(rowIndex, quote);
            QueueBackgroundPreviewLoadForRow(rowIndex, quote);
            RefreshStatusForRow(rowIndex);
            ResizeGridRows();
        }

        private void ShowPreviewMenu(int rowIndex, PhilosophyScriptItem quote)
        {
            if (quote == null)
            {
                return;
            }

            var sessionBase = ResolvePreviewSessionBase();
            var stamp = PhilosophyBackgroundPreviewHelper.ComputePreviewStamp(quote, _batch, _profileName);
            var videoPath = PhilosophyBackgroundPreviewHelper.GetPreviewVideoPath(sessionBase, rowIndex);
            var isCurrent = PhilosophyBackgroundPreviewHelper.IsPreviewCurrent(sessionBase, rowIndex, stamp);
            var cacheDir = PhilosophyBackgroundPreviewHelper.GetQuotePreviewDirectory(sessionBase, rowIndex);

            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };

            var regenLabel = isCurrent ? "Tạo lại preview" : "Tạo preview";
            menu.Items.Add(regenLabel, null, (_, __) =>
            {
                InvalidateBackgroundPreviewForRow(rowIndex, quote);
                _ = GenerateBackgroundPreviewAsync(rowIndex, quote);
            });
            var viewItem = menu.Items.Add("Xem video", null, (_, __) =>
                PhilosophyBackgroundPreviewHelper.TryOpenPreviewVideo(videoPath));
            viewItem.Enabled = isCurrent;
            menu.Items.Add("Mở thư mục cache", null, (_, __) =>
            {
                try
                {
                    Directory.CreateDirectory(cacheDir);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cacheDir)
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Preview nền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
            menu.Show(Cursor.Position);
        }

        private async Task<AppSettings> LoadSettingsCachedAsync()
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            _cachedSettings = await _configManager.LoadAsync().ConfigureAwait(false);
            return _cachedSettings;
        }

        private void RefreshAllRowStatuses(AppSettings settings = null)
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            settings = settings ?? TryLoadSettingsSync();

            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                RefreshStatusForRow(i, settings);
            }
        }

        private void RefreshStatusForRow(int rowIndex, AppSettings settings = null)
        {
            if (_grid == null || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var quote = _grid.Rows[rowIndex].Tag as PhilosophyScriptItem;
            if (settings == null)
            {
                settings = TryLoadSettingsSync();
            }

            var status = PhilosophyBackgroundRowValidator.Validate(quote, _batch, _profileName, settings);
            _statusByRow[rowIndex] = status;
            if (_grid.Columns.Contains("colBgStatus"))
            {
                _grid.InvalidateCell(_grid.Columns["colBgStatus"].Index, rowIndex);
            }
        }

        private void SyncQuoteContentFromGrid(int rowIndex)
        {
            if (_grid == null || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var quote = _grid.Rows[rowIndex].Tag as PhilosophyScriptItem;
            if (quote == null)
            {
                return;
            }

            var newContent = (_grid.Rows[rowIndex].Cells["colBgQuote"].Value ?? string.Empty).ToString().Trim();
            var oldContent = quote.Content ?? string.Empty;
            if (string.Equals(newContent, oldContent, StringComparison.Ordinal))
            {
                return;
            }

            AppSettings settings = TryLoadSettingsSync();

            var oldBuilt = PhilosophyBatchHelper.BuildAiVeoPrompt(
                new PhilosophyScriptItem { Content = oldContent, Mood = quote.Mood, MotionPrompt = string.Empty },
                _profileName,
                _batch.Topic,
                settings);
            var currentPrompt = (quote.MotionPrompt ?? string.Empty).Trim();
            quote.Content = newContent;
            if (string.IsNullOrWhiteSpace(currentPrompt)
                || string.Equals(currentPrompt, oldBuilt, StringComparison.Ordinal))
            {
                quote.MotionPrompt = PhilosophyBatchHelper.BuildAiVeoPrompt(quote, _profileName, _batch.Topic, settings);
                _grid.Rows[rowIndex].Cells["colBgPrompt"].Value = quote.MotionPrompt;
            }

            InvalidateBackgroundPreviewForRow(rowIndex, quote);
            QueueBackgroundPreviewLoadForRow(rowIndex, quote);
            RefreshStatusForRow(rowIndex, settings);
        }

        private void CopyBackgroundModeToAllRows()
        {
            var sourceIndex = GetCurrentRowIndex();
            if (sourceIndex < 0 || _grid.Rows.Count == 0)
            {
                MessageBox.Show(this, "Chọn một dòng nguồn trước.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplyRowEdits();
            var source = _grid.Rows[sourceIndex].Tag as PhilosophyScriptItem;
            if (source == null)
            {
                return;
            }

            var modeLabel = (_grid.Rows[sourceIndex].Cells["colBgMode"].Value ?? PhilosophyBatchHelper.BackgroundModeBroll).ToString();
            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                if (i == sourceIndex)
                {
                    continue;
                }

                var quote = _grid.Rows[i].Tag as PhilosophyScriptItem;
                if (quote == null)
                {
                    continue;
                }

                quote.VisualMode = source.VisualMode;
                _grid.Rows[i].Cells["colBgMode"].Value = modeLabel;
                _grid.InvalidateRow(i);
                RefreshStatusForRow(i);
            }

            UpdateColumnHeaderHighlights();
            MessageBox.Show(this, "Đã copy loại nền sang " + (_grid.Rows.Count - 1) + " dòng khác.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CopyBackgroundResourcesToMatchingRows()
        {
            var sourceIndex = GetCurrentRowIndex();
            if (sourceIndex < 0 || _grid.Rows.Count == 0)
            {
                MessageBox.Show(this, "Chọn một dòng nguồn trước.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplyRowEdits();
            var source = _grid.Rows[sourceIndex].Tag as PhilosophyScriptItem;
            if (source == null)
            {
                return;
            }

            var sourceMode = PhilosophyVisualModes.Normalize(source.VisualMode);
            var copied = 0;
            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                if (i == sourceIndex)
                {
                    continue;
                }

                var quote = _grid.Rows[i].Tag as PhilosophyScriptItem;
                if (quote == null)
                {
                    continue;
                }

                if (PhilosophyVisualModes.Normalize(quote.VisualMode) != sourceMode)
                {
                    continue;
                }

                quote.BRollFolder = source.BRollFolder;
                quote.ZoomImagePaths = source.ZoomImagePaths?.ToList() ?? new List<string>();
                quote.MotionPrompt = source.MotionPrompt;
                _grid.Rows[i].Cells["colBgPrompt"].Value = quote.MotionPrompt ?? string.Empty;
                InvalidateBackgroundPreviewForRow(i, quote);
                QueueBrollThumbnailLoadsForRow(i, quote);
                QueueZoomThumbnailLoadsForRow(i, quote);
                QueueBackgroundPreviewLoadForRow(i, quote);
                RefreshStatusForRow(i);
                copied++;
            }

            ResizeGridRows();
            MessageBox.Show(this, "Đã copy tài nguyên sang " + copied + " dòng cùng loại nền.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task RunGeminiBackgroundSuggestionsAsync(bool selectedRowOnly)
        {
            if (_isGeminiSuggestRunning)
            {
                return;
            }

            ApplyRowEdits();
            AppSettings settings;
            try
            {
                settings = await LoadSettingsCachedAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Gemini gợi ý nền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                MessageBox.Show(this, "Cần AI API Key (Gemini) trong tab Cài đặt.", "Gemini gợi ý nền", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rowIndices = new List<int>();
            if (selectedRowOnly)
            {
                var idx = GetCurrentRowIndex();
                if (idx >= 0)
                {
                    rowIndices.Add(idx);
                }
            }
            else
            {
                for (var i = 0; i < _grid.Rows.Count; i++)
                {
                    rowIndices.Add(i);
                }
            }

            if (rowIndices.Count == 0)
            {
                MessageBox.Show(this, "Không có dòng để gợi ý.", "Gemini gợi ý nền", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var quotes = rowIndices
                .Select(i => _grid.Rows[i].Tag as PhilosophyScriptItem)
                .Where(q => q != null)
                .ToList();
            if (quotes.Count == 0)
            {
                return;
            }

            _isGeminiSuggestRunning = true;
            _lblModeHint.Text = "Đang gọi Gemini gợi ý nền…";
            try
            {
                var suggestions = await _videoProcessingService.GeneratePhilosophyBackgroundSuggestionsAsync(
                    quotes,
                    _batch.Topic ?? string.Empty,
                    settings,
                    _profileName,
                    CancellationToken.None).ConfigureAwait(true);

                if (suggestions == null || suggestions.Count == 0)
                {
                    MessageBox.Show(this, "Gemini không trả gợi ý hợp lệ.", "Gemini gợi ý nền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                for (var i = 0; i < rowIndices.Count && i < suggestions.Count; i++)
                {
                    var rowIndex = rowIndices[i];
                    var quote = _grid.Rows[rowIndex].Tag as PhilosophyScriptItem;
                    var suggestion = suggestions[i];
                    if (quote == null || suggestion == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(suggestion.MotionPrompt))
                    {
                        quote.MotionPrompt = suggestion.MotionPrompt.Trim();
                        _grid.Rows[rowIndex].Cells["colBgPrompt"].Value = quote.MotionPrompt;
                    }

                    if (!string.IsNullOrWhiteSpace(suggestion.BRollFolder))
                    {
                        quote.BRollFolder = suggestion.BRollFolder.Trim();
                    }

                    if (suggestion.ZoomImagePaths != null && suggestion.ZoomImagePaths.Count > 0)
                    {
                        quote.ZoomImagePaths = suggestion.ZoomImagePaths.ToList();
                        if (suggestion.PreferZoomMode)
                        {
                            quote.VisualMode = PhilosophyVisualModes.ImageZoom;
                            _grid.Rows[rowIndex].Cells["colBgMode"].Value = PhilosophyBatchHelper.BackgroundModeZoom;
                        }
                    }

                    InvalidateBackgroundPreviewForRow(rowIndex, quote);
                    QueueBrollThumbnailLoadsForRow(rowIndex, quote);
                    QueueZoomThumbnailLoadsForRow(rowIndex, quote);
                    QueueBackgroundPreviewLoadForRow(rowIndex, quote);
                    RefreshStatusForRow(rowIndex, settings);
                }

                ResizeGridRows();
                UpdateColumnHeaderHighlights();
                MessageBox.Show(this, "Đã áp dụng gợi ý Gemini cho " + Math.Min(suggestions.Count, rowIndices.Count) + " dòng.", "Gemini gợi ý nền", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Gemini gợi ý nền", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isGeminiSuggestRunning = false;
                UpdateModeHintLabel(GetCurrentRowIndex());
            }
        }

        private async Task PreviewAllRowsAsync()
        {
            if (_isBatchPreviewRunning || _grid == null || _grid.Rows.Count == 0)
            {
                return;
            }

            AppSettings settings;
            try
            {
                settings = await LoadSettingsCachedAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Preview tất cả", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isBatchPreviewRunning = true;
            var total = _grid.Rows.Count;
            var done = 0;
            var failed = 0;
            try
            {
                for (var i = 0; i < total; i++)
                {
                    var quote = _grid.Rows[i].Tag as PhilosophyScriptItem;
                    _lblModeHint.Text = "Preview batch: dòng " + (i + 1) + " / " + total + "…";
                    if (quote == null)
                    {
                        continue;
                    }

                    if (!PhilosophyBackgroundPreviewHelper.TryValidateBackgroundPreviewRequest(
                            quote,
                            _batch,
                            _profileName,
                            settings,
                            out _))
                    {
                        failed++;
                        RefreshStatusForRow(i, settings);
                        continue;
                    }

                    var sessionBase = PhilosophyBackgroundPreviewHelper.GetSessionBase(_batch, settings);
                    _previewSessionBase = sessionBase;
                    var videoPath = PhilosophyBackgroundPreviewHelper.GetPreviewVideoPath(sessionBase, i);
                    var stamp = PhilosophyBackgroundPreviewHelper.ComputePreviewStamp(quote, _batch, _profileName);
                    if (PhilosophyBackgroundPreviewHelper.IsPreviewCurrent(sessionBase, i, stamp))
                    {
                        done++;
                        QueueBackgroundPreviewLoadForRow(i, quote);
                        continue;
                    }

                    _generatingPreviewRows.Add(i);
                    _previewPlaceholdersByRow[i] = "Đang tạo…";
                    _grid.InvalidateCell(_grid.Columns["colBgPreview"].Index, i);

                    var renderOptions = PhilosophyBackgroundPreviewHelper.BuildRenderOptions(_batch, quote, _profileName, settings);
                    var profile = PhilosophyBackgroundPreviewHelper.ResolveAutomationProfile(settings, _profileName);
                    var (ok, _) = await _previewPipeline.GenerateBackgroundVideoAsync(
                        quote,
                        renderOptions,
                        settings,
                        profile,
                        videoPath,
                        _ => { },
                        CancellationToken.None).ConfigureAwait(true);

                    _generatingPreviewRows.Remove(i);
                    if (ok)
                    {
                        PhilosophyBackgroundPreviewHelper.WritePreviewStamp(sessionBase, i, stamp);
                        QueueBackgroundPreviewLoadForRow(i, quote);
                        done++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                MessageBox.Show(
                    this,
                    "Preview batch xong: " + done + " OK, " + failed + " bỏ qua/lỗi (xem cột TT).",
                    "Preview tất cả",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            finally
            {
                _isBatchPreviewRunning = false;
                UpdateModeHintLabel(GetCurrentRowIndex());
            }
        }

        private void QueueBrollThumbnailLoadsForRow(int rowIndex, PhilosophyScriptItem quote)
        {
            if (quote == null || _grid == null || _grid.IsDisposed)
            {
                return;
            }

            var selection = quote.BRollFolder ?? string.Empty;
            Task.Run(() =>
            {
                var thumb = PhilosophyBrollThumbnailHelper.TryGetThumbnail(selection, _profileName);
                if (thumb == null || _grid.IsDisposed)
                {
                    return;
                }

                try
                {
                    _grid.BeginInvoke(new Action(() =>
                    {
                        if (_grid.IsDisposed || rowIndex >= _grid.Rows.Count)
                        {
                            return;
                        }

                        _brollThumbsByRow[rowIndex] = thumb;
                        _grid.InvalidateCell(_grid.Columns["colBgBroll"].Index, rowIndex);
                    }));
                }
                catch
                {
                    // ignored
                }
            });
        }

        private void ApplyGridColumnWidths()
        {
            if (_grid == null || _grid.IsDisposed || _applyingGridColumnWidths)
            {
                return;
            }

            _applyingGridColumnWidths = true;
            try
            {
                _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                AppGridSttColumn.EnsureFirstColumn(_grid, width: Sc((int)(AppGridSttColumn.CompactColumnWidth * 1.2)));

                const int promptFillWeight = 56;
                const int quoteFillWeight = 44;
                var compactColumnWidth = MeasureEqualCompactColumnWidth();

                SetFixedMinColumn("colBgMode", compactColumnWidth);
                SetFixedMinColumn("colBgStatus", Sc(40));
                SetFixedMinColumn("colBgBroll", compactColumnWidth);
                SetFixedMinColumn("colBgZoomImages", compactColumnWidth);
                SetFixedMinColumn("colBgRefImage", compactColumnWidth);
                SetFixedMinColumn("colBgPreview", compactColumnWidth);
                SetFillColumn("colBgQuote", quoteFillWeight, QuoteColumnMinWidth);
                SetFillColumn("colBgPrompt", promptFillWeight, PromptColumnMinWidth);
            }
            finally
            {
                _applyingGridColumnWidths = false;
            }
        }

        private void SetFixedMinColumn(string columnName, int width)
        {
            if (_grid == null || !_grid.Columns.Contains(columnName))
            {
                return;
            }

            var column = _grid.Columns[columnName];
            if (!column.Visible)
            {
                return;
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = width;
        }

        private void SetFillColumn(string columnName, int fillWeight, int minimumWidth)
        {
            if (_grid == null || !_grid.Columns.Contains(columnName))
            {
                return;
            }

            var column = _grid.Columns[columnName];
            if (!column.Visible)
            {
                return;
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
        }

        private int MeasureEqualCompactColumnWidth()
        {
            var modeWidth = MeasureComboColumnMinWidth("colBgMode");
            var previewWidth = Math.Max(MeasureBrollColumnWidth(), MeasureColumnHeaderWidth("colBgPreview"));
            var widths = new List<int> { modeWidth, previewWidth, Sc(40) };

            if (IsGridColumnVisible("colBgBroll"))
            {
                widths.Add(Math.Max(MeasureBrollColumnWidth(), MeasureColumnHeaderWidth("colBgBroll")));
            }

            if (IsGridColumnVisible("colBgZoomImages"))
            {
                widths.Add(Math.Max(MeasureBrollColumnWidth(), MeasureColumnHeaderWidth("colBgZoomImages")));
            }

            if (IsGridColumnVisible("colBgRefImage"))
            {
                widths.Add(Math.Max(MeasureRefImageColumnWidth(), MeasureColumnHeaderWidth("colBgRefImage")));
            }

            return widths.Max();
        }

        private bool IsGridColumnVisible(string columnName)
        {
            return _grid != null
                   && _grid.Columns.Contains(columnName)
                   && _grid.Columns[columnName].Visible;
        }

        private static int MeasureBrollColumnWidth()
        {
            var thumbHeight = BrollThumbMinRowHeight - Sc(8);
            var thumbWidth = (int)Math.Round(thumbHeight * 9.0 / 16.0);
            return Math.Max(Form1.AppGridColumnMinWidth, thumbWidth + Sc(8));
        }

        private int MeasureComboColumnMinWidth(string columnName)
        {
            if (_grid == null || !_grid.Columns.Contains(columnName))
            {
                return Form1.AppGridColumnMinWidth;
            }

            if (!(_grid.Columns[columnName] is DataGridViewComboBoxColumn column))
            {
                return MeasureColumnHeaderWidth(columnName);
            }

            var headerWidth = MeasureColumnHeaderWidth(column);
            var font = column.DefaultCellStyle?.Font ?? _grid.DefaultCellStyle.Font ?? _grid.Font;
            var contentWidth = headerWidth;
            foreach (var item in column.Items)
            {
                var text = item?.ToString() ?? string.Empty;
                contentWidth = Math.Max(
                    contentWidth,
                    TextRenderer.MeasureText(
                        text,
                        font,
                        new Size(int.MaxValue, 0),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 28);
            }

            return Math.Max(headerWidth, contentWidth);
        }

        private int MeasureColumnHeaderWidth(string columnName)
        {
            if (_grid == null || !_grid.Columns.Contains(columnName))
            {
                return Form1.AppGridColumnMinWidth;
            }

            return MeasureColumnHeaderWidth(_grid.Columns[columnName]);
        }

        private int MeasureColumnHeaderWidth(DataGridViewColumn column)
        {
            if (_grid == null || column == null)
            {
                return Form1.AppGridColumnMinWidth;
            }

            var headerFont = _grid.ColumnHeadersDefaultCellStyle?.Font ?? _grid.Font;
            var headerPadding = _grid.ColumnHeadersDefaultCellStyle?.Padding ?? Padding.Empty;
            var headerText = (column.HeaderText ?? string.Empty).Trim();
            if (headerText.Length == 0)
            {
                headerText = " ";
            }

            var textWidth = TextRenderer.MeasureText(
                headerText,
                headerFont,
                new Size(int.MaxValue, GridHeaderHeight),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;

            var measured = Math.Max(
                Form1.AppGridColumnMinWidth,
                textWidth + Form1.AppGridHeaderColumnPad + headerPadding.Horizontal + 4);

            if (column is DataGridViewComboBoxColumn)
            {
                measured += 18;
            }

            return measured;
        }

        private void ApplyRowEdits()
        {
            for (var r = 0; r < _grid.Rows.Count; r++)
            {
                var quote = _grid.Rows[r].Tag as PhilosophyScriptItem;
                if (quote == null)
                {
                    continue;
                }

                var modeLabel = (_grid.Rows[r].Cells["colBgMode"].Value ?? PhilosophyBatchHelper.BackgroundModeBroll).ToString();
                quote.VisualMode = PhilosophyBatchHelper.FromSimpleBackgroundModeLabel(modeLabel);
                quote.Content = (_grid.Rows[r].Cells["colBgQuote"].Value ?? string.Empty).ToString().Trim();
                quote.MotionPrompt = (_grid.Rows[r].Cells["colBgPrompt"].Value ?? string.Empty).ToString().Trim();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                ApplyRowEdits();
                _batch.RefreshDerivedFields();
            }

            _brollThumbsByRow.Clear();
            foreach (var thumb in _zoomThumbsByRow.Values)
            {
                thumb?.Dispose();
            }

            _zoomThumbsByRow.Clear();
            foreach (var kvp in _previewThumbsByRow.ToList())
            {
                if (_previewOwnsImageByRow.TryGetValue(kvp.Key, out var owns) && owns)
                {
                    kvp.Value?.Dispose();
                }
            }

            _previewThumbsByRow.Clear();
            _previewOwnsImageByRow.Clear();
            _previewPlaceholdersByRow.Clear();
            _previewTooltipsByRow.Clear();
            _generatingPreviewRows.Clear();
            _refImageThumb?.Dispose();
            _refImageThumb = null;
            base.OnFormClosing(e);
        }
    }
}
