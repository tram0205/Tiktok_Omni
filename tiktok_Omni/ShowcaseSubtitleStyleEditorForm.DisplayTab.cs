using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseSubtitleStyleEditorForm
    {
        private const int DisplayGroupPaddingH = 14;
        private const int DisplayRowInsetH = 16;
        /// <summary>1104 × 0.9 — ô thoại tab Chữ hiển thị.</summary>
        private const int DisplayTextWidth = 994;
        private const int DisplayRowGap = 12;
        private const int DisplayEffectColumnWidth = 336;
        private const int DisplayRowInnerWidth = DisplayTextWidth + DisplayRowGap + DisplayEffectColumnWidth;
        private const int DisplayBlockWidth = DisplayRowInnerWidth + DisplayGroupPaddingH * 2 + DisplayRowInsetH * 2;
        private const int DisplayTabStackWidth = DisplayBlockWidth;
        private const int DisplayIntroWidth = DisplayRowInnerWidth;
        private const int DisplayTextMinHeight = 36;
        private const int DisplayTextVerticalPadding = 14;
        private const int DisplayBlockTitleGap = 44;
        private const int DisplayBlockChromeHeight = 26;
        private const int DisplayEffectControlHeight = 36;
        private const int DisplayEffectHeaderHeight = DisplayEffectControlHeight * 2;

        private TabPage _displayTabHost;
        private TextBox _txtHookDisplay;
        private ComboBox _cbHookEffect;
        private string _hookSourceSpeech = string.Empty;
        private TextBox _txtCtaDisplay;
        private ComboBox _cbCtaEffect;
        private string _ctaSourceSpeech = string.Empty;
        private readonly List<SceneDisplayRow> _sceneDisplayRows = new List<SceneDisplayRow>();

        private sealed class SceneDisplayRow
        {
            public AiVideoGenInputItem Scene { get; set; }

            public int SceneOrder { get; set; }

            public string SourceSpeech { get; set; } = string.Empty;

            public TextBox TxtDisplay { get; set; }

            public ComboBox CbEffect { get; set; }
        }

        private void BuildDisplayTabUi()
        {
            if (_displayTabHost == null)
            {
                return;
            }

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BackColor,
                Padding = new Padding(8, 4, 16, 12)
            };
            scroll.HorizontalScroll.Enabled = false;
            scroll.HorizontalScroll.Visible = false;

            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = DisplayBlockWidth,
                MaximumSize = new Size(DisplayBlockWidth, 0),
                BackColor = BackColor
            };

            stack.Controls.Add(MakeDisplayIntroLabel());
            stack.Controls.Add(MakeDisplayColumnHeader());

            _hookSourceSpeech = ShowcaseSubtitleDisplayHelper.ResolveHookSpeechSource(_video);
            stack.Controls.Add(MakeDisplayBlock(
                "Hook (mở đầu)",
                ShowcaseDisplayLineEffectKind.Hook,
                out _txtHookDisplay,
                out _cbHookEffect));

            _sceneDisplayRows.Clear();
            var scenes = (_video.Scenes ?? Enumerable.Empty<AiVideoGenInputItem>()).Where(s => s != null).ToList();
            var firstVoiced = ShowcaseSubtitleDisplayHelper.FindFirstVoicedSceneIndex(scenes);
            var order = 0;
            foreach (var scene in scenes)
            {
                order++;
                if (scene.ShowcaseSceneSilent || string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    continue;
                }

                if (order - 1 == firstVoiced)
                {
                    continue;
                }

                var title = "Cảnh " + order;
                var st = (scene.SceneTitle ?? string.Empty).Trim();
                if (st.Length > 0)
                {
                    title += " — " + st;
                }

                TextBox txt;
                ComboBox cbFx;
                stack.Controls.Add(MakeDisplayBlock(title, ShowcaseDisplayLineEffectKind.Body, out txt, out cbFx));
                _sceneDisplayRows.Add(new SceneDisplayRow
                {
                    Scene = scene,
                    SceneOrder = order,
                    SourceSpeech = scene.SceneVoiceover.Trim(),
                    TxtDisplay = txt,
                    CbEffect = cbFx
                });
            }

            _ctaSourceSpeech = (_video.ShowcaseCtaText ?? string.Empty).Trim();
            if (_ctaSourceSpeech.Length > 0)
            {
                stack.Controls.Add(MakeDisplayBlock(
                    "CTA (kết)",
                    ShowcaseDisplayLineEffectKind.Body,
                    out _txtCtaDisplay,
                    out _cbCtaEffect));
            }
            else
            {
                _txtCtaDisplay = null;
                _cbCtaEffect = null;
            }

            scroll.Controls.Add(stack);
            _displayTabHost.Controls.Add(scroll);
        }

        private Label MakeDisplayIntroLabel()
        {
            return new Label
            {
                Text =
                    "Xóa từ không muốn lên màn; bên phải chọn hiệu ứng riêng cho dòng đó.",
                AutoSize = true,
                MaximumSize = new Size(DisplayIntroWidth, 0),
                ForeColor = Color.FromArgb(130, 138, 152),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 0, 0),
                Padding = new Padding(0)
            };
        }

        private Control MakeDisplayColumnHeader()
        {
            var header = new Panel
            {
                Width = DisplayBlockWidth,
                Height = DisplayEffectHeaderHeight,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 6)
            };

            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Width = DisplayRowInnerWidth,
                Height = DisplayEffectHeaderHeight,
                Location = new Point(DisplayGroupPaddingH + DisplayRowInsetH, 0),
                BackColor = BackColor
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DisplayTextWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DisplayEffectColumnWidth));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, DisplayEffectHeaderHeight));

            row.Controls.Add(CreateDisplayColumnHeaderBadge("Phụ đề"), 0, 0);
            row.Controls.Add(CreateDisplayColumnHeaderBadge("Hiệu ứng"), 1, 0);
            header.Controls.Add(row);
            return header;
        }

        private Panel CreateDisplayColumnHeaderBadge(string title)
        {
            var badge = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(42, 52, 78)
            };
            badge.Paint += (_, e) =>
            {
                var g = e.Graphics;
                using (var border = new Pen(Color.FromArgb(110, 155, 235), 1.5f))
                {
                    g.DrawRectangle(border, 0, 0, badge.Width - 1, badge.Height - 1);
                }
            };
            badge.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(190, 215, 255),
                BackColor = Color.FromArgb(42, 52, 78),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            });
            return badge;
        }

        private Control MakeDisplayBlock(
            string title,
            ShowcaseDisplayLineEffectKind effectKind,
            out TextBox txtDisplay,
            out ComboBox cbEffect)
        {
            var box = new GroupBox
            {
                Text = title,
                ForeColor = Color.FromArgb(200, 208, 222),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(31, 34, 42),
                Width = DisplayBlockWidth,
                Height = DisplayTextMinHeight + DisplayBlockTitleGap + DisplayBlockChromeHeight,
                Margin = new Padding(0, 0, 0, 14),
                Padding = new Padding(DisplayGroupPaddingH, 38, DisplayGroupPaddingH, 12)
            };

            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Width = DisplayRowInnerWidth,
                Height = DisplayTextMinHeight,
                Location = new Point(DisplayRowInsetH, DisplayBlockTitleGap),
                BackColor = box.BackColor
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DisplayTextWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DisplayEffectColumnWidth));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, DisplayTextMinHeight));

            txtDisplay = new TextBox
            {
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.None,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 0)
            };

            var effectHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = box.BackColor,
                Padding = new Padding(0, 0, 0, 0)
            };
            cbEffect = CreateDisplayEffectCombo(effectKind);
            cbEffect.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            cbEffect.Location = new Point(0, 0);
            cbEffect.Width = DisplayEffectColumnWidth;
            cbEffect.Height = DisplayEffectControlHeight;
            cbEffect.Margin = new Padding(0);
            effectHost.Controls.Add(cbEffect);

            row.Controls.Add(txtDisplay, 0, 0);
            row.Controls.Add(effectHost, 1, 0);
            box.Controls.Add(row);

            var displayBox = txtDisplay;
            displayBox.TextChanged += (_, __) => ResizeDisplayTextBox(displayBox);
            return box;
        }

        private ComboBox CreateDisplayEffectCombo(ShowcaseDisplayLineEffectKind kind)
        {
            var cb = new ComboBox
            {
                Width = DisplayEffectColumnWidth,
                DropDownWidth = 460,
                Height = DisplayEffectControlHeight,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(0)
            };
            ShowcaseDisplayLineAnimationHelper.PopulateCombo(cb, kind);
            cb.SelectedIndex = 0;
            return cb;
        }

        private void ResizeDisplayTextBox(TextBox txt)
        {
            if (txt == null || txt.IsDisposed)
            {
                return;
            }

            var height = MeasureDisplayTextHeight(txt, txt.Text ?? string.Empty);
            txt.Height = height;
            if (txt.Parent is TableLayoutPanel row)
            {
                row.Height = height;
                if (row.RowStyles.Count > 0)
                {
                    row.RowStyles[0].Height = height;
                }

                if (row.Parent is GroupBox box)
                {
                    box.Height = height + DisplayBlockTitleGap + DisplayBlockChromeHeight;
                }
            }
        }

        private int MeasureDisplayTextHeight(TextBox txt, string text)
        {
            var sample = string.IsNullOrEmpty(text) ? " " : text;
            var innerWidth = Math.Max(120, DisplayTextWidth - 8);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.Left | TextFormatFlags.Top;
            var measured = TextRenderer.MeasureText(sample, txt.Font, new Size(innerWidth, int.MaxValue), flags);
            return Math.Max(DisplayTextMinHeight, measured.Height + DisplayTextVerticalPadding);
        }

        private void ResizeAllDisplayTextBoxes()
        {
            ResizeDisplayTextBox(_txtHookDisplay);
            foreach (var row in _sceneDisplayRows)
            {
                ResizeDisplayTextBox(row?.TxtDisplay);
            }

            ResizeDisplayTextBox(_txtCtaDisplay);
        }

        private void LoadDisplayFromVideo()
        {
            if (_txtHookDisplay != null)
            {
                _txtHookDisplay.Text = ShowcaseSubtitleDisplayHelper.GetEditorDisplayText(
                    _video.ShowcaseSubtitleDisplayHook,
                    _hookSourceSpeech);
            }

            ShowcaseDisplayLineAnimationHelper.SelectStorage(
                _cbHookEffect,
                ShowcaseDisplayLineEffectKind.Hook,
                _video.ShowcaseSubtitleDisplayHookAnimation);

            foreach (var row in _sceneDisplayRows)
            {
                if (row?.TxtDisplay == null || row.Scene == null)
                {
                    continue;
                }

                row.TxtDisplay.Text = ShowcaseSubtitleDisplayHelper.GetEditorDisplayText(
                    row.Scene.ShowcaseSubtitleDisplayVoiceover,
                    row.SourceSpeech);
                ShowcaseDisplayLineAnimationHelper.SelectStorage(
                    row.CbEffect,
                    ShowcaseDisplayLineEffectKind.Body,
                    row.Scene.ShowcaseSubtitleDisplayAnimation);
            }

            if (_txtCtaDisplay != null)
            {
                _txtCtaDisplay.Text = ShowcaseSubtitleDisplayHelper.GetEditorDisplayText(
                    _video.ShowcaseSubtitleDisplayCta,
                    _ctaSourceSpeech);
            }

            ShowcaseDisplayLineAnimationHelper.SelectStorage(
                _cbCtaEffect,
                ShowcaseDisplayLineEffectKind.Body,
                _video.ShowcaseSubtitleDisplayCtaAnimation);

            ResizeAllDisplayTextBoxes();
        }

        private void SaveDisplayToVideo()
        {
            if (_txtHookDisplay != null)
            {
                _video.ShowcaseSubtitleDisplayHook = ShowcaseSubtitleDisplayHelper.CoalesceDisplayOverrideForSave(
                    _txtHookDisplay.Text,
                    _hookSourceSpeech);
            }

            _video.ShowcaseSubtitleDisplayHookAnimation = ShowcaseDisplayLineAnimationHelper.GetSelectedStorage(
                _cbHookEffect,
                ShowcaseDisplayLineEffectKind.Hook);

            foreach (var row in _sceneDisplayRows)
            {
                if (row?.Scene == null || row.TxtDisplay == null)
                {
                    continue;
                }

                row.Scene.ShowcaseSubtitleDisplayVoiceover = ShowcaseSubtitleDisplayHelper.CoalesceDisplayOverrideForSave(
                    row.TxtDisplay.Text,
                    row.SourceSpeech);
                row.Scene.ShowcaseSubtitleDisplayAnimation = ShowcaseDisplayLineAnimationHelper.GetSelectedStorage(
                    row.CbEffect,
                    ShowcaseDisplayLineEffectKind.Body);
            }

            if (_txtCtaDisplay != null)
            {
                _video.ShowcaseSubtitleDisplayCta = ShowcaseSubtitleDisplayHelper.CoalesceDisplayOverrideForSave(
                    _txtCtaDisplay.Text,
                    _ctaSourceSpeech);
            }

            _video.ShowcaseSubtitleDisplayCtaAnimation = ShowcaseDisplayLineAnimationHelper.GetSelectedStorage(
                _cbCtaEffect,
                ShowcaseDisplayLineEffectKind.Body);
        }
    }
}
