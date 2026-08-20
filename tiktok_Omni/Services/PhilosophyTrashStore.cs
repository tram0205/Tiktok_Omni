using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public sealed class PhilosophyTrashDocument
    {
        public List<PhilosophyTrashEntry> Items { get; set; } = new List<PhilosophyTrashEntry>();
    }

    public sealed class PhilosophyTrashEntry
    {
        public Guid TrashId { get; set; } = Guid.NewGuid();

        public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;

        public PhilosophyBatchItem Batch { get; set; }
    }

    /// <summary>Thùng rác batch Triết lý — giữ tối đa 24 giờ.</summary>
    public sealed class PhilosophyTrashStore
    {
        public const int RetentionHours = 24;

        private const string FileName = "philosophy_trash.json";

        public IReadOnlyList<PhilosophyTrashEntry> LoadActiveEntries()
        {
            PurgeExpired(saveIfChanged: true);
            return LoadDocument().Items
                .Where(e => e?.Batch != null)
                .OrderByDescending(e => e.DeletedAtUtc)
                .ToList();
        }

        public int CountActive()
        {
            return LoadActiveEntries().Count;
        }

        public void AddFromBatch(PhilosophyBatchItem batch)
        {
            if (batch == null)
            {
                return;
            }

            var clone = PhilosophyBatchCloneHelper.CloneBatch(batch, forCopy: false);
            if (clone == null)
            {
                return;
            }

            var doc = LoadDocument();
            PurgeExpired(doc, saveIfChanged: false);
            doc.Items.Add(new PhilosophyTrashEntry
            {
                TrashId = Guid.NewGuid(),
                DeletedAtUtc = DateTime.UtcNow,
                Batch = clone
            });
            Save(doc);
        }

        public bool TryRemove(Guid trashId)
        {
            if (trashId == Guid.Empty)
            {
                return false;
            }

            var doc = LoadDocument();
            var removed = doc.Items.RemoveAll(e => e != null && e.TrashId == trashId);
            if (removed <= 0)
            {
                return false;
            }

            Save(doc);
            return true;
        }

        public PhilosophyBatchItem TryTakeBatchForRestore(Guid trashId)
        {
            if (trashId == Guid.Empty)
            {
                return null;
            }

            var doc = LoadDocument();
            var entry = doc.Items.FirstOrDefault(e => e != null && e.TrashId == trashId);
            if (entry?.Batch == null)
            {
                return null;
            }

            if (IsExpired(entry.DeletedAtUtc))
            {
                doc.Items.Remove(entry);
                Save(doc);
                return null;
            }

            doc.Items.Remove(entry);
            Save(doc);
            return PhilosophyBatchCloneHelper.CloneBatch(entry.Batch, forCopy: false);
        }

        public int PurgeExpired(bool saveIfChanged = true)
        {
            var doc = LoadDocument();
            return PurgeExpired(doc, saveIfChanged);
        }

        private int PurgeExpired(PhilosophyTrashDocument doc, bool saveIfChanged)
        {
            if (doc?.Items == null)
            {
                return 0;
            }

            var before = doc.Items.Count;
            doc.Items.RemoveAll(e => e == null || e.Batch == null || IsExpired(e.DeletedAtUtc));
            var removed = before - doc.Items.Count;
            if (removed > 0 && saveIfChanged)
            {
                Save(doc);
            }

            return removed;
        }

        private static bool IsExpired(DateTime deletedAtUtc)
        {
            return deletedAtUtc <= DateTime.UtcNow.AddHours(-RetentionHours);
        }

        private PhilosophyTrashDocument LoadDocument()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new PhilosophyTrashDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<PhilosophyTrashDocument>(json);
                if (doc == null)
                {
                    return new PhilosophyTrashDocument();
                }

                doc.Items = doc.Items?
                    .Where(e => e != null && e.Batch != null)
                    .ToList() ?? new List<PhilosophyTrashEntry>();

                if (migrateFromLegacy && doc.Items.Count > 0)
                {
                    Save(doc);
                }

                return doc;
            }
            catch
            {
                return new PhilosophyTrashDocument();
            }
        }

        private void Save(PhilosophyTrashDocument document)
        {
            var doc = document ?? new PhilosophyTrashDocument();
            doc.Items = (doc.Items ?? new List<PhilosophyTrashEntry>())
                .Where(e => e != null && e.Batch != null)
                .ToList();

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
