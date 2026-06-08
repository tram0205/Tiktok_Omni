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

            AppDomain.CurrentDomain.BaseDirectory,

            "draft_slideshow.json");



        public SlideshowDraftDocument Load()

        {

            if (!File.Exists(DraftPath))

            {

                return new SlideshowDraftDocument();

            }



            try

            {

                var json = File.ReadAllText(DraftPath, TextFileEncoding.Utf8);

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

            var doc = document ?? new SlideshowDraftDocument();

            doc.Products = (doc.Products ?? new List<AiVideoGenInputItem>())

                .Where(p => p != null)

                .Select(NormalizeProduct)

                .ToList();



            try

            {

                File.WriteAllText(DraftPath, JsonConvert.SerializeObject(doc, Formatting.Indented), TextFileEncoding.Utf8NoBom);

            }

            catch

            {

                // ignored

            }

        }



        private static AiVideoGenInputItem NormalizeProduct(AiVideoGenInputItem item)

        {

            item.ProfileName = ProfileScopedPaths.ResolveProfileName(item.ProfileName);

            item.SourceKeyword = (item.SourceKeyword ?? string.Empty).Trim();

            item.ProductName = (item.ProductName ?? string.Empty).Trim();

            item.Price = (item.Price ?? string.Empty).Trim();

            item.ImageUrl = (item.ImageUrl ?? string.Empty).Trim();

            return item;

        }

    }

}

