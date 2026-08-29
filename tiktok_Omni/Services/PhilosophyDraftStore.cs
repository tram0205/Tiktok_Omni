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
        public List<PhilosophyBatchItem> Batches { get; set; } = new List<PhilosophyBatchItem>();

        /// <summary>Legacy flat scripts — migrated to Batches on load.</summary>
        public List<PhilosophyScriptItem> Scripts { get; set; } = new List<PhilosophyScriptItem>();

        public string Topic { get; set; } = string.Empty;

        public int VisualMode { get; set; }

        public int MinDurationSeconds { get; set; } = PhilosophyRenderOptions.QuotesDefaultMinSeconds;

        public int MaxDurationSeconds { get; set; } = PhilosophyRenderOptions.QuotesDefaultMaxSeconds;

        /// <summary>Thư mục video phân cảnh tự làm (mode 3) — lưu draft.</summary>
        public string PreRenderedVideoFolder { get; set; } = string.Empty;
    }

    public sealed class PhilosophyDraftStore
    {
        private const string FileName = "draft_philosophy.json";

        public PhilosophyDraftDocument Load()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new PhilosophyDraftDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<PhilosophyDraftDocument>(json);
                if (doc == null)
                {
                    return new PhilosophyDraftDocument();
                }

                doc.Batches = doc.Batches?
                    .Where(b => b != null)
                    .ToList() ?? new List<PhilosophyBatchItem>();

                doc.Scripts = doc.Scripts?
                    .Where(s => s != null)
                    .ToList() ?? new List<PhilosophyScriptItem>();

                if (doc.Batches.Count == 0 && doc.Scripts.Count > 0)
                {
                    doc.Batches = PhilosophyBatchHelper.MigrateLegacyScripts(
                        doc.Scripts,
                        doc.Topic,
                        doc.MinDurationSeconds,
                        doc.MaxDurationSeconds);
                    doc.Scripts = new List<PhilosophyScriptItem>();
                }

                foreach (var batch in doc.Batches)
                {
                    PhilosophyBatchHelper.EnsureBatchAudioDefaults(batch);
                    PhilosophyBatchHelper.NormalizeBatchQuoteOwnership(batch);
                }

                if (migrateFromLegacy && (doc.Batches.Count > 0 || doc.Scripts.Count > 0))
                {
                    Save(doc);
                }

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
            doc.Batches = (doc.Batches ?? new List<PhilosophyBatchItem>())
                .Where(b => b != null)
                .ToList();
            doc.Scripts = new List<PhilosophyScriptItem>();

            try
            {
                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(doc, Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }
    }
}
