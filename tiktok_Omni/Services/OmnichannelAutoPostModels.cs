using System;

namespace tiktok_Omni.Services

{

    /// <summary>Chuẩn hóa link/product id cho Auto Post (có link vs nuôi kênh).</summary>

    public static class OmnichannelAutoPostFields

    {

        public static string NormalizeLink(string value)

        {

            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        }



        /// <summary>Chỉ bật luồng gắn sản phẩm TikTok khi có URL affiliate hợp lệ.</summary>

        public static bool ShouldAttachAffiliateProduct(string affiliateLink, string productId = null)

        {

            return !string.IsNullOrWhiteSpace(NormalizeLink(affiliateLink));

        }

        public static bool IsShopeeProductUrl(string url)

        {

            var normalized = NormalizeLink(url);

            if (string.IsNullOrWhiteSpace(normalized))

            {

                return false;

            }



            return normalized.IndexOf("shopee", StringComparison.OrdinalIgnoreCase) >= 0

                || normalized.IndexOf("shp.ee", StringComparison.OrdinalIgnoreCase) >= 0;

        }



        public static bool ShouldAttachFacebookShopeeLink(bool attachEnabled, string shopeeLink)

        {

            return attachEnabled && IsShopeeProductUrl(shopeeLink);

        }



        public static string EnsureLinkInCaption(string caption, string link)

        {

            var body = caption?.Trim() ?? string.Empty;

            var normalizedLink = NormalizeLink(link);

            if (string.IsNullOrWhiteSpace(normalizedLink))

            {

                return body;

            }



            if (body.IndexOf(normalizedLink, StringComparison.OrdinalIgnoreCase) >= 0)

            {

                return body;

            }



            return string.IsNullOrWhiteSpace(body)

                ? normalizedLink

                : body + Environment.NewLine + Environment.NewLine + "🛒 " + normalizedLink;

        }

    }



    public sealed class OmnichannelAutoPostPlan

    {

        public string VideoFolder { get; set; } = string.Empty;

        public string VideoFilePath { get; set; } = string.Empty;

        public string Profile { get; set; } = string.Empty;

        public bool PostTikTok { get; set; }

        public bool PostFacebook { get; set; }

        public bool PostYouTube { get; set; }

        public string TikTokCaption { get; set; } = string.Empty;

        public string TikTokHashtags { get; set; } = string.Empty;

        public bool TikTokUploadOnly { get; set; }

        public string FacebookCaption { get; set; } = string.Empty;

        public string FacebookHashtags { get; set; } = string.Empty;

        public bool FacebookAttachShopeeLink { get; set; }

        public string FacebookShopeeLink { get; set; } = string.Empty;

        public string YouTubeTitle { get; set; } = string.Empty;

        public string YouTubeDescription { get; set; } = string.Empty;

        public string CombinedCaptionPreview { get; set; } = string.Empty;

        public string PostFingerprint { get; set; } = string.Empty;

        /// <summary>
        /// When true, skips the profile-scoped folder check so that any video file
        /// can be posted regardless of where it lives on disk.
        /// Used by the manual Auto Post Schedule queue.
        /// </summary>
        public bool SkipFolderScopeCheck { get; set; }



        /// <summary>Null hoặc rỗng = video nuôi kênh (không gắn sản phẩm).</summary>

        public string AffiliateLink { get; set; }



        /// <summary>Null hoặc rỗng khi không có sản phẩm affiliate.</summary>

        public string ProductId { get; set; }

    }



    public sealed class AutoPostApprovalPayload

    {

        public string VideoFolder { get; set; } = string.Empty;

        public string Hashtags { get; set; } = string.Empty;

        public string Profile { get; set; } = string.Empty;

        public string ProfileName { get; set; } = string.Empty;

        public string VideoFilePath { get; set; } = string.Empty;

        public string CaptionFull { get; set; } = string.Empty;

        public bool UploadOnlyNoPublish { get; set; }

        public bool PostTikTok { get; set; } = true;

        public bool PostFacebook { get; set; }

        public bool PostYouTube { get; set; }

        public string FacebookCaption { get; set; } = string.Empty;

        public string FacebookHashtags { get; set; } = string.Empty;

        public bool FacebookAttachShopeeLink { get; set; }

        public string FacebookShopeeLink { get; set; } = string.Empty;

        public string YouTubeTitle { get; set; } = string.Empty;

        public string YouTubeDescription { get; set; } = string.Empty;



        /// <summary>Null hoặc rỗng = nuôi kênh.</summary>

        public string AffiliateLink { get; set; }



        public string ProductId { get; set; }

    }

    /// <summary>Gắn link affiliate theo quyết định tại bước duyệt.</summary>
    public static class ApprovalAffiliateTagging
    {
        public static void EnsureTargetFields(ApprovalQueueItem item, string fallbackLink, string fallbackProductId)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(item.TargetAffiliateLink))
            {
                item.TargetAffiliateLink = OmnichannelAutoPostFields.NormalizeLink(
                    fallbackLink ?? item.AffiliateLink);
            }

            if (string.IsNullOrWhiteSpace(item.TargetProductId))
            {
                item.TargetProductId = OmnichannelAutoPostFields.NormalizeLink(
                    fallbackProductId ?? item.ProductId);
            }
        }

        public static bool AllowsAffiliateCheckbox(ApprovalQueueItem item)
        {
            if (item == null || item.JobType == ApprovalJobType.PhilosophyVideo)
            {
                return false;
            }

            return OmnichannelAutoPostFields.ShouldAttachAffiliateProduct(
                item.TargetAffiliateLink,
                item.TargetProductId);
        }

        public static void ApplyReviewerAffiliateChoice(ApprovalQueueItem item, bool reviewerWantsAffiliate)
        {
            if (item == null)
            {
                return;
            }

            if (item.JobType == ApprovalJobType.PhilosophyVideo)
            {
                item.CanAttachAffiliate = false;
                return;
            }

            item.CanAttachAffiliate = reviewerWantsAffiliate && AllowsAffiliateCheckbox(item);
        }

        public static void ResolveLinksForAutoPost(ApprovalQueueItem item, out string affiliateLink, out string productId)
        {
            if (item == null || !item.CanAttachAffiliate)
            {
                affiliateLink = string.Empty;
                productId = string.Empty;
                return;
            }

            affiliateLink = OmnichannelAutoPostFields.NormalizeLink(item.TargetAffiliateLink);
            productId = OmnichannelAutoPostFields.NormalizeLink(item.TargetProductId);
        }
    }

}

