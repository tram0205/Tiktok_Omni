using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private async void ShowPhilosophyBatchBackgroundMusicEditor(PhilosophyBatchItem batch, int gridRowIndex)
        {
            if (batch == null)
            {
                return;
            }

            var profile = ResolvePhilosophyBatchProfile(batch);
            var settings = _philosophySettingsSnap ?? await _configManager.LoadAsync().ConfigureAwait(true);
            if (_philosophySettingsSnap == null)
            {
                _philosophySettingsSnap = settings;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);

            var musicNames = OmniAudioLibrary.ListMusicFileNames(settings);
            var sharedMusicDir = OmniAudioLibrary.GetSharedMusicDirectory(settings);
            var musicSummary = musicNames.Count > 0
                ? musicNames.Count + " file nhạc · " + sharedMusicDir + "\r\n(Dùng chung mọi tab video — Showcase, Quote, Reup…)"
                : "Chưa có file — thêm vào Assets\\Audio\\Music (dùng chung mọi tab video)";

            var video = PhilosophyBatchShowcaseAudioAdapter.ToShowcaseVideo(batch, profile);
            PhilosophyAudioDefaults.EnsureVideoDefaults(video, settings);

            string SessionBase() => PhilosophyBatchAudioPreviewHelper.GetSessionBase(batch, settings);
            var quoteCount = batch.Quotes?.Count ?? 0;

            bool CanListenBody() =>
                PhilosophyBatchAudioPreviewHelper.HasAnyQuoteVoicePreview(SessionBase(), quoteCount);

            bool CanRenderFullMix() =>
                PhilosophyBatchAudioPreviewHelper.HasAnyQuoteVoicePreview(SessionBase(), quoteCount);

            bool CanListenFullMix() =>
                PhilosophyBatchAudioPreviewHelper.HasAnyQuoteFullMixPreview(SessionBase(), quoteCount);

            ShowcaseBackgroundMusicEditorForm audioDlg = null;
            audioDlg = PhilosophyAudioEditorForm.Create(
                video,
                settings,
                musicNames,
                generateBodyNarrationAsync: async () =>
                {
                    var quoteIndices = audioDlg.GetPhilosophyQuoteIndicesForBatchOperation();
                    audioDlg?.SetOperationStatus("Đang tạo audio quote…");
                    try
                    {
                        await PhilosophyBatchAudioPreviewHelper.GenerateQuoteVoicePreviewAsync(
                            batch,
                            video,
                            settings,
                            profile,
                            _videoProcessingService,
                            LogPhilosophy,
                            CancellationToken.None,
                            quoteIndices).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (audioDlg != null && !audioDlg.IsDisposed)
                        {
                            audioDlg.BeginInvoke(new Action(() =>
                            {
                                MessageBox.Show(audioDlg, ex.Message, "Tạo audio quote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }));
                        }

                        LogPhilosophy("[Quote] Tạo audio quote lỗi: " + ex.Message);
                        return;
                    }

                    if (audioDlg != null && !audioDlg.IsDisposed)
                    {
                        audioDlg.BeginInvoke(new Action(() =>
                        {
                            audioDlg.RefreshPhilosophyQuoteAudioUi();
                            audioDlg.SetOperationStatus(string.Empty);
                        }));
                    }
                },
                listenBodyNarrationAsync: async () =>
                {
                    audioDlg?.SetOperationStatus("Đang phát audio quote…");
                    await ListenPhilosophyBatchVoicePreviewAsync(batch, settings).ConfigureAwait(true);
                },
                canListenBodyNarration: CanListenBody,
                renderFullMixedAudioAsync: async () =>
                {
                    var quoteIndices = audioDlg.GetPhilosophyQuoteIndicesForBatchOperation();
                    audioDlg?.SetOperationStatus("Đang render thành phẩm audio…");
                    try
                    {
                        await PhilosophyBatchAudioPreviewHelper.RenderFullMixPreviewAsync(
                            batch,
                            settings,
                            profile,
                            LogPhilosophy,
                            CancellationToken.None,
                            quoteIndices,
                            video,
                            progress =>
                            {
                                if (audioDlg == null || audioDlg.IsDisposed || string.IsNullOrWhiteSpace(progress))
                                {
                                    return;
                                }

                                audioDlg.BeginInvoke(new Action(() => audioDlg.SetOperationStatus(progress)));
                            }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (audioDlg != null && !audioDlg.IsDisposed)
                        {
                            audioDlg.BeginInvoke(new Action(() =>
                            {
                                MessageBox.Show(audioDlg, ex.Message, "Render Audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }));
                        }

                        LogPhilosophy("[Quote] Render audio lỗi: " + ex.Message);
                        return;
                    }

                    if (audioDlg != null && !audioDlg.IsDisposed)
                    {
                        audioDlg.BeginInvoke(new Action(() =>
                        {
                            audioDlg.RefreshPhilosophyQuoteAudioUi();
                            audioDlg.SetOperationStatus(string.Empty);
                        }));
                    }
                },
                listenFullMixedAudioAsync: async () =>
                {
                    audioDlg?.SetOperationStatus("Đang phát thành phẩm audio…");
                    await ListenPhilosophyBatchFullMixPreviewAsync(batch, settings).ConfigureAwait(true);
                },
                canRenderFullMixedAudio: CanRenderFullMix,
                canListenFullMixedAudio: CanListenFullMix,
                philosophyAmbientKey: batch.AmbientKey,
                philosophyMusicLibrarySummary: musicSummary,
                philosophyBatch: batch);

            using (audioDlg)
            {
                if (audioDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            PhilosophyBatchShowcaseAudioAdapter.ApplyFromShowcaseVideo(video, batch);
            PhilosophyBatchHelper.SyncBatchAudioSummaryFromQuotes(batch);
            batch.RefreshDerivedFields();
            _philosophyBatchBindingList?.ResetBindings();
            dgvPhilosophyScripts?.InvalidateRow(gridRowIndex);
            NotifyPhilosophyDraftDirty();
        }

        private Task ListenPhilosophyBatchVoicePreviewAsync(PhilosophyBatchItem batch, AppSettings settings)
        {
            var sessionBase = PhilosophyBatchAudioPreviewHelper.GetSessionBase(batch, settings);
            var path = PhilosophyBatchAudioPreviewHelper.ResolveVoicePreviewPath(sessionBase);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this,
                    "Chưa có audio quote.\r\n\r\nBấm «Tạo audio thân» (giọng đọc quote) trước.",
                    "Nghe quote",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LogPhilosophy("[Quote] Nghe quote → " + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Nghe quote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return Task.CompletedTask;
        }

        private Task ListenPhilosophyBatchFullMixPreviewAsync(PhilosophyBatchItem batch, AppSettings settings)
        {
            var sessionBase = PhilosophyBatchAudioPreviewHelper.GetSessionBase(batch, settings);
            var path = PhilosophyBatchAudioPreviewHelper.GetFullMixPreviewPath(sessionBase);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this,
                    "Chưa có file thành phẩm audio.\r\n\r\nBấm «Render Audio» để ghép quote, nhạc nền và tiếng đệm trước.",
                    "Nghe thành phẩm",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LogPhilosophy("[Quote] Nghe thành phẩm → " + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Nghe thành phẩm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return Task.CompletedTask;
        }
    }
}
