using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Lưu kết quả săn video trên lưới Săn Affiliate — vị trí bền vững (không mất khi rebuild bin).</summary>
    public sealed class AffiliateHuntResultsStore
    {
        private const string FileName = "hunt_results.json";
        private const int MaxRows = 10_000;

        public List<AffiliateCandidate> Load()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migratedFromLegacy);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new List<AffiliateCandidate>();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var rows = JsonConvert.DeserializeObject<List<AffiliateCandidate>>(json);
                var list = rows?.Where(r => r != null).ToList() ?? new List<AffiliateCandidate>();

                if (migratedFromLegacy && list.Count > 0)
                {
                    Save(list);
                }

                return list;
            }
            catch
            {
                return new List<AffiliateCandidate>();
            }
        }

        public void Save(IEnumerable<AffiliateCandidate> rows)
        {
            var list = (rows ?? Enumerable.Empty<AffiliateCandidate>())
                .Where(r => r != null)
                .ToList();

            if (list.Count == 0)
            {
                try
                {
                    AppDataPaths.WriteJson(FileName, "[]");
                    AppDataPaths.TryDeleteLegacyJson(FileName);
                }
                catch
                {
                    // ignored
                }

                return;
            }

            var deduped = list
                .GroupBy(r => NormalizeVideoUrlKey(r.VideoUrl), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    if (string.IsNullOrEmpty(g.Key))
                    {
                        return g.First();
                    }

                    return g.OrderByDescending(c => c.PlayCount).First();
                })
                .OrderByDescending(c => c.PlayCount)
                .Take(MaxRows)
                .ToList();

            try
            {
                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(deduped, Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }

        private static string NormalizeVideoUrlKey(string videoUrl)
        {
            var u = (videoUrl ?? string.Empty).Trim();
            var q = u.IndexOf('?');
            if (q >= 0)
            {
                u = u.Substring(0, q);
            }

            return u.TrimEnd('/');
        }
    }
}
