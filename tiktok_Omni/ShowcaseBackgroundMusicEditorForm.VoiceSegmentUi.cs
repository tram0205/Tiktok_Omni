using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm
    {
        private sealed class VoiceSegmentUi
        {
            public bool IsHook;
            public TableLayoutPanel Table;
            public ComboBox CbTts;
            public ComboBox CbElevenPersona;
            public ComboBox CbGender;
            public ComboBox CbLanguage;
            public ComboBox CbTone;
            public ComboBox CbStyle;
            public TrackBar TrkRate;
            public TrackBar TrkPitch;
            public TrackBar TrkStability;
            public TrackBar TrkSimilarity;
            public TrackBar TrkStyle;
            public Label LblElevenPersona;
            public Label LblGender;
            public Label LblLanguage;
            public Label LblTone;
            public Label LblCustomTone;
            public Label LblStyle;
            public Label LblEdgeProsody;
            public Label LblRateValue;
            public Label LblPitchValue;
            public Label LblStabilityValue;
            public Label LblSimilarityValue;
            public Label LblStyleValue;
            public Control ProsodyHost;
            public Control CustomToneHost;
            public Label FootnoteLabel;
            public GroupBox Shell;
            public FlowLayoutPanel NarrationButtonRow;
            public TrackBar TrkNarrationSpeed;
            public Label LblNarrationSpeedValue;
            public Control NarrationSpeedHost;
        }

        private VoiceSegmentUi _hookVoice;
        private VoiceSegmentUi _bodyVoice;
        private TableLayoutPanel _voiceColumnsPanel;

        /// <summary>Label column; field column fills remainder of half-form (~1050px at 2376 dialog).</summary>
        private const int VoiceSegmentLabelWidth = 360;
        private const int VoiceSegmentLabelInset = 20;
        private const int VoiceGroupBoxBorderFudge = 4;
        private const float VoiceSegmentSliderSideScale = 1.3f;
        private static readonly int VoiceSegmentSliderValueWidth =
            (int)Math.Round(76 * VoiceSegmentSliderSideScale);
        /// <summary>Cột nhãn slider (Rate/Pitch/Stability/Similarity/Style) — cố định để mọi thanh trượt thẳng hàng, bằng nhau.</summary>
        private const int VoiceSegmentSliderLabelWidth = 150;
        /// <summary>Inset Hook/Thân from outer panel edge (each side).</summary>
        private const int VoiceColumnOuterInset = 20;
        /// <summary>Narrow each column this much vs half width (+10pt middle gap vs trim 20).</summary>
        private const int VoiceColumnWidthTrim = 35;
        /// <summary>Value column width as fraction of space after label column (0.7 = 70%).</summary>
        private const float VoiceSegmentFieldWidthScale = 0.7f;
        private const float VoiceSegmentVerticalScale = 1.2f;
        private static int VoiceSegV(int px) => (int)Math.Round(px * VoiceSegmentVerticalScale);
        private static readonly Color VoiceNarrationGenerateTint = Color.FromArgb(56, 120, 82);
        private static readonly Color VoiceNarrationListenTint = Color.FromArgb(118, 72, 158);
        private static readonly Color VoiceScriptReviewTint = Color.FromArgb(48, 112, 168);
        /// <summary>Độ tuổi không còn ô chọn riêng (persona/preset đã quyết định) — dùng mặc định cố định.</summary>
        private const string DefaultSegmentAgeId = ShowcaseVoicePresetDimensions.Age.Adult26_35;

        private Control BuildHookBodyVoiceColumnsPanel()
        {
            _hookVoice = BuildVoiceSegmentUi(isHook: true, "Hook (mở đầu)");
            _bodyVoice = BuildVoiceSegmentUi(isHook: false, "Thân (từng cảnh)");

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = BackColor,
                Margin = Padding.Empty
            };
            _voiceColumnsPanel = outer;
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            outer.Controls.Add(WrapVoiceSegmentGroup(_hookVoice), 0, 0);
            outer.Controls.Add(WrapVoiceSegmentGroup(_bodyVoice), 1, 0);
            AttachVoiceNarrationButtons();
            return outer;
        }

        private void ApplyVoiceSegmentColumnWidths(int totalInnerWidth)
        {
            totalInnerWidth = Math.Max(900, totalInnerWidth);
            if (_voiceColumnsPanel == null)
            {
                return;
            }

            _voiceColumnsPanel.Width = totalInnerWidth;
            _voiceColumnsPanel.MinimumSize = new Size(totalInnerWidth, 0);
            _voiceColumnsPanel.Padding = new Padding(VoiceColumnOuterInset, 0, VoiceColumnOuterInset, 0);

            var half = totalInnerWidth / 2;
            var colW = Math.Max(360, half - VoiceColumnWidthTrim);
            ApplySegmentColumnShellWidth(_hookVoice, colW);
            ApplySegmentColumnShellWidth(_bodyVoice, colW);
            LayoutVoiceNarrationButtons(_hookVoice);
            LayoutVoiceNarrationButtons(_bodyVoice);
            _voiceColumnsPanel.PerformLayout();
        }

        private void ApplySegmentColumnShellWidth(VoiceSegmentUi seg, int shellWidth)
        {
            if (seg?.Shell == null || seg.Table == null)
            {
                return;
            }

            seg.Shell.Width = shellWidth;
            seg.Shell.MinimumSize = new Size(shellWidth, 0);
            LayoutSegmentFromShellClient(seg);
            SyncSegmentShellHeight(seg);
        }

        private void SyncSegmentShellHeight(VoiceSegmentUi seg)
        {
            if (seg?.Shell == null || seg.Table == null)
            {
                return;
            }

            seg.ProsodyHost?.PerformLayout();
            seg.CustomToneHost?.PerformLayout();
            seg.Table.PerformLayout();
            var layoutW = Math.Max(320, seg.Shell.ClientSize.Width);
            if (layoutW < 100)
            {
                layoutW = Math.Max(320, seg.Shell.Width - seg.Shell.Padding.Horizontal);
            }

            var tableH = seg.Table.GetPreferredSize(new Size(layoutW, 0)).Height;
            seg.Shell.Height = tableH + seg.Shell.Padding.Vertical + VoiceSegV(8);
            _voiceColumnsPanel?.PerformLayout();
        }

        private void LayoutSegmentFromShellClient(VoiceSegmentUi seg)
        {
            if (seg?.Shell == null || seg.Table == null)
            {
                return;
            }

            var clientW = Math.Max(320, seg.Shell.ClientSize.Width - VoiceGroupBoxBorderFudge);
            var rawField = Math.Max(0, clientW - VoiceSegmentLabelWidth);
            var comboFieldW = Math.Max(200, (int)Math.Round(rawField * VoiceSegmentFieldWidthScale));
            var prosodyFieldW = Math.Max(200, rawField);
            ApplySegmentTableWidth(seg, clientW, comboFieldW, prosodyFieldW);
            LayoutVoiceNarrationButtons(seg);
        }

        private void ApplySegmentTableWidth(VoiceSegmentUi seg, int tableW, int comboFieldW, int prosodyFieldW)
        {
            if (seg?.Table == null)
            {
                return;
            }

            seg.Table.Width = tableW;
            seg.Table.MaximumSize = new Size(tableW, 8192);
            seg.Table.ColumnStyles[0].Width = VoiceSegmentLabelWidth;
            LayoutSegmentFieldCombos(seg, comboFieldW);
            if (seg.ProsodyHost != null)
            {
                seg.ProsodyHost.AutoSize = true;
                if (seg.ProsodyHost is FlowLayoutPanel prosodyFlow)
                {
                    prosodyFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                }

                seg.ProsodyHost.Dock = DockStyle.Top;
                seg.ProsodyHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                seg.ProsodyHost.Width = prosodyFieldW;
                LayoutProsodyHostContents(seg, prosodyFieldW);
                seg.ProsodyHost.PerformLayout();
            }

            if (seg.CustomToneHost != null)
            {
                seg.CustomToneHost.AutoSize = true;
                if (seg.CustomToneHost is FlowLayoutPanel toneFlow)
                {
                    toneFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                }

                seg.CustomToneHost.Dock = DockStyle.Top;
                seg.CustomToneHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                seg.CustomToneHost.Width = prosodyFieldW;
                LayoutSliderHostContents(seg.CustomToneHost, prosodyFieldW);
                seg.CustomToneHost.PerformLayout();
            }

            if (seg.NarrationSpeedHost != null)
            {
                seg.NarrationSpeedHost.AutoSize = true;
                seg.NarrationSpeedHost.Dock = DockStyle.Top;
                seg.NarrationSpeedHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                seg.NarrationSpeedHost.Width = prosodyFieldW;
                LayoutNarrationSpeedHostContents(seg, prosodyFieldW);
                seg.NarrationSpeedHost.PerformLayout();
            }

            if (seg.FootnoteLabel != null)
            {
                seg.FootnoteLabel.MaximumSize = new Size(tableW, 0);
            }

            seg.Shell?.Invalidate(true);
            SyncSegmentShellHeight(seg);
        }

        private void LayoutProsodyHostContents(VoiceSegmentUi seg, int rowWidth) =>
            LayoutSliderHostContents(seg?.ProsodyHost, rowWidth);

        private void LayoutNarrationSpeedHostContents(VoiceSegmentUi seg, int rowWidth)
        {
            if (seg?.NarrationSpeedHost == null)
            {
                return;
            }

            rowWidth = Math.Max(280, rowWidth);
            foreach (Control c in seg.NarrationSpeedHost.Controls)
            {
                if (c is TableLayoutPanel sliderRow && sliderRow.Tag as string == "narrationSpeedSlider")
                {
                    sliderRow.Width = rowWidth;
                    sliderRow.MinimumSize = new Size(rowWidth, 0);
                }
            }
        }

        /// <summary>Cột nhãn/giá trị (Absolute) không đổi — chỉ cột thanh trượt (Percent, phần còn lại) co theo tỉ lệ này.</summary>
        private const float VoiceSegmentSliderTrackWidthScale = 0.9f;

        private void LayoutSliderHostContents(Control host, int rowWidth)
        {
            if (host == null)
            {
                return;
            }

            rowWidth = Math.Max(280, rowWidth);
            var sliderRowWidth = Math.Max(280, (int)Math.Round(rowWidth * VoiceSegmentSliderTrackWidthScale));
            foreach (Control c in host.Controls)
            {
                if (c is Label lbl && lbl.AutoSize)
                {
                    lbl.MaximumSize = new Size(rowWidth, 0);
                }
                else if (c is TableLayoutPanel sliderRow && sliderRow.Tag as string == "edgeProsodySlider")
                {
                    sliderRow.Width = sliderRowWidth;
                    sliderRow.MinimumSize = new Size(sliderRowWidth, 0);
                }
            }
        }

        private static void LayoutSegmentFieldCombos(VoiceSegmentUi seg, int comboFieldW)
        {
            foreach (var cb in new[]
                     {
                         seg.CbTts, seg.CbElevenPersona, seg.CbGender, seg.CbLanguage, seg.CbTone, seg.CbStyle
                     })
            {
                if (cb == null)
                {
                    continue;
                }

                cb.Width = comboFieldW;
                cb.DropDownWidth = Math.Max(comboFieldW + 48, 720);
            }
        }

        private Control WrapVoiceSegmentGroup(VoiceSegmentUi seg)
        {
            var box = new GroupBox
            {
                Text = seg.IsHook ? "Hook" : "Thân",
                Dock = DockStyle.None,
                Anchor = seg.IsHook
                    ? AnchorStyles.Top | AnchorStyles.Left
                    : AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = false,
                ForeColor = Color.FromArgb(190, 198, 212),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Padding = new Padding(14, VoiceSegV(22), 14, VoiceSegV(6)),
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(31, 34, 42)
            };
            seg.Shell = box;
            seg.Table.Dock = DockStyle.Top;
            seg.Table.AutoSize = true;
            seg.Table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            seg.Table.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.Controls.Add(seg.Table);
            box.Resize += (_, __) =>
            {
                if (_voiceUiLock)
                {
                    return;
                }

                LayoutSegmentFromShellClient(seg);
            };
            return box;
        }

        private VoiceSegmentUi BuildVoiceSegmentUi(bool isHook, string headerHint)
        {
            Label MkLbl(string text, ContentAlignment align = ContentAlignment.MiddleLeft) => new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                MaximumSize = new Size(VoiceSegmentLabelWidth - VoiceSegmentLabelInset - 4, 0),
                TextAlign = align,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(VoiceSegmentLabelInset, VoiceSegV(18), 10, VoiceSegV(10))
            };

            var seg = new VoiceSegmentUi { IsHook = isHook };
            seg.CbTts = CreateVoiceSegmentCombo();
            FillSegmentEngineCombo(seg.CbTts);
            seg.CbElevenPersona = CreateVoiceSegmentCombo();
            FillDimensionCombo(seg.CbElevenPersona, ElevenVoicePersonaCatalog.ListOptions());
            seg.CbGender = CreateVoiceSegmentCombo();
            FillDimensionCombo(seg.CbGender, ShowcaseVoicePresetDimensions.ListGenderOptions());
            seg.CbLanguage = CreateVoiceSegmentCombo();
            seg.CbTone = CreateVoiceSegmentCombo();
            seg.CbTone.MaxDropDownItems = 12;
            seg.CbStyle = CreateVoiceSegmentCombo();
            seg.CbStyle.MaxDropDownItems = 12;

            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                var icon = HookStyleCatalog.GetStyleIcon(key);
                seg.CbStyle.Items.Add(new HookStyleListItem(
                    key,
                    icon + " " + HookStyleCatalog.GetDisplayName(key)));
            }

            seg.CbStyle.Items.Add(new HookStyleListItem(
                ShowcaseEdgeProsodyHelper.StyleCustom,
                "⚙ Tùy chỉnh (Rate / Pitch)"));

            FillDimensionCombo(seg.CbLanguage, ShowcaseVoicePresetDimensions.ListLanguageOptions());
            FillDimensionCombo(seg.CbTone, ShowcaseVoicePresetDimensions.ListToneOptions());

            seg.TrkRate = CreateEdgeProsodyTrackBar(
                ShowcaseEdgeProsodyHelper.MinRateOffsetPercent,
                ShowcaseEdgeProsodyHelper.MaxRateOffsetPercent,
                5,
                400);
            seg.TrkPitch = CreateEdgeProsodyTrackBar(
                ShowcaseEdgeProsodyHelper.MinPitchOffsetHz,
                ShowcaseEdgeProsodyHelper.MaxPitchOffsetHz,
                2,
                400);
            seg.LblRateValue = CreateEdgeProsodyValueLabel();
            seg.LblPitchValue = CreateEdgeProsodyValueLabel();

            seg.TrkStability = CreateEdgeProsodyTrackBar(
                ShowcaseElevenToneHelper.MinPercent,
                ShowcaseElevenToneHelper.MaxPercent,
                10,
                400);
            seg.TrkSimilarity = CreateEdgeProsodyTrackBar(
                ShowcaseElevenToneHelper.MinPercent,
                ShowcaseElevenToneHelper.MaxPercent,
                10,
                400);
            seg.TrkStyle = CreateEdgeProsodyTrackBar(
                ShowcaseElevenToneHelper.MinPercent,
                ShowcaseElevenToneHelper.MaxPercent,
                10,
                400);
            seg.LblStabilityValue = CreateEdgeProsodyValueLabel();
            seg.LblSimilarityValue = CreateEdgeProsodyValueLabel();
            seg.LblStyleValue = CreateEdgeProsodyValueLabel();

            void OnSegChanged(object s, EventArgs e)
            {
                if (ReferenceEquals(s, seg.CbStyle))
                {
                    OnSegmentStyleChanged(seg);
                }
                else if (ReferenceEquals(s, seg.CbTts))
                {
                    ApplyVoiceDimensionFieldsForEngine();
                    OnVoiceDimensionChanged();
                }
                else if (ReferenceEquals(s, seg.CbTone))
                {
                    OnSegmentToneChanged(seg);
                }
                else if (ReferenceEquals(s, seg.TrkRate) || ReferenceEquals(s, seg.TrkPitch))
                {
                    OnSegmentProsodySliderChanged(seg);
                }
                else if (ReferenceEquals(s, seg.TrkStability) || ReferenceEquals(s, seg.TrkSimilarity) || ReferenceEquals(s, seg.TrkStyle))
                {
                    OnSegmentCustomToneSliderChanged(seg);
                }
                else if (ReferenceEquals(s, seg.TrkNarrationSpeed))
                {
                    OnSegmentNarrationSpeedChanged(seg);
                }
                else
                {
                    OnVoiceDimensionChanged();
                }
            }

            seg.CbTts.SelectedIndexChanged += OnSegChanged;
            seg.CbElevenPersona.SelectedIndexChanged += OnSegChanged;
            seg.CbGender.SelectedIndexChanged += OnSegChanged;
            seg.CbLanguage.SelectedIndexChanged += OnSegChanged;
            seg.CbTone.SelectedIndexChanged += OnSegChanged;
            seg.CbStyle.SelectedIndexChanged += OnSegChanged;
            seg.TrkRate.ValueChanged += OnSegChanged;
            seg.TrkPitch.ValueChanged += OnSegChanged;
            seg.TrkRate.Scroll += OnSegChanged;
            seg.TrkPitch.Scroll += OnSegChanged;
            seg.TrkStability.ValueChanged += OnSegChanged;
            seg.TrkSimilarity.ValueChanged += OnSegChanged;
            seg.TrkStyle.ValueChanged += OnSegChanged;
            seg.TrkStability.Scroll += OnSegChanged;
            seg.TrkSimilarity.Scroll += OnSegChanged;
            seg.TrkStyle.Scroll += OnSegChanged;

            var tbl = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 10,
                BackColor = BackColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            seg.Table = tbl;
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, VoiceSegmentLabelWidth));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 10; i++)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            tbl.Controls.Add(MkLbl("Nguồn TTS"), 0, 0);
            tbl.Controls.Add(seg.CbTts, 1, 0);
            seg.LblElevenPersona = MkLbl("Giọng ElevenLabs");
            tbl.Controls.Add(seg.LblElevenPersona, 0, 1);
            tbl.Controls.Add(seg.CbElevenPersona, 1, 1);
            seg.LblGender = MkLbl("Giới tính");
            tbl.Controls.Add(seg.LblGender, 0, 1);
            tbl.Controls.Add(seg.CbGender, 1, 1);
            seg.LblLanguage = MkLbl("Ngôn ngữ");
            tbl.Controls.Add(seg.LblLanguage, 0, 2);
            tbl.Controls.Add(seg.CbLanguage, 1, 2);
            seg.LblTone = MkLbl("Tone giọng");
            tbl.Controls.Add(seg.LblTone, 0, 3);
            tbl.Controls.Add(seg.CbTone, 1, 3);

            var styleRow = isHook ? 4 : 5;
            var customToneRow = isHook ? 5 : 4;

            seg.LblCustomTone = MkLbl("Tinh chỉnh giọng", ContentAlignment.TopLeft);
            seg.LblCustomTone.Margin = new Padding(VoiceSegmentLabelInset, VoiceSegV(8), 10, VoiceSegV(2));
            var customToneHost = BuildSegmentCustomTonePanel(seg);
            seg.CustomToneHost = customToneHost;
            tbl.Controls.Add(seg.LblCustomTone, 0, customToneRow);
            tbl.Controls.Add(customToneHost, 1, customToneRow);

            seg.LblStyle = MkLbl("Phong cách Edge");
            tbl.Controls.Add(seg.LblStyle, 0, styleRow);
            tbl.Controls.Add(seg.CbStyle, 1, styleRow);

            seg.LblEdgeProsody = MkLbl("Rate / Pitch", ContentAlignment.TopLeft);
            var prosodyHost = BuildSegmentEdgeProsodyPanel(seg);
            seg.ProsodyHost = prosodyHost;
            tbl.Controls.Add(seg.LblEdgeProsody, 0, 6);
            tbl.Controls.Add(prosodyHost, 1, 6);

            seg.TrkNarrationSpeed = CreateNarrationSpeedTrackBar();
            seg.LblNarrationSpeedValue = CreateNarrationSpeedValueLabel();
            seg.TrkNarrationSpeed.ValueChanged += OnSegChanged;
            seg.TrkNarrationSpeed.Scroll += OnSegChanged;
            var speedHost = BuildSegmentNarrationSpeedHost(seg);
            seg.NarrationSpeedHost = speedHost;
            tbl.Controls.Add(MkLbl("Tốc độ thoại"), 0, 7);
            tbl.Controls.Add(speedHost, 1, 7);

            seg.FootnoteLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 128, 142),
                Font = new Font("Segoe UI", 9F),
                Text = headerHint,
                Margin = new Padding(VoiceSegmentLabelInset, VoiceSegV(2), 0, 0)
            };
            tbl.Controls.Add(seg.FootnoteLabel, 0, 8);
            tbl.SetColumnSpan(seg.FootnoteLabel, 2);

            seg.NarrationButtonRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, VoiceSegV(6), 0, VoiceSegV(4)),
                Padding = Padding.Empty
            };
            tbl.Controls.Add(seg.NarrationButtonRow, 1, 9);

            if (isHook)
            {
                _cbHookTtsEngine = seg.CbTts;
            }
            else
            {
                _cbBodyTtsEngine = seg.CbTts;
            }

            return seg;
        }

        private Control BuildSegmentEdgeProsodyPanel(VoiceSegmentUi seg)
        {
            var host = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor,
                Margin = new Padding(0, VoiceSegV(2), 0, 0)
            };

            host.Resize += (_, __) =>
            {
                if (_voiceUiLock || seg.ProsodyHost == null)
                {
                    return;
                }

                var w = Math.Max(280, host.ClientSize.Width);
                if (w > 0)
                {
                    LayoutProsodyHostContents(seg, w);
                }

                SyncSegmentShellHeight(seg);
            };

            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Rate",
                seg.TrkRate,
                seg.LblRateValue,
                "-25%",
                "+25%",
                400));
            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Pitch",
                seg.TrkPitch,
                seg.LblPitchValue,
                "-12Hz",
                "+12Hz",
                400));
            return host;
        }

        /// <summary>Panel «Tùy chỉnh» ElevenLabs — 3 slider Stability/Similarity/Style, chỉ bật khi Tone = Tùy chỉnh.</summary>
        private Control BuildSegmentCustomTonePanel(VoiceSegmentUi seg)
        {
            var host = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor,
                Margin = new Padding(0, VoiceSegV(2), 0, 0)
            };

            host.Resize += (_, __) =>
            {
                if (_voiceUiLock || seg.CustomToneHost == null)
                {
                    return;
                }

                var w = Math.Max(280, host.ClientSize.Width);
                if (w > 0)
                {
                    LayoutSliderHostContents(seg.CustomToneHost, w);
                }

                SyncSegmentShellHeight(seg);
            };

            host.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 148, 162),
                Font = new Font("Segoe UI", 9.25F),
                Text = seg.IsHook
                    ? "Chỉ áp dụng khi Phong cách hook = «⚙ Tùy chỉnh giọng» — kéo 3 số ElevenLabs."
                    : "Chỉ áp dụng khi Tone giọng = «Tùy chỉnh» — kéo 3 số ElevenLabs.",
                Margin = new Padding(0, 0, 0, VoiceSegV(4))
            });

            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Stability",
                seg.TrkStability,
                seg.LblStabilityValue,
                "0%",
                "100%",
                400));
            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Similarity",
                seg.TrkSimilarity,
                seg.LblSimilarityValue,
                "0%",
                "100%",
                400));
            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Style",
                seg.TrkStyle,
                seg.LblStyleValue,
                "0%",
                "100%",
                400));
            return host;
        }

        private const int NarrationSpeedValueColumnWidth = 72;
        private static readonly int NarrationSpeedTrackRowHeight = VoiceSegV(28);
        private static readonly int NarrationSpeedHintsRowHeight = VoiceSegV(26);

        private static TrackBar CreateNarrationSpeedTrackBar() =>
            new TrackBar
            {
                Minimum = ShowcaseNarrationSpeedHelper.MinManualSpeedPercent,
                Maximum = ShowcaseNarrationSpeedHelper.MaxManualSpeedPercent,
                Value = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent,
                TickStyle = TickStyle.None,
                SmallChange = 5,
                LargeChange = 10,
                BackColor = Color.FromArgb(45, 49, 60),
                Height = VoiceSegV(22),
                Margin = new Padding(0, VoiceSegV(1), 4, 0)
            };

        private static Label CreateNarrationSpeedValueLabel() => new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(175, 182, 196),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Text = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent + "%",
            Margin = new Padding(0, 0, 2, 0),
            AutoEllipsis = false
        };

        private static Control BuildSegmentNarrationSpeedHost(VoiceSegmentUi seg)
        {
            var trackBar = seg.TrkNarrationSpeed;
            var valueLabel = seg.LblNarrationSpeedValue;
            var root = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = new Padding(0, VoiceSegV(2), 0, VoiceSegV(4)),
                Tag = "narrationSpeedSlider"
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NarrationSpeedValueColumnWidth));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, NarrationSpeedTrackRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, NarrationSpeedHintsRowHeight + 2));

            trackBar.Dock = DockStyle.Fill;
            root.Controls.Add(trackBar, 0, 0);
            valueLabel.Dock = DockStyle.Fill;
            root.Controls.Add(valueLabel, 1, 0);

            var hints = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = Padding.Empty,
                MinimumSize = new Size(0, NarrationSpeedHintsRowHeight),
                Padding = new Padding(0, 0, 0, 2)
            };
            hints.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            hints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            hints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            hints.Controls.Add(new Label
            {
                Text = ShowcaseNarrationSpeedHelper.MinManualSpeedPercent + "%",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(120, 128, 142),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 2)
            }, 0, 0);
            hints.Controls.Add(new Label
            {
                Text = ShowcaseNarrationSpeedHelper.MaxManualSpeedPercent + "%",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(120, 128, 142),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 0, 2)
            }, 1, 0);
            root.Controls.Add(hints, 0, 1);

            return root;
        }

        private ComboBox CreateVoiceSegmentCombo()
        {
            var combo = CreateDropDownCombo();
            combo.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            combo.Width = 400;
            combo.DropDownWidth = 900;
            combo.Height = VoiceSegV(48);
            return combo;
        }

        private static TrackBar CreateEdgeProsodyTrackBar(int min, int max, int tickFrequency, int width) =>
            new TrackBar
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = tickFrequency,
                SmallChange = 1,
                LargeChange = Math.Max(1, tickFrequency),
                BackColor = Color.FromArgb(45, 49, 60),
                Height = VoiceSegV(35),
                Width = width
            };

        private static Control BuildEdgeProsodySliderRow(
            string title,
            TrackBar trackBar,
            Label valueLabel,
            string minHint,
            string maxHint,
            int rowWidth)
        {
            var root = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = new Padding(0, 0, 0, VoiceSegV(6)),
                Width = rowWidth,
                Tag = "edgeProsodySlider"
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, VoiceSegmentSliderLabelWidth));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, VoiceSegmentSliderValueWidth));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, VoiceSegV(32)));

            root.Controls.Add(new Label
            {
                Text = title,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(0, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            }, 0, 0);

            trackBar.Dock = DockStyle.Top;
            trackBar.Margin = new Padding(0, VoiceSegV(2), 4, 0);
            root.Controls.Add(trackBar, 1, 0);

            valueLabel.AutoSize = true;
            valueLabel.Dock = DockStyle.None;
            valueLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            valueLabel.Margin = new Padding(0, VoiceSegV(6), 0, VoiceSegV(2));
            valueLabel.TextAlign = ContentAlignment.TopRight;
            root.Controls.Add(valueLabel, 2, 0);

            // Không AutoSize — phải Dock=Top để CHIẾM ĐÚNG chiều rộng cột thanh trượt (khớp trackBar phía trên),
            // AutoSize=true sẽ khiến TableLayoutPanel co theo nội dung, lệch khỏi 2 đầu thanh trượt thật.
            var hints = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 1,
                MinimumSize = new Size(0, VoiceSegV(32)),
                BackColor = Color.FromArgb(31, 34, 42),
                Margin = new Padding(0, 0, 4, VoiceSegV(2))
            };
            hints.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            hints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            hints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // AutoSize=true khiến khung = đúng kích thước chữ → TextAlign vô nghĩa (không có khoảng trống để dạt lề).
            // Phải AutoSize=false + Dock=Fill để label chiếm hết nửa cột rồi TextAlign mới thực sự dạt trái/phải.
            hints.Controls.Add(new Label
            {
                Text = minHint,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(120, 128, 142),
                Font = new Font("Segoe UI", 8.5F),
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            hints.Controls.Add(new Label
            {
                Text = maxHint,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(120, 128, 142),
                Font = new Font("Segoe UI", 8.5F),
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleRight
            }, 1, 0);

            root.Controls.Add(hints, 1, 1);
            return root;
        }

        private static bool SegmentIsEdge(VoiceSegmentUi seg) =>
            string.Equals(
                (seg.CbTts.SelectedItem as TtsEngineListItem)?.Id ?? ShowcaseTtsHelper.EngineEdgeTts,
                ShowcaseTtsHelper.EngineEdgeTts,
                StringComparison.OrdinalIgnoreCase);

        private static bool SegmentIsEleven(VoiceSegmentUi seg) =>
            string.Equals(
                (seg.CbTts.SelectedItem as TtsEngineListItem)?.Id,
                ShowcaseTtsHelper.EngineElevenLabs,
                StringComparison.OrdinalIgnoreCase);

        private static string SelectedSegmentStyleKey(VoiceSegmentUi seg) =>
            (seg.CbStyle.SelectedItem as HookStyleListItem)?.Key ?? HookStyleCatalog.StyleHuongdan;

        private static bool SegmentCustomStyle(VoiceSegmentUi seg) =>
            string.Equals(
                SelectedSegmentStyleKey(seg),
                ShowcaseEdgeProsodyHelper.StyleCustom,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Hook + ElevenLabs + «Phong cách hook» = «⚙ Tùy chỉnh giọng» — bật 3 slider stability/similarity/style cho Hook.</summary>
        private static bool SegmentElevenCustomVoiceStyle(VoiceSegmentUi seg) =>
            seg.IsHook && SegmentIsEleven(seg) && ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(SelectedSegmentStyleKey(seg));

        private void SetSegmentTableRowVisible(VoiceSegmentUi seg, int row, bool visible)
        {
            if (seg?.Table == null)
            {
                return;
            }

            seg.Table.RowStyles[row].SizeType = visible ? SizeType.AutoSize : SizeType.Absolute;
            seg.Table.RowStyles[row].Height = 0f;
            foreach (Control c in seg.Table.Controls)
            {
                if (seg.Table.GetRow(c) != row)
                {
                    continue;
                }

                c.Visible = visible;
                if (!visible)
                {
                    c.MinimumSize = Size.Empty;
                }
            }
        }

        private void ApplySegmentEdgeProsodyUi(VoiceSegmentUi seg, int customRate, int customPitch)
        {
            if (seg?.TrkRate == null)
            {
                return;
            }

            var edge = SegmentIsEdge(seg);
            var custom = edge && SegmentCustomStyle(seg);
            seg.TrkRate.Enabled = custom;
            seg.TrkPitch.Enabled = custom;

            _voiceUiLock = true;
            try
            {
                if (custom)
                {
                    seg.TrkRate.Value = ShowcaseEdgeProsodyHelper.ClampRateOffset(customRate);
                    seg.TrkPitch.Value = ShowcaseEdgeProsodyHelper.ClampPitchOffset(customPitch);
                }
                else if (edge)
                {
                    var key = SelectedSegmentStyleKey(seg);
                    if (ShowcaseEdgeProsodyHelper.TryGetPresetOffsets(key, out var rate, out var pitch))
                    {
                        seg.TrkRate.Value = rate;
                        seg.TrkPitch.Value = pitch;
                    }
                }
            }
            finally
            {
                _voiceUiLock = false;
            }

            UpdateSegmentProsodyLabels(seg, presetLocked: !custom);
        }

        private static void UpdateSegmentProsodyLabels(VoiceSegmentUi seg, bool presetLocked)
        {
            if (seg.LblRateValue != null && seg.TrkRate != null)
            {
                var r = seg.TrkRate.Value;
                seg.LblRateValue.Text = r == 0 ? "±0%" : (r > 0 ? "+" : string.Empty) + r + "%";
                seg.LblRateValue.ForeColor = presetLocked
                    ? Color.FromArgb(140, 148, 162)
                    : Color.FromArgb(190, 198, 212);
            }

            if (seg.LblPitchValue != null && seg.TrkPitch != null)
            {
                var p = seg.TrkPitch.Value;
                seg.LblPitchValue.Text = p == 0 ? "±0Hz" : (p > 0 ? "+" : string.Empty) + p + "Hz";
                seg.LblPitchValue.ForeColor = presetLocked
                    ? Color.FromArgb(140, 148, 162)
                    : Color.FromArgb(190, 198, 212);
            }
        }

        private void OnSegmentStyleChanged(VoiceSegmentUi seg)
        {
            if (_voiceUiLock)
            {
                return;
            }

            ApplySegmentColumnVisibility(seg);
            OnVoiceDimensionChanged();
        }

        private void OnSegmentProsodySliderChanged(VoiceSegmentUi seg)
        {
            if (_voiceUiLock || !SegmentCustomStyle(seg))
            {
                return;
            }

            UpdateSegmentProsodyLabels(seg, presetLocked: false);
            OnVoiceDimensionChanged();
        }

        private void ApplySegmentCustomToneUi(VoiceSegmentUi seg, int stabilityPercent, int similarityPercent, int stylePercent)
        {
            if (seg?.TrkStability == null)
            {
                return;
            }

            var custom = seg.IsHook
                ? SegmentElevenCustomVoiceStyle(seg)
                : (SegmentIsEleven(seg) && ShowcaseElevenToneHelper.IsCustomTone(SelectedDimensionId(seg.CbTone)));
            seg.TrkStability.Enabled = custom;
            seg.TrkSimilarity.Enabled = custom;
            seg.TrkStyle.Enabled = custom;

            _voiceUiLock = true;
            try
            {
                seg.TrkStability.Value = ShowcaseElevenToneHelper.ClampPercent(stabilityPercent);
                seg.TrkSimilarity.Value = ShowcaseElevenToneHelper.ClampPercent(similarityPercent);
                seg.TrkStyle.Value = ShowcaseElevenToneHelper.ClampPercent(stylePercent);
            }
            finally
            {
                _voiceUiLock = false;
            }

            UpdateSegmentCustomToneLabels(seg, presetLocked: !custom);
        }

        private static void UpdateSegmentCustomToneLabels(VoiceSegmentUi seg, bool presetLocked)
        {
            var color = presetLocked ? Color.FromArgb(140, 148, 162) : Color.FromArgb(190, 198, 212);
            if (seg.LblStabilityValue != null && seg.TrkStability != null)
            {
                seg.LblStabilityValue.Text = seg.TrkStability.Value + "%";
                seg.LblStabilityValue.ForeColor = color;
            }

            if (seg.LblSimilarityValue != null && seg.TrkSimilarity != null)
            {
                seg.LblSimilarityValue.Text = seg.TrkSimilarity.Value + "%";
                seg.LblSimilarityValue.ForeColor = color;
            }

            if (seg.LblStyleValue != null && seg.TrkStyle != null)
            {
                seg.LblStyleValue.Text = seg.TrkStyle.Value + "%";
                seg.LblStyleValue.ForeColor = color;
            }
        }

        private void OnSegmentToneChanged(VoiceSegmentUi seg)
        {
            if (_voiceUiLock)
            {
                return;
            }

            ApplySegmentColumnVisibility(seg);
            OnVoiceDimensionChanged();
        }

        private void OnSegmentNarrationSpeedChanged(VoiceSegmentUi seg)
        {
            if (_voiceUiLock)
            {
                return;
            }

            UpdateSegmentNarrationSpeedLabel(seg);
            OnVoiceDimensionChanged();
        }

        private static void UpdateSegmentNarrationSpeedLabel(VoiceSegmentUi seg)
        {
            if (seg?.LblNarrationSpeedValue == null || seg.TrkNarrationSpeed == null)
            {
                return;
            }

            seg.LblNarrationSpeedValue.Text = seg.TrkNarrationSpeed.Value + "%";
        }

        private void OnSegmentCustomToneSliderChanged(VoiceSegmentUi seg)
        {
            var custom = seg.IsHook
                ? SegmentElevenCustomVoiceStyle(seg)
                : ShowcaseElevenToneHelper.IsCustomTone(SelectedDimensionId(seg.CbTone));
            if (_voiceUiLock || !custom)
            {
                return;
            }

            UpdateSegmentCustomToneLabels(seg, presetLocked: false);
            OnVoiceDimensionChanged();
        }

        private ShowcaseVoicePresetDimensions.VoiceDimensionSet SegmentDimensionSet(VoiceSegmentUi seg) =>
            ShowcaseVoicePresetDimensions.BuildFromVoiceUi(
                DefaultSegmentAgeId,
                SegmentIsEleven(seg)
                    ? ShowcaseVoicePresetDimensions.Language.ViSouth
                    : SelectedDimensionId(seg.CbLanguage),
                seg.IsHook ? ShowcaseVoicePresetDimensions.Tone.Natural : SelectedDimensionId(seg.CbTone),
                SegmentIsEdge(seg) ? SelectedDimensionId(seg.CbGender) : null);

        private string ResolvePresetIdForSegment(VoiceSegmentUi seg) =>
            ShowcaseVoicePresetDimensions.ResolvePresetId(SegmentDimensionSet(seg));

        private void SaveVoiceSegmentToVideo(VoiceSegmentUi seg)
        {
            var presetId = ResolvePresetIdForSegment(seg);
            if (seg.IsHook)
            {
                _video.ShowcaseVoicePresetId = presetId;
                if (SegmentIsEleven(seg))
                {
                    _video.ShowcaseVoiceAgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(DefaultSegmentAgeId);
                    _video.ShowcaseVoiceLanguageId = ShowcaseVoicePresetDimensions.Language.ViSouth;
                    _video.ShowcaseHookElevenPersona =
                        ElevenVoicePersonaCatalog.Normalize(SelectedDimensionId(seg.CbElevenPersona));
                }
                else
                {
                    var edgeDims = ShowcaseVoicePresetDimensions.GetForPreset(presetId);
                    _video.ShowcaseVoiceAgeId = edgeDims.AgeId;
                    _video.ShowcaseVoiceGenderId =
                        ShowcaseVoicePresetDimensions.NormalizeGenderId(SelectedDimensionId(seg.CbGender));
                    _video.ShowcaseVoiceLanguageId = edgeDims.LanguageId;
                }

                _video.ShowcaseVoiceToneId = ShowcaseVoicePresetDimensions.Tone.Natural;

                if (seg.CbStyle.SelectedItem is HookStyleListItem hookStyleItem)
                {
                    _video.ShowcaseHookStyleKey = hookStyleItem.Key;
                }

                if (SegmentCustomStyle(seg))
                {
                    _video.ShowcaseEdgeRateOffsetPercent = ShowcaseEdgeProsodyHelper.ClampRateOffset(seg.TrkRate.Value);
                    _video.ShowcaseEdgePitchOffsetHz = ShowcaseEdgeProsodyHelper.ClampPitchOffset(seg.TrkPitch.Value);
                }

                if (SegmentElevenCustomVoiceStyle(seg))
                {
                    _video.ShowcaseElevenCustomStabilityPercent = ShowcaseElevenToneHelper.ClampPercent(seg.TrkStability.Value);
                    _video.ShowcaseElevenCustomSimilarityPercent = ShowcaseElevenToneHelper.ClampPercent(seg.TrkSimilarity.Value);
                    _video.ShowcaseElevenCustomStylePercent = ShowcaseElevenToneHelper.ClampPercent(seg.TrkStyle.Value);
                }

                _video.ShowcaseHookTtsEngine = SelectedSegmentEngineId(seg.CbTts);
                SaveSegmentNarrationSpeed(seg);
                return;
            }

            _video.ShowcaseBodyVoicePresetId = presetId;
            if (SegmentIsEleven(seg))
            {
                _video.ShowcaseBodyVoiceAgeId = ShowcaseVoicePresetDimensions.NormalizeAgeId(DefaultSegmentAgeId);
                _video.ShowcaseBodyVoiceLanguageId = ShowcaseVoicePresetDimensions.Language.ViSouth;
                _video.ShowcaseBodyElevenPersona =
                    ElevenVoicePersonaCatalog.Normalize(SelectedDimensionId(seg.CbElevenPersona));
            }
            else
            {
                var edgeDims = ShowcaseVoicePresetDimensions.GetForPreset(presetId);
                _video.ShowcaseBodyVoiceAgeId = edgeDims.AgeId;
                _video.ShowcaseBodyVoiceGenderId =
                    ShowcaseVoicePresetDimensions.NormalizeGenderId(SelectedDimensionId(seg.CbGender));
                _video.ShowcaseBodyVoiceLanguageId = edgeDims.LanguageId;
            }

            _video.ShowcaseBodyVoiceToneId = ShowcaseElevenToneHelper.NormalizeToneId(SelectedDimensionId(seg.CbTone));
            if (ShowcaseElevenToneHelper.IsCustomTone(_video.ShowcaseBodyVoiceToneId))
            {
                _video.ShowcaseBodyElevenCustomStabilityPercent = ShowcaseElevenToneHelper.ClampPercent(seg.TrkStability.Value);
                _video.ShowcaseBodyElevenCustomSimilarityPercent = ShowcaseElevenToneHelper.ClampPercent(seg.TrkSimilarity.Value);
                _video.ShowcaseBodyElevenCustomStylePercent = ShowcaseElevenToneHelper.ClampPercent(seg.TrkStyle.Value);
            }

            if (seg.CbStyle.SelectedItem is HookStyleListItem bodyStyleItem)
            {
                _video.ShowcaseBodyStyleKey = bodyStyleItem.Key;
            }

            if (SegmentCustomStyle(seg))
            {
                _video.ShowcaseBodyEdgeRateOffsetPercent = ShowcaseEdgeProsodyHelper.ClampRateOffset(seg.TrkRate.Value);
                _video.ShowcaseBodyEdgePitchOffsetHz = ShowcaseEdgeProsodyHelper.ClampPitchOffset(seg.TrkPitch.Value);
            }

            _video.ShowcaseBodyTtsEngine = SelectedSegmentEngineId(seg.CbTts);
            SaveSegmentNarrationSpeed(seg);
        }

        private void SaveSegmentNarrationSpeed(VoiceSegmentUi seg)
        {
            if (seg?.TrkNarrationSpeed == null)
            {
                return;
            }

            var pct = ShowcaseNarrationSpeedHelper.ClampManualPercent(seg.TrkNarrationSpeed.Value);
            if (seg.IsHook)
            {
                _video.ShowcaseHookNarrationSpeedPercent = pct;
            }
            else
            {
                _video.ShowcaseBodyNarrationSpeedPercent = pct;
            }

            ShowcaseNarrationSpeedHelper.SyncLegacyCombinedSpeedField(_video);
        }

        private void LoadVoiceSegmentFromVideo(VoiceSegmentUi seg)
        {
            var presetId = seg.IsHook
                ? _video.ShowcaseVoicePresetId
                : _video.ShowcaseBodyVoicePresetId;
            var langId = seg.IsHook ? _video.ShowcaseVoiceLanguageId : _video.ShowcaseBodyVoiceLanguageId;
            var genderId = seg.IsHook ? _video.ShowcaseVoiceGenderId : _video.ShowcaseBodyVoiceGenderId;
            var personaId = seg.IsHook ? _video.ShowcaseHookElevenPersona : _video.ShowcaseBodyElevenPersona;
            var toneId = seg.IsHook ? _video.ShowcaseVoiceToneId : _video.ShowcaseBodyVoiceToneId;
            var styleKey = seg.IsHook ? _video.ShowcaseHookStyleKey : _video.ShowcaseBodyStyleKey;
            var rateOff = seg.IsHook ? _video.ShowcaseEdgeRateOffsetPercent : _video.ShowcaseBodyEdgeRateOffsetPercent;
            var pitchOff = seg.IsHook ? _video.ShowcaseEdgePitchOffsetHz : _video.ShowcaseBodyEdgePitchOffsetHz;
            var customStability = seg.IsHook ? _video.ShowcaseElevenCustomStabilityPercent : _video.ShowcaseBodyElevenCustomStabilityPercent;
            var customSimilarity = seg.IsHook ? _video.ShowcaseElevenCustomSimilarityPercent : _video.ShowcaseBodyElevenCustomSimilarityPercent;
            var customStyle = seg.IsHook ? _video.ShowcaseElevenCustomStylePercent : _video.ShowcaseBodyElevenCustomStylePercent;
            var engine = seg.IsHook ? _video.ShowcaseHookTtsEngine : _video.ShowcaseBodyTtsEngine;

            SelectSegmentEngineCombo(seg.CbTts, engine);
            SelectDimensionCombo(seg.CbElevenPersona, ElevenVoicePersonaCatalog.Normalize(personaId));

            var dims = ShowcaseVoicePresetDimensions.GetForPreset(presetId);
            SelectDimensionCombo(
                seg.CbGender,
                ShowcaseVoicePresetDimensions.NormalizeGenderId(
                    string.IsNullOrWhiteSpace(genderId) ? dims.GenderId : genderId));
            var resolvedLangId = string.IsNullOrWhiteSpace(langId) ? dims.LanguageId : langId;
            if (SegmentIsEdge(seg))
            {
                SelectDimensionCombo(
                    seg.CbLanguage,
                    ShowcaseVoicePresetDimensions.NormalizeLanguageId(resolvedLangId));
            }
            SelectDimensionCombo(
                seg.CbTone,
                seg.IsHook
                    ? ShowcaseVoicePresetDimensions.Tone.Natural
                    : (string.IsNullOrWhiteSpace(toneId) ? dims.ToneId : ShowcaseElevenToneHelper.NormalizeToneId(toneId)));
            SelectHookStyleCombo(seg.CbStyle, styleKey);
            ApplySegmentEdgeProsodyUi(seg, rateOff, pitchOff);
            ApplySegmentCustomToneUi(seg, customStability, customSimilarity, customStyle);
            ShowcaseNarrationSpeedHelper.EnsureSegmentSpeedDefaults(_video);
            if (seg.TrkNarrationSpeed != null)
            {
                var stored = seg.IsHook
                    ? _video.ShowcaseHookNarrationSpeedPercent
                    : _video.ShowcaseBodyNarrationSpeedPercent;
                var pct = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(stored);
                _voiceUiLock = true;
                try
                {
                    seg.TrkNarrationSpeed.Value = Math.Max(
                        seg.TrkNarrationSpeed.Minimum,
                        Math.Min(seg.TrkNarrationSpeed.Maximum, pct));
                }
                finally
                {
                    _voiceUiLock = false;
                }

                UpdateSegmentNarrationSpeedLabel(seg);
            }
        }

        internal void ApplyVoiceSegmentLayoutFromTab(int tabInnerWidth)
        {
            ApplyVoiceSegmentColumnWidths(tabInnerWidth);
        }

        private void RefreshSegmentStyleComboItems(VoiceSegmentUi seg)
        {
            if (seg?.CbStyle == null)
            {
                return;
            }

            var includeEdgeCustom = SegmentIsEdge(seg);
            var includeElevenVoiceCustom = seg.IsHook && SegmentIsEleven(seg);
            var priorKey = SelectedSegmentStyleKey(seg);
            _voiceUiLock = true;
            try
            {
                seg.CbStyle.Items.Clear();
                foreach (var key in HookStyleCatalog.AllStyleKeys)
                {
                    var icon = HookStyleCatalog.GetStyleIcon(key);
                    seg.CbStyle.Items.Add(new HookStyleListItem(
                        key,
                        icon + " " + HookStyleCatalog.GetDisplayName(key)));
                }

                if (includeEdgeCustom)
                {
                    seg.CbStyle.Items.Add(new HookStyleListItem(
                        ShowcaseEdgeProsodyHelper.StyleCustom,
                        "⚙ Tùy chỉnh (Rate / Pitch)"));
                }

                if (includeElevenVoiceCustom)
                {
                    seg.CbStyle.Items.Add(new HookStyleListItem(
                        ShowcaseElevenToneHelper.HookStyleCustomVoiceKey,
                        "⚙ Tùy chỉnh giọng (stability/similarity/style)"));
                }

                var pickKey = priorKey;
                if (!includeEdgeCustom && ShowcaseEdgeProsodyHelper.IsCustomStyle(priorKey))
                {
                    pickKey = HookStyleCatalog.StyleHuongdan;
                }

                if (!includeElevenVoiceCustom && ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(pickKey))
                {
                    pickKey = HookStyleCatalog.StyleHuongdan;
                }

                if (!string.Equals(pickKey, priorKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (seg.IsHook)
                    {
                        _video.ShowcaseHookStyleKey = pickKey;
                    }
                    else
                    {
                        _video.ShowcaseBodyStyleKey = pickKey;
                    }
                }

                SelectHookStyleCombo(seg.CbStyle, pickKey);
            }
            finally
            {
                _voiceUiLock = false;
            }
        }

        private void SelectHookStyleCombo(ComboBox combo, string styleKey)
        {
            var normalized = ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(styleKey);
            var idx = 0;
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is HookStyleListItem item &&
                    string.Equals(item.Key, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            combo.SelectedIndex = combo.Items.Count > 0 ? idx : -1;
        }

        private static string SegmentSummaryLine(VoiceSegmentUi seg, ShowcaseVideoItem video, bool isHook)
        {
            var engine = SegmentEngineLabel(SelectedSegmentEngineId(seg.CbTts));
            if (SegmentIsEdge(seg))
            {
                var presetId = ShowcaseVoicePresetDimensions.ResolvePresetId(
                    ShowcaseVoicePresetDimensions.BuildFromVoiceUi(
                        DefaultSegmentAgeId,
                        SelectedDimensionId(seg.CbLanguage),
                        SelectedDimensionId(seg.CbTone),
                        SelectedDimensionId(seg.CbGender)));
                var styleKey = SelectedSegmentStyleKey(seg);
                var styleLabel = ShowcaseEdgeProsodyHelper.IsCustomStyle(styleKey)
                    ? "Tùy chỉnh"
                    : HookStyleCatalog.GetDisplayName(styleKey);
                var rate = isHook ? video.ShowcaseEdgeRateOffsetPercent : video.ShowcaseBodyEdgeRateOffsetPercent;
                var pitch = isHook ? video.ShowcaseEdgePitchOffsetHz : video.ShowcaseBodyEdgePitchOffsetHz;
                return engine + " · " + ShowcaseVoicePresetCatalog.GetById(presetId).Label
                       + " · «" + styleLabel + "»"
                       + ShowcaseEdgeProsodyHelper.FormatSegmentProsodySuffix(styleKey, rate, pitch);
            }

            var set = ShowcaseVoicePresetDimensions.BuildFromVoiceUi(
                DefaultSegmentAgeId,
                ShowcaseVoicePresetDimensions.Language.ViSouth,
                SelectedDimensionId(seg.CbTone));
            var resolved = ShowcaseVoicePresetCatalog.GetById(ShowcaseVoicePresetDimensions.ResolvePresetId(set));
            var personaKey = ElevenVoicePersonaCatalog.Normalize(SelectedDimensionId(seg.CbElevenPersona));
            var voiceLabel = string.Equals(personaKey, ElevenVoicePersonaCatalog.None, StringComparison.OrdinalIgnoreCase)
                ? resolved.Label
                : "Giọng: " + ElevenVoicePersonaCatalog.GetDisplayName(personaKey);

            var customStability = isHook ? video.ShowcaseElevenCustomStabilityPercent : video.ShowcaseBodyElevenCustomStabilityPercent;
            var customSimilarity = isHook ? video.ShowcaseElevenCustomSimilarityPercent : video.ShowcaseBodyElevenCustomSimilarityPercent;
            var customStyle = isHook ? video.ShowcaseElevenCustomStylePercent : video.ShowcaseBodyElevenCustomStylePercent;

            if (isHook)
            {
                var styleKey = SelectedSegmentStyleKey(seg);
                string styleLabel;
                string toneSuffix;
                if (ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(styleKey))
                {
                    styleLabel = "⚙ Tùy chỉnh giọng";
                    toneSuffix = " · (" + ShowcaseElevenToneHelper.FormatCustomVoicePercentTriplet(
                        customStability, customSimilarity, customStyle) + ")";
                }
                else
                {
                    styleLabel = ShowcaseEdgeProsodyHelper.IsCustomStyle(styleKey)
                        ? "Tùy chỉnh"
                        : HookStyleCatalog.GetDisplayName(styleKey);
                    toneSuffix = string.Empty;
                }

                return engine + " · " + voiceLabel + " · «" + styleLabel + "»" + toneSuffix;
            }

            var toneId = SelectedDimensionId(seg.CbTone);
            var toneSummary = ShowcaseElevenToneHelper.FormatToneSummary(toneId, customStability, customSimilarity, customStyle);
            return engine + " · " + voiceLabel + (string.IsNullOrEmpty(toneSummary) ? string.Empty : " · " + toneSummary);
        }

        private void AttachVoiceFooterActionButtons(FlowLayoutPanel host)
        {
            if (host == null)
            {
                return;
            }

            if (_renderFullMixedAudioAsync != null)
            {
                _btnRenderFullMixedAudio = CreateVoiceNarrationJellyButton(
                    "btnShowcaseAudioRenderFull",
                    ShowcaseWorkflowConstants.RenderFullMixedAudioButtonText,
                    VoiceNarrationGenerateTint);
                _btnRenderFullMixedAudio.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnRenderFullMixedAudio.Enabled = false;
                    try
                    {
                        await _renderFullMixedAudioAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                    }
                };
                host.Controls.Add(_btnRenderFullMixedAudio);
            }

            if (_listenFullMixedAudioAsync != null)
            {
                _btnListenFullMixedAudio = CreateVoiceNarrationJellyButton(
                    "btnShowcaseAudioListenFull",
                    ShowcaseWorkflowConstants.ListenFullMixedAudioButtonText,
                    VoiceNarrationListenTint);
                _btnListenFullMixedAudio.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnListenFullMixedAudio.Enabled = false;
                    try
                    {
                        await _listenFullMixedAudioAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                    }
                };
                host.Controls.Add(_btnListenFullMixedAudio);
            }
        }

        private void AttachVoiceReviewScriptButton(Panel host)
        {
            if (host == null || _openScriptEditor == null)
            {
                return;
            }

            _btnReviewScript = CreateVoiceNarrationJellyButton(
                "btnShowcaseAudioReviewScript",
                "Xem lời thoại",
                VoiceScriptReviewTint);
            _btnReviewScript.Margin = new Padding(0);
            _btnReviewScript.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    return;
                }

                _openScriptEditor.Invoke();
            };
            host.Controls.Add(_btnReviewScript);
        }

        private void AttachVoiceNarrationButtons()
        {
            if (_generateHookNarrationAsync != null && _hookVoice?.NarrationButtonRow != null)
            {
                _btnGenerateHookNarration = CreateVoiceNarrationJellyButton(
                    "btnShowcaseAudioGenHook",
                    "Tạo audio hook",
                    VoiceNarrationGenerateTint);
                _btnGenerateHookNarration.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnGenerateHookNarration.Enabled = false;
                    try
                    {
                        await _generateHookNarrationAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                        if (_btnGenerateHookNarration != null && !_btnGenerateHookNarration.IsDisposed)
                        {
                            _btnGenerateHookNarration.Enabled = true;
                        }
                    }
                };
                _hookVoice.NarrationButtonRow.Controls.Add(_btnGenerateHookNarration);
            }

            if (_listenHookNarrationAsync != null && _hookVoice?.NarrationButtonRow != null)
            {
                _btnListenHookNarration = CreateVoiceNarrationJellyButton(
                    "btnShowcaseAudioListenHook",
                    "Nghe hook",
                    VoiceNarrationListenTint);
                _btnListenHookNarration.Enabled = _canListenHookNarration?.Invoke() ?? false;
                _btnListenHookNarration.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnListenHookNarration.Enabled = false;
                    try
                    {
                        await _listenHookNarrationAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                    }
                };
                _hookVoice.NarrationButtonRow.Controls.Add(_btnListenHookNarration);
            }

            if (_generateBodyNarrationAsync != null && _bodyVoice?.NarrationButtonRow != null)
            {
                _btnGenerateBodyNarration = CreateVoiceNarrationJellyButton(
                    "btnShowcaseAudioGenBody",
                    "Tạo audio thân",
                    VoiceNarrationGenerateTint);
                _btnGenerateBodyNarration.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnGenerateBodyNarration.Enabled = false;
                    try
                    {
                        await _generateBodyNarrationAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                        if (_btnGenerateBodyNarration != null && !_btnGenerateBodyNarration.IsDisposed)
                        {
                            _btnGenerateBodyNarration.Enabled = true;
                        }
                    }
                };
                _bodyVoice.NarrationButtonRow.Controls.Add(_btnGenerateBodyNarration);
            }

            if (_listenBodyNarrationAsync != null && _bodyVoice?.NarrationButtonRow != null)
            {
                _btnListenBodyNarration = CreateVoiceNarrationJellyButton(
                    "btnShowcaseAudioListenBody",
                    "Nghe thân",
                    VoiceNarrationListenTint);
                _btnListenBodyNarration.Enabled = _canListenBodyNarration?.Invoke() ?? false;
                _btnListenBodyNarration.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    _btnListenBodyNarration.Enabled = false;
                    try
                    {
                        await _listenBodyNarrationAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        RefreshNarrationButtons();
                    }
                };
                _bodyVoice.NarrationButtonRow.Controls.Add(_btnListenBodyNarration);
            }

            LayoutVoiceNarrationButtons(_hookVoice);
            LayoutVoiceNarrationButtons(_bodyVoice);
        }

        private static JellyButton CreateVoiceNarrationJellyButton(string name, string text, Color tint)
        {
            var btn = Form1.CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: Form1.AppJellyButtonHeight,
                minWidth: 96,
                margin: new Padding(0, 0, 8, 0),
                lockSize: true);
            Form1.ResizeAppJellyButton(btn, minWidth: 96);
            return btn;
        }

        private void LayoutVoiceNarrationButtons(VoiceSegmentUi seg)
        {
            if (seg == null)
            {
                return;
            }

            JellyButton gen = null;
            JellyButton listen = null;
            if (seg.IsHook)
            {
                gen = _btnGenerateHookNarration;
                listen = _btnListenHookNarration;
            }
            else
            {
                gen = _btnGenerateBodyNarration;
                listen = _btnListenBodyNarration;
            }

            foreach (var btn in new[] { gen, listen })
            {
                if (btn == null)
                {
                    continue;
                }

                Form1.ResizeAppJellyButton(btn, minWidth: 96);
            }

            seg.NarrationButtonRow?.PerformLayout();
            SyncSegmentShellHeight(seg);
        }
    }
}

