using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public sealed class PhilosophyDraftDocument
    {
        public List<PhilosophyScriptItem> Scripts { get; set; } = new List<PhilosophyScriptItem>();

        public string Topic { get; set; } = string.Empty;

        public int VisualMode { get; set; }

        public int MinDurationSeconds { get; set; } = 15;

        public int MaxDurationSeconds { get; set; } = 60;

        /// <summary>Thư mục video phân cảnh tự làm (mode 3) — lưu draft.</summary>
        public string PreRenderedVideoFolder { get; set; } = string.Empty;
    }

    public sealed class PhilosophyDraftStore
    {
        private static readonly string DraftPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "draft_philosophy.json");

        public PhilosophyDraftDocument Load()
        {
            if (!File.Exists(DraftPath))
            {
                return new PhilosophyDraftDocument();
            }

            try
            {
                var json = File.ReadAllText(DraftPath, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<PhilosophyDraftDocument>(json);
                if (doc == null)
                {
                    return new PhilosophyDraftDocument();
                }

                doc.Scripts = doc.Scripts?
                    .Where(s => s != null)
                    .ToList() ?? new List<PhilosophyScriptItem>();
                return doc;
            }
            catch
            {
                return new PhilosophyDraftDocument();
            }
        }

        public void Save(PhilosophyDraftDocument document)
        {
            var doc = document ?? new PhilosophyDraftDocument();
            doc.Scripts = (doc.Scripts ?? new List<PhilosophyScriptItem>())
                .Where(s => s != null)
                .ToList();

            try
            {
                File.WriteAllText(
                    DraftPath,
                    JsonConvert.SerializeObject(doc, Formatting.Indented),
                    TextFileEncoding.Utf8NoBom);
            }
            catch
            {
                // ignored
            }
        }
    }
}
