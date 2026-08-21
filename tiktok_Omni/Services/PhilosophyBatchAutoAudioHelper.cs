using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Sau Gemini sinh nội dung — tự TTS + render mix preview cho batch.</summary>
    public static class PhilosophyBatchAutoAudioHelper
    {
        public static async Task TryGenerateBatchAudioAsync(
            PhilosophyBatchItem batch,
            AppSettings settings,
            string profileName,
            VideoProcessingService videoProcessingService,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return;
            }

            if (videoProcessingService == null)
            {
                throw new ArgumentNullException(nameof(videoProcessingService));
            }

            PhilosophyBatchHelper.EnsureBatchAudioDefaults(batch, settings);
            var quoteCount = batch.Quotes.Count;
            var sessionBase = PhilosophyBatchAudioPreviewHelper.GetSessionBase(batch, settings);
            PhilosophyBatchAudioPreviewHelper.ClearAllQuotePreviewFiles(sessionBase, quoteCount);

            var video = PhilosophyBatchShowcaseAudioAdapter.ToShowcaseVideo(batch, profileName);
            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
            PhilosophyBatchShowcaseAudioAdapter.ApplyFromShowcaseVideo(video, batch);

            var edgeLabel = HookStyleCatalog.GetDisplayName(batch.BodyStyleKey);
            log?.Invoke("[Quote] Audio tự động — Edge «" + edgeLabel + "», "
                         + quoteCount + " câu (thoại → mix thành phẩm)…");

            await PhilosophyBatchAudioPreviewHelper.GenerateQuoteVoicePreviewAsync(
                batch,
                video,
                settings,
                profileName,
                videoProcessingService,
                log,
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await PhilosophyBatchAudioPreviewHelper.RenderFullMixPreviewAsync(
                batch,
                settings,
                profileName,
                log,
                cancellationToken,
                video: video).ConfigureAwait(false);

            log?.Invoke("[Quote] Audio thành phẩm xong batch «"
                         + (batch.Topic ?? string.Empty).Trim()
                         + "» — mở «Âm thanh» để nghe/chỉnh.");
        }
    }
}
