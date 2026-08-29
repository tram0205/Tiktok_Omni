using System;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Làm sạch prompt Veo sau Gemini — Flow-safe: chỉ vải/camera, không trigger person policy.</summary>
    public static class ShowcaseVeoPromptSanitizer
    {
        private const string OnModelPrefixToken = "ON-MODEL fashion lookbook — ";

        private const string FlatlayFidelitySuffix =
            "Garment color, silhouette and visible details stay exactly as shown. Camera and touch motion only, no restyling.";

        private static readonly (string Pattern, string Replacement)[] RiskyReplacements =
        {
            (@"\bON-wearer\b", "ON-MODEL"),
            (@"\bwalking model\b", "fabric panels sway gently"),
            (@"\bmodel walking\b", "fabric panels sway gently"),
            (@"\bfashion model\b", "fashion lookbook outfit"),
            (@"\bsupermodel\b", string.Empty),
            (@"\bcelebrity\b", string.Empty),
            (@"\binfluencer\b", string.Empty),
            (@"\bfamous\b", string.Empty),
            (@"\bnamed person\b", string.Empty),
            (@"\bactress\b", string.Empty),
            (@"\bactor\b", string.Empty),
            (@"\bidol\b", string.Empty),
            (@"\bportrait animation\b", "outfit showcase"),
            (@"\bportrait\b", string.Empty),
            (@"\bdancing\b", "gentle fabric sway"),
            (@"\btalking\b", string.Empty),
            (@"\bspeaking\b", string.Empty),
            (@"\blip sync\b", string.Empty),
            (@"\blip movement\b", string.Empty),
            (@"\bface close-up animation\b", "rack focus on outfit detail"),
            (@"\banimate (?:the )?(?:face|person|body|human)\b", string.Empty),
            (@"\bfacial morph\b", string.Empty),
            (@"\bfacial expression\b", string.Empty),
            (@"\bexpression change\b", string.Empty),
            (@"\bgenerate new person\b", string.Empty),
            (@"\bmorph body\b", string.Empty),
            (@"\bbody morph\b", string.Empty),
            (@"\bbody motion\b", "fabric motion"),
            (@"\bnew person\b", string.Empty),
            (@"\breal person likeness\b", string.Empty),
            (@"\bidentity unchanged\b", string.Empty),
            (@"\b(?:slow\s+)?(?:walks?|walking|stride|runway)\b", "fabric sways gently"),
            (@"\b(?:turns?|turning)\b", "fabric drapes naturally"),
            (@"\bthe model\b", "the outfit"),
            (@"\bmodel\b", "outfit")
        };

        private static readonly string[] FlowTriggerStripPatterns =
        {
            @"same wearer from reference photo,?\s*",
            @"same anonymous wearer from reference photo,?\s*",
            @"same wearer from (?:the )?reference photo,?\s*",
            @"same (?:anonymous )?subject from source image,?\s*",
            @"same person from (?:the )?photo,?\s*",
            @"from reference photo,?\s*",
            @"from source image,?\s*",
            @"reference photo,?\s*",
            @"source image,?\s*",
            @"face visible and unchanged,?\s*",
            @"face unchanged from photo,?\s*",
            @"keep face unchanged,?\s*",
            @"no facial morph,?\s*",
            @"no expression change,?\s*",
            @"no talking,?\s*",
            @"no,?\s*no lip movement,?\s*",
            @"no lip movement,?\s*",
            @"no celebrity likeness,?\s*",
            @"hair tips move[^.]*\.?\s*",
            @"hair tips[^.]*breeze[^.]*\.?\s*"
        };

        private static readonly string[] OnModelOnlyFlowTriggerStripPatterns =
        {
            @"hand adjusts[^.]*\.?\s*",
            @"hand smooths[^.]*\.?\s*",
            @"hand lightly[^.]*\.?\s*",
            @"gentle weight shift[^.]*\.?\s*",
            @"weight shift[^.]*\.?\s*",
            @"\bwearer\b,?\s*",
            @"\bthe wearer\b,?\s*"
        };

        private static readonly string[] OnModelHints =
        {
            @"\bon-model\b",
            @"\bon model\b",
            @"\bfashion lookbook\b",
            @"\blookbook\b",
            @"\boutfit\b",
            @"\bwearing\b",
            @"\bwearer\b"
        };

        private static readonly string[] FlowSafeMotionKeywords =
        {
            @"\bfabric\b",
            @"\boutfit\b",
            @"\bbreeze\b",
            @"\bsway\b",
            @"\bripple\b",
            @"\bdrape\b",
            @"\bhem\b",
            @"\bcollar\b",
            @"\bsleeve\b",
            @"\bembroidery\b",
            @"\bpush-in\b",
            @"\bdolly\b",
            @"\brack focus\b",
            @"\btracking\b",
            @"\bpanels\b"
        };

        private static readonly string[] FlatlayCreativeKeywords =
        {
            @"\bhand\b",
            @"\bfinger\b",
            @"\bprop\b",
            @"\baccessory\b",
            @"\brack focus\b",
            @"\bparallax\b",
            @"\bdolly\b",
            @"\bmacro\b",
            @"\bfabric\b",
            @"\btexture\b",
            @"\bwind\b",
            @"\bunfold\b",
            @"\bfold\b",
            @"\blift\b",
            @"\bhold\b",
            @"\breveal\b",
            @"\bstyling\b"
        };

        private static readonly string[] FlatlayCreativeBoosters =
        {
            "Hands enter frame (no face visible) to gently trace the fabric edge without changing the fold layout.",
            "Fingers lightly smooth the collar fold in a slow macro shot; garment layout unchanged.",
            "Soft camera parallax across the flatlay; rack focus shifts to visible closure detail as shown.",
            "Very slow dolly-in on fabric texture; colors and silhouette stay exactly as shown.",
            "Rack focus shifts from background to sharp close-up of collar and visible closure as shown.",
            "Hands hold the hem edge briefly (no face visible) without restyling or unfolding the garment.",
            "Slow tilt across the flatlay composition; product shape and colors unchanged.",
            "Gentle camera push-in on stitching and weave; no restyling, same lighting as shown."
        };

        private static readonly string[] FlowSafeOnModelBoostersPlain =
        {
            "Fabric panels and hem sway gently in a soft breeze; slow cinematic camera push-in.",
            "Dress panels ripple lightly in soft air; rack focus shifts to mandarin collar detail as shown.",
            "Outfit hem sways in a gentle breeze; slow dolly toward stitching and fabric texture as shown.",
            "Fabric panels move softly in breeze; slow push-in on mandarin collar detail as shown.",
            "Fabric drapes and ripples in soft wind; elegant slow tracking shot at mid-torso height.",
            "Hem and side panels sway gently in soft breeze; shallow depth of field on outfit detail.",
            "Soft breeze moves dress fabric panels; slow cinematic push-in on visible weave as shown.",
            "Outfit fabric sways naturally; rack focus from background to sharp product detail."
        };

        private static readonly string[] FlowSafeOnModelBoostersEmbroidery =
        {
            "Embroidered dress panels sway gently in a soft breeze; slow push-in on visible collar embroidery from the photo.",
            "Dress panels ripple lightly; rack focus shifts to embroidery detail shown in the photo.",
            "Embroidered hem sways in a gentle breeze; slow dolly toward visible stitching from the photo.",
            "Fabric panels with visible embroidery move softly; slow push-in on mandarin collar embroidery as shown.",
            "Embroidered outfit drapes in soft wind; slow tracking on floral pattern visible in the photo.",
            "Hem and embroidered side panels sway gently; shallow depth of field on collar embroidery as shown.",
            "Soft breeze moves embroidered fabric panels; slow push-in on weave and embroidery from the photo.",
            "Embroidered outfit fabric sways naturally; rack focus to sharp embroidery detail as shown."
        };

        private static readonly Regex GenericCameraOnlyRegex = new Regex(
            @"\b(?:slow\s+)?(?:camera\s+)?(?:pan|orbit|rotate|rotation|zoom|push[- ]in|pull[- ]back|tilt)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DuplicateOnModelPrefixRegex = new Regex(
            @"(?:ON-MODEL fashion lookbook\s*—\s*)+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Sanitize(string prompt, int sceneIndex = 0)
        {
            var text = (prompt ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return text;
            }

            text = ShowcasePromptTextHelper.StripDurationClauses(text);
            if (text.Length == 0)
            {
                return string.Empty;
            }

            text = Regex.Replace(text, @"ON-wearer", "ON-MODEL", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = DuplicateOnModelPrefixRegex.Replace(text, OnModelPrefixToken);
            text = ProtectOnModelPrefix(text);
            text = ApplyRiskyReplacements(text);
            text = NormalizeGarmentDetailClaims(text);
            var onModel = LooksOnModel(text);
            text = StripFlowTriggerPhrases(text, onModel);
            text = RestoreOnModelPrefix(text);

            if (onModel)
            {
                text = NormalizeOnModelPrefix(text);
                text = EnhanceFlowSafeOnModel(text, sceneIndex);
            }
            else
            {
                text = EnsureFlatlayPrefix(text);
                if (IsGenericFlatlayCameraOnly(text))
                {
                    var booster = FlatlayCreativeBoosters[Math.Abs(sceneIndex) % FlatlayCreativeBoosters.Length];
                    text = text.TrimEnd('.', ' ') + ". " + booster;
                }

                text = EnhanceFlatlayFidelity(text);
            }

            text = Regex.Replace(text, @"\s{2,}", " ", RegexOptions.CultureInvariant).Trim();
            text = Regex.Replace(text, @"\s+([,.;])", "$1", RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @",\s*,", ",", RegexOptions.CultureInvariant);
            return EnsureEnding(text);
        }

        private static string ProtectOnModelPrefix(string text)
        {
            return Regex.Replace(text, @"ON-MODEL", "§ONMODEL§", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string RestoreOnModelPrefix(string text)
        {
            return text.Replace("§ONMODEL§", "ON-MODEL");
        }

        private static string ApplyRiskyReplacements(string text)
        {
            foreach (var (pattern, replacement) in RiskyReplacements)
            {
                text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return text;
        }

        private static string StripFlowTriggerPhrases(string text, bool onModel)
        {
            foreach (var pattern in FlowTriggerStripPatterns)
            {
                text = Regex.Replace(text, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            if (onModel)
            {
                foreach (var pattern in OnModelOnlyFlowTriggerStripPatterns)
                {
                    text = Regex.Replace(text, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }

                text = Regex.Replace(text, @"\bface\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                text = Regex.Replace(text, @"\bperson\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                text = Regex.Replace(text, @"\bwoman\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                text = Regex.Replace(text, @"\bman\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                text = Regex.Replace(text, @"\bsubject\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return text;
        }

        private static bool LooksOnModel(string text)
        {
            if (Regex.IsMatch(text, @"\bFLATLAY\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return false;
            }

            if (Regex.IsMatch(text, @"\bON-MODEL\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }

            foreach (var pattern in OnModelHints)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeOnModelPrefix(string text)
        {
            text = DuplicateOnModelPrefixRegex.Replace(text, OnModelPrefixToken);
            if (!Regex.IsMatch(text, @"^\s*ON-MODEL\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                text = OnModelPrefixToken + text.TrimStart(' ', '—', '-');
            }

            return text;
        }

        private static string EnsureFlatlayPrefix(string text)
        {
            if (Regex.IsMatch(text, @"^\s*FLATLAY\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return text;
            }

            return "FLATLAY product shot — " + text;
        }

        private static bool HasFlowSafeMotion(string text)
        {
            foreach (var pattern in FlowSafeMotionKeywords)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGenericFlatlayCameraOnly(string text)
        {
            if (!GenericCameraOnlyRegex.IsMatch(text))
            {
                return false;
            }

            foreach (var pattern in FlatlayCreativeKeywords)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return false;
                }
            }

            return true;
        }

        private static string EnhanceFlowSafeOnModel(string text, int sceneIndex)
        {
            if (!HasFlowSafeMotion(text))
            {
                var boosters = HasExplicitEmbroideryInPhoto(text)
                    ? FlowSafeOnModelBoostersEmbroidery
                    : FlowSafeOnModelBoostersPlain;
                var booster = boosters[Math.Abs(sceneIndex) % boosters.Length];
                text = text.TrimEnd('.', ' ') + ". " + booster;
            }

            const string safety = "Static scene composition from photo. Product-focused outfit showcase.";

            if (text.IndexOf("static scene composition from photo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }

            return text.TrimEnd('.', ' ') + ". " + safety;
        }

        /// <summary>Gỡ embroidery/màu vải generic khi prompt không mô tả thêu rõ — tránh Flow bịa hoa văn.</summary>
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
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\bpreserve ao dai shape,?\s*mandarin collar and embroidery detail\b",
                "preserve ao dai silhouette, mandarin collar and visible fabric details from the photo",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\bcollar and embroidery detail\b",
                "mandarin collar and visible fabric details as shown",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\bpush-in on embroidery\b",
                "push-in on mandarin collar detail as shown",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\brack focus shifts to collar embroidery detail\b",
                "rack focus shifts to mandarin collar detail as shown",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\bslow cinematic push-in on weave and embroidery\b",
                "slow cinematic push-in on visible weave as shown",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\bembroidery detail\b",
                "visible fabric details from the photo",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\b(?:white|black|red|blue|green|pink|beige|navy|cream|ivory)\s+silk\b",
                "fabric as shown",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(
                result,
                @"\bsmooth plain white silk panels\b",
                "smooth plain fabric panels as shown",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return result;
        }

        private static bool HasExplicitEmbroideryInPhoto(string text)
        {
            return Regex.IsMatch(
                text,
                @"\b(?:gold embroidery|silver embroidery|embroidered panels|floral embroidery|embroidery on (?:the )?collar|visible embroidery|with (?:gold |silver )?embroidery|intricate embroidery|embroidery as shown|embroidery from the photo|embroidery visible)\b",
                RegexOptions.IgnoreCase);
        }

        private static string EnhanceFlatlayFidelity(string text)
        {
            if (text.IndexOf("stay exactly as shown", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("no restyling", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }

            return text.TrimEnd('.', ' ') + ". " + FlatlayFidelitySuffix;
        }

        private static string EnsureEnding(string text)
        {
            const string ending = "smooth cinematic motion, no text overlay";
            if (text.IndexOf(ending, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }

            return text.TrimEnd('.', ' ') + ". " + ending + ".";
        }
    }
}
