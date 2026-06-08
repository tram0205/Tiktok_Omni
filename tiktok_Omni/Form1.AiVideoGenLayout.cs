using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using tiktok_Omni.Services;



namespace tiktok_Omni

{

    public partial class Form1

    {

        private const int AiModeChromePadding = 8;

        private const int AiModeProgressBandHeight = 50;

        private static readonly Padding AiModeInputPadding = new Padding(8, 6, 8, 6);

        private static readonly Padding AiModeLogPadding = new Padding(8, 6, 8, 8);



        private static void ConfigureAiModeTabPage(TabPage page)

        {

            page.AutoScroll = false;

            page.Padding = new Padding(AiModeChromePadding, 6, AiModeChromePadding, AiModeChromePadding);

        }

        private static void ConfigureAiModeHostPanel(Panel page)

        {

            page.AutoScroll = false;

            page.Padding = new Padding(AiModeChromePadding, 6, AiModeChromePadding, AiModeChromePadding);

        }



        /// <summary>

        /// Tab mode: hàng nhập Absolute, hàng log Percent 100%; panel tiến độ (nếu có) Dock Bottom trên TabPage.

        /// </summary>

        private static void BuildAiModeFillLayout(

            TabPage page,

            out Panel inputPanel,

            out Panel logPanel,

            int inputRowHeight,

            Panel bottomProgressPanel = null)

