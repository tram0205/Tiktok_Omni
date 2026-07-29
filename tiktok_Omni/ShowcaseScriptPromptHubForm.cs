using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseScriptPromptHubForm : Form
    {
        private static readonly Color Bg = ShowcasePastelTheme.ShellBg;
        private static readonly Color FieldBg = ShowcasePastelTheme.FieldBg;

        private const int ButtonBarHeight = 100;
        private const int FormClientWidth = 2577;
        private const int FormMinWidth = 2178;
        private const int HubGridMinRowHeight = 72;
        private const int HubGridCellPad = 18;
        private const int HubGridRightEdgeInset = 20;
        private const string HubGridLayoutLock = "HubGridLayoutLock";

        private const string ColScene = "colHubScene";
        private const string ColImage = "colHubImage";
        private const string ColVoice = "colHubVoice";
        private const string ColTool = "colHubTool";
        private const string ColPrompt = "colHubPrompt";

        private const float HubGridPctScene = 0.14f;
        private const float HubGridPctImage = 0.12f;
        private const float HubGridPctVoice = 0.24f;
        private const float HubGridPctTool = 0.11f;

        private readonly ShowcaseVideoItem _video;
        private ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot _speechBeforeEdit;
        private readonly Func<Task> _exportExcelAsync;
        private readonly Func<Task<bool>> _regenerateScriptAsync;

        private TextBox _txtTheme;
        private DataGridView _dgvHub;
        private string _toolEditPrevious;
        private Button _btnRegenerateScript;

        private sealed class HubGridRowTag
        {
            public AiVideoGenInputItem Scene { get; set; }
            /// <summary>Cảnh 1 — lời thoại đồng bộ với <see cref="ShowcaseVideoItem.ShowcaseHookText"/>.</summary>
            public bool IsOpeningHookScene { get; set; }
            /// <summary>Cảnh cuối — lời thoại đồng bộ với <see cref="ShowcaseVideoItem.ShowcaseCtaText"/>.</summary>
            public bool IsClosingCtaScene { get; set; }
        }

        public ShowcaseScriptPromptHubForm(
            ShowcaseVideoItem video,
            Func<Task> exportExcelAsync = null,
            Func<Task<bool>> regenerateScriptAsync = null)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _speechBeforeEdit = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(_video);
            _exportExcelAsync = exportExcelAsync;
            _regenerateScriptAsync = regenerateScriptAsync;

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Kịch bản · Prompt" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Bg;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(FormMinWidth, 1170);
            ClientSize = new Size(FormClientWidth, 1380);

            var shell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(42, 24, 42, 20)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Bg
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonBarHeight));

            root.Controls.Add(BuildThemePanel(), 0, 0);

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            _dgvHub = BuildHubGrid(scenes);
            _dgvHub.Dock = DockStyle.Fill;
            _dgvHub.Margin = new Padding(0, 16, 0, 8);
            root.Controls.Add(_dgvHub, 0, 1);

            root.Controls.Add(BuildBottomBar(), 0, 2);

            shell.Controls.Add(root);
            Controls.Add(shell);

            Shown += (_, __) => BeginInvoke(new Action(LayoutHubGrid));
        }

        private Control BuildThemePanel()
        {
            var block = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = Bg,
                Dock = DockStyle.Top,
                Width = 400
            };
            block.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            block.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            block.Controls.Add(new Label
            {
                Text = "Chủ đề video",
                AutoSize = true,
                ForeColor = ShowcasePastelTheme.TextPrimary,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10)
            }, 0, 0);

            _txtTheme = new TextBox
            {
                Text = _video.ShowcaseTheme ?? string.Empty,
                BackColor = FieldBg,
                ForeColor = ShowcasePastelTheme.TextBody,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 4)
            };
            block.Controls.Add(_txtTheme, 0, 1);
            return block;
        }

        private DataGridView BuildHubGrid(IList<AiVideoGenInputItem> scenes)
        {
            var grid = new DataGridView
            {
                BackgroundColor = Color.FromArgb(38, 42, 52),
                GridColor = Color.FromArgb(58, 64, 78),
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ScrollBars = ScrollBars.Vertical,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    SelectionBackColor = Color.FromArgb(68, 118, 168),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 10.5F),
                    WrapMode = DataGridViewTriState.True
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(52, 58, 72),
                    ForeColor = Color.Gainsboro,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 66,
                RowTemplate = { MinimumHeight = HubGridMinRowHeight }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColScene,
                HeaderText = "Phân cảnh",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True
                }
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColImage,
                HeaderText = "Tên ảnh",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True
                }
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColVoice,
                HeaderText = "Lời thoại",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True
                }
            });

            var colTool = new DataGridViewComboBoxColumn
            {
                Name = ColTool,
                HeaderText = "Công cụ",
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            colTool.Items.Add(ShowcaseClipToolHelper.GetToolDisplayLabel(ShowcaseClipToolHelper.ToolVeo));
            colTool.Items.Add(ShowcaseClipToolHelper.GetToolDisplayLabel(ShowcaseClipToolHelper.ToolZoom));
            colTool.Items.Add(ShowcaseClipToolHelper.GetToolDisplayLabel(ShowcaseClipToolHelper.ToolKling));
            grid.Columns.Add(colTool);

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColPrompt,
                HeaderText = "Prompt",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True,
                    Font = new Font("Consolas", 10.5F)
                }
            });

            Form1.ApplyAppGridHeaderChrome(grid);
            AppGridSttColumn.EnsureFirstColumn(grid);

            PopulateHubGridRows(grid, scenes);

            grid.CellFormatting += HubGrid_CellFormatting;

            grid.CellBeginEdit += HubGrid_CellBeginEdit;
            grid.CellEndEdit += HubGrid_CellEndEdit;
            grid.DataError += HubGrid_DataError;
            grid.CellEndEdit += (_, __) => QueueHubGridRowResize();
            grid.ColumnWidthChanged += (_, __) => QueueHubGridRowResize();
            grid.Resize += (_, __) =>
            {
                ConfigureHubGridColumnWidths();
                QueueHubGridRowResize();
            };

            return grid;
        }

        private void PopulateHubGridRows(DataGridView grid, IList<AiVideoGenInputItem> scenes)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            grid.Rows.Clear();
            scenes = scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();

            if (scenes.Count == 0)
            {
                var rowIndex = grid.Rows.Add();
                var placeholder = grid.Rows[rowIndex];
                placeholder.Cells[ColScene].Value = "(Chưa có cảnh — thêm ảnh hoặc «Tạo kịch bản».)";
                placeholder.Cells[ColImage].Value = string.Empty;
                placeholder.Cells[ColVoice].Value = string.Empty;
                placeholder.Cells[ColTool].Value = string.Empty;
                placeholder.Cells[ColPrompt].Value = string.Empty;
                placeholder.ReadOnly = true;
                placeholder.DefaultCellStyle.ForeColor = Color.FromArgb(140, 148, 162);
                return;
            }

            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
                if (string.IsNullOrEmpty(tool))
                {
                    tool = ShowcaseClipToolHelper.ResolveDefaultTool(_video.ShowcaseClipModeId, scene.ShowcaseImageKind);
                }

                var isOpening = i == 0;
                var isClosing = i == scenes.Count - 1;
                var sceneLabel = FormatHubSceneLabel(scene, i, scenes.Count);
                var imageName = ResolveSceneImageFileName(scene);
                var voice = ResolveHubSceneVoice(scene, i, scenes.Count);
                var prompt = FormatPromptForCell(ShowcaseClipToolHelper.ResolveScenePrompt(scene));
                var toolLabel = ShowcaseClipToolHelper.GetToolDisplayLabel(tool);

                AddHubRow(grid, sceneLabel, imageName, voice, toolLabel, prompt,
                    new HubGridRowTag
                    {
                        Scene = scene,
                        IsOpeningHookScene = isOpening,
                        IsClosingCtaScene = isClosing
                    });
            }
        }

        private void ReloadHubGridFromVideo()
        {
            if (_dgvHub == null || _dgvHub.IsDisposed)
            {
                return;
            }

            _txtTheme.Text = _video.ShowcaseTheme ?? string.Empty;
            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            PopulateHubGridRows(_dgvHub, scenes);
            _speechBeforeEdit = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(_video);
            LayoutHubGrid();
        }

        private async Task TryRegenerateScriptAsync()
        {
            if (_regenerateScriptAsync == null || _btnRegenerateScript == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "Gemini sẽ xem lại ảnh và ghi đè lời thoại, prompt clip, Hook/CTA (và gợi ý nhạc/SFX nếu có) trên dòng video này.\r\n\r\nTiếp tục?",
                "Tạo lại kịch bản",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            _video.ShowcaseTheme = _txtTheme.Text?.Trim() ?? string.Empty;

            _btnRegenerateScript.Enabled = false;
            UseWaitCursor = true;
            try
            {
                var ok = await _regenerateScriptAsync().ConfigureAwait(true);
                if (ok)
                {
                    ReloadHubGridFromVideo();
                }
            }
            finally
            {
                UseWaitCursor = false;
                if (_btnRegenerateScript != null && !_btnRegenerateScript.IsDisposed)
                {
                    _btnRegenerateScript.Enabled = true;
                }
            }
        }

        private Control BuildBottomBar()
        {
            var bottomBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                BackColor = Bg,
                Padding = new Padding(0, 8, 0, 18)
            };
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var flpExcel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Bg,
                Padding = new Padding(4, 0, 8, 0)
            };
            if (_regenerateScriptAsync != null)
            {
                _btnRegenerateScript = MakeButton("Tạo lại kịch bản", ShowcasePastelTheme.ScriptHeader, 252);
                _btnRegenerateScript.Click += async (_, __) => await TryRegenerateScriptAsync().ConfigureAwait(true);
                flpExcel.Controls.Add(_btnRegenerateScript);
            }

            if (_exportExcelAsync != null)
            {
                var btnExcel = MakeButton("Tải excel prompt", ShowcasePastelTheme.ButtonExcel, 232);
                btnExcel.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    btnExcel.Enabled = false;
                    try
                    {
                        await _exportExcelAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        if (!btnExcel.IsDisposed)
                        {
                            btnExcel.Enabled = true;
                        }
                    }
                };
                flpExcel.Controls.Add(btnExcel);
            }

            var btnSave = MakeButton("Lưu", ShowcasePastelTheme.ButtonSave);
            btnSave.DialogResult = DialogResult.OK;
            AcceptButton = btnSave;
            var btnCancel = MakeButton("Hủy", ShowcasePastelTheme.ButtonCancel);
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            btnSave.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };

            var flpRight = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Bg,
                Padding = new Padding(0, 0, 8, 6),
                MinimumSize = new Size(316, 52),
                Margin = new Padding(0)
            };
            flpRight.Controls.Add(btnCancel);
            flpRight.Controls.Add(btnSave);

            var rightHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                MinimumSize = new Size(320, 52),
                Padding = new Padding(0, 0, 0, 4)
            };
            flpRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            rightHost.Controls.Add(flpRight);
            rightHost.Resize += (_, __) =>
            {
                if (rightHost.IsDisposed || flpRight.IsDisposed)
                {
                    return;
                }

                flpRight.Top = 0;
                flpRight.Left = Math.Max(0, rightHost.ClientSize.Width - flpRight.Width);
            };
            rightHost.HandleCreated += (_, __) => rightHost.PerformLayout();

            var midSpacer = new Panel { Dock = DockStyle.Fill, BackColor = Bg };

            bottomBar.Controls.Add(flpExcel, 0, 0);
            bottomBar.Controls.Add(midSpacer, 1, 0);
            bottomBar.Controls.Add(rightHost, 2, 0);
            return bottomBar;
        }

        private static string FormatHubSceneLabel(AiVideoGenInputItem scene, int index, int sceneCount)
        {
            var core = FormatSceneLabel(scene, index);
            var isOpening = index == 0;
            var isClosing = sceneCount > 0 && index == sceneCount - 1;
            if (isOpening && isClosing)
            {
                return "Hook · CTA · " + core;
            }

            if (isOpening)
            {
                return "Hook · " + core;
            }

            if (isClosing)
            {
                return "CTA · " + core;
            }

            return core;
        }

        private string ResolveHubSceneVoice(AiVideoGenInputItem scene, int sceneIndex, int sceneCount)
        {
            var voice = (scene?.SceneVoiceover ?? string.Empty).Trim();
            var hook = (_video.ShowcaseHookText ?? string.Empty).Trim();
            var cta = (_video.ShowcaseCtaText ?? string.Empty).Trim();
            var isOpening = sceneIndex == 0;
            var isClosing = sceneCount > 0 && sceneIndex == sceneCount - 1;

            if (isOpening && isClosing)
            {
                if (hook.Length > 0)
                {
                    return hook;
                }

                if (cta.Length > 0)
                {
                    return cta;
                }

                return voice;
            }

            if (isOpening && hook.Length > 0)
            {
                return hook;
            }

            if (isClosing && cta.Length > 0)
            {
                return cta;
            }

            return voice;
        }

        private static string FormatSceneLabel(AiVideoGenInputItem scene, int index)
        {
            var role = string.IsNullOrWhiteSpace(scene?.SceneRole) ? string.Empty : scene.SceneRole.Trim();
            var title = string.IsNullOrWhiteSpace(scene?.SceneTitle)
                ? "Cảnh " + (index + 1)
                : scene.SceneTitle.Trim();
            return role.Length > 0 ? "[" + role + "] " + title : title;
        }

        private static string FormatPromptForCell(string raw) =>
            ShowcasePromptTextHelper.StripDurationClauses(raw ?? string.Empty);

        private static string ResolveSceneImageFileName(AiVideoGenInputItem scene)
        {
            var fullPath = ResolveSceneImageFullPath(scene);
            if (fullPath.Length == 0)
            {
                return string.Empty;
            }

            return Path.GetFileName(fullPath);
        }

        private static string ResolveSceneImageFullPath(AiVideoGenInputItem scene)
        {
            if (scene == null)
            {
                return string.Empty;
            }

            foreach (var raw in new[]
                     {
                         scene.ThumbnailPath,
                         scene.ShowcaseLocalPickPath,
                         scene.ImageUrl
                     })
            {
                var p = (raw ?? string.Empty).Trim();
                if (p.Length == 0)
                {
                    continue;
                }

                if (p.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return new Uri(p).LocalPath;
                    }
                    catch
                    {
                        return p;
                    }
                }

                return p;
            }

            return string.Empty;
        }

        private static void AddHubRow(
            DataGridView grid,
            string sceneLabel,
            string imageName,
            string voice,
            string toolLabel,
            string prompt,
            HubGridRowTag tag)
        {
            var rowIndex = grid.Rows.Add();
            var row = grid.Rows[rowIndex];
            row.Cells[ColScene].Value = sceneLabel ?? string.Empty;
            row.Cells[ColImage].Value = imageName ?? string.Empty;
            row.Cells[ColVoice].Value = voice ?? string.Empty;
            row.Cells[ColTool].Value = toolLabel ?? string.Empty;
            row.Cells[ColPrompt].Value = prompt ?? string.Empty;
            row.Tag = tag;

            if (tag?.Scene == null)
            {
                row.Cells[ColImage].ReadOnly = true;
                row.Cells[ColImage].Style.BackColor = Color.FromArgb(52, 56, 68);
                row.Cells[ColTool].ReadOnly = true;
                row.Cells[ColPrompt].ReadOnly = true;
                row.Cells[ColTool].Style.BackColor = Color.FromArgb(52, 56, 68);
                row.Cells[ColPrompt].Style.BackColor = Color.FromArgb(52, 56, 68);
            }
        }

        private void HubGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_dgvHub == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_dgvHub.Columns[e.ColumnIndex].Name != ColImage)
            {
                if (_dgvHub.Columns[e.ColumnIndex].Name == ColTool)
                {
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                return;
            }

            var row = _dgvHub.Rows[e.RowIndex];
            if (row.Tag is HubGridRowTag tag && tag.Scene != null)
            {
                var full = ResolveSceneImageFullPath(tag.Scene);
                row.Cells[e.ColumnIndex].ToolTipText = full.Length > 0 ? full : "(chưa có ảnh)";
            }
        }

        private void HubGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (_dgvHub == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_dgvHub.Columns[e.ColumnIndex].Name != ColTool)
            {
                return;
            }

            _toolEditPrevious = _dgvHub.Rows[e.RowIndex].Cells[ColTool].Value?.ToString() ?? string.Empty;
        }

        private void HubGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_dgvHub == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var row = _dgvHub.Rows[e.RowIndex];
            if (!(row.Tag is HubGridRowTag tag) || tag.Scene == null)
            {
                return;
            }

            if (_dgvHub.Columns[e.ColumnIndex].Name == ColTool)
            {
                var prevTool = ToolLabelToId(_toolEditPrevious);
                var promptText = row.Cells[ColPrompt].Value?.ToString() ?? string.Empty;
                WritePromptToScene(tag.Scene, prevTool, promptText, Math.Max(0, _video.Scenes?.IndexOf(tag.Scene) ?? 0));

                var newTool = ToolLabelToId(row.Cells[ColTool].Value?.ToString());
                tag.Scene.ShowcaseClipTool = newTool;
                row.Cells[ColPrompt].Value = FormatPromptForCell(ShowcaseClipToolHelper.ResolveScenePrompt(tag.Scene));
            }
        }

        private static void HubGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.ColumnIndex < 0)
            {
                return;
            }

            var grid = sender as DataGridView;
            if (grid?.Columns[e.ColumnIndex].Name == ColTool)
            {
                e.ThrowException = false;
                e.Cancel = true;
            }
        }

        private void LayoutHubGrid()
        {
            ConfigureHubGridColumnWidths();
            ResizeHubGridRows();
        }

        private void QueueHubGridRowResize()
        {
            if (_dgvHub == null || _dgvHub.IsDisposed || IsHubGridLayoutLocked(_dgvHub))
            {
                return;
            }

            _dgvHub.BeginInvoke(new Action(ResizeHubGridRows));
        }

        private static bool IsHubGridLayoutLocked(DataGridView grid) =>
            string.Equals(grid?.Tag as string, HubGridLayoutLock, StringComparison.Ordinal);

        private void ConfigureHubGridColumnWidths()
        {
            if (_dgvHub == null || _dgvHub.IsDisposed || _dgvHub.Columns.Count == 0)
            {
                return;
            }

            var gridW = Math.Max(480, _dgvHub.ClientSize.Width);
            if (_dgvHub.DisplayedRowCount(false) < _dgvHub.RowCount)
            {
                gridW = Math.Max(480, gridW - SystemInformation.VerticalScrollBarWidth);
            }

            gridW = Math.Max(400, gridW - HubGridRightEdgeInset);
            var sttW = AppGridSttColumn.ColumnWidth;
            var remain = Math.Max(320, gridW - sttW);

            int Pct(int min, float pct, int max = int.MaxValue) =>
                Math.Min(max, Math.Max(min, (int)Math.Round(remain * pct)));

            var wScene = Pct(112, HubGridPctScene, 380);
            var wImage = Pct(96, HubGridPctImage, 280);
            var wVoice = Pct(152, HubGridPctVoice, 640);
            var wTool = Pct(100, HubGridPctTool, 200);
            var wPrompt = Math.Max(160, remain - wScene - wImage - wVoice - wTool);

            var prevTag = _dgvHub.Tag;
            _dgvHub.Tag = HubGridLayoutLock;
            try
            {
                SetHubColWidth(ColScene, wScene);
                SetHubColWidth(ColImage, wImage);
                SetHubColWidth(ColVoice, wVoice);
                SetHubColWidth(ColTool, wTool);
                SetHubColWidth(ColPrompt, wPrompt);
            }
            finally
            {
                _dgvHub.Tag = prevTag;
            }
        }

        private void SetHubColWidth(string name, int width)
        {
            if (_dgvHub.Columns[name] is DataGridViewColumn col)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                col.MinimumWidth = Math.Min(width, 80);
                col.Width = width;
            }
        }

        private void ResizeHubGridRows()
        {
            if (_dgvHub == null || _dgvHub.IsDisposed)
            {
                return;
            }

            foreach (DataGridViewRow row in _dgvHub.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.MinimumHeight = HubGridMinRowHeight;
                var hScene = MeasureCellRowHeight(row, ColScene);
                var hImage = MeasureCellRowHeight(row, ColImage);
                var hVoice = MeasureCellRowHeight(row, ColVoice);
                var hPrompt = MeasureCellRowHeight(row, ColPrompt);
                row.Height = Math.Max(HubGridMinRowHeight, Math.Max(Math.Max(hScene, hImage), Math.Max(hVoice, hPrompt)));
            }
        }

        private int MeasureCellRowHeight(DataGridViewRow row, string colName)
        {
            if (!_dgvHub.Columns.Contains(colName))
            {
                return HubGridMinRowHeight;
            }

            var cell = row.Cells[colName];
            var col = _dgvHub.Columns[colName];
            var colWidth = col.Displayed ? col.Width : col.MinimumWidth;
            if (colWidth < 48)
            {
                return HubGridMinRowHeight;
            }

            var font = cell.InheritedStyle.Font ?? _dgvHub.DefaultCellStyle.Font ?? _dgvHub.Font;
            var text = cell.Value?.ToString() ?? string.Empty;
            if (text.Length == 0)
            {
                return HubGridMinRowHeight;
            }

            var measureWidth = Math.Max(48, colWidth - HubGridCellPad);
            var size = TextRenderer.MeasureText(
                text,
                font,
                new Size(measureWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            return Math.Max(HubGridMinRowHeight, size.Height + HubGridCellPad);
        }

        private bool ValidateAndSave()
        {
            _video.ShowcaseTheme = _txtTheme.Text?.Trim() ?? string.Empty;
            _video.ShowcaseHookText = string.Empty;
            _video.ShowcaseCtaText = string.Empty;

            if (_dgvHub != null && !_dgvHub.IsDisposed)
            {
                foreach (DataGridViewRow row in _dgvHub.Rows)
                {
                    if (row.IsNewRow || !(row.Tag is HubGridRowTag tag))
                    {
                        continue;
                    }

                    var voice = (row.Cells[ColVoice].Value?.ToString() ?? string.Empty).Trim();
                    if (tag.Scene == null)
                    {
                        continue;
                    }

                    tag.Scene.SceneVoiceover = voice;
                    tag.Scene.ShowcaseTheme = _video.ShowcaseTheme;
                    if (tag.IsOpeningHookScene)
                    {
                        _video.ShowcaseHookText = voice;
                    }

                    if (tag.IsClosingCtaScene)
                    {
                        _video.ShowcaseCtaText = voice;
                    }

                    var tool = ToolLabelToId(row.Cells[ColTool].Value?.ToString());
                    if (string.IsNullOrEmpty(tool))
                    {
                        tool = ShowcaseClipToolHelper.ResolveDefaultTool(
                            _video.ShowcaseClipModeId,
                            tag.Scene.ShowcaseImageKind);
                    }

                    tag.Scene.ShowcaseClipTool = tool;
                    var prompt = row.Cells[ColPrompt].Value?.ToString() ?? string.Empty;
                    var sceneIndex = _video.Scenes?.IndexOf(tag.Scene) ?? 0;
                    if (sceneIndex < 0)
                    {
                        sceneIndex = 0;
                    }

                    WritePromptToScene(tag.Scene, tool, prompt, sceneIndex);
                }
            }

            _video.ApplySettingsToScenes();
            ShowcaseSubtitleDisplayHelper.SyncDisplayTextFromSpeechEdits(_video, _speechBeforeEdit);
            ShowcaseContentDisplayHelper.RefreshContentLabels(_video);
            return true;
        }

        private static string ToolLabelToId(string label)
        {
            var t = (label ?? string.Empty).Trim();
            if (t.StartsWith("Kling", StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseClipToolHelper.ToolKling;
            }

            if (t.StartsWith("Zoom", StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseClipToolHelper.ToolZoom;
            }

            if (t.StartsWith("Veo", StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseClipToolHelper.ToolVeo;
            }

            return ShowcaseClipToolHelper.NormalizeClipTool(t);
        }

        private static void WritePromptToScene(AiVideoGenInputItem scene, string tool, string prompt, int sceneIndex)
        {
            if (scene == null)
            {
                return;
            }

            tool = ShowcaseClipToolHelper.NormalizeClipTool(tool);
            prompt = (prompt ?? string.Empty).Trim();
            switch (tool)
            {
                case ShowcaseClipToolHelper.ToolKling:
                    scene.KlingPrompt = ShowcaseKlingPromptSanitizer.Sanitize(prompt, sceneIndex);
                    break;
                case ShowcaseClipToolHelper.ToolZoom:
                    scene.ZoomHint = ShowcasePromptTextHelper.StripDurationClauses(prompt);
                    break;
                default:
                    scene.VeoPrompt = ShowcaseVeoPromptSanitizer.Sanitize(prompt, sceneIndex);
                    break;
            }
        }

        private static Button MakeButton(string text, Color back, int width = 148)
        {
            var font = new Font("Segoe UI", 10F, FontStyle.Bold);
            var minW = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 36;
            var w = Math.Max(width, minW);
            return new Button
            {
                Text = text,
                Size = new Size(w, 48),
                MinimumSize = new Size(w, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = font,
                Margin = new Padding(8, 0, 0, 0),
                UseVisualStyleBackColor = false
            };
        }
    }
}
