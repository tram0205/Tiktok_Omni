using System;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Sinh VoiceoverTranscript xem trước (Gemini text) trước khi Deep Dive / Render — không tải full video.</summary>
    public sealed class AffiliateScriptPreviewService
    {
        private readonly GeminiService _gemini = new GeminiService();

        public async Task<string> GenerateVoiceoverPreviewAsync(
            AffiliateCandidate candidate,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                throw new InvalidOperationException("Cần AI API Key để sinh script.");
            }

            var context =
                "Sản phẩm/chủ đề: " + (candidate.ProductName ?? string.Empty) + "\r\n" +
                "Hashtag: " + (candidate.Hashtags ?? string.Empty) + "\r\n" +
                "Anchor: " + (candidate.LinkedProduct ?? string.Empty) + "\r\n" +
                "Ngách: " + (candidate.Category ?? string.Empty) + "\r\n" +
                "Từ khoá: " + (candidate.SourceKeyword ?? string.Empty);

            var prompt =
                "Viết lời thoại voiceover tiếng Việt (30-45 giây đọc) cho video TikTok affiliate dựa trên thông tin sau. " +
                "Giọng tự nhiên, thuyết phục, có CTA nhẹ. Chỉ trả nội dung lời thoại, không markdown.\r\n\r\n" +
                context;

            var raw = await _gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            return (raw ?? string.Empty).Trim();
        }

        public async Task<string> GenerateVoiceoverPreviewAsync(
            AiVideoGenInputItem item,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                throw new InvalidOperationException("Cần AI API Key để sinh script.");
            }

            var context =
                "Sản phẩm/chủ đề: " + (item.ProductName ?? string.Empty) + "\r\n" +
                "Giá: " + (item.Price ?? string.Empty) + "\r\n" +
                "Ngách: " + (item.Category ?? string.Empty) + "\r\n" +
                "Từ khoá: " + (item.SourceKeyword ?? string.Empty) + "\r\n" +
                "Link affiliate: " + (item.AffiliateLink ?? string.Empty);

            var prompt =
                "Viết lời thoại voiceover tiếng Việt (30-45 giây đọc) cho video TikTok affiliate dựa trên thông tin sau. " +
                "Giọng tự nhiên, thuyết phục, có CTA nhẹ. Chỉ trả nội dung lời thoại, không markdown.\r\n\r\n" +
                context;

            var raw = await _gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            return (raw ?? string.Empty).Trim();
        }
    }
}
