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
        private ComboBox _cbHookTtsEngine;
        private ComboBox _cbBodyTtsEngine;
        private Label _lblVoiceSummary;
        private bool _voiceUiLock;
        private TabPage _voiceTabPage;
        private FlowLayoutPanel _voiceTabRoot;
        private TableLayoutPanel _voiceTabFooter;

        private const int FieldLabelColumnWidth = 519;
        private const int DialogClientWidth = 2376;
        private const int DialogClientHeight = 1656;
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
                _video.ShowcaseHookTtsEngine = ShowcaseTtsHelper.EngineElevenLabs;
                _video.ShowcaseBodyTtsEngine = ShowcaseTtsHelper.EngineEdgeTts;
                _video.ShowcaseVoicePresetId = ShowcaseVoicePresetCatalog.DefaultPresetId;
                _video.ShowcaseHookStyleKey = HookStyleCatalog.StyleHuongdan;
                _video.ShowcaseBodyStyleKey = HookStyleCatalog.StyleKechuyen;
                _video.ShowcaseEdgeRateOffsetPercent = 0;
                _video.ShowcaseEdgePitchOffsetHz = 0;
                _video.ShowcaseBodyEdgeRateOffsetPercent = 0;
                _video.ShowcaseBodyEdgePitchOffsetHz = 0;
                _video.ShowcaseBodyVoicePresetId = string.Empty;
                _video.ShowcaseBodyVoiceAgeId = string.Empty;
                _video.ShowcaseBodyVoiceLanguageId = string.Empty;
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

            _cbTtsEngine = CreateSegmentEngineCombo();
            _cbTtsEngine.Visible = false;

            _lblVoiceSummary = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2100, 0),
                ForeColor = Color.FromArgb(190, 198, 212),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4)
            };

            var speedNote = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2100, 0),
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

            var columnsPanel = BuildHookBodyVoiceColumnsPanel();

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = BackColor,
                Margin = new Padding(0, 16, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldLabelColumnWidth));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 4; i++)
            {
                footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            var summaryHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = BackColor
            };
            summaryHost.Controls.Add(_lblVoiceSummary);
            footer.Controls.Add(MkLbl("Kết quả", ContentAlignment.TopLeft), 0, 0);
            footer.Controls.Add(summaryHost, 1, 0);
            footer.Controls.Add(MkLbl("Tốc độ clip", ContentAlignment.TopLeft), 0, 1);
            var speedHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                BackColor = BackColor
            };
            speedHost.Controls.Add(speedNote);
            speedHost.Controls.Add(speedRow);
            footer.Controls.Add(speedHost, 1, 1);

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

                footer.Controls.Add(MkLbl("Tạo file", ContentAlignment.TopLeft), 0, 2);
                footer.Controls.Add(_btnGenerateNarration, 1, 2);
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

                footer.Controls.Add(MkLbl("Thử nghe"), 0, 3);
                footer.Controls.Add(_btnListenNarration, 1, 3);
            }

            _voiceTabFooter = footer;

            _voiceTabRoot = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = BackColor
            };
            _voiceTabRoot.Controls.Add(columnsPanel);
            _voiceTabRoot.Controls.Add(footer);
            _voiceTabPage = tab;
            tab.Controls.Add(_voiceTabRoot);
            tab.Resize += (_, __) => ApplyVoiceTabLayout(tab);
            Load += (_, __) => ApplyVoiceTabLayout(tab);
        }

        private static int VoiceTabInnerWidth(TabPage tab)
        {
            if (tab == null)
            {
                return 1200;
            }

            return Math.Max(900, tab.ClientSize.Width - tab.Padding.Horizontal);
        }

        private void ApplyVoiceTabLayout(TabPage tab)
        {
            if (tab == null || _lblVoiceSummary == null)
            {
                return;
            }

            var innerW = VoiceTabInnerWidth(tab);
            if (_voiceTabRoot != null)
            {
                _voiceTabRoot.Width = innerW;
                _voiceTabRoot.MinimumSize = new Size(innerW, 0);
            }

            if (_voiceTabFooter != null)
            {
                _voiceTabFooter.AutoSize = false;
                _voiceTabFooter.Width = innerW;
                _voiceTabFooter.MinimumSize = new Size(innerW, 0);
                _voiceTabFooter.PerformLayout();
                _voiceTabFooter.Height = _voiceTabFooter.PreferredSize.Height;
            }

            ApplyVoiceSegmentLayoutFromTab(innerW);
            _lblVoiceSummary.MaximumSize = new Size(Math.Max(900, innerW - FieldLabelColumnWidth - 40), 0);
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

            if (_hookVoice != null)
            {
                SaveVoiceSegmentToVideo(_hookVoice);
            }

            if (_bodyVoice != null)
            {
                SaveVoiceSegmentToVideo(_bodyVoice);
            }

            _video.ShowcaseTtsEngine = _video.ShowcaseBodyTtsEngine;

            var hookLang = _video.ShowcaseVoiceLanguageId;
            var bodyLang = _video.ShowcaseBodyVoiceLanguageId;
            if (string.Equals(_video.ShowcaseHookTtsEngine, ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                && !ShowcaseVoicePresetDimensions.SupportsEdgeTts(hookLang))
            {
                MessageBox.Show(this,
                    "Edge hook cần tiếng Việt — đổi hook sang ElevenLabs hoặc preset Việt.",
                    "Audio thoại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (string.Equals(_video.ShowcaseBodyTtsEngine, ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
                && !ShowcaseVoicePresetDimensions.SupportsEdgeTts(bodyLang))
            {
                MessageBox.Show(this,
                    "Edge thân cần tiếng Việt — chọn ElevenLabs cho thân hoặc preset Việt.",
                    "Audio thoại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (string.Equals(_video.ShowcaseHookTtsEngine, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase)
                || string.Equals(_video.ShowcaseBodyTtsEngine, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                if (!TtsAvailabilityHelper.IsElevenLabsConfigured(_settings))
                {
                    MessageBox.Show(this,
                        "ElevenLabs chưa cấu hình — thêm TTS API Key + Endpoint trong Cài đặt.",
                        "Audio thoại",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return false;
                }
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
            SelectEngineCombo((_video.ShowcaseBodyTtsEngine ?? ShowcaseTtsHelper.EngineEdgeTts).Trim());

            _voiceUiLock = true;
            try
            {
                if (_hookVoice != null)
                {
                    LoadVoiceSegmentFromVideo(_hookVoice);
                }

                if (_bodyVoice != null)
                {
                    LoadVoiceSegmentFromVideo(_bodyVoice);
                }
            }
            finally
            {
                _voiceUiLock = false;
            }

            ApplyVoiceDimensionFieldsForEngine();
            RefreshVoiceSummaryAndHint();
            ApplyVoiceSegmentLayoutFromTab(VoiceTabInnerWidth(_voiceTabPage));
        }

        private void ApplyVoiceDimensionFieldsForEngine()
        {
            ApplySegmentColumnVisibility(_hookVoice);
            ApplySegmentColumnVisibility(_bodyVoice);
            ApplyVoiceSegmentLayoutFromTab(VoiceTabInnerWidth(_voiceTabPage));
        }

        private void ApplySegmentColumnVisibility(VoiceSegmentUi seg)
        {
            if (seg == null)
            {
                return;
            }

            var eleven = SegmentIsEleven(seg);
            var edge = SegmentIsEdge(seg);
            SetSegmentTableRowVisible(seg, 1, eleven);
            SetSegmentTableRowVisible(seg, 2, eleven);
            // Hook: "Tone giọng" bị bỏ hẳn — "Phong cách hook" lo hết cảm xúc + rate/pitch/stability tuỳ chỉnh.
            SetSegmentTableRowVisible(seg, 3, eleven && !seg.IsHook);
            var showCustomToneRow = seg.IsHook
                ? SegmentElevenCustomVoiceStyle(seg)
                : (eleven && ShowcaseElevenToneHelper.IsCustomTone(SelectedDimensionId(seg.CbTone)));
            var showStyleRow = edge || (eleven && seg.IsHook);
            // Hook: Style (row 4) nằm TRÊN Tinh chỉnh giọng (row 5) — đảo lại so với Thân (Tone → Tinh chỉnh → Style).
            var styleRow = seg.IsHook ? 4 : 5;
            var customToneRow = seg.IsHook ? 5 : 4;
            SetSegmentTableRowVisible(seg, styleRow, showStyleRow);
            SetSegmentTableRowVisible(seg, customToneRow, showCustomToneRow);
            SetSegmentTableRowVisible(seg, 6, edge);
            if (seg.LblStyle != null)
            {
                seg.LblStyle.Text = edge ? "Phong cách Edge" : "Phong cách hook";
            }

            RefreshSegmentStyleComboItems(seg);

            var rate = seg.IsHook ? _video.ShowcaseEdgeRateOffsetPercent : _video.ShowcaseBodyEdgeRateOffsetPercent;
            var pitch = seg.IsHook ? _video.ShowcaseEdgePitchOffsetHz : _video.ShowcaseBodyEdgePitchOffsetHz;
            ApplySegmentEdgeProsodyUi(seg, rate, pitch);

            var stability = seg.IsHook ? _video.ShowcaseElevenCustomStabilityPercent : _video.ShowcaseBodyElevenCustomStabilityPercent;
            var similarity = seg.IsHook ? _video.ShowcaseElevenCustomSimilarityPercent : _video.ShowcaseBodyElevenCustomSimilarityPercent;
            var style = seg.IsHook ? _video.ShowcaseElevenCustomStylePercent : _video.ShowcaseBodyElevenCustomStylePercent;
            ApplySegmentCustomToneUi(seg, stability, similarity, style);
            SyncSegmentShellHeight(seg);
        }

        private void RefreshVoiceSummaryAndHint()
        {
            if (_hookVoice == null || _bodyVoice == null)
            {
                return;
            }

            _lblVoiceSummary.Text = "Hook: " + SegmentSummaryLine(_hookVoice, _video, isHook: true)
                                    + "\r\nThân: " + SegmentSummaryLine(_bodyVoice, _video, isHook: false);
        }

        private static void FillSegmentEngineCombo(ComboBox combo)
        {
            combo.Items.Clear();
            combo.Items.Add(new TtsEngineListItem(ShowcaseTtsHelper.EngineEdgeTts, "Edge TTS (Microsoft, tiếng Việt)"));
            combo.Items.Add(new TtsEngineListItem(ShowcaseTtsHelper.EngineElevenLabs, "ElevenLabs (API, đa ngôn ngữ)"));
            combo.SelectedIndex = 0;
        }

        private void OnVoiceDimensionChanged()
        {
            if (_voiceUiLock)
            {
                return;
            }

            RefreshVoiceSummaryAndHint();
        }

        private static string SegmentEngineLabel(string engineId)
        {
            if (string.Equals(engineId, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
            {
                return "ElevenLabs";
            }

            return "Edge TTS";
        }

        private static string SelectedSegmentEngineId(ComboBox combo) =>
            (combo.SelectedItem as TtsEngineListItem)?.Id ?? ShowcaseTtsHelper.EngineEdgeTts;

        private static Label CreateEdgeProsodyValueLabel() => new Label
        {
            AutoSize = true,
            MaximumSize = new Size((int)Math.Round(88 * 1.3f), 0),
            TextAlign = ContentAlignment.TopRight,
            ForeColor = Color.FromArgb(190, 198, 212),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = "±0%",
            Margin = Padding.Empty
        };

        private ComboBox CreateSegmentEngineCombo()
        {
            var combo = CreateDropDownCombo();
            FillSegmentEngineCombo(combo);
            return combo;
        }

        private void SelectSegmentEngineCombo(ComboBox combo, string engineId)
        {
            engineId = ShowcaseTtsHelper.NormalizeEngineStorageId(engineId);
            if (string.IsNullOrWhiteSpace(engineId))
            {
                engineId = ShowcaseTtsHelper.EngineEdgeTts;
            }

            var idx = 0;
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is TtsEngineListItem item &&
                    string.Equals(item.Id, engineId, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            combo.SelectedIndex = combo.Items.Count > 0 ? idx : -1;
        }

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

        private sealed class HookStyleListItem
        {
            public HookStyleListItem(string key, string label)
            {
                Key = key;
                Label = label;
            }

            public string Key { get; }
            public string Label { get; }

            public override string ToString() => Label;
        }

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
            Width = 1152,
            DropDownWidth = 1200,
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
