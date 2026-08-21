using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Chọn file B-roll hoặc ảnh zoom từ thư viện với thumbnail trong app.</summary>
    internal sealed class PhilosophyAssetLibraryPickerForm : Form
    {
        public enum PickerMode
        {
            BrollVideo,
            ZoomImage
        }

        private const int ThumbSize = 96;

        private readonly PickerMode _mode;
        private readonly string _profileName;
        private readonly bool _allowMultiSelect;
        private readonly ListView _list;
        private readonly ImageList _images;
        private readonly Label _lblStatus;
        private readonly TextBox _txtFilter;
        private readonly List<string> _allPaths = new List<string>();
        private readonly Dictionary<string, int> _imageIndexByPath =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> SelectedPaths { get; private set; } = Array.Empty<string>();

        public PhilosophyAssetLibraryPickerForm(
            PickerMode mode,
            string profileName,
            bool allowMultiSelect = false)
        {
            _mode = mode;
            _profileName = profileName ?? string.Empty;
            _allowMultiSelect = allowMultiSelect;

            Text = mode == PickerMode.BrollVideo ? "Chọn B-roll từ thư viện" : "Chọn ảnh zoom từ thư viện";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(920, 620);
            MinimumSize = new Size(640, 420);

            _images = new ImageList
            {
                ImageSize = new Size(ThumbSize, (int)Math.Round(ThumbSize * 16.0 / 9.0)),
                ColorDepth = ColorDepth.Depth32Bit
            };

            _txtFilter = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 8)
            };
            _txtFilter.TextChanged += (_, __) => ApplyFilter();

            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                ForeColor = Color.FromArgb(150, 158, 172),
                Text = "Đang tải…"
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                LargeImageList = _images,
                MultiSelect = _allowMultiSelect,
                HideSelection = false,
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.FromArgb(230, 234, 242),
                BorderStyle = BorderStyle.None
            };
            _list.DoubleClick += (_, __) =>
            {
                if (_list.SelectedItems.Count > 0)
                {
                    ConfirmSelection();
                }
            };

            var btnOk = new Button
            {
                Text = "Chọn",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 120, 82),
                ForeColor = Color.White,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (_, __) => ConfirmSelection();

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(90, 96, 110),
                ForeColor = Color.White
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 8),
                BackColor = BackColor
            };
            footer.Controls.Add(btnOk);
            footer.Controls.Add(btnCancel);

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0) };
            host.Controls.Add(_list);
            host.Controls.Add(_txtFilter);

            Controls.Add(host);
            Controls.Add(_lblStatus);
            Controls.Add(footer);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Shown += async (_, __) => await LoadCatalogAsync().ConfigureAwait(true);
        }

        private async Task LoadCatalogAsync()
        {
            _lblStatus.Text = "Đang quét thư viện…";
            var paths = await Task.Run(() =>
            {
                if (_mode == PickerMode.BrollVideo)
                {
                    return PhilosophyBRollSelection.EnumerateVideoCatalog(_profileName)
                        .Select(e => e.FullPath)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                return PhilosophyBRollSelection.EnumerateZoomImageCatalog(_profileName)
                    .Select(e => e.FullPath)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }).ConfigureAwait(true);

            _allPaths.Clear();
            _allPaths.AddRange(paths);
            _lblStatus.Text = paths.Count == 0
                ? "Thư viện trống — dùng nút «Thư viện» trên dialog Nền để copy file vào."
                : paths.Count + " file — gõ để lọc tên.";

            await PopulateThumbnailsAsync(paths).ConfigureAwait(true);
            ApplyFilter();
        }

        private async Task PopulateThumbnailsAsync(IReadOnlyList<string> paths)
        {
            _images.Images.Clear();
            _imageIndexByPath.Clear();
            _list.Items.Clear();

            var loaded = 0;
            foreach (var path in paths)
            {
                var thumb = await Task.Run(() =>
                {
                    if (_mode == PickerMode.BrollVideo)
                    {
                        var cached = PhilosophyBrollThumbnailHelper.TryGetThumbnail(path, _profileName);
                        return cached != null ? (Image)cached.Clone() : null;
                    }

                    try
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            var img = Image.FromStream(stream);
                            return PhilosophyBrollThumbnailHelper.ResizeToThumb(img, ThumbSize);
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }).ConfigureAwait(true);

                if (IsDisposed)
                {
                    thumb?.Dispose();
                    return;
                }

                if (thumb == null)
                {
                    continue;
                }

                var index = _images.Images.Count;
                _images.Images.Add(thumb);
                _imageIndexByPath[path] = index;
                loaded++;
            }

            _lblStatus.Text = loaded + " / " + paths.Count + " thumbnail — chọn file rồi bấm «Chọn».";
        }

        private void ApplyFilter()
        {
            var filter = (_txtFilter.Text ?? string.Empty).Trim();
            _list.BeginUpdate();
            _list.Items.Clear();

            foreach (var path in _allPaths)
            {
                var name = Path.GetFileName(path) ?? path;
                if (!string.IsNullOrEmpty(filter)
                    && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!_imageIndexByPath.TryGetValue(path, out var imageIndex))
                {
                    continue;
                }

                var item = new ListViewItem(name, imageIndex) { Tag = path };
                _list.Items.Add(item);
            }

            _list.EndUpdate();
        }

        private void ConfirmSelection()
        {
            if (_list.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Chọn ít nhất một file.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            SelectedPaths = _list.SelectedItems
                .Cast<ListViewItem>()
                .Select(i => (i.Tag as string) ?? string.Empty)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (SelectedPaths.Count == 0)
            {
                DialogResult = DialogResult.None;
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (Image img in _images.Images)
            {
                img?.Dispose();
            }

            _images.Images.Clear();
            base.OnFormClosed(e);
        }
    }
}
