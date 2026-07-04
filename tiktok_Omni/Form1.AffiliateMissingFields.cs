using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private ComboBox cbAffiliateTikTokHuntMode;
        private CheckBox chkAffiliateTikTokApiFallbackBrowser;
        private Panel _pnlAiVideoGenGridContainer;
        private readonly AffiliateHuntResultsStore _affiliateHuntResultsStore = new AffiliateHuntResultsStore();

        private void SaveAffiliateHuntResultsToDisk()
        {
            try
            {
                _affiliateHuntResultsStore?.Save(_affiliateAllResults);
            }
            catch
            {
                // ignored — autosave best-effort
            }
        }
    }
}
