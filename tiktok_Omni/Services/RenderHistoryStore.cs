using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Lưu URL video reup đã render thành công — chống render trùng.</summary>
    public sealed class RenderHistoryStore
    {
        private static readonly string HistoryPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "render_history.json");

        private readonly object _sync = new object();
        private HashSet<string> _urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Load()
        {
            lock (_sync)
            {
                _urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!File.Exists(HistoryPath))
                {
                    return;
                }

                try
                {
                    var json = File.ReadAllText(HistoryPath, TextFileEncoding.Utf8);
                    var list = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                    foreach (var u in list)
                    {
                        var key = NormalizeUrl(u);
                        if (!string.IsNullOrEmpty(key))
                        {
                            _urls.Add(key);
                        }
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

                try
                {
                    File.WriteAllText(HistoryPath, JsonConvert.SerializeObject(_urls.ToList(), Formatting.Indented), TextFileEncoding.Utf8NoBom);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static string NormalizeUrl(string videoUrl)
        {
            return (videoUrl ?? string.Empty).Trim();
        }
    }
}
