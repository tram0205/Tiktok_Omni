using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly ShowcaseDraftStore _showcaseDraftStore = new ShowcaseDraftStore();
        private System.Windows.Forms.Timer _showcaseDraftTimer;
        private bool _showcaseDraftDirty;

        private void InitializeShowcaseDraftAutoSave()
        {
            _showcaseDraftTimer?.Stop();
            _showcaseDraftTimer?.Dispose();
            _showcaseDraftTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _showcaseDraftTimer.Tick += ShowcaseDraftTimer_Tick;
            _showcaseDraftTimer.Start();
        }

        private void ShowcaseDraftTimer_Tick(object sender, EventArgs e)
        {
            PurgeShowcaseTrashExpired(logWhenRemoved: false);

            if (!_showcaseDraftDirty)
            {
                return;
            }

            FlushShowcaseDraftToDisk();
        }

        public void FlushShowcaseDraftToDisk()
        {
            SaveDeepDiveGridState();

            var doc = new ShowcaseDraftDocument
            {
                ActiveVideoId = _activeShowcaseVideoId,
                Videos = GetShowcaseVideoBuffer()
                    .Where(v => v != null)
                    .Select(v => ShowcaseDraftStore.FromVideo(v, CloneAiVideoGenItem))
                    .ToList(),
                Session = ShowcaseDraftStore.FromSession(_showcaseSession)
            };

            _showcaseDraftStore.Save(doc);
            _showcaseDraftDirty = false;
        }

        private void LoadShowcaseDraftIntoBuffer()
        {
            var doc = _showcaseDraftStore.Load();
            if (doc.Videos == null || doc.Videos.Count == 0)
            {
                return;
            }

            _showcaseVideoBuffer = doc.Videos
                .Select(v => ShowcaseDraftStore.ToVideo(v, CloneAiVideoGenItem))
                .Where(v => v != null)
                .ToList();
            foreach (var video in _showcaseVideoBuffer)
            {
                video?.RefreshDisplayFields();
            }
            _deepDiveBuffer = new List<AiVideoGenInputItem>();
            _activeShowcaseVideoId = doc.ActiveVideoId;
            if (_activeShowcaseVideoId.HasValue &&
                !_showcaseVideoBuffer.Any(v => v.VideoId == _activeShowcaseVideoId.Value))
            {
                _activeShowcaseVideoId = _showcaseVideoBuffer.FirstOrDefault()?.VideoId;
            }

            _showcaseSession = ShowcaseDraftStore.ToSession(doc.Session);
            if (_showcaseSession != null && !string.IsNullOrWhiteSpace(_showcaseSession.ClipsDir))
            {
                foreach (var video in _showcaseVideoBuffer)
                {
                    if (video != null &&
                        string.Equals(video.ProductName, _showcaseSession.ProductName, StringComparison.OrdinalIgnoreCase))
                    {
                        BindShowcaseSessionToVideo(_showcaseSession, video);
                    }
                }
            }
            var activeVideo = GetActiveShowcaseVideo();
            if (activeVideo != null && _showcaseSession != null)
            {
                SyncShowcaseSourceImagesForVideo(activeVideo, refreshUi: false);
                ShowcaseSessionService.RefreshClipStatus(_showcaseSession.ClipsDir, activeVideo.Scenes, LogShowcase);
                activeVideo.RefreshDisplayFields();
            }

            SyncBuffersToGrids();
            RefreshAffiliateDeepStoryboard();
            RefreshAiVideoGenModeReadinessLabels();

            var sceneCount = _showcaseVideoBuffer.Sum(v => v.SceneCount);
            LogShowcase("[Showcase] Đã khôi phục " + _showcaseVideoBuffer.Count + " dòng video (" + sceneCount +
                        " cảnh) từ draft_showcase.json.");
            _showcaseDraftDirty = false;
        }

        private void NotifyShowcaseDraftDirty()
        {
            _showcaseDraftDirty = true;
        }
    }
}
