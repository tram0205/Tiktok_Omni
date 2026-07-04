using System;
using System.Text.RegularExpressions;

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

        public const double DefaultStability = 0.5;

        public const double DefaultSimilarityBoost = 0.75;

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

        /// <summary>Giữ tương thích tên cũ — cùng preset Web.</summary>
        public static object CreateVietnameseHookVoiceSettings()
        {
            return CreateDefaultVoiceSettings();
        }

        /// <summary>Giữ tương thích tên cũ — cùng preset Web.</summary>
        public static object CreateVietnameseNarrationVoiceSettings()
        {
            return CreateDefaultVoiceSettings();
        }

        /// <summary>JSON body POST /text-to-speech/{voice_id} — luôn có language_code vi.</summary>
        public static object BuildPayload(string text, AppSettings settings, bool emphaticHook, string voiceId = null)
        {
            var cleanText = ApplyDeepPauses(VietnameseTtsTextNormalizer.SanitizeForElevenLabsRequest(text));
            var vid = (voiceId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(vid))
            {
                return new
                {
                    text = cleanText,
                    model_id = ResolveModelId(settings),
                    language_code = ResolveLanguageCode(settings),
                    voice_settings = emphaticHook
                        ? CreateVietnameseHookVoiceSettings()
                        : CreateVietnameseNarrationVoiceSettings()
                };
            }

            return new
            {
                text = cleanText,
                model_id = ResolveModelId(settings),
                language_code = ResolveLanguageCode(settings),
                voice_id = vid,
                voice_settings = emphaticHook
                    ? CreateVietnameseHookVoiceSettings()
                    : CreateVietnameseNarrationVoiceSettings()
            };
        }

        /// <summary>JSON body POST /text-to-speech/{voice_id} — luôn có language_code vi.</summary>
        public static object BuildPayload(string text, AppSettings settings, bool emphaticHook)
        {
            return BuildPayload(text, settings, emphaticHook, voiceId: null);
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

        /// <summary>Payload mặc định khi không có <see cref="AppSettings"/> (model/lang từ hằng số helper).</summary>
        public static object BuildPayload(string text, bool emphaticHook)
        {
            return BuildPayload(text, settings: null, emphaticHook);
        }
    }
}
