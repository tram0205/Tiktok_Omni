using System;

using System.Diagnostics;

using System.Drawing;

using System.IO;

using System.Threading.Tasks;

using System.Windows.Forms;

using tiktok_Omni.Services;



namespace tiktok_Omni

{

    public partial class Form1

    {

        private Panel pnlReupPreviewHost;
        private Panel pnlReupPreviewAspect;
        private PictureBox pbReupPreview;

        private Label lblReupPreviewStage;

        private Label lblReupPreviewCaption;

        private Button btnReupPreviewPlaySource;

        private Button btnReupPreviewPlayOutput;

        private Button btnReupPreviewRefreshFrame;



        private string _reupPreviewSourcePath = string.Empty;

        private string _reupPreviewOutputPath = string.Empty;



        private void InitializeReupPreviewUi()

        {

            lblReupPreviewStage = new Label

            {

                Name = "lblReupPreviewStage",

                Text = "Ch\u1ECDn d\u00F2ng ho\u1EB7c b\u1EA5m \u00ABB\u1EAFt \u0111\u1EA7u render\u00BB \u0111\u1EC3 xem khung h\u00ECnh.",

                Dock = DockStyle.Top,

                Height = 36,

                ForeColor = Color.FromArgb(200, 204, 214),

                TextAlign = ContentAlignment.TopLeft

            };



            lblReupPreviewCaption = new Label

            {

                Name = "lblReupPreviewCaption",

                Text = string.Empty,

                Dock = DockStyle.Top,

                Height = 18,

                ForeColor = Color.FromArgb(140, 148, 165),

                AutoEllipsis = true

            };



            pbReupPreview = new PictureBox
            {
                Name = "pbReupPreview",
                Dock = DockStyle.None,
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                MinimumSize = new Size(80, 140)
            };
            pbReupPreview.DoubleClick += (_, __) => PlayReupPreviewVideo(ResolveReupPreviewPlayPath());

            pnlReupPreviewAspect = new Panel
            {
                Name = "pnlReupPreviewAspect",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 18, 24),
                Padding = new Padding(2)
            };
            pnlReupPreviewAspect.Controls.Add(pbReupPreview);
            pnlReupPreviewAspect.Resize += (_, __) => LayoutReupPreviewAspectBox();



            btnReupPreviewPlaySource = CreateToolbarButton("Ph\u00E1t ngu\u1ED3n", executeStyle: false, minWidth: 88);

            btnReupPreviewPlaySource.Name = "btnReupPreviewPlaySource";

            btnReupPreviewPlaySource.Click += (_, __) => PlayReupPreviewVideo(_reupPreviewSourcePath);



            btnReupPreviewPlayOutput = CreateToolbarButton("Ph\u00E1t th\u00E0nh ph\u1EA9m", executeStyle: false, minWidth: 100);

            btnReupPreviewPlayOutput.Name = "btnReupPreviewPlayOutput";

            btnReupPreviewPlayOutput.Click += (_, __) => PlayReupPreviewVideo(_reupPreviewOutputPath);



            btnReupPreviewRefreshFrame = CreateToolbarButton("L\u00E0m m\u1EDBi khung", executeStyle: false, minWidth: 88);

            btnReupPreviewRefreshFrame.Name = "btnReupPreviewRefreshFrame";

            btnReupPreviewRefreshFrame.Click += async (_, __) =>

            {

                if (TryGetVideoReupSelectedRow(out var row))

                {

                    await RefreshReupPreviewForRowAsync(row, lblReupPreviewStage?.Text).ConfigureAwait(true);

                }

            };



            var flpReupPreviewActions = CreateToolbarFlowPanel(dockRight: false, wrapContents: true);
            flpReupPreviewActions.Dock = DockStyle.Top;
            flpReupPreviewActions.FlowDirection = FlowDirection.TopDown;
            flpReupPreviewActions.WrapContents = false;
            flpReupPreviewActions.Controls.Add(btnReupPreviewPlaySource);
            flpReupPreviewActions.Controls.Add(btnReupPreviewPlayOutput);
            flpReupPreviewActions.Controls.Add(btnReupPreviewRefreshFrame);
            btnReupPreviewPlaySource.Width = 200;
            btnReupPreviewPlayOutput.Width = 200;
            btnReupPreviewRefreshFrame.Width = 200;



