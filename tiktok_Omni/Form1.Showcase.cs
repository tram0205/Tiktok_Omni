using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    /// <summary>
    /// Showcase sản phẩm (tab "Affiliate chuyên sâu" cải tạo lại): ảnh/storyboard → Gemini kịch bản theo cảnh →
    /// xuất Excel prompt Veo → người dùng tạo clip Veo I2V thủ công → Render ghép hook + voiceover + CTA.
    /// </summary>
    public partial class Form1
    {
        private ShowcaseSessionState _showcaseSession;
        private string _showcaseSessionRestoreLogKey = string.Empty;
        private Form _showcaseAudioDialogOwner;
        private Action<string> _showcaseAudioLogMirror;

        private string GetShowcaseThemeInput()
        {
            var video = GetActiveShowcaseVideo();
            if (video != null)
            {
                return (video.ShowcaseTheme ?? string.Empty).Trim();
            }

            var source = GetShowcaseSettingsSourceScene();
            return (source?.ShowcaseTheme ?? string.Empty).Trim();
        }

        async Task IAiVideoGenControlsHost.GenerateShowcaseSceneScriptAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video trên lưới (Ctrl+click để chọn nhiều dòng) rồi bấm «Tạo kịch bản»."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, "Cần cấu hình AI API Key trong tab Cài đặt.", "Tạo kịch bản",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            if (btnDeepGenerateScript != null)
            {
                btnDeepGenerateScript.Enabled = false;
            }

            try
            {
                for (var i = 0; i < videos.Count; i++)
                {
                    ThrowIfShowcaseTabCancelled();
                    var video = videos[i];
                    ActivateShowcaseVideo(video, refreshStoryboard: false);
                    var label = "«" + (video.ProductName ?? string.Empty).Trim() + "»";
                    if (videos.Count > 1)
                    {
                        LogShowcase("[Showcase] Gemini (" + (i + 1) + "/" + videos.Count + ") — " + label);
                    }

                    var ok = await GenerateShowcaseSceneScriptForVideoAsync(video, settings, profile).ConfigureAwait(true);
                    if (!ok && videos.Count > 1)
                    {
                        LogShowcase("[Showcase] Bỏ qua " + label + " — tiếp tục dòng kế tiếp.");
                    }
                }

                ActivateShowcaseVideo(videos[videos.Count - 1]);
                SyncBuffersToGrids();
            }
            finally
            {
                if (btnDeepGenerateScript != null && !btnDeepGenerateScript.IsDisposed)
                {
                    btnDeepGenerateScript.Enabled = true;
                }
            }
        }

        private async Task<bool> GenerateShowcaseSceneScriptForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var scenes = GetShowcaseVideoScenes(video);
            if (scenes.Count < ShowcaseWorkflowConstants.MinScenes)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video.ProductName ?? string.Empty).Trim() + "» cần ít nhất 1 ảnh trên storyboard (hiện có " + scenes.Count + ").",
                    "Tạo kịch bản",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            var productName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();
            var speechBefore = ShowcaseSubtitleDisplayHelper.ScriptSpeechSnapshot.Capture(video);

            try
            {
                var session = EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
                LogShowcase("[Showcase] Đang tải " + scenes.Count + " ảnh về phiên làm việc…");
                await ShowcaseSessionService.DownloadSceneImagesAsync(session, scenes, LogShowcase, ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);

                LogShowcase("[Showcase] Gemini đang xem ảnh và viết kịch bản ("
                            + (scenes.Count >= 5 ? "AIDA" : scenes.Count >= 4 ? "PAS" : scenes.Count + " cảnh") + ")…");
                var scriptService = new ShowcaseGeminiScriptService();
                var result = await scriptService.GenerateAsync(
                    scenes,
                    GetShowcaseThemeForVideo(video),
                    GetShowcaseProductTypeForVideo(video),
                    GetShowcaseClipModeForVideo(video),
                    GetShowcaseOutputAspectIdForVideo(video),
                    settings,
                    ShowcaseTabCancellationToken,
                    GetShowcaseVideoFormatForVideo(video),
                    video.ShowcaseOutputAspectCustomWidth,
                    video.ShowcaseOutputAspectCustomHeight).ConfigureAwait(true);

                ApplyShowcaseSceneOrder(result.OrderedScenes);
                session.Theme = result.Theme;
                session.HookText = result.HookText;
                session.CtaText = result.CtaText;
                video.ShowcaseTheme = result.Theme ?? video.ShowcaseTheme ?? string.Empty;
                video.ShowcaseHookText = result.HookText ?? string.Empty;
                video.ShowcaseCtaText = result.CtaText ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(result.CtaSfxFile))
                {
                    video.ShowcaseCtaSfxFile = result.CtaSfxFile;
                    video.ShowcaseCtaSfxEnabled = true;
                    video.ShowcaseCtaSfxGeminiHint = result.CtaSfxGeminiHint ?? string.Empty;
                    if (video.ShowcaseCtaSfxVolumePercent <= 0)
                    {
                        video.ShowcaseCtaSfxVolumePercent = ShowcaseSfxCatalog.DefaultVolumePercent;
                    }
                }

                if (!string.IsNullOrWhiteSpace(result.HookSfxFile))
                {
                    video.ShowcaseHookSfxFile = result.HookSfxFile;
                    video.ShowcaseHookSfxEnabled = true;
                    video.ShowcaseHookSfxGeminiHint = result.HookSfxGeminiHint ?? string.Empty;
                    if (video.ShowcaseHookSfxVolumePercent <= 0)
                    {
                        video.ShowcaseHookSfxVolumePercent = ShowcaseSfxCatalog.DefaultVolumePercent;
                    }
                }

                if (!string.IsNullOrWhiteSpace(result.BackgroundMusicFile))
                {
                    video.ShowcaseBackgroundMusicFile = result.BackgroundMusicFile.Trim();
                }

                ShowcaseGeminiProductionHintsHelper.ApplyToVideo(
                    video,
                    result.ProductionHints,
                    settings,
                    msg => LogShowcase("[Showcase] " + msg));

                ShowcaseVoiceoverHelper.ClearClipVoiceoverBaseline(video);
                foreach (var scene in result.OrderedScenes)
                {
                    if (scene != null)
                    {
                        scene.ShowcaseTheme = result.Theme ?? string.Empty;
                    }
                }

                video.ApplySettingsToScenes();
                ShowcaseScriptHubPolishHelper.PolishVideo(video, LogShowcase);
                ShowcaseSubtitleDisplayHelper.SyncDisplayTextFromSpeechEdits(video, speechBefore);
                if (session != null && !string.IsNullOrWhiteSpace(session.ClipsDir))
                {
                    var assignResult = ShowcaseSessionService.AssignPendingClipsToSceneSlots(
                        session.ClipsDir,
                        video.Scenes,
                        LogShowcase);
                    BindShowcaseSessionToVideo(session, video);
                    ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, video.Scenes, LogShowcase);
                    if (assignResult.AssignedCount > 0)
                    {
                        LogShowcase("[Showcase] Đã gán " + assignResult.AssignedCount
                                    + " clip quay tay vào cảnh (giữ tên gốc) — mở «Công cụ Video» để xem.");
                    }
                }

                ShowcaseSubtitleDisplayHelper.SyncDisplayTextFromSpeechEdits(video, speechBefore);
                video.RefreshDisplayFields();
                LogShowcase("[Showcase] Chủ đề: " + result.Theme);
                LogShowcase("[Showcase] Hook: " + result.HookText);
                LogShowcase("[Showcase] CTA: " + result.CtaText);
                if (!string.IsNullOrWhiteSpace(result.BackgroundMusicFile))
                {
                    LogShowcase("[Showcase] Nhạc nền (Gemini): " + result.BackgroundMusicFile
                                + (string.IsNullOrWhiteSpace(result.BackgroundMusicGeminiHint)
                                    ? string.Empty
                                    : " — " + result.BackgroundMusicGeminiHint));
                }

                var prodSummary = ShowcaseGeminiProductionHintsHelper.FormatSummaryLine(result.ProductionHints);
                if (!string.IsNullOrWhiteSpace(prodSummary))
                {
                    LogShowcase("[Showcase] Sản xuất (Gemini): " + prodSummary + " — mở tab Âm thanh / Phụ đề / Chuyển cảnh để duyệt.");
                }

                LogShowcase("[Showcase] Đã sinh kịch bản cho " + result.OrderedScenes.Count + " cảnh — kéo thả storyboard hoặc bấm cột «Kịch bản» để sửa.");
                LogShowcase("[Showcase] Thoại + Hook/CTA lúc này là bản nháp theo ảnh. Bỏ clip vào clips_render (scene_XX hoặc quay tay tên gốc) rồi «Tạo lời thoại» — Gemini xem hết clip và sắp timeline.");
                NotifyShowcaseDraftDirty();
                return true;
            }
            catch (OperationCanceledException)
            {
                LogShowcase("[Showcase] Đã dừng tạo kịch bản («" + productName + "»).");
                throw;
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Sinh kịch bản lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo kịch bản", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        async Task IAiVideoGenControlsHost.ExportShowcaseExcelAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video trên lưới rồi bấm «Tải excel prompt» (trong bảng Prompt)."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            for (var i = 0; i < videos.Count; i++)
            {
                ThrowIfShowcaseTabCancelled();
                var video = videos[i];
                ActivateShowcaseVideo(video, refreshStoryboard: false);
                if (videos.Count > 1)
                {
                    LogShowcase("[Showcase] Xuất Excel (" + (i + 1) + "/" + videos.Count + ") — «" + video.ProductName + "»");
                }

                await ExportShowcaseExcelForVideoAsync(video, settings, profile).ConfigureAwait(true);
            }

            ActivateShowcaseVideo(videos[videos.Count - 1]);
            SyncBuffersToGrids();
        }

        private async Task ExportShowcaseExcelForVideoAsync(ShowcaseVideoItem video, AppSettings settings, string profile)
        {
            var scenes = GetShowcaseVideoScenes(video);
            if (scenes.Count < ShowcaseWorkflowConstants.MinScenes)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video.ProductName ?? string.Empty).Trim() + "» cần ít nhất 1 ảnh trên storyboard trước khi xuất Excel.",
                    "Tải excel prompt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var hasScript = scenes.Any(s =>
                !string.IsNullOrWhiteSpace(s?.SceneVoiceover) ||
                ShowcaseClipToolHelper.SceneHasClipPrompt(s));
            if (!hasScript)
            {
                var proceed = MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video.ProductName ?? string.Empty).Trim() + "» chưa sinh kịch bản. Bấm «Tạo kịch bản» trước sẽ cho kết quả tốt hơn.\r\n\r\nVẫn xuất Excel trống để tự điền tay?",
                    "Tải excel prompt",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (proceed != DialogResult.Yes)
                {
                    return;
                }
            }

            var productName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();
            var session = EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            SyncShowcaseSessionFromVideo(session, video);

            try
            {
                await ShowcaseSessionService.DownloadSceneImagesAsync(session, scenes, LogShowcase, ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);

                var targetPath = Path.Combine(
                    session.BaseDir,
                    ShowcaseExcelHelper.BuildExcelFileName(profile, productName));

                var savedPath = ShowcaseExcelHelper.Export(
                    targetPath,
                    session.Theme,
                    session.HookText,
                    session.CtaText,
                    scenes);
                session.ExcelPath = savedPath;
                NotifyShowcaseDraftDirty();

                if (!string.Equals(savedPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    LogShowcase("[Showcase] File Excel cũ đang mở — đã lưu bản mới: " + savedPath);
                }
                else
                {
                    LogShowcase("[Showcase] Đã xuất Excel prompt Veo: " + savedPath);
                }

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = savedPath, UseShellExecute = true });
                }
                catch
                {
                    // Không mở được ứng dụng đọc Excel — bỏ qua, file vẫn đã lưu.
                }
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Xuất Excel lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tải excel prompt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        async Task IAiVideoGenControlsHost.OpenShowcaseClipsFolderAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn dòng video trên lưới, bấm cột «Cảnh» để mở thư mục clip Veo/Kling."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            for (var i = 0; i < videos.Count; i++)
            {
                await OpenShowcaseClipsFolderForVideoAsync(videos[i], settings, profile).ConfigureAwait(true);
            }

            if (videos.Count > 0)
            {
                LogShowcase("[Showcase] Bấm «Tạo lời thoại» khi đã có clip trong clips_render — Gemini xem hết clip (AI + quay tay) và sắp thứ tự timeline.");
                ActivateShowcaseVideo(videos[videos.Count - 1]);
            }

            await Task.CompletedTask;
        }

        async Task IAiVideoGenControlsHost.ImportShowcaseClipsForVideoAsync(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            await AddShowcaseClipsForVideoAsync(video).ConfigureAwait(true);
        }

        async Task IAiVideoGenControlsHost.AddShowcaseLibraryClipsToRenderQueueAsync(
            ShowcaseVideoItem video,
            IList<string> clipPaths)
        {
            if (video == null || clipPaths == null || clipPaths.Count == 0)
            {
                return;
            }

            await CopyShowcaseClipsToVeoClipsAsync(video, clipPaths, dialogOwner: ShowcaseActiveDialogOwner)
                .ConfigureAwait(true);
        }

        private async Task OpenShowcaseClipsFolderForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var productName = (video.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(productName))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Dòng này chưa có tên sản phẩm — đặt tên trước khi mở thư mục clip.",
                    "Thư mục clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (settings != null)
            {
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            }

            var session = EnsureShowcaseSession(profile, productName, settings?.StorageRootPath, video);
            if (session == null || string.IsNullOrWhiteSpace(session.ClipsDir))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Không tạo được thư mục clip — kiểm tra «Storage root» trong Cài đặt.",
                    "Thư mục clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(session.ClipsDir);
                Process.Start("explorer.exe", session.ClipsDir);
                LogShowcase("[Showcase] Đã mở thư mục clip («" + productName + "»): " + session.ClipsDir);
                var scenes = GetShowcaseVideoScenes(video);
                var missing = ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, scenes, LogShowcase);
                video.RefreshDisplayFields();
                SyncBuffersToGrids();
                NotifyShowcaseDraftDirty();
                LogShowcase(missing.Count == 0
                    ? "[Showcase] ✓ «" + productName + "» — đủ clip cho " + scenes.Count + " cảnh."
                    : "[Showcase] «" + productName + "» — còn thiếu clip cảnh " + string.Join(", ", missing));
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Không mở được thư mục clip («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Thư mục clip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask;
        }

        private async Task OpenShowcaseOutputVideoForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var productName = (video.ProductName ?? string.Empty).Trim();
            EnsureShowcaseSession(profile, productName, settings?.StorageRootPath, video);

            var outputPath = ShowcaseContentDisplayHelper.TryResolveFinishedVideoPath(video);
            if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
            {
                MessageBox.Show(
                    ShowcaseActiveDialogOwner,
                    "«" + productName + "» chưa có video thành phẩm.\r\n\r\nBấm «Render video» trước.",
                    "Xem video",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!string.Equals(video.OutputVideoPath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                video.OutputVideoPath = outputPath;
                video.RefreshDisplayFields();
                NotifyShowcaseDraftDirty();
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
                LogShowcase("[Showcase] Xem video thành phẩm → " + outputPath);
                LoadProductionVideoPreview(
                    outputPath,
                    ProductionPipeline.ResolveThumbnailPath(outputPath),
                    productName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ShowcaseActiveDialogOwner,
                    "Không mở được video:\r\n" + ex.Message,
                    "Xem video",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            await Task.CompletedTask;
        }

        private async Task OpenShowcaseOutputFolderForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var productName = (video.ProductName ?? string.Empty).Trim();
            var outputPath = (video.OutputVideoPath ?? string.Empty).Trim();

            string targetDir = null;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                try
                {
                    targetDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                }
                catch
                {
                    targetDir = Path.GetDirectoryName(outputPath);
                }
            }

            if (string.IsNullOrWhiteSpace(targetDir) && !string.IsNullOrWhiteSpace(video.ShowcaseSessionBaseDir))
            {
                targetDir = Path.Combine(video.ShowcaseSessionBaseDir.Trim(), "output");
            }

            if (string.IsNullOrWhiteSpace(targetDir) && !string.IsNullOrWhiteSpace(productName))
            {
                ProfileScopedPaths.SetConfiguredStorageRoot(settings?.StorageRootPath);
                var session = EnsureShowcaseSession(profile, productName, settings?.StorageRootPath, video);
                if (!string.IsNullOrWhiteSpace(session?.OutputDir))
                {
                    targetDir = session.OutputDir;
                }
            }

            if (string.IsNullOrWhiteSpace(targetDir))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Chưa có thư mục output — render xong hoặc thêm ảnh để tạo phiên Showcase.",
                    "Output",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Directory.CreateDirectory(targetDir);
                Process.Start("explorer.exe", targetDir);
                LogShowcase("[Showcase] Đã mở thư mục output («" + productName + "»): " + targetDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Output", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask;
        }

        async Task IAiVideoGenControlsHost.RefreshShowcaseClipStatusAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video trên lưới để kiểm tra clip."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            for (var i = 0; i < videos.Count; i++)
            {
                var video = videos[i];
                ActivateShowcaseVideo(video, refreshStoryboard: false);
                await RefreshShowcaseClipStatusForVideoAsync(video, settings, profile).ConfigureAwait(true);
            }

            ActivateShowcaseVideo(videos[videos.Count - 1]);
            SyncBuffersToGrids();
        }

        private async Task RefreshShowcaseClipStatusForVideoAsync(ShowcaseVideoItem video, AppSettings settings, string profile)
        {
            var productName = (video?.ProductName ?? string.Empty).Trim();
            EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);

            if (_showcaseSession != null && video.Scenes.Count == 0)
            {
                ShowcaseSessionService.EnsureScenesFromRenderFolder(
                    video,
                    _showcaseSession.ClipsDir,
                    profile,
                    LogShowcase);
            }

            var scenes = GetShowcaseVideoScenes(video);
            if (scenes.Count == 0)
            {
                var clipsDir = _showcaseSession?.ClipsDir ?? ShowcaseRenderClipsPaths.ResolveDirectory(
                    video?.ShowcaseSessionBaseDir,
                    createIfMissing: false,
                    migrateLegacy: true);
                var folderClips = string.IsNullOrWhiteSpace(clipsDir)
                    ? 0
                    : ShowcaseSessionService.CountClipFilesOnDisk(clipsDir);
                LogShowcase(folderClips > 0
                    ? "[Showcase] «" + productName + "» — có " + folderClips + " clip trong "
                      + ShowcaseRenderClipsPaths.FolderName + " nhưng chưa gán storyboard. Bấm dòng video để khôi phục."
                    : "[Showcase] «" + productName + "» chưa có cảnh hay clip.");
                video?.RefreshDisplayFields();
                return;
            }

            productName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();

            var missing = ShowcaseSessionService.RefreshClipStatus(_showcaseSession.ClipsDir, scenes, LogShowcase);
            video.RefreshDisplayFields();

            LogShowcase(missing.Count == 0
                ? "[Showcase] ✓ «" + productName + "» — đủ clip cho " + scenes.Count + " cảnh."
                : "[Showcase] «" + productName + "» — còn thiếu clip cảnh " + string.Join(", ", missing));

            await Task.CompletedTask;
        }

        async Task IAiVideoGenControlsHost.GenerateShowcaseVoiceoverAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video trên lưới (cột «Lời thoại»)."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, "Cần cấu hình AI API Key trong tab Cài đặt.", "Tạo lời thoại",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            for (var i = 0; i < videos.Count; i++)
            {
                ThrowIfShowcaseTabCancelled();
                var video = videos[i];
                ActivateShowcaseVideo(video, refreshStoryboard: false);
                if (videos.Count > 1)
                {
                    LogShowcase("[Showcase] Tạo thoại (" + (i + 1) + "/" + videos.Count + ") — «" + video.ProductName + "»");
                }

                await GenerateShowcaseVoiceoverForVideoAsync(video, settings, profile).ConfigureAwait(true);
            }

            ActivateShowcaseVideo(videos[videos.Count - 1]);
            SyncBuffersToGrids();
            RefreshAiVideoGenModeReadinessLabels();
        }

        async Task IAiVideoGenControlsHost.GenerateShowcaseVoiceoverForVideoAsync(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, "Cần cấu hình AI API Key trong tab Cài đặt.", "Tạo lời thoại",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            ActivateShowcaseVideo(video, refreshStoryboard: false);
            await GenerateShowcaseVoiceoverForVideoAsync(video, settings, profile).ConfigureAwait(true);
            SyncBuffersToGrids();
            RefreshAiVideoGenModeReadinessLabels();
        }

        void IAiVideoGenControlsHost.FlushShowcaseDraftToDisk() => FlushShowcaseDraftToDisk();

        async Task IAiVideoGenControlsHost.BuildShowcaseNarrationAsync()
        {
            await RunShowcaseNarrationActionAsync(btnShowcasePreviewNarration).ConfigureAwait(true);
        }

        async Task IAiVideoGenControlsHost.ListenShowcaseNarrationAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video trên lưới rồi bấm «🔊 Nghe audio»."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            if (btnShowcaseListenNarration != null)
            {
                btnShowcaseListenNarration.Enabled = false;
            }

            try
            {
                for (var i = 0; i < videos.Count; i++)
                {
                    ThrowIfShowcaseTabCancelled();
                    var video = videos[i];
                    ActivateShowcaseVideo(video, refreshStoryboard: false);
                    await ListenShowcaseNarrationForVideoAsync(video, settings, profile).ConfigureAwait(true);
                }

                ActivateShowcaseVideo(videos[videos.Count - 1]);
            }
            finally
            {
                UpdateShowcaseNarrationButtonState();
            }
        }

        private async Task ListenShowcaseNarrationForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var scenes = GetShowcaseVideoScenes(video);
            var productName = (video.ProductName ?? scenes.FirstOrDefault()?.ProductName ?? string.Empty).Trim();
            EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            var sessionBase = _showcaseSession?.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            if (string.IsNullOrWhiteSpace(sessionBase) ||
                !ShowcaseNarrationCacheHelper.HasNarrationFile(sessionBase))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + productName + "» chưa có file narration.mp3.\r\n\r\nBấm «🎙 Tạo audio» trước.",
                    "Nghe audio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var path = Path.Combine(ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase),
                ShowcaseNarrationCacheHelper.NarrationFileName);
            try
            {
                PlayShowcaseNarrationFile(path);
                LogShowcase("[Showcase] Nghe audio → " + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Nghe audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask.ConfigureAwait(true);
        }

        private async Task RunShowcaseNarrationActionAsync(
            Button actionButton,
            IReadOnlyList<ShowcaseVideoItem> videosOverride = null)
        {
            IReadOnlyList<ShowcaseVideoItem> videos;
            if (videosOverride != null && videosOverride.Count > 0)
            {
                videos = videosOverride;
            }
            else if (!TryGetShowcaseSelectedVideosOrdered(out var selected,
                         "Chọn ít nhất một dòng video trên lưới rồi bấm «Tạo audio»."))
            {
                return;
            }
            else
            {
                videos = selected;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();

            if (!TtsAvailabilityHelper.IsAnyShowcaseTtsConfigured(settings))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Cần Windows + mạng (Edge TTS miễn phí) hoặc cấu hình ElevenLabs trong tab Cài đặt.",
                    "Tạo audio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await FfmpegToolkitService.EnsureAvailableAsync(settings, LogShowcase, ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TtsEngineKind? batchAskEngine = null;

            if (actionButton != null)
            {
                actionButton.Enabled = false;
            }

            try
            {
                for (var i = 0; i < videos.Count; i++)
                {
                    ThrowIfShowcaseTabCancelled();
                    var video = videos[i];
                    ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
                    var ttsOptions = ShowcaseTtsRenderOptions.FromVideo(video, settings);
                    video.ShowcaseVoicePresetId = ttsOptions.VoicePresetId;
                    video.ShowcaseVoiceLanguageId = ttsOptions.VoiceLanguageId;
                    video.ShowcaseVoiceAgeId = ttsOptions.VoiceAgeId;
                    if (ShowcaseTtsHelper.UsesAskOnCreateEngine(video))
                    {
                        if (batchAskEngine == null)
                        {
                            using (var picker = new ShowcaseTtsEngineChoiceForm(
                                       settings,
                                       TtsEngineChoiceHelper.ParseStoredChoice(settings.ShowcaseLastTtsEngine)))
                            {
                                if (picker.ShowDialog(this) != DialogResult.OK)
                                {
                                    return;
                                }

                                batchAskEngine = picker.SelectedEngine;
                            }

                            settings.ShowcaseLastTtsEngine = batchAskEngine.Value.ToString();
                            try
                            {
                                await _configManager.SaveAsync(settings).ConfigureAwait(true);
                            }
                            catch
                            {
                                // non-critical
                            }
                        }

                        ttsOptions.HookEngine = batchAskEngine.Value;
                        ttsOptions.BodyEngine = batchAskEngine.Value;
                    }

                    try
                    {
                        TtsAvailabilityHelper.ValidateEngine(settings, ttsOptions.HookEngine);
                        TtsAvailabilityHelper.ValidateEngine(settings, ttsOptions.BodyEngine);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ShowcaseActiveDialogOwner,
                            "«" + (video.ProductName ?? string.Empty).Trim() + "»: " + ex.Message,
                            "Tạo audio",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        continue;
                    }

                    ActivateShowcaseVideo(video, refreshStoryboard: false);
                    if (videos.Count > 1)
                    {
                        LogShowcase("[Showcase] Tạo audio (" + (i + 1) + "/" + videos.Count + ") — «" + video.ProductName + "»");
                    }

                    await BuildShowcaseNarrationForVideoAsync(video, settings, profile, ttsOptions).ConfigureAwait(true);
                }

                ActivateShowcaseVideo(videos[videos.Count - 1]);
                UpdateShowcaseNarrationButtonState();
            }
            finally
            {
                if (actionButton != null && !actionButton.IsDisposed)
                {
                    actionButton.Enabled = true;
                }
            }
        }

        private async Task BuildShowcaseHookNarrationPreviewForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            ShowcaseTtsRenderOptions ttsOptions)
        {
            if (!TryPrepareShowcaseNarrationSession(video, settings, profile, out var scenes, out var sessionBase, out var productName))
            {
                return;
            }

            var hookText = video.ShowcaseHookText ?? _showcaseSession?.HookText ?? string.Empty;
            try
            {
                LogShowcase("[Showcase] Tạo audio hook preview cho «" + productName + "»…");
                await _videoProcessingService.GenerateShowcaseHookPreviewAsync(
                    scenes,
                    hookText,
                    settings,
                    sessionBase,
                    LogShowcase,
                    ShowcaseTabCancellationToken,
                    ttsOptions).ConfigureAwait(true);
                LogShowcase("[Showcase] hook_preview.mp3 → "
                            + ShowcaseNarrationCacheHelper.GetHookPreviewPath(sessionBase));
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Tạo audio hook lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo audio hook", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task BuildShowcaseBodyNarrationPreviewForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            ShowcaseTtsRenderOptions ttsOptions)
        {
            if (!TryPrepareShowcaseNarrationSession(
                    video,
                    settings,
                    profile,
                    out var scenes,
                    out var sessionBase,
                    out var productName,
                    requireClipAlignedVoiceover: true,
                    requireAllClips: true))
            {
                return;
            }

            var ctaText = video.ShowcaseCtaText ?? _showcaseSession?.CtaText ?? string.Empty;
            try
            {
                LogShowcase("[Showcase] Tạo audio thân preview cho «" + productName + "»…");
                await _videoProcessingService.GenerateShowcaseBodyPreviewAsync(
                    scenes,
                    ctaText,
                    settings,
                    sessionBase,
                    LogShowcase,
                    ShowcaseTabCancellationToken,
                    ttsOptions).ConfigureAwait(true);
                LogShowcase("[Showcase] body_preview.mp3 → "
                            + ShowcaseNarrationCacheHelper.GetBodyPreviewPath(sessionBase));
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Tạo audio thân lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo audio thân", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool TryPrepareShowcaseNarrationSession(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            out List<AiVideoGenInputItem> scenes,
            out string sessionBase,
            out string productName,
            bool requireClipAlignedVoiceover = false,
            bool requireAllClips = false)
        {
            scenes = GetShowcaseVideoScenes(video);
            sessionBase = null;
            productName = (video?.ProductName ?? string.Empty).Trim();

            if (scenes.Count < ShowcaseWorkflowConstants.MinScenes)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + productName + "» cần ít nhất 1 cảnh.",
                    "Tạo audio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (requireClipAlignedVoiceover
                && !ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + productName + "» chưa có lời thoại đủ cảnh.\r\n\r\nBấm «Tạo lời thoại» trước (hoặc sửa kịch bản trong hub «Kịch bản · Prompt»).",
                    "Tạo audio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (requireClipAlignedVoiceover
                && ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes))
            {
                var durationSignature = BuildShowcaseClipDurationSignature(settings, scenes);
                TryResyncShowcaseClipFingerprintForInPlaceRefresh(video, scenes, durationSignature);
            }

            EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            if (_showcaseSession == null)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + productName + "» chưa có phiên Showcase.",
                    "Tạo audio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (requireAllClips)
            {
                var missingClips = ShowcaseSessionService.RefreshClipStatus(_showcaseSession.ClipsDir, video.Scenes, LogShowcase);
                video.RefreshDisplayFields();
                if (missingClips.Count > 0)
                {
                    MessageBox.Show(ShowcaseActiveDialogOwner,
                        "«" + productName + "» còn thiếu clip cảnh: " + string.Join(", ", missingClips) + ".",
                        "Thiếu clip",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }

            sessionBase = _showcaseSession.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, "Không xác định được thư mục phiên audio.", "Tạo audio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private async Task ListenShowcaseHookPreviewForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var scenes = GetShowcaseVideoScenes(video);
            var sessionBase = _showcaseSession?.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            if (string.IsNullOrWhiteSpace(sessionBase)
                || !ShowcaseNarrationCacheHelper.HasHookPreviewFile(sessionBase))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video?.ProductName ?? string.Empty).Trim() + "» chưa có hook_preview.mp3.\r\n\r\nBấm «Tạo audio hook» trước.",
                    "Nghe hook",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                var previewPath = ShowcaseNarrationCacheHelper.GetHookPreviewPath(sessionBase);
                previewPath = await PrepareShowcaseSpeedPreviewPathAsync(
                        settings,
                        previewPath,
                        sessionBase,
                        video?.ShowcaseHookNarrationSpeedPercent ?? 0,
                        "hook_speed_preview.mp3",
                        ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);
                PlayShowcaseNarrationFile(previewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Nghe hook", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask.ConfigureAwait(true);
        }

        private async Task ListenShowcaseBodyPreviewForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var scenes = GetShowcaseVideoScenes(video);
            var sessionBase = _showcaseSession?.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            if (string.IsNullOrWhiteSpace(sessionBase)
                || !ShowcaseNarrationCacheHelper.HasBodyPreviewFile(sessionBase))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video?.ProductName ?? string.Empty).Trim() + "» chưa có body_preview.mp3.\r\n\r\nBấm «Tạo audio thân» trước.",
                    "Nghe thân",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                var previewPath = ShowcaseNarrationCacheHelper.GetBodyPreviewPath(sessionBase);
                previewPath = await PrepareShowcaseSpeedPreviewPathAsync(
                        settings,
                        previewPath,
                        sessionBase,
                        video?.ShowcaseBodyNarrationSpeedPercent ?? 0,
                        "body_speed_preview.mp3",
                        ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);
                PlayShowcaseNarrationFile(previewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Nghe thân", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask.ConfigureAwait(true);
        }

        internal Task RenderShowcaseFullMixedAudioForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            return RunShowcaseTabScopedWorkAsync(async () =>
            {
                if (!await TryBuildShowcaseFullMixedAudioPreviewForVideoAsync(
                        video,
                        settings,
                        profile,
                        ShowcaseWorkflowConstants.RenderFullMixedAudioDialogTitle).ConfigureAwait(true))
                {
                    return;
                }

                LogShowcase("[Showcase] Render Audio xong — bấm «Nghe thành phẩm» để nghe.");
            });
        }

        internal async Task ListenShowcaseFullMixedAudioForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var dialogTitle = ShowcaseWorkflowConstants.ListenFullMixedAudioDialogTitle;
            var scenes = GetShowcaseVideoScenes(video);
            var productName = (video?.ProductName ?? scenes.FirstOrDefault()?.ProductName ?? string.Empty).Trim();
            EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            var sessionBase = _showcaseSession?.BaseDir
                              ?? ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);

            var previewPath = string.IsNullOrWhiteSpace(sessionBase)
                ? string.Empty
                : ShowcaseNarrationCacheHelper.GetFullMixPreviewPath(sessionBase);

            if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Chưa có file thành phẩm audio.\r\n\r\n"
                    + "Bấm «Render Audio» để ghép hook, thân, nhạc nền và hiệu ứng trước.",
                    dialogTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                PlayShowcaseNarrationFile(previewPath);
                LogShowcase("[Showcase] Nghe thành phẩm → " + previewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask.ConfigureAwait(true);
        }

        /// <summary>Giữ tên cũ — chỉ nghe (không render lại).</summary>
        internal Task ListenShowcaseFullNarrationForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile) =>
            ListenShowcaseFullMixedAudioForVideoAsync(video, settings, profile);

        private async Task<bool> TryBuildShowcaseFullMixedAudioPreviewForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            string dialogTitle)
        {
            var scenes = GetShowcaseVideoScenes(video);
            var productName = (video?.ProductName ?? scenes.FirstOrDefault()?.ProductName ?? string.Empty).Trim();
            EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            var sessionBase = _showcaseSession?.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            if (string.IsNullOrWhiteSpace(sessionBase)
                || !ShowcaseNarrationCacheHelper.CanListenFullNarration(sessionBase, scenes))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + productName + "» chưa có audio đủ để render.\r\n\r\n"
                    + "Tạo audio hook (và thân nếu có thoại thân), hoặc bấm «🎙 Tạo audio» trên toolbar.",
                    dialogTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            string narrationPath;
            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
            var ttsOptions = ShowcaseTtsRenderOptions.FromVideo(video, settings);
            var hookText = video.ShowcaseHookText ?? _showcaseSession?.HookText ?? string.Empty;
            var ctaText = video.ShowcaseCtaText ?? _showcaseSession?.CtaText ?? string.Empty;
            var audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
            var fingerprint = ShowcaseNarrationCacheHelper.ComputeFingerprint(scenes, settings, ttsOptions);
            if (ShowcaseNarrationCacheHelper.TryReuseCachedNarration(audioDir, fingerprint, out narrationPath))
            {
                LogShowcase("[Showcase] Dùng lại narration.mp3 (lời thoại + giọng không đổi).");
            }
            else
            {
                if (!TryPrepareShowcaseNarrationSession(video, settings, profile, out scenes, out sessionBase, out productName))
                {
                    return false;
                }

                audioDir = ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);
                fingerprint = ShowcaseNarrationCacheHelper.ComputeFingerprint(scenes, settings, ttsOptions);

                try
                {
                    LogShowcase("[Showcase] Lời thoại đổi — ghép lại narration.mp3 trước khi render thành phẩm…");
                    var build = await _videoProcessingService.EnsureShowcaseNarrationAsync(
                        scenes,
                        hookText,
                        ctaText,
                        settings,
                        sessionBase,
                        LogShowcase,
                        ShowcaseTabCancellationToken,
                        ttsOptions).ConfigureAwait(true);

                    if (string.IsNullOrWhiteSpace(build?.NarrationFilePath) || !File.Exists(build.NarrationFilePath))
                    {
                        throw new InvalidOperationException("Không tạo được file narration.mp3.");
                    }

                    narrationPath = build.NarrationFilePath;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(narrationPath) || !File.Exists(narrationPath))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, "Không tìm thấy file thoại.", dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
            var renderSettings = ShowcasePerVideoRenderSettings.FromVideo(video, settings);

            try
            {
                LogShowcase("[Showcase] Render Audio — ghép hook, thân, nhạc nền, hiệu ứng…");
                var previewPath = await _videoProcessingService.BuildShowcaseFullAudioPreviewAsync(
                    scenes,
                    narrationPath,
                    settings,
                    renderSettings,
                    sessionBase,
                    LogShowcase,
                    ShowcaseTabCancellationToken).ConfigureAwait(true);

                LogShowcase("[Showcase] Render Audio → " + previewPath);
                return !string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private async Task BuildShowcaseNarrationForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            ShowcaseTtsRenderOptions ttsOptions)
        {
            if (!TryPrepareShowcaseNarrationSession(
                    video,
                    settings,
                    profile,
                    out var scenes,
                    out var sessionBase,
                    out var productName,
                    requireClipAlignedVoiceover: true,
                    requireAllClips: true))
            {
                return;
            }

            var hookText = video.ShowcaseHookText ?? _showcaseSession.HookText ?? string.Empty;
            var ctaText = video.ShowcaseCtaText ?? _showcaseSession.CtaText ?? string.Empty;
            var forceRegenerate = ShowcaseNarrationCacheHelper.HasNarrationFile(sessionBase);
            var dialogTitle = forceRegenerate ? "Tạo lại audio" : "Tạo audio";

            try
            {
                if (forceRegenerate)
                {
                    ShowcaseNarrationCacheHelper.ClearAllCachedAudio(sessionBase);
                    LogShowcase("[Showcase] Đã xóa cache narration — gọi TTS lại cho «" + productName + "»…");
                }
                else
                {
                    LogShowcase("[Showcase] Đang tạo narration.mp3 cho «" + productName + "» — " +
                                ttsOptions.Preset.Label + " (" + ttsOptions.Engine + ")…");
                }

                var build = await _videoProcessingService.EnsureShowcaseNarrationAsync(
                    scenes,
                    hookText,
                    ctaText,
                    settings,
                    sessionBase,
                    LogShowcase,
                    ShowcaseTabCancellationToken,
                    ttsOptions).ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(build?.NarrationFilePath) || !File.Exists(build.NarrationFilePath))
                {
                    throw new InvalidOperationException("Không tạo được file narration.mp3.");
                }

                var cacheNote = forceRegenerate || !build.ReusedFromCache
                    ? " (TTS mới)"
                    : " (dùng cache)";
                var durNote = build.DurationSeconds > 0
                    ? build.DurationSeconds.ToString("0.#") + "s"
                    : "?";
                LogShowcase("[Showcase] narration.mp3" + cacheNote + " — ~" + durNote + " → " + build.NarrationFilePath);
                LogShowcase("[Showcase] Bấm «🔊 Nghe audio» để nghe thử trước khi render.");
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] " + dialogTitle + " lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PlayShowcaseNarrationFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                LogShowcase("[Showcase] Nghe audio: file không tồn tại.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không mở được trình phát audio: " + ex.Message, ex);
            }
        }

        private async Task<string> PrepareShowcaseSpeedPreviewPathAsync(
            AppSettings settings,
            string inputPath,
            string sessionBase,
            int speedPercent,
            string outputFileName,
            CancellationToken cancellationToken)
        {
            var input = (inputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(input) || !File.Exists(input))
            {
                return input;
            }

            var tempo = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(speedPercent) / 100d;
            if (Math.Abs(tempo - 1d) <= 0.03d)
            {
                return input;
            }

            var ffmpeg = await FfmpegToolkitService.EnsureAvailableAsync(settings, LogShowcase, cancellationToken)
                .ConfigureAwait(false);
            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            var workDir = Path.Combine(
                ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase),
                "speed_preview_work");
            return await ShowcaseNarrationAvSyncHelper.PrepareSpeedAdjustedMp3Async(
                    ffmpeg,
                    ffprobe,
                    input,
                    workDir,
                    speedPercent,
                    outputFileName,
                    LogShowcase,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private Task RunShowcaseHookNarrationPreviewForVideoAsync(ShowcaseVideoItem video)
        {
            return RunShowcaseTabScopedWorkAsync(async () =>
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
                var profile = GetRunningProfileName();
                ActivateShowcaseVideo(video, refreshStoryboard: false);
                ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
                var ttsOptions = ShowcaseTtsRenderOptions.FromVideo(video, settings);
                await BuildShowcaseHookNarrationPreviewForVideoAsync(video, settings, profile, ttsOptions).ConfigureAwait(true);
                UpdateShowcaseNarrationButtonState();
            });
        }

        private Task RunShowcaseBodyNarrationPreviewForVideoAsync(ShowcaseVideoItem video)
        {
            return RunShowcaseTabScopedWorkAsync(async () =>
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
                var profile = GetRunningProfileName();
                ActivateShowcaseVideo(video, refreshStoryboard: false);
                ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
                var ttsOptions = ShowcaseTtsRenderOptions.FromVideo(video, settings);
                await BuildShowcaseBodyNarrationPreviewForVideoAsync(video, settings, profile, ttsOptions).ConfigureAwait(true);
                UpdateShowcaseNarrationButtonState();
            });
        }

        private async Task GenerateShowcaseVoiceoverForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var productName = (video?.ProductName ?? string.Empty).Trim();
            EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video, allowRecentSessionFallback: false);
            BindShowcaseSessionToVideo(_showcaseSession, video);
            if (_showcaseSession == null)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + productName + "» chưa có phiên Showcase — hãy thêm ảnh hoặc clip vào "
                    + ShowcaseRenderClipsPaths.FolderName + " trước.",
                    "Tạo lời thoại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var clipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(
                _showcaseSession.BaseDir,
                createIfMissing: true,
                migrateLegacy: true);
            _showcaseSession.ClipsDir = clipsDir;
            video.ShowcaseClipsDir = clipsDir;
            video.ShowcaseSessionBaseDir = _showcaseSession.BaseDir;

            var manifest = ShowcaseRenderClipsTimelineHelper.BuildManifest(clipsDir);
            if (manifest.Count == 0)
            {
                ShowcaseSessionService.EnsureScenesFromRenderFolder(video, clipsDir, profile, LogShowcase);
                manifest = ShowcaseRenderClipsTimelineHelper.BuildManifest(clipsDir);
            }

            if (manifest.Count == 0)
            {
                LogShowcase("[Showcase] «" + productName + "» — chưa có clip trong "
                            + ShowcaseRenderClipsPaths.FolderName + ", không gọi Gemini «Tạo lời thoại».");
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    ShowcaseClipStatusHelper.BuildNoClipsForVoiceoverMessage(productName, GetShowcaseClipModeForVideo(video)),
                    "Chưa có clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var themeForGemini = GetShowcaseThemeForVideo(video);
                var productType = GetShowcaseProductTypeForVideo(video);
                var voiceoverService = new ShowcaseGeminiVoiceoverService();
                var result = await voiceoverService.GenerateFromRenderFolderAsync(
                    video,
                    clipsDir,
                    themeForGemini,
                    productType,
                    profile,
                    settings,
                    LogShowcase,
                    ShowcaseTabCancellationToken,
                    GetShowcaseVideoFormatForVideo(video)).ConfigureAwait(true);

                var scenes = GetShowcaseVideoScenes(video);
                _showcaseSession.Theme = result.Theme;
                _showcaseSession.HookText = result.HookText;
                _showcaseSession.CtaText = result.CtaText;
                video.ShowcaseTheme = result.Theme ?? video.ShowcaseTheme ?? string.Empty;
                video.ShowcaseHookText = result.HookText ?? string.Empty;
                video.ShowcaseCtaText = result.CtaText ?? string.Empty;
                foreach (var scene in scenes)
                {
                    if (scene != null)
                    {
                        scene.ShowcaseTheme = video.ShowcaseTheme;
                    }
                }

                video.ApplySettingsToScenes();
                ShowcaseVoiceoverHelper.PersistHookCtaIntoSceneVoiceovers(video, scenes);
                ShowcaseVoiceoverHelper.SyncSilentFlagsFromVoiceover(scenes);
                ShowcaseSubtitleDisplayHelper.ClearDisplayTextOverrides(video);
                video.RefreshDisplayFields();
                var ffprobe = FfmpegToolkitService.TryResolve(settings, out var voiceTk, out _)
                    ? voiceTk?.FfprobeExe
                    : null;
                if (string.IsNullOrWhiteSpace(ffprobe))
                {
                    ffprobe = FfmpegToolkitService.GetBundledFfprobePath();
                }

                await ShowcaseVoiceoverHelper.StampClipVoiceoverBaselineAsync(
                        video,
                        scenes,
                        ffprobe,
                        ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);
                ShowcaseNarrationCacheHelper.ClearAllCachedAudio(_showcaseSession.BaseDir);
                NotifyShowcaseDraftDirty();
                FlushShowcaseDraftToDisk();
                RefreshAffiliateDeepStoryboard(skipSourceImagePrune: true);

                LogShowcase("[Showcase] Phụ đề burn-in đã lấy lại theo lời thoại mới — mở tab «Phụ đề» để rút gọn nếu cần.");
                LogShowcase("[Showcase] Chủ đề: " + result.Theme);
                LogShowcase("[Showcase] Hook: " + result.HookText);
                LogShowcase("[Showcase] CTA: " + result.CtaText);
                LogShowcase("[Showcase] Đã sinh thoại timeline — " +
                            ShowcaseVoiceoverHelper.CountVoicedScenes(scenes) + " cảnh có lời, " +
                            ShowcaseVoiceoverHelper.CountSilentScenes(scenes) + " cảnh im — sửa trong hub «Kịch bản · Prompt» rồi «🎙 Tạo audio».");
                if (string.Equals(
                        ShowcaseClipModePresets.ResolveIdForGemini(GetShowcaseClipModeForVideo(video)),
                        ShowcaseClipModePresets.ZoomOnlyId,
                        StringComparison.Ordinal))
                {
                    LogShowcase("[Showcase] Chỉ Zoom: nên «Tạo clip Zoom» trước «Tạo lời thoại»; nếu tạo lại clip Zoom sau đó, app tự giữ thoại.");
                }
                else
                {
                    LogShowcase("[Showcase] Đổi clip cùng thời lượng → vẫn render; đổi thời lượng → «Tạo lời thoại» lại rồi «Tạo audio».");
                }
                UpdateShowcaseNarrationButtonState();
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Tạo thoại lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo lời thoại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        async Task IAiVideoGenControlsHost.AddShowcaseImagesFromFilesAsync()
        {
            if (TryGetShowcaseSelectedVideosOrdered(out var selectedVideos, null) && selectedVideos.Count > 0)
            {
                await AddShowcaseImagesForVideoAsync(selectedVideos[0]).ConfigureAwait(true);
                return;
            }

            var created = EnsureActiveShowcaseVideoForImages();
            if (created != null)
            {
                await AddShowcaseImagesForVideoAsync(created).ConfigureAwait(true);
            }
        }

        private async Task AddShowcaseImagesForVideoAsync(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video);

            var existingCount = video.Scenes.Count;

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh sản phẩm (có thể chọn nhiều file)",
                Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Tất cả file|*.*",
                Multiselect = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.FileNames.Length == 0)
                {
                    return;
                }

                var files = dlg.FileNames.ToList();
                var productName = (video.ProductName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(productName) || productName.StartsWith("Video ", StringComparison.OrdinalIgnoreCase))
                {
                    var prompted = PromptForShowcaseProductName();
                    if (string.IsNullOrWhiteSpace(prompted))
                    {
                        LogShowcase("[Showcase] Đã hủy — cần đặt tên sản phẩm trước khi thêm ảnh.");
                        return;
                    }

                    productName = prompted;
                    video.ProductName = productName;
                }

                var profile = GetRunningProfileName();
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
                var session = EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
                if (session == null || string.IsNullOrWhiteSpace(session.SourceImagesDir))
                {
                    MessageBox.Show(ShowcaseActiveDialogOwner,
                        "Không tạo được thư mục phiên Showcase — kiểm tra «Storage root» trong Cài đặt.",
                        "Thêm ảnh",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var settingsTemplate = video.Scenes.FirstOrDefault();
                var nextIndex = existingCount + 1;
                var addedCount = 0;
                var skippedCount = 0;
                var picksThisBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    string pickFullPath;
                    try
                    {
                        pickFullPath = Path.GetFullPath(file);
                    }
                    catch
                    {
                        skippedCount++;
                        continue;
                    }

                    var isDuplicateInBatch = picksThisBatch.Contains(pickFullPath);
                    var isDuplicateOnStoryboard = ShowcaseSessionService.TryFindSceneIndexWithSameShowcasePickSource(
                        video.Scenes,
                        file,
                        out var duplicateSceneIndex);
                    if (isDuplicateInBatch || isDuplicateOnStoryboard)
                    {
                        if (isDuplicateInBatch && !isDuplicateOnStoryboard)
                        {
                            duplicateSceneIndex = 0;
                        }

                        var choice = PromptShowcaseDuplicateSameOriginImage(
                            Path.GetFileName(file),
                            Path.GetDirectoryName(pickFullPath),
                            duplicateSceneIndex);
                        if (choice == ShowcaseDuplicateImageChoice.Skip)
                        {
                            skippedCount++;
                            continue;
                        }

                        if (choice == ShowcaseDuplicateImageChoice.Cancel)
                        {
                            break;
                        }
                    }

                    while (true)
                    {
                        if (ShowcaseSessionService.TryGetExistingSourceImageAtIndex(session, nextIndex, out var existingFile))
                        {
                            var choice = PromptShowcaseDuplicateSlotFile(
                                Path.GetFileName(existingFile),
                                Path.GetFileName(file));
                            if (choice == ShowcaseDuplicateImageChoice.Skip)
                            {
                                skippedCount++;
                                break;
                            }

                            if (choice == ShowcaseDuplicateImageChoice.Cancel)
                            {
                                i = files.Count;
                                break;
                            }

                            var localPath = ShowcaseSessionService.CopyLocalSceneImage(session, nextIndex, file, overwriteExisting: true);
                            if (string.IsNullOrWhiteSpace(localPath))
                            {
                                skippedCount++;
                                break;
                            }

                            AppendShowcaseSceneFromImageFile(video, localPath, file, productName, profile, settingsTemplate);
                            picksThisBatch.Add(pickFullPath);
                            nextIndex++;
                            addedCount++;
                            break;
                        }

                        var copiedPath = ShowcaseSessionService.CopyLocalSceneImage(session, nextIndex, file, overwriteExisting: true);
                        if (string.IsNullOrWhiteSpace(copiedPath))
                        {
                            skippedCount++;
                            break;
                        }

                        AppendShowcaseSceneFromImageFile(video, copiedPath, file, productName, profile, settingsTemplate);
                        picksThisBatch.Add(pickFullPath);
                        nextIndex++;
                        addedCount++;
                        break;
                    }
                }

                video.ApplySettingsToScenes();
                video.RefreshDisplayFields();

                ShowcaseSessionService.ImportScenesIntoSessionSourceImages(session, video.Scenes);

                RefreshAffiliateDeepStoryboard();
                SyncBuffersToGrids();
                RefreshAiVideoGenModeReadinessLabels();
                LogShowcase("[Showcase] Đã thêm " + addedCount + " ảnh cho «" + productName + "» → "
                            + session.SourceImagesDir
                            + " (giữ tên file gốc trong thư mục)."
                            + (skippedCount > 0 ? " Bỏ qua " + skippedCount + " trùng." : string.Empty)
                            + " Tổng " + video.Scenes.Count + " cảnh.");
                NotifyShowcaseDraftDirty();
            }

            await Task.CompletedTask;
        }

        private async Task AddShowcaseClipsForVideoAsync(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);

            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn clip vào bảng chờ render (giữ tên gốc — có thể chọn nhiều file)",
                Filter = "Video (*.mp4;*.mov;*.webm;*.mkv)|*.mp4;*.mov;*.webm;*.mkv|Tất cả file|*.*",
                Multiselect = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.FileNames.Length == 0)
                {
                    return;
                }

                await CopyShowcaseClipsToVeoClipsAsync(video, dlg.FileNames, dialogOwner: this)
                    .ConfigureAwait(true);
            }
        }

        private async Task CopyShowcaseClipsToVeoClipsAsync(
            ShowcaseVideoItem video,
            IList<string> filePaths,
            IWin32Window dialogOwner)
        {
            if (video == null || filePaths == null || filePaths.Count == 0)
            {
                return;
            }

            var productName = (video.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(productName) || productName.StartsWith("Video ", StringComparison.OrdinalIgnoreCase))
            {
                var prompted = PromptForShowcaseProductName();
                if (string.IsNullOrWhiteSpace(prompted))
                {
                    LogShowcase("[Showcase] Đã hủy — cần đặt tên sản phẩm trước khi thêm clip.");
                    return;
                }

                productName = prompted;
                video.ProductName = productName;
            }

            var profile = GetRunningProfileName();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var session = EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            if (session == null || string.IsNullOrWhiteSpace(session.ClipsDir))
            {
                MessageBox.Show(dialogOwner ?? ShowcaseActiveDialogOwner,
                    "Không tạo được thư mục phiên Showcase — kiểm tra «Storage root» trong Cài đặt.",
                    "Thêm clip",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var scenes = GetShowcaseVideoScenes(video);
            var addedCount = 0;
            var skippedCount = 0;

            for (var i = 0; i < filePaths.Count; i++)
            {
                var file = filePaths[i];
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                {
                    skippedCount++;
                    continue;
                }

                var destName = Path.GetFileName(file);
                var destPath = Path.Combine(session.ClipsDir, destName);
                var overwrite = false;

                if (File.Exists(destPath)
                    && !string.Equals(
                        Path.GetFullPath(file),
                        Path.GetFullPath(destPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    var choice = PromptShowcaseDuplicateClipSlotFile(
                        destName,
                        destName,
                        dialogOwner ?? ShowcaseActiveDialogOwner);
                    if (choice == ShowcaseDuplicateImageChoice.Skip)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (choice == ShowcaseDuplicateImageChoice.Cancel)
                    {
                        break;
                    }

                    overwrite = true;
                }

                var copiedPath = ShowcaseSessionService.CopyLocalClipPreserveName(session, file, overwrite);
                if (string.IsNullOrWhiteSpace(copiedPath))
                {
                    skippedCount++;
                    continue;
                }

                addedCount++;
            }

            if (scenes.Count > 0)
            {
                var assignResult = ShowcaseSessionService.AssignPendingClipsToSceneSlots(
                    session.ClipsDir,
                    scenes,
                    LogShowcase);
                if (assignResult.AssignedCount > 0)
                {
                    LogShowcase("[Showcase] Đã gán " + assignResult.AssignedCount
                                + " clip quay tay vào cảnh (giữ tên gốc).");
                }

                var appended = ShowcaseSessionService.AppendUnassignedPendingClipsAsScenes(
                    session.ClipsDir,
                    scenes,
                    productName,
                    profile,
                    LogShowcase);
                if (appended.AppendedCount > 0)
                {
                    ShowcaseSceneNamingHelper.ApplyConventionSceneTitles(scenes);
                    video.ApplySettingsToScenes();
                    RefreshAffiliateDeepStoryboard();
                }
            }

            var missing = ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, scenes, LogShowcase);
            video.RefreshDisplayFields();
            SyncBuffersToGrids();
            RefreshAiVideoGenModeReadinessLabels();
            NotifyShowcaseDraftDirty();
            LogShowcase("[Showcase] Đã thêm " + addedCount + " clip cho «" + productName + "» → "
                        + session.ClipsDir
                        + " (giữ tên gốc)."
                        + (skippedCount > 0 ? " Bỏ qua " + skippedCount + "." : string.Empty)
                        + (missing.Count == 0 && scenes.Count > 0
                            ? " ✓ đủ clip cho " + scenes.Count + " cảnh."
                            : missing.Count > 0
                                ? " Chưa gán cảnh: thiếu cảnh " + string.Join(", ", missing) + "."
                                : string.Empty));

            await Task.CompletedTask;
        }

        private enum ShowcaseDuplicateImageChoice
        {
            Overwrite,
            Skip,
            Cancel
        }

        private ShowcaseDuplicateImageChoice PromptShowcaseDuplicateSameOriginImage(
            string fileName,
            string sourceDirectory,
            int existingSceneIndex)
        {
            var where = string.IsNullOrWhiteSpace(sourceDirectory) ? "?" : sourceDirectory;
            var sceneHint = existingSceneIndex > 0
                ? " (đã có ở cảnh " + existingSceneIndex + " trên storyboard)"
                : " (đã chọn trong cùng lần thêm này)";

            var message =
                "Ảnh «" + fileName + "» từ thư mục:\r\n  " + where + "\r\n"
                + "đã được thêm trước đó" + sceneHint + ".\r\n\r\n"
                + "• Có — vẫn thêm bản sao\r\n"
                + "• Không — bỏ qua, không thêm ảnh này\r\n"
                + "• Hủy — dừng thêm các ảnh còn lại";

            return ShowShowcaseDuplicateImageChoiceDialog(message, "Trùng ảnh gốc");
        }

        private ShowcaseDuplicateImageChoice PromptShowcaseDuplicateSlotFile(
            string existingFileName,
            string newFileName,
            IWin32Window owner = null)
        {
            var message =
                "Trong thư mục source_images đã có file:\r\n  «" + existingFileName + "»\r\n\r\n"
                + "Ảnh bạn chọn: «" + newFileName + "»\r\n\r\n"
                + "• Có — đè lên file cũ\r\n"
                + "• Không — bỏ qua, không thêm ảnh này\r\n"
                + "• Hủy — dừng thêm các ảnh còn lại";

            return ShowShowcaseDuplicateImageChoiceDialog(message, "Trùng tên file trong thư mục", owner);
        }

        private ShowcaseDuplicateImageChoice PromptShowcaseDuplicateClipSlotFile(
            string existingFileName,
            string newFileName,
            IWin32Window owner = null)
        {
            var message =
                "Trong thư mục clips_render đã có file:\r\n  «" + existingFileName + "»\r\n\r\n"
                + "Clip bạn chọn: «" + newFileName + "»\r\n\r\n"
                + "• Có — đè lên file cũ\r\n"
                + "• Không — bỏ qua, không thêm clip này\r\n"
                + "• Hủy — dừng thêm các clip còn lại";

            return ShowShowcaseDuplicateImageChoiceDialog(message, "Trùng tên trong clips_render", owner);
        }

        private ShowcaseDuplicateImageChoice ShowShowcaseDuplicateImageChoiceDialog(
            string message,
            string title,
            IWin32Window owner = null)
        {
            var result = MessageBox.Show(
                owner ?? this,
                message,
                title,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            switch (result)
            {
                case DialogResult.Yes:
                    return ShowcaseDuplicateImageChoice.Overwrite;
                case DialogResult.No:
                    return ShowcaseDuplicateImageChoice.Skip;
                default:
                    return ShowcaseDuplicateImageChoice.Cancel;
            }
        }

        private void AppendShowcaseSceneFromImageFile(
            ShowcaseVideoItem video,
            string localPath,
            string localPickPath,
            string productName,
            string profile,
            AiVideoGenInputItem settingsTemplate)
        {
            string storedPick = localPickPath;
            try
            {
                if (!string.IsNullOrWhiteSpace(localPickPath))
                {
                    storedPick = Path.GetFullPath(localPickPath);
                }
            }
            catch
            {
                storedPick = localPickPath ?? string.Empty;
            }

            var item = new AiVideoGenInputItem
            {
                ProfileName = profile ?? string.Empty,
                ProductName = productName,
                ImageUrl = localPath,
                ThumbnailPath = localPath,
                ShowcaseLocalPickPath = storedPick ?? string.Empty,
                PipelineStatus = "Chờ"
            };
            ApplyShowcaseSettingsTemplate(item, settingsTemplate);
            video.Scenes.Add(item);
        }

        private string ResolveShowcaseSourceImagesDirForVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            if (video == null)
            {
                return null;
            }

            var scenes = GetShowcaseVideoScenes(video);
            var fromScenes = ShowcaseSessionService.ResolveSceneImagesDirectory(scenes);
            if (!string.IsNullOrWhiteSpace(fromScenes))
            {
                var inferred = ShowcaseSessionService.TryBuildSessionFromSceneImagesDirectory(
                    GetRunningProfileName(),
                    video.ProductName,
                    fromScenes);
                if (inferred != null)
                {
                    BindShowcaseSessionToVideo(inferred, video);
                }

                return fromScenes;
            }

            var sessionBase = (video.ShowcaseSessionBaseDir ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sessionBase))
            {
                var sessionImagesDir = Path.Combine(sessionBase, "source_images");
                if (ShowcaseSessionService.DirectoryHasImageFiles(sessionImagesDir))
                {
                    return sessionImagesDir;
                }
            }

            var productName = (video.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(productName))
            {
                return null;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings?.StorageRootPath);
            var profile = GetRunningProfileName();
            var session = EnsureShowcaseSession(profile, productName, settings?.StorageRootPath, video);
            return session?.SourceImagesDir;
        }

        private async Task OpenShowcaseSourceImagesFolderForVideoAsync(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var productName = (video.ProductName ?? string.Empty).Trim();
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            var targetDir = ResolveShowcaseSourceImagesDirForVideo(video, settings);

            if (string.IsNullOrWhiteSpace(targetDir))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Chưa có thư mục ảnh cho dòng này — đặt tên sản phẩm rồi bấm «➕» để thêm ảnh (app tạo phiên trong Storage root).",
                    "Thư mục ảnh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Directory.CreateDirectory(targetDir);
                Process.Start("explorer.exe", targetDir);
                LogShowcase("[Showcase] Đã mở thư mục ảnh dòng «" + productName + "»: " + targetDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Thư mục ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await Task.CompletedTask;
        }

        /// <summary>Hộp nhập nhanh tên sản phẩm khi thêm ảnh đầu tiên từ máy (chưa có cảnh nào để suy ra tên).</summary>
        private string PromptForShowcaseProductName()
        {
            const int dialogWidth = 900;
            const int dialogHeight = 480;

            using (var dlg = new Form
            {
                Text = "Tên sản phẩm",
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(dialogWidth, dialogHeight),
                MinimumSize = new Size(dialogWidth, dialogHeight),
                MaximumSize = new Size(dialogWidth, dialogHeight),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                Padding = new Padding(24)
            })
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    BackColor = dlg.BackColor
                };
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

                var lbl = new Label
                {
                    Text = "Nhập tên sản phẩm (dùng để đặt tên phiên/thư mục Showcase):",
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    MaximumSize = new Size(dialogWidth - 48, 0),
                    ForeColor = Color.FromArgb(200, 204, 214),
                    Font = new Font(Font.FontFamily, 11f),
                    Margin = new Padding(0, 0, 0, 12)
                };

                var txt = new TextBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font(Font.FontFamily, 12f),
                    Margin = new Padding(0, 0, 0, 16)
                };

                var flpButtons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = dlg.BackColor,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Width = 120,
                    Height = 44,
                    Font = new Font(Font.FontFamily, 11f),
                    Margin = new Padding(8, 0, 0, 0)
                };
                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Width = 120,
                    Height = 44,
                    Font = new Font(Font.FontFamily, 11f),
                    Margin = new Padding(0)
                };

                flpButtons.Controls.Add(btnOk);
                flpButtons.Controls.Add(btnCancel);

                root.Controls.Add(lbl, 0, 0);
                root.Controls.Add(txt, 0, 1);
                root.Controls.Add(flpButtons, 0, 3);

                dlg.Controls.Add(root);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        /// <summary>Trạng thái workflow Showcase hiện tại (dùng cho dòng cảnh báo readiness) — trả về text + đã sẵn sàng render hay chưa.</summary>
        private (string Text, bool Ready) GetShowcaseWorkflowStatus()
        {
            var videos = GetShowcaseSelectedVideosOrdered();
            if (videos.Count > 1)
            {
                var parts = new List<string>();
                var allReady = true;
                foreach (var video in videos)
                {
                    var st = GetShowcaseWorkflowStatusForVideo(video);
                    if (!st.Ready)
                    {
                        allReady = false;
                    }

                    parts.Add("«" + (video.ProductName ?? "?") + "»: " + st.Text);
                }

                return (allReady
                    ? "Đủ clip + thoại cho " + videos.Count + " video đã chọn — tab Âm thanh «Render Audio» rồi «Render video»."
                    : string.Join("  |  ", parts), allReady);
            }

            var target = videos.Count == 1 ? videos[0] : GetActiveShowcaseVideo();
            return GetShowcaseWorkflowStatusForVideo(target);
        }

        private (string Text, bool Ready) GetShowcaseWorkflowStatusForVideo(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return ("Chọn dòng video trên lưới, thêm ảnh qua cột «Ảnh» (➕ Thêm ảnh) để bắt đầu.", false);
            }

            var scenes = GetShowcaseVideoScenes(video);
            if (scenes.Count < ShowcaseWorkflowConstants.MinScenes)
            {
                return ("«" + (video.ProductName ?? "?") + "» cần ít nhất 1 ảnh trên storyboard.", false);
            }

            var hasScript = scenes.Any(s => ShowcaseClipToolHelper.SceneHasClipPrompt(s));
            if (!hasScript)
            {
                return ("«" + (video.ProductName ?? "?") + "» — bấm «Tạo kịch bản».", false);
            }

            var profile = GetRunningProfileName();
            var productName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();
            ShowcaseSessionState session = null;
            if (!string.IsNullOrWhiteSpace(productName))
            {
                session = EnsureShowcaseSession(profile, productName, null, video);
            }

            if (session == null)
            {
                return ("«" + (video.ProductName ?? "?") + "» — «Tải excel prompt» (bảng Prompt) rồi bỏ file Veo/Kling vào clips_render (cột «Cảnh»).", false);
            }

            var clipsDir = !string.IsNullOrWhiteSpace(video.ShowcaseClipsDir)
                ? video.ShowcaseClipsDir
                : session.ClipsDir;
            ShowcaseSessionService.RefreshClipStatus(clipsDir, scenes, null);
            video.RefreshDisplayFields();

            var missing = scenes.Count(s => string.IsNullOrWhiteSpace(s?.ClipPath) || !File.Exists(s.ClipPath));
            var withClip = scenes.Count - missing;

            if (withClip == 0)
            {
                var zoomPending = scenes.Count(s =>
                    string.Equals(s?.ShowcaseClipTool, ShowcaseClipToolHelper.ToolZoom, StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(s?.ClipPath) || !File.Exists(s.ClipPath)));
                var hint = zoomPending > 0
                    ? " (có " + zoomPending + " cảnh Zoom — «Tạo clip Zoom»)"
                    : string.Empty;
                return ("«" + (video.ProductName ?? "?") + "» — bỏ ít nhất 1 clip vào clips_render (cột «Cảnh»)" + hint + ".", false);
            }

            if (!ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes))
            {
                var partial = missing > 0
                    ? " (đã có " + withClip + "/" + scenes.Count + " clip — cảnh thiếu giữ thoại nháp)"
                    : string.Empty;
                return ("«" + (video.ProductName ?? "?") + "» — bấm «Tạo lời thoại»" + partial + ".", false);
            }

            var durationSignature = BuildShowcaseClipDurationSignature(LoadShowcaseSettingsSnapshot(), scenes);
            TryResyncShowcaseClipFingerprintForInPlaceRefresh(video, scenes, durationSignature);

            if (missing > 0)
            {
                var zoomPending = scenes.Count(s =>
                    string.Equals(s?.ShowcaseClipTool, ShowcaseClipToolHelper.ToolZoom, StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(s?.ClipPath) || !File.Exists(s.ClipPath)));
                var hint = zoomPending > 0
                    ? " (có " + zoomPending + " cảnh Zoom — «Tạo clip Zoom»)"
                    : string.Empty;
                return ("«" + (video.ProductName ?? "?") + "» thiếu clip " + missing + "/" + scenes.Count + " cảnh trước render" + hint + ".", false);
            }

            var sessionBase = session?.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            if (string.IsNullOrWhiteSpace(sessionBase)
                || !ShowcaseNarrationCacheHelper.HasFullMixPreviewFile(sessionBase))
            {
                return ("«" + (video.ProductName ?? "?") + "» — tab Âm thanh → «Render Audio» trước khi render video.", false);
            }

            return ("«" + (video.ProductName ?? "?") + "» sẵn sàng render.", true);
        }

        private void ShowShowcaseRenderOverviewForVideo(ShowcaseVideoItem video, AppSettings settings, string profile)
        {
            if (video == null)
            {
                return;
            }

            var scenes = GetShowcaseVideoScenes(video);
            var firstName = (video.ProductName ?? scenes.FirstOrDefault()?.ProductName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                if (_showcaseSession == null || !string.Equals(_showcaseSession.ProductName, firstName, StringComparison.OrdinalIgnoreCase))
                {
                    EnsureShowcaseSession(profile, firstName, settings?.StorageRootPath, video);
                }
            }

            if (_showcaseSession != null && scenes.Count > 0)
            {
                var clipsDir = !string.IsNullOrWhiteSpace(video.ShowcaseClipsDir)
                    ? video.ShowcaseClipsDir
                    : _showcaseSession.ClipsDir;
                ShowcaseSessionService.RefreshClipStatus(clipsDir, scenes, null);
                video.RefreshDisplayFields();
            }

            var blockers = CollectShowcaseRenderBlockers(video, settings, profile);
            var snapshot = ShowcaseRenderOverviewBuilder.Build(video, scenes, _showcaseSession, settings, blockers);
            LogShowcase("[Showcase] Tổng quan — bảng xem trước render («" + snapshot.ProductTitle + "»).");

            using (var dlg = new ShowcaseRenderOverviewForm(snapshot))
            {
                dlg.ShowDialog(ShowcaseActiveDialogOwner);
            }
        }

        private bool HasActiveShowcaseRenderJobs()
        {
            if (_globalJobQueue == null)
            {
                return false;
            }

            return _globalJobQueue.AllJobs.Any(j =>
                j != null &&
                j.Kind == OmniJobKind.AffiliateDeepRender &&
                (j.Status == OmniJobStatus.Pending ||
                 j.Status == OmniJobStatus.RetryPending ||
                 j.Status == OmniJobStatus.Running ||
                 j.Status == OmniJobStatus.Processing));
        }

        private static string BuildShowcaseApprovalScriptText(ShowcaseVideoItem video, IList<AiVideoGenInputItem> scenes)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(video?.ShowcaseTheme))
            {
                sb.AppendLine("Theme: " + video.ShowcaseTheme.Trim());
            }

            if (!string.IsNullOrWhiteSpace(video?.ShowcaseHookText))
            {
                sb.AppendLine("Hook: " + video.ShowcaseHookText.Trim());
            }

            if (scenes != null)
            {
                for (var i = 0; i < scenes.Count; i++)
                {
                    var voice = scenes[i]?.SceneVoiceover?.Trim();
                    if (string.IsNullOrWhiteSpace(voice))
                    {
                        continue;
                    }

                    sb.AppendLine("Scene " + (i + 1) + ": " + voice);
                }
            }

            if (!string.IsNullOrWhiteSpace(video?.ShowcaseCtaText))
            {
                sb.AppendLine("CTA: " + video.ShowcaseCtaText.Trim());
            }

            return sb.ToString().Trim();
        }

        private void UpdateShowcaseNarrationButtonState()
        {
            var hasNarration = TryResolveActiveShowcaseSessionBase(out var sessionBase)
                && ShowcaseNarrationCacheHelper.HasNarrationFile(sessionBase);
            _aiVideoGenControls?.SetShowcaseNarrationButtonMode(hasNarration);
            _aiVideoGenControls?.SetShowcaseListenNarrationEnabled(hasNarration);
        }

        private bool TryResolveActiveShowcaseSessionBase(out string sessionBase)
        {
            sessionBase = null;
            var video = GetActiveShowcaseVideo();
            if (video == null)
            {
                return false;
            }

            var scenes = GetShowcaseVideoScenes(video);
            sessionBase = _showcaseSession?.BaseDir;
            if (string.IsNullOrWhiteSpace(sessionBase))
            {
                sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
            }

            return !string.IsNullOrWhiteSpace(sessionBase);
        }

        async Task IAiVideoGenControlsHost.GenerateShowcaseZoomClipsAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn ít nhất một dòng video trên lưới rồi bấm «Tạo clip Zoom»."))
            {
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            string ffmpeg;
            try
            {
                ffmpeg = await FfmpegToolkitService.EnsureAvailableAsync(settings, LogShowcase, ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo clip Zoom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Không tìm thấy ffmpeg.exe — bấm «⬇ Tải FFmpeg» trong tab Cài đặt rồi thử lại.",
                    "Tạo clip Zoom",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            for (var i = 0; i < videos.Count; i++)
            {
                ThrowIfShowcaseTabCancelled();
                var video = videos[i];
                ActivateShowcaseVideo(video, refreshStoryboard: false);
                if (videos.Count > 1)
                {
                    LogShowcase("[Showcase] Zoom clip (" + (i + 1) + "/" + videos.Count + ") — «" + video.ProductName + "»");
                }

                await GenerateShowcaseZoomClipsForVideoAsync(video, settings, profile, ffmpeg).ConfigureAwait(true);
            }

            ActivateShowcaseVideo(videos[videos.Count - 1]);
            SyncBuffersToGrids();
        }

        private async Task GenerateShowcaseZoomClipsForVideoAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            string ffmpegExecutable)
        {
            var scenes = GetShowcaseVideoScenes(video);
            if (scenes.Count == 0)
            {
                return;
            }

            var realScenes = scenes
                .Select((scene, index) => new { scene, index })
                .Where(x => x.scene != null &&
                            string.Equals(x.scene.ShowcaseClipTool, ShowcaseClipToolHelper.ToolReal, StringComparison.Ordinal))
                .ToList();

            var clipModeId = GetShowcaseClipModeForVideo(video);
            var zoomScenes = scenes
                .Select((scene, index) => new { scene, index })
                .Where(x => x.scene != null &&
                            string.Equals(x.scene.ShowcaseClipTool, ShowcaseClipToolHelper.ToolZoom, StringComparison.Ordinal))
                .ToList();

            if (zoomScenes.Count > 0 && !ShowcaseClipModePresets.ModeAllowsInAppZoom(clipModeId))
            {
                var modeLabel = ShowcaseClipModePresets.GetDisplayLabel(clipModeId);
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video.ProductName ?? string.Empty).Trim() + "» — cột «Công cụ Video» đang là «" + modeLabel
                    + "» (không có Zoom trong app).\r\n\r\n"
                    + "Đổi sang chế độ có Zoom trong app (Veo + Zoom, Zoom + Kling, Chỉ Zoom, Gemini gợi ý…) rồi bấm «Tạo kịch bản» trước khi «Tạo clip Zoom».",
                    "Tạo clip Zoom",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (zoomScenes.Count == 0 && realScenes.Count == 0)
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "«" + (video.ProductName ?? string.Empty).Trim() + "» không có cảnh gán công cụ Zoom hoặc clip quay tay.\r\n\r\n"
                    + "Chạy «Tạo kịch bản» (hoặc sửa từng cảnh trong cột Prompt) để có cảnh clip_tool = Zoom, rồi thử lại.",
                    "Tạo clip Zoom",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var productName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();
            var session = EnsureShowcaseSession(profile, productName, settings.StorageRootPath, video);
            if (session == null || string.IsNullOrWhiteSpace(session.ClipsDir))
            {
                MessageBox.Show(ShowcaseActiveDialogOwner,
                    "Không tạo được thư mục phiên Showcase (clips_render) — kiểm tra «Storage root» trong Cài đặt.",
                    "Tạo clip Zoom",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            BindShowcaseSessionToVideo(session, video);

            try
            {
                await ShowcaseSessionService.DownloadSceneImagesAsync(session, scenes, LogShowcase, ShowcaseTabCancellationToken)
                    .ConfigureAwait(true);
                Directory.CreateDirectory(session.ClipsDir);

                var outputCanvas = GetShowcaseOutputCanvasForVideo(video, settings);
                LogShowcase("[Showcase] Khung Zoom/render: " + outputCanvas.DisplayLabel);

                var mismatches = new List<ShowcaseZoomAspectMismatchInfo>();
                foreach (var entry in zoomScenes)
                {
                    var imagePath = entry.scene?.ThumbnailPath;
                    if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                    {
                        continue;
                    }

                    if (ShowcaseZoomAspectFitHelper.TryGetImagePixelSize(imagePath, out var imageWidth, out var imageHeight)
                        && ShowcaseZoomAspectFitHelper.IsAspectMismatch(imageWidth, imageHeight, outputCanvas))
                    {
                        mismatches.Add(new ShowcaseZoomAspectMismatchInfo(entry.index + 1, imageWidth, imageHeight));
                    }
                }

                var mismatchFitMode = ShowcaseZoomAspectFitMode.Crop;
                if (mismatches.Count > 0)
                {
                    using (var fitDialog = new ShowcaseZoomAspectFitDialog(
                               video?.ProductName,
                               outputCanvas,
                               mismatches))
                    {
                        if (fitDialog.ShowDialog(ShowcaseActiveDialogOwner) != DialogResult.OK)
                        {
                            LogShowcase("[Showcase] Hủy tạo clip Zoom — người dùng hủy chọn cách xử lý tỉ lệ ảnh.");
                            return;
                        }

                        mismatchFitMode = fitDialog.SelectedMode;
                    }

                    LogShowcase("[Showcase] Ảnh lệch khung: " + mismatches.Count + " cảnh → "
                                + (mismatchFitMode == ShowcaseZoomAspectFitMode.BlurPad ? "blur nền" : "cắt (cover)"));
                }

                var created = 0;
                for (var i = 0; i < zoomScenes.Count; i++)
                {
                    ThrowIfShowcaseTabCancelled();
                    var entry = zoomScenes[i];
                    var scene = entry.scene;
                    var sceneOrder = entry.index + 1;
                    var imagePath = scene.ThumbnailPath;
                    if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                    {
                        LogShowcase("[Showcase] Bỏ qua cảnh " + sceneOrder + " — chưa có ảnh local.");
                        continue;
                    }

                    var zoomStyle = scene.ShowcaseZoomStyleId;
                    if (string.IsNullOrWhiteSpace(zoomStyle))
                    {
                        zoomStyle = ShowcaseZoomStyleCatalog.ResolveStyleId(
                            null,
                            scene.ZoomHint,
                            scene.ShowcaseImageKind,
                            entry.index);
                    }

                    var tempOutput = Path.Combine(
                        Path.GetTempPath(),
                        "showcase_zoom_" + session.ProfileName + "_s" + sceneOrder.ToString("D2") + "_" + Guid.NewGuid().ToString("N") + ".mp4");
                    var clipDuration = ShowcaseSceneDurationHelper.ResolveZoomClipDuration(scene);
                    var zoomSpeed = scene.ShowcaseZoomSpeedId;
                    if (string.IsNullOrWhiteSpace(zoomSpeed))
                    {
                        zoomSpeed = ShowcaseZoomSpeedCatalog.ResolveSpeedId(null, scene.ZoomHint);
                    }

                    var aspectFitMode = ShowcaseZoomAspectFitMode.Crop;
                    if (ShowcaseZoomAspectFitHelper.TryGetImagePixelSize(imagePath, out var imgW, out var imgH)
                        && ShowcaseZoomAspectFitHelper.IsAspectMismatch(imgW, imgH, outputCanvas))
                    {
                        aspectFitMode = mismatchFitMode;
                    }

                    try
                    {
                        await ShowcaseZoomClipService.GenerateAsync(
                            imagePath,
                            tempOutput,
                            entry.index,
                            zoomStyle,
                            ffmpegExecutable,
                            LogShowcase,
                            ShowcaseTabCancellationToken,
                            clipDuration,
                            outputCanvas,
                            zoomSpeed,
                            aspectFitMode).ConfigureAwait(true);

                        var destPath = ShowcaseSessionService.CopyLocalSceneClip(
                            session,
                            sceneOrder,
                            tempOutput,
                            overwriteExisting: true);
                        if (string.IsNullOrWhiteSpace(destPath))
                        {
                            LogShowcase("[Showcase] Không ghi được clip Zoom cảnh " + sceneOrder + " vào clips_render.");
                            continue;
                        }

                        scene.ClipPath = destPath;
                        created++;
                        LogShowcase("[Showcase] Clip Zoom cảnh " + sceneOrder + " ("
                                    + ShowcaseSceneDurationHelper.FormatSecondsLog(clipDuration) + ") → "
                                    + Path.GetFileName(destPath));
                    }
                    finally
                    {
                        try
                        {
                            if (File.Exists(tempOutput))
                            {
                                File.Delete(tempOutput);
                            }
                        }
                        catch
                        {
                            // ignore temp cleanup
                        }
                    }
                }

                var realCreated = 0;
                if (realScenes.Count > 0)
                {
                    for (var i = 0; i < realScenes.Count; i++)
                    {
                        ThrowIfShowcaseTabCancelled();
                        var entry = realScenes[i];
                        var scene = entry.scene;
                        var sceneOrder = entry.index + 1;
                        var sourcePath = (scene.ShowcaseRealClipSourcePath ?? scene.ClipPath ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                        {
                            LogShowcase("[Showcase] Bỏ qua cảnh " + sceneOrder + " — không tìm thấy clip quay tay gốc.");
                            continue;
                        }

                        var originalFileName = Path.GetFileName(sourcePath);
                        var tempOutput = Path.Combine(
                            Path.GetTempPath(),
                            "showcase_real_" + session.ProfileName + "_s" + sceneOrder.ToString("D2") + "_" + Guid.NewGuid().ToString("N") + ".mp4");
                        try
                        {
                            await ShowcaseCtaBrollAppendHelper.NormalizeRealClipAsync(
                                    sourcePath,
                                    tempOutput,
                                    outputCanvas,
                                    ffmpegExecutable,
                                    LogShowcase,
                                    ShowcaseTabCancellationToken)
                                .ConfigureAwait(true);

                            var destPath = ShowcaseSessionService.CopyLocalClipWithBaseName(
                                session,
                                tempOutput,
                                originalFileName,
                                overwriteExisting: true);
                            if (string.IsNullOrWhiteSpace(destPath))
                            {
                                LogShowcase("[Showcase] Không ghi được clip quay tay cảnh " + sceneOrder + " vào clips_render.");
                                continue;
                            }

                            scene.ClipPath = destPath;
                            scene.ShowcaseRealClipSourcePath = destPath;
                            realCreated++;
                            LogShowcase("[Showcase] Clip quay tay cảnh " + sceneOrder + " → " + Path.GetFileName(destPath) + " (giữ tên gốc).");
                        }
                        catch (Exception ex)
                        {
                            LogShowcase("[Showcase] Lỗi chuẩn hoá clip quay tay cảnh " + sceneOrder + ": " + ex.Message);
                        }
                        finally
                        {
                            try
                            {
                                if (File.Exists(tempOutput))
                                {
                                    File.Delete(tempOutput);
                                }
                            }
                            catch
                            {
                                // ignore temp cleanup
                            }
                        }
                    }

                    LogShowcase("[Showcase] Đã tạo " + realCreated + " clip quay tay trong clips_render: " + session.ClipsDir);
                }

                ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, scenes, LogShowcase);
                video.RefreshDisplayFields();
                RefreshAffiliateDeepStoryboard();
                SyncBuffersToGrids();
                NotifyShowcaseDraftDirty();
                LogShowcase("[Showcase] Đã tạo " + created + " clip Zoom trong clips_render: " + session.ClipsDir);
                if (created > 0)
                {
                    var durationSignature = BuildShowcaseClipDurationSignature(settings, scenes);
                    if (TryResyncShowcaseClipFingerprintForInPlaceRefresh(video, scenes, durationSignature))
                    {
                        LogShowcase("[Showcase] Clip Zoom mới — giữ thoại nếu cùng thời lượng.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogShowcase("[Showcase] Tạo clip Zoom lỗi («" + productName + "»): " + ex.Message);
                MessageBox.Show(ShowcaseActiveDialogOwner, ex.Message, "Tạo clip Zoom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void SyncShowcaseSessionFromVideo(ShowcaseSessionState session, ShowcaseVideoItem video)
        {
            if (session == null || video == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(video.ShowcaseTheme))
            {
                session.Theme = video.ShowcaseTheme.Trim();
            }

            if (!string.IsNullOrWhiteSpace(video.ShowcaseHookText))
            {
                session.HookText = video.ShowcaseHookText.Trim();
            }

            if (!string.IsNullOrWhiteSpace(video.ShowcaseCtaText))
            {
                session.CtaText = video.ShowcaseCtaText.Trim();
            }
        }

        /// <summary>Tạo phiên mới nếu chưa có hoặc sản phẩm đã đổi — khôi phục từ dòng video/draft trước khi tạo folder mới.</summary>
        private void LogShowcaseSessionRestoreOnce(string message, string baseDir)
        {
            var key = (baseDir ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key))
            {
                LogShowcase(message);
                return;
            }

            if (string.Equals(_showcaseSessionRestoreLogKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _showcaseSessionRestoreLogKey = key;
            LogShowcase(message);
        }

        private ShowcaseSessionState EnsureShowcaseSession(
            string profile,
            string productName,
            string storageRoot,
            ShowcaseVideoItem video = null,
            bool allowRecentSessionFallback = true)
        {
            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profile);
            var trimmedProduct = (productName ?? string.Empty).Trim();

            if (video != null)
            {
                var fromVideo = ShowcaseSessionService.RestoreSession(
                    resolvedProfile,
                    trimmedProduct,
                    video.ShowcaseSessionBaseDir,
                    video.ShowcaseClipsDir);
                if (fromVideo != null)
                {
                    SyncShowcaseSessionFromVideo(fromVideo, video);
                    _showcaseSession = fromVideo;
                    BindShowcaseSessionToVideo(_showcaseSession, video);
                    LogShowcaseSessionRestoreOnce("[Showcase] Khôi phục phiên từ dòng video: " + _showcaseSession.BaseDir, _showcaseSession.BaseDir);
                    return _showcaseSession;
                }

                if (!string.IsNullOrWhiteSpace(video.ShowcaseSessionBaseDir))
                {
                    var fromBase = ShowcaseSessionService.TryRestoreSessionFromBaseDir(
                        resolvedProfile,
                        trimmedProduct,
                        video.ShowcaseSessionBaseDir);
                    if (fromBase != null)
                    {
                        SyncShowcaseSessionFromVideo(fromBase, video);
                        _showcaseSession = fromBase;
                        BindShowcaseSessionToVideo(_showcaseSession, video);
                        LogShowcaseSessionRestoreOnce(
                            "[Showcase] Khôi phục phiên từ BaseDir dòng video: " + _showcaseSession.BaseDir,
                            _showcaseSession.BaseDir);
                        return _showcaseSession;
                    }
                }

                if (video.Scenes.Count > 0)
                {
                    var imagesDir = ShowcaseSessionService.ResolveSceneImagesDirectory(GetShowcaseVideoScenes(video));
                    var fromImages = ShowcaseSessionService.TryBuildSessionFromSceneImagesDirectory(
                        resolvedProfile,
                        trimmedProduct,
                        imagesDir);
                    if (fromImages != null)
                    {
                        SyncShowcaseSessionFromVideo(fromImages, video);
                        _showcaseSession = fromImages;
                        BindShowcaseSessionToVideo(_showcaseSession, video);
                        LogShowcaseSessionRestoreOnce(
                            "[Showcase] Khôi phục phiên từ thư mục ảnh storyboard: " + _showcaseSession.BaseDir,
                            _showcaseSession.BaseDir);
                        return _showcaseSession;
                    }
                }
            }

            if (_showcaseSession != null &&
                string.Equals(_showcaseSession.ProductName, trimmedProduct, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_showcaseSession.ProfileName, resolvedProfile, StringComparison.OrdinalIgnoreCase) &&
                SessionMatchesVideoClipsDir(_showcaseSession, video))
            {
                SyncShowcaseSessionFromVideo(_showcaseSession, video);
                BindShowcaseSessionToVideo(_showcaseSession, video);
                return _showcaseSession;
            }

            var draftSession = ShowcaseDraftStore.ToSession(_showcaseDraftStore.Load().Session);
            if (draftSession != null &&
                string.Equals(draftSession.ProductName, trimmedProduct, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(draftSession.ProfileName, resolvedProfile, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(draftSession.ClipsDir) &&
                Directory.Exists(draftSession.ClipsDir))
            {
                SyncShowcaseSessionFromVideo(draftSession, video);
                _showcaseSession = draftSession;
                BindShowcaseSessionToVideo(_showcaseSession, video);
                LogShowcaseSessionRestoreOnce("[Showcase] Khôi phục phiên từ draft: " + _showcaseSession.BaseDir, _showcaseSession.BaseDir);
                return _showcaseSession;
            }

            var sceneCount = video?.SceneCount ?? 0;
            if (allowRecentSessionFallback && ShowcaseWorkflowConstants.HasEnoughScenes(sceneCount))
            {
                var recentSession = ShowcaseSessionService.TryFindRecentSessionWithClips(resolvedProfile, trimmedProduct, sceneCount);
                if (recentSession != null)
                {
                    SyncShowcaseSessionFromVideo(recentSession, video);
                    _showcaseSession = recentSession;
                    BindShowcaseSessionToVideo(_showcaseSession, video);
                    LogShowcase("[Showcase] Tìm thấy phiên có đủ clip: " + _showcaseSession.BaseDir);
                    NotifyShowcaseDraftDirty();
                    return _showcaseSession;
                }
            }

            if (!string.IsNullOrWhiteSpace(storageRoot))
            {
                ProfileScopedPaths.SetConfiguredStorageRoot(storageRoot);
            }

            _showcaseSession = ShowcaseSessionService.CreateSession(profile, productName, storageRoot);
            _showcaseSessionRestoreLogKey = string.Empty;
            SyncShowcaseSessionFromVideo(_showcaseSession, video);
            BindShowcaseSessionToVideo(_showcaseSession, video);
            LogShowcase("[Showcase] Phiên làm việc mới: " + _showcaseSession.BaseDir);
            NotifyShowcaseDraftDirty();
            return _showcaseSession;
        }

        private static void BindShowcaseSessionToVideo(ShowcaseSessionState session, ShowcaseVideoItem video)
        {
            if (session == null || video == null)
            {
                return;
            }

            video.ShowcaseSessionBaseDir = session.BaseDir ?? string.Empty;
            video.ShowcaseClipsDir = session.ClipsDir ?? string.Empty;
        }

        private static bool SessionMatchesVideoClipsDir(ShowcaseSessionState session, ShowcaseVideoItem video)
        {
            if (session == null)
            {
                return false;
            }

            var videoClipsDir = (video?.ShowcaseClipsDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(videoClipsDir))
            {
                return true;
            }

            return string.Equals(session.ClipsDir ?? string.Empty, videoClipsDir, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyShowcaseSceneOrder(System.Collections.Generic.List<AiVideoGenInputItem> orderedScenes)
        {
            var video = GetActiveShowcaseVideo();
            if (video == null || orderedScenes == null || orderedScenes.Count == 0)
            {
                return;
            }

            video.Scenes.Clear();
            video.Scenes.AddRange(orderedScenes);
            ShowcaseSceneNamingHelper.ApplyConventionSceneTitles(video.Scenes);
            video.ApplySettingsToScenes();
            video.RefreshDisplayFields();
            NotifyShowcaseDraftDirty();
        }

        private void ShowShowcaseSceneEditDialog(AiVideoGenInputItem item)
        {
            if (item == null)
            {
                return;
            }

            var profile = GetRunningProfileName();
            var productName = (item.ProductName ?? string.Empty).Trim();
            var session = EnsureShowcaseSession(profile, productName, null, GetActiveShowcaseVideo());

            using (var dlg = new Form
            {
                Text = "Sửa kịch bản Showcase",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(760, 620),
                BackColor = Color.FromArgb(31, 34, 42),
                ForeColor = Color.Gainsboro,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    Padding = new Padding(12),
                    RowCount = 9
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                Label MakeLabel(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(0, 6, 0, 2), ForeColor = Color.FromArgb(200, 204, 214) };
                TextBox MakeBox(string text, bool multiline = false) => new TextBox
                {
                    Text = text ?? string.Empty,
                    Multiline = multiline,
                    Dock = DockStyle.Fill,
                    ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                    BackColor = Color.FromArgb(45, 49, 60),
                    ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle
                };

                layout.Controls.Add(MakeLabel("Chủ đề video (cả session):"));
                var txtTheme = MakeBox(session.Theme);
                layout.Controls.Add(txtTheme);

                layout.Controls.Add(MakeLabel("Hook mở đầu:"));
                var txtHook = MakeBox(session.HookText);
                layout.Controls.Add(txtHook);

                layout.Controls.Add(MakeLabel("CTA kết thúc:"));
                var txtCta = MakeBox(session.CtaText);
                layout.Controls.Add(txtCta);

                layout.Controls.Add(MakeLabel("Tên cảnh (vd. \"Chất vải cận cảnh\") — vai trò: " + (string.IsNullOrWhiteSpace(item.SceneRole) ? "(chưa có)" : item.SceneRole) + ":"));
                var txtTitle = MakeBox(item.SceneTitle);
                layout.Controls.Add(txtTitle);

                layout.Controls.Add(MakeLabel("Voiceover cảnh này:"));
                var txtVoiceover = MakeBox(item.SceneVoiceover, multiline: true);
                layout.Controls.Add(txtVoiceover);

                layout.Controls.Add(MakeLabel("Prompt Veo (I2V, tiếng Anh):"));
                var txtVeoPrompt = MakeBox(item.VeoPrompt, multiline: true);
                layout.Controls.Add(txtVeoPrompt);

                var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
                var btnCancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 100, Height = 30 };
                var btnOk = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Width = 100, Height = 30 };
                buttonPanel.Controls.Add(btnCancel);
                buttonPanel.Controls.Add(btnOk);
                layout.Controls.Add(buttonPanel);

                dlg.Controls.Add(layout);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    session.Theme = txtTheme.Text.Trim();
                    session.HookText = txtHook.Text.Trim();
                    session.CtaText = txtCta.Text.Trim();
                    item.SceneTitle = txtTitle.Text.Trim();
                    item.SceneVoiceover = txtVoiceover.Text.Trim();
                    item.VeoPrompt = txtVeoPrompt.Text.Trim();
                    item.ShowcaseTheme = session.Theme;
                    SyncShowcaseSessionSettingsAcrossScenes(item);

                    RefreshAffiliateDeepStoryboard();
                    SyncBuffersToGrids();
                    RefreshAiVideoGenModeReadinessLabels();
                    LogShowcase("[Showcase] Đã lưu chỉnh sửa kịch bản cảnh «" + (item.SceneTitle.Length > 0 ? item.SceneTitle : productName) + "».");
                    NotifyShowcaseDraftDirty();
                }
            }
        }

        private AppSettings LoadShowcaseSettingsSnapshot()
        {
            try
            {
                return _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult() ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        private List<string> ResolveShowcaseRenderKeyBlockers(AppSettings settings = null)
        {
            settings = settings ?? LoadShowcaseSettingsSnapshot();
            var blockers = new List<string>();
            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                blockers.Add("AI API Key (Cài đặt — Whisper/phụ đề khi render)");
            }

            if (!TtsAvailabilityHelper.IsAnyShowcaseTtsConfigured(settings))
            {
                blockers.Add("TTS (Edge miễn phí hoặc ElevenLabs — tab Cài đặt)");
            }

            return blockers;
        }

        private bool ResolveShowcaseInfraReady(bool? ffmpegOk = null, bool? storageOk = null)
        {
            bool Ok(string key) => _systemHealth != null && _systemHealth.TryGetValue(key, out var value) && value;
            if (ffmpegOk == null)
            {
                ffmpegOk = Ok("ffmpeg");
            }

            if (storageOk == null)
            {
                storageOk = Ok("storage");
            }

            return ffmpegOk.Value && storageOk.Value;
        }

        private bool ResolveShowcaseExecuteButtonsIdle()
        {
            return !_showcaseTabPaused
                   && !_activeAffiliateDeepRenderJobId.HasValue
                   && !HasActiveShowcaseRenderJobs();
        }

        private List<string> CollectShowcaseRenderBlockers(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            bool? ffmpegOk = null,
            bool? storageOk = null)
        {
            var blockers = new List<string>();
            bool HealthOk(string key) =>
                _systemHealth != null && _systemHealth.TryGetValue(key, out var value) && value;

            if (ffmpegOk == null)
            {
                ffmpegOk = HealthOk("ffmpeg");
            }

            if (storageOk == null)
            {
                storageOk = HealthOk("storage");
            }

            if (!ffmpegOk.Value)
            {
                blockers.Add("FFmpeg chưa sẵn sàng — mở Cài đặt → «Tải FFmpeg».");
            }

            if (!storageOk.Value)
            {
                blockers.Add("Thư mục lưu trữ chưa cấu hình (Cài đặt).");
            }

            blockers.AddRange(ResolveShowcaseRenderKeyBlockers(settings));

            if (video == null)
            {
                blockers.Add("Chọn ít nhất một dòng video trên lưới.");
                return blockers;
            }

            var scenes = GetShowcaseVideoScenes(video);
            if (!ShowcaseWorkflowConstants.HasEnoughScenes(scenes.Count))
            {
                blockers.Add("«" + (video.ProductName ?? "?") + "» cần ít nhất 1 ảnh trên storyboard (cột «Ảnh»).");
                return blockers;
            }

            var firstName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();
            if (scenes.Any(x => !string.Equals((x?.ProductName ?? string.Empty).Trim(), firstName, StringComparison.OrdinalIgnoreCase)))
            {
                blockers.Add("«" + firstName + "» — mọi cảnh phải cùng tên sản phẩm.");
            }

            if (_showcaseSession == null || !string.Equals(_showcaseSession.ProductName, firstName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureShowcaseSession(profile, firstName, settings?.StorageRootPath, video);
            }

            if (_showcaseSession == null)
            {
                blockers.Add("«" + firstName + "» chưa có phiên Showcase — bấm «Tạo kịch bản» và «Tải excel prompt» (bảng Prompt).");
            }
            else
            {
                var missingClips = ShowcaseSessionService.RefreshClipStatus(_showcaseSession.ClipsDir, scenes, null);
                if (missingClips.Count > 0)
                {
                    blockers.Add("«" + firstName + "» thiếu clip cảnh: " + string.Join(", ", missingClips) + " (thư mục clips_render).");
                }
            }

            if (!ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes))
            {
                blockers.Add("«" + firstName + "» chưa có lời thoại — bấm «Tạo lời thoại».");
            }
            else
            {
                var durationSignature = BuildShowcaseClipDurationSignature(settings, scenes);
                TryResyncShowcaseClipFingerprintForInPlaceRefresh(video, scenes, durationSignature);

                var sessionBase = _showcaseSession?.BaseDir;
                if (string.IsNullOrWhiteSpace(sessionBase))
                {
                    sessionBase = ShowcaseNarrationCacheHelper.TryResolveSessionBaseFromClips(scenes);
                }

                if (string.IsNullOrWhiteSpace(sessionBase)
                    || !ShowcaseNarrationCacheHelper.HasFullMixPreviewFile(sessionBase))
                {
                    blockers.Add("«" + firstName + "» chưa có audio thành phẩm — tab Âm thanh → «Render Audio».");
                }
            }

            return blockers;
        }

        private string BuildShowcaseClipDurationSignature(AppSettings settings, IList<AiVideoGenInputItem> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return string.Empty;
            }

            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            try
            {
                return ShowcaseClipDurationHelper.BuildSignatureAsync(
                        scenes,
                        ffprobe,
                        ShowcaseTabCancellationToken)
                    .ConfigureAwait(true)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool TryResyncShowcaseClipFingerprintForInPlaceRefresh(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            string currentDurationSignature)
        {
            if (video == null || scenes == null)
            {
                return false;
            }

            if (!ShowcaseVoiceoverHelper.TryResyncFingerprintForClipFileRefresh(
                    video,
                    scenes,
                    currentDurationSignature))
            {
                return false;
            }

            LogShowcase("[Showcase] Clip đổi — giữ thoại/audio, render bình thường.");
            NotifyShowcaseDraftDirty();
            return true;
        }

        private void WarnShowcaseClipDurationDriftBeforeRender(
            ShowcaseVideoItem video,
            IList<AiVideoGenInputItem> scenes,
            AppSettings settings)
        {
            if (video == null || scenes == null || scenes.Count == 0 || settings == null)
            {
                return;
            }

            if (!ShowcaseVoiceoverHelper.HasCompleteVoiceover(video, scenes))
            {
                return;
            }

            var durationSignature = BuildShowcaseClipDurationSignature(settings, scenes);
            var drift = ShowcaseVoiceoverHelper.BuildClipDurationDriftSummary(video, durationSignature);
            if (string.IsNullOrWhiteSpace(drift))
            {
                return;
            }

            var productName = (video.ProductName ?? "?").Trim();
            LogShowcase("[Showcase] «" + productName + "» — cảnh báo lệch thời lượng clip trước render:\r\n" + drift);
            MessageBox.Show(
                ShowcaseActiveDialogOwner,
                "«" + productName + "» — thời lượng clip lệch so với lúc «Tạo lời thoại»:\r\n\r\n"
                + drift + "\r\n\r\nVẫn render với thoại/audio hiện có.",
                "Cảnh báo thời lượng clip",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void ShowShowcaseRenderBlockersMessage(IList<string> blockers)
        {
            if (blockers == null || blockers.Count == 0)
            {
                return;
            }

            MessageBox.Show(
                this,
                "Không thể render video:\r\n\r\n• " + string.Join("\r\n• ", blockers),
                "Render video",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private bool ResolveShowcaseRenderButtonEnabled(bool? ffmpegOk = null, bool? storageOk = null)
        {
            return ResolveShowcaseExecuteButtonsIdle();
        }

        private bool ResolveShowcaseOverviewButtonEnabled(bool? ffmpegOk = null, bool? storageOk = null)
        {
            return ResolveShowcaseExecuteButtonsIdle();
        }

        private void UpdateShowcaseRenderButtonState(bool? ffmpegOk = null, bool? storageOk = null)
        {
            RefreshShowcaseStopButtonState();

            if (_activeAffiliateDeepRenderJobId.HasValue || HasActiveShowcaseRenderJobs())
            {
                SetShowcaseExecuteButtonsEnabled(false, false);
                return;
            }

            var renderEnabled = ResolveShowcaseRenderButtonEnabled(ffmpegOk, storageOk);
            var overviewEnabled = ResolveShowcaseOverviewButtonEnabled(ffmpegOk, storageOk);
            SetShowcaseExecuteButtonsEnabled(renderEnabled, overviewEnabled);
            UpdateShowcaseNarrationButtonState();
        }

        private void SetShowcaseExecuteButtonsEnabled(bool renderEnabled, bool? overviewEnabled = null)
        {
            var idleEnabled = overviewEnabled ?? renderEnabled;

            if (btnRunAffiliateDeepVideo != null && !btnRunAffiliateDeepVideo.IsDisposed)
            {
                btnRunAffiliateDeepVideo.Enabled = renderEnabled;
            }

            if (btnShowcaseOverview != null && !btnShowcaseOverview.IsDisposed)
            {
                btnShowcaseOverview.Enabled = idleEnabled;
            }

            if (btnShowcasePushToAutoPost != null && !btnShowcasePushToAutoPost.IsDisposed)
            {
                btnShowcasePushToAutoPost.Enabled = idleEnabled;
            }

            if (btnDeepGenerateScript != null && !btnDeepGenerateScript.IsDisposed)
            {
                btnDeepGenerateScript.Enabled = idleEnabled;
            }
        }

        private IWin32Window ShowcaseActiveDialogOwner => (IWin32Window)_showcaseAudioDialogOwner ?? this;

        internal void BindShowcaseAudioDialog(ShowcaseBackgroundMusicEditorForm dialog)
        {
            if (dialog == null || dialog.IsDisposed)
            {
                return;
            }

            _showcaseAudioDialogOwner = dialog;
            _showcaseAudioLogMirror = dialog.SetOperationStatus;
            dialog.SetOperationStatus("Sẵn sàng. Log TTS / Render / nghe thử hiển thị tại đây.");
        }

        internal void UnbindShowcaseAudioDialog()
        {
            _showcaseAudioDialogOwner = null;
            _showcaseAudioLogMirror = null;
        }

        /// <summary>
        /// Gắn một dialog Showcase bất kỳ (vd. ShowcaseClipModeEditorForm) làm chủ sở hữu MessageBox +
        /// nơi hiển thị log trong lúc dialog đó đang mở modal — tái dùng chung cơ chế với
        /// <see cref="BindShowcaseAudioDialog"/> để log/MessageBox không bị khuất phía sau dialog.
        /// </summary>
        internal void BindShowcaseWorkDialog(Form dialog, Action<string> statusMirror)
        {
            if (dialog == null || dialog.IsDisposed)
            {
                return;
            }

            _showcaseAudioDialogOwner = dialog;
            _showcaseAudioLogMirror = statusMirror;
        }

        private void TryMirrorShowcaseLogToAudioDialog(string message)
        {
            if (_showcaseAudioLogMirror == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (message.IndexOf("[Showcase]", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (!ShouldMirrorShowcaseLineToAudioDialog(message))
            {
                return;
            }

            try
            {
                _showcaseAudioLogMirror(message);
            }
            catch
            {
                // non-critical UI
            }
        }

        private static bool ShouldMirrorShowcaseLineToAudioDialog(string message)
        {
            static bool Has(string text, string token) =>
                text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

            return Has(message, "audio")
                   || Has(message, "Render Audio")
                   || Has(message, "hook_preview")
                   || Has(message, "body_preview")
                   || Has(message, "narration")
                   || Has(message, "full_mix")
                   || Has(message, "TTS")
                   || Has(message, "Nghe")
                   || Has(message, "thành phẩm")
                   || Has(message, "ghép")
                   || Has(message, "Eleven")
                   || Has(message, "FFmpeg")
                   || Has(message, "lỗi");
        }
    }
}
