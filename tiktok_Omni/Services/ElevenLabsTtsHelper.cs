using System;
using System.Text.RegularExpressions;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Model + payload ElevenLabs cho tiếng Việt (language_code vi = Language Override trên Web).</summary>
    public static class ElevenLabsTtsHelper
    {
        /// <summary>Eleven v3 — ưu tiên trên Web, dấu tiếng Việt và ngữ điệu tự nhiên hơn.</summary>
        public const string DefaultVietnameseModel = "eleven_v3";

        public const string AlternateVietnameseModel = "eleven_turbo_v2_5";

        public const string LegacyMultilingualModel = "eleven_multilingual_v2";

        public const string LegacyFlashModel = "eleven_flash_v2_5";

        public const string LegacyTurboModel = "eleven_turbo_v2_5";

        public const string DefaultLanguageCode = "vi";

        /// <summary>URL POST TTS mặc định — phần sau path là voice_id.</summary>
        public const string DefaultTextToSpeechEndpointPrefix = "https://api.elevenlabs.io/v1/text-to-speech/";

        private static readonly Regex VoiceIdPattern = new Regex(
            @"^[A-Za-z0-9_-]{10,64}$",
            RegexOptions.CultureInvariant);

        public const double DefaultStability = 0.5;

        public const double DefaultSimilarityBoost = 0.75;

        /// <summary>Hook TikTok — biểu cảm hơn (stability thấp, style cao).</summary>
        public const double HookStability = 0.38;

        public const double HookSimilarityBoost = 0.78;

        public const double HookStyleExaggeration = 0.32;

        /// <summary>Thuyết minh Video reup — ổn định hơn (formal).</summary>
        public const double NarrationStability = 0.58;

        public const double NarrationSimilarityBoost = 0.72;

        public const double NarrationStyleExaggeration = 0.08;

        /// <summary>Showcase thân+CTA — giọng Nam trôi, ổn hơn hook một chút.</summary>
        public const double ShowcaseBodyStability = 0.48;

        public const double ShowcaseBodySimilarityBoost = 0.78;

        public const double ShowcaseBodyStyleExaggeration = 0.26;

        /// <summary>Showcase CTA — nhấn vừa phải, giữa thân và hook.</summary>
        public const double ShowcaseCtaStability = 0.43;

        public const double ShowcaseCtaSimilarityBoost = 0.77;

        public const double ShowcaseCtaStyleExaggeration = 0.29;

        public enum VoiceDeliveryMode
        {
            /// <summary>Video reup thuyết minh — stability cao, style thấp.</summary>
            Narration = 0,

            /// <summary>Hook — nhấn mạnh, style cao.</summary>
            Hook = 1,

            /// <summary>Showcase cảnh thân — giữ accent clone, kể tự nhiên hơn hook.</summary>
            ShowcaseBody = 2,

            /// <summary>Showcase CTA — nhấn vừa phải, nhanh hơn thân nhưng không bằng hook.</summary>
            ShowcaseCta = 3
        }

        public static string ResolveModelId(AppSettings settings)
        {
            var model = (settings?.TtsElevenLabsModel ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(model))
            {
                return DefaultVietnameseModel;
            }

            if (string.Equals(model, LegacyMultilingualModel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, LegacyFlashModel, StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, LegacyTurboModel, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultVietnameseModel;
            }

            return model;
        }

        public static string ResolveLanguageCode(AppSettings settings)
        {
            var lang = (settings?.TtsLanguageCode ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(lang) ? DefaultLanguageCode : lang;
        }

        public static bool EndpointIncludesVoiceId(string endpoint)
        {
            var url = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            return Regex.IsMatch(url, @"/text-to-speech/[^/\s?""']+", RegexOptions.IgnoreCase);
        }

        /// <summary>Trích voice_id từ URL …/text-to-speech/{voice_id}.</summary>
        public static string ExtractVoiceIdFromEndpoint(string endpoint)
        {
            var url = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            var match = Regex.Match(url, @"/text-to-speech/([^/\s?""']+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        /// <summary>Thay voice_id trong URL ElevenLabs hoặc nối vào path text-to-speech.</summary>
        public static string ResolveEndpointWithVoiceId(string endpoint, string voiceId)
        {
            var url = (endpoint ?? string.Empty).Trim();
            var vid = (voiceId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(vid))
            {
                return url;
            }

            if (Regex.IsMatch(url, @"/text-to-speech/[^/\s?""']+", RegexOptions.IgnoreCase))
            {
                // ${1} — tránh voice_id bắt đầu bằng số bị hiểu nhầm thành $19… (group 19).
                return Regex.Replace(
                    url,
                    @"(/text-to-speech/)[^/\s?""']+",
                    "${1}" + vid,
                    RegexOptions.IgnoreCase);
            }

            if (url.EndsWith("/", StringComparison.Ordinal))
            {
                url = url.TrimEnd('/');
            }

            if (url.IndexOf("/text-to-speech", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url + "/" + vid;
            }

            if (url.IndexOf("elevenlabs.io", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url + "/text-to-speech/" + vid;
            }

            return url;
        }

        /// <summary>voice_settings mặc định Web (stability / similarity).</summary>
        public static object CreateDefaultVoiceSettings()
        {
            return new
            {
                stability = DefaultStability,
                similarity_boost = DefaultSimilarityBoost
            };
        }

        /// <summary>Giọng hook Video reup — nhấn mạnh, nhiều cảm xúc (eleven_v3 + style).</summary>
        public static object CreateVietnameseHookVoiceSettings()
        {
            return new
            {
                stability = HookStability,
                similarity_boost = HookSimilarityBoost,
                style = HookStyleExaggeration
            };
        }

        /// <summary>Giọng thuyết minh Video reup — kể chuyện ổn định.</summary>
        public static object CreateVietnameseNarrationVoiceSettings()
        {
            return new
            {
                stability = NarrationStability,
                similarity_boost = NarrationSimilarityBoost,
                style = NarrationStyleExaggeration
            };
        }

        /// <summary>Showcase: thoại từng cảnh — cùng voice_id, giữ accent clone, kể tự nhiên (không kéo về giọng Bắc).</summary>
        public static object CreateShowcaseBodyVoiceSettings()
        {
            return new
            {
                stability = ShowcaseBodyStability,
                similarity_boost = ShowcaseBodySimilarityBoost,
                style = ShowcaseBodyStyleExaggeration
            };
        }

        public static object CreateShowcaseCtaVoiceSettings()
        {
            return new
            {
                stability = ShowcaseCtaStability,
                similarity_boost = ShowcaseCtaSimilarityBoost,
                style = ShowcaseCtaStyleExaggeration
            };
        }

        private static object ResolveVoiceSettings(
            bool emphaticHook,
            bool showcaseExpressiveBody,
            ShowcaseTtsRenderOptions segmentTts = null,
            bool emphaticCta = false)
        {
            object baseSettings = emphaticHook
                ? CreateVietnameseHookVoiceSettings()
                : emphaticCta
                    ? CreateShowcaseCtaVoiceSettings()
                    : showcaseExpressiveBody
                        ? CreateShowcaseBodyVoiceSettings()
                        : CreateVietnameseNarrationVoiceSettings();

            if (segmentTts == null)
            {
                return baseSettings;
            }

            if (emphaticHook)
            {
                // Hook: "Tone giọng" đã bị bỏ — trigger là "Phong cách hook" = ⚙ Tùy chỉnh giọng.
                if (!ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(segmentTts.HookStyleKey))
                {
                    return baseSettings;
                }

                return BuildVoiceSettingsObject(
                    segmentTts.ElevenCustomStabilityPercent,
                    segmentTts.ElevenCustomSimilarityPercent,
                    segmentTts.ElevenCustomStylePercent);
            }

            // Thân/Narration: giữ nguyên cơ chế Tone giọng.
            var toneId = segmentTts.ElevenToneId;
            if (!ShowcaseElevenToneHelper.HasVoiceSettingsOverride(toneId))
            {
                return baseSettings;
            }

            ShowcaseElevenToneHelper.ResolveEffectiveVoiceSettings(
                toneId,
                segmentTts.ElevenCustomStabilityPercent,
                segmentTts.ElevenCustomSimilarityPercent,
                segmentTts.ElevenCustomStylePercent,
                out var stabilityPercent,
                out var similarityPercent,
                out var stylePercent);

            return BuildVoiceSettingsObject(stabilityPercent, similarityPercent, stylePercent);
        }

        private static object BuildVoiceSettingsObject(int stabilityPercent, int similarityPercent, int stylePercent) => new
        {
            stability = stabilityPercent / 100.0,
            similarity_boost = similarityPercent / 100.0,
            style = stylePercent / 100.0
        };

        /// <summary>JSON body POST /text-to-speech/{voice_id} — luôn có language_code vi.</summary>
        public static object BuildPayload(
            string text,
            AppSettings settings,
            bool emphaticHook,
            string voiceId = null,
            bool showcaseExpressiveBody = false,
            string languageCodeOverride = null,
            ShowcaseTtsRenderOptions segmentTts = null,
            bool emphaticCta = false)
        {
            var sanitized = VietnameseTtsTextNormalizer.SanitizeForElevenLabsRequest(text);
            var cleanText = emphaticHook || emphaticCta || showcaseExpressiveBody
                ? sanitized
                : ApplyDeepPauses(sanitized);
            var voiceSettings = ResolveVoiceSettings(emphaticHook, showcaseExpressiveBody, segmentTts, emphaticCta);
            var lang = (languageCodeOverride ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(lang))
            {
                lang = ResolveLanguageCode(settings);
            }

            var vid = (voiceId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(vid))
            {
                return new
                {
                    text = cleanText,
                    model_id = ResolveModelId(settings),
                    language_code = lang,
                    voice_settings = voiceSettings
                };
            }

            return new
            {
                text = cleanText,
                model_id = ResolveModelId(settings),
                language_code = lang,
                voice_id = vid,
                voice_settings = voiceSettings
            };
        }

        /// <summary>JSON body POST /text-to-speech/{voice_id} — luôn có language_code vi.</summary>
        public static object BuildPayload(string text, AppSettings settings, bool emphaticHook)
        {
            return BuildPayload(text, settings, emphaticHook, voiceId: null, showcaseExpressiveBody: false);
        }

        public static object CreateVoiceSettings(VoiceDeliveryMode mode)
        {
            switch (mode)
            {
                case VoiceDeliveryMode.Hook:
                    return CreateVietnameseHookVoiceSettings();
                case VoiceDeliveryMode.ShowcaseBody:
                    return CreateShowcaseBodyVoiceSettings();
                case VoiceDeliveryMode.ShowcaseCta:
                    return CreateShowcaseCtaVoiceSettings();
                default:
                    return CreateVietnameseNarrationVoiceSettings();
            }
        }

        /// <summary>Thay dấu chấm câu bằng ... để ElevenLabs nghỉ sâu hơn (triết lý / quote).</summary>
        public static string ApplyDeepPauses(string text)
        {
            var s = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            s = Regex.Replace(s, @"(?<=[^\d])\.(?=\s|$)", "...");
            s = Regex.Replace(s, @"(?<=[^\d])\.(?=[^\s\d])", "...");
            return s;
        }

        /// <summary>Nghỉ ngắn cho hook ngắn (dấu phẩy → … nhẹ).</summary>
        public static string ApplyHookDeliveryPauses(string text)
        {
            var s = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            s = Regex.Replace(s, @",\s+", ", … ");
            return s;
        }

        /// <summary>Payload mặc định khi không có <see cref="AppSettings"/> (model/lang từ hằng số helper).</summary>
        public static object BuildPayload(string text, bool emphaticHook)
        {
            return BuildPayload(text, settings: null, emphaticHook);
        }

        public static bool IsElevenLabsEndpoint(string endpoint) =>
            (endpoint ?? string.Empty).IndexOf("elevenlabs.io", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Chuẩn hóa ô Cài đặt (voice_id hoặc URL cũ) → URL đầy đủ ElevenLabs.</summary>
        public static string NormalizeSettingsEndpoint(string endpointOrVoiceId)
        {
            var raw = (endpointOrVoiceId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            if (raw.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return string.Empty;
            }

            var extracted = ExtractVoiceIdFromEndpoint(raw);
            if (!string.IsNullOrEmpty(extracted) &&
                (IsElevenLabsEndpoint(raw) || raw.IndexOf("text-to-speech", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return DefaultTextToSpeechEndpointPrefix + extracted;
            }

            if (!raw.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var id = raw.Trim().Trim('/');
                if (IsPlausibleVoiceId(id))
                {
                    return DefaultTextToSpeechEndpointPrefix + id;
                }
            }

            if (IsElevenLabsEndpoint(raw))
            {
                return raw;
            }

            return raw;
        }

        /// <summary>Hiển thị ô Cài đặt — chỉ voice_id nếu là ElevenLabs.</summary>
        public static string FormatSettingsVoiceIdField(string storedEndpoint)
        {
            var normalized = NormalizeSettingsEndpoint(storedEndpoint);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (IsElevenLabsEndpoint(normalized))
            {
                return ExtractVoiceIdFromEndpoint(normalized) ?? string.Empty;
            }

            var legacyId = ExtractVoiceIdFromEndpoint(storedEndpoint);
            return !string.IsNullOrEmpty(legacyId) ? legacyId : (storedEndpoint ?? string.Empty).Trim();
        }

        public static bool IsPlausibleVoiceId(string voiceId)
        {
            voiceId = (voiceId ?? string.Empty).Trim();
            return voiceId.Length > 0 && VoiceIdPattern.IsMatch(voiceId);
        }

        /// <summary>Kiểm tra giá trị ô Voice ID trước khi lưu (rỗng = hợp lệ).</summary>
        public static bool IsValidSettingsVoiceField(string endpointOrVoiceId)
        {
            var raw = (endpointOrVoiceId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return true;
            }

            if (raw.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            var normalized = NormalizeSettingsEndpoint(raw);
            return IsElevenLabsEndpoint(normalized) && EndpointIncludesVoiceId(normalized);
        }
    }
}
