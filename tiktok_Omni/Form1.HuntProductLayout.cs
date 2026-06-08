using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private bool _huntProductLayoutWired;

        private void WireHuntProductTabLayout()
        {
            if (_huntProductLayoutWired || tabHuntProduct == null)
            {
                return;
            }

            _huntProductLayoutWired = true;

            if (tabCtrlHunter != null)
            {
                tabCtrlHunter.SelectedIndexChanged += (_, __) =>
                {
                    if (tabCtrlHunter.SelectedTab == tabHuntProduct)
                    {
                        ApplyHuntProductTabLayout();
                    }
                };
            }

            tabHuntProduct.Resize += (_, __) => ApplyHuntProductTabLayout();
            tabHuntProduct.VisibleChanged += (_, __) =>
            {
                if (tabHuntProduct.Visible)
                {
                    ApplyHuntProductTabLayout();
                }
            };
        }

        private void ApplyHuntProductTabLayout()
        {
            EnableHuntProductTabAutoScroll();
            RefreshHuntProductFilterScrollLayout();
            SyncHuntProductSplitDistance();
        }

        private void RefreshHuntProductFilterScrollLayout()
        {
            if (tabHuntProduct == null || tabHuntProduct.IsDisposed)
            {
                return;
            }

            var split = FindHuntProductControl(tabHuntProduct, "splitHuntProductMain") as SplitContainer;
            var filtersHost = FindHuntProductControl(tabHuntProduct, "pnlHuntProductFiltersHost");
            if (split == null || filtersHost == null)
            {
                return;
            }

            split.Panel1.AutoScroll = true;
            filtersHost.PerformLayout();
            split.Panel1.PerformLayout();

            var width = Math.Max(split.Panel1.ClientSize.Width, 120);
            var preferred = filtersHost.GetPreferredSize(new Size(width, 0));
            var contentHeight = preferred.Height + 4;
            split.Panel1.AutoScrollMinSize = new Size(0, contentHeight);
        }

        private void EnableHuntProductTabAutoScroll()
        {
            if (tabHuntProduct == null || tabHuntProduct.IsDisposed)
            {
                return;
            }

            tabHuntProduct.AutoScroll = true;
            tabHuntProduct.AutoScrollMinSize = Size.Empty;

            var split = FindHuntProductControl(tabHuntProduct, "splitHuntProductMain") as SplitContainer;
            if (split != null)
            {
                split.Panel1.AutoScroll = true;
            }
        }

        private static Control FindHuntProductControl(Control root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (string.Equals(root.Name, name, StringComparison.Ordinal))
            {
                return root;
            }

            foreach (Control child in root.Controls)
            {
                var hit = FindHuntProductControl(child, name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        private static Panel CreateHuntProductButtonHost(Button button)
        {
            PrepareAffiliateToolbarButtonForFlow(button);
            button.Dock = DockStyle.None;
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            button.Margin = Padding.Empty;

            var hostHeight = AffiliateJellyButtonMinHeight + 10;
            var host = new Panel
            {
                AutoSize = false,
                Height = hostHeight,
                MinimumSize = new Size(200, hostHeight),
                Margin = new Padding(0, 2, 0, 4),
                Padding = new Padding(0, 4, 0, 0)
            };
            host.Controls.Add(button);
            return host;
        }
    }
}