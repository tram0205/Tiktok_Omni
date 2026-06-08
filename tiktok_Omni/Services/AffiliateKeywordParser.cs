using System;
using System.Collections.Generic;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni.Services
{
    /// <summary>Phân tích danh sách từ khoá săn affiliate — profile gán riêng qua UI.</summary>
    public static class AffiliateKeywordParser
    {
        public static List<HuntKeywordEntry> ParseKeywordLines(string raw, string profileName)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var ordered = new List<HuntKeywordEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return ordered;
            }

            foreach (var part in raw.Replace("\r", string.Empty).Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var keyword = part.Trim();
                if (keyword.Length == 0)
                {
                    continue;
                }

                if (!seen.Add(keyword))
                {
                    continue;
                }

                ordered.Add(new HuntKeywordEntry
                {
                    RawInput = keyword,
                    Keyword = keyword,
                    ProfileName = profile
                });
            }

            return ordered;
        }
    }
}
