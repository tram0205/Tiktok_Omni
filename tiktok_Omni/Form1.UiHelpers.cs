using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Affiliate;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void RefreshAllProfileSelectors()
        {
            AppSettings settings;
            try
            {
                settings = Task.Run(() => _configManager.LoadAsync()).GetAwaiter().GetResult();
            }
            catch
            {
                settings = new AppSettings();
            }

            RefreshRunningProfileOptions(settings);
        }

        private void RefreshHuntProductProfileCombo(AppSettings settings)
        {
            if (cbHuntProductProfile == null)
            {
                return;
            }

            var previous = cbHuntProductProfile.SelectedItem?.ToString();
            cbHuntProductProfile.Items.Clear();
            cbHuntProductProfile.Items.Add("default");
            foreach (var profile in settings?.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || cbHuntProductProfile.Items.Contains(name))
                {
                    continue;
                }

                cbHuntProductProfile.Items.Add(name);
            }

            var target = string.IsNullOrWhiteSpace(previous) ? "default" : previous.Trim();
            var index = cbHuntProductProfile.Items.IndexOf(target);
            cbHuntProductProfile.SelectedIndex = index >= 0 ? index : 0;
        }

        private void RefreshAffiliateHuntProfileCombo(AppSettings settings)
        {
            if (cbAffiliateHuntProfile == null)
            {
                return;
            }

            var previous = cbAffiliateHuntProfile.SelectedItem?.ToString();
            cbAffiliateHuntProfile.Items.Clear();
            cbAffiliateHuntProfile.Items.Add("default");
            foreach (var profile in settings?.Profiles ?? new List<AutomationProfile>())
            {
                var name = (profile?.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || cbAffiliateHuntProfile.Items.Contains(name))
                {
                    continue;
                }

                cbAffiliateHuntProfile.Items.Add(name);
            }

            var target = string.IsNullOrWhiteSpace(previous) ? "default" : previous.Trim();
            var index = cbAffiliateHuntProfile.Items.IndexOf(target);
            cbAffiliateHuntProfile.SelectedIndex = index >= 0 ? index : 0;
        }

        private bool ResolveVideoReupRenderReady()
        {
            bool Ok(string key) => _systemHealth != null && _systemHealth.TryGetValue(key, out var v) && v;
            try
            {
                var settings = Task.Run(() => _configManager.LoadAsync()).GetAwaiter().GetResult();
                return VideoReupRemixService.IsTabRenderReady(settings, Ok("ffmpeg"), Ok("storage"), out _);
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureVideoReupRowMusicDefault(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(row.ReupSelectedMusicFile))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(row.ReupSuggestedMusicFile))
            {
                row.ReupSelectedMusicFile = row.ReupSuggestedMusicFile;
                return;
            }

            row.ReupSelectedMusicFile = VideoReupRowItem.NoMusicSelectionLabel;
        }

        private void dgvAffiliateResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete || e.Alt || e.Control)
            {
                return;
            }

            if (_affiliateBindingList == null || dgvAffiliateResults?.SelectedRows == null || dgvAffiliateResults.SelectedRows.Count == 0)
            {
                return;
            }

            var items = new List<AffiliateCandidate>();
            foreach (DataGridViewRow row in dgvAffiliateResults.SelectedRows)
            {
                if (row?.DataBoundItem is AffiliateCandidate candidate)
                {
                    items.Add(candidate);
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            foreach (var candidate in items)
            {
                _affiliateBindingList.Remove(candidate);
                _affiliateAllResults?.RemoveAll(c => ReferenceEquals(c, candidate) || string.Equals(c?.VideoUrl, candidate.VideoUrl, StringComparison.OrdinalIgnoreCase));
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            RefreshAffiliateGridByQualityFilter();
            SaveAffiliateHuntResultsToDisk();
            Log($"Affiliate: đã xóa {items.Count} dòng (Delete).");
        }

        private void dgvProxyProfiles_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvProxyProfiles == null || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (dgvProxyProfiles.Columns[e.ColumnIndex]?.Name != "colProfileMascotImage")
            {
                return;
            }

            if (!(dgvProxyProfiles.Rows[e.RowIndex].DataBoundItem is AutomationProfile profile))
            {
                return;
            }

            var profileName = (profile.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp";
                dialog.Title = "Chọn ảnh profile / linh vật cho «" + profileName + "»";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var dir = AvatarIdentityPackStore.GetProfileDirectory(profileName);
                foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
                {
                    var old = Path.Combine(dir, "mascot" + ext);
                    try
                    {
                        if (File.Exists(old))
                        {
                            File.Delete(old);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                var destExt = Path.GetExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(destExt))
                {
                    destExt = ".png";
                }

                var dest = Path.Combine(dir, "mascot" + destExt.ToLowerInvariant());
                File.Copy(dialog.FileName, dest, true);
                Log("AvatarVault: đã lưu ảnh profile «" + profileName + "» → " + dest);
            }
        }

        private async void btnTestTikTokRapidApi_Click(object sender, EventArgs e)
        {
            if (btnTestTikTokRapidApi != null)
            {
                btnTestTikTokRapidApi.Enabled = false;
            }

            try
            {
                var key = txtTikTokRapidApiKey?.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    NotifySettingsApiTest("TikTok RapidAPI", false, false, "Chưa nhập TikTok RapidAPI key.");
                    return;
                }

                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                var svc = new TikTokApiService();
                var results = await svc.SearchVideosAsync(
                    "tiktok",
                    1,
                    key,
                    settings.TikTokRapidApiHost,
                    Log,
                    CancellationToken.None).ConfigureAwait(true);

                NotifySettingsApiTest(
                    "TikTok RapidAPI",
                    true,
                    false,
                    "Key hoạt động.\r\nHost: " + (settings.TikTokRapidApiHost ?? TikTokApiService.DefaultRapidApiHost) +
                    "\r\nMẫu: " + (results?.Count ?? 0) + " kết quả.");
            }
            catch (Exception ex)
            {
                NotifySettingsApiTest("TikTok RapidAPI", false, false, FormatApiTestException(ex));
            }
            finally
            {
                if (btnTestTikTokRapidApi != null)
                {
                    btnTestTikTokRapidApi.Enabled = true;
                }
            }
        }
    }
}
