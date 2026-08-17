using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseScriptEditorForm : Form
    {
        private const string ColScene = "colScriptScene";
        private const string ColVoiceover = "colScriptVoiceover";
        private const int ScriptGridMinHeight = 480;
        private const int ScriptGridMinRowHeight = 64;
        private const int ScriptVoiceCellPad = 24;
        private const string ScriptGridLayoutLock = "ScriptGridLayoutLock";

        private enum ScriptGridRowKind
        {
            Hook,
            Scene,
            Cta
        }

        private sealed class ScriptGridRowTag
        {
            public ScriptGridRowKind Kind { get; set; }

            public AiVideoGenInputItem Scene { get; set; }
        }

        private readonly ShowcaseVideoItem _video;
        private readonly ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot _speechBeforeEdit;
        private TextBox _txtTheme;
        private DataGridView _dgvScenes;

        public ShowcaseScriptEditorForm(ShowcaseVideoItem video)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _speechBeforeEdit = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(_video);

            Text = "Kịch bản — " + ((_video.ProductName ?? string.Empty).Trim().Length > 0
                ? _video.ProductName.Trim()
                : "Showcase");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(1380, 920);
            MinimumSize = new Size(1140, 720);
            Padding = new Padding(24);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(0, 6, 0, 0)
            };
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var themePanel = BuildThemeFieldPanel();
            themePanel.Dock = DockStyle.Fill;
            body.Controls.Add(themePanel, 0, 0);

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            var gridSection = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                MinimumSize = new Size(0, ScriptGridMinHeight),
                Padding = new Padding(0, 20, 0, 0)
            };
            gridSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            gridSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var gridCaption = new Label
            {
                Text = "Thoại · Hook · CTA · từng cảnh (" + (scenes.Count + 2) + " dòng)",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 14),
                Padding = new Padding(0, 0, 0, 4)
            };

            _dgvScenes = BuildSceneGrid(scenes);
            _dgvScenes.Dock = DockStyle.Fill;
            _dgvScenes.Margin = new Padding(0, 4, 0, 0);
            gridSection.Controls.Add(gridCaption, 0, 0);
            gridSection.Controls.Add(_dgvScenes, 0, 1);
            body.Controls.Add(gridSection, 0, 1);

            root.Controls.Add(body, 0, 0);

            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 8, 0, 8),
                Padding = new Padding(0, 4, 0, 4)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            root.Controls.Add(flpButtons, 0, 1);

            Controls.Add(root);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };

            Shown += (_, __) => BeginInvoke(new Action(LayoutScriptGrid));
        }

        private void LayoutScriptGrid()
        {
            if (_dgvScenes == null || _dgvScenes.IsDisposed)
            {
                return;
            }

            FitScriptSceneColumnWidth(_dgvScenes);
            ResizeScriptVoiceoverRows(_dgvScenes);
        }

        private void QueueScriptGridRowResize()
        {
            if (_dgvScenes == null || _dgvScenes.IsDisposed || IsScriptGridLayoutLocked(_dgvScenes))
            {
                return;
            }

            _dgvScenes.BeginInvoke(new Action(() =>
            {
                FitScriptSceneColumnWidth(_dgvScenes);
                ResizeScriptVoiceoverRows(_dgvScenes);
            }));
        }

        private Control BuildThemeFieldPanel()
        {
            const int themeFieldHeight = 44;

            var block = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(0, 0, 0, 6)
            };
            block.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            block.RowStyles.Add(new RowStyle(SizeType.Absolute, themeFieldHeight));

            block.Controls.Add(new Label
            {
                Text = "Chủ đề video",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(0, 0, 0, 2)
            }, 0, 0);

            _txtTheme = new TextBox
            {
                Text = _video.ShowcaseTheme ?? string.Empty,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 6),
                MinimumSize = new Size(0, themeFieldHeight - 4)
            };
            block.Controls.Add(_txtTheme, 0, 1);
            return block;
        }

        private DataGridView BuildSceneGrid(IList<AiVideoGenInputItem> scenes)
        {
            var grid = new DataGridView
            {
                BackgroundColor = Color.FromArgb(38, 42, 52),
                GridColor = Color.FromArgb(58, 64, 78),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ScrollBars = ScrollBars.Both,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    SelectionBackColor = Color.FromArgb(68, 118, 168),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 10.5F),
                    WrapMode = DataGridViewTriState.True,
                    Padding = new Padding(4, 6, 4, 6)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(52, 58, 72),
                    ForeColor = Color.Gainsboro,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColScene,
                HeaderText = "Phân cảnh",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Tag = "SkipHeaderWidth",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True
                }
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColVoiceover,
                HeaderText = "Lời thoại",
                ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100F,
                MinimumWidth = 120,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True
                }
            });

            Form1.ApplyAppGridHeaderChrome(grid);
            AppGridSttColumn.EnsureFirstColumn(grid);
            grid.RowTemplate.MinimumHeight = Math.Max(grid.RowTemplate.MinimumHeight, ScriptGridMinRowHeight);

            AddGridRow(grid, "Hook mở đầu", _video.ShowcaseHookText ?? string.Empty, new ScriptGridRowTag { Kind = ScriptGridRowKind.Hook });

            if (scenes.Count == 0)
            {
                var rowIndex = grid.Rows.Add();
                var placeholder = grid.Rows[rowIndex];
                placeholder.Cells[ColScene].Value = "(Chưa có cảnh — thêm ảnh trên storyboard trước.)";
                placeholder.Cells[ColVoiceover].Value = string.Empty;
                placeholder.ReadOnly = true;
                placeholder.DefaultCellStyle.ForeColor = Color.FromArgb(140, 148, 162);
            }
            else
            {
                for (var i = 0; i < scenes.Count; i++)
                {
                    var scene = scenes[i];
                    ResolveSceneRowContent(scene, i, out var sceneName, out var voice);
                    AddGridRow(
                        grid,
                        sceneName,
                        voice,
                        new ScriptGridRowTag { Kind = ScriptGridRowKind.Scene, Scene = scene });
                }
            }

            AddGridRow(grid, "CTA kết thúc", _video.ShowcaseCtaText ?? string.Empty, new ScriptGridRowTag { Kind = ScriptGridRowKind.Cta });

            grid.CellEndEdit += (_, __) => QueueScriptGridRowResize();
            grid.ColumnWidthChanged += (_, __) => QueueScriptGridRowResize();
            grid.SizeChanged += (_, __) => QueueScriptGridRowResize();

            return grid;
        }

        private static bool IsScriptGridLayoutLocked(DataGridView grid) =>
            string.Equals(grid?.Tag as string, ScriptGridLayoutLock, StringComparison.Ordinal);

        /// <summary>Giãn chiều cao dòng theo wrap cột Phân cảnh và Lời thoại.</summary>
        private static void ResizeScriptVoiceoverRows(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed || IsScriptGridLayoutLocked(grid)
                || !grid.Columns.Contains(ColVoiceover))
            {
                return;
            }

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.MinimumHeight = ScriptGridMinRowHeight;
                var hScene = MeasureScriptGridCellHeight(grid, row, ColScene);
                var hVoice = MeasureScriptGridCellHeight(grid, row, ColVoiceover);
                row.Height = Math.Max(ScriptGridMinRowHeight, Math.Max(hScene, hVoice));
            }
        }

        private static int MeasureScriptGridCellHeight(DataGridView grid, DataGridViewRow row, string colName)
        {
            if (!grid.Columns.Contains(colName))
            {
                return ScriptGridMinRowHeight;
            }

            var col = grid.Columns[colName];
            var colWidth = col.Displayed ? col.Width : col.MinimumWidth;
            if (colWidth < 48)
            {
                colWidth = colName == ColScene ? 200 : 400;
            }

            var measureWidth = Math.Max(48, colWidth - ScriptVoiceCellPad);
            var cell = row.Cells[colName];
            var style = cell.InheritedStyle;
            var font = style.Font ?? grid.DefaultCellStyle.Font ?? grid.Font;
            var text = cell.Value?.ToString() ?? string.Empty;
            if (text.Length == 0)
            {
                return ScriptGridMinRowHeight;
            }

            var pad = style.Padding;
            measureWidth = Math.Max(48, colWidth - pad.Horizontal - ScriptVoiceCellPad);
            var size = TextRenderer.MeasureText(
                text,
                font,
                new Size(measureWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            return Math.Max(ScriptGridMinRowHeight, size.Height + pad.Vertical + ScriptVoiceCellPad);
        }

        /// <summary>Cột Phân cảnh co theo nội dung; Lời thoại chiếm phần còn lại.</summary>
        private static void FitScriptSceneColumnWidth(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed || !grid.Columns.Contains(ColScene))
            {
                return;
            }

            var sceneCol = grid.Columns[ColScene];
            var font = sceneCol.DefaultCellStyle.Font ?? grid.DefaultCellStyle.Font ?? grid.Font;
            const int horizontalPad = 28;
            const int minWidth = 96;
            const int maxWidth = 280;

            var maxText = TextRenderer.MeasureText(
                sceneCol.HeaderText ?? "Phân cảnh",
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding).Width;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var text = row.Cells[ColScene].Value?.ToString() ?? string.Empty;
                if (text.Length == 0)
                {
                    continue;
                }

                var w = TextRenderer.MeasureText(
                    text,
                    font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding).Width;
                if (w > maxText)
                {
                    maxText = w;
                }
            }

            var width = Math.Min(maxWidth, Math.Max(minWidth, maxText + horizontalPad));

            var prevTag = grid.Tag;
            grid.Tag = ScriptGridLayoutLock;
            try
            {
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                sceneCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                sceneCol.Width = width;
                sceneCol.MinimumWidth = width;
                sceneCol.FillWeight = 1f;

                if (grid.Columns.Contains(ColVoiceover))
                {
                    var voiceCol = grid.Columns[ColVoiceover];
                    voiceCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    voiceCol.FillWeight = 100f;
                    voiceCol.MinimumWidth = 120;
                }
            }
            finally
            {
                grid.Tag = prevTag;
            }
        }

        private static void AddGridRow(DataGridView grid, string sceneName, string voiceover, ScriptGridRowTag tag)
        {
            var rowIndex = grid.Rows.Add();
            var row = grid.Rows[rowIndex];
            row.Cells[ColScene].Value = sceneName ?? string.Empty;
            row.Cells[ColVoiceover].Value = voiceover ?? string.Empty;
            row.Tag = tag;
        }

        /// <summary>Tách tên cảnh và lời thoại — tránh gán nhầm khi thoại nằm trong SceneTitle.</summary>
        private static void ResolveSceneRowContent(
            AiVideoGenInputItem scene,
            int index,
            out string sceneName,
            out string voice)
        {
            voice = (scene?.SceneVoiceover ?? string.Empty).Trim();
            var title = (scene?.SceneTitle ?? string.Empty).Trim();
            var role = (scene?.SceneRole ?? string.Empty).Trim();

            if (voice.Length > 0)
            {
                sceneName = ShowcaseSceneNamingHelper.FormatDisplayLabel(scene, index);
                return;
            }

            if (title.Length > 0)
            {
                voice = title;
                sceneName = ShowcaseSceneNamingHelper.FormatDisplayLabel(scene, index);
                return;
            }

            sceneName = ShowcaseSceneNamingHelper.FormatDisplayLabel(scene, index);
        }

        private bool ValidateAndSave()
        {
            _video.ShowcaseTheme = _txtTheme.Text?.Trim() ?? string.Empty;
            _video.ShowcaseHookText = string.Empty;
            _video.ShowcaseCtaText = string.Empty;

            if (_dgvScenes != null && !_dgvScenes.IsDisposed)
            {
                foreach (DataGridViewRow row in _dgvScenes.Rows)
                {
                    if (row.IsNewRow || !(row.Tag is ScriptGridRowTag tag))
                    {
                        continue;
                    }

                    var voice = (row.Cells[ColVoiceover].Value?.ToString() ?? string.Empty).Trim();
                    switch (tag.Kind)
                    {
                        case ScriptGridRowKind.Hook:
                            _video.ShowcaseHookText = voice;
                            break;
                        case ScriptGridRowKind.Cta:
                            _video.ShowcaseCtaText = voice;
                            break;
                        case ScriptGridRowKind.Scene when tag.Scene != null:
                            tag.Scene.SceneVoiceover = voice;
                            tag.Scene.ShowcaseTheme = _video.ShowcaseTheme;
                            if (voice.Length > 0)
                            {
                                tag.Scene.ShowcaseSceneSilent = false;
                            }
                            break;
                    }
                }
            }

            _video.ApplySettingsToScenes();
            ShowcaseVoiceoverHelper.PersistHookCtaIntoSceneVoiceovers(_video, _video.Scenes);
            ShowcaseVoiceoverHelper.SyncSilentFlagsFromVoiceover(_video.Scenes);
            ShowcaseSubtitleDisplayHelper.SyncDisplayTextFromSpeechEdits(_video, _speechBeforeEdit);
            ShowcaseContentDisplayHelper.RefreshContentLabels(_video);
            return true;
        }

        private static Button CreateButton(string text, Color back) => new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(118, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(8, 4, 8, 4)
        };
    }
}
