using System;

using System.Collections.Generic;

using System.Drawing;

using System.IO;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using System.Windows.Forms;

using tiktok_Omni.Services;

using tiktok_Omni.Services.Showcase;



namespace tiktok_Omni

{

    public partial class Form1

    {

        private static readonly Color AiVideoGenProcessedRowBack = Color.FromArgb(210, 240, 220);

        private static readonly Color AiVideoGenProcessedRowFore = Color.FromArgb(24, 48, 32);

        private List<ProfileComboEntry> _aiVideoGenProfileComboSource = new List<ProfileComboEntry>();

        private const string AiVideoGenDragDropFormat = "tiktok_Omni.AiVideoGenInputItem";

        private static readonly string[] ShowcaseHiddenGridColumns =
        {
            "colAiProfile", "colAiUrl", "colAiHook", "colAiHashtag", "colAiThumb", "colAiKeyword",
            "colAiPrice", "colAiSafety"
        };

        /// <summary>Cột hiển thị trên lưới Showcase — mỗi dòng = 1 video; chi tiết cảnh nằm trên storyboard.</summary>
        private static readonly string[] ShowcaseVisibleGridColumns =
        {
            "colAiProduct", "colAiShowcaseImages", "colAiShowcaseProductType", "colAiShowcaseClipMode", "colAiShowcaseOutputAspect",
            "colAiShowcaseScript", "colAiShowcaseSceneSummary",
            "colAiShowcaseTextSize", "colAiShowcaseMusicVolume", "colAiShowcaseBrandLogo", "colAiStatus", "colAiShowcaseOutput"
        };

        private static readonly string[] ShowcaseLegacyOnlyGridColumns =
        {
            "colAiShowcaseProductType", "colAiShowcaseClipMode", "colAiShowcaseOutputAspect", "colAiShowcaseTheme", "colAiShowcaseUserTheme", "colAiShowcaseSceneSummary", "colAiShowcaseScript", "colAiShowcaseScenePrompt",
            "colAiSceneTitle", "colAiSceneRole", "colAiSceneVoice", "colAiVeoPrompt", "colAiClip",
            "colAiShowcaseMultiVoice", "colAiShowcaseTextSize", "colAiShowcaseMusicVolume", "colAiShowcaseTransition"
        };

        private const int ShowcaseGridVoicePreviewChars = 42;
        private const int ShowcaseGridVeoPromptPreviewChars = 52;

        private const int ShowcaseImagesGridRowHeight = AppDefaultRowHeight;

        private void ApplyShowcaseDeepDiveRowHeights()
        {
            if (dgvDeepDiveInput == null || dgvDeepDiveInput.IsDisposed)
            {
                return;
            }

            dgvDeepDiveInput.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvDeepDiveInput.RowTemplate.Height = ShowcaseImagesGridRowHeight;
            dgvDeepDiveInput.RowTemplate.MinimumHeight = ShowcaseImagesGridRowHeight;

            foreach (DataGridViewRow row in dgvDeepDiveInput.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.Height = ShowcaseImagesGridRowHeight;
                row.MinimumHeight = ShowcaseImagesGridRowHeight;
            }
        }

