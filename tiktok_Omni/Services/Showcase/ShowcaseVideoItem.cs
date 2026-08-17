using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;



namespace tiktok_Omni.Services.Showcase

{

    /// <summary>Một dòng lưới Showcase = một video hoàn chỉnh (các cảnh nằm trên storyboard).</summary>

    public sealed class ShowcaseVideoItem

    {

        public Guid VideoId { get; set; } = Guid.NewGuid();



        public string ProfileName { get; set; } = string.Empty;



        public string ProductName { get; set; } = string.Empty;



        public string ShowcaseTheme { get; set; } = string.Empty;

        /// <summary>Góc quảng cáo do người dùng chọn (preset) — gửi Gemini trước khi sinh kịch bản.</summary>
        public string ShowcaseThemePrompt { get; set; } = string.Empty;

        /// <summary>Chủ đề do người dùng tự gõ — ưu tiên hơn preset khi gửi Gemini.</summary>
        public string ShowcaseUserTheme { get; set; } = string.Empty;

        /// <summary>Loại sản phẩm/trang phục do người dùng chọn (preset) — gửi Gemini trước khi sinh kịch bản.</summary>
        public string ShowcaseProductTypePrompt { get; set; } = string.Empty;

        /// <summary>Chế độ tạo clip do người dùng chọn (preset id) — gửi Gemini trước khi sinh kịch bản.</summary>
        public string ShowcaseClipModeId { get; set; } = ShowcaseClipModePresets.DefaultId;

        /// <summary>Định dạng video (Quảng cáo SP / Kể chuyện / Tutorial / Không CTA) — gửi Gemini trước khi sinh kịch bản.</summary>
        public string ShowcaseVideoFormatId { get; set; } = ShowcaseVideoFormatPresets.DefaultId;

        /// <summary>Khung video: 9x16 | 16x9 | 1x1 | custom — cột lưới «Khung video».</summary>
        public string ShowcaseOutputAspectId { get; set; } = ShowcaseOutputAspectPresets.DefaultId;

        /// <summary>Chiều rộng khi <see cref="ShowcaseOutputAspectId"/> = custom (mặc định 1080).</summary>
        public int ShowcaseOutputAspectCustomWidth { get; set; } = ShowcaseOutputAspectPresets.DefaultCustomWidth;

        /// <summary>Chiều cao khi <see cref="ShowcaseOutputAspectId"/> = custom (mặc định 1350).</summary>
        public int ShowcaseOutputAspectCustomHeight { get; set; } = ShowcaseOutputAspectPresets.DefaultCustomHeight;

        public string ShowcaseHookText { get; set; } = string.Empty;

        /// <summary>Chữ hook trên video — để trống = hiện đủ thoại hook (TTS không đổi).</summary>
        public string ShowcaseSubtitleDisplayHook { get; set; } = string.Empty;

        /// <summary>Hiệu ứng hook trên tab Chữ hiển thị — rỗng = tab Kiểu chữ.</summary>
        public string ShowcaseSubtitleDisplayHookAnimation { get; set; } = string.Empty;

        public string ShowcaseCtaText { get; set; } = string.Empty;

        /// <summary>Chữ CTA trên video — để trống = hiện đủ CTA thoại.</summary>
        public string ShowcaseSubtitleDisplayCta { get; set; } = string.Empty;

        /// <summary>Hiệu ứng CTA trên tab Chữ hiển thị.</summary>
        public string ShowcaseSubtitleDisplayCtaAnimation { get; set; } = string.Empty;

        /// <summary>Tab Phụ đề — tắt burn-in dòng CTA (mặc định false = vẫn hiện).</summary>
        public bool ShowcaseSubtitleDisplayCtaDisabled { get; set; }

        /// <summary>Fingerprint clip lúc «Tạo lời thoại» — 0 = chỉ có thoại nháp từ «Tạo kịch bản» (ảnh).</summary>
        public long ShowcaseVoiceoverClipFingerprint { get; set; }

        /// <summary>Fingerprint đường dẫn clip (không gồm mtime) — phát hiện thay file cùng tên.</summary>
        public long ShowcaseVoiceoverClipPathFingerprint { get; set; }

        /// <summary>Thời lượng từng clip lúc «Tạo lời thoại» — dạng 1:3.45;2:5.1.</summary>
        public string ShowcaseVoiceoverClipDurationSignature { get; set; } = string.Empty;

        public string ShowcaseScriptLabel { get; set; } = string.Empty;

        public string ShowcaseScenePromptLabel { get; set; } = string.Empty;

