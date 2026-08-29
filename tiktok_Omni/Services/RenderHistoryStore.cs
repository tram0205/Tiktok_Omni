using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Lưu URL video reup đã render thành công (render lại sẽ gỡ URL rồi ghi lại).</summary>
    public sealed class RenderHistoryStore
    {
        private const string FileName = "render_history.json";

        private readonly object _sync = new object();
        private HashSet<string> _urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Load()
        {
            lock (_sync)
            {
                _urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
                if (!File.Exists(path))
                {
                    return;
                }

                try
                {
                    var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                    var list = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                    foreach (var u in list)
                    {
                        var key = NormalizeUrl(u);
                        if (!string.IsNullOrEmpty(key))
                        {
                            _urls.Add(key);
                        }
                    }

                    if (migrateFromLegacy && _urls.Count > 0)
                    {
                        SaveLocked();
                    }
                }
                catch
                {
                    _urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public bool Contains(string videoUrl)
        {
            var key = NormalizeUrl(videoUrl);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_sync)
            {
                return _urls.Contains(key);
            }
        }

        public void AddSuccess(string videoUrl)
        {
            var key = NormalizeUrl(videoUrl);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (_sync)
            {
                if (!_urls.Add(key))
                {
                    return;
                }

                SaveLocked();
            }
        }

        public void Remove(string videoUrl)
        {
            var key = NormalizeUrl(videoUrl);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (_sync)
            {
                if (!_urls.Remove(key))
                {
                    return;
                }

                SaveLocked();
            }
        }

        private void SaveLocked()
        {
            try
            {
                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(_urls.ToList(), Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }

        private static string NormalizeUrl(string videoUrl)
        {
            return (videoUrl ?? string.Empty).Trim();
        }
    }
}
