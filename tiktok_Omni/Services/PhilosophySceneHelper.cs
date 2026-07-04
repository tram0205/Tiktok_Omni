using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Tách phân cảnh, đặt tên file quy ước, gọi Gemini sinh prompt, xuất CSV.</summary>
    public static class PhilosophySceneHelper
    {
        private const double SecondsPerWord = 0.4;

        /// <summary>Mỗi clip video AI mục tiêu ~8 giây.</summary>
        public const double TargetSceneDurationSeconds = 8.0;

        private const double MinSceneDurationSeconds = 7.0;

        private const double MaxSceneDurationSeconds = 9.0;

        // ─── Tách phân cảnh ──────────────────────────────────────────────────

        /// <summary>Ước lượng thời lượng đọc (giây) từ số từ tiếng Việt (0.4s/từ).</summary>
        public static double EstimateDuration(string text)
        {
            var words = CountWords(text);
            return words * SecondsPerWord;
        }

        /// <summary>Số phân cảnh lý tưởng để mỗi clip ~8 giây đọc.</summary>
        public static int EstimateSceneCount(string quote)
        {
            var totalWords = CountWords(quote);
            if (totalWords == 0)
            {
                return 0;
            }

            var totalDuration = totalWords * SecondsPerWord;
            return Math.Max(1, (int)Math.Ceiling(totalDuration / TargetSceneDurationSeconds));
        }

        public static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return text.Trim().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>
        /// Chia câu triết lý thành phân cảnh — mỗi cảnh ~8 giây đọc (0.4s/từ).
        /// Số cảnh = ceil(tổng_thời_lượng / 8). Ưu tiên cắt tại dấu câu.
        /// </summary>
        public static List<PhilosophySceneItem> SplitIntoScenes(string quote, string imagePromptJson = null)
        {
            var totalWords = CountWords(quote);
            if (totalWords == 0)
            {
                return new List<PhilosophySceneItem>();
            }

            var sceneCount = EstimateSceneCount(quote);
            var targetWordsPerScene = Math.Max(1, (int)Math.Round((double)totalWords / sceneCount));
            var minWords = Math.Max(1, (int)Math.Floor(MinSceneDurationSeconds / SecondsPerWord));
            var maxWords = Math.Max(minWords, (int)Math.Ceiling(MaxSceneDurationSeconds / SecondsPerWord));

            var sentences = SplitAtPunctuation(quote);
            var scenes = new List<PhilosophySceneItem>();
            var buffer = new List<string>();

            void Flush()
            {
                if (buffer.Count == 0)
                {
                    return;
                }

                var text = string.Join(" ", buffer).Trim();
                scenes.Add(new PhilosophySceneItem
                {
                    SceneIndex = scenes.Count + 1,
                    SceneText = text,
                    EstimatedDurationSeconds = EstimateDuration(text),
                    ParentQuote = quote
                });
                buffer.Clear();
            }

            foreach (var sentence in sentences)
            {
                var words = sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var wordIdx = 0;
                while (wordIdx < words.Length)
                {
                    var remainingScenes = Math.Max(1, sceneCount - scenes.Count);
                    var remainingWords = totalWords - scenes.Sum(s => CountWords(s.SceneText)) - buffer.Count;
                    var dynamicTarget = remainingScenes > 0
                        ? Math.Max(minWords, (int)Math.Round((double)remainingWords / remainingScenes))
                        : targetWordsPerScene;
                    dynamicTarget = Math.Min(maxWords, Math.Max(minWords, dynamicTarget));

                    var take = Math.Min(words.Length - wordIdx, dynamicTarget - buffer.Count);
                    if (take <= 0)
                    {
                        take = Math.Min(words.Length - wordIdx, maxWords - buffer.Count);
                    }

                    if (take <= 0)
                    {
                        Flush();
                        take = Math.Min(words.Length - wordIdx, dynamicTarget);
                    }

                    for (var i = 0; i < take && wordIdx < words.Length; i++)
                    {
                        buffer.Add(words[wordIdx++]);
                    }

                    var bufferWords = buffer.Count;
                    var atSentenceEnd = wordIdx >= words.Length;
                    var reachedTarget = bufferWords >= dynamicTarget;
                    var reachedMax = bufferWords >= maxWords;
                    var hasMinAndEnd = bufferWords >= minWords && atSentenceEnd;

                    if (reachedMax || (reachedTarget && atSentenceEnd) || hasMinAndEnd)
                    {
                        Flush();
                    }
                }
            }

            Flush();

            // Gộp cảnh cuối nếu quá ngắn (&lt; 5 giây) vào cảnh trước
            if (scenes.Count >= 2)
            {
                var last = scenes[scenes.Count - 1];
                if (last.EstimatedDurationSeconds < 5.0)
                {
                    var prev = scenes[scenes.Count - 2];
                    prev.SceneText = (prev.SceneText + " " + last.SceneText).Trim();
                    prev.EstimatedDurationSeconds = EstimateDuration(prev.SceneText);
                    scenes.RemoveAt(scenes.Count - 1);
                }
            }

            // Re-index
            for (var i = 0; i < scenes.Count; i++)
            {
                scenes[i].SceneIndex = i + 1;
            }

            var prefix = BuildFilePrefix(quote);
            for (var i = 0; i < scenes.Count; i++)
            {
                scenes[i].ConventionFileName = prefix + "-" + (i + 1).ToString("D2") + ".mp4";
            }

            return scenes;
        }

        // ─── Đặt tên file quy ước ────────────────────────────────────────────

        /// <summary>Lấy 5 từ đầu, chuyển thành dạng viet-lien-khong-dau.</summary>
        public static string BuildFilePrefix(string quote)
        {
            var words = (quote ?? string.Empty)
                .Trim()
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(5)
                .Select(RemoveDiacritics)
                .Select(w => Regex.Replace(w.ToLowerInvariant(), @"[^a-z0-9]", string.Empty))
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            return words.Count > 0 ? string.Join("-", words) : "scene";
        }

        /// <summary>Khớp tên file trong thư mục theo prefix quy ước của câu triết lý.</summary>
        public static List<string> FindPreRenderedVideoFiles(string folder, string quote)
        {
            if (!Directory.Exists(folder))
            {
                return new List<string>();
            }

            var prefix = BuildFilePrefix(quote);
            return Directory.GetFiles(folder, "*.mp4", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Kiểm tra dãy file có đủ liên tục (01, 02, 03…) không. Trả lại danh sách số thiếu.</summary>
        public static List<int> FindMissingSceneNumbers(List<string> files, int expectedCount)
        {
            var presentNumbers = files
                .Select(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    var match = Regex.Match(name, @"-(\d{2})$");
                    return match.Success ? (int?)int.Parse(match.Groups[1].Value) : null;
                })
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToHashSet();

            var missing = new List<int>();
            for (var i = 1; i <= expectedCount; i++)
            {
                if (!presentNumbers.Contains(i))
                {
                    missing.Add(i);
                }
            }

            return missing;
        }

        // ─── Gọi Gemini sinh prompt hình ảnh ─────────────────────────────────

        /// <summary>
        /// Gọi Gemini sinh prompt video chuẩn (bối cảnh + tâm lý nhân vật + chuyển động/camera).
        /// Nếu có <paramref name="referenceImagePath"/> (mode 3), Gemini phân tích nhân vật trong ảnh.
        /// </summary>
        public static async Task<List<PhilosophySceneItem>> GenerateScenePromptsAsync(
            string quote,
            List<PhilosophySceneItem> scenes,
            AppSettings settings,
            CancellationToken cancellationToken,
            string referenceImagePath = null,
            string mood = null)
        {
            if (scenes == null || scenes.Count == 0)
            {
                scenes = SplitIntoScenes(quote);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are an expert AI video prompt engineer for philosophical TikTok/Reels.");
            promptBuilder.AppendLine("Format: vertical 9:16, 720p. Each clip is exactly ~" +
                TargetSceneDurationSeconds.ToString("0", CultureInfo.InvariantCulture) +
                " seconds. Target tools: Veo, Kling, Fal.ai (text-to-video / image-to-video).");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("FULL VIETNAMESE QUOTE (overall meaning must guide every scene):");
            promptBuilder.AppendLine("\"" + quote + "\"");

            var moodText = (mood ?? "reflective").Trim().ToLowerInvariant();
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Overall mood/tone: " + moodText + ".");

            var imagePath = (referenceImagePath ?? string.Empty).Trim();
            var hasReferenceImage = !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath);
            if (hasReferenceImage)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("=== REFERENCE IMAGE ATTACHED — VISUAL STYLE LOCK (NON-NEGOTIABLE) ===");
                promptBuilder.AppendLine("STEP 1 — Analyze the reference image and classify its EXACT visual medium:");
                promptBuilder.AppendLine("  • photorealistic live-action (real photo of real humans)");
                promptBuilder.AppendLine("  • 2D digital illustration / storybook / hand-drawn art");
                promptBuilder.AppendLine("  • anime / manga / cel-shaded");
                promptBuilder.AppendLine("  • 3D CGI / Pixar-style render");
                promptBuilder.AppendLine("  • watercolor / oil painting / sketch");
                promptBuilder.AppendLine("  • other (name it precisely)");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("STEP 2 — LOCK that medium for ALL scene prompts:");
                promptBuilder.AppendLine("  • If reference is ILLUSTRATION/DRAWING → every prompt MUST say \"2D illustration\", \"digital painting\", \"hand-drawn style\"");
                promptBuilder.AppendLine("    and MUST include \"NOT photorealistic\", \"NOT live-action\", \"NOT real human photography\".");
                promptBuilder.AppendLine("  • If reference is PHOTOREALISTIC → every prompt MUST say \"photorealistic\", \"live-action\", \"real human\"");
                promptBuilder.AppendLine("    and MUST include \"NOT illustration\", \"NOT cartoon\", \"NOT anime\", \"NOT drawing\".");
                promptBuilder.AppendLine("  • NEVER convert illustration to live-action or vice versa.");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("STEP 3 — CHARACTER IDENTITY LOCK:");
                promptBuilder.AppendLine("  • Describe each main character's face, hair, skin tone, age, clothing from the reference.");
                promptBuilder.AppendLine("  • The SAME face identity must appear in every scene (consistent features, hairstyle, outfit palette).");
                promptBuilder.AppendLine("  • You MAY change background/location, add or remove secondary characters, change pose/action —");
                promptBuilder.AppendLine("    but the primary character(s) must remain visually recognizable as the same person from the reference.");
                promptBuilder.AppendLine("  • Match color palette, line weight, shading style, and lighting mood from the reference.");
            }
            else
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("No reference image — use cinematic photorealistic live-action style unless quote implies otherwise.");
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("SCENE BREAKDOWN (" + scenes.Count + " clips × ~" +
                TargetSceneDurationSeconds.ToString("0", CultureInfo.InvariantCulture) + "s each):");
            for (var i = 0; i < scenes.Count; i++)
            {
                promptBuilder.AppendLine("Scene " + (i + 1) + " (~" +
                    scenes[i].EstimatedDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) +
                    "s read / " + TargetSceneDurationSeconds.ToString("0", CultureInfo.InvariantCulture) +
                    "s video) — spoken line: \"" + scenes[i].SceneText + "\"");
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("TASK: Write ONE English video prompt per scene (60-100 words).");
            promptBuilder.AppendLine("Each prompt MUST START with the locked visual medium label, then include in flowing prose:");
            promptBuilder.AppendLine("1) VISUAL MEDIUM — repeat the exact art style lock (e.g. \"2D digital illustration, warm storybook style, NOT photorealistic\");");
            promptBuilder.AppendLine("2) SETTING — place, time, atmosphere for THIS scene's spoken line;");
            promptBuilder.AppendLine("3) CHARACTER(S) — identity-locked faces/features matching reference" +
                (hasReferenceImage ? " (same person every scene)" : "") + ", wardrobe, posture;");
            promptBuilder.AppendLine("4) EMOTION/PSYCHOLOGY — inner state, expression, body language;");
            promptBuilder.AppendLine("5) MOTION — character action + subtle environment movement;");
            promptBuilder.AppendLine("6) CAMERA — shot type, slow movement, pacing for ~" +
                TargetSceneDurationSeconds.ToString("0", CultureInfo.InvariantCulture) + " seconds;");
            promptBuilder.AppendLine("7) LIGHTING — key light, color grade matching reference style.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Rules: vertical 9:16, NO on-screen text, NO logos, NO subtitles in frame.");
            promptBuilder.AppendLine("Output ONLY a numbered list — one prompt per scene starting with \"1. \", \"2. \", etc. No markdown.");

            var geminiService = new GeminiService();
            string raw;
            if (hasReferenceImage)
            {
                raw = await geminiService.GenerateScriptWithImageAsync(
                    promptBuilder.ToString(),
                    imagePath,
                    (settings.AiProvider ?? "Google").Trim(),
                    settings.AiApiKey,
                    string.IsNullOrWhiteSpace(settings.AiModel) ? "gemini-2.0-flash" : settings.AiModel,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                raw = await geminiService.GenerateScriptAsync(
                    promptBuilder.ToString(),
                    (settings.AiProvider ?? "Google").Trim(),
                    settings.AiApiKey,
                    string.IsNullOrWhiteSpace(settings.AiModel) ? "gemini-2.0-flash" : settings.AiModel,
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var prompts = ParseNumberedList(raw, scenes.Count);
            for (var i = 0; i < scenes.Count; i++)
            {
                scenes[i].ImagePromptEn = string.Empty;
                if (i < prompts.Count && !string.IsNullOrWhiteSpace(prompts[i]))
                {
                    scenes[i].ImagePromptEn = prompts[i].Trim();
                }
            }

            return scenes;
        }

        // ─── Xuất CSV ────────────────────────────────────────────────────────

        public static string GetScenePromptDisplayLabel(PhilosophyScriptItem item)
        {
            var count = item?.Scenes?.Count ?? 0;
            if (count == 0)
            {
                return "Xem prompt…";
            }

            var withPrompt = item.Scenes.Count(s => !string.IsNullOrWhiteSpace(s.ImagePromptEn));
            return count + " cảnh" + (withPrompt > 0 && withPrompt < count ? " (" + withPrompt + " prompt)" : string.Empty);
        }

        /// <summary>Xuất danh sách phân cảnh ra file CSV UTF-8 BOM, trả đường dẫn đã lưu.</summary>
        public static string ExportToCsv(
            string savePath,
            IEnumerable<PhilosophySceneItem> allScenes)
        {
            var csv = new StringBuilder();
            csv.AppendLine("STT,Câu Triết Lý,Phân Cảnh,Prompt Hình Ảnh AI,Tên File Video Quy Ước");

            var stt = 1;
            foreach (var scene in allScenes)
            {
                csv.AppendLine(string.Join(",",
                    stt++,
                    CsvEscape(scene.ParentQuote),
                    CsvEscape(scene.SceneText),
                    CsvEscape(scene.ImagePromptEn),
                    CsvEscape(scene.ConventionFileName)));
            }

            File.WriteAllText(savePath, csv.ToString(), new UTF8Encoding(true));
            return savePath;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        private static List<string> SplitAtPunctuation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var parts = Regex.Split(text.Trim(), @"(?<=[.,;:!?…—–\n])");
            var result = new List<string>();
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            if (result.Count == 0)
            {
                result.Add(text.Trim());
            }

            return result;
        }

        private static List<string> ParseNumberedList(string raw, int expectedCount)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var text = raw.Trim();
            var matches = Regex.Matches(text, @"(?m)^\s*(\d+)\.\s*", RegexOptions.Multiline);
            if (matches.Count == 0)
            {
                return result;
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index + matches[i].Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var body = text.Substring(start, end - start).Trim();
                body = Regex.Replace(body, @"^[\-\*]\s+", string.Empty);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    result.Add(body);
                }
            }

            return result;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Bảng chuyển đổi ký tự tiếng Việt thường gặp
            var map = new Dictionary<string, string>
            {
                {"à","a"},{"á","a"},{"ả","a"},{"ã","a"},{"ạ","a"},
                {"ă","a"},{"ắ","a"},{"ặ","a"},{"ằ","a"},{"ẳ","a"},{"ẵ","a"},
                {"â","a"},{"ấ","a"},{"ầ","a"},{"ẩ","a"},{"ẫ","a"},{"ậ","a"},
                {"è","e"},{"é","e"},{"ẻ","e"},{"ẽ","e"},{"ẹ","e"},
                {"ê","e"},{"ế","e"},{"ề","e"},{"ể","e"},{"ễ","e"},{"ệ","e"},
                {"ì","i"},{"í","i"},{"ỉ","i"},{"ĩ","i"},{"ị","i"},
                {"ò","o"},{"ó","o"},{"ỏ","o"},{"õ","o"},{"ọ","o"},
                {"ô","o"},{"ố","o"},{"ồ","o"},{"ổ","o"},{"ỗ","o"},{"ộ","o"},
                {"ơ","o"},{"ớ","o"},{"ờ","o"},{"ở","o"},{"ỡ","o"},{"ợ","o"},
                {"ù","u"},{"ú","u"},{"ủ","u"},{"ũ","u"},{"ụ","u"},
                {"ư","u"},{"ứ","u"},{"ừ","u"},{"ử","u"},{"ữ","u"},{"ự","u"},
                {"ỳ","y"},{"ý","y"},{"ỷ","y"},{"ỹ","y"},{"ỵ","y"},
                {"đ","d"},
                {"À","A"},{"Á","A"},{"Ả","A"},{"Ã","A"},{"Ạ","A"},
                {"Ă","A"},{"Ắ","A"},{"Ặ","A"},{"Ằ","A"},{"Ẳ","A"},{"Ẵ","A"},
                {"Â","A"},{"Ấ","A"},{"Ầ","A"},{"Ẩ","A"},{"Ẫ","A"},{"Ậ","A"},
                {"È","E"},{"É","E"},{"Ẻ","E"},{"Ẽ","E"},{"Ẹ","E"},
                {"Ê","E"},{"Ế","E"},{"Ề","E"},{"Ể","E"},{"Ễ","E"},{"Ệ","E"},
                {"Ì","I"},{"Í","I"},{"Ỉ","I"},{"Ĩ","I"},{"Ị","I"},
                {"Ò","O"},{"Ó","O"},{"Ỏ","O"},{"Õ","O"},{"Ọ","O"},
                {"Ô","O"},{"Ố","O"},{"Ồ","O"},{"Ổ","O"},{"Ỗ","O"},{"Ộ","O"},
                {"Ơ","O"},{"Ớ","O"},{"Ờ","O"},{"Ở","O"},{"Ỡ","O"},{"Ợ","O"},
                {"Ù","U"},{"Ú","U"},{"Ủ","U"},{"Ũ","U"},{"Ụ","U"},
                {"Ư","U"},{"Ứ","U"},{"Ừ","U"},{"Ử","U"},{"Ữ","U"},{"Ự","U"},
                {"Ỳ","Y"},{"Ý","Y"},{"Ỷ","Y"},{"Ỹ","Y"},{"Ỵ","Y"},
                {"Đ","D"}
            };

            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                var chStr = ch.ToString();
                sb.Append(map.TryGetValue(chStr, out var replacement) ? replacement : chStr);
            }

            return sb.ToString();
        }
    }
}
