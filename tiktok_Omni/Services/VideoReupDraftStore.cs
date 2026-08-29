using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public sealed class VideoReupDraftStore
    {
        private const string FileName = "draft_reup.json";

        public List<VideoReupRowItem> Load()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new List<VideoReupRowItem>();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var rows = JsonConvert.DeserializeObject<List<VideoReupRowItem>>(json);
                var list = rows?.Where(r => r != null).ToList() ?? new List<VideoReupRowItem>();
                if (migrateFromLegacy && list.Count > 0)
                {
                    Save(list);
                }

                return list;
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
                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(list, Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }
    }
}
