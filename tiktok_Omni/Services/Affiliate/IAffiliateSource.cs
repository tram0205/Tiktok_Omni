using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Affiliate
{
    public interface IAffiliateSource
    {
        string PlatformId { get; }
        string DisplayName { get; }

        Task<List<AffiliateCandidate>> HuntAsync(
            string keyword,
            int limit,
            AffiliateHuntContext context,
            CancellationToken cancellationToken);
    }
}
