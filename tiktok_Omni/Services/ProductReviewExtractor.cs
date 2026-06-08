using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services
{
    /// <summary>Trích xuất feedback khách hàng từ HTML trang sản phẩm (TikTok Shop / Shopee).</summary>
    public static class ProductReviewExtractor
    {
        private static readonly Regex JsonStringRegex = new Regex(
            @"""(?:reviewBody|comment|content|text|review_text|buyer_review)""\s*:\s*""((?:\\.|[^""\\]){8,400})""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<string> ExtractTopCustomerReviews(string html, int maxReviews = 3)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return Array.Empty<string>();
            }

            var found = new List<string>();
            foreach (Match match in JsonStringRegex.Matches(html))
            {
                var text = DecodeJsonString(match.Groups[1].Value);
                if (IsValidReviewSnippet(text))
                {
                    found.Add(text);
                }
            }

            foreach (var block in ExtractReviewBlocksFromDomLikeHtml(html))
            {
                if (IsValidReviewSnippet(block))
                {
                    found.Add(block);
                }
            }

            return found
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Length)
                .Take(Math.Max(1, maxReviews))
                .ToList();
        }

        private static IEnumerable<string> ExtractReviewBlocksFromDomLikeHtml(string html)
        {
            var patterns = new[]
            {
                @"class=""[^""]*review[^""]*""[^>]*>([^<]{12,320})<",
                @"data-testid=""[^""]*review[^""]*""[^>]*>([^<]{12,320})<",
                @"""rating_text""\s*:\s*""([^""]{12,320})"""
            };

            foreach (var pattern in patterns)
            {
                foreach (Match m in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
                {
                    var raw = (m.Groups[1].Value ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        yield return Regex.Replace(raw, @"\s+", " ");
                    }
                }
            }
        }

        private static string DecodeJsonString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return raw
                .Replace("\\n", " ")
                .Replace("\\r", " ")
                .Replace("\\t", " ")
                .Replace("\\\"", "\"")
                .Replace("\\/", "/")
                .Trim();
        }

        private static bool IsValidReviewSnippet(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var t = text.Trim();
            if (t.Length < 12 || t.Length > 400)
            {
                return false;
            }

            if (t.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var letterCount = t.Count(char.IsLetterOrDigit);
            return letterCount >= 8;
        }
    }
}
