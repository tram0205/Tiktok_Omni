using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {

        private void SetupManualInputUi()
        {
            const int manualInputRowHeight = AppInputMinHeight + 10;

            pnlManualInput = new Panel
            {
                Name = "pnlManualInput",
                Dock = DockStyle.Top,
                Height = manualInputRowHeight + 16,
                MinimumSize = new Size(0, manualInputRowHeight + 16),
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(32, 34, 44)
            };

            var tblManual = new TableLayoutPanel
            {
                Name = "tblManualProductUrl",
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = pnlManualInput.BackColor,
                MinimumSize = new Size(0, manualInputRowHeight)
            };
            tblManual.RowStyles.Add(new RowStyle(SizeType.Absolute, manualInputRowHeight));
            tblManual.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblManual.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblManual.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblPasteLink = new Label
            {
                Text = "Dán Link",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(200, 204, 214),
                Font = AppInputFont,
                Margin = new Padding(0, 0, 8, 0)
            };

            txtManualProductUrl = new TextBox
            {
                Name = "txtManualProductUrl",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = AppInputFont,
                Margin = new Padding(0, 0, 8, 0)
            };
            ApplyAppInputChrome(txtManualProductUrl);

            btnAddManualProduct = new Button
            {
                Name = "btnAddManualProduct",
                Text = "+ Thêm",
                AutoSize = true,
                MinimumSize = new Size(88, AppInputMinHeight),
                Anchor = AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnAddManualProduct.ApplyTheme(ButtonRole.Primary);
            btnAddManualProduct.Click += btnAddManualProduct_Click;

            tblManual.Controls.Add(lblPasteLink, 0, 0);
            tblManual.Controls.Add(txtManualProductUrl, 1, 0);
            tblManual.Controls.Add(btnAddManualProduct, 2, 0);
            pnlManualInput.Controls.Add(tblManual);
            ApplyTextBoxPlaceholder(txtManualProductUrl, "Dán link TikTok/Shopee...");
        }

        private static FlowLayoutPanel CreateToolbarFlowPanel(bool dockRight, bool rightToLeft = false, bool wrapContents = false)
        {
            return new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = wrapContents,
                FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                Dock = dockRight ? DockStyle.Right : DockStyle.Left,
                Padding = new Padding(0, 2, 0, 2),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
        }

        private static Panel CreateDualToolbarHost(int minHeight = 40)
        {
            return new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, minHeight),
                Padding = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            };
        }

        private static Button CreateToolbarButton(
            string text,
            bool executeStyle = false,
            int minWidth = 100)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(minWidth, 32),
                Margin = new Padding(4, 2, 4, 2),
                UseVisualStyleBackColor = false
            };
            var role = executeStyle
                ? ButtonRole.Primary
                : UIThemeManager.InferRole(null, text);
            btn.ApplyTheme(role);
            return btn;
        }

        private static Button CreateApprovalToolbarButton(string text, int minWidth = 96)
        {
            var btn = CreateToolbarButton(text, executeStyle: false, minWidth: minWidth);
            btn.Font = ApprovalQueueButtonFont;
            btn.MinimumSize = new Size(minWidth, ApprovalQueueInputHeight);
            return btn;
        }

        private Panel BuildApprovalQueueActionPanel(
            Button btnApprove,
            Button btnApproveAndPublish,
            Button btnReject,
            Button btnViewAuditLog,
            Button btnApproveLowRisk,
            Button btnRunApproved,
            Button btnRunAllApproved,
            ComboBox cbStatusFilter,
            TextBox txtApprovalKeyword,
            ComboBox cbApprovalProfile,
            DateTimePicker dtApprovalFrom,
            DateTimePicker dtApprovalTo,
            Button btnApproveFiltered,
            Button btnRunFilteredApproved)
        {
            var actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 88),
                Padding = new Padding(6, 6, 6, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var flpFilters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 0, 0, 4),
                BackColor = actionPanel.BackColor
            };
            flpFilters.Controls.Add(new Label
            {
                Text = "Lọc:",
                AutoSize = true,
                Font = ApprovalQueueBodyFont,
                ForeColor = Color.FromArgb(180, 185, 198),
                Margin = new Padding(0, 8, 4, 0)
            });
            StyleApprovalFilterCombo(cbStatusFilter);
            StyleApprovalFilterCombo(cbApprovalProfile);
            StyleApprovalFilterTextBox(txtApprovalKeyword);
            StyleApprovalDatePicker(dtApprovalFrom);
            StyleApprovalDatePicker(dtApprovalTo);
            flpFilters.Controls.Add(cbStatusFilter);
            flpFilters.Controls.Add(txtApprovalKeyword);
            flpFilters.Controls.Add(cbApprovalProfile);
            flpFilters.Controls.Add(dtApprovalFrom);
            flpFilters.Controls.Add(dtApprovalTo);
            flpFilters.Controls.Add(btnApproveFiltered);
            flpFilters.Controls.Add(btnRunFilteredApproved);

            var flpLeftActions = CreateToolbarFlowPanel(dockRight: false);
            flpLeftActions.Controls.Add(btnApprove);
            flpLeftActions.Controls.Add(btnApproveAndPublish);
            flpLeftActions.Controls.Add(btnReject);
            flpLeftActions.Controls.Add(btnViewAuditLog);

            var flpRightActions = CreateToolbarFlowPanel(dockRight: true, rightToLeft: true);
            flpRightActions.Controls.Add(btnRunAllApproved);
            flpRightActions.Controls.Add(btnRunApproved);
            flpRightActions.Controls.Add(btnApproveLowRisk);

            var pnlButtonRow = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 40),
                BackColor = actionPanel.BackColor
            };
            pnlButtonRow.Controls.Add(flpRightActions);
            pnlButtonRow.Controls.Add(flpLeftActions);

            actionPanel.Controls.Add(pnlButtonRow);
            actionPanel.Controls.Add(flpFilters);
            return actionPanel;
        }

        private static void StyleApprovalFilterCombo(ComboBox combo)
        {
            if (combo == null)
            {
                return;
            }

            combo.Width = 120;
            combo.Height = 28;
            combo.Margin = new Padding(4, 4, 4, 4);
            combo.BackColor = Color.FromArgb(45, 49, 60);
            combo.ForeColor = Color.WhiteSmoke;
        }

        private static void StyleApprovalFilterTextBox(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.Width = 150;
            textBox.Height = 28;
            textBox.Margin = new Padding(4, 4, 4, 4);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = Color.FromArgb(45, 49, 60);
            textBox.ForeColor = Color.WhiteSmoke;
        }

        private static void StyleApprovalDatePicker(DateTimePicker picker)
        {
            if (picker == null)
            {
                return;
            }

            picker.Width = 132;
            picker.Height = ApprovalQueueInputHeight;
            picker.Font = ApprovalQueueBodyFont;
            picker.Margin = new Padding(4, 4, 4, 4);
            picker.Format = DateTimePickerFormat.Short;
        }
    }
}
