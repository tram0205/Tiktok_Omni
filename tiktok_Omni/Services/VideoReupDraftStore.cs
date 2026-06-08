using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public sealed class VideoReupDraftStore
    {
        private static readonly string DraftPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "draft_reup.json");

        public List<VideoReupRowItem> Load()
        {
            if (!File.Exists(DraftPath))
            {
                return new List<VideoReupRowItem>();
            }

            try
            {
                var json = File.ReadAllText(DraftPath, TextFileEncoding.Utf8);
                var rows = JsonConvert.DeserializeObject<List<VideoReupRowItem>>(json);
                return rows?.Where(r => r != null).ToList() ?? new List<VideoReupRowItem>();
            }
            catch
            {
                return new List<VideoReupRowItem>();
            }
        }

        public void Save(IEnumerable<VideoReupRowItem> rows)
        {
            var list = (rows ?? Enumerable.Empty<VideoReupRowItem>())
                .Where(r => r != null)
                .ToList();
            try
            {
                File.WriteAllText(DraftPath, JsonConvert.SerializeObject(list, Formatting.Indented), TextFileEncoding.Utf8NoBom);
            }
            catch
            {
                // ignored
            }
        }
    }
}
