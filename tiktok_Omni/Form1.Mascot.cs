using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Mascot;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private DataGridView dgvMascotBatches;
        private FlowLayoutPanel flpMascotAvatarThumbnails;
        private Button btnMascotSelectAvatarVault;
        private Button btnAddMascotBatch;
        private Button btnMascotGenerateExcel;
        private Button btnMascotOpenExcel;
        private Button btnMascotRender;
        private BindingList<MascotBatchRow> _mascotBatchBinding;
        private readonly List<string> _mascotAvatarVaultPaths = new List<string>();
        private MascotBatchRenderPipeline _mascotBatchPipeline;
        private CancellationTokenSource _mascotBatchCts;
        private Label lblMascotGeminiBalance;
        private Label lblMascotElevenBalance;
        private CheckBox chkMascotUseVisualHookSfx;
        private TextBox txtMascotVisualHookSfx;

        private void BuildMascotStoryUi()
        {
            if (tabMascotStory == null) return;

            tabMascotStory.SuspendLayout();
            tabMascotStory.Controls.Clear();
            tabMascotStory.AutoScroll = false;
            tabMascotStory.BackColor = Color.FromArgb(32, 34, 44);

            var root = new TableLayoutPanel
            {
                Name = "tblMascotStoryRoot",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = tabMascotStory.BackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BuildMascotTopPanel(), 0, 0);
            root.Controls.Add(BuildMascotGridPanel(), 0, 1);
            root.Controls.Add(BuildMascotActionBar(), 0, 2);

            tabMascotStory.Controls.Add(root);
            tabMascotStory.ResumeLayout(true);

            _mascotBatchBinding = _mascotBatchBinding ?? new BindingList<MascotBatchRow>();
            dgvMascotBatches.DataSource = _mascotBatchBinding;
            WireMascotBatchGridColumns();

            _ = InitMascotTabProfilesAsync();
            _ = RefreshMascotApiBalanceDashboardAsync();
        }

        private Control BuildMascotTopPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12, 10, 12, 6), BackColor = Color.FromArgb(32, 34, 44) };

            lblMascotGeminiBalance = new Label { AutoSize = true, ForeColor = Color.Silver, Text = "Gemini: —", Margin = new Padding(0, 0, 12, 0) };
            lblMascotElevenBalance = new Label { AutoSize = true, ForeColor = Color.Silver, Text = "ElevenLabs: —" };
            var flpBalance = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, BackColor = panel.BackColor, Padding = new Padding(0, 0, 0, 6) };
            flpBalance.Controls.Add(lblMascotGeminiBalance);
            flpBalance.Controls.Add(lblMascotElevenBalance);
            panel.Controls.Add(flpBalance);

            txtMascotImagePath = new TextBox { Name = "txtMascotImagePath", Visible = false };
            chkMascotUseVisualHookSfx = new CheckBox { Name = "chkMascotUseVisualHookSfx", Visible = false };
            txtMascotVisualHookSfx = new TextBox { Name = "txtMascotVisualHookSfx", Visible = false };
            panel.Controls.Add(txtMascotImagePath);
            panel.Controls.Add(chkMascotUseVisualHookSfx);
            panel.Controls.Add(txtMascotVisualHookSfx);

            var tbl = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, BackColor = panel.BackColor };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddMascotLabeledRow(tbl, 0, "Profile:", () =>
            {
                cbMascotProfile = new ComboBox { Name = "cbMascotProfile", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 49, 60), ForeColor = Color.WhiteSmoke };
                cbMascotProfile.SelectedIndexChanged += (_, __) =>
                {
                    LoadMascotAvatarVaultForProfile();
                    LoadMascotIdentityPackForSelectedProfile();
                };
                return cbMascotProfile;
            });

            AddMascotLabeledRow(tbl, 1, "Chu de kenh:", () =>
            {
                txtMascotChannelTheme = new TextBox
                {
                    Name = "txtMascotChannelTheme",
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    Height = 72,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke
                };
                return txtMascotChannelTheme;
            });

            var grpAvatar = new GroupBox { Text = "Avatar Identity Pack (3-5 anh)", Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.Gainsboro, Padding = new Padding(8), BackColor = Color.FromArgb(38, 40, 50) };
            var avatarTbl = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, BackColor = grpAvatar.BackColor };
            btnMascotSelectAvatarVault = new Button { Text = "Chon 3-5 anh AvatarVault", AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 64, 77), ForeColor = Color.WhiteSmoke, Margin = new Padding(0, 0, 0, 6) };
            btnMascotSelectAvatarVault.FlatAppearance.BorderSize = 0;
            btnMascotSelectAvatarVault.Click += BtnMascotSelectAvatarVault_Click;
            flpMascotAvatarThumbnails = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Height = 110, AutoScroll = true, BackColor = Color.FromArgb(28, 30, 38) };
            avatarTbl.Controls.Add(btnMascotSelectAvatarVault, 0, 0);
            avatarTbl.Controls.Add(flpMascotAvatarThumbnails, 0, 1);
            grpAvatar.Controls.Add(avatarTbl);

            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.Controls.Add(grpAvatar, 0, 2);
            tbl.SetColumnSpan(grpAvatar, 2);

            panel.Controls.Add(tbl);
            return panel;
        }

        private Control BuildMascotGridPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4), BackColor = Color.FromArgb(32, 34, 44) };
            dgvMascotBatches = new DataGridView
            {
                Name = "dgvMascotBatches",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(22, 24, 30),
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false
            };
            dgvMascotBatches.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(48, 52, 64);
            dgvMascotBatches.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            dgvMascotBatches.DefaultCellStyle.BackColor = Color.FromArgb(28, 30, 38);
            dgvMascotBatches.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
            panel.Controls.Add(dgvMascotBatches);
            return panel;
        }

        private Control BuildMascotActionBar()
        {
            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(12, 8, 12, 10),
                BackColor = Color.FromArgb(28, 30, 38)
            };

            btnMascotRender = MakeMascotActionButton("Render Video tu Excel", Color.FromArgb(50, 110, 68));
            btnMascotRender.Click += BtnMascotRender_Click;
            btnMascotOpenExcel = MakeMascotActionButton("Mo file Excel", Color.FromArgb(78, 120, 166));
            btnMascotOpenExcel.Click += BtnMascotOpenExcel_Click;
            btnMascotGenerateExcel = MakeMascotActionButton("AI sinh Excel", Color.FromArgb(120, 90, 170));
            btnMascotGenerateExcel.Click += BtnMascotGenerateExcel_Click;
            btnAddMascotBatch = MakeMascotActionButton("Them dong moi", Color.FromArgb(60, 64, 77));
            btnAddMascotBatch.Click += BtnAddMascotBatch_Click;

            flp.Controls.AddRange(new Control[] { btnMascotRender, btnMascotOpenExcel, btnMascotGenerateExcel, btnAddMascotBatch });
            return flp;
        }

        private static Button MakeMascotActionButton(string text, Color back)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(140, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(6, 4, 0, 4)
            };
        }

        private static void AddMascotLabeledRow(TableLayoutPanel tbl, int row, string caption, Func<Control> create)
        {
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label { Text = caption, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightGray, Margin = new Padding(0, 6, 8, 6) };
            var ctrl = create();
            ctrl.Dock = DockStyle.Fill;
            ctrl.Margin = new Padding(0, 6, 0, 6);
            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(ctrl, 1, row);
        }

        private void WireMascotBatchGridColumns()
        {
            dgvMascotBatches.AutoGenerateColumns = false;
            dgvMascotBatches.Columns.Clear();
            dgvMascotBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MascotBatchRow.Profile), HeaderText = "Profile", FillWeight = 12 });
            dgvMascotBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MascotBatchRow.Request), HeaderText = "Yeu cau", FillWeight = 28 });
            dgvMascotBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MascotBatchRow.ExcelPath), HeaderText = "File Excel", FillWeight = 30 });
            dgvMascotBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MascotBatchRow.Status), HeaderText = "Trang thai", FillWeight = 12 });
            dgvMascotBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MascotBatchRow.OutputVideoPath), HeaderText = "Video dau ra", FillWeight = 18 });
        }

        private async Task InitMascotTabProfilesAsync()
        {
            try
            {
                RefreshAllProfileSelectors();
                LoadMascotAvatarVaultForProfile();
            }
            catch (Exception ex)
            {
                Log("[Mascot] Profile load: " + ex.Message);
            }
        }

        private void BtnAddMascotBatch_Click(object sender, EventArgs e)
        {
            var row = new MascotBatchRow
            {
                Profile = GetSelectedMascotProfileName(),
                Request = txtMascotChannelTheme?.Text?.Trim() ?? string.Empty,
                Status = "Cho"
            };
            _mascotBatchBinding.Add(row);
            dgvMascotBatches.ClearSelection();
            dgvMascotBatches.Rows[_mascotBatchBinding.Count - 1].Selected = true;
        }

        private async void BtnMascotGenerateExcel_Click(object sender, EventArgs e)
        {
            var row = GetSelectedMascotBatchRow();
            if (row == null)
            {
                Log("[Mascot] Chon mot dong trong bang batch.");
                return;
            }

            var request = string.IsNullOrWhiteSpace(row.Request) ? txtMascotChannelTheme?.Text?.Trim() : row.Request.Trim();
            if (string.IsNullOrWhiteSpace(request))
            {
                Log("[Mascot] Nhap yeu cau / chu de.");
                return;
            }

            row.Request = request;
            SetMascotButtonsEnabled(false);
            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                var svc = new MascotGeminiScriptService();
                Log("[Mascot] Gemini: dang sinh kich ban JSON...");
                var scenes = await svc.GenerateScenesAsync(request, settings, CancellationToken.None).ConfigureAwait(true);
                var dir = MascotExcelHelper.GetScriptsRoot(settings.StorageRootPath);
                var excelPath = Path.Combine(dir, MascotExcelHelper.BuildExcelFileName(row.Profile, request));
                MascotExcelHelper.ExportScenes(excelPath, scenes);
                row.ExcelPath = excelPath;
                row.Status = "Da sinh Excel";
                _mascotBatchBinding.ResetBindings();
                Log("[Mascot] Da luu Excel: " + excelPath);
            }
            catch (Exception ex)
            {
                row.Status = "Loi Excel";
                Log("[Mascot] Sinh Excel that bai: " + ex.Message);
            }
            finally
            {
                SetMascotButtonsEnabled(true);
            }
        }

        private void BtnMascotOpenExcel_Click(object sender, EventArgs e)
        {
            var row = GetSelectedMascotBatchRow();
            if (row == null || string.IsNullOrWhiteSpace(row.ExcelPath) || !File.Exists(row.ExcelPath))
            {
                Log("[Mascot] Chon dong co file Excel hop le.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = row.ExcelPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log("[Mascot] Mo Excel: " + ex.Message);
            }
        }

        private async void BtnMascotRender_Click(object sender, EventArgs e)
        {
            var row = GetSelectedMascotBatchRow();
            if (row == null)
            {
                Log("[Mascot] Chon mot dong batch de render.");
                return;
            }

            if (string.IsNullOrWhiteSpace(row.ExcelPath) || !File.Exists(row.ExcelPath))
            {
                Log("[Mascot] Dong nay chua co file Excel.");
                return;
            }

            if (_mascotAvatarVaultPaths.Count < 3)
            {
                Log("[Mascot] Can 3-5 anh trong Avatar Identity Pack.");
                return;
            }

            _mascotBatchCts?.Cancel();
            _mascotBatchCts = new CancellationTokenSource();
            SetMascotButtonsEnabled(false);
            row.Status = "Dang render...";

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                _mascotBatchPipeline?.Dispose();
                _mascotBatchPipeline = new MascotBatchRenderPipeline();
                var primary = _mascotAvatarVaultPaths[0];
                if (txtMascotImagePath != null) txtMascotImagePath.Text = primary;

                var result = await _mascotBatchPipeline.RenderFromExcelAsync(
                    row.ExcelPath,
                    row.Profile,
                    _mascotAvatarVaultPaths,
                    primary,
                    settings,
                    Log,
                    _mascotBatchCts.Token).ConfigureAwait(true);

                if (result.Success)
                {
                    row.OutputVideoPath = result.FinalVideoPath;
                    row.Status = "Hoan thanh";
                    Log("[Mascot] Video xong: " + result.FinalVideoPath);
                }
                else
                {
                    row.Status = "Loi render";
                    Log("[Mascot] Render that bai.");
                }
            }
            catch (OperationCanceledException)
            {
                row.Status = "Da huy";
                Log("[Mascot] Render da huy.");
            }
            catch (Exception ex)
            {
                row.Status = "Loi render";
                Log("[Mascot] Render loi: " + ex.Message);
            }
            finally
            {
                SetMascotButtonsEnabled(true);
                _mascotBatchBinding.ResetBindings();
            }
        }

        private void BtnMascotSelectAvatarVault_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chon 3-5 anh Avatar Identity Pack";
                dlg.Filter = "Image|*.jpg;*.jpeg;*.png;*.webp;*.gif";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var files = dlg.FileNames?.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList() ?? new List<string>();
                if (files.Count < 3)
                {
                    Log("[Mascot] Can chon it nhat 3 anh.");
                    return;
                }

                var profile = GetSelectedMascotProfileName();
                var vaultDir = AvatarIdentityPackStore.GetProfileDirectory(profile);
                Directory.CreateDirectory(vaultDir);
                _mascotAvatarVaultPaths.Clear();
                foreach (var src in files)
                {
                    var dest = Path.Combine(vaultDir, Path.GetFileName(src));
                    File.Copy(src, dest, true);
                    _mascotAvatarVaultPaths.Add(dest);
                }

                var pack = AvatarIdentityPackStore.LoadOrCreate(profile);
                pack.IdentityImagePaths = _mascotAvatarVaultPaths.ToList();
                AvatarIdentityPackStore.Save(profile, pack);
                RefreshMascotAvatarThumbnails();
                Log("[Mascot] AvatarVault: " + _mascotAvatarVaultPaths.Count + " anh.");
            }
        }

        private void LoadMascotAvatarVaultForProfile()
        {
            var profile = GetSelectedMascotProfileName();
            var synced = AvatarIdentityPackStore.SyncIdentityImagesFromVault(profile);
            _mascotAvatarVaultPaths.Clear();
            _mascotAvatarVaultPaths.AddRange(synced);
            RefreshMascotAvatarThumbnails();
        }

        private void RefreshMascotAvatarThumbnails()
        {
            if (flpMascotAvatarThumbnails == null) return;
            foreach (Control c in flpMascotAvatarThumbnails.Controls) c.Dispose();
            flpMascotAvatarThumbnails.Controls.Clear();
            foreach (var path in _mascotAvatarVaultPaths)
            {
                try
                {
                    var img = Image.FromFile(path);
                    var pb = new PictureBox
                    {
                        Width = 96,
                        Height = 96,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = img,
                        BorderStyle = BorderStyle.FixedSingle,
                        Margin = new Padding(4)
                    };
                    flpMascotAvatarThumbnails.Controls.Add(pb);
                }
                catch { }
            }
        }

        private MascotBatchRow GetSelectedMascotBatchRow()
        {
            if (dgvMascotBatches?.CurrentRow?.DataBoundItem is MascotBatchRow row) return row;
            return null;
        }

        private void SetMascotButtonsEnabled(bool enabled)
        {
            if (btnAddMascotBatch != null) btnAddMascotBatch.Enabled = enabled;
            if (btnMascotGenerateExcel != null) btnMascotGenerateExcel.Enabled = enabled;
            if (btnMascotOpenExcel != null) btnMascotOpenExcel.Enabled = enabled;
            if (btnMascotRender != null) btnMascotRender.Enabled = enabled;
        }
    }
}
