using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private async Task RefreshMascotApiBalanceDashboardAsync()
        {
            try
            {
                if (lblMascotGeminiBalance == null || lblMascotElevenBalance == null) return;
                lblMascotGeminiBalance.Text = "Gemini: dang kiem tra...";
                lblMascotElevenBalance.Text = "ElevenLabs: dang kiem tra...";
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                var snap = await new ApiProviderBalanceService().CheckAllAsync(settings).ConfigureAwait(true);
                lblMascotGeminiBalance.Text = snap.Gemini?.Summary ?? "Gemini: —";
                lblMascotElevenBalance.Text = snap.ElevenLabs?.Summary ?? "ElevenLabs: —";
            }
            catch (System.Exception ex)
            {
                if (lblMascotGeminiBalance != null) lblMascotGeminiBalance.Text = "Gemini: " + ex.Message;
            }
        }

        private Task RunAutoDetectMouthAsync()
        {
            Log("Auto-Detect Mouth: UI legacy đã gỡ — cấu hình miệng qua AvatarVault nếu cần lip-sync.");
            return Task.CompletedTask;
        }
    }
}
