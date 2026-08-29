using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        /// <summary>Metadata sản phẩm cho luồng Auto Post (thay biến toàn cục pending link).</summary>
        private OmniJob _autoPostInboxJob = new OmniJob();

        private GeminiStyleTemplate GetSelectedGeminiStyleTemplate()
        {
            return _aiVideoGenControls?.GetSelectedGeminiStyleTemplate() ?? GeminiStyleTemplate.Storytelling;
        }

        private void ApplyReupVisualHookSettingsToRow(VideoReupRowItem row)
        {
            // SFX hook chọn theo cột «SFX Hook» trên lưới — không còn preset/đường dẫn global trên toolbar.
        }

        private void SetAutoPostInboxFromJob(OmniJob source, string affiliateLink, string productId)
        {
            _autoPostInboxJob = source ?? new OmniJob();
            if (!string.IsNullOrWhiteSpace(affiliateLink) || !string.IsNullOrWhiteSpace(productId))
            {
                OmniJobMetadataHelper.ApplyAffiliateFields(_autoPostInboxJob, affiliateLink, productId);
            }

            RefreshAutoPostAffiliateUx();
        }
    }
}
