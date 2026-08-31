using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed class ProductAdImageGeminiWatermarkDialog : Form
    {
        private readonly List<string> _files = new List<string>();
        private readonly ListBox _lst;
        private readonly Label _lblHint;
        private readonly Label _lblStatus;
        private readonly ComboBox _cboRegion;
        private readonly Button _btnProcess;
        private readonly Button _btnAddFiles;
        private readonly Button _btnAddFolder;
        private readonly Button _btnRemove;
        private readonly Button _btnOpenFolder;
        private bool _busy;

        public int ProcessedCount { get; private set; }
        public string LastOutputFolder { get; private set; }

        public ProductAdImageGeminiWatermarkDialog()
        {
            Text = "Xoá logo Gemini";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 11F);
            MinimumSize = new Size(1080, 700);
            ClientSize = new Size(1160, 760);
            Padding = new Padding(20, 16, 20, 16);
            AllowDrop = true;

            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 96,
                Padding = new Padding(2, 8, 2, 12),
                ForeColor = Color.FromArgb(180, 186, 198),
                UseCompatibleTextRendering = true,
                Text = "Chọn ảnh đã tải từ Gemini — app xoá ngôi sao 4 cánh góc phải dưới và lưu file mới «tên_nologo.png» cạnh file gốc. Không ghi đè bản gốc. Không gỡ được watermark ẩn (SynthID)."
            };

            _lst = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 22, 28),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                SelectionMode = SelectionMode.MultiExtended,
                HorizontalScrollbar = true
            };

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 196,
                Padding = new Padding(0, 14, 0, 4),
                BackColor = Color.FromArgb(31, 34, 42)
            };

            var flpTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = Padding.Empty
            };

            _btnAddFiles = CreateButton("Chọn ảnh", Color.FromArgb(52, 92, 158));
            _btnAddFiles.Click += (_, __) => AddFiles();
            _btnAddFolder = CreateButton("Thêm thư mục", Color.FromArgb(72, 88, 118));
            _btnAddFolder.Click += (_, __) => AddFolder();
            _btnRemove = CreateButton("Bỏ khỏi list", Color.FromArgb(88, 94, 112));
            _btnRemove.Click += (_, __) => RemoveSelected();

            var lblRegion = new Label
            {
                Text = "Vùng xoá",
                AutoSize = false,
                Width = 140,
                Height = 52,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(16, 0, 10, 0),
                ForeColor = Color.FromArgb(180, 186, 198),
                UseCompatibleTextRendering = true
            };
            _cboRegion = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 240,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 48, 58),
                ForeColor = Color.Gainsboro,
                Margin = new Padding(0, 8, 0, 0)
            };
            _cboRegion.Items.Add("Tự động (khuyên dùng)");
            _cboRegion.Items.Add("Góc nhỏ");
            _cboRegion.Items.Add("Góc vừa");
            _cboRegion.Items.Add("Góc lớn");
            _cboRegion.SelectedIndex = 0;

            flpTop.Controls.Add(_btnAddFiles);
            flpTop.Controls.Add(_btnAddFolder);
            flpTop.Controls.Add(_btnRemove);
            flpTop.Controls.Add(lblRegion);
            flpTop.Controls.Add(_cboRegion);

            var flpActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 4)
            };

            _btnProcess = CreateButton("Xử lý và lưu", Color.FromArgb(56, 110, 78));
            _btnProcess.Enabled = false;
            _btnProcess.Click += (_, __) => ProcessAll();
            _btnOpenFolder = CreateButton("Mở folder", Color.FromArgb(72, 88, 118));
            _btnOpenFolder.Enabled = false;
            _btnOpenFolder.Click += (_, __) => OpenLastFolder();
            var btnClose = CreateButton("Đóng", Color.FromArgb(68, 72, 86));
            btnClose.Click += (_, __) => Close();

            flpActions.Controls.Add(_btnProcess);
            flpActions.Controls.Add(_btnOpenFolder);
            flpActions.Controls.Add(btnClose);

            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(2, 4, 2, 6),
                ForeColor = Color.FromArgb(168, 176, 190),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true,
                Text = "Kéo thả ảnh hoặc thư mục vào đây."
            };

            pnlBottom.Controls.Add(flpActions);
            pnlBottom.Controls.Add(_lblStatus);
            pnlBottom.Controls.Add(flpTop);

            Controls.Add(_lst);
            Controls.Add(pnlBottom);
            Controls.Add(_lblHint);
            CancelButton = btnClose;

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (_busy)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (_busy || e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped == null)
            {
                return;
            }

            var added = 0;
            foreach (var path in dropped)
            {
                if (Directory.Exists(path))
                {
                    added += AddImagesFromFolder(path);
                }
                else
                {
                    added += AddPath(path) ? 1 : 0;
                }
            }

            RefreshList();
            _lblStatus.Text = added == 0
                ? "Không thêm được file ảnh nào."
                : "Đã thêm " + added + " ảnh.";
        }

        private void AddFiles()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh Gemini (có logo ngôi sao)",
                Filter = ProductAdImageGeminiWatermarkHelper.OpenFileFilter,
                Multiselect = true,
                CheckFileExists = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var added = 0;
                foreach (var path in dlg.FileNames)
                {
                    if (AddPath(path))
                    {
                        added++;
                    }
                }

                RefreshList();
                _lblStatus.Text = added == 0
                    ? "Không thêm file mới (trùng hoặc không phải ảnh)."
                    : "Đã thêm " + added + " ảnh.";
            }
        }

        private void AddFolder()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Chọn thư mục chứa ảnh Gemini",
                ShowNewFolderButton = false
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var added = AddImagesFromFolder(dlg.SelectedPath);
                RefreshList();
                _lblStatus.Text = added == 0
                    ? "Thư mục không có ảnh hỗ trợ (bỏ qua file _nologo)."
                    : "Đã thêm " + added + " ảnh từ thư mục.";
            }
        }

        private int AddImagesFromFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return 0;
            }

            var added = 0;
            foreach (var path in Directory.GetFiles(folder))
            {
                if (AddPath(path))
                {
                    added++;
                }
            }

            return added;
        }

        private bool AddPath(string path)
        {
            if (!ProductAdImageGeminiWatermarkHelper.IsSupportedImage(path) || !File.Exists(path))
            {
                return false;
            }

            if (ProductAdImageGeminiWatermarkHelper.LooksLikeAlreadyStripped(path))
            {
                return false;
            }

            var full = Path.GetFullPath(path);
            if (_files.Any(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _files.Add(full);
            return true;
        }

        private void RemoveSelected()
        {
            if (_lst.SelectedIndices.Count == 0)
            {
                return;
            }

            var indices = _lst.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (var i in indices)
            {
                if (i >= 0 && i < _files.Count)
                {
                    _files.RemoveAt(i);
                }
            }

            RefreshList();
        }

        private void RefreshList()
        {
            _lst.BeginUpdate();
            _lst.Items.Clear();
            foreach (var path in _files)
            {
                _lst.Items.Add(Path.GetFileName(path));
            }

            _lst.EndUpdate();
            _btnProcess.Enabled = !_busy && _files.Count > 0;
            if (_files.Count == 0)
            {
                _lblStatus.Text = "Chưa có ảnh trong danh sách.";
            }
        }

        private GeminiWatermarkRegionKind SelectedRegion()
        {
            switch (_cboRegion.SelectedIndex)
            {
                case 1:
                    return GeminiWatermarkRegionKind.Small;
                case 2:
                    return GeminiWatermarkRegionKind.Medium;
                case 3:
                    return GeminiWatermarkRegionKind.Large;
                default:
                    return GeminiWatermarkRegionKind.Auto;
            }
        }

        private void ProcessAll()
        {
            if (_busy || _files.Count == 0)
            {
                return;
            }

            _busy = true;
            SetBusy(true);
            ProcessedCount = 0;
            var ok = 0;
            var fail = 0;
            string lastFolder = null;
            var region = SelectedRegion();
            try
            {
                for (var i = 0; i < _files.Count; i++)
                {
                    var path = _files[i];
                    _lblStatus.Text = "Đang xử lý " + (i + 1) + "/" + _files.Count + " — " + Path.GetFileName(path);
                    _lblStatus.Refresh();
                    Application.DoEvents();

                    var result = ProductAdImageGeminiWatermarkHelper.ProcessFile(path, region);
                    if (result.Success)
                    {
                        ok++;
                        lastFolder = Path.GetDirectoryName(result.OutputPath);
                    }
                    else
                    {
                        fail++;
                    }
                }
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }

            ProcessedCount = ok;
            LastOutputFolder = lastFolder;
            _btnOpenFolder.Enabled = !string.IsNullOrWhiteSpace(lastFolder) && Directory.Exists(lastFolder);

            if (fail == 0)
            {
                _lblStatus.Text = "Xong — đã lưu " + ok + " file «_nologo.png» cạnh file gốc.";
            }
            else
            {
                _lblStatus.Text = "Xong — thành công " + ok + ", lỗi " + fail + ".";
            }
        }

        private void OpenLastFolder()
        {
            if (string.IsNullOrWhiteSpace(LastOutputFolder) || !Directory.Exists(LastOutputFolder))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = LastOutputFolder,
                UseShellExecute = true
            });
        }

        private void SetBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            _btnAddFiles.Enabled = !busy;
            _btnAddFolder.Enabled = !busy;
            _btnRemove.Enabled = !busy;
            _btnProcess.Enabled = !busy && _files.Count > 0;
            _cboRegion.Enabled = !busy;
            _lst.Enabled = !busy;
        }

        private static Button CreateButton(string text, Color back)
        {
            var font = new Font("Segoe UI", 11F, FontStyle.Bold);
            var textWidth = TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, 52),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            return new Button
            {
                Text = text,
                AutoSize = false,
                Width = Math.Max(132, textWidth + 36),
                Height = 52,
                MinimumSize = new Size(132, 52),
                FlatStyle = FlatStyle.Flat,
                Font = font,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true
            };
        }
    }
}
