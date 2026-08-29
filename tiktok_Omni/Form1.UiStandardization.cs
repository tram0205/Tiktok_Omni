using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private bool _settingsValidationBarLayoutHooked;

        private void WrapSettingsTabResponsive()
        {
            if (tabSetting == null || btnSaveSettings == null || lblSettingsValidation == null)
            {
                return;
            }

            var hasV3Host = tabSetting.Controls.Cast<Control>()
                .Any(c => c.Name == "pnlSettingsMainHostV3");
            var hasV4Footer = tabSetting.Controls.Cast<Control>()
                .Any(c => c.Name == "pnlSettingsSaveFooter");

            if (hasV3Host && !hasV4Footer)
            {
                EnsureSettingsValidationBarVisible();
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

            Panel existingTopBar = null;
            var legacyMainHost = contentControls.FirstOrDefault(c =>
                c.Name == "pnlSettingsMainHostV3" || c.Name == "pnlSettingsMainHostV4");
            if (legacyMainHost != null)
            {
                existingTopBar = tabSetting.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Name == "pnlSettingsTopBar") as Panel;

                var legacyTbl = legacyMainHost.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Name == "tblSettingsRoot");
                if (legacyTbl != null)
                {
                    contentControls = legacyTbl.Controls.Cast<Control>().ToList();
                }
            }

            if (hasV4Footer)
            {
                var footer = tabSetting.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Name == "pnlSettingsSaveFooter");
                var saveRow = footer?.Controls.Cast<Control>()
                    .FirstOrDefault(c => c.Name == "tblSettingsSaveRow"
                        || c.Controls.OfType<Control>().Any(x => ReferenceEquals(x, btnSaveSettings)));
                if (saveRow != null && !contentControls.Contains(saveRow))
                {
                    contentControls.Add(saveRow);
                }
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
            var grpVoice = FindGroupByName("grpVoiceSettings") ?? FindGroup("Voice");
            var grpProfiles = FindGroupByName("grpProfiles") ?? FindGroup("T\u00E0i kho\u1EA3n");
            var grpMedia = FindGroupByName("grpMedia") ?? FindGroup("FFmpeg");
            var pnlSaveRow = contentControls.FirstOrDefault(c =>
                string.Equals(c.Name, "tblSettingsSaveRow", StringComparison.Ordinal)
                || ReferenceEquals(c, btnSaveSettings)
                || c.Controls.OfType<Control>().Any(x => ReferenceEquals(x, btnSaveSettings)));

            tabSetting.SuspendLayout();
            tabSetting.Controls.Clear();
            tabSetting.AutoScroll = false;
            tabSetting.AutoScrollMinSize = Size.Empty;
            tabSetting.Padding = Padding.Empty;

            var pnlTop = existingTopBar ?? new Panel
            {
                Name = "pnlSettingsTopBar",
                Dock = DockStyle.Top,
                Height = 32,
                MinimumSize = new Size(0, 28),
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(28, 30, 38)
            };

            if (existingTopBar == null)
            {
                lblSettingsValidation.AutoSize = false;
                lblSettingsValidation.Dock = DockStyle.Fill;
                lblSettingsValidation.Margin = Padding.Empty;
                lblSettingsValidation.TextAlign = ContentAlignment.MiddleLeft;
                pnlTop.Controls.Add(lblSettingsValidation);
            }

            var pnlMain = new Panel
            {
                Name = "pnlSettingsMainHostV3",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 6, 8, 10),
                AutoScroll = true,
                BackColor = tabSetting.BackColor
            };

            var tbl = new TableLayoutPanel
            {
                Name = "tblSettingsRoot",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = tabSetting.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
                if (dgvProxyProfiles != null && !dgvProxyProfiles.IsDisposed)
                {
                    dgvProxyProfiles.Dock = DockStyle.Fill;
                }

                tbl.Controls.Add(grpProfiles, 0, 1);
            }

            if (pnlSaveRow != null)
            {
                if (pnlSaveRow.Parent != null)
                {
                    pnlSaveRow.Parent.Controls.Remove(pnlSaveRow);
                }

                pnlSaveRow.Dock = DockStyle.Top;
                pnlSaveRow.Margin = new Padding(0, 0, 0, 6);
                btnSaveSettings.Anchor = AnchorStyles.None;
                btnSaveSettings.Margin = new Padding(0, 4, 0, 8);
                tbl.Controls.Add(pnlSaveRow, 0, 2);
            }

            StyleTopGroup(grpGateway);
            if (grpGateway != null)
            {
                tbl.Controls.Add(grpGateway, 0, 3);
            }

            StyleTopGroup(grpVoice, new Padding(6, 10, 6, 6));
            if (grpVoice != null)
            {
                tbl.Controls.Add(grpVoice, 0, 4);
            }

            StyleTopGroup(grpMedia, new Padding(6, 10, 6, 6));
            if (grpMedia != null)
            {
                tbl.Controls.Add(grpMedia, 0, 5);
            }

            pnlMain.Controls.Add(tbl);
            tabSetting.Controls.Add(pnlMain);
            tabSetting.Controls.Add(pnlTop);
            tabSetting.ResumeLayout(true);
            tabSetting.PerformLayout();
            EnsureSettingsValidationBarVisible();
        }

        private void EnsureSettingsValidationBarHooked()
        {
            if (_settingsValidationBarLayoutHooked || tabSetting == null)
            {
                return;
            }

            _settingsValidationBarLayoutHooked = true;
            tabSetting.Resize += (_, __) => EnsureSettingsValidationBarVisible();
        }

        private void EnsureSettingsValidationBarVisible()
        {
            if (tabSetting == null || lblSettingsValidation == null || lblSettingsValidation.IsDisposed)
            {
                return;
            }

            EnsureSettingsValidationBarHooked();

            var pnlTop = tabSetting.Controls.Cast<Control>()
                .FirstOrDefault(c => c.Name == "pnlSettingsTopBar") as Panel;
            if (pnlTop == null)
            {
                pnlTop = new Panel
                {
                    Name = "pnlSettingsTopBar",
                    Dock = DockStyle.Top,
                    Padding = new Padding(8, 6, 8, 6),
                    BackColor = Color.FromArgb(48, 28, 28)
                };
                tabSetting.Controls.Add(pnlTop);
            }

            if (!ReferenceEquals(lblSettingsValidation.Parent, pnlTop))
            {
                lblSettingsValidation.Parent?.Controls.Remove(lblSettingsValidation);
                pnlTop.Controls.Add(lblSettingsValidation);
            }

            lblSettingsValidation.Visible = true;
            lblSettingsValidation.ForeColor = Color.FromArgb(255, 120, 120);
            lblSettingsValidation.BackColor = Color.Transparent;
            lblSettingsValidation.AutoSize = false;
            lblSettingsValidation.Dock = DockStyle.Fill;
            lblSettingsValidation.Margin = Padding.Empty;
            lblSettingsValidation.TextAlign = ContentAlignment.MiddleLeft;
            lblSettingsValidation.AutoEllipsis = false;

            var message = (lblSettingsValidation.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(message))
            {
                pnlTop.Visible = false;
                pnlTop.Height = 0;
                pnlTop.MinimumSize = Size.Empty;
                pnlTop.BringToFront();
                return;
            }

            pnlTop.Visible = true;
            pnlTop.BackColor = Color.FromArgb(48, 28, 28);
            pnlTop.Padding = new Padding(10, 8, 10, 8);

            var textWidth = Math.Max(240, tabSetting.ClientSize.Width - pnlTop.Padding.Horizontal - 4);
            var textSize = TextRenderer.MeasureText(
                message,
                lblSettingsValidation.Font,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            var height = Math.Max(36, textSize.Height + pnlTop.Padding.Vertical + 4);
            pnlTop.Height = height;
            pnlTop.MinimumSize = new Size(0, height);
            pnlTop.BringToFront();
        }

        private void ApplySettingsSaveButtonState()
        {
            if (btnSaveSettings == null || btnSaveSettings.IsDisposed)
            {
                return;
            }

            if (btnSaveSettings is JellyButton jelly)
            {
                var enabled = btnSaveSettings.Enabled;
                jelly.JellyTint = enabled
                    ? Color.FromArgb(28, 156, 72)
                    : Color.FromArgb(78, 82, 92);
                jelly.ForeColor = enabled ? Color.White : Color.FromArgb(168, 172, 180);
                jelly.Cursor = enabled ? Cursors.Hand : Cursors.No;
                jelly.Invalidate();
            }
        }
    }
}
