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

    internal sealed class ShowcaseScenePromptEditorForm : Form

    {

        private readonly ShowcaseVideoItem _video;

        private readonly Func<Task> _exportExcelClipAsync;

        private readonly List<ScenePromptFields> _sceneFields = new List<ScenePromptFields>();



        private sealed class ScenePromptFields

        {

            public AiVideoGenInputItem Scene { get; set; }

            public TextBox VeoBox { get; set; }

            public TextBox KlingBox { get; set; }

            public TextBox ZoomBox { get; set; }

        }



        public ShowcaseScenePromptEditorForm(ShowcaseVideoItem video, Func<Task> exportExcelClipAsync = null)

        {

            _video = video ?? throw new ArgumentNullException(nameof(video));

            _exportExcelClipAsync = exportExcelClipAsync;



            Text = "Prompt phân cảnh — " + ((_video.ProductName ?? string.Empty).Trim().Length > 0

                ? _video.ProductName.Trim()

                : "Showcase");

            StartPosition = FormStartPosition.CenterParent;

            FormBorderStyle = FormBorderStyle.Sizable;

            MaximizeBox = true;

            MinimizeBox = false;

            ShowInTaskbar = false;

            BackColor = Color.FromArgb(31, 34, 42);

            ForeColor = Color.Gainsboro;

            Font = new Font("Segoe UI", 10.5F);

            ClientSize = new Size(1840, 960);

            MinimumSize = new Size(1520, 620);

            Padding = new Padding(24);



            var root = new TableLayoutPanel

            {

                Dock = DockStyle.Fill,

                ColumnCount = 1,

                RowCount = 2,

                BackColor = BackColor

            };

            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));



            var scroll = new Panel

            {

                Dock = DockStyle.Fill,

                AutoScroll = true,

                BackColor = BackColor,

                Padding = new Padding(0, 0, 12, 8)

            };



            var stack = new FlowLayoutPanel

            {

                FlowDirection = FlowDirection.TopDown,

                WrapContents = false,

                AutoSize = true,

                AutoSizeMode = AutoSizeMode.GrowAndShrink,

                Dock = DockStyle.Top,

                BackColor = BackColor,

                Width = 1680,

                Padding = new Padding(0, 8, 0, 12)

            };



            void SyncStackWidths()

            {

                var w = Math.Max(400, stack.ClientSize.Width - stack.Padding.Horizontal);

                foreach (Control section in stack.Controls)

                {

                    section.Width = w;

                    if (!(section is FlowLayoutPanel flp))

                    {

                        continue;

                    }



                    foreach (Control child in flp.Controls)

                    {

                        if (child is Label lbl)

                        {

                            lbl.MaximumSize = new Size(w, 0);

                        }

                        else if (child is TextBox box)

                        {

                            box.Width = w;

                        }

                    }

                }

            }



            scroll.Resize += (_, __) =>

            {

                stack.Width = Math.Max(1240, scroll.ClientSize.Width - 24);

                SyncStackWidths();

            };



            void AddSectionRow(string title)

            {

                stack.Controls.Add(new Label

                {

                    Text = title,

                    AutoSize = true,

                    ForeColor = Color.FromArgb(160, 168, 182),

                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),

                    Margin = new Padding(0, 8, 0, 20)

                });

            }



            TextBox MakeBox(string text) => new TextBox

            {

                Text = text ?? string.Empty,

                Multiline = true,

                WordWrap = true,

                AcceptsReturn = true,

                ScrollBars = ScrollBars.Vertical,

                BackColor = Color.FromArgb(45, 49, 60),

                ForeColor = Color.WhiteSmoke,

                BorderStyle = BorderStyle.FixedSingle,

                Font = new Font("Consolas", 10F),

                Margin = new Padding(0)

            };



            void AddLabeledField(FlowLayoutPanel section, string title, TextBox field, int fieldHeight, bool visible)

            {

                fieldHeight += 24;

                var lbl = new Label

                {

                    Text = title,

                    AutoSize = true,

                    ForeColor = Color.FromArgb(160, 168, 182),

                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),

                    Margin = new Padding(0, 0, 0, 10),

                    Visible = visible

                };

                field.Height = fieldHeight;

                field.Margin = new Padding(0, 0, 0, 2);

                field.Width = 400;

                field.Visible = visible;

                section.Controls.Add(lbl);

                section.Controls.Add(field);

            }



            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();

            AddSectionRow("Prompt clip cho từng cảnh — " + scenes.Count + " cảnh (theo cột «Công cụ Video»)");



            if (scenes.Count == 0)

            {

                stack.Controls.Add(new Label

                {

                    Text = "Chưa có cảnh trên storyboard — thêm ảnh trước.",

                    AutoSize = false,

                    Height = 36,

                    TextAlign = ContentAlignment.TopLeft,

                    ForeColor = Color.FromArgb(140, 148, 162),

                    Font = new Font("Segoe UI", 10F),

                    Margin = new Padding(0, 0, 0, 16)

                });

            }



            for (var i = 0; i < scenes.Count; i++)

            {

                var scene = scenes[i];

                var role = string.IsNullOrWhiteSpace(scene.SceneRole) ? "?" : scene.SceneRole;

                var title = string.IsNullOrWhiteSpace(scene.SceneTitle) ? "Cảnh " + (i + 1) : scene.SceneTitle;

                var tool = ShowcaseClipToolHelper.NormalizeClipTool(scene.ShowcaseClipTool);

                if (string.IsNullOrEmpty(tool))

                {

                    tool = ShowcaseClipToolHelper.ResolveDefaultTool(_video.ShowcaseClipModeId, scene.ShowcaseImageKind);

                }



                var section = new FlowLayoutPanel

                {

                    FlowDirection = FlowDirection.TopDown,

                    WrapContents = false,

                    AutoSize = true,

                    AutoSizeMode = AutoSizeMode.GrowAndShrink,

                    BackColor = BackColor,

                    Margin = new Padding(0, 12, 0, 28)

                };



                section.Controls.Add(new Label

                {

                    Text = (i + 1) + ". [" + role + "] " + title + " — " +

                           ShowcaseClipToolHelper.GetImageKindDisplayLabel(scene.ShowcaseImageKind) + " · " +

                           ShowcaseClipToolHelper.GetToolDisplayLabel(tool),

                    AutoSize = true,

                    ForeColor = Color.FromArgb(200, 204, 214),

                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),

                    Margin = new Padding(0, 0, 0, 8)

                });



                var veoBox = MakeBox(scene.VeoPrompt);

                var klingBox = MakeBox(scene.KlingPrompt);

                var zoomBox = MakeBox(scene.ZoomHint);

                var veoLabel = string.Equals(
                        ShowcaseClipToolHelper.NormalizeImageKind(scene.ShowcaseImageKind),
                        ShowcaseClipToolHelper.KindFlatlay,
                        StringComparison.Ordinal)
                    ? "Prompt Veo flatlay — mô tả đúng ảnh; tay OK (no face), camera/touch only, no restyling"
                    : "Prompt Veo (Flow I2V)";

                AddLabeledField(section, veoLabel, veoBox, 120,

                    tool == ShowcaseClipToolHelper.ToolVeo || tool == ShowcaseClipToolHelper.ToolKling);

                AddLabeledField(section, "Prompt Kling (I2V on-model)", klingBox, 120, tool == ShowcaseClipToolHelper.ToolKling);

                AddLabeledField(section, "Zoom gợi ý (Ken Burns trong app)", zoomBox, 72, tool == ShowcaseClipToolHelper.ToolZoom);



                _sceneFields.Add(new ScenePromptFields

                {

                    Scene = scene,

                    VeoBox = veoBox,

                    KlingBox = klingBox,

                    ZoomBox = zoomBox

                });

                stack.Controls.Add(section);

            }



            scroll.Controls.Add(stack);

            SyncStackWidths();

            root.Controls.Add(scroll, 0, 0);



            var btnOk = CreateButton("OK", Color.FromArgb(56, 120, 82));

            btnOk.DialogResult = DialogResult.OK;

            AcceptButton = btnOk;

            var btnCancel = CreateButton("Hủy", Color.FromArgb(90, 96, 110));

            btnCancel.DialogResult = DialogResult.Cancel;

            CancelButton = btnCancel;



            var bottomBar = new TableLayoutPanel

            {

                Dock = DockStyle.Fill,

                ColumnCount = 2,

                RowCount = 1,

                BackColor = BackColor,

                Margin = new Padding(0, 8, 0, 8)

            };

            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));



            var flpLeft = new FlowLayoutPanel

            {

                Dock = DockStyle.Fill,

                FlowDirection = FlowDirection.LeftToRight,

                WrapContents = false,

                BackColor = BackColor,

                Padding = new Padding(0, 4, 0, 4)

            };

            if (_exportExcelClipAsync != null)

            {

                var btnExportExcel = CreateButton("Tải excel prompt", ShowcasePastelTheme.ButtonExcel);

                btnExportExcel.Click += async (_, __) =>

                {

                    if (!ValidateAndSave())

                    {

                        return;

                    }

                    btnExportExcel.Enabled = false;

                    try

                    {

                        await _exportExcelClipAsync().ConfigureAwait(true);

                    }

                    finally

                    {

                        if (!btnExportExcel.IsDisposed)

                        {

                            btnExportExcel.Enabled = true;

                        }

                    }

                };

                flpLeft.Controls.Add(btnExportExcel);

            }



            var flpButtons = new FlowLayoutPanel

            {

                Dock = DockStyle.Fill,

                FlowDirection = FlowDirection.RightToLeft,

                WrapContents = false,

                BackColor = BackColor,

                Padding = new Padding(0, 4, 0, 4)

            };

            flpButtons.Controls.Add(btnCancel);

            flpButtons.Controls.Add(btnOk);

            bottomBar.Controls.Add(flpLeft, 0, 0);

            bottomBar.Controls.Add(flpButtons, 1, 0);

            root.Controls.Add(bottomBar, 0, 1);



            Controls.Add(root);



            Load += (_, __) =>

            {

                SyncStackWidths();

                stack.PerformLayout();

            };



            btnOk.Click += (_, __) =>

            {

                if (!ValidateAndSave())

                {

                    DialogResult = DialogResult.None;

                }

            };

        }



        private bool ValidateAndSave()

        {

            for (var i = 0; i < _sceneFields.Count; i++)

            {

                var entry = _sceneFields[i];

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

                scene.ZoomHint = entry.ZoomBox.Text?.Trim() ?? string.Empty;

            }



            ShowcaseContentDisplayHelper.RefreshContentLabels(_video);

            return true;

        }



        private static Button CreateButton(string text, Color back) => new Button

        {

            Text = text,

            AutoSize = true,

            MinimumSize = new Size(118, 36),

            FlatStyle = FlatStyle.Flat,

            BackColor = back,

            ForeColor = Color.White,

            Font = new Font("Segoe UI", 10.5F),

            Margin = new Padding(8, 0, 0, 0),

            Padding = new Padding(8, 4, 8, 4)

        };

    }

}


