using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private GroupBox CreateSettingsGroupBox(string title, Color? backColor = null)
        {
            return new GroupBox
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 12, 8, 8),
                Margin = new Padding(0, 0, 0, 8),
                ForeColor = Color.FromArgb(200, 205, 215),
                BackColor = backColor ?? Color.FromArgb(36, 39, 48),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                FlatStyle = FlatStyle.Flat
            };
        }

        private TableLayoutPanel CreateSettingsStackTable()
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return tbl;
        }

        private Label CreateSettingCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 0, 4),
                Dock = DockStyle.Top
            };
        }

        private TextBox CreateSettingField(string name, bool isSecret)
        {
            var field = new TextBox
            {
                Name = name,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                UseSystemPasswordChar = isSecret,
                MinimumSize = new Size(160, AppInputMinHeight),
                Margin = new Padding(0, 0, 6, 0)
            };
            ApplyAppInputChrome(field);
            return field;
        }

        private Button CreateSettingBrowseButton(string name, string text)
        {
            return CreateSettingsGreenFlatButton(name, text);
        }

        private JellyButton CreateSettingsGreenFlatButton(string name, string text, int minWidth = 70, int height = 32)
        {
            var width = Math.Max(minWidth, MeasureProfileButtonTextWidth(text, ProfileToolbarButtonFont, height) + 22);
            return new JellyButton
            {
                Name = name,
                Text = text,
                Font = ProfileToolbarButtonFont,
                JellyTint = SettingsTintGreen,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                ForeColor = SettingsButtonFore,
                AutoSize = false,
                Size = new Size(width, height),
                MinimumSize = new Size(width, height),
                Margin = new Padding(0, 0, 6, 0)
            };
        }

        private Button CreateSettingsFlatBlueButton(
            string name,
            string text,
            int height,
            int horizontalPad,
            int minWidth = 72,
            bool lockWidth = true,
            Padding? margin = null)
        {
            var width = Math.Max(
                minWidth,
                MeasureProfileButtonTextWidth(text, ProfileToolbarButtonFont, height) + horizontalPad);

            var button = new Button
            {
                Name = name,
                Text = text,
                Font = ProfileToolbarButtonFont,
                ForeColor = SettingsButtonFore,
                AutoSize = false,
                Height = height,
                MinimumSize = new Size(width, height),
                Margin = margin ?? new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = SettingsActionBlue,
                Cursor = Cursors.Hand,
                AccessibleName = SettingsActionChromeTag,
                Tag = SettingsActionChromeTag,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(4, 2, 4, 2)
            };

            if (lockWidth)
            {
                button.Size = new Size(width, height);
                button.MaximumSize = new Size(width, height + 2);
            }

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = SettingsActionBlueOver;
            button.FlatAppearance.MouseDownBackColor = SettingsActionBlueDown;
            return button;
        }

        private Button CreateSettingsShowTestButton(string name, string englishLabel, int minWidth = 72, int height = 32)
        {
            const int horizontalPad = 18;
            var displayText = LocalizeDisplayText(englishLabel);
            var width = Math.Max(
                minWidth,
                MeasureProfileButtonTextWidth(displayText, ProfileToolbarButtonFont, height) + horizontalPad);

            return CreateSettingsFlatBlueButton(
                name,
                englishLabel,
                height,
                horizontalPad,
                width,
                lockWidth: true);
        }

        private Button CreateSettingsShowToggleButton(string name, int minWidth = 72, int height = 32)
        {
            const int horizontalPad = 18;
            var showLabel = LocalizeDisplayText("Show");
            var hideLabel = LocalizeDisplayText("Hide");
            var width = Math.Max(
                minWidth,
                Math.Max(
                    MeasureProfileButtonTextWidth(showLabel, ProfileToolbarButtonFont, height),
                    MeasureProfileButtonTextWidth(hideLabel, ProfileToolbarButtonFont, height))
                + horizontalPad);

            return CreateSettingsShowTestButton(name, "Show", width, height);
        }

        private static readonly Font ProfileToolbarButtonFont = new Font("Segoe UI", 10.25F, FontStyle.Bold);
        private const string SettingsActionChromeTag = "SettingsActionChrome";
        private static readonly Color SettingsTintGreen = Color.FromArgb(56, 158, 88);
        private static readonly Color ProfileTintLoginAll = Color.FromArgb(88, 101, 242);
        private static readonly Color ProfileTintRefresh = Color.FromArgb(48, 176, 199);
        private static readonly Color ProfileTintTikTok = Color.FromArgb(78, 82, 96);
        private static readonly Color ProfileTintFacebook = Color.FromArgb(37, 99, 235);
        private static readonly Color ProfileTintYouTube = Color.FromArgb(220, 48, 48);
        private static readonly Color ProfileTintScan = Color.FromArgb(200, 130, 45);
        private static readonly Color SettingsActionBlue = Color.FromArgb(52, 120, 220);
        private static readonly Color SettingsActionBlueOver = Color.FromArgb(72, 140, 235);
        private static readonly Color SettingsActionBlueDown = Color.FromArgb(40, 100, 200);
        private static readonly Color SettingsButtonFore = Color.FromArgb(245, 247, 250);

        private static int MeasureProfileButtonTextWidth(string text, Font font, int buttonHeight)
        {
            return TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, buttonHeight),
                TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.GlyphOverhangPadding).Width;
        }

        private JellyButton CreateProfileJellyButton(string name, string text, Color tint)
        {
            const int buttonHeight = 34;

            return new JellyButton
            {
                Name = name,
                Text = text,
                Font = ProfileToolbarButtonFont,
                JellyTint = tint,
                JellyFillOpacity = 1f - JellyButton.DefaultTransparency,
                AutoSize = false,
                Height = buttonHeight,
                MinimumSize = new Size(72, buttonHeight),
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
        }

        private JellyButton CreateSettingsSaveButton(string name, string text)
        {
            const int buttonHeight = 48;
            const int buttonWidth = 196;

            return new JellyButton
            {
                Name = name,
                Text = text,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                JellyTint = Color.FromArgb(28, 156, 72),
                JellyFillOpacity = 1f,
                AutoSize = false,
                Size = new Size(buttonWidth, buttonHeight),
                MinimumSize = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.None,
                Margin = new Padding(8, 0, 8, 2),
                Cursor = Cursors.Hand
            };
        }

        private TableLayoutPanel CreateProfileToolbarTable(params Control[] buttons)
        {
            const int buttonHeight = 34;
            const int toolbarTopMargin = 2;
            const int toolbarBottomMargin = 4;
            const int horizontalPad = 28;
            const int gap = 6;

            var weights = new float[buttons.Length];
            var totalWeight = 0f;
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.AutoSize = false;
                button.Height = buttonHeight;
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 0, i < buttons.Length - 1 ? gap : 0, 0);

                var minWidth = Math.Max(72, MeasureProfileButtonTextWidth(button.Text, button.Font, buttonHeight) + horizontalPad);
                weights[i] = minWidth;
                totalWeight += minWidth;
            }

            if (totalWeight <= 0f)
            {
                totalWeight = buttons.Length;
                for (var i = 0; i < buttons.Length; i++)
                {
                    weights[i] = 1f;
                }
            }

            var tbl = new TableLayoutPanel
            {
                Name = "tblProfileToolbar",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = buttons.Length,
                RowCount = 1,
                Margin = new Padding(0, toolbarTopMargin, 0, toolbarBottomMargin),
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(36, 39, 48)
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonHeight));

            for (var i = 0; i < buttons.Length; i++)
            {
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f * weights[i] / totalWeight));
                tbl.Controls.Add(buttons[i], i, 0);
            }

            return tbl;
        }

        private Button CreateSettingsFlatButton(string name, string text, Color back, Color fore)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(70, 30),
                BackColor = back,
                FlatStyle = FlatStyle.Flat,
                ForeColor = fore,
                Margin = new Padding(0, 0, 6, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void AddSettingsTableRow(TableLayoutPanel tbl, ref int row, Control c, SizeType sizeType = SizeType.AutoSize, float rowHeight = 100F)
        {
            while (tbl.RowStyles.Count <= row)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            tbl.RowStyles[row] = new RowStyle(sizeType, rowHeight);
            tbl.RowCount = Math.Max(tbl.RowCount, row + 1);
            c.Dock = DockStyle.Fill;
            tbl.Controls.Add(c, 0, row);
            row++;
        }

        private void AddSettingsSecretKeyBlock(
            TableLayoutPanel tbl,
            ref int row,
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            field = CreateSettingField(fieldName, true);
            field.Dock = DockStyle.Fill;

            var toggleName = fieldName switch
            {
                "txtAiApiKey" => "btnToggleAiApiKey",
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtTikTokRapidApiKey" => "btnToggleTikTokRapidApiKey",
                "txtTtsApiKey" => "btnToggleTtsApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
                "txtTikTokRapidApiKey" => "btnTestTikTokRapidApi",
                "txtTtsApiKey" => "btnTestTts",
                "txtVeoApiKey" => "btnTestVeo",
                _ => "btnTest" + fieldName.Substring(3)
            };

            toggle = CreateSettingsShowToggleButton(toggleName);
            test = CreateSettingsShowTestButton(testName, "Test");
            var secretField = field;
            var secretToggle = toggle;
            toggle.Click += (sender, e) => ToggleSecretVisibility(secretField, secretToggle);
            test.Click += testClick;

            var rowPanel = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6)
            };
            rowPanel.Controls.Add(CreateSettingsWrapFlow(FlowDirection.LeftToRight, field, toggle, test));
            rowPanel.Controls.Add(CreateSettingCaption(caption));
            AddSettingsTableRow(tbl, ref row, rowPanel);
        }

        private Control CreateSettingsSecretKeyCell(
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            field = CreateSettingField(fieldName, true);
            field.Dock = DockStyle.Fill;
            field.MinimumSize = new Size(80, 28);

            var toggleName = fieldName switch
            {
                "txtAiApiKey" => "btnToggleAiApiKey",
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtTikTokRapidApiKey" => "btnToggleTikTokRapidApiKey",
                "txtTtsApiKey" => "btnToggleTtsApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
                "txtTikTokRapidApiKey" => "btnTestTikTokRapidApi",
                "txtTtsApiKey" => "btnTestTts",
                "txtVeoApiKey" => "btnTestVeo",
                _ => "btnTest" + fieldName.Substring(3)
            };

            toggle = CreateSettingsShowToggleButton(toggleName);
            test = CreateSettingsShowTestButton(testName, "Test");
            var secretField = field;
            var secretToggle = toggle;
            toggle.Click += (sender, e) => ToggleSecretVisibility(secretField, secretToggle);
            test.Click += testClick;

            var cell = new TableLayoutPanel
            {
                Name = "cell" + fieldName,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 8, 0),
                Padding = Padding.Empty
            };
            cell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var captionLabel = CreateSettingCaption(caption);
            captionLabel.Dock = DockStyle.Fill;
            captionLabel.Margin = new Padding(0, 0, 0, 2);
            cell.Controls.Add(captionLabel, 0, 0);
            cell.Controls.Add(field, 0, 1);

            var btnRow = CreateSettingsWrapFlow(FlowDirection.LeftToRight, toggle, test);
            btnRow.Dock = DockStyle.Fill;
            cell.Controls.Add(btnRow, 0, 2);

            return cell;
        }

        private Control CreateSettingsSecretKeyInlineCell(
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            field = CreateSettingField(fieldName, true);
            field.Height = 28;
            field.MinimumSize = new Size(80, 28);
            field.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            var toggleName = fieldName switch
            {
                "txtAiApiKey" => "btnToggleAiApiKey",
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtTikTokRapidApiKey" => "btnToggleTikTokRapidApiKey",
                "txtTtsApiKey" => "btnToggleTtsApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
                "txtTikTokRapidApiKey" => "btnTestTikTokRapidApi",
                "txtTtsApiKey" => "btnTestTts",
                "txtVeoApiKey" => "btnTestVeo",
                _ => "btnTest" + fieldName.Substring(3)
            };

            toggle = CreateSettingsShowToggleButton(toggleName);
            test = CreateSettingsShowTestButton(testName, "Test");
            var secretField = field;
            var secretToggle = toggle;
            toggle.Click += (sender, e) => ToggleSecretVisibility(secretField, secretToggle);
            test.Click += testClick;
            toggle.Margin = new Padding(4, 0, 0, 0);
            test.Margin = new Padding(4, 0, 0, 0);

            var cell = new TableLayoutPanel
            {
                Name = "cellInline" + fieldName,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 6, 0),
                Padding = Padding.Empty
            };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var captionLabel = CreateSettingCaption(caption);
            captionLabel.AutoSize = true;
            captionLabel.Anchor = AnchorStyles.Left;
            captionLabel.Margin = new Padding(0, 6, 6, 0);

            cell.Controls.Add(captionLabel, 0, 0);
            cell.Controls.Add(field, 1, 0);
            cell.Controls.Add(toggle, 2, 0);
            cell.Controls.Add(test, 3, 0);

            return cell;
        }

        private Control CreateSettingsCompactSecretKeyInlineCell(
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick,
            int fieldWidth = 200)
        {
            field = CreateSettingField(fieldName, true);
            field.Height = 30;
            field.Width = fieldWidth;
            field.MinimumSize = new Size(fieldWidth, 30);
            field.MaximumSize = new Size(fieldWidth, 30);
            field.Anchor = AnchorStyles.Left;

            var toggleName = fieldName switch
            {
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtTikTokRapidApiKey" => "btnToggleTikTokRapidApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
                "txtTikTokRapidApiKey" => "btnTestTikTokRapidApi",
                _ => "btnTest" + fieldName.Substring(3)
            };

            toggle = CreateSettingsShowToggleButton(toggleName, minWidth: 52, height: 32);
            test = CreateSettingsShowTestButton(testName, "Test", minWidth: 72, height: 32);
            var secretField = field;
            var secretToggle = toggle;
            toggle.Click += (sender, e) => ToggleSecretVisibility(secretField, secretToggle);
            test.Click += testClick;
            toggle.Margin = new Padding(4, 0, 0, 0);
            test.Margin = new Padding(4, 0, 0, 0);

            var cell = new FlowLayoutPanel
            {
                Name = "cellCompact" + fieldName,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(0, 0, 0, 2)
            };

            var captionLabel = CreateSettingCaption(caption);
            captionLabel.AutoSize = true;
            captionLabel.Margin = new Padding(0, 7, 6, 0);

            cell.Controls.Add(captionLabel);
            cell.Controls.Add(field);
            cell.Controls.Add(toggle);
            cell.Controls.Add(test);

            return cell;
        }

        private Control CreateSettingsGatewaySecondRow(
            out TextBox captchaField,
            out Button captchaToggle,
            out Button captchaTest,
            EventHandler captchaTestClick,
            out TextBox tikTokRapidField,
            out Button tikTokRapidToggle,
            out Button tikTokRapidTest,
            EventHandler tikTokRapidTestClick,
            out JellyButton saveButton,
            EventHandler saveClick)
        {
            var captchaCell = CreateSettingsCompactSecretKeyInlineCell(
                "2Captcha",
                "txtTwoCaptchaApiKey",
                out captchaField,
                out captchaToggle,
                out captchaTest,
                captchaTestClick);

            var rapidCell = CreateSettingsCompactSecretKeyInlineCell(
                "TikTok RapidAPI (tiktok-api23)",
                "txtTikTokRapidApiKey",
                out tikTokRapidField,
                out tikTokRapidToggle,
                out tikTokRapidTest,
                tikTokRapidTestClick);

            saveButton = CreateSettingsSaveButton("btnSaveSettings", "L\u01b0u c\u00e0i \u0111\u1eb7t");
            saveButton.Click += saveClick;
            var saveBtnLocal = saveButton;

            var row = new Panel
            {
                Name = "pnlGatewaySecondRow",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Margin = new Padding(0, 6, 0, 2),
                Padding = new Padding(0, 2, 0, 10),
                MinimumSize = new Size(0, 60)
            };

            var keysFlow = new FlowLayoutPanel
            {
                Name = "flpGatewaySecondKeys",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            captchaCell.Margin = new Padding(0, 0, 12, 2);
            rapidCell.Margin = new Padding(0, 0, 0, 2);
            keysFlow.Controls.Add(captchaCell);
            keysFlow.Controls.Add(rapidCell);

            saveBtnLocal.Anchor = AnchorStyles.None;
            row.Controls.Add(keysFlow);
            row.Controls.Add(saveBtnLocal);

            void LayoutSecondRow()
            {
                var contentHeight = row.ClientSize.Height;
                var contentWidth = row.ClientSize.Width;
                keysFlow.Location = new Point(0, Math.Max(0, (contentHeight - keysFlow.Height) / 2));
                saveBtnLocal.Location = new Point(
                    Math.Max(0, (contentWidth - saveBtnLocal.Width) / 2),
                    Math.Max(0, (contentHeight - saveBtnLocal.Height) / 2));
            }

            row.Resize += (sender, e) => LayoutSecondRow();
            row.HandleCreated += (sender, e) => LayoutSecondRow();
            LayoutSecondRow();

            return row;
        }

        private Control CreateSettingsSecretKeyAlignedColumn(
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            var keyCell = CreateSettingsSecretKeyInlineCell(caption, fieldName, out field, out toggle, out test, testClick);

            var col = new TableLayoutPanel
            {
                Name = "colKey" + fieldName,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 6, 0),
                Padding = Padding.Empty
            };
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
            col.Controls.Add(keyCell, 0, 1);

            return col;
        }

        private Control CreateSettingsCompactLabeledField(string caption, TextBox field)
        {
            field.Height = 26;
            field.MinimumSize = new Size(60, 26);
            field.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var captionLabel = CreateSettingCaption(caption);
            captionLabel.AutoSize = true;
            captionLabel.Anchor = AnchorStyles.Left;
            captionLabel.Margin = new Padding(0, 5, 6, 0);

            row.Controls.Add(captionLabel, 0, 0);
            row.Controls.Add(field, 1, 0);

            return row;
        }

        private Control CreateSettingsGeminiColumn(
            out TextBox providerField,
            out TextBox modelField,
            out TextBox apiKeyField,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            providerField = CreateSettingField("txtAiProvider", false);
            modelField = CreateSettingField("txtAiModel", false);

            var providerRow = CreateSettingsCompactLabeledField("Provider", providerField);
            var modelRow = CreateSettingsCompactLabeledField("Model", modelField);
            var keyCell = CreateSettingsSecretKeyInlineCell("Gemini", "txtAiApiKey", out apiKeyField, out toggle, out test, testClick);
            if (keyCell is TableLayoutPanel keyTbl)
            {
                keyTbl.Margin = new Padding(0, 0, 6, 0);
            }

            var col = new TableLayoutPanel
            {
                Name = "colGatewayGemini",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 8, 0),
                Padding = Padding.Empty
            };
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.Controls.Add(providerRow, 0, 0);
            col.Controls.Add(modelRow, 0, 1);
            col.Controls.Add(keyCell, 0, 2);

            return col;
        }

        private Control CreateSettingsGatewayPairColumn(
            string serviceTitle,
            string urlFieldName,
            out TextBox urlField,
            string keyCaption,
            string keyFieldName,
            out TextBox keyField,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            urlField = CreateSettingField(urlFieldName, false);
            urlField.Height = 28;
            urlField.MinimumSize = new Size(80, 28);
            urlField.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            var urlRow = new TableLayoutPanel
            {
                Name = "rowUrl" + keyFieldName,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            urlRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var svcLabel = new Label
            {
                Text = serviceTitle,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 210, 225),
                Margin = new Padding(0, 6, 8, 0),
                Anchor = AnchorStyles.Left
            };
            urlRow.Controls.Add(svcLabel, 0, 0);
            urlRow.Controls.Add(urlField, 1, 0);

            var keyCell = CreateSettingsSecretKeyInlineCell(keyCaption, keyFieldName, out keyField, out toggle, out test, testClick);
            if (keyCell is TableLayoutPanel keyTbl)
            {
                keyTbl.Margin = new Padding(0, 0, 6, 0);
            }

            var col = new TableLayoutPanel
            {
                Name = "colGateway" + keyFieldName,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 8, 0),
                Padding = Padding.Empty
            };
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.Controls.Add(urlRow, 0, 0);
            col.Controls.Add(keyCell, 0, 1);

            return col;
        }

        private Control CreateSettingsCompactPathRow(
            out TextBox textBox,
            out Button browse,
            string shortLabel,
            string fieldName,
            EventHandler browseClick,
            string browseText = "Duyệt",
            string browseName = null,
            params (string name, string text, Color back, EventHandler click)[] extraButtons)
        {
            textBox = CreateSettingField(fieldName, false);
            textBox.Dock = DockStyle.Fill;
            textBox.MinimumSize = new Size(80, 26);
            textBox.Margin = new Padding(0, 0, 4, 0);

            browse = CreateSettingBrowseButton(
                browseName ?? ("btnBrowse" + fieldName.Substring(3)),
                browseText);
            browse.MinimumSize = new Size(64, 28);
            browse.Click += browseClick;

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3 + (extraButtons?.Length ?? 0),
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = shortLabel,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 185, 198),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 4, 0)
            };
            row.Controls.Add(lbl, 0, 0);
            row.Controls.Add(textBox, 1, 0);
            row.Controls.Add(browse, 2, 0);

            if (extraButtons != null)
            {
                var col = 3;
                foreach (var extra in extraButtons)
                {
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    var btn = CreateSettingsGreenFlatButton(extra.name, extra.text);
                    btn.Click += extra.click;
                    btn.MinimumSize = new Size(72, 28);
                    row.Controls.Add(btn, col, 0);
                    col++;
                }
            }

            return row;
        }

        private Control CreateSettingsToolPathInlineCell(
            string shortLabel,
            string fieldName,
            out TextBox textBox,
            out Button browse,
            out Button download,
            EventHandler browseClick,
            EventHandler downloadClick,
            string browseName,
            string downloadName)
        {
            textBox = CreateSettingField(fieldName, false);
            textBox.Height = 26;
            textBox.MinimumSize = new Size(60, 26);
            textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            browse = CreateSettingsGreenFlatButton(browseName, "Duyệt", minWidth: 52, height: 26);
            browse.Click += browseClick;
            browse.Margin = new Padding(4, 0, 0, 0);

            download = CreateSettingsGreenFlatButton(downloadName, "\u2B07 T\u1EA3i", minWidth: 52, height: 26);
            download.Click += downloadClick;
            download.Margin = new Padding(4, 0, 0, 0);

            var cell = new TableLayoutPanel
            {
                Name = "cellTool" + fieldName,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 6, 0),
                Padding = Padding.Empty
            };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = shortLabel,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(180, 185, 198),
                Margin = new Padding(0, 5, 6, 0)
            };

            cell.Controls.Add(lbl, 0, 0);
            cell.Controls.Add(textBox, 1, 0);
            cell.Controls.Add(browse, 2, 0);
            cell.Controls.Add(download, 3, 0);

            return cell;
        }

        private Control CreateSettingsPathRow(
            out TextBox textBox,
            out Button browse,
            string label,
            string fieldName,
            EventHandler browseClick,
            string browseText = "Browse",
            string browseName = null,
            params (string name, string text, Color back, EventHandler click)[] extraButtons)
        {
            textBox = CreateSettingField(fieldName, false);
            textBox.Dock = DockStyle.Fill;
            textBox.MinimumSize = new Size(200, 28);

            browse = CreateSettingBrowseButton(
                browseName ?? ("btnBrowse" + fieldName.Substring(3)),
                browseText);
            browse.Click += browseClick;

            var controls = new System.Collections.Generic.List<Control> { textBox, browse };
            if (extraButtons != null)
            {
                foreach (var extra in extraButtons)
                {
                    var btn = CreateSettingsGreenFlatButton(extra.name, extra.text, 72, 28);
                    btn.Click += extra.click;
                    controls.Add(btn);
                }
            }

            var container = new Panel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6)
            };
            container.Controls.Add(CreateSettingsWrapFlow(FlowDirection.LeftToRight, controls.ToArray()));
            container.Controls.Add(CreateSettingCaption(label));
            return container;
        }

        private FlowLayoutPanel CreateSettingsWrapFlow(FlowDirection direction, params Control[] controls)
        {
            var flp = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                FlowDirection = direction,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            if (controls != null)
            {
                foreach (var control in controls)
                {
                    if (control != null)
                    {
                        flp.Controls.Add(control);
                    }
                }
            }

            return flp;
        }

        private static void AddSettingsInlinePairRow(
            TableLayoutPanel tbl,
            ref int row,
            string labelLeft,
            Control controlLeft,
            string labelRight,
            Control controlRight)
        {
            tbl.RowCount = Math.Max(tbl.RowCount, row + 1);
            while (tbl.RowStyles.Count <= row)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            var lblLeft = new Label
            {
                Text = labelLeft,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 185, 198),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 4, 4)
            };
            var lblRight = new Label
            {
                Text = labelRight ?? string.Empty,
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 185, 198),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 0, 4, 4)
            };

            if (controlLeft != null)
            {
                controlLeft.Dock = DockStyle.Fill;
                controlLeft.Margin = new Padding(0, 0, 0, 4);
                controlLeft.MinimumSize = new Size(60, 26);
            }

            if (controlRight != null)
            {
                controlRight.Dock = DockStyle.Fill;
                controlRight.Margin = new Padding(0, 0, 0, 4);
                controlRight.MinimumSize = new Size(60, 26);
            }

            tbl.Controls.Add(lblLeft, 0, row);
            if (controlLeft != null)
            {
                tbl.Controls.Add(controlLeft, 1, row);
            }

            tbl.Controls.Add(lblRight, 2, row);
            if (controlRight != null)
            {
                tbl.Controls.Add(controlRight, 3, row);
            }

            row++;
        }

        private static DataGridViewTextBoxColumn CreateProxyProfileStatusColumn(string name, string header, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = width,
                MinimumWidth = width,
                Resizable = DataGridViewTriState.False
            };
        }

        private void BuildSettingUi()
        {
            tabSetting.SuspendLayout();
            tabSetting.AutoScroll = false;
            tabSetting.Controls.Clear();

            var grpPlatformLogin = CreateSettingsGroupBox("Đăng nhập nền tảng (Chrome profile)");
            grpPlatformLogin.Name = "grpPlatformLogin";

            btnLoginAllSocial = CreateProfileJellyButton(
                "btnLoginAllSocial",
                "Đăng nhập 3 nền tảng (1 Chrome)",
                ProfileTintLoginAll);
            btnLoginAllSocial.Click += btnLoginAllSocial_Click;

            btnRefreshProfileLoginStatus = CreateProfileJellyButton(
                "btnRefreshProfileLoginStatus",
                "Làm mới trạng thái login",
                ProfileTintRefresh);
            btnRefreshProfileLoginStatus.Click += btnRefreshProfileLoginStatus_Click;

            btnOpenTikTokManualBrowser = CreateProfileJellyButton(
                "btnOpenTikTokManualBrowser",
                "Tiktok",
                ProfileTintTikTok);
            btnOpenTikTokManualBrowser.Click += btnOpenTikTokLoginBrowser_Click;

            btnLoginFacebook = CreateProfileJellyButton(
                "btnLoginFacebook",
                "Facebook",
                ProfileTintFacebook);
            btnLoginFacebook.Click += btnLoginFacebook_Click;

            btnLoginYouTube = CreateProfileJellyButton(
                "btnLoginYouTube",
                "Youtube",
                ProfileTintYouTube);
            btnLoginYouTube.Click += btnLoginYouTube_Click;

            btnCheckBrowserProfileHealth = CreateProfileJellyButton(
                "btnCheckBrowserProfileHealth",
                "Quét thư mục profile",
                ProfileTintScan);
            btnCheckBrowserProfileHealth.Click += btnCheckBrowserProfileHealth_Click;

            var tblPlatformLogin = CreateProfileToolbarTable(
                btnLoginAllSocial,
                btnRefreshProfileLoginStatus,
                btnOpenTikTokManualBrowser,
                btnLoginFacebook,
                btnLoginYouTube,
                btnCheckBrowserProfileHealth);
            tblPlatformLogin.Dock = DockStyle.Top;
            grpPlatformLogin.Controls.Add(tblPlatformLogin);

            var grpGatewayTtsVeo = CreateSettingsGroupBox("Gateway — TTS · Veo · Gemini · 2Captcha");
            grpGatewayTtsVeo.Name = "grpGatewayTtsVeo";
            grpGatewayTtsVeo.Padding = new Padding(6, 10, 6, 12);

            var tblGatewayRoot = new TableLayoutPanel
            {
                Name = "tblGatewayRoot",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblGatewayRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblGatewayRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var tblGatewayMain = new TableLayoutPanel
            {
                Name = "tblGatewayTtsVeo",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblGatewayMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tblGatewayMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tblGatewayMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tblGatewayMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblGatewayMain.Controls.Add(
                CreateSettingsGatewayPairColumn(
                    "TTS · URL",
                    "txtTtsEndpoint",
                    out txtTtsEndpoint,
                    "Khóa TTS",
                    "txtTtsApiKey",
                    out txtTtsApiKey,
                    out btnToggleTtsApiKey,
                    out btnTestTts,
                    btnTestTts_Click),
                0,
                0);
            tblGatewayMain.Controls.Add(
                CreateSettingsGatewayPairColumn(
                    "Veo · URL",
                    "txtVeoEndpoint",
                    out txtVeoEndpoint,
                    "Khóa Veo",
                    "txtVeoApiKey",
                    out txtVeoApiKey,
                    out btnToggleVeoApiKey,
                    out btnTestVeo,
                    btnTestVeo_Click),
                1,
                0);
            tblGatewayMain.Controls.Add(
                CreateSettingsGeminiColumn(
                    out txtAiProvider,
                    out txtAiModel,
                    out txtAiApiKey,
                    out btnToggleAiApiKey,
                    out btnTestAi,
                    btnTestAi_Click),
                2,
                0);

            var pnlGatewaySecond = CreateSettingsGatewaySecondRow(
                out txtTwoCaptchaApiKey,
                out btnToggleTwoCaptchaApiKey,
                out btnTestTwoCaptcha,
                btnTestTwoCaptcha_Click,
                out txtTikTokRapidApiKey,
                out btnToggleTikTokRapidApiKey,
                out btnTestTikTokRapidApi,
                btnTestTikTokRapidApi_Click,
                out var saveBtn,
                btnSaveSettings_Click);
            btnSaveSettings = saveBtn;

            tblGatewayRoot.Controls.Add(tblGatewayMain, 0, 0);
            tblGatewayRoot.Controls.Add(pnlGatewaySecond, 0, 1);
            grpGatewayTtsVeo.Controls.Add(tblGatewayRoot);

            var grpProfiles = CreateSettingsGroupBox("Tài khoản Chrome profile");
            grpProfiles.Name = "grpProfiles";
            grpProfiles.Padding = new Padding(8, 16, 8, 8);

            var pnlProfilesRoot = new Panel
            {
                Name = "pnlProfilesRoot",
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(36, 39, 48)
            };

            _proxyProfileBindingList = new BindingList<AutomationProfile>();
            dgvProxyProfiles = new DataGridView
            {
                Name = "dgvProxyProfiles",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 80),
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 4, 0, 0)
            };
            dgvProxyProfiles.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                SelectionBackColor = Color.FromArgb(76, 110, 245),
                SelectionForeColor = Color.White
            };
            dgvProxyProfiles.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
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
            dgvProxyProfiles.ColumnHeadersHeight = AppGridHeaderHeight;
            dgvProxyProfiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvProxyProfiles.EnableHeadersVisualStyles = false;

            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Tên profile (app)",
                FillWeight = 12,
                MinimumWidth = 90
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ChromeUserDataPath",
                HeaderText = "Thư mục Chrome (user-data)",
                FillWeight = 22,
                MinimumWidth = 120
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyHost",
                HeaderText = "Proxy host",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyPort",
                HeaderText = "Port",
                FillWeight = 6,
                MinimumWidth = 44
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyUser",
                HeaderText = "Proxy user",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProxyPass",
                HeaderText = "Proxy pass",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokUniqueId",
                HeaderText = "TikTok @nick",
                FillWeight = 12,
                MinimumWidth = 88
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokNickname",
                HeaderText = "Tên TikTok",
                FillWeight = 12,
                MinimumWidth = 88
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FacebookName",
                HeaderText = "Facebook",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "YouTubeName",
                HeaderText = "YouTube",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokUserId",
                HeaderText = "User id (web)",
                FillWeight = 10,
                MinimumWidth = 72
            });
            dgvProxyProfiles.Columns.Add(CreateProxyProfileStatusColumn("colProfileIsTTLoggedIn", "TikTok OK", 58));
            dgvProxyProfiles.Columns.Add(CreateProxyProfileStatusColumn("colProfileIsFBLoggedIn", "FB OK", 52));
            dgvProxyProfiles.Columns.Add(CreateProxyProfileStatusColumn("colProfileIsYTLoggedIn", "YT OK", 52));

            dgvProxyProfiles.DataSource = _proxyProfileBindingList;
            dgvProxyProfiles.SelectionChanged += dgvProxyProfiles_SelectionChanged;
            dgvProxyProfiles.CellFormatting += dgvProxyProfiles_CellFormatting;
            dgvProxyProfiles.DataError += dgvProxyProfiles_DataError;

            pnlProfilesRoot.Controls.Add(dgvProxyProfiles);
            grpProfiles.Controls.Add(pnlProfilesRoot);

            var profileGridTip = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
            var profileHintDetail =
                "Một dòng = một nick. Chỉ bắt buộc nhập «Tên». «Thư mục Chrome» = folder user-data nếu tách nhiều tài khoản; để trống vẫn chạy profile mặc định.\r\n" +
                "Đăng nhập TikTok bằng QR (hoặc mật khẩu) trong cửa sổ trình duyệt khi bấm Làm ấm / Đăng — không nhập mã QR vào bảng. Cột proxy chỉ khi dùng VPN/proxy.\r\n" +
                "Nút «Đăng nhập TikTok thủ công» mở đúng profile dòng đang chọn (--user-data-dir), vào /login; đóng trình duyệt khi xong.";
            profileGridTip.SetToolTip(grpPlatformLogin, profileHintDetail);
            profileGridTip.SetToolTip(grpProfiles, profileHintDetail);
            profileGridTip.SetToolTip(
                dgvProxyProfiles,
                "Cột «Tên profile (app)» dùng trong app (dropdown). «TikTok @nick» / «Tên TikTok» / «Facebook» / «YouTube» tự điền sau đăng nhập. «Thư mục Chrome»: user-data (vd. ...\\User Data\\Profile 1).");
            profileGridTip.SetToolTip(
                btnOpenTikTokManualBrowser,
                "Chọn một dòng rồi bấm: mở đăng nhập TikTok đúng profile; sau khi đăng nhập thành công bot đóng trình duyệt và cập nhật @nick.");
            profileGridTip.SetToolTip(
                btnCheckBrowserProfileHealth,
                "Quét browser_profile: có file Cookies (session) hay không — không đọc nội dung cookie.");

            var grpMedia = CreateSettingsGroupBox("FFmpeg / yt-dlp");
            grpMedia.Name = "grpMedia";
            grpMedia.Padding = new Padding(6, 10, 6, 6);
            grpMedia.AutoSize = true;
            grpMedia.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var tblMediaTools = new TableLayoutPanel
            {
                Name = "tblMediaTools",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblMediaTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblMediaTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblMediaTools.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblMediaTools.Controls.Add(
                CreateSettingsToolPathInlineCell(
                    "FFmpeg",
                    "txtFfmpegPath",
                    out txtFfmpegPath,
                    out btnBrowseFfmpegPath,
                    out btnDownloadFfmpeg,
                    btnBrowseFfmpegPath_Click,
                    btnDownloadFfmpeg_Click,
                    "btnBrowseFfmpegPath",
                    "btnDownloadFfmpeg"),
                0,
                0);
            tblMediaTools.Controls.Add(
                CreateSettingsToolPathInlineCell(
                    "yt-dlp",
                    "txtYtDlpPath",
                    out txtYtDlpPath,
                    out btnBrowseYtDlpPath,
                    out btnDownloadYtDlp,
                    btnBrowseYtDlpPath_Click,
                    btnDownloadYtDlp_Click,
                    "btnBrowseYtDlpPath",
                    "btnDownloadYtDlp"),
                1,
                0);

            grpMedia.Controls.Add(tblMediaTools);

            WireSettingsTtsVeoFieldTooltips();

            lblSettingsValidation = new Label
            {
                Name = "lblSettingsValidation",
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 120, 120),
                Dock = DockStyle.Top,
                Margin = new Padding(8, 4, 8, 4)
            };

            tabSetting.Controls.Add(grpPlatformLogin);
            tabSetting.Controls.Add(grpProfiles);
            tabSetting.Controls.Add(grpGatewayTtsVeo);
            tabSetting.Controls.Add(grpMedia);
            tabSetting.Controls.Add(lblSettingsValidation);

            HookSettingValidationEvents();

            tabSetting.ResumeLayout(true);
            tabSetting.PerformLayout();
        }

        private void WireSettingsTtsVeoFieldTooltips()
        {
            var tip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };

            tip.SetToolTip(
                txtTtsApiKey,
                "Khóa bí mật (Key) cho Text-to-Speech — cùng cột với URL gateway TTS bên trên.");
            tip.SetToolTip(
                txtVeoApiKey,
                "Khóa bí mật (Key) cho video AI Veo — cùng cột với URL gateway Veo bên trên.");
            tip.SetToolTip(
                txtTtsEndpoint,
                "URL API gateway TTS (POST JSON → audioUrl).\r\n" +
                "Cặp với «Khóa API TTS» ngay bên dưới trong cùng cột.");
            tip.SetToolTip(
                txtVeoEndpoint,
                "URL API gateway Veo.\r\n" +
                "Cặp với «Khóa API Veo» ngay bên dưới trong cùng cột.");
        }

    }
}