        /// <summary>dgvDeepDiveInput chỉ dùng cho Showcase — ẩn cột Slideshow/Affiliate thừa;
        /// STT dùng cột chuẩn colAppGridStt (ApplyAppGridChrome), không vẽ row header riêng.</summary>
        private void ApplyDeepDiveGridColumnVisibility(bool showcaseMode)
        {
            if (dgvDeepDiveInput == null)
            {
                return;
            }

            dgvDeepDiveInput.RowHeadersVisible = false;

            foreach (var columnName in ShowcaseHiddenGridColumns)
            {
                var column = dgvDeepDiveInput.Columns[columnName];
                if (column != null)
                {
                    column.Visible = !showcaseMode;
                }
            }

            foreach (var columnName in ShowcaseLegacyOnlyGridColumns)
            {
                var column = dgvDeepDiveInput.Columns[columnName];
                if (column != null)
                {
                    column.Visible = !showcaseMode;
                }
            }

            if (showcaseMode)
            {
                foreach (DataGridViewColumn column in dgvDeepDiveInput.Columns)
                {
                    if (column == null)
                    {
                        continue;
                    }

                    var isStt = string.Equals(column.Name, AppGridSttColumn.ColumnName, StringComparison.Ordinal);
                    column.Visible = isStt || ShowcaseVisibleGridColumns.Contains(column.Name);
                }
            }

            var imageColumn = dgvDeepDiveInput.Columns["colAiImage"];
            if (imageColumn != null && !showcaseMode)
            {
                imageColumn.HeaderText = "Ảnh URL";
            }

            if (showcaseMode)
            {
                AppGridSttColumn.EnsureFirstColumn(dgvDeepDiveInput);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, AppGridSttColumn.ColumnName, 0);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiProduct", 1);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseImages", 2);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseProductType", 3);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseClipMode", 4);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseOutputAspect", 5);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseScript", 6);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseSceneSummary", 7);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseTextSize", 8);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseMusicVolume", 9);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseBrandLogo", 10);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiStatus", 11);
                SetGridColumnDisplayIndex(dgvDeepDiveInput, "colAiShowcaseOutput", 12);

                var multiVoiceCol = dgvDeepDiveInput.Columns["colAiShowcaseMultiVoice"];
                if (multiVoiceCol != null)
                {
                    multiVoiceCol.Visible = false;
                }

                var transitionCol = dgvDeepDiveInput.Columns["colAiShowcaseTransition"];
                if (transitionCol != null)
                {
                    transitionCol.Visible = false;
                }
                dgvDeepDiveInput.ShowCellToolTips = true;

                var productCol = dgvDeepDiveInput.Columns["colAiProduct"];
                if (productCol != null)
                {
                    productCol.HeaderText = "Tên SP";
                    productCol.ReadOnly = true;
                    productCol.FillWeight = 52;
                    productCol.MinimumWidth = 220;
                    productCol.ToolTipText = "Bấm để sửa tên sản phẩm.";
                    productCol.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    productCol.DefaultCellStyle.ForeColor = Color.FromArgb(130, 175, 255);
                    productCol.DefaultCellStyle.SelectionForeColor = Color.White;
                }

                var imagesCol = dgvDeepDiveInput.Columns["colAiShowcaseImages"];
                if (imagesCol != null)
                {
                    imagesCol.HeaderText = "Ảnh";
                    imagesCol.ReadOnly = true;
                    imagesCol.FillWeight = 22;
                    imagesCol.MinimumWidth = 108;
                    imagesCol.ToolTipText = "Trái: thêm ảnh · Phải: mở thư mục · Di chuột vào ô xem số ảnh.";
                }

                ApplyShowcaseDeepDiveRowHeights();

                var productTypeCol = dgvDeepDiveInput.Columns["colAiShowcaseProductType"];
                if (productTypeCol is DataGridViewComboBoxColumn productTypeCombo)
                {
                    ApplyShowcaseProductTypeComboColumn(productTypeCombo);
                    productTypeCombo.ReadOnly = true;
                    productTypeCombo.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
                    productTypeCombo.FlatStyle = FlatStyle.Flat;
                    productTypeCombo.HeaderText = "Loại SP · Chủ đề";
                    productTypeCombo.FillWeight = 24;
                    productTypeCombo.MinimumWidth = 180;
                    productTypeCombo.ToolTipText =
                        "Loại trang phục + góc quảng cáo cho Gemini — bấm ô để chọn loại SP hoặc chủ đề.";
                }

                var clipModeCol = dgvDeepDiveInput.Columns["colAiShowcaseClipMode"];
                if (clipModeCol is DataGridViewComboBoxColumn clipModeCombo)
                {
                    ApplyShowcaseClipModeComboColumn(clipModeCombo);
                    clipModeCombo.ReadOnly = false;
                    clipModeCombo.HeaderText = "Công cụ Video";
                    clipModeCombo.ToolTipText =
                        "Công cụ clip: Veo + Zoom, Veo + Kling, Zoom + Kling, Kling + Veo + Zoom, Chỉ Veo/Kling/Zoom, Gemini gợi ý. Chọn trước «Tạo kịch bản».";
                }

                var aspectCol = dgvDeepDiveInput.Columns["colAiShowcaseOutputAspect"];
                if (aspectCol is DataGridViewComboBoxColumn aspectCombo)
                {
                    ApplyShowcaseOutputAspectComboColumn(aspectCombo);
                    aspectCombo.ReadOnly = false;
                    aspectCombo.HeaderText = "Khung video";
                    aspectCombo.ToolTipText =
                        "Tỉ lệ file xuất (9:16 / 16:9 / 1:1). Chọn trước «Tạo kịch bản» và «Tạo clip Zoom» — đổi sau khi có clip nên tạo clip lại.";
                }

                var themeCol = dgvDeepDiveInput.Columns["colAiShowcaseTheme"];
                if (themeCol != null)
                {
                    themeCol.Visible = false;
                    themeCol.ReadOnly = true;
                }

                var userThemeCol = dgvDeepDiveInput.Columns["colAiShowcaseUserTheme"];
                if (userThemeCol != null)
                {
                    userThemeCol.Visible = false;
                    userThemeCol.ReadOnly = true;
                }

                var sceneSummaryCol = dgvDeepDiveInput.Columns["colAiShowcaseSceneSummary"];
                if (sceneSummaryCol != null)
                {
                    sceneSummaryCol.ReadOnly = true;
                    sceneSummaryCol.FillWeight = 22;
                    sceneSummaryCol.MinimumWidth = 108;
                    sceneSummaryCol.ToolTipText =
                        "Trái: thêm clip · Phải: mở veo_clips · Di chuột vào ô xem số cảnh.";
                }

                var scriptCol = dgvDeepDiveInput.Columns["colAiShowcaseScript"];
                if (scriptCol != null)
                {
                    scriptCol.HeaderText = "Kịch bản · Prompt";
                    scriptCol.ReadOnly = true;
                    scriptCol.FillWeight = 28;
                    scriptCol.MinimumWidth = 200;
                    scriptCol.ToolTipText =
                        "Thoại (nháp theo ảnh hoặc đã «Tạo lời thoại») + prompt clip — bấm ô mở hub «Kịch bản · Prompt».";
                }

                var promptCol = dgvDeepDiveInput.Columns["colAiShowcaseScenePrompt"];
                if (promptCol != null)
                {
                    promptCol.Visible = false;
                }

                var statusCol = dgvDeepDiveInput.Columns["colAiStatus"];
                if (statusCol != null)
                {
                    statusCol.HeaderText = "Trạng thái";
                    statusCol.ReadOnly = true;
                    statusCol.FillWeight = 14;
                    statusCol.MinimumWidth = 96;
                    statusCol.ToolTipText = "Trạng thái pipeline: Chờ, Đang render, Xong, Lỗi.";
                }

                var outputCol = dgvDeepDiveInput.Columns["colAiShowcaseOutput"];
                if (outputCol != null)
                {
                    outputCol.HeaderText = "Output";
                    outputCol.ReadOnly = true;
                    outputCol.FillWeight = 16;
                    outputCol.MinimumWidth = 88;
                    outputCol.ToolTipText = "▶ xem video thành phẩm · 📂 mở thư mục output.";
                }

                var subtitleCol = dgvDeepDiveInput.Columns["colAiShowcaseTextSize"];
                if (subtitleCol != null)
                {
                    subtitleCol.HeaderText = "Phụ đề";
                    subtitleCol.ReadOnly = true;
                    subtitleCol.ToolTipText = "Bấm để mở bảng phụ đề — cài hook và thân riêng.";
                }

                var musicCol = dgvDeepDiveInput.Columns["colAiShowcaseMusicVolume"];
                if (musicCol != null)
                {
                    musicCol.HeaderText = "Âm thanh";
                    musicCol.ReadOnly = true;
                    musicCol.ToolTipText = "Bấm để chọn nhạc nền, âm lượng, tốc độ thoại và nghe audio.";
                }

                var logoCol = dgvDeepDiveInput.Columns["colAiShowcaseBrandLogo"];
                if (logoCol != null)
                {
                    logoCol.HeaderText = "Logo";
                    logoCol.ReadOnly = true;
                    logoCol.FillWeight = 10;
                    logoCol.MinimumWidth = 88;
                    logoCol.ToolTipText = "Bấm để bật logo, chọn vị trí, scale. Kho logo: Assets\\Logos.";
                }
            }
        }

        private void DgvDeepDiveInput_ShowcaseRowHeightsAfterBind(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (IsDeepDiveModeTab())
            {
                ApplyShowcaseDeepDiveRowHeights();
            }
        }

        private static string TruncateShowcaseGridText(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();
            if (trimmed.Length <= maxChars)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxChars) + "…";
        }

        private static void ApplyShowcaseGridCellToolTip(DataGridViewRow row, int columnIndex, string fullText)
        {
            if (row == null || columnIndex < 0 || columnIndex >= row.Cells.Count)
            {
                return;
            }

            row.Cells[columnIndex].ToolTipText = string.IsNullOrWhiteSpace(fullText) ? string.Empty : fullText.Trim();
        }

        private static void SetGridColumnDisplayIndex(DataGridView grid, string columnName, int displayIndex)
        {
            var column = grid?.Columns[columnName];
            if (column != null)
            {
                column.DisplayIndex = displayIndex;
            }
        }

        private void ConfigureProductInputGrid(DataGridView grid)

        {

            if (grid == null)

            {

                return;

            }



            grid.AutoGenerateColumns = false;

            grid.Columns.Clear();

            grid.Columns.Add(new DataGridViewComboBoxColumn

            {

                Name = "colAiProfile",

                HeaderText = "Profile",

                DataPropertyName = nameof(AiVideoGenInputItem.ProfileName),

                DisplayMember = nameof(ProfileComboEntry.Name),

                ValueMember = nameof(ProfileComboEntry.Name),

                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,

                FlatStyle = FlatStyle.Flat,

                FillWeight = 14,

                MinimumWidth = 72,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiProduct",

                HeaderText = "Sản phẩm",

                DataPropertyName = nameof(AiVideoGenInputItem.ProductName),

                FillWeight = 32,

                MinimumWidth = 120,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiUrl",

                HeaderText = "URL video",

                DataPropertyName = nameof(AiVideoGenInputItem.VideoUrl),

                FillWeight = 22,

                MinimumWidth = 100,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiHook",

                HeaderText = "Hook",

                DataPropertyName = nameof(AiVideoGenInputItem.HookText),

                FillWeight = 20,

                MinimumWidth = 88,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiHashtag",

                HeaderText = "Hashtag",

                DataPropertyName = nameof(AiVideoGenInputItem.Hashtags),

                FillWeight = 18,

                MinimumWidth = 80,

                ReadOnly = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiPrice",

                HeaderText = "Giá",

                DataPropertyName = nameof(AiVideoGenInputItem.Price),

                FillWeight = 10,

                MinimumWidth = 56,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiStatus",

                HeaderText = "Trạng thái",

                DataPropertyName = nameof(AiVideoGenInputItem.PipelineStatus),

                FillWeight = 14,

                MinimumWidth = 88,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseOutput",

                HeaderText = "Output",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseOutputGridLabel),

                FillWeight = 18,

                MinimumWidth = 120,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiSafety",

                HeaderText = "SafetyScore",

                DataPropertyName = nameof(AiVideoGenInputItem.SafetyScore),

                FillWeight = 10,

                MinimumWidth = 64,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiThumb",

                HeaderText = "Thumbnail Preview",

                DataPropertyName = nameof(AiVideoGenInputItem.ThumbnailPath),

                FillWeight = 16,

                MinimumWidth = 100,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiKeyword",

                HeaderText = "Từ khóa",

                DataPropertyName = nameof(AiVideoGenInputItem.SourceKeyword),

                FillWeight = 18,

                MinimumWidth = 80,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiImage",

                HeaderText = "Ảnh URL",

                DataPropertyName = nameof(AiVideoGenInputItem.ImageUrl),

                FillWeight = 22,

                MinimumWidth = 100,

                ReadOnly = true

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiSceneTitle",

                HeaderText = "Tên cảnh",

                DataPropertyName = nameof(AiVideoGenInputItem.SceneTitle),

                FillWeight = 24,

                MinimumWidth = 100,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiSceneRole",

                HeaderText = "Vai trò",

                DataPropertyName = nameof(AiVideoGenInputItem.SceneRole),

                FillWeight = 12,

                MinimumWidth = 72,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiClip",

                HeaderText = "Clip",

                DataPropertyName = nameof(AiVideoGenInputItem.ClipPath),

                FillWeight = 14,

                MinimumWidth = 88,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiSceneVoice",

                HeaderText = "Voice",

                DataPropertyName = nameof(AiVideoGenInputItem.SceneVoiceover),

                FillWeight = 22,

                MinimumWidth = 100,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiVeoPrompt",

                HeaderText = "Prompt Veo",

                DataPropertyName = nameof(AiVideoGenInputItem.VeoPrompt),

                FillWeight = 26,

                MinimumWidth = 120,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewComboBoxColumn

            {

                Name = "colAiShowcaseProductType",

                HeaderText = "Loại SP",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseProductTypePrompt),

                DisplayMember = nameof(ShowcaseProductTypePreset.DisplayLabel),

                ValueMember = nameof(ShowcaseProductTypePreset.PromptHint),

                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,

                FlatStyle = FlatStyle.Flat,

                FillWeight = 20,

                MinimumWidth = 148,

                ReadOnly = false,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewComboBoxColumn

            {

                Name = "colAiShowcaseClipMode",

                HeaderText = "Công cụ Video",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseClipModeId),

                DisplayMember = nameof(ShowcaseClipModePreset.DisplayLabel),

                ValueMember = nameof(ShowcaseClipModePreset.Id),

                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,

                FlatStyle = FlatStyle.Flat,

                FillWeight = 22,

                MinimumWidth = 168,

                ReadOnly = false,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewComboBoxColumn

            {

                Name = "colAiShowcaseOutputAspect",

                HeaderText = "Khung video",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseOutputAspectId),

                DisplayMember = nameof(ShowcaseOutputAspectPreset.DisplayLabel),

                ValueMember = nameof(ShowcaseOutputAspectPreset.Id),

                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,

                FlatStyle = FlatStyle.Flat,

                FillWeight = 20,

                MinimumWidth = 156,

                ReadOnly = false,

                Visible = false,

                ToolTipText = "Tỉ lệ video xuất: 9:16 TikTok, 16:9 ngang, 1:1 vuông — chọn trước khi tạo clip/render."

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseTheme",

                HeaderText = "Chủ đề",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseThemeGridLabel),

                FillWeight = 26,

                MinimumWidth = 180,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseUserTheme",

                HeaderText = "Chủ đề tùy chỉnh",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseUserTheme),

                FillWeight = 24,

                MinimumWidth = 160,

                ReadOnly = false,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseImages",

                HeaderText = "Ảnh",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseImagesGridLabel),

                FillWeight = 28,

                MinimumWidth = 200,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseSceneSummary",

                HeaderText = "Phân cảnh",

                DataPropertyName = nameof(ShowcaseVideoItem.SceneCountDisplay),

                FillWeight = 22,

                MinimumWidth = 108,

                ReadOnly = true,

                Visible = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseScript",

                HeaderText = "Kịch bản",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseScriptLabel),

                FillWeight = 16,

                MinimumWidth = 120,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseScenePrompt",

                HeaderText = "Prompt",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseScenePromptLabel),

                FillWeight = 18,

                MinimumWidth = 140,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });

            grid.Columns.Add(new DataGridViewCheckBoxColumn

            {

                Name = "colAiShowcaseMultiVoice",

                HeaderText = "Đa giọng",

                DataPropertyName = nameof(AiVideoGenInputItem.ShowcaseMultiVoice),

                FillWeight = 10,

                MinimumWidth = 64,

                ReadOnly = false,

                Visible = false,

                ThreeState = false

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseTextSize",

                HeaderText = "Phụ đề",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseSubtitleStyleLabel),

                FillWeight = 14,

                MinimumWidth = 120,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseMusicVolume",

                HeaderText = "Âm thanh",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseMusicLabel),

                FillWeight = 12,

                MinimumWidth = 100,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseBrandLogo",

                HeaderText = "Logo",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseBrandLogoLabel),

                FillWeight = 10,

                MinimumWidth = 88,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });

            grid.Columns.Add(new DataGridViewTextBoxColumn

            {

                Name = "colAiShowcaseTransition",

                HeaderText = "Chuyển cảnh",

                DataPropertyName = nameof(ShowcaseVideoItem.ShowcaseTransitionLabel),

                FillWeight = 10,

                MinimumWidth = 96,

                ReadOnly = true,

                Visible = false,

                DefaultCellStyle =
                {
                    ForeColor = Color.FromArgb(130, 175, 255),
                    SelectionForeColor = Color.White
                }

            });



            ApplyAiVideoGenProfileComboColumn(grid);

            grid.CellFormatting -= ProductInputGrid_CellFormatting;

            grid.CellFormatting += ProductInputGrid_CellFormatting;

            grid.CellToolTipTextNeeded -= ProductInputGrid_CellToolTipTextNeeded;

            grid.CellToolTipTextNeeded += ProductInputGrid_CellToolTipTextNeeded;

            grid.RowPrePaint -= ProductInputGrid_RowPrePaint;

            grid.RowPrePaint += ProductInputGrid_RowPrePaint;

            grid.CurrentCellDirtyStateChanged -= ProductInputGrid_CurrentCellDirtyStateChanged;

            grid.CurrentCellDirtyStateChanged += ProductInputGrid_CurrentCellDirtyStateChanged;

            grid.DataError -= ProductInputGrid_DataError;

            grid.DataError += ProductInputGrid_DataError;

            grid.CellValueChanged -= ProductInputGrid_CellValueChanged;

            grid.CellValueChanged += ProductInputGrid_CellValueChanged;

            grid.CellEndEdit -= ProductInputGrid_CellEndEdit;

            grid.CellEndEdit += ProductInputGrid_CellEndEdit;

            if (string.Equals(grid.Name, "dgvDeepDive", StringComparison.OrdinalIgnoreCase))
            {
                grid.CellMouseClick -= DgvDeepDiveInput_ShowcaseEditorCellMouseClick;
                grid.CellMouseClick += DgvDeepDiveInput_ShowcaseEditorCellMouseClick;
                grid.CellMouseClick -= DgvDeepDiveInput_ShowcaseImagesCellMouseClick;
                grid.CellMouseClick += DgvDeepDiveInput_ShowcaseImagesCellMouseClick;
                grid.CellPainting -= DgvDeepDiveInput_ShowcaseImagesCellPainting;
                grid.CellPainting += DgvDeepDiveInput_ShowcaseImagesCellPainting;
                grid.DataBindingComplete -= DgvDeepDiveInput_ShowcaseRowHeightsAfterBind;
                grid.DataBindingComplete += DgvDeepDiveInput_ShowcaseRowHeightsAfterBind;
            }

            if (IsSlideshowProductInputGrid(grid))
            {
                WireSlideshowProductGridDragDrop(grid);
            }

        }

        private static bool IsSlideshowProductInputGrid(DataGridView grid)
        {
            if (grid == null)
            {
                return false;
            }

            return string.Equals(grid.Name, "dgvSlideshow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(grid.Name, "dgvAiVideoGenInput", StringComparison.OrdinalIgnoreCase);
        }

        private void WireSlideshowProductGridDragDrop(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.AllowDrop = true;

            grid.MouseDown -= SlideshowProductGrid_MouseDown;
            grid.MouseDown += SlideshowProductGrid_MouseDown;
            grid.DragOver -= SlideshowProductGrid_DragOver;
            grid.DragOver += SlideshowProductGrid_DragOver;
            grid.DragDrop -= SlideshowProductGrid_DragDrop;
            grid.DragDrop += SlideshowProductGrid_DragDrop;
        }

        private void SlideshowProductGrid_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var grid = sender as DataGridView;
            if (grid == null || grid.IsCurrentCellInEditMode)
            {
                return;
            }

            var hit = grid.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0 || hit.RowIndex >= grid.Rows.Count || grid.Rows[hit.RowIndex].IsNewRow)
            {
                return;
            }

            if (!(grid.Rows[hit.RowIndex].DataBoundItem is AiVideoGenInputItem item))
            {
                return;
            }

            grid.ClearSelection();
            grid.Rows[hit.RowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[Math.Max(0, hit.ColumnIndex)];

            grid.DoDragDrop(item, DragDropEffects.Move);
        }

        private void SlideshowProductGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(AiVideoGenDragDropFormat)
                || e.Data.GetDataPresent(typeof(AiVideoGenInputItem)))
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void SlideshowProductGrid_DragDrop(object sender, DragEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
            {
                return;
            }

            var source = ExtractDraggedAiVideoGenItem(e.Data);
            if (source == null)
            {
                return;
            }

            var client = grid.PointToClient(new Point(e.X, e.Y));
            var visibleInsertIndex = ResolveSlideshowVisibleInsertIndex(grid, client, out var originalVisibleIndex, source);
            if (visibleInsertIndex < 0)
            {
                return;
            }

            if (originalVisibleIndex >= 0
                && (visibleInsertIndex == originalVisibleIndex || visibleInsertIndex == originalVisibleIndex + 1))
            {
                return;
            }

            if (!TryMoveSlideshowItemToVisibleIndex(source, visibleInsertIndex))
            {
                return;
            }

            SyncBuffersToGrids();
            _slideshowDraftDirty = true;
            SelectSlideshowGridItem(grid, source);
        }

        private static AiVideoGenInputItem ExtractDraggedAiVideoGenItem(IDataObject data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.GetDataPresent(typeof(AiVideoGenInputItem)))
            {
                return data.GetData(typeof(AiVideoGenInputItem)) as AiVideoGenInputItem;
            }

            if (data.GetDataPresent(AiVideoGenDragDropFormat))
            {
                return data.GetData(AiVideoGenDragDropFormat) as AiVideoGenInputItem;
            }

            return null;
        }

        private int ResolveSlideshowVisibleInsertIndex(
            DataGridView grid,
            Point clientPoint,
            out int sourceVisibleIndex,
            AiVideoGenInputItem source)
        {
            sourceVisibleIndex = -1;
            if (grid == null)
            {
                return -1;
            }

            var visibleBefore = GetSlideshowBuffer()
                .Where(ShouldShowAiVideoGenItem)
                .ToList();
            sourceVisibleIndex = FindBufferItemIndex(visibleBefore, source);

            var hit = grid.HitTest(clientPoint.X, clientPoint.Y);
            if (hit.RowIndex >= 0 && hit.RowIndex < grid.Rows.Count && !grid.Rows[hit.RowIndex].IsNewRow)
            {
                var insertAfter = false;
                var rowRect = grid.GetRowDisplayRectangle(hit.RowIndex, false);
                if (rowRect.Height > 0)
                {
                    insertAfter = clientPoint.Y > rowRect.Top + (rowRect.Height / 2);
                }

                return hit.RowIndex + (insertAfter ? 1 : 0);
            }

            return visibleBefore.Count;
        }

        private bool TryMoveSlideshowItemToVisibleIndex(AiVideoGenInputItem source, int visibleInsertIndex)
        {
            var buffer = GetSlideshowBuffer();
            var from = FindBufferItemIndex(buffer, source);
            if (from < 0)
            {
                return false;
            }

            var item = buffer[from];
            buffer.RemoveAt(from);

            var visible = buffer.Where(ShouldShowAiVideoGenItem).ToList();
            visibleInsertIndex = Math.Max(0, Math.Min(visibleInsertIndex, visible.Count));

            int bufferInsertIndex;
            if (visibleInsertIndex >= visible.Count)
            {
                bufferInsertIndex = buffer.Count;
            }
            else
            {
                bufferInsertIndex = FindBufferItemIndex(buffer, visible[visibleInsertIndex]);
                if (bufferInsertIndex < 0)
                {
                    bufferInsertIndex = buffer.Count;
                }
            }

            buffer.Insert(bufferInsertIndex, item);
            return true;
        }

        private static int FindBufferItemIndex(IList<AiVideoGenInputItem> list, AiVideoGenInputItem item)
        {
            if (list == null || item == null)
            {
                return -1;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    return i;
                }
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (AiVideoGenItemsMatch(list[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SelectSlideshowGridItem(DataGridView grid, AiVideoGenInputItem item)
        {
            if (grid == null || item == null)
            {
                return;
            }

            for (var i = 0; i < grid.Rows.Count; i++)
            {
                var row = grid.Rows[i];
                if (row?.DataBoundItem is AiVideoGenInputItem bound
                    && (ReferenceEquals(bound, item) || AiVideoGenItemsMatch(bound, item)))
                {
                    grid.ClearSelection();
                    row.Selected = true;
                    if (row.Cells.Count > 0)
                    {
                        grid.CurrentCell = row.Cells[0];
                    }

                    grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i);
                    break;
                }
            }
        }



        private static readonly List<ShowcaseThemePreset> ShowcaseThemePresetComboSource =
            ShowcaseThemePresets.All.ToList();

        private static readonly List<ShowcaseProductTypePreset> ShowcaseProductTypePresetComboSource =
            ShowcaseProductTypePresets.All.ToList();

        private static readonly List<ShowcaseClipModePreset> ShowcaseClipModePresetComboSource =
            ShowcaseClipModePresets.All.ToList();

        private static readonly List<ShowcaseOutputAspectPreset> ShowcaseOutputAspectPresetComboSource =
            ShowcaseOutputAspectPresets.All.ToList();

        private static void ApplyShowcaseThemeComboColumn(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.DisplayMember = nameof(ShowcaseThemePreset.DisplayLabel);
            column.ValueMember = nameof(ShowcaseThemePreset.PromptHint);
            column.DataSource = ShowcaseThemePresetComboSource;
        }

        private static void ApplyShowcaseProductTypeComboColumn(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.DisplayMember = nameof(ShowcaseProductTypePreset.DisplayLabel);
            column.ValueMember = nameof(ShowcaseProductTypePreset.PromptHint);
            column.DataSource = ShowcaseProductTypePresetComboSource;
        }

        private static void ApplyShowcaseClipModeComboColumn(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.DisplayMember = nameof(ShowcaseClipModePreset.DisplayLabel);
            column.ValueMember = nameof(ShowcaseClipModePreset.Id);
            column.DataSource = ShowcaseClipModePresetComboSource;
        }

        private static void ApplyShowcaseOutputAspectComboColumn(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.DisplayMember = nameof(ShowcaseOutputAspectPreset.DisplayLabel);
            column.ValueMember = nameof(ShowcaseOutputAspectPreset.Id);
            column.DataSource = ShowcaseOutputAspectPresetComboSource;
        }

        private void ApplyGridProfileComboColumn(DataGridView grid, string columnName)

        {

            if (grid == null)

            {

                return;

            }



            if (!(grid.Columns[columnName] is DataGridViewComboBoxColumn profileColumn))

            {

                return;

            }



            profileColumn.DisplayMember = nameof(ProfileComboEntry.Name);

            profileColumn.ValueMember = nameof(ProfileComboEntry.Name);

            profileColumn.DataSource = _aiVideoGenProfileComboSource;

        }



        private void ApplyAiVideoGenProfileComboColumn(DataGridView grid)

        {

            ApplyGridProfileComboColumn(grid, "colAiProfile");

        }

        private void RefreshGridProfileComboSource(AppSettings settings)
        {
            _aiVideoGenProfileComboSource = ConfigManager.BuildProfileComboEntries(settings ?? new AppSettings());
        }

        private void ApplyVideoReupProfileComboColumn()
        {
            ApplyGridProfileComboColumn(dgvVideoReupInput, "colReupProfile");
        }

        private void EnsureProfileComboIncludes(string profileName)
        {
            var n = (profileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(n))
            {
                return;
            }

            if (_aiVideoGenProfileComboSource == null)
            {
                _aiVideoGenProfileComboSource = new List<ProfileComboEntry>();
            }

            foreach (var entry in _aiVideoGenProfileComboSource)
            {
                if (entry != null && string.Equals(entry.Name, n, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _aiVideoGenProfileComboSource.Add(new ProfileComboEntry { Name = n });
        }




        private void ProductInputGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)

        {

            var grid = sender as DataGridView;

            if (grid == null || !grid.IsCurrentCellDirty)

            {

                return;

            }



            if (grid.CurrentCell is DataGridViewComboBoxCell)

            {

                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            }

            else if (grid.CurrentCell is DataGridViewCheckBoxCell)

            {

                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            }

        }



        private void ProductInputGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)

        {

            if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(sender is DataGridView grid) || grid != dgvDeepDiveInput)

            {

                return;

            }



            var columnName = grid.Columns[e.ColumnIndex]?.Name;

            if (!IsShowcaseSettingsGridColumn(columnName))

            {

                return;

            }



            if (grid.Rows[e.RowIndex].DataBoundItem is ShowcaseVideoItem video)

            {

                if (string.Equals(columnName, "colAiProduct", StringComparison.Ordinal))
                {
                    video.ProductName = Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value) ?? string.Empty;
                }
                else if (string.Equals(columnName, "colAiShowcaseProductType", StringComparison.Ordinal))
                {
                    video.ShowcaseProductTypePrompt = Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value) ?? string.Empty;
                    NotifyShowcaseDraftDirty();
                }
                else if (string.Equals(columnName, "colAiShowcaseClipMode", StringComparison.Ordinal))
                {
                    video.ShowcaseClipModeId = ShowcaseClipModePresets.ResolveIdForGemini(
                        Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));
                    NotifyShowcaseDraftDirty();
                }
                else if (string.Equals(columnName, "colAiShowcaseOutputAspect", StringComparison.Ordinal))
                {
                    video.ShowcaseOutputAspectId = ShowcaseOutputAspectPresets.ResolveId(
                        Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value),
                        null);
                    NotifyShowcaseDraftDirty();
                }

                SyncShowcaseVideoSettingsToScenes(video);

                return;

            }



            if (grid.Rows[e.RowIndex].DataBoundItem is AiVideoGenInputItem item)

            {

                SyncShowcaseSessionSettingsAcrossScenes(item);

            }

        }



        private void ProductInputGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)

        {

            if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(sender is DataGridView grid) || grid != dgvDeepDiveInput)

            {

                return;

            }



            var rowItem = grid.Rows[e.RowIndex].DataBoundItem;
            AiVideoGenInputItem item = rowItem as AiVideoGenInputItem;
            var video = rowItem as ShowcaseVideoItem;
            if (item == null && video == null)
            {
                return;
            }

            var columnName = grid.Columns[e.ColumnIndex]?.Name;

            if (string.Equals(columnName, "colAiProduct", StringComparison.Ordinal) && video != null)
            {
                video.ProductName = Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value) ?? string.Empty;
            }

            if (IsShowcaseSettingsGridColumn(columnName) || (video != null && string.Equals(columnName, "colAiProduct", StringComparison.Ordinal)))
            {
                if (video != null)
                {
                    SyncShowcaseVideoSettingsToScenes(video);
                }
                else
                {
                    SyncShowcaseSessionSettingsAcrossScenes(item);
                }
            }

        }



        private void ProductInputGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)

        {

            if (e.ColumnIndex < 0)

            {

                return;

            }



            var grid = sender as DataGridView;

            var col = grid?.Columns[e.ColumnIndex];

            if (col?.Name == "colAiProfile")

            {

                e.ThrowException = false;

            }

            else if (IsShowcaseSettingsGridColumn(col?.Name))

            {

                e.ThrowException = false;

            }

        }



        private void ProductInputGrid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var col = grid.Columns[e.ColumnIndex];
            if (col == null)
            {
                return;
            }

            var row = grid.Rows[e.RowIndex];
            if (grid != dgvDeepDiveInput || !(row?.DataBoundItem is ShowcaseVideoItem video))
            {
                return;
            }

            if (string.Equals(col.Name, "colAiShowcaseProductType", StringComparison.Ordinal))
            {
                e.ToolTipText = ShowcaseContentDisplayHelper.FormatProductTypeThemeGridToolTip(video);
                return;
            }

            if (string.Equals(col.Name, "colAiShowcaseScript", StringComparison.Ordinal))
            {
                e.ToolTipText = ShowcaseContentDisplayHelper.FormatScriptPromptGridToolTip(video);
            }
        }

        private void ProductInputGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)

        {

            var grid = sender as DataGridView;

            if (grid == null || e.RowIndex < 0)

            {

                return;

            }



            var col = grid.Columns[e.ColumnIndex];

            if (col == null)

            {

                return;

            }



            var row = grid.Rows[e.RowIndex];

            if (grid == dgvDeepDiveInput &&
                col.Name == "colAiProduct" &&
                row?.DataBoundItem is ShowcaseVideoItem)
            {
                e.CellStyle.ForeColor = Color.FromArgb(130, 175, 255);
                e.CellStyle.SelectionForeColor = Color.White;
                e.CellStyle.WrapMode = DataGridViewTriState.True;
                return;
            }

            if (grid == dgvDeepDiveInput && row?.DataBoundItem is ShowcaseVideoItem showcaseVideo)
            {
                if (col.Name == "colAiStatus")
                {
                    var status = string.IsNullOrWhiteSpace(showcaseVideo.PipelineStatus)
                        ? "Chờ"
                        : showcaseVideo.PipelineStatus.Trim();
                    e.Value = status;
                    if (string.Equals(status, "Xong", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(120, 220, 160);
                    }
                    else if (string.Equals(status, "Lỗi", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(230, 140, 120);
                    }
                    else if (string.Equals(status, "Đang render", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(255, 200, 120);
                    }

                    row.Cells[e.ColumnIndex].ToolTipText = "Trạng thái pipeline render.";
                    e.FormattingApplied = true;
                    return;
                }

                if (col.Name == "colAiShowcaseOutput")
                {
                    e.Value = string.Empty;
                    var outputPath = ShowcaseContentDisplayHelper.TryResolveFinishedVideoPath(showcaseVideo);
                    row.Cells[e.ColumnIndex].ToolTipText =
                        ShowcaseContentDisplayHelper.FormatOutputGridLabel(showcaseVideo, outputPath)
                        + " — ▶ xem video · 📂 thư mục output";
                    e.FormattingApplied = true;
                    return;
                }

                if (col.Name == "colAiShowcaseScript")
                {
                    e.Value = showcaseVideo.ShowcaseScriptLabel
                              ?? ShowcaseContentDisplayHelper.FormatScriptPromptGridLabel(showcaseVideo);
                    e.CellStyle.WrapMode = DataGridViewTriState.True;
                    row.Cells[e.ColumnIndex].ToolTipText =
                        ShowcaseContentDisplayHelper.FormatScriptPromptGridToolTip(showcaseVideo);
                    e.FormattingApplied = true;
                    return;
                }

                if (col.Name == "colAiShowcaseProductType")
                {
                    e.Value = showcaseVideo.ShowcaseGeminiSetupGridLabel
                              ?? ShowcaseContentDisplayHelper.FormatProductTypeThemeGridLabel(showcaseVideo);
                    e.CellStyle.ForeColor = Color.FromArgb(130, 175, 255);
                    e.CellStyle.SelectionForeColor = Color.White;
                    e.CellStyle.WrapMode = DataGridViewTriState.True;
                    row.Cells[e.ColumnIndex].ToolTipText =
                        ShowcaseContentDisplayHelper.FormatProductTypeThemeGridToolTip(showcaseVideo);
                    e.FormattingApplied = true;
                    return;
                }

                if (col.Name == "colAiShowcaseImages")
                {
                    e.Value = string.Empty;
                    row.Cells[e.ColumnIndex].ToolTipText =
                        (showcaseVideo.ShowcaseImagesGridLabel ?? "0 ảnh") + " — trái: thêm · phải: thư mục";
                    e.FormattingApplied = true;
                    return;
                }

                if (col.Name == "colAiShowcaseSceneSummary")
                {
                    e.Value = string.Empty;
                    row.Cells[e.ColumnIndex].ToolTipText =
                        (showcaseVideo.SceneCountDisplay ?? "0 cảnh") + " — trái: thêm clip · phải: veo_clips";
                    e.FormattingApplied = true;
                    return;
                }
            }

            if (!(row?.DataBoundItem is AiVideoGenInputItem item))

            {

                return;

            }



            if (col.Name == "colAiStatus")

            {

                if (!string.IsNullOrWhiteSpace(item.PipelineStatus) && item.PipelineStatus != "Chờ")

                {

                    e.Value = item.PipelineStatus;

                }

                else

                {

                    e.Value = item.IsProcessed ? "Xong" : "Chờ";

                }



                e.FormattingApplied = true;

                return;

            }



            if (col.Name == "colAiClip")

            {

                var hasClip = !string.IsNullOrWhiteSpace(item.ClipPath) && File.Exists(item.ClipPath);

                e.Value = hasClip ? "✓ Có clip" : "✗ Chưa có";

                e.CellStyle.ForeColor = hasClip ? Color.FromArgb(120, 220, 160) : Color.FromArgb(230, 140, 120);

                e.FormattingApplied = true;

                return;

            }

            if (grid == dgvDeepDiveInput && col.Name == "colAiSceneVoice")

            {

                var full = item.SceneVoiceover ?? string.Empty;

                ApplyShowcaseGridCellToolTip(row, e.ColumnIndex, full);

                e.Value = TruncateShowcaseGridText(full, ShowcaseGridVoicePreviewChars);

                e.FormattingApplied = true;

                return;

            }

            if (grid == dgvDeepDiveInput && col.Name == "colAiVeoPrompt")

            {

                var full = item.VeoPrompt ?? string.Empty;

                ApplyShowcaseGridCellToolTip(row, e.ColumnIndex, full);

                e.Value = TruncateShowcaseGridText(full, ShowcaseGridVeoPromptPreviewChars);

                e.FormattingApplied = true;

                return;

            }



            if (col.Name == "colAiImage" && grid == dgvDeepDiveInput)

            {

                var imageRef = item.ImageUrl ?? string.Empty;

                if (string.IsNullOrWhiteSpace(imageRef))

                {

                    return;

                }



                if (imageRef.StartsWith("http", StringComparison.OrdinalIgnoreCase))

                {

                    e.Value = imageRef.Length > 48 ? imageRef.Substring(0, 45) + "…" : imageRef;

                }

                else

                {

                    e.Value = Path.GetFileName(imageRef);

                }



                e.FormattingApplied = true;

            }

        }



        private void ProductInputGrid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)

        {

            var grid = sender as DataGridView;

            if (grid == null || e.RowIndex < 0)

            {

                return;

            }



            var row = grid.Rows[e.RowIndex];

            if (row?.DataBoundItem is AiVideoGenInputItem item && item.IsProcessed)

            {

                row.DefaultCellStyle.BackColor = AiVideoGenProcessedRowBack;

                row.DefaultCellStyle.ForeColor = AiVideoGenProcessedRowFore;

                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(120, 180, 140);

                row.DefaultCellStyle.SelectionForeColor = Color.White;

            }

            else if (grid == dgvDeepDiveInput &&
                     row?.DataBoundItem is ShowcaseVideoItem showcaseRow &&
                     string.Equals(showcaseRow.PipelineStatus, "Xong", StringComparison.OrdinalIgnoreCase))

            {

                row.DefaultCellStyle.BackColor = AiVideoGenProcessedRowBack;

                row.DefaultCellStyle.ForeColor = AiVideoGenProcessedRowFore;

                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(120, 180, 140);

                row.DefaultCellStyle.SelectionForeColor = Color.White;

            }

            else if (row != null)

            {

                row.DefaultCellStyle.BackColor = Color.FromArgb(20, 22, 28);

                row.DefaultCellStyle.ForeColor = Color.Gainsboro;

                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);

                row.DefaultCellStyle.SelectionForeColor = Color.White;

            }

        }



        private void ChkAiVideoGenCurrentProfileOnly_CheckedChanged(object sender, EventArgs e)

        {

            SyncBuffersToGrids();

        }



        private void MarkSlideshowItemsProcessed(IEnumerable<AiVideoGenInputItem> renderedItems)

        {

            MarkBufferItemsProcessed(GetSlideshowBuffer(), renderedItems, SyncBuffersToGrids, () => _slideshowDraftDirty = true);

        }



        private void MarkDeepDiveItemsProcessed(IEnumerable<AiVideoGenInputItem> renderedItems)

        {

            MarkBufferItemsProcessed(GetDeepDiveBuffer(), renderedItems, SyncBuffersToGrids, null);

            var video = GetActiveShowcaseVideo();
            if (video != null)
            {
                video.PipelineStatus = "Xong";
                video.RefreshDisplayFields();
            }

        }



        private void MarkBufferItemsProcessed(

            List<AiVideoGenInputItem> buffer,

            IEnumerable<AiVideoGenInputItem> renderedItems,

            Action refreshGrid,

            Action onChanged)

        {

            if (renderedItems == null || buffer == null)

            {

                return;

            }



            var changed = false;

            foreach (var rendered in renderedItems)

            {

                if (rendered == null)

                {

                    continue;

                }



                var match = buffer.FirstOrDefault(x => AiVideoGenItemsMatch(x, rendered));

                if (match != null && !match.IsProcessed)

                {

                    match.IsProcessed = true;

                    changed = true;

                }

            }



            if (changed)

            {

                onChanged?.Invoke();

                refreshGrid?.Invoke();

            }

        }

        private List<AiVideoGenInputItem> GetSelectedAiVideoGenItemsForPipeline()
        {
            if (!TryGetSelectedAiVideoGenItems(out var selected))
            {
                return new List<AiVideoGenInputItem>();
            }

            return selected
                .Where(x => x != null)
                .ToList();
        }

        private List<AiVideoGenInputItem> CloneAiVideoGenItemsForPipeline(IEnumerable<AiVideoGenInputItem> items)
        {
            return (items ?? Enumerable.Empty<AiVideoGenInputItem>())
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList();
        }

        private void SetAiVideoGenPipelineProgress(VideoRenderProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            progress.Slot = progress.Slot <= 0 ? 1 : progress.Slot;
            progress.VideoIndex = progress.VideoIndex <= 0 ? 1 : progress.VideoIndex;
            progress.TotalVideos = progress.TotalVideos <= 0 ? 1 : progress.TotalVideos;
            UpdateAiRenderProgress(progress);
        }

    }

}


