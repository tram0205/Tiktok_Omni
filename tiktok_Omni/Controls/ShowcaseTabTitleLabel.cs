using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace tiktok_Omni.Controls
{
    /// <summary>Tiêu đề tab Showcase — chữ 3D vàng nghệ, viền xám.</summary>
    internal sealed class ShowcaseTabTitleLabel : Panel
    {
        private static readonly Color OutlineColor = Color.FromArgb(108, 112, 122);
        private static readonly Color FaceColor = Color.FromArgb(232, 178, 42);
        private static readonly Color HighlightColor = Color.FromArgb(255, 228, 118);
        private static readonly Color ShadowColor = Color.FromArgb(138, 92, 14);

        private string _titleText = "SHOWCASE SẢN PHẨM";
        private readonly Font _titleFont = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point);

        public string TitleText
        {
            get => _titleText;
            set
            {
                _titleText = value ?? string.Empty;
                Invalidate();
            }
        }

        private const int TitleRowHeight = 64;

        public ShowcaseTabTitleLabel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.FromArgb(31, 34, 42);
            Padding = new Padding(10, 2, 8, 14);
            Height = TitleRowHeight;
            MinimumSize = new Size(0, TitleRowHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            var rect = ClientRectangle;
            rect.X += Padding.Left;
            rect.Y += Padding.Top;
            rect.Width = Math.Max(0, rect.Width - Padding.Horizontal);
            rect.Height = Math.Max(0, rect.Height - Padding.Vertical);

            DrawEmbossedText(e.Graphics, _titleText, _titleFont, rect);
        }

        private static void DrawEmbossedText(Graphics graphics, string text, Font font, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0 || string.IsNullOrEmpty(text))
            {
                return;
            }

            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Near;
                format.Trimming = StringTrimming.None;
                format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

                var layoutRect = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                var emSize = font.Size * graphics.DpiY / 72f;

                using (var textPath = new GraphicsPath())
                {
                    textPath.AddString(text, font.FontFamily, (int)font.Style, emSize, layoutRect, format);

                    using (var shadowPath = (GraphicsPath)textPath.Clone())
                    {
                        using (var matrix = new Matrix())
                        {
                            matrix.Translate(1.5f, 1.5f);
                            shadowPath.Transform(matrix);
                        }

                        using (var shadowBrush = new SolidBrush(ShadowColor))
                        {
                            graphics.FillPath(shadowBrush, shadowPath);
                        }
                    }

                    using (var highlightPath = (GraphicsPath)textPath.Clone())
                    {
                        using (var matrix = new Matrix())
                        {
                            matrix.Translate(-1f, -1f);
                            highlightPath.Transform(matrix);
                        }

                        using (var highlightBrush = new SolidBrush(HighlightColor))
                        {
                            graphics.FillPath(highlightBrush, highlightPath);
                        }
                    }

                    using (var outlinePen = new Pen(OutlineColor, 1.8f)
                    {
                        LineJoin = LineJoin.Round
                    })
                    {
                        graphics.DrawPath(outlinePen, textPath);
                    }

                    using (var faceBrush = new SolidBrush(FaceColor))
                    {
                        graphics.FillPath(faceBrush, textPath);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
