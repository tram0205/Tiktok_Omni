using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Helpers;
using Newtonsoft.Json;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1 : IAiVideoGenControlsHost
    {
        async Task IAiVideoGenControlsHost.GenerateGeminiPromptAsync()
        {
            var slideshowItems = GetSlideshowItemsForRender();
            if (slideshowItems.Count == 0)
            {
                Log("AI Video Gen: chưa có dữ liệu sản phẩm. Hãy bấm 'Đẩy sang AI Video Gen' từ tab Affiliate Hunter.");
                return;
            }

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    Log("AI Video Gen: thiếu AI API key trong Setting.");
                    return;
                }

                var gemini = new GeminiService();
                var script = await gemini.GenerateAffiliateExperienceScriptAsync(
                    "slideshow",
                    slideshowItems,
                    slideshowItems[0],
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None,
                    GetSelectedGeminiStyleTemplate()).ConfigureAwait(true);

                txtAiVideoGenPrompt.Text = script ?? string.Empty;
                Log("AI Video Gen: đã tạo prompt/script bằng Gemini.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen thất bại: " + ex.Message);
            }
        }

        async Task IAiVideoGenControlsHost.ReviewScriptBeforeRenderAsync()
        {
            var slideshowItems = GetSlideshowItemsForRender();
            if (slideshowItems.Count == 0)
            {
                Log("AI Video Gen: chưa có dữ liệu sản phẩm để review script.");
                return;
            }

            if (btnGenerateGeminiPrompt != null)
            {
                btnGenerateGeminiPrompt.Enabled = false;
            }

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    Log("AI Video Gen: thiếu AI API key trong Setting.");
                    return;
                }

                var selectedItems = slideshowItems.Take(10).ToList();
                if (slideshowItems.Count > 10)
                {
                    Log("AI Video Gen: chỉ tạo review script cho 10 sản phẩm đầu tiên mỗi lượt.");
                }

                var gemini = new GeminiService();
                var raw = await gemini.GenerateAffiliateExperienceScriptAsync(
                    "per-product",
                    selectedItems,
                    selectedItems[0],
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    CancellationToken.None,
                    GeminiStyleTemplate.Review).ConfigureAwait(true);

                var generated = ParseReviewScripts(raw, selectedItems);
                _aiVideoScriptBindingList.Clear();
                for (var i = 0; i < generated.Count; i++)
                {
                    _aiVideoScriptBindingList.Add(generated[i]);
                }

                if (_aiVideoScriptBindingList.Count > 0)
                {
                    dgvAiVideoScriptReview.ClearSelection();
                    dgvAiVideoScriptReview.Rows[0].Selected = true;
                    LoadSelectedReviewScriptToEditor();
                }

                Log($"AI Video Gen: đã tạo {generated.Count} script để bạn review trước khi render.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen review script thất bại: " + ex.Message);
            }
            finally
            {
                if (btnGenerateGeminiPrompt != null && !btnGenerateGeminiPrompt.IsDisposed)
                {
                    btnGenerateGeminiPrompt.Enabled = true;
                }
            }
        }

        async Task IAiVideoGenControlsHost.GenerateAffiliateScriptAsync()
        {
            if (!IsProductPipelineModeTab())
            {
                MessageBox.Show(this,
                    "Chọn tab Slideshow hoặc Affiliate chuyên sâu trước khi sinh script.",
                    "Sinh Script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!TryGetSelectedAiVideoGenItems(out var selected))
            {
                MessageBox.Show(this,
                    "Hãy chọn ít nhất một dòng sản phẩm trên lưới trước khi sinh script.",
                    "Sinh Script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                MessageBox.Show(this, "Cần AI API Key.", "Sinh script", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetAiGenScriptButtonsEnabled(false);
            try
            {
                var modeLabel = IsDeepDiveModeTab() ? "Affiliate Deep" : "Slideshow";
                foreach (var item in selected)
                {
                    try
                    {
                        var script = await _affiliateScriptPreviewService
                            .GenerateVoiceoverPreviewAsync(item, settings, CancellationToken.None)
                            .ConfigureAwait(true);
                        item.ScriptPreview = (script ?? string.Empty).Trim();
                        Log($"[Script] ({modeLabel}) Đã sinh script: {item.ProductName}");
                    }
                    catch (Exception ex)
                    {
                        Log("[Script] Lỗi: " + ex.Message);
                    }
                }

                AfterAiVideoGenScriptEdited();
            }
            finally
            {
                SetAiGenScriptButtonsEnabled(true);
            }
        }

        Task IAiVideoGenControlsHost.EditAffiliateScriptAsync()
        {
            if (!IsProductPipelineModeTab())
            {
                MessageBox.Show(this,
                    "Chọn tab Slideshow hoặc Affiliate chuyên sâu trước khi sửa script.",
                    "Sửa Script",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            if (!TryGetSingleSelectedAiVideoGenItem(out var item))
            {
                MessageBox.Show(this, "Chọn một dòng sản phẩm để sửa script.", "Sửa Script",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            if (IsDeepDiveModeTab())
            {
                if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                        "Chọn một hoặc nhiều dòng video trên lưới rồi bấm «Sửa kịch bản»."))
                {
                    return Task.CompletedTask;
                }

                foreach (var video in videos)
                {
                    ActivateShowcaseVideo(video, refreshStoryboard: false);
                    var rowIndex = FindShowcaseVideoGridRowIndex(video);
                    if (rowIndex < 0)
                    {
                        rowIndex = dgvDeepDiveInput?.CurrentRow?.Index ?? -1;
                    }

                    if (video.Scenes.Count == 0)
                    {
                        LogShowcase("[Showcase] «" + video.ProductName + "» chưa có cảnh — bỏ qua sửa kịch bản.");
                        continue;
                    }

                    ShowShowcaseScriptEditor(video, rowIndex);
                }

                if (videos.Count > 0)
                {
                    ActivateShowcaseVideo(videos[videos.Count - 1]);
                    SyncBuffersToGrids();
                }

                return Task.CompletedTask;
            }

            using (var dlg = new Form
            {
                Text = "Sửa Voiceover / Script",
                StartPosition = FormStartPosition.CenterParent,
                Size = new System.Drawing.Size(720, 480),
                BackColor = System.Drawing.Color.FromArgb(31, 34, 42),
                ForeColor = System.Drawing.Color.Gainsboro
            })
            {
                var box = new TextBox
                {
                    Multiline = true,
                    Dock = DockStyle.Fill,
                    ScrollBars = ScrollBars.Vertical,
                    Text = item.ScriptPreview ?? string.Empty,
                    BackColor = System.Drawing.Color.FromArgb(45, 49, 60),
                    ForeColor = System.Drawing.Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new System.Drawing.Font("Segoe UI", 10F)
                };
                var panel = new Panel { Dock = DockStyle.Bottom, Height = 44 };
                var btnOk = new Button
                {
                    Text = "Lưu",
                    DialogResult = DialogResult.OK,
                    Location = new System.Drawing.Point(12, 8),
                    Size = new System.Drawing.Size(100, 28)
                };
                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Location = new System.Drawing.Point(120, 8),
                    Size = new System.Drawing.Size(100, 28)
                };
                panel.Controls.Add(btnOk);
                panel.Controls.Add(btnCancel);
                dlg.Controls.Add(box);
                dlg.Controls.Add(panel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    item.ScriptPreview = box.Text?.Trim() ?? string.Empty;
                    AfterAiVideoGenScriptEdited();
                    Log("[Script] Đã lưu chỉnh sửa script.");
                }
            }

            return Task.CompletedTask;
        }

        async Task IAiVideoGenControlsHost.RunBatchPipelineAsync()
        {
            var keywords = (GetAffiliateKeywordsTextFromUi() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(keywords))
            {
                MessageBox.Show(this,
                    "Nhập từ khóa ở ô «từ khoá» (tab Săn Video) trước khi chạy Pipeline hàng loạt.",
                    "Pipeline hàng loạt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                txtAffiliateKeywords?.Focus();
                return;
            }

            _industrialBatchPhase = IndustrialBatchPhase.Hunt;
            Log("[Batch Pipeline] 1/5 — Hunt → Queue…");
            btnHuntAffiliates_Click(this, EventArgs.Empty);
            await Task.Delay(500).ConfigureAwait(true);
            await ContinueIndustrialBatchAfterHuntAsync().ConfigureAwait(true);
        }

        void IAiVideoGenControlsHost.CopyAiVideoPrompt()
        {
            var text = txtAiVideoGenPrompt?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Log("AI Video Gen: chưa có prompt để copy.");
                return;
            }

            try
            {
                Clipboard.SetText(text);
                Log("AI Video Gen: đã copy prompt vào clipboard.");
            }
            catch (Exception ex)
            {
                Log("AI Video Gen copy thất bại: " + ex.Message);
            }
        }

        void IAiVideoGenControlsHost.SaveAiVideoPrompt()
        {
            var text = txtAiVideoGenPrompt?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Log("AI Video Gen: chưa có nội dung để lưu file.");
                return;
            }

            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.FileName = $"ai_video_prompt_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    dialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                    dialog.Title = "Lưu AI Video Prompt";

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    File.WriteAllText(dialog.FileName, text, TextFileEncoding.Utf8NoBom);
                    Log("AI Video Gen: đã lưu prompt ra file " + dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log("AI Video Gen lưu file thất bại: " + ex.Message);
            }
        }

        async Task IAiVideoGenControlsHost.ProcessSlideshowVideoAsync()
        {
            SaveAllProductGridState();

            var selectedItems = GetSelectedAiVideoGenItemsForPipeline();
            if (selectedItems.Count == 0)
            {
                Log("AI Video Gen: chọn ít nhất một dòng sản phẩm trên lưới.");
                return;
            }

            var pipelineItems = CloneAiVideoGenItemsForPipeline(selectedItems);

            SyncSelectedScriptFromEditor();
            var script = txtAiVideoGenPrompt?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(script))
            {
                Log("AI Video Gen: chưa có script. Hãy tạo prompt Gemini hoặc nhập script trước.");
                return;
            }

            if (btnGenerateGeminiPrompt != null)
            {
                btnGenerateGeminiPrompt.Enabled = false;
            }

            _aiVideoGenCancellation?.Dispose();
            _aiVideoGenCancellation = new CancellationTokenSource();

            Log("Bắt đầu Pipeline Render & Đóng gói...");
            ResetAiRenderSlotProgress();

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                settings.VideoTransitionDurationSeconds = (double)numAiTransitionDuration.Value;
                settings.VideoTextSize = (int)numAiTextSize.Value;
                settings.VideoMusicVolume = (int)numAiMusicVolume.Value;
                await _configManager.SaveAsync(settings).ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);

                var profile = ResolvePrimaryProfileFromAiBuffer(pipelineItems);
                ProfileScopedPaths.EnsureProfileVideoTypeHierarchy(settings.StorageRootPath, profile);

                foreach (var item in selectedItems)
                {
                    if (item != null)
                    {
                        item.PipelineStatus = "Đang render";
                    }
                }

                SyncBuffersToGrids();

                var resultPath = await _videoProcessingService.RunFullVideoPipelineAsync(
                    pipelineItems,
                    script,
                    settings,
                    profile,
                    Log,
                    SetAiVideoGenPipelineProgress,
                    _aiVideoGenCancellation.Token).ConfigureAwait(true);

                Log("Pipeline hoàn tất: " + resultPath);

                foreach (var item in selectedItems)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    item.IsProcessed = true;
                    item.PipelineStatus = "Xong";
                    item.OutputVideoPath = resultPath ?? string.Empty;
                }

                MarkSlideshowItemsProcessed(selectedItems);
                SyncBuffersToGrids();
                dgvAiVideoGenInput?.Refresh();

                MessageBox.Show(
                    this,
                    "Video đã được Render & Đóng gói thành công!",
                    "AI Video Gen",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                Log("Pipeline đã hủy.");
            }
            catch (Exception ex)
            {
                Log("Lỗi Pipeline: " + ex.Message);
                MessageBox.Show(this, "Lỗi: " + ex.Message, "AI Video Gen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (btnGenerateGeminiPrompt != null && !btnGenerateGeminiPrompt.IsDisposed)
                {
                    btnGenerateGeminiPrompt.Enabled = true;
                }
            }
        }

        async Task IAiVideoGenControlsHost.OpenSlideshowOutputFolderAsync()
        {
            try
            {
                await OpenProfileOutputFolderInExplorerAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("[Folder] Không mở được thư mục output: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Không mở được thư mục: " + ex.Message,
                    "Thư mục output",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        async Task IAiVideoGenControlsHost.OpenAffiliateDeepOutputFolderAsync()
        {
            try
            {
                var scenes = GetDeepDiveStoryboardOrderedBuffer();
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
                var profile = GetRunningProfileName();
                var productName = (scenes.FirstOrDefault()?.ProductName ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(productName))
                {
                    // Chưa có sản phẩm/phiên nào — mở thư mục output chung của profile như trước.
                    await ((IAiVideoGenControlsHost)this).OpenSlideshowOutputFolderAsync().ConfigureAwait(true);
                    return;
                }

                var session = EnsureShowcaseSession(profile, productName, settings.StorageRootPath, GetActiveShowcaseVideo());
                var targetDir = !string.IsNullOrWhiteSpace(session?.OutputDir) ? session.OutputDir : session?.BaseDir;
                if (string.IsNullOrWhiteSpace(targetDir))
                {
                    await ((IAiVideoGenControlsHost)this).OpenSlideshowOutputFolderAsync().ConfigureAwait(true);
                    return;
                }

                Directory.CreateDirectory(targetDir);
                Process.Start("explorer.exe", targetDir);
                LogShowcase("[Showcase] Đã mở thư mục phiên: " + targetDir);
            }
            catch (Exception ex)
            {
                LogShowcase("[Folder] Không mở được thư mục output Showcase: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Không mở được thư mục: " + ex.Message,
                    "Thư mục output",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        void IAiVideoGenControlsHost.OpenApprovalQueue()
        {
            btnOpenApprovalQueue_Click(this, EventArgs.Empty);
        }

        void IAiVideoGenControlsHost.ClearActiveGrid()
        {
            try
            {
                if (_selectedAiVideoGenMode == AiVideoGenMode.Slideshow
                    || _selectedAiVideoGenMode == AiVideoGenMode.AffiliateDeep)
                {
                    var grid = GetActiveProductGrid();
                    if (grid != null && TryDeleteSelectedProductInputGridRows(grid, out var deleted) && deleted > 0)
                    {
                        if (ReferenceEquals(grid, dgvDeepDiveInput))
                        {
                            Log("[Grid] Đã xóa " + deleted + " dòng — đã chuyển vào thùng rác (giữ 24 giờ).");
                        }
                        else
                        {
                            Log("[Grid] Đã xóa " + deleted + " dòng.");
                        }
                    }

                    return;
                }

                var count = GetActiveGridRowCountForClear();
                if (count <= 0)
                {
                    return;
                }

                if (!UiConfirmHelper.ConfirmDeleteRows(this, count))
                {
                    return;
                }

                ClearActiveAiVideoGenModeBuffer();
            }
            catch (Exception ex)
            {
                Log("[Grid] Xóa dòng lỗi: " + ex.Message);
            }
        }

        async Task IAiVideoGenControlsHost.RunAffiliateDeepVideoAsync()
        {
            try
            {
                if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                        "Chọn ít nhất một dòng video trên lưới (Ctrl+click nhiều dòng) rồi bấm «Render video»."))
                {
                    return;
                }

                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
                var profile = GetRunningProfileName();
                var enqueued = 0;

                for (var i = 0; i < videos.Count; i++)
                {
                    ThrowIfShowcaseTabCancelled();
                    var video = videos[i];
                    ActivateShowcaseVideo(video, refreshStoryboard: false);
                    if (videos.Count > 1)
                    {
                        LogShowcase("[Showcase] Render (" + (i + 1) + "/" + videos.Count + ") — «" + video.ProductName + "»");
                    }

                    if (await TryEnqueueShowcaseRenderForVideoAsync(video, settings, profile).ConfigureAwait(true))
                    {
                        enqueued++;
                    }
                }

                if (enqueued > 0)
                {
                    ActivateShowcaseVideo(videos[videos.Count - 1]);
                    SyncBuffersToGrids();
                }
            }
            finally
            {
                RestoreShowcaseRenderButtonIfIdle();
            }
        }

        private void RestoreShowcaseRenderButtonIfIdle()
        {
            UpdateShowcaseRenderButtonState();
        }

        async Task IAiVideoGenControlsHost.PreviewShowcaseOverviewAsync()
        {
            if (!TryGetShowcaseSelectedVideosOrdered(out var videos,
                    "Chọn một dòng video trên lưới rồi bấm «Tổng quan»."))
            {
                return;
            }

            if (videos.Count > 1)
            {
                MessageBox.Show(this,
                    "«Tổng quan» chỉ xem một video/lần. Chọn đúng một dòng (bỏ Ctrl+click các dòng khác).",
                    "Tổng quan",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var video = videos[0];
            ActivateShowcaseVideo(video, refreshStoryboard: false);
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            ShowShowcaseRenderOverviewForVideo(video, settings, profile);
            await Task.CompletedTask.ConfigureAwait(true);
        }

        private sealed class ShowcaseRenderPlan
        {
            public List<AiVideoGenInputItem> Scenes { get; set; }

            public string FirstName { get; set; }

            public string Category { get; set; }

            public string HookText { get; set; }

            public string CtaText { get; set; }

            public string Theme { get; set; }

            public ShowcasePerVideoRenderSettings RenderSettings { get; set; }

            public AssSubtitleGeneratorOptions SubtitleOptions { get; set; }
        }

        private async Task<ShowcaseRenderPlan> BuildShowcaseRenderPlanCoreAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile,
            List<AiVideoGenInputItem> scenes,
            string firstName)
        {
            if (numAiTransitionDuration != null)
            {
                settings.VideoTransitionDurationSeconds = (double)numAiTransitionDuration.Value;
            }

            ShowcaseTransitionHelper.EnsureVideoDefaults(video, settings);
            var renderSettings = ShowcasePerVideoRenderSettings.FromVideo(video, settings);
            settings.VideoTransitionDurationSeconds = renderSettings.TransitionSeconds;
            settings.VideoTextSize = video.ShowcaseSubtitleFontSize > 0 ? video.ShowcaseSubtitleFontSize : 72;
            settings.VideoMusicVolume = renderSettings.MusicVolume;
            settings.VideoBackgroundMusicFileName = VideoReupRowItem.IsNoMusicSelection(renderSettings.BackgroundMusicFile)
                ? VideoReupRowItem.NoMusicSelectionLabel
                : (renderSettings.BackgroundMusicFile ?? string.Empty).Trim();

            await _configManager.SaveAsync(settings).ConfigureAwait(true);

            var category = (scenes[0]?.Category ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "Showcase";
            }

            ScoreAndApplyAiVideoGenSafety(scenes, txtAiVideoGenPrompt?.Text?.Trim());

            var subtitleOptions = ShowcaseSubtitleStyleHelper.BuildOptions(renderSettings, settings);
            var hookText = !string.IsNullOrWhiteSpace(video.ShowcaseHookText)
                ? video.ShowcaseHookText
                : (_showcaseSession?.HookText ?? string.Empty);
            var ctaText = !string.IsNullOrWhiteSpace(video.ShowcaseCtaText)
                ? video.ShowcaseCtaText
                : (_showcaseSession?.CtaText ?? string.Empty);

            return new ShowcaseRenderPlan
            {
                Scenes = scenes,
                FirstName = firstName,
                Category = category,
                HookText = hookText ?? string.Empty,
                CtaText = ctaText ?? string.Empty,
                Theme = _showcaseSession?.Theme ?? string.Empty,
                RenderSettings = renderSettings,
                SubtitleOptions = subtitleOptions
            };
        }

        private async Task<ShowcaseRenderPlan> TryBuildShowcaseRenderPlanAsync(
            ShowcaseVideoItem video,
            AppSettings settings,
            string profile)
        {
            var scenes = GetShowcaseVideoScenes(video);
            if (!ShowcaseWorkflowConstants.HasEnoughScenes(scenes.Count))
            {
                return null;
            }

            var firstName = (video.ProductName ?? scenes[0]?.ProductName ?? string.Empty).Trim();
            if (scenes.Any(x => !string.Equals((x?.ProductName ?? string.Empty).Trim(), firstName, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            if (_showcaseSession == null || !string.Equals(_showcaseSession.ProductName, firstName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureShowcaseSession(profile, firstName, settings.StorageRootPath, video);
            }

            if (_showcaseSession != null)
            {
                ShowcaseSessionService.RefreshClipStatus(_showcaseSession.ClipsDir, scenes, LogShowcase);
                video.RefreshDisplayFields();
                SyncBuffersToGrids();
                RefreshAiVideoGenModeReadinessLabels();
            }

            return await BuildShowcaseRenderPlanCoreAsync(video, settings, profile, scenes, firstName).ConfigureAwait(true);
        }

        private async Task<bool> TryEnqueueShowcaseRenderForVideoAsync(ShowcaseVideoItem video, AppSettings settings, string profile)
        {
            var blockers = CollectShowcaseRenderBlockers(video, settings, profile);
            if (blockers.Count > 0)
            {
                ShowShowcaseRenderBlockersMessage(blockers);
                return false;
            }

            var plan = await TryBuildShowcaseRenderPlanAsync(video, settings, profile).ConfigureAwait(true);
            if (plan == null)
            {
                ShowShowcaseRenderBlockersMessage(new List<string> { "Không chuẩn bị được dữ liệu render — kiểm tra storyboard và thử lại." });
                return false;
            }

            var scenes = plan.Scenes;
            var firstName = plan.FirstName;
            var renderSettings = plan.RenderSettings;
            var category = plan.Category;

            foreach (var item in scenes)
            {
                if (item != null)
                {
                    item.PipelineStatus = "Chờ";
                    item.ProfileName = string.IsNullOrWhiteSpace(item.ProfileName) ? profile : item.ProfileName;
                }
            }

            video.PipelineStatus = "Đang render";

            if (btnProcessVideo != null)
            {
                btnProcessVideo.Enabled = false;
            }

            if (btnGenerateGeminiPrompt != null)
            {
                btnGenerateGeminiPrompt.Enabled = false;
            }

            if (btnReviewScriptBeforeRender != null)
            {
                btnReviewScriptBeforeRender.Enabled = false;
            }

            ResetAiRenderSlotProgress();
            UpdateSinglePipelineProgress(2, "Đã xếp hàng Showcase render…");

            var job = new OmniJob
            {
                Kind = OmniJobKind.AffiliateDeepRender,
                Title = "Showcase — " + firstName,
                ProfileName = profile,
                PayloadJson = JsonConvert.SerializeObject(new AffiliateDeepRenderJobPayload
                {
                    ShowcaseVideoId = video.VideoId,
                    ProfileName = profile,
                    ProductName = firstName,
                    Category = category,
                    Products = scenes,
                    StorageRootPath = settings.StorageRootPath ?? string.Empty,
                    SafetyScore = scenes[0]?.SafetyScore ?? 100,
                    AffiliateLink = scenes[0]?.AffiliateLink ?? string.Empty,
                    ProductId = scenes[0]?.ProductId ?? string.Empty,
                    Theme = plan.Theme ?? string.Empty,
                    HookText = plan.HookText ?? string.Empty,
                    CtaText = plan.CtaText ?? string.Empty,
                    RenderSettings = renderSettings
                }),
                MaxRetries = 1,
                AffiliateLink = scenes[0]?.AffiliateLink ?? string.Empty,
                ProductId = scenes[0]?.ProductId ?? string.Empty,
                Tag = CreateShowcaseLinkedJobCancellation()
            };

            _activeAffiliateDeepRenderJobId = job.Id;
            _globalJobQueue.Enqueue(job);
            RefreshShowcaseStopButtonState();
            LogShowcase("[JobQueue] Showcase render «" + firstName + "» đã vào hàng đợi.");
            return true;
        }

        async Task IAiVideoGenControlsHost.SaveGeminiStyleTemplateAsync(GeminiStyleTemplate template)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            settings.GeminiStyleTemplate = template.ToString();
            await _configManager.SaveAsync(settings).ConfigureAwait(true);
        }
    }
}
