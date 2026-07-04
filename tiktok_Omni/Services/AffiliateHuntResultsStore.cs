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
        private static readonly string LegacyDraftPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "hunt_results.json");

        private const int MaxRows = 10_000;

        private static string PersistentDraftPath =>
            Path.Combine(GetPersistentAppDataDirectory(), "hunt_results.json");

        private static string GetPersistentAppDataDirectory()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "tiktok_Omni");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public List<AffiliateCandidate> Load()
        {
            var path = ResolveReadableDraftPath(out var migratedFromLegacy);
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
                    TryDeleteLegacyDraft();
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

            var targetPath = PersistentDraftPath;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetPersistentAppDataDirectory());

            if (list.Count == 0)
            {
                try
                {
                    File.WriteAllText(targetPath, "[]", TextFileEncoding.Utf8NoBom);
                    TryDeleteLegacyDraft();
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
                File.WriteAllText(
                    targetPath,
                    JsonConvert.SerializeObject(deduped, Formatting.Indented),
                    TextFileEncoding.Utf8NoBom);
                TryDeleteLegacyDraft();
            }
            catch
            {
                // ignored
            }
        }

        private static string ResolveReadableDraftPath(out bool migratedFromLegacy)
        {
            migratedFromLegacy = false;
            var persistent = PersistentDraftPath;
            if (File.Exists(persistent))
            {
                return persistent;
            }

            if (File.Exists(LegacyDraftPath))
            {
                migratedFromLegacy = true;
                return LegacyDraftPath;
            }

            return persistent;
        }

        private static void TryDeleteLegacyDraft()
        {
            try
            {
                if (File.Exists(LegacyDraftPath))
                {
                    File.Delete(LegacyDraftPath);
                }
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
