using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

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

        Task IAiVideoGenControlsHost.OpenAffiliateDeepOutputFolderAsync()
        {
            return ((IAiVideoGenControlsHost)this).OpenSlideshowOutputFolderAsync();
        }

        void IAiVideoGenControlsHost.OpenApprovalQueue()
        {
            btnOpenApprovalQueue_Click(this, EventArgs.Empty);
        }

        void IAiVideoGenControlsHost.ClearActiveGrid()
        {
            try
            {
                ClearActiveAiVideoGenModeBuffer();
            }
            catch (Exception ex)
            {
                Log("[Grid] Làm sạch buffer lỗi: " + ex.Message);
            }
        }

        async Task IAiVideoGenControlsHost.RunAffiliateDeepVideoAsync()
        {
            var top4 = GetDeepDiveOrderedScenesForRender();
            if (top4.Count < 4)
            {
                Log("Affiliate Deep Video: cần ít nhất 4 ảnh của cùng 1 sản phẩm.");
                return;
            }

            var firstName = (top4[0]?.ProductName ?? string.Empty).Trim();
            if (top4.Any(x => !string.Equals((x?.ProductName ?? string.Empty).Trim(), firstName, StringComparison.OrdinalIgnoreCase)))
            {
                Log("Affiliate Deep Video: 4 ảnh đầu phải thuộc cùng một sản phẩm (ProductName giống nhau).");
                return;
            }

            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);
            var profile = GetRunningProfileName();
            var category = (top4[0]?.Category ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "AffiliateDeep";
            }

            foreach (var item in top4)
            {
                if (item != null)
                {
                    item.PipelineStatus = "Chờ";
                    item.ProfileName = string.IsNullOrWhiteSpace(item.ProfileName) ? profile : item.ProfileName;
                }
            }

            ScoreAndApplyAiVideoGenSafety(top4, txtAiVideoGenPrompt?.Text?.Trim());
            SyncBuffersToGrids();

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
            UpdateSinglePipelineProgress(2, "Đã xếp hàng Deep render…");

            var job = new OmniJob
            {
                Kind = OmniJobKind.AffiliateDeepRender,
                Title = "Affiliate Deep — " + firstName,
                ProfileName = profile,
                PayloadJson = JsonConvert.SerializeObject(new AffiliateDeepRenderJobPayload
                {
                    ProfileName = profile,
                    ProductName = firstName,
                    Category = category,
                    Products = top4,
                    StorageRootPath = settings.StorageRootPath ?? string.Empty,
                    SafetyScore = top4[0]?.SafetyScore ?? 100,
                    UseMultiVoiceNarration = UseMultiVoiceNarrationEnabled(),
                    AffiliateLink = top4[0]?.AffiliateLink ?? string.Empty,
                    ProductId = top4[0]?.ProductId ?? string.Empty
                }),
                MaxRetries = 1,
                AffiliateLink = top4[0]?.AffiliateLink ?? string.Empty,
                ProductId = top4[0]?.ProductId ?? string.Empty
            };

            _activeAffiliateDeepRenderJobId = job.Id;
            _globalJobQueue.Enqueue(job);
            Log("[JobQueue] Affiliate Deep render đã vào hàng đợi (không chạy trên UI).");
        }

        async Task IAiVideoGenControlsHost.SaveGeminiStyleTemplateAsync(GeminiStyleTemplate template)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            settings.GeminiStyleTemplate = template.ToString();
            await _configManager.SaveAsync(settings).ConfigureAwait(true);
        }
    }
}
