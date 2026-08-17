using System.Collections.Generic;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Trạng thái phiên làm việc Showcase (ảnh → kịch bản → Excel → clip Veo → render).</summary>
    public sealed class ShowcaseSessionState
    {
        public string BaseDir { get; set; } = string.Empty;
        public string SourceImagesDir { get; set; } = string.Empty;
        public string ClipsDir { get; set; } = string.Empty;
        public string OutputDir { get; set; } = string.Empty;
        public string ExcelPath { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string HookText { get; set; } = string.Empty;
        public string CtaText { get; set; } = string.Empty;
    }

    /// <summary>Kết quả kịch bản Showcase do Gemini sinh — đã ánh xạ lại vào danh sách cảnh gốc.</summary>
    public sealed class ShowcaseScriptResult
    {
        public string Theme { get; set; } = string.Empty;
        public string HookText { get; set; } = string.Empty;
        public string CtaText { get; set; } = string.Empty;

        public List<AiVideoGenInputItem> OrderedScenes { get; set; } = new List<AiVideoGenInputItem>();

        /// <summary>File SFX gợi ý cho CTA (tên trong thư viện) — rỗng nếu không có.</summary>
        public string CtaSfxFile { get; set; } = string.Empty;

        public string CtaSfxGeminiHint { get; set; } = string.Empty;

        public string HookSfxFile { get; set; } = string.Empty;

        public string HookSfxGeminiHint { get; set; } = string.Empty;

        /// <summary>File nhạc nền Gemini gợi ý (tên trong Assets\Audio\Music).</summary>
        public string BackgroundMusicFile { get; set; } = string.Empty;

        public string BackgroundMusicGeminiHint { get; set; } = string.Empty;

        /// <summary>Gợi ý chuyển cảnh, phụ đề, giọng, tốc độ… từ cùng lần gọi «Tạo kịch bản».</summary>
        public ShowcaseGeminiProductionHintsHelper.Hints ProductionHints { get; set; }
            = new ShowcaseGeminiProductionHintsHelper.Hints();
    }

    /// <summary>Kết quả thoại Showcase do Gemini xem clip phân cảnh sinh ra.</summary>
    public sealed class ShowcaseVoiceoverResult
    {
        public string Theme { get; set; } = string.Empty;
        public string HookText { get; set; } = string.Empty;
        public string CtaText { get; set; } = string.Empty;
        public List<AiVideoGenInputItem> Scenes { get; set; } = new List<AiVideoGenInputItem>();
    }

    /// <summary>Kết quả build narration.mp3 (preview hoặc render).</summary>
    public sealed class ShowcaseNarrationBuildResult
    {
        public string NarrationFilePath { get; set; } = string.Empty;

        public string AudioDirectory { get; set; } = string.Empty;

        public bool ReusedFromCache { get; set; }

        public double DurationSeconds { get; set; }

        /// <summary>Hook B-Roll đã chọn khi build audio (render dùng cùng file nếu gọi ngay sau).</summary>
        public string HookBrollPath { get; set; } = string.Empty;
    }

    /// <summary>DTO thô parse trực tiếp từ JSON Gemini trả về.</summary>
    internal sealed class ShowcaseScriptDto
    {
        public string theme { get; set; }
        public string hook_text { get; set; }
        public string cta_text { get; set; }
        public string cta_broll_id { get; set; }
        public string cta_sfx_id { get; set; }
        public string cta_sfx_hint { get; set; }
        public string hook_sfx_id { get; set; }
        public string hook_sfx_hint { get; set; }
        public string background_music_id { get; set; }
        public string background_music_hint { get; set; }
        public double transition_seconds { get; set; }
        public string transition_hint { get; set; }
        public bool subtitle_enabled { get; set; }
        public bool hook_subtitle_enabled { get; set; }
        public string body_subtitle_look_id { get; set; }
        public string hook_subtitle_look_id { get; set; }
        public string hook_subtitle_animation_id { get; set; }
        public string body_subtitle_animation_id { get; set; }
        public string subtitle_position { get; set; }
        public string hook_style_key { get; set; }
        public string body_style_key { get; set; }
        public string voice_preset_id { get; set; }
        public string body_voice_preset_id { get; set; }
        public string voice_age_id { get; set; }
        public string voice_tone_id { get; set; }
        public string tts_engine { get; set; }
        public int hook_narration_speed_percent { get; set; }
        public int body_narration_speed_percent { get; set; }
        public int music_volume_percent { get; set; } = -1;
        public List<ShowcaseSceneDto> scenes { get; set; }
    }

    internal sealed class ShowcaseSceneDto
    {
        public int order { get; set; }

        /// <summary>1-based — clip nào trong danh sách INPUT gửi kèm (folder clips_render).</summary>
        public int clip_index { get; set; }

        public string role { get; set; }
        public string scene_title { get; set; }
        public string voiceover { get; set; }
        public bool silent { get; set; }
        public string image_kind { get; set; }
        public string clip_tool { get; set; }
        public string veo_prompt { get; set; }
        public string kling_prompt { get; set; }
        public string zoom_hint { get; set; }
        public string zoom_style { get; set; }
        public string zoom_speed { get; set; }
        public double clip_duration_seconds { get; set; }
        public int image_index { get; set; }
        public string sfx_id { get; set; }
        public string sfx_hint { get; set; }
        public string sfx_placement { get; set; }
    }
}