        /// <summary>Nhãn cột «Lời thoại» — cập nhật qua <see cref="RefreshDisplayFields"/>.</summary>
        public string ShowcaseVoiceoverLabel { get; set; } = string.Empty;

        /// <summary>Nhãn gộp loại SP + chủ đề trên lưới.</summary>
        public string ShowcaseGeminiSetupGridLabel { get; private set; } = string.Empty;

        public bool ShowcaseMultiVoice { get; set; }



        /// <summary>Giữ tương thích scene — đồng bộ với <see cref="ShowcaseSubtitleFontSize"/>.</summary>

        public int ShowcaseTextSize { get; set; } = 72;



        public string ShowcaseSubtitleFontName { get; set; } = string.Empty;



        public int ShowcaseSubtitleFontSize { get; set; } = 72;



        public string ShowcaseSubtitlePosition { get; set; } = string.Empty;



        public string ShowcaseSubtitleAnimation { get; set; } = string.Empty;



        public bool ShowcaseSubtitleBold { get; set; } = true;



        public bool ShowcaseSubtitleItalic { get; set; }



        public int ShowcaseSubtitleWordsPerLine { get; set; } = 6;

        /// <summary>Bật burn-in phụ đề thân khi render — mặc định tắt.</summary>
        public bool ShowcaseSubtitleEnabled { get; set; }

        /// <summary>Bật phụ đề hook (giữa màn, hiệu ứng mạnh) — mặc định tắt.</summary>
        public bool ShowcaseHookSubtitleEnabled { get; set; }

        /// <summary>Hiệu ứng hook: PopStrong, Highlight, FadeIn, Slam.</summary>
        public string ShowcaseHookSubtitleAnimation { get; set; } = "PopStrong";

        public string ShowcaseHookSubtitleFontName { get; set; } = string.Empty;

        public int ShowcaseHookSubtitleFontSize { get; set; }

        /// <summary>ASS PrimaryColour thân — rỗng = trắng.</summary>
        public string ShowcaseSubtitlePrimaryColourAss { get; set; } = string.Empty;

        /// <summary>Viền/bóng thân — rỗng = mặc định.</summary>
        public string ShowcaseSubtitleDecorPreset { get; set; } = string.Empty;

        /// <summary>ASS PrimaryColour hook — rỗng = vàng catalog.</summary>
        public string ShowcaseHookSubtitlePrimaryColourAss { get; set; } = string.Empty;

        /// <summary>Viền/bóng hook.</summary>
        public string ShowcaseHookSubtitleDecorPreset { get; set; } = string.Empty;

        /// <summary>Preset gom font/màu/trang trí thân — <see cref="ShowcaseSubtitleLookPresetCatalog"/>.</summary>
        public string ShowcaseSubtitleLookPreset { get; set; } = string.Empty;

        /// <summary>Preset gom font/màu/trang trí hook.</summary>
        public string ShowcaseHookSubtitleLookPreset { get; set; } = string.Empty;

        /// <summary>Màu tô karaoke/highlight thân — rỗng = theo kiểu chữ.</summary>
        public string ShowcaseSubtitleHighlightColourAss { get; set; } = string.Empty;

        /// <summary>Màu tô hook — rỗng = theo kiểu hook.</summary>
        public string ShowcaseHookSubtitleHighlightColourAss { get; set; } = string.Empty;

        public string ShowcaseSubtitleStyleLabel { get; set; } = string.Empty;



        public string ShowcaseBackgroundMusicFile { get; set; } = string.Empty;



        public int ShowcaseMusicVolume { get; set; } = 14;

        /// <summary>0 = tự khớp thoại với clip; 50–200 = tốc độ thoại % (legacy — dùng hook/thân riêng).</summary>
        public int ShowcaseNarrationSpeedPercent { get; set; }

        /// <summary>Tốc độ timeline thoại hook — 50–200% (0 → 100%).</summary>
        public int ShowcaseHookNarrationSpeedPercent { get; set; }

        /// <summary>Tốc độ timeline thoại thân — 50–200% (0 → 100%).</summary>
        public int ShowcaseBodyNarrationSpeedPercent { get; set; }

        public string ShowcaseTtsEngine { get; set; } = string.Empty;

        /// <summary>EdgeTts | ElevenLabs — hook (cảnh đầu).</summary>
        public string ShowcaseHookTtsEngine { get; set; } = string.Empty;

        /// <summary>EdgeTts | ElevenLabs — thân từng cảnh.</summary>
        public string ShowcaseBodyTtsEngine { get; set; } = string.Empty;

