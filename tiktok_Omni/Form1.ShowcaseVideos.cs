using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private List<ShowcaseVideoItem> _showcaseVideoBuffer = new List<ShowcaseVideoItem>();
        private Guid? _activeShowcaseVideoId;

        private List<ShowcaseVideoItem> GetShowcaseVideoBuffer() =>
            _showcaseVideoBuffer ?? (_showcaseVideoBuffer = new List<ShowcaseVideoItem>());

        private void EnsureShowcaseVideoBufferMigrated()
        {
            if (GetShowcaseVideoBuffer().Count > 0 || _deepDiveBuffer == null || _deepDiveBuffer.Count == 0)
            {
                return;
            }

            var groups = _deepDiveBuffer
                .Where(x => x != null)
                .GroupBy(x => (x.ProductName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count == 0)
            {
                return;
            }

            foreach (var group in groups)
            {
                var video = CreateShowcaseVideoRow(group.Key);
                foreach (var scene in group)
                {
                    video.Scenes.Add(scene);
                }

                video.CopySettingsFromScene(video.Scenes.FirstOrDefault());
                video.ApplySettingsToScenes();
                video.RefreshDisplayFields();
                GetShowcaseVideoBuffer().Add(video);
            }

            _deepDiveBuffer.Clear();
            if (!_activeShowcaseVideoId.HasValue && GetShowcaseVideoBuffer().Count > 0)
            {
                _activeShowcaseVideoId = GetShowcaseVideoBuffer()[0].VideoId;
            }
        }

        private ShowcaseVideoItem CreateShowcaseVideoRow(string productName = null)
        {
            var name = (productName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Video " + (GetShowcaseVideoBuffer().Count + 1);
            }

            return new ShowcaseVideoItem
            {
                ProfileName = GetRunningProfileName() ?? string.Empty,
                ProductName = name,
                PipelineStatus = "Chờ"
            };
        }

        public ShowcaseVideoItem GetActiveShowcaseVideo()
        {
            EnsureShowcaseVideoBufferMigrated();
            var buffer = GetShowcaseVideoBuffer();
            if (buffer.Count == 0)
            {
                return null;
            }

            if (_activeShowcaseVideoId.HasValue)
            {
                var active = buffer.FirstOrDefault(v => v.VideoId == _activeShowcaseVideoId.Value);
                if (active != null)
                {
                    return active;
                }
            }

            if (dgvDeepDiveInput?.CurrentRow?.DataBoundItem is ShowcaseVideoItem selected)
            {
                _activeShowcaseVideoId = selected.VideoId;
                return selected;
            }

            _activeShowcaseVideoId = buffer[0].VideoId;
            return buffer[0];
        }

        private bool ShouldShowShowcaseVideo(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return false;
            }

            if (chkAiVideoGenCurrentProfileOnly == null || !chkAiVideoGenCurrentProfileOnly.Checked)
            {
                return true;
            }

            var current = ProfileScopedPaths.ResolveProfileName(GetRunningProfileName());
            var itemProfile = ProfileScopedPaths.ResolveProfileName(video.ProfileName);
            return string.Equals(current, itemProfile, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshShowcaseVideoDisplayFields()
        {
            foreach (var video in GetShowcaseVideoBuffer())
            {
                video?.RefreshDisplayFields();
            }
        }

        private void SyncShowcaseVideoSettingsToScenes(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ApplySettingsToScenes();
            video.RefreshDisplayFields();
            dgvDeepDiveInput?.Invalidate();
        }

        private void SyncAllShowcaseVideoSettingsToScenes()
        {
            foreach (var video in GetShowcaseVideoBuffer())
            {
                SyncShowcaseVideoSettingsToScenes(video);
            }
        }

        void IAiVideoGenControlsHost.AddShowcaseVideoRow()
        {
            EnsureShowcaseVideoBufferMigrated();
            var video = CreateShowcaseVideoRow();
            GetShowcaseVideoBuffer().Add(video);
            _activeShowcaseVideoId = video.VideoId;
            _showcaseSession = null;
            SyncBuffersToGrids();
            SelectShowcaseVideoGridRow(video);
            RefreshAffiliateDeepStoryboard();
            RefreshAiVideoGenModeReadinessLabels();
            LogShowcase("[Showcase] Đã thêm dòng video «" + video.ProductName + "» — dùng cột «Ảnh» (➕ Thêm ảnh) để thêm cảnh.");
            NotifyShowcaseDraftDirty();
        }

        private void SelectShowcaseVideoGridRow(ShowcaseVideoItem video)
        {
            if (video == null || dgvDeepDiveInput == null || dgvDeepDiveInput.IsDisposed)
            {
                return;
            }

            foreach (DataGridViewRow row in dgvDeepDiveInput.Rows)
            {
                if (row.DataBoundItem is ShowcaseVideoItem bound && bound.VideoId == video.VideoId)
                {
                    row.Selected = true;
                    dgvDeepDiveInput.CurrentCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible && !c.ReadOnly)
                        ?? row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible)
                        ?? row.Cells[0];
                    break;
                }
            }
        }

        private void DgvDeepDiveInput_SelectionChanged_Showcase(object sender, EventArgs e)
        {
            if (!IsDeepDiveModeTab())
            {
                return;
            }

            if (dgvDeepDiveInput?.CurrentRow?.DataBoundItem is ShowcaseVideoItem video)
            {
                _activeShowcaseVideoId = video.VideoId;
                if (_showcaseSession != null &&
                    !string.Equals(_showcaseSession.ProductName, video.ProductName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    _showcaseSession = null;
                }

                RefreshAffiliateDeepStoryboard();
                RefreshAiVideoGenModeReadinessLabels();
                UpdateShowcaseRenderButtonState();
            }
        }

        private ShowcaseVideoItem EnsureActiveShowcaseVideoForImages()
        {
            var video = GetActiveShowcaseVideo();
            if (video != null)
            {
                return video;
            }

            ((IAiVideoGenControlsHost)this).AddShowcaseVideoRow();
            return GetActiveShowcaseVideo();
        }

        private void ReplaceShowcaseVideoBufferFromScenes(IEnumerable<AiVideoGenInputItem> items)
        {
            _showcaseVideoBuffer = new List<ShowcaseVideoItem>();
            _activeShowcaseVideoId = null;
            _deepDiveBuffer = items?
                .Where(x => x != null)
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList() ?? new List<AiVideoGenInputItem>();
            EnsureShowcaseVideoBufferMigrated();
            SyncBuffersToGrids();
        }

        /// <summary>Dòng video đang chọn trên lưới — theo thứ tự từ trên xuống (ổn định cho xử lý lô).</summary>
        private List<ShowcaseVideoItem> GetShowcaseSelectedVideosOrdered()
        {
            var result = new List<ShowcaseVideoItem>();
            if (dgvDeepDiveInput == null || dgvDeepDiveInput.IsDisposed)
            {
                return result;
            }

            var selectedRows = dgvDeepDiveInput.SelectedRows?.Cast<DataGridViewRow>()
                .Where(r => r?.DataBoundItem is ShowcaseVideoItem)
                .OrderBy(r => r.Index)
                .ToList();

            if (selectedRows != null && selectedRows.Count > 0)
            {
                foreach (var row in selectedRows)
                {
                    result.Add((ShowcaseVideoItem)row.DataBoundItem);
                }

                return result;
            }

            if (dgvDeepDiveInput.CurrentRow?.DataBoundItem is ShowcaseVideoItem current)
            {
                result.Add(current);
            }

            return result;
        }

        private bool TryGetShowcaseSelectedVideosOrdered(out List<ShowcaseVideoItem> videos, string emptySelectionMessage)
        {
            SaveDeepDiveGridState();
            videos = GetShowcaseSelectedVideosOrdered();
            if (videos.Count > 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(emptySelectionMessage))
            {
                MessageBox.Show(this, emptySelectionMessage, "Showcase sản phẩm",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return false;
        }

        private void ActivateShowcaseVideo(ShowcaseVideoItem video, bool refreshStoryboard = true)
        {
            if (video == null)
            {
                return;
            }

            _activeShowcaseVideoId = video.VideoId;
            if (_showcaseSession != null &&
                !string.Equals(_showcaseSession.ProductName, video.ProductName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                _showcaseSession = null;
            }

            var productName = (video.ProductName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(productName))
            {
                var session = EnsureShowcaseSession(GetRunningProfileName(), productName, null, video);
                var scenes = GetShowcaseVideoScenes(video);
                if (session != null && scenes.Count > 0)
                {
                    SyncShowcaseSourceImagesForVideo(video, refreshUi: false);
                    ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, video.Scenes, LogShowcase);
                    video.RefreshDisplayFields();
                }
            }

            SelectShowcaseVideoGridRow(video);
            if (refreshStoryboard)
            {
                RefreshAffiliateDeepStoryboard();
                RefreshAiVideoGenModeReadinessLabels();
            }
            else
            {
                UpdateShowcaseNarrationButtonState();
            }
        }

        private List<AiVideoGenInputItem> GetShowcaseVideoScenes(ShowcaseVideoItem video)
        {
            if (video?.Scenes == null || video.Scenes.Count == 0)
            {
                return new List<AiVideoGenInputItem>();
            }

            return video.Scenes.ToList();
        }

        private string ResolveShowcaseSourceImagesDirForSync(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return null;
            }

            var productName = (video.ProductName ?? string.Empty).Trim();
            if (_showcaseSession != null
                && string.Equals(_showcaseSession.ProductName, productName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_showcaseSession.SourceImagesDir)
                && Directory.Exists(_showcaseSession.SourceImagesDir))
            {
                return _showcaseSession.SourceImagesDir;
            }

            var sessionBase = (video.ShowcaseSessionBaseDir ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sessionBase))
            {
                var dir = Path.Combine(sessionBase, "source_images");
                if (Directory.Exists(dir))
                {
                    return dir;
                }
            }

            return ShowcaseSessionService.ResolveSceneImagesDirectory(GetShowcaseVideoScenes(video));
        }

        /// <summary>Đồng bộ storyboard với source_images — gỡ cảnh khi ảnh đã xóa ngoài app.</summary>
        private int SyncShowcaseSourceImagesForVideo(ShowcaseVideoItem video, bool refreshUi)
        {
            if (video?.Scenes == null || video.Scenes.Count == 0)
            {
                return 0;
            }

            var imagesDir = ResolveShowcaseSourceImagesDirForSync(video);
            var removed = ShowcaseSessionService.PruneScenesMissingSourceImages(imagesDir, video.Scenes, LogShowcase);
            if (removed <= 0)
            {
                ShowcaseSessionService.RefreshSourceImageStatus(imagesDir, video.Scenes, log: null);
                return 0;
            }

            NotifyShowcaseDraftDirty();
            var productName = (video.ProductName ?? string.Empty).Trim();
            if (_showcaseSession != null
                && string.Equals(_showcaseSession.ProductName, productName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_showcaseSession.ClipsDir))
            {
                ShowcaseSessionService.RefreshClipStatus(_showcaseSession.ClipsDir, video.Scenes, LogShowcase);
            }

            video.RefreshDisplayFields();
            if (refreshUi)
            {
                SyncBuffersToGrids();
                RefreshAiVideoGenModeReadinessLabels();
            }

            return removed;
        }

        private string GetShowcaseThemeForVideo(ShowcaseVideoItem video)
        {
            return ShowcaseThemePresets.ResolveUserThemeForGemini(
                video?.ShowcaseThemePrompt,
                video?.ShowcaseUserTheme);
        }

        private string GetShowcaseProductTypeForVideo(ShowcaseVideoItem video)
        {
            return ShowcaseProductTypePresets.ResolvePromptForGemini(video?.ShowcaseProductTypePrompt);
        }

        private string GetShowcaseClipModeForVideo(ShowcaseVideoItem video)
        {
            return ShowcaseClipModePresets.ResolveIdForGemini(video?.ShowcaseClipModeId);
        }

        private string GetShowcaseOutputAspectIdForVideo(ShowcaseVideoItem video)
        {
            return ShowcaseOutputAspectPresets.ResolveId(video?.ShowcaseOutputAspectId, null);
        }

        private ShowcaseOutputAspectPreset GetShowcaseOutputCanvasForVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            return ShowcaseOutputAspectPresets.Resolve(
                video?.ShowcaseOutputAspectId,
                settings?.ShowcaseOutputAspectDefault);
        }

        private ShowcaseVideoItem FindShowcaseVideoById(Guid videoId)
        {
            if (videoId == Guid.Empty)
            {
                return null;
            }

            return GetShowcaseVideoBuffer().FirstOrDefault(v => v != null && v.VideoId == videoId);
        }

        private int FindShowcaseVideoGridRowIndex(ShowcaseVideoItem video)
        {
            if (video == null || dgvDeepDiveInput == null || dgvDeepDiveInput.IsDisposed)
            {
                return -1;
            }

            for (var i = 0; i < dgvDeepDiveInput.Rows.Count; i++)
            {
                if (dgvDeepDiveInput.Rows[i].DataBoundItem is ShowcaseVideoItem bound && bound.VideoId == video.VideoId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
