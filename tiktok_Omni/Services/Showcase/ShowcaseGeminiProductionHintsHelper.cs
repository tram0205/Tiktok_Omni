using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Parse + áp dụng gợi ý sản xuất Showcase (chuyển cảnh, phụ đề, giọng, tốc độ…) từ JSON «Tạo kịch bản».</summary>
    public static class ShowcaseGeminiProductionHintsHelper
    {
        public sealed class Hints
        {
            public double TransitionSeconds { get; set; }
            public string TransitionHint { get; set; } = string.Empty;

            public bool SubtitleEnabled { get; set; }
            public bool HookSubtitleEnabled { get; set; }
            public string BodySubtitleLookStorage { get; set; } = string.Empty;
            public string HookSubtitleLookStorage { get; set; } = string.Empty;
            public string HookSubtitleAnimation { get; set; } = string.Empty;
            public string BodySubtitleAnimation { get; set; } = string.Empty;
            public string SubtitlePosition { get; set; } = string.Empty;

            public string HookStyleKey { get; set; } = string.Empty;
            public string BodyStyleKey { get; set; } = string.Empty;

            public string VoicePresetId { get; set; } = string.Empty;
            public string BodyVoicePresetId { get; set; } = string.Empty;
            public string VoiceAgeId { get; set; } = string.Empty;
            public string VoiceToneId { get; set; } = string.Empty;
            public string TtsEngine { get; set; } = string.Empty;

            public int HookNarrationSpeedPercent { get; set; }
            public int BodyNarrationSpeedPercent { get; set; }
            public int MusicVolumePercent { get; set; } = -1;
        }

        public static string BuildPromptSection(AppSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("9) CHUYỂN CẢNH: 'transition_seconds' 0.2–2.0 (crossfade giữa clip); 'transition_hint' 3-8 từ tiếng Việt (Nhanh/Vừa/Chậm + mood).");
            sb.AppendLine("10) PHỤ ĐỀ burn-in (tiếng Việt):");
            sb.AppendLine("   - 'subtitle_enabled' + 'hook_subtitle_enabled' (true/false) — bật phụ đề thân / hook.");
            sb.AppendLine("   - 'body_subtitle_look_id' CHỈ từ: " + BuildSubtitleLookList(ShowcaseDisplayLineEffectKind.Body) + ".");
            sb.AppendLine("   - 'hook_subtitle_look_id' CHỈ từ: " + BuildSubtitleLookList(ShowcaseDisplayLineEffectKind.Hook) + ".");
            sb.AppendLine("   - 'hook_subtitle_animation_id' CHỈ từ: " + BuildAnimationList() + ".");
            sb.AppendLine("   - 'body_subtitle_animation_id' CHỈ từ: " + BuildAnimationList() + " hoặc \"Plain\" (cả dòng tĩnh).");
            sb.AppendLine("   - 'subtitle_position' = bottom | middle | top.");
            sb.AppendLine("11) PHONG CÁCH HOOK TTS (Edge/Eleven):");
            sb.AppendLine("   - 'hook_style_key' CHỈ từ: " + BuildHookStyleList() + " — khớp mood hook_text.");
            sb.AppendLine("   - 'body_style_key' (tùy chọn) CHỈ từ cùng danh sách — mặc định ke_chuyen nếu bỏ trống.");
            sb.AppendLine("12) GIỌNG & TỐC ĐỘ THOẠI:");
            sb.AppendLine("   - 'voice_preset_id' CHỈ từ: " + BuildVoicePresetList() + ".");
            sb.AppendLine("   - 'body_voice_preset_id' (tùy chọn) — rỗng = cùng hook.");
            sb.AppendLine("   - 'voice_age_id' (tùy chọn) CHỈ từ: " + BuildAgeList() + " — bổ sung nếu preset chưa đủ.");
            sb.AppendLine("   - 'voice_tone_id' (tùy chọn) CHỈ từ: " + BuildToneList() + " — ElevenLabs tone.");
            sb.AppendLine("   - 'tts_engine' = edge_tts | eleven_labs (mặc định edge_tts).");
            sb.AppendLine("   - 'hook_narration_speed_percent' + 'body_narration_speed_percent' 50–200 (100 = bình thường).");
            sb.AppendLine("   - 'music_volume_percent' 0–100 (mặc định 14 — nhạc nền không lấn thoại).");
            return sb.ToString().TrimEnd();
        }

        public static string BuildJsonSchemaSuffix()
        {
            return ",\"transition_seconds\":0.6,\"transition_hint\":\"\",\"subtitle_enabled\":true,\"hook_subtitle_enabled\":true,"
                   + "\"body_subtitle_look_id\":\"TikTokWhite\",\"hook_subtitle_look_id\":\"HookGoldBold\","
                   + "\"hook_subtitle_animation_id\":\"PopStrong\",\"body_subtitle_animation_id\":\"Pop\","
                   + "\"subtitle_position\":\"bottom\",\"hook_style_key\":\"huong_dan\",\"body_style_key\":\"ke_chuyen\","
                   + "\"voice_preset_id\":\"female_south_young\",\"body_voice_preset_id\":\"\","
                   + "\"voice_age_id\":\"adult_26_35\",\"voice_tone_id\":\"natural\",\"tts_engine\":\"edge_tts\","
                   + "\"hook_narration_speed_percent\":100,\"body_narration_speed_percent\":100,\"music_volume_percent\":14";
        }

        internal static Hints ResolveFromDto(ShowcaseScriptDto dto)
        {
            var hints = new Hints();
            if (dto == null)
            {
                return hints;
            }

            if (dto.transition_seconds > 0)
            {
                hints.TransitionSeconds = ShowcaseTransitionHelper.ClampSeconds(dto.transition_seconds);
            }

            hints.TransitionHint = (dto.transition_hint ?? string.Empty).Trim();

            var hasBodyLook = !string.IsNullOrWhiteSpace(dto.body_subtitle_look_id);
            var hasHookLook = !string.IsNullOrWhiteSpace(dto.hook_subtitle_look_id);
            hints.SubtitleEnabled = dto.subtitle_enabled || hasBodyLook;
            hints.HookSubtitleEnabled = dto.hook_subtitle_enabled || hasHookLook || hasBodyLook;

            hints.BodySubtitleLookStorage = ResolveSubtitleLookStorage(
                dto.body_subtitle_look_id,
                ShowcaseDisplayLineEffectKind.Body);
            hints.HookSubtitleLookStorage = ResolveSubtitleLookStorage(
                dto.hook_subtitle_look_id,
                ShowcaseDisplayLineEffectKind.Hook);
            hints.HookSubtitleAnimation = ResolveAnimationStorage(dto.hook_subtitle_animation_id, forHook: true);
            hints.BodySubtitleAnimation = ResolveBodyAnimationStorage(dto.body_subtitle_animation_id);
            hints.SubtitlePosition = ResolveSubtitlePosition(dto.subtitle_position);

            hints.HookStyleKey = ResolveHookStyleKey(dto.hook_style_key);
            hints.BodyStyleKey = ResolveHookStyleKey(dto.body_style_key);
            if (string.IsNullOrWhiteSpace(hints.BodyStyleKey) && !string.IsNullOrWhiteSpace(hints.HookStyleKey))
            {
                hints.BodyStyleKey = HookStyleCatalog.StyleKechuyen;
            }

            hints.VoicePresetId = ResolveVoicePresetId(dto.voice_preset_id);
            hints.BodyVoicePresetId = ResolveVoicePresetId(dto.body_voice_preset_id);
            hints.VoiceAgeId = ResolveAgeId(dto.voice_age_id);
            hints.VoiceToneId = ResolveToneId(dto.voice_tone_id);
            hints.TtsEngine = ResolveTtsEngine(dto.tts_engine);

            if (dto.hook_narration_speed_percent > 0)
            {
                hints.HookNarrationSpeedPercent = ShowcaseNarrationSpeedHelper.ClampManualPercent(dto.hook_narration_speed_percent);
            }

            if (dto.body_narration_speed_percent > 0)
            {
                hints.BodyNarrationSpeedPercent = ShowcaseNarrationSpeedHelper.ClampManualPercent(dto.body_narration_speed_percent);
            }

            if (dto.music_volume_percent >= 0)
            {
                hints.MusicVolumePercent = Math.Max(0, Math.Min(100, dto.music_volume_percent));
            }

            return hints;
        }

        public static void ApplyToVideo(ShowcaseVideoItem video, Hints hints, AppSettings settings, Action<string> log = null)
        {
            if (video == null || hints == null)
            {
                return;
            }

            if (hints.TransitionSeconds > 0)
            {
                video.ShowcaseTransitionSeconds = hints.TransitionSeconds;
                ShowcaseTransitionHelper.RefreshTransitionLabel(video);
                LogLine(log, "Chuyển cảnh (Gemini): " + video.ShowcaseTransitionLabel
                              + (string.IsNullOrWhiteSpace(hints.TransitionHint) ? string.Empty : " — " + hints.TransitionHint));
            }

            ApplySubtitleHints(video, hints, log);
            ApplyVoiceHints(video, hints, log);

            if (hints.MusicVolumePercent >= 0)
            {
                video.ShowcaseMusicVolume = hints.MusicVolumePercent;
                LogLine(log, "Âm lượng nhạc nền (Gemini): " + hints.MusicVolumePercent + "%");
            }

            if (hints.SubtitleEnabled || hints.HookSubtitleEnabled
                || !string.IsNullOrWhiteSpace(hints.BodySubtitleLookStorage)
                || !string.IsNullOrWhiteSpace(hints.HookSubtitleLookStorage))
            {
                ShowcaseSubtitleStyleHelper.EnsureVideoDefaults(video, settings);
            }

            ShowcaseSubtitleStyleHelper.RefreshStyleLabel(video);
            ShowcaseNarrationSpeedHelper.SyncLegacyCombinedSpeedField(video);
        }

        public static string FormatSummaryLine(Hints hints)
        {
            if (hints == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (hints.TransitionSeconds > 0)
            {
                parts.Add("chuyển " + hints.TransitionSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s");
            }

            if (hints.SubtitleEnabled || hints.HookSubtitleEnabled)
            {
                parts.Add("phụ đề");
            }

            if (!string.IsNullOrWhiteSpace(hints.HookStyleKey))
            {
                parts.Add("hook «" + HookStyleCatalog.GetDisplayName(hints.HookStyleKey) + "»");
            }

            if (!string.IsNullOrWhiteSpace(hints.VoicePresetId))
            {
                parts.Add("giọng " + ShowcaseVoicePresetCatalog.GetById(hints.VoicePresetId).Label);
            }

            if (hints.HookNarrationSpeedPercent > 0 || hints.BodyNarrationSpeedPercent > 0)
            {
                var hook = hints.HookNarrationSpeedPercent > 0
                    ? hints.HookNarrationSpeedPercent
                    : ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;
                var body = hints.BodyNarrationSpeedPercent > 0
                    ? hints.BodyNarrationSpeedPercent
                    : ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;
                parts.Add("tốc độ hook " + hook + "% · thân " + body + "%");
            }

            return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
        }

        private static void ApplySubtitleHints(ShowcaseVideoItem video, Hints hints, Action<string> log)
        {
            var appliedAny = false;

            if (hints.SubtitleEnabled)
            {
                video.ShowcaseSubtitleEnabled = true;
                appliedAny = true;
            }

            if (hints.HookSubtitleEnabled)
            {
                video.ShowcaseHookSubtitleEnabled = true;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.BodySubtitleLookStorage))
            {
                ShowcaseSubtitleLookPresetCatalog.SyncBodyVideoFields(video, hints.BodySubtitleLookStorage);
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.HookSubtitleLookStorage))
            {
                ShowcaseSubtitleLookPresetCatalog.SyncHookVideoFields(video, hints.HookSubtitleLookStorage);
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.HookSubtitleAnimation))
            {
                video.ShowcaseHookSubtitleAnimation = hints.HookSubtitleAnimation;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.BodySubtitleAnimation))
            {
                video.ShowcaseSubtitleAnimation = hints.BodySubtitleAnimation;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.SubtitlePosition))
            {
                video.ShowcaseSubtitlePosition = hints.SubtitlePosition;
                appliedAny = true;
            }

            if (appliedAny)
            {
                var fx = ShowcaseHookAnimationCatalog.DisplayLabel(video.ShowcaseHookSubtitleAnimation);
                var bodyLook = ShowcaseSubtitleLookPresetCatalog.LabelFromStorage(
                    video.ShowcaseSubtitleLookPreset,
                    ShowcaseDisplayLineEffectKind.Body);
                LogLine(log, "Phụ đề (Gemini): " + (video.ShowcaseHookSubtitleEnabled ? "hook" : "—")
                          + " + " + (video.ShowcaseSubtitleEnabled ? "thân" : "—")
                          + " · " + bodyLook + " · hook «" + fx + "»");
            }
        }

        private static void ApplyVoiceHints(ShowcaseVideoItem video, Hints hints, Action<string> log)
        {
            var appliedAny = false;

            if (!string.IsNullOrWhiteSpace(hints.TtsEngine))
            {
                video.ShowcaseTtsEngine = hints.TtsEngine;
                video.ShowcaseHookTtsEngine = hints.TtsEngine;
                video.ShowcaseBodyTtsEngine = hints.TtsEngine;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.VoicePresetId))
            {
                video.ShowcaseVoicePresetId = hints.VoicePresetId;
                var dims = ShowcaseVoicePresetDimensions.GetForPreset(hints.VoicePresetId);
                if (string.IsNullOrWhiteSpace(hints.VoiceAgeId))
                {
                    video.ShowcaseVoiceAgeId = dims.AgeId;
                }

                video.ShowcaseVoiceLanguageId = dims.LanguageId;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.BodyVoicePresetId))
            {
                video.ShowcaseBodyVoicePresetId = hints.BodyVoicePresetId;
                var bodyDims = ShowcaseVoicePresetDimensions.GetForPreset(hints.BodyVoicePresetId);
                video.ShowcaseBodyVoiceAgeId = bodyDims.AgeId;
                video.ShowcaseBodyVoiceLanguageId = bodyDims.LanguageId;
            }
            else if (!string.IsNullOrWhiteSpace(hints.VoicePresetId))
            {
                video.ShowcaseBodyVoicePresetId = hints.VoicePresetId;
                video.ShowcaseBodyVoiceAgeId = video.ShowcaseVoiceAgeId;
                video.ShowcaseBodyVoiceLanguageId = video.ShowcaseVoiceLanguageId;
            }

            if (!string.IsNullOrWhiteSpace(hints.VoiceAgeId))
            {
                video.ShowcaseVoiceAgeId = hints.VoiceAgeId;
                if (string.IsNullOrWhiteSpace(hints.BodyVoicePresetId))
                {
                    video.ShowcaseBodyVoiceAgeId = hints.VoiceAgeId;
                }

                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.VoiceToneId))
            {
                video.ShowcaseVoiceToneId = hints.VoiceToneId;
                video.ShowcaseBodyVoiceToneId = hints.VoiceToneId;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.HookStyleKey))
            {
                video.ShowcaseHookStyleKey = hints.HookStyleKey;
                appliedAny = true;
            }

            if (!string.IsNullOrWhiteSpace(hints.BodyStyleKey))
            {
                video.ShowcaseBodyStyleKey = hints.BodyStyleKey;
                appliedAny = true;
            }

            if (hints.HookNarrationSpeedPercent > 0)
            {
                video.ShowcaseHookNarrationSpeedPercent = hints.HookNarrationSpeedPercent;
                appliedAny = true;
            }

            if (hints.BodyNarrationSpeedPercent > 0)
            {
                video.ShowcaseBodyNarrationSpeedPercent = hints.BodyNarrationSpeedPercent;
                appliedAny = true;
            }

            if (appliedAny)
            {
                ShowcaseTtsHelper.EnsureVideoDefaults(video, null);
                var engine = string.Equals(video.ShowcaseTtsEngine, ShowcaseTtsHelper.EngineElevenLabs, StringComparison.OrdinalIgnoreCase)
                    ? "ElevenLabs"
                    : "Edge TTS";
                LogLine(log, "Giọng (Gemini): " + engine + " · "
                          + ShowcaseVoicePresetCatalog.FormatShortLabel(
                              video.ShowcaseVoicePresetId,
                              video.ShowcaseVoiceAgeId,
                              video.ShowcaseVoiceLanguageId)
                          + " · hook «" + HookStyleCatalog.GetDisplayName(video.ShowcaseHookStyleKey) + "»");
            }
        }

        private static string BuildSubtitleLookList(ShowcaseDisplayLineEffectKind kind)
        {
            return string.Join(", ", ShowcaseSubtitleLookPresetCatalog.AllForKind(kind).Select(p => p.Storage));
        }

        private static string BuildAnimationList()
        {
            return string.Join(", ", ShowcaseHookAnimationCatalog.All.Select(e => e.Storage));
        }

        private static string BuildHookStyleList()
        {
            return string.Join(", ", HookStyleCatalog.AllStyleKeys);
        }

        private static string BuildVoicePresetList()
        {
            return string.Join(", ", ShowcaseVoicePresetCatalog.ListAll().Select(p => p.Id));
        }

        private static string BuildAgeList()
        {
            return string.Join(", ", ShowcaseVoicePresetDimensions.ListAgeOptions().Select(o => o.Id));
        }

        private static string BuildToneList()
        {
            return string.Join(", ", ShowcaseVoicePresetDimensions.ListToneOptions()
                .Where(o => !string.Equals(o.Id, ShowcaseVoicePresetDimensions.Tone.Custom, StringComparison.OrdinalIgnoreCase))
                .Select(o => o.Id));
        }

        private static string ResolveSubtitleLookStorage(string raw, ShowcaseDisplayLineEffectKind kind)
        {
            var key = (raw ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return string.Empty;
            }

            if (ShowcaseSubtitleLookPresetCatalog.FindByStorage(key, kind) != null)
            {
                return key;
            }

            return ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(key, kind);
        }

        private static string ResolveAnimationStorage(string raw, bool forHook)
        {
            var key = (raw ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return string.Empty;
            }

            if (string.Equals(key, "Plain", StringComparison.OrdinalIgnoreCase))
            {
                return forHook ? string.Empty : "Plain";
            }

            if (ShowcaseHookAnimationCatalog.All.Any(e =>
                    string.Equals(e.Storage, key, StringComparison.OrdinalIgnoreCase)))
            {
                return ShowcaseHookAnimationCatalog.All.First(e =>
                    string.Equals(e.Storage, key, StringComparison.OrdinalIgnoreCase)).Storage;
            }

            return forHook ? ShowcaseHookAnimationCatalog.DefaultStorage : "Pop";
        }

        private static string ResolveBodyAnimationStorage(string raw)
        {
            return ResolveAnimationStorage(raw, forHook: false);
        }

        private static string ResolveSubtitlePosition(string raw)
        {
            var v = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Length == 0)
            {
                return string.Empty;
            }

            if (v is "top" or "tren" or "trên")
            {
                return "Top";
            }

            if (v is "middle" or "center" or "giua" or "giữa")
            {
                return "Middle";
            }

            return "Bottom";
        }

        private static string ResolveHookStyleKey(string raw)
        {
            var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                return string.Empty;
            }

            return HookStyleCatalog.AllStyleKeys.FirstOrDefault(k =>
                       string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                   ?? string.Empty;
        }

        private static string ResolveVoicePresetId(string raw)
        {
            var id = (raw ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                return string.Empty;
            }

            var match = ShowcaseVoicePresetCatalog.ListAll().FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? string.Empty;
        }

        private static string ResolveAgeId(string raw)
        {
            var id = (raw ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                return string.Empty;
            }

            return ShowcaseVoicePresetDimensions.NormalizeAgeId(id);
        }

        private static string ResolveToneId(string raw)
        {
            var id = (raw ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                return string.Empty;
            }

            return ShowcaseElevenToneHelper.NormalizeToneId(id);
        }

        private static string ResolveTtsEngine(string raw)
        {
            var v = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Length == 0)
            {
                return string.Empty;
            }

            if (v is "eleven" or "elevenlabs" or "eleven_labs" or "11labs")
            {
                return ShowcaseTtsHelper.EngineElevenLabs;
            }

            if (v is "edge" or "edge_tts" or "edgetts" or "microsoft")
            {
                return ShowcaseTtsHelper.EngineEdgeTts;
            }

            return string.Empty;
        }

        private static void LogLine(Action<string> log, string message)
        {
            if (log == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            log(message);
        }
    }
}
