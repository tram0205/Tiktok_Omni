using System;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chuẩn bị text ElevenLabs Showcase — cùng voice_id, khác audio tag theo HookStyleCatalog (v3).</summary>
    public static class ShowcaseElevenLabsTextHelper
    {
        public enum SegmentKind
        {
            Hook,
            Body,
            Cta
        }

        public static string PrepareSegmentText(
            string text,
            SegmentKind kind,
            AppSettings settings,
            ShowcaseTtsRenderOptions showcaseTts,
            Action<string> log)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            if (!TtsAvailabilityHelper.IsElevenLabsConfigured(settings))
            {
                return ApplyPauseOnly(line, kind);
            }

            showcaseTts = showcaseTts ?? new ShowcaseTtsRenderOptions();
            var model = ElevenLabsTtsHelper.ResolveModelId(settings);
            var isV3 = string.Equals(
                model,
                ElevenLabsTtsHelper.DefaultVietnameseModel,
                StringComparison.OrdinalIgnoreCase);

            switch (kind)
            {
                case SegmentKind.Hook:
                case SegmentKind.Cta:
                    return PrepareHookOrCta(line, showcaseTts.HookStyleKey, isV3, log);
                default:
                    return PrepareBody(line, showcaseTts.BodyStyleKey, showcaseTts.BodyVoiceToneId, isV3, log);
            }
        }

        private static string PrepareHookOrCta(string line, string hookStyleKey, bool isV3, Action<string> log)
        {
            if (isV3 && ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(hookStyleKey))
            {
                line = ElevenLabsTtsHelper.ApplyHookDeliveryPauses(line);
                log?.Invoke("[TTS] ElevenLabs hook · ⚙ Tùy chỉnh giọng (không audio tag, dùng voice_settings tay).");
                return line;
            }

            var key = ResolveStyleKeyForEleven(hookStyleKey);
            if (isV3)
            {
                line = ReupElevenLabsVoiceHelper.ApplyHookStyleAudioTag(line, key);
                line = ElevenLabsTtsHelper.ApplyHookDeliveryPauses(line);
                log?.Invoke("[TTS] ElevenLabs hook · «" + HookStyleCatalog.GetDisplayName(key) + "» · " +
                            ReupElevenLabsVoiceHelper.ResolveStyleAudioTag(key) + ".");
            }
            else
            {
                line = ElevenLabsTtsHelper.ApplyHookDeliveryPauses(line);
            }

            return line;
        }

        /// <summary>
        /// Thân: Tone giọng (nếu khác «Tự nhiên»/«Tùy chỉnh») quyết định audio tag — thay cho tag theo Phong cách Edge.
        /// «Tự nhiên» giữ hành vi cũ (tag theo bodyStyleKey). «Tùy chỉnh» không chèn tag (chỉ dùng voice_settings tay).
        /// </summary>
        private static string PrepareBody(string line, string bodyStyleKey, string bodyToneId, bool isV3, Action<string> log)
        {
            if (!isV3)
            {
                if (!line.StartsWith("[", StringComparison.Ordinal))
                {
                    line = "[calm] " + line;
                }

                return ElevenLabsTtsHelper.ApplyDeepPauses(line);
            }

            var toneTag = ShowcaseElevenToneHelper.ResolveAudioTag(bodyToneId);
            if (!string.IsNullOrEmpty(toneTag))
            {
                line = InsertAudioTagIfAbsent(line, toneTag);
                line = ElevenLabsTtsHelper.ApplyDeepPauses(line);
                log?.Invoke("[TTS] ElevenLabs thân · Tone «" + ShowcaseElevenToneHelper.GetToneLabel(bodyToneId) + "» · " + toneTag + ".");
                return line;
            }

            if (ShowcaseElevenToneHelper.IsCustomTone(bodyToneId))
            {
                line = ElevenLabsTtsHelper.ApplyDeepPauses(line);
                log?.Invoke("[TTS] ElevenLabs thân · Tùy chỉnh (không audio tag, dùng voice_settings tay).");
                return line;
            }

            var key = ResolveStyleKeyForEleven(bodyStyleKey);
            line = ReupElevenLabsVoiceHelper.ApplyHookStyleAudioTag(line, key);
            line = ElevenLabsTtsHelper.ApplyDeepPauses(line);
            log?.Invoke("[TTS] ElevenLabs thân · «" + HookStyleCatalog.GetDisplayName(key) + "» · " +
                        ReupElevenLabsVoiceHelper.ResolveStyleAudioTag(key) + ".");
            return line;
        }

        private static string InsertAudioTagIfAbsent(string text, string tag)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(tag) || line.StartsWith("[", StringComparison.Ordinal))
            {
                return line;
            }

            return tag + " " + line;
        }

        private static string ResolveStyleKeyForEleven(string styleKey)
        {
            var normalized = ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(styleKey);
            if (ShowcaseEdgeProsodyHelper.IsCustomStyle(styleKey) || ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(styleKey))
            {
                return HookStyleCatalog.StyleHuongdan;
            }

            return normalized;
        }

        private static string ApplyPauseOnly(string line, SegmentKind kind)
        {
            switch (kind)
            {
                case SegmentKind.Hook:
                case SegmentKind.Cta:
                    return ElevenLabsTtsHelper.ApplyHookDeliveryPauses(line);
                default:
                    return line;
            }
        }
    }
}
