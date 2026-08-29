using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class PhilosophyBatchRenderReportDialog : Form
    {
        public PhilosophyBatchRenderReportDialog(PhilosophyBatchItem batch)
        {
            var topic = PhilosophyBatchHelper.TrimGridLabel(batch?.Topic, 60, "batch");
            var quotes = batch?.Quotes ?? new List<PhilosophyScriptItem>();
            var ok = quotes.Count(q =>
                string.Equals(q?.Status, "Xong", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(q?.OutputPath));
            var fail = quotes.Count(q =>
                q?.Status != null && q.Status.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0);

            Text = "Render batch xong";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 320);
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);
            Padding = new Padding(20, 16, 20, 12);

            var title = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(500, 0),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = fail > 0 ? Color.FromArgb(255, 190, 120) : Color.FromArgb(120, 210, 150),
                Text = "«" + topic + "»: " + ok + "/" + quotes.Count + " video thành công."
            };

            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            foreach (var q in quotes)
            {
                var preview = PhilosophyBatchHelper.TrimGridLabel(q?.Content, 48, "—");
                var status = q?.Status ?? "—";
                list.Items.Add("• " + preview + " → " + status);
            }

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft
            };
            var btnFolder = new Button { Text = "Mở thư mục", AutoSize = true, MinimumSize = new Size(112, 34) };
            btnFolder.Click += (_, __) =>
            {
                var folder = PhilosophyBatchHelper.ResolveBatchOutputFolder(batch);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", folder);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            };
            var btnClose = new Button { Text = "Đóng", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(96, 34) };
            buttons.Controls.Add(btnClose);
            buttons.Controls.Add(btnFolder);
            AcceptButton = btnClose;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            title.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(list, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);
        }
    }
}
