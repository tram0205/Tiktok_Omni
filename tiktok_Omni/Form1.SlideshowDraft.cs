using System;

using System.Collections.Generic;

using System.Linq;

using System.Windows.Forms;

using tiktok_Omni.Services;



namespace tiktok_Omni

{

    public partial class Form1

    {

        private readonly SlideshowDraftStore _slideshowDraftStore = new SlideshowDraftStore();

        private System.Windows.Forms.Timer _slideshowDraftTimer;

        private bool _slideshowDraftDirty;



        private void InitializeSlideshowDraftAutoSave()

        {

            _slideshowDraftTimer?.Stop();

            _slideshowDraftTimer?.Dispose();

            _slideshowDraftTimer = new System.Windows.Forms.Timer { Interval = 30000 };

            _slideshowDraftTimer.Tick += SlideshowDraftTimer_Tick;

            _slideshowDraftTimer.Start();

        }



        private void SlideshowDraftTimer_Tick(object sender, EventArgs e)

        {

            if (!_slideshowDraftDirty)

            {

                return;

            }



            FlushSlideshowDraftToDisk();

        }



        private void FlushSlideshowDraftToDisk()

        {

            var doc = new SlideshowDraftDocument

            {

                Products = GetSlideshowBuffer() ?? new List<AiVideoGenInputItem>(),

                SharedScript = txtAiVideoGenPrompt?.Text ?? string.Empty

            };

            _slideshowDraftStore.Save(doc);

            _slideshowDraftDirty = false;

        }



        private void LoadSlideshowDraftIntoBuffer()

        {

            var doc = _slideshowDraftStore.Load();

            if (doc.Products == null || doc.Products.Count == 0)

            {

                return;

            }



            ReplaceSlideshowBuffer(doc.Products);

            if (!string.IsNullOrWhiteSpace(doc.SharedScript) && txtAiVideoGenPrompt != null)

            {

                txtAiVideoGenPrompt.Text = doc.SharedScript;

            }



            _slideshowDraftDirty = false;

            var processed = GetSlideshowBuffer().Count(x => x.IsProcessed);

            Log($"Slideshow: đã khôi phục {GetSlideshowBuffer().Count} sản phẩm từ draft_slideshow.json (đã render: {processed}).");

        }



        private void NotifySlideshowDraftDirty()

        {

            _slideshowDraftDirty = true;

        }

    }

}


