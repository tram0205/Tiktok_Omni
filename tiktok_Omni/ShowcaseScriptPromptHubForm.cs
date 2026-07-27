using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseScriptPromptHubForm : Form
    {
        private static readonly Color Bg = ShowcasePastelTheme.ShellBg;
        private static readonly Color AccentScript = ShowcasePastelTheme.ScriptHeader;
        private static readonly Color AccentScriptCap = ShowcasePastelTheme.ScriptCap;
        private static readonly Color AccentPrompt = ShowcasePastelTheme.PromptHeader;
        private static readonly Color AccentPromptCap = ShowcasePastelTheme.PromptCap;
        private static readonly Color FrameBodyScript = ShowcasePastelTheme.ScriptFrame;
        private static readonly Color FrameBodyPrompt = ShowcasePastelTheme.PromptFrame;
        private static readonly Color FieldBg = ShowcasePastelTheme.FieldBg;

        private const int HeaderRowHeight = 128;
        private const int ButtonBarHeight = 72;
        private const int FormClientWidth = 2577;
        private const int FormMinWidth = 2178;
        private const int StackDefaultWidth = 1162;
        private const int FieldDefaultWidth = 1016;
        private const int SectionDefaultWidth = 1125;
        private const int CapBarHeight = 54;
        private const float FieldFontSize = 11.5F;
        private const float MonoFieldFontSize = 10.5F;
        private const int PromptBoxMinHeight = 96;
        private const int PromptBoxMaxHeight = 720;

        private readonly ShowcaseVideoItem _video;
        private readonly Func<Task> _exportExcelAsync;
        private TextBox _txtTheme;
        private TextBox _txtHook;
        private TextBox _txtCta;
        private readonly List<TextBox> _sceneVoiceBoxes = new List<TextBox>();
        private readonly List<ScenePromptFields> _scenePromptFields = new List<ScenePromptFields>();
        private readonly List<TextBox> _autoHeightPromptBoxes = new List<TextBox>();
        private FlowLayoutPanel _scriptStack;
        private FlowLayoutPanel _promptStack;

        private sealed class ScenePromptFields
        {
            public AiVideoGenInputItem Scene { get; set; }
            public TextBox VeoBox { get; set; }
            public TextBox KlingBox { get; set; }
            public TextBox ZoomBox { get; set; }
        }

        public ShowcaseScriptPromptHubForm(ShowcaseVideoItem video, Func<Task> exportExcelAsync = null)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));
            _exportExcelAsync = exportExcelAsync;

            var product = (_video.ProductName ?? string.Empty).Trim();
            Text = "Kịch bản · Prompt" + (product.Length > 0 ? " — " + product : string.Empty);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Bg;
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(FormMinWidth, 1170);
            ClientSize = new Size(FormClientWidth, 1380);

            var shell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(42, 24, 42, 20)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonBarHeight));

            root.Controls.Add(MakeHeader("Kịch bản", "Thoại · Hook · CTA", AccentScript, rightColumn: false), 0, 0);
            root.Controls.Add(MakeHeader("Prompt clip", "Veo · Zoom · Kling", AccentPrompt, rightColumn: true), 1, 0);

            root.Controls.Add(BuildScriptScrollColumn(), 0, 1);
            root.Controls.Add(BuildPromptScrollColumn(), 1, 1);

            var btnSave = MakeButton("Lưu", ShowcasePastelTheme.ButtonSave);
            btnSave.DialogResult = DialogResult.OK;
            AcceptButton = btnSave;
            var btnCancel = MakeButton("Hủy", ShowcasePastelTheme.ButtonCancel);
            btnCancel.DialogResult = DialogResult.Cancel;
            CancelButton = btnCancel;

            var bottomBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = Bg,
                Padding = new Padding(0, 10, 0, 0)
            };
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var leftSpacer = new Panel { Dock = DockStyle.Fill, BackColor = Bg };

            var rightHalfBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                BackColor = Bg
            };
            rightHalfBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rightHalfBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightHalfBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var flpExcel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Bg,
                Padding = new Padding(4, 0, 8, 0)
            };
            if (_exportExcelAsync != null)
            {
                var btnExcel = MakeButton("Tải excel prompt", ShowcasePastelTheme.ButtonExcel);
                btnExcel.Click += async (_, __) =>
                {
                    if (!ValidateAndSave())
                    {
                        return;
                    }

                    btnExcel.Enabled = false;
                    try
                    {
                        await _exportExcelAsync().ConfigureAwait(true);
                    }
                    finally
                    {
                        if (!btnExcel.IsDisposed)
                        {
                            btnExcel.Enabled = true;
                        }
                    }
                };
                flpExcel.Controls.Add(btnExcel);
            }

            var flpRight = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Bg,
                Padding = new Padding(0, 0, 8, 0),
                MinimumSize = new Size(320, 48)
            };
            flpRight.Controls.Add(btnCancel);
            flpRight.Controls.Add(btnSave);

            var midSpacer = new Panel { Dock = DockStyle.Fill, BackColor = Bg };

            rightHalfBar.Controls.Add(flpExcel, 0, 0);
            rightHalfBar.Controls.Add(midSpacer, 1, 0);
            rightHalfBar.Controls.Add(flpRight, 2, 0);

            bottomBar.Controls.Add(leftSpacer, 0, 0);
            bottomBar.Controls.Add(rightHalfBar, 1, 0);

            root.Controls.Add(bottomBar, 0, 2);
            root.SetColumnSpan(bottomBar, 2);

            shell.Controls.Add(root);
            Controls.Add(shell);

            btnSave.Click += (_, __) =>
            {
                if (!ValidateAndSave())
                {
                    DialogResult = DialogResult.None;
                }
            };

            Shown += (_, __) =>
            {
                SyncAllStackWidths();
                RefitAllPromptBoxes();
            };
        }

        private Control BuildScriptScrollColumn()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = FrameBodyScript,
                Margin = new Padding(0, 6, 8, 8),
                Padding = new Padding(10, 10, 10, 10)
            };

            _scriptStack = CreateVerticalStack(FrameBodyScript);
            scroll.Controls.Add(_scriptStack);
            scroll.Resize += (_, __) =>
            {
                SyncStackWidth(_scriptStack, scroll);
                RefitAllPromptBoxes();
            };

            _txtTheme = MakeFieldBox(_video.ShowcaseTheme, height: 56, mono: false, autoGrow: false);
            _txtHook = MakeFieldBox(_video.ShowcaseHookText, height: 132, mono: false, autoGrow: false);
            _txtCta = MakeFieldBox(_video.ShowcaseCtaText, height: 132, mono: false, autoGrow: false);

            _scriptStack.Controls.Add(MakeSectionFrame("Chủ đề video", AccentScriptCap, FrameBodyScript, _txtTheme));
            _scriptStack.Controls.Add(MakeSectionFrame("Hook mở đầu", AccentScriptCap, FrameBodyScript, _txtHook));
            _scriptStack.Controls.Add(MakeSectionFrame("CTA kết thúc", AccentScriptCap, FrameBodyScript, _txtCta));

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            if (scenes.Count == 0)
            {
                _scriptStack.Controls.Add(MakeInfoFrame(
                    "Chưa có cảnh",
                    "Thêm ảnh trên storyboard hoặc «Tạo kịch bản» trước.",
                    AccentScriptCap,
                    FrameBodyScript));
            }
            else
            {
                for (var i = 0; i < scenes.Count; i++)
                {
                    var scene = scenes[i];
                    var role = string.IsNullOrWhiteSpace(scene.SceneRole) ? "?" : scene.SceneRole;
                    var title = string.IsNullOrWhiteSpace(scene.SceneTitle) ? "Cảnh " + (i + 1) : scene.SceneTitle;
                    var cap = (i + 1) + ". [" + role + "] " + title;
                    var box = MakeFieldBox(scene.SceneVoiceover, height: 148, mono: false, autoGrow: false);
                    _sceneVoiceBoxes.Add(box);
                    _scriptStack.Controls.Add(MakeSectionFrame(cap, AccentScriptCap, FrameBodyScript, box));
                }
            }

            return scroll;
        }

        private Control BuildPromptScrollColumn()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = FrameBodyPrompt,
                Margin = new Padding(0, 6, 0, 8),
                Padding = new Padding(10, 10, 10, 10)
            };

            _promptStack = CreateVerticalStack(FrameBodyPrompt);
            scroll.Controls.Add(_promptStack);
            scroll.Resize += (_, __) =>
            {
                SyncStackWidth(_promptStack, scroll);
                RefitAllPromptBoxes();
            };

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            if (scenes.Count == 0)
            {
                _promptStack.Controls.Add(MakeInfoFrame(
                    "Chưa có cảnh",
                    "Thêm ảnh trước khi nhập prompt clip.",
                    AccentPromptCap,
                    FrameBodyPrompt));
            }
            else
            {
                for (var i = 0; i < scenes.Count; i++)
                {
                    var scene = scenes[i];
                    var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
                    if (string.IsNullOrEmpty(tool))
                    {
                        tool = ShowcaseClipToolHelper.ResolveDefaultTool(_video.ShowcaseClipModeId, scene.ShowcaseImageKind);
                    }

                    var role = string.IsNullOrWhiteSpace(scene.SceneRole) ? "?" : scene.SceneRole;
                    var title = string.IsNullOrWhiteSpace(scene.SceneTitle) ? "Cảnh " + (i + 1) : scene.SceneTitle;
                    var cap = (i + 1) + ". [" + role + "] " + title + " · "
                              + ShowcaseClipToolHelper.GetToolDisplayLabel(tool);

                    var inner = new FlowLayoutPanel
                    {
                        FlowDirection = FlowDirection.TopDown,
                        WrapContents = false,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        BackColor = FrameBodyPrompt,
                        Width = SectionDefaultWidth,
                        Padding = new Padding(0)
                    };

                    var veoBox = MakePromptFieldBox(scene.VeoPrompt);
                    var klingBox = MakePromptFieldBox(scene.KlingPrompt);
                    var zoomBox = MakePromptFieldBox(scene.ZoomHint, mono: false);

                    var showVeo = tool == ShowcaseClipToolHelper.ToolVeo || tool == ShowcaseClipToolHelper.ToolKling;
                    var showKling = tool == ShowcaseClipToolHelper.ToolKling;
                    var showZoom = tool == ShowcaseClipToolHelper.ToolZoom;

                    AddPromptSubField(inner, "Prompt Veo (Flow I2V)", veoBox, showVeo);
                    AddPromptSubField(inner, "Prompt Kling (on-model)", klingBox, showKling);
                    AddPromptSubField(inner, "Zoom (Ken Burns trong app)", zoomBox, showZoom);

                    _scenePromptFields.Add(new ScenePromptFields
                    {
                        Scene = scene,
                        VeoBox = veoBox,
                        KlingBox = klingBox,
                        ZoomBox = zoomBox
                    });

                    _promptStack.Controls.Add(MakeSectionFrame(cap, AccentPromptCap, FrameBodyPrompt, inner));
                }
            }

            return scroll;
        }

        private TextBox MakePromptFieldBox(string raw, bool mono = true)
        {
            var cleaned = ShowcasePromptTextHelper.StripDurationClauses(raw ?? string.Empty);
            var box = MakeFieldBox(cleaned, height: PromptBoxMinHeight, mono: mono, autoGrow: true);
            return box;
        }

        private static FlowLayoutPanel CreateVerticalStack(Color back) => new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            BackColor = back,
            Width = StackDefaultWidth,
            Padding = new Padding(6, 6, 6, 14)
        };

        private void RefitAllPromptBoxes()
        {
            foreach (var box in _autoHeightPromptBoxes)
            {
                FitPromptBoxHeight(box);
            }

            _promptStack?.PerformLayout();
            _scriptStack?.PerformLayout();
        }

        private static void FitPromptBoxHeight(TextBox box)
        {
            if (box == null || box.IsDisposed || box.Width < 40)
            {
                return;
            }

            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
            var text = box.Text ?? string.Empty;
            var innerW = Math.Max(40, box.Width - 8);
            var measured = TextRenderer.MeasureText(text, box.Font, new Size(innerW, int.MaxValue), flags);
            var target = Math.Max(PromptBoxMinHeight, measured.Height + 22);
            if (target > PromptBoxMaxHeight)
            {
                box.Height = PromptBoxMaxHeight;
                box.ScrollBars = ScrollBars.Vertical;
            }
            else
            {
                box.Height = target;
                box.ScrollBars = ScrollBars.None;
            }
        }

        private void SyncAllStackWidths()
        {
            if (_scriptStack?.Parent is Panel leftScroll)
            {
                SyncStackWidth(_scriptStack, leftScroll);
            }

            if (_promptStack?.Parent is Panel rightScroll)
            {
                SyncStackWidth(_promptStack, rightScroll);
            }

            _scriptStack?.PerformLayout();
            _promptStack?.PerformLayout();
            RefitAllPromptBoxes();
        }

        private void SyncStackWidth(FlowLayoutPanel stack, Panel scrollHost)
        {
            var w = Math.Max(871, scrollHost.ClientSize.Width - scrollHost.Padding.Horizontal - 16);
            stack.Width = w;
            foreach (Control c in stack.Controls)
            {
                if (c is FlowLayoutPanel section)
                {
                    section.Width = w - 8;
                    var sectionInnerWidth = section.Width;
                    foreach (Control child in section.Controls)
                    {
                        if (child is Panel cap && cap.Height <= CapBarHeight + 2)
                        {
                            cap.Width = sectionInnerWidth;
                            foreach (Control capChild in cap.Controls)
                            {
                                if (capChild is Label cl)
                                {
                                    cl.MaximumSize = new Size(sectionInnerWidth - 28, CapBarHeight - 6);
                                    cl.Top = Math.Max(0, (cap.Height - cl.Height) / 2);
                                }
                            }
                        }
                        else if (child is TextBox box)
                        {
                            box.Width = sectionInnerWidth;
                        }
                        else if (child is FlowLayoutPanel inner)
                        {
                            inner.Width = sectionInnerWidth;
                            foreach (Control ic in inner.Controls)
                            {
                                if (ic is TextBox ib)
                                {
                                    ib.Width = sectionInnerWidth;
                                }
                            }
                        }
                        else if (child is Label lbl && !lbl.AutoSize)
                        {
                            lbl.Width = sectionInnerWidth;
                        }
                    }
                }
            }
        }

        private bool ValidateAndSave()
        {
            _video.ShowcaseTheme = _txtTheme.Text?.Trim() ?? string.Empty;
            _video.ShowcaseHookText = _txtHook.Text?.Trim() ?? string.Empty;
            _video.ShowcaseCtaText = _txtCta.Text?.Trim() ?? string.Empty;

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            for (var i = 0; i < scenes.Count && i < _sceneVoiceBoxes.Count; i++)
            {
                scenes[i].SceneVoiceover = _sceneVoiceBoxes[i].Text?.Trim() ?? string.Empty;
                scenes[i].ShowcaseTheme = _video.ShowcaseTheme;
            }

            for (var i = 0; i < _scenePromptFields.Count; i++)
            {
                var entry = _scenePromptFields[i];
                var scene = entry.Scene;
                if (scene == null)
                {
                    continue;
                }

                var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);
                if (string.IsNullOrEmpty(tool))
                {
                    tool = ShowcaseClipToolHelper.ResolveDefaultTool(_video.ShowcaseClipModeId, scene.ShowcaseImageKind);
                    scene.ShowcaseClipTool = tool;
                }

                scene.VeoPrompt = ShowcaseVeoPromptSanitizer.Sanitize(entry.VeoBox.Text?.Trim() ?? string.Empty, i);
                scene.KlingPrompt = ShowcaseKlingPromptSanitizer.Sanitize(entry.KlingBox.Text?.Trim() ?? string.Empty, i);
                scene.ZoomHint = ShowcasePromptTextHelper.StripDurationClauses(entry.ZoomBox.Text?.Trim() ?? string.Empty);
            }

            _video.ApplySettingsToScenes();
            ShowcaseContentDisplayHelper.RefreshContentLabels(_video);
            return true;
        }

        private static void AddPromptSubField(FlowLayoutPanel section, string title, TextBox field, bool visible)
        {
            if (!visible)
            {
                field.Visible = false;
                return;
            }

            section.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                ForeColor = ShowcasePastelTheme.TextMuted,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Margin = new Padding(0, 8, 0, 8)
            });
            field.Margin = new Padding(0, 0, 0, 10);
            section.Controls.Add(field);
        }

        private TextBox MakeFieldBox(string text, int height, bool mono, bool autoGrow)
        {
            var box = new TextBox
            {
                Text = text ?? string.Empty,
                Multiline = true,
                WordWrap = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.None,
                Height = height,
                Width = FieldDefaultWidth,
                BackColor = FieldBg,
                ForeColor = ShowcasePastelTheme.TextBody,
                BorderStyle = BorderStyle.FixedSingle,
                Font = mono ? new Font("Consolas", MonoFieldFontSize) : new Font("Segoe UI", FieldFontSize),
                Margin = new Padding(0, 4, 0, 6)
            };

            if (autoGrow)
            {
                _autoHeightPromptBoxes.Add(box);
                box.TextChanged += (_, __) => FitPromptBoxHeight(box);
                box.Resize += (_, __) => FitPromptBoxHeight(box);
            }

            return box;
        }

        private static FlowLayoutPanel MakeSectionFrame(string caption, Color accent, Color bodyBg, Control content)
        {
            var section = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = bodyBg,
                Margin = new Padding(0, 0, 0, 18),
                Width = SectionDefaultWidth,
                Padding = new Padding(0, 0, 0, 6)
            };

            var capBar = MakeCapBar(caption, accent, SectionDefaultWidth);

            content.Margin = new Padding(0, 12, 0, 10);
            content.Width = SectionDefaultWidth;

            section.Controls.Add(capBar);
            section.Controls.Add(content);
            return section;
        }

        private static Panel MakeCapBar(string caption, Color accent, int width)
        {
            var capBar = new Panel
            {
                Height = CapBarHeight,
                Width = width,
                BackColor = accent
            };

            var capLabel = new Label
            {
                Text = caption,
                AutoSize = true,
                MaximumSize = new Size(Math.Max(120, width - 28), CapBarHeight - 6),
                Left = 14,
                ForeColor = ShowcasePastelTheme.TextPrimary,
                BackColor = accent,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                UseCompatibleTextRendering = true
            };

            void CenterCaption()
            {
                capLabel.MaximumSize = new Size(Math.Max(120, capBar.Width - 28), CapBarHeight - 6);
                capLabel.Top = Math.Max(0, (capBar.Height - capLabel.Height) / 2);
            }

            capBar.Controls.Add(capLabel);
            capBar.Resize += (_, __) => CenterCaption();
            capLabel.SizeChanged += (_, __) => CenterCaption();
            CenterCaption();
            return capBar;
        }

        private static FlowLayoutPanel MakeInfoFrame(string caption, string message, Color accent, Color bodyBg)
        {
            var lbl = new Label
            {
                Text = message,
                AutoSize = false,
                Width = FieldDefaultWidth,
                Height = 56,
                ForeColor = ShowcasePastelTheme.TextMuted,
                Font = new Font("Segoe UI", FieldFontSize)
            };
            return MakeSectionFrame(caption, accent, bodyBg, lbl);
        }

        private static Panel MakeHeader(string title, string subtitle, Color accent, bool rightColumn)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = accent,
                Margin = rightColumn ? new Padding(0) : new Padding(0, 0, 8, 0),
                Padding = new Padding(14, 8, 10, 8)
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = accent
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            inner.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = ShowcasePastelTheme.TextPrimary,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true
            }, 0, 0);
            inner.Controls.Add(new Label
            {
                Text = subtitle,
                Dock = DockStyle.Fill,
                ForeColor = ShowcasePastelTheme.TextSubheader,
                Font = new Font("Segoe UI", 10.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true
            }, 0, 1);
            p.Controls.Add(inner);
            return p;
        }

        private static Button MakeButton(string text, Color back) => new Button
        {
            Text = text,
            Size = new Size(148, 48),
            MinimumSize = new Size(148, 48),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = false
        };
    }
}
