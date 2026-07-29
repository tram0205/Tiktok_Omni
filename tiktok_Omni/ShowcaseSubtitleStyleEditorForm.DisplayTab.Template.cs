using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseSubtitleStyleEditorForm
    {
        private ComboBox _tplLook;
        private ComboBox _tplHighlight;
        private NumericUpDown _tplFontSize;
        private ComboBox _tplPosition;
        private ComboBox _tplEffect;

        private Panel BuildSubtitleTemplateBar()
        {
            var uiFont = _displayTabCellFont;
            var uiFontBold = _displayTabHeaderFont;
            var ctrlH = _displayTabControlHeight;
            var itemH = _displayTabComboItemHeight;
            var inset = _displayTabUiInset;
            var back = Color.FromArgb(45, 49, 58);
            var padH = (int)Math.Max(14, Math.Round(Font.Size * 1.35));
            var padV = (int)Math.Max(8, Math.Round(Font.Size * 0.85));

            var bar = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                BackColor = back,
                Padding = new Padding(inset, inset, inset, inset / 2)
            };

            var rowGap = (int)Math.Max(8, Math.Round(Font.Size * 0.65));
            var cellPad = new Padding(0, 4, 12, 4);

            Label MkTplLabel(string text)
            {
                return new Label
                {
                    Text = text,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(160, 168, 182),
                    Font = uiFont,
                    Margin = cellPad
                };
            }

            ComboBox MkTplCombo(object[] items)
            {
                var cb = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    MinimumSize = new Size(64, ctrlH),
                    Height = ctrlH,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Font = uiFont,
                    IntegralHeight = false,
                    ItemHeight = itemH,
                    Margin = cellPad,
                    Dock = DockStyle.Fill
                };
                if (items != null && items.Length > 0)
                {
                    cb.Items.AddRange(items);
                    cb.SelectedIndex = 0;
                }

                return cb;
            }

            NumericUpDown MkTplNumeric(int min, int max, int val) => new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                MinimumSize = new Size(72, ctrlH),
                Height = ctrlH,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = uiFont,
                Margin = cellPad,
                Dock = DockStyle.Fill
            };

            static TableLayoutPanel CreateTemplateRowTable(Color backColor, int columnCount)
            {
                var tlp = new TableLayoutPanel
                {
                    ColumnCount = columnCount,
                    RowCount = 1,
                    AutoSize = false,
                    BackColor = backColor,
                    Margin = new Padding(0)
                };
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
                return tlp;
            }

            static void AddStretchColumn(TableLayoutPanel tlp, float percent)
            {
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percent));
            }

            static void AddAutoColumn(TableLayoutPanel tlp)
            {
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            static void AddFixedColumn(TableLayoutPanel tlp, float pixels)
            {
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, pixels));
            }

            var tlp1 = CreateTemplateRowTable(back, 8);
            AddAutoColumn(tlp1);
            AddStretchColumn(tlp1, 36f);
            AddAutoColumn(tlp1);
            AddFixedColumn(tlp1, Math.Max(96f, Font.Size * 8.5f));
            AddAutoColumn(tlp1);
            AddStretchColumn(tlp1, 14f);
            AddAutoColumn(tlp1);
            AddStretchColumn(tlp1, 50f);

            tlp1.Controls.Add(MkTplLabel("Kiểu chữ"), 0, 0);
            _tplLook = MkTplCombo(Array.Empty<object>());
            ShowcaseSubtitleLookPresetCatalog.PopulateCombo(_tplLook, ShowcaseDisplayLineEffectKind.Body);
            tlp1.Controls.Add(_tplLook, 1, 0);
            tlp1.Controls.Add(MkTplLabel("Cỡ"), 2, 0);
            _tplFontSize = MkTplNumeric(32, 160, 72);
            tlp1.Controls.Add(_tplFontSize, 3, 0);
            tlp1.Controls.Add(MkTplLabel("Vị trí"), 4, 0);
            _tplPosition = MkTplCombo(new object[] { "Dưới", "Giữa", "Trên" });
            tlp1.Controls.Add(_tplPosition, 5, 0);
            tlp1.Controls.Add(MkTplLabel("Hiệu ứng"), 6, 0);
            _tplEffect = MkTplCombo(Array.Empty<object>());
            ShowcaseDisplayLineAnimationHelper.PopulateCombo(_tplEffect, ShowcaseDisplayLineEffectKind.Body);
            tlp1.Controls.Add(_tplEffect, 7, 0);

            var tlp2 = CreateTemplateRowTable(back, 3);
            tlp2.Margin = new Padding(0, rowGap, 0, 0);
            AddAutoColumn(tlp2);
            AddStretchColumn(tlp2, 24f);
            AddStretchColumn(tlp2, 76f);

            tlp2.Controls.Add(MkTplLabel("Nền dòng"), 0, 0);
            _tplHighlight = MkTplCombo(Array.Empty<object>());
            ShowcaseSubtitleHighlightColourCatalog.PopulateCombo(_tplHighlight);
            tlp2.Controls.Add(_tplHighlight, 1, 0);

            var btnApply = new Button
            {
                Text = "Áp dụng cho tất cả dòng",
                MinimumSize = new Size(120, ctrlH),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 120, 82),
                ForeColor = Color.White,
                Font = uiFontBold,
                Padding = new Padding(padH, padV, padH, padV),
                Margin = cellPad,
                Dock = DockStyle.Fill
            };
            btnApply.Click += (_, __) => ApplyTemplateToAllContentRows();
            tlp2.Controls.Add(btnApply, 2, 0);

            var rowH = ctrlH + cellPad.Vertical + 4;
            tlp1.RowStyles[0] = new RowStyle(SizeType.Absolute, rowH);
            tlp1.Height = (int)rowH;
            tlp2.RowStyles[0] = new RowStyle(SizeType.Absolute, rowH);
            tlp2.Height = (int)rowH;
            tlp1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tlp2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            void SyncTemplateBarLayout()
            {
                var hostW = bar.Parent?.ClientSize.Width ?? bar.Width;
                if (hostW <= 0)
                {
                    hostW = 720;
                }

                var innerW = Math.Max(320, hostW - bar.Padding.Horizontal);
                var left = bar.Padding.Left;
                var top = bar.Padding.Top;
                tlp1.SetBounds(left, top, innerW, (int)rowH);
                tlp2.SetBounds(left, top + (int)rowH + rowGap, innerW, (int)rowH);
                bar.Height = tlp2.Bottom + bar.Padding.Bottom;
            }

            bar.Layout += (_, __) => SyncTemplateBarLayout();
            bar.ParentChanged += (_, __) => SyncTemplateBarLayout();

            bar.Controls.Add(tlp2);
            bar.Controls.Add(tlp1);
            SyncTemplateBarLayout();
            return bar;
        }

        private void LoadTemplateFromVideo()
        {
            if (_tplLook == null)
            {
                return;
            }

            _tplFontSize.Value = Math.Max(_tplFontSize.Minimum,
                Math.Min(_tplFontSize.Maximum, _video.ShowcaseSubtitleFontSize <= 0 ? 72 : _video.ShowcaseSubtitleFontSize));
            _tplPosition.SelectedItem = ReupSubtitleStyleHelper.ParsePosition(_video.ShowcaseSubtitlePosition) switch
            {
                ReupSubtitleVerticalPosition.Top => "Trên",
                ReupSubtitleVerticalPosition.Middle => "Giữa",
                _ => "Dưới"
            };
            ShowcaseDisplayLineAnimationHelper.SelectStorage(_tplEffect, ShowcaseDisplayLineEffectKind.Body, _video.ShowcaseSubtitleAnimation);
            var bodyLook = ShowcaseSubtitleLookPresetCatalog.ResolveBodyStorageFromLegacy(_video);
            ShowcaseSubtitleLookPresetCatalog.SelectLabel(_tplLook, bodyLook, ShowcaseDisplayLineEffectKind.Body);
            ShowcaseSubtitleHighlightColourCatalog.SelectLabel(_tplHighlight, _video.ShowcaseSubtitleHighlightColourAss);
        }

        private bool SaveTemplateToVideo()
        {
            if (_tplLook == null)
            {
                return true;
            }

            _video.ShowcaseSubtitleFontSize = (int)_tplFontSize.Value;
            _video.ShowcaseTextSize = _video.ShowcaseSubtitleFontSize;
            var pos = _tplPosition.SelectedItem?.ToString() ?? "Dưới";
            _video.ShowcaseSubtitlePosition = string.Equals(pos, "Trên", StringComparison.Ordinal) ? "Top"
                : string.Equals(pos, "Giữa", StringComparison.Ordinal) ? "Middle"
                : "Bottom";
            var run = _tplEffect.SelectedItem?.ToString() ?? string.Empty;
            _video.ShowcaseSubtitleAnimation = ShowcaseDisplayLineAnimationHelper.GetSelectedStorage(
                _tplEffect,
                ShowcaseDisplayLineEffectKind.Body);
            var lookStorage = ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(
                _tplLook.SelectedItem?.ToString(),
                ShowcaseDisplayLineEffectKind.Body);
            ShowcaseSubtitleLookPresetCatalog.SyncBodyVideoFields(_video, lookStorage);
            _video.ShowcaseSubtitleHighlightColourAss = ShowcaseSubtitleHighlightColourCatalog.SecondaryAssFromLabel(
                _tplHighlight.SelectedItem?.ToString());
            return true;
        }

        private void ApplyTemplateToAllContentRows()
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

                if (tag.Role == DisplayGridRowRole.Hook)
                {
                    continue;
                }

                WriteBodyStyleCellsFromTemplate(row);
                var fxCell = row.Cells["colDisplayEffect"] as DataGridViewComboBoxCell;
                ConfigureDisplayEffectCell(fxCell, ShowcaseDisplayLineEffectKind.Body);
                if (_tplEffect.SelectedItem != null)
                {
                    fxCell.Value = _tplEffect.SelectedItem;
                }
            }

            ClearSubtitlePreviewFontCache();
            _dgvDisplay.InvalidateColumn(_dgvDisplay.Columns["colDisplaySubtitle"].Index);
        }

        private void WriteBodyStyleCellsFromTemplate(DataGridViewRow row)
        {
            row.Cells["colStyleEnabled"].Value = _video.ShowcaseSubtitleEnabled;
            row.Cells["colStyleLook"].Value = _tplLook.SelectedItem ?? _tplLook.Items[0];
            row.Cells["colStyleHighlight"].Value = _tplHighlight.SelectedItem ?? ShowcaseSubtitleHighlightColourCatalog.FollowLookLabel;
            row.Cells["colStyleFontSize"].Value = _tplFontSize.Value.ToString();
            row.Cells["colStylePosition"].Value = _tplPosition.SelectedItem ?? "Dưới";
        }

        private void WriteHookStyleCellsFromVideo(DataGridViewRow row)
        {
            row.Cells["colStyleEnabled"].Value = _video.ShowcaseHookSubtitleEnabled;
            ConfigureLookCell(row.Cells["colStyleLook"] as DataGridViewComboBoxCell, ShowcaseDisplayLineEffectKind.Hook);
            var hookLook = ShowcaseSubtitleLookPresetCatalog.ResolveHookStorageFromLegacy(_video);
            ShowcaseSubtitleLookPresetCatalog.SelectLabelCell(
                row.Cells["colStyleLook"] as DataGridViewComboBoxCell,
                hookLook,
                ShowcaseDisplayLineEffectKind.Hook);
            var hookSize = _video.ShowcaseHookSubtitleFontSize > 0
                ? _video.ShowcaseHookSubtitleFontSize
                : Math.Max(80, _video.ShowcaseSubtitleFontSize + 22);
            row.Cells["colStyleFontSize"].Value = hookSize.ToString();
            row.Cells["colStylePosition"].Value = "Giữa";
            ShowcaseSubtitleHighlightColourCatalog.SelectLabelCell(
                row.Cells["colStyleHighlight"] as DataGridViewComboBoxCell,
                _video.ShowcaseHookSubtitleHighlightColourAss);
        }

        private void LoadContentRowStyleCells(DataGridViewRow row, DisplayGridRowTag tag)
        {
            if (tag.Role == DisplayGridRowRole.Hook)
            {
                WriteHookStyleCellsFromVideo(row);
                SelectDisplayEffectCell(
                    row.Cells["colDisplayEffect"] as DataGridViewComboBoxCell,
                    ShowcaseDisplayLineEffectKind.Hook,
                    _video.ShowcaseHookSubtitleAnimation);
                return;
            }

            if (tag.Role == DisplayGridRowRole.Scene && tag.Scene != null)
            {
                LoadSceneRowStyleFromSceneOrTemplate(row, tag.Scene);
                return;
            }
        }

        private void LoadSceneRowStyleFromSceneOrTemplate(DataGridViewRow row, AiVideoGenInputItem scene)
        {
            var hasOverride = !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayLookPreset)
                              || !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayHighlightColourAss)
                              || !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayFontName)
                              || scene.ShowcaseSubtitleDisplayFontSize > 0
                              || !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayFontFace)
                              || !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayPosition)
                              || !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayPrimaryColourAss)
                              || !string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayDecorPreset);

            if (!hasOverride)
            {
                WriteBodyStyleCellsFromTemplate(row);
                return;
            }

            row.Cells["colStyleEnabled"].Value = _video.ShowcaseSubtitleEnabled;
            ConfigureLookCell(row.Cells["colStyleLook"] as DataGridViewComboBoxCell, ShowcaseDisplayLineEffectKind.Body);
            var look = ShowcaseSubtitleLookPresetCatalog.ResolveSceneStorageFromLegacy(scene);
            if (string.IsNullOrWhiteSpace(look))
            {
                row.Cells["colStyleLook"].Value = _tplLook.SelectedItem ?? _tplLook.Items[0];
            }
            else
            {
                ShowcaseSubtitleLookPresetCatalog.SelectLabelCell(
                    row.Cells["colStyleLook"] as DataGridViewComboBoxCell,
                    look,
                    ShowcaseDisplayLineEffectKind.Body);
            }

            row.Cells["colStyleFontSize"].Value = scene.ShowcaseSubtitleDisplayFontSize > 0
                ? scene.ShowcaseSubtitleDisplayFontSize.ToString()
                : _tplFontSize.Value.ToString();

            if (!string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayPosition))
            {
                row.Cells["colStylePosition"].Value = PositionStorageToLabel(scene.ShowcaseSubtitleDisplayPosition);
            }
            else
            {
                row.Cells["colStylePosition"].Value = _tplPosition.SelectedItem ?? "Dưới";
            }

            if (!string.IsNullOrWhiteSpace(scene.ShowcaseSubtitleDisplayHighlightColourAss))
            {
                ShowcaseSubtitleHighlightColourCatalog.SelectLabelCell(
                    row.Cells["colStyleHighlight"] as DataGridViewComboBoxCell,
                    scene.ShowcaseSubtitleDisplayHighlightColourAss);
            }
            else
            {
                row.Cells["colStyleHighlight"].Value = _tplHighlight.SelectedItem ?? ShowcaseSubtitleHighlightColourCatalog.FollowLookLabel;
            }
        }

        private void SaveSceneRowStyleOverrides(DataGridViewRow row, AiVideoGenInputItem scene)
        {
            if (scene == null || RowBodyStyleMatchesTemplate(row))
            {
                if (scene != null)
                {
                    scene.ShowcaseSubtitleDisplayLookPreset = string.Empty;
                    scene.ShowcaseSubtitleDisplayHighlightColourAss = string.Empty;
                    scene.ShowcaseSubtitleDisplayFontName = string.Empty;
                    scene.ShowcaseSubtitleDisplayFontSize = 0;
                    scene.ShowcaseSubtitleDisplayFontFace = string.Empty;
                    scene.ShowcaseSubtitleDisplayPosition = string.Empty;
                    scene.ShowcaseSubtitleDisplayPrimaryColourAss = string.Empty;
                    scene.ShowcaseSubtitleDisplayDecorPreset = string.Empty;
                }

                return;
            }

            var lookStorage = ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(
                row.Cells["colStyleLook"].Value?.ToString(),
                ShowcaseDisplayLineEffectKind.Body);
            ShowcaseSubtitleLookPresetCatalog.SyncSceneDisplayFields(scene, lookStorage);
            scene.ShowcaseSubtitleDisplayHighlightColourAss = ShowcaseSubtitleHighlightColourCatalog.SecondaryAssFromLabel(
                row.Cells["colStyleHighlight"].Value?.ToString());
            if (TryParseFontSize(row.Cells["colStyleFontSize"].Value, 32, 160, out var size))
            {
                scene.ShowcaseSubtitleDisplayFontSize = size;
            }

            scene.ShowcaseSubtitleDisplayPosition = PositionLabelToStorage(row.Cells["colStylePosition"].Value?.ToString());
        }

        private bool RowBodyStyleMatchesTemplate(DataGridViewRow row)
        {
            if (_tplLook == null)
            {
                return true;
            }

            if (ReadBoolCell(row.Cells["colStyleEnabled"]) != _video.ShowcaseSubtitleEnabled)
            {
                return false;
            }

            if (!string.Equals(row.Cells["colStyleLook"].Value?.ToString(), _tplLook.SelectedItem?.ToString(), StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(row.Cells["colStyleHighlight"].Value?.ToString(), _tplHighlight.SelectedItem?.ToString(), StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryParseFontSize(row.Cells["colStyleFontSize"].Value, 32, 160, out var size)
                || size != (int)_tplFontSize.Value)
            {
                return false;
            }

            if (!string.Equals(row.Cells["colStylePosition"].Value?.ToString(), _tplPosition.SelectedItem?.ToString(), StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static string PositionLabelToStorage(string label)
        {
            if (string.Equals(label, "Trên", StringComparison.Ordinal))
            {
                return "Top";
            }

            if (string.Equals(label, "Giữa", StringComparison.Ordinal))
            {
                return "Middle";
            }

            return "Bottom";
        }

        private static string PositionStorageToLabel(string storage)
        {
            return ReupSubtitleStyleHelper.ParsePosition(storage) switch
            {
                ReupSubtitleVerticalPosition.Top => "Trên",
                ReupSubtitleVerticalPosition.Middle => "Giữa",
                _ => "Dưới"
            };
        }

        private static string FaceLabelToStorage(bool bold, bool italic)
        {
            if (italic && !bold)
            {
                return "Italic";
            }

            if (bold)
            {
                return "Bold";
            }

            return string.Empty;
        }

        private static string FaceStorageToLabel(string storage)
        {
            var s = (storage ?? string.Empty).Trim();
            if (string.Equals(s, "Italic", StringComparison.OrdinalIgnoreCase))
            {
                return StyleFaceItalic;
            }

            if (string.Equals(s, "Bold", StringComparison.OrdinalIgnoreCase))
            {
                return StyleFaceBold;
            }

            return StyleFaceRegular;
        }
    }
}
