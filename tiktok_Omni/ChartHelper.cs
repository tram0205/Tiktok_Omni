using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace tiktok_Omni
{
    public static class ChartHelper
    {
        public static void DrawQueueTrendMiniChart(Graphics g, Rectangle clientRect, IList<QueueTrendRow> rows)
        {
            if (g == null || rows == null || rows.Count == 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 22, 28));
            var paddingLeft = 48;
            var paddingTop = 20;
            var paddingBottom = 42;
            var paddingRight = 16;
            var plot = new Rectangle(
                clientRect.X + paddingLeft,
                clientRect.Y + paddingTop,
                Math.Max(1, clientRect.Width - paddingLeft - paddingRight),
                Math.Max(1, clientRect.Height - paddingTop - paddingBottom));
            using (var axisPen = new Pen(Color.FromArgb(90, 95, 110)))
            using (var gridPen = new Pen(Color.FromArgb(52, 56, 70)))
            using (var successBrush = new SolidBrush(Color.FromArgb(82, 170, 96)))
            using (var failedBrush = new SolidBrush(Color.FromArgb(206, 86, 86)))
            using (var textBrush = new SolidBrush(Color.Gainsboro))
            using (var smallFont = new Font("Segoe UI", 8f))
            using (var titleFont = new Font("Segoe UI Semibold", 9f))
            {
                g.DrawRectangle(axisPen, plot);
                var maxValue = Math.Max(1, rows.Max(x => Math.Max(x.Success, x.Failed)));
                var ySteps = Math.Max(1, Math.Min(5, maxValue));
                for (var i = 0; i <= ySteps; i++)
                {
                    var ratio = i / (double)ySteps;
                    var y = plot.Bottom - (int)(plot.Height * ratio);
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    g.DrawString(((int)Math.Round(maxValue * ratio)).ToString(), smallFont, textBrush, plot.Left - 34, y - 7);
                }
                var groupWidth = plot.Width / (double)rows.Count;
                var barWidth = Math.Max(4f, (float)(groupWidth * 0.32));
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var center = (float)(plot.Left + i * groupWidth + groupWidth / 2d);
                    var successHeight = (float)(row.Success * 1d / maxValue * plot.Height);
                    var failedHeight = (float)(row.Failed * 1d / maxValue * plot.Height);
                    g.FillRectangle(successBrush, new RectangleF(center - barWidth - 1f, plot.Bottom - successHeight, barWidth, successHeight));
                    g.FillRectangle(failedBrush, new RectangleF(center + 1f, plot.Bottom - failedHeight, barWidth, failedHeight));
                    if (i % 2 == 0 || rows.Count <= 8)
                    {
                        var label = row.Date.Length >= 5 ? row.Date.Substring(5) : row.Date;
                        var size = g.MeasureString(label, smallFont);
                        g.DrawString(label, smallFont, textBrush, center - size.Width / 2f, plot.Bottom + 6);
                    }
                }
                g.DrawString("Success vs Failed (14 days)", titleFont, textBrush, plot.Left, clientRect.Top + 2);
                g.FillRectangle(successBrush, plot.Right - 190, clientRect.Top + 3, 14, 10);
                g.DrawString("Success", smallFont, textBrush, plot.Right - 172, clientRect.Top + 1);
                g.FillRectangle(failedBrush, plot.Right - 105, clientRect.Top + 3, 14, 10);
                g.DrawString("Failed", smallFont, textBrush, plot.Right - 87, clientRect.Top + 1);
            }
        }
    }
}