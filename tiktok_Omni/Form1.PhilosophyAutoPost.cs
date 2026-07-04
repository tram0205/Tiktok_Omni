using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private Button btnPhilosophyPushToAutoPost;

        private void btnPhilosophyPushToAutoPost_Click(object sender, EventArgs e)
        {
            PushPhilosophySelectionToAutoPost();
        }

        private void PushPhilosophySelectionToAutoPost()
        {
            var rows = GetPhilosophyTargetRowsFromGrid();
            if (rows.Count == 0)
            {
                LogPhilosophy("Chọn ít nhất một dòng đã render để đẩy sang tab Đăng tự động.");
                return;
            }

            var scheduleAdded = 0;
            var pushed = 0;
            var skipped = 0;
            var appliedUi = false;

            foreach (var item in rows)
            {
                var published = PublishPhilosophyOutputForAutoPost(item, out var publishError);
                if (string.IsNullOrWhiteSpace(published))
                {
                    LogPhilosophy(publishError);
                    skipped++;
                    continue;
                }

                if (!TryBuildAutoPostPlanFromPhilosophyRow(item, published, out var plan, out var planError))
                {
                    LogPhilosophy(planError);
                    skipped++;
                    continue;
                }

                if (!appliedUi)
                {
                    ApplyPhilosophyPlanToAutoPostUi(plan, published);
                    appliedUi = true;
                }

                if (TryAddAutoPostScheduleRow(plan, "TikTok", null, out _))
                {
                    scheduleAdded++;
                }

                if (TryAddAutoPostScheduleRow(plan, "Facebook", null, out _))
                {
                    scheduleAdded++;
                }

                if (TryAddAutoPostScheduleRow(plan, "YouTube", null, out _))
                {
                    scheduleAdded++;
                }

                pushed++;
            }

            if (pushed == 0)
            {
                MessageBox.Show(
                    this,
                    "Không đẩy được dòng nào. Các dòng cần đã render xong (Status «Xong») và có file MP4.",
                    "Đẩy sang Đăng tự động",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RefreshAutoPostScheduleStatus();
            RefreshPlatformScheduleGrids();
            SwitchToMainTab(tabAutoPost);

            LogPhilosophy(
                "Đã đẩy " + pushed + " video sang tab Đăng tự động — "
                + scheduleAdded + " lịch (TikTok + Facebook + YouTube mỗi video). Bỏ qua: " + skipped + ".");
        }

        private string PublishPhilosophyOutputForAutoPost(PhilosophyScriptItem item, out string error)
        {
            error = string.Empty;
            if (item == null)
            {
                error = "Dòng không hợp lệ.";
                return string.Empty;
            }

            var source = (item.OutputPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                error = "«" + TrimPhilosophyPreviewN(item.Content, 40) + "»: chưa có video — bấm «Bắt đầu Render» trước.";
                return string.Empty;
            }

            if (!AssetIntegrityService.CheckVideoFile(source, out var integrityError))
            {
                error = "«" + TrimPhilosophyPreviewN(item.Content, 40) + "»: " + integrityError;
                return string.Empty;
            }

            var profile = ResolvePhilosophyRowProfile(item);
            ProfileScopedPaths.SetConfiguredStorageRoot(_storageRootPathCache);

            if (ProfileScopedPaths.IsUnderPublishingRoot(_storageRootPathCache, source))
            {
                return source;
            }

            var published = ProfileScopedPaths.CopyVideoToPublishing(
                _storageRootPathCache,
                profile,
                source,
                VideoStorageType.Processed);

            if (string.IsNullOrWhiteSpace(published) || !File.Exists(published))
            {
                error = "«" + TrimPhilosophyPreviewN(item.Content, 40) + "»: không copy được video vào thư mục Publishing.";
                return string.Empty;
            }

            return published;
        }

        private bool TryBuildAutoPostPlanFromPhilosophyRow(
            PhilosophyScriptItem item,
            string publishedVideoPath,
            out OmnichannelAutoPostPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;

            if (item == null || string.IsNullOrWhiteSpace(publishedVideoPath) || !File.Exists(publishedVideoPath))
            {
                error = "Thiếu file video thành phẩm.";
                return false;
            }

            var profile = ResolvePhilosophyRowProfile(item);
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

            var captionBody = (item.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(captionBody))
            {
                error = "Dòng thiếu nội dung (Content) để tạo caption đăng.";
                return false;
            }

            var hashtags = BuildPhilosophyDefaultHashtags(item.Mood);
            var tikTokCaption = BuildFinalAutoPostCaptionBody(captionBody, hashtags);
            var facebookCaption = BuildFinalAutoPostCaptionBody(captionBody, hashtags);

            var youtubeTitle = captionBody;
            if (youtubeTitle.Length > 60)
            {
                youtubeTitle = youtubeTitle.Substring(0, 60).Trim();
            }

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
                hashtags + "|philosophy",
                profile,
                publishedVideoPath) + "|tiktok=true|facebook=true|youtube=true|philosophy=1";

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
                FacebookAttachShopeeLink = false,
                FacebookShopeeLink = string.Empty,
                YouTubeTitle = youtubeTitle,
                YouTubeDescription = BuildFinalAutoPostCaptionBody(captionBody, hashtags),
                CombinedCaptionPreview = combinedPreview,
                PostFingerprint = fingerprintSource,
                AffiliateLink = string.Empty,
                ProductId = string.Empty
            };

            return true;
        }

        private static string BuildPhilosophyDefaultHashtags(string mood)
        {
            var m = (mood ?? "reflective").Trim().ToLowerInvariant();
            var extra = m switch
            {
                "melancholic" => " #tamtrang #suytu",
                "hopeful" => " #hyvong #dongluc",
                "intense" => " #manhme #dongluc",
                "calm" => " #binhyen #thien",
                _ => " #suyngam #quotes"
            };

            return "#trietly #motivation #tiktokvietnam" + extra;
        }

        private void ApplyPhilosophyPlanToAutoPostUi(OmnichannelAutoPostPlan plan, string publishedVideoPath)
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
                cbAutoPostVideoType.SelectedItem = VideoStorageType.Processed;
            }

            var folder = Path.GetDirectoryName(publishedVideoPath)
                ?? OneClickPipelineService.GetAutoPostInboxFolder(
                    _storageRootPathCache,
                    profile,
                    VideoStorageType.Processed);

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
                txtAutoPostFbShopeeLink.Text = string.Empty;
            }

            if (chkAutoPostFbAttachShopee != null)
            {
                chkAutoPostFbAttachShopee.Checked = false;
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
    }
}
