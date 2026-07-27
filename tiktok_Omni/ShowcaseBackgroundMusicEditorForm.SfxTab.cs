using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm
    {
        private const int SfxRowTagCta = -1;
        private const int SfxRowTagHook = -2;

        private CheckBox _chkSfxMaster;
        private DataGridView _dgvSfx;
        private Label _lblSfxLibrary;
        private List<string> _sfxFileNames = new List<string>();

        private void BuildSfxTab(TabPage tab)
        {
            tab.AutoScroll = false;

            _chkSfxMaster = new CheckBox
            {
                Text = "Bật hiệu ứng âm thanh khi render (theo từng cảnh + CTA)",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12),
                Dock = DockStyle.Fill
            };

            _dgvSfx = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                GridColor = Color.FromArgb(58, 64, 78),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ShowCellToolTips = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 66,
                RowTemplate = { Height = 60 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    SelectionBackColor = Color.FromArgb(56, 120, 82),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(52, 58, 72),
                    ForeColor = Color.FromArgb(190, 198, 212),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };

            var colEnabled = new DataGridViewCheckBoxColumn
            {
                Name = "colSfxEnabled",
                HeaderText = "Bật",
                FillWeight = 4,
                TrueValue = true,
                FalseValue = false
            };
            var colLabel = new DataGridViewTextBoxColumn
            {
                Name = "colSfxLabel",
                HeaderText = "Vị trí",
                ReadOnly = true,
                FillWeight = 21
            };
            var colPlacement = new DataGridViewComboBoxColumn
            {
                Name = "colSfxPlacement",
                HeaderText = "Chèn",
                FillWeight = 20,
                FlatStyle = FlatStyle.Flat
            };
            colPlacement.Items.Add("Đầu cảnh");
            colPlacement.Items.Add("Giữa cảnh");
            colPlacement.Items.Add("Cuối cảnh");
            var colFile = new DataGridViewComboBoxColumn
            {
                Name = "colSfxFile",
                HeaderText = "File SFX",
                FillWeight = 20,
                FlatStyle = FlatStyle.Flat
            };
            var colVolume = new DataGridViewTextBoxColumn
            {
                Name = "colSfxVolume",
                HeaderText = "Volume",
                FillWeight = 20
            };
            var colOffset = new DataGridViewTextBoxColumn
            {
                Name = "colSfxOffset",
                HeaderText = "Lệch (s)",
                FillWeight = 20
            };
            var colGemini = new DataGridViewTextBoxColumn
            {
                Name = "colSfxGemini",
                HeaderText = "Gợi ý Gemini",
                ReadOnly = true,
                FillWeight = 20
            };

            _dgvSfx.Columns.AddRange(colEnabled, colLabel, colPlacement, colFile, colVolume, colOffset, colGemini);
            ConfigureSfxGridColumnWidths();
            _dgvSfx.DataError += SfxGrid_DataError;
            _dgvSfx.CellFormatting += SfxGrid_CellFormatting;
            _dgvSfx.Resize += (_, __) =>
            {
                ConfigureSfxGridColumnWidths();
                _dgvSfx.Invalidate();
            };

            _lblSfxLibrary = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 0, 6),
                Dock = DockStyle.Fill
            };

            var btnOpen = CreateActionButton("Mở thư mục SFX", Color.FromArgb(48, 112, 168));
            btnOpen.Click += (_, __) =>
            {
                try
                {
                    ShowcaseSfxCatalog.EnsureLibraryDirectoryExists(_settings);
                    Process.Start("explorer.exe", OmniAudioLibrary.GetPrimarySfxDirectory(_settings));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "SFX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var btnRefresh = CreateActionButton("Làm mới danh sách", Color.FromArgb(62, 132, 88));
            btnRefresh.Click += (_, __) =>
            {
                ReloadSfxFileList();
                RefreshSfxFileComboCells();
            };

            var btnClearRow = CreateActionButton("Xóa SFX dòng chọn", Color.FromArgb(120, 72, 72));
            btnClearRow.Click += (_, __) =>
            {
                if (_dgvSfx.CurrentRow == null)
                {
                    return;
                }

                SetRowFile(_dgvSfx.CurrentRow, string.Empty);
                _dgvSfx.CurrentRow.Cells["colSfxEnabled"].Value = false;
            };

            _btnPreviewSfx = CreateActionButton(PreviewPlaySfxLabel, Color.FromArgb(88, 118, 158));
            _btnPreviewSfx.Click += (_, __) => TryPreviewSfxCurrentRow();

            var libButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0, 8, 0, 14),
                MinimumSize = new Size(480, 70)
            };
            libButtons.Controls.Add(btnOpen);
            libButtons.Controls.Add(btnRefresh);
            libButtons.Controls.Add(_btnPreviewSfx);
            libButtons.Controls.Add(btnClearRow);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = BackColor,
                Width = tab.ClientSize.Width - tab.Padding.Horizontal
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.Controls.Add(_chkSfxMaster, 0, 0);
            header.Controls.Add(_lblSfxLibrary, 0, 1);
            header.Controls.Add(libButtons, 0, 2);

            tab.Controls.Add(_dgvSfx);
            tab.Controls.Add(header);

            tab.Resize += (_, __) =>
            {
                header.Width = Math.Max(320, tab.ClientSize.Width - tab.Padding.Horizontal);
            };

            ReloadSfxFileList();
        }

        /// <summary>Cột Bật cố định px; Vị trí rộng (Fill + min width).</summary>
        private void ConfigureSfxGridColumnWidths()
        {
            if (_dgvSfx == null || _dgvSfx.Columns.Count == 0)
            {
                return;
            }

            const int enabledWidth = 144;

            if (_dgvSfx.Columns["colSfxEnabled"] is DataGridViewColumn enabledCol)
            {
                enabledCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                enabledCol.MinimumWidth = enabledWidth;
                enabledCol.Width = enabledWidth;
                enabledCol.Resizable = DataGridViewTriState.False;
            }

            if (_dgvSfx.Columns["colSfxLabel"] is DataGridViewColumn labelCol)
            {
                labelCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                labelCol.FillWeight = 34;
                labelCol.MinimumWidth = 260;
                labelCol.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            }

            const float equalTailWeight = 20f;
            const int equalTailMinWidth = 96;
            var equalWidthColumns = new[]
            {
                "colSfxPlacement",
                "colSfxFile",
                "colSfxVolume",
                "colSfxOffset",
                "colSfxGemini"
            };
            foreach (var columnName in equalWidthColumns)
            {
                if (_dgvSfx.Columns[columnName] is DataGridViewColumn col)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    col.FillWeight = equalTailWeight;
                    col.MinimumWidth = equalTailMinWidth;
                }
            }

            ApplySfxGridColumnContentAlignment();
        }

        private void ApplySfxGridColumnContentAlignment()
        {
            if (_dgvSfx == null)
            {
                return;
            }

            void CenterCell(string name)
            {
                if (_dgvSfx.Columns[name] is DataGridViewColumn col)
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            }

            CenterCell("colSfxEnabled");
            CenterCell("colSfxPlacement");
            CenterCell("colSfxVolume");
            CenterCell("colSfxOffset");

            if (_dgvSfx.Columns["colSfxLabel"] is DataGridViewColumn labelCol)
            {
                labelCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }
        }

        private void SfxGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_dgvSfx == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvSfx.Columns[e.ColumnIndex].Name;

            switch (colName)
            {
                case "colSfxEnabled":
                case "colSfxPlacement":
                case "colSfxVolume":
                case "colSfxOffset":
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    break;
                case "colSfxLabel":
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    break;
            }

            if (colName == "colSfxLabel")
            {
                var full = (e.Value ?? string.Empty).ToString();
                e.CellStyle.WrapMode = DataGridViewTriState.False;
                var colWidth = Math.Max(24, _dgvSfx.Columns[e.ColumnIndex].Width - 10);
                var font = e.CellStyle.Font ?? _dgvSfx.Font;
                e.Value = TruncateWithEllipsis(full, font, colWidth);
                e.FormattingApplied = true;
                _dgvSfx.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = full;
                return;
            }

            if (colName != "colSfxPlacement")
            {
                return;
            }

            var row = _dgvSfx.Rows[e.RowIndex];
            if (!(row.Tag is int tag))
            {
                return;
            }

            if (tag == SfxRowTagHook)
            {
                e.Value = "Hook";
                e.FormattingApplied = true;
            }
            else if (tag == SfxRowTagCta)
            {
                e.Value = "CTA";
                e.FormattingApplied = true;
            }
        }

        private static string TruncateWithEllipsis(string text, Font font, int maxPixelWidth)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0 || maxPixelWidth <= 4)
            {
                return text;
            }

            var fullW = TextRenderer.MeasureText(
                text,
                font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            if (fullW <= maxPixelWidth)
            {
                return text;
            }

            return EllipsisByChars(text, font, maxPixelWidth);
        }

        private static string EllipsisByChars(string text, Font font, int maxPixelWidth)
        {
            const string ellipsis = "…";
            for (var len = text.Length; len > 0; len--)
            {
                var candidate = text.Substring(0, len) + ellipsis;
                var w = TextRenderer.MeasureText(candidate, font, Size.Empty, TextFormatFlags.SingleLine).Width;
                if (w <= maxPixelWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private void SfxGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }

            var col = _dgvSfx.Columns[e.ColumnIndex];
            if (col?.Name == "colSfxFile" || col?.Name == "colSfxPlacement")
            {
                e.ThrowException = false;
                e.Cancel = true;
            }
        }

        private void ReloadSfxFileList()
        {
            _sfxFileNames = ShowcaseSfxCatalog.ListFileNames(_settings);
            _lblSfxLibrary.Text = _sfxFileNames.Count == 0
                ? "Chưa có file · Assets\\Audio\\Sfx"
                : _sfxFileNames.Count + " file · Assets\\Audio\\Sfx";
        }

        private void RefreshSfxFileComboCells()
        {
            if (_dgvSfx == null)
            {
                return;
            }

            foreach (DataGridViewRow row in _dgvSfx.Rows)
            {
                EnsureFileComboItems(row);
            }
        }

        private void EnsureFileComboItems(DataGridViewRow row)
        {
            var cell = row.Cells["colSfxFile"] as DataGridViewComboBoxCell;
            if (cell == null)
            {
                return;
            }

            var current = cell.Value?.ToString()?.Trim() ?? string.Empty;
            cell.Items.Clear();
            cell.Items.Add(string.Empty);
            foreach (var name in _sfxFileNames)
            {
                cell.Items.Add(name);
            }

            if (!string.IsNullOrEmpty(current))
            {
                if (!cell.Items.Contains(current))
                {
                    cell.Items.Add(current);
                }

                cell.Value = current;
            }
            else
            {
                cell.Value = string.Empty;
            }
        }

        private void LoadSfxGridFromVideo()
        {
            if (_dgvSfx == null)
            {
                return;
            }

            _chkSfxMaster.Checked = _video.ShowcaseSfxMasterEnabled;
            _dgvSfx.Rows.Clear();

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();

            var hookVol = _video.ShowcaseHookSfxVolumePercent > 0
                ? _video.ShowcaseHookSfxVolumePercent
                : ShowcaseSfxCatalog.DefaultVolumePercent;
            AddSfxRow(
                sceneIndex: SfxRowTagHook,
                label: "Hook · mở đầu",
                enabled: _video.ShowcaseHookSfxEnabled,
                placement: ShowcaseSfxCatalog.PlacementSceneStart,
                file: _video.ShowcaseHookSfxFile,
                volume: hookVol,
                offset: _video.ShowcaseHookSfxOffsetSeconds,
                geminiHint: _video.ShowcaseHookSfxGeminiHint,
                placementEditable: false);

            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var title = string.IsNullOrWhiteSpace(scene.SceneTitle)
                    ? "Cảnh " + (i + 1)
                    : scene.SceneTitle.Trim();

                AddSfxRow(
                    sceneIndex: i,
                    label: title,
                    enabled: scene.ShowcaseSfxEnabled && !string.IsNullOrWhiteSpace(scene.ShowcaseSfxFile),
                    placement: scene.ShowcaseSfxPlacement,
                    file: scene.ShowcaseSfxFile,
                    volume: scene.ShowcaseSfxVolumePercent,
                    offset: scene.ShowcaseSfxOffsetSeconds,
                    geminiHint: scene.ShowcaseSfxGeminiHint,
                    placementEditable: true);
            }

            var ctaVol = _video.ShowcaseCtaSfxVolumePercent > 0
                ? _video.ShowcaseCtaSfxVolumePercent
                : ShowcaseSfxCatalog.DefaultVolumePercent;
            AddSfxRow(
                sceneIndex: SfxRowTagCta,
                label: "CTA · cảnh cuối",
                enabled: _video.ShowcaseCtaSfxEnabled && !string.IsNullOrWhiteSpace(_video.ShowcaseCtaSfxFile),
                placement: ShowcaseSfxCatalog.PlacementSceneStart,
                file: _video.ShowcaseCtaSfxFile,
                volume: ctaVol,
                offset: _video.ShowcaseCtaSfxOffsetSeconds,
                geminiHint: _video.ShowcaseCtaSfxGeminiHint,
                placementEditable: false);

            ReloadSfxFileList();
            RefreshSfxFileComboCells();
        }

        private void AddSfxRow(
            int sceneIndex,
            string label,
            bool enabled,
            string placement,
            string file,
            int volume,
            double offset,
            string geminiHint,
            bool placementEditable)
        {
            var rowIndex = _dgvSfx.Rows.Add();
            var row = _dgvSfx.Rows[rowIndex];
            row.Tag = sceneIndex;
            row.Cells["colSfxEnabled"].Value = enabled && !string.IsNullOrWhiteSpace(file);
            row.Cells["colSfxLabel"].Value = label;
            var placementCell = row.Cells["colSfxPlacement"];
            if (!placementEditable)
            {
                placementCell.Value = "Đầu cảnh";
                placementCell.ReadOnly = true;
                placementCell.Style.BackColor = Color.FromArgb(52, 56, 68);
            }
            else
            {
                placementCell.Value = PlacementToUi(placement);
                placementCell.ReadOnly = false;
            }
            row.Cells["colSfxFile"].Value = string.Empty;
            EnsureFileComboItems(row);
            var filePick = (file ?? string.Empty).Trim();
            if (filePick.Length > 0)
            {
                row.Cells["colSfxFile"].Value = filePick;
            }

            row.Cells["colSfxVolume"].Value = volume > 0 ? volume.ToString() : ShowcaseSfxCatalog.DefaultVolumePercent.ToString();
            row.Cells["colSfxOffset"].Value = offset.ToString("0.##");
            row.Cells["colSfxGemini"].Value = (geminiHint ?? string.Empty).Trim();
        }

        private static string PlacementToUi(string placement)
        {
            var norm = ShowcaseSfxCatalog.NormalizePlacement(placement);
            if (norm == ShowcaseSfxCatalog.PlacementSceneEnd)
            {
                return "Cuối cảnh";
            }

            if (norm == ShowcaseSfxCatalog.PlacementSceneMiddle)
            {
                return "Giữa cảnh";
            }

            return "Đầu cảnh";
        }

        private static string PlacementFromUi(string ui)
        {
            var t = (ui ?? string.Empty).Trim();
            if (t.StartsWith("Cuối", StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseSfxCatalog.PlacementSceneEnd;
            }

            if (t.StartsWith("Giữa", StringComparison.OrdinalIgnoreCase))
            {
                return ShowcaseSfxCatalog.PlacementSceneMiddle;
            }

            return ShowcaseSfxCatalog.PlacementSceneStart;
        }

        private static void SetRowFile(DataGridViewRow row, string fileName)
        {
            var cell = row.Cells["colSfxFile"] as DataGridViewComboBoxCell;
            if (cell == null)
            {
                return;
            }

            var want = (fileName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(want) && !cell.Items.Contains(want))
            {
                cell.Items.Add(want);
            }

            cell.Value = want;
        }

        private bool SaveSfxFromGrid()
        {
            if (_dgvSfx == null)
            {
                return true;
            }

            _video.ShowcaseSfxMasterEnabled = _chkSfxMaster.Checked;

            foreach (DataGridViewRow row in _dgvSfx.Rows)
            {
                var tag = row.Tag is int idx ? idx : 0;
                if (!TryParseRow(row, out var enabled, out var placement, out var file, out var volume, out var offset))
                {
                    MessageBox.Show(this,
                        "Kiểm tra lại âm lượng (0–100) và lệch thời gian (số) trên tab Hiệu ứng âm thanh.",
                        "SFX",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return false;
                }

                if (tag == SfxRowTagHook)
                {
                    _video.ShowcaseHookSfxEnabled = enabled;
                    _video.ShowcaseHookSfxFile = file;
                    _video.ShowcaseHookSfxVolumePercent = volume;
                    _video.ShowcaseHookSfxOffsetSeconds = offset;
                    continue;
                }

                if (tag == SfxRowTagCta)
                {
                    _video.ShowcaseCtaSfxEnabled = enabled;
                    _video.ShowcaseCtaSfxFile = file;
                    _video.ShowcaseCtaSfxVolumePercent = volume;
                    _video.ShowcaseCtaSfxOffsetSeconds = offset;
                    continue;
                }

                if (tag < 0 || tag >= _video.Scenes.Count)
                {
                    continue;
                }

                var scene = _video.Scenes[tag];
                if (scene == null)
                {
                    continue;
                }

                scene.ShowcaseSfxEnabled = enabled;
                scene.ShowcaseSfxPlacement = placement;
                scene.ShowcaseSfxFile = file;
                scene.ShowcaseSfxVolumePercent = volume;
                scene.ShowcaseSfxOffsetSeconds = offset;
                if (string.IsNullOrEmpty(file))
                {
                    scene.ShowcaseSfxEnabled = false;
                }
            }

            return true;
        }

        private static bool TryParseRow(
            DataGridViewRow row,
            out bool enabled,
            out string placement,
            out string file,
            out int volume,
            out double offset)
        {
            enabled = row.Cells["colSfxEnabled"].Value as bool? ?? false;
            placement = PlacementFromUi(row.Cells["colSfxPlacement"].Value?.ToString());
            file = (row.Cells["colSfxFile"].Value?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(file))
            {
                enabled = false;
            }

            var volText = (row.Cells["colSfxVolume"].Value?.ToString() ?? string.Empty).Trim();
            if (!int.TryParse(volText, out volume))
            {
                volume = ShowcaseSfxCatalog.DefaultVolumePercent;
            }

            volume = ShowcaseSfxCatalog.ClampVolumePercent(volume);

            var offText = (row.Cells["colSfxOffset"].Value?.ToString() ?? "0").Trim()
                .Replace(',', '.');
            if (!double.TryParse(offText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out offset))
            {
                offset = 0d;
            }

            offset = Math.Max(-3d, Math.Min(8d, offset));
            return true;
        }
    }
}
