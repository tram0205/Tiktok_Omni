using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Affiliate
{
    public sealed class TikTokHuntStrategy : IAffiliateSource
    {
        private readonly AffiliateHunter _hunter;

        public TikTokHuntStrategy(AffiliateHunter hunter)
        {
            _hunter = hunter;
        }

        public string PlatformId => AffiliateSourceIds.TikTok;
        public string DisplayName => "TikTok";

        public async Task<List<AffiliateCandidate>> HuntAsync(
            string keyword,
            int limit,
            AffiliateHuntContext context,
            CancellationToken cancellationToken)
        {
            var mode = context?.TikTokSearchMode ?? AffiliateSearchMode.Video;
            
            // Truyền thông tin TikTokHuntMethod và Fallback sang HuntTikTokPlatformAsync
            var results = await _hunter.HuntTikTokPlatformAsync(
                keyword,
                limit,
                mode,
                cancellationToken,
                context?.Log,
                context?.ConfigManager,
                context?.RunningProfileName,
                context?.TikTokHuntMethod,
                context?.TikTokRapidApiFallbackToBrowser).ConfigureAwait(false);

            foreach (var c in results ?? Enumerable.Empty<AffiliateCandidate>())
            {
                if (c != null)
                {
                    c.SourcePlatform = AffiliateSourceIds.TikTok;
                }
            }

            return results ?? new List<AffiliateCandidate>();
        }
    }
}
