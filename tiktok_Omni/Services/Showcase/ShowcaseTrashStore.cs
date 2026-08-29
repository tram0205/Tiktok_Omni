using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    public sealed class ShowcaseTrashDocument
    {
        public List<ShowcaseTrashEntry> Items { get; set; } = new List<ShowcaseTrashEntry>();
    }

    public sealed class ShowcaseTrashEntry
    {
        public Guid TrashId { get; set; } = Guid.NewGuid();

        public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;

        public ShowcaseVideoDraftEntry Video { get; set; }
    }

    /// <summary>Thùng rác dòng Showcase — giữ tối đa 24 giờ rồi tự xóa.</summary>
    public sealed class ShowcaseTrashStore
    {
        public const int RetentionHours = 24;

        private const string FileName = "showcase_trash.json";

        public IReadOnlyList<ShowcaseTrashEntry> LoadActiveEntries()
        {
            PurgeExpired(saveIfChanged: true);
            return LoadDocument().Items
                .Where(e => e?.Video != null)
                .OrderByDescending(e => e.DeletedAtUtc)
                .ToList();
        }

        public int CountActive()
        {
            return LoadActiveEntries().Count;
        }

        public void AddFromVideo(ShowcaseVideoItem video, Func<AiVideoGenInputItem, AiVideoGenInputItem> cloneScene)
        {
            if (video == null)
            {
                return;
            }

            var doc = LoadDocument();
            PurgeExpired(doc, saveIfChanged: false);

            doc.Items.Add(new ShowcaseTrashEntry
            {
                TrashId = Guid.NewGuid(),
                DeletedAtUtc = DateTime.UtcNow,
                Video = ShowcaseDraftStore.FromVideo(video, cloneScene)
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

        public ShowcaseVideoItem TryTakeVideoForRestore(
            Guid trashId,
            Func<AiVideoGenInputItem, AiVideoGenInputItem> cloneScene)
        {
            if (trashId == Guid.Empty || cloneScene == null)
            {
                return null;
            }

            var doc = LoadDocument();
            var entry = doc.Items.FirstOrDefault(e => e != null && e.TrashId == trashId);
            if (entry?.Video == null)
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
            return ShowcaseDraftStore.ToVideo(entry.Video, cloneScene);
        }

        public int PurgeExpired(bool saveIfChanged = true)
        {
            var doc = LoadDocument();
            return PurgeExpired(doc, saveIfChanged);
        }

        private int PurgeExpired(ShowcaseTrashDocument doc, bool saveIfChanged)
        {
            if (doc?.Items == null)
            {
                return 0;
            }

            var before = doc.Items.Count;
            doc.Items.RemoveAll(e => e == null || e.Video == null || IsExpired(e.DeletedAtUtc));
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

        private ShowcaseTrashDocument LoadDocument()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new ShowcaseTrashDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<ShowcaseTrashDocument>(json);
                if (doc == null)
                {
                    return new ShowcaseTrashDocument();
                }

                doc.Items = doc.Items?
                    .Where(e => e != null && e.Video != null)
                    .ToList() ?? new List<ShowcaseTrashEntry>();

                if (migrateFromLegacy && doc.Items.Count > 0)
                {
                    Save(doc);
                }

                return doc;
            }
            catch
            {
                return new ShowcaseTrashDocument();
            }
        }

        private void Save(ShowcaseTrashDocument document)
        {
            var doc = document ?? new ShowcaseTrashDocument();
            doc.Items = (doc.Items ?? new List<ShowcaseTrashEntry>())
                .Where(e => e != null && e.Video != null)
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
