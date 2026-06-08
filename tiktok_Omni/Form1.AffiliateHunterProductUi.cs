using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private const float HuntProductFieldFontSize = AppInputFontSize;
        private const float HuntProductManualLinkFontSize = AppInputFontSize;
        private static readonly Font HuntProductFieldFont = AppInputFont;
        private static readonly Font HuntProductManualLinkFont = AppInputFont;
        private const int HuntProductInputHeight = AppInputMinHeight;
        private const int HuntProductFilterRowHeight = AppGridHeaderHeight;
        private const float HuntProductNumericEditorWidth = 104F;
        private const float HuntProductMaxResultsWidth = 64F;

        private static void StyleHuntProductEditor(Control editor, int minWidth = 0, Font font = null)
        {
            if (editor == null)
            {
                return;
            }

            editor.Font = font ?? HuntProductFieldFont;
            editor.MinimumSize = new Size(
                Math.Max(minWidth, editor.MinimumSize.Width),
                HuntProductInputHeight);
            editor.Height = HuntProductInputHeight;
        }

        private void BuildAffiliateHunterProductUi(TabPage tab)
        {
            if (tab == null)
            {
                return;
            }

            tab.Controls.Clear();
            tab.AutoScroll = false;

            _huntProductBindingList = new BindingList<HuntProductCandidate>();
            dgvHuntProduct = CreateHuntProductGrid();
            dgvHuntProduct.Dock = DockStyle.Fill;

            var pnlFilters = BuildHuntProductFiltersPanel();
            pnlFilters.Dock = DockStyle.Top;
            pnlFilters.AutoSize = true;
            pnlFilters.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var split = new SplitContainer
            {
                Name = "splitHuntProductMain",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 6,
                Panel1MinSize = 200,
                Panel2MinSize = 100
            };
            split.Panel1.AutoScroll = true;
            split.Panel1.Padding = new Padding(0, 0, 0, 4);
            split.Panel1.Controls.Add(pnlFilters);
            split.Panel2.Controls.Add(dgvHuntProduct);

            var pnlHuntProductFooter = BuildHuntProductFooterPanel();
            pnlHuntProductFooter.Dock = DockStyle.Bottom;

            var pnlHuntProductStatus = BuildHuntProductStatusPanel();
            pnlHuntProductStatus.Dock = DockStyle.Bottom;

            var flpActions = BuildHuntProductActionsPanel();
            flpActions.Dock = DockStyle.Bottom;
            flpActions.Height = AffiliateJellyButtonMinHeight + 10;

            tab.Controls.Add(split);
            tab.Controls.Add(pnlHuntProductFooter);
            tab.Controls.Add(pnlHuntProductStatus);
            tab.Controls.Add(flpActions);

            void SyncSplit()
            {
                SyncHuntProductSplitDistance();
            }

            tab.Resize += (_, __) =>
            {
                SyncSplit();
                RefreshHuntProductFilterScrollLayout();
            };
            txtHuntProductKeyword.TextChanged += (_, __) => RefreshHuntProductDownloadFolderHint();
            RefreshHuntProductDownloadFolderHint();
            EnableHuntProductTabAutoScroll();
            SyncSplit();
            tab.HandleCreated += (_, __) =>
            {
                SyncSplit();
                RefreshHuntProductFilterScrollLayout();
            };
            RefreshHuntProductFilterScrollLayout();
        }
        private Panel BuildHuntProductFooterPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlHuntProductFooter",
                Dock = DockStyle.Fill,
                Height = 22,
                Padding = new Padding(6, 0, 6, 2),
                BackColor = Color.FromArgb(32, 34, 44)
            };

            lnkHuntProductDownloadFolder = new LinkLabel
            {
                Name = "lnkHuntProductDownloadFolder",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Color.FromArgb(140, 185, 255),
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.FromArgb(140, 185, 255),
                ForeColor = Color.FromArgb(180, 190, 210),
                Font = new Font("Segoe UI", 8.25F),
                UseMnemonic = false,
                Text = "📁 Thư mục media sản phẩm: …"
            };
            lnkHuntProductDownloadFolder.LinkClicked += LnkHuntProductDownloadFolder_LinkClicked;
            pnl.Controls.Add(lnkHuntProductDownloadFolder);
            return pnl;
        }

        private Panel BuildHuntProductStatusPanel()
        {
            var pnl = new Panel
            {
                Name = "pnlHuntProductStatusHost",
                Dock = DockStyle.Bottom,
                Height = 48,
                MinimumSize = new Size(0, 48),
                Padding = new Padding(8, 4, 8, 2),
                BackColor = Color.FromArgb(32, 34, 44)
            };

            lblHuntProductStatus = new Label
            {
                Name = "lblHuntProductStatus",
                Dock = DockStyle.Top,
                Height = 22,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(160, 210, 175),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Text = "Sẵn sàng — bấm «Quét sản phẩm» để bắt đầu."
            };

            pbHuntProductScan = new ProgressBar
            {
                Name = "pbHuntProductScan",
                Dock = DockStyle.Bottom,
                Height = 12,
                Style = ProgressBarStyle.Continuous,
                Visible = false,
                MarqueeAnimationSpeed = 28
            };

            pnl.Controls.Add(pbHuntProductScan);
            pnl.Controls.Add(lblHuntProductStatus);
            return pnl;
        }

        private static void AddHuntProductFilterRow(TableLayoutPanel tbl, int row, string labelText, Control editor)
        {
            if (tbl.RowCount <= row)
            {
                tbl.RowCount = row + 1;
            }

            if (editor is Panel buttonHostPanel && buttonHostPanel.MinimumSize.Height >= 40)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonHostPanel.MinimumSize.Height + 8));
            }
            else
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            if (!string.IsNullOrEmpty(labelText))
            {
                var lbl = CreateHuntProductFilterLabel(labelText);
                lbl.Dock = DockStyle.Fill;
                lbl.TextAlign = ContentAlignment.MiddleLeft;
                lbl.Margin = new Padding(0, 4, 8, 4);
                tbl.Controls.Add(lbl, 0, row);
            }

            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 4, 0, 4);
            if (string.IsNullOrEmpty(labelText))
            {
                tbl.SetColumnSpan(editor, 2);
                if (editor is Panel buttonHost)
                {
                    buttonHost.MinimumSize = new Size(0, 48);
                }

                tbl.Controls.Add(editor, 0, row);
            }
            else
            {
                tbl.Controls.Add(editor, 1, row);
            }
        }

        private static TableLayoutPanel CreateHuntProductFilterHalf(string labelText, Control editor, Padding margin)
        {
            const float editorWidth = HuntProductNumericEditorWidth;

            var half = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = margin
            };
            half.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            half.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MeasureHuntProductLabelTextWidth(labelText)));
            half.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, editorWidth));
            half.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lbl = CreateHuntProductRowLabel(labelText);
            lbl.Dock = DockStyle.Fill;
            lbl.Margin = new Padding(0, 0, 8, 0);

            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 6, 0, 6);
            StyleHuntProductEditor(editor, (int)editorWidth);
            editor.MaximumSize = new Size((int)editorWidth, HuntProductInputHeight);

            var spacer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            half.Controls.Add(lbl, 0, 0);
            half.Controls.Add(editor, 1, 0);
            half.Controls.Add(spacer, 2, 0);
            return half;
        }

        private static void AddHuntProductFilterPairRow(
            TableLayoutPanel tbl,
            int row,
            string label1,
            Control editor1,
            string label2,
            Control editor2)
        {
            if (tbl.RowCount <= row)
            {
                tbl.RowCount = row + 1;
            }

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, HuntProductFilterRowHeight));

            var pairTbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            pairTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            pairTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pairTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            pairTbl.Controls.Add(CreateHuntProductFilterHalf(label1, editor1, new Padding(0, 0, 8, 0)), 0, 0);
            pairTbl.Controls.Add(CreateHuntProductFilterHalf(label2, editor2, Padding.Empty), 1, 0);

            tbl.Controls.Add(pairTbl, 0, row);
        }

        private static void AddHuntProductFilterTripleRow(
            TableLayoutPanel tbl,
            int row,
            string label1,
            Control editor1,
            string label2,
            Control editor2,
            string label3,
            Control editor3)
        {
            if (tbl.RowCount <= row)
            {
                tbl.RowCount = row + 1;
            }

            const int rowHeight = HuntProductFilterRowHeight;
            const float keywordWidth = 300F;
            const float maxResultsWidth = HuntProductMaxResultsWidth;

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));

            var tripleTbl = new TableLayoutPanel
            {
                Name = "tblHuntProductTripleRow",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 7,
                RowCount = 1,
                Margin = Padding.Empty
            };
            tripleTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var labelW1 = MeasureHuntProductLabelTextWidth(label1);
            var labelW2 = MeasureHuntProductLabelTextWidth(label2);
            var labelW3 = MeasureHuntProductLabelTextWidth(label3);
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelW1));
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, keywordWidth));
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelW2));
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelW3));
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, maxResultsWidth));
            tripleTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            void AddCell(int col, Control control, bool fill, Padding margin)
            {
                control.Margin = margin;
                control.Dock = fill ? DockStyle.Fill : DockStyle.None;
                if (!fill)
                {
                    control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                }

                tripleTbl.Controls.Add(control, col, 0);
            }

            AddCell(0, CreateHuntProductRowLabel(label1), true, new Padding(0, 0, 6, 0));
            if (editor1 is TextBox txtKeyword)
            {
                StyleHuntProductEditor(txtKeyword);
                txtKeyword.MaximumSize = new Size((int)keywordWidth, HuntProductInputHeight);
            }

            AddCell(1, editor1, true, new Padding(0, 6, 12, 6));

            AddCell(2, CreateHuntProductRowLabel(label2), true, new Padding(0, 0, 6, 0));
            editor2.Margin = new Padding(0, 6, 12, 6);
            AddCell(3, editor2, false, Padding.Empty);

            AddCell(4, CreateHuntProductRowLabel(label3), true, new Padding(0, 0, 6, 0));
            StyleHuntProductEditor(editor3, (int)maxResultsWidth);
            editor3.MaximumSize = new Size((int)maxResultsWidth, HuntProductInputHeight);
            AddCell(5, editor3, true, new Padding(0, 6, 0, 6));

            var spacer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            AddCell(6, spacer, true, Padding.Empty);

            tbl.Controls.Add(tripleTbl, 0, row);
        }

        private Panel BuildHuntProductFiltersPanel()
        {
            var host = new Panel
            {
                Name = "pnlHuntProductFiltersHost",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };

            var grpAuto = new GroupBox
            {
                Name = "grpHuntProductAuto",
                Text = "Săn tự động",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ForeColor = Color.Gainsboro,
                Padding = new Padding(10, 6, 10, 10),
                Margin = new Padding(0, 0, 0, 8)
            };

            var tblAuto = new TableLayoutPanel
            {
                Name = "tblHuntProductAuto",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0
            };
            tblAuto.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            txtHuntProductKeyword = new TextBox
            {
                Name = "txtHuntProductKeyword",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro
            };
            StyleHuntProductEditor(txtHuntProductKeyword);
            ApplyTextBoxPlaceholder(txtHuntProductKeyword, "Nhập từ khoá sản phẩm…");

            var flpPlatforms = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            chkHuntProductTikTok = new CheckBox
            {
                Name = "chkHuntProductTikTok",
                Text = "TikTok Shop (tìm như người mua)",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Font = HuntProductFieldFont,
                Margin = new Padding(0, 0, 16, 0)
            };
            chkHuntProductShopee = new CheckBox
            {
                Name = "chkHuntProductShopee",
                Text = "Shopee",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Font = HuntProductFieldFont
            };
            flpPlatforms.Controls.Add(chkHuntProductTikTok);
            flpPlatforms.Controls.Add(chkHuntProductShopee);

            numHuntProductMaxResults = new NumericUpDown
            {
                Name = "numHuntProductMaxResults",
                Minimum = 5,
                Maximum = 200,
                Value = 30,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro
            };
            StyleHuntProductEditor(numHuntProductMaxResults, (int)HuntProductMaxResultsWidth);
            AddHuntProductFilterTripleRow(
                tblAuto,
                tblAuto.RowCount,
                "Từ khoá:",
                txtHuntProductKeyword,
                "Nền tảng:",
                flpPlatforms,
                "Số kết quả:",
                numHuntProductMaxResults);

            numHuntProductMinSales = new NumericUpDown
            {
                Name = "numHuntProductMinSales",
                Minimum = 0,
                Maximum = 999999999,
                Value = 0,
                ThousandsSeparator = true,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro
            };
            StyleHuntProductEditor(numHuntProductMinSales, (int)HuntProductNumericEditorWidth);
            numHuntProductMinRating = new NumericUpDown
            {
                Name = "numHuntProductMinRating",
                DecimalPlaces = 1,
                Increment = 0.1M,
                Minimum = 0,
                Maximum = 5,
                Value = 0,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro
            };
            StyleHuntProductEditor(numHuntProductMinRating, (int)HuntProductNumericEditorWidth);
            AddHuntProductFilterPairRow(
                tblAuto,
                tblAuto.RowCount,
                "Lượt bán tối thiểu (0 = bỏ qua):",
                numHuntProductMinSales,
                "Điểm đánh giá tối thiểu (0 = bỏ qua):",
                numHuntProductMinRating);

            cbHuntProductProfile = new ComboBox
            {
                Name = "cbHuntProductProfile",
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Width = 280
            };
            StyleHuntProductEditor(cbHuntProductProfile, 280);
            cbHuntProductProfile.Items.Add("default");
            cbHuntProductProfile.SelectedIndex = 0;
            AddHuntProductProfileRow(tblAuto, tblAuto.RowCount, cbHuntProductProfile);

            grpAuto.Controls.Add(tblAuto);
            host.Controls.Add(grpAuto);

            var grpManual = new GroupBox
            {
                Name = "grpHuntProductManual",
                Text = "Nhập thủ công",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ForeColor = Color.Gainsboro,
                Padding = new Padding(10, 6, 10, 10),
                Margin = Padding.Empty
            };

            var tblManual = new TableLayoutPanel
            {
                Name = "tblHuntProductManual",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0
            };
            tblManual.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            txtHuntProductManualLink = new TextBox
            {
                Name = "txtHuntProductManualLink",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.Gainsboro
            };
            StyleHuntProductEditor(txtHuntProductManualLink, font: HuntProductManualLinkFont);
            ApplyTextBoxPlaceholder(txtHuntProductManualLink, "https://…");

            btnHuntProductAddManual = CreateAffiliateJellyButton("btnHuntProductAddManual", "Thêm dòng", AffiliateTintPush);
            btnHuntProductAddManual.Click += btnHuntProductAddManual_Click;
            AddHuntProductManualLinkRow(tblManual, tblManual.RowCount, "Link sản phẩm:", txtHuntProductManualLink, btnHuntProductAddManual);

            grpManual.Controls.Add(tblManual);
            host.Controls.Add(grpManual);
            return host;
        }

        private static Label CreateHuntProductFilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 6, 6, 0),
                Padding = new Padding(0, 4, 0, 0)
            };
        }

        private static Label CreateHuntProductRowLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gainsboro,
                Font = HuntProductFieldFont,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        private static void AddHuntProductManualLinkRow(
            TableLayoutPanel tbl,
            int row,
            string labelText,
            TextBox txt,
            Button btn)
        {
            if (tbl.RowCount <= row)
            {
                tbl.RowCount = row + 1;
            }

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, HuntProductFilterRowHeight));

            var line = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty
            };
            line.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MeasureHuntProductLabelTextWidth(labelText)));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            var lbl = CreateHuntProductRowLabel(labelText);
            lbl.Dock = DockStyle.Fill;

            txt.Dock = DockStyle.Fill;
            txt.Margin = new Padding(0, 0, 12, 0);

            PrepareAffiliateToolbarButtonForFlow(btn);
            btn.Margin = new Padding(0, 4, 8, 4);
            btn.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            var spacer = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };

            line.Controls.Add(lbl, 0, 0);
            line.Controls.Add(txt, 1, 0);
            line.Controls.Add(btn, 2, 0);
            line.Controls.Add(spacer, 3, 0);
            tbl.Controls.Add(line, 0, row);
        }

        private static void AddHuntProductProfileRow(TableLayoutPanel tbl, int row, ComboBox cb)
        {
            if (tbl.RowCount <= row)
            {
                tbl.RowCount = row + 1;
            }

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, HuntProductFilterRowHeight));

            var line = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 0)
            };
            line.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MeasureHuntProductLabelTextWidth("Profile:")));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lbl = CreateHuntProductRowLabel("Profile:");
            lbl.Dock = DockStyle.Fill;

            cb.Margin = new Padding(0, 6, 0, 6);
            cb.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            line.Controls.Add(lbl, 0, 0);
            line.Controls.Add(cb, 1, 0);
            tbl.Controls.Add(line, 0, row);
        }

        private void SyncHuntProductSplitDistance()
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

            try
            {
                if (tabHuntProduct.ClientSize.Height <= 200)
                {
                    return;
                }

                filtersHost.PerformLayout();
                var footer = FindHuntProductControl(tabHuntProduct, "pnlHuntProductFooter");
                var actions = FindHuntProductControl(tabHuntProduct, "flpHuntProductActions");
                var chrome = (footer?.Height ?? 22) + (actions?.Height ?? 44) + split.SplitterWidth + 10;
                var width = Math.Max(split.Panel1.ClientSize.Width, 120);
                var preferred = filtersHost.GetPreferredSize(new Size(width, 0));
                var wanted = preferred.Height + split.Panel1.Padding.Vertical + 6;
                wanted = Math.Max(wanted, split.Panel1MinSize);
                var maxDist = tabHuntProduct.ClientSize.Height - chrome - split.Panel2MinSize;
                if (maxDist < wanted)
                {
                    wanted = Math.Max(split.Panel1MinSize, maxDist);
                }

                if (Math.Abs(split.SplitterDistance - wanted) > 2)
                {
                    split.SplitterDistance = wanted;
                }
            }
            catch
            {
            }
        }

        private static int MeasureHuntProductLabelTextWidth(string text)
        {
            var size = TextRenderer.MeasureText(
                text,
                HuntProductFieldFont,
                new Size(int.MaxValue, HuntProductFilterRowHeight),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            return size.Width + 10;
        }

        private DataGridView CreateHuntProductGrid()
        {
            var grid = new DataGridView
            {
                Name = "dgvHuntProduct",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = AppGridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Font = AppGridHeaderFont,
                Padding = new Padding(6, 8, 6, 8),
                WrapMode = DataGridViewTriState.False
            };
            grid.EnableHeadersVisualStyles = false;
            grid.DataSource = _huntProductBindingList;
            grid.DataBindingComplete += dgvHuntProduct_DataBindingComplete;
            return grid;
        }

        private void dgvHuntProduct_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (dgvHuntProduct?.Columns == null || dgvHuntProduct.Columns.Count == 0)
            {
                return;
            }

            foreach (DataGridViewColumn col in dgvHuntProduct.Columns)
            {
                var prop = col.DataPropertyName ?? string.Empty;
                switch (prop)
                {
                    case "ImageUrl":
                        col.Visible = false;
                        break;
                    case "SourcePlatform":
                        col.HeaderText = "Nền tảng";
                        col.FillWeight = 8;
                        col.MinimumWidth = 64;
                        break;
                    case "ProfileName":
                        col.HeaderText = "Profile";
                        col.FillWeight = 10;
                        col.MinimumWidth = 64;
                        break;
                    case "ProductName":
                        col.HeaderText = "Tên sản phẩm";
                        col.FillWeight = 40;
                        col.MinimumWidth = 120;
                        break;
                    case "ProductLink":
                        col.HeaderText = "Link";
                        col.FillWeight = 18;
                        col.MinimumWidth = 100;
                        break;
                    case "SalesVolume":
                        col.HeaderText = "Lượt bán";
                        col.FillWeight = 10;
                        col.MinimumWidth = 72;
                        col.DefaultCellStyle.Format = "N0";
                        break;
                    case "Rating":
                        col.HeaderText = "Điểm đánh giá";
                        col.FillWeight = 10;
                        col.MinimumWidth = 72;
                        col.DefaultCellStyle.Format = "0.0";
                        break;
                    case "Price":
                        col.HeaderText = "Giá";
                        col.FillWeight = 10;
                        col.MinimumWidth = 64;
                        break;
                    case "Commission":
                        col.HeaderText = "% Hoa hồng";
                        col.FillWeight = 10;
                        col.MinimumWidth = 72;
                        break;
                }
            }
        }

        private FlowLayoutPanel BuildHuntProductActionsPanel()
        {
            btnHuntProductAutoScan = CreateAffiliateJellyButton("btnHuntProductAutoScan", "Quét sản phẩm", AffiliateTintHunt);
            btnHuntProductAutoScan.Click += btnHuntProductAutoScan_Click;

            btnHuntProductDownloadMedia = CreateAffiliateJellyButton("btnHuntProductDownloadMedia", "Tải Media", AffiliateTintDownload);
            btnHuntProductDownloadMedia.Click += btnHuntProductDownloadMedia_Click;

            btnHuntProductPushDeep = CreateAffiliateJellyButton("btnHuntProductPushDeep", "Đẩy sang Affiliate chuyên sâu", AffiliateTintDeepDive);
            btnHuntProductPushDeep.Click += btnHuntProductPushDeep_Click;

            btnHuntProductDeleteRow = CreateAffiliateJellyButton("btnHuntProductDeleteRow", "Xóa dòng", AffiliateTintStop);
            btnHuntProductDeleteRow.Click += btnHuntProductDeleteRow_Click;


            btnHuntProductExportCsv = CreateAffiliateJellyButton("btnHuntProductExportCsv", "Xuất CSV", AffiliateTintNeutral);
            btnHuntProductExportCsv.Click += btnHuntProductExportCsv_Click;

            var flp = new FlowLayoutPanel
            {
                Name = "flpHuntProductActions",
                AutoSize = false,
                Height = AffiliateJellyButtonMinHeight + 10,
                MinimumSize = new Size(200, AffiliateJellyButtonMinHeight + 10),
                WrapContents = true,
                FlowDirection = FlowDirection.RightToLeft,
                AutoScroll = false,
                Padding = new Padding(0, 4, 0, 2),
                Margin = Padding.Empty,
                BackColor = Color.Transparent
            };

            foreach (var btn in new[] { btnHuntProductExportCsv, btnHuntProductDeleteRow, btnHuntProductPushDeep, btnHuntProductDownloadMedia, btnHuntProductAutoScan })
            {
                PrepareAffiliateToolbarButtonForFlow(btn);
                flp.Controls.Add(btn);
            }

            return flp;
        }

        private void RefreshHuntProductProfileCombo(AppSettings settings)
        {
            if (cbHuntProductProfile == null)
            {
                return;
            }

            var previous = cbHuntProductProfile.SelectedItem?.ToString();
            cbHuntProductProfile.Items.Clear();
            cbHuntProductProfile.Items.Add("default");
            foreach (var profile in settings?.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || cbHuntProductProfile.Items.Contains(name))
                {
                    continue;
                }

                cbHuntProductProfile.Items.Add(name);
            }

            var target = string.IsNullOrWhiteSpace(previous) ? "default" : previous.Trim();
            var index = cbHuntProductProfile.Items.IndexOf(target);
            cbHuntProductProfile.SelectedIndex = index >= 0 ? index : 0;
        }

        private void RefreshAffiliateHuntProfileCombo(AppSettings settings)
        {
            if (cbAffiliateHuntProfile == null)
            {
                return;
            }

            var previous = cbAffiliateHuntProfile.SelectedItem?.ToString();
            cbAffiliateHuntProfile.Items.Clear();
            cbAffiliateHuntProfile.Items.Add("default");
            foreach (var profile in settings?.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || cbAffiliateHuntProfile.Items.Contains(name))
                {
                    continue;
                }

                cbAffiliateHuntProfile.Items.Add(name);
            }

            var target = string.IsNullOrWhiteSpace(previous) ? "default" : previous.Trim();
            var index = cbAffiliateHuntProfile.Items.IndexOf(target);
            cbAffiliateHuntProfile.SelectedIndex = index >= 0 ? index : 0;
        }

        private void btnHuntProductAddManual_Click(object sender, EventArgs e)
        {
            if (_huntProductBindingList == null)
            {
                return;
            }

            var link = (txtHuntProductManualLink?.Text ?? string.Empty).Trim();
            if (!IsValidHttpUrl(link))
            {
                MessageBox.Show(this, "Nhập link sản phẩm hợp lệ (http/https).", "Săn Link Sản phẩm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtHuntProductManualLink?.Focus();
                return;
            }

            var profile = cbHuntProductProfile?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(profile))
            {
                profile = GetRunningProfileName();
            }

            var platform = link.IndexOf("shopee", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Shopee"
                : (link.IndexOf("tiktok", StringComparison.OrdinalIgnoreCase) >= 0 ? "TikTok" : "Web");

            _huntProductBindingList.Add(new HuntProductCandidate
            {
                ProfileName = profile,
                SourcePlatform = platform,
                ProductName = "Sản phẩm " + (_huntProductBindingList.Count + 1),
                ProductLink = link,
                SalesVolume = 0,
                Rating = 0,
                Price = string.Empty,
                Commission = string.Empty
            });

            txtHuntProductManualLink.Clear();
            if (dgvHuntProduct != null && dgvHuntProduct.Rows.Count > 0)
            {
                var idx = dgvHuntProduct.Rows.Count - 1;
                dgvHuntProduct.ClearSelection();
                dgvHuntProduct.Rows[idx].Selected = true;
                dgvHuntProduct.FirstDisplayedScrollingRowIndex = idx;
            }

            Log("Đã thêm link sản phẩm thủ công vào bảng.");
        }

        private void btnHuntProductDeleteRow_Click(object sender, EventArgs e)
        {
            if (_huntProductBindingList == null || dgvHuntProduct == null)
            {
                return;
            }

            var toRemove = new List<HuntProductCandidate>();
            foreach (DataGridViewRow row in dgvHuntProduct.SelectedRows)
            {
                if (row.DataBoundItem is HuntProductCandidate item)
                {
                    toRemove.Add(item);
                }
            }

            if (toRemove.Count == 0)
            {
                Log("Chọn ít nhất một dòng để xóa.");
                return;
            }

            foreach (var item in toRemove)
            {
                _huntProductBindingList.Remove(item);
            }

            Log("Đã xóa " + toRemove.Count + " dòng sản phẩm.");
        }

        private void btnHuntProductExportCsv_Click(object sender, EventArgs e)
        {
            if (_huntProductBindingList == null || _huntProductBindingList.Count == 0)
            {
                Log("Không có dòng sản phẩm để xuất CSV.");
                return;
            }

            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.FileName = "hunt_products_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                    dialog.Filter = "CSV (*.csv)|*.csv|Tất cả|*.*";
                    dialog.Title = "Xuất CSV sản phẩm";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    var lines = new List<string>
                    {
                        "Profile,Tên sản phẩm,Link,Lượt bán,Điểm đánh giá,Giá,% Hoa hồng"
                    };
                    foreach (var row in _huntProductBindingList)
                    {
                        lines.Add(string.Join(",",
                            EscapeHuntProductCsv(row.ProfileName),
                            EscapeHuntProductCsv(row.ProductName),
                            EscapeHuntProductCsv(row.ProductLink),
                            row.SalesVolume.ToString(),
                            row.Rating.ToString("0.0"),
                            EscapeHuntProductCsv(row.Price),
                            EscapeHuntProductCsv(row.Commission)));
                    }

                    File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
                    Log("Đã xuất CSV sản phẩm: " + dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log("Xuất CSV sản phẩm thất bại: " + ex.Message);
            }
        }

        private static string EscapeHuntProductCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private void btnHuntProductPushDeep_Click(object sender, EventArgs e)
        {
            Log("Đẩy sang Affiliate chuyên sâu: tính năng sẽ được bổ sung trong bản cập nhật tiếp theo.");
        }
    }
}
