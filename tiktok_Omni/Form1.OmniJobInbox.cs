using System.IO;
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

        private string GetReupVisualHookSfxPath()
        {
            return (txtReupVisualHookSfx?.Text ?? string.Empty).Trim();
        }

        private bool IsReupVisualHookSfxEnabled()
        {
            return chkReupUseVisualHookSfx?.Checked == true
                   && !string.IsNullOrWhiteSpace(GetReupVisualHookSfxPath())
                   && File.Exists(GetReupVisualHookSfxPath());
        }

        private void ApplyReupVisualHookSettingsToRow(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            if (!row.UseVisualHookSfx)
            {
                return;
            }

            var path = GetReupVisualHookSfxPath();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                row.VisualHookSfxPath = path;
            }
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
