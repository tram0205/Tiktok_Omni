using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public sealed class SlideshowDraftDocument
    {
        public List<AiVideoGenInputItem> Products { get; set; } = new List<AiVideoGenInputItem>();
        public string SharedScript { get; set; } = string.Empty;
    }

    public sealed class SlideshowDraftStore
    {
        private const string FileName = "draft_slideshow.json";

        public SlideshowDraftDocument Load()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                return new SlideshowDraftDocument();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                var doc = JsonConvert.DeserializeObject<SlideshowDraftDocument>(json);
                if (doc == null)
                {
                    return new SlideshowDraftDocument();
                }

                doc.Products = doc.Products?
                    .Where(p => p != null)
                    .Select(NormalizeProduct)
                    .ToList() ?? new List<AiVideoGenInputItem>();

                if (migrateFromLegacy && doc.Products.Count > 0)
                {
                    Save(doc);
                }

                return doc;
            }
            catch
            {
                return new SlideshowDraftDocument();
            }
        }

        public void Save(SlideshowDraftDocument document)
        {
            var doc = document ?? new SlideshowDraftDocument();
            doc.Products = (doc.Products ?? new List<AiVideoGenInputItem>())
                .Where(p => p != null)
                .Select(NormalizeProduct)
                .ToList();
            doc.SharedScript = doc.SharedScript ?? string.Empty;

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

        private static AiVideoGenInputItem NormalizeProduct(AiVideoGenInputItem item)
        {
            if (item == null)
            {
                return new AiVideoGenInputItem();
            }

            item.ProfileName = ProfileScopedPaths.ResolveProfileName(item.ProfileName);
            item.SourceKeyword = (item.SourceKeyword ?? string.Empty).Trim();
            item.ProductName = (item.ProductName ?? string.Empty).Trim();
            item.VideoUrl = (item.VideoUrl ?? string.Empty).Trim();
            item.HookText = (item.HookText ?? string.Empty).Trim();
            item.Hashtags = (item.Hashtags ?? string.Empty).Trim();
            item.Price = (item.Price ?? string.Empty).Trim();
            item.ImageUrl = (item.ImageUrl ?? string.Empty).Trim();
            item.CustomerReviews = (item.CustomerReviews ?? string.Empty).Trim();
            return item;
        }
    }
}