        public string ShowcaseVoicePresetId { get; set; } = string.Empty;

        /// <summary>Id dải tuổi UI (ShowcaseVoicePresetDimensions.Age.*).</summary>
        public string ShowcaseVoiceAgeId { get; set; } = string.Empty;

        /// <summary>Giới tính giọng hook (ShowcaseVoicePresetDimensions.Gender.*) — Edge TTS: Hoài My / Nam Minh.</summary>
        public string ShowcaseVoiceGenderId { get; set; } = string.Empty;

        public string ShowcaseVoiceLanguageId { get; set; } = string.Empty;

        /// <summary>Giọng ElevenLabs cố định (ElevenVoicePersonaCatalog) cho hook — rỗng = mặc định theo preset/Cài đặt.</summary>
        public string ShowcaseHookElevenPersona { get; set; } = string.Empty;

        /// <summary>Tone giọng hook (ShowcaseVoicePresetDimensions.Tone.*) — ảnh hưởng voice_settings/audio tag ElevenLabs.</summary>
        public string ShowcaseVoiceToneId { get; set; } = string.Empty;

        /// <summary>3 số Tùy chỉnh hook khi ShowcaseVoiceToneId = Tone.Custom — % (0-100).</summary>
        public int ShowcaseElevenCustomStabilityPercent { get; set; }

        public int ShowcaseElevenCustomSimilarityPercent { get; set; }

        public int ShowcaseElevenCustomStylePercent { get; set; }

        /// <summary>Phong cách hook (HookStyleCatalog) — Edge prosody hook nhấn / thân êm.</summary>
        public string ShowcaseHookStyleKey { get; set; } = string.Empty;

        /// <summary>Offset rate Edge ±% (trên preset giọng).</summary>
        public int ShowcaseEdgeRateOffsetPercent { get; set; }

        /// <summary>Offset pitch Edge ±Hz.</summary>
        public int ShowcaseEdgePitchOffsetHz { get; set; }

        /// <summary>Preset Eleven / chiều giọng — thân (độc lập hook).</summary>
        public string ShowcaseBodyVoicePresetId { get; set; } = string.Empty;

        public string ShowcaseBodyVoiceAgeId { get; set; } = string.Empty;

        /// <summary>Giới tính giọng thân (ShowcaseVoicePresetDimensions.Gender.*).</summary>
        public string ShowcaseBodyVoiceGenderId { get; set; } = string.Empty;

        public string ShowcaseBodyVoiceLanguageId { get; set; } = string.Empty;

        /// <summary>Giọng ElevenLabs cố định (ElevenVoicePersonaCatalog) cho thân — rỗng = mặc định theo preset/Cài đặt.</summary>
        public string ShowcaseBodyElevenPersona { get; set; } = string.Empty;

        /// <summary>Tone giọng thân (ShowcaseVoicePresetDimensions.Tone.*) — ảnh hưởng voice_settings/audio tag ElevenLabs.</summary>
        public string ShowcaseBodyVoiceToneId { get; set; } = string.Empty;

        /// <summary>3 số Tùy chỉnh thân khi ShowcaseBodyVoiceToneId = Tone.Custom — % (0-100).</summary>
        public int ShowcaseBodyElevenCustomStabilityPercent { get; set; }

        public int ShowcaseBodyElevenCustomSimilarityPercent { get; set; }

        public int ShowcaseBodyElevenCustomStylePercent { get; set; }

        /// <summary>Phong cách Edge cho thân.</summary>
        public string ShowcaseBodyStyleKey { get; set; } = string.Empty;

        public int ShowcaseBodyEdgeRateOffsetPercent { get; set; }

        public int ShowcaseBodyEdgePitchOffsetHz { get; set; }

        public string ShowcaseMusicLabel { get; set; } = string.Empty;

        /// <summary>Bật chèn hiệu ứng âm thanh khi render (từng cảnh + CTA).</summary>
        public bool ShowcaseSfxMasterEnabled { get; set; } = true;

        /// <summary>SFX mở video (0s) — tách khỏi cảnh 1 / hook thoại.</summary>
        public string ShowcaseHookSfxFile { get; set; } = string.Empty;

        public bool ShowcaseHookSfxEnabled { get; set; }

        public double ShowcaseHookSfxOffsetSeconds { get; set; }

        public int ShowcaseHookSfxVolumePercent { get; set; }

        public string ShowcaseHookSfxGeminiHint { get; set; } = string.Empty;

        public string ShowcaseCtaSfxFile { get; set; } = string.Empty;

        public bool ShowcaseCtaSfxEnabled { get; set; }

