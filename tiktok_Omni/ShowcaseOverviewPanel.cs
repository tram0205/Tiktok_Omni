using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Nội dung dialog «Tổng quan» — pipeline từng bước luôn hiện trong bảng.</summary>
    internal sealed class ShowcaseOverviewPanel : UserControl
    {
        public ShowcaseOverviewPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(31, 34, 42);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.None;
        }

        public void ApplySnapshot(ShowcaseRenderOverviewSnapshot snapshot)
        {
            snapshot = snapshot ?? new ShowcaseRenderOverviewSnapshot();
            Controls.Clear();

            var header = CreateHeaderBlock(snapshot);
            var grid = CreateOverviewTable(snapshot);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            header.Dock = DockStyle.Fill;
            grid.Dock = DockStyle.Fill;

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(grid, 0, 1);
            Controls.Add(root);
        }

        private static Control CreateHeaderBlock(ShowcaseRenderOverviewSnapshot snapshot)
        {
            var pnl = new Panel
            {
                AutoSize = true,
                BackColor = Color.FromArgb(38, 42, 52),
                Padding = new Padding(16, 12, 16, 10)
            };

            var lblTitle = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2400, 0),
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(245, 247, 250),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Text = snapshot.ProductTitle ?? "Showcase"
            };

            var lblMeta = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2400, 0),
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(180, 190, 205),
                Margin = new Padding(0, 4, 0, 0),
                Text = "Chủ đề: " + snapshot.Theme + "  ·  " + snapshot.AspectLabel
            };

            var heroText = snapshot.RenderReady
                ? snapshot.HeroSummary
                : BuildRenderStatusLine(snapshot);
            var lblHero = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(2400, 0),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 6, 0, 0),
                ForeColor = snapshot.RenderReady ? Color.FromArgb(120, 220, 160) : Color.FromArgb(255, 190, 120),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Text = heroText
            };

            pnl.Controls.Add(lblHero);
            pnl.Controls.Add(lblMeta);
            pnl.Controls.Add(lblTitle);
            return pnl;
        }

        private Control CreateOverviewTable(ShowcaseRenderOverviewSnapshot snapshot)
        {
            var backColor = Color.FromArgb(24, 26, 32);
            var au = snapshot.AudioSubtitle ?? new ShowcaseOverviewAudioSubtitlePanel();
            var cards = snapshot.SceneCards?.ToList() ?? new List<ShowcaseOverviewSceneCard>();
            var steps = snapshot.Pipeline?.ToList() ?? new List<ShowcaseOverviewPipelineStep>();

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                RowHeadersVisible = false,
                BackgroundColor = backColor,
                GridColor = Color.FromArgb(55, 60, 72),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(36, 40, 50),
                    ForeColor = Color.FromArgb(235, 238, 245),
                    SelectionBackColor = Color.FromArgb(55, 75, 110),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 9.25F),
                    WrapMode = DataGridViewTriState.True,
                    Padding = new Padding(10, 8, 10, 8)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 50, 62),
                    ForeColor = Color.FromArgb(235, 238, 245),
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True
                }
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStt",
                HeaderText = "STT",
                Width = 72,
                MinimumWidth = 56,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStep",
                HeaderText = "Bước / Cảnh",
                MinimumWidth = 200,
                FillWeight = 22F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDetail",
                HeaderText = "Chi tiết",
                MinimumWidth = 180,
                FillWeight = 34F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Trạng thái",
                MinimumWidth = 120,
                FillWeight = 14F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgv.RowTemplate.MinimumHeight = 44;

            // ── Hướng dẫn từng bước (luôn hiện đầu bảng) ──
            var guideHeader = dgv.Rows.Add("—", "▸ Hướng dẫn từng bước", "Làm theo thứ tự trái → phải", "");
            StyleSectionHeaderRow(dgv.Rows[guideHeader]);

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var rowIndex = dgv.Rows.Add(
                    (i + 1).ToString(),
                    step.Title ?? string.Empty,
                    step.Detail ?? string.Empty,
                    FormatStepStatusLabel(step.Status));
                StylePipelineRow(dgv.Rows[rowIndex], step.Status);
            }

            if (snapshot.RenderBlockers != null && snapshot.RenderBlockers.Count > 0 && !snapshot.RenderReady)
            {
                foreach (var blocker in snapshot.RenderBlockers)
                {
                    var rowIndex = dgv.Rows.Add(
                        "!",
                        "Cần trước Render",
                        StripBlockerProductPrefix(blocker, snapshot.ProductTitle),
                        "✗");
                    StylePipelineRow(dgv.Rows[rowIndex], ShowcaseOverviewStepStatus.Missing);
                }
            }

            // ── Chi tiết từng cảnh ──
            var sceneHeader = dgv.Rows.Add("—", "▸ Chi tiết từng cảnh", "Ảnh · clip · thoại · audio", "");
            StyleSectionHeaderRow(dgv.Rows[sceneHeader]);

            var narrationLine = au.HasNarration
                ? "✓ " + au.NarrationLabel
                : "✗ " + au.NarrationLabel;
            var voiceLine = au.VoiceAligned ? "✓ Khớp clip" : "✗ Chưa khớp clip";
            var audioSummary = narrationLine + " · " + voiceLine + " · Nhạc: " + au.MusicLabel;

            if (cards.Count == 0)
            {
                var rowIndex = dgv.Rows.Add("—", "Storyboard", "Chưa có cảnh — thêm ảnh cột «Ảnh»", "✗");
                StyleSceneRow(dgv.Rows[rowIndex]);
            }
            else
            {
                for (var i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    var clip = card.HasClip ? "✓ Clip" : "✗ Thiếu clip";
                    var prompt = card.HasPrompt ? "✓ Prompt" : "✗ Thiếu prompt";
                    var tool = string.IsNullOrWhiteSpace(card.ClipToolLabel) ? "—" : card.ClipToolLabel.Trim();
                    var detail = "Công cụ: " + tool + Environment.NewLine
                                 + clip + " · " + prompt + Environment.NewLine
                                 + "Thoại: "
                                 + (string.IsNullOrWhiteSpace(card.VoiceText) ? card.VoicePreview : card.VoiceText);
                    if (!string.IsNullOrWhiteSpace(card.SubtitleDisplayText) && card.SubtitleDisplayText != "—")
                    {
                        detail += Environment.NewLine + "Phụ đề: " + card.SubtitleDisplayText;
                    }

                    var status = card.HasClip && card.HasPrompt ? "✓" : card.HasClip || card.HasPrompt ? "◐" : "✗";
                    var rowIndex = dgv.Rows.Add(card.SceneIndex.ToString(), "Cảnh " + card.SceneIndex, detail, status);
                    StyleSceneRow(dgv.Rows[rowIndex]);
                    if (i == 0)
                    {
                        dgv.Rows[rowIndex].Cells["colDetail"].Value = detail + Environment.NewLine + "Audio: " + audioSummary;
                    }
                }
            }

            dgv.ClearSelection();
            return dgv;
        }

        private static void StyleSectionHeaderRow(DataGridViewRow row)
        {
            if (row == null)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = Color.FromArgb(52, 58, 72);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(200, 210, 225);
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
            row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
        }

        private static void StylePipelineRow(DataGridViewRow row, ShowcaseOverviewStepStatus status)
        {
            if (row == null)
            {
                return;
            }

            Color bg;
            switch (status)
            {
                case ShowcaseOverviewStepStatus.Complete:
                    bg = Color.FromArgb(32, 58, 44);
                    break;
                case ShowcaseOverviewStepStatus.Partial:
                    bg = Color.FromArgb(58, 48, 28);
                    break;
                default:
                    bg = Color.FromArgb(52, 34, 34);
                    break;
            }

            row.DefaultCellStyle.BackColor = bg;
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 75, 110);
            row.Cells["colStep"].Style.Font = new Font("Segoe UI", 9.25F, FontStyle.Bold);
        }

        private static void StyleSceneRow(DataGridViewRow row)
        {
            if (row == null)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = Color.FromArgb(36, 40, 50);
        }

        private static string FormatStepStatusLabel(ShowcaseOverviewStepStatus status)
        {
            switch (status)
            {
                case ShowcaseOverviewStepStatus.Complete:
                    return "✓ Xong";
                case ShowcaseOverviewStepStatus.Partial:
                    return "◐ Một phần";
                default:
                    return "✗ Thiếu";
            }
        }

        private static string BuildRenderStatusLine(ShowcaseRenderOverviewSnapshot snapshot)
        {
            if (snapshot.RenderReady)
            {
                return "✓ Đủ điều kiện Render — bấm «▶ Render video» trên tab Showcase.";
            }

            var blockers = snapshot.RenderBlockers;
            if (blockers == null || blockers.Count == 0)
            {
                return "⚠ Chưa đủ điều kiện Render.";
            }

            return "⚠ Còn thiếu — xem bảng «Hướng dẫn từng bước» bên dưới.";
        }

        private static string StripBlockerProductPrefix(string blocker, string productTitle)
        {
            var text = (blocker ?? string.Empty).Trim();
            var name = (productTitle ?? string.Empty).Trim();
            if (text.Length == 0 || name.Length == 0)
            {
                return text;
            }

            var quoted = "«" + name + "»";
            if (!text.StartsWith(quoted, StringComparison.Ordinal))
            {
                return text;
            }

            text = text.Substring(quoted.Length).TrimStart();
            if (text.StartsWith("—", StringComparison.Ordinal) || text.StartsWith("-", StringComparison.Ordinal))
            {
                text = text.Substring(1).TrimStart();
            }

            return text.Length == 0 ? blocker.Trim() : text;
        }
    }
}
