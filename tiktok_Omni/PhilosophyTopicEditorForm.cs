using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class PhilosophyTopicEditorForm : Form
    {
        private const int GridRowHeight = 58; // 36 × 1.6
        private const int GridHeaderHeight = 56; // 80 × 0.7
        private const int GridSectionTitleHeight = 56; // 28 × 2
        private const int GridSectionTitleGap = 12; // 6 × 2

        private const int OptionsControlHeight = 42; // 32 × 1.3
        private const int TemplateRowControlHeight = 55; // 42 × 1.3 — dòng Loại nội dung
        private const int TemplateRowItemHeight = 26;

        private readonly PhilosophyBatchItem _batch;
        private readonly Func<PhilosophyBatchItem, CancellationToken, Task<IReadOnlyList<PhilosophyScriptItem>>> _generateScriptsAsync;
        private readonly Action<PhilosophyBatchItem, IReadOnlyList<PhilosophyScriptItem>> _applyScripts;

        private Label _lblTopic;
        private TextBox _txtTopic;
        private ComboBox _cboTemplate;
        private Label _lblMetadata;
        private TextBox _txtMetadata;
        private RadioButton _rbQuotes;
        private RadioButton _rbStory;
        private NumericUpDown _numCount;
        private NumericUpDown _numMinDuration;
        private NumericUpDown _numMaxDuration;
        private Button _btnGenerate;
        private Label _lblIntro;
        private Label _lblQuotesGridTitle;
        private DataGridView _grid;
        private BindingList<PhilosophyScriptItem> _quotes;
        private CancellationTokenSource _generateCts;

        public PhilosophyTopicEditorForm(
            PhilosophyBatchItem batch,
            Func<PhilosophyBatchItem, CancellationToken, Task<IReadOnlyList<PhilosophyScriptItem>>> generateScriptsAsync,
            Action<PhilosophyBatchItem, IReadOnlyList<PhilosophyScriptItem>> applyScripts)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
            _generateScriptsAsync = generateScriptsAsync ?? throw new ArgumentNullException(nameof(generateScriptsAsync));
            _applyScripts = applyScripts ?? throw new ArgumentNullException(nameof(applyScripts));

            _quotes = new BindingList<PhilosophyScriptItem>(
                _batch.Quotes?.Where(q => q != null).ToList() ?? new List<PhilosophyScriptItem>());
            NormalizeQuoteMoods(_quotes);

            Text = "Chủ đề & Gemini — batch";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(1920, 1440);
            MinimumSize = new Size(1560, 1080);
            Padding = new Padding(20);

            BuildUi();
            LoadFromBatch();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(CreateIntroLabel(), 0, 0);
            root.Controls.Add(CreateTemplateSection(), 0, 1);
            root.Controls.Add(CreateTopicSection(), 0, 2);
            root.Controls.Add(CreateOptionsSection(), 0, 3);
            root.Controls.Add(CreateQuotesGrid(), 0, 4);

            Controls.Add(root);
            Controls.Add(CreateFooterBar());
        }

        private Control CreateFooterBar()
        {
            var btnOk = CreateFooterButton("Lưu", Color.FromArgb(56, 120, 82), new Padding(10, 0, 0, 0));
            btnOk.Name = "btnPhilosophyTopicSave";
            btnOk.DialogResult = DialogResult.OK;

            var btnCancel = CreateFooterButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.Name = "btnPhilosophyTopicCancel";
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

        private Label CreateIntroLabel()
        {
            _lblIntro = new Label
            {
                Text = BuildIntroText(manual: false),
                AutoSize = false,
                Height = 52,
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 0, 0, 12)
            };
            return _lblIntro;
        }

        private static string BuildIntroText(bool manual) =>
            manual
                ? "«Nhập tay»: gõ từng câu vào lưới bên dưới (cột «Quote») — thêm dòng bằng hàng trống cuối lưới."
                : "Chọn loại nội dung, nhập chủ đề — bấm «Tạo nội dung» (Gemini) hoặc gõ tay trên lưới bên dưới. "
                  + "Toolbar «Tạo nội dung Gemini» xử lý nhiều batch đã chọn.";

        private Panel CreateTopicSection()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 10)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            _lblTopic = new Label
            {
                Text = "Chủ đề",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 8, 0)
            };
            panel.Controls.Add(_lblTopic, 0, 0);

            _txtTopic = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle
            };
            panel.Controls.Add(_txtTopic, 1, 0);
            panel.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = BackColor }, 2, 0);
            return panel;
        }

        private Control CreateTemplateSection()
        {
            var templateRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 10),
                MinimumSize = new Size(0, TemplateRowControlHeight + 6),
                Padding = new Padding(0, 2, 0, 2)
            };

            var lblType = CreateInlineLabel("Loại nội dung");
            lblType.Margin = new Padding(0, 16, 8, 0);
            templateRow.Controls.Add(lblType);

            _cboTemplate = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 512,
                Height = TemplateRowControlHeight,
                ItemHeight = TemplateRowItemHeight,
                IntegralHeight = false,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(0, 0, 24, 0)
            };
            foreach (var preset in PhilosophyContentTemplatePresets.All)
            {
                _cboTemplate.Items.Add(preset);
            }

            _cboTemplate.SelectedIndexChanged += (_, __) => ApplySelectedTemplateDefaultsToUi();
            templateRow.Controls.Add(_cboTemplate);

            _lblMetadata = CreateInlineLabel("Tác giả / nguồn");
            _lblMetadata.Visible = false;
            _lblMetadata.Margin = new Padding(0, 16, 8, 0);
            templateRow.Controls.Add(_lblMetadata);

            _txtMetadata = new TextBox
            {
                Width = 280,
                Height = TemplateRowControlHeight,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5F),
                Visible = false,
                Margin = new Padding(0, 0, 0, 0)
            };
            templateRow.Controls.Add(_txtMetadata);
            return templateRow;
        }

        private Panel CreateOptionsSection()
        {
            _numCount = CreateSpinner(1, 20, 5);
            _numMinDuration = CreateSpinner(5, 180, 15);
            _numMaxDuration = CreateSpinner(5, 180, 60);

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 10),
                Padding = Padding.Empty
            };

            panel.Controls.Add(CreateInlineLabel("Kiểu"));
            var modeRow = CreateModeRow();
            modeRow.Margin = new Padding(0, 4, 28, 0);
            panel.Controls.Add(modeRow);

            panel.Controls.Add(CreateInlineLabel("Số câu"));
            _numCount.Margin = new Padding(0, 2, 28, 0);
            panel.Controls.Add(_numCount);

            panel.Controls.Add(CreateInlineLabel("Thời lượng (giây)"));
            _numMinDuration.Margin = new Padding(0, 2, 0, 0);
            panel.Controls.Add(_numMinDuration);
            panel.Controls.Add(new Label
            {
                Text = "đến",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(10, 8, 10, 0)
            });
            _numMaxDuration.Margin = new Padding(0, 2, 0, 0);
            panel.Controls.Add(_numMaxDuration);

            _btnGenerate = Form1.CreateAppJellyButton(
                "btnPhilosophyTopicGenerate",
                "Tạo nội dung",
                Color.FromArgb(76, 110, 245),
                margin: new Padding(28, 2, 12, 0));
            _btnGenerate.Click += async (_, __) => await GenerateContentAsync().ConfigureAwait(true);
            panel.Controls.Add(_btnGenerate);

            _rbQuotes.CheckedChanged += (_, __) => SyncQuoteCountEnabled();
            _rbStory.CheckedChanged += (_, __) => SyncQuoteCountEnabled();

            return panel;
        }

        private static Label CreateInlineLabel(string title) => new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 168, 182),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(0, 10, 8, 0)
        };

        private Control CreateModeRow()
        {
            _rbQuotes = new RadioButton
            {
                Text = "Quotes",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Checked = true
            };
            _rbStory = new RadioButton
            {
                Text = "Story",
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(16, 0, 0, 0)
            };

            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };
            row.Controls.Add(_rbQuotes);
            row.Controls.Add(_rbStory);
            return row;
        }

        private Control CreateQuotesGrid()
        {
            _lblQuotesGridTitle = new Label
            {
                Text = BuildQuotesGridTitle(manual: false),
                Dock = DockStyle.Top,
                Height = GridSectionTitleHeight,
                ForeColor = Color.FromArgb(200, 204, 214),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Padding(0, 8, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(38, 42, 52),
                BorderStyle = BorderStyle.FixedSingle,
                DataSource = _quotes,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AllowUserToResizeRows = true,
                ColumnHeadersHeight = GridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(60, 64, 77),
                MinimumSize = new Size(0, 240),
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
                    Padding = Padding.Empty
                }
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Quote",
                Name = "colTopicQuoteContent",
                DataPropertyName = nameof(PhilosophyScriptItem.Content),
                FillWeight = 70,
                MinimumWidth = 320,
                DefaultCellStyle =
                {
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            });
            var moodCol = new DataGridViewComboBoxColumn
            {
                Name = "colTopicQuoteMood",
                HeaderText = "Tâm trạng",
                DataPropertyName = nameof(PhilosophyScriptItem.Mood),
                FillWeight = 5,
                MinimumWidth = 35,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                ToolTipText = "calm=bình an | melancholic=buồn | hopeful=hy vọng | intense=mạnh | reflective=suy ngẫm",
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            moodCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            moodCol.Items.AddRange("calm", "melancholic", "hopeful", "intense", "reflective");
            _grid.Columns.Add(moodCol);

            AppGridSttColumn.EnsureFirstColumn(_grid, width: (int)(AppGridSttColumn.CompactColumnWidth * 1.3));
            _grid.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.DataError += Grid_DataError;
            _grid.DataBindingComplete += (_, __) => ResizeQuoteGridRows();
            _grid.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex >= 0
                    && e.ColumnIndex >= 0
                    && _grid.Columns[e.ColumnIndex].Name == "colTopicQuoteContent")
                {
                    ResizeQuoteGridRows();
                }
            };
            _grid.SizeChanged += (_, __) => ResizeQuoteGridRows();

            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, GridSectionTitleHeight));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblQuotesGridTitle.Dock = DockStyle.Fill;
            _grid.Margin = new Padding(0, GridSectionTitleGap, 0, 0);
            host.Controls.Add(_lblQuotesGridTitle, 0, 0);
            host.Controls.Add(_grid, 0, 1);
            return host;
        }

        private static string BuildQuotesGridTitle(bool manual) =>
            manual
                ? "Nhập câu thủ công tại đây (mỗi dòng = 1 clip video)"
                : "Nội dung đã sinh / chỉnh sửa";

        private static void NormalizeQuoteMoods(IEnumerable<PhilosophyScriptItem> quotes)
        {
            if (quotes == null)
            {
                return;
            }

            foreach (var quote in quotes)
            {
                if (quote == null)
                {
                    continue;
                }

                quote.Mood = PhilosophyBatchHelper.NormalizeQuoteMood(quote.Mood);
            }
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "colTopicQuoteMood")
            {
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = PhilosophyBatchHelper.NormalizeQuoteMood(
                    _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
            }
        }

        private void ResizeQuoteGridRows()
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

        private static NumericUpDown CreateSpinner(int min, int max, int value) => new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Max(min, Math.Min(max, value)),
            Width = 88,
            Height = OptionsControlHeight,
            BackColor = Color.FromArgb(45, 49, 60),
            ForeColor = Color.WhiteSmoke
        };

        private void LoadFromBatch()
        {
            _txtTopic.Text = _batch.Topic ?? string.Empty;
            SelectTemplateCombo(_batch.ContentTemplateId);
            _txtMetadata.Text = _batch.ContentMetadata ?? string.Empty;
            var story = string.Equals(_batch.GenerationMode, "Story", StringComparison.OrdinalIgnoreCase);
            _rbStory.Checked = story;
            _rbQuotes.Checked = !story;
            _numCount.Value = Math.Max(_numCount.Minimum,
                Math.Min(_numCount.Maximum, _batch.QuoteCount > 0 ? _batch.QuoteCount : 5));
            _numMinDuration.Value = Math.Max(_numMinDuration.Minimum,
                Math.Min(_numMinDuration.Maximum, _batch.MinDurationSeconds > 0 ? _batch.MinDurationSeconds : 15));
            _numMaxDuration.Value = Math.Max(_numMaxDuration.Minimum,
                Math.Min(_numMaxDuration.Maximum, _batch.MaxDurationSeconds > 0 ? _batch.MaxDurationSeconds : 60));
            SyncQuoteCountEnabled();
            SyncTemplateUi();
        }

        private void SelectTemplateCombo(string templateId)
        {
            if (_cboTemplate == null)
            {
                return;
            }

            var id = PhilosophyContentTemplatePresets.NormalizeId(templateId);
            for (var i = 0; i < _cboTemplate.Items.Count; i++)
            {
                if (_cboTemplate.Items[i] is PhilosophyContentTemplatePreset preset
                    && preset.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    _cboTemplate.SelectedIndex = i;
                    return;
                }
            }

            _cboTemplate.SelectedIndex = 0;
        }

        private PhilosophyContentTemplatePreset GetSelectedTemplate()
        {
            if (_cboTemplate?.SelectedItem is PhilosophyContentTemplatePreset preset)
            {
                return preset;
            }

            return PhilosophyContentTemplatePresets.Philosophy;
        }

        private void ApplySelectedTemplateDefaultsToUi()
        {
            var preset = GetSelectedTemplate();
            if (!preset.UsesGemini)
            {
                SyncTemplateUi();
                return;
            }

            _rbStory.Checked = preset.IsStory;
            _rbQuotes.Checked = !preset.IsStory;
            _numCount.Value = Math.Max(_numCount.Minimum,
                Math.Min(_numCount.Maximum, preset.QuoteCount));
            _numMinDuration.Value = Math.Max(_numMinDuration.Minimum,
                Math.Min(_numMinDuration.Maximum, preset.MinDurationSeconds));
            _numMaxDuration.Value = Math.Max(_numMaxDuration.Minimum,
                Math.Min(_numMaxDuration.Maximum, preset.MaxDurationSeconds));
            SyncQuoteCountEnabled();
            SyncTemplateUi();
        }

        private void SyncTemplateUi()
        {
            var preset = GetSelectedTemplate();
            var manual = !preset.UsesGemini;
            if (_btnGenerate != null)
            {
                _btnGenerate.Visible = !manual;
                _btnGenerate.Enabled = !manual;
            }

            if (_lblMetadata != null)
            {
                _lblMetadata.Visible = preset.SupportsMetadata;
            }

            if (_txtMetadata != null)
            {
                _txtMetadata.Visible = preset.SupportsMetadata;
            }

            if (_lblIntro != null)
            {
                _lblIntro.Text = BuildIntroText(manual);
            }

            if (_lblQuotesGridTitle != null)
            {
                _lblQuotesGridTitle.Text = BuildQuotesGridTitle(manual);
                _lblQuotesGridTitle.ForeColor = manual
                    ? Color.FromArgb(255, 210, 120)
                    : Color.FromArgb(200, 204, 214);
            }

            SyncManualModeFields(manual);
        }

        private void SyncManualModeFields(bool manual)
        {
            if (_lblTopic != null)
            {
                _lblTopic.Text = manual ? "Chủ đề (không dùng)" : "Chủ đề";
                _lblTopic.ForeColor = manual
                    ? Color.FromArgb(100, 108, 122)
                    : Color.FromArgb(160, 168, 182);
            }

            if (_txtTopic != null)
            {
                _txtTopic.Enabled = !manual;
                _txtTopic.BackColor = manual
                    ? Color.FromArgb(35, 38, 48)
                    : Color.FromArgb(45, 49, 60);
                _txtTopic.ForeColor = manual
                    ? Color.FromArgb(110, 116, 128)
                    : Color.WhiteSmoke;
            }

            if (_rbQuotes != null)
            {
                _rbQuotes.Enabled = !manual;
            }

            if (_rbStory != null)
            {
                _rbStory.Enabled = !manual;
            }

            if (_numMinDuration != null)
            {
                _numMinDuration.Enabled = !manual;
            }

            if (_numMaxDuration != null)
            {
                _numMaxDuration.Enabled = !manual;
            }

            SyncQuoteCountEnabled();
        }

        private void SyncQuoteCountEnabled()
        {
            if (_numCount == null)
            {
                return;
            }

            var manual = !GetSelectedTemplate().UsesGemini;
            var story = _rbStory != null && _rbStory.Checked;
            _numCount.Enabled = !manual && !story;
            if (story)
            {
                _numCount.Value = 1;
            }
        }

        private void SaveBatchFieldsFromUi()
        {
            _batch.Topic = _txtTopic.Text?.Trim() ?? string.Empty;
            _batch.ContentTemplateId = GetSelectedTemplate().Id;
            _batch.ContentMetadata = _txtMetadata?.Text?.Trim() ?? string.Empty;
            _batch.GenerationMode = _rbStory != null && _rbStory.Checked ? "Story" : "Quotes";
            _batch.QuoteCount = (int)_numCount.Value;
            _batch.MinDurationSeconds = (int)_numMinDuration.Value;
            _batch.MaxDurationSeconds = (int)_numMaxDuration.Value;
            if (_batch.MinDurationSeconds > _batch.MaxDurationSeconds)
            {
                var swap = _batch.MinDurationSeconds;
                _batch.MinDurationSeconds = _batch.MaxDurationSeconds;
                _batch.MaxDurationSeconds = swap;
            }

            PhilosophyBatchHelper.EnsureBatchDefaults(_batch);
            var preset = PhilosophyContentTemplatePresets.Resolve(_batch.ContentTemplateId);
            if (!preset.SupportsMetadata)
            {
                _batch.ContentMetadata = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(preset.DefaultAmbientKey) && string.IsNullOrWhiteSpace(_batch.AmbientKey))
            {
                _batch.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(preset.DefaultAmbientKey);
            }
        }

        private void SyncQuotesToBatch()
        {
            _batch.Quotes = _quotes
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.Content))
                .ToList();
        }

        private void ReloadQuotesFromBatch()
        {
            _quotes = new BindingList<PhilosophyScriptItem>(
                _batch.Quotes?.Where(q => q != null).ToList() ?? new List<PhilosophyScriptItem>());
            NormalizeQuoteMoods(_quotes);
            _grid.DataSource = _quotes;
            ResizeQuoteGridRows();
        }

        private async Task GenerateContentAsync()
        {
            if (_btnGenerate == null || _btnGenerate.IsDisposed)
            {
                return;
            }

            SaveBatchFieldsFromUi();
            if (string.IsNullOrWhiteSpace(_batch.Topic))
            {
                MessageBox.Show(this, "Nhập chủ đề trước khi tạo nội dung.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!GetSelectedTemplate().UsesGemini)
            {
                MessageBox.Show(
                    this,
                    "Loại «Nhập tay» không dùng Gemini.\r\nThêm hoặc sửa câu trực tiếp trên lưới bên dưới rồi «Lưu».",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _generateCts?.Cancel();
            _generateCts?.Dispose();
            _generateCts = new CancellationTokenSource();

            _btnGenerate.Enabled = false;
            try
            {
                var scripts = await _generateScriptsAsync(_batch, _generateCts.Token).ConfigureAwait(true);
                _generateCts.Token.ThrowIfCancellationRequested();

                _applyScripts(_batch, scripts);
                ReloadQuotesFromBatch();
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _btnGenerate.Enabled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                SaveBatchFieldsFromUi();
                SyncQuotesToBatch();
                _batch.RefreshDerivedFields();
            }

            _generateCts?.Cancel();
            _generateCts?.Dispose();
            base.OnFormClosing(e);
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
    }
}