            var tblReupPreviewInner = new TableLayoutPanel

            {

                Dock = DockStyle.Fill,

                ColumnCount = 1,

                RowCount = 4,

                Padding = new Padding(0)

            };

            tblReupPreviewInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblReupPreviewInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblReupPreviewInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblReupPreviewInner.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            tblReupPreviewInner.Controls.Add(lblReupPreviewStage, 0, 0);

            tblReupPreviewInner.Controls.Add(lblReupPreviewCaption, 0, 1);

            tblReupPreviewInner.Controls.Add(flpReupPreviewActions, 0, 2);

            tblReupPreviewInner.Controls.Add(pnlReupPreviewAspect, 0, 3);



            var grpReupPreview = new GroupBox

            {

                Name = "grpReupPreview",

                Text = "Xem tr\u01B0\u1EDBc video",

                Dock = DockStyle.Fill,

                ForeColor = Color.FromArgb(200, 204, 214),

                FlatStyle = FlatStyle.Flat,

                Padding = new Padding(8, 6, 8, 8)

            };

            grpReupPreview.Controls.Add(tblReupPreviewInner);



            pnlReupPreviewHost = new Panel
            {
                Name = "pnlReupPreviewHost",
                Dock = DockStyle.Fill,
                Padding = new Padding(2, 2, 2, 2),
                BackColor = Color.FromArgb(31, 34, 42),
                MinimumSize = new Size(200, 120)
            };

            pnlReupPreviewHost.Controls.Add(grpReupPreview);

        }



        private void LayoutVideoReupShell()
        {
            if (dgvVideoReupInput != null)
            {
                dgvVideoReupInput.EnableHeadersVisualStyles = false;
                if (dgvVideoReupInput.ColumnHeadersHeight < AppGridHeaderHeight)
                {
                    dgvVideoReupInput.ColumnHeadersHeight = AppGridHeaderHeight;
                }

                dgvVideoReupInput.Dock = DockStyle.Fill;
            }

            LayoutReupPreviewAspectBox();
        }

        private void LayoutReupPreviewAspectBox()
        {
            if (pnlReupPreviewAspect == null || pbReupPreview == null)
            {
                return;
            }

            const double aspectWidthOverHeight = 9.0 / 16.0;
            var pad = 4;
            var availW = Math.Max(0, pnlReupPreviewAspect.ClientSize.Width - pad * 2);
            var availH = Math.Max(0, pnlReupPreviewAspect.ClientSize.Height - pad * 2);
            if (availW <= 0 || availH <= 0)
            {
                return;
            }

            var height = availH;
            var width = (int)Math.Round(height * aspectWidthOverHeight);
            if (width > availW)
            {
                width = availW;
                height = (int)Math.Round(width / aspectWidthOverHeight);
            }

            pbReupPreview.Size = new Size(Math.Max(80, width), Math.Max(140, height));
            pbReupPreview.Location = new Point(
                pad + Math.Max(0, (availW - pbReupPreview.Width) / 2),
                pad + Math.Max(0, (availH - pbReupPreview.Height) / 2));
        }

        private void SetReupPreviewStage(string stageText)

        {

            if (lblReupPreviewStage == null)

            {

                return;

            }



            void Apply()

            {

                lblReupPreviewStage.Text = stageText ?? string.Empty;

            }



            if (lblReupPreviewStage.InvokeRequired)

            {

                lblReupPreviewStage.Invoke(new Action(Apply));

                return;

            }



            Apply();

        }



        private async Task RefreshReupPreviewForRowAsync(VideoReupRowItem row, string stageHint = null)

