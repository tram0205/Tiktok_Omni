using System;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Chuẩn hoá prompt Kling I2V — chuyển động tự nhiên, sống động như quay thật (không quá chậm/giả AI).</summary>
    public static class ShowcaseKlingPromptSanitizer
    {
        private const string PromptEndingSuffix = "fluid natural motion, no text overlay";

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

        /// <summary>Chỉ làm mềm chuyển động nguy hiểm — không ép mọi thứ thành «slow step».</summary>
        private static readonly (string Pattern, string Replacement)[] AggressiveMotionSofteners =
        {
            (@"\bstrong\s+(?:breeze|wind|gust)\b", "light breeze"),
            (@"\bheavy\s+(?:breeze|wind)\b", "light breeze"),
            (@"\bdramatic(?:ally)?\b", "natural"),
            (@"\b(?:twirl|spin|runway\s+walk)\b", "natural fabric sway with relaxed body turn"),
            (@"\bhair\s+(?:flowing|blowing|flying)\b", "natural hair movement"),
            (@"\brunway\b", "natural walk"),
            (@"\b(?:sprint|running|jogging)\b", "natural brisk step")
        };

        private static readonly (string Pattern, string Replacement)[] SluggishMotionUpgraders =
        {
            (@"\bvery\s+slow\s+cinematic\s+push-in\b", "smooth natural push-in with handheld-style camera drift"),
            (@"\bvery\s+slow\s+cinematic\b", "smooth natural cinematic"),
            (@"\bvery\s+slow\s+push-in\b", "smooth natural push-in"),
            (@"\bvery\s+slow\b", "smooth natural"),
            (@"\bone\s+slow\s+step\s+forward\b", "a natural relaxed step forward"),
            (@"\bone\s+slow\s+step\b", "a natural step"),
            (@"\btwo\s+slow\s+steps\b", "two natural steps"),
            (@"\bthree\s+slow\s+steps\b", "two natural steps"),
            (@"\bslow\s+step\s+forward\b", "natural step forward"),
            (@"\bslow\s+steps?\b", "natural steps"),
            (@"\bsubtle\s+weight\s+shift\s+forward\b", "natural weight shift with relaxed body motion and fabric follow-through"),
            (@"\bsubtle\s+weight\s+shift\b", "natural weight shift with soft body rhythm"),
            (@"\bminimal\s+motion\b", "natural lifelike motion"),
            (@"\bhardly\s+moving\b", "moving naturally in place"),
            (@"\b barely\b", " naturally"),
            (@"\bstatic\b", "natural"),
            (@"\bgentle\s+cinematic\s+motion\b", "fluid natural motion"),
            (@"\bsmooth\s+cinematic\s+motion\b", "fluid natural motion")
        };

        private static readonly string[] BalancedMotionBoosters =
        {
            "Natural step forward with relaxed posture, dress panels sway in light breeze, smooth handheld-style camera drift and gentle push-in",
            "Soft shoulder turn and weight shift, fabric drapes with natural follow-through, fluid tracking on outfit detail",
            "Relaxed in-place rhythm — hip shift and hem ripple naturally, smooth camera drift like real TikTok footage",
            "Model moves with natural body momentum, panels sway in light breeze, cinematic but lifelike push-in",
            "Light breeze moves fabric while model shifts weight naturally, subtle head turn, fluid handheld camera motion"
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

            foreach (var (pattern, replacement) in SluggishMotionUpgraders)
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
            else if (IsMotionTooSluggish(text))
            {
                text += ". Natural body rhythm, fabric follow-through, smooth handheld-style camera drift";
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
                @"\b(?:sway|swaying|step|steps|shift|turn|turns|breeze|walk|walking|movement|push-in|dolly|ripple|drape|drift|handheld|momentum|follow-through|fluid|natural|panels?\s+(?:sway|ripple)|fabric\s+motion|body\s+motion|weight\s+shift|tracking|rhythm)\b",
                RegexOptions.IgnoreCase);
        }

        private static bool IsMotionTooSluggish(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var sluggishHits = Regex.Matches(
                text,
                @"\b(?:very\s+slow|one\s+slow|subtle\s+only|minimal\s+motion|hardly\s+moving|barely\s+moving|almost\s+still|slight(?:ly)?\s+weight\s+shift\s+forward(?!\s+with\s+relaxed))\b",
                RegexOptions.IgnoreCase).Count;

            var livelyHits = Regex.Matches(
                text,
                @"\b(?:natural|relaxed|fluid|drift|handheld|momentum|follow-through|lively|rhythm|step\s+forward|shoulder\s+turn|hip\s+shift|tracking|ripple|sway)\b",
                RegexOptions.IgnoreCase).Count;

            return sluggishHits >= 1 && livelyHits <= 2;
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
                "mandarin collar and visible fabric details as shown",
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
                @"\b(?:gold embroidery|silver embroidery|embroidered panels|floral embroidery|embroidery on (?:the )?collar|visible embroidery|with (?:gold |silver )?embroidery|intricate embroidery|embroidery as shown|embroidery from the photo|embroidery visible)\b",
                RegexOptions.IgnoreCase);
        }
    }
}
