using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Affiliate
{
    public sealed class YouTubeHuntStrategy : IAffiliateSource
    {
        private readonly YouTubeShortsHuntService _service;

        public YouTubeHuntStrategy(YouTubeShortsHuntService service)
        {
            _service = service ?? new YouTubeShortsHuntService();
        }

        public string PlatformId => AffiliateSourceIds.YouTube;
        public string DisplayName => "YouTube Shorts";

        public Task<List<AffiliateCandidate>> HuntAsync(
            string keyword,
            int limit,
            AffiliateHuntContext context,
            CancellationToken cancellationToken)
        {
            var ytDlp = context?.YtDlpPath;
            if (string.IsNullOrWhiteSpace(ytDlp) && context?.ConfigManager != null)
            {
                try
                {
                    var settings = context.ConfigManager.LoadAsync().GetAwaiter().GetResult();
                    ytDlp = YtDlpToolResolver.Resolve(settings, context.Log);
                }
                catch
                {
                    // HuntAsync will validate path.
                }
            }

            return _service.HuntAsync(keyword, limit, ytDlp, context?.Log, cancellationToken);
        }
    }
}
