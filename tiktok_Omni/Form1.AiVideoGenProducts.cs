using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private ManualProductFetchResult _lastManualProductFetch;

        private bool IsSlideshowModeTab()
        {
            return GetSelectedAiVideoGenModeIndex() == 0;
        }

        private bool IsDeepDiveModeTab()
        {
            return GetSelectedAiVideoGenModeIndex() == 1;
        }

        private bool IsProductPipelineModeTab()
        {
            return IsSlideshowModeTab() || IsDeepDiveModeTab();
        }

        private List<AiVideoGenInputItem> GetSlideshowBuffer() => _slideshowBuffer ?? (_slideshowBuffer = new List<AiVideoGenInputItem>());

        private List<AiVideoGenInputItem> GetDeepDiveBuffer() => _deepDiveBuffer ?? (_deepDiveBuffer = new List<AiVideoGenInputItem>());

        private DataGridView GetActiveProductGrid()
        {
            return IsDeepDiveModeTab() ? dgvDeepDiveInput : dgvAiVideoGenInput;
        }

        private void SyncBuffersToGrids()
        {
            SaveAllProductGridState();

            if (dgvAiVideoGenInput != null)
            {
                dgvAiVideoGenInput.DataSource = null;
                dgvAiVideoGenInput.DataSource = GetSlideshowBuffer()
                    .Where(ShouldShowAiVideoGenItem)
                    .ToList();
            }

            if (dgvDeepDiveInput != null)
            {
                dgvDeepDiveInput.DataSource = null;
                dgvDeepDiveInput.DataSource = GetDeepDiveBuffer()
                    .Where(ShouldShowAiVideoGenItem)
                    .ToList();
            }
        }

        /// <summary>Commit ô đang sửa trên lưới Slideshow và ghi ngược vào <see cref="_slideshowBuffer"/>.</summary>
        public void SaveCurrentGridState()
        {
            SaveProductGridStateToBuffer(dgvAiVideoGenInput, GetSlideshowBuffer(), notifySlideshowDraftDirty: true);
        }

        /// <summary>Commit ô đang sửa trên lưới Affiliate Deep và ghi ngược vào <see cref="_deepDiveBuffer"/>.</summary>
        public void SaveDeepDiveGridState()
        {
            SaveProductGridStateToBuffer(dgvDeepDiveInput, GetDeepDiveBuffer(), notifySlideshowDraftDirty: false);
        }

        /// <summary>Lưu cả hai lưới sản phẩm trước rebind / render / đổi tab.</summary>
        public void SaveAllProductGridState()
        {
            SaveDeepDiveGridState();
            SaveCurrentGridState();
        }

        private void SaveProductGridStateToBuffer(
            DataGridView grid,
            List<AiVideoGenInputItem> buffer,
            bool notifySlideshowDraftDirty)
        {
            if (grid == null || grid.IsDisposed || buffer == null)
            {
                return;
            }

            if (grid.IsCurrentCellInEditMode)
            {
                grid.EndEdit(DataGridViewDataErrorContexts.Commit);
            }

            var changed = false;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                if (!(row.DataBoundItem is AiVideoGenInputItem gridItem))
                {
                    continue;
                }

                var target = buffer.FirstOrDefault(x => ReferenceEquals(x, gridItem))
                    ?? buffer.FirstOrDefault(x => AiVideoGenItemsMatch(x, gridItem));
                if (target == null)
                {
                    continue;
                }

                if (ApplyGridRowToAiVideoGenItem(grid, row, target))
                {
                    changed = true;
                }
            }

            if (changed && notifySlideshowDraftDirty)
            {
                NotifySlideshowDraftDirty();
            }
        }

        private static bool ApplyGridRowToAiVideoGenItem(DataGridView grid, DataGridViewRow row, AiVideoGenInputItem target)
        {
            if (grid == null || row == null || target == null)
            {
                return false;
            }

            var profile = ReadGridCellText(grid, row, "colAiProfile");
            var product = ReadGridCellText(grid, row, "colAiProduct");
            var videoUrl = ReadGridCellText(grid, row, "colAiUrl");
            var hook = ReadGridCellText(grid, row, "colAiHook");
            var hashtags = ReadGridCellText(grid, row, "colAiHashtag");

            var changed = false;
            changed |= SetIfDifferent(target.ProfileName, profile, v => target.ProfileName = ProfileScopedPaths.ResolveProfileName(v));
            changed |= SetIfDifferent(target.ProductName, product, v => target.ProductName = v);
            changed |= SetIfDifferent(target.VideoUrl, videoUrl, v => target.VideoUrl = v);
            changed |= SetIfDifferent(target.HookText, hook, v => target.HookText = v);
            changed |= SetIfDifferent(target.Hashtags, hashtags, v => target.Hashtags = v);
            return changed;
        }

        private static string ReadGridCellText(DataGridView grid, DataGridViewRow row, string columnName)
        {
            if (grid == null || row == null || !grid.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : value.ToString().Trim();
        }

        private static bool SetIfDifferent(string current, string incoming, Action<string> apply)
        {
            var normalized = incoming ?? string.Empty;
            if (string.Equals(current ?? string.Empty, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            apply(normalized);
            return true;
        }

        private void ReplaceSlideshowBuffer(IEnumerable<AiVideoGenInputItem> items)
        {
            _slideshowBuffer = items?
                .Where(x => x != null)
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList() ?? new List<AiVideoGenInputItem>();
            NotifySlideshowDraftDirty();
            SyncBuffersToGrids();
        }

        private void ReplaceDeepDiveBuffer(IEnumerable<AiVideoGenInputItem> items)
        {
            _deepDiveBuffer = items?
                .Where(x => x != null)
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList() ?? new List<AiVideoGenInputItem>();
            SyncBuffersToGrids();
        }

        private void PushAffiliateCandidatesToTargetBuffer(IEnumerable<AffiliateCandidate> candidates, bool deepDiveTarget)
        {
            var mapped = (candidates ?? Array.Empty<AffiliateCandidate>())
                .Where(x => x != null)
                .GroupBy(x => (x.VideoUrl ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(MapAffiliateToAiVideoInput)
                .ToList();
            PushMappedProductsToTargetBuffer(mapped, deepDiveTarget);
        }

        private void PushMappedProductsToTargetBuffer(IEnumerable<AiVideoGenInputItem> mapped, bool deepDiveTarget)
        {
            var list = (mapped ?? Array.Empty<AiVideoGenInputItem>())
                .Where(x => x != null)
                .Select(CloneAiVideoGenItem)
                .Where(x => x != null)
                .ToList();

            if (deepDiveTarget)
            {
                ReplaceDeepDiveBuffer(list);
                RefreshAffiliateDeepStoryboard();
            }
            else
            {
                ReplaceSlideshowBuffer(list);
            }
        }

        private const int ProductInputGridHeaderHeight = AppGridHeaderHeight;
        private const int ProductInputGridRowHeight = 30;
        private static readonly Font ProductInputGridHeaderFont = AppGridHeaderFont;

        private DataGridView CreateProductInputGrid(string name)
        {
            var grid = new DataGridView
            {
                Name = name,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(180, ProductInputGridHeaderHeight + ProductInputGridRowHeight + 8),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.FromArgb(20, 22, 28),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(60, 64, 77),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = ProductInputGridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            grid.DefaultCellStyle.BackColor = Color.FromArgb(20, 22, 28);
            grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 110, 245);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 49, 60);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 49, 60);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.WhiteSmoke;
            grid.ColumnHeadersDefaultCellStyle.Font = ProductInputGridHeaderFont;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 8, 6, 8);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = ProductInputGridRowHeight;
            ConfigureProductInputGrid(grid);
            return grid;
        }

        private async void btnAddManualProduct_Click(object sender, EventArgs e)
        {
            var url = txtManualProductUrl?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(this, "Vui lòng nhập link sản phẩm.", "Nhập link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsProductPipelineModeTab())
            {
                MessageBox.Show(this, "Chọn «Slideshow» hoặc «Affiliate Deep» trước khi thêm.", "Nhập link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnAddManualProduct.Enabled = false;
            UseWaitCursor = true;
            try
            {
                var item = await ScrapeProductDetailsAsync(url, CancellationToken.None).ConfigureAwait(true);
                if (item == null)
                {
                    MessageBox.Show(this, "Không lấy được thông tin sản phẩm.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (IsSlideshowModeTab())
                {
                    GetSlideshowBuffer().Add(CloneAiVideoGenItem(item));
                    NotifySlideshowDraftDirty();
                    Log($"Slideshow: đã thêm «{item.ProductName}» từ link.");
                }
                else
                {
                    var images = (_lastManualProductFetch?.ImageUrls ?? new List<string>())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();
                    if (images.Count < 4)
                    {
                        MessageBox.Show(this, "Affiliate Deep cần ít nhất 4 ảnh. Link này chỉ trả về " + images.Count + " ảnh.", "Không đủ ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    foreach (var image in images.Take(4))
                    {
                        var scene = CloneAiVideoGenItem(item);
                        if (scene == null)
                        {
                            continue;
                        }

                        scene.ImageUrl = image;
                        GetDeepDiveBuffer().Add(scene);
                    }

                    RefreshAffiliateDeepStoryboard();
                    Log($"Affiliate Deep: đã thêm 4 cảnh cho «{item.ProductName}».");
                }

                SyncBuffersToGrids();
                txtManualProductUrl?.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Log("[Manual] " + ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                btnAddManualProduct.Enabled = true;
                SetStatusStripText(tabAiVideoGen?.Text ?? "tiktok_Omni");
            }
        }

        private void DgvAiVideoGenInput_SelectionChanged_Production(object sender, EventArgs e)
        {
            if (dgvAiVideoGenInput?.CurrentRow?.DataBoundItem is AiVideoGenInputItem item)
            {
                BindProductionPreviewForAiItem(item);
            }
        }

        private void DgvDeepDiveInput_SelectionChanged_Production(object sender, EventArgs e)
        {
            if (dgvDeepDiveInput?.CurrentRow?.DataBoundItem is AiVideoGenInputItem item)
            {
                BindProductionPreviewForAiItem(item);
            }
        }

        public void InitializeAiVideoGenProductGrids(Panel slideshowGridHost, Panel deepDiveGridHost)
        {
            if (slideshowGridHost != null)
            {
                foreach (Control c in slideshowGridHost.Controls.OfType<DataGridView>().ToArray())
                {
                    if (c.Name == "dgvAiVideoGenInput" || c.Name == "dgvSlideshow")
                    {
                        slideshowGridHost.Controls.Remove(c);
                        c.Dispose();
                    }
                }

                dgvAiVideoGenInput = CreateProductInputGrid("dgvSlideshow");
                dgvAiVideoGenInput.SelectionChanged += DgvAiVideoGenInput_SelectionChanged_Production;
                slideshowGridHost.Controls.Add(dgvAiVideoGenInput);
            }

            if (deepDiveGridHost != null)
            {
                foreach (Control c in deepDiveGridHost.Controls.OfType<DataGridView>().ToArray())
                {
                    deepDiveGridHost.Controls.Remove(c);
                    c.Dispose();
                }

                dgvDeepDiveInput = CreateProductInputGrid("dgvDeepDive");
                dgvDeepDiveInput.SelectionChanged += DgvDeepDiveInput_SelectionChanged_Production;
                deepDiveGridHost.Controls.Add(dgvDeepDiveInput);
            }

            SyncBuffersToGrids();
            InitializeSlideshowDraftAutoSave();
            WireSlideshowProductGridLayout();
            WireDeepDiveProductGridLayout();
            RefreshAllProfileSelectors();
        }

        public void SyncProductGridVisibilityForMode(int modeTabIndex)
        {
            if (lblAiVideoGenProductsTitle != null)
            {
                lblAiVideoGenProductsTitle.Text = modeTabIndex == 1
                    ? "Affiliate Deep — storyboard & dữ liệu cảnh"
                    : modeTabIndex == 0
                        ? "Dữ liệu sản phẩm (Slideshow)"
                        : "Sản xuất video AI";
                lblAiVideoGenProductsTitle.Visible = modeTabIndex == 0 || modeTabIndex == 1;
            }

            if (modeTabIndex == 1)
            {
                RefreshAffiliateDeepStoryboard();
            }

            SyncBuffersToGrids();
        }

        private Task<AiVideoGenInputItem> ScrapeProductDetailsAsync(string url, CancellationToken cancellationToken = default)
        {
            return WithBrowserLockAsync(
                GetRunningProfileName(),
                ct => ScrapeProductDetailsCoreAsync(url, ct),
                cancellationToken);
        }

        private async Task<AiVideoGenInputItem> ScrapeProductDetailsCoreAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fetch = await _affiliateHunter
                .FetchProductInfoFromUrlAsync(url, Log, cancellationToken)
                .ConfigureAwait(false);
            _lastManualProductFetch = fetch;
            if (fetch == null)
            {
                return null;
            }

            var reviews = fetch.CustomerReviews != null && fetch.CustomerReviews.Count > 0
                ? string.Join(" ||| ", fetch.CustomerReviews.Where(r => !string.IsNullOrWhiteSpace(r)))
                : string.Empty;

            var candidate = new AffiliateCandidate
            {
                ProductName = fetch.ProductName,
                Price = fetch.Price,
                ImageUrl = fetch.ImageUrls?.FirstOrDefault() ?? string.Empty,
                LinkedProduct = string.IsNullOrWhiteSpace(fetch.AffiliateLink) ? url.Trim() : fetch.AffiliateLink,
                VideoUrl = url.Trim(),
                CustomerReviews = reviews,
                ProfileName = GetRunningProfileName() ?? string.Empty
            };

            var item = MapAffiliateToAiVideoInput(candidate);
            if (item != null && !string.IsNullOrWhiteSpace(fetch.ProductId))
            {
                item.ProductId = fetch.ProductId.Trim();
            }

            return item;
        }
    }
}
