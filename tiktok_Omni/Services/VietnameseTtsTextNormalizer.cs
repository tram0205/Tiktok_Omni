using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Chuẩn hóa lời thoại tiếng Việt trước ElevenLabs (dấu, dấu câu, nhịp nghỉ).</summary>
    public static class VietnameseTtsTextNormalizer
    {
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex VietnameseDiacriticRegex = new Regex(
            @"[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] CommaBeforeConjunctions =
        {
            "tuy nhiên",
            "thế nhưng",
            "bởi vì",
            "nhưng",
            "nên",
            "còn",
            "lại"
        };

        /// <summary>Làm sạch văn bản trước khi gửi ElevenLabs (đồng bộ + Gemini nếu cần).</summary>
        public static Task<string> PrepareForElevenLabsAsync(
            string rawText,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            return PrepareForOnlineTtsAsync(rawText, settings, log, cancellationToken);
        }

        private static async Task<string> PrepareForOnlineTtsAsync(
            string rawText,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var text = SanitizeForElevenLabsRequest(rawText);
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var needsGemini = ShouldRunGeminiCleanup(text, settings);
            if (!needsGemini)
            {
                return text;
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                if (LooksLikeMissingDiacritics(text))
                {
                    log?.Invoke("[TTS] Cảnh báo: văn bản có thể thiếu dấu — thêm AI API Key để tự sửa trước TTS.");
                }

                return text;
            }

            try
            {
                log?.Invoke("[TTS] Gemini: đang bổ sung dấu và ngắt câu cho TTS…");
                var gemini = new GeminiService();
                var prompt =
                    "Bạn là biên tập lời thoại TTS tiếng Việt.\r\n" +
                    "Nhiệm vụ: nhận văn bản sau và trả về DUY NHẤT bản đã chỉnh — không giải thích, không markdown, không ngoặc kép.\r\n" +
                    "Yêu cầu:\r\n" +
                    "1) Bổ sung đầy đủ dấu thanh đúng chính tả tiếng Việt (mọi từ tiếng Việt phải có dấu, không để chữ không dấu).\r\n" +
                    "2) Thêm dấu phẩy (,) và chấm (.) hợp lý để người đọc biết chỗ nghỉ ngắn và chỗ nhấn giọng.\r\n" +
                    "3) Giữ nguyên ý nghĩa, độ dài gần với bản gốc, văn phong tự nhiên như hook TikTok.\r\n" +
                    "4) KHÔNG thêm tag [excited] hay markdown — chỉ văn bản thuần.\r\n" +
                    "\r\nVăn bản:\r\n" + text;

                var fixedText = await gemini.GenerateScriptAsync(
                    prompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    cancellationToken).ConfigureAwait(false);

                var cleaned = SanitizeModelOutput(fixedText);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    text = NormalizePunctuation(cleaned);
                    log?.Invoke("[TTS] Gemini: «" + text + "»");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[TTS] Làm sạch Gemini bỏ qua: " + ex.Message);
            }

            return text;
        }

        /// <summary>Chuẩn hóa script thuyết minh trước ElevenLabs — chỉ dấu/câu, giữ nguyên từ ngữ.</summary>
        public static async Task<string> PrepareNarrationForElevenLabsAsync(
            string rawText,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var text = CollapseNarrationWhitespace(rawText);
            text = NormalizePunctuation(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (!ShouldRunNarrationGeminiCleanup(text, settings))
            {
                return text;
            }

            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                if (LooksLikeMissingDiacritics(text))
                {
                    log?.Invoke("[TTS] Cảnh báo: script thuyết minh có thể thiếu dấu — thêm AI API Key để tự sửa trước ElevenLabs.");
                }

                return text;
            }

            try
            {
                log?.Invoke("[TTS] Gemini: đang bổ sung dấu và ngắt câu cho script thuyết minh…");
                var gemini = new GeminiService();
                var prompt =
                    "Bạn là biên tập lời thuyết minh TTS tiếng Việt (ElevenLabs).\r\n" +
                    "Nhiệm vụ: nhận kịch bản sau và trả về DUY NHẤT bản đã chỉnh — không giải thích, không markdown, không ngoặc kép.\r\n" +
                    "Yêu cầu:\r\n" +
                    "1) Sửa đầy đủ dấu thanh tiếng Việt chuẩn (không để chữ không dấu hoặc sai dấu).\r\n" +
                    "2) Thêm dấu phẩy, chấm hợp lý để người đọc biết chỗ nghỉ.\r\n" +
                    "3) GIỮ NGUYÊN từ ngữ, cách diễn đạt và văn phong — không thay từ, không rút gọn, không paraphrase.\r\n\r\n" +
                    "Kịch bản:\r\n" + text;

                var fixedText = await gemini.GenerateScriptAsync(
                    prompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    cancellationToken).ConfigureAwait(false);

                var cleaned = SanitizeNarrationModelOutput(fixedText);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    text = NormalizePunctuation(cleaned);
                    log?.Invoke("[TTS] Script sau chỉnh dấu/câu: «" + TrimForLog(text, 160) + "»");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[TTS] Chỉnh dấu script bỏ qua: " + ex.Message);
            }

            return text;
        }

        private static bool ShouldRunNarrationGeminiCleanup(string text, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                return false;
            }

            if (LooksLikeMissingDiacritics(text))
            {
                return true;
            }

            return text.Length >= 40 && text.IndexOf(',') < 0;
        }

        private static string CollapseNarrationWhitespace(string raw)
        {
            var s = (raw ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return WhitespaceRegex.Replace(s, " ").Trim();
        }

        private static string TrimForLog(string text, int max)
        {
            var t = (text ?? string.Empty).Trim();
            return t.Length <= max ? t : t.Substring(0, max - 1) + "…";
        }

        private static string SanitizeNarrationModelOutput(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
                if (end > fence)
                {
                    s = s.Substring(fence + 3, end - fence - 3).Trim();
                    if (s.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    {
                        s = s.Substring(4).TrimStart();
                    }
                }
            }

            s = s.Trim().Trim('"', '\'', '“', '”', '‘', '’');
            return CollapseNarrationWhitespace(s);
        }

        /// <summary>Làm sạch encoding/ký tự lạ ngay trước khi gửi ElevenLabs (UTF-8 NFC + dấu câu).</summary>
        public static string SanitizeForElevenLabsRequest(string rawText)
        {
            var s = (rawText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = s.Trim('\uFEFF');
            s = Regex.Replace(s, @"[\u200B-\u200D\u2060\uFEFF]", string.Empty);
            s = Regex.Replace(s, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]", string.Empty);
            s = s.Replace('\u2018', '\'').Replace('\u2019', '\'')
                .Replace('\u201C', '"').Replace('\u201D', '"')
                .Replace('\u00A0', ' ');
            s = Regex.Replace(s, @"[`*_#~\[\]<>|\\]", " ");
            s = WhitespaceRegex.Replace(s, " ").Trim();
            s = s.Normalize(NormalizationForm.FormC);
            return NormalizePunctuation(s);
        }

        /// <summary>Chuẩn hóa khoảng trắng và dấu câu (không gọi API).</summary>
        public static string NormalizePunctuation(string rawText) =>
            NormalizePunctuation(rawText, injectCommaHeuristics: true);

        /// <summary>Quote triết lý / text người dùng đã sửa — giữ dấu câu tay, không tự chèn phẩy.</summary>
        public static string NormalizeQuotePunctuation(string rawText) =>
            NormalizePunctuation(rawText, injectCommaHeuristics: false);

        private static string NormalizePunctuation(string rawText, bool injectCommaHeuristics)
        {
            var s = (rawText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = s.Normalize(NormalizationForm.FormC);
            s = WhitespaceRegex.Replace(s, " ");
            s = CollapseSpacedPeriodEllipsis(s);
            s = Regex.Replace(s, @"\.{4,}", "...");
            s = Regex.Replace(s, @"\.{3}", "\uE001");
            s = s.Replace('…', '\uE001');

            s = Regex.Replace(s, @"\s+([,\.;:!?])", "$1");
            s = Regex.Replace(s, @"([,\.;:!?])([^\s""',.;:!?…])", "$1 $2");
            s = Regex.Replace(s, @",{2,}", ",");

            if (injectCommaHeuristics)
            {
                foreach (var conj in CommaBeforeConjunctions)
                {
                    var pattern = @"(\S+)\s+(" + Regex.Escape(conj) + @")(\s+)";
                    s = Regex.Replace(
                        s,
                        pattern,
                        m => EndsWithClausePunctuation(m.Groups[1].Value)
                            ? m.Groups[1].Value + " " + m.Groups[2].Value + m.Groups[3].Value
                            : m.Groups[1].Value + ", " + m.Groups[2].Value + m.Groups[3].Value,
                        RegexOptions.IgnoreCase);
                }
            }

            s = s.Replace("\uE001", "...");
            s = CollapseSpacedPeriodEllipsis(s);
            s = Regex.Replace(s, @"\s+", " ").Trim();
            if (!Regex.IsMatch(s, @"[\.!\?…]$"))
            {
                s = s.TrimEnd(',', ';', ':', ' ') + ".";
            }

            return s;
        }

        /// <summary>Gom «. .» / «..» / «...» về một dấu chấm (quote triết lý).</summary>
        public static string FixSpacedPeriods(string text)
        {
            var s = CollapseSpacedPeriodArtifacts(text);
            s = Regex.Replace(s, @"\.{2,}", ".");
            return s.Replace('…', '.');
        }

        /// <summary>Chỉ sửa chấm bị tách khoảng trắng «. .» — không đụng «...» viết liền.</summary>
        private static string CollapseSpacedPeriodEllipsis(string text) => CollapseSpacedPeriodArtifacts(text);

        private static string CollapseSpacedPeriodArtifacts(string text)
        {
            var s = text ?? string.Empty;
            s = Regex.Replace(s, @"(?:\.\s+){2,}\.", ".");
            s = Regex.Replace(s, @"\.\s+\.", ".");
            return s;
        }

        public static bool LooksLikeMissingDiacritics(string text)
        {
            var s = (text ?? string.Empty).Trim();
            if (s.Length < 4)
            {
                return false;
            }

            var letterCount = 0;
            var diacriticCount = 0;
            foreach (var ch in s)
            {
                if (!char.IsLetter(ch))
                {
                    continue;
                }

                letterCount++;
                if (VietnameseDiacriticRegex.IsMatch(ch.ToString()))
                {
                    diacriticCount++;
                }
            }

            if (letterCount < 4)
            {
                return false;
            }

            if (diacriticCount >= Math.Max(2, letterCount / 4))
            {
                return false;
            }

            return Regex.IsMatch(s, @"[A-Za-z]{3,}");
        }

        private static bool ShouldRunGeminiCleanup(string text, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                return false;
            }

            if (LooksLikeMissingDiacritics(text))
            {
                return true;
            }

            return text.Length >= 18 && text.IndexOf(',') < 0;
        }

        private static readonly Regex UnaccentedTokenRegex = new Regex(
            @"\b[A-Za-z]{3,}\b",
            RegexOptions.Compiled);

        private static bool HasUnaccentedVietnameseTokens(string text)
        {
            var s = (text ?? string.Empty).Trim();
            if (s.Length < 6)
            {
                return false;
            }

            var hasDiacritic = VietnameseDiacriticRegex.IsMatch(s);
            if (!hasDiacritic)
            {
                return LooksLikeMissingDiacritics(s);
            }

            foreach (Match m in UnaccentedTokenRegex.Matches(s))
            {
                var token = m.Value;
                if (token.Length < 4)
                {
                    continue;
                }

                if (!VietnameseDiacriticRegex.IsMatch(token))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EndsWithClausePunctuation(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var last = token[token.Length - 1];
            return last == ',' || last == '.' || last == '!' || last == '?' || last == '…' || last == ';' || last == ':';
        }

        private static string SanitizeModelOutput(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
                if (end > fence)
                {
                    s = s.Substring(fence + 3, end - fence - 3).Trim();
                }
            }

            s = s.Trim().Trim('"', '\'', '“', '”', '‘', '’');
            var lineBreak = s.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0)
            {
                s = s.Substring(0, lineBreak).Trim();
            }

            return s;
        }
    }
}
