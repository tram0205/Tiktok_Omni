using System;
using System.Drawing;
using System.IO;
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
            catch (Exception ex)
            {
                if (lblMascotGeminiBalance != null) lblMascotGeminiBalance.Text = "Gemini: " + ex.Message;
            }
        }

        private async Task RunAutoDetectMouthAsync()
        {
            var path = txtMascotImagePath?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Log("Auto-Detect Mouth: chon anh linh vat truoc.");
                return;
            }

            Log("Auto-Detect Mouth: uoc luong vi tri mieng (heuristic). " + MascotMouthPositionEstimator.GetDlibIntegrationHint());
            var pt = await MascotMouthPositionEstimator.EstimateMouthOverlayAsync(path).ConfigureAwait(true);
            if (_mascotIdentityPackConfig == null) _mascotIdentityPackConfig = new AvatarIdentityPackConfig();
            _mascotIdentityPackConfig.MouthOverlayX = pt.X;
            _mascotIdentityPackConfig.MouthOverlayY = pt.Y;
            SaveMascotIdentityPackConfig();
            PositionMouthMarkerFromConfig();
            UpdateMouthOverlayCoordLabel();
            RefreshLipSyncPreviewBaseImage();
            Log("Auto-Detect Mouth: X=" + pt.X + ", Y=" + pt.Y);
        }
    }
}