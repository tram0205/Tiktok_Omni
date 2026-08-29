using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Lịch sử URL đã săn — tránh quét trùng trong cửa sổ 7 ngày (hunt_history.json).</summary>
    public sealed class HuntHistoryStore
    {
        private const string FileName = "hunt_history.json";

        private readonly object _sync = new object();
        private List<HuntHistoryEntry> _entries = new List<HuntHistoryEntry>();

        public void Load()
        {
            lock (_sync)
            {
                _entries = new List<HuntHistoryEntry>();
                var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
                if (!File.Exists(path))
                {
                    return;
                }

                try
                {
                    var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                    _entries = JsonConvert.DeserializeObject<List<HuntHistoryEntry>>(json)
                               ?? new List<HuntHistoryEntry>();
                    PruneExpiredLocked(TimeSpan.FromDays(7));
                    if (migrateFromLegacy && _entries.Count > 0)
                    {
                        SaveLocked();
                    }
                }
                catch
                {
                    _entries = new List<HuntHistoryEntry>();
                }
            }
        }

        public bool ContainsRecent(string videoUrl, TimeSpan maxAge)
        {
            var key = NormalizeUrl(videoUrl);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_sync)
            {
                PruneExpiredLocked(maxAge);
                return _entries.Any(e =>
                    string.Equals(e.Url, key, StringComparison.OrdinalIgnoreCase) &&
                    e.ScannedAtUtc >= DateTime.UtcNow - maxAge);
            }
        }

        public void Record(string videoUrl, string profileName = null, string keyword = null)
        {
            var key = NormalizeUrl(videoUrl);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (_sync)
            {
                _entries.RemoveAll(e => string.Equals(e.Url, key, StringComparison.OrdinalIgnoreCase));
                _entries.Add(new HuntHistoryEntry
                {
                    Url = key,
                    ProfileName = ProfileScopedPaths.ResolveProfileName(profileName),
                    Keyword = (keyword ?? string.Empty).Trim(),
                    ScannedAtUtc = DateTime.UtcNow
                });
                PruneExpiredLocked(TimeSpan.FromDays(7));
                SaveLocked();
            }
        }

        private void PruneExpiredLocked(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            _entries = _entries
                .Where(e => e != null && e.ScannedAtUtc >= cutoff && !string.IsNullOrWhiteSpace(e.Url))
                .OrderByDescending(e => e.ScannedAtUtc)
                .Take(50_000)
                .ToList();
        }

        private void SaveLocked()
        {
            try
            {
                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(_entries, Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }

        private static string NormalizeUrl(string videoUrl)
        {
            var u = (videoUrl ?? string.Empty).Trim();
            var q = u.IndexOf('?');
            if (q >= 0)
            {
                u = u.Substring(0, q);
            }

            return u.TrimEnd('/');
        }

        private sealed class HuntHistoryEntry
        {
            public string Url { get; set; } = string.Empty;
            public string ProfileName { get; set; } = string.Empty;
            public string Keyword { get; set; } = string.Empty;
            public DateTime ScannedAtUtc { get; set; }
        }
    }
}
