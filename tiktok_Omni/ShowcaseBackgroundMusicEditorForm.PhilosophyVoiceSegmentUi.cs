using System;
using System.Drawing;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Quote-only voice tab UI — isolated from Showcase hook/body layout.</summary>
    internal sealed partial class ShowcaseBackgroundMusicEditorForm
    {
        private const int VoiceSegmentPairLabelWidth = 296;
        private const int PhilosophyVoiceLabelInset = 12;
        private const int VoiceSegRowMain = 0;
        private const int VoiceSegRowFootnote = 1;
        private const int VoiceSegRowButtons = 2;
        private const int VoiceSegLeftRowEngine = 0;
        private const int VoiceSegLeftRowStyle = 1;
        private const int VoiceSegLeftRowGender = 2;
        private const int VoiceSegLeftRowTone = 3;
        private const int VoiceSegPhilosophyRightRowProsody = 0;
        private const int VoiceSegPhilosophyRightRowCustomTone = 1;

        private Control BuildPhilosophyVoiceColumnsPanel()
        {
            _bodyVoice = BuildPhilosophyVoiceSegmentUi("Giọng đọc quote");

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = BackColor,
                Margin = Padding.Empty
            };
            _voiceColumnsPanel = outer;
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.Controls.Add(WrapPhilosophyVoiceSegmentGroup(_bodyVoice, "Giọng đọc quote"), 0, 0);
            AttachVoiceNarrationButtons();
            return outer;
        }

        private Control WrapPhilosophyVoiceSegmentGroup(VoiceSegmentUi seg, string title)
        {
            var box = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
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

                LayoutPhilosophySegmentFromShellClient(seg);
            };

            return box;
        }

        private void ApplyPhilosophyVoiceSegmentColumnWidths(int totalInnerWidth)
        {
            totalInnerWidth = Math.Max(900, totalInnerWidth);
            if (_voiceColumnsPanel == null || !_philosophyVoiceLayoutReady)
            {
                return;
            }

            _voiceColumnsPanel.MinimumSize = new Size(totalInnerWidth, 0);
            _voiceColumnsPanel.Dock = DockStyle.Top;
            _voiceColumnsPanel.Padding = new Padding(VoiceColumnOuterInset, 0, VoiceColumnOuterInset, 0);

            var colW = Math.Max(720, totalInnerWidth - VoiceColumnOuterInset * 2 - 40);
            ApplyPhilosophySegmentColumnShellWidth(_bodyVoice, colW);
            LayoutVoiceNarrationButtons(_bodyVoice);
            _voiceColumnsPanel.PerformLayout();
        }

        private void ApplyPhilosophySegmentColumnShellWidth(VoiceSegmentUi seg, int shellWidth)
        {
            if (seg?.Shell == null || seg.Table == null || shellWidth <= 0)
            {
                return;
            }

            seg.Shell.Width = shellWidth;
            seg.Shell.MinimumSize = new Size(shellWidth, 0);
            LayoutPhilosophySegmentFromShellClient(seg);
            SyncSegmentShellHeight(seg);
        }

        private void LayoutPhilosophySegmentFromShellClient(VoiceSegmentUi seg)
        {
            if (seg?.Shell == null || seg.Table == null)
            {
                return;
            }

            var clientW = Math.Max(320, seg.Shell.ClientSize.Width);
            var leftColW = Math.Max(280, (clientW - 16) / 2);
            var rightColW = Math.Max(280, clientW - leftColW - 16);
            var comboFieldW = Math.Max(140, leftColW - VoiceSegmentPairLabelWidth);
            ApplyPhilosophySegmentTableWidth(seg, clientW, leftColW, rightColW, comboFieldW);
            LayoutVoiceNarrationButtons(seg);
        }

        private void ApplyPhilosophySegmentTableWidth(
            VoiceSegmentUi seg,
            int tableW,
            int leftColW,
            int rightColW,
            int comboFieldW)
        {
            if (seg?.Table == null)
            {
                return;
            }

            seg.Table.Width = tableW;
            seg.Table.MaximumSize = new Size(tableW, 8192);
            if (seg.LeftFieldsPanel != null)
            {
                seg.LeftFieldsPanel.Width = leftColW;
                seg.LeftFieldsPanel.MaximumSize = new Size(leftColW, 8192);
                if (seg.LeftFieldsPanel.ColumnStyles.Count >= 2)
                {
                    seg.LeftFieldsPanel.ColumnStyles[0].Width = VoiceSegmentPairLabelWidth;
                }
            }

            if (seg.RightParamsPanel != null)
            {
                seg.RightParamsPanel.Width = rightColW;
                seg.RightParamsPanel.MaximumSize = new Size(rightColW, 8192);
            }

            LayoutSegmentFieldCombos(seg, comboFieldW);
            if (seg.ProsodyHost != null)
            {
                seg.ProsodyHost.AutoSize = true;
                seg.ProsodyHost.Dock = DockStyle.Top;
                seg.ProsodyHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                seg.ProsodyHost.Width = rightColW;
                LayoutPhilosophyProsodyHostContents(seg, rightColW);
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
                seg.CustomToneHost.Width = rightColW;
                LayoutSliderHostContents(seg.CustomToneHost, rightColW);
                seg.CustomToneHost.PerformLayout();
            }

            if (seg.FootnoteLabel != null)
            {
                seg.FootnoteLabel.MaximumSize = new Size(tableW, 0);
            }

            seg.Shell?.Invalidate(true);
            SyncSegmentShellHeight(seg);
        }

        private void LayoutPhilosophyProsodyHostContents(VoiceSegmentUi seg, int rowWidth)
        {
            if (seg?.ProsodyHost == null)
            {
                return;
            }

            rowWidth = Math.Max(280, rowWidth);
            if (seg.ProsodyHost is TableLayoutPanel prosodyGrid && prosodyGrid.ColumnCount == 2)
            {
                var halfW = Math.Max(140, (rowWidth - 12) / 2);
                foreach (Control c in prosodyGrid.Controls)
                {
                    LayoutSliderHostContents(c, halfW);
                }

                return;
            }

            LayoutProsodyHostContents(seg, rowWidth);
        }

        private VoiceSegmentUi BuildPhilosophyVoiceSegmentUi(string headerHint)
        {
            Label MkLbl(string text, ContentAlignment align = ContentAlignment.MiddleLeft) => new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                MaximumSize = new Size(VoiceSegmentPairLabelWidth - PhilosophyVoiceLabelInset - 4, 0),
                TextAlign = align,
                ForeColor = Color.FromArgb(160, 168, 182),
                Margin = new Padding(PhilosophyVoiceLabelInset, VoiceSegV(10), 8, VoiceSegV(6))
            };

            var seg = new VoiceSegmentUi { IsHook = false };
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

            FillDimensionCombo(seg.CbLanguage, ShowcaseVoicePresetDimensions.ListElevenLanguageOptions());
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
                RowCount = 3,
                BackColor = BackColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            seg.Table = tbl;
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (var i = 0; i < 3; i++)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            const int leftRowCount = 4;
            var leftPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = leftRowCount,
                BackColor = BackColor,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 8, 0)
            };
            leftPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, VoiceSegmentPairLabelWidth));
            leftPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < leftRowCount; i++)
            {
                leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            seg.LeftFieldsPanel = leftPanel;
            leftPanel.Controls.Add(MkLbl("Nguồn TTS"), 0, VoiceSegLeftRowEngine);
            leftPanel.Controls.Add(seg.CbTts, 1, VoiceSegLeftRowEngine);

            seg.LblStyle = MkLbl("Phong cách Edge");
            leftPanel.Controls.Add(seg.LblStyle, 0, VoiceSegLeftRowStyle);
            leftPanel.Controls.Add(seg.CbStyle, 1, VoiceSegLeftRowStyle);

            seg.LblGender = MkLbl("Giới tính");
            leftPanel.Controls.Add(seg.LblGender, 0, VoiceSegLeftRowGender);
            leftPanel.Controls.Add(seg.CbGender, 1, VoiceSegLeftRowGender);
            seg.LblElevenPersona = MkLbl("Giọng ElevenLabs");
            leftPanel.Controls.Add(seg.LblElevenPersona, 0, VoiceSegLeftRowStyle);
            leftPanel.Controls.Add(seg.CbElevenPersona, 1, VoiceSegLeftRowStyle);
            seg.LblLanguage = MkLbl("Ngôn ngữ");
            leftPanel.Controls.Add(seg.LblLanguage, 0, VoiceSegLeftRowGender);
            leftPanel.Controls.Add(seg.CbLanguage, 1, VoiceSegLeftRowGender);
            seg.LblTone = MkLbl("Tone giọng");
            leftPanel.Controls.Add(seg.LblTone, 0, VoiceSegLeftRowTone);
            leftPanel.Controls.Add(seg.CbTone, 1, VoiceSegLeftRowTone);

            const int rightRowCount = 2;
            var rightPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = rightRowCount,
                BackColor = BackColor,
                Dock = DockStyle.Top,
                Margin = new Padding(8, 0, 0, 0)
            };
            for (var i = 0; i < rightRowCount; i++)
            {
                rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            seg.RightParamsPanel = rightPanel;

            var customToneHost = BuildSegmentCustomTonePanel(seg);
            seg.CustomToneHost = customToneHost;

            var prosodyHost = BuildPhilosophySegmentEdgeProsodyPanel(seg);
            seg.ProsodyHost = prosodyHost;
            rightPanel.Controls.Add(prosodyHost, 0, VoiceSegPhilosophyRightRowProsody);
            rightPanel.Controls.Add(customToneHost, 0, VoiceSegPhilosophyRightRowCustomTone);

            tbl.Controls.Add(leftPanel, 0, VoiceSegRowMain);
            tbl.Controls.Add(rightPanel, 1, VoiceSegRowMain);

            seg.FootnoteLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 128, 142),
                Font = new Font("Segoe UI", 9F),
                Text = headerHint,
                Margin = new Padding(PhilosophyVoiceLabelInset, VoiceSegV(2), 0, 0)
            };
            tbl.Controls.Add(seg.FootnoteLabel, 0, VoiceSegRowFootnote);
            tbl.SetColumnSpan(seg.FootnoteLabel, 2);

            seg.NarrationButtonRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, VoiceSegV(4), 0, VoiceSegV(2)),
                Padding = Padding.Empty
            };
            tbl.Controls.Add(seg.NarrationButtonRow, 0, VoiceSegRowButtons);
            tbl.SetColumnSpan(seg.NarrationButtonRow, 2);

            _cbBodyTtsEngine = seg.CbTts;
            return seg;
        }

        private Control BuildPhilosophySegmentEdgeProsodyPanel(VoiceSegmentUi seg)
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = BackColor,
                Margin = new Padding(0, VoiceSegV(2), 0, 0)
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            host.Resize += (_, __) =>
            {
                if (_voiceUiLock || seg.ProsodyHost == null)
                {
                    return;
                }

                var w = Math.Max(280, host.ClientSize.Width);
                if (w > 0)
                {
                    LayoutPhilosophyProsodyHostContents(seg, w);
                }

                SyncSegmentShellHeight(seg);
            };

            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Rate",
                seg.TrkRate,
                seg.LblRateValue,
                "-25%",
                "+25%",
                400), 0, 0);
            host.Controls.Add(BuildEdgeProsodySliderRow(
                "Pitch",
                seg.TrkPitch,
                seg.LblPitchValue,
                "-12Hz",
                "+12Hz",
                400), 1, 0);
            return host;
        }

        private int ResolveSegmentProsodyRightRow(VoiceSegmentUi seg) =>
            VoiceSegPhilosophyRightRowProsody;

        private int ResolveSegmentCustomToneRightRow(VoiceSegmentUi seg) =>
            VoiceSegPhilosophyRightRowCustomTone;

        private void ApplyPhilosophyEngineFieldVisibility(VoiceSegmentUi seg, bool eleven, bool edge)
        {
            SetSegmentLeftRowVisible(seg, VoiceSegLeftRowStyle, eleven || edge);
            if (seg.LblElevenPersona != null)
            {
                seg.LblElevenPersona.Visible = eleven;
            }

            if (seg.CbElevenPersona != null)
            {
                seg.CbElevenPersona.Visible = eleven;
            }

            if (seg.LblStyle != null)
            {
                seg.LblStyle.Visible = edge;
                seg.LblStyle.Text = "Phong cách Edge";
            }

            if (seg.CbStyle != null)
            {
                seg.CbStyle.Visible = edge;
            }

            SetSegmentLeftRowVisible(seg, VoiceSegLeftRowGender, eleven || edge);
            if (seg.LblLanguage != null)
            {
                seg.LblLanguage.Visible = eleven;
            }

            if (seg.CbLanguage != null)
            {
                seg.CbLanguage.Visible = eleven;
            }

            if (seg.LblGender != null)
            {
                seg.LblGender.Visible = edge;
            }

            if (seg.CbGender != null)
            {
                seg.CbGender.Visible = edge;
            }

            SetSegmentLeftRowVisible(seg, VoiceSegLeftRowTone, eleven);
            if (seg.LblTone != null)
            {
                seg.LblTone.Visible = eleven;
            }

            if (seg.CbTone != null)
            {
                seg.CbTone.Visible = eleven;
            }

            SetSegmentRightRowVisible(seg, VoiceSegPhilosophyRightRowProsody, edge);
            if (seg.ProsodyHost != null)
            {
                seg.ProsodyHost.Visible = edge;
            }
        }

        private void SetSegmentPanelRowVisible(TableLayoutPanel panel, int row, bool visible)
        {
            if (panel == null || row < 0 || row >= panel.RowStyles.Count)
            {
                return;
            }

            panel.RowStyles[row].SizeType = visible ? SizeType.AutoSize : SizeType.Absolute;
            panel.RowStyles[row].Height = 0f;
            foreach (Control c in panel.Controls)
            {
                if (panel.GetRow(c) != row)
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

        private void SetSegmentLeftRowVisible(VoiceSegmentUi seg, int row, bool visible) =>
            SetSegmentPanelRowVisible(seg?.LeftFieldsPanel, row, visible);

        private void SetSegmentRightRowVisible(VoiceSegmentUi seg, int row, bool visible) =>
            SetSegmentPanelRowVisible(seg?.RightParamsPanel, row, visible);
    }
}
