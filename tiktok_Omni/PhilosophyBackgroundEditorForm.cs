using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class PhilosophyBackgroundEditorForm : Form
    {
        private const int GridRowHeight = 36;
        private const int GridHeaderHeight = 57; // 44 × 1.3

        private readonly PhilosophyBatchItem _batch;
        private readonly string _profileName;
        private DataGridView _grid;

        public PhilosophyBackgroundEditorForm(PhilosophyBatchItem batch, string profileName)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
            _profileName = profileName ?? string.Empty;
            Text = "Nền — " + PhilosophyBatchHelper.TrimGridLabel(_batch.Topic, 40, "batch");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(1840, 1040);
            MinimumSize = new Size(920, 520);
            Padding = new Padding(20);
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(CreateIntroLabel(), 0, 0);
            root.Controls.Add(CreateGrid(), 0, 1);

            Controls.Add(root);
            Controls.Add(CreateFooterBar());
        }

        private Control CreateFooterBar()
        {
            var btnOk = CreateFooterButton("Lưu", Color.FromArgb(56, 120, 82), new Padding(10, 0, 0, 0));
            btnOk.DialogResult = DialogResult.OK;

            var btnCancel = CreateFooterButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            var flpButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 10, 0, 8),
                Margin = Padding.Empty,
                Dock = DockStyle.Fill
            };
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnCancel);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(0, 0, 0, 8),
                Margin = Padding.Empty
            };
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 1F));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            footer.Controls.Add(new Panel
            {
                Dock = DockStyle.Fill,
                Height = 1,
                BackColor = Color.FromArgb(55, 60, 72),
                Margin = Padding.Empty
            }, 0, 0);
            footer.Controls.Add(flpButtons, 0, 1);
            return footer;
        }

        private static Button CreateFooterButton(string text, Color back, Padding? margin = null)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(120, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F),
                Margin = margin ?? Padding.Empty,
                Padding = new Padding(12, 6, 12, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static Label CreateIntroLabel()
        {
            return new Label
            {
                Text = "Cột «Ảnh ref» (batch — AI I2V) · «Loại nền» B-roll hoặc AI · «Prompt Veo» cho từng câu.",
                AutoSize = true,
                MaximumSize = new Size(1700, 0),
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 0, 0, 14)
            };
        }

        private DataGridView CreateGrid()
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AllowUserToResizeRows = true,
                ColumnHeadersHeight = GridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(60, 64, 77),
                MinimumSize = new Size(0, 320),
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgQuote",
                HeaderText = "Quote",
                ReadOnly = true,
                FillWeight = 38f,
                MinimumWidth = 360,
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            });
            var modeCol = new DataGridViewComboBoxColumn
            {
                Name = "colBgMode",
                HeaderText = "Loại nền",
                FillWeight = 8,
                MinimumWidth = 84,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                HeaderCell =
                {
                    Style =
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        Padding = Padding.Empty
                    }
                }
            };
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeBroll);
            modeCol.Items.Add(PhilosophyBatchHelper.BackgroundModeAi);
            _grid.Columns.Add(modeCol);
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colBgBroll",
                HeaderText = "B-roll",
                Text = "Chọn…",
                UseColumnTextForButtonValue = true,
                FillWeight = 10,
                MinimumWidth = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBgPrompt",
                HeaderText = "Prompt Veo (AI)",
                FillWeight = 38,
                MinimumWidth = 260,
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colBgRefImage",
                HeaderText = "Ảnh ref",
                Text = "Chọn…",
                UseColumnTextForButtonValue = false,
                FillWeight = 11,
                MinimumWidth = 112,
                ToolTipText = "Ảnh tham chiếu batch (AI I2V) — bấm để chọn hoặc xóa",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.False
                }
            });

            AppGridSttColumn.EnsureFirstColumn(_grid, width: (int)(AppGridSttColumn.CompactColumnWidth * 1.2));
            _grid.DataError += Grid_DataError;
            PopulateRows();
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.SizeChanged += (_, __) => ResizeGridRows();
            _grid.CellValueChanged += (_, e) =>
            {
                ApplyRowEdits();
                if (e.RowIndex >= 0
                    && e.ColumnIndex >= 0
                    && (_grid.Columns[e.ColumnIndex].Name == "colBgPrompt"
                        || _grid.Columns[e.ColumnIndex].Name == "colBgMode"))
                {
                    ResizeGridRows();
                }
            };

            return _grid;
        }

        private static string DescribeReferenceImageLabel(string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return "Chọn…";
            }

            if (!File.Exists(trimmed))
            {
                return "Thiếu file";
            }

            var name = Path.GetFileName(trimmed);
            return name.Length <= 18 ? name : name.Substring(0, 15) + "…";
        }

        private void RefreshReferenceImageColumn()
        {
            if (_grid == null || _grid.IsDisposed)
            {
                return;
            }

            var col = _grid.Columns["colBgRefImage"];
            if (col == null)
            {
                return;
            }

            _grid.InvalidateColumn(col.Index);
        }

        private void BrowseReferenceImage()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Title = "Chọn ảnh tham chiếu cho batch"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _batch.ReferenceImagePath = dlg.FileName;
                RefreshReferenceImageColumn();
            }
        }

        private void ShowReferenceImageMenu()
        {
            var hasImage = !string.IsNullOrWhiteSpace(_batch.ReferenceImagePath);
            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = Font
            };
            menu.Items.Add("Chọn ảnh…", null, (_, __) => BrowseReferenceImage());
            var clearItem = menu.Items.Add("Xóa ảnh", null, (_, __) =>
            {
                _batch.ReferenceImagePath = string.Empty;
                RefreshReferenceImageColumn();
            });
            clearItem.Enabled = hasImage;
            menu.Show(Cursor.Position);
        }

        private void ResizeGridRows()
        {
            if (_grid == null || _grid.IsDisposed || _grid.Rows.Count == 0)
            {
                return;
            }

            _grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            for (var i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Height < GridRowHeight)
                {
                    _grid.Rows[i].Height = GridRowHeight;
                }
            }
        }

        private void PopulateRows()
        {
            _grid.Rows.Clear();
            var quotes = _batch.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>();
            foreach (var quote in quotes)
            {
                if (quote == null)
                {
                    continue;
                }

                var mode = PhilosophyBatchHelper.ToSimpleBackgroundModeLabel(quote.VisualMode);
                var prompt = PhilosophyBatchHelper.BuildAiVeoPrompt(quote, _profileName, _batch.Topic);
                if (string.IsNullOrWhiteSpace(quote.MotionPrompt))
                {
                    quote.MotionPrompt = prompt;
                }

                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["colBgQuote"].Value = quote.Content ?? string.Empty;
                row.Cells["colBgMode"].Value = mode;
                row.Cells["colBgPrompt"].Value = quote.MotionPrompt ?? prompt;
                row.Tag = quote;
            }

            RefreshReferenceImageColumn();
            ResizeGridRows();
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "colBgMode")
            {
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = PhilosophyBatchHelper.BackgroundModeBroll;
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName == "colBgRefImage")
            {
                e.Value = DescribeReferenceImageLabel(_batch.ReferenceImagePath);
                e.FormattingApplied = true;
                return;
            }

            if (colName == "colBgBroll")
            {
                var quote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
                if (quote != null)
                {
                    e.Value = PhilosophyBRollSelection.GetDisplayLabel(quote.BRollFolder);
                    e.FormattingApplied = true;
                }
            }
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName == "colBgRefImage")
            {
                ShowReferenceImageMenu();
                return;
            }

            var quote = _grid.Rows[e.RowIndex].Tag as PhilosophyScriptItem;
            if (quote == null)
            {
                return;
            }

            if (colName == "colBgBroll")
            {
                PickBRoll(quote, e.RowIndex);
            }
        }

        private void PickBRoll(PhilosophyScriptItem quote, int rowIndex)
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Chọn thư mục hoặc duyệt file B-roll",
                ShowNewFolderButton = false
            })
            {
                var initial = PhilosophyBRollSelection.GetBrowseInitialDirectory(_profileName);
                if (!string.IsNullOrEmpty(initial))
                {
                    dlg.SelectedPath = initial;
                }

                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                quote.BRollFolder = dlg.SelectedPath?.Trim() ?? string.Empty;
                _grid.Rows[rowIndex].Cells["colBgBroll"].Value =
                    PhilosophyBRollSelection.GetDisplayLabel(quote.BRollFolder);
                ResizeGridRows();
            }
        }

        private void ApplyRowEdits()
        {
            for (var r = 0; r < _grid.Rows.Count; r++)
            {
                var quote = _grid.Rows[r].Tag as PhilosophyScriptItem;
                if (quote == null)
                {
                    continue;
                }

                var modeLabel = (_grid.Rows[r].Cells["colBgMode"].Value ?? PhilosophyBatchHelper.BackgroundModeBroll).ToString();
                quote.VisualMode = PhilosophyBatchHelper.FromSimpleBackgroundModeLabel(modeLabel);
                quote.MotionPrompt = (_grid.Rows[r].Cells["colBgPrompt"].Value ?? string.Empty).ToString().Trim();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                ApplyRowEdits();
                _batch.RefreshDerivedFields();
            }

            base.OnFormClosing(e);
        }
    }
}
