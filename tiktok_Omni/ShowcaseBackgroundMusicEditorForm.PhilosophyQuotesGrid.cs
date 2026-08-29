using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm
    {
        private const int PhilosophyQuotesGridMinHeight = 96;
        private const int PhilosophyQuotesGridViewportMinHeight = 360;
        private const int PhilosophyQuotesGridRowHeight = 36;
        private const int PhilosophyQuotesGridHeaderHeight = 52;
        private const int PhilosophyQuoteCellPad = 8;
        private const int PhilosophyQuoteMinColumnWidth = 160;
        private const int PhilosophyQuoteMusicColumnMaxWidth = 208;
        private const int PhilosophyQuoteAmbientColumnMaxWidth = 156;
        private const int PhilosophyQuoteComboDropDownMinWidth = 420;
        private static readonly int PhilosophyQuotesSttColumnWidth = AppGridSttColumn.CompactColumnWidth * 2;
        private const string PhilosophyQuotesGridLayoutLock = "PhilosophyQuotesGridLayoutLock";

        private PhilosophyBatchItem _philosophyBatch;
        private Panel _philosophyQuotesGridHost;
        private DataGridView _dgvPhilosophyQuotes;
        private BindingSource _philosophyQuotesBinding;
        private Button _btnPhilosophyDeleteQuoteRows;
        private DataGridViewComboBoxColumn _colPhilosophyQuoteMusic;
        private DataGridViewComboBoxColumn _colPhilosophyQuoteAmbient;
        private readonly Dictionary<int, PhilosophyBatchAudioRowValidator.RowValidationResult> _philosophyQuoteStatusByRow =
            new Dictionary<int, PhilosophyBatchAudioRowValidator.RowValidationResult>();
        private Label _lblPhilosophyBatchScopeHint;
        private bool _philosophyProsodyExpanded;
        private JellyButton _btnPhilosophySuggestMood;
        private JellyButton _btnPhilosophyBatchVoice;
        private JellyButton _btnPhilosophyBatchRender;
        private bool _philosophyQuotesGridSyncLock;
        private bool _philosophyQuoteStatusRefreshQueued;

        private sealed class PhilosophyAmbientComboItem
        {
            public string Key { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;

            public override string ToString() => Label ?? Key ?? string.Empty;
        }

        private void EnsurePhilosophyQuotesAudioGrid()
        {
            if (!_philosophyMode || _philosophyBatch == null || _philosophyVoiceTabLayout == null)
            {
                return;
            }

            if (_dgvPhilosophyQuotes != null && !_dgvPhilosophyQuotes.IsDisposed)
            {
                RefreshPhilosophyQuotesGrid();
                return;
            }

                PhilosophyBatchHelper.EnsureBatchAudioDefaults(_philosophyBatch, _settings);

            _philosophyQuotesGridHost = new Panel
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Margin = new Padding(0, 4, 0, 0)
            };

            var quotesToolbar = CreatePhilosophyQuotesGridToolbar();
            quotesToolbar.Dock = DockStyle.Top;

            _dgvPhilosophyQuotes = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                GridColor = Color.FromArgb(58, 64, 78),
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeight = PhilosophyQuotesGridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ScrollBars = ScrollBars.Vertical,
                RowTemplate = { Height = PhilosophyQuotesGridRowHeight },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ShowCellToolTips = true,
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

            _colPhilosophyQuoteMusic = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyQuoteMusic",
                HeaderText = "Nhạc nền",
                DataPropertyName = nameof(PhilosophyScriptItem.MusicFolder),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 18,
                MinimumWidth = 150,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            };

            _colPhilosophyQuoteAmbient = new DataGridViewComboBoxColumn
            {
                Name = "colPhilosophyQuoteAmbient",
                HeaderText = "Tiếng đệm",
                DataPropertyName = nameof(PhilosophyScriptItem.AmbientKey),
                DisplayMember = nameof(PhilosophyAmbientComboItem.Label),
                ValueMember = nameof(PhilosophyAmbientComboItem.Key),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight = 12,
                MinimumWidth = 88,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            };

            var colPhilosophyQuoteVolume = new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyQuoteVolume",
                HeaderText = "volume nhạc",
                FillWeight = 6,
                MinimumWidth = 64,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            };

            _dgvPhilosophyQuotes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyQuoteMood",
                HeaderText = "Mood",
                ReadOnly = true,
                FillWeight = 6,
                MinimumWidth = 72,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    ForeColor = Color.FromArgb(160, 168, 182)
                }
            });
            _dgvPhilosophyQuotes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyQuoteContent",
                HeaderText = "Quote",
                DataPropertyName = nameof(PhilosophyScriptItem.Content),
                FillWeight = 25,
                MinimumWidth = 160,
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            });
            _dgvPhilosophyQuotes.Columns.Add(_colPhilosophyQuoteMusic);
            _dgvPhilosophyQuotes.Columns.Add(_colPhilosophyQuoteAmbient);
            _dgvPhilosophyQuotes.Columns.Add(colPhilosophyQuoteVolume);
            _dgvPhilosophyQuotes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyQuoteStatus",
                HeaderText = "TT",
                ReadOnly = true,
                FillWeight = 4,
                MinimumWidth = 40,
                ToolTipText = "✓ sẵn sàng · ◐ có thoại · ! thiếu hoặc cũ",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold)
                }
            });
            _dgvPhilosophyQuotes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhilosophyQuoteSpeed",
                HeaderText = "Tốc độ",
                FillWeight = 7,
                MinimumWidth = 72,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            });
            _dgvPhilosophyQuotes.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPhilosophyQuoteVoice",
                HeaderText = "Thoại",
                Text = "—",
                UseColumnTextForButtonValue = false,
                ReadOnly = true,
                FillWeight = 8,
                MinimumWidth = 76,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            });
            _dgvPhilosophyQuotes.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colPhilosophyQuoteFullMix",
                HeaderText = "Audio",
                Text = "—",
                UseColumnTextForButtonValue = false,
                ReadOnly = true,
                FillWeight = 14,
                MinimumWidth = 132,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            });
            _dgvPhilosophyQuotes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            Form1.ApplyAppGridChrome(_dgvPhilosophyQuotes);
            AppGridSttColumn.EnsureFirstColumn(_dgvPhilosophyQuotes, width: PhilosophyQuotesSttColumnWidth);

            _dgvPhilosophyQuotes.CellFormatting += PhilosophyQuotesGrid_CellFormatting;
            _dgvPhilosophyQuotes.CellToolTipTextNeeded += PhilosophyQuotesGrid_CellToolTipTextNeeded;
            _dgvPhilosophyQuotes.EditingControlShowing += PhilosophyQuotesGrid_EditingControlShowing;
            _dgvPhilosophyQuotes.CellValidating += PhilosophyQuotesGrid_CellValidating;
            _dgvPhilosophyQuotes.DataError += PhilosophyQuotesGrid_DataError;
            _dgvPhilosophyQuotes.CellBeginEdit += PhilosophyQuotesGrid_CellBeginEdit;
            _dgvPhilosophyQuotes.CellContentClick += PhilosophyQuotesGrid_CellContentClick;
            _dgvPhilosophyQuotes.CellMouseClick += PhilosophyQuotesGrid_CellMouseClick;
            _dgvPhilosophyQuotes.SelectionChanged += (_, __) => RefreshPhilosophyQuoteBatchActionLabels();
            _dgvPhilosophyQuotes.CellValueChanged += PhilosophyQuotesGrid_CellValueChanged;
            _dgvPhilosophyQuotes.KeyDown += PhilosophyQuotesGrid_KeyDown;
            _dgvPhilosophyQuotes.CellEndEdit += PhilosophyQuotesGrid_CellEndEdit;
            _dgvPhilosophyQuotes.ColumnWidthChanged += (_, __) => QueuePhilosophyQuotesGridRowResize();
            _dgvPhilosophyQuotes.Resize += (_, __) =>
            {
                if (IsPhilosophyQuotesGridLayoutLocked(_dgvPhilosophyQuotes))
                {
                    return;
                }

                ApplyPhilosophyQuotesGridColumnWidths();
            };
            _dgvPhilosophyQuotes.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (_dgvPhilosophyQuotes == null
                    || _dgvPhilosophyQuotes.IsDisposed
                    || IsPhilosophyQuotesGridLayoutLocked(_dgvPhilosophyQuotes)
                    || !_dgvPhilosophyQuotes.IsCurrentCellDirty
                    || !_dgvPhilosophyQuotes.IsCurrentCellInEditMode
                    || !(_dgvPhilosophyQuotes.CurrentCell is DataGridViewComboBoxCell))
                {
                    return;
                }

                try
                {
                    _dgvPhilosophyQuotes.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
                catch (InvalidOperationException)
                {
                    // Combo auto-fill from binding — ignore.
                }
            };

            InitializePhilosophyQuoteComboItems();
            BindPhilosophyQuotesGrid();

            _philosophyQuotesGridHost.Controls.Add(_dgvPhilosophyQuotes);
            _philosophyQuotesGridHost.Controls.Add(quotesToolbar);
            _dgvPhilosophyQuotes.DataBindingComplete += (_, __) => QueuePhilosophyQuotesGridRowResize();
            ApplyPhilosophyQuotesGridColumnWidths();
            QueuePhilosophyQuotesGridRowResize();
            LayoutPhilosophyQuotesGrid();
            RefreshPhilosophyQuoteBatchActionLabels();
            RefreshPhilosophyQuoteRowStatuses();

            _philosophyVoiceTabLayout.Controls.Add(_philosophyQuotesGridHost, 0, 1);
        }

        private void RefreshPhilosophyQuoteRowStatuses()
        {
            if (_dgvPhilosophyQuotes == null || _philosophyBatch?.Quotes == null)
            {
                return;
            }

            var sessionBase = GetPhilosophyQuotesSessionBase();
            _philosophyQuoteStatusByRow.Clear();
            for (var i = 0; i < _dgvPhilosophyQuotes.Rows.Count; i++)
            {
                var quote = i < _philosophyBatch.Quotes.Count ? _philosophyBatch.Quotes[i] : null;
                var status = PhilosophyBatchAudioRowValidator.Validate(
                    quote,
                    i,
                    _philosophyBatch,
                    sessionBase,
                    _video,
                    _settings);
                _philosophyQuoteStatusByRow[i] = status;
            }

            if (_dgvPhilosophyQuotes.Columns.Contains("colPhilosophyQuoteStatus"))
            {
                _dgvPhilosophyQuotes.InvalidateColumn(_dgvPhilosophyQuotes.Columns["colPhilosophyQuoteStatus"].Index);
            }
        }

        private static bool IsPhilosophyQuotesGridLayoutLocked(DataGridView grid) =>
            string.Equals(grid?.Tag as string, PhilosophyQuotesGridLayoutLock, StringComparison.Ordinal);

        private void WithPhilosophyQuotesGridLayoutLock(Action action)
        {
            if (_dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.IsDisposed || action == null)
            {
                return;
            }

            var prevTag = _dgvPhilosophyQuotes.Tag;
            _dgvPhilosophyQuotes.Tag = PhilosophyQuotesGridLayoutLock;
            try
            {
                action();
            }
            finally
            {
                _dgvPhilosophyQuotes.Tag = prevTag;
            }
        }

        private void QueuePhilosophyQuotesGridRowResize()
        {
            if (_dgvPhilosophyQuotes == null
                || _dgvPhilosophyQuotes.IsDisposed
                || IsPhilosophyQuotesGridLayoutLocked(_dgvPhilosophyQuotes))
            {
                return;
            }

            if (!_dgvPhilosophyQuotes.IsHandleCreated)
            {
                ResizePhilosophyQuotesGridRows();
                return;
            }

            _dgvPhilosophyQuotes.BeginInvoke(new Action(ResizePhilosophyQuotesGridRows));
        }

        private Control CreatePhilosophyQuotesGridToolbar()
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(0, 10, 0, 10)
            };

            _btnPhilosophyDeleteQuoteRows = new Button
            {
                Text = "Xóa dòng",
                AutoSize = true,
                MinimumSize = new Size(108, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 58, 58),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 4, 8, 4),
                Padding = new Padding(12, 6, 12, 6),
                UseVisualStyleBackColor = false
            };
            _btnPhilosophyDeleteQuoteRows.FlatAppearance.BorderSize = 0;
            _btnPhilosophyDeleteQuoteRows.Click += (_, __) => DeleteSelectedPhilosophyQuotes();

            panel.Controls.Add(_btnPhilosophyDeleteQuoteRows);
            panel.Controls.Add(CreatePhilosophyToolbarButton(
                "📁 Thư viện nhạc",
                Color.FromArgb(52, 92, 118),
                (_, __) => OpenPhilosophyAudioLibraryFolder(OmniAudioLibrary.GetSharedMusicDirectory(_settings), "Thư viện nhạc")));
            panel.Controls.Add(CreatePhilosophyToolbarButton(
                "📁 Thư viện SFX",
                Color.FromArgb(72, 88, 118),
                (_, __) => OpenPhilosophyAudioLibraryFolder(OmniAudioLibrary.GetSharedSfxDirectory(_settings), "Thư viện SFX")));

            var hint = new Label
            {
                Text = "Chuột phải Volume/Tốc độ → preset · Đổi tốc độ = FFmpeg chỉnh thoại (không TTS lại) · TT = trạng thái",
                AutoSize = true,
                MaximumSize = new Size(1200, 0),
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.25F),
                Margin = new Padding(8, 10, 0, 0)
            };
            panel.Controls.Add(hint);
            return panel;
        }

        private static Button CreatePhilosophyToolbarButton(string text, Color back, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(120, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.25F),
                Margin = new Padding(0, 4, 8, 4),
                Padding = new Padding(10, 6, 10, 6),
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void OpenPhilosophyAudioLibraryFolder(string folder, string title)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                }

                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyMoodSuggestionsForSelectedOrAllRows()
        {
            if (_philosophyBatch?.Quotes == null)
            {
                return;
            }

            CommitPhilosophyQuotesGridEdits();
            var indices = GetSelectedPhilosophyQuoteRowIndices();
            if (indices.Count == 0)
            {
                indices = Enumerable.Range(0, _philosophyBatch.Quotes.Count).ToList();
            }

            var profile = PhilosophyBatchHelper.ResolveBatchProfileName(_philosophyBatch, _video?.ProfileName);
            foreach (var i in indices)
            {
                if (i < 0 || i >= _philosophyBatch.Quotes.Count)
                {
                    continue;
                }

                PhilosophyBatchAudioSuggestionHelper.ApplyMoodSuggestion(
                    _philosophyBatch.Quotes[i],
                    _philosophyBatch,
                    _settings,
                    profile);
            }

            RefreshPhilosophyQuotesGrid();
            SetOperationStatus("Đã gợi ý nhạc + tiếng đệm theo mood cho " + indices.Count + " dòng.");
        }

        private async Task RunPhilosophyBatchVoiceAllAsync()
        {
            if (_generateBodyNarrationAsync == null)
            {
                return;
            }

            _dgvPhilosophyQuotes?.ClearSelection();
            SetOperationStatus("Đang tạo thoại cả batch…");
            try
            {
                await _generateBodyNarrationAsync().ConfigureAwait(true);
                RefreshPhilosophyQuoteAudioUi();
                SetOperationStatus("Tạo thoại cả batch xong.");
            }
            catch (Exception ex)
            {
                SetOperationStatus("Lỗi tạo thoại: " + ex.Message);
            }
        }

        private async Task RunPhilosophyBatchRenderAllAsync()
        {
            if (_renderFullMixedAudioAsync == null)
            {
                return;
            }

            _dgvPhilosophyQuotes?.ClearSelection();
            SetOperationStatus("Đang render mix cả batch…");
            try
            {
                await _renderFullMixedAudioAsync().ConfigureAwait(true);
                RefreshPhilosophyQuoteAudioUi();
                SetOperationStatus("Render mix cả batch xong.");
            }
            catch (Exception ex)
            {
                SetOperationStatus("Lỗi render mix: " + ex.Message);
            }
        }

        private void AttachPhilosophyQuoteBatchJellyButtons()
        {
            if (!_philosophyMode || _bodyVoice?.NarrationButtonRow == null || _btnPhilosophySuggestMood != null)
            {
                return;
            }

            _btnPhilosophySuggestMood = CreateVoiceNarrationJellyButton(
                "btnPhilosophySuggestMood",
                "Gợi ý theo mood",
                Color.FromArgb(92, 72, 128));
            _btnPhilosophySuggestMood.Click += (_, __) => ApplyMoodSuggestionsForSelectedOrAllRows();

            _btnPhilosophyBatchVoice = CreateVoiceNarrationJellyButton(
                "btnPhilosophyBatchVoice",
                "Tạo thoại cả batch",
                Color.FromArgb(56, 108, 88));
            _btnPhilosophyBatchVoice.Click += async (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    return;
                }

                _btnPhilosophyBatchVoice.Enabled = false;
                try
                {
                    await RunPhilosophyBatchVoiceAllAsync().ConfigureAwait(true);
                }
                finally
                {
                    _btnPhilosophyBatchVoice.Enabled = true;
                    RefreshNarrationButtons();
                }
            };

            _btnPhilosophyBatchRender = CreateVoiceNarrationJellyButton(
                "btnPhilosophyBatchRender",
                "Render cả batch",
                Color.FromArgb(68, 98, 88));
            _btnPhilosophyBatchRender.Click += async (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    return;
                }

                _btnPhilosophyBatchRender.Enabled = false;
                try
                {
                    await RunPhilosophyBatchRenderAllAsync().ConfigureAwait(true);
                }
                finally
                {
                    _btnPhilosophyBatchRender.Enabled = true;
                    RefreshNarrationButtons();
                }
            };

            _bodyVoice.NarrationButtonRow.Controls.Add(_btnPhilosophySuggestMood);
            _bodyVoice.NarrationButtonRow.Controls.Add(_btnPhilosophyBatchVoice);
            _bodyVoice.NarrationButtonRow.Controls.Add(_btnPhilosophyBatchRender);
        }

        private IReadOnlyList<int> GetSelectedPhilosophyQuoteRowIndices()
        {
            if (_dgvPhilosophyQuotes == null)
            {
                return Array.Empty<int>();
            }

            return _dgvPhilosophyQuotes.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => r.Index >= 0 && !r.IsNewRow)
                .Select(r => r.Index)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
        }

        private void DeleteSelectedPhilosophyQuotes()
        {
            if (!_philosophyMode || _philosophyBatch?.Quotes == null || _dgvPhilosophyQuotes == null)
            {
                return;
            }

            CommitPhilosophyQuotesGridEdits();
            var indices = GetSelectedPhilosophyQuoteRowIndices();
            if (indices.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn một hoặc nhiều dòng trên lưới để xóa.",
                    "Xóa dòng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_philosophyBatch.Quotes.Count - indices.Count < 1)
            {
                MessageBox.Show(this,
                    "Batch cần giữ ít nhất một câu quote.",
                    "Xóa dòng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var prompt = indices.Count == 1
                ? "Xóa dòng quote đã chọn?\r\n\r\nAudio preview của batch sẽ bị xóa — tạo lại sau khi xóa."
                : "Xóa " + indices.Count.ToString() + " dòng quote đã chọn?\r\n\r\nAudio preview của batch sẽ bị xóa — tạo lại sau khi xóa.";
            if (MessageBox.Show(this,
                    prompt,
                    "Xóa dòng",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            var sessionBase = GetPhilosophyQuotesSessionBase();
            PhilosophyBatchAudioPreviewHelper.ClearAllQuotePreviewFiles(
                sessionBase,
                _philosophyBatch.Quotes.Count);

            foreach (var index in indices.OrderByDescending(i => i))
            {
                if (index >= 0 && index < _philosophyBatch.Quotes.Count)
                {
                    _philosophyBatch.Quotes.RemoveAt(index);
                }
            }

            _philosophyBatch.QuoteCount = Math.Max(1, _philosophyBatch.Quotes.Count);
            _philosophyBatch.RefreshDerivedFields();
            _philosophyQuotesBinding?.ResetBindings(false);
            RefreshPhilosophyQuotesGrid();
            RefreshNarrationButtons();
            SetOperationStatus("Đã xóa " + indices.Count + " dòng · tạo lại audio nếu cần.");
        }

        private void PhilosophyQuotesGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && !IsPhilosophyQuoteGridEditingText())
            {
                e.Handled = true;
                DeleteSelectedPhilosophyQuotes();
            }
        }

        private bool IsPhilosophyQuoteGridEditingText()
        {
            if (_dgvPhilosophyQuotes == null || !_dgvPhilosophyQuotes.IsCurrentCellInEditMode)
            {
                return false;
            }

            return _dgvPhilosophyQuotes.CurrentCell is DataGridViewTextBoxCell;
        }

        private void PhilosophyQuotesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteContent")
            {
                if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quote)
                {
                    quote.Content = (quote.Content ?? string.Empty).Trim();
                }

                var sessionBase = GetPhilosophyQuotesSessionBase();
                PhilosophyBatchAudioPreviewHelper.DeleteQuotePreviewFiles(sessionBase, e.RowIndex);
                QueuePhilosophyQuotesGridRowResize();
                _dgvPhilosophyQuotes.InvalidateRow(e.RowIndex);
                RefreshNarrationButtons();
                RefreshPhilosophyQuoteRowStatuses();
                SetOperationStatus("Quote dòng " + (e.RowIndex + 1) + " đổi — cần «Tạo lại thoại».");
                return;
            }

            if (colName == "colPhilosophyQuoteMusic"
                || colName == "colPhilosophyQuoteAmbient")
            {
                var sessionBase = GetPhilosophyQuotesSessionBase();
                PhilosophyBatchAudioPreviewHelper.DeleteQuotePreviewFiles(sessionBase, e.RowIndex);
                QueuePhilosophyQuoteRowStatusRefresh();
            }
        }

        private void PhilosophyQuotesGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (!(_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quote))
            {
                return;
            }

            if (colName == "colPhilosophyQuoteVolume")
            {
                var volume = PhilosophyBatchHelper.ResolveQuoteMusicVolumePercent(quote, _philosophyBatch);
                _dgvPhilosophyQuotes.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = volume.ToString(CultureInfo.InvariantCulture);
                return;
            }

            if (colName == "colPhilosophyQuoteSpeed")
            {
                var speed = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(quote, _philosophyBatch);
                _dgvPhilosophyQuotes.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = speed.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void PhilosophyQuotesGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            QueuePhilosophyQuotesGridRowResize();

            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0 || _philosophyQuotesGridSyncLock)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteVolume" || colName == "colPhilosophyQuoteSpeed")
            {
                TryCommitPhilosophyQuoteNumericCell(e.RowIndex, colName, invalidateOnly: true);
                if (colName == "colPhilosophyQuoteSpeed")
                {
                    _ = ReapplyPhilosophyQuoteVoiceSpeedForRowAsync(e.RowIndex);
                }
            }
        }

        private async Task ReapplyPhilosophyBatchVoiceSpeedAsync()
        {
            if (_philosophyBatch?.Quotes == null)
            {
                return;
            }

            PhilosophyBatchTtsHelper.SyncBodyVoiceToHookTrack(_video);
            var indices = Enumerable.Range(0, _philosophyBatch.Quotes.Count).ToList();
            SetOperationStatus("Đang chỉnh tốc độ thoại cả batch…");
            try
            {
                await PhilosophyBatchAudioPreviewHelper.ReapplyQuoteVoiceSpeedBatchAsync(
                    _philosophyBatch,
                    _video,
                    _settings,
                    indices,
                    null).ConfigureAwait(true);
                RefreshPhilosophyQuoteAudioUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Tốc độ thoại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task ReapplyPhilosophyQuoteVoiceSpeedForRowAsync(int rowIndex)
        {
            if (_philosophyBatch?.Quotes == null || rowIndex < 0 || rowIndex >= _philosophyBatch.Quotes.Count)
            {
                return;
            }

            var quote = _philosophyBatch.Quotes[rowIndex];
            if (quote == null || string.IsNullOrWhiteSpace(quote.Content))
            {
                return;
            }

            var sessionBase = GetPhilosophyQuotesSessionBase();
            if (!PhilosophyBatchAudioPreviewHelper.HasQuoteVoicePreview(sessionBase, rowIndex))
            {
                QueuePhilosophyQuoteRowStatusRefresh();
                return;
            }

            PhilosophyBatchTtsHelper.SyncBodyVoiceToHookTrack(_video);
            SetOperationStatus("Đang chỉnh tốc độ thoại dòng " + (rowIndex + 1) + "…");
            try
            {
                await PhilosophyBatchAudioPreviewHelper.ReapplyQuoteVoiceSpeedAsync(
                    sessionBase,
                    rowIndex,
                    quote,
                    _philosophyBatch,
                    _video,
                    _settings,
                    null).ConfigureAwait(true);
                RefreshPhilosophyQuoteAudioUi();
                SetOperationStatus("Tốc độ dòng " + (rowIndex + 1) + " đã cập nhật — bấm «Render audio» để ghép mix.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Tốc độ thoại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetOperationStatus(string.Empty);
            }
        }

        internal void RefreshPhilosophyQuoteAudioUi()
        {
            RefreshPhilosophyQuoteRowStatuses();
            RefreshNarrationButtons();
            RefreshPhilosophyQuoteBatchActionLabels();
            if (_dgvPhilosophyQuotes == null)
            {
                return;
            }

            foreach (var colName in new[] { "colPhilosophyQuoteStatus", "colPhilosophyQuoteVoice", "colPhilosophyQuoteFullMix" })
            {
                if (_dgvPhilosophyQuotes.Columns.Contains(colName))
                {
                    _dgvPhilosophyQuotes.InvalidateColumn(_dgvPhilosophyQuotes.Columns[colName].Index);
                }
            }
        }

        private void QueuePhilosophyQuoteRowStatusRefresh()
        {
            if (_philosophyQuoteStatusRefreshQueued || _dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.IsDisposed)
            {
                return;
            }

            _philosophyQuoteStatusRefreshQueued = true;
            BeginInvoke(new Action(() =>
            {
                _philosophyQuoteStatusRefreshQueued = false;
                if (_dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.IsDisposed)
                {
                    return;
                }

                RefreshPhilosophyQuoteRowStatuses();
            }));
        }

        private bool TryCommitPhilosophyQuoteNumericCell(int rowIndex, string colName, bool invalidateOnly)
        {
            if (_philosophyBatch?.Quotes == null || rowIndex < 0 || rowIndex >= _philosophyBatch.Quotes.Count || _dgvPhilosophyQuotes == null)
            {
                return false;
            }

            var quote = _philosophyBatch.Quotes[rowIndex];
            if (quote == null || !_dgvPhilosophyQuotes.Columns.Contains(colName))
            {
                return false;
            }

            var cell = _dgvPhilosophyQuotes.Rows[rowIndex].Cells[colName];
            var text = (cell.EditedFormattedValue ?? cell.Value)?.ToString()?.Trim().TrimEnd('%') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            var changed = false;
            if (colName == "colPhilosophyQuoteVolume")
            {
                var volume = PhilosophyBatchHelper.ClampMusicVolumePercent(parsed);
                changed = quote.MusicVolumePercent != volume;
                quote.MusicVolumePercent = volume;
            }
            else if (colName == "colPhilosophyQuoteSpeed")
            {
                var speed = PhilosophyBatchHelper.ClampNarrationSpeedPercent(parsed);
                changed = quote.NarrationSpeedPercent != speed;
                quote.NarrationSpeedPercent = speed;
            }
            else
            {
                return false;
            }

            if (!changed && invalidateOnly)
            {
                return true;
            }

            if (colName == "colPhilosophyQuoteVolume")
            {
                var sessionBase = GetPhilosophyQuotesSessionBase();
                PhilosophyBatchAudioPreviewHelper.DeleteQuotePreviewFiles(sessionBase, rowIndex);
            }

            QueuePhilosophyQuoteRowStatusRefresh();
            if (colName == "colPhilosophyQuoteSpeed")
            {
                SetOperationStatus("Tốc độ dòng " + (rowIndex + 1) + " → " + parsed + "%…");
            }

            _dgvPhilosophyQuotes.InvalidateCell(_dgvPhilosophyQuotes.Columns[colName].Index, rowIndex);
            return true;
        }

        private void ApplyPhilosophyBatchSpeedToAllQuotes(int speedPercent)
        {
            if (_philosophyBatch?.Quotes == null)
            {
                return;
            }

            var speed = PhilosophyBatchHelper.ClampNarrationSpeedPercent(speedPercent);
            _philosophyBatch.BodyNarrationSpeedPercent = speed;
            foreach (var quote in _philosophyBatch.Quotes)
            {
                if (quote != null)
                {
                    quote.NarrationSpeedPercent = speed;
                }
            }

            if (_dgvPhilosophyQuotes != null && _dgvPhilosophyQuotes.Columns.Contains("colPhilosophyQuoteSpeed"))
            {
                var col = _dgvPhilosophyQuotes.Columns["colPhilosophyQuoteSpeed"].Index;
                for (var i = 0; i < _dgvPhilosophyQuotes.Rows.Count; i++)
                {
                    _dgvPhilosophyQuotes.InvalidateCell(col, i);
                }
            }

            QueuePhilosophyQuoteRowStatusRefresh();
        }

        private Task PlayPhilosophyQuotePreviewAsync(int rowIndex, bool fullMix)
        {
            var sessionBase = GetPhilosophyQuotesSessionBase();
            var path = fullMix
                ? PhilosophyBatchAudioPreviewHelper.GetQuoteFullMixPreviewPath(sessionBase, rowIndex)
                : PhilosophyBatchAudioPreviewHelper.GetQuoteVoicePreviewPath(sessionBase, rowIndex);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                if (fullMix)
                {
                    MessageBox.Show(
                        this,
                        "Chưa có audio thành phẩm cho dòng này. Bấm «Render audio» trước.",
                        "Nghe audio",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return Task.CompletedTask;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Nghe audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return Task.CompletedTask;
        }

        private static string FormatPhilosophyMoodLabel(string mood)
        {
            var m = (mood ?? "reflective").Trim().ToLowerInvariant();
            switch (m)
            {
                case "calm": return "calm";
                case "hopeful": return "hopeful";
                case "melancholic": return "melancholic";
                case "intense": return "intense";
                default: return "reflective";
            }
        }

        private void PhilosophyQuotesGrid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0 || _dgvPhilosophyQuotes == null)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteVolume")
            {
                ShowPhilosophyVolumePresetMenu(e.RowIndex);
                return;
            }

            if (colName == "colPhilosophyQuoteSpeed")
            {
                ShowPhilosophySpeedPresetMenu(e.RowIndex);
            }
        }

        private void ShowPhilosophyVolumePresetMenu(int rowIndex)
        {
            if (_philosophyBatch?.Quotes == null || rowIndex < 0 || rowIndex >= _philosophyBatch.Quotes.Count)
            {
                return;
            }

            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };
            foreach (var pct in new[] { 10, 15, 20, 25, 30 })
            {
                var value = pct;
                menu.Items.Add(value + "%", null, (_, __) => ApplyPhilosophyQuoteVolume(rowIndex, value));
            }

            menu.Show(Cursor.Position);
        }

        private void ShowPhilosophySpeedPresetMenu(int rowIndex)
        {
            if (_philosophyBatch?.Quotes == null || rowIndex < 0 || rowIndex >= _philosophyBatch.Quotes.Count)
            {
                return;
            }

            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };
            foreach (var pct in new[] { 70, 80, 90, 100, 110 })
            {
                var value = pct;
                menu.Items.Add(value + "%", null, (_, __) => ApplyPhilosophyQuoteSpeed(rowIndex, value));
            }

            menu.Show(Cursor.Position);
        }

        private void ApplyPhilosophyQuoteVolume(int rowIndex, int volume)
        {
            if (_philosophyBatch?.Quotes == null || rowIndex < 0 || rowIndex >= _philosophyBatch.Quotes.Count)
            {
                return;
            }

            _philosophyBatch.Quotes[rowIndex].MusicVolumePercent = PhilosophyBatchHelper.ClampMusicVolumePercent(volume);
            var sessionBase = GetPhilosophyQuotesSessionBase();
            PhilosophyBatchAudioPreviewHelper.DeleteQuotePreviewFiles(sessionBase, rowIndex);
            if (_dgvPhilosophyQuotes != null && _dgvPhilosophyQuotes.Columns.Contains("colPhilosophyQuoteVolume"))
            {
                _dgvPhilosophyQuotes.InvalidateCell(_dgvPhilosophyQuotes.Columns["colPhilosophyQuoteVolume"].Index, rowIndex);
            }

            QueuePhilosophyQuoteRowStatusRefresh();
        }

        private void ApplyPhilosophyQuoteSpeed(int rowIndex, int speed)
        {
            if (_philosophyBatch?.Quotes == null || rowIndex < 0 || rowIndex >= _philosophyBatch.Quotes.Count)
            {
                return;
            }

            _philosophyBatch.Quotes[rowIndex].NarrationSpeedPercent = PhilosophyBatchHelper.ClampNarrationSpeedPercent(speed);
            if (_dgvPhilosophyQuotes != null && _dgvPhilosophyQuotes.Columns.Contains("colPhilosophyQuoteSpeed"))
            {
                _dgvPhilosophyQuotes.InvalidateCell(_dgvPhilosophyQuotes.Columns["colPhilosophyQuoteSpeed"].Index, rowIndex);
            }

            _ = ReapplyPhilosophyQuoteVoiceSpeedForRowAsync(rowIndex);
        }

        private void ResizePhilosophyQuotesGridRows()
        {
            if (_dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.IsDisposed || _dgvPhilosophyQuotes.Rows.Count == 0)
            {
                return;
            }

            SafeEndPhilosophyQuotesGridEdit();

            foreach (DataGridViewRow row in _dgvPhilosophyQuotes.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.MinimumHeight = PhilosophyQuotesGridRowHeight;
                try
                {
                    row.Height = MeasurePhilosophyQuoteCellHeight(row);
                }
                catch (InvalidOperationException)
                {
                    row.Height = PhilosophyQuotesGridRowHeight;
                }
            }

            _philosophyQuotesGridHost?.PerformLayout();
        }

        private int MeasurePhilosophyQuotesToolbarHeight()
        {
            if (_philosophyQuotesGridHost == null)
            {
                return 0;
            }

            foreach (Control c in _philosophyQuotesGridHost.Controls)
            {
                if (ReferenceEquals(c, _dgvPhilosophyQuotes))
                {
                    continue;
                }

                c.PerformLayout();
                return c.Height + c.Margin.Top + c.Margin.Bottom;
            }

            return 0;
        }

        private int MeasurePhilosophyQuotesRowsHeight()
        {
            if (_dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.Rows.Count == 0)
            {
                var count = Math.Max(1, _philosophyBatch?.Quotes?.Count ?? 1);
                return count * PhilosophyQuotesGridRowHeight;
            }

            var total = 0;
            foreach (DataGridViewRow row in _dgvPhilosophyQuotes.Rows)
            {
                if (!row.IsNewRow)
                {
                    total += row.Height;
                }
            }

            return Math.Max(PhilosophyQuotesGridRowHeight, total);
        }

        private int MeasurePhilosophyQuotesTotalBodyHeight()
        {
            if (_dgvPhilosophyQuotes == null)
            {
                return PhilosophyQuotesGridMinHeight;
            }

            var headerH = _dgvPhilosophyQuotes.ColumnHeadersVisible
                ? _dgvPhilosophyQuotes.ColumnHeadersHeight
                : 0;
            return headerH + MeasurePhilosophyQuotesRowsHeight() + 2;
        }

        private int MeasurePhilosophyQuoteCellHeight(DataGridViewRow row)
        {
            if (_dgvPhilosophyQuotes == null || row == null || !_dgvPhilosophyQuotes.Columns.Contains("colPhilosophyQuoteContent"))
            {
                return PhilosophyQuotesGridRowHeight;
            }

            var col = _dgvPhilosophyQuotes.Columns["colPhilosophyQuoteContent"];
            var colWidth = col.Displayed ? col.Width : col.MinimumWidth;
            if (colWidth < 48)
            {
                colWidth = col.MinimumWidth > 0 ? col.MinimumWidth : 320;
            }

            var cell = row.Cells[col.Index];
            var style = cell.InheritedStyle;
            var font = style.Font ?? _dgvPhilosophyQuotes.DefaultCellStyle.Font ?? _dgvPhilosophyQuotes.Font;
            var text = cell.FormattedValue?.ToString() ?? cell.Value?.ToString() ?? string.Empty;
            if (text.Length == 0)
            {
                return PhilosophyQuotesGridRowHeight;
            }

            var pad = style.Padding;
            var measureWidth = Math.Max(48, colWidth - pad.Horizontal - PhilosophyQuoteCellPad);
            var size = TextRenderer.MeasureText(
                text,
                font,
                new Size(measureWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            return Math.Max(PhilosophyQuotesGridRowHeight, size.Height + pad.Vertical + PhilosophyQuoteCellPad);
        }

        private void BindPhilosophyQuotesGrid()
        {
            if (_dgvPhilosophyQuotes == null || _philosophyBatch?.Quotes == null)
            {
                return;
            }

            foreach (var quote in _philosophyBatch.Quotes)
            {
                if (quote == null)
                {
                    continue;
                }

                PhilosophyAmbientCatalog.EnsureRowDefault(quote);
                quote.Content = PhilosophyGeminiTtsContext.FixQuotePeriods(quote.Content);
                EnsurePhilosophyQuoteAmbientInCombo(quote.AmbientKey);
                EnsurePhilosophyQuoteMusicInCombo(quote.MusicFolder);
                if (quote.MusicVolumePercent <= 0)
                {
                    quote.MusicVolumePercent = PhilosophyBatchHelper.ResolveBatchMusicVolumePercent(_philosophyBatch);
                }

                if (quote.NarrationSpeedPercent <= 0)
                {
                    quote.NarrationSpeedPercent = PhilosophyBatchHelper.ResolveBatchNarrationSpeedPercent(_philosophyBatch);
                }
            }

            _philosophyQuotesBinding = new BindingSource
            {
                DataSource = _philosophyBatch.Quotes
            };
            WithPhilosophyQuotesGridLayoutLock(() => _dgvPhilosophyQuotes.DataSource = _philosophyQuotesBinding);
        }

        internal void RefreshPhilosophyQuotesGrid()
        {
            if (_philosophyBatch == null)
            {
                return;
            }

                PhilosophyBatchHelper.EnsureBatchAudioDefaults(_philosophyBatch, _settings);
            MergePhilosophyQuoteComboItems();
            if (_philosophyQuotesBinding == null)
            {
                BindPhilosophyQuotesGrid();
            }
            else
            {
                WithPhilosophyQuotesGridLayoutLock(() => _philosophyQuotesBinding.ResetBindings(false));
            }

            _dgvPhilosophyQuotes?.Invalidate();
            ApplyPhilosophyQuotesGridColumnWidths();
            QueuePhilosophyQuotesGridRowResize();
            LayoutPhilosophyQuotesGrid();
            RefreshPhilosophyQuoteBatchActionLabels();
            RefreshPhilosophyQuoteRowStatuses();
        }

        private void InitializePhilosophyQuoteComboItems()
        {
            if (_colPhilosophyQuoteMusic == null || _colPhilosophyQuoteAmbient == null)
            {
                return;
            }

            _colPhilosophyQuoteMusic.Items.Clear();
            _colPhilosophyQuoteMusic.Items.Add(string.Empty);
            foreach (var name in _musicNames ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _colPhilosophyQuoteMusic.Items.Add(name);
                }
            }

            _colPhilosophyQuoteAmbient.Items.Clear();
            foreach (var key in PhilosophyAmbientCatalog.AllKeys)
            {
                _colPhilosophyQuoteAmbient.Items.Add(CreatePhilosophyAmbientComboItem(key));
            }

            MergePhilosophyQuoteComboItems();
        }

        private void MergePhilosophyQuoteComboItems()
        {
            if (_colPhilosophyQuoteMusic == null || _colPhilosophyQuoteAmbient == null)
            {
                return;
            }

            foreach (var name in _musicNames ?? Enumerable.Empty<string>())
            {
                EnsurePhilosophyQuoteMusicInCombo(name);
            }

            if (_philosophyBatch?.Quotes != null)
            {
                foreach (var quote in _philosophyBatch.Quotes)
                {
                    if (quote == null)
                    {
                        continue;
                    }

                    EnsurePhilosophyQuoteMusicInCombo(quote.MusicFolder);
                    EnsurePhilosophyQuoteAmbientInCombo(quote.AmbientKey);
                }
            }
        }

        private void RefreshPhilosophyQuoteComboItems()
        {
            MergePhilosophyQuoteComboItems();
        }

        private void AppendPhilosophyQuoteSfxComboItems()
        {
            if (_colPhilosophyQuoteAmbient == null
                || _dgvPhilosophyQuotes == null
                || _dgvPhilosophyQuotes.IsDisposed)
            {
                return;
            }

            WithPhilosophyQuotesGridLayoutLock(() =>
            {
                OmniAudioLibrary.EnsureSharedDirectoriesExist(_settings);
                foreach (var sfx in OmniAudioLibrary.ListSfxFileNames(_settings))
                {
                    EnsurePhilosophyQuoteAmbientInCombo(sfx);
                }
            });
        }

        private void EnsurePhilosophyQuoteMusicInCombo(string fileName)
        {
            if (_colPhilosophyQuoteMusic == null)
            {
                return;
            }

            var trimmed = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            foreach (var item in _colPhilosophyQuoteMusic.Items)
            {
                if (string.Equals(item?.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _colPhilosophyQuoteMusic.Items.Add(trimmed);
        }

        private void EnsurePhilosophyQuoteAmbientInCombo(string ambientKey)
        {
            if (_colPhilosophyQuoteAmbient == null)
            {
                return;
            }

            var key = PhilosophyAmbientCatalog.NormalizeKey(ambientKey);
            if (PhilosophyAmbientCatalog.IsMediaFileName(ambientKey))
            {
                key = System.IO.Path.GetFileName(ambientKey.Trim());
            }

            foreach (var item in _colPhilosophyQuoteAmbient.Items)
            {
                if (item is PhilosophyAmbientComboItem ambient
                    && string.Equals(ambient.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _colPhilosophyQuoteAmbient.Items.Add(CreatePhilosophyAmbientComboItem(key));
        }

        private static PhilosophyAmbientComboItem CreatePhilosophyAmbientComboItem(string key)
        {
            var normalized = PhilosophyAmbientCatalog.NormalizeKey(key);
            if (PhilosophyAmbientCatalog.IsMediaFileName(key))
            {
                normalized = System.IO.Path.GetFileName(key.Trim());
            }

            return new PhilosophyAmbientComboItem
            {
                Key = normalized,
                Label = PhilosophyAmbientCatalog.GetLabel(normalized)
            };
        }

        internal int MeasurePhilosophyQuotesGridAreaMinHeight()
        {
            var toolbarH = _philosophyQuotesGridHost != null
                ? MeasurePhilosophyQuotesToolbarHeight()
                : 40;
            return toolbarH + PhilosophyQuotesGridViewportMinHeight + (_philosophyQuotesGridHost?.Padding.Vertical ?? 0);
        }

        private void LayoutPhilosophyQuotesGrid()
        {
            if (_philosophyQuotesGridHost == null || _dgvPhilosophyQuotes == null || _philosophyVoiceTabLayout == null)
            {
                return;
            }

            _philosophyVoiceTabLayout.PerformLayout();
            _philosophyQuotesGridHost.PerformLayout();
        }

        internal IReadOnlyList<int> GetPhilosophyQuoteIndicesForBatchOperation()
        {
            if (_dgvPhilosophyQuotes == null || _philosophyBatch?.Quotes == null)
            {
                return Array.Empty<int>();
            }

            var selected = _dgvPhilosophyQuotes.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => r.Index >= 0 && !r.IsNewRow)
                .Select(r => r.Index)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            return PhilosophyBatchAudioPreviewHelper.ResolveQuoteIndicesForOperation(
                _philosophyBatch,
                selected.Count > 0 ? selected : null);
        }

        private int GetPhilosophyQuoteExplicitSelectionCount()
        {
            if (_dgvPhilosophyQuotes == null)
            {
                return 0;
            }

            return _dgvPhilosophyQuotes.SelectedRows
                .Cast<DataGridViewRow>()
                .Count(r => r.Index >= 0 && !r.IsNewRow);
        }

        private void RefreshPhilosophyQuoteBatchActionLabels()
        {
            if (!_philosophyMode)
            {
                return;
            }

            var selectedCount = GetPhilosophyQuoteExplicitSelectionCount();

            if (_btnGenerateBodyNarration != null && !_btnGenerateBodyNarration.IsDisposed)
            {
                var hasBody = _canListenBodyNarration?.Invoke() ?? false;
                if (selectedCount == 1)
                {
                    _btnGenerateBodyNarration.Text = hasBody ? "Tạo lại thoại" : "Tạo audio thoại";
                }
                else if (selectedCount > 1)
                {
                    _btnGenerateBodyNarration.Text = hasBody
                        ? "Tạo lại (" + selectedCount + " dòng)"
                        : "Tạo audio (" + selectedCount + " dòng)";
                }
                else
                {
                    _btnGenerateBodyNarration.Text = hasBody ? "Tạo lại thoại" : "Tạo audio thoại";
                }
            }

            if (_btnRenderFullMixedAudio != null && !_btnRenderFullMixedAudio.IsDisposed)
            {
                _btnRenderFullMixedAudio.Text = selectedCount > 1
                    ? "Render audio (" + selectedCount + " dòng)"
                    : "Render audio";
            }

            if (_lblPhilosophyBatchScopeHint != null && !_lblPhilosophyBatchScopeHint.IsDisposed)
            {
                _lblPhilosophyBatchScopeHint.Text = selectedCount > 0
                    ? "«Tạo audio thoại» / «Render audio» áp dụng " + selectedCount + " dòng đang chọn."
                    : "Không chọn dòng = áp dụng cả batch (mọi quote có nội dung).";
            }
        }

        private void ApplyPhilosophyQuotesGridColumnWidths()
        {
            if (_dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.IsDisposed)
            {
                return;
            }

            WithPhilosophyQuotesGridLayoutLock(() =>
            {
                _dgvPhilosophyQuotes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                AppGridSttColumn.EnsureFirstColumn(_dgvPhilosophyQuotes, width: PhilosophyQuotesSttColumnWidth);

                SetPhilosophyQuoteComboContentFitWidth("colPhilosophyQuoteMusic", PhilosophyQuoteMusicColumnMaxWidth);
                SetPhilosophyQuoteComboContentFitWidth("colPhilosophyQuoteAmbient", PhilosophyQuoteAmbientColumnMaxWidth);
                SetPhilosophyQuoteHeaderFitWidth("colPhilosophyQuoteVolume");
                SetPhilosophyQuoteHeaderFitWidth("colPhilosophyQuoteSpeed");
                SetPhilosophyQuoteHeaderFitWidth("colPhilosophyQuoteVoice");
                SetPhilosophyQuoteHeaderFitWidth("colPhilosophyQuoteFullMix");

                foreach (DataGridViewColumn col in _dgvPhilosophyQuotes.Columns)
                {
                    if (!col.Visible
                        || string.Equals(col.Name, "colPhilosophyQuoteContent", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }

                if (_dgvPhilosophyQuotes.Columns.Contains("colPhilosophyQuoteContent"))
                {
                    var quoteCol = _dgvPhilosophyQuotes.Columns["colPhilosophyQuoteContent"];
                    quoteCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    quoteCol.FillWeight = 100;
                    quoteCol.MinimumWidth = PhilosophyQuoteMinColumnWidth;
                }
            });
        }

        private int MeasurePhilosophyQuoteColumnHeaderWidth(DataGridViewColumn column)
        {
            if (_dgvPhilosophyQuotes == null || column == null)
            {
                return Form1.AppGridColumnMinWidth;
            }

            var headerFont = _dgvPhilosophyQuotes.ColumnHeadersDefaultCellStyle?.Font ?? _dgvPhilosophyQuotes.Font;
            var headerPadding = _dgvPhilosophyQuotes.ColumnHeadersDefaultCellStyle?.Padding;
            var pad = headerPadding ?? new Padding(6, 8, 6, 8);
            var headerText = (column.HeaderText ?? string.Empty).Trim();
            if (headerText.Length == 0)
            {
                headerText = " ";
            }

            var textWidth = TextRenderer.MeasureText(
                headerText,
                headerFont,
                new Size(int.MaxValue, Form1.AppGridHeaderHeight),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;

            var measured = Math.Max(
                Form1.AppGridColumnMinWidth,
                textWidth + Form1.AppGridHeaderColumnPad + pad.Horizontal + 4);

            if (column is DataGridViewComboBoxColumn)
            {
                measured += 18;
            }

            if (column is DataGridViewButtonColumn)
            {
                measured += 18;
                var cellFont = column.DefaultCellStyle?.Font ?? _dgvPhilosophyQuotes.DefaultCellStyle.Font ?? _dgvPhilosophyQuotes.Font;
                var listenWidth = TextRenderer.MeasureText(
                    "▶ Nghe",
                    cellFont,
                    new Size(int.MaxValue, 0),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                measured = Math.Max(measured, listenWidth + 20);
            }

            return measured;
        }

        private void SetPhilosophyQuoteComboContentFitWidth(string columnName, int maxColumnWidth)
        {
            if (_dgvPhilosophyQuotes == null || !_dgvPhilosophyQuotes.Columns.Contains(columnName))
            {
                return;
            }

            if (!(_dgvPhilosophyQuotes.Columns[columnName] is DataGridViewComboBoxColumn column))
            {
                SetPhilosophyQuoteHeaderFitWidth(columnName);
                return;
            }

            var headerWidth = MeasurePhilosophyQuoteColumnHeaderWidth(column);
            var contentWidth = MeasurePhilosophyQuoteComboColumnWidth(column);
            var width = Math.Max(headerWidth, Math.Min(maxColumnWidth, contentWidth));
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = Math.Max(headerWidth, Math.Min(width, headerWidth + 24));
            column.DropDownWidth = MeasurePhilosophyQuoteComboDropDownWidth(column);
        }

        private int MeasurePhilosophyQuoteComboColumnWidth(DataGridViewComboBoxColumn column)
        {
            if (_dgvPhilosophyQuotes == null || column == null)
            {
                return Form1.AppGridColumnMinWidth;
            }

            var font = column.DefaultCellStyle?.Font ?? _dgvPhilosophyQuotes.DefaultCellStyle.Font ?? _dgvPhilosophyQuotes.Font;
            var max = MeasurePhilosophyQuoteColumnHeaderWidth(column);
            foreach (var text in EnumeratePhilosophyQuoteComboItemTexts(column))
            {
                max = Math.Max(
                    max,
                    TextRenderer.MeasureText(
                        text,
                        font,
                        new Size(int.MaxValue, 0),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 28);
            }

            return max;
        }

        private int MeasurePhilosophyQuoteComboDropDownWidth(DataGridViewComboBoxColumn column)
        {
            if (_dgvPhilosophyQuotes == null || column == null)
            {
                return PhilosophyQuoteComboDropDownMinWidth;
            }

            var font = column.DefaultCellStyle?.Font ?? _dgvPhilosophyQuotes.DefaultCellStyle.Font ?? _dgvPhilosophyQuotes.Font;
            var max = Math.Max(column.Width, PhilosophyQuoteComboDropDownMinWidth);
            foreach (var text in EnumeratePhilosophyQuoteComboItemTexts(column))
            {
                max = Math.Max(
                    max,
                    TextRenderer.MeasureText(
                        text,
                        font,
                        new Size(int.MaxValue, 0),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
            }

            return max + SystemInformation.VerticalScrollBarWidth + 16;
        }

        private static IEnumerable<string> EnumeratePhilosophyQuoteComboItemTexts(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                yield break;
            }

            foreach (var item in column.Items)
            {
                if (item is PhilosophyAmbientComboItem ambient)
                {
                    yield return ambient.Label ?? ambient.Key ?? string.Empty;
                    continue;
                }

                yield return item?.ToString() ?? string.Empty;
            }
        }

        private static void ApplyPhilosophyQuoteComboDropDownWidth(ComboBox combo, DataGridViewComboBoxColumn column)
        {
            if (combo == null || column == null)
            {
                return;
            }

            try
            {
                var font = combo.Font ?? SystemFonts.DefaultFont;
                var max = Math.Max(combo.Width, PhilosophyQuoteComboDropDownMinWidth);
                foreach (var text in EnumeratePhilosophyQuoteComboItemTexts(column))
                {
                    max = Math.Max(
                        max,
                        TextRenderer.MeasureText(
                            text,
                            font,
                            new Size(int.MaxValue, 0),
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
                }

                combo.DropDownWidth = max + SystemInformation.VerticalScrollBarWidth + 16;
            }
            catch
            {
                combo.DropDownWidth = Math.Max(combo.Width, PhilosophyQuoteComboDropDownMinWidth);
            }
        }

        private void PhilosophyQuotesGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null
                || _dgvPhilosophyQuotes.CurrentCell == null
                || !(e.Control is ComboBox combo))
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[_dgvPhilosophyQuotes.CurrentCell.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteMusic" && _colPhilosophyQuoteMusic != null)
            {
                combo.FormattingEnabled = false;
                ApplyPhilosophyQuoteComboDropDownWidth(combo, _colPhilosophyQuoteMusic);
                return;
            }

            if (colName == "colPhilosophyQuoteAmbient" && _colPhilosophyQuoteAmbient != null)
            {
                combo.DisplayMember = _colPhilosophyQuoteAmbient.DisplayMember;
                combo.ValueMember = _colPhilosophyQuoteAmbient.ValueMember;
                combo.FormattingEnabled = true;
                ApplyPhilosophyQuoteComboDropDownWidth(combo, _colPhilosophyQuoteAmbient);
            }
        }

        private void PhilosophyQuotesGrid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!(_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quote))
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteMusic")
            {
                var music = (quote.MusicFolder ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(music))
                {
                    e.ToolTipText = music;
                }

                return;
            }

            if (colName == "colPhilosophyQuoteAmbient")
            {
                e.ToolTipText = PhilosophyAmbientCatalog.GetLabel(quote.AmbientKey);
                return;
            }

            if (colName == "colPhilosophyQuoteStatus"
                && _philosophyQuoteStatusByRow.TryGetValue(e.RowIndex, out var status))
            {
                e.ToolTipText = status.Detail;
                return;
            }

            if (colName == "colPhilosophyQuoteVolume")
            {
                e.ToolTipText = "Chuột phải → chọn preset volume nhạc nền";
                return;
            }

            if (colName == "colPhilosophyQuoteSpeed")
            {
                e.ToolTipText = "Chuột phải → chọn preset tốc độ thoại";
            }
        }

        private void SetPhilosophyQuoteHeaderFitWidth(string columnName)
        {
            if (_dgvPhilosophyQuotes == null || !_dgvPhilosophyQuotes.Columns.Contains(columnName))
            {
                return;
            }

            var column = _dgvPhilosophyQuotes.Columns[columnName];
            var width = MeasurePhilosophyQuoteColumnHeaderWidth(column);
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = width;
        }

        private string GetPhilosophyQuotesSessionBase() =>
            _philosophyBatch == null
                ? string.Empty
                : PhilosophyBatchAudioPreviewHelper.GetSessionBase(_philosophyBatch, _settings);

        private void PhilosophyQuotesGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteStatus")
            {
                if (_philosophyQuoteStatusByRow.TryGetValue(e.RowIndex, out var status))
                {
                    e.Value = status.Summary;
                    e.CellStyle.ForeColor = status.IsValid
                        ? Color.FromArgb(120, 210, 140)
                        : status.Summary == "◐"
                            ? Color.FromArgb(200, 190, 120)
                            : Color.FromArgb(240, 150, 110);
                }
                else
                {
                    e.Value = "?";
                }

                e.FormattingApplied = true;
                return;
            }

            if (colName == "colPhilosophyQuoteMood")
            {
                if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quoteMood)
                {
                    e.Value = FormatPhilosophyMoodLabel(quoteMood.Mood);
                    e.FormattingApplied = true;
                }

                return;
            }

            if (colName == "colPhilosophyQuoteVolume")
            {
                if (_dgvPhilosophyQuotes.IsCurrentCellInEditMode
                    && _dgvPhilosophyQuotes.CurrentCell?.RowIndex == e.RowIndex
                    && _dgvPhilosophyQuotes.CurrentCell?.ColumnIndex == e.ColumnIndex)
                {
                    return;
                }

                if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quoteVolume)
                {
                    e.Value = PhilosophyBatchHelper.ClampMusicVolumePercent(
                                  quoteVolume.MusicVolumePercent > 0
                                      ? quoteVolume.MusicVolumePercent
                                      : PhilosophyBatchHelper.ResolveBatchMusicVolumePercent(_philosophyBatch))
                              + "%";
                    e.FormattingApplied = true;
                }

                return;
            }

            if (colName == "colPhilosophyQuoteSpeed")
            {
                if (_dgvPhilosophyQuotes.IsCurrentCellInEditMode
                    && _dgvPhilosophyQuotes.CurrentCell?.RowIndex == e.RowIndex
                    && _dgvPhilosophyQuotes.CurrentCell?.ColumnIndex == e.ColumnIndex)
                {
                    return;
                }

                if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quoteSpeed)
                {
                    e.Value = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(quoteSpeed, _philosophyBatch)
                              + "%";
                    e.FormattingApplied = true;
                }

                return;
            }

            if (colName != "colPhilosophyQuoteVoice" && colName != "colPhilosophyQuoteFullMix")
            {
                return;
            }

            var sessionBase = GetPhilosophyQuotesSessionBase();
            var hasFile = colName == "colPhilosophyQuoteVoice"
                ? PhilosophyBatchAudioPreviewHelper.HasQuoteVoicePreview(sessionBase, e.RowIndex)
                : PhilosophyBatchAudioPreviewHelper.HasQuoteFullMixPreview(sessionBase, e.RowIndex);
            e.Value = hasFile ? "▶ Nghe" : "—";
            e.FormattingApplied = true;
        }

        private void PhilosophyQuotesGrid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName == "colPhilosophyQuoteContent")
            {
                if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quoteContent)
                {
                    quoteContent.Content = (e.FormattedValue?.ToString() ?? string.Empty).Trim();
                }

                return;
            }

            if (colName == "colPhilosophyQuoteVolume")
            {
                var text = (e.FormattedValue?.ToString() ?? string.Empty).Trim().TrimEnd('%');
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = PhilosophyBatchHelper.DefaultMusicVolumePercent.ToString();
                }

                if (!int.TryParse(text, out var volume) || volume < 0 || volume > 100)
                {
                    e.Cancel = true;
                    MessageBox.Show(this,
                        "Âm lượng phải là số từ 0 đến 100.",
                        "Âm lượng",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quoteVolume)
                {
                    quoteVolume.MusicVolumePercent = volume;
                }

                return;
            }

            if (colName != "colPhilosophyQuoteSpeed")
            {
                return;
            }

            var speedText = (e.FormattedValue?.ToString() ?? string.Empty).Trim().TrimEnd('%');
            if (string.IsNullOrWhiteSpace(speedText))
            {
                speedText = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent.ToString();
            }

            if (!int.TryParse(speedText, out var speed)
                || speed < ShowcaseNarrationSpeedHelper.MinManualSpeedPercent
                || speed > ShowcaseNarrationSpeedHelper.MaxManualSpeedPercent)
            {
                e.Cancel = true;
                MessageBox.Show(this,
                    "Tốc độ thoại phải là số từ "
                    + ShowcaseNarrationSpeedHelper.MinManualSpeedPercent
                    + " đến "
                    + ShowcaseNarrationSpeedHelper.MaxManualSpeedPercent
                    + ".",
                    "Tốc độ thoại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem quoteSpeed)
            {
                quoteSpeed.NarrationSpeedPercent = speed;
            }
        }

        private async void PhilosophyQuotesGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
            if (colName != "colPhilosophyQuoteVoice" && colName != "colPhilosophyQuoteFullMix")
            {
                return;
            }

            await PlayPhilosophyQuotePreviewAsync(e.RowIndex, colName == "colPhilosophyQuoteFullMix").ConfigureAwait(true);
        }

        private void PhilosophyQuotesGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            if (_dgvPhilosophyQuotes == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_dgvPhilosophyQuotes.Rows[e.RowIndex].DataBoundItem is PhilosophyScriptItem item)
            {
                var colName = _dgvPhilosophyQuotes.Columns[e.ColumnIndex].Name;
                if (colName == "colPhilosophyQuoteMusic")
                {
                    EnsurePhilosophyQuoteMusicInCombo(item.MusicFolder);
                }
                else if (colName == "colPhilosophyQuoteAmbient")
                {
                    item.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(item.AmbientKey);
                    EnsurePhilosophyQuoteAmbientInCombo(item.AmbientKey);
                }
            }
        }

        private void SafeEndPhilosophyQuotesGridEdit()
        {
            if (_dgvPhilosophyQuotes == null || _dgvPhilosophyQuotes.IsDisposed)
            {
                return;
            }

            try
            {
                if (_dgvPhilosophyQuotes.IsCurrentCellInEditMode && _dgvPhilosophyQuotes.CurrentCell != null)
                {
                    var colName = _dgvPhilosophyQuotes.Columns[_dgvPhilosophyQuotes.CurrentCell.ColumnIndex].Name;
                    if (colName == "colPhilosophyQuoteVolume" || colName == "colPhilosophyQuoteSpeed")
                    {
                        TryCommitPhilosophyQuoteNumericCell(_dgvPhilosophyQuotes.CurrentCell.RowIndex, colName, invalidateOnly: false);
                    }

                    _philosophyQuotesGridSyncLock = true;
                    try
                    {
                        _dgvPhilosophyQuotes.CancelEdit();
                    }
                    finally
                    {
                        _philosophyQuotesGridSyncLock = false;
                    }
                }
                else if (_dgvPhilosophyQuotes.IsCurrentCellInEditMode)
                {
                    _dgvPhilosophyQuotes.EndEdit();
                }
            }
            catch (InvalidOperationException)
            {
                // Combo auto-fill — ignore.
            }

            try
            {
                _philosophyQuotesBinding?.EndEdit();
            }
            catch (InvalidOperationException)
            {
                // Binding mid-update — ignore.
            }
        }

        private void CommitPhilosophyQuotesGridEdits()
        {
            SafeEndPhilosophyQuotesGridEdit();
            if (_philosophyBatch?.Quotes == null)
            {
                return;
            }

            foreach (var quote in _philosophyBatch.Quotes)
            {
                if (quote == null)
                {
                    continue;
                }

                quote.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(quote.AmbientKey);
                if (PhilosophyAmbientCatalog.IsMediaFileName(quote.AmbientKey))
                {
                    quote.AmbientKey = System.IO.Path.GetFileName(quote.AmbientKey.Trim());
                }

                quote.Content = PhilosophyGeminiTtsContext.FixQuotePeriods(quote.Content);
                quote.MusicVolumePercent = PhilosophyBatchHelper.ClampMusicVolumePercent(
                    quote.MusicVolumePercent > 0
                        ? quote.MusicVolumePercent
                        : PhilosophyBatchHelper.ResolveBatchMusicVolumePercent(_philosophyBatch));
                quote.NarrationSpeedPercent = PhilosophyBatchHelper.ClampNarrationSpeedPercent(
                    quote.NarrationSpeedPercent > 0
                        ? quote.NarrationSpeedPercent
                        : PhilosophyBatchHelper.ResolveBatchNarrationSpeedPercent(_philosophyBatch));
            }

            var firstQuote = _philosophyBatch.Quotes.FirstOrDefault(q => q != null);
            if (firstQuote != null)
            {
                _philosophyBatch.BodyNarrationSpeedPercent = firstQuote.NarrationSpeedPercent;
            }
        }
    }
}
