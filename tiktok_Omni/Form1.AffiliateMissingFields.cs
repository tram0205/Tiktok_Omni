using System.Collections.Generic;
using System.Linq;
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

        private void LoadAffiliateHuntResultsFromDisk()
        {
            if (_affiliateBindingList == null)
            {
                return;
            }

            try
            {
                var rows = _affiliateHuntResultsStore?.Load() ?? new List<AffiliateCandidate>();
                if (rows.Count == 0)
                {
                    return;
                }

                foreach (var candidate in rows)
                {
                    if (candidate != null)
                    {
                        candidate.ProfileName = ProfileScopedPaths.ResolveProfileName(candidate.ProfileName);
                    }
                }

                _affiliateAllResults = rows;
                SortAffiliateCandidatesByViewsDescending(_affiliateAllResults);
                RefreshAffiliateGridByQualityFilter();
                Log("[Affiliate] Đã khôi phục " + rows.Count + " dòng từ hunt_results.json.");
            }
            catch (System.Exception ex)
            {
                Log("[Affiliate] Không đọc được hunt_results.json: " + ex.Message);
            }
        }

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

        private void FlushAffiliateHuntResultsOnExit()
        {
            if (_affiliateAllResults == null)
            {
                return;
            }

            // Giữ file cũ nếu đang săn dở mà chưa có dòng mới (tránh ghi đè [] khi đóng app).
            if (_affiliateAllResults.Count == 0 && _huntCancellation != null)
            {
                return;
            }

            SaveAffiliateHuntResultsToDisk();
        }
    }
}
