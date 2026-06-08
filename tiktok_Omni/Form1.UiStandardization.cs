using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void WrapSettingsTabResponsive()
        {
            if (tabSetting == null || btnSaveSettings == null || lblSettingsValidation == null)
            {
                return;
            }

            if (tabSetting.Controls.Cast<Control>().Any(c => c.Name == "pnlSettingsMainHostV3"))
            {
                return;
            }

            var contentControls = new List<Control>();

            foreach (var ctrl in tabSetting.Controls.Cast<Control>().ToList())
            {
                if (ctrl == btnSaveSettings || ctrl == lblSettingsValidation)
                {
                    continue;
                }

                contentControls.Add(ctrl);
            }

            var groupBoxes = contentControls
                .OfType<GroupBox>()
                .OrderBy(g => g.TabIndex)
                .ThenBy(g => g.Top)
                .ToList();

            GroupBox FindGroupByName(string name)
            {
                return groupBoxes.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));
            }

            GroupBox FindGroup(string titlePrefix)
            {
                return groupBoxes.FirstOrDefault(g =>
                    !string.IsNullOrWhiteSpace(g.Text) &&
                    g.Text.StartsWith(titlePrefix, StringComparison.OrdinalIgnoreCase));
            }

            var grpPlatformLogin = FindGroupByName("grpPlatformLogin") ?? FindGroup("Đăng nhập");
            var grpGateway = FindGroupByName("grpGatewayTtsVeo") ?? FindGroup("Gateway");
            var grpProfiles = FindGroupByName("grpProfiles") ?? FindGroup("T\u00E0i kho\u1EA3n");
            var grpMedia = FindGroupByName("grpMedia") ?? FindGroup("FFmpeg");

            tabSetting.SuspendLayout();
            tabSetting.Controls.Clear();
            tabSetting.AutoScroll = false;
            tabSetting.AutoScrollMinSize = Size.Empty;
            tabSetting.Padding = Padding.Empty;

            var pnlTop = new Panel
            {
                Name = "pnlSettingsTopBar",
                Dock = DockStyle.Top,
                Height = 32,
                MinimumSize = new Size(0, 28),
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(28, 30, 38)
            };

            lblSettingsValidation.AutoSize = false;
            lblSettingsValidation.Dock = DockStyle.Fill;
            lblSettingsValidation.Margin = Padding.Empty;
            lblSettingsValidation.TextAlign = ContentAlignment.MiddleLeft;
            pnlTop.Controls.Add(lblSettingsValidation);

            var pnlMain = new Panel
            {
                Name = "pnlSettingsMainHostV3",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 6, 8, 10),
                BackColor = tabSetting.BackColor
            };

            var tbl = new TableLayoutPanel
            {
                Name = "tblSettingsRoot",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = tabSetting.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            void StyleTopGroup(GroupBox box, Padding? padding = null)
            {
                if (box == null)
                {
                    return;
                }

                box.Dock = DockStyle.Top;
                box.Margin = new Padding(0, 0, 0, 6);
                box.Padding = padding ?? new Padding(6, 8, 6, 4);
                box.AutoSize = true;
                box.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            }

            StyleTopGroup(grpPlatformLogin);
            if (grpPlatformLogin != null)
            {
                tbl.Controls.Add(grpPlatformLogin, 0, 0);
            }

            if (grpProfiles != null)
            {
                grpProfiles.Dock = DockStyle.Fill;
                grpProfiles.Margin = new Padding(0, 0, 0, 6);
                grpProfiles.Padding = new Padding(6, 16, 6, 4);
                grpProfiles.AutoSize = false;
                tbl.Controls.Add(grpProfiles, 0, 1);
            }

            StyleTopGroup(grpGateway);
            if (grpGateway != null)
            {
                tbl.Controls.Add(grpGateway, 0, 2);
            }

            StyleTopGroup(grpMedia, new Padding(6, 10, 6, 6));
            if (grpMedia != null)
            {
                tbl.Controls.Add(grpMedia, 0, 3);
            }

            pnlMain.Controls.Add(tbl);
            tabSetting.Controls.Add(pnlMain);
            tabSetting.Controls.Add(pnlTop);
            tabSetting.ResumeLayout(true);
            tabSetting.PerformLayout();
        }
    }
}
