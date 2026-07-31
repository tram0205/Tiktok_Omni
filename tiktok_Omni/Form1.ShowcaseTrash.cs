using System;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly ShowcaseTrashStore _showcaseTrashStore = new ShowcaseTrashStore();

        private void InitializeShowcaseTrashMaintenance()
        {
            PurgeShowcaseTrashExpired(logWhenRemoved: false);
            RefreshShowcaseTrashButtonLabel();
        }

        private void PurgeShowcaseTrashExpired(bool logWhenRemoved)
        {
            var removed = _showcaseTrashStore.PurgeExpired();
            if (removed > 0)
            {
                RefreshShowcaseTrashButtonLabel();
                if (logWhenRemoved)
                {
                    LogShowcase("[Showcase] Thùng rác: đã xóa " + removed + " dòng quá 24 giờ.");
                }
            }
        }

        private void RefreshShowcaseTrashButtonLabel()
        {
            _aiVideoGenControls?.SetShowcaseTrashButtonCount(_showcaseTrashStore.CountActive());
        }

        void IAiVideoGenControlsHost.OpenShowcaseTrash()
        {
            PurgeShowcaseTrashExpired(logWhenRemoved: true);
            EnsureShowcaseVideoBufferMigrated();

            using (var dlg = new ShowcaseTrashForm(_showcaseTrashStore, CloneAiVideoGenItem))
            {
                dlg.ShowDialog(this);
                if (dlg.RestoredVideos.Count == 0)
                {
                    RefreshShowcaseTrashButtonLabel();
                    return;
                }

                var buffer = GetShowcaseVideoBuffer();
                ShowcaseVideoItem lastRestored = null;
                foreach (var restored in dlg.RestoredVideos)
                {
                    if (restored == null)
                    {
                        continue;
                    }

                    EnsureUniqueShowcaseVideoIdForRestore(restored, buffer);
                    buffer.Add(restored);
                    lastRestored = restored;
                }

                if (lastRestored == null)
                {
                    RefreshShowcaseTrashButtonLabel();
                    return;
                }

                _activeShowcaseVideoId = lastRestored.VideoId;
                _showcaseSession = null;
                SyncBuffersToGrids();
                SelectShowcaseVideoGridRow(lastRestored);
                RefreshAffiliateDeepStoryboard();
                RefreshAiVideoGenModeReadinessLabels();
                NotifyShowcaseDraftDirty();
                RefreshShowcaseTrashButtonLabel();
                LogShowcase("[Showcase] Đã khôi phục " + dlg.RestoredVideos.Count +
                            " dòng từ thùng rác («" + lastRestored.ProductName + "»).");
            }
        }

        private static void EnsureUniqueShowcaseVideoIdForRestore(ShowcaseVideoItem video, System.Collections.Generic.List<ShowcaseVideoItem> buffer)
        {
            if (video == null || buffer == null)
            {
                return;
            }

            if (buffer.Any(v => v != null && v.VideoId == video.VideoId))
            {
                video.VideoId = Guid.NewGuid();
            }
        }

        private void MoveShowcaseVideosToTrash(System.Collections.Generic.IEnumerable<ShowcaseVideoItem> videos)
        {
            if (videos == null)
            {
                return;
            }

            var count = 0;
            foreach (var video in videos)
            {
                if (video == null)
                {
                    continue;
                }

                _showcaseTrashStore.AddFromVideo(video, CloneAiVideoGenItem);
                count++;
            }

            if (count > 0)
            {
                RefreshShowcaseTrashButtonLabel();
            }
        }
    }
}