        {

            ConfigureAiModeTabPage(page);



            if (bottomProgressPanel != null)

            {

                bottomProgressPanel.Dock = DockStyle.Bottom;

                bottomProgressPanel.Height = AiModeProgressBandHeight;

                bottomProgressPanel.Padding = new Padding(8, 4, 8, 4);

                bottomProgressPanel.Margin = new Padding(0);

                page.Controls.Add(bottomProgressPanel);

            }



            var tbl = new TableLayoutPanel

            {

                Name = page.Name + "_fillLayout",

                Dock = DockStyle.Fill,

                ColumnCount = 1,

                RowCount = 2,

                BackColor = page.BackColor,

                Margin = new Padding(0),

                Padding = new Padding(0)

            };

            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(40, inputRowHeight)));

            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));



            inputPanel = new Panel

            {

                Name = page.Name + "_input",

                Dock = DockStyle.Fill,

                AutoScroll = false,

                Padding = AiModeInputPadding,

                Margin = new Padding(0),

                BackColor = page.BackColor

            };

            logPanel = new Panel

            {

                Name = page.Name + "_log",

                Dock = DockStyle.Fill,

                Padding = AiModeLogPadding,

                Margin = new Padding(0),

                BackColor = Color.FromArgb(20, 22, 28)

            };



            tbl.Controls.Add(inputPanel, 0, 0);

            tbl.Controls.Add(logPanel, 0, 1);

            page.Controls.Add(tbl);

        }

        /// <summary>Input/log chia theo % (Mascot — tránh cắt preview + hàng đợi).</summary>
        private static void BuildAiModeFillLayoutPercent(
            Panel page,
            out Panel inputPanel,
            out Panel logPanel,
            float inputRowPercent,
            Panel bottomProgressPanel = null)
        {
            ConfigureAiModeHostPanel(page);

            if (bottomProgressPanel != null)
            {
                bottomProgressPanel.Dock = DockStyle.Bottom;
                bottomProgressPanel.Height = AiModeProgressBandHeight;
                bottomProgressPanel.Padding = new Padding(8, 4, 8, 4);
                bottomProgressPanel.Margin = new Padding(0);
                page.Controls.Add(bottomProgressPanel);
            }

            var pct = Math.Max(32F, Math.Min(52F, inputRowPercent));
            var tbl = new TableLayoutPanel
            {
                Name = page.Name + "_fillLayout",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = page.BackColor,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, pct));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F - pct));

            inputPanel = new Panel
            {
                Name = page.Name + "_input",
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = AiModeInputPadding,
                Margin = new Padding(0),
                BackColor = page.BackColor
            };
            logPanel = new Panel
            {
                Name = page.Name + "_log",
                Dock = DockStyle.Fill,
                Padding = AiModeLogPadding,
                Margin = new Padding(0),
                BackColor = Color.FromArgb(20, 22, 28)
            };

            tbl.Controls.Add(inputPanel, 0, 0);
            tbl.Controls.Add(logPanel, 0, 1);
            page.Controls.Add(tbl);
        }

        private static void BuildAiModeFillLayout(

            Panel page,

            out Panel inputPanel,

            out Panel logPanel,

            int inputRowHeight,

            Panel bottomProgressPanel = null)

        {

            ConfigureAiModeHostPanel(page);

            if (bottomProgressPanel != null)

            {

                bottomProgressPanel.Dock = DockStyle.Bottom;

                bottomProgressPanel.Height = AiModeProgressBandHeight;

                bottomProgressPanel.Padding = new Padding(8, 4, 8, 4);

                bottomProgressPanel.Margin = new Padding(0);

                page.Controls.Add(bottomProgressPanel);

            }

            var tbl = new TableLayoutPanel

            {

                Name = page.Name + "_fillLayout",

                Dock = DockStyle.Fill,

                ColumnCount = 1,

                RowCount = 2,

                BackColor = page.BackColor,

                Margin = new Padding(0),

                Padding = new Padding(0)

            };

            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(40, inputRowHeight)));

            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            inputPanel = new Panel

            {

                Name = page.Name + "_input",

                Dock = DockStyle.Fill,

                AutoScroll = false,

                Padding = AiModeInputPadding,

                Margin = new Padding(0),

                BackColor = page.BackColor

            };

            logPanel = new Panel

            {

                Name = page.Name + "_log",

                Dock = DockStyle.Fill,

                Padding = AiModeLogPadding,

                Margin = new Padding(0),

                BackColor = Color.FromArgb(20, 22, 28)

            };

            tbl.Controls.Add(inputPanel, 0, 0);

            tbl.Controls.Add(logPanel, 0, 1);

            page.Controls.Add(tbl);

        }

        /// <summary>Chỉ một hàng toolbar (Slideshow / Affiliate khi bảng SP ở tab chính).</summary>
        private static void BuildAiModeToolbarOnlyLayout(Panel page, out Panel toolbarPanel)
        {
            ConfigureAiModeHostPanel(page);

            toolbarPanel = new Panel
            {
                Name = page.Name + "_toolbar",
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = AiModeInputPadding,
                Margin = new Padding(0),
                BackColor = page.BackColor
            };

            page.Controls.Add(toolbarPanel);
        }

        /// <summary>Readiness + toolbar trong TableLayout (tránh Location tuyệt đối bị cắt).</summary>
        private static void WireAiVideoGenModeToolbarInput(Panel inputPanel, Label readinessLabel, Panel toolbarPanel)
        {
            inputPanel.SuspendLayout();
            inputPanel.Controls.Clear();

            var tbl = new TableLayoutPanel
            {
                Name = inputPanel.Name + "_layout",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = inputPanel.BackColor
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            readinessLabel.Dock = DockStyle.Fill;
            readinessLabel.Margin = new Padding(0, 0, 0, 4);
            readinessLabel.AutoSize = false;
            readinessLabel.Height = 36;

            toolbarPanel.Dock = DockStyle.Fill;
            toolbarPanel.Margin = new Padding(0);
            toolbarPanel.AutoSize = true;

            tbl.Controls.Add(readinessLabel, 0, 0);
            tbl.Controls.Add(toolbarPanel, 0, 1);
            inputPanel.Controls.Add(tbl);
            inputPanel.ResumeLayout(false);
        }

        private int MeasureAiVideoGenToolbarRowHeight(int modeTabIndex)
        {
            if (modeTabIndex != 0 && modeTabIndex != 1)
            {
                return 120;
            }

            var panelName = modeTabIndex == 0 ? "pnlModeSlideshow_toolbar" : "pnlModeAffiliateDeep_toolbar";
            var found = Controls.Find(panelName, true);
            if (found.Length == 0)
            {
                return 120;
            }

            return MeasureToolbarInputPreferredHeight(found[0]);
        }

        private static int MeasureToolbarInputPreferredHeight(Control inputPanel)
        {
            if (inputPanel == null)
            {
                return 120;
            }

            inputPanel.PerformLayout();
            var width = inputPanel.ClientSize.Width > 0
                ? inputPanel.ClientSize.Width
                : 900;
            width = Math.Max(320, width);
            var preferred = inputPanel.GetPreferredSize(new Size(width, 0));
            return Math.Max(96, Math.Min(260, preferred.Height + inputPanel.Padding.Vertical));
        }

        private static Panel CreateAiModeProgressBand(string name)

        {

            return new Panel

            {

                Name = name,

                BackColor = Color.FromArgb(28, 30, 38),

                Margin = new Padding(0),

                MinimumSize = new Size(0, AiModeProgressBandHeight)

            };

        }



        private static void ConfigureAiModeProgressBar(ProgressBar bar)

        {

            if (bar == null)

            {

                return;

            }



            bar.Dock = DockStyle.Bottom;

            bar.Height = 14;

            bar.Margin = new Padding(0, 4, 0, 0);

            bar.Style = ProgressBarStyle.Continuous;

        }



        private static void ConfigureAiModeProgressLabel(Label label)

        {

            if (label == null)

            {

                return;

            }



            label.Dock = DockStyle.Top;

            label.Height = 20;

            label.Margin = new Padding(0, 0, 0, 2);

            label.ForeColor = Color.FromArgb(200, 204, 214);

            label.TextAlign = ContentAlignment.MiddleLeft;

        }



        private static RichTextBox CreateAiModeLogTextBox(string name)

        {

            var font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            return new RichTextBox

            {

                Name = name,

                Dock = DockStyle.Fill,

                ReadOnly = true,

                BackColor = Color.FromArgb(20, 22, 28),

                ForeColor = Color.FromArgb(210, 214, 224),

                BorderStyle = BorderStyle.None,

                ScrollBars = RichTextBoxScrollBars.Vertical,

                Font = font,

                DetectUrls = false,

                WordWrap = true,

                Margin = new Padding(0)

            };

        }



        private static void ApplyAiModeLogLineSpacing(RichTextBox rtb)

        {

            if (rtb == null || rtb.IsDisposed)

            {

                return;

            }



            rtb.SelectAll();

            rtb.SelectionCharOffset = 1;

            rtb.SelectionLength = 0;

        }



        private static void LayoutAiModeClearLogButton(Button clearBtn, Control host)

        {

            if (clearBtn == null || host == null)

            {

                return;

            }



            clearBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            clearBtn.Margin = new Padding(0, 4, 4, 0);

            void Place()

            {

                clearBtn.Location = new Point(

                    Math.Max(8, host.ClientSize.Width - clearBtn.Width - 8),

                    4);

            }



            Place();

            host.Resize += (_, __) => Place();

        }



        private static void LayoutPhilosophyStep2Row(
            Panel inputPanel,
            FlowLayoutPanel actionRow,
            Label flowHint)
        {
            if (inputPanel == null || actionRow == null || flowHint == null)
            {
                return;
            }

            var w = Math.Max(320, inputPanel.ClientSize.Width - inputPanel.Padding.Horizontal);
            actionRow.Width = w;
            actionRow.PerformLayout();
            flowHint.Location = new Point(0, actionRow.Bottom + 6);
            flowHint.Width = w;
            inputPanel.AutoScrollMinSize = new Size(0, flowHint.Bottom + 12);
        }

        private void WireAiModeInputStretch(Panel inputPanel, params Control[] stretchControls)

        {

            if (inputPanel == null || stretchControls == null || stretchControls.Length == 0)

            {

                return;

            }



            void Apply()

            {

                var w = Math.Max(120, inputPanel.ClientSize.Width - inputPanel.Padding.Horizontal - 12);

                foreach (var c in stretchControls)

                {

                    if (c == null || c.IsDisposed)

                    {

                        continue;

                    }



                    if (c.Anchor.HasFlag(AnchorStyles.Left) && c.Anchor.HasFlag(AnchorStyles.Right))

                    {

                        c.Width = w;

                    }

                }

            }



            Apply();

            inputPanel.Resize += (_, __) => Apply();

        }
    }

}


