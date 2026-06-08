using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private Panel pnlGlobalLog;
        private Panel pnlGlobalLogHeader;
        private Panel pnlGlobalLogHeaderRow;
        private Button btnToggleGlobalLog;
        private FlowLayoutPanel flpGlobalLogActions;
        private bool _globalLogExpanded = false;
        private const int GlobalLogHeaderRowHeight = 32;
        private const int GlobalLogExpandedHeight = 156;
        private const int GlobalLogCollapsedHeight = 44;

        private void BuildGlobalLogShell()
        {
            pnlGlobalLog = new Panel
            {
                Name = "pnlGlobalLog",
                Dock = DockStyle.Bottom,
                Height = GlobalLogCollapsedHeight,
                MinimumSize = new Size(0, GlobalLogCollapsedHeight),
                BackColor = Color.FromArgb(24, 26, 32),
                Padding = new Padding(0)
            };

            pnlGlobalLogHeader = new Panel
            {
                Name = "pnlGlobalLogHeader",
                Dock = DockStyle.Top,
                Height = GlobalLogCollapsedHeight,
                BackColor = Color.FromArgb(36, 39, 48),
                Padding = new Padding(8, 6, 8, 6),
                MinimumSize = new Size(0, GlobalLogCollapsedHeight)
            };

            pnlGlobalLogHeaderRow = new Panel
            {
                Name = "pnlGlobalLogHeaderRow",
                Dock = DockStyle.Top,
                Height = GlobalLogHeaderRowHeight,
                BackColor = Color.Transparent,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };

            var lblGlobalLogTitle = new Label
            {
                Text = "Nh\u1eadt k\u00fd phi\u00ean",
                AutoSize = false,
                ForeColor = Color.FromArgb(180, 190, 210),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 8, 0)
            };

            btnToggleGlobalLog = new Button
            {
                Name = "btnToggleGlobalLog",
                Text = "M\u1edf log",
                AutoSize = false,
                Size = new Size(84, GlobalLogHeaderRowHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 64, 77),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0),
                Padding = new Padding(0),
                TabStop = false,
                Tag = "GlobalLogChrome",
                AccessibleName = "GlobalLogChrome",
                Dock = DockStyle.Right
            };
            btnToggleGlobalLog.FlatAppearance.BorderSize = 0;
            btnToggleGlobalLog.Click += (_, __) => ToggleGlobalLogPanel();

            flpGlobalLogActions = new FlowLayoutPanel
            {
                Name = "flpGlobalLogActions",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0),
                Visible = false
            };

            btnClearLogs = new Button
            {
                Name = "btnClearLogs",
                Text = "X\u00f3a log",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 64, 77),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(8, 4, 8, 4),
                Tag = "GlobalLogChrome",
                AccessibleName = "GlobalLogChrome"
            };
            btnClearLogs.FlatAppearance.BorderSize = 0;
            btnClearLogs.Click += btnClearLogs_Click;

            btnExportCurrentLogs = new Button
            {
                Name = "btnExportCurrentLogs",
                Text = "Xu\u1ea5t log",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 64, 77),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(8, 4, 8, 4),
                Tag = "GlobalLogChrome",
                AccessibleName = "GlobalLogChrome"
            };
            btnExportCurrentLogs.FlatAppearance.BorderSize = 0;
            btnExportCurrentLogs.Click += btnExportCurrentLogs_Click;

            btnOpenLogsFolder = new Button
            {
                Name = "btnOpenLogsFolder",
                Text = "Th\u01b0 m\u1ee5c log",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 64, 77),
                ForeColor = Color.WhiteSmoke,
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(8, 4, 8, 4),
                Tag = "GlobalLogChrome",
                AccessibleName = "GlobalLogChrome"
            };
            btnOpenLogsFolder.FlatAppearance.BorderSize = 0;
            btnOpenLogsFolder.Click += btnOpenLogsFolder_Click;

            flpGlobalLogActions.Controls.Add(btnClearLogs);
            flpGlobalLogActions.Controls.Add(btnExportCurrentLogs);
            flpGlobalLogActions.Controls.Add(btnOpenLogsFolder);

            pnlGlobalLogHeaderRow.Controls.Add(lblGlobalLogTitle);
            pnlGlobalLogHeaderRow.Controls.Add(btnToggleGlobalLog);
            pnlGlobalLogHeader.Controls.Add(flpGlobalLogActions);
            pnlGlobalLogHeader.Controls.Add(pnlGlobalLogHeaderRow);

            rtbLogs = new RichTextBox
            {
                Name = "rtbLogs",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Visible = false
            };

            pnlGlobalLog.Controls.Add(rtbLogs);
            pnlGlobalLog.Controls.Add(pnlGlobalLogHeader);
            ApplyGlobalLogChrome();
        }

        private void ToggleGlobalLogPanel()
        {
            _globalLogExpanded = !_globalLogExpanded;
            if (pnlGlobalLog == null)
            {
                return;
            }

            ApplyGlobalLogChrome();
        }

        private void ApplyGlobalLogChrome()
        {
            if (pnlGlobalLog == null)
            {
                return;
            }

            pnlGlobalLog.SuspendLayout();
            if (pnlGlobalLogHeader != null)
            {
                pnlGlobalLogHeader.SuspendLayout();
            }

            try
            {
                if (flpGlobalLogActions != null)
                {
                    flpGlobalLogActions.Visible = _globalLogExpanded;
                }

                if (rtbLogs != null)
                {
                    if (_globalLogExpanded)
                    {
                        rtbLogs.Visible = true;
                        rtbLogs.Dock = DockStyle.Fill;
                    }
                    else
                    {
                        rtbLogs.Visible = false;
                        rtbLogs.Dock = DockStyle.None;
                        rtbLogs.Height = 0;
                    }
                }

                if (_globalLogExpanded)
                {
                    flpGlobalLogActions?.PerformLayout();

                    var headerHeight = GlobalLogHeaderRowHeight + 12;
                    if (flpGlobalLogActions != null && flpGlobalLogActions.Visible)
                    {
                        headerHeight += flpGlobalLogActions.Margin.Top + flpGlobalLogActions.Height;
                    }

                    if (headerHeight < 64)
                    {
                        headerHeight = 64;
                    }

                    if (pnlGlobalLogHeader != null)
                    {
                        pnlGlobalLogHeader.Dock = DockStyle.Top;
                        pnlGlobalLogHeader.Height = headerHeight;
                        pnlGlobalLogHeader.MinimumSize = new Size(0, headerHeight);
                        pnlGlobalLogHeader.MaximumSize = new Size(0, headerHeight);
                    }

                    pnlGlobalLog.Height = GlobalLogExpandedHeight;
                    pnlGlobalLog.MinimumSize = new Size(0, GlobalLogExpandedHeight);
                    pnlGlobalLog.MaximumSize = new Size(0, GlobalLogExpandedHeight);
                }
                else
                {
                    if (pnlGlobalLogHeader != null)
                    {
                        pnlGlobalLogHeader.Dock = DockStyle.Fill;
                        pnlGlobalLogHeader.Height = GlobalLogCollapsedHeight;
                        pnlGlobalLogHeader.MinimumSize = new Size(0, GlobalLogCollapsedHeight);
                        pnlGlobalLogHeader.MaximumSize = new Size(0, GlobalLogCollapsedHeight);
                    }

                    pnlGlobalLog.Height = GlobalLogCollapsedHeight;
                    pnlGlobalLog.MinimumSize = new Size(0, GlobalLogCollapsedHeight);
                    pnlGlobalLog.MaximumSize = new Size(0, GlobalLogCollapsedHeight);
                }

                if (btnToggleGlobalLog != null)
                {
                    btnToggleGlobalLog.Text = _globalLogExpanded ? "Thu g\u1ecdn" : "M\u1edf log";
                }
            }
            finally
            {
                if (pnlGlobalLogHeader != null)
                {
                    pnlGlobalLogHeader.ResumeLayout(true);
                }

                pnlGlobalLog.ResumeLayout(true);
                pnlGlobalLog.PerformLayout();
            }
        }
    }
}
