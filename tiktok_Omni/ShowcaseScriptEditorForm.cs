using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseScriptEditorForm : Form
    {
        private readonly ShowcaseVideoItem _video;
        private readonly TextBox _txtTheme;
        private readonly TextBox _txtHook;
        private readonly TextBox _txtCta;
        private readonly List<TextBox> _sceneVoiceBoxes = new List<TextBox>();

        public ShowcaseScriptEditorForm(ShowcaseVideoItem video)
        {
            _video = video ?? throw new ArgumentNullException(nameof(video));

            Text = "Kịch bản — " + ((_video.ProductName ?? string.Empty).Trim().Length > 0
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
            ClientSize = new Size(1380, 740);
            MinimumSize = new Size(1140, 580);
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
                Width = 1260,
                Padding = new Padding(0, 8, 0, 12)
            };

            void SyncStackWidths()
            {
                var w = Math.Max(600, stack.ClientSize.Width - stack.Padding.Horizontal);
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
                stack.Width = Math.Max(930, scroll.ClientSize.Width - 24);
                SyncStackWidths();
            };

            TextBox MkBox(string text, bool multiline = false) => new TextBox
            {
                Text = text ?? string.Empty,
                Multiline = multiline,
                WordWrap = multiline,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5F)
            };

            void AddLabeledField(string title, TextBox field, int fieldHeight, bool multiline = false)
            {
                fieldHeight += multiline ? 24 : 8;

                var section = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = BackColor,
                    Margin = new Padding(0, 12, 0, 28)
                };

                var lbl = new Label
                {
                    Text = title,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(160, 168, 182),
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    Margin = new Padding(0, 0, 0, 10)
                };

                field.Height = fieldHeight;
                field.Margin = new Padding(0, 0, 0, 2);
                field.Width = 600;

                section.Controls.Add(lbl);
                section.Controls.Add(field);
                stack.Controls.Add(section);
            }

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

            AddLabeledField("Chủ đề video", _txtTheme = MkBox(_video.ShowcaseTheme), 34);

            AddLabeledField("Hook mở đầu", _txtHook = MkBox(_video.ShowcaseHookText, multiline: true), 76, multiline: true);

            AddLabeledField("CTA kết thúc", _txtCta = MkBox(_video.ShowcaseCtaText, multiline: true), 76, multiline: true);

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            AddSectionRow("Voiceover từng cảnh (" + scenes.Count + ")");

            if (scenes.Count == 0)
            {
                stack.Controls.Add(new Label
                {
                    Text = "Chưa có cảnh — thêm ảnh trên storyboard trước.",
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
                var box = MkBox(scene.SceneVoiceover, multiline: true);
                _sceneVoiceBoxes.Add(box);
                AddLabeledField((i + 1) + ". [" + role + "] " + title, box, 112, multiline: true);
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

            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 8, 0, 8),
                Padding = new Padding(0, 4, 0, 4)
            };
            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            root.Controls.Add(flpButtons, 0, 1);

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
            _video.ShowcaseTheme = _txtTheme.Text?.Trim() ?? string.Empty;
            _video.ShowcaseHookText = _txtHook.Text?.Trim() ?? string.Empty;
            _video.ShowcaseCtaText = _txtCta.Text?.Trim() ?? string.Empty;

            var scenes = _video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            for (var i = 0; i < scenes.Count && i < _sceneVoiceBoxes.Count; i++)
            {
                scenes[i].SceneVoiceover = _sceneVoiceBoxes[i].Text?.Trim() ?? string.Empty;
                scenes[i].ShowcaseTheme = _video.ShowcaseTheme;
            }

            _video.ApplySettingsToScenes();
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
