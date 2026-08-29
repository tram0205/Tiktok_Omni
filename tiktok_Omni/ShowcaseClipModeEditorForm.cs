using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Cột «Công cụ Video»: chọn công cụ clip AI + duyệt/thêm clip quay tay (thủ công, thành cảnh riêng).</summary>
    internal sealed class ShowcaseClipModeEditorForm : Form
    {
        private const int DialogClientWidth = 2280;

        private const int DialogClientHeight = 1320;

        private const float BrollSubLibraryRowHeight = 58F;

        private const float BrollButtonRowHeight = 88F;

        private const float BrollActionRowHeight = 88F;

        private const float FooterRowHeight = 100F;

        private const int ListRowHeight = 34;

        private readonly ShowcaseVideoItem _video;

        private readonly AppSettings _settings;

        private readonly IAiVideoGenControlsHost _host;

        private string _brollLibraryId = ShowcaseCtaBrollLibraryService.SharedLibraryId;

        private bool _suppressBrollLibraryEvents;

        private ListBox _lstClipMode;
        private ComboBox _cbBrollLibraryFolder;
        private Label _lblBrollLibraryFolder;
        private Button _btnBrollLibraryAuto;
        private Button _btnBrowseRenderClips;
        private Button _btnBrowseClips;
        private Button _btnAddToStoryboard;
        private Button _btnRemoveRealClip;
        private Button _btnGenerateScript;
        private Button _btnGenerateZoomClips;
        private ComboBox _cbOutputAspect;
        private NumericUpDown _numAspectWidth;
        private NumericUpDown _numAspectHeight;
        private Label _lblAspectSize;
        private Label _lblAspectSizeX;
        private ListView _lvLibraryClips;
        private ListView _lvRenderClips;
        private Label _lblLibraryDir;
        private TableLayoutPanel _brollLayout;
        private ImageList _listRowHeightSpacer;
        private readonly Dictionary<string, double> _clipDurationCache =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private int _libraryCatalogGeneration;

        public ShowcaseClipModeEditorForm(ShowcaseVideoItem video, AppSettings settings, IAiVideoGenControlsHost host = null)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _host = host;

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Công cụ Video · Clip quay tay" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(DialogClientWidth, DialogClientHeight);
            BuildUi();
            LoadFromVideo();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _listRowHeightSpacer?.Dispose();
            }

            base.Dispose(disposing);
        }

        private string ResolveBrollLibraryId() =>
            ShowcaseCtaBrollLibraryPaths.ResolveFullLibraryPath(_video);

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(28, 22, 28, 20),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterRowHeight));

            root.Controls.Add(new Label
            {
                Text = "Công cụ tạo clip AI (Veo / Kling / Zoom) — chọn trước «Tạo kịch bản».",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(160, 168, 182),
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);

            _lstClipMode = new ListBox
            {
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(24, 27, 34),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                ItemHeight = 32,
                Font = new Font("Segoe UI", 10.5F)
            };
            foreach (var preset in ShowcaseClipModePresets.All)
            {
                _lstClipMode.Items.Add(preset);
            }

            _lstClipMode.DisplayMember = nameof(ShowcaseClipModePreset.DisplayLabel);

            // Co chiều rộng list vừa đủ chữ dài nhất, tránh chiếm hết bề ngang dialog.
            var longestLabelWidth = ShowcaseClipModePresets.All
                .Select(p => TextRenderer.MeasureText(p.DisplayLabel, _lstClipMode.Font).Width)
                .DefaultIfEmpty(0)
                .Max();
            _lstClipMode.Width = longestLabelWidth + 40;

            var clipModePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(0, 4, 0, 8)
            };

            // Khu vực trống bên phải danh sách công cụ AI — khung video + 3 nút thao tác chính.
            var clipModeRightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(20, 4, 8, 8)
            };
            clipModeRightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            clipModeRightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var aspectRow = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 0, 12)
            };
            aspectRow.Controls.Add(MkInlineLabel("Khung video:"));
            _cbOutputAspect = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(24, 27, 34),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5F),
                Width = 180,
                Margin = new Padding(8, 4, 0, 0)
            };
            foreach (var preset in ShowcaseOutputAspectPresets.All)
            {
                _cbOutputAspect.Items.Add(preset);
            }

            _cbOutputAspect.DisplayMember = nameof(ShowcaseOutputAspectPreset.DisplayLabel);
            _cbOutputAspect.SelectedIndexChanged += (_, __) => UpdateCustomAspectRowVisibility();
            aspectRow.Controls.Add(_cbOutputAspect);

            _lblAspectSize = MkInlineLabel("Kích thước:");
            _lblAspectSize.Margin = new Padding(16, 8, 0, 0);
            _numAspectWidth = CreateAspectDimensionInput(ShowcaseOutputAspectPresets.DefaultCustomWidth);
            _lblAspectSizeX = new Label
            {
                Text = "×",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(6, 8, 6, 0)
            };
            _numAspectHeight = CreateAspectDimensionInput(ShowcaseOutputAspectPresets.DefaultCustomHeight);
            aspectRow.Controls.Add(_lblAspectSize);
            aspectRow.Controls.Add(_numAspectWidth);
            aspectRow.Controls.Add(_lblAspectSizeX);
            aspectRow.Controls.Add(_numAspectHeight);
            clipModeRightPanel.Controls.Add(aspectRow, 0, 0);

            var clipModeActionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0)
            };
            _btnGenerateScript = MkActionButton("📝 Tạo kịch bản", Color.FromArgb(70, 92, 128));
            _btnGenerateScript.Click += async (_, __) =>
                await RunHostAction(_btnGenerateScript, h => h.GenerateShowcaseSceneScriptAsync()).ConfigureAwait(true);
            _btnGenerateZoomClips = MkActionButton("⚡ Tạo clip Zoom", Color.FromArgb(120, 96, 56));
            _btnGenerateZoomClips.Click += async (_, __) =>
                await RunHostAction(_btnGenerateZoomClips, h => h.GenerateShowcaseZoomClipsAsync()).ConfigureAwait(true);
            clipModeActionsPanel.Controls.Add(_btnGenerateScript);
            clipModeActionsPanel.Controls.Add(_btnGenerateZoomClips);
            clipModeRightPanel.Controls.Add(clipModeActionsPanel, 0, 1);

            clipModePanel.Controls.Add(clipModeRightPanel);
            clipModePanel.Controls.Add(_lstClipMode);
            root.Controls.Add(clipModePanel, 0, 1);

            var brollGroup = new GroupBox
            {
                Text = "  Clip quay tay & bảng chờ render (clips_render)  ",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 220, 200),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Padding(14, 28, 14, 16)
            };

            var brollLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = BackColor,
                Padding = new Padding(0, 6, 0, 4)
            };
            _brollLayout = brollLayout;
            brollLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, BrollSubLibraryRowHeight));
            brollLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, BrollButtonRowHeight));
            brollLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            brollLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, BrollActionRowHeight));

            var libraryRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                BackColor = BackColor,
                Padding = new Padding(0, 4, 0, 4)
            };
            libraryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            libraryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            libraryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168F));
            _lblBrollLibraryFolder = MkLabel("Thư viện clip:");
            _lblBrollLibraryFolder.Margin = new Padding(0, 6, 8, 6);
            libraryRow.Controls.Add(_lblBrollLibraryFolder, 0, 0);
            _cbBrollLibraryFolder = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(24, 27, 34),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                IntegralHeight = false,
                Margin = new Padding(0, 6, 8, 6)
            };
            _cbBrollLibraryFolder.SelectedIndexChanged += (_, __) => OnBrollLibraryFolderChanged();
            libraryRow.Controls.Add(_cbBrollLibraryFolder, 1, 0);
            _btnBrollLibraryAuto = MkActionButton("Theo Loại SP", Color.FromArgb(58, 72, 88));
            _btnBrollLibraryAuto.Margin = new Padding(0, 6, 0, 6);
            _btnBrollLibraryAuto.Click += (_, __) => ResetBrollLibraryToProductType();
            libraryRow.Controls.Add(_btnBrollLibraryAuto, 2, 0);
            brollLayout.Controls.Add(libraryRow, 0, 0);

            var buttonsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 14)
            };
            _btnBrowseRenderClips = MkActionButton("Duyệt video vào bảng", Color.FromArgb(56, 100, 140));
            _btnBrowseRenderClips.Click += OnBrowseRenderClipsClick;
            var btnOpenRenderClips = MkActionButton("Mở clip chờ render", Color.FromArgb(56, 90, 78));
            btnOpenRenderClips.Click += (_, __) => OpenRenderClipsFolder();
            _btnBrowseClips = MkActionButton("Duyệt && copy vào thư viện", Color.FromArgb(72, 138, 118));
            _btnBrowseClips.Click += OnBrowseClipsClick;
            var btnOpenLibrary = MkActionButton("Mở thư mục clip", Color.FromArgb(56, 90, 78));
            btnOpenLibrary.Click += (_, __) => OpenBrollLibraryFolder(ResolveBrollLibraryId());
            var btnRefreshCatalog = MkActionButton("Làm mới danh sách", Color.FromArgb(58, 72, 88));
            btnRefreshCatalog.Click += (_, __) =>
            {
                RefreshLibraryCatalog();
                RefreshRenderClipList();
            };
            var btnPreviewClip = MkActionButton("Xem clip", Color.FromArgb(70, 92, 128));
            btnPreviewClip.Click += OnPreviewClipClick;
            buttonsRow.Controls.Add(_btnBrowseRenderClips);
            buttonsRow.Controls.Add(btnOpenRenderClips);
            buttonsRow.Controls.Add(_btnBrowseClips);
            buttonsRow.Controls.Add(btnOpenLibrary);
            buttonsRow.Controls.Add(btnRefreshCatalog);
            buttonsRow.Controls.Add(btnPreviewClip);
            brollLayout.Controls.Add(buttonsRow, 0, 1);

            // ListView Details view không có RowHeight — ép chiều cao dòng bằng SmallImageList
            // chứa 1 ảnh trong suốt cao ListRowHeight, để các dòng không bị chèn ép sát nhau.
            _listRowHeightSpacer = new ImageList { ImageSize = new Size(1, ListRowHeight) };
            _listRowHeightSpacer.Images.Add(new Bitmap(1, ListRowHeight));

            var catalogSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(0, 6, 0, 6)
            };
            catalogSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            catalogSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
            catalogSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            catalogSplit.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            catalogSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            catalogSplit.Controls.Add(MkSectionLabel("Thư viện clip thật"), 0, 0);
            catalogSplit.Controls.Add(MkSectionLabel("Video chờ render — Zoom / Veo / Kling / quay tay"), 2, 0);

            _lvLibraryClips = CreateClipListView();
            _lvLibraryClips.SmallImageList = _listRowHeightSpacer;
            _lvLibraryClips.Columns.Add("File clip", 520);
            _lvLibraryClips.Columns.Add("time", 280);
            _lvLibraryClips.DoubleClick += OnAddToRenderQueueClick;
            StretchLastColumnToFill(_lvLibraryClips);
            catalogSplit.Controls.Add(_lvLibraryClips, 0, 1);

            _lvRenderClips = CreateClipListView();
            _lvRenderClips.ShowItemToolTips = true;
            _lvRenderClips.SmallImageList = _listRowHeightSpacer;
            _lvRenderClips.Columns.Add("Cảnh #", 70);
            _lvRenderClips.Columns.Add("Công cụ", 120);
            _lvRenderClips.Columns.Add("Tên video (gốc / scene_XX)", 480);
            _lvRenderClips.DoubleClick += OnPreviewClipClick;
            StretchLastColumnToFill(_lvRenderClips);
            catalogSplit.Controls.Add(_lvRenderClips, 2, 1);
            brollLayout.Controls.Add(catalogSplit, 0, 2);

            var actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 14)
            };
            _btnAddToStoryboard = MkActionButton("Thêm vào bảng chờ render →", Color.FromArgb(56, 120, 82));
            _btnAddToStoryboard.Click += OnAddToRenderQueueClick;
            _btnRemoveRealClip = MkActionButton("Xoá khỏi bảng chờ render", Color.FromArgb(120, 64, 64));
            _btnRemoveRealClip.Click += OnRemoveFromRenderQueueClick;
            actionRow.Controls.Add(_btnAddToStoryboard);
            actionRow.Controls.Add(_btnRemoveRealClip);
            brollLayout.Controls.Add(actionRow, 0, 3);

            brollGroup.Controls.Add(brollLayout);
            root.Controls.Add(brollGroup, 0, 2);

            _lblLibraryDir = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                TextAlign = ContentAlignment.TopLeft
            };

            var btnOk = MkButton("Lưu", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = MkButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = BackColor
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(_lblLibraryDir, 0, 0);
            var footerButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = BackColor,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            footerButtons.Controls.Add(btnCancel);
            footerButtons.Controls.Add(btnOk);
            footer.Controls.Add(footerButtons, 1, 0);
            root.Controls.Add(footer, 0, 3);

            Controls.Add(root);

            btnOk.Click += (_, __) =>
            {
                if (!SaveToVideo())
                {
                    DialogResult = DialogResult.None;
                }
            };

            RefreshLibraryInfo();
        }

        private static ListView CreateClipListView() => new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            HideSelection = false,
            GridLines = true,
            BackColor = Color.FromArgb(24, 27, 34),
            ForeColor = Color.WhiteSmoke,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(0)
        };

        /// <summary>Ép cột cuối luôn lấp hết phần rộng còn lại của ListView, tránh cuộn ngang thừa.</summary>
        private static void StretchLastColumnToFill(ListView lv)
        {
            void Apply()
            {
                if (lv.Columns.Count < 2 || lv.ClientSize.Width <= 0)
                {
                    return;
                }

                var otherWidth = 0;
                for (var i = 0; i < lv.Columns.Count - 1; i++)
                {
                    otherWidth += lv.Columns[i].Width;
                }

                var last = lv.Columns[lv.Columns.Count - 1];
                var target = lv.ClientSize.Width - otherWidth;
                if (target > 40 && target != last.Width)
                {
                    last.Width = target;
                }
            }

            lv.Resize += (_, __) => Apply();
            lv.ColumnWidthChanged += (_, e) =>
            {
                if (e.ColumnIndex != lv.Columns.Count - 1)
                {
                    Apply();
                }
            };
            Apply();
        }

        private void LoadFromVideo()
        {
            var modeId = ShowcaseClipModePresets.ResolveIdForGemini(_video.ShowcaseClipModeId);
            for (var i = 0; i < _lstClipMode.Items.Count; i++)
            {
                if (_lstClipMode.Items[i] is ShowcaseClipModePreset preset
                    && string.Equals(preset.Id, modeId, StringComparison.Ordinal))
                {
                    _lstClipMode.SelectedIndex = i;
                    break;
                }
            }

            if (_lstClipMode.SelectedIndex < 0 && _lstClipMode.Items.Count > 0)
            {
                _lstClipMode.SelectedIndex = 0;
            }

            var aspectId = ShowcaseOutputAspectPresets.ResolveId(
                _video.ShowcaseOutputAspectId,
                _settings?.ShowcaseOutputAspectDefault);
            for (var i = 0; i < _cbOutputAspect.Items.Count; i++)
            {
                if (_cbOutputAspect.Items[i] is ShowcaseOutputAspectPreset preset
                    && string.Equals(preset.Id, aspectId, StringComparison.Ordinal))
                {
                    _cbOutputAspect.SelectedIndex = i;
                    break;
                }
            }

            if (_cbOutputAspect.SelectedIndex < 0 && _cbOutputAspect.Items.Count > 0)
            {
                _cbOutputAspect.SelectedIndex = 0;
            }

            _numAspectWidth.Value = ShowcaseOutputAspectPresets.NormalizeCustomWidth(
                _video.ShowcaseOutputAspectCustomWidth);
            _numAspectHeight.Value = ShowcaseOutputAspectPresets.NormalizeCustomHeight(
                _video.ShowcaseOutputAspectCustomHeight);
            UpdateCustomAspectRowVisibility();

            SyncBrollLibraryRow();
            PopulateBrollLibraryFolderCombo();
            _brollLibraryId = ResolveBrollLibraryId();
            RefreshLibraryCatalog();
            RefreshRenderClipList();
            RefreshLibraryInfo();
        }

        private void OnBrollLibraryFolderChanged()
        {
            if (_suppressBrollLibraryEvents)
            {
                return;
            }

            if (_cbBrollLibraryFolder.SelectedItem is ShowcaseCtaBrollLibraryService.LibraryFolderEntry entry)
            {
                _video.ShowcaseCtaBrollLibraryId = entry.LibraryId ?? string.Empty;
            }

            _brollLibraryId = ResolveBrollLibraryId();
            RefreshLibraryCatalog();
            RefreshLibraryInfo();
        }

        private void ResetBrollLibraryToProductType()
        {
            _video.ShowcaseCtaBrollLibraryId = string.Empty;
            PopulateBrollLibraryFolderCombo();
            _brollLibraryId = ResolveBrollLibraryId();
            RefreshLibraryCatalog();
            RefreshLibraryInfo();
        }

        private void PopulateBrollLibraryFolderCombo()
        {
            var folders = ShowcaseCtaBrollLibraryService.ListLibraryFolders(includeSubFolders: true);
            var selectId = ShowcaseCtaBrollLibraryService.NormalizeLibraryPath(_video.ShowcaseCtaBrollLibraryId);
            if (selectId.Length == 0)
            {
                selectId = ResolveBrollLibraryId();
            }

            _suppressBrollLibraryEvents = true;
            try
            {
                _cbBrollLibraryFolder.Items.Clear();
                foreach (var folder in folders)
                {
                    _cbBrollLibraryFolder.Items.Add(folder);
                }

                for (var i = 0; i < _cbBrollLibraryFolder.Items.Count; i++)
                {
                    if (_cbBrollLibraryFolder.Items[i] is ShowcaseCtaBrollLibraryService.LibraryFolderEntry entry
                        && string.Equals(entry.LibraryId, selectId, StringComparison.OrdinalIgnoreCase))
                    {
                        _cbBrollLibraryFolder.SelectedIndex = i;
                        return;
                    }
                }

                _cbBrollLibraryFolder.SelectedIndex = _cbBrollLibraryFolder.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                _suppressBrollLibraryEvents = false;
            }
        }

        private void SyncBrollLibraryRow()
        {
            if (_brollLayout != null && _brollLayout.RowStyles.Count > 0)
            {
                _brollLayout.RowStyles[0].SizeType = SizeType.Absolute;
                _brollLayout.RowStyles[0].Height = BrollSubLibraryRowHeight;
            }
        }

        private void RefreshLibraryInfo()
        {
            var dir = ShowcaseCtaBrollLibraryService.GetLibraryDirectory(_brollLibraryId, false);
            var libraryLabel = ShowcaseCtaBrollLibraryService.GetLibraryDisplayLabel(_brollLibraryId);
            var clipsDir = ResolveClipsDir();
            var renderCount = CountRenderQueueClips(clipsDir);
            var sourceLabel = string.IsNullOrWhiteSpace(_video.ShowcaseCtaBrollLibraryId)
                ? "Tự động (Loại SP)"
                : "Đã chọn";
            _lblLibraryDir.Text = "Thư viện (" + sourceLabel + "): " + libraryLabel
                + "  ·  Thư mục: " + dir
                + "  ·  Bảng chờ render: " + renderCount + " clip"
                + (string.IsNullOrWhiteSpace(clipsDir) ? string.Empty : "  ·  clips_render: " + clipsDir);
        }

        private static int CountRenderQueueClips(string clipsDir) =>
            ShowcaseSessionService.EnumerateClipFiles(clipsDir).Count;

        private void RefreshRenderClipList()
        {
            _lvRenderClips.Items.Clear();
            var clipsDir = ResolveClipsDir();
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                RefreshLibraryInfo();
                return;
            }

            if (_video.Scenes?.Count == 0)
            {
                ShowcaseSessionService.EnsureScenesFromRenderFolder(_video, clipsDir, string.Empty, null);
            }

            ShowcaseSessionService.RefreshClipStatus(clipsDir, _video.Scenes, null);

            var assignedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var clipPath = (scene.ClipPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(clipPath) && File.Exists(clipPath))
                {
                    try
                    {
                        assignedPaths.Add(Path.GetFullPath(clipPath));
                    }
                    catch
                    {
                        assignedPaths.Add(clipPath);
                    }
                }

                var toolLabel = ResolveRenderQueueToolLabel(scene, clipPath);
                var displayName = string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath)
                    ? "✗ Chưa có clip"
                    : ShowcaseClipDisplayHelper.FormatRenderQueueFileName(scene, clipPath);
                var lvi = new ListViewItem(new[]
                {
                    (i + 1).ToString(),
                    toolLabel,
                    displayName
                })
                {
                    Tag = new RenderClipRowTag(clipPath)
                };
                lvi.ToolTipText = ShowcaseClipDisplayHelper.FormatRenderQueueToolTip(scene, clipPath);
                _lvRenderClips.Items.Add(lvi);
            }

            foreach (var clipPath in ShowcaseSessionService.EnumeratePendingClipFiles(clipsDir))
            {
                if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(clipPath);
                }
                catch
                {
                    fullPath = clipPath;
                }

                if (assignedPaths.Contains(fullPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(clipPath);
                var lvi = new ListViewItem(new[]
                {
                    "—",
                    "Chờ gán",
                    fileName
                })
                {
                    Tag = new RenderClipRowTag(clipPath)
                };
                lvi.ToolTipText = "Clip quay tay — sẽ thêm cảnh khi «Tạo lời thoại» hoặc sau khi duyệt video.\r\n" + clipPath;
                _lvRenderClips.Items.Add(lvi);
            }

            RefreshLibraryInfo();
        }

        private void RefreshLibraryCatalog()
        {
            _lvLibraryClips.Items.Clear();
            var missing = new List<string>();
            foreach (var clipPath in ShowcaseCtaBrollLibraryService.EnumerateClipFiles(_brollLibraryId))
            {
                if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(clipPath);
                }
                catch
                {
                    fullPath = clipPath;
                }

                var durationLabel = _clipDurationCache.TryGetValue(fullPath, out var cachedSeconds)
                    ? FormatDurationLabel(cachedSeconds)
                    : "…";
                var lvi = new ListViewItem(new[] { Path.GetFileName(clipPath), durationLabel })
                {
                    Tag = fullPath
                };
                _lvLibraryClips.Items.Add(lvi);

                if (!_clipDurationCache.ContainsKey(fullPath))
                {
                    missing.Add(fullPath);
                }
            }

            if (missing.Count > 0)
            {
                var generation = ++_libraryCatalogGeneration;
                _ = ProbeLibraryDurationsAsync(missing, generation);
            }
        }

        // Dò thời lượng bằng ffprobe chạy nền (không chặn UI) rồi cập nhật từng dòng khi có kết quả.
        // "generation" dùng để bỏ qua kết quả cũ nếu danh sách thư viện đã được làm mới lại trước khi dò xong.
        private async Task ProbeLibraryDurationsAsync(IList<string> clipPaths, int generation)
        {
            string ffprobeExe;
            try
            {
                ffprobeExe = await Task.Run(() =>
                        FfmpegToolkitService.TryResolve(_settings, out var toolkit, out _)
                            ? toolkit.FfprobeExe
                            : FfmpegToolkitService.GetBundledFfprobePath())
                    .ConfigureAwait(true);
            }
            catch
            {
                return;
            }

            foreach (var clipPath in clipPaths)
            {
                if (IsDisposed || generation != _libraryCatalogGeneration)
                {
                    return;
                }

                double seconds;
                try
                {
                    seconds = await ShowcaseCtaBrollLibraryService
                        .ProbeDurationSecondsAsync(clipPath, ffprobeExe, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                catch
                {
                    seconds = 0d;
                }

                _clipDurationCache[clipPath] = seconds;

                if (IsDisposed || generation != _libraryCatalogGeneration)
                {
                    return;
                }

                foreach (ListViewItem item in _lvLibraryClips.Items)
                {
                    if (item.Tag is string tag && string.Equals(tag, clipPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.SubItems[1].Text = FormatDurationLabel(seconds);
                        break;
                    }
                }
            }
        }

        private static string FormatDurationLabel(double seconds)
        {
            return seconds > 0
                ? seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s"
                : "?";
        }

        private string ResolveClipsDir()
        {
            var clipsDir = (_video.ShowcaseClipsDir ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(clipsDir) && Directory.Exists(clipsDir))
            {
                return clipsDir;
            }

            var sessionBase = (_video.ShowcaseSessionBaseDir ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sessionBase))
            {
                var derived = ShowcaseRenderClipsPaths.ResolveDirectory(sessionBase, createIfMissing: false, migrateLegacy: true);
                if (!string.IsNullOrWhiteSpace(derived) && Directory.Exists(derived))
                {
                    return derived;
                }
            }

            return clipsDir;
        }

        private static string ResolveRenderQueueToolLabel(AiVideoGenInputItem scene, string clipPath)
        {
            if (scene != null && !string.IsNullOrWhiteSpace(scene.ShowcaseClipTool))
            {
                return ShowcaseClipToolHelper.GetToolDisplayLabel(scene.ShowcaseClipTool);
            }

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                return ShowcaseClipToolHelper.GetToolDisplayLabel(ShowcaseClipToolHelper.ToolReal);
            }

            return "—";
        }

        private async void OnBrowseRenderClipsClick(object sender, EventArgs e)
        {
            if (_host == null)
            {
                MessageBox.Show(this,
                    "Không thể duyệt video — hãy mở dialog từ cột «Công cụ Video» trên lưới chính.",
                    "Duyệt video vào bảng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _btnBrowseRenderClips.Enabled = false;
            try
            {
                ApplyEditorSettingsToVideo();
                await _host.ImportShowcaseClipsForVideoAsync(_video).ConfigureAwait(true);
                RefreshRenderClipList();
                RefreshLibraryInfo();
                LogStatus("Đã cập nhật bảng clip chờ render từ clips_render.");
            }
            finally
            {
                if (!IsDisposed)
                {
                    _btnBrowseRenderClips.Enabled = true;
                }
            }
        }

        private void OpenRenderClipsFolder()
        {
            var sessionBase = (_video.ShowcaseSessionBaseDir ?? string.Empty).Trim();
            var dir = !string.IsNullOrWhiteSpace(sessionBase)
                ? ShowcaseRenderClipsPaths.ResolveDirectory(sessionBase, createIfMissing: true, migrateLegacy: true)
                : ResolveClipsDir();
            if (string.IsNullOrWhiteSpace(dir))
            {
                MessageBox.Show(this,
                    "Chưa có thư mục clip chờ render — bấm «Duyệt video vào bảng» để tạo phiên và thêm clip.",
                    "Mở clip chờ render",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Mở clip chờ render", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void OnBrowseClipsClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn clip quay tay (có thể chọn nhiều file)",
                Filter = "Video (*.mp4;*.mov;*.webm;*.mkv)|*.mp4;*.mov;*.webm;*.mkv|Tất cả file|*.*",
                Multiselect = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.FileNames.Length == 0)
                {
                    return;
                }

                _btnBrowseClips.Enabled = false;
                try
                {
                    await CopyClipsToLibraryAsync(dlg.FileNames).ConfigureAwait(true);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        _btnBrowseClips.Enabled = true;
                    }
                }
            }
        }

        private async Task CopyClipsToLibraryAsync(IList<string> filePaths)
        {
            var result = await Task.Run(() =>
                    ShowcaseCtaBrollLibraryService.CopyClipsPreserveName(_brollLibraryId, filePaths))
                .ConfigureAwait(true);

            foreach (var message in result.Messages)
            {
                LogStatus(message);
            }

            RefreshLibraryCatalog();
            RefreshLibraryInfo();
            LogStatus("Đã thêm " + result.Copied + " clip vào thư viện «"
                      + ShowcaseCtaBrollLibraryService.GetLibraryDisplayLabel(_brollLibraryId) + "»"
                      + (result.Skipped > 0 ? " (bỏ qua " + result.Skipped + ")." : ".")
                      + " Chọn clip bên trái rồi bấm «Thêm vào bảng chờ render».");
        }

        // Gọi thao tác Showcase chính (Tạo kịch bản / Tạo clip Zoom / Tạo lời thoại) trên host (Form1) —
        // các thao tác này tác động lên (các) dòng video đang được chọn trên lưới chính, thường trùng với
        // dòng đang mở dialog này. Dùng chung cơ chế khoá TryBeginShowcaseTabWork/EndShowcaseTabWork với
        // AiVideoGenControls để tránh chạy chồng nhiều thao tác Showcase cùng lúc.
        private async Task RunHostAction(Button button, Func<IAiVideoGenControlsHost, Task> action)
        {
            if (_host == null || button == null || action == null)
            {
                return;
            }

            if (!_host.TryBeginShowcaseTabWork())
            {
                return;
            }

            button.Enabled = false;
            try
            {
                ApplyEditorSettingsToVideo();
                await action(_host).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Thao tác Showcase", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _host.EndShowcaseTabWork();
                if (!IsDisposed)
                {
                    if (!button.IsDisposed && !_host.IsShowcaseTabPaused)
                    {
                        button.Enabled = true;
                    }

                    // Kịch bản/clip Zoom/lời thoại có thể làm thay đổi thứ tự hoặc số lượng cảnh —
                    // đồng bộ lại bảng chờ render trong dialog cho khớp trạng thái mới nhất.
                    RefreshLibraryCatalog();
                    RefreshRenderClipList();
                }
            }
        }

        private void OnPreviewClipClick(object sender, EventArgs e)
        {
            var paths = _lvRenderClips.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag is RenderClipRowTag tag ? tag.ClipPath : item.Tag as string)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (paths.Count == 0)
            {
                paths = _lvLibraryClips.SelectedItems
                    .Cast<ListViewItem>()
                    .Select(item => item.Tag as string)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
            }

            if (paths.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn một clip trong bảng chờ render hoặc thư viện clip thật để xem trước.",
                    "Xem clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                {
                    LogStatus("Không tìm thấy file «" + Path.GetFileName(path) + "» để xem.");
                    continue;
                }

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "Không mở được clip «" + Path.GetFileName(path) + "»: " + ex.Message,
                        "Xem clip",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private async void OnAddToRenderQueueClick(object sender, EventArgs e)
        {
            if (_lvLibraryClips.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn ít nhất một clip trong «Thư viện clip thật».",
                    "Thêm vào bảng chờ render",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_host == null)
            {
                MessageBox.Show(this,
                    "Không thể thêm clip — hãy mở dialog từ cột «Công cụ Video» trên lưới chính.",
                    "Thêm vào bảng chờ render",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _btnAddToStoryboard.Enabled = false;
            try
            {
                ApplyEditorSettingsToVideo();
                var paths = _lvLibraryClips.SelectedItems
                    .Cast<ListViewItem>()
                    .Select(item => item.Tag as string)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                await _host.AddShowcaseLibraryClipsToRenderQueueAsync(_video, paths).ConfigureAwait(true);
                RefreshRenderClipList();
                LogStatus("Đã thêm clip vào clips_render (giữ tên gốc) — cột «Tên video» hiện đúng tên file gốc theo từng cảnh.");
            }
            finally
            {
                if (!IsDisposed)
                {
                    _btnAddToStoryboard.Enabled = true;
                }
            }
        }

        private void OnRemoveFromRenderQueueClick(object sender, EventArgs e)
        {
            if (_lvRenderClips.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn clip trong bảng «Video chờ render» để xoá.",
                    "Xoá khỏi bảng chờ render",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var removed = 0;
            foreach (ListViewItem item in _lvRenderClips.SelectedItems)
            {
                if (!(item.Tag is RenderClipRowTag tag) || string.IsNullOrWhiteSpace(tag.ClipPath))
                {
                    continue;
                }

                try
                {
                    if (File.Exists(tag.ClipPath))
                    {
                        File.Delete(tag.ClipPath);
                        removed++;
                    }

                    foreach (var scene in _video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
                    {
                        if (scene == null || string.IsNullOrWhiteSpace(scene.ClipPath))
                        {
                            continue;
                        }

                        if (string.Equals(
                                Path.GetFullPath(scene.ClipPath),
                                Path.GetFullPath(tag.ClipPath),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            scene.ClipPath = string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogStatus("Không xoá được «" + Path.GetFileName(tag.ClipPath) + "»: " + ex.Message);
                }
            }

            RefreshRenderClipList();
            LogStatus(removed > 0
                ? "Đã xoá " + removed + " clip khỏi clips_render."
                : "Không có file nào được xoá.");
        }

        private sealed class RenderClipRowTag
        {
            public RenderClipRowTag(string clipPath)
            {
                ClipPath = clipPath ?? string.Empty;
            }

            public string ClipPath { get; }
        }

        internal void LogStatus(string message)
        {
            if (_lblLibraryDir != null && !_lblLibraryDir.IsDisposed)
            {
                _lblLibraryDir.Text = message;
            }
        }

        private bool SaveToVideo()
        {
            ApplyEditorSettingsToVideo();
            return true;
        }

        private void ApplyEditorSettingsToVideo()
        {
            if (_lstClipMode.SelectedItem is ShowcaseClipModePreset mode)
            {
                _video.ShowcaseClipModeId = mode.Id;
            }

            if (_cbOutputAspect.SelectedItem is ShowcaseOutputAspectPreset aspect)
            {
                _video.ShowcaseOutputAspectId = aspect.Id;
            }

            if (ShowcaseOutputAspectPresets.IsCustomId(_video.ShowcaseOutputAspectId))
            {
                _video.ShowcaseOutputAspectCustomWidth = (int)_numAspectWidth.Value;
                _video.ShowcaseOutputAspectCustomHeight = (int)_numAspectHeight.Value;
            }

            if (_cbBrollLibraryFolder.SelectedItem is ShowcaseCtaBrollLibraryService.LibraryFolderEntry folderEntry)
            {
                _video.ShowcaseCtaBrollLibraryId = folderEntry.LibraryId ?? string.Empty;
            }
        }

        private static void OpenBrollLibraryFolder(string libraryId)
        {
            var dir = ShowcaseCtaBrollLibraryService.GetLibraryDirectory(libraryId, true);
            Process.Start("explorer.exe", "\"" + dir + "\"");
        }

        private void UpdateCustomAspectRowVisibility()
        {
            var isCustom = _cbOutputAspect.SelectedItem is ShowcaseOutputAspectPreset preset
                           && ShowcaseOutputAspectPresets.IsCustomId(preset.Id);
            _lblAspectSize.Visible = isCustom;
            _lblAspectSizeX.Visible = isCustom;
            _numAspectWidth.Visible = isCustom;
            _numAspectHeight.Visible = isCustom;
        }

        private static NumericUpDown CreateAspectDimensionInput(int defaultValue)
        {
            return new NumericUpDown
            {
                Minimum = ShowcaseOutputAspectPresets.MinDimension,
                Maximum = ShowcaseOutputAspectPresets.MaxDimension,
                Value = ShowcaseOutputAspectPresets.ClampDimension(defaultValue) > 0
                    ? ShowcaseOutputAspectPresets.ClampDimension(defaultValue)
                    : ShowcaseOutputAspectPresets.DefaultCustomWidth,
                Increment = 2,
                Width = 88,
                BackColor = Color.FromArgb(24, 27, 34),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(8, 4, 0, 0)
            };
        }

        private static Label MkInlineLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 168, 182),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 0)
        };

        private static Label MkLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(160, 168, 182),
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static Label MkSectionLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(150, 200, 180),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };

        private static Button MkActionButton(string text, Color back)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(160, 46),
                Padding = new Padding(14, 8, 14, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                // Margin dưới (6px) để viền nút không bị "chạm đáy" panel — tránh trông như mất đít.
                Margin = new Padding(0, 0, 10, 6),
                UseVisualStyleBackColor = false
            };
            // FlatAppearance mặc định không vẽ viền đủ rõ trên nền tối — thêm viền sáng đều 4 cạnh
            // để đáy nút không bị "biến mất" vào nền.
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = ControlPaint.Light(back, 0.55f);
            return btn;
        }

        private static Button MkButton(string text, Color back)
        {
            var btn = new Button
            {
                Text = text,
                MinimumSize = new Size(130, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 6),
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = ControlPaint.Light(back, 0.55f);
            return btn;
        }
    }
}
