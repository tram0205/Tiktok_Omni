using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Hộp thoại xem toàn bộ kịch bản phân cảnh + prompt của một dòng Triết lý.</summary>
    internal sealed class PhilosophyScenePromptViewerForm : Form
    {
        private readonly PhilosophyScriptItem _item;

        public PhilosophyScenePromptViewerForm(PhilosophyScriptItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));

            var preview = TrimPreview(_item.Content);
            Text = "Prompt video — " + (string.IsNullOrEmpty(preview) ? "Video Quote" : preview);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(640, 360);
            ClientSize = new Size(820, 420);

            BuildUi();
        }

        private void BuildUi()
        {
            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(8, 8, 8, 4),
                ForeColor = Color.FromArgb(160, 168, 182),
                Text = BuildHeaderText()
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(36, 39, 48),
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                DefaultCellStyle =
                {
                    BackColor = Color.FromArgb(36, 39, 48),
                    ForeColor = Color.Gainsboro,
                    SelectionBackColor = Color.FromArgb(76, 110, 245),
                    SelectionForeColor = Color.White,
                    WrapMode = DataGridViewTriState.True
                },
                ColumnHeadersDefaultCellStyle =
                {
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = Padding.Empty
                }
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colScene",
                HeaderText = "Phân cảnh",
                FillWeight = 28,
                MinimumWidth = 120
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPrompt",
                HeaderText = "Prompt Hình Ảnh AI",
                FillWeight = 50,
                MinimumWidth = 220,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFile",
                HeaderText = "Tên File Video Quy Ước",
                FillWeight = 18,
                MinimumWidth = 100
            });

            var scenes = _item.Scenes ?? new System.Collections.Generic.List<PhilosophySceneItem>();
            if (scenes.Count == 0 && !string.IsNullOrWhiteSpace(_item.Content))
            {
                scenes = PhilosophySceneHelper.SplitIntoScenes(_item.Content);
            }

            foreach (var scene in scenes.OrderBy(s => s.SceneIndex))
            {
                grid.Rows.Add(
                    scene.SceneText ?? string.Empty,
                    scene.ImagePromptEn ?? string.Empty,
                    scene.ConventionFileName ?? string.Empty);
            }

            AppGridSttColumn.EnsureFirstColumn(grid, compact: true);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                BackColor = BackColor
            };

            var btnClose = new Button
            {
                Text = "Đóng",
                Width = 88,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(88, 94, 112),
                ForeColor = Color.White
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => Close();

            footer.Controls.Add(btnClose);
            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private string BuildHeaderText()
        {
            var quote = (_item.Content ?? string.Empty).Trim();
            var image = (_item.BRollFolder ?? string.Empty).Trim();
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(quote))
            {
                parts.Add("Quote: " + TrimPreview(quote, 80));
            }

            if (PhilosophyVisualModes.Normalize(_item.VisualMode) == PhilosophyVisualModes.PreRendered
                && !string.IsNullOrEmpty(image)
                && System.IO.File.Exists(image))
            {
                parts.Add("Ảnh nền: " + System.IO.Path.GetFileName(image));
            }

            var sceneCount = _item.Scenes?.Count ?? 0;
            parts.Add(sceneCount > 0 ? sceneCount + " phân cảnh" : "Chưa có prompt — bấm «Tạo Prompt Phân Cảnh»");
            return string.Join("  |  ", parts);
        }

        private static string TrimPreview(string text, int maxLen = 48)
        {
            var t = (text ?? string.Empty).Replace("\r\n", " ").Trim();
            return t.Length <= maxLen ? t : t.Substring(0, maxLen) + "…";
        }
    }
}
