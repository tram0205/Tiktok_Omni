using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void RefreshHealthReadinessPanel(Dictionary<string, bool> health)
        {
            if (lblHealthReadiness == null || lblHealthReadiness.IsDisposed)
            {
                return;
            }

            health = health ?? _systemHealth;
            bool Ok(string key) => health != null && health.TryGetValue(key, out var v) && v;

            var items = new[]
            {
                ("FFmpeg", Ok("ffmpeg")),
                ("yt-dlp", Ok("ytdlp")),
                ("API Keys", Ok("api_keys")),
                ("L\u01b0u tr\u1eef", Ok("storage"))
            };

            var parts = items.Select(i => (i.Item2 ? "\u2713 " : "\u2717 ") + i.Item1).ToArray();
            var allOk = items.All(i => i.Item2);
            lblHealthReadiness.ForeColor = allOk
                ? Color.FromArgb(120, 220, 160)
                : Color.FromArgb(255, 180, 120);
            lblHealthReadiness.Text = "Checklist s\u1eb5n s\u00e0ng: " + string.Join("  \u2022  ", parts);
        }

        private void UpdateHealthOpenSettingsButton(Dictionary<string, bool> health)
        {
            if (btnHealthOpenSettings == null)
            {
                return;
            }

            health = health ?? _systemHealth;
            var apiBad = health == null || !health.TryGetValue("api_keys", out var apiOk) || !apiOk;
            btnHealthOpenSettings.Visible = apiBad;
        }

        private async void btnHealthRecheckHealth_Click(object sender, System.EventArgs e)
        {
            if (btnHealthRecheck != null)
            {
                btnHealthRecheck.Enabled = false;
            }

            try
            {
                await RunStartupSystemHealthCheckAsync().ConfigureAwait(true);
            }
            finally
            {
                if (btnHealthRecheck != null && !btnHealthRecheck.IsDisposed)
                {
                    btnHealthRecheck.Enabled = true;
                }
            }
        }

        private void btnHealthOpenSettings_Click(object sender, System.EventArgs e)
        {
            SwitchToMainTab(tabSetting);
        }
    }
}
