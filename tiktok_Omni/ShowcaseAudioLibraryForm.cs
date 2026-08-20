using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Quản lý thư viện nhạc nền hoặc hiệu ứng âm thanh (Assets\Audio\Music | Sfx).</summary>
    internal sealed class ShowcaseAudioLibraryForm : Form
    {
        public enum LibraryKind
        {
            BackgroundMusic,
            SoundEffects
        }

        private readonly LibraryKind _kind;
        private readonly AppSettings _settings;
        private readonly ListView _lvFiles;
        private readonly Label _lblStatus;
        private readonly Button _btnAddFiles;
        private ImageList _listRowHeightSpacer;

        private const int SectionLabelHeight = 56;
        private const int StatusBarHeight = 60;
        private const int ListRowHeight = 44;
        private const int BaseClientWidth = 960;
        private const int DefaultClientWidth = (int)(BaseClientWidth * 1.3);
        private const int DefaultClientHeight = 728;
        private const int MinimumClientWidth = (int)(720 * 1.3);
        private const int MinimumClientHeight = 624;

        public ShowcaseAudioLibraryForm(LibraryKind kind, AppSettings settings = null)
        {
            _kind = kind;
            _settings = settings ?? new AppSettings();

            Text = kind == LibraryKind.BackgroundMusic
                ? "Thư viện nhạc nền"
                : "Hiệu ứng âm thanh";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(31, 34, 42);
            MinimumSize = new Size(MinimumClientWidth, MinimumClientHeight);
            ClientSize = new Size(DefaultClientWidth, DefaultClientHeight);
            Font = new Font("Segoe UI", 10.5F);
            FormClosed += (_, __) => _listRowHeightSpacer?.Dispose();

            var hint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 52,
                Padding = new Padding(16, 14, 16, 0),
                ForeColor = Color.FromArgb(170, 178, 192),
                Text = kind == LibraryKind.BackgroundMusic
                    ? "File nhạc nền dùng chung toàn app (Showcase, Quote, Reup, Slideshow…) — Gemini chọn background_music_id khi «Tạo kịch bản»."
                    : "File hiệu ứng ngắn dùng chung toàn app — Gemini chọn sfx_id / hook_sfx / cta_sfx khi «Tạo kịch bản»."
            };

            _lvFiles = new ListView
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
            _lvFiles.Columns.Add("Tên file", -2);
            _lvFiles.Columns.Add("Id Gemini", -2);
            _listRowHeightSpacer = new ImageList { ImageSize = new Size(1, ListRowHeight) };
            _listRowHeightSpacer.Images.Add(new Bitmap(1, ListRowHeight));
            _lvFiles.SmallImageList = _listRowHeightSpacer;
            _lvFiles.Resize += (_, __) => BalanceListViewColumns();
            _lvFiles.DoubleClick += (_, __) => PlaySelectedFile();

            var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 4) };
            listPanel.Controls.Add(MkSectionLabel("Danh sách file"));
            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, SectionLabelHeight + 8, 0, 0) };
            listHost.Controls.Add(_lvFiles);
            listPanel.Controls.Add(listHost);

            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = StatusBarHeight,
                Padding = new Padding(16, 14, 16, 14),
                ForeColor = Color.FromArgb(140, 148, 162),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
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

            var btnRefresh = MkButton("Làm mới", Color.FromArgb(58, 72, 88));
            btnRefresh.Click += (_, __) => RefreshFileList();

            var btnOpenFolder = MkButton("Mở thư mục", Color.FromArgb(56, 90, 78));
            btnOpenFolder.Click += (_, __) => OpenLibraryFolder();

            var btnPlay = MkButton("Nghe thử", Color.FromArgb(70, 92, 128));
            btnPlay.Click += (_, __) => PlaySelectedFile();

            _btnAddFiles = MkButton(
                kind == LibraryKind.BackgroundMusic ? "Thêm nhạc nền" : "Thêm hiệu ứng",
                kind == LibraryKind.BackgroundMusic ? Color.FromArgb(138, 58, 118) : Color.FromArgb(168, 118, 42));
            _btnAddFiles.Click += async (_, __) => await AddFilesAsync().ConfigureAwait(true);

            footer.Controls.Add(btnClose);
            footer.Controls.Add(btnRefresh);
            footer.Controls.Add(btnOpenFolder);
            footer.Controls.Add(btnPlay);
            footer.Controls.Add(_btnAddFiles);

            Controls.Add(listPanel);
            Controls.Add(_lblStatus);
            Controls.Add(footer);
            Controls.Add(hint);

            Shown += (_, __) =>
            {
                RefreshFileList();
                BalanceListViewColumns();
            };
        }

        private void BalanceListViewColumns()
        {
            if (_lvFiles == null || _lvFiles.Columns.Count < 2 || _lvFiles.ClientSize.Width <= 0)
            {
                return;
            }

            var total = _lvFiles.ClientSize.Width;
            var half = Math.Max(80, total / 2);
            _lvFiles.Columns[0].Width = half;
            _lvFiles.Columns[1].Width = total - half;
        }

        private string GetLibraryDirectory()
        {
            OmniAudioLibrary.EnsureSharedDirectoriesExist(_settings);
            return _kind == LibraryKind.BackgroundMusic
                ? OmniAudioLibrary.GetSharedMusicDirectory(_settings)
                : OmniAudioLibrary.GetSharedSfxDirectory(_settings);
        }

        private IReadOnlyList<string> ListFileNames()
        {
            return _kind == LibraryKind.BackgroundMusic
                ? OmniAudioLibrary.ListMusicFileNames(_settings)
                : OmniAudioLibrary.ListSfxFileNames(_settings);
        }

        private string GetOpenFileFilter()
        {
            return _kind == LibraryKind.BackgroundMusic
                ? "Nhạc nền|*.mp3;*.wav;*.m4a|Tất cả|*.*"
                : "Hiệu ứng âm thanh|*.mp3;*.wav;*.m4a;*.ogg|Tất cả|*.*";
        }

        private void RefreshFileList()
        {
            _lvFiles.Items.Clear();
            var dir = GetLibraryDirectory();
            foreach (var name in ListFileNames())
            {
                var id = OmniAudioLibrary.NormalizeId(Path.GetFileNameWithoutExtension(name));
                _lvFiles.Items.Add(new ListViewItem(new[] { name, id })
                {
                    Tag = Path.Combine(dir, name)
                });
            }

            _lblStatus.Text = (_kind == LibraryKind.BackgroundMusic ? "Nhạc nền" : "Hiệu ứng")
                               + " · "
                               + _lvFiles.Items.Count
                               + " file · "
                               + dir;
            BalanceListViewColumns();
        }

        private async Task AddFilesAsync()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = _kind == LibraryKind.BackgroundMusic
                    ? "Chọn file nhạc nền"
                    : "Chọn file hiệu ứng âm thanh",
                Filter = GetOpenFileFilter(),
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
                    var files = dlg.FileNames;
                    var result = await Task.Run(() =>
                        _kind == LibraryKind.BackgroundMusic
                            ? OmniAudioLibrary.CopyMusicFilesPreserveName(_settings, files)
                            : OmniAudioLibrary.CopySfxFilesPreserveName(_settings, files)).ConfigureAwait(true);

                    foreach (var message in result.Messages)
                    {
                        AppendStatus(message);
                    }

                    AppendStatus("Đã thêm " + result.Copied + " file"
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

        private void PlaySelectedFile()
        {
            if (_lvFiles.SelectedItems.Count == 0)
            {
                return;
            }

            var path = _lvFiles.SelectedItems[0].Tag as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Nghe thử", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                Padding = new Padding(4, 14, 4, 10),
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
