using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public sealed class ProductAdImageDraftDocument
    {
        public List<ProductAdImageBatchItem> Items { get; set; } = new List<ProductAdImageBatchItem>();
    }

    public sealed class ProductAdImageDraftStore
    {
        private const string FileName = "draft_product_ad_image.json";
        private const string BackupFileName = "draft_product_ad_image.json.bak";

        public ProductAdImageDraftDocument Load()
        {
            var primary = LoadFromPath(AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy));
            var backup = LoadFromPath(AppDataPaths.PersistentFile(BackupFileName), allowMissing: true);
            var doc = ChooseRicherDraft(primary, backup) ?? new ProductAdImageDraftDocument();
            if (migrateFromLegacy && doc.Items.Count > 0)
            {
                Save(doc);
            }

            return doc;
        }

        public void Save(ProductAdImageDraftDocument document)
        {
            var doc = Normalize(document);
            try
            {
                var path = AppDataPaths.PersistentFile(FileName);
                var backupPath = AppDataPaths.PersistentFile(BackupFileName);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, backupPath, overwrite: true);
                    }
                    catch
                    {
                        // vẫn ghi file chính
                    }
                }

                AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(doc, Formatting.Indented));
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }
            catch
            {
                // ignored
            }
        }

        public void SaveItems(IEnumerable<ProductAdImageBatchItem> items)
        {
            Save(new ProductAdImageDraftDocument
            {
                Items = (items ?? Enumerable.Empty<ProductAdImageBatchItem>()).ToList()
            });
        }

        private static ProductAdImageDraftDocument ChooseRicherDraft(
            ProductAdImageDraftDocument primary,
            ProductAdImageDraftDocument backup)
        {
            primary = primary ?? new ProductAdImageDraftDocument();
            backup = backup ?? new ProductAdImageDraftDocument();
            var primaryCount = primary.Items?.Count ?? 0;
            var backupCount = backup.Items?.Count ?? 0;
            return backupCount > primaryCount ? backup : primary;
        }

        private static ProductAdImageDraftDocument LoadFromPath(string path, bool allowMissing = false)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return allowMissing ? null : new ProductAdImageDraftDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<ProductAdImageDraftDocument>(json);
                if (doc == null)
                {
                    return allowMissing ? null : new ProductAdImageDraftDocument();
                }

                return Normalize(doc);
            }
            catch
            {
                return allowMissing ? null : new ProductAdImageDraftDocument();
            }
        }

        private static ProductAdImageDraftDocument Normalize(ProductAdImageDraftDocument document)
        {
            var doc = document ?? new ProductAdImageDraftDocument();
            doc.Items = (doc.Items ?? new List<ProductAdImageBatchItem>())
                .Where(i => i != null)
                .ToList();
            foreach (var item in doc.Items)
            {
                if (item.RowId == Guid.Empty)
                {
                    item.RowId = Guid.NewGuid();
                }

                if (item.GeneratedShots == null)
                {
                    item.GeneratedShots = new List<ProductAdImageShotPlan>();
                }

                item.AspectRatio = ProductAdImageAspectRatioHelper.Normalize(item.AspectRatio);
                if (string.IsNullOrWhiteSpace(item.Status))
                {
                    item.Status = item.GeneratedShots.Count > 0 ? "Xong" : "Chờ";
                }
            }

            return doc;
        }
    }
}
