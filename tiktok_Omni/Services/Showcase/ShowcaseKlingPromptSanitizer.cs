using System;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chuẩn hoá prompt Kling I2V — chuyển động vừa phải (sống động nhưng không phá áo dài/vải).</summary>
    public static class ShowcaseKlingPromptSanitizer
    {
        private const string PromptEndingSuffix = "smooth cinematic motion, no text overlay";

        private static readonly string[] FlowStylePrefixes =
        {
            "ON-MODEL fashion lookbook —",
            "ON-MODEL fashion lookbook -",
            "ON-MODEL fashion lookbook:",
            "FLATLAY product shot —",
            "FLATLAY product shot -",
            "FLATLAY product shot:"
        };

        private static readonly (string Pattern, string Replacement)[] RiskyReplacements =
        {
            (@"\bbeautiful\s+young\b", string.Empty),
            (@"\byoung\s+vietnamese\s+woman\b", "Vietnamese model"),
            (@"\byoung\s+woman\b", "model"),
            (@"\byoung\s+man\b", "model"),
            (@"\bbeautiful\b", string.Empty),
            (@"\bgorgeous\b", string.Empty),
            (@"\bstunning\b", string.Empty),
            (@"\bsexy\b", string.Empty),
            (@"\bcelebrity\b", string.Empty),
            (@"\bsupermodel\b", string.Empty),
            (@"\binfluencer\b", string.Empty),
            (@"\bstatic scene composition from photo\b", string.Empty),
            (@"\bproduct-focused outfit showcase\b", string.Empty),
            (@"\bproduct-focused\b", string.Empty)
        };

        private static readonly (string Pattern, string Replacement)[] AggressiveMotionSofteners =
        {
            (@"\bstrong\s+(?:breeze|wind|gust)\b", "soft breeze"),
            (@"\bheavy\s+(?:breeze|wind)\b", "gentle breeze"),
            (@"\bdramatic(?:ally)?\b", "gentle"),
            (@"\b(?:walks?|walking)\s+(?:slowly\s+)?(?:forward\s+)?through\b", "takes one slow step forward in"),
            (@"\b(?:twirl|spin|runway\s+walk)\b", "gentle fabric sway"),
            (@"\bhair\s+(?:flowing|blowing|flying)\b", "subtle hair movement"),
            (@"\b(?:fast|quick|rushing)\s+(?:walk|step)\b", "slow step"),
            (@"\bthree\s+slow\s+steps\b", "one slow step"),
            (@"\btwo\s+slow\s+steps\b", "one slow step with gentle fabric sway")
        };

        private static readonly string[] BalancedMotionBoosters =
        {
            "One slow step forward, dress panels and hem sway gently in soft breeze, very slow cinematic push-in",
            "Subtle weight shift with gentle fabric drape movement, soft natural body motion, slow push-in on outfit detail",
            "Slight shoulder turn, fabric panels ripple lightly, natural in-place rhythm, gentle cinematic motion"
        };

        public static string Sanitize(string rawPrompt, int sceneIndex)
        {
            var text = (rawPrompt ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return string.Empty;
            }

            text = ShowcasePromptTextHelper.StripDurationClauses(text);
            if (text.Length == 0)
            {
                return string.Empty;
            }

            text = StripFlowStylePrefixes(text);

            foreach (var (pattern, replacement) in RiskyReplacements)
            {
                text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
            }

            foreach (var (pattern, replacement) in AggressiveMotionSofteners)
            {
                text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
            }

            text = NormalizeGarmentDetailClaims(text);

            text = Regex.Replace(text, @"\s{2,}", " ").Trim();
            text = Regex.Replace(text, @"\s+([,.])", "$1");
            text = Regex.Replace(text, @"([,.])\s*([,.])", "$1");
            text = text.Trim(' ', ',', '.', ';', '—', '-');

            if (text.Length == 0)
            {
                return string.Empty;
            }

            if (!ContainsSameSettingHint(text))
            {
                text += ". Same setting as the photo, face and outfit stay consistent";
            }

            if (!ContainsMotionHint(text))
            {
                var booster = BalancedMotionBoosters[Math.Abs(sceneIndex) % BalancedMotionBoosters.Length];
                text += ". " + booster;
            }

            if (IsAoDaiLike(text) && !ContainsPreserveGarmentHint(text))
            {
                text += ". Preserve ao dai silhouette, mandarin collar and visible fabric details from the photo";
            }

            if (text.IndexOf("no text overlay", StringComparison.OrdinalIgnoreCase) < 0)
            {
                text += ". " + PromptEndingSuffix;
            }

            return text.Trim();
        }

        private static string StripFlowStylePrefixes(string text)
        {
            var result = text.Trim();
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var prefix in FlowStylePrefixes)
                {
                    if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        result = result.Substring(prefix.Length).TrimStart();
                        changed = true;
                    }
                }
            }

            return result;
        }

        private static bool ContainsSameSettingHint(string text)
        {
            return text.IndexOf("same setting", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("as the photo", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("as in the photo", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("stay consistent", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("unchanged from", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsMotionHint(string text)
        {
            return Regex.IsMatch(
                text,
                @"\b(?:sway|swaying|step|steps|shift|turn|turns|breeze|walk|walking|movement|push-in|dolly|ripple|drape|panels?\s+(?:sway|ripple)|fabric\s+motion|body\s+motion|weight\s+shift)\b",
                RegexOptions.IgnoreCase);
        }

        private static bool IsAoDaiLike(string text)
        {
            return text.IndexOf("ao dai", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("ao-dai", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("mandarin collar", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("dress panels", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsPreserveGarmentHint(string text)
        {
            return text.IndexOf("preserve", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("visible fabric details", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("from the photo", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("hem shape", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Gỡ embroidery generic khi prompt không mô tả thêu rõ — tránh Kling bịa hoa văn.</summary>
        private static string NormalizeGarmentDetailClaims(string text)
        {
            if (HasExplicitEmbroideryInPhoto(text))
            {
                return text;
            }

            var result = text;
            result = Regex.Replace(
                result,
                @"\bpreserve ao dai shape,?\s*collar and embroidery detail\b",
                "preserve ao dai silhouette, mandarin collar and visible fabric details from the photo",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                @"\bpreserve ao dai shape,?\s*mandarin collar and embroidery detail\b",
                "preserve ao dai silhouette, mandarin collar and visible fabric details from the photo",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                @"\bcollar and embroidery detail\b",
                "mandarin collar and smooth plain silk fabric",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                @"\bembroidery detail\b",
                "visible fabric details from the photo",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                @"\bon collar embroidery\b",
                "on mandarin collar",
                RegexOptions.IgnoreCase);
            result = Regex.Replace(
                result,
                @"\bpush-in on embroidery\b",
                "push-in on mandarin collar",
                RegexOptions.IgnoreCase);
            return result;
        }

        private static bool HasExplicitEmbroideryInPhoto(string text)
        {
            return Regex.IsMatch(
                text,
                @"\b(?:gold embroidery|silver embroidery|embroidered panels|floral embroidery|embroidery on (?:the )?collar|visible embroidery|with (?:gold |silver )?embroidery|intricate embroidery)\b",
                RegexOptions.IgnoreCase);
        }
    }
}
