using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private static Control FindAffiliateUiControl(Control root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.Name, name, StringComparison.Ordinal))
            {
                return root;
            }

            foreach (Control child in root.Controls)
            {
                var hit = FindAffiliateUiControl(child, name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        private static void SyncAffiliateFilterChildWidths(FlowLayoutPanel filtersHost, int innerWidth)
        {
            if (filtersHost == null || filtersHost.IsDisposed)
            {
                return;
            }

            foreach (Control child in filtersHost.Controls)
            {
                child.Width = Math.Max(120, innerWidth - child.Margin.Horizontal);
                child.PerformLayout();
            }
        }

        private static void SyncAffiliateKeywordLineWidth(Control kwHost, TableLayoutPanel kwLine, Control shell)
        {
            if (shell == null || shell.IsDisposed)
            {
                return;
            }

            var shellW = Math.Max(200, shell.ClientSize.Width);
            if (kwHost != null && !kwHost.IsDisposed)
            {
                kwHost.Width = shellW;
            }

            if (kwLine == null || kwLine.IsDisposed)
            {
                return;
            }

            kwLine.Width = kwHost != null ? kwHost.ClientSize.Width : shellW;
            kwLine.PerformLayout();
        }

        private static int MeasureAffiliateFiltersNeededHeight(Control filtersHost, int availableWidth)
        {
            if (filtersHost == null || filtersHost.IsDisposed)
            {
                return 0;
            }

            var innerWidth = Math.Max(
                200,
                availableWidth - filtersHost.Margin.Horizontal - filtersHost.Padding.Horizontal);
            var probeWidth = Math.Max(innerWidth, 1600);

            var sum = filtersHost.Padding.Vertical;
            foreach (Control ctrl in filtersHost.Controls)
            {
                var rowWidth = ctrl is FlowLayoutPanel ? probeWidth : innerWidth;
                var pref = ctrl.GetPreferredSize(new Size(
                    Math.Max(80, rowWidth - ctrl.Margin.Horizontal),
                    0));
                sum += pref.Height + ctrl.Margin.Top + ctrl.Margin.Bottom;
            }

            return sum + filtersHost.Margin.Top + filtersHost.Margin.Bottom;
        }

        private void EnsureAffiliateFiltersLayout()
        {
            if (tabAffiliateHunter == null || tabAffiliateHunter.IsDisposed)
            {
                return;
            }

            tabAffiliateHunter.AutoScroll = false;
            tabAffiliateHunter.AutoScrollMinSize = Size.Empty;
            DisableAutoScrollRecursive(tabAffiliateHunter);

            var shell = FindAffiliateUiControl(tabAffiliateHunter, "pnlAffiliateShell");
            var filtersHost = FindAffiliateUiControl(tabAffiliateHunter, "tblAffiliateFilters") as FlowLayoutPanel;
            var actionsFlow = FindAffiliateUiControl(tabAffiliateHunter, "flpAffiliateActions") as FlowLayoutPanel;

            var tabH = tabAffiliateHunter.ClientSize.Height;
            var tabW = tabAffiliateHunter.ClientSize.Width;
            if (tabH < 150 || tabW < 150)
            {
                return;
            }

            if (actionsFlow != null)
            {
                actionsFlow.WrapContents = tabW < 1100;
            }

            var kwHost = FindAffiliateUiControl(tabAffiliateHunter, "pnlAffiliateKeywordHost");
            var kwLine = FindAffiliateUiControl(tabAffiliateHunter, "pnlAffiliateKeywordLine") as TableLayoutPanel;
            if (filtersHost != null && shell != null)
            {
                var shellW = Math.Max(120, shell.ClientSize.Width);
                SyncAffiliateKeywordLineWidth(kwHost, kwLine, shell);

                filtersHost.Width = shellW;
                var innerWidth = Math.Max(120, shellW - filtersHost.Margin.Horizontal);
                SyncAffiliateFilterChildWidths(filtersHost, innerWidth);

                filtersHost.MaximumSize = Size.Empty;
                shell.PerformLayout();
                kwLine?.PerformLayout();
                filtersHost.PerformLayout();
            }

            EnableHuntProductTabAutoScroll();
            RefreshHuntProductFilterScrollLayout();
        }
    }
}
