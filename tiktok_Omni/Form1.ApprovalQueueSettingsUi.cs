using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static readonly Font ApprovalQueueBodyFont = AppInputFont;
        private static readonly Font ApprovalQueueTitleFont = AppGridHeaderFont;
        private static readonly Font ApprovalQueueButtonFont = new Font("Segoe UI", 12.5F, FontStyle.Bold);
        private const int ApprovalQueueInputHeight = AppDefaultInputHeight;
        private const int ApprovalQueueGridRowHeight = AppDefaultRowHeight;

        private Panel pnlApprovalBehaviorHost;
        private bool _approvalBehaviorControlsInitialized;
        private bool _suppressApprovalBehaviorPersist;

        private void BuildApprovalQueueBehaviorControls()
        {
            if (_approvalBehaviorControlsInitialized)
            {
                return;
            }

            pnlApprovalBehaviorHost = new Panel
            {
                Name = "pnlApprovalBehaviorHost",
                Visible = false,
                Size = new Size(0, 0)
            };
            Controls.Add(pnlApprovalBehaviorHost);

            chkAlwaysRequirePrePostApproval = new CheckBox
            {
                Name = "chkAlwaysRequirePrePostApproval",
                Text = "Lu\u00f4n y\u00eau c\u1ea7u duy\u1ec7t tr\u01b0\u1edbc khi \u0111\u0103ng",
                AutoSize = true,
                Font = ApprovalQueueBodyFont,
                ForeColor = Color.Gainsboro,
                Checked = true,
                Margin = new Padding(0, 0, 16, 4)
            };
            chkAlwaysRequirePreRenderApproval = new CheckBox
            {
                Name = "chkAlwaysRequirePreRenderApproval",
                Text = "Lu\u00f4n y\u00eau c\u1ea7u duy\u1ec7t tr\u01b0\u1edbc khi render",
                AutoSize = true,
                Font = ApprovalQueueBodyFont,
                ForeColor = Color.Gainsboro,
                Checked = false,
                Margin = new Padding(0, 0, 16, 4)
            };
            chkAutoRunApprovedQueue = new CheckBox
            {
                Name = "chkAutoRunApprovedQueue",
                Text = "T\u1ef1 ch\u1ea1y m\u1ee5c \u0111\u00e3 duy\u1ec7t",
                AutoSize = true,
                Font = ApprovalQueueBodyFont,
                ForeColor = Color.Gainsboro,
                Checked = false,
                Margin = new Padding(0, 0, 16, 4)
            };

            pnlApprovalBehaviorHost.Controls.Add(chkAlwaysRequirePrePostApproval);
            pnlApprovalBehaviorHost.Controls.Add(chkAlwaysRequirePreRenderApproval);
            pnlApprovalBehaviorHost.Controls.Add(chkAutoRunApprovedQueue);

            chkAlwaysRequirePrePostApproval.CheckedChanged += ApprovalBehaviorSetting_Changed;
            chkAlwaysRequirePreRenderApproval.CheckedChanged += ApprovalBehaviorSetting_Changed;
            chkAutoRunApprovedQueue.CheckedChanged += ApprovalBehaviorSetting_Changed;

            _approvalBehaviorControlsInitialized = true;
        }

        private Panel BuildApprovalQueueBehaviorPanel()
        {
            BuildApprovalQueueBehaviorControls();

            var panel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 6, 8, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var grp = new GroupBox
            {
                Text = "Quy tr\u00ecnh duy\u1ec7t",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = ApprovalQueueTitleFont,
                ForeColor = Color.FromArgb(200, 204, 214),
                Padding = new Padding(10, 10, 10, 8),
                Margin = new Padding(0)
            };

            var hint = new Label
            {
                Text = "Thay \u0111\u1ed5i \u0111\u01b0\u1ee3c l\u01b0u t\u1ef1 \u0111\u1ed9ng.",
                AutoSize = true,
                Font = ApprovalQueueBodyFont,
                ForeColor = Color.FromArgb(150, 156, 172),
                Margin = new Padding(0, 0, 0, 6),
                Dock = DockStyle.Top
            };

            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0)
            };

            foreach (var cb in new[] { chkAlwaysRequirePrePostApproval, chkAlwaysRequirePreRenderApproval, chkAutoRunApprovedQueue })
            {
                if (cb?.Parent != null)
                {
                    cb.Parent.Controls.Remove(cb);
                }

                flp.Controls.Add(cb);
            }

            grp.Controls.Add(flp);
            grp.Controls.Add(hint);
            panel.Controls.Add(grp);
            return panel;
        }

        private void RestoreApprovalBehaviorControlsToHost()
        {
            if (pnlApprovalBehaviorHost == null)
            {
                return;
            }

            foreach (var cb in new[] { chkAlwaysRequirePrePostApproval, chkAlwaysRequirePreRenderApproval, chkAutoRunApprovedQueue })
            {
                if (cb == null)
                {
                    continue;
                }

                if (cb.Parent != null)
                {
                    cb.Parent.Controls.Remove(cb);
                }

                pnlApprovalBehaviorHost.Controls.Add(cb);
            }
        }

        private async void ApprovalBehaviorSetting_Changed(object sender, EventArgs e)
        {
            if (_suppressApprovalBehaviorPersist)
            {
                return;
            }

            await PersistApprovalBehaviorSettingsAsync().ConfigureAwait(true);
        }

        private async Task PersistApprovalBehaviorSettingsAsync()
        {
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                settings.AlwaysRequirePrePostApproval = chkAlwaysRequirePrePostApproval?.Checked ?? true;
                settings.AlwaysRequirePreRenderApproval = chkAlwaysRequirePreRenderApproval?.Checked ?? false;
                settings.AutoRunApprovedQueue = chkAutoRunApprovedQueue?.Checked ?? false;
                await _configManager.SaveAsync(settings).ConfigureAwait(true);
                Log("[APPROVAL] \u0110\u00e3 l\u01b0u quy tr\u00ecnh duy\u1ec7t.");
            }
            catch (Exception ex)
            {
                Log("[APPROVAL] L\u1ed7i l\u01b0u quy tr\u00ecnh duy\u1ec7t: " + ex.Message);
            }
        }

        private static void ApplyApprovalQueueDialogTypography(Control root)
        {
            if (root == null)
            {
                return;
            }

            if (root is Button button)
            {
                button.Font = ApprovalQueueButtonFont;
                button.MinimumSize = new Size(button.MinimumSize.Width, Math.Max(button.MinimumSize.Height, ApprovalQueueInputHeight));
            }
            else if (root is DataGridView grid)
            {
                // Font control trước chrome — net472 OnFontChanged sau Controls.Add sẽ đè header nếu set Font sau.
                grid.Font = ApprovalQueueBodyFont;
                grid.DefaultCellStyle = new DataGridViewCellStyle(grid.DefaultCellStyle)
                {
                    Font = ApprovalQueueBodyFont,
                    Padding = new Padding(4, 2, 4, 2)
                };
                ApplyAppGridChrome(grid);
            }
            else if (root is GroupBox groupBox)
            {
                groupBox.Font = ApprovalQueueTitleFont;
            }
            else if (!(root is Form))
            {
                root.Font = ApprovalQueueBodyFont;
            }

            foreach (Control child in root.Controls)
            {
                ApplyApprovalQueueDialogTypography(child);
            }
        }
    }
}
