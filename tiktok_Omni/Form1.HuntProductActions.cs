using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Affiliate;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void RefreshHuntProductDownloadFolderHint()
        {
            if (lnkHuntProductDownloadFolder == null || lnkHuntProductDownloadFolder.IsDisposed)
            {
                return;
            }

            try
            {
                var dir = GetHuntProductMediaDirectoryHint();
                lnkHuntProductDownloadFolder.Tag = Directory.Exists(dir) || dir.Contains(Path.DirectorySeparatorChar) || dir.Contains(":")
                    ? dir
                    : null;
                var shortPath = dir.StartsWith("(", StringComparison.Ordinal)
                    ? dir
                    : EllipsisMiddlePath(dir, 105);
                lnkHuntProductDownloadFolder.Text = "📁 Thư mục media sản phẩm (bấm mở): " + shortPath;
            }
            catch
            {
                lnkHuntProductDownloadFolder.Text = "📁 Thư mục media sản phẩm: (không đọc được đường dẫn)";
                lnkHuntProductDownloadFolder.Tag = null;
            }
        }

        private string GetHuntProductMediaDirectoryHint()
        {
            var keyword = SanitizeHuntProductKeywordForFolder(txtHuntProductKeyword?.Text);
            var profile = cbHuntProductProfile?.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(profile))
            {
                profile = GetRunningProfileName();
            }

            return ProfileScopedPaths.GetProductHuntKeywordFolder(profile, keyword);
        }

        private static string SanitizeHuntProductKeywordForFolder(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Contains("Nhập từ khoá"))
            {
                return "default";
            }

            return ProfileScopedPaths.SanitizeSegment(text);
        }

        private void LnkHuntProductDownloadFolder_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var dir = (lnkHuntProductDownloadFolder?.Tag as string)?.Trim();
            if (string.IsNullOrWhiteSpace(dir) || dir.StartsWith("(", StringComparison.Ordinal))
            {
                dir = GetHuntProductMediaDirectoryHint();
            }

            try
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log("[Săn SP] Không mở được thư mục: " + ex.Message);
            }
        }

        private void EnsureGlobalLogExpanded()
        {
            if (!_globalLogExpanded)
            {
                ToggleGlobalLogPanel();
            }
        }

        private void SetHuntProductScanStatus(string message, bool busy)
        {
            void Apply()
            {
                if (lblHuntProductStatus != null && !lblHuntProductStatus.IsDisposed)
                {
                    lblHuntProductStatus.Text = string.IsNullOrWhiteSpace(message)
                        ? "Sẵn sàng — bấm «Săn SP Affiliate» để bắt đầu."
                        : message.Trim();
                    lblHuntProductStatus.ForeColor = busy
                        ? Color.FromArgb(140, 220, 160)
                        : Color.FromArgb(160, 210, 175);
                }

                if (pbHuntProductScan != null && !pbHuntProductScan.IsDisposed)
                {
                    if (busy)
                    {
                        pbHuntProductScan.Style = ProgressBarStyle.Marquee;
                        pbHuntProductScan.Visible = true;
                    }
                    else
                    {
                        pbHuntProductScan.Style = ProgressBarStyle.Continuous;
                        pbHuntProductScan.Visible = false;
                    }
                }

                if (btnHuntProductAutoScan != null && !btnHuntProductAutoScan.IsDisposed)
                {
                    btnHuntProductAutoScan.Text = busy ? "Đang quét…" : "Săn SP Affiliate";
                }
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(Apply));
            }
            else
            {
                Apply();
            }
        }

        private Action<string> CreateHuntProductProgressLogger()
        {
            return message =>
            {
                Log(message);
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                var status = message.Trim();
                if (status.StartsWith("[Săn SP]", StringComparison.Ordinal))
                {
                    status = status.Substring("[Săn SP]".Length).TrimStart(' ', '—', '-', ':');
                }
                else if (status.StartsWith("[Shop/TikTok]", StringComparison.Ordinal))
                {
                    status = status.Substring("[Shop/TikTok]".Length).TrimStart(' ', '—', '-', ':');
                }
                else if (status.StartsWith("[Shop/Selenium]", StringComparison.Ordinal))
                {
                    status = "TikTok Shop: " + status.Substring("[Shop/Selenium]".Length).TrimStart(' ', '—', '-', ':');
                }
                else if (status.StartsWith("[TikTok API]", StringComparison.Ordinal))
                {
                    status = status.Substring("[TikTok API]".Length).TrimStart(' ', '—', '-', ':');
                }
                else if (status.StartsWith("[Affiliate]", StringComparison.Ordinal))
                {
                    status = status.Substring("[Affiliate]".Length).TrimStart(' ', '—', '-', ':');
                }
                else if (status.StartsWith("[WarmupProfile]", StringComparison.Ordinal) ||
                         status.StartsWith("[Browser]", StringComparison.Ordinal))
                {
                    status = "Chrome: " + status;
                }

                SetHuntProductScanStatus(status, busy: true);
            };
        }

        private static bool IsHuntProductUserFacingError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.IndexOf("Affiliate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("đăng nhập", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("DevToolsActivePort", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("session not created", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static HuntProductCandidate MapAffiliateToHuntProduct(AffiliateCandidate source, string profileName)
        {
            if (source == null)
            {
                return null;
            }

            var link = !string.IsNullOrWhiteSpace(source.VideoUrl)
                ? source.VideoUrl.Trim()
                : (source.LinkedProduct ?? string.Empty).Trim();

            return new HuntProductCandidate
            {
                ProfileName = profileName ?? string.Empty,
                SourcePlatform = string.IsNullOrWhiteSpace(source.SourcePlatform) ? "TikTok" : source.SourcePlatform.Trim(),
                ProductName = string.IsNullOrWhiteSpace(source.ProductName) ? "TikTok Affiliate" : source.ProductName.Trim(),
                ProductLink = link,
                ImageUrl = source.ImageUrl ?? string.Empty,
                Price = source.Price ?? string.Empty,
                Commission = source.CommissionRate ?? string.Empty
            };
        }

        private async void btnHuntProductAutoScan_Click(object sender, EventArgs e)
        {
            var keyword = (txtHuntProductKeyword?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(keyword) || keyword.Contains("Nhập từ khoá"))
            {
                MessageBox.Show(this, "Nhập từ khoá sản phẩm.", "Săn Link Sản phẩm", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtHuntProductKeyword?.Focus();
                return;
            }

            if (chkHuntProductTikTok?.Checked != true && chkHuntProductShopee?.Checked != true)
            {
                MessageBox.Show(this, "Chọn ít nhất TikTok hoặc Shopee.", "Săn Link Sản phẩm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _huntProductCancellation?.Cancel();
            _huntProductCancellation?.Dispose();
            _huntProductCancellation = new CancellationTokenSource();

            btnHuntProductAutoScan.Enabled = false;
            EnsureGlobalLogExpanded();
            SetHuntProductScanStatus("Bước 1/4: Chuẩn bị từ khoá…", busy: true);
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
                var profile = cbHuntProductProfile?.SelectedItem?.ToString();
                if (string.IsNullOrWhiteSpace(profile))
                {
                    profile = GetRunningProfileName();
                }

                var progressLog = CreateHuntProductProgressLogger();
                progressLog("[Săn SP] Bước 2/4: Profile «" + profile + "», từ khoá «" + keyword + "»");
                if (chkHuntProductTikTok?.Checked == true)
                {
                    if (!string.IsNullOrWhiteSpace(settings?.TikTokRapidApiKey))
                    {
                        progressLog("[Săn SP] TikTok: RapidAPI Get Top Products (key tab Cài đặt), lọc HH > 5%.");
                    }
                    else
                    {
                        progressLog("[Săn SP] TikTok: chưa có RapidAPI key — nhập key tab Cài đặt (bắt buộc, không dùng Chrome Chợ Affiliate).");
                    }
                }

                ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(settings.StorageRootPath, profile);
                _lastHuntProductKeyword = keyword;

                var scanner = new HuntProductScanner(_affiliateHunter);
                var request = new HuntProductScanRequest
                {
                    Keyword = keyword,
                    ProfileName = profile,
                    MaxResults = (int)(numHuntProductMaxResults?.Value ?? 30),
                    MinSales = (long)(numHuntProductMinSales?.Value ?? 0),
                    MinRating = numHuntProductMinRating?.Value ?? 0,
                    MinCommissionPercent = TikTokApiService.DefaultMinAffiliateCommissionPercent,
                    ScanTikTok = chkHuntProductTikTok?.Checked == true,
                    ScanShopee = chkHuntProductShopee?.Checked == true
                };

                progressLog("[Săn SP] Bước 3/4: Đang quét nền tảng đã chọn…");
                var rows = await scanner.ScanAsync(
                    request,
                    _configManager,
                    _huntProductCancellation.Token,
                    progressLog).ConfigureAwait(true);

                progressLog("[Săn SP] Bước 4/4: Hiển thị " + rows.Count + " sản phẩm lên bảng…");

                if (_huntProductBindingList == null)
                {
                    SetHuntProductScanStatus("Lỗi: chưa khởi tạo bảng sản phẩm.", busy: false);
                    return;
                }

                _huntProductBindingList.RaiseListChangedEvents = false;
                _huntProductBindingList.Clear();
                foreach (var row in rows)
                {
                    _huntProductBindingList.Add(row);
                }

                _huntProductBindingList.RaiseListChangedEvents = true;
                _huntProductBindingList.ResetBindings();
                RefreshHuntProductDownloadFolderHint();
                Log("Săn sản phẩm xong: " + rows.Count + " dòng (ưu tiên bán chạy trước).");
                SetHuntProductScanStatus(
                    rows.Count > 0
                        ? "Xong — tìm thấy " + rows.Count + " sản phẩm."
                        : "Xong — không có sản phẩm (xem Log phía dưới).",
                    busy: false);
                if (rows.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "Không tìm thấy sản phẩm nào.\n\n" +
                        "Kiểm tra:\n" +
                        "• Tab Cài đặt → TikTok RapidAPI key (tiktok-api23) đã Subscribe «Get Top Products»\n" +
                        "• Thử từ khoá khác (tiếng Anh thường nhiều kết quả hơn) hoặc đổi country_code = US\n" +
                        "• Lọc tự động chỉ giữ HH > 5% — thử từ khoá rộng hơn\n" +
                        "• Bật thêm Shopee nếu cần nguồn khác\n" +
                        "• Xem panel Log phía dưới để biết chi tiết",
                        "Săn Link Sản phẩm",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Đã hủy quét sản phẩm.");
                SetHuntProductScanStatus("Đã hủy quét sản phẩm.", busy: false);
            }
            catch (InvalidOperationException ex)
            {
                Log("Quét sản phẩm lỗi: " + ex.Message);
                SetHuntProductScanStatus("Lỗi: " + ex.Message, busy: false);
                MessageBox.Show(this, ex.Message, "Săn SP Affiliate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Log("Quét sản phẩm lỗi: " + ex.Message);
                SetHuntProductScanStatus("Lỗi: " + ex.Message, busy: false);
                if (IsHuntProductUserFacingError(ex.Message))
                {
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Quét sản phẩm",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else if (ex.Message.IndexOf("DevToolsActivePort", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ex.Message.IndexOf("session not created", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MessageBox.Show(
                        this,
                        "Chrome không khởi động được. Đóng hết cửa sổ Google Chrome rồi thử lại.\n\n" + ex.Message,
                        "Quét sản phẩm",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            finally
            {
                if (btnHuntProductAutoScan != null && !btnHuntProductAutoScan.IsDisposed)
                {
                    btnHuntProductAutoScan.Enabled = true;
                    if (btnHuntProductAutoScan.Text == "Đang quét…")
                    {
                        btnHuntProductAutoScan.Text = "Săn SP Affiliate";
                    }
                }
            }
        }

        private async void btnHuntProductDownloadMedia_Click(object sender, EventArgs e)
        {
            if (_huntProductBindingList == null || dgvHuntProduct == null)
            {
                return;
            }

            var selected = dgvHuntProduct.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as HuntProductCandidate)
                .Where(x => x != null)
                .ToList();
            if (selected.Count == 0)
            {
                Log("Chọn ít nhất một dòng để tải media.");
                return;
            }

            var keyword = SanitizeHuntProductKeywordForFolder(
                string.IsNullOrWhiteSpace(_lastHuntProductKeyword)
                    ? txtHuntProductKeyword?.Text
                    : _lastHuntProductKeyword);

            btnHuntProductDownloadMedia.Enabled = false;
            try
            {
                foreach (var item in selected)
                {
                    var productKey = ProfileScopedPaths.SanitizeSegment(item.ProductLink) ?? "item";
                    if (productKey.Length > 48)
                    {
                        productKey = productKey.Substring(0, 48);
                    }

                    var imageDir = ProfileScopedPaths.GetProductAssetFolder(item.ProfileName, keyword, productKey, images: true);
                    if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                    {
                        await DownloadHuntProductFileAsync(item.ImageUrl, imageDir, "cover").ConfigureAwait(true);
                    }
                    else if (!string.IsNullOrWhiteSpace(item.ProductLink))
                    {
                        try
                        {
                            var info = await _affiliateHunter.FetchProductInfoFromUrlAsync(item.ProductLink, Log, CancellationToken.None)
                                .ConfigureAwait(true);
                            var idx = 0;
                            foreach (var url in info.ImageUrls ?? new List<string>())
                            {
                                idx++;
                                await DownloadHuntProductFileAsync(url, imageDir, "img" + idx).ConfigureAwait(true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("[Săn SP] Không tải media «" + item.ProductName + "»: " + ex.Message);
                        }
                    }
                }

                RefreshHuntProductDownloadFolderHint();
                Log("Đã tải media cho " + selected.Count + " sản phẩm.");
            }
            finally
            {
                if (btnHuntProductDownloadMedia != null && !btnHuntProductDownloadMedia.IsDisposed)
                {
                    btnHuntProductDownloadMedia.Enabled = true;
                }
            }
        }

        private static async Task DownloadHuntProductFileAsync(string url, string folder, string baseName)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            var ext = ".jpg";
            try
            {
                var uri = new Uri(url);
                var pathExt = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(pathExt) && pathExt.Length <= 5)
                {
                    ext = pathExt;
                }
            }
            catch
            {
                // keep default
            }

            var target = Path.Combine(folder, baseName + ext);
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
            {
                var bytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);
                File.WriteAllBytes(target, bytes);
            }
        }
    }
}