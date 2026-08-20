using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;
        private readonly AppSettings _settings;
        private List<string> _musicNames;

        private readonly Func<Task> _generateHookNarrationAsync;
        private readonly Func<Task> _generateBodyNarrationAsync;
        private readonly Func<Task> _listenHookNarrationAsync;
        private readonly Func<Task> _listenBodyNarrationAsync;
        private readonly Func<bool> _canListenHookNarration;
        private readonly Func<bool> _canListenBodyNarration;
        private readonly Func<Task> _renderFullMixedAudioAsync;
        private readonly Func<Task> _listenFullMixedAudioAsync;
        private readonly Func<bool> _canRenderFullMixedAudio;
        private readonly Func<bool> _canListenFullMixedAudio;
        private readonly Func<bool> _openScriptEditor;

        private readonly bool _philosophyMode;
        private string _philosophyAmbientKey;
        private readonly string _philosophyMusicLibrarySummary;
        private ComboBox _cbPhilosophyAmbient;
        private Label _lblPhilosophyAmbientHint;

        private JellyButton _btnGenerateHookNarration;
        private JellyButton _btnGenerateBodyNarration;
        private JellyButton _btnListenHookNarration;
        private JellyButton _btnListenBodyNarration;
        private JellyButton _btnRenderFullMixedAudio;
        private JellyButton _btnListenFullMixedAudio;
        private JellyButton _btnReviewScript;

        private ComboBox _cbMusic;
        private TrackBar _trkMusicVolume;
        private Label _lblMusicVolumeValue;
        private Panel _musicVolumeSliderHost;
        private Label _lblLibrary;
        private ComboBox _cbTtsEngine;
        private ComboBox _cbHookTtsEngine;
        private ComboBox _cbBodyTtsEngine;
        private bool _voiceUiLock;
        private Control _voiceTabHost;
        private TableLayoutPanel _philosophyVoiceTabLayout;
        private FlowLayoutPanel _voiceTabRoot;
        private bool _philosophyVoiceLayoutReady;
        private FlowLayoutPanel _voiceFooterActionsRow;
        private Panel _voiceFooterActionsHost;
        private Panel _voiceReviewScriptRow;
        private Panel _footerBarPanel;
        private TableLayoutPanel _footerStack;
        private Label _lblOperationStatus;

        private const int DialogClientWidth = 2376;
        private const int DialogClientHeight = 1656;
        private const int DialogOuterPaddingH = 39;
        private const int DialogOuterPaddingTop = 34;
        private const int DialogOuterPaddingBottom = 16;
        private const int TabPagePaddingH = 26;
        private const int TabPagePaddingTop = 22;
        private const int TabPagePaddingBottom = 17;
        private const int VoiceTabPaddingBottom = 12;
        private const int MinTabContentHeight = 480;
        private const int VoiceTabHeightFitBuffer = 56;
        private const int FooterBarMinHeight = 248;
        private const int TabFooterSpacerHeight = 14;
        private const int PhilosophyDialogClientWidth = 2560;
        private const int PhilosophyDialogClientHeight = 1607;
        private const int PhilosophyDialogMinWidth = 1840;
        private const int PhilosophyDialogMinHeight = 1215;
        private const float PhilosophyDialogVerticalBias = 0.18f;
        private const float MusicComboWidthScale = 0.64f;
        private const float MusicVolumeSliderWidthScale = 0.5f;
        private const int MusicTabLabelColumnWidth = 256;
        private const int MusicTabRowHeight = 158;
        private const int MusicTabRowMarginV = 12;

        public ShowcaseBackgroundMusicEditorForm(
            ShowcaseVideoItem video,
            AppSettings settings,
            IReadOnlyList<string> musicFileNames,
            Func<Task> generateHookNarrationAsync = null,
            Func<Task> generateBodyNarrationAsync = null,
            Func<Task> listenHookNarrationAsync = null,
            Func<Task> listenBodyNarrationAsync = null,
            Func<bool> canListenHookNarration = null,
            Func<bool> canListenBodyNarration = null,
            Func<Task> renderFullMixedAudioAsync = null,
            Func<Task> listenFullMixedAudioAsync = null,
            Func<bool> canRenderFullMixedAudio = null,
            Func<bool> canListenFullMixedAudio = null,
            Func<bool> openScriptEditor = null,
            bool philosophyMode = false,
            string philosophyAmbientKey = null,
            string philosophyMusicLibrarySummary = null,
            PhilosophyBatchItem philosophyBatch = null)
        {
            _philosophyMode = philosophyMode;
            _philosophyAmbientKey = PhilosophyAmbientCatalog.NormalizeKey(philosophyAmbientKey);
            _philosophyMusicLibrarySummary = philosophyMusicLibrarySummary;
            _philosophyBatch = philosophyBatch;
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _settings = settings ?? new AppSettings();
            _generateHookNarrationAsync = generateHookNarrationAsync;
            _generateBodyNarrationAsync = generateBodyNarrationAsync;
            _listenHookNarrationAsync = listenHookNarrationAsync;
            _listenBodyNarrationAsync = listenBodyNarrationAsync;
            _canListenHookNarration = canListenHookNarration;
            _canListenBodyNarration = canListenBodyNarration;
            _renderFullMixedAudioAsync = renderFullMixedAudioAsync;
            _listenFullMixedAudioAsync = listenFullMixedAudioAsync;
            _canRenderFullMixedAudio = canRenderFullMixedAudio;
            _canListenFullMixedAudio = canListenFullMixedAudio;
            _openScriptEditor = openScriptEditor;
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
            if (_philosophyMode)
            {
                var philosophySize = ResolvePhilosophyDialogClientSize();
                ClientSize = philosophySize;
                MinimumSize = new Size(
                    Math.Min(PhilosophyDialogMinWidth, philosophySize.Width),
                    Math.Min(PhilosophyDialogMinHeight, philosophySize.Height));
            }
            else
            {
                ClientSize = new Size(DialogClientWidth, DialogClientHeight);
                MinimumSize = new Size(DialogClientWidth, DialogClientHeight);
            }
            Padding = new Padding(DialogOuterPaddingH, DialogOuterPaddingTop, DialogOuterPaddingH, DialogOuterPaddingBottom);
            BuildUi();
            AppFormTitleBarHelper.ApplyShowcaseAudioDialogTitleBar(this);
            WireAudioPreviewLifecycle();
            LoadFromVideo();
            if (_philosophyMode)
            {
                ApplyPhilosophyModeUi();
                FinalizePhilosophyDialogLayout();
            }
            else
            {
                RefreshNarrationButtons();
            }

            Shown += (_, __) => OnAudioDialogShown();
        }

        private void FinalizePhilosophyDialogLayout()
        {
            if (!_philosophyMode)
            {
                return;
            }

            EnsurePhilosophyQuotesAudioGrid();
            RefreshPhilosophyQuoteBatchActionLabels();
        }

        private void OnAudioDialogShown()
        {
            if (_philosophyMode)
            {
                AppendPhilosophyQuoteSfxComboItems();
                _philosophyVoiceLayoutReady = true;
                if (_voiceTabHost != null)
                {
                    ApplyVoiceTabLayout(_voiceTabHost);
                }

                ApplyPhilosophyDialogPlacement();
                RefreshNarrationButtons();
                return;
            }

            FitShowcaseDialogClientHeightToVoiceTabContent();
        }

        private void ApplyPhilosophyDialogPlacement()
        {
            if (!_philosophyMode || IsDisposed)
            {
                return;
            }

            var screen = Owner != null && !Owner.IsDisposed && Owner.Visible
                ? Screen.FromControl(Owner).WorkingArea
                : Screen.FromControl(this).WorkingArea;

            Rectangle anchor;
            if (Owner != null && !Owner.IsDisposed && Owner.Visible)
            {
                anchor = Owner.RectangleToScreen(new Rectangle(0, 0, Owner.Width, Owner.Height));
            }
            else
            {
                anchor = screen;
            }

            var x = anchor.Left + Math.Max(0, (anchor.Width - Width) / 2);
            if (x + Width > screen.Right - 12)
            {
                x = screen.Right - 12 - Width;
            }

            if (x < screen.Left + 12)
            {
                x = screen.Left + 12;
            }

            var centeredY = anchor.Top + Math.Max(0, (anchor.Height - Height) / 2);
            var bias = (int)Math.Round(Math.Max(0, anchor.Height - Height) * PhilosophyDialogVerticalBias);
            var y = Math.Max(screen.Top + 12, centeredY - bias);
            if (y + Height > screen.Bottom - 12)
            {
                y = Math.Max(screen.Top + 12, screen.Bottom - 12 - Height);
            }

            StartPosition = FormStartPosition.Manual;
            Location = new Point(x, y);
        }

        private static Size ResolvePhilosophyDialogClientSize()
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 2560, 1440);
            var width = Math.Min(PhilosophyDialogClientWidth, Math.Max(PhilosophyDialogMinWidth, area.Width - 48));
            var height = Math.Min(PhilosophyDialogClientHeight, Math.Max(PhilosophyDialogMinHeight, area.Height - 48));
            return new Size(width, height);
        }

        /// <summary>Cập nhật nhãn tạo lại và trạng thái nghe thử hook/thân.</summary>
        public void RefreshNarrationButtons()
        {
            var hasHook = !_philosophyMode && (_canListenHookNarration?.Invoke() ?? false);
            var hasBody = _canListenBodyNarration?.Invoke() ?? false;

            if (_btnGenerateHookNarration != null && !_btnGenerateHookNarration.IsDisposed)
            {
                _btnGenerateHookNarration.Text = hasHook ? "Tạo lại hook" : "Tạo audio hook";
            }

            if (_btnGenerateBodyNarration != null && !_btnGenerateBodyNarration.IsDisposed)
            {
                if (_philosophyMode)
                {
                    RefreshPhilosophyQuoteBatchActionLabels();
                }
                else
                {
                    _btnGenerateBodyNarration.Text = hasBody ? "Tạo lại thân" : "Tạo audio thân";
                }
            }

            if (_btnListenHookNarration != null && !_btnListenHookNarration.IsDisposed)
            {
                _btnListenHookNarration.Enabled = hasHook;
            }

            if (_btnListenBodyNarration != null && !_btnListenBodyNarration.IsDisposed && !_philosophyMode)
            {
                _btnListenBodyNarration.Enabled = hasBody;
            }

            if (_hookVoice != null)
            {
                LayoutVoiceNarrationButtons(_hookVoice);
            }

            if (_bodyVoice != null)
            {
                LayoutVoiceNarrationButtons(_bodyVoice);
            }

            if (_btnRenderFullMixedAudio != null && !_btnRenderFullMixedAudio.IsDisposed)
            {
                _btnRenderFullMixedAudio.Enabled = _canRenderFullMixedAudio?.Invoke() ?? false;
            }

            if (_btnListenFullMixedAudio != null && !_btnListenFullMixedAudio.IsDisposed && !_philosophyMode)
            {
                _btnListenFullMixedAudio.Enabled = _canListenFullMixedAudio?.Invoke() ?? false;
            }

            SyncFooterBarHeight();
        }

        /// <summary>Lưu nhạc + giọng từ dialog xuống <see cref="ShowcaseVideoItem"/>.</summary>
        public bool SaveToVideo() => ValidateAndSave();

        /// <summary>Một dòng trạng thái / log Showcase mirror (footer dialog).</summary>
        public void SetOperationStatus(string message)
        {
            if (IsDisposed || _lblOperationStatus == null || _lblOperationStatus.IsDisposed)
            {
                return;
            }

            void Apply()
            {
                var text = FormatShowcaseStatusLine(message);
                _lblOperationStatus.Text = string.IsNullOrWhiteSpace(text)
                    ? "Sẵn sàng. Log TTS / Render / nghe thử hiển thị tại đây."
                    : text;
                var isError = (message ?? string.Empty).IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                              || (message ?? string.Empty).IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;
                _lblOperationStatus.ForeColor = isError
                    ? Color.FromArgb(232, 140, 120)
                    : Color.FromArgb(148, 156, 172);
            }

            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(Apply));
                }
                else
                {
                    Apply();
                }

                return;
            }

            Apply();
        }

        private static string FormatShowcaseStatusLine(string message)
        {
            message = (message ?? string.Empty).Trim();
            if (message.Length == 0)
            {
                return string.Empty;
            }

            const string prefix = "[Showcase]";
            if (message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                message = message.Substring(prefix.Length).TrimStart();
            }

            const int maxLen = 140;
            if (message.Length <= maxLen)
            {
                return message;
            }

            return "…" + message.Substring(message.Length - (maxLen - 1));
        }

        private void BuildUi()
        {
            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));
            btnOk.DialogResult = DialogResult.OK;
            AcceptButton = btnOk;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = FooterBarMinHeight,
                BackColor = BackColor,
                Padding = new Padding(0, 14, 0, 18)
            };
            _footerBarPanel = btnBar;

            _lblOperationStatus = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(3200, 36),
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.FromArgb(148, 156, 172),
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
                Text = "Sẵn sàng. Log TTS / Render / nghe thử hiển thị tại đây.",
                Margin = new Padding(4, 10, 8, 6),
                AutoEllipsis = true,
                UseCompatibleTextRendering = true
            };

            var statusRow = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                BackColor = BackColor,
                MinimumSize = new Size(0, 40),
                Padding = new Padding(0, 4, 0, 4)
            };
            statusRow.Controls.Add(_lblOperationStatus);
            statusRow.Resize += (_, __) =>
            {
                if (_lblOperationStatus != null && !_lblOperationStatus.IsDisposed)
                {
                    _lblOperationStatus.MaximumSize = new Size(
                        Math.Max(200, statusRow.ClientSize.Width - 16),
                        36);
                }
            };

            var voiceActionsRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 4, 0, 8)
            };
            _voiceFooterActionsRow = voiceActionsRow;

            var footerRule = new Panel
            {
                Height = 1,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(72, 78, 92),
                Margin = Padding.Empty
            };

            var flpButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Padding = new Padding(0, 4, 0, 6),
                Margin = new Padding(0, 2, 0, 4)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);

            _footerStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = _philosophyMode ? 3 : 4,
                BackColor = BackColor,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 2, 16)
            };
            var footerStack = _footerStack;
            footerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (!_philosophyMode)
            {
                footerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            footerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            footerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            statusRow.Margin = new Padding(0, 0, 0, 2);
            footerStack.Controls.Add(statusRow, 0, 0);

            if (_philosophyMode)
            {
                _voiceTabHost = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = BackColor,
                    Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, VoiceTabPaddingBottom)
                };
                BuildVoiceTab(_voiceTabHost);
                AttachVoiceFooterActionButtons(voiceActionsRow);
                ApplyPhilosophyVoiceActionButtons();
                Controls.Add(_voiceTabHost);

                footerStack.Controls.Add(footerRule, 0, 1);
                footerStack.Controls.Add(WrapFooterRight(flpButtons), 0, 2);
            }
            else
            {
                AttachVoiceFooterActionButtons(voiceActionsRow);
                _voiceFooterActionsHost = (Panel)WrapFooterCentered(voiceActionsRow);
                footerStack.Controls.Add(_voiceFooterActionsHost, 0, 1);
                footerStack.Controls.Add(footerRule, 0, 2);
                footerStack.Controls.Add(WrapFooterRight(flpButtons), 0, 3);

                var tabs = new TabControl
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 10.5F),
                    Padding = new Point(12, 6),
                    BackColor = BackColor
                };

                var tabMusic = new TabPage("Nhạc nền")
                {
                    BackColor = BackColor,
                    Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, TabPagePaddingBottom)
                };
                var tabVoice = new TabPage("Audio thoại")
                {
                    BackColor = BackColor,
                    Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, VoiceTabPaddingBottom),
                    AutoScroll = true
                };
                var tabSfx = new TabPage("Hiệu ứng âm thanh")
                {
                    BackColor = BackColor,
                    Padding = new Padding(TabPagePaddingH, TabPagePaddingTop, TabPagePaddingH, 13)
                };
                _voiceTabHost = tabVoice;
                BuildVoiceTab(tabVoice);
                tabs.TabPages.Add(tabVoice);
                BuildMusicTab(tabMusic);
                BuildSfxTab(tabSfx);
                tabs.TabPages.Add(tabMusic);
                tabs.TabPages.Add(tabSfx);
                WireAudioEditorTabDraw(tabs, BackColor);
                Controls.Add(tabs);
            }

            btnBar.Controls.Add(footerStack);

            var tabFooterSpacer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = TabFooterSpacerHeight,
                BackColor = BackColor
            };
            Controls.Add(tabFooterSpacer);
            Controls.Add(btnBar);

            btnOk.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };

            SyncFooterBarHeight();
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
                Margin = new Padding(0, 16, 24, 16),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
            };

            _cbMusic = CreateDropDownCombo();
            _cbMusic.Dock = DockStyle.None;
            _cbMusic.Width = (int)Math.Round(_cbMusic.Width * MusicComboWidthScale);
            _cbMusic.DropDownWidth = (int)Math.Round(_cbMusic.DropDownWidth * MusicComboWidthScale);
            _cbMusic.Items.Add(VideoReupRowItem.NoMusicSelectionLabel);
            PopulateMusicComboItems();
            _cbMusic.SelectedIndexChanged += (_, __) => UpdateMusicPreviewButtonState();

            _btnPreviewMusic = CreateActionButton(
                "btnShowcaseAudioPreviewMusic",
                PreviewPlayMusicLabel,
                Color.FromArgb(88, 118, 158));
            _btnPreviewMusic.Margin = new Padding(14, 0, 0, 0);
            _btnPreviewMusic.Click += (_, __) => TryPreviewMusic();

            var musicRowCrossHeight = Form1.AppJellyButtonHeight;
            var comboInsetY = Math.Max(0, (musicRowCrossHeight - _cbMusic.Height) / 2);
            _cbMusic.Margin = new Padding(0, comboInsetY, 0, comboInsetY);

            var musicRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            musicRow.Controls.Add(_cbMusic);
            musicRow.Controls.Add(_btnPreviewMusic);

            var volumeSliderHost = BuildMusicVolumeSliderHost();
            volumeSliderHost.Dock = DockStyle.Fill;
            volumeSliderHost.Margin = new Padding(0, MusicTabRowMarginV, 0, MusicTabRowMarginV);

            _lblLibrary = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 8, 0, 28)
            };

            var btnOpenFolder = CreateActionButton(
                "btnShowcaseAudioOpenMusicFolder",
                "Mở thư mục nhạc",
                Color.FromArgb(48, 112, 168));
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

            var btnRefresh = CreateActionButton(
                "btnShowcaseAudioRefreshMusicList",
                "Làm mới danh sách",
                Color.FromArgb(62, 132, 88));
            btnRefresh.Click += (_, __) => ReloadMusicList();

            var libButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor,
                Padding = new Padding(0, 12, 0, 8),
                Margin = new Padding(0, 8, 0, 16)
            };
            libButtons.Controls.Add(btnOpenFolder);
            libButtons.Controls.Add(btnRefresh);

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = BackColor,
                Padding = new Padding(0, 20, 0, 24)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MusicTabLabelColumnWidth));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 920));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, MusicTabRowHeight));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, MusicTabRowHeight));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));

            var musicFieldHost = BuildMusicComboRowHost(musicRow, musicRowCrossHeight);

            tbl.Controls.Add(MkLbl("File nhạc"), 0, 0);
            tbl.Controls.Add(musicFieldHost, 1, 0);
            tbl.Controls.Add(MkLbl("Âm lượng nhạc (%)"), 0, 1);
            tbl.Controls.Add(volumeSliderHost, 1, 1);
            tbl.Controls.Add(MkLbl("Thư viện", ContentAlignment.MiddleLeft), 0, 2);

            var libStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = BackColor,
                Margin = new Padding(0, 20, 0, 20),
                Padding = new Padding(0, 12, 0, 0)
            };
            libStack.Controls.Add(_lblLibrary);
            libStack.Controls.Add(libButtons);
            tbl.Controls.Add(libStack, 1, 2);

            var layoutShell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor
            };
            layoutShell.Controls.Add(tbl);
            tab.Controls.Add(layoutShell);

            void LayoutMusicTabBlock()
            {
                if (tab.IsDisposed || tbl.IsDisposed || layoutShell.IsDisposed)
                {
                    return;
                }

                var avail = Math.Max(360, tab.ClientSize.Width - tab.Padding.Horizontal);
                var musicRowW = musicRow.PreferredSize.Width;
                LayoutMusicVolumeSliderRow();
                var volHost = _musicVolumeSliderHost;
                var volRow = volHost?.Controls.Count > 0 ? volHost.Controls[0] : null;
                var volRowW = volRow != null && !volRow.IsDisposed ? volRow.Width + 24 : 280;
                var libW = Math.Max(libButtons.PreferredSize.Width, _lblLibrary.PreferredSize.Width);
                var contentW = Math.Max(musicRowW, Math.Max(volRowW, libW));
                contentW = Math.Max(320, contentW);
                var blockW = Math.Min(avail, MusicTabLabelColumnWidth + contentW + 8);

                tbl.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, Math.Max(280, blockW - MusicTabLabelColumnWidth));
                tbl.Width = blockW;
                tbl.PerformLayout();

                var left = tab.Padding.Left + Math.Max(0, (avail - blockW) / 2);
                var availH = Math.Max(200, layoutShell.ClientSize.Height);
                var top = Math.Max(tab.Padding.Top, (availH - tbl.Height) / 2);
                tbl.Location = new Point(left, top);

                _lblLibrary.MaximumSize = new Size(Math.Max(240, blockW - MusicTabLabelColumnWidth - 12), 0);
                libButtons.MinimumSize = new Size(Math.Max(240, blockW - MusicTabLabelColumnWidth - 12), 70);
                LayoutMusicVolumeSliderRow();
            }

            tab.Resize += (_, __) => LayoutMusicTabBlock();
            layoutShell.Resize += (_, __) => LayoutMusicTabBlock();
            musicRow.SizeChanged += (_, __) => LayoutMusicTabBlock();
            tbl.HandleCreated += (_, __) => LayoutMusicTabBlock();
            Shown += (_, __) => LayoutMusicTabBlock();
            LayoutMusicTabBlock();
        }

        private static Panel BuildMusicComboRowHost(Control row, int minHeight)
        {
            row.Dock = DockStyle.None;
            row.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, minHeight),
                Margin = new Padding(0, MusicTabRowMarginV, 0, MusicTabRowMarginV),
                BackColor = Color.FromArgb(31, 34, 42)
            };
            host.Controls.Add(row);

            void layoutRow()
            {
                if (host.IsDisposed || row.IsDisposed)
                {
                    return;
                }

                row.Left = 0;
                row.Top = Math.Max(0, (host.ClientSize.Height - row.Height) / 2);
            }

            host.Resize += (_, __) => layoutRow();
            host.HandleCreated += (_, __) => layoutRow();
            host.Layout += (_, __) => layoutRow();
            row.Layout += (_, __) => layoutRow();
            row.SizeChanged += (_, __) => layoutRow();
            layoutRow();
            return host;
        }

        private void BuildVoiceTab(Control host)
        {
            _cbTtsEngine = CreateSegmentEngineCombo();
            _cbTtsEngine.Visible = false;

            var columnsPanel = _philosophyMode
                ? BuildPhilosophyVoiceColumnsPanel()
                : BuildHookBodyVoiceColumnsPanel();

            _voiceReviewScriptRow = new Panel
            {
                AutoSize = false,
                BackColor = BackColor,
                Margin = new Padding(0, 8, 0, 14)
            };
            if (_openScriptEditor != null)
            {
                AttachVoiceReviewScriptButton(_voiceReviewScriptRow);
            }

            _voiceTabHost = host;
            if (_philosophyMode)
            {
                _philosophyVoiceTabLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = BackColor,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                _philosophyVoiceTabLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _philosophyVoiceTabLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _philosophyVoiceTabLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                columnsPanel.Dock = DockStyle.Top;
                columnsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _philosophyVoiceTabLayout.Controls.Add(columnsPanel, 0, 0);
                _voiceTabRoot = null;
                host.Controls.Add(_philosophyVoiceTabLayout);
            }
            else
            {
                _voiceTabRoot = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    BackColor = BackColor
                };
                _voiceTabRoot.Controls.Add(columnsPanel);
                if (_openScriptEditor != null)
                {
                    _voiceTabRoot.Controls.Add(_voiceReviewScriptRow);
                }

                host.Controls.Add(_voiceTabRoot);
            }

            if (_philosophyMode)
            {
                host.Resize += (_, __) =>
                {
                    if (_philosophyVoiceLayoutReady)
                    {
                        ApplyVoiceTabLayout(host);
                    }
                };
            }
            else
            {
                host.Resize += (_, __) => ApplyVoiceTabLayout(host);
                Load += (_, __) => ApplyVoiceTabLayout(host);
            }
        }

        private static int VoiceTabInnerWidth(Control host)
        {
            if (host == null)
            {
                return 1200;
            }

            return Math.Max(900, host.ClientSize.Width - host.Padding.Horizontal);
        }

        private void ApplyVoiceTabLayout(Control host)
        {
            if (host == null || (_philosophyMode && !_philosophyVoiceLayoutReady))
            {
                return;
            }

            var innerW = VoiceTabInnerWidth(host);
            if (!_philosophyMode && _voiceTabRoot != null)
            {
                _voiceTabRoot.Width = innerW;
                _voiceTabRoot.MinimumSize = new Size(innerW, 0);
            }
            else if (_philosophyMode && _voiceColumnsPanel != null)
            {
                _voiceColumnsPanel.MinimumSize = new Size(Math.Max(900, innerW), 0);
                _voiceColumnsPanel.Dock = DockStyle.Top;
            }

            if (_voiceReviewScriptRow != null && _voiceReviewScriptRow.Visible
                && _btnReviewScript != null && !_btnReviewScript.IsDisposed)
            {
                _voiceReviewScriptRow.Width = innerW;
                Form1.ResizeAppJellyButton(_btnReviewScript, minWidth: 96);
                _voiceReviewScriptRow.Height = _btnReviewScript.Height + _btnReviewScript.Margin.Vertical;
                _btnReviewScript.Left = Math.Max(0, (innerW - _btnReviewScript.Width) / 2);
                _btnReviewScript.Top = 0;
            }

            ApplyVoiceSegmentLayoutFromTab(innerW);
            if (_philosophyMode)
            {
                ApplyPhilosophyQuotesGridColumnWidths();
                LayoutPhilosophyQuotesGrid();
            }

            CompactVoiceTabRootHeight(innerW);
        }

        private void CompactVoiceTabRootHeight(int innerW)
        {
            if (_philosophyMode)
            {
                return;
            }

            if (_voiceTabRoot == null)
            {
                return;
            }

            _voiceTabRoot.PerformLayout();
            var pref = _voiceTabRoot.GetPreferredSize(new Size(innerW, 0));
            _voiceTabRoot.Size = new Size(innerW, Math.Max(0, pref.Height));
        }

        private int MeasurePhilosophyDialogNaturalClientHeight(int innerW)
        {
            SyncFooterBarHeight();

            var tabHeader = 0;

            var voiceH = 0;
            if (_voiceColumnsPanel != null)
            {
                _voiceColumnsPanel.PerformLayout();
                voiceH = _voiceColumnsPanel.GetPreferredSize(new Size(innerW, 0)).Height;
            }

            var gridAreaH = MeasurePhilosophyQuotesGridAreaMinHeight();
            var tabContentH = voiceH + gridAreaH + (_voiceTabHost?.Padding.Vertical ?? 0);
            return Padding.Vertical + tabHeader + tabContentH + TabFooterSpacerHeight + _footerBarPanel.Height;
        }

        private void SyncFooterBarHeight()
        {
            if (_footerBarPanel == null || _footerBarPanel.IsDisposed || _footerStack == null || _footerStack.IsDisposed)
            {
                return;
            }

            _footerStack.PerformLayout();
            var contentW = Math.Max(320, _footerBarPanel.ClientSize.Width);
            var contentH = _footerStack.GetPreferredSize(new Size(contentW, 0)).Height;
            var target = _philosophyMode
                ? contentH + _footerBarPanel.Padding.Vertical + 2
                : Math.Max(FooterBarMinHeight, contentH + _footerBarPanel.Padding.Vertical + 4);
            if (_footerBarPanel.Height != target)
            {
                _footerBarPanel.Height = target;
            }
        }

        private void FitShowcaseDialogClientHeightToVoiceTabContent()
        {
            if (_voiceTabHost == null || _voiceTabRoot == null || _footerBarPanel == null)
            {
                return;
            }

            SyncFooterBarHeight();

            var tabs = _voiceTabHost.Parent as TabControl;
            if (tabs == null)
            {
                return;
            }

            var innerW = VoiceTabInnerWidth(_voiceTabHost);
            CompactVoiceTabRootHeight(innerW);

            var voicePageH = _voiceTabRoot.Height + _voiceTabHost.Padding.Vertical;
            var tabContentH = Math.Max(voicePageH + VoiceTabHeightFitBuffer, MinTabContentHeight);
            var tabHeader = tabs.DisplayRectangle.Top;
            if (tabHeader < 20)
            {
                tabHeader = tabs.ItemSize.Height + tabs.Padding.Y + 4;
            }

            var targetClientH = Padding.Vertical + tabHeader + tabContentH + TabFooterSpacerHeight + _footerBarPanel.Height;
            targetClientH = Math.Max(targetClientH, 820);
            targetClientH = Math.Min(targetClientH, DialogClientHeight);

            if (ClientSize.Height < targetClientH - 4)
            {
                ClientSize = new Size(ClientSize.Width, targetClientH);
                MinimumSize = new Size(DialogClientWidth, targetClientH);
                ApplyVoiceTabLayout(_voiceTabHost);
                return;
            }

            if (ClientSize.Height <= targetClientH + 24)
            {
                ApplyVoiceTabLayout(_voiceTabHost);
                return;
            }

            ClientSize = new Size(ClientSize.Width, targetClientH);
            MinimumSize = new Size(DialogClientWidth, targetClientH);
            ApplyVoiceTabLayout(_voiceTabHost);
        }

        private void FitDialogClientHeightToVoiceTabContent()
        {
            if (!_philosophyMode)
            {
                FitShowcaseDialogClientHeightToVoiceTabContent();
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
            if (_philosophyMode)
            {
                return;
            }

            _musicNames = ShowcaseMusicHelper.ListMusicFileNames(_settings);
            PopulateMusicComboItems();
            LoadFromVideo();
        }

        private void LoadFromVideo()
        {
            if (!_philosophyMode && _cbMusic != null)
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
            }

            _trkMusicVolume?.Value = Math.Max(0,
                Math.Min(100, _video.ShowcaseMusicVolume >= 0 ? _video.ShowcaseMusicVolume : 14));
            UpdateMusicVolumeLabel();

            ShowcaseNarrationSpeedHelper.EnsureSegmentSpeedDefaults(_video);
            LoadVoiceControlsFromVideo();

            if (_philosophyMode)
            {
                RefreshPreviewButtons();
                return;
            }

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
            if (!_philosophyMode)
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
            }

            _video.ShowcaseMusicVolume = MusicVolumePercent;

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
            if (!_philosophyMode
                && string.Equals(_video.ShowcaseHookTtsEngine, ShowcaseTtsHelper.EngineEdgeTts, StringComparison.OrdinalIgnoreCase)
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

            if ((!_philosophyMode
                 && string.Equals(_video.ShowcaseHookTtsEngine, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase))
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
            if (_philosophyMode)
            {
                CommitPhilosophyQuotesGridEdits();
            }
            else if (!SaveSfxFromGrid())
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
            if (!_philosophyMode || _philosophyVoiceLayoutReady)
            {
                ApplyVoiceSegmentLayoutFromTab(VoiceTabInnerWidth(_voiceTabHost));
            }
        }

        private void ApplyVoiceDimensionFieldsForEngine()
        {
            ApplySegmentColumnVisibility(_hookVoice);
            ApplySegmentColumnVisibility(_bodyVoice);
            if (!_philosophyMode || _philosophyVoiceLayoutReady)
            {
                ApplyVoiceSegmentLayoutFromTab(VoiceTabInnerWidth(_voiceTabHost));
            }
        }

        private void ApplySegmentColumnVisibility(VoiceSegmentUi seg)
        {
            if (seg == null)
            {
                return;
            }

            var eleven = SegmentIsEleven(seg);
            var edge = SegmentIsEdge(seg);
            if (_philosophyMode && !seg.IsHook)
            {
                ApplyPhilosophyBodySegmentColumnVisibility(seg, eleven, edge);
                return;
            }

            SetSegmentTableRowVisible(seg, 1, eleven || edge);
            if (seg.LblElevenPersona != null)
            {
                seg.LblElevenPersona.Visible = eleven;
            }

            if (seg.CbElevenPersona != null)
            {
                seg.CbElevenPersona.Visible = eleven;
            }

            if (seg.LblGender != null)
            {
                seg.LblGender.Visible = edge;
            }

            if (seg.CbGender != null)
            {
                seg.CbGender.Visible = edge;
            }

            SetSegmentTableRowVisible(seg, 2, false);
            // Hook: "Tone giọng" bị bỏ hẳn — "Phong cách hook" lo hết cảm xúc + rate/pitch/stability tuỳ chỉnh.
            SetSegmentTableRowVisible(seg, 3, eleven && !seg.IsHook);
            var showCustomToneRow = seg.IsHook
                ? SegmentElevenCustomVoiceStyle(seg)
                : (eleven && ShowcaseElevenToneHelper.IsCustomTone(SelectedDimensionId(seg.CbTone)));
            var showStyleRow = edge || (eleven && seg.IsHook);
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
            RefreshSegmentLanguageComboItems(seg);

            var rate = seg.IsHook ? _video.ShowcaseEdgeRateOffsetPercent : _video.ShowcaseBodyEdgeRateOffsetPercent;
            var pitch = seg.IsHook ? _video.ShowcaseEdgePitchOffsetHz : _video.ShowcaseBodyEdgePitchOffsetHz;
            ApplySegmentEdgeProsodyUi(seg, rate, pitch);

            var stability = seg.IsHook ? _video.ShowcaseElevenCustomStabilityPercent : _video.ShowcaseBodyElevenCustomStabilityPercent;
            var similarity = seg.IsHook ? _video.ShowcaseElevenCustomSimilarityPercent : _video.ShowcaseBodyElevenCustomSimilarityPercent;
            var style = seg.IsHook ? _video.ShowcaseElevenCustomStylePercent : _video.ShowcaseBodyElevenCustomStylePercent;
            ApplySegmentCustomToneUi(seg, stability, similarity, style);
            SyncSegmentShellHeight(seg);
        }

        private void ApplyPhilosophyBodySegmentColumnVisibility(VoiceSegmentUi seg, bool eleven, bool edge)
        {
            ApplyPhilosophyEngineFieldVisibility(seg, eleven, edge);

            var philosophyCustomTone = eleven
                && ShowcaseElevenToneHelper.IsCustomTone(SelectedDimensionId(seg.CbTone));
            SetSegmentRightRowVisible(seg, ResolveSegmentCustomToneRightRow(seg), philosophyCustomTone);
            RefreshSegmentStyleComboItems(seg);
            if (eleven)
            {
                RefreshSegmentLanguageComboItems(seg);
            }

            ApplySegmentEdgeProsodyUi(seg, _video.ShowcaseBodyEdgeRateOffsetPercent, _video.ShowcaseBodyEdgePitchOffsetHz);
            ApplySegmentCustomToneUi(
                seg,
                _video.ShowcaseBodyElevenCustomStabilityPercent,
                _video.ShowcaseBodyElevenCustomSimilarityPercent,
                _video.ShowcaseBodyElevenCustomStylePercent);
            SyncSegmentShellHeight(seg);
        }

        internal void RefreshVoiceSummaryAndHint()
        {
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

            if (_hookVoice != null)
            {
                SaveVoiceSegmentToVideo(_hookVoice);
            }

            if (_bodyVoice != null)
            {
                SaveVoiceSegmentToVideo(_bodyVoice);
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

        private int MusicVolumePercent =>
            _trkMusicVolume != null && !_trkMusicVolume.IsDisposed
                ? _trkMusicVolume.Value
                : Math.Max(0, Math.Min(100, _video?.ShowcaseMusicVolume >= 0 ? _video.ShowcaseMusicVolume : PhilosophyBatchHelper.DefaultMusicVolumePercent));

        private void UpdateMusicVolumeLabel()
        {
            if (_lblMusicVolumeValue == null || _lblMusicVolumeValue.IsDisposed || _trkMusicVolume == null)
            {
                return;
            }

            _lblMusicVolumeValue.Text = _trkMusicVolume.Value + "%";
        }

        private Control BuildMusicVolumeSliderHost()
        {
            _trkMusicVolume = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 14,
                TickStyle = TickStyle.BottomRight,
                TickFrequency = 5,
                SmallChange = 1,
                LargeChange = 10,
                BackColor = Color.FromArgb(45, 49, 60),
                Height = 48,
                Width = 280,
                Margin = new Padding(0, 2, 0, 0)
            };
            _trkMusicVolume.ValueChanged += (_, __) => UpdateMusicVolumeLabel();
            _trkMusicVolume.Scroll += (_, __) => UpdateMusicVolumeLabel();

            _lblMusicVolumeValue = new Label
            {
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(175, 182, 196),
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Text = "14%",
                Margin = new Padding(10, 16, 0, 0)
            };

            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.Controls.Add(_trkMusicVolume);
            row.Controls.Add(_lblMusicVolumeValue);

            _musicVolumeSliderHost = new Panel
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42),
                MinimumSize = new Size((int)(240 * MusicVolumeSliderWidthScale), 52),
                Height = 52
            };
            _musicVolumeSliderHost.Controls.Add(row);
            _musicVolumeSliderHost.Resize += (_, __) => LayoutMusicVolumeSliderRow();
            row.HandleCreated += (_, __) => LayoutMusicVolumeSliderRow();
            LayoutMusicVolumeSliderRow();
            return _musicVolumeSliderHost;
        }

        private void LayoutMusicVolumeSliderRow()
        {
            if (_musicVolumeSliderHost == null || _trkMusicVolume == null || _musicVolumeSliderHost.IsDisposed)
            {
                return;
            }

            var row = _musicVolumeSliderHost.Controls.Count > 0 ? _musicVolumeSliderHost.Controls[0] : null;
            if (row == null || row.IsDisposed)
            {
                return;
            }

            var trackW = Math.Max(120, (int)(_musicVolumeSliderHost.ClientSize.Width * MusicVolumeSliderWidthScale));
            _trkMusicVolume.Width = trackW;
            row.Location = new Point(
                0,
                Math.Max(0, (_musicVolumeSliderHost.ClientSize.Height - row.Height) / 2));
        }

        private static ComboBox CreateDropDownCombo() => new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 49, 60),
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10.5F),
            Height = 48,
            Width = 1152,
            DropDownWidth = 1200,
            Margin = new Padding(0, 8, 0, 16),
            IntegralHeight = false
        };

        private static JellyButton CreateActionButton(string name, string text, Color tint)
        {
            var btn = Form1.CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: Form1.AppJellyButtonHeight,
                minWidth: 96,
                margin: new Padding(0, 4, 12, 8),
                lockSize: true);
            Form1.ResizeAppJellyButton(btn, minWidth: 96);
            return btn;
        }

        private static void ApplyActionButtonLabel(JellyButton btn, string text, int minWidth = 96)
        {
            if (btn == null)
            {
                return;
            }

            btn.Text = (text ?? string.Empty).Trim();
            Form1.ResizeAppJellyButton(btn, minWidth: minWidth);
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

        private static Control WrapFooterCentered(Control child)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            child.Anchor = AnchorStyles.Top;
            host.Controls.Add(child);

            void Center()
            {
                if (host.IsDisposed || child.IsDisposed)
                {
                    return;
                }

                host.Height = Math.Max(host.Height, child.Height + child.Margin.Vertical);
                child.Left = Math.Max(0, (host.ClientSize.Width - child.Width) / 2);
                child.Top = 0;
            }

            host.Resize += (_, __) => Center();
            child.SizeChanged += (_, __) => Center();
            host.HandleCreated += (_, __) => Center();
            Center();
            return host;
        }

        private static Control WrapFooterRight(Control child)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 8)
            };
            child.Anchor = AnchorStyles.Top;
            host.Controls.Add(child);

            void AlignRight()
            {
                if (host.IsDisposed || child.IsDisposed)
                {
                    return;
                }

                var insetRight = host.Padding.Right;
                var neededH = child.Height + child.Margin.Vertical + host.Padding.Vertical + 4;
                child.Top = host.Padding.Top;
                child.Left = Math.Max(host.Padding.Left, host.ClientSize.Width - insetRight - child.Width);
                if (host.Parent is TableLayoutPanel && host.Dock == DockStyle.Fill)
                {
                    host.MinimumSize = new Size(0, neededH);
                }
            }

            host.Resize += (_, __) => AlignRight();
            child.SizeChanged += (_, __) => AlignRight();
            host.HandleCreated += (_, __) => AlignRight();
            AlignRight();
            return host;
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

        private static Button CreateButton(string text, Color back)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(108, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(10, 0, 0, 0),
                Padding = new Padding(16, 6, 16, 8),
                UseCompatibleTextRendering = true
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.UseVisualStyleBackColor = false;
            return btn;
        }
    }
}
