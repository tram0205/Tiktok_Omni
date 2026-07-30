using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public sealed class HuntProductDraftStore
    {
        private static readonly string DraftPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "tiktok_Omni",
            "draft_hunt_product.json");

        public List<HuntProductCandidate> Load()
        {
            var path = GetPersistentPath();
            if (!File.Exists(path))
            {
                return new List<HuntProductCandidate>();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var rows = JsonConvert.DeserializeObject<List<HuntProductCandidate>>(json);
                return rows?.Where(r => r != null).ToList() ?? new List<HuntProductCandidate>();
            }
            catch
            {
                return new List<HuntProductCandidate>();
            }
        }

        public void Save(IEnumerable<HuntProductCandidate> rows)
        {
            var list = (rows ?? Enumerable.Empty<HuntProductCandidate>())
                .Where(r => r != null)
                .ToList();
            try
            {
                var path = GetPersistentPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented), TextFileEncoding.Utf8NoBom);
            }
            catch
            {
                // ignored
            }
        }

        private static string GetPersistentPath()
        {
            // Tự động migrate từ thư mục cũ cạnh exe nếu có
            var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "draft_hunt_product.json");
            if (!File.Exists(DraftPath) && File.Exists(legacyPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(DraftPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(legacyPath, DraftPath, overwrite: false);
                }
                catch
                {
                    // ignored
                }
            }
            return DraftPath;
        }
    }
}
