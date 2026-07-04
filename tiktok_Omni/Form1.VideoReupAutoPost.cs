using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private Button btnVideoReupPushToAutoPost;

        private void btnVideoReupPushToAutoPost_Click(object sender, EventArgs e)
        {
            PushVideoReupSelectionToAutoPost();
        }

        private void PushVideoReupSelectionToAutoPost()
        {
            var rows = GetVideoReupSelectedRowsOrdered();
            if (rows.Count == 0
                && dgvVideoReupInput?.CurrentRow?.DataBoundItem is VideoReupRowItem currentRow)
            {
                rows.Add(currentRow);
            }

            if (rows.Count == 0)
            {
                LogVideoReup("Chọn ít nhất một dòng đã render để đẩy sang tab Đăng tự động.");
                MessageBox.Show(
                    this,
                    "Chọn ít nhất một dòng đã render (Ctrl+click nhiều dòng).",
                    "Đẩy sang Đăng tự động",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var scheduleAdded = 0;
            var pushed = 0;
            var skipped = 0;
            var appliedUi = false;

            foreach (var row in rows)
            {
                var published = PublishVideoReupOutputForAutoPost(row, out var publishError);
                if (string.IsNullOrWhiteSpace(published))
                {
                    LogVideoReup(publishError);
                    skipped++;
                    continue;
                }

                if (!TryBuildAutoPostPlanFromVideoReupRow(row, published, out var plan, out var planError))
                {
                    LogVideoReup(planError);
                    skipped++;
                    continue;
                }

                if (!appliedUi)
                {
                    ApplyVideoReupPlanToAutoPostUi(plan, published);
                    appliedUi = true;
                }

                // Add to all 3 grids at once (multi-channel requirement)
                AddVideoToAllGrids(
                    videoPath:  published,
                    videoLabel: System.IO.Path.GetFileName(published),
                    caption:    StripHashtagsFromCaptionBody(plan.TikTokCaption, plan.TikTokHashtags),
                    link:       plan.AffiliateLink ?? string.Empty,
                    profile:    plan.Profile ?? "default",
                    ytTitle:    plan.YouTubeTitle,
                    ytDesc:     plan.YouTubeDescription);
                scheduleAdded += 3;

                pushed++;
            }

            if (pushed == 0)
            {
                MessageBox.Show(
                    this,
                    "Không đẩy được dòng nào. Các dòng cần đã «Render & Đóng gói» và có file MP4 thành phẩm.",
                    "Đẩy sang Đăng tự động",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RefreshAutoPostScheduleStatus();
            RefreshPlatformScheduleGrids();
            SwitchToMainTab(tabAutoPost);

            LogVideoReup(
                $"Đã đẩy {pushed} video sang tab Đăng tự động — {scheduleAdded} lịch (TikTok + Facebook + YouTube mỗi video). Bỏ qua: {skipped}.");
            ShowVideoReupBatchDoneMessage(
                "Đẩy sang Đăng tự động",
                pushed,
                skipped,
                rows.Count);
        }

        private string PublishVideoReupOutputForAutoPost(VideoReupRowItem row, out string error)
        {
            error = string.Empty;
            if (row == null)
            {
                error = "Dòng video reup không hợp lệ.";
                return string.Empty;
            }

            var source = (row.LastRemixOutputPath ?? row.OutputPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                error = "«" + (row.ProductName ?? "?") + "»: chưa có video thành phẩm — chạy «Render & Đóng gói» trước.";
                return string.Empty;
            }

            if (!AssetIntegrityService.CheckVideoFile(source, out var integrityError))
            {
                error = "«" + (row.ProductName ?? "?") + "»: " + integrityError;
                return string.Empty;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(row.ProfileName);
            ProfileScopedPaths.SetConfiguredStorageRoot(_storageRootPathCache);

            if (ProfileScopedPaths.IsUnderPublishingRoot(_storageRootPathCache, source))
            {
                return source;
            }

            var published = ProfileScopedPaths.CopyVideoToPublishing(
                _storageRootPathCache,
                profile,
                source,
                VideoStorageType.Reup);

            if (string.IsNullOrWhiteSpace(published) || !File.Exists(published))
            {
                error = "«" + (row.ProductName ?? "?") + "»: không copy được video vào thư mục Publishing.";
                return string.Empty;
            }

            return published;
        }

        private bool TryBuildAutoPostPlanFromVideoReupRow(
            VideoReupRowItem row,
            string publishedVideoPath,
            out OmnichannelAutoPostPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;

            if (row == null || string.IsNullOrWhiteSpace(publishedVideoPath) || !File.Exists(publishedVideoPath))
            {
                error = "Thiếu file video thành phẩm.";
                return false;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(row.ProfileName);
            var folder = Path.GetDirectoryName(publishedVideoPath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                error = "Thư mục Publishing không hợp lệ.";
                return false;
            }

            try
            {
                OneClickPipelineService.ValidateAutoPostInboxOnly(
                    _storageRootPathCache,
                    profile,
                    folder,
                    publishedVideoPath);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            var product = (row.ProductName ?? string.Empty).Trim();
            var hook = (row.ReupHookDraft ?? string.Empty).Trim();
            var hashtags = (row.Hashtags ?? string.Empty).Trim();
            var captionBody = string.IsNullOrWhiteSpace(hook)
                ? product
                : (string.IsNullOrWhiteSpace(product) ? hook : product + Environment.NewLine + Environment.NewLine + hook).Trim();

            if (string.IsNullOrWhiteSpace(captionBody))
            {
                captionBody = product;
            }

            if (string.IsNullOrWhiteSpace(captionBody))
            {
                error = "«" + (row.ProductName ?? "?") + "»: thiếu tên sản phẩm / hook để tạo caption đăng.";
                return false;
            }

            var tikTokCaption = BuildFinalAutoPostCaptionBody(captionBody, hashtags);
            var facebookCaption = BuildFinalAutoPostCaptionBody(captionBody, hashtags);
            var youtubeTitle = product;
            if (string.IsNullOrWhiteSpace(youtubeTitle))
            {
                youtubeTitle = hook;
            }

            if (youtubeTitle.Length > 60)
            {
                youtubeTitle = youtubeTitle.Substring(0, 60).Trim();
            }

            if (string.IsNullOrWhiteSpace(youtubeTitle))
            {
                error = "«" + (row.ProductName ?? "?") + "»: thiếu tiêu đề YouTube.";
                return false;
            }

            var affiliateLink = OmnichannelAutoPostFields.IsShopeeProductUrl(row.VideoUrl)
                ? OmnichannelAutoPostFields.NormalizeLink(row.VideoUrl)
                : string.Empty;

            var previewParts = new List<string>
            {
                "[TikTok] " + tikTokCaption,
                "[Facebook] " + facebookCaption,
                "[YouTube] " + youtubeTitle + " | " + captionBody
            };
            var combinedPreview = string.Join(" || ", previewParts);
            var fingerprintSource = BuildAutoPostFingerprintSource(
                folder,
                combinedPreview,
                hashtags + "|reup",
                profile,
                publishedVideoPath) + "|tiktok=true|facebook=true|youtube=true|reup=1";

            plan = new OmnichannelAutoPostPlan
            {
                VideoFolder = folder,
                VideoFilePath = publishedVideoPath,
                Profile = profile,
                PostTikTok = true,
                PostFacebook = true,
                PostYouTube = true,
                TikTokCaption = tikTokCaption,
                TikTokHashtags = hashtags,
                TikTokUploadOnly = false,
                FacebookCaption = facebookCaption,
                FacebookHashtags = hashtags,
                FacebookAttachShopeeLink = OmnichannelAutoPostFields.ShouldAttachFacebookShopeeLink(
                    OmnichannelAutoPostFields.IsShopeeProductUrl(affiliateLink),
                    affiliateLink),
                FacebookShopeeLink = affiliateLink,
                YouTubeTitle = youtubeTitle,
                YouTubeDescription = BuildFinalAutoPostCaptionBody(captionBody, hashtags),
                CombinedCaptionPreview = combinedPreview,
                PostFingerprint = fingerprintSource,
                AffiliateLink = affiliateLink,
                ProductId = string.Empty
            };

            return true;
        }

        private void ApplyVideoReupPlanToAutoPostUi(OmnichannelAutoPostPlan plan, string publishedVideoPath)
        {
            if (plan == null)
            {
                return;
            }

            var profile = ProfileScopedPaths.ResolveProfileName(plan.Profile);
            SelectRunningProfileInUi(profile);
            ApplyProfileScope(profile);

            if (cbAutoPostProfile != null)
            {
                var idx = cbAutoPostProfile.Items.IndexOf(profile);
                if (idx < 0)
                {
                    cbAutoPostProfile.Items.Add(profile);
                    idx = cbAutoPostProfile.Items.IndexOf(profile);
                }

                if (idx >= 0)
                {
                    cbAutoPostProfile.SelectedIndex = idx;
                }
            }

            if (cbAutoPostVideoType != null)
            {
                cbAutoPostVideoType.SelectedItem = VideoStorageType.Reup;
            }

            var folder = Path.GetDirectoryName(publishedVideoPath)
                ?? OneClickPipelineService.GetAutoPostInboxFolder(_storageRootPathCache, profile, VideoStorageType.Reup);

            if (txtAutoPostFolder != null)
            {
                txtAutoPostFolder.Text = folder;
            }

            RefreshAutoPostVideoCombo(publishedVideoPath);

            if (txtAutoPostCaption != null)
            {
                txtAutoPostCaption.Text = StripHashtagsFromCaptionBody(plan.TikTokCaption, plan.TikTokHashtags);
            }

            if (txtAutoPostHashtags != null)
            {
                txtAutoPostHashtags.Text = plan.TikTokHashtags ?? string.Empty;
            }

            if (txtAutoPostFbCaption != null)
            {
                txtAutoPostFbCaption.Text = StripHashtagsFromCaptionBody(plan.FacebookCaption, plan.FacebookHashtags);
            }

            if (txtAutoPostFbHashtags != null)
            {
                txtAutoPostFbHashtags.Text = plan.FacebookHashtags ?? string.Empty;
            }

            if (txtAutoPostFbShopeeLink != null)
            {
                txtAutoPostFbShopeeLink.Text = plan.FacebookShopeeLink ?? string.Empty;
            }

            if (chkAutoPostFbAttachShopee != null)
            {
                chkAutoPostFbAttachShopee.Checked = plan.FacebookAttachShopeeLink;
            }

            if (txtAutoPostYtTitle != null)
            {
                txtAutoPostYtTitle.Text = plan.YouTubeTitle ?? string.Empty;
            }

            if (txtAutoPostYtDescription != null)
            {
                txtAutoPostYtDescription.Text = plan.YouTubeDescription ?? string.Empty;
            }

            if (chkAutoPostEnableTikTok != null)
            {
                chkAutoPostEnableTikTok.Checked = true;
            }

            if (chkAutoPostEnableFacebook != null)
            {
                chkAutoPostEnableFacebook.Checked = true;
            }

            if (chkAutoPostEnableYouTube != null)
            {
                chkAutoPostEnableYouTube.Checked = true;
            }
        }

        private static string StripHashtagsFromCaptionBody(string caption, string hashtags)
        {
            var body = (caption ?? string.Empty).Trim();
            var tags = (hashtags ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(tags))
            {
                return body;
            }

            if (body.EndsWith(tags, StringComparison.OrdinalIgnoreCase))
            {
                return body.Substring(0, body.Length - tags.Length).TrimEnd();
            }

            return body;
        }
    }
}
