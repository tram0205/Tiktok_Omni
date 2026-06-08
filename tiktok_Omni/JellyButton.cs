using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>Nút kiểu thạch trong — mặc định 60% trong suốt (fill opacity 40%).</summary>
    public class JellyButton : Button
    {
        public const string ChromeTag = "JellyChrome";
        public const float DefaultTransparency = 0.60f;

        private bool _hover;
        private bool _pressed;

        public Color JellyTint { get; set; } = Color.FromArgb(60, 64, 77);

        /// <summary>Độ đặc của lớp màu (1 - trong suốt). 0.40 = trong suốt 60%.</summary>
        public float JellyFillOpacity { get; set; } = 1f - DefaultTransparency;

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }

            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(ResolveSurfaceColor());
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var surface = ResolveSurfaceColor();
            g.Clear(surface);

            var rect = ClientRectangle;
            rect.Inflate(-1, 0);
            if (rect.Width <= 2 || rect.Height <= 2)
            {
                return;
            }

            var radius = Math.Max(2, Math.Min(rect.Height / 2, rect.Width / 2));
            using var shape = CreateRoundedPath(rect, radius);

            var fillOpacity = JellyFillOpacity;
            if (!Enabled)
            {
                fillOpacity *= 0.45f;
            }
            else if (_pressed)
            {
                fillOpacity = Math.Min(0.72f, fillOpacity + 0.14f);
            }
            else if (_hover)
            {
                fillOpacity = Math.Min(0.62f, fillOpacity + 0.10f);
            }

            var fillAlpha = (int)Math.Round(255f * fillOpacity);
            g.SetClip(shape);
            using (var fill = new SolidBrush(Color.FromArgb(fillAlpha, JellyTint)))
            {
                g.FillPath(fill, shape);
            }

            var highlightRect = new Rectangle(rect.X, rect.Y, rect.Width, Math.Max(4, rect.Height / 2));
            using (var highlight = new LinearGradientBrush(
                       highlightRect,
                       Color.FromArgb(64, 255, 255, 255),
                       Color.FromArgb(0, 255, 255, 255),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(highlight, highlightRect);
            }

            g.ResetClip();

            var borderAlpha = Enabled ? 96 : 48;
            using (var border = new Pen(Color.FromArgb(borderAlpha, 255, 255, 255), 1f))
            {
                g.DrawPath(border, shape);
            }

            var textColor = Enabled ? ForeColor : Color.FromArgb(140, ForeColor);
            var textPadX = Math.Max(10, radius / 2);
            var textRect = new Rectangle(
                rect.X + textPadX,
                rect.Y,
                Math.Max(1, rect.Width - textPadX * 2),
                rect.Height);
            var textSize = TextRenderer.MeasureText(
                g,
                Text,
                Font,
                textRect.Size,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.GlyphOverhangPadding);
            var textFlags = TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine
                | TextFormatFlags.NoPrefix
                | TextFormatFlags.NoClipping
                | TextFormatFlags.GlyphOverhangPadding;
            textFlags |= textSize.Width <= textRect.Width
                ? TextFormatFlags.HorizontalCenter
                : TextFormatFlags.Left;
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                textColor,
                textFlags);
        }

        private Color ResolveSurfaceColor()
        {
            for (var current = Parent; current != null; current = current.Parent)
            {
                if (current.BackColor.A == 255 && current.BackColor != Color.Transparent)
                {
                    return current.BackColor;
                }
            }

            return Color.FromArgb(36, 39, 48);
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var d = Math.Max(2, radius * 2);
            if (bounds.Width < d || bounds.Height < d)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new Rectangle(bounds.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public JellyButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Color.FromArgb(36, 39, 48);
            ForeColor = Color.FromArgb(245, 247, 250);
            Cursor = Cursors.Hand;
            AutoSize = true;
            MinimumSize = new Size(72, 34);
            Margin = new Padding(0, 0, 6, 0);
            Tag = ChromeTag;
            AccessibleName = ChromeTag;
            SetStyle(
                ControlStyles.UserPaint
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.Opaque,
                true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, false);
        }
    }
}
