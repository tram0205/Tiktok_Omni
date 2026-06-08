using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Color UiBannerBack = Color.FromArgb(40, 44, 54);
        private static readonly Color UiBannerFore = Color.FromArgb(200, 210, 225);

        protected static Label CreateStepBanner(Control parent, string text)
        {
            var banner = new Label
            {
                Name = "lblStepBanner",
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiBannerFore,
                BackColor = UiBannerBack,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point),
                Padding = new Padding(12, 0, 8, 0),
                AutoEllipsis = true
            };
            parent?.Controls.Add(banner);
            return banner;
        }

        protected static Label CreateMascotHeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 6),
                ForeColor = Color.FromArgb(120, 200, 255),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point)
            };
        }

        protected static Label CreateReadinessLabel(string name, string defaultText, int height = 36, bool allowWrap = false)
        {
            return new Label
            {
                Name = name,
                Text = defaultText,
                AutoSize = false,
                AutoEllipsis = !allowWrap,
                Dock = DockStyle.Top,
                Height = height,
                ForeColor = Color.FromArgb(255, 180, 120),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Padding = new Padding(4, 4, 4, 4)
            };
        }

        protected static TableLayoutPanel BuildTabRootTable(string name, float bannerHeight = 32F)
        {
            var tbl = new TableLayoutPanel
            {
                Name = name,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(32, 34, 44),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, bannerHeight));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return tbl;
        }

        protected static Panel CreateBannerHostPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiBannerBack,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
        }

        protected Button CreateSecondaryNavButton(string name, string text)
        {
            var btn = new Button
            {
                Name = name,
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 64, 77),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(8, 4, 0, 4),
                Padding = new Padding(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}