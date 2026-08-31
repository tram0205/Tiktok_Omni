using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public static class ProductAdImagePromptBuilder
    {
        public const string DefaultGeminiModel = "gemini-2.5-flash";

        public static string ResolveGeminiModel(string settingsModel)
        {
            var raw = (settingsModel ?? string.Empty).Trim();
            return string.IsNullOrEmpty(raw) ? DefaultGeminiModel : raw;
        }

        public static string BuildPlanningPrompt(ProductAdImageBatchItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var productName = (item.ProductName ?? string.Empty).Trim();
            var aspect = ProductAdImageAspectRatioHelper.Normalize(item.AspectRatio);
            var productType = ShowcaseProductTypePresets.ResolvePromptForGemini(item.ProductTypePrompt);
            var shootStyle = ProductAdImageShootStylePresets.ResolvePromptForGemini(item.ShootStylePrompt);
            var identityLock = ProductAdImageIdentityLockPresets.ResolvePromptForGemini(item.IdentityLockPrompt);
            var productLock = (item.ProductLockDescription ?? string.Empty).Trim();
            var slug = Slugify(productName);

            var sb = new StringBuilder();
            sb.AppendLine("You are a fashion e-commerce photo director. Study the attached master reference photo (the person AND the garment/product).");
            sb.AppendLine("Return ONLY a JSON array of shot plans. No markdown, no commentary.");
            sb.AppendLine();
            sb.AppendLine("PRODUCT NAME: " + productName);
            sb.AppendLine("ASPECT RATIO for every shot: " + aspect);
            sb.AppendLine("PRODUCT TYPE HINT: " + (string.IsNullOrEmpty(productType) ? "(auto from photo)" : productType));
            sb.AppendLine("SHOOT STYLE: " + (string.IsNullOrEmpty(shootStyle) ? "(choose a coherent style from the photo)" : shootStyle));
            sb.AppendLine("IDENTITY LOCK: " + (string.IsNullOrEmpty(identityLock) ? "(keep face + garment unless shot is flatlay/macro)" : identityLock));
            sb.AppendLine("PRODUCT LOCK (user notes — color, print, logo, form): " + (string.IsNullOrEmpty(productLock) ? "(use only what is visible in the photo)" : productLock));
            sb.AppendLine();
            sb.AppendLine("SHOT COUNTS (exact totals — emit exactly this many objects, in this order of types):");
            AppendCount(sb, "solo_female", item.SoloFemaleCount);
            AppendCount(sb, "solo_male", item.SoloMaleCount);
            AppendCount(sb, "couple", item.CoupleCount);
            AppendCount(sb, "group", item.GroupCount);
            AppendCount(sb, "flatlay", item.FlatlayCount);
            AppendCount(sb, "fabric_closeup", item.FabricCloseupCount);
            AppendCount(sb, "detail_highlight", item.DetailHighlightCount);
            sb.AppendLine("TOTAL SHOTS: " + item.TotalImageCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("HARD LOCKS:");
            sb.AppendLine("- Garment/product must stay identical: color, print, logo, silhouette, fabric, stitching, hardware as in the photo. Do NOT invent details.");
            sb.AppendLine("- For on-model shots (solo_female, solo_male, couple, group): lock the model's identity (face, hair, skin, body type) from the reference. Do not change ethnicity, age, or beauty-filter the face.");
            sb.AppendLine("- For flatlay / fabric_closeup / detail_highlight: no face required; still lock garment identity.");
            sb.AppendLine("- You MAY change ONLY: pose, gesture, camera angle, framing, background, lighting, and small accessories that do not hide the product.");
            sb.AppendLine("- Each prompt must be in English, detailed, ready for an image model. Do not mention TikTok, UI, or JSON in the prompt text.");
            sb.AppendLine("- Vary pose/camera across shots of the same type so they are not duplicates.");
            sb.AppendLine();
            sb.AppendLine("JSON SCHEMA — array of objects:");
            sb.AppendLine("[");
            sb.AppendLine("  {");
            sb.AppendLine("    \"shotType\": \"solo_female|solo_male|couple|group|flatlay|fabric_closeup|detail_highlight\",");
            sb.AppendLine("    \"title\": \"short Vietnamese label\",");
            sb.AppendLine("    \"prompt\": \"English image prompt\",");
            sb.AppendLine("    \"aspectRatio\": \"" + aspect + "\",");
            sb.AppendLine("    \"outputFileName\": \"" + slug + "_solo_female_01.png\"");
            sb.AppendLine("  }");
            sb.AppendLine("]");
            return sb.ToString();
        }

        public static ProductAdImagePlanningResult ParsePlanningJson(string raw, ProductAdImageBatchItem item)
        {
            var json = StripMarkdownFence(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Gemini không trả JSON prompt hợp lệ.");
            }

            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không parse được JSON prompt từ Gemini.", ex);
            }

            JArray array;
            if (root is JArray direct)
            {
                array = direct;
            }
            else if (root is JObject obj)
            {
                array = obj["shots"] as JArray
                        ?? obj["Shots"] as JArray
                        ?? obj["items"] as JArray
                        ?? new JArray();
            }
            else
            {
                array = new JArray();
            }

            var aspect = ProductAdImageAspectRatioHelper.Normalize(item?.AspectRatio);
            var slug = Slugify(item?.ProductName);
            var typeIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var shots = new List<ProductAdImageShotPlan>();
            foreach (var token in array)
            {
                if (!(token is JObject shotObj))
                {
                    continue;
                }

                var shotType = NormalizeShotType(shotObj.Value<string>("shotType") ?? shotObj.Value<string>("ShotType"));
                if (string.IsNullOrEmpty(shotType))
                {
                    continue;
                }

                if (!typeIndex.TryGetValue(shotType, out var n))
                {
                    n = 0;
                }

                n++;
                typeIndex[shotType] = n;

                var title = (shotObj.Value<string>("title") ?? shotObj.Value<string>("Title") ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(title))
                {
                    title = ProductAdImageShotPlan.FormatShotType(shotType);
                }

                var prompt = (shotObj.Value<string>("prompt") ?? shotObj.Value<string>("Prompt") ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(prompt))
                {
                    continue;
                }

                var ratio = ProductAdImageAspectRatioHelper.Normalize(
                    shotObj.Value<string>("aspectRatio") ?? shotObj.Value<string>("AspectRatio") ?? aspect);
                var fileName = (shotObj.Value<string>("outputFileName") ?? shotObj.Value<string>("OutputFileName") ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = slug + "_" + shotType + "_" + n.ToString("00", CultureInfo.InvariantCulture) + ".png";
                }

                shots.Add(new ProductAdImageShotPlan
                {
                    Index = shots.Count + 1,
                    ShotType = shotType,
                    Title = title,
                    Prompt = prompt,
                    AspectRatio = ratio,
                    OutputFileName = fileName
                });
            }

            if (shots.Count == 0)
            {
                throw new InvalidOperationException("Gemini không trả danh sách prompt hợp lệ.");
            }

            return new ProductAdImagePlanningResult(shots);
        }

        public static string Slugify(string text)
        {
            var s = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Length == 0)
            {
                return "sp";
            }

            var chars = s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            var slug = new string(chars).Trim('_');
            while (slug.Contains("__"))
            {
                slug = slug.Replace("__", "_");
            }

            if (slug.Length > 40)
            {
                slug = slug.Substring(0, 40).Trim('_');
            }

            return string.IsNullOrEmpty(slug) ? "sp" : slug;
        }

        private static void AppendCount(StringBuilder sb, string shotType, int count)
        {
            sb.AppendLine("- " + shotType + ": " + Math.Max(0, count).ToString(CultureInfo.InvariantCulture));
        }

        private static string NormalizeShotType(string raw)
        {
            var s = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            switch (s)
            {
                case "solo_female":
                case "solo_male":
                case "couple":
                case "group":
                case "flatlay":
                case "fabric_closeup":
                case "detail_highlight":
                    return s;
                default:
                    return s;
            }
        }

        private static string StripMarkdownFence(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNl = text.IndexOf('\n');
                if (firstNl > 0)
                {
                    text = text.Substring(firstNl + 1);
                }

                var fence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0)
                {
                    text = text.Substring(0, fence);
                }
            }

            var match = Regex.Match(text, @"(\[.*\]|\{.*\})", RegexOptions.Singleline);
            return match.Success ? match.Value : text.Trim();
        }
    }
}
