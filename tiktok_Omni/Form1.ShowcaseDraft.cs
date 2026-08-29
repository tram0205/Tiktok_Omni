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
        private bool _showcaseAllowShrinkDraftSave;

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

            if (doc.Videos.Count == 0)
            {
                var existing = _showcaseDraftStore.LoadPrimaryFile();
                if (existing.Videos != null && existing.Videos.Count > 0)
                {
                    LogShowcase("[Showcase] Bỏ qua ghi draft rỗng — giữ " + existing.Videos.Count +
                                " dòng đã lưu trong draft_showcase.json.");
                    _showcaseDraftDirty = false;
                    return;
                }
            }

            if (!_showcaseAllowShrinkDraftSave)
            {
                doc = MergeShowcaseDraftPreservingDiskRows(doc);
            }

            _showcaseDraftStore.Save(doc);
            _showcaseDraftDirty = false;
            _showcaseAllowShrinkDraftSave = false;
        }

        /// <summary>Giữ dòng trên đĩa nếu buffer thiếu (tránh ghi đè do lưới chưa nạp draft).</summary>
        private ShowcaseDraftDocument MergeShowcaseDraftPreservingDiskRows(ShowcaseDraftDocument incoming)
        {
            incoming = incoming ?? new ShowcaseDraftDocument();
            incoming.Videos = incoming.Videos ?? new List<ShowcaseVideoDraftEntry>();

            var onDisk = _showcaseDraftStore.LoadPrimaryFile();
            var diskVideos = onDisk?.Videos ?? new List<ShowcaseVideoDraftEntry>();
            if (diskVideos.Count <= incoming.Videos.Count)
            {
                return incoming;
            }

            var merged = new List<ShowcaseVideoDraftEntry>(incoming.Videos);
            var kept = 0;
            foreach (var diskVideo in diskVideos)
            {
                if (diskVideo == null)
                {
                    continue;
                }

                if (merged.Any(v => v != null && v.VideoId == diskVideo.VideoId))
                {
                    continue;
                }

                merged.Add(diskVideo);
                kept++;
            }

            if (kept <= 0)
            {
                return incoming;
            }

            LogShowcase("[Showcase] Buffer thiếu " + kept + " dòng so với draft trên đĩa — đã giữ lại (tránh mất dữ liệu).");
            incoming.Videos = merged;
            return incoming;
        }

        internal void AllowShowcaseDraftShrinkOnNextSave()
        {
            _showcaseAllowShrinkDraftSave = true;
        }

        private void LoadShowcaseDraftIntoBuffer()
        {
            var doc = _showcaseDraftStore.Load();
            if (doc.Videos == null || doc.Videos.Count == 0)
            {
                LogShowcase("[Showcase] Không có dòng video trong draft_showcase.json — bấm «+ Thêm dòng» để bắt đầu.");
                return;
            }

            ApplyShowcaseDraftDocumentToBuffer(doc);

            var sceneCount = _showcaseVideoBuffer.Sum(v => v.SceneCount);
            LogShowcase("[Showcase] Đã khôi phục " + _showcaseVideoBuffer.Count + " dòng video (" + sceneCount +
                        " cảnh) từ draft_showcase.json.");
            TryAutoLinkShowcaseSessionsFromDisk();
            _showcaseDraftDirty = false;
        }

        private void TryAutoLinkShowcaseSessionsFromDisk()
        {
            var buffer = GetShowcaseVideoBuffer();
            if (buffer == null || buffer.Count == 0)
            {
                return;
            }

            if (!buffer.Any(ShowcaseSessionRestoreHelper.NeedsDiskRestore))
            {
                return;
            }

            var linked = ShowcaseSessionRestoreHelper.RestoreVideosFromDisk(
                GetRunningProfileName(),
                buffer,
                LogShowcase);
            if (linked <= 0)
            {
                return;
            }

            foreach (var video in buffer)
            {
                video?.RefreshDisplayFields();
            }

            NotifyShowcaseDraftDirty();
            LogShowcase("[Showcase] Tự gắn " + linked + " dòng với phiên Showcase trên đĩa.");
        }

        /// <summary>Nạp lại buffer + lưới Showcase sau khởi động (tránh lưới trống dù draft còn dữ liệu).</summary>
        private void EnsureShowcaseGridHydratedAfterStartup()
        {
            var doc = _showcaseDraftStore.Load();
            var draftCount = doc.Videos?.Count ?? 0;
            var bufferCount = GetShowcaseVideoBuffer().Count;

            if (draftCount > bufferCount)
            {
                if (bufferCount == 0)
                {
                    LogShowcase("[Showcase] Buffer trống — nạp lại " + draftCount + " dòng từ draft.");
                }
                else
                {
                    LogShowcase("[Showcase] Buffer có " + bufferCount + " dòng nhưng draft có " + draftCount +
                                " — đồng bộ lại từ draft.");
                }

                ApplyShowcaseDraftDocumentToBuffer(doc);
                _showcaseDraftDirty = false;
            }

            if (_selectedAiVideoGenMode == AiVideoGenMode.AffiliateDeep)
            {
                ApplyAffiliateDeepControlHosts(1);
            }

            SyncBuffersToGrids();
            dgvDeepDiveInput?.Refresh();
            TryAutoLinkShowcaseSessionsFromDisk();
        }

        private void ApplyShowcaseDraftDocumentToBuffer(ShowcaseDraftDocument doc)
        {
            if (doc?.Videos == null || doc.Videos.Count == 0)
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

            var profile = GetRunningProfileName();
            foreach (var video in _showcaseVideoBuffer)
            {
                if (video == null)
                {
                    continue;
                }

                var clipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(
                    video.ShowcaseSessionBaseDir,
                    createIfMissing: false,
                    migrateLegacy: true);
                if (string.IsNullOrWhiteSpace(clipsDir))
                {
                    clipsDir = (video.ShowcaseClipsDir ?? string.Empty).Trim();
                }

                ShowcaseSessionService.EnsureScenesFromRenderFolder(video, clipsDir, profile, LogShowcase);
                video.RefreshDisplayFields();
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
        }

        private void NotifyShowcaseDraftDirty()
        {
            _showcaseDraftDirty = true;
        }
    }
}
