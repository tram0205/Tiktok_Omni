using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Quản lý thư viện logo thương hiệu (Assets\Logos).</summary>
    internal sealed class ShowcaseLogoLibraryForm : Form
    {
        private readonly AppSettings _settings;
        private readonly ListView _lvFiles;
        private readonly Label _lblStatus;
        private readonly Button _btnAddFiles;
        private readonly Button _btnDeleteFiles;
        private readonly ImageList _listRowHeightSpacer;
        private FileSystemWatcher _folderWatcher;
        private Timer _refreshDebounceTimer;
        private readonly Dictionary<string, Image> _thumbnailCache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Image> _loadedImages = new List<Image>();

        private int _thumbGeneration;

        private const int SectionLabelHeight = 72;
        private const int StatusBarHeight = 80;
        private const int ListRowHeight = 128;
        private const int SttColumnWidth = 130;
        private const int ThumbnailColumnWidth = 260;
        private const int BaseClientWidth = 960;
        private const int DefaultClientWidth = (int)(BaseClientWidth * 1.3);
        private const int BaseClientHeight = 728;
        private const int DefaultClientHeight = BaseClientHeight * 2;
        private const int MinimumClientWidth = (int)(720 * 1.3);
        private const int MinimumClientHeight = 624 * 2;

        private static readonly Color RowBackColor = Color.FromArgb(38, 42, 52);
        private static readonly Color RowTextColor = Color.FromArgb(230, 234, 242);
        private static readonly Color RowSelectedBackColor = Color.FromArgb(0, 120, 215);
        private static readonly Color GridLineColor = Color.FromArgb(72, 78, 92);
        private static readonly Color HeaderBackColor = Color.White;
        private static readonly Color HeaderTextColor = Color.Black;

        public ShowcaseLogoLibraryForm(AppSettings settings = null)
        {
            _settings = settings ?? new AppSettings();

            Text = "Thư viện logo";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(MinimumClientWidth, MinimumClientHeight);
            ClientSize = new Size(DefaultClientWidth, DefaultClientHeight);
            Font = new Font("Segoe UI", 10.5F);
            FormClosed += (_, __) => DisposeResources();

            var hint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 52,
                Padding = new Padding(16, 14, 16, 0),
                ForeColor = Color.FromArgb(170, 178, 192),
                Text = "Logo dùng chung — chọn trên cột «Logo» từng dòng video khi render."
            };

            _lvFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                BackColor = RowBackColor,
                ForeColor = RowTextColor,
                Font = new Font("Segoe UI", 10.5F),
                OwnerDraw = true
            };
            _lvFiles.Columns.Add("STT", SttColumnWidth, HorizontalAlignment.Center);
            _lvFiles.Columns.Add("Ảnh thu nhỏ", ThumbnailColumnWidth, HorizontalAlignment.Center);
            _lvFiles.Columns.Add("Tên file", -2, HorizontalAlignment.Left);
            _listRowHeightSpacer = new ImageList { ImageSize = new Size(1, ListRowHeight) };
            _listRowHeightSpacer.Images.Add(new Bitmap(1, ListRowHeight));
            _lvFiles.SmallImageList = _listRowHeightSpacer;
            _lvFiles.Resize += (_, __) => BalanceListViewColumns();
            _lvFiles.DrawColumnHeader += OnDrawColumnHeader;
            _lvFiles.DrawSubItem += OnDrawSubItem;
            _lvFiles.DoubleClick += (_, __) => ViewSelectedLogo();
            _lvFiles.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelectedLogos();
                    e.Handled = true;
                }
            };

            var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 4) };
            listPanel.Controls.Add(MkSectionLabel("Danh sách logo"));
            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, SectionLabelHeight + 8, 0, 0) };
            listHost.Controls.Add(_lvFiles);
            listPanel.Controls.Add(listHost);

            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = StatusBarHeight,
                Padding = new Padding(16, 20, 16, 20),
                ForeColor = Color.FromArgb(140, 148, 162),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseCompatibleTextRendering = true
            };

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(12, 14, 12, 14),
                BackColor = BackColor
            };

            var btnClose = MkButton("Đóng", Color.FromArgb(90, 96, 110));
            btnClose.DialogResult = DialogResult.OK;
            CancelButton = btnClose;

            var btnOpenFolder = MkButton("Mở thư mục", Color.FromArgb(56, 90, 78));
            btnOpenFolder.Click += (_, __) => OpenLibraryFolder();

            _btnAddFiles = MkButton("Thêm logo", Color.FromArgb(72, 118, 168));
            _btnAddFiles.Click += async (_, __) => await AddFilesAsync().ConfigureAwait(true);

            _btnDeleteFiles = MkButton("Xoá logo", Color.FromArgb(120, 64, 64));
            _btnDeleteFiles.Click += (_, __) => DeleteSelectedLogos();

            footer.Controls.Add(btnClose);
            footer.Controls.Add(btnOpenFolder);
            footer.Controls.Add(_btnDeleteFiles);
            footer.Controls.Add(_btnAddFiles);

            Controls.Add(listPanel);
            Controls.Add(_lblStatus);
            Controls.Add(footer);
            Controls.Add(hint);

            Shown += (_, __) =>
            {
                StartFolderWatcher();
                RefreshFileList();
                BalanceListViewColumns();
            };
        }

        private void StartFolderWatcher()
        {
            StopFolderWatcher();

            var dir = GetLibraryDirectory();
            _folderWatcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
            _folderWatcher.Created += OnLibraryFolderChanged;
            _folderWatcher.Deleted += OnLibraryFolderChanged;
            _folderWatcher.Renamed += OnLibraryFolderRenamed;
            _folderWatcher.Changed += OnLibraryFolderChanged;
            _folderWatcher.EnableRaisingEvents = true;

            _refreshDebounceTimer = new Timer { Interval = 350 };
            _refreshDebounceTimer.Tick += (_, __) =>
            {
                _refreshDebounceTimer.Stop();
                if (!IsDisposed)
                {
                    RefreshFileList();
                }
            };
        }

        private void StopFolderWatcher()
        {
            if (_folderWatcher != null)
            {
                _folderWatcher.EnableRaisingEvents = false;
                _folderWatcher.Created -= OnLibraryFolderChanged;
                _folderWatcher.Deleted -= OnLibraryFolderChanged;
                _folderWatcher.Renamed -= OnLibraryFolderRenamed;
                _folderWatcher.Changed -= OnLibraryFolderChanged;
                _folderWatcher.Dispose();
                _folderWatcher = null;
            }

            if (_refreshDebounceTimer != null)
            {
                _refreshDebounceTimer.Stop();
                _refreshDebounceTimer.Dispose();
                _refreshDebounceTimer = null;
            }
        }

        private void OnLibraryFolderChanged(object sender, FileSystemEventArgs e)
        {
            if (!IsLogoFileName(e.Name))
            {
                return;
            }

            ScheduleRefreshFromFolderChange();
        }

        private void OnLibraryFolderRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsLogoFileName(e.Name) && !IsLogoFileName(e.OldName))
            {
                return;
            }

            ScheduleRefreshFromFolderChange();
        }

        private void ScheduleRefreshFromFolderChange()
        {
            if (IsDisposed || _refreshDebounceTimer == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(ScheduleRefreshFromFolderChange));
                return;
            }

            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer.Start();
        }

        private static bool IsLogoFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var ext = Path.GetExtension(fileName);
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private void BalanceListViewColumns()
        {
            if (_lvFiles == null || _lvFiles.Columns.Count < 3 || _lvFiles.ClientSize.Width <= 0)
            {
                return;
            }

            var total = _lvFiles.ClientSize.Width;
            _lvFiles.Columns[0].Width = SttColumnWidth;
            _lvFiles.Columns[1].Width = ThumbnailColumnWidth;
            _lvFiles.Columns[2].Width = Math.Max(160, total - SttColumnWidth - ThumbnailColumnWidth);
        }

        private void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var backBrush = new SolidBrush(HeaderBackColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            var headerFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            headerFlags |= e.ColumnIndex == 0 || e.ColumnIndex == 1
                ? TextFormatFlags.HorizontalCenter
                : TextFormatFlags.Left;

            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                new Font("Segoe UI", 10F, FontStyle.Bold),
                e.Bounds,
                HeaderTextColor,
                headerFlags);

            DrawCellGridLines(e.Graphics, e.Bounds, drawRight: true, drawBottom: true);
        }

        private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            var backColor = e.Item.Selected ? RowSelectedBackColor : RowBackColor;
            var textColor = e.Item.Selected ? Color.White : RowTextColor;

            using (var backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            if (e.ColumnIndex == 1)
            {
                var path = e.Item.Tag as string;
                if (!string.IsNullOrWhiteSpace(path)
                    && _thumbnailCache.TryGetValue(path, out var thumb)
                    && thumb != null)
                {
                    var inner = Rectangle.Inflate(e.Bounds, -8, -8);
                    DrawThumbnail(e.Graphics, thumb, inner);
                }
            }
            else
            {
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                if (e.ColumnIndex == 0)
                {
                    flags |= TextFormatFlags.HorizontalCenter;
                }
                else
                {
                    flags |= TextFormatFlags.Left;
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    e.SubItem.Text,
                    e.Item.Font,
                    e.Bounds,
                    textColor,
                    flags);
            }

            DrawCellGridLines(e.Graphics, e.Bounds, drawRight: true, drawBottom: true);
        }

        private static void DrawCellGridLines(Graphics g, Rectangle bounds, bool drawRight, bool drawBottom)
        {
            using (var pen = new Pen(GridLineColor))
            {
                if (drawRight)
                {
                    g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
                }

                if (drawBottom)
                {
                    g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                }
            }
        }

        private static void DrawThumbnail(Graphics g, Image img, Rectangle bounds)
        {
            if (img == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var imageAspect = (float)img.Width / img.Height;
            var boundsAspect = (float)bounds.Width / bounds.Height;
            Rectangle dest;
            if (imageAspect > boundsAspect)
            {
                var h = (int)(bounds.Width / imageAspect);
                dest = new Rectangle(bounds.X, bounds.Y + (bounds.Height - h) / 2, bounds.Width, h);
            }
            else
            {
                var w = (int)(bounds.Height * imageAspect);
                dest = new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y, w, bounds.Height);
            }

            g.DrawImage(img, dest);
        }

        private string GetLibraryDirectory() => OmniBrandLogoLibrary.EnsureSharedLogosDirectory(_settings);

        private void RefreshFileList()
        {
            var selectedPaths = _lvFiles.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag as string)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
            var focusedPath = _lvFiles.FocusedItem?.Tag as string;

            ClearThumbnails();
            _lvFiles.BeginUpdate();
            _lvFiles.Items.Clear();
            var dir = GetLibraryDirectory();
            var index = 1;
            foreach (var name in OmniBrandLogoLibrary.ListLogoFileNames(_settings))
            {
                var path = Path.Combine(dir, name);
                var item = new ListViewItem(index.ToString());
                item.SubItems.Add(string.Empty);
                item.SubItems.Add(name);
                item.Tag = path;
                _lvFiles.Items.Add(item);
                index++;
            }

            _lvFiles.EndUpdate();

            RestoreSelection(selectedPaths, focusedPath);

            _lblStatus.Text = "Logo · "
                             + _lvFiles.Items.Count
                             + " file · "
                             + dir;
            BalanceListViewColumns();
            _ = LoadThumbnailsAsync();
        }

        private void RestoreSelection(IReadOnlyList<string> selectedPaths, string focusedPath)
        {
            if (_lvFiles.Items.Count == 0)
            {
                return;
            }

            ListViewItem focusItem = null;
            foreach (ListViewItem item in _lvFiles.Items)
            {
                var path = item.Tag as string;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (selectedPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                {
                    item.Selected = true;
                }

                if (!string.IsNullOrWhiteSpace(focusedPath)
                    && string.Equals(path, focusedPath, StringComparison.OrdinalIgnoreCase))
                {
                    focusItem = item;
                }
            }

            (focusItem ?? _lvFiles.Items[0])?.Focused = true;
        }

        private async Task LoadThumbnailsAsync()
        {
            var generation = ++_thumbGeneration;
            var paths = _lvFiles.Items
                .Cast<ListViewItem>()
                .Select(item => item.Tag as string)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var path in paths)
            {
                if (IsDisposed || generation != _thumbGeneration)
                {
                    return;
                }

                if (_thumbnailCache.ContainsKey(path))
                {
                    continue;
                }

                Image thumb = null;
                try
                {
                    thumb = await Task.Run(() => CreateThumbnailImage(path)).ConfigureAwait(true);
                }
                catch
                {
                    // ignore unreadable image
                }

                if (IsDisposed || generation != _thumbGeneration)
                {
                    thumb?.Dispose();
                    return;
                }

                if (thumb != null)
                {
                    _thumbnailCache[path] = thumb;
                    _loadedImages.Add(thumb);
                    _lvFiles.Invalidate();
                }
            }
        }

        private static Image CreateThumbnailImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using (var source = Image.FromFile(path))
            {
                var maxEdge = ListRowHeight - 12;
                var scale = Math.Min(
                    maxEdge / (float)Math.Max(1, source.Width),
                    maxEdge / (float)Math.Max(1, source.Height));
                var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                var height = Math.Max(1, (int)Math.Round(source.Height * scale));
                var thumb = new Bitmap(width, height);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(source, 0, 0, width, height);
                }

                return thumb;
            }
        }

        private void ClearThumbnails()
        {
            _thumbGeneration++;
            _thumbnailCache.Clear();
            foreach (var image in _loadedImages)
            {
                image?.Dispose();
            }

            _loadedImages.Clear();
        }

        private void DisposeResources()
        {
            StopFolderWatcher();
            ClearThumbnails();
            _listRowHeightSpacer?.Dispose();
        }

        private void DeleteSelectedLogos()
        {
            if (_lvFiles.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn một hoặc nhiều logo trong danh sách để xoá.",
                    "Xoá logo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var paths = _lvFiles.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag as string)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            var prompt = paths.Count == 1
                ? "Xoá logo «" + Path.GetFileName(paths[0]) + "» khỏi thư viện?"
                : "Xoá " + paths.Count + " logo đã chọn khỏi thư viện?";

            if (MessageBox.Show(this,
                    prompt + "\nFile sẽ bị xoá khỏi thư mục Assets\\Logos.",
                    "Xoá logo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            var deleted = 0;
            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "Không xoá được «" + Path.GetFileName(path) + "»: " + ex.Message,
                        "Xoá logo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            if (deleted > 0)
            {
                AppendStatus("Đã xoá " + deleted + " logo.");
                RefreshFileList();
            }
        }

        private async Task AddFilesAsync()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn file logo",
                Filter = "Ảnh logo|*.png;*.jpg;*.jpeg;*.webp|Tất cả|*.*",
                Multiselect = true,
                CheckFileExists = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.FileNames.Length == 0)
                {
                    return;
                }

                _btnAddFiles.Enabled = false;
                try
                {
                    var result = await Task.Run(() =>
                            OmniBrandLogoLibrary.CopyLogoFilesPreserveName(_settings, dlg.FileNames))
                        .ConfigureAwait(true);

                    foreach (var message in result.Messages)
                    {
                        AppendStatus(message);
                    }

                    AppendStatus("Đã thêm " + result.Copied + " logo"
                                 + (result.Skipped > 0 ? " (bỏ qua " + result.Skipped + ")." : "."));
                    RefreshFileList();
                }
                finally
                {
                    _btnAddFiles.Enabled = true;
                }
            }
        }

        private void OpenLibraryFolder()
        {
            var dir = GetLibraryDirectory();
            try
            {
                Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Mở thư mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ViewSelectedLogo()
        {
            if (_lvFiles.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn một logo trong danh sách để xem.",
                    "Xem logo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var path = _lvFiles.SelectedItems[0].Tag as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this,
                    "Không tìm thấy file logo đã chọn.",
                    "Xem logo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Xem logo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AppendStatus(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _lblStatus.Text = message;
            }
        }

        private static Label MkSectionLabel(string text) =>
            new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = SectionLabelHeight,
                Padding = new Padding(4, 18, 4, 14),
                ForeColor = Color.FromArgb(180, 220, 200),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

        private static Button MkButton(string text, Color tint) =>
            new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = tint,
                ForeColor = Color.White,
                Margin = new Padding(6, 0, 0, 0),
                Padding = new Padding(12, 4, 12, 4),
                Cursor = Cursors.Hand
            };
    }
}
