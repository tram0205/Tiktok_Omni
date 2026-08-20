using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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

            PhilosophyBatchHelper.EnsureQuoteAudioDefaults(_philosophyBatch);

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
                DataPropertyName = nameof(PhilosophyScriptItem.MusicVolumePercent),
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
                Name = "colPhilosophyQuoteSpeed",
                HeaderText = "Tốc độ",
                DataPropertyName = nameof(PhilosophyScriptItem.NarrationSpeedPercent),
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
            _dgvPhilosophyQuotes.CellContentClick += PhilosophyQuotesGrid_CellContentClick;
            _dgvPhilosophyQuotes.SelectionChanged += (_, __) => RefreshPhilosophyQuoteBatchActionLabels();
            _dgvPhilosophyQuotes.CellValueChanged += PhilosophyQuotesGrid_CellValueChanged;
            _dgvPhilosophyQuotes.KeyDown += PhilosophyQuotesGrid_KeyDown;
            _dgvPhilosophyQuotes.CellEndEdit += (_, __) => QueuePhilosophyQuotesGridRowResize();
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

            _philosophyVoiceTabLayout.Controls.Add(_philosophyQuotesGridHost, 0, 1);
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
                Margin = new Padding(0, 4, 12, 4),
                Padding = new Padding(12, 6, 12, 6),
                UseVisualStyleBackColor = false
            };
            _btnPhilosophyDeleteQuoteRows.FlatAppearance.BorderSize = 0;
            _btnPhilosophyDeleteQuoteRows.Click += (_, __) => DeleteSelectedPhilosophyQuotes();

            var hint = new Label
            {
                Text = "Sửa trực tiếp cột «Quote» · chọn dòng rồi «Xóa dòng» hoặc phím Delete",
                AutoSize = true,
                MaximumSize = new Size(980, 0),
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.25F),
                Margin = new Padding(0, 10, 0, 0)
            };

            panel.Controls.Add(_btnPhilosophyDeleteQuoteRows);
            panel.Controls.Add(hint);
            return panel;
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
            }
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

            PhilosophyBatchHelper.EnsureQuoteAudioDefaults(_philosophyBatch);
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
            if (colName == "colPhilosophyQuoteVolume")
            {
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

        private void PhilosophyQuotesGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
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

            var sessionBase = GetPhilosophyQuotesSessionBase();
            var path = colName == "colPhilosophyQuoteVoice"
                ? PhilosophyBatchAudioPreviewHelper.GetQuoteVoicePreviewPath(sessionBase, e.RowIndex)
                : PhilosophyBatchAudioPreviewHelper.GetQuoteFullMixPreviewPath(sessionBase, e.RowIndex);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                if (colName == "colPhilosophyQuoteFullMix")
                {
                    MessageBox.Show(
                        this,
                        "Chưa có audio thành phẩm cho dòng này. Bấm «Render audio» trước.",
                        "Nghe audio",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
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
                if (_dgvPhilosophyQuotes.IsCurrentCellInEditMode)
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

                quote.Content = (quote.Content ?? string.Empty).Trim();
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