        {

            if (pbReupPreview == null)

            {

                return;

            }



            if (row == null)

            {

                _reupPreviewSourcePath = string.Empty;

                _reupPreviewOutputPath = string.Empty;

                SetReupPreviewImage(null);

                SetReupPreviewStage("Ch\u1ECDn d\u00F2ng trong b\u1EA3ng \u0111\u1EC3 xem khung h\u00ECnh.");

                if (lblReupPreviewCaption != null)

                {

                    lblReupPreviewCaption.Text = string.Empty;

                }



                return;

            }



            _reupPreviewSourcePath = (row.ReupDownloadedVideoPath ?? string.Empty).Trim();

            _reupPreviewOutputPath = (row.LastRemixOutputPath ?? string.Empty).Trim();



            var preferOutput = !string.IsNullOrWhiteSpace(_reupPreviewOutputPath) && File.Exists(_reupPreviewOutputPath);

            var videoPath = preferOutput ? _reupPreviewOutputPath : _reupPreviewSourcePath;

            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))

            {

                SetReupPreviewImage(null);

                SetReupPreviewStage(stageHint ?? "Ch\u01B0a c\u00F3 file video \u2014 t\u1EA3i ngu\u1ED3n ho\u1EB7c render tr\u01B0\u1EDBc.");

                if (lblReupPreviewCaption != null)

                {

                    lblReupPreviewCaption.Text = string.Empty;

                }



                return;

            }



            if (!string.IsNullOrWhiteSpace(stageHint))

            {

                SetReupPreviewStage(stageHint);

            }

            else

            {

                SetReupPreviewStage(preferOutput ? "Khung h\u00ECnh \u2014 th\u00E0nh ph\u1EA9m" : "Khung h\u00ECnh \u2014 video ngu\u1ED3n");

            }



            if (lblReupPreviewCaption != null)

            {

                lblReupPreviewCaption.Text = Path.GetFileName(videoPath);

            }



            var settings = await _configManager.LoadAsync().ConfigureAwait(true);

            var ffmpeg = settings?.FfmpegPath;

            if (string.IsNullOrWhiteSpace(ffmpeg))

            {

                ffmpeg = "ffmpeg";

            }



            var thumbDir = Path.Combine(Path.GetTempPath(), "tiktok_Omni_reup_preview");

            Directory.CreateDirectory(thumbDir);

            var thumbPath = Path.Combine(thumbDir, Guid.NewGuid().ToString("N") + ".jpg");



            var ok = await Task.Run(() => ReupPreviewFrameService.TryExtractFrame(ffmpeg, videoPath, thumbPath, 1.0))

                .ConfigureAwait(true);



            if (!ok || !File.Exists(thumbPath))

            {

                SetReupPreviewImage(null);

                return;

            }



            try

            {

                using (var bmp = new Bitmap(thumbPath))

                {

                    SetReupPreviewImage(new Bitmap(bmp));

                }

            }

            catch

            {

                SetReupPreviewImage(null);

            }

        }



        private void SetReupPreviewImage(Image image)

        {

            if (pbReupPreview == null)

            {

                image?.Dispose();

                return;

            }



            void Apply()

            {

                var old = pbReupPreview.Image;

                pbReupPreview.Image = image;

                old?.Dispose();

            }



            if (pbReupPreview.InvokeRequired)

            {

                pbReupPreview.Invoke(new Action(Apply));

                return;

            }



            Apply();

        }



        private string ResolveReupPreviewPlayPath()

        {

            if (!string.IsNullOrWhiteSpace(_reupPreviewOutputPath) && File.Exists(_reupPreviewOutputPath))

            {

                return _reupPreviewOutputPath;

            }



            if (!string.IsNullOrWhiteSpace(_reupPreviewSourcePath) && File.Exists(_reupPreviewSourcePath))

            {

                return _reupPreviewSourcePath;

            }



            return string.Empty;

        }



        private void PlayReupPreviewVideo(string path)

        {

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))

            {

                LogVideoReup("Xem video: ch\u01B0a c\u00F3 file (t\u1EA3i ngu\u1ED3n ho\u1EB7c render tr\u01B0\u1EDBc).");

                return;

            }



            try

            {

                Process.Start(new ProcessStartInfo

                {

                    FileName = path,

                    UseShellExecute = true

                });

                LogVideoReup("Đ\u00E3 m\u1EDF video: " + Path.GetFileName(path));

            }

            catch (Exception ex)

            {

                LogVideoReup("Kh\u00F4ng m\u1EDF \u0111\u01B0\u1EE3c video: " + ex.Message);

            }

        }

    }

}


