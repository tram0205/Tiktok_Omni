using System.Collections.Generic;
using System.Linq;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Jobs
{
    public static class OmniJobMetadataHelper
    {
        public static void ApplyAffiliateFields(OmniJob job, string affiliateLink, string productId)
        {
            if (job == null)
            {
                return;
            }

            job.AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(affiliateLink);
            job.ProductId = OmnichannelAutoPostFields.NormalizeLink(productId);
        }

        public static void ApplyFromProduct(OmniJob job, AiVideoGenInputItem item)
        {
            if (job == null || item == null)
            {
                return;
            }

            ApplyAffiliateFields(job, item.AffiliateLink, item.ProductId);
        }

        public static void ApplyFromProducts(OmniJob job, IEnumerable<AiVideoGenInputItem> items)
        {
            if (job == null || items == null)
            {
                return;
            }

            var first = items.FirstOrDefault(x => x != null);
            if (first == null)
            {
                return;
            }

            ApplyFromProduct(job, first);
        }

        public static void CopyAffiliateFields(OmniJob source, OmniJob target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.AffiliateLink = OmnichannelAutoPostFields.NormalizeLink(source.AffiliateLink);
            target.ProductId = OmnichannelAutoPostFields.NormalizeLink(source.ProductId);
        }
    }
}
