using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public sealed class ProductAdImageTrashDocument
    {
        public List<ProductAdImageTrashEntry> Items { get; set; } = new List<ProductAdImageTrashEntry>();
    }

    public sealed class ProductAdImageTrashEntry
    {
        public Guid TrashId { get; set; } = Guid.NewGuid();

        public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;

        public ProductAdImageBatchItem Item { get; set; }
    }

    public sealed class ProductAdImageTrashStore
    {
        private const string FileName = "trash_product_ad_image.json";

        public IReadOnlyList<ProductAdImageTrashEntry> LoadActiveEntries()
        {
            return LoadDocument().Items
                .Where(e => e?.Item != null)
                .OrderByDescending(e => e.DeletedAtUtc)
                .ToList();
        }

        public int CountActive() => LoadActiveEntries().Count;

        public void AddFromItem(ProductAdImageBatchItem item)
        {
            if (item == null)
            {
                return;
            }

            var clone = ProductAdImageBatchCloneHelper.CloneRow(item, copyReferenceImage: false);
            if (clone == null)
            {
                return;
            }

            clone.ProductName = item.ProductName;
            clone.RowId = item.RowId == Guid.Empty ? Guid.NewGuid() : item.RowId;

            var doc = LoadDocument();
            doc.Items.Add(new ProductAdImageTrashEntry
            {
                TrashId = Guid.NewGuid(),
                DeletedAtUtc = DateTime.UtcNow,
                Item = clone
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

        public ProductAdImageBatchItem TryTakeItemForRestore(Guid trashId)
        {
            if (trashId == Guid.Empty)
            {
                return null;
            }

            var doc = LoadDocument();
            var entry = doc.Items.FirstOrDefault(e => e != null && e.TrashId == trashId);
            if (entry?.Item == null)
            {
                return null;
            }

            doc.Items.Remove(entry);
            Save(doc);
            return ProductAdImageBatchCloneHelper.CloneRow(entry.Item, copyReferenceImage: false)
                   ?? entry.Item;
        }

        private ProductAdImageTrashDocument LoadDocument()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new ProductAdImageTrashDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<ProductAdImageTrashDocument>(json);
                if (doc == null)
                {
                    return new ProductAdImageTrashDocument();
                }

                doc.Items = doc.Items?
                    .Where(e => e != null && e.Item != null)
                    .ToList() ?? new List<ProductAdImageTrashEntry>();

                if (migrateFromLegacy && doc.Items.Count > 0)
                {
                    Save(doc);
                }

                return doc;
            }
            catch
            {
                return new ProductAdImageTrashDocument();
            }
        }

        private void Save(ProductAdImageTrashDocument document)
        {
            var doc = document ?? new ProductAdImageTrashDocument();
            doc.Items = (doc.Items ?? new List<ProductAdImageTrashEntry>())
                .Where(e => e != null && e.Item != null)
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
