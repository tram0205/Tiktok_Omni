using System;
using System.Drawing;

using System.Windows.Forms;

namespace tiktok_Omni

{

    public partial class Form1

    {

        private static readonly Color StudioSidebarColor = Color.FromArgb(26, 28, 35);

        private static readonly Color StudioMainColor = Color.FromArgb(32, 34, 44);

        private static readonly Color StudioNavActiveColor = Color.FromArgb(50, 110, 68);



        private Panel pnlSidebar;

        private FlowLayoutPanel pnlSidebarNav;

        private Button btnNavHealth;

        private Button btnNavRevenue;

        private Button btnNavAffiliate;

        private Button btnNavAiVideo;

        private Button btnNavAutoPost;

        private Button btnNavWarmup;

        private Button btnNavSettings;



        private void ConfigureMainTabChrome()

        {

            tabMain.Appearance = TabAppearance.FlatButtons;

            tabMain.ItemSize = new Size(0, 1);

            tabMain.SizeMode = TabSizeMode.Fixed;

            tabMain.Padding = new Point(0, 0);

            tabMain.BackColor = StudioMainColor;

            tabMain.DrawMode = TabDrawMode.Normal;

        }



        private void SwitchToMainTab(TabPage page)

        {

            if (tabMain == null || page == null)

            {

                return;

            }



            var index = tabMain.TabPages.IndexOf(page);

            if (index < 0)

            {

                return;

            }



            tabMain.SelectedIndex = index;

            if (ReferenceEquals(page, tabAiVideoGen))
            {
                SetAiVideoGenSubNavExpanded(true);
            }
            else
            {
                SetAiVideoGenSubNavExpanded(false);
            }

            HighlightSidebarForSelectedTab();

            if (ReferenceEquals(page, tabAffiliateHunter))
            {
                EnsureAffiliateFiltersLayout();
            }

            BeginInvoke(new Action(() =>
            {
                if (ReferenceEquals(tabMain?.SelectedTab, tabAffiliateHunter))
                {
                    EnsureAffiliateFiltersLayout();
                    if (tabCtrlHunter != null && tabHuntProduct != null)
                    {
                        tabCtrlHunter.SelectedTab = tabHuntProduct;
                    }

                    ApplyHuntProductTabLayout();
                }
            }));

        }



        private void HighlightSidebarForSelectedTab()

        {

            if (tabMain?.SelectedTab != null && ReferenceEquals(tabMain.SelectedTab, tabAiVideoGen))

            {

                HighlightAiVideoGenSidebar(_selectedAiVideoGenMode);

                return;

            }



            HighlightSidebarButton(NavButtonForSelectedTab());

        }



        private Button NavButtonForSelectedTab()

        {

            var selected = tabMain?.SelectedTab;

            if (selected == null)

            {

                return null;

            }



            if (ReferenceEquals(selected, tabHealthDashboard))

            {

                return btnNavHealth;

            }



            if (ReferenceEquals(selected, tabRevenueDashboard))

            {

                return btnNavRevenue;

            }



            if (ReferenceEquals(selected, tabAffiliateHunter))

            {

                return btnNavAffiliate;

            }



            if (ReferenceEquals(selected, tabAiVideoGen))

            {

                return btnNavAiVideo;

            }



            if (ReferenceEquals(selected, tabAutoPost))

            {

                return btnNavAutoPost;

            }



            if (ReferenceEquals(selected, tabAutoWarmup))

            {

                return btnNavWarmup;

            }



            if (ReferenceEquals(selected, tabSetting))

            {

                return btnNavSettings;

            }



            return null;

        }



        private void HighlightSidebarButton(Button active)

        {

            if (pnlSidebar == null)

            {

                return;

            }



            foreach (Control ctrl in pnlSidebar.Controls)

            {

                if (ctrl is Button sideBtn && sideBtn == btnEmergencyStop)

                {

                    continue;

                }

                ApplySidebarButtonStyle(ctrl as Button, active != null && ReferenceEquals(ctrl, active));

            }



            if (pnlSidebarNav != null)

            {

                foreach (Control ctrl in pnlSidebarNav.Controls)

                {

                    if (!(ctrl is Button btn))

                    {

                        continue;

                    }



                    if (ReferenceEquals(btn, btnNavApprovalQueue))

                    {

                        ApplySidebarSecondaryNavStyle(btn);

                        continue;

                    }



                    if (IsAiVideoGenModeSidebarButton(btn))

                    {

                        if (TryGetAiVideoGenModeFromButton(btn, out var subMode))

                        {

                            var onAiTab = ReferenceEquals(tabMain?.SelectedTab, tabAiVideoGen);

                            ApplyAiVideoGenSubNavButtonStyle(btn, subMode, onAiTab && subMode == _selectedAiVideoGenMode);

                        }

                        continue;

                    }

                    ApplySidebarButtonStyle(btn, active != null && ReferenceEquals(btn, active));

                }

            }

        }



        private static void ApplySidebarSecondaryNavStyle(Button btn)

        {

            if (btn == null)

            {

                return;

            }

            btn.BackColor = Color.FromArgb(34, 38, 48);

            btn.ForeColor = Color.FromArgb(190, 198, 212);

        }



        private void ApplySidebarButtonStyle(Button btn, bool isActive)

        {

            if (btn == null)

            {

                return;

            }



            if (isActive)

            {

                btn.BackColor = StudioNavActiveColor;

                btn.ForeColor = Color.White;

            }

            else

            {

                btn.BackColor = StudioSidebarColor;

                btn.ForeColor = Color.Gainsboro;

            }

        }

    }

}