        public double ShowcaseCtaSfxOffsetSeconds { get; set; }

        public int ShowcaseCtaSfxVolumePercent { get; set; }

        public string ShowcaseCtaSfxGeminiHint { get; set; } = string.Empty;

        /// <summary>Thư mục con trong kho clip quay tay (vd. hoc-sinh trong ao-dai) — dùng khi duyệt/thêm clip.</summary>
        public string ShowcaseCtaBrollSubLibraryId { get; set; } = string.Empty;

        /// <summary>Thư viện clip quay tay do người dùng chọn — rỗng = theo Loại SP (+ nhóm con).</summary>
        public string ShowcaseCtaBrollLibraryId { get; set; } = string.Empty;

        /// <summary>Nhãn cột «Công cụ Video» — cập nhật qua <see cref="RefreshDisplayFields"/>.</summary>
        public string ShowcaseClipModeGridLabel { get; private set; } = string.Empty;

        public double ShowcaseTransitionSeconds { get; set; } = 0.6;



        public string ShowcaseTransitionLabel { get; set; } = string.Empty;

        /// <summary>Chèn logo thương hiệu lên video thành phẩm (che watermark Flow).</summary>
        public bool ShowcaseBrandLogoEnabled { get; set; }

        /// <summary>Đường dẫn logo tùy chọn — rỗng = Assets\{profile}\logo.png.</summary>
        public string ShowcaseBrandLogoFile { get; set; } = string.Empty;

        public string ShowcaseBrandLogoPositionId { get; set; } = ShowcaseBrandLogoPositionCatalog.BottomRight;

        public int ShowcaseBrandLogoScaleWidthPercent { get; set; } = ShowcaseBrandOverlayHelper.DefaultScaleWidthPercent;

        public int ShowcaseBrandLogoMarginX { get; set; } = ShowcaseBrandOverlayHelper.DefaultMargin;

        public int ShowcaseBrandLogoMarginY { get; set; } = ShowcaseBrandOverlayHelper.DefaultMargin;

        public int ShowcaseBrandLogoOpacityPercent { get; set; } = ShowcaseBrandOverlayHelper.DefaultOpacityPercent;

        public string ShowcaseBrandLogoLabel { get; set; } = string.Empty;

        public string PipelineStatus { get; set; } = "Chờ";



        public string OutputVideoPath { get; set; } = string.Empty;

        /// <summary>Nhãn gộp trạng thái + output trên lưới — cập nhật qua <see cref="RefreshDisplayFields"/>.</summary>
        public string ShowcaseOutputGridLabel { get; private set; } = "Chờ";

        /// <summary>Thư mục phiên Showcase (Processed/Showcase/.../) — giữ lại sau Gemini/Excel để Render tìm đúng clips_render.</summary>
        public string ShowcaseSessionBaseDir { get; set; } = string.Empty;

        /// <summary>Thư mục clips_render của phiên — nơi đặt scene_01.mp4, clip quay tay, ...</summary>
        public string ShowcaseClipsDir { get; set; } = string.Empty;



        public List<AiVideoGenInputItem> Scenes { get; } = new List<AiVideoGenInputItem>();



        /// <summary>Hiển thị trên lưới — cập nhật qua <see cref="RefreshDisplayFields"/>.</summary>

        public string SceneCountDisplay { get; private set; } = "0 cảnh";

        /// <summary>Số ảnh trên storyboard — cột Ảnh vẽ nút riêng.</summary>
        public string ShowcaseImagesGridLabel { get; private set; } = "0 ảnh";

