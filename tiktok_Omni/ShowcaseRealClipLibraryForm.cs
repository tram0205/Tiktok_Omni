using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>Quản lý thư viện clip quay tay theo loại SP — thêm/xem clip trong từng thư mục.</summary>
    internal sealed class ShowcaseRealClipLibraryForm : Form
    {
        private const int BaseClientWidth = 1180;
        private const int DefaultClientWidth = (int)(BaseClientWidth * 1.3);
        private const int DefaultClientHeight = 720;
        private const int FolderPanelPaddingLeft = 12;
        private const int FolderPanelPaddingRight = 6;
        private const int ClipPanelPaddingLeft = 6;
        private const int ClipPanelPaddingRight = 12;
        private const int SplitContentHorizontalMargin =
            FolderPanelPaddingLeft + FolderPanelPaddingRight + ClipPanelPaddingLeft + ClipPanelPaddingRight;
        private const int SectionLabelHeight = 72;
        private const int FolderListRowHeight = 64;
        private const int ClipListRowHeight = 64;
        private const int StatusBarHeight = 80;
        private const int DurationColumnWidth = 200;

        private readonly ListBox _lstFolders;
        private readonly ListView _lvClips;
        private readonly SplitContainer _splitMain;
        private readonly ImageList _clipListRowHeightSpacer;
        private readonly Label _lblStatus;
        private readonly Button _btnAddClips;
        private readonly Button _btnViewClip;
        private readonly Button _btnOpenFolder;
        private readonly Button _btnDeleteClips;
        private readonly AppSettings _settings;

        private FileSystemWatcher _folderWatcher;
        private System.Windows.Forms.Timer _refreshDebounceTimer;

        private bool _balancingSplitPanels;
        private int _clipListGeneration;
        private readonly Dictionary<string, double> _clipDurationCache =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private List<ShowcaseCtaBrollLibraryService.LibraryFolderEntry> _folders =
            new List<ShowcaseCtaBrollLibraryService.LibraryFolderEntry>();

        public ShowcaseRealClipLibraryForm(AppSettings settings = null)
        {
            _settings = settings;
            Text = "Thư viện clip thật";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(920, 560);
            ClientSize = new Size(DefaultClientWidth, DefaultClientHeight);
            Font = new Font("Segoe UI", 10.5F);

            var hint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 56,
                Padding = new Padding(16, 14, 16, 0),
                ForeColor = Color.FromArgb(170, 178, 192),
                Text = "Mỗi thư mục = một loại SP. Chọn «Loại SP» trên lưới khớp thư mục → cột «Công cụ Video» hiện clip tương ứng."
            };

            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = BackColor,
                Panel1MinSize = 200,
                Panel2MinSize = 120
            };
            _splitMain.Resize += (_, __) => BalanceSplitPanels();
            Resize += (_, __) => BalanceSplitPanels();

            _lstFolders = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                ItemHeight = FolderListRowHeight,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.FromArgb(230, 234, 242),
                Font = new Font("Segoe UI", 10.5F)
            };
            _lstFolders.SelectedIndexChanged += (_, __) => RefreshClipList();

            var folderPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(FolderPanelPaddingLeft, 8, FolderPanelPaddingRight, 8) };
            folderPanel.Controls.Add(MkSectionLabel("Thư mục loại SP"));
            var folderHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, SectionLabelHeight + 8, 0, 0) };
            folderHost.Controls.Add(_lstFolders);
            folderPanel.Controls.Add(folderHost);
            _splitMain.Panel1.Controls.Add(folderPanel);

            _lvClips = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(38, 42, 52),
                ForeColor = Color.FromArgb(230, 234, 242),
                Font = new Font("Segoe UI", 10.5F)
            };
            _lvClips.Columns.Add("Tên file", -1);
            _lvClips.Columns.Add("Thời lượng", DurationColumnWidth);
            _clipListRowHeightSpacer = new ImageList { ImageSize = new Size(1, ClipListRowHeight) };
            _clipListRowHeightSpacer.Images.Add(new Bitmap(1, ClipListRowHeight));
            _lvClips.SmallImageList = _clipListRowHeightSpacer;
            _lvClips.Resize += (_, __) => BalanceClipListColumns();
            _lvClips.DoubleClick += (_, __) => ViewSelectedClip();
            _lvClips.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelectedClips();
                    e.Handled = true;
                }
            };

            var clipPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(ClipPanelPaddingLeft, 8, ClipPanelPaddingRight, 8) };
            clipPanel.Controls.Add(MkSectionLabel("Clip trong thư mục"));
            var clipHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, SectionLabelHeight + 8, 0, 0) };
            clipHost.Controls.Add(_lvClips);
            clipPanel.Controls.Add(clipHost);
            _splitMain.Panel2.Controls.Add(clipPanel);

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
                Padding = new Padding(12, 10, 12, 12),
                BackColor = BackColor
            };

            var btnClose = MkButton("Đóng", Color.FromArgb(90, 96, 110));
            btnClose.DialogResult = DialogResult.OK;
            CancelButton = btnClose;

            _btnOpenFolder = MkButton("Mở thư mục", Color.FromArgb(56, 90, 78));
            _btnOpenFolder.Click += (_, __) => OpenSelectedFolder();

            _btnDeleteClips = MkButton("Xoá clip", Color.FromArgb(120, 64, 64));
            _btnDeleteClips.Click += (_, __) => DeleteSelectedClips();

            _btnAddClips = MkButton("Thêm clip thật", Color.FromArgb(72, 138, 118));
            _btnAddClips.Click += async (_, __) => await AddClipsAsync().ConfigureAwait(true);

            _btnViewClip = MkButton("Xem clip", Color.FromArgb(70, 92, 128));
            _btnViewClip.Click += (_, __) => ViewSelectedClip();

            footer.Controls.Add(btnClose);
            footer.Controls.Add(_btnOpenFolder);
            footer.Controls.Add(_btnDeleteClips);
            footer.Controls.Add(_btnAddClips);
            footer.Controls.Add(_btnViewClip);

            Controls.Add(_splitMain);
            Controls.Add(_lblStatus);
            Controls.Add(footer);
            Controls.Add(hint);

            FormClosed += (_, __) => StopFolderWatcher();

            Shown += (_, __) =>
            {
                BeginInvoke(new Action(() =>
                {
                    BalanceSplitPanels();
                    StartFolderWatcher();
                    RefreshAll();
                }));
            };
        }

        private void StartFolderWatcher()
        {
            StopFolderWatcher();

            var dir = ShowcaseCtaBrollLibraryService.GetCtaBRollsRootDirectory(true);
            _folderWatcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.CreationTime
            };
            _folderWatcher.Created += OnLibraryFolderChanged;
            _folderWatcher.Deleted += OnLibraryFolderChanged;
            _folderWatcher.Renamed += OnLibraryFolderRenamed;
            _folderWatcher.Changed += OnLibraryFolderChanged;
            _folderWatcher.EnableRaisingEvents = true;

            _refreshDebounceTimer = new System.Windows.Forms.Timer { Interval = 350 };
            _refreshDebounceTimer.Tick += (_, __) =>
            {
                _refreshDebounceTimer.Stop();
                if (!IsDisposed)
                {
                    RefreshAll();
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
            if (!IsClipLibraryPath(e.Name))
            {
                return;
            }

            ScheduleRefreshFromFolderChange();
        }

        private void OnLibraryFolderRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsClipLibraryPath(e.Name) && !IsClipLibraryPath(e.OldName))
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

        private static bool IsClipLibraryPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var ext = Path.GetExtension(name);
            if (ext.Length > 0)
            {
                return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void BalanceSplitPanels()
        {
            if (_balancingSplitPanels || _splitMain == null || _splitMain.IsDisposed)
            {
                return;
            }

            var totalWidth = _splitMain.Width;
            var splitterWidth = _splitMain.SplitterWidth;
            var minTotal = _splitMain.Panel1MinSize + _splitMain.Panel2MinSize + splitterWidth;
            if (totalWidth < minTotal)
            {
                return;
            }

            var contentWidth = totalWidth - splitterWidth;
            var usableWidth = Math.Max(0, contentWidth - SplitContentHorizontalMargin);
            var folderWidth = usableWidth / 3 + FolderPanelPaddingLeft + FolderPanelPaddingRight;

            var minDistance = _splitMain.Panel1MinSize;
            var maxDistance = totalWidth - _splitMain.Panel2MinSize - splitterWidth;
            if (maxDistance < minDistance)
            {
                return;
            }

            folderWidth = Math.Max(minDistance, Math.Min(maxDistance, folderWidth));
            if (Math.Abs(_splitMain.SplitterDistance - folderWidth) <= 2)
            {
                return;
            }

            _balancingSplitPanels = true;
            try
            {
                _splitMain.SplitterDistance = folderWidth;
            }
            catch (ArgumentOutOfRangeException)
            {
                // SplitContainer chưa layout xong — thử lại ở lần resize sau.
            }
            finally
            {
                _balancingSplitPanels = false;
            }
        }

        private void BalanceClipListColumns()
        {
            if (_lvClips == null || _lvClips.Columns.Count < 2 || _lvClips.ClientSize.Width <= 0)
            {
                return;
            }

            var total = _lvClips.ClientSize.Width;
            var durationWidth = Math.Min(DurationColumnWidth, Math.Max(96, total - 120));
            var nameWidth = Math.Max(120, total - durationWidth);
            _lvClips.Columns[0].Width = nameWidth;
            _lvClips.Columns[1].Width = durationWidth;
        }

        private void RefreshAll()
        {
            ShowcaseCtaBrollSubLibraryPresets.EnsureDefaultFolders(ShowcaseCtaBrollSubLibraryPresets.AoDaiParentId);
            var selectedId = GetSelectedLibraryId();
            _folders = ShowcaseCtaBrollLibraryService.ListLibraryFolders(includeSubFolders: true).ToList();
            _lstFolders.BeginUpdate();
            _lstFolders.Items.Clear();
            foreach (var entry in _folders)
            {
                _lstFolders.Items.Add(entry);
            }

            _lstFolders.EndUpdate();

            if (_folders.Count == 0)
            {
                _lblStatus.Text = "Chưa có thư mục thư viện.";
                _lvClips.Items.Clear();
                return;
            }

            var index = 0;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                for (var i = 0; i < _folders.Count; i++)
                {
                    if (string.Equals(_folders[i].LibraryId, selectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            _lstFolders.SelectedIndex = index;
            RefreshClipList();
        }

        private void RefreshClipList()
        {
            _lvClips.Items.Clear();
            var libraryId = GetSelectedLibraryId();
            if (string.IsNullOrWhiteSpace(libraryId))
            {
                _lblStatus.Text = "Chọn một thư mục loại SP.";
                return;
            }

            var dir = ShowcaseCtaBrollLibraryService.GetLibraryDirectory(libraryId, false);
            var clips = ShowcaseCtaBrollLibraryService.EnumerateClipFiles(libraryId);
            var missing = new List<string>();
            foreach (var clipPath in clips)
            {
                if (string.IsNullOrWhiteSpace(clipPath) || !File.Exists(clipPath))
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(clipPath);
                }
                catch
                {
                    fullPath = clipPath;
                }

                var durationLabel = _clipDurationCache.TryGetValue(fullPath, out var cachedSeconds)
                    ? FormatDurationLabel(cachedSeconds)
                    : "…";
                _lvClips.Items.Add(new ListViewItem(new[] { Path.GetFileName(clipPath), durationLabel })
                {
                    Tag = fullPath
                });

                if (!_clipDurationCache.ContainsKey(fullPath))
                {
                    missing.Add(fullPath);
                }
            }

            _lblStatus.Text = ShowcaseCtaBrollLibraryService.GetLibraryDisplayLabel(libraryId)
                             + " — "
                             + clips.Count
                             + " clip · "
                             + dir;
            BalanceClipListColumns();

            if (missing.Count > 0)
            {
                var generation = ++_clipListGeneration;
                _ = ProbeClipDurationsAsync(missing, generation);
            }
        }

        private async Task ProbeClipDurationsAsync(IList<string> clipPaths, int generation)
        {
            string ffprobeExe;
            try
            {
                ffprobeExe = await Task.Run(() =>
                        FfmpegToolkitService.TryResolve(_settings, out var toolkit, out _)
                            ? toolkit.FfprobeExe
                            : FfmpegToolkitService.GetBundledFfprobePath())
                    .ConfigureAwait(true);
            }
            catch
            {
                return;
            }

            foreach (var clipPath in clipPaths)
            {
                if (IsDisposed || generation != _clipListGeneration)
                {
                    return;
                }

                double seconds;
                try
                {
                    seconds = await ShowcaseCtaBrollLibraryService
                        .ProbeDurationSecondsAsync(clipPath, ffprobeExe, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                catch
                {
                    seconds = 0d;
                }

                _clipDurationCache[clipPath] = seconds;

                if (IsDisposed || generation != _clipListGeneration)
                {
                    return;
                }

                foreach (ListViewItem item in _lvClips.Items)
                {
                    if (item.Tag is string tag && string.Equals(tag, clipPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.SubItems[1].Text = FormatDurationLabel(seconds);
                        break;
                    }
                }
            }
        }

        private static string FormatDurationLabel(double seconds) =>
            seconds > 0
                ? seconds.ToString("0.#", CultureInfo.InvariantCulture) + "s"
                : "?";

        private string GetSelectedLibraryId()
        {
            return _lstFolders.SelectedItem is ShowcaseCtaBrollLibraryService.LibraryFolderEntry entry
                ? entry.LibraryId
                : string.Empty;
        }

        private async Task AddClipsAsync()
        {
            var libraryId = GetSelectedLibraryId();
            if (string.IsNullOrWhiteSpace(libraryId))
            {
                libraryId = PromptLibraryFolder();
                if (string.IsNullOrWhiteSpace(libraryId))
                {
                    return;
                }

                SelectFolderById(libraryId);
            }

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn video thêm vào thư viện",
                Filter = "Video|*.mp4;*.mov;*.webm;*.mkv|Tất cả|*.*",
                Multiselect = true,
                CheckFileExists = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.FileNames.Length == 0)
                {
                    return;
                }

                _btnAddClips.Enabled = false;
                try
                {
                    var files = dlg.FileNames;
                    var result = await Task.Run(() =>
                            ShowcaseCtaBrollLibraryService.CopyClipsPreserveName(libraryId, files))
                        .ConfigureAwait(true);

                    foreach (var message in result.Messages)
                    {
                        AppendStatus(message);
                    }

                    AppendStatus("Đã thêm "
                                 + result.Copied
                                 + " clip vào «"
                                 + ShowcaseCtaBrollLibraryService.GetLibraryDisplayLabel(libraryId)
                                 + "»"
                                 + (result.Skipped > 0 ? " (bỏ qua " + result.Skipped + ")." : "."));
                    RefreshAll();
                }
                finally
                {
                    _btnAddClips.Enabled = true;
                }
            }
        }

        private string PromptLibraryFolder()
        {
            var folders = ShowcaseCtaBrollLibraryService.ListLibraryFolders(includeSubFolders: true);
            if (folders.Count == 0)
            {
                MessageBox.Show(this,
                    "Chưa có thư mục thư viện.",
                    "Thêm clip thật",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return string.Empty;
            }

            using (var dlg = new ShowcaseRealClipLibraryFolderPickerForm(folders))
            {
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedLibraryId : string.Empty;
            }
        }

        private void SelectFolderById(string libraryId)
        {
            for (var i = 0; i < _folders.Count; i++)
            {
                if (string.Equals(_folders[i].LibraryId, libraryId, StringComparison.OrdinalIgnoreCase))
                {
                    _lstFolders.SelectedIndex = i;
                    return;
                }
            }
        }

        private void ViewSelectedClip()
        {
            if (_lvClips.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn một clip trong danh sách để xem.",
                    "Xem clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var path = _lvClips.SelectedItems[0].Tag as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this,
                    "Không tìm thấy file clip đã chọn.",
                    "Xem clip",
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
                MessageBox.Show(this,
                    "Không mở được clip «" + Path.GetFileName(path) + "»: " + ex.Message,
                    "Xem clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void OpenSelectedFolder()
        {
            var libraryId = GetSelectedLibraryId();
            if (string.IsNullOrWhiteSpace(libraryId))
            {
                MessageBox.Show(this,
                    "Chọn một thư mục loại SP trước.",
                    "Mở thư mục",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var dir = ShowcaseCtaBrollLibraryService.GetLibraryDirectory(libraryId, true);
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Mở thư mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteSelectedClips()
        {
            if (_lvClips.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    "Chọn một hoặc nhiều clip trong danh sách để xoá.",
                    "Xoá clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var paths = _lvClips.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag as string)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                return;
            }

            var libraryId = GetSelectedLibraryId();
            var folderLabel = string.IsNullOrWhiteSpace(libraryId)
                ? "thư viện"
                : "«" + ShowcaseCtaBrollLibraryService.GetLibraryDisplayLabel(libraryId) + "»";

            var prompt = paths.Count == 1
                ? "Xoá clip «" + Path.GetFileName(paths[0]) + "» khỏi " + folderLabel + "?"
                : "Xoá " + paths.Count + " clip đã chọn khỏi " + folderLabel + "?";

            if (MessageBox.Show(this,
                    prompt + "\nFile sẽ bị xoá khỏi thư mục thư viện.",
                    "Xoá clip",
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
                        _clipDurationCache.Remove(path);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "Không xoá được «" + Path.GetFileName(path) + "»: " + ex.Message,
                        "Xoá clip",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            if (deleted > 0)
            {
                AppendStatus("Đã xoá " + deleted + " clip.");
                RefreshAll();
            }
        }

        private void AppendStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _lblStatus.Text = message;
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

    internal sealed class ShowcaseRealClipLibraryFolderPickerForm : Form
    {
        private readonly ComboBox _cbFolders;

        public ShowcaseRealClipLibraryFolderPickerForm(
            IReadOnlyList<ShowcaseCtaBrollLibraryService.LibraryFolderEntry> folders)
        {
            Text = "Thêm clip vào thư mục nào?";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            ClientSize = new Size(520, 168);
            Font = new Font("Segoe UI", 10.5F);

            var lbl = new Label
            {
                Text = "Chọn thư mục loại SP:",
                AutoSize = true,
                Location = new Point(20, 22),
                ForeColor = Color.FromArgb(220, 224, 232)
            };

            _cbFolders = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(20, 52),
                Width = 472,
                BackColor = Color.FromArgb(24, 27, 34),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var folder in folders ?? Array.Empty<ShowcaseCtaBrollLibraryService.LibraryFolderEntry>())
            {
                _cbFolders.Items.Add(folder);
            }

            if (_cbFolders.Items.Count > 0)
            {
                _cbFolders.SelectedIndex = 0;
            }

            var btnOk = new Button
            {
                Text = "Duyệt video…",
                DialogResult = DialogResult.OK,
                Location = new Point(292, 108),
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(72, 138, 118),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            AcceptButton = btnOk;

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location = new Point(420, 108),
                Width = 72,
                Height = 34,
                BackColor = Color.FromArgb(90, 96, 110),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            CancelButton = btnCancel;

            Controls.Add(lbl);
            Controls.Add(_cbFolders);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }

        public string SelectedLibraryId =>
            _cbFolders.SelectedItem is ShowcaseCtaBrollLibraryService.LibraryFolderEntry entry
                ? entry.LibraryId
                : string.Empty;
    }
}
