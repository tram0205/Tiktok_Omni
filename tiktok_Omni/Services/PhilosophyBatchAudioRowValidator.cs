using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Trạng thái audio từng dòng trong popup Âm thanh Quote.</summary>
    public static class PhilosophyBatchAudioRowValidator
    {
        public sealed class RowValidationResult
        {
            public bool IsValid { get; set; }

            public string Summary { get; set; } = string.Empty;

            public string Detail { get; set; } = string.Empty;
        }

        public static RowValidationResult Validate(
            PhilosophyScriptItem quote,
            int quoteIndex,
            PhilosophyBatchItem batch,
            string sessionBase,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            var result = new RowValidationResult();
            if (quote == null)
            {
                result.Summary = "!";
                result.Detail = "Thiếu dòng quote.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(quote.Content))
            {
                result.Summary = "!";
                result.Detail = "Thiếu nội dung quote.";
                return result;
            }

            var hasVoice = PhilosophyBatchAudioPreviewHelper.HasQuoteVoicePreview(sessionBase, quoteIndex);
            var voiceCurrent = hasVoice
                               && PhilosophyBatchAudioPreviewHelper.IsQuoteVoiceCurrent(
                                   sessionBase,
                                   quoteIndex,
                                   quote,
                                   batch,
                                   video,
                                   settings);

            if (!hasVoice)
            {
                result.Summary = "!";
                result.Detail = "Chưa có thoại — bấm «Tạo audio thoại».";
                return result;
            }

            if (!voiceCurrent)
            {
                if (PhilosophyBatchAudioPreviewHelper.IsQuoteVoiceStaleOnlyDueToSpeed(
                        sessionBase,
                        quoteIndex,
                        quote,
                        batch,
                        video,
                        settings))
                {
                    result.Summary = "◐";
                    result.Detail = "Tốc độ đổi — đang cập nhật thoại… bấm «Render audio» để ghép mix.";
                    return result;
                }

                result.Summary = "!";
                result.Detail = "Quote hoặc cài đặt giọng đã đổi — bấm «Tạo lại thoại».";
                return result;
            }

            var hasMix = PhilosophyBatchAudioPreviewHelper.HasQuoteFullMixPreview(sessionBase, quoteIndex);
            var mixCurrent = hasMix
                             && PhilosophyBatchAudioPreviewHelper.IsQuoteMixCurrent(
                                 sessionBase,
                                 quoteIndex,
                                 quote,
                                 batch,
                                 video,
                                 settings);

            if (!hasMix)
            {
                result.Summary = "◐";
                result.Detail = "Có thoại · chưa render mix — bấm «Render audio».";
                return result;
            }

            if (!mixCurrent)
            {
                result.Summary = "◐";
                result.Detail = "Thoại/nhạc/đệm đổi — bấm «Render audio» (không cần tạo lại thoại).";
                return result;
            }

            result.IsValid = true;
            result.Summary = "✓";
            result.Detail = "Thoại + mix sẵn sàng render video.";
            return result;
        }
    }
}