        /// <summary>Nhãn chủ đề trên lưới: kết quả Gemini nếu có, không thì preset đang chọn.</summary>
        public string ShowcaseThemeGridDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ShowcaseUserTheme))
                {
                    return ShowcaseUserTheme.Trim();
                }

                if (!string.IsNullOrWhiteSpace(ShowcaseTheme))
                {
                    return ShowcaseTheme.Trim();
                }

                return ShowcaseThemePresets.GetDisplayLabel(ShowcaseThemePrompt);
            }
        }

        /// <summary>Chủ đề gộp trên lưới — preset hoặc tự gõ trong một cột.</summary>
        public string ShowcaseThemeGridText
        {
            get => ShowcaseThemeGridDisplay;
            set => ShowcaseThemePresets.ApplyCombinedInput(this, value);
        }

        /// <summary>Nhãn cột Chủ đề (chỉ hiển thị — bấm mở bảng chọn).</summary>
        public string ShowcaseThemeGridLabel => ShowcaseThemeGridDisplay;



        public int SceneCount => Scenes?.Count ?? 0;



        public void RefreshDisplayFields()

        {

            ShowcaseVoiceoverHelper.SyncSilentFlagsFromVoiceover(Scenes);

            var count = SceneCount;

            if (count <= 0)

            {

                var clipsDir = ResolveClipsDirForDisplay();

                var folderClips = !string.IsNullOrWhiteSpace(clipsDir) && Directory.Exists(clipsDir)

                    ? ShowcaseSessionService.CountClipFilesOnDisk(clipsDir)

                    : 0;

                SceneCountDisplay = folderClips > 0

                    ? folderClips + " clip · " + ShowcaseRenderClipsPaths.FolderName

                    : "0 cảnh";

            }

            else

            {

                var clipsDir = ResolveClipsDirForDisplay();

                var clips = !string.IsNullOrWhiteSpace(clipsDir)

                    ? ShowcaseSessionService.CountValidClipsOnDisk(clipsDir, Scenes)

                    : Scenes.Count(s => !string.IsNullOrWhiteSpace(s?.ClipPath) && File.Exists(s.ClipPath));

                SceneCountDisplay = count + " cảnh · " + clips + "/" + count + " clip";

            }



            ShowcaseSubtitleStyleHelper.RefreshStyleLabel(this);

            ShowcaseMusicHelper.RefreshMusicLabel(this);

            ShowcaseBrandOverlayHelper.RefreshLabel(this);

            ShowcaseContentDisplayHelper.RefreshContentLabels(this);

            ShowcaseImagesGridLabel = count + " ảnh";

            ShowcaseGeminiSetupGridLabel = ShowcaseContentDisplayHelper.FormatProductTypeThemeGridLabel(this);

            ShowcaseClipModeGridLabel = ShowcaseContentDisplayHelper.FormatClipModeBrollGridLabel(this);

            ShowcaseOutputGridLabel = ShowcaseContentDisplayHelper.FormatOutputGridLabel(this);
        }

        private string ResolveClipsDirForDisplay()
        {
            var clipsDir = (ShowcaseClipsDir ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(clipsDir))
            {
                return clipsDir;
            }

            var baseDir = (ShowcaseSessionBaseDir ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(baseDir) ? string.Empty : ShowcaseRenderClipsPaths.Combine(baseDir);
        }




        public void ApplySettingsToScenes()

        {

            var textSize = ShowcaseSubtitleFontSize > 0 ? ShowcaseSubtitleFontSize : 72;

            ShowcaseTextSize = textSize;



            foreach (var scene in Scenes)

            {

                if (scene == null)

                {

                    continue;

                }



                scene.ProductName = ProductName ?? string.Empty;

                scene.ProfileName = ProfileName ?? string.Empty;

                scene.ShowcaseTheme = ShowcaseTheme ?? string.Empty;

                scene.ShowcaseMultiVoice = ShowcaseMultiVoice;

                scene.ShowcaseTextSize = textSize;

                scene.ShowcaseMusicVolume = ShowcaseMusicVolume >= 0 ? ShowcaseMusicVolume : 14;

                scene.ShowcaseTransitionSeconds = ShowcaseTransitionSeconds > 0 ? ShowcaseTransitionSeconds : 0.6;

            }

        }



        public void CopySettingsFromScene(AiVideoGenInputItem scene)

        {

            if (scene == null)

            {

                return;

            }



            if (string.IsNullOrWhiteSpace(ProductName))

            {

                ProductName = scene.ProductName ?? string.Empty;

            }



            if (string.IsNullOrWhiteSpace(ProfileName))

            {

                ProfileName = scene.ProfileName ?? string.Empty;

            }



            ShowcaseTheme = scene.ShowcaseTheme ?? string.Empty;

            ShowcaseMultiVoice = scene.ShowcaseMultiVoice;

            if (ShowcaseSubtitleFontSize <= 0 && scene.ShowcaseTextSize > 0)

            {

                ShowcaseSubtitleFontSize = scene.ShowcaseTextSize;

            }



            ShowcaseTextSize = ShowcaseSubtitleFontSize > 0 ? ShowcaseSubtitleFontSize : scene.ShowcaseTextSize > 0 ? scene.ShowcaseTextSize : 72;

            ShowcaseMusicVolume = scene.ShowcaseMusicVolume >= 0 ? scene.ShowcaseMusicVolume : 14;

            ShowcaseTransitionSeconds = scene.ShowcaseTransitionSeconds > 0 ? scene.ShowcaseTransitionSeconds : 0.6;

        }

    }

}


