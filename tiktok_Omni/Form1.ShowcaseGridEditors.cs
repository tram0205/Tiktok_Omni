using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;namespace tiktok_Omni
{
    public partial class Form1
    {
        private int _showcaseGridEditorClickSuppressUntilTick;
        private int _showcaseGridEditorClickRow = -1;
        private int _showcaseGridEditorClickCol = -1;

        private void DgvDeepDiveInput_ShowcaseEditorCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = dgvDeepDiveInput?.Columns[e.ColumnIndex]?.Name;
            if (IsShowcaseDualActionGridColumn(colName))
            {
                return;
            }

            TryHandleShowcaseEditorCellClick(e.RowIndex, e.ColumnIndex);
        }

        private void TryHandleShowcaseEditorCellClick(int rowIndex, int columnIndex)
        {
            try
            {
                TryHandleShowcaseEditorCellClickCore(rowIndex, columnIndex);
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Lỗi khi bấm ô lưới: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Showcase", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TryHandleShowcaseEditorCellClickCore(int rowIndex, int columnIndex)
        {
            if (dgvDeepDiveInput == null || rowIndex < 0 || columnIndex < 0)
            {
                return;
            }

            var tick = Environment.TickCount;
            if (rowIndex == _showcaseGridEditorClickRow
                && columnIndex == _showcaseGridEditorClickCol
                && tick - _showcaseGridEditorClickSuppressUntilTick < 900)
            {
                return;
            }

            _showcaseGridEditorClickRow = rowIndex;
            _showcaseGridEditorClickCol = columnIndex;
            _showcaseGridEditorClickSuppressUntilTick = tick;

            if (!(dgvDeepDiveInput.Rows[rowIndex].DataBoundItem is ShowcaseVideoItem video))
            {
                return;
            }

            var colName = dgvDeepDiveInput.Columns[columnIndex]?.Name;
            if (string.Equals(colName, "colAiProduct", StringComparison.Ordinal))
            {
                ShowShowcaseProductNameEditor(video, rowIndex);
            }
            else if (string.Equals(colName, "colAiShowcaseTheme", StringComparison.Ordinal)
                     || string.Equals(colName, "colAiShowcaseProductType", StringComparison.Ordinal))
            {
                ShowShowcaseProductTypeThemeChooser(video, rowIndex);
            }
            else if (string.Equals(colName, "colAiShowcaseTextSize", StringComparison.Ordinal))
            {
                ShowShowcaseSubtitleStyleEditor(video, rowIndex);
            }
            else if (string.Equals(colName, "colAiShowcaseMusicVolume", StringComparison.Ordinal))
            {
                ShowShowcaseBackgroundMusicEditor(video, rowIndex);
            }
            else if (string.Equals(colName, "colAiShowcaseScript", StringComparison.Ordinal)
                     || string.Equals(colName, "colAiShowcaseScenePrompt", StringComparison.Ordinal))
            {
                ShowShowcaseScriptPromptChooser(video, rowIndex);
            }
            else if (string.Equals(colName, "colAiStatus", StringComparison.Ordinal)
                     || string.Equals(colName, "colAiShowcaseOutput", StringComparison.Ordinal))
            {
                OpenShowcaseOutputFolderFromGridCell(video);
            }

            _showcaseGridEditorClickSuppressUntilTick = Environment.TickCount;
        }

        private void ShowShowcaseScriptPromptChooser(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var productName = (video.ProductName ?? string.Empty).Trim();
            if (_showcaseSession != null &&
                string.Equals(_showcaseSession.ProductName, productName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(video.ShowcaseHookText))
                {
                    video.ShowcaseHookText = _showcaseSession.HookText ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(video.ShowcaseCtaText))
                {
                    video.ShowcaseCtaText = _showcaseSession.CtaText ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(video.ShowcaseTheme))
                {
                    video.ShowcaseTheme = _showcaseSession.Theme ?? string.Empty;
                }
            }

            using (var hub = new ShowcaseScriptPromptHubForm(video, ExportShowcaseExcelForVideoFromPromptEditorAsync))
            {
                if (hub.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            if (_showcaseSession != null &&
                string.Equals(_showcaseSession.ProductName, productName, StringComparison.OrdinalIgnoreCase))
            {
                _showcaseSession.Theme = video.ShowcaseTheme ?? string.Empty;
                _showcaseSession.HookText = video.ShowcaseHookText ?? string.Empty;
                _showcaseSession.CtaText = video.ShowcaseCtaText ?? string.Empty;
            }

            SyncShowcaseVideoSettingsToScenes(video);
            RefreshAffiliateDeepStoryboard();
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            RefreshAiVideoGenModeReadinessLabels();
            NotifyShowcaseDraftDirty();
        }

        private async void OpenShowcaseOutputFolderFromGridCell(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            await OpenShowcaseOutputFolderForVideoAsync(video, settings, GetRunningProfileName()).ConfigureAwait(true);
        }

        private void ShowShowcaseProductTypeThemeChooser(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            using (var dlg = new ShowcaseGeminiSetupEditorForm(video))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            SyncShowcaseVideoSettingsToScenes(video);
            video.RefreshDisplayFields();
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            NotifyShowcaseDraftDirty();
        }

        private void ShowShowcaseThemeEditor(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            using (var dlg = new ShowcaseThemeEditorForm(video))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            SyncShowcaseVideoSettingsToScenes(video);
            video.RefreshDisplayFields();
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            NotifyShowcaseDraftDirty();
        }

        private void ShowShowcaseProductNameEditor(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);

            const int dialogWidth = 1120;
            const int dialogHeight = 520;

            using (var dlg = new Form
            {
                Text = "Tên sản phẩm",
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(dialogWidth, dialogHeight),
                MinimumSize = new Size(dialogWidth, dialogHeight),
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false,
                MaximizeBox = true,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                Padding = new Padding(24)
            })
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    BackColor = dlg.BackColor
                };
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

                var lbl = new Label
                {
                    Text = "Tên sản phẩm (dùng cho phiên Showcase, thư mục clip và voiceover):",
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    MaximumSize = new Size(dialogWidth - 48, 0),
                    ForeColor = Color.FromArgb(200, 204, 214),
                    Font = new Font(Font.FontFamily, 11f),
                    Margin = new Padding(0, 0, 0, 12)
                };

                var txt = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Text = video.ProductName ?? string.Empty,
                    Multiline = true,
                    WordWrap = true,
                    AcceptsReturn = false,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font(Font.FontFamily, 12f),
                    Margin = new Padding(0, 0, 0, 12),
                    MinimumSize = new Size(400, 120)
                };

                var hint = new Label
                {
                    Text = "Tên hiển thị đầy đủ trên lưới — tự xuống dòng khi dài. Sửa tại đây nếu cần.",
                    AutoSize = true,
                    ForeColor = Color.FromArgb(140, 148, 162),
                    Font = new Font(Font.FontFamily, 10f),
                    Margin = new Padding(0, 0, 0, 8),
                    MaximumSize = new Size(dialogWidth - 48, 0)
                };

                var flpButtons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = dlg.BackColor,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Width = 120,
                    Height = 44,
                    Font = new Font(Font.FontFamily, 11f),
                    Margin = new Padding(8, 0, 0, 0)
                };
                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Width = 120,
                    Height = 44,
                    Font = new Font(Font.FontFamily, 11f),
                    Margin = new Padding(0)
                };

                flpButtons.Controls.Add(btnOk);
                flpButtons.Controls.Add(btnCancel);

                root.Controls.Add(lbl, 0, 0);
                root.Controls.Add(txt, 0, 1);
                root.Controls.Add(hint, 0, 2);
                root.Controls.Add(flpButtons, 0, 3);

                dlg.Controls.Add(root);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                video.ProductName = txt.Text.Trim();
            }

            SyncShowcaseVideoSettingsToScenes(video);
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            RefreshAiVideoGenModeReadinessLabels();
            NotifyShowcaseDraftDirty();
        }

        private void ShowShowcaseScriptEditor(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var productName = (video.ProductName ?? string.Empty).Trim();
            if (_showcaseSession != null &&
                string.Equals(_showcaseSession.ProductName, productName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(video.ShowcaseHookText))
                {
                    video.ShowcaseHookText = _showcaseSession.HookText ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(video.ShowcaseCtaText))
                {
                    video.ShowcaseCtaText = _showcaseSession.CtaText ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(video.ShowcaseTheme))
                {
                    video.ShowcaseTheme = _showcaseSession.Theme ?? string.Empty;
                }
            }

            using (var dlg = new ShowcaseScriptEditorForm(video))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            if (_showcaseSession != null &&
                string.Equals(_showcaseSession.ProductName, productName, StringComparison.OrdinalIgnoreCase))
            {
                _showcaseSession.Theme = video.ShowcaseTheme ?? string.Empty;
                _showcaseSession.HookText = video.ShowcaseHookText ?? string.Empty;
                _showcaseSession.CtaText = video.ShowcaseCtaText ?? string.Empty;
            }

            SyncShowcaseVideoSettingsToScenes(video);
            RefreshAffiliateDeepStoryboard();
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            RefreshAiVideoGenModeReadinessLabels();
            NotifyShowcaseDraftDirty();
        }

        private async void ShowShowcaseScenePromptEditor(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);

            using (var dlg = new ShowcaseScenePromptEditorForm(video, ExportShowcaseExcelForVideoFromPromptEditorAsync))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            SyncShowcaseVideoSettingsToScenes(video);
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            RefreshAiVideoGenModeReadinessLabels();
            NotifyShowcaseDraftDirty();
        }

        private async Task ExportShowcaseExcelForVideoFromPromptEditorAsync()
        {
            var video = GetActiveShowcaseVideo();
            if (video == null)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            await ExportShowcaseExcelForVideoAsync(video, settings, profile).ConfigureAwait(true);
            SyncBuffersToGrids();
        }

        private async void ShowShowcaseSubtitleStyleEditor(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ShowcaseSubtitleStyleHelper.EnsureVideoDefaults(video, settings);
            using (var dlg = new ShowcaseSubtitleStyleEditorForm(video, settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            SyncShowcaseVideoSettingsToScenes(video);
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            NotifyShowcaseDraftDirty();
        }

        private async void ShowShowcaseBackgroundMusicEditor(ShowcaseVideoItem video, int gridRowIndex)
        {
            if (video == null)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ShowcaseMusicHelper.EnsureVideoDefaults(video, settings);
            var musicNames = ShowcaseMusicHelper.ListMusicFileNames(settings);
            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var profile = GetRunningProfileName();
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            EnsureShowcaseSession(profile, (video.ProductName ?? string.Empty).Trim(), settings.StorageRootPath, video);

            bool CanListenNarration()
            {
                var scenes = GetShowcaseVideoScenes(video);
                var sessionBase = _showcaseSession?.BaseDir;
                if (string.IsNullOrWhiteSpace(sessionBase))
                {
                    sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
                }

                return !string.IsNullOrWhiteSpace(sessionBase)
                       && ShowcaseNarrationCacheHelper.HasNarrationFile(sessionBase);
            }

            ShowcaseBackgroundMusicEditorForm audioDlg = null;
            audioDlg = new ShowcaseBackgroundMusicEditorForm(
                video,
                settings,
                musicNames,
                () => ListenShowcaseNarrationForVideoAsync(video, settings, profile),
                CanListenNarration,
                async () =>
                {
                    await RunShowcaseNarrationActionAsync(null, new[] { video }).ConfigureAwait(true);
                    if (audioDlg != null && !audioDlg.IsDisposed)
                    {
                        audioDlg.RefreshNarrationButtons();
                    }

                    SyncShowcaseVideoSettingsToScenes(video);
                    dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
                    NotifyShowcaseDraftDirty();
                    UpdateShowcaseNarrationButtonState();
                });

            using (audioDlg)
            {
                if (audioDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            SyncShowcaseVideoSettingsToScenes(video);
            dgvDeepDiveInput?.InvalidateRow(gridRowIndex);
            NotifyShowcaseDraftDirty();
        }

        private enum ShowcaseDualActionCellAction
        {
            None,
            Add,
            OpenFolder
        }

        private static bool IsShowcaseDualActionGridColumn(string columnName) =>
            string.Equals(columnName, "colAiShowcaseImages", StringComparison.Ordinal)
            || string.Equals(columnName, "colAiShowcaseSceneSummary", StringComparison.Ordinal);

        private const int ShowcaseImagesCellInset = 4;
        private const int ShowcaseImagesCellGap = 4;

        private static void GetShowcaseImagesCellLayout(int innerWidth, int innerHeight, out Rectangle addRect, out Rectangle folderRect)
        {
            var w = Math.Max(48, innerWidth);
            var h = Math.Max(40, innerHeight);
            var btnW = Math.Max(22, (w - ShowcaseImagesCellInset * 2 - ShowcaseImagesCellGap) / 2);
            var btnH = Math.Max(36, h - ShowcaseImagesCellInset * 2);
            addRect = new Rectangle(ShowcaseImagesCellInset, ShowcaseImagesCellInset, btnW, btnH);
            folderRect = new Rectangle(addRect.Right + ShowcaseImagesCellGap, ShowcaseImagesCellInset, btnW, btnH);
        }

        private static float ResolveShowcaseImagesIconFontSize(Rectangle bounds)
        {
            var minSide = Math.Min(bounds.Width, bounds.Height);
            return Math.Max(11f, Math.Min(14f, minSide * 0.52f));
        }

        private static Rectangle OffsetRect(Rectangle cellBounds, Rectangle local) =>
            new Rectangle(cellBounds.X + local.X, cellBounds.Y + local.Y, local.Width, local.Height);

        private static ShowcaseDualActionCellAction HitTestShowcaseDualActionCell(int innerWidth, int innerHeight, Point clickInCell)
        {
            GetShowcaseImagesCellLayout(innerWidth, innerHeight, out var addRect, out var folderRect);
            if (addRect.Contains(clickInCell))
            {
                return ShowcaseDualActionCellAction.Add;
            }

            if (folderRect.Contains(clickInCell))
            {
                return ShowcaseDualActionCellAction.OpenFolder;
            }

            return clickInCell.X < innerWidth / 2
                ? ShowcaseDualActionCellAction.Add
                : ShowcaseDualActionCellAction.OpenFolder;
        }

        private static void PaintShowcaseImagesActionButton(
            Graphics g,
            Rectangle bounds,
            string icon,
            Color iconColor,
            Color baseBack,
            Color baseBorder,
            bool selectedRow)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var back = selectedRow
                ? Color.FromArgb(
                    Math.Min(255, baseBack.R + 18),
                    Math.Min(255, baseBack.G + 18),
                    Math.Min(255, baseBack.B + 22))
                : baseBack;
            using (var brush = new SolidBrush(back))
            {
                g.FillRectangle(brush, bounds);
            }

            var border = selectedRow ? Color.FromArgb(160, 190, 255) : baseBorder;
            ControlPaint.DrawBorder(g, bounds, border, ButtonBorderStyle.Solid);

            var iconRect = Rectangle.Inflate(bounds, -3, -4);
            if (iconRect.Width <= 0 || iconRect.Height <= 0)
            {
                iconRect = bounds;
            }

            var fontSize = ResolveShowcaseImagesIconFontSize(iconRect);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var font = new Font("Segoe UI Emoji", fontSize, FontStyle.Regular, GraphicsUnit.Point))
            using (var iconBrush = new SolidBrush(iconColor))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            })
            {
                g.DrawString(icon, font, iconBrush, iconRect, format);
            }
        }

        private void DgvDeepDiveInput_ShowcaseImagesCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (!IsDeepDiveModeTab() || dgvDeepDiveInput == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = dgvDeepDiveInput.Columns[e.ColumnIndex]?.Name;
            if (!IsShowcaseDualActionGridColumn(colName))
            {
                return;
            }

            if (!(dgvDeepDiveInput.Rows[e.RowIndex].DataBoundItem is ShowcaseVideoItem video))
            {
                return;
            }

            e.Handled = true;
            var graphics = e.Graphics;
            var selected = (e.State & DataGridViewElementStates.Selected) != 0;
            var cellBounds = e.CellBounds;
            e.Paint(
                cellBounds,
                DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);

            GetShowcaseImagesCellLayout(cellBounds.Width, cellBounds.Height, out var addLocal, out var folderLocal);
            var addRect = OffsetRect(cellBounds, addLocal);
            var folderRect = OffsetRect(cellBounds, folderLocal);

            graphics.SetClip(cellBounds);
            var isClipsColumn = string.Equals(colName, "colAiShowcaseSceneSummary", StringComparison.Ordinal);
            dgvDeepDiveInput.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = isClipsColumn
                ? (video.SceneCountDisplay ?? "0 cảnh") + " — ➕ thêm clip · 📂 veo_clips"
                : (video.ShowcaseImagesGridLabel ?? "0 ảnh") + " — ➕ thêm ảnh · 📂 thư mục";

            PaintShowcaseImagesActionButton(
                graphics,
                addRect,
                "➕",
                Color.FromArgb(90, 235, 150),
                Color.FromArgb(32, 58, 46),
                Color.FromArgb(70, 130, 95),
                selected);

            PaintShowcaseImagesActionButton(
                graphics,
                folderRect,
                "📂",
                Color.FromArgb(255, 205, 90),
                Color.FromArgb(58, 50, 32),
                Color.FromArgb(140, 110, 55),
                selected);

            graphics.ResetClip();
        }

        private async void DgvDeepDiveInput_ShowcaseImagesCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (!IsDeepDiveModeTab() || dgvDeepDiveInput == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = dgvDeepDiveInput.Columns[e.ColumnIndex]?.Name;
            if (!IsShowcaseDualActionGridColumn(colName))
            {
                return;
            }

            if (!(dgvDeepDiveInput.Rows[e.RowIndex].DataBoundItem is ShowcaseVideoItem video))
            {
                return;
            }

            var display = dgvDeepDiveInput.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var action = HitTestShowcaseDualActionCell(display.Width, display.Height, new Point(e.X, e.Y));
            var isClipsColumn = string.Equals(colName, "colAiShowcaseSceneSummary", StringComparison.Ordinal);
            if (action == ShowcaseDualActionCellAction.Add)
            {
                if (isClipsColumn)
                {
                    await AddShowcaseClipsForVideoAsync(video).ConfigureAwait(true);
                }
                else
                {
                    await AddShowcaseImagesForVideoAsync(video).ConfigureAwait(true);
                }

                dgvDeepDiveInput.InvalidateCell(e.ColumnIndex, e.RowIndex);
            }
            else if (action == ShowcaseDualActionCellAction.OpenFolder)
            {
                if (isClipsColumn)
                {
                    var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                    await OpenShowcaseClipsFolderForVideoAsync(video, settings, GetRunningProfileName()).ConfigureAwait(true);
                }
                else
                {
                    await OpenShowcaseSourceImagesFolderForVideoAsync(video).ConfigureAwait(true);
                }
            }
        }
    }
}
