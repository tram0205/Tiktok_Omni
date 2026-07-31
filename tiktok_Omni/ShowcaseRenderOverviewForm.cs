using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseRenderOverviewForm : Form
    {
        private const int Pad = 20;
        private const int OverviewGridCellPad = 10;
        private const int SceneCellTextGap = 20;
        private const int SceneCellThumbMinWidth = 52;
        private const int SceneCellThumbMaxWidth = 180;
        private const float PipelineRowHeight = 185F;
        private const int PipelineChipHeight = 134;
        private const float PipelineChipTitleRowHeight = 44F;
        private const int PipelineChipWidth = 237;
        private const int ContentMaxWidth = 2640;
        private readonly List<Image> _loadedImages = new List<Image>();

        public ShowcaseRenderOverviewForm(ShowcaseRenderOverviewSnapshot snapshot)
        {
            snapshot = snapshot ?? new ShowcaseRenderOverviewSnapshot();
            Text = "Tổng quan trước Render";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(2200, 1170);
            ClientSize = new Size(2800, 1380);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(Pad, Pad, Pad, Pad);

            var pnlBottom = CreateBottomPanel();
            var header = CreateHeaderBlock(snapshot);
            var pipeline = CreatePipelineBlock(snapshot);
            var mainBody = CreateMainOverviewBody(snapshot);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, PipelineRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

            header.Dock = DockStyle.Fill;
            pipeline.Dock = DockStyle.Fill;
            mainBody.Dock = DockStyle.Fill;
            pnlBottom.Dock = DockStyle.Fill;

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(pipeline, 0, 1);
            root.Controls.Add(mainBody, 0, 2);
            root.Controls.Add(pnlBottom, 0, 3);
            Controls.Add(root);

            FormClosed += (_, __) =>
            {
                foreach (var img in _loadedImages)
                {
                    img?.Dispose();
                }

                _loadedImages.Clear();
            };

            AcceptButton = pnlBottom.Controls.OfType<Button>().FirstOrDefault();
        }

        private Control CreateHeaderBlock(ShowcaseRenderOverviewSnapshot snapshot)
        {
            var pnl = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(38, 42, 52),
                Padding = new Padding(16, 12, 16, 10)
            };
            pnl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(ContentMaxWidth, 0),
                ForeColor = Color.FromArgb(245, 247, 250),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Text = snapshot.ProductTitle
            };

            var lblMeta = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(ContentMaxWidth, 0),
                ForeColor = Color.FromArgb(180, 190, 205),
                Margin = new Padding(0, 4, 0, 0),
                Text = "Chủ đề: " + snapshot.Theme + "  ·  " + snapshot.AspectLabel
            };

            var lblHero = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(ContentMaxWidth, 0),
                Margin = new Padding(0, 6, 0, 0),
                ForeColor = snapshot.RenderReady ? Color.FromArgb(120, 220, 160) : Color.FromArgb(255, 190, 120),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Text = snapshot.RenderReady ? snapshot.HeroSummary : BuildRenderStatusLine(snapshot)
            };

            pnl.Controls.Add(lblTitle, 0, 0);
            pnl.Controls.Add(lblMeta, 0, 1);
            pnl.Controls.Add(lblHero, 0, 2);
            return pnl;
        }

        private Control CreatePipelineBlock(ShowcaseRenderOverviewSnapshot snapshot)
        {
            var outer = new Panel
            {
                BackColor = BackColor,
                Padding = new Padding(0, 4, 0, 6),
                MinimumSize = new Size(0, (int)PipelineRowHeight - 6)
            };
            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = BackColor,
                Padding = new Padding(0, 6, 0, 6)
            };

            var steps = snapshot.Pipeline ?? new List<ShowcaseOverviewPipelineStep>();
            for (var i = 0; i < steps.Count; i++)
            {
                flp.Controls.Add(CreatePipelineChip(steps[i]));
                if (i < steps.Count - 1)
                {
                    flp.Controls.Add(CreateArrowLabel());
                }
            }

            outer.Controls.Add(flp);
            return outer;
        }

        private Control CreateMainOverviewBody(ShowcaseRenderOverviewSnapshot snapshot)
        {
            var backColor = Color.FromArgb(24, 26, 32);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = backColor,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            layout.Controls.Add(CreateOverviewSceneGrid(snapshot, backColor), 0, 0);
            return layout;
        }

        private Control CreateOverviewSceneGrid(ShowcaseRenderOverviewSnapshot snapshot, Color backColor)
        {
            var au = snapshot.AudioSubtitle ?? new ShowcaseOverviewAudioSubtitlePanel();
            var cards = snapshot.SceneCards?.ToList() ?? new List<ShowcaseOverviewSceneCard>();
            var sceneCount = cards.Count;

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
                    Padding = new Padding(OverviewGridCellPad, OverviewGridCellPad - 2, OverviewGridCellPad, OverviewGridCellPad - 2)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(45, 50, 62),
                    ForeColor = Color.FromArgb(235, 238, 245),
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True,
                    Padding = new Padding(OverviewGridCellPad, 6, OverviewGridCellPad, 6)
                }
            };

            var overviewHeaderFont = dgv.ColumnHeadersDefaultCellStyle.Font;

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStt",
                HeaderText = "STT",
                Width = 96,
                MinimumWidth = 80,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colScene",
                HeaderText = "Cảnh",
                Width = 400,
                MinimumWidth = 320
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colVoice",
                HeaderText = "Thoại",
                MinimumWidth = 102,
                FillWeight = 18.36F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSubtitle",
                HeaderText = "Phụ đề",
                MinimumWidth = 140,
                FillWeight = 16F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNarrationAudio",
                HeaderText = "Audio thoại",
                MinimumWidth = 253,
                FillWeight = 15.4F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBedAudio",
                HeaderText = "Audio đệm",
                MinimumWidth = 220,
                FillWeight = 15.4F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTransitionFx",
                HeaderText = "Hiệu ứng chuyển",
                MinimumWidth = 49,
                FillWeight = 7F,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                col.HeaderCell.Style.Font = overviewHeaderFont;
                col.HeaderCell.Style.ForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
                col.HeaderCell.Style.BackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
            }

            var sceneColumnIndex = dgv.Columns["colScene"].Index;
            var statusPaintColumns = new HashSet<int>
            {
                dgv.Columns["colNarrationAudio"].Index,
                dgv.Columns["colBedAudio"].Index
            };
            dgv.CellPainting += (_, e) =>
            {
                PaintSceneComboCell(dgv, e, sceneColumnIndex);
                if (e.RowIndex >= 0 && statusPaintColumns.Contains(e.ColumnIndex))
                {
                    PaintOverviewStatusCell(dgv, e);
                }
            };

            var narrationAudioCell = FormatOverviewNarrationAudioCell(au);
            var bedAudioCell = FormatOverviewBedAudioCell(au);
            var transitionFxCell = FormatOverviewTransitionCell(au);

            dgv.RowTemplate.MinimumHeight = 132;

            if (cards.Count == 0)
            {
                var rowIndex = dgv.Rows.Add(
                    "—",
                    "Chưa có cảnh",
                    "—",
                    "—",
                    narrationAudioCell,
                    bedAudioCell,
                    transitionFxCell);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colScene"]);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colNarrationAudio"]);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colBedAudio"]);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colTransitionFx"]);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colVoice"]);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colSubtitle"]);
                ApplyPlainTextColor(dgv.Rows[rowIndex].Cells["colStt"]);
            }
            else
            {
                foreach (var card in cards)
                {
                    var rowIndex = dgv.Rows.Add(
                        card.SceneIndex.ToString(),
                        FormatSceneCheckCell(card),
                        string.IsNullOrWhiteSpace(card.VoiceText) ? card.VoicePreview : card.VoiceText,
                        string.IsNullOrWhiteSpace(card.SubtitleDisplayText) ? "—" : card.SubtitleDisplayText,
                        narrationAudioCell,
                        bedAudioCell,
                        transitionFxCell);
                    dgv.Rows[rowIndex].Cells["colScene"].Tag = LoadGridThumbnail(card.ThumbnailPath);
                    StyleOverviewGridRow(dgv.Rows[rowIndex]);
                }
            }

            dgv.ClearSelection();
            return dgv;
        }

        private static void PaintSceneComboCell(DataGridView dgv, DataGridViewCellPaintingEventArgs e, int sceneColumnIndex)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != sceneColumnIndex)
            {
                return;
            }

            e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var img = cell.Tag as Image;
            var text = Convert.ToString(cell.Value) ?? string.Empty;
            var font = cell.Style.Font ?? dgv.Font;
            var pad = OverviewGridCellPad;
            var inner = Rectangle.Inflate(e.CellBounds, -1, -1);
            var thumbRect = ComputeSceneThumbRect(inner, img, pad);

            if (img != null)
            {
                DrawThumbnail(e.Graphics, img, thumbRect);
            }
            else
            {
                using (var brush = new SolidBrush(Color.FromArgb(22, 24, 30)))
                {
                    e.Graphics.FillRectangle(brush, thumbRect);
                }

                using (var pen = new Pen(Color.FromArgb(70, 75, 85)))
                {
                    e.Graphics.DrawRectangle(pen, thumbRect.X, thumbRect.Y, thumbRect.Width - 1, thumbRect.Height - 1);
                }
            }

            var textRect = new Rectangle(
                thumbRect.Right + SceneCellTextGap,
                inner.Y + pad,
                Math.Max(40, inner.Right - thumbRect.Right - SceneCellTextGap - pad),
                inner.Height - pad * 2);
            var selected = e.State.HasFlag(DataGridViewElementStates.Selected);
            DrawOverviewStatusText(e.Graphics, text, textRect, font, selected);

            e.Handled = true;
        }

        private static Rectangle ComputeSceneThumbRect(Rectangle inner, Image img, int pad)
        {
            var thumbH = Math.Max(48, inner.Height - pad * 2);
            var aspect = img != null && img.Height > 0
                ? (float)img.Width / img.Height
                : 9f / 16f;
            var thumbW = (int)Math.Round(thumbH * aspect);
            thumbW = Math.Max(SceneCellThumbMinWidth, Math.Min(SceneCellThumbMaxWidth, thumbW));
            var y = inner.Y + Math.Max(pad, (inner.Height - thumbH) / 2);
            return new Rectangle(inner.X + pad, y, thumbW, thumbH);
        }

        private static void DrawThumbnail(Graphics g, Image img, Rectangle bounds)
        {
            if (img == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var imageAspect = (float)img.Width / img.Height;
            var boundsAspect = (float)bounds.Width / bounds.Height;
            Rectangle dest;
            if (imageAspect > boundsAspect)
            {
                var h = (int)(bounds.Width / imageAspect);
                dest = new Rectangle(bounds.X, bounds.Y + (bounds.Height - h) / 2, bounds.Width, h);
            }
            else
            {
                var w = (int)(bounds.Height * imageAspect);
                dest = new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y, w, bounds.Height);
            }

            g.DrawImage(img, dest);
        }

        private Image LoadGridThumbnail(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                var img = Image.FromFile(path);
                _loadedImages.Add(img);
                return img;
            }
            catch
            {
                return null;
            }
        }

        private static readonly Color OverviewPlainTextColor = Color.FromArgb(235, 238, 245);
        private static readonly Color OverviewCheckOkColor = Color.FromArgb(130, 210, 150);
        private static readonly Color OverviewCheckFailColor = Color.FromArgb(255, 150, 120);

        private static void ApplyPlainTextColor(DataGridViewCell cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.Style.ForeColor = OverviewPlainTextColor;
            cell.Style.SelectionForeColor = Color.White;
        }

        private struct OverviewTextSegment
        {
            public string Text;
            public bool Plain;
            public bool Ok;
        }

        private static void DrawOverviewStatusText(
            Graphics g,
            string text,
            Rectangle bounds,
            Font font,
            bool selected)
        {
            if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            const TextFormatFlags flags =
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;

            var y = bounds.Top;
            foreach (var logicalLine in text.Replace("\r", string.Empty).Split('\n'))
            {
                if (y >= bounds.Bottom)
                {
                    break;
                }

                if (logicalLine.Length == 0)
                {
                    y += font.Height;
                    continue;
                }

                var segments = TokenizeOverviewLine(logicalLine);
                var x = bounds.Left;
                var rowMaxHeight = 0;

                foreach (var segment in segments)
                {
                    var remainder = segment.Text ?? string.Empty;
                    while (remainder.Length > 0 && y < bounds.Bottom)
                    {
                        var maxWidth = bounds.Right - x;
                        if (maxWidth <= 2)
                        {
                            y += Math.Max(rowMaxHeight, font.Height);
                            x = bounds.Left;
                            rowMaxHeight = 0;
                            maxWidth = bounds.Width;
                            if (maxWidth <= 2)
                            {
                                break;
                            }
                        }

                        var chunk = TakeOverviewWrappedChunk(g, remainder, font, maxWidth, flags);
                        if (chunk.Length == 0)
                        {
                            y += Math.Max(rowMaxHeight, font.Height);
                            x = bounds.Left;
                            rowMaxHeight = 0;
                            chunk = TakeOverviewWrappedChunk(g, remainder, font, bounds.Width, flags);
                            if (chunk.Length == 0)
                            {
                                break;
                            }

                            maxWidth = bounds.Width;
                        }

                        var color = selected
                            ? Color.White
                            : segment.Plain
                                ? OverviewPlainTextColor
                                : segment.Ok
                                    ? OverviewCheckOkColor
                                    : OverviewCheckFailColor;

                        var chunkSize = TextRenderer.MeasureText(g, chunk, font, new Size(maxWidth, int.MaxValue), flags);
                        TextRenderer.DrawText(
                            g,
                            chunk,
                            font,
                            new Rectangle(x, y, maxWidth, chunkSize.Height),
                            color,
                            flags);

                        rowMaxHeight = Math.Max(rowMaxHeight, chunkSize.Height);
                        remainder = remainder.Substring(chunk.Length);
                        x += TextRenderer.MeasureText(g, chunk, font, Size.Empty, TextFormatFlags.NoPadding).Width;

                        if (remainder.Length > 0)
                        {
                            y += rowMaxHeight;
                            x = bounds.Left;
                            rowMaxHeight = 0;
                        }
                    }
                }

                if (rowMaxHeight > 0 || x > bounds.Left)
                {
                    y += Math.Max(rowMaxHeight, font.Height);
                }
            }
        }

        private static List<OverviewTextSegment> TokenizeOverviewLine(string line)
        {
            var segments = new List<OverviewTextSegment>();
            var i = 0;
            while (i < line.Length)
            {
                var marker = IndexOfCheckMarker(line, i);
                if (marker < 0)
                {
                    segments.Add(new OverviewTextSegment { Text = line.Substring(i), Plain = true, Ok = true });
                    break;
                }

                if (marker > i)
                {
                    segments.Add(new OverviewTextSegment { Text = line.Substring(i, marker - i), Plain = true, Ok = true });
                }

                i = marker;
                var isOk = line[i] == '✓';
                var end = IndexOfOverviewSegmentEnd(line, i);
                segments.Add(new OverviewTextSegment { Text = line.Substring(i, end - i), Plain = false, Ok = isOk });
                i = end;
            }

            return segments;
        }

        private static string TakeOverviewWrappedChunk(
            Graphics g,
            string text,
            Font font,
            int maxWidth,
            TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            {
                return string.Empty;
            }

            var singleLine = flags | TextFormatFlags.SingleLine;
            if (TextRenderer.MeasureText(g, text, font, Size.Empty, singleLine).Width <= maxWidth)
            {
                return text;
            }

            var lo = 1;
            var hi = text.Length;
            var best = 0;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var sub = text.Substring(0, mid);
                if (TextRenderer.MeasureText(g, sub, font, Size.Empty, singleLine).Width <= maxWidth)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (best <= 0)
            {
                return text.Substring(0, 1);
            }

            if (best < text.Length)
            {
                var sub = text.Substring(0, best);
                var lastSpace = sub.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                    best = lastSpace + 1;
                }
            }

            return text.Substring(0, best);
        }

        private static int IndexOfCheckMarker(string line, int start)
        {
            var ok = line.IndexOf('✓', start);
            var fail = line.IndexOf('✗', start);
            if (ok < 0)
            {
                return fail;
            }

            if (fail < 0)
            {
                return ok;
            }

            return Math.Min(ok, fail);
        }

        private static int IndexOfOverviewSegmentEnd(string line, int markerIndex)
        {
            var sep = line.IndexOf(" · ", markerIndex, StringComparison.Ordinal);
            var sepWide = line.IndexOf("  ·  ", markerIndex, StringComparison.Ordinal);
            var end = line.Length;
            if (sep >= 0)
            {
                end = Math.Min(end, sep);
            }

            if (sepWide >= 0)
            {
                end = Math.Min(end, sepWide);
            }

            var next = IndexOfCheckMarker(line, markerIndex + 1);
            if (next >= 0)
            {
                end = Math.Min(end, next);
            }

            return Math.Max(markerIndex + 1, end);
        }

        private static void PaintOverviewStatusCell(DataGridView dgv, DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var text = Convert.ToString(cell.Value) ?? string.Empty;
            var font = cell.Style.Font ?? dgv.Font;
            var pad = OverviewGridCellPad;
            var inner = Rectangle.Inflate(e.CellBounds, -1, -1);
            var textRect = new Rectangle(inner.X + pad, inner.Y + pad, inner.Width - pad * 2, inner.Height - pad * 2);
            var selected = e.State.HasFlag(DataGridViewElementStates.Selected);
            DrawOverviewStatusText(e.Graphics, text, textRect, font, selected);
            e.Handled = true;
        }

        private static string FormatSceneCheckCell(ShowcaseOverviewSceneCard card)
        {
            var clip = card.HasClip ? "✓ Clip" : "✗ Thiếu clip";
            var prompt = card.HasPrompt ? "✓ Prompt" : "✗ Thiếu prompt";
            var tool = string.IsNullOrWhiteSpace(card.ClipToolLabel) ? "—" : card.ClipToolLabel.Trim();
            return "Cảnh " + card.SceneIndex + " · " + tool + Environment.NewLine + clip + "  ·  " + prompt;
        }

        private static void StyleOverviewGridRow(DataGridViewRow row)
        {
            if (row == null)
            {
                return;
            }

            ApplyPlainTextColor(row.Cells["colStt"]);
            ApplyPlainTextColor(row.Cells["colScene"]);
            ApplyPlainTextColor(row.Cells["colVoice"]);
            ApplyPlainTextColor(row.Cells["colSubtitle"]);
            ApplyPlainTextColor(row.Cells["colNarrationAudio"]);
            ApplyPlainTextColor(row.Cells["colBedAudio"]);
            ApplyPlainTextColor(row.Cells["colTransitionFx"]);
        }

        private static string FormatOverviewNarrationAudioCell(ShowcaseOverviewAudioSubtitlePanel au)
        {
            if (au == null)
            {
                return "—";
            }

            var lines = new List<string>();
            lines.Add(au.HasNarration
                ? "✓ Thoại: " + au.NarrationLabel
                : "✗ Thoại: " + au.NarrationLabel);
            lines.Add(au.VoiceAligned ? "✓ Khớp clip" : "✗ Khớp clip: chưa khớp");
            if (!string.IsNullOrWhiteSpace(au.SpeedLabel))
            {
                lines.Add("Tốc độ: " + au.SpeedLabel.Trim());
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatOverviewBedAudioCell(ShowcaseOverviewAudioSubtitlePanel au)
        {
            if (au == null)
            {
                return "—";
            }

            var lines = new List<string>();
            lines.Add("Nhạc nền: " + au.MusicLabel + " · " + au.MusicVolume);
            var sfxOk = (au.SfxLabel ?? string.Empty).IndexOf("Hook SFX", StringComparison.OrdinalIgnoreCase) >= 0
                        || (au.SfxLabel ?? string.Empty).IndexOf("CTA SFX", StringComparison.OrdinalIgnoreCase) >= 0;
            lines.Add(sfxOk ? "✓ SFX: " + au.SfxLabel : "SFX: " + (au.SfxLabel ?? "—"));
            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatOverviewTransitionCell(ShowcaseOverviewAudioSubtitlePanel au)
        {
            if (au == null)
            {
                return "—";
            }

            var label = (au.TransitionLabel ?? string.Empty).Trim();
            return label.Length == 0 ? "—" : label;
        }

        private static string BuildRenderStatusLine(ShowcaseRenderOverviewSnapshot snapshot)
        {
            if (snapshot.RenderReady)
            {
                return "✓ Đủ điều kiện Render — bấm «Render video» trên tab Showcase.";
            }

            var blockers = snapshot.RenderBlockers;
            if (blockers == null || blockers.Count == 0)
            {
                return "⚠ Chưa đủ điều kiện Render.";
            }

            return "⚠ Còn thiếu: " + string.Join(" · ",
                blockers.Select(b => StripBlockerProductPrefix(b, snapshot.ProductTitle)));
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
            if (text.StartsWith("—", StringComparison.Ordinal))
            {
                text = text.Substring(1).TrimStart();
            }

            if (text.StartsWith("-", StringComparison.Ordinal))
            {
                text = text.Substring(1).TrimStart();
            }

            return text.Length == 0 ? blocker.Trim() : text;
        }

        private static Panel CreatePipelineChip(ShowcaseOverviewPipelineStep step)
        {
            Color bg;
            Color fg = Color.White;
            switch (step.Status)
            {
                case ShowcaseOverviewStepStatus.Complete:
                    bg = Color.FromArgb(45, 100, 70);
                    break;
                case ShowcaseOverviewStepStatus.Partial:
                    bg = Color.FromArgb(120, 95, 45);
                    break;
                default:
                    bg = Color.FromArgb(95, 55, 55);
                    break;
            }

            var pnl = new TableLayoutPanel
            {
                Width = PipelineChipWidth,
                Height = PipelineChipHeight,
                Margin = new Padding(4, 0, 4, 0),
                BackColor = bg,
                Padding = new Padding(10, 10, 10, 10),
                ColumnCount = 1,
                RowCount = 2
            };
            pnl.RowStyles.Add(new RowStyle(SizeType.Absolute, PipelineChipTitleRowHeight));
            pnl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            pnl.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = false,
                UseCompatibleTextRendering = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 1, 0, 3),
                ForeColor = fg,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Text = step.Title ?? string.Empty
            }, 0, 0);
            pnl.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 4, 0, 2),
                ForeColor = Color.FromArgb(230, 235, 240),
                Font = new Font("Segoe UI", 8.5F),
                Text = step.Detail ?? string.Empty
            }, 0, 1);
            return pnl;
        }

        private static Label CreateArrowLabel()
        {
            return new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 130, 145),
                Font = new Font("Segoe UI", 14F),
                Margin = new Padding(2, 56, 2, 0),
                Text = "→"
            };
        }

        private Panel CreateBottomPanel()
        {
            var pnl = new Panel { Height = 52, BackColor = Color.FromArgb(31, 34, 42) };
            var btnOk = new Button
            {
                Text = "Đóng",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 32,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(ClientSize.Width - Pad - 100, 10),
                BackColor = Color.FromArgb(55, 90, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            pnl.Controls.Add(btnOk);
            return pnl;
        }
    }
}
