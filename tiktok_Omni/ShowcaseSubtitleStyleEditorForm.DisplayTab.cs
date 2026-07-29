using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseSubtitleStyleEditorForm
    {
        private const int DisplayGridRightEdgeInset = 16;

        private Font _displayTabCellFont;
        private Font _displayTabHeaderFont;
        private int _displayTabControlHeight;
        private int _displayTabComboItemHeight;
        private int _displayTabHeaderHeight;
        private int _displayTabRowMinHeight;
        private int _displayTabUiInset;

        private const string StyleFaceRegular = "Thường";
        private const string StyleFaceBold = "Đậm";
        private const string StyleFaceItalic = "Nghiêng";

        private Panel _displayTabHost;
        private DataGridView _dgvDisplay;
        private string _hookSourceSpeech = string.Empty;
        private readonly Dictionary<string, Font> _subtitlePreviewFontCache = new Dictionary<string, Font>(StringComparer.Ordinal);

        private enum DisplayGridRowRole
        {
            Hook,
            Scene
        }

        private sealed class DisplayGridRowTag
        {
            public DisplayGridRowRole Role { get; set; }

            public AiVideoGenInputItem Scene { get; set; }

            public string SourceSpeech { get; set; } = string.Empty;

            public ShowcaseDisplayLineEffectKind EffectKind { get; set; }

            /// <summary>Cảnh cuối trong lưới = CTA (cùng <see cref="ShowcaseVideoItem.ShowcaseCtaText"/>).</summary>
            public bool IsFinalCtaScene { get; set; }
        }

        private void BuildDisplayTabUi()
        {
            if (_displayTabHost == null)
            {
                return;
            }

            _displayTabHost.Controls.Clear();
            RefreshDisplayTabLayoutMetrics();

            _dgvDisplay = CreateDisplayGridShell();
            ApplyDisplayGridColumnHeaderStyle();
            var templateBar = BuildSubtitleTemplateBar();
            _displayTabHost.Controls.Add(_dgvDisplay);
            _displayTabHost.Controls.Add(templateBar);

            PopulateDisplayGridRows();
        }

        private DataGridView CreateDisplayGridShell()
        {
            var grid = new DataGridView
            {
                Name = "dgvShowcaseSubtitleDisplay",
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                GridColor = Color.FromArgb(58, 64, 78),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ShowCellToolTips = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = _displayTabHeaderHeight,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    SelectionBackColor = Color.FromArgb(56, 120, 82),
                    SelectionForeColor = Color.White,
                    Font = _displayTabCellFont,
                    WrapMode = DataGridViewTriState.True,
                    Padding = new Padding(4, (int)Math.Max(4, Math.Round(Font.Size * 0.45)), 4, (int)Math.Max(4, Math.Round(Font.Size * 0.45)))
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(42, 52, 78),
                    ForeColor = Color.FromArgb(190, 215, 255),
                    Font = _displayTabHeaderFont,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True
                }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDisplayScene",
                HeaderText = "Phân cảnh",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.TopLeft,
                    WrapMode = DataGridViewTriState.True
                }
            });
            var colSubtitle = new DataGridViewTextBoxColumn
            {
                Name = "colDisplaySubtitle",
                HeaderText = "Phụ đề",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colSubtitle.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            grid.Columns.Add(colSubtitle);
            grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "colDisplayEffect",
                HeaderText = "Hiệu ứng",
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colStyleEnabled",
                HeaderText = "Bật",
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            var colLook = new DataGridViewComboBoxColumn
            {
                Name = "colStyleLook",
                HeaderText = "Kiểu chữ",
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            ShowcaseSubtitleLookPresetCatalog.PopulateComboColumn(colLook, ShowcaseDisplayLineEffectKind.Body);
            grid.Columns.Add(colLook);

            var colHighlight = new DataGridViewComboBoxColumn
            {
                Name = "colStyleHighlight",
                HeaderText = "Nền dòng",
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            ShowcaseSubtitleHighlightColourCatalog.PopulateComboColumn(colHighlight);
            grid.Columns.Add(colHighlight);

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStyleFontSize",
                HeaderText = "Cỡ",
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns["colStyleFontSize"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            var colPos = new DataGridViewComboBoxColumn
            {
                Name = "colStylePosition",
                HeaderText = "Vị trí",
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colPos.Items.AddRange(new object[] { "Dưới", "Giữa", "Trên" });
            grid.Columns.Add(colPos);

            grid.RowTemplate.MinimumHeight = _displayTabRowMinHeight;

            grid.DataError += DisplayGrid_DataError;
            grid.CellFormatting += DisplayGrid_CellFormatting;
            grid.CellValueChanged += DisplayGrid_CellValueChanged;
            grid.SelectionChanged += DisplayGrid_SelectionChanged;
            grid.EditingControlShowing += DisplayGrid_EditingControlShowing;
            grid.Resize += (_, __) => ConfigureDisplayGridColumnWidths();
            grid.Disposed += (_, __) => ClearSubtitlePreviewFontCache();

            return grid;
        }

        private void DisplayGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (_dgvDisplay == null || !_dgvDisplay.Columns.Contains("colDisplaySubtitle"))
            {
                return;
            }

            _dgvDisplay.InvalidateColumn(_dgvDisplay.Columns["colDisplaySubtitle"].Index);
        }

        private void DisplayGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_dgvDisplay == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvDisplay.Columns[e.ColumnIndex].Name;
            if (IsSubtitlePreviewStyleColumn(colName))
            {
                ClearSubtitlePreviewFontCache();
                _dgvDisplay.InvalidateColumn(_dgvDisplay.Columns["colDisplaySubtitle"].Index);
            }
        }

        private static bool IsSubtitlePreviewStyleColumn(string colName)
        {
            return string.Equals(colName, "colDisplaySubtitle", StringComparison.Ordinal)
                   || string.Equals(colName, "colStyleLook", StringComparison.Ordinal)
                   || string.Equals(colName, "colStyleHighlight", StringComparison.Ordinal);
        }

        private void ClearSubtitlePreviewFontCache()
        {
            foreach (var font in _subtitlePreviewFontCache.Values)
            {
                font?.Dispose();
            }

            _subtitlePreviewFontCache.Clear();
        }

        private void ConfigureDisplayGridColumnWidths()
        {
            if (_dgvDisplay == null || _dgvDisplay.IsDisposed || _dgvDisplay.Columns.Count < 3)
            {
                return;
            }

            var client = _dgvDisplay.ClientSize.Width - DisplayGridRightEdgeInset;
            if (client < 400)
            {
                return;
            }

            const float lookWeight = 16f * 0.7f;
            var weights = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["colDisplayScene"] = 14f,
                ["colDisplaySubtitle"] = 20f + (16f - lookWeight),
                ["colDisplayEffect"] = 12f,
                ["colStyleEnabled"] = 4f,
                ["colStyleLook"] = lookWeight,
                ["colStyleHighlight"] = 10f,
                ["colStyleFontSize"] = 5f,
                ["colStylePosition"] = 6f
            };

            var total = weights.Values.Sum();
            _dgvDisplay.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            foreach (DataGridViewColumn col in _dgvDisplay.Columns)
            {
                if (!weights.TryGetValue(col.Name, out var w))
                {
                    continue;
                }

                col.Width = Math.Max(36, (int)(client * (w / total)));
            }

            _dgvDisplay.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
        }

        private void PopulateDisplayGridRows()
        {
            if (_dgvDisplay == null)
            {
                return;
            }

            _dgvDisplay.Rows.Clear();

            _hookSourceSpeech = ShowcaseSubtitleDisplayHelper.ResolveHookSpeechSource(_video);
            AddDisplayGridRow(
                "Hook (mở đầu)",
                string.Empty,
                DisplayGridRowRole.Hook,
                null,
                _hookSourceSpeech,
                ShowcaseDisplayLineEffectKind.Hook);

            var scenes = (_video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>()).Where(s => s != null).ToList();
            var firstVoiced = ShowcaseSubtitleDisplayHelper.FindFirstVoicedSceneIndex(scenes);
            var ctaSpeech = (_video.ShowcaseCtaText ?? string.Empty).Trim();
            var sceneRows = new List<(AiVideoGenInputItem Scene, string Title, string SourceSpeech)>();
            var order = 0;
            foreach (var scene in scenes)
            {
                order++;
                if (scene.ShowcaseSceneSilent || string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    continue;
                }

                if (order - 1 == firstVoiced)
                {
                    continue;
                }

                var title = "Cảnh " + order;
                var st = (scene.SceneTitle ?? string.Empty).Trim();
                if (st.Length > 0)
                {
                    title += " — " + st;
                }

                var speech = scene.SceneVoiceover.Trim();
                sceneRows.Add((scene, title, speech));
            }

            for (var i = 0; i < sceneRows.Count; i++)
            {
                var entry = sceneRows[i];
                var isFinalCta = i == sceneRows.Count - 1;
                var title = entry.Title;
                if (isFinalCta)
                {
                    title += " · CTA";
                }

                var sourceSpeech = isFinalCta && ctaSpeech.Length > 0 ? ctaSpeech : entry.SourceSpeech;
                AddDisplayGridRow(
                    title,
                    string.Empty,
                    DisplayGridRowRole.Scene,
                    entry.Scene,
                    sourceSpeech,
                    ShowcaseDisplayLineEffectKind.Body,
                    isFinalCta);
            }

            ConfigureDisplayGridColumnWidths();
        }

        private void AddDisplayGridRow(
            string sceneLabel,
            string subtitle,
            DisplayGridRowRole role,
            AiVideoGenInputItem scene,
            string sourceSpeech,
            ShowcaseDisplayLineEffectKind effectKind,
            bool isFinalCtaScene = false)
        {
            var defaultLook = ShowcaseSubtitleLookPresetCatalog.LabelFromStorage(
                ShowcaseSubtitleLookPresetCatalog.BodyDefaultStorage,
                ShowcaseDisplayLineEffectKind.Body);
            var idx = _dgvDisplay.Rows.Add(
                sceneLabel,
                subtitle,
                ShowcaseDisplayLineAnimationHelper.DefaultEffectLabel(effectKind),
                false,
                defaultLook,
                ShowcaseSubtitleHighlightColourCatalog.FollowLookLabel,
                "72",
                "Dưới");
            var row = _dgvDisplay.Rows[idx];
            row.Tag = new DisplayGridRowTag
            {
                Role = role,
                Scene = scene,
                SourceSpeech = sourceSpeech ?? string.Empty,
                EffectKind = effectKind,
                IsFinalCtaScene = isFinalCtaScene
            };

            ApplyDisplayRowCellAccess(row);

            var fxCell = row.Cells["colDisplayEffect"] as DataGridViewComboBoxCell;
            ConfigureDisplayEffectCell(fxCell, effectKind);
            ConfigureLookCell(row.Cells["colStyleLook"] as DataGridViewComboBoxCell, effectKind);
        }

        private static void ConfigureLookCell(DataGridViewComboBoxCell cell, ShowcaseDisplayLineEffectKind kind)
        {
            if (cell == null)
            {
                return;
            }

            cell.Items.Clear();
            foreach (var p in ShowcaseSubtitleLookPresetCatalog.AllForKind(kind))
            {
                cell.Items.Add(p.Label);
            }

            if (cell.Items.Count > 0)
            {
                cell.Value = cell.Items[0];
            }
        }

        private void ApplyDisplayRowCellAccess(DataGridViewRow row)
        {
            var tag = GetDisplayRowTag(row);
            if (tag == null)
            {
                return;
            }

            static void Ro(DataGridViewCell cell, bool readOnly)
            {
                if (cell != null)
                {
                    cell.ReadOnly = readOnly;
                }
            }

            Ro(row.Cells["colDisplayScene"], true);
            Ro(row.Cells["colDisplaySubtitle"], false);
            Ro(row.Cells["colDisplayEffect"], false);
            Ro(row.Cells["colStyleEnabled"], false);
            Ro(row.Cells["colStyleLook"], false);
            Ro(row.Cells["colStyleHighlight"], false);
            Ro(row.Cells["colStyleFontSize"], false);

            if (tag.Role == DisplayGridRowRole.Hook)
            {
                Ro(row.Cells["colStylePosition"], true);
            }
        }

        private void DisplayGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_dgvDisplay == null || e.RowIndex < 0 || e.RowIndex >= _dgvDisplay.Rows.Count)
            {
                return;
            }

            var tag = GetDisplayRowTag(_dgvDisplay.Rows[e.RowIndex]);
            if (tag == null)
            {
                return;
            }

            var col = _dgvDisplay.Columns[e.ColumnIndex].Name;
            if (string.Equals(col, "colDisplaySubtitle", StringComparison.Ordinal))
            {
                ApplySubtitleCellPreviewStyle(e, tag, _dgvDisplay.Rows[e.RowIndex]);
            }
        }

        private void ApplySubtitleCellPreviewStyle(DataGridViewCellFormattingEventArgs e, DisplayGridRowTag tag, DataGridViewRow row)
        {
            if (!TryReadSubtitlePreviewStyleFromRow(tag, row, out var fontName, out var bold, out var italic, out var primaryAss, out var lineBackgroundAss))
            {
                return;
            }

            e.CellStyle.Font = GetSubtitlePreviewFont(fontName, bold, italic);
            ApplySubtitleLineBackgroundPreview(e.CellStyle, primaryAss, lineBackgroundAss);
        }

        private void ApplySubtitleLineBackgroundPreview(DataGridViewCellStyle cellStyle, string primaryAss, string lineBackgroundAss)
        {
            if (cellStyle == null)
            {
                return;
            }

            var gridBack = _dgvDisplay?.DefaultCellStyle.BackColor ?? Color.FromArgb(45, 49, 60);
            var fore = ShowcaseSubtitleColourPresetCatalog.ToDrawingColor(
                string.IsNullOrWhiteSpace(primaryAss) ? "&H00FFFFFF" : primaryAss);
            cellStyle.ForeColor = fore;

            Color back;
            if (!string.IsNullOrWhiteSpace(lineBackgroundAss))
            {
                var blended = ShowcaseSubtitleHighlightColourCatalog.ToOpaquePreviewOnBackground(
                    lineBackgroundAss,
                    gridBack);
                back = blended != Color.Empty ? blended : gridBack;
            }
            else
            {
                back = gridBack;
            }

            cellStyle.BackColor = back;
            cellStyle.SelectionBackColor = back;
            cellStyle.SelectionForeColor = fore;
        }

        private bool TryReadSubtitlePreviewStyleFromRow(
            DisplayGridRowTag tag,
            DataGridViewRow row,
            out string fontName,
            out bool bold,
            out bool italic,
            out string primaryAss,
            out string lineBackgroundAss)
        {
            fontName = "Segoe UI";
            bold = tag.Role == DisplayGridRowRole.Hook;
            italic = false;
            primaryAss = tag.Role == DisplayGridRowRole.Hook ? "&H0000FFFF" : "&H00FFFFFF";
            lineBackgroundAss = string.Empty;

            if (row == null)
            {
                return true;
            }

            lineBackgroundAss = ShowcaseSubtitleHighlightColourCatalog.LineBackgroundAssFromLabel(
                row.Cells["colStyleHighlight"].Value?.ToString());

            var kind = tag.Role == DisplayGridRowRole.Hook
                ? ShowcaseDisplayLineEffectKind.Hook
                : ShowcaseDisplayLineEffectKind.Body;
            var storage = ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(
                row.Cells["colStyleLook"].Value?.ToString(),
                kind);
            var preset = ShowcaseSubtitleLookPresetCatalog.FindByStorage(storage, kind);
            if (preset != null)
            {
                fontName = preset.FontName;
                bold = preset.Bold;
                italic = preset.Italic;
                primaryAss = preset.PrimaryAss;
            }

            return true;
        }

        private Font GetSubtitlePreviewFont(string fontName, bool bold, bool italic)
        {
            var uiSize = _displayTabCellFont?.Size ?? Math.Max(9f, Font.Size);
            var family = fontName;
            var b = bold;
            var i = italic;
            ShowcaseSubtitleFontHelper.Normalize(ref family, ref b, ref i);

            var style = FontStyle.Regular;
            if (b)
            {
                style |= FontStyle.Bold;
            }

            if (i)
            {
                style |= FontStyle.Italic;
            }

            var key = family + "|" + uiSize.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            if (_subtitlePreviewFontCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Font created = ShowcaseSubtitleFontHelper.CreateDrawingFont(fontName, bold, italic, uiSize, Font);
            _subtitlePreviewFontCache[key] = created;
            return created;
        }

        private static void ConfigureGlobalHookStyleEffectCell(DataGridViewComboBoxCell cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.Items.Clear();
            foreach (var entry in ShowcaseHookAnimationCatalog.All)
            {
                cell.Items.Add(entry.ComboLabel);
            }

            if (cell.Items.Count > 0)
            {
                cell.Value = cell.Items[0];
            }
        }

        private static void SelectGlobalHookStyleEffectCell(DataGridViewComboBoxCell cell, string storage)
        {
            ConfigureGlobalHookStyleEffectCell(cell);
            if (cell == null)
            {
                return;
            }

            var idx = ShowcaseSubtitleStyleHelper.HookAnimationToSelectedIndex(storage);
            if (idx >= 0 && idx < cell.Items.Count)
            {
                cell.Value = cell.Items[idx];
            }
        }

        private static string GetGlobalHookStyleEffectStorage(DataGridViewComboBoxCell cell)
        {
            if (cell?.Items == null || cell.Value == null)
            {
                return ShowcaseSubtitleStyleHelper.HookAnimationToStorage(0);
            }

            for (var i = 0; i < cell.Items.Count; i++)
            {
                if (string.Equals(cell.Items[i]?.ToString(), cell.Value.ToString(), StringComparison.Ordinal))
                {
                    return ShowcaseSubtitleStyleHelper.HookAnimationToStorage(i);
                }
            }

            return ShowcaseSubtitleStyleHelper.HookAnimationToStorage(0);
        }

        private static void ConfigureDisplayEffectCell(DataGridViewComboBoxCell cell, ShowcaseDisplayLineEffectKind kind)
        {
            if (cell == null)
            {
                return;
            }

            ShowcaseDisplayLineAnimationHelper.PopulateEffectCellItems(cell, kind);
            cell.Value = cell.Items.Count > 0
                ? cell.Items[0]
                : ShowcaseDisplayLineAnimationHelper.DefaultEffectLabel(kind);
        }

        private static void SelectDisplayEffectCell(
            DataGridViewComboBoxCell cell,
            ShowcaseDisplayLineEffectKind kind,
            string storage)
        {
            if (cell == null)
            {
                return;
            }

            ShowcaseDisplayLineAnimationHelper.SelectStorageCell(cell, kind, storage);
        }

        private static string GetDisplayEffectStorage(
            DataGridViewComboBoxCell cell,
            ShowcaseDisplayLineEffectKind kind)
        {
            return ShowcaseDisplayLineAnimationHelper.GetSelectedStorageFromCell(cell, kind);
        }

        private void DisplayGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_dgvDisplay?.CurrentCell == null)
            {
                return;
            }

            var colName = _dgvDisplay.CurrentCell.OwningColumn?.Name ?? string.Empty;

            if (string.Equals(colName, "colDisplaySubtitle", StringComparison.Ordinal)
                && e.Control is TextBox tb)
            {
                var tag = GetDisplayRowTag(_dgvDisplay.CurrentRow);
                if (tag != null
                    && TryReadSubtitlePreviewStyleFromRow(tag, _dgvDisplay.CurrentRow, out var fontName, out var bold, out var italic, out var primaryAss, out var lineBackgroundAss))
                {
                    tb.Font = GetSubtitlePreviewFont(fontName, bold, italic);
                    var gridBack = _dgvDisplay.DefaultCellStyle.BackColor;
                    var fore = ShowcaseSubtitleColourPresetCatalog.ToDrawingColor(
                        string.IsNullOrWhiteSpace(primaryAss) ? "&H00FFFFFF" : primaryAss);
                    tb.ForeColor = fore;
                    if (!string.IsNullOrWhiteSpace(lineBackgroundAss))
                    {
                        var blended = ShowcaseSubtitleHighlightColourCatalog.ToOpaquePreviewOnBackground(
                            lineBackgroundAss,
                            gridBack);
                        tb.BackColor = blended != Color.Empty ? blended : gridBack;
                    }
                    else
                    {
                        tb.BackColor = gridBack;
                    }
                }

                return;
            }

            if (string.Equals(colName, "colStyleLook", StringComparison.Ordinal)
                && e.Control is ComboBox cbLook)
            {
                var rowTag = GetDisplayRowTag(_dgvDisplay.CurrentRow);
                var kind = rowTag?.Role == DisplayGridRowRole.Hook
                    ? ShowcaseDisplayLineEffectKind.Hook
                    : ShowcaseDisplayLineEffectKind.Body;

                cbLook.DropDownStyle = ComboBoxStyle.DropDownList;
                cbLook.Items.Clear();
                foreach (var p in ShowcaseSubtitleLookPresetCatalog.AllForKind(kind))
                {
                    cbLook.Items.Add(p.Label);
                }

                if (_dgvDisplay.CurrentCell.Value != null && cbLook.Items.Contains(_dgvDisplay.CurrentCell.Value))
                {
                    cbLook.SelectedItem = _dgvDisplay.CurrentCell.Value;
                }

                cbLook.Font = _displayTabCellFont;
                cbLook.ItemHeight = _displayTabComboItemHeight;
                return;
            }

            if (colName != "colDisplayEffect" && colName != "colStyleLook" && colName != "colStyleHighlight" && colName != "colStylePosition")
            {
                return;
            }

            if (!(e.Control is ComboBox cb) || !(_dgvDisplay.CurrentCell is DataGridViewComboBoxCell cell))
            {
                return;
            }

            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Items.Clear();
            foreach (var item in cell.Items)
            {
                cb.Items.Add(item);
            }

            if (cell.Value != null && cb.Items.Contains(cell.Value))
            {
                cb.SelectedItem = cell.Value;
            }

            cb.Font = _displayTabCellFont;
            cb.ItemHeight = _displayTabComboItemHeight;
        }

        private void RefreshDisplayTabLayoutMetrics()
        {
            _displayTabUiInset = (int)Math.Max(8, Math.Round(Font.Size * 0.95));
            var cellSize = Math.Max(10.5f, Font.Size);
            _displayTabCellFont?.Dispose();
            _displayTabHeaderFont?.Dispose();
            _displayTabCellFont = new Font(Font.FontFamily, cellSize, FontStyle.Regular, Font.Unit);
            _displayTabHeaderFont = new Font(Font.FontFamily, cellSize + 0.5f, FontStyle.Bold, Font.Unit);
            _displayTabControlHeight = MeasureSingleLineControlHeight(_displayTabCellFont, 8);
            _displayTabComboItemHeight = MeasureSingleLineControlHeight(_displayTabCellFont, 6);
            _displayTabHeaderHeight = MeasureSingleLineControlHeight(_displayTabHeaderFont, 10);
            _displayTabRowMinHeight = _displayTabControlHeight + (int)Math.Max(10, Math.Round(Font.Size * 1.1));
            ApplyDisplayGridColumnHeaderStyle();
        }

        private void ApplyDisplayGridColumnHeaderStyle()
        {
            if (_dgvDisplay == null || _dgvDisplay.IsDisposed || _displayTabHeaderFont == null)
            {
                return;
            }

            _dgvDisplay.EnableHeadersVisualStyles = false;
            _dgvDisplay.ColumnHeadersHeight = _displayTabHeaderHeight;
            _dgvDisplay.ColumnHeadersDefaultCellStyle.Font = _displayTabHeaderFont;
            _dgvDisplay.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            foreach (DataGridViewColumn col in _dgvDisplay.Columns)
            {
                col.HeaderCell.Style.Font = _displayTabHeaderFont;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            _dgvDisplay.Invalidate();
        }

        private static int AlignMarginTop(int rowHeight, Control control)
        {
            var h = control.PreferredSize.Height;
            if (h <= 0)
            {
                h = control.Height;
            }

            return Math.Max(0, (rowHeight - h) / 2);
        }

        private string TabDefaultEffectStorage(DisplayGridRowTag tag)
        {
            if (tag == null)
            {
                return string.Empty;
            }

            if (tag.EffectKind == ShowcaseDisplayLineEffectKind.Hook)
            {
                return (_video.ShowcaseHookSubtitleAnimation ?? string.Empty).Trim();
            }

            return (_video.ShowcaseSubtitleAnimation ?? string.Empty).Trim();
        }

        private string ResolveEffectStorageForEditor(DisplayGridRowTag tag, string lineStorage)
        {
            var line = (lineStorage ?? string.Empty).Trim();
            if (string.Equals(line, "(Mặc định Kiểu chữ)", StringComparison.Ordinal))
            {
                line = string.Empty;
            }

            if (line.Length > 0)
            {
                return line;
            }

            return TabDefaultEffectStorage(tag);
        }

        private void DisplayGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        private DisplayGridRowTag GetDisplayRowTag(DataGridViewRow row)
        {
            return row?.Tag as DisplayGridRowTag;
        }

        private DataGridViewRow FindDisplayRow(DisplayGridRowRole role)
        {
            if (_dgvDisplay == null)
            {
                return null;
            }

            foreach (DataGridViewRow row in _dgvDisplay.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var tag = GetDisplayRowTag(row);
                if (tag?.Role == role)
                {
                    return row;
                }
            }

            return null;
        }

        internal bool SaveStyleFromGridToVideo()
        {
            if (!SaveTemplateToVideo())
            {
                return false;
            }

            var hookRow = FindDisplayRow(DisplayGridRowRole.Hook);
            if (hookRow != null)
            {
                _video.ShowcaseHookSubtitleEnabled = ReadBoolCell(hookRow.Cells["colStyleEnabled"]);
                if (!TryParseFontSize(hookRow.Cells["colStyleFontSize"].Value, 72, 132, out var hookSize))
                {
                    MessageBox.Show(this, "Cỡ chữ HOOK phải từ 72 đến 132.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                _video.ShowcaseHookSubtitleFontSize = hookSize;
                _video.ShowcaseHookSubtitleAnimation = GetDisplayEffectStorage(
                    hookRow.Cells["colDisplayEffect"] as DataGridViewComboBoxCell,
                    ShowcaseDisplayLineEffectKind.Hook);
                var hookLook = ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(
                    hookRow.Cells["colStyleLook"].Value?.ToString(),
                    ShowcaseDisplayLineEffectKind.Hook);
                ShowcaseSubtitleLookPresetCatalog.SyncHookVideoFields(_video, hookLook);
                _video.ShowcaseHookSubtitleHighlightColourAss = ShowcaseSubtitleHighlightColourCatalog.SecondaryAssFromLabel(
                    hookRow.Cells["colStyleHighlight"].Value?.ToString());
            }

            return true;
        }

        private static string StyleFaceFromBoldItalic(bool bold, bool italic)
        {
            if (italic && !bold)
            {
                return StyleFaceItalic;
            }

            if (bold)
            {
                return StyleFaceBold;
            }

            return StyleFaceRegular;
        }

        private static void BoldItalicFromStyleFace(object faceValue, out bool bold, out bool italic)
        {
            var face = faceValue?.ToString() ?? StyleFaceRegular;
            if (string.Equals(face, StyleFaceItalic, StringComparison.Ordinal))
            {
                bold = false;
                italic = true;
                return;
            }

            if (string.Equals(face, StyleFaceBold, StringComparison.Ordinal))
            {
                bold = true;
                italic = false;
                return;
            }

            bold = false;
            italic = false;
        }

        private static void SelectFontCell(DataGridViewComboBoxCell cell, string primary, string fallback)
        {
            if (cell == null)
            {
                return;
            }

            var font = (primary ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(font))
            {
                font = (fallback ?? string.Empty).Trim();
            }

            for (var i = 0; i < cell.Items.Count; i++)
            {
                if (string.Equals(cell.Items[i]?.ToString(), font, StringComparison.OrdinalIgnoreCase))
                {
                    cell.Value = cell.Items[i];
                    return;
                }
            }

            if (cell.Items.Count > 0)
            {
                cell.Value = cell.Items[0];
            }
        }

        private static bool ReadBoolCell(DataGridViewCell cell)
        {
            return cell?.Value is bool b && b;
        }

        private static bool TryParseFontSize(object value, int min, int max, out int size)
        {
            size = min;
            if (value == null)
            {
                return false;
            }

            if (value is int i)
            {
                size = i;
            }
            else if (!int.TryParse(value.ToString(), out size))
            {
                return false;
            }

            if (size < min || size > max)
            {
                return false;
            }

            return true;
        }

        private void LoadDisplayFromVideo()
        {
            if (_dgvDisplay == null)
            {
                return;
            }

            LoadTemplateFromVideo();

            foreach (DataGridViewRow row in _dgvDisplay.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var tag = GetDisplayRowTag(row);
                if (tag == null)
                {
                    continue;
                }

                LoadContentRowStyleCells(row, tag);

                string displayText;
                string animStorage;
                switch (tag.Role)
                {
                    case DisplayGridRowRole.Hook:
                        displayText = ShowcaseSubtitleDisplayHelper.GetEditorDisplayText(
                            _video.ShowcaseSubtitleDisplayHook,
                            tag.SourceSpeech);
                        animStorage = _video.ShowcaseSubtitleDisplayHookAnimation;
                        break;
                    default:
                        if (tag.IsFinalCtaScene)
                        {
                            displayText = ShowcaseSubtitleDisplayHelper.GetEditorDisplayText(
                                _video.ShowcaseSubtitleDisplayCta,
                                tag.SourceSpeech);
                            animStorage = _video.ShowcaseSubtitleDisplayCtaAnimation;
                        }
                        else
                        {
                            displayText = ShowcaseSubtitleDisplayHelper.GetEditorDisplayText(
                                tag.Scene?.ShowcaseSubtitleDisplayVoiceover,
                                tag.SourceSpeech);
                            animStorage = tag.Scene?.ShowcaseSubtitleDisplayAnimation;
                        }

                        break;
                }

                row.Cells["colDisplaySubtitle"].Value = displayText;
                var editorAnim = ResolveEffectStorageForEditor(tag, animStorage);
                SelectDisplayEffectCell(
                    row.Cells["colDisplayEffect"] as DataGridViewComboBoxCell,
                    tag.EffectKind,
                    editorAnim);
            }

            _dgvDisplay.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            ClearSubtitlePreviewFontCache();
            _dgvDisplay.InvalidateColumn(_dgvDisplay.Columns["colDisplaySubtitle"].Index);
        }

        private void SaveDisplayToVideo()
        {
            if (_dgvDisplay == null)
            {
                return;
            }

            foreach (DataGridViewRow row in _dgvDisplay.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var tag = GetDisplayRowTag(row);
                if (tag == null)
                {
                    continue;
                }

                var subtitle = row.Cells["colDisplaySubtitle"].Value?.ToString() ?? string.Empty;
                var anim = GetDisplayEffectStorage(
                    row.Cells["colDisplayEffect"] as DataGridViewComboBoxCell,
                    tag.EffectKind);
                anim = ShowcaseSubtitleDisplayHelper.CoalesceAnimationOverrideForSave(anim, TabDefaultEffectStorage(tag));

                switch (tag.Role)
                {
                    case DisplayGridRowRole.Hook:
                        _video.ShowcaseSubtitleDisplayHook = ShowcaseSubtitleDisplayHelper.CoalesceDisplayOverrideForSave(
                            subtitle,
                            tag.SourceSpeech);
                        _video.ShowcaseSubtitleDisplayHookAnimation = anim;
                        break;
                    default:
                        if (tag.Scene == null)
                        {
                            break;
                        }

                        if (tag.IsFinalCtaScene)
                        {
                            _video.ShowcaseSubtitleDisplayCta = ShowcaseSubtitleDisplayHelper.CoalesceDisplayOverrideForSave(
                                subtitle,
                                tag.SourceSpeech);
                            _video.ShowcaseSubtitleDisplayCtaAnimation = anim;
                        }
                        else
                        {
                            tag.Scene.ShowcaseSubtitleDisplayVoiceover = ShowcaseSubtitleDisplayHelper.CoalesceDisplayOverrideForSave(
                                subtitle,
                                tag.SourceSpeech);
                            tag.Scene.ShowcaseSubtitleDisplayAnimation = anim;
                        }

                        SaveSceneRowStyleOverrides(row, tag.Scene);
                        break;
                }
            }
        }
    }
}
