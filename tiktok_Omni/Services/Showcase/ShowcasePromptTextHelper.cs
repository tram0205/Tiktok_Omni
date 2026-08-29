using System;
using System.Text.RegularExpressions;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcasePromptTextHelper
    {
        /// <summary>Gỡ quy định thời lượng (5s/7 seconds/…) — người dùng chọn trên Veo/Kling.</summary>
        public static string StripDurationClauses(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var t = text.Trim();
            t = Regex.Replace(
                t,
                @",?\s*\d+\s*(?:-\s*)?(?:second|seconds|sec)\b",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            t = Regex.Replace(t, @"\s{2,}", " ");
            t = Regex.Replace(t, @"\s+([,.])", "$1");
            t = Regex.Replace(t, @"([,.])\s*([,.])", "$1");
            t = Regex.Replace(t, @",\s*,", ",");
            return t.Trim(' ', ',', '.', ';', '—', '-');
        }
    }
}
