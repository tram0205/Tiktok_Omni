using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private CheckBox chkMascotUseLipSync;
        private TextBox txtMouthClosedPath;
        private TextBox txtMouthOpenSmallPath;
        private TextBox txtMouthOpenPath;
        private Button btnBrowseMouthClosed;
        private Button btnBrowseMouthOpenSmall;
        private Button btnBrowseMouthOpen;
        private Button btnPreviewLipSyncOverlay;
        private Panel pnlLipSyncPreview;
        private PictureBox pbLipSyncBase;
        private Panel pnlMouthMarker;
        private Label lblMouthOverlayCoords;
        private AvatarIdentityPackConfig _mascotIdentityPackConfig = new AvatarIdentityPackConfig();
        private bool _isDraggingMouthMarker;
        private Point _mouthDragOffset;
        private const int LipSyncRefWidth = 1080;
        private const int LipSyncRefHeight = 1920;

        private void WireMascotLipSyncEvents()
        {
            if (cbMascotProfile != null)
            {
                cbMascotProfile.SelectedIndexChanged += (s, e) => LoadMascotIdentityPackForSelectedProfile();
            }

            if (txtMascotImagePath != null)
            {
                txtMascotImagePath.TextChanged += (s, e) => RefreshLipSyncPreviewBaseImage();
            }
        }

        private void LoadMascotIdentityPackForSelectedProfile()
        {
            var profile = GetSelectedMascotProfileName();
            _mascotIdentityPackConfig = AvatarIdentityPackStore.LoadOrCreate(profile);
            var files = AvatarIdentityPackStore.SyncIdentityImagesFromVault(profile);
            if (files.Count >= 3)
            {
                _mascotIdentityPackConfig.IdentityImagePaths = files;
            }

            SyncMascotIdentityPackUi(profile, files);
            SyncMouthPathsToUi();
            PositionMouthMarkerFromConfig();
            RefreshLipSyncPreviewBaseImage();
        }

        private void SyncMascotIdentityPackUi(string profile, System.Collections.Generic.List<string> files)
        {
            if (txtAvatarIdentityPack != null)
            {
                var dir = AvatarIdentityPackStore.GetProfileDirectory(profile);
                txtAvatarIdentityPack.Text = $"{dir} ({files.Count} ảnh)";
            }
        }

        private void SyncMouthPathsToUi()
        {
            var pack = _mascotIdentityPackConfig ?? new AvatarIdentityPackConfig();
            if (txtMouthClosedPath != null)
            {
                txtMouthClosedPath.Text = pack.MouthClosedPath ?? string.Empty;
            }

            if (txtMouthOpenSmallPath != null)
            {
                txtMouthOpenSmallPath.Text = pack.MouthOpenSmallPath ?? string.Empty;
            }

            if (txtMouthOpenPath != null)
            {
                txtMouthOpenPath.Text = pack.MouthOpenPath ?? string.Empty;
            }

            UpdateMouthOverlayCoordLabel();
        }

        private void SaveMascotIdentityPackConfig()
        {
            var profile = GetSelectedMascotProfileName();
            if (_mascotIdentityPackConfig == null)
            {
                _mascotIdentityPackConfig = new AvatarIdentityPackConfig();
            }

            _mascotIdentityPackConfig.MouthClosedPath = txtMouthClosedPath?.Text?.Trim() ?? string.Empty;
            _mascotIdentityPackConfig.MouthOpenSmallPath = txtMouthOpenSmallPath?.Text?.Trim() ?? string.Empty;
            _mascotIdentityPackConfig.MouthOpenPath = txtMouthOpenPath?.Text?.Trim() ?? string.Empty;
            _mascotIdentityPackConfig.IdentityImagePaths =
                AvatarIdentityPackStore.SyncIdentityImagesFromVault(profile);
            AvatarIdentityPackStore.Save(profile, _mascotIdentityPackConfig);
        }

        private void UpdateMouthOverlayCoordLabel()
        {
            if (lblMouthOverlayCoords == null || _mascotIdentityPackConfig == null)
            {
                return;
            }

            lblMouthOverlayCoords.Text =
                $"Vị trí miệng: X={_mascotIdentityPackConfig.MouthOverlayX}, Y={_mascotIdentityPackConfig.MouthOverlayY} (1080×1920 — kéo/click để ghim)";
        }

        private void pbMascotMouthMarker_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || pnlMouthMarker == null)
            {
                return;
            }

            var display = GetImageDisplayRectangle();
            if (display.IsEmpty)
            {
                return;
            }

            var parentOff = GetMouthMarkerParentOffset();
            var size = pnlMouthMarker.Size;
            var cx = Math.Max(display.Left, Math.Min(display.Right, e.X));
            var cy = Math.Max(display.Top, Math.Min(display.Bottom, e.Y));
            pnlMouthMarker.Location = new Point(
                parentOff.X + cx - size.Width / 2,
                parentOff.Y + cy - size.Height / 2);
            CommitMouthMarkerToConfig();
        }

        private void BrowseMouthImage(TextBox target, Action afterPick)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.gif";
                dialog.Title = "Chọn ảnh miệng";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                target.Text = dialog.FileName;
                afterPick?.Invoke();
            }
        }

        private void btnBrowseMouthClosed_Click(object sender, EventArgs e)
        {
            BrowseMouthImage(txtMouthClosedPath, () =>
            {
                SaveMascotIdentityPackConfig();
                SyncMouthPathsToUi();
            });
        }

        private void btnBrowseMouthOpenSmall_Click(object sender, EventArgs e)
        {
            BrowseMouthImage(txtMouthOpenSmallPath, () =>
            {
                SaveMascotIdentityPackConfig();
                SyncMouthPathsToUi();
            });
        }

        private void btnBrowseMouthOpen_Click(object sender, EventArgs e)
        {
            BrowseMouthImage(txtMouthOpenPath, () =>
            {
                SaveMascotIdentityPackConfig();
                SyncMouthPathsToUi();
            });
        }

        private void chkMascotUseLipSync_CheckedChanged(object sender, EventArgs e)
        {
            SaveMascotIdentityPackConfig();
        }

        private void RefreshLipSyncPreviewBaseImage()
        {
            var pb = MascotLipSyncPictureBox;
            if (pb == null)
            {
                return;
            }

            var path = txtMascotImagePath?.Text?.Trim() ?? string.Empty;
            var old = pb.Image;
            pb.Image = null;
            old?.Dispose();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                {
                    pb.Image = new Bitmap(img);
                }
            }
            catch
            {
                // ignored
            }

            PositionMouthMarkerFromConfig();
        }

        private PictureBox MascotLipSyncPictureBox => pbMascotMouthMarker ?? pbLipSyncBase;

        private Rectangle GetImageDisplayRectangle()
        {
            var pb = MascotLipSyncPictureBox;
            if (pb?.Image == null || pb.ClientSize.Width <= 0 || pb.ClientSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            var image = pb.Image;
            var ratio = Math.Min(
                pb.ClientSize.Width / (double)image.Width,
                pb.ClientSize.Height / (double)image.Height);
            var w = (int)Math.Round(image.Width * ratio);
            var h = (int)Math.Round(image.Height * ratio);
            var x = (pb.ClientSize.Width - w) / 2;
            var y = (pb.ClientSize.Height - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        private Point GetMouthMarkerParentOffset()
        {
            var pb = MascotLipSyncPictureBox;
            if (pb == null)
            {
                return Point.Empty;
            }

            return pb.Location;
        }

        private void PositionMouthMarkerFromConfig()
        {
            if (pnlMouthMarker == null || _mascotIdentityPackConfig == null)
            {
                return;
            }

            var display = GetImageDisplayRectangle();
            if (display.IsEmpty)
            {
                return;
            }

            var parentOff = GetMouthMarkerParentOffset();
            var scaleX = display.Width / (double)LipSyncRefWidth;
            var scaleY = display.Height / (double)LipSyncRefHeight;
            var cx = parentOff.X + display.X + (int)Math.Round(_mascotIdentityPackConfig.MouthOverlayX * scaleX);
            var cy = parentOff.Y + display.Y + (int)Math.Round(_mascotIdentityPackConfig.MouthOverlayY * scaleY);
            var size = pnlMouthMarker.Size;
            pnlMouthMarker.Location = new Point(
                Math.Max(0, cx - size.Width / 2),
                Math.Max(0, cy - size.Height / 2));
            UpdateMouthOverlayCoordLabel();
        }

        private void CommitMouthMarkerToConfig()
        {
            if (_mascotIdentityPackConfig == null || pnlMouthMarker == null)
            {
                return;
            }

            var display = GetImageDisplayRectangle();
            if (display.IsEmpty)
            {
                return;
            }

            var parentOff = GetMouthMarkerParentOffset();
            var centerX = pnlMouthMarker.Location.X + pnlMouthMarker.Width / 2 - parentOff.X - display.X;
            var centerY = pnlMouthMarker.Location.Y + pnlMouthMarker.Height / 2 - parentOff.Y - display.Y;
            var scaleX = LipSyncRefWidth / (double)Math.Max(1, display.Width);
            var scaleY = LipSyncRefHeight / (double)Math.Max(1, display.Height);
            _mascotIdentityPackConfig.MouthOverlayX = (int)Math.Round(centerX * scaleX);
            _mascotIdentityPackConfig.MouthOverlayY = (int)Math.Round(centerY * scaleY);
            _mascotIdentityPackConfig.MouthOverlayX = Math.Max(0, Math.Min(LipSyncRefWidth - 20, _mascotIdentityPackConfig.MouthOverlayX));
            _mascotIdentityPackConfig.MouthOverlayY = Math.Max(0, Math.Min(LipSyncRefHeight - 20, _mascotIdentityPackConfig.MouthOverlayY));
            SaveMascotIdentityPackConfig();
            UpdateMouthOverlayCoordLabel();
        }

        private void pnlMouthMarker_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _isDraggingMouthMarker = true;
            _mouthDragOffset = new Point(e.X, e.Y);
        }

        private void pnlMouthMarker_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingMouthMarker || pnlMouthMarker == null || MascotLipSyncPictureBox == null)
            {
                return;
            }

            var newLoc = pnlMouthMarker.Location;
            newLoc.X += e.X - _mouthDragOffset.X;
            newLoc.Y += e.Y - _mouthDragOffset.Y;
            var display = GetImageDisplayRectangle();
            if (!display.IsEmpty)
            {
                var parentOff = GetMouthMarkerParentOffset();
                var minX = parentOff.X + display.Left;
                var minY = parentOff.Y + display.Top;
                var maxX = parentOff.X + display.Right - pnlMouthMarker.Width;
                var maxY = parentOff.Y + display.Bottom - pnlMouthMarker.Height;
                newLoc.X = Math.Max(minX, Math.Min(maxX, newLoc.X));
                newLoc.Y = Math.Max(minY, Math.Min(maxY, newLoc.Y));
            }

            pnlMouthMarker.Location = newLoc;
        }

        private void pnlMouthMarker_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isDraggingMouthMarker)
            {
                return;
            }

            _isDraggingMouthMarker = false;
            CommitMouthMarkerToConfig();
        }

        private void pbLipSyncBase_Resize(object sender, EventArgs e)
        {
            PositionMouthMarkerFromConfig();
        }

        private async void btnPreviewLipSyncOverlay_Click(object sender, EventArgs e)
        {
            var mascotPath = txtMascotImagePath?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mascotPath) || !File.Exists(mascotPath))
            {
                Log("LipSync Preview: chọn ảnh mascot gốc trước.");
                return;
            }

            SaveMascotIdentityPackConfig();
            if (!AvatarIdentityPackStore.HasValidMouthAssets(_mascotIdentityPackConfig))
            {
                Log("LipSync Preview: cần MouthClosed + MouthOpen (Small/Large).");
                return;
            }

            btnPreviewLipSyncOverlay.Enabled = false;
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                var theme = txtMascotChannelTheme?.Text?.Trim() ?? "Xin chào các bạn";
                var audioDir = Path.Combine(Path.GetTempPath(), "tiktok_omni_lipsync_preview");
                Directory.CreateDirectory(audioDir);
                var audioPath = Path.Combine(audioDir, "preview_narration.mp3");
                await _videoProcessingService.GenerateMascotPreviewNarrationAsync(
                    theme,
                    theme,
                    settings,
                    audioPath,
                    Log,
                    CancellationToken.None).ConfigureAwait(true);

                if (!File.Exists(audioPath))
                {
                    Log("LipSync Preview: không tạo được audio thử — kiểm tra TTS API.");
                    return;
                }

                var outPath = Path.Combine(audioDir, "lipsync_preview.mp4");
                var lip = new LipSyncService();
                var request = new LipSyncService.LipSyncRenderRequest
                {
                    BaseImagePath = mascotPath,
                    AudioPath = audioPath,
                    MouthClosedPath = _mascotIdentityPackConfig.MouthClosedPath,
                    MouthOpenSmallPath = _mascotIdentityPackConfig.MouthOpenSmallPath,
                    MouthOpenPath = _mascotIdentityPackConfig.MouthOpenPath,
                    OverlayX = _mascotIdentityPackConfig.MouthOverlayX,
                    OverlayY = _mascotIdentityPackConfig.MouthOverlayY,
                    OverlayScale = _mascotIdentityPackConfig.MouthOverlayScale <= 0
                        ? 1d
                        : _mascotIdentityPackConfig.MouthOverlayScale,
                    OutputPath = outPath
                };

                await lip.RenderPreviewOnBaseAsync(request, settings, Log, CancellationToken.None).ConfigureAwait(true);
                Log("LipSync Preview: " + outPath);
                try
                {
                    Process.Start(outPath);
                }
                catch
                {
                    // ignored
                }
            }
            catch (Exception ex)
            {
                Log("LipSync Preview lỗi: " + ex.Message);
            }
            finally
            {
                btnPreviewLipSyncOverlay.Enabled = true;
            }
        }

        private AvatarIdentityPackConfig BuildMascotLipSyncPackForEnqueue()
        {
            SaveMascotIdentityPackConfig();
            return _mascotIdentityPackConfig ?? new AvatarIdentityPackConfig();
        }
    }
}
