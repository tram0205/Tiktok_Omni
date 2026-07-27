using System;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Làm sạch prompt Veo sau Gemini — Flow-safe: chỉ vải/camera, không trigger person policy.</summary>
    public static class ShowcaseVeoPromptSanitizer
    {
        private const string OnModelPrefixToken = "ON-MODEL fashion lookbook — ";

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
            @"hair tips[^.]*breeze[^.]*\.?\s*",
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
            "A hand enters frame to gently lift and unfold the garment, revealing fabric texture.",
            "Fingers trace along the fabric surface in a slow macro shot with shallow depth of field.",
            "Styling props slide into frame as the camera parallax-reveals product details.",
            "Soft breeze makes the fabric ripple while the camera slowly dollies in on stitching.",
            "Rack focus shifts from background to a sharp close-up of collar and button detail.",
            "Hands adjust the hem and sleeve without showing a face, emphasizing fit and drape.",
            "Slow tilt reveals the garment standing upright to show silhouette and form.",
            "Layered flatlay elements separate slightly with parallax to highlight product layers."
        };

        private static readonly string[] FlowSafeOnModelBoosters =
        {
            "Fabric panels and hem sway gently in a soft breeze; slow cinematic camera push-in.",
            "Dress panels ripple lightly in soft air; rack focus shifts to collar embroidery detail.",
            "Outfit hem sways in a gentle breeze; slow dolly toward stitching and fabric texture.",
            "Silk fabric panels move softly in breeze; slow push-in on mandarin collar detail.",
            "Fabric drapes and ripples in soft wind; elegant slow tracking shot at mid-torso height.",
            "Hem and side panels sway beautifully in soft breeze; shallow depth of field on outfit detail.",
            "Soft breeze moves dress fabric panels; slow cinematic push-in on weave and embroidery.",
            "Outfit fabric sways naturally; rack focus from background to sharp product detail."
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
            text = StripFlowTriggerPhrases(text);
            text = RestoreOnModelPrefix(text);

            var onModel = LooksOnModel(text);
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

        private static string StripFlowTriggerPhrases(string text)
        {
            foreach (var pattern in FlowTriggerStripPatterns)
            {
                text = Regex.Replace(text, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            text = Regex.Replace(text, @"\bface\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\bperson\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\bwoman\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\bman\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\bsubject\b", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
                var booster = FlowSafeOnModelBoosters[Math.Abs(sceneIndex) % FlowSafeOnModelBoosters.Length];
                text = text.TrimEnd('.', ' ') + ". " + booster;
            }

            const string safety = "Static scene composition from photo. Product-focused outfit showcase.";

            if (text.IndexOf("static scene composition from photo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }

            return text.TrimEnd('.', ' ') + ". " + safety;
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
