using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;
        private readonly AppSettings _settings;
        private List<string> _musicNames;

        private readonly Func<Task> _listenNarrationAsync;
        private readonly Func<bool> _canListenNarration;
        private readonly Func<Task> _generateNarrationAsync;

        private Button _btnListenNarration;
        private Button _btnGenerateNarration;

        private ComboBox _cbMusic;
        private NumericUpDown _numVolume;
        private NumericUpDown _numNarrationSpeed;
        private Label _lblLibrary;
        private ComboBox _cbTtsEngine;
        private ComboBox _cbGender;
        private ComboBox _cbAge;
        private ComboBox _cbLanguage;
        private ComboBox _cbTone;
        private Label _lblVoiceSummary;
        private Label _lblAge;
        private Label _lblLanguage;
        private Label _lblTone;
        private TableLayoutPanel _voiceFieldsTable;
        private bool _voiceUiLock;

        private const int FieldLabelColumnWidth = 346;
        private const int DialogClientWidth = 1584;
        private const int DialogClientHeight = 1104;
        private const int DialogOuterPaddingH = 43;
        private const int DialogOuterPaddingTop = 38;
        private const int DialogOuterPaddingBottom = 34;
        private const int TabPagePaddingH = 29;
        private const int TabPagePaddingTop = 24;
        private const int TabPagePaddingBottom = 19;

        public ShowcaseBackgroundMusicEditorForm(
            ShowcaseVideoItem video,
            AppSettings settings,
            IReadOnlyList<string> musicFileNames,
            Func<Task> listenNarrationAsync = null,
            Func<bool> canListenNarration = null,
            Func<Task> generateNarrationAsync = null)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _settings = settings ?? new AppSettings();
            _listenNarrationAsync = listenNarrationAsync;
            _canListenNarration = canListenNarration;
            _generateNarrationAsync = generateNarrationAsync;
            _musicNames = musicFileNames?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                ?? new List<string>();
            ShowcaseMusicHelper.EnsureVideoDefaults(_video, _settings);

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Âm thanh" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10.5F);
            ClientSize = new Size(DialogClientWidth, DialogClientHeight);
            MinimumSize = new Size(DialogClientWidth, DialogClientHeight);
            Padding = new Padding(DialogOuterPaddingH, DialogOuterPaddingTop, DialogOuterPaddingH, DialogOuterPaddingBottom);
            BuildUi();
            WireAudioPreviewLifecycle();
            LoadFromVideo();
            RefreshNarrationButtons();
        }

        /// <summary>Cập nhật nhãn «Tạo audio» / «Tạo lại audio» và trạng thái nghe thử.</summary>
        public void RefreshNarrationButtons()
        {
            var has = _canListenNarration?.Invoke() ?? false;
            if (_btnGenerateNarration != null && !_btnGenerateNarration.IsDisposed)
            {
                ApplyActionButtonLabel(_btnGenerateNarration, has ? "🔁 Tạo lại audio" : "🎙 Tạo audio");
            }

            if (_btnListenNarration != null && !_btnListenNarration.IsDisposed)
            {
                _btnListenNarration.Enabled = has;
            }
        }

        private void BuildUi()
        {
            var btnDefaults = CreateButton("Mặc định app", Color.FromArgb(70, 78, 96));
            btnDefaults.Click += (_, __) =>
            {
                _video.ShowcaseBackgroundMusicFile = string.Empty;
                _video.ShowcaseMusicVolume = _settings?.VideoMusicVolume ?? 14;
                _video.ShowcaseNarrationSpeedPercent = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;
                _video.ShowcaseTtsEngine = ShowcaseTtsHelper.EngineEdgeTts;
                _video.ShowcaseVoicePresetId = ShowcaseVoicePresetCatalog.DefaultPresetId;
                _video.ShowcaseSfxMasterEnabled = true;
                _video.ShowcaseCtaSfxFile = string.Empty;
                _video.ShowcaseCtaSfxEnabled = false;
                foreach (var scene in _video.Scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }

                    scene.ShowcaseSfxFile = string.Empty;
                    scene.ShowcaseSfxEnabled = false;
                }

                LoadFromVideo();
            };

            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 130,
                BackColor = BackColor,
                Padding = new Padding(0, 24, 0, 6)
            };
            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            flpButtons.Controls.Add(btnDefaults);
            btnBar.Controls.Add(flpButtons);

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5F),
                Padding = new Point(12, 6)
            };

            var tabMusic = new TabPage("Nhạc nền")
            {
                BackColor = BackColor,
                Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, TabPagePaddingBottom)
            };
            var tabVoice = new TabPage("Audio thoại")
            {
                BackColor = BackColor,
                Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, TabPagePaddingBottom),
                AutoScroll = true
            };
            var tabSfx = new TabPage("Hiệu ứng âm thanh")
            {
                BackColor = BackColor,
                Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, 14)
            };
            BuildMusicTab(tabMusic);
            BuildVoiceTab(tabVoice);
            BuildSfxTab(tabSfx);
            tabs.TabPages.Add(tabVoice);
            tabs.TabPages.Add(tabMusic);
            tabs.TabPages.Add(tabSfx);
            WireAudioEditorTabDraw(tabs, BackColor);

            Controls.Add(tabs);
            Controls.Add(btnBar);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };
        }

        private void BuildMusicTab(TabPage tab)
        {
            Label MkLbl(string text, ContentAlignment align = ContentAlignment.MiddleLeft) => new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = align,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 8, 24, 8),
                Font = new Font("Segoe UI", 10.5F)
            };

            _cbMusic = CreateDropDownCombo();
            _cbMusic.Dock = DockStyle.None;
            _cbMusic.Items.Add(VideoReupRowItem.NoMusicSelectionLabel);
            PopulateMusicComboItems();
            _cbMusic.SelectedIndexChanged += (_, __) => UpdateMusicPreviewButtonState();

            _btnPreviewMusic = CreateActionButton(PreviewPlayMusicLabel, Color.FromArgb(88, 118, 158));
            _btnPreviewMusic.Margin = new Padding(14, 4, 0, 16);
            _btnPreviewMusic.Click += (_, __) => TryPreviewMusic();

            var musicRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            musicRow.Controls.Add(_cbMusic);
            musicRow.Controls.Add(_btnPreviewMusic);

            _numVolume = new NumericUpDown
            {
                Width = 192,
                Height = 32,
                Minimum = 0,
                Maximum = 100,
                Value = 14,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5F),
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };

            _lblLibrary = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 4, 0, 14)
            };

            var btnOpenFolder = CreateActionButton("Mở thư mục nhạc", Color.FromArgb(48, 112, 168));
            btnOpenFolder.Click += (_, __) =>
            {
                var dir = ShowcaseMusicHelper.GetMusicLibraryDirectory(_settings);
                try
                {
                    Directory.CreateDirectory(dir);
                    Process.Start("explorer.exe", dir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Mở thư mục nhạc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var btnRefresh = CreateActionButton("Làm mới danh sách", Color.FromArgb(62, 132, 88));
            btnRefresh.Click += (_, __) => ReloadMusicList();

            var libButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0, 6, 0, 4),
                Margin = new Padding(0, 4, 0, 8)
            };
            libButtons.Controls.Add(btnOpenFolder);
            libButtons.Controls.Add(btnRefresh);

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = BackColor,
                Width = tab.ClientSize.Width - tab.Padding.Horizontal,
                Padding = new Padding(0, 10, 0, 12)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldLabelColumnWidth));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));

            var musicFieldHost = CreateVerticallyCenteredFieldHost(musicRow);
            var volumeFieldHost = CreateVerticallyCenteredFieldHost(_numVolume);

            tbl.Controls.Add(MkLbl("File nhạc"), 0, 0);
            tbl.Controls.Add(musicFieldHost, 1, 0);
            tbl.Controls.Add(MkLbl("Âm lượng nhạc (%)"), 0, 1);
            tbl.Controls.Add(volumeFieldHost, 1, 1);
            tbl.Controls.Add(MkLbl("Thư viện", ContentAlignment.MiddleLeft), 0, 2);

            var libStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = BackColor,
                Margin = new Padding(0, 10, 0, 10),
                Padding = new Padding(0, 6, 0, 0)
            };
            libStack.Controls.Add(_lblLibrary);
            libStack.Controls.Add(libButtons);
            tbl.Controls.Add(libStack, 1, 2);

            tab.Controls.Add(tbl);
            tab.Resize += (_, __) =>
            {
                tbl.Width = tab.ClientSize.Width - tab.Padding.Horizontal;
                _lblLibrary.MaximumSize = new Size(Math.Max(384, tbl.Width - 260), 0);
                libButtons.MinimumSize = new Size(Math.Max(384, tbl.Width - 48), 70);
            };
        }

        private static Panel CreateVerticallyCenteredFieldHost(Control field, int minHeight = 72)
        {
            field.Dock = DockStyle.None;
            field.Anchor = AnchorStyles.Left;

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, minHeight),
                Margin = new Padding(0, 6, 0, 6),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            host.Controls.Add(field);

            void centerField()
            {
                if (host.IsDisposed || field.IsDisposed)
                {
                    return;
                }

                field.Left = 0;
                field.Top = Math.Max(0, (host.ClientSize.Height - field.Height) / 2);
            }

            host.Resize += (_, __) => centerField();
            host.HandleCreated += (_, __) => centerField();
            return host;
        }

        private void BuildVoiceTab(TabPage tab)
        {
            Label MkLbl(string text, ContentAlignment align = ContentAlignment.MiddleLeft) => new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                MaximumSize = new Size(FieldLabelColumnWidth - 8, 0),
                TextAlign = align,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 18, 14, 10),
                Padding = new Padding(0)
            };

            _cbTtsEngine = CreateDropDownCombo();
            _cbTtsEngine.Items.Add(new TtsEngineListItem(ShowcaseTtsHelper.EngineEdgeTts, "Edge TTS (miễn phí, tiếng Việt neural — mặc định)"));
            _cbTtsEngine.Items.Add(new TtsEngineListItem(ShowcaseTtsHelper.EngineElevenLabs, "ElevenLabs online (chất lượng cao)"));
            _cbTtsEngine.Items.Add(new TtsEngineListItem(ShowcaseTtsHelper.EngineAskOnCreate, "Hỏi engine khi bấm «Tạo audio»"));
            _cbTtsEngine.SelectedIndexChanged += (_, __) =>
            {
                ApplyVoiceDimensionFieldsForEngine();
                OnVoiceDimensionChanged();
            };

            _cbGender = CreateDropDownCombo();
            _cbAge = CreateDropDownCombo();
            _cbAge.MaxDropDownItems = 12;
            _cbLanguage = CreateDropDownCombo();
            _cbTone = CreateDropDownCombo();
            FillDimensionCombo(_cbGender, ShowcaseVoicePresetDimensions.ListGenderOptions());
            FillDimensionCombo(_cbAge, ShowcaseVoicePresetDimensions.ListAgeOptions());
            FillDimensionCombo(_cbLanguage, ShowcaseVoicePresetDimensions.ListLanguageOptions());
            FillDimensionCombo(_cbTone, ShowcaseVoicePresetDimensions.ListToneOptions());

            void DimChanged(object s, EventArgs e) => OnVoiceDimensionChanged();
            _cbGender.SelectedIndexChanged += DimChanged;
            _cbAge.SelectedIndexChanged += DimChanged;
            _cbLanguage.SelectedIndexChanged += DimChanged;
            _cbTone.SelectedIndexChanged += DimChanged;

            _lblVoiceSummary = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(1080, 0),
                ForeColor = Color.FromArgb(190, 198, 212),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4)
            };

            var speedNote = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(1056, 0),
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.5F),
                Text = "Khi render: app áp dụng % bạn chọn lên thoại, rồi tự tua nhanh bên dài hơn (clip hoặc audio) để khớp khi ghép.",
                Margin = new Padding(0, 0, 0, 14)
            };

            _numNarrationSpeed = new NumericUpDown
            {
                Width = 96,
                DecimalPlaces = 0,
                Increment = 5M,
                Minimum = ShowcaseNarrationSpeedHelper.MinManualSpeedPercent,
                Maximum = ShowcaseNarrationSpeedHelper.MaxManualSpeedPercent,
                Value = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke
            };

            var speedRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };
            speedRow.Controls.Add(new Label
            {
                Text = "Tốc độ thoại:",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 8, 12, 0)
            });
            speedRow.Controls.Add(_numNarrationSpeed);
            speedRow.Controls.Add(new Label
            {
                Text = "%",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(8, 8, 0, 0)
            });

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 9,
                BackColor = BackColor
            };
            _voiceFieldsTable = tbl;
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldLabelColumnWidth));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 9; i++)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            tbl.Controls.Add(MkLbl("Nguồn TTS"), 0, 0);
            tbl.Controls.Add(_cbTtsEngine, 1, 0);
            tbl.Controls.Add(MkLbl("Giới tính / vai"), 0, 1);
            tbl.Controls.Add(_cbGender, 1, 1);
            _lblAge = MkLbl("Độ tuổi");
            tbl.Controls.Add(_lblAge, 0, 2);
            tbl.Controls.Add(_cbAge, 1, 2);
            _lblLanguage = MkLbl("Ngôn ngữ / vùng");
            tbl.Controls.Add(_lblLanguage, 0, 3);
            tbl.Controls.Add(_cbLanguage, 1, 3);
            _lblTone = MkLbl("Tone giọng");
            tbl.Controls.Add(_lblTone, 0, 4);
            tbl.Controls.Add(_cbTone, 1, 4);

            var summaryHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = BackColor,
                Margin = new Padding(0, 8, 0, 0)
            };
            summaryHost.Controls.Add(_lblVoiceSummary);
            tbl.Controls.Add(MkLbl("Kết quả", ContentAlignment.TopLeft), 0, 5);
            tbl.Controls.Add(summaryHost, 1, 5);
            tbl.Controls.Add(MkLbl("Tốc độ clip", ContentAlignment.TopLeft), 0, 6);
            var speedHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                BackColor = BackColor
            };
            speedHost.Controls.Add(speedNote);
            speedHost.Controls.Add(speedRow);
            tbl.Controls.Add(speedHost, 1, 6);

            if (_generateNarrationAsync != null)
            {
                _btnGenerateNarration = CreateActionButton("🎙 Tạo audio", Color.FromArgb(56, 120, 82));
                _btnGenerateNarration.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnGenerateNarration.Enabled = false;
                    try
                    {
                        await _generateNarrationAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                        if (_btnGenerateNarration != null && !_btnGenerateNarration.IsDisposed)
                        {
                            _btnGenerateNarration.Enabled = true;
                        }
                    }
                };

                tbl.Controls.Add(MkLbl("Tạo file", ContentAlignment.TopLeft), 0, 7);
                tbl.Controls.Add(_btnGenerateNarration, 1, 7);
            }

            if (_listenNarrationAsync != null)
            {
                _btnListenNarration = CreateActionButton("Nghe audio thoại", Color.FromArgb(108, 78, 158));
                _btnListenNarration.Enabled = _canListenNarration?.Invoke() ?? false;
                _btnListenNarration.Click += async (_, __) =>
                {
                    _btnListenNarration.Enabled = false;
                    try
                    {
                        await _listenNarrationAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                    }
                };

                tbl.Controls.Add(MkLbl("Thử nghe"), 0, 8);
                tbl.Controls.Add(_btnListenNarration, 1, 8);
            }

            tab.Controls.Add(tbl);
            tab.Resize += (_, __) => ApplyVoiceTabLayout(tab);
            Load += (_, __) => ApplyVoiceTabLayout(tab);
        }

        private void ApplyVoiceTabLayout(TabPage tab)
        {
            if (_voiceFieldsTable == null || tab == null)
            {
                return;
            }

            var innerW = Math.Max(860, tab.ClientSize.Width - tab.Padding.Horizontal);
            _voiceFieldsTable.Width = innerW;
            var wrapW = Math.Max(620, innerW - FieldLabelColumnWidth - 28);
            if (_lblVoiceSummary != null)
            {
                _lblVoiceSummary.MaximumSize = new Size(wrapW, 0);
            }
        }

        private void PopulateMusicComboItems()
        {
            var previous = _cbMusic.SelectedItem?.ToString()?.Trim();
            _cbMusic.Items.Clear();
            _cbMusic.Items.Add(VideoReupRowItem.NoMusicSelectionLabel);
            foreach (var name in _musicNames)
            {
                _cbMusic.Items.Add(name);
            }

            if (!string.IsNullOrWhiteSpace(previous))
            {
                var idx = _cbMusic.Items.IndexOf(previous);
                if (idx >= 0)
                {
                    _cbMusic.SelectedIndex = idx;
                }
            }
        }

        private void ReloadMusicList()
        {
            _musicNames = ShowcaseMusicHelper.ListMusicFileNames(_settings);
            PopulateMusicComboItems();
            LoadFromVideo();
        }

        private void LoadFromVideo()
        {
            var pick = (_video.ShowcaseBackgroundMusicFile ?? string.Empty).Trim();
            if (VideoReupRowItem.IsNoMusicSelection(pick))
            {
                _cbMusic.SelectedIndex = 0;
            }
            else if (!string.IsNullOrWhiteSpace(pick))
            {
                var idx = _cbMusic.Items.IndexOf(pick);
                _cbMusic.SelectedIndex = idx >= 0 ? idx : (_cbMusic.Items.Count > 1 ? 1 : 0);
            }
            else if (_cbMusic.Items.Count > 1)
            {
                _cbMusic.SelectedIndex = 1;
            }
            else
            {
                _cbMusic.SelectedIndex = 0;
            }

            _numVolume.Value = Math.Max(_numVolume.Minimum,
                Math.Min(_numVolume.Maximum, _video.ShowcaseMusicVolume >= 0 ? _video.ShowcaseMusicVolume : 14));

            _numNarrationSpeed.Value = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(
                _video.ShowcaseNarrationSpeedPercent);
            LoadVoiceControlsFromVideo();

            var dir = ShowcaseMusicHelper.GetMusicLibraryDirectory(_settings);
            var count = _musicNames?.Count ?? 0;
            var searchDirs = ShowcaseMusicHelper.GetMusicSearchDirectories(_settings);
            var dirSummary = searchDirs.Count <= 1 ? dir : string.Join("\r\n", searchDirs);
            _lblLibrary.Text = count > 0
                ? count + " file nhạc (.mp3/.wav/.m4a) — quét:\r\n" + dirSummary
                : "Chưa thấy file nhạc — copy vào thư mục chính rồi bấm «Làm mới danh sách»:\r\n" + dir;
            LoadSfxGridFromVideo();
            RefreshPreviewButtons();
        }

        private bool ValidateAndSave()
        {
            var selected = _cbMusic.SelectedItem?.ToString()?.Trim() ?? VideoReupRowItem.NoMusicSelectionLabel;
            if (VideoReupRowItem.IsNoMusicSelection(selected))
            {
                _video.ShowcaseBackgroundMusicFile = VideoReupRowItem.NoMusicSelectionLabel;
            }
            else if (_musicNames != null && _musicNames.Count > 0 &&
                     !_musicNames.Any(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this,
                    "Hãy chọn một file nhạc trong danh sách hoặc «(Không có nhạc)».",
                    "Âm thanh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }
            else
            {
                _video.ShowcaseBackgroundMusicFile = selected;
            }

            _video.ShowcaseMusicVolume = (int)_numVolume.Value;
            _video.ShowcaseNarrationSpeedPercent =
                ShowcaseNarrationSpeedHelper.ClampManualPercent((int)_numNarrationSpeed.Value);

            if (_cbTtsEngine.SelectedItem is TtsEngineListItem engineItem)
            {
                _video.ShowcaseTtsEngine = engineItem.Id;
            }

            _video.ShowcaseVoicePresetId = ResolvePresetIdFromUi();
            if (IsEdgeEngineSelected())
            {
                var edgePreset = ShowcaseVoicePresetCatalog.GetById(_video.ShowcaseVoicePresetId);
                var edgeDims = ShowcaseVoicePresetDimensions.GetForPreset(edgePreset.Id);
                _video.ShowcaseVoiceAgeId = edgeDims.AgeId;
                _video.ShowcaseVoiceLanguageId = edgeDims.LanguageId;
            }
            else
            {
                _video.ShowcaseVoiceAgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(SelectedDimensionId(_cbAge));
                _video.ShowcaseVoiceLanguageId =
                    ShowcaseVoicePresetDimensions.NormalizeLanguageId(SelectedDimensionId(_cbLanguage));
            }

            var engineId = (_cbTtsEngine.SelectedItem as TtsEngineListItem)?.Id ?? ShowcaseTtsHelper.EngineEdgeTts;
            if (string.Equals(engineId, ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                && !ShowcaseVoicePresetDimensions.SupportsEdgeTts(_video.ShowcaseVoiceLanguageId))
            {
                MessageBox.Show(this,
                    "Edge TTS hiện chỉ hỗ trợ tiếng Việt — chọn ElevenLabs cho preset ngôn ngữ ngoại.",
                    "Audio thoại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            ShowcaseMusicHelper.RefreshMusicLabel(_video);
            if (!SaveSfxFromGrid())
            {
                return false;
            }

            return true;
        }

        private void LoadVoiceControlsFromVideo()
        {
            ShowcaseTtsHelper.EnsureVideoDefaults(_video, _settings);
            SelectEngineCombo((_video.ShowcaseTtsEngine ?? string.Empty).Trim());

            _voiceUiLock = true;
            try
            {
                var dims = ShowcaseVoicePresetDimensions.GetForPreset(_video.ShowcaseVoicePresetId);
                var ageId = ShowcaseVoicePresetDimensions.NormalizeAgeId(
                    string.IsNullOrWhiteSpace(_video.ShowcaseVoiceAgeId)
                        ? dims.AgeId
                        : _video.ShowcaseVoiceAgeId);
                SelectDimensionCombo(_cbGender, ShowcaseVoicePresetDimensions.NormalizeGenderId(dims.GenderId));
                SelectDimensionCombo(_cbAge, ageId);
                var langId = ShowcaseVoicePresetDimensions.NormalizeLanguageId(
                    string.IsNullOrWhiteSpace(_video.ShowcaseVoiceLanguageId)
                        ? dims.LanguageId
                        : _video.ShowcaseVoiceLanguageId);
                SelectDimensionCombo(_cbLanguage, langId);
                SelectDimensionCombo(_cbTone, dims.ToneId);
            }
            finally
            {
                _voiceUiLock = false;
            }

            ApplyVoiceDimensionFieldsForEngine();
            RefreshVoiceSummaryAndHint();
        }

        private string SelectedTtsEngineId() =>
            (_cbTtsEngine.SelectedItem as TtsEngineListItem)?.Id ?? ShowcaseTtsHelper.EngineEdgeTts;

        private bool IsEdgeEngineSelected() =>
            string.Equals(SelectedTtsEngineId(), ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase);

        private void ApplyVoiceDimensionFieldsForEngine()
        {
            var edge = IsEdgeEngineSelected();
            SetVoiceTableRowVisible(2, !edge);
            SetVoiceTableRowVisible(3, !edge);
            SetVoiceTableRowVisible(4, !edge);
        }

        private void SetVoiceTableRowVisible(int row, bool visible)
        {
            if (_voiceFieldsTable == null)
            {
                return;
            }

            _voiceFieldsTable.RowStyles[row].SizeType = visible ? SizeType.AutoSize : SizeType.Absolute;
            _voiceFieldsTable.RowStyles[row].Height = visible ? 0f : 0f;
            foreach (Control c in _voiceFieldsTable.Controls)
            {
                if (_voiceFieldsTable.GetRow(c) == row)
                {
                    c.Visible = visible;
                }
            }
        }

        private void OnVoiceDimensionChanged()
        {
            if (_voiceUiLock)
            {
                return;
            }

            RefreshVoiceSummaryAndHint();
        }

        private void RefreshVoiceSummaryAndHint()
        {
            if (IsEdgeEngineSelected())
            {
                var presetId = ShowcaseVoicePresetDimensions.ResolveEdgePresetIdFromGender(
                    SelectedDimensionId(_cbGender));
                var preset = ShowcaseVoicePresetCatalog.GetById(presetId);
                var voice = string.Equals(presetId, "male_south_young", StringComparison.OrdinalIgnoreCase)
                    ? "Nam Minh"
                    : "Hoài My";
                _lblVoiceSummary.Text = "Edge TTS: " + preset.Label + " · giọng " + voice;
                return;
            }

            var set = CurrentDimensionSet();
            var resolved = ShowcaseVoicePresetDimensions.ResolvePresetId(set);
            var resolvedPreset = ShowcaseVoicePresetCatalog.GetById(resolved);
            var exact = ShowcaseVoicePresetDimensions.TryResolveExactPresetId(set, out _);
            var ageLabel = ShowcaseVoicePresetDimensions.ListAgeOptions()
                .FirstOrDefault(a => string.Equals(a.Id, set.AgeId, StringComparison.OrdinalIgnoreCase))?.Label;

            _lblVoiceSummary.Text = "Áp dụng: " + resolvedPreset.Label
                                    + (string.IsNullOrWhiteSpace(ageLabel) ? string.Empty : " · " + ageLabel)
                                    + (exact ? string.Empty : " (gần nhất — chỉnh tone/vùng)");
        }

        private string ResolvePresetIdFromUi()
        {
            if (IsEdgeEngineSelected())
            {
                return ShowcaseVoicePresetDimensions.ResolveEdgePresetIdFromGender(
                    SelectedDimensionId(_cbGender));
            }

            return ShowcaseVoicePresetDimensions.ResolvePresetId(CurrentDimensionSet());
        }

        private ShowcaseVoicePresetDimensions.VoiceDimensionSet CurrentDimensionSet() =>
            new ShowcaseVoicePresetDimensions.VoiceDimensionSet
            {
                GenderId = ShowcaseVoicePresetDimensions.NormalizeGenderId(SelectedDimensionId(_cbGender)),
                AgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(SelectedDimensionId(_cbAge)),
                LanguageId = ShowcaseVoicePresetDimensions.NormalizeLanguageId(SelectedDimensionId(_cbLanguage)),
                ToneId = SelectedDimensionId(_cbTone)
            };

        private void SelectEngineCombo(string engineId)
        {
            var engineIdx = 0;
            for (var i = 0; i < _cbTtsEngine.Items.Count; i++)
            {
                if (_cbTtsEngine.Items[i] is TtsEngineListItem item &&
                    string.Equals(item.Id, engineId, StringComparison.OrdinalIgnoreCase))
                {
                    engineIdx = i;
                    break;
                }
            }

            _cbTtsEngine.SelectedIndex = engineIdx;
        }

        private static void FillDimensionCombo(
            ComboBox combo,
            IReadOnlyList<ShowcaseVoicePresetDimensions.VoiceDimensionChoice> options)
        {
            combo.Items.Clear();
            foreach (var opt in options)
            {
                combo.Items.Add(opt);
            }
        }

        private static void SelectDimensionCombo(ComboBox combo, string id)
        {
            id = (id ?? string.Empty).Trim();
            var idx = 0;
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ShowcaseVoicePresetDimensions.VoiceDimensionChoice item &&
                    string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            combo.SelectedIndex = combo.Items.Count > 0 ? idx : -1;
        }

        private static string SelectedDimensionId(ComboBox combo) =>
            (combo.SelectedItem as ShowcaseVoicePresetDimensions.VoiceDimensionChoice)?.Id ?? string.Empty;

        private sealed class TtsEngineListItem
        {
            public TtsEngineListItem(string id, string label)
            {
                Id = id;
                Label = label;
            }

            public string Id { get; }
            public string Label { get; }
            public override string ToString() => Label;
        }

        private static ComboBox CreateDropDownCombo() => new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 49, 60),
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10.5F),
            Height = 48,
            Width = 768,
            DropDownWidth = 864,
            Margin = new Padding(0, 8, 0, 16),
            IntegralHeight = false
        };

        private static Button CreateActionButton(string text, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 4, 20, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(22, 10, 22, 10),
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.15f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.06f);
            ApplyActionButtonLabel(btn, text);
            return btn;
        }

        private static void ApplyActionButtonLabel(Button btn, string text)
        {
            if (btn == null)
            {
                return;
            }

            btn.Text = (text ?? string.Empty).Trim();
            var textW = TextRenderer.MeasureText(
                btn.Text,
                btn.Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            btn.Width = Math.Min(760, Math.Max(168, textW + btn.Padding.Horizontal + 32));
            btn.Height = 58;
        }

        private static readonly Color[] AudioEditorTabAccents =
        {
            Color.FromArgb(88, 118, 210),
            Color.FromArgb(56, 130, 102),
            Color.FromArgb(176, 118, 56)
        };

        private static void WireAudioEditorTabDraw(TabControl tabs, Color formBackColor)
        {
            tabs.BackColor = formBackColor;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(0, 65);
            tabs.Padding = new Point(22, 16);
            tabs.DrawItem += (sender, e) => AudioEditorTabs_DrawItem(sender, e, formBackColor);
            tabs.Paint += AudioEditorTabs_PaintStrip;
        }

        private static void AudioEditorTabs_PaintStrip(object sender, PaintEventArgs e)
        {
            if (!(sender is TabControl tc) || tc.TabCount == 0)
            {
                return;
            }

            var stripHeight = Math.Max(tc.ItemSize.Height, tc.DisplayRectangle.Top);
            if (stripHeight <= 0)
            {
                return;
            }

            var back = tc.BackColor;
            using (var brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, 0, 0, tc.Width, stripHeight);
            }

            var last = tc.GetTabRect(tc.TabCount - 1);
            var fillLeft = Math.Max(0, last.Right);
            var fillWidth = tc.ClientSize.Width - fillLeft;
            if (fillWidth > 0)
            {
                using (var brush = new SolidBrush(back))
                {
                    e.Graphics.FillRectangle(brush, fillLeft, 0, fillWidth, stripHeight);
                }
            }
        }

        private static void AudioEditorTabs_DrawItem(object sender, DrawItemEventArgs e, Color formBackColor)
        {
            if (!(sender is TabControl tc) || e.Index < 0 || e.Index >= tc.TabPages.Count)
            {
                return;
            }

            var page = tc.TabPages[e.Index];
            var accent = e.Index < AudioEditorTabAccents.Length
                ? AudioEditorTabAccents[e.Index]
                : Color.FromArgb(90, 96, 110);
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = e.Bounds;

            var back = selected
                ? accent
                : BlendColors(accent, formBackColor, 0.22f);
            var fore = selected ? Color.White : ControlPaint.Light(accent, 0.35f);

            using (var backBrush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(backBrush, bounds);
            }

            if (!selected)
            {
                using (var line = new Pen(accent, 2f))
                {
                    e.Graphics.DrawLine(line, bounds.Left + 4, bounds.Top + 2, bounds.Right - 4, bounds.Top + 2);
                }
            }

            var tabFont = new Font(tc.Font.FontFamily, tc.Font.Size, FontStyle.Bold, GraphicsUnit.Point);
            try
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    page.Text,
                    tabFont,
                    bounds,
                    fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            finally
            {
                tabFont.Dispose();
            }
        }

        private static Color BlendColors(Color accent, Color baseColor, float accentWeight)
        {
            var w = Math.Max(0f, Math.Min(1f, accentWeight));
            var inv = 1f - w;
            return Color.FromArgb(
                (int)(accent.R * w + baseColor.R * inv),
                (int)(accent.G * w + baseColor.G * inv),
                (int)(accent.B * w + baseColor.B * inv));
        }

        private static Button CreateButton(string text, Color back) => new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(264, 72),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F),
            Margin = new Padding(16, 0, 0, 0),
            Padding = new Padding(22, 12, 22, 12)
        };
    }
}
