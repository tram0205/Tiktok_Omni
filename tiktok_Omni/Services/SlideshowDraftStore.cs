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
        private static readonly string DraftPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "tiktok_Omni",
            "draft_slideshow.json");

        public SlideshowDraftDocument Load()
        {
            var path = GetPersistentPath();
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
                return doc;
            }
            catch
            {
                return new SlideshowDraftDocument();
            }
        }

        public void Save(SlideshowDraftDocument document)
        {
            if (document == null)
            {
                return;
            }

            var list = (document.Products ?? Enumerable.Empty<AiVideoGenInputItem>())
                .Where(p => p != null)
                .Select(NormalizeProduct)
                .ToList();
            var doc = new SlideshowDraftDocument
            {
                Products = list,
                SharedScript = document.SharedScript ?? string.Empty
            };

            try
            {
                var path = GetPersistentPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented), TextFileEncoding.Utf8NoBom);
            }
            catch
            {
                // ignored
            }
        }

        private static string GetPersistentPath()
        {
            // Tự động migrate từ thư mục cũ cạnh exe nếu có
            var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "draft_slideshow.json");
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

        private static AiVideoGenInputItem NormalizeProduct(AiVideoGenInputItem item)
        {
            if (item == null)
            {
                return new AiVideoGenInputItem();
            }
            item.ProfileName = (item.ProfileName ?? string.Empty).Trim();
            item.SourceKeyword = (item.SourceKeyword ?? string.Empty).Trim();
            item.ProductName = (item.ProductName ?? string.Empty).Trim();
            item.Price = (item.Price ?? string.Empty).Trim();
            item.VideoUrl = (item.VideoUrl ?? string.Empty).Trim();
            item.ImageUrl = (item.ImageUrl ?? string.Empty).Trim();
            item.Hashtags = (item.Hashtags ?? string.Empty).Trim();
            item.VideoScript = (item.VideoScript ?? string.Empty).Trim();
            item.VoiceoverTranscript = (item.VoiceoverTranscript ?? string.Empty).Trim();
            item.CustomerReviews = (item.CustomerReviews ?? string.Empty).Trim();
            item.VoiceId = (item.VoiceId ?? string.Empty).Trim();
            item.VideoStyle = (item.VideoStyle ?? string.Empty).Trim();
            item.MascotStyle = (item.MascotStyle ?? string.Empty).Trim();
            item.LastRenderOutputPath = (item.LastRenderOutputPath ?? string.Empty).Trim();
            item.RenderStatus = (item.RemixStatus ?? string.Empty).Trim();
            item.RenderLastError = (item.RemixLastError ?? string.Empty).Trim();
            return item;
        }
    }
}
