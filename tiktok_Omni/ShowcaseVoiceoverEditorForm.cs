using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseVoiceoverEditorForm : Form
    {
        private const int GridMinRowHeight = 72;
        private const int GridCellPad = 24;
        private const int BottomBarHeight = 96;
        private const string VoiceGridLayoutLock = "VoiceGridLayoutLock";

        private const string ColScene = "colVoiceScene";
        private const string ColClip = "colVoiceClip";
        private const string ColVoice = "colVoiceText";

        private readonly ShowcaseVideoItem _video;
        private readonly IAiVideoGenControlsHost _host;
        private ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot _speechBeforeEdit;

        private Label _lblStatus;
        private DataGridView _dgv;
        private Button _btnGenerateVoiceover;

        private sealed class VoiceRowTag
        {
            public AiVideoGenInputItem Scene { get; set; }
            public bool IsOpeningHookScene { get; set; }
            public bool IsClosingCtaScene { get; set; }
        }

        public ShowcaseVoiceoverEditorForm(ShowcaseVideoItem video, IAiVideoGenControlsHost host)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _speechBeforeEdit = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(_video);

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Lời thoại" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = ShowcasePastelTheme.ShellBg;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(1180, 720);
            ClientSize = new Size(1480, 880);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = BackColor,
                Padding = new Padding(28, 20, 28, 16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, BottomBarHeight));

            root.Controls.Add(BuildStatusPanel(), 0, 0);
            _dgv = BuildGrid();
            _dgv.Dock = DockStyle.Fill;
            _dgv.Margin = new Padding(0, 12, 0, 8);
            root.Controls.Add(_dgv, 0, 1);
            root.Controls.Add(BuildBottomBar(), 0, 2);
            Controls.Add(root);
            Shown += (_, __) =>
            {
                RefreshFromVideo();
                BeginInvoke(new Action(LayoutVoiceoverGrid));
            };
        }

        private void LayoutVoiceoverGrid()
        {
            if (_dgv == null || _dgv.IsDisposed)
            {
                return;
            }

            ResizeVoiceoverGridRows(_dgv);
        }

        private void QueueVoiceGridRowResize()
        {
            if (_dgv == null || _dgv.IsDisposed
                || string.Equals(_dgv.Tag as string, VoiceGridLayoutLock, StringComparison.Ordinal))
            {
                return;
            }

            _dgv.BeginInvoke(new Action(LayoutVoiceoverGrid));
        }

        private static void ResizeVoiceoverGridRows(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.MinimumHeight = GridMinRowHeight;
                var hScene = MeasureVoiceGridCellHeight(grid, row, ColScene);
                var hClip = MeasureVoiceGridCellHeight(grid, row, ColClip);
                var hVoice = MeasureVoiceGridCellHeight(grid, row, ColVoice);
                row.Height = Math.Max(GridMinRowHeight, Math.Max(hScene, Math.Max(hClip, hVoice)));
            }
        }

        private static int MeasureVoiceGridCellHeight(DataGridView grid, DataGridViewRow row, string colName)
        {
            if (!grid.Columns.Contains(colName))
            {
                return GridMinRowHeight;
            }

            var col = grid.Columns[colName];
            var colWidth = col.Displayed ? col.Width : col.MinimumWidth;
            if (colWidth < 48)
            {
                colWidth = colName == ColVoice ? 400 : 140;
            }

            var cell = row.Cells[colName];
            var style = cell.InheritedStyle;
            var font = style.Font ?? grid.DefaultCellStyle.Font ?? grid.Font;
            var text = cell.Value?.ToString() ?? string.Empty;
            if (text.Length == 0)
            {
                return GridMinRowHeight;
            }

            var pad = style.Padding;
            var measureWidth = Math.Max(48, colWidth - pad.Horizontal - GridCellPad);
            var size = TextRenderer.MeasureText(
                text,
                font,
                new Size(measureWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            return Math.Max(GridMinRowHeight, size.Height + pad.Vertical + GridCellPad);
        }

        private void RefreshFromVideo()
        {
            var clipsDir = (_video.ShowcaseClipsDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clipsDir) && !string.IsNullOrWhiteSpace(_video.ShowcaseSessionBaseDir))
            {
                clipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(_video.ShowcaseSessionBaseDir, createIfMissing: false);
            }

            if (!string.IsNullOrWhiteSpace(clipsDir) && Directory.Exists(clipsDir))
            {
                if (_video.Scenes?.Count == 0 && _host != null)
                {
                    ShowcaseSessionService.EnsureScenesFromRenderFolder(
                        _video,
                        clipsDir,
                        string.Empty,
                        null);
                }

                if (_video.Scenes?.Count > 0)
                {
                    ShowcaseSessionService.RefreshClipStatus(clipsDir, _video.Scenes, null);
                }
            }

            ReloadFromVideo();
        }

        public void SetStatus(string message)
        {
            if (_lblStatus == null || _lblStatus.IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetStatus(message)));
                return;
            }

            _lblStatus.Text = string.IsNullOrWhiteSpace(message) ? " " : message.Trim();
        }

        private Control BuildStatusPanel()
        {
            var panel = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(38, 42, 52),
                Padding = new Padding(14, 10, 14, 10)
            };

            _lblStatus = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2200, 0),
                Dock = DockStyle.Top,
                ForeColor = ShowcasePastelTheme.TextBody,
                Font = new Font("Segoe UI", 10F),
                Text = BuildStatusSummary()
            };

            var hint = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2200, 0),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 6, 0, 0),
                ForeColor = ShowcasePastelTheme.HintText,
                Font = new Font("Segoe UI", 9.25F),
                Text = "«File clip» = video trong clips_render (chỉ xem). «Lời thoại» = text đọc từng cảnh — bấm «Tạo lời thoại» để Gemini viết theo clip, hoặc sửa tay rồi «Lưu»."
            };

            panel.Controls.Add(hint);
            panel.Controls.Add(_lblStatus);
            return panel;
        }

        private string BuildStatusSummary()
        {
            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            return "Trạng thái: " + ShowcaseContentDisplayHelper.FormatVoiceoverGridLabel(_video)
                   + " · " + scenes.Count + " cảnh";
        }

        private DataGridView BuildGrid()
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ScrollBars = ScrollBars.Vertical,
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
                ColumnHeadersHeight = 56,
                RowTemplate = { MinimumHeight = GridMinRowHeight }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColScene,
                HeaderText = "Cảnh",
                ReadOnly = true,
                FillWeight = 18F,
                MinimumWidth = 140,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.TopLeft }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColClip,
                HeaderText = "File clip",
                ReadOnly = true,
                FillWeight = 16F,
                MinimumWidth = 120,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.TopLeft }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColVoice,
                HeaderText = "Lời thoại",
                FillWeight = 42F,
                MinimumWidth = 240,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.TopLeft }
            });

            Form1.ApplyAppGridHeaderChrome(grid);
            AppGridSttColumn.EnsureFirstColumn(grid);
            grid.CellEndEdit += (_, __) => QueueVoiceGridRowResize();
            grid.ColumnWidthChanged += (_, __) => QueueVoiceGridRowResize();
            grid.SizeChanged += (_, __) => QueueVoiceGridRowResize();
            PopulateRows(grid);
            return grid;
        }

        private void PopulateRows(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            grid.Rows.Clear();
            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            var sceneCount = scenes.Count;

            for (var i = 0; i < scenes.Count; i++)
            {
                var scene = scenes[i];
                var tag = new VoiceRowTag
                {
                    Scene = scene,
                    IsOpeningHookScene = i == 0,
                    IsClosingCtaScene = sceneCount > 0 && i == sceneCount - 1
                };
                var rowIndex = grid.Rows.Add();
                var row = grid.Rows[rowIndex];
                row.Cells[ColScene].Value = FormatSceneLabel(scene, i, sceneCount);
                row.Cells[ColClip].Value = FormatClipCell(scene);
                row.Cells[ColVoice].Value = ResolveDisplayVoice(scene, i, sceneCount);
                row.Tag = tag;
            }

            if (scenes.Count == 0)
            {
                var rowIndex = grid.Rows.Add();
                var row = grid.Rows[rowIndex];
                row.Cells[ColScene].Value = "Chưa có cảnh";
                row.Cells[ColClip].Value = "—";
                row.Cells[ColVoice].Value = "Thêm clip vào clips_render hoặc ảnh cột «Ảnh».";
                row.Tag = new VoiceRowTag();
                foreach (DataGridViewCell cell in row.Cells)
                {
                    if (!AppGridSttColumn.IsSttColumn(grid.Columns[cell.ColumnIndex]))
                    {
                        cell.ReadOnly = true;
                    }
                }
            }

            QueueVoiceGridRowResize();
        }

        private void ReloadFromVideo()
        {
            _speechBeforeEdit = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(_video);
            PopulateRows(_dgv);
            if (_lblStatus != null && !_lblStatus.IsDisposed)
            {
                _lblStatus.Text = BuildStatusSummary();
            }
        }

        private static string FormatSceneLabel(AiVideoGenInputItem scene, int index, int sceneCount)
        {
            var title = ShowcaseSceneNamingHelper.FormatDisplayLabel(scene, index);
            if (index == 0 && sceneCount > 1)
            {
                return "Hook · " + title;
            }

            if (sceneCount > 1 && index == sceneCount - 1)
            {
                return "CTA · " + title;
            }

            return title;
        }

        private string ResolveDisplayVoice(AiVideoGenInputItem scene, int sceneIndex, int sceneCount)
        {
            if (scene?.ShowcaseSceneSilent == true)
            {
                return "(im lặng)";
            }

            var voice = (scene?.SceneVoiceover ?? string.Empty).Trim();
            var hook = (_video.ShowcaseHookText ?? string.Empty).Trim();
            var cta = (_video.ShowcaseCtaText ?? string.Empty).Trim();

            if (sceneIndex == 0 && hook.Length > 0)
            {
                return hook;
            }

            if (sceneCount > 0 && sceneIndex == sceneCount - 1 && cta.Length > 0)
            {
                return cta;
            }

            if (voice.Length > 0)
            {
                return voice;
            }

            if (sceneIndex == 0 && hook.Length > 0)
            {
                return hook;
            }

            if (sceneCount > 0 && sceneIndex == sceneCount - 1 && cta.Length > 0)
            {
                return cta;
            }

            return string.Empty;
        }

        private static string FormatClipCell(AiVideoGenInputItem scene)
        {
            var path = (scene?.ClipPath ?? string.Empty).Trim();
            if (path.Length > 0 && File.Exists(path))
            {
                return "✓ " + Path.GetFileName(path);
            }

            return "✗ Chưa có clip";
        }

        private Control BuildBottomBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                BackColor = BackColor,
                Padding = new Padding(0, 8, 0, 4)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var flpLeft = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };

            _btnGenerateVoiceover = MakeButton("🎙 Tạo lời thoại", Color.FromArgb(96, 72, 120), 200);
            _btnGenerateVoiceover.Click += async (_, __) => await RunGenerateVoiceoverAsync().ConfigureAwait(true);
            flpLeft.Controls.Add(_btnGenerateVoiceover);

            var btnSave = MakeButton("Lưu", ShowcasePastelTheme.ButtonSave);
            btnSave.DialogResult = DialogResult.OK;
            AcceptButton = btnSave;
            btnSave.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };

            var btnCancel = MakeButton("Đóng", ShowcasePastelTheme.ButtonCancel);
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var flpRight = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor
            };
            flpRight.Controls.Add(btnCancel);
            flpRight.Controls.Add(btnSave);

            bar.Controls.Add(flpLeft, 0, 0);
            bar.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = BackColor }, 1, 0);
            bar.Controls.Add(flpRight, 2, 0);
            return bar;
        }

        private async Task RunGenerateVoiceoverAsync()
        {
            if (_host == null || _btnGenerateVoiceover == null)
            {
                return;
            }

            if (!_host.TryBeginShowcaseTabWork())
            {
                return;
            }

            _btnGenerateVoiceover.Enabled = false;
            try
            {
                if (!ValidateAndSave())
                {
                    return;
                }

                SetStatus("Đang gọi Gemini xem clip và tạo lời thoại…");
                await _host.GenerateShowcaseVoiceoverForVideoAsync(_video).ConfigureAwait(true);
                ReloadFromVideo();
                _host.FlushShowcaseDraftToDisk();
                SetStatus("Đã cập nhật lời thoại từ clip — phụ đề đã lấy theo lời thoại mới.");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Đã hủy.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Tạo lời thoại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus(ex.Message);
            }
            finally
            {
                _host.EndShowcaseTabWork();
                if (!_btnGenerateVoiceover.IsDisposed && !_host.IsShowcaseTabPaused)
                {
                    _btnGenerateVoiceover.Enabled = true;
                }
            }
        }

        private bool ValidateAndSave()
        {
            if (_dgv != null && !_dgv.IsDisposed && _dgv.IsCurrentCellInEditMode)
            {
                _dgv.EndEdit(DataGridViewDataErrorContexts.Commit);
            }

            if (_dgv != null && !_dgv.IsDisposed)
            {
                foreach (DataGridViewRow row in _dgv.Rows)
                {
                    if (row.IsNewRow || !(row.Tag is VoiceRowTag tag) || tag.Scene == null)
                    {
                        continue;
                    }

                    var voice = (row.Cells[ColVoice].Value?.ToString() ?? string.Empty).Trim();
                    var existing = (tag.Scene.SceneVoiceover ?? string.Empty).Trim();
                    if (voice.Length == 0 && existing.Length > 0)
                    {
                        voice = existing;
                        row.Cells[ColVoice].Value = existing;
                    }

                    if (string.Equals(voice, "(im lặng)", StringComparison.OrdinalIgnoreCase))
                    {
                        tag.Scene.ShowcaseSceneSilent = true;
                        tag.Scene.SceneVoiceover = string.Empty;
                        continue;
                    }

                    tag.Scene.SceneVoiceover = voice;
                    if (voice.Length > 0)
                    {
                        tag.Scene.ShowcaseSceneSilent = false;
                    }

                    if (tag.IsOpeningHookScene)
                    {
                        _video.ShowcaseHookText = voice;
                    }

                    if (tag.IsClosingCtaScene)
                    {
                        _video.ShowcaseCtaText = voice;
                    }
                }
            }

            _video.ApplySettingsToScenes();
            ShowcaseVoiceoverHelper.PersistHookCtaIntoSceneVoiceovers(_video, _video.Scenes);
            ShowcaseVoiceoverHelper.SyncSilentFlagsFromVoiceover(_video.Scenes);
            ShowcaseVoiceoverHelper.StampClipFingerprintIfVoiced(_video, _video.Scenes);
            ShowcaseSubtitleDisplayHelper.SyncDisplayTextFromSpeechEdits(_video, _speechBeforeEdit);
            _speechBeforeEdit = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(_video);
            _video.RefreshDisplayFields();
            return true;
        }

        private static Button MakeButton(string text, Color backColor, int minWidth = 120)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minWidth, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Margin = new Padding(6, 0, 0, 0),
                Padding = new Padding(14, 4, 14, 4),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
