using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseClipAspectFitDialog : Form
    {
        private sealed class SceneRowControls
        {
            public ShowcaseClipAspectMismatchEntry Entry { get; set; }

            public RadioButton RbCrop { get; set; }

            public RadioButton RbBlur { get; set; }
        }

        private readonly List<SceneRowControls> _rows = new List<SceneRowControls>();
        private readonly FlowLayoutPanel _rowsPanel;

        public ShowcaseClipAspectFitDialog(
            string productName,
            ShowcaseOutputAspectPreset canvas,
            IReadOnlyList<ShowcaseClipAspectMismatchEntry> mismatches)
        {
            canvas = canvas ?? ShowcaseOutputAspectPresets.Vertical9x16;
            mismatches = mismatches ?? Array.Empty<ShowcaseClipAspectMismatchEntry>();

            var product = (productName ?? string.Empty).Trim();
            Text = "Clip lệch khung video";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ClientSize = new Size(1320, 760);
            MinimumSize = new Size(1080, 620);
            BackColor = Color.FromArgb(28, 31, 38);
            ForeColor = Color.FromArgb(210, 215, 225);
            Padding = new Padding(28, 20, 28, 16);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
                MaximumSize = new Size(1240, 0),
                Text = product.Length > 0
                    ? "«" + product + "» — " + mismatches.Count + " clip không khớp khung "
                      + canvas.DisplayLabel + " (" + canvas.Width + "×" + canvas.Height + ")"
                    : mismatches.Count + " clip không khớp khung "
                      + canvas.DisplayLabel + " (" + canvas.Width + "×" + canvas.Height + ")"
            };

            var hint = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(1240, 0),
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 8, 0, 6),
                Text = "Chọn cách lấp khung cho từng cảnh trước khi Render. "
                         + "Cắt (cover) = phóng to kín khung, có thể mất mép. "
                         + "Blur nền = giữ toàn bộ clip ở giữa, viền là nền mờ."
            };

            var bulkRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            bulkRow.Controls.Add(MkBulkButton("Tất cả: Cắt (cover)", () => ApplyAll(ShowcaseZoomAspectFitMode.Crop)));
            bulkRow.Controls.Add(MkBulkButton("Tất cả: Blur nền", () => ApplyAll(ShowcaseZoomAspectFitMode.BlurPad)));

            var topPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 4)
            };
            topPanel.Controls.Add(hint);
            topPanel.Controls.Add(bulkRow);

            _rowsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(24, 27, 34),
                Padding = new Padding(8, 8, 8, 8)
            };

            foreach (var entry in mismatches)
            {
                _rowsPanel.Controls.Add(BuildSceneRow(entry));
            }

            var btnOk = new Button
            {
                Text = "Render với lựa chọn này",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(220, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 120, 82),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 10, 0)
            };
            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(120, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(90, 96, 110),
                ForeColor = Color.White
            };
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            btnOk.Click += (_, __) => CommitSelections();

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            footer.Controls.Add(btnCancel);
            footer.Controls.Add(btnOk);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(topPanel, 0, 1);
            layout.Controls.Add(_rowsPanel, 0, 2);
            layout.Controls.Add(footer, 0, 3);
            Controls.Add(layout);
        }

        private Control BuildSceneRow(ShowcaseClipAspectMismatchEntry entry)
        {
            var card = new Panel
            {
                Width = Math.Max(980, _rowsPanel?.ClientSize.Width - 36 ?? 980),
                Height = 168,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.FromArgb(34, 38, 48),
                Padding = new Padding(12)
            };

            var thumb = new PictureBox
            {
                Width = 120,
                Height = 120,
                Left = 12,
                Top = 12,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(18, 20, 26),
                BorderStyle = BorderStyle.FixedSingle
            };
            TryLoadThumb(thumb, entry.ThumbnailPath);

            var info = new Label
            {
                Left = 148,
                Top = 12,
                Width = 520,
                Height = 72,
                ForeColor = Color.FromArgb(210, 215, 225),
                Font = new Font("Segoe UI", 10F),
                Text = "Cảnh " + entry.SceneOrderOneBased + " · " + entry.ClipFileName + "\r\n"
                       + "Clip: " + ShowcaseZoomAspectFitHelper.FormatAspectRatio(entry.ClipWidth, entry.ClipHeight)
            };

            var rbCrop = new RadioButton
            {
                Left = 148,
                Top = 92,
                AutoSize = true,
                Text = "Cắt (cover)",
                ForeColor = Color.FromArgb(210, 215, 225),
                Checked = entry.SelectedMode != ShowcaseZoomAspectFitMode.BlurPad
            };
            var rbBlur = new RadioButton
            {
                Left = 300,
                Top = 92,
                AutoSize = true,
                Text = "Blur nền",
                ForeColor = Color.FromArgb(210, 215, 225),
                Checked = entry.SelectedMode == ShowcaseZoomAspectFitMode.BlurPad
            };

            _rows.Add(new SceneRowControls
            {
                Entry = entry,
                RbCrop = rbCrop,
                RbBlur = rbBlur
            });

            card.Controls.Add(thumb);
            card.Controls.Add(info);
            card.Controls.Add(rbCrop);
            card.Controls.Add(rbBlur);
            return card;
        }

        private static void TryLoadThumb(PictureBox box, string path)
        {
            if (box == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                using (var img = Image.FromFile(path))
                {
                    box.Image = new Bitmap(img);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static Button MkBulkButton(string text, Action action)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 72, 88),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, __) => action?.Invoke();
            return btn;
        }

        private void ApplyAll(ShowcaseZoomAspectFitMode mode)
        {
            foreach (var row in _rows)
            {
                if (row?.RbCrop == null || row.RbBlur == null)
                {
                    continue;
                }

                row.RbCrop.Checked = mode == ShowcaseZoomAspectFitMode.Crop;
                row.RbBlur.Checked = mode == ShowcaseZoomAspectFitMode.BlurPad;
            }
        }

        private void CommitSelections()
        {
            foreach (var row in _rows)
            {
                if (row?.Entry?.Scene == null)
                {
                    continue;
                }

                var mode = row.RbBlur != null && row.RbBlur.Checked
                    ? ShowcaseZoomAspectFitMode.BlurPad
                    : ShowcaseZoomAspectFitMode.Crop;
                row.Entry.SelectedMode = mode;
                row.Entry.Scene.ShowcaseClipAspectFitMode = mode;
            }
        }
    }
}
