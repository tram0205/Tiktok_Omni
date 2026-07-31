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
        void IAiVideoGenControlsHost.PushShowcaseSelectionToAutoPost()
        {
            PushShowcaseSelectionToAutoPost();
        }

        private void PushShowcaseSelectionToAutoPost()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video đã render để đẩy sang tab Đăng tự động."))
            {
                return;
            }

            var scheduleAdded = 0;
            var pushed = 0;
            var skipped = 0;
            var appliedUi = false;

            foreach (var video in videos)
            {
                var published = PublishShowcaseOutputForAutoPost(video, out var publishError);
                if (string.IsNullOrWhiteSpace(published))
                {
                    LogShowcase(publishError);
                    skipped++;
                    continue;
                }

                if (!TryBuildAutoPostPlanFromShowcaseRow(video, published, out var plan, out var planError))
                {
                    LogShowcase(planError);
                    skipped++;
                    continue;
                }

                EnsureProfileComboIncludes(plan.Profile);
                ApplyAutoPostScheduleProfileComboColumns();

                if (!appliedUi)
                {
                    ApplyShowcasePlanToAutoPostUi(plan, published);
                    appliedUi = true;
                }

                AddVideoToAllGrids(
                    videoPath: published,
                    videoLabel: Path.GetFileName(published),
                    caption: StripHashtagsFromCaptionBody(plan.TikTokCaption, plan.TikTokHashtags),
                    link: plan.AffiliateLink ?? string.Empty,
                    profile: plan.Profile ?? "default",
                    ytTitle: plan.YouTubeTitle,
                    ytDesc: StripHashtagsFromCaptionBody(plan.YouTubeDescription, plan.TikTokHashtags),
                    hashtag: plan.TikTokHashtags);
                scheduleAdded += 3;
                pushed++;
            }

            if (pushed == 0)
            {
                MessageBox.Show(
                    this,
                    "Không đẩy được dòng nào. Các dòng cần đã render xong và có file MP4 thành phẩm.",
                    "Đẩy sang Đăng tự động",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RefreshAutoPostScheduleStatus();
            RefreshPlatformScheduleGrids();
            SwitchToMainTab(tabAutoPost);

            LogShowcase(
                "Đã đẩy " + pushed + " video Showcase sang tab Đăng tự động — "
                + scheduleAdded + " lịch (TikTok + Facebook + YouTube mỗi video). Bỏ qua: " + skipped + ".");
        }

        private string PublishShowcaseOutputForAutoPost(ShowcaseVideoItem video, out string error)
        {
            error = string.Empty;
            if (video == null)
            {
                error = "Dòng video không hợp lệ.";
                return string.Empty;
            }

            var label = (video.ProductName ?? "(Không tên)").Trim();
            var source = ShowcaseContentDisplayHelper.TryResolveFinishedVideoPath(video);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                error = "«" + label + "»: chưa có video thành phẩm — bấm «Render video» trước.";
                return string.Empty;
            }

            if (!AssetIntegrityService.CheckVideoFile(source, out var integrityError))
            {
                error = "«" + label + "»: " + integrityError;
                return string.Empty;
            }

            var profile = ResolveShowcaseRowProfileForAutoPost(video);
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
                error = "«" + label + "»: không copy được video vào thư mục Publishing.";
                return string.Empty;
            }

            return published;
        }

        private bool TryBuildAutoPostPlanFromShowcaseRow(
            ShowcaseVideoItem video,
            string publishedVideoPath,
            out OmnichannelAutoPostPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;

            if (video == null || string.IsNullOrWhiteSpace(publishedVideoPath) || !File.Exists(publishedVideoPath))
            {
                error = "Thiếu file video thành phẩm.";
                return false;
            }

            var profile = ResolveShowcaseRowProfileForAutoPost(video);
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

            var captionBody = BuildShowcaseAutoPostCaptionBody(video);
            if (string.IsNullOrWhiteSpace(captionBody))
            {
                error = "«" + (video.ProductName ?? "?") + "»: thiếu hook / thoại để tạo caption đăng.";
                return false;
            }

            var hashtags = BuildShowcaseDefaultHashtags(video);
            var tikTokCaption = BuildFinalAutoPostCaptionBody(captionBody, hashtags);
            var facebookCaption = BuildFinalAutoPostCaptionBody(captionBody, hashtags);

            var youtubeTitle = (video.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(youtubeTitle))
            {
                youtubeTitle = (video.ShowcaseHookText ?? string.Empty).Trim();
            }

            if (youtubeTitle.Length > 60)
            {
                youtubeTitle = youtubeTitle.Substring(0, 60).Trim();
            }

            if (string.IsNullOrWhiteSpace(youtubeTitle))
            {
                youtubeTitle = TruncateAutoPostTitle(captionBody, 60);
            }

            if (string.IsNullOrWhiteSpace(youtubeTitle))
            {
                error = "«" + (video.ProductName ?? "?") + "»: thiếu tiêu đề YouTube.";
                return false;
            }

            var affiliateLink = ResolveShowcaseAffiliateLink(video);
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
                hashtags + "|showcase",
                profile,
                publishedVideoPath) + "|tiktok=true|facebook=true|youtube=true|showcase=1";

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

        private void ApplyShowcasePlanToAutoPostUi(OmnichannelAutoPostPlan plan, string publishedVideoPath)
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

            if (txtAutoPostAffiliateLink != null)
            {
                txtAutoPostAffiliateLink.Text = plan.AffiliateLink ?? string.Empty;
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

        private string ResolveShowcaseRowProfileForAutoPost(ShowcaseVideoItem video)
        {
            var fromRow = (video?.ProfileName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(fromRow))
            {
                return ProfileScopedPaths.ResolveProfileName(fromRow);
            }

            return ProfileScopedPaths.ResolveProfileName(GetRunningProfileName());
        }

        private static string ResolveShowcaseAffiliateLink(ShowcaseVideoItem video)
        {
            if (video?.Scenes == null)
            {
                return string.Empty;
            }

            foreach (var scene in video.Scenes)
            {
                if (scene == null)
                {
                    continue;
                }

                var affiliate = OmnichannelAutoPostFields.NormalizeLink(scene.AffiliateLink);
                if (OmnichannelAutoPostFields.IsShopeeProductUrl(affiliate))
                {
                    return affiliate;
                }

                var videoUrl = OmnichannelAutoPostFields.NormalizeLink(scene.VideoUrl);
                if (OmnichannelAutoPostFields.IsShopeeProductUrl(videoUrl))
                {
                    return videoUrl;
                }
            }

            return string.Empty;
        }

        private static string BuildShowcaseAutoPostCaptionBody(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return string.Empty;
            }

            var hook = (video.ShowcaseHookText ?? string.Empty).Trim();
            var cta = (video.ShowcaseCtaText ?? string.Empty).Trim();
            var scenes = video.Scenes?.Where(s => s != null).ToList() ?? new List<AiVideoGenInputItem>();
            var narration = (ShowcaseNarrationCacheHelper.BuildNarrationScript(scenes) ?? string.Empty).Trim();

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(hook))
            {
                parts.Add(hook);
            }

            if (!string.IsNullOrEmpty(narration))
            {
                if (string.IsNullOrEmpty(hook)
                    || narration.IndexOf(hook, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    parts.Add(narration);
                }
                else if (parts.Count == 0)
                {
                    parts.Add(narration);
                }
            }

            if (!string.IsNullOrEmpty(cta)
                && (parts.Count == 0
                    || !ShowcaseCtaDedupHelper.PassageContainsCta(parts[parts.Count - 1], cta)))
            {
                parts.Add(cta);
            }

            if (parts.Count > 0)
            {
                return string.Join(Environment.NewLine + Environment.NewLine, parts);
            }

            var product = (video.ProductName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(product))
            {
                return product;
            }

            return (video.ShowcaseThemeGridDisplay ?? string.Empty).Trim();
        }

        private static string BuildShowcaseDefaultHashtags(ShowcaseVideoItem video)
        {
            var product = (video?.ProductName ?? string.Empty).Trim();
            if (product.Length > 0)
            {
                return "#showcase #tiktokshop #review #fyp #muasam";
            }

            return "#showcase #tiktokvietnam #review #fyp";
        }
    }
}
