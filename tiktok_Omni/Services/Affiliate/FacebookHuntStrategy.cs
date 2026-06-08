using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Affiliate
{
    public sealed class FacebookHuntStrategy : IAffiliateSource
    {
        private readonly FacebookReelsHuntService _service;

        public FacebookHuntStrategy(FacebookReelsHuntService service)
        {
            _service = service ?? new FacebookReelsHuntService();
        }

        public string PlatformId => AffiliateSourceIds.Facebook;
        public string DisplayName => "Facebook Reels";

        public Task<List<AffiliateCandidate>> HuntAsync(
            string keyword,
            int limit,
            AffiliateHuntContext context,
            CancellationToken cancellationToken)
        {
            return _service.HuntAsync(keyword, limit, context?.Log, cancellationToken);
        }
    }
}
