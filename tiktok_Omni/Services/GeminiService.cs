using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public class GeminiService
    {
        private static readonly SemaphoreSlim _geminiThrottle = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Inline video payload limit for Gemini REST.
        /// After compression the clip is always well below this.
        /// </summary>
        private const long MaxInlineVideoBytes = 50 * 1024 * 1024;

        private const long MaxInlineImageBytes = 8 * 1024 * 1024;

        public Task<string> GenerateAffiliateExperienceScriptAsync(
            string promptKind,
            IList<AiVideoGenInputItem> items,
            AiVideoGenInputItem primary,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default,
            GeminiStyleTemplate styleTemplate = GeminiStyleTemplate.Storytelling)
        {
            string prompt;
            switch ((promptKind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "deep":
                    prompt = AffiliateScriptPromptBuilder.BuildAffiliateDeepNarrationPrompt(primary, items, styleTemplate);
                    break;
                case "per-product":
                    prompt = AffiliateScriptPromptBuilder.BuildPerProductReviewScriptsPrompt(items, styleTemplate);
                    break;
                default:
                    prompt = AffiliateScriptPromptBuilder.BuildSlideshowNarrationPrompt(items, styleTemplate);
                    break;
            }

            return GenerateScriptAsync(prompt, provider, apiKey, model, cancellationToken);
        }

        /// <summary>Sinh kịch bản Triết lý — JSON array [{content, mood, motion_prompt, broll_video}].</summary>
        public async Task<IReadOnlyList<PhilosophyScriptItem>> GeneratePhilosophyScriptsAsync(
            string topic,
            string mode,
            int count,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            int minDurationSeconds = 15,
            int maxDurationSeconds = 60,
            string profileName = null,
            AppSettings settings = null,
            string contentTemplateId = null,
            string contentMetadata = null,
            CancellationToken cancellationToken = default)
        {
            var preset = PhilosophyContentTemplatePresets.Resolve(contentTemplateId);
            var subject = PhilosophyContentTemplatePresets.BuildTopicWithMetadata(topic, contentMetadata, preset);
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException("Chủ đề không được trống.", nameof(topic));
            }

            var normalizedMode = (mode ?? preset.GenerationMode).Trim();
            var isStory = normalizedMode.Equals("Story", StringComparison.OrdinalIgnoreCase)
                          || normalizedMode.Contains("chuyện", StringComparison.OrdinalIgnoreCase)
                          || preset.IsStory;
            var itemCount = isStory ? 1 : Math.Max(1, Math.Min(count, 20));
            var (minSec, maxSec) = PhilosophyRenderOptions.NormalizeDurationBounds(minDurationSeconds, maxDurationSeconds);

            var mascot = PhilosophyGeminiBackgroundContext.BuildMascotContext(profileName, settings);
            var brollSection = PhilosophyGeminiBackgroundContext.BuildBrollCatalogPromptSection(profileName);
            var zoomSection = PhilosophyGeminiBackgroundContext.BuildZoomImageCatalogPromptSection(profileName);
            var musicSection = PhilosophyGeminiBackgroundContext.BuildMusicCatalogPromptSection(settings);
            var ambientSection = PhilosophyGeminiBackgroundContext.BuildSharedSfxCatalogPromptSection(settings);
            var edgeStyleSection = PhilosophyGeminiBackgroundContext.BuildEdgeStylePromptSection();
            var subtitleSection = PhilosophyGeminiSubtitleContext.BuildSubtitlePromptSection();
            var edgeTtsSection = PhilosophyGeminiTtsContext.GeminiEdgeTtsWritingSection;
            var motionRules = PhilosophyGeminiBackgroundContext.BuildMotionPromptRules(mascot);
            var templateHint = (preset?.PromptHint ?? string.Empty).Trim();

            var prompt = isStory
                ? BuildPhilosophyStoryPrompt(subject, minSec, maxSec, motionRules, brollSection, zoomSection, musicSection, ambientSection, edgeStyleSection, subtitleSection, edgeTtsSection, templateHint)
                : BuildPhilosophyQuotesPrompt(subject, itemCount, minSec, maxSec, motionRules, brollSection, zoomSection, musicSection, ambientSection, edgeStyleSection, subtitleSection, edgeTtsSection, templateHint);

            if (mascot.HasMascotImage)
            {
                prompt = "REFERENCE IMAGE ATTACHED: this is the canonical mascot for profile «" + mascot.ProfileName +
                         "». Study the character's visible appearance before writing JSON. " +
                         "Every motion_prompt must describe THIS exact character (art style, hair, outfit, colors, accessories) — not a generic figure.\r\n\r\n" +
                         prompt;
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            string raw;
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                raw = await GenerateScriptAsync(prompt, provider, apiKey, model, cancellationToken).ConfigureAwait(false);
            }
            else if (mascot.HasMascotImage)
            {
                raw = await SendGeminiJsonWithImageAsync(
                    prompt,
                    mascot.MascotImagePath,
                    model,
                    apiKey,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                raw = await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken, jsonResponse: true).ConfigureAwait(false);
            }

            var parsed = ParsePhilosophyScriptsJson(raw, isStory, profileName, settings);
            if (parsed.Count == 0)
            {
                throw new InvalidOperationException("Gemini không trả JSON kịch bản hợp lệ.");
            }

            return parsed;
        }

        /// <summary>Gợi ý lại B-roll / zoom / motion_prompt cho các quote có sẵn (không đổi content).</summary>
        public async Task<IReadOnlyList<PhilosophyBackgroundGeminiSuggestion>> GeneratePhilosophyBackgroundSuggestionsAsync(
            IReadOnlyList<PhilosophyScriptItem> quotes,
            string topic,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            string profileName = null,
            AppSettings settings = null,
            CancellationToken cancellationToken = default)
        {
            if (quotes == null || quotes.Count == 0)
            {
                return Array.Empty<PhilosophyBackgroundGeminiSuggestion>();
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var mascot = PhilosophyGeminiBackgroundContext.BuildMascotContext(profileName, settings);
            var brollSection = PhilosophyGeminiBackgroundContext.BuildBrollCatalogPromptSection(profileName);
            var zoomSection = PhilosophyGeminiBackgroundContext.BuildZoomImageCatalogPromptSection(profileName);
            var motionRules = PhilosophyGeminiBackgroundContext.BuildMotionPromptRules(mascot);
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Bạn gợi ý NỀN VIDEO cho từng quote triết lý TikTok — KHÔNG viết lại content.");
            sb.AppendLine("CHỦ ĐỀ BATCH: [" + (topic ?? string.Empty).Trim() + "]");
            sb.AppendLine();
            sb.AppendLine(motionRules);
            sb.AppendLine();
            sb.AppendLine(brollSection);
            sb.AppendLine();
            sb.AppendLine(zoomSection);
            sb.AppendLine();
            sb.AppendLine("DANH SÁCH QUOTE (giữ đúng thứ tự, trả 1 phần tử JSON cho mỗi quote):");
            for (var i = 0; i < quotes.Count; i++)
            {
                var q = quotes[i];
                sb.AppendLine((i + 1).ToString(inv) + ". mood=" + (q?.Mood ?? "reflective")
                              + " | «" + PhilosophyBatchHelper.TrimGridLabel(q?.Content ?? string.Empty, 200, "") + "»");
            }

            sb.AppendLine();
            sb.AppendLine("Trả DUY NHẤT JSON array cùng số phần tử, không markdown:");
            sb.AppendLine("[{\"motion_prompt\":\"...\",\"broll_video\":\"file.mp4 hoặc @random\",\"zoom_images\":[\"a.jpg\"]}]");
            sb.AppendLine("Quy tắc: có zoom_images khớp quote → ưu tiên zoom; không thì broll_video; motion_prompt cho AI Veo nếu cần.");

            var prompt = sb.ToString();
            string raw;
            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                raw = await GenerateScriptAsync(prompt, provider, apiKey, model, cancellationToken).ConfigureAwait(false);
            }
            else if (mascot.HasMascotImage)
            {
                raw = await SendGeminiJsonWithImageAsync(
                    prompt,
                    mascot.MascotImagePath,
                    model,
                    apiKey,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                raw = await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken, jsonResponse: true).ConfigureAwait(false);
            }

            return ParsePhilosophyBackgroundSuggestionsJson(raw, profileName);
        }

        private static IReadOnlyList<PhilosophyBackgroundGeminiSuggestion> ParsePhilosophyBackgroundSuggestionsJson(
            string raw,
            string profileName)
        {
            var text = ExtractJsonArray(raw);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<PhilosophyBackgroundGeminiSuggestion>();
            }

            try
            {
                var arr = JArray.Parse(text);
                var result = new List<PhilosophyBackgroundGeminiSuggestion>();
                foreach (var token in arr)
                {
                    var motion = (token["motion_prompt"] ?? token["motionPrompt"])?.ToString()?.Trim() ?? string.Empty;
                    var broll = (token["broll_video"] ?? token["brollVideo"] ?? token["broll"])?.ToString()?.Trim() ?? string.Empty;
                    var zoom = ParseGeminiZoomImages(profileName, token["zoom_images"] ?? token["zoomImages"]);
                    var suggestion = new PhilosophyBackgroundGeminiSuggestion
                    {
                        MotionPrompt = motion,
                        ZoomImagePaths = zoom
                    };

                    if (!string.IsNullOrWhiteSpace(profileName))
                    {
                        suggestion.BRollFolder = PhilosophyBRollSelection.ResolveGeminiBrollFileName(profileName, broll);
                    }
                    else
                    {
                        suggestion.BRollFolder = broll;
                    }

                    if (zoom.Count > 0)
                    {
                        suggestion.PreferZoomMode = true;
                    }

                    result.Add(suggestion);
                }

                return result;
            }
            catch
            {
                return Array.Empty<PhilosophyBackgroundGeminiSuggestion>();
            }
        }

        private static string BuildPhilosophyQuotesPrompt(
            string topic,
            int count,
            int minSeconds,
            int maxSeconds,
            string motionRules,
            string brollSection,
            string zoomSection,
            string musicSection,
            string ambientSection,
            string edgeStyleSection,
            string subtitleSection,
            string edgeTtsSection,
            string templateHint = null)
        {
            var (minWords, maxWords) = PhilosophyRenderOptions.EstimateSpeechWordCount(minSeconds, maxSeconds);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var styleBlock = string.IsNullOrWhiteSpace(templateHint)
                ? string.Empty
                : "LOẠI NỘI DUNG / PHONG CÁCH:\r\n" + templateHint.Trim() + "\r\n\r\n";
            return "Bạn là biên kịch video triết lý TikTok tiếng Việt.\r\n" +
                   styleBlock +
                   "CHỦ ĐỀ: [" + topic + "]\r\n\r\n" +
                   edgeTtsSection + "\r\n\r\n" +
                   "THỜI LƯỢNG MỖI VIDEO: " + minSeconds.ToString(inv) + "–" + maxSeconds.ToString(inv) +
                   " giây (giọng đọc chậm, trầm, có khoảng dừng sau câu).\r\n" +
                   "Viết đúng " + count.ToString(inv) +
                   " câu quote độc lập, sâu sắc, cảm xúc.\r\n" +
                   "Mỗi quote BẮT BUỘC " + minWords.ToString(inv) + "–" + maxWords.ToString(inv) +
                   " từ tiếng Việt có dấu — không ít hơn " + minWords.ToString(inv) +
                   " và không quá " + maxWords.ToString(inv) + " từ.\r\n" +
                   "TUYỆT ĐỐI không vượt " + maxWords.ToString(inv) + " từ.\r\n" +
                   "Video xuất = thời gian đọc hết quote + "
                   + PhilosophyRenderOptions.OutroPadMinSeconds.ToString("0", inv)
                   + "–"
                   + PhilosophyRenderOptions.OutroPadMaxSeconds.ToString("0", inv)
                   + " giây thở (tùy độ dài video phân cảnh cuối) — căn số từ cho khớp khung "
                   + minSeconds.ToString(inv) + "–" + maxSeconds.ToString(inv) + " giây đọc.\r\n" +
                   "Mood (tâm trạng) — BẮT BUỘC mỗi phần tử có trường \"mood\", chọn ĐÚNG cảm xúc của câu đó:\r\n" +
                   "  • calm — bình an, nhẹ nhàng, an nhiên\r\n" +
                   "  • melancholic — buồn, day dứt, hoài niệm\r\n" +
                   "  • hopeful — hy vọng, lạc quan, hướng về tương lai\r\n" +
                   "  • intense — mạnh mẽ, thúc đẩy, gay cấn\r\n" +
                   "  • reflective — suy ngẫm, chiêm nghiệm, trầm lắng (chỉ dùng khi câu mang tone suy tư)\r\n" +
                   "Không được để tất cả cùng mood. Với nhiều quote, mood phải đa dạng và khớp nội dung từng câu.\r\n\r\n" +
                   ambientSection + "\r\n\r\n" +
                   edgeStyleSection + "\r\n\r\n" +
                   subtitleSection + "\r\n\r\n" +
                   musicSection + "\r\n\r\n" +
                   motionRules + "\r\n\r\n" +
                   brollSection + "\r\n\r\n" +
                   zoomSection + "\r\n\r\n" +
                   "Trả về DUY NHẤT JSON array hợp lệ, không markdown, không giải thích:\r\n" +
                   "[{\"content\":\"...\",\"mood\":\"melancholic\",\"edge_style\":\"ke_chuyen\",\"subtitle_look\":\"StoryItalic\",\"subtitle_size\":76,\"subtitle_position\":\"middle\",\"subtitle_effect\":\"Highlight\",\"subtitle_line_bg\":\"Không nền\",\"ambient_sfx\":\"rain-light.mp3\",\"music_file\":\"sad-piano-bed.mp3\",\"motion_prompt\":\"Chibi art style, same profile mascot from reference — medium dark bob hair, light blue floral ao dai, white pants — stands alone looking out a rain-streaked classroom window, melancholic reflective expression, same face and outfit as reference, 9:16 vertical aspect ratio\",\"broll_video\":\"rain-city-night.mp4\",\"zoom_images\":[\"forest-mist.jpg\",\"mountain-sunrise.jpg\"]}," +
                   "{\"content\":\"...\",\"mood\":\"hopeful\",\"edge_style\":\"ke_chuyen\",\"subtitle_look\":\"TikTokWhite\",\"subtitle_size\":80,\"subtitle_position\":\"middle\",\"subtitle_effect\":\"FadeIn\",\"subtitle_line_bg\":\"Không nền\",\"ambient_sfx\":\"none\",\"music_file\":\"hopeful-strings.mp3\",\"motion_prompt\":\"...\",\"broll_video\":\"@random\",\"zoom_images\":[]}]";
        }

        private static string BuildPhilosophyStoryPrompt(
            string topic,
            int minSeconds,
            int maxSeconds,
            string motionRules,
            string brollSection,
            string zoomSection,
            string musicSection,
            string ambientSection,
            string edgeStyleSection,
            string subtitleSection,
            string edgeTtsSection,
            string templateHint = null)
        {
            var (minWords, maxWords) = PhilosophyRenderOptions.EstimateSpeechWordCount(minSeconds, maxSeconds);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var styleBlock = string.IsNullOrWhiteSpace(templateHint)
                ? string.Empty
                : "LOẠI NỘI DUNG / PHONG CÁCH:\r\n" + templateHint.Trim() + "\r\n\r\n";
            return "Bạn là biên kịch video triết lý TikTok tiếng Việt.\r\n" +
                   styleBlock +
                   "CHỦ ĐỀ / BÀI HỌC: [" + topic + "]\r\n\r\n" +
                   edgeTtsSection + "\r\n\r\n" +
                   "THỜI LƯỢNG VIDEO: " + minSeconds.ToString(inv) + "–" + maxSeconds.ToString(inv) +
                   " giây (giọng kể chậm, trầm, có khoảng dừng).\r\n" +
                   "Viết MỘT câu chuyện ngắn có 3 phần trong cùng một đoạn: Mở đầu → Thân bài → Kết luận bài học.\r\n" +
                   "Tổng BẮT BUỘC " + minWords.ToString(inv) + "–" + maxWords.ToString(inv) +
                   " từ tiếng Việt có dấu — không ít hơn " + minWords.ToString(inv) +
                   " và không quá " + maxWords.ToString(inv) + " từ.\r\n" +
                   "Mood (tâm trạng) — BẮT BUỘC mỗi phần tử có trường \"mood\", chọn ĐÚNG cảm xúc của câu đó:\r\n" +
                   "  • calm — bình an, nhẹ nhàng, an nhiên\r\n" +
                   "  • melancholic — buồn, day dứt, hoài niệm\r\n" +
                   "  • hopeful — hy vọng, lạc quan, hướng về tương lai\r\n" +
                   "  • intense — mạnh mẽ, thúc đẩy, gay cấn\r\n" +
                   "  • reflective — suy ngẫm, chiêm nghiệm, trầm lắng (chỉ dùng khi câu mang tone suy tư)\r\n" +
                   "Không được để tất cả cùng mood. Với nhiều quote, mood phải đa dạng và khớp nội dung từng câu.\r\n\r\n" +
                   ambientSection + "\r\n\r\n" +
                   edgeStyleSection + "\r\n\r\n" +
                   subtitleSection + "\r\n\r\n" +
                   musicSection + "\r\n\r\n" +
                   motionRules + "\r\n\r\n" +
                   brollSection + "\r\n\r\n" +
                   zoomSection + "\r\n\r\n" +
                   "Trả về DUY NHẤT JSON array 1 phần tử, không markdown:\r\n" +
                   "[{\"content\":\"...\",\"mood\":\"reflective\",\"edge_style\":\"ke_chuyen\",\"subtitle_look\":\"StoryItalic\",\"subtitle_size\":76,\"subtitle_position\":\"middle\",\"subtitle_effect\":\"Highlight\",\"subtitle_line_bg\":\"Không nền\",\"ambient_sfx\":\"night-crickets.mp3\",\"music_file\":\"calm-ambient.mp3\",\"motion_prompt\":\"...\",\"broll_video\":\"forest-path.mp4\",\"zoom_images\":[\"lake-sunset.jpg\",\"misty-forest.jpg\",\"autumn-leaves.jpg\"]}]";
        }

        private static List<PhilosophyScriptItem> ParsePhilosophyScriptsJson(
            string raw,
            bool isStory,
            string profileName = null,
            AppSettings settings = null)
        {
            var text = ExtractJsonArray(raw);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<PhilosophyScriptItem>();
            }

            try
            {
                var arr = JArray.Parse(text);
                var result = new List<PhilosophyScriptItem>();
                foreach (var token in arr)
                {
                    var content = PhilosophyGeminiTtsContext.NormalizeQuoteContent(
                        (token["content"] ?? token["quote"] ?? token["text"])?.ToString());
                    if (string.IsNullOrEmpty(content))
                    {
                        continue;
                    }

                    var mood = NormalizePhilosophyMood((token["mood"] ?? token["tone"])?.ToString());
                    var ambientSfx = (token["ambient_sfx"] ?? token["ambientSfx"] ?? token["ambient"] ?? token["ambience"] ?? token["sfx"])?.ToString();
                    var motionPrompt = (token["motion_prompt"] ?? token["motionPrompt"])?.ToString()?.Trim() ?? string.Empty;
                    var brollVideo = (token["broll_video"] ?? token["brollVideo"] ?? token["broll"])?.ToString()?.Trim() ?? string.Empty;
                    var musicFile = (token["music_file"] ?? token["musicFile"] ?? token["background_music"])?.ToString()?.Trim() ?? string.Empty;
                    var edgeStyle = (token["edge_style"] ?? token["edgeStyle"])?.ToString()?.Trim() ?? string.Empty;
                    var subtitleLook = (token["subtitle_look"] ?? token["subtitleLook"])?.ToString()?.Trim() ?? string.Empty;
                    var subtitleSize = (token["subtitle_size"] ?? token["subtitleSize"])?.ToString()?.Trim() ?? string.Empty;
                    var subtitlePosition = (token["subtitle_position"] ?? token["subtitlePosition"])?.ToString()?.Trim() ?? string.Empty;
                    var subtitleEffect = (token["subtitle_effect"] ?? token["subtitleEffect"])?.ToString()?.Trim() ?? string.Empty;
                    var subtitleLineBg = (token["subtitle_line_bg"] ?? token["subtitleLineBg"] ?? token["subtitle_line_background"])?.ToString()?.Trim() ?? string.Empty;
                    var row = new PhilosophyScriptItem
                    {
                        Content = content,
                        Mood = mood,
                        MotionPrompt = motionPrompt,
                        EdgeStyleKey = edgeStyle,
                        Status = "Nháp"
                    };
                    PhilosophyGeminiSubtitleContext.ApplyGeminiHints(
                        row,
                        subtitleLook,
                        subtitleSize,
                        subtitlePosition,
                        subtitleEffect,
                        subtitleLineBg);
                    if (!string.IsNullOrWhiteSpace(profileName))
                    {
                        row.BRollFolder = PhilosophyBRollSelection.ResolveGeminiBrollFileName(profileName, brollVideo);
                        row.ZoomImagePaths = ParseGeminiZoomImages(profileName, token["zoom_images"] ?? token["zoomImages"]);
                        if (row.ZoomImagePaths.Count > 0)
                        {
                            row.VisualMode = PhilosophyVisualModes.ImageZoom;
                        }
                    }

                    if (settings != null)
                    {
                        row.MusicFolder = PhilosophyBatchHelper.ResolveGeminiMusicFileName(settings, musicFile, mood);
                    }

                    PhilosophyAmbientCatalog.ApplyGeminiAmbient(row, ambientSfx, settings);
                    result.Add(row);
                }

                if (isStory && result.Count > 1)
                {
                    var merged = string.Join("\r\n\r\n", result.Select(x => x.Content));
                    var mood = result[0].Mood;
                    var ambient = result[0].AmbientKey;
                    var storyRow = new PhilosophyScriptItem
                    {
                        Content = merged,
                        Mood = mood,
                        MotionPrompt = result[0].MotionPrompt,
                        BRollFolder = result[0].BRollFolder,
                        ZoomImagePaths = result[0].ZoomImagePaths?.ToList() ?? new List<string>(),
                        VisualMode = result[0].VisualMode,
                        MusicFolder = result[0].MusicFolder,
                        AmbientKey = result[0].AmbientKey,
                        EdgeStyleKey = result[0].EdgeStyleKey,
                        SubtitleLookPreset = result[0].SubtitleLookPreset,
                        SubtitleFontSize = result[0].SubtitleFontSize,
                        SubtitlePosition = result[0].SubtitlePosition,
                        SubtitleDisplayAnimation = result[0].SubtitleDisplayAnimation,
                        SubtitleHighlightColourAss = result[0].SubtitleHighlightColourAss,
                        SubtitleAnimation = result[0].SubtitleAnimation,
                        SubtitleEnabled = result[0].SubtitleEnabled,
                        Status = "Nháp"
                    };
                    PhilosophyAmbientCatalog.ApplyGeminiAmbient(storyRow, ambient);
                    return new List<PhilosophyScriptItem> { storyRow };
                }

                return result;
            }
            catch
            {
                return new List<PhilosophyScriptItem>();
            }
        }

        private static List<string> ParseGeminiZoomImages(string profileName, JToken token)
        {
            var suggestions = new List<string>();
            if (token == null)
            {
                return suggestions;
            }

            if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    var text = item?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        suggestions.Add(text);
                    }
                }
            }
            else
            {
                var raw = token.ToString().Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    suggestions.AddRange(raw.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0));
                }
            }

            return PhilosophyBRollSelection.ResolveGeminiZoomImageNames(profileName, suggestions);
        }

        private static string ExtractJsonArray(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = Regex.Match(s, @"```(?:json)?\s*(\[[\s\S]*?\])\s*```", RegexOptions.IgnoreCase);
            if (fence.Success)
            {
                return fence.Groups[1].Value.Trim();
            }

            var start = s.IndexOf('[');
            var end = s.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                return s.Substring(start, end - start + 1);
            }

            return s;
        }

        private static string NormalizePhilosophyMood(string mood)
        {
            var m = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var valid = new[] { "calm", "hopeful", "melancholic", "intense", "reflective" };
            return valid.Contains(m) ? m : "reflective";
        }

        public async Task<string> GenerateScriptAsync(
            string prompt,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                return await SendClaudeRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
            }

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                InvalidOperationException lastRateLimit = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        return await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex) when (attempt < 2 && IsLikelyGeminiQuotaOrRateLimit(ex.Message))
                    {
                        lastRateLimit = ex;
                        var waitSec = 6 * (attempt + 1);
                        await Task.Delay(TimeSpan.FromSeconds(waitSec), cancellationToken).ConfigureAwait(false);
                    }
                }

                if (lastRateLimit != null)
                {
                    throw lastRateLimit;
                }

                return await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        /// <summary>Gửi prompt kèm ảnh tham chiếu (Gemini multimodal).</summary>
        public async Task<string> GenerateScriptWithImageAsync(
            string prompt,
            string imagePath,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("Reference image not found.", imagePath);
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                return await SendClaudeRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
            }

            var fileInfo = new FileInfo(imagePath);
            if (fileInfo.Length > MaxInlineImageBytes)
            {
                throw new InvalidOperationException(
                    "Ảnh tham chiếu (" + (fileInfo.Length / 1024d / 1024d).ToString("0.0") +
                    " MB) lớn hơn giới hạn inline của Gemini.");
            }

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
                var mime = GuessImageMime(imagePath);
                var bytes = await ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(bytes);

                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = prompt },
                                new JObject
                                {
                                    ["inline_data"] = new JObject
                                    {
                                        ["mime_type"] = mime,
                                        ["data"] = b64
                                    }
                                })
                        })
                };

                var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + safeModel + ":generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(3));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var json = JObject.Parse(response.Content ?? "{}");
                return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                       ?? string.Empty;
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        /// <summary>Showcase: gửi NHIỀU ảnh + 1 prompt văn bản cho Gemini (vision) — dùng để suy luận chủ đề/thứ tự cảnh.</summary>
        public async Task<string> GenerateScriptWithImagesAsync(
            string prompt,
            IList<string> imagePaths,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (imagePaths == null || imagePaths.Count == 0)
            {
                throw new ArgumentException("At least one reference image is required.", nameof(imagePaths));
            }

            foreach (var path in imagePaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    throw new FileNotFoundException("Reference image not found.", path ?? string.Empty);
                }
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                // Claude multi-image inline chưa được hỗ trợ trong codebase — fallback text-only.
                return await SendClaudeRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
            }

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
                var parts = new JArray { new JObject { ["text"] = prompt } };
                foreach (var path in imagePaths)
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Length > MaxInlineImageBytes)
                    {
                        throw new InvalidOperationException(
                            "Ảnh «" + Path.GetFileName(path) + "» (" + (fileInfo.Length / 1024d / 1024d).ToString("0.0") +
                            " MB) lớn hơn giới hạn inline của Gemini.");
                    }

                    var mime = GuessImageMime(path);
                    var bytes = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    var b64 = Convert.ToBase64String(bytes);
                    parts.Add(new JObject
                    {
                        ["inline_data"] = new JObject
                        {
                            ["mime_type"] = mime,
                            ["data"] = b64
                        }
                    });
                }

                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = parts
                        }),
                    ["generationConfig"] = new JObject
                    {
                        ["responseMimeType"] = "application/json"
                    }
                };

                var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + safeModel + ":generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(3));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var json = JObject.Parse(response.Content ?? "{}");
                return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                       ?? string.Empty;
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        /// <summary>Showcase: nén từng clip phân cảnh rồi gửi Gemini vision — viết thoại khớp clip.</summary>
        public async Task<string> GenerateShowcaseVoiceoverFromClipsAsync(
            string prompt,
            IList<string> clipPaths,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            Action<string> logAction = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt is required.", nameof(prompt));
            }

            if (clipPaths == null || clipPaths.Count == 0)
            {
                throw new ArgumentException("At least one clip is required.", nameof(clipPaths));
            }

            foreach (var path in clipPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    throw new FileNotFoundException("Scene clip not found.", path ?? string.Empty);
                }
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                return await SendClaudeRequestAsync(prompt, model, apiKey, cancellationToken).ConfigureAwait(false);
            }

            var compressedPaths = new List<string>();
            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                for (var i = 0; i < clipPaths.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    logAction?.Invoke("[Showcase] Nén clip cảnh " + (i + 1) + "/" + clipPaths.Count +
                                      " (144p, giữ tốc độ gốc) — " + Path.GetFileName(clipPaths[i]));
                    var compressed = await CompressVideoForGeminiAsync(
                        clipPaths[i],
                        cancellationToken,
                        preservePlaybackSpeed: true).ConfigureAwait(false);
                    compressedPaths.Add(compressed);
                }

                var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
                var parts = new JArray { new JObject { ["text"] = prompt } };
                long totalBytes = 0;
                for (var i = 0; i < compressedPaths.Count; i++)
                {
                    var path = compressedPaths[i];
                    var fileInfo = new FileInfo(path);
                    totalBytes += fileInfo.Length;
                    if (fileInfo.Length > MaxInlineVideoBytes)
                    {
                        throw new InvalidOperationException(
                            "Clip cảnh " + (i + 1) + " sau nén vẫn quá lớn (" +
                            (fileInfo.Length / 1024d / 1024d).ToString("0.0") + " MB).");
                    }

                    parts.Add(new JObject { ["text"] = "--- CẢNH " + (i + 1) + " (clip video) ---" });
                    var mime = GuessVideoMime(path);
                    var bytes = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    var b64 = Convert.ToBase64String(bytes);
                    parts.Add(new JObject
                    {
                        ["inline_data"] = new JObject
                        {
                            ["mime_type"] = mime,
                            ["data"] = b64
                        }
                    });
                }

                if (totalBytes > MaxInlineVideoBytes * 2)
                {
                    logAction?.Invoke("[Showcase] Tổng dung lượng clip gửi Gemini: " +
                                      (totalBytes / 1024d / 1024d).ToString("0.0") + " MB");
                }

                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = parts
                        }),
                    ["generationConfig"] = new JObject
                    {
                        ["responseMimeType"] = "application/json"
                    }
                };

                var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + safeModel + ":generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(8));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                logAction?.Invoke("[Showcase] Đang gửi " + clipPaths.Count + " clip cho Gemini viết thoại…");
                var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var json = JObject.Parse(response.Content ?? "{}");
                return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                       ?? string.Empty;
            }
            finally
            {
                foreach (var path in compressedPaths)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try { File.Delete(path); } catch { /* non-critical */ }
                    }
                }

                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        private static string GuessImageMime(string path)
        {
            var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            switch (ext)
            {
                case ".png": return "image/png";
                case ".webp": return "image/webp";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                default: return "image/jpeg";
            }
        }

        /// <summary>Model Gemini native image generation (hook intro Video reup) — Nano Banana / 2.5 Flash Image.</summary>
        public const string HookSceneImageModel = "gemini-2.5-flash-image";

        /// <summary>
        /// Sinh ảnh hook intro: giữ identity nhân vật từ ảnh tham chiếu, biểu cảm khớp câu hook.
        /// Lưu PNG/JPEG ra <paramref name="outputImagePath"/>.
        /// </summary>
        public async Task GenerateHookSceneImageAsync(
            string hookText,
            string mascotImagePath,
            string apiKey,
            string outputImagePath,
            Action<string> log = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Cần AI API Key (Gemini) để sinh ảnh hook intro.");
            }

            if (string.IsNullOrWhiteSpace(mascotImagePath) || !File.Exists(mascotImagePath))
            {
                throw new FileNotFoundException("Không tìm thấy ảnh profile (mascot).", mascotImagePath);
            }

            var hook = (hookText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(hook))
            {
                hook = "Biểu cảm tò mò, thu hút — nhìn thẳng camera.";
            }

            var fileInfo = new FileInfo(mascotImagePath);
            if (fileInfo.Length > MaxInlineImageBytes)
            {
                throw new InvalidOperationException(
                    "Ảnh profile quá lớn (" + (fileInfo.Length / 1024d / 1024d).ToString("0.0") +
                    " MB) — dùng ảnh dưới 8 MB.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");

            var prompt =
                "You are creating a TikTok hook intro frame (9:16 vertical portrait).\r\n" +
                "REFERENCE IMAGE: the channel mascot/character — keep the SAME face identity, hairstyle, outfit style, and art medium.\r\n" +
                "HOOK LINE (Vietnamese context, match mood/expression): \"" + hook + "\"\r\n\r\n" +
                "Generate ONE new image:\r\n" +
                "- Same character as reference, expression and pose matching the hook mood\r\n" +
                "- Clean background, cinematic lighting, no text, no watermark, no QR code\r\n" +
                "- Portrait 9:16 composition, face clearly visible, suitable for short-form video intro\r\n" +
                "- Photorealistic or consistent with reference style if illustrated";

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var mime = GuessImageMime(mascotImagePath);
                var bytes = await ReadAllBytesAsync(mascotImagePath, cancellationToken).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(bytes);

                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = prompt },
                                new JObject
                                {
                                    ["inline_data"] = new JObject
                                    {
                                        ["mime_type"] = mime,
                                        ["data"] = b64
                                    }
                                })
                        }),
                    ["generationConfig"] = new JObject
                    {
                        ["responseModalities"] = new JArray("IMAGE"),
                        ["imageConfig"] = new JObject
                        {
                            ["aspectRatio"] = "9:16"
                        }
                    }
                };

                var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + HookSceneImageModel + ":generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(5));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                log?.Invoke("[VideoReup] Gemini image gen (" + HookSceneImageModel + "): đang sinh ảnh hook intro…");
                var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var json = JObject.Parse(response.Content ?? "{}");
                var err = json["error"]?["message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(err))
                {
                    throw new InvalidOperationException("Gemini image gen lỗi: " + err);
                }

                var imageBytes = ExtractInlineImageBytes(json);
                if (imageBytes == null || imageBytes.Length < 1024)
                {
                    throw new InvalidOperationException("Gemini không trả về ảnh hook intro — thử lại hoặc kiểm tra model/quota.");
                }

                File.WriteAllBytes(outputImagePath, imageBytes);
                log?.Invoke("[VideoReup] Gemini image gen: đã lưu " + Path.GetFileName(outputImagePath) +
                            " (" + (imageBytes.Length / 1024) + " KB).");
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        private static byte[] ExtractInlineImageBytes(JObject json)
        {
            var parts = json["candidates"]?[0]?["content"]?["parts"] as JArray;
            if (parts == null)
            {
                return null;
            }

            foreach (var part in parts)
            {
                var inline = part["inline_data"] ?? part["inlineData"];
                if (inline == null)
                {
                    continue;
                }

                var mime = (inline["mime_type"] ?? inline["mimeType"])?.ToString() ?? string.Empty;
                if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = inline["data"]?.ToString();
                if (string.IsNullOrWhiteSpace(data))
                {
                    continue;
                }

                try
                {
                    return Convert.FromBase64String(data);
                }
                catch
                {
                    // try next part
                }
            }

            return null;
        }

        /// <summary>
        /// Generates an omnichannel caption optimised for the target <paramref name="platform"/>
        /// ("TikTok", "Facebook", or "YouTube").  Uses Gemini multimodal when the video is small
        /// enough; falls back to a text-only prompt derived from the filename otherwise.
        /// </summary>
        public async Task<string> GenerateOmnichannelCaptionAsync(
            string videoPath,
            string captionStyleKey,
            string platform,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                throw new FileNotFoundException("Video file not found.", videoPath);
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                return await GenerateCaptionTextOnlyAsync(
                    videoPath, captionStyleKey, platform,
                    model, apiKey, cancellationToken, useAnthropic: true).ConfigureAwait(false);
            }

            var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
            var fileInfo = new FileInfo(videoPath);
            if (fileInfo.Length > MaxInlineVideoBytes)
            {
                return await GenerateCaptionTextOnlyAsync(
                    videoPath, captionStyleKey, platform,
                    safeModel, apiKey, cancellationToken, useAnthropic: false).ConfigureAwait(false);
            }

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            string multimodalCaption = null;
            try
            {
                var mime = GuessVideoMime(videoPath);
                var bytes = await ReadAllBytesAsync(videoPath, cancellationToken).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(bytes);

                var prompt = BuildOmnichannelCaptionPrompt(platform, captionStyleKey, Path.GetFileName(videoPath), multimodal: true);
                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = prompt },
                                new JObject
                                {
                                    ["inline_data"] = new JObject
                                    {
                                        ["mime_type"] = mime,
                                        ["data"] = b64
                                    }
                                })
                        })
                };

                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(6));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var json = JObject.Parse(response.Content ?? "{}");
                var text = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    multimodalCaption = SanitizeCaptionOutput(text);
                }
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }

            if (!string.IsNullOrWhiteSpace(multimodalCaption))
            {
                return multimodalCaption;
            }

            return await GenerateCaptionTextOnlyAsync(
                videoPath, captionStyleKey, platform,
                safeModel, apiKey, cancellationToken, useAnthropic: false).ConfigureAwait(false);
        }

        public async Task<JObject> AnalyzeAffiliateVideoAsync(
            string videoPath,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                throw new FileNotFoundException("Video file not found.", videoPath);
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("AI provider API key is required.");
            }

            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedProvider.Contains("claude") || normalizedProvider.Contains("anthropic"))
            {
                throw new InvalidOperationException("Phân tích video chuyên sâu hiện chỉ hỗ trợ Gemini (provider 'gemini'). Hãy đổi AI Provider trong Cài đặt.");
            }

            var fileInfo = new FileInfo(videoPath);
            if (fileInfo.Length > MaxInlineVideoBytes)
            {
                throw new InvalidOperationException(
                    $"File video ({fileInfo.Length / 1024d / 1024d:0.0} MB) lớn hơn giới hạn inline {MaxInlineVideoBytes / 1024d / 1024d:0.0} MB của Gemini REST. Hãy nén nhỏ hơn nữa.");
            }

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
                var mime = GuessVideoMime(videoPath);
                var bytes = await ReadAllBytesAsync(videoPath, cancellationToken).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(bytes);

                const string prompt =
                    "Bạn là chuyên gia Marketing TikTok. Hãy xem kỹ video TikTok này và:\n" +
                    "1) Trích xuất CHÍNH XÁC lời thoại / voiceover (chỉ phần audio do người nói, không kèm thuyết minh phụ).\n" +
                    "2) Tóm tắt kịch bản từng cảnh theo timestamp (giây), súc tích.\n" +
                    "3) QUAN TRỌNG NHẤT: chỉ rõ (theo số giây, ví dụ 7-12s) đoạn 'ăn tiền' — đoạn mẫu mặc đẹp nhất / hiệu quả sử dụng sản phẩm rõ nhất / cú twist gây chú ý — giúp chốt đơn cao.\n\n" +
                    "Trả về DUY NHẤT một JSON hợp lệ (không markdown, không backticks), schema:\n" +
                    "{\n" +
                    "  \"voiceover\": \"<toàn bộ lời thoại tiếng Việt>\",\n" +
                    "  \"script_summary\": \"<tóm tắt kịch bản theo từng cảnh, mỗi cảnh bắt đầu bằng [mm:ss-mm:ss]>\",\n" +
                    "  \"money_shot\": \"<mô tả ngắn (1-3 câu) đoạn ăn tiền + khoảng thời gian chính xác>\"\n" +
                    "}\n" +
                    "Tất cả nội dung viết bằng tiếng Việt tự nhiên.";

                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = prompt },
                                new JObject
                                {
                                    ["inline_data"] = new JObject
                                    {
                                        ["mime_type"] = mime,
                                        ["data"] = b64
                                    }
                                })
                        }),
                    ["generationConfig"] = new JObject
                    {
                        ["temperature"] = 0.25,
                        ["responseMimeType"] = "application/json"
                    }
                };

                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(6));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                RestResponse response;
                try
                {
                    response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex) when (IsLikelyGeminiQuotaOrRateLimit(ex.Message))
                {
                    GeminiUsageTracker.Instance.RecordRateLimit429(ex.Message);
                    throw;
                }

                var json = JObject.Parse(response.Content ?? "{}");
                var err = json["error"]?["message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(err))
                {
                    if (IsLikelyGeminiQuotaOrRateLimit(err))
                    {
                        GeminiUsageTracker.Instance.RecordRateLimit429(err);
                    }

                    throw new InvalidOperationException("Gemini từ chối phân tích video: " + err);
                }

                var text = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Gemini không trả về nội dung phân tích.");
                }

                var cleaned = SanitizeCaptionOutput(text);
                try
                {
                    var parsed = JObject.Parse(cleaned);
                    parsed["_raw"] = text;
                    GeminiUsageTracker.Instance.RecordVideoAnalysisSuccess();
                    return parsed;
                }
                catch
                {
                    GeminiUsageTracker.Instance.RecordVideoAnalysisSuccess();
                    return new JObject
                    {
                        ["voiceover"] = string.Empty,
                        ["script_summary"] = cleaned,
                        ["money_shot"] = string.Empty,
                        ["_raw"] = text
                    };
                }
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        private static bool IsLikelyGeminiQuotaOrRateLimit(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.IndexOf("429", StringComparison.Ordinal) >= 0
                   || message.IndexOf("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.ReadAllBytes(path);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static string GuessVideoMime(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
            switch (ext)
            {
                case ".webm":
                    return "video/webm";
                case ".mov":
                    return "video/quicktime";
                case ".mkv":
                    return "video/x-matroska";
                default:
                    return "video/mp4";
            }
        }

        private async Task<string> GenerateCaptionTextOnlyAsync(
            string videoPath,
            string captionStyleKey,
            string platform,
            string model,
            string apiKey,
            CancellationToken cancellationToken,
            bool useAnthropic)
        {
            var fileName = Path.GetFileName(videoPath) ?? "video.mp4";
            var prompt = BuildOmnichannelCaptionPrompt(platform, captionStyleKey, fileName, multimodal: false);
            var raw = await GenerateScriptAsync(
                prompt, useAnthropic ? "claude" : "gemini", apiKey, model, cancellationToken)
                .ConfigureAwait(false);
            return SanitizeCaptionOutput(raw);
        }

        private static string BuildOmnichannelCaptionPrompt(
            string platform,
            string captionStyleKey,
            string fileLabel,
            bool multimodal)
        {
            var mediaHint = multimodal
                ? "Bạn có thể xem video đính kèm."
                : $"Không có video — chỉ dựa trên tên file: \"{fileLabel}\". Hãy suy đoán chủ đề hợp lý từ tên file.";

            string systemPrompt;
            switch ((platform ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "facebook":
                    systemPrompt =
                        "Bạn là chuyên gia chốt sale Affiliate trên Facebook Reels. " +
                        "Dựa vào nội dung/kịch bản sau, hãy viết một caption theo lối kể chuyện (storytelling) tự nhiên, thân thiện. " +
                        "Khéo léo chèn Call-to-Action kích thích người xem nhấp vào link Shopee ở phần bình luận hoặc tiểu sử để mua hàng " +
                        "(ví dụ: tham khảo gian hàng quần áo, đồ gia dụng...). " +
                        "Văn phong gần gũi, sử dụng icon hợp lý. " +
                        "Kết thúc bằng 2-3 hashtag rộng (broad hashtags). " +
                        "Trả về DUY NHẤT nội dung caption, không giải thích, không markdown.";
                    break;

                case "youtube":
                    systemPrompt =
                        "Bạn là chuyên gia SEO YouTube Shorts. " +
                        "Dựa vào nội dung/kịch bản sau, hãy trả về kết quả tuân thủ NGHIÊM NGẶT định dạng sau:\n" +
                        "Dòng 1: Tiêu đề video — giật tít, chứa từ khóa chính, độ dài tối đa 60 ký tự (không có nhãn hay dấu ngoặc).\n" +
                        "Dòng 2 trở đi: Mô tả video chi tiết, chứa nhiều từ khóa ngách liên quan (LSI keywords) để tối ưu tìm kiếm, " +
                        "kèm một Call-to-Action đăng ký kênh.\n" +
                        "Cuối cùng: 3-5 hashtag liên quan, bắt buộc phải có #shorts.\n" +
                        "Không thêm nhãn, tiêu đề phụ hay giải thích — chỉ trả về nội dung thuần.";
                    break;

                default: // TikTok
                    var styleVi = DescribeCaptionStyle(captionStyleKey);
                    systemPrompt =
                        "Bạn là một chuyên gia sáng tạo nội dung TikTok triệu view. " +
                        "Dựa vào nội dung/kịch bản được cung cấp, hãy viết một caption ngắn gọn, giật gân, " +
                        "tạo sự tò mò mạnh mẽ trong 2 giây đầu để giữ chân người xem. " +
                        "Phải có Call-to-Action kêu gọi thả tim hoặc bình luận. " +
                        "Cuối caption, cung cấp đúng 3-5 hashtag đang thịnh hành nhất liên quan đến nội dung, " +
                        "phân cách bằng dấu #. Không viết lan man. " +
                        "Phong cách: " + styleVi + " " +
                        "Trả về DUY NHẤT nội dung caption và hashtag, không giải thích, không markdown.";
                    break;
            }

            return systemPrompt + "\n\n" + mediaHint;
        }

        private static string DescribeCaptionStyle(string key)
        {
            var k = (key ?? string.Empty).Trim().ToLowerInvariant();
            if (k == "philosophy" || k.Contains("triết") || k.Contains("phil")) return "Triết lý — insight, ẩn dụ nhẹ, tone suy ngẫm, không sến.";
            if (k == "humor" || k.Contains("hài") || k.Contains("humor")) return "Hài hước — twist, chơi chữ vừa phải, phù hợp TikTok, không toxic.";
            if (k == "debate" || k.Contains("tranh") || k.Contains("hỏi")) return "Câu hỏi / gây tranh luận — mở đầu bằng câu hỏi hoặc ý kiến gây tương tác, không thù hận.";
            return "Kiến thức — fact/tip hữu ích, giọng chia sẻ chuyên gia nhẹ nhàng.";
        }

        private static string SanitizeCaptionOutput(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var text = raw.Trim();
            text = text.Trim('"', '\'', '`');
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var idx = text.LastIndexOf("```", StringComparison.Ordinal);
                if (idx > 3)
                {
                    text = text.Substring(3, idx - 3).Trim();
                }
            }

            return text;
        }

        private static async Task<string> SendGeminiJsonWithImageAsync(
            string prompt,
            string imagePath,
            string model,
            string apiKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken, jsonResponse: true)
                    .ConfigureAwait(false);
            }

            var fileInfo = new FileInfo(imagePath);
            if (fileInfo.Length > MaxInlineImageBytes)
            {
                return await SendGeminiRequestAsync(prompt, model, apiKey, cancellationToken, jsonResponse: true)
                    .ConfigureAwait(false);
            }

            await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
                var mime = GuessImageMime(imagePath);
                var bytes = await ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(bytes);

                var body = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = prompt },
                                new JObject
                                {
                                    ["inline_data"] = new JObject
                                    {
                                        ["mime_type"] = mime,
                                        ["data"] = b64
                                    }
                                })
                        }),
                    ["generationConfig"] = new JObject
                    {
                        ["responseMimeType"] = "application/json"
                    }
                };

                var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/" + safeModel + ":generateContent";
                var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(3));
                var request = new RestRequest(string.Empty, Method.Post);
                request.AddHeader("Content-Type", "application/json");
                request.AddQueryParameter("key", apiKey);
                request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var json = JObject.Parse(response.Content ?? "{}");
                return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                       ?? string.Empty;
            }
            finally
            {
                await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                _geminiThrottle.Release();
            }
        }

        private static async Task<string> SendGeminiRequestAsync(
            string prompt,
            string model,
            string apiKey,
            CancellationToken cancellationToken,
            bool jsonResponse = false)
        {
            var safeModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
            var apiClient = new ApiClient(endpoint);

            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddQueryParameter("key", apiKey);
            if (jsonResponse)
            {
                request.AddJsonBody(new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json"
                    }
                });
            }
            else
            {
                request.AddJsonBody(new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                });
            }

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                   ?? string.Empty;
        }

        private static async Task<string> SendClaudeRequestAsync(string prompt, string model, string apiKey, CancellationToken cancellationToken)
        {
            var safeModel = string.IsNullOrWhiteSpace(model) ? "claude-3-7-sonnet-latest" : model.Trim();
            var apiClient = new ApiClient("https://api.anthropic.com/v1/messages");

            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("x-api-key", apiKey);
            request.AddHeader("anthropic-version", "2023-06-01");
            request.AddJsonBody(new
            {
                model = safeModel,
                max_tokens = 900,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            });

            var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = JObject.Parse(response.Content ?? "{}");
            return json["content"]?[0]?["text"]?.ToString()
                   ?? string.Empty;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  ALL-CHANNELS CAPTION  (single AI call → JSON → TikTok + Facebook + YouTube)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates captions for all three platforms in a single AI call.
        /// Uses a dynamic system prompt that injects <paramref name="profileName"/> and
        /// <paramref name="productName"/> so AI applies channel branding and removes
        /// competitor references automatically.
        /// </summary>
        public async Task<OmnichannelCaptionResult> GenerateAllChannelsCaptionAsync(
            string videoPath,
            string profileName,
            string productName,
            string provider,
            string apiKey,
            string model = "gemini-2.0-flash",
            CancellationToken cancellationToken = default,
            IReadOnlyList<string> recentCaptions = null)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                throw new FileNotFoundException("Video file not found.", videoPath);

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("AI provider API key is required.");

            var safeProfile = (profileName ?? "kênh của tôi").Trim();
            var safeProduct = (productName  ?? "sản phẩm").Trim();
            var safeModel   = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();

            // ── Compress video to a small clip before sending to Gemini ──────────
            // This keeps the payload small (< 8 MB) for any source video while
            // preserving enough content for Gemini to understand the topic.
            string compressedPath = null;
            string sendPath       = videoPath;
            try
            {
                compressedPath = await CompressVideoForGeminiAsync(videoPath, cancellationToken)
                    .ConfigureAwait(false);
                sendPath = compressedPath;
            }
            catch
            {
                // If FFmpeg is unavailable fall back to original file (may be slow for large files)
                sendPath = videoPath;
            }

            try
            {
                // ── Multimodal path (always) ──────────────────────────────────────
                await _geminiThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var mime   = GuessVideoMime(sendPath);
                    var bytes  = await ReadAllBytesAsync(sendPath, cancellationToken).ConfigureAwait(false);
                    var b64    = Convert.ToBase64String(bytes);
                    var prompt = BuildAllChannelsPrompt(safeProfile, safeProduct,
                                     Path.GetFileName(videoPath), multimodal: true, recentCaptions);

                    var body = new JObject
                    {
                        ["contents"] = new JArray(
                            new JObject
                            {
                                ["role"] = "user",
                                ["parts"] = new JArray(
                                    new JObject { ["text"] = prompt },
                                    new JObject
                                    {
                                        ["inline_data"] = new JObject
                                        {
                                            ["mime_type"] = mime,
                                            ["data"]      = b64
                                        }
                                    })
                            }),
                        ["generationConfig"] = new JObject
                        {
                            ["responseMimeType"] = "application/json"
                        }
                    };

                    var endpoint  = $"https://generativelanguage.googleapis.com/v1beta/models/{safeModel}:generateContent";
                    var apiClient = new ApiClient(endpoint, TimeSpan.FromMinutes(4));
                    var request   = new RestRequest(string.Empty, Method.Post);
                    request.AddHeader("Content-Type", "application/json");
                    request.AddQueryParameter("key", apiKey);
                    request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), DataFormat.Json);

                    var response = await apiClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    var json     = JObject.Parse(response.Content ?? "{}");
                    var text     = json["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                                   ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var result = PostFilterCaptions(ParseAllChannelsJson(text));
                        if (result.IsValid) return result;
                    }

                    // Gemini returned something but JSON was malformed — retry text-only as last resort
                    var fallbackPrompt = BuildAllChannelsPrompt(safeProfile, safeProduct,
                                             Path.GetFileName(videoPath), multimodal: false, recentCaptions);
                    var fallbackRaw    = await GenerateScriptAsync(fallbackPrompt, "gemini", apiKey, safeModel,
                                             cancellationToken).ConfigureAwait(false);
                    return PostFilterCaptions(ParseAllChannelsJson(fallbackRaw));
                }
                finally
                {
                    await Task.Delay(4000, CancellationToken.None).ConfigureAwait(false);
                    _geminiThrottle.Release();
                }
            }
            finally
            {
                // Always clean up the compressed temp file
                if (!string.IsNullOrEmpty(compressedPath) && File.Exists(compressedPath))
                {
                    try { File.Delete(compressedPath); } catch { /* non-critical */ }
                }
            }
        }

        /// <summary>
        /// FFmpeg → clip nhỏ cho Gemini inline upload (144p, bitrate thấp).
        /// preservePlaybackSpeed=true: giữ tốc độ/thời lượng gốc (Showcase thoại).
        /// </summary>
        private static async Task<string> CompressVideoForGeminiAsync(
            string sourceVideoPath,
            CancellationToken cancellationToken,
            bool preservePlaybackSpeed = false)
        {
            var tmpName = Path.GetFileNameWithoutExtension(sourceVideoPath)
                          + "_gemini_" + System.Guid.NewGuid().ToString("N").Substring(0, 6) + ".mp4";
            var outPath = Path.Combine(Path.GetTempPath(), tmpName);

            string args;
            if (preservePlaybackSpeed)
            {
                args = $"-y -i \"{sourceVideoPath}\" " +
                       $"-vf \"scale=-2:144\" " +
                       $"-c:v libx264 -preset ultrafast -crf 35 -b:v 150k " +
                       $"-c:a aac -b:a 32k -ac 1 " +
                       $"-map_metadata -1 " +
                       $"\"{outPath}\"";
            }
            else
            {
                args = $"-y -i \"{sourceVideoPath}\" " +
                       $"-vf \"scale=-2:144,setpts=0.5*PTS\" " +
                       $"-af \"atempo=2.0\" " +
                       $"-c:v libx264 -preset ultrafast -crf 35 -b:v 150k " +
                       $"-c:a aac -b:a 32k -ac 1 " +
                       $"-map_metadata -1 " +
                       $"\"{outPath}\"";
            }

            var ffmpeg = ResolveFfmpegPath();
            var psi    = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = ffmpeg,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
                CreateNoWindow         = true
            };

            using (var proc = new System.Diagnostics.Process { StartInfo = psi })
            {
                proc.Start();
                var errTask = proc.StandardError.ReadToEndAsync();
                var outTask = proc.StandardOutput.ReadToEndAsync();

                // Up to 5 min for a long video (ultrafast preset is very quick)
                var waitTask = Task.Run(() => proc.WaitForExit(300_000));
                await Task.WhenAny(waitTask, Task.Delay(305_000, cancellationToken)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (!proc.HasExited) proc.Kill();
                await errTask.ConfigureAwait(false);
                await outTask.ConfigureAwait(false);
            }

            if (!File.Exists(outPath) || new FileInfo(outPath).Length < 5000)
                throw new InvalidOperationException("FFmpeg compression produced no output.");

            return outPath;
        }

        private static string ResolveFfmpegPath()
        {
            try
            {
                var settings = new ConfigManager().LoadAsync().GetAwaiter().GetResult();
                var p = (settings?.FfmpegPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
            }
            catch { }
            return "ffmpeg";
        }

        // ── Forbidden "salesy" words that TikTok flags as spam ───────────────────
        private static readonly (string Pattern, string Replacement)[] _captionForbiddenWords =
        {
            (@"\bgiá\s*rẻ\b",            "giá tốt"),
            (@"\bgiá\s*sốc\b",           "giá ổn"),
            (@"\bgiá\s*cực\s*rẻ\b",      "giá vừa phải"),
            (@"\bmua\s*ngay\b",           "tham khảo ngay"),
            (@"\bchốt\s*đơn\b",          "chốt trải nghiệm"),
            (@"\bliên\s*hệ\s*ngay\b",    "xem link trong Bio"),
            (@"\binbox\b",               "nhắn tin hỏi mình"),
            (@"\border\b",               "đặt thử"),
            (@"\bhttps?://\S+",          "xem link trong Bio"),  // strip any URL
            (@"\bwww\.\S+",              "xem link trong Bio"),
        };

        /// <summary>
        /// Post-process: strip forbidden spam/sales words and URLs from every generated caption.
        /// </summary>
        private static OmnichannelCaptionResult PostFilterCaptions(OmnichannelCaptionResult result)
        {
            if (result == null) return result;

            result.TikTokStyles   = FilterStyleDict(result.TikTokStyles);
            result.FacebookStyles = FilterStyleDict(result.FacebookStyles);
            result.YouTubeStyles  = FilterStyleDict(result.YouTubeStyles);

            result.TikTok          = FilterCaption(result.TikTok);
            result.Facebook        = FilterCaption(result.Facebook);
            result.YouTubeTitle    = FilterCaption(result.YouTubeTitle);
            result.YouTubeDescription = FilterCaption(result.YouTubeDescription);

            return result;
        }

        private static Dictionary<string, string> FilterStyleDict(Dictionary<string, string> dict)
        {
            if (dict == null) return dict;
            var result = new Dictionary<string, string>(dict.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
                result[kv.Key] = FilterCaption(kv.Value);
            return result;
        }

        private static string FilterCaption(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var (pattern, replacement) in _captionForbiddenWords)
                text = System.Text.RegularExpressions.Regex.Replace(
                    text, pattern, replacement,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            return text;
        }

        private static string BuildAllChannelsPrompt(
            string profileName,
            string productName,
            string fileLabel,
            bool multimodal,
            IReadOnlyList<string> recentCaptions = null)
        {
            var mediaHint = multimodal
                ? "Bạn có thể xem video đính kèm để lấy nội dung kịch bản."
                : $"Không có video đính kèm — hãy suy đoán chủ đề từ tên file: \"{fileLabel}\".";

            // Build the "recently used captions" block for duplicate-avoidance
            var recentBlock = string.Empty;
            if (recentCaptions != null && recentCaptions.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("DANH SÁCH CAPTION ĐÃ DÙNG GẦN ĐÂY (tuyệt đối không lặp lại cấu trúc câu, không dùng chung từ mở đầu với các dòng dưới đây):");
                var take = Math.Min(recentCaptions.Count, 10);
                for (int i = 0; i < take; i++)
                    sb.AppendLine($"  [{i + 1}] {recentCaptions[i]}");
                sb.AppendLine();
                recentBlock = sb.ToString();
            }

            return
                $"Bạn là một chuyên gia Content Marketing & SEO Affiliate hàng đầu, chuyên viết caption VIRAL cho mạng xã hội.\n" +
                $"Thông tin hiện tại:\n" +
                $"- Tên kênh (Profile): '{profileName}'\n" +
                $"- Sản phẩm đang bán: '{productName}'\n\n" +
                (recentBlock) +
                $"Nhiệm vụ: Viết cho mỗi nền tảng (TikTok, Facebook, YouTube) 5 phiên bản caption theo 5 PHONG CÁCH COPYWRITING khác nhau.\n" +
                "Mỗi phong cách phải có CẤU TRÚC CÂU VÀ TỪ MỞ ĐẦU HOÀN TOÀN KHÁC BIỆT — không được bắt đầu hai phong cách bằng cùng một từ/cụm từ.\n\n" +
                "BẢNG 5 PHONG CÁCH (áp dụng đúng tone từng phong cách):\n" +
                "  1. noi_dau   — Tone: khai thác vấn đề/nỗi lo người xem đang gặp. Từ khóa: \"bạn có đang...\", \"tại sao mãi không...\", \"lý do bí mật...\"\n" +
                "  2. boc_phot  — Tone: tiết lộ sự thật ẩn, gây shock nhẹ, kích thích tò mò. Từ khóa: \"sự thật là...\", \"không ai nói với bạn rằng...\", \"cảnh báo:\"\n" +
                "  3. huong_dan — Tone: thực tế, rõ ràng, cung cấp giá trị ngay. Từ khóa: \"cách\", \"bí quyết\", \"3 bước\", \"hướng dẫn\", \"mẹo\"\n" +
                "  4. fomo      — Tone: tạo sự cấp bách, sợ bỏ lỡ. Từ khóa: \"chỉ còn...\", \"đừng bỏ lỡ\", \"hôm nay thôi\", \"giới hạn\", \"ngay bây giờ\"\n" +
                "  5. ke_chuyen — Tone: kể chuyện cá nhân, chân thực, gần gũi. Từ khóa: \"hôm qua mình...\", \"câu chuyện của...\", \"lần đầu tiên mình...\"\n\n" +
                "QUY TẮC BẮT BUỘC — VI PHẠM MỘT TRONG CÁC QUY TẮC NÀY LÀ SAI HOÀN TOÀN:\n" +
                $"1. XÓA BỎ ĐỐI THỦ: Tuyệt đối không giữ lại tên shop/thương hiệu gốc hay số điện thoại từ kịch bản đầu vào.\n" +
                $"2. BRANDING: Luôn khéo léo nhắc đến '{profileName}' như địa chỉ uy tín để mua '{productName}'.\n" +
                "3. FACEBOOK: Dòng đầu tiên mỗi phiên bản Facebook PHẢI VIẾT HOA TOÀN BỘ (ALL CAPS).\n" +
                "4. ĐỘ DÀI (không tính hashtag): TikTok ~80–180 ký tự | Facebook ~100–210 ký tự | YouTube title ~50–70 ký tự | YouTube desc ~150–330 ký tự.\n" +
                "5. HASHTAG KHÔNG DẤU (bắt buộc): Viết không có dấu tiếng Việt.\n" +
                "   - TikTok: #[TênKênh] + #[TênSảnPhẩm] + #xuhuong + #review (3–5 tags).\n" +
                "   - Facebook: #[TênKênh] + #[TênSảnPhẩm] + #shopeehaul + #meovat (4–6 tags).\n" +
                "   - YouTube: #[TênKênh] + #[TênSảnPhẩm] + từ khóa dài + BẮT BUỘC #shorts cuối cùng.\n" +
                "6. YouTube: Mỗi phiên bản viết dạng \"TIÊU ĐỀ|||MÔ TẢ SEO\" (dấu ||| là dấu phân cách, không xuất hiện trong nội dung).\n" +
                "7. TUYỆT ĐỐI KHÔNG SPAM — CẤM các từ/cụm sau (sẽ bị TikTok phạt): \"giá rẻ\", \"mua ngay\", \"chốt đơn\", \"giá sốc\", \"giá cực rẻ\", \"liên hệ ngay\", \"inbox\", \"order\". " +
                "   Thay bằng ngôn ngữ chia sẻ trải nghiệm: \"mình dùng thử thấy...\", \"đồ này dùng ổn lắm\", \"trải nghiệm cá nhân của mình\".\n" +
                "8. TUYỆT ĐỐI KHÔNG URL: Không được đưa bất kỳ link http/https vào caption. Nếu cần dẫn nguồn, chỉ được ghi \"Xem link trong Bio\" hoặc \"Mã sản phẩm: [Code]\".\n" +
                "9. CHỐNG TRÙNG LẶP: Mỗi phong cách phải mở đầu bằng một từ/cụm từ hoàn toàn khác nhau. Không được dùng chung câu mở đầu với bất kỳ caption nào trong danh sách ĐÃ DÙNG ở trên (nếu có).\n\n" +
                mediaHint + "\n\n" +
                "Trả về JSON THUẦN (không dùng ```json, không text thừa) theo cấu trúc:\n" +
                "{\n" +
                "  \"tiktok\":   { \"noi_dau\": \"...\", \"boc_phot\": \"...\", \"huong_dan\": \"...\", \"fomo\": \"...\", \"ke_chuyen\": \"...\" },\n" +
                "  \"facebook\": { \"noi_dau\": \"...\", \"boc_phot\": \"...\", \"huong_dan\": \"...\", \"fomo\": \"...\", \"ke_chuyen\": \"...\" },\n" +
                "  \"youtube\":  { \"noi_dau\": \"...\", \"boc_phot\": \"...\", \"huong_dan\": \"...\", \"fomo\": \"...\", \"ke_chuyen\": \"...\" }\n" +
                "}\n" +
                "(Mỗi value trong youtube phải có dạng \"Tiêu đề|||Mô tả SEO + hashtag\")";
        }

        private static OmnichannelCaptionResult ParseAllChannelsJson(string raw)
        {
            var cleaned = SanitizeCaptionOutput(raw ?? string.Empty);

            // Strip markdown fences if AI ignored the instruction
            var fence = System.Text.RegularExpressions.Regex.Match(
                cleaned, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (fence.Success) cleaned = fence.Groups[1].Value.Trim();

            // Find outermost { ... } block
            var start = cleaned.IndexOf('{');
            var end   = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
                cleaned = cleaned.Substring(start, end - start + 1);

            try
            {
                var obj    = JObject.Parse(cleaned);
                var result = new OmnichannelCaptionResult();

                // ── New 5-style format ────────────────────────────────────────
                result.TikTokStyles   = ParseStyleBlock(obj["tiktok"]   as JObject);
                result.FacebookStyles = ParseStyleBlock(obj["facebook"] as JObject);
                result.YouTubeStyles  = ParseStyleBlock(obj["youtube"]  as JObject);

                // Populate legacy fields from first available style (fomo preferred, else first)
                var preferredKey = "fomo";
                if (result.TikTokStyles.Count > 0)
                    result.TikTok = result.TikTokStyles.ContainsKey(preferredKey)
                        ? result.TikTokStyles[preferredKey]
                        : result.TikTokStyles.Values.First();

                if (result.FacebookStyles.Count > 0)
                    result.Facebook = result.FacebookStyles.ContainsKey(preferredKey)
                        ? result.FacebookStyles[preferredKey]
                        : result.FacebookStyles.Values.First();

                if (result.YouTubeStyles.Count > 0)
                {
                    var ytRaw = result.YouTubeStyles.ContainsKey(preferredKey)
                        ? result.YouTubeStyles[preferredKey]
                        : result.YouTubeStyles.Values.First();
                    SplitYouTubeStyleValue(ytRaw, out var ytT, out var ytD);
                    result.YouTubeTitle       = ytT;
                    result.YouTubeDescription = ytD;
                }

                // ── Fallback: old flat format (backward compat) ───────────────
                if (!result.IsValid)
                {
                    result.TikTok   = (obj["tiktok"] as JValue)?.ToString()?.Trim() ?? string.Empty;
                    result.Facebook = (obj["facebook"] as JValue)?.ToString()?.Trim() ?? string.Empty;
                    var ytTitle = (obj["youtube_title"] ?? obj["youtubeTitle"] ?? obj["yt_title"])
                                  ?.ToString()?.Trim() ?? string.Empty;
                    result.YouTubeTitle       = ytTitle;
                    result.YouTubeDescription = (obj["youtube_desc"] ?? obj["youtubeDesc"] ?? obj["yt_desc"])
                                                 ?.ToString()?.Trim() ?? string.Empty;
                }

                return result;
            }
            catch
            {
                return new OmnichannelCaptionResult { TikTok = cleaned };
            }
        }

        /// <summary>Parses a {"noi_dau":"...","boc_phot":"...",...} JObject into a string dictionary.</summary>
        private static Dictionary<string, string> ParseStyleBlock(JObject block)
        {
            var d = new Dictionary<string, string>();
            if (block == null) return d;
            foreach (var key in OmnichannelCaptionResult.StyleKeys)
            {
                var val = block[key]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    d[key] = val;
            }
            return d;
        }

        /// <summary>Splits a YouTube style value "TITLE|||DESCRIPTION" into its two parts.</summary>
        public static void SplitYouTubeStyleValue(string value, out string title, out string description)
        {
            const string sep = "|||";
            var idx = (value ?? string.Empty).IndexOf(sep, StringComparison.Ordinal);
            if (idx >= 0)
            {
                title       = value.Substring(0, idx).Trim();
                description = value.Substring(idx + sep.Length).Trim();
            }
            else
            {
                // No separator — treat whole value as description, derive title from first line
                var lines = (value ?? string.Empty).Split(new[] { '\n' }, 2);
                title       = lines[0].Trim();
                description = lines.Length > 1 ? lines[1].Trim() : string.Empty;
            }
        }
    }

    /// <summary>Structured result returned by <see cref="GeminiService.GenerateAllChannelsCaptionAsync"/>.</summary>
    public sealed class OmnichannelCaptionResult
    {
        // ── Legacy single-value fields (kept for backward compat) ────────────
        public string TikTok             { get; set; } = string.Empty;
        public string Facebook           { get; set; } = string.Empty;
        public string YouTubeTitle       { get; set; } = string.Empty;
        public string YouTubeDescription { get; set; } = string.Empty;

        // ── 5-style variants per platform ────────────────────────────────────
        /// <summary>Key = style key ("noi_dau" | "boc_phot" | "huong_dan" | "fomo" | "ke_chuyen"), Value = full caption + hashtag string.</summary>
        public Dictionary<string, string> TikTokStyles   { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> FacebookStyles { get; set; } = new Dictionary<string, string>();
        /// <summary>YouTube: value is "TITLE|||DESCRIPTION" separated by |||.</summary>
        public Dictionary<string, string> YouTubeStyles  { get; set; } = new Dictionary<string, string>();

        /// <summary>True when at least one style variant was populated.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsValid =>
            TikTokStyles.Count > 0 || FacebookStyles.Count > 0 || YouTubeStyles.Count > 0 ||
            !string.IsNullOrWhiteSpace(TikTok) ||
            !string.IsNullOrWhiteSpace(Facebook) ||
            !string.IsNullOrWhiteSpace(YouTubeTitle);

        /// <summary>Human-readable display names for each style key.</summary>
        public static readonly Dictionary<string, string> StyleLabels = new Dictionary<string, string>
        {
            { "noi_dau",   "😔 Nỗi đau"   },
            { "boc_phot",  "🔥 Bóc phốt"  },
            { "huong_dan", "📖 Hướng dẫn" },
            { "fomo",      "⚡ FOMO"       },
            { "ke_chuyen", "💬 Kể chuyện" },
        };

        public static readonly string[] StyleKeys = { "noi_dau", "boc_phot", "huong_dan", "fomo", "ke_chuyen" };
    }
}
