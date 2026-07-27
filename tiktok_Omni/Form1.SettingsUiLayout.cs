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
                Font = AppCaptionFont,
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

        /// <summary>Độ rộng cột nhãn Voice/TTS — đủ «TTS · URL» / «Khóa TTS» / «Intense».</summary>
        private const int SettingsVoiceLabelWidth = 180;

        private Label CreateVoiceFieldCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = SettingsVoiceLabelWidth,
                Height = AppDefaultInputHeight,
                Font = AppLabelFont,
                ForeColor = Color.Gainsboro,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 6, 0),
                Dock = DockStyle.Fill,
                MinimumSize = new Size(SettingsVoiceLabelWidth, AppDefaultInputHeight)
            };
        }

        private TextBox CreateVoiceStretchField(string name, bool isSecret = false)
        {
            // Không dùng CreateSettingField (Min 160) — Min thấp để ô co, chừa chỗ label.
            var field = new TextBox
            {
                Name = name,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                UseSystemPasswordChar = isSecret,
                Height = AppDefaultInputHeight,
                MinimumSize = new Size(40, AppDefaultInputHeight),
                MaximumSize = Size.Empty,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = Padding.Empty
            };
            ApplyAppInputChrome(field);
            field.MinimumSize = new Size(40, AppDefaultInputHeight);
            field.Height = AppDefaultInputHeight;
            return field;
        }

        private TableLayoutPanel CreateVoiceAlignedRow(bool withButtons)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = withButtons ? 4 : 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            // Label cố định trước → ô nhập co theo phần còn lại (mép phải thẳng hàng).
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SettingsVoiceLabelWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            if (withButtons)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return row;
        }

        private Control CreateVoiceLabeledStretchField(string caption, TextBox field)
        {
            var row = CreateVoiceAlignedRow(withButtons: false);
            row.Controls.Add(CreateVoiceFieldCaption(caption), 0, 0);
            row.Controls.Add(field, 1, 0);
            return row;
        }

        private Control CreateVoiceSecretStretchRow(
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            field = CreateVoiceStretchField(fieldName, true);

            toggle = CreateSettingsShowToggleButton("btnToggleTtsApiKey", minWidth: 52, height: AppDefaultInputHeight);
            test = CreateSettingsShowTestButton("btnTestTts", "Test", minWidth: 72, height: AppDefaultInputHeight);
            var secretField = field;
            var secretToggle = toggle;
            toggle.Click += (sender, e) => ToggleSecretVisibility(secretField, secretToggle);
            test.Click += testClick;
            toggle.Margin = new Padding(4, 0, 0, 0);
            test.Margin = new Padding(4, 0, 0, 0);
            toggle.Anchor = AnchorStyles.Left;
            test.Anchor = AnchorStyles.Left;

            var row = CreateVoiceAlignedRow(withButtons: true);
            row.Name = "rowVoice" + fieldName;
            row.Controls.Add(CreateVoiceFieldCaption(caption), 0, 0);
            row.Controls.Add(field, 1, 0);
            row.Controls.Add(toggle, 2, 0);
            row.Controls.Add(test, 3, 0);
            return row;
        }

        /// <summary>
        /// Nhóm Voice/TTS: 2 cột canh đều full chiều rộng — TTS | Voice ID Triết lý.
        /// </summary>
        private GroupBox BuildVoiceSettingsGroupBox()
        {
            var grp = CreateSettingsGroupBox("Voice / TTS — dùng chung các tab video");
            grp.Name = "grpVoiceSettings";
            grp.Padding = new Padding(8, 12, 8, 10);
            grp.Margin = new Padding(0, 0, 0, 8);

            var tbl = new TableLayoutPanel
            {
                Name = "tblVoiceTwoCols",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // —— Cột 1: TTS ——
            var colTts = new TableLayoutPanel
            {
                Name = "colVoiceTts",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 30, 0),
                Padding = Padding.Empty
            };
            colTts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 3; i++)
            {
                colTts.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            colTts.Controls.Add(new Label
            {
                Text = "TTS (ElevenLabs)",
                AutoSize = true,
                Font = AppCaptionFont,
                ForeColor = Color.FromArgb(200, 210, 225),
                Margin = new Padding(0, 0, 0, 4),
                Dock = DockStyle.Top
            }, 0, 0);

            txtTtsEndpoint = CreateVoiceStretchField("txtTtsEndpoint");
            colTts.Controls.Add(CreateVoiceLabeledStretchField("TTS · URL", txtTtsEndpoint), 0, 1);
            colTts.Controls.Add(CreateVoiceSecretStretchRow(
                "Khóa TTS",
                "txtTtsApiKey",
                out txtTtsApiKey,
                out btnToggleTtsApiKey,
                out btnTestTts,
                btnTestTts_Click), 0, 2);

            // —— Cột 2: Voice ID Triết lý — 3 cột nhỏ, label phía trên mỗi ô ——
            var colVoices = new TableLayoutPanel
            {
                Name = "colVoicePhilosophy",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            colVoices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            colVoices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            colVoices.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            colVoices.Controls.Add(new Label
            {
                Text = "Voice ID — Triết lý (theo mood)",
                AutoSize = true,
                Font = AppCaptionFont,
                ForeColor = Color.FromArgb(200, 210, 225),
                Margin = new Padding(0, 0, 0, 6),
                Dock = DockStyle.Top
            }, 0, 0);

            txtVoiceIdMelancholic = CreateVoiceStretchField("txtVoiceIdMelancholic");
            txtVoiceIdIntense = CreateVoiceStretchField("txtVoiceIdIntense");
            txtVoiceIdCalm = CreateVoiceStretchField("txtVoiceIdCalm");

            var tblMoodIds = new TableLayoutPanel
            {
                Name = "tblPhilosophyVoiceIds",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (var i = 0; i < 3; i++)
            {
                tblMoodIds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            }

            tblMoodIds.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblMoodIds.Controls.Add(CreateVoiceStackedField("Melanc", txtVoiceIdMelancholic, isLast: false), 0, 0);
            tblMoodIds.Controls.Add(CreateVoiceStackedField("Intense", txtVoiceIdIntense, isLast: false), 1, 0);
            tblMoodIds.Controls.Add(CreateVoiceStackedField("Calm", txtVoiceIdCalm, isLast: true), 2, 0);
            colVoices.Controls.Add(tblMoodIds, 0, 1);

            tbl.Controls.Add(colTts, 0, 0);
            tbl.Controls.Add(colVoices, 1, 0);
            grp.Controls.Add(tbl);
            return grp;
        }

        /// <summary>Label phía trên + ô nhập bên dưới — dùng cho 3 cột Voice ID mood.</summary>
        private Control CreateVoiceStackedField(string caption, TextBox field, bool isLast = false)
        {
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, isLast ? 0 : 10, 0),
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                Font = AppLabelFont,
                ForeColor = Color.Gainsboro,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 4)
            };

            field.Dock = DockStyle.Fill;
            field.Margin = Padding.Empty;

            stack.Controls.Add(lbl, 0, 0);
            stack.Controls.Add(field, 0, 1);
            return stack;
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

        private JellyButton CreateSettingsGreenFlatButton(string name, string text, int minWidth = 70, int height = 0)
        {
            var resolvedHeight = height > 0 ? height : AppJellyButtonHeight;
            return CreateAppJellyButton(
                name,
                text,
                SettingsTintGreen,
                heightOverride: resolvedHeight,
                minWidth: minWidth,
                margin: new Padding(0, 0, 6, 0));
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

        private Button CreateSettingsShowTestButton(string name, string englishLabel, int minWidth = 72, int height = 0)
        {
            const int horizontalPad = 18;
            var resolvedHeight = height > 0 ? height : AppJellyButtonHeight;
            var displayText = LocalizeDisplayText(englishLabel);
            var width = Math.Max(
                minWidth,
                MeasureProfileButtonTextWidth(displayText, ProfileToolbarButtonFont, resolvedHeight) + horizontalPad);

            return CreateSettingsFlatBlueButton(
                name,
                englishLabel,
                resolvedHeight,
                horizontalPad,
                width,
                lockWidth: true);
        }

        private Button CreateSettingsShowToggleButton(string name, int minWidth = 72, int height = 0)
        {
            const int horizontalPad = 18;
            var resolvedHeight = height > 0 ? height : AppJellyButtonHeight;
            var showLabel = LocalizeDisplayText("Show");
            var hideLabel = LocalizeDisplayText("Hide");
            var width = Math.Max(
                minWidth,
                Math.Max(
                    MeasureProfileButtonTextWidth(showLabel, ProfileToolbarButtonFont, resolvedHeight),
                    MeasureProfileButtonTextWidth(hideLabel, ProfileToolbarButtonFont, resolvedHeight))
                + horizontalPad);

            return CreateSettingsShowTestButton(name, "Show", width, resolvedHeight);
        }

        private static readonly Font ProfileToolbarButtonFont = AppJellyButtonFont;
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
            return MeasureAppJellyButtonTextWidth(text, font, buttonHeight);
        }

        private JellyButton CreateProfileJellyButton(string name, string text, Color tint)
        {
            // Profile toolbar dùng TableLayout Fill — không khóa width cứng.
            var btn = CreateAppJellyButton(
                name,
                text,
                tint,
                heightOverride: AppPrimaryActionHeight,
                minWidth: AppJellyButtonMinWidth,
                margin: Padding.Empty,
                lockSize: false);
            btn.Dock = DockStyle.Fill;
            // Bỏ MinWidth theo chữ — nếu giữ, tổng 6 nút dễ tràn khung và cắt nút cuối.
            btn.MinimumSize = new Size(0, btn.Height);
            btn.MaximumSize = Size.Empty;
            return btn;
        }

        private JellyButton CreateSettingsSaveButton(string name, string text)
        {
            // Nút Lưu nổi bật hơn primary thường.
            var btn = CreateAppPrimaryJellyButton(
                name,
                text,
                Color.FromArgb(28, 156, 72),
                minWidth: 300,
                margin: new Padding(8, 0, 8, 2));
            btn.JellyFillOpacity = 1f;
            btn.ForeColor = Color.White;
            btn.Anchor = AnchorStyles.None;
            btn.Cursor = Cursors.Hand;
            btn.Height = Math.Max(AppPrimaryActionHeight, 72);
            btn.MinimumSize = new Size(300, btn.Height);
            btn.MaximumSize = new Size(300, btn.Height);
            btn.Width = 300;
            return btn;
        }

        private TableLayoutPanel CreateProfileToolbarTable(params Control[] buttons)
        {
            const int buttonHeight = 66;
            const int toolbarTopMargin = 2;
            const int toolbarBottomMargin = 4;
            // Pad chữ nhỏ hơn → nút hẹp hơn, ít bị cắt mép phải khi cửa sổ hẹp.
            const int horizontalPad = 20;
            const int gap = 4;

            var weights = new float[buttons.Length];
            var totalWeight = 0f;
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.AutoSize = false;
                button.Height = buttonHeight;
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 0, i < buttons.Length - 1 ? gap : 0, 0);
                // Bỏ MinimumSize cứng theo chữ — để cột Percent co theo chiều rộng khung.
                button.MinimumSize = new Size(0, buttonHeight);
                button.MaximumSize = Size.Empty;

                var minWidth = Math.Max(64, MeasureProfileButtonTextWidth(button.Text, button.Font, buttonHeight) + horizontalPad);
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

            // Không AutoSize theo tổng min-width nút (dễ tràn cắt nút cuối).
            // Dock Top + Percent: rộng theo GroupBox, cao cố định theo nút.
            var tbl = new TableLayoutPanel
            {
                Name = "tblProfileToolbar",
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = buttonHeight + toolbarTopMargin + toolbarBottomMargin,
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
                "txtTtsApiKey" => "btnToggleTtsApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
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
            field.MinimumSize = new Size(80, AppDefaultInputHeight);

            var toggleName = fieldName switch
            {
                "txtAiApiKey" => "btnToggleAiApiKey",
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtTtsApiKey" => "btnToggleTtsApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
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
            field.Height = AppDefaultInputHeight;
            field.MinimumSize = new Size(80, AppDefaultInputHeight);
            field.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            var toggleName = fieldName switch
            {
                "txtAiApiKey" => "btnToggleAiApiKey",
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtTtsApiKey" => "btnToggleTtsApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
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

        /// <summary>
        /// Độ rộng cột nhãn trong mỗi cột Gateway — đủ «TikTok RapidAPI» / «Provider»,
        /// các ô nhập bắt đầu cùng mép trái; mép phải vẫn thẳng hàng nhờ Percent fill.
        /// </summary>
        private const int SettingsGatewayLabelWidth = 138;

        private Label CreateGatewayColumnTitle(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = AppCaptionFont,
                ForeColor = Color.FromArgb(200, 210, 225),
                Margin = new Padding(0, 0, 0, 4),
                Dock = DockStyle.Top
            };
        }

        private TextBox CreateGatewayStretchField(string name, bool isSecret)
        {
            // Min width thấp để Absolute label không bị ép cắt chữ.
            var field = new TextBox
            {
                Name = name,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                UseSystemPasswordChar = isSecret,
                Height = AppDefaultInputHeight,
                MinimumSize = new Size(40, AppDefaultInputHeight),
                MaximumSize = Size.Empty,
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = Padding.Empty
            };
            ApplyAppInputChrome(field);
            field.MinimumSize = new Size(40, AppDefaultInputHeight);
            field.Height = AppDefaultInputHeight;
            return field;
        }

        private Label CreateGatewayInlineCaption(string caption)
        {
            var captionLabel = CreateSettingCaption(caption);
            captionLabel.AutoSize = false;
            captionLabel.Dock = DockStyle.Fill;
            captionLabel.TextAlign = ContentAlignment.MiddleLeft;
            captionLabel.Margin = new Padding(0, 0, 4, 0);
            captionLabel.MinimumSize = new Size(SettingsGatewayLabelWidth, AppDefaultInputHeight);
            return captionLabel;
        }

        private TableLayoutPanel CreateGatewayAlignedRow(bool withButtons)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = withButtons ? 4 : 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            // Label cố định → ô nhập chiếm hết phần còn lại của cột → nút AutoSize.
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SettingsGatewayLabelWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            if (withButtons)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return row;
        }

        private Control CreateGatewayLabeledInlineField(string caption, TextBox field)
        {
            var row = CreateGatewayAlignedRow(withButtons: false);
            row.Controls.Add(CreateGatewayInlineCaption(caption), 0, 0);
            row.Controls.Add(field, 1, 0);
            return row;
        }

        private Control CreateGatewaySecretInlineRow(
            string caption,
            string fieldName,
            out TextBox field,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            field = CreateGatewayStretchField(fieldName, true);

            var toggleName = fieldName switch
            {
                "txtAiApiKey" => "btnToggleAiApiKey",
                "txtTwoCaptchaApiKey" => "btnToggleTwoCaptchaApiKey",
                "txtVeoApiKey" => "btnToggleVeoApiKey",
                "txtTikTokRapidApiKey" => "btnToggleTikTokRapidApiKey",
                _ => "btnToggle" + fieldName.Substring(3)
            };

            var testName = fieldName switch
            {
                "txtAiApiKey" => "btnTestAi",
                "txtTwoCaptchaApiKey" => "btnTestTwoCaptcha",
                "txtVeoApiKey" => "btnTestVeo",
                "txtTikTokRapidApiKey" => "btnTestTikTokRapidApi",
                _ => "btnTest" + fieldName.Substring(3)
            };

            toggle = CreateSettingsShowToggleButton(toggleName, minWidth: 52, height: AppDefaultInputHeight);
            test = CreateSettingsShowTestButton(testName, "Test", minWidth: 72, height: AppDefaultInputHeight);
            var secretField = field;
            var secretToggle = toggle;
            toggle.Click += (sender, e) => ToggleSecretVisibility(secretField, secretToggle);
            test.Click += testClick;
            toggle.Margin = new Padding(4, 0, 0, 0);
            test.Margin = new Padding(4, 0, 0, 0);
            toggle.Anchor = AnchorStyles.Left;
            test.Anchor = AnchorStyles.Left;

            var row = CreateGatewayAlignedRow(withButtons: true);
            row.Name = "rowGateway" + fieldName;
            row.Controls.Add(CreateGatewayInlineCaption(caption), 0, 0);
            row.Controls.Add(field, 1, 0);
            row.Controls.Add(toggle, 2, 0);
            row.Controls.Add(test, 3, 0);
            return row;
        }

        private TableLayoutPanel CreateGatewayColumnHost(string name)
        {
            var col = new TableLayoutPanel
            {
                Name = name,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                Margin = new Padding(0, 0, 30, 0),
                Padding = new Padding(2, 2, 2, 2)
            };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return col;
        }

        private Control CreateSettingsGatewayVeoColumn(
            out TextBox urlField,
            out TextBox keyField,
            out Button toggle,
            out Button test,
            EventHandler testClick,
            out TextBox tikTokField,
            out Button tikTokToggle,
            out Button tikTokTest,
            EventHandler tikTokTestClick)
        {
            var col = CreateGatewayColumnHost("colGatewayVeo");
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 4; i++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            urlField = CreateGatewayStretchField("txtVeoEndpoint", false);
            stack.Controls.Add(CreateGatewayColumnTitle("RapidAPI · Veo · TikTok"), 0, 0);
            stack.Controls.Add(CreateGatewayLabeledInlineField("URL Veo", urlField), 0, 1);
            stack.Controls.Add(CreateGatewaySecretInlineRow(
                "Khóa Veo",
                "txtVeoApiKey",
                out keyField,
                out toggle,
                out test,
                testClick), 0, 2);
            stack.Controls.Add(CreateGatewaySecretInlineRow(
                "TikTok RapidAPI",
                "txtTikTokRapidApiKey",
                out tikTokField,
                out tikTokToggle,
                out tikTokTest,
                tikTokTestClick), 0, 3);

            col.Controls.Add(stack, 0, 0);
            return col;
        }

        private Control CreateSettingsGatewayGeminiColumn(
            out TextBox providerField,
            out TextBox modelField,
            out TextBox apiKeyField,
            out Button toggle,
            out Button test,
            EventHandler testClick)
        {
            var col = CreateGatewayColumnHost("colGatewayGemini");
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < 4; i++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            providerField = CreateGatewayStretchField("txtAiProvider", false);
            modelField = CreateGatewayStretchField("txtAiModel", false);
            stack.Controls.Add(CreateGatewayColumnTitle("Gemini"), 0, 0);
            stack.Controls.Add(CreateGatewayLabeledInlineField("Provider", providerField), 0, 1);
            stack.Controls.Add(CreateGatewayLabeledInlineField("Model", modelField), 0, 2);
            stack.Controls.Add(CreateGatewaySecretInlineRow(
                "Khóa API",
                "txtAiApiKey",
                out apiKeyField,
                out toggle,
                out test,
                testClick), 0, 3);

            col.Controls.Add(stack, 0, 0);
            return col;
        }

        private Control CreateSettingsGatewayExtrasColumn(
            out TextBox captchaField,
            out Button captchaToggle,
            out Button captchaTest,
            EventHandler captchaTestClick)
        {
            var col = CreateGatewayColumnHost("colGatewayExtras");
            col.Margin = new Padding(0, 0, 0, 0);
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            stack.Controls.Add(CreateGatewayColumnTitle("2Captcha"), 0, 0);
            stack.Controls.Add(CreateGatewaySecretInlineRow(
                "Khóa API",
                "txtTwoCaptchaApiKey",
                out captchaField,
                out captchaToggle,
                out captchaTest,
                captchaTestClick), 0, 1);

            col.Controls.Add(stack, 0, 0);
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
            textBox.MinimumSize = new Size(80, AppDefaultInputHeight);
            textBox.Margin = new Padding(0, 0, 4, 0);

            browse = CreateSettingBrowseButton(
                browseName ?? ("btnBrowse" + fieldName.Substring(3)),
                browseText);
            browse.MinimumSize = new Size(64, AppDefaultInputHeight);
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
                    btn.MinimumSize = new Size(72, AppDefaultInputHeight);
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
            textBox.Height = AppDefaultInputHeight;
            textBox.MinimumSize = new Size(60, AppDefaultInputHeight);
            textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            browse = CreateSettingsGreenFlatButton(browseName, "Duyệt", minWidth: 52, height: AppDefaultInputHeight);
            browse.Click += browseClick;
            browse.Margin = new Padding(4, 0, 0, 0);

            download = CreateSettingsGreenFlatButton(downloadName, "\u2B07 T\u1EA3i", minWidth: 52, height: AppDefaultInputHeight);
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
            textBox.MinimumSize = new Size(200, AppDefaultInputHeight);

            browse = CreateSettingBrowseButton(
                browseName ?? ("btnBrowse" + fieldName.Substring(3)),
                browseText);
            browse.Click += browseClick;

            var controls = new System.Collections.Generic.List<Control> { textBox, browse };
            if (extraButtons != null)
            {
                foreach (var extra in extraButtons)
                {
                    var btn = CreateSettingsGreenFlatButton(extra.name, extra.text, 72, AppDefaultInputHeight);
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
                controlLeft.MinimumSize = new Size(60, AppDefaultInputHeight);
            }

            if (controlRight != null)
            {
                controlRight.Dock = DockStyle.Fill;
                controlRight.Margin = new Padding(0, 0, 0, 4);
                controlRight.MinimumSize = new Size(60, AppDefaultInputHeight);
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

        private void BuildSettingUi()
        {
            tabSetting.SuspendLayout();
            tabSetting.AutoScroll = true;
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
            grpPlatformLogin.Controls.Add(tblPlatformLogin);

            var grpGatewayTtsVeo = CreateSettingsGroupBox("Gateway — Veo · Gemini · Captcha");
            grpGatewayTtsVeo.Name = "grpGatewayTtsVeo";
            grpGatewayTtsVeo.Padding = new Padding(6, 10, 6, 10);

            // 3 cột chia đều full chiều rộng giao diện; ô nhập co giãn theo cột.
            var tblGatewayMain = new TableLayoutPanel
            {
                Name = "tblGatewayThreeCols",
                Dock = DockStyle.Top,
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
                CreateSettingsGatewayVeoColumn(
                    out txtVeoEndpoint,
                    out txtVeoApiKey,
                    out btnToggleVeoApiKey,
                    out btnTestVeo,
                    btnTestVeo_Click,
                    out txtTikTokRapidApiKey,
                    out btnToggleTikTokRapidApiKey,
                    out btnTestTikTokRapidApi,
                    btnTestTikTokRapidApi_Click),
                0,
                0);
            tblGatewayMain.Controls.Add(
                CreateSettingsGatewayGeminiColumn(
                    out txtAiProvider,
                    out txtAiModel,
                    out txtAiApiKey,
                    out btnToggleAiApiKey,
                    out btnTestAi,
                    btnTestAi_Click),
                1,
                0);
            tblGatewayMain.Controls.Add(
                CreateSettingsGatewayExtrasColumn(
                    out txtTwoCaptchaApiKey,
                    out btnToggleTwoCaptchaApiKey,
                    out btnTestTwoCaptcha,
                    btnTestTwoCaptcha_Click),
                2,
                0);

            grpGatewayTtsVeo.Controls.Add(tblGatewayMain);

            // Nút Lưu nằm ngoài / phía trên khung Gateway.
            btnSaveSettings = CreateSettingsSaveButton("btnSaveSettings", "L\u01b0u c\u00e0i \u0111\u1eb7t");
            btnSaveSettings.Click += btnSaveSettings_Click;
            btnSaveSettings.Anchor = AnchorStyles.None;
            btnSaveSettings.Margin = new Padding(0, 4, 0, 8);

            var tblSaveRow = new TableLayoutPanel
            {
                Name = "tblSettingsSaveRow",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6),
                Padding = Padding.Empty
            };
            tblSaveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblSaveRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblSaveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblSaveRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblSaveRow.Controls.Add(btnSaveSettings, 1, 0);

            var grpVoiceSettings = BuildVoiceSettingsGroupBox();

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
                AllowUserToDeleteRows = false,
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
                SelectionForeColor = Color.White,
                Font = AppGridBodyFont,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 4, 8, 4),
                WrapMode = DataGridViewTriState.False
            };
            dgvProxyProfiles.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 44, 54),
                ForeColor = Color.WhiteSmoke,
                SelectionBackColor = Color.FromArgb(40, 44, 54),
                SelectionForeColor = Color.WhiteSmoke,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };

            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Tên profile",
                FillWeight = 12,
                MinimumWidth = 90
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ChromeUserDataPath",
                HeaderText = "Thư mục Chrome",
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
                Name = "colProfileTikTokNick",
                DataPropertyName = "TikTokUniqueId",
                HeaderText = "TikTok @nick",
                FillWeight = 12,
                MinimumWidth = 88,
                ReadOnly = true
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProfileTikTokName",
                DataPropertyName = "TikTokNickname",
                HeaderText = "TikTok",
                FillWeight = 12,
                MinimumWidth = 88,
                ReadOnly = true
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProfileFacebookName",
                DataPropertyName = "FacebookName",
                HeaderText = "Facebook",
                FillWeight = 10,
                MinimumWidth = 72,
                ReadOnly = true
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProfileYouTubeName",
                DataPropertyName = "YouTubeName",
                HeaderText = "YouTube",
                FillWeight = 10,
                MinimumWidth = 72,
                ReadOnly = true
            });
            dgvProxyProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TikTokUserId",
                HeaderText = "User ID",
                FillWeight = 10,
                MinimumWidth = 72
            });

            dgvProxyProfiles.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colProfileMascotImage",
                HeaderText = "Ảnh profile",
                Text = "📷 Chọn",
                UseColumnTextForButtonValue = false,
                Width = 96,
                MinimumWidth = 80,
                FlatStyle = FlatStyle.Flat,
                ToolTipText = "Ảnh nhân vật cho hook intro Video reup — hover xem thư mục, bấm để chọn/đổi"
            });

            dgvProxyProfiles.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colProfileHookClips",
                HeaderText = "Hook Clips",
                Text = "📁 Video Hook",
                UseColumnTextForButtonValue = false,
                Width = 110,
                MinimumWidth = 90,
                FlatStyle = FlatStyle.Flat,
                ToolTipText = "Tạo thư mục 5 style cho profile này và mở trong Explorer để thêm clip (.mp4)"
            });

            dgvProxyProfiles.ShowCellToolTips = true;
            dgvProxyProfiles.DataSource = _proxyProfileBindingList;

            // Có cột nút (Ảnh profile / Hook Clips) → dùng chiều cao combo chuẩn, áp sau khi đã có cột.
            ApplyAppComboGridRowHeight(dgvProxyProfiles);
            ApplyAppGridChrome(dgvProxyProfiles);
            EnsureAppGridRowHeights(dgvProxyProfiles);
            dgvProxyProfiles.DataBindingComplete += (_, __) => EnsureAppGridRowHeights(dgvProxyProfiles);

            dgvProxyProfiles.SelectionChanged += dgvProxyProfiles_SelectionChanged;
            dgvProxyProfiles.CellFormatting += dgvProxyProfiles_CellFormatting;
            dgvProxyProfiles.CellToolTipTextNeeded += dgvProxyProfiles_CellToolTipTextNeeded;
            dgvProxyProfiles.CellContentClick += dgvProxyProfiles_CellContentClick;
            dgvProxyProfiles.DataError += dgvProxyProfiles_DataError;
            dgvProxyProfiles.KeyDown += dgvProxyProfiles_KeyDown;

            pnlProfilesRoot.Controls.Add(dgvProxyProfiles);
            ApplyAppGridChrome(dgvProxyProfiles);
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
                "Cột «Tên profile» dùng trong app (dropdown). «TikTok @nick» / «TikTok» / «Facebook» / «YouTube» chỉ hiện khi đã đăng nhập nền tảng đó. «Thư mục Chrome»: user-data (vd. ...\\User Data\\Profile 1).");
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

            // Dock Top: control thêm sau nằm gần mép trên hơn.
            // Thứ tự: Login → Profiles → Lưu → Gateway → Voice → Media
            tabSetting.Controls.Add(grpPlatformLogin);
            tabSetting.Controls.Add(grpProfiles);
            tabSetting.Controls.Add(tblSaveRow);
            tabSetting.Controls.Add(grpGatewayTtsVeo);
            tabSetting.Controls.Add(grpVoiceSettings);
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

            if (txtTtsApiKey != null)
            {
                tip.SetToolTip(
                    txtTtsApiKey,
                    "Khóa bí mật (Key) cho Text-to-Speech — nhóm Voice / TTS.");
            }

            tip.SetToolTip(
                txtVeoApiKey,
                "RapidAPI key (x-rapidapi-key) cho Google Veo 3.1 Text-to-Video — có thể trùng key TikTok RapidAPI nếu cùng tài khoản.");

            if (txtTtsEndpoint != null)
            {
                tip.SetToolTip(
                    txtTtsEndpoint,
                    "URL API gateway TTS (POST JSON → audioUrl).\r\n" +
                    "Cặp với «Khóa TTS» ngay bên dưới trong nhóm Voice / TTS.");
            }

            tip.SetToolTip(
                txtVeoEndpoint,
                "Base URL RapidAPI Google Veo 3.1 (mặc định " + RapidApiGoogleVeoHelper.DefaultBaseUrl + ").\r\n" +
                "Cặp với «Khóa RapidAPI Veo» ngay bên dưới trong cùng cột.");

            if (txtVoiceIdMelancholic != null)
            {
                tip.SetToolTip(
                    txtVoiceIdMelancholic,
                    "ElevenLabs voice_id cho mood melancholic / sad — tab Video Triết lý chọn tự động theo cột Mood.");
            }

            if (txtVoiceIdIntense != null)
            {
                tip.SetToolTip(
                    txtVoiceIdIntense,
                    "ElevenLabs voice_id cho mood intense / hopeful — tab Video Triết lý.");
            }

            if (txtVoiceIdCalm != null)
            {
                tip.SetToolTip(
                    txtVoiceIdCalm,
                    "ElevenLabs voice_id calm / reflective — tab Video Triết lý.");
            }
        }

    }
}
