using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed class ShowcaseRenderOverviewForm : Form
    {
        private const int Pad = 20;
        private readonly ShowcaseOverviewPanel _overviewPanel;

        public ShowcaseRenderOverviewForm(ShowcaseRenderOverviewSnapshot snapshot)
        {
            snapshot = snapshot ?? new ShowcaseRenderOverviewSnapshot();
            Text = "Tổng quan";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(1200, 720);
            ClientSize = new Size(1600, 900);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(Pad, Pad, Pad, 0);

            _overviewPanel = new ShowcaseOverviewPanel
            {
                Dock = DockStyle.Fill
            };
            _overviewPanel.ApplySnapshot(snapshot);

            var pnlBottom = CreateBottomPanel();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

            root.Controls.Add(_overviewPanel, 0, 0);
            root.Controls.Add(pnlBottom, 0, 1);
            Controls.Add(root);

            AcceptButton = pnlBottom.Controls.OfType<Button>().FirstOrDefault();
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
